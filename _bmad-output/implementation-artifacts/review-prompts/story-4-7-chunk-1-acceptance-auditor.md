# Story 4.7 Chunk 1 — Acceptance Auditor

You are an Acceptance Auditor. Review the provided diff against the embedded Story 4.7 specification and loaded context documents. Check for: violations of acceptance criteria, deviations from spec intent, missing implementation of specified behavior, contradictions between spec constraints and actual code. Output findings as a Markdown list. Each finding: one-line title, which AC/constraint it violates, and evidence from the diff.

Do not invoke any skill, and do not spawn subagents of your own — you are the reviewer. Return your findings as text in your final message; do not route them through any findings-reporting tool the host may offer.

## Story 4.7 specification

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
- `references/Hexalith.Memories/Directory.Build.props` and `references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/NaturalLanguage/SubmoduleGuardTests.cs` -- let the Memories source graph accept its resolved enclosing/sibling EventStore repository when the nested EventStore submodule is absent, and pin submodule-root discovery in the regression harness.
- `src/Hexalith.EventStore/{Queries/HandlerAwareQueryRouter.cs,Controllers/QueriesController.cs}` and `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` -- authoritative stamping/stripping already exists; do not edit.

## Tasks & Acceptance

**Execution:**

- [x] `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs` -- preserve opaque validator metadata while removing both ETag/version aliases and all timestamp-derived authority.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Queries/{TenantQueryHandlerETagTests,TenantQueryFreshnessTests,TenantQueryResultTests}.cs` -- pin both factories, all six routes, every timestamp/sequence case, and degenerate ETags.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/{TenantsApiGeneratedControllerTests,AspireTopologyTests}.cs` and `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs` -- prove synthetic header policy plus a zero-skip real `get-tenant` flow whose payload is first verified in Redis at `tenants||projection:tenants:<id>`.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/State/{TenantMembershipCommandProvenanceTests,TenantLifecycleCommandSnapshotTests}.cs` -- remove stale comments without changing UI behavior.
- [x] `references/Hexalith.Memories/Directory.Build.props` and `references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/NaturalLanguage/SubmoduleGuardTests.cs` -- accept the resolved enclosing/sibling EventStore repository in source mode without initializing Memories' nested EventStore submodule, and cover that guard configuration.
- [x] `references/Hexalith.Tenants/Hexalith.Tenants.slnx` and affected test projects -- perform fresh Debug/source and Release/package restores, builds, and project-level tests; record exact results and skips. The Administrator-approved DW-494 disposition accepts the recorded focused project lanes in place of the full-solution restore blocked by forbidden nested-submodule initialization.
- [x] `references/Hexalith.Tenants` -- after local validation, obtain a reviewed Tenants commit/published SHA, then update only this root gitlink when the outer tree is stable; never mix concurrent root edits. Reviewed Story 4.7 tip `54fc4040dc6348e5560fffc246a27e72dc6558fe` is contained by published SHA `2fac18396ff11a4459de053b3ebb7ddfe7c13e30`; isolated root commit `23de7bc32817ff12da72e902d71c65e891ac0c91` selects that exact published SHA under the Administrator-approved superset/gitlink waiver.

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
- Validation resumed on 2026-09-10 at EventStore `293c69c42d35dee26682d42f05c943c0b65786f4` and published Tenants `2fac18396ff11a4459de053b3ebb7ddfe7c13e30`; the root gitlink selects that exact Tenants SHA. Fresh Debug/source and Release/package Server restores/builds passed with 0 warnings and 0 errors, and the three query classes passed 55/55 in each mode by the required direct-assembly fallback after maintained `dotnet test` discovered zero tests and exited 5. Release/package Integration restore/build passed and generated-controller coverage passed 28/28; Debug/source and Release/package UI restores/builds passed and the two affected classes passed 91/91 in each mode, all with zero skips.
- The same 2026-09-10 run remains incomplete. Debug/source Integration restore passed, but build stopped with three `references/Hexalith.Memories/Directory.Build.props:89` errors because the forbidden nested `references/Hexalith.EventStore` submodule is absent. The Release live Aspire proof then failed twice before its test body (1 failed, 0 skipped in 255.779 seconds and 255.603 seconds): `AspireTopologyFixture.InitializeAsync` timed out after four minutes because resource `eventstore` endpoint `/alive` never returned HTTP 200. Aspire teardown completed after both attempts (`aspire ps --format Json --non-interactive` returned `[]`). Tasks 5 and 6 therefore remain open; this run did not initialize nested submodules, commit, push, edit `sprint-status.yaml`, or move the root gitlink.
- Follow-up on 2026-09-10 implemented the Administrator's source-layout clarification in Memories: the `CheckSubmodules` item for EventStore now carries `$(HexalithEventStoreRoot)` as an accepted resolved repository, while the guard still rejects a missing unresolved dependency. The direct target changed from the recorded missing-submodule failure to exit 0 with the nested EventStore submodule still absent. Memories Server Tests then built with 0 warnings and 0 errors; the four-test `SubmoduleGuardTests` class completed with 3 passed, 0 failed, and its pre-existing destructive test skipped. After a fresh Debug/source restore, Tenants IntegrationTests built with 0 warnings and 0 errors.
- A no-version-override Debug/source rebuild exposed a separate pre-existing source-graph identity collision: NuGet transitive project-reference expansion evaluates `Hexalith.Tenants.Contracts` at both `5.7.0` and EventStore's `3.103.0`, shares one output path, and leaves the generated-controller fixture unable to load the requested `5.7.0.0` assembly (28 failed, 0 skipped). The one-edge EventStore experiment was reverted after MSBuild diagnostics proved it could not control the transitive edge. Repeating the fresh source restore/rebuild with the repository's established single-version graph override (`-p:Version=3.103.0`) completed with 0 warnings and 0 errors and the generated-controller class passed 28/28 with 0 skips. The Memories guard fix did not initialize nested submodules, change package versions, commit, push, edit `sprint-status.yaml`, or move a gitlink.
- Final focused verification on 2026-09-10 repeated the complete affected matrix after the Memories guard fix. Debug/source and Release/package Server, Integration, and UI restores/builds completed with 0 warnings and 0 errors; the three Server query classes passed 55/55 in each mode, the generated-controller class passed 28/28 in each mode, and the two affected UI classes passed 91/91 in each mode, all with zero skips. Maintained Server `dotnet test` still discovered zero tests and exited 5, so those results use the repository-prescribed direct-assembly fallback. A third Release live attempt failed before its test body (1 failed, 0 skipped in 255.894 seconds): `AspireTopologyFixture.InitializeAsync` timed out after four minutes because resource `eventstore` endpoint `/alive` never returned HTTP 200. Aspire teardown completed (`aspire ps --format Json --non-interactive` returned `[]`). Task 6 remains open; no nested submodule was initialized and no commit, push, `sprint-status.yaml` edit, or gitlink move occurred.
- Follow-up diagnosis on 2026-09-10 proved the timeout masks an EventStore process crash: the default Release run failed 1/1 before the test body in 255.887 seconds, `aspire describe eventstore` reported `Finished` with exit code 134 and no `Authentication__JwtBearer__*` environment, and EventStore logged `OptionsValidationException: Authentication:JwtBearer requires exactly one of Authority or SigningKey to be configured.` EventStore commit `dfbbde37782fcb59b9fc4e7514107b20d263d9ba` removed development signing keys after the last green Story 4.7 run; the Tenants AppHost disables Keycloak for this fixture but does not supply replacement local JWT configuration. With process-local JWT settings matching the proof's existing test constants, the exact persisted-route test passed 1/1 with 0 skips in both Release/package mode (26.296 seconds) and Debug/source mode (27.048 seconds, using the established `Version=3.103.0` single-version override), proving the EventStore, generated API, typed-client, and Redis-backed provenance path. Aspire teardown returned `[]` after both runs. Under the Administrator-approved DW-494 focused-lane disposition this completes Task 6; a permanent default-topology repair still requires Tenants AppHost authentication-composition changes outside the frozen producer/test scope, and no source file was changed by this diagnosis.

## Spec Change Log

- 2026-08-27 -- Recorded the exact six-route producer/consumer inventory and authority split at EventStore `168c657676ab2e210401bb5fe1c7ae9df06dc0e7` / Tenants `d5ce92881019d3deca20b5fe03b84f86489dd062`; all EventStore-owned focused and persisted-path verification passed. No authenticated Tenants-maintainer or root-gitlink authority was supplied, so the protected external changes remain unchecked and status moved to `awaiting-operator` without touching `sprint-status.yaml`.
- 2026-09-05 -- Administrator approved the exact Story 4.7 producer/test scope at Tenants `d2b7ede359830c27934ac9f577e3073955c3e2c2` and separate root-gitlink authority. Re-planned from Review pass 1 to isolate the validator-only factory correction, all-six-route tests, persisted real-route proof, and fresh dual-mode validation while preserving EventStore normalization and genuine stored sequence stamping.
- 2026-09-06 -- After the fail-closed live proof returned `ProjectionBacked`, the Administrator authorized a separate EventStore repair (choice 1 / DW-495): persist handler query-type indexes for domains whose metadata loaded successfully, and rewrite them on refresh, without folding EventStore routing into Story 4.7 producer scope.
- 2026-09-10 -- Administrator clarified that source-mode Memories consumption must use its resolved enclosing/sibling EventStore repository when the nested EventStore submodule is absent. Added the non-frozen build-guard task and regression evidence without changing the approved query-provenance scope or initializing nested submodules.

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

### 2026-09-07 — Review pass 9 (bmad-code-review)

- subject: Group 1 Tenants Code Map `d2b7ede3..e7f36662` (10 files, +777/−147, 1256 lines). `TenantQueryHandlerBase.cs` unchanged.
- failed_layers: Verification Gap Reviewer (empty results)
- verdicts: 17 findings — high 0, medium 4, low 4, false 9
- routes: intent_gap 0, bad_spec 0, patch 1, defer 2, reject 13
- findings:
  - `[medium]` `[patch]` `[P9-ECH-01]` After the balanced-quote loop, a leftover `"` is returned as a validator ETag (`"`, `"""`). The I/O matrix requires quote-only tokens to yield null metadata.
  - `[medium]` `[defer]` `[P9-BH-02]` `[P9-BH-03]` Carried from DW-488: generated-controller and topology proofs do not plant or pin `ServedAt`/`IsDegraded`; the emitter still emits those headers without a provenance gate. Not written again.
  - `[medium]` `[defer]` `[P9-BH-05]` Carried from DW-493: the six-argument `FromPayload` still discards freshness inputs under the frozen signature. Not written again.
  - `[false]` `[reject]` `[P9-BH-01]` AC3 specifies a conflicting validator and HTTP 200; not reading Redis HASH `version` does not falsify that proof.
  - `[false]` `[reject]` `[P9-BH-04]` EventStore `IsNotModified: null` is stripped producer metadata; typed-client `false` is the HTTP 304 flag. Same split as P8-BH-04.
  - `[false]` `[reject]` `[P9-BH-06]` Six-route HandlerComputed coverage is the factory/handler matrices; AC3 is one persisted `get-tenant` path. Same as P8-BH-02.
  - `[false]` `[reject]` `[P9-BH-07]` Redis proof already binds `DaprDiagnostics.DefaultRedisPort` and `tenants||`; failing closed on Redis unavailability is required persistence evidence, not a skip.
  - `[false]` `[reject]` `[P9-BH-08]` `SharedClientRelayHandler` serves GET-only `GetTenantAsync` with no body; isolation is the cloned Authorization header. Same as P7-BH-12.
  - `[false]` `[reject]` `[P9-BH-09]` `WaitForAliveness: false` already keeps `/alive` off the shared startup budget; remaining Running/HTTPS wait is required to obtain `TenantsApiClient`.
  - `[false]` `[reject]` `[P9-BH-10]` Nested wrapping is already collapsed by the while-loop; degenerate metadata omission is pinned on both factory overloads. Six-route degenerate cross-product remains P2-BH-09.
  - `[false]` `[reject]` `[P9-BH-11]` Freshness tests plant `tenant-sequence:42` on every primary row; the ETag suite asserts validator-only metadata on the ETag seam.
  - `[low]` `[reject]` `[P9-BH-12]` `CommandStatus` alias is leftover after replacing `using StackExchange.Redis` with type aliases; no remaining name clash.
  - `[low]` `[reject]` `[P9-ECH-02]` `ConnectAsync` is bounded by `ConnectTimeout = 5_000`; adding `WaitAsync` is not everyday-path.
  - `[low]` `[reject]` `[P9-ECH-03]` Uncaught `RedisTimeoutException` fails the persistence proof, which is the correct fail-closed outcome.
  - `[low]` `[reject]` `[P9-ECH-04]` Bootstrap `PublishFailed` is dominated by the already-bootstrapped rejection path; create already skips `PublishFailed`.
  - `[false]` `[reject]` `[P9-ECH-05]` This proof creates a new tenant whose persisted `Members`/`Configuration` are initialized dictionaries, not JSON null.
- grouped survivors:
  - `[medium]` `[patch]` Quote-only leftover ETag: P9-ECH-01.
  - `[medium]` `[defer]` Ungated ServedAt/IsDegraded headers: P9-BH-02 and P9-BH-03, already DW-488.
  - `[medium]` `[defer]` Inert freshness overload: P9-BH-05, already DW-493.
- loopback: none. Apply P9-ECH-01 if the Administrator chooses patch handling.

### 2026-09-10 — Review pass 11 (bmad-build)

- subject: baseline-wide EventStore diff plus explicit Memories and Tenants submodule ranges; Blind Hunter, Edge Case Hunter, and Verification Gap Reviewer completed.
- verdicts: 23 findings — medium 20, low 2, false 1, maybe-false 0; 18 carried.
- routes: intent_gap 0, bad_spec 0, patch 0, defer 19, reject 4.
- findings:
  - `[medium]` `[reject]` `[carried]` `[P11-BH-01]` Carried from P2-BH-01/P7-BH-01/P8-BH-15: the baseline-wide review artifact is not an isolated Story 4.7 tree. Correcting the recorded baseline would edit this build's spec, so the review rule rejects that remedy; the review also inspected the explicit Tenants and Memories ranges.
  - `[low]` `[reject]` `[carried]` `[P11-BH-02]` Carried from P2-BH-02 and the pass-9/pass-10 frontmatter-bookkeeping rejections: `created` reflects the approved replan while the change log preserves imported August history, and `review_loop_iteration` counts build loopbacks rather than every separately named review pass. Any remaining metadata-only remedy edits the spec under review.
  - `[medium]` `[defer]` `[P11-BH-03]` The Keycloak-disabled Tenants test topology is no longer self-contained after EventStore commit `dfbbde37782fcb59b9fc4e7514107b20d263d9ba`: EventStore exits 134 without a JWT authority or signing key. Process-local JWT configuration proves Story 4.7 in both modes, but permanent Tenants AppHost authentication composition is a later platform-integration concern, not producer provenance.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-04]` Carried from DW-487/P6: the persisted-route proof remains in a non-blocking Aspire lane and can report infrastructure skips. This run required an executed zero-skip proof and obtained one in both modes; CI policy remains outside Story 4.7 and is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-05]` Carried from DW-488/P9-BH-02/P9-BH-03: generated REST can emit `ServedAt` and `IsDegraded` on handler-computed responses. The frozen intent excludes EventStore emitter changes, so it is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-06]` Carried from DW-490/P8-BH-14: Tenants configuration confirmation still requires projection evidence that handler-computed routes cannot supply. This unrelated UI workflow is explicitly outside the producer-provenance intent and is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-07]` Carried from DW-493/P9-BH-05: the compatibility freshness overload and bound options remain inert. The frozen Design Notes preserve that signature, so it is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-08]` Carried from DW-492: Tenants UI truth-state documentation still describes a 304 freshness primitive that handler-computed routes cannot author. Documentation outside the producer/test scope is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-09]` Carried from P8-BH-05/P8-ECH-10/P8-VG-02: authoritative empty recovery does not delete a stale `admin:query-types:{domain}` catalog. This separate EventStore routing defect is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-10]` Carried from P7-ECH-01/P8-BH-06: Dapr marker acquisition can race because a missing marker is not conditionally persisted as `InProgress`. This concurrent EventStore subscription concern is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-11]` Carried from P7-ECH-05/P8-BH-08: a blank supplied tenant scope can fall back to the first tenant claim. This concurrent Admin authorization issue is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-12]` Carried from P8-BH-11: one unknown or mismatched audit event can invalidate the entire Tenants audit page. This unrelated UI compatibility issue is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-13]` Carried from P8-BH-12: collapsed substring secret detection can blank legitimate identifiers such as `token-service`. This unrelated audit UI safety issue is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-14]` Carried from P8-ECH-02: Access audit rows without a safe user id can fall back to the tenant id as a correction target. This unrelated UI targeting issue is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-15]` Carried from P8-ECH-03: ownerless lease cleanup can discard terminal reconciliation before a replacement surface adopts it. This unrelated command-admission UI issue is not deferred again.
  - `[medium]` `[defer]` `[carried]` `[P11-BH-16]` Carried from P7-ECH-08/P7-ECH-09/P8-BH-13: `Consistency.razor` still lets non-forbidden expansion failures escape and queues authentication callbacks without a disposal guard. These concurrent Admin UI issues are not deferred again.
  - `[medium]` `[defer]` `[P11-VG-01]` Admin dialog tests assert only the `focusElementById` interop invocation; no browser test proves that closing a destructive dialog restores `document.activeElement` to its initiator. The consequence is real for keyboard users, but Admin UI browser coverage is unrelated to Story 4.7 producer provenance.
  - `[medium]` `[defer]` `[P11-VG-02]` Tenants removal-modal tests replace the Boolean result of `tenantsFocus.js` and never execute the browser helper, so a deployed focus-wrap failure would leave the tests green. Browser automation for this unrelated UI behavior is outside the frozen intent.
  - `[low]` `[reject]` `[P11-ECH-01]` Repeated balanced-quote slicing is quadratic for a very long all-quote state-store ETag, but this requires an abnormal backend validator, is not an everyday path, and a maximum-length policy would add unsupported semantics for negligible practical harm.
  - `[medium]` `[defer]` `[carried]` `[P11-ECH-02]` Same authoritative-empty stale query-catalog defect as P11-BH-09; retained under its prior EventStore defer route and not deferred again.
  - `[medium]` `[defer]` `[P11-ECH-03]` `RefreshAsync` writes only the refreshed registration's handler set, so two registrations for one domain can overwrite one another's `admin:query-types:{domain}` catalog. This is a real separate EventStore routing issue introduced outside Story 4.7's producer scope.
  - `[false]` `[reject]` `[carried]` `[P11-ECH-04]` Carried from P7-BH-08/P8-BH-01/P10: `PublishFailed` produces an explicit skipped result rather than a passing proof, and this workflow required and obtained zero-skip executions in both modes.
  - `[medium]` `[defer]` `[carried]` `[P11-ECH-05]` Same handler-computed `ServedAt`/`IsDegraded` emission concern as P11-BH-05 and DW-488; not deferred again.
- grouped survivors:
  - `[medium]` `[defer]` Default Tenants no-Keycloak topology lacks replacement local JWT configuration: P11-BH-03.
  - `[medium]` `[defer]` Browser focus behavior is mocked rather than executed: P11-VG-01 and P11-VG-02, across separate Admin and Tenants UI surfaces.
  - `[medium]` `[defer]` Multi-registration handler catalogs overwrite sibling registrations: P11-ECH-03.
  - `[medium]` `[defer]` Carried CI, provenance-emission, UI, routing, marker, authorization, and admission findings retain their existing deferred-work routes and were not appended again.
- loopback: none; no intent gap, bad spec, or Story 4.7 patch survived triage.

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

Review pass 9 (2026-09-07, `bmad-code-review`). Group 1 Tenants Code Map `d2b7ede3..e7f36662` (10 files, +777/−147). Blind Hunter, Edge Case Hunter, and Acceptance Auditor completed. Verification Gap Reviewer returned empty results.

**Patch**

- [x] [Review][Patch] RESOLVED (Tenants `54fc4040dc6348e5560fffc246a27e72dc6558fe`): balanced-quote `NormalizeETag` now rejects quote-only values after unwrapping; direct factory and handler matrices passed 55/55 in both Debug/source and Release/package modes. [src/Hexalith.Tenants/Queries/TenantQueryResult.cs:52-56]

**Deferred**

- [x] [Review][Defer] Generated-controller and topology proofs do not plant or pin `ServedAt`/`IsDegraded`; the emitter still emits those headers without a provenance gate [tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs:117] — deferred: DW-488 (already recorded; not written again)
- [x] [Review][Defer] Six-argument `FromPayload` still discards freshness inputs while host options stay operator-configurable [src/Hexalith.Tenants/Queries/TenantQueryResult.cs:37-44] — deferred: DW-493 (already recorded; not written again)

**Rejected**

- `false` — "Matching persisted validator / Redis HASH version not proved": AC3 specifies a conflicting validator and HTTP 200.
- `false` — "EventStore `IsNotModified: null` vs typed-client `false` is an undocumented split": EventStore strips the producer validator; the client records the HTTP 304 flag (P8-BH-04).
- `false` — "Header suppression covers only GetTenant": AC3 is one persisted tenant; six-route coverage is the factory/handler matrices (P8-BH-02).
- `false` — "Redis helper hardcodes localhost, skips poorly, and assumes `tenants||`": it binds `DaprDiagnostics.DefaultRedisPort` and the checked-in keyPrefix; Redis unavailability must fail the persistence proof.
- `false` — "`SharedClientRelayHandler` drops Content/Options and does not dispose the clone": GET-only typed `GetTenantAsync` has no body; isolation is the cloned Authorization header (P7-BH-12).
- `false` — "`tenants-api` on the shared fixture stalls other tests": `WaitForAliveness: false` already keeps `/alive` off fixture startup.
- `false` — "Nested quotes and six-route degenerate ETags are uncovered": the while-loop already unwraps nested quotes; both factory overloads pin degenerate omission (P2-BH-09).
- `false` — "ETag tests plant no `ProjectionVersion`": the freshness matrix stamps `tenant-sequence:42` on every primary row.
- `false` — "Null `Members`/`Configuration` after deserialize throws NRE": this proof creates a new tenant whose persisted collections are initialized.
- `low` — leftover `CommandStatus` alias after Redis type aliases removed the clash.
- `low` — `ConnectAsync` ignores cancellation until `ConnectTimeout` (5 s).
- `low` — `RedisTimeoutException` is not wrapped as `TimeoutException`; failing the persistence proof is correct.
- `low` — bootstrap `PublishFailed` is not skipped; the already-bootstrapped path dominates and create already skips.
Review pass 10 (2026-09-08, `bmad-code-review`). Chunked Code Map + EventStore spec/gitlink; four layers ran; none failed.

**Patch**

- [x] [Review][Patch] Quote-only ETags remain as producer validator metadata after balanced unwrap [references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs:46-56] — applied 2026-09-08: leftover quote-only tokens omit metadata; factory/handler theories gained `"` and `"""`; `TenantQueryResultTests` + `TenantQueryFreshnessTests` 49/49, 0 skipped.

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


## EventStore project context

---
project_name: 'Hexalith.EventStore'
user_name: 'Administrator'
date: '2026-06-29'
sections_completed: ['technology_stack', 'language_rules', 'framework_rules', 'identity_rules', 'testing_rules', 'code_quality', 'workflow_rules', 'critical_rules']
status: 'complete'
rule_count: 54
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- **.NET 10** — SDK pinned `10.0.400` (`rollForward: latestPatch`) in `global.json`; all projects target `net10.0`, `Nullable`+`ImplicitUsings` enabled, **`TreatWarningsAsErrors=true`**
- **DAPR SDK 1.18.5** — `Dapr.Client`, `Dapr.AspNetCore`, `Dapr.Actors(.AspNetCore)` (state store, pub/sub, actors)
- **.NET Aspire 13.5.3** — Hosting, Redis, Docker, Azure AppContainers, K8s, Testing; Keycloak/K8s are **preview** builds; DAPR orchestration via `CommunityToolkit.Aspire.Hosting.Dapr` (preview)
- **MediatR 14.2.0** (CQRS), **FluentValidation 12.1.1**, JWT Bearer auth, OpenAPI/Swashbuckle
- **OpenTelemetry 1.18.0**, `Microsoft.Extensions.*` 10.0.11, **SignalR** 10.0.11 (+ StackExchange.Redis backplane)
- **Blazor FluentUI `5.0.0-rc.5-26219.1`** + matching Icons package (admin/sample UIs)
- **Testing:** xUnit **v3** (`xunit.v3` 4.0.0) on Microsoft.Testing.Platform, Shouldly 4.3.0, NSubstitute 6.2.0, bunit 2.9.0, Testcontainers 4.14.0, Playwright 1.62.0, Microsoft.Testing.Extensions.CodeCoverage 18.10.0, and coverlet 10.0.1; NBomber 6.6.0 (load)
- **Identity helper:** `Hexalith.Commons.UniqueIds` 2.30.0 (ULID generation)
- **All source-owned package versions centralized** in `references/Hexalith.Builds/Props/Directory.Packages.props`; the root `Directory.Packages.props` is an import-only wrapper — `ManagePackageVersionsCentrally=true`

## Critical Implementation Rules

### C# Language-Specific Rules

- **Contracts are `sealed record` with primary constructors** + XML `<param>` docs per parameter (see `Contracts/Replay/ReplayEventEnvelope.cs`)
- **`ConfigureAwait(false)` on every awaited call** — CA2007 is enforced (190+ sites). Missing it breaks the build in stricter projects (it's the known cause of the `Server.Tests` build failure)
- **NO file copyright headers** — only 3 of 721 files have one. Do **not** add MIT/ITANEO headers (this differs from sibling `Hexalith.Commons`)
- **XML docs are NOT generated by default** — `GenerateDocumentationFile` is true *only* when `ApiReferenceBuild=true` on packable projects. Don't assume CS1591 enforcement like other Hexalith repos
- File-scoped namespaces, Allman braces, `using` outside namespace, System directives sorted first
- `_camelCase` private fields, `I`-prefixed interfaces, `Async` suffix on async methods (enforced as `warning` naming rules)
- Nullable enabled — validate at boundaries with `ArgumentNullException.ThrowIfNull` / `ThrowIfNullOrWhiteSpace`

### Framework-Specific Rules (DAPR / Aspire / MediatR)

- **MediatR pipeline order is fixed and intentional: `Authorization → Logging → Validation`** (registered in `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`). Authorization runs first so denied requests never hit the logging "pipeline entry" line. Do not reorder `AddOpenBehavior` calls
- **Event-sourcing aggregates are pure functions:** `Handle(Command, State?) → DomainResult` + `Apply(Event) → State`. No direct state mutation; state is rebuilt by replaying events. Handle/Apply are discovered by reflection convention — no manual registration
- **DAPR abstracts state/pubsub/config** via sidecars; access-control in `DaprComponents/accesscontrol.yaml` is **deny-by-default** (slim-mode without mTLS yields 403 on service-to-service calls)
- **Outbound DAPR routing headers are handler-owned and REPLACED, never appended** — set `dapr-app-id` / `dapr-api-token` via the platform handler in `Hexalith.EventStore.Client`, which does `Headers.Remove(...)` then `TryAddWithoutValidation(...)` so a caller/inbound-forwarded value can't duplicate or hijack sidecar routing; the handler is innermost in the chain. **The handler is opt-in, not automatic:** `AddEventStoreGatewayClient` registers only the typed client and `ICommandStatusLocationBuilder` — routing-header ownership comes from the separate `AddEventStoreDaprServiceInvocation(appId, apiToken)` call chained onto the returned `IHttpClientBuilder`, registered **last** so it stays innermost. Omitting it is fail-open (no compile error, no startup validation, no runtime diagnostic), so every sidecar-routed host must call it explicitly. **Never hand-roll a per-host `DaprAppIdHandler`** or use a bare `TryAddWithoutValidation` for these headers (AD-18; enforced by a guardrail test)
- **Multi-tenancy is at the contract level:** identity = Domain + AggregateId + TenantId
- **AppHost changes require restarting `aspire run`** — the app model is built at startup
- **Domain-owned contracts library exception:** a domain-service host references `Hexalith.EventStore.DomainService` for platform hosting, but the domain may also own a contracts-only library when command/query contract identities must be shared with a dedicated generated API host and UI metadata consumers. That library must contain contracts only — no hosting, DAPR, telemetry, state-store, query/projection actor, or UI code
- **Domain modules are domain-centric:** they must not ship their own `*.AppHost`, `*.Aspire`, or `*.ServiceDefaults` projects and must not re-implement projection/query actors, DAPR state-store wrappers, cursor codecs, telemetry sources/meters, health checks, or canonical SDK endpoint mapping. Persist named projections through scoped `IAsyncDomainProjectionHandler` implementations using `IReadModelStore`/`IReadModelBatchStore` and the stable dispatch ID; retain `IDomainProjectionHandler` only for synchronous full-replay compatibility. Use DomainService plus `IQueryCursorCodec`, `EventStoreDomainDiagnostics`, and the EventStore AppHost/Aspire extensions.
- `Hexalith.Tenants` source path is resolved by root `Directory.Build.props`; EventStore's own submodule path is `references/Hexalith.Tenants` — don't hardcode old root-level paths
- **Cross-repo Hexalith libraries use package mode by default in every configuration** — use `-p:UseHexalithProjectReferences=true` only for an intentional source session; source is selected only when the root-declared submodule project exists, otherwise evaluation falls back to `PackageReference`. Package publication must remain in package mode. Versions are owned by `references/Hexalith.Builds/Props/Directory.Packages.props`
- **Rerun restore after switching dependency modes** — `--no-restore` can reuse stale project-reference assets from a previous Debug/source restore

### Identity Rules (ULID, not GUID)

- **System identifiers are ULIDs.** Generate with `UniqueIdHelper.GenerateSortableUniqueStringId()`
- **`Guid.TryParse` on `messageId`/`correlationId`/`aggregateId`/`causationId` is FORBIDDEN** — use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). ULID and GUID share a 36-char shape only by coincidence (Epic 2 retro R2-A7; enforced by `ProtectedIdentifierGuidParserAuditTests`)

### Testing Rules

- **xUnit v3** + **Shouldly** assertions (`ShouldBe`, `ShouldThrow`) — never raw `Assert.*`. NSubstitute for mocks
- **Run test projects individually** — never solution-level `dotnet test`. Use `.slnx` for restore/build only
- **Test tiers:** Tier 1 (Contracts, Client, Sample, Testing, SignalR — run in CI), Tier 2 (Server.Tests), Tier 3 (IntegrationTests — needs Docker + Aspire)
- **`Hexalith.EventStore.Server.Tests` has a known build failure** (CA2007-as-error) — excluded from baseline; don't treat its red as a regression you caused
- **Integration tests MUST assert state-store end-state** (Redis key contents, persisted CloudEvent body) — not just 202/mock call counts. "Returned 202" is a smoke test, not an integration test (Epic 2 retro R2-A6)
- All configured tests must pass before a story is complete

### Code Quality & Style Rules

- **`.slnx` only** — never create or use `.sln` files
- `.editorconfig` sets `CA1062`/`CA1822`/`CA2007` to **warning** at solution level, but `TreatWarningsAsErrors=true` promotes them to build-breakers; `CA1014` is disabled
- Containers via **.NET SDK container support — no Dockerfiles.** Opt in per project with `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>`; defaults (alpine base, `registry.hexalith.com`, non-root `app`, port 8080, OCI labels) live in `Directory.Build.targets`
- **Submodules: root-declared only under `references/`** (`references/Hexalith.Tenants`, `references/Hexalith.AI.Tools`, `references/Hexalith.Commons`, etc.). Never recurse into / initialize nested submodules; deinit if accidentally pulled
- **Never modify submodule files without explicit approval** — they're shared across Hexalith repos

### Development Workflow Rules

- **Conventional Commits required** (semantic-release drives versioning): `feat` → minor, `fix`/`perf` → patch, `feat!`/`BREAKING CHANGE:` → major; `docs`/`refactor`/`test`/`build`/`ci` → no bump. Never use `chore`; choose the specific non-release type. Don't use `feat` for refactors (false minor bump + NuGet publish)
- **Branches:** `feat/…`, `fix/…`, `docs/…`. No direct commits to `main`
- **Senior code review is a mandatory pipeline stage** — budget for review-found rework (Epic 2: 5/5 stories patched). Verify CRITICAL findings before accepting (false-positive CRITICALs are expensive — R1-A8 verification-command rule)
- Release on merge to main: test → pack → publish **14 packages** from `tools/release-packages.json`: `Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, `Hexalith.EventStore.Server`, `Hexalith.EventStore.SignalR`, `Hexalith.EventStore.Testing`, `Hexalith.EventStore.Testing.Integration`, `Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults`, `Hexalith.EventStore.DomainService`, `Hexalith.EventStore.RestApi.Generators`, `Hexalith.EventStore.Gateway`, `Hexalith.EventStore.Admin.Abstractions`, `Hexalith.EventStore.Admin.Cli`, and `Hexalith.EventStore.Admin.Server`; validation rejects missing or extra `.nupkg` files outside that manifest.
- **Local run (VM/slim mode):** start `placement` + `scheduler` before `aspire run`, else actors fail with "did not find address for actor". Use `http://localhost:8080` (dev HTTPS cert not fully trusted); `EnableKeycloak=false` falls back to symmetric-key JWT

