using System;
using System.Threading.Tasks;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqConsumerPipe
    {
        Task<IDisposable> Create(RabbitMqExecutionContext context);
    }

    public class RabbitMqConsumerPipe: IRabbitMqConsumerPipe
    {
        public virtual Task<IDisposable> Create(RabbitMqExecutionContext context)
        {
            IDisposable scope = new RabbitMqScope();
            return Task.FromResult(scope);
        }

        protected class RabbitMqScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
