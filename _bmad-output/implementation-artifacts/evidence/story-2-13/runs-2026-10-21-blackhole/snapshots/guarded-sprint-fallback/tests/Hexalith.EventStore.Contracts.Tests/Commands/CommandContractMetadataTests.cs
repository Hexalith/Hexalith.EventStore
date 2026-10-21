using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class CommandContractMetadataTests {
    [Fact]
    public void CommandContractMetadata_StoresResolvedValues() {
        var metadata = new CommandContractMetadata("create-counter", "counter");

        metadata.CommandType.ShouldBe("create-counter");
        metadata.Domain.ShouldBe("counter");
    }
}
