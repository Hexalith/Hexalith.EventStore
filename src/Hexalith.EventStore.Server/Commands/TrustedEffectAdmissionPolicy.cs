using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Fail-closed trusted effect admission with server-owned semantic intent.</summary>
public sealed class TrustedEffectAdmissionPolicy(
    IIdempotencyIntentAdapterRegistry intentAdapters,
    ITrustedEffectDelegationVerifier? delegationVerifier = null,
    ITrustedEffectCommandAuthority? commandAuthority = null,
    ITrustedEffectRetentionGate? retentionGate = null,
    ITrustedEffectAuditSink? auditSink = null) : ITrustedEffectAdmissionPolicy
{
    /// <inheritdoc/>
    public async Task<TrustedEffectAdmission> AdmitAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        TrustedEffectAdmission admission = await PrepareAsync(submission, context, cancellationToken)
            .ConfigureAwait(false);
        await CompleteAsync(admission, cancellationToken).ConfigureAwait(false);
        return admission;
    }

    /// <inheritdoc/>
    public async Task<TrustedEffectAdmission> PrepareAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (delegationVerifier is null || commandAuthority is null || retentionGate is null || auditSink is null)
        {
            throw new InvalidOperationException("Trusted effect origin, command, retention, or audit policy is not configured.");
        }

        try
        {
            return await PrepareCoreAsync(submission, context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await auditSink.AppendAsync(new TrustedEffectAuditRecord(
                "submission", submission.Identity?.Tenant, null,
                context.Workload, context.Purpose, "denied"), CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(
        TrustedEffectAdmission admission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ITrustedEffectRetentionGate gate = retentionGate
            ?? throw new InvalidOperationException("Trusted effect retention policy is not configured.");
        ITrustedEffectAuditSink audit = auditSink
            ?? throw new InvalidOperationException("Trusted effect audit policy is not configured.");

        try
        {
            string effectId = EffectIdentityCodec.ComputeEffectId(admission.Submission.Identity);
            // Registration is a privileged lifecycle mutation. The audit must be durable first.
            await audit.AppendAsync(new TrustedEffectAuditRecord(
                "submission", admission.Submission.Identity.Tenant, effectId,
                admission.Context.Workload, admission.Context.Purpose, "attempted"), cancellationToken)
                .ConfigureAwait(false);
            await audit.AppendAsync(new TrustedEffectAuditRecord(
                "submission", admission.Submission.Identity.Tenant, effectId,
                admission.Context.Workload, admission.Context.Purpose, "authorized"), cancellationToken)
                .ConfigureAwait(false);
            await gate.ValidateAsync(admission.Submission.Identity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await audit.AppendAsync(new TrustedEffectAuditRecord(
                "submission", admission.Submission.Identity?.Tenant, null,
                admission.Context.Workload, admission.Context.Purpose, "denied"), CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    private async Task<TrustedEffectAdmission> PrepareCoreAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken)
    {
        if (submission.Identity is null
            || submission.CommandPayload is null
            || string.IsNullOrWhiteSpace(submission.CommandType)
            || string.IsNullOrWhiteSpace(context.Workload)
            || string.IsNullOrWhiteSpace(context.Purpose)
            || string.IsNullOrWhiteSpace(context.CausationId)
            || string.IsNullOrWhiteSpace(context.DelegationToken))
        {
            throw new InvalidOperationException("Trusted effect request or provenance is incomplete.");
        }

        string messageId = EffectIdentityCodec.ComputeMessageId(submission.Identity);
        if (!string.Equals(submission.MessageId, messageId, StringComparison.Ordinal)
            || !string.Equals(submission.IdempotencyKey, messageId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Trusted effect identity does not match its canonical tuple.");
        }

        var command = new SubmitCommand(
            messageId,
            submission.Identity.Tenant,
            submission.Identity.TargetDomain,
            submission.Identity.TargetAggregate,
            submission.CommandType,
            submission.CommandPayload,
            messageId,
            context.Workload);
        TrustedIdempotencyDescriptor descriptor = intentAdapters.Resolve(command);
        string semanticDigest = EffectIdentityCodec.RenderDigest(SHA256.HashData(descriptor.CanonicalIntent));
        await delegationVerifier!.VerifyAsync(submission, context, semanticDigest, cancellationToken)
            .ConfigureAwait(false);
        if (!commandAuthority!.IsAllowed(submission, context))
        {
            throw new InvalidOperationException("Trusted effect command origin or purpose is denied.");
        }

        return new TrustedEffectAdmission(submission, context, semanticDigest);
    }
}
