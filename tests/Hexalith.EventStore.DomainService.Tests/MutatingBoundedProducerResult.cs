using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Mutates caller event references when the optional result payload is read.</summary>
internal sealed record MutatingBoundedProducerResult : DomainResult
{
    private readonly Action _onRead;

    /// <summary>Creates a result whose enriched-payload getter probes reference capture.</summary>
    internal MutatingBoundedProducerResult(IReadOnlyList<IEventPayload> events, Action onRead) : base(events)
        => _onRead = onRead;

    /// <inheritdoc/>
    public override string? ResultPayload { get { _onRead(); return null; } }
}
