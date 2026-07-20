---
title: 'Simplify intentional release governance'
type: 'refactor'
created: '2026-07-20'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'b66d66c9a0c49d7666a8c3e8b5470e0b70c49e33'
context:
  - '{project-root}/docs/ci.md'
  - '{project-root}/references/Hexalith.Builds/.github/workflows/ci-cd-standards.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Ordinary `main` commits trigger publication and couple the Builds development gitlink, immutable release-tool SHA, and short-lived comment authority. Routine changes therefore cause release failures and pointer/authority rotation when nobody intends to release.

**Approach:** Release manually from an exact green `main` SHA through a reviewer-gated `production` environment. Replace comment authority with reusable source/destination evidence, decouple the release-tool pin from the Builds gitlink, and validate squash titles before merge.

## Boundaries & Constraints

**Always:** Keep existing CI triggers. Release only through `workflow_dispatch` on current `main` after successful exact-SHA push CI and `production` approval. Keep shared release workflow/publisher pinned to one reviewed Builds SHA. Retain secret checks, exact 14-package inventory, collision rejection, source/Builds/run evidence, two-platform OCI validation, smoke tests, and no-overwrite behavior. Implement reusable behavior in Hexalith.Builds first.

**Ask First:** Any real release dispatch, weakened gate, unavailable reviewer protection, credential copy/exposure, unrelated pointer/content change, or scope beyond both implementations and focused docs/specs.

**Never:** Publish as verification; release from non-`main` or unverified source; use mutable publication references; equate release pin with development gitlink; duplicate shared machinery locally; retain hidden comment authority; rewrite history, bypass checks, or disturb unrelated work.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Routine commit | PR/push CI | No Release run or release maintenance | CI is the only automatic gate |
| Approved release | Manual current-green-`main` dispatch and approval | Pinned publication runs once | Evidence binds source, run, Builds, packages, and container |
| Invalid dispatch | Wrong ref, stale head, or no exact-SHA green CI | No publication job | Fail before approval/secrets |
| Collision | Existing package/tag/image | No mutation | Fail at verify and pre-publish recheck |
| Builds bump | Gitlink differs from release pin | CI remains green | Pin changes only by deliberate review |
| Invalid title | Non-conventional squash title | Required check blocks merge | Title edit retriggers check |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds/.github/workflows/` -- environment-gated release job and pre-merge title validation.
- `references/Hexalith.Builds/Github/publish-containers/` -- comment-free source/destination evidence plus immutable helper verification.
- `.github/workflows/release.yml` -- manual exact-green-main dispatcher and immutable caller.
- `.releaserc.json` and `scripts/` -- destination/source preflight instead of comment validation.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/` and active CI docs -- local regression contract and operator flow.
- GitHub `production` environment and authority variable -- replacement approval gate and obsolete state.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds` -- add the environment input, comment-free source/destination preflight, publisher/tests/docs updates, and PR-title validation -- supplies one reusable safe primitive.
- [x] `.github/workflows/release.yml`, `.releaserc.json`, and `scripts/` -- adopt manual exact-green-main release and the merged immutable Builds SHA without authority inputs -- removes per-commit coordination.
- [x] Packaging tests and active CI docs -- replace automatic/comment/gitlink assertions with manual/environment/source/collision/pin-independence/title contracts -- prevents regression.
- [x] GitHub configuration -- create main-only `production` requiring `jpiquot`, confirm title edits use the required Commitlint context, and remove the obsolete variable after merge -- completes cutover.
- [x] Both repositories -- deliver validated Conventional Commit PRs in dependency order and verify CI -- consumes only merged shared code.

**Acceptance Criteria:**
- Given an ordinary commit, when CI completes, then no publication or release maintenance starts.
- Given invalid ref/head/CI or missing approval, when release is requested, then secrets and mutation remain unreachable.
- Given approved green `main` and absent destinations, when release runs, then package/container integrity and evidence remain enforced without comment authority.
- Given differing development and release pins, when Contracts run, then pin/input/helper identity passes independently.
- Given an invalid or edited squash title, when Commitlint runs, then merge stays blocked until valid.

## Spec Change Log

## Design Notes

Environment approval is the human authority; the helper owns machine-verifiable source/destination safety. The checked-in release SHA is the tool trust decision; the Builds gitlink may advance independently.

## Verification

**Commands:**
- Builds workflow/publisher/preflight/Commitlint suites -- expected: source, collision, and title cases pass.
- `actionlint`, EventStore Contracts Release build, and direct Packaging tests -- expected: zero diagnostics/failures.
- Exact PR head/title/base and checks in both repositories -- expected: Builds merges first; EventStore consumes its merge SHA.
- Environment/ruleset/variable readback -- expected: main-only reviewer-gated `production`, title edits covered, obsolete variable absent.
- Actions inspection -- expected: ordinary push creates CI but no Release run; no release is dispatched for testing.

**Results:**
- Hexalith.Builds PRs `#22`, `#23`, and adversarial hardening PR `#24` passed protected checks and merged as `909abb32bad6c15b278fe7c56fae205811cc148a`, `2c2fd14ac6418d5758662aac8cfbe7df68615e12`, and `cf04c419378dfe1bd3c41a9244b5e3283092056e`; EventStore pins the final SHA for reusable-workflow resolution and `builds-execution-sha`.
- Shared validation passed actionlint, 517 npm provenance signatures, the real Commitlint behavior matrix (5/5), publication/source/evidence/request tests (44/44), domain-workflow assertions, package catalogs/fixtures, Dapr contracts, and G-4 build/test/package qualification (64/64, zero warnings/errors).
- EventStore validation passed actionlint, shell/JSON/Python/diff checks, 492 npm provenance signatures, the real caller title matrix, a zero-warning Release build, affected release-governance tests (44/44), the complete Contracts assembly (748/748), and all protected GitHub checks.
- EventStore PRs `#314` and adversarial hardening `#315` merged without bypass as `61bafeabc9f209002ee2035046a1d48cdc55cabf` and `402519170541ff4708b67168af6eaadc256cddb9`; exact post-merge push CI run `29733352451` completed successfully, as did Advisory, Integration, Commitlint, and CodeQL.
- GitHub readback confirms both repositories are squash-only with `PR_TITLE`, protected rulesets have no bypass actors, and EventStore retains seven strict checks. Both `production` environments require reviewer `jpiquot`, permit only `main`, disable administrator bypass, and contain no duplicated secrets; `HEXALITH_RELEASE_AUTHORITY_URL` is absent.
- The exact Release API query for final merge SHA `402519170541ff4708b67168af6eaadc256cddb9` returned `total_count: 0`; no release or canary was dispatched. Three pre-existing publisher limitations were recorded in `deferred-work.md` without expanding this change.

