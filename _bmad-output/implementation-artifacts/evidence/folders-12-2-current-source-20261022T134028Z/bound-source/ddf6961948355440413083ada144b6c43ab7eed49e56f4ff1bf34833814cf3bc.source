using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Qualified test processor for once-admitted folded-state capture.</summary>
public sealed class SnapshotCaptureTestProcessor : DomainProcessorBase<SnapshotCaptureTestState>
{
    /// <summary>Creates a processor with the selected reviewed capture declaration.</summary>
    public SnapshotCaptureTestProcessor(FoldedSnapshotCaptureLimits? limits = null)
    {
        limits ??= SnapshotCaptureTestLimits.Value;
        FoldedSnapshotCapture = new("test-profile", "test-parity-evidence",
            new DetachedStateCapture<SnapshotCaptureTestState>(limits.MaximumPreCommandStateGraphBytes,
                (state, _) =>
                {
                    CopiedCount = state.Count;
                    return CapturedState = new SnapshotCaptureTestState
                    { Count = state.Count, ThrowOnApply = state.ThrowOnApply, Text = state.Text };
                }),
            limits);
    }

    /// <summary>Gets the count observed by the pre-command copier.</summary>
    public int? CopiedCount { get; private set; }

    /// <summary>Gets or sets a handler-only state mutation excluded from replay authority.</summary>
    public int HandlerMutation { get; set; }

    /// <summary>Gets or sets whether to include nonempty command-result bytes.</summary>
    public bool IncludeResultPayload { get; set; }

    /// <summary>Gets the live returned event for isolation assertions.</summary>
    public SnapshotCaptureTestEvent? ReturnedEvent { get; private set; }

    /// <summary>Gets the isolated pre-command state used to observe optional Apply admission.</summary>
    public SnapshotCaptureTestState? CapturedState { get; private set; }

    /// <summary>Gets the live rejection event for canonical-Apply assertions.</summary>
    public SnapshotCaptureTestRejectionEvent? ReturnedRejectionEvent { get; private set; }

    /// <summary>Gets or sets whether the command returns a persisted rejection.</summary>
    public bool Reject { get; set; }

    /// <inheritdoc/>
    protected override FoldedSnapshotCaptureProfile<SnapshotCaptureTestState> FoldedSnapshotCapture { get; }

    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, SnapshotCaptureTestState? currentState)
    {
        if (currentState is not null) { currentState.Count += HandlerMutation; }
        if (Reject)
        {
            ReturnedRejectionEvent = new SnapshotCaptureTestRejectionEvent { Amount = 99 };
            return Task.FromResult(DomainResult.Rejection([ReturnedRejectionEvent]));
        }
        ReturnedEvent = new SnapshotCaptureTestEvent { Amount = 99 };
        if (IncludeResultPayload) { return Task.FromResult<DomainResult>(new SnapshotCaptureTestResult([ReturnedEvent])); }
        return Task.FromResult(DomainResult.Success([ReturnedEvent]));
    }
}
