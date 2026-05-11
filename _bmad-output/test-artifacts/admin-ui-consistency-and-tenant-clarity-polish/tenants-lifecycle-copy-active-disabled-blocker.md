# Active/Disabled Tenant Evidence Blocker

Date: 2026-05-11
Story: `admin-ui-consistency-and-tenant-clarity-polish`

## Requested Evidence

The code review requested `/tenants` browser evidence with at least one active tenant and one disabled tenant visible, because the existing runtime capture only showed the empty tenant state.

## Blocker

The review patch attempted to restart the Aspire AppHost in dev mode:

```powershell
$env:EnableKeycloak='false'; aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json
```

The AppHost build succeeded, but startup exited with code 2 before resources became available. The Aspire log reported:

```text
Prerequisites missing:
  - DAPR CLI is not installed.
Install DAPR: https://docs.dapr.io/getting-started/install-dapr-cli/
Then run 'dapr init' and retry.
```

Direct prerequisite verification also failed:

```powershell
dapr --version
```

PowerShell returned that `dapr` is not recognized as a cmdlet, function, script file, or executable program.

## Fallback Evidence

Per AC6 fallback behavior when Aspire is unavailable, the loaded tenant lifecycle path is pinned by bUnit:

- `TenantsPage_ExplainsDisableInsteadOfDeleteLifecycle` renders a loaded tenant row and asserts lifecycle copy plus absence of `Delete Tenant`.
- `TenantsPage_ExplainsLifecycle_WhenTenantListIsEmpty` asserts lifecycle copy and absence of `Delete Tenant` in the empty state.
- `TenantsPage_ExplainsLifecycle_WhenFilterHidesAllTenants` asserts lifecycle copy and absence of `Delete Tenant` when filters hide all rows.

Review patch validation passed:

```powershell
dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release --filter "FullyQualifiedName~Consistency_RefreshUpdatesStatCardSecondaryText_FromCurrentSummary|FullyQualifiedName~Consistency_NormalizesWhitespaceFiltersFromUrl_OnInit|FullyQualifiedName~TenantsPage_ExplainsLifecycle|FullyQualifiedName~TenantsPage_ExplainsDisableInsteadOfDeleteLifecycle|FullyQualifiedName~StatCard_ClearsStaleLiveAnnouncement|FullyQualifiedName~StatCard_DoesNotDuplicateAccessibleLabel"
```

Result: 7/7 passed.
