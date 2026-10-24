using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V041/V049-V058/V122-V123 ordered reservation, caller outcomes and issued-context occurrence safety.</summary>
public sealed class ReservationCompletionTests
{
    [Fact]
    public async Task V053_LostActivationAcknowledgementIsReconciledBeforeIdempotentCompletion()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.AcquireCompletionAsync(write.Completion!);
        fixture.Store.LoseNextAcknowledgement = true;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => fixture.Reservations.CompleteAsync(write.Completion!,
            PayloadProtectionPersistenceOutcome.Persisted).AsTask());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value.State.ShouldBe("Active");
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(0u);
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    [Fact]
    public async Task V049_V050_IntentPrecedesWrapAndReservedKeyIndexesPrecedeOutput()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        ReadOnlyMemory<byte> material = default;
        fixture.Backend.Wrap = async (id, bytes, token) =>
        {
            material = bytes;
            PayloadProtectionStateValue<OperationV1> intent = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(id), token))!;
            intent.Value.Phase.ShouldBe("Started");
            (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, intent.Value.KeyRef, 1), token)).ShouldBeNull();
            (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root, token)).Head.Value.EntryCount.ShouldBe(1u);
            return new(PayloadBackendFailure.None, Enumerable.Repeat((byte)0x83, 32).ToArray(), "fixture:version-01");
        };
        (CoreProtectionResult result, PayloadProtectionCompletionContext? completion) = await fixture.Reservations.ProtectEventAsync(
            CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        completion.ShouldNotBeNull();
        result.SerializationFormat.ShouldBe("json+pdenc-v2");
        material.ToArray().ShouldAllBe(b => b == 0);
        WrappedDekV1 key = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, completion.KeyReference, 1)))!.Value;
        key.State.ShouldBe("Reserved");
        key.FenceApprovalEpoch.ShouldBe(7ul);
        key.HighestWrittenEpoch.ShouldBe(1ul);
        var indexes = new PayloadKeyIndexes(fixture.Store, fixture.Clock);
        PayloadKeyIndexes.VerifyMembership(key, await indexes.ReadScopeAsync(fixture.Scope), await indexes.ReadKekAsync(
            PayloadProtectionStateKeys.Digest(fixture.Backend.BackendId), PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId)));
        FenceV1 fence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(fixture.Root)))!.Value;
        fence.ApprovalEpoch.ShouldBe(7ul);
        fence.HighestWrittenFormat.ShouldBe("V2");
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(completion.OperationId)))!.Value.Phase.ShouldBe("PayloadReturned");
        CryptographicOperations.ZeroMemory(result.PayloadBytes);
    }

    [Fact]
    public async Task V041_InvalidSelectionAndAuditDenialCreateNoIntentOrKey()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        (CoreProtectionResult unchanged, PayloadProtectionCompletionContext? completion) = await fixture.Reservations.ProtectEventAsync(
            CompatibilityTestData.PlainJson(), [], TestFixture.Context());
        completion.ShouldBeNull();
        unchanged.ProtectedPathCount.ShouldBe(0);
        await Should.ThrowAsync<PayloadProtectionFormatException>(() => fixture.Reservations.ProtectEventAsync(
            CompatibilityTestData.PlainJson(), ["/email", "/email"], TestFixture.Context()).AsTask());
        fixture.Backend.WrapCalls.ShouldBe(0);
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(0u);
        ReservationTestFixture denied = await ReservationTestFixture.CreateAsync(allowAudit: false);
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => denied.Reservations.ProtectEventAsync(
            CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        denied.Backend.WrapCalls.ShouldBe(0);
        (await new PayloadProtectionCatalogs(denied.Store).ReadWorkAsync(denied.Root)).Head.Value.EntryCount.ShouldBe(0u);
    }

    [Fact]
    public async Task V053_V056_OnlyIssuedMatchingContextCanActivateAndCompleteIdempotently()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        PayloadProtectionCompletionContext completion = write.Completion!;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => fixture.Reservations.AcquireCompletionAsync(completion with {
            Occurrence = completion.Occurrence with { RecordSequence = 2 } }).AsTask());
        await Should.ThrowAsync<PayloadProtectionStateException>(() => fixture.Reservations.CompleteAsync(completion,
            PayloadProtectionPersistenceOutcome.Persisted).AsTask());
        await fixture.Reservations.AcquireCompletionAsync(completion);
        await fixture.Reservations.CompleteAsync(completion, PayloadProtectionPersistenceOutcome.Persisted);
        await fixture.Reservations.CompleteAsync(completion, PayloadProtectionPersistenceOutcome.Persisted);
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, completion.KeyReference, 1)))!.Value.State.ShouldBe("Active");
        (await fixture.Store.ReadAsync<CompletionLeaseV1>(PayloadProtectionStateKeys.Completion(fixture.Scope, completion.KeyReference, 1, completion.OperationId))).ShouldBeNull();
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(0u);
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(completion.OperationId)))!.Value.Status.ShouldBe("Completed");
        await Should.ThrowAsync<PayloadProtectionStateException>(() => fixture.Reservations.CompleteAsync(completion,
            PayloadProtectionPersistenceOutcome.NotPersisted).AsTask());
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    [Fact]
    public async Task V052_UnknownRetainsLeaseAndReservationWhileNotPersistedReleasesOnlyLease()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        PayloadProtectionCompletionContext completion = write.Completion!;
        await fixture.Reservations.AcquireCompletionAsync(completion);
        await fixture.Reservations.CompleteAsync(completion, PayloadProtectionPersistenceOutcome.Unknown);
        string lease = PayloadProtectionStateKeys.Completion(fixture.Scope, completion.KeyReference, 1, completion.OperationId);
        (await fixture.Store.ReadAsync<CompletionLeaseV1>(lease)).ShouldNotBeNull();
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, completion.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(1u);
        await fixture.Reservations.CompleteAsync(completion, PayloadProtectionPersistenceOutcome.NotPersisted);
        (await fixture.Store.ReadAsync<CompletionLeaseV1>(lease)).ShouldBeNull();
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, completion.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(completion.OperationId)))!.Value.Status.ShouldBe("Pending");
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    [Fact]
    public async Task V049_V122_ChangedApprovalAfterWrapWithholdsKeyAndPayloadButRetainsIndexedIntent()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        ReadOnlyMemory<byte> observed = default;
        string? operation = null;
        fixture.Backend.Wrap = async (id, bytes, token) =>
        {
            operation = id;
            observed = bytes;
            PayloadProtectionStateValue<FenceV1> fence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(fixture.Root), token))!;
            await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(fence.Value with { ApprovalEpoch = 8 }, fence.ETag)], token);
            return new(PayloadBackendFailure.None, new byte[32], "fixture:version-01");
        };
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => fixture.Reservations.ProtectEventAsync(
            CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        observed.ToArray().ShouldAllBe(b => b == 0);
        OperationV1 intent = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operation!)))!.Value;
        intent.Phase.ShouldBe("Failed");
        intent.ReasonCode.ShouldBe("consistency-mismatch");
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, intent.KeyRef, 1))).ShouldBeNull();
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(fixture.Root)).Head.Value.EntryCount.ShouldBe(1u);
    }
}
