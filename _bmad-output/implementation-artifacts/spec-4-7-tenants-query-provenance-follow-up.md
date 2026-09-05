---
title: 'Tenants Query Provenance Follow-Up'
type: 'bugfix'
created: '2026-09-05'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 1
followup_review_recommended: false
baseline_commit: 'b43d64f906665e2bf3015eb2d3f16b771598d352'
baseline_revision: 'b43d64f906665e2bf3015eb2d3f16b771598d352'
tenants_baseline_commit: 'd2b7ede359830c27934ac9f577e3073955c3e2c2'
context:
  - '_bmad-output/project-context.md'
  - 'references/Hexalith.Tenants/_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-4-context.md'
warnings: [oversized]
deferred: []
---

<frozen-after-approval reason="human-approved Story 4.7 scope and repository authority — do not modify unless human renegotiates">

## Intent

**Problem:** All six Tenants query handlers are `HandlerComputed`, yet their shared result factory aliases opaque state-store ETags to `ProjectionVersion` and derives lifecycle/staleness from `ProjectedAt`. EventStore consumers fail closed, but raw producer metadata and its tests still claim authority the route does not possess.

**Approach:** At approved Tenants baseline `d2b7ede359830c27934ac9f577e3073955c3e2c2`, keep ETag only as an opaque validator, remove producer-authored projection/freshness claims, cover every route and edge case, and prove the real EventStore plus generated Tenants API path against persisted Redis state. The Administrator approved this exact producer/test scope and separate EventStore gitlink authority on 2026-09-05.

## Boundaries & Constraints

**Always:** Preserve `HandlerComputed`/`Unknown`; retain normalized ETag only inside raw producer validator metadata; leave `ProjectionVersion`, `IsStale`, `IsDegraded`, and `ServedAt` absent. Test all six routes, genuine sequence, missing/old timestamps, and null/blank/quote-only ETags. Bind validation to exact SHAs and fresh source/package restores. Assert the persisted read model before accepting HTTP evidence. Keep nested submodules uninitialized and preserve orchestrator-owned `sprint-status.yaml`.

**Never:** Do not promote a handler to `ProjectionBacked`, change EventStore routing/normalization, remove genuine persisted sequence stamping, add persistence plumbing, or treat mocks, command completion, HTTP success, timestamp, sequence, or ETag as public freshness proof. Do not touch unrelated UI behavior, initialize nested submodules, commit/push/publish during implementation, or move the root gitlink before a reviewed Tenants commit exists and the concurrently dirty outer tree is stable.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Opaque ETag | Any handler row has a normalized ETag | Raw producer retains ETag and `IsNotModified=false`; every projection/freshness field stays absent | Blank or quote-only token yields null metadata |
| Genuine sequence or timestamp | `tenant-sequence:<n>` and current, old, or absent `ProjectedAt` | Identical validator-only producer output; route stays `HandlerComputed`/`Unknown` | Never promote persisted evidence publicly |
| EventStore gateway | Request supplies `If-None-Match` to a real handler route | HTTP 200; body/headers expose `HandlerComputed`, `Unknown`, and no validator/projection/freshness claims | Fail closed on contradictory producer metadata |
| Generated Tenants route | Persisted tenant is read through `tenants-api` | Payload matches Redis state; raw and typed clients expose no authoritative metadata | A skip or mock-only pass is not evidence |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs:18-69` -- keep normalized ETag/`IsNotModified=false`; make both overloads omit projection/freshness authority and keep the freshness signature for caller compatibility.
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/TenantQueryHandlerBase.cs:150-167` and six handler call sites -- all routes share the active overload; inspect only, do not change.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Queries/{TenantQueryHandlerETagTests,TenantQueryFreshnessTests}.cs` -- replace alias/age expectations with six-route validator-only matrices.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryResultTests.cs` -- new direct coverage for the dormant overload and ETag normalization.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/{TenantsApiGeneratedControllerTests,AspireTopologyTests}.cs` -- retain synthetic emitter guards; add handler-computed stripping and the real generated route/persisted-state proof.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs` -- expose the existing `tenants-api` HTTPS resource to the Tier-3 test.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/State/{TenantMembershipCommandProvenanceTests,TenantLifecycleCommandSnapshotTests}.cs` -- update only stale fallback comments.
- `src/Hexalith.EventStore/{Queries/HandlerAwareQueryRouter.cs,Controllers/QueriesController.cs}` and `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` -- authoritative stamping/stripping already exists; do not edit.

## Tasks & Acceptance

**Execution:**

- [x] `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs` -- preserve opaque validator metadata while removing both ETag/version aliases and all timestamp-derived authority.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Queries/{TenantQueryHandlerETagTests,TenantQueryFreshnessTests,TenantQueryResultTests}.cs` -- pin both factories, all six routes, every timestamp/sequence case, and degenerate ETags.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/{TenantsApiGeneratedControllerTests,AspireTopologyTests}.cs` and `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs` -- prove synthetic header policy plus a zero-skip real `get-tenant` flow whose payload is first verified in Redis at `tenants||projection:tenants:<id>`.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/State/{TenantMembershipCommandProvenanceTests,TenantLifecycleCommandSnapshotTests}.cs` -- remove stale comments without changing UI behavior.
- [ ] `references/Hexalith.Tenants/Hexalith.Tenants.slnx` and affected test projects -- perform fresh Debug/source and Release/package restores, builds, and project-level tests; record exact results and skips.
- [ ] `references/Hexalith.Tenants` -- after local validation, obtain a reviewed Tenants commit/published SHA, then update only this root gitlink when the outer tree is stable; never mix concurrent root edits.

