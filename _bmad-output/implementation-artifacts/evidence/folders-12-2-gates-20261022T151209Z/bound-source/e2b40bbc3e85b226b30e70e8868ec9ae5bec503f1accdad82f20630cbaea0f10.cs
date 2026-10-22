using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Guard authority. Signing and lookup evidence are returned only for an exact installed receipt. Missing provenance returns null.</summary>
public sealed class GovernanceGuardAuthority : IGovernanceGuardAuthority
{
    private readonly IGovernanceGuardInstallation? _installation;

    /// <summary>Creates the authority. Production passes no installation.</summary>
    /// <param name="installation">Injected evidence source.</param>
    public GovernanceGuardAuthority(IGovernanceGuardInstallation? installation = null)
    {
        _installation = installation;
    }

    /// <inheritdoc />
    public async Task<GovernanceSigningRecovery?> ReadSigningRecoveryAsync(GovernanceProtocolReceipt noIssue, TenantGovernanceGuardState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || noIssue is null || state is null || string.IsNullOrWhiteSpace(noIssue.ReceiptId))
        {
            return null;
        }

        try
        {
            GovernanceSigningRecovery? recovery = await _installation.ReadSigningRecoveryAsync(noIssue, state, cancellationToken).ConfigureAwait(false);
            if (recovery is null || recovery.NoIssueReceiptId != noIssue.ReceiptId || recovery.CurrentGuardRevision != state.Revision
                || string.IsNullOrWhiteSpace(recovery.AuthorityRevision) || string.IsNullOrWhiteSpace(recovery.CurrentHealthyKeyVersion)
                || string.IsNullOrWhiteSpace(recovery.NoIssueReceiptId))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(noIssue.DetachedJwsDigest) || recovery.DetachedJwsDigest != noIssue.DetachedJwsDigest
                || string.IsNullOrWhiteSpace(noIssue.SigningRequestId) || recovery.SigningRequestId != noIssue.SigningRequestId)
            {
                return null;
            }

            if (noIssue.Capability is not null && recovery.Payload != noIssue.Capability)
            {
                return null;
            }

            return recovery;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GovernanceGuardEvidence?> ReadLookupAsync(GovernanceGuardTransition transition, string intentDigest, string targetMutationDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || transition is null)
        {
            return null;
        }

        try
        {
            GovernanceGuardEvidence? evidence = await _installation.ReadLookupAsync(transition, intentDigest, targetMutationDigest, cancellationToken).ConfigureAwait(false);
            return Exact(evidence, transition, intentDigest, targetMutationDigest, requireAppendResource: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GovernanceGuardEvidence?> ReadAsync(GovernanceGuardTransition transition, string intentDigest, string targetMutationDigest, TenantGovernanceGuardState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || transition is null || state is null || state.TenantId != transition.TenantId)
        {
            return null;
        }

        try
        {
            GovernanceGuardEvidence? evidence = await _installation.ReadAsync(transition, intentDigest, targetMutationDigest, state, cancellationToken).ConfigureAwait(false);
            return Exact(evidence, transition, intentDigest, targetMutationDigest, transition.Operation == GovernanceGuardOperation.AppendWrite);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static GovernanceGuardEvidence? Exact(GovernanceGuardEvidence? evidence, GovernanceGuardTransition transition, string intentDigest, string targetMutationDigest, bool requireAppendResource)
    {
        if (evidence is null || evidence.TenantId != transition.TenantId || evidence.IntentDigest != intentDigest
            || evidence.TargetMutationDigest != targetMutationDigest || string.IsNullOrWhiteSpace(evidence.AuthorityReceiptId)
            || string.IsNullOrWhiteSpace(evidence.AuthorityRevision))
        {
            return null;
        }

        if (requireAppendResource && string.IsNullOrWhiteSpace(evidence.AppendResourceId))
        {
            return null;
        }

        return evidence;
    }
}
