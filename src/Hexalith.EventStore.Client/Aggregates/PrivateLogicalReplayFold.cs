using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Retains private canonical last-good bytes; failure supplies no committed continuation.</summary>
/// <param name="State">The separately retained canonical last-good state.</param>
/// <param name="LastGoodSequence">The last sequence whose Apply and canonical capture succeeded.</param>
/// <param name="Failure">The candidate failure, when the page cannot transition.</param>
/// <param name="FailedSequence">The exact sequence whose Apply failed.</param>
/// <param name="FailedEventType">The admitted canonical event identity whose Apply failed.</param>
internal sealed record PrivateLogicalReplayFold(ImmutablePayload State, long LastGoodSequence, Exception? Failure,
    long? FailedSequence = null, string? FailedEventType = null) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => State.Dispose();
}
