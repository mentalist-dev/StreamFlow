using System;
using System.Collections.Generic;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq.Server
{
    public class RabbitMqExecutionContext : IExecutionContext
    {
        public RabbitMqExecutionContext(BasicDeliverEventArgs @event)
        {
            Content = @event.Body;
            Headers = @event.BasicProperties?.Headers ?? new Dictionary<string, object>();
            CorrelationId = @event.BasicProperties?.CorrelationId;
            RoutingKey = @event.RoutingKey;
            Exchange = @event.Exchange;
            ConsumerTag = @event.ConsumerTag;
            DeliveryTag = @event.DeliveryTag;
            Event = @event;
        }

        public ReadOnlyMemory<byte> Content { get; }
        public IDictionary<string, object> Headers { get; }
        public string? CorrelationId { get; }
        public string? RoutingKey { get; }
        public string Exchange { get; }
        public string ConsumerTag { get; }
        public ulong DeliveryTag { get; }
        public BasicDeliverEventArgs Event { get; }
    }
}