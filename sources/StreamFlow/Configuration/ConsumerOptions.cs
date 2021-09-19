using System;
using System.Collections.Generic;

namespace StreamFlow.Configuration
{
    public class ConsumerOptions: IConsumerOptionsBuilder
    {
        public string? ConsumerGroup { get; private set; }
        public int ConsumerCount { get; private set; } = 1;
        public QueueOptions Queue { get; } = new();
        public bool IncludeHeadersToLoggerScope { get; private set; } = true;
        public HashSet<string> ExcludeHeaderNamesFromLoggerScope { get; } = new(StringComparer.OrdinalIgnoreCase);

        IConsumerOptionsBuilder IConsumerOptionsBuilder.ConsumerGroup(string consumerGroupName)
        {
            ConsumerGroup = consumerGroupName;
            return this;
        }

        IConsumerOptionsBuilder IConsumerOptionsBuilder.ConsumerCount(int consumerCount)
        {
            ConsumerCount = consumerCount;
            return this;
        }

        IConsumerOptionsBuilder IConsumerOptionsBuilder.ConfigureQueue(Action<IQueueOptionsBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            configure(Queue);

            return this;
        }

        IConsumerOptionsBuilder IConsumerOptionsBuilder.IncludeHeadersToLoggerScope(bool include, params string[] exceptHeaderNames)
        {
            IncludeHeadersToLoggerScope = include;

            ExcludeHeaderNamesFromLoggerScope.Clear();
            ExcludeHeaderNamesFromLoggerScope.UnionWith(exceptHeaderNames);

            return this;
        }
    }
}
