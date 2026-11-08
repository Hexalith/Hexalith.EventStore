using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>Explicit live qualification; failures remain evidence and block qualification.</summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
public sealed class DurableBrokerQualificationTests
{
    /// <summary>Requires independent persisted readback for every real Dapr broker fault vector.</summary>
    [Fact(Explicit = true)]
    public async Task BothCandidates_AllFaultScenarios_RetainVerifiedOutcomes()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        await using var fixture = new DurableBrokerQualificationFixture();
        using JsonDocument packet = await fixture.QualifyAsync(cancellation.Token).ConfigureAwait(true);
        packet.RootElement.GetProperty("productionEligible").GetBoolean().ShouldBeFalse();
        packet.RootElement.GetProperty("selectedBroker").ValueKind.ShouldBe(JsonValueKind.Null);
        string[] expected = ["crash-redelivery", "broker-restart", "durable-quarantine", "failed-diversion",
            "retention-expiry", "stable-id-deduplication", "app-topic-scopes", "tls-negative", "missing-secret",
            "secret-rotation", "cancellation-backpressure", "missing-bindings"];
        foreach (string broker in new[] { "rabbitmq", "kafka" })
        {
            JsonElement results = packet.RootElement.GetProperty("brokers").GetProperty(broker);
            results.EnumerateObject().Select(result => result.Name).Order(StringComparer.Ordinal)
                .ShouldBe(expected.Order(StringComparer.Ordinal));
            foreach (string scenario in expected)
            {
                JsonElement outcome = results.GetProperty(scenario);
                outcome.GetProperty("result").GetString().ShouldBeOneOf("PASS", "FAIL");
                string observationPath = outcome.GetProperty("observation").GetString()!;
                using JsonDocument observation = JsonDocument.Parse(await File.ReadAllTextAsync(
                    Path.Combine(fixture.EvidenceDirectory, observationPath), cancellation.Token).ConfigureAwait(true));
                // Rejected broker guarantees are retained outcomes. A fixture
                // readiness failure still fails this live test.
                if (observation.RootElement.TryGetProperty("blockedBy", out JsonElement blocker))
                {
                    throw new InvalidOperationException($"{broker}/{scenario} did not execute: {blocker.GetString()}");
                }
            }
        }
    }
}
