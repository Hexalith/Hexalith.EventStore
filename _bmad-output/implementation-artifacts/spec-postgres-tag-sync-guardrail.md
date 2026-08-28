---
title: 'Guard PostgreSQL image tag synchronization'
type: 'bugfix'
created: '2026-08-28'
status: 'in-progress'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: 'adb3a999ba26e92ec0b2abbdb992b3e58035ba2f'
context:
  - '_bmad-output/project-context.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The integration workflow, OQ8 PostgreSQL fixture, and OQ8 evidence validator independently declare the PostgreSQL image tag. A one-file edit can silently make CI preparation, runtime execution, and evidence validation use different images.

**Approach:** Add a deterministic Contracts packaging test that structurally extracts the image from each authoritative declaration and requires all three values to agree. Keep the selected `postgres:18.4` image and every authority file unchanged.

## Boundaries & Constraints

**Always:** Identify the workflow value only inside the uniquely named `Pull PostgreSQL container image` step, identify the fixture and validator values only through their `PostgresImage` and `POSTGRES_IMAGE` declarations, and fail closed when an authority is missing, duplicated, malformed, or different.

**Block If:** A source no longer exposes one uniquely identifiable authoritative declaration and selecting a replacement authority would require a product or evidence-contract decision.

**Never:** Edit `_bmad-output/implementation-artifacts/deferred-work.md`; change the selected PostgreSQL image; modify `.github/workflows/integration.yml`, `Oq8PostgresqlFixture.cs`, `validate-oq8-platform-evidence.py`, retained OQ8 evidence, or existing hash-bound guardrail tests; introduce another hard-coded image value as a fourth authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Synchronized authorities | Each source exposes exactly one identical PostgreSQL image | Governance test passes | No error expected |
| Image drift | Any extracted image differs from either peer | Governance test fails and identifies the differing values | Deterministic assertion failure |
| Missing or ambiguous authority | A declaration is absent, malformed, or duplicated | Governance test fails before accepting an arbitrary match | Deterministic count assertion names the source |

</intent-contract>

## Code Map

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- new deterministic source-inspection guard; reuse the packaging tests' repository-root discovery and Shouldly conventions, with bounded culture-invariant regular expressions.
- `.github/workflows/integration.yml` -- read-only workflow authority; extract `docker pull` only from the unique `Pull PostgreSQL container image` step at lines 74-80.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` -- read-only runtime authority; extract the private `PostgresImage` constant near line 29.
- `tools/validate-oq8-platform-evidence.py` -- read-only evidence authority; extract the module-level `POSTGRES_IMAGE` assignment near line 49.
- `tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` -- existing xUnit v3, Shouldly, and implicit-source-inclusion configuration; no project-file edit is needed.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- orchestrator-owned and explicitly read-only.

## Tasks & Acceptance

**Execution:**
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- add one live-repository guard plus private extraction/root-discovery helpers that require exactly one named workflow step, pull command, fixture constant, and validator constant -- directly enforces DW-8 without changing any selected image or evidence authority.

**Acceptance Criteria:**
- Given the checked-in integration workflow, OQ8 fixture, and evidence validator, when `PostgreSqlImageGovernanceTests` runs, then it extracts one image from each authoritative surface and proves all three values are identical.
- Given a missing, malformed, or duplicated named step, pull command, fixture constant, or validator constant, when the structural guard evaluates that source, then it fails closed with a source-specific count diagnostic instead of selecting an arbitrary value.
- Given one authority changes while either peer retains its prior image, when the structural guard runs, then it fails and reports the compared image values.
- Given the implementation diff, when reviewed, then `postgres:18.4` remains selected in all three authority files and the deferred-work ledger is unchanged.

## Spec Change Log

## Review Triage Log

## Design Notes

The test compares extracted values rather than embedding `postgres:18.4` in test code. This keeps the guard focused on the invariant (CI preparation, runtime execution, and evidence validation agree) and avoids creating a fourth image literal that could itself drift. Exact-one extraction still freezes the current source structure and makes unreviewed ambiguity fail closed.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.PostgreSqlImageGovernanceTests -noColor` -- expected: the focused guard passes with all three authorities synchronized.
- `git diff --check -- tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs _bmad-output/implementation-artifacts/spec-postgres-tag-sync-guardrail.md` -- expected: no whitespace errors.
