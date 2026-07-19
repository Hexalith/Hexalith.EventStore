
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline.Queries;

public class SubmitQueryTests {
    [Fact]
    public void Constructor_ValidFields_SetsAllProperties() {
        byte[] payload = [0x01, 0x02];
        var sut = new SubmitQuery(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: payload,
            CorrelationId: "corr-1",
            UserId: "user-1",
            EntityId: "entity-1");

        sut.Tenant.ShouldBe("test-tenant");
        sut.Domain.ShouldBe("orders");
        sut.AggregateId.ShouldBe("order-1");
        sut.QueryType.ShouldBe("GetOrderStatus");
        sut.Payload.ShouldBe(payload);
        sut.CorrelationId.ShouldBe("corr-1");
        sut.UserId.ShouldBe("user-1");
        sut.EntityId.ShouldBe("entity-1");
    }

    [Fact]
    public void Constructor_WithPaging_SetsPagingPolicy() {
        var sut = new SubmitQuery(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1",
            Paging: new QueryPagingOptions(PageSize: 25, Cursor: "opaque-cursor"));

        _ = sut.Paging.ShouldNotBeNull();
        sut.Paging.PageSize.ShouldBe(25);
        sut.Paging.Cursor.ShouldBe("opaque-cursor");
    }

    [Fact]
    public void Constructor_EmptyPayload_IsValid() {
        var sut = new SubmitQuery("t", "d", "a", "q", [], "c", "u");
        sut.Payload.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_LegacyCaller_DualPrincipalFieldsDefaultToNull() {
        // Matrix row: "Legacy caller, no dual-principal fields populated" -> behaves exactly as today.
        var sut = new SubmitQuery(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1");

        sut.OriginalActorId.ShouldBeNull();
        sut.AuthenticatedWorkloadId.ShouldBeNull();
        sut.IsDelegated.ShouldBeFalse();
        sut.Scopes.ShouldBeNull();
        sut.Audience.ShouldBeNull();
        sut.DelegationId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_OriginalShapeOverload_DefaultsDualPrincipalFieldsToNull() {
        var sut = new SubmitQuery(
            "test-tenant", "orders", "order-1", "GetOrderStatus", [], "corr-1", "user-1",
            entityId: null, projectionType: null, projectionActorType: null, isGlobalAdmin: false);

        sut.OriginalActorId.ShouldBeNull();
        sut.AuthenticatedWorkloadId.ShouldBeNull();
        sut.IsDelegated.ShouldBeFalse();
        sut.Scopes.ShouldBeNull();
        sut.Audience.ShouldBeNull();
        sut.DelegationId.ShouldBeNull();
        sut.Paging.ShouldBeNull();
    }

    [Fact]
    public void Constructor_DualPrincipalFields_SetsAllProperties() {
        var sut = new SubmitQuery(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1",
            OriginalActorId: "actor-1",
            AuthenticatedWorkloadId: "workload-1",
            IsDelegated: true,
            Scopes: ["orders.read"],
            Audience: ["eventstore-api"],
            DelegationId: "delegate-service");

        sut.OriginalActorId.ShouldBe("actor-1");
        sut.AuthenticatedWorkloadId.ShouldBe("workload-1");
        sut.IsDelegated.ShouldBeTrue();
        sut.Scopes.ShouldBe(["orders.read"]);
        sut.Audience.ShouldBe(["eventstore-api"]);
        sut.DelegationId.ShouldBe("delegate-service");
    }

    [Fact]
    public void PublicCompatibility_PreservesSeventeenParameterConstructorAndDeconstruct() {
        Type[] priorParameters = [
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(byte[]),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(bool),
            typeof(QueryPagingOptions),
            typeof(string),
            typeof(string),
            typeof(bool),
            typeof(IReadOnlyList<string>),
            typeof(IReadOnlyList<string>),
        ];
        typeof(SubmitQuery).GetConstructor(priorParameters).ShouldNotBeNull();
        var value = new SubmitQuery(
            "tenant-a",
            "orders",
            "order-1",
            "get-order",
            [],
            "corr-1",
            "user-1",
            null,
            null,
            null,
            false,
            null,
            "actor-1",
            "workload-1",
            true,
            ["orders.read"],
            ["eventstore-api"]);
        (_, _, _, string queryType, _, _, _, _, _, _, _, _, string? actorId, _, _, _, _) = value;

        queryType.ShouldBe("get-order");
        actorId.ShouldBe("actor-1");
        value.DelegationId.ShouldBeNull();
    }

    private static SubmitQuery CreateValid(
        IReadOnlyList<string>? scopes = null,
        IReadOnlyList<string>? audience = null) =>
        new(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1",
            Scopes: scopes,
            Audience: audience);

    // A `with` expression is a second construction path that bypasses the primary constructor
    // entirely -- the property's own init accessor must normalize just as construction does, so a
    // caller cannot retain a live, later-mutable reference to the list backing Scopes/Audience.
    [Fact]
    public void WithExpression_ReassignsScopesAndAudience_NormalizesToArrayLikeConstruction() {
        SubmitQuery original = CreateValid();
        List<string> mutableScopes = ["orders.read"];
        List<string> mutableAudience = ["eventstore-api"];

        SubmitQuery mutated = original with { Scopes = mutableScopes, Audience = mutableAudience };

        mutated.Scopes.ShouldBeOfType<string[]>();
        mutated.Audience.ShouldBeOfType<string[]>();

        mutableScopes.Add("orders.write");
        mutableAudience.Add("admin-api");
        mutated.Scopes.ShouldBe(["orders.read"]);
        mutated.Audience.ShouldBe(["eventstore-api"]);
    }

    // Scopes/Audience must compare by content, not by array reference -- the default
    // record-synthesized equality would otherwise use array reference equality for these members.
    [Fact]
    public void Equals_ContentEqualScopesAndAudienceFromDifferentArrayInstances_AreEqual() {
        SubmitQuery left = CreateValid(scopes: ["orders.read"], audience: ["eventstore-api"]);
        SubmitQuery right = CreateValid(scopes: ["orders.read"], audience: ["eventstore-api"]);

        left.Scopes.ShouldNotBeSameAs(right.Scopes);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentScopesContent_AreNotEqual() {
        SubmitQuery left = CreateValid(scopes: ["orders.read"]);
        SubmitQuery right = CreateValid(scopes: ["orders.write"]);

        left.ShouldNotBe(right);
    }

    [Fact]
    public void Equals_NullScopesVersusEmptyScopes_AreNotEqual() {
        SubmitQuery left = CreateValid(scopes: null);
        SubmitQuery right = CreateValid(scopes: []);

        left.ShouldNotBe(right);
    }
}

public class SubmitQueryResultTests {
    [Fact]
    public void Constructor_SetsProperties() {
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var sut = new SubmitQueryResult("corr-1", payload);

        sut.CorrelationId.ShouldBe("corr-1");
        sut.Payload.GetProperty("count").GetInt32().ShouldBe(42);
    }

    [Fact]
    public void PublicCompatibility_MaintainsOriginalConstructorShape() {
        typeof(SubmitQueryResult)
            .GetConstructor([typeof(string), typeof(JsonElement), typeof(string)])
            .ShouldNotBeNull();
    }
}
