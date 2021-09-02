using System;

namespace StreamFlow.Outbox
{
    public class StreamFlowOutboxPublisherOptions
    {
        public TimeSpan SleepDuration { get; set; } = TimeSpan.FromSeconds(15);
    }
}
