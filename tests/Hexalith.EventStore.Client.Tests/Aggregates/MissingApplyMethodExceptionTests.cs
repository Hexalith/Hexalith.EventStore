using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

public class MissingApplyMethodExceptionTests {
    private sealed class PlainState {
        public int Value { get; private set; }
    }

    private sealed class TerminatableState : ITerminatable {
        public bool IsTerminated { get; private set; }
    }

    [Fact]
    public void Constructor_PreservesAllProperties() {
        var ex = new MissingApplyMethodException(
            stateType: typeof(PlainState),
            eventTypeName: "ItemAdded",
            messageId: "01HQ7K8N9PXYZ",
            aggregateId: "agg-42");

        Assert.Equal(typeof(PlainState), ex.StateType);
        Assert.Equal("ItemAdded", ex.EventTypeName);
        Assert.Equal("01HQ7K8N9PXYZ", ex.MessageId);
        Assert.Equal("agg-42", ex.AggregateId);
    }

    [Fact]
    public void Constructor_OptionalContextDefaultsToNull() {
        var ex = new MissingApplyMethodException(typeof(PlainState), "ItemAdded");

        Assert.Null(ex.MessageId);
        Assert.Null(ex.AggregateId);
    }

    [Fact]
    public void Message_ContainsStateTypeAndEventTypeName() {
        var ex = new MissingApplyMethodException(typeof(PlainState), "OrderShipped");

        Assert.Contains(typeof(PlainState).FullName!, ex.Message);
        Assert.Contains("OrderShipped", ex.Message);
        Assert.Contains("Apply(OrderShipped)", ex.Message);
    }

    [Fact]
    public void Message_IncludesAggregateAndMessageIds_WhenProvided() {
        var ex = new MissingApplyMethodException(
            stateType: typeof(PlainState),
            eventTypeName: "ItemAdded",
            messageId: "msg-123",
            aggregateId: "agg-7");

        Assert.Contains("agg-7", ex.Message);
        Assert.Contains("msg-123", ex.Message);
    }

    [Fact]
    public void Message_OmitsContextSuffix_WhenIdsAreNull() {
        var ex = new MissingApplyMethodException(typeof(PlainState), "ItemAdded");

        Assert.DoesNotContain("MessageId=", ex.Message);
        Assert.DoesNotContain("AggregateId=", ex.Message);
    }

    [Fact]
    public void Message_IncludesTombstoningHint_ForTerminatableState() {
        var ex = new MissingApplyMethodException(typeof(TerminatableState), "ItemAdded");

        Assert.Contains("ITerminatable", ex.Message);
        Assert.Contains(nameof(AggregateTerminated), ex.Message);
    }

    [Fact]
    public void Message_IncludesTombstoningHint_ForAggregateTerminatedEvent() {
        var ex = new MissingApplyMethodException(typeof(PlainState), nameof(AggregateTerminated));

        Assert.Contains("ITerminatable", ex.Message);
        Assert.Contains(nameof(AggregateTerminated), ex.Message);
    }

    [Fact]
    public void Message_OmitsTombstoningHint_ForNonTerminatableStateAndUnrelatedEvent() {
        var ex = new MissingApplyMethodException(typeof(PlainState), "OrderShipped");

        Assert.DoesNotContain("ITerminatable", ex.Message);
    }

    [Fact]
    public void Exception_IsAssignableToInvalidOperationException() {
        var ex = new MissingApplyMethodException(typeof(PlainState), "ItemAdded");

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public void Constructor_NullStateType_Throws() {
        _ = Assert.Throws<ArgumentNullException>(
            () => new MissingApplyMethodException(stateType: null!, eventTypeName: "ItemAdded"));
    }

    [Fact]
    public void Constructor_WhitespaceEventTypeName_Throws() {
        _ = Assert.Throws<ArgumentException>(
            () => new MissingApplyMethodException(typeof(PlainState), "  "));
    }
}
