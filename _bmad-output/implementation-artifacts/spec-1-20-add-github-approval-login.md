---
title: 'Story 1.20 GitHub approval role authorization'
type: 'feature'
created: '2026-07-17'
status: 'done'
baseline_commit: 'a9718a2106b31ff9c005203e5381c28d06db0e21'
review_loop_iteration: 2
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.20's committed GitHub approval-role allowlist currently has empty arrays for every recognized role. The exact-SHA gate therefore fails closed even when a legitimate repository owner submits a durable approval record.

**Approach:** Add the authenticated repository user's GitHub login, `jpiquot`, once to each of the four existing role arrays. Preserve the allowlist schema, repository identity, role-key set, and all fail-closed validation behavior.

## Boundaries & Constraints

**Always:** Keep the JSON syntactically valid; retain schema `hexalith.eventstore.github-approval-role-allowlist/v1`; retain repository `Hexalith/Hexalith.EventStore`; keep exactly the roles `eventstore_owner`, `release_owner`, `architecture_owner`, and `story_1_16_reviewer`; place `jpiquot` exactly once in every role array; leave existing Story 1.20 packet, story, sprint-status, and submodule changes untouched.

**Ask First:** Any role-key addition or removal; authorization of a different login; changes to proof-packet gate logic; staging, committing, or running the full exact-SHA product gate.

**Never:** Infer additional authorized users; weaken non-empty, uniqueness, login-format, repository, or role-key validation; mark Story 1.20 available or authorize consumer migration; rewrite or revert unrelated working-tree changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Authorized login | Authenticated GitHub login is `jpiquot`; all four role arrays are empty | Every role array becomes `["jpiquot"]` | Stop if the authenticated login differs |
| Gate schema validation | Updated allowlist is evaluated by the packet's `jq` predicate | Schema, repository, exact key set, non-empty arrays, uniqueness, and login format all pass | Preserve fail-closed behavior on any mismatch |
| Existing worktree changes | Story packet and submodule changes already exist | Only the allowlist receives the authorization edit | Do not stage, revert, or rewrite unrelated changes |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` -- source of authorized GitHub logins for Story 1.20 approval roles.
- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- embeds the fail-closed validator and binds fetched GitHub approval records to the allowlist.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` -- add `jpiquot` once to all four existing arrays so GitHub records authored by this login can satisfy each role check.

**Acceptance Criteria:**
- Given the four recognized approval roles, when the allowlist is read, then each role contains exactly the single login `jpiquot`.
- Given the updated file, when the packet's schema and role predicate runs, then it exits successfully without weakening any check.
- Given the pre-existing dirty working tree, when the change is complete, then no Story 1.20 packet field, sprint status, or submodule state has been altered by this task.

### Review Findings

_Code review 2026-07-17 of committed range `a9718a21..ea6ce49b` (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor). AC1 (exact membership), AC2 (packet predicate), and the identity check were replayed and pass; the findings below are what remains._

- [ ] [Review][Decision] Committed range falsifies the spec's isolation contract — commit `ea6ce49b` bundled the allowlist edit with 7 `references/` submodule bumps and this spec file, and `ba203bde` (Directory.Packages.props) also postdates `baseline_commit` `a9718a21`; Verification commands 4 and 5 therefore fail permanently at HEAD (replayed: 10 changed tracked paths, empty untracked set), command 3 has no exit-code predicate (vacuously passes), commands 4–5 are cwd-dependent, and AC3 ("no submodule state altered") is false in the committed record. The "Ask First: staging, committing" boundary has no recorded approval (consistent with the concurrent auto-commit loop absorbing worktree state; all submodule moves are fast-forward and the commit is owner-authored). Owner must either ratify the committed state and restate the Verification section post-commit (range-scoped, root-pinned, exit-code-asserted), or treat as a breach requiring remediation.
- [ ] [Review][Decision] Exact-membership (anti-over-authorization) guard exists only in this spec's manual Verification command 2 — the operative packet validator (`1-20-owner-approved-parity-closure-proof-packet.md` allowlist predicate and per-record `index($login)` membership check) accepts any non-empty, unique, valid-format login arrays, and no CI, test, or script consumes the allowlist; a silently appended valid-format login would be honored by the gate. Hardening the packet is "Ask First" (gate logic), so the owner must choose: pin exact four-role membership in the packet validator, add a CI assertion, or accept as-is.
- [ ] [Review][Patch] Local `PackageVersion Update` silently pins `Microsoft.Extensions.TimeProvider.Testing` 10.7.0 below the Builds-central 10.8.0 (`references/Hexalith.Builds/Props/Directory.Packages.props:212`), violating the Builds-owns-global-versions invariant with no check observing the divergence [`Directory.Packages.props:31`]
- [x] [Review][Defer] Commit `ba203bde` is unbuildable in isolation — `Update` with no central definition until `ea6ce49b` bumps Builds to `cfafcbf1` (NU1010 for the three consuming test projects; bisect/rollback hazard) — deferred, history already on `main`
- [x] [Review][Defer] Login-format regex `^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$` admits trailing/consecutive hyphens GitHub forbids [`1-20-owner-approved-parity-closure-proof-packet.md` allowlist predicate] — deferred, pre-existing packet behavior masked today by exact-membership equality
- [x] [Review][Defer] Redundant `PackageVersion Update="Microsoft.Playwright"` duplicates the identical central 1.61.0 and will silently freeze the version on the next central bump [`Directory.Packages.props:32`] — deferred, pre-existing line not introduced by this range

