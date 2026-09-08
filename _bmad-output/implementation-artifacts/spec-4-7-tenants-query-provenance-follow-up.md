---
title: 'Tenants Query Provenance Follow-Up'
type: 'bugfix'
created: '2026-09-05'
status: 'done'
route: 'dispatch'
review_loop_iteration: 6
followup_review_recommended: true
baseline_commit: 'b43d64f906665e2bf3015eb2d3f16b771598d352'
baseline_revision: 'b43d64f906665e2bf3015eb2d3f16b771598d352'
tenants_baseline_commit: 'd2b7ede359830c27934ac9f577e3073955c3e2c2'
context:
  - '_bmad-output/project-context.md'
  - 'references/Hexalith.Tenants/_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-4-context.md'
warnings: [oversized]
deferred: ['DW-487', 'DW-488', 'DW-489', 'DW-490', 'DW-491', 'DW-492', 'DW-493', 'DW-494']
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
- Review pass 6 patches (2026-09-06): balanced-quote ETag normalization; factory coverage for `W/"abc"`, matching degenerate theories, and `JsonValueKind.Undefined`; six-route freshness controls that prove persisted `ProjectedAt`/`tenant-sequence:` inputs and the matching `GetAsync` reads; Redis helper `AbortOnConnectFail=true` plus `JsonException` retries; `tenants-api` moved last without fixture-wide aliveness wait; isolated typed client that clones the HTTP request; direct `StackExchange.Redis` PackageReference; `TenantProjectionVersionFormat.SequencePrefix`; and `PublishFailed` skipped as an environment outage. Review pass 7 removed the EventStore `ProjectionBacked` retry and the test-injected `eventstore||admin:query-types:tenants` write so the gateway assertion fail-closes. After deleting leftover Redis catalogs, the Release live proof executed 1 failed / 0 skipped in 26.063 seconds: Redis tenant persistence succeeded, then `POST /api/v1/queries` returned `X-Hexalith-Query-Provenance: ProjectionBacked`. Server query classes remain 49/49 (0 skipped) in Release; generated-controller tests remain 28/28 (0 skipped). Full-solution restores remain DW-494. On 2026-09-06 the Administrator authorized a separate EventStore change (DW-495) so `AdminOperationalIndexHostedService` persists recovered `admin:query-types:{domain}` catalogs when sibling metadata fails; that EventStore edit is not Story 4.7 producer scope. After rebuilding EventStore Debug/Release and deleting leftover Redis catalogs, the same fail-closed live proof passed 1/1 (0 skipped) in 26.173 seconds: Event 6104 wrote recovered handler catalogs for two domains, EventStore invoked `tenants/method/query`, and `eventstore||admin:query-types:tenants` contained the five Tenants handler types. No commit, push, nested-submodule init, `sprint-status.yaml` edit, or root gitlink move.

## Spec Change Log

- 2026-08-27 -- Recorded the exact six-route producer/consumer inventory and authority split at EventStore `168c657676ab2e210401bb5fe1c7ae9df06dc0e7` / Tenants `d5ce92881019d3deca20b5fe03b84f86489dd062`; all EventStore-owned focused and persisted-path verification passed. No authenticated Tenants-maintainer or root-gitlink authority was supplied, so the protected external changes remain unchecked and status moved to `awaiting-operator` without touching `sprint-status.yaml`.
- 2026-09-05 -- Administrator approved the exact Story 4.7 producer/test scope at Tenants `d2b7ede359830c27934ac9f577e3073955c3e2c2` and separate root-gitlink authority. Re-planned from Review pass 1 to isolate the validator-only factory correction, all-six-route tests, persisted real-route proof, and fresh dual-mode validation while preserving EventStore normalization and genuine stored sequence stamping.
- 2026-09-06 -- After the fail-closed live proof returned `ProjectionBacked`, the Administrator authorized a separate EventStore repair (choice 1 / DW-495): persist handler query-type indexes for domains whose metadata loaded successfully, and rewrite them on refresh, without folding EventStore routing into Story 4.7 producer scope.

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

### 2026-09-05 — Review pass 5

