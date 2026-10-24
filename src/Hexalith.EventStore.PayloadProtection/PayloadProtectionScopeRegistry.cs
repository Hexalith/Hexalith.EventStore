// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 1.
using System.Collections.Frozen;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Freezes the trusted set of at most 4,096 roots that operations and the worker may visit.</summary>
internal sealed class PayloadProtectionScopeRegistry
{
    private readonly FrozenDictionary<string, PayloadProtectionScope> _roots;

    /// <summary>Rejects missing, duplicate, wildcard or over-capacity enrollment.</summary>
    internal PayloadProtectionScopeRegistry(IEnumerable<PayloadProtectionScope> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var enrolled = new Dictionary<string, PayloadProtectionScope>(StringComparer.Ordinal);
        foreach (PayloadProtectionScope root in roots)
        {
            if (root is null) { throw new PayloadProtectionFormatException(); }
            try
            {
                var identity = new AggregateIdentity(root.TenantId, root.Domain, "enrollment");
                if (identity.TenantId != root.TenantId || identity.Domain != root.Domain) { throw new PayloadProtectionFormatException(); }
            }
            catch (ArgumentException) { throw new PayloadProtectionFormatException(); }
            if (root.TenantId == "*" || root.Domain == "*" || enrolled.Count == 4096
                || !enrolled.TryAdd(PayloadProtectionStateKeys.TenantDomainDigest(root.TenantId, root.Domain), root))
            {
                throw new PayloadProtectionFormatException();
            }
        }

        if (enrolled.Count == 0)
        {
            throw new PayloadProtectionFormatException();
        }

        _roots = enrolled.ToFrozenDictionary(StringComparer.Ordinal);
        Roots = Array.AsReadOnly(enrolled.Values.ToArray());
    }

    /// <summary>Gets the frozen roots in trusted enrollment order.</summary>
    internal IReadOnlyList<PayloadProtectionScope> Roots { get; }

    /// <summary>Requires exact authenticated root enrollment before admitting work.</summary>
    internal string Require(AggregateIdentity identity)
    {
        string digest = PayloadProtectionStateKeys.TenantDomainDigest(identity.TenantId, identity.Domain);
        return _roots.TryGetValue(digest, out PayloadProtectionScope? root) && root.TenantId == identity.TenantId && root.Domain == identity.Domain
            ? digest : throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch);
    }

    /// <summary>Rejects removal of any predecessor root lacking complete clearing evidence.</summary>
    internal void ValidateSuccessor(PayloadProtectionScopeRegistry successor, IReadOnlySet<string> provenClearedRoots)
    {
        foreach (string root in _roots.Keys)
        {
            if (!successor._roots.ContainsKey(root) && !provenClearedRoots.Contains(root))
            {
                throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch);
            }
        }
    }
}
