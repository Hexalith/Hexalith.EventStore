
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

// --- Test stub types implementing IQueryContract ---

internal class GetCounterStatusQuery : IQueryContract {
    public static string QueryType => "get-counter-status";
    public static string Domain => "counter";
    public static string ProjectionType => "counter";
}

internal class GetOrderSummaryQuery : IQueryContract {
    public static string QueryType => "get-order-summary";
    public static string Domain => "reporting";
    public static string ProjectionType => "order-summary";
}

public class IQueryContractTests {
    [Fact]
    public void IQueryContract_StaticMembers_AreAccessible() {
        Assert.Equal("get-counter-status", GetCounterStatusQuery.QueryType);
        Assert.Equal("counter", GetCounterStatusQuery.Domain);
        Assert.Equal("counter", GetCounterStatusQuery.ProjectionType);
    }

    [Fact]
    public void IQueryContract_CrossDomainQuery_DomainDiffersFromProjectionType() {
        Assert.Equal("get-order-summary", GetOrderSummaryQuery.QueryType);
        Assert.Equal("reporting", GetOrderSummaryQuery.Domain);
        Assert.Equal("order-summary", GetOrderSummaryQuery.ProjectionType);
        Assert.NotEqual(GetOrderSummaryQuery.Domain, GetOrderSummaryQuery.ProjectionType);
    }

    [Fact]
    public void QueryContractMetadata_RecordEquality_WorksCorrectly() {
        var meta1 = new QueryContractMetadata("get-counter-status", "counter", "counter");
        var meta2 = new QueryContractMetadata("get-counter-status", "counter", "counter");

        Assert.Equal(meta1, meta2);
    }

    [Fact]
    public void QueryContractMetadata_DifferentValues_AreNotEqual() {
        var meta1 = new QueryContractMetadata("get-counter-status", "counter", "counter");
        var meta2 = new QueryContractMetadata("get-order-summary", "reporting", "order-summary");

        Assert.NotEqual(meta1, meta2);
    }

    [Fact]
    public void QueryContractMetadata_IsImmutable() {
        var meta = new QueryContractMetadata("get-counter-status", "counter", "counter");

        Assert.Equal("get-counter-status", meta.QueryType);
        Assert.Equal("counter", meta.Domain);
        Assert.Equal("counter", meta.ProjectionType);
    }
}
