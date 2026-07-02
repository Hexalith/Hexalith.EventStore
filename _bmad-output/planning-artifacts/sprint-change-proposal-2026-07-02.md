# Sprint Change Proposal — CI/CD Best-Practice Hardening and Shared Build Boundary

- **Date:** 2026-07-02
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Batch (direct implementation requested)
- **Change classification:** Minor (CI/CD hardening and documentation correction)
- **Status:** Implemented locally; pending review/commit
- **Builds on:** `sprint-change-proposal-2026-06-22-ci-release-retier.md`

---

## 1. Issue Summary

### Problem statement

The three root CI/CD workflows are functional but still carried avoidable
operational risk:

- Release repeated the CI test gate on every push to `main`.
- Several fast test projects existed in the solution but were not in the CI test
  manifest.
- Workflow actions and shared Hexalith.Builds actions used mutable references
  such as `@main` and `@master`.
- Integration test artifacts were not uploaded consistently.
- `docs/ci.md` described obsolete workflows and policies.
- Shared CI/CD policy was documented in EventStore instead of Hexalith.Builds.

### Discovery context

The issue was identified during a CI/CD best-practice review of
`.github/workflows/ci.yml`, `.github/workflows/integration.yml`, and
`.github/workflows/release.yml`.

### Evidence

- Root workflows used remote `Hexalith/Hexalith.Builds/...@main` and
  `actions/setup-node@main`.
- Hexalith.Builds reusable workflows and composite actions used
  `actions/*@main`, `dapr/setup-dapr@main`, `nick-fields/retry@master`, and
  recursive submodule checkout.
- `Hexalith.EventStore.slnx` included test projects absent from the CI fast-test
  list, including `RestApi.Generators.Tests`.
- Release had a `release-gate` job duplicating CI restore/build/test work.
- `docs/ci.md` referenced removed workflows such as deploy staging, docs
  validation, API docs, and perf lab.

---

## 2. Impact Analysis

### Epic impact

Epic D remains in progress and is not resequenced. This correction supports Epic
D by ensuring the newly added generator test project is part of the blocking
fast-test gate and by explicitly classifying ATDD validator scaffolds as
non-blocking until their required artifacts exist.

### Story impact

No story scope changes are required. The active D3/D4 review state is unchanged.

### Artifact impact

| Artifact | Impact |
|----------|--------|
| `references/Hexalith.Builds/Github/*` | Shared composite actions now pin upstream actions to full SHAs where applicable. |
| `references/Hexalith.Builds/.github/workflows/*` | Reusable workflows now avoid recursive submodule checkout and mutable third-party action references. |
| `references/Hexalith.Builds/.github/workflows/ci-cd-standards.md` | New shared CI/CD standards document. |
| `.github/workflows/ci.yml` | Uses local Hexalith.Builds submodule actions, pins third-party actions, improves cache key, adds missing fast tests and test-manifest drift guard. |
| `.github/workflows/integration.yml` | Uses local Hexalith.Builds submodule actions, pins third-party actions, uploads live-sidecar TRX/coverage artifacts. |
| `.github/workflows/release.yml` | Runs after successful CI workflow on push to `main`; removes duplicate release-gate restore/build/test work. |
| `docs/ci.md` | Rewritten as EventStore-specific CI documentation and linked to Hexalith.Builds shared standards. |

### PRD / epic document availability

Formal PRD and epic source documents were not present under
`_bmad-output/planning-artifacts`; the current planning baseline is the existing
sprint change proposals plus `_bmad-output/implementation-artifacts/sprint-status.yaml`.
No PRD/MVP scope change is needed.

---

## 3. Recommended Approach

**Selected path: Direct Adjustment.**

This is a CI/CD hygiene correction, not a product replan. Move shared standards
and reusable behavior into Hexalith.Builds, keep EventStore-specific test/package
manifests in EventStore, and reduce duplicated release work by making release
depend on successful CI.

- **Effort:** Low-Medium.
- **Risk:** Low.
- **Timeline impact:** Immediate improvement; no Epic D resequencing.

Rejected alternatives:

- **Rollback:** Not useful; the June release/test retier remains valid.
- **MVP review:** Not applicable; no product scope changed.
- **Move all EventStore workflow contents to Hexalith.Builds:** Not appropriate;
  test project lists, package lists, and EventStore-specific exclusions are
  module-specific and should remain in this repository.

---

## 4. Detailed Change Proposals

### CP-1 — Shared CI/CD standards belong in Hexalith.Builds

**Artifact:** `references/Hexalith.Builds/.github/workflows/ci-cd-standards.md`

**OLD:** Shared CI/CD policy was embedded in EventStore documentation.

**NEW:** Hexalith.Builds now owns common standards for:

- action pinning,
- root-only submodule initialization,
- .NET restore/build/test conventions,
- NuGet caching,
- release gates,
- artifact upload.

**Rationale:** CI/CD rules shared across Hexalith modules should not be
maintained independently in each module repository.

### CP-2 — Harden Hexalith.Builds reusable workflows/actions

**Artifacts:** `references/Hexalith.Builds/Github/*`,
`references/Hexalith.Builds/.github/workflows/*`

**OLD:**

```yaml
uses: actions/checkout@main
uses: actions/setup-dotnet@main
uses: nick-fields/retry@master
submodules: true
```

