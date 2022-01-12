using MediatR;
using StreamFlow.RabbitMq.Publisher;

namespace StreamFlow.RabbitMq.MediatR;

internal class RequestConsumer<TRequest> : IConsumer<TRequest>, IGenericConsumer
    where TRequest: IRequest
{
    private readonly IMediator _mediator;

    public RequestConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Handle(IMessage<TRequest> message, CancellationToken cancellationToken)
    {
        await _mediator
            .Send(message.Body, cancellationToken)
            .ConfigureAwait(false);
    }
}
internal class RequestConsumer<TRequest, TResponse> : IConsumer<TRequest>, IGenericConsumer
    where TRequest: IRequest<TResponse>
{
    private readonly IMediator _mediator;
    private readonly IRabbitMqPublisherBus _bus;

    public RequestConsumer(IMediator mediator, IRabbitMqPublisherBus bus)
    {
        _mediator = mediator;
        _bus = bus;
    }

    public async Task Handle(IMessage<TRequest> message, CancellationToken cancellationToken)
    {
        var response = await _mediator
            .Send(message.Body, cancellationToken)
            .ConfigureAwait(false);

        if (response != null)
        {
            _bus.Publish(response, new PublishOptions
            {
                CorrelationId = message.Context.CorrelationId,
                IsMandatory = true
            });
        }
    }
}
