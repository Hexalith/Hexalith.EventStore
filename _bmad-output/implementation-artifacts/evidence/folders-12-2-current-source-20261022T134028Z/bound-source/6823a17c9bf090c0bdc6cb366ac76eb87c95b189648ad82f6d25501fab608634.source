
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Client.Tests.Queries;

// --- Test stub types for QueryContractResolver ---

internal class ValidCounterQuery : IQueryContract {
    public static string QueryType => "get-counter-status";
    public static string Domain => "counter";
    public static string ProjectionType => "counter";
}

internal class CrossDomainQuery : IQueryContract {
    public static string QueryType => "get-order-summary";
    public static string Domain => "reporting";
    public static string ProjectionType => "order-summary";
}

internal class InvalidKebabCaseQuery : IQueryContract {
    public static string QueryType => "GetCounter";
    public static string Domain => "counter";
    public static string ProjectionType => "counter";
}

internal class EmptyDomainQuery : IQueryContract {
    public static string QueryType => "get-counter";
    public static string Domain => "";
    public static string ProjectionType => "counter";
}

internal class InvalidProjectionTypeQuery : IQueryContract {
    public static string QueryType => "get-counter";
    public static string Domain => "counter";
    public static string ProjectionType => "Counter";
}

internal class NullQueryTypeQuery : IQueryContract {
    public static string QueryType => null!;
    public static string Domain => "counter";
    public static string ProjectionType => "counter";
}

public class QueryContractResolverTests : IDisposable {
    public QueryContractResolverTests() => QueryContractResolver.ClearCache();

    public void Dispose() {
        QueryContractResolver.ClearCache();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Resolve_ValidContract_ReturnsCorrectMetadata() {
        QueryContractMetadata meta = QueryContractResolver.Resolve<ValidCounterQuery>();

        Assert.Equal("get-counter-status", meta.QueryType);
        Assert.Equal("counter", meta.Domain);
        Assert.Equal("counter", meta.ProjectionType);
    }

    [Fact]
    public void Resolve_CrossDomainContract_DomainDiffersFromProjectionType() {
        QueryContractMetadata meta = QueryContractResolver.Resolve<CrossDomainQuery>();

        Assert.Equal("get-order-summary", meta.QueryType);
        Assert.Equal("reporting", meta.Domain);
        Assert.Equal("order-summary", meta.ProjectionType);
        Assert.NotEqual(meta.Domain, meta.ProjectionType);
    }

    [Fact]
    public void Resolve_CalledTwice_ReturnsCachedInstance() {
        QueryContractMetadata first = QueryContractResolver.Resolve<ValidCounterQuery>();
        QueryContractMetadata second = QueryContractResolver.Resolve<ValidCounterQuery>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_InvalidKebabCase_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
            QueryContractResolver.Resolve<InvalidKebabCaseQuery>);

    [Fact]
    public void Resolve_EmptyDomain_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
            QueryContractResolver.Resolve<EmptyDomainQuery>);

    [Fact]
    public void Resolve_NullQueryType_ThrowsArgumentNullException() => _ = Assert.Throws<ArgumentNullException>(
            QueryContractResolver.Resolve<NullQueryTypeQuery>);

    [Fact]
    public void GetETagActorId_ValidInputs_UsesProjectionType() {
        string etagId = QueryContractResolver.GetETagActorId<ValidCounterQuery>("acme");

        Assert.Equal("counter:acme", etagId);
    }

    [Fact]
    public void GetETagActorId_CrossDomain_UsesProjectionTypeNotDomain() {
        // Domain is "reporting" but ProjectionType is "order-summary"
        string etagId = QueryContractResolver.GetETagActorId<CrossDomainQuery>("acme");

        Assert.Equal("order-summary:acme", etagId);
    }

    [Fact]
    public void GetETagActorId_InvalidProjectionType_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
            () => QueryContractResolver.GetETagActorId<InvalidProjectionTypeQuery>("acme"));

    [Fact]
    public void GetETagActorId_NullTenantId_ThrowsArgumentNullException() => _ = Assert.Throws<ArgumentNullException>(
            () => QueryContractResolver.GetETagActorId<ValidCounterQuery>(null!));

    [Fact]
    public void GetETagActorId_EmptyTenantId_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
            () => QueryContractResolver.GetETagActorId<ValidCounterQuery>(""));

    [Fact]
    public void GetETagActorId_WhitespaceTenantId_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
            () => QueryContractResolver.GetETagActorId<ValidCounterQuery>("  "));

    [Fact]
    public void GetETagActorId_TenantIdWithColon_ThrowsArgumentException() => _ = Assert.Throws<ArgumentException>(
            () => QueryContractResolver.GetETagActorId<ValidCounterQuery>("acme:west"));
}
