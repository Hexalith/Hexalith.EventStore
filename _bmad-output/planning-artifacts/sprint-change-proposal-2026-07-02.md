# Sprint Change Proposal - CI/CD Reuse and Supply-Chain Hardening

- **Date:** 2026-07-02
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Batch (direct implementation requested)
- **Change classification:** Minor/Moderate
- **Status:** Implemented locally; pending review/commit

---

## 1. Issue Summary

The repository and its root-declared submodules need CI/CD behavior that is
secure by default, reproducible, and reusable across Hexalith modules.

The triggering request was to apply the CI/CD improvement recommendations across
the root repository and all root-declared submodules. The evidence came from the
current workflow set:

- EventStore still carried local copies of CodeQL, dependency-review, and
  commitlint workflow logic.
- Hexalith.Builds LLM instructions define `@main` as the required reference for
  Hexalith.Builds actions and reusable workflows, but the previous correction
  had pinned several consumers to a commit SHA.
- EventStore release packaging kept the NuGet package list inside a long
  `.releaserc.json` command string.
- Release dependency installation did not run the npm signature/provenance check
  already used by the shared package-release action.

Formal PRD and epic documents were not present under
`_bmad-output/planning-artifacts`; impact analysis used project-context files,
existing sprint change proposals, workflow files, and CI documentation.

---

## 2. Impact Analysis

### Epic Impact

No product epic is resequenced. This is a CI/CD infrastructure correction that
supports ongoing implementation by reducing duplicated workflow policy and
making release scope reviewable.

### Story Impact

No active story acceptance criteria are changed. Future CI/CD stories should
inherit these constraints:

- Hexalith.Builds actions and reusable workflows use `@main`; third-party
  actions remain SHA-pinned;
- module-specific package scope is declared in a manifest, not in opaque release
  command strings;
- long-lived publish secrets remain documented until an OIDC trusted-publishing
  policy is configured externally.

### Artifact Impact

| Artifact | Impact |
|----------|--------|
| `.github/workflows/codeql.yml` | Converted to a thin caller of the shared Hexalith.Builds CodeQL workflow using `@main`. |
| `.github/workflows/dependency-review.yml` | Converted to a thin caller of the shared dependency-review workflow using `@main`. |
| `.github/workflows/commitlint.yml` | Converted to a thin caller of the shared commitlint workflow using `@main`. |
| `.github/workflows/release.yml` | Added npm cache for release tooling installation. |
| `.releaserc.json` | Replaced embedded package loop with manifest-driven pack and validation scripts. |
| `tools/release-packages.json` | New source of truth for EventStore NuGet package publish scope. |
| `tools/pack-release-packages.py` | New release pack script that disables source/project-reference mode for packages. |
| `tools/validate-release-packages.py` | New validator that fails release if package output drifts from manifest. |
| `docs/ci.md` | Updated to document shared security workflow callers and manifest-driven packaging. |
| `docs/ci-secrets-checklist.md` | Updated release tooling notes and Trusted Publishing hardening backlog. |
| `references/Hexalith.Builds/AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` | Reinforced that Hexalith.Builds action and reusable workflow references must use `@main`. |
| `references/Hexalith.Builds/.github/workflows/ci-cd-standards.md` | Updated standards to document the Hexalith.Builds `@main` exception to third-party SHA pinning. |
| `references/Hexalith.Commons/.github/workflows/*` | Restored Hexalith.Builds action/reusable workflow calls to `@main`. |
| `references/Hexalith.FrontComposer/.github/workflows/*` | Restored Hexalith.Builds action/reusable workflow calls to `@main`. |
| `references/Hexalith.Memories/.github/workflows/*` | Restored Hexalith.Builds action/reusable workflow calls to `@main`. |
| `references/Hexalith.PolymorphicSerializations/.github/workflows/*` | Restored Hexalith.Builds action/reusable workflow calls to `@main`. |
| `references/Hexalith.Tenants/.github/workflows/*` | Restored Hexalith.Builds action/reusable workflow calls to `@main`. |

`references/Hexalith.AI.Tools` has no `.github/workflows` directory in this
checkout, so no CI/CD workflow change was applicable there.

---

## 3. Recommended Approach

**Selected path: Direct Adjustment.**

This is the least risky path because it does not change product scope, runtime
architecture, or release package set. It moves repeated security workflow logic
behind shared reusable workflows, follows the Hexalith.Builds `@main` policy,
and makes EventStore release scope manifest-driven.

- **Effort:** Medium, because it crosses the root repo and multiple submodule
  working trees.
- **Risk:** Low/Medium. YAML caller changes are straightforward, but consumers
  intentionally track the current Hexalith.Builds `main` branch.
- **Timeline impact:** No sprint resequencing required.

Rejected alternatives:

- **Rollback:** Not useful; no implementation path is being reversed.
- **MVP review:** Not applicable; product scope is unchanged.
- **Full CI unification now:** Deferred. FrontComposer and Memories have
  intentionally specialized release/test governance that should not be collapsed
  into a generic workflow in this change.

---

## 4. Detailed Change Proposals

### CP-1 - Hexalith.Builds References Use `@main`

**OLD:**

