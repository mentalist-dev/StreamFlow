using StreamFlow.RabbitMq.Connection;

namespace StreamFlow.RabbitMq;

public class RabbitMqPublisherOptions
{
    internal ConfirmationType? ConfirmationType { get; set; }
    internal bool IsPublisherHostEnabled { get; set; }
    internal RabbitMqPublisherPoolOptions? PoolOptions { get; set; } = new();

    public RabbitMqPublisherOptions EnablePublisherHost(bool enable = true)
    {
        IsPublisherHostEnabled = enable;
        return this;
    }

    public RabbitMqPublisherOptions EnablePublisherTransactions(bool enable = true)
    {
        ConfirmationType = null;
        if (enable)
        {
            ConfirmationType = Connection.ConfirmationType.Transactional;
        }

        return this;
    }

    public RabbitMqPublisherOptions EnablePublisherConfirms(bool enable = true)
    {
        ConfirmationType = null;

        if (enable)
        {
            ConfirmationType = Connection.ConfirmationType.PublisherConfirms;
        }

        return this;
    }

    public RabbitMqPublisherOptions EnablePublisherPooling(uint desiredPoolSize = 10, TimeSpan? publisherRetentionPeriod = null, bool enable = true)
    {
        PoolOptions = null;

        if (enable)
        {
            PoolOptions = new RabbitMqPublisherPoolOptions
            {
                DesiredPoolSize = desiredPoolSize,
                RetentionPeriod = publisherRetentionPeriod ?? TimeSpan.FromMinutes(1)
            };
        }

        return this;
    }
}
