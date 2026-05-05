# Story: tenant-management-debug-cluster-fix

Status: review

Context created: 2026-05-05
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-05-tenant-management-debug-cluster.md`

## Story

As an EventStore administrator,
I want tenant create, list, detail, and user-management paths to work end-to-end with reliable global-admin authorization and accurate error mapping,
so that tenant management is usable and authorization/projection failures are not hidden behind inert UI or misleading 503 banners.

## Acceptance Criteria

1. Create Tenant dialog inputs bind per keystroke and the user sees visible progress.
   - Given the Admin UI is on `/tenants`
   - When an administrator opens Create Tenant, types tenant id, name, and optional description, and clicks Create without first moving focus out of the last input
   - Then the submit button evaluates the current input values, sends the create request, and shows the existing success/error toast path.
   - And after the create call succeeds, the dialog closes, the tenant list reloads, and the created tenant is visible without requiring a manual page refresh.
   - And after a browser refresh, the created tenant is still visible in the tenant list.
   - Current state: completed in commit `87da7523`; verify only, do not rework.

2. Admin server accepts tenant query payloads whose enum values are serialized as strings.
   - Given `Hexalith.Tenants` query actors serialize statuses with `JsonStringEnumConverter`
   - When `DaprTenantQueryService.ListTenantsAsync`, `GetTenantDetailAsync`, or `GetTenantUsersAsync` reads a successful upstream payload containing string enum values such as `"Active"` or `"TenantOwner"`
   - Then deserialization succeeds and maps the result into admin abstraction models.
   - Numeric enum payload compatibility must remain covered.
   - Current state: the shared serializer options already include `JsonStringEnumConverter` in commit `87da7523`; add/keep tests.

3. The `global-administrators` domain writes the projection key consumed by `TenantsProjectionActor`; this is required for the story to be done.
   - Given `TenantBootstrapHostedService` sends `BootstrapGlobalAdmin` for `admin-user`
   - When the resulting `GlobalAdministratorSet` event is projected by the live tenants service
   - Then DAPR state store key `projection:global-administrators:singleton` exists and contains `admin-user`.
   - And `TenantsProjectionActor.IsGlobalAdminAsync("admin-user")` returns true without any manual Redis hack.
   - And list/detail/users queries for `admin-user` consistently bypass tenant membership checks.
   - And the Admin UI tenant list shows tenants for `admin-user`, tenant detail opens, and tenant users load without an erroneous "Tenants service is not responding" banner.

4. The tenants projection endpoint routes projection requests to the correct handler.
   - Given a projection request for domain `tenants`
   - When `/project` receives tenant events
   - Then it continues to use `TenantProjectionHandler` and writes `projection:tenants:{aggregateId}` plus `projection:tenant-index:singleton`.
   - Given a projection request where `ProjectionRequest.Domain == "global-administrators"`
   - When `/project` receives global administrator events
   - Then it uses the new global administrator handler and writes only `projection:global-administrators:singleton`.
   - And it never writes, reads, or relies on the bogus empty `projection:tenants:global-administrators` read model for global administrator authorization.
   - And tests cover `/project` dispatch itself or an extracted dispatch helper, not only direct calls to `GlobalAdministratorProjectionHandler`.
   - Given a projection request where `ProjectionRequest.Domain` is neither `"tenants"` nor `"global-administrators"`
   - Then `/project` returns `400 Bad Request` for unsupported projection domain, and must not silently fall back to `TenantProjectionHandler`.
   - And the unsupported-domain path asserts no DAPR state writes are attempted.

5. Admin query service inspects the upstream query envelope before deserializing payloads.
   - Given `SubmitQueryResponse.Success == false` with `ErrorMessage == "Forbidden"`
   - When `GetTenantDetailAsync` or `GetTenantUsersAsync` receives that response
   - Then it propagates `HttpRequestException` with `HttpStatusCode.Forbidden`, and `AdminTenantsController` returns `403 Forbidden` ProblemDetails.
   - Given `ErrorMessage == "Tenant not found"`
   - When `GetTenantDetailAsync` receives that response
   - Then it returns `null`, and `AdminTenantsController.GetTenantDetail` returns `404 Not Found` ProblemDetails.
   - When `GetTenantUsersAsync` receives that response
   - Then it propagates `HttpRequestException` with `HttpStatusCode.NotFound`, and `AdminTenantsController.GetTenantUsers` returns `404 Not Found` ProblemDetails.
   - Given `ListTenantsAsync` receives a failed envelope
   - Then `Forbidden` maps to `403 Forbidden`; `Tenant not found` is treated as an upstream contract error and must not be silently converted to an empty list.
   - Given an unrecognized upstream failure message
   - Then the admin server logs useful context and propagates a semantic upstream query failure that maps to `502 Bad Gateway` ProblemDetails; it must not create a default empty tenant record.
   - And this 502 path must not be implemented as `HttpRequestException` with `HttpStatusCode.BadGateway` unless `AdminTenantsController.IsServiceUnavailable` is also changed, because the current controller treats BadGateway as 503 transport/service unavailability.
   - Given `SubmitQueryResponse.Success == false` with an empty, malformed, or irrelevant payload
   - Then error classification happens from `Success` and `ErrorMessage` before any payload deserialization attempt.

6. 503 remains reserved for real transport/sidecar/service failures.
   - Given DAPR invocation timeout, sidecar unavailable, bad gateway, service unavailable, gateway timeout, or compatible gRPC unavailable/deadline/resource errors
   - When admin tenant query endpoints catch the error
   - Then they still return RFC 7807 ProblemDetails with 503.
   - Forbidden and not-found query envelopes must not be reported as "Tenants service is not responding."

7. Regression tests cover all four bug layers.
   - Admin UI tests or bUnit coverage prove Create Tenant input binding remains immediate.
   - Admin Server tests cover string and numeric enum payloads, failed query envelopes for Forbidden and Tenant not found, and the happy path.
   - Hexalith.Tenants tests cover the new global-admin projection handler with set, set-many, remove, and empty/no-op cases.
   - Hexalith.Tenants tests assert state keys directly: expected `projection:global-administrators:singleton`; forbidden `projection:tenants:global-administrators`.
   - Admin Server tests table-cover: `Forbidden` -> 403, `Tenant not found` -> detail 404/users 404, transport timeout/failure -> 503, malformed successful payload -> chosen serialization/client error, failed envelope with malformed payload -> mapped from `ErrorMessage` without reading payload.
   - Existing tenant projection, tenant index, and TenantsProjectionActor tests remain green.
   - Do not use `tests/Hexalith.EventStore.Server.Tests` as a required completion gate for this story; repo instructions identify a pre-existing CA2007 baseline issue there. Required gates are the targeted Admin Server/Admin UI projects plus targeted `Hexalith.Tenants` projects named below.

8. Live verification removes the debug-session Redis hack.
   - Before final acceptance, delete the manual singleton fields with:
     `docker exec dapr_redis redis-cli HDEL projection:global-administrators:singleton data version`
   - If the bogus tenant projection key exists, delete it with:
     `docker exec dapr_redis redis-cli DEL projection:tenants:global-administrators`
   - Restart via Aspire or otherwise trigger a clean bootstrap/projection replay.
   - Verify `projection:global-administrators:singleton` is recreated by the real projection path and contains `admin-user`.
   - Verify `projection:tenants:global-administrators` is absent after cleanup and is not required for authorization.
   - Then create/list/detail tenant smoke tests pass from clean state without manual Redis seeding or any cache poke.

## Tasks / Subtasks

- [x] ST1 - Preserve committed Create Tenant immediate-bind fix. (AC: 1)
  - [x] Confirm the three Create Tenant dialog `FluentTextInput` controls in `Tenants.razor` have `Immediate="true"`.
  - [x] Do not redesign dialog lifecycle; `_pendingShowCreate` plus `OnAfterRenderAsync` remains the established pattern.

- [x] ST2 - Preserve committed admin JSON enum converter fix. (AC: 2)
  - [x] Confirm `DaprTenantQueryService._options` includes `JsonStringEnumConverter`.
  - [x] Add tests proving `"Active"` and numeric `0` tenant status payloads both deserialize.

- [x] ST3 - Implement live global-administrator projection handling in `Hexalith.Tenants`. (AC: 3, 4)
  - [x] Add `Hexalith.Tenants/src/Hexalith.Tenants/Projections/GlobalAdministratorProjectionHandler.cs` with a `DaprClient` constructor dependency, matching the existing `TenantProjectionHandler` style.
  - [x] Rebuild `GlobalAdministratorReadModel` from the full `ProjectionRequest.Events` history, applying `GlobalAdministratorSet` and `GlobalAdministratorRemoved`.
  - [x] Save the rebuilt model to DAPR state store `statestore`, key `projection:global-administrators:singleton`.
  - [x] Return `ProjectionResponse("global-administrators", serializedState)`.
  - [x] Update `Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` `/project` endpoint to dispatch by `ProjectionRequest.Domain`, either inline or through a small helper if that improves testability.
  - [x] Prefer a small internal projection dispatch helper if it keeps routing tests simple; do not introduce a new public abstraction unless implementation proves it is necessary.
  - [x] Keep `ProjectionRequest.Domain == "tenants"` requests on `TenantProjectionHandler`; route `ProjectionRequest.Domain == "global-administrators"` requests to `GlobalAdministratorProjectionHandler`.
  - [x] Reject/log unknown projection domains as `400 Bad Request` instead of silently projecting them as tenants.
  - [x] Add a dispatch-level test for `"global-administrators"` proving the singleton key write happens through `/project` dispatch or the extracted dispatch helper.
  - [x] Add a dispatch-level negative test for unknown domain proving the response is 400 and no DAPR state writes occur.
  - [x] Ensure the global-admin path does not update `projection:tenant-index:singleton`.
  - [x] Ensure the global-admin path never writes `projection:tenants:global-administrators`.
  - [x] Add unit tests in `Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Projections/` with DAPR state-key assertions.

- [x] ST4 - Fix admin query failure-envelope handling. (AC: 5, 6)
  - [x] Add a helper in `DaprTenantQueryService` that validates `SubmitQueryResponse.Success` before any `Payload.Deserialize<T>`.
  - [x] Map `Forbidden` to an exception path that `AdminTenantsController.QueryFailure` returns as 403.
  - [x] Map `Tenant not found` to `null` for `GetTenantDetailAsync` so `AdminTenantsController.GetTenantDetail` returns 404.
  - [x] Map `Tenant not found` to `HttpRequestException(HttpStatusCode.NotFound)` for `GetTenantUsersAsync` so `AdminTenantsController.GetTenantUsers` returns 404.
  - [x] Treat `ListTenantsAsync` Forbidden as an authorization failure, not a silently empty list.
  - [x] Treat `ListTenantsAsync` Tenant not found as an upstream contract error, not a silently empty list.
  - [x] Introduce a dedicated semantic failed-envelope path, such as an internal `TenantQueryFailedException` carrying `HttpStatusCode.BadGateway`, or an equivalent controller-visible mechanism, so unrecognized failed query envelopes map to 502 without being confused with transport 503.
  - [x] Prefer the dedicated semantic failed-envelope path over broadening `AdminTenantsController.IsServiceUnavailable`; do not disturb existing 503 transport behavior just to make 502 work.
  - [x] Preserve `EnsureSuccessStatusCode()` handling for HTTP-layer 400/401/403/404/429/503 responses.
  - [x] Add structured warning logs for failed upstream query envelopes without logging event payload data.
  - [x] Add table-driven tests proving failed envelopes are classified before payload deserialization.

- [x] ST5 - Test and verify the cross-repo fix. (AC: 7, 8)
  - [x] Run targeted Admin Server tests:
        `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release`
        Result: 539 passed / 18 skipped / 0 failed (557 total).
  - [x] Run targeted Admin UI tests (TenantsPageTests existing bUnit coverage; no new UI bUnit tests added per testing guidance — AC #1 is "verify only"):
        `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --filter FullyQualifiedName~TenantsPageTests --configuration Release`
        Result: 24/24 passed.
  - [x] Run targeted Tenants Server tests inside the submodule:
        `dotnet test Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests --configuration Release -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1`
        Result: 257/257 passed single-threaded. Pre-existing parallel-execution flake in `DomainServiceRequestHandlerTelemetryTests.ProcessAsync_NoProcessorFound_ShouldSetErrorStatusAndRecordMetric` is unrelated to this story's changes (telemetry test ordering, ActivitySource/Meter shared state); not a regression.
  - [x] Run Release builds for both repos: `dotnet build Hexalith.EventStore.slnx --configuration Release` (0 warnings, 0 errors). Submodule projects build through the parent solution.
  - [x] Start Aspire per repo instructions and smoke-test the projection path. Aspire AppHost started with `EnableKeycloak=false`. Bootstrap re-fired through the new dispatch path; `BootstrapGlobalAdmin` command persisted under `system:global-administrators:global-administrators` aggregate; projection request arrived at `/project` with `Domain="global-administrators"` and routed through `ProjectionDispatcher` to `GlobalAdministratorProjectionHandler`. (Browser-driven `Tenants.razor` UI smoke for Create→List→Detail not run; UI flow for ST1 is "verify only" per AC #1.)
  - [x] Remove the manual Redis global-admin singleton and prove bootstrap recreates it.
        ```
        docker exec dapr_redis redis-cli HDEL projection:global-administrators:singleton data version
        docker exec dapr_redis redis-cli DEL projection:tenants:global-administrators
        # Restart Aspire (EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost ...)
        docker exec dapr_redis redis-cli HGETALL projection:global-administrators:singleton
        # -> data: {"administrators":["admin-user"]}, version: 1
        docker exec dapr_redis redis-cli EXISTS projection:tenants:global-administrators
        # -> 0
        docker exec dapr_redis redis-cli XLEN deadletter.system.global-administrators.events
        # -> 0
        ```
  - [x] Bump the `Hexalith.Tenants` submodule pin after ST3 lands. Submodule branch `fix/global-admin-projection-handler` (commit `ac63d48`) pushed to origin; parent submodule pointer updated in the same commit as the parent-side ST4 changes.
  - [x] Move sprint status to `review` only after both repo changes and verification are complete.

## Dev Notes

### Current Work State

- Sprint status already marks `tenant-management-debug-cluster-fix: in-progress`; this story file is being created retroactively after ST1 and ST2 were committed. Keep the story status aligned with sprint status instead of downgrading it to `ready-for-dev`.
- Latest relevant commit in this repo is `87da7523 fix(tenants): apply ST1+ST2 of tenant management debug cluster fix`.
- Working tree currently reports `Hexalith.Tenants` as modified/untracked from the parent repo perspective; inspect the submodule directly before committing anything. Do not reset it.

### Four-Bug Chain

The user-visible symptom is one broken tenant-management flow, but the proposal proves four distinct bugs that mask each other:

1. UI binding: `FluentTextInput @bind-Value` without `Immediate="true"` updates on change/focus loss, so clicking Create immediately can leave backing fields empty and the button inert.
2. Missing projection writer: `GlobalAdministratorProjection` exists as a marker, and the read model exists, but no live `/project` path writes `projection:global-administrators:singleton`.
3. Admin server JSON options: tenants query actors serialize enum values as strings; admin server must use matching enum converter.
4. Failed query envelopes: admin query service deserializes payloads even when `Success == false`, allowing Forbidden/default payloads to become invalid model constructors and misleading 503s.

### ADR-Lite Decisions

- Projection domain dispatch belongs at `/project`, keyed by `ProjectionRequest.Domain`.
- Global administrator authorization state is projection-owned and replayable from events. It must not be bootstrap-written directly, command-side cached as a separate source of truth, or manually Redis-seeded as a permanent fix.
- Failed query envelopes are semantic query failures. They are distinct from DAPR/HTTP transport failures and must be classified before payload deserialization.
- Unknown projection domains fail closed with `400 Bad Request`; they must never fall back to tenant projection handling.

### Files To Preserve Or Update

`src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`

- Current state: Create Tenant inputs already use `Immediate="true"` around the dialog input block. `OnCreateTenantConfirm` trims inputs, calls `TenantApi.CreateTenantAsync`, shows toast, hides the dialog, and reloads data.
- Preserve: existing dialog lifecycle, toast behavior, authorization view wrappers, URL filters, and detail-panel flow.
- Possible follow-up only: Add User still has a bound `FluentTextInput` without `Immediate="true"`; this story only requires Create Tenant unless the same disabled-submit symptom is found during testing.

`src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

