using Hexalith.EventStore.Contracts.Commands;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public sealed class CommandStatusQueryResponseTests {
    [Theory]
    [InlineData("Rejected", (int)CommandStatus.Rejected, true)]
    [InlineData("Completed", (int)CommandStatus.Rejected, false)]
    [InlineData("Rejected", (int)CommandStatus.Completed, false)]
    [InlineData("rejected", (int)CommandStatus.Completed, false)]
    [InlineData("Completed", (int)CommandStatus.Completed, false)]
    public void IsRejectedRequiresConsistentCanonicalWireRepresentations(
        string status,
        int statusCode,
        bool expected) {
        var response = new CommandStatusQueryResponse("corr-1", status, statusCode);

        response.IsRejected.ShouldBe(expected);
    }
}
