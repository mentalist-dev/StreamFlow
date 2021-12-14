// ReSharper disable once CheckNamespace
namespace StreamFlow.RabbitMq;

internal class RabbitMqNoOpMetrics : IRabbitMqMetrics
{
    public IDurationMetric? Publishing(string exchangeName)
    {
        return null;
    }

    public void PublishingEvent(string exchangeName, string eventName, TimeSpan duration)
    {
    }

    public void PublishingError(string exchangeName)
    {
    }

    public IDurationMetric? Consuming(string exchangeName, string queue)
    {
        return null;
    }

    public void MessageConsumerError(string exchangeName, string queue)
    {
    }

    public void BusPublishing()
    {
    }

    public void BusPublishingError()
    {
    }

    public void ReportPublisherPoolSize(int poolSize)
    {
    }

    public void ReportPublisherPoolInUse(int publishersInUse)
    {
    }
}
