using System.Threading.Channels;

namespace StreamFlow.RabbitMq.Publisher;

internal interface IRabbitMqPublicationQueue
{
    void Publish(RabbitMqPublication message);
    IAsyncEnumerable<RabbitMqPublication?> ReadAllAsync(CancellationToken cancellationToken);
    void Complete();
}

internal class RabbitMqPublicationQueue : IRabbitMqPublicationQueue
{
    private readonly Channel<RabbitMqPublication> _channel = Channel.CreateBounded<RabbitMqPublication>(100_000);

    public void Publish(RabbitMqPublication message)
    {
        var queued = _channel.Writer.TryWrite(message);

        if (!queued)
            throw new RabbitMqPublisherException(PublicationExceptionReason.InternalQueueIsFull);
    }

    public IAsyncEnumerable<RabbitMqPublication?> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
