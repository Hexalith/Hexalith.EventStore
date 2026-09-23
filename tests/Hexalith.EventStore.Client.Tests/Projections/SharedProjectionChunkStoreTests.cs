using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionChunkStoreTests
{
    [Fact]
    public async Task CorruptPersistedChunkFailsClosedBeforePayloadUse()
    {
        var scope = new SharedProjectionScope("statestore", "tenant-a", "widget", "index", ["writer"]);
        var store = new InMemoryReadModelStore();
        var chunks = new SharedProjectionChunkStore(store);
        byte[] bytes = new byte[50_000];
        Random.Shared.NextBytes(bytes);
        SharedProjectionChunkReference reference = SharedProjectionChunkStore.Describe("delivery", "event-1", bytes);
        await chunks.WriteAsync(scope, reference, bytes, CancellationToken.None);
        store.SeedRaw(
            scope.StoreName,
            SharedProjectionChunkStore.Key(scope, reference, 1),
            new SharedProjectionChunk(reference.Digest, "BAD", [1], false));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => chunks.ReadAsync(scope, reference, CancellationToken.None));
        await chunks.TombstoneAsync(scope, reference, CancellationToken.None);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => chunks.WriteAsync(scope, reference, bytes, CancellationToken.None));
    }
}
