using System.Threading;
using System.Threading.Tasks;
using StreamFlow.RabbitMq;
using Xunit;
using Xunit.Abstractions;

namespace StreamFlow.Tests
{
    public class RabbitMqConventionsShould
    {
        private readonly ITestOutputHelper _debug;

        public RabbitMqConventionsShould(ITestOutputHelper debug)
        {
            _debug = debug;
        }

        [Fact]
        public void GetQueueName()
        {
            var conventions = new RabbitMqConventions(new StreamFlowOptions());
            var queueName = conventions.GetQueueName(typeof(PingRequest), typeof(PingRequestConsumer), null);
            _debug.WriteLine(queueName);

            Assert.Equal("PingRequest:PingRequestConsumer", queueName);
        }

        [Fact]
        public void GetGenericConsumerQueueName()
        {
            var conventions = new RabbitMqConventions(new StreamFlowOptions());
            var queueName = conventions.GetQueueName(typeof(PingRequest), typeof(PingRequestGenericConsumer<Sample>), null);
            _debug.WriteLine(queueName);

            Assert.Equal("PingRequest:PingRequestGenericConsumer<Sample>", queueName);
        }

        [Fact]
        public void GetGenericConsumerQueueName2()
        {
            var conventions = new RabbitMqConventions(new StreamFlowOptions());
            var queueName = conventions.GetQueueName(typeof(PingRequest), typeof(PingRequestGenericConsumer<GenericSample<int>>), null);
            _debug.WriteLine(queueName);

            Assert.Equal("PingRequest:PingRequestGenericConsumer<GenericSample<Int32>>", queueName);
        }

        [Fact]
        public void GetGenericConsumerQueueNameWithGroup()
        {
            var conventions = new RabbitMqConventions(new StreamFlowOptions());
            var queueName = conventions.GetQueueName(typeof(PingRequest), typeof(PingRequestGenericConsumer<GenericSample<int>>), "group");
            _debug.WriteLine(queueName);

            Assert.Equal("PingRequest:PingRequestGenericConsumer<GenericSample<Int32>>:group", queueName);
        }

        [Fact]
        public void GetGenericConsumerQueueNameWithGroupAndServiceId()
        {
            var conventions = new RabbitMqConventions(new StreamFlowOptions {ServiceId = "service"});
            var queueName = conventions.GetQueueName(typeof(PingRequest), typeof(PingRequestGenericConsumer<GenericSample<int>>), "group");
            _debug.WriteLine(queueName);

            Assert.Equal("PingRequest:PingRequestGenericConsumer<GenericSample<Int32>>:service:group", queueName);
        }

        [Fact]
        public void GetGenericConsumerQueueNameWithServiceId()
        {
            var conventions = new RabbitMqConventions(new StreamFlowOptions {ServiceId = "service"});
            var queueName = conventions.GetQueueName(typeof(PingRequest), typeof(PingRequestGenericConsumer<GenericSample<int>>), null);
            _debug.WriteLine(queueName);

            Assert.Equal("PingRequest:PingRequestGenericConsumer<GenericSample<Int32>>:service", queueName);
        }

        private class PingRequest
        {
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class Sample
        {
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        // ReSharper disable once UnusedTypeParameter
        private class GenericSample<T>
        {
        }

        private class PingRequestConsumer : IConsumer<PingRequest>
        {
            public Task Handle(IMessage<PingRequest> message, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        // ReSharper disable once UnusedTypeParameter
        private class PingRequestGenericConsumer<T> : IConsumer<PingRequest>
        {
            public Task Handle(IMessage<PingRequest> message, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
