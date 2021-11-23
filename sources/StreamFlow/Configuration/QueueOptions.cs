namespace StreamFlow.Configuration
{
    public class QueueOptions: IQueueOptionsBuilder
    {
        public bool Durable { get; private set; } = true;
        public bool Exclusive { get; private set; }
        public bool AutoDelete { get; private set; }
        public IDictionary<string, object> Arguments { get; } = new Dictionary<string, object>();

        public ExchangeOptions ExchangeOptions { get; } = new();
        public QueueQuorumOptions? QuorumOptions { get; private set; }

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
            Arguments[key] = value;
            return this;
        }

        IQueueOptionsBuilder IQueueOptionsBuilder.Quorum(int? initialGroupSize, bool enabled)
        {
            QuorumOptions ??= new QueueQuorumOptions();
            QuorumOptions.Enabled = enabled;
            QuorumOptions.Size = initialGroupSize;
            return this;
        }
    }
}