## Spec Change Log

- 2026-07-17, review loop 1: Review found that the documented generic allowlist predicate accepted additional valid-format logins and that the scoped diff command did not verify the isolation acceptance criterion. Verification now requires the exact four-role object with `jpiquot` as the sole member of every array and explicitly checks the packet, story, sprint tracker, and root submodule paths for changes. This avoids silently accepting over-authorization or unrelated mutations. KEEP: preserve the approved `jpiquot` assignment to all four roles, the existing schema/repository identities, and every fail-closed packet predicate.
- 2026-07-17, review loop 2: Review found that the identity command could succeed for a different authenticated login or GitHub host, the JSON command could accept a stream containing more than one root value, and worktree-to-index isolation could miss staged, committed, untracked, or omitted-submodule changes. Verification now pins `github.com`, requires the result to equal `jpiquot`, requires exactly one JSON root, compares every tracked path and submodule against the recorded baseline, and checks the complete untracked-file set. This avoids validating the wrong identity, malformed multi-document input, or hidden unrelated mutations. KEEP: retain loop 1's exact four-role membership assertion and preserve the approved allowlist-only behavior.

## Verification

**Commands:**
- `test "$(gh api --hostname github.com user --jq .login)" = jpiquot` -- expected: exits zero only for the approved authenticated GitHub login.
- `jq -e -s 'length == 1 and (.[0] | .schema == "hexalith.eventstore.github-approval-role-allowlist/v1" and .repository == "Hexalith/Hexalith.EventStore" and .roles == {"eventstore_owner":["jpiquot"],"release_owner":["jpiquot"],"architecture_owner":["jpiquot"],"story_1_16_reviewer":["jpiquot"]} and all(.roles[]; type == "array" and length > 0 and length == (unique | length) and all(.[]; type == "string" and test("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$"))))' _bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` -- expected: exits zero.
- `git diff -- _bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` -- expected: only the four role-array values change from empty to `jpiquot`.
- `test "$(git -c diff.ignoreSubmodules=none diff --name-only a9718a2106b31ff9c005203e5381c28d06db0e21 -- .)" = _bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` -- expected: exits zero, proving that the allowlist is the only tracked path changed since the recorded baseline.
- `test "$(git ls-files --others --exclude-standard)" = _bmad-output/implementation-artifacts/spec-1-20-add-github-approval-login.md` -- expected: exits zero, proving that the workflow spec is the only untracked file.

## Suggested Review Order

**Approved intent**

- Human-owned scope fixes one authenticated login across all four existing roles.
  [`spec-1-20-add-github-approval-login.md:16`](spec-1-20-add-github-approval-login.md#L16)

**Authorization mapping**

- Existing fail-closed roles gain `jpiquot` without changing schema or repository identity.
  [`1-20-github-approval-role-allowlist.json:5`](1-20-github-approval-role-allowlist.json#L5)

**Verification**

- Exact identity, single-document membership, and whole-tree isolation checks prevent drift.
  [`spec-1-20-add-github-approval-login.md:61`](spec-1-20-add-github-approval-login.md#L61)
