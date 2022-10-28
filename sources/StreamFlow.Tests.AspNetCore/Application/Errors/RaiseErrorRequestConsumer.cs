using StreamFlow.Tests.Contracts;

namespace StreamFlow.Tests.AspNetCore.Application.Errors
{
    public class RaiseErrorRequestConsumer: IConsumer<RaiseErrorRequest>
    {
        public Task Handle(IMessage<RaiseErrorRequest> message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException($"Final Retry: {message.Context.FinalRetry}; Retry Count: {message.Context.RetryCount ?? 0}");
        }
    }
}