- verdicts: 35 findings — high 3, medium 22, low 8, false 1, maybe-false 1
- routes: intent_gap 0, bad_spec 0, patch 0, defer 29, reject 6
- findings:
  - `[maybe-false]` `[defer]` `[P5-BH-01]` `StagePendingCommandCountAsync` and `ActorStateMachine.CheckpointAsync` run before the guarded admission save, and no catch discards their batch if either staging call throws. The bad outcome requires a Dapr state-manager or logger failure that throws after retaining staged state; a fault test or implementation guarantee for those post-staging exceptions would settle whether a later turn can commit the abandoned admission.
  - `[medium]` `[defer]` `[P5-BH-02]` Legacy idempotency migration stages the new key before removing the legacy key, and an exception from `TryRemoveStateAsync` escapes `CheckAsync` without actor-owned discard or poison handling. A later state save can commit the partially staged migration; this concurrent actor-safety issue is outside Story 4.7.
  - `[medium]` `[defer]` `[P5-BH-03]` Carried from P4-BH-09: `CreateManualSnapshotAsync` can report `Created` after an earlier inspection, reconstruction, or creation failure when a pre-existing snapshot has the current sequence. This concurrent EventStore issue remains outside Story 4.7 and is not deferred again.
  - `[medium]` `[defer]` `[P5-BH-04]` `GetEventsAsync` catches metadata-read `OperationCanceledException` as `Exception` and wraps it as `EventDeserializationException`, while its event reads preserve cancellation. Callers and telemetry can misclassify cancellation as corrupt event state; this EventStore actor issue is unrelated to Story 4.7.
  - `[high]` `[defer]` `[P5-BH-05]` Normalization retains a malformed entry before a well-formed entry with the same message id; activation terminalizes that id from the malformed entry and final `Prune` removes both entries, including the valid recovery owner. That can strand committed publication recovery and is concurrent EventStore work outside Story 4.7.
  - `[medium]` `[defer]` `[P5-BH-06]` Malformed nonblank entries do not consume either activation budget but call `CommitRecoverableCompletionAsync`, which can read and save actor state. Because the deserialized index has no length cap, malformed entries can cause unbounded activation work; this is unrelated to the Tenants provenance change.
  - `[false]` `[reject]` `[P5-BH-07]` The stale-handoff pipeline-presence check runs in a serialized actor turn immediately after loading and attempting to remove that exact checkpoint; no reachable in-turn writer can replace the same correlation key before inspection. The proposed unequal-checkpoint outcome was not demonstrated.
  - `[high]` `[defer]` `[P5-BH-08]` Carried from P4-BH-08: drain exhaustion publishes externally before durably marking `DeadLettered`, leaving a duplicate-publication window on a pre-commit marker-save failure. This pre-existing EventStore issue is not deferred again.
  - `[medium]` `[defer]` `[P5-BH-09]` Carried from P4-BH-01/P4-VG-03: no automatic workflow positively executes the real `HeavyweightContainerPublish` cases. This unrelated release-governance gap is not deferred again.
  - `[medium]` `[defer]` `[P5-BH-10]` The documented v4 succession path requires a handler-specific codec digest, but `_load_handler` rejects every handler whose digest differs from `V3_PACKET_CODEC_SHA256`. Following the documentation therefore produces an unusable v4 handler; this corrective-release tooling issue is outside Story 4.7.
  - `[low]` `[reject]` `[P5-BH-11]` Carried from P2-BH-15/P4-VG-05: the fixed-window source binder is weaker than Windows execution, but adding a Windows lane or parser is disproportionate to this low unrelated concern.
  - `[low]` `[defer]` `[P5-BH-12]` Carried from P2-BH-14/P4-BH-02: the exact 86,401-second first-invalid retained-authority boundary remains untested. This unrelated release-evidence gap is not deferred again.
  - `[low]` `[defer]` `[P5-BH-13]` Carried from P2-BH-13: DW-372 still incorrectly says the malformed-input direct-MSBuild theory is heavyweight and excluded. The inaccurate concurrent ledger record is not deferred again.
  - `[low]` `[defer]` `[P5-BH-14]` The pass-3 deferred actor entry still cites missing nonempty-index activation and direct cache-barrier tests, while both named tests now exist. The remaining actor concerns may still be valid, but stale evidence can misroute future work; ledger maintenance is outside Story 4.7.
  - `[medium]` `[reject]` `[P5-BH-15]` Carried from P2-BH-01/P4-ECH-06: the recorded baseline and current Tenants gitlink span unrelated commits, and root pointer advances were mixed with other work. The spec already leaves final reviewed-SHA/gitlink completion open, while changing the review baseline or historical commits is not a code-review patch.
  - `[medium]` `[defer]` `[P5-ECH-01]` Carried from P4-BH-01/P4-VG-03: heavyweight container publication has no positive automatic lane. It is unrelated to Story 4.7 and is not deferred again.
  - `[low]` `[reject]` `[P5-ECH-02]` Carried from P2-BH-15/P4-VG-05: a balanced-block parser or Windows execution would strengthen the source binder, but the low unrelated risk does not justify that added machinery here.
  - `[low]` `[defer]` `[P5-ECH-03]` Carried from P2-BH-14/P4-BH-02: the 86,401-second validity boundary remains uncovered and is not deferred again.
  - `[medium]` `[defer]` `[P5-ECH-04]` Carried from P4-BH-03: pending-count reconciliation uses publication-index owners only and can erase a slot owned by a committed `Processing` checkpoint. This unrelated actor issue is not deferred again.
  - `[medium]` `[defer]` `[P5-ECH-05]` Carried from P4-BH-04: normalization can keep the wrong correlation owner when well-formed duplicate message ids conflict. This unrelated actor issue is not deferred again.
  - `[medium]` `[defer]` `[P5-ECH-06]` Carried from P4-BH-05: first-match removal can delete only a malformed duplicate and leave the well-formed owner stuck. This unrelated actor issue is not deferred again.
  - `[medium]` `[defer]` `[P5-ECH-07]` Carried from P4-BH-06: `Contains` accepts a malformed nonblank message id while `OwnerCount` excludes it. This unrelated actor issue is not deferred again.
  - `[medium]` `[defer]` `[P5-ECH-08]` Carried from P4-BH-07/P4-ECH-02: malformed entries consume raw index capacity without contributing usable owners. This unrelated actor issue is not deferred again.
  - `[high]` `[defer]` `[P5-ECH-09]` Carried from P4-BH-08: a pre-commit dead-letter marker failure can cause duplicate external publication. This pre-existing EventStore issue is not deferred again.
  - `[medium]` `[defer]` `[P5-ECH-10]` Carried from P4-BH-09: manual-snapshot same-sequence inference can turn an earlier infrastructure exception into a false `Created` result. This concurrent actor issue is not deferred again.
  - `[medium]` `[defer]` `[P5-ECH-11]` A stale `Processing` checkpoint may contribute an existing pending slot; after its cleanup commits, a pre-commit replacement-admission save returns `false` from inspection and overwrites `pendingCommandTracked`, so finalization skips the now-ownerless durable slot. This concurrent EventStore actor issue is outside Story 4.7.
  - `[medium]` `[defer]` `[P5-ECH-12]` The metadata-read catch in `GetEventsAsync` wraps cancellation as `EventDeserializationException`, unlike the adjacent per-event read path. This is the same unrelated cancellation-classification defect as P5-BH-04.
  - `[medium]` `[reject]` `[P5-ECH-13]` Carried from P2-BH-01/P4-ECH-06: the broad Tenants gitlink delta contains unrelated ancestry and the root moves were not isolated. The spec does not claim that the final SHA/gitlink task is complete, and rewriting its baseline or shared history is not a review patch.
  - `[medium]` `[defer]` `[P5-VG-01]` Carried from P4-BH-01/P4-VG-03: real container publication is excluded from every automatic lane. This unrelated gap is not deferred again.
  - `[low]` `[reject]` `[P5-VG-02]` Carried from P2-BH-15/P4-VG-05: Windows skip outcomes are verified structurally rather than by a Windows lane, but the low unrelated gap remains rejected.
  - `[low]` `[defer]` `[P5-VG-03]` Carried from P2-BH-14/P4-VG-04: the first invalid 24-hour authority boundary remains uncovered and is not deferred again.
  - `[medium]` `[defer]` `[P5-VG-04]` Pre-verified: legacy-source and redirect reads convert state-manager exceptions directly to `Unavailable`, so the actor never discards or poisons a possibly unsafe state cache before the next state-bearing turn. This cache-safety adoption gap is concurrent EventStore work outside Story 4.7.
  - `[medium]` `[defer]` `[P5-VG-05]` Carried from P4-VG-01: stale-checkpoint handoff has no before-commit or commit-then-throw save-fault coverage. This unrelated test gap is not deferred again.
  - `[medium]` `[defer]` `[P5-VG-06]` Carried from P4-VG-02: drain-retry repair still lacks a pre-commit save-fault test. This unrelated test gap is not deferred again.
  - `[medium]` `[defer]` `[P5-VG-07]` Carried from P4-BH-09: manual-snapshot same-sequence success inference remains broader than the save ambiguity it is meant to resolve. This unrelated actor defect is not deferred again.
