namespace StreamFlow.Tests.AspNetCore.Application.Errors
{
    public class RaiseRequestConsumer: IConsumer<RaiseRequest>
    {
        public Task Handle(IMessage<RaiseRequest> message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
