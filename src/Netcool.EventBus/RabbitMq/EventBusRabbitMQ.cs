using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Netcool.EventBus
{
    public class EventBusRabbitMq : IEventBus, IDisposable
    {
        private readonly string _brokerName;
        private readonly IRabbitMqPersistentConnection _persistentConnection;
        private readonly ILogger<EventBusRabbitMq> _logger;
        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly int _retryCount;
        private IModel _consumerChannel;
        private readonly string _queueName;
        private readonly string _exchangeType;

        private readonly IServiceProvider _services;

        public EventBusRabbitMq(
            IServiceProvider services,
            IOptionsMonitor<EventBusRabbitMqOptions> options,
            IRabbitMqPersistentConnection persistentConnection, ILogger<EventBusRabbitMq> logger,
            IEventBusSubscriptionsManager subsManager)
        {
            _logger = logger;
            _services = services;
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _subsManager = subsManager ?? new EventBusSubscriptionsManager();
            _queueName = options.CurrentValue.QueueName;
            _retryCount = options.CurrentValue.RetryCount;
            _brokerName = options.CurrentValue.BrokerName ?? "event_bus";
            _exchangeType = "direct";
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }
            using (var channel = _persistentConnection.CreateModel())
            {
                channel.QueueUnbind(queue: _queueName,
                    exchange: _brokerName,
                    routingKey: eventName);
                if (_subsManager.IsEmpty)
                {
                    _consumerChannel.Close();
                }
            }
        }

        public void Publish(Event @event)
        {
            _consumerChannel = CreateConsumerChannel();
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }
            var policy = Policy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    _logger.LogWarning(ex.ToString());
                });
            using (var channel = _persistentConnection.CreateModel())
            {
                var eventName = @event.GetType()
                    .Name;
                channel.ExchangeDeclare(exchange: _brokerName,
                                    type: _exchangeType);
                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);
                policy.Execute(() =>
                {
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2; // persistent
                    channel.BasicPublish(exchange: _brokerName,
                                     routingKey: eventName,
                                     mandatory: true,
                                     basicProperties: properties,
                                     body: body);
                });
            }
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicEventHandler
        {
            _consumerChannel = CreateConsumerChannel();
            DoInternalSubscription(eventName);
            _subsManager.AddDynamicSubscription<TH>(eventName);
        }

        public void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            _consumerChannel = CreateConsumerChannel();
            var eventName = _subsManager.GetEventKey<T>();
            DoInternalSubscription(eventName);
            _subsManager.AddSubscription<T, TH>();
        }

        private void DoInternalSubscription(string eventName)
        {
            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);
            if (!containsKey)
            {
                if (!_persistentConnection.IsConnected)
                {
                    _persistentConnection.TryConnect();
                }
                using (var channel = _persistentConnection.CreateModel())
                {
                    channel.QueueBind(queue: _queueName,
                                      exchange: _brokerName,
                                      routingKey: eventName);
                }
            }
        }

        public void Unsubscribe<T, TH>()
            where TH : IEventHandler<T>
            where T : Event
        {
            _subsManager.RemoveSubscription<T, TH>();
        }

        public void UnsubscribeDynamic<TH>(string eventName)
            where TH : IDynamicEventHandler
        {
            _subsManager.RemoveDynamicSubscription<TH>(eventName);
        }

        public void Dispose()
        {
            _consumerChannel?.Dispose();
            _subsManager.Clear();
        }

        private IModel CreateConsumerChannel()
        {
            if (_consumerChannel != null) return _consumerChannel;
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }
            var channel = _persistentConnection.CreateModel();
            channel.ExchangeDeclare(exchange: _brokerName,
                                 type: "direct");
            channel.QueueDeclare(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var eventName = ea.RoutingKey;
                var message = Encoding.UTF8.GetString(ea.Body);
                await ProcessEvent(eventName, message);
                channel.BasicAck(ea.DeliveryTag, multiple: false);
            };
            channel.BasicConsume(queue: _queueName,
                                 autoAck: false,
                                 consumer: consumer);
            channel.CallbackException += (sender, ea) =>
            {
                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
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
                            if (!(scope.ServiceProvider.GetRequiredService(subscription.HandlerType) is IDynamicEventHandler handler))
                            {
                                throw new NullReferenceException($"Cannot find EventHandler, type {subscription.HandlerType.Name}");
                            }
                            dynamic eventData = JObject.Parse(message);
                            await handler.Handle(eventData);
                        }
                        else
                        {
                            var eventType = _subsManager.GetEventTypeByName(eventName);
                            var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                            var handler = scope.ServiceProvider.GetRequiredService(subscription.HandlerType);
                            var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);
                            await (Task)concreteType.GetMethod("Handle").Invoke(handler, new[] { integrationEvent });
                        }
                    }
                    processed = true;
                }
            }

            return processed;
        }
    }
}