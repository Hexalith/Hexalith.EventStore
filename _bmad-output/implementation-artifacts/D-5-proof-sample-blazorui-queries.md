---
created: 2026-07-01
source_story_key: D-5-proof-sample-blazorui-queries
baseline_commit: 4d1a207138baf70f5460ba755e2389cd7a52c22b
---

# Story D.5: Proof - Sample BlazorUI Queries

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain-module author building a UI host on Hexalith.EventStore**,
I want **the Sample Blazor UI query REST facade generated from the Counter query contract**,
so that **the sample proves generated, gateway-backed typed query endpoints can replace the hand-written `CounterQueryService` wrapper before commands and Tenants adopt the generator**.

## Story Context

This is **story D5 of Epic D - REST Controller Source Generator**. It is the first in-repository adoption proof after the platform foundation:

- D1 added the author-facing contract seam (`ICommandContract`, `RestRouteAttribute`, `RestApiAttribute`).
- D2 added the analyzer project and manifest-only discovery spike.
- D3 is supposed to add generated ASP.NET Core controller emission.
- D4 is supposed to add persistent generator tests.
- D5 proves the query side in `samples/Hexalith.EventStore.Sample.BlazorUI`.
- D6 proves Counter commands.
- D7 proves Tenants UI host in the submodule.
- D8 closes package release/docs/guardrails.

**Critical preflight:** at story creation time, source inspection showed the generator is still manifest-only:

