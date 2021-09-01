using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Pipes;
using Xunit;
using Xunit.Abstractions;

namespace StreamFlow.Tests
{
    public class StreamFlowMiddlewareShould
    {
        private readonly ITestOutputHelper _debug;

        public StreamFlowMiddlewareShould(ITestOutputHelper debug)
        {
            _debug = debug;
        }

        [Fact]
        public void ExecuteInOrder()
        {
            var output = new Output();

            var services = new ServiceCollection();
            services.AddSingleton(output);

            services.AddStreamFlow(transport => transport.ConfigureConsumerPipe(builder =>
            {
                builder.Use(async (context, next) =>
                {
                    output.Add("1");
                    await next(context);
                    output.Add("4");
                });

                builder.Use(async (context, next) =>
                {
                    output.Add("2");
                    await next(context);
                    output.Add("3");
                });

                builder.Use<MyMiddleware1>();
                builder.Use(_ => new MyMiddleware2(output));
            }));

            var provider = services.BuildServiceProvider();
            var executor = provider.GetRequiredService<IStreamFlowConsumerPipe>();

            executor.ExecuteAsync(provider, null!, _ =>
            {
                output.Add("The world is mine!");
                return Task.CompletedTask;
            });

            var message = string.Empty;
            foreach (var line in output.Lines)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    message += " ";
                }

                message += line;
                _debug.WriteLine(line);
            }

            var expected =  "1 2 Hello from middleware1 Hello from middleware2 The world is mine! Bye bye from middleware2 Bye bye from middleware1 3 4";
            Assert.Equal(expected, message);
        }

        private class Output
        {
            public List<string> Lines { get; } = new();

            public void Add(string message)
            {
                Lines.Add(message);
            }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class MyMiddleware1 : IStreamFlowMiddleware
        {
            private readonly Output _debug;

            public MyMiddleware1(Output debug)
            {
                _debug = debug;
            }

            public async Task Invoke(IMessageContext context, Func<IMessageContext, Task> next)
            {
                _debug.Add("Hello from middleware1");
                await next(context);
                _debug.Add("Bye bye from middleware1");
            }
        }

        private class MyMiddleware2 : IStreamFlowMiddleware
        {
            private readonly Output _debug;

            public MyMiddleware2(Output debug)
            {
                _debug = debug;
            }

            public async Task Invoke(IMessageContext context, Func<IMessageContext, Task> next)
            {
                _debug.Add("Hello from middleware2");
                await next(context);
                _debug.Add("Bye bye from middleware2");
            }
        }
    }
}