- grouped survivors:
  - `[medium-unverified]` `[defer]` Admission staging exceptions: P5-BH-01 needs a fault test or Dapr implementation guarantee to establish whether a post-staging exception can leave an abandoned batch commit-capable.
  - `[medium]` `[defer]` Legacy idempotency cache safety: P5-BH-02 and P5-VG-04 expose migration/read failure paths that bypass the actor's discard-or-poison protocol.
  - `[medium]` `[defer]` Cancellation classification: P5-BH-04 and P5-ECH-12 show `GetEventsAsync` translating cancellation into data corruption.
  - `[high]` `[defer]` Malformed publication-index activation: P5-BH-05 and P5-BH-06 show a malformed duplicate can terminalize/prune a valid owner and malformed indexes can bypass activation work bounds.
  - `[medium]` `[defer]` Corrective-release v4 dispatch: P5-BH-10 shows the documented successor-handler procedure conflicts with the dispatcher's hard-coded v3 codec pin.
  - `[medium]` `[defer]` Stale-Processing pending-slot cleanup: P5-ECH-11 shows a pre-commit replacement-admission failure can leak the prior durable slot.
  - `[low]` `[defer]` Deferred-ledger evidence: P5-BH-14 leaves already-added actor tests described as missing.
  - `[high]` `[defer]` Carried actor, publication, snapshot, and verification findings retain their pass-2 through pass-4 routes and were not deferred again.
- loopback: none; no intent gap, bad spec, or Story 4.7 patch survived triage. The review added no production change and preserves the still-open reviewed-SHA/gitlink and broad Debug/source validation tasks.

### 2026-09-06 — Review pass 7 (bmad-build)

