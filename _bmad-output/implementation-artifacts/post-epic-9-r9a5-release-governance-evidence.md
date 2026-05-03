# Post-Epic-9 R9-A5: Release Governance Evidence

Status: ready-for-dev

<!-- Source: epic-9-retro-2026-04-30.md - R9-A5 -->
<!-- Source: post-epic-10-r10a8-r9-r10-follow-through-tracking.md - R9/R10 reconciliation -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **release owner responsible for repository governance**,
I want semantic-release baseline, branch protection, required checks, and release-secret evidence recorded in the repository,
so that release readiness is based on auditable controls instead of assumptions about GitHub settings and tag history.

## Story Context

Epic 9 left R9-A5 open because release workflows and tags exist, but release governance evidence is incomplete. R10-A8 later confirmed that local and remote release tags exist through `v3.5.0` at the time of reconciliation, while remote `v0.0.0` was not evidenced and repository governance settings were not fully recorded. Current repository state now shows release tags through `v3.8.0`, `.releaserc.json` targets `main` with `tagFormat: v${version}`, and `.github/workflows/release.yml` runs semantic-release on pushes to `main`.

This story is an evidence and documentation story. It should not redesign CI/CD, change release versioning, rotate secrets, publish packages, or alter branch protection directly unless the evidence check exposes a concrete defect and the fix is deliberately scoped. The primary deliverable is a durable, sanitized governance record that a future release reviewer can inspect without needing admin memory.

Current HEAD at story creation: `5821f90`.

## Acceptance Criteria

1. **Release governance evidence artifact exists.** Create `_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/evidence-2026-05-03.md` or an equivalent dated evidence file. The file must record the exact command/query source, working directory when local, timestamp in UTC, actor/role performing the check, repository, branch, commit SHA, working-tree state, and whether evidence was captured from local git, GitHub CLI/API, workflow YAML, or manual admin inspection. Include a required evidence matrix with columns `Control`, `Evidence Source`, `Command/UI/API Used`, `Observed Result`, `Timestamp`, `Conclusion`, and `Gap/Blocker`.

2. **Semantic-release baseline is verified without inventing history.** Record local and remote release tag state with exact read-only commands, including `git tag --list "v*" --sort=-v:refname`, `git ls-remote --tags origin "v*"`, and optional inspection-only commands such as `git show-ref --tags`. Explicitly state whether `v0.0.0` exists locally and remotely, whether the latest remote release tag is present locally, and whether `.releaserc.json` `tagFormat` matches the observed tags. Do not create, delete, sign, retag, fetch with forced tag updates, push, force-push, or otherwise mutate tags as part of this story unless Jerome explicitly approves that release-history change.

3. **Release workflow contract is documented.** Inspect `.github/workflows/release.yml`, `.releaserc.json`, `package.json`, `CHANGELOG.md`, and `docs/ci.md`. Record the release trigger, branch, permissions, checkout depth, test scope, package publishing path, GitHub Release behavior, changelog update behavior, and required secrets. The evidence must distinguish workflow configuration from proof that the latest workflow run succeeded.

4. **Branch protection evidence is captured or blocked honestly.** Use the GitHub UI, GitHub CLI, or GitHub API to record the current `main` branch protection / ruleset state. Evidence must include required status checks, whether direct pushes are restricted, whether reviews are required, whether linear history or signed commits are required, and whether admins are included. If the automation lacks permission to view repository rules, record `permission-blocker` with the exact command/page/API endpoint attempted, timestamp, safe actor identity or permission context, non-sensitive returned error, and blocked acceptance criterion; do not mark branch protection verified by inference from `docs/ci.md`.

5. **Required checks are reconciled against workflow jobs.** Compare the protected required checks, if available, with `.github/workflows/ci.yml`, `.github/workflows/docs-validation.yml`, and `.github/workflows/release.yml`. Include two tables: one for documented-versus-observed branch protection/ruleset required checks, and one for release workflow executed checks. At minimum, classify `commitlint`, `secret-scan`, `build-and-test`, `aspire-tests`, documentation validation, and any `Hexalith.EventStore.Server.Tests` release path as `required`, `recommended`, `optional`, `executed-not-required`, `permission-blocked`, or `unknown`. The story should preserve the current policy that Tier 3 Aspire tests are non-blocking unless repository rules say otherwise.

