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
            if (ordinary.Select(static route => (route.Domain, route.ProjectionType)).Distinct().Count() != ordinary.Length
                || ordinary.Any(route => !canonicalRoutes.Any(candidate => candidate.Domain == route.Domain
                    && candidate.ProjectionType == route.ProjectionType))) {
                throw new ArgumentException("Ordinary delivery must be a unique subset of the named catalog.", nameof(ordinaryRoutes));
            }
            if (!canonicalRoutes.Select(static route => (route.Domain, route.ProjectionType))
                .SequenceEqual(ordinary.Select(static route => (route.Domain, route.ProjectionType)))) {
                Append(hash, "ordinary-delivery-v1");
                BinaryPrimitives.WriteInt32BigEndian(routeCount, ordinary.Length);
                hash.AppendData(routeCount);
                foreach (ProjectionDispatchRoute route in ordinary) {
                    Append(hash, route.Domain);
                    Append(hash, route.ProjectionType);
                }
            }
        }

        ProjectionDispatchRoute[] guarded = [.. canonicalRoutes.Where(static route => route.SourceGuard is not null)];
        if (guarded.Length > 0)
        {
            if (guarded.Any(static route => route.SourceGuard?.IsSupported != true))
                throw new ArgumentException("Unsupported source guard descriptor.", nameof(routes));
            Append(hash, ProjectionSourceGuardCapability.Capability);
            BinaryPrimitives.WriteInt32BigEndian(routeCount, guarded.Length);
            hash.AppendData(routeCount);
            foreach (ProjectionDispatchRoute route in guarded)
            {
                ProjectionSourceGuardCapability descriptor = route.SourceGuard!;
                Append(hash, route.Domain);
                Append(hash, route.ProjectionType);
                Append(hash, descriptor.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Append(hash, descriptor.SourceCodecVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Append(hash, descriptor.GuardEvidenceVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Append(hash, descriptor.HandlerSemantics);
                Append(hash, descriptor.SelectedOwnerProofProtocol);
                Append(hash, descriptor.PerEventBatchIdentity ?? string.Empty);
                Append(hash, descriptor.AppliedReceiptRecovery ?? string.Empty);
            }
            ProjectionDispatchRoute[] initialized = [.. guarded.Where(static route => route.SourceGuard!.InitialOwnerProtocol is not null)];
            if (initialized.Length > 0)
            {
                Append(hash, "projection-initial-owner/v1");
                BinaryPrimitives.WriteInt32BigEndian(routeCount, initialized.Length);
                hash.AppendData(routeCount);
                foreach (ProjectionDispatchRoute route in initialized)
                {
                    Append(hash, route.Domain);
                    Append(hash, route.ProjectionType);
                    Append(hash, route.SourceGuard!.InitialOwnerProtocol!);
                }
            }
            ProjectionDispatchRoute[] fallbacks = [.. guarded.Where(static route => route.SourceGuard!.CanonicalReplayProtocol is not null)];
            if (fallbacks.Length > 0)
            {
                Append(hash, "projection-canonical-fallback/v1");
                BinaryPrimitives.WriteInt32BigEndian(routeCount, fallbacks.Length);
                hash.AppendData(routeCount);
                foreach (ProjectionDispatchRoute route in fallbacks)
                {
                    Append(hash, route.Domain);
                    Append(hash, route.ProjectionType);
                    Append(hash, route.SourceGuard!.CanonicalReplayProtocol!);
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
