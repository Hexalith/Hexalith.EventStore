using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>Registers an explicit domain event-evolution manifest candidate.</summary>
public static class EventEvolutionServiceCollectionExtensions
{
    /// <summary>Registers one explicit JSON upcaster for the host's immutable startup registry.</summary>
    public static IServiceCollection AddEventPayloadUpcaster<T>(this IServiceCollection services)
        where T : IEventPayloadUpcaster
    {
        ArgumentNullException.ThrowIfNull(services);
        GetOrCreateRegistration(services).AddUpcaster(typeof(T));
        return services;
    }

    /// <summary>Registers an event type consumed by a projection handler that has no typed Apply method.</summary>
    public static IServiceCollection AddKnownEventPayload<T>(this IServiceCollection services)
        where T : IEventPayload
    {
        ArgumentNullException.ThrowIfNull(services);
        GetOrCreateRegistration(services).AddKnownType(typeof(T));
        return services;
    }

    /// <summary>Gets the host's mutable registration collection before startup.</summary>
    internal static EventPayloadEvolutionRegistration GetOrCreateRegistration(IServiceCollection services)
    {
        ServiceDescriptor? descriptor = services.FirstOrDefault(static item => item.ServiceType == typeof(EventPayloadEvolutionRegistration));
        if (descriptor?.ImplementationInstance is EventPayloadEvolutionRegistration existing)
        {
            return existing;
        }
        var registration = new EventPayloadEvolutionRegistration();
        services.AddSingleton(registration);
        services.TryAddSingleton(static provider => provider.GetRequiredService<EventPayloadEvolutionRegistration>().Build());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EventPayloadEvolutionStartupValidation>());
        return registration;
    }

    /// <summary>Validates and registers a pinned domain manifest without granting V2 readiness.</summary>
    /// <remarks>
    /// The caller must obtain the pin from the gateway's authoritative capability record.
    /// Complete reviewed dependency inventory, immutable artifact execution binding, loader observations,
    /// corpus readiness and route proof are separate requirements. Local hash and supplied-graph checks
    /// grant no readiness and provide no confinement of executing application code.
    /// Registration switches command rehydration and manual snapshot reconstruction for every aggregate
    /// in this domain to the fail-closed allow-list reader. Missing mappings, incompatible aggregate
    /// types and required catalog hops reject those reads. Stamped metadata-V1 source events remain
    /// available to the JSON payload upcaster path. The production pin binds
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