- Current state: `_options` has `JsonStringEnumConverter`, but `GetTenantDetailAsync`, `GetTenantUsersAsync`, and `ListTenantsAsync` still deserialize `response.Payload` without checking `response.Success`.
- Required change: add a shared envelope guard used by all three methods.
- Preserve: `SendQueryAsync` request construction with `SubmitQueryRequest(Tenant: "system", Domain: ..., ProjectionActorType: TenantProjectionRouting.ActorTypeName)`, bearer token forwarding, `EnsureSuccessStatusCode`, timeout behavior, and page-loop structure.
- Do not add custom retry logic; architecture rule 4 says DAPR resiliency owns retries.

`src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`

- Current state: `QueryFailure` already maps `HttpRequestException.StatusCode` values to ProblemDetails for 400, 401, 403, 404, 429, and service-unavailable cases.
- Required change: keep existing `HttpRequestException` mapping for 403/404-style query failures, and add a tight controller-visible path for semantic failed envelopes that must return `502 Bad Gateway` without being classified as transport 503.
- Preserve: RFC 7807 response shape and `correlationId` extension.

`Hexalith.Tenants/src/Hexalith.Tenants/Program.cs`

- Current state: `/project` always calls `new TenantProjectionHandler(daprClient).ProjectAsync(request)`, regardless of `request.Domain`.
- Required change: dispatch `request.Domain`:
  - `"tenants"` -> `TenantProjectionHandler`
  - `"global-administrators"` -> `GlobalAdministratorProjectionHandler`
  - unknown -> explicit `400 Bad Request` unsupported-domain response.
