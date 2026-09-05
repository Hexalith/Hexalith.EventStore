---
title: 'Tenants Query Provenance Follow-Up'
type: 'bugfix'
created: '2026-09-05'
status: 'in-progress'
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

- [ ] `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs` -- preserve opaque validator metadata while removing both ETag/version aliases and all timestamp-derived authority.
- [ ] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Queries/{TenantQueryHandlerETagTests,TenantQueryFreshnessTests,TenantQueryResultTests}.cs` -- pin both factories, all six routes, every timestamp/sequence case, and degenerate ETags.
- [ ] `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/{TenantsApiGeneratedControllerTests,AspireTopologyTests}.cs` and `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs` -- prove synthetic header policy plus a zero-skip real `get-tenant` flow whose payload is first verified in Redis at `tenants||projection:tenants:<id>`.
- [ ] `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/State/{TenantMembershipCommandProvenanceTests,TenantLifecycleCommandSnapshotTests}.cs` -- remove stale comments without changing UI behavior.
- [ ] `references/Hexalith.Tenants/Hexalith.Tenants.slnx` and affected test projects -- perform fresh Debug/source and Release/package restores, builds, and project-level tests; record exact results and skips.
- [ ] `references/Hexalith.Tenants` -- after local validation, obtain a reviewed Tenants commit/published SHA, then update only this root gitlink when the outer tree is stable; never mix concurrent root edits.

**Acceptance Criteria:**

- Given any of the six handlers returns a row with an opaque ETag, when its raw `TenantQueryResult` is inspected, then only normalized ETag and `IsNotModified=false` are populated and every projection/freshness authority field remains absent.
- Given genuine `tenant-sequence:<n>` or current, old, or missing `ProjectedAt`, when `get-tenant` or another handler executes, then the result is identical validator-only metadata and no evidence promotes the route.
- Given a uniquely persisted tenant, when EventStore and the generated Tenants API read it with a conflicting validator, then Redis content matches the payload, HTTP remains 200, provenance is `HandlerComputed`, lifecycle is `Unknown`, and unsupported body/header metadata is absent.
- Given fresh source and package dependency modes, when the solution builds and affected test projects run independently, then both graphs pass without nested submodule initialization or reused restore assets.
- Given completion is claimed, when repository identity is checked, then evidence records the approved baseline, reviewed Tenants commit and full published SHA, exact validation results, and separately authorized root gitlink with no unrelated outer-tree changes.

## Implementation Notes

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

## Design Notes

Keep the active freshness overload signature so all handler constructors and call sites stay stable, but delegate it to the validator-only factory. The persisted read model still stores timestamp and sequence for replay/idempotency; only query-response authority changes. The Tier-3 proof must inspect Redis before both raw HTTP and typed-client assertions because a completed command or successful response does not establish projection origin.

## Verification

**Commands:**

- `dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false -nodeReuse:false -m:1` then Debug build/tests by project -- expected: source-mode graph and affected suites pass.
- `dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Release -p:UseHexalithProjectReferences=false -nodeReuse:false -m:1` then Release build/tests by project -- expected: package-mode graph and affected suites pass.
- `dotnet test tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj` and `dotnet test tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj` with matching mode/configuration and no-build/no-restore -- expected: producer matrices pass; the named Aspire proof executes with zero skips and verifies Redis plus raw/typed routes.
- `git -C references/Hexalith.Tenants diff --check` and `git diff --check` -- expected: clean Tenants and outer diffs, with no `sprint-status.yaml` or unrelated outer-tree changes attributable to Story 4.7.
