using System.Collections.Concurrent;

namespace StreamFlow
{
    public interface IPublisher
    {
        Task<PublishResponse> PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T : class;
    }

    public class PublishOptions
    {
        public Dictionary<string, object> Headers { get; } = new();

        public string? CorrelationId { get; set; }
        public string? RoutingKey { get; set; }
        public bool IsMandatory { get; set; }

        public bool PublisherConfirmsEnabled { get; set; }
        public TimeSpan? PublisherConfirmsTimeout { get; set; }

        public string? TargetAddress { get; set; }
    }

    public class PublishResponse
    {
        private readonly ConcurrentBag<PublishResponseResult> _acknowledged = new();
        private readonly ConcurrentBag<PublishResponseResult> _rejected = new();

        public ulong? SequenceNo { get; private set; }

        public void Sequence(ulong sequenceNo)
        {
            SequenceNo = sequenceNo;
        }

        public void Acknowledged(ulong sequenceNo, bool multiple)
        {
            _acknowledged.Add(new PublishResponseResult(sequenceNo, multiple));
        }

        public void Rejected(ulong sequenceNo, bool multiple)
        {
            _rejected.Add(new PublishResponseResult(sequenceNo, multiple));
        }
    }

    public class PublishResponseResult
    {
        public ulong SequenceNo { get; }
        public bool Multiple { get; }

        public PublishResponseResult(ulong sequenceNo, bool multiple)
        {
            SequenceNo = sequenceNo;
            Multiple = multiple;
        }
    }
}
