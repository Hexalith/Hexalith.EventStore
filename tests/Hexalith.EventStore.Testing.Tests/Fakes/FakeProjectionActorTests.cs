
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class FakeProjectionActorTests {
    private static QueryEnvelope CreateTestEnvelope(string queryType = "GetState") =>
        new("test-tenant", "orders", "order-1", queryType, [], "corr-1", "user-1");

    [Fact]
    public async Task QueryAsync_RecordsEnvelopeAndIncrementsCount() {
        var sut = new FakeProjectionActor();
        QueryEnvelope envelope = CreateTestEnvelope();

        _ = await sut.QueryAsync(envelope);

        Assert.Single(sut.ReceivedEnvelopes);
        Assert.Equal(1, sut.QueryCount);
        Assert.Equal(envelope, sut.ReceivedEnvelopes.First());
    }

    [Fact]
    public async Task QueryAsync_ReturnsConfiguredResult() {
        var sut = new FakeProjectionActor();
        JsonElement payload = JsonDocument.Parse("{\"value\":99}").RootElement;
        var expected = new QueryResult(true, payload);
        sut.ConfiguredResult = expected;

        QueryResult result = await sut.QueryAsync(CreateTestEnvelope());

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task QueryAsync_ThrowsConfiguredException() {
        var sut = new FakeProjectionActor();
        sut.ConfiguredException = new InvalidOperationException("boom");
        sut.ConfiguredResult = new QueryResult(true, default);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.QueryAsync(CreateTestEnvelope()));
    }

    [Fact]
    public async Task QueryAsync_ReturnsDefaultSuccessWhenNothingConfigured() {
        var sut = new FakeProjectionActor();

        QueryResult result = await sut.QueryAsync(CreateTestEnvelope());

        Assert.True(result.Success);
        Assert.Equal(JsonValueKind.Object, result.Payload.ValueKind);
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
}
