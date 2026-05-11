# admin-ui-operator-action-and-dev-role-testability-fix Evidence

Date/time: 2026-05-09T18:54:57+02:00 through 2026-05-09T19:18:50+02:00
Environment: local development workspace, Aspire AppHost baseline already running; Admin UI resource stopped during bUnit build to release Windows file locks. Positive role-switcher smoke captured after restarting Aspire with `EnableKeycloak=false`.
Keycloak-disabled state for dev-role contract: covered by configuration-gated automated tests with `Development` plus empty `EventStore:Authentication:Authority`; negative tests cover `Production` and Development with Keycloak authority configured. Live no-Keycloak smoke captured at `https://localhost:8093/`.

## Automated Evidence

| Evidence | Artifact / command | Result |
|---|---|---|
| Dead-letter Retry/Skip/Archive full failure recovery | `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --no-restore --filter "DeadLetters_BulkActionFullFailure|DeadLetters_DoubleSubmit|AdminApiAccessTokenProviderRoleTests|MainLayout_RendersDevelopmentRoleSelector|MainLayout_HidesDevelopmentRoleSelector"` | 14/14 pass |
| Problem-detail/status parsing and raw-body redaction | `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --no-restore --filter "AdminDeadLetterApiClientErrorTests"` | Expanded during review patching to include non-object JSON and problem-detail secret redaction; covered by the 29/29 review regression run. |
| Same-session authorization refresh | `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --no-restore --filter "AuthorizedViewRoleChangeTests"` | 1/1 pass |
| Dev JWT role claim evidence | `AdminApiAccessTokenProviderRoleTests` decodes generated development JWT payloads without recording token material. | Redacted decoded payload evidence: `eventstore:admin-role=ReadOnly`, `eventstore:admin-role=Operator`, and `eventstore:admin-role=Admin`; issuer `hexalith-dev`, audience `hexalith-eventstore`, subject, tenants, domains, and permissions are preserved; `global_admin=true` is present only for `Admin`. |
| Review patch regression suite | `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj --no-restore --filter "AdminDeadLetterApiClientTests|AdminDeadLetterApiClientErrorTests|DeadLetters_HandlesPartialFailure_OnRetry|DeadLetters_BulkActionFullFailure|DeadLetters_DoubleSubmit|DeadLetters_RetryAfterFullFailure|DeadLetters_DisposeDuringPendingAction|AdminApiAccessTokenProviderRoleTests"` | 29/29 pass on 2026-05-10. |
| Full Admin UI regression suite | `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj --no-restore` | 780/780 pass on 2026-05-10 after review patches. |
| Live Aspire header smoke with Keycloak authority configured | Playwright snapshot `_bmad-output/test-artifacts/admin-ui-role-switch-keycloak-gated-snapshot.md` and screenshot `_bmad-output/test-artifacts/admin-ui-role-switch-keycloak-gated-2026-05-09.png` | Admin UI loaded at `https://localhost:8093/`; header shows status/theme controls and no `Development role` selector, matching the Keycloak/authority negative gate. Console only reported pre-existing `favicon.ico` 404. |
| Live Aspire header smoke with Keycloak disabled | Playwright snapshot `_bmad-output/test-artifacts/admin-ui-role-switch-dev-admin-snapshot.md` and screenshot `_bmad-output/test-artifacts/admin-ui-role-switch-dev-admin-2026-05-09.png` | Admin UI loaded at `https://localhost:8093/`; header shows the `Development role` selector, visible active role text, and polite status copy. |
| ReadOnly role guard smoke | Playwright snapshot `_bmad-output/test-artifacts/admin-ui-role-switch-dev-readonly-deadletters-snapshot.md` and screenshot `_bmad-output/test-artifacts/admin-ui-role-switch-dev-readonly-deadletters-2026-05-09.png` | Role changed in-session to `ReadOnly`; `/health/dead-letters` retained read/refresh affordances and no Retry/Skip/Archive destructive action controls were rendered. |
| Operator role guard smoke | Playwright snapshots `_bmad-output/test-artifacts/admin-ui-role-switch-dev-operator-deadletters-snapshot.md`, `_bmad-output/test-artifacts/admin-ui-role-switch-dev-operator-consistency-snapshot.md` and screenshots `_bmad-output/test-artifacts/admin-ui-role-switch-dev-operator-deadletters-2026-05-09.png`, `_bmad-output/test-artifacts/admin-ui-role-switch-dev-operator-consistency-2026-05-09.png` | Role changed in-session to `Operator`; `/consistency` rendered Run Check. Dead Letters had no current selected rows in the seeded workspace, so bulk action visibility remains primarily covered by bUnit role/failure tests. |
| Admin role guard smoke | Playwright snapshot `_bmad-output/test-artifacts/admin-ui-role-switch-dev-admin-settings-snapshot.md` and screenshot `_bmad-output/test-artifacts/admin-ui-role-switch-dev-admin-settings-2026-05-09.png` | Role changed in-session back to `Admin`; Admin-only Settings page rendered. |

## Operator/Admin Dialog Audit Matrix

| Surface | Current behavior inspected | Disposition |
|---|---|---|
| `DeadLetters.razor` Retry/Skip/Archive | Full failure previously toast-only; now has attempt guard, single finally path, preserved selection, inline sanitized failure panel, and duplicate-submit prevention. | fixed |
| `Snapshots.razor` create/edit/delete policy and create snapshot dialogs | Each confirm path disables on `_isOperating`, catches auth/service/request failures, and clears `_isOperating` in `finally`; no selected-row bulk state to preserve. Failures remain toast-only but no stuck busy-state parity defect found. | already-safe |
| `Compaction.razor` trigger dialog | Confirm path uses `_isOperating`, cancellation token, explicit `OperationCanceledException` handling, and clears `_isOperating` in `finally`; no selection-loss pattern found. | already-safe |
| `Backups.razor` create/validate/restore/export/import dialogs | Operation paths clear `_isOperating` in `finally` or explicit completion branches; failures use the page's shared toast/error helper. Broader backup business behavior is out of scope. | already-safe |
| `Consistency.razor` trigger/cancel dialogs | Trigger and cancel have separate busy flags (`_isTriggering`, `_isCancelling`) cleared in `finally`; Run Check visibility is role-guarded and covered by the dev-role smoke guide. | already-safe |
| `Tenants.razor` create/lifecycle/user/role dialogs | Operation paths use `_isOperating`, cancellation token where applicable, and clear `_isOperating` in `finally`; tenant lifecycle/product copy changes are outside this story. | already-safe |
| `Projections.razor` / `ProjectionDetailPanel.razor` | Projections page has no action modal; projection reset/replay panel dialogs clear `_isOperating` in `finally` and close only after attempted operation. | already-safe |
| Shared modal helpers | `StateInspectorModal` lifecycle was handled by prior DW7 story; no shared bulk action helper with the dead-letter stuck-state pattern exists. | not-applicable |

Deferred items: none from this audit. Broader upstream snapshot/compaction/backup operation gaps remain owned by `admin-storage-snapshot-compaction-backup-operations`.

## Manual Guide Update

Updated `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md` with:

- exact dev-role selector location and gate;
- expected token claim behavior per `ReadOnly`, `Operator`, and `Admin`;
- dead-letter failure dialog recovery checks for Retry/Skip/Archive;
- retry-after-failure and duplicate-submit expectations.
