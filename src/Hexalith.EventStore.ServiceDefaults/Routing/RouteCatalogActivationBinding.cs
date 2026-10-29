using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Separate lifecycle v1 binding; envelope v1/v2 canonicalization remains exclusively Contracts-owned.</summary>
public static class RouteCatalogActivationBinding
{
    /// <summary>Hashes all lifecycle inputs, including the exact predecessor and sorted required host identities.</summary>
    public static string ComputeDigest(RouteCatalogActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(activation.CanonicalCatalog, activation.RootDigest);
        if (envelope.SchemaVersion != 2 || string.IsNullOrWhiteSpace(activation.AttemptId)
            || string.IsNullOrWhiteSpace(activation.Owner) || activation.RequiredHosts is not { Count: > 0 and <= 256 }
            || activation.RequiredHosts.Any(h => h is null || string.IsNullOrWhiteSpace(h.HostId)
                || string.IsNullOrWhiteSpace(h.AppId) || !Digest(h.InventoryDigest) || !Digest(h.TopologyDigest)
                || string.IsNullOrWhiteSpace(h.AuthenticationFingerprint))
            || activation.RequiredHosts.Select(h => h.HostId).Distinct(StringComparer.Ordinal).Count() != activation.RequiredHosts.Count
            || activation.RequiredHosts.Select(h => h.AppId).Distinct(StringComparer.Ordinal).Count() != activation.RequiredHosts.Count
            || (activation.PredecessorRootDigest is not null && !Digest(activation.PredecessorRootDigest))
            || (activation.AuxiliaryOperations is { } auxiliary && (auxiliary.Any(a => a is null
                || !activation.RequiredHosts.Any(h => h.AppId == a.AppId) || string.IsNullOrWhiteSpace(a.Method)
                || string.IsNullOrWhiteSpace(a.Operation) || string.IsNullOrWhiteSpace(a.Domain))
                || auxiliary.Distinct().Count() != auxiliary.Count))
            || envelope.Routes.Any(r => !activation.RequiredHosts.Any(h => h.AppId == r.AppId))
            || envelope.Idempotency.Any(i => !activation.RequiredHosts.Any(h => h.AppId == i.ConsumerAppId)))
        {
            throw new RouteCatalogUnavailableException("Incomplete or ambiguous activation identity or required host set.");
        }
        var capabilities = activation.ProjectionCapabilities ?? [];
        if (capabilities.Any(c => c is null || string.IsNullOrWhiteSpace(c.Fingerprint) || c.ProjectionTypes is not { Count: > 0 }
            || c.ProjectionTypes.Distinct(StringComparer.Ordinal).Count() != c.ProjectionTypes.Count
            || !activation.RequiredHosts.Any(h => h.AppId == c.AppId)
            || c.ProjectionTypes.Any(type => !envelope.Declarations.Any(d => d.Kind == "projection" && d.Domain == c.Domain && d.ProjectionType == type
                && envelope.Routes.Any(r => r.RouteId == d.RouteId && r.AppId == c.AppId && r.ContractVersion == c.ContractVersion && r.Method == "project/v2"))))
            || capabilities.Select(c => (c.AppId, c.Domain, c.ContractVersion)).Distinct().Count() != capabilities.Count)
            throw new RouteCatalogUnavailableException("Named projection capabilities disagree with declared executable targets.");
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            purpose = "route-catalog-activation", version = 1,
            activation.AttemptId, activation.RootDigest, activation.PredecessorRootDigest,
            envelope.Generation, envelope.Profile, envelope.RouteFacetDigest, envelope.IdempotencyFacetDigest,
            activation.Owner, hosts = activation.RequiredHosts.OrderBy(h => h.HostId, StringComparer.Ordinal).ToArray(),
            auxiliary = (activation.AuxiliaryOperations ?? []).OrderBy(a => a.AppId, StringComparer.Ordinal)
                .ThenBy(a => a.Domain, StringComparer.Ordinal).ThenBy(a => a.Method, StringComparer.Ordinal).ThenBy(a => a.Operation, StringComparer.Ordinal).ToArray(),
            capabilities = capabilities.OrderBy(c => c.AppId, StringComparer.Ordinal).ThenBy(c => c.Domain, StringComparer.Ordinal)
                .ThenBy(c => c.ContractVersion, StringComparer.Ordinal).Select(c => c with { ProjectionTypes = c.ProjectionTypes.Order(StringComparer.Ordinal).ToArray() }).ToArray(),
        })));
    }

    private static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
