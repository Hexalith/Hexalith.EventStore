#if FLOOR
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

namespace PublishedConsumer;

/// <summary>Admits only the synthetic qualification workload; no production authority is conferred.</summary>
public sealed class FixtureEffectPolicy : ITrustedEffectAdmissionPolicy
{
    /// <inheritdoc/>
    public Task<TrustedEffectAdmission> PrepareAsync(TrustedEffectSubmission submission, TrustedEffectContext context, CancellationToken cancellationToken = default)
    {
        if (context.Workload != "p1r-fixture" || context.Purpose != "qualification" || context.DelegationToken != "fixture-only"
            || submission.Identity.Tenant != "tenant-a" || submission.CommandType != "Increment"
            || submission.MessageId != EffectIdentityCodec.ComputeMessageId(submission.Identity) || submission.IdempotencyKey != submission.MessageId)
            throw new InvalidOperationException("Synthetic trusted-effect delegation refused.");
        return Task.FromResult(new TrustedEffectAdmission(submission, context, "P1R-SYNTHETIC-INTENT-V1"));
    }

    /// <inheritdoc/>
    public Task CompleteAsync(TrustedEffectAdmission admission, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task<TrustedEffectAdmission> AdmitAsync(TrustedEffectSubmission submission, TrustedEffectContext context, CancellationToken cancellationToken = default)
    {
        TrustedEffectAdmission admission = await PrepareAsync(submission, context, cancellationToken);
        await CompleteAsync(admission, cancellationToken);
        return admission;
    }
}
#endif