6. **Release-secret presence is verified without exposing secrets.** Verify whether `NUGET_API_KEY` is configured for the release workflow and whether `GITHUB_TOKEN` permissions are sufficient for tags/releases. Acceptable evidence is presence/visibility metadata from GitHub Actions secrets, an admin screenshot summary without values, a failed/successful workflow log that proves availability without printing it, or a permission-blocker note. Never print, copy, hash, decode, test, partially reveal, or infer secret values, masked values, token fragments, lengths, prefixes, suffixes, or screenshots containing unrelated secret names. Do not validate secrets by attempting a publish.

7. **Most recent release evidence is linked.** Record latest local tag, latest remote tag, latest GitHub Release, latest release workflow run on `main`, and visible NuGet/package outcome when accessible. Include tag, commit SHA, release URL or workflow run URL, conclusion, package/publish outcome if visible, and any skipped or failed steps. If these sources disagree, route the mismatch to `Release Governance Gaps` instead of choosing one silently. If GitHub access is unavailable, record local evidence from `CHANGELOG.md`, `git show --no-patch --decorate <latest-tag>`, and remote tag lookup, then classify the GitHub-side release evidence as blocked.

8. **Docs reflect the audited governance state.** Update only `docs/ci.md` or a focused release-governance docs section only if the evidence shows the current documentation is stale, incomplete, or missing the governance caveat. Preserve the difference between documented intended policy and observed repository settings. If no docs edit is needed, record the reason in the evidence artifact. Do not overstate controls that were not verified, and do not use docs edits to change release policy or workflow behavior.

9. **No release side effects occur by default.** Do not run `npx semantic-release`, publish NuGet packages, modify `CHANGELOG.md` through semantic-release, trigger deploy workflows, change branch protection, rotate secrets, or force-push tags during this story. If a dry run is useful, use a no-publish dry-run command and record that it is not release evidence.

10. **Governance gaps are routed, not hidden.** Add a mandatory "Release Governance Gaps" section to the evidence file. It must contain either `No gaps found based on available evidence.` or a dated list of unknown, contradictory, blocked, or stale evidence with owner, impact, required follow-up, and recommended story/status routing. Do not mark R9-A5 complete while a high-impact governance item remains `unknown` without either an accepted non-action decision or a follow-up row.

11. **Validation is appropriate for docs/evidence.** Run the repository's docs validation command when practical, or at least the targeted markdown lint/link checks for changed docs and evidence files. The evidence artifact must record the exact validation command, result, and warnings; if no markdown validator is available, record the fallback CommonMark/link/path sanity check. If only BMAD evidence and docs change, product tests are not required. If workflow YAML changes, run or record a YAML parse/lint check.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names the release-governance evidence result. At code-review signoff, both become `done` only after evidence artifacts and any docs updates are recorded.

## Scope Boundaries

- Do not publish releases, packages, or tags.
- Do not modify branch protection, rulesets, required checks, repository secrets, environments, or deployment credentials unless the project lead explicitly approves the change.
- Do not expose secret values, token fragments, private runner details, production hostnames, or screenshots containing unrelated repository settings.
- Do not rewrite release history or normalize old tags unless approved as a separate release-management action.
- Do not run `git push`, `git tag -d`, tag force operations, `gh release create`, `gh workflow run`, `npx semantic-release`, or semantic-release dry runs unless Jerome explicitly approves the side-effect or misleading-evidence risk.
- Do not change semantic-release versioning behavior, commit analyzer rules, changelog format, or package publishing paths unless the evidence proves a concrete defect.
- Do not use this story to resolve query-pipeline proof gaps owned by R9-A1, R9-A2, or R9-A8.
- Do not initialize or update nested submodules.
- Do not edit generated preflight JSON audit files.