```yaml
uses: Hexalith/Hexalith.Builds/.github/workflows/codeql.yml@06c0aaaae4c373ba8042a42fe7f0c2ea22eeb7cb
```

**NEW:**

```yaml
uses: Hexalith/Hexalith.Builds/.github/workflows/codeql.yml@main
```

**Rationale:** Hexalith.Builds is the shared CI/CD source of truth. Its own LLM
instructions require consumers to use the latest `main` branch for Hexalith.Builds
actions and reusable workflows. This is an intentional exception to third-party
action SHA pinning.

### CP-2 - EventStore Uses Shared Security Gates

**OLD:** EventStore duplicated CodeQL, dependency-review, and commitlint job
steps locally.

**NEW:** EventStore keeps only triggers, concurrency, explicit caller
permissions, and `@main` shared workflow calls.

**Rationale:** Shared gate behavior belongs in Hexalith.Builds. EventStore keeps
module-specific trigger policy.

### CP-3 - Manifest-Driven NuGet Package Scope

**OLD:**

```json
"prepareCmd": "for p in Contracts Client Server ...; do dotnet pack ...; done"
```

**NEW:**

```json
"prepareCmd": "python3 tools/pack-release-packages.py ./nupkgs ${nextRelease.version} && python3 tools/validate-release-packages.py ./nupkgs ${nextRelease.version}"
```

**Rationale:** Package scope is now reviewable in
`tools/release-packages.json`, and release fails if output drifts from the
manifest.

### CP-4 - Release Tooling Install Cache

**OLD:** `release.yml` ran `npm ci` without npm dependency cache.

**NEW:** `release.yml` uses `actions/setup-node` with `cache: npm`, then runs
`npm ci` before semantic-release.

**Rationale:** Release tooling installs are deterministic through
`package-lock.json`; cache reduces repeated install cost without changing the
dependency source of truth.

### CP-5 - Trusted Publishing Backlog

**OLD:** `NUGET_API_KEY` was listed only as an annually rotated secret.

**NEW:** The secrets checklist records that `NUGET_API_KEY` remains required
until a NuGet.org Trusted Publishing policy is configured, after which release
should move to OIDC-based publishing and remove the long-lived secret.

**Rationale:** Trusted Publishing requires external NuGet.org repository policy;
the repository can document the migration target without pretending the external
trust relationship already exists.

### CP-6 - npm Signature Gate Deferred

**OLD:** No npm signature/provenance gate.

**NEW:** The secrets checklist records the blocked validation result:
`npm audit signatures` fails after `npm ci` because semantic-release pulls
`tunnel@0.0.6`, whose npm registry signing key expired on 2025-01-29.

**Rationale:** A best-practice gate that is known to fail on the current locked
dependency tree should not be added to the release workflow. Keep it as explicit
hardening backlog until it can pass reproducibly.

---

## 5. Implementation Handoff

- **Scope:** Minor/Moderate.
- **Route:** Developer agent for validation, then normal code review.
- **Implementation status:** Applied locally in root repository and root-declared
  submodules that have workflows.

### Success Criteria

1. YAML files parse successfully.
2. EventStore release package manifest validates.
3. EventStore release pack script dry-run succeeds.
4. All remote `Hexalith/Hexalith.Builds/...` workflow/action references use
   `@main`.
5. Third-party action references remain SHA-pinned by the shared workflows.

### Follow-Ups

- Configure NuGet Trusted Publishing for EventStore, then replace
  `NUGET_API_KEY` with OIDC-based publishing.
- Resolve the `npm audit signatures` blocker (`tunnel@0.0.6` expired signing
  key) before making npm signature verification a release gate.
- Add artifact attestations/SBOM to EventStore package and container release
  paths in a dedicated release-hardening story.
- Consider manifest-driven test tiers for EventStore after the package manifest
  pattern settles.

---

## 6. Checklist Summary

| Item | Status | Notes |
|------|--------|-------|
| 1.1 Triggering story | N/A | User requested CI/CD correction across repo and submodules. |
| 1.2 Core problem | Done | Reusability, reproducibility, and release package drift risk. |
| 1.3 Evidence | Done | Workflow and release configuration review. |
| 2.1-2.5 Epic impact | Done | No epic resequencing required. |
| 3.1 PRD conflicts | N/A | No MVP/product scope change. |
| 3.2 Architecture conflicts | Done | CI/CD ownership boundary clarified. |
| 3.3 UI/UX conflicts | N/A | No UX change. |
| 3.4 Other artifacts | Done | Workflows, release tooling, CI docs, submodule workflow callers. |
| 4.1 Direct adjustment | Viable | Selected. |
| 4.2 Rollback | Not viable | No rollback simplifies this. |
| 4.3 MVP review | Not viable | Product scope unchanged. |
| 4.4 Recommended path | Done | Direct adjustment with follow-up backlog. |
| 5.1-5.5 Proposal components | Done | Included above. |
| 6.1-6.2 Review | Done | Local validation pending below. |
| 6.3 Approval | Done | User requested direct implementation. |
| 6.4 Sprint status | N/A | No epic/story status changes. |
| 6.5 Handoff | Done | Developer validation and review. |
