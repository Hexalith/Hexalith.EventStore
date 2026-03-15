
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

// --- Test stub types implementing IQueryResponse<T> ---

internal record CounterDto(int Count);

internal sealed record OrderDto(string OrderId, string Status);

internal sealed record DerivedCounterDto(int Count, string Label) : CounterDto(Count);

internal sealed class CounterQueryResponse : IQueryResponse<CounterDto> {
    public CounterDto Data { get; init; } = new(0);

    public string ProjectionType => "counter";
}

internal sealed class OrderQueryResponse : IQueryResponse<OrderDto> {
    public OrderDto Data { get; init; } = new("", "");

    public string ProjectionType => "order-list";
}

public class IQueryResponseTests {
    [Fact]
    public void IQueryResponse_ConcreteImplementation_HasMandatoryProjectionType() {
        var response = new CounterQueryResponse { Data = new CounterDto(42) };

        Assert.Equal(42, response.Data.Count);
        Assert.Equal("counter", response.ProjectionType);
    }

    [Fact]
    public void IQueryResponse_DifferentProjectionType_ReturnsCorrectValue() {
        var response = new OrderQueryResponse { Data = new OrderDto("ord-1", "shipped") };

        Assert.Equal("order-list", response.ProjectionType);
        Assert.Equal("ord-1", response.Data.OrderId);
        Assert.Equal("shipped", response.Data.Status);
    }

    [Fact]
    public void IQueryResponse_Covariance_DerivedTypeAssignableToBase() {
        // IQueryResponse<DerivedCounterDto> should be assignable to IQueryResponse<CounterDto>
        IQueryResponse<DerivedCounterDto> derived = new DerivedCounterQueryResponse();
        IQueryResponse<CounterDto> baseRef = derived; // covariance

        _ = Assert.IsAssignableFrom<IQueryResponse<CounterDto>>(derived);
        Assert.Equal("counter", baseRef.ProjectionType);
        Assert.Equal(99, baseRef.Data.Count);
    }

    private sealed class DerivedCounterQueryResponse : IQueryResponse<DerivedCounterDto> {
        public DerivedCounterDto Data => new(99, "test");

        public string ProjectionType => "counter";
    }
}
