using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace StreamFlow
{
    public interface IMessageContext
    {
        CancellationToken CancellationToken { get; }
        IReadOnlyDictionary<string, object> Headers { get; }
        ReadOnlyMemory<byte> Content { get; }
        string? ContentEncoding { get; }
        string? ContentType { get; }
        string? CorrelationId { get; }
        string? RoutingKey { get; }
        string? Exchange { get; }
        string? ClusterId { get; }
        string? AppId { get; }
        string? MessageId { get; }
        MessageDeliveryMode? DeliveryMode { get; }
        byte? Priority { get; }
        string? ReplyTo { get; }
        string? Type { get; }
        string? UserId { get; }

        IMessageContext SetHeader(string key, object value, bool overrideIfExists = false);
        IMessageContext RemoveHeader(string key);

        T? GetHeader<T>(string key, T? defaultValue);

        IMessageContext WithContentEncoding(string? contentEncoding);
        IMessageContext WithContentType(string? contentType);
        IMessageContext WithCorrelationId(string? correlationId);
        IMessageContext WithRoutingKey(string? routingKey);
        IMessageContext WithExchange(string? exchange);
        IMessageContext WithClusterId(string? clusterId);
        IMessageContext WithAppId(string? appId);
        IMessageContext WithMessageId(string? messageId);
        IMessageContext WithDeliveryMode(MessageDeliveryMode? deliveryMode);
        IMessageContext WithPriority(byte? priority);
        IMessageContext WithReplyTo(string? replyTo);
        IMessageContext WithType(string? type);
        IMessageContext WithUserId(string? userId);
    }

    public enum MessageDeliveryMode
    {
        NonPersistent,
        Persistent
    }

    public abstract class MessageContext : IMessageContext
    {
        private readonly Dictionary<string, object> _headers = new();

        public CancellationToken CancellationToken { get; }
        public IReadOnlyDictionary<string, object> Headers => _headers;
        public ReadOnlyMemory<byte> Content { get; }
        public string? ContentEncoding { get; private set; }
        public string? ContentType { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? RoutingKey { get; private set; }
        public string? Exchange { get; private set; }
        public string? ClusterId { get; private set; }
        public string? AppId { get; private set; }
        public string? MessageId { get; private set; }
        public MessageDeliveryMode? DeliveryMode { get; private set; }
        public byte? Priority { get; private set; }
        public string? ReplyTo { get; private set; }
        public string? Type { get; private set; }
        public string? UserId { get; private set; }

        protected MessageContext(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            Content = content;
        }

        public IMessageContext SetHeader(string key, object value, bool overrideIfExists = false)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

            if (_headers.ContainsKey(key) && !overrideIfExists)
            {
                return this;
            }

            _headers[key] = value ?? throw new ArgumentNullException(nameof(value));

            return this;
        }

        public IMessageContext RemoveHeader(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (_headers.ContainsKey(key))
            {
                _headers.Remove(key);
            }
            return this;
        }

        public T? GetHeader<T>(string key, T? defaultValue)
        {
            if (Headers.TryGetValue(key, out var value))
            {
                if (value.GetType() == typeof(T))
                {
                    return (T) value;
                }

                if (typeof(T) == typeof(string))
                {
                    // https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/415

                    object stringValue = value.GetType() == typeof(byte[])
                        ? Encoding.UTF8.GetString((byte[]) value)
                        : value.ToString()!;

                    return (T) stringValue;
                }

                if (typeof(T) == typeof(Guid))
                {
                    string guidValue = value.GetType() == typeof(byte[])
                        ? Encoding.UTF8.GetString((byte[])value)
                        : value.ToString()!;

                    return (T) (object) Guid.Parse(guidValue);
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }

            return defaultValue;
        }

        public IMessageContext WithContentEncoding(string? contentEncoding)
        {
            ContentEncoding = contentEncoding;
            return this;
        }

        public IMessageContext WithContentType(string? contentType)
        {
            ContentType = contentType;
            return this;
        }

        public IMessageContext WithCorrelationId(string? correlationId)
        {
            CorrelationId = correlationId;
            return this;
        }

        public IMessageContext WithRoutingKey(string? routingKey)
        {
            RoutingKey = routingKey;
            return this;
        }

        public IMessageContext WithExchange(string? exchange)
        {
            Exchange = exchange;
            return this;
        }

        public IMessageContext WithClusterId(string? clusterId)
        {
            ClusterId = clusterId;
            return this;
        }

        public IMessageContext WithAppId(string? appId)
        {
            AppId = appId;
            return this;
        }

        public IMessageContext WithMessageId(string? messageId)
        {
            MessageId = messageId;
            return this;
        }

        public IMessageContext WithDeliveryMode(MessageDeliveryMode? deliveryMode)
        {
            DeliveryMode = deliveryMode;
            return this;
        }

        public IMessageContext WithPriority(byte? priority)
        {
            Priority = priority;
            return this;
        }

        public IMessageContext WithReplyTo(string? replyTo)
        {
            ReplyTo = replyTo;
            return this;
        }

        public IMessageContext WithType(string? type)
        {
            Type = type;
            return this;
        }

        public IMessageContext WithUserId(string? userId)
        {
            UserId = userId;
            return this;
        }
    }
}
