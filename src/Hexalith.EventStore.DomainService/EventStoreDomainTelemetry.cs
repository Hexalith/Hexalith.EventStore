using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Convention that derives a domain module's OpenTelemetry instrument names from its (kebab-case) domain name,
/// so a domain no longer hand-declares an <c>ActivitySource</c>/<c>Meter</c> and a per-domain telemetry class
/// (Epic A5). The platform owns the naming so every domain's traces and metrics are discoverable under a
/// predictable prefix.
/// </summary>
public static class EventStoreDomainTelemetry {
    /// <summary>The common prefix for every domain module's telemetry instrument names.</summary>
    public const string Prefix = "Hexalith.EventStore.Domain";

    /// <summary>Gets the conventional <see cref="ActivitySource"/> name for a domain (e.g. <c>Hexalith.EventStore.Domain.counter</c>).</summary>
    /// <param name="domain">The kebab-case domain name.</param>
    /// <returns>The conventional activity-source name.</returns>
    public static string ActivitySourceName(string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return $"{Prefix}.{domain.Trim()}";
    }

    /// <summary>Gets the conventional <see cref="Meter"/> name for a domain. Matches <see cref="ActivitySourceName"/>.</summary>
    /// <param name="domain">The kebab-case domain name.</param>
    /// <returns>The conventional meter name.</returns>
    public static string MeterName(string domain) => ActivitySourceName(domain);

    /// <summary>Gets the conventional health-check registration name for a domain's DAPR state store.</summary>
    /// <param name="domain">The kebab-case domain name.</param>
    /// <returns>The conventional health-check name (e.g. <c>dapr-statestore-counter</c>).</returns>
    public static string StateStoreHealthCheckName(string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return $"dapr-statestore-{domain.Trim()}";
    }
}
