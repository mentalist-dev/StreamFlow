using RabbitMQ.Client;

namespace StreamFlow.RabbitMq.Connection
{
    public class RabbitMqChannel: IDisposable
    {
        public RabbitMqChannel(IConnection connection, ConfirmationType? confirmationType)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Channel = connection.CreateModel();
            Confirmation = confirmationType;

            switch (confirmationType)
            {
                case ConfirmationType.PublisherConfirms:
                    Channel.ConfirmSelect();
                    break;
                case ConfirmationType.Transactional:
                    Channel.TxSelect();
                    break;
                case null:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(confirmationType), confirmationType, null);
            }
        }

        public IConnection Connection { get; }
        public IModel Channel { get; }
        public ConfirmationType? Confirmation { get; }

        public void Dispose()
        {
            Channel.Dispose();
        }

        public void Confirm(TimeSpan? timeout = null)
        {
            switch (Confirmation)
            {
                case ConfirmationType.PublisherConfirms:
                    timeout ??= Timeout.InfiniteTimeSpan;
                    Channel.WaitForConfirms(timeout.Value);
                     break;
                case ConfirmationType.Transactional:
                    Channel.TxCommit();
                    break;
                case null:
                    break;
            }
        }
    }
}
