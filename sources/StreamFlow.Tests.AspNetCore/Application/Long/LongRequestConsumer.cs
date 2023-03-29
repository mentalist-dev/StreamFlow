using StreamFlow.Tests.Contracts;

namespace StreamFlow.Tests.AspNetCore.Application.Long;

public class LongRequestConsumer: IConsumer<LongRequest>
{
    public async Task Handle(IMessage<LongRequest> message, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(message.Body.Duration, cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine("Long request failed: " + e);
        }
    }
}
