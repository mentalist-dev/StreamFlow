using StreamFlow;
using StreamFlow.RabbitMq;
using StreamFlow.RabbitMq.Publisher;
using StreamFlow.Tests.Contracts;

var services = new ServiceCollection();

var streamFlowOptions = new StreamFlowOptions
{
    ServiceId = "publisher-console",
    QueuePrefix = "SFQ.",
    ExchangePrefix = "SFE."
};

services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
services.AddStreamFlow(streamFlowOptions, transport =>
{
    transport.UseRabbitMq(mq => mq
        .Connection("localhost", "guest", "guest")
    );
});

var cts = new CancellationTokenSource();

var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<Program>>();
var hostedServices = provider
    .GetServices<IHostedService>()
    .ToList();

foreach (var hostedService in hostedServices)
{
    await hostedService.StartAsync(cts.Token);
}

var finished = false;

while (!finished)
{
    Console.WriteLine("Choose an option:");
    Console.WriteLine("ANY: publish PingRequest");
    Console.WriteLine("ESC: exit");
    Console.WriteLine("  A: start single threaded publications (any key will stop it)");
    Console.WriteLine("  B: start 10 threads for publications (any key will stop it)");
    Console.WriteLine("  C: publish to non existing exchange");
    Console.WriteLine("  D: publish error request (consumer will fail)");

    var key = Console.ReadKey(false);
    Console.WriteLine();

    try
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape:
            {
                finished = true;
                break;
            }
            case ConsoleKey.A:
            {
                await PublishMessagesAsync(provider, logger);

                break;
            }
            case ConsoleKey.B:
            {
                await PublishThreadedMessages(provider, logger);

                break;
            }
            case ConsoleKey.C:
            {
                await PublishToNonExistingExchange(provider, logger);

                break;
            }
            case ConsoleKey.D:
            {
                await PublishErrorRequest(provider, logger);

                break;
            }
            default:
            {
                await PublishSingleMessage(provider, logger);
                break;
            }

        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
}

foreach (var hostedService in hostedServices)
{
    await hostedService.StopAsync(cts.Token);
}

cts.Cancel();
cts.Dispose();

async Task PublishMessagesAsync(ServiceProvider provider1, ILogger<Program> logs)
{
    await using var scope = provider1.CreateAsyncScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

    var tasks = new List<Task>();
    var count = 0;
    while (!Console.KeyAvailable)
    {
        count += 1;
        var request = new PingRequest { Timestamp = DateTime.Now };

        var task = publisher.PublishAsync(request, new PublishOptions { Timeout = TimeSpan.FromSeconds(60) });
        tasks.Add(task);

        if (count % 100 == 0)
        {
            logs.LogInformation("Published {Count} messages", count);
        }

        if (tasks.Count >= 1000)
        {
            Task.WaitAll(tasks.ToArray());
            tasks = new List<Task>();
        }
    }

    // clean buffer
    while (Console.KeyAvailable)
    {
        Console.ReadKey();
    }
}

async Task PublishThreadedMessages(ServiceProvider provider1, ILogger<Program> logs)
{
    await using var scope = provider1.CreateAsyncScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

    using var cancellation = new CancellationTokenSource();
    var ct = cancellation.Token;
    var threads = new Task[10];

    for (int i = 0; i < threads.Length; i++)
    {
        var threadNo = i;
        threads[threadNo] = Task.Run(() =>
        {
            var tasks = new List<Task>();
            var count = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    count += 1;
                    var request = new PingRequest { Timestamp = DateTime.Now };
                    var task = publisher.PublishAsync(request, new PublishOptions { Timeout = TimeSpan.FromSeconds(60) });
                    tasks.Add(task);

                    if (count % 100 == 0)
                    {
                        logs.LogInformation("[{Thread}] Published {Count} messages", threadNo, count);
                    }

                    if (tasks.Count >= 1000)
                    {
                        Task.WaitAll(tasks.ToArray());
                        tasks = new List<Task>();
                    }
                }
                catch (Exception e)
                {
                    logs.LogError(e, "[{Thread}] failed to publish {MessageNo} message", threadNo, count);
                }
            }

            return Task.CompletedTask;
        });
    }

    Console.ReadKey(true);

    cancellation.Cancel();

    Task.WaitAll(threads);

    // clean buffer
    while (Console.KeyAvailable)
    {
        Console.ReadKey();
    }
}

async Task PublishToNonExistingExchange(ServiceProvider serviceProvider, ILogger<Program> logs)
{
    await using var scope = serviceProvider.CreateAsyncScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();
    var request = new PingRequest {Timestamp = DateTime.Now};
    logs.LogInformation("Publishing request: {@Request}", request);
    await publisher.PublishAsync(request, new PublishOptions {Timeout = TimeSpan.FromSeconds(60), Exchange = Guid.NewGuid().ToString(), IgnoreNoRouteEvents = true});
    logs.LogInformation("Request published {@Request}", request);
}

async Task PublishErrorRequest(ServiceProvider serviceProvider, ILogger<Program> logs)
{
    await using var scope = serviceProvider.CreateAsyncScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();
    var request = new RaiseErrorRequest();
    logs.LogInformation("Publishing request: {@Request}", request);
    await publisher.PublishAsync(request, new PublishOptions {Timeout = TimeSpan.FromSeconds(60), CreateExchangeEnabled = true});
    logs.LogInformation("Request published {@Request}", request);
}

async Task PublishSingleMessage(ServiceProvider serviceProvider, ILogger<Program> logs)
{
    await using var scope = serviceProvider.CreateAsyncScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();
    var request = new PingRequest {Timestamp = DateTime.Now};
    logs.LogInformation("Publishing request: {@Request}", request);
    await publisher.PublishAsync(request, new PublishOptions {Timeout = TimeSpan.FromSeconds(60), CreateExchangeEnabled = true});
    logs.LogInformation("Request published {@Request}", request);
}
