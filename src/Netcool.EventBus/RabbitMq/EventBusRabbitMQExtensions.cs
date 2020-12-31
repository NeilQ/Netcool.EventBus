using System;
using Microsoft.Extensions.DependencyInjection;

namespace Netcool.EventBus
{
    public static class EventBusRabbitMQExtensions
    {
        public static void AddEventBusRabbitMq(this IServiceCollection services,
            Action<EventBusRabbitMqOptions> configureOptions)
        {
            var options = new EventBusRabbitMqOptions();
            configureOptions(options);
            services.Configure(configureOptions);

            services.AddSingleton<IRabbitMqPersistentConnection, DefaultRabbitMqPersistentConnection>();
            services.AddEventBus<EventBusRabbitMq>();
        }

      
    }
}