using System.Diagnostics;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

[Collection("GatewayNativeSourceMetadataAdmission")]
public sealed class GatewayNativeSourceMetadataAdmissionTests
{
    [Fact]
    public async Task SaturationRefusesNewProvidersUntilAbandonedWorkersActuallyCompleteAndNeverReleasesExpiredEvidence()
    {
        var identity = new AggregateIdentity("tenant-a", "conversations", "conversation-a");
        NativeSourceProof proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var page = new StreamReadPage(identity.TenantId, identity.Domain, identity.AggregateId, [],
            new(0, 0, null, 0, 0, false, null) { NativeProof = proof });
        TaskCompletionSource<StreamReadPage>[] providers = [.. Enumerable.Range(0, 16).Select(_ => new TaskCompletionSource<StreamReadPage>())];
        AuthoritativeReadTimeProvider[] clocks = [.. Enumerable.Range(0, 16).Select(_ => new AuthoritativeReadTimeProvider())];
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 16) allStarted.TrySetResult();
            return call <= 16 ? providers[call - 1].Task : Task.FromResult(page);
        });
        Task<NativeSourceObservation?>[] abandoned = [.. clocks.Select(clock =>
            new GatewayNativeSourceMetadataReader(gateway, clock).ReadAsync(identity, TestContext.Current.CancellationToken))];
        var fresh = new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System);
        try
        {
            await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            (await fresh.ReadAsync(identity, TestContext.Current.CancellationToken)).ShouldBeNull();
            Volatile.Read(ref calls).ShouldBe(16);

            foreach (AuthoritativeReadTimeProvider clock in clocks) clock.Advance(TimeSpan.FromSeconds(2));
            (await Task.WhenAll(abandoned).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
                .ShouldAllBe(observation => observation == null);
            (await fresh.ReadAsync(identity, TestContext.Current.CancellationToken)).ShouldBeNull();
            Volatile.Read(ref calls).ShouldBe(16);

            providers[0].SetResult(page);
            NativeSourceObservation? recovered = null;
            var recovery = Stopwatch.StartNew();
            while (recovered is null && recovery.Elapsed < TimeSpan.FromSeconds(5))
            {
                recovered = await fresh.ReadAsync(identity, TestContext.Current.CancellationToken);
                if (recovered is null) await Task.Yield();
            }
            recovered.ShouldNotBeNull().Proof.ShouldBe(proof);
            Volatile.Read(ref calls).ShouldBe(17);
            (await Task.WhenAll(abandoned)).ShouldAllBe(observation => observation == null);
            providers.Skip(1).ShouldAllBe(provider => !provider.Task.IsCompleted);
        }
        finally
        {
            foreach (TaskCompletionSource<StreamReadPage> provider in providers) provider.TrySetResult(page);
        }
    }
    /// <summary>Preserves actual provider ownership across deadline abandonment and slot recovery.</summary>
    [Fact]
    public async Task NativeScanKeepsAllSixteenAbandonedProviderSlotsUntilActualCompletion()
    {
        var identity = new AggregateIdentity("tenant-a", "conversations", "conversation-a");
        NativeSourceProof proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var page = new StreamReadPage(identity.TenantId, identity.Domain, identity.AggregateId, [],
            new(0, 0, null, 0, 0, false, null) { NativeProof = proof });
        TaskCompletionSource<StreamReadPage>[] providers = [.. Enumerable.Range(0, 16).Select(_ => new TaskCompletionSource<StreamReadPage>())];
        AuthoritativeReadTimeProvider[] clocks = [.. Enumerable.Range(0, 16).Select(_ => new AuthoritativeReadTimeProvider())];
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadNativeSourceScanPageAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 16) allStarted.TrySetResult();
            return call <= 16 ? providers[call - 1].Task : Task.FromResult(page);
        });
        Task<NativeSourceScanResult<string>>[] abandoned = [.. clocks.Select(clock =>
            new NativeSourceScanner(gateway, clock, TimeSpan.FromSeconds(2)).ScanAsync(identity, Fold, TestContext.Current.CancellationToken))];
        var fresh = new NativeSourceScanner(gateway, TimeProvider.System);
        try
        {
            await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            (await fresh.ScanAsync(identity, Fold, TestContext.Current.CancellationToken)).Observation.ShouldBeNull();
            Volatile.Read(ref calls).ShouldBe(16);

            foreach (AuthoritativeReadTimeProvider clock in clocks) clock.Advance(TimeSpan.FromSeconds(2));
            (await Task.WhenAll(abandoned).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
                .ShouldAllBe(observation => observation.Observation == null);
            (await fresh.ScanAsync(identity, Fold, TestContext.Current.CancellationToken)).Observation.ShouldBeNull();
            Volatile.Read(ref calls).ShouldBe(16);

            providers[0].SetResult(page);
            NativeSourceScanResult<string>? recovered = null;
            var recovery = Stopwatch.StartNew();
            while (recovered?.Observation is null && recovery.Elapsed < TimeSpan.FromSeconds(5))
            {
                recovered = await fresh.ScanAsync(identity, Fold, TestContext.Current.CancellationToken);
                if (recovered.Observation is null) await Task.Yield();
            }
            recovered.ShouldNotBeNull().Observation.ShouldNotBeNull().Proof.ShouldBe(proof);
            Volatile.Read(ref calls).ShouldBe(18);
            (await Task.WhenAll(abandoned)).ShouldAllBe(observation => observation.Observation == null);
            providers.Skip(1).ShouldAllBe(provider => !provider.Task.IsCompleted);
        }
        finally
        {
            foreach (TaskCompletionSource<StreamReadPage> provider in providers) provider.TrySetResult(page);
        }
    }
    /// <summary>Noncooperative selector callbacks retain every admission slot after caller/deadline abandonment until actual callback completion.</summary>
    [Theory]
    [InlineData("factory", false)]
    [InlineData("apply", false)]
    [InlineData("complete", false)]
    [InlineData("factory", true)]
    [InlineData("apply", true)]
    [InlineData("complete", true)]
    public async Task NativeSelectorAbandonmentRetainsActualCallbackOwnership(string stage, bool callerCanceled)
    {
        ThreadPool.GetMinThreads(out int minimum, out int ioMinimum); ThreadPool.SetMinThreads(Math.Max(minimum, 32), ioMinimum);
        var identity = new AggregateIdentity("tenant-a", "folders", "folder-a");
        var proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        proof = NativeSourceProofCodec.ExtendDigest(proof, identity, 1, "message-1", new string('a', 64));
        var source = new StreamReadEvent(1, "Example", [1], "json", 1, "message-1", null, null, DateTimeOffset.UnixEpoch, null) { NativeEventDigest = new string('a', 64) };
        var gateway = Substitute.For<IEventStoreGatewayClient>(); int providerCalls = 0;
        gateway.ReadNativeSourceScanPageAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Interlocked.Increment(ref providerCalls);
            var request = call.Arg<StreamReadRequest>(); StreamReadEvent[] events = request.ToSequence == 0 ? [] : [source];
            return Task.FromResult(new StreamReadPage(identity.TenantId, identity.Domain, identity.AggregateId, events,
                new(request.FromSequence, request.ToSequence, events.Length == 0 ? null : 1, 1, events.Length, false, null) { NativeProof = proof }));
        });
        var releases = Enumerable.Range(0, 16).Select(_ => new ManualResetEventSlim()).ToArray();
        var exited = Enumerable.Range(0, 16).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var entered = Enumerable.Range(0, 16).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray(); int completedCalls = 0;
        void Block(int ordinal)
        {
            entered[ordinal].TrySetResult();
            try { releases[ordinal].Wait(); } finally { exited[ordinal].TrySetResult(); }
        }
        var folds = Enumerable.Range(0, 16).Select(ordinal =>
        {
            var fold = Substitute.For<INativeSourceScanFold<string>>();
            fold.TryApply(Arg.Any<StreamReadEvent>()).Returns(_ => { if (stage == "apply") Block(ordinal); return true; });
            fold.Complete().Returns(_ => { Interlocked.Increment(ref completedCalls); if (stage == "complete") Block(ordinal); return "late-secret"; });
            return fold;
        }).ToArray();
        var callers = Enumerable.Range(0, 16).Select(_ => new CancellationTokenSource()).ToArray();
        var clocks = Enumerable.Range(0, 16).Select(_ => new AuthoritativeReadTimeProvider()).ToArray();
        Task<NativeSourceScanResult<string>> StartScan(int ordinal) => new NativeSourceScanner(gateway, clocks[ordinal], TimeSpan.FromSeconds(2)).ScanAsync(identity,
            () => { if (stage == "factory") Block(ordinal); return folds[ordinal]; }, callers[ordinal].Token);
        var pending = new List<Task<NativeSourceScanResult<string>>>(16);
        Task<NativeSourceScanResult<string>>? starting = null;
        Exception? originalFailure = null;
        var fresh = new NativeSourceScanner(gateway, TimeProvider.System);
        INativeSourceScanFold<string> Healthy()
        {
            var value = Fold(); value.TryApply(Arg.Any<StreamReadEvent>()).Returns(true); return value;
        }
        try
        {
            using var setup = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            setup.CancelAfter(TimeSpan.FromSeconds(10));
            for (int ordinal = 0; ordinal < 16; ordinal++)
            {
                while (true)
                {
                    setup.Token.ThrowIfCancellationRequested();
                    starting = StartScan(ordinal);
                    await Task.WhenAny(starting, entered[ordinal].Task).WaitAsync(setup.Token);
                    setup.Token.ThrowIfCancellationRequested();
                    if (entered[ordinal].Task.IsCompleted)
                    {
                        pending.Add(starting);
                        starting = null;
                        break;
                    }
                    NativeSourceScanResult<string> refused = await starting;
                    starting = null;
                    refused.Observation.ShouldBeNull(); refused.Value.ShouldBeNull();
                    refused.ReasonCode.ShouldBe("source-unavailable");
                    await Task.Yield();
                }
            }
            pending.Count.ShouldBe(16);
            entered.ShouldAllBe(signal => signal.Task.IsCompleted);
            int before = Volatile.Read(ref providerCalls);
            if (callerCanceled)
            {
                foreach (var caller in callers) await caller.CancelAsync();
                foreach (var task in pending) await Should.ThrowAsync<OperationCanceledException>(() => task);
            }
            else
            {
                foreach (var clock in clocks) clock.Advance(TimeSpan.FromSeconds(2));
                (await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
                    .ShouldAllBe(result => result.Observation == null && result.Value == null);
            }
            (await fresh.ScanAsync(identity, Healthy, TestContext.Current.CancellationToken)).Observation.ShouldBeNull();
            Volatile.Read(ref providerCalls).ShouldBe(before);
            releases[0].Set(); await exited[0].Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            NativeSourceScanResult<string>? recovered = null; var elapsed = Stopwatch.StartNew();
            while (recovered?.Observation is null && elapsed.Elapsed < TimeSpan.FromSeconds(5))
            { recovered = await fresh.ScanAsync(identity, Healthy, TestContext.Current.CancellationToken); if (recovered.Observation is null) await Task.Yield(); }
            recovered.ShouldNotBeNull().Observation.ShouldNotBeNull();
            exited.Skip(1).ShouldAllBe(value => !value.Task.IsCompleted);
            if (stage != "complete") Volatile.Read(ref completedCalls).ShouldBe(0);
            if (callerCanceled) pending.ShouldAllBe(task => task.IsCanceled);
            else pending.ShouldAllBe(task => task.Result.Observation == null && task.Result.Value == null);
        }
        catch (Exception exception)
        {
            originalFailure = exception;
            throw;
        }
        finally
        {
            Task<NativeSourceScanResult<string>>[] started = starting is null ? [.. pending] : [.. pending, starting];
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                Task cancellations;
                try { cancellations = Task.WhenAll(callers.Select(caller => caller.CancelAsync())); }
                finally { foreach (var release in releases) release.Set(); }
                await cancellations.WaitAsync(cleanup.Token);
                await Task.WhenAll(started.Select(async attempt =>
                {
                    try { await attempt; }
                    catch (OperationCanceledException) { }
                })).WaitAsync(cleanup.Token);
                await Task.WhenAll(exited.Where((_, ordinal) => entered[ordinal].Task.IsCompleted)
                    .Select(value => value.Task)).WaitAsync(cleanup.Token);
            }
            catch (Exception cleanupFailure) when (originalFailure is not null)
            {
                TestContext.Current.TestOutputHelper?.WriteLine("Native selector cleanup failed: " + cleanupFailure.GetType().Name);
            }
            finally
            {
                foreach (var caller in callers) caller.Dispose();
                for (int ordinal = 0; ordinal < releases.Length; ordinal++)
                    if (exited[ordinal].Task.IsCompleted) releases[ordinal].Dispose();
                ThreadPool.SetMinThreads(minimum, ioMinimum);
            }
        }
    }

    private static INativeSourceScanFold<string> Fold()
    {
        var fold = Substitute.For<INativeSourceScanFold<string>>();
        fold.Complete().Returns("selected");
        return fold;
    }
}
