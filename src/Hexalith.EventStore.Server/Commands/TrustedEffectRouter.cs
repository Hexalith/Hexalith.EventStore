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
    IOptions<EventStoreActorOptions> actorOptions) : ITrustedEffectRouter
{
    /// <inheritdoc/>
    public Task<TrustedEffectResult> RouteAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        string gatewayProof,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        EffectIdentity identity = submission.Identity;
        var target = new AggregateIdentity(identity.Tenant, identity.TargetDomain, identity.TargetAggregate);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(target.ActorId), actorOptions.Value.AggregateActorTypeName);
        return actor.ProcessTrustedEffectAsync(submission, context, gatewayProof);
    }
}
