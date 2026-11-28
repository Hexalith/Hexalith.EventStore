using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public sealed class IdempotencyAdmissionRequestCompatibilityTests
{
    [Fact]
    public void TransitionRequestPreservesLegacyShapeAndRoundTripsAuthority()
    {
        var legacy = new IdempotencyAdmissionTransitionRequest(FencingToken: 7);
        legacy.Deconstruct(out long fencingToken);
        fencingToken.ShouldBe(7);
        JsonSerializer.Deserialize<IdempotencyAdmissionTransitionRequest>("""{"FencingToken":7}""")
            .ShouldBe(legacy);

        var authority = new IdempotencyAdmissionAuthorityRequest(
            7, "v1", "execution-message", "execution-correlation", IdempotencyExecutionPurpose.Execute);
        var current = new IdempotencyAdmissionTransitionRequest(7, authority);
        JsonSerializer.Deserialize<IdempotencyAdmissionTransitionRequest>(JsonSerializer.Serialize(current))
            .ShouldBe(current);
    }

    [Fact]
    public void CompletionRequestPreservesLegacyShapeAndRoundTripsAuthority()
    {
        var result = new CommandProcessingResult(Accepted: true);
        var legacy = new IdempotencyAdmissionCompletionRequest(FencingToken: 7, Result: result);
        (long fencingToken, CommandProcessingResult deconstructedResult) = legacy;
        fencingToken.ShouldBe(7);
        deconstructedResult.ShouldBe(result);
        JsonSerializer.Deserialize<IdempotencyAdmissionCompletionRequest>("""{"FencingToken":7,"Result":{"Accepted":true}}""")
            .ShouldBe(legacy);

        var authority = new IdempotencyAdmissionAuthorityRequest(
            7, "v1", "execution-message", "execution-correlation", IdempotencyExecutionPurpose.Execute);
        var current = new IdempotencyAdmissionCompletionRequest(7, result, authority);
        JsonSerializer.Deserialize<IdempotencyAdmissionCompletionRequest>(JsonSerializer.Serialize(current))
            .ShouldBe(current);
    }
}
