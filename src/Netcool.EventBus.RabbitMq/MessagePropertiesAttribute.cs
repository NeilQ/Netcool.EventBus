using System;

namespace Netcool.EventBus
{
    public class MessagePropertiesAttribute : Attribute
    {
        public int Expiration { get; set; }

        public MessagePropertiesAttribute(int expiration)
        {
            Expiration = expiration;
        }
    }
}
