using StreamFlow.Tests.AspNetCore.Application.Ping;

namespace StreamFlow.Tests.AspNetCore.Application.Services
{
    public class HighLoadPublisherHostedService: IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IHostApplicationLifetime _lifetime;

        public HighLoadPublisherHostedService(IServiceProvider services, IHostApplicationLifetime lifetime)
        {
            _services = services;
            _lifetime = lifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Task.Factory.StartNew(HighLoadPublisher, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void HighLoadPublisher()
        {
            var taskList = new List<Task>();
            while (!_lifetime.ApplicationStopping.IsCancellationRequested)
            {
                var task = DoPublish();
                taskList.Add(task);

                if (taskList.Count >= 100)
                {
                    Task.WaitAll(taskList.ToArray());
                    taskList.Clear();
                }
            }
        }

        private async Task DoPublish()
        {
            await using var scope = _services.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.PublishAsync(new PingMessage()).ConfigureAwait(false);
        }
    }
}
