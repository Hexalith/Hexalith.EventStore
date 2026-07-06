---
title: 'Run and green the integration + E2E test suites'
type: 'bugfix'
created: '2026-07-06'
status: 'done'
review_loop_iteration: 0
context: ['{project-root}/_bmad-output/project-context.md']
baseline_commit: '7b8edfe904498cdf32fd079060b55f64401e309b'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Three suites never run in the normal unit pass because they need Docker / Aspire / Playwright: `IntegrationTests` (231 Tier-3 assertions), `Admin.UI.E2E` (23 Playwright assertions), and several environment-gated `Assert.Skip` tests. Locally they contribute zero signal, so whether they pass is unknown. (`Testing.Integration.Tests` is actually pure-unit and already passes 13/13.)

**Approach:** The local environment is now verified capable (Docker, Redis, Dapr placement/scheduler, Playwright Chromium all up; build green 0/0). Run all three suites, triage every failure as a real product defect (fix at source) vs a broken/flaky test (fix the test), drive both heavy suites to green, and confirm the environment-gated skips now execute instead of skipping.

## Boundaries & Constraints

**Always:**
- Obey `project-context.md`: `ConfigureAwait(false)` on awaits, ULID (never `Guid.TryParse`) for id fields, Shouldly + xUnit v3, `.slnx` only, no copyright headers, warnings-as-errors.
- Root-cause each failure before editing. A product bug is fixed in `src/`; a test bug is fixed in the test. Never weaken/delete an assertion to force green.
- Preserve the Tier 2/3 rule (R2-A6): integration tests assert persisted state (Redis contents / CloudEvent body), not just status codes. Keep that when repairing.

**Ask First:**
- The running **FrontComposer** Aspire app shares the Dapr placement/scheduler and reuses the `sample`/`tenants` app-ids that EventStore's topology also boots. Before running `IntegrationTests`, decide: stop FrontComposer for a clean run, or coexist and re-run/flag interference. Do **not** kill the user's FrontComposer session without approval.
- If a failure needs a production change with real blast radius (auth, event ordering, actor drain, tenant scoping), confirm the fix approach before applying.

**Never:**
- Don't modify submodule files (`references/*`) to force a pass.
- Don't implement deferred-work DW items or un-skip the ATDD red-phase tests (DW1/DW2/DW4/DW6/DW9) — they track unbuilt features and are out of scope.
- Don't skip/disable a failing test to make a suite green.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Env-gated skip, infra now present | `ChaosResilienceTests`, `TenantBootstrapHealthTests` (dapr_redis up); `LiveCommandSurfaceSmokeTests` (dev signing key); `ProtectedIdentifierGuidParserAuditTests` (repo root) | Test **executes** (not skipped) and passes | If it still skips, fix the gate detection so present infra is detected |
| Real product defect | Failing assertion traced to `src/` behavior | Fix source; keep the assertion; re-run green | Confirm approach first if blast radius is high |
| Broken / flaky test | Fails on timing, ordering, or isolation | Fix the test (timeout/wait, isolation, teardown) keeping it meaningful | Re-run to confirm determinism |
| FrontComposer interference | Shared placement misroutes `sample`/`tenants` invocations | Detect and HALT per Ask-First before treating as a product bug | — |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.IntegrationTests/` — 231 Tier-3 assertions; serial (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`).
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs` — boots the AppHost topology, waits for `eventstore`/`eventstore-admin`/`sample` healthy (3-min timeouts).
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/{ChaosResilienceTests,TenantBootstrapHealthTests,LiveCommandSurfaceSmokeTests}.cs` — env-gated `Assert.Skip`.
- `tests/Hexalith.EventStore.Server.Tests/Governance/ProtectedIdentifierGuidParserAuditTests.cs` — repo-root-gated `Assert.Skip`.
- `tests/Hexalith.EventStore.Admin.UI.E2E/PlaywrightFixture.cs` + `*.cs` — 23 Playwright assertions; self-hosts Admin.UI on Kestrel (no shared Dapr).
- `src/**` — fix target **only if** a real product defect surfaces (unknown until the suites run).

## Tasks & Acceptance

