namespace StreamFlow.Configuration;

public interface IConsumerOptionsBuilder
{
    IConsumerOptionsBuilder ConsumerGroup(string consumerGroupName);
    IConsumerOptionsBuilder ConsumerCount(int consumerCount);
    IConsumerOptionsBuilder ConfigureQueue(Action<IQueueOptionsBuilder> configure);
    IConsumerOptionsBuilder IncludeHeadersToLoggerScope(bool include = true, params string[] exceptHeaderNames);
    IConsumerOptionsBuilder Prefetch(ushort prefetchCount);
    IConsumerOptionsBuilder RetryOnError(int? maxAllowedRetries = 1);
}
