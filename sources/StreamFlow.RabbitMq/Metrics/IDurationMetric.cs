// ReSharper disable once CheckNamespace
namespace StreamFlow.RabbitMq;

public interface IDurationMetric : IDisposable
{
    void Complete();
}
