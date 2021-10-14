using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using StreamFlow.RabbitMq;
using StreamFlow.RabbitMq.Prometheus;
using StreamFlow.Tests.AspNetCore.Application.TimeSheetEdited;
using StreamFlow.Tests.AspNetCore.Database;
using StreamFlow.Tests.AspNetCore.Models;

namespace StreamFlow.Tests.AspNetCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            var streamFlowOptions = new StreamFlowOptions
            {
                ServiceId = "sf-tests",
                QueuePrefix = "SF.",
                ExchangePrefix = "SF."
            };

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql("Server=localhost;User Id=admin;Password=admin;Database=StreamFlow");
            });

            services.AddStreamFlow(streamFlowOptions, transport =>
            {
                transport
                    .UsingRabbitMq(mq => mq
                        .Connection("localhost", "guest", "guest")
                        .StartConsumerHostedService()
                        .WithPrometheusMetrics()
                        .WithPublisherChannelPoolOptions(new RabbitMqChannelPoolOptions {MaxPoolSize = 10})
                    )
                    /*
                    .WithOutboxSupport(outbox =>
                    {
                        outbox
                            .UseEntityFrameworkCore<ApplicationDbContext>()
                            .UsePublishingServer();
                    })
                    */
                    .Consumers(builder => builder
                        /*
                        .Add<PingRequest, PingRequestConsumer>(options => options
                            .ConsumerCount(5)
                            .ConsumerGroup("gr1"))
                        .Add<PingRequest, PingRequestConsumer>(options => options
                            .ConsumerCount(5)
                            .ConsumerGroup("gr2"))
                        */
                        /*
                        .Add<PingRequest, PingRequestDelayedConsumer>(options => options
                            .ConsumerCount(1)
                            .ConsumerGroup("gr3"))
                        */
                        .Add<PingRequest, PingRequestConsumer>(options => options
                            .ConsumerCount(1)
                            .ConsumerGroup("gr4")
                            .IncludeHeadersToLoggerScope()
                            .ConfigureQueue(q => q.AutoDelete())
                        )
                        .Add<TimeSheetEditedEvent, TimeSheetEditedEventConsumer>()
                        // .Add<PingRequestConsumer>()
                    )
                    .ConfigureConsumerPipe(builder => builder
                        .Use<LogAppIdMiddleware>()
                    )
                    .ConfigurePublisherPipe(builder => builder
                        .Use(_ => new SetAppIdMiddleware("Published from StreamFlow.Tests.AspNetCore", "StreamFlow Asp.Net Core Test"))
                    );
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IPublisher publisher)
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

            /*
            Task.Factory.StartNew(() => publisher.PublishAsync(
                new PingRequest { Timestamp = DateTime.UtcNow },
                new PublishOptions { PublisherConfirmsEnabled = true })
            );
            */
        }
    }

    public interface IDomainEvent
    {

    }

    public class PingRequest: IDomainEvent
    {
        public DateTime Timestamp { get; set; }
    }

    public class PingRequestConsumer : IConsumer<PingRequest>
    {
        public Task Handle(IMessage<PingRequest> message, CancellationToken cancellationToken)
        {
            Console.WriteLine(message.Body.Timestamp);
            // throw new Exception("Unable to handle!");
            return Task.CompletedTask;
        }
    }

    public class PingRequestDelayedConsumer : IConsumer<PingRequest>
    {
        public Task Handle(IMessage<PingRequest> message, CancellationToken cancellationToken)
        {
            return Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
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
}
