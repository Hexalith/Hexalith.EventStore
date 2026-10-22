using NBomber.Contracts;
using NBomber.CSharp;

namespace Hexalith.EventStore.LoadTests.Scenarios;

/// <summary>
/// NFR17 — capacity envelope: ≥ 10,000 active aggregates per instance with
/// command latency NFRs (NFR1, NFR3, NFR4) holding within target.
/// STATUS: STUB — implement before GA. Tracked separately from the harness scaffold.
/// </summary>
internal static class Nfr17ActiveAggregatesScenario {
    public static ScenarioProps Build(Uri baseAddress) =>
        // Implementation requirements (follow-up story):
        // - Activation phase: dispatch one command to each of 10,000 distinct aggregateIds
        //   (rotating tenants 1..10 to honor NFR18) — drives actor allocation up to capacity.
        // - Steady-state phase: random pick across the 10K population at e.g. 50 cmd/sec
        //   to keep the working set hot and detect activation/deactivation thrash.
        // - Capture: process working set, GC counters via dotnet-counters sidecar; assert
        //   no memory growth slope over 10min steady-state.
        Scenario.Create("nfr17_active_aggregates_stub", _ =>
            Task.FromResult((IResponse)Response.Ok()))
            .WithoutWarmUp()
            .WithLoadSimulations(Simulation.KeepConstant(copies: 0, during: TimeSpan.FromSeconds(1)));
}
