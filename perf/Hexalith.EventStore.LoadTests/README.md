# Hexalith.EventStore Load Tests

NBomber-based load harness for the EventStore performance NFRs.

## Status

| Scenario | NFR | Status |
|---|---|---|
| `nfr7` | NFR7 — 100 cmd/sec sustained, NFR1 p99 ≤ 50ms | **Implemented** |
| `nfr39` | NFR39 — 1,000 qps sustained, NFR36/NFR37 latency budgets | **Stub** |
| `nfr17` | NFR17 — 10,000 active aggregates, NFR1/3/4 latency budgets | **Stub** |

The two stub scenarios compile and register but do nothing — they reserve the names and document the implementation requirements. Implement them as separate stories.

## Running

The harness is decoupled from infrastructure orchestration: it expects the EventStore to already be reachable at a target URL.

### Local

1. Start the Aspire AppHost (which boots DAPR + EventStore + sample domain):

    ```bash
    dotnet run --project src/Hexalith.EventStore.AppHost
    ```

2. Wait for the AppHost dashboard to show all resources `Running`. Note the `eventstore` HTTP endpoint (e.g. `http://localhost:5170`).

3. Run the load test against it:

    ```bash
    EVENTSTORE_BASE_URL=http://localhost:5170 \
      dotnet run --project perf/Hexalith.EventStore.LoadTests --configuration Release
    ```

4. Open `perf/Hexalith.EventStore.LoadTests/reports/<timestamp>/report.html` for the per-percentile summary.

### Selecting scenarios

```bash
# Single scenario (default: nfr7)
dotnet run --project perf/Hexalith.EventStore.LoadTests -- nfr7

# Multiple scenarios — comma-separated env var
LOAD_TEST_SCENARIOS=nfr7,nfr39 dotnet run --project perf/Hexalith.EventStore.LoadTests
```

### CI

The `.github/workflows/perf-lab.yml` workflow runs on `workflow_dispatch` (manual). It:

1. Sets up DAPR (`dapr init`)
2. Starts the AppHost in the background
3. Waits for the EventStore endpoint to become healthy
4. Runs the load harness with `LOAD_TEST_SCENARIOS=nfr7`
5. Uploads the HTML/CSV/MD reports as a build artifact

Add a nightly schedule once the harness is stable across runs and you have a baseline to gate against.

## Auth

The `nfr7` scenario uses synthetic dev-signed JWTs (`LoadTestJwtTokenGenerator`) that match the EventStore's dev signing key. This is fine for performance measurement — auth latency is in the critical path and consistent across runs — but the tokens will not validate against a Keycloak-backed deployment. For Keycloak-targeting load runs, swap in a Keycloak token issuer modeled on `tests/Hexalith.EventStore.IntegrationTests/Helpers/KeycloakTokenHelper.cs`.

## Interpreting reports

NBomber emits per-scenario percentiles in `report.html`:

- **p99 ≤ 50ms** validates NFR1 under the NFR7 throughput envelope.
- **OK count / time** at the target rate validates NFR7 (≥ 100 cmd/sec sustained without saturation).
- **Failure rate** > 0 indicates the system could not absorb the requested load — investigate sidecar latency, state store, or actor activation.

For pre-GA gating, define a threshold file the workflow asserts against (e.g. `dotnet run -- ... && assert-percentiles --p99-ms 50`). That gating step is intentionally not in the scaffold — add once you have baseline numbers.
