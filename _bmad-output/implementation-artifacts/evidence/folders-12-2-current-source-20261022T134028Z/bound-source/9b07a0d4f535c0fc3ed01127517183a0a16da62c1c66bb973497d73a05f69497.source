
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
/// Tier 3 end-to-end tests proving consistency between validation and submission endpoints.
/// AC: #13.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class CrossEndpointFlowE2ETests {
    private readonly AspireContractTestFixture _fixture;

    public CrossEndpointFlowE2ETests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #13: Validate command → isAuthorized:true, then submit same command → 202 Accepted,
    /// then poll to Completed. Proves validation oracle matches actual authorization.
    /// </summary>
    [Fact]
    public async Task ValidateThenSubmitCommand_BothSucceed_ProvesConsistency() {
        // Step 1: Validate command authorization
        using HttpRequestMessage validateRequest = ContractTestHelpers.CreateCommandValidationRequest(
            "tenant-a", "counter", "IncrementCounter");

        using HttpResponseMessage validateResponse = await _fixture.EventStoreClient
            .SendAsync(validateRequest);

        validateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement validateResult = await validateResponse.Content.ReadFromJsonAsync<JsonElement>();
        validateResult.GetProperty("isAuthorized").GetBoolean().ShouldBeTrue();

        // Step 2: Submit the same command
        string aggregateId = $"cross-flow-{Guid.NewGuid():N}";
        string correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdAsync(
            _fixture.EventStoreClient, "tenant-a", "counter", aggregateId, "IncrementCounter");

        correlationId.ShouldNotBeNullOrEmpty();

        // Step 3: Poll status to Completed
        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            _fixture.EventStoreClient, correlationId, "tenant-a");

        status.GetProperty("status").GetString().ShouldBe("Completed");
    }

    /// <summary>
    /// AC #13: Validate command with wrong tenant → isAuthorized:false, then submit → 403.
    /// Proves denial is consistent.
    /// </summary>
    [Fact]
    public async Task ValidateThenSubmitCommand_ValidationDenied_SubmitAlsoDenied() {
        // Step 1: Validate command with wrong tenant claims
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-b"],
            domains: ["counter"],
            permissions: ["command:submit"]);

        var validateBody = new {
            Tenant = "tenant-a",
            Domain = "counter",
            CommandType = "IncrementCounter",
        };

        using var validateRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands/validate") {
            Content = new StringContent(
                JsonSerializer.Serialize(validateBody),
                Encoding.UTF8,
                "application/json"),
        };
        validateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage validateResponse = await _fixture.EventStoreClient
            .SendAsync(validateRequest);

        validateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement validateResult = await validateResponse.Content.ReadFromJsonAsync<JsonElement>();
        validateResult.GetProperty("isAuthorized").GetBoolean().ShouldBeFalse();

        // Step 2: Submit the same command with the same wrong-tenant token → 403
        var submitBody = new {
            MessageId = Guid.NewGuid().ToString(),
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = $"cross-flow-denied-{Guid.NewGuid():N}",
            CommandType = "IncrementCounter",
            Payload = new { id = Guid.NewGuid().ToString() },
        };

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(submitBody),
                Encoding.UTF8,
                "application/json"),
        };
        submitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage submitResponse = await _fixture.EventStoreClient
            .SendAsync(submitRequest);

        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
