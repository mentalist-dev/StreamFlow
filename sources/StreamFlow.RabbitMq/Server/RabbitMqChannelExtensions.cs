using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using StreamFlow.Configuration;

namespace StreamFlow.RabbitMq.Server
{
    public static class RabbitMqChannelExtensions
    {
        public static void TryCreateQueue(this IModel channel
            , ILogger logger
            , StreamFlowDefaults? defaults
            , QueueOptions? queueOptions
            , string queue
            , bool? autoDelete = null
            , Dictionary<string, object>? additionalArguments = null)
        {
            var durable = queueOptions?.Durable ?? true;
            var exclusive = queueOptions?.Exclusive ?? false;

            autoDelete ??= queueOptions?.AutoDelete ?? false;

            IDictionary<string, object>? arguments = null;

            var quorum = queueOptions?.QuorumOptions ?? defaults?.QuorumOptions ?? new QueueQuorumOptions { Enabled = true };

            if (quorum.Enabled && !autoDelete.Value && !exclusive)
            {
                arguments ??= new Dictionary<string, object>();
                arguments.Add("x-queue-type", "quorum");

                if (quorum.Size > 0)
                {
                    if (quorum.Size % 2 == 0)
                    {
                        logger.LogWarning(
                            "Quorum size for {QueueName} is set to even number {QuorumSize}. " +
                            "It is highly recommended for the factor to be an odd number.",
                            queue, quorum.Size);
                    }

                    arguments.Add("x-quorum-initial-group-size", quorum.Size);
                }
            }

            if (queueOptions?.Arguments is {Count: > 0})
            {
                arguments ??= new Dictionary<string, object>();
                foreach (var argument in queueOptions.Arguments)
                {
                    arguments[argument.Key] = argument.Value;
                }
            }

            if (additionalArguments is {Count: > 0})
            {
                arguments ??= new Dictionary<string, object>();
                foreach (var argument in additionalArguments)
                {
                    arguments[argument.Key] = argument.Value;
                }
            }

            try
            {
                logger.LogInformation(
                    "Declaring queue: [{QueueName}]. Queue options = {@QueueOptions}.",
                    queue, queueOptions);

                channel.QueueDeclare(queue, durable, exclusive, autoDelete.Value, arguments);
            }
            catch (OperationInterruptedException e)
            {
                // 406: PRECONDITION_FAILED, queue exists, but with different properties
                if (e.ShutdownReason?.ReplyCode != Constants.PreconditionFailed)
                {
                    logger.LogError(e, "Unable to declare queue [{QueueName}]. Queue options = {@QueueOptions}.", queue, queueOptions);
                    throw;
                }

                logger.LogWarning(e, "Queue already exists with name [{QueueName}] but different options. Desired queue options = {@QueueOptions}.", queue, queueOptions);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unable to declare queue [{QueueName}]. Queue options = {@QueueOptions}.", queue, queueOptions);
                throw;
            }
        }
    }
}
