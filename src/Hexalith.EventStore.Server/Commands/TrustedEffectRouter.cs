using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Canonical target routing for trusted effects.</summary>
public sealed class TrustedEffectRouter(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreActorOptions> actorOptions,
    ITrustedEffectRetentionGate? retentionGate = null) : ITrustedEffectRouter
{
    /// <inheritdoc/>
    public async Task<TrustedEffectResult> RouteAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        string gatewayProof,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        ITrustedEffectRetentionGate gate = retentionGate
            ?? throw new InvalidOperationException("Trusted effect retention policy is not configured.");
        EffectIdentity identity = submission.Identity;
        var target = new AggregateIdentity(identity.Tenant, identity.TargetDomain, identity.TargetAggregate);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(target.ActorId), actorOptions.Value.AggregateActorTypeName);
        TrustedEffectResult result = await actor.ProcessTrustedEffectAsync(submission, context, gatewayProof)
            .ConfigureAwait(false);
        // Target actors must finish their turn before completion calls the lifecycle actor.
        // Purge may hold the lifecycle turn while it awaits this target's erasure turn.
        await gate.CompleteAsync(identity, cancellationToken).ConfigureAwait(false);
        return result;
    }
}
