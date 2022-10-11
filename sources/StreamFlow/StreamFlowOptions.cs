using StreamFlow.Configuration;

namespace StreamFlow;

public class StreamFlowOptions
{
    public string? ServiceId { get; set; }
    public string? QueuePrefix { get; set; }
    public string? ExchangePrefix { get; set; }
    public string? ErrorQueueSuffix { get; set; }
    public string? Separator { get; set; }

    public StreamFlowDefaults? Default { get; set; }

    public StreamFlowOptions Service(string id)
    {
        ServiceId = id;
        return this;
    }

    public StreamFlowOptions Prefixes(string? queuePrefix = null, string? exchangePrefix = null, string? errorQueueSuffix = null)
    {
        QueuePrefix = queuePrefix;
        ExchangePrefix = exchangePrefix;
        ErrorQueueSuffix = errorQueueSuffix;
        return this;
    }

    public StreamFlowOptions ConsumerSeparator(string separator)
    {
        Separator = separator;
        return this;
    }
}

public class StreamFlowDefaults
{
    public QueueQuorumOptions? QuorumOptions { get; set; }
}
