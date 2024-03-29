using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StreamFlow.Pipes;
using StreamFlow.RabbitMq;
using StreamFlow.RabbitMq.Publisher;
using StreamFlow.Tests.Contracts;
using Xunit;

namespace StreamFlow.Tests;

public class RabbitMqPublisherShould
{
    [Fact]
    public async Task PublishAsync()
    {
        var publicationQueue = Substitute.For<IRabbitMqPublicationQueue>();
        var metrics = Substitute.For<IRabbitMqMetrics>();
        var collection = new ServiceCollection();
        var services = collection.BuildServiceProvider();

        var host = Substitute.For<IRabbitMqPublisherHost>();
        host.IsRunning.Returns(true);

        var publisher = new RabbitMqPublisher(
            new RabbitMqPublisherOptions(),
            new StreamFlowPipe(),
            publicationQueue,
            new RabbitMqConventions(new StreamFlowOptions()),
            new RabbitMqMessageSerializer(),
            metrics,
            services,
            host);

        publicationQueue
            .When(q => q.Publish(Arg.Any<RabbitMqPublication>()))
            .Do(p => p.Arg<RabbitMqPublication>().Complete());

        await publisher.PublishAsync(new PingMessage());
    }

    [Fact]
    public async Task PublishWithMiddlewareAsync()
    {
        var publicationQueue = Substitute.For<IRabbitMqPublicationQueue>();
        var metrics = Substitute.For<IRabbitMqMetrics>();
        var collection = new ServiceCollection();
        collection.AddScoped<RequestContext>();
        collection.AddScoped<RabbitMqPublisherMiddleware>();

        var pipeBuilder = new StreamFlowPipeBuilder(collection);
        pipeBuilder.Use(p => p.GetRequiredService<RabbitMqPublisherMiddleware>());

        var pipe = new StreamFlowPipe();
        pipe.AddRange(pipeBuilder.Actions);

        var services = collection.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = services.CreateScope();

        var id = Guid.NewGuid();

        var context = scope.ServiceProvider.GetRequiredService<RequestContext>();
        context.Current.Id = id;

        var host = Substitute.For<IRabbitMqPublisherHost>();
        host.IsRunning.Returns(true);

        var publisher = new RabbitMqPublisher(
            new RabbitMqPublisherOptions(),
            pipe,
            publicationQueue,
            new RabbitMqConventions(new StreamFlowOptions()),
            new RabbitMqMessageSerializer(),
            metrics,
            scope.ServiceProvider,
            host);

        publicationQueue
            .When(q => q.Publish(Arg.Any<RabbitMqPublication>()))
            .Do(p => p.Arg<RabbitMqPublication>().Complete());

        await publisher.PublishAsync(new PingMessage());

        publicationQueue
            .Received(1)
            .Publish(Arg.Is<RabbitMqPublication>(p => IsValid(p, id)));
    }

    private static bool IsValid(RabbitMqPublication p, Guid id)
    {
        var header = p.Context.GetHeader("account-id", string.Empty);
        return header == id.ToString();
    }

    private class RabbitMqPublisherMiddleware : IStreamFlowMiddleware
    {
        private readonly RequestContext _context;

        public RabbitMqPublisherMiddleware(RequestContext context)
        {
            _context = context;
        }

        public Task Invoke(IMessageContext context, Func<IMessageContext, Task> next)
        {
            var id = _context.Current.Id;
            context.SetHeader("account-id", id.ToString());
            return next(context);
        }
    }

    private class RequestContext
    {
        private static readonly AsyncLocal<RequestContextData> Data = new();

        public RequestContext()
        {
            Data.Value = new RequestContextData();
        }

        public RequestContextData Current => Data.Value;
    }

    private class RequestContextData
    {
        public Guid Id { get; set; }
    }
}
