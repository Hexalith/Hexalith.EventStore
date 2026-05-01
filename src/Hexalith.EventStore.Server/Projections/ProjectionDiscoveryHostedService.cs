
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// One-shot startup service that discovers projection-capable domain services
/// from existing <see cref="DomainServiceOptions.Registrations"/> and logs
/// the configured projection mode for each domain.
/// </summary>
/// <remarks>
/// Convention-based discovery: any domain service registered in
/// <c>EventStore:DomainServices:Registrations</c> is automatically
/// a potential projection source. No separate projection registration
/// is needed. The refresh interval from <see cref="ProjectionOptions"/>
/// controls whether projections use immediate (fire-and-forget) or
/// polling mode.
/// <para><b>Fail-fast:</b> Accessing <see cref="ProjectionOptions"/> triggers
/// ValidateOnStart validation. Invalid configuration (negative intervals, empty
/// domain keys) will intentionally crash startup — this is fail-fast by design.</para>
/// </remarks>
public sealed partial class ProjectionDiscoveryHostedService(
    IOptions<DomainServiceOptions> domainServiceOptions,
    IOptions<ProjectionOptions> projectionOptions,
    ILogger<ProjectionDiscoveryHostedService> logger) : IHostedService {
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) {
        DomainServiceOptions dsOptions = domainServiceOptions.Value;
        ProjectionOptions pOptions = projectionOptions.Value;

        // Extract unique domain names from static registrations.
        // Registration keys are "{tenant}:{domain}:{version}" or "{tenant}|{domain}|{version}".
        var discoveredDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in dsOptions.Registrations.Keys) {
            string domain = ExtractDomain(key);
            if (!string.IsNullOrWhiteSpace(domain)) {
                _ = discoveredDomains.Add(domain);
            }
            else {
                Log.MalformedRegistrationKey(logger, key);
            }
        }

        if (discoveredDomains.Count == 0) {
            Log.NoRegistrations(logger);
            return Task.CompletedTask;
        }

        // Log projection mode for each discovered domain
        foreach (string domain in discoveredDomains.Order()) {
            int refreshMs = pOptions.GetRefreshIntervalMs(domain);
            if (refreshMs == 0) {
                Log.DomainImmediate(logger, domain);
            }
            else {
                Log.DomainPolling(logger, domain, refreshMs);
            }
        }

        // Warn on projection config entries that reference domains without registrations
        foreach (string configuredDomain in pOptions.Domains.Keys) {
            if (!discoveredDomains.Contains(configuredDomain)) {
                Log.OrphanedConfig(logger, configuredDomain);
            }
        }

        Log.DiscoveryComplete(logger, discoveredDomains.Count, pOptions.DefaultRefreshIntervalMs);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Extracts the domain segment from a registration key.
    /// Keys use either colon or pipe separators: "{tenant}:{domain}:{version}" or "{tenant}|{domain}|{version}".
    /// </summary>
    internal static string ExtractDomain(string registrationKey) {
        bool hasColon = registrationKey.Contains(':');
        bool hasPipe = registrationKey.Contains('|');

        if (hasColon == hasPipe) {
            return string.Empty;
        }

        char separator = hasColon ? ':' : '|';
        string[] parts = registrationKey.Split(separator);
        return parts.Length == 3 ? parts[1] : string.Empty;
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1120,
            Level = LogLevel.Information,
            Message = "Projection discovery: no domain service registrations found in EventStore:DomainServices:Registrations. Projections will activate on first domain service resolution at runtime.")]
        public static partial void NoRegistrations(ILogger logger);

        [LoggerMessage(
            EventId = 1121,
            Level = LogLevel.Information,
            Message = "Projection discovery: domain '{Domain}' -> immediate mode (fire-and-forget after persistence)")]
        public static partial void DomainImmediate(ILogger logger, string domain);

        [LoggerMessage(
            EventId = 1122,
            Level = LogLevel.Information,
            Message = "Projection discovery: domain '{Domain}' -> polling mode active (RefreshIntervalMs={RefreshIntervalMs}). Projection freshness is delayed until the next poll tick and remains at-least-once.")]
        public static partial void DomainPolling(ILogger logger, string domain, int refreshIntervalMs);

        [LoggerMessage(
            EventId = 1123,
            Level = LogLevel.Warning,
            Message = "Projection configuration for domain '{Domain}' has no matching domain service registration in EventStore:DomainServices:Registrations. This configuration entry will have no effect.")]
        public static partial void OrphanedConfig(ILogger logger, string domain);

        [LoggerMessage(
            EventId = 1124,
            Level = LogLevel.Information,
            Message = "Projection discovery complete: {DomainCount} domains discovered. Default refresh interval: {DefaultRefreshIntervalMs}ms (0=immediate).")]
        public static partial void DiscoveryComplete(ILogger logger, int domainCount, int defaultRefreshIntervalMs);

        [LoggerMessage(
            EventId = 1125,
            Level = LogLevel.Warning,
            Message = "Projection discovery: malformed registration key '{RegistrationKey}'. Expected exactly 3 segments using either ':' or '|'.")]
        public static partial void MalformedRegistrationKey(ILogger logger, string registrationKey);
    }
}
