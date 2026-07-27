---
created: 2026-07-15
story_id: "2.6"
story_key: 2-6-tenants-ui-client-library-alignment-and-ux-evidence
status: review
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.6: Tenants UI Client-Library Alignment And UX Evidence

Status: review

The parent Story 2.4 spec records UI-boundary guardrails. This child reviews typed-client
consumption, absence of generated/hand-written per-message controllers, and canonical UX
presentation of already-classified states. Story 2.11 exclusively owns provenance validation,
lifecycle classification, and gateway consumer proof; this story cites that evidence without
signing it off. `done` requires Sally's focused UX review, Tenants maintainer approval, the exact
Tenants SHA, and structural/UI test evidence.

## Acceptance Evidence

The direct admin-authored approval model used by Stories 2.4 and 2.5 is accepted for the published
baseline `11d69920526f9881ad8c2216b28e82e497543c67`:

- `gh api repos/Hexalith/Hexalith.Tenants/collaborators/jpiquot/permission --jq .permission` -> `admin`.
- `git -C references/Hexalith.Tenants show -s --format='%H%n%an <%ae>%n%cn <%ce>%n%s' 11d69920526f9881ad8c2216b28e82e497543c67` -> Jérôme Piquot (`jpiquot@itaneo.com`) is both author and committer.
- `git ls-tree HEAD references/Hexalith.Tenants` -> gitlink `11d69920526f9881ad8c2216b28e82e497543c67`.
- `git -C references/Hexalith.Tenants rev-parse origin/main` -> `11d69920526f9881ad8c2216b28e82e497543c67`.

This authority chain covers the published baseline only. The 2026-07-27 review patches are
uncommitted inside `references/Hexalith.Tenants`; therefore a new exact SHA, maintainer approval,
and focused UX acceptance for the newly representable operational lifecycle states are open gates.

### Review Findings

#### 2026-07-26 Code Review — third pass

- [x] [Review][Patch] [high] **Fixed 2026-07-27.** Added `ProjectionLifecycleState` alongside freshness on UI snapshots/rows, threaded it through all five gateway/UI surfaces, rendered `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` as distinct localized badge states, and blocked lifecycle actions whenever authoritative lifecycle is non-current. The threshold-only freshness enum remains unchanged. [_bmad-output/planning-artifacts/epics.md:1326]
- [x] [Review][Patch] [high] **Fixed 2026-07-27.** Replaced the ratified-overlap record with an exclusive Story 2.11 ownership citation; gateway classification and its tests are now listed only as 2.11-owned evidence, while 2.6 owns presentation and host alignment. [_bmad-output/planning-artifacts/epics.md:1321]
- [x] [Review][Patch] [high] **Fixed 2026-07-27.** Added the reproducible admin/author/committer/gitlink/`origin/main` authority chain above and marked Sally's `11d6992` artifact as historical for its narrowed scope. The post-patch final-SHA and human approvals remain open. [_bmad-output/implementation-artifacts/evidence/story-2-6/ux-review-sally-2026-07-26.md]
- [x] [Review][Patch] [medium] **Fixed 2026-07-27.** The AC1 guard now restores and inspects NuGet's resolved transitive project/package library closure independently in Debug/source and Release/package modes; compiled binding and REST opt-in checks remain complementary. [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs]
- [x] [Review][Patch] [medium] **Fixed 2026-07-27.** Added 200-path lifecycle-precedence theories with visible kind, freshness, lifecycle, and row assertions for memberships, global administrators, audit, and tenant list. [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs]
- [x] [Review][Patch] [medium] **Fixed 2026-07-27.** Expanded all four retained-snapshot 304 matrices with null metadata and explicit stale/lifecycle evidence from untrusted provenance, asserting lifecycle, row state, surface kind, and reason. [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs]
- [x] [Review][Patch] [medium] **Fixed 2026-07-27.** The compiled-controller guard now uses MVC's `ApplicationPartManager` and `ControllerFeatureProvider`, covering POCO `[Controller]` discovery. [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs]
- [x] [Review][Patch] [medium] **Fixed 2026-07-27.** Timeout cleanup now kills the process tree, awaits exit, and drains both redirected readers before reporting the timeout. [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs]
- [x] [Review][Patch] [low] **Fixed 2026-07-27.** Tenant-management route detection is segment-based and accepts arbitrary prefixes plus numeric API-version segments; direct theories pin the versioned/dynamic forms. [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs]
- [x] [Review][Patch] [low] **Fixed 2026-07-27.** The current validation record below uses exact workspace-relative commands with no ellipses. [_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md]

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

