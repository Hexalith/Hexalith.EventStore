using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class SubmitCommandResponseTests {
    [Fact]
    public void Constructor_WithCorrelationId_CreatesInstance() {
        var response = new SubmitCommandResponse("corr-1");

        response.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public void JsonRoundTrip_UsesCamelCaseGatewayContract() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string json = JsonSerializer.Serialize(new SubmitCommandResponse("corr-1"), options);

        json.ShouldBe("{\"correlationId\":\"corr-1\"}");

        SubmitCommandResponse? roundTripped = JsonSerializer.Deserialize<SubmitCommandResponse>(json, options);

        _ = roundTripped.ShouldNotBeNull();
        roundTripped.CorrelationId.ShouldBe("corr-1");
    }
}