**Acceptance Criteria:**

- Given any of the six handlers returns a row with an opaque ETag, when its raw `TenantQueryResult` is inspected, then only normalized ETag and `IsNotModified=false` are populated and every projection/freshness authority field remains absent.
- Given genuine `tenant-sequence:<n>` or current, old, or missing `ProjectedAt`, when `get-tenant` or another handler executes, then the result is identical validator-only metadata and no evidence promotes the route.
- Given a uniquely persisted tenant, when EventStore and the generated Tenants API read it with a conflicting validator, then Redis content matches the payload, HTTP remains 200, provenance is `HandlerComputed`, lifecycle is `Unknown`, and unsupported body/header metadata is absent.
- Given fresh source and package dependency modes, when the solution builds and affected test projects run independently, then both graphs pass without nested submodule initialization or reused restore assets.
- Given completion is claimed, when repository identity is checked, then evidence records the approved baseline, reviewed Tenants commit and full published SHA, exact validation results, and separately authorized root gitlink with no unrelated outer-tree changes.

## Implementation Notes

- Implemented validator-only producer metadata: normalized opaque ETags remain internal validators with `IsNotModified=false`; projection version, lifecycle, staleness, degradation, and served-at authority are not authored by either factory overload.
- Added direct factory coverage, a six-route handler matrix, current/old/missing timestamp and genuine-sequence cases, degenerate ETags, generated-controller stripping, and a Tier-3 proof that verifies Redis before reading the same tenant directly through EventStore, through the generated `tenants-api` route, and through `TenantsRestQueryClient`.
- Focused verification passed with zero skips: Debug/source and Release/package server builds had 0 warnings and 0 errors; the three server classes passed 45/45 in both modes; generated-controller tests passed 28/28; UI provenance/snapshot tests passed 17/17; the live Aspire/Dapr/Redis test passed 1/1 in 27.007 seconds after the direct EventStore assertions were added.
- Full `Hexalith.Tenants.slnx` restore remains blocked in both modes because it explicitly includes projects from uninitialized nested Commons, EventStore, FrontComposer, and Memories submodules. The approved boundary forbids initializing those nested submodules.
- The fresh Debug/source integration restore succeeded, but its build stops in `references/Hexalith.Memories/Directory.Build.props:89` because Memories' nested `references/Hexalith.EventStore` is absent. The fresh Release/package integration restore succeeded, but its build stops at `src/Hexalith.Tenants.AppHost/Program.cs:132` with pre-existing `CS1503` Dapr-component/string API skew.
- Post-review verification applied P2-ECH-01 by binding the Redis assertion to `DaprDiagnostics.DefaultRedisPort`, then repeated the affected matrix. Debug/source and fresh Release/package server builds again completed with 0 warnings and 0 errors and 45/45 tests passed in each mode with 0 skips; the patch-local Debug integration build completed with 0 warnings and 0 errors; generated-controller tests passed 28/28, UI tests passed 17/17, and the final live Aspire/Dapr/Redis proof passed 1/1 with 0 skips in 23.692 seconds. Fresh source and package integration restores both succeeded before reproducing only the recorded Memories nested-submodule blocker (2 errors) and AppHost `CS1503` blocker (1 error), respectively; both fresh full-solution restores remain blocked by explicitly listed uninitialized nested projects.
- Concurrent automation published Tenants commits `2a204a03` and `a54f0b952eb213026b95fd64810f686d1403c17c`, then moved the EventStore root gitlink to `a54f0b95` in root commit `c08cb3497768806d80a8e949d320cb28ccc40afc`. The final compile aliases and direct EventStore leg remain an uncommitted local delta in `AspireTopologyTests.cs`, so `a54f0b95` is not the final reviewed Story 4.7 SHA and the publication/gitlink task remains open. This run did not commit, push, reset, initialize nested submodules, or edit `sprint-status.yaml`.
- Review pass 4 strengthened the persisted-route proof so the EventStore, raw generated-API, and typed-client payloads are compared with Redis for tenant identity, name, description, status, creation time, members, and configuration. The patched live proof passed in both Debug/source-local output (1/1, 0 skipped, 27.026 seconds) and Release/package mode (1/1, 0 skipped, 29.868 seconds); Release generated-controller tests passed 28/28, Server tests passed 789/789 by direct-assembly fallback, and the two affected UI classes passed 91/91, all with zero skips.
- Release/package project restores and builds for Server, Integration, and UI completed with 0 warnings and 0 errors. The maintained project-level Server test command returned exit 5 with zero tests under Microsoft.Testing.Platform, so the repository-prescribed direct-assembly fallback supplied the 789/789 result. Fresh full-solution restores remain blocked by the solution's uninitialized nested project paths. Debug/source broad validation is additionally blocked by concurrent outer work: the Integration build reaches the known `references/Hexalith.Memories/Directory.Build.props:89` nested-EventStore guard, and the Server build fails at `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1098` because the unrelated dirty actor change references undefined `eventsStoredState`.
- Concurrent automation advanced EventStore to `a907fc07a1d7b33c1fc413ca98370c7cd5e360f1` and both the root Tenants gitlink and Tenants `main` to `b5e9907c938a8384e3bd4a37cdadbddf6dc39cfa`. The pass-4 payload assertion is an uncommitted Tenants delta on top of that published SHA, so the final reviewed-SHA/gitlink task remains open.
- `python3 references/Hexalith.Tenants/scripts/validate-story-gitlinks.py _bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md` cannot validate this cross-repository story record: it exits `FAIL` because the root-owned `baseline_commit` is not a commit in the Tenants repository. No Tenants-local story file exists to supply a Tenants baseline to that repository-scoped validator.