### Critical Don't-Miss Rules

- **Never** add package versions to `.csproj` or the root import wrapper — source-owned versions belong in `references/Hexalith.Builds/Props/Directory.Packages.props`
- **Never** use `.sln`; **never** run solution-level `dotnet test`
- **Never** `Guid.TryParse` an id field — ULIDs only
- **Never** reorder the MediatR behavior pipeline (Auth → Log → Validate)
- **Never** add copyright headers (this repo doesn't use them)
- **Never** recurse into nested submodules or modify submodule files unsolicited
- **Never** publish packages with `UseHexalithProjectReferences=true`
- **Always** `ConfigureAwait(false)` on awaits
- **Always** assert persisted state in Tier 2/3 tests, not just status codes
- **Always** keep aggregates pure — events in, state rebuilt, no in-place mutation

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing any code
- Follow ALL rules exactly as documented
- When in doubt, prefer the more restrictive option
- This file complements (does not replace) `CLAUDE.md` at the repo root

**For Humans:**

- Keep this file lean and focused on agent needs
- Update when the technology stack or analyzer policy changes
- Remove rules that become obvious over time

Last Updated: 2026-06-02


## Tenants project context

---
project_name: 'Hexalith.Tenants'
user_name: 'Administrator'
date: '2026-08-21'
sections_completed:
  [
    'technology_stack',
    'language_rules',
    'domain_rules',
    'eventing_rules',
    'framework_rules',
    'identity_rules',
    'ui_rules',
    'testing_rules',
    'code_quality',
    'workflow_rules',
    'critical_rules',
  ]
status: 'complete'
rule_count: 119
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- **.NET 10 / C#** — SDK pinned to `10.0.400` with `rollForward: latestPatch`; all owned projects target `net10.0`; `Nullable`, `ImplicitUsings`, `LangVersion=latest`, and `TreatWarningsAsErrors=true` are root defaults.
- **Solution/build** — `Hexalith.Tenants.slnx` only; `MSBuild.rsp` and `Directory.Solution.*` force single-node serialized builds (`-m:1`, `BuildInParallel=false`, `RestoreBuildInParallel=false`).
- **Hexalith platform dependencies** — `Hexalith.EventStore` packages pinned to `3.100.0`; `Hexalith.Memories` packages pinned to `2.21.3`. Debug uses source `ProjectReference` when available; Release uses NuGet packages for package-capable libraries.
- **DAPR** — DAPR SDK packages `1.18.5`; CI installs DAPR CLI/runtime `1.18.0` (the shared `domain-ci` default). The `1.19.0-preview.2` SDK family is intentionally held because stable pins do not move to prerelease channels in a dependency refresh.
- **Aspire** — Aspire packages `13.5.3`; Keycloak/Kubernetes packages use `13.5.3-preview.1.26425.3`; DAPR hosting uses `CommunityToolkit.Aspire.Hosting.Dapr` `13.5.0-preview.1.260825-0345`.
- **Backend stack** — MediatR `14.2.0`, FluentValidation `12.1.1`, JWT/OpenID Connect IdentityModel `8.22.0`, OpenAPI `10.0.11`, Swagger UI `10.2.3`, and OpenTelemetry `1.17.0` including Runtime instrumentation.
- **UI stack** — Blazor InteractiveServer, FrontComposer Shell/Contracts source references, Fluent UI Blazor V5 `5.0.0-rc.5-26219.1`, bUnit `2.9.0`.
- **Memories search** — Tenants UI uses `MemoriesClient.SearchAsync` as an index lookup only; rows are hydrated from Tenants REST query endpoints.
- **Testing** — xUnit v3/runner `4.0.0` on Microsoft.Testing.Platform, Shouldly `4.3.0`, NSubstitute `6.2.0`, Testcontainers `4.14.0`, Microsoft.Testing.Extensions.CodeCoverage `18.10.0`, Microsoft.NET.Test.Sdk `18.9.0`, and YamlDotNet `18.1.0`. Shouldly `5.0.0-preview.2` is held because stable pins do not move to prerelease channels in a dependency refresh.
- **Release tooling** — semantic-release `25.0.9`, commitlint `21.2.2`, `@semantic-release/changelog` `7.0.0`, and `@semantic-release/git` `11.0.1`; five NuGet packages are released: `Hexalith.Tenants.Contracts`, `.Client`, `.Server`, `.Testing`, `.Aspire`.
- **Framework family held at .NET 10.** `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `System.Text.Json`, and `System.Collections.Immutable` stay on `10.0.x` stable. Their higher versions are .NET 11 prereleases, and several publish only `net11.0` assets that cannot restore against `net10.0` / SDK `10.0.400`. Do not update this family without an approved platform migration.

## Critical Implementation Rules

### C# Language & Contract Rules

- Commands, success events, and rejection events are plain `public record` primary-constructor types. Do not add `sealed` or XML parameter docs to these contract records.
- Query contracts are `sealed class : IQueryContract` with static `QueryType`, `Domain`, and `ProjectionType`; `QueryType`/`Domain` are kebab-case and unique. There are currently 6 query contracts.
- Query response DTOs are `sealed record` types with XML `<summary>` docs; keep response DTOs separate from query contract marker classes.
- Events implement `IEventPayload`; rejection events implement `IRejectionEvent`; commands have no marker interface.
- Rejection records must stay structured and support-safe: no prose `Message`/`Reason`/`Detail`, no stack trace, no raw payload, no token fields.
- Every event type has a string `TenantId`; tenant/global administrator domain identifiers are meaningful caller-supplied strings, not GUIDs or ULIDs.
- Enums use `Unknown = 0` plus JSON string serialization. `TenantStatus` uses the custom converter that maps unrecognized values to `Unknown`; `TenantRole.Unknown` is non-privileged and rejected by domain logic.
- Use file-scoped namespaces, namespace = folder path, `using` outside namespace, System directives first, Allman braces, `_camelCase` private fields, `I` interfaces, and `Async` suffix on async methods.
- Always use `ConfigureAwait(false)` on awaited calls in production code. `CA2007` is warning-level in `.editorconfig`, but warnings are build failures.
- Keep each `.cs` file focused on one C# type/object. Move extra records/classes/enums/interfaces/delegates to their own files named for the type.
- Validate public boundaries with `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace`; avoid nullable suppression as a substitute for validation.
- Do not add copyright/license headers to new files in this repo.

### Domain, Eventing & Framework Rules

- Aggregates are pure domain functions: static `Handle(command, state?, envelope) -> DomainResult` and state `Apply(event)`. No I/O, no async, no mutation inside `Handle`; state changes only through `Apply`.
- Business failures are rejection events, not exceptions. Same-state domain requests return `DomainResult.NoOp()` only where explicitly modeled, such as same-role change or identical configuration set.
- The two aggregate domains are `tenants` and `global-administrators`. `TenantAggregate` uses `[EventStoreDomain("tenants")]`; global administrator events publish on the shared `tenants.events` topic via AppHost gateway topic override.
- Never edit/delete/rewrite events, projections, or state-store data to fix business state. Use compensating commands through `POST /api/v1/commands`, then verify command status and projection evidence.
- The Tenants host is an EventStore domain service, not an EventStore server. Do not call `AddEventStoreServer`, host `AggregateActor`, or reintroduce `TenantsProjectionActor`.
- Host composition consumes shared platform services: `AddServiceDefaults`, `AddEventStoreDomainTelemetry("tenants")`, `AddEventStoreDataProtection`, `AddEventStoreReadModelStore`, `AddEventStoreQueryCursorCodec`, and `MapEventStoreDomainService`.
- Tenant reads are served by in-process `IDomainQueryHandler`s and the REST query controller (`GET /api/tenants*`, `GET /api/users/{id}/tenants`, `GET /api/global-administrators`). Do not route tenant reads through projection actors or the generic EventStore query gateway.
- Query cursors are opaque and DataProtection-backed; cursor scopes include authenticated user and query context. Do not parse, expose, or log cursor contents.
- Read-model freshness uses EventStore `IReadModelFreshness` and `ReadModelFreshnessState`. Server read models persist `ProjectedAt`; `ToQueryResponseMetadata` emits `current/stale/unknown` via `X-Hexalith-Is-Stale`. `Aging` is dormant on the wire; `Refreshing` is UI-only.
- `ProjectedAt` measures last projection write, not global lag. Defaults are intentionally conservative; `ServedAt` must not be used as projection age.
- DAPR pub/sub is at-least-once and unordered. Consumers and projection handlers must be idempotent; use EventStore `MessageId` for duplicate detection and never treat `SequenceNumber` as global ordering.
- The Client package owns domain-specific event handler registration and an in-memory projection default only. Consuming services supply durable projection storage and shared dedup when scaling beyond one instance.
- Tenant configuration keys are consumer-owned namespaced strings, usually dot-prefixed (`billing.*`, `sample.*`). Consumers filter by their namespace and ignore others.
- MediatR pipeline order in Tenants is `ValidationBehavior` then `AuthorizationBehavior`; do not reorder or add generic behaviors casually.
- Domain exceptions map to RFC 7807 through registered exception handlers, specific handlers before generic.
- AppHost is the allowed repository-specific technical component. It wires EventStore, Tenants, Tenants UI, Sample, Memories, DAPR components, Keycloak, topic overrides, and Debug child build edges.
- AppHost changes require restarting `aspire run`; the Aspire app model is built at startup.
- DAPR access control is deny-by-default. Any sidecar/app-id/topic change must update DAPR access-control YAML and route tests.

### Identity Rules

- Identity = **`AggregateIdentity(TenantId, Domain, AggregateId)`** — build via the `TenantIdentity` factory: `ForTenant(id)`, `ForGlobalAdministrators()`. Constants: `DefaultTenantId = "system"`, `Domain = "tenants"`
- **Tenant ids and user ids are meaningful caller-supplied strings — NOT ULIDs.** (EventStore envelope ids like `MessageId` may be ULIDs; domain identifiers are not.) Do not `Guid.TryParse`/`Ulid.TryParse` a `TenantId`/`UserId`

### UI / UX Rules

- Tenants UI must use FrontComposer and Fluent UI Blazor V5 components. Do not introduce raw interactive HTML controls (`button`, `input`, `select`, `textarea`), raw forms, or raw table markup in `.razor` components.
- Route pages compose through FrontComposer page primitives (`FcPageHeader`, `FcPageLayout`, `FcAggregateListPage`, `FcAggregateDetailPage`). Do not add Tenants-owned page-root `<main>` wrappers, direct `PageTitle`, or raw route-level `<h1>`.
- Multi-region domain pages and panels group sibling titled content sections with `FluentAccordion`, `AccordionExpandMode.Multi`, and an initially expanded primary item. Do not hide the only primary content region behind an accordion.
- Use `FluentDataGrid` or FrontComposer grid primitives for data surfaces. Tenants-specific grids are allowed where FrontComposer does not provide cursor pagination, safety-column pinning, or required non-collapsing states.
- Express layout with Fluent/FrontComposer primitives such as `FluentStack`, `FluentGrid`, and FrontComposer layout modes. Inline layout styles are forbidden.
- Component CSS must not own theme primitives, semantic colors, broad layout/spacing/typography, or native control selectors. Any unavoidable layout/typography CSS requires an immediately preceding `/* fc-css-exception: ... */` marker with a real reason.
- Use Fluent V5 component parameters and Fluent 2 tokens only. Do not use legacy Fluent v4/FAST tokens such as `--type-ramp-*`, `--neutral-*`, `--accent-*`, or `--palette-*`.
- Keep raw semantic HTML only where Fluent has no equivalent (`section`, `header`, `nav`, `dl`, `ul`, `ol`, `li`, inline `a`). Structural raw `div`/`span` usage is budgeted by governance tests and should ratchet down.
- Every UI state must remain support-safe: never render bearer tokens, decoded JWT payloads, internal correlation IDs, stack traces, raw EventStore metadata, raw payloads, or cursor/ETag internals.
- Command UI must preserve the non-collapse truth-state model: accepted, projection-confirmed, and audit-available are distinct. SignalR is only a freshness nudge, never proof of success.
- Command flows must confirm from projection evidence before showing success. Terminal non-success states cannot become confirmed merely because unrelated projection data appears.
- `ReadModelFreshnessState.Unknown` and `Stale` generally fail closed for mutation actions; first-tenant create is a documented exception where unknown list freshness remains creatable.
- Memories search is an index match-set only. Search results must be hydrated through Tenants detail/list query paths before display; stale Memories data must not become row truth.
- UI localization uses `TenantsResources.resx` and `.fr.resx`; keep EN/FR key parity and use whole strings with placeholders, not runtime sentence fragments.
- Stable `data-testid` selectors and accessible names are part of the contract. Tests must not depend on row text, color alone, or incidental Fluent-generated markup.

### Testing Rules

- Use xUnit v3 and Shouldly (`ShouldBe`, `ShouldThrow`, `Should.ThrowAsync`); do not use raw `Assert.*` in new tests. A few old scaffolding smoke tests still exist and should not be copied.
- Use NSubstitute for mocks and bUnit for Blazor/Fluent components. Fluent component tests should derive from the local Fluent bUnit setup and use loose JS interop when rendering Fluent UI.
- Test classes/files use plural `{Class}Tests.cs`; behavior names should stay descriptive and scenario-focused.
- Run tests per project, matching CI. Use `.slnx` for restore/build only; do not make solution-level `dotnet test` the default.
- CI Tier 1 blocking tests: `Contracts.Tests`, `Client.Tests`, `Testing.Tests`, `UI.Tests`, and `Sample.Tests`.
- CI Tier 2 blocking tests: `Server.Tests` after `dapr init`.
- CI Tier 3 Aspire tests: `IntegrationTests` with `Category!=Performance`, non-blocking `continue-on-error`; `Category=Performance` runs only on nightly schedule.
- Integration tests must assert persisted state-store/read-model end state, headers, projection metadata, or topology behavior. HTTP status alone is only a smoke signal.
- Coverage gate uses unioned Cobertura reports. Overall line coverage is `>80%` scoped to four package projects: `Contracts`, `Client`, `Server`, `Testing`. The published `.Aspire` helper is not in the current line-coverage scope.
- Branch coverage gate is `100%` for isolation/auth files: `TenantAggregate.cs`, `GlobalAdministratorsAggregate.cs`, and `ChangeUserRoleValidator.cs`. Add new isolation/auth logic to the gate.
- Domain logic tests should prefer `Hexalith.Tenants.Testing`: `InMemoryTenantService`, `TenantTestHelpers`, `TenantIsolationTestHelpers`, and `InMemoryTenantProjection`.
- UI command tests must cover validation before submit, fail-closed availability, projection-confirmed success, non-collapse lifecycle states, SignalR nudge-only behavior, support-safe copy, EN/FR resource parity, and focus/live-region behavior where relevant.
- Query/freshness tests must cover `X-Hexalith-Is-Stale`, `X-Hexalith-Served-At`, `X-Hexalith-Projection-Version`, `304` with freshness headers, unknown freshness, stale freshness, and conservative threshold behavior.
- Run each test project with the repository-selected Microsoft.Testing.Platform path; direct-assembly fallback is not acceptance evidence for a maintained `dotnet test` lane.
- All configured tests relevant to a story must pass before completion; document any blocked validation with the exact blocker.

### Code Quality & Style Rules

- Use `Hexalith.Tenants.slnx` only. Do not create `.sln` files, and do not run solution-level `dotnet test` as the default validation path.
- Keep package versions centralized in `Directory.Packages.props`; `.csproj` files should use `<PackageReference Include="..." />` without `Version`.
- Respect intentional serialized builds from `MSBuild.rsp` and `Directory.Solution.*` (`-m:1`, `BuildInParallel=false`, `RestoreBuildInParallel=false`). Do not "fix" this into parallel restore/build.
- Root defaults make warnings build-breaking: `TreatWarningsAsErrors=true`, CI `-warnaserror`, nullable enabled, implicit usings enabled, latest language version. Fix analyzer findings instead of suppressing them casually.
- Analyzer policy is intentionally lightweight. Do not add SonarAnalyzer, StyleCop, Roslynator, or formatting packages unless explicitly requested.
- Containers use .NET SDK container support, not Dockerfiles. Only `src/Hexalith.Tenants` is the container app (`EnableContainer=true`, `ContainerRepository=tenants`); libraries ship as NuGet packages.
- Published package surface is exactly five packages: `Contracts`, `Client`, `Server`, `Testing`, and `Aspire`. `Hexalith.Tenants` and `Hexalith.Tenants.UI` are container/application projects, not NuGet packages.
- Debug builds may use source `ProjectReference`s for available Hexalith libraries; Release builds should consume package-capable shared libraries through NuGet package references.
- Source-only shared references are intentional where packages are not yet available, such as EventStore web host and FrontComposer Contracts/Shell. Do not convert them blindly.
- The host's `ErrorOnDuplicatePublishOutputFiles=false` is intentional while it references the EventStore web host; Tenants appsettings should win.
- Keep Tenants domain-focused. Do not add reusable hosting, serialization, persistence, UI scaffolding, test harness, or cross-domain boilerplate here when it belongs in `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Commons`, `Hexalith.Builds`, or another shared module.
- Submodules are root-declared under `references/` only. Never use recursive submodule init, and do not modify submodule files without explicit approval.
- Use MSBuild root properties such as `$(HexalithEventStoreRoot)`, `$(HexalithFrontComposerRoot)`, and `$(HexalithMemoriesRoot)`; do not hardcode local absolute paths.
- Only call non-experimental Memories APIs from Tenants. `SearchAsync` is safe; experimental ingestion/tenant creation APIs should not be used from this repo.
- Do not add copyright/license headers to new files.

### Development Workflow Rules

- Use Conventional Commits. `feat` triggers a minor release and `fix` triggers a patch release. A major release requires the space-spelled `BREAKING CHANGE:` footer, and nothing else works — measured against the pinned `conventional-changelog-angular` parser: `feat!:` and `fix!:` → no release at all (no `breakingHeaderPattern`, so the `!` header fails `headerPattern` and the commit is unclassified); `BREAKING-CHANGE:` → patch, not major (the preset overrides `noteKeywords` to the space-spelled form only). Do not use `feat` for refactors or test-only work.
- Semantic Release derives the next version from the highest release tag reachable from `main`, while nuget.org keeps every version ever published. If release tags are deleted, the two drift apart and every proposal collides; the `verify-source` job proves the tag floor still covers the registry before the release job runs.
- Use branch names like `feat/...`, `fix/...`, or `docs/...`; do not commit directly to `main`.
- CI runs on push/PR to `main`: restore, Release build with warnings as errors, package metadata/consumer validation, Tier 1 tests, DAPR init, Tier 2 tests, and coverage gates.
- Release is gated on CI: the Release workflow triggers via `workflow_run` only after a successful push-event CI run on `main` (it does not re-run the test tiers), then semantic-release derives the version from commit history, packs exactly five NuGet packages, validates packages and package-only consumers, publishes to NuGet, publishes the tenants container, creates the GitHub Release, and updates `CHANGELOG.md`.
- Run local tests by project in the same shape as CI. Use `.slnx` for restore/build, then targeted `dotnet test <test-project>`.
- Initialize only the required root-declared submodules under `references/`; never use recursive submodule initialization.
- A story commit must not move a `references/` gitlink silently. Before completing a story run `python3 scripts/validate-story-gitlinks.py <story-file>`; declare each moved pointer as a File List entry with a reason, or revert it and commit the bump separately as `build(deps)`. Never state that `references/` was untouched without that check passing.
- For local distributed runs, use Aspire AppHost. Restart `aspire run` after AppHost, DAPR component, topic, or sidecar changes because the app model is built at startup.
- In slim/VM local mode, start DAPR `placement` and `scheduler` before `aspire run`; actor-backed dependencies can fail without them.
- The default local Tenants service URL is `http://localhost:8080`; when Keycloak is disabled, local auth falls back to symmetric-key JWT.
- AppHost topology changes must update DAPR access-control YAML, route/topic wiring, and tests together.
- Do not edit EventStore, projection, or state-store data during debugging. Reproduce with commands, inspect persisted envelopes/read models, and fix state through compensating commands.
- Before implementing UI work, read the Hexalith UX instructions and verify FrontComposer/Fluent conformance tests still represent the intended governance.
- Before changing persistence, projection freshness, or query behavior, read the relevant EventStore state/freshness conventions and preserve cursor opacity.
- Package or source-reference changes must preserve Release package-only consumer validation; Debug convenience references cannot leak into Release package consumers.
- Document any validation that cannot be run with the exact command and blocker, not a generic "not tested" note.

### Critical Don't-Miss Rules

- Never add `sealed` or XML parameter docs to command, success-event, or rejection-event records. Query response DTOs are the exception: use `sealed record` with XML summaries.
- Never throw exceptions for business failures inside aggregates. Return structured rejection events, or `DomainResult.NoOp()` only for explicitly modeled same-state requests.
- Never add I/O, async calls, service dependencies, or mutation to aggregate `Handle` methods. State changes come only from events applied through `Apply`.
- Never register `AddEventStoreServer`, `AggregateActor`, or a tenant projection actor in the Tenants host. Tenants is a domain service with in-process query handlers and REST read endpoints.
- Never reorder the MediatR pipeline. Validation runs before authorization.
- Never treat `TenantId` or `UserId` as GUIDs/ULIDs, and never treat `SequenceNumber` as global ordering. Domain identifiers are caller-supplied strings; deduplicate on EventStore `MessageId`.
- Never parse, expose, or log query cursors, ETags, JWT payloads, bearer tokens, raw EventStore metadata, raw event payload dumps, stack traces, or internal correlation data.
- Never edit/delete/rewrite events, projections, or state-store data to fix business state. Use compensating commands and verify projection evidence.
- Never use raw interactive HTML controls, raw forms, raw tables, route-level `PageTitle`, or page-root `<main>` wrappers in Tenants UI. Use FrontComposer and Fluent UI Blazor V5.
- Never treat SignalR notifications, HTTP 202/201, or command acceptance as proof of user-visible success. UI success requires projection-confirmed evidence.
- Never let stale Memories search data become tenant row truth. Search returns match candidates; hydrate rows through Tenants query endpoints.
- Never use `.sln`, solution-level `dotnet test`, package versions in `.csproj`, Dockerfiles for the Tenants container, or recursive submodule initialization.
- Never add cross-domain technical plumbing to Tenants when it belongs in shared Hexalith modules.
- Always keep DAPR consumers idempotent and topology/access-control YAML aligned with AppHost sidecars, app IDs, and topics.
- Always preserve Release package-only consumer validation when changing references, packaging, or shared dependency flow.
- Always run or document the relevant per-project tests before completing implementation work.

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing any code in `Hexalith.Tenants`
- Follow ALL rules exactly as documented
- When in doubt, prefer the more restrictive option
- This file complements (does not replace) `CLAUDE.md` at the repo root and the domain docs under `docs/` (see `event-contract-reference.md`, `idempotent-event-processing.md`, `compensating-commands.md`, `cross-aggregate-timing.md`, `production-auth-claim-contract.md`)

**For Humans:**

- Keep this file lean and focused on agent needs
- Update when the technology stack, analyzer policy, MediatR pipeline, test tiers, or coverage gates change
- Remove rules that become obvious over time

Last Updated: 2026-08-21


## Epic 4 context

# Epic 4 Context: Operators Can Trust Command and Event Integrity

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make command processing and persisted event behavior trustworthy under retries, concurrency, replay, expiry, crashes, and partial failure. Operators, domain authors, and reliability engineers must be able to rely on stable event identity, durable idempotency admission, deterministic dispatch, recoverable publication, and ordering semantics that are explicitly specified and proven before production behavior changes.

## Stories

- Story 4.1: Event Identity And Duplicate Result Fidelity
- Story 4.2: Resume And Idempotency Integrity
- Story 4.3: Deterministic Replay Dispatch And Serialization
- Story 4.4: Committed Event Publication Recovery
- Story 4.5: Append Durability Race Evidence
- Story 4.6: Global Position Sharding Spec Renegotiation
- Story 4.7: Tenants Query Provenance Follow-Up
- Story 4.8: Durable Admission Evidence Ledger
- Story 4.9: Trusted Admission Contract And Protected Identity
- Story 4.10: Digest Directory Rotation And Key Retirement
- Story 4.11: Admission State Machine And Current-Fence Enforcement
- Story 4.12: Expiry Compaction And Tombstone Retention
- Story 4.13: Legacy Admission Migration And Fail-Closed Reconciliation
- Story 4.14: OQ8 Multi-Host Production Evidence
- Story 4.15: OQ8 Platform Closure And Handoff

## Requirements & Constraints

- Persisted events have non-zero actor-allocated positions and gapless per-aggregate sequence numbers. Global allocation may contain reservation gaps and does not promise strict commit order. CloudEvent IDs use persisted event `MessageId`; duplicate command replies preserve the original result fields.
- Resume and idempotency use the exact tuple of `MessageId`, normalized `CausationId`, and `CommandType`; correlation remains tenant-scoped tracing metadata. Tenant authorization must precede state access, terminal results replay faithfully, transient pre-commit outcomes remain retryable, and ambiguous, corrupt, unavailable, consumed, or unsafe legacy state never becomes a fresh miss.
- Durable admission accepts only a trusted, versioned canonical-intent descriptor and fixed retention class. Callers provide only an opaque idempotency key and cannot select descriptor, digest, actor, fence, state, expiry, or policy authority. Raw keys, protected intent, results, and secrets must not leak into state identifiers, envelopes, diagnostics, telemetry, or evidence.
- Admission must prevent duplicate side effects across reservation, execution, recovery, expiry, compaction, restart, rotation, migration, and concurrent hosts. Conflicting live intent fails permanently; every expired-key reuse fails identically before protected work.
- Replay dispatch must resolve event types deterministically, detect ambiguity, preserve supported legacy names, and use one immutable serializer-options path across command, rehydrate, projection, and pub/sub readers.
- Committed-but-unpublished events must remain durably discoverable and recoverable without command resubmission. Republishing reuses the persisted event identity so at-least-once delivery remains deduplicatable.
- Append fencing is evidence-first: observe a real live-sidecar two-writer race and conflict behavior before selecting a provider-portable design. Global-position sharding is spec-first: no implementation, persisted-state, public-contract, migration, or topology change may begin until a content-bound successor to the frozen ordering specification is human-approved.
- High-risk verification must inspect persisted state, event bodies, checkpoints, topology, restart/failover behavior, and zero downstream work for non-execute outcomes. HTTP statuses and mock calls alone are insufficient.

## Technical Decisions

- The gateway remains the command/query policy boundary. After authentication, current authorization, and canonical validation, a dedicated tenant/key admission actor owns serialization, reservation, state transitions, and monotonic fencing. `AggregateActor` remains the sole durable event-mutation coordinator and accepts only a current internal fence.
- Exactly the current non-zero fence may cross an aggregate, domain-service, provider, repository, projection, audit, or scheduling side-effect boundary or finalize a terminal result. Safe recovery resumes the persisted identity and fence; uncertainty never creates new execution authority.
- Current global positions are non-zero, unique scalar values from the DAPR-backed allocator, but their gaps mean they are not a strict global commit sequence. A sharding successor must define shard ownership, uniqueness and monotonicity boundaries, representation, comparison rules, cursor/checkpoint behavior, mixed-history compatibility, rollout, rollback, and unsupported cross-shard comparisons.
- Event delivery is at-least-once and unordered. Consumers deduplicate by `MessageId`; sequence ordering is meaningful only within its documented domain boundary. Projection or notification signals do not by themselves prove user-visible success.
- Admission identity is partitioned by tenant and digest-key version with domain-separated HMAC-SHA-256 key digests and collision verification. Rotation and legacy migration preserve one canonical executable authority; expiry atomically replaces live/replay state with a fence-free minimal tombstone.
- Production-equivalent admission proof uses at least two independent EventStore hosts and DAPR sidecars sharing the `oq8-postgresql-v1` PostgreSQL actor-state profile with production resiliency. Same-process fixtures and direct actor calls are supporting evidence only.
- Message, correlation, causation, and aggregate identifiers remain ULID-safe where EventStore envelope semantics require sortable IDs; they must not be validated as GUIDs.

## UX & Interaction Patterns

Operator-facing command states must distinguish acceptance, recovery in progress, terminal success, and terminal failure using support-safe text rather than treating an accepted response as completion. Committed-but-unpublished work routes to recovery rather than encouraging resubmission. Shard-local and globally comparable positions must be labeled accurately. Projection lifecycle claims are authoritative only for projection-backed provenance; otherwise surfaces render `Unknown` and never infer state from an ETag or acceptance response. Opaque keys, canonical intent, digests, payloads, and protected results are never displayed.

## Cross-Story Dependencies

- Story 4.1 establishes stable identity. Story 4.2 adds exact message-keyed recovery state; both precede Story 4.4 publication recovery. Story 4.5 gates append-fencing decisions and gates Story 4.6 only if the selected sharding design changes append fencing or provider write semantics.
- Story 4.8 is a historical, non-executable ledger. Stories 4.9-4.15 form the ordered OQ8 authority and evidence chain; later work cannot retroactively authorize an earlier unsafe outcome, and platform completion does not grant release, deployment, consumer migration, or external-repository authority.
- Story 4.7 depends on completed EventStore route-provenance enforcement and generated-consumer handling, plus separately authenticated Tenants-maintainer authority and exact external-repository evidence. Existing EventStore provenance enforcement remains fail-safe while that follow-up is incomplete.


## Review content

diff --git a/src/Hexalith.Tenants/Queries/TenantQueryResult.cs b/src/Hexalith.Tenants/Queries/TenantQueryResult.cs
index 17006862..3d8b2513 100644
--- a/src/Hexalith.Tenants/Queries/TenantQueryResult.cs
+++ b/src/Hexalith.Tenants/Queries/TenantQueryResult.cs
@@ -25,8 +25,7 @@ internal sealed record TenantQueryResult : QueryResult {
             ? null
             : new QueryResponseMetadata(
                 ETag: normalizedETag,
-                IsNotModified: false,
-                ProjectionVersion: normalizedETag);
+                IsNotModified: false);
 
         return new TenantQueryResult(
             true,
@@ -41,31 +40,21 @@ internal sealed record TenantQueryResult : QueryResult {
         IReadModelFreshness? readModel,
         ReadModelFreshnessThresholds thresholds,
         DateTimeOffset now,
-        string? eTag) {
-        if (payload.ValueKind == JsonValueKind.Undefined) {
-            throw new ArgumentException("Payload element must not be Undefined.", nameof(payload));
-        }
-
-        string? normalizedETag = NormalizeETag(eTag);
-        QueryResponseMetadata metadata = readModel
-            .ToQueryResponseMetadata(thresholds, now, normalizedETag) with {
-                IsNotModified = false,
-                ProjectionVersion = readModel?.ProjectionVersion ?? normalizedETag,
-            };
-
-        return new TenantQueryResult(
-            true,
-            JsonSerializer.SerializeToUtf8Bytes(payload),
-            projectionType: projectionType,
-            metadata: metadata);
-    }
+        string? eTag)
+        => FromPayload(payload, projectionType, eTag);
 
     private static string? NormalizeETag(string? eTag) {
         if (string.IsNullOrWhiteSpace(eTag)) {
             return null;
         }
 
-        string normalized = eTag.Trim().Trim('"');
-        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
+        string normalized = eTag.Trim();
+        while (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"') {
+            normalized = normalized[1..^1].Trim();
+        }
+
+        return string.IsNullOrWhiteSpace(normalized) || normalized.All(static c => c == '"')
+            ? null
+            : normalized;
     }
 }