- verdicts: 31 findings — high 0, medium 12, low 3, false 8, maybe-false 0, carried 8
- routes: intent_gap 0, bad_spec 0, patch 2, defer 8, reject 21
- findings:
  - `[medium]` `[reject]` `[P7-BH-01]` Carried from P2-BH-01/P5-BH-15: the concatenated EventStore-since-baseline plus Tenants-dirty subject is not an isolated Story 4.7 tree. Correcting the baseline is a spec-only remedy.
  - `[medium]` `[reject]` `[P7-BH-02]` Carried from P5-BH-15: pass-6 Tenants edits are still an uncommitted delta on `6eb579fa`, while the audited range remains `d2b7ede3..37fcfded`. Publication/gitlink completion is already open and AC5 was waived; rewriting SHAs is spec/history work.
  - `[medium]` `[reject]` `[P7-BH-03]` Carried from P1-BH-01/P5-ECH-13: the EventStore half of the baseline-wide diff moves many gitlinks. Story 4.7's Tenants delta does not move the root gitlink; this run left it at `6eb579fa`.
  - `[false]` `[reject]` `[P7-BH-04]` Carried from P1-BH-13: `sprint-status.yaml` changes in the EventStore-since-baseline diff are later orchestrator-owned edits, not this Story 4.7 implementation. The Tenants pass-6 delta does not touch that file.
  - `[medium]` `[patch]` `[P7-BH-05]` `SendEventStoreGetTenantUntilHandlerComputedAsync` retries HTTP 200 `ProjectionBacked` instead of failing closed. The frozen EventStore-gateway row requires fail-closed on contradictory provenance, and AC3 is a single-request `HandlerComputed` proof.
  - `[medium]` `[patch]` `[P7-BH-06]` `EnsureEventStoreSidecarHandlerQueryTypesAsync` writes `eventstore||admin:query-types:tenants` before the EventStore assertion, so the live proof can observe a test-injected catalog rather than `AdminOperationalIndexHostedService`. Frozen Never forbids treating mocks as freshness proof.
  - `[medium]` `[defer]` `[P7-BH-07]` EventStore `DaprDomainQueryHandlerRegistry` fail-opens to the projection-actor path when `admin:query-types:tenants` is missing after `AdminOperationalIndexHostedService` skips writes (sample metadata `InvalidOperationException`). This is pre-existing platform topology (DW-107 class), not a Tenants producer defect.
  - `[false]` `[reject]` `[P7-BH-08]` `PublishFailed` → `Assert.Skip` is the approved pass-6 patch so a pub/sub outage is not scored as a provenance failure. This run's live proof completed 1/1 with 0 skips; a skip is still not completion evidence.
  - `[low]` `[patch]` `[P7-BH-09]` If the 4-minute CTS cancels an in-flight `/alive` GET, `WaitForTenantsApiAliveAsync` lets `TaskCanceledException` escape because the catch requires `!timeout.IsCancellationRequested`. The Delay path already breaks into the existing `TimeoutException`.
  - `[false]` `[reject]` `[P7-BH-10]` `AssertPrimaryReadModelInputsExist` stamps `tenant-sequence:42` on every setup model so whichever row is primary actually carries sequence; ETag-only and degenerate tokens remain on the factory and `TenantQueryHandlerETagTests` seams (P2-BH-09).
  - `[false]` `[reject]` `[P7-BH-11]` Balanced-quote `NormalizeETag` retaining `W/"abc"` and unmatched `"abc` is the pass-6 specified result. Weak-tag HTTP parsing was rejected at P2-BH-10/P4-BH-10.
  - `[false]` `[reject]` `[P7-BH-12]` `SharedClientRelayHandler` clones method, URI, and headers for the GET-only typed client; `TenantsRestQueryClient.SendAsync` sends no body. Isolation is the Authorization header on the outer client, which is copied onto the clone.
  - `[medium]` `[reject]` `[P7-BH-13]` Missing DW-487–DW-494 rows in `.bmad-loop/decisions.json` is ledger/orchestrator bookkeeping. The only in-story fix would edit this spec or agent-context files.
  - `[medium]` `[reject]` `[P7-BH-14]` Missing pass-6 triage-log section and stale RESOLVED gitlink prose are spec-only bookkeeping, rejected by the review rule.
  - `[medium]` `[defer]` `[P7-ECH-01]` `DaprEventStoreDomainEventMarkerStore` first-write race is concurrent EventStore subscription work in the baseline-wide diff, outside Story 4.7.
  - `[medium]` `[defer]` `[P7-ECH-02]` `InMemoryEventStoreDomainEventMarkerStore` retry with `CancellationToken.None` is concurrent EventStore work, outside Story 4.7.
  - `[medium]` `[defer]` `[P7-ECH-03]` `AggregateActor` post-save inspect swallowing cancellation is concurrent EventStore actor work, outside Story 4.7.
  - `[medium]` `[defer]` `[P7-ECH-04]` Carried from P4-BH-05/P5-ECH-06: malformed publication-index remnants can make save inspection throw. Unrelated actor work; not deferred again.
  - `[medium]` `[defer]` `[P7-ECH-05]` Admin consistency empty `tenantId` fallback is concurrent Admin/epic-5 work, outside Story 4.7.
  - `[low]` `[defer]` `[P7-ECH-06]` Repeated `tenantId` query parameters in `AdminTenantAuthorizationFilter` is concurrent Admin work, outside Story 4.7.
  - `[medium]` `[defer]` `[P7-ECH-07]` Admin challenge `MemoryStream` cap is concurrent Admin work, outside Story 4.7.
  - `[medium]` `[defer]` `[P7-ECH-08]` `Consistency.razor` unhandled `ServiceUnavailableException` on expand is concurrent Admin UI work, outside Story 4.7.
  - `[medium]` `[defer]` `[P7-ECH-09]` `Consistency.razor` auth callback after dispose is concurrent Admin UI work, outside Story 4.7.
  - `[low]` `[defer]` `[P7-ECH-10]` `DaprActors.razor` access-denied banner on deep-link inspect is concurrent Admin UI work, outside Story 4.7.
  - `[false]` `[reject]` `[P7-ECH-11]` Unmatched wrapping quotes are retained by the new balanced stripper; that is the approved pass-6 ETag contract, not a regression to `Trim('"')`.
  - `[low]` `[patch]` `[P7-ECH-12]` Same `/alive` cancellation hole as P7-BH-09.
  - `[low]` `[reject]` `[P7-ECH-13]` Duplicate `X-Hexalith-Query-Provenance` values make `SingleOrDefault` throw. EventStore emits one header; throwing on duplicates is fail-closed, and adding retry complexity is disproportionate.
  - `[medium]` `[defer]` `[P7-ECH-14]` `assemble-corrected-deployed-runtime-parity.py` previous-closure unlink is Story 3.15 tooling, outside Story 4.7.
  - `[medium]` `[defer]` `[P7-ECH-15]` `capture-corrected-deployed-runtime-parity-smokes.py` leftover docker container is Story 3.15 tooling, outside Story 4.7.
  - `[false]` `[reject]` `[P7-ECH-16]` Claim that `PublishFailed` skip prevents the Redis-backed proof: same as P7-BH-08; skip is environmental triage, not a passing substitute, and this run executed 0 skips.
  - `[medium]` `[defer]` `[P7-VG-01]` Carried from P4-BH-01/P5-BH-09: `HeavyweightContainerPublish` remains excluded from automatic Contracts CI. Unrelated release-governance gap; not deferred again.