## Suggested Review Order

**Intentional release boundary**

- Manual dispatch proves exact green `main` before entering the protected reusable job.
  [`release.yml:17`](../../.github/workflows/release.yml#L17)

- The immutable shared SHA stays independent from the development Builds gitlink.
  [`release.yml:72`](../../.github/workflows/release.yml#L72)

**Publication identity and mutation ordering**

- EventStore binds source, manifest, environment, and shared helper inputs fail-closed.
  [`validate-publication-preflight.sh:45`](../../scripts/validate-publication-preflight.sh#L45)

- Semantic-release rechecks identity before NuGet and then invokes the container publisher.
  [`.releaserc.json:10`](../../.releaserc.json#L10)

**Merge-history integrity**

- Repository policy excludes ambiguous `chore` commits from semantic version history.
  [`commitlint.config.mjs:1`](../../commitlint.config.mjs#L1)

- PR title edits retrigger the required shared Commitlint check.
  [`commitlint.yml:3`](../../.github/workflows/commitlint.yml#L3)

**Verification and operator contract**

- Governance tests prove pin independence, exact source gates, and mutation blocking.
  [`ContainerPublishingGovernanceTests.cs:123`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L123)

- Behavioral source fixtures reject wrong refs, stale heads, and missing exact CI.
  [`ContainerPublishingGovernanceTests.cs:176`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L176)

- Real policy contracts preserve pinned Commitlint tools and disallow `chore`.
  [`CommitMessagePolicyTests.cs:204`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs#L204)

- Operator documentation explains approval, repeated source proof, and immutable evidence.
  [`ci.md:93`](../../docs/ci.md#L93)

- Secret guidance keeps credentials repository-scoped behind non-bypassable approval.
  [`ci-secrets-checklist.md:39`](../../docs/ci-secrets-checklist.md#L39)

- Deferred shared-publisher limitations remain explicit and separately actionable.
  [`deferred-work.md:3`](deferred-work.md#L3)
