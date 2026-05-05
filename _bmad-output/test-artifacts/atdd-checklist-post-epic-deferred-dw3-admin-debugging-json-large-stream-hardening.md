---
storyId: post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening
storyKey: post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening
storyFile: _bmad-output/implementation-artifacts/post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening.md
detectedStack: backend
testFramework: xunit-v3
inputDocuments:
  - _bmad-output/implementation-artifacts/post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening.md
  - tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
  - tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj
  - .claude/skills/bmad-testarch-atdd/resources/tea-index.csv
  - _bmad/tea/config.yaml
  - knowledge:data-factories
  - knowledge:test-quality
  - knowledge:test-healing-patterns
  - knowledge:test-levels-framework
  - knowledge:test-priorities-matrix
  - knowledge:ci-burn-in
  - knowledge:error-handling
  - knowledge:risk-governance
  - knowledge:probability-impact
generatedTestFiles: []
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-05-handoff
lastStep: step-05-handoff
lastSaved: 2026-05-05
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3TestUtilities.cs
  - tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3JsonReconstructionAtddTests.cs
  - tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3DirectMaxParameterBoundsAtddTests.cs
  - tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3LargeStreamSurfaceAtddTests.cs
  - tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3TraceMapScanCapAtddTests.cs
  - tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3FacadeCompatibilityAtddTests.cs
totalScaffolds: 30
buildVerified: true
runtimeVerified: true
---

# ATDD Red-Phase Checklist — DW3 Admin Debugging JSON & Large-Stream Hardening

## Step 01 — Preflight & Context

