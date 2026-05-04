using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 proof that the public query topology converges from cold query, through
/// current-validator 304, projection invalidation, and re-warmed validator 304.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public sealed partial class QueryCacheTopologyProofE2ETests {
    private static readonly TimeSpan s_projectionPollTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan s_projectionPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan s_statusPollTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan s_commandSubmitRetryTimeout = TimeSpan.FromSeconds(60);

    private readonly AspireContractTestFixture _fixture;

    public QueryCacheTopologyProofE2ETests(AspireContractTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryCacheTopology_WhenProjectionChanges_InvalidatesOldETagAndCachesNewETag() {
        const string tenant = "tenant-a";
        const string domain = "counter";
        const string projectionType = "counter";
        const string queryType = "get-counter-status";
        string aggregateId = $"query-cache-topology-{Guid.NewGuid():N}";
        QueryIdentity query = QueryIdentity.Create(tenant, domain, aggregateId, projectionType, queryType);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        CancellationToken ct = cts.Token;
        var history = new List<QueryObservation>();

        string firstCorrelationId = await SubmitCompletedIncrementAsync(query, ct);

        QueryPollResult coldQuery = await PollUntilProjectedCountAsync(
            query,
            expectedCount: 1,
            ifNoneMatch: null,
            firstCorrelationId,
            secondCorrelationId: null,
            baselineETag: null,
            history,
            phase: "cold-query",
            ct);

        string baselineETag = coldQuery.ETag!;
        AssertStrongSelfRoutingETag(baselineETag, projectionType, query);

        QueryObservation warmBaseline = await SendSingleQueryAsync(query, baselineETag, ct);
        warmBaseline.StatusCode.ShouldBe(
            HttpStatusCode.NotModified,
            FailureContext(
                query,
                firstCorrelationId,
                secondCorrelationId: null,
                expectedCount: 1,
                phase: "warm-baseline-current-validator",
                originalETag: baselineETag,
                lastResult: warmBaseline,
                history,
                elapsed: null));

        string secondCorrelationId = await SubmitCompletedIncrementAsync(query, ct);

        QueryPollResult changedProjection = await PollUntilProjectedCountAsync(
            query,
            expectedCount: 2,
            ifNoneMatch: baselineETag,
            firstCorrelationId,
            secondCorrelationId,
            baselineETag,
            history,
            phase: "post-change-stale-validator",
            ct);

        string changedETag = changedProjection.ETag!;
        changedETag.ShouldNotBe(
            baselineETag,
            FailureContext(
                query,
                firstCorrelationId,
                secondCorrelationId,
                expectedCount: 2,
                phase: "post-change-stale-validator",
                originalETag: baselineETag,
                lastResult: changedProjection,
                history,
                elapsed: null));
        AssertStrongSelfRoutingETag(changedETag, projectionType, query);

        QueryObservation reWarmed = await SendSingleQueryAsync(query, changedETag, ct);
        reWarmed.StatusCode.ShouldBe(
            HttpStatusCode.NotModified,
            FailureContext(
                query,
                firstCorrelationId,
                secondCorrelationId,
                expectedCount: 2,
                phase: "rewarm-current-validator",
                originalETag: changedETag,
                lastResult: reWarmed,
                history,
                elapsed: null));
    }

    private async Task<string> SubmitCompletedIncrementAsync(QueryIdentity query, CancellationToken cancellationToken) {
        string correlationId = await SubmitIncrementAndGetCorrelationIdWithRetryAsync(query, cancellationToken).ConfigureAwait(false);

        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            _fixture.EventStoreClient,
            correlationId,
            query.Tenant,
            timeout: s_statusPollTimeout).ConfigureAwait(false);

        status.GetProperty("status").GetString().ShouldBe(
            "Completed",
            $"Expected IncrementCounter command {correlationId} to complete before topology proof queries. Final status JSON: {status}");
        AssertPersistedEventEvidence(status, correlationId);

        cancellationToken.ThrowIfCancellationRequested();
        return correlationId;
    }

    private async Task<string> SubmitIncrementAndGetCorrelationIdWithRetryAsync(
        QueryIdentity query,
        CancellationToken cancellationToken) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_commandSubmitRetryTimeout);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();

            try {
                return await SubmitIncrementAndGetCorrelationIdAsync(query, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                lastError = ex;
                await Task.Delay(ContractTestHelpers.DefaultPollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            $"Unable to submit IncrementCounter command within {s_commandSubmitRetryTimeout}. {query.DiagnosticIdentity}",
            lastError);
    }

    private async Task<string> SubmitIncrementAndGetCorrelationIdAsync(
        QueryIdentity query,
        CancellationToken cancellationToken) {
        using HttpRequestMessage request = CreateIncrementCommandRequest(query);
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Accepted) {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ShouldAssertException(
                $"Expected IncrementCounter submit to return 202 Accepted for {query.DiagnosticIdentity} "
                + $"but was {(int)response.StatusCode} {response.StatusCode}. Body:{Environment.NewLine}{body}");
        }

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        return result.GetProperty("correlationId").GetString()!;
    }

    private async Task<QueryPollResult> PollUntilProjectedCountAsync(
        QueryIdentity query,
        int expectedCount,
        string? ifNoneMatch,
        string firstCorrelationId,
        string? secondCorrelationId,
        string? baselineETag,
        List<QueryObservation> history,
        string phase,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_projectionPollTimeout);
        QueryPollResult lastResult = new(
            StatusCode: 0,
            Body: string.Empty,
            ParsedCount: null,
            ParseError: "Projection query was not sent.",
            ETag: null,
            TraceHeaders: "<none>");

        while (DateTimeOffset.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();

            QueryObservation observation = await SendSingleQueryAsync(query, ifNoneMatch, cancellationToken).ConfigureAwait(false);
            history.Add(observation);

            lastResult = new QueryPollResult(
                observation.StatusCode,
                observation.BodySnippet,
                observation.ParsedCount,
                observation.ParseError,
                observation.ETag,
                observation.TraceHeaders);

            if (observation.StatusCode == HttpStatusCode.OK && observation.ParsedCount == expectedCount) {
                AssertExpectedCountETagContract(
                    query,
                    expectedCount,
                    baselineETag,
                    firstCorrelationId,
                    secondCorrelationId,
                    phase,
                    observation,
                    history,
                    stopwatch.Elapsed);

                return lastResult;
            }

            if (IsFailFast(observation.StatusCode)) {
                throw new ShouldAssertException(
                    FailureContext(
                        query,
                        firstCorrelationId,
                        secondCorrelationId,
                        expectedCount,
                        phase,
                        ifNoneMatch,
                        observation,
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
                phase,
                ifNoneMatch,
                lastResult,
                history,
                stopwatch.Elapsed));
    }

    private async Task<QueryObservation> SendSingleQueryAsync(
        QueryIdentity query,
        string? ifNoneMatch,
        CancellationToken cancellationToken) {
        using HttpRequestMessage request = CreateProjectionQueryRequest(query, ifNoneMatch);
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request, cancellationToken).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return QueryObservation.FromResponse(response, body);
    }

    private static void AssertExpectedCountETagContract(
        QueryIdentity query,
        int expectedCount,
        string? baselineETag,
        string firstCorrelationId,
        string? secondCorrelationId,
        string phase,
        QueryObservation observation,
        IReadOnlyCollection<QueryObservation> history,
        TimeSpan elapsed) {
        if (string.IsNullOrWhiteSpace(observation.ETag)) {
            throw new ShouldAssertException(
                FailureContext(
                    query,
                    firstCorrelationId,
                    secondCorrelationId,
                    expectedCount,
                    phase,
                    baselineETag,
                    observation,
                    history,
                    elapsed)
                + " Expected-count response did not include an ETag.");
        }

        AssertStrongSelfRoutingETag(observation.ETag!, query.ProjectionType, query);

        if (!string.IsNullOrWhiteSpace(baselineETag) && observation.ETag == baselineETag) {
            throw new ShouldAssertException(
                FailureContext(
                    query,
                    firstCorrelationId,
                    secondCorrelationId,
                    expectedCount,
                    phase,
                    baselineETag,
                    observation,
                    history,
                    elapsed)
                + " Projection reached the changed count but returned the original stale ETag.");
        }
    }

    private static bool IsFailFast(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.BadRequest
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static HttpRequestMessage CreateIncrementCommandRequest(QueryIdentity query) {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: [query.Tenant],
            domains: [query.Domain],
            permissions: ["command:submit", "command:query"]);

        var body = new {
            MessageId = Guid.NewGuid().ToString(),
            Tenant = query.Tenant,
            Domain = query.Domain,
            AggregateId = query.AggregateId,
            CommandType = "IncrementCounter",
            Payload = new { id = query.AggregateId },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static HttpRequestMessage CreateProjectionQueryRequest(QueryIdentity query, string? ifNoneMatch) {
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

        eventCount.ValueKind.ShouldNotBe(
            JsonValueKind.Null,
            $"eventCount for command {correlationId} should not be null. Status JSON: {status}");
        eventCount.GetInt32().ShouldBeGreaterThan(
            0,
            $"eventCount should prove at least one event was persisted for command {correlationId}. Status JSON: {status}");
    }

    private static void AssertStrongSelfRoutingETag(string etag, string expectedProjectionType, QueryIdentity query) {
        if (etag.StartsWith("W/", StringComparison.Ordinal)) {
            throw new ShouldAssertException($"Expected a strong ETag for {query.DiagnosticIdentity}. Actual: {etag}");
        }

        if (!etag.StartsWith('"') || !etag.EndsWith('"')) {
            throw new ShouldAssertException($"Expected quoted ETag for {query.DiagnosticIdentity}. Actual: {etag}");
        }

        string inner = etag[1..^1];
        inner.ShouldNotBeNullOrWhiteSpace($"Expected non-empty ETag value for {query.DiagnosticIdentity}.");

        string[] parts = inner.Split('.');
        parts.Length.ShouldBe(2, $"Expected self-routing ETag shape '<base64url projection>.<base64url guid>' for {query.DiagnosticIdentity}.");
        Base64UrlRegex().IsMatch(parts[0]).ShouldBeTrue(
            $"Expected ETag projection prefix to be base64url for {query.DiagnosticIdentity}. Actual: {etag}");
        Base64UrlGuidRegex().IsMatch(parts[1]).ShouldBeTrue(
            $"Expected ETag GUID suffix to be 22-character base64url GUID for {query.DiagnosticIdentity}. Actual: {etag}");

        DecodeBase64Url(parts[0]).ShouldBe(
            expectedProjectionType,
            $"Expected ETag prefix to decode to projection type '{expectedProjectionType}' for {query.DiagnosticIdentity}.");
        DecodeBase64UrlToBytes(parts[1]).Length.ShouldBe(
            16,
            $"Expected ETag GUID suffix to decode to 16 bytes for {query.DiagnosticIdentity}.");
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

    private static string FailureContext(
        QueryIdentity query,
        string firstCorrelationId,
        string? secondCorrelationId,
        int expectedCount,
        string phase,
        string? originalETag,
        QueryObservation lastResult,
        IReadOnlyCollection<QueryObservation> history,
        TimeSpan? elapsed)
        => FailureContext(
            query,
            firstCorrelationId,
            secondCorrelationId,
            expectedCount,
            phase,
            originalETag,
            lastResult.StatusCode,
            lastResult.BodySnippet,
            lastResult.ParsedCount,
            lastResult.ParseError,
            lastResult.ETag,
            lastResult.TraceHeaders,
            history,
            elapsed);

    private static string FailureContext(
        QueryIdentity query,
        string firstCorrelationId,
        string? secondCorrelationId,
        int expectedCount,
        string phase,
        string? originalETag,
        QueryPollResult lastResult,
        IReadOnlyCollection<QueryObservation> history,
        TimeSpan? elapsed)
        => FailureContext(
            query,
            firstCorrelationId,
            secondCorrelationId,
            expectedCount,
            phase,
            originalETag,
            lastResult.StatusCode,
            Snip(lastResult.Body),
            lastResult.ParsedCount,
            lastResult.ParseError,
            lastResult.ETag,
            lastResult.TraceHeaders,
            history,
            elapsed);

    private static string FailureContext(
        QueryIdentity query,
        string firstCorrelationId,
        string? secondCorrelationId,
        int expectedCount,
        string phase,
        string? originalETag,
        HttpStatusCode lastStatus,
        string lastBodySnippet,
        int? parsedCount,
        string? parseError,
        string? lastETag,
        string traceHeaders,
        IReadOnlyCollection<QueryObservation> history,
        TimeSpan? elapsed) {
        string observedHistory = history.Count == 0
            ? "<none>"
            : string.Join(
                " | ",
                history.Select(h => $"{(int)h.StatusCode}:{h.StatusCode};count={h.ParsedCount?.ToString() ?? "<null>"};etag={h.ETag ?? "<none>"};trace={h.TraceHeaders};parse={h.ParseError ?? "<none>"}"));

        string unexpectedETags = string.Join(
            ",",
            history.Select(h => h.ETag).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.Ordinal));
        if (string.IsNullOrWhiteSpace(unexpectedETags)) {
            unexpectedETags = "<none>";
        }

        return "Query cache topology proof failed. "
            + $"Phase={phase}; Tenant={query.Tenant}; Domain={query.Domain}; AggregateId={query.AggregateId}; "
            + $"ProjectionType={query.ProjectionType}; EntityId={query.EntityId}; QueryType={query.QueryType}; "
            + $"QueryPayloadSha256={query.PayloadFingerprint}; SerializedQuery={query.SerializedBody}; "
            + $"FirstCommandCorrelationId={firstCorrelationId}; SecondCommandCorrelationId={secondCorrelationId ?? "<none>"}; "
            + $"ExpectedCount={expectedCount}; LastStatus={(int)lastStatus} {lastStatus}; LastBodySnippet={lastBodySnippet}; "
            + $"ParsedCount={parsedCount?.ToString() ?? "<null>"}; ParseError={parseError ?? "<none>"}; "
            + $"OriginalETag={originalETag ?? "<none>"}; LastObservedETag={lastETag ?? "<none>"}; "
            + $"ObservedETags={unexpectedETags}; LastTraceHeaders={traceHeaders}; "
            + $"ObservedStatusCountETagHistory={observedHistory}; Elapsed={elapsed?.ToString() ?? "<not measured>"}; "
            + "SameTenantProjectionConcurrency=AspireContractTests collection serializes this same-tenant counter proof; "
            + "unexpected tenant-a/counter validator churn should be treated as shared-scope interference diagnostics.";
    }

    private static string Snip(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return "<empty>";
        }

        const int max = 800;
        return value.Length <= max ? value : value[..max];
    }

    private static string DecodeBase64Url(string value)
        => Encoding.UTF8.GetString(DecodeBase64UrlToBytes(value));

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
        string? ETag,
        string TraceHeaders) {
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
                response.Headers.ETag?.ToString(),
                ReadTraceHeaders(response));
        }

        private static string ReadTraceHeaders(HttpResponseMessage response) {
            string[] names = ["traceparent", "Request-Id", "X-Correlation-ID", "X-Request-ID"];
            var values = new List<string>();

            foreach (string name in names) {
                if (response.Headers.TryGetValues(name, out IEnumerable<string>? headerValues)) {
                    values.Add($"{name}={string.Join(",", headerValues)}");
                }
            }

            return values.Count == 0 ? "<none>" : string.Join(";", values);
        }
    }

    private sealed record QueryPollResult(
        HttpStatusCode StatusCode,
        string Body,
        int? ParsedCount,
        string? ParseError,
        string? ETag,
        string TraceHeaders);
}
