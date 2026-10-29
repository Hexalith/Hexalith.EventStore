namespace Hexalith.EventStore.Server.Commands;

/// <summary>Tracks one protected admission actor decision and fence.</summary>
/// <param name="ActorId">The protected tenant/key actor identifier.</param>
/// <param name="FencingToken">The active fence.</param>
/// <param name="Decision">The admission decision.</param>
/// <param name="ReplayResult">The optional replay result.</param>
/// <param name="ExecutionContext">The internal signed current-fence capability for execute decisions.</param>
/// <param name="ExecutionMessageId">The persisted stable execution identity for live states.</param>
/// <param name="ExecutionCorrelationId">The persisted stable aggregate-checkpoint identity for live states.</param>
/// <param name="CatalogRootDigest">Captured runtime catalog root; absent only in explicit legacy construction seams.</param>
/// <param name="CatalogEpoch">Captured activation fence; rollback never revives a prior session.</param>
public sealed record IdempotencyAdmissionSession(
    string ActorId,
    long FencingToken,
    Actors.IdempotencyAdmissionDecision Decision,
    Actors.CommandProcessingResult? ReplayResult = null,
    IdempotencyExecutionContext? ExecutionContext = null,
    string? ExecutionMessageId = null,
    string? ExecutionCorrelationId = null,
    string? CatalogRootDigest = null,
    long? CatalogEpoch = null);
