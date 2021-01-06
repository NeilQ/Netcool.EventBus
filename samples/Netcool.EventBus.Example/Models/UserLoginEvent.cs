namespace Netcool.EventBus.Example.Models
{
    [EventName("user-login")]
    public class UserLoginEvent : Event
    {
        public string UserName { get; set; }
    }
}