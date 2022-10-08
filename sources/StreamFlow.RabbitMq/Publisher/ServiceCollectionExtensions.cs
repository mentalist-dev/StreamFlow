using Microsoft.Extensions.DependencyInjection;

namespace StreamFlow.RabbitMq.Publisher;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqPublisherHost>();
        services.AddSingleton<IRabbitMqPublicationQueue, RabbitMqPublicationQueue>();
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddSingleton<IPublisher, RabbitMqPublisher>();

        return services;
    }
}
