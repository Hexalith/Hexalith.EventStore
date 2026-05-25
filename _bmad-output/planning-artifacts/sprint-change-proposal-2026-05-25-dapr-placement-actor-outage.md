# Sprint Change Proposal — DAPR Actor Placement Outage Misdiagnosed as a UI Transport Fault

- **Date:** 2026-05-25
- **Author:** Jérôme Piquot (with Claude Code)
- **Trigger:** Recurring `TaskCanceledException` on Sample BlazorUI → EventStore query calls, **after** the D13/D14 DAPR-transport migration had already shipped and the exception persisted.
- **Scope classification:** Moderate (root cause is environmental; the durable change is a server-side observability guardrail + docs).
- **Supersedes the root-cause analysis of:** `sprint-change-proposal-2026-05-25-sample-ui-dapr-invocation.md` (D14) and, by extension, the Admin.UI D13 proposal — both attributed this failure to UI transport.

---

## Section 1 — Issue Summary

The Sample BlazorUI surfaces a `TaskCanceledException` from `EventStoreApiAuthorizationHandler.SendAsync` (line 13) when loading a counter page. The D14 proposal hypothesised a missing DAPR sidecar / Aspire service-discovery resolution failure and migrated the BlazorUI → EventStore hop to DAPR service invocation.

**That fix shipped in full** (AppHost sidecar, `DaprAppIdHandler`, DI re-point, ACL — all verified present) **and the identical exception still occurred.** A live reproduction under `aspire run` (2026-05-25) localised the real fault three layers below the UI:

- The query reaches EventStore, authenticates (`admin-user`, global-admin bypass), and routes to the projection actor `counter:tenant-a:counter-1` (Tier 1). It then **hangs ~10 s**, the BlazorUI resilience handler's per-attempt timeout fires, three fast `500` retries follow (the actor turn lock is held), and the request ends after ~20 s as a `TaskCanceledException`.
- An **unrelated** actor — the startup `BootstrapGlobalAdmin` command actor `system:global-administrators:global-administrators` — failed identically (100 s actor-proxy timeout). Two different actors, two domains, the same hang.
- The EventStore DAPR sidecar console showed a continuous loop:
  `Failed to connect to placement service: ... error reading server preface: EOF. Retrying...`
  (and the same for the scheduler host). The placement/scheduler containers were `Up 23 hours` and self-reported healthy, but the current run's sidecars never completed the gRPC stream through Docker Desktop's port-forward (`6050→50005`). With placement unreachable, **no actor can be located or activated**, so every actor invocation hangs to its timeout.

**Proof:** `docker restart dapr_placement dapr_scheduler` → the exact same query went from **20.3 s (→ TaskCanceledException)** to **HTTP 200 in 0.2 s**.

**Root cause:** a DAPR control-plane (placement) connectivity outage in the local environment — *not* the UI, *not* the transport, *not* the actor code. The transport migration was harmless but neither caused nor could fix the bug.

### Evidence index

| Signal | Value |
|--------|-------|
| Trace | `d50ffc265d78c64985f1fb4149f27624` — `GET /pattern-silent-reload`, 20,120 ms |
| Hanging span | `CallActor/ProjectionActor/QueryAsync` (`counter:tenant-a:counter-1`), 10,001 ms → 500 |
| Corroborating failure | `BootstrapGlobalAdmin` → actor `system:global-administrators:global-administrators`, `TaskCanceledException … HttpClient.Timeout of 100 seconds` |
| Sidecar log | `error reading server preface: EOF` connecting to placement `localhost:6050` (+ scheduler `6060`) |
| Control plane | `dapr_placement` / `dapr_scheduler` containers `Up 23h`, image `daprio/dapr:1.17.7` (version-matched), no new sidecar registrations logged for the current run |
| Fix proof | restart placement+scheduler → query 20.3 s → 0.2 s |

---

## Section 2 — Impact Analysis

