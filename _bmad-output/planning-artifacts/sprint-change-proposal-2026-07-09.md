---
project: eventstore
date: 2026-07-09
workflow: bmad-correct-course
mode: batch
status: approved
trigger: EventStore CI/CD should match the Tenants module CI/CD pattern.
scope_classification: moderate
artifacts_reviewed:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux.md
  - _bmad-output/project-context.md
  - docs/ci.md
  - .github/workflows/ci.yml
  - .github/workflows/release.yml
  - .github/workflows/integration.yml
  - .github/workflows/codeql.yml
  - .github/workflows/commitlint.yml
  - .github/workflows/dependency-review.yml
  - references/Hexalith.Tenants/.github/workflows/ci.yml
  - references/Hexalith.Tenants/.github/workflows/release.yml
  - references/Hexalith.Tenants/.github/workflows/codeql.yml
  - references/Hexalith.Tenants/.github/workflows/commitlint.yml
  - references/Hexalith.Tenants/.github/workflows/dependency-review.yml
  - references/Hexalith.Builds/.github/workflows/domain-ci.yml
  - references/Hexalith.Builds/.github/workflows/domain-release.yml
---

# Sprint Change Proposal - Align EventStore CI/CD With Tenants

## 1. Issue Summary

Administrator identified that the EventStore module CI/CD should be the same as the Tenants module.

The current EventStore workflows are only partially aligned:

- `codeql.yml`, `commitlint.yml`, and `dependency-review.yml` are already thin callers of shared `Hexalith.Builds` workflows through `@main`.
- `ci.yml` remains a 270-line custom workflow with local restore/build/test/cache/artifact logic.
- `release.yml` remains a custom semantic-release workflow instead of a thin caller of `Hexalith.Builds/.github/workflows/domain-release.yml@main`.
- `integration.yml` is a separate EventStore-specific live-sidecar workflow.
- Tenants uses thin `domain-ci.yml@main` and `domain-release.yml@main` callers with module-specific inputs.

Evidence from Tenants:

```yaml
jobs:
  ci:
    uses: Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main
```

```yaml
jobs:
  release:
    uses: Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main
```

The direct copy is not yet safe for EventStore because EventStore has an explicit PRD/NFR gate: live-sidecar tests must stay out of the deterministic release gate while still running in a dedicated lane (`NFR10`, `FR17`, Story 3.1). The current shared `domain-ci.yml` runs each listed unit/integration test project without a per-project trait filter. If EventStore lists `tests/Hexalith.EventStore.Server.Tests` as a shared integration project, `Category=LiveSidecar` tests become part of blocking CI and therefore block release, violating NFR10. If EventStore omits the project, the deterministic `Category!=LiveSidecar` server gate is lost.

## 2. Impact Analysis

### Epic Impact

| Epic | Impact |
| --- | --- |
| Epic 3 - Release And Repository Reliability | Directly affected. Story 3.7 should expand from shared security gates to Tenants-style reusable CI/release workflow migration. |
| Epic 7 - Operator Trust, Admin Honesty, And Future Capabilities | Indirectly affected. Story 7.4 still owns full Aspire/integration evidence recovery and must not be hidden inside the CI/CD parity migration. |
| Other epics | No product scope, architecture, or UX behavior changes. |

### Story Impact

No active implementation story file exists for Story 3.7, so no mandatory Correct-Course Story Rewrite Gate is triggered.

Affected planning story:

- `_bmad-output/planning-artifacts/epics.md` - Story 3.7 should be updated before implementation.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` - Story 3.7 remains `backlog`; after this proposal is approved, it should move through normal story creation with the updated scope.

Existing story boundaries to preserve:

- Story 3.1 remains the live-sidecar lane separation story. Do not regress its `Category!=LiveSidecar` deterministic gate.
- Story 3.8 remains the local DAPR/Aspire smoke-preflight story. Do not re-open it.
- Story 7.4 remains the full integration/Aspire CI recovery story.

### Artifact Conflicts

| Artifact | Conflict / gap | Required handling |
| --- | --- | --- |
| `.github/workflows/ci.yml` | Custom workflow, not Tenants-style reusable caller. | Replace with a thin `domain-ci.yml@main` caller only after filtered Server.Tests support is available. |
| `.github/workflows/release.yml` | Custom workflow, not Tenants-style reusable caller. | Replace with a thin `domain-release.yml@main` caller. |
| `.github/workflows/integration.yml` | Separate workflow not present in Tenants. | Absorb live-sidecar execution into shared CI as a non-blocking/advisory filtered lane, or keep temporarily until shared CI supports that lane. |
| `tools/pack-release-packages.py` and `tools/validate-release-packages.py` | EventStore package scripts are under `tools/`, while shared `domain-ci` expects `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, and `scripts/validate-consumer-package-references.py` when `run-consumer-validation: true`. | Add EventStore-compatible wrappers or move/rename scripts while keeping manifest-driven `tools/release-packages.json` as the package inventory. |
| `.releaserc.json` | Release prepare command already uses EventStore manifest scripts. Container publishing is not wired like Tenants. | Keep manifest-driven NuGet release and add container publish only for approved EventStore container project mappings. |
| `docs/ci.md` | Documents custom EventStore workflows. | Update after workflow migration. |

