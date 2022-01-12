namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingMessageConsumer : IConsumer<PingMessage>
{
    public Task Handle(IMessage<PingMessage> message, CancellationToken cancellationToken)
    {
        Console.WriteLine(message.Body.Timestamp + " " + message.Body.Message);
        // throw new Exception("Unable to handle!");
        return Task.CompletedTask;
    }
}
