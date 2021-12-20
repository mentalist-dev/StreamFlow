using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using StreamFlow.Configuration;
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
        public RabbitMqConsumerInfo(string exchange, string queue, string routingKey, ushort? prefetchCount)
        {
            Exchange = exchange;
            Queue = queue;
            RoutingKey = routingKey;
            PrefetchCount = prefetchCount;
        }

        public string Exchange { get; }
        public string Queue { get; }
        public string RoutingKey { get; }
        public ushort? PrefetchCount { get; }
    }

    public class RabbitMqConsumer: IRabbitMqConsumer, IDisposable
    {
        private readonly ConcurrentDictionary<ulong, BasicDeliverEventArgs> _received = new();
        private readonly ManualResetEventSlim _consumerIsIdle = new(true);

        private readonly IServiceProvider _services;
        private readonly RabbitMqConsumerInfo _info;
        private readonly ILogger<RabbitMqConsumer> _logger;

        private readonly AsyncEventingBasicConsumer? _consumer;
        private readonly IModel _channel;

        public RabbitMqConsumer(IServiceProvider services, IConnection connection, RabbitMqConsumerInfo consumerInfo, ILogger<RabbitMqConsumer> logger)
        {
            Id = Guid.NewGuid().ToString();

            _services = services;
            _logger = logger;
            _info = consumerInfo;

            var current = CreateConsumer(connection, consumerInfo);
            _channel = current.Channel;
            _consumer = current.Consumer;
        }

        private (IModel Channel, AsyncEventingBasicConsumer Consumer) CreateConsumer(IConnection connection, RabbitMqConsumerInfo consumerInfo)
        {
            var channel = connection.CreateModel();
            channel.ModelShutdown += (_, args) =>
            {
                _received.Clear();

                _logger.LogWarning("Channel shutdown: {@Details}", args);
                
                if (args.ReplyCode == Constants.PreconditionFailed)
                {
                    ChannelCrashed?.Invoke(this, EventArgs.Empty);
                }
            };

            channel.CallbackException += (_, args) =>
            {
                _logger.LogWarning("Callback exception: {@Details}", args);
            };

            if (consumerInfo.PrefetchCount > 0)
                channel.BasicQos(0, consumerInfo.PrefetchCount.Value, false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            return (channel, consumer);
        }

        public string Id { get; }
        public string? ConsumerTag { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsCanceled { get; private set; }
        public event EventHandler? ChannelCrashed;

        public void Start(IConsumerRegistration consumerRegistration, CancellationToken cancellationToken)
        {
            if (_consumer == null)
                throw new Exception("Consumer is null!");

            _consumer.Received += async (_, @event) =>
            {
                try
                {
                    _received.TryAdd(@event.DeliveryTag, @event);
                    _consumerIsIdle.Reset();

                    await OnReceivedAsync(@event, consumerRegistration, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Unhandled exception occurred. Consumer is now stopping..");

                    Nack(@event.DeliveryTag);

                    try { Cancel(); } catch { /**/ }
                }
                finally
                {
                    _consumerIsIdle.Set();
                    _received.TryRemove(@event.DeliveryTag, out var _);
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

                if (_channel is {IsOpen: true} && !string.IsNullOrWhiteSpace(consumerTag))
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

        private async Task OnReceivedAsync(BasicDeliverEventArgs @event, IConsumerRegistration consumer, CancellationToken cancellationToken)
        {
            if (IsDisposed || IsCanceled)
            {
                _logger.LogTrace("IsDisposed = {Disposed} or IsCanceled = {Canceled}", IsDisposed, IsCanceled);
                return;
            }

            using var scope = _services.CreateScope();
            var serviceProvider = scope.ServiceProvider;

            var metrics = serviceProvider.GetRequiredService<IRabbitMqMetrics>();
            using var progress = metrics.Consuming(_info.Exchange, _info.Queue);

            var correlationId = @event.BasicProperties?.CorrelationId ?? Guid.NewGuid().ToString();

            using var loggerScope = CreateLoggerScope(serviceProvider, @event, consumer.Options, correlationId);

            _logger.LogDebug("Received message. Message info = {@MessageInfo}. CorrelationId = {CorrelationId}.", _info, correlationId);

            bool? acknowledge = false;

            try
            {
                var context = new RabbitMqConsumerMessageContext(@event);

                // find handler and consume message
                var executor = serviceProvider.GetRequiredService<IStreamFlowConsumerPipe>();
                await executor
                    .ExecuteAsync(serviceProvider, context, ctx => consumer.ExecuteAsync(serviceProvider, ctx, cancellationToken))
                    .ConfigureAwait(false);

                acknowledge = true;

                progress?.Complete();
            }
            catch (OperationCanceledException e)
            {
                _logger.LogWarning(e, "Message handling cancelled. Message info = {@MessageInfo}.", _info);
            }
            catch (AlreadyClosedException e)
            {
                metrics.MessageConsumerError(_info.Exchange, _info.Queue);
                _logger.LogWarning(e, "Message handling failed because channel is already closed. Message info = {@MessageInfo}.", _info);
            }
            catch (Exception e)
            {
                metrics.MessageConsumerError(_info.Exchange, _info.Queue);
                _logger.LogError(e, "Message handling failed. Message info = {@MessageInfo}.", _info);
                acknowledge = await HandleError(serviceProvider, @event, consumer, e).ConfigureAwait(false);
            }
            finally
            {
                if (acknowledge != null)
                {
                    if (acknowledge == true)
                    {
                        Ack(@event.DeliveryTag);
                    }
                    else
                    {
                        Nack(@event.DeliveryTag);
                    }
                }
            }
        }

        private IDisposable? CreateLoggerScope(IServiceProvider services, BasicDeliverEventArgs @event, ConsumerOptions consumerOptions, string correlationId)
        {
            if (consumerOptions.IncludeHeadersToLoggerScope)
            {
                var scopeStateFactory = services.GetRequiredService<ILoggerScopeStateFactory>();
                var state = scopeStateFactory.Create(@event, consumerOptions, _info, correlationId);
                if (state is { Count: > 0 })
                {
                    return _logger.BeginScope(state);
                }
            }

            return null;
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
                    Nack(@event.DeliveryTag);
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
                    .HandleAsync(exception, consumerRegistration, @event, _info.Queue)
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

        private void Ack(ulong deliveryTag)
        {
            if (_received.TryRemove(deliveryTag, out _) && !_channel.IsClosed)
            {
                try
                {
                    _channel.BasicAck(deliveryTag, false);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to send message acknowledgment");
                }
            }
        }

        private void Nack(ulong deliveryTag)
        {
            if (_received.TryRemove(deliveryTag, out _) && !_channel.IsClosed)
            {
                try
                {
                    _channel.BasicNack(deliveryTag, false, true);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to send message not acknowledgment");
                }
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            _logger.LogDebug("Disposing consumer. Message info = {@MessageInfo}.", _info);

            try
            {
                Cancel();

                _consumerIsIdle.Wait(TimeSpan.FromSeconds(60));

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

            GC.SuppressFinalize(this);
        }
    }
}
