using StreamFlow.RabbitMq.Connection;

namespace StreamFlow.RabbitMq;

public class RabbitMqPublisherOptions
{
    internal ConfirmationType? ConfirmationType { get; set; }
    internal bool IsPublisherHostEnabled { get; set; }

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
}
