using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Events;

/// <summary>Persisted Redis proof for synthetic target receipts.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class TrustedEffectPersistenceTests(DaprTestContainerFixture fixture)
{
    /// <summary>Receipt and first event survive an actor turn and stop duplicate target handling.</summary>
    [Fact]
    public async Task EffectReceiptAndEventPersistTogether()
    {
        fixture.SetupCounterDomain();
        string targetId = "trusted-effect-" + Guid.NewGuid().ToString("N");
        var identity = new EffectIdentity(
            "tenant-a", "counter", "source-1", 1,
            EffectKindCatalog.ChildCompletionResume, "counter", targetId, 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var submission = new TrustedEffectSubmission(identity, "IncrementCounter", [123, 125], messageId, messageId);
        var context = new TrustedEffectContext("synthetic-test", "synthetic-integration", "source-cause", "fixture-only");
        var aggregate = new AggregateIdentity(identity.Tenant, identity.TargetDomain, identity.TargetAggregate);
        var factory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = fixture.DaprHttpEndpoint });
        IAggregateActor actor = factory.CreateActorProxy<IAggregateActor>(
            new ActorId(aggregate.ActorId), fixture.AggregateActorTypeName);
        fixture.ThrowIfHostStopped();
        var admission = new TrustedEffectAdmission(submission, context, "SYNTHETIC-INTENT-V1");
        string proof = await fixture.Services.GetRequiredService<ITrustedEffectGatewayProof>()
            .SignAsync(admission);

        _ = await Should.ThrowAsync<Exception>(
            () => actor.ProcessTrustedEffectAsync(submission, context, "forged-proof"));

        TrustedEffectResult first = await actor.ProcessTrustedEffectAsync(submission, context, proof);
        TrustedEffectResult replay = await actor.ProcessTrustedEffectAsync(submission, context, proof);

        first.Disposition.ShouldBe(TrustedEffectDisposition.Success);
        first.Replayed.ShouldBeFalse();
        replay.Replayed.ShouldBeTrue();
        string receiptJson = await fixture.GetActorStateJsonAsync(
            fixture.AggregateActorTypeName,
            aggregate.ActorId,
            "effect_receipt_" + first.EffectId);
        using JsonDocument receipt = JsonDocument.Parse(receiptJson);
        JsonElement root = receipt.RootElement;
        (root.TryGetProperty("EffectId", out JsonElement effectId)
            ? effectId.GetString()
            : root.GetProperty("effectId").GetString()).ShouldBe(first.EffectId);
        (root.TryGetProperty("SemanticDigest", out JsonElement semanticDigest)
            ? semanticDigest.GetString()
            : root.GetProperty("semanticDigest").GetString()).ShouldBe("SYNTHETIC-INTENT-V1");
        (await actor.GetCurrentSequenceAsync()).ShouldBe(1);
        _ = await fixture.GetActorStateJsonAsync(
            fixture.AggregateActorTypeName,
            aggregate.ActorId,
            aggregate.EventStreamKeyPrefix + "1");
    }
}
