// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 3; base sections 10 and 15.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Shares admission, cancellation and clearing evidence across every engine-owned read/write/backend operation.</summary>
internal sealed class PayloadLocalOperations
{
    private readonly object _gate = new();
    private readonly Dictionary<PayloadLocalOperation, (string Root, string Scope)> _active = [];
    private readonly HashSet<string> _knownScopes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blockedScopes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blockedRoots = new(StringComparer.Ordinal);

    /// <summary>Admits bounded local work before any key material is obtained.</summary>
    internal PayloadLocalOperation Begin(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string root = PayloadProtectionStateKeys.TenantDomainDigest(identity.TenantId, identity.Domain);
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        lock (_gate)
        {
            AssertAllowed(root, scope);
            if (_active.Count >= PayloadProtectionOperationalLimits.ScopeMetadata) { throw Capacity(); }
            var operation = new PayloadLocalOperation(cancellationToken, Released);
            _active.Add(operation, (root, scope));
            return operation;
        }
    }

    /// <summary>Blocks fresh local admission before obtaining distributed authority.</summary>
    internal void AssertAllowed(AggregateIdentity identity)
    {
        lock (_gate) { AssertAllowed(PayloadProtectionStateKeys.TenantDomainDigest(identity.TenantId, identity.Domain), PayloadProtectionStateKeys.ScopeDigest(identity)); }
    }

    /// <summary>Permanently blocks the scope, requests cancellation and waits for actual foreground and abandoned-result clearing.</summary>
    internal ValueTask DrainScopeAsync(string scope, CancellationToken cancellationToken = default) => DrainAsync(null, scope, cancellationToken);
    /// <summary>Permanently blocks the enrolled root for graceful member retirement.</summary>
    internal ValueTask DrainRootAsync(string root, CancellationToken cancellationToken = default) => DrainAsync(root, null, cancellationToken);

    private async ValueTask DrainAsync(string? root, string? scope, CancellationToken cancellationToken)
    {
        PayloadLocalOperation[] operations;
        lock (_gate)
        {
            if (root is not null)
            {
                _ = PayloadProtectionStateKeys.Sha(root);
                if (_blockedRoots.Count >= PayloadProtectionOperationalLimits.ScopeMetadata && !_blockedRoots.Contains(root)) { throw Capacity(); }
                _blockedRoots.Add(root);
            }
            if (scope is not null) { AdmitScope(PayloadProtectionStateKeys.Sha(scope)); _blockedScopes.Add(scope); }
            operations = _active.Where(p => root is not null ? p.Value.Root == root : p.Value.Scope == scope).Select(p => p.Key).ToArray();
        }
        Task[] cancellations = operations.Select(operation => operation.CancelAsync()).ToArray();
        await Task.WhenAll(cancellations.Concat(operations.Select(operation => operation.Cleared))).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
    private void AssertAllowed(string root, string scope)
    {
        if (_blockedRoots.Contains(root) || _blockedScopes.Contains(scope)) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted); }
        AdmitScope(scope);
    }
    private void AdmitScope(string scope)
    {
        if (_knownScopes.Count >= PayloadProtectionOperationalLimits.ScopeMetadata && !_knownScopes.Contains(scope)) { throw Capacity(); }
        _knownScopes.Add(scope);
    }
    private static PayloadProtectionStateException Capacity() => new(UnreadableProtectedDataReason.ProviderUnavailable, isAdmissionLimit: true);
    private void Released(PayloadLocalOperation operation)
    {
        lock (_gate) { _active.Remove(operation); }
    }
}
