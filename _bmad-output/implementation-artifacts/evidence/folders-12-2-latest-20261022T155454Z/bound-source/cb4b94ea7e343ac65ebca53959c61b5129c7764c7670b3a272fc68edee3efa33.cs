using System.Text.Json;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class ProjectionDeliveryStateContractTests
{
    [Fact]
    public async Task HistoricalSchema1ProofRow_KeepsItsConfiguredBytesBeyondTheGuardedAbsoluteCap()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1).ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState original = fixture.Rows[route];
        string messageId = new('x', ProjectionDeliveryStateSize.AbsoluteLimit + 1);
        var stored = fixture.Events[0] with { MessageId = messageId };
        var wire = new Hexalith.EventStore.Contracts.Projections.ProjectionEventDto(stored.EventTypeName, stored.Payload,
            stored.SerializationFormat, stored.SequenceNumber, stored.Timestamp, stored.CorrelationId, stored.MessageId,
            stored.UserId, stored.GlobalPosition);
        ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(fixture.Identity, route, [wire]);
        ProjectionDeliveryEventDigest item = history.Events.Single();
        ProjectionDeliveryState historical = original with
        {
            SchemaVersion = 1, GuardEvidenceVersion = null, GuardEvidence = null, LastCompletedMessageId = messageId,
            CompletedPrefixFingerprint = history.PrefixFingerprint,
            CompletedReceipts = [new(item.SequenceNumber, item.MessageId, item.EventFingerprint, item.PrefixFingerprint)],
            CompletedMessageIdDigests = [Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(messageId)))],
        };
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));

        (await store.TrySaveAsync(fixture.Identity, route, historical, string.Empty).ConfigureAwait(true)).ShouldBeTrue();
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true);

        read.StateBytes.ShouldBeGreaterThan(ProjectionDeliveryStateSize.AbsoluteLimit);
        read.State.ShouldNotBeNull().LastCompletedMessageId.ShouldBe(messageId);
        read.State.CompletedMessageIdDigests.ShouldBe(historical.CompletedMessageIdDigests);
        read.State.SchemaVersion.ShouldBe(1);
        read.SourceGuard.ShouldBeNull();
    }

    [Theory]
    [InlineData(256)]
    [InlineData(4096)]
    public async Task GuardedProofRow_ExactSerializedByteCeilingPasses_OneByteOverRetainsEveryProof(int retention)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState state = fixture.Rows[route];
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(state, fixture.Backend.JsonSerializerOptions);
        long limit = ProjectionDeliveryStateSize.GetLimit(state, retention);
        byte[] exact = [.. serialized, .. Enumerable.Repeat((byte)' ', checked((int)(limit - serialized.Length)))];
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, exact);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()),
            Options.Create(new ProjectionDeliveryIdempotencyOptions { CompletedReceiptLimit = retention }));
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true);
        read.StateBytes.ShouldBe(limit);
        read.State!.CompletedMessageIdDigests.ShouldBe(state.CompletedMessageIdDigests);

        byte[] oversized = [.. exact, (byte)' '];
        fixture.Backend.SeedByteStore(key, oversized);
        ProjectionDeliveryHistoryValidationException exception = await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(
            () => store.ReadAsync(fixture.Identity, route)).ConfigureAwait(true);
        exception.Message.ShouldBe("delivery_proof_limit_exceeded");
        fixture.Backend.ByteStoreValue(key).ShouldBe(oversized);
    }

    [Fact]
    public async Task GuardedProofRow_OversizedWriteFailsBeforeDaprMutation()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        ProjectionDeliveryState oversized = fixture.Rows[route] with { CompletedPrefixFingerprint = new string('x', 20_000) };
        int priorWrites = fixture.Backend.TrySaveByteOperations.Count;
        ProjectionDeliveryHistoryValidationException exception = await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(
            () => store.TrySaveAsync(fixture.Identity, route, oversized, string.Empty)).ConfigureAwait(true);
        exception.Message.ShouldBe("delivery_proof_limit_exceeded");
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(priorWrites);
        fixture.Backend.ByteStoreContains(ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route)).ShouldBeFalse();
        oversized.CompletedMessageIdDigests.ShouldBe(fixture.Rows[route].CompletedMessageIdDigests);
    }
}
