namespace StreamFlow.RabbitMq;

public class RabbitMqPublisherException(PublicationExceptionReason reason) : Exception(reason.ToString())
{
    public PublicationExceptionReason Reason { get; } = reason;
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
