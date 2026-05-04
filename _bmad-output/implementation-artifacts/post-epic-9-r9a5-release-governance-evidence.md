# Post-Epic-9 R9-A5: Release Governance Evidence

Status: done

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

1. **Release governance evidence artifact exists.** Create `_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/evidence-2026-05-03.md` or an equivalent dated evidence file. The file must record the exact command/query source, working directory when local, timestamp in UTC, actor/role performing the check, repository, branch, commit SHA, working-tree state, and whether evidence was captured from local git, GitHub CLI/API, workflow YAML, or manual admin inspection. Include a required evidence matrix with columns `Control`, `Evidence Source`, `Command/UI/API Used`, `Observed Result`, `Timestamp`, `Conclusion`, and `Gap/Blocker`. Each row must classify the conclusion as `observed`, `inferred-from-config`, `permission-blocked`, `contradictory`, or `unknown` so reviewers can distinguish direct evidence from weaker evidence.

2. **Semantic-release baseline is verified without inventing history.** Record local and remote release tag state with exact read-only commands, including `git tag --list "v*" --sort=-v:refname`, `git ls-remote --tags origin "v*"`, and optional inspection-only commands such as `git show-ref --tags`. Explicitly state whether `v0.0.0` exists locally and remotely, whether the latest remote release tag is present locally, and whether `.releaserc.json` `tagFormat` matches the observed tags. Do not create, delete, sign, retag, fetch with forced tag updates, push, force-push, or otherwise mutate tags as part of this story unless Jerome explicitly approves that release-history change.

3. **Release workflow contract is documented.** Inspect `.github/workflows/release.yml`, `.releaserc.json`, `package.json`, `CHANGELOG.md`, and `docs/ci.md`. Record the release trigger, branch, permissions, checkout depth, test scope, package publishing path, GitHub Release behavior, changelog update behavior, and required secrets. The evidence must distinguish workflow configuration from proof that the latest workflow run succeeded.

4. **Branch protection evidence is captured or blocked honestly.** Use the GitHub UI, GitHub CLI, or GitHub API to record the current `main` branch protection / ruleset state. Evidence must include required status checks, whether direct pushes are restricted, whether reviews are required, whether linear history or signed commits are required, and whether admins are included. Query classic branch protection and repository rulesets separately when API access is available, because a missing classic protection object does not prove rulesets are absent. If the automation lacks permission to view repository rules, record `permission-blocker` with the exact command/page/API endpoint attempted, timestamp, safe actor identity or permission context, non-sensitive returned error, and blocked acceptance criterion; do not mark branch protection verified by inference from `docs/ci.md`.

5. **Required checks are reconciled against workflow jobs.** Compare the protected required checks, if available, with `.github/workflows/ci.yml`, `.github/workflows/docs-validation.yml`, and `.github/workflows/release.yml`. Include two tables: one for documented-versus-observed branch protection/ruleset required checks, and one for release workflow executed checks. At minimum, classify `commitlint`, `secret-scan`, `build-and-test`, `aspire-tests`, documentation validation, and any `Hexalith.EventStore.Server.Tests` release path as `required`, `recommended`, `optional`, `executed-not-required`, `permission-blocked`, or `unknown`. Record exact GitHub check context names when available and route stale, missing, renamed, or workflow-only checks to `Release Governance Gaps`. The story should preserve the current policy that Tier 3 Aspire tests are non-blocking unless repository rules say otherwise.

6. **Release-secret presence is verified without exposing secrets.** Verify whether `NUGET_API_KEY` is configured for the release workflow and whether `GITHUB_TOKEN` permissions are sufficient for tags/releases. Acceptable evidence is presence/visibility metadata from GitHub Actions secrets, an admin screenshot summary without values, a failed/successful workflow log that proves availability without printing it, or a permission-blocker note. Never print, copy, hash, decode, test, partially reveal, or infer secret values, masked values, token fragments, lengths, prefixes, suffixes, or screenshots containing unrelated secret names. Do not validate secrets by attempting a publish.

