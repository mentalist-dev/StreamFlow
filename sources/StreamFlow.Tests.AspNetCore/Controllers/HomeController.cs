using Microsoft.AspNetCore.Mvc;
using StreamFlow.Tests.AspNetCore.Models;
using System.Diagnostics;
using StreamFlow.Outbox;
using StreamFlow.Tests.AspNetCore.Application.TimeSheetEdited;

namespace StreamFlow.Tests.AspNetCore.Controllers
{
    public class HomeController : Controller
    {
        private readonly IOutboxPublisher _publisher;

        public HomeController(IOutboxPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task<IActionResult> Index()
        {
            IDomainEvent request = new PingRequest {Timestamp = DateTime.UtcNow};
            await _publisher.PublishAsync(
                request,
                new PublishOptions
                {
                    Headers =
                    {
                        {"index", "sent-from-index"},
                        {"index-id", Guid.NewGuid()},
                        {"check-priority", "set inside index"},
                    }
                }
            );

            return View();
        }

        public async Task<IActionResult> TimeSheet()
        {
            var ev = new TimeSheetEditedEvent
            {
                PortfolioId = Guid.NewGuid(),
                SecurityUserId = Guid.NewGuid(),
                TimeEntryId = Guid.NewGuid(),
                Changes = new TimeSheetChangesDto
                {
                    TotalAmount = new StateChangeObj<decimal>(100, 50)
                }
            };

            await _publisher.PublishAsync(ev);

            return Ok();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
