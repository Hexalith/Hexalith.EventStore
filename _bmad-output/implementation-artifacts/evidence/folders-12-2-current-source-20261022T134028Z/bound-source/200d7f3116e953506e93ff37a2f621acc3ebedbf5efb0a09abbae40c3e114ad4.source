using System.Security.Claims;

using Hexalith.EventStore.Client.Processes;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Authenticated, audited, crash-recoverable operator revalidation.</summary>
public sealed class ProcessDispositionService(
    IProcessDispositionAuthorizer authorizer,
    IProcessDispositionAuditSink audit,
    ProcessCheckpointStore checkpoints,
    ProcessSweepStore sweeps,
    IOptions<EventStoreProcessOptions>? options = null)
{
    private readonly IProcessDispositionAuthorizer _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    private readonly IProcessDispositionAuditSink _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly ProcessCheckpointStore _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
    private readonly ProcessSweepStore _sweeps = sweeps ?? throw new ArgumentNullException(nameof(sweeps));
    private readonly TimeSpan _operationTimeout = options?.Value.OperationTimeout ?? TimeSpan.FromSeconds(30);

    /// <summary>Revalidates a previously published source only after authentication, authorization, and durable audit.</summary>
    /// <returns>True when source mutation completed; false when an active owner requires reconciliation to finish it.</returns>
    public async Task<bool> RevalidateAsync(ClaimsPrincipal principal, ProcessSource source, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        source.Validate();
        if (principal.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Process disposition is not authorized.");
        using var authorizationDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        authorizationDeadline.CancelAfter(_operationTimeout);
        if (!await _authorizer.AuthorizeAsync(principal, source, authorizationDeadline.Token)
            .WaitAsync(_operationTimeout, cancellationToken).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("Process disposition is not authorized.");
        }

        if (!await _checkpoints.ContainsCandidateAsync(source, cancellationToken).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("Process disposition is not authorized.");
        }

        using var auditDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        auditDeadline.CancelAfter(_operationTimeout);
        await _audit.RecordAsync(principal, source, reason, auditDeadline.Token)
            .WaitAsync(_operationTimeout, cancellationToken).ConfigureAwait(false);
        long preparedGeneration = await _sweeps.PrepareDispositionAsync(source, cancellationToken).ConfigureAwait(false);
        ProcessPendingDisposition? claim = await _sweeps.ClaimDispositionAsync(source, preparedGeneration, cancellationToken).ConfigureAwait(false);
        if (claim is null) return false;
        if (!await _checkpoints.RevalidateAsync(claim, cancellationToken).ConfigureAwait(false))
        {
            await _sweeps.ReleaseDispositionClaimAsync(claim, cancellationToken).ConfigureAwait(false);
            return false;
        }
        return await _sweeps.MarkDispositionAppliedAsync(claim, cancellationToken).ConfigureAwait(false);
    }
}
