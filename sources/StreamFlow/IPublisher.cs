using System.Collections.Concurrent;

namespace StreamFlow
{
    public interface IPublisher
    {
        Task PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T : class;
    }

    public class PublishResponse
    {
        private readonly ConcurrentBag<PublishResponseResult> _acknowledged = new();
        private readonly ConcurrentBag<PublishResponseResult> _rejected = new();

        public ulong? SequenceNo { get; }

        public PublishResponse(ulong? sequenceNo)
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
