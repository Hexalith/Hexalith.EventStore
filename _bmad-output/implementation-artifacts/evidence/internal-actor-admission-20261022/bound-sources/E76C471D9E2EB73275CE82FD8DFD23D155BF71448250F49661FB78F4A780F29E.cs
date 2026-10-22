namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable fence, capture, stage, and cleanup state for one shared projection scope.</summary>
/// <param name="Version">The durable schema version.</param>
/// <param name="Epoch">The current fence epoch.</param>
/// <param name="ActiveGeneration">The selected readable generation.</param>
/// <param name="Phase">The current epoch phase.</param>
/// <param name="OperationId">The current rebuild operation.</param>
/// <param name="InventoryFingerprint">The captured inventory digest.</param>
/// <param name="CaptureHighWatermarks">The sealed owner positions.</param>
/// <param name="ConsumerCheckpoints">The writer checkpoints.</param>
/// <param name="RequiredWriters">The authoritative writer identifiers.</param>
/// <param name="RegisteredWriters">The admitted writer identifiers.</param>
/// <param name="Journal">The pending ordinary deliveries.</param>
/// <param name="StageFingerprint">The staged candidate digest.</param>
/// <param name="StageKeys">The staged logical keys.</param>
/// <param name="StageMutations">The inline staged mutations.</param>
/// <param name="StageManifestDigest">The staged manifest digest.</param>
/// <param name="StageControlIndexIntent">The staged control index update.</param>
/// <param name="RetainedControlIndexNames">The selected control index names.</param>
/// <param name="OffboardingAuditId">The offboarding audit identifier.</param>
/// <param name="StageChunks">The staged immutable chunks.</param>
/// <param name="StageExecutionStarted">Whether promotion execution began.</param>
/// <param name="CleanupChunks">The committed chunk cleanup reference.</param>
/// <param name="CaptureChunks">The sealed capture chunks.</param>
/// <param name="CleanupPreparedChunks">The prepared chunk cleanup reference.</param>
/// <param name="StageStreaming">Whether streamed staging is active.</param>
/// <param name="StageCompletedPageCount">The number of completed stream pages.</param>
/// <param name="StageSealedPageCount">The sealed stream page count.</param>
/// <param name="CleanupStreamOperationId">The operation awaiting stream metadata cleanup.</param>
/// <param name="CleanupStreamEpoch">The epoch awaiting stream metadata cleanup.</param>
/// <param name="CleanupStreamPageCount">The stream pages awaiting cleanup.</param>
/// <param name="OwnerSequenceHandoffPending">Whether committed owner sequence seeding remains.</param>
/// <param name="StageReservedPageCount">The number of stream page ordinals reserved before metadata writes.</param>
internal sealed record SharedProjectionEpochState(
    int Version,
    long Epoch,
    long ActiveGeneration,
    SharedProjectionEpochPhase Phase,
    string? OperationId,
    string? InventoryFingerprint,
    Dictionary<string, long> CaptureHighWatermarks,
    Dictionary<string, SharedProjectionCheckpoint> ConsumerCheckpoints,
    string[] RequiredWriters,
    string[] RegisteredWriters,
    SharedProjectionJournalEntry[] Journal,
    string? StageFingerprint,
    string[] StageKeys,
    SharedProjectionMutation[]? StageMutations,
    string? StageManifestDigest,
    SharedProjectionControlIndexIntent? StageControlIndexIntent = null,
    string[]? RetainedControlIndexNames = null,
    string? OffboardingAuditId = null,
    SharedProjectionChunkReference? StageChunks = null,
    bool StageExecutionStarted = false,
    SharedProjectionChunkReference? CleanupChunks = null,
    SharedProjectionChunkReference? CaptureChunks = null,
    SharedProjectionChunkReference? CleanupPreparedChunks = null,
    bool StageStreaming = false,
    int StageCompletedPageCount = 0,
    int StageSealedPageCount = 0,
    string? CleanupStreamOperationId = null,
    long CleanupStreamEpoch = 0,
    int CleanupStreamPageCount = 0,
    bool OwnerSequenceHandoffPending = false,
    int StageReservedPageCount = 0)
{
    /// <summary>Conflicting delivery identities, including the explicit repair audit.</summary>
    public SharedProjectionDeliveryConflict[] DeliveryConflicts { get; init; } = [];

    /// <summary>The first identity beyond the bounded evidence capacity; requires scope restore or offboarding.</summary>
    public SharedProjectionDeliveryConflict? DeliveryConflictOverflow { get; init; }

    /// <summary>Discoverable terminal fold dispositions, including sources without successful family membership.</summary>
    public SharedProjectionParkingEvidence[] ParkingEvidence { get; init; } = [];

    /// <summary>The first parked identity beyond bounded evidence capacity; requires scope restore or offboarding.</summary>
    public SharedProjectionParkingEvidence? ParkingEvidenceOverflow { get; init; }

    /// <summary>The pre-copy-on-write selected generation inherited by unchanged logical keys.</summary>
    public long? CopyOnWriteAnchorGeneration { get; init; }

    /// <summary>Whether pending streamed cleanup may retire old immutable values after promotion.</summary>
    public bool CleanupPromotedRetirements { get; init; }

    /// <summary>The tenant that owns the current operation's private rebuild session.</summary>
    public string? OperationTenantId { get; init; }

    /// <summary>Gets the uninitialized epoch state.</summary>
    internal static SharedProjectionEpochState Empty => new(
        1,
        0,
        0,
        SharedProjectionEpochPhase.Open,
        null,
        null,
        new Dictionary<string, long>(StringComparer.Ordinal),
        new Dictionary<string, SharedProjectionCheckpoint>(StringComparer.Ordinal),
        [],
        [],
        [],
        null,
        [],
        null,
        null,
        null,
        [],
        null);
}
