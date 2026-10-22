using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Deterministic branch coverage for the outcome-neutral Story 4.5 classifier.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class AppendDurabilityRaceClassifierTests
{
    /// <summary>Gets supported, inconsistent, rejection, and infrastructure classification cases.</summary>
    public static TheoryData<
        AppendDurabilityRaceClassifier.Input,
        string,
        bool,
        bool,
        bool> Cases => new()
        {
            { Input(204, rawDurable: true, actorSurvives: true), "same-key-overwrite-raw-durable-write-lost", true, false, false },
            { Input(204, rawSurvives: true, actorAccepted: true), "same-key-overwrite-actor-acknowledged-write-lost", true, false, false },
            { Input(409, actorSurvives: true), "raw-writer-conflict-rejected", true, false, true },
            { Input(412, actorSurvives: true), "raw-writer-conflict-rejected", true, false, true },
            { Input(500), "raw-writer-infrastructure-error", false, true, false },
            { Input(400), "raw-writer-unrecognized-http-error", false, true, false },
            { Input(null, rawExceptionType: "System.Net.Http.HttpRequestException"), "raw-writer-transport-error", false, true, false },
            { Input(204, actorExceptionType: "System.TimeoutException"), "actor-writer-infrastructure-error", false, true, false },
            { Input(204, actorAccepted: true, finalSequence: 0), "inconsistent-both-acknowledged-total-loss", false, false, false },
            { Input(204, finalSequence: 0), "inconsistent-raw-acknowledged-total-loss", false, false, false },
            { Input(409, actorAccepted: true, finalSequence: 0), "inconsistent-actor-acknowledged-total-loss", false, false, false },
            { Input(409, actorRejected: true, finalSequence: 0), "neither-writer-durable", true, false, true },
            { Input(204, rawSurvives: true, actorSurvives: true, finalSequence: 2, retryCount: 1), "conflict-retry-to-sequence-2", true, false, true },
            { Input(204, rawSurvives: true, actorSurvives: true, finalSequence: 2), "inconsistent-sequence-2-outcome", false, false, false },
            { Input(204, rawSurvives: true, finalSequence: 2, retryCount: 1), "inconsistent-sequence-2-outcome", false, false, false },
            { Input(204, actorSurvives: true, rawDurable: false), "inconsistent-raw-acknowledgement-not-proven-durable", false, false, false },
            { Input(409, actorConflict: true, rawSurvives: true), "actor-writer-conflict-rejected", true, false, true },
            { Input(204, rawSurvives: true, actorSurvives: true), "inconsistent-single-sequence-has-two-writers", false, false, false },
            { Input(204, rawSurvives: true, actorRejected: true), "actor-writer-rejected", true, false, true },
            { Input(204, rawSurvives: true), "raw-writer-survived", true, false, false },
            { Input(409, finalSequence: 3), "inconsistent-final-sequence", false, false, false },
            { Input(409), "inconsistent-final-writer-missing", false, false, false },
        };

    /// <summary>
    /// Verifies every classifier branch is stable and carries its semantic flags. The case table
    /// covers all twenty reachable classification names; the classifier returns no other name.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Classify_ReturnsExpectedOutcome(
        AppendDurabilityRaceClassifier.Input input,
        string expectedName,
        bool expectedConsistency,
        bool expectedInfrastructureFailure,
        bool expectedRecognizedRejection)
    {
        AppendDurabilityRaceClassifier.Result result = AppendDurabilityRaceClassifier.Classify(input);

        result.Name.ShouldBe(expectedName);
        result.IsInternallyConsistent.ShouldBe(expectedConsistency);
        result.IsInfrastructureFailure.ShouldBe(expectedInfrastructureFailure);
        result.RecognizedRejectionOrConflict.ShouldBe(expectedRecognizedRejection);
    }

    private static AppendDurabilityRaceClassifier.Input Input(
        int? rawStatus,
        string? rawExceptionType = null,
        bool rawDurable = false,
        bool rawSurvives = false,
        bool actorSurvives = false,
        bool actorAccepted = false,
        bool actorRejected = false,
        bool actorConflict = false,
        string? actorExceptionType = null,
        long finalSequence = 1,
        int retryCount = 0)
        => new(
            rawStatus,
            rawExceptionType,
            rawDurable,
            rawSurvives,
            actorSurvives,
            actorAccepted,
            actorRejected,
            actorConflict,
            actorExceptionType,
            finalSequence,
            retryCount);
}
