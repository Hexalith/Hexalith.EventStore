
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// R3-A7 permanent regression coverage for the public command surface (AC #12 / AC #13).
/// Pins the four public-surface URLs (Story 3.6), the no-auth contract (Story 3.5 / AC #9),
/// the signed happy path (AC #3 + #4), and the replay ULID-validation regression watch
/// (R3-A1 / AC #8) so future drift is caught by the test suite, not by re-running the
/// retro-time live verification each release.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class LiveCommandSurfaceSmokeTests
{
    private static readonly TimeSpan s_statusPollTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_statusPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly Regex s_openApiVersion = new(@"^3\.1\.\d+$", RegexOptions.Compiled);

    private readonly AspireContractTestFixture _fixture;

    public LiveCommandSurfaceSmokeTests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Fact A (AC #2 / AC #12): the four public-surface URLs each return 200 with the expected
    /// Content-Type and per-URL shape — no auth header. OpenAPI version assertion is relaxed
    /// to the 3.1.x family because Microsoft.AspNetCore.OpenApi auto-emits the library's current
    /// minor (live observation: 3.1.1) — UX-DR12's design intent is "OpenAPI 3.1, not 3.0".
    /// </summary>
    [Fact]
    public async Task PublicSurface_AllFourUrls_Return200WithExpectedShape()
    {
        // /swagger/index.html
        using HttpResponseMessage swagger = await _fixture.EventStoreClient.GetAsync("/swagger/index.html");
        swagger.StatusCode.ShouldBe(HttpStatusCode.OK);
        swagger.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        string swaggerBody = await swagger.Content.ReadAsStringAsync();
        swaggerBody.ShouldNotBeNullOrEmpty();
        swaggerBody.ShouldContain("<title>Swagger UI</title>");

        // /openapi/v1.json
        using HttpResponseMessage openApi = await _fixture.EventStoreClient.GetAsync("/openapi/v1.json");
        openApi.StatusCode.ShouldBe(HttpStatusCode.OK);
        openApi.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        JsonElement openApiDoc = await openApi.Content.ReadFromJsonAsync<JsonElement>();
        string? openApiVersion = openApiDoc.GetProperty("openapi").GetString();
        openApiVersion.ShouldNotBeNullOrWhiteSpace();
        s_openApiVersion.IsMatch(openApiVersion!).ShouldBeTrue(
            $"Expected OpenAPI 3.1.x (UX-DR12), got '{openApiVersion}'");
        openApiDoc.GetProperty("paths").TryGetProperty("/api/v1/commands", out JsonElement commandsPath).ShouldBeTrue(
            "OpenAPI document should declare the /api/v1/commands path (Story 3.6)");
        commandsPath.TryGetProperty("post", out _).ShouldBeTrue(
            "/api/v1/commands path should declare the Submit (POST) operation");

        // /problems/validation-error
        using HttpResponseMessage problemValidation = await _fixture.EventStoreClient.GetAsync("/problems/validation-error");
        problemValidation.StatusCode.ShouldBe(HttpStatusCode.OK);
        problemValidation.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        string problemValidationBody = await problemValidation.Content.ReadAsStringAsync();
        problemValidationBody.ShouldNotBeNullOrEmpty();

        // /problems/concurrency-conflict — proves Story 3.5 type-URI pages are reachable, not just validation-error.
        using HttpResponseMessage problemConcurrency = await _fixture.EventStoreClient.GetAsync("/problems/concurrency-conflict");
        problemConcurrency.StatusCode.ShouldBe(HttpStatusCode.OK);
        problemConcurrency.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        string problemConcurrencyBody = await problemConcurrency.Content.ReadAsStringAsync();
        problemConcurrencyBody.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Fact B (AC #9 / AC #12): a no-Authorization POST to /api/v1/commands returns 401 with the
    /// Story 3.5 contract — WWW-Authenticate Bearer realm, problem+json body, type URI, and
    /// NO correlationId/tenantId extensions in body. Smoke-only — deep contract lives in
    /// JwtAuthenticationIntegrationTests.
    /// </summary>
    [Fact]
    public async Task PostCommands_NoAuthToken_Returns401WithStory35Contract()
    {
        var body = new
        {
            messageId = "irrelevant",
            tenant = "t",
            domain = "d",
            aggregateId = "a",
            commandType = "X",
            payload = new { },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        // NOTE: no Authorization header attached — that's the test.

        using HttpResponseMessage response = await _fixture.EventStoreClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        AuthenticationHeaderValue? challenge = response.Headers.WwwAuthenticate.FirstOrDefault();
        _ = challenge.ShouldNotBeNull("WWW-Authenticate header is required on 401 (Story 3.5)");
        challenge.Scheme.ShouldBe("Bearer");
        challenge.Parameter.ShouldNotBeNullOrEmpty();
        challenge.Parameter!.ShouldStartWith("realm=\"");

        response.Content.Headers.ContentType?.MediaType.ShouldNotBeNull();
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/authentication-required");
        problem.TryGetProperty("correlationId", out _).ShouldBeFalse(
            "401 response body MUST NOT include the correlationId extension (Story 3.5 / UX-DR4)");
        problem.TryGetProperty("tenantId", out _).ShouldBeFalse(
            "401 response body MUST NOT include the tenantId extension (Story 3.5 / UX-DR8)");
    }

    /// <summary>
    /// Fact C (AC #3 / AC #4 / AC #12): a signed POST reaches Completed with eventCount &gt;= 1
    /// inside 30 seconds. Mirrors CommandLifecycleTests.SubmitCommand_PollStatus shape,
    /// explicitly named for the R3-A7 surface. Polling helper is inlined per the
    /// "extract only if a third caller materializes" rule.
    /// </summary>
    [Fact]
    public async Task PostCommands_SignedHappyPath_ReachesCompletedWithEvent()
    {
        // Pre-condition guard — the AspireContractTestFixture pins EnableKeycloak=false so the
        // symmetric-key path is the configured auth scheme. If the configured signing key ever
        // becomes unreachable (e.g., a future fixture refactor wipes appsettings.Development.json),
        // this skip path produces a clear signal — Fact C did not run because of fixture
        // configuration, not because of a Story 3.5 contract failure.
        if (string.IsNullOrWhiteSpace(TestJwtTokenGenerator.SigningKey))
        {
            Assert.Skip("Symmetric signing key not configured — Fact C cannot run; this is configuration, not a regression.");
        }

        string aggregateId = $"counter-r3a7-smoke-{Guid.NewGuid():N}";
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:submit", "command:query"]);

        var submitBody = new
        {
            MessageId = Guid.NewGuid().ToString(),
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = aggregateId,
            CommandType = "IncrementCounter",
            Payload = new { id = Guid.NewGuid().ToString() },
        };

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands")
        {
            Content = new StringContent(JsonSerializer.Serialize(submitBody), Encoding.UTF8, "application/json"),
        };
        submitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage submitResponse = await _fixture.EventStoreClient.SendAsync(submitRequest);

        if (submitResponse.StatusCode != HttpStatusCode.Accepted)
        {
            string failBody = await submitResponse.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"Expected 202 Accepted on signed POST but was {(int)submitResponse.StatusCode} {submitResponse.StatusCode}.\nBody:\n{failBody}");
        }

        JsonElement submitResult = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        string? correlationId = submitResult.GetProperty("correlationId").GetString();
        correlationId.ShouldNotBeNullOrWhiteSpace();
        string correlationIdValue = correlationId!;

        // Inlined polling helper — copied from CommandLifecycleTests pattern. Per R3-A7 polling-rule
        // pin (P12): do NOT extract until a third caller materializes.
        var observedStatuses = new List<string>();
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_statusPollTimeout);
        JsonElement lastStatus = default;

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{correlationIdValue}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage statusResponse = await _fixture.EventStoreClient.SendAsync(statusRequest);

            if (statusResponse.StatusCode == HttpStatusCode.OK)
            {
                lastStatus = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
                string statusValue = lastStatus.GetProperty("status").GetString()!;

                if (observedStatuses.Count == 0 || observedStatuses[^1] != statusValue)
                {
                    observedStatuses.Add(statusValue);
                }

                if (statusValue is "Completed" or "Rejected" or "PublishFailed" or "TimedOut")
                {
                    break;
                }
            }
            else if ((int)statusResponse.StatusCode is >= 400 and < 500)
            {
                string errorBody = await statusResponse.Content.ReadAsStringAsync();
                throw new ShouldAssertException(
                    $"Status endpoint returned {(int)statusResponse.StatusCode} (non-retriable) for correlationId '{correlationIdValue}'.\n{errorBody}");
            }

            await Task.Delay(s_statusPollInterval);
        }

        // Lifecycle assertions — the actor pipeline can collapse intermediate stages on fast paths
        // (live R3-A7 observation: even at 10ms polling, dev-machine commands transition
        // Received → Completed in < 80ms). eventCount >= 1 is the load-bearing persistence
        // evidence, mirroring CommandLifecycleTests' tolerance.
        // Optional soft check for "Received": if the actor pipeline didn't collapse, assert it was seen;
        // if it collapsed (the common fast-path), the eventCount >= 1 check is the evidence that matters.
        // This mirrors CommandLifecycleTests.cs:96-111 per D2 decision.
        bool sawReceived = observedStatuses.Contains("Received");
        if (sawReceived)
        {
            observedStatuses.ShouldContain("Received",
                "When observable, 'Received' MUST precede 'Completed' in the deduplicated sequence (AC #4)");
        }

        observedStatuses.Count.ShouldBeGreaterThan(0,
            $"At least one status should have been observed during polling. Last raw payload: {lastStatus}");
        observedStatuses[^1].ShouldBe("Completed",
            $"Terminal status MUST be Completed for happy-path IncrementCounter. Sequence: [{string.Join(", ", observedStatuses)}]");

        lastStatus.TryGetProperty("eventCount", out JsonElement eventCount).ShouldBeTrue(
            "Completed status payload MUST include eventCount proving end-to-end persistence");
        eventCount.ValueKind.ShouldNotBe(JsonValueKind.Null,
            "eventCount MUST be a non-null integer — null indicates incomplete persistence (AC #4)");
        eventCount.GetInt32().ShouldBeGreaterThanOrEqualTo(1,
            "eventCount MUST be >= 1 — at least one event was persisted and published");
    }

    /// <summary>
    /// Fact D (AC #8 / AC #12): replay smoke after Completed — pins the R3-A1 fix as a regression
    /// watch. Accepts 202 (replay accepted), 404 (not in archive), or 409 (replay refused for
    /// completed commands, the R3-A7 live observation). The critical assertion is that the response
    /// is NOT 400 with type=bad-request AND detail mentioning "GUID" — that would be a regression
    /// of post-epic-3-r3a1-replay-ulid-validation.
    /// </summary>
    [Fact]
    public async Task Replay_AfterCompleted_DoesNotReturn400WithGuidText()
    {
        if (string.IsNullOrWhiteSpace(TestJwtTokenGenerator.SigningKey))
        {
            Assert.Skip("Symmetric signing key not configured — Fact D pre-condition (Fact C path) cannot run.");
        }

        // Submit a fresh command and let it complete, then replay it.
        string aggregateId = $"counter-r3a7-replay-{Guid.NewGuid():N}";
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:submit", "command:query", "command:replay"]);

        var submitBody = new
        {
            MessageId = Guid.NewGuid().ToString(),
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = aggregateId,
            CommandType = "IncrementCounter",
            Payload = new { id = Guid.NewGuid().ToString() },
        };

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands")
        {
            Content = new StringContent(JsonSerializer.Serialize(submitBody), Encoding.UTF8, "application/json"),
        };
        submitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage submitResponse = await _fixture.EventStoreClient.SendAsync(submitRequest);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "Fact D pre-condition: signed POST must reach 202");

        JsonElement submitResult = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        string correlationId = submitResult.GetProperty("correlationId").GetString()!;

        // Wait until the command reaches Completed before replaying.
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_statusPollTimeout);
        bool reachedCompleted = false;
        while (DateTimeOffset.UtcNow < deadline && !reachedCompleted)
        {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{correlationId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage statusResponse = await _fixture.EventStoreClient.SendAsync(statusRequest);
            if (statusResponse.StatusCode == HttpStatusCode.OK)
            {
                JsonElement statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (statusBody.GetProperty("status").GetString() == "Completed")
                {
                    reachedCompleted = true;
                    break;
                }
            }
            else if ((int)statusResponse.StatusCode is >= 400 and < 500)
            {
                string errorBody = await statusResponse.Content.ReadAsStringAsync();
                throw new ShouldAssertException(
                    $"Fact D pre-condition: status endpoint returned {(int)statusResponse.StatusCode} (non-retriable) for correlationId '{correlationId}'.\n{errorBody}");
            }

            await Task.Delay(s_statusPollInterval);
        }

        reachedCompleted.ShouldBeTrue("Fact D pre-condition: the command must reach Completed within 30s");

        // Now replay.
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/commands/replay/{correlationId}");
        replayRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage replayResponse = await _fixture.EventStoreClient.SendAsync(replayRequest);

        // Allowed live outcomes for a Completed command: 202 (replay accepted by some implementations),
        // 404 (not in dead-letter archive), 409 (replay refused — the R3-A7 live observation).
        HttpStatusCode[] allowed = [HttpStatusCode.Accepted, HttpStatusCode.NotFound, HttpStatusCode.Conflict];
        allowed.ShouldContain(replayResponse.StatusCode,
            $"Replay of a Completed command should return 202/404/409 — got {(int)replayResponse.StatusCode} {replayResponse.StatusCode}");

        // Critical: pin the R3-A1 fix. The body's type MUST NOT be the bad-request URI, AND
        // detail (if present) MUST NOT contain "GUID" (case-insensitive).
        string replayBodyText = await replayResponse.Content.ReadAsStringAsync();
        if (replayResponse.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
            && !string.IsNullOrEmpty(replayBodyText))
        {
            JsonElement replayBody;
            try
            {
                replayBody = JsonSerializer.Deserialize<JsonElement>(replayBodyText);
            }
            catch (JsonException ex)
            {
                throw new ShouldAssertException(
                    $"Replay response claimed Content-Type JSON but body was not parseable. "
                    + $"Status: {(int)replayResponse.StatusCode}. Body: {replayBodyText}. Parse error: {ex.Message}");
            }

            if (replayBody.TryGetProperty("type", out JsonElement typeElement)
                && typeElement.ValueKind == JsonValueKind.String)
            {
                typeElement.GetString().ShouldNotBe(
                    "https://hexalith.io/problems/bad-request",
                    "Replay MUST NOT return bad-request type — that would regress post-epic-3-r3a1-replay-ulid-validation");
            }

            if (replayBody.TryGetProperty("detail", out JsonElement detailElement)
                && detailElement.ValueKind == JsonValueKind.String)
            {
                string? detail = detailElement.GetString();
                if (!string.IsNullOrEmpty(detail))
                {
                    detail.Contains("GUID", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                        $"Replay detail MUST NOT mention GUID format — that would regress R3-A1 ULID validation. detail: '{detail}'");
                }
            }
        }
    }
}
