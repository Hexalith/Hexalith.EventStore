using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Testing.Security;

namespace Hexalith.EventStore.Admin.Cli.Tests.Client;

public class AdminApiClientProblemDetailsTests {
    private record TestResponse(string Id);

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetAsync_ProtectedProblemDetails_PreservesAllowListedSafeExtensions(HttpStatusCode statusCode) {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using AdminApiClient client = CreateClient(statusCode, ProtectedProblemDetailsJson((int)statusCode));

        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.GetAsync<TestResponse>("/api/v1/admin/streams/acme/orders/order-1/state", ct));

        ProtectedDataLeakSentinel.AssertNoLeak([ex.Message, JsonSerializer.Serialize(ex.Problem, JsonDefaults.Options)]);
        _ = ex.Problem.ShouldNotBeNull();
        ex.Problem.Type.ShouldBe(UnreadableProtectedDataProblem.TypeUri);
        ex.Problem.Status.ShouldBe((int)statusCode);
        ex.Problem.Extensions["reasonCode"].ShouldBe("missing-key");
        ex.Problem.Extensions["stage"].ShouldBe("cli-client-test");
        ex.Problem.Extensions["metadataVersion"].ShouldBe("3");
        ex.Problem.Extensions["retryable"].ShouldBe("true");
        ex.Problem.Extensions["permanent"].ShouldBe("false");
        ex.Problem.Extensions.ShouldNotContainKey("providerPrivateMetadata");
        ex.Problem.Extensions.ShouldNotContainKey("payloadJson");
        ex.Problem.Extensions.ShouldNotContainKey("unsafeArray");
        ex.Message.ShouldContain("Protected data is unreadable");
        ex.Message.ShouldContain("missing-key");
    }

    [Fact]
    public async Task TryGetAsync_NotFoundWithProblemDetailsBody_ReturnsNull_NotThrow() {
        // P-D1 / D1 — 404 must return null even when the response carries a safe ProblemDetails body
        // (Story 22.7d-1 added safe ProblemDetails on most error responses; existing CLI callers
        // depend on null for stream/aggregate-not-found semantics).
        CancellationToken ct = TestContext.Current.CancellationToken;
        using AdminApiClient client = CreateClient(HttpStatusCode.NotFound, ProtectedProblemDetailsJson(404));

        TestResponse? result = await client.TryGetAsync<TestResponse>("/api/v1/admin/streams/acme/orders/order-1/state", ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ProblemDetailsLargeAllowListedExtension_TruncatesToBoundedLength() {
        // P12 — per-extension length cap (1024 chars). A 60 KB upstream-controlled extension must
        // be truncated before landing on AdminApiException.Problem.Extensions.
        CancellationToken ct = TestContext.Current.CancellationToken;
        string oversized = new('a', 4096);
        string body = JsonSerializer.Serialize(new Dictionary<string, object?> {
            ["type"] = UnreadableProtectedDataProblem.TypeUri,
            ["title"] = UnreadableProtectedDataProblem.DefaultTitle,
            ["status"] = 422,
            ["reasonCode"] = oversized,
        }, JsonDefaults.Options);
        using AdminApiClient client = CreateClient(HttpStatusCode.UnprocessableEntity, body);

        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.GetAsync<TestResponse>("/api/v1/admin/streams/acme/orders/order-1/state", ct));

        _ = ex.Problem.ShouldNotBeNull();
        ex.Problem.Extensions["reasonCode"].Length.ShouldBeLessThanOrEqualTo(1024);
    }

    [Fact]
    public async Task GetAsync_MalformedProblemDetails_DoesNotCopyRawBodyToMessage() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using AdminApiClient client = CreateClient(
            HttpStatusCode.UnprocessableEntity,
            "{ \"type\": \"" + UnreadableProtectedDataProblem.TypeUri + "\", \"detail\": \"" + ProtectedDataLeakSentinel.ProtectedPayloadPlaintext);

        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.GetAsync<TestResponse>("/api/v1/admin/streams/acme/orders/order-1/state", ct));

        ProtectedDataLeakSentinel.AssertNoLeak([ex.Message]);
        ex.Problem.ShouldBeNull();
        ex.Message.ShouldContain("Admin API request failed");
    }

    [Fact]
    public async Task GetAsync_OversizedProblemDetails_DoesNotCopyRawBodyToMessage() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string body = new string('x', 70_000) + ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob;
        using AdminApiClient client = CreateClient(HttpStatusCode.UnprocessableEntity, body);

        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.GetAsync<TestResponse>("/api/v1/admin/streams/acme/orders/order-1/state", ct));

        ProtectedDataLeakSentinel.AssertNoLeak([ex.Message]);
        ex.Problem.ShouldBeNull();
        ex.Message.ShouldContain("Admin API request failed");
    }

    private static AdminApiClient CreateClient(HttpStatusCode statusCode, string body) {
        HttpResponseMessage response = new(statusCode) {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/problem+json"),
        };
        MockHttpMessageHandler handler = new(response);
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        return new AdminApiClient(options, handler);
    }

    private static string ProtectedProblemDetailsJson(int statusCode)
        => JsonSerializer.Serialize(new Dictionary<string, object?> {
            ["type"] = UnreadableProtectedDataProblem.TypeUri,
            ["title"] = UnreadableProtectedDataProblem.DefaultTitle,
            ["status"] = statusCode,
            ["detail"] = "Protected data could not be read safely.",
            ["reasonCode"] = "missing-key",
            ["reasonCategory"] = "MissingKey",
            ["stage"] = "cli-client-test",
            ["metadataVersion"] = 3,
            ["retryable"] = true,
            ["permanent"] = false,
            ["tenantId"] = "acme",
            ["domain"] = "orders",
            ["aggregateId"] = "order-1",
            ["sequenceNumber"] = 42,
            ["correlationId"] = "corr-1",
            ["providerPrivateMetadata"] = ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob,
            ["payloadJson"] = new { protectedPayload = ProtectedDataLeakSentinel.ProtectedPayloadPlaintext },
            ["unsafeArray"] = new[] { ProtectedDataLeakSentinel.ProtectedKeyAlias },
        }, JsonDefaults.Options);
}
