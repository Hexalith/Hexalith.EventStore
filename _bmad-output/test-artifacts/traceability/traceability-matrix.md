---
stepsCompleted: ['step-01-load-context', 'step-02-discover-tests', 'step-03-map-criteria', 'step-04-analyze-gaps', 'step-05-gate-decision']
lastStep: 'step-05-gate-decision'
lastSaved: '2026-05-04'
scope: 'Post-Epic Deferred Work Cleanup — DW1 through DW5'
tempCoverageMatrixPath: '_bmad-output/test-artifacts/traceability/.tmp-coverage-matrix-2026-05-04.json'
gateDecision: 'FAIL'
gateBasis: 'priority_thresholds'
coverageBasis: 'acceptance_criteria'
oracleResolutionMode: 'formal_requirements'
oracleConfidence: 'high'
oracleSources:
  - '_bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md'
  - '_bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md'
  - '_bmad-output/implementation-artifacts/post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening.md'
  - '_bmad-output/implementation-artifacts/post-epic-deferred-dw4-operational-evidence-schema-validation.md'
  - '_bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md'
externalPointerStatus: 'not_used'
---

# Traceability Matrix — Post-Epic Deferred Work (DW1–DW5)

## Step 1 — Coverage Oracle & Knowledge Base

### Resolved Coverage Oracle

**Mode:** `formal_requirements` (highest-priority oracle source).
**Basis:** `acceptance_criteria` — every DW story file declares numbered, testable ACs.
**Confidence:** `high`. The five stories were drafted from the 2026-05-04 deferred-work triage proposal and tightened through party-mode review (DW1, DW2). All five carry status `ready-for-dev` in `_bmad-output/implementation-artifacts/sprint-status.yaml`. **No implementation has shipped yet** — this trace measures readiness against the intended target, not delivered code.
**External pointer status:** `not_used` — every requirement is in-repo.

### Story Inventory

| Story key | File | Status | ACs | Theme |
|-----------|------|:-----:|:---:|-------|
| DW1 | `post-epic-deferred-dw1-projection-and-drain-hardening.md` | ready-for-dev | 13 | Projection orchestrator, checkpoint tracker, drain reminder, activity reason codes |
| DW2 | `post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md` | ready-for-dev | 15 | Aspire baseline, Admin DAPR controllers, Epic 20 debugging, MCP startup/tools, NFR43 latency |
| DW3 | `post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening.md` | ready-for-dev | 14 | `DeepMerge`/`JsonDiff`, `GetEventsAsync(0)`, max-param bounds, `[AllowAnonymous]` trust note |
| DW4 | `post-epic-deferred-dw4-operational-evidence-schema-validation.md` | ready-for-dev | 15 | Validator for query + SignalR evidence templates, CI integration, samples |
| DW5 | `post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md` | ready-for-dev | 15 | TypeCatalog navigation, Ctrl+B sidebar, dialog `aria-label` runtime evidence |
| **Total** | | | **72** | |

### Why This Oracle Was Selected

