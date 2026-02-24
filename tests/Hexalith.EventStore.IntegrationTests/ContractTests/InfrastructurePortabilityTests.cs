
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
/// Tier 3 tests verifying infrastructure portability (AC #6).
///
/// INFRASTRUCTURE PORTABILITY NOTES:
/// ================================
/// All Tier 3 contract tests in this project are backend-agnostic by design.
/// They operate exclusively through the REST API contract and never make
/// backend-specific assertions (no Redis commands, no direct state store access).
///
/// To swap Redis for PostgreSQL:
/// 1. In the Aspire AppHost (src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs),
///    replace the Redis-backed Dapr state store component with a PostgreSQL-backed one.
///    The Dapr component name ("statestore") stays the same -- only the backing store changes.
///
/// 2. Similarly, swap the Dapr pub/sub component from Redis to a PostgreSQL-backed pub/sub.
///    Again, the component name ("pubsub") stays the same.
///
/// 3. All contract tests will pass without modification because:
///    - Tests interact only via HTTP REST API (POST /api/v1/commands, GET /api/v1/commands/status/{id})
///    - State persistence is abstracted by Dapr components
///    - Event publishing is abstracted by Dapr pub/sub
///    - No test makes Redis-specific assertions (no key patterns, no Redis CLI, no RESP protocol)
///
/// This fulfills NFR29: Zero code changes when swapping backends.
///
/// Example PostgreSQL Dapr component configuration:
/// <code>
/// apiVersion: dapr.io/v1alpha1
/// kind: Component
/// metadata:
///   name: statestore
/// spec:
///   type: state.postgresql
///   version: v1
///   metadata:
///     - name: connectionString
///       value: "host=localhost;port=5432;database=eventstore;user=dapr;password=dapr"
///     - name: actorStateStore
///       value: "true"
/// </code>
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class InfrastructurePortabilityTests {
    private readonly AspireContractTestFixture _fixture;

    public InfrastructurePortabilityTests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #6: Verify test design is backend-agnostic. This test exercises the full command
    /// lifecycle through the REST API without any backend-specific assertions.
    /// The same test passes regardless of whether Redis or PostgreSQL backs the Dapr components.
    /// </summary>
    [Fact]
    public async Task CommandLifecycle_BackendAgnostic_NoInfrastructureSpecificAssertions() {
        // Arrange - standard command submission through REST API only
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:submit", "command:query"]);

        string aggregateId = $"portability-{Guid.NewGuid():N}";

        var body = new {
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = aggregateId,
            CommandType = "IncrementCounter",
            Payload = new { id = Guid.NewGuid().ToString() },
        };

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        submitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - submit via REST API (backend-agnostic)
        using HttpResponseMessage submitResponse = await _fixture.CommandApiClient
            .SendAsync(submitRequest);

        if (submitResponse.StatusCode != HttpStatusCode.Accepted) {
            string responseBody = await submitResponse.Content.ReadAsStringAsync();
            throw new ShouldAssertException(
                $"Expected 202 Accepted but was {(int)submitResponse.StatusCode}.\nBody:\n{responseBody}");
        }

        JsonElement submitResult = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        string correlationId = submitResult.GetProperty("correlationId").GetString()!;

        // Act - poll status via REST API (backend-agnostic)
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30));
        string? terminalStatus = null;

        while (DateTimeOffset.UtcNow < deadline) {
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{correlationId}");
            statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage statusResponse = await _fixture.CommandApiClient
                .SendAsync(statusRequest);

            if (statusResponse.StatusCode == HttpStatusCode.OK) {
                JsonElement status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
                terminalStatus = status.GetProperty("status").GetString();

                if (terminalStatus is "Completed" or "Rejected" or "PublishFailed" or "TimedOut") {
                    break;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        // Assert - only REST API response assertions, zero backend-specific checks
        // No Redis key patterns, no direct state store queries, no RESP protocol assertions
        terminalStatus.ShouldNotBeNull("Command should reach a terminal status");
        terminalStatus.ShouldBe("Completed");
    }
}
