using System;

namespace Netcool.EventBus
{
    public class Event
    {
        public Guid Id { get; } = Guid.NewGuid();

        public DateTime CreationDate { get; } = DateTime.UtcNow;
    }
}
