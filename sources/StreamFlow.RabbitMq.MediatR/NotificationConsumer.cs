using MediatR;

namespace StreamFlow.RabbitMq.MediatR;

internal class NotificationConsumer<TRequest> : IConsumer<TRequest>, IGenericConsumer
    where TRequest: INotification
{
    private readonly IMediator _mediator;

    public NotificationConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Handle(IMessage<TRequest> message, CancellationToken cancellationToken)
    {
        await _mediator
            .Publish(message.Body, cancellationToken)
            .ConfigureAwait(false);
    }
}
