#if SELECTED
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

namespace PublishedConsumer;

/// <summary>Signs admitted fixture effects and submits to the real target actor receipt inbox.</summary>
public sealed class FixtureEffectSubmitter(ITrustedEffectAdmissionPolicy policy, ITrustedEffectGatewayProof proof) : ITrustedEffectSubmitter
{
    public async Task<TrustedEffectResult> SubmitAsync(TrustedEffectSubmission submission, TrustedEffectContext context, CancellationToken cancellationToken = default)
    {
        TrustedEffectAdmission admission = await policy.PrepareAsync(submission, context, cancellationToken);
        string signed = await proof.SignAsync(admission, cancellationToken);
        IAggregateActor actor = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = "http://127.0.0.1:" + Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") })
            .CreateActorProxy<IAggregateActor>(new ActorId(submission.Identity.Tenant + ":" + submission.Identity.TargetDomain + ":" + submission.Identity.TargetAggregate), "AggregateActor");
        TrustedEffectResult result = await actor.ProcessTrustedEffectAsync(submission, context, signed);
        await policy.CompleteAsync(admission, cancellationToken);
        return result;
    }
}
#endif