- **No PRD / epic / MVP impact.** No requirement changes.
- **Diagnostic-process impact (the real lesson):** a runtime `TaskCanceledException` at a UI HTTP boundary was diagnosed twice (D13, D14) by inspecting only the UI layer. The Aspire **distributed trace** localised it in one read. Trace-first diagnosis should precede any transport hypothesis.
- **Observability gap (the durable defect to fix):** the existing `DaprSidecarHealthCheck` calls `DaprClient.CheckHealthAsync` (`/healthz`), which returns **healthy even when actor placement is down**. The outage was therefore invisible in the dashboard; it only manifested as opaque client-side timeouts.
- **Affected artifacts:**
  - `src/Hexalith.EventStore/HealthChecks/*` — new placement health check (this proposal).
  - `docs/guides/troubleshooting-dapr-actor-placement.md` — new runbook (this proposal).
  - `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-25-sample-ui-dapr-invocation.md` — annotated (this proposal).

---

## Section 3 — Recommended Approach

**Direct adjustment.** The trigger is fixed operationally (restart the control plane). The durable engineering changes make the failure *self-evident* next time:

1. **Server-side actor-placement health check** so a placement outage shows as **Unhealthy** in the Aspire dashboard / `/health` instead of silent actor timeouts.
2. **Runbook** documenting the `error reading server preface: EOF` signature and the restart/`dapr init` fix.
3. **Keep D13/D14** (DAPR transport is a reasonable consistency end-state) but **annotate** D14 that it did not address the root cause.

Rollback and MVP-review options are not applicable.

---

## Section 4 — Detailed Change Proposals

### 4.1 — Actor-placement health check (implemented)

**New files** under `src/Hexalith.EventStore/HealthChecks/`:

- `IDaprActorPlacementProbe.cs` — `IDaprActorPlacementProbe` + `DaprActorPlacementStatus(MetadataReachable, HostReady, Placement, RuntimeStatus)`.
- `DaprActorPlacementProbe.cs` — reads the local sidecar's raw `/v1.0/metadata` (the typed `DaprClient` metadata does **not** expose `actorRuntime.hostReady`/`placement` in Dapr.Client 1.17.9) and parses the `actorRuntime` section. Endpoint resolved from `DAPR_HTTP_ENDPOINT`/`DAPR_HTTP_PORT`.
- `DaprActorPlacementHealthCheck.cs` — `Unhealthy` when `hostReady == false`, with an operator-facing message ("Actor invocations … will hang until timeout. Check the DAPR placement service.").

**Modified:** `HealthCheckBuilderExtensions.cs` registers the typed probe HttpClient and adds the `dapr-actor-placement` check (`Unhealthy`, tag `ready`, 3 s timeout) alongside the existing DAPR checks.

**Tests:** `DaprActorPlacementHealthCheckTests` (probe mocked — healthy/unhealthy/throws/CT/ctor-null) and `DaprActorPlacementProbeTests` (real metadata JSON parsing — connected/disconnected/missing-section/HTTP-error/ctor-null). 11/11 green; solution builds under `TreatWarningsAsErrors`.

### 4.2 — Troubleshooting runbook (implemented)

**New:** `docs/guides/troubleshooting-dapr-actor-placement.md` — symptom (`TaskCanceledException` on query/command; actor spans hang to timeout), diagnosis (sidecar log `error reading server preface: EOF`; `/v1.0/metadata` → `actorRuntime.hostReady=false`), and fix (`docker restart dapr_placement dapr_scheduler`; escalate to `dapr uninstall --all && dapr init`).

### 4.3 — D14 annotation (implemented)

A correction note prepended to the D14 proposal: transport migration retained for consistency, but the root cause was the placement outage documented here.

### 4.4 — Not in scope / deliberately not done

- **No revert of D13/D14.** The transport change is consistent with the rest of the topology and harmless.
- **No change to the actors or the resilience handler.** They behaved correctly given an unreachable placement service.

---

## Section 5 — Implementation Handoff

- **Scope:** Moderate. Code change is localised and reviewed-test-covered; the trigger fix is operational.
- **Recipient:** Developer agent (done in this session) → senior review.
- **Success criteria:**
  1. With placement down, `/health` (ready) reports `dapr-actor-placement` **Unhealthy** with the actionable message (instead of all-green).
  2. With placement up, the check is Healthy and the counter query returns ~0.2 s.
  3. Solution builds with `TreatWarningsAsErrors`; new Tier-1 tests pass (11/11 verified).
  4. Runbook discoverable under `docs/guides/`.
- **Operational note for the team:** when any query/command intermittently times out under `aspire run`, check the EventStore sidecar log for `error reading server preface: EOF` before suspecting application code.
