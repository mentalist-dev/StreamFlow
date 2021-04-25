using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq
{
    public class RabbitMqConsumerMessageContext : MessageContext
    {
        public RabbitMqConsumerMessageContext(BasicDeliverEventArgs @event) : base(@event.Body)
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
        public RabbitMqPublisherMessageContext(ReadOnlyMemory<byte> content) : base(content)
        {
        }
    }

    public static class BasicPropertiesExtensions
    {
        public static void MapTo(this IBasicProperties? properties, IMessageContext message)
        {
            if (properties == null)
                return;

            var headers = properties.Headers;
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    message.SetHeader(header.Key, header.Value);
                }
            }

            message.WithContentEncoding(properties.ContentEncoding);
            message.WithContentType(properties.ContentType);
            message.WithCorrelationId(properties.CorrelationId);
            message.WithClusterId(properties.ClusterId);
            message.WithAppId(properties.AppId);
            message.WithMessageId(properties.MessageId);

            message.WithDeliveryMode(properties.DeliveryMode == 0
                ? MessageDeliveryMode.NonPersistent
                : MessageDeliveryMode.Persistent);

            message.WithPriority(properties.Priority);
            message.WithReplyTo(properties.ReplyTo);
            message.WithType(properties.Type);
            message.WithUserId(properties.UserId);
        }

        public static void MapTo(this IMessageContext message, IBasicProperties properties)
        {
            properties.Headers ??= new Dictionary<string, object>();

            var headers = message.Headers;
            if (headers is { Count: > 0 })
            {
                foreach (var header in headers)
                {
                    properties.Headers[header.Key] = header.Value;
                }
            }

            if (!string.IsNullOrWhiteSpace(message.ContentEncoding))
            {
                properties.ContentEncoding = message.ContentEncoding;
            }

            if (!string.IsNullOrWhiteSpace(message.ContentType))
            {
                properties.ContentType = message.ContentType;
            }

            if (!string.IsNullOrWhiteSpace(message.CorrelationId))
            {
                properties.CorrelationId = message.CorrelationId;
            }

            if (!string.IsNullOrWhiteSpace(message.ClusterId))
            {
                properties.ClusterId = message.ClusterId;
            }

            if (!string.IsNullOrWhiteSpace(message.AppId))
            {
                properties.AppId = message.AppId;
            }

            if (!string.IsNullOrWhiteSpace(message.MessageId))
            {
                properties.MessageId = message.MessageId;
            }

            if (message.DeliveryMode != null)
            {
                switch (message.DeliveryMode)
                {
                    case MessageDeliveryMode.NonPersistent:
                        properties.DeliveryMode = 0;
                        break;
                    case MessageDeliveryMode.Persistent:
                        properties.DeliveryMode = 1;
                        break;
                }
            }

            if (message.Priority != null)
            {
                properties.Priority = message.Priority.Value;
            }

            if (!string.IsNullOrWhiteSpace(message.ReplyTo))
            {
                properties.ReplyTo = message.ReplyTo;
            }

            if (!string.IsNullOrWhiteSpace(message.Type))
            {
                properties.Type = message.Type;
            }

            if (!string.IsNullOrWhiteSpace(message.UserId))
            {
                properties.UserId = message.UserId;
            }
        }
    }
}
