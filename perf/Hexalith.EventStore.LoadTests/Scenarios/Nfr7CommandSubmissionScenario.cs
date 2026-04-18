using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.LoadTests.Helpers;

using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace Hexalith.EventStore.LoadTests.Scenarios;

/// <summary>
/// NFR7 — sustained command submission throughput.
/// Target: ≥ 100 cmd/sec per instance with NFR1 p99 ≤ 50ms holding under load.
/// Reports per-percentile latencies via NBomber HTML/CSV/MD exporters.
/// </summary>
internal static class Nfr7CommandSubmissionScenario
{
    private const int TargetRatePerSec = 100;
    private const int WarmupSeconds = 30;
    private const int SteadyStateSeconds = 300;
    private const string Tenant = "loadtest-tenant";
    private const string Domain = "counter";
    private const string CommandType = "Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter, Hexalith.EventStore.Sample";

    // Single shared HttpClient — thread-safe and reused across virtual users to avoid socket exhaustion.
    private static readonly HttpClient s_httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public static ScenarioProps Build(Uri baseAddress)
    {
        string token = LoadTestJwtTokenGenerator.GenerateToken(
            tenants: [Tenant],
            domains: [Domain],
            permissions: ["command:submit", "command:query"]);

        return Scenario.Create("nfr7_command_submission", async _ =>
        {
            // Random aggregate per request to spread actor load and avoid optimistic-concurrency contention.
            string aggregateId = $"counter-{Guid.NewGuid():N}";

            HttpRequestMessage request = BuildCommandRequest(baseAddress, token, aggregateId);
            var response = await Http.Send(s_httpClient, request).ConfigureAwait(false);
            return (IResponse)response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Warmup ramps to target rate to avoid cold-start skew in steady-state measurements.
            Simulation.RampingInject(rate: TargetRatePerSec, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(WarmupSeconds)),
            Simulation.Inject(rate: TargetRatePerSec, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(SteadyStateSeconds)));
    }

    private static HttpRequestMessage BuildCommandRequest(Uri baseAddress, string token, string aggregateId)
    {
        var body = new
        {
            MessageId = Guid.NewGuid().ToString(),
            Tenant,
            Domain,
            AggregateId = aggregateId,
            CommandType,
            Payload = new { id = Guid.NewGuid().ToString() },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress, "/api/v1/commands"))
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
