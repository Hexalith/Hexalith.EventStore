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
        request.Extensions.ShouldNotBeNull();
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

        roundTripped.ShouldNotBeNull();
        roundTripped.MessageId.ShouldBe("message-1");
        roundTripped.Payload.GetProperty("name").GetString().ShouldBe("Demo");
    }
}
