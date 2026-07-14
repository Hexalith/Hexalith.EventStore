namespace Hexalith.EventStore.Server.Projections;

/// <summary>Durable scheduler for immediate-mode named projection retry work.</summary>
public interface IProjectionDeliveryRetryScheduler {
    /// <summary>Registers work before remote dispatch, preserving an existing stable item.</summary>
    /// <param name="workItem">The payload-free work metadata.</param>
    /// <param name="cancellationToken">Propagates persistence cancellation.</param>
    /// <returns>The existing or newly persisted stable work item.</returns>
    Task<ProjectionDeliveryRetryWorkItem> ScheduleAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default);

    /// <summary>Acquires revision-checked work and aggregate leases for one dispatch attempt.</summary>
    /// <param name="workItem">The observed work-item revision.</param>
    /// <param name="leaseOwner">An opaque unique owner for this activation.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="leaseDuration">The positive lease duration.</param>
    /// <param name="cancellationToken">Propagates persistence cancellation.</param>
    /// <returns>The claimed revision, or <c>null</c> when another replica won.</returns>
    Task<ProjectionDeliveryRetryWorkItem?> TryAcquireAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        string leaseOwner,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>Applies an update only when its revision and lease still match.</summary>
    /// <param name="workItem">The claimed replacement state.</param>
    /// <param name="cancellationToken">Propagates persistence cancellation.</param>
    /// <returns><c>true</c> when the transition committed.</returns>
    Task<bool> TryUpdateAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes work only when its revision and lease still match.</summary>
    /// <param name="workItem">The claimed work state.</param>
    /// <param name="cancellationToken">Propagates persistence cancellation.</param>
    /// <returns><c>true</c> when the transition committed.</returns>
    Task<bool> TryDeleteAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default);

    /// <summary>Updates pending, terminal, attempt, and due metadata for a claimed revision.</summary>
    /// <param name="workItem">The claimed replacement item.</param>
    /// <param name="cancellationToken">Propagates persistence cancellation.</param>
    Task UpdateAsync(ProjectionDeliveryRetryWorkItem workItem, CancellationToken cancellationToken = default);

    /// <summary>Legacy deletion shape retained for ABI compatibility; implementations reject unleased deletion.</summary>
    /// <param name="workId">The deterministic work id.</param>
    /// <param name="cancellationToken">Propagates persistence cancellation.</param>
    Task DeleteAsync(string workId, CancellationToken cancellationToken = default);

    /// <summary>Reads a bounded deterministic set of work due at or before the supplied time.</summary>
    /// <param name="dueUtc">The inclusive due time.</param>
    /// <param name="maximumCount">The positive scan bound.</param>
    /// <param name="cancellationToken">Propagates persistence cancellation.</param>
    /// <returns>The due work ordered by due time and work id.</returns>
    Task<IReadOnlyList<ProjectionDeliveryRetryWorkItem>> GetDueAsync(
        DateTimeOffset dueUtc,
        int maximumCount,
        CancellationToken cancellationToken = default);
}
