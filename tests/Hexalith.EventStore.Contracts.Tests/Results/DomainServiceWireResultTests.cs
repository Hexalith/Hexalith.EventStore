using System.Text.Json;

using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Contracts.Tests.Results;

public class DomainServiceWireResultTests {
    [Fact]
    public void DeserializesLegacyWireJsonWithoutResultPayloadAsNull() {
        // Older wire JSON omitted optional resultPayload.
        const string json = """
            {
              "isRejection": false,
              "events": [
                {
                  "eventTypeName": "Hexalith.Sample.CounterIncremented",
                  "payload": "AQID",
                  "serializationFormat": "json"
                }
              ]
            }
            """;

        json.ShouldNotContain("resultPayload");

        DomainServiceWireResult? result = JsonSerializer.Deserialize<DomainServiceWireResult>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        _ = result.ShouldNotBeNull();
        result.ResultPayload.ShouldBeNull();
        result.IsRejection.ShouldBeFalse();
        result.Events.Count.ShouldBe(1);

        DomainServiceWireEvent wireEvent = result.Events[0];
        wireEvent.EventTypeName.ShouldBe("Hexalith.Sample.CounterIncremented");
        wireEvent.Payload.ShouldBe([1, 2, 3]);
        wireEvent.SerializationFormat.ShouldBe("json");
    }
}
