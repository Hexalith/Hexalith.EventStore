# Post-Epic Deferred DW11: Admin Action Binding and Projection Detail Contracts

Status: done

Context created: 2026-05-20
Context refreshed: 2026-05-20
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`
Trace evidence:

- Retry: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/fa3fc5fd10b041f1162f2944d18953d9-*`
- Skip: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/b0a9110c494774fec42be2369b39da9f-*`
- Archive: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/54257a9188c24dd9a4ca6791cdf02b82-*`
- Projection detail: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/7ac735d7c7c4ef15fb7ba5791eb5fc46-*`

## Story

As an EventStore operator using Admin UI action and projection pages,
I want dead-letter actions and projection details to use valid backend contracts,
so that operator workflows fail recoverably instead of producing model-binding
500s or unsupported detail navigation.

## Scope

This story covers:

- CC-1 / Issue 9: Dead-letter Retry, Skip, and Archive fail with HTTP 500 before business logic.
- CC-2 / Issue 11: Projection list shows the `tenant-a` / `counter` fixture, but projection detail returns `Projection not found`.

This story does not cover:

- DW12 consistency-check false positives and Blazor dispatcher safety.
- DW13 tenant lifecycle actor timeout investigation.
- DW14 snapshot, compaction, backup, validation, and export deferred UX policy.
- DW15 TypeCatalog disconnect navigation hygiene.
- Real backend success for a visual-only DLQ fixture. If the message does not exist in the product backend, a 404/422 business result is acceptable.

## Implementation Decision

DW11 uses the Admin read-model fallback path for projection detail. Do not add a
new EventStore generic projection-detail endpoint in this story unless this
document is revised first.

Fallback detail is list-fidelity detail: it may expose only data already present
in `ProjectionStatus`. It must preserve that data exactly and leave unknown
detail-only fields empty or explicit minimal defaults.

## Acceptance Criteria

1. Dead-letter Retry, Skip, and Archive accept this JSON body without ASP.NET Core model-validation metadata failure:

   ```json
   { "messageIds": ["manual-dlq-tenant-a-001"] }
   ```

2. `DeadLetterActionRequest` validation metadata is compatible with ASP.NET Core record validation. Use constructor-parameter metadata, for example `[Required] IReadOnlyList<string> MessageIds`, or convert the DTO to an explicit class model. Do not leave `[property: Required]` on a positional record primary-constructor parameter. If any ambiguity remains, prefer a boring explicit class model over a clever record shape.

3. HTTP-level API tests cover Retry, Skip, and Archive by posting real JSON through ASP.NET Core model binding. Direct controller unit tests may supplement this, but are not sufficient for this defect. Required payload cases:
   - valid body `{ "messageIds": ["manual-dlq-tenant-a-001"] }` reaches `IDeadLetterCommandService`;
   - missing `messageIds` returns controlled `400` `ProblemDetails`;
   - `messageIds: null` returns controlled `400` `ProblemDetails`;
   - empty `messageIds: []` returns controlled `400` `ProblemDetails`;
   - `messageIds` containing empty or whitespace-only values returns controlled `400` `ProblemDetails`;
   - duplicate IDs are either de-duplicated before service invocation or preserved intentionally with a test documenting the chosen behavior;
   - no case produces metadata/model-binding HTTP 500.

4. If `manual-dlq-tenant-a-001` is only a visual fixture and no real backend message exists, Retry, Skip, and Archive return a recoverable business failure such as 404 or 422. They must not return model-binding 500, raw stack traces, secrets, or connection strings.

5. Projection detail source of truth for this story is mandatory: use an Admin read-model fallback built from the admin projection index when EventStore does not support generic projection detail. This fallback is not authoritative projection actor state; it is an operator-facing Admin detail read model that keeps list-to-detail navigation useful without expanding the EventStore public contract.

6. `DaprProjectionQueryService.GetProjectionDetailAsync` falls back only for known detail-missing or unsupported responses from EventStore: `404 NotFound`, `405 MethodNotAllowed`, or `501 NotImplemented`. It must not fallback on `401`, `403`, `409`, `500`, timeout/cancellation, malformed JSON, serialization failure, or any unexpected infrastructure failure. Those failures must remain visible through the existing error path and logs.

7. Projection identity matching is explicit:
   - route values are separate `tenantId` and `projectionName` segments, e.g. `tenant-a` and `counter`;
   - lookup checks `admin:projections:{tenantId}` first;
   - lookup may fallback to `admin:projections:all` only when the entry is tenant-neutral (`TenantId == "all"`) or matches the requested tenant;
   - tenant-specific entries take precedence over `all`;
   - projection name matching is ordinal unless existing Admin API behavior already defines otherwise;
   - never return a detail for a different tenant.

8. Fallback `ProjectionDetail` uses only known index data and deliberate defaults:
   - preserve projection name, tenant id, status, lag, throughput, error count, last processed position, and last processed timestamp from `ProjectionStatus`;
   - preserve the indexed `ProjectionStatus.Status`; do not force `Error` merely because EventStore generic detail is unsupported;
   - set detail-only fields such as errors, configuration, and subscribed event types to empty or explicit minimal values;
   - use `{}` for fallback `Configuration` unless an existing `ProjectionDetail` convention requires a different empty JSON object representation;
   - do not fabricate actor state, checkpoint state, subscribed event types, or configuration that the index does not contain;
   - if the projection is absent from both tenant and allowed fallback indexes, return a clear not-found result rather than a synthetic success.

9. When projection detail fallback is used, emit a structured informational log with tenant id, projection name, upstream status code, and fallback source key. Do not log payloads, configuration bodies, state bodies, raw DAPR values, secrets, or connection strings.

10. Tests prove list and detail behavior for the `tenant-a` / `counter` fixture shape from the manual evidence:
   - list source key: `admin:projections:tenant-a`
   - name: `counter`
   - lag: `0`
   - last processed position: about `18`
   - clicking/opening detail no longer returns `Projection not found` solely because the EventStore generic detail endpoint is absent.

11. Manual retest records Issue 9 and Issue 11 after implementation:

   ```text
   Issue 9: OK
   Issue 11: OK
   Issue 11 detail fields visible:
   - Tenant = tenant-a
   - Projection = counter
   - Status = Running
   - Lag = 0
   - Last Position = 18
   - Fallback/limited detail behavior acceptable: OK
   Notes / messages exacts:
   - Issue 9 Retry/Skip/Archive all returned recoverable 404 NotFound for the visual fixture `manual-dlq-tenant-a-001`; no model-binding 500.
   - Issue 11 detail displayed Running, Lag 0, Throughput 0,0 /s, Errors 0, Last Position 18, `{}`, no subscribed event types, and no recorded errors.
   - Projection detail action buttons remain visible on fallback detail. Pause trace: `a696b60d04dd91c7c8c7c4e57530f98a`; Reset operation: `01KS2VTP9JHT2CCX5KPOPAY91H`; Replay showed `Replay initiated`. This is noted as projection-lifecycle follow-up observation, not a DW11 blocker.
   ```

## Tasks / Subtasks

- [x] Write failing HTTP-level API tests for valid, missing, null, empty, whitespace-only, and duplicate `messageIds` bodies on Retry, Skip, and Archive. (AC: 1, 3)
- [x] Fix `DeadLetterActionRequest` validation metadata. (AC: 1, 2)
- [x] Confirm visual-fixture DLQ misses map to a recoverable 404/422-style business result. (AC: 4)
- [x] Implement mandatory Admin read-model fallback for projection detail. (AC: 5, 6, 7, 8, 9)
- [x] Write failing projection-detail tests for success, known 404/405/501 fallback, non-fallback failures, missing projection, tenant index precedence, `all` fallback, tenant isolation, and fallback logging. (AC: 5, 6, 7, 8, 9, 10)
- [x] Verify the Admin UI list-to-detail path for `tenant-a` / `counter` opens useful detail rather than `Projection not found`. (AC: 10)
- [x] Run targeted tests and record results. (AC: 3, 10)
- [x] Restart Aspire only if needed for manual validation, then rerun Issue 9 and Issue 11 checks from the source evidence. (AC: 11)

### Review Findings

- [x] [Review][Patch] Tenant-scoped projection fallback can return a row for the wrong tenant [src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:188]
- [x] [Review][Patch] Projection fallback logging infers the source key from row tenant instead of the state key actually read [src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:153]
- [x] [Review][Patch] Projection fallback state reads are not bounded by the configured invocation timeout [src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:118]
- [x] [Review][Patch] Manual retest evidence for Issue 9 and Issue 11 is still not recorded [_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md:867]

Preferred implementation sequence:

1. Fix and test `DeadLetterActionRequest` binding.
2. Add projection fallback tests around `DaprProjectionQueryService`.
3. Implement fallback in `DaprProjectionQueryService`.
4. Change UI only if fallback detail renders misleadingly with existing components.

## Dev Notes

### Current Failure Evidence

- Dead-letter actions reach `eventstore-admin`:
  - `POST /api/v1/admin/dead-letters/tenant-a/retry`
  - `POST /api/v1/admin/dead-letters/tenant-a/skip`
  - `POST /api/v1/admin/dead-letters/tenant-a/archive`
- All three fail before service logic with:

  ```text
  InvalidOperationException: Record type 'Hexalith.EventStore.Admin.Server.Models.DeadLetterActionRequest' has validation metadata defined on property 'MessageIds' that will be ignored. 'MessageIds' is a parameter in the record primary constructor and validation metadata must be associated with the constructor parameter.
  ```

- Projection detail evidence:
  - Admin UI calls `GET https://localhost:8091/api/v1/admin/projections/tenant-a/counter`.
  - Admin Server invokes EventStore at `api/v1/admin/projections/tenant-a/counter`.
  - EventStore returns `404 Not Found`.
  - `DaprProjectionQueryService` calls `EnsureSuccessStatusCode()`, so the existing `CreateEmptyProjectionDetail(...)` fallback is not reached.
  - EventStore currently exposes rebuild lifecycle routes such as `GET /api/v1/admin/projections/{tenantId}/{projectionName}/rebuild-status`; the observed detail route is absent.

