using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Server.Actors.Authorization;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Integration;

/// <summary>
/// Tier 2 integration tests verifying actor-based authorization flows.
/// Uses <see cref="ActorBasedAuthWebApplicationFactory"/> with mocked IActorProxyFactory.
/// AC: #14, #15.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Tier", "2")]
public class ActorBasedAuthIntegrationTests : IClassFixture<ActorBasedAuthWebApplicationFactory> {
    private readonly ActorBasedAuthWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ActorBasedAuthIntegrationTests(ActorBasedAuthWebApplicationFactory factory) {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _factory.ResetActors();
        _client = factory.CreateClient();
    }

    /// <summary>
    /// AC #14: When actor-based tenant validator approves, command is accepted.
    /// </summary>
    [Fact]
    public async Task ActorTenantValidation_Authorized_CommandAccepted() {
        // Arrange
        _factory.FakeTenantActor.ConfiguredResult = new ActorValidationResponse(true);
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>
    /// AC #14: When actor-based tenant validator denies, command is rejected with 403.
    /// </summary>
    [Fact]
    public async Task ActorTenantValidation_Denied_CommandRejected403() {
        // Arrange
        _factory.FakeTenantActor.ConfiguredResult = new ActorValidationResponse(false, "Tenant denied");
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// AC #15: When actor-based RBAC validator denies for commands, rejected with 403.
    /// </summary>
    [Fact]
    public async Task ActorRbacValidation_DeniedForCommand_CommandRejected403() {
        // Arrange
        _factory.FakeTenantActor.ConfiguredResult = new ActorValidationResponse(true);
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(false, "RBAC denied");

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// AC #15: Command submission passes messageCategory="command" to RBAC validator actor.
    /// </summary>
    [Fact]
    public async Task ActorRbacValidation_CommandMessageCategoryPassedCorrectly() {
        // Arrange
        _factory.FakeTenantActor.ConfiguredResult = new ActorValidationResponse(true);
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);

        using HttpRequestMessage request = CreateCommandRequest("tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _factory.FakeRbacActor.ReceivedRequests.ShouldNotBeEmpty();
        RbacValidationRequest lastRequest = _factory.FakeRbacActor.ReceivedRequests.Last();
        lastRequest.MessageCategory.ShouldBe("command");
    }

    /// <summary>
    /// AC #15: Query validation passes messageCategory="query" to RBAC validator actor.
    /// </summary>
    [Fact]
    public async Task ActorRbacValidation_QueryMessageCategoryPassedCorrectly() {
        // Arrange
        _factory.FakeTenantActor.ConfiguredResult = new ActorValidationResponse(true);
        _factory.FakeRbacActor.ConfiguredResult = new ActorValidationResponse(true);

        using HttpRequestMessage request = CreateQueryValidationRequest("tenant-a", "counter", "GetOrderDetails");

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.FakeRbacActor.ReceivedRequests.ShouldNotBeEmpty();
        RbacValidationRequest lastRequest = _factory.FakeRbacActor.ReceivedRequests.Last();
        lastRequest.MessageCategory.ShouldBe("query");
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
            AggregateId = $"actor-auth-{Guid.NewGuid():N}",
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

    private static HttpRequestMessage CreateQueryValidationRequest(
        string tenant, string domain, string queryType) {
        string token = TestJwtHelper.GenerateToken(
            tenants: [tenant],
            domains: [domain],
            permissions: ["query:read"]);

        var body = new {
            Tenant = tenant,
            Domain = domain,
            QueryType = queryType,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries/validate") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
