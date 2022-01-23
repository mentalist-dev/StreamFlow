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
- queue name is created using `<request class name>:<request handler name>[:<service id>][:<consumer group name>]`
- error queue name is created using `<request class name>:<request handler name>[:<service id>][:<consumer group name>]:Error`

```
A consumer group is a group of consumers that share the same group id. 
When a topic is consumed by consumers in the same group, every record 
will be delivered to only one consumer.
If all the consumer instances have the same consumer group, then the 
records will effectively be load-balanced over the consumer instances.
```

```
Service id identifies service implementation. As for example if we have 
multiple services like customers and orders - they will have different 
service id's. Most simple case here would be to use a short memorable
identifier like: "customers", "orders". 

The purpose of such id is to distinguish message handlers which can have
exactly the same name and can handle exactly the same message. Since these
are in different services - load balancing scenario is not acceptable in this case.

It can be also treated as a consumer group but in this case it applies to whole service.
```

## Component Lifetimes

### Connection

Connection object is registered as singleton. It means that single service will have only one connection to RabbitMQ server.

### IPublisher

Uses scoped life time. Channel is opened one per scope. It means that for each requests where publisher is used - channel will be opened and closed.
If you want to achieve higher publishing performance - need to ensure that IPublisher instance is reused.

### Middleware

Each middleware is added as transient. Every middleware will be created on each received or published message.

### Consumers

Every consumer is singleton. However for each received message own scope is created and disposed at the end of processing.

### Handlers

Handlers are registered as scoped instances.

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
    public Task Handle(IMessage<PingRequest> message, CancellationToken cancellation)
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

## MediatR

There is an implementation for MediatR library which might greatly simplify consumer implementation.

Simply install package:

```
Install-Package StreamFlow.RabbitMq.MediatR
```

Library expects that MediatR itself is already configured and available in dependency injection container.

### MediatR Notifications

Request class must implement INotification interface as for example:

```
public class PingNotification : INotification
{
}
```

And then it can be used in StreamFlow:
```
    ....
    .Consumers(builder => builder
        .AddNotification<PingNotification>()
    )
    ....
```

## MediatR Request without response

Request class must implement IRequest interface as for example:

```
public class PingRequest : IRequest
{
}
```

And then it can be used in StreamFlow:
```
    ....
    .Consumers(builder => builder
        .AddRequest<PingRequest>()
    )
    ....
```

## MediatR Request with response

Request class must implement IRequest interface as for example:

```
public class PongResponse
{
}

public class PingPongRequest : IRequest<PongResponse>
{
}
```

And then it can be used in StreamFlow:
```
    ....
    .Consumers(builder => builder
        .AddRequest<PingPongRequest, PongResponse>()
    )
    ....
```

However in this case you get an additional behavior: response is sent back using RabbitMQ publisher bus.
For this to work you also need to enable publisher host (see EnablePublisherHost call):

```
services.AddStreamFlow(transport =>
{
    transport
        .UseRabbitMq(mq => mq
            .Connection("localhost", "guest", "guest")
            .EnableConsumerHost(consumer => consumer.Prefetch(5))
            .WithPrometheusMetrics()
            .WithPublisherOptions(publisher => publisher
                .EnablePublisherHost()
            )
        )
}    
```

## Metrics

Library has a support for various metrics which represents various information about insights of publishers and consumers.
Out of the box there is an implementation for Prometheus metrics, but definitelly its not limited to it.

In order to enable metrics, add Prometheus package:

```
PM> Install-Package StreamFlow.RabbitMq.Prometheus
```

And enable metrics when configuring RabbitMQ (see: WithPrometheusMetrics):

```
services.AddStreamFlow(streamFlowOptions, transport =>
{
    transport
        .UseRabbitMq(mq => mq
            .Connection(options.Host, options.Username, options.Password, options.VirtualHost)
            .WithPrometheusMetrics()
            ....
        );
});
```

### Prometheus metrics

- streamflow_messages_published
    
    histogram, represents published message counts and durations (seconds)

    labels: exchange, state [completed or failed]
    
- streamflow_bus_messages_published

    histogram, represents published message counts and durations (seconds) when using publisher host

    labels: exchange, state [completed or failed]

- streamflow_messages_publishing_events 
    
    histogram, represents various events happening during publishing process, like channel creation, preparations, serialization, transaction commit and so on, helps to see if there are any bottlenecks in publisher process

    labels: exchange, event

- streamflow_messages_publishing_errors

    counter, represents exceptions happened during message publishing

    labels: exchange

- streamflow_bus_messages_publishing_errors

    counter, represents exceptions happened during message publishing when publisher host is used

    labels: exchange

- streamflow_messages_consumed

    histogram, consumed message count and consumer durations (seconds)

    labels: exchange, queue, state

- streamflow_messages_consumed_errors

    counter, represents exceptions got during consumer process
    
    labels: exchange, queue

- streamflow_messages_bus_publishing

    counter, shows how many messages are published using publisher host

- streamflow_messages_bus_publishing_errors

    counter, represents publishing over publisher host errors, since publisher host channel is bounded - it will start increasing when publisher host is receiving more messasges than is able to actually send to RabbitMQ

-  streamflow_publisher_pool_size

    counter, when using publisher pooling, shows pool size (publishers created but currently not used)

- streamflow_publisher_pool_in_use

    counter, when using publisher pooling, shows how many publisher instances are currently in use

## Error Handling

Every application deals with errors. I am pretty sure - RabbitMQ handlers can get into multiple exceptional cases.
In case of unhandled exception IRabbitMqErrorHandler instance is created which by default will send a message to error queue.
Even though you can define your own error queue name by overriding IRabbitMqConventions interface, default behavior simply
add ":Error" suffix to the end of the consumer group queue and send message to that queue with exception details in message header.
Application developers or support can later decide what to do with those messages.

