using Dapr.Client;

namespace Hexalith.EventStore.Configuration;

/// <summary>
/// Background service that reads per-tenant rate limit overrides from the DAPR config store
/// and periodically refreshes the in-memory configuration so the rate limiting options monitor
/// can observe changes without requiring an application restart.
/// Optional — gracefully falls back to appsettings.json values when the DAPR sidecar is unavailable
/// or <see cref="DaprClient"/> is not registered (e.g., in tests).
/// </summary>
/// <remarks>
/// Key pattern: <c>ratelimit:{tenantId}:permit-limit</c> → integer value as string.
/// The service polls DAPR every 10 seconds so config changes converge well within the
/// 60-second sliding window used by the tenant limiter.
/// </remarks>
public class DaprRateLimitConfigSync(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<DaprRateLimitConfigSync> logger) : BackgroundService {
    private const string ConfigStoreName = "configstore";
    private const string KeyPrefix = "ratelimit:";
    private const string TenantsKey = "ratelimit:tenants";
    private const string PermitLimitSuffix = ":permit-limit";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);
    private readonly Dictionary<string, string> _appliedTenantLimits = new(StringComparer.Ordinal);
    private bool _configStoreUnavailableLogged;

    /// <summary>
    /// Timeout for each DAPR config store call. Short to avoid blocking startup or refresh cycles
    /// when the sidecar is unavailable (e.g., in tests or local dev without DAPR).
    /// </summary>
    private static readonly TimeSpan DaprTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // Resolve DaprClient optionally — it may not be registered in test environments.
        DaprClient? daprClient = serviceProvider.GetService<DaprClient>();
        if (daprClient is null) {
            logger.LogWarning("DaprClient not registered. Falling back to appsettings.json rate limit values.");
            return;
        }

        await SyncTenantOverridesAsync(daprClient, stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(RefreshInterval);

        try {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) {
                await SyncTenantOverridesAsync(daprClient, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Normal shutdown.
        }
    }

    private async Task SyncTenantOverridesAsync(DaprClient daprClient, CancellationToken cancellationToken) {
        try {
            string[] tenantIds = await LoadTenantIdsAsync(daprClient, cancellationToken).ConfigureAwait(false);
            Dictionary<string, string> latestTenantLimits = await LoadTenantPermitLimitsAsync(daprClient, tenantIds, cancellationToken).ConfigureAwait(false);

            _configStoreUnavailableLogged = false;

            if (ApplyTenantOverrides(latestTenantLimits) && configuration is IConfigurationRoot configRoot) {
                configRoot.Reload();
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested) {
            logger.LogWarning(
                ex,
                "DAPR config store timeout after {TimeoutSeconds}s during rate limit sync. Falling back to appsettings.json values until the next refresh.",
                DaprTimeout.TotalSeconds);
        }
        catch (Exception ex) {
            if (cancellationToken.IsCancellationRequested && ContainsCancellation(ex)) {
                return;
            }

            // DAPR sidecar unavailable — graceful fallback to appsettings.json values.
            // This is expected in WebApplicationFactory tests and local development without DAPR.
            // Log the first occurrence at Warning, then Debug to avoid log noise.
            if (!_configStoreUnavailableLogged) {
                _configStoreUnavailableLogged = true;
                logger.LogWarning(
                    ex,
                    "DAPR config store unavailable for rate limit sync. Falling back to appsettings.json values.");
            }
            else {
                logger.LogDebug(
                    ex,
                    "DAPR config store still unavailable for rate limit sync.");
            }
        }
    }

    private bool ApplyTenantOverrides(IReadOnlyDictionary<string, string> latestTenantLimits) {
        bool changed = false;

        foreach (string removedTenant in _appliedTenantLimits.Keys.Except(latestTenantLimits.Keys, StringComparer.Ordinal).ToArray()) {
            configuration[GetTenantConfigPath(removedTenant)] = null;
            _ = _appliedTenantLimits.Remove(removedTenant);
            changed = true;

            logger.LogInformation("Removed DAPR rate limit override: Tenant={TenantId}", removedTenant);
        }

        foreach (KeyValuePair<string, string> entry in latestTenantLimits) {
            if (_appliedTenantLimits.TryGetValue(entry.Key, out string? currentValue)
                && string.Equals(currentValue, entry.Value, StringComparison.Ordinal)) {
                continue;
            }

            configuration[GetTenantConfigPath(entry.Key)] = entry.Value;
            _appliedTenantLimits[entry.Key] = entry.Value;
            changed = true;

            logger.LogInformation(
                "Applied DAPR rate limit override: Tenant={TenantId}, PermitLimit={PermitLimit}",
                entry.Key,
                entry.Value);
        }

        return changed;
    }

    private async Task<Dictionary<string, string>> LoadTenantPermitLimitsAsync(
        DaprClient daprClient,
        string[] tenantIds,
        CancellationToken cancellationToken) {
        if (tenantIds.Length == 0) {
            return [];
        }

        string[] keys = tenantIds.Select(t => $"{KeyPrefix}{t}{PermitLimitSuffix}").ToArray();
        GetConfigurationResponse tenantResponse = await FetchConfigurationAsync(daprClient, keys, cancellationToken).ConfigureAwait(false);

        var tenantLimits = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string tenantId in tenantIds) {
            string key = $"{KeyPrefix}{tenantId}{PermitLimitSuffix}";
            if (!tenantResponse.Items.TryGetValue(key, out ConfigurationItem? item) || string.IsNullOrWhiteSpace(item.Value)) {
                logger.LogWarning("DAPR rate limit override missing or empty for tenant {TenantId} (key: {ConfigKey}).", tenantId, key);
                continue;
            }

            if (!int.TryParse(item.Value, out int permitLimit) || permitLimit <= 0) {
                logger.LogWarning(
                    "Ignoring invalid DAPR rate limit override: Tenant={TenantId}, ConfigKey={ConfigKey}, RawValue={RawValue}",
                    tenantId,
                    key,
                    item.Value);
                continue;
            }

            tenantLimits[tenantId] = permitLimit.ToString();
        }

        return tenantLimits;
    }

    private static async Task<string[]> LoadTenantIdsAsync(DaprClient daprClient, CancellationToken cancellationToken) {
        // DAPR GetConfiguration requires explicit key names — we use a well-known
        // sentinel key to discover which tenants currently have overrides.
        GetConfigurationResponse response = await FetchConfigurationAsync(daprClient, [TenantsKey], cancellationToken).ConfigureAwait(false);
        if (!response.Items.TryGetValue(TenantsKey, out ConfigurationItem? tenantsItem)
            || string.IsNullOrWhiteSpace(tenantsItem.Value)) {
            return [];
        }

        return tenantsItem.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<GetConfigurationResponse> FetchConfigurationAsync(
        DaprClient daprClient,
        string[] keys,
        CancellationToken cancellationToken) {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DaprTimeout);

        return await daprClient
            .GetConfiguration(ConfigStoreName, keys, cancellationToken: timeoutCts.Token)
            .ConfigureAwait(false);
    }

    private static bool ContainsCancellation(Exception ex) =>
        ex is OperationCanceledException or TaskCanceledException
        || (ex.InnerException is not null && ContainsCancellation(ex.InnerException));

    private static string GetTenantConfigPath(string tenantId) => $"EventStore:RateLimiting:TenantPermitLimits:{tenantId}";
}
