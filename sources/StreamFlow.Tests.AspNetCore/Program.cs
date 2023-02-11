using MediatR;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using StreamFlow.Tests.AspNetCore.Database;
using StreamFlow.Tests.AspNetCore;
using StreamFlow;
using StreamFlow.RabbitMq;
using StreamFlow.RabbitMq.MediatR;
using StreamFlow.RabbitMq.Prometheus;
using StreamFlow.RabbitMq.Publisher;
using StreamFlow.Tests.AspNetCore.Application;
using StreamFlow.Tests.AspNetCore.Application.Errors;
using StreamFlow.Tests.AspNetCore.Application.Ping;
using StreamFlow.Tests.AspNetCore.Application.TimeSheetEdited;
using StreamFlow.Tests.Contracts;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((_, configuration) =>
    {
        configuration.MinimumLevel.ControlledBy(new LoggingLevelSwitch(LogEventLevel.Debug));
        configuration.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    }, true);

    var services = builder.Services;
    services.AddControllersWithViews();

    var streamFlowOptions = new StreamFlowOptions
    {
        ServiceId = "sf-tests",
        QueuePrefix = "SFQ.",
        ExchangePrefix = "SFE."
    };

    services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql("Server=localhost;User Id=admin;Password=admin;Database=StreamFlow");
    });

    services.AddMediatR(typeof(Program).Assembly);

    services.AddStreamFlow(streamFlowOptions, transport =>
    {
        transport
            .UseRabbitMq(mq => mq
                .Connection("localhost", "guest", "guest")
                .EnableConsumerHost(consumer => consumer.Prefetch(5))
                .WithPrometheusMetrics()
                .WithPublisherOptions(publisher => publisher
                    .EnableExchangeDeclaration()
                )
            )
            .Consumers(b => b
                .Add<PingMessage, PingMessageConsumer>(options => options
                    .ConsumerCount(25)
                    .ConsumerGroup("gr4")
                    .IncludeHeadersToLoggerScope()
                    .ConfigureQueue(q => q.AutoDelete())
                )
                .Add<TimeSheetEditedEvent, TimeSheetEditedEventConsumer>()
                .Add<RaiseErrorRequest, RaiseErrorRequestConsumer>(opt => opt.RetryOnError(5))
                .AddNotification<PingNotification>(opt => opt.Prefetch(1))
            )
            .ConfigureConsumerPipe(b => b
                .Use<LogAppIdMiddleware>()
            )
            .ConfigurePublisherPipe(b => b
                .Use(_ => new SetAppIdMiddleware("Published from StreamFlow.Tests.AspNetCore", "StreamFlow Asp.Net Core Test"))
            );
    });
    var app = builder.Build();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);

    var publisher = app.Services.GetRequiredService<IRabbitMqPublisher>();

    var request = new TimeSheetEditedEvent();
    await publisher.PublishAsync(
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

    app.UseDeveloperExceptionPage();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthorization();
    app.UseHttpMetrics();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        endpoints.MapMetrics();
    });

    await app.RunAsync();
}
catch (Exception e)
{
    Environment.ExitCode = 1; //code for general errors (https://tldp.org/LDP/abs/html/exitcodes.html)
    Console.WriteLine(e.ToString());
    Log.Fatal(e, "Application Execution Error");
}
finally
{
    Log.CloseAndFlush();
}
