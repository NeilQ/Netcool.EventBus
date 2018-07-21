﻿using Microsoft.Extensions.DependencyInjection;

namespace Netcool.EventBus
{
    public static class EventBusExtentions
    {
        public static void AddEventBus<TEventBus>(this IServiceCollection services) where TEventBus : class, IEventBus
        {
            services.AddSingleton<IEventBus, TEventBus>();
            services.AddSingleton<IEventBusSubscriptionsManager, EventBusSubscriptionsManager>();
        }
    }
}