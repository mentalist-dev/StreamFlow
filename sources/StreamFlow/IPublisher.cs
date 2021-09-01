using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StreamFlow
{
    public interface IPublisher
    {
        Task PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T : class;
    }

    public class PublishOptions
    {
        public Dictionary<string, object> Headers { get; } = new();

        public string? CorrelationId { get; set; }
        public string? RoutingKey { get; set; }
        public bool IsMandatory { get; set; }

        public bool PublisherConfirmsEnabled { get; set; }
        public TimeSpan? PublisherConfirmsTimeout { get; set; }
    }
}
