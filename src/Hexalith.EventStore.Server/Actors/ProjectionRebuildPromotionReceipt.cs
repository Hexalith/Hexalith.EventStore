namespace Hexalith.EventStore.Server.Actors;

internal sealed record ProjectionRebuildPromotionReceipt(
    string OperationId,
    ProjectionState? PreviousState,
    ProjectionState PromotedState);
