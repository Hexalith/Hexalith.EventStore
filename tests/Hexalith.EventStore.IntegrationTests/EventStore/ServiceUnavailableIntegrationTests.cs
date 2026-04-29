extern alias eventstore;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using eventstore::Hexalith.EventStore.Authorization;
using eventstore::Hexalith.EventStore.ErrorHandling;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.IntegrationTests.EventStore;

/// <summary>
/// Tier 3 (in-process WebApplicationFactory) tests for the 503 Service Unavailable
/// contract introduced by Story 3.5. Verifies UX-DR2, UX-DR5, UX-DR6, UX-DR7, UX-DR11.
/// Sidecar-unavailable path is intentionally deferred — see Dev Agent Record (R3-A6 / Task 5.4).
/// </summary>
public class ServiceUnavailableIntegrationTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory> {
    [Fact]
    public async Task PostCommands_AuthorizationServiceUnavailable_Returns503ProblemDetails() {
        // Arrange — swap ITenantValidator for a stub that throws
        // AuthorizationServiceUnavailableException, simulating an actor-based auth-service outage.
        // Pattern matches ConcurrencyConflictIntegrationTests (explicit-descriptor swap).
        using WebApplicationFactory<EventStoreProgram> customFactory =
            factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => {
                ServiceDescriptor? descriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ITenantValidator));
                if (descriptor is not null) {
                    _ = services.Remove(descriptor);
                }

                _ = services.AddSingleton<ITenantValidator, ThrowingTenantValidator>();
            }));

        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateCommandRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert — response shape (UX-DR5, UX-DR7).
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");
        response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? retryValues).ShouldBeTrue();
        // Single() over First(): a duplicate Retry-After header would be a real contract regression
        // (UX-DR5 specifies one value of "30") and should fail the test, not pass silently.
        retryValues!.Single().ShouldBe("30");

        // CRITICAL sync point per AC #5: read response body to completion BEFORE inspecting StatusStore.
        // Reading the body forces the request thread to unwind so the no-domain-side-effect
        // assertion below is not racy on CI.
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("status").GetInt32().ShouldBe(503);
        body.GetProperty("type").GetString()
            .ShouldBe("https://hexalith.io/problems/service-unavailable");
        body.GetProperty("title").GetString().ShouldBe("Service Unavailable");

        string detail = body.GetProperty("detail").GetString()!;
        detail.ShouldContain("command processing pipeline");

        // UX-DR11: 503 detail MUST NOT name internal components.
        // Case.Insensitive bans catch the human-language tokens; the case-sensitive `Unavailable`
        // ban (capital U) catches the gRPC `StatusCode.Unavailable` identifier specifically — the
        // lowercase English word "unavailable" appears legitimately in the production detail
        // ("temporarily unavailable") and stays allowed.
        detail.ShouldNotContain("Authorization service", Case.Insensitive);
        detail.ShouldNotContain("DAPR sidecar", Case.Insensitive);
        detail.ShouldNotContain("actor", Case.Insensitive);
        detail.ShouldNotContain("gRPC", Case.Insensitive);
        detail.ShouldNotContain("Unavailable", Case.Sensitive);

        // UX-DR2: no correlationId on 503 (pre-pipeline rejection — the auth behavior throws
        // before any correlation ID is allocated). tenantId likewise has no authenticated context
        // to attach to and must not appear.
        body.TryGetProperty("correlationId", out _).ShouldBeFalse();
        body.TryGetProperty("tenantId", out _).ShouldBeFalse();

        // No domain side effect — AuthorizationBehavior throws before SubmitCommandHandler runs,
        // so no CommandStatusRecord (Received or Completed) leaks into the in-memory store.
        // Project leaked records into a list so the failure message names them, instead of the
        // bare "false was expected to be true" diagnostic from .Any(...).ShouldBeFalse().
        IReadOnlyDictionary<string, (CommandStatusRecord Record, DateTimeOffset Expiry)> all =
            factory.StatusStore.GetAllStatuses();
        List<string> leakedRecords = [.. all
            .Where(kv => kv.Value.Record.Status == CommandStatus.Received
                      || kv.Value.Record.Status == CommandStatus.Completed)
            .Select(kv => $"CorrelationId={kv.Key}, Status={kv.Value.Record.Status}")];
        leakedRecords.ShouldBeEmpty(
            "503 path must not write any CommandStatusRecord — AuthorizationBehavior rejects before SubmitCommandHandler.");
    }

    private static object CreateCommandRequest() => new {
        messageId = Guid.NewGuid().ToString(),
        tenant = "test-tenant",
        domain = "test-domain",
        aggregateId = "agg-503",
        commandType = "CreateOrder",
        payload = new { amount = 100 },
    };

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<EventStoreProgram> factory) {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Stub validator that simulates an actor-based authorization service outage by always
    /// throwing <see cref="AuthorizationServiceUnavailableException"/>. Pattern matches the
    /// nested <c>ConcurrencyConflictSimulatingHandler</c> in
    /// <c>ConcurrencyConflictIntegrationTests.cs</c>.
    /// </summary>
    private sealed class ThrowingTenantValidator : ITenantValidator {
        public Task<TenantValidationResult> ValidateAsync(
            ClaimsPrincipal user,
            string tenantId,
            CancellationToken cancellationToken,
            string? aggregateId = null)
            => throw new AuthorizationServiceUnavailableException(
                actorTypeName: "ActorTenantValidator",
                actorId: "test-tenant",
                reason: "Simulated outage",
                innerException: new InvalidOperationException("test"));
    }
}