### PRD And Architecture Impact

No PRD scope reduction is recommended.

NFR10 remains valid and must be preserved:

```markdown
CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane.
```

Architecture AD-11 and AD-12 remain valid:

- Release remains manifest-governed.
- High-risk verification must use persisted evidence.

The CI/CD parity change should align workflow structure with Tenants without weakening EventStore release and evidence constraints.

### UX Impact

No UX artifact changes are required. This is a CI/CD and release reliability correction.

## 3. Recommended Approach

Recommended path: Direct Adjustment with shared-workflow adapter work.

Implement structural parity with Tenants:

1. Use `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main` for EventStore CI.
2. Use `Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main` for EventStore release.
3. Keep CodeQL, commitlint, and dependency-review as the existing shared callers, with commitlint also running on `push` to `main` like Tenants.
4. Add EventStore release-validation wrappers under `scripts/` so shared CI can run consumer/package validation.
5. Preserve EventStore NFR10 by adding one of these implementation prerequisites before replacing `ci.yml`:
   - preferred: extend the shared `domain-ci.yml` workflow to support filtered test lanes, so EventStore can run `Server.Tests` with `Category!=LiveSidecar` as blocking and `Category=LiveSidecar` as non-blocking/advisory; or
   - alternative: split live-sidecar tests into a separate test project so the existing shared workflow can express the tiers without filters.

Effort estimate: Medium.

Risk level: Medium until the shared workflow can express EventStore's test-tier split; Low after that support exists and the migration is verified.

## 4. Detailed Change Proposals

### Story 3.7 Scope Update

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: 3.7 Shared CI/CD Security Gates And Supply-Chain Backlog

OLD:

```markdown
As a repository maintainer,
I want EventStore CI/CD to reuse shared Hexalith.Builds security gates and document supply-chain hardening backlog,
So that workflow policy is consistent, reproducible, and reviewable across Hexalith modules.
```

NEW:

```markdown
As a repository maintainer,
I want EventStore CI/CD to follow the same reusable workflow pattern as Hexalith.Tenants,
So that CI, release, security gates, package validation, and container publishing are governed through Hexalith.Builds with only module-specific inputs in EventStore.
```

Rationale: The user's requested direction is broader than CodeQL/dependency/commitlint. It includes the main CI and release workflow shape used by Tenants.

### Story 3.7 Acceptance Criteria

Artifact: `_bmad-output/planning-artifacts/epics.md`

OLD:

```markdown
Given EventStore uses CodeQL, dependency-review, and commitlint workflows
When workflow files are inspected
Then they are thin callers of shared Hexalith.Builds reusable workflows using @main
And caller workflows retain only module-specific triggers, concurrency, and permissions.
```

NEW:

```markdown
Given EventStore uses GitHub Actions for CI, release, CodeQL, dependency review, and commitlint
When workflow files are inspected
Then `.github/workflows/ci.yml` is a thin caller of `Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main`
And `.github/workflows/release.yml` is a thin caller of `Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main`
And CodeQL, dependency-review, and commitlint remain thin shared workflow callers using `@main`
And caller workflows retain only module-specific triggers, concurrency, permissions, secrets, and workflow inputs.

Given EventStore Server.Tests contains both deterministic in-process tests and `Category=LiveSidecar` tests
When CI is migrated to the shared reusable workflow pattern
Then the deterministic release gate still runs `Server.Tests` with `Category!=LiveSidecar`
And live-sidecar tests still run in a dedicated non-release-blocking lane with DAPR initialized
And the migration does not make `Category=LiveSidecar` failures block semantic-release publishing.

Given EventStore package release is manifest-governed
When shared CI runs consumer/package validation
Then EventStore provides compatible `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, and `scripts/validate-consumer-package-references.py` entry points
And those scripts preserve `tools/release-packages.json` as the package inventory
And validation rejects submodule packages or package outputs outside the EventStore manifest.

Given EventStore release runs through `domain-release.yml@main`
When semantic-release publishes artifacts
Then NuGet publishing remains scoped to manifest-listed `Hexalith.EventStore.*` packages
And container publishing uses explicit EventStore project-to-repository mappings approved for release
And no sample or admin container is published accidentally.

