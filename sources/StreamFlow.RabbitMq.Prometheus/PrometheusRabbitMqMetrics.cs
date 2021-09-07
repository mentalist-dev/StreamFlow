using System;
using Prometheus;

namespace StreamFlow.RabbitMq.Prometheus
{
    public class PrometheusRabbitMqMetrics: IRabbitMqMetrics
    {
        private readonly Counter _messagesPublishedCounter =
            Metrics.CreateCounter(
                "streamflow_messages_published", 
                "Messages published by StreamFlow",
                new CounterConfiguration
                {
                    LabelNames = new [] {"exchange"}
                });

        private readonly Histogram _messageConsumerHistogram = Metrics.CreateHistogram(
            "streamflow_message_consumer",
            "Messages consumed by StreamFlow",
            new HistogramConfiguration
            {
                LabelNames = new[] {"exchange", "queue"},
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
            });

        private readonly Counter _messageConsumerErrorCounter =
            Metrics.CreateCounter(
                "streamflow_message_consumer_error", 
                "Error happened during consumer execution by StreamFlow",
                new CounterConfiguration { LabelNames = new [] {"exchange", "queue"} });

        public void MessagePublished(string exchangeName)
        {
            _messagesPublishedCounter.Labels(exchangeName).Inc();
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

    }
}
