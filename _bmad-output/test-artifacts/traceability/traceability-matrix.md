---
stepsCompleted: ['step-01-load-context']
lastStep: 'step-01-load-context'
lastSaved: '2026-05-04'
scope: 'Post-Epic Deferred Work Cleanup — DW1 through DW5'
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
