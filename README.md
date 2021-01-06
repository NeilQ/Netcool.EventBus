[中文README](README-zh.md)

# Netcool.EventBus
An EventBus base on netstandard2.1 and RabbitMq. 

Most of codes retrived from [dotnet-architecture/eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers), however there are some changes:
- Replace Autofac with default asp.net core ioc container.
- Add some extension methods for adding event bus.
- Delayed to create rabbitmq channel for event publish.
- Class name changed.

## Install

You can find it at nuget.org with id `Netcool.EventBus`.

## Add RabbitMq EventBus

```c#
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddEventBusRabbitMq(ops =>
    {
       ops.HostName = "localhost";
       ops.UserName = "guest";
       ops.Password = "guest";   
       ops.QueueName = "event_bus_queue";
       ops.BrokerName = "event_bus";
       ops.RetryCount = 5;
    });
}
```

## Add Custom EventBus

```c#
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddEventBus<CustomEventBus>();
}
```

## Publish

```c#
public class UserLoginEvent:Event
{
   public string UserName { get; set; }
}

public class ValuesController : ControllerBase
{
  private readonly IEventBus _eventBus;

  public ValuesController(IEventBus eventBus)
  {
     _eventBus = eventBus;
  }

  [HttpGet]
  public ActionResult Get()
  {
     _eventBus.Publish(new UserLoginEvent() { UserName = "Peppa" });
     return Ok();
  }
}
```

## Subscribe

```c#
public class UserLoginEventHandler : IEventHandler<UserLoginEvent>
{
   private readonly ILogger<UserLoginEventHandler> _logger;

   public UserLoginEventHandler(ILogger<UserLoginEventHandler> logger)
   {
       _logger = logger;
   }

   public Task Handle(UserLoginEvent @event)
   {
       _logger.LogInformation($"Welcome {@event.UserName}!");
       return Task.FromResult(0);
   }
}
```

```c#
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{        
    ...
    
    app.UseEventBus(eventBus =>
    {
        eventBus.Subscribe<UserLoginEvent, UserLoginEventHandler>();
    });
}
```

## Customize event name
There's two ways to customize event name:

### EventNameAttribute
```c#
[EventName("user-login")]
public class UserLoginEvent:Event
{
   public string UserName { get; set; }
}
```

### Dynamic Event
```c#
public class UserLoginDynamicEventHandler : IDynamicEventHandler
{
    public Task Handle(dynamic eventData)
    {
        Console.WriteLine($"Welcome {eventData.UserName}!");
        return Task.FromResult(0);
    }
}
```

```c#
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{        
    ...
    
    app.UseEventBus(eventBus =>
    {
        eventBus.SubscribeDynamic<UserLoginDynamicEventHandler>("UserLoginDynamicEvent");
    });
}
```







