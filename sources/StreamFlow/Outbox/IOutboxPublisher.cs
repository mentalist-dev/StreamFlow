using System;
using System.Threading;
using System.Threading.Tasks;

namespace StreamFlow.Outbox
{
    public interface IOutboxPublisher
    {
        Task PublishAsync<T>(T message, PublishOptions? options = null, SchedulingOptions? scheduling = null, bool triggerSaveChanges = true, CancellationToken cancellationToken = default) where T : class;
    }

    public class SchedulingOptions
    {
        public DateTime? ExactDateTime { get; set; }
        public TimeSpan? RelativeFromNow { get; set; }
    }
}
