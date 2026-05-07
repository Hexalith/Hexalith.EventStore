# Story: admin-ui-operator-action-and-dev-role-testability-fix

Status: ready-for-dev

Context created: 2026-05-07
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
Triggering evidence: `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
Scope: Issues #9 and #13 only.

## Story

As an EventStore operator and manual tester using the Admin UI,
I want operator action dialogs to recover cleanly after backend failures and a development-only way to switch between ReadOnly, Operator, and Admin roles,
so that I can trust failed actions, retry safely, and validate RBAC behavior without forging JWTs by hand.

## Issue Traceability

| Issue | Failing symptom | Covered by | Required evidence |
| --- | --- | --- | --- |
| #9 | `/health/dead-letters` Retry, Skip, and Archive confirmation buttons can stay in a loading state after backend failure, and failure detail is only surfaced as generic toast copy. | AC1, AC2, AC3, AC6 | bUnit action-failure tests for all three actions; error-detail extraction tests; manual dead-letter failure evidence. |
| #13 | In `EnableKeycloak=false` development mode, the manual guide references a role toggle that is not visible or discoverable, blocking visual ReadOnly/Operator/Admin guard validation across seven sections. | AC4, AC5, AC6, AC7 | MainLayout/header role-switch tests; token-provider role override tests; manual guide update; role-guard page evidence. |

## Operator Action Failure Contract

Operator action UX must be deterministic under success, partial failure, full failure, auth failure, validation rejection, backend outage, and cancellation.

| Scenario | Required result |
| --- | --- |
| Full success | Close the dialog, clear selection, reload data, and show success feedback. |
| Partial tenant-group failure | Close the dialog only after reporting the successful and failed counts; failed message IDs must remain discoverable in the feedback path or the refreshed grid. |
| Full backend failure | Keep the dialog open, stop the spinner, keep the original selection, and show inline error details in the dialog. |
| 401 or 403 | Stop the spinner, show inline auth-specific copy, keep selection, and do not hide role-guard failure behind a generic backend message. |
| 422 or `AdminOperationResult.Success == false` | Stop the spinner, show the operation message/error code, and list affected message IDs or tenant groups when available. |
| 503, timeout, network error, or malformed response | Stop the spinner, show status/category and trace/correlation evidence when available, and keep the action recoverable. |
| Cancellation caused by component disposal/navigation | Do not show a false success or stale error after disposal. |

Do not rely on toast-only feedback for failed destructive or replay actions. Toasts can supplement the dialog, but the dialog is the durable failure surface while it remains open.

## Development Role Switch Contract

The role switcher is a development/testability aid, not a production authorization feature.

Rules:

- It must be visible only when the Admin UI is running in Development and no Keycloak authority is configured, or behind an equivalently explicit non-production flag.
- It must never weaken Admin.Server policy evaluation. Server endpoints continue to authorize based on the JWT presented to `AdminApi`.
- It must update both UI role rendering and outgoing Admin API bearer tokens, not only local component visibility.
- It must use the canonical claim `eventstore:admin-role` with values `ReadOnly`, `Operator`, and `Admin`.
- It must make the active role visible in the header and announce changes through an accessible status region.
- It must persist only for the current developer browser/session unless a documented local-storage key is deliberately chosen and tested.
- It must be absent, disabled, or replaced with documentation when Keycloak/production auth is active.

## Acceptance Criteria

1. **Dead-letter action dialogs always exit busy state on failure.**
   - Given selected dead-letter entries on `/health/dead-letters`
   - When Retry, Skip, or Archive is confirmed and the backend returns `Success == false`, HTTP 4xx/5xx, timeout, or a wrapped `ServiceUnavailableException`
   - Then the confirmation button stops showing its spinner.
   - And Cancel/close behavior is recoverable.
   - And the selected message IDs remain selected unless the action had partial success and the implementation explicitly records which rows were completed.
   - And no failed path leaves `_isOperating` or equivalent operation state stuck after the awaited action completes.

2. **Dead-letter failures are visible inside the dialog.**
   - Given an action failure from Retry, Skip, or Archive
   - Then the dialog shows an inline error panel with:
     - action name;
     - affected tenant IDs and message IDs, with long IDs truncated only visually;
     - HTTP status or operation error code when available;
     - backend message/category;
     - trace ID, correlation ID, or operation ID when available.
   - And raw stack traces, connection strings, signing keys, bearer tokens, Redis connection details, and unbounded response bodies are not rendered.
   - And a toast may still be emitted, but it is not the only failure evidence.

3. **Other operator/admin action dialogs are audited for the same failure-state pattern.**
   - The developer must inspect Admin UI action dialogs for snapshots, compaction, backups, consistency, tenants, projections, and any shared modal/action component.
   - If the same stuck busy-state or toast-only destructive failure pattern exists and can be fixed locally without expanding scope, apply the same pattern.
   - If the fix would require upstream endpoint design or product decisions, record a deferred item in `deferred-work.md` with owner, reason, and target story.
   - Do not implement snapshot, compaction, backup, projection, tenant lifecycle, or consistency business behavior in this story.

4. **Development-only role switcher is discoverable and gated.**
   - Given `ASPNETCORE_ENVIRONMENT=Development` and no configured `EventStore:Authentication:Authority`
   - When the Admin UI header renders
   - Then a compact role selector is visible near the existing status/theme controls.
   - And it offers exactly `ReadOnly`, `Operator`, and `Admin`.
   - And the active role is visible to sighted users and exposed through an accessible label.
   - And the selector is absent or non-interactive when Keycloak authority is configured or when the app is not running in Development.

5. **Role switching updates UI guards and Admin API tokens consistently.**
   - Given the user switches to `ReadOnly`
   - Then `AuthorizedView MinimumRole="Operator"` and `MinimumRole="Admin"` content is hidden after the switch.
   - And outgoing Admin API calls use a JWT containing `eventstore:admin-role = ReadOnly`.
   - Given the user switches to `Operator`
   - Then Operator actions such as dead-letter Retry/Skip/Archive and consistency Run Check are visible, but Admin-only navigation/actions remain hidden.
   - And outgoing Admin API calls use `eventstore:admin-role = Operator`.
   - Given the user switches to `Admin`
   - Then Admin-only navigation/actions such as Settings, backup/admin operations, and tenant admin controls are visible according to existing guards.
   - And outgoing Admin API calls use `eventstore:admin-role = Admin`.
   - Existing tenant/domain/permission claims from the development token must be preserved.

6. **Automated tests pin the action and role-switch contracts.**
   - bUnit tests cover Retry, Skip, and Archive full failure:
     - spinner appears while pending;
     - spinner stops after failure;
     - inline error panel is rendered;
     - selection is preserved.
   - API-client tests or focused unit tests cover problem-detail/error parsing for 401, 403, 422, 503, and generic HTTP failure where available.
   - MainLayout/header tests cover role selector visibility, active role text, and absence in non-dev/Keycloak modes.
   - `AdminApiAccessTokenProvider` or a new role-state service has tests proving token regeneration with the selected role and cache invalidation after role changes.
   - `AuthorizedView` or integration-style UI tests prove role updates refresh protected content in the same circuit/session.

7. **Manual-test guide and evidence are updated.**
   - Update the manual Admin UI test guide section that currently references the missing role toggle.
   - Document exact dev-mode behavior, including where the selector appears and which pages should change for each role.
   - Capture manual or Playwright evidence for:
     - ReadOnly hides `/health/dead-letters` action buttons and other Operator/Admin-only controls;
     - Operator shows dead-letter actions and consistency Run Check but not Admin-only controls;
     - Admin shows Admin-only navigation/actions;
     - failed Retry/Skip/Archive dialogs return to idle and show inline failure details.

## Tasks / Subtasks

- [ ] **ST1 - Dead-letter action failure-state audit and model.** (AC: 1, 2)
  - [ ] Review `DeadLetters.razor` `ExecuteBulkActionAsync` and dialog render branches.
  - [ ] Introduce a small dialog failure model, such as `BulkActionFailureState`, carrying action name, failed tenant/message IDs, status/error code, user-safe message, and trace/operation/correlation ID.
  - [ ] Reset this model when a dialog opens and when the action succeeds.
  - [ ] Ensure all awaited branches end in a single `finally` that clears operation state unless the component is disposed.

- [ ] **ST2 - Inline error rendering for Retry, Skip, and Archive.** (AC: 1, 2)
  - [ ] Render the failure model inside each confirmation dialog using existing `IssueBanner`/status styling where practical.
  - [ ] Keep selection after full failure.
  - [ ] Preserve current success and partial-success behavior unless tests prove partial-success selection handling is misleading.
  - [ ] Add safe truncation and redaction for long IDs/messages.

- [ ] **ST3 - Error-detail plumbing.** (AC: 2, 6)
  - [ ] Audit `AdminDeadLetterApiClient.HandleErrorStatusAsync`.
  - [ ] Preserve status code and parse RFC 7807 fields (`title`, `detail`, `status`, `traceId`/`traceId` extension if present) into a typed UI-safe exception or result.
  - [ ] Preserve `AdminOperationResult.OperationId` and `ErrorCode` for non-success results.
  - [ ] Do not expose raw response bodies directly to the UI.

- [ ] **ST4 - Operator/admin dialog audit.** (AC: 3)
  - [ ] Inspect pages/components: `Snapshots.razor`, `Compaction.razor`, `Backups.razor`, `Consistency.razor`, `Tenants.razor`, `Projections.razor`, and any shared action modal helpers.
  - [ ] Apply the same recoverable busy-state pattern where the change is local and low risk.
  - [ ] Record deferred items for broader endpoint/product work.

- [ ] **ST5 - Development role state and token regeneration.** (AC: 4, 5, 6)
  - [ ] Add a scoped role-state service, or extend the token provider with a safe dev-only override API.
  - [ ] Make `AdminApiAccessTokenProvider.CreateDevelopmentToken` use the selected role instead of hardcoding `Admin`.
  - [ ] Invalidate the token cache when the selected role changes.
  - [ ] Notify `AuthenticationStateProvider` subscribers so UI guards refresh in the same session.
  - [ ] Preserve existing dev token issuer, audience, subject, tenants, domains, permissions, and optional `global_admin` behavior unless the selected role requires global admin to be disabled for ReadOnly/Operator.

- [ ] **ST6 - Header role selector.** (AC: 4, 5)
  - [ ] Add a compact role selector to `MainLayout.razor` near `HeaderStatusIndicator` and `ThemeToggle`.
  - [ ] Gate it to Development plus no Keycloak authority, or a clearly named non-production config flag.
  - [ ] Use Fluent UI v5-compatible components already in the app (`FluentSelect`, `FluentOption`, `FluentButton`, or equivalent).
  - [ ] Provide accessible label and polite status announcement after role changes.

- [ ] **ST7 - Tests and manual guide evidence.** (AC: 6, 7)
  - [ ] Add/extend `DeadLettersPageTests`.
  - [ ] Add/extend `MainLayoutTests`, `AdminUserContextTests`, `AdminApiAccessTokenProvider` tests, and role-guard tests as needed.
  - [ ] Update the manual Admin UI test guide role-switch instructions.
  - [ ] Capture evidence under `_bmad-output/test-artifacts/` or append concise evidence to this story.

## Developer Notes

Current observations from story creation:

- `DeadLetters.razor` already sets `_isOperating = false` in a `finally` in `ExecuteBulkActionAsync`; the manual failure may be caused by a render/update gap, an exception path not represented in tests, dialog state not repainting, or the lack of inline failure state making the dialog look unrecovered. Reproduce with a pending/failing task before patching.
- `DeadLetters.razor` currently catches per-tenant exceptions and converts them into `lastError = ex.Message`, then shows only `ToastService.ShowErrorAsync(...)` on full failure. There is no dialog-local failure model.
- `AdminDeadLetterApiClient.HandleErrorStatusAsync` currently parses 422 `detail` only, maps 401/403/503 to typed exceptions, and maps other statuses to `HttpRequestException` with status code but without problem-detail fields.
- `MainLayout.razor` renders header status and theme controls but no role selector.
- `AdminApiAccessTokenProvider.CreateDevelopmentToken` hardcodes `[AdminClaimTypes.Role] = "Admin"`.
- `TokenAuthenticationStateProvider` parses the token each time `GetAuthenticationStateAsync` is called but has no role-change notification path today.
- `AuthorizedView.razor` computes `_isAuthorized` in `OnInitializedAsync`; if role switching must update already-rendered content, it may need to subscribe to auth-state changes or recompute when the authentication state changes.
- `AdminUserContext.GetRoleAsync` reads `eventstore:admin-role` first and falls back to `global_admin`/role claims.
- The canonical roles live in `AdminRole` with ordering `ReadOnly < Operator < Admin`.

Local package context:

- Admin UI uses `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.2-26098.1`.
- Admin UI tests use bUnit `2.7.2`.
- Browser/E2E tests use Playwright `1.52.0`.
- Target SDK/package set is centralized in `Directory.Packages.props`; do not add ad hoc package versions to project files.

## Files Likely Touched

- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminDeadLetterApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminApiAccessTokenProvider.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/TokenAuthenticationStateProvider.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminUserContext.cs`
- `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/AuthorizedView.razor`
- `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` if the role selector needs local styling.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminUserContextTests.cs`
- Admin UI service tests for token-provider/role-state behavior.
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md` or the current manual Admin UI guide artifact.
- `_bmad-output/implementation-artifacts/deferred-work.md` only if the operator/admin dialog audit discovers broader deferred work.

## Out of Scope

- Issues #6, #7, and #8; they belong to `admin-ui-health-dapr-truthfulness-fix`.
- Issue #10 actor diagnostics honesty.
- Issues #11, #12, #14, and #17 admin operational index population and consistency retest.
- Issue #15 snapshot, compaction, and backup upstream endpoint implementation.
- Issues #16 and #18 consistency subtitle and tenant delete clarity polish.
- Production user impersonation, Keycloak user switching, role administration, or bypassing Admin.Server authorization policies.
- Changing tenant/domain/permission semantics beyond the dev token role claim required for testability.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
- `_bmad-output/implementation-artifacts/admin-ui-health-dapr-truthfulness-fix.md`
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminDeadLetterApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminApiAccessTokenProvider.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/TokenAuthenticationStateProvider.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminUserContext.cs`
- `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/AuthorizedView.razor`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminRole.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs`

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

TBD by dev agent.

### Completion Notes List

TBD by dev agent.

### File List

TBD by dev agent.

## Change Log

- 2026-05-07 - Story created and marked ready-for-dev. Context engine analysis completed from sprint-change proposal, manual-test issue evidence, current Admin UI auth/dead-letter code, sibling health story, and local package/test context.
