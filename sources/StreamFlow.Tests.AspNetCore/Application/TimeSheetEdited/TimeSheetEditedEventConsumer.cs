using System;
using System.Threading;
using System.Threading.Tasks;

namespace StreamFlow.Tests.AspNetCore.Application.TimeSheetEdited
{
    public class TimeSheetEditedEventConsumer : IConsumer<TimeSheetEditedEvent>
    {
        public Task Handle(IMessage<TimeSheetEditedEvent> message, CancellationToken cancellationToken)
        {
            Console.WriteLine(message.Body.PortfolioId.ToString());
            return Task.CompletedTask;
        }
    }
}
