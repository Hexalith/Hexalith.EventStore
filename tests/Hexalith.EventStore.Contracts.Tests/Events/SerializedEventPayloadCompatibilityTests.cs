using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class SerializedEventPayloadCompatibilityTests {
    [Fact]
    public void LegacyImplementationReceivesNullVersionDefaults() {
        ISerializedEventPayload payload = new LegacySerializedPayload("Legacy.Event", [1], "json");

        payload.MetadataVersion.ShouldBeNull();
        payload.EventContractType.ShouldBeNull();
        payload.PayloadVersion.ShouldBeNull();
    }

}