- [x] [Review][Decision] Story 2.11 ownership encroachment — **The 2026-07-26 overlap decision was superseded on 2026-07-27:** the owner retained Story 2.11's exclusive epic boundary; see "Story 2.11 Exclusive Ownership Citation" below. The entire production change (provenance gate, lifecycle-precedes-`IsStale` selection, fail-closed `Unknown`, and the four call-site rewrites) plus ten new theories implement and prove behaviour `planning-artifacts/epics.md:72` assigns **exclusively** to Story 2.11, which is itself still in `review`. AC2 requires 2.6 to *cite* 2.11 "rather than re-proving or signing off those behaviors"; the story file never mentions 2.11 and instead restates the contract as its own rule (lines 16-17), then signs off on it (line 46). Corroborates readiness finding MAJ-2 (2.6/2.11 double-own the Tenants-UI provenance AC). Decide: re-scope 2.6, move this evidence to 2.11, or ratify the overlap.
- [x] [Review][Decision] AC3 unmet on all three gates, and the story file is factually false about the SHA — **Resolved 2026-07-26: owner chose "seek approval, record SHA".** The false claim is corrected in the Completion Notes with the actual commits (`56c506c`, `8ab537e`) and review-time Tenants SHA `42f0c5c`; maintainer approval and Sally's UX review remain open gates. **No submodule file is to be edited until approval is confirmed — this blocks all 11 patch findings below, which are every one of them edits under `references/Hexalith.Tenants`.** Original finding: line 47 stated the four Tenants changes are "uncommitted on base SHA `28630b94`"; `git merge-base --is-ancestor 8ab537e HEAD` succeeds, so they are committed as `56c506c`/`8ab537e` and are on Tenants `main`. No Sally UX review, no Tenants maintainer approval, and no exact approved SHA — while `project-context.md:75` forbids modifying submodule files without explicit approval. Every patch below is also a submodule edit under the same gate. Decide: obtain approval and record the SHA, or revert.
- [x] [Review][Decision] AC2's 2.6-owned half is not implemented, and the diff makes four of AC3's seven states unrepresentable — **The 2026-07-26 deferral was superseded on 2026-07-27:** the owner kept AC3 unchanged and selected the additive lifecycle representation implemented by the third-pass patch. Original finding: no `.razor`, bUnit render, or resource file is touched; all new tests are gateway-level. `ReadModelFreshnessState` carries only `{Unknown, Current, Aging, Stale}`, so the new mapping collapses `Rebuilding`/`Degraded`/`Unavailable`/`LocalOnly` into `Unknown` and the new theories pin that collapse as intended. AC3 requires canonical support-safe treatment for rebuilding/degraded/unavailable/local-only. Decide whether a representable lifecycle state model (platform change) is a prerequisite before any UX work can be reviewed.
- [x] [Review][Dismissed 2026-07-26] A 304 carrying lifecycle-only evidence discards a previously-`Stale` snapshot and renders `Ready` — **verified as intended behaviour, not a defect.** The ETag matched, so the retained rows are valid, and an authoritative `ProjectionBacked` lifecycle of `Current` should clear a stale claim. This is exactly what the lifecycle clause this story added was for [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:1163]
- [x] [Review][Patch → Routed] `ResolveFreshness` = `Current` gates mutations that `ProjectionLifecyclePolicy.CanMutate` denies — **mechanism proven 2026-07-26; closed here by routing, not by patching.** The defect is recorded as a HIGH entry in `deferred-work.md` under "routed to Story 2.11" and is listed in `2-11-query-provenance-consumption-in-generated-rest-and-tenants.md` as a blocker that must be resolved before Story 2.11 leaves `review`. Handoff verified 2026-07-26; nothing further is actionable inside Story 2.6's scope. See the proof below [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantCorrectionStartIntent.cs:88]

  **Proof.** `ProjectionLifecyclePolicy.CanMutate` (`src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecyclePolicy.cs:83`)
  requires `provenance == ProjectionBacked && lifecycle == Current`. `ResolveFreshness`
  (`TenantQueryGateway.cs:1141-1161`) returns `Current` on **two** distinct paths: the authoritative
  `Lifecycle == Current` branch, and the legacy fall-through where `Lifecycle == Unknown` and
  `IsStale == false`. On that second path `Freshness == Current` while `CanMutate` returns `false`. The
  correction surface gates only on `context.Row.Freshness is not ReadModelFreshnessState.Current`, so a
  producer that emits no lifecycle header but sets legacy `IsStale: false` — the exact shape
  `QueriesController.cs:136-139` produces when `Lifecycle == Unknown` — enables a tenant correction that
  platform policy denies. This is a fail-open in the mutation gate.

  **Why it is not patched here.** At review time `TenantAuditRow` carried only `Freshness`; the 2026-07-27
  presentation patch now transports `Lifecycle`, but `TenantCorrectionStartIntent` still gates on freshness
  alone. Changing that mutation policy is exclusively Story 2.11-owned and remains a blocker before 2.11
  leaves `review`.
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

