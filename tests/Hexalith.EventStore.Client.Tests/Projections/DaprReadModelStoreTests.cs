using Dapr.Client;

using Hexalith.EventStore.Client.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public class DaprReadModelStoreTests {
    private const string StoreName = "statestore";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrySaveAsync_UsesFirstWriteConcurrencyPassesETagAndReturnsDaprResult(bool daprResult) {
        var daprClient = new RecordingDaprClient { TrySaveResult = daprResult };
        var store = new DaprReadModelStore(daprClient);
        var value = new DaprReadModelStoreTestModel { Value = 42 };

        bool saved = await store.TrySaveAsync(StoreName, "read-model:1", value, "etag-1");

        saved.ShouldBe(daprResult);
        daprClient.StoreName.ShouldBe(StoreName);
        daprClient.Key.ShouldBe("read-model:1");
        daprClient.Value.ShouldBe(value);
        daprClient.ETag.ShouldBe("etag-1");
        StateOptions options = daprClient.StateOptions.ShouldNotBeNull();
        options.Concurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryEraseAsync_UsesFirstWriteConcurrencyPassesETagAndReturnsDaprResult(bool daprResult) {
        var daprClient = new RecordingDaprClient { TryDeleteResult = daprResult };
        var store = new DaprReadModelStore(daprClient);

        bool erased = await store.TryEraseAsync(StoreName, "read-model:1", "etag-1");

        erased.ShouldBe(daprResult);
        daprClient.StoreName.ShouldBe(StoreName);
        daprClient.Key.ShouldBe("read-model:1");
        daprClient.ETag.ShouldBe("etag-1");
        StateOptions options = daprClient.StateOptions.ShouldNotBeNull();
        options.Concurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }

    [Fact]
    public async Task TryEraseAsync_Cancelled_ThrowsOperationCanceledException() {
        var daprClient = new RecordingDaprClient { TryDeleteResult = true };
        var store = new DaprReadModelStore(daprClient);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => store.TryEraseAsync(StoreName, "read-model:1", "etag-1", source.Token));
    }
}
