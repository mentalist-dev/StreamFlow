# StreamFlow - Just another RabbitMQ handler

## Justification

I know there are multiple cool RabbitMQ wrappers (MassTransit, EasyNetQ and many more) in the wild which does the job much better then this library.
However I was never been able to use them as is without overriding multiple methods or classes. So I decided to make my own wrapper with my own needs.

## Install via NuGet

If you want to include the HTTP sink in your project, you can [install it directly from NuGet](https://www.nuget.org/packages/Serilog.Sinks.Logz.Io/).

To install the sink, run the following command in the Package Manager Console:

```
PM> Install-Package StreamFlow.RabbitMq
```

## Concepts

All names in RabbitMQ server can be defined by implementing IRabbitMqConventions and registering in dependency injection.
However default behavior works like this:

- exchange name is created using request class name
- queue name is created using <request class name>:<request handler name>[:<consumer group name>]
- error queue name is created using <request class name>:<request handler name>[:<consumer group name>]:Error

## Super simple to use

Define a message:

```
public class PingRequest
{
    public DateTime Timestamp { get; set; }
}
```

Publish message:

```
public class PingController
{
    private readonly IPublisher _publisher;
    
    public PingController(IPublisher publisher)
    {
        _publisher = publisher;
    }
    
    public async Task SendPing()
    {
        await _publisher.PublishAsync(new PingRequest { Timestamp = DateTime.UtcNow; });
    }
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

Configure using ASP.NET Core:

```
services.AddStreamFlow(transport =>
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
        )
        .ConfigureConsumerPipe(builder => builder
            .Use<LogAppIdMiddleware>()
        )
        .ConfigurePublisherPipe(builder => builder
            .Use(_ => new SetAppIdMiddleware("Published from StreamFlow.Tests.AspNetCore"))
        );
});
```

The code above registers stream flow classes, configured RabbitMQ connection and instructs to start ASP.NET Core hosted service which starts configured consumers.
It configures 2 consumer groupds launching 5 instances for each group.

```
A consumer group is a group of consumers that share the same group id. 
When a topic is consumed by consumers in the same group, every record 
will be delivered to only one consumer.
If all the consumer instances have the same consumer group, then the 
records will effectively be load-balanced over the consumer instances.
```

ConfigureConsumerPipe and ConfigurePublisherPipe registers middleware-like actions which are executed when message is received or when published respectively.
Code of these middlewares:

```
// used at consumer side to log received AppId
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

        return next(context);
    }
}

// used at publisher side to set custom AppId
public class SetAppIdMiddleware : IStreamFlowMiddleware
{
    private readonly string _appId;

    public SetAppIdMiddleware(string appId)
    {
        _appId = appId;
    }

    public Task Invoke(IMessageContext context, Func<IMessageContext, Task> next)
    {
        context.WithAppId(_appId);
        return next(context);
    }
}
```

Even though those example middlewares are very simple in the real world scenarios it can create very powerfull implementations:

- retries
- exception handling
- logging
- metrics
- ...

## Error Handling

Every application deals with errors. I am pretty sure - RabbitMQ handlers can get into multiple exceptional cases.
In case of unhandled exception IRabbitMqErrorHandler instance is created which by default will send a message to error queue.
Even though you can define your own error queue name by overriding IRabbitMqConventions interface, default behavior simply
add ":Error" suffix to the end of the consumer group queue and send message to that queue with exception details in message header.
Application developers or support can later decide what to do with those messages.

