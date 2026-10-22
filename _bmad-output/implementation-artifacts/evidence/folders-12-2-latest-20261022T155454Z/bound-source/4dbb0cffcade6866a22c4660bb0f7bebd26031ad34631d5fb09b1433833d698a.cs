namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Contains a support-safe structural projection of PostgreSQL state.</summary>
/// <param name="Stage">The bounded observation stage.</param>
/// <param name="TotalRows">The total state row count.</param>
/// <param name="AdmissionRows">The live admission row count.</param>
/// <param name="TerminalRows">The terminal admission row count.</param>
/// <param name="TombstoneRows">The compacted tombstone row count.</param>
/// <param name="MinimalTombstoneRows">The approved minimal-tombstone row count.</param>
/// <param name="DirectoryRows">The tenant-directory row count.</param>
/// <param name="LifecycleRows">The tenant-lifecycle row count.</param>
/// <param name="AggregateMetadataRows">The aggregate metadata row count.</param>
/// <param name="AggregateEventRows">The persisted aggregate event row count.</param>
/// <param name="AggregateSequenceTotal">The sum of persisted aggregate sequences.</param>
/// <param name="ProtectedSentinelMatches">The raw-key sentinel match count.</param>
/// <param name="SchemaSha256">The PostgreSQL state-table schema digest.</param>
/// <param name="ProjectionSha256">The complete structural projection digest.</param>
internal sealed record Oq8PostgresqlSnapshot(
    string Stage,
    int TotalRows,
    int AdmissionRows,
    int TerminalRows,
    int TombstoneRows,
    int MinimalTombstoneRows,
    int DirectoryRows,
    int LifecycleRows,
    int AggregateMetadataRows,
    int AggregateEventRows,
    int AggregateSequenceTotal,
    int ProtectedSentinelMatches,
    string SchemaSha256,
    string ProjectionSha256);
