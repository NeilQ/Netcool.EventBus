using System;

namespace Netcool.EventBus
{
    public class EventNameAttribute : Attribute
    {
        public string Name { get; set; }

        public EventNameAttribute(string name)
        {
            Name = name;
        }
    }
}