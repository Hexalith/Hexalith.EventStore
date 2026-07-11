extern alias eventstore;

using System.Net.Http.Headers;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Actors.Authorization;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.Server.Tests.Integration;

/// <summary>
/// Tier 2 proof that persisted freshness metadata traverses the production query and HTTP carriers.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Tier", "2")]
public sealed class QueryResponseProvenancePersistenceTests(
    ActorBasedAuthWebApplicationFactory factory) : IClassFixture<ActorBasedAuthWebApplicationFactory>
{
    [Theory]
    [InlineData(-1, ProjectionLifecycleState.Current, false)]
    [InlineData(-10, ProjectionLifecycleState.Stale, true)]
    public async Task PersistedFreshness_TraversesRouterHandlerControllerAndGatewayClient(
        int projectedMinutes,
        ProjectionLifecycleState expectedLifecycle,
        bool expectedIsStale)
    {
        const string StoreName = "test-readmodels";
        const string Key = "freshness:index";
        const string ExpectedVersion = "model-v42";
        DateTimeOffset now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var thresholds = ReadModelFreshnessThresholds.Create(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5));
        var store = new InMemoryReadModelStore();
        var persistedModel = new PersistedFreshnessReadModel(
            Value: 42,
            ProjectedAt: now.AddMinutes(projectedMinutes),
            ProjectionVersion: ExpectedVersion);
        await store.SaveAsync(StoreName, Key, persistedModel, TestContext.Current.CancellationToken);

        ReadModelEntry<PersistedFreshnessReadModel> persistedEntry = await store
            .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, TestContext.Current.CancellationToken);
        _ = persistedEntry.Value.ShouldNotBeNull();
        persistedEntry.Value.ProjectedAt.ShouldBe(persistedModel.ProjectedAt);
        persistedEntry.Value.ProjectionVersion.ShouldBe(ExpectedVersion);

        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker
            .InvokeAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<QueryEnvelope>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                CancellationToken cancellationToken = callInfo.ArgAt<CancellationToken>(3);
                ReadModelEntry<PersistedFreshnessReadModel> read = await store
                    .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, cancellationToken)
                    .ConfigureAwait(false);
                PersistedFreshnessReadModel model = read.Value.ShouldNotBeNull();
                QueryResponseMetadata metadata = model.ToQueryResponseMetadata(
                    thresholds,
                    now,
                    read.ETag);
                return QueryResult.FromPayload(
                    JsonSerializer.SerializeToElement(new { model.Value }),
                    "freshness",
                    metadata);
            });
        var queryRouter = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        IETagService eTagService = Substitute.For<IETagService>();
        string currentETag = SelfRoutingETag.GenerateNew("freshness");
        _ = eTagService
            .GetCurrentETagAsync("freshness", "tenant-a", Arg.Any<CancellationToken>())
            .Returns(currentETag);

        factory.ResetActors();
        factory.FakeTenantActor.ConfiguredResult = new ActorValidationResponse(true);
        factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);
        using WebApplicationFactory<EventStoreProgram> configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQueryRouter>();
                services.RemoveAll<IETagService>();
                services.AddSingleton<IQueryRouter>(queryRouter);
                services.AddSingleton(eTagService);
            }));
        using HttpClient httpClient = configuredFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtHelper.GenerateToken(
                tenants: ["tenant-a"],
                domains: ["freshness"],
                permissions: ["query:read"]));
        var client = new EventStoreGatewayClient(
            httpClient,
            Options.Create(new EventStoreGatewayClientOptions()));
        var request = new SubmitQueryRequest(
            Tenant: "tenant-a",
            Domain: "freshness",
            AggregateId: "index",
            QueryType: "get-freshness",
            ProjectionType: "freshness");

        EventStoreQueryResult result = await client.SubmitQueryAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ETag.ShouldBe(currentETag);
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        result.Metadata.ProjectionVersion.ShouldBe(ExpectedVersion);
        result.Metadata.Lifecycle.ShouldBe(expectedLifecycle);
        result.Metadata.IsStale.ShouldBe(expectedIsStale);
        JsonElement payload = result.Payload.ShouldNotBeNull();
        payload.GetProperty("Value").GetInt32().ShouldBe(42);

        ReadModelEntry<PersistedFreshnessReadModel> persistedAfterUntypedQuery = await store
            .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, TestContext.Current.CancellationToken);
        PersistedFreshnessReadModel modelAfterUntypedQuery = persistedAfterUntypedQuery.Value.ShouldNotBeNull();
        modelAfterUntypedQuery.ProjectedAt.ShouldBe(persistedModel.ProjectedAt);
        modelAfterUntypedQuery.ProjectionVersion.ShouldBe(ExpectedVersion);

        EventStoreQueryResult<PersistedPayload> typedResult = await client.SubmitQueryAsync<PersistedPayload>(
            request,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = typedResult.Metadata.ShouldNotBeNull();
        typedResult.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        typedResult.Metadata.Lifecycle.ShouldBe(expectedLifecycle);
        typedResult.Metadata.ProjectionVersion.ShouldBe(ExpectedVersion);
        typedResult.Metadata.IsStale.ShouldBe(expectedIsStale);
        typedResult.Payload.ShouldNotBeNull().Value.ShouldBe(42);

        ReadModelEntry<PersistedFreshnessReadModel> persistedAfterTypedQuery = await store
            .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, TestContext.Current.CancellationToken);
        PersistedFreshnessReadModel modelAfterTypedQuery = persistedAfterTypedQuery.Value.ShouldNotBeNull();
        modelAfterTypedQuery.ProjectedAt.ShouldBe(persistedModel.ProjectedAt);
        modelAfterTypedQuery.ProjectionVersion.ShouldBe(ExpectedVersion);
    }

    private sealed record PersistedPayload(int Value);
}
