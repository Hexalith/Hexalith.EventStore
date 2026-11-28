using Hexalith.EventStore.Client.Gateway;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public sealed class FakeEventStoreGatewayClientTests
{
    [Fact]
    public async Task StatusReadOnFakeHonorsCallerCancellation()
    {
        IEventStoreGatewayClient gateway = new FakeEventStoreGatewayClient();
        var cancellation = new CancellationToken(canceled: true);

        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => gateway.GetCommandStatusAsync("message-1", cancellation));

        Assert.Equal(cancellation, error.CancellationToken);
    }
}
