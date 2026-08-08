---
title: 'Update all .NET SDK references to 10.0.302'
type: 'chore'
created: '2026-07-16'
status: 'done'
review_loop_iteration: 2
baseline_commit: 'f670892f0826de2097e9f47175f5caf5c5ad346a'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Root EventStore still has predecessor SDK patch tokens in BMAD docs, planning archives, and story-1-20 evidence after a partial doc revert, even though live pins already use the requested baseline.

**Approach:** Clear every in-scope root leftover with literal token rewrite (including embedded evidence strings), using minimal contrast-preserving edits where a blind replace would collapse seed/below-min meaning. Submodule leftovers are deferred.

## Boundaries & Constraints

**Always:** In the root EventStore worktree only, replace every tracked predecessor patch token with the current baseline, including historical and embedded evidence strings. When a literal replace would collapse a required contrast (seed-vs-baseline), rewrite only enough text to keep the contrast true—prefer a non-predecessor older patch such as `10.0.299`. Preserve encoding and line endings; keep coupled assertions synchronized.

**Ask First:** Halt on unsafe substitutions, unrelated dirty files, nested-submodule work, or any need to edit a root-declared submodule in this pass.

**Never:** Do not edit FrontComposer, Memories, Tenants, Builds, AI.Tools, Commons, or PolymorphicSerializations in this pass. Do not change target frameworks, ASP.NET/package pins, roll-forward policy, `.git` metadata, ignored outputs, submodule pointers, commits, or pushes. Do not touch Builds NuGet package-audit evidence (deferred/exempt).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Ordinary leftover | Root tracked text names a predecessor SDK patch | Token becomes the current baseline; other prose unchanged | Stop if unsafe |
| Embedded evidence | `environment.txt` workload string embeds a predecessor patch | Replace only that token inside the string | No new manifest IDs beyond the token swap |
| Seed contrast | Architecture text contrasts former seed vs required baseline | Former seed becomes `10.0.299`; baseline stays current | Do not leave predecessor patches |
| Out-of-scope submodule leftover | FrontComposer/Memories/Builds still match | Leave unchanged in this pass | Covered by deferred-work entries |

</frozen-after-approval>

## Code Map

