using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Maps coordinated read-model batch truth into the closed named projection outcome set.</summary>
public static class ReadModelBatchProjectionResultMapper {
    /// <summary>Maps one batch result without exposing fingerprints or recovery details.</summary>
    /// <param name="result">The coordinated batch result.</param>
    /// <returns>The corresponding support-safe handler result.</returns>
    public static DomainProjectionHandlerResult Map(ReadModelBatchResult result) {
        ArgumentNullException.ThrowIfNull(result);
        return result.Status switch {
            ReadModelBatchStatus.Completed when result.ConflictKind == ReadModelBatchConflictKind.None
                => DomainProjectionHandlerResult.Completed(),
            ReadModelBatchStatus.AlreadyCompleted when result.ConflictKind == ReadModelBatchConflictKind.None
                => DomainProjectionHandlerResult.AlreadyCompleted(),
            ReadModelBatchStatus.Conflict when result.ConflictKind == ReadModelBatchConflictKind.Optimistic
                => DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.PartialRetry),
            ReadModelBatchStatus.Incomplete when result.ConflictKind == ReadModelBatchConflictKind.None
                => DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.PartialRetry),
            ReadModelBatchStatus.Conflict when result.ConflictKind == ReadModelBatchConflictKind.Identity
                => DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.HandlerFailure),
            ReadModelBatchStatus.Indeterminate when result.ConflictKind == ReadModelBatchConflictKind.None
                => DomainProjectionHandlerResult.Indeterminate(ProjectionDispatchReasonCodes.HandlerFailure),
            _ => DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.MalformedOutcome),
        };
    }
}
