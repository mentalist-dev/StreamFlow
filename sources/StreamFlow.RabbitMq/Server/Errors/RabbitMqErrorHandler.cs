using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using StreamFlow.RabbitMq.Publisher;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server.Errors;

public interface IRabbitMqErrorHandler
{
    Task HandleAsync(Exception exception, IConsumerRegistration consumerRegistration, BasicDeliverEventArgs @event, string queue);
}

internal class RabbitMqErrorHandler : IRabbitMqErrorHandler
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, DateTime> ErrorQueues = new();

    private readonly IRabbitMqPublisherConnection _connection;
    private readonly IRabbitMqConventions _conventions;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqErrorHandler> _logger;

    public RabbitMqErrorHandler(IRabbitMqPublisherConnection connection, IRabbitMqConventions conventions, IRabbitMqMetrics metrics, ILogger<RabbitMqErrorHandler> logger)
    {
        _connection = connection;
        _conventions = conventions;
        _metrics = metrics;
        _logger = logger;
    }

    public Task HandleAsync(Exception exception, IConsumerRegistration consumerRegistration, BasicDeliverEventArgs @event, string queue)
    {
        var errorQueueName = _conventions.GetErrorQueueName(
            consumerRegistration.RequestType,
            consumerRegistration.ConsumerType,
            consumerRegistration.Options.ConsumerGroup
        );

        _logger.LogDebug("Moving message to error queue [{ErrorQueueName}].", errorQueueName);

        try
        {
            var properties = @event.BasicProperties;

            properties.Headers ??= new Dictionary<string, object>();
            properties.Headers["SF:OriginalExchange"] = @event.Exchange;
            properties.Headers["SF:OriginalQueue"] = queue;
            properties.Headers["SF:OriginalRoutingKey"] = @event.RoutingKey;
            properties.Headers["SF:ExceptionTime"] = DateTime.UtcNow.ToString("O");
            properties.Headers["SF:ExceptionMessage"] = exception.Message;

            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                var stackTrace = new StringBuilder(exception.StackTrace);
                var innerException = exception.InnerException;
                while (innerException != null)
                {
                    stackTrace.AppendLine();
                    stackTrace.AppendLine("====================");
                    stackTrace.AppendLine(innerException.Message);
                    stackTrace.AppendLine();
                    stackTrace.AppendLine(innerException.StackTrace);

                    innerException = innerException.InnerException;
                }

                properties.Headers["SF:ExceptionDetails"] = stackTrace.ToString();
            }

            var force = false;
            for (var i = 0; i < 2; i++)
            {
                try
                {
                    EnsureErrorQueueExists(consumerRegistration, errorQueueName, force);

                    using var rabbitmq = new RabbitMqErrorChannel(_connection.Get(), _logger);
                    rabbitmq.Send(@event.Body, properties, errorQueueName);
                    break;
                }
                catch (OperationInterruptedException e)
                {
                    if (force || e.ShutdownReason.ReplyCode != Constants.NotFound)
                    {
                        throw;
                    }

                    // queue was not found, try recreate queue
                    force = true;
                }
            }

            _metrics.ErrorQueuePublished(@event.Exchange, queue);
        }
        catch (Exception e)
        {
            _metrics.ErrorQueueFailed(@event.Exchange, queue);
            _logger.LogError(e, "Unable to send message to error queue [{ErrorQueue}].", errorQueueName);
            throw;
        }

        return Task.CompletedTask;
    }

    private void EnsureErrorQueueExists(IConsumerRegistration consumerRegistration, string errorQueueName, bool force)
    {
        if (!ErrorQueues.ContainsKey(errorQueueName) || force)
        {
            lock (Lock)
            {
                if (!ErrorQueues.ContainsKey(errorQueueName) || force)
                {
                    var arguments = new Dictionary<string, object>
                    {
                        {"streamflow-error-queue", true}
                    };

                    var defaults = consumerRegistration.Default;
                    var queueOptions = consumerRegistration.Options.Queue;

                    using var rabbitmq = new RabbitMqErrorChannel(_connection.Get(), _logger);
                    rabbitmq.Channel.TryCreateQueue(_logger, defaults, queueOptions, errorQueueName, false, arguments);

                    ErrorQueues[errorQueueName] = DateTime.UtcNow;
                }
            }
        }
    }
}
