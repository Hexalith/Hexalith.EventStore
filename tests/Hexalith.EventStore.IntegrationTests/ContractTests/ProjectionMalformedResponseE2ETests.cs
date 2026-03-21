using System.Net;
using System.Text.Json;

using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 scaffold for validating malformed /project responses through the real Aspire + DAPR path.
/// This covers Story 11-3 AC #3 where projection update failures must fail-open.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireProjectionFaultTests")]
public class ProjectionMalformedResponseE2ETests {
    private readonly AspireProjectionFaultTestFixture _fixture;

    public ProjectionMalformedResponseE2ETests(AspireProjectionFaultTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// With malformed /project response enabled in sample service, command processing must remain non-blocking.
    /// This validates fail-open behavior for projection update errors in the real Aspire + DAPR path.
    /// </summary>
    [Fact]
    public async Task MalformedProjectResponse_DoesNotBreakCommandProcessing() {
        HttpClient sampleClient = _fixture.App.CreateHttpClient("sample");

        // Verify fault injection endpoint is active and returns malformed JSON.
        using (HttpResponseMessage malformedResponse = await sampleClient.PostAsync("/project", content: null)) {
            malformedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            string body = await malformedResponse.Content.ReadAsStringAsync();
            _ = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ProjectionResponse>(body));
        }

        string aggregateId = $"projection-fault-{Guid.NewGuid():N}";

        string correlationId = await ContractTestHelpers.SubmitCommandAndGetCorrelationIdAsync(
            _fixture.CommandApiClient,
            "tenant-a",
            "counter",
            aggregateId,
            "IncrementCounter");

        JsonElement status = await ContractTestHelpers.PollUntilTerminalStatusAsync(
            _fixture.CommandApiClient,
            correlationId,
            "tenant-a");

        status.GetProperty("status").GetString().ShouldBe("Completed");
    }
}
