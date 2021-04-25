using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamFlow
{
    public interface IPublisher
    {
        Task PublishAsync<T>(T message, PublishOptions? options = null) where T : class;
    }

    public class PublishOptions
    {
        public Dictionary<string, object> Headers { get; } = new();

        public string? CorrelationId { get; set; }
        public string? RoutingKey { get; set; }
        public bool IsMandatory { get; set; }
    }
}
