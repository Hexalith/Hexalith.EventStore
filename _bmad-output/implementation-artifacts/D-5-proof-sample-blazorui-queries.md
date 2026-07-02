---
created: 2026-07-01
source_story_key: D-5-proof-sample-blazorui-queries
baseline_commit: 4d1a207138baf70f5460ba755e2389cd7a52c22b
---

# Story D.5: Proof - Sample BlazorUI Queries

Status: in-progress

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

- [ ] **Task 1: Preflight D3/D4 and generator discovery model** (AC: 1, 2)
  - [ ] Inspect `src/Hexalith.EventStore.RestApi.Generators/` for controller emitter, route parser, diagnostic descriptors, and generated query action support.
  - [ ] Verify `tests/Hexalith.EventStore.RestApi.Generators.Tests/` exists and passes.
  - [ ] Verify the generator can emit controllers into `Sample.BlazorUI` from query contracts located outside the Blazor UI source tree.
  - [ ] If still manifest-only or syntax-only for local source, stop and correct D3/D4 before continuing.

- [ ] **Task 2: Annotate and test `GetCounterStatusQuery`** (AC: 3, 6)
  - [ ] Add `RestRouteAttribute` using a route parameter that preserves `EntityId = counter-1`.
  - [ ] Keep existing static query metadata unchanged.
  - [ ] Extend `GetCounterStatusQueryTests` to verify route verb/template and unchanged metadata.

- [ ] **Task 3: Wire the generator and REST opt-in into Sample.BlazorUI** (AC: 4, 5)
  - [ ] Add the analyzer project reference with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.
  - [ ] Add required runtime references for generated controllers (`Client` and the supported query-contract reference).
  - [ ] Add the `[assembly: RestApi(...)]` opt-in file with `RestTenantSource.Route`.
  - [ ] Register `AddControllers()` and `MapControllers()`.
  - [ ] Register `IEventStoreGatewayClient` using the existing DAPR/auth handler path.

- [ ] **Task 4: Replace `CounterQueryService` usage** (AC: 7, 8)
  - [ ] Delete `CounterQueryService.cs` and its DI registration.
  - [ ] Update `CounterValueCard`, `CounterHistoryGrid`, `SilentReloadPattern`, and `NotificationPattern`.
  - [ ] Preserve count-zero, ETag, refresh, and support-safe error behavior.
  - [ ] Avoid adding a renamed per-domain query wrapper.

- [ ] **Task 5: Add/extend proof tests** (AC: 2, 6, 7, 9)
  - [ ] Add a D4 generator test that proves referenced query contract discovery for the Sample Blazor UI shape, if not already present.
  - [ ] Assert the generated Sample query action builds the exact `SubmitQueryRequest` shape required by AC 6.
  - [ ] Add or update a structural test to prevent `CounterQueryService` from returning under a different name if the D4/D8 guardrail suite already has a natural place for it.

- [ ] **Task 6: Verify and record generated evidence** (AC: 9)
  - [ ] Build Sample.BlazorUI with `EmitCompilerGeneratedFiles=true` to `/tmp/hexalith-eventstore-d5-generated`.
  - [ ] Record generated controller file path and key source-shape evidence.
  - [ ] Run the generator tests, Sample tests, and Release package-mode solution build.
  - [ ] Run Aspire smoke validation if feasible; otherwise record the exact blocker.
  - [ ] Confirm `git status --short` contains only intended source/story/sprint-status changes and no generated files.

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

| Date | Change |
|---|---|
| 2026-07-01 | Story D5 created with Sample Blazor UI query proof scope, D3/D4 preflight gates, referenced-contract discovery warning, Counter route semantics, wrapper-removal guardrails, and verification requirements. Status ready-for-dev. |
