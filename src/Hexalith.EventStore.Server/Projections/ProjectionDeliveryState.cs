using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Versioned projection-scoped delivery identity, reservation, receipt, and checkpoint row.</summary>
/// <param name="SchemaVersion">The persisted row schema version.</param>
/// <param name="WriterProtocolVersion">The last writer protocol version.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The aggregate domain.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="ProjectionName">The named projection route.</param>
/// <param name="LastDeliveredSequence">The last contiguous completed aggregate-local sequence.</param>
/// <param name="LastCompletedMessageId">The persisted identity at the completed head.</param>
/// <param name="CompletedPrefixFingerprint">The cumulative completed-prefix fingerprint.</param>
/// <param name="CompletedReceipts">The bounded recent exact-completion receipts.</param>
/// <param name="FirstRetainedSequence">The first sequence retained as an exact receipt.</param>
/// <param name="ActiveReservation">The optional active fenced delivery reservation.</param>
/// <param name="MigrationProvenance">How the row was initialized or hydrated.</param>
/// <param name="UpdatedAt">The UTC time of the last row transition.</param>
internal sealed record ProjectionDeliveryState(
    int SchemaVersion,
    int WriterProtocolVersion,
    string TenantId,
    string Domain,
    string AggregateId,
    string? ProjectionName,
    long LastDeliveredSequence,
    string? LastCompletedMessageId,
    string? CompletedPrefixFingerprint,
    IReadOnlyList<ProjectionDeliveryReceipt>? CompletedReceipts,
    long FirstRetainedSequence,
    ProjectionDeliveryReservation? ActiveReservation,
    ProjectionDeliveryMigrationProvenance MigrationProvenance,
    DateTimeOffset UpdatedAt) {
    /// <summary>The current persisted row schema.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The only writer protocol permitted after cutover.</summary>
    public const int CurrentWriterProtocolVersion = 2;

    /// <summary>Creates a valid empty versioned row for an absent or zero checkpoint.</summary>
    public static ProjectionDeliveryState CreateEmpty(
        AggregateIdentity identity,
        string projectionName,
        string initialFingerprint,
        DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ArgumentException.ThrowIfNullOrWhiteSpace(initialFingerprint);
        return new ProjectionDeliveryState(
            CurrentSchemaVersion,
            CurrentWriterProtocolVersion,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            projectionName,
            0,
            null,
            initialFingerprint,
            [],
            0,
            null,
            ProjectionDeliveryMigrationProvenance.InitializedFromZero,
            now);
    }
}