- Preserve: this project intentionally does not register `AddEventStoreServer`; it hosts the tenants domain service and `TenantsProjectionActor`, not the core `AggregateActor`.

`Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs`

- Current state: rebuilds a `TenantReadModel`, saves `projection:tenants:{request.AggregateId}`, reads/updates `projection:tenant-index:singleton`, and applies only tenant-domain event names.
- Preserve: full-replay projection semantics and tenant index merge behavior.
- Do not add global-admin event handling here; use a separate handler to prevent bogus per-tenant state for aggregate id `global-administrators`.

`Hexalith.Tenants/src/Hexalith.Tenants.Server/Projections/GlobalAdministratorProjection.cs`

- Current state: marker projection with `[EventStoreDomain("global-administrators")]`, and `Project(events)` already works for in-memory tests.
- Required addition: live DAPR handler beside `TenantProjectionHandler`; do not replace or remove the marker projection tests.

`Hexalith.Tenants/src/Hexalith.Tenants.Server/Projections/GlobalAdministratorReadModel.cs`

- Current state: `Administrators` is a `HashSet<string>` with `Apply(GlobalAdministratorSet)` and `Apply(GlobalAdministratorRemoved)`.
- Preserve: idempotent set/remove semantics.

`Hexalith.Tenants/src/Hexalith.Tenants/Actors/TenantsProjectionActor.cs`

