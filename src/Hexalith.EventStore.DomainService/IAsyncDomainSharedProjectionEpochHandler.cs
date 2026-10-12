using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.DomainService;

/// <summary>Opts a shared rebuild handler into generation selection and durable ordinary catch-up.</summary>
public interface IAsyncDomainSharedProjectionEpochHandler : IAsyncDomainSharedProjectionRebuildHandler
{
    /// <summary>The reserved tenant segment for a truly global generation.</summary>
    const string GlobalTenantSegment = "global-index";

    /// <summary>Gets whether generation-selected readers and writers have been activated.</summary>
    bool EpochEnabled { get; }

    /// <summary>Uses one global generation across tenant rebuilds for a pre-tenant lookup route.</summary>
    bool GlobalEpochScope => false;

    /// <summary>Maps a tenant-owned aggregate identifier into the epoch's source namespace.</summary>
    string SourceStreamKey(string tenantId, string aggregateId)
        => GlobalEpochScope ? new AggregateIdentity(tenantId, Domain, aggregateId).ActorId : aggregateId;

    /// <summary>Gets the registered writer identity used by named ordinary dispatch.</summary>
    string OrdinaryWriterId { get; }

    /// <summary>
    /// Gets an optional store-local cross-tenant discovery index. Named ordinary deliveries
    /// prepare this tenant's append-only membership before acknowledging their journal entry.
    /// </summary>
    string? ControlIndexName => null;

    /// <summary>Names the tenant/family and all writers required before inventory capture.</summary>
    SharedProjectionScope CreateScope(string tenantId);

    /// <summary>Folds a journaled delivery into logical mutations for the selected generation.</summary>
    Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(
        SharedProjectionDelivery delivery,
        long generation,
        SharedProjectionEpochCoordinator coordinator,
        CancellationToken cancellationToken);
}
