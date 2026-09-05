---
title: 'Tenants Query Provenance Follow-Up'
type: 'bugfix'
created: '2026-08-27'
status: 'in-review'
review_loop_iteration: 1
followup_review_recommended: false
baseline_commit: '168c657676ab2e210401bb5fe1c7ae9df06dc0e7'
baseline_revision: '168c657676ab2e210401bb5fe1c7ae9df06dc0e7'
tenants_baseline_commit: 'd5ce92881019d3deca20b5fe03b84f86489dd062'
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-4-context.md'
warnings: [oversized]
deferred: []
operator_actions:
  - 'Authenticate as a Hexalith.Tenants maintainer and approve the Story 4.7 producer-and-test scope against baseline d5ce92881019d3deca20b5fe03b84f86489dd062.'
  - 'Review, merge, and publish the approved Hexalith.Tenants correction, then provide its pull request and exact full commit SHA.'
  - 'Grant separate EventStore root authority to move the Hexalith.Tenants gitlink to the accepted published SHA after the source/package and persisted-path evidence passes.'
---

<intent-contract>

## Intent

**Problem:** Tenants query producers alias opaque state-store ETags to `ProjectionVersion` and derive authoritative freshness from producer-side age calculations even though all six routes are classified `HandlerComputed`. The public EventStore and Tenants consumers currently fail closed, but the producer contract remains misleading and its tests enshrine the alias.

**Approach:** Preserve the existing EventStore fail-safe boundary and record the complete producer/consumer inventory at the exact Tenants baseline. After separately authenticated Tenants-maintainer authorization, remove unsupported producer metadata, update the producer tests, and prove source/package behavior through the real gateway and persisted read models before any accepted Tenants SHA or root gitlink movement is claimed.

## Boundaries & Constraints

**Always:** Treat the Tenants checkout and root gitlink as read-only until authenticated Tenants-maintainer authority names the exact baseline and accepted scope. Keep all six handler routes `HandlerComputed`; expose `Unknown` lifecycle and no authoritative projection version, stale/degraded state, or ETag outside the opaque validator boundary. Bind completion evidence to exact full SHAs, dependency mode, commands, and persisted production-path observations. Preserve `sprint-status.yaml` byte-for-byte because the orchestrator owns it.

**Block If:** Stop implementation only if an approved change would require EventStore platform behavior, a new provenance design, or an observably different classification than `HandlerComputed`. Missing Tenants authority is an operator handoff: finish all EventStore-owned inventory and verification, commit it, then use `awaiting-operator`, never `blocked`.

