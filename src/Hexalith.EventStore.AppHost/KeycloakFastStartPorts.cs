// <copyright file="KeycloakFastStartPorts.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.AppHost;

using System.Globalization;

/// <summary>
/// Resolves and validates the fixed host ports used by the experimental Keycloak dev fast-start
/// (opt-in via <c>KeycloakPersistent=true</c>). The persistent Keycloak container pins its
/// <c>http</c>/<c>management</c> endpoints proxyless to deterministic host ports so DCP's
/// container-reuse <c>lifecycle-key</c> stays byte-stable across runs. Those ports are configurable
/// so a collision (a desktop app, a second topology) can be relocated without editing source.
/// <para>
/// Hardening here is intentionally <b>configuration-driven only</b>: it validates the configured
/// values and fails fast with an actionable message. It does NOT probe whether a port is currently
/// free — in reuse mode the warm container from the previous run is legitimately still listening on
/// these ports, so a free-port probe would mistake successful reuse for a collision (and
/// auto-reassigning would churn the lifecycle-key and defeat reuse entirely).
/// </para>
/// </summary>
internal static class KeycloakFastStartPorts {
    /// <summary>Default proxyless host port for Keycloak's <c>http</c> endpoint (avoids EventStore's 8080).</summary>
    internal const int DefaultHttpPort = 8180;

    /// <summary>Default proxyless host port for Keycloak's <c>management</c> endpoint.</summary>
    internal const int DefaultManagementPort = 8543;

    /// <summary>EventStore's app port; Keycloak's fixed ports must not collide with it.</summary>
    internal const int ReservedEventStoreAppPort = 8080;

    /// <summary>Configuration key overriding <see cref="DefaultHttpPort"/>.</summary>
    internal const string HttpPortKey = "KeycloakHttpPort";

    /// <summary>Configuration key overriding <see cref="DefaultManagementPort"/>.</summary>
    internal const string ManagementPortKey = "KeycloakManagementPort";

    /// <summary>
    /// Resolves the http/management host ports from their configured raw values, validating each is a
    /// distinct integer in <c>1..65535</c> that does not collide with the EventStore app port. Validation
    /// is configuration-only — it does NOT probe whether a port is currently free (see the class remarks).
    /// </summary>
    /// <param name="httpPortRaw">Raw configured value for <see cref="HttpPortKey"/> (null/blank ⇒ default).</param>
    /// <param name="managementPortRaw">Raw configured value for <see cref="ManagementPortKey"/> (null/blank ⇒ default).</param>
    /// <returns>The validated http and management host ports.</returns>
    /// <exception cref="DistributedApplicationException">Thrown with an actionable message when any value is invalid.</exception>
    internal static (int HttpPort, int ManagementPort) Resolve(string? httpPortRaw, string? managementPortRaw) {
        int httpPort = ParsePort(HttpPortKey, httpPortRaw, DefaultHttpPort);
        int managementPort = ParsePort(ManagementPortKey, managementPortRaw, DefaultManagementPort);

        if (httpPort == managementPort) {
            throw Invalid(
                $"'{HttpPortKey}' and '{ManagementPortKey}' must be different ports, but both resolved to {httpPort}. "
                + "Set them to two distinct free ports.");
        }

        if (httpPort == ReservedEventStoreAppPort) {
            throw Invalid(
                $"'{HttpPortKey}' ({httpPort}) collides with the reserved EventStore app port {ReservedEventStoreAppPort}. "
                + $"Set '{HttpPortKey}' to a different free port.");
        }

        if (managementPort == ReservedEventStoreAppPort) {
            throw Invalid(
                $"'{ManagementPortKey}' ({managementPort}) collides with the reserved EventStore app port {ReservedEventStoreAppPort}. "
                + $"Set '{ManagementPortKey}' to a different free port.");
        }

        return (httpPort, managementPort);
    }

    private static int ParsePort(string key, string? raw, int defaultPort) {
        string trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) {
            return defaultPort;
        }

        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)
            || port is < 1 or > 65535) {
            throw Invalid(
                $"'{key}' must be an integer between 1 and 65535, but was '{trimmed}'. "
                + $"Set '{key}' to a free port, or unset it to use the default {defaultPort}.");
        }

        return port;
    }

    private static DistributedApplicationException Invalid(string detail)
        => new($"Invalid Keycloak dev fast-start port configuration: {detail}");
}