7. **Most recent release evidence is linked.** Record latest local tag, latest remote tag, latest GitHub Release, latest release workflow run on `main`, and visible NuGet/package outcome when accessible. Include tag, commit SHA, release URL or workflow run URL, conclusion, package/publish outcome if visible, and any skipped or failed steps. Compare the tag SHA, release target commit, workflow head SHA, changelog version, and package version when those sources are available. If these sources disagree, route the mismatch to `Release Governance Gaps` instead of choosing one silently. If GitHub access is unavailable, record local evidence from `CHANGELOG.md`, `git show --no-patch --decorate <latest-tag>`, and remote tag lookup, then classify the GitHub-side release evidence as blocked.

8. **Docs reflect the audited governance state.** Update only `docs/ci.md` or a focused release-governance docs section only if the evidence shows the current documentation is stale, incomplete, or missing the governance caveat. Preserve the difference between documented intended policy and observed repository settings. If no docs edit is needed, record the reason in the evidence artifact. Do not overstate controls that were not verified, and do not use docs edits to change release policy or workflow behavior.

9. **No release side effects occur by default.** Do not run `npx semantic-release`, publish NuGet packages, modify `CHANGELOG.md` through semantic-release, trigger deploy workflows, change branch protection, rotate secrets, or force-push tags during this story. If a dry run is useful, use a no-publish dry-run command and record that it is not release evidence.

10. **Governance gaps are routed, not hidden.** Add a mandatory "Release Governance Gaps" section to the evidence file. It must contain either `No gaps found based on available evidence.` or a dated list of unknown, contradictory, blocked, or stale evidence with owner, impact, required follow-up, and recommended story/status routing. Treat branch-protection/ruleset enforcement, release-secret presence, latest-release source disagreement, and required-check authority as high-impact governance items. Do not mark R9-A5 complete while a high-impact governance item remains `unknown` without either an accepted non-action decision or a follow-up row.

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
- Do not treat a GitHub API `404`, empty ruleset response, or missing workflow run as success until the evidence artifact explains whether it means absent control, unavailable permission, filtered visibility, or unsupported endpoint.

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