**Never:** Do not edit Tenants source, tests, documentation, branches, commits, or pull requests without separate maintainer authorization. Do not infer `ProjectionBacked` from persisted storage access, `ProjectedAt`, HTTP success, mock metadata, or ETag. Do not parse or render an ETag as a projection version, reopen Stories 1.2/2.11, change EventStore runtime behavior, move the Tenants gitlink without an accepted published SHA and root authority, or write/revert `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Handler route with ETag only | Persisted row has an opaque ETag and no genuine projection version | Producer keeps no authoritative version/lifecycle metadata; public consumers receive `HandlerComputed`, `Unknown`, and no projection fields | Fail closed; retain ETag only inside the HTTP validator boundary |
| Tenant detail with genuine sequence | `TenantReadModel.ProjectionVersion` contains `tenant-sequence:<n>` | The handler route remains `HandlerComputed`; the genuine value is not publicly promoted under the current architecture | A new `ProjectionBacked` design requires separate approval |
| Missing or old projection timestamp | `ProjectedAt` is absent or beyond a freshness threshold | Producer does not fabricate `Current`, `Stale`, `Degraded`, or `Unavailable`; consumers render `Unknown` | Omit unsupported metadata without converting the query to an error |
| Conditional or synthetic metadata | A generated-controller/mock response claims projection metadata or a `304` carries a validator | Transport tests remain supporting evidence only; real route provenance governs the user-visible result | Strip unsupported metadata and never inherit lifecycle from a retained ETag |
| Unauthorized implementation attempt | No authenticated Tenants-maintainer approval names scope and baseline | EventStore-owned evidence is committed and the story becomes `awaiting-operator` | No Tenants or gitlink mutation occurs |

</intent-contract>

## Code Map

- `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs:18-54` -- central defect: the dormant overload maps ETag directly to `ProjectionVersion`; the active overload falls back from persisted version to ETag and creates age-derived metadata.
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/TenantQueryHandlerBase.cs:150-167` -- common result factory used by the six query handlers.
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/{ListTenants,GetUserTenants,GetTenant,GetTenantUsers,GetTenantAudit,GetGlobalAdministrators}QueryHandler.cs` -- generated REST producer surfaces; every result flows through the common factory.
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:121-123,236-247` -- only `TenantReadModel` currently persists a genuine ordered `tenant-sequence:<n>` version.
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Projections/{TenantIndexReadModel,TenantAuditReadModel,GlobalAdministratorReadModel}.cs` -- persisted timestamps exist, but these models carry no genuine ordered projection version.
- `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs:31-98` -- authoritative runtime classification: domain-handler results are stamped `HandlerComputed` with `Unknown` lifecycle.
- `_bmad-output/planning-artifacts/architecture.md:225-235` -- AD-15 forbids treating handler-computed output as projection-backed evidence.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs:185-202` and `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:315-377` -- existing gateway and client normalization strip unsupported fields.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:328-477` -- generated API emits version/lifecycle/stale headers only for permitted provenance.
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantsRestQueryClient.cs:321-402` -- typed client and UI normalize unsupported evidence to `Unknown`.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs:26-54` -- producer unit test currently enshrines ETag-as-version for five routes.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs:31-138` -- producer tests currently enshrine ETag fallback and timestamp-derived freshness across all six routes.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryResponseProvenanceE2ETests.cs:35-190` -- real gateway/persisted-state proof that `list-tenants` is `HandlerComputed` and leaks no projection metadata.
- `_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md` -- completed consumer boundary; synthetic `ProjectionBacked` tests do not authorize or prove the producer.

## Baseline Inventory And Authority Evidence

Inventory captured on 2026-08-27 against EventStore `168c657676ab2e210401bb5fe1c7ae9df06dc0e7` and the clean, detached Tenants checkout/root gitlink `d5ce92881019d3deca20b5fe03b84f86489dd062`. The locally recorded Tenants `origin/main` is `4d8b19a33f12a583a4f81deb406ff6f97f4f31af`; its availability is not acceptance evidence and does not authorize changing the checkout or gitlink.

| Route | Handler and primary persisted model | Baseline producer metadata | Public consumer impact |
|---|---|---|---|
| `list-tenants` | `ListTenantsQueryHandler`; `TenantIndexReadModel` at `projection:tenant-index:singleton` | Active freshness overload copies the opaque entry ETag, substitutes it for a missing projection version, and derives lifecycle/staleness from `ProjectedAt`; the model has no ordered version producer | EventStore exposes `HandlerComputed`/`Unknown` and strips ETag, version, and stale fields; generated API and Tenants client/UI therefore omit projection evidence and remain non-mutable |
| `get-user-tenants` | `GetUserTenantsQueryHandler`; the same tenant-index singleton | Same ETag fallback and timestamp-age path as `list-tenants` | Same fail-closed consumer result |
| `get-tenant` | `GetTenantQueryHandler`; `TenantReadModel` at `projection:tenants:<tenant-id>` | The read model may persist the genuine ordered `tenant-sequence:<n>`, otherwise the active overload falls back to ETag; `ProjectedAt` still fabricates route lifecycle/staleness | The genuine sequence remains persisted evidence only: the handler route is not promoted to `ProjectionBacked`, and the public fields are stripped |
| `get-tenant-users` | `GetTenantUsersQueryHandler`; the same per-tenant read model | Same genuine-sequence-or-ETag choice and timestamp-age path as `get-tenant` | Same fail-closed consumer result |
| `get-tenant-audit` | `GetTenantAuditQueryHandler`; `TenantAuditReadModel` at `audit:<tenant-id>` | Active overload copies the opaque audit ETag, substitutes it for the normally absent ordered version, and derives lifecycle/staleness from `ProjectedAt` | EventStore and Tenants consumers suppress the unsupported claims and render `Unknown` |
| `get-global-administrators` | `GetGlobalAdministratorsQueryHandler`; `GlobalAdministratorReadModel` at `projection:global-administrators:singleton` | Active overload copies the opaque entry ETag, substitutes it for the normally absent ordered version, and derives lifecycle/staleness from `ProjectedAt` | EventStore and Tenants consumers suppress the unsupported claims and render `Unknown` |

The two aliases are both centralized in `TenantQueryResult`: the dormant three-argument overload assigns `ProjectionVersion` directly from the normalized ETag, while the active freshness overload assigns `readModel?.ProjectionVersion ?? normalizedETag`. That active overload also calls `ToQueryResponseMetadata`, converting a producer-side age calculation into `Current`, `Stale`, or `Unknown` lifecycle and compatible `IsStale` values. All six handlers reach it through `TenantQueryHandlerBase.CreateSuccessResult`.

Consumer boundary inventory is unchanged and fail-safe. `HandlerAwareQueryRouter` route-binds handler results to `HandlerComputed` and normalizes lifecycle to `Unknown`; `QueriesController.NormalizeProducerMetadata` removes ETag, not-modified, stale, and projection-version claims for every non-`ProjectionBacked` result. `EventStoreGatewayClient` repeats that normalization across body/header mismatches. The generated REST controller emits projection version, ETag, and stale headers only for `ProjectionBacked`. `TenantsRestQueryClient` accepts ETag/version only for `ProjectionBacked`, normalizes unsupported lifecycle to `Unknown`, and the Story 2.11 UI gates keep mutations unavailable without projection-confirmed current evidence. Consequently the defect misleads direct/internal producer consumers and producer tests, but does not currently escape the public EventStore/Tenants consumer boundary.

The affected producer tests are `TenantQueryHandlerETagTests` (five-route ETag-as-version theory; it omits `get-global-administrators`) and `TenantQueryFreshnessTests` (ETag fallback, genuine sequence preference, missing timestamp, aged timestamp, and all-six-route age classification). `TenantsApiGeneratedControllerTests` currently proves synthetic `ProjectionBacked` responses and a synthetic `304`; this is transport support only and is not evidence of any actual Tenants handler route. `AspireTopologyTests` is the required home for authorized generated-API/gateway and persisted-end-state proof.

Tenants dependency mode is package-first: `UseHexalithProjectReferences=false`/`UseNuGetDeps=true` by default, including the EventStore verification below. An intentional source session sets `UseHexalithProjectReferences=true` (or `UseNuGetDeps=false`) and selects source only when each referenced project exists. Switching modes requires a fresh restore. The post-correction Debug/source plus Release/package restore/build/test matrix remains pending because no authorized Tenants correction exists to validate.

Authority is intentionally split: EventStore ownership covers this inventory and existing platform verification; authenticated Tenants-maintainer approval must bind the producer/test change scope to baseline `d5ce92881019d3deca20b5fe03b84f86489dd062`; the maintainer must merge/publish an accepted Tenants commit; and separate root authority must approve moving the root gitlink to that exact full SHA. The sprint orchestrator alone owns `sprint-status.yaml`. No authenticated Tenants approval, accepted commit/PR, or root gitlink authorization was supplied, so no Tenants file, branch, commit, PR, or gitlink was mutated and the story is `awaiting-operator` rather than complete.

## Tasks & Acceptance

**Execution:**

- [x] `_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md` -- record the six-route inventory, exact root/Tenants SHAs, consumer impact, dependency modes, and authority split before any producer edit.
- [x] `tests/Hexalith.EventStore.QueryRouting.Tests/HandlerAwareQueryRouterTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`, `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`, and `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryResponseProvenanceE2ETests.cs` -- run the narrow existing guards and persisted-path proof; do not alter platform behavior to accommodate Tenants.
- [ ] `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/TenantQueryResult.cs` -- only after authenticated Tenants authority, remove ETag fallback and producer-authored authoritative lifecycle/freshness for handler-computed routes while preserving opaque validator transport.
- [ ] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs` and `TenantQueryFreshnessTests.cs` -- replace alias-preserving expectations with all-six-route fail-closed assertions, including genuine persisted sequence, missing timestamp, old timestamp, and ETag-only cases.
- [ ] `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs` and `TenantsApiGeneratedControllerTests.cs` -- prove the actual generated API/gateway route is `HandlerComputed`, strips unsupported headers/body metadata, and cannot satisfy NFR16 from mocks or HTTP success; assert persisted end-state origin.
- [ ] `references/Hexalith.Tenants/Directory.Build.props` and affected project files (read-only configuration inputs) -- validate fresh Debug/source and Release/package restores/builds, then run Contracts, Server, Integration, and UI projects individually.
- [ ] `references/Hexalith.Tenants` gitlink -- after maintainer merge/publication and separate root authority, record the accepted Tenants commit and move only the root-declared gitlink; preserve the approved scope and full validation evidence.

