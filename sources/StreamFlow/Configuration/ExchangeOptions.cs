namespace StreamFlow.Configuration;

public class ExchangeOptions
{
    public bool? Durable { get; set; }
    public bool? AutoDelete { get; set; }
    public IDictionary<string, object>? Arguments { get; } = new Dictionary<string, object>();
}
