// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 8-10, 13-15; lifecycle amendment sections 1-7.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V061, V068 and V120 indexed rotation, restore rejection and fleet closure boundaries.</summary>
public sealed class LifecycleRotationRestoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V061_RewrapPreservesTheDEKAndImmutableEnvelopeAndReconcilesALostCommitAcknowledgement(bool loseAcknowledgement)
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        byte[]? retainedDek = null;
        fixture.Backend.Wrap = (_, bytes, _) => { retainedDek = bytes.ToArray(); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-01")); };
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.AcquireCompletionAsync(write.Completion!);
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        byte[] envelopeBefore = write.Result.PayloadBytes.ToArray();
        var owned = new List<byte[]>();
        fixture.Backend.Unwrap = (_, provider, _, _) =>
        {
            provider.ShouldBeOneOf("fixture:version-01", "fixture:version-02");
            byte[] bytes = retainedDek!.ToArray(); owned.Add(bytes);
            return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, bytes, provider));
        };
        fixture.Backend.Wrap = (_, bytes, _) =>
        {
            bytes.ToArray().ShouldBe(retainedDek);
            if (loseAcknowledgement) { fixture.Store.LoseNextAcknowledgement = true; }
            byte[] wrapped = Enumerable.Repeat((byte)2, 32).ToArray(); owned.Add(wrapped);
            return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, wrapped, "fixture:version-02"));
        };
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var rotation = new PayloadKeyRotationCoordinator(fixture.Store, roots, fixture.Leases, fixture.Backend, new(fixture.Clock), _ => true, "fixture-actor", fixture.Clock);
        const string operation = "01J00000000000000000000301";
        if (loseAcknowledgement)
        {
            await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(fixture.Identity, write.Completion!.KeyReference, 1, operation, "fixture:version-02").AsTask());
        }
        else { (await rotation.RewrapAsync(fixture.Identity, write.Completion!.KeyReference, 1, operation, "fixture:version-02")).ShouldBeTrue(); }
        (await rotation.RewrapAsync(fixture.Identity, write.Completion!.KeyReference, 1, operation, "fixture:version-02")).ShouldBeTrue();
        fixture.Backend.WrapCalls.ShouldBe(2); // One creation and one rewrap, including after acknowledgement loss.
        fixture.Store.Audits.Single(a => a.Operation == "Rewrap" && a.Result == "Succeeded").AttemptCount.ShouldBe(3u); // Two exact-version unwraps and one wrap.
        WrappedDekV1 key = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion.KeyReference, 1)))!.Value;
        key.ProviderKeyVersionId.ShouldBe("fixture:version-02"); key.State.ShouldBe("Active");
        (await rotation.VerifyInventoryAsync("fixture:version-01")).Head!.Value.EntryCount.ShouldBe(0ul);
        (await rotation.VerifyInventoryAsync("fixture:version-02")).Head!.Value.EntryCount.ShouldBe(1ul);
        write.Result.PayloadBytes.ShouldBe(envelopeBefore);
        foreach (byte[] buffer in owned) { buffer.ShouldAllBe(b => b == 0); }
        CryptographicOperations.ZeroMemory(retainedDek!);
    }

    [Fact]
    public async Task V068_RestoreAfterErasureIsQuarantinedThroughTheExistingWorkflowWithoutUnwrapOrKeyImport()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        FenceV1 backupFence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(fixture.Root)))!.Value;
        PayloadProtectionStateValue<LifecycleV1> before = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!;
        WrappedDekV1 backup = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value;
        await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(before.Value with { State = "Deleted", Epoch = 2, ReasonCode = "domain-deleted" }, before.ETag)]);
        RestoredBackupAdmissionResult? recorded = null;
        var admission = new PayloadRestoreAdmission(fixture.Store, new([new("tenant-a", "parties")]), fixture.Backend, new(fixture.Clock), _ => true,
            (result, _) => { recorded = result; return ValueTask.FromResult(result); }, fixture.Clock);
        var request = new RestoredBackupAdmissionRequest("01J00000000000000000000302", "tenant-a", "parties", "party-01", null, null,
            "01J00000000000000000000303", fixture.Clock.GetUtcNow(), fixture.Clock.GetUtcNow(), 1, KeyReferencePolicy.NoKeyReference, null, null, null, "fixture-actor");
        RestoredBackupAdmissionResult result = await admission.AdmitAsync(request, backupFence, before.Value, [backup]);
        result.State.ShouldBe(RestoredBackupAdmissionState.Quarantined); recorded.ShouldBe(result);
        fixture.Backend.UnwrapCalls.ShouldBe(0);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(fixture.Scope)))!.Value.State.ShouldBe("Deleted");
    }

    [Fact]
    public async Task V120_ClosureStartsAFreshFullWaitAndDoesNotRemoveUnclearedMemberTombstones()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        int waits = 0;
        var deployments = new PayloadProtectionDeploymentCoordinator(fixture.Store, roots, fixture.Clock, (duration, _) => { waits++; fixture.Clock.Advance(duration); return ValueTask.CompletedTask; });
        await deployments.CloseAsync(fixture.Identity, 1);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => fixture.Leases.RenewAsync(fixture.Identity).AsTask());
        DeploymentGenerationV1 closed = (await fixture.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(fixture.Root)))!.Value;
        await deployments.OpenSuccessorAsync(fixture.Identity, closed with { Generation = 2, Status = "Open" });
        waits.ShouldBe(1);
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadMembersAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(1u);
        FenceV1 fence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(fixture.Root)))!.Value;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => deployments.AdvanceFenceAsync(fixture.Identity, fence with {
            DeploymentGeneration = 2, ApprovalEpoch = 8, ApprovalId = "01J00000000000000000000304" }).AsTask()); // No serving successor reader yet.
    }
}
