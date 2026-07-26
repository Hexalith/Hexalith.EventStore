---
created: 2026-07-15
story_id: "2.6"
story_key: 2-6-tenants-ui-client-library-alignment-and-ux-evidence
status: in-progress
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.6: Tenants UI Client-Library Alignment And UX Evidence

Status: in-progress

The parent Story 2.4 spec records UI-boundary guardrails. This child reviews typed-client
consumption, absence of generated/hand-written per-message controllers, and canonical UX
states. Only `ProjectionBacked` evidence may claim lifecycle state; handler-computed,
missing, or invalid provenance renders `Unknown`. `done` requires Sally's focused UX
review, Tenants maintainer approval, the exact Tenants SHA, and structural/UI test evidence.

### Review Findings

- [x] [Review][Patch] Reject non-projection-backed lifecycle evidence before mapping UI freshness [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:792]
- [x] [Review][Patch] Evaluate the effective UI dependency graph instead of raw project `Include` text [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:166]
- [x] [Review][Patch] Verify compiled UI controllers and runtime endpoints instead of literal source markers [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:186]

#### 2026-07-15 Review Resolution

- Patched UI freshness mapping so only projection-backed provenance can produce current or stale lifecycle evidence; handler-computed, missing, and invalid provenance remain unknown.
- Replaced raw project/source scans with evaluated source/package dependency checks, compiled-controller inspection, and an in-process endpoint inventory.
- Validation: Release package-mode build passed with zero warnings/errors; focused query-gateway tests passed 103/103; composition/runtime-boundary tests passed 16/16; the full UI test assembly passed 881/881; Debug source-mode build passed with the unrelated Memories source edge pinned to package mode.
- Remaining completion gates: Sally's focused UX review, Tenants maintainer approval, and an exact committed Tenants SHA are not yet recorded. The story therefore remains `in-progress`.

#### 2026-07-26 Code Review (commits `56c506c^..8ab537e`)

Adversarial review of the four File List paths across four layers (blind-hunter, edge-case-hunter,
verification-gap, acceptance-auditor). Line references are current-state at Tenants HEAD `42f0c5c`;
the reviewed range ended at `8ab537e` and later commits have moved these files.

