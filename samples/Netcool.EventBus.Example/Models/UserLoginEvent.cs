namespace Netcool.EventBus.Example.Models
{
    public class UserLoginEvent:Event
    {
        public string UserName { get; set; }
    }
}