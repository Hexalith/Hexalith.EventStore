using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Replay;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Replay;

/// <summary>
/// Tier 1 wire-contract tests for the replay request/result records introduced by
/// admin-ui-aggregate-state-replay-correctness. Both records cross the Dapr boundary as
/// JSON; round-trip parity is the contract.
/// </summary>
public class AggregateReconstructionRoundTripTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Request_RoundTripsAllFields()
    {
        AggregateReconstructionRequest original = new(
            TenantId: "tenant-a",
            Domain: "counter",
            AggregateType: "Counter",
            AggregateId: "counter-1",
            UpToSequence: 18,
            Events: [
                new ReplayEventEnvelope(
                    SequenceNumber: 1,
                    EventTypeName: "CounterIncremented",
                    Payload: Encoding.UTF8.GetBytes("{\"step\":1}"),
                    SerializationFormat: "json",
                    MetadataVersion: 1,
                    MessageId: "msg-1",
                    CorrelationId: "corr-1",
                    CausationId: null),
            ],
            IncludeTimeline: true,
            RequestId: "req-1");

        string json = JsonSerializer.Serialize(original, WebOptions);
        AggregateReconstructionRequest? decoded = JsonSerializer.Deserialize<AggregateReconstructionRequest>(json, WebOptions);

        _ = decoded.ShouldNotBeNull();
        decoded!.TenantId.ShouldBe(original.TenantId);
        decoded.Domain.ShouldBe(original.Domain);
        decoded.AggregateType.ShouldBe(original.AggregateType);
        decoded.AggregateId.ShouldBe(original.AggregateId);
        decoded.UpToSequence.ShouldBe(original.UpToSequence);
        decoded.IncludeTimeline.ShouldBe(original.IncludeTimeline);
        decoded.RequestId.ShouldBe(original.RequestId);
        decoded.Events.Count.ShouldBe(1);
        decoded.Events[0].SequenceNumber.ShouldBe(1);
        decoded.Events[0].EventTypeName.ShouldBe("CounterIncremented");
        decoded.Events[0].Payload.ShouldBe(original.Events[0].Payload);
        decoded.Events[0].SerializationFormat.ShouldBe("json");
        decoded.Events[0].MetadataVersion.ShouldBe(1);
        decoded.Events[0].MessageId.ShouldBe("msg-1");
        decoded.Events[0].CorrelationId.ShouldBe("corr-1");
        decoded.Events[0].CausationId.ShouldBeNull();
    }

    [Fact]
    public void Result_Succeeded_RoundTripsTimeline()
    {
        AggregateReconstructionResult original = AggregateReconstructionResult.Succeeded(
            stateJson: "{\"count\":10,\"isTerminated\":false}",
            lastAppliedSequenceNumber: 18,
            timeline: [
                new AggregateReconstructionTimelineEntry(1, "CounterIncremented", "{\"count\":1}"),
                new AggregateReconstructionTimelineEntry(18, "CounterIncremented", "{\"count\":10,\"isTerminated\":false}"),
            ]);

        string json = JsonSerializer.Serialize(original, WebOptions);
        AggregateReconstructionResult? decoded = JsonSerializer.Deserialize<AggregateReconstructionResult>(json, WebOptions);

        _ = decoded.ShouldNotBeNull();
        decoded!.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        decoded.StateJson.ShouldBe(original.StateJson);
        decoded.LastAppliedSequenceNumber.ShouldBe(18);
        decoded.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.None);
        decoded.FailedSequenceNumber.ShouldBeNull();
        decoded.FailedEventType.ShouldBeNull();
        decoded.Message.ShouldBeNull();
        _ = decoded.Timeline.ShouldNotBeNull();
        decoded.Timeline!.Count.ShouldBe(2);
        decoded.Timeline[1].SequenceNumber.ShouldBe(18);
        decoded.Timeline[1].EventTypeName.ShouldBe("CounterIncremented");
    }

    [Fact]
    public void Result_Partial_PreservesDiagnosticsAndStateUpToLastGood()
    {
        AggregateReconstructionResult original = AggregateReconstructionResult.Partial(
            stateJson: "{\"count\":3}",
            lastAppliedSequenceNumber: 3,
            failedSequenceNumber: 4,
            failedEventType: "BogusEvent",
            errorCategory: AggregateReconstructionErrorCategory.ApplyFailed,
            message: "Apply threw NRE.");

        string json = JsonSerializer.Serialize(original, WebOptions);
        AggregateReconstructionResult? decoded = JsonSerializer.Deserialize<AggregateReconstructionResult>(json, WebOptions);

        _ = decoded.ShouldNotBeNull();
        decoded!.Status.ShouldBe(AggregateReconstructionStatus.Partial);
        decoded.StateJson.ShouldBe("{\"count\":3}");
        decoded.LastAppliedSequenceNumber.ShouldBe(3);
        decoded.FailedSequenceNumber.ShouldBe(4);
        decoded.FailedEventType.ShouldBe("BogusEvent");
        decoded.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.ApplyFailed);
        decoded.Message.ShouldBe("Apply threw NRE.");
    }

    [Fact]
    public void Result_Failed_OmitsStateAndPreservesDiagnostics()
    {
        AggregateReconstructionResult original = AggregateReconstructionResult.Failed(
            errorCategory: AggregateReconstructionErrorCategory.UnknownAggregateType,
            message: "No domain registered.",
            failedSequenceNumber: 1,
            failedEventType: "Unknown");

        string json = JsonSerializer.Serialize(original, WebOptions);
        AggregateReconstructionResult? decoded = JsonSerializer.Deserialize<AggregateReconstructionResult>(json, WebOptions);

        _ = decoded.ShouldNotBeNull();
        decoded!.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        decoded.StateJson.ShouldBeNull();
        decoded.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownAggregateType);
        decoded.FailedSequenceNumber.ShouldBe(1);
        decoded.Message.ShouldBe("No domain registered.");
        decoded.Timeline.ShouldBeNull();
    }

    [Fact]
    public void Status_HasExpectedDiscriminants_ForRfc7807Mapping()
    {
        // ProblemDetails mapping in AdminStreamQueryController depends on these enum values.
        ((int)AggregateReconstructionStatus.Succeeded).ShouldBe(0);
        ((int)AggregateReconstructionStatus.Partial).ShouldBe(1);
        ((int)AggregateReconstructionStatus.Failed).ShouldBe(2);
    }

    [Fact]
    public void ErrorCategory_HasExpectedDiscriminants_ForRfc7807Mapping()
    {
        ((int)AggregateReconstructionErrorCategory.None).ShouldBe(0);
        ((int)AggregateReconstructionErrorCategory.UnknownAggregateType).ShouldBe(1);
        ((int)AggregateReconstructionErrorCategory.UnknownEventType).ShouldBe(2);
        ((int)AggregateReconstructionErrorCategory.DeserializationFailed).ShouldBe(3);
        ((int)AggregateReconstructionErrorCategory.ApplyHandlerMissing).ShouldBe(4);
        ((int)AggregateReconstructionErrorCategory.ApplyFailed).ShouldBe(5);
        ((int)AggregateReconstructionErrorCategory.UnsupportedVersion).ShouldBe(6);
        ((int)AggregateReconstructionErrorCategory.Unexpected).ShouldBe(7);
    }
}
