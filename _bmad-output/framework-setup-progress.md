---
stepsCompleted: ['step-01-preflight', 'step-02-select-framework', 'step-03-scaffold-framework', 'step-04-docs-and-scripts', 'step-05-validate-and-summary']
lastStep: 'step-05-validate-and-summary'
lastSaved: '2026-03-29'
status: complete
---

# Test Framework Setup — Complete

## Summary

### Production Code Change
- **`src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs`** (new) — Extracted all Admin.UI service and middleware configuration from Program.cs into `AddAdminUI()` / `UseAdminUI()` / `StartSignalRAsync()` extension methods for testability.
- **`src/Hexalith.EventStore.Admin.UI/Program.cs`** (simplified) — Now calls the extension methods. Zero behavior change.

### Test Infrastructure Created
- **`PlaywrightFixture.cs`** — Builds Admin.UI with Kestrel on a random TCP port using `AddAdminUI()`, skipping Aspire service defaults. Launches headless Chromium via Playwright.
- **`PlaywrightCollection.cs`** — xUnit `[CollectionDefinition("Playwright")]` for shared fixture.
- **`BrowserSmokeTests.cs`** — 5 browser E2E tests: shell render, accessible navigation, page navigation, performance, stat cards.
- **`GlobalUsings.cs`** (updated) — Added `global using Microsoft.Playwright;`

### Test Results
- **5/5 new Playwright browser tests pass** (13 seconds)
- **464 bUnit component tests pass** (no regression)
- **Full solution builds Release with 0 warnings, 0 errors**

### Approach: Direct Kestrel Host (not WebApplicationFactory)
.NET 10's `WebApplicationFactory` forces TestServer for minimal hosting — Playwright can't connect via TCP. Solution: build the Admin.UI directly with `WebApplication.CreateBuilder()` + `AddAdminUI()` + Kestrel, setting `ContentRootPath` and `ApplicationName` to the Admin.UI output directory so `MapStaticAssets()` resolves correctly.