- `src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs` only registers `RestApiMessageDiscovery`, parses `[assembly: RestApi(...)]`, collects message descriptors, and emits `HexalithEventStoreRestApiGeneratorManifest.g.cs`.
- There is no controller emitter, action descriptor model, route-template parser, generated `ControllerBase` source, generated `IEventStoreGatewayClient` call, or diagnostic descriptor catalog on disk.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/` does not exist on disk.
- Commit `294aab40 feat: Implement D3 controller emission for Hexalith.EventStore` changed story text and D2 manifest/discovery files, but `git show 294aab40:src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs` is still manifest-only.

D5 must **not** become hidden D3 or D4 work. If controller emission or generator tests are still absent when D5 starts, stop and complete/correct D3 and D4 first. D5 can only adopt the generated query endpoint after the generated controller path is already real and protected by tests.

Source of truth: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4, Epic D row for D5, plus D1-D4 story files and the current Sample Blazor UI query path.

## Acceptance Criteria

1. **D3 and D4 are real before D5 implementation begins.**
   - `src/Hexalith.EventStore.RestApi.Generators/` contains controller-emission implementation, not only manifest emission.
   - The generator still preserves D2 manifest output and incremental discovery.
   - `tests/Hexalith.EventStore.RestApi.Generators.Tests/` exists and passes.
   - Generator tests cover at least one generated query controller using `RestTenantSource.Route`, a route-provided `entityId`, `If-None-Match`, ETag copy, 304, and gateway delegation through `IEventStoreGatewayClient`.
   - If any of the above is false, do not implement D5. Return to D3/D4 or correct the sprint state.

2. **The generator supports UI-host generation from referenced query contracts.**
   - `GetCounterStatusQuery` currently lives in `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs`, not in the Blazor UI project.
   - The generated controller is intended to be emitted into `samples/Hexalith.EventStore.Sample.BlazorUI`.
   - A syntax-only generator running in the Blazor UI compilation will not discover source syntax from a referenced Sample/domain assembly. Before adopting the sample query, verify the D3 generator supports the approved contract discovery model for UI hosts:
     - either discovering `IQueryContract` metadata from referenced domain/contract assemblies, or
     - a documented, tested shared-contract-source pattern that does not duplicate query contract types in the UI host.
   - Do not copy `GetCounterStatusQuery` into the Blazor UI project as a second type.
   - Do not create a new `Sample.Contracts` project inside D5 unless D3/D4 have explicitly established that as the supported pattern; otherwise this is scope expansion and needs story correction.

3. **Counter query contract is REST-annotated without changing query metadata.**
   - Update `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs`.
   - Add the REST route annotation needed by the generated controller.
   - Use a route shape that preserves the current query actor routing: the existing hand-written service sends both `aggregateId = "counter-1"` and `entityId = "counter-1"`. D3's stated mapping rules allow a route parameter named `entityId` to be used for `EntityId`, and with exactly one non-tenant route parameter it can also serve as `AggregateId`.
   - Preferred annotation unless D3 introduces a clearer explicit mapping:

     ```csharp
     [RestRoute(RestVerb.Get, "{entityId}")]
     public sealed record GetCounterStatusQuery : IQueryContract
     ```

   - Keep `QueryType = "get-counter-status"`, `Domain = "counter"`, and `ProjectionType = "counter"` unchanged.
   - Keep the query payload-free.
   - Update `samples/Hexalith.EventStore.Sample.Tests/Counter/Queries/GetCounterStatusQueryTests.cs` to assert the route attribute, verb, and template.

4. **Sample Blazor UI opts into generated Counter query controllers.**
   - Update `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj`.
   - Reference `src/Hexalith.EventStore.Client` because generated controllers inject `IEventStoreGatewayClient`.
   - Reference the generator as an analyzer, not as a runtime assembly:

     ```xml
     <ProjectReference Include="..\..\src\Hexalith.EventStore.RestApi.Generators\Hexalith.EventStore.RestApi.Generators.csproj"
                       OutputItemType="Analyzer"
                       ReferenceOutputAssembly="false" />
     ```

   - Reference the Sample domain/contract assembly only in the supported form proven by AC 2.
   - Do not add package versions to the `.csproj`; central package management owns versions.
   - Do not add release package inventory changes in D5.
   - Add a Blazor UI assembly-level REST opt-in file, for example `samples/Hexalith.EventStore.Sample.BlazorUI/RestApiAssemblyInfo.cs`:

     ```csharp
     using Hexalith.EventStore.Contracts.Rest;

     [assembly: RestApi("api/{tenant}/counter", "counter", RestTenantSource.Route)]
     ```

   - The route prefix must include `tenant` or `tenantId`, because `RestTenantSource.Route` must fail closed when no tenant route parameter is present.

5. **Sample Blazor UI hosts generated API controllers correctly.**
   - Update `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs`.
   - Register controller services with `builder.Services.AddControllers()`.
   - Map generated attribute-routed controllers with `app.MapControllers()`.
   - Keep existing Razor component mapping and SignalR client behavior.
   - Register `IEventStoreGatewayClient` using the existing DAPR sidecar and authorization handlers instead of inventing a second outbound path. The typed client should use the same effective base address and handlers as the current named `"EventStoreApi"` client:
     - `daprHttpEndpoint` as `BaseAddress`
     - `EventStoreApiAuthorizationHandler`
     - `DaprAppIdHandler("eventstore", daprApiToken)`
   - Preserve forwarding of the bearer token to the EventStore gateway; generated controllers must not bypass EventStore auth, RBAC, tenant validation, ETag handling, or problem mapping.

6. **Generated Counter query endpoint preserves current request semantics.**
   - The generated endpoint is expected to be equivalent to:

     ```text
     GET /api/{tenant}/counter/{entityId}
     ```

   - For `/api/tenant-a/counter/counter-1`, generated code must build a `SubmitQueryRequest` with:
     - `Tenant = "tenant-a"`
     - `Domain = GetCounterStatusQuery.Domain`
     - `AggregateId = "counter-1"`
     - `QueryType = GetCounterStatusQuery.QueryType`
     - `ProjectionType = GetCounterStatusQuery.ProjectionType`
     - `EntityId = "counter-1"`
     - no payload, or the same empty-payload behavior D3/D4 define for projection queries
   - It must forward a strong `If-None-Match` value to `IEventStoreGatewayClient.SubmitQueryAsync`.
   - It must return HTTP 304 when the gateway reports `IsNotModified`.
   - It must copy the strong ETag header on 200 and 304 when available.
   - It must return the raw query payload on HTTP 200, not a `SubmitQueryResponse` wrapper.
   - It must not call `DomainQueryDispatcher`, MediatR, DAPR actors/clients, projection actors, or state stores directly.

7. **The hand-written Counter query wrapper is removed without reintroducing equivalent boilerplate.**
   - Delete `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs`.
   - Remove `builder.Services.AddScoped<CounterQueryService>()`.
   - Update all current consumers:
     - `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor`
     - `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor`
     - `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor`
     - `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor`
   - Do not replace `CounterQueryService` with another hand-written per-domain BFF/query wrapper under a different name.
   - If a tiny DTO such as `CounterStatusResult` remains useful for component state, keep it as a state model, not as a service that constructs EventStore query envelopes.
   - User-visible behavior remains equivalent:
     - initial no-projection state displays count `0`;
     - projection change notifications still refresh data according to the existing pattern pages;
     - ETag/304 behavior avoids unnecessary payload parsing;
     - errors stay support-safe and do not render raw tokens, JWT payloads, EventStore metadata, stack traces, cursor internals, or raw query envelopes.

8. **Scope stays query-proof only.**
   - Do not annotate Counter command records, update `CounterCommandForm`, or replace generic command submission in D5; D6 owns commands.
   - Do not touch Tenants or any submodule files; D7 owns Tenants.
   - Do not update release package inventory, `.releaserc.json`, NuGet package count docs, or package governance tests; D8 owns packaging.
   - Do not redesign Sample UI visuals, layout, copy, or notification patterns.
   - Do not introduce raw interactive HTML controls. If component markup changes are unavoidable, keep using Fluent UI components and Fluent 2 tokens per the Hexalith UX rules.
   - Do not add AppHost, DAPR component, state store, pub/sub, container, or access-control changes unless local smoke validation proves a missing sidecar invocation permission. If that happens, document the exact missing policy and keep the change scoped to `sample-blazor-ui` invoking `eventstore`.

9. **Generated output and tests prove D5.**
   - Build the Blazor UI with generated files emitted to an untracked temp path:

     ```bash
     dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj \
       --configuration Release \
       -p:EmitCompilerGeneratedFiles=true \
       -p:CompilerGeneratedFilesOutputPath=/tmp/hexalith-eventstore-d5-generated
     ```

   - Inspect the generated controller source and record the generated file path in the Dev Agent Record.
   - The generated source must include `[ApiController]`, `[Authorize]`, `[Route("api/{tenant}/counter")]`, `[HttpGet("{entityId}")]`, `IEventStoreGatewayClient`, `SubmitQueryAsync`, `If-None-Match`, `GetCounterStatusQuery.QueryType`, `GetCounterStatusQuery.ProjectionType`, `ConfigureAwait(false)`, and no forbidden bypass strings.
   - Run the generator test project from D4:

     ```bash
     dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/
     ```

   - Run the Sample tests:

     ```bash
     dotnet test samples/Hexalith.EventStore.Sample.Tests/
     ```

   - Build the solution in Release package mode:

     ```bash
     dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false
     ```

   - Do not run solution-level `dotnet test`.
   - If local Aspire smoke validation is feasible, run the AppHost with `EnableKeycloak=false`, call the generated Sample Blazor UI endpoint for `tenant-a/counter-1`, and verify persisted/query end-state behavior after a Counter increment. If local DAPR/containers block this, record the exact command and blocker.

## Tasks / Subtasks

- [x] **Task 1: Preflight D3/D4 and generator discovery model** (AC: 1, 2)
  - [x] Inspect `src/Hexalith.EventStore.RestApi.Generators/` for controller emitter, route parser, diagnostic descriptors, and generated query action support.
  - [x] Verify `tests/Hexalith.EventStore.RestApi.Generators.Tests/` exists and passes.
  - [x] Verify the generator can emit controllers into `Sample.BlazorUI` from query contracts located outside the Blazor UI source tree.
  - [x] If still manifest-only or syntax-only for local source, stop and correct D3/D4 before continuing.

- [x] **Task 2: Annotate and test `GetCounterStatusQuery`** (AC: 3, 6)
  - [x] Add `RestRouteAttribute` using a route parameter that preserves `EntityId = counter-1`.
  - [x] Keep existing static query metadata unchanged.
  - [x] Extend `GetCounterStatusQueryTests` to verify route verb/template and unchanged metadata.

- [x] **Task 3: Wire the generator and REST opt-in into Sample.BlazorUI** (AC: 4, 5)
  - [x] Add the analyzer project reference with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.
  - [x] Add required runtime references for generated controllers (`Client` and the supported query-contract reference).
  - [x] Add the `[assembly: RestApi(...)]` opt-in file with `RestTenantSource.Route`.
  - [x] Register `AddControllers()` and `MapControllers()`.
  - [x] Register `IEventStoreGatewayClient` using the existing DAPR/auth handler path.

- [x] **Task 4: Replace `CounterQueryService` usage** (AC: 7, 8)
  - [x] Delete `CounterQueryService.cs` and its DI registration.
  - [x] Update `CounterValueCard`, `CounterHistoryGrid`, `SilentReloadPattern`, and `NotificationPattern`.
  - [x] Preserve count-zero, ETag, refresh, and support-safe error behavior.
  - [x] Avoid adding a renamed per-domain query wrapper.

- [x] **Task 5: Add/extend proof tests** (AC: 2, 6, 7, 9)
  - [x] Add a D4 generator test that proves referenced query contract discovery for the Sample Blazor UI shape, if not already present.
  - [x] Assert the generated Sample query action builds the exact `SubmitQueryRequest` shape required by AC 6.
  - [x] Add or update a structural test to prevent `CounterQueryService` from returning under a different name if the D4/D8 guardrail suite already has a natural place for it.

- [x] **Task 6: Verify and record generated evidence** (AC: 9)
  - [x] Build Sample.BlazorUI with `EmitCompilerGeneratedFiles=true` to `/tmp/hexalith-eventstore-d5-generated`.
  - [x] Record generated controller file path and key source-shape evidence.
  - [x] Run the generator tests, Sample tests, and Release package-mode solution build.
  - [x] Run Aspire smoke validation if feasible; otherwise record the exact blocker.
  - [x] Confirm `git status --short` contains only intended source/story/sprint-status changes and no generated files.

### Review Findings

_Adversarial code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor), 2026-07-02. 3 decision-needed, 2 patch, 6 deferred, 3 dismissed as noise._

_Resolution (2026-07-02): user chose **A1** (correct the wiring), **C1** (apply both patches), **B3** (defer Finding 3). Both patches applied and build-verified. **A1 as a direct `ProjectReference` failed** (`CS0436` `Program` collision) → escalated to **correct-course**._

_Correct-course outcome (2026-07-02) — see `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md`: owner directive established that **interactive UI hosts consume the platform Client libraries; the generated REST API is an external-facing surface**. Findings 1 & 2 are **RESOLVED by re-architecture**, not accepted as a deviation:_
- _New `Hexalith.EventStore.Sample.Contracts` library holds `GetCounterStatusQuery` (single compiled identity; no more `<Compile Link>`)._
- _New external `Hexalith.EventStore.Sample.Api` host owns the generated controller — its build **proves referenced-assembly discovery** end-to-end (generated `CounterRestController.g.cs` has `[ApiController]/[Authorize]/[Route]/[HttpGet]/SubmitQueryAsync/If-None-Match/QueryType/ProjectionType/ConfigureAwait`, no bypass strings)._
- _`Sample.BlazorUI` now uses the gateway **Client library** for queries only — generator analyzer ref, `[RestApi]` opt-in, `<Compile Link>`, `AddControllers/MapControllers`, and the inbound JWT surface all removed._
- _`sample-api` wired into the AppHost (resource + `WithDaprSidecar` app-id `sample-api` + `WithEventStoreClientCredentials` + DAPR access-control allow-list) — AppHost Release build clean, 34/34 AppHost tests pass._
- _Verified: `Sample.Api` + `Sample.BlazorUI` Release builds clean; 76/76 Sample tests pass; AppHost 34/34; full `slnx` Release build clean (0/0)._
- _**Remaining (follow-up):** run the live Aspire smoke (`aspire run` + `GET /api/tenant-a/counter/counter-1` → 200/ETag/304 against `sample-api`); formal AC rewrite to the new scope; D6/D7/D8 reframe per the proposal. Story stays `in-progress`._

**Decision-needed**

- [x] [Review][Decision→Resolved] Query contract type was duplicated in the UI host via `<Compile Link>`. **RESOLVED via correct-course:** the contract now lives in the `Hexalith.EventStore.Sample.Contracts` library (single compiled identity), referenced by the domain, the UI host (metadata), and the external `Sample.Api` host (generation). The `<Compile Link>` is gone; referenced-assembly discovery is proven by the `Sample.Api` build. [samples/Hexalith.EventStore.Sample.Contracts/Counter/Queries/GetCounterStatusQuery.cs]
- [x] [Review][Decision→Resolved] Generated query endpoint not consumed by the UI. **RESOLVED — now the intended architecture:** the interactive UI consumes the Client library (`IEventStoreGatewayClient`); the generated REST controller is an external-facing surface hosted in `Sample.Api`. [samples/Hexalith.EventStore.Sample.Api/Program.cs]
- [x] [Review][Decision→Defer] `CounterHistoryGrid` records a duplicate history row on every HTTP 304 (no change) — ambiguous intent (value-change log vs. polling/ETag-activity log). **Deferred per user decision B3.** [samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor:79]

**Patch**

- [x] [Review][Patch] Refresh errors are invisible — after a successful first load, a failed refresh sets `_error` but leaves `_result`/`_lastRefreshed` unchanged, and the render was `@if (_result is not null) … else if (_error is not null)`, so the error banner never showed and the stale count/timestamp remained. **APPLIED** — error banner now renders unconditionally (above the value) in `CounterValueCard`, `NotificationPattern`, `SilentReloadPattern`. [samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor:82]
- [x] [Review][Patch] `CounterHistoryGrid` 404 branch bypassed the 20-entry cap — the success path trimmed `_history` to 20 but the 404 branch only inserted, growing `_history` unbounded for a persistently-absent projection. **APPLIED** — the same trim now runs on the 404 branch. [samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor:94]

**Deferred**

- [x] [Review][Defer] Malformed projection payload throws in `ParseCountFromPayload` (`Convert.FromBase64String` / `JsonDocument.Parse` / `GetInt32`) — pre-existing behavior carried over from the deleted `CounterQueryService`; combined with the patch above it becomes invisible. [samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterStatusResult.cs:46] — deferred, pre-existing
- [x] [Review][Defer] Concurrent/re-entrant refresh race + in-flight request not cancelled on dispose (post-dispose `StateHasChanged`) — no in-flight guard; `GetAsync`'s `CancellationToken` is never wired; `SilentReloadPattern` partially mitigates via debounce. Demo-UI hardening. [samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor:46] — deferred, pre-existing pattern
- [x] [Review][Defer] Generator silently drops a `record struct` carrying `[RestRoute]` (kind check returns null with no diagnostic) — every other unsupported shape emits a HESREST diagnostic; this one is a silent no-op. Generator hardening. [src/Hexalith.EventStore.RestApi.Generators/RestApiMessageParser.cs:79] — deferred, generator scope
- [x] [Review][Defer] Referenced-message discovery runs off `CompilationProvider` producing a reference-equality `ImmutableArray`, adding heavier per-compilation work / weakening IDE incrementality — consistent with the generator's pre-existing CompilationProvider pattern; perf-only. [src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs:36] — deferred, pre-existing pattern
- [x] [Review][Defer] "No projection" empty-state is only handled for HTTP 404; a gateway `Success==false` semantic failure (StatusCode 200) falls through to the generic catch — divergence is minor and matches the old code's 404-only behavior in practice. [samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor:76] — deferred, matches prior behavior
- [x] [Review][Defer] AC8 scope hygiene — generator command-route mapping was changed and a command diagnostic test added in a query-only story (defensible as generator enablement), and the broader branch/working-tree carries CI/CD + `tools/release-*` + `.releaserc.json` + submodule-pointer changes (D7/D8 scope) that are absent from the scoped D5 diff and should be split out. [src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:871] — deferred, split into D7/D8

**Dismissed (noise / false positive)**

- Generated controllers unauthenticated → tenant confusion/IDOR — false positive: the emitter emits `[Authorize]` (asserted by `RestApiControllerGenerationTests`), and route-tenant authorization is delegated to the EventStore gateway by design (AC5).
- Named `"EventStoreApi"` HttpClient is dead config — false positive: still consumed by `CounterCommandForm.razor:62` (command submission, D6 scope).
- Rendering `ex.Message` in catch blocks is support-unsafe — Message-only (no stack trace, tokens, or JWT payload) and pre-existing; AC7 support-safe requirement is met.

## Dev Notes

### Top Guardrails

1. **Do not hide missing D3/D4 work inside D5.** Current source is manifest-only. If that is still true, D5 cannot start.
2. **Do not duplicate query contracts in the UI host.** The generator must support the intended UI-host/reference model, or the earlier story design is incomplete.
3. **Preserve tier-1 query routing.** Existing Sample query calls use `EntityId = "counter-1"`. A generated query with no `EntityId` will route to a different projection actor/cache key (`counter:tenant-a` instead of `counter:tenant-a:counter-1`).
4. **Generated controllers are gateway facades.** They call `IEventStoreGatewayClient`; they must not call domain query dispatch, DAPR actors, MediatR, or state stores.
5. **D5 is query-only.** Leave commands and `CounterCommandForm` to D6, even though `CounterCommandForm` currently uses `Guid.NewGuid()` for `messageId`.

### Current Code State Read During Story Creation

| File | Current state | D5 change | Preserve |
|---|---|---|---|
| `src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs` | Manifest-only incremental generator. | Must already be extended by D3 before D5 starts. | D2 manifest, tracking names, analyzer-only shape. |
| `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs` | Implements `IQueryContract`; no `RestRoute` attribute; metadata is `get-counter-status` / `counter` / `counter`. | Add route metadata that yields `EntityId = counter-1` for typed query endpoint. | Static metadata values and payload-free query shape. |
| `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` | Hand-written generic gateway wrapper; caches ETag; maps 404 to count 0; parses direct or base64 payload. | Delete after generated endpoint/component path preserves behavior. | User-visible count, ETag, 304, and support-safe error behavior. |
| `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` | Registers Razor components, Fluent UI, SignalR, named `EventStoreApi` HttpClient using DAPR app-id handler, and `CounterQueryService`. Does not register MVC controllers. | Add controller services/endpoints, generated controller dependencies, typed gateway client, remove `CounterQueryService`. | Existing DAPR invocation path, JWT token forwarding, SignalR startup, Razor component mapping. |
| `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` | References SignalR and ServiceDefaults only; no Client, generator analyzer, or Sample/domain contract reference. | Add only required references, versionless and analyzer-safe. | Web SDK, container settings, no package versions. |
| `CounterValueCard.razor`, `CounterHistoryGrid.razor`, `SilentReloadPattern.razor`, `NotificationPattern.razor` | Inject `CounterQueryService` and use `CounterStatusResult`. | Consume generated query endpoint or a platform-generic client path without per-domain envelope wrapper. | Existing visual patterns and Fluent UI usage. |

### Generated Endpoint Shape

The intended public query facade is:

```text
GET /api/{tenant}/counter/{entityId}
```

For the default sample configuration this is:

```text
GET /api/tenant-a/counter/counter-1
```

The generated controller should translate that to the same generic gateway query currently sent by `CounterQueryService`, except through `IEventStoreGatewayClient`:

```csharp
new SubmitQueryRequest(
    Tenant: tenant,
    Domain: GetCounterStatusQuery.Domain,
    AggregateId: entityId,
    QueryType: GetCounterStatusQuery.QueryType,
    ProjectionType: GetCounterStatusQuery.ProjectionType,
    Payload: null,
    EntityId: entityId);
