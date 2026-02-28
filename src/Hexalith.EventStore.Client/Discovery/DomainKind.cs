
namespace Hexalith.EventStore.Client.Discovery;

/// <summary>
/// Identifies whether a discovered domain type is an aggregate or a projection.
/// </summary>
public enum DomainKind {
    /// <summary>A type that inherits from <c>EventStoreAggregate&lt;TState&gt;</c>.</summary>
    Aggregate,

    /// <summary>A type that inherits from <c>EventStoreProjection&lt;TReadModel&gt;</c>.</summary>
    Projection,
}
