---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-04c-aggregate
  - step-05-validate-and-complete
lastStep: step-05-validate-and-complete
lastSaved: '2026-05-05'
storyId: post-epic-deferred-dw6-deferred-work-governance
storyKey: post-epic-deferred-dw6-deferred-work-governance
storyFile: _bmad-output/implementation-artifacts/post-epic-deferred-dw6-deferred-work-governance.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw6-deferred-work-governance.md
generatedTestFiles:
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Hexalith.EventStore.DeferredWorkGovernance.Tests.csproj
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6RuleVocabulary.cs
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6GovernanceDiagnostic.cs
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6TestPaths.cs
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/IDw6GovernanceCheckerInvoker.cs
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6GovernanceCheckerInvokerFactory.cs
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6GovernanceVocabularyAtddTests.cs
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6CheckerReportAtddTests.cs
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6LedgerSweepAtddTests.cs
  - tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6BookkeepingAtddTests.cs
  - _bmad-output/test-artifacts/deferred-work-governance/README.md
  - Hexalith.EventStore.slnx
detectedStack: backend
testFrameworks:
  - xUnit v3
  - Shouldly
generationMode: ai-generation
---

# DW6 ATDD Checklist

## Step 1: Preflight & Context

### Story

- Key: `post-epic-deferred-dw6-deferred-work-governance`
- Status: `ready-for-dev`
- Acceptance criteria: 12 governance, checker, legacy compatibility, evidence, scope-boundary, and bookkeeping criteria.

### Stack Detection

- Auto-detected: `backend`.
- The repo contains Blazor/UI projects, but DW6 is a Markdown ledger and local tooling governance story. No browser recording or runtime API coverage is needed.
- Test framework selected: xUnit v3 + Shouldly, matching existing ATDD projects and central package versions.

### Aspire Baseline

- Ran `aspire doctor`: Docker and .NET SDK checks passed; HTTPS dev-cert warnings were present but non-blocking.
- Started the root AppHost detached with `EnableKeycloak=false`.
- Aspire MCP resource inspection showed core resources running and healthy, including `eventstore` on `http://localhost:8080`, Admin host/UI, sample, tenants, Dapr sidecars, `statestore`, and `pubsub`.

### Inputs Loaded

- Story: `_bmad-output/implementation-artifacts/post-epic-deferred-dw6-deferred-work-governance.md`
- Ledger: `_bmad-output/implementation-artifacts/deferred-work.md`
- Sprint status: `_bmad-output/implementation-artifacts/sprint-status.yaml`
- TEA config: `_bmad/tea/config.yaml`
- Knowledge fragments: `data-factories`, `test-quality`, `test-healing-patterns`, `test-levels-framework`, `test-priorities-matrix`, `ci-burn-in`
- Prior DW patterns: DW4 validator ATDD project and DW5 checklist

## Step 2: Generation Mode

Mode: AI generation.

Rationale:

- DW6 has clear acceptance criteria and is a backend/docs-governance story.
- There is no UI journey to record.
- The implementation shape is intentionally open, so the tests use an entrypoint seam that can call PowerShell, Bash, or .NET.

## Step 3: Test Strategy

| AC | Concern | Priority | Level | Test file |
| --- | --- | ---: | --- | --- |
| #1 | Canonical governance vocabulary exists | P0 | Unit/filesystem | `Dw6GovernanceVocabularyAtddTests.cs` |
| #2 | OPEN/STORY metadata rules fail closed | P0 | Unit/tool contract | `Dw6GovernanceVocabularyAtddTests.cs`, `Dw6CheckerReportAtddTests.cs` |
| #3 | New deferrals include grouping guidance | P1 | Unit/filesystem | `Dw6GovernanceVocabularyAtddTests.cs` |
| #4 | Counts are deterministic and complete | P0 | Unit/tool contract | `Dw6CheckerReportAtddTests.cs` |
| #5 | Legacy forms recognized or advisory | P0 | Unit/filesystem + tool contract | `Dw6GovernanceVocabularyAtddTests.cs`, `Dw6LedgerSweepAtddTests.cs` |
| #6 | Curated sweep preserves raw text | P1 | Unit/filesystem | `Dw6LedgerSweepAtddTests.cs` |
| #7 | Unclassified live work includes locator | P0 | Unit/tool contract | `Dw6CheckerReportAtddTests.cs` |
| #8 | Reviewer and retrospective handoff guidance | P1 | Unit/filesystem | `Dw6GovernanceVocabularyAtddTests.cs` |
| #9 | Local automation stays small and consistent | P1 | Unit/filesystem + tool contract | `Dw6CheckerReportAtddTests.cs` |
| #10 | Evidence records before/after counts | P1 | Unit/filesystem | `Dw6LedgerSweepAtddTests.cs`, `Dw6BookkeepingAtddTests.cs` |
| #11 | Product/runtime scope boundaries intact | P0 | Unit/filesystem | `Dw6LedgerSweepAtddTests.cs` |
| #12 | Story and sprint bookkeeping gates | P1 | Unit/filesystem | `Dw6BookkeepingAtddTests.cs` |

