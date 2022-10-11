using System;
using StreamFlow;
using StreamFlow.Configuration;
using StreamFlow.Outbox;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStreamFlow(this IServiceCollection services, StreamFlowOptions options, Action<IStreamFlowTransport> transport)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (transport == null) throw new ArgumentNullException(nameof(transport));

        var registrations = new ConsumerRegistrations();
        services.AddSingleton<IConsumerRegistrations>(_ => registrations);

        services.AddSingleton(options);

        var builder = new StreamFlowBuilder(services, registrations, options);

        transport(builder);

        builder.WithOutboxSupport(_ => { });

        return services;
    }

    public static IServiceCollection AddStreamFlow(this IServiceCollection services, Action<IStreamFlowTransport> transport)
    {
        return AddStreamFlow(services, new StreamFlowOptions(), transport);
    }
}
