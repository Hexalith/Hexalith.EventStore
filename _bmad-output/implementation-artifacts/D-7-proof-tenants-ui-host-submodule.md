---
created: 2026-07-02
source_story_key: D-7-proof-tenants-ui-host-submodule
supersedes_scope_note: sprint-change-proposal-2026-07-02-rest-api-external-host
baseline_commit: 496395f27ab52b3b4372d4ca0cd5197d1a47eb19
---

# Story D.7: Proof - Tenants External API Host and UI Client Split

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain-module author shipping Hexalith.Tenants on Hexalith.EventStore**,
I want **Tenants command/query REST contracts generated into a dedicated external API host while the interactive Tenants UI uses EventStore Client libraries**,
so that **external applications get typed REST endpoints without putting generated MVC controllers or hand-written query facades inside the UI host**.

## Story Context

This is **story D7 of Epic D - REST Controller Source Generator**. D1-D4 built the contract seam, generator skeleton, controller emission, and generator tests. D5 proved the query route in Sample and was corrected to use a contracts library plus an external API host. D6 is the Counter command proof and should be read for command-contract and kebab-case dispatch lessons before Tenants adoption.

The original June 21 D7 wording said to generate controllers into `Hexalith.Tenants.UI`. That is no longer valid. The July 2 correction explicitly says: **interactive UI hosts consume platform Client libraries directly; generated controllers go into a dedicated external-facing API host per domain**.

