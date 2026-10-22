using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Tests.Formatting;

/// <summary>
/// P9 / P10 / P11 — EventDetailJsonConverter contract tests covering missing-both fields,
/// non-Object payload kinds, and malformed descriptor handling.
/// </summary>
public class EventDetailJsonConverterContractTests {
    [Fact]
    public void Read_ThrowsJsonException_WhenNeitherPayloadNorPayloadJson() {
        // P9 — converter must not silently fabricate "{}" when both representations are missing.
        string json = """
            {
              "tenantId":"t1","domain":"orders","aggregateId":"o1","sequenceNumber":1,
              "eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z","correlationId":"c1"
            }
            """;

        JsonException ex = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<EventDetail>(json, JsonDefaults.Options));

        ex.Message.ShouldContain("payload");
    }

    [Fact]
    public void Read_ThrowsJsonException_WhenPayloadIsArray() {
        // P10 — descriptor 'payload' must be a JSON object; arrays/strings/numbers should fail loudly.
        string json = """
            {
              "tenantId":"t1","domain":"orders","aggregateId":"o1","sequenceNumber":1,
              "eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z","correlationId":"c1",
              "payload":[1,2,3]
            }
            """;

        JsonException ex = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<EventDetail>(json, JsonDefaults.Options));

        ex.Message.ShouldContain("payload");
        ex.Message.ShouldContain("object");
    }

    [Fact]
    public void Read_ThrowsWrappedJsonException_WhenDescriptorMalformed() {
        // P11 — malformed AdminRedactedContent inside 'payload' surfaces a safe contract error.
        string json = """
            {
              "tenantId":"t1","domain":"orders","aggregateId":"o1","sequenceNumber":1,
              "eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z","correlationId":"c1",
              "payload":{"isRedacted":"not-a-bool"}
            }
            """;

        JsonException ex = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<EventDetail>(json, JsonDefaults.Options));

        ex.Message.ShouldContain("descriptor");
    }
}
