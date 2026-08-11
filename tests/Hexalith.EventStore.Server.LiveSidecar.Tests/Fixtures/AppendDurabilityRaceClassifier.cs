namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Classifies bounded Story 4.5 race observations without depending on a live sidecar.</summary>
public static class AppendDurabilityRaceClassifier
{
    /// <summary>Input facts captured after both contenders quiesce.</summary>
    /// <param name="RawHttpStatus">The raw writer HTTP status, or null when no response arrived.</param>
    /// <param name="RawExceptionType">The raw writer transport exception type, if any.</param>
    /// <param name="RawDurabilityProven">Whether Redis contained the exact raw contender while gated.</param>
    /// <param name="RawSurvives">Whether the exact raw contender remains in final Redis state.</param>
    /// <param name="ActorSurvives">Whether the exact actor contender remains in final Redis state.</param>
    /// <param name="ActorAccepted">Whether the actor acknowledged the command.</param>
    /// <param name="ActorRejected">Whether the actor returned a non-accepted result.</param>
    /// <param name="ActorConflictSignalled">Whether the actor returned or threw a recognized conflict.</param>
    /// <param name="ActorExceptionType">The actor exception type, if any.</param>
    /// <param name="FinalSequence">The final metadata sequence, or zero when metadata is absent.</param>
    /// <param name="RetryCount">The derived allocation retry count.</param>
    public sealed record Input(
        int? RawHttpStatus,
        string? RawExceptionType,
        bool RawDurabilityProven,
        bool RawSurvives,
        bool ActorSurvives,
        bool ActorAccepted,
        bool ActorRejected,
        bool ActorConflictSignalled,
        string? ActorExceptionType,
        long FinalSequence,
        int RetryCount);

    /// <summary>Classification plus consistency and infrastructure-failure flags.</summary>
    /// <param name="Name">Stable evidence classification.</param>
    /// <param name="IsInternallyConsistent">Whether the facts form a supported coherent outcome.</param>
    /// <param name="IsInfrastructureFailure">Whether transport or infrastructure prevented a race conclusion.</param>
    /// <param name="RecognizedRejectionOrConflict">Whether a writer was rejected through a recognized surface.</param>
    public sealed record Result(
        string Name,
        bool IsInternallyConsistent,
        bool IsInfrastructureFailure,
        bool RecognizedRejectionOrConflict);

    /// <summary>Classifies one final observation.</summary>
    /// <param name="input">The captured facts.</param>
    /// <returns>The deterministic classification.</returns>
    public static Result Classify(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);

        bool rawResponseReceived = input.RawHttpStatus is not null;
        bool rawSucceeded = input.RawHttpStatus is >= 200 and < 300;
        bool rawConflictRejected = input.RawHttpStatus is 409 or 412;
        bool rawInfrastructureFailure = input.RawExceptionType is not null
            || !rawResponseReceived
            || input.RawHttpStatus >= 500
            || (!rawSucceeded && !rawConflictRejected);
        bool actorInfrastructureFailure = input.ActorExceptionType is not null
            && !input.ActorConflictSignalled;

        if (rawInfrastructureFailure)
        {
            string name = input.RawExceptionType is not null || !rawResponseReceived
                ? "raw-writer-transport-error"
                : input.RawHttpStatus >= 500
                    ? "raw-writer-infrastructure-error"
                    : "raw-writer-unrecognized-http-error";
            return new Result(name, false, true, false);
        }

        if (actorInfrastructureFailure)
        {
            return new Result("actor-writer-infrastructure-error", false, true, false);
        }

        if (input.FinalSequence is < 0 or > 2)
        {
            return new Result("inconsistent-final-sequence", false, false, false);
        }

        if (input.FinalSequence == 0)
        {
            if (rawSucceeded || input.ActorAccepted)
            {
                string acknowledgedLoss = rawSucceeded && input.ActorAccepted
                    ? "inconsistent-both-acknowledged-total-loss"
                    : rawSucceeded
                        ? "inconsistent-raw-acknowledged-total-loss"
                        : "inconsistent-actor-acknowledged-total-loss";
                return new Result(acknowledgedLoss, false, false, false);
            }

            return new Result(
                "neither-writer-durable",
                true,
                false,
                rawConflictRejected || input.ActorRejected || input.ActorConflictSignalled);
        }

        if (input.FinalSequence == 2)
        {
            if (input.RawSurvives && input.ActorSurvives && input.RetryCount >= 1)
            {
                return new Result("conflict-retry-to-sequence-2", true, false, true);
            }

            return new Result("inconsistent-sequence-2-outcome", false, false, false);
        }

        if (input.RawSurvives && input.ActorSurvives)
        {
            return new Result("inconsistent-single-sequence-has-two-writers", false, false, false);
        }

        if (input.RawSurvives)
        {
            if (input.ActorAccepted)
            {
                return new Result(
                    "same-key-overwrite-actor-acknowledged-write-lost",
                    true,
                    false,
                    false);
            }

            if (input.ActorConflictSignalled)
            {
                return new Result("actor-writer-conflict-rejected", true, false, true);
            }

            return new Result(
                input.ActorRejected ? "actor-writer-rejected" : "raw-writer-survived",
                true,
                false,
                input.ActorRejected);
        }

        if (input.ActorSurvives)
        {
            if (input.RawDurabilityProven)
            {
                return new Result("same-key-overwrite-raw-durable-write-lost", true, false, false);
            }

            if (rawSucceeded)
            {
                return new Result("inconsistent-raw-acknowledgement-not-proven-durable", false, false, false);
            }

            // Reaching here requires a recognized 409/412 raw rejection: the infrastructure gate
            // above already returned for every other non-2xx shape, and both 2xx cases are
            // handled by the two branches immediately above.
            return new Result("raw-writer-conflict-rejected", true, false, true);
        }

        return new Result("inconsistent-final-writer-missing", false, false, false);
    }
}