- grouped survivors:
  - `[medium]` `[patch]` EventStore live-proof fail-open workaround: P7-BH-05 and P7-BH-06 retry `ProjectionBacked` and write a sidecar handler catalog. Restore a single fail-closed EventStore POST and do not mutate Redis routing keys.
  - `[low]` `[patch]` `WaitForTenantsApiAliveAsync` cancellation: P7-BH-09 and P7-ECH-12. Catch a CTS-cancelled `/alive` GET and surface the existing `TimeoutException`.
  - `[medium]` `[defer]` EventStore handler-registry fail-open: P7-BH-07 remains platform topology outside Tenants producer scope.
  - `[medium]` `[defer]` Concurrent EventStore/Admin/3.15 findings: P7-ECH-01 through P7-ECH-10, P7-ECH-14, P7-ECH-15, and carried P7-ECH-04/P7-VG-01 retain prior routing and are not deferred again.
- loopback: none; no intent gap or bad spec survived. Apply the two Story 4.7 patches.

### 2026-09-06 — Review pass 8 (bmad-build)

- verdicts: 29 findings — high 0, medium 16, low 1, false 12, maybe-false 0, carried 11
- routes: intent_gap 0, bad_spec 0, patch 0, defer 11, reject 18
- findings:
  - `[false]` `[reject]` `[carried]` `[P8-BH-01]` Carried from P7-BH-08: `PublishFailed` → `Assert.Skip` remains the approved pass-6 environment triage. This run's subject still contains that skip; a skip is still not completion evidence.
  - `[false]` `[reject]` `[P8-BH-02]` The live Aspire proof covers `get-tenant` only. Frozen Always six-route coverage is the handler/factory matrices; AC3 specifies one uniquely persisted tenant through EventStore, generated API, and typed client.
  - `[false]` `[reject]` `[P8-BH-03]` After the fail-closed EventStore POST, a missing `admin:query-types:tenants` catalog yields `ProjectionBacked` and the test fails. AC3 does not require asserting Event 6104 or the catalog key.
  - `[false]` `[reject]` `[P8-BH-04]` EventStore `Metadata.IsNotModified` is the stripped producer validator (`null`). Typed-client `IsNotModified` is the HTTP 304 flag from `TenantsRestQueryClient.ReadMetadata`, not a second producer contract.
  - `[medium]` `[defer]` `[P8-BH-05]` `WriteDomainQueryTypeIndexAsync` returns false without `DeleteStateAsync` when recovered types are empty, so a prior `admin:query-types:{domain}` list can remain. This is DW-495 EventStore routing, which Story 4.7 intent excludes.
  - `[medium]` `[defer]` `[carried]` `[P8-BH-06]` Carried from P7-ECH-01: Dapr `TryAcquireAsync` still never persists `InProgress`. Concurrent EventStore marker work; not deferred again.
  - `[medium]` `[defer]` `[P8-BH-07]` In-memory `TryAcquireAsync` returns `(EventStoreDomainEventMarkerAcquisitionResult)(-1)` for unknown states while the Dapr store throws. Concurrent EventStore marker protocol, not Tenants producer scope.
  - `[medium]` `[defer]` `[carried]` `[P8-BH-08]` Carried from P7-ECH-05: blank `tenantId` still injects `tenantClaims.FirstOrDefault()`. Concurrent Admin work; not deferred again.
  - `[false]` `[reject]` `[P8-BH-09]` `QueryEnvelope` construction requires non-whitespace `UserId` (`QueryEnvelope.cs:182`). A requester-less query never reaches `GetTenantAuditQueryHandler`.
  - `[medium]` `[defer]` `[P8-BH-10]` Binding `get-tenant-audit` cursors to requester identity can invalidate previously issued cursors. The handler is outside the Story 4.7 Code Map.
  - `[medium]` `[defer]` `[P8-BH-11]` `IsValidTenantAuditPayload` fails the whole page on an unknown event type or category mismatch. Tenants UI validation, not producer provenance.
  - `[medium]` `[defer]` `[P8-BH-12]` `TenantAuditSupportSafety` matches unsafe fragments against an alphanumeric-only collapsed string, so identifiers containing `token`/`secret`/`eyj` are rejected. Tenants UI safety, not Story 4.7.
  - `[medium]` `[defer]` `[carried]` `[P8-BH-13]` Carried from P7-ECH-08 and P7-ECH-09: `OnRowClick` still lets `ServiceUnavailableException` escape, and `OnAuthenticationStateChanged` still queues `InvokeAsync` without a `_disposed` guard. Concurrent Admin UI; not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P8-BH-14]` Carried from DW-493/DW-490: inert `ReadModelFreshnessOptions` and unsatisfiable UI projection-confirmation remain. Frozen Design Notes keep the signature; not deferred again.
  - `[medium]` `[reject]` `[carried]` `[P8-BH-15]` Carried from P7-BH-01/P7-BH-04: the concatenated EventStore-since-baseline plus Tenants-since-`d2b7ede3` subject is not an isolated Story 4.7 tree, and `sprint-status.yaml` movement is orchestrator-owned. Spec/history rewrite is rejected.
  - `[false]` `[reject]` `[P8-ECH-01]` The cited `HasMore && Items.Count < pageSize` guard does not exist. Nonempty short pages keep `payload.HasMore`; `Empty()` runs only when `rows.Count == 0`.
  - `[medium]` `[defer]` `[P8-ECH-02]` `TenantAuditRow.FromEntry` falls back to `tenantId` when an Access narrative has no safe `UserId`. Tenants UI targeting, not producer provenance.
  - `[medium]` `[defer]` `[P8-ECH-03]` Terminal reconciliation with a null current owner removes the lock and nulls `Reconciliation`, so a later owner cannot adopt that terminal result. Concurrent Tenants command-admission UI.
  - `[medium]` `[defer]` `[P8-ECH-04]` The remove-dispatch `catch` can still overwrite an accepted lease with `Ambiguous` after a later exception. Concurrent Tenants UI, not Story 4.7.
  - `[medium]` `[defer]` `[carried]` `[P8-ECH-05]` Carried from P7-ECH-05 / P8-BH-08: blank `tenantId` still injects the first tenant claim. The cited `tenantClaims.Count != 1` deny guard is not in the current filter. Not deferred again.
  - `[medium]` `[defer]` `[P8-ECH-06]` `TenantDetailPage` still calls caller-free `MatchesScope(request)` while the gateway binds retention with caller scope. Concurrent Tenants UI.
  - `[medium]` `[defer]` `[P8-ECH-07]` Same in-memory unknown-state `(-1)` result as P8-BH-07. Concurrent EventStore marker protocol.
  - `[medium]` `[defer]` `[P8-ECH-08]` In-memory marker `Transition` retries until cancel, unlike the Dapr five-attempt cap. Concurrent EventStore marker protocol.
  - `[false]` `[reject]` `[P8-ECH-09]` `WriteTooLargeAsync` checks `Response.HasStarted` and does not write a second 413 problem body; it rethrows the original overflow exception.
  - `[medium]` `[defer]` `[P8-ECH-10]` Same empty recovered catalog as P8-BH-05. The claimed `DeleteStateAsync` line is not in `WriteDomainQueryTypeIndexAsync`; the leftover prior key is the actual outcome.
  - `[medium]` `[defer]` `[carried]` `[P8-VG-01]` Carried from P7-VG-01: `HeavyweightContainerPublish` remains excluded from automatic Contracts CI. Unrelated release-governance gap; not deferred again.
  - `[medium]` `[defer]` `[P8-VG-02]` Pre-verified: hosted-service tests never seed a leftover `admin:query-types:{domain}` and then recover an empty list. Same DW-495 empty-catalog defect as P8-BH-05; Story 4.7 intent excludes EventStore routing, so this is not a story patch.
  - `[medium]` `[defer]` `[P8-VG-03]` Pre-verified: no Operator+Running test asserts Cancel is hidden. The page already wraps Cancel in `AuthorizedView MinimumRole=Admin`. Concurrent Admin UI.
  - `[low]` `[defer]` `[P8-VG-O1]` `Consistency_ShowsTriggerButton_ForOperatorUser` uses the default Admin identity and never calls `ConfigureRole(Operator)`. Concurrent Admin UI test naming.
- grouped survivors:
  - `[medium]` `[defer]` Empty recovered query-type catalogs leave a prior handler index: P8-BH-05, P8-ECH-10, and P8-VG-02.
  - `[medium]` `[defer]` In-memory vs Dapr marker protocol: P8-BH-07, P8-ECH-07, and P8-ECH-08.
  - `[medium]` `[defer]` `get-tenant-audit` requester-bound cursors: P8-BH-10.
  - `[medium]` `[defer]` Unknown audit events fail the whole page: P8-BH-11.
  - `[medium]` `[defer]` Alphanumeric audit-safety false positives: P8-BH-12.
  - `[medium]` `[defer]` Access audit target fallback: P8-ECH-02.
  - `[medium]` `[defer]` Terminal reconciliation dropped after owner left: P8-ECH-03.
  - `[medium]` `[defer]` Remove-dispatch catch overwrites accepted lease: P8-ECH-04.
  - `[medium]` `[defer]` Caller-free audit `MatchesScope` on tenant detail: P8-ECH-06.
  - `[medium]` `[defer]` Missing Operator+Running Cancel test: P8-VG-03.
  - `[low]` `[defer]` Misnamed Operator trigger test: P8-VG-O1.
- loopback: none; no intent gap, bad spec, or Story 4.7 patch survived. Carried deferrals were not written again.

## Design Notes

Keep the active freshness overload signature so all handler constructors and call sites stay stable, but delegate it to the validator-only factory. The persisted read model still stores timestamp and sequence for replay/idempotency; only query-response authority changes. The Tier-3 proof must inspect Redis before both raw HTTP and typed-client assertions because a completed command or successful response does not establish projection origin.

## Verification

**Commands:**

- `dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false -nodeReuse:false -m:1` then Debug build/tests by project -- expected: source-mode graph and affected suites pass.
- `dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Release -p:UseHexalithProjectReferences=false -nodeReuse:false -m:1` then Release build/tests by project -- expected: package-mode graph and affected suites pass.
- `dotnet test tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj` and `dotnet test tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj` with matching mode/configuration and no-build/no-restore -- expected: producer matrices pass; the named Aspire proof executes with zero skips and verifies Redis plus raw/typed routes.
- `git -C references/Hexalith.Tenants diff --check` and `git diff --check` -- expected: clean Tenants and outer diffs, with no `sprint-status.yaml` or unrelated outer-tree changes attributable to Story 4.7.

### Review Findings

Review pass 6 (2026-09-06, `bmad-code-review`). Reviewed range corrected mid-review: the staged diff `d2b7ede3..a54f0b95` is not the story's final state. Story 4.7 test work continues through `ce0e2aab` and `37fcfded`; the audited range is `d2b7ede3..37fcfded` restricted to the Code Map file set (9 files, +543/-151). Four layers ran; none failed.

**Decision needed — all three resolved by the Administrator on 2026-09-06**

- [x] [Review][Decision] RESOLVED (accepted 2026-09-06: gitlink `b7d3619e` accepted as a superset of the reviewed artifact; the AC5 "separately authorized gitlink" clause is formally waived, and the reviewed range `d2b7ede3..37fcfded` is the audited artifact of record). Root gitlink was published at a non-compiling Tenants tree — AC5 remains unmet. The EventStore root gitlink was moved to `a54f0b95` (root commit `c08cb349`) before any reviewed SHA existed. At `a54f0b95` `tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs:37` carries a bare `using StackExchange.Redis;` while line 419 uses unqualified `CommandStatus.Rejected`; `StackExchange.Redis.CommandStatus` collides with `Hexalith.EventStore.Contracts.Commands.CommandStatus`, so the whole IntegrationTests project fails CS0104. The collision is resolved only at `37fcfded`, which replaces the namespace import with five type aliases. The gitlink is now `b7d3619e` — three commits past the reviewed tip and carrying unrelated stories. Decide the authorized reviewed SHA and whether the gitlink is re-pointed.
- [x] [Review][Decision] RESOLVED (accepted 2026-09-06: keep the frozen Design Notes signature and host binding unchanged; cleanup deferred as DW-493). Freshness plumbing is inert but still operator-configurable. `TenantQueryResult.FromPayload`'s six-argument overload discards `readModel`, `thresholds`, and `now`. `ReadModelFreshnessOptions` is still bound, `.Validate(...)`d and `.ValidateOnStart()`d in `src/Hexalith.Tenants/Program.cs:71-75`, threaded through all six handler constructors, and converted to `_freshnessThresholds`/`_timeProvider` in `TenantQueryHandlerBase.cs:45,76,156-166` — so `ReadModelFreshness:Aging`/`Stale` accept any valid value with no observable effect anywhere. Two comments still describe `ToQueryResponseMetadata` as live (`TenantsRestQueryClient.cs:391`, `TenantQueryGatewayTests.cs:2934`). Design Notes deliberately froze the signature for caller stability, so removing the dead surface contradicts the approved spec — human call required.
- [x] [Review][Decision] RESOLVED (accepted 2026-09-06: the recorded focused-lane evidence is accepted in place of the blocked broad gate; the full-solution blocker is recorded as DW-494). AC4 is unmet while frontmatter declares `status: 'done'`. The fresh dual-mode task is unchecked and Implementation Notes record that both full-solution restores are blocked by uninitialized nested submodules, the Debug/source Integration build stops at `references/Hexalith.Memories/Directory.Build.props:89`, and Release/package stops at `src/Hexalith.Tenants.AppHost/Program.cs:132` (CS1503). Accept the focused-lane evidence in place of the broad gate, or hold the story open.

**Patch**

- [x] [Review][Patch] Six-route freshness matrix has no control proving its inputs exist [tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs:55-113]
- [x] [Review][Patch] Tier-3 Redis helper degrades to opaque failures — `JsonException` escapes the retry loop and `AbortOnConnectFail=false` lets a dead Redis surface as `RedisConnectionException` instead of the crafted diagnostic [tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs:442-470]
- [x] [Review][Patch] Fixture change widens Tier-3 blast radius — `tenants-api` is the only `https` resource among five, is inserted before `tenants-ui`/`sample`, waits on aliveness with `CancellationToken.None` (outside the 6-minute startup budget), and the new test mutates the shared client's `DefaultRequestHeaders.Authorization` [tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs:26]
- [x] [Review][Patch] `using StackExchange.Redis` types with no `PackageReference` — compiles transitively only; version already exists in Builds central props [tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj]
- [x] [Review][Patch] Magic `"tenant-sequence:"` literals instead of the live `TenantProjectionVersionFormat.SequencePrefix` constant [tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs:227]
- [x] [Review][Patch] `NormalizeETag` lossy cases uncovered — `W/"abc"` becomes `W/"abc`, the two degenerate-ETag theories are asymmetric (5 cases vs 4), and the `JsonValueKind.Undefined` guard gained no replacement coverage after being removed from the six-argument overload [src/Hexalith.Tenants/Queries/TenantQueryResult.cs:46-53]
- [x] [Review][Patch] Tier-3 proof misreports an environment pub/sub outage as a provenance-contract failure [tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs:140-152]

