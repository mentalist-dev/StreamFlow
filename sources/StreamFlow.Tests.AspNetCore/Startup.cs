using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StreamFlow.RabbitMq;
using StreamFlow.RabbitMq.Server;

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

            services.AddStreamFlow(flow =>
            {
                flow
                    .RabbitMqTransport(mq => mq
                        .ConnectTo("localhost", "guest", "guest")
                        .ConsumeInHostedService()
                        .WithConsumerPipe<CustomConsumerPipe>()
                        .WithPublisherPipe<CustomPublisherPipe>()
                    )
                    .Consumes<PingRequest, PingRequestConsumer>(new ConsumerOptions {ConsumerCount = 5, ConsumerGroup = "gr1"})
                    .Consumes<PingRequest, PingRequestConsumer>(new ConsumerOptions {ConsumerCount = 5, ConsumerGroup = "gr2"})
                    ;
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

            publisher.Publish(new PingRequest {Timestamp = DateTime.UtcNow});
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

    public class CustomConsumerPipe : RabbitMqConsumerPipe
    {
        private readonly ILogger<CustomConsumerPipe> _logger;

        public CustomConsumerPipe(ILogger<CustomConsumerPipe> logger)
        {
            _logger = logger;
        }

        public override Task<IDisposable> Create(RabbitMqExecutionContext context)
        {
            _logger.LogInformation($"AppId: {context.Event.BasicProperties?.AppId}");
            IDisposable scope = new RabbitMqScope();
            return Task.FromResult(scope);
        }
    }

    public class CustomPublisherPipe : IRabbitMqPublisherPipe
    {
        public Task Execute<T>(IBasicProperties properties)
        {
            properties.AppId = ".NET Core";
            return Task.CompletedTask;
        }
    }
}
