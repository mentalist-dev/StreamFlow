using System.Diagnostics.CodeAnalysis;

namespace StreamFlow.RabbitMq.Publisher
{
    public interface IRabbitMqPublisherBus
    {
        bool Publish<TMessage>([DisallowNull] TMessage message, PublishOptions? options = null);
    }

    internal class RabbitMqPublisherBus: IRabbitMqPublisherBus
    {
        private readonly IRabbitMqPublisherChannel _channel;

        public RabbitMqPublisherBus(IRabbitMqPublisherChannel channel)
        {
            _channel = channel;
        }

        public bool Publish<TMessage>([DisallowNull] TMessage message, PublishOptions? options = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            return _channel.Publish(message, options);
        }
    }
}
