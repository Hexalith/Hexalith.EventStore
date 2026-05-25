---
stepsCompleted: ['step-01-preflight', 'step-02-select-framework', 'step-03-scaffold-framework', 'step-04-docs-and-scripts', 'step-05-validate-and-summary']
lastStep: 'step-05-validate-and-summary'
lastSaved: '2026-05-25'
---

# Framework Setup Progress — Hexalith.EventStore

## Step 1 — Preflight

- **Detected stack:** `backend` (.NET 10, `.slnx`, Microsoft.Playwright .NET wrapper + xUnit v3).
- **Prerequisite check:** FAILED — a mature E2E framework already exists
  (`tests/Hexalith.EventStore.Admin.UI.E2E`, audited & green 2026-05-25: 16 passed / 2 skipped).
- **User override:** Jerome chose **Force new scaffold** after being shown the HALT and the
  "NOT recommended" warning. Proceeded under explicit instruction.

## Step 2 — Framework Selection

- **Selected:** xUnit v3 (+ Shouldly, NSubstitute, coverlet). Deterministic backend/.NET default.
  `test_framework: playwright` config applies to frontend/fullstack only.

## Step 3 — Scaffold

- Created isolated, self-contained project `tests/Hexalith.EventStore.Scaffold.Tests/`:
  - `Hexalith.EventStore.Scaffold.Tests.csproj` (central package versions; `IsPackable=false`).
  - `Unit/CustomerGreeterTests.cs` — Fact/Theory, Shouldly, NSubstitute, factory overrides.
  - `Integration/ExampleIntegrationTests.cs` — skipped placeholder (Tier 2: DAPR+Docker, state-store assert).
  - `Api/ExampleApiTests.cs` — skipped placeholder (WebApplicationFactory / Aspire).
  - `Support/` — sample SUT, `TestDataFactory` (override pattern), `SampleCollectionFixture`.
  - `.env.example` — workflow-standard template.
- No production references (deletable in one step). `.slnx` and CI deliberately NOT modified.
- **Verified:** build 0 warn / 0 err (warnings-as-errors); `dotnet test` → 6 passed, 2 skipped.

## Step 4 — Docs & Scripts

- `tests/Hexalith.EventStore.Scaffold.Tests/README.md` — stack, layout, run commands
  (`dotnet test`, `--collect "XPlat Code Coverage"`, `--filter`), architecture notes, removal command,
  repo conventions (R2-A6 state-store asserts, R2-A7 ULID validation).

## Step 5 — Validation & Summary

Checklist is Node/Playwright-frontend oriented; scored against backend-.NET-applicable items.

- ✅ Stack detected; manifest read; framework justified (xUnit) and user-notified.
- ✅ `tests/` root + flexible layout (Unit/Integration/Api) + `Support/` (the key support pattern).
- ✅ Config (csproj) syntactically valid — builds 0 warn / 0 err.
- ✅ `.env.example` with TEST_ENV/BASE_URL/API_URL; `.nvmrc` N/A (.NET, global.json present).
- ✅ Shared fixture with auto-cleanup; data factory with override pattern.
- ✅ Sample tests use AAA (Given/When/Then), factory, NSubstitute, Shouldly assertions.
- ✅ README with setup/run/architecture/CI/conventions; `dotnet test` documented.
- ✅ Sample test executes: 6 passed, 2 skipped, 0 failed. No secrets; `.env.example` placeholders only.
- N/A: playwright.config/cypress.config, .nvmrc, mergeTests, data-testid, faker (deterministic by
  choice), Pact.js CDC (Node-runtime-gated), Node knowledge-base fragments.
- ⚠️ Intentional deviations: not added to `.slnx`/CI; no API/auth helper files; `Guid`-free placeholders.

**Status:** complete (forced scaffold). Redundant with the existing audited suite; deletable via
`git clean -fdx tests/Hexalith.EventStore.Scaffold.Tests`.

## Outcome — REVERTED 2026-05-25

Per Jerome's instruction, `tests/Hexalith.EventStore.Scaffold.Tests/` was **deleted** in full.
It was never added to `.slnx` or CI, so removal leaves the repo exactly as it was before the
scaffold — the audited `Admin.UI.E2E` suite is untouched. Net change from this run: none (this
progress record only).
