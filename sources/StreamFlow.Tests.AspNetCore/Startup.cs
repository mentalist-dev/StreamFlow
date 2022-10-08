using MediatR;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StreamFlow.RabbitMq;
using StreamFlow.RabbitMq.MediatR;
using StreamFlow.RabbitMq.Prometheus;
using StreamFlow.RabbitMq.Publisher;
using StreamFlow.Tests.AspNetCore.Application.Errors;
using StreamFlow.Tests.AspNetCore.Application.Ping;
using StreamFlow.Tests.AspNetCore.Application.TimeSheetEdited;
using StreamFlow.Tests.AspNetCore.Database;
using StreamFlow.Tests.Contracts;

namespace StreamFlow.Tests.AspNetCore;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
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

        services.AddMediatR(typeof(Startup).Assembly);

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
                .Consumers(builder => builder
                    .Add<PingMessage, PingMessageConsumer>(options => options
                        .ConsumerCount(25)
                        .ConsumerGroup("gr4")
                        .IncludeHeadersToLoggerScope()
                        .ConfigureQueue(q => q.AutoDelete())
                    )
                    .Add<TimeSheetEditedEvent, TimeSheetEditedEventConsumer>()
                    .Add<RaiseErrorRequest, RaiseRequestConsumer>(opt => opt.Prefetch(1))
                    .AddNotification<PingNotification>(opt => opt.Prefetch(1))
                )
                .ConfigureConsumerPipe(builder => builder
                    .Use<LogAppIdMiddleware>()
                )
                .ConfigurePublisherPipe(builder => builder
                    .Use(_ => new SetAppIdMiddleware("Published from StreamFlow.Tests.AspNetCore", "StreamFlow Asp.Net Core Test"))
                );
        });

        //services.AddHostedService<HighLoadPublisherHostedService>();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IRabbitMqPublisher bus, IHostApplicationLifetime lifetime, IServiceProvider services)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
        }
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

        // Task.Factory.StartNew(() => StartPublisher(bus, lifetime.ApplicationStopping), TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);

        /*
        Task.Factory.StartNew(() => publisher.PublishAsync(
            new PingMessage { Timestamp = DateTime.UtcNow },
            new PublishOptions { PublisherConfirmsEnabled = true })
        );
        */
    }

    private async Task StartPublisher(IRabbitMqPublisher bus, CancellationToken cancellationToken)
    {
        var counter = 0;

        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            counter += 1;

            await bus.PublishAsync(new PingMessage
            {
                Timestamp = DateTime.UtcNow,
                Message = counter.ToString()
            }, cancellationToken: cancellationToken);

            await bus.PublishAsync(new PingNotification
            {
                Timestamp = DateTime.UtcNow,
                Message = counter.ToString()
            }, cancellationToken: cancellationToken);


            if ((counter-1) % 10 == 0)
            {
                try
                {
                    await bus.PublishAsync(new RaiseErrorRequest(), cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unable to publish RaiseRequest: {e.Message}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }
}

public class LogAppIdMiddleware : IStreamFlowMiddleware
{
    private readonly ILogger<LogAppIdMiddleware> _logger;

    public LogAppIdMiddleware(ILogger<LogAppIdMiddleware> logger)
    {
        _logger = logger;
    }

    public Task Invoke(IMessageContext context, Func<IMessageContext, Task> next)
    {
        if (!string.IsNullOrWhiteSpace(context.AppId))
        {
            _logger.LogInformation("AppId: {AppId}", context.AppId);
        }

        var value = context.GetHeader("not-existent", "not found");
        _logger.LogInformation("not-existent header value: {HeaderValue}", value);

        var customAppName = context.GetHeader("custom_app_name", "");
        _logger.LogInformation("custom app name header value: {CustomAppName}", customAppName);

        var customAppId = context.GetHeader("custom_app_id", Guid.Empty);
        _logger.LogInformation("custom app id header value: {CustomAppId}", customAppId);

        var customAppIdString = context.GetHeader("custom_app_id", string.Empty);
        _logger.LogInformation("custom app id (string) header value: {CustomAppId}", customAppIdString);

        var index = context.GetHeader("index", string.Empty);
        _logger.LogInformation("index header value: {Index}", index);

        var indexId = context.GetHeader("index-id", string.Empty);
        _logger.LogInformation("index-id header value: {IndexId}", indexId);

        var priority = context.GetHeader("check-priority", string.Empty);
        _logger.LogInformation("check-priority header value: {Priority}", priority);

        var state = new List<KeyValuePair<string, object>> { new("Account", "Account Name") };

        using (_logger.BeginScope(state))
        {
            return next(context);
        }
    }
}

public class SetAppIdMiddleware : IStreamFlowMiddleware
{
    private readonly string _appId;
    private readonly string _customAppName;

    public SetAppIdMiddleware(string appId, string customAppName)
    {
        _appId = appId;
        _customAppName = customAppName;
    }

    public Task Invoke(IMessageContext context, Func<IMessageContext, Task> next)
    {
        context.WithAppId(_appId);
        context.SetHeader("custom_app_name", _customAppName);
        context.SetHeader("custom_app_id", Guid.NewGuid());
        context.SetHeader("check-priority", "set-inside-middleware");
        return next(context);
    }
}
