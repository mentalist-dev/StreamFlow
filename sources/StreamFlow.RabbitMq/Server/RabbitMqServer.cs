using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using StreamFlow.Configuration;
using StreamFlow.RabbitMq.Connection;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqServer
    {
        void Start(IConsumerRegistration consumerRegistration);
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

        public void Start(IConsumerRegistration consumerRegistration)
        {
            var requestType = consumerRegistration.RequestType;
            var consumerType = consumerRegistration.ConsumerType;
            var consumerGroup = consumerRegistration.Options.ConsumerGroup;

            var connection = _connection.Value;

            var exchange = _conventions.GetExchangeName(requestType);
            var queue = _conventions.GetQueueName(requestType, consumerType, consumerGroup);
            var routingKey = "#";

            if (string.IsNullOrWhiteSpace(exchange))
                throw new Exception($"Unable to resolve exchange name from request type [{requestType}]");

            if (string.IsNullOrWhiteSpace(queue))
                throw new Exception($"Unable to resolve queue name from consumer type [{consumerType}] with consumer group [{consumerGroup}]");

            TryCreateTopology(connection, consumerRegistration.Options.Queue, exchange, queue, routingKey);

            var consumerCount = consumerRegistration.Options.ConsumerCount;
            if (consumerCount < 1)
            {
                consumerCount = 1;
            }

            for (int i = 0; i < consumerCount; i++)
            {
                var channel = connection.CreateModel();

                var consumerInfo = new RabbitMqConsumerInfo(exchange, queue, routingKey);
                var consumerIndex = i + 1;

                _logger.LogInformation(
                    "Creating consumer {ConsumerIndex}/{ConsumerCount} consumer. Consumer info: {@ConsumerInfo}.",
                    consumerIndex, consumerCount, consumerInfo);

                var consumer = new RabbitMqConsumer(_services, channel, consumerInfo, _logger);

                _consumers.Add(consumer);

                consumer.Start(consumerRegistration);
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

        private void TryCreateTopology(IConnection connection, QueueOptions? queueOptions, string exchange, string queue, string routingKey)
        {
            using var channel = connection.CreateModel();

            TryCreateExchange(exchange, channel);

            TryCreateQueue(queueOptions, queue, channel);

            TryCreateQueueAndExchangeBinding(exchange, queue, routingKey, channel);
        }

        private void TryCreateExchange(string exchange, IModel? channel)
        {
            try
            {
                _logger.LogInformation("Declaring exchange: [{ExchangeName}].", exchange);
                channel.ExchangeDeclare(exchange, ExchangeType.Topic);
            }
            catch (OperationInterruptedException e)
            {
                _logger.LogError(e, "Unable to declare exchange [{ExchangeName}]", exchange);

                // 406: PRECONDITION_FAILED, exchange exists, but with different properties
                if (e.ShutdownReason?.ReplyCode != 406)
                {
                    throw;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to declare exchange [{ExchangeName}]", exchange);
                throw;
            }
        }

        private void TryCreateQueue(QueueOptions? queueOptions, string queue, IModel channel)
        {
            var durable = queueOptions?.Durable ?? true;
            var exclusive = queueOptions?.Exclusive ?? false;
            var autoDelete = queueOptions?.AutoDelete ?? false;
            var arguments = queueOptions?.Arguments;

            try
            {
                _logger.LogInformation(
                    "Declaring queue: [{QueueName}]. Queue options = {@QueueOptions}.",
                    queue, queueOptions);

                channel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
            }
            catch (OperationInterruptedException e)
            {
                _logger.LogError(e, "Unable to declare queue [{QueueName}]. Queue options = {@QueueOptions}.", queue,
                    queueOptions);

                // 406: PRECONDITION_FAILED, queue exists, but with different properties
                if (e.ShutdownReason?.ReplyCode != 406)
                {
                    throw;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to declare queue [{QueueName}]. Queue options = {@QueueOptions}.", queue,
                    queueOptions);
                throw;
            }
        }

        private void TryCreateQueueAndExchangeBinding(string exchange, string queue, string routingKey, IModel? channel)
        {
            try
            {
                _logger.LogInformation(
                    "Binding exchange [{ExchangeName}] to queue [{QueueName}] using routing key [{RoutingKey}].", exchange,
                    queue, routingKey);

                channel.QueueBind(queue, exchange, routingKey);
            }
            catch (OperationInterruptedException e)
            {
                _logger.LogError(e,
                    "Unable to bind exchange [{ExchangeName}] to queue [{QueueName}] using routing key [{RoutingKey}].",
                    exchange, queue, routingKey);

                // 406: PRECONDITION_FAILED, binding exists, but with different properties
                if (e.ShutdownReason?.ReplyCode != 406)
                {
                    throw;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "Unable to bind exchange [{ExchangeName}] to queue [{QueueName}] using routing key [{RoutingKey}].",
                    exchange, queue, routingKey);

                throw;
            }
        }
    }
}