### Files To Read Before Coding

- `src/Hexalith.EventStore.Admin.Server/Models/DeadLetterActionRequest.cs`
  - Current state: positional record with `[property: Required] IReadOnlyList<string> MessageIds`.
  - Change: move validation metadata to the constructor parameter or convert to a class model.
  - Preserve: JSON body shape `messageIds`.

- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs`
  - Current state: Retry, Skip, Archive call `IDeadLetterCommandService` and map `AdminOperationResult` errors to 404/403/422/500.
  - Change: only if tests show body validation or null/empty input needs clearer controller behavior.
  - Preserve: authorization policies, tenant filter, `AdminOperationResult` mapping, `ProblemDetails` correlation extension.

- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs`
  - Current state: list reads DAPR state-store indexes; detail delegates to EventStore via DAPR service invocation and calls `EnsureSuccessStatusCode()`.
  - Change: remove the unconditional `EnsureSuccessStatusCode()` trap for known 404/405/501 unsupported responses. Reuse the existing state-store projection index to construct fallback detail for `counter`.
  - Preserve: JWT forwarding, DAPR invocation timeout, tenant-scoped and `all` index fallback behavior in `ListProjectionsAsync`.
  - Do not catch `HttpRequestException` broadly and turn all failures into fallback success.
  - Record fallback use with structured informational logging. Keep the log payload-free and include only tenant id, projection name, upstream status code, and fallback source key.

- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs`
  - Current state: Admin.Server already exposes `GET /api/v1/admin/projections/{tenantId}/{projectionName}` and maps null detail to 404.
  - Change: usually none if the service returns a structured fallback/detail. If the service intentionally returns null for unsupported detail, keep 404 recoverable.
  - Preserve: read-only authorization and `AdminTenantAuthorizationFilter`.

- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs`
  - Current state: EventStore owns projection rebuild lifecycle endpoints under `/api/v1/admin/projections`, including `/rebuild-status`, pause, resume, reset, replay, cancel, retry.
  - Change: only if choosing the EventStore-detail-endpoint option. Do not overload rebuild-status as generic detail without clear tests.
  - Preserve: GlobalAdministrator guard, canonical tenant/projection validation, rebuild checkpoint semantics, `ProblemDetails` reason-code behavior.

- `src/Hexalith.EventStore.Admin.UI/Pages/Projections.razor`
  - Current state: row click always opens `ProjectionDetailPanel` and writes `tenant` plus `projection` query parameters.
  - Change: only if choosing the "unsupported detail UI" option.
  - Preserve: list rendering, filters, deep links, refresh, existing tenant option provider behavior.

- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor`
  - Current state: null detail shows `Projection not found` and invokes `OnClose`; detail supports pause/resume/reset/replay.
  - Change: only if the UI must display an explicit unsupported-detail state.
  - Preserve: role-gated controls, polling behavior, existing operation dialogs.

### Existing Test Anchors

- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDeadLettersControllerTests.cs`
  - Current tests instantiate the controller directly, which does not exercise ASP.NET Core model binding. Add an MVC/API-style test if needed so the record metadata bug is actually caught.

- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprProjectionQueryServiceTests.cs`
  - Current tests cover list fallback, null JSON fallback, success detail, cancellation, and DAPR list errors.
  - Add tests for HTTP 404/unsupported response from EventStore and for constructing or mapping detail from the projection index.

- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminProjectionsControllerTests.cs`
  - Current tests cover null detail -> 404 and projection command result mapping.
  - Extend only if the controller behavior changes.

- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ProjectionsPageTests.cs` and `tests/Hexalith.EventStore.Admin.UI.Tests/Components/ProjectionDetailPanelTests.cs`
  - Use if UI behavior changes. If the fix stays in Admin.Server, UI test updates are optional unless existing UI behavior is misleading.

### Dev Agent Record Expectations

- Record the chosen fallback behavior, including why no new EventStore generic projection-detail endpoint was added.
- Record the duplicate-ID behavior chosen for `DeadLetterActionRequest` (`de-duplicated` or `preserved intentionally`) with the test that proves it.
- Record whether the UI required changes after fallback detail was available, or why existing components were sufficient.

### Architecture Guardrails

- Admin tooling uses a shared Admin API; Web UI, CLI, and MCP must not access DAPR directly. Source: `_bmad-output/planning-artifacts/architecture.md` ADR-P4 and PRD FR79.
- Admin.Server may read admin indexes through DAPR state store; writes and lifecycle actions go through EventStore service invocation so security and validation are not bypassed.
- Admin API errors use RFC 7807 `ProblemDetails`; do not introduce custom error shapes.
- Preserve DAPR portability and role-based access: PRD NFR44 requires DAPR-only admin data access; NFR46 requires Admin API RBAC.
- Projection-related public contracts are sensitive because Epic 22 made query/projection adapter behavior a platform boundary. Do not create a detail endpoint whose payload conflicts with `Hexalith.EventStore.Admin.Abstractions.Models.Projections.ProjectionDetail`.
- Do not add a new EventStore generic projection-detail endpoint in this story unless the Admin read-model fallback is proven impossible and the story is updated first. Adding the endpoint expands the public projection/admin contract surface and needs a deliberate architecture decision.
- Do not leak event payloads, command payloads, secrets, stack traces, or connection strings in UI messages, logs intended for operators, or `ProblemDetails`.
- Do not touch Aspire AppHost, Keycloak, DAPR component scopes, or submodules for this story unless implementation proves a direct dependency. Current `git status` already shows a modified `Hexalith.Tenants` submodule pointer; do not revert or modify it as part of DW11.

### Latest Technical Note

- Official ASP.NET Core model-binding documentation for .NET 10 says record-type validation and binding metadata is read from constructor parameters, and metadata on properties is ignored. This directly matches the observed `DeadLetterActionRequest` failure. Source: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0#record-types-validation-and-binding-metadata

### Validation Commands

Run targeted tests first:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~AdminDeadLettersControllerTests|FullyQualifiedName~AdminProjectionsControllerTests|FullyQualifiedName~DaprProjectionQueryServiceTests" -m:1
```

If the HTTP-level model-binding tests live in a different Admin.Server integration-test class, include that class in the same targeted run. The gating point is not the class name; it is that at least one test posts JSON through ASP.NET Core MVC binding for each dead-letter action.

If UI behavior changes, run the UI slice:

