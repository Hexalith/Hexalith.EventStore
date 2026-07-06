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
}
