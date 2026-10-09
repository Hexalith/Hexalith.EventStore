namespace Hexalith.EventStore.Server.Events;
/// <summary>Frames an unsigned private range plan, never a verified prefix or completed checkpoint.</summary>
/// <param name = "SelectionHash">The exact privately owned unsigned selection digest.</param>
/// <param name = "RequestedSourceHash">The complete fixed requested-target source commitment.</param>
/// <param name = "CoveredSequence">The positive checkpoint coverage k.</param>
/// <param name = "ActorHead">The actual fixed source head H.</param>
/// <param name = "TargetSequence">The inclusive requested preparation target T.</param>
/// <param name = "StartSequence">The requested first tail sequence or zero sentinel.</param>
/// <param name = "EndSequence">The planned bounded end, not verified event progress.</param>
/// <param name = "PlannedCount">The planned count, not an actual authenticated route count.</param>
/// <param name = "Kind">The unsigned current-zero or tail planning shape.</param>
/// <param name = "Model">The literal unsigned preparation policy.</param>
internal sealed record DaprLogicalCheckpointRangePreparation(ReadOnlyMemory<byte> SelectionHash, ReadOnlyMemory<byte> RequestedSourceHash, long CoveredSequence, long ActorHead, long TargetSequence, long StartSequence, long EndSequence, int PlannedCount, DaprLogicalCheckpointRangeKind Kind, string Model);
