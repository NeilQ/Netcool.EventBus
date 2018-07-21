using System;

namespace Netcool.EventBus
{
    public class Event
    {
        public Event()
        {
            Id = Guid.NewGuid();
            CreationDate = DateTime.UtcNow;
        }
        public Guid Id  { get; }
        public DateTime CreationDate { get; }
    }
}