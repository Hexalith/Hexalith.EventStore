
using System.Runtime.Serialization;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class QueryEnvelopeTests {
    private static QueryEnvelope CreateValid(
        string tenantId = "test-tenant",
        string domain = "orders",
        string aggregateId = "order-42",
        string queryType = "GetOrderStatus",
        byte[]? payload = null,
        string correlationId = "corr-1",
        string userId = "user-1",
        string? entityId = null,
        bool isGlobalAdmin = false,
        QueryPagingOptions? paging = null) =>
        new(tenantId, domain, aggregateId, queryType, payload ?? [], correlationId, userId, entityId, isGlobalAdmin, paging);

    private static QueryEnvelope CreateValidWithDualPrincipal(
        string? originalActorId = "actor-1",
        string? authenticatedWorkloadId = "workload-1",
        bool isDelegated = false,
        IReadOnlyList<string>? scopes = null,
        IReadOnlyList<string>? audience = null,
        string tenantId = "test-tenant",
        string domain = "orders",
        string aggregateId = "order-42",
        string queryType = "GetOrderStatus",
        byte[]? payload = null,
        string correlationId = "corr-1",
        string userId = "user-1",
        string? entityId = null,
        bool isGlobalAdmin = false,
        QueryPagingOptions? paging = null) =>
        new(
            tenantId,
            domain,
            aggregateId,
            queryType,
            payload ?? [],
            correlationId,
            userId,
            entityId,
            isGlobalAdmin,
            paging,
            originalActorId,
            authenticatedWorkloadId,
            isDelegated,
            scopes,
            audience);

    [Fact]
    public void Constructor_ValidFields_SetsAllProperties() {
        byte[] payload = [0x01, 0x02];
        QueryEnvelope sut = CreateValid(payload: payload, entityId: "entity-1");

        sut.TenantId.ShouldBe("test-tenant");
        sut.Domain.ShouldBe("orders");
        sut.AggregateId.ShouldBe("order-42");
        sut.QueryType.ShouldBe("GetOrderStatus");
        sut.Payload.ShouldBe(payload);
        sut.CorrelationId.ShouldBe("corr-1");
        sut.UserId.ShouldBe("user-1");
        sut.EntityId.ShouldBe("entity-1");
    }

    [Fact]
    public void Constructor_WithPaging_SetsPagingPolicy() {
        var paging = new QueryPagingOptions(PageSize: 25, Cursor: "opaque-cursor");

        QueryEnvelope sut = CreateValid(paging: paging);

        _ = sut.Paging.ShouldNotBeNull();
        sut.Paging.PageSize.ShouldBe(25);
        sut.Paging.Cursor.ShouldBe("opaque-cursor");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidTenantId_ThrowsArgumentException(string? tenantId) =>
        Should.Throw<ArgumentException>(() => CreateValid(tenantId: tenantId!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidDomain_ThrowsArgumentException(string? domain) =>
        Should.Throw<ArgumentException>(() => CreateValid(domain: domain!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidAggregateId_ThrowsArgumentException(string? aggregateId) =>
        Should.Throw<ArgumentException>(() => CreateValid(aggregateId: aggregateId!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidQueryType_ThrowsArgumentException(string? queryType) =>
        Should.Throw<ArgumentException>(() => CreateValid(queryType: queryType!));

    [Fact]
    public void Constructor_NullPayload_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() =>
            new QueryEnvelope("t", "d", "a", "q", null!, "c", "u"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidCorrelationId_ThrowsArgumentException(string? correlationId) =>
        Should.Throw<ArgumentException>(() => CreateValid(correlationId: correlationId!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidUserId_ThrowsArgumentException(string? userId) =>
        Should.Throw<ArgumentException>(() => CreateValid(userId: userId!));

    [Fact]
    public void ToString_RedactsPayload() {
        QueryEnvelope sut = CreateValid(payload: [0x41, 0x42]);
        string result = sut.ToString();

        result.ShouldContain("[REDACTED");
        result.ShouldNotContain("AB");
        result.ShouldNotContain("65");
    }

    [Fact]
    public void AggregateIdentity_ReturnsCorrectIdentity() {
        QueryEnvelope sut = CreateValid();
        AggregateIdentity identity = sut.AggregateIdentity;

        identity.TenantId.ShouldBe("test-tenant");
        identity.Domain.ShouldBe("orders");
        identity.AggregateId.ShouldBe("order-42");
    }

    [Fact]
    public void Constructor_EmptyPayload_IsValid() {
        QueryEnvelope sut = CreateValid(payload: []);
        sut.Payload.ShouldBeEmpty();
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllProperties() {
        byte[] payload = [0x01, 0x02, 0x03];
        QueryEnvelope original = CreateValid(
            payload: payload,
            entityId: "entity-99",
            paging: new QueryPagingOptions(PageSize: 25, Cursor: "opaque-cursor"));

        string json = JsonSerializer.Serialize(original);
        QueryEnvelope? deserialized = JsonSerializer.Deserialize<QueryEnvelope>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.TenantId.ShouldBe(original.TenantId);
        deserialized.Domain.ShouldBe(original.Domain);
        deserialized.AggregateId.ShouldBe(original.AggregateId);
        deserialized.QueryType.ShouldBe(original.QueryType);
        deserialized.Payload.ShouldBe(original.Payload);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.UserId.ShouldBe(original.UserId);
        deserialized.EntityId.ShouldBe(original.EntityId);
        _ = deserialized.Paging.ShouldNotBeNull();
        deserialized.Paging.PageSize.ShouldBe(25);
        deserialized.Paging.Cursor.ShouldBe("opaque-cursor");
    }

    [Fact]
    public void Constructor_NullEntityId_AcceptedWithoutException() {
        QueryEnvelope sut = CreateValid(entityId: null);
        sut.EntityId.ShouldBeNull();
    }

    [Fact]
    public void JsonRoundTrip_NonNullEntityId_PreservedThroughSerialization() {
        QueryEnvelope original = CreateValid(entityId: "order-42");

        string json = JsonSerializer.Serialize(original);
        QueryEnvelope? deserialized = JsonSerializer.Deserialize<QueryEnvelope>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.EntityId.ShouldBe("order-42");
    }

    [Fact]
    public void ToString_WithEntityId_IncludesEntityId() {
        QueryEnvelope sut = CreateValid(entityId: "order-42");
        string result = sut.ToString();

        result.ShouldContain("EntityId = order-42");
    }

    [Fact]
    public void ToString_WithoutEntityId_OmitsEntityId() {
        QueryEnvelope sut = CreateValid(entityId: null);
        string result = sut.ToString();

        result.ShouldNotContain("EntityId");
    }

    [Fact]
    public void DataContractSerializer_OldFormatWithoutEntityId_DeserializesWithNullEntityId() {
        // Simulate an old serialized QueryEnvelope without EntityId field
        QueryEnvelope original = CreateValid();

        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);

        // Manually remove EntityId from serialized XML to simulate old format
        ms.Position = 0;
        string xml = new StreamReader(ms).ReadToEnd();
        var document = XDocument.Parse(xml);
        document.Descendants().Where(e => e.Name.LocalName == "EntityId").Remove();
        string oldFormatXml = document.ToString(SaveOptions.DisableFormatting);

        oldFormatXml.ShouldNotContain("EntityId");

        using var ms2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(oldFormatXml));
        var deserialized = (QueryEnvelope?)serializer.ReadObject(ms2);

        _ = deserialized.ShouldNotBeNull();
        deserialized.EntityId.ShouldBeNull();
        deserialized.TenantId.ShouldBe(original.TenantId);
    }

    [Fact]
    public void Constructor_DefaultsIsGlobalAdminToFalse() {
        QueryEnvelope sut = CreateValid();
        sut.IsGlobalAdmin.ShouldBeFalse();
    }

    [Fact]
    public void ToString_WithGlobalAdmin_IncludesFlag() {
        QueryEnvelope sut = CreateValid(isGlobalAdmin: true);
        string result = sut.ToString();

        result.ShouldContain("IsGlobalAdmin = True");
    }

    [Fact]
    public void ToString_WithoutGlobalAdmin_OmitsFlag() {
        QueryEnvelope sut = CreateValid(isGlobalAdmin: false);
        string result = sut.ToString();

        result.ShouldNotContain("IsGlobalAdmin");
    }

    [Fact]
    public void JsonRoundTrip_PreservesIsGlobalAdmin() {
        QueryEnvelope original = CreateValid(isGlobalAdmin: true);

        string json = JsonSerializer.Serialize(original);
        QueryEnvelope? deserialized = JsonSerializer.Deserialize<QueryEnvelope>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.IsGlobalAdmin.ShouldBeTrue();
    }

    [Fact]
    public void DataContractSerializer_PreservesIsGlobalAdmin_AcrossActorBoundary() {
        // The flag is minted server-side then crosses the DAPR actor invocation as a DataContract
        // member; if it did not round-trip, the whole authorization-by-claim path would silently fail.
        QueryEnvelope original = CreateValid(isGlobalAdmin: true);
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);
        ms.Position = 0;
        var deserialized = (QueryEnvelope?)serializer.ReadObject(ms);

        _ = deserialized.ShouldNotBeNull();
        deserialized.IsGlobalAdmin.ShouldBeTrue();
    }

    [Fact]
    public void DataContractSerializer_PreservesPaging_AcrossActorBoundary() {
        QueryEnvelope original = CreateValid(paging: new QueryPagingOptions(PageSize: 25, Cursor: "opaque-cursor"));
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);
        ms.Position = 0;
        var deserialized = (QueryEnvelope?)serializer.ReadObject(ms);

        _ = deserialized.ShouldNotBeNull();
        _ = deserialized.Paging.ShouldNotBeNull();
        deserialized.Paging.PageSize.ShouldBe(25);
        deserialized.Paging.Cursor.ShouldBe("opaque-cursor");
    }

    [Fact]
    public void DataContractSerializer_OldFormatWithoutIsGlobalAdmin_DeserializesAsFalse() {
        // An in-flight envelope serialized before the flag existed must fail safe to non-admin.
        QueryEnvelope original = CreateValid(isGlobalAdmin: true);
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);

        ms.Position = 0;
        string xml = new StreamReader(ms).ReadToEnd();
        var document = XDocument.Parse(xml);
        document.Descendants().Where(e => e.Name.LocalName == "IsGlobalAdmin").Remove();
        string oldFormatXml = document.ToString(SaveOptions.DisableFormatting);

        oldFormatXml.ShouldNotContain("IsGlobalAdmin");

        using var ms2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(oldFormatXml));
        var deserialized = (QueryEnvelope?)serializer.ReadObject(ms2);

        _ = deserialized.ShouldNotBeNull();
        deserialized.IsGlobalAdmin.ShouldBeFalse();
    }

    // --- Dual-principal identity fields (Story 6.1-P2) ---

    [Fact]
    public void Constructor_LegacyCaller_NoDualPrincipalFieldsPopulated_DefaultsToNull() {
        // Matrix row: "Legacy caller, no dual-principal fields populated" -> behaves exactly as today.
        QueryEnvelope sut = CreateValid();

        sut.OriginalActorId.ShouldBeNull();
        sut.AuthenticatedWorkloadId.ShouldBeNull();
        sut.IsDelegated.ShouldBeFalse();
        sut.Scopes.ShouldBeNull();
        sut.Audience.ShouldBeNull();
    }

    [Fact]
    public void Constructor_TenParameterOverload_DefaultsDualPrincipalFieldsToNull() {
        // The pre-existing 10-parameter (with Paging) overload must still compile and behave
        // unchanged for existing single-principal callers.
        var sut = new QueryEnvelope(
            "test-tenant", "orders", "order-42", "GetOrderStatus", [], "corr-1", "user-1",
            entityId: null, isGlobalAdmin: false, paging: null);

        sut.OriginalActorId.ShouldBeNull();
        sut.AuthenticatedWorkloadId.ShouldBeNull();
        sut.IsDelegated.ShouldBeFalse();
        sut.Scopes.ShouldBeNull();
        sut.Audience.ShouldBeNull();
    }

    [Fact]
    public void Constructor_DualPrincipalFields_SetsAllProperties() {
        QueryEnvelope sut = CreateValidWithDualPrincipal(
            originalActorId: "actor-1",
            authenticatedWorkloadId: "workload-1",
            isDelegated: true,
            scopes: ["orders.read", "orders.write"],
            audience: ["eventstore-api"]);

        sut.OriginalActorId.ShouldBe("actor-1");
        sut.AuthenticatedWorkloadId.ShouldBe("workload-1");
        sut.IsDelegated.ShouldBeTrue();
        sut.Scopes.ShouldBe(["orders.read", "orders.write"]);
        sut.Audience.ShouldBe(["eventstore-api"]);
    }

    [Fact]
    public void JsonRoundTrip_PreservesDualPrincipalFields() {
        QueryEnvelope original = CreateValidWithDualPrincipal(
            originalActorId: "actor-1",
            authenticatedWorkloadId: "workload-1",
            isDelegated: true,
            scopes: ["orders.read"],
            audience: ["eventstore-api", "admin-api"]);

        string json = JsonSerializer.Serialize(original);
        QueryEnvelope? deserialized = JsonSerializer.Deserialize<QueryEnvelope>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.OriginalActorId.ShouldBe("actor-1");
        deserialized.AuthenticatedWorkloadId.ShouldBe("workload-1");
        deserialized.IsDelegated.ShouldBeTrue();
        deserialized.Scopes.ShouldBe(["orders.read"]);
        deserialized.Audience.ShouldBe(["eventstore-api", "admin-api"]);
    }

    [Fact]
    public void JsonRoundTrip_LegacyEnvelopeWithoutDualPrincipalFields_PreservesNullDefaults() {
        QueryEnvelope original = CreateValid();

        string json = JsonSerializer.Serialize(original);
        QueryEnvelope? deserialized = JsonSerializer.Deserialize<QueryEnvelope>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.OriginalActorId.ShouldBeNull();
        deserialized.AuthenticatedWorkloadId.ShouldBeNull();
        deserialized.IsDelegated.ShouldBeFalse();
        deserialized.Scopes.ShouldBeNull();
        deserialized.Audience.ShouldBeNull();
    }

    [Fact]
    public void DataContractSerializer_PreservesDualPrincipalFields_AcrossActorBoundary() {
        QueryEnvelope original = CreateValidWithDualPrincipal(
            originalActorId: "actor-1",
            authenticatedWorkloadId: "workload-1",
            isDelegated: true,
            scopes: ["orders.read", "orders.write"],
            audience: ["eventstore-api"]);
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);
        ms.Position = 0;
        var deserialized = (QueryEnvelope?)serializer.ReadObject(ms);

        _ = deserialized.ShouldNotBeNull();
        deserialized.OriginalActorId.ShouldBe("actor-1");
        deserialized.AuthenticatedWorkloadId.ShouldBe("workload-1");
        deserialized.IsDelegated.ShouldBeTrue();
        deserialized.Scopes.ShouldBe(["orders.read", "orders.write"]);
        deserialized.Audience.ShouldBe(["eventstore-api"]);
    }

    [Fact]
    public void DataContractSerializer_OldFormatWithoutDualPrincipalFields_DeserializesWithSafeDefaults() {
        // Simulate an envelope serialized before the dual-principal fields existed: an older
        // producer's XML simply omits the new elements entirely. New/newer consumers must still
        // round-trip it with safe (null/false) defaults rather than throwing.
        QueryEnvelope original = CreateValid();
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);

        ms.Position = 0;
        string xml = new StreamReader(ms).ReadToEnd();
        var document = XDocument.Parse(xml);
        foreach (string elementName in new[] { "OriginalActorId", "AuthenticatedWorkloadId", "IsDelegated", "Scopes", "Audience" }) {
            document.Descendants().Where(e => e.Name.LocalName == elementName).Remove();
        }

        string oldFormatXml = document.ToString(SaveOptions.DisableFormatting);
        oldFormatXml.ShouldNotContain("OriginalActorId");
        oldFormatXml.ShouldNotContain("AuthenticatedWorkloadId");

        using var ms2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(oldFormatXml));
        var deserialized = (QueryEnvelope?)serializer.ReadObject(ms2);

        _ = deserialized.ShouldNotBeNull();
        deserialized.OriginalActorId.ShouldBeNull();
        deserialized.AuthenticatedWorkloadId.ShouldBeNull();
        deserialized.IsDelegated.ShouldBeFalse();
        deserialized.Scopes.ShouldBeNull();
        deserialized.Audience.ShouldBeNull();
        deserialized.TenantId.ShouldBe(original.TenantId);
    }

    [Fact]
    public void Constructor_NullScopesAndAudience_AreAcceptedAndRemainNull() {
        QueryEnvelope sut = CreateValidWithDualPrincipal(scopes: null, audience: null);

        sut.Scopes.ShouldBeNull();
        sut.Audience.ShouldBeNull();
    }

    [Fact]
    public void Constructor_EmptyScopesAndAudienceLists_ArePreserved() {
        QueryEnvelope sut = CreateValidWithDualPrincipal(scopes: [], audience: []);

        _ = sut.Scopes.ShouldNotBeNull();
        sut.Scopes.ShouldBeEmpty();
        _ = sut.Audience.ShouldNotBeNull();
        sut.Audience.ShouldBeEmpty();
    }

    // DataContractSerializer round-trip coverage was previously only null vs. non-empty; an
    // explicitly empty (non-null, zero-length) array is a third, distinct wire shape that must
    // also survive the actor boundary without collapsing to null.
    [Fact]
    public void DataContractSerializer_EmptyScopesAndAudienceArrays_PreservesEmptyNotNull() {
        QueryEnvelope original = CreateValidWithDualPrincipal(scopes: [], audience: []);
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);
        ms.Position = 0;
        var deserialized = (QueryEnvelope?)serializer.ReadObject(ms);

        _ = deserialized.ShouldNotBeNull();
        _ = deserialized.Scopes.ShouldNotBeNull();
        deserialized.Scopes.ShouldBeEmpty();
        _ = deserialized.Audience.ShouldNotBeNull();
        deserialized.Audience.ShouldBeEmpty();
    }

    // A `with` expression is a second construction path that bypasses the constructor entirely --
    // the property's own init accessor must normalize just as the constructor does, so DAPR's
    // KnownType(typeof(string[])) contract and the caller-mutable-list-leak concern both hold
    // regardless of which path produced the instance.
    [Fact]
    public void WithExpression_ReassignsScopesAndAudience_NormalizesToArrayLikeConstructor() {
        QueryEnvelope original = CreateValid();
        List<string> mutableScopes = ["orders.read"];
        List<string> mutableAudience = ["eventstore-api"];

        QueryEnvelope mutated = original with { Scopes = mutableScopes, Audience = mutableAudience };

        mutated.Scopes.ShouldBeOfType<string[]>();
        mutated.Audience.ShouldBeOfType<string[]>();
        mutated.Scopes.ShouldBe(["orders.read"]);
        mutated.Audience.ShouldBe(["eventstore-api"]);

        // Mutating the caller's list after the `with` expression must not be observable on the
        // envelope -- proves the init accessor copied rather than retained the reference.
        mutableScopes.Add("orders.write");
        mutableAudience.Add("admin-api");
        mutated.Scopes.ShouldBe(["orders.read"]);
        mutated.Audience.ShouldBe(["eventstore-api"]);
    }

    // Scopes/Audience must compare by content, not by array reference: two envelopes built from
    // separate (but content-identical) array instances must be Equals()-equal, since the default
    // record-synthesized equality would otherwise use array reference equality for these members.
    [Fact]
    public void Equals_ContentEqualScopesAndAudienceFromDifferentArrayInstances_AreEqual() {
        QueryEnvelope left = CreateValidWithDualPrincipal(scopes: ["orders.read"], audience: ["eventstore-api"]);
        QueryEnvelope right = CreateValidWithDualPrincipal(scopes: ["orders.read"], audience: ["eventstore-api"]);

        left.Scopes.ShouldNotBeSameAs(right.Scopes);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        (left == right).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentScopesContent_AreNotEqual() {
        QueryEnvelope left = CreateValidWithDualPrincipal(scopes: ["orders.read"]);
        QueryEnvelope right = CreateValidWithDualPrincipal(scopes: ["orders.write"]);

        left.ShouldNotBe(right);
    }

    [Fact]
    public void Equals_NullScopesVersusEmptyScopes_AreNotEqual() {
        QueryEnvelope left = CreateValidWithDualPrincipal(scopes: null);
        QueryEnvelope right = CreateValidWithDualPrincipal(scopes: []);

        left.ShouldNotBe(right);
    }

    [Fact]
    public void Equals_NullOther_ReturnsFalse() {
        QueryEnvelope sut = CreateValid();

        sut.Equals(null).ShouldBeFalse();
    }
}
