// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 13; amendment sections 2-3; pass-3 deployment regressions.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Historical clearing proof avoids capability scans and definite conflicts retain their constructive meaning.</summary>
public sealed class ReviewDeploymentCorrectionsTests
{
    [Fact]
    public async Task SuccessorSkipsCapabilityReadsFor4096PermanentClearedIncarnations()
    {
        var f = await ReservationTestFixture.CreateAsync();
        const int count = 4096;
        string now = PayloadProtectionStateCodec.Timestamp(f.Clock.GetUtcNow());
        var catalogs = new PayloadProtectionCatalogs(f.Store, f.Clock);
        var existing = await catalogs.ReadMembersAsync(f.Root);
        var mutations = new List<PayloadProtectionStateMutation>
        {
            PayloadProtectionStateMutation.Put(new MemberHeadV1(f.Root, existing.Head.Value.Revision + 1, count / 256, count, now), existing.Head.ETag),
        };
        for (uint page = 0; page < count / 256; page++)
        {
            var entries = Enumerable.Range(0, 256).Select(i => new MemberEntryV1(page * 256 + (uint)i + 1, 1, (page * 256 + (uint)i + 1).ToString("D26"), "Cleared")).ToArray();
            mutations.Add(PayloadProtectionStateMutation.Put(new MemberPageV1(f.Root, page, entries), page == 0 ? existing.Pages[0].ETag : null));
        }
        await f.Store.CommitAsync(mutations);
        int capabilityReads = 0;
        f.Store.Reading = key => { if (key.StartsWith("eventstore:pp:v2:capability:", StringComparison.Ordinal)) { capabilityReads++; } };
        var deployment = new PayloadProtectionDeploymentCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Clock,
            (duration, _) => { f.Clock.Advance(duration); return ValueTask.CompletedTask; });
        await deployment.CloseAsync(f.Identity, 1);
        var closed = (await f.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(f.Root)))!.Value;
        await deployment.OpenSuccessorAsync(f.Identity, closed with { Generation = 2, Status = "Open" });
        capabilityReads.ShouldBe(0);
        (await f.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(f.Root)))!.Value.Generation.ShouldBe(2ul);
    }

    [Fact]
    public async Task DefiniteDeploymentConflictReturnsMismatchAndUnknownAcknowledgementReturnsUnavailable()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var deployment = new PayloadProtectionDeploymentCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Clock);
        f.Store.BeforeCommit = () =>
        {
            var current = f.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(f.Root)).GetAwaiter().GetResult()!;
            f.Store.CommitAsync([PayloadProtectionStateMutation.Compare(current)]).GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
        };
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => deployment.CloseAsync(f.Identity, 1).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        f.Store.LoseNextAcknowledgement = true;
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => deployment.CloseAsync(f.Identity, 1).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        await deployment.CloseAsync(f.Identity, 1);
    }

    [Fact]
    public async Task DefiniteRotationIntentConflictReturnsMismatchWithoutWrappingAgain()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Store.Committing = mutations =>
        {
            if (!mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:operation:", StringComparison.Ordinal)
                && PayloadProtectionStateCodec.Decode<OperationV1>(m.Bytes!).Operation == "Rewrap")) { return; }
            f.Store.Committing = null;
            var current = f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)).GetAwaiter().GetResult()!;
            f.Store.CommitAsync([PayloadProtectionStateMutation.Compare(current)]).GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
        };
        var rotation = new PayloadKeyRotationCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(f.Clock), _ => true, "fixture-actor", f.Clock);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, "01J00000000000000000009402", "fixture:version-02").AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        f.Backend.WrapCalls.ShouldBe(1); f.Backend.UnwrapCalls.ShouldBe(0);
    }
}
