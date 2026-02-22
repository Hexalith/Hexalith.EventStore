
using System.Text.Json;

using Dapr.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.DomainServices;
/// <summary>
/// Resolves domain service registrations from the DAPR config store.
/// Encapsulates config store lookup logic (AC #1, #7).
/// </summary>
/// <remarks>
/// <para><b>DEPLOYMENT SECURITY (Red Team H1):</b> The DAPR sidecar (port 3500) must be network-isolated
/// to the pod/container. Direct sidecar access bypasses HTTP security layers 1-4 (API gateway, JWT auth,
/// rate limiting, request validation). Use Kubernetes NetworkPolicy or equivalent to restrict sidecar access
/// to the EventStore container only.</para>
/// <para><b>DEPLOYMENT SECURITY (Red Team H2):</b> Config store write access must be restricted to
/// admin service accounts only. Config store poisoning can redirect domain service registrations to
/// malicious endpoints. Use DAPR component-level RBAC or infrastructure-level access controls to ensure
/// only authorized operators can write to the config store.</para>
/// <para><b>BLOCKING:</b> Story 5.1 (DAPR ACLs) is required before production multi-tenant deployments.
/// Without DAPR app-level access policies, any sidecar-accessible service can invoke any other service.</para>
/// </remarks>
public class DomainServiceResolver(
    DaprClient daprClient,
    IOptions<DomainServiceOptions> options,
    ILogger<DomainServiceResolver> logger) : IDomainServiceResolver {
    /// <inheritdoc/>
    public async Task<DomainServiceRegistration?> ResolveAsync(
        string tenantId, string domain, string version = "v1", CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        // Normalize and validate version format (ADR-4)
        version = version.ToLowerInvariant();
        DaprDomainServiceInvoker.ValidateVersionFormat(version);

        string configKey = $"{tenantId}:{domain}:{version}";

        // Check static registrations first (local dev/testing)
        if (options.Value.Registrations.TryGetValue(configKey, out DomainServiceRegistration? staticRegistration)) {
            logger.LogDebug(
                "Resolved domain service from static registration: AppId={AppId}, Method={MethodName}, ConfigKey={ConfigKey}",
                staticRegistration.AppId,
                staticRegistration.MethodName,
                configKey);
            return staticRegistration;
        }

        logger.LogDebug(
            "Resolving domain service: ConfigKey={ConfigKey}, ConfigStore={ConfigStore}, Version={Version}",
            configKey,
            options.Value.ConfigStoreName,
            version);

        GetConfigurationResponse configResponse = await daprClient
            .GetConfiguration(
                options.Value.ConfigStoreName,
                [configKey],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!configResponse.Items.TryGetValue(configKey, out ConfigurationItem? configItem) ||
            string.IsNullOrWhiteSpace(configItem.Value)) {
            logger.LogWarning(
                "No domain service registered: Tenant={TenantId}, Domain={Domain}, Version={Version}, ConfigKey={ConfigKey}",
                tenantId,
                domain,
                version,
                configKey);
            return null;
        }

        DomainServiceRegistration? registration;
        try {
            registration = JsonSerializer.Deserialize<DomainServiceRegistration>(configItem.Value);
        }
        catch (JsonException ex) {
            logger.LogError(
                ex,
                "Failed to deserialize domain service registration: Tenant={TenantId}, Domain={Domain}, ConfigKey={ConfigKey}, RawValue={RawValue}",
                tenantId,
                domain,
                configKey,
                configItem.Value);
            throw new DomainServiceException(
                $"Domain service registration for tenant '{tenantId}', domain '{domain}' has corrupted configuration (key: '{configKey}'). Verify config store contents.",
                ex);
        }

        if (registration is null) {
            logger.LogWarning(
                "Domain service registration deserialized to null: Tenant={TenantId}, Domain={Domain}, ConfigKey={ConfigKey}",
                tenantId,
                domain,
                configKey);
            return null;
        }

        logger.LogDebug(
            "Resolved domain service: AppId={AppId}, Method={MethodName}, Tenant={TenantId}, Domain={Domain}",
            registration.AppId,
            registration.MethodName,
            tenantId,
            domain);

        return registration;
    }
}
