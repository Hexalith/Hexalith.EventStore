namespace Hexalith.EventStore.Server.Projections;

/// <summary>Explicit maintenance attestations required to activate v2 delivery writers.</summary>
/// <param name="CutoverCommit">The exact repository commit deployed to every writer.</param>
/// <param name="BackupReference">The operator's durable pre-cutover backup reference.</param>
/// <param name="WritersQuiesced">Whether every old server writer is stopped and verified.</param>
/// <param name="RetryWorkersQuiesced">Whether every old retry worker is stopped and verified.</param>
/// <param name="DowngradeProhibitedAcknowledged">Whether the post-marker no-downgrade boundary is acknowledged.</param>
internal sealed record ProjectionDeliveryCutoverRequest(
    string CutoverCommit,
    string BackupReference,
    bool WritersQuiesced,
    bool RetryWorkersQuiesced,
    bool DowngradeProhibitedAcknowledged);
