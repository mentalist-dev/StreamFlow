using MediatR;

namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingNotification : INotification
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
}
