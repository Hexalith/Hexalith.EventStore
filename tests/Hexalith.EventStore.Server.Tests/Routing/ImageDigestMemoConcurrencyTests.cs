using System.Reflection;
using System.Security.Cryptography;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Independent fresh observations share successful comparisons while publication, budgets and closure stay exclusive.</summary>
public sealed class ImageDigestMemoConcurrencyTests
{
    private const int MiB = 1024 * 1024;
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Warm fresh reads finish under independent shared protection, with separate scopes and complete original EOF observations.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void IndependentWarmReadersMakeProgressWithoutExclusiveAdmission(int readers)
    {
        byte[] bytes = Bytes();
        string expected = Digest(bytes);
        Assembly key = Key();
        using var memo = new RouteCatalogImageDigestMemo();
        memo.TryCompute(key, bytes, out _, out _).ShouldBeTrue();
        ReaderWriterLockSlim gate = Gate(memo);
        gate.EnterReadLock();
        try
        {
            string[] results = Concurrent(readers, _ =>
            {
                using var scope = RouteCatalogImageDigestScope.Enter(memo);
                using var image = new FragmentedExecutableImageStream(bytes, 1009, 0);
                var pool = new RecordingExecutableImagePool();
                string result = RouteCatalogFreshStreamImageDigest.Compute(key, image, pool);
                image.BytesRead.ShouldBe(bytes.Length);
                image.EofReads.ShouldBe(1);
                image.SeekCalls.ShouldBe(0);
                scope.CacheHits.ShouldBe(1);
                pool.Outstanding.ShouldBe(0);
                pool.Returns.ShouldBe(pool.Rents);
                return result;
            });
            results.ShouldAllBe(result => result == expected);
        }
        finally { gate.ExitReadLock(); }
        memo.Count.ShouldBe(1);
        memo.RetainedBytes.ShouldBe(bytes.Length + 512);
    }

    /// <summary>Two cold calls recheck the same key after exclusive entry and always hash privately owned complete bytes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColdSameKeyPublicationsRecheckCurrentBytes(bool differentBytes)
    {
        byte[] bytes = Bytes();
        byte[][] inputs = [bytes, differentBytes ? bytes.Select(value => (byte)(value ^ 1)).ToArray() : bytes];
        Assembly key = Key();
        using var memo = new RouteCatalogImageDigestMemo();
        (bool Reused, string Hash)[] results = Concurrent(2, index =>
        {
            using var scope = RouteCatalogImageDigestScope.Enter(memo);
            memo.TryCompute(key, inputs[index], out string hash, out bool reused).ShouldBeTrue();
            scope.CacheHits.ShouldBe(0);
            return (reused, hash);
        });
        for (int index = 0; index < results.Length; index++) results[index].Hash.ShouldBe(Digest(inputs[index]));
        if (!differentBytes) results.Count(result => result.Reused).ShouldBe(1);
        else results.ShouldAllBe(result => !result.Reused);
        memo.Count.ShouldBe(1);
        memo.RetainedBytes.ShouldBe(bytes.Length + 512);

        byte[] ownedInput = Bytes();
        string expected = Digest(ownedInput);
        Assembly ownershipKey = Key();
        memo.TryCompute(ownershipKey, ownedInput, out string digest, out bool reusedOwnership).ShouldBeTrue();
        reusedOwnership.ShouldBeFalse();
        digest.ShouldBe(expected);
        ownedInput.AsSpan().Fill(0xcc);
        memo.TryCompute(ownershipKey, Bytes(), out digest, out bool reusedAfterPoison).ShouldBeTrue();
        digest.ShouldBe(expected);
        reusedAfterPoison.ShouldBeTrue();
    }

    /// <summary>Different cold keys publish independently without losing an entry or its conservative byte charge.</summary>
    [Fact]
    public void ColdDistinctKeysBothPublishExactDigestsAndCharges()
    {
        byte[][] inputs = [Bytes(), [1, 3, 5, 7, 9]];
        Assembly[] keys = [Key(), Key()];
        using var memo = new RouteCatalogImageDigestMemo();
        string[] results = Concurrent(2, index =>
        {
            using var scope = RouteCatalogImageDigestScope.Enter(memo);
            memo.TryCompute(keys[index], inputs[index], out string digest, out bool reused).ShouldBeTrue();
            reused.ShouldBeFalse();
            return digest;
        });
        for (int index = 0; index < results.Length; index++) results[index].ShouldBe(Digest(inputs[index]));
        memo.Count.ShouldBe(2);
        memo.RetainedBytes.ShouldBe(inputs.Sum(bytes => bytes.Length + 512));
    }

