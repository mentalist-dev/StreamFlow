using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StreamFlow.RabbitMq.Connection;

namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqChannelPool
    {
        RabbitMqChannel Get();
        void Return(RabbitMqChannel channel);
    }

    public class RabbitMqChannelPoolOptions
    {
        /// <summary>
        /// Defines maximal pool size for publisher channels. Default is 0 - unlimited.
        /// </summary>
        public uint MaxPoolSize { get; set; }

        /// <summary>
        /// Defines channel retention period. Once retention period hits - one channel is disposed.
        /// When set to 0 or less - retention job does not start.
        /// Default is one minute.
        /// </summary>
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromMinutes(1);
    }

    public class RabbitMqChannelPool: IRabbitMqChannelPool, IDisposable
    {
        private readonly object _syncRoot = new();

        private readonly IRabbitMqConnection _connection;
        private readonly RabbitMqChannelPoolOptions _options;
        private readonly IRabbitMqMetrics _metrics;
        private readonly ILogger<RabbitMqChannelPool> _logger;
        private readonly ConcurrentBag<RabbitMqChannel> _channels = new();


        private IConnection? _physicalConnection;
        private bool _isDisposed;

        public RabbitMqChannelPool(IRabbitMqConnection connection, RabbitMqChannelPoolOptions options, IRabbitMqMetrics metrics, ILogger<RabbitMqChannelPool> logger)
        {
            _connection = connection;
            _options = options;
            _metrics = metrics;
            _logger = logger;

            Task.Factory.StartNew(ChannelMonitor, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning);
        }

        private async Task ChannelMonitor()
        {
            var retentionPeriod = _options.RetentionPeriod;
            if (retentionPeriod <= TimeSpan.Zero)
                return;

            while (!_isDisposed)
            {
                if (_channels.TryTake(out var channel))
                {
                    var poolSize = _channels.Count;
                    _metrics.PublisherChannelPoolSize(poolSize);

                    DisposeChannel(channel);
                }

                await Task.Delay(retentionPeriod).ConfigureAwait(false);
            }
        }

        public RabbitMqChannel Get()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RabbitMqChannelPool));

            if (_channels.TryTake(out var channel))
            {
                if (!channel.Channel.IsOpen)
                {
                    DisposeChannel(channel);
                    channel = null;
                }
            }

            if (channel == null)
            {
                var connection = GetConnection();
                channel = new RabbitMqChannel(connection);
            }

            _metrics.PublisherChannelPoolSize(_channels.Count);

            return channel;
        }

        private IConnection GetConnection()
        {
            if (_physicalConnection == null || !_physicalConnection.IsOpen)
            {
                lock (_syncRoot)
                {
                    if (_physicalConnection == null || !_physicalConnection.IsOpen)
                    {
                        if (_physicalConnection is { IsOpen: false })
                        {
                            try
                            {
                                _physicalConnection.Dispose();
                            }
                            catch
                            {
                                // 
                            }
                        }

                        _physicalConnection = _connection.Create();
                    }
                }
            }

            return _physicalConnection;
        }

        public void Return(RabbitMqChannel channel)
        {
            if (_options.MaxPoolSize > 0 && _channels.Count >= _options.MaxPoolSize)
            {
                _logger.LogWarning("Maximum size {MaxPoolSize} of channel pool is reached. Returned channel will be disposed immediately.", _options.MaxPoolSize);
                DisposeChannel(channel);
            }
            else if (_isDisposed)
            {
                DisposeChannel(channel);
            }
            else
            {
                _channels.Add(channel);

                var poolSize = _channels.Count;
                _metrics.PublisherChannelPoolSize(poolSize);
            }
        }

        public void Dispose()
        {
            _isDisposed = true;

            while (_channels.TryTake(out var channel))
            {
                DisposeChannel(channel);
            }

            _physicalConnection?.Dispose();
        }

        private static void DisposeChannel(RabbitMqChannel channel)
        {
            try
            {
                channel.Dispose();
            }
            catch
            {
                //
            }
        }
    }

}
