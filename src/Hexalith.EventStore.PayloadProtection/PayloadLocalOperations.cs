// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; amendment section 3; base sections 10 and 15.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Shares admission, cancellation and clearing evidence across every engine-owned read/write/backend operation.</summary>
internal sealed class PayloadLocalOperations
{
    private readonly object _gate = new();
    private readonly Dictionary<PayloadLocalOperation, (string Root, string Scope)> _active = [];
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
            if (_active.Count >= 65536) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
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
            if (root is not null) { _blockedRoots.Add(PayloadProtectionStateKeys.Sha(root)); }
            if (scope is not null) { _blockedScopes.Add(PayloadProtectionStateKeys.Sha(scope)); }
            operations = _active.Where(p => root is not null ? p.Value.Root == root : p.Value.Scope == scope).Select(p => p.Key).ToArray();
        }
        foreach (PayloadLocalOperation operation in operations) { operation.Cancel(); }
        await Task.WhenAll(operations.Select(o => o.Cleared)).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
    private void AssertAllowed(string root, string scope)
    {
        if (_blockedRoots.Contains(root) || _blockedScopes.Contains(scope)) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted); }
    }
    private void Released(PayloadLocalOperation operation)
    {
        lock (_gate) { _active.Remove(operation); }
    }
}
