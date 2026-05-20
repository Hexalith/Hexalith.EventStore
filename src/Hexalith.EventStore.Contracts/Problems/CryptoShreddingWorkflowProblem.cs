using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Problems;

/// <summary>
/// Story 22.7c — stable ProblemDetails contract for crypto-shredding workflow conflicts.
/// Extension keys here MUST be the only sources of detail surfaced in API/admin/CLI/MCP
/// responses; payload bytes, snapshot state, raw keys, provider-private metadata, and stack
/// traces never appear.
/// </summary>
public static class CryptoShreddingWorkflowProblem {
    /// <summary>Stable ProblemDetails type URI for crypto-shredding workflow conflicts.</summary>
    public const string TypeUri = "https://hexalith.io/problems/crypto-shredding-workflow-conflict";

    /// <summary>Default human-readable title.</summary>
    public const string DefaultTitle = "Crypto-shredding workflow conflict";

    /// <summary>Extension key carrying the workflow identifier.</summary>
    public const string ExtensionWorkflowId = "workflowId";

    /// <summary>Extension key carrying the current workflow state name.</summary>
    public const string ExtensionWorkflowState = "workflowState";

    /// <summary>Extension key carrying the stable reason code.</summary>
    public const string ExtensionReasonCode = "reasonCode";

    /// <summary>Extension key carrying the operator next-action hint.</summary>
    public const string ExtensionNextAction = "nextAction";

    /// <summary>Extension key carrying the affected tenant.</summary>
    public const string ExtensionTenantId = "tenantId";

    /// <summary>Extension key carrying the affected domain.</summary>
    public const string ExtensionDomain = "domain";

    /// <summary>Extension key carrying the affected aggregate identifier.</summary>
    public const string ExtensionAggregateId = "aggregateId";

    /// <summary>Extension key carrying the inclusive lower bound of the affected sequence range.</summary>
    public const string ExtensionFromSequence = "fromSequence";

    /// <summary>Extension key carrying the inclusive upper bound of the affected sequence range.</summary>
    public const string ExtensionToSequence = "toSequence";

    /// <summary>Extension key carrying the correlation identifier.</summary>
    public const string ExtensionCorrelationId = "correlationId";

    /// <summary>Extension key carrying the audit identifier.</summary>
    public const string ExtensionAuditId = "auditId";

    /// <summary>Extension key indicating whether an irreversible decision has been recorded.</summary>
    public const string ExtensionIrreversibleDecisionRecorded = "irreversibleDecisionRecorded";

    /// <summary>Returns the recommended HTTP status code for the supplied workflow state.</summary>
    /// <param name="state">The workflow state.</param>
    /// <returns>The recommended HTTP status code.</returns>
    public static int GetStatusCode(CryptoShreddingWorkflowState state) => state switch {
        // 425 Too Early — caller submitted while a non-terminal provider call is in flight
        CryptoShreddingWorkflowState.PendingProvider => 425,
        // 409 Conflict — caller attempted to undo a terminal/irreversible decision
        CryptoShreddingWorkflowState.Invalidated
        or CryptoShreddingWorkflowState.Deleted
        or CryptoShreddingWorkflowState.Completed
        or CryptoShreddingWorkflowState.Rejected
        or CryptoShreddingWorkflowState.CancelledBeforeDecision
        or CryptoShreddingWorkflowState.RestoreConflict
        or CryptoShreddingWorkflowState.OperatorDecisionRequired
        or CryptoShreddingWorkflowState.Quarantined => 409,
        // 422 Unprocessable Entity — verification failed, awaiting operator action
        CryptoShreddingWorkflowState.VerificationFailed => 422,
        // 202 Accepted — workflow accepted but no decision yet
        CryptoShreddingWorkflowState.Requested or CryptoShreddingWorkflowState.Approved => 202,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown CryptoShreddingWorkflowState value."),
    };

    /// <summary>Returns safe operator-facing guidance for the supplied workflow state.</summary>
    /// <param name="state">The workflow state.</param>
    /// <returns>A safe fixed guidance string.</returns>
    public static string GetSafeOperatorGuidance(CryptoShreddingWorkflowState state) => state switch {
        CryptoShreddingWorkflowState.Requested => "Workflow recorded. Submit approval or rejection.",
        CryptoShreddingWorkflowState.Approved => "Workflow approved. Provider dispatch pending.",
        CryptoShreddingWorkflowState.Rejected => "Workflow rejected by operator. No further action.",
        CryptoShreddingWorkflowState.PendingProvider => "Provider call in flight. Retry the request after the provider reports a terminal outcome.",
        CryptoShreddingWorkflowState.Invalidated => "Key invalidation has been recorded. Affected data is permanently unreadable.",
        CryptoShreddingWorkflowState.Deleted => "Key deletion has been recorded. Affected data is permanently unreadable.",
        CryptoShreddingWorkflowState.VerificationFailed => "Provider reported a verification failure. Submit an explicit operator decision.",
        CryptoShreddingWorkflowState.RestoreConflict => "A restored backup conflicts with this workflow's irreversible decision. Submit an operator decision.",
        CryptoShreddingWorkflowState.Quarantined => "Affected data has been quarantined. Submit an operator decision to close.",
        CryptoShreddingWorkflowState.OperatorDecisionRequired => "Workflow paused awaiting an explicit operator decision.",
        CryptoShreddingWorkflowState.Completed => "Workflow completed and audit evidence has been recorded.",
        CryptoShreddingWorkflowState.CancelledBeforeDecision => "Workflow cancelled before any irreversible decision was recorded.",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown CryptoShreddingWorkflowState value."),
    };
}
