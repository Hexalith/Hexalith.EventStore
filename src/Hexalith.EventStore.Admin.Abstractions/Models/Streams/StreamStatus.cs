namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Status of an event stream.
/// </summary>
public enum StreamStatus
{
    /// <summary>The stream is actively receiving events.</summary>
    Active,

    /// <summary>The stream has not received events recently.</summary>
    Idle,

    /// <summary>The stream has been tombstoned and will not accept new events.</summary>
    Tombstoned,
}
