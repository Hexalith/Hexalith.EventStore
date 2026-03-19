
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Sample.Counter.Queries;

/// <summary>
/// Query contract for retrieving the current counter status.
/// Implements <see cref="IQueryContract"/> to prove the contract compiles
/// against a real domain type. Projection actor, wiring, and API endpoint
/// are deferred to Story 11.5.
/// </summary>
public sealed record GetCounterStatusQuery : IQueryContract {
    /// <inheritdoc/>
    public static string QueryType => "get-counter-status";

    /// <inheritdoc/>
    public static string Domain => "counter";

    /// <inheritdoc/>
    public static string ProjectionType => "counter";
}