## Spec Change Log

- 2026-08-27 -- Recorded the exact six-route producer/consumer inventory and authority split at EventStore `168c657676ab2e210401bb5fe1c7ae9df06dc0e7` / Tenants `d5ce92881019d3deca20b5fe03b84f86489dd062`; all EventStore-owned focused and persisted-path verification passed. No authenticated Tenants-maintainer or root-gitlink authority was supplied, so the protected external changes remain unchecked and status moved to `awaiting-operator` without touching `sprint-status.yaml`.
- 2026-09-05 -- Administrator approved the exact Story 4.7 producer/test scope at Tenants `d2b7ede359830c27934ac9f577e3073955c3e2c2` and separate root-gitlink authority. Re-planned from Review pass 1 to isolate the validator-only factory correction, all-six-route tests, persisted real-route proof, and fresh dual-mode validation while preserving EventStore normalization and genuine stored sequence stamping.

## Review Triage Log

### 2026-09-05 — Review pass 1

- verdicts: 26 findings — high 5, medium 18, low 1, false 2, maybe-false 0
- routes: intent_gap 12, bad_spec 0, patch 0, defer 10, reject 4
- findings:
  - `[high]` `[intent_gap]` `[BH-01]` Story merge `25a3ac4825acc3ca6367bf56280bf44ac83da10b` moved the Tenants gitlink from the declared read-only baseline `d5ce92881019d3deca20b5fe03b84f86489dd062` to `4d8b19a33f12a583a4f81deb406ff6f97f4f31af` and bundled two unrelated gitlinks; the current pointer is `d2b7ede359830c27934ac9f577e3073955c3e2c2`, with no Story 4.7 authority receipt for either move. The frozen authority clause requires a human to bind an accepted SHA and separately authorize the root pointer.
  - `[medium]` `[intent_gap]` `[BH-02]` `TenantQueryResult.FromPayload` still assigns `readModel?.ProjectionVersion ?? normalizedETag`; the scoped diff between the declared and current Tenants commits is empty for this file. Direct/internal producer consumers therefore still receive the unsupported alias, pending authenticated Tenants authority.
  - `[medium]` `[intent_gap]` `[BH-03]` The three-argument `TenantQueryResult.FromPayload` overload still copies normalized ETag into `ProjectionVersion` at lines 23-29. This second alias is real and remains inside the protected Tenants change boundary.
  - `[medium]` `[intent_gap]` `[BH-04]` The active overload still calls `ToQueryResponseMetadata`, so `ProjectedAt` continues to produce producer-authored lifecycle/staleness for handler-computed routes. Public EventStore normalization mitigates the leak, but the producer contract remains wrong.
  - `[medium]` `[intent_gap]` `[BH-05]` `TenantQueryHandlerETagTests` still requires ETag to equal `ProjectionVersion`, preserving the unsupported producer contract. Replacing the expectation requires the same Tenants-maintainer authority as the source correction.
  - `[low]` `[intent_gap]` `[BH-06]` The ETag theory still covers five routes and omits `get-global-administrators`; the all-six-route acceptance matrix is incomplete even before its expectations are corrected.
  - `[medium]` `[intent_gap]` `[BH-07]` `TenantQueryFreshnessTests` still asserts age-derived staleness and ETag fallback across the named scenarios. The required fail-closed expectations have not been implemented.
  - `[medium]` `[intent_gap]` `[BH-08]` The current Tenants revision adds neither the required real generated-API/gateway proof nor persisted-read-model assertions in `AspireTopologyTests` and `TenantsApiGeneratedControllerTests`. Those protected test changes remain unchecked in the task list.
  - `[medium]` `[intent_gap]` `[BH-09]` The retained EventStore E2E test exercises only `list-tenants` and supplies an ETag from a separate `counter` projection. It proves outer normalization for one route, not the six Tenants producers with their own persisted-model validators.
  - `[medium]` `[intent_gap]` `[BH-10]` The retained Redis assertion reads `admin:query-types:tenants`, which proves handler registration rather than any route inventory read model or its metadata. It cannot satisfy the post-correction persisted-read-model-origin criterion.
  - `[medium]` `[intent_gap]` `[BH-11]` No fresh Debug/source and Release/package Tenants validation matrix is retained for the current pointer. Completion remains unsafe until an authorized correction exists and both dependency modes are run against its exact SHA.
  - `[medium]` `[reject]` `[BH-12]` Frontmatter `in-review`, prose `awaiting-operator`, and orchestrator-owned sprint `backlog` are genuinely different workflow signals and caused this invocation to route into review. Reconciliation would edit this build's spec or the separately owned sprint file, so the review rule rejects this finding from code remediation.
  - `[false]` `[reject]` `[BH-13]` Story 4.7 did not modify `sprint-status.yaml`: commit `25a3ac4825acc3ca6367bf56280bf44ac83da10b` changed only this spec and three gitlinks. The 21 additions and 10 deletions in the baseline-wide diff are later orchestrator-owned changes, so they do not establish the claimed Story 4.7 violation.
  - `[high]` `[intent_gap]` `[BH-14]` All retained verification is bound to EventStore `168c657676ab2e210401bb5fe1c7ae9df06dc0e7` and Tenants `d5ce92881019d3deca20b5fe03b84f86489dd062`, while the reviewed root now selects `d2b7ede359830c27934ac9f577e3073955c3e2c2`. No completion claim can cross that identity gap without a human-approved target SHA and rerun evidence.
  - `[medium]` `[reject]` `[BH-15]` The recorded baseline produces a 25,998,034-byte, 2,684-file-section diff containing many later stories and all root submodule movements, so it is not a reviewable isolated Story 4.7 subject. Correcting the baseline is a spec-only remedy and is rejected by the review rule; this pass instead verified the story-specific claims directly.
  - `[high]` `[defer]` `[ECH-01]` Admin invokes `eventstore-operations`, but the standard AppHost contains no Operations project or sidecar. Local dead-letter calls therefore fail Dapr discovery; this Operations feature was added after Story 4.7 and is not caused by its provenance work.
  - `[high]` `[defer]` `[ECH-02]` AppHost pub/sub and state-store component scopes omit `eventstore-operations`, including subscription access to the dead-letter topic. An Operations sidecar added without those grants could not capture or persist work; this is unrelated later topology work.
  - `[medium]` `[defer]` `[ECH-03]` `DeadLetterBacklogReconciler` abandons reconciliation after five startup attempts, so a sidecar becoming ready later can leave retained-backlog gauges at zero indefinitely. This later Operations behavior is outside Story 4.7.
  - `[medium]` `[defer]` `[ECH-04]` `DaprDeadLetterQueryService` rejects only null/whitespace tenant identifiers, while the Operations actor enforces the 256-character/control-character safe identity contract. Invalid input can consequently surface as a backend failure instead of a caller error; this Admin/Operations issue is unrelated to Story 4.7.
  - `[medium]` `[defer]` `[ECH-05]` The loop hook refuses a redirected final event directory but its POSIX `O_NOFOLLOW` open does not anchor each ancestor, leaving an ancestor-symlink redirection/stall case. This tooling hardening issue is unrelated to Story 4.7.
  - `[false]` `[reject]` `[ECH-06]` The reusable release workflow re-resolves live `main` immediately before Semantic Release and requires it to equal the checked-out dispatch SHA at `domain-release.yml:412-459`; governed mode repeats the equivalent check at lines 811-842. That directly disproves the claimed stale-main publication path.
  - `[high]` `[defer]` `[VG-01]` Pre-verified gap: the executable Operations workload and Admin's `eventstore-operations` target have no AppHost resource, sidecar, component wiring, or model assertion. Mocked Admin tests cannot reveal the resulting local service-discovery failure; this later feature is not Story 4.7 work.
  - `[medium]` `[defer]` `[VG-02]` Pre-verified gap: no behavioral test covers `DeadLetterBacklogReconciler` actor identity, one-item activation, retry bound, success, or cancellation. Startup reconciliation can regress while existing host and telemetry tests stay green; this is unrelated later Operations work.
  - `[medium]` `[defer]` `[VG-03]` Pre-verified gap: validator tests cover only invalid `MaxActionItems`, leaving every required string and all other numeric boundaries unpinned. Runtime configuration validation can regress unnoticed, but the surface belongs to later Operations work.
  - `[medium]` `[defer]` `[VG-04]` Pre-verified gap: `pick_methods.py` has meaningful pytest coverage but normal CI invokes only .NET lanes. Advanced-elicitation catalog behavior can regress behind a green pull request; this agent-tooling issue is unrelated to Story 4.7.
  - `[medium]` `[defer]` `[VG-05]` Pre-verified gap: the loop event relay's partial-write, atomic publication, path selection, mode, and redirect refusal have no tests in normal CI. A relay regression can lose completion signals until timeout, but it is unrelated to Story 4.7.
