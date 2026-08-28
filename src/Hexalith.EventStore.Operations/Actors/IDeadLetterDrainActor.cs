using Dapr.Actors;

using Hexalith.EventStore.Operations.Models;

namespace Hexalith.EventStore.Operations.Actors;

/// <summary>
/// Serializes capture, query, replay, and archive operations for one dead-letter topic.
/// </summary>
public interface IDeadLetterDrainActor : IActor
{
    /// <summary>Durably captures an item and its index entry before returning.</summary>
    Task<DeadLetterCaptureResult> CaptureAsync(DeadLetterCaptureRequest request);

    /// <summary>Returns a redacted tenant-scoped page.</summary>
    Task<DeadLetterListResult> ListAsync(DeadLetterListRequest request);

    /// <summary>Durably requests replay of the selected items.</summary>
    Task<DeadLetterActorActionResult> RetryAsync(DeadLetterActionRequest request);

    /// <summary>Archives the selected items as intentionally skipped.</summary>
    Task<DeadLetterActorActionResult> SkipAsync(DeadLetterActionRequest request);

    /// <summary>Archives the selected items.</summary>
    Task<DeadLetterActorActionResult> ArchiveAsync(DeadLetterActionRequest request);
}
