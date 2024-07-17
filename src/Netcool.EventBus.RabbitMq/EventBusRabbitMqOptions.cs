namespace Netcool.EventBus
{
    public class EventBusRabbitMqOptions : EventBusOptions
    {
        public string HostName { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string QueueName { get; set; } = "event_bus_queue";

        public string BrokerName { get; set; } = "event_bus";

        public int RetryCount { get; set; } = 5;

        /// <summary>
        /// Indicates whether the event should be handled synchronously, default false.
        /// </summary>
        public bool HandleSynchronously { get; set; }

        /// <summary>
        /// Indicates whether unbinding the queue with the event routing key when unsubscribed, default true.
        /// </summary>
        public bool UnbindOnUnsubscribe { get; set; } = true;
    }
}
