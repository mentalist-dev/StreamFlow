namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingRequestDelayedConsumer : IConsumer<PingRequest>
{
    public Task Handle(IMessage<PingRequest> message, CancellationToken cancellationToken)
    {
        return Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
    }
}
