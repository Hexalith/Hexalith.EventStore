
using Hexalith.EventStore.Sample.Counter.Queries;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.Counter.Queries;

public class GetCounterStatusQueryTests {
    [Fact]
    public void StaticMembers_AreAccessible() {
        GetCounterStatusQuery.QueryType.ShouldBe("get-counter-status");
        GetCounterStatusQuery.Domain.ShouldBe("counter");
        GetCounterStatusQuery.ProjectionType.ShouldBe("counter");
    }

    [Fact]
    public void QueryType_IsKebabCase() {
        string queryType = GetCounterStatusQuery.QueryType;

        queryType.ShouldNotContain(":");
        queryType.ShouldBe(queryType.ToLowerInvariant());
        queryType.ShouldNotContain(" ");
    }

    [Fact]
    public void Domain_IsKebabCase() {
        string domain = GetCounterStatusQuery.Domain;

        domain.ShouldNotContain(":");
        domain.ShouldBe(domain.ToLowerInvariant());
    }

    [Fact]
    public void ProjectionType_IsKebabCase() {
        string projectionType = GetCounterStatusQuery.ProjectionType;

        projectionType.ShouldNotContain(":");
        projectionType.ShouldBe(projectionType.ToLowerInvariant());
    }
}
