using System.Globalization;

using Aspire.Hosting;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Resolves and validates the fixed host ports used by the experimental Keycloak dev fast-start
/// (opt-in via <c>KeycloakPersistent=true</c>). The persistent Keycloak container pins its
/// <c>http</c>/<c>management</c> endpoints proxyless to deterministic host ports so DCP's
/// container-reuse <c>lifecycle-key</c> stays byte-stable across runs. Those ports are configurable
/// so a collision can be relocated without editing source.
/// </summary>
internal static class KeycloakFastStartPorts
{
    /// <summary>Default proxyless host port for Keycloak's <c>http</c> endpoint.</summary>
    internal const int DefaultHttpPort = 8180;

    /// <summary>Default proxyless host port for Keycloak's <c>management</c> endpoint.</summary>
    internal const int DefaultManagementPort = 8543;

    /// <summary>EventStore's app port; Keycloak's fixed ports must not collide with it.</summary>
    internal const int ReservedEventStoreAppPort = 8080;

    /// <summary>Configuration key overriding <see cref="DefaultHttpPort"/>.</summary>
    internal const string HttpPortKey = HexalithEventStoreSecurityOptions.DefaultHttpPortConfigurationKey;

    /// <summary>Configuration key overriding <see cref="DefaultManagementPort"/>.</summary>
    internal const string ManagementPortKey = HexalithEventStoreSecurityOptions.DefaultManagementPortConfigurationKey;

    /// <summary>
    /// Resolves the http/management host ports from their configured raw values.
    /// </summary>
    /// <param name="httpPortRaw">Raw configured value for <see cref="HttpPortKey"/>.</param>
    /// <param name="managementPortRaw">Raw configured value for <see cref="ManagementPortKey"/>.</param>
    /// <returns>The validated http and management host ports.</returns>
    internal static (int HttpPort, int ManagementPort) Resolve(string? httpPortRaw, string? managementPortRaw)
    {
        int httpPort = ParsePort(HttpPortKey, httpPortRaw, DefaultHttpPort);
        int managementPort = ParsePort(ManagementPortKey, managementPortRaw, DefaultManagementPort);

        if (httpPort == managementPort)
        {
            throw Invalid(
                $"'{HttpPortKey}' and '{ManagementPortKey}' must be different ports, but both resolved to {httpPort}. "
                + "Set them to two distinct free ports.");
        }

        if (httpPort == ReservedEventStoreAppPort)
        {
            throw Invalid(
                $"'{HttpPortKey}' ({httpPort}) collides with the reserved EventStore app port {ReservedEventStoreAppPort}. "
                + $"Set '{HttpPortKey}' to a different free port.");
        }

        if (managementPort == ReservedEventStoreAppPort)
        {
            throw Invalid(
                $"'{ManagementPortKey}' ({managementPort}) collides with the reserved EventStore app port {ReservedEventStoreAppPort}. "
                + $"Set '{ManagementPortKey}' to a different free port.");
        }

        return (httpPort, managementPort);
    }

    private static int ParsePort(string key, string? raw, int defaultPort)
    {
        string trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return defaultPort;
        }

        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)
            || port is < 1 or > 65535)
        {
            throw Invalid(
                $"'{key}' must be an integer between 1 and 65535, but was '{trimmed}'. "
                + $"Set '{key}' to a free port, or unset it to use the default {defaultPort}.");
        }

        return port;
    }

    private static DistributedApplicationException Invalid(string detail)
        => new($"Invalid Keycloak dev fast-start port configuration: {detail}");
}
