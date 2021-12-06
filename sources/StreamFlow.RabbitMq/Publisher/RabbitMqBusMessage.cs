namespace StreamFlow.RabbitMq.Publisher;

internal class RabbitMqBusMessage
{
    public RabbitMqBusMessage(object message, PublishOptions? options)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Options = options;
    }

    public object Message { get; }
    public PublishOptions? Options { get; }
}
