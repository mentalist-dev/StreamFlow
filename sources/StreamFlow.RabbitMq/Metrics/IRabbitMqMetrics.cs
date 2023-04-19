// ReSharper disable once CheckNamespace
namespace StreamFlow.RabbitMq;

public interface IRabbitMqMetrics
{
    IDurationMetric? PublicationCreated(string exchangeName);
    IDurationMetric? PublicationConsumed(string exchangeName);
    IDurationMetric? Published(string exchangeName);

    void PublisherError(string exchangeName, PublicationExceptionReason reason);

    IDurationMetric? Consumed(string exchangeName, string queueName);
    void ConsumerError(string exchangeName, string queueName);
    void ConsumerCancelled(string exchangeName, string queueName);

    void ErrorQueuePublished(string originalExchangeName, string originalQueueName);
    void ErrorQueueFailed(string originalExchangeName, string originalQueueName);

    IDisposable? PublisherQueued(string exchangeName);
    void ChannelShutdown(ushort replyCode);
    void ChannelCrashed(ushort replyCode);
}
