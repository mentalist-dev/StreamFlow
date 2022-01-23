using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;
using StreamFlow.RabbitMq.Connection;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server;

public interface IRabbitMqErrorHandler
{
    Task HandleAsync(Exception exception, IConsumerRegistration consumerRegistration, BasicDeliverEventArgs @event, string queue);
}

internal class RabbitMqErrorHandler: IRabbitMqErrorHandler
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, DateTime> ErrorQueues = new();

    private readonly IRabbitMqErrorHandlerConnection _connection;
    private readonly IRabbitMqConventions _conventions;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqErrorHandler> _logger;

    public RabbitMqErrorHandler(IRabbitMqErrorHandlerConnection connection, IRabbitMqConventions conventions, IRabbitMqMetrics metrics, ILogger<RabbitMqErrorHandler> logger)
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

            EnsureErrorQueueExists(consumerRegistration, errorQueueName);

            using var rabbitmq = _connection.CreateChannel();
            rabbitmq.Send(@event.Body, properties, errorQueueName, null, _metrics, "err:");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to send message to error queue [{ErrorQueue}].", errorQueueName);
            throw;
        }

        return Task.CompletedTask;
    }

    private void EnsureErrorQueueExists(IConsumerRegistration consumerRegistration, string errorQueueName)
    {
        if (!ErrorQueues.ContainsKey(errorQueueName))
        {
            lock (Lock)
            {
                if (!ErrorQueues.ContainsKey(errorQueueName))
                {
                    var arguments = new Dictionary<string, object>
                    {
                        {"streamflow-error-queue", true}
                    };

                    var defaults = consumerRegistration.Default;
                    var queueOptions = consumerRegistration.Options.Queue;

                    using var rabbitmq = _connection.CreateChannel();
                    rabbitmq.Channel.TryCreateQueue(_logger, defaults, queueOptions, errorQueueName, false, arguments);

                    ErrorQueues.TryAdd(errorQueueName, DateTime.UtcNow);
                }
            }
        }
    }
}
