using System;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqScopeFactory
    {
        IDisposable Create(RabbitMqExecutionContext context);
    }

    public class RabbitMqScopeFactory: IRabbitMqScopeFactory
    {
        public virtual IDisposable Create(RabbitMqExecutionContext context)
        {
            return new RabbitMqScope();
        }

        protected class RabbitMqScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
