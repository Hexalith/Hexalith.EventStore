namespace Hexalith.EventStore.Controllers;

/// <summary>Operator attestations required to activate projection delivery writer protocol v2.</summary>
/// <param name="CutoverCommit">The exact repository commit deployed to every writer.</param>
/// <param name="BackupReference">The durable pre-cutover backup reference.</param>
/// <param name="WritersQuiesced">Whether every old server writer is stopped and verified.</param>
/// <param name="RetryWorkersQuiesced">Whether every old retry worker is stopped and verified.</param>
/// <param name="DowngradeProhibitedAcknowledged">Whether the post-marker no-downgrade boundary is acknowledged.</param>
public sealed record ProjectionDeliveryCutoverRequestBody(
    string CutoverCommit,
    string BackupReference,
    bool WritersQuiesced,
    bool RetryWorkersQuiesced,
    bool DowngradeProhibitedAcknowledged);