- grouped survivors:
  - `[high]` `[intent_gap]` Tenants authority and identity: BH-01 and BH-14 require an authenticated accepted Tenants SHA, approval for its exact producer/test scope, rerun evidence, and separate root-gitlink authority.
  - `[medium]` `[intent_gap]` Protected producer correction: BH-02 through BH-08 and BH-11 are the unimplemented Tenants source, all-six-route tests, integration proof, and dual-mode matrix that cannot proceed without that authority.
  - `[medium]` `[intent_gap]` Persisted production-path proof: BH-09 and BH-10 show the retained EventStore-only evidence cannot replace the authorized real Tenants route/read-model proof.
  - `[high]` `[defer]` Later Operations topology: ECH-01, ECH-02, and VG-01 share the missing `eventstore-operations` AppHost/sidecar/component wiring root cause.
  - `[medium]` `[defer]` Later Operations, Admin, release-tooling, and agent-tooling findings: ECH-03 through ECH-05 and VG-02 through VG-05 are independently real but were not processed because the higher-priority intent gap triggered loopback.
- loopback: No implementation code was changed in this review run, so there is no run-owned code to revert. Historical `main` commits and subsequent submodule updates were preserved. Review pauses for authenticated Tenants-maintainer and root-gitlink authority before planning can resume.

