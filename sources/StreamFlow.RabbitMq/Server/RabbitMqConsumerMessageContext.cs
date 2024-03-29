using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq.Server;

public sealed class RabbitMqConsumerMessageContext : MessageContext
{
    public RabbitMqConsumerMessageContext(BasicDeliverEventArgs @event, bool finalRetry, long? retryCount) : base(@event.Body)
    {
        WithExchange(@event.Exchange);
        WithRoutingKey(@event.RoutingKey);
        SetFinalRetry(finalRetry);

        if (retryCount.HasValue)
        {
            SetRetryCount(retryCount.Value);
        }

        @event.BasicProperties.MapTo(this);

        ConsumerTag = @event.ConsumerTag;
        DeliveryTag = @event.DeliveryTag;

        Event = @event;
    }

    public string ConsumerTag { get; }
    public ulong DeliveryTag { get; }
    public BasicDeliverEventArgs Event { get; }
}
