using Hexalith.EventStore.Client.Events;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Supplies only the explicit fixture domain's caller pin to command rehydration tests.</summary>
/// <param name="candidate">The pinned fixture manifest candidate.</param>
internal sealed class KeyedCandidateProvider(EventEvolutionManifestCandidate candidate) : IKeyedServiceProvider
{
    /// <inheritdoc/>
    public object? GetService(Type serviceType) => null;

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
        => serviceType == typeof(EventEvolutionManifestCandidate) && Equals(serviceKey, "d")
            ? candidate
            : null;

    /// <inheritdoc/>
    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        => GetKeyedService(serviceType, serviceKey)
            ?? throw new InvalidOperationException($"No keyed service for {serviceType}.");
}