**Deferred**

- [x] [Review][Defer] Flagship route evidence cannot fail Tenants CI [tests/Hexalith.Tenants.IntegrationTests/] — deferred: DW-487; closing it is a CI-policy change beyond approved scope
- [x] [Review][Defer] `X-Hexalith-Served-At` and `X-Hexalith-Is-Degraded` are not provenance-gated [src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:472-485] — deferred: DW-488; pre-existing platform fail-open, and the spec forbids editing the emitter
- [x] [Review][Defer] Production behavior change published as `refactor(tests)` [commit 2a204a03] — deferred: DW-489; history already published
- [x] [Review][Defer] UI projection-confirmation gate is permanently unsatisfiable for Tenants routes [src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:2517] — deferred: DW-490; pre-existing, predates this change
- [x] [Review][Defer] `ToQueryResponseMetadata` has no production caller but still advertises producer authority [src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs:62-84] — deferred: DW-491; EventStore-side, out of story scope
- [x] [Review][Defer] UI truth-state spec still documents an unreachable 304 freshness primitive [docs/tenants-ui-truth-state-and-action-availability-spec.md:102] — deferred: DW-492; fix edits another spec
- [x] [Review][Defer] Inert operator-facing read-model freshness configuration [src/Hexalith.Tenants/Program.cs:71-75] — deferred: DW-493; frozen Design Notes keep the signature deliberately, so the cleanup needs its own story
- [x] [Review][Defer] AC4 broad-gate dual-graph validation blocked by uninitialized nested submodules [references/Hexalith.Tenants/Hexalith.Tenants.slnx] — deferred: DW-494; accepted on focused-lane evidence

