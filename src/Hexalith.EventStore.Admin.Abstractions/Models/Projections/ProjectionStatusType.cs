namespace Hexalith.EventStore.Admin.Abstractions.Models.Projections;

/// <summary>
/// Status of a projection.
/// </summary>
public enum ProjectionStatusType {
    /// <summary>The projection is actively processing events.</summary>
    Running,

    /// <summary>The projection has been paused by an operator.</summary>
    Paused,

    /// <summary>The projection has encountered an error.</summary>
    Error,

    /// <summary>The projection is being rebuilt from scratch.</summary>
    Rebuilding,
}
