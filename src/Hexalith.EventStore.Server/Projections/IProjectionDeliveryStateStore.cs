using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Internal ETag-authoritative projection delivery state-store seam.</summary>
internal interface IProjectionDeliveryStateStore {
    /// <summary>Reads and classifies one in-place projection delivery row.</summary>
    Task<ProjectionDeliveryStateReadResult> ReadAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default);

    /// <summary>Conditionally writes one current delivery row.</summary>
    Task<bool> TrySaveAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryState state,
        string etag,
        CancellationToken cancellationToken = default);

    /// <summary>Conditionally writes a hydrated delivery row and its operator evidence atomically.</summary>
    Task<bool> TrySaveWithReconciliationAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryState state,
        ProjectionDeliveryReconciliationWork work,
        string etag,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the store-global writer protocol marker.</summary>
    Task<ProjectionDeliveryWriterProtocol?> ReadWriterProtocolAsync(CancellationToken cancellationToken = default);

    /// <summary>Activates the store-global v2 writer protocol by first-write concurrency.</summary>
    Task<bool> TryActivateWriterProtocolAsync(
        ProjectionDeliveryWriterProtocol marker,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts payload-free reconciliation work for one scope.</summary>
    Task RecordReconciliationAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryReconciliationWork work,
        CancellationToken cancellationToken = default);
}
