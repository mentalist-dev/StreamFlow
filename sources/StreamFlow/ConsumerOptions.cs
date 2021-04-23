using System.Collections.Generic;

namespace StreamFlow
{
    public class ConsumerOptions
    {
        public string? ConsumerGroup { get; set; }
        public int ConsumerCount { get; set; }
        public bool AutoAck { get; set; }

        public QueueOptions? Queue { get; set; }
    }

    public class QueueOptions
    {
        public bool Durable { get; set; }
        public bool Exclusive { get; set; }
        public bool AutoDelete { get; set; }
        public IDictionary<string, object>? Arguments { get; set; }
    }
}