```

If D3 chooses an empty JSON payload rather than `null`, D4/D5 tests must prove it does not change query actor routing or ETag behavior.

### Project Structure Notes

- The generated controller belongs in the Sample Blazor UI host, not in `samples/Hexalith.EventStore.Sample`.
- The Sample domain project remains a domain module and must keep referencing only `Hexalith.EventStore.DomainService`.
- `samples/Hexalith.EventStore.Sample.BlazorUI` may reference platform client/generator dependencies because it is a UI host, not the domain module.
- Do not create `.sln` files. If a new test project becomes necessary, add it to `Hexalith.EventStore.slnx`.
- Do not modify submodule files.

### Previous Story Intelligence

From D1:

- Empty/whitespace route templates are allowed at the contract seam; semantic validation belongs to generator stories.
- `RestApiAttribute` is assembly-level and `RestRouteAttribute` targets contract classes.
- Contract metadata values are kebab-case and colon-free by contract.

From D2:

- The analyzer project must stay `netstandard2.0`, `IsRoslynComponent=true`, and analyzer-only.
- Production generator code must use metadata-name discovery and cannot reference EventStore runtime assemblies directly.
- Manifest output is deterministic evidence and should remain even after controllers are generated.

From D3:

- Generated controllers must derive from `ControllerBase`, use `[ApiController]`, `[Authorize]`, route/tag attributes, and inject only `IEventStoreGatewayClient`.
- Query actions must forward `If-None-Match`, return 304, copy strong ETags, and return raw payload on 200.
- `RestTenantSource.Route` must require a `tenant` or `tenantId` route parameter and fail closed otherwise.

From D4:

- Generated source must compile in memory, not only satisfy string assertions.
- Diagnostics should have stable IDs and unsupported shapes should not emit broken actions.
- Current D4 story also warns that source on disk was manifest-only despite D3 status; D5 repeats that guard because it remains true at D5 story creation.

### Git Intelligence

Recent commits observed while creating D5:

- `294aab40 feat: Implement D3 controller emission for Hexalith.EventStore`
- `c30e6d23 chore(deps): update HexalithCommonsUniqueIdsVersion to 2.24.2 and add Microsoft.OpenApi package`
- `9cee4d3d chore: update sprint change proposals and project context with approval statuses`
- `f4f9bd69 chore(deps): update Hexalith dependency references`
- `84ac5b41 chore(workflows): consolidate ci and release automation`

Actionable implications:

- Trust source inspection over commit title. Commit `294aab40` is manifest/discovery only.
- Validate Release package mode with `-p:UseHexalithProjectReferences=false`.
- Keep `Microsoft.OpenApi` at the repo-pinned `2.9.0`; `Directory.Packages.props` explicitly notes `Microsoft.OpenApi` 3.x currently breaks ASP.NET Core 10 OpenAPI XML-comment generation in this repo.

### Latest Technical Notes

- ASP.NET Core 10 controller-based APIs use classes deriving from `ControllerBase`; Microsoft documents `[ApiController]` and `[Route]` as the normal web API controller shape. Use that shape for generated controllers, matching D3. Source: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0
- Controller services are registered with `AddControllers()`, which configures common controller API services and does not register views or pages. This is appropriate for Sample.BlazorUI generated API endpoints. Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.mvcservicecollectionextensions.addcontrollers?view=aspnetcore-10.0
- Attribute-routed REST controllers are mapped with `MapControllers()`. Microsoft notes REST APIs should use attribute routing. Source: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing?view=aspnetcore-10.0
- Microsoft documents `IIncrementalGenerator` lifetime as compiler-controlled and says generator instances must not store state. D5 must not add mutable generator instance state to make Sample discovery work. Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator?view=roslyn-dotnet-5.0.0
- Microsoft Roslyn SDK docs describe source generators as compile-time metaprogramming that add source to the compilation. D5 adoption should consume generated source; it must not depend on a runtime reflection fallback. Source: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/
- NuGet lists `Microsoft.AspNetCore.OpenApi` 10.0.9 as supporting OpenAPI generation for controller-based and minimal APIs. Use the repo's central pin; do not add package versions in Sample.BlazorUI. Source: https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi
- NuGet lists `Microsoft.CodeAnalysis.CSharp` 5.3.0; this matches the repo's central Roslyn pin and D2/D4 generator-test setup. Source: https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/

### Testing and Verification Standards

- Use xUnit v3 and Shouldly; no raw `Assert.*` in new tests.
- Run test projects individually; do not run solution-level `dotnet test`.
- Use `Hexalith.EventStore.slnx` only for restore/build.
- Always use `ConfigureAwait(false)` on awaited production-code calls.
- D5's integration/smoke evidence must inspect generated source and, when Aspire is feasible, observable query behavior. A build-only pass is not enough to prove the generated endpoint replaced the wrapper.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4, Epic D table] - D5 scope and sequence.
- [Source: `_bmad-output/implementation-artifacts/D-1-contract-seam.md`] - D1 contract seam and route attribute decisions.
- [Source: `_bmad-output/implementation-artifacts/D-2-generator-skeleton-spike.md`] - D2 manifest-only analyzer baseline.
- [Source: `_bmad-output/implementation-artifacts/D-3-controller-emission.md`] - D3 generated controller contract and query mapping expectations.
- [Source: `_bmad-output/implementation-artifacts/D-4-generator-tests.md`] - D4 generator test expectations and source-state warning.
- [Source: `src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs`] - current manifest-only generator entry point.
- [Source: `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs`] - current Counter query contract.
- [Source: `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs`] - hand-written query wrapper to replace.
- [Source: `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs`] - current DAPR HttpClient, auth handler, SignalR, and UI host startup.
- [Source: `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`] - generated controller gateway dependency.
- [Source: `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`] - ETag and gateway exception behavior.
- [Source: `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs`] - query request shape.
- [Source: `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs`] - tiered actor ID routing and `EntityId` importance.
- [Source: `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`] - query routing uses `ProjectionType`, `EntityId`, payload, and gateway envelope.
- [Source: `references/Hexalith.AI.Tools/hexalith-ux-instructions.md`] - UI/UX rules for Fluent/FrontComposer and no theme redefinition.
- [Microsoft Learn: Create web APIs with ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0) - controller-based web API shape.
- [Microsoft Learn: AddControllers](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.mvcservicecollectionextensions.addcontrollers?view=aspnetcore-10.0) - controller service registration.
- [Microsoft Learn: Routing to controller actions](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing?view=aspnetcore-10.0) - attribute-routed controller mapping.
- [Microsoft Learn: IIncrementalGenerator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator?view=roslyn-dotnet-5.0.0) - generator lifetime and no instance state.
- [Microsoft Learn: Roslyn SDK](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) - source generator compile-time model.
- [NuGet: Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) - controller/minimal API OpenAPI support package.
- [NuGet: Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/) - Roslyn C# package version.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `aspire run --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` reached Running/Healthy resources for `eventstore`, `sample`, and `sample-blazor-ui`; final smoke session was stopped.
- `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` passed: 43 tests.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/` passed: 76 tests.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` passed: 549 tests.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/` passed: 480 tests.
- `dotnet test tests/Hexalith.EventStore.SignalR.Tests/` passed: 35 tests.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/` passed: 144 tests.
- `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj --configuration Release -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/tmp/hexalith-eventstore-d5-generated` passed.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` passed.
- Generated controller evidence path: `/tmp/hexalith-eventstore-d5-generated/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.RestApiGenerator/Hexalith.EventStore.RestApi.Hexalith.EventStore.Sample.BlazorUI.Generated.CounterRestController.Controller.g.cs`.
- Generated source evidence included `[ApiController]`, `[Authorize]`, `[Route("api/{tenant}/counter")]`, `[HttpGet("{entityId}")]`, `IEventStoreGatewayClient`, `SubmitQueryAsync`, `If-None-Match`, `GetCounterStatusQuery.QueryType`, `GetCounterStatusQuery.ProjectionType`, `ConfigureAwait(false)`, null payload, and aggregate/entity values from `entityId`; forbidden bypass strings were absent.
- Aspire smoke: `POST https://localhost:45941/api/v1/commands` accepted `IncrementCounter` for `tenant-a/counter-1` with 202; `GET https://localhost:41951/api/tenant-a/counter/counter-1` returned 200 `{"count":1}` with strong ETag; the same GET with `If-None-Match` returned 304 with the same ETag.
- Final `aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive` reported no EventStore AppHost running after cleanup.
- Initial final live-smoke retry found `sample-api` without an Aspire launch profile: no published URL, no DAPR `--app-port`, `ASPNETCORE_ENVIRONMENT=Production`, and startup failure because `EventStore:Authentication:SigningKey` was not loaded from development settings.
- Added `samples/Hexalith.EventStore.Sample.Api/Properties/launchSettings.json`; after AppHost restart, `aspire describe sample-api` reported HTTP/HTTPS URLs, `ASPNETCORE_ENVIRONMENT=Development`, and the `sample-api-dapr-cli` command included `--app-port`.
- Final generated controller evidence path: `/tmp/hexalith-eventstore-d5-generated/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.RestApiGenerator/Hexalith.EventStore.RestApi.Hexalith.EventStore.Sample.Api.Generated.CounterRestController.Controller.g.cs`.
- Final generated source evidence included `[ApiController]`, `[Authorize]`, `[Route("api/{tenant}/counter")]`, `[HttpGet("{entityId}")]`, `IEventStoreGatewayClient`, `SubmitQueryAsync`, `If-None-Match`, `GetCounterStatusQuery.QueryType`, `GetCounterStatusQuery.ProjectionType`, and `ConfigureAwait(false)`; forbidden bypass strings `DomainQueryDispatcher`, `MediatR`, `DaprClient`, `ProjectionActor`, and `state store` were absent.
- Final live smoke against `sample-api` used `POST /api/v1/commands` through EventStore and `GET /api/tenant-a/counter/counter-1` through the generated external API endpoint: pre-count `2`, post-count `3`, final 200 with strong ETag, and conditional GET returned 304 with empty body.
- Redis end-state evidence after the final smoke showed event sequence `3` for `tenant-a:counter:counter-1`, projection state version `3` with decoded payload `{"count":3}`, completed command status for message `01KWHTQN02SMVYBD76XGA52MPG`, and persisted command metadata for tenant `tenant-a`, domain `counter`, aggregate `counter-1`.
- `dotnet build samples/Hexalith.EventStore.Sample.Api/Hexalith.EventStore.Sample.Api.csproj --configuration Release --no-incremental -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/tmp/hexalith-eventstore-d5-generated` passed with 0 warnings/errors.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` passed with 0 warnings/errors.
- Release test pass: `tests/Hexalith.EventStore.AppHost.Tests/` 35/35, `tests/Hexalith.EventStore.DomainService.Tests/` 37/37, `tests/Hexalith.EventStore.RestApi.Generators.Tests/` 43/43, `samples/Hexalith.EventStore.Sample.Tests/` 4/4, `tests/Hexalith.EventStore.Sample.Tests/` 76/76, `tests/Hexalith.EventStore.QueryRouting.Tests/` 3/3.
- Release regression pass: `tests/Hexalith.EventStore.Contracts.Tests/` 549/549, `tests/Hexalith.EventStore.Client.Tests/` 480/480, `tests/Hexalith.EventStore.SignalR.Tests/` 35/35, `tests/Hexalith.EventStore.Testing.Tests/` 144/144, `tests/Hexalith.EventStore.Server.Tests/` 2209 passed / 25 skipped.
- Release Admin pass: `tests/Hexalith.EventStore.Admin.Abstractions.Tests/` 423/423, `tests/Hexalith.EventStore.Admin.Cli.Tests/` 342/342, `tests/Hexalith.EventStore.Admin.Mcp.Tests/` 320 passed / 8 skipped, `tests/Hexalith.EventStore.Admin.Server.Host.Tests/` 15/15, `tests/Hexalith.EventStore.Admin.Server.Tests/` 717 passed / 18 skipped, `tests/Hexalith.EventStore.Admin.UI.Tests/` 840/840.
- Out-of-scope red-phase governance suites attempted and still blocked by missing story-specific artifacts: `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/` failed on missing DW6 story/checker entrypoint artifacts; `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/` failed on missing operational-evidence validator entrypoint artifacts.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Completed D3/D4 preflight: controller emission, route parsing, diagnostics, and generator tests are present and passing.
- Added referenced-assembly contract discovery to the REST generator for explicitly `[RestRoute]`-annotated command/query contracts, preserving source syntax discovery and D2 manifest output.
- Annotated `GetCounterStatusQuery` with `[RestRoute(RestVerb.Get, "{entityId}")]` and preserved `QueryType`, `Domain`, and `ProjectionType`.
- Wired Sample.BlazorUI for generated controllers with analyzer reference, REST assembly opt-in, controller service/endpoint mapping, JWT bearer validation, and typed `IEventStoreGatewayClient` using the existing DAPR app-id and bearer-token handlers.
- Removed `CounterQueryService` and replaced consumer usage with a platform-generic projection query client plus `CounterStatusResult` state parsing; no renamed per-domain query wrapper was added.
- Added proof tests for referenced contract discovery, generated request shape, route annotation, and guardrails preventing the hand-written query wrapper/path from returning.
- Added a Sample.Api launch profile so Aspire publishes HTTP/HTTPS URLs, runs the host in `Development`, loads the local symmetric signing key, and supplies the DAPR sidecar app port.
- Removed the Sample domain module's direct reference to `Hexalith.EventStore.Sample.Contracts`; the domain remains SDK-only and tests reference the contracts library directly where contract metadata is asserted.
- Added an AppHost guard test for the Sample.Api launch profile to prevent regressing generated external API smokeability.
- Closed the remaining AppHost/live-smoke gap with generated `Sample.Api` endpoint evidence and persisted Redis end-state evidence after a Counter increment.

### File List

- `_bmad-output/implementation-artifacts/D-5-proof-sample-blazorui-queries.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs`
- `samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj`
- `samples/Hexalith.EventStore.Sample.Api/Properties/launchSettings.json`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/RestApiAssemblyInfo.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` (deleted)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterStatusResult.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/EventStoreProjectionQueryClient.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiMessageParser.cs`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratorTestHarness.cs`
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/BlazorUI/CounterQueryWrapperGuardTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj`
- `tests/Hexalith.EventStore.Sample.Tests/Counter/Queries/GetCounterStatusQueryTests.cs`

## Change Log

| Date | Change |
|---|---|
| 2026-07-01 | Story D5 created with Sample Blazor UI query proof scope, D3/D4 preflight gates, referenced-contract discovery warning, Counter route semantics, wrapper-removal guardrails, and verification requirements. Status ready-for-dev. |
| 2026-07-02 | Implemented D5 Sample BlazorUI query proof: generator referenced-contract support, Counter query REST annotation, generated controller wiring, wrapper removal, proof tests, generated evidence, Release build, and Aspire smoke validation. Status review. |
| 2026-07-02 | Adversarial code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Applied 2 patches (refresh-error visibility in 3 components; 404 history-cap trim) — Release build clean, 76/76 Sample tests pass. Attempted AC2 rewiring via `ProjectReference` to Sample; reverted (`CS0436` `Program` collision). Finding 1 (contract duplicated in UI host via `<Compile Link>`) + coupled Finding 2 remain open pending re-decision (contracts project vs. accept deviation); Finding 3 deferred. 6 items added to deferred-work.md. Status → in-progress. |
| 2026-07-02 | Correct-course (`sprint-change-proposal-2026-07-02-rest-api-external-host.md`): re-architected per owner directive "UI uses Client libraries; REST API is external." Added `Sample.Contracts` library (relocated `GetCounterStatusQuery`) + `Sample.Api` external host (owns the generated controller). Stripped generator wiring/`[RestApi]` opt-in/`<Compile Link>`/`AddControllers`/`MapControllers`/inbound-JWT from `Sample.BlazorUI`; UI now uses the gateway Client library for queries. Findings 1 & 2 resolved. Verified: `Sample.Api` generated-controller emission via referenced discovery, Release builds clean, 76/76 Sample tests, full slnx Release build 0/0. Remaining: AppHost wiring + live smoke + formal AC rewrite + D6/D7/D8. |
| 2026-07-02 | Closed D5 AppHost/live-smoke gap: added `Sample.Api` launch profile, preserved Sample domain SDK-only references, added launch-profile guard test, verified generated external API endpoint with 200/ETag/304 and Redis persisted end-state after increment, refreshed generated source evidence, and completed Release build/test regression. Status review. |
