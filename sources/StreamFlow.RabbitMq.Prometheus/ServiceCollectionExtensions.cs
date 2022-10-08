namespace StreamFlow.RabbitMq.Prometheus;

public static class ServiceCollectionExtensions
{
    // ReSharper disable once InconsistentNaming
    public static IStreamFlowRabbitMq WithPrometheusMetrics(this IStreamFlowRabbitMq registry)
    {
        registry.WithMetricsProvider<PrometheusRabbitMqMetrics>();
        return registry;
    }
}
