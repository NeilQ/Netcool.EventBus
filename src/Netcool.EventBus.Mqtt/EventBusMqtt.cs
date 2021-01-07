using System;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using Polly;

namespace Netcool.EventBus.Mqtt
{
    public class EventBusMqtt : IEventBus
    {
        private readonly IServiceProvider _services;
        private readonly ILogger _logger;

        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly IMqttPersistentConnection _persistentConnection;
        private readonly EventBusMqttOptions _options;

        public EventBusMqtt(IServiceProvider services, ILogger<EventBusMqtt> logger,
            IEventBusSubscriptionsManager subsManager, IOptions<EventBusMqttOptions> options,
            IMqttPersistentConnection persistentConnection)
        {
            _services = services;
            _logger = logger;
            _persistentConnection = persistentConnection;
            _subsManager = subsManager ?? new EventBusSubscriptionsManager();
            _options = options.Value;

            StartMessageHandler();
        }

        public void Publish(Event @event)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            var policy = Policy.Handle<Exception>()
                .WaitAndRetry(_options.RetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, time) =>
                    {
                        _logger.LogWarning(ex,
                            "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id,
                            $"{time.TotalSeconds:n1}", ex.Message);
                    });
            var client = _persistentConnection.GetClient();
            var eventName = _subsManager.GetEventKey(@event);

            var message = JsonSerializer.Serialize(@event, @event.GetType());

            policy.Execute(() =>
            {
                var mqttMessageBuilder = new MqttApplicationMessageBuilder()
                    .WithTopic(eventName)
                    .WithPayload(message)
                    .WithExactlyOnceQoS();
                if (_options.PublishRetainedMessage)
                {
                    mqttMessageBuilder = mqttMessageBuilder.WithRetainFlag();
                }

                client.PublishAsync(mqttMessageBuilder.Build(), CancellationToken.None).Wait();
            });
        }

        public void Subscribe<T, TH>() where T : Event where TH : IEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            DoInternalSubscription(eventName);

            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName,
                typeof(TH).GetGenericTypeName());
            _subsManager.AddSubscription<T, TH>();
        }

        public void SubscribeDynamic<TH>(string eventName) where TH : IDynamicEventHandler
        {
            DoInternalSubscription(eventName);

            _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName,
                typeof(TH).GetGenericTypeName());
            _subsManager.AddDynamicSubscription<TH>(eventName);
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

                var client = _persistentConnection.GetClient();
                client.SubscribeAsync(eventName).Wait();
            }
        }

        private void StartMessageHandler()
        {
            _persistentConnection.GetClient().UseApplicationMessageReceivedHandler(MqttMessageHandler);
        }

        private async void MqttMessageHandler(MqttApplicationMessageReceivedEventArgs e)
        {
            var eventName = e.ApplicationMessage.Topic;
            var message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            /*
            _logger.LogInformation($@"### RECEIVED APPLICATION MESSAGE ###
                            Topic = {e.ApplicationMessage.Topic}
                            Payload = {message}
                            QoS = {e.ApplicationMessage.QualityOfServiceLevel}
                            Retain = {e.ApplicationMessage.Retain}");
                            */

            var processed = false;
            try
            {
                processed = await ProcessEvent(eventName, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "----- ERROR Processing message \"{Message}\"", message);
            }

            e.ProcessingFailed = !processed;
        }

        public void Unsubscribe<T, TH>() where T : Event where TH : IEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            _logger.LogInformation("Unsubscribing from event {EventName}", eventName);
            _subsManager.RemoveSubscription<T, TH>();
            _persistentConnection.GetClient().UnsubscribeAsync(eventName);
        }

        public void UnsubscribeDynamic<TH>(string eventName) where TH : IDynamicEventHandler
        {
            _logger.LogInformation("Unsubscribing from event {EventName}", eventName);
            _subsManager.RemoveDynamicSubscription<TH>(eventName);
            _persistentConnection.GetClient().UnsubscribeAsync(eventName);
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

                            dynamic eventData = JsonSerializer.Deserialize<ExpandoObject>(message);

                            await handler.Handle(eventData);
                        }
                        else
                        {
                            var eventType = _subsManager.GetEventTypeByName(eventName);
                            var integrationEvent = JsonSerializer.Deserialize(message, eventType);
                            var handler = scope.ServiceProvider.GetRequiredService(subscription.HandlerType);
                            var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

                            // ReSharper disable once PossibleNullReferenceException
                            await (Task) concreteType.GetMethod("Handle").Invoke(handler, new[] {integrationEvent});
                        }
                    }
                }

                processed = true;
            }
            else
            {
                _logger.LogWarning("No subscription for Mqtt event: {EventName}", eventName);
            }

            return processed;
        }
    }
}