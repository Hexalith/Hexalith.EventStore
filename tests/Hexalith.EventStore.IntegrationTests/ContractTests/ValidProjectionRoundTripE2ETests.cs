using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 proof that a valid sample projection response reaches the query endpoint through
/// the public EventStore command and query surfaces.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class ValidProjectionRoundTripE2ETests {
    private static readonly TimeSpan s_projectionPollTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan s_projectionPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly AspireContractTestFixture _fixture;

    public ValidProjectionRoundTripE2ETests(AspireContractTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IncrementCounter_ProjectsToQueryEndpoint_WithExpectedCountAndETag() {
        const string tenant = "tenant-a";
        const string domain = "counter";
        const string projectionType = "counter";
        const string queryType = "get-counter-status";
        string aggregateId = $"valid-projection-{Guid.NewGuid():N}";

        string correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdWithRetryAsync(
            _fixture.EventStoreClient,
            tenant,
            domain,
            aggregateId,
            "IncrementCounter");

        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            _fixture.EventStoreClient,
            correlationId,
            tenant);

        status.GetProperty("status").GetString().ShouldBe(
            "Completed",
            $"Expected command to complete before querying projection. Final status JSON: {status}");
        AssertPersistedEventEvidence(status);

        QueryPollResult projected = await PollUntilProjectedCountAsync(
            tenant,
            domain,
            aggregateId,
            projectionType,
            queryType,
            expectedCount: 1);

        projected.ETag.ShouldNotBeNullOrWhiteSpace(
            $"Successful projection query should include ETag. Response body: {projected.Body}");

        using HttpRequestMessage cachedRequest = CreateProjectionQueryRequest(
            tenant,
            domain,
            aggregateId,
            projectionType,
            queryType,
            projected.ETag);

        using HttpResponseMessage cachedResponse = await _fixture.EventStoreClient
            .SendAsync(cachedRequest);

        if (cachedResponse.StatusCode == HttpStatusCode.NotModified) {
            return;
        }

        string cachedBody = await cachedResponse.Content.ReadAsStringAsync();
        cachedResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"Expected cached query to return 304 or 200 with unchanged count. Body: {cachedBody}");

        int cachedCount = ParseCountFromQueryBody(cachedBody);
        cachedCount.ShouldBe(
            1,
            $"Expected cached-query fallback response to preserve projected count. Body: {cachedBody}");
    }

    private async Task<QueryPollResult> PollUntilProjectedCountAsync(
        string tenant,
        string domain,
        string aggregateId,
        string projectionType,
        string queryType,
        int expectedCount) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_projectionPollTimeout);
        QueryPollResult lastResult = new(
            StatusCode: 0,
            Body: string.Empty,
            ParsedCount: null,
            ParseError: "Projection query was not sent.",
            ETag: null);

        while (DateTimeOffset.UtcNow < deadline) {
            using HttpRequestMessage request = CreateProjectionQueryRequest(
                tenant,
                domain,
                aggregateId,
                projectionType,
                queryType);

            using HttpResponseMessage response = await _fixture.EventStoreClient
                .SendAsync(request).ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            int? parsedCount = null;
            string? parseError = null;

            if (response.StatusCode == HttpStatusCode.OK) {
                try {
                    parsedCount = ParseCountFromQueryBody(body);
                }
                catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException or KeyNotFoundException) {
                    parseError = ex.Message;
                }
            }

            lastResult = new QueryPollResult(
                response.StatusCode,
                body,
                parsedCount,
                parseError,
                response.Headers.ETag?.ToString());

            if (response.StatusCode == HttpStatusCode.OK && parsedCount == expectedCount) {
                return lastResult;
            }

            await Task.Delay(s_projectionPollInterval).ConfigureAwait(false);
        }

        throw new ShouldAssertException(
            $"Projection query did not return expected count {expectedCount} within {s_projectionPollTimeout}. "
            + $"Tenant={tenant}, Domain={domain}, AggregateId={aggregateId}, ProjectionType={projectionType}, QueryType={queryType}. "
            + $"LastStatus={(int)lastResult.StatusCode} {lastResult.StatusCode}; ParsedCount={lastResult.ParsedCount?.ToString() ?? "<null>"}; "
            + $"ParseError={lastResult.ParseError ?? "<none>"}; ETag={lastResult.ETag ?? "<none>"}; Body:{Environment.NewLine}{lastResult.Body}");
    }

    private static void AssertPersistedEventEvidence(JsonElement status) {
        status.TryGetProperty("eventCount", out JsonElement eventCount).ShouldBeTrue(
            $"Completed command status should include eventCount. Status JSON: {status}");

        eventCount.ValueKind.ShouldNotBe(JsonValueKind.Null, $"eventCount should not be null. Status JSON: {status}");
        eventCount.GetInt32().ShouldBeGreaterThan(
            0,
            $"eventCount should prove at least one event was persisted. Status JSON: {status}");
    }

    private static HttpRequestMessage CreateProjectionQueryRequest(
        string tenant,
        string domain,
        string aggregateId,
        string projectionType,
        string queryType,
        string? ifNoneMatch = null) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [tenant],
            domains: [domain],
            permissions: ["query:read"]);

        var body = new {
            tenant,
            domain,
            aggregateId,
            projectionType,
            queryType,
            entityId = aggregateId,
            payload = new { id = aggregateId },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrWhiteSpace(ifNoneMatch)) {
            _ = request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        return request;
    }

    private static int ParseCountFromQueryBody(string body) {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("payload", out JsonElement payload)) {
            throw new KeyNotFoundException("Response body does not contain a payload property.");
        }

        return ParseCountFromPayload(payload);
    }

    private static int ParseCountFromPayload(JsonElement payload) {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("count", out JsonElement directCount)) {
            return directCount.GetInt32();
        }

        if (payload.ValueKind == JsonValueKind.String) {
            string? encoded = payload.GetString();
            if (string.IsNullOrWhiteSpace(encoded)) {
                throw new FormatException("Payload is an empty base64 string.");
            }

            byte[] bytes = Convert.FromBase64String(encoded);
            using JsonDocument inner = JsonDocument.Parse(bytes);
            if (inner.RootElement.TryGetProperty("count", out JsonElement encodedCount)) {
                return encodedCount.GetInt32();
            }

            throw new KeyNotFoundException("Decoded payload does not contain a count property.");
        }

        throw new InvalidOperationException($"Unsupported payload JSON kind: {payload.ValueKind}.");
    }

    private sealed record QueryPollResult(
        HttpStatusCode StatusCode,
        string Body,
        int? ParsedCount,
        string? ParseError,
        string? ETag);
}
