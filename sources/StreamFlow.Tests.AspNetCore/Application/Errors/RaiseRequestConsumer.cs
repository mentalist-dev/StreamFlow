using StreamFlow.Tests.Contracts;

namespace StreamFlow.Tests.AspNetCore.Application.Errors
{
    public class RaiseRequestConsumer: IConsumer<RaiseErrorRequest>
    {
        public Task Handle(IMessage<RaiseErrorRequest> message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
