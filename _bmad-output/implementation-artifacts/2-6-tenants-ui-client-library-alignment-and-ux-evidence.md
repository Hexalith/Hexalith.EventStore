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
- [x] [Review][Dismissed 2026-07-26] A 304 carrying lifecycle-only evidence discards a previously-`Stale` snapshot and renders `Ready` — **verified as intended behaviour, not a defect.** The ETag matched, so the retained rows are valid, and an authoritative `ProjectionBacked` lifecycle of `Current` should clear a stale claim. This is exactly what the lifecycle clause this story added was for [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1163]
- [ ] [Review][Patch] `ResolveFreshness` = `Current` gates mutations that `ProjectionLifecyclePolicy.CanMutate` denies — **mechanism proven 2026-07-26; deliberately left open, see below** [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantCorrectionStartIntent.cs:88]

  **Proof.** `ProjectionLifecyclePolicy.CanMutate` (`src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecyclePolicy.cs:83`)
  requires `provenance == ProjectionBacked && lifecycle == Current`. `ResolveFreshness`
  (`TenantQueryGateway.cs:1141-1161`) returns `Current` on **two** distinct paths: the authoritative
  `Lifecycle == Current` branch, and the legacy fall-through where `Lifecycle == Unknown` and
  `IsStale == false`. On that second path `Freshness == Current` while `CanMutate` returns `false`. The
  correction surface gates only on `context.Row.Freshness is not ReadModelFreshnessState.Current`, so a
  producer that emits no lifecycle header but sets legacy `IsStale: false` — the exact shape
  `QueriesController.cs:136-139` produces when `Lifecycle == Unknown` — enables a tenant correction that
  platform policy denies. This is a fail-open in the mutation gate.

  **Why it is not patched here.** `TenantAuditRow` carries only `Freshness`; it carries neither
  `Provenance` nor `Lifecycle`, so `TenantCorrectionStartIntent` cannot call `CanMutate` at all. Closing
  this requires threading the authoritative evidence through the row or the intent context — a production
  design change in shared submodule code, with more than one reasonable shape (widen the row, add a
  gateway-computed `CanMutate` flag, or have the gateway stop deriving `Current` from legacy evidence).
  The "Story 2.11 Citation And Ratified Overlap" section already assigns this defect to Story 2.11 and
  requires it to be resolved before 2.11 leaves `review`. Routing it there rather than inventing a shape.
- [x] [Review][Patch] **Fixed 2026-07-26.** Lifecycle-on-304 and lifecycle-over-`IsStale` precedence are verified on the tenant-detail surface only; four of five surfaces sharing the changed helper are unexercised [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:2129]
- [x] [Review][Patch] **Fixed 2026-07-26.** The two new fail-closed 304 branches have no coverage; the same commit rewrote every 304 fixture to hardcode `ProjectionBacked` [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:2043]
- [x] [Review][Defer] `ResolveDetailKindForFreshness` is dead code — **attribution corrected 2026-07-26: not caused by Story 2.6.** `git log -S` places the caller removal in Tenants `b73093b` (tenant configuration read policy), which post-dates `8ab537e`; this story's production diff never mentions the symbol. Deferred to the owning story [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1175]
- [x] [Review][Patch] **Fixed 2026-07-26** (3-minute timeout + `Kill(entireProcessTree: true)`, `-nodeReuse:false`, guarded `JsonDocument.Parse`, `TryGetProperty("Items")`, and a diagnostic `Win32Exception` wrap). `ReadEvaluatedDependencyValuesAsync` is environmentally fragile — no timeout or cancellation, no `-nodeReuse:false`, unguarded `JsonDocument.Parse`/`GetProperty("Items")`, bare `Win32Exception` when `dotnet` is off `PATH` [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:500]
- [x] [Review][Patch] **Fixed 2026-07-26.** The `Analyzer`/`Reference` item types in the `-getItem` request are vacuous, giving the AC1 analyzer assertion false authority [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:515]
- [x] [Review][Patch] **Fixed 2026-07-26.** `MatchesDependencyIdentity` false-positive landmine via MSBuild's `Filename` metadatum [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:549]
- [x] [Review][Patch] **Fixed 2026-07-26.** `TenantsUiHost_DoesNotExposeTenantManagementApiEndpoints` is a bare negative assertion over a reduced host, with no positive control [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:224]
- [x] [Review][Patch] **Fixed 2026-07-26.** Unioning both evaluation modes weakens the one positive dependency assertion [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:194]
- [x] [Review][Patch] **Fixed 2026-07-26.** `Get_tenant_projection_lifecycle_precedes_legacy_stale_evidence` asserts freshness but never surface kind [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:111]
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