### 2026-09-05 — Review pass 2

- verdicts: 24 findings — high 0, medium 7, low 6, false 10, maybe-false 1
- routes: intent_gap 0, bad_spec 0, patch 1, defer 8, reject 15
- findings:
  - `[medium]` `[reject]` `[P2-BH-01]` Carried from pass-1 BH-15: the 24,262,123-byte, 2,596-section baseline diff is not an isolated Story 4.7 review subject. Correcting the baseline would edit this build's spec, so the review rule rejects that remedy; this pass separately inspected the Tenants baseline delta.
  - `[low]` `[reject]` `[P2-BH-02]` Baseline `b43d64f9` records `created: 2026-08-27`, while the replanned spec says `2026-09-05`. The historical-date discrepancy is real but its only fix is this build's spec, which review rules reject.
  - `[medium]` `[reject]` `[P2-BH-03]` Fresh solution restores are structurally blocked by explicitly listed, uninitialized nested projects, while the frozen boundary forbids initializing nested submodules. The blocker is real, but the proposed acceptance-gate rewrite edits this build's spec and the alternative solution restructuring is not Story 4.7 implementation work.
  - `[false]` `[reject]` `[P2-BH-04]` The spec does not claim a reproducible published completion: status is `in-review`, the publication task remains unchecked, and the uncommitted final delta plus exact blockers are explicitly recorded. Published-SHA evidence is required before that task can be checked.
  - `[false]` `[reject]` `[P2-BH-05]` Repository search found no automation that interprets frontmatter `deferred:` as the review ledger. Pass-1 deferred findings already live in `deferred-work.md`; `deferred: []` does not conceal them from a demonstrated consumer.
  - `[false]` `[reject]` `[P2-BH-06]` `IsDegraded` is deliberately valid for a handler-computed degraded path, independently of projection lifecycle; controller tests explicitly preserve it while stripping ETag, not-modified, staleness, and projection version. The real Tenants producer now authors no degradation value.
  - `[false]` `[reject]` `[P2-BH-07]` The generated emitter's route-agnostic degradation header matches the established controller contract rather than leaking projection authority. The real Story 4.7 path asserts the header absent because its producer supplies no degraded state.
  - `[false]` `[reject]` `[P2-BH-08]` `ServedAt` on the outer response is gateway timing metadata, not Tenants projection authority. `TenantQueryResult` leaves it null and the EventStore gateway stamps the response time; retaining/emitting that supported field does not contradict the producer-only contract.
  - `[false]` `[reject]` `[P2-BH-09]` Degenerate ETags are pinned at both shared factory overloads, including null/blank/whitespace/quote-only forms, while separate executed matrices prove all six handlers use the same factory and omit timestamp/sequence authority. A six-route cross-product would duplicate the tested seam without covering a distinct branch.
  - `[low]` `[reject]` `[P2-BH-10]` `Trim('"')` can produce a malformed raw token from weak or unbalanced quoted input, but Dapr supplies opaque unquoted state-store versions and EventStore strips the validator from handler-computed public responses. The unlikely direct-consumer case is low impact and robust weak/malformed parsing would add guards outside the approved edge matrix.
  - `[false]` `[reject]` `[P2-BH-11]` The Redis proof intentionally binds the checked-in Dapr component's current `localhost:6379`, no-auth, `tenants||` physical-key contract. A future component change should break and update this persistence-origin proof rather than silently validate a different topology.
  - `[medium]` `[defer]` `[P2-BH-12]` Real multi-RID and missing-input container publication tests are filtered from the default Contracts job and repository-wide workflow search found no automatic lane selecting `HeavyweightContainerPublish`; a real container regression can therefore merge behind synthetic-only coverage. This concurrent release-governance gap is unrelated to Story 4.7.
  - `[low]` `[defer]` `[P2-BH-13]` DW-372's resolution says the malformed-input theory is heavyweight and excluded, while code and its manifest binder intentionally leave that direct-MSBuild theory in the default gate. The inaccurate concurrent ledger resolution is unrelated to Story 4.7.
  - `[low]` `[defer]` `[P2-BH-14]` The validity-window theory tests 90,001 seconds rather than the exact 86,401-second first-invalid boundary, so a widened 24-hour validator limit could escape this focused test. This concurrent release-evidence test is unrelated to Story 4.7.
  - `[low]` `[reject]` `[P2-BH-15]` Automatic lanes are Linux-only, but the seven Windows branches are structurally bound to `Assert.Skip` and forbidden from using `return`. Runtime observation on Windows would be stronger, but adding a platform lane is disproportionate to this low, unrelated verification concern.
  - `[medium]` `[defer]` `[P2-VG-01]` Pre-verified: the default workflow excludes both real-publish `HeavyweightContainerPublish` theories and no automatic alternate lane selects them, leaving actual OCI publication behavior unexecuted. This is the same unrelated root cause as P2-BH-12/P2-BH-13.
  - `[medium]` `[defer]` `[P2-VG-02]` Carried from pass-1 VG-04: `pick_methods.py` has meaningful pytest coverage but no normal Memories CI invocation, so advanced-elicitation catalog behavior can regress behind a green build. It remains unrelated agent tooling and is not deferred again.
  - `[medium]` `[defer]` `[P2-VG-03]` Carried from pass-1 VG-05: loop event relay path selection, partial writes, atomic publication, mode, and redirect refusal still lack normal-CI behavioral tests. It remains unrelated agent tooling and is not deferred again.
  - `[low]` `[patch]` `[P2-ECH-01]` The new Redis proof uses `DaprDiagnostics.ResolveRedisPort()`, which honors `HEXALITH_EVENTSTORE_TEST_REDIS_PORT`, while the actual checked-in Tenants state-store component is fixed to `localhost:6379`. With an override and both endpoints present, the proof can inspect the wrong Redis; the smallest fix is to bind the proof to `DaprDiagnostics.DefaultRedisPort`.
  - `[false]` `[reject]` `[P2-ECH-02]` No supported production or test configuration supplies a throwing query-handler `TimeProvider`; all discovered callers use `TimeProvider.System` or non-throwing fixed providers. A hypothetical malicious dependency does not demonstrate a reachable defect, and the frozen design deliberately retains the compatibility overload.
  - `[medium]` `[defer]` `[P2-ECH-03]` Carried from pass-1 ECH-05: the POSIX loop hook anchors the final directory but not every ancestor, leaving an ancestor-symlink redirect/refusal case. This unrelated tooling issue is not deferred again.
  - `[false]` `[reject]` `[P2-ECH-04]` A failed write/rename can leave a temporary file, but the next event uses a new nanosecond-based filename, so the orphan cannot block the retried signal as claimed. Residual-file cleanup may be desirable but does not establish the filed consequence.
  - `[maybe-false]` `[defer]` `[P2-ECH-05]` The hook does not sanitize separators in `BMAD_LOOP_TASK_ID` or the event name, but the producer of those environment values is outside the reviewed repository, so valid-character guarantees could not be established. Evidence from the orchestrator that IDs are separator-free would refute the risk; otherwise a valid separator can make the hook silently drop an event.
  - `[false]` `[reject]` `[P2-ECH-06]` The test uses the repository's standard Dapr availability mechanism, but this acceptance run executed the method twice with 1 passed and 0 skipped. The frozen requirement rejects skipped output as evidence; it does not require every developer machine without Dapr to fail its full suite.
