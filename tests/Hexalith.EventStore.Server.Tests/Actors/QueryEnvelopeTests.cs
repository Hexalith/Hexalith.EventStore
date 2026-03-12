
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

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
        string userId = "user-1") =>
        new(tenantId, domain, aggregateId, queryType, payload ?? [], correlationId, userId);

    [Fact]
    public void Constructor_ValidFields_SetsAllProperties() {
        byte[] payload = [0x01, 0x02];
        QueryEnvelope sut = CreateValid(payload: payload);

        sut.TenantId.ShouldBe("test-tenant");
        sut.Domain.ShouldBe("orders");
        sut.AggregateId.ShouldBe("order-42");
        sut.QueryType.ShouldBe("GetOrderStatus");
        sut.Payload.ShouldBe(payload);
        sut.CorrelationId.ShouldBe("corr-1");
        sut.UserId.ShouldBe("user-1");
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
        var identity = sut.AggregateIdentity;

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
        QueryEnvelope original = CreateValid(payload: payload);

        string json = JsonSerializer.Serialize(original);
        QueryEnvelope? deserialized = JsonSerializer.Deserialize<QueryEnvelope>(json);

        deserialized.ShouldNotBeNull();
        deserialized.TenantId.ShouldBe(original.TenantId);
        deserialized.Domain.ShouldBe(original.Domain);
        deserialized.AggregateId.ShouldBe(original.AggregateId);
        deserialized.QueryType.ShouldBe(original.QueryType);
        deserialized.Payload.ShouldBe(original.Payload);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.UserId.ShouldBe(original.UserId);
    }
}
