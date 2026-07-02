---
created: 2026-07-02
source_story_key: D-7-proof-tenants-ui-host-submodule
supersedes_scope_note: sprint-change-proposal-2026-07-02-rest-api-external-host
---

# Story D.7: Proof - Tenants External API Host and UI Client Split

Status: ready-for-dev

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

- [ ] **Task 1: Preflight corrected scope and current code** (AC: 1, 4, 8, 10)
  - [ ] Read the July 2 correction and D5/D6 stories before editing.
  - [ ] Compare current `Sample.Api` host pattern with Tenants AppHost/UI/domain service.
  - [ ] Confirm generator support gaps for Tenants aggregate/entity binding and freshness headers.
  - [ ] Record current root and Tenants submodule git status before modifying files.

- [ ] **Task 2: Annotate Tenants commands** (AC: 2)
  - [ ] Add `ICommandContract` metadata and aggregate IDs to public Tenants commands.
  - [ ] Add `[RestRoute]` only for supported external commands.
  - [ ] Keep `BootstrapGlobalAdmin` unrouted unless an explicit product/security decision approves external exposure.
  - [ ] Add contract tests for command metadata and route/body parameter compatibility.

- [ ] **Task 3: Annotate Tenants queries** (AC: 3, 4)
  - [ ] Add `[RestRoute]` and bindable properties to the six query contract classes.
  - [ ] Preserve existing query constants and cursor/page-size semantics.
  - [ ] Add contract tests proving route templates, route parameters, query-string properties, and static metadata.

- [ ] **Task 4: Patch generator only if Tenants requires it** (AC: 4, 9)
  - [ ] Add reusable metadata or emitter behavior for constant aggregate/entity values and distinct entity binding.
  - [ ] Add freshness header emission from `QueryResponseMetadata`.
  - [ ] Add generator tests before relying on Tenants API host smoke tests.
  - [ ] Preserve existing D3/D4 generator behavior and diagnostics.

- [ ] **Task 5: Add external Tenants API host** (AC: 5, 6)
  - [ ] Create `Hexalith.Tenants.Api` project and Program startup mirroring Sample external API.
  - [ ] Add analyzer reference to `Hexalith.EventStore.RestApi.Generators`.
  - [ ] Add `RestApiAssemblyInfo.cs`.
  - [ ] Add project to `Hexalith.Tenants.slnx`.
  - [ ] Wire `tenants-api` into Tenants AppHost and DAPR access control.

- [ ] **Task 6: Move Tenants UI queries to EventStore Client** (AC: 1, 7)
  - [ ] Replace `TenantsQueryApiClient` usage with gateway-client submission.
  - [ ] Remove `Tenants:BaseAddress` UI dependency.
  - [ ] Preserve `ITenantQueryGateway` behavior, 304/freshness handling, cursor behavior, and search hydration.
  - [ ] Update UI DI/tests for the new client path.

- [ ] **Task 7: Retire the hand-written controller and stale tests** (AC: 8, 9)
  - [ ] Delete `TenantsQueryController` only after replacement API tests exist.
  - [ ] Remove stale query-controller-only services/tests.
  - [ ] Retarget integration coverage to generated external API or gateway-client behavior.
  - [ ] Keep domain service query/project endpoints and handlers.

- [ ] **Task 8: Verify and record evidence** (AC: 9, 10)
  - [ ] Emit generated source to `/tmp/hexalith-tenants-d7-generated`.
  - [ ] Run required per-project Tenants tests/builds.
  - [ ] Run required EventStore tests/builds if platform code changed.
  - [ ] Run integration/Aspire smoke if feasible, recording exact commands and blockers.
  - [ ] Confirm final root and submodule `git status --short` contain only intended D7 changes plus known unrelated files.

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

| Date | Change |
|---|---|
| 2026-07-02 | Story D7 created with corrected external API host scope, Tenants contract annotation requirements, generator binding/freshness gaps, UI client-library migration, old controller retirement guardrails, AppHost/DAPR topology requirements, and verification plan. Status ready-for-dev. |