```powershell
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~ProjectionsPageTests|FullyQualifiedName~ProjectionDetailPanelTests" -m:1
```

After implementation, restart Aspire only if validating through the browser or changed runtime-hosted projects are already running:

```powershell
$env:EnableKeycloak = 'false'
aspire run --project .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Manual validation source:

- `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`, sections Issue 9 and Issue 11.

## Project Context Reference

Loaded project context: `_bmad-output/project-context.md`.

Rules especially relevant to this story:

- Target .NET SDK `10.0.300` and `net10.0`; warnings are treated as errors.
- Use xUnit v3, Shouldly, and NSubstitute patterns already present in the test projects.
- Prefer existing helpers and fakes before adding new infrastructure.
- Admin UI work stays in Blazor and Fluent UI v5 patterns.
- Validate incrementally with narrow project tests before broader test runs.
- Do not initialize nested submodules.

## Handoff Status

Ultimate context engine analysis completed - comprehensive developer guide created.

Party-mode review hardening applied while remaining ready-for-dev: mandatory Admin read-model projection detail fallback, narrow fallback status boundary, HTTP-level model-binding tests, invalid-payload 400 requirements, projection identity precedence, tenant isolation, and non-fabricated fallback detail fields.

Advanced elicitation hardening applied while remaining ready-for-dev: explicit implementation decision, list-fidelity fallback wording, whitespace/duplicate message-id validation, ordinal projection-name matching, and preserving indexed projection status during fallback.

Second elicitation simplification applied while remaining ready-for-dev: fallback logging, `{}` fallback configuration convention, Dev Agent Record decision notes, richer Issue 11 manual evidence fields, and preferred implementation sequence.

The story is ready for dev. The implementation should be test-first because both
defects have narrow, reproducible failure modes and existing test files nearby.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context) — bmad-dev-story workflow.

### Debug Log References

- 2026-05-20 — focused Admin.Server test slice (AdminDeadLettersControllerTests | AdminProjectionsControllerTests | DaprProjectionQueryServiceTests | AdminDeadLettersHttpBindingTests): 61/61 passed, 0 warnings, 0 errors.
- 2026-05-20 — full `tests/Hexalith.EventStore.Admin.Server.Tests` (Release): 671 passed, 18 skipped (pre-existing AT DD/Tier-2 placeholders), 0 failures.
- 2026-05-20 — focused Admin.UI slice (ProjectionsPageTests | ProjectionDetailPanelTests): 19/19 passed.
- 2026-05-20 — Release build of Admin.Server, Admin.Server.Host, Admin.UI, Admin.Mcp, Admin.Cli: 0 warnings / 0 errors. Solution-wide `dotnet build` is environment-blocked by a running `Hexalith.Tenants` process holding output DLLs; not caused by these changes.

### Completion Notes List

- AC1, AC2 — `DeadLetterActionRequest` is now an explicit class with `[Required] IReadOnlyList<string>? MessageIds` and `IValidatableObject`. The `[property: Required]` on a positional record was the root cause of the ASP.NET Core model-binding 500 in Issue 9. Chose the boring class shape over constructor-parameter metadata for clarity (per AC2 guidance).
- AC3 — Eight HTTP-level integration tests per dead-letter action (24 total cases) post real JSON through ASP.NET Core MVC binding in `AdminDeadLettersHttpBindingTests`. Covered: valid, missing body, `messageIds: null`, `messageIds: []`, whitespace, empty-string element, duplicate de-dup, and visual-fixture 404 mapping. Assertions explicitly check the body does NOT contain `"validation metadata"` or `InvalidOperationException`, so a regression to the record metadata bug fails loudly.
- **Duplicate-ID decision** — De-duplicate before service invocation. `AdminDeadLettersController.NormalizeMessageIds` removes ordinal duplicates while preserving operator-supplied order. Proven by `DuplicateMessageIds_AreDeDuplicated_BeforeServiceInvocation` for each of Retry/Skip/Archive.
- AC4 — `DaprDeadLetterCommandService.GetErrorCode` now canonicalises HTTP statuses: 404 → `NotFound`, 401/403 → `Unauthorized`, 400/405/409/410/422 → `InvalidOperation`. The Admin controller mapping table picks these up and surfaces 404 / 403 / 422 ProblemDetails instead of falling through to 500. Verified by `VisualFixtureMissBackend404_MapsToRecoverable4xx` and `VisualFixtureMissBackendInvalidOperation_MapsTo422` against the manual-dlq-tenant-a-001 fixture id.
- AC5–AC9 — Implemented the Admin read-model fallback path in `DaprProjectionQueryService.GetProjectionDetailAsync`. The unconditional `EnsureSuccessStatusCode()` trap is removed; instead, the response status is inspected first: only `404 NotFound`, `405 MethodNotAllowed`, and `501 NotImplemented` trigger fallback (`IsDetailUnsupportedStatus`). All other failures (401/403/409/500/timeout/serialization errors) bubble up through the existing exception path, proven by the parameterised `GetProjectionDetailAsync_DoesNotFallback_OnNonAllowedStatuses` test which also asserts the DAPR index is never consulted on those statuses.
- **Projection-detail-endpoint decision** — Per the story Implementation Decision and Architecture Guardrails, did NOT add a new EventStore generic projection-detail endpoint. The Admin read-model fallback is sufficient and keeps the EventStore public projection contract surface unchanged.
- Tenant identity precedence: the lookup checks `admin:projections:{tenantId}` first; only on miss does it consult `admin:projections:all`, and even there it filters strictly to tenant-neutral (`TenantId == "all"`) or tenant-matching rows. `GetProjectionDetailAsync_DoesNotLeakTenant_WhenAllIndexHoldsDifferentTenant` proves a `tenant-b` row in `all` is never returned for a `tenant-a` request. Projection-name matching is ordinal.
- AC8 — Fallback `ProjectionDetail` preserves the indexed `ProjectionStatus.Status` (no forced `Error`). Detail-only fields use deliberate defaults: `Errors = []`, `Configuration = "{}"` (exposed as `DaprProjectionQueryService.FallbackEmptyConfiguration` constant), `SubscribedEventTypes = []`. The Configuration JSON is empty object literal per AC8 convention. Index miss returns `null` (clear not-found), which `AdminProjectionsController.GetProjectionDetail` maps to a recoverable 404 ProblemDetails.
- AC9 — Structured informational log on every fallback usage: tenant id, projection name, upstream HTTP status code, and fallback source key (`admin:projections:tenant-a` or `admin:projections:all`). A separate "fallback miss" info log is emitted when the projection is absent from both indexes. Logs are payload-free; no configuration bodies, state, raw DAPR values, secrets, or connection strings. Verified by `GetProjectionDetailAsync_EmitsStructuredFallbackLog` and `GetProjectionDetailAsync_EmitsStructuredFallbackMissLog_WhenIndexEmpty` using `RecordingLogger<T>`.
- AC10 — `GetProjectionDetailAsync_FallsBackToTenantIndex_OnUnsupportedDetailStatus` exercises the manual evidence fixture shape (`admin:projections:tenant-a`, name `counter`, lag `0`, last processed position `18`, status `Running`) end-to-end through 404 / 405 / 501. The resulting `ProjectionDetail` is non-null, so `AdminProjectionApiClient` returns it to `ProjectionDetailPanel`, which renders the operator-useful detail instead of the `Projection not found` empty state.
- Interface change: `IProjectionQueryService.GetProjectionDetailAsync` now returns `Task<ProjectionDetail?>`. `AdminProjectionApiClient` (UI) was already typed `ProjectionDetail?`, so no UI code change was required. The Admin.Server controller already handled `result is null` and now is annotated correctly.
- **UI change decision** — None required. The existing `ProjectionDetailPanel` renders any non-null `ProjectionDetail` correctly; the fallback path returns a populated detail, so the misleading `Projection not found` view is no longer reached for the `tenant-a` / `counter` fixture. Existing 19 `ProjectionsPageTests` + `ProjectionDetailPanelTests` continue to pass.
- AC11 — Manual retest completed after Aspire restart, Redis `FLUSHALL`, and fixture reapplication. Issue 9 is OK: Retry, Skip, and Archive return recoverable `404 NotFound` for the visual fixture `manual-dlq-tenant-a-001`, with no model-binding 500. Issue 11 is OK: projection detail for `tenant-a` / `counter` renders the fallback detail with status `Running`, lag `0`, last position `18`, `{}` configuration, no subscribed event types, and no errors. Projection lifecycle action buttons remain visible on fallback detail; Pause trace `a696b60d04dd91c7c8c7c4e57530f98a`, Reset operation `01KS2VTP9JHT2CCX5KPOPAY91H`, and Replay initiated are recorded as a non-blocking follow-up observation outside DW11's acceptance scope.

### File List

Source (modified):

- `src/Hexalith.EventStore.Admin.Abstractions/Services/IProjectionQueryService.cs` — return type changed to `Task<ProjectionDetail?>`.
- `src/Hexalith.EventStore.Admin.Server/Models/DeadLetterActionRequest.cs` — converted from positional record to explicit class with `IValidatableObject`.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs` — added `NormalizeMessageIds` de-duplication helper; controller pipes deduped ids to the command service.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs` — annotated `result` as nullable to match the updated interface.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprDeadLetterCommandService.cs` — `GetErrorCode` canonicalises HTTP statuses to admin error-code strings (`NotFound`, `Unauthorized`, `InvalidOperation`).
- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs` — implemented Admin read-model fallback for known unsupported statuses (404/405/501); added structured logging; ordinal projection-name matching; tenant precedence; never returns a different tenant.

Tests (added):

- `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminDeadLettersHttpBindingTests.cs` — 24 HTTP-level binding cases across Retry/Skip/Archive: valid, missing, null, empty, whitespace, empty-string, duplicates, and visual-fixture miss mapping.

Tests (modified):

- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDeadLettersControllerTests.cs` — switched to object-initializer syntax for the new class DTO.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprProjectionQueryServiceTests.cs` — rewrote `GetProjectionDetailAsync_ReturnsFallback_WhenEventStoreUnavailable` for the new null-on-200/null contract; added 8 new tests covering 404/405/501 fallback, tenant precedence, `all` fallback, tenant isolation, both-empty indexes, non-allowed statuses (401/403/409/500/502/503), and two structured-log assertions (fallback used + fallback miss).
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprDeadLetterCommandServiceTests.cs` — updated `InvokePost_MapsHttpStatusCode_FromHttpRequestException` to expect the canonical `NotFound` code.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprDeadLetterServiceTests.cs` — updated `RetryDeadLettersAsync_MapsHttpStatusCode_WhenRequestFails` to expect the canonical `Unauthorized` code.
- `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminTestHost.cs` — switched DAPR-backed mock service lifetimes from scoped to singleton so HTTP-level integration tests can fetch and configure the same mock instance the controller sees, and exposed `GetService<T>()`.

## Change Log

- 2026-05-20 - Code review and manual retest closure:
  - Fixed review findings for tenant-scoped fallback filtering, exact fallback source-key logging, and bounded fallback state reads.
  - Recorded manual Issue 9 / Issue 11 retest evidence after Aspire restart, Redis flush, and fixture reapplication.
  - Status: review/in-progress -> done.

- 2026-05-20 — DW11 implementation pass:
  - Fixed `DeadLetterActionRequest` record metadata defect → no more ASP.NET Core model-binding 500s on Retry/Skip/Archive.
  - Added 24 HTTP-level integration test cases proving controlled 400 ProblemDetails for every invalid body shape and OK delegation for the valid shape.
  - Canonicalised `DaprDeadLetterCommandService` HTTP-status mapping so visual-fixture DLQ misses surface as recoverable 404/422 ProblemDetails.
  - Implemented `DaprProjectionQueryService` Admin read-model fallback for known unsupported detail statuses (404/405/501) with tenant precedence, tenant isolation, ordinal projection-name matching, list-fidelity detail (preserved indexed status, lag, throughput, error count, last processed position/timestamp), `{}` Configuration default, and structured informational logging on fallback used + fallback miss.
  - Made `IProjectionQueryService.GetProjectionDetailAsync` return `Task<ProjectionDetail?>` to express the "not found" outcome explicitly; UI client was already nullable-aware so no UI change required.
  - Status: ready-for-dev → review.
