using Prometheus;

namespace StreamFlow.RabbitMq.Prometheus
{
    public class PrometheusRabbitMqMetrics: IRabbitMqMetrics
    {
        private readonly Histogram _publishingHistogram = Metrics.CreateHistogram(
            "streamflow_messages_published",
            "Messages published",
            new HistogramConfiguration
            {
                LabelNames = new[] { "exchange" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
            });

        private readonly Counter _publishingErrorCounter = Metrics.CreateCounter(
            "streamflow_messages_publishing_errors",
            "Message publishing errors",
            new CounterConfiguration {LabelNames = new[] {"exchange"}});

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
            new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

        private readonly Counter _busPublishingCounter = Metrics.CreateCounter(
            "streamflow_messages_bus_publishing",
            "Message bus publishing errors");

        private readonly Counter _busPublishingErrorCounter = Metrics.CreateCounter(
            "streamflow_messages_bus_publishing_errors",
            "Message bus publishing errors");

        public IDisposable Publishing(string exchangeName)
        {
            return _publishingHistogram
                .Labels(exchangeName)
                .NewTimer();
        }

        public void PublishingError(string exchangeName)
        {
            _publishingErrorCounter
                .Labels(exchangeName)
                .Inc();
        }

        public IDisposable Consuming(string exchangeName, string queue)
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

        public void BusPublishing()
        {
            _busPublishingCounter.Inc();
        }

        public void BusPublishingError()
        {
            _busPublishingErrorCounter.Inc();
        }
    }
}
