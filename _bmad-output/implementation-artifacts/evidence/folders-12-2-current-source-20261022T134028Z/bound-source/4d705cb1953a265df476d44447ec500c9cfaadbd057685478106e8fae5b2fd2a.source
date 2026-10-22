
using Hexalith.EventStore.Server.Events;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.1: EventPublishResult record tests.
/// </summary>
public class EventPublishResultTests {
    [Fact]
    public void SuccessResult_HasCorrectProperties() {
        var result = new EventPublishResult(true, 5, null);

        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(5);
        result.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void FailureResult_HasCorrectProperties() {
        var result = new EventPublishResult(false, 2, "Connection refused");

        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(2);
        result.FailureReason.ShouldBe("Connection refused");
    }

    [Fact]
    public void EmptySuccessResult_HasZeroCount() {
        var result = new EventPublishResult(true, 0, null);

        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(0);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly() {
        var a = new EventPublishResult(true, 3, null);
        var b = new EventPublishResult(true, 3, null);

        a.ShouldBe(b);
    }
}
