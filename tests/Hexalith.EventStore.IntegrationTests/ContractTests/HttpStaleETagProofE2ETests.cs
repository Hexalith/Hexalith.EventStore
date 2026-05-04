using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 proof that the public HTTP query surface returns 304 for a current
/// self-routing ETag and re-queries when that validator becomes stale.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public sealed partial class HttpStaleETagProofE2ETests {
    private static readonly TimeSpan s_projectionPollTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan s_projectionPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan s_statusPollTimeout = TimeSpan.FromSeconds(90);
    private const int MaxHistoryEntries = 200;
    private const int BodySnipMax = 800;

    private readonly AspireContractTestFixture _fixture;

    public HttpStaleETagProofE2ETests(AspireContractTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IncrementCounter_CurrentETagReturns304_StaleETagRequeriesAfterProjectionChange() {
        const string tenant = "tenant-a";
        const string domain = "counter";
        const string projectionType = "counter";
        const string queryType = "get-counter-status";
        string aggregateId = $"stale-etag-proof-{Guid.NewGuid():N}";
        QueryIdentity query = QueryIdentity.Create(tenant, domain, aggregateId, projectionType, queryType);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        CancellationToken ct = cts.Token;
        var history = new List<QueryObservation>();

        string firstCorrelationId = await SubmitCompletedIncrementAsync(tenant, domain, aggregateId, ct);

        QueryPollResult baseline = await PollUntilProjectedCountAsync(
            query,
            expectedCount: 1,
            originalETag: null,
            firstCorrelationId,
            secondCorrelationId: null,
            history,
            ct);

        baseline.ETag.ShouldNotBeNullOrWhiteSpace(
            FailureContext(
                query,
                firstCorrelationId,
                secondCorrelationId: null,
                expectedCount: 1,
                originalETag: null,
                lastResult: baseline,
                history,
                elapsed: null));
        string baselineETag = baseline.ETag!;
        AssertStrongSelfRoutingETag(baselineETag, projectionType, query);

        using HttpRequestMessage currentRequest = CreateProjectionQueryRequest(query, baselineETag);
        using HttpResponseMessage currentResponse = await _fixture.EventStoreClient
            .SendAsync(currentRequest, ct);

        string currentBody = await currentResponse.Content.ReadAsStringAsync(ct);
        currentResponse.StatusCode.ShouldBe(
            HttpStatusCode.NotModified,
            FailureContext(
                query,
                firstCorrelationId,
                secondCorrelationId: null,
                expectedCount: 1,
                originalETag: baselineETag,
                lastResult: QueryObservation.FromResponse(currentResponse, currentBody),
                history,
                elapsed: null));

        string secondCorrelationId = await SubmitCompletedIncrementAsync(tenant, domain, aggregateId, ct);

        QueryPollResult staleValidatorResult = await PollUntilProjectedCountAsync(
            query,
            expectedCount: 2,
            originalETag: baselineETag,
            firstCorrelationId,
            secondCorrelationId,
            history,
            ct);

        staleValidatorResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        staleValidatorResult.ParsedCount.ShouldBe(2);
        staleValidatorResult.ETag.ShouldNotBe(
            baselineETag,
            FailureContext(
                query,
                firstCorrelationId,
                secondCorrelationId,
                expectedCount: 2,
                originalETag: baselineETag,
                lastResult: staleValidatorResult,
                history,
                elapsed: null));
        AssertStrongSelfRoutingETag(staleValidatorResult.ETag!, projectionType, query);
    }

    private async Task<string> SubmitCompletedIncrementAsync(
        string tenant,
        string domain,
        string aggregateId,
        CancellationToken cancellationToken) {
        string correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdWithRetryAsync(
            _fixture.EventStoreClient,
            tenant,
            domain,
            aggregateId,
            "IncrementCounter").ConfigureAwait(false);

        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            _fixture.EventStoreClient,
            correlationId,
            tenant,
            timeout: s_statusPollTimeout).ConfigureAwait(false);

        status.GetProperty("status").GetString().ShouldBe(
            "Completed",
            $"Expected IncrementCounter command {correlationId} to complete before querying projection. Final status JSON: {status}");
        AssertPersistedEventEvidence(status, correlationId);

        cancellationToken.ThrowIfCancellationRequested();
        return correlationId;
    }

    private async Task<QueryPollResult> PollUntilProjectedCountAsync(
        QueryIdentity query,
        int expectedCount,
        string? originalETag,
        string firstCorrelationId,
        string? secondCorrelationId,
        List<QueryObservation> history,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();
        QueryPollResult lastResult = new(
            StatusCode: 0,
            Body: string.Empty,
            ParsedCount: null,
            ParseError: "Projection query was not sent.",
            ETag: null);

        while (stopwatch.Elapsed < s_projectionPollTimeout) {
            cancellationToken.ThrowIfCancellationRequested();

            using HttpRequestMessage request = CreateProjectionQueryRequest(query, originalETag);
            using HttpResponseMessage response = await _fixture.EventStoreClient
                .SendAsync(request, cancellationToken).ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            QueryObservation observation = QueryObservation.FromResponse(response, body);
            AppendHistory(history, observation);

            lastResult = new QueryPollResult(
                response.StatusCode,
                body,
                observation.ParsedCount,
                observation.ParseError,
                observation.ETag);

            if (response.StatusCode == HttpStatusCode.OK
                && observation.ParsedCount == expectedCount
                && !string.IsNullOrWhiteSpace(observation.ETag)) {
                return lastResult;
            }

            if (IsFailFast(response.StatusCode)) {
                throw new ShouldAssertException(
                    FailureContext(
                        query,
                        firstCorrelationId,
                        secondCorrelationId,
                        expectedCount,
                        originalETag,
                        lastResult,
                        history,
                        stopwatch.Elapsed));
            }

            await Task.Delay(s_projectionPollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new ShouldAssertException(
            FailureContext(
                query,
                firstCorrelationId,
                secondCorrelationId,
                expectedCount,
                originalETag,
                lastResult,
                history,
                stopwatch.Elapsed));
    }

    private static bool IsFailFast(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.BadRequest
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.UnsupportedMediaType
            or HttpStatusCode.NotImplemented
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.HttpVersionNotSupported;

    private static HttpRequestMessage CreateProjectionQueryRequest(QueryIdentity query, string? ifNoneMatch = null) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [query.Tenant],
            domains: [query.Domain],
            permissions: ["query:read"]);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
            Content = new StringContent(query.SerializedBody, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrWhiteSpace(ifNoneMatch)) {
            _ = request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        return request;
    }

    private static void AssertPersistedEventEvidence(JsonElement status, string correlationId) {
        status.TryGetProperty("eventCount", out JsonElement eventCount).ShouldBeTrue(
            $"Completed command status {correlationId} should include eventCount. Status JSON: {status}");

        eventCount.ValueKind.ShouldBe(
            JsonValueKind.Number,
            $"eventCount for command {correlationId} should be a JSON number. ValueKind={eventCount.ValueKind}. Status JSON: {status}");
        eventCount.TryGetInt32(out int parsed).ShouldBeTrue(
            $"eventCount for command {correlationId} should fit in Int32. Raw: {eventCount}. Status JSON: {status}");
        parsed.ShouldBeGreaterThan(
            0,
            $"eventCount should prove at least one event was persisted for command {correlationId}. Status JSON: {status}");
    }

    private static void AssertStrongSelfRoutingETag(string etag, string expectedProjectionType, QueryIdentity query) {
        if (etag.Length < 3
            || etag.StartsWith("W/", StringComparison.Ordinal)
            || !etag.StartsWith('"')
            || !etag.EndsWith('"')) {
            throw new ShouldAssertException(
                $"Expected a strong quoted ETag (not weak/unquoted/empty) for {query.DiagnosticIdentity}. Actual: {etag}");
        }

        string inner = etag[1..^1];
        inner.ShouldNotBeNullOrWhiteSpace($"Expected non-empty ETag value for {query.DiagnosticIdentity}.");

        string[] parts = inner.Split('.');
        parts.Length.ShouldBe(2, $"Expected self-routing ETag shape '<base64url projection>.<base64url guid>' for {query.DiagnosticIdentity}.");
        Base64UrlRegex().IsMatch(parts[0]).ShouldBeTrue(
            $"Expected ETag projection prefix to be base64url for {query.DiagnosticIdentity}. Actual: {etag}");
        Base64UrlGuidRegex().IsMatch(parts[1]).ShouldBeTrue(
            $"Expected ETag GUID suffix to be 22-character base64url GUID for {query.DiagnosticIdentity}. Actual: {etag}");

        TryDecodeBase64Url(parts[0]).ShouldBe(
            expectedProjectionType,
            $"Expected ETag prefix to decode to projection type '{expectedProjectionType}' for {query.DiagnosticIdentity}. Actual: {etag}");
        TryDecodeBase64UrlBytes(parts[1]).Length.ShouldBe(
            16,
            $"Expected ETag GUID suffix to decode to 16 bytes for {query.DiagnosticIdentity}. Actual: {etag}");
    }

    private static string TryDecodeBase64Url(string value) {
        try {
            return Encoding.UTF8.GetString(DecodeBase64UrlToBytes(value));
        }
        catch (FormatException ex) {
            throw new ShouldAssertException($"ETag prefix '{value}' could not be decoded as base64url: {ex.Message}");
        }
    }

    private static byte[] TryDecodeBase64UrlBytes(string value) {
        try {
            return DecodeBase64UrlToBytes(value);
        }
        catch (FormatException ex) {
            throw new ShouldAssertException($"ETag GUID suffix '{value}' could not be decoded as base64url: {ex.Message}");
        }
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

            byte[] bytes = DecodeBase64UrlToBytes(encoded);
            using JsonDocument inner = JsonDocument.Parse(bytes);
            if (inner.RootElement.TryGetProperty("count", out JsonElement encodedCount)) {
                return encodedCount.GetInt32();
            }

            throw new KeyNotFoundException("Decoded payload does not contain a count property.");
        }

        throw new InvalidOperationException($"Unsupported payload JSON kind: {payload.ValueKind}.");
    }

    private static string FailureContext(
        QueryIdentity query,
        string firstCorrelationId,
        string? secondCorrelationId,
        int expectedCount,
        string? originalETag,
        QueryObservation lastResult,
        IReadOnlyCollection<QueryObservation> history,
        TimeSpan? elapsed)
        => FailureContext(
            query,
            firstCorrelationId,
            secondCorrelationId,
            expectedCount,
            originalETag,
            lastResult.StatusCode,
            lastResult.BodySnippet,
            lastResult.ParsedCount,
            lastResult.ParseError,
            lastResult.ETag,
            history,
            elapsed);

    private static string FailureContext(
        QueryIdentity query,
        string firstCorrelationId,
        string? secondCorrelationId,
        int expectedCount,
        string? originalETag,
        QueryPollResult lastResult,
        IReadOnlyCollection<QueryObservation> history,
        TimeSpan? elapsed)
        => FailureContext(
            query,
            firstCorrelationId,
            secondCorrelationId,
            expectedCount,
            originalETag,
            lastResult.StatusCode,
            Snip(lastResult.Body),
            lastResult.ParsedCount,
            lastResult.ParseError,
            lastResult.ETag,
            history,
            elapsed);

    private static string FailureContext(
        QueryIdentity query,
        string firstCorrelationId,
        string? secondCorrelationId,
        int expectedCount,
        string? originalETag,
        HttpStatusCode lastStatus,
        string lastBodySnippet,
        int? parsedCount,
        string? parseError,
        string? lastETag,
        IReadOnlyCollection<QueryObservation> history,
        TimeSpan? elapsed) {
        string observedHistory = history.Count == 0
            ? "<none>"
            : string.Join(
                " | ",
                history.Select(h => $"{(int)h.StatusCode}:{h.StatusCode};count={h.ParsedCount?.ToString() ?? "<null>"};etag={h.ETag ?? "<none>"};parse={h.ParseError ?? "<none>"}"));

        return "HTTP stale ETag proof failed. "
            + $"Tenant={query.Tenant}; Domain={query.Domain}; AggregateId={query.AggregateId}; "
            + $"ProjectionType={query.ProjectionType}; EntityId={query.EntityId}; QueryType={query.QueryType}; "
            + $"QueryPayloadSha256={query.PayloadFingerprint}; SerializedQuery={query.SerializedBody}; "
            + $"FirstCommandCorrelationId={firstCorrelationId}; SecondCommandCorrelationId={secondCorrelationId ?? "<none>"}; "
            + $"ExpectedCount={expectedCount}; LastStatus={(int)lastStatus} {lastStatus}; "
            + $"LastBodySnippet={lastBodySnippet}; ParsedCount={parsedCount?.ToString() ?? "<null>"}; "
            + $"ParseError={parseError ?? "<none>"}; OriginalETag={originalETag ?? "<none>"}; LastObservedETag={lastETag ?? "<none>"}; "
            + $"ObservedStatusCountETagHistory={observedHistory}; Elapsed={elapsed?.ToString() ?? "<not measured>"}; "
            + "SameTenantProjectionConcurrency=AspireContractTests collection serializes this same-tenant counter proof; "
            + "unrelated same-tenant counter updates between baseline and current revalidation should be treated as an environment blocker.";
    }

    private static string Snip(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return "<empty>";
        }

        return value.Length <= BodySnipMax
            ? value
            : $"{value[..BodySnipMax]}…[truncated, full length={value.Length}]";
    }

    private static void AppendHistory(List<QueryObservation> history, QueryObservation observation) {
        if (history.Count >= MaxHistoryEntries) {
            history.RemoveAt(0);
        }

        history.Add(observation);
    }

    private static byte[] DecodeBase64UrlToBytes(string value) {
        string padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4) {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            case 1:
                throw new FormatException("Invalid base64url length.");
        }

        return Convert.FromBase64String(padded);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex Base64UrlRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{22}$")]
    private static partial Regex Base64UrlGuidRegex();

    private sealed record QueryIdentity(
        string Tenant,
        string Domain,
        string AggregateId,
        string ProjectionType,
        string EntityId,
        string QueryType,
        string SerializedBody,
        string PayloadFingerprint) {
        public string DiagnosticIdentity
            => $"Tenant={Tenant}; Domain={Domain}; AggregateId={AggregateId}; ProjectionType={ProjectionType}; EntityId={EntityId}; QueryType={QueryType}; QueryPayloadSha256={PayloadFingerprint}";

        public static QueryIdentity Create(
            string tenant,
            string domain,
            string aggregateId,
            string projectionType,
            string queryType) {
            var body = new {
                tenant,
                domain,
                aggregateId,
                projectionType,
                queryType,
                entityId = aggregateId,
                payload = new { id = aggregateId },
            };

            string serializedBody = JsonSerializer.Serialize(body);
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serializedBody)));

            return new QueryIdentity(
                tenant,
                domain,
                aggregateId,
                projectionType,
                aggregateId,
                queryType,
                serializedBody,
                fingerprint);
        }
    }

    private sealed record QueryObservation(
        HttpStatusCode StatusCode,
        string BodySnippet,
        int? ParsedCount,
        string? ParseError,
        string? ETag) {
        public static QueryObservation FromResponse(HttpResponseMessage response, string body) {
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

            return new QueryObservation(
                response.StatusCode,
                Snip(body),
                parsedCount,
                parseError,
                response.Headers.ETag?.ToString());
        }
    }

    private sealed record QueryPollResult(
        HttpStatusCode StatusCode,
        string Body,
        int? ParsedCount,
        string? ParseError,
        string? ETag);
}
