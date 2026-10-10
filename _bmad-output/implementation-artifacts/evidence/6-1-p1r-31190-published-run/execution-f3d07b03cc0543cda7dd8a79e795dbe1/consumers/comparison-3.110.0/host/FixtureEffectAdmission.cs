#if P1R_CAPABILITIES
using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

/// <summary>Bounded fixture authority, independently disclosed; it grants no production identity acceptance.</summary>
#if P1R_CANDIDATE
internal sealed class FixtureEffectAdmission(IConfiguration configuration, ITrustedEffectAuditSink audit) : ITrustedEffectAdmissionPolicy
#else
internal sealed class FixtureEffectAdmission(IConfiguration configuration) : ITrustedEffectAdmissionPolicy
#endif
{
    /// <inheritdoc/>
    public Task<TrustedEffectAdmission> PrepareAsync(TrustedEffectSubmission submission, TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string configured = configuration["P1R_DELEGATION"] ?? throw new InvalidOperationException("Fixture authority unavailable.");
        bool authorized = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(configured)),
            SHA256.HashData(Encoding.UTF8.GetBytes(context.DelegationToken)));
        if (!authorized || context.Workload != "p1r-fixture" || context.Purpose != "published-qualification"
            || submission.Identity.Tenant != "tenant-a" || submission.Identity.TargetDomain != "counter"
            || submission.CommandType != "P1R.Counter.IncrementCounter"
            || submission.MessageId != EffectIdentityCodec.ComputeMessageId(submission.Identity)
            || submission.IdempotencyKey != submission.MessageId)
        {
            throw new InvalidOperationException("Bounded fixture delegation refused.");
        }

        return Task.FromResult(new TrustedEffectAdmission(submission, context, "P1R-BOUNDED-COUNTER-V1"));
    }

    /// <inheritdoc/>
    public Task CompleteAsync(TrustedEffectAdmission admission, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if P1R_CANDIDATE
        return audit.AppendAsync(new TrustedEffectAuditRecord(
            "submission", admission.Submission.Identity.Tenant,
            EffectIdentityCodec.ComputeEffectId(admission.Submission.Identity),
            admission.Context.Workload, admission.Context.Purpose, "authorized"), cancellationToken);
#else
        return Task.CompletedTask;
#endif
    }

    /// <inheritdoc/>
    public async Task<TrustedEffectAdmission> AdmitAsync(TrustedEffectSubmission submission, TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        TrustedEffectAdmission admission = await PrepareAsync(submission, context, cancellationToken);
        await CompleteAsync(admission, cancellationToken);
        return admission;
    }
}
#endif
