namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingRequestDelayedConsumer : IConsumer<PingMessage>
{
    public Task Handle(IMessage<PingMessage> message, CancellationToken cancellationToken)
    {
        return Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
    }
}
