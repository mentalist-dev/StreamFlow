using MediatR;

namespace StreamFlow.Tests.AspNetCore.Application.Ping;

public class PingNotificationConsumer : INotificationHandler<PingNotification>
{
    public Task Handle(PingNotification message, CancellationToken cancellationToken)
    {
        Console.WriteLine("Ping Notification: " + message.Timestamp + " " + message.Message);
        return Task.CompletedTask;
    }
}

public class PongResponseConsumer : INotificationHandler<PongResponse>
{
    public Task Handle(PongResponse notification, CancellationToken cancellationToken)
    {
        Console.WriteLine("Pong Response: " + notification.Timestamp + " " + notification.Message);
        return Task.CompletedTask;
    }
}
