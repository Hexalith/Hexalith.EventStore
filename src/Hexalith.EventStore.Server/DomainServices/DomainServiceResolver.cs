namespace Hexalith.EventStore.Server.DomainServices;

using System.Text.Json;

using Dapr.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Resolves domain service registrations from the DAPR config store.
/// Encapsulates config store lookup logic (AC #1, #7).
/// </summary>
public class DomainServiceResolver(
    DaprClient daprClient,
    IOptions<DomainServiceOptions> options,
    ILogger<DomainServiceResolver> logger) : IDomainServiceResolver
{
    /// <inheritdoc/>
    public async Task<DomainServiceRegistration?> ResolveAsync(
        string tenantId, string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        string configKey = $"{tenantId}:{domain}:service";

        logger.LogDebug(
            "Resolving domain service: ConfigKey={ConfigKey}, ConfigStore={ConfigStore}",
            configKey,
            options.Value.ConfigStoreName);

        GetConfigurationResponse configResponse = await daprClient
            .GetConfiguration(
                options.Value.ConfigStoreName,
                [configKey],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!configResponse.Items.TryGetValue(configKey, out ConfigurationItem? configItem) ||
            string.IsNullOrWhiteSpace(configItem.Value))
        {
            logger.LogWarning(
                "No domain service registered: Tenant={TenantId}, Domain={Domain}, ConfigKey={ConfigKey}",
                tenantId,
                domain,
                configKey);
            return null;
        }

        DomainServiceRegistration? registration = JsonSerializer.Deserialize<DomainServiceRegistration>(configItem.Value);

        logger.LogDebug(
            "Resolved domain service: AppId={AppId}, Method={MethodName}, Tenant={TenantId}, Domain={Domain}",
            registration?.AppId,
            registration?.MethodName,
            tenantId,
            domain);

        return registration;
    }
}
