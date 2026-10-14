using System.Security.Cryptography;
using System.Buffers.Binary;
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
        IEnumerable<ProjectionDispatchRoute> routes)
        => Compute(appId, serviceVersion, routes, null);

    /// <summary>Computes a fingerprint that also fences the enabled ordinary-delivery subset.</summary>
    /// <param name="appId">The bound DAPR app id.</param>
    /// <param name="serviceVersion">The bound domain-service version.</param>
    /// <param name="routes">The complete named catalog, including rebuild-only routes.</param>
    /// <param name="ordinaryRoutes">The enabled ordinary subset; null retains historical all-route behavior.</param>
    /// <returns>A lowercase hexadecimal SHA-256 fingerprint.</returns>
    public static string Compute(
        string appId,
        string serviceVersion,
        IEnumerable<ProjectionDispatchRoute> routes,
        IEnumerable<ProjectionDispatchRoute>? ordinaryRoutes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        ArgumentNullException.ThrowIfNull(routes);

        ProjectionDispatchRoute[] canonicalRoutes = [.. routes
            .OrderBy(static route => route.Domain, StringComparer.Ordinal)
            .ThenBy(static route => route.ProjectionType, StringComparer.Ordinal)];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, ProjectionDispatchProtocol.Capability);
        Append(hash, ProjectionDispatchProtocol.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, appId);
        Append(hash, serviceVersion);
        Span<byte> routeCount = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(routeCount, canonicalRoutes.Length);
        hash.AppendData(routeCount);
        foreach (ProjectionDispatchRoute route in canonicalRoutes) {
            Append(hash, route.Domain);
            Append(hash, route.ProjectionType);
        }

        if (ordinaryRoutes is not null) {
            ProjectionDispatchRoute[] ordinary = [.. ordinaryRoutes
                .OrderBy(static route => route.Domain, StringComparer.Ordinal)
                .ThenBy(static route => route.ProjectionType, StringComparer.Ordinal)];
            if (ordinary.Distinct().Count() != ordinary.Length || ordinary.Any(route => !canonicalRoutes.Contains(route))) {
                throw new ArgumentException("Ordinary delivery must be a unique subset of the named catalog.", nameof(ordinaryRoutes));
            }
            if (!canonicalRoutes.SequenceEqual(ordinary)) {
                Append(hash, "ordinary-delivery-v1");
                BinaryPrimitives.WriteInt32BigEndian(routeCount, ordinary.Length);
                hash.AppendData(routeCount);
                foreach (ProjectionDispatchRoute route in ordinary) {
                    Append(hash, route.Domain);
                    Append(hash, route.ProjectionType);
                }
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value) {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
