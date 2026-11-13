using Hexalith.EventStore.Contracts.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks separately bounded exact-key computations through independent closing scopes.</summary>
public sealed class RouteCatalogCanonicalHashMemoTests
{
    /// <summary>Full ordinal keys, entry limits and charged bytes remain independent of the existing byte memo.</summary>
    [Fact]
    public void CompleteKeysAndIndependentBoundsCannotAliasOrOvercharge()
    {
        using var memo = new RouteCatalogCanonicalHashMemo();
        string digest = new('0', 64);
        memo.Remember("Key", digest);
        memo.TryGet("key", out _).ShouldBeFalse();
        memo.TryGet("Key", out string found).ShouldBeTrue();
        found.ShouldBe(digest);
        for (int index = 1; index <= 512; index++) memo.Remember("key-" + index, digest);
        memo.Count.ShouldBe(512);
        memo.RetainedCharge.ShouldBeLessThanOrEqualTo(RouteCatalogCanonicalHashMemo.MaximumRetainedCharge);
        using var charged = new RouteCatalogCanonicalHashMemo();
        for (int index = 0; index < 512; index++) charged.Remember(new string('x', 8192) + index, digest);
        charged.Count.ShouldBeLessThan(512);
        charged.RetainedCharge.ShouldBeLessThanOrEqualTo(RouteCatalogCanonicalHashMemo.MaximumRetainedCharge);
    }

    /// <summary>Inputs too large for reuse still take the original successful computation without retention.</summary>
    [Fact]
    public void OversizedPreimageFallsBackWithoutChangingItsDigest()
    {
        var value = new { purpose = "oversize", value = new string('x', 2 * 1024 * 1024) };
        string expected = CanonicalHashTestInputs.Uncached(() => CanonicalHashTestInputs.Hash(value));
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        CanonicalHashTestInputs.Hash(value).ShouldBe(expected);
        hashes.Count.ShouldBe(0);
        hashes.RetainedCharge.ShouldBe(0);
        bytes.Count.ShouldBe(0);
    }

    /// <summary>Nested, suppressed and legacy scopes cannot implicitly borrow another owner's pure computations.</summary>
    [Fact]
    public void ScopeAndOwnerSelectionRemainExplicit()
    {
        using var boundary = RouteCatalogDecodeScope.Suppress();
        var value = new { purpose = "scope", schemaVersion = 1 };
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        using var outer = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        CanonicalHashTestInputs.Hash(value);
        hashes.Count.ShouldBe(1);
        using (RouteCatalogDecodeScope.Enter()) CanonicalHashTestInputs.Hash(value);
        using (RouteCatalogDecodeScope.EnterWithMemo(bytes)) CanonicalHashTestInputs.Hash(value);
        using (RouteCatalogDecodeScope.Suppress()) CanonicalHashTestInputs.Hash(value);
        hashes.Hits.ShouldBe(0);
        using var foreignBytes = new RouteCatalogValidatedBytesMemo();
        using var foreign = new RouteCatalogCanonicalHashMemo();
        using (RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(foreignBytes, foreign)) CanonicalHashTestInputs.Hash(value);
        foreign.Count.ShouldBe(1);
        hashes.Hits.ShouldBe(0);
        RouteCatalogDecodeScope.Current.ShouldBeSameAs(outer);
        CanonicalHashTestInputs.Hash(value);
        hashes.Hits.ShouldBe(1);
        outer.Dispose();
        hashes.Count.ShouldBe(1);
        CanonicalHashTestInputs.Hash(value);
        hashes.Hits.ShouldBe(1);
        hashes.Dispose();
        hashes.Count.ShouldBe(0);
        hashes.RetainedCharge.ShouldBe(0);
        using var after = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        after.HasCanonicalHashMemo.ShouldBeFalse();
        CanonicalHashTestInputs.Hash(value);
        hashes.Count.ShouldBe(0);
    }

    /// <summary>An escaped asynchronous scope reference cannot publish after its owning observation closes.</summary>
    [Fact]
    public async Task EscapedClosedScopeCannotUseOrPopulateOpenOwner()
    {
        using var boundary = RouteCatalogDecodeScope.Suppress();
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        var value = new { purpose = "escaped", schemaVersion = 1 };
        string expected = CanonicalHashTestInputs.Hash(value);
        var scope = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task escaped = Task.Run(async () =>
        {
            await go.Task;
            RouteCatalogDecodeScope.Current.ShouldBeSameAs(scope);
            scope.HasCanonicalHashMemo.ShouldBeFalse();
            CanonicalHashTestInputs.Hash(value).ShouldBe(expected);
        });
        scope.Dispose();
        go.SetResult();
        await escaped;
        hashes.Count.ShouldBe(0);
        hashes.Hits.ShouldBe(0);
    }

    /// <summary>Concurrent guaranteed misses publish the complete same key only once with one retained charge.</summary>
    [Fact]
    public async Task ConcurrentMissesPublishOnceWithinIndependentBounds()
    {
        using var hashes = new RouteCatalogCanonicalHashMemo();
        using var rendezvous = new Barrier(8);
        const string key = "synthetic-complete-serialized-preimage";
        string digest = new('0', 64);
        Task[] tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            hashes.TryGet(key, out _).ShouldBeFalse();
            rendezvous.SignalAndWait();
            hashes.Remember(key, digest);
        })).ToArray();
        await Task.WhenAll(tasks);
        hashes.Count.ShouldBe(1);
        hashes.Misses.ShouldBe(8);
        hashes.RetainedCharge.ShouldBe(128 + 2 * (key.Length + digest.Length));
    }

    /// <summary>Closing an owner with eight actual live hashing scopes permanently prevents later reuse or publication.</summary>
    [Fact]
    public async Task OwnerDisposalWhileScopesRemainLiveKeepsOriginalFallback()
    {
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        var value = new { purpose = "concurrent", schemaVersion = 1 };
        string expected = CanonicalHashTestInputs.Uncached(() => CanonicalHashTestInputs.Hash(value));
        var allComputed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var afterClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int completed = 0;
        Task[] tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(async () =>
        {
            using var scope = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
            CanonicalHashTestInputs.Hash(value).ShouldBe(expected);
            if (Interlocked.Increment(ref completed) == 8) allComputed.SetResult();
            await afterClose.Task;
            scope.HasCanonicalHashMemo.ShouldBeFalse();
            CanonicalHashTestInputs.Hash(value).ShouldBe(expected);
        })).ToArray();
        try
        {
            await allComputed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            hashes.Count.ShouldBe(1);
            hashes.Dispose();
        }
        finally
        {
            afterClose.TrySetResult();
            await Task.WhenAll(tasks);
        }
        hashes.IsClosed.ShouldBeTrue();
        hashes.Count.ShouldBe(0);
        hashes.RetainedCharge.ShouldBe(0);
    }
}
