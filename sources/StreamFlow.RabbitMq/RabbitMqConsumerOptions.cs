namespace StreamFlow.RabbitMq;

public class RabbitMqConsumerOptions
{
    internal ushort? PrefetchCount { get; private set; }

    public RabbitMqConsumerOptions Prefetch(ushort prefetchCount)
    {
        PrefetchCount = prefetchCount;
        return this;
    }
}