**Rejected**

- `false` — "EventStore query leg missing from AC3 proof": stale range artifact; the `/api/v1/queries` leg exists at `37fcfded:166-198` with full metadata assertions.
- `false` — "Redis/payload comparison covers only 3 of 7 fields": stale; `AssertTenantDetailMatchesPersisted` compares identity, name, description, status, created-at, members, and configuration.
- `false` — "`ResolveRedisPort()` honours an env override that can point at the wrong store": stale; `37fcfded:442` binds `DaprDiagnostics.DefaultRedisPort`.
- `false` — verification-gap's restatement of the 3-field comparison, same refutation as above.
- rejected — "Frontmatter bookkeeping (`review_loop_iteration: 1`, `deferred: []`) contradicts recorded history": the only fix is to edit the spec under review.
- `low` — "Degenerate-ETag coverage exercises only `get-tenant`": all six routes provably share one factory seam, which this diff confirms; previously adjudicated as P2-BH-09.
- `low` — "Header assertions use raw string literals": no shared constant exists, so the fix adds new public surface for a rename hazard that has not occurred.
- `low` — "Brace style inconsistent within the changeset": the new code matches the pre-existing same-line style of the files it edits; only the wholesale Allman reformat of `TenantQueryFreshnessTests` differs, and it matches `.editorconfig`.
- `low` — "Changeset ships no positive control for header emission": the emission side has blocking coverage in `tests/Hexalith.EventStore.RestApi.Generators.Tests/` and `QueryResponseProvenanceE2ETests`.

