using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>
/// Guarded-transaction authority. It authorizes the current cell, lookup, commit, and receipt.
/// Restored absence is not a fresh empty cell, and a missing installation denies every call.
/// </summary>
public sealed class GuardedStateTransactionAuthority : IGuardedStateTransactionAuthority
{
    private readonly IGuardedStateInstallation? _installation;

    /// <summary>Creates the authority. Production passes no installation.</summary>
    /// <param name="installation">Injected cell installation.</param>
    public GuardedStateTransactionAuthority(IGuardedStateInstallation? installation = null)
    {
        _installation = installation;
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeReadAsync(GuardedStateTransactionTarget target, string cellId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await CurrentAsync(target, cancellationToken).ConfigureAwait(false) || string.IsNullOrWhiteSpace(cellId))
        {
            return false;
        }

        try
        {
            return await _installation!.AuthorizeReadAsync(target, cellId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeLookupAsync(GuardedStateTransactionTarget target, string operationId, string logicalIntentDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await CurrentAsync(target, cancellationToken).ConfigureAwait(false) || string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(logicalIntentDigest))
        {
            return false;
        }

        try
        {
            return await _installation!.AuthorizeLookupAsync(target, operationId, logicalIntentDigest, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<GuardedStateTransactionTarget?> GetCurrentAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_installation is null || string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        try
        {
            GuardedStateTransactionTarget? target = await _installation.GetCurrentAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return target is not null && target.TenantId == tenantId ? target : null;
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
    public async Task<bool> AuthorizeAsync(GuardedStateTransactionTarget target, GuardedStateCommitRequest request, string requestDigest, string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target is null || !await CurrentAsync(target, cancellationToken).ConfigureAwait(false) || request is null || request.TenantId != target.TenantId
            || string.IsNullOrWhiteSpace(requestDigest) || string.IsNullOrWhiteSpace(operation))
        {
            return false;
        }

        try
        {
            return await _installation!.AuthorizeAsync(target, request, requestDigest, operation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateCellAsync(GuardedStateTransactionTarget target, GuardedStateCell? cell, string cellId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target is null || !await CurrentAsync(target, cancellationToken).ConfigureAwait(false) || string.IsNullOrWhiteSpace(cellId))
        {
            return false;
        }

        try
        {
            GuardedCellHistory history = await _installation!.ReadCellAsync(target, cellId, cell, cancellationToken).ConfigureAwait(false);
            if (cell is null)
            {
                return history == GuardedCellHistory.NeverWritten;
            }

            return history == GuardedCellHistory.Occupied
                && cell.TenantId == target.TenantId
                && cell.InstallationId == target.InstallationId
                && cell.CellId == cellId
                && cell.Revision > 0
                && cell.Value is { Length: > 0 };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateReceiptAsync(GuardedStateTransactionTarget target, GuardedStateCommitReceipt receipt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target is null || !await CurrentAsync(target, cancellationToken).ConfigureAwait(false) || receipt is null
            || receipt.TenantId != target.TenantId || receipt.InstallationId != target.InstallationId)
        {
            return false;
        }

        try
        {
            return await _installation!.ValidateReceiptAsync(target, receipt, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> CurrentAsync(GuardedStateTransactionTarget target, CancellationToken cancellationToken)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.TenantId))
        {
            return false;
        }

        GuardedStateTransactionTarget? current = await GetCurrentAsync(target.TenantId, cancellationToken).ConfigureAwait(false);
        return current == target;
    }
}
