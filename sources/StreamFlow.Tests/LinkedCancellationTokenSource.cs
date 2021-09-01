using System.Threading;
using Xunit;

namespace StreamFlow.Tests
{
    public class LinkedCancellationTokenSource
    {
        [Fact]
        public void ShouldCancel()
        {
            var source1 = new CancellationTokenSource();
            var source2 = new CancellationTokenSource();

            var linked = CancellationTokenSource.CreateLinkedTokenSource(source1.Token, source2.Token);
            var token = linked.Token;

            Assert.False(token.IsCancellationRequested);
            Assert.False(source1.IsCancellationRequested);
            Assert.False(source2.IsCancellationRequested);

            linked.Cancel();

            Assert.True(token.IsCancellationRequested);
            Assert.False(source1.IsCancellationRequested);
            Assert.False(source2.IsCancellationRequested);
        }

        [Fact]
        public void ShouldCancelOnDispose()
        {
            var source1 = new CancellationTokenSource();
            var source2 = new CancellationTokenSource();

            var linked = CancellationTokenSource.CreateLinkedTokenSource(source1.Token, source2.Token);
            var token = linked.Token;

            Assert.False(token.IsCancellationRequested);
            Assert.False(source1.IsCancellationRequested);
            Assert.False(source2.IsCancellationRequested);

            linked.Dispose();

            Assert.False(token.IsCancellationRequested);
            Assert.False(source1.IsCancellationRequested);
            Assert.False(source2.IsCancellationRequested);
        }

        [Fact]
        public void ShouldCancelSingle()
        {
            var source1 = new CancellationTokenSource();

            var linked = CancellationTokenSource.CreateLinkedTokenSource(source1.Token);
            var token = linked.Token;

            Assert.False(token.IsCancellationRequested);
            Assert.False(source1.IsCancellationRequested);

            linked.Cancel();

            Assert.True(token.IsCancellationRequested);
            Assert.False(source1.IsCancellationRequested);
        }
    }
}