**NEW:**

```yaml
uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0
uses: actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1 # v5.4.0
uses: nick-fields/retry@ad984534de44a9489a53aefd81eb77f87c70dc60 # v4.0.0
submodules: false
run: git -c submodule.recurse=false submodule update --init
```

**Rationale:** Shared workflows should be reproducible and should not initialize
nested submodules.

### CP-3 — EventStore consumes local Hexalith.Builds shared actions

**Artifacts:** `.github/workflows/ci.yml`, `.github/workflows/integration.yml`,
`.github/workflows/release.yml`

**OLD:**

```yaml
uses: Hexalith/Hexalith.Builds/Github/initialize-dotnet@main
```

**NEW:**

```yaml
run: git -c submodule.recurse=false submodule update --init references/Hexalith.Builds
uses: ./references/Hexalith.Builds/Github/initialize-dotnet
```

**Rationale:** EventStore now uses the Hexalith.Builds version pinned by its
submodule reference instead of a floating remote branch.

### CP-4 — Expand and guard the test manifest

**Artifact:** `.github/workflows/ci.yml`

**OLD:** Fast-test and non-blocking lists omitted known test projects from the
solution.

**NEW:** The blocking fast-test lane includes:

- `tests/Hexalith.EventStore.RestApi.Generators.Tests`

The manifest also classifies red-phase ATDD validator scaffolds as known
non-blocking:

- `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests`
- `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests`

Every discovered test project must be classified as blocking or known
non-blocking.

**Rationale:** New tests should not silently fall out of CI.

### CP-5 — Release depends on CI success instead of duplicating CI

**Artifact:** `.github/workflows/release.yml`

**OLD:**

```yaml
on:
  push:
    branches: [main]
jobs:
  release-gate:
    ...
  release:
    needs: release-gate
```

**NEW:**

```yaml
on:
  workflow_run:
    workflows: [CI]
    types: [completed]
    branches: [main]
jobs:
  release:
    if: ${{ github.event.workflow_run.conclusion == 'success' && github.event.workflow_run.event == 'push' }}
```

**Rationale:** CI is the gate. Release should publish after the gate passes, not
repeat it. The release job keeps GitHub token credentials because
`@semantic-release/git` must push the generated changelog commit and release tag.

### CP-6 — Integration evidence upload

**Artifact:** `.github/workflows/integration.yml`

**OLD:** Live-sidecar tests wrote a TRX logger but did not set a results
directory or upload artifacts.

**NEW:** The integration workflow writes TRX/coverage under
`TestResults/live-sidecar` and uploads those files with `if: always()`.

**Rationale:** Failing integration runs need durable evidence.

### CP-7 — EventStore CI docs corrected

**Artifact:** `docs/ci.md`

**OLD:** Documented obsolete workflows and stale branch/status expectations.

**NEW:** Documents only the three current EventStore workflows and links to
Hexalith.Builds for common CI/CD standards.

**Rationale:** Module docs should describe module-specific wiring accurately.

---

## 5. Implementation Handoff

- **Scope:** Minor.
- **Route:** Developer direct implementation.
- **Implementation status:** Applied locally.
- **Handoff recipients:** Developer agent for validation, then normal code
  review.

### Success criteria

1. `actionlint .github/workflows/*.yml` passes.
2. `actionlint references/Hexalith.Builds/.github/workflows/*.yml` passes.
3. Mutable action reference scan finds no `@main`, `@master`, or floating major
   action references in EventStore workflows or Hexalith.Builds shared workflow
   assets.
4. CI manifest check accounts for every test project under `tests/`.
5. Newly added blocking fast test projects pass locally.
6. Release runs only after a successful CI push workflow on `main`.

### Follow-ups

- Create/push a corresponding Hexalith.Builds submodule commit before relying on
  these shared changes from other modules.
- Decide whether `Admin.UI.E2E` should get its own Playwright lane.
- Build a reliable Aspire-in-CI lane before making
  `tests/Hexalith.EventStore.IntegrationTests` blocking.

## 6. Checklist Summary

| Item | Status | Notes |
|------|--------|-------|
| 1.1 Triggering story | N/A | No single story; CI/CD best-practice review. |
| 1.2 Core problem | Done | CI/CD efficiency, reproducibility, evidence, and docs drift. |
| 1.3 Evidence | Done | Workflow and solution/test manifest review. |
| 2.1-2.5 Epic impact | Done | Epic D unchanged; CI support improved. |
| 3.1 PRD conflicts | N/A | No MVP/product scope change. |
| 3.2 Architecture conflicts | Done | CI/CD architecture boundary clarified. |
| 3.3 UI/UX conflicts | N/A | No UX change. |
| 3.4 Other artifacts | Done | Workflows, shared actions, docs. |
| 4.1 Direct adjustment | Viable | Selected. |
| 4.2 Rollback | Not viable | Would not improve CI/CD. |
| 4.3 MVP review | Not viable | No product scope impact. |
| 4.4 Recommended path | Done | Direct adjustment. |
| 5.1-5.5 Proposal components | Done | Included above. |
| 6.1-6.2 Review | Done | Validation planned/executed. |
| 6.3 Approval | Done | User requested direct fix. |
| 6.4 Sprint status | N/A | No epic/story status changes. |
| 6.5 Handoff | Done | Developer validation and review. |
