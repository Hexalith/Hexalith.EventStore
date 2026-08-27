---
title: 'Tenants Query Provenance Follow-Up'
type: 'bugfix'
created: '2026-08-27'
status: 'awaiting-operator'
review_loop_iteration: 0
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
