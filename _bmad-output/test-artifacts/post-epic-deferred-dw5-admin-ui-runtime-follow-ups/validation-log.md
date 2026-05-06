# DW5 Validation Log

Date: 2026-05-05

## Commands

| Command | Result | Notes |
| --- | --- | --- |
| `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj --filter "FullyQualifiedName~Dw5SidebarShortcutAtddTests" -p:BaseOutputPath=D:\Hexalith.EventStore\.tmp\test-bin\admin-ui-tests\` | Passed: 11/11 | First run after unskipping bUnit shortcut ATDD gates; validates deterministic Ctrl+B state/storage contracts. |
| `dotnet test tests\Hexalith.EventStore.Admin.UI.E2E\Hexalith.EventStore.Admin.UI.E2E.csproj --filter "FullyQualifiedName~Dw5SidebarShortcutBrowserAtddTests|FullyQualifiedName~Dw5TypeCatalogNavigationBrowserAtddTests" -p:BaseOutputPath=D:\Hexalith.EventStore\.tmp\test-bin\admin-ui-e2e\` | Passed: 7/7 | Browser-level TypeCatalog route proof and shortcut proof. Uses isolated build output because the live Aspire Admin UI process locks `bin\Debug`. |
| `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj --filter "FullyQualifiedName~Dw5SidebarShortcutAtddTests|FullyQualifiedName~Dw5DialogAccessibilityAtddTests|FullyQualifiedName~Dw5FluentV5InvariantsAtddTests|FullyQualifiedName~CommandPaletteTests" -p:BaseOutputPath=D:\Hexalith.EventStore\.tmp\test-bin\admin-ui-tests\` | Passed: 25/25 | Static dialog/Fluent v5 invariants, command palette regression, and shortcut bUnit coverage. |
| `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj --filter "FullyQualifiedName~Dw5TypeCatalogUrlIdempotencyAtddTests|FullyQualifiedName~Dw5TypeCatalogRenderLoopAtddTests" -p:BaseOutputPath=D:\Hexalith.EventStore\.tmp\test-bin\admin-ui-tests\` | Passed: 7/7 | TypeCatalog deep-link, URL idempotency, and render-loop guard scaffolds unskipped and green. |
| `dotnet build src\Hexalith.EventStore.Admin.UI\Hexalith.EventStore.Admin.UI.csproj -p:BaseOutputPath=D:\Hexalith.EventStore\.tmp\test-bin\admin-ui-build\` | Passed: 0 warnings, 0 errors | Admin UI build validation without writing to locked live Aspire output. |
| `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj --filter "FullyQualifiedName~Dw5GovernanceAtddTests" -p:BaseOutputPath=D:\Hexalith.EventStore\.tmp\test-bin\admin-ui-tests\` | Passed: 6/6 | Final file-system governance gates after story/evidence/deferred-work bookkeeping. |
| `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj --filter "FullyQualifiedName~TypeCatalogPageTests|FullyQualifiedName~MainLayoutTests|FullyQualifiedName~CommandPaletteTests|FullyQualifiedName~CommandSandboxTests|FullyQualifiedName~EventDebuggerTests|FullyQualifiedName~Dw5" -p:BaseOutputPath=D:\Hexalith.EventStore\.tmp\test-bin\admin-ui-tests\` | Passed: 87/87 | Final affected Admin UI bUnit/static/governance validation, no skipped tests. |

## Notable Red/Blocked Observations

| Observation | Disposition |
| --- | --- |
| Normal-output `dotnet test` failed with MSB3027/MSB3021 because the running Aspire Admin UI process locked Debug DLLs. | Switched validation to isolated `BaseOutputPath` so the live apphost stayed running. |
| E2E project initially could not restore with central package management because it referenced `xunit` while the repo defines `xunit.v3`. | Updated E2E project to `xunit.v3` and adjusted fixtures to xUnit v3 `ValueTask` async lifetime signatures. |
| DW5 browser tests initially failed on a generic `nav[aria-label='Main navigation']` selector because Fluent rendered duplicate nav DOM and the first instance was hidden. | Added `aria-label="Main navigation"` to `NavMenu` and scoped E2E selectors to `.admin-sidebar nav[...]`. |
| Ctrl+B browser tests initially failed because shortcut key matching was lowercase-only and did not reliably match browser-generated key values. | Made JS shortcut matching case-insensitive. |
| Ctrl+K browser reopen initially failed after Escape due to stale command-palette open state. | Kept normal `OpenAsync()` no-op behavior, but added a shortcut-only `force` path for re-open recovery. |
