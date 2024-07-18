namespace Netcool.EventBus.Example.Models
{
    [MessageProperties(8000)]
    public class ExceptionEvent : Event
    {
        public string Name { get; set; }
    }
}
