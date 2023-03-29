using Microsoft.Extensions.DependencyInjection;

namespace StreamFlow.RabbitMq.Publisher;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqPublisherHost, RabbitMqPublisherHost>();
        services.AddSingleton<IRabbitMqPublisherHostLifetime, RabbitMqPublisherHostLifetime>();
        services.AddSingleton<IRabbitMqService, RabbitMqService>();
        services.AddSingleton<IRabbitMqServiceFactory, RabbitMqServiceFactory>();
        services.AddSingleton<IRabbitMqPublicationQueue, RabbitMqPublicationQueue>();

        services.AddTransient<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddTransient<IPublisher, RabbitMqPublisher>();

        return services;
    }
}
