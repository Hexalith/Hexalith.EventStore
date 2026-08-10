namespace Hexalith.EventStore.Server.Actors;

/// <summary>Defines the legal durable transitions for an idempotency admission.</summary>
public static class IdempotencyAdmissionStateTransitions
{
    /// <summary>Determines whether a state may transition to another state.</summary>
    /// <param name="from">The persisted source state.</param>
    /// <param name="to">The requested target state.</param>
    /// <returns><see langword="true"/> when the transition is legal.</returns>
    public static bool IsAllowed(IdempotencyAdmissionState from, IdempotencyAdmissionState to)
        => (from, to) switch
        {
            (IdempotencyAdmissionState.Reserved, IdempotencyAdmissionState.Pending)
                or (IdempotencyAdmissionState.Reserved, IdempotencyAdmissionState.Recoverable)
                or (IdempotencyAdmissionState.Pending, IdempotencyAdmissionState.Recoverable)
                or (IdempotencyAdmissionState.Pending, IdempotencyAdmissionState.UnknownProviderOutcome)
                or (IdempotencyAdmissionState.Recoverable, IdempotencyAdmissionState.Pending)
                or (IdempotencyAdmissionState.Recoverable, IdempotencyAdmissionState.UnknownProviderOutcome)
                or (IdempotencyAdmissionState.Recoverable, IdempotencyAdmissionState.Terminal)
                or (IdempotencyAdmissionState.Pending, IdempotencyAdmissionState.Terminal)
                or (IdempotencyAdmissionState.UnknownProviderOutcome, IdempotencyAdmissionState.Terminal)
                => true,
            _ => false,
        };
}