- grouped survivors:
  - `[low]` `[patch]` Redis proof endpoint: P2-ECH-01 is a one-line test-only correction binding direct persistence inspection to the component endpoint actually used by this topology.
  - `[medium]` `[defer]` Container publication automation and ledger accuracy: P2-BH-12, P2-BH-13, and P2-VG-01 share the incomplete retiering that removed real publishes from the default job without adding a positive lane and then overstated DW-372's resolution.
  - `[low]` `[defer]` Release authority boundary: P2-BH-14 leaves the exact first-invalid 24-hour boundary unpinned.
  - `[medium-unverified]` `[defer]` Loop hook identifier safety: P2-ECH-05 needs the external orchestrator's task/event identifier contract to settle whether separator injection is reachable.
  - `[medium]` `[defer]` Carried tooling gaps: P2-VG-02, P2-VG-03, and P2-ECH-03 retain their pass-1 routes and are not patched or deferred again.
- loopback: none; no intent-gap or bad-spec survivor was found. Apply P2-ECH-01, rerun its focused live proof, and append only the three newly deferred groups.

### 2026-09-05 — Review pass 3

- verdicts: 18 findings — high 0, medium 4, low 4, false 0, maybe-false 0
- routes: intent_gap 0, bad_spec 0, patch 0, defer 18, reject 0
- findings:
  - `[defer]` `[BH-01]` through `[BH-05]`, `[BH-09]` through `[BH-14]`: CI, documentation, date-history, release-boundary, and concurrent actor recovery concerns are outside the approved Tenants provenance change and arise from other dirty-tree work.
  - `[defer]` `[BH-06]` through `[BH-08]`: additional actor recovery and pending-count concerns are outside Story 4.7 and require separate implementation authority.
  - `[defer]` `[ECH-01]`: a failed cache-barrier recovery before durable counts are known may need a dedicated actor-state design review; it is not caused by the Tenants producer change.
  - `[defer]` `[ECH-02]`: malformed retained publication entries and owner-capacity accounting belong to concurrent publication-recovery work, not Story 4.7.
  - `[defer]` `[VG-01]`: activation reconciliation lacks a nonempty-index behavioral test, but the changed actor/index code is concurrent and unrelated to Story 4.7.
  - `[defer]` `[VG-02]`: non-command cache-entry barriers lack direct behavioral tests, but the changed actor infrastructure is concurrent and unrelated to Story 4.7.
- grouped survivors:
  - `[medium]` `[defer]` Concurrent actor state recovery and publication-index hardening: BH-06 through BH-08, BH-09 through BH-14, ECH-01, ECH-02, VG-01, and VG-02 require separate actor-state scope and tests.
  - `[medium]` `[defer]` Concurrent CI, release, documentation, and tooling concerns: BH-01 through BH-05 concern unrelated dirty-tree changes and are not Story 4.7 defects.
