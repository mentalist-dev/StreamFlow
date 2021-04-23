using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                        .WithScopeFactory<CustomScopeFactory>()
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

    public class CustomScopeFactory : RabbitMqScopeFactory
    {
        private readonly ILogger<CustomScopeFactory> _logger;

        public CustomScopeFactory(ILogger<CustomScopeFactory> logger)
        {
            _logger = logger;
        }

        public override IDisposable Create(RabbitMqExecutionContext context)
        {
            _logger.LogInformation("Creating custom RabbitMq scope");
            return new RabbitMqScope();
        }
    }
}
