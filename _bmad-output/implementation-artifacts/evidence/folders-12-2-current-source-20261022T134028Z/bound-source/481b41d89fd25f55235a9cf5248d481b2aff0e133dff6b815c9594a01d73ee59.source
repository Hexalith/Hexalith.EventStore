using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class EventContractMetadataTests {
    [Fact]
    public void EventContractMetadata_StoresResolvedValues() {
        var metadata = new EventContractMetadata("counter-created", "counter");

        metadata.EventType.ShouldBe("counter-created");
        metadata.Domain.ShouldBe("counter");
    }
}
