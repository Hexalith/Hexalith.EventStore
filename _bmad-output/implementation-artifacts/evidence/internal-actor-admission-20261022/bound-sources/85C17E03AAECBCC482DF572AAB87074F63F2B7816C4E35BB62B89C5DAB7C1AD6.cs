namespace Hexalith.EventStore.Server.Actors;

internal sealed record ProjectionRebuildPromotionReceipt(
    string OperationId,
    ProjectionState? PreviousState,
    ProjectionState? PromotedState)
{
    /// <summary>Gets the optional immutable original-work binding.</summary>
    public ProjectionReplayPublicationBinding? PublicationBinding { get; init; }

    /// <summary>Gets whether complete publication was compacted to a bounded tombstone.</summary>
    public bool Completed { get; init; }

    /// <summary>Gets the bounded prior publication floor preserved across ordinary rebuild cleanup.</summary>
    public ProjectionReplayPublicationBinding? PublicationFloor { get; init; }

    /// <summary>Gets the selected source ordering, independently of the delivery checkpoint family.</summary>
    public ProjectionReplaySelectionFence? SelectedReplayFence { get; init; }

    /// <summary>Gets the prior selected ordering for owned rollback only.</summary>
    public ProjectionReplaySelectionFence? PriorReplayFence { get; init; }

    /// <summary>Gets the selected state digest retained when rollback/model bytes are compacted.</summary>
    public string? SelectedStateFingerprint { get; init; }
}