    /// <summary>Competing new keys cannot both consume the last count slot or the last charged-byte allowance.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcurrentPublicationKeepsCountAndChargedByteBoundsAtomic(bool byteBudget)
    {
        using var memo = new RouteCatalogImageDigestMemo();
        if (byteBudget)
        {
            byte[] bytes = new byte[16 * MiB - 512];
            for (int index = 0; index < 4; index++)
                memo.TryCompute(Key(), bytes.AsSpan(0, bytes.Length - (index == 3 ? 513 : 0)), out _, out _).ShouldBeTrue();
            memo.RetainedBytes.ShouldBe(64 * MiB - 513);
        }
        else
        {
            for (int index = 0; index < 127; index++) memo.TryCompute(Key(), [1], out _, out _).ShouldBeTrue();
        }
        (bool Accepted, string Hash, bool Reused)[] results = Concurrent(2, _ =>
        {
            using var scope = RouteCatalogImageDigestScope.Enter(memo);
            bool accepted = memo.TryCompute(Key(), [2], out string digest, out bool reused);
            return (accepted, digest, reused);
        });
        results.Count(result => result.Accepted).ShouldBe(1);
        foreach (var result in results)
        {
            result.Reused.ShouldBeFalse();
            result.Hash.ShouldBe(result.Accepted ? Digest([2]) : string.Empty);
        }
        memo.Count.ShouldBe(byteBudget ? 5 : 128);
        memo.RetainedBytes.ShouldBe(byteBudget ? 64 * MiB : 128 * 513);
    }

