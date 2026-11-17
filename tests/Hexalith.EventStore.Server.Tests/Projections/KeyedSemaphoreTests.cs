using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class KeyedSemaphoreTests {
    [Fact]
    public async Task AcquireAsync_SameKeySerial_ReleasesEntryWhenLastHolderDisposes() {
        var keyed = new KeyedSemaphore<string>();

        IDisposable first = await keyed.AcquireAsync("a", CancellationToken.None);
        keyed.Count.ShouldBe(1);
        first.Dispose();

        keyed.Count.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_NDistinctKeys_DictionaryReturnsToBaseline() {
        // Pumping N short-lived aggregates must not leave SemaphoreSlim entries behind.
        var keyed = new KeyedSemaphore<string>();
        const int distinctKeys = 1000;

        for (int i = 0; i < distinctKeys; i++) {
            using IDisposable holder = await keyed.AcquireAsync($"agg-{i}", CancellationToken.None);
        }

        keyed.Count.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_SameKeyConcurrentWaiters_SerializesAndReleasesAtBaseline() {
        var keyed = new KeyedSemaphore<string>();
        using IDisposable first = await keyed.AcquireAsync("a", CancellationToken.None);
        Task<IDisposable> secondTask = keyed.AcquireAsync("a", CancellationToken.None);

        // The second waiter cannot acquire the same key while the first lease is held.
        secondTask.IsCompleted.ShouldBeFalse();
        keyed.Count.ShouldBe(1);
        first.Dispose();

        using (IDisposable second = await secondTask) {
            keyed.Count.ShouldBe(1);
        }
        keyed.Count.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_CancellationDuringWait_DoesNotLeakHolderAfterReleaseFromWinner() {
        var keyed = new KeyedSemaphore<string>();
        IDisposable winner = await keyed.AcquireAsync("a", CancellationToken.None);

        using var cts = new CancellationTokenSource();
        Task<IDisposable> loserTask = keyed.AcquireAsync("a", cts.Token);
        cts.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => loserTask);

        // Winner releasing must drain the dictionary even though a waiter previously cancelled.
        winner.Dispose();
        keyed.Count.ShouldBe(0);
    }
}