#### 2026-07-26 Code Review — second pass (current state at Tenants `42f0c5c`)

Four adversarial layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) over the
**current content** of the four File List files, not the incremental `56c506c^..8ab537e` range. 61 raw
findings triaged. The submodule-approval gate that blocked the first pass is cleared as of this review.

**Scope boundary established by this pass.** Story 2.6's entire production change is 29 lines: the
provenance gate plus lifecycle-precedence in `ResolveFreshness` / `ResolveNotModifiedFreshness`, and four
`result.Metadata?.IsStale == true` → `freshness is ReadModelFreshnessState.Stale` call-site rewrites.
Everything else in these 4,329 lines arrived from later stories (support-safe copy, tenant configuration,
cursor paging, Memories search). Findings are routed on that boundary.

- [x] [Review][Decision] The AC1 structural test cannot see transitively acquired dependencies — **Resolved 2026-07-26: owner chose "strengthen the technique."** The fix is to inspect the compiled UI assembly's referenced-assembly closure (and generated-source output) rather than the MSBuild evaluation, so a REST generator arriving through `Hexalith.Tenants.Client` becomes visible. This resolution absorbs the `Analyzer`/`Reference` vacuity patch and the union/empty-set patch below. **Correction to the original finding:** the review first reported the `Hexalith.Tenants.Client` reference as inert because no UI source has a `using` for it. That framing is wrong. `Hexalith.Tenants.Client.csproj` is the *sole* supply path for both `Hexalith.Tenants.Contracts` and `Hexalith.EventStore.Client` into the UI — the UI declares neither directly (`Hexalith.Tenants.UI.csproj` contains zero references to either), and `TenantQueryGateway.cs:4-11` imports both. Deleting that one reference breaks the build, so the positive assertion is load-bearing and AC1's "uses Tenants/EventStore client libraries" is honestly satisfied. The real defect is confined to the three **negative** assertions: `-getItem` is evaluation-time and reads only the project's own declared items, so anything `Hexalith.Tenants.Client` pulls in is invisible to all three. Measured control: on `Hexalith.Tenants.Api`, which does carry the generator, it appears only under `ProjectReference`; `Analyzer` returns the same four SDK built-ins for both projects and `Reference` returns zero [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:198]
- [x] [Review][Patch] The 304 provenance gate and lifecycle clause this story added have zero test coverage on all four consuming surfaces [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1163]
- [x] [Review][Patch] `Get_tenant_not_modified_applies_authoritative_lifecycle_evidence` is tautological — the detail path discards the 304 wholesale, and both fixtures carry identical metadata [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:184]
- [x] [Review][Patch] Recorded validation evidence (103/103, 16/16) predates half this story's own implementation; current state is 140/140 and 23/23 [_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md:90]
- [x] [Review][Patch] The `Analyzer` and `Reference` item types in the `-getItem` request carry no signal, so the REST-generator assertion cannot see a transitively acquired generator [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:515]
- [x] [Review][Patch] The dependency test passes vacuously on an empty item set, and its one positive assertion runs over the union of both evaluation modes [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:194]
- [x] [Review][Patch] `TenantsUiHost_DoesNotExposeTenantManagementApiEndpoints` has no positive control, and its `ControllerActionDescriptor` clause is unsatisfiable because the host never calls `AddControllers()` [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:224]
- [x] [Review][Patch] `ReadEvaluatedDependencyValuesAsync` has no timeout or cancellation on the MSBuild subprocess [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:500]
- [x] [Review][Patch] AC2's 2.6-owned half has no cited evidence — the Completion Note certifies only the 2.11-owned provenance behaviour [_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md:99]
- [x] [Review][Patch] `MatchesDependencyIdentity` false-positive via MSBuild's `Filename` metadatum, confirmed by measurement [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:549]
- [x] [Review][Patch] The tenant-list surface has no provenance-gate theory while the other four surfaces each have one [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:90]
- [x] [Review][Patch] AC1's "no assembly opt-in" clause has no proof anywhere in the Tenants suite [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:170]
- [x] [Review][Patch] `sprint-status.yaml` still routes AD-17 command-status `Location` enforcement to "Story 2.6" after the renumber moved it to Story 2.9 [_bmad-output/implementation-artifacts/sprint-status.yaml:271]
- [x] [Review][Defer] `EnrichRowsAsync`/`LoadTenantDetailAsync` publish member/owner counts with none of the guards the search path applies — no tenant-identity check, no `Members` null guard, no `IsDegraded` check [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1009] — deferred, pre-existing
- [x] [Review][Defer] A single row's detail failure outside `{403,404,503}` destroys the whole fetched tenant list [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1024] — deferred, pre-existing
- [x] [Review][Defer] Non-`EventStoreGatewayException` failures escape four of the six public read methods [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:283] — deferred, pre-existing
- [x] [Review][Defer] 304 evidence asymmetries: zero-evidence 304 re-affirms the previous claim, per-row `Freshness` is never rewritten, and `Degraded` is sticky across 304s [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1163] — deferred, pre-existing
- [x] [Review][Defer] `ResolveTenantListKindForFreshness` lacks the surface-kind whitelist its three siblings have, promoting `Error`/`Unauthorized` to `Empty` [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1267] — deferred, pre-existing
- [x] [Review][Defer] `ReadModelFreshnessState.Aging` is absent from the mutation-blocking set, a latent fail-open for the deferred read-model-age work [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantDetail/TenantLifecycleAvailability.cs:42] — deferred, pre-existing
- [x] [Review][Defer] A search term containing a control character silently becomes an unfiltered full list with no notice [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:550] — deferred, pre-existing
- [x] [Review][Defer] `payload.Items == null` throws `NullReferenceException` on three surfaces with no generic exception handler [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:244] — deferred, pre-existing
- [x] [Review][Defer] Test-quality cluster: fixture queue-underflow keeps two tests green, `ContentSnippet` safety claim is asserted nowhere, `TenantDetailSnapshot.ErrorMessage` has zero consumers, French resource strings lack accents, and two tests couple CI to documentation prose [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs:501] — deferred, pre-existing

