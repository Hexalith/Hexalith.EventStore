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
        wireEvent.MetadataVersion.ShouldBeNull();
        wireEvent.EventContractType.ShouldBeNull();
        wireEvent.PayloadVersion.ShouldBeNull();
    }

    [Fact]
    public void SerializesVersionedEventMembersWithWebJsonNames() {
        var result = new DomainServiceWireResult(
            false,
            [new DomainServiceWireEvent("counter-incremented", [1, 2, 3]) {
                MetadataVersion = 2,
                EventContractType = "counter-incremented",
                PayloadVersion = 3,
            }]) {
            WriterMode = "V2",
            RegistryFingerprint = new string('a', 64),
        };

        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.ShouldContain("\"writerMode\":\"V2\"");
        json.ShouldContain("\"registryFingerprint\":\"" + new string('a', 64) + "\"");
        json.ShouldContain("\"metadataVersion\":2");
        json.ShouldContain("\"eventContractType\":\"counter-incremented\"");
        json.ShouldContain("\"payloadVersion\":3");
    }

    [Fact]
    public void OmitsVersionedMembersForLegacyWireEvents() {
        string json = JsonSerializer.Serialize(
            new DomainServiceWireResult(false, [new DomainServiceWireEvent("Legacy.CounterIncremented", [1])]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.ShouldNotContain("writerMode");
        json.ShouldNotContain("registryFingerprint");
        json.ShouldNotContain("metadataVersion");
        json.ShouldNotContain("eventContractType");
        json.ShouldNotContain("payloadVersion");
    }

    [Fact]
    public void FromDomainResult_StampsTypedVersionTwoAndLeavesTypedVersionOneUnstamped() {
        DomainServiceWireResult produced = DomainServiceWireResult.FromDomainResult(
            DomainResult.Success([new VersionedWireResultTestEvent(2), new LegacyWireResultTestEvent(1)]));

        produced.Events[0].PayloadVersion.ShouldBe(2);
        produced.Events[0].MetadataVersion.ShouldBeNull();
        produced.Events[0].EventTypeName.ShouldBe(typeof(VersionedWireResultTestEvent).FullName);
        produced.Events[1].PayloadVersion.ShouldBeNull();
        produced.Events[1].EventTypeName.ShouldBe(typeof(LegacyWireResultTestEvent).FullName);
    }
}
