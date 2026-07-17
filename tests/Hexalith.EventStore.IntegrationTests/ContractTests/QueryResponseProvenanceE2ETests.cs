using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Aspire.Hosting.Testing;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 proof that live handler and projection routes expose only route-authoritative query evidence.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public sealed class QueryResponseProvenanceE2ETests(AspireContractTestFixture fixture)
{
    private const string HandlerQueryTypesStateKey = "admin:query-types:tenants";
    private const string RedisEndpoint = "localhost:6379";
    private static readonly TimeSpan s_projectionPollTimeout = TimeSpan.FromSeconds(45);

    [Fact]
    public async Task LiveHandlerRoute_WithCurrentProjectionValidator_NeutralizesProjectionEvidence()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        CancellationToken cancellationToken = cancellation.Token;
        await fixture.RestartEventStoreWithClearedHandlerQueryTypesStateAsync(cancellationToken).ConfigureAwait(true);
        _ = await fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("tenants", cancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(2), cancellationToken)
            .ConfigureAwait(true);

        string aggregateId = $"provenance-{Guid.NewGuid():N}";
        string correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdWithRetryAsync(
            fixture.EventStoreClient,
            "tenant-a",
            "counter",
            aggregateId,
            "IncrementCounter").ConfigureAwait(true);
        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            fixture.EventStoreClient,
            correlationId,
            "tenant-a").ConfigureAwait(true);
        status.GetProperty("status").GetString().ShouldBe("Completed");
        status.GetProperty("eventCount").GetInt32().ShouldBeGreaterThan(0);

        string currentProjectionETag = await PollForCurrentProjectionETagAsync(
            aggregateId,
            cancellationToken).ConfigureAwait(true);

        await AssertProjectionNotModifiedRoundTripAsync(
            aggregateId,
            currentProjectionETag,
            cancellationToken).ConfigureAwait(true);

        using HttpRequestMessage request = CreateHandlerRequest(currentProjectionETag);
        using HttpResponseMessage response = await fixture.EventStoreClient
            .SendAsync(request, cancellationToken).ConfigureAwait(true);
        string responseBody = await response.Content
            .ReadAsStringAsync(cancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"Expected live list-tenants handler route to execute despite matching projection validator. Body: {responseBody}");
        response.Headers.GetValues("X-Hexalith-Query-Provenance").Single().ShouldBe("HandlerComputed");
        response.Headers.ETag.ShouldBeNull();
        response.Headers.Contains("X-Hexalith-Projection-Version").ShouldBeFalse();
        response.Headers.Contains("X-Hexalith-Is-Stale").ShouldBeFalse();

        using JsonDocument document = JsonDocument.Parse(responseBody);
        JsonElement metadata = document.RootElement.GetProperty("metadata");
        metadata.GetProperty("provenance").GetString().ShouldBe("HandlerComputed");
        ShouldBeMissingOrNull(metadata, "etag");
        ShouldBeMissingOrNull(metadata, "projectionVersion");
        ShouldBeMissingOrNull(metadata, "isStale");

        await AssertHandlerQueryTypePersistedAsync(cancellationToken).ConfigureAwait(true);
    }

    private static async Task AssertHandlerQueryTypePersistedAsync(CancellationToken cancellationToken)
    {
        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        }).ConfigureAwait(false);

        RedisValue payload = await redis.GetDatabase()
            .HashGetAsync(HandlerQueryTypesStateKey, "data")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        payload.HasValue.ShouldBeTrue($"Expected persisted DAPR state at {HandlerQueryTypesStateKey}.");

        using JsonDocument document = JsonDocument.Parse(payload.ToString());
        document.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        document.RootElement
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ShouldContain("list-tenants");

        await redis.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(false);
    }

    private async Task AssertProjectionNotModifiedRoundTripAsync(
        string aggregateId,
        string currentProjectionETag,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage rawRequest = CreateProjectionRequest(aggregateId);
        _ = rawRequest.Headers.TryAddWithoutValidation("If-None-Match", currentProjectionETag);
        using HttpResponseMessage rawResponse = await fixture.EventStoreClient
            .SendAsync(rawRequest, cancellationToken).ConfigureAwait(false);

        rawResponse.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        rawResponse.Headers.ETag.ShouldNotBeNull();
        rawResponse.Headers.ETag.IsWeak.ShouldBeFalse();
        rawResponse.Headers.ETag.ToString().ShouldBe(currentProjectionETag);
        rawResponse.Headers.GetValues("X-Hexalith-Query-Provenance").Single()
            .ShouldBe("ProjectionBacked");

        using HttpClient gatewayHttpClient = fixture.App.CreateHttpClient("eventstore");
        gatewayHttpClient.Timeout = TimeSpan.FromSeconds(60);
        gatewayHttpClient.DefaultRequestHeaders.Authorization = CreateAuthorization(
            "test-user",
            "tenant-a",
            ["counter"]);
        var gatewayClient = new EventStoreGatewayClient(
            gatewayHttpClient,
            Options.Create(new EventStoreGatewayClientOptions()));
        var request = new SubmitQueryRequest(
            Tenant: "tenant-a",
            Domain: "counter",
            AggregateId: aggregateId,
            QueryType: "get-counter-status",
            ProjectionType: "counter",
            Payload: JsonSerializer.SerializeToElement(new { id = aggregateId }),
            EntityId: aggregateId);

        EventStoreQueryResult clientResult = await gatewayClient
            .SubmitQueryAsync(request, currentProjectionETag, cancellationToken)
            .ConfigureAwait(false);

        clientResult.IsNotModified.ShouldBeTrue();
        clientResult.ETag.ShouldBe(currentProjectionETag.Trim('"'));
        _ = clientResult.Metadata.ShouldNotBeNull();
        clientResult.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        clientResult.Metadata.IsNotModified.ShouldBe(true);
    }

    private async Task<string> PollForCurrentProjectionETagAsync(
        string aggregateId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_projectionPollTimeout);
        string lastBody = string.Empty;
        HttpStatusCode lastStatus = 0;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using HttpRequestMessage request = CreateProjectionRequest(aggregateId);
            using HttpResponseMessage response = await fixture.EventStoreClient
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            lastStatus = response.StatusCode;
            lastBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.OK
                && response.Headers.ETag is { IsWeak: false } entityTag)
            {
                response.Headers.GetValues("X-Hexalith-Query-Provenance").Single().ShouldBe("ProjectionBacked");
                using JsonDocument document = JsonDocument.Parse(lastBody);
                document.RootElement.GetProperty("metadata").GetProperty("provenance").GetString()
                    .ShouldBe("ProjectionBacked");
                return entityTag.ToString();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        throw new ShouldAssertException(
            $"Projection did not expose a current strong ETag within {s_projectionPollTimeout}. "
            + $"LastStatus={(int)lastStatus} {lastStatus}; Body: {lastBody}");
    }

    private static HttpRequestMessage CreateProjectionRequest(string aggregateId)
    {
        var body = new
        {
            tenant = "tenant-a",
            domain = "counter",
            aggregateId,
            projectionType = "counter",
            queryType = "get-counter-status",
            entityId = aggregateId,
            payload = new { id = aggregateId },
        };
        return CreateAuthorizedRequest(body, "tenant-a", ["counter"]);
    }

    private static HttpRequestMessage CreateHandlerRequest(string ifNoneMatch)
    {
        var body = new
        {
            tenant = "tenant-a",
            domain = "tenants",
            aggregateId = "index",
            projectionType = "counter",
            queryType = "list-tenants",
            entityId = "index",
        };
        HttpRequestMessage request = CreateAuthorizedRequest(
            body,
            "tenant-a",
            ["tenants", "counter"],
            role: "global-admin");
        _ = request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        return request;
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        object body,
        string tenant,
        string[] domains,
        string? role = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = CreateAuthorization(
            role is null ? "test-user" : "admin-user",
            tenant,
            domains,
            role);
        return request;
    }

    private static AuthenticationHeaderValue CreateAuthorization(
        string subject,
        string tenant,
        string[] domains,
        string? role = null)
        => new(
            "Bearer",
            TestJwtTokenGenerator.GenerateToken(
                subject: subject,
                tenants: [tenant],
                domains: domains,
                permissions: ["query:read"],
                role: role));

    private static void ShouldBeMissingOrNull(JsonElement metadata, string propertyName)
    {
        if (metadata.TryGetProperty(propertyName, out JsonElement value))
        {
            value.ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }
}
