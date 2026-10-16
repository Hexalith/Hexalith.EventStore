namespace Hexalith.EventStore.Server.Projections;

/// <summary>Actions supported by the existing bounded mapped staging protocol.</summary>
internal enum ProjectionMappedReplayAction { Commit, Verify, Abort, Stage }
