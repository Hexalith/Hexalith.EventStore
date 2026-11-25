using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Represents the durable result returned by one named projection handler.
/// </summary>
/// <param name="Status">The closed durable outcome classification.</param>
/// <param name="State">Optional compatibility state for a completed actor-backed projection.</param>
/// <param name="ReasonCode">An optional bounded support-safe reason code.</param>
public sealed record DomainProjectionHandlerResult(
    ProjectionDispatchStatus Status,
    JsonElement? State,
    string? ReasonCode) {
    /// <summary>Creates a completed result.</summary>
    /// <param name="state">Optional compatibility state.</param>
    /// <returns>A completed result.</returns>
    public static DomainProjectionHandlerResult Completed(JsonElement? state = null)
        => new(ProjectionDispatchStatus.Completed, state, null);

    /// <summary>Creates an already-completed result.</summary>
    /// <returns>An already-completed result.</returns>
    public static DomainProjectionHandlerResult AlreadyCompleted()
        => AlreadyCompleted(null);

    /// <summary>Creates an already-completed result with compatibility state for actor write replay.</summary>
    /// <param name="state">The same compatibility state the completed operation produced.</param>
    /// <returns>An already-completed result.</returns>
    public static DomainProjectionHandlerResult AlreadyCompleted(JsonElement? state)
        => new(ProjectionDispatchStatus.AlreadyCompleted, state, null);

    /// <summary>Creates a retryable result.</summary>
    /// <param name="reasonCode">The support-safe reason code.</param>
    /// <returns>A retryable result.</returns>
    public static DomainProjectionHandlerResult Retryable(string reasonCode)
        => new(ProjectionDispatchStatus.Retryable, null, reasonCode);

    /// <summary>Creates an indeterminate result.</summary>
    /// <param name="reasonCode">The support-safe reason code.</param>
    /// <returns>An indeterminate result.</returns>
    public static DomainProjectionHandlerResult Indeterminate(string reasonCode)
        => new(ProjectionDispatchStatus.Indeterminate, null, reasonCode);

    /// <summary>Creates a terminal failed result.</summary>
    /// <param name="reasonCode">The support-safe reason code.</param>
    /// <returns>A failed result.</returns>
    public static DomainProjectionHandlerResult Failed(string reasonCode)
        => new(ProjectionDispatchStatus.Failed, null, reasonCode);
}
