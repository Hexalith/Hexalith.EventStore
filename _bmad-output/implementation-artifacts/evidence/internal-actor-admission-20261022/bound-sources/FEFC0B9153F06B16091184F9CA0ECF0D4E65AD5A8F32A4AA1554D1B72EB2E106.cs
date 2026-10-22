namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — provider-neutral state machine for an operator-initiated key
/// deletion/invalidation workflow. EventStore owns the audit/state transitions; provider-specific
/// crypto execution remains out of scope. Transitions are documented in the story ST0 decision
/// table; <see cref="CryptoShreddingWorkflowTransitions"/> exposes the allowed set.
/// </summary>
public enum CryptoShreddingWorkflowState {
    /// <summary>The workflow has been submitted by an operator but no decision has been recorded yet.</summary>
    Requested = 0,

    /// <summary>An operator approved the workflow; the provider call has not been dispatched.</summary>
    Approved = 1,

    /// <summary>The workflow was rejected before any irreversible step.</summary>
    Rejected = 2,

    /// <summary>Approval has been forwarded to the provider; no terminal outcome has been observed yet.</summary>
    PendingProvider = 3,

    /// <summary>The key has been invalidated; protected data is permanently unreadable. Terminal.</summary>
    Invalidated = 4,

    /// <summary>The key has been deleted; protected data is permanently unreadable. Terminal.</summary>
    Deleted = 5,

    /// <summary>
    /// Provider reported a verification failure; affected data is unreadable and the workflow
    /// requires operator decision before completion.
    /// </summary>
    VerificationFailed = 6,

    /// <summary>
    /// A restored-backup admission flagged a conflict with this workflow's irreversible decision.
    /// Requires <see cref="OperatorDecisionRequired"/> resolution before terminal completion.
    /// </summary>
    RestoreConflict = 7,

    /// <summary>
    /// Protected data linked to this workflow has been quarantined pending explicit operator
    /// review.
    /// </summary>
    Quarantined = 8,

    /// <summary>An explicit operator decision is required to make progress.</summary>
    OperatorDecisionRequired = 9,

    /// <summary>
    /// The workflow reached its terminal success state. Audit evidence has been recorded.
    /// </summary>
    Completed = 10,

    /// <summary>
    /// The workflow was cancelled before any irreversible decision (e.g. key invalidation or
    /// deletion) was recorded. Audit evidence preserves the cancellation transition.
    /// </summary>
    CancelledBeforeDecision = 11,
}
