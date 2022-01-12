using MediatR;

namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingRequestConsumer : IRequestHandler<PingRequest>
{
    public Task<Unit> Handle(PingRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine("Ping Request: " + request.Timestamp + " " + request.Message);
        return Unit.Task;
    }
}

public class PingPongRequestConsumer : IRequestHandler<PingPongRequest, PongResponse>
{
    public Task<PongResponse> Handle(PingPongRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine("Pong Request: " + request.Timestamp + " " + request.Message);
        return Task.FromResult(new PongResponse {Message = request.Message, Timestamp = DateTime.UtcNow});
    }
}
