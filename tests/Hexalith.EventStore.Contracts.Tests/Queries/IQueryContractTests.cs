
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
        GetCounterStatusQuery.QueryType.ShouldBe("get-counter-status");
        GetCounterStatusQuery.Domain.ShouldBe("counter");
        GetCounterStatusQuery.ProjectionType.ShouldBe("counter");
    }

    [Fact]
    public void IQueryContract_CrossDomainQuery_DomainDiffersFromProjectionType() {
        GetOrderSummaryQuery.QueryType.ShouldBe("get-order-summary");
        GetOrderSummaryQuery.Domain.ShouldBe("reporting");
        GetOrderSummaryQuery.ProjectionType.ShouldBe("order-summary");
        GetOrderSummaryQuery.ProjectionType.ShouldNotBe(GetOrderSummaryQuery.Domain);
    }

    [Fact]
    public void QueryContractMetadata_RecordEquality_WorksCorrectly() {
        var meta1 = new QueryContractMetadata("get-counter-status", "counter", "counter");
        var meta2 = new QueryContractMetadata("get-counter-status", "counter", "counter");

        meta2.ShouldBe(meta1);
    }

    [Fact]
    public void QueryContractMetadata_DifferentValues_AreNotEqual() {
        var meta1 = new QueryContractMetadata("get-counter-status", "counter", "counter");
        var meta2 = new QueryContractMetadata("get-order-summary", "reporting", "order-summary");

        meta2.ShouldNotBe(meta1);
    }

    [Fact]
    public void QueryContractMetadata_IsImmutable() {
        var meta = new QueryContractMetadata("get-counter-status", "counter", "counter");

        meta.QueryType.ShouldBe("get-counter-status");
        meta.Domain.ShouldBe("counter");
        meta.ProjectionType.ShouldBe("counter");
    }
}
