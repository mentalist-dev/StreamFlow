using System.Diagnostics;
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
                LabelNames = new[] {"exchange", "state"},
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
            });

        private readonly Histogram _publishingEventsHistogram = Metrics.CreateHistogram(
            "streamflow_messages_publishing_events",
            "Events happened during publishing",
            new HistogramConfiguration
            {
                LabelNames = new[] {"exchange", "event"},
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
                LabelNames = new[] {"exchange", "queue", "state"},
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

        public IDurationMetric Publishing(string exchangeName)
        {
            return new DurationMetric(_publishingHistogram, exchangeName);
        }

        public void PublishingEvent(string exchangeName, string eventName, TimeSpan duration)
        {
            _publishingEventsHistogram
                .Labels(exchangeName, eventName)
                .Observe(duration.TotalSeconds);
        }

        public void PublishingError(string exchangeName)
        {
            _publishingErrorCounter
                .Labels(exchangeName)
                .Inc();
        }

        public IDurationMetric Consuming(string exchangeName, string queue)
        {
            return new DurationMetric(_messageConsumerHistogram, exchangeName, queue);
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

    internal class DurationMetric : IDurationMetric
    {
        private readonly Histogram _histogram;
        private readonly string[]? _labels;
        private readonly Stopwatch _timer;
        private bool _completed;

        public DurationMetric(Histogram histogram, params string[]? labels)
        {
            _histogram = histogram;
            _labels = labels;
            _timer = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _timer.Stop();

            var labelValues = new List<string>();
            if (_labels != null)
            {
                labelValues.AddRange(_labels);
            }
            labelValues.Add(_completed ? "completed" : "failed");

            _histogram
                .Labels(labelValues.ToArray())
                .Observe(_timer.Elapsed.TotalSeconds);
        }

        public void Complete()
        {
            _completed = true;
        }
    }
}