- `global.json` -- already current baseline; read-only.
- Root pin/claim leftovers -- `_bmad-output/implementation-artifacts/{1-2,1-12,1-13,1-14,1-15,1-20,2-8,4-1,4-2}-*.md`, `deferred-work.md`, `investigations/hexalith-eventstore-client-3-33-5-nuget-investigation.md`, `spec-1-16-1-20-sprint-change-proposal.md` -- restate current pin.
- Root planning/archive -- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-02.md`, `…06-22-ci-release-retier.md`, `…07-16-story-1-16-review-and-story-1-20-proof-closure.md`, `…07-18.md`, archive `ARCHITECTURE-SPINE.md` -- historical rewrite.
- Seed contrast -- `architecture-eventstore-2026-07-05/.memlog.md:14,:48` and `reviews/review-update-2026-07-15-technology-currentness.md:28,:85` -- 2B with `10.0.299` former seed.
- Evidence -- `docs/operations/projection-delivery-v2-evidence.md:7` and `evidence/story-1-20/*/environment.txt:6` (×4) -- 3A-strict token rewrite.
- Exclude from AC grep -- this spec file (documents the predecessor tokens by name).

## Tasks & Acceptance

**Execution:**
- [x] Root BMAD/docs/planning leftovers -- replace every in-scope predecessor SDK patch token with the current baseline.
- [x] Root seed-contrast rows -- apply minimal 2B rewrite (`10.0.299` former seed) where blind replace would tautologize.
- [x] Root evidence strings -- 3A-strict embedded token rewrite in story-1-20 `environment.txt` files and ops evidence doc.
- [x] Root rescan -- confirm zero in-scope leftovers outside this spec file; no submodule diffs.

**Acceptance Criteria:**
- Given the root leftover inventory above, when cleanup completes, then `git grep -I -E '10\.0\.30(0|1)' -- ':!_bmad-output/implementation-artifacts/spec-update-dotnet-sdk-to-10-0-302.md' ':!_bmad-output/implementation-artifacts/review-diff-update-dotnet-sdk-to-10-0-302.md'` returns no matches in the root repo.
- Given architecture seed-vs-baseline rows, when rewritten, then former seed is a non-predecessor (`10.0.299`) and required baseline remains current.
- Given FrontComposer/Memories/Builds leftovers, when this pass finishes, then those worktrees are unchanged and remain tracked in deferred-work.

## Spec Change Log

- 2026-08-08 review loop 2 intent renegotiation: human chose **1A** / **2B** / **3A-strict**.
- 2026-08-08 scope split `[S]`: main goal narrowed to **root EventStore leftovers only**. Deferred FrontComposer review-diff scrub and Memories BMAD scrub to `deferred-work.md`. KEEP: live pins already current; do not edit Builds audit JSON; submodule cleanups stay deferred.

## Design Notes

- Below-min/seed example: former seed `10.0.301` → `10.0.299`; baseline stays `10.0.302`.
- Embedded evidence example: `10.0.300-manifests.…` → `10.0.302-manifests.…` (token only).

## Verification

**Commands:**
- `git grep -n -I -E '10\.0\.30(0|1)' -- ':!_bmad-output/implementation-artifacts/spec-update-dotnet-sdk-to-10-0-302.md' ':!_bmad-output/implementation-artifacts/review-diff-update-dotnet-sdk-to-10-0-302.md'` -- expected: no output.
- `git status --porcelain` and `git diff --check` -- expected: only root in-scope text files; no submodule pointer changes; no new whitespace errors.
- `git -C references/Hexalith.FrontComposer status --porcelain` and same for Memories/Builds/Tenants -- expected: clean / unchanged.

## Suggested Review Order

**Seed contrast (2B)**

- Former seed labeled explicitly so current pin is not falsified
  [`.memlog.md:14`](../planning-artifacts/architecture/architecture-eventstore-2026-07-05/.memlog.md#L14)

- AD-11 move keeps former seed vs required baseline distinct
  [`.memlog.md:48`](../planning-artifacts/architecture/architecture-eventstore-2026-07-05/.memlog.md#L48)

- Review evidence uses then-stated seed, not a live-pin lie
  [`review-update-2026-07-15-technology-currentness.md:28`](../planning-artifacts/architecture/architecture-eventstore-2026-07-05/reviews/review-update-2026-07-15-technology-currentness.md#L28)

**ASP.NET-only residual gap (post-patch)**

- Runtime baseline row no longer claims an SDK shortfall
  [`sprint-change-proposal-2026-07-16-story-1-16-review-and-story-1-20-proof-closure.md:82`](../planning-artifacts/sprint-change-proposal-2026-07-16-story-1-16-review-and-story-1-20-proof-closure.md#L82)

- Hard-blocker bullets match the ASP.NET-only remaining half
  [`sprint-change-proposal-2026-07-16-story-1-16-review-and-story-1-20-proof-closure.md:295`](../planning-artifacts/sprint-change-proposal-2026-07-16-story-1-16-review-and-story-1-20-proof-closure.md#L295)

**Embedded evidence (3A-strict)**

- Workload token rewritten without inventing a new manifest suffix
  [`environment.txt:6`](evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/environment.txt#L6)

- Ops evidence capture retargeted to the current SDK pin
  [`projection-delivery-v2-evidence.md:7`](../../docs/operations/projection-delivery-v2-evidence.md#L7)

**Ordinary pin restates**

- Representative story pin claim now names the current baseline
  [`1-2-domain-query-handler-routing.md:158`](1-2-domain-query-handler-routing.md#L158)

- SourceLink exception avoids a search-reports tautology
  [`sprint-change-proposal-2026-07-18.md:165`](../planning-artifacts/sprint-change-proposal-2026-07-18.md#L165)

**Deferred scope**

- FrontComposer/Memories scrub stays tracked, not silently dropped
  [`deferred-work.md:982`](deferred-work.md#L982)
