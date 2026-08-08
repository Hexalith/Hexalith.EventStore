---
title: 'Resolve main rebase conflicts'
type: 'bugfix'
created: '2026-07-21'
status: 'done'
baseline_commit: '2321205baad8724c0508a60f92fd0ecfbca6845d'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The July draft assumed local `main` held unpushed commits `f3e036bf` / `026b039b` that conflicted with `origin/main`. Those commits are orphans; `origin/main` already contains the stronger sibling `f6db558c` (same subject/parent), and `main` matches `origin/main` at `37fdcd1f`. Replaying the orphans would regress resilience, fixtures, Story 1.20 status, and submodule pins.

**Approach:** Close this draft as obsolete without rebase, cherry-pick, status downgrade, or gitlink replay. Record forensic orphan SHAs and leave unrelated feature-branch work untouched.

## Boundaries & Constraints

**Always:** Treat `origin/main` / `main` tip `37fdcd1f` as authoritative for this closure; keep Story 1.20 `status: done`; keep current Builds/Memories/Tenants gitlinks; leave `feat/story-4-5-append-durability-race-evidence` and its dirty BMAD files untouched; append one deferred-work forensic note for the orphan SHAs.

**Ask First:** Any request to salvage unique content from `f3e036bf`/`026b039b`, reopen Story 1.20 as `blocked`, or change submodule pins.

**Never:** Rebase or cherry-pick the orphans onto current tips; force-push; initialize/update nested submodules; stack this closure onto the story-4.5 branch commits; use wall-clock `CancelAfter` or dual resilience pipelines from the orphan lineage.

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceHttpClientBuilderFilter.cs:6-24` -- remote-winning filter; strips `ResilienceHandler` only for named client `domain-service-invocation` (KEEP; do not replace with orphan `RemoveAllResilienceHandlers`).
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:71-130` -- `TimeProvider`-backed linked CTS; caller cancel → `OperationCanceledException`; configured timeout → `DomainServiceException` + `ConfiguredTimeoutElapsed` (KEEP; orphan used `CancelAfter`).
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:63-68` -- registers filter + named client `Timeout = InfiniteTimeSpan`.
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs:169+` -- FakeTimeProvider timeout vs caller-cancel coverage already on tip.
- `tests/Hexalith.EventStore.IntegrationTests/Security/ProjectionDeliveryWriterProtocolTestLease.cs:174-215` -- lease-based Redis ownership restore (stronger than orphan inline snapshot restore).
- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` -- tip `status: done` (orphan had `blocked`; do not downgrade).
- Orphan objects (read-only forensic): `f3e036bf0cae72b50508a3e729f24a052a7c4e95`, `026b039b237372774d998af8f5b77c58db00d348`; winning sibling already on tip: `f6db558c`.
- Gitlinks on tip (KEEP): Builds `824d7ef1…`, Memories `da5df100…`, Tenants `323baf88…` — orphan `026` pins are older.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-resolve-main-rebase-conflicts.md` -- after approval, set `status: done` and leave frozen intent locked -- closes the obsolete conflict draft without code mutation.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- append one new entry naming orphan SHAs `f3e036bf` / `026b039b` and sibling `f6db558c` -- preserves forensic value if reflog GC drops tip reachability.
- [x] Working tree -- verify `git merge-base --is-ancestor f6db558c origin/main`, `main`/`origin/main` alignment, Story 1.20 `done`, and no conflict markers under `src`/`tests`/`_bmad-output` -- proves closure premises still hold at execution time.
- [x] Branch hygiene -- do not checkout, stash, commit, or push on `feat/story-4-5-append-durability-race-evidence` for this work -- avoids mixing unrelated dirty BMAD edits into closure.

**Acceptance Criteria:**
- Given the approved obsolete-closure intent, when implementation finishes, then this spec is `done`, no orphan commits are replayed onto `main`/`origin/main`, and Story 1.20 remains `done`.
- Given tip inspection, when gitlinks and resilience entry points are checked, then current filter/`TimeProvider` shape and Builds/Memories/Tenants pins are unchanged by this work.
- Given deferred-work append, when the ledger is read, then a new entry cites the orphan and winning-sibling SHAs without editing prior entries.

## Spec Change Log

- 2026-08-08 — Human chose close-as-obsolete (`[C]`). Replaced frozen intent: original rebase/push narrative → no-op closure after confirming orphans vs winning sibling `f6db558c` on `origin/main`. Avoids regressing tip resilience/fixtures/1.20/`gitlinks`. KEEP tip remote patterns; NEVER replay orphans.

## Design Notes

`f3e036bf` and `f6db558c` share parent `014bd00a` and the same subject; only `f6` is an ancestor of `origin/main`. Diffing tip → `f3` is a mass regression (~1357 files), not a missing upgrade. Current HEAD may sit on `feat/story-4-5-…` with unrelated dirty BMAD files — closure must not touch that branch.

## Verification

**Commands:**
- `git rev-parse main origin/main` -- expected: both `37fdcd1f…` (or equal tips if main advanced; still no orphan replay).
- `git merge-base --is-ancestor f6db558c origin/main && git cat-file -t f3e036bf && git cat-file -t 026b039b` -- expected: ancestor check succeeds; orphans still exist as objects (or report missing objects if GC already dropped them).
- `rg -m1 '^status:' _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` -- expected: `status: done`.
- `git ls-tree origin/main references/Hexalith.Builds references/Hexalith.Memories references/Hexalith.Tenants` -- expected: unchanged vs pre-closure pins.
- `git diff --check && ! rg -n '^(<<<<<<<|=======|>>>>>>>)' src tests _bmad-output` -- expected: clean.
- `git status -sb` -- expected: no new changes from this closure except the intentional deferred-work append (and this spec status flip); story-4.5 dirty files remain whatever they were.

## Suggested Review Order

- Frozen obsolete-closure intent: no orphan replay; tip stays authoritative.
  [`spec-resolve-main-rebase-conflicts.md:15`](spec-resolve-main-rebase-conflicts.md#L15)

- Forensic deferred note with full SHAs and non-actionable status.
  [`deferred-work.md:916`](deferred-work.md#L916)

- Checked tasks prove tip premises and branch hygiene were verified.
  [`spec-resolve-main-rebase-conflicts.md:43`](spec-resolve-main-rebase-conflicts.md#L43)

- Tip-inspection verification commands (no code rebuild/push).
  [`spec-resolve-main-rebase-conflicts.md:64`](spec-resolve-main-rebase-conflicts.md#L64)
