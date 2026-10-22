using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Canonical target routing for trusted effects.</summary>
public sealed class TrustedEffectRouter(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreActorOptions> actorOptions,
    ITrustedEffectRetentionGate? retentionGate,
    IProjectionActivationOutbox? projectionActivationOutbox,
    IProjectionUpdateOrchestrator? projectionOrchestrator) : ITrustedEffectRouter
{
    /// <summary>Preserves the released router constructor for existing consumers.</summary>
    public TrustedEffectRouter(IActorProxyFactory actorProxyFactory, IOptions<EventStoreActorOptions> actorOptions,
        ITrustedEffectRetentionGate? retentionGate = null)
        : this(actorProxyFactory, actorOptions, retentionGate, null, null) { }

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
        // Trusted effects bypass SubmitCommandHandler, whose write-ahead activation is
        // mandatory for recovering a committed event after a gateway restart.
        if (projectionActivationOutbox is not null)
            await projectionActivationOutbox.EnsureAsync(target, cancellationToken).ConfigureAwait(false);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(target.ActorId), actorOptions.Value.AggregateActorTypeName);
        TrustedEffectResult result = await actor.ProcessTrustedEffectAsync(submission, context, gatewayProof)
            .ConfigureAwait(false);
        // Target actors must finish their turn before completion calls the lifecycle actor.
        // Purge may hold the lifecycle turn while it awaits this target's erasure turn.
        await gate.CompleteAsync(identity, cancellationToken).ConfigureAwait(false);
        if (projectionOrchestrator is not null)
        {
            // Delivery may call the target actor again. Start only after its turn and
            // retention completion; a slow projection must not hold the effect receipt.
            _ = Task.Run(async () =>
            {
                try { await projectionOrchestrator.UpdateProjectionAsync(target, CancellationToken.None).ConfigureAwait(false); }
                catch { /* The durable activation remains owned by SDK retry recovery. */ }
            }, CancellationToken.None);
        }
        return result;
    }
}
