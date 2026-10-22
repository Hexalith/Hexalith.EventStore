using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class SubmitCommandRequestTests {
    [Fact]
    public void Constructor_WithAllFields_CreatesInstance() {
        JsonElement payload = JsonDocument.Parse("{\"name\":\"Demo\"}").RootElement;
        var request = new SubmitCommandRequest(
            MessageId: "01HX0000000000000000000000",
            Tenant: "tenant-a",
            Domain: "party",
            AggregateId: "party-1",
            CommandType: "CreateParty",
            Payload: payload,
            CorrelationId: "corr-1",
            Extensions: new Dictionary<string, string> { ["source"] = "test" });

        request.MessageId.ShouldBe("01HX0000000000000000000000");
        request.Tenant.ShouldBe("tenant-a");
        request.Domain.ShouldBe("party");
        request.AggregateId.ShouldBe("party-1");
        request.CommandType.ShouldBe("CreateParty");
        request.Payload.GetProperty("name").GetString().ShouldBe("Demo");
        request.CorrelationId.ShouldBe("corr-1");
        _ = request.Extensions.ShouldNotBeNull();
        request.Extensions["source"].ShouldBe("test");
    }

    [Fact]
    public void JsonRoundTrip_UsesCamelCaseGatewayContract() {
        JsonElement payload = JsonDocument.Parse("{\"name\":\"Demo\"}").RootElement;
        var request = new SubmitCommandRequest(
            "message-1",
            "tenant-a",
            "party",
            "party-1",
            "CreateParty",
            payload,
            "message-1");

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string json = JsonSerializer.Serialize(request, options);

        json.ShouldContain("\"messageId\"");
        json.ShouldContain("\"aggregateId\"");

        SubmitCommandRequest? roundTripped = JsonSerializer.Deserialize<SubmitCommandRequest>(json, options);

        _ = roundTripped.ShouldNotBeNull();
        roundTripped.MessageId.ShouldBe("message-1");
        roundTripped.Payload.GetProperty("name").GetString().ShouldBe("Demo");
    }

    [Fact]
    public void JsonRoundTrip_PreservesOnlyOpaqueIdempotencyKeyAuthority() {
        const string opaqueKey = "opaque/retry key with caller-defined bytes";
        const string json = """
            {
              "messageId": "message-1",
              "tenant": "tenant-a",
              "domain": "folders",
              "aggregateId": "folder-1",
              "commandType": "CreateFolderCommand",
              "payload": { "name": "Demo" },
              "correlationId": "correlation-1",
              "idempotencyKey": "opaque/retry key with caller-defined bytes",
              "idempotency": {
                "adapterId": "attacker",
                "operationId": "override",
                "descriptorVersion": 99,
                "canonicalIntent": "AQIDBA==",
                "retentionTier": 2
              }
            }
            """;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        SubmitCommandRequest roundTripped = JsonSerializer
            .Deserialize<SubmitCommandRequest>(json, options)
            .ShouldNotBeNull();

        typeof(SubmitCommandRequest).GetProperty("Idempotency").ShouldBeNull();
        typeof(SubmitCommandRequest).GetProperty("IdempotencyKey").ShouldNotBeNull();
        typeof(SubmitCommandRequest).GetProperty("IdempotencyKey")!.GetValue(roundTripped).ShouldBe(opaqueKey);

        string roundTrippedJson = JsonSerializer.Serialize(roundTripped, options);
        roundTrippedJson.ShouldContain("\"idempotencyKey\"");
        roundTrippedJson.ShouldNotContain("adapterId");
        roundTrippedJson.ShouldNotContain("canonicalIntent");
        roundTrippedJson.ShouldNotContain("retentionTier");
    }
}