Red-phase strategy:

- Every test is emitted with `[Fact(Skip = "...")]`.
- Helpers compile today and do not require the eventual checker to exist.
- When implementation starts, the dev writes `_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt`, removes the skip for one AC, verifies red, implements, then watches green.

## Step 4: Generated Tests

Generated project:

- `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests`

Generated support files:

- `Dw6RuleVocabulary.cs` pins dispositions, count buckets, metadata names, legacy forms, and blocking rule ids.
- `Dw6GovernanceDiagnostic.cs` pins JSON-report diagnostic shape and deterministic sort order.
- `Dw6TestPaths.cs` centralizes repo-relative paths and lightweight Markdown helpers.
- `IDw6GovernanceCheckerInvoker.cs` and `Dw6GovernanceCheckerInvokerFactory.cs` provide script/.NET entrypoint seams.
- `_bmad-output/test-artifacts/deferred-work-governance/README.md` documents the entrypoint and snapshot handoff.

Generated test files:

- `Dw6GovernanceVocabularyAtddTests.cs`: AC #1, #2, #3, #5, #8.
- `Dw6CheckerReportAtddTests.cs`: AC #4, #7, #9.
- `Dw6LedgerSweepAtddTests.cs`: AC #5, #6, #10, #11.
- `Dw6BookkeepingAtddTests.cs`: AC #10, #12 and ATDD handoff linkage.

Total scaffolds: 18 skipped facts.

## Step 4C: Aggregation

- Execution mode: sequential.
- API/tool-contract scaffolds and filesystem-governance scaffolds were generated in the same pass because DW6 has no UI/E2E split.
- TDD red phase compliance: all scaffolded tests use `[Fact(Skip = ...)]`.
- Placeholder assertion check: no no-op `true == true` assertions were generated.
- Solution entry added to `Hexalith.EventStore.slnx`.
- Story Dev Notes linked this checklist and the red-phase test project.

## Step 5: Validate & Complete

### Validation Results

- Checklist frontmatter includes story metadata, generated paths, stack, and generation mode.
- Red-phase project builds cleanly in Release.
- Focused test run reports 18 skipped tests, 0 passed, 0 failed.
- Temp artifacts are stored under `_bmad-output/test-artifacts/deferred-work-governance/`.
- No browser/Playwright sessions were opened for this ATDD run.

### Commands Run

```powershell
dotnet build tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Hexalith.EventStore.DeferredWorkGovernance.Tests.csproj --configuration Release -p:NuGetAudit=false
```

Result: 0 warnings, 0 errors.

```powershell
dotnet test tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Hexalith.EventStore.DeferredWorkGovernance.Tests.csproj --configuration Release --no-build -p:NuGetAudit=false
```

Result: 0 failed, 0 passed, 18 skipped, 18 total.

### Dev Handoff

1. Take the ledger snapshot before editing:
   `Copy-Item _bmad-output/implementation-artifacts/deferred-work.md _bmad-output/test-artifacts/deferred-work-governance/deferred-work-snapshot.md`
2. Pick the checker shape and write `_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt`.
3. Remove skips AC by AC, confirm red, implement, then confirm green.
4. Record before/after counts, checker output, files touched, and any intentionally unclassified sections in the Dev Agent Record.
5. Move the story and sprint-status row to `review` only after the governance convention, checker/checklist, curated ledger sweep, and validation evidence are present.
