using System;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — result of a workflow operation (create, advance, cancel). Carries the current
/// workflow state plus safe metadata. Idempotent replays return the existing record with
/// <see cref="IdempotentReplay"/> set to <see langword="true"/>.
/// </summary>
/// <param name="Identity">The workflow identity.</param>
/// <param name="State">The current workflow state.</param>
/// <param name="ReasonCode">Stable kebab-case reason code (workflow state name as kebab-case).</param>
/// <param name="NextAction">Operator next-action hint.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
/// <param name="AuditId">Identifier of the most recent audit record.</param>
/// <param name="DecisionActorId">Actor who recorded the most recent decision.</param>
/// <param name="DecidedAtUtc">When the most recent decision was recorded.</param>
/// <param name="IrreversibleDecisionRecorded">Indicates whether the workflow has already recorded an irreversible decision.</param>
/// <param name="IdempotentReplay">Indicates whether this decision was produced by a repeated request.</param>
public sealed record CryptoShreddingWorkflowDecision(
    CryptoShreddingWorkflowIdentity Identity,
    CryptoShreddingWorkflowState State,
    string ReasonCode,
    CryptoShreddingNextAction NextAction,
    string? CorrelationId,
    string? AuditId,
    string DecisionActorId,
    DateTimeOffset DecidedAtUtc,
    bool IrreversibleDecisionRecorded,
    bool IdempotentReplay) {
    /// <summary>Returns the canonical kebab-case reason code for the supplied workflow state.</summary>
    /// <param name="state">The workflow state.</param>
    /// <returns>The kebab-case wire code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown enum values.</exception>
    public static string ReasonCodeFor(CryptoShreddingWorkflowState state) => state switch {
        CryptoShreddingWorkflowState.Requested => "requested",
        CryptoShreddingWorkflowState.Approved => "approved",
        CryptoShreddingWorkflowState.Rejected => "rejected",
        CryptoShreddingWorkflowState.PendingProvider => "pending-provider",
        CryptoShreddingWorkflowState.Invalidated => "invalidated",
        CryptoShreddingWorkflowState.Deleted => "deleted",
        CryptoShreddingWorkflowState.VerificationFailed => "verification-failed",
        CryptoShreddingWorkflowState.RestoreConflict => "restore-conflict",
        CryptoShreddingWorkflowState.Quarantined => "quarantined",
        CryptoShreddingWorkflowState.OperatorDecisionRequired => "operator-decision-required",
        CryptoShreddingWorkflowState.Completed => "completed",
        CryptoShreddingWorkflowState.CancelledBeforeDecision => "cancelled-before-decision",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown CryptoShreddingWorkflowState value."),
    };

    /// <summary>Returns the canonical operator next-action hint for the supplied workflow state.</summary>
    /// <param name="state">The workflow state.</param>
    /// <returns>The operator next-action hint.</returns>
    public static CryptoShreddingNextAction NextActionFor(CryptoShreddingWorkflowState state) => state switch {
        CryptoShreddingWorkflowState.Requested => CryptoShreddingNextAction.SubmitOperatorDecision,
        CryptoShreddingWorkflowState.Approved => CryptoShreddingNextAction.None,
        CryptoShreddingWorkflowState.Rejected => CryptoShreddingNextAction.None,
        CryptoShreddingWorkflowState.PendingProvider => CryptoShreddingNextAction.RetryWithBackoff,
        CryptoShreddingWorkflowState.Invalidated => CryptoShreddingNextAction.None,
        CryptoShreddingWorkflowState.Deleted => CryptoShreddingNextAction.None,
        CryptoShreddingWorkflowState.VerificationFailed => CryptoShreddingNextAction.SubmitOperatorDecision,
        CryptoShreddingWorkflowState.RestoreConflict => CryptoShreddingNextAction.SubmitOperatorDecision,
        CryptoShreddingWorkflowState.Quarantined => CryptoShreddingNextAction.SubmitOperatorDecision,
        CryptoShreddingWorkflowState.OperatorDecisionRequired => CryptoShreddingNextAction.SubmitOperatorDecision,
        CryptoShreddingWorkflowState.Completed => CryptoShreddingNextAction.None,
        CryptoShreddingWorkflowState.CancelledBeforeDecision => CryptoShreddingNextAction.None,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown CryptoShreddingWorkflowState value."),
    };
}
