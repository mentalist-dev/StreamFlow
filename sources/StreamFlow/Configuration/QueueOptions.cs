using System.Collections.Generic;

namespace StreamFlow.Configuration
{
    public class QueueOptions: IQueueOptionsBuilder
    {
        public bool Durable { get; private set; } = true;
        public bool Exclusive { get; private set; }
        public bool AutoDelete { get; private set; }
        public IDictionary<string, object>? Arguments { get; private set; }

        public ExchangeOptions ExchangeOptions { get; } = new();

        IQueueOptionsBuilder IQueueOptionsBuilder.Durable(bool durable)
        {
            Durable = durable;
            return this;
        }

        IQueueOptionsBuilder IQueueOptionsBuilder.Exclusive(bool exclusive)
        {
            Exclusive = exclusive;
            return this;
        }

        IQueueOptionsBuilder IQueueOptionsBuilder.AutoDelete(bool autoDelete)
        {
            AutoDelete = autoDelete;
            return this;
        }

        IQueueOptionsBuilder IQueueOptionsBuilder.Argument(string key, object value)
        {
            Arguments ??= new Dictionary<string, object>();
            Arguments[key] = value;
            return this;
        }
    }

    public class ExchangeOptions
    {
        public bool? Durable { get; set; }
        public bool? AutoDelete { get; set; }
        public IDictionary<string, object>? Arguments { get; private set; }
    }
}