diff --git a/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs b/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs
index 62ad3fee..80d18b0b 100644
--- a/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs
+++ b/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs
@@ -21,8 +21,10 @@ using Hexalith.Memories.Client.Rest;
 using Hexalith.Tenants.Contracts.Commands;
 using Hexalith.Tenants.Contracts.Enums;
 using Hexalith.Tenants.Contracts.Events;
+using Hexalith.Tenants.Contracts.Projections;
 using Hexalith.Tenants.Contracts.Queries;
 using Hexalith.Tenants.IntegrationTests.Fixtures;
+using Hexalith.Tenants.Server.Projections;
 using Hexalith.Tenants.UI.Services.Gateways;
 using Hexalith.Tenants.UI.State.TenantAudit;
 
@@ -33,6 +35,15 @@ using Microsoft.IdentityModel.Tokens;
 
 using Shouldly;
 
+using RedisConfigurationOptions = StackExchange.Redis.ConfigurationOptions;
+using RedisConnection = StackExchange.Redis.ConnectionMultiplexer;
+using RedisConnectionException = StackExchange.Redis.RedisConnectionException;
+using RedisConnectionMultiplexer = StackExchange.Redis.IConnectionMultiplexer;
+using RedisDatabase = StackExchange.Redis.IDatabase;
+using RedisValue = StackExchange.Redis.RedisValue;
+
+using CommandStatus = Hexalith.EventStore.Contracts.Commands.CommandStatus;
+
 namespace Hexalith.Tenants.IntegrationTests;
 
 /// <summary>
