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
        /// Indicates whether unbinding the queue with the event routing key when unsubscribed, default false.
        /// </summary>
        public bool UnbindOnUnsubscribe { get; set; }

        public QueueArguments QueueArguments { get; set; }
    }

    public class QueueArguments
    {
        /// <summary>
        /// How long a message published to a queue can live before it is discarded (milliseconds).
        /// </summary>
        public int MessageTTL { get; set; }

        /// <summary>
        /// How long a queue can be unused for before it is automatically deleted (milliseconds).
        /// </summary>
        public int Expires { get; set; }

        /// <summary>
        /// How many (ready) messages a queue can contain before it starts to drop them from its head.
        /// </summary>
        public int MaxLength { get; set; }

        /// <summary>
        /// Total body size for ready messages a queue can contain before it starts to drop them from its head.
        /// </summary>
        public int MaxLengthBytes { get; set; }
    }
}
