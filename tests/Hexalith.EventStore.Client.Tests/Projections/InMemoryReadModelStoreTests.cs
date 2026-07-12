using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public class InMemoryReadModelStoreTests {
    private const string StoreName = "statestore";

    public sealed class Model {
        public int Value { get; set; }
    }

    [Fact]
    public async Task GetAsync_AbsentKey_ReturnsNullValueAndETag() {
        var store = new InMemoryReadModelStore();

        ReadModelEntry<Model> entry = await store.GetAsync<Model>(StoreName, "missing");

        entry.Value.ShouldBeNull();
        entry.ETag.ShouldBeNull();
    }

    [Fact]
    public async Task TrySaveAsync_CreateWithEmptyEtag_Succeeds() {
        var store = new InMemoryReadModelStore();

        bool saved = await store.TrySaveAsync(StoreName, "k", new Model { Value = 1 }, etag: string.Empty);

        saved.ShouldBeTrue();
        store.Snapshot<Model>(StoreName, "k")!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task TrySaveAsync_StaleEtag_Fails() {
        var store = new InMemoryReadModelStore();
        _ = await store.TrySaveAsync(StoreName, "k", new Model { Value = 1 }, etag: string.Empty);

        // A second create attempt (empty ETag against a now-present key) must conflict.
        bool saved = await store.TrySaveAsync(StoreName, "k", new Model { Value = 2 }, etag: string.Empty);

        saved.ShouldBeFalse();
        store.Snapshot<Model>(StoreName, "k")!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task TrySaveAsync_MatchingEtag_Succeeds() {
        var store = new InMemoryReadModelStore();
        _ = await store.TrySaveAsync(StoreName, "k", new Model { Value = 1 }, etag: string.Empty);
        ReadModelEntry<Model> entry = await store.GetAsync<Model>(StoreName, "k");

        bool saved = await store.TrySaveAsync(StoreName, "k", new Model { Value = 2 }, entry.ETag!);

        saved.ShouldBeTrue();
        store.Snapshot<Model>(StoreName, "k")!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task TrySaveAsync_SuccessfulUpdate_AdvancesETag() {
        var store = new InMemoryReadModelStore();
        _ = await store.TrySaveAsync(StoreName, "k", new Model { Value = 1 }, etag: string.Empty);
        ReadModelEntry<Model> first = await store.GetAsync<Model>(StoreName, "k");

        bool saved = await store.TrySaveAsync(StoreName, "k", new Model { Value = 2 }, first.ETag!);
        ReadModelEntry<Model> second = await store.GetAsync<Model>(StoreName, "k");

        saved.ShouldBeTrue();
        first.ETag.ShouldNotBeNull();
        second.ETag.ShouldNotBeNull();
        second.ETag.ShouldNotBe(first.ETag);
    }

    [Fact]
    public async Task TrySaveAsync_ConcurrentWriteInjection_FailsHeldETagAndPreservesConcurrentValue() {
        var store = new InMemoryReadModelStore();
        store.ConcurrentWriteBeforeTrySave = () => {
            store.SeedRaw(StoreName, "k", new Model { Value = 7 });
            store.ConcurrentWriteBeforeTrySave = null;
        };

        bool saved = await store.TrySaveAsync(StoreName, "k", new Model { Value = 1 }, etag: string.Empty);

        saved.ShouldBeFalse();
        store.Snapshot<Model>(StoreName, "k")!.Value.ShouldBe(7);
    }

    [Fact]
    public async Task GetAsync_ReturnsClone_NotSharedReference() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(StoreName, "k", new Model { Value = 1 });

        Model first = (await store.GetAsync<Model>(StoreName, "k")).Value!;
        first.Value = 999;
        Model second = (await store.GetAsync<Model>(StoreName, "k")).Value!;

        second.Value.ShouldBe(1);
    }

    [Fact]
    public async Task TryEraseAsync_MatchingEtag_RemovesValue() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(StoreName, "k", new Model { Value = 1 });
        ReadModelEntry<Model> entry = await store.GetAsync<Model>(StoreName, "k");

        bool erased = await store.TryEraseAsync(StoreName, "k", entry.ETag!);

        erased.ShouldBeTrue();
        store.Snapshot<Model>(StoreName, "k").ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("stale-etag")]
    public async Task TryEraseAsync_AbsentKey_IsIdempotentRegardlessOfEtag(string etag) {
        var store = new InMemoryReadModelStore();

        bool erased = await store.TryEraseAsync(StoreName, "missing", etag);

        erased.ShouldBeTrue();
        store.Count.ShouldBe(0);
    }

    [Fact]
    public async Task TryEraseAsync_StaleEtag_ReturnsConflictAndPreservesValue() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(StoreName, "k", new Model { Value = 1 });

        bool erased = await store.TryEraseAsync(StoreName, "k", "stale-etag");

        erased.ShouldBeFalse();
        store.Snapshot<Model>(StoreName, "k")!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task TryEraseAsync_ConcurrentWriteInjection_ReturnsConflictAndPreservesConcurrentValue() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(StoreName, "k", new Model { Value = 1 });
        ReadModelEntry<Model> entry = await store.GetAsync<Model>(StoreName, "k");
        store.ConcurrentWriteBeforeTryErase = () => {
            store.SeedRaw(StoreName, "k", new Model { Value = 7 });
            store.ConcurrentWriteBeforeTryErase = null;
        };

        bool erased = await store.TryEraseAsync(StoreName, "k", entry.ETag!);

        erased.ShouldBeFalse();
        store.Snapshot<Model>(StoreName, "k")!.Value.ShouldBe(7);
    }

    [Fact]
    public async Task TryEraseAsync_Cancelled_ThrowsOperationCanceledException() {
        var store = new InMemoryReadModelStore();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => store.TryEraseAsync(StoreName, "k", string.Empty, source.Token));
    }

    [Fact]
    public async Task TryReadEtagAsync_PresentValue_ReturnsPresentTrueWithEtagUsableForErase() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(StoreName, "k", new Model { Value = 1 });
        ReadModelEntry<Model> entry = await store.GetAsync<Model>(StoreName, "k");

        (bool present, string etag) = await store.TryReadEtagAsync(StoreName, "k");

        present.ShouldBeTrue();
        etag.ShouldBe(entry.ETag);

        // Parity with DAPR: the read ETag drives a first-write-wins erase to completion.
        bool erased = await store.TryEraseAsync(StoreName, "k", etag);
        erased.ShouldBeTrue();
        store.Snapshot<Model>(StoreName, "k").ShouldBeNull();
    }

    [Fact]
    public async Task TryReadEtagAsync_AbsentValue_ReturnsPresentFalseWithEmptyEtag() {
        var store = new InMemoryReadModelStore();

        (bool present, string etag) = await store.TryReadEtagAsync(StoreName, "missing");

        present.ShouldBeFalse();
        etag.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task TryReadEtagAsync_Cancelled_ThrowsOperationCanceledException() {
        var store = new InMemoryReadModelStore();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => store.TryReadEtagAsync(StoreName, "k", source.Token));
    }
}
