
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Dapr.Actors;

using Hexalith.EventStore.Server.Actors.Authorization;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Integration;

/// <summary>
/// Tier 2 integration tests verifying 503 Service Unavailable responses when
/// actor-based authorization validators fail. Uses the shared
/// <see cref="ActorBasedAuthWebApplicationFactory"/> fixture.
/// AC: #16, #17, #18.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Tier", "2")]
public class AuthorizationServiceUnavailableTests : IClassFixture<ActorBasedAuthWebApplicationFactory> {
    private readonly ActorBasedAuthWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationServiceUnavailableTests(ActorBasedAuthWebApplicationFactory factory) {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _factory.ResetActors();
        _client = factory.CreateClient();
    }

    /// <summary>
    /// AC #16: When tenant validator actor throws, returns 503 with Retry-After header.
    /// </summary>
    [Fact]
    public async Task TenantActorUnavailable_Returns503WithRetryAfter() {
        // Arrange - configure fake tenant actor to throw (simulates DAPR connectivity failure)
        _factory.FakeTenantActor.ConfiguredException = CreateActorInvocationException(
            "Simulated DAPR actor connectivity failure");
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        _ = response.Headers.RetryAfter.ShouldNotBeNull();
        response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ShouldBe(5);

        // Cleanup
        _factory.FakeTenantActor.ConfiguredException = null;
    }

    /// <summary>
    /// AC #17: When RBAC validator actor throws, returns 503 with Retry-After header.
    /// </summary>
    [Fact]
    public async Task RbacActorUnavailable_Returns503WithRetryAfter() {
        // Arrange - tenant passes, RBAC actor throws
        _factory.FakeTenantActor.ConfiguredResult = new ActorValidationResponse(true);
        _factory.FakeRbacActor.ConfiguredException = CreateActorInvocationException(
            "Simulated DAPR actor connectivity failure");

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        _ = response.Headers.RetryAfter.ShouldNotBeNull();
        response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ShouldBe(5);

        // Cleanup
        _factory.FakeRbacActor.ConfiguredException = null;
    }

    /// <summary>
    /// AC #16: 503 response body is RFC 9457 ProblemDetails and does NOT leak internal details.
    /// </summary>
    [Fact]
    public async Task ServiceUnavailable_ResponseIsProblemDetails() {
        // Arrange
        _factory.FakeTenantActor.ConfiguredException = CreateActorInvocationException(
            "Actor connection timeout");
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert - ProblemDetails structure
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        contentType.ShouldContain("problem+json");

        JsonElement pd = await response.Content.ReadFromJsonAsync<JsonElement>();
        pd.GetProperty("status").GetInt32().ShouldBe(503);
        pd.GetProperty("title").GetString().ShouldNotBeNullOrEmpty();

        // CRITICAL negative security assertions: no internal details in response body
        string responseBody = await response.Content.ReadAsStringAsync();
        responseBody.ShouldNotContain("TestTenantValidatorActor");
        responseBody.ShouldNotContain("ActorMethodInvocationException");
        responseBody.ShouldNotContain("StackTrace");
        responseBody.ShouldNotContain("tenant-a", Case.Sensitive,
            "Response body should not contain actor ID (tenant ID)");

        // Cleanup
        _factory.FakeTenantActor.ConfiguredException = null;
    }

    /// <summary>
    /// AC #16: Retry-After header matches configured RetryAfterSeconds (default 5).
    /// </summary>
    [Fact]
    public async Task ServiceUnavailable_RetryAfterHeaderPresent() {
        // Arrange
        _factory.FakeTenantActor.ConfiguredException = CreateActorInvocationException(
            "Simulated actor failure");
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        _ = response.Headers.RetryAfter.ShouldNotBeNull();
        response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ShouldBe(5);

        // Cleanup
        _factory.FakeTenantActor.ConfiguredException = null;
    }

    /// <summary>
    /// AC #18: Full exception translation chain. Actor proxy throws ->
    /// ActorTenantValidator wraps as AuthorizationServiceUnavailableException ->
    /// AuthorizationServiceUnavailableHandler produces 503 + Retry-After.
    /// The fake actor throws directly (simulating what happens when the real DAPR proxy
    /// throws ActorMethodInvocationException), and the validator catches + wraps it.
    /// </summary>
    [Fact]
    public async Task ExceptionTranslationChain_ActorProxyThrows_ValidatorWraps_HandlerProduces503() {
        // Arrange - fake actor throws (simulates ActorMethodInvocationException from DAPR)
        _factory.FakeTenantActor.ConfiguredException = CreateActorInvocationException(
            "Actor method invocation failed: actor type not found");
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert - the full chain produced 503
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        contentType.ShouldContain("problem+json");

        _ = response.Headers.RetryAfter.ShouldNotBeNull();
        response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ShouldBe(5);

        // Verify the exception was caught by 503 handler, NOT the 403 handler
        // (the 503 handler is registered before 403 in ServiceCollectionExtensions)
        JsonElement pd = await response.Content.ReadFromJsonAsync<JsonElement>();
        pd.GetProperty("status").GetInt32().ShouldBe(503);
        pd.GetProperty("title").GetString().ShouldBe("Service Unavailable");

        // Cleanup
        _factory.FakeTenantActor.ConfiguredException = null;
    }

    private static HttpRequestMessage CreateCommandRequest(
        string tenant, string domain, string commandType) {
        string token = TestJwtHelper.GenerateToken(
            tenants: [tenant],
            domains: [domain],
            permissions: ["command:submit", "command:query"]);

        var body = new {
            MessageId = Guid.NewGuid().ToString(),
            Tenant = tenant,
            Domain = domain,
            AggregateId = $"unavail-{Guid.NewGuid():N}",
            CommandType = commandType,
            Payload = new { id = Guid.NewGuid().ToString() },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static ActorMethodInvocationException CreateActorInvocationException(string message) =>
        new(
            message,
            new InvalidOperationException("Simulated DAPR sidecar connectivity failure"),
            true);
}
