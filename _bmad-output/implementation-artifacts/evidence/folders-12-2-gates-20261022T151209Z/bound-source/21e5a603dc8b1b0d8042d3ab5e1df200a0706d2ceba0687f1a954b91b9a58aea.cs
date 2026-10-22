using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Persisted rejection whose canonical payload is admitted before optional capture.</summary>
public sealed class SnapshotCaptureTestRejectionEvent : IRejectionEvent
{
    /// <summary>Gets or sets the folded amount.</summary>
    public int Amount { get; set; }
}
