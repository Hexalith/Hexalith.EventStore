using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Supplies the caller pin and reconstructor used by addressed manual-snapshot tests.</summary>
/// <param name="candidate">The explicit fixture domain's event-evolution manifest pin.</param>
/// <param name="reconstructor">The domain-backed aggregate state reconstructor.</param>
internal sealed class KeyedEvolutionProvider(
    EventEvolutionManifestCandidate candidate,
    DaprAggregateStateReconstructor reconstructor) : IKeyedServiceProvider
{
    /// <inheritdoc/>
    public object? GetService(Type serviceType)
        => serviceType == typeof(IAggregateStateReconstructor) ? reconstructor : null;

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
