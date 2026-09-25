using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Fixture-only synthetic delegation policy for persisted receipt tests.</summary>
internal sealed class SyntheticTrustedEffectAdmissionPolicy : ITrustedEffectAdmissionPolicy
{
    /// <inheritdoc/>
    public Task<TrustedEffectAdmission> AdmitAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (submission.Identity.Tenant != "tenant-a"
            || submission.Identity.TargetDomain != "counter"
            || submission.CommandType != "IncrementCounter"
            || submission.MessageId != EffectIdentityCodec.ComputeMessageId(submission.Identity)
            || submission.IdempotencyKey != submission.MessageId
            || context.Workload != "synthetic-test"
            || context.Purpose != "synthetic-integration"
            || context.DelegationToken != "fixture-only")
        {
            throw new InvalidOperationException("Synthetic effect delegation denied.");
        }

        return Task.FromResult(new TrustedEffectAdmission(submission, context, "SYNTHETIC-INTENT-V1"));
    }
}