Review pass 10 (2026-09-08, `bmad-code-review`). Chunked Code Map + EventStore spec/gitlink; four layers ran; none failed.

**Patch**

- [ ] [Review][Patch] Quote-only ETags remain as producer validator metadata after balanced unwrap [references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs:46-56]

**Rejected**

- `false` — "Handler still calls `GetUtcNow()` after freshness authorship was removed": spec Code Map forbids changing `TenantQueryHandlerBase` call sites; `TimeProvider.System.GetUtcNow()` does not fail closed on the success path.
- `false` — "Synthetic generated-controller test never inspects JSON for leaked metadata fields": `EnqueueQueryResult` serializes a `TenantDetail` that cannot carry those properties; the live raw `JsonDocument` scan already covers the real body.
- `false` — "Synthetic test omits `X-Hexalith-Is-Degraded` / `ServedAt` pinning": planted metadata leaves both null; live proof asserts `Is-Degraded` absent; `ServedAt` is gateway timing (P2-BH-08) and emitter emission is DW-488.
- `false` — "EventStore `SubmitQueryResponse`/`TenantDetail` deserialize drops smuggled payload properties": handler payload is `TenantDetail` without those fields; provenance claims live on sibling `Metadata`, which the EventStore leg already asserts.
- `false` — "`AssertTenantDetailMatchesPersisted` skips `GetConcreteMembers`": equal Redis/HTTP member counts cannot silently accept a filtered payload; a fresh `CreateTenant` row has only concrete roles.
- `false` — "Live proof builds tenant ids with `Guid.NewGuid()`": Tenants aggregate ids are caller-supplied strings, not ULIDs; the same file already uses this uniqueness pattern.
- `false` — "`PublishFailed` → `Assert.Skip` lets a pub/sub outage pass AC3": skip is not a passed result (P7-BH-08 / P8-BH-01); pass-6 approved it as environment triage, and recorded live runs executed 0 skips.
- rejected — "Frontmatter `review_loop_iteration: 6` / unchecked gitlink vs pass-9 notes and `e7f36662` pointer": the only fix is to edit the spec under review.
- `low` — "Six-argument `FromPayload` lacks XML `<param>` notes that `readModel`/`thresholds`/`now` are discarded": the file has no XML on either factory; Design Notes already froze the compatibility seam (DW-493).
- `low` — "`SharedClientRelayHandler` is a second type in `AspireTopologyTests.cs`": the file already nests several test helpers; extracting a new file adds surface.
- `low` — "Relay handler never disposes the cloned `HttpRequestMessage`": clone has no `Content`; one-shot test request.
- `low` — "`WaitForPersistedTenantAsync` uses 60s `SampleProjectionTimeout` instead of the 5-minute test CTS": recorded proofs finish in ~26s; the cap also prevents a hung Redis poll from consuming the whole budget.
- `low` — "Proof never deletes the `provenance-*` Redis/aggregate tenant": leftover test data; cleanup would add persistence plumbing the spec forbids.
- `low` — "Shared fixture waits for `tenants-api` Running on every topology test": Code Map requires exposing that existing resource; `WaitForAliveness` is already false so only client creation waits for Running.
