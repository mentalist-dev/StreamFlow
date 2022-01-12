using MediatR;

namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingRequest: IRequest
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
}

public class PongResponse: INotification
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
}

public class PingPongRequest : IRequest<PongResponse>
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
}
