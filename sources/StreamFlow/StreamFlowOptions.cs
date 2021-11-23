using StreamFlow.Configuration;

namespace StreamFlow
{
    public class StreamFlowOptions
    {
        public string? ServiceId { get; set; }
        public string? QueuePrefix { get; set; }
        public string? ExchangePrefix { get; set; }
        public string? ErrorSuffix { get; set; }
        public string? Separator { get; set; }

        public StreamFlowDefaults? Default { get; set; }
    }

    public class StreamFlowDefaults
    {
        public QueueQuorumOptions? QuorumOptions { get; set; }
    }
}
