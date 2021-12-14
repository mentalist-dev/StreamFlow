using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqPublisherPoolController
    {
        void ReduceToDesiredSize();
        void Clear();
    }

    internal interface IRabbitMqPublisherPool: IRabbitMqPublisherPoolController
    {
        RabbitMqPublisher Get();
        void Return(RabbitMqPublisher publisher);
    }

    public class RabbitMqPublisherPoolOptions
    {
        public uint DesiredPoolSize { get; set; }
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromMinutes(1);
    }

    internal class RabbitMqPublisherPool: IRabbitMqPublisherPool, IDisposable
    {
        private readonly RabbitMqPublisherPoolOptions _options;
        private readonly IServiceProvider _services;
        private readonly IRabbitMqMetrics _metrics;
        private readonly ConcurrentQueue<RabbitMqPublisher> _publishers = new();
        private readonly ConcurrentDictionary<RabbitMqPublisher, DateTime> _publishersInUse = new();
        private bool _disposed;

        public RabbitMqPublisherPool(RabbitMqPublisherPoolOptions options, IServiceProvider services, IRabbitMqMetrics metrics)
        {
            _options = options;
            _services = services;
            _metrics = metrics;

            Task.Factory.StartNew(PoolMonitor, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            _disposed = true;

            while (_publishers.TryDequeue(out var publisher))
            {
                publisher.Dispose();
            }

            var inUse = _publishersInUse.Keys.ToArray();
            foreach (var publisher in inUse)
            {
                publisher.Dispose();
            }
            _publishersInUse.Clear();

            GC.SuppressFinalize(this);
        }
        
        public RabbitMqPublisher Get()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RabbitMqPublisherPool));

            if (_publishers.TryDequeue(out var publisher))
            {
                _metrics.ReportPublisherPoolSize(_publishers.Count);

                _publishersInUse[publisher] = DateTime.UtcNow;
                _metrics.ReportPublisherPoolInUse(_publishersInUse.Count);

                return publisher;
            }

            return _services.GetRequiredService<RabbitMqPublisher>();
        }

        public void Return(RabbitMqPublisher publisher)
        {
            if (_disposed)
            {
                publisher.Dispose();
                return;
            }

            // desired pool size is not checked here
            // on a high load it is not optimal to dispose publisher here as it might be needed immediately by another publishing call
            // lets leave retention job to pool monitor
            _publishers.Enqueue(publisher);
            _metrics.ReportPublisherPoolSize(_publishers.Count);

            _publishersInUse.TryRemove(publisher, out _);
            _metrics.ReportPublisherPoolInUse(_publishersInUse.Count);
        }

        public void ReduceToDesiredSize()
        {
            while (_publishers.Count > _options.DesiredPoolSize)
            {
                if (_publishers.TryDequeue(out var publisher))
                {
                    publisher.Dispose();
                }
            }

            _metrics.ReportPublisherPoolSize(_publishers.Count);
        }

        public void Clear()
        {
            while (_publishers.Count > 0)
            {
                if (_publishers.TryDequeue(out var publisher))
                {
                    publisher.Dispose();
                }
            }

            _metrics.ReportPublisherPoolSize(_publishers.Count);
        }

        private async Task PoolMonitor()
        {
            var retentionPeriod = _options.RetentionPeriod;
            if (retentionPeriod <= TimeSpan.Zero)
                retentionPeriod = TimeSpan.FromMinutes(1);

            while (!_disposed)
            {
                await Task.Delay(retentionPeriod).ConfigureAwait(false);

                // taking out only one publisher per retention period
                // such approach minimizes expensive publisher channel close/reopen cycle when publisher has high demand
                // on the other hand - it might keep open publisher channels for a longer time than wanted
                if (_publishers.Count + _publishersInUse.Count > _options.DesiredPoolSize)
                {
                    if (_publishers.TryDequeue(out var publisher))
                    {
                        publisher.Dispose();
                    }
                }

                _metrics.ReportPublisherPoolSize(_publishers.Count);
            }
        }
    }
}
