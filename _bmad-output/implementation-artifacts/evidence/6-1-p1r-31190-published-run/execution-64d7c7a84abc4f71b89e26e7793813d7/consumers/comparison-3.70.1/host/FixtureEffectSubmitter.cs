#if P1R_CANDIDATE
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

/// <summary>Routes fixture-admitted signed effects through the published target actor and its persisted inbox.</summary>
internal sealed class FixtureEffectSubmitter(ITrustedEffectAdmissionPolicy policy, ITrustedEffectGatewayProof proof) : ITrustedEffectSubmitter
{
    /// <inheritdoc/>
    public async Task<TrustedEffectResult> SubmitAsync(TrustedEffectSubmission submission, TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        TrustedEffectAdmission admitted = await policy.PrepareAsync(submission, context, cancellationToken);
        string signed = await proof.SignAsync(admitted, cancellationToken);
        IAggregateActor actor = ActorProxy.Create<IAggregateActor>(new ActorId($"{submission.Identity.Tenant}:{submission.Identity.TargetDomain}:{submission.Identity.TargetAggregate}"), "AggregateActor");
        TrustedEffectResult result = await actor.ProcessTrustedEffectAsync(submission, context, signed);
        await policy.CompleteAsync(admitted, cancellationToken);
        return result;
    }
}
#endif
