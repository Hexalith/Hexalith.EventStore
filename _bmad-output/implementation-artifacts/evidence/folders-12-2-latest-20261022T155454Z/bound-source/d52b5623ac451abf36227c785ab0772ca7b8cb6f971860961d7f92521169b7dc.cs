using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Tests.Actors;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Qualified processor that returns a live event different from its admitted JSON.</summary>
internal sealed class ParityProcessor : DomainProcessorBase<ParityState>
{
    /// <summary>Gets the returned event for isolation assertions.</summary>
    public ParityEvent? ReturnedEvent { get; private set; }

    /// <inheritdoc/>
    protected override FoldedSnapshotCaptureProfile<ParityState> FoldedSnapshotCapture { get; }
        = new("parity-profile", "persisted-byte-and-json-restore-parity",
            new DetachedStateCapture<ParityState>(1024,
                static (state, _) => new ParityState { Total = state.Total, EventCount = state.EventCount }),
            SnapshotTestLimits.ForState());

    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, ParityState? currentState)
    {
        ReturnedEvent = new ParityEvent { Amount = 99, Defaulted = 7, Ignored = 11 };
        return Task.FromResult(DomainResult.Success([ReturnedEvent]));
    }
}
