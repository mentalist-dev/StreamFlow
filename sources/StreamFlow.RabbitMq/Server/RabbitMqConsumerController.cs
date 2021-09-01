using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server
{
    internal class RabbitMqConsumerController: IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly IConsumerRegistration _registration;
        private readonly RabbitMqConsumerInfo _consumerInfo;
        private readonly IConnection _connection;
        private readonly ILogger<RabbitMqConsumer> _logger;
        private readonly CancellationToken _cancellationToken;

        private CancellationTokenSource? _cancellationTokenSource;
        private RabbitMqConsumer? _consumer;
        private bool _initialized;
        private bool _disposing;
        private bool _disposed;

        public RabbitMqConsumerController(IServiceProvider services
            , IConsumerRegistration registration
            , RabbitMqConsumerInfo consumerInfo
            , IConnection connection
            , ILogger<RabbitMqConsumer> logger
            , CancellationToken cancellationToken)
        {
            _services = services;
            _registration = registration;
            _consumerInfo = consumerInfo;
            _connection = connection;
            _logger = logger;
            _cancellationToken = cancellationToken;
        }

        public void Initialize()
        {
            if (_disposing || _disposed)
                throw new ObjectDisposedException("Controller already disposed.");

            lock (this)
            {
                if (!_initialized)
                {
                    CreateConsumerInternal(_consumerInfo);
                    _initialized = true;
                }
            }
        }

        public void Dispose()
        {
            _disposing = true;
            DestroyConsumer();
            _disposed = true;
        }

        private void CreateConsumerInternal(RabbitMqConsumerInfo consumerInfo)
        {
            _logger.LogInformation("Creating new consumer. Consumer info: {@ConsumerInfo}.", consumerInfo);

            var channel = _connection.CreateModel();

            channel.ModelShutdown += (_, _) => RecreateConsumer(_consumerInfo);

            _consumer = new RabbitMqConsumer(_services, channel, consumerInfo, _logger);

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

            _consumer.Start(_registration, _cancellationTokenSource.Token);
        }

        private void RecreateConsumer(RabbitMqConsumerInfo consumerInfo)
        {
            if (_disposing || _disposed)
                return;

            _logger.LogTrace("Channel was shut down. Recreating consumer. Consumer info: {@ConsumerInfo}.", consumerInfo);

            DestroyConsumer();

            Task.Factory.StartNew(() => CreateConsumer(consumerInfo), CancellationToken.None);
        }

        private void CreateConsumer(RabbitMqConsumerInfo consumerInfo)
        {
            try
            {
                if (!_disposing && !_disposed)
                {
                    lock (this)
                    {
                        CreateConsumerInternal(consumerInfo);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to create consumer. Consumer info: {@ConsumerInfo}.", consumerInfo);
            }
        }

        #region Destroy Consumer

        private void DestroyConsumer()
        {
            var consumer = _consumer;
            if (consumer != null)
            {
                lock (this)
                {
                    var canceledConsumerTag = CancelConsumerAtServer();
                    LogConsumerDestroy(consumer.Id, canceledConsumerTag);
                    CancelCurrentExecutions();
                    DisposeConsumer();
                }
            }
        }

        private string? CancelConsumerAtServer()
        {
            try
            {
                var consumer = _consumer;
                if (consumer != null)
                {
                    _logger.LogTrace(
                        "Cancelling consumer {ConsumerId} / {ConsumerTag} subscription at server.",
                        consumer.Id, consumer.ConsumerTag
                    );

                    return consumer.Cancel();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to cancel consumer.");
            }

            return null;
        }

        private void LogConsumerDestroy(string consumerId, string? consumerTag)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(consumerTag))
                {
                    _logger.LogWarning(
                        "Destroying consumer instance {ConsumerId} / {ConsumerTag} for Queue = {QueueName} using Routing Key = {RoutingKey}.",
                        consumerId, consumerTag, _consumerInfo.Queue, _consumerInfo.RoutingKey);
                }
            }
            catch (Exception e)
            {
                // if logger is failing - we cannot log it..
                Trace.WriteLine(e.ToString(), "RabbitMqConsumerController");
            }
        }

        private void CancelCurrentExecutions()
        {
            try
            {
                _logger.LogTrace("Marking consumer executions as cancelled");
                _cancellationTokenSource?.Cancel();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to cancel consumer");
            }

            try
            {
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to dispose cancellation token source");
            }
            finally
            {
                _cancellationTokenSource = null;
            }
        }

        private void DisposeConsumer()
        {
            try
            {
                var consumer = _consumer;
                if (consumer != null)
                {
                    _logger.LogTrace("Disposing consumer {ConsumerId}", consumer.Id);
                    consumer.Dispose();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Consumer dispose failed");
            }
            finally
            {
                _consumer = null;
            }
        }

        #endregion
    }

}
