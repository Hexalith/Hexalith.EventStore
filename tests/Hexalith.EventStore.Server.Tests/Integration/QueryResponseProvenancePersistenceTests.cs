extern alias eventstore;

using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
    [InlineData(-1, ProjectionLifecycleState.Current, false, "model-v42", true)]
    [InlineData(-10, ProjectionLifecycleState.Stale, true, "model,v42", true)]
    [InlineData(-1, ProjectionLifecycleState.Current, false, "modèle-v42", false)]
    [InlineData(-10, ProjectionLifecycleState.Stale, true, "version-☃", false)]
    [InlineData(-1, ProjectionLifecycleState.Current, false, " model-v42", false)]
    [InlineData(-10, ProjectionLifecycleState.Stale, true, "model-v42 ", false)]
    [InlineData(-1, ProjectionLifecycleState.Current, false, " ", false)]
    public async Task PersistedFreshness_TraversesRouterHandlerControllerAndGatewayClient(
        int projectedMinutes,
        ProjectionLifecycleState expectedLifecycle,
        bool expectedIsStale,
        string expectedVersion,
        bool expectedNotModified)
    {
        const string StoreName = "test-readmodels";
        const string Key = "freshness:index";
        string? expectedHeaderVersion = expectedNotModified ? expectedVersion : null;
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
                    "counter",
                    metadata);
            });
        var queryRouter = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        IETagService eTagService = Substitute.For<IETagService>();

        using WebApplicationFactory<EventStoreProgram> configuredFactory = CreateKestrelFactory(queryRouter, eTagService);
        using HttpClient httpClient = CreateAuthorizedClient(configuredFactory);
        var client = new EventStoreGatewayClient(
            httpClient,
            Options.Create(new EventStoreGatewayClientOptions()));
        SubmitQueryRequest request = CreateCounterQuery();

        using HttpResponseMessage rawResponse = await httpClient.PostAsJsonAsync(
            "/api/v1/queries", request, TestContext.Current.CancellationToken);
        rawResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        rawResponse.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{persistedEntry.ETag}\"");
        rawResponse.Headers.TryGetValues("X-Hexalith-Projection-Version", out IEnumerable<string>? versionValues)
            .ShouldBe(expectedHeaderVersion is not null);
        if (expectedHeaderVersion is not null) {
            versionValues.ShouldNotBeNull().Single().ShouldBe(expectedHeaderVersion);
        }

        using JsonDocument rawBody = await JsonDocument.ParseAsync(
            await rawResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        JsonElement rawMetadata = rawBody.RootElement.GetProperty("metadata");
        rawMetadata.GetProperty("eTag").GetString().ShouldBe(persistedEntry.ETag);
        rawMetadata.GetProperty("projectionVersion").GetString().ShouldBe(expectedVersion);
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
        conditional.IsNotModified.ShouldBe(expectedNotModified);
        if (expectedNotModified) {
            conditional.Payload.ShouldBeNull();
        }
        else {
            conditional.Payload.ShouldNotBeNull().GetProperty("Value").GetInt32().ShouldBe(42);
        }
        conditional.ETag.ShouldBe(persistedEntry.ETag);
        QueryResponseMetadata conditionalMetadata = conditional.Metadata.ShouldNotBeNull();
        conditionalMetadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        conditionalMetadata.Lifecycle.ShouldBe(expectedLifecycle);
        conditionalMetadata.ProjectionVersion.ShouldBe(expectedVersion);
        conditionalMetadata.ServedAt.ShouldBe(now);
        conditionalMetadata.IsStale.ShouldBe(expectedIsStale);
        conditionalMetadata.WarningCodes.ShouldBe([QueryWarningCodes.DegradedSearch, "source.warning-2"]);

        EventStoreQueryResult<PersistedPayload> typedConditional = await client.SubmitQueryAsync<PersistedPayload>(
            request,
            result.ETag,
            TestContext.Current.CancellationToken);
        typedConditional.IsNotModified.ShouldBe(expectedNotModified);
        if (expectedNotModified) {
            typedConditional.Payload.ShouldBeNull();
        }
        else {
            typedConditional.Payload.ShouldNotBeNull().Value.ShouldBe(42);
        }
        typedConditional.ETag.ShouldBe(persistedEntry.ETag);
        QueryResponseMetadata typedConditionalMetadata = typedConditional.Metadata.ShouldNotBeNull();
        typedConditionalMetadata.ProjectionVersion.ShouldBe(expectedVersion);
        typedConditionalMetadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        typedConditionalMetadata.Lifecycle.ShouldBe(expectedLifecycle);
        typedConditionalMetadata.IsStale.ShouldBe(expectedIsStale);
        typedConditionalMetadata.ServedAt.ShouldBe(now);
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

    [Theory]
    [InlineData("raw")]
    [InlineData("untyped")]
    [InlineData("typed")]
    public async Task UntaggedPersistedRead_AdvancingProjectionCannotSupplyValidator(string transport) {
        const string StoreName = "test-readmodels";
        const string Key = "conditional:index";
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTimeOffset servedAt = new(2026, 11, 28, 8, 0, 0, TimeSpan.Zero);
        var paging = new QueryPagingMetadata(PageSize: 10, Offset: 0, TotalCount: 1, HasMore: false);
        var store = new InMemoryReadModelStore();
        var revisionOne = new PersistedFreshnessReadModel(1, servedAt, "revision-1");
        PersistedFreshnessReadModel revisionTwo = revisionOne with { Value = 2, ProjectionVersion = "revision-2" };
        await store.SaveAsync(StoreName, Key, revisionOne, cancellationToken);
        ReadModelEntry<PersistedFreshnessReadModel> before = await store.GetAsync<PersistedFreshnessReadModel>(StoreName, Key, cancellationToken);
        before.Value.ShouldBe(revisionOne);

        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                CancellationToken requestCancellationToken = call.ArgAt<CancellationToken>(3);
                ReadModelEntry<PersistedFreshnessReadModel> read = await store.GetAsync<PersistedFreshnessReadModel>(StoreName, Key, requestCancellationToken);
                PersistedFreshnessReadModel selected = read.Value.ShouldNotBeNull();
                if (selected.Value == revisionOne.Value) {
                    // Commit revision two after selecting revision one and before the router receives it.
                    await store.SaveAsync(StoreName, Key, revisionTwo, requestCancellationToken);
                }

                return QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { selected.Value }), "counter",
                    new QueryResponseMetadata(
                        IsStale: true,
                        IsDegraded: true,
                        ProjectionVersion: selected.ProjectionVersion,
                        ServedAt: selected.ProjectedAt,
                        Paging: paging,
                        WarningCodes: [QueryWarningCodes.DegradedSearch]) {
                        Lifecycle = ProjectionLifecycleState.Stale,
                    });
            });
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant-a", Arg.Any<CancellationToken>())
            .Returns<Task<string?>>(async call => GetSelfRoutingTag(
                await store.GetAsync<PersistedFreshnessReadModel>(StoreName, Key, call.ArgAt<CancellationToken>(2))));
        using WebApplicationFactory<EventStoreProgram> configuredFactory = CreateKestrelFactory(router, eTagService);
        using HttpClient httpClient = CreateAuthorizedClient(configuredFactory);
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));
        SubmitQueryRequest request = CreateCounterQuery();

        await AssertUntaggedQueryAsync(revisionOne, ifNoneMatch: null);
        ReadModelEntry<PersistedFreshnessReadModel> advanced = await store.GetAsync<PersistedFreshnessReadModel>(StoreName, Key, cancellationToken);
        advanced.Value.ShouldBe(revisionTwo);
        string laterTag = GetSelfRoutingTag(advanced);
        laterTag.ShouldNotBe(GetSelfRoutingTag(before));

        await AssertUntaggedQueryAsync(revisionTwo, laterTag);
        ReadModelEntry<PersistedFreshnessReadModel> after = await store.GetAsync<PersistedFreshnessReadModel>(StoreName, Key, cancellationToken);
        after.Value.ShouldBe(revisionTwo);
        after.ETag.ShouldBe(advanced.ETag);
        _ = await invoker.Received(2).InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
        eTagService.ReceivedCalls().ShouldBeEmpty();

        async Task AssertUntaggedQueryAsync(PersistedFreshnessReadModel expected, string? ifNoneMatch) {
            QueryResponseMetadata metadata;
            if (transport == "raw") {
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
                    Content = JsonContent.Create(request),
                };
                if (ifNoneMatch is not null) {
                    message.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{ifNoneMatch}\""));
                }

                using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.Headers.Contains("ETag").ShouldBeFalse();
                SubmitQueryResponse body = (await response.Content.ReadFromJsonAsync<SubmitQueryResponse>(cancellationToken))
                    .ShouldNotBeNull();
                body.Payload.GetProperty("Value").GetInt32().ShouldBe(expected.Value);
                metadata = body.Metadata.ShouldNotBeNull();
            }
            else if (transport == "typed") {
                EventStoreQueryResult<PersistedPayload> result = await client.SubmitQueryAsync<PersistedPayload>(request, ifNoneMatch, cancellationToken);
                result.IsNotModified.ShouldBeFalse();
                result.ETag.ShouldBeNull();
                result.Payload.ShouldNotBeNull().Value.ShouldBe(expected.Value);
                metadata = result.Metadata.ShouldNotBeNull();
            }
            else {
                EventStoreQueryResult result = await client.SubmitQueryAsync(request, ifNoneMatch, cancellationToken);
                result.IsNotModified.ShouldBeFalse();
                result.ETag.ShouldBeNull();
                result.Payload.ShouldNotBeNull().GetProperty("Value").GetInt32().ShouldBe(expected.Value);
                metadata = result.Metadata.ShouldNotBeNull();
            }

            metadata.ETag.ShouldBeNull();
            metadata.IsNotModified.ShouldBe(false);
            metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
            metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Stale);
            metadata.ProjectionVersion.ShouldBe(expected.ProjectionVersion);
            metadata.IsStale.ShouldBe(true);
            metadata.IsDegraded.ShouldBe(true);
            metadata.ServedAt.ShouldBe(expected.ProjectedAt);
            metadata.Paging.ShouldBe(paging);
            metadata.WarningCodes.ShouldBe([QueryWarningCodes.DegradedSearch]);
        }

        static string GetSelfRoutingTag(ReadModelEntry<PersistedFreshnessReadModel> entry)
            => SelfRoutingETag.Encode("counter", Base64Url.EncodeToString(
                SHA256.HashData(Encoding.UTF8.GetBytes(entry.ETag.ShouldNotBeNull())).AsSpan(0, 16)));
    }

    private WebApplicationFactory<EventStoreProgram> CreateKestrelFactory(IQueryRouter router, IETagService eTagService) {
        factory.ResetActors();
        factory.FakeTenantActor.ConfiguredResult = new ActorValidationResponse(true);
        factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);
        WebApplicationFactory<EventStoreProgram> configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQueryRouter>();
                services.RemoveAll<IETagService>();
                services.AddSingleton(router);
                services.AddSingleton(eTagService);
            }));
        configuredFactory.UseKestrel(0);
        return configuredFactory;
    }

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<EventStoreProgram> configuredFactory) {
        HttpClient httpClient = configuredFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtHelper.GenerateToken(
                tenants: ["tenant-a"],
                domains: ["counter"],
                permissions: ["query:read"]));
        return httpClient;
    }

    // The gateway admits only queries declared by the committed Development catalog.
    private static SubmitQueryRequest CreateCounterQuery() => new(
        Tenant: "tenant-a",
        Domain: "counter",
        AggregateId: "index",
        QueryType: "get-counter-status",
        ProjectionType: "counter");

    private sealed record PersistedPayload(int Value);
}
