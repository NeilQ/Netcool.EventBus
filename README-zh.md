# Netcool.EventBus
一个基于Asp.net core 2.1与RabbitMq的事件总线。

大部分代码来自于 [dotnet-architecture/eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers)， 并且做了一些改动:
- 用asp.net core内置的ioc container替换了Autofac。
- 添加了一些便于注册事件总线的扩展方法
- RabbitMq EventBus的Publish方法将延迟创建连接Channel。
- 修改了一些类名。

## 安装

你可以在nuget.org搜索`Netcool.EventBus`找到它。

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

## 添加自定义事件总线

```csharp
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddEventBus<CustomEventBus>();
}
```

**!注意: 你必须在Asp.net core项目中启用Logging**

## 发布

```csharp
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
```csharp
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

```csharp
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{        
    app.UseMvc();

    var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
    eventBus.Subscribe<UserLoginEvent, UserLoginEventHandler>();
}
```







