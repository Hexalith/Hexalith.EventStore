using System.Text.Json;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Builds fixture-private discovery configuration without changing domain-state components or application IDs.</summary>
internal static class Oq8DiscoveryConfiguration
{
    /// <summary>Creates a Dapr configuration for a private local discovery registry.</summary>
    /// <param name="registryFile">The absolute fixture-owned registry path.</param>
    /// <returns>The self-hosted Dapr discovery configuration.</returns>
    internal static string Create(string registryFile)
    {
        if (!Path.IsPathFullyQualified(registryFile))
        {
            throw new ArgumentException("The discovery registry must use an absolute fixture-owned path.", nameof(registryFile));
        }

        // Dapr 1.18.2 mDNS does not scope lookups by NAMESPACE. A private registry isolates
        // discovery while preserving the production app IDs and PostgreSQL state profile.
        return $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: oq8-private-discovery
            spec:
              features:
                - name: HotReload
                  enabled: false
              nameResolution:
                component: sqlite
                version: v1
                configuration:
                  connectionString: {{JsonSerializer.Serialize(registryFile)}}
            """;
    }
}
