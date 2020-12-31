using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Netcool.EventBus
{
    public static class EventBusExtensions
    {
        public static void AddEventBus<TEventBus>(this IServiceCollection services) where TEventBus : class, IEventBus
        {
            services.AddSingleton<IEventBus, TEventBus>();
            services.AddSingleton<IEventBusSubscriptionsManager, EventBusSubscriptionsManager>();
        }

        public static void UseEventBus(this IApplicationBuilder app, Action<IEventBus> eventBus)
        {
            var bus = app.ApplicationServices.GetRequiredService<IEventBus>();
            eventBus(bus);
        }
    }
}