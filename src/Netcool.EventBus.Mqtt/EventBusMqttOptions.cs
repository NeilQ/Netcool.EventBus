namespace Netcool.EventBus.Mqtt
{
    public class EventBusMqttOptions: EventBusOptions
    {
        public string ClientId { get; set; }

        public bool CleanSession { get; set; }
        
        public string TcpIp { get; set; }

        public int TcpPort { get; set; }
        
        public string Username { get; set; }

        public string Password { get; set; }
        
        public bool PublishRetainedMessage { get; set; }

        public int RetryCount { get; set; } = 5;
    }



}