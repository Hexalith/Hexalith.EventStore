using System;
using System.Collections.Generic;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — declarative table of allowed crypto-shredding workflow state transitions. Any
/// transition not listed here is rejected as an idempotent replay (existing status returned) or as
/// an invalid operation. Terminal states (<see cref="CryptoShreddingWorkflowState.Invalidated"/>,
/// <see cref="CryptoShreddingWorkflowState.Deleted"/>, <see cref="CryptoShreddingWorkflowState.Rejected"/>,
/// <see cref="CryptoShreddingWorkflowState.Completed"/>, and
/// <see cref="CryptoShreddingWorkflowState.CancelledBeforeDecision"/>) cannot transition further.
/// </summary>
public static class CryptoShreddingWorkflowTransitions {
    private static readonly Dictionary<CryptoShreddingWorkflowState, IReadOnlySet<CryptoShreddingWorkflowState>> _allowed
        = new() {
            [CryptoShreddingWorkflowState.Requested] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.Approved,
                CryptoShreddingWorkflowState.Rejected,
                CryptoShreddingWorkflowState.CancelledBeforeDecision,
            },
            [CryptoShreddingWorkflowState.Approved] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.PendingProvider,
                CryptoShreddingWorkflowState.CancelledBeforeDecision,
            },
            [CryptoShreddingWorkflowState.PendingProvider] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.Invalidated,
                CryptoShreddingWorkflowState.Deleted,
                CryptoShreddingWorkflowState.VerificationFailed,
            },
            [CryptoShreddingWorkflowState.Invalidated] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.Completed,
                CryptoShreddingWorkflowState.RestoreConflict,
            },
            [CryptoShreddingWorkflowState.Deleted] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.Completed,
                CryptoShreddingWorkflowState.RestoreConflict,
            },
            [CryptoShreddingWorkflowState.VerificationFailed] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.OperatorDecisionRequired,
            },
            [CryptoShreddingWorkflowState.RestoreConflict] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.OperatorDecisionRequired,
            },
            [CryptoShreddingWorkflowState.OperatorDecisionRequired] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.Quarantined,
                CryptoShreddingWorkflowState.Completed,
            },
            [CryptoShreddingWorkflowState.Quarantined] = new HashSet<CryptoShreddingWorkflowState> {
                CryptoShreddingWorkflowState.OperatorDecisionRequired,
            },
            [CryptoShreddingWorkflowState.Rejected] = new HashSet<CryptoShreddingWorkflowState>(),
            [CryptoShreddingWorkflowState.Completed] = new HashSet<CryptoShreddingWorkflowState>(),
            [CryptoShreddingWorkflowState.CancelledBeforeDecision] = new HashSet<CryptoShreddingWorkflowState>(),
        };

    private static readonly HashSet<CryptoShreddingWorkflowState> _terminal = new() {
        CryptoShreddingWorkflowState.Rejected,
        CryptoShreddingWorkflowState.Completed,
        CryptoShreddingWorkflowState.CancelledBeforeDecision,
    };

    private static readonly HashSet<CryptoShreddingWorkflowState> _irreversibleDecision = new() {
        CryptoShreddingWorkflowState.Invalidated,
        CryptoShreddingWorkflowState.Deleted,
    };

    /// <summary>
    /// Returns <see langword="true"/> when transitioning from <paramref name="from"/> to
    /// <paramref name="to"/> is allowed by the state machine.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The proposed next state.</param>
    /// <returns><see langword="true"/> when the transition is allowed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either state is outside the defined enum.</exception>
    public static bool IsAllowed(CryptoShreddingWorkflowState from, CryptoShreddingWorkflowState to) {
        if (!_allowed.TryGetValue(from, out IReadOnlySet<CryptoShreddingWorkflowState>? allowed)) {
            throw new ArgumentOutOfRangeException(nameof(from), from, "Unknown CryptoShreddingWorkflowState value.");
        }

        if (!_allowed.ContainsKey(to)) {
            throw new ArgumentOutOfRangeException(nameof(to), to, "Unknown CryptoShreddingWorkflowState value.");
        }

        return allowed.Contains(to);
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="state"/> cannot transition further.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> when terminal.</returns>
    public static bool IsTerminal(CryptoShreddingWorkflowState state) => _terminal.Contains(state);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="state"/> represents an irreversible
    /// crypto-shredding decision (key invalidated or deleted). Cancellation after this point cannot
    /// undo the decision.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> when the decision is irreversible.</returns>
    public static bool IsIrreversibleDecision(CryptoShreddingWorkflowState state) => _irreversibleDecision.Contains(state);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="state"/> permits cancellation that
    /// transitions to <see cref="CryptoShreddingWorkflowState.CancelledBeforeDecision"/>.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> when cancellation is allowed.</returns>
    public static bool IsCancellable(CryptoShreddingWorkflowState state) => state is
        CryptoShreddingWorkflowState.Requested
        or CryptoShreddingWorkflowState.Approved;
}
