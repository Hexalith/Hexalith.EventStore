using Hexalith.EventStore.Client.Events;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>Registers an explicit domain event-evolution manifest candidate.</summary>
public static class EventEvolutionServiceCollectionExtensions
{
    /// <summary>Validates and registers a pinned domain manifest without granting V2 readiness.</summary>
    /// <remarks>
    /// The caller must obtain the pin from the gateway's authoritative capability record.
    /// Resolved dependency closure, loader capture, corpus readiness and route proof are separate requirements.
    /// Registration switches command rehydration and manual snapshot reconstruction for every aggregate
    /// in this domain to the fail-closed allow-list reader. Missing mappings, incompatible aggregate
    /// types, versioned sources and required upcast hops reject those reads. The production pin binds
    /// no upcasters and runs no registered schema or identity validators.
    /// </remarks>
    /// <param name="services">The target service collection.</param>
    /// <param name="domain">The exact domain name carried by every registry row.</param>
    /// <param name="encodedRows">The bounded, exact registry manifest rows.</param>
    /// <param name="pinnedFingerprint">The gateway-pinned lower-case registry fingerprint.</param>
    /// <param name="referencedManifestBytes">Other manifest bytes charged to the same admission budget.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreEventEvolutionManifestCandidate(
        this IServiceCollection services,
        string domain,
        IReadOnlyList<ReadOnlyMemory<byte>> encodedRows,
        string pinnedFingerprint,
        long referencedManifestBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(EventEvolutionManifestCandidate)
            && descriptor.IsKeyedService
            && string.Equals(descriptor.ServiceKey as string, domain, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("A domain event-evolution manifest candidate is already registered.");
        }

        var candidate = new EventEvolutionManifestCandidate(domain, encodedRows, pinnedFingerprint, referencedManifestBytes);
        services.AddKeyedSingleton(domain, candidate);
        return services;
    }
}