- loopback: none; no Story 4.7 finding survived triage. The current Tenants producer correction and Redis proof remain unchanged.

### 2026-09-05 — Review pass 4

- verdicts: 28 findings — high 1, medium 15, low 8, false 4, maybe-false 0
- routes: intent_gap 0, bad_spec 0, patch 1, defer 15, reject 12
- findings:
  - `[medium]` `[defer]` `[P4-BH-01]` Carried from P2-BH-12/P2-VG-01: the default Contracts workflow excludes `HeavyweightContainerPublish` and no automatic lane positively selects it. This concurrent release-governance gap remains unrelated to Story 4.7 and is not deferred again.
  - `[low]` `[defer]` `[P4-BH-02]` Carried from P2-BH-14: the retained-authority test still omits the exact 86,401-second first-invalid boundary. This concurrent release-evidence gap is unrelated to Story 4.7 and is not deferred again.
  - `[medium]` `[defer]` `[P4-BH-03]` Carried from pass-3 pending-count reconciliation triage: `ReconcilePendingCommandCountAsync` derives the durable count only from publication-index owners even though a committed `Processing` checkpoint can own a pending slot. The current actor code still exhibits the claimed accounting risk, but it is concurrent EventStore work outside Story 4.7 and is not deferred again.
  - `[medium]` `[defer]` `[P4-BH-04]` Carried from pass-3 publication-index hardening triage: normalization keeps the first well-formed duplicate message owner without durable correlation evidence, so a conflicting retained entry can select the wrong recovery owner. This is unrelated concurrent actor work and is not deferred again.
  - `[medium]` `[defer]` `[P4-BH-05]` Carried from pass-3 publication-index hardening triage: a malformed entry followed by a well-formed entry with the same message id survives normalization, and first-match removal can remove only the malformed entry. This is unrelated concurrent actor work and is not deferred again.
  - `[medium]` `[defer]` `[P4-BH-06]` Carried from pass-3 malformed-owner triage: `Contains` accepts a malformed nonblank message id while `OwnerCount` excludes it, so the two owner checks can disagree. This is unrelated concurrent actor work and is not deferred again.
  - `[medium]` `[defer]` `[P4-BH-07]` Carried from pass-3 owner-capacity triage: `TryAdd` gates on total `Entries.Count` while owner accounting excludes malformed retained entries, allowing unusable entries to consume capacity. This is unrelated concurrent actor work and is not deferred again.
  - `[high]` `[defer]` `[P4-BH-08]` `CompleteDrainExhaustionAsync` publishes before durably marking `DeadLettered`; a pre-commit marker-save failure leaves the record eligible for a duplicate external publication. The current publisher supplies a stable CloudEvent id but no repository-owned consumer/idempotency guarantee proves duplicate suppression, and this pre-existing EventStore recovery defect is outside Story 4.7.
  - `[medium]` `[defer]` `[P4-BH-09]` `CreateManualSnapshotAsync` performs same-sequence success inference from a catch covering inspection, reconstruction, creation, and save; a pre-existing same-sequence snapshot can therefore turn an earlier infrastructure exception into a false `Created` result. This concurrent EventStore actor issue is outside Story 4.7.
  - `[low]` `[reject]` `[P4-BH-10]` Carried from P2-BH-10: weak, wildcard, or unbalanced ETag syntax is not a reachable public-authority leak because Dapr versions are opaque producer validators and EventStore strips them from handler-computed responses. Adding HTTP entity-tag parsing would exceed the approved edge matrix for negligible direct-consumer impact.
  - `[low]` `[reject]` `[P4-BH-11]` The bootstrap helper's synthetic 409 result predates Story 4.7, and the exercised `BootstrapGlobalAdmin` aggregate currently has no other conflict outcome at this endpoint. Parsing a hypothetical future conflict would add test-helper complexity without demonstrating a current bad outcome.
  - `[medium]` `[reject]` `[P4-BH-12]` Carried from P2-BH-01: the baseline still produces a broad multi-story review subject. Its only proposed remedy edits this build's spec, so review rules reject it from code remediation.
  - `[medium]` `[reject]` `[P4-BH-13]` Carried from P2-BH-03: the full-solution gate and the prohibition on nested-submodule initialization remain structurally incompatible. The proposed remedies edit the spec or restructure the solution, so they are rejected from this code review.
  - `[low]` `[reject]` `[P4-BH-14]` Carried from P2-BH-02: the historical creation-date discrepancy is real, but its only fix is this build's spec and is therefore rejected.
  - `[low]` `[reject]` `[P4-BH-15]` The frontmatter review-loop counter does not enumerate completed review passes, but synchronizing it would only edit this build's spec and is rejected by the review rule.
  - `[false]` `[reject]` `[P4-BH-16]` `followup_review_recommended: false` describes the review outcome rather than implementation completion, and no repository automation treats `deferred: []` as the deferred-work ledger. Open tasks and separately recorded deferrals do not make either value false.
  - `[false]` `[reject]` `[P4-BH-17]` The spec remains `in-review` with validation/publication tasks unchecked and does not claim reproducible completion. It records exact result counts and blockers; attached raw logs are not yet required by a completed evidence claim.
  - `[medium]` `[defer]` `[P4-VG-01]` Pre-verified: stale-checkpoint handoff has successful-path coverage but no before-commit or commit-then-throw save-fault case, so its durable-witness recovery branch can regress unobserved. This concurrent EventStore actor test gap is outside Story 4.7.
  - `[medium]` `[defer]` `[P4-VG-02]` Pre-verified: drain-retry persistence has normal and commit-then-throw coverage but no pre-commit failure repair test. This concurrent EventStore actor test gap is outside Story 4.7.
  - `[medium]` `[defer]` `[P4-VG-03]` Carried from P2-VG-01: no automatic workflow positively executes the real heavyweight container-publication tests. This unrelated release-governance gap is not deferred again.
  - `[low]` `[defer]` `[P4-VG-04]` Carried from P2-BH-14: the exact first-invalid 86,401-second authority boundary remains untested. This unrelated release-evidence gap is not deferred again.
  - `[low]` `[reject]` `[P4-VG-05]` Carried from P2-BH-15: Windows runtime execution would be stronger than the structural `Assert.Skip` binder, but adding a Windows lane is disproportionate to this low, unrelated concern.
  - `[false]` `[reject]` `[P4-ECH-01]` Current source uses `DaprDiagnostics.DefaultRedisPort`, not the historical `ResolveRedisPort()` line in the staged baseline diff; the pass-2 patch already prevents the proof from following an override to the wrong store.
  - `[medium]` `[defer]` `[P4-ECH-02]` Carried with P4-BH-07 from pass-3 owner-capacity triage: malformed retained entries count toward `Entries.Count` but not usable owner capacity. This unrelated actor issue is not deferred again.
  - `[medium]` `[defer]` `[P4-ECH-03]` Carried from P2-ECH-03: the loop hook does not anchor every directory ancestor with `O_NOFOLLOW`. This unrelated tooling issue is not deferred again.
  - `[false]` `[reject]` `[P4-ECH-04]` Carried from P2-ECH-06 and reconfirmed by this pass: the `DaprFact` can skip only when prerequisites are unavailable, while the acceptance evidence requires and obtained an executed run. Both pass-4 live runs completed 1/1 with 0 skips.
  - `[low]` `[patch]` `[P4-ECH-05]` The new persisted-route proof compared only tenant id, name, and description across responses, leaving status, members, configuration, and creation time outside its claimed Redis payload-equivalence check. The direct test-only correction now compares the complete `TenantDetail` shape on all three paths.
  - `[medium]` `[reject]` `[P4-ECH-06]` Carried from P2-BH-01: the baseline diff includes unrelated root, submodule, release, and actor work, but changing this build's baseline is a spec-only remedy and is rejected from code remediation.