Given the migration completes
When docs and workflow references are scanned
Then `docs/ci.md`, `.releaserc.json`, package-governance tests, and CI documentation describe the Tenants-style reusable workflow pattern accurately.
```

Rationale: This captures the requested Tenants parity while preserving EventStore-specific release and live-sidecar constraints.

### CI Workflow Target Shape

Artifact: `.github/workflows/ci.yml`

OLD:

```yaml
jobs:
  ci:
    name: ci / build-and-test
    runs-on: ubuntu-latest
    steps:
      # local checkout, submodule init, restore, build, test manifest,
      # unit tests, coverage summary, artifact upload
```

NEW:

```yaml
jobs:
  ci:
    uses: Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main
    with:
      solution: Hexalith.EventStore.slnx
      run-consumer-validation: true
      unit-test-projects: |
        tests/Hexalith.EventStore.Contracts.Tests
        tests/Hexalith.EventStore.Client.Tests
        tests/Hexalith.EventStore.Testing.Tests
        tests/Hexalith.EventStore.SignalR.Tests
        tests/Hexalith.EventStore.Admin.Abstractions.Tests
        tests/Hexalith.EventStore.Admin.Cli.Tests
        tests/Hexalith.EventStore.Admin.Mcp.Tests
        tests/Hexalith.EventStore.Admin.Server.Tests
        tests/Hexalith.EventStore.Admin.Server.Host.Tests
        tests/Hexalith.EventStore.Admin.UI.Tests
        tests/Hexalith.EventStore.AppHost.Tests
        tests/Hexalith.EventStore.DomainService.Tests
        tests/Hexalith.EventStore.QueryRouting.Tests
        tests/Hexalith.EventStore.RestApi.Generators.Tests
        tests/Hexalith.EventStore.Sample.Tests
        tests/Hexalith.EventStore.Testing.Integration.Tests
      # Requires shared-workflow support or a test-project split before finalizing:
      # blocking filtered lane: tests/Hexalith.EventStore.Server.Tests with Category!=LiveSidecar
      # advisory live lane: tests/Hexalith.EventStore.Server.Tests with Category=LiveSidecar
```

Rationale: This is the Tenants pattern, but the Server.Tests filter support is a prerequisite to avoid violating NFR10.

### Release Workflow Target Shape

Artifact: `.github/workflows/release.yml`

OLD:

```yaml
jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      # local checkout, submodule init, setup dotnet/node, npm ci,
      # npx semantic-release
```

NEW:

```yaml
jobs:
  release:
    if: >-
      github.event.workflow_run.conclusion == 'success' &&
      github.event.workflow_run.event == 'push'
    permissions:
      contents: write
      issues: write
      pull-requests: write
    uses: Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main
    with:
      solution: Hexalith.EventStore.slnx
      publish-containers: true
      container-projects: |
        src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore
    secrets:
      NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
      HEXALITH_ZOT_USERNAME: ${{ secrets.HEXALITH_ZOT_USERNAME }}
      HEXALITH_ZOT_API_KEY: ${{ secrets.HEXALITH_ZOT_API_KEY }}
```

Rationale: This mirrors Tenants. Additional EventStore container mappings such as admin server/UI or sample hosts should be added only if release owners explicitly approve publishing them.

### Commitlint Trigger Parity

Artifact: `.github/workflows/commitlint.yml`

OLD:

```yaml
on:
  pull_request:
    branches: [main]
```

NEW:

```yaml
on:
  pull_request:
    branches: [main]
  push:
    branches: [main]
