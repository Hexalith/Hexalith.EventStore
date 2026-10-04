using Hexalith.EventStore.Server.DomainServices;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed class BoundedDomainServiceResponseStreamTests
{
    [Fact]
    public async Task WholeReceivedCapCountsUnknownBytesAndStopsOnFirstExcessByte()
    {
        using var input = new MemoryStream(new byte[11]);
        using var bounded = new BoundedDomainServiceResponseStream(input, 10, CancellationToken.None);
        byte[] bytes = new byte[10];
        (await bounded.ReadAsync(bytes)).ShouldBe(10);
        (await Should.ThrowAsync<InvalidOperationException>(async () => (await bounded.ReadAsync(bytes)).ShouldBe(0))).Message.ShouldContain("ResultLimit");
        Should.Throw<ObjectDisposedException>(() => bounded.Read(bytes));
    }

    [Fact]
    public async Task ReadWindowNeverExceeds64KiBAndExactLimitIsAllowed()
    {
        using var input = new MemoryStream(new byte[65_536]);
        using var bounded = new BoundedDomainServiceResponseStream(input, 65_536, CancellationToken.None);
        (await bounded.ReadAsync(new byte[128 * 1024])).ShouldBe(65_536);
        (await bounded.ReadAsync(new byte[1])).ShouldBe(0);
    }

    [Fact]
    public async Task OriginatingCancellationCannotBeReplacedWithNone()
    {
        using var cancellation = new CancellationTokenSource();
        using var bounded = new BoundedDomainServiceResponseStream(new MemoryStream(new byte[1]), 1, cancellation.Token);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => (await bounded.ReadAsync(new byte[1], CancellationToken.None)).ShouldBe(0));
    }
}
