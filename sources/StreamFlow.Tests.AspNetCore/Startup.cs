using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamFlow.RabbitMq;

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

            var streamFlowOptions = new StreamFlowOptions {ServiceId = "sf-tests"};

            services.AddStreamFlow(streamFlowOptions, transport =>
            {
                transport
                    .UsingRabbitMq(mq => mq
                        .Connection("localhost", "guest", "guest")
                        .StartConsumerHostedService()
                    )
                    .Consumers(builder => builder
                        .Add<PingRequest, PingRequestConsumer>(options => options
                            .ConsumerCount(5)
                            .ConsumerGroup("gr1"))
                        .Add<PingRequest, PingRequestConsumer>(options => options
                            .ConsumerCount(5)
                            .ConsumerGroup("gr2"))
                        .Add<PingRequestConsumer>()
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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            Task.Factory.StartNew(() => publisher.PublishAsync(new PingRequest {Timestamp = DateTime.UtcNow}));
        }
    }

    public class PingRequest
    {
        public DateTime Timestamp { get; set; }
    }

    public class PingRequestConsumer : IConsumer<PingRequest>
    {
        public Task Handle(IMessage<PingRequest> message)
        {
            Console.WriteLine(message.Body.Timestamp);
            throw new Exception("Unable to handle!");
            return Task.CompletedTask;
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

            return next(context);
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
