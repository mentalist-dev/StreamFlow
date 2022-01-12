namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingMessage: IDomainEvent
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
}
