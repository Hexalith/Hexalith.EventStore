using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Qualifies logical Dapr equivalence without replacing independent physical host readiness or leases.</summary>
internal static class RouteCatalogReplicaPolicy
{
    /// <summary>Requires equal compiled inventories, actual executable bytes and authentication policies; physical coordinates may differ.</summary>
    internal static bool Equivalent(IEnumerable<RouteCatalogHostIdentity> hosts)
    {
        var group = hosts.ToArray();
        return group.Length == 1 || group.Length > 1
            && group.Select(host => host.HostId).Distinct(StringComparer.Ordinal).Count() == group.Length
            && group.All(host => host.ExecutableDigest is { Length: 64 } && host.ExecutableDigest.All(char.IsAsciiHexDigitLower))
            && group.Select(host => (host.InventoryDigest, host.AuthenticationFingerprint, host.ExecutableDigest)).Distinct().Count() == 1;
    }
}
