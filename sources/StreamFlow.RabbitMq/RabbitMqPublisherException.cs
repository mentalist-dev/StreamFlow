using System.Runtime.Serialization;

namespace StreamFlow.RabbitMq;

[Serializable]
public class RabbitMqPublisherException : Exception
{
    public PublicationExceptionReason Reason { get; }

    public RabbitMqPublisherException(PublicationExceptionReason reason) : base(reason.ToString())
    {
        Reason = reason;
    }

    protected RabbitMqPublisherException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}

public enum PublicationExceptionReason
{
    ChannelReset = 0,
    Rejected = 1,
    Returned = 2,
    ExchangeNotFound = 3,
    InternalQueueIsFull = 4,
    PublisherStopped = 5,
    Cancelled = 6,
    OperationInterrupted = 7,
    Unknown = 255
}
