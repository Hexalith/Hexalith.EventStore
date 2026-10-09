namespace Hexalith.EventStore.Server.Events;
/// <summary>Frames unsigned private initial pins; this record grants no checkpoint or consumer authority.</summary>
/// <param name = "CheckpointSourceHash">The complete original source commitment at covered sequence.</param>
/// <param name = "RequestedSourceHash">The fixed requested-target source commitment.</param>
/// <param name = "FoldHash">The exact declared projection fold fingerprint.</param>
/// <param name = "ReconstructionHash">The exact state and canonical codec binding fingerprint.</param>
/// <param name = "RegistryFingerprint">The current event registry fingerprint.</param>
/// <param name = "StateHash">The exact private canonical state digest.</param>
/// <param name = "RootHash">The complete private checkpoint root image digest.</param>
/// <param name = "WitnessHash">The complete private checkpoint witness image digest.</param>
/// <param name = "CoveredSequence">The positive checkpoint coverage k.</param>
/// <param name = "ActorHead">The unchanged actual fixed head H.</param>
/// <param name = "TargetSequence">The requested target T, at or after k.</param>
/// <param name = "Model">The literal unsigned preparation policy.</param>
internal sealed record DaprLogicalCheckpointInitialSelection(ReadOnlyMemory<byte> CheckpointSourceHash, ReadOnlyMemory<byte> RequestedSourceHash, ReadOnlyMemory<byte> FoldHash, ReadOnlyMemory<byte> ReconstructionHash, ReadOnlyMemory<byte> RegistryFingerprint, ReadOnlyMemory<byte> StateHash, ReadOnlyMemory<byte> RootHash, ReadOnlyMemory<byte> WitnessHash, long CoveredSequence, long ActorHead, long TargetSequence, string Model);
