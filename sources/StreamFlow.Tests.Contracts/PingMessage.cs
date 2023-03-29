namespace StreamFlow.Tests.Contracts;

public class PingMessage: IDomainEvent
{
    public DateTime Timestamp { get; set; }
    public string? Message { get; set; }
}
