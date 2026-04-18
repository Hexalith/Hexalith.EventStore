using NBomber.Contracts;
using NBomber.CSharp;

namespace Hexalith.EventStore.LoadTests.Scenarios;

/// <summary>
/// NFR39 — sustained query throughput.
/// Target: ≥ 1,000 queries/sec per instance, NFR36 (cache hit p99 ≤ 10ms) and
/// NFR37 (cache miss p99 ≤ 200ms) holding under load.
/// STATUS: STUB — implement before GA. Tracked separately from the harness scaffold.
/// </summary>
internal static class Nfr39QueryThroughputScenario {
    public static ScenarioProps Build(Uri baseAddress) =>
        // Implementation requirements (follow-up story):
        // - Pre-seed N aggregates so cache-hit vs cache-miss can be controlled.
        // - Mix: ~80% cache-hit, ~20% cache-miss to validate both NFR36 and NFR37.
        // - Use POST /api/v1/queries with the GetCounterStatusQuery shape.
        // - Ramp 0 → 1000 qps, hold 5min steady; surface per-percentile latencies per mix bucket.
        Scenario.Create("nfr39_query_throughput_stub", _ =>
            Task.FromResult((IResponse)Response.Ok()))
            .WithoutWarmUp()
            .WithLoadSimulations(Simulation.KeepConstant(copies: 0, during: TimeSpan.FromSeconds(1)));
}