    /// <summary>A replacement releases precisely its prior charge and rejected growth preserves the current retained input.</summary>
    [Fact]
    public void ReplacementRechecksExactChargeWithoutLosingThePriorDigest()
    {
        using var memo = new RouteCatalogImageDigestMemo();
        byte[] bytes = new byte[16 * MiB - 512];
        Assembly[] keys = [Key(), Key(), Key(), Key(), Key()];
        for (int index = 0; index < 4; index++) memo.TryCompute(keys[index], bytes, out _, out _).ShouldBeTrue();
        memo.RetainedBytes.ShouldBe(64 * MiB);
        memo.TryCompute(keys[4], [1], out _, out _).ShouldBeFalse();
        ReadOnlySpan<byte> smaller = bytes.AsSpan(0, bytes.Length - 513);
        memo.TryCompute(keys[0], smaller, out string expected, out bool reused).ShouldBeTrue();
        reused.ShouldBeFalse();
        expected.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(smaller)));
        memo.TryCompute(keys[4], [1], out _, out _).ShouldBeTrue();
        memo.TryCompute(keys[0], bytes.AsSpan(0, bytes.Length - 512), out string rejected, out reused).ShouldBeFalse();
        rejected.ShouldBeEmpty();
        reused.ShouldBeFalse();
        memo.TryCompute(keys[0], smaller, out string retained, out reused).ShouldBeTrue();
        retained.ShouldBe(expected);
        reused.ShouldBeTrue();
        memo.Count.ShouldBe(5);
        memo.RetainedBytes.ShouldBe(64 * MiB);
    }

    /// <summary>Logical closure waits for active readers, then permits late getters, repeated disposal and complete uncached stream hashing.</summary>
    [Fact]
    public async Task DisposalWaitsForReadersAndLeavesClosedFallbackUsableAsync()
    {
        byte[] bytes = Bytes();
        Assembly key = Key();
        using var memo = new RouteCatalogImageDigestMemo();
        memo.TryCompute(key, bytes, out _, out _).ShouldBeTrue();
        ReaderWriterLockSlim gate = Gate(memo);
        Task? closing = null;
        gate.EnterReadLock();
        try
        {
            closing = Dedicated(memo.Dispose);
            SpinWait.SpinUntil(() => gate.WaitingWriteCount > 0, CompletionTimeout).ShouldBeTrue();
            closing.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            gate.ExitReadLock();
            if (closing is not null) await closing.WaitAsync(CompletionTimeout);
        }
        AssertClosed(memo, key, bytes);
        memo.Dispose();
        AssertClosed(memo, key, bytes);
        using var scope = RouteCatalogImageDigestScope.Enter(memo);
        using var image = new FragmentedExecutableImageStream(bytes, 127, 0);
        var pool = new RecordingExecutableImagePool();
        RouteCatalogFreshStreamImageDigest.Compute(key, image, pool).ShouldBe(Digest(bytes));
        image.BytesRead.ShouldBe(bytes.Length);
        image.EofReads.ShouldBe(1);
        image.SeekCalls.ShouldBe(0);
        pool.Rents.ShouldBe(0);
        scope.CacheHits.ShouldBe(0);
        AssertClosed(memo, key, bytes);
    }

    /// <summary>A miss queued behind active protection and a racing close obey either legal order without publishing after closure.</summary>
    [Fact]
    public async Task PendingColdWriterAndDisposalRecheckTheClosedStateAsync()
    {
        byte[] bytes = Bytes();
        for (int iteration = 0; iteration < 8; iteration++)
        {
            using var memo = new RouteCatalogImageDigestMemo();
            Assembly key = Key();
            ReaderWriterLockSlim gate = Gate(memo);
            Task<(bool Accepted, string Digest, bool Reused)>? publication = null;
            Task? closing = null;
            gate.EnterReadLock();
            try
            {
                publication = Dedicated(() =>
                {
                    using var scope = RouteCatalogImageDigestScope.Enter(memo);
                    bool accepted = memo.TryCompute(key, bytes, out string digest, out bool reused);
                    return (accepted, digest, reused);
                });
                SpinWait.SpinUntil(() => gate.WaitingWriteCount == 1, CompletionTimeout).ShouldBeTrue();
                closing = Dedicated(memo.Dispose);
                SpinWait.SpinUntil(() => gate.WaitingWriteCount == 2, CompletionTimeout).ShouldBeTrue();
                publication.IsCompleted.ShouldBeFalse();
                closing.IsCompleted.ShouldBeFalse();
            }
            finally
            {
                gate.ExitReadLock();
                await Task.WhenAll(new Task?[] { publication, closing }.OfType<Task>()).WaitAsync(CompletionTimeout);
            }
            var result = await publication!;
            result.Reused.ShouldBeFalse();
            result.Digest.ShouldBe(result.Accepted ? Digest(bytes) : string.Empty);
            AssertClosed(memo, key, bytes);
        }
    }

    private static T[] Concurrent<T>(int readers, Func<int, T> action)
    {
        using var ready = new CountdownEvent(readers);
        using var start = new ManualResetEventSlim();
        Task<T>[] work = Enumerable.Range(0, readers).Select(index => Dedicated(() =>
        {
            ready.Signal();
            start.Wait();
            return action(index);
        })).ToArray();
        bool allReady = ready.Wait(CompletionTimeout);
        start.Set();
        T[] result = Task.WhenAll(work).WaitAsync(CompletionTimeout).GetAwaiter().GetResult();
        allReady.ShouldBeTrue();
        return result;
    }

    private static Task<T> Dedicated<T>(Func<T> action)
        => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static Task Dedicated(Action action)
        => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static ReaderWriterLockSlim Gate(RouteCatalogImageDigestMemo memo)
        => (ReaderWriterLockSlim)typeof(RouteCatalogImageDigestMemo).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(memo)!;

    private static Assembly Key() => new ExecutableImageAssembly(string.Empty);

    private static byte[] Bytes() => Enumerable.Range(0, 100003).Select(index => (byte)(index * 31)).ToArray();

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void AssertClosed(RouteCatalogImageDigestMemo memo, Assembly key, byte[] bytes)
    {
        memo.IsClosed.ShouldBeTrue();
        memo.Count.ShouldBe(0);
        memo.RetainedBytes.ShouldBe(0);
        memo.TryCompute(key, bytes, out string digest, out bool reused).ShouldBeFalse();
        digest.ShouldBeEmpty();
        reused.ShouldBeFalse();
    }
}