- grouped survivors:
  - `[low]` `[patch]` Complete persisted-payload comparison: P4-ECH-05 required one shared assertion helper and no production change; the Debug and Release live proofs both passed with zero skips.
  - `[high]` `[defer]` Dead-letter publication atomicity: P4-BH-08 is a pre-existing EventStore recovery defect outside Story 4.7.
  - `[medium]` `[defer]` Manual-snapshot ambiguous success inference: P4-BH-09 is concurrent EventStore actor work outside Story 4.7.
  - `[medium]` `[defer]` Missing stale-handoff save-fault coverage: P4-VG-01 is concurrent EventStore actor test work outside Story 4.7.
  - `[medium]` `[defer]` Missing pre-commit drain-retry repair coverage: P4-VG-02 is concurrent EventStore actor test work outside Story 4.7.
  - `[medium]` `[defer]` Carried actor/index hardening: P4-BH-03 through P4-BH-07 and P4-ECH-02 retain pass-3 routing and were not deferred again.
  - `[medium]` `[defer]` Carried release/tooling gaps: P4-BH-01, P4-BH-02, P4-VG-03, P4-VG-04, and P4-ECH-03 retain prior routing and were not deferred again.
- loopback: none; no intent gap or bad spec survived. The Story 4.7 patch passed focused and Release/package verification, but review cannot advance while the required Debug/source gate is blocked by unrelated concurrent outer changes.

## Design Notes

Keep the active freshness overload signature so all handler constructors and call sites stay stable, but delegate it to the validator-only factory. The persisted read model still stores timestamp and sequence for replay/idempotency; only query-response authority changes. The Tier-3 proof must inspect Redis before both raw HTTP and typed-client assertions because a completed command or successful response does not establish projection origin.

## Verification

**Commands:**

- `dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false -nodeReuse:false -m:1` then Debug build/tests by project -- expected: source-mode graph and affected suites pass.
- `dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Release -p:UseHexalithProjectReferences=false -nodeReuse:false -m:1` then Release build/tests by project -- expected: package-mode graph and affected suites pass.
- `dotnet test tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj` and `dotnet test tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj` with matching mode/configuration and no-build/no-restore -- expected: producer matrices pass; the named Aspire proof executes with zero skips and verifies Redis plus raw/typed routes.
- `git -C references/Hexalith.Tenants diff --check` and `git diff --check` -- expected: clean Tenants and outer diffs, with no `sprint-status.yaml` or unrelated outer-tree changes attributable to Story 4.7.