- [x] Task 0: Baseline the release-governance scope (AC: #1, #2, #9)
    - [x] 0.1 Record baseline HEAD and confirm this story is still `ready-for-dev`.
    - [x] 0.2 Create the dated evidence artifact under `_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/`.
    - [x] 0.3 Record the exact "no release side effects" guardrail before running evidence commands.

- [x] Task 1: Verify semantic-release and tag state (AC: #2, #3, #7)
    - [x] 1.1 Run `git tag --list "v*"` and record local tag range, including whether `v0.0.0` exists.
    - [x] 1.2 Run `git ls-remote --tags origin "v*"` and record remote tag range, including whether `v0.0.0` exists.
    - [x] 1.3 Inspect `.releaserc.json`, `package.json`, `CHANGELOG.md`, and `.github/workflows/release.yml`.
    - [x] 1.4 Link the latest release tag to its commit and GitHub Release/workflow run when accessible.

- [x] Task 2: Capture branch protection and required checks (AC: #4, #5)
    - [x] 2.1 Query `main` branch protection or repository rulesets with GitHub CLI/API, or record manual UI evidence.
    - [x] 2.2 Compare required checks to `ci.yml` job names and `docs-validation.yml`.
    - [x] 2.3 Record whether `aspire-tests` is non-blocking by policy, required by rules, or unknown.
    - [x] 2.4 If permissions block the check, record the exact command/API response and classify the unknowns.
    - [x] 2.5 Record any discrepancy between classic branch protection, rulesets, `docs/ci.md`, and observed workflow check names as a routed governance gap.

- [x] Task 3: Verify release-secret and permission evidence safely (AC: #6, #7)
    - [x] 3.1 Verify `NUGET_API_KEY` presence by metadata, workflow evidence, or admin inspection without exposing values.
    - [x] 3.2 Verify `GITHUB_TOKEN` release/tag permissions from workflow `permissions:` and observed release behavior.
    - [x] 3.3 Record any environment, ruleset, or secret visibility blockers separately from configuration defects.
    - [x] 3.4 Confirm the evidence artifact contains no masked values, hashes, token fragments, screenshots with unrelated secret names, or inferred secret lengths.

- [x] Task 4: Update docs and route gaps (AC: #8, #10)
    - [x] 4.1 Update `docs/ci.md` only if observed governance differs from its Branch Protection, Secrets, or Workflow sections.
    - [x] 4.2 Add a "Release Governance Gaps" section to the evidence artifact when any item remains unknown or deficient.
    - [x] 4.3 Route high-impact unknowns to a follow-up story/status row or record a dated accepted non-action decision with owner and revisit trigger.
    - [x] 4.4 If no follow-up row is created for a high-impact gap, record who accepted the residual risk and the concrete revisit trigger.

- [x] Task 5: Validate and close bookkeeping (AC: #11, #12)
    - [x] 5.1 Run targeted markdown lint/link validation for changed docs and evidence files, or record why unavailable.
    - [x] 5.2 If workflow YAML changes, run a YAML parse/lint check.
    - [x] 5.3 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row.

### Review Findings

Code review run: 2026-05-04. Sources: Blind Hunter (diff-only), Edge Case Hunter (diff + project read), Acceptance Auditor (diff + spec).

- [x] [Review][Decision][Resolved → Patch] Commit `9c62333` bundles R9-A1 closure with R9-A5 submission — commit modified 7 files (`deferred-work.md`, `post-epic-9-r9a1-http-stale-etag-proof.md`, `tests/Hexalith.EventStore.Server.Tests/.../HttpStaleETagProofE2ETests.cs`, plus the 4 R9-A5 files). Resolution: File List was updated to acknowledge the bundled R9-A1 files (already-reviewed-and-done) without rewriting branch history. Branch hygiene preference: future submissions should split close-of-prior-story from new-story-submission into separate commits.
- [x] [Review][Patch][HIGH] AC #12 violation — `sprint-status.yaml` `last_updated` field comment names R9-A2's outcome instead of R9-A5's release-governance evidence result [`_bmad-output/implementation-artifacts/sprint-status.yaml:31`]. Spec: "`last_updated` names the release-governance evidence result." Dev acknowledged the clobber on line 32 ("Original R9-A1 dev-handoff attribution clobbered by R9-A5 update on line 31; preserved here per AC #12") but the `last_updated` field itself still does not name R9-A5.
- [x] [Review][Patch][MED] AC #8 violation — `docs/ci.md:32` ASCII Job Topology caption "(parallel; both gate the PR)" contradicts the new Branch Protection section at lines 99-102 saying no required status checks were observed [`docs/ci.md:32` and `docs/ci.md:99-102`]. Either soften the caption to reflect intent-vs-enforcement, or annotate it explicitly.
- [x] [Review][Patch][MED] AC #4 violation — NUGET_API_KEY permission-blocker rows missing required schema fields [`_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/evidence-2026-05-04.md:31, 142-148`]. AC #4 demands "exact command/page/API endpoint attempted, timestamp, safe actor identity or permission context, non-sensitive returned error, and blocked acceptance criterion." The rows record endpoints and a single shared timestamp but omit non-sensitive error message bodies and the blocked AC# citation.
- [x] [Review][Patch][MED] AC #10 — High-impact branch-protection gap row presents two action options ("Create a follow-up governance story/status row if enforcement is desired; otherwise record an accepted non-action decision") without doing either [`evidence-2026-05-04.md:552`]. Story is moving to `review` while neither path is taken. Either add a routed follow-up row in `sprint-status.yaml` or record a dated accepted non-action decision with owner and revisit trigger.
- [x] [Review][Patch][LOW] AC #7 — Workflow head SHA `f9c2021…` vs tag SHA `3f360cc…` divergence is observed and explained inline at `evidence-2026-05-04.md:182-186` and `:543` but never added as a row in `Release Governance Gaps` (lines 552-554). Spec: "If these sources disagree, route the mismatch to `Release Governance Gaps` instead of choosing one silently." Add a row even if classified as expected.
- [x] [Review][Patch][LOW] Trailing whitespace on `5-5-e2e-security-testing-with-keycloak: done ` line introduced by this diff [`_bmad-output/implementation-artifacts/sprint-status.yaml:81`]. Unrelated incidental edit; remove trailing space.
- [x] [Review][Patch][LOW] Evidence file's bookkeeping summary written in future tense ("Story status and sprint status will be updated to `review`…") even though both are now `review` in this same diff [`evidence-2026-05-04.md:211-214`]. Update tense to confirm closed state.
- [x] [Review][Patch][LOW] Scope-boundary disambiguation — classic branch-protection 404 row concludes "observed absence of classic branch protection" without explicitly stating that the actor has sufficient permission to read this endpoint (i.e., 404 means absent control, not permission-blocked or filtered) [`evidence-2026-05-04.md:28, 95-97`]. Same disambiguation is correctly applied to NUGET_API_KEY 404/403 rows.
- [x] [Review][Patch][LOW] AC #8 — `docs/ci.md:9` Workflows summary row lists "Tier 1 + Tier 2 + Tier 3 tests" without noting Tier 3 (`aspire-tests`) is `continue-on-error: true`. Spec preserves "Tier 3 Aspire tests are non-blocking" — note this in the Workflows row.
- [x] [Review][Defer] Required Evidence Artifact Shape section #3 "Local and remote tag evidence" is folded into section #2 "Semantic-Release Baseline" rather than its own heading [`evidence-2026-05-04.md`] — content present, structural deviation only; deferred.
- [x] [Review][Defer] AC #1 "Observed Result" — `git ls-remote` v0.0.0 result is summarized as "remote `v0.0.0` was not present" rather than quoted as a raw output excerpt [`evidence-2026-05-04.md:384`] — deferred, conclusion is correct and reproducible.
- [x] [Review][Defer] AC #11 — Validation record didn't formally check job-name fidelity between docs/ci.md edits and workflow YAML [`evidence-2026-05-04.md:184`] — fidelity is correct in practice; deferred.

Dismissed (6) — not actionable: `gho_` regex in secret-hygiene scan (scan filter, not leaked prefix); R10-A9 / R9-A8 / R4-A8 status flips (each closed by their own commits on this branch); ruleset "cannot be bypassed" wording (accurate property description); pre-dev party-mode/elicitation final recommendations (planning artifacts, not closure).

## Dev Notes

### Architecture Guardrails

- Treat release governance as a control-plane evidence concern. The product runtime, query pipeline, and EventStore APIs should remain untouched.
- `main` is the release branch according to `.releaserc.json` and `.github/workflows/release.yml`; do not route evidence against another branch unless repository settings prove a ruleset applies differently.
- `semantic-release` owns version calculation, tags, GitHub Releases, changelog updates, and NuGet package publishing. Do not duplicate its behavior with manual tag or changelog edits.
- Branch protection and required checks are external GitHub settings. Repository docs can describe intended policy, but only GitHub settings or admin evidence can prove enforcement.
- Secret evidence must be presence/outcome metadata only. Never print secret values or masked values into BMAD artifacts.
- Record blockers as blockers. A permission error is useful evidence when it names exactly what could not be verified.
- Use this evidence authority order when sources conflict: GitHub branch protection/ruleset settings, successful or failed workflow run evidence, workflow YAML and release config, repository docs, then retrospective notes. Lower-authority sources may explain intent but must not override higher-authority observations.
- Do not collapse governance gaps into one generic blocker. Separate enforcement gaps, release-history mismatches, secret visibility blockers, required-check drift, and documentation staleness so a follow-up owner can resolve each one independently.

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
When a source is unavailable, the evidence entry should name the unavailable source, the attempted access path, the non-sensitive result, and the fallback source used, if any.

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

Codex dev-story agent (GPT-5).

### Implementation Plan

1. Keep the story scoped to read-only governance evidence and documentation;
   do not mutate release tags, releases, secrets, branch protection, rulesets,
   deployment state, or workflow execution.
2. Capture local git, GitHub API/CLI, workflow YAML, release config, and NuGet
   package metadata evidence into a dated artifact with explicit evidence
   classifications.
3. Update `docs/ci.md` only for observed documentation drift, preserving the
   distinction between intended policy and verified GitHub enforcement.
4. Run targeted markdown/link validation and record any unavailable checks or
   non-applicable validation.

### Debug Log References

- Resolved BMad workflow customization with
  `python3 _bmad/scripts/resolve_customization.py --skill .agents/skills/bmad-dev-story --key workflow`.
- Checked Aspire context with Aspire MCP `list_apphosts` and `doctor`; no
  AppHost was running, environment had no failed Aspire checks.
- Captured baseline with `git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`,
  `git status --porcelain=v1`, and authenticated GitHub CLI user lookup.
- Captured release/tag/config evidence with `git tag --list "v*" --sort=-v:refname`,
  `git ls-remote --tags origin "v*"`, `.releaserc.json`, `package.json`,
  `CHANGELOG.md`, and release workflow inspection.
- Captured GitHub governance evidence with branch protection API, ruleset API,
  `gh ruleset view`, latest release/run APIs, release job API, and filtered
  secret metadata attempts.
- Captured NuGet package outcome through read-only NuGet flat-container metadata.
- Ran markdownlint, markdown-link-check, and refined secret-hygiene scans.

### Completion Notes List

- Created `_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/evidence-2026-05-04.md`
  with metadata, evidence matrix, semantic-release baseline, tag state, workflow
  contract, branch protection/ruleset evidence, required-check reconciliation,
  secret evidence, latest release evidence, governance gaps, validation record,
  and story bookkeeping summary.
- Verified local and remote latest release tag `v3.9.1`; local `v0.0.0` exists,
  remote `v0.0.0` was not returned by remote tag lookup.
- Verified latest GitHub Release `v3.9.1`, successful release workflow run
  `25314142173`, GitHub Release package assets, and representative NuGet
  package versions `3.9.1`.
- Found governance drift: classic `main` branch protection is absent and active
  ruleset `Protect` only blocks deletion and non-fast-forward updates. Updated
  `docs/ci.md` to separate observed settings from intended/recommended policy.
- Recorded `NUGET_API_KEY` exact metadata as permission-blocked/partially
  unavailable while documenting runtime availability through the successful
  release and NuGet package publication evidence.
- No release side effects were performed.

### File List

R9-A5 in-scope files:

- `_bmad-output/implementation-artifacts/post-epic-9-r9a5-release-governance-evidence.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/post-epic-9-r9a5-release-governance-evidence/evidence-2026-05-04.md`
- `docs/ci.md`

Bundled in commit `9c62333` ("close r9a1 review and submit r9a5 evidence") but belonging to R9-A1's already-reviewed-and-done closure (not R9-A5 scope; recorded here for File List honesty per code-review finding D1):

- `_bmad-output/implementation-artifacts/deferred-work.md` (R9-A1 defers)
- `_bmad-output/implementation-artifacts/post-epic-9-r9a1-http-stale-etag-proof.md` (R9-A1 status update)
- `tests/Hexalith.EventStore.Server.Tests/.../HttpStaleETagProofE2ETests.cs` (R9-A1 review-driven patches)

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-03 | 0.1 | Created ready-for-dev R9-A5 release governance evidence story. | Codex automation |
| 2026-05-03 | 0.2 | Party-mode review applied evidence-format, read-only, permission-blocker, secret-handling, and governance-gap hardening. | Codex automation |
| 2026-05-04 | 0.3 | Advanced elicitation tightened evidence authority, API ambiguity, mismatch routing, secret hygiene, and high-impact gap handling. | Codex automation |
| 2026-05-04 | 1.0 | Implemented release-governance evidence artifact, docs correction, validation record, and review handoff. | Codex dev-story agent |
| 2026-05-04 | 1.1 | Code review applied 9 patches (1 HIGH AC #12 sprint-status `last_updated` attribution; MED AC #8 docs/ci.md caption + Workflows-row Tier 3 note; MED AC #4 NUGET_API_KEY blocker schema; MED AC #10 accepted-non-action decision for branch-protection gap; LOW AC #7 SHA-divergence routed; LOW trailing whitespace; LOW future-tense bookkeeping; LOW 404 disambiguation; File List honesty for R9-A1 bundled files); 1 decision resolved (File List update); 3 defers recorded; 6 dismissed. | Claude Opus 4.7 code-review agent |

## Verification Status

Code review complete (Claude Opus 4.7, 2026-05-04). 9 patches applied + 1
decision resolved + 3 defers + 6 dismissed. Targeted markdown validation passed
with 0 errors, docs links passed, evidence link check reported no Markdown
hyperlinks, refined secret hygiene scan found no token-style fragments or
masked values, and workflow YAML lint was not required because no workflow
YAML changed.

## Party-Mode Review

- Date/time: 2026-05-03T13:32:13Z
- Selected story key: post-epic-9-r9a5-release-governance-evidence
- Command/skill invocation used: `/bmad-party-mode post-epic-9-r9a5-release-governance-evidence; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: All reviewers recommended `needs-story-update`. The shared risk was false-positive governance closure from prose evidence, ambiguous GitHub permission blockers, unclear required-check authority, insufficient release-source reconciliation, unsafe secret/tag evidence handling, open-ended docs scope, and underspecified validation records.
- Changes applied: Added a required evidence matrix and artifact outline; narrowed tag/release collection to read-only evidence unless Jerome approves side effects; defined permission-blocker content; split required checks into branch-protection/ruleset and release-workflow evidence; hardened secret handling to metadata-only proof; required latest local tag, remote tag, GitHub Release, workflow, and package observations; made Release Governance Gaps mandatory even when empty; narrowed docs update scope; required exact validation command/result recording.
- Findings deferred: Whether `aspire-tests` should become branch-protection blocking; whether `Hexalith.EventStore.Server.Tests` should remain in release-required validation despite known CA2007 build failures; whether GitHub branch protection should migrate to rulesets; whether release secrets should be rotated or renamed; whether semantic-release should continue targeting only `main` with `v${version}` tags.
- Final recommendation: needs-story-update

## Advanced Elicitation

- Date/time: 2026-05-04T10:30:42Z
- Selected story key: post-epic-9-r9a5-release-governance-evidence
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-9-r9a5-release-governance-evidence`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Comparative Analysis Matrix
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction
- Findings summary: The story already had strong guardrails, but elicitation found remaining false-closure paths around API ambiguity, check-name drift, release-source mismatch, secret-evidence hygiene, and treating docs as enforcement proof.
- Changes applied: Added explicit evidence classification values; required separate classic branch-protection and ruleset handling; tightened GitHub API ambiguity handling; required exact check context names and mismatch routing; added latest-release SHA/version comparisons; made high-impact governance categories explicit; added secret-artifact hygiene confirmation; added an evidence authority hierarchy and fallback-source recording.
- Findings deferred: Whether the project should make `aspire-tests` branch-protection blocking; whether release-required `Hexalith.EventStore.Server.Tests` should be changed while its CA2007 failure history exists; whether branch protection should be migrated to rulesets; whether release secrets need rotation or environment scoping; whether future release governance evidence should be collected by a reusable script.
- Final recommendation: ready-for-dev
