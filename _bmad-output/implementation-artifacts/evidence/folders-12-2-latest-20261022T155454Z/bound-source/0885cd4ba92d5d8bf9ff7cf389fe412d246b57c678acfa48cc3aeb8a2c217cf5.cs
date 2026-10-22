using System.Text.Json;

using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Contracts.Tests.Replay;

/// <summary>Verifies additive replay outcomes preserve legacy positional and wire contracts.</summary>
public sealed class ReplayEvolutionOutcomeTests
{
    /// <summary>Checks all new category values and exact typed reasons survive a wire round trip.</summary>
    [Theory]
    [InlineData(AggregateReconstructionErrorCategory.Hold, 8, "TimelineEvidenceHold")]
    [InlineData(AggregateReconstructionErrorCategory.Limit, 9, "LegacyArrayLimit")]
    [InlineData(AggregateReconstructionErrorCategory.Conflict, 10, "ReplayScalarInvalid")]
    public void NewOutcomesRoundTripWithoutStateOrProgress(AggregateReconstructionErrorCategory category, int value, string reason)
    {
        ((int)category).ShouldBe(value);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        AggregateReconstructionResult original = AggregateReconstructionResult.Failed(category, "Safe failure.") with { ReasonCode = reason };
        string wire = JsonSerializer.Serialize(original, options);
        AggregateReconstructionResult decoded = JsonSerializer.Deserialize<AggregateReconstructionResult>(wire, options)!;
        decoded.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        decoded.ErrorCategory.ShouldBe(category);
        decoded.ReasonCode.ShouldBe(reason);
        decoded.StateJson.ShouldBeNull();
        decoded.Timeline.ShouldBeNull();
        decoded.PagedProgress.ShouldBeNull();
    }

    /// <summary>Checks an old result keeps its eight-member deconstruction and omits new null wire members.</summary>
    [Fact]
    public void LegacyResultRetainsDeconstructionAndOmitsNullReason()
    {
        var original = new AggregateReconstructionResult(AggregateReconstructionStatus.Failed, null, 0, null, null,
            AggregateReconstructionErrorCategory.Unexpected, "safe", null);
        var (status, state, applied, failed, eventType, category, message, timeline) = original;
        status.ShouldBe(AggregateReconstructionStatus.Failed);
        state.ShouldBeNull();
        applied.ShouldBe(0);
        failed.ShouldBeNull();
        eventType.ShouldBeNull();
        category.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        message.ShouldBe("safe");
        timeline.ShouldBeNull();
        string wire = JsonSerializer.Serialize(original, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        wire.ShouldNotContain("reasonCode");
        wire.ShouldNotContain("pagedProgress");
    }
}
