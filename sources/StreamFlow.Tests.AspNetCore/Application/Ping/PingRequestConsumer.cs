namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingRequestConsumer : IConsumer<PingRequest>
{
    public Task Handle(IMessage<PingRequest> message, CancellationToken cancellationToken)
    {
        Console.WriteLine(message.Body.Timestamp + " " + message.Body.Message);
        // throw new Exception("Unable to handle!");
        return Task.CompletedTask;
    }
}
