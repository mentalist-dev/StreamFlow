using System.Collections.Generic;

namespace StreamFlow
{
    public class PublishOptions
    {
        public Dictionary<string, object> Headers { get; } = new();

        public string? CorrelationId { get; set; }
        public string? RoutingKey { get; set; }
        public bool IsMandatory { get; set; }
    }
}