## Implementation Inventory

| Area | File / command | Expected use |
|---|---|---|
| Release config | `.releaserc.json` | Verify branch, tag format, changelog, GitHub release, NuGet publish command |
| Release workflow | `.github/workflows/release.yml` | Verify trigger, permissions, tests, `semantic-release`, and secret names |
| CI workflow | `.github/workflows/ci.yml` | Compare required/protected checks with actual job names |
| Docs workflow | `.github/workflows/docs-validation.yml` | Classify documentation validation requirement/recommendation |
| CI docs | `docs/ci.md` | Update only when observed governance differs from documented policy |
| Release history | `CHANGELOG.md`; `git tag --list "v*"`; `git ls-remote --tags origin "v*"` | Record local/remote tag and changelog evidence |
| GitHub governance | `gh api repos/:owner/:repo/branches/main/protection`; ruleset endpoints or UI export | Capture branch protection/ruleset evidence or permission blocker |
| Evidence artifact | `_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/` | Store sanitized dated evidence and blockers |

## Tasks / Subtasks

- [ ] Task 0: Baseline the release-governance scope (AC: #1, #2, #9)
    - [ ] 0.1 Record baseline HEAD and confirm this story is still `ready-for-dev`.
    - [ ] 0.2 Create the dated evidence artifact under `_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/`.
    - [ ] 0.3 Record the exact "no release side effects" guardrail before running evidence commands.

- [ ] Task 1: Verify semantic-release and tag state (AC: #2, #3, #7)
    - [ ] 1.1 Run `git tag --list "v*"` and record local tag range, including whether `v0.0.0` exists.
    - [ ] 1.2 Run `git ls-remote --tags origin "v*"` and record remote tag range, including whether `v0.0.0` exists.
    - [ ] 1.3 Inspect `.releaserc.json`, `package.json`, `CHANGELOG.md`, and `.github/workflows/release.yml`.
    - [ ] 1.4 Link the latest release tag to its commit and GitHub Release/workflow run when accessible.

- [ ] Task 2: Capture branch protection and required checks (AC: #4, #5)
    - [ ] 2.1 Query `main` branch protection or repository rulesets with GitHub CLI/API, or record manual UI evidence.
    - [ ] 2.2 Compare required checks to `ci.yml` job names and `docs-validation.yml`.
    - [ ] 2.3 Record whether `aspire-tests` is non-blocking by policy, required by rules, or unknown.
    - [ ] 2.4 If permissions block the check, record the exact command/API response and classify the unknowns.

- [ ] Task 3: Verify release-secret and permission evidence safely (AC: #6, #7)
    - [ ] 3.1 Verify `NUGET_API_KEY` presence by metadata, workflow evidence, or admin inspection without exposing values.
    - [ ] 3.2 Verify `GITHUB_TOKEN` release/tag permissions from workflow `permissions:` and observed release behavior.
    - [ ] 3.3 Record any environment, ruleset, or secret visibility blockers separately from configuration defects.

- [ ] Task 4: Update docs and route gaps (AC: #8, #10)
    - [ ] 4.1 Update `docs/ci.md` only if observed governance differs from its Branch Protection, Secrets, or Workflow sections.
    - [ ] 4.2 Add a "Release Governance Gaps" section to the evidence artifact when any item remains unknown or deficient.
    - [ ] 4.3 Route high-impact unknowns to a follow-up story/status row or record a dated accepted non-action decision with owner and revisit trigger.

- [ ] Task 5: Validate and close bookkeeping (AC: #11, #12)
    - [ ] 5.1 Run targeted markdown lint/link validation for changed docs and evidence files, or record why unavailable.
    - [ ] 5.2 If workflow YAML changes, run a YAML parse/lint check.
    - [ ] 5.3 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row.

## Dev Notes

### Architecture Guardrails

- Treat release governance as a control-plane evidence concern. The product runtime, query pipeline, and EventStore APIs should remain untouched.
- `main` is the release branch according to `.releaserc.json` and `.github/workflows/release.yml`; do not route evidence against another branch unless repository settings prove a ruleset applies differently.
- `semantic-release` owns version calculation, tags, GitHub Releases, changelog updates, and NuGet package publishing. Do not duplicate its behavior with manual tag or changelog edits.
- Branch protection and required checks are external GitHub settings. Repository docs can describe intended policy, but only GitHub settings or admin evidence can prove enforcement.
- Secret evidence must be presence/outcome metadata only. Never print secret values or masked values into BMAD artifacts.
- Record blockers as blockers. A permission error is useful evidence when it names exactly what could not be verified.

### Current-Code Intelligence

- `.github/workflows/release.yml` runs on push to `main`, uses `contents: write`, initializes Dapr, runs all listed Tier 1/Tier 2 test projects including `Hexalith.EventStore.Server.Tests`, then runs `npx semantic-release` with `GITHUB_TOKEN` and `NUGET_API_KEY`.
- `.releaserc.json` uses `branches: ["main"]`, `tagFormat: "v${version}"`, `@semantic-release/exec` to build/pack/push NuGet packages, `@semantic-release/github` to create releases/assets, and `@semantic-release/git` to commit `CHANGELOG.md`.
- `docs/ci.md` currently states `main` is protected, required checks are `commitlint` for PRs, `secret-scan`, and `build-and-test`, and `aspire-tests` is not required.
- Current local/remote tag inspection at story creation showed release tags through `v3.8.0`; previous R10-A8 evidence found no remote `v0.0.0`.
- `package.json` pins the semantic-release toolchain as dev dependencies and keeps the npm package private.
- `.github/workflows/ci.yml` includes `commitlint`, `secret-scan`, `build-and-test`, and `aspire-tests`; `aspire-tests` has `continue-on-error: true`.

### Previous Story Intelligence

- Epic 9 R9-A5 was left open because remote tag state and repository release controls were not fully evidenced during the retrospective.
- R10-A8 created this backlog row after finding release workflows/docs and remote release tags, but not enough branch protection, required-check, secret, and baseline-tag evidence to close R9-A5.
- R9-A7 established a reusable follow-through rule: unresolved governance evidence should be either direct evidence, a visible owning story/status row, or an accepted non-action decision with owner, residual risk, and revisit trigger.
- Do not repeat R10-A8's false-closure risk. A related workflow file is not the same as proof that GitHub enforcement is configured.

### Testing Standards

- Markdown/YAML/evidence-only changes do not require product tests.
- Prefer targeted validation:
    - `npx --yes markdownlint-cli2 "_bmad-output/implementation-artifacts/post-epic-9-r9a5-release-governance-evidence.md" "_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/**/*.md" "docs/ci.md"`
    - `npx --yes markdown-link-check <changed-md-files>` when links are added or changed.
    - YAML parse/lint only if workflow YAML changes.
- If `docs/ci.md` changes, verify relative links still resolve.
- Product tests are not needed unless implementation unexpectedly changes product code or workflow scripts.

### Latest Technical Information

- The repository currently uses semantic-release `^24.2.3` with `@semantic-release/github` `^11.0.1`, `@semantic-release/exec` `^7.0.3`, and Node `lts/*` in GitHub Actions.
- Release and CI workflows pin third-party GitHub Actions by full commit SHA with version comments. Preserve this policy for any workflow edit.
- GitHub branch protection and rulesets are repository settings outside git history; capture them through `gh api`, GitHub UI, or admin-provided export instead of assuming docs are authoritative.

### Project Structure Notes

- Keep governance evidence under `_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/`.
- Keep story bookkeeping in `_bmad-output/implementation-artifacts/post-epic-9-r9a5-release-governance-evidence.md` and `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- Keep durable user-facing CI/release policy updates in `docs/ci.md`.
- Do not edit `.agents/skills/`, `.claude/skills/`, `_bmad/bmm/`, or the tools submodule for this story.

### Required Evidence Artifact Shape

The evidence file should use a stable outline so reviewers can audit closure without replaying the whole story:

1. Metadata: UTC timestamp, local timezone if relevant, repo, branch, commit SHA, safe actor/role, working directory, working-tree state.
2. Semantic-release baseline: `.releaserc.json`, `package.json`, release workflow, changelog observations.
3. Local and remote tag evidence: exact read-only commands, sanitized output excerpt, interpretation.
4. Branch protection/ruleset evidence or permission blocker.
5. Required checks reconciliation: documented required checks versus observed GitHub required checks, plus release workflow executed checks.
6. Secret presence evidence: sanitized presence/visibility metadata or permission blocker only.
7. Latest release/workflow/package evidence: local tag, remote tag, GitHub Release, release workflow run, NuGet/package visibility when accessible.
8. Release Governance Gaps: mandatory `No gaps found based on available evidence.` or dated owner/impact/follow-up rows.
9. Validation record: exact command, result, warnings, and fallback if tooling is unavailable.
10. Story bookkeeping summary.

Evidence entries must distinguish observed fact, inferred conclusion, permission blocker, and recommended follow-up.

## References

- [Source: `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md#9-action-items`] - R9-A5 source action: confirm semantic-release baseline and release governance.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md`] - R9/R10 reconciliation that opened this story row.
- [Source: `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md#13-r9-follow-through-annotation-recorded-by-r10-a8-reconciliation`] - R9-A5 disposition and missing evidence summary.
- [Source: `.github/workflows/release.yml`] - Release workflow contract.
- [Source: `.github/workflows/ci.yml`] - CI job names and non-blocking Tier 3 policy.
- [Source: `.github/workflows/docs-validation.yml`] - Documentation validation workflow.
- [Source: `.releaserc.json`] - semantic-release branches, tag format, and plugins.
- [Source: `package.json`] - semantic-release toolchain dependencies.
- [Source: `CHANGELOG.md`] - generated release notes and latest release tag evidence.
- [Source: `docs/ci.md`] - documented CI, branch protection, and release policy.
- [Source: `commitlint.config.mjs`] - Conventional Commit lint configuration.

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent.

### Debug Log References

TBD by dev-story agent.

### Completion Notes List

TBD by dev-story agent.

### File List

TBD by dev-story agent.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-03 | 0.1 | Created ready-for-dev R9-A5 release governance evidence story. | Codex automation |
| 2026-05-03 | 0.2 | Party-mode review applied evidence-format, read-only, permission-blocker, secret-handling, and governance-gap hardening. | Codex automation |

## Verification Status

Story created for pre-development hardening. Implementation and validation are pending `bmad-dev-story`.

## Party-Mode Review

- Date/time: 2026-05-03T13:32:13Z
- Selected story key: post-epic-9-r9a5-release-governance-evidence
- Command/skill invocation used: `/bmad-party-mode post-epic-9-r9a5-release-governance-evidence; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: All reviewers recommended `needs-story-update`. The shared risk was false-positive governance closure from prose evidence, ambiguous GitHub permission blockers, unclear required-check authority, insufficient release-source reconciliation, unsafe secret/tag evidence handling, open-ended docs scope, and underspecified validation records.
- Changes applied: Added a required evidence matrix and artifact outline; narrowed tag/release collection to read-only evidence unless Jerome approves side effects; defined permission-blocker content; split required checks into branch-protection/ruleset and release-workflow evidence; hardened secret handling to metadata-only proof; required latest local tag, remote tag, GitHub Release, workflow, and package observations; made Release Governance Gaps mandatory even when empty; narrowed docs update scope; required exact validation command/result recording.
- Findings deferred: Whether `aspire-tests` should become branch-protection blocking; whether `Hexalith.EventStore.Server.Tests` should remain in release-required validation despite known CA2007 build failures; whether GitHub branch protection should migrate to rulesets; whether release secrets should be rotated or renamed; whether semantic-release should continue targeting only `main` with `v${version}` tags.
- Final recommendation: needs-story-update
