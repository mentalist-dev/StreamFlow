using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace StreamFlow.RabbitMq
{
    public class RabbitMqPublisher : IPublisher, IDisposable
    {
        private readonly IRabbitMqConventions _conventions;
        private readonly IMessageSerializer _messageSerializer;
        private readonly ILogger<RabbitMqPublisher> _logger;
        private readonly Lazy<RabbitMqChannel> _model;

        public RabbitMqPublisher(IRabbitMqConnection connection, IRabbitMqConventions conventions, IMessageSerializer messageSerializer, ILogger<RabbitMqPublisher> logger)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            _conventions = conventions;
            _messageSerializer = messageSerializer;
            _logger = logger;
            _model = new Lazy<RabbitMqChannel>(() => new RabbitMqChannel(connection.Create()));
        }

        protected IModel Channel => _model.Value.Channel;

        public void Publish<T>(T message, PublishOptions? options = null) where T: class
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var exchange = _conventions.GetExchangeName(message.GetType());

            var routingKey = options?.RoutingKey;
            if (string.IsNullOrWhiteSpace(routingKey))
            {
                routingKey = "#";
            }

            var isMandatory = options?.IsMandatory ?? false;

            var properties = Channel.CreateBasicProperties();
            properties.Headers ??= new Dictionary<string, object>();

            var headers = options?.Headers;
            if (headers is {Count: > 0})
            {
                foreach (var header in headers)
                {
                    properties.Headers[header.Key] = header.Value;
                }
            }

            var correlationId = options?.CorrelationId;
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            properties.CorrelationId = correlationId;
            properties.ContentType = _messageSerializer.GetContentType<T>();
            properties.Headers["SF:PublishTime"] = DateTime.UtcNow.ToString("O");

            var body = _messageSerializer.Serialize(message);

            _logger.LogDebug(
                "Publishing message to exchange [{ExchangeName}] using routing key [{RoutingKey}] with CorrelationId [{CorrelationId}]",
                exchange, routingKey, correlationId);

            Channel.BasicPublish(exchange, routingKey, isMandatory, properties, body);
        }

        public Task PublishAsync<T>(T message, PublishOptions? options = null) where T : class
        {
            Publish(message, options);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_model.IsValueCreated)
            {
                var value = _model.Value;
                value.Dispose();
            }
        }
    }
}