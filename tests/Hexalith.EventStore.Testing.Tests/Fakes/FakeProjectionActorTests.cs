
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class FakeProjectionActorTests {
    private static QueryEnvelope CreateTestEnvelope(string queryType = "GetState") =>
        new("test-tenant", "orders", "order-1", queryType, [], "corr-1", "user-1");

    [Fact]
    public async Task QueryAsync_RecordsEnvelopeAndIncrementsCount() {
        var sut = new FakeProjectionActor();
        QueryEnvelope envelope = CreateTestEnvelope();

        _ = await sut.QueryAsync(envelope);

        _ = Assert.Single(sut.ReceivedEnvelopes);
        Assert.Equal(1, sut.QueryCount);
        Assert.Equal(envelope, sut.ReceivedEnvelopes.First());
    }

    [Fact]
    public async Task QueryAsync_ReturnsConfiguredResult() {
        var sut = new FakeProjectionActor();
        JsonElement payload = JsonDocument.Parse("{\"value\":99}").RootElement;
        var expected = QueryResult.FromPayload(payload);
        sut.ConfiguredResult = expected;

        QueryResult result = await sut.QueryAsync(CreateTestEnvelope());

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task QueryAsync_ThrowsConfiguredException() {
        var sut = new FakeProjectionActor {
            ConfiguredException = new InvalidOperationException("boom"),
            ConfiguredResult = new QueryResult(true)
        };

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.QueryAsync(CreateTestEnvelope()));
    }

    [Fact]
    public async Task QueryAsync_ReturnsDefaultSuccessWhenNothingConfigured() {
        var sut = new FakeProjectionActor();

        QueryResult result = await sut.QueryAsync(CreateTestEnvelope());

        Assert.True(result.Success);
        Assert.Equal(JsonValueKind.Object, result.GetPayload().ValueKind);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task QueryAsync_MultipleCallsAccumulate() {
        var sut = new FakeProjectionActor();
        QueryEnvelope env1 = CreateTestEnvelope("Query1");
        QueryEnvelope env2 = CreateTestEnvelope("Query2");

        _ = await sut.QueryAsync(env1);
        _ = await sut.QueryAsync(env2);

        Assert.Equal(2, sut.ReceivedEnvelopes.Count);
        Assert.Equal(2, sut.QueryCount);
    }

    [Theory]
    [InlineData("get-party", "party-42", "entity")]
    [InlineData("list-parties", null, "list")]
    [InlineData("search-parties", null, "search")]
    public async Task QueryAsync_RecordsRepresentativePublicAdapterRoutes(string queryType, string? entityId, string routeKind) {
        var sut = new FakeProjectionActor();
        byte[] payload = routeKind == "search" ? JsonSerializer.SerializeToUtf8Bytes(new { term = "ada" }) : [];
        var envelope = new QueryEnvelope(
            "tenant-a",
            "parties",
            "party",
            queryType,
            payload,
            "corr-1",
            "user-1",
            entityId);

        _ = await sut.QueryAsync(envelope);

        QueryEnvelope received = Assert.Single(sut.ReceivedEnvelopes);
        Assert.Equal(queryType, received.QueryType);
        Assert.Equal(entityId, received.EntityId);
        Assert.Equal(payload, received.Payload);
    }

    [Fact]
    public async Task QueryAsync_ReturnsConfiguredMalformedPayloadForAdapterEdgeTests() {
        var sut = new FakeProjectionActor {
            ConfiguredResult = new QueryResult(true, [0xFF])
        };

        QueryResult result = await sut.QueryAsync(CreateTestEnvelope());

        Assert.True(result.Success);
        Assert.Equal([0xFF], result.PayloadBytes);
    }

    [Fact]
    public void PublicProjectionFakeApi_UsesContractsTypesOnly() {
        Type fakeType = typeof(FakeProjectionActor);
        Type projectionActorInterface = typeof(IProjectionActor);

        fakeType.GetInterfaces().ShouldContain(projectionActorInterface);

        fakeType.GetProperty(nameof(FakeProjectionActor.ConfiguredResult))!
            .PropertyType
            .ShouldBe(typeof(QueryResult));

        fakeType.GetProperty(nameof(FakeProjectionActor.ReceivedEnvelopes))!
            .PropertyType
            .GetGenericArguments()
            .Single()
            .ShouldBe(typeof(QueryEnvelope));

        fakeType.GetMethod(nameof(FakeProjectionActor.QueryAsync), [typeof(QueryEnvelope)])!
            .ReturnType
            .GetGenericArguments()
            .Single()
            .ShouldBe(typeof(QueryResult));
    }
}
