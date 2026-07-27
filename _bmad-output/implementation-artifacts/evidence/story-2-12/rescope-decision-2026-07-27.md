# Story 2.12 Re-Scope Decision Record — 2026-07-27

- Decision date: `2026-07-27`
- Decided by: `Administrator` (release owner `jpiquot`), during the `bmad-dev-story` 2.12 session
- Status: **decided and APPLIED 2026-07-27** via `bmad-correct-course`. Approved sprint change
  proposal:
  `../../../planning-artifacts/sprint-change-proposal-2026-07-27-story-2-12-runtime-identity-rescope.md`
- Recorded by: Claude Opus 5 dev-story session

This file records two owner decisions verbatim in intent. It intentionally does **not** amend
any acceptance criterion, epic, or architecture decision. `bmad-dev-story` has no authority to
rewrite acceptance criteria; that is a correct-course action.

## Why A Decision Was Needed

Two acceptance criteria had become impossible to satisfy as literally written.

**AC2 (exact source identity).** The approved EventStore SHA
`fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` is not merely stale — it is actively and repeatedly
overwritten on Tenants `main` by an automated `build(deps)` submodule bump:

| Tenants `main` | `references/Hexalith.EventStore` |
| --- | --- |
| `902065e` / `db09a84` | `fa2d1c9910f8` (approved, correctly adopted) |
| `230a533d` | `737b3e5a` (`/pushall` merge clobber) |
| `4ca5f86` | `b2d34025` (automated bump) |
| `f1053a31` | `c8c70030` (automated bump, observed live mid-session) |

The approved SHA is 46 commits behind EventStore `main`. Restoring the pin is proven green but
provably ephemeral.

`git log 902065e..HEAD -- references/Hexalith.EventStore` counts **5** commits that have moved
this gitlink since the approved adoption. Two of them landed while this single session was
running, and the umbrella's `references/Hexalith.Tenants` and `references/Hexalith.Builds`
worktrees were likewise advanced by the concurrent automation mid-session (to `0ded4a17` and
`1b1c0b0`). This corroborates the decision: a frozen consumer pin cannot survive the
repository's current automation.

**AC3 (exact package identity).** The 14 approved `.nupkg` files at
`999.1.20-proof.fa2d1c9910f8` do not exist in any avenue the External Prerequisite Contract
names — whole-filesystem scan, nuget.org, the Azure WORM archive, retained GitHub Actions
artifacts, and GitHub Packages with `read:packages` granted all returned negative (0 of 14).
AC3 compares against artifacts that no longer exist.

## Decision 1 — AC2 Re-Scoped To Track EventStore `main`

Tenants tracks EventStore `main` through its normal automated submodule bump instead of pinning
`references/Hexalith.EventStore` to a frozen owner-approved SHA.

Consequences that correct-course must resolve:

- AC2's "gitlink and checkout both equal that SHA" no longer applies and must be replaced.
- **AD-22** (`architecture.md:298`) states source mode compares the EventStore gitlink and
  checkout to the approved EventStore SHA. This decision contradicts AD-22 as written.
- Story 1.20's **source consumer procedure** stops being a gate for Tenants.
- The `authorization_story: 1-20` linkage weakens: activation was gated on an owner approval of
  one exact tested runtime. Tracking `main` removes that exact-runtime guarantee for Tenants.
- The `Activation Decision And Immutable Pins` table's source SHA becomes historical rather
  than binding.

## Decision 2 — AC3 Re-Scoped To A Published Package Version

Package mode validates against a real published catalog version (currently `3.82.0`, and
`3.83.0` at Tenants' newer Builds pin `1b1c0b0`) instead of the unpublished proof version, with
no byte-equality check against the Story 1.20 manifest.

Consequences that correct-course must resolve:

- AC3's exact-version and byte-equality requirements must be replaced.
- The 14-package SHA-256 manifest
  (`4271ddc76411780591423ab024b776cd34a2abccf1cc2dac03a245e141dbe0bc`) and the
  `External Prerequisite Contract` become non-binding for Story 2.12.
- The `package_lane_status: blocked` and `package_lane_prerequisite_receipt` frontmatter fields
  need re-evaluation.
- **AD-11/AD-12** should be re-checked: AD-12 forbids closing high-risk compatibility on
  compilation alone, so the replacement AC still needs persisted-path evidence even without
  byte equality.
- The approved Builds prerequisite (`8f32f127`, PR #47) becomes historical; the surviving
  useful part of it is the **central `Hexalith.EventStore.Gateway` catalog entry**, which is
  retained in the current Builds pins and is still required by AC4.

## What Already Satisfies The Re-Scoped Intent

Proven in this session (`dual-mode-2026-07-27.md`) and unaffected by either re-scope:

- Gateway conditional alignment is published on Tenants `main` (`a7ca142`) with its
  `PackageGovernanceTests` host rule, passing 115/115.
- Source mode resolves 7 EventStore edges, **all** `type: project`, **0** package edges.
- Package mode resolves **0** project edges and **7** package edges, so Release assets contain
  zero EventStore `ProjectReference`.
- Gateway and DomainService resolve identically in both directions — **no mixed graph is
  reachable**, which is AC4's structural requirement.
- Full dual-mode matrix green with `--warnaserror` 0 Warning(s) / 0 Error(s):
  Contracts 115/115, Server 738/738, UI 1266/1266, Integration 167 passed / 1 skipped, in
  **each** mode.

## Required Re-Validation Before Closure

The matrix above was run on a proof clone whose EventStore gitlink was restored to
`fa2d1c9910f8`. Under the re-scoped AC2 the binding pin is whatever Tenants `main` carries, so
the matrix must be re-run at the accepted Tenants `main` commit (currently `f1053a31`, EventStore
`c8c70030`, Builds `1b1c0b0` → catalog `3.83.0`) before the story can close. Only the unmodified
solution-level Debug/source restore has been proved at a real `main` pin so far (exit 0 at
`4ca5f86`).

## Scope Statement

No acceptance criterion, epic, architecture decision, or planning artifact was modified by this
record. No dependency identity was changed in any published repository and nothing was pushed.
Story 2.12 remains `in-progress` and below `review`.
