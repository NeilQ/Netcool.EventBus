# Netcool.EventBus
一个基于netstandard2的事件总线, 支持RabbitMq与Mqtt。

大部分代码来自于 [dotnet-architecture/eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers)， 并且做了一些改动:
- 用asp.net core内置的ioc container替换了Autofac。
- 添加了一些便于注册事件总线的扩展方法
- RabbitMq EventBus的Publish方法将延迟创建连接Channel。
- 修改了一些类名。

## 安装

你可以在nuget.org搜索`Netcool.EventBus.RabbitMq`或者`Netcool.EventBus.Mqtt`找到它。

## 添加 RabbitMq 事件总线

```csharp
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

## 添加Mqtt事件总线
```c#
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddEventBusMqtt(ops =>
    {
        ops.TcpIp = "localhost";
        ops.TcpPort = 1883;
        ops.ClientId = "test";
        ops.Username = "";
        ops.Password = "";
        ops.PublishRetainedMessage = true;
        ops.RetryCount = 5;
        ops.CleanSession = false;    });
}
```

## 添加自定义事件总线

```c#
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddEventBus<CustomEventBus>();
}
```

## 发布

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

## 订阅 
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

## 自定义事件名称
有两种方式自定义事件名称:

### EventNameAttribute
```c#
[EventName("user-login")]
public class UserLoginEvent:Event
{
   public string UserName { get; set; }
}
```
事件名称在RabbitMq中代表RoutingKey，在Mqtt中的代表Topic

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






