using System;
using Microsoft.Extensions.DependencyInjection;

namespace StreamFlow
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStreamFlow(this IServiceCollection services, Action<IStreamFlow> build)
        {
            var registrations = new ConsumerRegistrations();
            services.AddSingleton<IConsumerRegistrations>(_ => registrations);

            var builder = new StreamFlowBuilder(services, registrations);

            build(builder);

            return services;
        }
    }
}
