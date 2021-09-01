using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using StreamFlow.Pipes;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqConsumer
    {
        string Id { get; }
        string? ConsumerTag { get; }
    }

    public class RabbitMqConsumerInfo
    {
        public RabbitMqConsumerInfo(string exchange, string queue, string routingKey)
        {
            Exchange = exchange;
            Queue = queue;
            RoutingKey = routingKey;
        }

        public string Exchange { get; init; }
        public string Queue { get; init; }
        public string RoutingKey { get; init; }
    }

    public class RabbitMqConsumer: IRabbitMqConsumer, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly IModel _channel;
        private readonly RabbitMqConsumerInfo _info;
        private readonly ILogger<RabbitMqConsumer> _logger;
        private readonly AsyncEventingBasicConsumer _consumer;
        private readonly ManualResetEventSlim _consumerIsIdle = new(true);

        public RabbitMqConsumer(IServiceProvider services, IModel channel, RabbitMqConsumerInfo consumerInfo, ILogger<RabbitMqConsumer> logger)
        {
            Id = Guid.NewGuid().ToString();

            _services = services;
            _channel = channel;
            _logger = logger;
            _info = consumerInfo;
            _consumer = new AsyncEventingBasicConsumer(channel);
        }

        public string Id { get; }
        public string? ConsumerTag { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsCanceled { get; private set; }

        public void Start(IConsumerRegistration consumerRegistration, CancellationToken cancellationToken)
        {
            _consumer.Received += async (_, @event) =>
            {
                try
                {
                    _consumerIsIdle.Reset();

                    await OnReceivedAsync(@event, consumerRegistration, _channel, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _consumerIsIdle.Set();
                }
            };

            ConsumerTag = _channel.BasicConsume(_info.Queue, false, _consumer);
        }

        public string? Cancel()
        {
            var consumerTag = ConsumerTag;

            if (!IsCanceled)
            {
                IsCanceled = true;

                if (_channel.IsOpen && !string.IsNullOrWhiteSpace(consumerTag))
                {
                    _logger.LogWarning("Cancelling RabbitMQ subscription {ConsumerId} / {ConsumerTag}", Id, consumerTag);
                    try
                    {
                        _channel.BasicCancel(consumerTag);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Unable to cancel RabbitMQ subscription {ConsumerId} / {ConsumerTag}", Id, consumerTag);
                    }
                }
            }

            ConsumerTag = null;

            return consumerTag;
        }

        private async Task OnReceivedAsync(BasicDeliverEventArgs @event, IConsumerRegistration consumer, IModel channel, CancellationToken cancellationToken)
        {
            if (IsDisposed || IsCanceled)
            {
                _logger.LogTrace("IsDisposed = {Disposed} or IsCanceled = {Canceled}", IsDisposed, IsCanceled);
                return;
            }

            var correlationId = @event.BasicProperties?.CorrelationId ?? string.Empty;
            _logger.LogDebug("Received message. Message info = {@MessageInfo}. CorrelationId = {CorrelationId}.", _info, correlationId);

            using var scope = _services.CreateScope();
            var serviceProvider = scope.ServiceProvider;

            bool? acknowledge = false;

            Exception? lastException = null;
            ShutdownEventArgs? shutdownArgs = null;

            void ChannelOnModelShutdown(object? _, ShutdownEventArgs args)
            {
                shutdownArgs = args;
            }

            try
            {
                channel.ModelShutdown += ChannelOnModelShutdown;

                var context = new RabbitMqConsumerMessageContext(@event);

                // find handler and consume message
                var executor = serviceProvider.GetRequiredService<IStreamFlowConsumerPipe>();
                await executor
                    .ExecuteAsync(serviceProvider, context, ctx => consumer.ExecuteAsync(serviceProvider, ctx, cancellationToken))
                    .ConfigureAwait(false);

                acknowledge = true;
            }
            catch (OperationCanceledException e)
            {
                lastException = e;

                if (shutdownArgs == null)
                {
                    _logger.LogWarning(e, "Message handling failed. Message info = {@MessageInfo}. Operation cancelled.", _info);
                    acknowledge = false;
                }
            }
            catch (AlreadyClosedException e)
            {
                lastException = e;
                shutdownArgs = e.ShutdownReason;
            }
            catch (Exception e)
            {
                lastException = e;
                _logger.LogError(e, "Message handling failed. Message info = {@MessageInfo}.", _info);
                acknowledge = await HandleError(serviceProvider, @event, consumer, e).ConfigureAwait(false);
            }
            finally
            {
                channel.ModelShutdown -= ChannelOnModelShutdown;

                if (shutdownArgs != null)
                {
                    _logger.LogError(lastException,
                        "Message handling failed as channel has been closed. Message info = {@MessageInfo}. Shutdown reason = {@ShutdownReason}.",
                        _info, shutdownArgs
                    );
                }

                if (acknowledge != null && !channel.IsClosed)
                {
                    if (acknowledge == true)
                    {
                        _channel.BasicAck(@event.DeliveryTag, false);
                    }
                    else
                    {
                        ReturnMessageBackToQueue(@event.DeliveryTag);
                    }
                }
            }
        }

        private async Task<bool?> HandleError(IServiceProvider serviceProvider, BasicDeliverEventArgs @event, IConsumerRegistration consumerRegistration, Exception exception)
        {
            IRabbitMqErrorHandler? errorHandler;
            try
            {
                errorHandler = serviceProvider.GetRequiredService<IRabbitMqErrorHandler>();
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Unable to create error handler <IRabbitMqErrorHandler>. Cancelling subscription. Shutting down consumer.");

                try
                {
                    ReturnMessageBackToQueue(@event.DeliveryTag);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to send NACK to RabbitMQ server");
                }
                finally
                {
                    Cancel();
                }

                // response to RabbitMQ already handled
                return null;
            }

            try
            {
                await errorHandler
                    .HandleAsync(_channel, exception, consumerRegistration, @event, _info.Queue)
                    .ConfigureAwait(false);

                // error successfully handled, acknowledge current message
                return true;
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Message error handling failed. Message info = {@MessageInfo}.", _info);

                // error was not handled
                return false;
            }
        }

        private void ReturnMessageBackToQueue(ulong deliveryTag)
        {
            _channel.BasicNack(deliveryTag, false, true);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            _logger.LogDebug("Disposing consumer. Message info = {@MessageInfo}.", _info);

            try
            {
                Cancel();

                _consumerIsIdle.Wait(TimeSpan.FromSeconds(30));

                var channel = _channel;
                if (channel.IsOpen)
                {
                    _logger.LogDebug("Closing channel.");
                    channel.Close();
                }
                channel.Dispose();
            }
            finally
            {
                IsDisposed = true;
            }
        }
    }
}
