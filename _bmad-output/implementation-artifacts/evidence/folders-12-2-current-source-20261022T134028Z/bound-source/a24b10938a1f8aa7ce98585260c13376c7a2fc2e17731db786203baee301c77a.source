using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Runtime-owned DAPR actor interface for projection query actors.
/// </summary>
/// <remarks>
/// The public Contracts package owns the implementation-neutral
/// <see cref="Hexalith.EventStore.Contracts.Queries.IProjectionActor"/> query method
/// contract. Server and DAPR-hosting projects own actor inheritance and typed-proxy
/// guard surfaces. DAPR actor interfaces cannot inherit non-actor interfaces, so this
/// runtime-owned interface mirrors the public method signature instead of inheriting
/// the Contracts interface.
/// </remarks>
public interface IDaprProjectionActor : IActor {
    /// <summary>
    /// Serves a projection query from a public query envelope.
    /// </summary>
    /// <param name="envelope">The query envelope containing routing metadata and UTF-8 JSON payload bytes.</param>
    /// <returns>The query result containing payload bytes or an adapter-edge failure.</returns>
    Task<Hexalith.EventStore.Contracts.Queries.QueryResult> QueryAsync(
        Hexalith.EventStore.Contracts.Queries.QueryEnvelope envelope);
}
