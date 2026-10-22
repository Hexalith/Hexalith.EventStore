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
}
