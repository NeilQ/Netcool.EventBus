using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Netcool.EventBus
{
    public class EventBusRabbitMq : IEventBus, IDisposable
    {
        private readonly IRabbitMqPersistentConnection _persistentConnection;
        private readonly ILogger<EventBusRabbitMq> _logger;
        private readonly IEventBusSubscriptionsManager _subsManager;
        private IModel _consumerChannel;
        private readonly string _exchangeType;

        private readonly EventBusRabbitMqOptions _options;

        private readonly IServiceProvider _services;

        public EventBusRabbitMq(
            IServiceProvider services,
            IOptions<EventBusRabbitMqOptions> options,
            IRabbitMqPersistentConnection persistentConnection, ILogger<EventBusRabbitMq> logger,
            IEventBusSubscriptionsManager subsManager)
        {
            _logger = logger;
            _services = services;
            _persistentConnection =
                persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _subsManager = subsManager;

            _options = options.Value;
            _exchangeType = "direct";
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            if (!_persistentConnection.IsConnected)
            {
                if (!_persistentConnection.TryConnect()) return;
            }

            using (var channel = _persistentConnection.CreateModel())
            {
                if (_options.UnbindOnUnsubscribe)
                {
                    channel.QueueUnbind(queue: _options.QueueName,
                        exchange: _options.BrokerName,
                        routingKey: eventName);
                }

                if (_subsManager.IsEmpty)
                {
                    _consumerChannel.Close();
                    _consumerChannel.Dispose();
                    _consumerChannel = null;
                }
            }
        }

        public void Publish(Event @event)
        {
            if (!_persistentConnection.IsConnected)
            {
                if (!_persistentConnection.TryConnect())
                {
                    _logger.LogError("Could not publish event: {EventId}", @event.Id);
                    return;
                }
            }

            var retry = Policy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(_options.RetryCount, retryAttempt => TimeSpan.FromSeconds(1),
                    (ex, time) =>
                    {
                        _logger.LogWarning(ex,
                            "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id,
                            $"{time.TotalSeconds:n1}", ex.Message);
                    });
            var fallback = Policy.Handle<Exception>()
                .Fallback(() => { }, ex => { _logger.LogError(ex, "Could not publish event: {EventId}", @event.Id); });

            fallback.Wrap(retry).Execute(() =>
            {
                using (var channel = _persistentConnection.CreateModel())
                {
                    var eventName = _subsManager.GetEventKey(@event);
                    channel.ExchangeDeclare(exchange: _options.BrokerName, type: _exchangeType);

                    var message =
                        JsonSerializer.Serialize(@event, @event.GetType(), _options.JsonSerializerOptions);
                    var body = Encoding.UTF8.GetBytes(message);
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2; // persistent

                    var expirationAttribute =
                        @event.GetType().GetTypeInfo().GetCustomAttribute<MessagePropertiesAttribute>();
                    if (expirationAttribute != null && expirationAttribute.Expiration > 0)
                        properties.Expiration = expirationAttribute.Expiration.ToString();

                    channel.BasicPublish(exchange: _options.BrokerName,
                        routingKey: eventName,
                        mandatory: true,
                        basicProperties: properties,
                        body: body);
                }
            });
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicEventHandler
        {
            _consumerChannel = CreateConsumerChannel();
            DoInternalSubscription(eventName);

            _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName,
                typeof(TH).GetGenericTypeName());
            _subsManager.AddDynamicSubscription<TH>(eventName);
            StartBasicConsume();
        }

        public void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            _consumerChannel = CreateConsumerChannel();
            var eventName = _subsManager.GetEventKey<T>();
            DoInternalSubscription(eventName);

            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName,
                typeof(TH).GetGenericTypeName());
            _subsManager.AddSubscription<T, TH>();
            StartBasicConsume();
        }

        private void DoInternalSubscription(string eventName)
        {
            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);
            if (containsKey) return;
            if (!_persistentConnection.IsConnected)
            {
                if (!_persistentConnection.TryConnect()) return;
            }

            using (var channel = _persistentConnection.CreateModel())
            {
                channel.QueueBind(queue: _options.QueueName,
                    exchange: _options.BrokerName,
                    routingKey: eventName);
            }
        }

        public void Unsubscribe<T, TH>()
            where TH : IEventHandler<T>
            where T : Event
        {
            var eventName = _subsManager.GetEventKey<T>();
            _logger.LogInformation("Unsubscribing from event {EventName}", eventName);
            _subsManager.RemoveSubscription<T, TH>();
        }

        public void UnsubscribeDynamic<TH>(string eventName)
            where TH : IDynamicEventHandler
        {
            _logger.LogInformation("Unsubscribing from event {EventName}", eventName);
            _subsManager.RemoveDynamicSubscription<TH>(eventName);
        }

        public void Dispose()
        {
            _consumerChannel?.Dispose();
            _subsManager.Clear();
        }

        private void StartBasicConsume()
        {
            _logger.LogTrace("Starting RabbitMQ basic consume");

            if (_consumerChannel != null)
            {
                var consumer = new EventingBasicConsumer(_consumerChannel);

                consumer.Received += Consumer_Received;

                _consumerChannel.BasicConsume(
                    queue: _options.QueueName,
                    autoAck: false,
                    consumer: consumer);
            }
            else
            {
                _logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
            }
        }

        private async void Consumer_Received(object model, BasicDeliverEventArgs ea)
        {
            var eventName = ea.RoutingKey;

#if NETSTANDARD2_1
            var message = Encoding.UTF8.GetString(ea.Body.Span);
#else
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
#endif

            try
            {
                var processed = _options.HandleSynchronously
                    ? ProcessEvent(eventName, message).GetAwaiter().GetResult()
                    : await ProcessEvent(eventName, message);

                if (processed)
                {
                    _consumerChannel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                else
                {
                    _consumerChannel.BasicNack(ea.DeliveryTag, false, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "----- ERROR Processing message \"{Message}\"", message);
            }
        }

        private IModel CreateConsumerChannel()
        {
            if (_consumerChannel != null) return _consumerChannel;
            if (!_persistentConnection.IsConnected)
            {
                if (!_persistentConnection.TryConnect()) return null;
            }

            _logger.LogTrace("Creating RabbitMQ consumer channel");
            var channel = _persistentConnection.CreateModel();

            channel.ExchangeDeclare(exchange: _options.BrokerName,
                type: _exchangeType);

            var arguments = new Dictionary<string, object>();
            if (_options.QueueArguments != null)
            {
                if (_options.QueueArguments.MessageTTL > 0)
                    arguments.Add("x-message-ttl", _options.QueueArguments.MessageTTL);
                if (_options.QueueArguments.Expires > 0)
                    arguments.Add("x-expires", _options.QueueArguments.Expires);
                if (_options.QueueArguments.MaxLength > 0)
                    arguments.Add("x-max-length", _options.QueueArguments.MaxLength);
                if (_options.QueueArguments.MaxLengthBytes > 0)
                    arguments.Add("x-max-length-bytes", _options.QueueArguments.MaxLengthBytes);
            }

            channel.QueueDeclare(queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments);
            _logger.LogInformation("Queue [{OptionsQueueName}] declared", _options.QueueName);

            channel.CallbackException += (sender, ea) =>
            {
                _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");
                _consumerChannel?.Dispose();
                _consumerChannel = CreateConsumerChannel();
                StartBasicConsume();
            };

            _logger.LogInformation("Channel created!");

            return channel;
        }

        private async Task<bool> ProcessEvent(string eventName, string message)
        {
            var processed = false;
            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                using (var scope = _services.CreateScope())
                {
                    var subscriptions = _subsManager.GetHandlersForEvent(eventName);
                    foreach (var subscription in subscriptions)
                    {
                        if (subscription.IsDynamic)
                        {
                            if (!(scope.ServiceProvider.GetRequiredService(subscription.HandlerType) is
                                    IDynamicEventHandler handler))
                            {
                                throw new NullReferenceException(
                                    $"Cannot find EventHandler, type {subscription.HandlerType.Name}");
                            }

                            dynamic eventData =
                                JsonSerializer.Deserialize<ExpandoObject>(message, _options.JsonSerializerOptions);

                            await handler.Handle(eventData);
                        }
                        else
                        {
                            var eventType = _subsManager.GetEventTypeByName(eventName);
                            var integrationEvent =
                                JsonSerializer.Deserialize(message, eventType, _options.JsonSerializerOptions);
                            var handler = scope.ServiceProvider.GetRequiredService(subscription.HandlerType);
                            var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

                            // ReSharper disable once PossibleNullReferenceException
                            await (Task)concreteType.GetMethod("Handle").Invoke(handler, new[] { integrationEvent });
                        }
                    }
                }

                processed = true;
            }
            else
            {
                _logger.LogWarning("No subscription for RabbitMQ event: {EventName}", eventName);
            }

            return processed;
        }
    }
}
