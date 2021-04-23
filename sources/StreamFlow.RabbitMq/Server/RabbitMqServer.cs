using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqServer
    {
        void Start(IRegistration registration);
        void Stop();
    }

    public class RabbitMqServer: IRabbitMqServer, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly IRabbitMqConventions _conventions;
        private readonly Lazy<IConnection> _connection;
        private readonly List<RabbitMqConsumer> _consumers = new();
        private readonly ILogger<RabbitMqConsumer> _logger;

        public RabbitMqServer(IServiceProvider services, IRabbitMqConnection connection, IRabbitMqConventions conventions, ILoggerFactory loggers)
        {
            _services = services;
            _conventions = conventions;
            _connection = new Lazy<IConnection>(connection.Create);

            _logger = loggers.CreateLogger<RabbitMqConsumer>();
        }

        public void Start(IRegistration registration)
        {
            var requestType = registration.RequestType;
            var consumerType = registration.ConsumerType;
            var consumerGroup = registration.Options.ConsumerGroup;
            var autoAck = registration.Options.AutoAck;

            var connection = _connection.Value;

            var exchange = _conventions.GetExchangeName(requestType);
            var queue = _conventions.GetQueueName(consumerType, consumerGroup);
            var routingKey = "#";

            if (string.IsNullOrWhiteSpace(exchange))
                throw new Exception($"Unable to resolve exchange name from request type [{requestType}]");

            if (string.IsNullOrWhiteSpace(queue))
                throw new Exception($"Unable to resolve queue name from consumer type [{consumerType}] with consumer group [{consumerGroup}]");

            EnsureTopology(connection, registration.Options.Queue, exchange, queue, routingKey);

            var consumerCount = registration.Options.ConsumerCount;
            if (consumerCount < 1)
            {
                consumerCount = 1;
            }

            for (int i = 0; i < consumerCount; i++)
            {
                var channel = connection.CreateModel();

                var consumerInfo = new RabbitMqConsumerInfo(exchange, queue, routingKey, autoAck);
                _logger.LogInformation(
                    "Creating consumer {ConsumerIndex}/{ConsumerCount} consumer. Consumer info: {@ConsumerInfo}.",
                    i + 1, consumerCount, consumerInfo);

                var consumer = new RabbitMqConsumer(_services, channel, consumerInfo, _logger);

                _consumers.Add(consumer);

                consumer.Start(registration);
            }
        }

        public void Stop()
        {
            foreach (var consumer in _consumers)
            {
                consumer.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var consumer in _consumers)
            {
                consumer.Dispose();
            }

            if (_connection.IsValueCreated)
            {
                _connection.Value.Dispose();
            }
        }

        private void EnsureTopology(IConnection connection, QueueOptions? queueOptions, string exchange, string queue, string routingKey)
        {
            using var channel = connection.CreateModel();

            var durable = queueOptions?.Durable ?? false;
            var exclusive = queueOptions?.Exclusive ?? false;
            var autoDelete = queueOptions?.AutoDelete ?? false;
            var arguments = queueOptions?.Arguments;

            _logger.LogInformation("Declaring exchange: [{ExchangeName}].", exchange);
            channel.ExchangeDeclare(exchange, ExchangeType.Topic);

            _logger.LogInformation("Declaring queue: [{QueueName}]. Routing key = [{RoutingKey}]. Queue options = {@QueueOptions}.", queue, routingKey, queueOptions);
            channel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);

            _logger.LogInformation("Binding exchange [{ExchangeName}] to queue [{QueueName}] using routing key [{RoutingKey}].", exchange, queue, routingKey);
            channel.QueueBind(queue, exchange, routingKey);
        }
    }
}