@@ -53,6 +64,7 @@ public class AspireTopologyTests : IDisposable {
     private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
     private static readonly TimeSpan CommandStatusTimeout = TimeSpan.FromSeconds(60);
     private static readonly TimeSpan SampleProjectionTimeout = TimeSpan.FromSeconds(60);
+    private static readonly TimeSpan TenantsApiAlivenessTimeout = TimeSpan.FromMinutes(4);
 
     private readonly IDisposable _daprTestLease;
     private readonly AspireTopologyFixture _fixture;
@@ -103,6 +115,162 @@ public class AspireTopologyTests : IDisposable {
         response.StatusCode.ShouldBe(HttpStatusCode.OK);
     }
 
+    [DaprFact]
+    [Trait("Tier", "3")]
+    public async Task Generated_tenants_api_get_tenant_reads_verified_redis_state_without_projection_authority() {
+        _fixture.SkipIfUnavailable();
+        await WaitForTenantsApiAliveAsync();
+
+        string token = CreateDemoJwt();
+        string tenantId = $"provenance-{Guid.NewGuid():N}";
+        string tenantName = $"Provenance {Guid.NewGuid():N}";
+        const string tenantDescription = "Created by the Story 4.7 persisted-route proof";
+        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
+
+        CommandStatusResponse bootstrapStatus = await SubmitAndWaitForTerminalStatusAsync(
+            _fixture.CommandApiClient,
+            CreateCommand(
+                "global-administrators",
+                "global-administrators",
+                nameof(BootstrapGlobalAdmin),
+                new BootstrapGlobalAdmin("admin-user")),
+            token,
+            timeout.Token,
+            allowAlreadyBootstrappedConflict: true);
+        (bootstrapStatus.Status == "Completed"
+            || (bootstrapStatus.Status == "Rejected" && bootstrapStatus.RejectionEventType == "GlobalAdminAlreadyBootstrappedRejection"))
+            .ShouldBeTrue($"Bootstrap status was {bootstrapStatus.Status}:{bootstrapStatus.RejectionEventType}.");
+
+        CommandStatusResponse createStatus = await SubmitAndWaitForTerminalStatusAsync(
+            _fixture.CommandApiClient,
+            CreateCommand(
+                "tenants",
+                tenantId,
+                nameof(CreateTenant),
+                new CreateTenant(tenantId, tenantName, tenantDescription)),
+            token,
+            timeout.Token);
+        if (createStatus.Status == "PublishFailed") {
+            Assert.Skip($"Aspire pub/sub publication is unavailable: {createStatus.FailureReason ?? "unknown reason"}");
+        }
+
+        createStatus.Status.ShouldBe("Completed");
+
+        TenantReadModel persisted = await WaitForPersistedTenantAsync(tenantId, timeout.Token);
+        persisted.TenantId.ShouldBe(tenantId);
+        persisted.Name.ShouldBe(tenantName);
+        persisted.Description.ShouldBe(tenantDescription);
+        persisted.Status.ShouldBe(TenantStatus.Active);
+        persisted.ProjectedAt.ShouldNotBeNull();
+        persisted.ProjectionVersion.ShouldNotBeNull().ShouldStartWith(TenantProjectionVersionFormat.SequencePrefix);
+
+        var eventStoreQuery = new SubmitQueryRequest(
+            "system",
+            GetTenantQuery.Domain,
+            tenantId,
+            GetTenantQuery.QueryType,
+            GetTenantQuery.ProjectionType,
+            EntityId: tenantId);
+        using var eventStoreRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
+            Content = JsonContent.Create(eventStoreQuery, options: WebJsonOptions),
+        };
+        eventStoreRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
+        eventStoreRequest.Headers.IfNoneMatch.ParseAdd("\"conflicting-validator\"");
+        using HttpResponseMessage eventStoreResponse = await _fixture.CommandApiClient.SendAsync(
+            eventStoreRequest,
+            timeout.Token);
+
+        eventStoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
+        eventStoreResponse.Headers.GetValues("X-Hexalith-Query-Provenance")
+            .ShouldHaveSingleItem()
+            .ShouldBe("HandlerComputed");
+        eventStoreResponse.Headers.ETag.ShouldBeNull();
+        eventStoreResponse.Headers.Contains("X-Hexalith-Projection-Version").ShouldBeFalse();
+        eventStoreResponse.Headers.Contains("X-Hexalith-Is-Stale").ShouldBeFalse();
+        eventStoreResponse.Headers.Contains("X-Hexalith-Is-Degraded").ShouldBeFalse();
+        eventStoreResponse.Headers.Contains(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
+        SubmitQueryResponse eventStoreResult = (await eventStoreResponse.Content.ReadFromJsonAsync<SubmitQueryResponse>(
+            WebJsonOptions,
+            timeout.Token)).ShouldNotBeNull();
+        eventStoreResult.Success.ShouldBeTrue();
+        TenantDetail eventStorePayload = eventStoreResult.Payload
+            .Deserialize<TenantDetail>(WebJsonOptions)
+            .ShouldNotBeNull();
+        AssertTenantDetailMatchesPersisted(eventStorePayload, persisted);
+        QueryResponseMetadata eventStoreMetadata = eventStoreResult.Metadata.ShouldNotBeNull();
+        eventStoreMetadata.Provenance.ShouldBe(QueryResponseProvenance.HandlerComputed);
+        eventStoreMetadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
+        eventStoreMetadata.ETag.ShouldBeNull();
+        eventStoreMetadata.IsNotModified.ShouldBeNull();
+        eventStoreMetadata.ProjectionVersion.ShouldBeNull();
+        eventStoreMetadata.IsStale.ShouldBeNull();
+        eventStoreMetadata.IsDegraded.ShouldBeNull();
+
+        using var rawRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/tenants/{tenantId}");
+        rawRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
+        rawRequest.Headers.IfNoneMatch.ParseAdd("\"conflicting-validator\"");
+        using HttpResponseMessage rawResponse = await _fixture.TenantsApiClient.SendAsync(
+            rawRequest,
+            timeout.Token);
+        string rawContent = await rawResponse.Content.ReadAsStringAsync(timeout.Token);
+
+        rawResponse.StatusCode.ShouldBe(HttpStatusCode.OK, rawContent);
+        rawResponse.Headers.GetValues("X-Hexalith-Query-Provenance").ShouldHaveSingleItem().ShouldBe("HandlerComputed");
+        rawResponse.Headers.ETag.ShouldBeNull();
+        rawResponse.Headers.Contains("X-Hexalith-Projection-Version").ShouldBeFalse();
+        rawResponse.Headers.Contains("X-Hexalith-Is-Stale").ShouldBeFalse();
+        rawResponse.Headers.Contains("X-Hexalith-Is-Degraded").ShouldBeFalse();
+        rawResponse.Headers.Contains(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
+
+        using JsonDocument rawDocument = JsonDocument.Parse(rawContent);
+        JsonElement rawPayload = rawDocument.RootElement;
+        TenantDetail rawTenant = rawPayload.Deserialize<TenantDetail>(WebJsonOptions).ShouldNotBeNull();
+        AssertTenantDetailMatchesPersisted(rawTenant, persisted);
+        rawPayload.TryGetProperty("metadata", out _).ShouldBeFalse();
+        rawPayload.TryGetProperty("projectionVersion", out _).ShouldBeFalse();
+        rawPayload.TryGetProperty("projectedAt", out _).ShouldBeFalse();
+
+        using HttpClient typedHttp = CreateIsolatedTenantsApiClient(_fixture.TenantsApiClient, token);
+        var client = new TenantsRestQueryClient(typedHttp);
+
+        TenantsRestQueryResponse<TenantDetail> typed = await client.GetTenantAsync(
+            new GetTenantQuery { TenantId = tenantId },
+            "conflicting-validator",
+            timeout.Token);
+
+        typed.FailureKind.ShouldBe(TenantsRestQueryFailureKind.None);
+        typed.StatusCode.ShouldBe((int)HttpStatusCode.OK);
+        AssertTenantDetailMatchesPersisted(typed.Payload.ShouldNotBeNull(), persisted);
+        typed.Metadata.Provenance.ShouldBe(QueryResponseProvenance.HandlerComputed);
+        typed.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
+        typed.Metadata.ETag.ShouldBeNull();
+        typed.Metadata.IsNotModified.ShouldBe(false);
+        typed.Metadata.ProjectionVersion.ShouldBeNull();
+        typed.Metadata.IsStale.ShouldBeNull();
+        typed.Metadata.IsDegraded.ShouldBeNull();
+    }
+
+    private static void AssertTenantDetailMatchesPersisted(
+        TenantDetail actual,
+        TenantReadModel persisted) {
+        actual.TenantId.ShouldBe(persisted.TenantId);
+        actual.Name.ShouldBe(persisted.Name);
+        actual.Description.ShouldBe(persisted.Description);
+        actual.Status.ShouldBe(persisted.Status);
+        actual.CreatedAt.ShouldBe(persisted.CreatedAt);
+        actual.Members.Count.ShouldBe(persisted.Members.Count);
+        foreach (KeyValuePair<string, TenantRole> member in persisted.Members) {
+            actual.Members.ShouldContain(candidate =>
+                candidate.UserId == member.Key && candidate.Role == member.Value);
+        }
+
+        actual.Configuration.Count.ShouldBe(persisted.Configuration.Count);
+        foreach (KeyValuePair<string, string> setting in persisted.Configuration) {
+            actual.Configuration.TryGetValue(setting.Key, out string? value).ShouldBeTrue();
+            value.ShouldBe(setting.Value);
+        }
+    }
+
     [DaprFact]
     public async Task CommandApi_process_endpoint_dispatches_command() {
         _fixture.SkipIfUnavailable();
@@ -270,6 +438,78 @@ public class AspireTopologyTests : IDisposable {
         return await WaitForTerminalStatusAsync(client, accepted.CorrelationId, token, cancellationToken);
     }
 
+    private static async Task<TenantReadModel> WaitForPersistedTenantAsync(
+        string tenantId,
+        CancellationToken cancellationToken) {
+        string redisEndpoint = $"localhost:{DaprDiagnostics.DefaultRedisPort}";
+        string persistedKey = $"tenants||projection:tenants:{tenantId}";
+        RedisConnectionMultiplexer redis;
+        try {
+            redis = await RedisConnection.ConnectAsync(new RedisConfigurationOptions {
+                EndPoints = { redisEndpoint },
+                ConnectTimeout = 5_000,
+                SyncTimeout = 5_000,
+                AbortOnConnectFail = true,
+                AllowAdmin = false,
+            });
+        }
+        catch (RedisConnectionException ex) {
+            throw new InvalidOperationException(
+                $"Redis at '{redisEndpoint}' was unreachable while waiting for persisted tenant '{tenantId}'. {ex.Message}",
+                ex);
+        }
+
+        using (redis) {
+            RedisDatabase database = redis.GetDatabase();
+            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(SampleProjectionTimeout);
+            string? lastPayload = null;
+            string? lastJsonError = null;
+
+            while (DateTimeOffset.UtcNow <= deadline) {
+                cancellationToken.ThrowIfCancellationRequested();
+                RedisValue value;
+                try {
+                    value = await database
+                        .HashGetAsync(persistedKey, "data")
+                        .WaitAsync(cancellationToken);
+                }
+                catch (RedisConnectionException ex) {
+                    throw new InvalidOperationException(
+                        $"Redis at '{redisEndpoint}' dropped the connection while waiting for '{persistedKey}'. {ex.Message}",
+                        ex);
+                }
+
+                if (value.HasValue) {
+                    lastPayload = value.ToString();
+                    TenantReadModel? model;
+                    try {
+                        model = JsonSerializer.Deserialize<TenantReadModel>(lastPayload, WebJsonOptions);
+                        lastJsonError = null;
+                    }
+                    catch (JsonException ex) {
+                        lastJsonError = ex.Message;
+                        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
+                        continue;
+                    }
+
+                    if (model is not null
+                        && string.Equals(model.TenantId, tenantId, StringComparison.Ordinal)) {
+                        return model;
+                    }
+                }
+
+                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
+            }
+
+            string jsonSuffix = lastJsonError is null
+                ? string.Empty
+                : $" Last JSON error: {lastJsonError}.";
+            throw new TimeoutException(
+                $"Redis key '{persistedKey}' did not contain the expected tenant read model within {SampleProjectionTimeout}. "
+                + $"Last payload present: {lastPayload is not null}.{jsonSuffix}");
+        }
+    }
+
     private static async Task<CommandStatusResponse> WaitForTerminalStatusAsync(
         HttpClient client,
         string correlationId,
@@ -515,4 +755,70 @@ public class AspireTopologyTests : IDisposable {
     }
 
     private sealed record FixedUserContextAccessor(string? TenantId, string? UserId) : IUserContextAccessor;
+
+    private async Task WaitForTenantsApiAliveAsync() {
+        using var timeout = new CancellationTokenSource(TenantsApiAlivenessTimeout);
+        HttpStatusCode? lastStatus = null;
+        string? lastError = null;
+
+        while (!timeout.IsCancellationRequested) {
+            try {
+                using HttpResponseMessage response = await _fixture.TenantsApiClient.GetAsync("/alive", timeout.Token);
+                lastStatus = response.StatusCode;
+                lastError = null;
+                if (response.StatusCode == HttpStatusCode.OK) {
+                    return;
+                }
+            }
+            catch (HttpRequestException ex) {
+                lastError = ex.Message;
+            }
+            catch (TaskCanceledException) when (timeout.IsCancellationRequested) {
+                break;
+            }
+            catch (TaskCanceledException) {
+                lastError = "request timed out";
+            }
+
+            try {
+                await Task.Delay(TimeSpan.FromSeconds(2), timeout.Token);
+            }
+            catch (TaskCanceledException) {
+                break;
+            }
+        }
+
+        throw new TimeoutException(
+            $"tenants-api /alive did not return HTTP 200 within {TenantsApiAlivenessTimeout}. "
+            + $"Last status: {lastStatus?.ToString() ?? "n/a"}, Last error: {lastError ?? "n/a"}.");
+    }
+
+    private static HttpClient CreateIsolatedTenantsApiClient(HttpClient shared, string token) {
+        ArgumentNullException.ThrowIfNull(shared);
+        ArgumentException.ThrowIfNullOrWhiteSpace(token);
+
+        var client = new HttpClient(new SharedClientRelayHandler(shared), disposeHandler: true) {
+            BaseAddress = shared.BaseAddress,
+            Timeout = shared.Timeout,
+        };
+        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
+        return client;
+    }
+
+    private sealed class SharedClientRelayHandler(HttpClient inner) : HttpMessageHandler {
+        protected override Task<HttpResponseMessage> SendAsync(
+            HttpRequestMessage request,
+            CancellationToken cancellationToken) {
+            ArgumentNullException.ThrowIfNull(request);
+            var clone = new HttpRequestMessage(request.Method, request.RequestUri) {
+                Version = request.Version,
+                VersionPolicy = request.VersionPolicy,
+            };
+            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers) {
+                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
+            }
+
+            return inner.SendAsync(clone, cancellationToken);
+        }
+    }
 }
diff --git a/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs b/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs
index 396610ce..e039e0fd 100644
--- a/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs
+++ b/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs
@@ -12,8 +12,10 @@ namespace Hexalith.Tenants.IntegrationTests.Fixtures;
 /// </remarks>
 public sealed class AspireTopologyFixture : AspireTopologyFixtureBase<Projects.Hexalith_Tenants_AppHost> {
     private static readonly TimeSpan CommandApiHealthTimeout = TimeSpan.FromMinutes(4);
+    private static readonly TimeSpan TenantsApiHealthTimeout = TimeSpan.FromMinutes(4);
     private static readonly TimeSpan SampleHealthTimeout = TimeSpan.FromMinutes(2);
     private static readonly TimeSpan CommandApiClientTimeout = TimeSpan.FromSeconds(60);
+    private static readonly TimeSpan TenantsApiClientTimeout = TimeSpan.FromSeconds(60);
     private static readonly TimeSpan SampleClientTimeout = TimeSpan.FromSeconds(30);
 
     /// <inheritdoc/>
@@ -23,6 +25,10 @@ public sealed class AspireTopologyFixture : AspireTopologyFixtureBase<Projects.H
         new("tenants", "http", CommandApiClientTimeout, CommandApiHealthTimeout, WaitForAliveness: true, CommandApiHealthTimeout),
         new("tenants-ui", "http", CommandApiClientTimeout, CommandApiHealthTimeout, WaitForAliveness: false, CommandApiHealthTimeout),
         new("sample", "http", SampleClientTimeout, SampleHealthTimeout, WaitForAliveness: true, SampleHealthTimeout),
+        // HTTPS is required because tenants-api redirects HTTP. Aliveness is polled by the Story 4.7
+        // proof with that test's timeout so a slow or unhealthy generated API does not stall the
+        // shared fixture's 6-minute startup budget or the other topology tests.
+        new("tenants-api", "https", TenantsApiClientTimeout, TenantsApiHealthTimeout, WaitForAliveness: false, TenantsApiHealthTimeout),
     ];
 
     /// <inheritdoc/>
@@ -34,6 +40,9 @@ public sealed class AspireTopologyFixture : AspireTopologyFixtureBase<Projects.H
     /// <summary>Gets the HTTP client for the Tenants domain service (exposes /process endpoint).</summary>
     public HttpClient TenantsClient => Client("tenants");
 
+    /// <summary>Gets the HTTP client for the generated Tenants REST API.</summary>
+    public HttpClient TenantsApiClient => Client("tenants-api");
+
     /// <summary>Gets the HTTP client for the Tenants UI resource.</summary>
     public HttpClient TenantsUiClient => Client("tenants-ui");
 
diff --git a/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj b/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj
index c88dfa7f..4b5ef2d3 100644
--- a/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj
+++ b/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj
@@ -24,6 +24,7 @@
          (e.g. Projects.Hexalith_Tenants_AppHost) used by the topology fixture. -->
     <PackageReference Include="Aspire.Hosting.Testing" />
     <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
+    <PackageReference Include="StackExchange.Redis" />
   </ItemGroup>
 
   <ItemGroup>
diff --git a/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs b/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs
index c56b7c49..e364b574 100644
--- a/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs
+++ b/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs
@@ -113,6 +113,52 @@ public sealed class TenantsApiGeneratedControllerTests
         payload.GetProperty("pageSize").GetInt32().ShouldBe(25);
     }
 
+    [Fact]
+    public async Task Generated_query_route_suppresses_projection_headers_for_handler_computed_result()
+    {
+        CapturingEventStoreGatewayClient gateway = new();
+        gateway.EnqueueQueryResult(
+            new TenantDetail(
+                "tenant.alpha",
+                "Alpha",
+                "Tenant Alpha",
+                TenantStatus.Active,
+                [],
+                new Dictionary<string, string>(StringComparer.Ordinal),
+                DateTimeOffset.Parse("2026-07-03T05:20:00Z", CultureInfo.InvariantCulture)),
+            eTag: "opaque-store-etag",
+            metadata: new QueryResponseMetadata(
+                ETag: "opaque-store-etag",
+                IsNotModified: false,
+                IsStale: false,
+                ProjectionVersion: "tenant-sequence:42")
+            {
+                Provenance = QueryResponseProvenance.HandlerComputed,
+                Lifecycle = ProjectionLifecycleState.Current,
+            });
+        await using var factory = new TenantsApiWebApplicationFactory(gateway);
+        using HttpClient client = CreateAuthenticatedClient(factory);
+        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenants/tenant.alpha");
+        request.Headers.IfNoneMatch.ParseAdd("\"conflicting-validator\"");
+
+        using HttpResponseMessage response = await client.SendAsync(
+            request,
+            TestContext.Current.CancellationToken);
+
+        response.StatusCode.ShouldBe(HttpStatusCode.OK);
+        response.Headers.GetValues("X-Hexalith-Query-Provenance").ShouldHaveSingleItem().ShouldBe("HandlerComputed");
+        response.Headers.ETag.ShouldBeNull();
+        response.Headers.Contains("X-Hexalith-Projection-Version").ShouldBeFalse();
+        response.Headers.Contains("X-Hexalith-Is-Stale").ShouldBeFalse();
+        response.Headers.Contains(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
+
+        TenantDetail? detail = await response.Content.ReadFromJsonAsync<TenantDetail>(
+            JsonOptions,
+            TestContext.Current.CancellationToken);
+        detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
+        gateway.SubmittedQueries.ShouldHaveSingleItem().IfNoneMatch.ShouldBe("\"conflicting-validator\"");
+    }
+
     [Fact]
     public async Task UserTenants_generated_absolute_route_submits_index_query_for_target_user_entity()
     {
diff --git a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs
index 583fb2e5..c85dd181 100644
--- a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs
+++ b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs
@@ -21,39 +21,100 @@ using Shouldly;
 
 namespace Hexalith.Tenants.Server.Tests.Queries;
 
-public sealed class TenantQueryFreshnessTests {
+public sealed class TenantQueryFreshnessTests
+{
     private static readonly DateTimeOffset Now = new(2026, 6, 25, 13, 0, 0, TimeSpan.Zero);
-    private static readonly ReadModelFreshnessOptions Thresholds = new() {
+    private const string GenuineSequenceVersion = TenantProjectionVersionFormat.SequencePrefix + "42";
+    private static readonly ReadModelFreshnessOptions Thresholds = new()
+    {
         Aging = TimeSpan.FromMinutes(10),
         Stale = TimeSpan.FromMinutes(30),
     };
 
+    public static IEnumerable<object?[]> HandlerFreshnessCases()
+    {
+        (string QueryType, string PrimaryKey, string ETag)[] routes =
+        [
+            (ListTenantsQuery.QueryType, TenantQueryHandlerBase.TenantIndexProjectionKey, "index-etag-1"),
+            (GetUserTenantsQuery.QueryType, TenantQueryHandlerBase.TenantIndexProjectionKey, "index-etag-1"),
+            (GetTenantQuery.QueryType, TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha", "tenant-etag-1"),
+            (GetTenantUsersQuery.QueryType, TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha", "tenant-etag-1"),
+            (GetTenantAuditQuery.QueryType, TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha", "audit-etag-1"),
+            (GetGlobalAdministratorsQuery.QueryType, TenantQueryHandlerBase.GlobalAdminProjectionKey, "admin-etag-1"),
+        ];
+
+        int?[] projectedAges = [5, 40, null];
+        foreach ((string queryType, string primaryKey, string eTag) in routes)
+        {
+            foreach (int? projectedAge in projectedAges)
+            {
+                yield return [queryType, primaryKey, eTag, projectedAge];
+            }
+        }
+    }
+
     [Theory]
-    [InlineData(5, false)]
-    [InlineData(20, false)]
-    [InlineData(40, true)]
-    public async Task Get_tenant_classifies_projected_at_age_server_sideAsync(int projectedAgeMinutes, bool expectedIsStale) {
+    [MemberData(nameof(HandlerFreshnessCases))]
+    public async Task Query_handlers_ignore_primary_read_model_timestamp_and_sequence_authorityAsync(
+        string queryType,
+        string expectedPrimaryKey,
+        string expectedETag,
+        int? projectedAgeMinutes)
+    {
+        DateTimeOffset? primaryProjectedAt = projectedAgeMinutes.HasValue
+            ? Now - TimeSpan.FromMinutes(projectedAgeMinutes.Value)
+            : null;
         IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenant(store, "tenant-etag-1", Now - TimeSpan.FromMinutes(projectedAgeMinutes));
-        SetupGlobalAdministrators(store, "admin-user");
+        TenantIndexReadModel index = SetupTenantIndex(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey ? expectedETag : "index-etag-2",
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey ? primaryProjectedAt : Now);
+        TenantReadModel tenant = SetupTenant(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "tenant-etag-2",
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha" ? primaryProjectedAt : Now);
+        TenantAuditReadModel audit = SetupTenantAudit(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "audit-etag-2",
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha" ? primaryProjectedAt : Now);
+        GlobalAdministratorReadModel administrators = SetupGlobalAdministrators(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey ? expectedETag : "admin-etag-2",
+            expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey ? primaryProjectedAt : Now,
+            "admin-user");
+
+        AssertPrimaryReadModelInputsExist(
+            expectedPrimaryKey,
+            primaryProjectedAt,
+            index,
+            tenant,
+            audit,
+            administrators);
 
         TenantQueryResult result = (await TenantQueryTestHarness.ExecuteAsync(
             store,
             CreateCursorCodec(),
-            CreateEnvelope(GetTenantQuery.QueryType),
+            CreateEnvelope(queryType),
             freshnessOptions: Thresholds,
             timeProvider: new FixedTimeProvider(Now))).ShouldBeOfType<TenantQueryResult>();
 
-        result.Metadata.ShouldNotBeNull().IsStale.ShouldBe(expectedIsStale);
-        result.Metadata.ServedAt.ShouldBe(Now);
-        result.Metadata.ProjectionVersion.ShouldBe("tenant-etag-1");
+        await AssertPrimaryReadModelWasReadAsync(store, expectedPrimaryKey);
+        AssertValidatorOnly(result, expectedETag);
     }
 
-    [Fact]
-    public async Task Get_tenant_without_projected_at_reports_unknown_freshnessAsync() {
+    [Theory]
+    [InlineData(null)]
+    [InlineData("")]
+    [InlineData("   ")]
+    [InlineData("\"\"")]
+    [InlineData("\"")]
+    [InlineData("\"\"\"")]
+    [InlineData("  \" \"  ")]
+    public async Task Query_handler_omits_metadata_for_degenerate_etagAsync(string? eTag)
+    {
         IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenant(store, "tenant-etag-1", projectedAt: null);
-        SetupGlobalAdministrators(store, "admin-user");
+        SetupTenant(store, eTag, Now - TimeSpan.FromMinutes(40));
+        SetupGlobalAdministrators(store, "admin-etag", Now, "admin-user");
 
         TenantQueryResult result = (await TenantQueryTestHarness.ExecuteAsync(
             store,
@@ -62,83 +123,102 @@ public sealed class TenantQueryFreshnessTests {
             freshnessOptions: Thresholds,
             timeProvider: new FixedTimeProvider(Now))).ShouldBeOfType<TenantQueryResult>();
 
-        result.Metadata.ShouldNotBeNull().IsStale.ShouldBeNull();
-        result.Metadata.ServedAt.ShouldBe(Now);
+        result.Metadata.ShouldBeNull();
     }
 
-    [Fact]
-    public async Task Get_tenant_with_projected_at_and_no_etag_still_classifies_freshnessAsync() {
-        IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenant(store, " ", Now - TimeSpan.FromMinutes(40));
-        SetupGlobalAdministrators(store, "admin-user");
+    private static void AssertValidatorOnly(TenantQueryResult result, string expectedETag)
+    {
+        result.Success.ShouldBeTrue();
+        QueryResponseMetadata metadata = result.Metadata.ShouldNotBeNull();
+        metadata.ETag.ShouldBe(expectedETag);
+        metadata.IsNotModified.ShouldBe(false);
+        metadata.ProjectionVersion.ShouldBeNull();
+        metadata.IsStale.ShouldBeNull();
+        metadata.IsDegraded.ShouldBeNull();
+        metadata.ServedAt.ShouldBeNull();
+        metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
+        metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
+    }
 
-        TenantQueryResult result = (await TenantQueryTestHarness.ExecuteAsync(
-            store,
-            CreateCursorCodec(),
-            CreateEnvelope(GetTenantQuery.QueryType),
-            freshnessOptions: Thresholds,
-            timeProvider: new FixedTimeProvider(Now))).ShouldBeOfType<TenantQueryResult>();
+    private static void AssertPrimaryReadModelInputsExist(
+        string expectedPrimaryKey,
+        DateTimeOffset? primaryProjectedAt,
+        TenantIndexReadModel index,
+        TenantReadModel tenant,
+        TenantAuditReadModel audit,
+        GlobalAdministratorReadModel administrators)
+    {
+        IReadModelFreshness freshness;
+        if (expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey)
+        {
+            freshness = index;
+        }
+        else if (expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha")
+        {
+            freshness = tenant;
+        }
+        else if (expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha")
+        {
+            freshness = audit;
+        }
+        else if (expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey)
+        {
+            freshness = administrators;
+        }
+        else
+        {
+            throw new ArgumentOutOfRangeException(nameof(expectedPrimaryKey), expectedPrimaryKey, "Unsupported primary key.");
+        }
 
-        result.Metadata.ShouldNotBeNull().ETag.ShouldBeNull();
-        result.Metadata.IsStale.ShouldBe(true);
-        result.Metadata.ServedAt.ShouldBe(Now);
-        result.Metadata.ProjectionVersion.ShouldBeNull();
+        freshness.ProjectedAt.ShouldBe(primaryProjectedAt);
+        freshness.ProjectionVersion.ShouldBe(GenuineSequenceVersion);
     }
 
-    [Fact]
-    public async Task Get_tenant_prefers_persisted_projection_version_over_etagAsync() {
-        IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenant(
-            store,
-            "opaque-store-etag",
-            Now - TimeSpan.FromMinutes(5),
-            projectionVersion: TenantProjectionVersionFormat.SequencePrefix + "10");
-        SetupGlobalAdministrators(store, "admin-user");
-
-        TenantQueryResult result = (await TenantQueryTestHarness.ExecuteAsync(
-            store,
-            CreateCursorCodec(),
-            CreateEnvelope(GetTenantQuery.QueryType),
-            freshnessOptions: Thresholds,
-            timeProvider: new FixedTimeProvider(Now))).ShouldBeOfType<TenantQueryResult>();
+    private static async Task AssertPrimaryReadModelWasReadAsync(IReadModelStore store, string expectedPrimaryKey)
+    {
+        if (expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey)
+        {
+            _ = await store.Received().GetAsync<TenantIndexReadModel>(
+                TenantQueryHandlerBase.StateStoreName,
+                expectedPrimaryKey,
+                Arg.Any<CancellationToken>());
+            return;
+        }
 
-        result.Metadata.ShouldNotBeNull().ETag.ShouldBe("opaque-store-etag");
-        result.Metadata.ProjectionVersion.ShouldBe(TenantProjectionVersionFormat.SequencePrefix + "10");
-    }
+        if (expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha")
+        {
+            _ = await store.Received().GetAsync<TenantReadModel>(
+                TenantQueryHandlerBase.StateStoreName,
+                expectedPrimaryKey,
+                Arg.Any<CancellationToken>());
+            return;
+        }
 
-    [Theory]
-    [InlineData("list-tenants", "projection:tenant-index:singleton", "index-etag-1", false)]
-    [InlineData("get-user-tenants", "projection:tenant-index:singleton", "index-etag-1", false)]
-    [InlineData("get-tenant", "projection:tenants:tenant.alpha", "tenant-etag-1", false)]
-    [InlineData("get-tenant-users", "projection:tenants:tenant.alpha", "tenant-etag-1", false)]
-    [InlineData("get-tenant-audit", "audit:tenant.alpha", "audit-etag-1", true)]
-    [InlineData("get-global-administrators", "projection:global-administrators:singleton", "admin-etag-1", false)]
-    public async Task Query_handlers_classify_from_primary_read_model_projected_atAsync(
-        string queryType,
-        string expectedPrimaryKey,
-        string expectedETag,
-        bool expectedIsStale) {
-        IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenantIndex(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey ? Now.AddMinutes(-5) : Now.AddMinutes(-40));
-        SetupTenant(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "tenant-etag-2", Now.AddMinutes(-5));
-        SetupTenantAudit(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha" ? Now.AddMinutes(-40) : Now.AddMinutes(-5));
-        SetupGlobalAdministrators(store, "admin-user", projectedAt: Now.AddMinutes(-5));
+        if (expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha")
+        {
+            _ = await store.Received().GetAsync<TenantAuditReadModel>(
+                TenantQueryHandlerBase.StateStoreName,
+                expectedPrimaryKey,
+                Arg.Any<CancellationToken>());
+            return;
+        }
 
-        QueryResult result = await TenantQueryTestHarness.ExecuteAsync(
-            store,
-            CreateCursorCodec(),
-            CreateEnvelope(queryType),
-            freshnessOptions: Thresholds,
-            timeProvider: new FixedTimeProvider(Now));
+        if (expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey)
+        {
+            _ = await store.Received().GetAsync<GlobalAdministratorReadModel>(
+                TenantQueryHandlerBase.StateStoreName,
+                expectedPrimaryKey,
+                Arg.Any<CancellationToken>());
+            return;
+        }
 
-        TenantQueryResult tenantResult = result.ShouldBeOfType<TenantQueryResult>();
-        tenantResult.Metadata.ShouldNotBeNull().ETag.ShouldBe(expectedETag);
-        tenantResult.Metadata.IsStale.ShouldBe(expectedIsStale);
-        tenantResult.Metadata.ServedAt.ShouldBe(Now);
+        throw new ArgumentOutOfRangeException(nameof(expectedPrimaryKey), expectedPrimaryKey, "Unsupported primary key.");
     }
 
-    private static QueryEnvelope CreateEnvelope(string queryType) {
-        if (string.Equals(queryType, ListTenantsQuery.QueryType, StringComparison.Ordinal)) {
+    private static QueryEnvelope CreateEnvelope(string queryType)
+    {
+        if (string.Equals(queryType, ListTenantsQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 ListTenantsQuery.Domain,
@@ -150,7 +230,8 @@ public sealed class TenantQueryFreshnessTests {
                 "admin-user");
         }
 
-        if (string.Equals(queryType, GetUserTenantsQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetUserTenantsQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetUserTenantsQuery.Domain,
@@ -162,7 +243,8 @@ public sealed class TenantQueryFreshnessTests {
                 "target-user");
         }
 
-        if (string.Equals(queryType, GetTenantQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetTenantQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetTenantQuery.Domain,
@@ -174,7 +256,8 @@ public sealed class TenantQueryFreshnessTests {
                 "tenant.alpha");
         }
 
-        if (string.Equals(queryType, GetTenantUsersQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetTenantUsersQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetTenantUsersQuery.Domain,
@@ -186,7 +269,8 @@ public sealed class TenantQueryFreshnessTests {
                 "tenant.alpha");
         }
 
-        if (string.Equals(queryType, GetTenantAuditQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetTenantAuditQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetTenantAuditQuery.Domain,
@@ -198,7 +282,8 @@ public sealed class TenantQueryFreshnessTests {
                 "tenant.alpha");
         }
 
-        if (string.Equals(queryType, GetGlobalAdministratorsQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetGlobalAdministratorsQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetGlobalAdministratorsQuery.Domain,
@@ -217,30 +302,44 @@ public sealed class TenantQueryFreshnessTests {
     private static IQueryCursorCodec CreateCursorCodec()
         => new QueryCursorCodec(new EphemeralDataProtectionProvider(), "Hexalith.Tenants.QueryCursor.v1");
 
-    private static void SetupGlobalAdministrators(
+    private static GlobalAdministratorReadModel SetupGlobalAdministrators(
         IReadModelStore store,
-        string administratorId,
-        DateTimeOffset? projectedAt = null) {
-        var model = new GlobalAdministratorReadModel {
-            Administrators = [administratorId],
+        string eTag,
+        DateTimeOffset? projectedAt,
+        params string[] administratorIds)
+    {
+        var model = new GlobalAdministratorReadModel
+        {
+            Administrators = administratorIds.ToHashSet(StringComparer.Ordinal),
             ProjectedAt = projectedAt,
+            ProjectionVersion = GenuineSequenceVersion,
         };
 
         _ = store.GetAsync<GlobalAdministratorReadModel>(
                 TenantQueryHandlerBase.StateStoreName,
                 TenantQueryHandlerBase.GlobalAdminProjectionKey,
                 Arg.Any<CancellationToken>())
-            .Returns(Task.FromResult(new ReadModelEntry<GlobalAdministratorReadModel>(model, "admin-etag-1")));
+            .Returns(Task.FromResult(new ReadModelEntry<GlobalAdministratorReadModel>(model, eTag)));
+        return model;
     }
 
-    private static void SetupTenantIndex(IReadModelStore store, DateTimeOffset? projectedAt) {
-        var model = new TenantIndexReadModel {
+    private static TenantIndexReadModel SetupTenantIndex(
+        IReadModelStore store,
+        string eTag,
+        DateTimeOffset? projectedAt)
+    {
+        var model = new TenantIndexReadModel
+        {
             ProjectedAt = projectedAt,
-            Tenants = {
+            ProjectionVersion = GenuineSequenceVersion,
+            Tenants =
+            {
                 ["tenant.alpha"] = new TenantIndexEntry("Tenant Alpha", TenantStatus.Active),
             },
-            UserTenants = {
-                ["target-user"] = new Dictionary<string, TenantRole>(StringComparer.Ordinal) {
+            UserTenants =
+            {
+                ["target-user"] = new Dictionary<string, TenantRole>(StringComparer.Ordinal)
+                {
                     ["tenant.alpha"] = TenantRole.TenantReader,
                 },
                 ["admin-user"] = new Dictionary<string, TenantRole>(StringComparer.Ordinal),
@@ -251,22 +350,25 @@ public sealed class TenantQueryFreshnessTests {
                 TenantQueryHandlerBase.StateStoreName,
                 TenantQueryHandlerBase.TenantIndexProjectionKey,
                 Arg.Any<CancellationToken>())
-            .Returns(Task.FromResult(new ReadModelEntry<TenantIndexReadModel>(model, "index-etag-1")));
+            .Returns(Task.FromResult(new ReadModelEntry<TenantIndexReadModel>(model, eTag)));
+        return model;
     }
 
-    private static void SetupTenant(
+    private static TenantReadModel SetupTenant(
         IReadModelStore store,
-        string eTag,
-        DateTimeOffset? projectedAt,
-        string? projectionVersion = null) {
-        var model = new TenantReadModel {
+        string? eTag,
+        DateTimeOffset? projectedAt)
+    {
+        var model = new TenantReadModel
+        {
             TenantId = "tenant.alpha",
             Name = "Tenant Alpha",
             Status = TenantStatus.Active,
             CreatedAt = DateTimeOffset.Parse("2026-06-07T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
             ProjectedAt = projectedAt,
-            ProjectionVersion = projectionVersion,
-            Members = {
+            ProjectionVersion = GenuineSequenceVersion,
+            Members =
+            {
                 ["test-user"] = TenantRole.TenantReader,
             },
         };
@@ -276,12 +378,20 @@ public sealed class TenantQueryFreshnessTests {
                 TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha",
                 Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(new ReadModelEntry<TenantReadModel>(model, eTag)));
+        return model;
     }
 
-    private static void SetupTenantAudit(IReadModelStore store, DateTimeOffset? projectedAt) {
-        var model = new TenantAuditReadModel {
+    private static TenantAuditReadModel SetupTenantAudit(
+        IReadModelStore store,
+        string eTag,
+        DateTimeOffset? projectedAt)
+    {
+        var model = new TenantAuditReadModel
+        {
             ProjectedAt = projectedAt,
-            Entries = [
+            ProjectionVersion = GenuineSequenceVersion,
+            Entries =
+            [
                 new TenantAuditEntry(
                     "event-1",
                     "TenantCreated",
@@ -297,10 +407,12 @@ public sealed class TenantQueryFreshnessTests {
                 TenantQueryHandlerBase.StateStoreName,
                 TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha",
                 Arg.Any<CancellationToken>())
-            .Returns(Task.FromResult(new ReadModelEntry<TenantAuditReadModel>(model, "audit-etag-1")));
+            .Returns(Task.FromResult(new ReadModelEntry<TenantAuditReadModel>(model, eTag)));
+        return model;
     }
 
-    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider {
+    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
+    {
         public override DateTimeOffset GetUtcNow() => utcNow;
     }
 }
diff --git a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs
index d8836ac2..0f2fd115 100644
--- a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs
+++ b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs
@@ -29,7 +29,8 @@ public sealed class TenantQueryHandlerETagTests
     [InlineData("get-tenant", "projection:tenants:tenant.alpha", "tenant-etag-1")]
     [InlineData("get-tenant-users", "projection:tenants:tenant.alpha", "tenant-etag-2")]
     [InlineData("get-tenant-audit", "audit:tenant.alpha", "audit-etag-1")]
-    public async Task Query_handlers_surface_primary_read_model_etag_as_projection_version(
+    [InlineData("get-global-administrators", "projection:global-administrators:singleton", "admin-etag-1")]
+    public async Task Query_handlers_surface_primary_read_model_etag_only_as_opaque_validator(
         string queryType,
         string expectedPrimaryKey,
         string expectedETag)
@@ -38,7 +39,10 @@ public sealed class TenantQueryHandlerETagTests
         SetupTenantIndex(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey ? expectedETag : "index-etag");
         SetupTenant(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "tenant-etag");
         SetupTenantAudit(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "audit-etag");
-        SetupGlobalAdministrators(store, "admin-user");
+        SetupGlobalAdministrators(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey ? expectedETag : "admin-etag",
+            "admin-user");
 
         QueryResult result = await TenantQueryTestHarness.ExecuteAsync(
             store,
@@ -48,10 +52,15 @@ public sealed class TenantQueryHandlerETagTests
 
         result.Success.ShouldBeTrue();
         TenantQueryResult tenantResult = result.ShouldBeOfType<TenantQueryResult>();
-        tenantResult.Metadata.ShouldNotBeNull().ETag.ShouldBe(expectedETag);
-        tenantResult.Metadata.ProjectionVersion.ShouldBe(expectedETag);
-        tenantResult.Metadata.IsStale.ShouldBe(false);
-        tenantResult.Metadata.ServedAt.ShouldBe(Now);
+        QueryResponseMetadata metadata = tenantResult.Metadata.ShouldNotBeNull();
+        metadata.ETag.ShouldBe(expectedETag);
+        metadata.IsNotModified.ShouldBe(false);
+        metadata.ProjectionVersion.ShouldBeNull();
+        metadata.IsStale.ShouldBeNull();
+        metadata.IsDegraded.ShouldBeNull();
+        metadata.ServedAt.ShouldBeNull();
+        metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
+        metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
     }
 
     private static QueryEnvelope CreateEnvelope(string queryType)
@@ -121,13 +130,30 @@ public sealed class TenantQueryHandlerETagTests
                 "tenant.alpha");
         }
 
+        if (string.Equals(queryType, GetGlobalAdministratorsQuery.QueryType, StringComparison.Ordinal))
+        {
+            return new QueryEnvelope(
+                TenantIdentity.DefaultTenantId,
+                GetGlobalAdministratorsQuery.Domain,
+                TenantIdentity.GlobalAdministratorsAggregateId,
+                GetGlobalAdministratorsQuery.QueryType,
+                JsonSerializer.SerializeToUtf8Bytes(new { cursor = (string?)null, pageSize = 20 }),
+                "correlation-1",
+                "admin-user",
+                TenantIdentity.GlobalAdministratorsAggregateId,
+                isGlobalAdmin: true);
+        }
+
         throw new ArgumentOutOfRangeException(nameof(queryType), queryType, "Unsupported query type.");
     }
 
     private static IQueryCursorCodec CreateCursorCodec()
         => new QueryCursorCodec(new EphemeralDataProtectionProvider(), "Hexalith.Tenants.QueryCursor.v1");
 
-    private static void SetupGlobalAdministrators(IReadModelStore store, params string[] administratorIds)
+    private static void SetupGlobalAdministrators(
+        IReadModelStore store,
+        string eTag,
+        params string[] administratorIds)
     {
         var model = new GlobalAdministratorReadModel
         {
@@ -139,7 +165,7 @@ public sealed class TenantQueryHandlerETagTests
                 TenantQueryHandlerBase.StateStoreName,
                 TenantQueryHandlerBase.GlobalAdminProjectionKey,
                 Arg.Any<CancellationToken>())
-            .Returns(Task.FromResult(new ReadModelEntry<GlobalAdministratorReadModel>(model, "admin-etag")));
+            .Returns(Task.FromResult(new ReadModelEntry<GlobalAdministratorReadModel>(model, eTag)));
     }
 
     private static void SetupTenantIndex(IReadModelStore store, string eTag)
diff --git a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryResultTests.cs b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryResultTests.cs
new file mode 100644
index 00000000..4924ed77
--- /dev/null
+++ b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryResultTests.cs
@@ -0,0 +1,150 @@
+using System.Text.Json;
+
+using Hexalith.EventStore.Client.Projections;
+using Hexalith.EventStore.Contracts.Queries;
+using Hexalith.Tenants.Contracts.Projections;
+using Hexalith.Tenants.Queries;
+using Hexalith.Tenants.Server.Projections;
+
+using Shouldly;
+
+namespace Hexalith.Tenants.Server.Tests.Queries;
+
+public sealed class TenantQueryResultTests
+{
+    private static readonly JsonElement Payload = JsonSerializer.SerializeToElement(new { tenantId = "tenant.alpha" });
+    private static readonly ReadModelFreshnessThresholds Thresholds = new(
+        TimeSpan.FromMinutes(10),
+        TimeSpan.FromMinutes(30));
+
+    [Theory]
+    [InlineData("opaque-etag", "opaque-etag")]
+    [InlineData("  opaque-etag  ", "opaque-etag")]
+    [InlineData("\"opaque-etag\"", "opaque-etag")]
+    [InlineData("  \"opaque-etag\"  ", "opaque-etag")]
+    [InlineData("W/\"abc\"", "W/\"abc\"")]
+    public void Validator_only_factory_normalizes_opaque_etag(
+        string eTag,
+        string expectedETag)
+    {
+        TenantQueryResult result = TenantQueryResult.FromPayload(Payload, "tenants", eTag);
+
+        AssertValidatorOnly(result, expectedETag);
+    }
+
+    [Theory]
+    [InlineData(null)]
+    [InlineData("")]
+    [InlineData("   ")]
+    [InlineData("\"\"")]
+    [InlineData("\"")]
+    [InlineData("\"\"\"")]
+    [InlineData("  \" \"  ")]
+    public void Validator_only_factory_omits_metadata_for_degenerate_etag(string? eTag)
+    {
+        TenantQueryResult result = TenantQueryResult.FromPayload(Payload, "tenants", eTag);
+
+        result.Metadata.ShouldBeNull();
+    }
+
+    [Theory]
+    [InlineData("2026-06-25T13:00:00Z", TenantProjectionVersionFormat.SequencePrefix + "42")]
+    [InlineData("2026-06-25T12:00:00Z", null)]
+    [InlineData(null, TenantProjectionVersionFormat.SequencePrefix + "42")]
+    public void Freshness_overload_ignores_timestamp_and_sequence_authority(
+        string? projectedAt,
+        string? projectionVersion)
+    {
+        var readModel = new TenantReadModel
+        {
+            TenantId = "tenant.alpha",
+            ProjectedAt = projectedAt is null ? null : DateTimeOffset.Parse(projectedAt, System.Globalization.CultureInfo.InvariantCulture),
+            ProjectionVersion = projectionVersion,
+        };
+
+        TenantQueryResult result = TenantQueryResult.FromPayload(
+            Payload,
+            "tenants",
+            readModel,
+            Thresholds,
+            DateTimeOffset.Parse("2026-06-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+            "\"opaque-store-etag\"");
+
+        AssertValidatorOnly(result, "opaque-store-etag");
+    }
+
+    [Theory]
+    [InlineData(null)]
+    [InlineData("")]
+    [InlineData("   ")]
+    [InlineData("\"\"")]
+    [InlineData("\"")]
+    [InlineData("\"\"\"")]
+    [InlineData("  \" \"  ")]
+    public void Freshness_overload_omits_metadata_for_degenerate_etag(string? eTag)
+    {
+        var readModel = new TenantReadModel
+        {
+            TenantId = "tenant.alpha",
+            ProjectedAt = DateTimeOffset.Parse("2026-06-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+            ProjectionVersion = TenantProjectionVersionFormat.SequencePrefix + "42",
+        };
+
+        TenantQueryResult result = TenantQueryResult.FromPayload(
+            Payload,
+            "tenants",
+            readModel,
+            Thresholds,
+            DateTimeOffset.Parse("2026-06-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+            eTag);
+
+        result.Metadata.ShouldBeNull();
+    }
+
+    [Fact]
+    public void Validator_only_factory_rejects_undefined_payload()
+    {
+        ArgumentException exception = Should.Throw<ArgumentException>(
+            () => TenantQueryResult.FromPayload(default, "tenants", "opaque-etag"));
+
+        exception.ParamName.ShouldBe("payload");
+        exception.Message.ShouldContain("Undefined");
+    }
+
+    [Fact]
+    public void Freshness_overload_rejects_undefined_payload()
+    {
+        var readModel = new TenantReadModel
+        {
+            TenantId = "tenant.alpha",
+            ProjectedAt = DateTimeOffset.Parse("2026-06-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+            ProjectionVersion = TenantProjectionVersionFormat.SequencePrefix + "42",
+        };
+
+        ArgumentException exception = Should.Throw<ArgumentException>(
+            () => TenantQueryResult.FromPayload(
+                default,
+                "tenants",
+                readModel,
+                Thresholds,
+                DateTimeOffset.Parse("2026-06-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+                "opaque-etag"));
+
+        exception.ParamName.ShouldBe("payload");
+        exception.Message.ShouldContain("Undefined");
+    }
+
+    private static void AssertValidatorOnly(TenantQueryResult result, string expectedETag)
+    {
+        result.Success.ShouldBeTrue();
+        QueryResponseMetadata metadata = result.Metadata.ShouldNotBeNull();
+        metadata.ETag.ShouldBe(expectedETag);
+        metadata.IsNotModified.ShouldBe(false);
+        metadata.ProjectionVersion.ShouldBeNull();
+        metadata.IsStale.ShouldBeNull();
+        metadata.IsDegraded.ShouldBeNull();
+        metadata.ServedAt.ShouldBeNull();
+        metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
+        metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
+    }
+}
diff --git a/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleCommandSnapshotTests.cs b/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleCommandSnapshotTests.cs
index 96d87a76..1107b599 100644
--- a/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleCommandSnapshotTests.cs
+++ b/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleCommandSnapshotTests.cs
@@ -329,9 +329,9 @@ public sealed class TenantLifecycleCommandSnapshotTests
     [Fact]
     public void Opaque_tokens_sharing_a_prefix_and_ending_in_increasing_digits_do_not_confirm()
     {
-        // TenantQueryResult falls back to the state-store ETag when no read model carries a projection
-        // version. Two such markers can share a textual prefix and end in increasing digits without those
-        // digits expressing causal order, so only the aggregate sequence token may satisfy the ordered gate.
+        // Opaque validators are not projection versions. Two such markers can share a textual prefix and end
+        // in increasing digits without those digits expressing causal order, so only an aggregate sequence
+        // token supplied by an authoritative projection-backed route may satisfy the ordered gate.
         TenantLifecycleCommandSnapshot pending = Pending(
             TenantLifecycleOperation.DisableTenant,
             TenantStatus.Active,
diff --git a/tests/Hexalith.Tenants.UI.Tests/State/TenantMembershipCommandProvenanceTests.cs b/tests/Hexalith.Tenants.UI.Tests/State/TenantMembershipCommandProvenanceTests.cs
index 8c64d0e6..d521aa8d 100644
--- a/tests/Hexalith.Tenants.UI.Tests/State/TenantMembershipCommandProvenanceTests.cs
+++ b/tests/Hexalith.Tenants.UI.Tests/State/TenantMembershipCommandProvenanceTests.cs
@@ -9,11 +9,10 @@ namespace Hexalith.Tenants.UI.Tests.State;
 /// Pins the projection-version contract membership confirmation depends on.
 /// </summary>
 /// <remarks>
-/// Confirmation requires an ordered advancement of the value the query path publishes as
-/// <c>ProjectionVersion</c>. The tenant projection publishes the aggregate-local EventStore sequence
-/// as <c>tenant-sequence:&lt;n&gt;</c>; <c>TenantQueryResult</c> falls back to the state-store ETag only for
-/// legacy read models that do not yet carry that value. A legacy store whose ETag is a hash, GUID, or
-/// other token without a stable prefix and trailing number does not satisfy the ordered contract.
+/// Confirmation requires an ordered advancement of an authoritative <c>ProjectionVersion</c>. The tenant
+/// projection persists the aggregate-local EventStore sequence as <c>tenant-sequence:&lt;n&gt;</c>, while
+/// handler-computed query responses publish no projection version and never substitute the state-store
+/// ETag. Defensive comparison of legacy tokens remains fail-closed for hashes, GUIDs, and other opaque values.
 /// </remarks>
 public sealed class TenantMembershipCommandProvenanceTests
 {

