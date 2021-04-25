using System;
using StreamFlow.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStreamFlow(this IServiceCollection services, Action<IStreamFlowTransport> transport)
        {
            var registrations = new ConsumerRegistrations();
            services.AddSingleton<IConsumerRegistrations>(_ => registrations);

            var builder = new StreamFlowBuilder(services, registrations);

            transport(builder);

            return services;
        }
    }
}