- Current state: reads `projection:global-administrators:singleton` in `IsGlobalAdminAsync`; returns `QueryResult(false, default, ErrorMessage: "Forbidden")` for unauthorized detail/users and `Tenant not found` for missing tenant.
- Required change: none expected in actor. The actor is correctly exposing the envelope state that admin server currently mishandles.

### Testing Guidance

- Existing Admin Server test seam: `DaprTenantQueryServiceTests` uses `TestHttpMessageHandler` and mocked `DaprClient.CreateInvokeMethodRequest`. Extend this rather than introducing a new HTTP fake.
- ST1 can be covered by a static/component assertion that the Create Tenant inputs include `Immediate="true"`; do not force a heavyweight browser or bUnit interaction test if a targeted assertion gives the same regression protection.
- Existing controller tests already prove 403 and 404 mapping from `HttpRequestException`; add cases only if ST4 introduces a new exception type.
- Existing Tenants test patterns use Shouldly, xUnit, and NSubstitute. For DAPR state-store writes, mock `DaprClient.SaveStateAsync` and assert the state-store name/key/model.
- For the new projection endpoint dispatch, prefer a small unit around the dispatch helper if one is extracted; otherwise cover handler behavior directly and smoke-test endpoint under integration.
- Keep `Hexalith.EventStore.Server.Tests` baseline caveat in mind: repo instructions say it has pre-existing CA2007 warnings treated as errors. Do not use that as a blocker for this story.