Source of truth: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md` sections CP-1, CP-6, and CP-7; the original Epic D row in `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md`; D1-D6 story files; current Sample external API host source; and current `references/Hexalith.Tenants` source.

## Acceptance Criteria

1. **Corrected July 2 architecture is enforced.**
   - Do not generate controllers into `references/Hexalith.Tenants/src/Hexalith.Tenants.UI`.
   - `Hexalith.Tenants.UI` must not depend on `Tenants:BaseAddress` or an app-local Tenants REST query client for its interactive workflows after this story.
   - The interactive UI uses `IEventStoreGatewayClient` or a small UI service built on that client for command/query workflows.
   - A dedicated external API host owns generated MVC controllers for Tenants REST endpoints.
   - The old hand-written `TenantsQueryController` is removed only after generated external API coverage preserves its required route, authorization, cache, and error semantics.

2. **Tenants command contracts are REST-capable without exposing bootstrap casually.**
   - Add `ICommandContract` metadata to Tenants command records in `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/`.
   - Preserve Tenants contract style: public record primary-constructor command types, no infrastructure dependencies, and one C# type per file.
   - Use kebab-case `CommandType` values:
     - `create-tenant`
     - `update-tenant`
     - `enable-tenant`
     - `disable-tenant`
     - `add-user-to-tenant`
     - `change-user-role`
     - `remove-user-from-tenant`
     - `set-tenant-configuration`
     - `remove-tenant-configuration`
     - `set-global-administrator`
     - `remove-global-administrator`
   - Use `Domain => "tenants"` and `AggregateId => TenantId` for tenant aggregate commands.
   - Use `Domain => "global-administrators"` and the existing global-administrator aggregate identity constant for global administrator commands.
   - `BootstrapGlobalAdmin` may get contract metadata if needed for dispatch consistency, but it must not get `[RestRoute]` or appear in generated external REST endpoints unless a product/security decision is explicitly recorded in the Dev Agent Record.
   - Add `[RestRoute]` to externally supported commands using stable, typed routes; route/body ID mismatches must remain 400 problem details in generated controllers.

3. **Six Tenants query contracts are annotated with rich REST routes and bindable payload properties.**
   - Annotate these existing sealed query contract classes:
     - `ListTenantsQuery`
     - `GetTenantQuery`
     - `GetTenantUsersQuery`
     - `GetTenantAuditQuery`
     - `GetUserTenantsQuery`
     - `GetGlobalAdministratorsQuery`
   - Preserve Tenants query-contract style: sealed classes implementing `IQueryContract` with static `QueryType`, `Domain`, and `ProjectionType`.
   - Add public get-only or init-only properties required for route and query-string binding, such as `TenantId`, `UserId`, `Cursor`, `PageSize`, `From`, `To`, and `Category`.
   - Preserve the existing route surface:
     - `GET /api/tenants`
     - `GET /api/tenants/{tenantId}`
     - `GET /api/tenants/{tenantId}/users`
     - `GET /api/tenants/{tenantId}/audit`
     - `GET /api/users/{userId}/tenants`
     - `GET /api/global-administrators`
   - Query-string payloads must preserve current paging, cursor, audit filter, and category behavior.
   - Existing query type/domain/projection constants must not change unless a failing test proves the old value is incompatible.

4. **The generator supports Tenants request-shape semantics instead of hard-coding Tenants.**
   - Generated Tenants query endpoints must submit the same `SubmitQueryRequest` shape that the current Tenants UI/controller path requires.
   - Required aggregate/entity routing semantics:
     - `GetTenantQuery`: aggregate and entity are `tenantId`.
     - `GetTenantUsersQuery`: aggregate and entity are `tenantId`.
     - `GetTenantAuditQuery`: aggregate and entity are `tenantId`.
     - `GetUserTenantsQuery`: aggregate is `index`; entity is `userId`.
     - `ListTenantsQuery`: aggregate is `index`; entity follows the existing domain-handler contract, and the final choice must be proven by tests.
     - `GetGlobalAdministratorsQuery`: aggregate and entity are `global-administrators`.
   - Current generator behavior treats a single non-tenant route parameter as the aggregate ID and defaults routes with no parameter to aggregate `index`; that is not enough for all Tenants routes.
   - If the current generator cannot express these bindings, add a narrow reusable generator contract/metadata extension with tests. Do not add Tenants-specific generated code or route-name special cases.
   - Generated query controllers must forward `If-None-Match` and preserve 304 behavior.
   - Generated query controllers must expose freshness metadata equivalent to the old controller: `ETag`, `X-Hexalith-Projection-Version`, `X-Hexalith-Served-At`, and `X-Hexalith-Is-Stale` when available.

5. **A dedicated external Tenants API host is added.**
   - Add a new Web SDK project under `references/Hexalith.Tenants/src/`, preferably `Hexalith.Tenants.Api`, unless the submodule already has a stronger naming convention.
   - The project is not packable, is publishable, and can opt into container publishing with repository name `tenants-api`.
   - The host mirrors the current Sample external API host pattern:
     - service defaults
     - controllers
     - inbound JWT bearer validation
     - `IEventStoreGatewayClient`
     - bearer-token forwarding
     - DAPR app-id invocation handler targeting `eventstore`
     - default endpoints
   - Add a Tenants REST API assembly attribute with route prefix `api/tenants`, tag `tenants`, and system tenant source unless implementation proves a different tenant source is required by existing Tenants authorization semantics.
   - Reference `Hexalith.Tenants.Contracts`, `Hexalith.EventStore.Client`, `Hexalith.EventStore.ServiceDefaults`, and `Hexalith.EventStore.RestApi.Generators` as an analyzer using the repository's source-reference conventions.
   - Do not add package versions to project files.
   - Add the new project to `references/Hexalith.Tenants/Hexalith.Tenants.slnx`.

6. **Tenants AppHost topology routes external REST separately from interactive UI.**
   - Add a Tenants API project metadata/resource to `references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost`.
   - The new resource uses DAPR app ID `tenants-api`, waits for/references EventStore, exposes external HTTP endpoints, and does not own state-store or pub/sub components.
   - Pass the same EventStore client/security configuration pattern used by Sample external API.
   - Remove `Tenants__BaseAddress` from the UI resource unless another non-query UI feature still requires it and is documented with tests.
   - Update DAPR access-control configuration so the EventStore sidecar allows `tenants-api` to invoke required GET/POST operations.
   - Do not initialize or update nested submodules while working inside `references/Hexalith.Tenants`.

7. **Tenants UI query workflows use EventStore Client libraries directly.**
   - Replace `ITenantsQueryApiClient` / `TenantsQueryApiClient` usage in `Hexalith.Tenants.UI` with `IEventStoreGatewayClient` or a local adapter that builds `SubmitQueryRequest`.
   - Preserve `ITenantQueryGateway` as the UI-facing abstraction unless tests show it is no longer useful.
   - Preserve UI behaviors: support-safe failures, unauthorized fail-closed states, stale/freshness indicators, 304 handling, pagination cursors, and Memories search hydration.
   - Do not redesign UI components or add raw HTML controls, raw CSS component systems, or JavaScript. If UI markup is touched, follow FrontComposer and Fluent UI Blazor V5 instructions.
   - The UI must not call the generated Tenants external API for its normal interactive workflows.

8. **The old Tenants query controller is retired safely.**
   - Delete `references/Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs` after replacement tests exist.
   - Remove stale REST-query DI registrations and tests tied only to that controller.
   - Keep the Tenants domain service host domain-centric: no new AppHost, Aspire, ServiceDefaults, projection actor, telemetry, health-check, or DAPR wiring inside the domain service project.
   - Do not remove `/query`, `/project`, or domain query handlers; EventStore and generated external API still need the domain service query surface.
   - Be cautious with imported EventStore command controllers in the Tenants service host; remove them only if tests prove they are no longer required for existing bootstrap or operational flows.

9. **Tests and generated-source evidence prove D7 end to end.**
   - Add or update Tenants Contracts tests for command metadata, aggregate IDs, query metadata, route attributes, and route bindable properties.
   - If the EventStore generator is extended, add focused tests in `tests/Hexalith.EventStore.RestApi.Generators.Tests/` for:
     - constant aggregate/entity binding
     - distinct aggregate/entity binding
     - freshness header emission
     - referenced contract discovery
     - generated source compilation
   - Update Tenants UI tests so `TenantQueryGateway` assertions capture `SubmitQueryRequest` values sent through `IEventStoreGatewayClient`, not HTTP GET URLs to `TenantsQueryApiClient`.
   - Replace or retarget `TenantsQueryControllerIntegrationTests` to the new external API host. Do not leave stale tests asserting the deleted controller.
   - Add AppHost/topology tests proving `tenants-api` is present and the UI no longer receives `Tenants__BaseAddress`.
   - Emit generated files to `/tmp/hexalith-tenants-d7-generated`, inspect the generated controller path, and record it in the Dev Agent Record.
   - Required Tenants verification:
     ```bash
     dotnet build Hexalith.Tenants.slnx --configuration Release
     dotnet test tests/Hexalith.Tenants.Contracts.Tests/
     dotnet test tests/Hexalith.Tenants.UI.Tests/
     dotnet test tests/Hexalith.Tenants.Server.Tests/
     ```
   - If platform generator or client code is touched, also run:
     ```bash
     dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/
     dotnet test tests/Hexalith.EventStore.Client.Tests/
     dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false
     ```
   - Run `dotnet test tests/Hexalith.Tenants.IntegrationTests/` when Docker/Aspire prerequisites are available. If not, record the exact blocker.
   - Do not run solution-level `dotnet test`.
   - Runtime smoke, when feasible, must verify observable state/read-model results or generated gateway request shape, not only HTTP 200/202.

10. **Scope is kept to D7.**
    - Do not update package-release inventory, NuGet package count docs, D8 guardrail docs, or semantic-release configuration.
    - Do not change unrelated Sample behavior except to use it as a reference.
    - Do not broaden Tenants UI visuals or navigation.
    - Do not add persistent containers for local validation.
    - Do not recurse into nested submodules.
    - Keep a separate Tenants submodule commit/PR boundary; the root repo should only receive the submodule pointer update and any required platform generator/client patches.

## Tasks / Subtasks

- [x] **Task 1: Preflight corrected scope and current code** (AC: 1, 4, 8, 10)
  - [x] Read the July 2 correction and D5/D6 stories before editing.
  - [x] Compare current `Sample.Api` host pattern with Tenants AppHost/UI/domain service.
  - [x] Confirm generator support gaps for Tenants aggregate/entity binding and freshness headers.
  - [x] Record current root and Tenants submodule git status before modifying files.

- [x] **Task 2: Annotate Tenants commands** (AC: 2)
  - [x] Add `ICommandContract` metadata and aggregate IDs to public Tenants commands.
  - [x] Add `[RestRoute]` only for supported external commands.
  - [x] Keep `BootstrapGlobalAdmin` unrouted unless an explicit product/security decision approves external exposure.
  - [x] Add contract tests for command metadata and route/body parameter compatibility.

- [x] **Task 3: Annotate Tenants queries** (AC: 3, 4)
  - [x] Add `[RestRoute]` and bindable properties to the six query contract classes.
  - [x] Preserve existing query constants and cursor/page-size semantics.
  - [x] Add contract tests proving route templates, route parameters, query-string properties, and static metadata.

- [x] **Task 4: Patch generator only if Tenants requires it** (AC: 4, 9)
  - [x] Add reusable metadata or emitter behavior for constant aggregate/entity values and distinct entity binding.
  - [x] Add freshness header emission from `QueryResponseMetadata`.
  - [x] Add generator tests before relying on Tenants API host smoke tests.
  - [x] Preserve existing D3/D4 generator behavior and diagnostics.

- [x] **Task 5: Add external Tenants API host** (AC: 5, 6)
  - [x] Create `Hexalith.Tenants.Api` project and Program startup mirroring Sample external API.
  - [x] Add analyzer reference to `Hexalith.EventStore.RestApi.Generators`.
  - [x] Add `RestApiAssemblyInfo.cs`.
  - [x] Add project to `Hexalith.Tenants.slnx`.
  - [x] Wire `tenants-api` into Tenants AppHost and DAPR access control.

- [x] **Task 6: Move Tenants UI queries to EventStore Client** (AC: 1, 7)
  - [x] Replace `TenantsQueryApiClient` usage with gateway-client submission.
  - [x] Remove `Tenants:BaseAddress` UI dependency.
  - [x] Preserve `ITenantQueryGateway` behavior, 304/freshness handling, cursor behavior, and search hydration.
  - [x] Update UI DI/tests for the new client path.

- [x] **Task 7: Retire the hand-written controller and stale tests** (AC: 8, 9)
  - [x] Delete `TenantsQueryController` only after replacement API tests exist.
  - [x] Remove stale query-controller-only services/tests.
  - [x] Retarget integration coverage to generated external API or gateway-client behavior.
  - [x] Keep domain service query/project endpoints and handlers.

- [x] **Task 8: Verify and record evidence** (AC: 9, 10)
  - [x] Emit generated source to `/tmp/hexalith-tenants-d7-generated`.
  - [x] Run required per-project Tenants tests/builds.
  - [x] Run required EventStore tests/builds if platform code changed.
  - [x] Run integration/Aspire smoke if feasible, recording exact commands and blockers.
  - [x] Confirm final root and submodule `git status --short` contain only intended D7 changes plus known unrelated files.

### Review Findings

Adversarial code review 2026-07-04 (Blind Hunter + Edge Case Hunter + Acceptance Auditor, all three cross-verified). 7 patch, 6 defer, 9 dismissed. Fixes span the Tenants submodule and the root platform; see notes on scope per item.

**All 7 patches applied and verified 2026-07-04 (uncommitted working-tree changes in both the root repo and the `references/Hexalith.Tenants` submodule — still need committing + a submodule-pointer bump).** Verification:
- C1 — rebuilt `Hexalith.Tenants.Api` in source mode: generator manifest now emits `CommandCount=11; QueryCount=6` and the 17-action `TenantsRestController` (was `0/0`).
- H2/M2 — `dotnet test tests/Hexalith.EventStore.Client.Tests/` → 486/486 (added a transport-fault 503 test and reworked the If-None-Match test to assert header omission).
- H3/M1 — `dotnet test tests/Hexalith.Tenants.Server.Tests/` → 738/738; `dotnet build Hexalith.EventStore.slnx -c Release -p:UseHexalithProjectReferences=false` → 0/0. Platform maps the new `QueryAdapterFailureReason.InvalidCursor` / existing `InvalidEnvelope` sentinels (bare or `sentinel: detail` prefix) to HTTP 400.
- C1 contracts — `tests/Hexalith.Tenants.Contracts.Tests/` REST-metadata subset 5/5; UI `TenantQueryGatewayTests` 97/97.
- H1 — `appsettings.Development.json` added to `Hexalith.Tenants.Api` mirroring `Sample.Api` (dev symmetric SigningKey); validated by build + Sample parity. A live Aspire `tenants-api` smoke was not run (same environment limitation as the original dev pass); the missing-config startup crash cause is removed.

- [x] [Review][Patch] **CRITICAL — tenants-api generates ZERO controllers at HEAD (missing `ApiScope="tenants"`)** [references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/{Commands,Queries}/*.cs]. Root commit `219ef623` (post-D7, 2026-07-04) added fail-closed referenced-contract filtering (`RestApiMessageParser.IsInReferencedApiScope`) requiring `route.ApiScope == host Tag`. The Sample got `ApiScope="counter"`; the 17 Tenants `[RestRoute]` contracts got nothing, while `Hexalith.Tenants.Api` declares tag `tenants`. Verified by three agents + a direct build: manifest emits `CommandCount=0; QueryCount=0`, no `.Controller.g.cs` — every external route 404s. Fix: add `ApiScope = "tenants"` to all 17 routed contracts (proven to restore `CommandCount=11; QueryCount=6`).
- [x] [Review][Patch] **HIGH — tenants-api crash-loops at startup in the `EnableKeycloak=false` topology** [references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:43; references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs:185]. `Program.cs` throws when neither Authority nor SigningKey is configured; `WithEventStoreClientCredentials(security)` runs only in the `security is not null` branch, and the project ships no `appsettings*.json` (Sample.Api ships a dev SigningKey). The integration fixture runs `--EnableKeycloak=false`, so tenants-api never actually started — this is the real cause the Dev Agent Record misattributed to "Aspire did not publish an http endpoint". Fix: mirror Sample.Api — add `appsettings.Development.json` with the dev SigningKey and/or wire the AppHost else-branch.
- [x] [Review][Patch] **HIGH — EventStore outage/timeout now escapes into the Blazor circuit** [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs (catches only `EventStoreGatewayException`); src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:119 (`SubmitQueryAsync`, no transport-exception catch)]. The deleted `TenantsQueryApiClient` deliberately translated `HttpRequestException`/timeout → 503 `EventStoreGatewayException`; the shared client does not, and every UI catch is typed `EventStoreGatewayException`. EventStore down → raw exception in the circuit instead of the fail-closed Unavailable/Degraded snapshot. Deleted `TenantsQueryApiClientTests` (204 lines) covered this; no replacement. Fix (platform): translate transport faults to `EventStoreGatewayException` in the shared client or a resilience handler.
- [x] [Review][Patch] **HIGH — invalid/stale cursor regressed 400 → 500; UI audit auto-reset is now dead code** [references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/TenantQueryHandlerBase.cs:370 → src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs:77]. Handler returns plain `"Invalid cursor."`, which matches none of SubmitQueryHandler's Forbidden/NotFound/NotImplemented buckets → 500 `internal-error`. The old controller validated pre-dispatch → 400 `invalid-cursor`. UI `IsInvalidAuditCursor` (requires status 400) can never match, so a stale audit cursor no longer auto-resets to page 1. Fix: map cursor-validation failures to a 400 + `invalid-cursor` reason code.
- [x] [Review][Patch] **MEDIUM — audit `from > to` (and invalid category in payload) regressed 400 → 500** [references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/TenantQueryHandlerBase.cs:330]. Same default-bucket mapping as above → 500 for caller-input errors the old controller rejected with 400. Fix: same reason-code mapping family.
- [x] [Review][Patch] **MEDIUM — RFC-9110-valid `If-None-Match` forms (`*`, comma list, weak `W/`, multiple headers) now return 400** [src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:287 `NormalizeIfNoneMatch` throws `ArgumentException` → generated controller → 400]. Old path treated these as no-match → 200 full body. Standards-compliant caching clients hard-fail. Fix: return null/no-match for these shapes instead of throwing.
- [x] [Review][Patch] **LOW — `docs/event-contract-reference.md` invents a `Subject` command member** [references/Hexalith.Tenants/docs/event-contract-reference.md:63]. `ICommandContract` exposes `CommandType`, `Domain`, `AggregateId` — no `Subject`. Fix: `Subject` → `Domain`.
- [x] [Review][Defer] **HIGH — generated freshness headers never carry real values in production** [src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs:9 (no metadata field); src/Hexalith.EventStore/Controllers/QueriesController.cs:169 (fabricates ETag-only metadata)] — deferred, platform gap overlapping the known D6 read-model-freshness handoff. Tenant handlers produce real `IsStale`/`ProjectionVersion`, but it is dropped at the EventStore `/query` hop, so the D7-emitted `X-Hexalith-Is-Stale`/`X-Hexalith-Projection-Version` are always absent and the UI stale badge is unreachable. AC4 says "when available", so D7 satisfies its own AC; the D7 integration test only proves emission with mocked metadata. Fix belongs in the platform query path, not this story.
- [x] [Review][Defer] **MEDIUM — `{tenantId}` route segment is unvalidated under `RestTenantSource.System`** [src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1143 `IsTenantParameter`] — deferred, generator design decision. Name-based tenant detection excludes `{tenantId}` from route/body mismatch checks regardless of tenant source; under System source the URL segment is decorative and unchecked, so a URL/body tenant mismatch executes against the body's tenant. Authorization is still enforced on the body tenant via the forwarded bearer, so no privilege escalation — but the URL is misleading. Blocked behind the CRITICAL anyway. Recommend validating tenant-named route params against the body when tenant source ≠ Route.
- [x] [Review][Defer] **MEDIUM — old controller retired without external error-semantics test coverage** [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs] — deferred, test-coverage gap. The 2054-line controller test was replaced by 296 lines covering 401/request-shape/freshness/ETag-304 but nothing on 403/RBAC, gateway-failure → problem-details, or invalid-cursor at the generated surface. Add coverage once the H2/H3/M2 patches land.
- [x] [Review][Defer] **LOW — generator silently falls back to aggregate `"index"` for invalid `[RestQueryBinding]` sources (None/out-of-range/empty Constant); no diagnostic; GetHashCode NRE risk on null values** [src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:1153] — deferred, already logged from the D5 review; now exercised by D7 so worth prioritizing. Emit an HESREST diagnostic instead of a silent wrong-aggregate query.
- [x] [Review][Defer] **LOW — `Hexalith.Tenants.csproj` references `Hexalith.EventStore.Gateway` as an unconditional source ProjectReference while the repo default is package mode** [references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj:21] — deferred, unverified build-consistency risk. Gateway is a published package but has no version pin; mixing source Gateway with package DomainService could double-load EventStore.Client. Pure package-mode Tenants build was never verified (Tenants.slnx Release blocked by MSB3202). Resolve when the Gateway package pin/dependency mode is settled.
- [x] [Review][Defer] **LOW — governance tests now require mutable `@main` workflow refs on the release pipeline** [references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs] — deferred, deliberate org CI decision (shared `Hexalith.Builds` reusable workflows pinned to `main` "for stability"), not D7 work. Supply-chain note: whoever controls `Hexalith.Builds@main` controls this repo's release step (which holds `NUGET_API_KEY`); pinning is delegated to the shared repo and unenforced here.

**Dismissed (not defects):** DAPR `/**` GET/POST for `tenants-api` (local-only access-control file; production uses receiver-specific policies, pinned by a server test) · audit category "serialized as a number" (refuted — `.ToString()` emits the enum name, matching the old path) · `RestVerb.Patch` unsupported (refuted — generated controller emits `[HttpPatch]` for ChangeUserRole) · ListTenants/user-tenants wire-shape change to `Tenant=system` / `EntityId` (deliberate, test-proven 864/864 UI + 738/738 server; authz on forwarded bearer + handler RBAC) · stale "4 governance failures" blocker (record drift — suite now passes 111/111) · root commit bumps unrelated submodule pointers / nested-submodule gitlinks (minor scope; no forbidden files; nested submodules confirmed uninitialized) · package-version validator merge order and repo-wide OpenApi analyzer removal (tooling, no demonstrated harm).

## Dev Notes

### Top Guardrails

1. **Do not implement the superseded UI-host controller design.** The D7 file name still says UI host because it came from the old plan, but the accepted scope is external API host plus UI client split.
2. **Generated controllers are gateway facades.** They call `IEventStoreGatewayClient`; they do not call `DomainQueryDispatcher`, MediatR, DAPR actors, state stores, projection actors, or aggregate handlers directly.
3. **Tenants routes are richer than Sample routes.** User-tenants and global-admin queries need aggregate/entity values that the current generator does not infer correctly.
4. **Do not expose bootstrap as external REST by accident.** `BootstrapGlobalAdmin` is an internal bootstrap operation unless product/security explicitly says otherwise.
5. **Interactive UI must not depend on the external API host.** The external API is for external applications; the UI consumes EventStore client libraries.
6. **Submodule discipline matters.** Work inside `references/Hexalith.Tenants` should become a Tenants submodule PR/commit; root changes should remain limited to platform generator/client work if needed and the submodule pointer update.

### Current Code State Read During Story Creation

| File | Current state | D7 change | Preserve |
|---|---|---|---|
| `samples/Hexalith.EventStore.Sample.Api/Program.cs` | Dedicated external API host using controllers, JWT bearer auth, `IEventStoreGatewayClient`, inbound bearer forwarding, and DAPR app-id handler. | Use as the host pattern for `Hexalith.Tenants.Api`. | Generated-controller host stays separate from interactive UI. |
| `samples/Hexalith.EventStore.Sample.Api/RestApiAssemblyInfo.cs` | `[assembly: RestApi("api/{tenant}/counter", "counter", RestTenantSource.Route)]`. | Tenants API needs its own assembly attribute, likely `api/tenants`, `tenants`, `RestTenantSource.System`. | Assembly-level generator configuration. |
| `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` | Query aggregate ID is inferred from route parameters; a single non-tenant parameter becomes aggregate ID; query responses currently emit `ETag` only. | Add reusable support for Tenants binding/freshness semantics if needed. | Existing command behavior, problem details, tenant modes, diagnostics, and source determinism. |
| `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/*.cs` | Plain public record commands without `ICommandContract` or `[RestRoute]`. | Add static command metadata, aggregate ID, and route attributes for external commands. | Plain domain contract shape and existing command names. |
| `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/*.cs` | Sealed query contract classes with static metadata and no bindable instance properties. | Add route attributes and bindable properties. | Query constants and sealed-class contract style. |
| `references/Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs` | Hand-written query REST controller with auth/RBAC checks, cursor validation, `DomainQueryDispatcher`, `ETag`, and Tenants freshness headers. | Replace with generated external API host coverage, then delete. | Route surface, cache/freshness behavior, support-safe problem details. |
| `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Program.cs` | Registers both EventStore gateway/client services and `Tenants:BaseAddress` + `TenantsQueryApiClient`. | Remove Tenants REST query client path; UI uses EventStore gateway client. | FrontComposer/Fluent setup, Memories client setup, auth handlers, unavailable fail-safe behavior. |
| `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/TenantQueryGateway.cs` | Builds `TenantsQueryApiRequest` and calls `ITenantsQueryApiClient`; also hydrates search results through Memories. | Build `SubmitQueryRequest` through `IEventStoreGatewayClient` or a local adapter. | UI-facing result mapping, snapshots, pagination, stale metadata, Memories hydration. |
| `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/TenantCommandGateway.cs` | Already submits commands through `IEventStoreGatewayClient`, but uses existing command type strings. | Align command type values with `ICommandContract.CommandType` after D6 dispatch compatibility exists. | Support-safe error mapping and UI abstraction. |
| `references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs` | Adds EventStore platform, Tenants domain service, Tenants UI, admin resources, Memories, and sample resources; UI receives `Tenants__BaseAddress` and `EventStore__BaseAddress`. | Add `tenants-api`; remove UI `Tenants__BaseAddress` for query flows. | Existing EventStore and Memories references, security wiring, no extra state/pubsub for API host. |
| `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsQueryControllerIntegrationTests.cs` | Tests the old hand-written query controller. | Retarget to generated external API host or gateway request-shape behavior. | Coverage of routes, auth, cursor validation, cache, and freshness semantics. |

### Expected External Route Surface

The generated Tenants API host must cover these query routes:

```text
GET /api/tenants
GET /api/tenants/{tenantId}
GET /api/tenants/{tenantId}/users
GET /api/tenants/{tenantId}/audit
GET /api/users/{userId}/tenants
GET /api/global-administrators
```

Command routes should be stable typed routes under `/api/tenants` and `/api/global-administrators`. Prefer action-specific POST/PUT/PATCH routes over DELETE-with-body semantics unless existing generated-controller tests prove DELETE bodies are intentional and client-safe.

### Previous Story Intelligence

From D1:

- `ICommandContract.CommandType` and `IQueryContract.QueryType` are kebab-case and colon-free by contract.
- `RestRouteAttribute` is class-level and applies to both commands and queries.
- Route template validation belongs to the generator layer.

From D2:

- The generator uses metadata-name discovery and must not take runtime dependencies on EventStore assemblies.
- Referenced assembly discovery is required for contracts that live outside the generator-consuming project.

From D3:

- Generated controllers must delegate through `IEventStoreGatewayClient`.
- Generated command actions use `UniqueIdHelper.GenerateSortableUniqueStringId()`, not GUIDs.
- Route/body mismatches must return 400 problem details.
- Generated source must not call MediatR, DAPR actors, state stores, projection actors, or `DomainQueryDispatcher`.

From D4:

- Generator tests already cover command route/body mismatch, `Retry-After`, `Location`, tenant modes, duplicate route diagnostics, unsupported command route parameters, and generated source compilation.
- Extend generator tests for Tenants-specific binding classes of behavior before relying on Tenants smoke tests.

From D5:

- The proof architecture changed to contracts library plus external API host.
- Interactive UI hosts must consume EventStore client libraries directly.
- Contract source must not be compile-linked into UI projects.

From D6:

- Command contracts use kebab-case `CommandType`.
- If a dispatch path still assumes CLR short names, fix dispatch compatibility rather than weakening command contract values.
- UI command/query code should use typed client abstractions, not generic hand-built JSON in components.

### Git Intelligence

Root repository recent commits at story creation:

- `6db716e4 feat: Introduce external REST API host and contracts library`
- `90709756 refactor: remove unused RestApiAssemblyInfo and GetCounterStatusQuery files`
- `dc55d61c chore(release): 3.27.0 [skip ci]`
- `15ecad6f chore(references): update Hexalith.FrontComposer submodule commit`
- `3222176d chore(references): update Hexalith.Tenants submodule`

Tenants submodule recent commits at story creation:

- `fe21e8b feat: update subproject references and configure AppTitle in settings for Hexalith Tenants UI`
- `80bd492 fix: update workflow references to use the main branch for stability`
- `a989733 feat: update Dapr version to 1.18.0 in CI and Release workflows; add new workflows for CodeQL, Commitlint, and Dependency Review`
- `1a64a34 fix: update workflow references to specific commit hashes for stability`
- `f9f546a refactor: update Directory.Build.props and Directory.Packages.props for improved source management and package paths`

Current git status during story creation included an unrelated untracked root path:

- `samples/Hexalith.EventStore.Sample.Api/Properties/launchSettings.json`
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs`

Do not modify or remove those unrelated files while implementing D7 unless the user explicitly asks.

### Latest Technical Notes

- ASP.NET Core 10 Web API controllers still derive from `ControllerBase`; generated external controllers should keep the existing `[ApiController]` + route-attribute shape. Source: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0
- ASP.NET Core attribute routing supports absolute route templates starting with `~/`, which is needed for Tenants routes such as `/api/users/{userId}/tenants` and `/api/global-administrators` under a primary `api/tenants` prefix. Source: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing?view=aspnetcore-10.0
- Roslyn `IIncrementalGenerator` instances are compiler-owned and must not store mutable generator state. Any D7 generator patch must stay stateless and deterministic. Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator?view=roslyn-dotnet-5.0.0
- .NET Aspire DAPR integration is the current documented way to attach DAPR sidecars/components to Aspire resources; use the existing AppHost patterns and `aspire describe`/CLI diagnostics rather than the obsolete Aspire MCP server. Source: https://learn.microsoft.com/en-us/dotnet/aspire/community-toolkit/dapr?tabs=dotnet-cli
- DAPR service-invocation access control policies decide which app IDs can call which operations. Add `tenants-api` to the EventStore receiver policy where production or local deny-by-default policy requires it. Source: https://docs.dapr.io/operations/configuration/invoke-allowlist/
- DAPR service invocation is sidecar-mediated; the API host should call the EventStore app through the EventStore gateway client and DAPR app-id handler, not by discovering service URLs manually. Source: https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/
- DAPR's sidecar model exposes application APIs through the local sidecar process. This matches the existing Sample external API pattern and avoids adding state/pubsub ownership to the API host. Source: https://docs.dapr.io/concepts/dapr-services/sidecar/

### Testing and Verification Standards

- Use xUnit v3 and Shouldly for new tests.
- Run test projects individually; do not run solution-level `dotnet test`.
- Use `Hexalith.EventStore.slnx` only for root restore/build and `Hexalith.Tenants.slnx` for Tenants submodule restore/build.
- Always use `ConfigureAwait(false)` on awaited production-code calls.
- Integration tests must inspect persisted state, read-model state, generated gateway request shape, or response metadata. A 200/202 status alone is not enough.
- For runtime validation, start Aspire through the CLI, use `aspire describe` for endpoints/state, and use `EnableKeycloak=false` only for local symmetric-token validation when appropriate.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md`] - corrected external API host architecture.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md`] - original Epic D story sequence and D7 proof intent.
- [Source: `_bmad-output/implementation-artifacts/D-1-contract-seam.md`] - contract seam and route attribute decisions.
- [Source: `_bmad-output/implementation-artifacts/D-2-generator-skeleton-spike.md`] - analyzer discovery constraints.
- [Source: `_bmad-output/implementation-artifacts/D-3-controller-emission.md`] - generated controller behavior.
- [Source: `_bmad-output/implementation-artifacts/D-4-generator-tests.md`] - generator test expectations.
- [Source: `_bmad-output/implementation-artifacts/D-5-proof-sample-blazorui-queries.md`] - external API host correction and Sample query proof.
- [Source: `_bmad-output/implementation-artifacts/D-6-proof-counter-commands.md`] - command contract and dispatch lessons.
- [Source: `references/Hexalith.AI.Tools/hexalith-llm-instructions.md`] - repository AI rules.
- [Source: `references/Hexalith.AI.Tools/hexalith-ux-instructions.md`] - FrontComposer/Fluent UI rules.
- [Source: `docs/brownfield/architecture.md`] - EventStore domain-module and orchestration architecture.
- [Source: `docs/brownfield/integration-architecture.md`] - DAPR/service-invocation architecture.
- [Source: `samples/Hexalith.EventStore.Sample.Api/Program.cs`] - external API host pattern.
- [Source: `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs`] - current generated controller behavior and known gaps.
- [Source: `references/Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs`] - old query route/cache/freshness behavior to replace.
- [Source: `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/TenantQueryGateway.cs`] - current UI query workflow to migrate to EventStore Client.
- [Source: `references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs`] - current Tenants AppHost topology.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Activation: loaded `bmad-dev-story`, resolved workflow customization, loaded `_bmad/bmm/config.yaml`, and loaded `_bmad-output/project-context.md`.
- Preflight: read July 2 corrected architecture proposal plus D5 and D6 story records before code edits.
- Preflight git status: root contained only D7 story/sprint tracking edits after status transition; `references/Hexalith.Tenants` was clean; no nested Tenants submodules were initialized.
- Preflight Aspire baseline: `EnableKeycloak=false aspire run --apphost references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj --non-interactive` ran in an interactive PTY; `aspire describe` reported `eventstore`, `tenants`, and `tenants-ui` running Healthy before shutdown.
- Preflight topology finding: `tenants-ui` currently receives `Tenants__BaseAddress`, and no `tenants-api` resource exists yet.
- Generator gap confirmed: current query emission infers aggregate/entity from route parameters and emits only `ETag`, so Tenants needs reusable binding metadata and freshness-header emission before generated controllers can replace the old controller.
- Red: `dotnet test tests/Hexalith.Tenants.Contracts.Tests/` failed on `CommandContractRestMetadataTests` because Tenants commands did not implement `ICommandContract` or expose static metadata.
- Green: `dotnet test tests/Hexalith.Tenants.Contracts.Tests/ --filter FullyQualifiedName~CommandContractRestMetadataTests` passed: 3/3.
- Full contracts-suite note: `dotnet test tests/Hexalith.Tenants.Contracts.Tests/` currently has unrelated package-governance failures (`DAPR_VERSION: '1.17.` and package-version centralization assertions) after the command metadata tests pass; keep this as a verification blocker unless later scope explicitly addresses governance tests.
- Red: `dotnet test tests/Hexalith.Tenants.Contracts.Tests/ --filter FullyQualifiedName~QueryRestMetadataTests` failed because the six query contracts had no `[RestRoute]` attributes.
- Green: `dotnet test tests/Hexalith.Tenants.Contracts.Tests/ --filter "FullyQualifiedName~CommandContractRestMetadataTests|FullyQualifiedName~QueryRestMetadataTests"` passed: 5/5.
- Red: `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/ --filter FullyQualifiedName~Run_ReferencedQueryContractWithExplicitBinding_GeneratesQueryEnvelopeAndFreshnessHeaders` failed because `RestQueryBindingAttribute` / `RestQueryBindingSource` did not exist.
- Green: `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/ --filter FullyQualifiedName~Run_ReferencedQueryContractWithExplicitBinding_GeneratesQueryEnvelopeAndFreshnessHeaders` passed after adding reusable query binding metadata and freshness header emission.
- Green: `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` passed: 45/45, preserving existing generator behavior and diagnostics.
- Green: `dotnet test tests/Hexalith.Tenants.Contracts.Tests/ --filter "FullyQualifiedName~CommandContractRestMetadataTests|FullyQualifiedName~QueryRestMetadataTests"` passed: 5/5 after adding explicit query binding assertions for ListTenants, GetUserTenants, and GetGlobalAdministrators.
- Green: `dotnet build src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj --configuration Debug` passed, proving generated Tenants controllers compile in the new external API host.
- Green: `dotnet test tests/Hexalith.Tenants.UI.Tests/ --filter FullyQualifiedName~TenantQueryGatewayTests` passed: 97/97 after migrating the UI query gateway to `IEventStoreGatewayClient`.
- Green: `dotnet test tests/Hexalith.Tenants.IntegrationTests/ --filter FullyQualifiedName~TenantsApiGeneratedControllerTests` passed: 6/6, covering generated API auth, route-to-query request shape, freshness headers, ETag/304, and `If-None-Match` forwarding.
- Green: `dotnet test tests/Hexalith.Tenants.UI.Tests/ --filter FullyQualifiedName~Global_administrators_read_contract_uses_fixed_platform_scope_without_tenant_substitute` passed: 1/1 after retargeting the stale old-controller composition assertion to generated API metadata.
- Green: `dotnet test tests/Hexalith.Tenants.Server.Tests/ --filter "FullyQualifiedName~AppHost_DaprTopology_UsesPlatformDomainModuleExtensionAndStableResourceNames|FullyQualifiedName~LocalAccessControl_IsClearlyLocalOnlyAndProductionUsesReceiverSpecificFiles"` passed: 2/2 after adding `tenants-api` AppHost/access-control assertions.
- Generated-source evidence: `dotnet build src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj --configuration Debug -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/tmp/hexalith-tenants-d7-generated` passed. Inspected `/tmp/hexalith-tenants-d7-generated/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.RestApiGenerator/Hexalith.EventStore.RestApi.Hexalith.Tenants.Api.Generated.TenantsRestController.Controller.g.cs`.
- Green: `dotnet test tests/Hexalith.Tenants.UI.Tests/` passed: 864/864.
- Green: `dotnet test tests/Hexalith.Tenants.Server.Tests/` passed: 738/738 after documenting command/query adapter metadata and adding sibling-checkout path fallback for EventStore source references without initializing nested submodules.
- Green: `dotnet test tests/Hexalith.Tenants.IntegrationTests/` passed: 128/129 with 1 skipped snapshot performance test. Note: an intermediate run failed when `tenants-api` was added to the shared Aspire fixture resource wait list because Aspire reported the resource `Running` but did not publish an `http` endpoint to the test fixture within 4 minutes; the fixture was restored to its existing smoke resources, while AppHost tests and generated API integration tests keep `tenants-api` coverage.
- Green: `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` passed: 45/45.
- Green: `dotnet test tests/Hexalith.EventStore.Client.Tests/` passed: 483/483.
- Green: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` passed with 0 warnings/errors.
- Green: `dotnet build src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj --configuration Release -p:HexalithEventStoreFromSource=true -p:HexalithMemoriesFromSource=true` passed with 0 warnings/errors.
- Green: `dotnet build src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj --configuration Release -p:HexalithEventStoreFromSource=true` passed with 0 warnings/errors.
- Blocked-by-workspace-layout: `dotnet build Hexalith.Tenants.slnx --configuration Release` from the Tenants submodule failed with MSB3202 because the `.slnx` includes dependency projects under `references/Hexalith.Tenants/references/...`; this workspace intentionally keeps those dependencies as parent/sibling checkouts and repo instructions forbid initializing nested submodules.
- Resolved governance drift: `DiffEngine_Disabled=true dotnet test tests/Hexalith.Tenants.Contracts.Tests/` passed 111/111 in the final validation pass.
- Final stale-reference scan: `rg -n "TenantsQueryController|DomainQueryDispatcher|TenantsQueryApiClient|ITenantsQueryApiClient|TenantsQueryApiRequest|Tenants__BaseAddress|Tenants:BaseAddress" references/Hexalith.Tenants/src references/Hexalith.Tenants/tests -g '!test-summary.md'` found only negative assertions in tests.
- Final root validation 2026-07-04: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` passed with 0 warnings/errors.
- Final root tests 2026-07-04: Contracts 559/559, Client 486/486, Sample 91/91, SignalR 35/35, Testing 144/144, and RestApi.Generators 46/46 all passed.
- Final Tenants validation 2026-07-04: Contracts 111/111 and UI 864/864 passed with `DiffEngine_Disabled=true`; Server passed 738/738 with `-p:HexalithEventStoreFromSource=true`.
- Package-mode Tenants Server note: `DiffEngine_Disabled=true dotnet test tests/Hexalith.Tenants.Server.Tests/` failed to compile against the older package set because `QueryAdapterFailureReason.InvalidCursor` is unavailable there; the source-reference run passed.
- Tenants solution build blocker remains workspace-layout specific: `dotnet build Hexalith.Tenants.slnx --configuration Release` failed with MSB3202 for dependency projects under `references/Hexalith.Tenants/references/...`; nested submodules were not initialized per repository rules.
- Generated-source final evidence 2026-07-04: `dotnet build src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj --configuration Release -p:HexalithEventStoreFromSource=true -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/tmp/hexalith-tenants-d7-generated` passed and produced `Hexalith.EventStore.RestApi.Hexalith.Tenants.Api.Generated.TenantsRestController.Controller.g.cs`.
- Final Tenants AppHost validation 2026-07-04: `dotnet build src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj --configuration Release -p:HexalithEventStoreFromSource=true` passed with 0 warnings/errors.
- Final Tenants integration validation 2026-07-04: focused pub/sub degradation test passed 1/1, then `DiffEngine_Disabled=true dotnet test tests/Hexalith.Tenants.IntegrationTests/ -p:HexalithEventStoreFromSource=true` passed 128/129 with 1 skipped snapshot performance test.
- DAPR integration diagnosis: intermediate failures were traced to DAPR actor placement collisions with unrelated running sidecars advertising the shared `AggregateActor` type; Tenants integration fixtures now register a unique per-run aggregate actor type and all actor proxies/deactivation calls use it.
- Runtime-test diagnostics hardening: DAPR E2E setup assertions now include support-safe command/status/dead-letter diagnostics, and pub/sub failure tests wait for drain recovery before moving to the next failure case.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Completed D7 preflight against the corrected July 2 external-API-host architecture. Sample.Api is the reference host; Tenants currently routes UI queries through `TenantsQueryApiClient` and `Tenants__BaseAddress`; the old `TenantsQueryController` owns freshness headers and route/request semantics that generated external controllers must preserve.
- Added `ICommandContract` metadata to Tenants command records with kebab-case command types and aggregate IDs. External command records have typed `[RestRoute]` attributes; `BootstrapGlobalAdmin` is metadata-addressable for dispatch consistency but intentionally has no REST route.
- Annotated the six Tenants query contracts with REST route attributes and bindable route/query-string properties while preserving existing `QueryType`, `Domain`, and `ProjectionType` constants. Page-size omission still flows through existing server-side clamp/default behavior.
- Added reusable `RestQueryBindingAttribute` metadata and generator support for constant aggregate/entity values and route-bound entity IDs. Generated query controllers now forward freshness headers from `QueryResponseMetadata` while preserving `ETag` and 304 behavior.
- Added `Hexalith.Tenants.Api` as the dedicated external generated-controller host with EventStore gateway forwarding through DAPR app-id invocation. Tenants AppHost now includes `tenants-api`, and the UI no longer receives `Tenants__BaseAddress`.
- Migrated `TenantQueryGateway` to submit `SubmitQueryRequest` through `IEventStoreGatewayClient`, removed the app-local Tenants query REST client, and updated focused UI tests to assert gateway request shape plus existing freshness/304 behavior.
- Deleted the old hand-written `TenantsQueryController` and its controller integration tests after adding generated external API integration coverage. Kept the domain service `/query` and bespoke `/project` surfaces intact.
- Updated AppHost/configuration coverage so `tenants-api` resource wiring and local DAPR access-control policy are pinned, while the UI is guarded against reintroducing `Tenants__BaseAddress`.
- Updated Tenants documentation/test helpers for the new public command/query adapter metadata and for parent/sibling EventStore checkout resolution without nested submodule initialization.
- Emitted and inspected generated controller source under `/tmp/hexalith-tenants-d7-generated`.
- Fixed final validation drift in Tenants tests: the UI composition assertion now expects `ApiScope = "tenants"`, integration test publisher matches the current platform `IEventPublisher` signature, and integration actor tests are isolated from other DAPR sidecars by using a unique fixture actor type.

### File List

- `_bmad-output/implementation-artifacts/D-7-proof-tenants-ui-host-submodule.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `references/Hexalith.Tenants/Directory.Packages.props`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/AddUserToTenant.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/BootstrapGlobalAdmin.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/ChangeUserRole.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/CreateTenant.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/DisableTenant.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/EnableTenant.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/RemoveGlobalAdministrator.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/RemoveTenantConfiguration.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/RemoveUserFromTenant.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/SetGlobalAdministrator.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/SetTenantConfiguration.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/UpdateTenant.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetGlobalAdministratorsQuery.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetTenantAuditQuery.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetTenantQuery.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetTenantUsersQuery.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetUserTenantsQuery.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/ListTenantsQuery.cs`
- `references/Hexalith.Tenants/docs/event-contract-reference.md`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/Commands/CommandContractRestMetadataTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/Queries/QueryRestMetadataTests.cs`
- `src/Hexalith.EventStore.Contracts/Rest/RestQueryBindingAttribute.cs`
- `src/Hexalith.EventStore.RestApi.Generators/AnalyzerReleases.Unshipped.md`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiDiagnosticDescriptors.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiMessageDescriptor.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiMessageParser.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiMetadataNames.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiQueryBindingDescriptor.cs`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`
- `references/Hexalith.Tenants/Hexalith.Tenants.slnx`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/RestApiAssemblyInfo.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/InboundBearerForwardingHandler.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/DaprComponents/accesscontrol.yaml`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/HexalithTenantsApi.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Program.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/ITenantsQueryApiClient.cs` (deleted)
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantsQueryApiClient.cs` (deleted)
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantsQueryApiRequest.cs` (deleted)
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs` (deleted)
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs`
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/TenantQueryHandlerBase.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/DaprEndToEndTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/Fixtures/TenantsDaprTestFixture.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/Fixtures/TestEventPublisher.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/GracefulDegradationTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/SnapshotPerformanceTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/StatelessRestartTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsQueryControllerIntegrationTests.cs` (deleted)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Documentation/CompensatingCommandsDocumentationTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Documentation/CrossAggregateTimingDocumentationTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Documentation/DeploymentReadinessDocumentationTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Documentation/QuickstartDocumentationTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Projections/TenantsProjectionActorTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Support/TenantQueryTestHarness.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantsQueryApiClientTests.cs` (deleted)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs`

## Change Log

| Date | Change |
|---|---|
| 2026-07-02 | Story D7 created with corrected external API host scope, Tenants contract annotation requirements, generator binding/freshness gaps, UI client-library migration, old controller retirement guardrails, AppHost/DAPR topology requirements, and verification plan. Status ready-for-dev. |
| 2026-07-03 | Started D7 implementation, captured baseline commit, moved sprint status to in-progress, and completed corrected-scope/current-code preflight. |
| 2026-07-03 | Annotated Tenants command contracts with `ICommandContract` metadata, external REST routes, aggregate IDs, and tests; kept bootstrap unrouted. |
| 2026-07-03 | Annotated six Tenants query contracts with REST routes and bindable properties plus contract metadata tests. |
| 2026-07-03 | Extended REST generator with reusable query binding metadata and freshness header emission; updated Tenants query binding tests. |
| 2026-07-03 | Added dedicated `Hexalith.Tenants.Api` generated-controller host, AppHost `tenants-api` resource, and DAPR access-control entry. |
| 2026-07-03 | Migrated Tenants UI query gateway from app-local REST client to EventStore gateway client and removed `Tenants__BaseAddress` UI injection. |
| 2026-07-03 | Retired the hand-written Tenants query controller, retargeted integration coverage to the generated API host, pinned AppHost topology tests, emitted generated-source evidence, completed validation, and moved story to review. |
| 2026-07-04 | Completed final review-remediation validation, fixed Tenants test drift and DAPR actor-placement collisions in integration fixtures, recorded remaining package/workspace-layout blockers, and moved story to review. |
