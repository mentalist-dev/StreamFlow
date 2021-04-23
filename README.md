# StreamFlow - Just another RabbitMQ handler

## Install via NuGet

If you want to include the HTTP sink in your project, you can [install it directly from NuGet](https://www.nuget.org/packages/Serilog.Sinks.Logz.Io/).

To install the sink, run the following command in the Package Manager Console:

```
PM> Install-Package StreamFlow.RabbitMq
```

## Super simple to use

Define a message:

```
public class PingRequest
{
    public DateTime Timestamp { get; set; }
}
```

Define message consumer:

```
public class PingRequestConsumer : IConsumer<PingRequest>
{
    public Task Handle(IMessage<PingRequest> message)
    {
        Console.WriteLine(message.Body.Timestamp);
        return Task.CompletedTask;
    }
}
```

Register using ASP.NET Core:

```
services.AddStreamFlow(flow =>
{
    flow
        .RabbitMqTransport(mq => mq
            .ConnectTo("localhost", "guest", "guest")
            .ConsumeInHostedService())
        .Consumes<PingRequest, PingRequestConsumer>(new ConsumerOptions {ConsumerCount = 5, ConsumerGroup = "gr1"});
});
```
