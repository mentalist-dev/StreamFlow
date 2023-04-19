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

    private readonly Counter _publisherErrorCounter = Metrics.CreateCounter(
        "streamflow_publication_errors_total",
        "RabbitMQ publisher errors",
        new CounterConfiguration { LabelNames = new[] { "exchange", "reason" } });

    private readonly Histogram _consumerHistogram = Metrics.CreateHistogram(
        "streamflow_messages_consumed",
        "Messages consumed",
        new HistogramConfiguration
        {
            LabelNames = new[] {"exchange", "queue", "state"},
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
        });

    private readonly Counter _consumerErrorCounter = Metrics.CreateCounter(
        "streamflow_consumer_errors_total",
        "RabbitMQ consumer errors",
        new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

    private readonly Counter _consumerCancellationCounter = Metrics.CreateCounter(
        "streamflow_consumer_cancellations_total",
        "RabbitMQ consumer cancellations",
        new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

    private readonly Counter _consumerErrorQueuePublishedCounter = Metrics.CreateCounter(
        "streamflow_consumer_error_queue_publication_total",
        "Counts messages published to error queue when consumer fails",
        new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

    private readonly Counter _consumerErrorQueueFailedCounter = Metrics.CreateCounter(
        "streamflow_consumer_error_queue_failures_total",
        "Counts errors when publishing to error queue",
        new CounterConfiguration {LabelNames = new[] {"exchange", "queue"}});

    private readonly Gauge _publisherQueuedMessages = Metrics.CreateGauge(
        "streamflow_publisher_queue",
        "Publisher queue size monitor",
        new GaugeConfiguration {LabelNames = new[] {"exchange"}});

    private readonly Counter _channelShutdownCounter = Metrics.CreateCounter(
        "streamflow_publication_channel_shutdown_total",
        "RabbitMQ channel shutdowns",
        new CounterConfiguration { LabelNames = new[] { "replyCode" } });

    private readonly Counter _channelCrashedCounter = Metrics.CreateCounter(
        "streamflow_publication_channel_crashed_total",
        "RabbitMQ channel crashes",
        new CounterConfiguration { LabelNames = new[] { "replyCode" } });

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

    public void PublisherError(string exchangeName, PublicationExceptionReason reason)
    {
        _publisherErrorCounter.Labels(exchangeName, reason.ToString()).Inc();
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

    public IDisposable? PublisherQueued(string exchange)
    {
        return _publisherQueuedMessages.Labels(exchange).TrackInProgress();
    }

    public void ChannelShutdown(ushort replyCode)
    {
        _channelShutdownCounter.Labels(replyCode.ToString()).Inc();
    }

    public void ChannelCrashed(ushort replyCode)
    {
        _channelCrashedCounter.Labels(replyCode.ToString()).Inc();
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
