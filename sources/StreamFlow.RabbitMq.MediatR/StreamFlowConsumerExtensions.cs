using MediatR;
using StreamFlow.Configuration;

namespace StreamFlow.RabbitMq.MediatR;

public static class StreamFlowConsumerExtensions
{
    public static IStreamFlowConsumer AddNotification<TRequest>(this IStreamFlowConsumer builder, Action<IConsumerOptionsBuilder>? consumer = null)
        where TRequest : INotification
    {
        builder.Add<TRequest, NotificationConsumer<TRequest>>(consumer);
        return builder;
    }

    public static IStreamFlowConsumer AddRequest<TRequest>(this IStreamFlowConsumer builder, Action<IConsumerOptionsBuilder>? consumer = null)
        where TRequest : IRequest
    {
        builder.Add<TRequest, RequestConsumer<TRequest>>(consumer);
        return builder;
    }

    public static IStreamFlowConsumer AddRequest<TRequest, TResponse>(this IStreamFlowConsumer builder, Action<IConsumerOptionsBuilder>? consumer = null)
        where TRequest : IRequest<TResponse>
    {
        builder.Add<TRequest, RequestConsumer<TRequest, TResponse>>(consumer);
        return builder;
    }
}
