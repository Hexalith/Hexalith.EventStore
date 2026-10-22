using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Supplies nonempty admitted result bytes for optional-capture preservation proofs.</summary>
internal sealed record SnapshotCaptureTestResult : DomainResult
{
    /// <summary>Creates the result with its original event list.</summary>
    internal SnapshotCaptureTestResult(IReadOnlyList<IEventPayload> events) : base(events) { }

    /// <inheritdoc/>
    public override string ResultPayload => "{\"result\":7}";
}