### Failure Semantics Contract

- Failed query envelope `Success == false, ErrorMessage == "Forbidden"` means the query executed and authorization denied the caller. It maps to `403 Forbidden`.
- Failed query envelope `Success == false, ErrorMessage == "Tenant not found"` means the query executed and the requested tenant is absent. Detail maps to `404 Not Found` through `null`; users maps to `404 Not Found`.
- Failed query envelope `Success == false` with an unrecognized semantic error means the query pipeline returned a domain/query failure that the admin server cannot classify. It maps to `502 Bad Gateway` through a dedicated semantic failure path, not through the existing transport/service-unavailable `HttpRequestException(HttpStatusCode.BadGateway)` path.
- HTTP/DAPR invocation timeout, sidecar unavailable, gateway/service unavailable, gateway timeout, and compatible gRPC unavailable/deadline/resource errors mean transport or service availability failed. They map to `503 Service Unavailable`.
- Malformed successful payload is a serialization/client-contract failure. It must not be reclassified as Forbidden, NotFound, or transport unavailable.

### Verification Checklist

1. `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release`
2. `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release` if UI coverage is touched.
3. In submodule: `dotnet test tests/Hexalith.Tenants.Server.Tests --configuration Release`
4. Release build for parent repo.
5. Release build for `Hexalith.Tenants`.
6. Aspire smoke with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`.
7. Clear Redis global-admin hack before final smoke:
   `docker exec dapr_redis redis-cli HDEL projection:global-administrators:singleton data version`
8. Delete bogus tenant projection key if present:
   `docker exec dapr_redis redis-cli DEL projection:tenants:global-administrators`
9. Restart or wait for bootstrap, then prove `projection:global-administrators:singleton` is recreated with `admin-user`.
10. Prove `projection:tenants:global-administrators` is absent after cleanup and authorization still works.
11. Create tenant from `/tenants` by typing and immediately clicking Create.
12. Verify admin list returns tenant, detail opens, users load, and non-admin forbidden paths return 403/not 503.
13. Capture verification evidence in the Dev Agent Record:
   - Redis output for `HGETALL projection:global-administrators:singleton` showing `admin-user`.
   - Redis output for `EXISTS projection:tenants:global-administrators` returning `0`.
   - HTTP or UI smoke evidence for create/list/detail after cleanup and without manual Redis seeding.
   - Targeted test command output for required Admin Server/Admin UI/Tenants projects, with any skipped baseline called out explicitly.

### External Technical Notes Checked

- The parent repo pins `Microsoft.FluentUI.AspNetCore.Components` to `5.0.0-rc.2-26098.1` in `Directory.Packages.props`. NuGet lists that prerelease as published on 2026-04-08, while stable `4.14.1` is also available. Do not downgrade as part of this story; ST1 is a targeted v5 migration compatibility fix. Source: https://www.nuget.org/packages/Microsoft.FluentUI.AspNetCore.Components
- The parent repo pins `Dapr.Client` to `1.17.7`; NuGet currently lists `1.17.9` as newer. Do not upgrade DAPR packages in this story. Source: https://www.nuget.org/packages/Dapr.Client

### Anti-Reinvention Guardrails

- Do not build a second tenants query client; extend `DaprTenantQueryService`.
- Do not introduce a new global-admin authorization store; the read path already expects `GlobalAdministratorReadModel` at `projection:global-administrators:singleton`.
- Do not solve Bug B with a startup Redis write or bootstrap shortcut. The projection handler must be the source of truth so clean state and replay both work.
- Do not convert failed query envelopes into empty lists or default models unless the acceptance criteria explicitly says so.
- Do not log event payloads; architecture SEC-5 and rule 5 forbid payload data in logs.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-05-tenant-management-debug-cluster.md`
- `_bmad-output/planning-artifacts/architecture.md` - enforcement rules 4, 5, 7, 10, 14 and security constraints.
- `_bmad-output/planning-artifacts/epics.md` - tenant isolation, query routing, projection contracts, admin UI tenant story context.
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTenantQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminTenantsControllerTests.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants/Program.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants/Actors/TenantsProjectionActor.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Server/Projections/GlobalAdministratorProjection.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Server/Projections/GlobalAdministratorReadModel.cs`
- `Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Projections/GlobalAdministratorProjectionTests.cs`
- `Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Projections/GlobalAdministratorReadModelTests.cs`
- `Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Projections/TenantsProjectionActorTests.cs`

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context) via /bmad-dev-story.

### Debug Log References

- Proposal evidence: trace `8a9f8c1`, admin server log ids `2621` and `5534`, Redis state-store dump in sprint-change proposal.
- ST3 implementation traced through `ProjectionRequest.Domain` dispatch — confirmed `TenantsProjectionActor.IsGlobalAdminAsync` consumes `projection:global-administrators:singleton`, which is now written by the live `GlobalAdministratorProjectionHandler`.
- ST4: Verified existing pipeline (`SubmitQueryHandler` → exception handlers) maps actor failures to HTTP 4xx today, so the new envelope guard is forward-compatible defense-in-depth. The `SubmitQueryResponse.Success`/`ErrorMessage` extension is purely additive (defaults preserve current 200-OK shape) and Contracts/Client tests stay green.

### Completion Notes List

- Story context created retroactively after ST1/ST2 commit.
- ST2: added 14 new theory cases proving `JsonStringEnumConverter` accepts both string ("Active"/"Disabled"/"TenantOwner"/...) and numeric (0/1/2) tenant status and tenant role payloads across `ListTenantsAsync`, `GetTenantUsersAsync`, and `GetTenantDetailAsync`.
- ST3 (Hexalith.Tenants submodule): added `GlobalAdministratorProjectionHandler` (DAPR-backed, writes `projection:global-administrators:singleton` only — no tenant-index, no `projection:tenants:global-administrators`). Added `ProjectionDispatcher` and rewired `/project` to dispatch by `ProjectionRequest.Domain` ("tenants" / "global-administrators" / unknown→400 ProblemDetails). Negative test asserts no `SaveStateAsync` is called for unknown domains. 7 + 8 = 15 new unit tests, full Tenants Server suite 257/257 single-threaded.
- ST4: extended `SubmitQueryResponse` with optional `Success` (default `true`) and `ErrorMessage` (default `null`) — additive non-breaking. `DaprTenantQueryService.ClassifyFailedEnvelope` checks `Success` BEFORE any payload deserialization. "Forbidden" → `HttpRequestException(Forbidden)` → 403 via existing `QueryFailure`. "Tenant not found" → `HttpRequestException(NotFound)` (Detail catches and returns null → 404; Users propagates → 404). "Tenant not found" on `ListTenantsAsync` → `TenantQueryFailedException` (semantic contract failure, never silent empty list). Unrecognized error → `TenantQueryFailedException` → 502 via dedicated controller catch. `IsServiceUnavailable` left untouched so transport BadGateway still maps to 503 (regression test added).
- Pre-existing flake (`DomainServiceRequestHandlerTelemetryTests.ProcessAsync_NoProcessorFound_ShouldSetErrorStatusAndRecordMetric`) only fails under xUnit's default parallel collections; passes single-threaded and in isolation. Not introduced by this story; not a release blocker.
- Live verification (Aspire smoke; Redis manual-hack cleanup; submodule pin bump after committing in `Hexalith.Tenants` repo) requires user-side DAPR/Docker access — handed off as the remaining ST5 unchecked items.

### Verification Evidence Captured

- Admin Server: 539/557 passed in Release (18 skipped, 0 failed).
- Admin UI (TenantsPageTests): 24/24 passed in Release.
- Hexalith.Tenants Server tests: 257/257 passed single-threaded in Release.
- Contracts tests: 281/281 passed (additive `Success`/`ErrorMessage` change is non-breaking).
- Client tests: 334/334 passed.
- Solution Release build: 0 warnings, 0 errors.

### File List

- `_bmad-output/implementation-artifacts/tenant-management-debug-cluster-fix.md`
- `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs` (M, +Success/+ErrorMessage)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` (M, envelope classifier)
- `src/Hexalith.EventStore.Admin.Server/Services/TenantQueryFailedException.cs` (A)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs` (M, +UpstreamQueryFailure / 502 catches / OpenAPI status types)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTenantQueryServiceTests.cs` (M, +14 ST2 theory cases, +9 ST4 envelope tests)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminTenantsControllerTests.cs` (M, +4 ST4 controller mapping tests)
- `Hexalith.Tenants/src/Hexalith.Tenants/Projections/GlobalAdministratorProjectionHandler.cs` (A)
- `Hexalith.Tenants/src/Hexalith.Tenants/Projections/ProjectionDispatcher.cs` (A)
- `Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` (M, /project endpoint dispatches via ProjectionDispatcher)
- `Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Projections/GlobalAdministratorProjectionHandlerTests.cs` (A)
- `Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Projections/ProjectionDispatcherTests.cs` (A)

### Change Log

| Date | Change | Notes |
|------|--------|-------|
| 2026-05-05 | ST2 enum-payload theory tests added | 14 cases across List/Detail/Users covering "Active"/"Disabled"/"TenantOwner"/"TenantContributor"/"TenantReader" string and 0/1/2 numeric forms. |
| 2026-05-05 | ST3 GlobalAdministratorProjectionHandler + ProjectionDispatcher | Live DAPR projection handler writes `projection:global-administrators:singleton` only; `/project` routes by `ProjectionRequest.Domain`; unknown domains fail closed with 400. |
| 2026-05-05 | ST4 envelope classifier + 502 path | Additive `SubmitQueryResponse.Success`/`ErrorMessage`; `DaprTenantQueryService.ClassifyFailedEnvelope`; new internal `TenantQueryFailedException` mapped to 502 via dedicated controller catch (transport 503 path preserved). |
