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

## Issue #17 Disposition

Prerequisite story `admin-operational-index-populators` retested Issue #17 after projection indexes existed. Check `01KR8N6MTQJGVF7BNN9ZZEAWV5` reduced the missing-index false-positive cluster to one residual granularity warning, already recorded in `deferred-work.md`. This story made no consistency algorithm changes.

## Automated Validation

- `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release --filter "FullyQualifiedName~Consistency_RefreshUpdatesStatCardSecondaryText_FromCurrentSummary|FullyQualifiedName~TenantsPage_ExplainsLifecycle"`
- `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release --filter "FullyQualifiedName~ConsistencyPageTests|FullyQualifiedName~TenantsPageTests"`

