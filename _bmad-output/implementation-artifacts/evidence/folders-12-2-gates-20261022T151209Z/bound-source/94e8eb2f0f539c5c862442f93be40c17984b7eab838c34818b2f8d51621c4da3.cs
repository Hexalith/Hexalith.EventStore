using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Testing.Security;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class AdminRedactedContentSerializationTests {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void EventDetail_RedactedPayloadSerialization_OmitsRawPayloadJsonAndKeepsSafeDescriptor() {
        var detail = EventDetail.WithRedactedPayload(
            "tenant-a",
            "Counter",
            "agg-1",
            5,
            "CounterIncremented",
            DateTimeOffset.Parse("2026-05-18T12:00:00Z"),
            "corr-1",
            null,
            "user-1",
            Redacted("event-payload", sequenceNumber: 5, correlationId: "corr-1"));

        string json = JsonSerializer.Serialize(detail, JsonOptions);

        ProtectedDataLeakSentinel.AssertNoLeak([json]);
        json.ShouldNotContain("payloadJson");
        json.ShouldContain("payload");
        json.ShouldContain("event-payload");
        json.ShouldContain("provider-unavailable");
        json.ShouldContain("Check provider health and retry after the protected-data provider is available.");
    }

    [Fact]
    public void RepresentativeAdminResponseGraphSerialization_OmitsRawBearingPropertiesWhenRedacted() {
        var graph = new {
            Event = EventDetail.WithRedactedPayload("tenant-a", "Counter", "agg-1", 5, "CounterIncremented", DateTimeOffset.UtcNow, "corr-1", null, null, Redacted("event-payload")),
            State = AggregateStateSnapshot.WithRedactedState("tenant-a", "Counter", "agg-1", 5, DateTimeOffset.UtcNow, Redacted("aggregate-state")),
            Step = EventStepFrame.WithRedactedContent("tenant-a", "Counter", "agg-1", 5, "CounterIncremented", DateTimeOffset.UtcNow, "corr-1", "cause-1", "user-1", Redacted("event-payload"), Redacted("aggregate-state"), [FieldChange.Redacted("Count", Redacted("field-old-value"), Redacted("field-new-value"))], 6),
            Sandbox = SandboxResult.WithRedactedContent("tenant-a", "Counter", "agg-1", 5, "IncrementCounter", "error", [SandboxEvent.Redacted(0, "CounterRejected", true, Redacted("sandbox-event-payload"))], Redacted("sandbox-resulting-state"), [FieldChange.Redacted("Count", Redacted("field-old-value"), Redacted("field-new-value"))], Redacted("sandbox-error"), 12),
            DeadLetter = DeadLetterEntry.WithRedactedFailure("msg-1", "tenant-a", "Counter", "agg-1", "corr-1", Redacted("dead-letter-failure"), DateTimeOffset.UtcNow, 3, "IncrementCounter"),
            Projection = ProjectionError.WithRedactedMessage(10, DateTimeOffset.UtcNow, Redacted("projection-error"), "CounterIncremented")
        };

        string json = JsonSerializer.Serialize(graph, JsonOptions);

        ProtectedDataLeakSentinel.AssertNoLeak([json]);
        foreach (string forbidden in new[] { "\"payloadJson\"", "\"stateJson\"", "\"eventPayloadJson\"", "\"resultingStateJson\"", "\"failureReason\"", "\"errorMessage\"", "\"message\"" }) {
            json.ShouldNotContain(forbidden);
        }

        json.ShouldContain("reasonCode");
        json.ShouldContain("safeNextAction");
        json.ShouldContain("tenant-a");
        json.ShouldContain("Counter");
        json.ShouldContain("agg-1");
    }

    private static AdminRedactedContent Redacted(
        string contentKind,
        long? sequenceNumber = null,
        string? correlationId = null)
        => AdminRedactedContent.Protected(
            contentKind,
            reasonCode: "provider-unavailable",
            stage: "admin-api",
            metadataVersion: 1,
            retryable: true,
            permanent: false,
            safeNextAction: "Check provider health and retry after the protected-data provider is available.",
            tenantId: "tenant-a",
            domain: "Counter",
            aggregateId: "agg-1",
            sequenceNumber: sequenceNumber,
            correlationId: correlationId);
}
