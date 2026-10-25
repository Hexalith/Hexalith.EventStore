extern alias eventstore;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
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
    [InlineData(-1, ProjectionLifecycleState.Current, false, "model-v42")]
    [InlineData(-10, ProjectionLifecycleState.Stale, true, "model,v42")]
    [InlineData(-1, ProjectionLifecycleState.Current, false, "modèle-v42")]
    [InlineData(-10, ProjectionLifecycleState.Stale, true, "version-☃")]
    public async Task PersistedFreshness_TraversesRouterHandlerControllerAndGatewayClient(
        int projectedMinutes,
        ProjectionLifecycleState expectedLifecycle,
        bool expectedIsStale,
        string expectedVersion)
    {
        const string StoreName = "test-readmodels";
        const string Key = "freshness:index";
        string? expectedHeaderVersion = expectedVersion.All(static character => character <= 0x7e) ? expectedVersion : null;
        DateTimeOffset now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var thresholds = ReadModelFreshnessThresholds.Create(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5));
        var store = new InMemoryReadModelStore();
        var persistedModel = new PersistedFreshnessReadModel(
            Value: 42,
            ProjectedAt: now.AddMinutes(projectedMinutes),
            ProjectionVersion: expectedVersion);
        await store.SaveAsync(StoreName, Key, persistedModel, TestContext.Current.CancellationToken);

        ReadModelEntry<PersistedFreshnessReadModel> persistedEntry = await store
            .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, TestContext.Current.CancellationToken);
        _ = persistedEntry.Value.ShouldNotBeNull();
        persistedEntry.Value.ProjectedAt.ShouldBe(persistedModel.ProjectedAt);
        persistedEntry.Value.ProjectionVersion.ShouldBe(expectedVersion);

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
                    read.ETag) with {
                        WarningCodes = [QueryWarningCodes.DegradedSearch, "source.warning-2"],
                    };
                return QueryResult.FromPayload(
                    JsonSerializer.SerializeToElement(new { model.Value }),
                    "freshness",
                    metadata);
            });
        var queryRouter = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        IETagService eTagService = Substitute.For<IETagService>();

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
        configuredFactory.UseKestrel(0);
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

        using HttpResponseMessage rawResponse = await httpClient.PostAsJsonAsync(
            "/api/v1/queries", request, TestContext.Current.CancellationToken);
        rawResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        rawResponse.Headers.TryGetValues("X-Hexalith-Projection-Version", out IEnumerable<string>? versionValues)
            .ShouldBe(expectedHeaderVersion is not null);
        if (expectedHeaderVersion is not null) {
            versionValues.ShouldNotBeNull().Single().ShouldBe(expectedHeaderVersion);
        }

        using JsonDocument rawBody = await JsonDocument.ParseAsync(
            await rawResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        rawBody.RootElement.GetProperty("metadata").GetProperty("projectionVersion").GetString().ShouldBe(expectedVersion);
        rawResponse.Headers.GetValues("X-Hexalith-Warning-Codes").Single().ShouldBe("degraded_search,source.warning-2");

        EventStoreQueryResult result = await client.SubmitQueryAsync(
            request,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ETag.ShouldBe(persistedEntry.ETag);
        _ = await eTagService.DidNotReceiveWithAnyArgs()
            .GetCurrentETagAsync(default!, default!, default);
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        result.Metadata.ProjectionVersion.ShouldBe(expectedVersion);
        result.Metadata.Lifecycle.ShouldBe(expectedLifecycle);
        result.Metadata.IsStale.ShouldBe(expectedIsStale);
        JsonElement payload = result.Payload.ShouldNotBeNull();
        payload.GetProperty("Value").GetInt32().ShouldBe(42);

        ReadModelEntry<PersistedFreshnessReadModel> persistedAfterUntypedQuery = await store
            .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, TestContext.Current.CancellationToken);
        PersistedFreshnessReadModel modelAfterUntypedQuery = persistedAfterUntypedQuery.Value.ShouldNotBeNull();
        modelAfterUntypedQuery.ProjectedAt.ShouldBe(persistedModel.ProjectedAt);
        modelAfterUntypedQuery.ProjectionVersion.ShouldBe(expectedVersion);

        EventStoreQueryResult<PersistedPayload> typedResult = await client.SubmitQueryAsync<PersistedPayload>(
            request,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = typedResult.Metadata.ShouldNotBeNull();
        typedResult.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        typedResult.Metadata.Lifecycle.ShouldBe(expectedLifecycle);
        typedResult.Metadata.ProjectionVersion.ShouldBe(expectedVersion);
        typedResult.Metadata.IsStale.ShouldBe(expectedIsStale);
        typedResult.Payload.ShouldNotBeNull().Value.ShouldBe(42);

        EventStoreQueryResult conditional = await client.SubmitQueryAsync(
            request,
            result.ETag,
            TestContext.Current.CancellationToken);
        conditional.IsNotModified.ShouldBeTrue();
        conditional.Payload.ShouldBeNull();
        conditional.ETag.ShouldBe(persistedEntry.ETag);
        QueryResponseMetadata conditionalMetadata = conditional.Metadata.ShouldNotBeNull();
        conditionalMetadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        conditionalMetadata.Lifecycle.ShouldBe(expectedLifecycle);
        conditionalMetadata.ProjectionVersion.ShouldBe(expectedHeaderVersion);
        conditionalMetadata.ServedAt.ShouldBe(now);
        conditionalMetadata.IsStale.ShouldBe(expectedIsStale);
        conditionalMetadata.WarningCodes.ShouldBe([QueryWarningCodes.DegradedSearch, "source.warning-2"]);

        EventStoreQueryResult<PersistedPayload> typedConditional = await client.SubmitQueryAsync<PersistedPayload>(
            request,
            result.ETag,
            TestContext.Current.CancellationToken);
        typedConditional.IsNotModified.ShouldBeTrue();
        typedConditional.Payload.ShouldBeNull();
        typedConditional.ETag.ShouldBe(persistedEntry.ETag);
        QueryResponseMetadata typedConditionalMetadata = typedConditional.Metadata.ShouldNotBeNull();
        typedConditionalMetadata.ProjectionVersion.ShouldBe(expectedHeaderVersion);
        typedConditionalMetadata.Lifecycle.ShouldBe(expectedLifecycle);
        typedConditionalMetadata.WarningCodes.ShouldBe(conditionalMetadata.WarningCodes);
        _ = await eTagService.DidNotReceiveWithAnyArgs().GetCurrentETagAsync(default!, default!, default);

        ReadModelEntry<PersistedFreshnessReadModel> persistedAfterTypedQuery = await store
            .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, TestContext.Current.CancellationToken);
        PersistedFreshnessReadModel modelAfterTypedQuery = persistedAfterTypedQuery.Value.ShouldNotBeNull();
        modelAfterTypedQuery.ProjectedAt.ShouldBe(persistedModel.ProjectedAt);
        modelAfterTypedQuery.ProjectionVersion.ShouldBe(expectedVersion);
        persistedAfterTypedQuery.ETag.ShouldBe(persistedEntry.ETag);

        await store.SaveAsync(StoreName, Key, persistedModel with {
            Value = 43,
            ProjectionVersion = "model-v43",
        }, TestContext.Current.CancellationToken);
        ReadModelEntry<PersistedFreshnessReadModel> changedEntry = await store
            .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, TestContext.Current.CancellationToken);
        changedEntry.ETag.ShouldNotBe(persistedEntry.ETag);
        changedEntry.Value.ShouldNotBeNull().Value.ShouldBe(43);
        changedEntry.Value.ProjectionVersion.ShouldBe("model-v43");

        EventStoreQueryResult changed = await client.SubmitQueryAsync(request, result.ETag, TestContext.Current.CancellationToken);
        changed.IsNotModified.ShouldBeFalse();
        changed.Payload.ShouldNotBeNull().GetProperty("Value").GetInt32().ShouldBe(43);
        changed.ETag.ShouldBe(changedEntry.ETag);
        changed.Metadata.ShouldNotBeNull().ProjectionVersion.ShouldBe("model-v43");
        EventStoreQueryResult<PersistedPayload> typedChanged = await client.SubmitQueryAsync<PersistedPayload>(
            request, result.ETag, TestContext.Current.CancellationToken);
        typedChanged.IsNotModified.ShouldBeFalse();
        typedChanged.Payload.ShouldNotBeNull().Value.ShouldBe(43);
        typedChanged.Metadata.ShouldNotBeNull().ProjectionVersion.ShouldBe("model-v43");

        EventStoreQueryResult changedConditional = await client.SubmitQueryAsync(
            request, changed.ETag, TestContext.Current.CancellationToken);
        changedConditional.IsNotModified.ShouldBeTrue();
        changedConditional.Payload.ShouldBeNull();
        changedConditional.Metadata.ShouldNotBeNull().ProjectionVersion.ShouldBe("model-v43");
        EventStoreQueryResult<PersistedPayload> typedChangedConditional = await client.SubmitQueryAsync<PersistedPayload>(
            request, changed.ETag, TestContext.Current.CancellationToken);
        typedChangedConditional.IsNotModified.ShouldBeTrue();
        typedChangedConditional.Payload.ShouldBeNull();
        typedChangedConditional.Metadata.ShouldNotBeNull().ProjectionVersion.ShouldBe("model-v43");
        typedChangedConditional.Metadata.WarningCodes.ShouldBe(conditionalMetadata.WarningCodes);

        ReadModelEntry<PersistedFreshnessReadModel> afterRevalidation = await store
            .GetAsync<PersistedFreshnessReadModel>(StoreName, Key, TestContext.Current.CancellationToken);
        afterRevalidation.Value.ShouldBe(changedEntry.Value);
        afterRevalidation.ETag.ShouldBe(changedEntry.ETag);
        _ = await invoker.Received(9).InvokeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    private sealed record PersistedPayload(int Value);
}
