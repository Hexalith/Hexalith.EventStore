namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — declarative table of allowed restored-backup admission transitions.
/// </summary>
public static class RestoredBackupAdmissionTransitions {
    private static readonly Dictionary<RestoredBackupAdmissionState, IReadOnlySet<RestoredBackupAdmissionState>> _allowed
        = new() {
            [RestoredBackupAdmissionState.Pending] = new HashSet<RestoredBackupAdmissionState> {
                RestoredBackupAdmissionState.Accepted,
                RestoredBackupAdmissionState.Blocked,
                RestoredBackupAdmissionState.Quarantined,
                RestoredBackupAdmissionState.OperatorDecisionRequired,
                RestoredBackupAdmissionState.DeferredValidation,
            },
            [RestoredBackupAdmissionState.Accepted] = new HashSet<RestoredBackupAdmissionState>(),
            [RestoredBackupAdmissionState.Blocked] = new HashSet<RestoredBackupAdmissionState>(),
            [RestoredBackupAdmissionState.Quarantined] = new HashSet<RestoredBackupAdmissionState> {
                RestoredBackupAdmissionState.OperatorDecisionRequired,
            },
            [RestoredBackupAdmissionState.OperatorDecisionRequired] = new HashSet<RestoredBackupAdmissionState> {
                RestoredBackupAdmissionState.Accepted,
                RestoredBackupAdmissionState.Blocked,
                RestoredBackupAdmissionState.Quarantined,
            },
            [RestoredBackupAdmissionState.DeferredValidation] = new HashSet<RestoredBackupAdmissionState> {
                RestoredBackupAdmissionState.Pending,
                RestoredBackupAdmissionState.Accepted,
                RestoredBackupAdmissionState.Blocked,
                RestoredBackupAdmissionState.Quarantined,
            },
        };

    private static readonly HashSet<RestoredBackupAdmissionState> _terminal = new() {
        RestoredBackupAdmissionState.Accepted,
        RestoredBackupAdmissionState.Blocked,
    };

    /// <summary>
    /// Returns <see langword="true"/> when transitioning from <paramref name="from"/> to
    /// <paramref name="to"/> is allowed.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The proposed next state.</param>
    /// <returns><see langword="true"/> when the transition is allowed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either state is outside the defined enum.</exception>
    public static bool IsAllowed(RestoredBackupAdmissionState from, RestoredBackupAdmissionState to) {
        if (!_allowed.TryGetValue(from, out IReadOnlySet<RestoredBackupAdmissionState>? allowed)) {
            throw new ArgumentOutOfRangeException(nameof(from), from, "Unknown RestoredBackupAdmissionState value.");
        }

        if (!_allowed.ContainsKey(to)) {
            throw new ArgumentOutOfRangeException(nameof(to), to, "Unknown RestoredBackupAdmissionState value.");
        }

        return allowed.Contains(to);
    }

    /// <summary>Returns <see langword="true"/> when the state is terminal.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> when terminal.</returns>
    public static bool IsTerminal(RestoredBackupAdmissionState state) => _terminal.Contains(state);
}