**Acceptance Criteria:**

- Given the exact Tenants baseline `d5ce92881019d3deca20b5fe03b84f86489dd062`, when the inventory is reviewed, then all six query routes, their primary persisted models, both ETag/version aliases, timestamp freshness path, generated API, typed client, UI, source/package modes, and affected tests are named with consumer impact.
- Given any Tenants handler route, when producer and outer gateway metadata are observed, then provenance is `HandlerComputed`, lifecycle is `Unknown`, authoritative projection version/stale/degraded fields are absent, and ETag remains opaque and non-displayable.
- Given a persisted `tenant-sequence:<n>`, ETag-only row, absent timestamp, or aged timestamp, when the route executes, then none promotes the handler route to `ProjectionBacked` or fabricates lifecycle; genuine values remain persisted evidence for a future separately approved design.
- Given generated API, typed-client, and UI consumers, when unsupported or inconsistent metadata and `304` responses are received, then projection headers are omitted, retained lifecycle is not inferred, mutation gates remain unavailable, and the user-visible state is `Unknown`.
- Given source and package dependency modes, when the authorized Tenants correction is validated, then fresh restores/builds and individual focused/higher-tier projects pass, and a real gateway test inspects persisted read-model state rather than relying on mocks, compilation, or HTTP success.
- Given completion is requested, when authority and runtime identity are reviewed, then evidence names authenticated maintainer approval, accepted PR/commit and full Tenants SHA, approved scope, dependency modes, exact commands/results, persisted production-path proof, and separate gitlink authority; without it the story is `awaiting-operator` and no external completion is inferred.

