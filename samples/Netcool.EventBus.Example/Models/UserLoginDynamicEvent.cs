namespace Netcool.EventBus.Example.Models
{
    public class UserLoginDynamicEvent : Event
    {
        public string UserName { get; set; }
    }
}