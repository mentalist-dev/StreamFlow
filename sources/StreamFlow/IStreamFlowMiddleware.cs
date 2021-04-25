using System;
using System.Threading.Tasks;

namespace StreamFlow
{
    public interface IStreamFlowMiddleware
    {
        Task Invoke(IMessageContext context, Func<IMessageContext, Task> next);
    }
}
