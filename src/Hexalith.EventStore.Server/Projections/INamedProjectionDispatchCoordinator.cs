using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Coordinates pre-admitted named projection persistence for normal delivery only.</summary>
internal interface INamedProjectionDispatchCoordinator {
    /// <summary>Delivers a validated complete readable prefix without retaining a second stored-envelope array.</summary>
    Task<bool> TryDispatchHeadAsync(AggregateIdentity identity, DomainServiceRegistration registration,
        long headSequence, string headMessageId, ProjectionEventDto[] events, CancellationToken cancellationToken);

    /// <summary>Delivers only the explicitly unguarded ordinary scopes from the same verified catalog snapshot.</summary>
    Task<bool> TryDispatchCompatibleAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        long headSequence, string headMessageId, ProjectionEventDto[] events, IReadOnlyList<string> routes,
        CancellationToken cancellationToken);

    /// <summary>Checks whether every named route is either streamed full replay or fenced shared epoch delivery.</summary>
    Task<bool> SupportsMappedOnlyStreamedDeliveryAsync(
        AggregateIdentity identity, DomainServiceRegistration registration, CancellationToken cancellationToken);

    /// <summary>Delivers one frozen actor head using bounded source pages and durable named route state.</summary>
    Task<bool> TryDispatchMappedOnlyAsync(
        AggregateIdentity identity, DomainServiceRegistration registration,
        long sourceHeadSequence, string headMessageId, CancellationToken cancellationToken);

    /// <summary>Checks the verified mapped-only streamed capability for a full-replay operation.</summary>
    Task<bool> SupportsMappedOnlyStreamedRebuildAsync(
        AggregateIdentity identity, DomainServiceRegistration registration,
        string projectionName, CancellationToken cancellationToken);

    /// <summary>Uploads bounded authoritative pages and stages the exact mapped request.</summary>
    Task<ProjectionMappedReplayStageResult> StageMappedOnlyStreamedRebuildAsync(
        AggregateIdentity identity, DomainServiceRegistration registration, string operationId,
        long sourceHead, Func<Stream, CancellationToken, Task<string?>> writeSource,
        CancellationToken cancellationToken);

    /// <summary>Completes one previously staged bounded mapped request.</summary>
    Task<NamedProjectionRebuildResult> InvokeMappedOnlyStreamedRebuildAsync(
        AggregateIdentity identity, DomainServiceRegistration registration,
        ProjectionDispatchRequest header, ProjectionMappedReplayAction action,
        CancellationToken cancellationToken);
    /// <summary>Acquires every catalog-required rebuild lifecycle before the stream boundary is frozen.</summary>
    Task<NamedProjectionRebuildResult> AcquireRebuildLifecyclesAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        string operationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts coordinated full-prefix rebuild promotion when an exact verified catalog binding exists.
    /// </summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="registration">The resolved exact domain-service registration.</param>
    /// <param name="projectionEvents">The complete protected-data-readable event prefix.</param>
    /// <param name="sourceHeadSequence">The authoritative owner head read from the aggregate actor.</param>
    /// <param name="operationId">The stable rebuild operation identity.</param>
    /// <param name="cancellationToken">Propagates rebuild cancellation.</param>
    /// <returns>A structured ownership and per-route durable outcome.</returns>
    Task<NamedProjectionRebuildResult> TryRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        long sourceHeadSequence,
        string operationId,
        CancellationToken cancellationToken);

    /// <summary>Commits previously staged named rebuild candidates.</summary>
    Task<NamedProjectionRebuildResult> CommitRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken);

    /// <summary>Aborts previously staged named rebuild candidates.</summary>
    Task<NamedProjectionRebuildResult> AbortRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken);

    /// <summary>Reads back named rebuild marker and output evidence.</summary>
    Task<NamedProjectionRebuildResult> VerifyRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken);

    /// <summary>Attempts named v2 delivery when an exact verified catalog binding exists.</summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="registration">The resolved exact domain-service registration.</param>
    /// <param name="events">The persisted event history.</param>
    /// <param name="projectionEvents">The protected-data-readable wire event history.</param>
    /// <param name="cancellationToken">Propagates delivery cancellation.</param>
    /// <returns><c>true</c> when named delivery owned the request; otherwise the caller may use v1.</returns>
    Task<bool> TryDispatchAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        EventEnvelope[] events,
        ProjectionEventDto[] projectionEvents,
        CancellationToken cancellationToken);
}
