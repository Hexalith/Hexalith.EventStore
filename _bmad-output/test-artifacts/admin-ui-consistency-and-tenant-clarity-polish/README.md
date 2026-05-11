# Admin UI Consistency and Tenant Clarity Polish Evidence

Date: 2026-05-10
Story: `admin-ui-consistency-and-tenant-clarity-polish`

## Runtime Baseline

- AppHost: `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json`
- Dev-mode restart: `$env:EnableKeycloak='false'; aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json`
- Dashboard URL: `https://localhost:17017/login?t=4375416a68a27020733b731fb8514fb5`
- Admin UI endpoint used for browser evidence: `http://localhost:63635`

## Evidence Files

- `consistency-empty-summary-subtitles.png` - `/consistency` shows current visible secondary text for all summary cards in the empty-state runtime.
- `consistency-empty-summary-subtitles-snapshot.md` - Playwright accessibility snapshot for `/consistency`.
- `tenants-lifecycle-copy-empty.png` - `/tenants` shows visible lifecycle copy explaining disable-over-delete behavior in the empty-state runtime.
- `tenants-lifecycle-copy-empty-snapshot.md` - Playwright accessibility snapshot for `/tenants`.
- `tenants-lifecycle-copy-active-disabled-blocker.md` - Review follow-up note: active/disabled browser evidence could not be recaptured because the AppHost runtime prerequisite check now fails before resources start; focused bUnit coverage pins the loaded tenant lifecycle path.

## Review Follow-up Runtime Blocker

Review patch attempt on 2026-05-11 tried to recapture the requested active/disabled tenant browser evidence with:

`$env:EnableKeycloak='false'; aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json`

The AppHost build succeeded, but runtime startup exited with code 2 because the DAPR CLI is not installed or not on PATH in this environment. Direct verification with `dapr --version` also failed with `The term 'dapr' is not recognized`.

Fallback evidence per AC6 is the focused bUnit coverage:

- `TenantsPage_ExplainsDisableInsteadOfDeleteLifecycle` renders a loaded tenant row and asserts the lifecycle copy plus absence of `Delete Tenant`.
- `TenantsPage_ExplainsLifecycle_WhenTenantListIsEmpty` keeps the lifecycle copy and no-delete assertion in the empty state.
- `TenantsPage_ExplainsLifecycle_WhenFilterHidesAllTenants` keeps the lifecycle copy and no-delete assertion when filters hide all rows.
- Review patch validation: `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release --filter "FullyQualifiedName~Consistency_RefreshUpdatesStatCardSecondaryText_FromCurrentSummary|FullyQualifiedName~Consistency_NormalizesWhitespaceFiltersFromUrl_OnInit|FullyQualifiedName~TenantsPage_ExplainsLifecycle|FullyQualifiedName~TenantsPage_ExplainsDisableInsteadOfDeleteLifecycle|FullyQualifiedName~StatCard_ClearsStaleLiveAnnouncement|FullyQualifiedName~StatCard_DoesNotDuplicateAccessibleLabel"` passed 7/7.

## Issue #17 Disposition

Prerequisite story `admin-operational-index-populators` retested Issue #17 after projection indexes existed. Check `01KR8N6MTQJGVF7BNN9ZZEAWV5` reduced the missing-index false-positive cluster to one residual granularity warning, already recorded in `deferred-work.md`. This story made no consistency algorithm changes.

## Automated Validation

- `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release --filter "FullyQualifiedName~Consistency_RefreshUpdatesStatCardSecondaryText_FromCurrentSummary|FullyQualifiedName~TenantsPage_ExplainsLifecycle"`
- `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release --filter "FullyQualifiedName~ConsistencyPageTests|FullyQualifiedName~TenantsPageTests"`
