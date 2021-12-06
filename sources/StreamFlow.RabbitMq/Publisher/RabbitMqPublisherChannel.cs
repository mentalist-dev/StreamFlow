using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace StreamFlow.RabbitMq.Publisher;

internal interface IRabbitMqPublisherChannel
{
    bool Publish<TMessage>([DisallowNull] TMessage message, PublishOptions? options);
    IAsyncEnumerable<RabbitMqBusMessage?> ReadAllAsync(CancellationToken cancellationToken);
    void Complete();
}

internal class RabbitMqPublisherChannel : IRabbitMqPublisherChannel
{
    private readonly Channel<RabbitMqBusMessage> _channel = Channel.CreateBounded<RabbitMqBusMessage>(10000);

    public bool Publish<TMessage>([DisallowNull] TMessage message, PublishOptions? options)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        return _channel.Writer.TryWrite(new RabbitMqBusMessage(message, options));
    }

    public IAsyncEnumerable<RabbitMqBusMessage?> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