**Execution:**
- [x] Run `Admin.UI.E2E` — **39 passed** (2 DW5-ATDD skips), 0 failed.
- [x] Resolve FrontComposer coexistence (user stopped it), then run `IntegrationTests` and monitor — full failure list captured.
- [x] Triage each failure → real defect vs test defect. **Real product defect** found & fixed in `src/` (`QueryResult` STJ ctor). Remaining reds triaged to one test-harness root cause (HotReload DCP-stop cascade) → deferred per user.
- [x] Confirm the env-gated tests now **execute** rather than skip — `LiveCommandSurfaceSmokeTests`, `ProtectedIdentifierGuidParserAuditTests`, `ChaosResilienceTests`, `TenantBootstrapHealthTests` all ran (not skipped).
- [x] Re-run runnable unit set + projection subset — green, no regression. (Full `IntegrationTests` not all-green: blocked only by the deferred HotReload harness cascade.)

**Acceptance Criteria:**
- Given the verified local env, when `Admin.UI.E2E` runs, then all 23 tests pass (0 failed).
- Given FrontComposer coexistence resolved, when `IntegrationTests` runs, then all non-ATDD tests pass (0 failed) and the previously env-gated tests execute instead of skipping.
- Given a real product defect was found, when fixed, then the change is in `src/` (not a weakened assertion) and the persisted-state assertion is preserved.
- Given the ATDD red-phase DW skips, they remain skipped (still tracking unbuilt features).
- Given the fixes, when the full runnable unit set re-runs, then it stays green (no regression).

## Spec Change Log

- 2026-07-06 — **Product bug found + fixed.** The projection-query failures root-caused to a genuine defect: `src/Hexalith.EventStore.Contracts/Queries/QueryResult.cs` had two public constructors and no `[JsonConstructor]`, so System.Text.Json (the projection actor wire path, `DefaultProjectionActorInvoker.InvokeMethodAsync<QueryEnvelope,QueryResult>`) threw `NotSupportedException` on **every** projection query (fast 500 on a clean host; 60s hang when compounded by shared-placement staleness). Fixed by converting `QueryResult` to the `QueryEnvelope` idiom (explicit `init` properties + `[JsonConstructor]` on the full ctor), **preserving** the API-surface-pinned 4-arg compat ctor and all DataContractSerializer wire behavior. Updated the one named-arg call site (`src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`). Added two STJ round-trip regression tests to `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs` (prior tests only covered DataContractSerializer — the gap that let the bug ship). Verified: projection integration subset green in a clean topology; unit regression-clean (Contracts 564/0, Server query/projection 428/0, Testing 144/0).
- 2026-07-06 — **Scope decision (user).** The remaining full-suite `IntegrationTests` reds (15) all cascade from `HotReloadTests`' Aspire DCP resource-stop failing under WSL2 and corrupting the shared `AspireContractTests` topology — test-harness/environment, non-gated Tier-3. Deferred to `deferred-work.md` (HotReload fixture isolation + shared-placement actor-name randomization) rather than fixed here.

## Design Notes

- **Verified prereqs — do NOT re-provision:** Docker up; `dapr_redis`, `dapr_placement` (:50005), `dapr_scheduler` (:50006) containers up; `~/.dapr/bin/daprd` present; Playwright `chromium-1228` installed; `Testing.Integration.Tests` already 13/13.
- **Runtime:** `IntegrationTests` runs serially and boots the full topology per collection (incl. a Keycloak collection). Expect tens of minutes — run in the background and monitor; don't foreground-block.
- **FrontComposer detail:** its daprd sidecars register `sample`/`tenants`/`parties` on the shared placement; the EventStore fixture randomizes only its own aggregate actor type, so `sample`/`tenants` service-invocation could misroute → false failures. `Admin.UI.E2E` is unaffected (self-hosted Kestrel, no shared Dapr).

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Admin.UI.E2E/Hexalith.EventStore.Admin.UI.E2E.csproj -c Release` — expected: Passed, 0 failed.
- `dotnet test tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Release` — expected: Passed, 0 failed; env-gated tests run (not skipped).
- `dotnet test tests/Hexalith.EventStore.Testing.Integration.Tests/Hexalith.EventStore.Testing.Integration.Tests.csproj -c Release` — expected: 13 passed (regression guard).
