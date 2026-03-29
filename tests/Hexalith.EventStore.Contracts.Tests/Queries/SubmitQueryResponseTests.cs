
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class SubmitQueryResponseTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var response = new SubmitQueryResponse(
            CorrelationId: "corr-123",
            Payload: payload);

        response.CorrelationId.ShouldBe("corr-123");
        response.Payload.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        JsonElement payload = JsonDocument.Parse("42").RootElement;
        var response1 = new SubmitQueryResponse("corr-1", payload);
        var response2 = new SubmitQueryResponse("corr-1", payload);

        response2.ShouldBe(response1);
    }
}
