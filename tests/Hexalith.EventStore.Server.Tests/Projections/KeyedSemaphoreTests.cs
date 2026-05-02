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
        var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task secondTask = Task.Run(async () => {
            await firstReady.Task;
            using IDisposable second = await keyed.AcquireAsync("a", CancellationToken.None);
            firstReleased.Task.IsCompleted.ShouldBeTrue();
        });

        IDisposable first = await keyed.AcquireAsync("a", CancellationToken.None);
        firstReady.SetResult();
        await Task.Delay(50);
        keyed.Count.ShouldBe(1);
        first.Dispose();
        firstReleased.SetResult();

        await secondTask;
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
