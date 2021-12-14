// ReSharper disable once CheckNamespace
namespace StreamFlow.RabbitMq;

public interface IRabbitMqMetrics
{
    IDurationMetric? Publishing(string exchangeName);
    void PublishingEvent(string exchangeName, string eventName, TimeSpan duration);
    void PublishingError(string exchangeName);

    IDurationMetric? Consuming(string exchangeName, string queue);
    void MessageConsumerError(string exchangeName, string queue);

    void BusPublishing();
    void BusPublishingError();

    void ReportPublisherPoolSize(int poolSize);
    void ReportPublisherPoolInUse(int publishersInUse);
}