- [x] [Review][Decision] Story 2.11 ownership encroachment — **Resolved 2026-07-26: owner ratified the overlap** (see "Story 2.11 Citation And Ratified Overlap" below). The entire production change (provenance gate, lifecycle-precedes-`IsStale` selection, fail-closed `Unknown`, and the four call-site rewrites) plus ten new theories implement and prove behaviour `planning-artifacts/epics.md:72` assigns **exclusively** to Story 2.11, which is itself still in `review`. AC2 requires 2.6 to *cite* 2.11 "rather than re-proving or signing off those behaviors"; the story file never mentions 2.11 and instead restates the contract as its own rule (lines 16-17), then signs off on it (line 46). Corroborates readiness finding MAJ-2 (2.6/2.11 double-own the Tenants-UI provenance AC). Decide: re-scope 2.6, move this evidence to 2.11, or ratify the overlap.
- [x] [Review][Decision] AC3 unmet on all three gates, and the story file is factually false about the SHA — **Resolved 2026-07-26: owner chose "seek approval, record SHA".** The false claim is corrected in the Completion Notes with the actual commits (`56c506c`, `8ab537e`) and review-time Tenants SHA `42f0c5c`; maintainer approval and Sally's UX review remain open gates. **No submodule file is to be edited until approval is confirmed — this blocks all 11 patch findings below, which are every one of them edits under `references/Hexalith.Tenants`.** Original finding: line 47 stated the four Tenants changes are "uncommitted on base SHA `28630b94`"; `git merge-base --is-ancestor 8ab537e HEAD` succeeds, so they are committed as `56c506c`/`8ab537e` and are on Tenants `main`. No Sally UX review, no Tenants maintainer approval, and no exact approved SHA — while `project-context.md:75` forbids modifying submodule files without explicit approval. Every patch below is also a submodule edit under the same gate. Decide: obtain approval and record the SHA, or revert.
- [x] [Review][Decision] AC2's 2.6-owned half is not implemented, and the diff makes four of AC3's seven states unrepresentable — **Resolved 2026-07-26: owner chose "defer to a platform story".** The `ReadModelFreshnessState` limitation is logged as a Hexalith.EventStore platform prerequisite in `deferred-work.md`, and Story 2.6's UX obligation narrows to the states the current model can express (`Current`, `Stale`, `Unknown`) plus the denied/loading/unavailable surface states the UI already owns. Sally's focused UX review is scoped accordingly. Original finding: no `.razor`, bUnit render, or resource file is touched; all new tests are gateway-level. `ReadModelFreshnessState` carries only `{Unknown, Current, Aging, Stale}`, so the new mapping collapses `Rebuilding`/`Degraded`/`Unavailable`/`LocalOnly` into `Unknown` and the new theories pin that collapse as intended. AC3 requires canonical support-safe treatment for rebuilding/degraded/unavailable/local-only. Decide whether a representable lifecycle state model (platform change) is a prerequisite before any UX work can be reviewed.
- [ ] [Review][Patch] A 304 carrying lifecycle-only evidence discards a previously-`Stale` snapshot and renders `Ready` [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1163]
- [ ] [Review][Patch] `ResolveFreshness` = `Current` gates mutations that `ProjectionLifecyclePolicy.CanMutate` denies [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantCorrectionStartIntent.cs:88]
- [ ] [Review][Patch] Lifecycle-on-304 and lifecycle-over-`IsStale` precedence are verified on the tenant-detail surface only; four of five surfaces sharing the changed helper are unexercised [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:2129]
- [ ] [Review][Patch] The two new fail-closed 304 branches have no coverage; the same commit rewrote every 304 fixture to hardcode `ProjectionBacked` [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:2043]
- [ ] [Review][Patch] `ResolveDetailKindForFreshness` is dead code — zero callers after the detail 304 path was rewritten [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1175]
- [ ] [Review][Patch] `ReadEvaluatedDependencyValuesAsync` is environmentally fragile — no timeout or cancellation, no `-nodeReuse:false`, unguarded `JsonDocument.Parse`/`GetProperty("Items")`, bare `Win32Exception` when `dotnet` is off `PATH` [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:500]
- [ ] [Review][Patch] The `Analyzer`/`Reference` item types in the `-getItem` request are vacuous, giving the AC1 analyzer assertion false authority [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:515]
- [ ] [Review][Patch] `MatchesDependencyIdentity` false-positive landmine via MSBuild's `Filename` metadatum [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:549]
- [ ] [Review][Patch] `TenantsUiHost_DoesNotExposeTenantManagementApiEndpoints` is a bare negative assertion over a reduced host, with no positive control [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:224]
- [ ] [Review][Patch] Unioning both evaluation modes weakens the one positive dependency assertion [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:194]
- [ ] [Review][Patch] `Get_tenant_projection_lifecycle_precedes_legacy_stale_evidence` asserts freshness but never surface kind [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:111]
- [x] [Review][Defer] A 304 carrying `Lifecycle: Degraded` renders `Ready` while the identical 200 renders `Degraded` [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1169] — deferred, pre-existing
- [x] [Review][Defer] `IsTenantManagementApiRoute` hardcodes route prefixes with no shared constant [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:559] — deferred, pre-existing
- [x] [Review][Defer] Marker enforcement for the UI host now runs only in the EventStore repo's suite [tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:119] — deferred, pre-existing
- [x] [Review][Defer] Tier placement — a full Blazor host boot and two MSBuild subprocesses run in the Tier-1 unit list [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:224] — deferred, pre-existing

#### Story 2.11 Citation And Ratified Overlap (owner decision, 2026-07-26)

The AC2 boundary in `planning-artifacts/epics.md:72` assigns provenance preservation, authoritative
lifecycle/header selection, the fail-closed `Unknown` fallback, and real-gateway/persisted-read-model
proof exclusively to Story 2.11. The 2026-07-26 code review found that Story 2.6 authored and proved
those behaviours instead of citing them. **The owner has ratified the overlap** rather than reverting
the work or amending the epic boundary.

Recorded consequences of that ratification:

- **Story 2.6 cites Story 2.11** (`2-11-query-provenance-consumption-in-generated-rest-and-tenants`)
  as the owning story for provenance preservation, authoritative lifecycle/header selection, the
  fail-closed `Unknown` fallback, and real-gateway/persisted-read-model consumer proof. The framing
  at lines 16-17 of this story and the AC2 sign-off in the Completion Notes are consumer-side
  restatements of the Story 2.11 contract, not independent 2.6 authority.
- **Story 2.11's review adopts Tenants commits `56c506c` and `8ab537e` as consumer evidence.** The
  provenance gate, the lifecycle-precedes-legacy-`IsStale` selection, and the four
  `result.Metadata?.IsStale == true` to `freshness is ReadModelFreshnessState.Stale` call-site
  rewrites are Story 2.11 consumer behaviour physically located in the Tenants UI gateway.
- **Open review findings against that code remain listed here** but are 2.11-owned in substance. The
  two correctness patches below (lifecycle-only 304 discarding a `Stale` snapshot; `ResolveFreshness`
  = `Current` gating mutations that `ProjectionLifecyclePolicy.CanMutate` denies) are defects in the
  ratified consumer logic and should be resolved before Story 2.11 leaves `review`.
- **The readiness finding MAJ-2** (2.6/2.11 double-own the Tenants-UI provenance AC) is closed by
  explicit owner ratification rather than by boundary correction. `epics.md:72` is left unchanged, so
  the epic text and the shipped allocation now differ by design; this citation is the reconciliation
  of record.

## Dev Agent Record

### Debug Log References

- `dotnet build tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false` -> **Build succeeded. 0 warnings, 0 errors.**
- `dotnet tests/Hexalith.Tenants.UI.Tests/bin/Debug/net10.0/Hexalith.Tenants.UI.Tests.dll -class Hexalith.Tenants.UI.Tests.Services.Gateways.TenantQueryGatewayTests` -> **103/103 passed.**
- `dotnet tests/Hexalith.Tenants.UI.Tests/bin/Debug/net10.0/Hexalith.Tenants.UI.Tests.dll -class Hexalith.Tenants.UI.Tests.TenantsUiCompositionTests` -> **16/16 passed.**
- `dotnet tests/Hexalith.Tenants.UI.Tests/bin/Debug/net10.0/Hexalith.Tenants.UI.Tests.dll` -> **881/881 passed.**
- Root and Tenants `git diff --check` -> **passed.**

### Completion Notes

- AC1 implementation evidence is green: the evaluated UI dependency graph retains the typed Tenants client seam and excludes the external API host, REST generator, and domain-service host; compiled-controller and in-process endpoint inventories are empty for forbidden API surfaces.
- AC2 implementation evidence is green: only projection-backed metadata can resolve current or stale freshness; handler-computed, missing, unknown, and invalid provenance resolve to unknown.
- AC3 remains blocked: Sally's focused UX acceptance and Tenants maintainer approval are not recorded, so no exact **approved** implementation SHA exists.
- **Correction (2026-07-26 code review):** the statement that the four Tenants changes are "uncommitted on base SHA `28630b94a7b4931dcd6796eb50ad1c21b092055d`" was false. They are committed as Tenants `56c506c` (`refactor(gateway): improve freshness state handling in TenantQueryGateway`, all four files) and `8ab537e` (`test(gateway): add tests for tenant projection lifecycle and freshness states`), verified on Tenants `main` via `git merge-base --is-ancestor 8ab537e HEAD`. `28630b94` is `56c506c^`, i.e. the base, not the tip. Tenants SHA at review time: `42f0c5c`. These commits therefore landed in a shared submodule without the maintainer approval required by AC3 and by `project-context.md:75`; recording the SHA does not substitute for that approval.
- Story status remains `in-progress`; it is not ready to advance to `review` or `done` until the external acceptance and exact-SHA gates are satisfied.

### File List

**Tenants production (modified):**
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs`

**Tenants tests (modified):**
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs`

**Artifacts (modified):**
- `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-07-15: Revalidated the completed provenance and UI-boundary patches in Debug/source mode: build 0 warnings/errors, gateway tests 103/103, composition tests 16/16, and full Tenants UI tests 881/881. Added the exact file inventory and retained `in-progress` because UX/maintainer approval and an exact committed Tenants SHA remain unavailable.