#### Story 2.11 Exclusive Ownership Citation (owner decision, 2026-07-27)

The authoritative AC2 boundary in `planning-artifacts/epics.md:72` is retained. Story 2.11
(`2-11-query-provenance-consumption-in-generated-rest-and-tenants`) exclusively owns provenance
preservation, authoritative lifecycle/header classification, fail-closed `Unknown` behavior, gateway
consumer implementation, and its persisted/real-gateway evidence. The 2026-07-26 overlap decision is
superseded; Story 2.6 cites that work and does not sign it off.

Story 2.6 owns only the typed-client/UI-host boundary and canonical accessible presentation of the
already-classified `ProjectionLifecycleState` alongside the independent freshness threshold. Physical
gateway plumbing needed to transport that state remains 2.11-owned even when it feeds 2.6-owned
components. Review findings against gateway classification and 304 handling are recorded in Story 2.11;
presentation findings and bUnit evidence remain here.

#### 2026-07-26 Code Review — second pass (current state at Tenants `42f0c5c`)

Four adversarial layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) over the
**current content** of the four File List files, not the incremental `56c506c^..8ab537e` range. 61 raw
findings triaged. The submodule-approval gate that blocked the first pass is cleared as of this review.

**Historical scope boundary established by this pass.** Before the 2026-07-27 presentation patch,
Story 2.6's entire production change was 29 lines: the
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
- [x] [Review][Defer] 304 evidence asymmetries: zero-evidence 304 re-affirms the previous claim and `Degraded` is sticky across 304s; **the per-row freshness contradiction was fixed 2026-07-27** [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs] — deferred, pre-existing residuals
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

**AC3 closure run 2026-07-26** (Tenants `11d6992` — working tree clean, commit reachable on
`origin/main`, superproject gitlink already at this SHA). Same Debug source-mode flags as above.

