using Hexalith.EventStore.Contracts.Commands;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public sealed class CommandStatusQueryResponseTests {
    [Theory]
    [InlineData("Rejected", (int)CommandStatus.Rejected, "OrderRejected", null, null, true)]
    [InlineData("Rejected", (int)CommandStatus.Rejected, "OrderRejected", null, false, true)]
    [InlineData("Rejected", (int)CommandStatus.Rejected, "OrderRejected", null, true, false)]
    [InlineData("Rejected", (int)CommandStatus.Rejected, null, "BackpressureExceeded", true, false)]
    [InlineData("Rejected", (int)CommandStatus.Rejected, "OrderRejected", "Contradictory", false, false)]
    [InlineData("Rejected", (int)CommandStatus.Rejected, null, null, null, false)]
    [InlineData("Completed", (int)CommandStatus.Rejected, "OrderRejected", null, null, false)]
    [InlineData("Rejected", (int)CommandStatus.Completed, "OrderRejected", null, null, false)]
    [InlineData("rejected", (int)CommandStatus.Completed, "OrderRejected", null, null, false)]
    [InlineData("Completed", (int)CommandStatus.Completed, null, null, null, false)]
    public void IsRejectedRequiresConsistentCanonicalWireRepresentations(
        string status,
        int statusCode,
        string? rejectionEventType,
        string? failureReason,
        bool? retryable,
        bool expected) {
        var response = new CommandStatusQueryResponse(
            "corr-1",
            status,
            statusCode,
            RejectionEventType: rejectionEventType) {
            FailureReason = failureReason,
            Retryable = retryable,
        };

        response.IsRejected.ShouldBe(expected);
    }
}