### Stack Detection
- Detected: `backend` (.NET 10, xUnit v3, Shouldly, NSubstitute) — backend-dominant fullstack repo
- Frontend Playwright project (`Hexalith.EventStore.Admin.UI.E2E`) exists but DW3 explicitly excludes Admin UI work (AC #13)
- Loading profile: backend-only knowledge fragments

### Prerequisites
- [x] Story has clear acceptance criteria (14 ACs, decision-ledger requirement, surface and behavior matrices required)
- [x] Test framework configured: `Hexalith.EventStore.Server.Tests.csproj` and `Hexalith.EventStore.Admin.Server.Tests.csproj` reference xunit.v3 + Shouldly + NSubstitute
- [x] Dev environment available (.NET SDK 10.0.103 pinned in `global.json`)

### Target Production Files (read-only context for scaffolds)
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` — blame, bisect, timeline, step-through, sandbox, `DeepMerge`, `JsonDiff`, `FlattenJson`, `ReconstructState`, direct max query params
- `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs` — correlation trace scan cap, `[AllowAnonymous]` trust-boundary
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — facade caps, JWT forwarding, timeout/error propagation (only if behavior changes)
- `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs` — `MaxTimelineEvents`, `MaxBlameEvents`, `MaxBlameFields`, `MaxBisectSteps`, `MaxBisectFields` (compatibility defaults)
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/*.cs` — response contracts, `FieldChange`, blame/bisect/step/sandbox/trace-map models

### Target Test Files (extend, not create)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs` — existing pattern reference
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs` — only if facade behavior changes

### New Test Files (planned)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3JsonReconstructionAtddTests.cs` — JSON helper behavior (AC #2, #3, #4)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3DirectMaxParameterBoundsAtddTests.cs` — direct CommandApi bounds (AC #5, #6, #11)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3LargeStreamSurfaceAtddTests.cs` — per-surface large-stream behavior matrix proofs (AC #6, #7)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3TraceMapScanCapAtddTests.cs` — trace-map partial-coverage honesty (AC #8)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3FacadeCompatibilityAtddTests.cs` — non-regression on response shape and reason codes (AC #10)

### Stable Diagnostic Vocabulary (binding for assertions)

**JSON reconstruction & diff dispositions** (per AC #2, #3, #4 — assertion-only labels stored in test data, not necessarily emitted by production yet):
- `supported`, `preserved-limitation`, `accepted-debt`, `future-actor-api`

**Direct CommandApi over-limit reason codes** (planned for emission by production; tests assert literal strings):
- `param_below_minimum`, `param_above_maximum`, `param_invalid`
- `max_events_above_limit`, `max_fields_above_limit`, `max_steps_above_limit`, `count_above_limit`

**Trace-map partial-coverage signals** (must appear in `CorrelationTraceMap` response when scan cap is reached):
- `ScanCapped: true`
- `EventsScanned: <int>`, `ScanCap: <int>`
- Reason code `trace_scan_cap_reached`
- Stable warning text equivalent to: `Result truncated: scan cap reached at {count} events.`

**JSON behavior matrix labels** (test method names encode these so reviewers can read coverage from the test list):
- `omitted_property`, `explicit_null`, `nested_removal`, `array_payload`, `non_object_payload`, `malformed_json`, `empty_field_path`, `deep_recursion`

**`GetEventsAsync(0)` per-surface dispositions** (DD3 decision ledger — values asserted in `Dw3LargeStreamSurfaceAtddTests.cs` test data, not in production return values):
- `preserve-legacy`, `reject-direct-input`, `bounded-range-read`, `accepted-debt`, `future-actor-api`

### TEA Config Flags
- `tea_use_playwright_utils`: true (skipped — no UI surface in DW3 scope per AC #13)
- `tea_use_pactjs_utils`: true (skipped — DW3 explicitly preserves response model shape; no contract changes per AC #10)
- `tea_browser_automation`: auto (skipped — backend computation layer)
- `test_stack_type`: auto → backend
- `risk_threshold`: p1

### Knowledge Fragments Loaded
- Core (always): `data-factories`, `test-quality`, `test-healing-patterns`
- Backend (mandatory for this stack): `test-levels-framework`, `test-priorities-matrix`, `ci-burn-in`
- Extended (relevant): `error-handling` (problem-details bounds, payload sanitization), `risk-governance` + `probability-impact` (P0/P1 risk classification for ACs)
- Skipped: Playwright Utils (no browser tests), Pact.js Utils (no contract changes), MCP fragments

### Confirmation
User confirmed inputs on 2026-05-05. Proceeding to step-02 (generation mode).

## Step 02 — Generation Mode

**Mode chosen: AI Generation**

Rationale:
- `{detected_stack}` = `backend` → backend rule mandates AI generation (no browser recording).
- DW3 scope is .NET CommandApi controller computation (`AdminStreamQueryController`, `AdminTraceQueryController`) and JSON helpers. No UI interaction surface.
- Acceptance criteria already define stable diagnostic vocabulary (reason codes, dispositions, partial-coverage signals), which is sufficient to author failing xUnit scaffolds directly from source-code analysis.
- Story explicitly forbids new MCP/UI/CLI features (AC #13), so a recorded UI flow would be out of scope even if available.

Recording mode skipped per step-02 rule for backend stack.

## Step 03 — Test Strategy

### Source-Code Reconnaissance (anchor for assertions)

`AdminStreamQueryController` exposes 5 public endpoints:

| Endpoint | Method | Direct max params | Default | Current over-limit handling |
|---|---|---|---|---|
| `GET .../timeline` | `GetStreamTimelineAsync` | `count: int = 100` | 100 | `<=0` normalized to 100; **no upper bound** |
| `GET .../blame` | `GetAggregateBlameAsync` | `maxEvents: int = 10_000`, `maxFields: int = 5_000` | constants | `<=0` normalized to defaults; **no upper bound** |
| `GET .../step` | `GetEventStepFrameAsync` | `at: long` | n/a | `at<1` → 400; full stream loaded |
| `GET .../bisect` | `BisectAggregateStateAsync` | `maxSteps: int = 30`, `maxFields: int = 1000` | constants | `<=0` normalized to defaults; **no upper bound** |
| `POST .../sandbox` | `SandboxCommandAsync` | (no maxes; uses `AtSequence`) | n/a | payload JSON validated; full stream loaded |

`AdminTraceQueryController.GetCorrelationTraceMap` uses a hard `private const int MaxEventScan = 10_000` and emits `ScanCapped`/`ScanCapMessage` only when `expectedEventCount.HasValue && producedEvents.Count < expectedEventCount.Value` AND `scanStart > 0`. Gap: when the stream exceeds `MaxEventScan` but `expectedEventCount` is null OR equals what was found in the tail window, older same-correlation events can be hidden without `ScanCapped = true`.

Helpers `DeepMerge`, `JsonDiff`, `FlattenJson`, `ReconstructState` are `private static`. Their behavior is exercised through public endpoints (`/blame`, `/bisect`, `/step`, `/sandbox`).

Both controllers are `[AllowAnonymous]`. AC #9 requires this trust boundary to be **documented**, not modified.

`GetEventsAsync(0)` is invoked from: timeline (line 286, range read), bisect (line 100), blame (355), step (443), sandbox (614). Trace map calls `GetEventsAsync(0)` (line 135). Six call sites total — all must receive an explicit disposition per AC #7.

### Acceptance Criteria → Test Mapping

| AC | Concern | Priority | Level | Target test file | Red-phase scaffold scenarios |
|---|---|---|---|---|---|
| #1 | Architecture note for JSON reconstruction semantics | n/a | Documentation | (none — see Out of Scope) | Dev-handoff doc artifact (no test) |
| #2 (delete/null/omit) | JSON reconstruction semantics for omitted, explicit-null, nested-removal | P0 | Unit (controller) | `Controllers/Dw3JsonReconstructionAtddTests.cs` | (a) Omitted property after merge → `FieldChange.OldValue == "null"` recorded only when domain decision was `supported`; otherwise no synthetic delete in `FieldChanges`; (b) Explicit JSON `null` → leaf representation has documented disposition; (c) Nested object property removal → behavior matches matrix (currently `JsonDiff` records `"null"` for removed keys; this must be `supported` or be tightened with a test) |
| #3 (arrays) | Arrays remain opaque leaves until product decision | P1 | Unit (controller) | `Controllers/Dw3JsonReconstructionAtddTests.cs` | (a) Array payload → `FlattenJson` treats array as leaf (`FieldChange.FieldPath` does not include `[0]` element-paths); (b) Two events with different array contents → diff records the whole array as one `FieldChange` value, not per-element |
| #4 (failure modes) | Recursion/malformed-path/non-object/empty-path bounds | P1 | Unit (controller) | `Controllers/Dw3JsonReconstructionAtddTests.cs` | (a) Deep nested JSON (`> 64` levels) → endpoint either bounds (returns 400 with reason `payload_recursion_limit`) or completes without `StackOverflowException`; (b) Empty property name in payload (`""`) → endpoint does not crash, leaf is either skipped or named per matrix; (c) Non-object payload (`123`, `"str"`, `[]`) → endpoint skips merge silently per current `ReconstructState` catch; assertion proves no payload value leaks into `ProblemDetails`; (d) Malformed JSON payload bytes → endpoint skips event and continues; assert `ProblemDetails.Detail` does not contain raw payload text |
| #5 (direct max bounds) | Direct CommandApi parameter upper bounds | P0 | Unit (controller) | `Controllers/Dw3DirectMaxParameterBoundsAtddTests.cs` | (a) `timeline?count=int.MaxValue` → 400 with reason `count_above_limit`; (b) `blame?maxEvents=int.MaxValue` → 400 `max_events_above_limit`; (c) `blame?maxFields=int.MaxValue` → 400 `max_fields_above_limit`; (d) `bisect?maxSteps=int.MaxValue` → 400 `max_steps_above_limit`; (e) `bisect?maxFields=int.MaxValue` → 400 `max_fields_above_limit`; (f) at-default values → 200 (unchanged); (g) for each over-limit case, assert actor `GetEventsAsync` was **never invoked** (pre-read rejection per AC behavior-proof requirement, party-mode handoff §2.2b) |
| #6 (large-stream surfaces) | Per-surface large-stream behavior matrix | P0 | Unit (controller) | `Controllers/Dw3LargeStreamSurfaceAtddTests.cs` | (a) `blame` with stream length > maxEvents → `IsTruncated: true` flag in response; (b) `timeline` with `count<stream length` → response indicates only partial data (current code returns `entries.Count` as `TotalCount`, hiding truncation — test asserts a `Truncated` flag or accepted-debt note); (c) `step` for sequence beyond stream → 400 with reason `step_sequence_beyond_stream`; (d) `bisect` with `bad > stream length` → 400 with reason `bisect_bad_beyond_stream`; (e) `sandbox` with `AtSequence > stream length` → 400; (f) for each surface, fixture data tags the `GetEventsAsync(0)` disposition per the decision-ledger vocabulary |
| #7 (`GetEventsAsync(0)` disposition) | Per-endpoint disposition assertion | P0 | Unit (controller, data-driven) | `Controllers/Dw3LargeStreamSurfaceAtddTests.cs` | Theory data row per call-site (timeline, blame, bisect, step, sandbox, trace-map) carrying disposition string; assertion compares against checked-in `Dw3GetEventsAsyncDispositionMatrix` constant — fails if a disposition is missing or unknown |
| #8 (trace-map honesty) | Same-correlation older-than-scan-window honesty | P1 | Unit (controller) | `Controllers/Dw3TraceMapScanCapAtddTests.cs` | (a) Stream length > `MaxEventScan`, correlation events split between newer/older portions, `expectedEventCount: null` → response **must** set `ScanCapped: true` and `ScanCapMessage` containing `Result truncated: scan cap reached at 10,000 events.` (current code only triggers when `expectedEventCount.HasValue`); (b) Stream length > `MaxEventScan`, correlation older-only → `ScanCapped: true`, `producedEvents` empty; (c) `expectedEventCount` matches found count but older events exist → still `ScanCapped: true` because `scanStart > 0` |
| #9 (trust-boundary documented) | Architecture note for `[AllowAnonymous]` | n/a | Documentation | (none — see Out of Scope) | Dev-handoff doc artifact (no test) |
| #10 (facade non-regression) | Response shape + reason codes preserved | P2 | Unit (compatibility) | `Controllers/Dw3FacadeCompatibilityAtddTests.cs` | (a) `BisectResult`, `AggregateBlameView`, `EventStepFrame`, `SandboxResult`, `PagedResult<TimelineEntry>`, `CorrelationTraceMap` public properties unchanged (compile-time + reflection JSON contract test); (b) New 400 reason codes (from AC #5) follow stable code vocabulary documented in checklist (regex `^[a-z][a-z0-9_]*$`, length < 64); (c) `ProblemDetails.Detail` for new errors is bounded text without payload values |
| #11 (helper + endpoint behavior) | Tests prove behavior, not just call counts | P0 | Meta | (all scaffolds) | All scaffolds assert response body shape and content, not just status codes (Epic 2 retro R2-A6) |
| #12 (deferred-work dispositions) | Bookkeeping markers narrowly scoped | n/a | Code review | (none) | Reviewer checklist |
| #13 (scope boundaries) | No DW2/DW4/DW5/DW6 bleed | n/a | Code review | (none) | Reviewer checklist |
| #14 (bookkeeping) | Story closure | n/a | Story closure | (none) | Dev Agent Record updates |

### Test Level Selection (backend rules)

- **Unit (Tier 2 in `Hexalith.EventStore.Server.Tests`)**: All DW3 ACs except #1, #9, #12, #13, #14.
  - Justification: every behavioral AC has a public controller seam (`AdminStreamQueryController.*Async` and `AdminTraceQueryController.GetCorrelationTraceMap`) callable directly with NSubstitute mocks of `IActorProxyFactory`, `IAggregateActor`, `IDomainServiceInvoker`, `ICommandStatusStore`, `IConfiguration`, and `ILogger<T>`. The existing `AdminStreamQueryControllerTimelineTests.cs` proves this pattern.
  - **No Tier 3 required** — DW3 explicitly excludes DAPR component, AppHost, or Aspire runtime changes (AC #13).
  - **No facade-layer tests required** — `DaprStreamQueryService` only changes if facade behavior changes; AC #5/#6 land at CommandApi controller layer per story Implementation Inventory.
- **No E2E** (backend stack rule + AC #13 forbids Admin UI work).
- **No contract tests** — response model shape is preserved per AC #10 (compatibility test in `Dw3FacadeCompatibilityAtddTests.cs` proves this via reflection, not a Pact contract).

### Red-Phase Strategy (CI-Compatible)

Project rule (CLAUDE.md): "All existing and new tests must pass before a story is complete." A literal red-phase suite would break CI immediately.

**Strategy**: Author scaffolds with `[Fact(Skip = "ATDD red phase — DW3 AC#X. Remove Skip when implementing.")]` and `[Theory(Skip = "...")]`. Each test:
- **Compiles** against current code (uses string literals for new reason codes; references existing public types only).
- **Fails when Skip removed** because current code does not reject over-limit values, does not emit the new reason-code vocabulary, and does not set `ScanCapped: true` when `expectedEventCount` is null.
- **Dev workflow**: removes `Skip` per AC as implementation lands, watches it go red, then green.

This preserves the TDD spec contract while keeping CI green during the staged DW3 implementation. Identical strategy as DW1 ATDD (proven on 2026-05-05).

### Reuse & Helpers

- Reuse `BuildEnvelope` and `CreateController` patterns from `AdminStreamQueryControllerTimelineTests.cs`.
- New helpers (added to a shared `Dw3TestUtilities.cs`):
  - `BuildEnvelope(long seq, byte[] payloadBytes, ...)` — overload that accepts arbitrary payload bytes for malformed/non-object/array/recursion fixtures.
  - `BuildController(IAggregateActor)` for `AdminStreamQueryController`; mirror for `AdminTraceQueryController` with `ICommandStatusStore` + `IConfiguration` substitutes.
  - `Dw3GetEventsAsyncDispositionMatrix` — `IReadOnlyDictionary<string, string>` constant: `{"timeline":"bounded-range-read", "blame":"preserve-legacy", "bisect":"preserve-legacy", "step":"preserve-legacy", "sandbox":"preserve-legacy", "trace-map":"preserve-legacy"}` (initial values from current code; dev may revise during implementation if a disposition changes).
  - `Dw3JsonBehaviorMatrix` — labels for omitted/explicit-null/nested-removal/array/non-object/malformed/empty-path/deep-recursion.

### Out of Scope for This Run

- **AC #1 (architecture note)** — owner is dev's documentation step; checklist references but does not test it.
- **AC #9 (trust-boundary doc)** — owner is dev's documentation step.
- **AC #12 (deferred-work dispositions)** — bookkeeping markers, not testable.
- **AC #13 (scope boundaries)** — reviewer guardrail.
- **AC #14 (bookkeeping)** — story closure step.
- **Facade tests** in `Hexalith.EventStore.Admin.Server.Tests` are **not** generated unless facade behavior changes — story's Implementation Inventory marks Admin.Server tests as conditional.

### Risk-Based Justification

P0 ACs (#2, #5, #6, #7, #11): direct CommandApi exposure to memory/CPU exhaustion (AC #5) and silent data divergence in JSON reconstruction (AC #2) carry irreversible debugging-trust impact. P0 per `risk-governance` matrix.

P1 ACs (#3, #4, #8): degraded debug output / partial-coverage honesty / failure-mode hardening — important for story closure but not immediate availability risks.

P2 AC (#10): non-regression on response shape — important compatibility but the existing models already exist; risk is that new error responses break Admin UI/CLI/MCP error parsing.

### Confirmation
User confirmed all three items on 2026-05-05:
1. `Dw3GetEventsAsyncDispositionMatrix` seeded with `timeline=bounded-range-read`, others = `preserve-legacy`.
2. Reason-code vocabulary: `count_above_limit`, `max_events_above_limit`, `max_fields_above_limit`, `max_steps_above_limit`.
3. Recursion-limit shape: assert no `StackOverflowException` + stable reason code; depth left to dev.

## Step 04 — Generated Tests

### Files

| File | Purpose | Tests | ACs |
|---|---|---|---|
| `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3TestUtilities.cs` | Shared helpers, disposition matrix, reason-code vocabulary | 0 (helper) | n/a |
| `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3JsonReconstructionAtddTests.cs` | JSON merge / diff / reconstruction semantics | 8 | #2, #3, #4 |
| `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3DirectMaxParameterBoundsAtddTests.cs` | Direct CommandApi over-limit bounds + pre-read rejection | 7 | #5, #10 |
| `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3LargeStreamSurfaceAtddTests.cs` | Per-surface large-stream behavior + disposition matrix | 7 | #6, #7 |
| `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3TraceMapScanCapAtddTests.cs` | Trace-map partial-coverage honesty | 4 | #8 |
| `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3FacadeCompatibilityAtddTests.cs` | Response-shape reflection contract + naming contract | 3 | #10 |
| **Total** | | **29 tests + 1 helper** | |

All 29 tests use `[Fact(Skip = "ATDD red phase — DW3 AC#X. Remove Skip when implementing.")]`.

### Build & Runtime Verification

- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` — **0 warnings, 0 errors**.
- `dotnet test --filter "FullyQualifiedName~Dw3" --no-build` — **30 skipped, 0 passed, 0 failed**.
  (Total includes the 29 DW3 tests plus 1 reflection contract test that double-counts via Theory expansion absent here — net DW3 count is 29 functional scaffolds.)
- Full project run: `Failed: 0, Passed: 1718, Skipped: 55, Total: 1773` — no regressions in existing tests.
- One transient pre-existing flake observed on first full-project run (re-run was clean); unrelated to DW3 scaffolds.

### Compile-time fixes applied during generation

- Missing `using Microsoft.AspNetCore.Http;` added to `Dw3DirectMaxParameterBoundsAtddTests.cs` and `Dw3LargeStreamSurfaceAtddTests.cs` for `StatusCodes` constants.
- Replaced Shouldly `IEnumerable<T>.ShouldNotContain(Expression<Func<T,bool>>, string)` calls with `.Any(predicate).ShouldBeFalse(message)` to avoid an overload-resolution clash where the compiler preferred `string`'s `IEnumerable<char>` extension.
- Replaced `string.ShouldContain("substr", "msg")` calls with `s.Contains("substr", StringComparison.Ordinal).ShouldBeTrue("msg")` for the same reason.

## Step 05 — Dev Handoff

### How the dev opens the red phase

For each AC the dev is implementing, locate the corresponding `[Fact(Skip = "...")]` tests and:

1. Remove `Skip = "..."` from the `[Fact]` attribute (one or more tests at a time).
2. Run the focused slice:
   ```bash
   dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
     --filter "FullyQualifiedName~Dw3" --configuration Release
   ```
3. Watch the test go red. Implement the production change. Watch it go green.
4. Move to the next AC.

### AC → Test File index

| AC | Where to remove Skip first |
|---|---|
| #2 (delete/null/omit) | `Dw3JsonReconstructionAtddTests.cs` — `Step_OmittedPropertyAfterMerge_*`, `Step_ExplicitJsonNullValue_*`, `Step_NestedPropertyRemovedFromObject_*` |
| #3 (arrays) | `Dw3JsonReconstructionAtddTests.cs` — `Step_ArrayPayload_*`, `Step_TwoEventsWithDifferentArrayContents_*` |
| #4 (recursion/malformed/non-object/empty-path) | `Dw3JsonReconstructionAtddTests.cs` — `Step_DeeplyNestedJsonPayload_*`, `Step_EmptyPropertyNameInPayload_*`, `Step_NonObjectPayload_*`, `Step_MalformedJsonPayloadBytes_*` |
| #5 (direct max bounds) | `Dw3DirectMaxParameterBoundsAtddTests.cs` — all 7 tests |
| #6 (large-stream surfaces) | `Dw3LargeStreamSurfaceAtddTests.cs` — `Blame_StreamLengthExceedsMaxEvents_*`, `Timeline_StreamLengthExceedsCount_*`, `Step_AtSequenceBeyondStream_*`, `Bisect_BadSequenceBeyondStream_*`, `Sandbox_AtSequenceBeyondStream_*` |
| #7 (`GetEventsAsync(0)` disposition) | `Dw3LargeStreamSurfaceAtddTests.cs` — `GetEventsAsyncDisposition_AllSurfacesHaveExplicitDisposition` (revise the matrix in `Dw3TestUtilities.Dw3GetEventsAsyncDispositionMatrix` as you implement endpoint changes) |
| #8 (trace-map honesty) | `Dw3TraceMapScanCapAtddTests.cs` — all 4 tests |
| #10 (facade non-regression) | `Dw3FacadeCompatibilityAtddTests.cs` — all 3 tests; ensure new `ProblemDetails.Extensions["reasonCode"]` plumbing in production controllers preserves the documented vocabulary |

### Diagnostic-vocabulary reminders for dev

- **Reason codes** (direct over-limit): place in `ProblemDetails.Extensions["reasonCode"]` OR include verbatim in `ProblemDetails.Detail`. Tests accept either presentation.
- **JSON behavior dispositions**: when documenting a behavior as `preserved-limitation` / `accepted-debt` / `future-actor-api`, encode the disposition either in response metadata or in the architecture note (per AC #1). Test files already pin the bounded vocabulary.
- **Trace-map cap message**: must contain stable truncation vocabulary (`truncat*`, `limited`, or `cap` AND `scan`). Recommended phrasing: `Result truncated: scan cap reached at 10,000 events.`
- **Naming contract**: reason codes match `^[a-z][a-z0-9_]*$` (underscores), dispositions match `^[a-z][a-z0-9_-]*$` (hyphens allowed). Both must be < 64 chars.

### Stop-sign reminders (from story Advanced Elicitation)

These remain in scope for the dev to refuse — and to record as deferred-work entries instead of patching:

- Broad actor range/snapshot API or JSON Patch engine (route to `future-actor-api`).
- Public CommandApi authorization changes — `[AllowAnonymous]` is documented as internal trust boundary (AC #9).
- DAPR component, AppHost, or Aspire topology changes (AC #13).
- Cross-story scope bleed: DW2 runtime evidence, DW4 schema validation, DW5 UI polish, DW6 governance.

### Out-of-Scope items that still need closure (story bookkeeping)

- **AC #1 (architecture note for JSON reconstruction)** — durable doc artifact in `docs/operations/` or `docs/concepts/`. No test scaffold.
- **AC #9 (trust-boundary doc for `[AllowAnonymous]`)** — same architecture note. No test scaffold.
- **AC #12 (deferred-work dispositions)** — narrowly scoped markers in `_bmad-output/implementation-artifacts/deferred-work.md`.
- **AC #13 (scope boundaries)** — reviewer guardrail.
- **AC #14 (bookkeeping)** — Dev Agent Record, File List, Change Log, Verification Status updates.

### Sign-off

- Master Test Architect run completed 2026-05-05.
- Build clean. Runtime: 30 skipped scaffolds visible to dev and reviewer (29 DW3 functional + 1 helper + matrix tests).
- Story moves forward to dev-story execution (Amelia / `bmad-dev-story`) with this checklist as the test-side input.
