using MediatR;

namespace StreamFlow.Tests.Contracts;

public class PingNotification : INotification
{
    public DateTime Timestamp { get; set; }
    public string? Message { get; set; }
}
