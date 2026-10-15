using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace FoldedSnapshotCompatibility;

/// <summary>Old binary override providing nonempty virtual command-result bytes.</summary>
public sealed record LegacyEventfulResult : DomainResult
{
    private readonly string _resultPayload;

    /// <summary>Creates an eventful result with the exact virtual result payload.</summary>
    public LegacyEventfulResult(IReadOnlyList<IEventPayload> events, string resultPayload) : base(events)
        => _resultPayload = resultPayload;

    /// <inheritdoc/>
    public override string ResultPayload => _resultPayload;
}
