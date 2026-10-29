using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.Aspire;

/// <summary>Compares an explicit Development fixture to its actual host and ACL inventory, without certifying unfinished deployed topology.</summary>
public static class RouteCatalogTopologyGuard
{
    /// <summary>Validates complete host enrollment and each retained effective Dapr input.</summary>
    public static void ValidateDevelopmentFixture(RouteCatalogActivation activation, IReadOnlyCollection<string> appIds,
        ReadOnlyMemory<byte> component, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> configurations)
    {
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentNullException.ThrowIfNull(appIds);
        ArgumentNullException.ThrowIfNull(configurations);
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(activation.CanonicalCatalog, activation.RootDigest);
        if (envelope.Profile != "Development" || appIds.Distinct(StringComparer.Ordinal).Count() != appIds.Count
            || !appIds.Order(StringComparer.Ordinal).SequenceEqual(activation.RequiredHosts.Select(h => h.AppId).Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || !configurations.Keys.Order(StringComparer.Ordinal).SequenceEqual(appIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new RouteCatalogValidationException([new("$.appHost", "unfinished-or-drifting-required-host-topology")]);
        foreach (string appId in appIds)
            RouteCatalogTopologyValidator.ValidateDevelopment(component, configurations[appId], envelope, activation.RequiredHosts, appId, activation.AuxiliaryOperations);
    }

    /// <summary>The general repository AppHost has not qualified Stories 5.6/5.7 and cannot activate a catalog from declaration delivery.</summary>
    public static void RefuseUnqualifiedActivation(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!string.IsNullOrWhiteSpace(configuration["EventStore:RouteCatalog:ActivationFile"]))
            throw new InvalidOperationException("This AppHost's component/sidecar and deployed ACL prerequisites remain unqualified. Use the separate owned Development catalog fixture; production activation belongs to its Platform owner.");
    }
}
