using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Fact]
    public async Task GuardedCanonicalReplay_LongOwnerGenerationUsesCompactContextAndRecoversOneOriginalManifest()
    {
        string generation = new('g', 4096);
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1,
            handlerSemantics: "full-replay-v1", ownerGeneration: generation).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        fixture.Http.LoseReplayResponseAfterAction = "stage";

        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();

        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
        fixture.Rows[route].ActiveReservation.ShouldNotBeNull();
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        string key = fixture.Http.ReplayPreparationKeys.Single(static key => key.EndsWith(":-2", StringComparison.Ordinal));
        byte[] manifest = (await fixture.Models.GetAsync<byte[]>("models", key).ConfigureAwait(true)).Value.ShouldNotBeNull();
        string fingerprint = ReadModelBatchFingerprint.ComputeFromManifest(DomainProjectionGuardedReplay.OriginalManifest(manifest, null).Span);
        fixture.Http.MaximumReplayContextHeaderBytes.ShouldBeInRange(1, 511);
        ProjectionSourceGuardDeliveryResult recovered = await fixture.DeliverAsync(256).ConfigureAwait(true);
        recovered.AlreadyCurrent.ShouldBeTrue(recovered.ReasonCode);

        ProjectionDeliveryState completed = fixture.Rows[route];
        completed.LastDeliveredSequence.ShouldBe(2);
        completed.ActiveReservation.ShouldBeNull();
        completed.CompletedMessageIdDigests.Count.ShouldBe(2);
        completed.GuardEvidence.ShouldNotBeNull().OwnerProof.Selection.OwnerGeneration.ShouldBe(generation);
        completed.GuardEvidence.OwnerProof.ReceiptFingerprint.ShouldBe(fingerprint);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Handlers.Single().Calls.ShouldBe(0);
        JsonSerializer.SerializeToUtf8Bytes(completed, JsonSerializerOptions.Web).LongLength
            .ShouldBeLessThanOrEqualTo(ProjectionDeliveryStateSize.AbsoluteLimit);
        fixture.Http.MaximumReplayContextHeaderBytes.ShouldBeInRange(1, 511);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionReplayContext " + JsonSerializer.Serialize(new
        {
            ownerGenerationUtf8Bytes = System.Text.Encoding.UTF8.GetByteCount(generation),
            headerBytes = fixture.Http.MaximumReplayContextHeaderBytes,
            proofRowBytes = JsonSerializer.SerializeToUtf8Bytes(completed, JsonSerializerOptions.Web).Length,
            originalManifestBytes = manifest.Length, preparations = fixture.Handlers.Single().RebuildCalls,
        }));
    }

    [Fact]
    public async Task CompactReplayContextResumesAnExistingVersionOneReferenceWithoutPreparingAgain()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1,
            handlerSemantics: "full-replay-v1").ConfigureAwait(true);
        (await fixture.StagedCanonicalReplayAsync("stage", legacyPrivateReference: true).ConfigureAwait(true))
            .Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);

        foreach (string action in new[] { "commit", "verify" })
            (await fixture.StagedCanonicalReplayAsync(action, compactTransport: true).ConfigureAwait(true))
                .Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);

        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        GuardedTailModel selected = (await fixture.Models.GetAsync<GuardedTailModel>("models",
            fixture.Identity.ActorId + ":" + fixture.Catalog.OrdinaryProjectionTypes.Single()).ConfigureAwait(true)).Value.ShouldNotBeNull();
        selected.Count.ShouldBe(2);
        selected.ProjectionOwner.OwnerGeneration.ShouldBe("generation-1");
    }
}
