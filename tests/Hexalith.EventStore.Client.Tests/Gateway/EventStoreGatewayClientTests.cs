using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Gateway;

public class EventStoreGatewayClientTests {
    [Fact]
    public async Task SubmitCommandAsync_PostsContractRequestAndReturnsCorrelationId() {
        HttpRequestMessage? observedRequest = null;
        string? observedBody = null;
        using HttpClient httpClient = CreateClient(async request => {
            observedRequest = request;
            observedBody = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
            return Json(HttpStatusCode.Accepted, "{\"correlationId\":\"corr-1\"}");
        });

        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));
        SubmitCommandRequest request = CreateCommandRequest();

        SubmitCommandResponse response = await client.SubmitCommandAsync(request);

        response.CorrelationId.ShouldBe("corr-1");
        _ = observedRequest.ShouldNotBeNull();
        observedRequest.Method.ShouldBe(HttpMethod.Post);
        observedRequest.RequestUri!.AbsolutePath.ShouldBe("/api/v1/commands");
        _ = observedBody.ShouldNotBeNull();
        observedBody.ShouldContain("\"messageId\":\"message-1\"");
        observedBody.ShouldContain("\"aggregateId\":\"party-1\"");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithNotModified_ReturnsCacheResultAndETag() {
        HttpRequestMessage? observedRequest = null;
        using HttpClient httpClient = CreateClient(request => {
            observedRequest = request;
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-1\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, "Current");
            return Task.FromResult(response);
        });

        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest(), "\"etag-1\"");

        result.IsNotModified.ShouldBeTrue();
        result.Payload.ShouldBeNull();
        result.ETag.ShouldBe("etag-1");
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.ETag.ShouldBe("etag-1");
        result.Metadata.IsNotModified.ShouldBe(true);
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
        _ = observedRequest.ShouldNotBeNull();
        observedRequest.Headers.TryGetValues("If-None-Match", out IEnumerable<string>? values).ShouldBeTrue();
        _ = values.ShouldNotBeNull();
        values.Single().ShouldBe("\"etag-1\"");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithPayload_ReturnsTypedPayloadAndETag() {
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(
                HttpStatusCode.OK,
                "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3},\"metadata\":{\"provenance\":\"ProjectionBacked\",\"isStale\":false,\"paging\":{\"pageSize\":25,\"offset\":50,\"hasMore\":true}}}");
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-2\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            return Task.FromResult(response);
        });

        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult<CounterDto> result = await client.SubmitQueryAsync<CounterDto>(CreateQueryRequest());

        result.IsNotModified.ShouldBeFalse();
        result.CorrelationId.ShouldBe("corr-2");
        result.ETag.ShouldBe("etag-2");
        _ = result.Payload.ShouldNotBeNull();
        result.Payload.Count.ShouldBe(3);
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.ETag.ShouldBe("etag-2");
        result.Metadata.IsNotModified.ShouldBe(false);
        _ = result.Metadata.Paging.ShouldNotBeNull();
        result.Metadata.Paging.PageSize.ShouldBe(25);
        result.Metadata.Paging.Offset.ShouldBe(50);
        result.Metadata.Paging.HasMore.ShouldBe(true);
    }

    [Fact]
    public async Task SubmitQueryAsync_TypedAndUntypedResultsExposeEquivalentMetadata() {
        const string QueryJson = """
            {
              "correlationId": "corr-2",
              "payload": { "count": 3 },
              "metadata": {
                "provenance": "ProjectionBacked",
                "lifecycle": "Degraded",
                "etag": "body-etag",
                "isStale": true,
                "isDegraded": true,
                "projectionVersion": "party-v3",
                "servedAt": "2026-07-06T08:45:00Z",
                "paging": {
                  "pageSize": 25,
                  "offset": 50,
                  "nextCursor": "next-page",
                  "totalCount": 100,
                  "hasMore": true
                },
                "warningCodes": [ "degraded_search" ]
              }
            }
            """;
        using HttpClient untypedHttpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(HttpStatusCode.OK, QueryJson);
            response.Headers.ETag = new EntityTagHeaderValue("\"gateway-etag\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, "Degraded");
            return Task.FromResult(response);
        });
        using HttpClient typedHttpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(HttpStatusCode.OK, QueryJson);
            response.Headers.ETag = new EntityTagHeaderValue("\"gateway-etag\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, "Degraded");
            return Task.FromResult(response);
        });

        var untypedClient = new EventStoreGatewayClient(untypedHttpClient, Options.Create(new EventStoreGatewayClientOptions()));
        var typedClient = new EventStoreGatewayClient(typedHttpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult untyped = await untypedClient.SubmitQueryAsync(CreateQueryRequest());
        EventStoreQueryResult<CounterDto> typed = await typedClient.SubmitQueryAsync<CounterDto>(CreateQueryRequest());

        _ = untyped.Metadata.ShouldNotBeNull();
        _ = typed.Metadata.ShouldNotBeNull();
        typed.Metadata.ETag.ShouldBe(untyped.Metadata.ETag);
        typed.Metadata.IsNotModified.ShouldBe(untyped.Metadata.IsNotModified);
        typed.Metadata.IsStale.ShouldBe(untyped.Metadata.IsStale);
        typed.Metadata.IsDegraded.ShouldBe(untyped.Metadata.IsDegraded);
        typed.Metadata.ProjectionVersion.ShouldBe(untyped.Metadata.ProjectionVersion);
        typed.Metadata.Provenance.ShouldBe(untyped.Metadata.Provenance);
        typed.Metadata.Lifecycle.ShouldBe(untyped.Metadata.Lifecycle);
        typed.Metadata.ServedAt.ShouldBe(untyped.Metadata.ServedAt);
        typed.Metadata.Paging.ShouldBe(untyped.Metadata.Paging);
        _ = typed.Metadata.WarningCodes.ShouldNotBeNull();
        _ = untyped.Metadata.WarningCodes.ShouldNotBeNull();
        typed.Metadata.WarningCodes.ToArray().ShouldBe(untyped.Metadata.WarningCodes.ToArray());
        untyped.Metadata.ETag.ShouldBe("gateway-etag");
        untyped.Metadata.IsNotModified.ShouldBe(false);
        untyped.Metadata.IsStale.ShouldBeNull();
        untyped.Metadata.IsDegraded.ShouldBe(true);
        untyped.Metadata.ProjectionVersion.ShouldBe("party-v3");
        untyped.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        untyped.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Degraded);
        _ = untyped.Metadata.Paging.ShouldNotBeNull();
        untyped.Metadata.Paging.NextCursor.ShouldBe("next-page");
        untyped.Metadata.Paging.TotalCount.ShouldBe(100);
        untyped.Metadata.Paging.HasMore.ShouldBe(true);
        _ = untyped.Metadata.WarningCodes.ShouldNotBeNull();
        untyped.Metadata.WarningCodes.ShouldContain(QueryWarningCodes.DegradedSearch);
    }

    [Fact]
    public async Task SubmitQueryAsyncTyped_WithNotModified_ReturnsMetadataWithNormalizedETag() {
        using HttpClient httpClient = CreateClient(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-typed\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, "Current");
            return Task.FromResult(response);
        });

        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult<CounterDto> result = await client.SubmitQueryAsync<CounterDto>(CreateQueryRequest(), "etag-typed");

        result.IsNotModified.ShouldBeTrue();
        result.ETag.ShouldBe("etag-typed");
        result.Payload.ShouldBeNull();
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.ETag.ShouldBe("etag-typed");
        result.Metadata.IsNotModified.ShouldBe(true);
        result.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Unexpected")]
    [InlineData("current")]
    public async Task SubmitQueryAsync_WithUnsafeNotModifiedLifecycle_DefaultsUnknown(string? lifecycle) {
        using HttpClient httpClient = CreateClient(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-lifecycle\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            if (lifecycle is not null) {
                response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, lifecycle);
            }

            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest(), "etag-lifecycle");

        result.IsNotModified.ShouldBeTrue();
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitQueryAsync_WithDuplicateNotModifiedLifecycle_DefaultsUnknown(bool conflicting) {
        using HttpClient httpClient = CreateClient(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-lifecycle\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(
                ProjectionLifecyclePolicy.HeaderName,
                conflicting ? ["Current", "Stale"] : ["Current", "Current"]);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest(), "etag-lifecycle");

        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        result.Metadata.IsStale.ShouldBeNull();
        result.Metadata.IsDegraded.ShouldBeNull();
    }

    [Theory]
    [InlineData("Current", ProjectionLifecycleState.Current, false, null)]
    [InlineData("Stale", ProjectionLifecycleState.Stale, true, null)]
    [InlineData("Rebuilding", ProjectionLifecycleState.Rebuilding, null, null)]
    [InlineData("Degraded", ProjectionLifecycleState.Degraded, null, true)]
    [InlineData("Unavailable", ProjectionLifecycleState.Unavailable, null, null)]
    [InlineData("LocalOnly", ProjectionLifecycleState.LocalOnly, null, null)]
    public async Task SubmitQueryAsync_WithCanonicalNotModifiedLifecycle_ProjectsCompatibilityFields(
        string lifecycle,
        ProjectionLifecycleState expected,
        bool? expectedIsStale,
        bool? expectedIsDegraded) {
        using HttpClient httpClient = CreateClient(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-lifecycle\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, lifecycle);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest(), "etag-lifecycle");

        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Lifecycle.ShouldBe(expected);
        result.Metadata.IsStale.ShouldBe(expectedIsStale);
        result.Metadata.IsDegraded.ShouldBe(expectedIsDegraded);
    }

    [Theory]
    [InlineData("Current")]
    [InlineData("Stale")]
    [InlineData("Rebuilding")]
    [InlineData("Degraded")]
    [InlineData("Unavailable")]
    [InlineData("LocalOnly")]
    public async Task SubmitQueryAsync_WithAuthoritativeLifecycle_PreservesExactValue(string lifecycle) {
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(
                HttpStatusCode.OK,
                $$$"""{"correlationId":"corr-lifecycle","payload":{"count":3},"metadata":{"provenance":"ProjectionBacked","lifecycle":"{{{lifecycle}}}"}}""");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, lifecycle);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest());

        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Lifecycle.ShouldBe(Enum.Parse<ProjectionLifecycleState>(lifecycle));
    }

    [Fact]
    public async Task SubmitQueryAsync_WithMismatchedLifecycle_DefaultsUnknown() {
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(
                HttpStatusCode.OK,
                "{\"correlationId\":\"corr-lifecycle\",\"payload\":{\"count\":3},\"metadata\":{\"provenance\":\"ProjectionBacked\",\"lifecycle\":\"Current\",\"isStale\":false}}");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, "Stale");
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest());

        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        result.Metadata.IsStale.ShouldBe(false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitQueryAsync_WithDuplicateSuccessLifecycle_DefaultsUnknown(bool conflicting) {
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(
                HttpStatusCode.OK,
                "{\"correlationId\":\"corr-lifecycle\",\"payload\":{\"count\":3},\"metadata\":{\"provenance\":\"ProjectionBacked\",\"lifecycle\":\"Current\",\"isStale\":false}}");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            response.Headers.Add(
                ProjectionLifecyclePolicy.HeaderName,
                conflicting ? ["Current", "Stale"] : ["Current", "Current"]);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest());

        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        result.Metadata.IsStale.ShouldBe(false);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("\"current\"", "current")]
    [InlineData("1", "1")]
    [InlineData("\"Unexpected\"", "Unexpected")]
    public async Task SubmitQueryAsync_WithUnsafeSuccessLifecycle_DefaultsUnknown(
        string? bodyLifecycle,
        string? headerLifecycle) {
        string lifecycleProperty = bodyLifecycle is null ? string.Empty : $",\"lifecycle\":{bodyLifecycle}";
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(
                HttpStatusCode.OK,
                $"{{\"correlationId\":\"corr-lifecycle\",\"payload\":{{\"count\":3}},\"metadata\":{{\"provenance\":\"ProjectionBacked\"{lifecycleProperty}}}}}");
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            if (headerLifecycle is not null) {
                response.Headers.Add(ProjectionLifecyclePolicy.HeaderName, headerLifecycle);
            }

            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest());

        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Unexpected")]
    [InlineData("projectionbacked")]
    public async Task SubmitQueryAsync_WithUnsafeNotModifiedProvenance_ThrowsGatewayException(string? provenance) {
        using HttpClient httpClient = CreateClient(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-unsafe\"");
            if (provenance is not null) {
                response.Headers.Add("X-Hexalith-Query-Provenance", provenance);
            }

            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException exception = await Should.ThrowAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest(), "etag-unsafe"));

        exception.StatusCode.ShouldBe(502);
    }

    [Fact]
    public async Task SubmitQueryAsync_WithWildcardNotModifiedETag_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = EntityTagHeaderValue.Any;
            response.Headers.Add("X-Hexalith-Query-Provenance", "ProjectionBacked");
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException exception = await Should.ThrowAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest(), "etag-unsafe"));

        exception.StatusCode.ShouldBe(502);
    }

    [Fact]
    public async Task SubmitQueryAsync_WithDuplicateNotModifiedProvenance_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-unsafe\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", ["ProjectionBacked", "ProjectionBacked"]);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException exception = await Should.ThrowAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest(), "etag-unsafe"));

        exception.StatusCode.ShouldBe(502);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Unexpected")]
    [InlineData("HandlerComputed")]
    public async Task SubmitQueryAsync_WithContradictorySuccessProvenance_DefaultsUnknownAndClearsProjectionEvidence(
        string? headerProvenance) {
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(
                HttpStatusCode.OK,
                "{\"correlationId\":\"corr-safe\",\"payload\":{\"count\":3},\"metadata\":{\"provenance\":\"ProjectionBacked\",\"etag\":\"body-etag\",\"isNotModified\":true,\"isStale\":false,\"isDegraded\":true,\"projectionVersion\":\"v9\"}}");
            response.Headers.ETag = new EntityTagHeaderValue("\"http-etag\"");
            if (headerProvenance is not null) {
                response.Headers.Add("X-Hexalith-Query-Provenance", headerProvenance);
            }

            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest());

        result.ETag.ShouldBeNull();
        result.IsNotModified.ShouldBeFalse();
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
        result.Metadata.ETag.ShouldBeNull();
        result.Metadata.IsNotModified.ShouldBeNull();
        result.Metadata.IsStale.ShouldBeNull();
        result.Metadata.ProjectionVersion.ShouldBeNull();
        result.Metadata.IsDegraded.ShouldBe(true);
    }

    [Fact]
    public async Task SubmitQueryAsync_WithDuplicateSuccessProvenance_DefaultsUnknownAndClearsProjectionEvidence() {
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(
                HttpStatusCode.OK,
                "{\"correlationId\":\"corr-safe\",\"payload\":{\"count\":3},\"metadata\":{\"provenance\":\"ProjectionBacked\",\"etag\":\"body-etag\",\"isNotModified\":true,\"isStale\":false,\"projectionVersion\":\"v9\"}}");
            response.Headers.ETag = new EntityTagHeaderValue("\"http-etag\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", ["ProjectionBacked", "ProjectionBacked"]);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest());

        result.ETag.ShouldBeNull();
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
        result.Metadata.ETag.ShouldBeNull();
        result.Metadata.IsNotModified.ShouldBeNull();
        result.Metadata.IsStale.ShouldBeNull();
        result.Metadata.ProjectionVersion.ShouldBeNull();
    }

    [Theory]
    [InlineData("HandlerComputed")]
    [InlineData("Unknown")]
    public async Task SubmitQueryAsync_WithNonProjectionProvenance_SanitizesProjectionEvidence(string provenance) {
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(
                HttpStatusCode.OK,
                $$$"""{"correlationId":"corr-safe","payload":{"count":3},"metadata":{"provenance":"{{{provenance}}}","etag":"body-etag","isNotModified":true,"isStale":false,"isDegraded":true,"projectionVersion":"v9","warningCodes":["degraded_search"]}}""");
            response.Headers.ETag = new EntityTagHeaderValue("\"http-etag\"");
            response.Headers.Add("X-Hexalith-Query-Provenance", provenance);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest());

        result.ETag.ShouldBeNull();
        result.IsNotModified.ShouldBeFalse();
        _ = result.Metadata.ShouldNotBeNull();
        result.Metadata.ETag.ShouldBeNull();
        result.Metadata.IsNotModified.ShouldBeNull();
        result.Metadata.IsStale.ShouldBeNull();
        result.Metadata.ProjectionVersion.ShouldBeNull();
        result.Metadata.IsDegraded.ShouldBe(true);
        result.Metadata.WarningCodes.ShouldBe([QueryWarningCodes.DegradedSearch]);
    }

    [Fact]
    public async Task SubmitCommandAsync_WithProblemDetails_ThrowsGatewayException() {
        const string ProblemJson = """
            {
              "type": "https://hexalith.io/problems/insufficient-permissions",
              "title": "Insufficient Permissions",
              "status": 403,
              "detail": "Missing role.",
              "correlationId": "corr-denied",
              "tenantId": "tenant-a",
              "reason": "missing-role",
              "reasonCode": "insufficient_role",
              "retryAfter": "PT30S",
              "errors": {
                "permissions": "commands:* is required"
              },
              "traceCode": "trace-123"
            }
            """;
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.Forbidden, ProblemJson, "application/problem+json")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitCommandAsync(CreateCommandRequest()));

        ex.StatusCode.ShouldBe(403);
        ex.Title.ShouldBe("Insufficient Permissions");
        ex.Type.ShouldBe("https://hexalith.io/problems/insufficient-permissions");
        ex.Detail.ShouldBe("Missing role.");
        ex.CorrelationId.ShouldBe("corr-denied");
        ex.TenantId.ShouldBe("tenant-a");
        ex.Reason.ShouldBe("missing-role");
        ex.ReasonCode.ShouldBe("insufficient_role");
        ex.RetryAfter.ShouldBe("PT30S");
        ex.Errors.ShouldContainKey("permissions");
        ex.Errors["permissions"].ShouldBe("commands:* is required");
        ex.Extensions.ShouldContainKey("traceCode");
        ex.Extensions["traceCode"].GetString().ShouldBe("trace-123");
    }

    [Fact]
    public async Task SubmitCommandAsync_WithExpiredIdempotencyKey_PreservesCanonicalContract() {
        const string ProblemJson = """
            {
              "type": "https://hexalith.io/problems/idempotency-key-expired",
              "title": "Idempotency Key Expired",
              "status": 409,
              "detail": "Refresh current state, then submit the intended mutation with a new idempotency key.",
              "correlationId": "corr-current",
              "reasonCode": "idempotency_key_expired",
              "code": "idempotency_key_expired",
              "category": "idempotency_key_expired",
              "retryable": false,
              "clientAction": "refresh_state_then_submit_with_new_key"
            }
            """;
        using HttpClient httpClient = CreateClient(
            _ => Task.FromResult(Json(HttpStatusCode.Conflict, ProblemJson, "application/problem+json")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException exception = await Should.ThrowAsync<EventStoreGatewayException>(
            () => client.SubmitCommandAsync(CreateCommandRequest()));

        exception.StatusCode.ShouldBe(409);
        exception.ReasonCode.ShouldBe("idempotency_key_expired");
        exception.CorrelationId.ShouldBe("corr-current");
        exception.RetryAfter.ShouldBeNull();
        exception.Extensions["code"].GetString().ShouldBe("idempotency_key_expired");
        exception.Extensions["category"].GetString().ShouldBe("idempotency_key_expired");
        exception.Extensions["retryable"].GetBoolean().ShouldBeFalse();
        exception.Extensions["clientAction"].GetString()
            .ShouldBe("refresh_state_then_submit_with_new_key");
    }

    [Fact]
    public async Task SubmitCommandAsync_WithRawPayloadTooLargeResponse_ThrowsStatusOnlyGatewayException() {
        using HttpClient httpClient = CreateClient(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge) {
                ReasonPhrase = "Payload Too Large",
                Content = new StringContent("too large", Encoding.UTF8, "text/plain"),
            }));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitCommandAsync(CreateCommandRequest()));

        ex.StatusCode.ShouldBe(413);
        ex.Title.ShouldBe("Payload Too Large");
        ex.CorrelationId.ShouldBeNull();
        ex.Extensions.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitCommandAsync_WithCanceledToken_ThrowsOperationCanceledException() {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        using HttpClient httpClient = CreateClient(_ =>
            Task.FromResult(Json(HttpStatusCode.Accepted, "{\"correlationId\":\"corr-1\"}")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SubmitCommandAsync(CreateCommandRequest(), cancellationSource.Token));
    }

    [Fact]
    public async Task SubmitQueryAsync_WithSuccessFalse_ThrowsSemanticGatewayException() {
        const string QueryJson = """
            {
              "correlationId": "corr-semantic",
              "success": false,
              "errorMessage": "Projection denied.",
              "payload": {
                "reason": "forbidden"
              }
            }
            """;
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, QueryJson)));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Title.ShouldBe("Query semantic failure");
        ex.Detail.ShouldBe("Projection denied.");
        ex.CorrelationId.ShouldBe("corr-semantic");
    }

    [Fact]
    public async Task SubmitQueryAsyncTyped_WithSuccessFalse_ThrowsSemanticGatewayException() {
        const string QueryJson = """
            {
              "correlationId": "corr-semantic",
              "success": false,
              "errorMessage": "Projection denied.",
              "payload": {
                "reason": "forbidden"
              }
            }
            """;
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, QueryJson)));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync<CounterDto>(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Title.ShouldBe("Query semantic failure");
        ex.Detail.ShouldBe("Projection denied.");
        ex.CorrelationId.ShouldBe("corr-semantic");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithUnquotedIfNoneMatch_SendsQuotedHeader() {
        HttpRequestMessage? observedRequest = null;
        using HttpClient httpClient = CreateClient(request => {
            observedRequest = request;
            return Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}"));
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        _ = await client.SubmitQueryAsync(CreateQueryRequest(), "etag-1");

        _ = observedRequest.ShouldNotBeNull();
        observedRequest.Headers.TryGetValues("If-None-Match", out IEnumerable<string>? values).ShouldBeTrue();
        _ = values.ShouldNotBeNull();
        values.Single().ShouldBe("\"etag-1\"");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithEmptyIfNoneMatch_OmitsHeader() {
        HttpRequestMessage? observedRequest = null;
        using HttpClient httpClient = CreateClient(request => {
            observedRequest = request;
            return Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}"));
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        _ = await client.SubmitQueryAsync(CreateQueryRequest(), " ");

        _ = observedRequest.ShouldNotBeNull();
        observedRequest.Headers.Contains("If-None-Match").ShouldBeFalse();
    }

    [Theory]
    [InlineData("W/\"etag-1\"")]
    [InlineData("\"unterminated")]
    [InlineData("*")]
    [InlineData("\"a\", \"b\"")]
    public async Task SubmitQueryAsync_WithUnsupportedIfNoneMatch_OmitsHeaderAndServesBody(string ifNoneMatch) {
        // RFC 9110 conditional forms the gateway cannot express (wildcard, weak validators, tag
        // lists) degrade to an unconditional request instead of failing the call.
        HttpRequestMessage? observedRequest = null;
        using HttpClient httpClient = CreateClient(request => {
            observedRequest = request;
            return Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}"));
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreQueryResult result = await client.SubmitQueryAsync(CreateQueryRequest(), ifNoneMatch);

        _ = observedRequest.ShouldNotBeNull();
        observedRequest.Headers.Contains("If-None-Match").ShouldBeFalse();
        result.IsNotModified.ShouldBeFalse();
    }

    [Fact]
    public async Task SubmitQueryAsync_WhenTransportFails_ThrowsGatewayServiceUnavailable() {
        using HttpClient httpClient = CreateClient(_ => throw new HttpRequestException("connection refused"));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(503);
        _ = ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task SubmitQueryAsync_WithWeakResponseETag_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => {
            HttpResponseMessage response = Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}");
            response.Headers.ETag = new EntityTagHeaderValue("\"etag-2\"", isWeak: true);
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Detail.ShouldBe("Query response contained an unsupported weak ETag.");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithMalformedBody_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, "{")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Detail.ShouldBe("Query response body could not be parsed.");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithEmptyBody_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, string.Empty)));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Detail.ShouldBe("Query response body could not be parsed.");
    }

    [Fact]
    public async Task SubmitQueryAsyncTyped_WithInvalidPayloadShape_ThrowsGatewayException() {
        using HttpClient httpClient = CreateClient(_ => Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":\"invalid\"}}")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync<CounterDto>(CreateQueryRequest()));

        ex.StatusCode.ShouldBe(200);
        ex.Detail.ShouldBe("Query response payload could not be deserialized.");
    }

    [Fact]
    public async Task SubmitCommandAsync_WithNonJsonResponseAndRetryAfterHeader_ThrowsGatewayExceptionWithRetryAfter() {
        using HttpClient httpClient = CreateClient(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
                ReasonPhrase = "Service Unavailable",
                Content = new StringContent("back off", Encoding.UTF8, "text/plain"),
            };
            response.Headers.Add("Retry-After", "120");
            return Task.FromResult(response);
        });
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Assert.ThrowsAsync<EventStoreGatewayException>(
            () => client.SubmitCommandAsync(CreateCommandRequest()));

        ex.StatusCode.ShouldBe(503);
        ex.RetryAfter.ShouldBe("120");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithCanceledToken_ThrowsOperationCanceledException() {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        using HttpClient httpClient = CreateClient(_ =>
            Task.FromResult(Json(HttpStatusCode.OK, "{\"correlationId\":\"corr-2\",\"payload\":{\"count\":3}}")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SubmitQueryAsync(CreateQueryRequest(), cancellationToken: cancellationSource.Token));
    }

    private static SubmitCommandRequest CreateCommandRequest() {
        JsonElement payload = JsonSerializer.SerializeToElement(new { name = "Demo" });
        return new SubmitCommandRequest(
            "message-1",
            "tenant-a",
            "party",
            "party-1",
            "CreateParty",
            payload,
            "message-1");
    }

    [Fact]
    public async Task SubmitQueryAsync_WithProblemDetailsReasonCode_ExposesReasonCodeOnException() {
        const string problemJson = """
            {
                "type": "https://eventstore.hexalith.com/problems/not-found",
                "title": "Not Found",
                "status": 404,
                "detail": "The requested resource was not found.",
                "correlationId": "corr-404",
                "reasonCode": "query_projection_missing"
            }
            """;
        using HttpClient httpClient = CreateClient(_ =>
            Task.FromResult(Json(HttpStatusCode.NotFound, problemJson, "application/problem+json")));
        var client = new EventStoreGatewayClient(httpClient, Options.Create(new EventStoreGatewayClientOptions()));

        EventStoreGatewayException ex = await Should.ThrowAsync<EventStoreGatewayException>(
            () => client.SubmitQueryAsync(CreateQueryRequest()));

        ex.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        ex.ReasonCode.ShouldBe("query_projection_missing");
        ex.CorrelationId.ShouldBe("corr-404");
    }

    private static SubmitQueryRequest CreateQueryRequest()
        => new(
            Tenant: "tenant-a",
            Domain: "party",
            AggregateId: "party-1",
            QueryType: "GetParty",
            EntityId: "party-1");

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => CreateClient((request, _) => handler(request));

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(new RecordingHandler(handler)) { BaseAddress = new Uri("https://eventstore.local/") };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json, string contentType = "application/json")
        => new(statusCode) {
            Content = new StringContent(json, Encoding.UTF8, contentType),
        };

    private sealed record CounterDto(int Count);

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
