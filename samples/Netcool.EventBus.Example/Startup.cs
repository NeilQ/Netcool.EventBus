﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Netcool.EventBus.Example.Models;
using Netcool.EventBus.Mqtt;
using Serilog;

namespace Netcool.EventBus.Example
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            services.AddEventBusRabbitMq(ops =>
            {
                ops.HostName = "localhost";
                ops.UserName = "guest";
                ops.Password = "guest";
                ops.RetryCount = 5;
                ops.QueueName = "event_bus_queue";
                ops.BrokerName = "event_bus";
                //ops.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

            /*
            services.AddEventBusMqtt(ops =>
            {
                ops.TcpIp = "localhost";
                ops.TcpPort = 1883;
                ops.ClientId = "test";
                ops.Username = "";
                ops.Password = "";
                ops.PublishRetainedMessage = true;
                ops.RetryCount = 5;
                ops.CleanSession = false;
            });
            */

            services.AddTransient<UserLoginEventHandler>();
            services.AddTransient<UserLoginDynamicEventHandler>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSerilogRequestLogging();

            app.UseRouting();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

            app.UseEventBus(eventBus =>
            {
                eventBus.SubscribeDynamic<UserLoginDynamicEventHandler>("UserLoginDynamicEvent");
                eventBus.Subscribe<UserLoginEvent, UserLoginEventHandler>();
            });
        }
    }
}