```

Rationale: Tenants runs commitlint on PRs and direct pushes to `main` because semantic-release derives versions from commit messages and direct pushes can bypass a PR-only check.

### EventStore Script Compatibility

Artifacts:

- `scripts/pack-release-packages.py`
- `scripts/validate-nuget-packages.py`
- `scripts/validate-consumer-package-references.py`
- existing `tools/pack-release-packages.py`
- existing `tools/validate-release-packages.py`
- existing `tools/release-packages.json`

OLD:

```text
domain-ci.yml run-consumer-validation expects scripts/...
EventStore currently provides tools/pack-release-packages.py and tools/validate-release-packages.py.
```

NEW:

```text
Add scripts/ entry points compatible with domain-ci.yml.
Keep tools/release-packages.json as the manifest.
Keep or wrap existing tools/ scripts so .releaserc.json and package-governance tests remain manifest-driven.
Add consumer dependency validation equivalent to Tenants, adapted to the EventStore package inventory.
```

Rationale: This removes custom workflow logic while preserving EventStore's stronger manifest-governed release package set.

## 5. Implementation Handoff

Change scope classification: Moderate.

Recommended handoff:

- Developer agent: implement EventStore repository changes after approval.
- Hexalith.Builds maintainer: approve and implement reusable `domain-ci.yml` support for filtered blocking/advisory test lanes, unless the team chooses the alternative test-project split.
- Release owner: approve the EventStore container publish mapping. Initial parity should publish only `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore` unless admin/sample container publishing is explicitly approved.
- Test Architect: confirm NFR10 remains satisfied after migration: deterministic tests gate release; live-sidecar tests remain visible but non-release-blocking.

Implementation sequence:

1. Add or obtain shared workflow support for filtered test lanes, or split `Server.Tests` so shared CI can express non-live and live tiers separately.
2. Add EventStore-compatible `scripts/` release validation wrappers and consumer package validation.
3. Replace `ci.yml` with a thin `domain-ci.yml@main` caller.
4. Replace `release.yml` with a thin `domain-release.yml@main` caller.
5. Remove or retire `integration.yml` only after live-sidecar execution is preserved in the shared CI shape as a non-release-blocking lane.
6. Add commitlint push trigger parity.
7. Update `docs/ci.md` and package-governance tests.
8. Validate with a package-mode restore/build, focused package validation, workflow syntax checks, and an explicit NFR10 lane audit.

Success criteria:

- EventStore `.github/workflows/ci.yml` and `.github/workflows/release.yml` match the Tenants reusable-workflow pattern.
- EventStore release still publishes only manifest-listed packages.
- `Category=LiveSidecar` tests do not block semantic-release.
- Commitlint covers direct pushes to `main`.
- CI docs describe the new reusable workflow pattern.

## 6. Checklist Results

| Checklist item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | N/A | User-triggered CI/CD parity request, not discovered by one implementation story. |
| 1.2 Core problem | Done | EventStore CI/release drift from Tenants reusable workflow pattern. |
| 1.3 Evidence | Done | Compared EventStore workflows, Tenants workflows, and shared `domain-ci`/`domain-release`. |
| 2.1 Current epic impact | Done | Epic 3 remains valid but Story 3.7 needs expanded scope. |
| 2.2 Epic-level changes | Done | No new epic required. |
| 2.3 Remaining epics | Done | Story 7.4 boundary preserved. |
| 2.4 New/obsolete epics | Done | None. |
| 2.5 Priority/order | Done | Shared workflow filter support or test split must precede CI replacement. |
| 3.1 PRD conflicts | Done | Exact direct copy would violate NFR10; recommendation preserves NFR10. |
| 3.2 Architecture conflicts | Done | AD-11 and AD-12 remain aligned. |
| 3.3 UX conflicts | N/A | No UI impact. |
| 3.4 Other artifacts | Done | Workflow files, release scripts, docs, package tests affected. |
| 3.5 Affected story files | N/A | No active Story 3.7 implementation file exists. |
| 4.1 Direct adjustment | Viable | Selected, with prerequisite shared workflow/test split. |
| 4.2 Rollback | Not viable | No rollback needed. |
| 4.3 MVP review | Not viable | MVP scope unchanged. |
| 4.4 Path forward | Done | Direct adjustment through Story 3.7. |
| 5.1 Issue summary | Done | Included. |
| 5.2 Impact summary | Done | Included. |
| 5.3 Recommendation | Done | Included. |
| 5.4 MVP impact | Done | No MVP change. |
| 5.5 Handoff | Done | Developer, Build maintainer, Release owner, Test Architect. |
| 5.6 Story rewrite gate | N/A | No active affected story file; update planning story before story creation. |
| 6.1 Checklist completion | Done | All applicable items addressed. |
| 6.2 Proposal accuracy | Done | Based on loaded artifacts and workflow comparison. |
| 6.3 User approval | Action-needed | Awaiting Administrator approval. |
| 6.4 Sprint status update | Action-needed | Update only after approval and story creation. |
| 6.5 Next steps | Done | Handoff and success criteria defined. |

## 7. Approval

Approved by Administrator at 2026-07-09T12:53:52+02:00.

Decision:

- Continue with the Moderate scope handoff.
- Preserve EventStore NFR10 while aligning the workflow shape with Tenants.

## 8. Handoff Log

| Time | Route | Notes |
| --- | --- | --- |
| 2026-07-09T12:53:52+02:00 | Developer agent + Hexalith.Builds maintainer + Release owner + Test Architect | Implement Tenants-style reusable CI/release callers only after filtered Server.Tests support or an equivalent test-project split preserves the deterministic/non-live and live-sidecar lane separation. |

