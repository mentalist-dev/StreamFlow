using System;
using Prometheus;

namespace StreamFlow.RabbitMq.Prometheus
{
    public class PrometheusRabbitMqMetrics: IRabbitMqMetrics
    {
        private readonly Histogram _messagesPublishedHistogram = Metrics.CreateHistogram(
            "streamflow_messages_published",
            "Messages published",
            new HistogramConfiguration
            {
                LabelNames = new[] { "exchange" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
            });

        private readonly Histogram _messageConsumerHistogram = Metrics.CreateHistogram(
            "streamflow_messages_consumed",
            "Messages consumed",
            new HistogramConfiguration
            {
                LabelNames = new[] {"exchange", "queue"},
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
            });

        private readonly Counter _messageConsumerErrorCounter = Metrics.CreateCounter(
            "streamflow_messages_consumed_errors",
            "Message consumption errors",
            new CounterConfiguration { LabelNames = new[] { "exchange", "queue" } });

        private readonly Gauge _publisherChannelPoolSize = Metrics.CreateGauge(
            "streamflow_publisher_channel_pool_size",
            "Publisher channel pool size");

        public IDisposable MessageInPublishing(string exchangeName)
        {
            return _messagesPublishedHistogram
                .Labels(exchangeName)
                .NewTimer();
        }

        public IDisposable ConsumerInProgress(string exchangeName, string queue)
        {
            return _messageConsumerHistogram
                .Labels(exchangeName, queue)
                .NewTimer();
        }

        public void MessageConsumerError(string exchangeName, string queue)
        {
            _messageConsumerErrorCounter
                .Labels(exchangeName, queue)
                .Inc();
        }

        public void PublisherChannelPoolSize(int poolSize)
        {
            _publisherChannelPoolSize.Set(poolSize);
        }
    }
}
