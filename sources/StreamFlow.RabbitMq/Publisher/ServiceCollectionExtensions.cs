using Microsoft.Extensions.DependencyInjection;

namespace StreamFlow.RabbitMq.Publisher;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqPublisherHost>();
        services.AddSingleton<IRabbitMqService, RabbitMqService>();
        services.AddSingleton<IRabbitMqServiceFactory, RabbitMqServiceFactory>();
        services.AddSingleton<IRabbitMqPublicationQueue, RabbitMqPublicationQueue>();

        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<IRabbitMqPublisher>(p => p.GetRequiredService<RabbitMqPublisher>());
        services.AddSingleton<IPublisher>(p => p.GetRequiredService<RabbitMqPublisher>());

        return services;
    }
}
