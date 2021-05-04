using System;
using System.Threading;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq
{
    public class RabbitMqConsumerMessageContext : MessageContext
    {
        public RabbitMqConsumerMessageContext(BasicDeliverEventArgs @event, CancellationToken cancellationToken = default) : base(@event.Body, cancellationToken)
        {
            WithExchange(@event.Exchange);
            WithRoutingKey(@event.RoutingKey);

            @event.BasicProperties.MapTo(this);

            ConsumerTag = @event.ConsumerTag;
            DeliveryTag = @event.DeliveryTag;

            Event = @event;
        }

        public string ConsumerTag { get; }
        public ulong DeliveryTag { get; }
        public BasicDeliverEventArgs Event { get; }
    }

    public class RabbitMqPublisherMessageContext : MessageContext
    {
        public RabbitMqPublisherMessageContext(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default) : base(content, cancellationToken)
        {
        }
    }
}
