using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StreamFlow.Tests.AspNetCore.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace StreamFlow.Tests.AspNetCore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IPublisher _publisher;

        public HomeController(ILogger<HomeController> logger, IPublisher publisher)
        {
            _logger = logger;
            _publisher = publisher;
        }

        public async Task<IActionResult> Index()
        {
            await _publisher.PublishAsync(
                new PingRequest {Timestamp = DateTime.UtcNow},
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
