using System.Diagnostics;
using Prometheus;

namespace StreamFlow.RabbitMq.Prometheus;

public class PrometheusRabbitMqMetrics: IRabbitMqMetrics
{
    private readonly Histogram _publicationCreatedHistogram = Metrics.CreateHistogram(
        "streamflow_publication_created",
        "Measures publication create phase (before passing to internal queue)",
        new HistogramConfiguration
        {
            LabelNames = new[] {"exchange", "state"},
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
        });

    private readonly Histogram _publicationConsumedHistogram = Metrics.CreateHistogram(
        "streamflow_publication_consumed",
        "Measures publication consume phase (internal thread which handles actual publication)",
        new HistogramConfiguration
        {
            LabelNames = new[] {"exchange", "state"},
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
        });

    private readonly Histogram _publishedHistogram = Metrics.CreateHistogram(
        "streamflow_publication_total_duration",
        "Measures publication total duration from PublishAsync to reaching server",
        new HistogramConfiguration
        {
            LabelNames = new[] {"exchange", "state"},
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
        });

    private readonly Histogram _consumerHistogram = Metrics.CreateHistogram(
        "streamflow_messages_consumed",
        "Messages consumed",
        new HistogramConfiguration
        {
            LabelNames = new[] {"exchange", "queue", "state"},
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
        });

    private readonly Counter _consumerErrorCounter = Metrics.CreateCounter(
        "streamflow_consumer_errors",
        "RabbitMQ consumer errors",
        new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

    private readonly Counter _consumerCancellationCounter = Metrics.CreateCounter(
        "streamflow_consumer_cancellations",
        "RabbitMQ consumer cancellations",
        new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

    private readonly Counter _consumerErrorQueuePublishedCounter = Metrics.CreateCounter(
        "streamflow_consumer_error_queue_publication",
        "Counts messages published to error queue when consumer fails",
        new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

    private readonly Counter _consumerErrorQueueFailedCounter = Metrics.CreateCounter(
        "streamflow_consumer_error_queue_failures",
        "Counts errors when publishing to error queue",
        new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

    public IDurationMetric PublicationCreated(string exchangeName)
    {
        return new DurationMetric(_publicationCreatedHistogram, exchangeName);
    }

    public IDurationMetric PublicationConsumed(string exchangeName)
    {
        return new DurationMetric(_publicationConsumedHistogram, exchangeName);
    }

    public IDurationMetric Published(string exchangeName)
    {
        return new DurationMetric(_publishedHistogram, exchangeName);
    }

    public IDurationMetric Consumed(string exchangeName, string queueName)
    {
        return new DurationMetric(_consumerHistogram, exchangeName, queueName);
    }

    public void ConsumerError(string exchangeName, string queueName)
    {
        _consumerErrorCounter
            .Labels(exchangeName, queueName)
            .Inc();
    }

    public void ConsumerCancelled(string exchangeName, string queueName)
    {
        _consumerCancellationCounter
            .Labels(exchangeName, queueName)
            .Inc();
    }

    public void ErrorQueuePublished(string originalExchangeName, string originalQueueName)
    {
        _consumerErrorQueuePublishedCounter
            .Labels(originalExchangeName, originalQueueName)
            .Inc();
    }

    public void ErrorQueueFailed(string originalExchangeName, string originalQueueName)
    {
        _consumerErrorQueueFailedCounter
            .Labels(originalExchangeName, originalQueueName)
            .Inc();
    }
}

internal sealed class DurationMetric : IDurationMetric
{
    private readonly Histogram _histogram;
    private readonly string[]? _labels;
    private readonly Stopwatch _timer;
    private string _stateName = "failed";

    public DurationMetric(Histogram histogram, params string[]? labels)
    {
        _histogram = histogram;
        _labels = labels;
        _timer = Stopwatch.StartNew();
    }

    ~DurationMetric()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();

            var labelValues = new List<string>();
            if (_labels != null)
            {
                labelValues.AddRange(_labels);
            }

            labelValues.Add(_stateName);

            _histogram
                .Labels(labelValues.ToArray())
                .Observe(_timer.Elapsed.TotalSeconds);
        }
    }

    public void Complete(string? stateName = null)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            stateName = "completed";
        }

        _stateName = stateName;
    }
}