## Spec Change Log

- 2026-08-27 -- Recorded the exact six-route producer/consumer inventory and authority split at EventStore `168c657676ab2e210401bb5fe1c7ae9df06dc0e7` / Tenants `d5ce92881019d3deca20b5fe03b84f86489dd062`; all EventStore-owned focused and persisted-path verification passed. No authenticated Tenants-maintainer or root-gitlink authority was supplied, so the protected external changes remain unchecked and status moved to `awaiting-operator` without touching `sprint-status.yaml`.

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

The checked-out Tenants baseline and root gitlink both identify `d5ce92881019d3deca20b5fe03b84f86489dd062`; the checkout is clean and detached. The locally recorded Tenants `origin/main` is six unrelated commits ahead, so neither branch position nor newer availability substitutes for accepted scope. Package mode is the default; `UseHexalithProjectReferences=true` is an intentional source session and requires a fresh restore.

Removing aliases at the producer is still valuable even though EventStore already strips them: internal tests and direct consumers must not encode metadata the runtime contract declares non-authoritative. Conversely, changing `HandlerAwareQueryRouter` to stamp these handlers `ProjectionBacked` would convert this external cleanup into a platform design change and is explicitly outside this story.

## Verification

**Commands:**

- `dotnet test tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj --configuration Release` -- expected: handler routes remain `HandlerComputed` and lifecycle/version claims fail closed.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Release` -- expected: gateway metadata normalization passes.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` followed by the built xUnit v3 assembly `-class Hexalith.EventStore.Server.Tests.Controllers.QueriesControllerTests` -- expected: controller strips unsupported producer fields.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Release` followed by the built xUnit v3 assembly `-class Hexalith.EventStore.IntegrationTests.ContractTests.QueryResponseProvenanceE2ETests` -- expected: when environment prerequisites are available, real gateway/persisted-state proof passes.
- After authorized Tenants edits, run the Story 2.12 fresh source/package command matrix and each affected Tenants test project individually -- expected: both modes build and all focused/higher-tier tests pass with exact retained results.
- `git diff --check` -- expected: no whitespace errors; `sprint-status.yaml` absent from the diff.

**Retained results (2026-08-27, EventStore `168c657676ab2e210401bb5fe1c7ae9df06dc0e7`, Tenants/package mode `d5ce92881019d3deca20b5fe03b84f86489dd062`):**

- `dotnet test tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj --configuration Release` -- passed 7/7; handler routing stayed `HandlerComputed` with `Unknown` lifecycle.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Release` -- passed 768/768; gateway body/header normalization remained fail-closed.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` -- succeeded with 0 warnings and 0 errors; `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Controllers.QueriesControllerTests` then passed 71/71.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Release` -- succeeded with 0 warnings and 0 errors; `dotnet tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll -class Hexalith.EventStore.IntegrationTests.ContractTests.QueryResponseProvenanceE2ETests` then passed 1/1. The live test executed `list-tenants` through the real EventStore gateway, observed `HandlerComputed`, no public ETag/version/stale claims, and inspected Redis key `admin:query-types:tenants` to prove the handler-query registration came from persisted DAPR state.
- Authorized Tenants source/package restores, builds, focused producer tests, generated-API integration proof, UI suites, accepted Tenants SHA, and gitlink movement were not attempted because their prerequisite authorities are absent.

**Manual checks (if no CLI):**

- Verify authority evidence is content-bound to the accepted Tenants SHA and scope before any Tenants or gitlink mutation.
- Verify no test treats synthetic `ProjectionBacked` metadata as proof of actual Tenants route provenance.
