using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Stands in for the gateway on the live reminder path: it admits the submission with the synthetic policy,
/// signs the attested decision with the real gateway proof, and routes to the target aggregate actor, whose
/// Story 4.13 receipt inbox persists or replays the receipt in Redis.
/// </summary>
internal sealed class LiveActorTrustedEffectSubmitter(
    ITrustedEffectAdmissionPolicy admissionPolicy,
    ITrustedEffectGatewayProof gatewayProof,
    IActorProxyFactory actorProxyFactory,
    string aggregateActorTypeName) : ITrustedEffectSubmitter
{
    /// <inheritdoc/>
    public async Task<TrustedEffectResult> SubmitAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(context);
        TrustedEffectAdmission admission = await admissionPolicy
            .PrepareAsync(submission, context, cancellationToken)
            .ConfigureAwait(false);
        string proof = await gatewayProof.SignAsync(admission, cancellationToken).ConfigureAwait(false);
        EffectIdentity identity = submission.Identity;
        var target = new AggregateIdentity(identity.Tenant, identity.TargetDomain, identity.TargetAggregate);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(target.ActorId),
            aggregateActorTypeName);
        TrustedEffectResult result = await actor
            .ProcessTrustedEffectAsync(submission, context, proof)
            .ConfigureAwait(false);
        await admissionPolicy.CompleteAsync(admission, cancellationToken).ConfigureAwait(false);
        return result;
    }
}
