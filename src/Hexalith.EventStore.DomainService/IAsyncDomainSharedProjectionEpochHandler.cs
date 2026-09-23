using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Opts a shared rebuild handler into generation selection and durable ordinary catch-up.</summary>
public interface IAsyncDomainSharedProjectionEpochHandler : IAsyncDomainSharedProjectionRebuildHandler
{
    /// <summary>Gets whether generation-selected readers and writers have been activated.</summary>
    bool EpochEnabled { get; }

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
