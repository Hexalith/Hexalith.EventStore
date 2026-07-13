using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Computes deterministic fingerprints for app/version-bound named route catalogs.</summary>
public static class ProjectionRouteCatalogFingerprint {
    /// <summary>Computes a SHA-256 fingerprint from protocol binding and exact sorted routes.</summary>
    /// <param name="appId">The bound DAPR app id.</param>
    /// <param name="serviceVersion">The bound domain-service version.</param>
    /// <param name="routes">The exact named routes.</param>
    /// <returns>A lowercase hexadecimal SHA-256 fingerprint.</returns>
    public static string Compute(
        string appId,
        string serviceVersion,
        IEnumerable<ProjectionDispatchRoute> routes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        ArgumentNullException.ThrowIfNull(routes);

        var builder = new StringBuilder();
        _ = builder.Append(ProjectionDispatchProtocol.Capability).Append('\n');
        _ = builder.Append(ProjectionDispatchProtocol.Version).Append('\n');
        _ = builder.Append(appId).Append('\n');
        _ = builder.Append(serviceVersion).Append('\n');
        foreach (ProjectionDispatchRoute route in routes
            .OrderBy(static route => route.Domain, StringComparer.Ordinal)
            .ThenBy(static route => route.ProjectionType, StringComparer.Ordinal)) {
            _ = builder.Append(route.Domain).Append('\u001f').Append(route.ProjectionType).Append('\n');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