**Attribution correction.** The open finding above at line 46 states `ResolveDetailKindForFreshness` became
dead "after the detail 304 path was rewritten". That rewrite was **not** Story 2.6: `git log -S` shows the
caller was removed in Tenants `b73093b` ("implement tenant configuration read policy and management
context"), which post-dates `8ab537e`. Story 2.6's production diff does not mention the symbol at all. The
dead code is real but belongs to the tenant-configuration story.

**Dismissed after verification (6).** Notably the open finding at line 42 — "a 304 carrying lifecycle-only
evidence discards a previously-`Stale` snapshot and renders `Ready`" — is the *intended* behaviour of the
change this story made: the ETag matched, so the retained rows are valid, and an authoritative
`ProjectionBacked` lifecycle of `Current` should clear a stale claim. Also dismissed: the theory that the
four call-site rewrites fail open when provenance is not projection-backed. Verified they do not —
`HandlerComputed` + `IsStale: true` now yields `Ready(composition, eTag, freshness)` carrying
`Freshness = Unknown`, and `TenantLifecycleAvailability.Evaluate` blocks mutations on `Unknown`, so the
mutation gate stays fail-closed while the badge stops over-claiming `Stale` on non-projection evidence.

## Dev Agent Record

### Debug Log References

**Superseded 2026-07-15 run** (state at Tenants `56c506c`, i.e. BEFORE this story's second implementation
commit `8ab537e` added the AC2 lifecycle theories — see the correction below):

- `dotnet build … --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false` -> **Build succeeded. 0 warnings, 0 errors.**
- `… -class …TenantQueryGatewayTests` -> **103/103 passed.**
- `… -class …TenantsUiCompositionTests` -> **16/16 passed.**
- `… (full assembly)` -> **881/881 passed.**
- Root and Tenants `git diff --check` -> **passed.**

**Current run 2026-07-26** (Tenants `42f0c5c` plus the second-pass review patches). Note the added
`-p:HexalithCommonsFromSource=false`: without it the source-built `Hexalith.Commons.UniqueIds` 3.82.0
collides with the cached 2.28.2 package and the build fails with `CS1704`.

- `dotnet build /…/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-restore -nodeReuse:false` -> **Build succeeded, 7.03s.**
- `… -class …Services.Gateways.TenantQueryGatewayTests` -> **167/167 passed** (140 before this pass; +27 from the new 304 provenance/lifecycle theories and the tenant-list provenance theory).
- `… -class …TenantsUiCompositionTests` -> **25/25 passed** (23 before; +2 from splitting the dependency test into a per-mode theory and adding the compiled-assembly seam test).
- `… (full assembly)` -> **1060/1060 passed, 0 failed, 0 skipped.**

**Correction (2026-07-26 second-pass review).** The 2026-07-15 numbers above pin the evidence to `56c506c`
exactly: at that commit the gateway class had 50 `[Fact]` + 53 `[InlineData]` = 103 executed tests and the
composition class had 16. The story's *own* second implementation commit `8ab537e` — the one that added the
lifecycle theories constituting the AC2 evidence — raised the gateway class to 126, and Tenants `42f0c5c`
to 140. No line in this story previously described any state containing the story's full implementation.

### Completion Notes

- AC1 implementation evidence is green: the evaluated UI dependency graph retains the typed Tenants client seam and excludes the external API host, REST generator, and domain-service host; compiled-controller and in-process endpoint inventories are empty for forbidden API surfaces.
- AC2 has two halves and they carry different authority.
  - **The Story 2.11-owned half** (provenance preservation, authoritative lifecycle/header selection,
    fail-closed `Unknown`) is implemented in this repo's consumer code — only projection-backed metadata
    can resolve current or stale freshness; handler-computed, missing, unknown, and invalid provenance
    resolve to unknown. Per the ratified overlap above this is **cited from Story 2.11, not signed off
    here**; the statement is a consumer-side restatement of the 2.11 contract.
  - **The Story 2.6-owned half** — "the UI applies the canonical support-safe and accessible treatment
    for the supplied lifecycle state" — is evidenced by the existing bUnit render suite, which the story
    previously failed to cite at all. Twenty component test files in
    `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Components/` render against
    `ReadModelFreshnessState`, including `TruthStateBadgeTests.cs` (the canonical badge treatment per
    lifecycle state), `TenantDetailSurfaceTests.cs`, `TenantListSurfaceTests.cs`,
    `MyTenantsSurfaceTests.cs`, `GlobalAdministratorsPageTests.cs`, and
    `TenantLifecycleActionAvailabilityTests.cs` (mutation availability per freshness). Scope is the
    states the current model can express (`Current`, `Stale`, `Unknown`) plus the denied/loading/
    unavailable surface states the UI already owns, per the D3 owner decision.
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
- 2026-07-26: Second-pass adversarial code review over the **current state** of the four File List files at Tenants `42f0c5c` (four layers, 61 raw findings). Established that this story's production change is 29 lines, and routed findings on that boundary. Applied 22 patches: 12 from this pass plus 9 first-pass items unblocked by the cleared submodule-approval gate, and the owner-chosen AC1 strengthening. Verified green — gateway `167/167` (was 140), composition `25/25` (was 23), full assembly `1060/1060`. One finding dismissed after verification as intended behaviour (lifecycle-only 304 clearing a stale claim); one attribution corrected (`ResolveDetailKindForFreshness` died in `b73093b`, not here); nine pre-existing defects deferred to the ledger, two of them HIGH. Status stays `in-progress`: the AC3 gates (Sally's UX review, Tenants maintainer approval, exact **approved** SHA) are still unmet, the mutation-gate fail-open is routed to Story 2.11, and the Tenants edits are uncommitted.