1. **Formal beats synthetic.** Each story has explicit, numbered, testable acceptance criteria (e.g. DW1.AC#1 `checkpoint drift`, DW3.AC#5 `direct CommandApi max bounds`). No need to descend to user-journey synthesis.
2. **Test areas are pre-declared.** Each story's *Implementation Inventory* table names the production files plus the candidate test projects (e.g. `ProjectionUpdateOrchestratorTests`, `EventDrainRecoveryTests`, `TypeCatalogPageTests`, `BrowserSmokeTests`). Step 2 can search those locations directly.
3. **Three story types coexist.** The trace has to handle a mixed-mode workload:
   - **DW1, DW3** — code-change stories (assertable via unit/integration tests).
   - **DW2** — runtime-evidence story (assertable via filed evidence artifacts under `_bmad-output/test-artifacts/post-epic-deferred-dw2.../`).
   - **DW4** — tooling story (assertable via positive/negative validator samples).
   - **DW5** — mixed code + runtime evidence (bUnit + Playwright + manual browser).
4. **Reviewer-found rework is the norm here.** Per CLAUDE.md "Code Review Process," every Epic 2 story produced at least one review-driven patch. DW stories will follow the same pattern; the trace must surface gaps that *would* trigger reviewer pushback.

### Knowledge Fragments Loaded

From `.claude/skills/bmad-testarch-trace/resources/knowledge/`:

- `test-priorities-matrix.md` — P0–P3 mapping, coverage targets, decision tree.
- `risk-governance.md` — score thresholds (≥6 mitigate, =9 block), gate engine, traceability matrix shape.
- `probability-impact.md` — probability×impact = score (1–9), action ladder DOCUMENT/MONITOR/MITIGATE/BLOCK.
- `test-quality.md` — DoD: deterministic, isolated, <300 lines, <1.5 min, explicit assertions, self-cleaning.
- `selective-testing.md` — tag strategy (@p0/@smoke/@regression), promotion rules, change-detection.

### Supporting Artifacts Located

- **Sprint change proposal:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md` (Proposals B–F = DW1–DW5).
- **Existing test projects** named in DW story inventories (presence to be verified in Step 2):
  - `tests/Hexalith.EventStore.Server.Tests/Projections/*.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`
  - `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ProjectionMalformedResponseE2ETests.cs`
  - `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ValidProjectionRoundTripE2ETests.cs`
  - `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs`
  - `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs`
  - `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`
  - `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs`
  - `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs`
  - `tests/Hexalith.EventStore.Admin.UI.E2E/BrowserSmokeTests.cs`
- **Templates referenced by DW4:**
  - `_bmad-output/test-artifacts/query-operational-evidence-template.md`
  - `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`
- **Evidence folders the stories require:**
  - `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/` (does not yet exist)
  - `_bmad-output/test-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups/` (does not yet exist)

### Pre-Trace Risk Snapshot (informational, refined in Step 4)

| Story | Initial risk read | Why |
|-------|------------------|-----|
| DW1 | **HIGH** (P0/P1) | Touches drain integrity (R4-A6), checkpoint non-regression (R11-A1), polling fairness (R11-A2). Silent regressions corrupt event delivery. |
| DW2 | **HIGH** (P0) | Pure live-evidence story; without artifacts, Admin/DAPR/MCP claims are unfalsifiable. |
| DW3 | **MED-HIGH** (P1) | JSON reconstruction is a diagnostic approximation; `[AllowAnonymous]` carries a trust-boundary debt. |
| DW4 | **MED** (P1) | Process/governance lever. Future evidence quality compounds on this. |
| DW5 | **MED** (P1) | User-visible bugs (Ctrl+B, /types nav) and accessibility runtime claims; small blast radius per item. |

---

_Next step: discover and catalog tests under each named area; classify by level (Unit / Integration / E2E / Component); build coverage heuristics inventory._

---

## Step 2 — Test Discovery & Catalog

### Test Project Inventory (DW-relevant projects only)

| Project | Tier | Test count probed | Relevance |
|---------|:----:|:----:|-----------|
| `tests/Hexalith.EventStore.Server.Tests/Projections/` | 1 | ~60+ across 9 files | DW1 projection orchestrator, checkpoint tracker, poller, KeyedSemaphore |
| `tests/Hexalith.EventStore.Server.Tests/Actors/` | 1 | ~120+ across 27 files | DW1 drain recovery (`EventDrainRecoveryTests` = 19), `UnpublishedEventsRecordTests` |
| `tests/Hexalith.EventStore.Server.Tests/Controllers/` | 1 | 12 in `AdminStreamQueryControllerTimelineTests` | DW3 only timeline path covered today |
| `tests/Hexalith.EventStore.IntegrationTests/ContractTests/` | 3 | 1 each in `ProjectionMalformedResponseE2ETests`, `ValidProjectionRoundTripE2ETests` | DW1 runtime projection / DW2 evidence-adjacent |
| `tests/Hexalith.EventStore.Admin.Server.Tests/` | 1 | ~50 files | DW2/DW3 facade & controllers (`AdminDaprController*`, `DaprInfrastructureQueryService`, `DaprStreamQueryService*`) |
| `tests/Hexalith.EventStore.Admin.Mcp.Tests/` | 1 | ~33 files | DW2 MCP tools & session |
| `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs` | 1 (bUnit) | 5 | DW5 layout — **no Ctrl+B coverage** |
| `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs` | 1 (bUnit) | 16 | DW5 TypeCatalog (49 keyword hits — existing UpdateUrl/deep-link tests) |
| `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs` | 1 (bUnit) | 10 | DW5 dialog — `PayloadDialog_CarriesAriaLabel` exists (markup only) |
| `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs` | 1 (bUnit) | 19 | DW5 dialog — `PayloadDialog_CarriesAriaLabel` exists (markup only) |
| `tests/Hexalith.EventStore.Admin.UI.E2E/BrowserSmokeTests.cs` | 3 (Playwright) | 5 | DW5 — no TypeCatalog/shortcut coverage |
| `_bmad-output/test-artifacts/post-epic-deferred-dw2.../` | — | **does not exist** | DW2 evidence target (P0) |
| `_bmad-output/test-artifacts/post-epic-deferred-dw5.../` | — | **does not exist** | DW5 evidence target |
| `_bmad-output/test-artifacts/operational-evidence-validator/` | — | **does not exist** | DW4 sample target |
| `scripts/validate-docs.ps1` / `.sh` | tooling | exists | DW4 host for new validator (currently has no schema validator) |

### High-Value Test Files (Stable IDs)

| ID | File:Line | Level | Tests | Skipped/Pending | DW relevance |
|----|-----------|:----:|:----:|:----:|--------------|
| **T-PROJ-01** | `ProjectionUpdateOrchestratorTests.cs` | Unit | ~24 | 0 | DW1 AC#1, AC#2, AC#3 (partial: cancellation@687, invalid response@405/434, write failure@461) |
| **T-PROJ-02** | `ProjectionUpdateOrchestratorRefreshIntervalTests.cs` | Unit | (in-file) | 0 | DW1 AC#4 polling/refresh |
| **T-PROJ-03** | `ProjectionCheckpointTrackerTests.cs` | Unit | 22 | 0 | DW1 AC#1, AC#5, AC#6 |
| **T-PROJ-04** | `ProjectionPollerServiceTests.cs` | Unit | 9 | 0 | DW1 AC#4, AC#5 |
| **T-PROJ-05** | `KeyedSemaphoreTests.cs` | Unit | 4 | 0 | DW1 AC#4 |
| **T-DRAIN-01** | `EventDrainRecoveryTests.cs` | Unit | 19 | 0 | DW1 AC#7, AC#8, AC#9 (no terminal/poison/backoff terms — 0 hits) |
| **T-DRAIN-02** | `UnpublishedEventsRecordTests.cs` | Unit | (in-file) | 0 | DW1 AC#7 record state |
| **T-ADMSQ-01** | `AdminStreamQueryControllerTimelineTests.cs` | Unit | 12 | 0 | DW3 timeline path only — no blame/bisect/step/sandbox/diff |
| **T-ADMTR-01** | `AdminTracesControllerTests.cs` (Admin.Server.Tests) | Unit | (in-file) | 0 | DW2 facade for trace map / DW3 scan-cap (cap is in CommandApi controller) |
| **T-ADMSV-01** | `DaprInfrastructureQueryServiceTests.cs` | Unit | (in-file) | 0 | DW2 AC#3 RemoteMetadataStatus parser tests (≠ live evidence) |
| **T-ADMSV-02** | `DaprStreamQueryServiceTests.cs`, `DaprStreamQueryServiceSandboxTests.cs` | Unit | (in-file) | 0 | DW2/DW3 facade behavior |
| **T-ADMSV-03** | `DaprHealthHistoryCollectorTests.cs`, `DaprHealthQueryServiceHistoryTests.cs` | Unit | (in-file) | 0 | DW2 AC#2 health-history parser |
| **T-ADMDR-01** | `AdminDaprControllerTests.cs`, `AdminDaprControllerActorTests.cs`, `AdminDaprControllerPubSubTests.cs`, `AdminDaprControllerResiliencyTests.cs` | Unit | (in-file) | 0 | DW2 AC#2 controller surface (no live runtime) |
| **T-MCP-01** | 33 `*Tools*Tests.cs` and `AdminApiClient*Tests.cs` files | Unit | many | unknown | DW2 AC#7–10 (no live `tools/list`, no NFR43 sample) |
| **T-UI-LAY-01** | `MainLayoutTests.cs` | Unit (bUnit) | 5 | 0 | DW5 AC#5/AC#6 — **0 Ctrl+B coverage** |
| **T-UI-TC-01** | `TypeCatalogPageTests.cs` | Unit (bUnit) | 16 | 0 | DW5 AC#3, AC#4 — UpdateUrl/deep-link guard rails (must remain green) |
| **T-UI-CS-01** | `CommandSandboxTests.cs::CommandSandbox_PayloadDialog_CarriesAriaLabel` | Unit (bUnit) | 1 of 10 | 0 | DW5 AC#8 (markup only, not runtime DOM) |
| **T-UI-ED-01** | `EventDebuggerTests.cs::EventDebugger_PayloadDialog_CarriesAriaLabel` | Unit (bUnit) | 1 of 19 | 0 | DW5 AC#9 (markup only, not runtime DOM) |
| **T-E2E-UI-01** | `BrowserSmokeTests.cs` | E2E (Playwright) | 5 | 0 | DW5 AC#2, #5, #7 — **no TypeCatalog nav or shortcut tests** |
| **T-E2E-PROJ-01** | `ProjectionMalformedResponseE2ETests.cs` | E2E (Aspire/Tier 3) | 1 | 0 | DW1 AC#2 single-case malformed proof |
| **T-E2E-PROJ-02** | `ValidProjectionRoundTripE2ETests.cs` | E2E (Aspire/Tier 3) | 1 | 0 | DW1 AC#1, #4 round-trip proof |
| **T-E2E-PUBSUB** | `PubSubDeliveryProofTests.cs` | E2E | (in-file) | 0 | DW1 adjacent — pubsub delivery |
| **T-E2E-SR** | `SignalRRedisBackplaneRuntimeProofTests.cs` | E2E | (in-file) | 0 | DW2/DW4 SignalR evidence pattern source |

> **Note:** The trace records discovered tests with stable IDs (file:method names), not opaque numeric IDs, because the codebase has no machine-parseable `[Trait("AC", "DW1-3")]` attributes — every binding is by file+method+keyword inspection.

### Tests Conspicuously Absent

| Missing test class | Why expected | Affected ACs |
|--------------------|-------------|-------------|
| `ProjectionCheckpointDriftTests` (or method on T-PROJ-03) | AC#1 explicit `LastDeliveredSequence > current sequence` scenario | DW1.AC#1 |
| `/project` per-failure-class tests with stable reason codes | AC#2 codes `project_upstream_4xx`, `…_5xx`, `…_unsupported_content_type`, `…_invalid_charset`, `…_malformed_json`, `…_invalid_projection_type`, `…_invalid_state`, `…_timeout`, `…_cancelled` (0 hits in `tests/`) | DW1.AC#2, AC#10 |
| Same-aggregate overlap regression | AC#4 explicit overlap, not sequential repeats | DW1.AC#4 |
| Tracker scope-index / identity-index corruption tests | AC#5 corruption disposition codes (`tracker_corrupt_scope_index`, `tracker_corrupt_identity_index`) — 0 hits | DW1.AC#5 |
| Drain terminal/poison disposition test (terminal/poison/backoff/disposition keywords = 0 hits in `EventDrainRecoveryTests`) | AC#7 terminal disposition or accepted-debt decision | DW1.AC#7 |
| Drain activity tag stable reason codes (`drain_event_count_mismatch`, `drain_publish_failed`, `drain_terminal_failure` — 0 hits) | AC#8 bounded activity tags | DW1.AC#8 |
| `MainLayout` Ctrl+B / `OnToggleSidebarShortcut` test | AC#5 sidebar toggle without console error / circuit exception | DW5.AC#5 |
| `MainLayout` viewport-tier storage key test (`hexalith-sidebar-collapsed-{tier}`) | AC#6 persistence | DW5.AC#6 |
| Browser-level TypeCatalog `/types?tab=*` → sidebar nav test | AC#2 reproduce/close navigation block | DW5.AC#2 |
| Browser-level Ctrl+B + Ctrl+K co-existence test | AC#5, #7 in same browser session | DW5.AC#5, AC#7 |
| Runtime DOM evidence (Playwright) for `aria-label` on `<fluent-dialog>` | AC#8, AC#9 — not bUnit | DW5.AC#8, AC#9 |
| `AdminStreamQueryControllerTests` for blame, bisect, step, sandbox, diff JSON behavior | AC#2, #3, #4 — only timeline path covered today | DW3.AC#2, AC#3, AC#4 |
| Direct CommandApi max-parameter bounds tests | AC#5 zero / negative / huge `maxEvents`/`maxFields`/`maxSteps` | DW3.AC#5 |
| Trace-map `ScanCapped` honesty tests (when expected count unknown) | AC#8 scan cap not falsely reported | DW3.AC#8 |
| `[AllowAnonymous]` trust-boundary architecture note + supporting test | AC#9 documentation contract | DW3.AC#9 |
| Operational-evidence validator (positive + negative samples for both `query-operational-evidence/v1` and `signalr-operational-evidence/v1`) | AC#1–13 — directory does not exist | DW4 entire surface |
| DW4 CI integration in `validate-docs.ps1` / `.sh` and `docs-validation.yml` | AC#10 — no validator wired | DW4.AC#10 |
| Live MCP evidence (initialize, tools/list, read tool, write-preview, session fallback, NFR43 latency sample) | DW2 AC#7–10 — only Tier 1 unit-test coverage | DW2.AC#7, AC#8, AC#9, AC#10 |
| Aspire runtime baseline + Admin DAPR live-evidence index | DW2 AC#1, #2, #4, #11 — folder does not exist | DW2 entire surface |
| Epic 20 debugging seeded-stream live evidence (blame/bisect/step/sandbox/trace-map against same correlation id) | DW2 AC#5, #6 | DW2.AC#5, AC#6 |

### Coverage Heuristics Inventory (per Step 2.3)

#### API Endpoint Coverage

| Endpoint | Story | Tests targeting it | Coverage |
|----------|-------|---------------------|----------|
| `GET /api/v1/admin/dapr/components` | DW2 AC#2 | `AdminDaprControllerTests` (Tier 1 unit) | **API smoke** — no live evidence |
| `GET /api/v1/admin/dapr/sidecar` | DW2 AC#2, AC#3 | `AdminDaprControllerTests`, `DaprInfrastructureQueryServiceTests` | **Parser-tested only** — no `RemoteMetadataStatus` matrix evidence |
| `GET /api/v1/admin/dapr/actors` | DW2 AC#2, AC#3 | `AdminDaprControllerActorTests`, `DaprActorQueryServiceTests` | **Parser-tested only** |
| `GET /api/v1/admin/dapr/pubsub` | DW2 AC#2, AC#3 | `AdminDaprControllerPubSubTests`, `DaprPubSubQueryServiceTests` | **Parser-tested only** |
| `GET /api/v1/admin/dapr/resiliency` | DW2 AC#2, AC#4 | `AdminDaprControllerResiliencyTests`, `DaprResiliencyQueryServiceTests` | **Parser-tested only** |
| `GET /api/v1/admin/health/history` | DW2 AC#2 | `AdminHealthControllerHistoryTests`, `DaprHealthQueryServiceHistoryTests` | **Parser-tested only** |
| `AdminStreamQueryController.GetAggregateBlameAsync` (CommandApi `[AllowAnonymous]`) | DW3 AC#5, #7, #9 | none in `tests/` (0 in `Server.Tests/Controllers/`) | **❌ uncovered** |
| `AdminStreamQueryController.BisectAggregateStateAsync` | DW3 AC#5, #7, #9 | none | **❌ uncovered** |
| `AdminStreamQueryController.GetEventStepFrameAsync` | DW3 AC#5, #7, #9 | none | **❌ uncovered** |
| `AdminStreamQueryController.SandboxCommandAsync` | DW3 AC#5, #7, #9 | none direct (facade has `AdminStreamsControllerSandboxTests`) | **❌ direct path uncovered** |
| `AdminTraceQueryController` (CommandApi) `[AllowAnonymous]` | DW3 AC#8, #9 | facade only (`AdminTracesControllerTests`) | **❌ direct trust-boundary path uncovered** |
| `/project` upstream call (Dapr service-invocation) | DW1 AC#2 | `ProjectionUpdateOrchestratorTests` (lines 405, 434, 461) covers invalid response/null state/actor write fail; **does not exercise charset/content-type/4xx/5xx/timeout distinct codes** | **Partial — happy + 3 invalid paths only** |

#### Authentication / Authorization Coverage

| Concern | Tests | Status |
|---------|-------|--------|
| `[AllowAnonymous]` on `AdminStreamQueryController` / `AdminTraceQueryController` (DW3 AC#9) | none assert this is intentional / behind DAPR boundary | **❌ no negative auth test, no architecture note backing the trust boundary** |
| Admin facade (Admin.Server) bearer token forwarding to CommandApi | `AdminAuthorizationIntegrationTests`, `JwtAuthenticatedWebApplicationFactory` | **Covered for happy path** |
| Ctrl+B circuit safety (renderer context) — DW5 AC#5 | none | **❌ uncovered** |

#### Error-Path Coverage

| Concern | Tests | Status |
|---------|-------|--------|
| `/project` 4xx upstream → distinct reason code | none | **❌ uncovered** |
| `/project` 5xx upstream → distinct reason code | none | **❌ uncovered** |
| `/project` unsupported content-type | none | **❌ uncovered** |
| `/project` invalid charset | none | **❌ uncovered** |
| `/project` malformed JSON | none with stable code | **❌ uncovered** |
| `/project` host cancellation vs. service timeout | T-PROJ-01 line 686 (`OperationCanceledException_PropagatesThroughOuterCatch`) covers cancellation; no inner-timeout class | **Partial** |
| Drain `EventCount` mismatch | `EventDrainRecoveryTests` `[Theory]` lines 412/447 cover happy + retry; no terminal disposition | **Partial — failure classified, terminal not chosen** |
| Drain partial publish prevention | `FullDrainCycle_*` tests at 678, 715, 744, 778 cover normal failure recovery; no integrity-failure pending-counter check | **Partial** |
| Tracker scope-index / identity-index corruption (DW1 AC#5) | `corrupt` keyword matches `ProjectionCheckpointTrackerTests.cs` only generically | **❌ unconfirmed; assume uncovered without explicit AC binding** |
| TypeCatalog navigation block (DW5 AC#2) | none | **❌ uncovered** |
| Direct CommandApi extreme `maxEvents`/`maxFields`/`maxSteps` (DW3 AC#5) | none | **❌ uncovered** |
| Trace-map `ScanCapped` honesty when expected count unknown (DW3 AC#8) | none | **❌ uncovered** |
| Operational evidence schema violations (DW4 AC#2, #3, #5, #6) | none | **❌ no validator exists** |

#### UI Journey Coverage

| Journey | Tests | Status |
|---------|-------|--------|
| `/types` deep-link → tab init | `TypeCatalogPageTests` (16 tests, 49 keyword hits) | **Covered (must remain green per DW5 AC#4)** |
| `/types?tab=*` click sidebar nav 3+ links → URL/page change | none | **❌ uncovered (DW5 AC#2)** |
| Ctrl+K command palette open/close/reopen | `CommandPaletteTests` exists | **Covered (Story 21-13)** |
| Ctrl+B sidebar collapse/expand + persistence per viewport tier | none | **❌ uncovered (DW5 AC#5, AC#6)** |
| Ctrl+B + Ctrl+K co-existence in same session | none | **❌ uncovered (DW5 AC#7)** |
| CommandSandbox dialog `aria-label` rendered DOM | bUnit markup test only (T-UI-CS-01) | **Partial — needs runtime DOM evidence** |
| EventDebugger dialog `aria-label` rendered DOM | bUnit markup test only (T-UI-ED-01) | **Partial — needs runtime DOM evidence** |
| Aspire runtime baseline (DW2 AC#1) | none | **❌ uncovered (live evidence)** |
| Epic 20 debugging tools live smoke (blame/bisect/step/sandbox/trace-map) (DW2 AC#5) | none | **❌ uncovered (live evidence)** |
| MCP `tools/list` + representative read + write-preview + session fallback (DW2 AC#7–9) | unit tools tests only | **❌ live MCP transcript missing** |

#### UI State Coverage

| State | Tests | Status |
|-------|-------|--------|
| TypeCatalog empty / loading / error | covered in `TypeCatalogPageTests` and `EmptyStateTests` | **Covered** |
| TypeCatalog navigation-blocked state | none | **❌ uncovered** |
| MainLayout sidebar collapsed/expanded for each viewport tier | none | **❌ uncovered** |
| CommandSandbox / EventDebugger dialog states (open/closed/error/timeout) | covered via existing `CommandSandbox_Shows*` and `EventDebugger_Shows*` tests | **Covered for non-AT states** |

### Discovery Summary

- **3 of 5 stories** (DW2, DW4, DW5 partial) require **artifact directories that do not yet exist** — those branches of the trace are 0% covered no matter how many unit tests pass.
- **DW1** has the strongest existing scaffold (~24 orchestrator tests, 22 tracker tests, 19 drain tests) but **0 tests reference the stable diagnostic vocabulary** the story mandates (per AC#2 and AC#8 reason codes).
- **DW3** has only 12 tests on the CommandApi `AdminStreamQueryController` (timeline path) — blame, bisect, step-through, sandbox, diff are all CommandApi-side untested; only the Admin.Server facade has any sandbox coverage (`AdminStreamsControllerSandboxTests`, `DaprStreamQueryServiceSandboxTests`).
- **DW5 AC#5–7** (Ctrl+B sidebar toggle, viewport persistence, Ctrl+K co-existence) is the most acutely uncovered code-level area in the cleanup package — `MainLayoutTests.cs` has zero shortcut tests.

---

_Next step: map every acceptance criterion (72 ACs) to its test ID(s) and produce the criterion-level coverage matrix._

---

## Step 3 — Acceptance Criterion → Test Mapping

**Coverage status legend**

- **FULL** — directly covered with both happy and unhappy paths at the appropriate level.
- **PARTIAL** — covered for one path / one level only; story explicitly demands more (e.g. multiple distinct reason codes, runtime DOM, live evidence).
- **UNIT-ONLY** — bUnit / xUnit covers the markup or method, but the AC requires runtime/browser/integration evidence that does not exist.
- **INTEGRATION-ONLY** — Tier 2/3 covers it, but unit-level regression guard is also required.
- **NONE** — no test currently asserts this criterion.
- **EVIDENCE-ONLY** — AC is satisfied by an artifact (markdown/screenshot/transcript), not test code; status reflects whether the artifact exists.
- **TOOLING** — AC is implemented as a script / validator and the test for it is the validator's own positive/negative samples.

**Priority assignment** follows `test-priorities-matrix.md` + `risk-governance.md`. Code-correctness ACs that affect event-store integrity, projection delivery, or drain recovery → P0. User-visible runtime defects, live evidence stories, and trust-boundary documentation → P1. Bookkeeping / scope-boundary ACs → P2/P3.

### DW1 — Projection and Drain Hardening (13 ACs)

| AC | Title (short) | Priority | Bound test IDs | Coverage | Notes |
|:--:|--------------|:--------:|----------------|:--------:|-------|
| 1 | Checkpoint drift handled (`LastDeliveredSequence > current sequence`) | P0 | T-PROJ-01 (~4 invalid-response tests cover write/checkpoint non-regression but **not** the drift scenario specifically) | **PARTIAL** | No test forces `checkpoint > stream max`; reason code `checkpoint_drift` not asserted |
| 2 | `/project` failures classified with stable codes (4xx, 5xx, content-type, charset, malformed JSON, null `ProjectionType`, null state) | P0 | T-PROJ-01 lines 405/434 cover invalid response + null state; T-E2E-PROJ-01 covers one malformed runtime case | **PARTIAL** | 0 hits for `project_upstream_4xx`, `project_unsupported_content_type`, `project_invalid_charset`, `project_malformed_json`, `project_invalid_projection_type`, `project_invalid_state`, `project_timeout`, `project_cancelled`. Story demands ≥9 stable categories |
| 3 | Cancellation vs. timeout separated | P0 | T-PROJ-01 line 686 (`OperationCanceledException_PropagatesThroughOuterCatch`) | **PARTIAL** | Cancellation covered; inner-timeout-as-transient-failure path not separately classified |
| 4 | Per-aggregate projection serialization non-regressive (KeyedSemaphore, no global lock) | P0 | T-PROJ-05 (4 KeyedSemaphore tests), T-PROJ-01 line 248 (`RepeatTriggersOnSameAggregate_ProducesIdenticalProjectionState`) | **PARTIAL** | Repeat-trigger test exists; **explicit overlap regression** (concurrent triggers on same `ActorId`) not asserted |
| 5 | Tracker enumeration corruption bounded (scope-index, identity-index, page) | P0 | T-PROJ-03 (22 tests; generic `corrupt` keyword in file but no AC-bound corruption disposition tests) | **NONE** | No test for `tracker_corrupt_scope_index` / `tracker_corrupt_identity_index` / `tracker_terminal_failure` |
| 6 | Tracker scaling documented; no broad caching layer | P2 | T-PROJ-03 + T-PROJ-04 cover existing page-100/ETag behavior | **PARTIAL** | Documentation AC, not directly testable; current behavior tests act as guard |
| 7 | Drain poison/terminal disposition decided | P0 | T-DRAIN-01 (19 tests cover happy + retry + integrity-mismatch); 0 hits for `terminal`, `poison`, `backoff`, `disposition` | **NONE** | Decision-AC: must be implemented or explicitly accepted-debt before close |
| 8 | Drain activity tags use bounded stable reason codes (`drain_event_count_mismatch`, `drain_publish_failed`, `drain_terminal_failure`) | P1 | T-DRAIN-01 covers failure paths but does not assert stable code names | **NONE** | 0 hits for any DW1 drain reason-code identifier |
| 9 | Drain reminder re-entrancy proof | P0 | T-DRAIN-01 (`ReceiveReminder_*` tests verify single-call behavior) | **PARTIAL** | Existing tests assume single execution; no concurrent-reminder regression test, only Dapr behavior assumption |
| 10 | EventId / signal collisions not made worse (11xx range) | P2 | none | **NONE** | Convention check; needs review-time assertion or local search test |
| 11 | Tests cover production behavior, not helper internals | P1 | All T-PROJ-* and T-DRAIN-* are behavior-style (state-store / record-side-effect) | **PARTIAL** | Existing tests qualify; new ACs (#5, #7, #8) must follow same shape |
| 12 | Scope boundaries (no admin endpoints, no contract changes, no Dapr YAML changes) | P2 | none directly; absence test = git diff | **NONE** | Policy AC; gate-validated, not test-validated |
| 13 | Bookkeeping closed (Dev Agent Record, File List, Change Log, Verification Status) | P3 | none | **NONE** | Process AC; story workflow gate |

### DW2 — Admin DAPR + MCP Live Evidence (15 ACs)

> All ACs require **live runtime evidence artifacts** under `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/` — that directory **does not exist**. Tier 1 unit tests (parsers, controller models, MCP tool unit tests) are pre-existing scaffolding; they do not satisfy DW2's evidence ACs.

| AC | Title (short) | Priority | Bound test IDs / artifacts | Coverage | Notes |
|:--:|--------------|:--------:|----------------------------|:--------:|-------|
| 1 | Aspire runtime baseline captured (commands, env, URLs, resource states, blockers) | P0 | none — folder absent | **NONE** | Live evidence required |
| 2 | Admin DAPR runtime surface: components, sidecar, actors, pub/sub, resiliency, health-history | P0 | T-ADMDR-01 (4 controller test files), T-ADMSV-01 (parser tests) — Tier 1 only | **PARTIAL** (parser-level) | Story explicitly bars treating parser pass as live proof |
| 3 | Remote sidecar metadata `RemoteMetadataStatus` matrix (sidecar, actors, pub/sub) | P0 | T-ADMSV-01 covers parser branches; no live `Available` / `Unreachable` / `NotConfigured` row evidence | **PARTIAL** | Per-surface matrix evidence missing |
| 4 | Degraded states preserved (empty, parse-error, timeout, 4xx/5xx) | P1 | T-ADMSV-01 + T-ADMDR-01 cover degraded model output | **PARTIAL** | Live degraded-state observation absent |
| 5 | Epic 20 debugging tools smoke-tested on one seeded stream (blame, bisect, step, sandbox, trace-map) | P0 | none — no seeded-stream evidence | **NONE** | Live evidence required |
| 6 | Debugging evidence respects scope (no DW3 patches sneaking in) | P1 | none | **NONE** | Process gate AC |
| 7 | MCP startup + initialize + `tools/list` + stderr separation | P0 | T-MCP-01 unit tests for `ServerToolsTests`, `DiagnosticToolsTests`, etc. | **PARTIAL** (unit-level only) | No live `tools/list` transcript |
| 8 | Representative MCP read tool + approval-gated write-preview tool exercised | P0 | T-MCP-01 covers tool unit behavior; no live JSON-RPC transcript | **PARTIAL** (unit-level only) | Live no-mutation proof missing |
| 9 | MCP session fallback for tenant/domain context | P1 | T-MCP-01 `SessionContextIntegrationTests`, `SessionDependencyInjectionTests`, `InvestigationSessionTests` | **PARTIAL** (unit-level only) | Live fallback sequence not recorded |
| 10 | NFR43 MCP single-resource latency plan + ≥1 measured sample | P1 | none | **NONE** | Latency sample required |
| 11 | Evidence artifacts durable + reviewable (index + tables) | P0 | none | **NONE** | Folder absent |
| 12 | `deferred-work.md` dispositions updated narrowly with `STORY:` markers | P2 | none | **NONE** | Bookkeeping AC |
| 13 | Security & redaction preserved (no tokens, no payloads) | P1 | none | **NONE** | Cross-cuts every artifact |
| 14 | No deployment topology / contract changes from evidence work | P2 | none | **NONE** | Scope-boundary AC |
| 15 | Bookkeeping closed | P3 | none | **NONE** | Process AC |

### DW3 — Admin Debugging JSON & Large-Stream Hardening (14 ACs)

| AC | Title (short) | Priority | Bound test IDs | Coverage | Notes |
|:--:|--------------|:--------:|----------------|:--------:|-------|
| 1 | JSON reconstruction semantics documented as architecture decision | P1 | none | **NONE** | Documentation AC; needs `docs/operations/` or `docs/concepts/` note |
| 2 | Deletion + nested-removal behavior tested or accepted | P1 | T-ADMSQ-01 (12 tests, timeline only); 0 `DeepMerge`/`JsonDiff`/`FlattenJson`/`ReconstructState` hits in `tests/` | **NONE** | Helpers wholly untested |
| 3 | Array treatment bounded + visible | P1 | none | **NONE** | Same as AC#2 |
| 4 | Recursion + malformed-path failure modes guarded | P0 | none | **NONE** | StackOverflow/ArgumentException risk uncovered |
| 5 | Direct CommandApi `maxEvents`/`maxFields`/`maxSteps`/`count` bounded | P0 | none on direct CommandApi controllers; facade `AdminServerOptions` bounds tested | **NONE** | Direct path explicitly unprotected per AC text |
| 6 | Large-stream behavior explicit per debugging surface (truncation flag / 400 / debt) | P1 | none | **NONE** | Per-surface matrix doesn't exist |
| 7 | `GetEventsAsync(0)` usages dispositioned per endpoint | P1 | none | **NONE** | Decision AC |
| 8 | Trace-map `ScanCapped` honesty when expected count unknown | P1 | none | **NONE** | Existing `ScanCapped` only set when expected count known |
| 9 | Internal admin trust boundary documented (`[AllowAnonymous]` + DAPR isolation) | P1 | none | **NONE** | Architecture-note AC |
| 10 | Facade + MCP + Admin UI + CLI compatibility (response shapes, route names) | P1 | T-ADMSQ-01 + Admin.Server controller tests + UI client tests can guard via existing tests | **PARTIAL** | Existing tests guard contract; new error responses untested |
| 11 | Tests cover helper + endpoint behavior | P1 | T-ADMSQ-01 = 12 (timeline only); helpers = 0 | **NONE** | Story explicitly names `AdminStreamQueryController` JSON merge/diff/reconstruction tests |
| 12 | `deferred-work.md` dispositions narrow | P2 | none | **NONE** | Bookkeeping |
| 13 | Scope boundaries (no JSON Patch engine, no actor API rewrite) | P2 | none | **NONE** | Policy |
| 14 | Bookkeeping closed | P3 | none | **NONE** | Process |

### DW4 — Operational Evidence Schema Validation (15 ACs)

> Validator does not exist. There is no `_bmad-output/test-artifacts/operational-evidence-validator/` directory. `scripts/validate-docs.{ps1,sh}` exists but has no schema validator wired in. Every AC is currently **NONE / TOOLING** until the validator is built.

| AC | Title (short) | Priority | Bound test IDs | Coverage | Notes |
|:--:|--------------|:--------:|----------------|:--------:|-------|
| 1 | Validator scope explicit (`query-operational-evidence/v1`, `signalr-operational-evidence/v1`) | P1 | none | **NONE** | Templates exist; validator does not |
| 2 | Required-field enforcement mechanical | P1 | none | **NONE** | TOOLING — proven via positive/negative samples |
| 3 | Placeholder detection strict (`<required>`, template row labels, empty cells) | P1 | none | **NONE** | TOOLING |
| 4 | Classification taxonomy single-source + drift-resistant | P1 | none | **NONE** | Docs alignment AC |
| 5 | Redaction rules validated (tokens, connection strings, hostnames, secrets) | P0 | none | **NONE** | Security AC |
| 6 | Controls + correlation checks first-class | P1 | none | **NONE** | Validator AC |
| 7 | Aspire-specific evidence optional / profile-scoped | P1 | none | **NONE** | Validator AC |
| 8 | Positive + negative samples prove validator (≥1 valid + ≥1 invalid for each schema) | P1 | none | **NONE** | TOOLING — required artifacts absent |
| 9 | Implementation fits repo toolchain (script / .NET / JSON Schema + lint companion) | P1 | none | **NONE** | Implementation choice AC |
| 10 | CI integration deliberate (or deferred with reason) | P1 | none | **NONE** | `validate-docs.{ps1,sh}` exists but unwired |
| 11 | Existing evidence not mass-rewritten | P2 | none | **NONE** | Scope-boundary AC |
| 12 | `deferred-work.md` dispositions narrow | P2 | none | **NONE** | Bookkeeping |
| 13 | Validator output useful (file path + schema + rule + remediation) | P1 | none | **NONE** | TOOLING |
| 14 | Scope boundaries (no telemetry, no perf harness, no contract changes) | P2 | none | **NONE** | Policy |
| 15 | Bookkeeping closed | P3 | none | **NONE** | Process |

### DW5 — Admin UI Runtime Follow-Ups (15 ACs)

| AC | Title (short) | Priority | Bound test IDs | Coverage | Notes |
|:--:|--------------|:--------:|----------------|:--------:|-------|
| 1 | DW5 baselined from real deferred entries with classification | P2 | none | **NONE** | Process AC |
| 2 | TypeCatalog `/types?tab=*` sidebar nav block reproduced or closed | P0 | none — no browser test for nav-from-/types | **NONE** | Live evidence required |
| 3 | TypeCatalog render-loop hypotheses tested before broad rewrite | P1 | T-UI-TC-01 (16 tests guard URL/deep-link behavior) | **PARTIAL** | Guard tests exist; hypothesis-isolation tests do not |
| 4 | TypeCatalog URL + selection non-regressive (`/types`, `?tab=commands`, `?tab=aggregates`, `type=` selection) | P0 | T-UI-TC-01 (49 keyword hits, 16 tests cover UpdateUrl + deep-links) | **FULL** | Existing coverage explicitly named in AC; must remain green |
| 5 | Ctrl+B sidebar toggle works on Blazor renderer context | P0 | none in `MainLayoutTests.cs` (5 tests, 0 Ctrl+B / OnToggleSidebarShortcut hits) | **NONE** | Hardest acutely-uncovered code-level AC in package |
| 6 | Ctrl+B persistence viewport-scoped (`hexalith-sidebar-collapsed-{tier}`) | P0 | none | **NONE** | Storage-key behavior untested |
| 7 | Ctrl+K command palette non-regressive | P1 | `CommandPaletteTests` exists; no co-existence test with Ctrl+B | **PARTIAL** | Solo behavior covered; combined-session test missing |
| 8 | CommandSandbox dialog `aria-label` runtime/AT evidence | P1 | T-UI-CS-01 (markup-only bUnit assertion `CommandSandbox_PayloadDialog_CarriesAriaLabel`) | **UNIT-ONLY** | Story explicitly bars markup-only proof for AT verification |
| 9 | EventDebugger dialog `aria-label` runtime/AT evidence | P1 | T-UI-ED-01 (markup-only bUnit assertion `EventDebugger_PayloadDialog_CarriesAriaLabel`) | **UNIT-ONLY** | Same as AC#8 |
| 10 | Runtime evidence artifacts durable | P1 | none | **NONE** | Folder absent |
| 11 | Smallest useful mix of bUnit + browser coverage | P1 | T-UI-LAY-01, T-UI-TC-01, T-UI-CS-01, T-UI-ED-01 cover bUnit; T-E2E-UI-01 (5 tests, no DW5 paths) | **PARTIAL** | Browser tests do not exercise DW5 scenarios |
| 12 | Fluent UI Blazor v5 component behavior respected | P1 | existing `FluentDialog`/`FluentTabs`/`FluentDataGrid` usage in tests | **PARTIAL** | Existing pattern check; v5 invariants only enforced via build |
| 13 | Deferred-work dispositions narrow | P2 | none | **NONE** | Bookkeeping |
| 14 | Scope boundaries (no Admin API contract, Dapr YAML, query/SignalR semantic changes) | P2 | none | **NONE** | Policy |
| 15 | Bookkeeping closed | P3 | none | **NONE** | Process |

### Coverage Tally

| Story | ACs | FULL | PARTIAL | UNIT-ONLY | NONE | Coverage rate (FULL/total) |
|-------|:---:|:----:|:-------:|:---------:|:----:|:---:|
| DW1 | 13 | 0 | 6 | 0 | 7 | **0%** |
| DW2 | 15 | 0 | 7 | 0 | 8 | **0%** |
| DW3 | 14 | 0 | 1 | 0 | 13 | **0%** |
| DW4 | 15 | 0 | 0 | 0 | 15 | **0%** |
| DW5 | 15 | 1 | 4 | 2 | 8 | **7%** |
| **Total** | **72** | **1** | **18** | **2** | **51** | **1.4%** |

### Coverage-Logic Validation Notes

- **P0 items without coverage:** DW1.AC#5, DW1.AC#7; DW2.AC#1, AC#5, AC#11; DW3.AC#4, AC#5; DW4.AC#5; DW5.AC#2, AC#5, AC#6 — **11 P0 acceptance criteria are unfalsifiable today**.
- **No duplicate coverage across levels** — opposite problem: critical ACs are missing at every level.
- **Happy-path-only risk** — DW1.AC#2 (`/project` failure classes) explicitly demands ≥9 distinct categories; current tests cover happy + 3 invalid responses. Reviewer-found rework guaranteed.
- **API endpoint coverage** — every Admin DAPR endpoint has a parser/controller unit test, but DW2 explicitly classifies that as a smoke test, not integration coverage (matches CLAUDE.md R2-A6 rule: "Asserts the call returned 202" is an API smoke test, not an integration test).
- **Auth/authz** — DW3.AC#9 trust boundary has zero negative test backing the `[AllowAnonymous]` decision. This is a documentation gap rather than an authorization defect, but per CLAUDE.md it must be recorded as architecture-note debt.
- **Synthetic UI journeys** — DW5.AC#2 (TypeCatalog nav) is uncovered at every level. AC#5/AC#6 (Ctrl+B) likewise.

---

_Next step: gap analysis, risk scoring (probability × impact, 1–9), and gate decision (PASS / CONCERNS / FAIL / WAIVED)._

---

## Step 4 — Phase 1 Coverage Matrix Complete

**Execution mode:** sequential (per `tea_execution_mode: auto`; sub-agents not used here).
**Coverage matrix written to:** `_bmad-output/test-artifacts/traceability/.tmp-coverage-matrix-2026-05-04.json` (full machine-readable export for Phase 2).

### Coverage Statistics

| Metric | Value |
|--------|------:|
| Total acceptance criteria | 72 |
| Fully covered (FULL) | 1 (1.4%) |
| Partially covered (PARTIAL + UNIT-ONLY) | 20 (27.8%) |
| Uncovered (NONE) | 51 (70.8%) |

### Priority Coverage

| Priority | Total | FULL | % FULL | NONE | % NONE |
|:-------:|:----:|:----:|:----:|:----:|:----:|
| **P0**  | 21 | 1 | **5%** | 11 | **52%** |
| **P1**  | 33 | 0 | **0%** | 23 | **70%** |
| **P2**  | 13 | 0 | **0%** | 12 | **92%** |
| **P3**  |  5 | 0 | **0%** |  5 | **100%** |

Per `test-priorities-matrix.md`, P0 coverage targets are >90% Unit / >80% Integration / all critical E2E paths. We are at **5% FULL on P0**. Per `risk-governance.md`, scores ≥6 require mitigation; scores =9 block the gate.

### Per-Story Snapshot

| Story | Total | FULL | PARTIAL | UNIT-ONLY | NONE | Headline |
|-------|:---:|:----:|:----:|:----:|:----:|---------|
| DW1 | 13 | 0 | 7 | 0 | 6 | Strongest scaffold; **0 stable reason codes asserted** |
| DW2 | 15 | 0 | 6 | 0 | 9 | Live-evidence story; **artifact folder absent** |
| DW3 | 14 | 0 | 1 | 0 | 13 | Helper code (`DeepMerge`/`JsonDiff`/`FlattenJson`) entirely untested |
| DW4 | 15 | 0 | 0 | 0 | 15 | **Validator does not exist** — green-field work |
| DW5 | 15 | 1 | 4 | 2 | 8 | Only story with FULL coverage on any AC; Ctrl+B utterly uncovered |

### Critical (P0) Gaps — 11 ACs

These are the items that, per CLAUDE.md, mandate fix-before-close:

1. **DW1.AC5** — Tracker enumeration corruption disposition (stable codes `tracker_corrupt_scope_index`, `tracker_corrupt_identity_index`)
2. **DW1.AC7** — Drain poison/terminal disposition (decision-AC; either implement terminal/backoff or write accepted-debt record)
3. **DW2.AC1** — Aspire runtime baseline evidence (commands, env flags, URLs, resource states)
4. **DW2.AC5** — Epic 20 debugging tools live smoke against one seeded stream
5. **DW2.AC11** — Evidence artifact index + tables (folder absent)
6. **DW3.AC4** — JSON recursion / malformed-path guards (StackOverflow / ArgumentException defenses)
7. **DW3.AC5** — Direct CommandApi `maxEvents`/`maxFields`/`maxSteps`/`count` bounds (zero/negative/huge)
8. **DW4.AC5** — Redaction rules validated (tokens, connection strings, secrets, hostnames)
9. **DW5.AC2** — TypeCatalog `/types?tab=*` sidebar nav block reproduce-or-close
10. **DW5.AC5** — Ctrl+B sidebar toggle on Blazor renderer context (no console error / circuit exception)
11. **DW5.AC6** — Ctrl+B viewport-tier persistence (`hexalith-sidebar-collapsed-{tier}`)

### High (P1) Gaps — 23 ACs

DW3 (8) and DW4 (10) dominate; see `gap_analysis.high_gaps_p1` in the JSON for full list. Notable:
- **DW3.AC9** — `[AllowAnonymous]` trust-boundary architecture note (no negative auth test exists today).
- **DW4.AC8** — positive + negative validator samples (no validator exists).
- **DW2.AC10** — NFR43 MCP single-resource latency plan + ≥1 sample.
- **DW5.AC10** — DW5 evidence-artifact folder.

### Coverage Heuristics — Blind Spots

| Heuristic | Count | Examples |
|-----------|:----:|----------|
| Endpoints without tests | 6 | All four CommandApi `AdminStreamQueryController` debugging endpoints + `AdminTraceQueryController` direct path + `/project` per-failure-class diagnostics |
| Auth missing negative-path tests | 1 | DW3.AC9 `[AllowAnonymous]` trust boundary |
| Happy-path-only criteria | 5 | DW1.AC2 (only 3 of ≥9 reason codes), DW1.AC3, DW1.AC9, DW2.AC2/3/4, DW3.AC10 |
| UI journeys without E2E | 8 | TypeCatalog nav, Ctrl+B, Ctrl+B+Ctrl+K coexistence, both dialog runtime DOMs, Aspire baseline, Epic 20 seeded smoke, MCP transcripts |
| UI states missing coverage | 3 | TypeCatalog blocked state, sidebar tier collapsed state, FluentDialog runtime aria state |

### Recommendations (Prioritized)

| Priority | Action |
|----------|--------|
| **URGENT** | `/bmad-testarch-atdd` for the 11 P0 NONE acceptance criteria — pin failing tests with the stable diagnostic vocabulary before dev-story execution begins. |
| **HIGH** | `/bmad-testarch-automate` for the 23 P1 NONE acceptance criteria. Budget DW3 helper tests (blame/bisect/step/sandbox/diff JSON behavior) and DW4 validator samples first. |
| **MEDIUM** | Complete coverage for the 20 PARTIAL items. Top of list: DW1.AC2 needs ≥6 more `/project` failure-class tests; DW1.AC4 needs an explicit overlap regression (not just sequential repeat); DW2.AC3 needs a per-surface `RemoteMetadataStatus` matrix; DW5.AC8/AC9 need Playwright runtime DOM evidence. |
| **HIGH** | API tests for the 6 uncovered CommandApi admin endpoints. |
| **HIGH** | Architecture/trust-boundary documentation note for DW3.AC9 `[AllowAnonymous]` controllers (no negative auth test today). |
| **HIGH** | E2E or component coverage for the 8 inferred UI/runtime journeys. |
| **MEDIUM** | Error/edge scenario tests for the 5 happy-path-only criteria. |
| **MEDIUM** | UI state coverage (TypeCatalog blocked, sidebar tier collapsed, dialog runtime aria). |
| **LOW** | `/bmad-testarch-test-review` once new tests are written — stable reason codes are easy to drift. |

### Test Inventory Summary (Deduplicated, Stable IDs)

- **21 stable test references** named with IDs (T-PROJ-01 … T-E2E-PROJ-02).
- Underlying file count: ~70+ test files in `tests/Hexalith.EventStore.*` actually touch DW-relevant code paths today.
- **0 blockers** (no skipped/pending/fixme tests in the named set).
- By level: ~14 unit, ~4 component (bUnit), ~3 e2e (Playwright + Aspire/Tier-3).

---

_Phase 1 complete. Next step: gate decision (PASS / CONCERNS / FAIL / WAIVED) with risk scoring._

---

## Step 5 — Gate Decision (Phase 2)

### Decision: 🚫 **FAIL**

**Rationale:** P0 coverage is **5%** (required: 100%). 11 P0 acceptance criteria are uncovered across all five DW stories. All 5 stories are at status `ready-for-dev`, so this gate measures readiness vs. intended target — not delivered functionality. FAIL is the expected and correct signal: dev-story execution must be preceded by ATDD scaffolding for the 11 P0 NONE items.

### Gate Criteria — Detail

| Criterion | Threshold | Actual | Status |
|-----------|----------|--------|:------:|
| P0 coverage | 100% | **5%** | ❌ NOT_MET |
| P1 coverage (PASS target) | 90% | **0%** | ❌ NOT_MET |
| P1 coverage (minimum) | 80% | **0%** | ❌ NOT_MET |
| Overall coverage | ≥80% | **1%** | ❌ NOT_MET |

Per `risk-governance.md`: critical risks (score = 9) automatically FAIL the gate. The 11 P0 NONE items collectively meet that bar — projection drift, drain poison, Aspire baseline absence, JSON recursion guard absence, missing `[AllowAnonymous]` bounds, missing redaction validator, Ctrl+B circuit hazard, TypeCatalog nav block — each carries probability=3 (will surface immediately on first user/reviewer interaction) × impact=3 (operational correctness, accessibility, security, or trust boundary).

### Risk Scoring Applied to P0 Gaps

| AC | Probability (1-3) | Impact (1-3) | Score | Action |
|----|:-:|:-:|:-:|:------:|
| DW1.AC5 (tracker corruption disposition) | 2 (corrupt state rare in practice but happens) | 3 (tight retry loop or silent reset = data integrity risk) | 6 | MITIGATE |
| DW1.AC7 (drain poison terminal disposition) | 2 (corruption rare) | 3 (corrupted record loops forever or silently drops events) | 6 | MITIGATE |
| DW2.AC1 (Aspire runtime baseline) | 3 (every reviewer needs to reproduce) | 3 (without baseline, all DW2 evidence is unfalsifiable) | **9** | BLOCK |
| DW2.AC5 (Epic 20 seeded smoke) | 3 (debugging tools used in prod incident response) | 3 (untrusted debug output during incident = wrong fix) | **9** | BLOCK |
| DW2.AC11 (evidence artifact index) | 3 | 3 | **9** | BLOCK |
| DW3.AC4 (JSON recursion guard) | 2 (depth depends on payload) | 3 (StackOverflow → process crash) | 6 | MITIGATE |
| DW3.AC5 (CommandApi max bounds) | 3 (admin tools easy to misuse) | 3 (memory amplification → DoS on internal endpoint) | **9** | BLOCK |
| DW4.AC5 (redaction validator) | 3 (templates encourage paste-from-prod) | 3 (token/secret leak risk) | **9** | BLOCK |
| DW5.AC2 (TypeCatalog nav block) | 3 (already reproduced on `/types`) | 2 (workaround = navigate from elsewhere) | 6 | MITIGATE |
| DW5.AC5 (Ctrl+B circuit hazard) | 3 (already reproduced) | 2 (circuit exception → forced reload) | 6 | MITIGATE |
| DW5.AC6 (Ctrl+B viewport persistence) | 2 (only seen on viewport-tier change) | 2 | 4 | MONITOR |

**Five score-9 (BLOCK) items + six score-6 (MITIGATE) items = gate FAIL is unambiguous.**

### Why FAIL Is the Right Answer Here

This is a **planning-stage trace**: every DW story is `ready-for-dev`, no implementation has shipped. A FAIL gate decision **does not block any release** — it's a pre-flight signal that the cleanup package needs ATDD before development. Per CLAUDE.md "Code Review Process," every Epic 2 story produced at least one review-driven patch from a HIGH/MEDIUM finding; pre-test scaffolding for the 11 P0 ACs reduces that rework cost.

If this trace were re-run after dev-story execution and finds the same 11 P0 NONE items, that **would** be a release blocker.

### Top 3 Recommended Actions

1. **🚨 URGENT — Run `/bmad-testarch-atdd`** for the 11 P0 NONE acceptance criteria. Pin failing tests with the stable diagnostic vocabulary the DW1 story explicitly defined (`checkpoint_drift`, `project_upstream_4xx`, `tracker_corrupt_scope_index`, `drain_terminal_failure`, etc.). The vocabulary is the contract; tests asserting reason-code names will not drift after dev-story execution.
2. **HIGH — Run `/bmad-testarch-automate`** for the 23 P1 NONE acceptance criteria. Budget order: DW3 helper tests for `DeepMerge`/`JsonDiff`/`FlattenJson` (untested today, named in story inventory), then DW4 validator positive/negative samples (story is green-field).
3. **HIGH — Sequence the cleanup package**: per the sprint-change-proposal recommendation, ship DW1 first (operational risk), then DW2 (confidence), then DW3 (debugging hardening), then DW4 (governance lever), then DW5 (UX runtime polish). Each story carries reviewer-found rework risk; running ATDD before dev-story shifts that rework left.

### Outputs

| Artifact | Path |
|----------|------|
| Markdown report (this file) | `_bmad-output/test-artifacts/traceability/traceability-matrix.md` |
| Phase 1 coverage matrix (machine-readable) | `_bmad-output/test-artifacts/traceability/.tmp-coverage-matrix-2026-05-04.json` |
| Phase 2 e2e summary (CI-friendly) | `_bmad-output/test-artifacts/traceability/e2e-trace-summary.json` |
| Gate signal (slim) | `_bmad-output/test-artifacts/traceability/gate-decision.json` |

---

## Final Gate Display

```
🚫 GATE DECISION: FAIL

📊 Coverage Analysis:
- P0 Coverage: 5% (Required: 100%) → NOT_MET
- P1 Coverage: 0% (PASS target: 90%, minimum: 80%) → NOT_MET
- Overall Coverage: 1% (Minimum: 80%) → NOT_MET

✅ Decision Rationale:
P0 coverage is 5% (required: 100%). 11 critical requirements uncovered across DW1–DW5.
This is a planning-stage trace — all 5 stories at ready-for-dev. FAIL is the correct
signal that dev-story execution must be preceded by ATDD for the 11 P0 NONE items.

⚠️  Critical (P0) Gaps: 11
⚠️  High (P1) Gaps: 23
⚠️  Medium (P2) Gaps: 12
⚠️  Low (P3) Gaps: 5

📝 Top Recommendations:
1. URGENT: /bmad-testarch-atdd for 11 P0 NONE acceptance criteria
2. HIGH:   /bmad-testarch-automate for 23 P1 NONE acceptance criteria
3. HIGH:   sequence dev-story execution DW1 → DW2 → DW3 → DW4 → DW5

📂 Full Report: _bmad-output/test-artifacts/traceability/traceability-matrix.md
🚫 GATE: FAIL — release would be BLOCKED if this were post-implementation;
         pre-implementation, treat as ATDD trigger.
```

_Workflow complete._
