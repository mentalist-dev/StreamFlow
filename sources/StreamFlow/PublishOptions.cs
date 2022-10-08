namespace StreamFlow;

public class PublishOptions
{
    public Dictionary<string, object> Headers { get; } = new();

    public string? CorrelationId { get; set; }
    public string? RoutingKey { get; set; }
    public bool IsMandatory { get; set; }

    public TimeSpan? Timeout { get; set; }

    public string? Exchange { get; set; }
    public bool? CreateExchangeEnabled { get; set; }
    public bool? IgnoreNoRouteEvents { get; set; }
}
