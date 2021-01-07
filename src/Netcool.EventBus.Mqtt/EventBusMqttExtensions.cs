using System;
using Microsoft.Extensions.DependencyInjection;

namespace Netcool.EventBus.Mqtt
{
    public static class EventBusMqttExtensions
    {
        public static void AddEventBusMqtt(this IServiceCollection services,
            Action<EventBusMqttOptions> configureOptions)
        {
            var options = new EventBusMqttOptions();
            configureOptions(options);
            services.Configure(configureOptions);

            services.AddSingleton<IMqttPersistentConnection, MqttPersistentConnection>();
            services.AddEventBus<EventBusMqtt>();
        }

      
    }
}