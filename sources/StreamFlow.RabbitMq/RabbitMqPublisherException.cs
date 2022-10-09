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
    ChannelReset,
    Rejected,
    Returned,
    ExchangeNotFound,
    InternalQueueIsFull
}
