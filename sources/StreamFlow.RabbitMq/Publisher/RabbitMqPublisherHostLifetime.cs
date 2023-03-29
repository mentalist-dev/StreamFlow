using Microsoft.Extensions.Hosting;

namespace StreamFlow.RabbitMq.Publisher;

public interface IRabbitMqPublisherHostLifetime
{
    void OnApplicationStopping(Action callback);
}

internal class RabbitMqPublisherHostLifetime: IRabbitMqPublisherHostLifetime
{
    private readonly IHostApplicationLifetime? _lifetime;

    public RabbitMqPublisherHostLifetime()
    {
    }

    public RabbitMqPublisherHostLifetime(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    public void OnApplicationStopping(Action callback)
    {
        _lifetime?.ApplicationStopping.Register(callback);
    }
}
