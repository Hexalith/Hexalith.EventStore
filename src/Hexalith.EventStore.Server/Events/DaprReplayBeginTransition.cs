using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Retains initial canonical bytes and staging capacity until begin/takeover readback and cache detachment resolve.</summary>
/// <param name="Prior">The exact predecessor operation, or absence for initial creation.</param>
/// <param name="Expected">The exact proposed operation pointer.</param>
/// <param name="InitialState">The private initial canonical state, when creating an operation.</param>
/// <param name="Staging">The retained cache staging and readback capacity.</param>
/// <param name="Budget">The shared operation lifetime budget.</param>
/// <param name="ParticipantDigest">The bounded semantic digest of the unchanged or initial participants.</param>
internal sealed record DaprReplayBeginTransition(DaprReplayOperationRecord? Prior, DaprReplayOperationRecord Expected,
    DaprLogicalResponseOwner? InitialState, EventBufferReservation Staging, EventBufferBudget Budget, byte[] ParticipantDigest) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose()
    {
        InitialState?.Dispose();
        Staging.Dispose();
    }
}
