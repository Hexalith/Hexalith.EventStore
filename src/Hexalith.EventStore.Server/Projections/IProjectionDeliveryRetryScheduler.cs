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

    /// <summary>Updates pending, terminal, attempt, and due metadata after reconciliation.</summary>
    /// <param name="workItem">The replacement item.</param>
    /// <param name="cancellationToken">Propagates persistence cancellation.</param>
    Task UpdateAsync(ProjectionDeliveryRetryWorkItem workItem, CancellationToken cancellationToken = default);

    /// <summary>Deletes work only after every route converges successfully.</summary>
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
