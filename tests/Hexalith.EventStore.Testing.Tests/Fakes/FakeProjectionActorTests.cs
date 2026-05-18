
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
    public async Task QueryAsync_WithCancellationToken_RecordsToken() {
        var sut = new FakeProjectionActor();
        QueryEnvelope envelope = CreateTestEnvelope();
        using var cts = new CancellationTokenSource();

        _ = await sut.QueryAsync(envelope, cts.Token);

        CancellationToken received = Assert.Single(sut.ReceivedCancellationTokens);
        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task QueryAsync_WithPreCancelledToken_ThrowsWithoutRecordingEnvelope() {
        var sut = new FakeProjectionActor();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.QueryAsync(CreateTestEnvelope(), cts.Token));

        Assert.Empty(sut.ReceivedEnvelopes);
        Assert.Empty(sut.ReceivedCancellationTokens);
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

        fakeType.GetProperty(nameof(FakeProjectionActor.ReceivedCancellationTokens))!
            .PropertyType
            .GetGenericArguments()
            .Single()
            .ShouldBe(typeof(CancellationToken));

        fakeType.GetMethod(nameof(FakeProjectionActor.QueryAsync), [typeof(QueryEnvelope)])!
            .ReturnType
            .GetGenericArguments()
            .Single()
            .ShouldBe(typeof(QueryResult));

        fakeType.GetMethod(nameof(FakeProjectionActor.QueryAsync), [typeof(QueryEnvelope), typeof(CancellationToken)])!
            .ReturnType
            .GetGenericArguments()
            .Single()
            .ShouldBe(typeof(QueryResult));
    }

    [Fact]
    public void PublicProjectionFakeAssembly_DoesNotReferenceServerActorsAssembly() {
        System.Reflection.Assembly testingAssembly = typeof(FakeProjectionActor).Assembly;
        string serverActorsNamespace = "Hexalith.EventStore.Server";

        IEnumerable<string> referencedNames = testingAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        // The Testing assembly may reference Server for runtime-test utilities, but
        // no type from Hexalith.EventStore.Server.Actors should appear in the
        // FakeProjectionActor's public API surface types.
        IEnumerable<System.Reflection.Assembly> referencedAssemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => referencedNames.Contains(a.GetName().Name));

        Type fakeType = typeof(FakeProjectionActor);
        IEnumerable<Type> apiTypes = new[] { fakeType }
            .Concat(fakeType.GetInterfaces())
            .Concat(fakeType.GetProperties().Select(p => p.PropertyType))
            .Concat(fakeType.GetMethods().SelectMany(m =>
                m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
            .SelectMany(t => t.IsGenericType ? t.GetGenericArguments().Append(t) : [t]);

        foreach (Type apiType in apiTypes.Where(t => t.Assembly != typeof(object).Assembly)) {
            bool fromServerActors = apiType.Namespace?.StartsWith(serverActorsNamespace + ".Actors", StringComparison.Ordinal) ?? false;
            fromServerActors.ShouldBeFalse($"Public API type '{apiType.FullName}' must not come from {serverActorsNamespace}.Actors");
        }
    }
}