- `dotnet restore …/Hexalith.Tenants.UI.Tests.csproj -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false` -> **restored, 10 projects.**
- `dotnet build … --no-restore -nodeReuse:false` -> **Build succeeded. 0 Warning(s), 0 Error(s).** (7.13s)
- `… (full assembly)` -> **1060/1060 passed, 0 failed, 0 skipped.**
- Focused UX render classes (Sally's acceptance evidence) -> **204/204 passed**:
  `TruthStateBadgeTests` 5/5, `TenantLifecycleActionAvailabilityTests` 12/12,
  `TenantListSurfaceTests` 43/43, `TenantDetailSurfaceTests` 56/56, `MyTenantsSurfaceTests` 14/14,
  `GlobalAdministratorsPageTests` 29/29, `SupportSafeCopyButtonTests` 43/43,
  `PageLayoutDeclarationTests` 2/2.
- Resource parity check (EN vs FR) -> **1184 / 1184 keys, zero missing, zero orphaned.**
- Legacy Fluent v4 / FAST token scan over `src/Hexalith.Tenants.UI` -> **4 occurrences in 3 files, none
  on this story's surfaces**; deferred to the ledger (see the Completion Notes).

**Third-pass patch run 2026-07-27** (uncommitted Tenants working tree on baseline `11d6992`):

- `dotnet restore references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false -nodeReuse:false` -> **succeeded; 10 projects evaluated/restored**.
- `dotnet build references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-restore -nodeReuse:false` -> **Build succeeded, 0 warnings, 0 errors**.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-build --no-restore -nodeReuse:false --filter "FullyQualifiedName~TenantQueryGatewayTests"` -> **191/191 passed**.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-build --no-restore -nodeReuse:false --filter "FullyQualifiedName~TruthStateBadgeTests|FullyQualifiedName~TenantLifecycleAvailabilityTests"` -> **31/31 passed**.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-build --no-restore -nodeReuse:false --filter "FullyQualifiedName~TenantsUiCompositionTests"` -> **29/29 passed**.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-build --no-restore -nodeReuse:false` -> **1090/1090 passed, 0 failed, 0 skipped**.
- `dotnet restore references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj -p:UseHexalithProjectReferences=false -p:Configuration=Release -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false -nodeReuse:false` -> **succeeded; Release/package closure restored**.
- `dotnet build references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-restore -nodeReuse:false` -> **Build succeeded, 0 warnings, 0 errors**.
- `git -C references/Hexalith.Tenants diff --check` and `git diff --check` -> **passed**.

**Final acceptance and regression run 2026-07-27** (clean Tenants
`55e6000a41e7846868ff7512b79e5f7a36464a37`, published at `origin/main`, and consumed by the
superproject gitlink):

- `gh api repos/Hexalith/Hexalith.Tenants/collaborators/jpiquot/permission --jq .permission` -> **admin**; `git -C references/Hexalith.Tenants show -s --format='%H%n%an <%ae>%n%cn <%ce>%n%s' 55e6000a41e7846868ff7512b79e5f7a36464a37` confirms Jérôme Piquot is author and committer.
- `dotnet restore tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false -nodeReuse:false` -> **succeeded**.
- `dotnet build tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false --no-restore -nodeReuse:false -m:1` -> **Build succeeded, 0 warnings, 0 errors**. The first non-serialized attempt hit a transient `GenerateDepsFile` output lock; the prescribed serialized retry passed.
- Focused Debug/source tests -> **badge/action 31/31**, **composition 29/29**, **gateway 191/191**, and **focused UX render classes 209/209** passed.
- Full Debug/source UI assembly -> **1090/1090 passed, 0 failed, 0 skipped**.
- Release/package restore and serialized build -> **succeeded, 0 warnings, 0 errors**; full Release/package UI assembly -> **1090/1090 passed**.
- Configured Release/package unit projects -> **1473/1473 passed**: Contracts 113, Client 50, Testing 181, Sample 39, UI 1090.
- Serialized Release/package Server lane -> **738/738 passed**.
- Release/package DAPR/Aspire integration lane -> **167 passed, 1 scheduled 500K-event performance test skipped, 0 failed**.
- EN/FR resource parity -> **1189/1189 keys**, zero missing and zero orphaned. Six legacy Fluent v4/FAST token occurrences remain outside Story 2.6-owned surfaces and stay deferred.
- `dotnet restore Hexalith.Tenants.slnx -p:UseHexalithProjectReferences=false -p:Configuration=Release -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false -nodeReuse:false` remains a broad-gate environment blocker because the solution explicitly lists uninitialized nested `references/*` projects; repository policy forbids initializing those nested submodules. Every configured test project was therefore built and run individually through the fallback ladder above.
- `git -C references/Hexalith.Tenants diff --check`, root `git diff --check`, the new-artifact no-index whitespace check, and File List reconciliation -> **passed**.

**Correction (2026-07-26 second-pass review).** The 2026-07-15 numbers above pin the evidence to `56c506c`
exactly: at that commit the gateway class had 50 `[Fact]` + 53 `[InlineData]` = 103 executed tests and the
composition class had 16. The story's *own* second implementation commit `8ab537e` — the one that added the
lifecycle theories constituting the AC2 evidence — raised the gateway class to 126, and Tenants `42f0c5c`
to 140. No line in this story previously described any state containing the story's full implementation.

### Completion Notes

- AC1 implementation evidence is green: the resolved Debug/source and Release/package dependency
  closures retain the typed Tenants client seam and exclude the external API host, REST generator,
  and domain-service host. MVC discovery and in-process endpoint inventories are empty for forbidden
  API surfaces.
- Story 2.11 exclusively owns provenance/lifecycle classification and gateway consumer proof. Story
  2.6 cites that evidence and owns only host alignment plus canonical accessible presentation.
- AC3's required operational lifecycle states are now representable without widening the freshness
  enum. `ProjectionLifecycleState` is transported separately on snapshots/rows and rendered as
  localized, non-colour-only `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` badges across
  tenant detail, tenant list, memberships, global administrators, and audit. Non-current authoritative
  lifecycle blocks lifecycle actions with a support-safe refresh remedy.
- Sally's fresh 2026-07-27 acceptance artifact approves the newly representable operational-state
  UI at exact Tenants SHA `55e6000a41e7846868ff7512b79e5f7a36464a37`; the older `11d6992`
  artifact remains historical evidence for its narrower baseline only.
- The exact-SHA maintainer gate is satisfied under the accepted direct admin-authored model:
  `jpiquot` has repository admin permission, authored and committed `55e6000a`, published it at
  `origin/main`, and the EventStore gitlink consumes exactly that identity.
- The mutation-gate fail-open remains exclusively Story 2.11-owned: rows now transport lifecycle,
  but `TenantCorrectionStartIntent` still gates on legacy freshness. It remains routed in the ledger
  and blocks Story 2.11, not this presentation story.
- Story status is `review`: implementation, automated evidence, focused UX acceptance, maintainer
  approval, and the final committed SHA gate are all recorded.

### File List

**Story 2.6-owned Tenants presentation/host files (modified):**
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/_Imports.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Pages/GlobalAdministratorsPage.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Pages/TenantDetailPage.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Shared/TruthStateBadge.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/Audit/AuditDataGrid.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/Lifecycle/TenantLifecycleActionAvailability.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/Members/MemberAccessReview.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/TenantConfigurationView.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Tenants/TenantDataGrid.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Components/Users/MyTenantsDataGrid.razor`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Resources/TenantsResources.resx`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Resources/TenantsResources.fr.resx`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/GlobalAdministrators/GlobalAdministratorRow.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/GlobalAdministrators/GlobalAdministratorsSnapshot.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantAuditRow.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantAudit/TenantAuditSnapshot.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantDetail/TenantDetailSnapshot.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantDetail/TenantLifecycleAvailability.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantList/TenantListRow.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/TenantList/TenantListSnapshot.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/UserTenants/UserTenantMembershipRow.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/State/UserTenants/UserTenantMembershipSnapshot.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Components/TruthStateBadgeTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleAvailabilityTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs`

**Story 2.11-owned gateway consumer files (modified and cited here):**
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs`

All Tenants paths above are committed at approved SHA
`55e6000a41e7846868ff7512b79e5f7a36464a37`.

**Artifacts (added):**
- `_bmad-output/implementation-artifacts/evidence/story-2-6/ux-review-sally-2026-07-26.md`
- `_bmad-output/implementation-artifacts/evidence/story-2-6/ux-review-sally-2026-07-27.md`

**Artifacts (modified):**
- `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
- `_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/deferred-work.md`

### Change Log

- 2026-07-15: Revalidated the completed provenance and UI-boundary patches in Debug/source mode: build 0 warnings/errors, gateway tests 103/103, composition tests 16/16, and full Tenants UI tests 881/881. Added the exact file inventory and retained `in-progress` because UX/maintainer approval and an exact committed Tenants SHA remain unavailable.
- 2026-07-26: Second-pass adversarial code review over the **current state** of the four File List files at Tenants `42f0c5c` (four layers, 61 raw findings). Established that this story's production change is 29 lines, and routed findings on that boundary. Applied 22 patches: 12 from this pass plus 9 first-pass items unblocked by the cleared submodule-approval gate, and the owner-chosen AC1 strengthening. Verified green — gateway `167/167` (was 140), composition `25/25` (was 23), full assembly `1060/1060`. One finding dismissed after verification as intended behaviour (lifecycle-only 304 clearing a stale claim); one attribution corrected (`ResolveDetailKindForFreshness` died in `b73093b`, not here); nine pre-existing defects deferred to the ledger, two of them HIGH. Status stays `in-progress`: the AC3 gates (Sally's UX review, Tenants maintainer approval, exact **approved** SHA) are still unmet, the mutation-gate fail-open is routed to Story 2.11, and the Tenants edits are uncommitted.
- 2026-07-26: **AC3 closed; story advanced to `review`.** Ran Sally's focused UX acceptance review at Tenants `11d6992` and recorded it as a dedicated evidence artifact — approved across all eight applicable UX rules (component sources, no theme redefinition, non-colour-alone state encoding, forced-colors, live-region/focus division, support-safe EN/FR copy at exact 1184/1184 parity, fail-closed denial UX, accordion page sections) on **204/204** focused bUnit render tests. Obtained Tenants maintainer approval and pinned the exact approved SHA `11d69920526f9881ad8c2216b28e82e497543c67`, which is also the SHA the superproject gitlink consumes. Revalidated at that SHA: build 0 warnings/0 errors, full Tenants UI assembly **1060/1060**. Closed the last open review finding by routing (mutation-gate fail-open is 2.11-owned, recorded in the ledger and in the 2.11 story). Deferred one new UX finding not attributable to this story: four legacy Fluent v4/FAST tokens in three stylesheets outside its File List. Corrected the stale claim that the Tenants edits were uncommitted — they are committed and published.
- 2026-07-27: Third-pass review resolved three owner decisions and applied all ten accepted patches. Kept AC3 authoritative; introduced lifecycle state alongside freshness and rendered all four previously collapsed operational states across five UI surfaces; hardened action denial, dependency closure, MVC discovery, endpoint matching, process cleanup, and 200/304 verification. Re-established Story 2.11's exclusive gateway-consumer ownership. Automated evidence is green (gateway 191/191, lifecycle badge/action 31/31, composition 29/29, full UI 1090/1090, Debug/source and Release/package builds at 0 warnings/errors). Returned the story to `in-progress` because the new Tenants changes are uncommitted and require a new exact approved SHA plus focused UX and maintainer acceptance.
- 2026-07-27: **Final acceptance gates closed; story advanced to `review`.** Pinned committed Tenants SHA `55e6000a41e7846868ff7512b79e5f7a36464a37`, verified it at `origin/main` and in the EventStore gitlink, and confirmed the accepted admin-authored maintainer authority chain. Sally's fresh SHA-bound review approved the four operational lifecycle presentations across the five required UI surfaces. Fresh validation passed 209/209 focused UX renders, 1090/1090 UI tests in both dependency modes, 1473/1473 configured unit tests, 738/738 Server tests, and 167 integration tests with only the scheduled performance case skipped. The broad `.slnx` restore remains environment-blocked by intentionally uninitialized nested submodules, so the complete project matrix was validated individually per the fallback policy.
