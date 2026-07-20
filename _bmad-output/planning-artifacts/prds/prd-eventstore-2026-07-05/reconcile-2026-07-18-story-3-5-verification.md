# Reconciliation Verification — 2026-07-18 Reconcile Story 3.5 Activation And Scope

- **Source proposal:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md`
- **Applied by:** commits `8dfb154f` ("feat: reconcile Story 3.5 Activation and Scope") and `1d83607c` ("feat: enhance Story 3.5 with ecosystem-wide Builds catalog and explicit source opt-in")
- **Verification date:** 2026-07-19
- **Verdict:** `faithfully-applied` (no PRD-scoped gaps)

## Input

- Proposal: approved 2026-07-18 (batch, moderate scope), final. PRD-scoped adjustments per proposal section 4.1 and the section 2 artifact-conflict table row "`prd.md` FR21 and repository guardrail":
  1. Replace FR21's dependency-mode sentence with explicit source opt-in wording (exact NEW text in proposal section 4.1), retaining the shared-catalog ownership sentence unchanged.
  2. Clarify the section 8.1 repository guardrail: unset `UseHexalithProjectReferences` is package intent even in Debug; source mode requires explicit `true`.
- All other section 4 items (4.2 AD-11, 4.3/4.4 epic AC1/AC6, 4.5 story artifact, 4.6 tracker/context, 4.7 docs/follow-ups) are architecture-, epics-, story-, or tracker-owned and do not require PRD text.
- PRD under verification: `_bmad-output/planning-artifacts/prd.md` at HEAD (`00314259`), `updated: 2026-07-19`.

## What The Loop Applied (PRD file only)

Commit `8dfb154f` (verified via `git show 8dfb154f -- _bmad-output/planning-artifacts/prd.md`):

- Rewrote the FR21 dependency-mode sentence to the proposal 4.1 NEW text; the catalog-ownership sentence was left byte-identical.
- Replaced the 8.1 bullet "Preserve Debug source-reference and Release package-reference behavior." with "Require explicit `UseHexalithProjectReferences=true` for source intent; unset or explicit `false` remains package intent in Debug, Release, and configuration-less evaluation."
- Added `sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md` (and the separate `sprint-change-proposal-2026-07-18.md`) to frontmatter `source_artifacts`; bumped `updated` to 2026-07-18.

Commit `1d83607c` (PRD portion):

- Updated the section 12 traceability row for FR21 from "Shared Builds package catalog with Debug source references and Release package references" to "Epic 3 - Ecosystem-wide Builds package catalog with explicit source opt-in and package-safe defaults".

Post-application integrity: the only later commit touching `prd.md` is `fcff0464` (OpenBao secret-store proposal); its diff does not touch FR21, section 8.1, NFR9, or the FR21 traceability row. The applied text survives intact at HEAD.

## Coverage Check Per Approved Item

| # | Approved PRD-scoped change | Status | Evidence |
| --- | --- | --- | --- |
| 1 | FR21 NEW text (proposal 4.1) | Applied, verbatim | `prd.md:176` — "Cross-repo Hexalith library dependencies use source project references only when `UseHexalithProjectReferences=true` is explicitly supplied and the root-declared source exists. An unset or explicit `false` value selects package references in every configuration, including Debug; Release and configuration-less evaluation therefore remain package-safe." Word-for-word match against proposal section 4.1 NEW. |
| 2 | FR21 shared-catalog sentence "remains unchanged" (proposal 4.1) | Preserved | `prd.md:176` second/third sentences ("Every source-owned NuGet dependency version ... no local `PackageVersion`, version override, or fallback version property.") are unchanged from the pre-commit text (confirmed in the `8dfb154f` diff). |
| 3 | Section 8.1 repository guardrail clarification (artifact table) | Applied, faithful | `prd.md:282` states exactly the requested clarification: explicit `true` for source intent; unset or explicit `false` is package intent in Debug, Release, and configuration-less evaluation. |
| 4 | Proposal listed in frontmatter `source_artifacts` | Confirmed | `prd.md:42` — `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md`. |
| 5 | Traceability row FR21 (supporting, not explicitly mandated by section 4) | Applied, consistent | `prd.md:381` — "Epic 3 - Ecosystem-wide Builds package catalog with explicit source opt-in and package-safe defaults". Correctly reflects both the AC1 explicit-opt-in/package-safe rule and the retained ecosystem-wide AC6 scope; supersedes the older Debug-source phrasing that would have contradicted item 1. |

Downstream (non-PRD) items — verified present only as sequencing/consistency context, no PRD text required:

- AD-11: `architecture.md:126` contains the proposal 4.2 ADD text verbatim ("Unset or explicit `UseHexalithProjectReferences=false` is package intent in every configuration, including Debug. Explicit `true` is source intent ... Empty or unset configuration remains package-safe.").
- Epics: `epics.md:1654` records the Story 3.3 `done` activation gate for Story 3.5; AC1/AC6 rewrites landed in `epics.md` via both commits.
- The `UseNuGetDeps` authority/normalization rule (proposal section 2 Technical Impact) landed in the Story 3.5 implementation artifact (`_bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md:208-209, 293`). It was never a PRD-scoped change, and neither FR21 nor 8.1 needs it.

## Fidelity Issues

None affecting the PRD. Two observations, both resolved or immaterial:

1. The `8dfb154f` commit message says the implementation acceptance was "narrowed ... to Hexalith.Builds and EventStore", which contradicts the proposal's ecosystem-wide AC6 retention. The PRD text itself never carried that narrowing, and `1d83607c` restored ecosystem-wide scope in `epics.md`/story artifact and stamped "Ecosystem-wide" into the PRD traceability row. Current state matches the proposal; only the intermediate commit message is misleading.
2. FR21 keeps the pre-existing generic phrase "version override" rather than the proposal AC6's literal `VersionOverride` MSBuild attribute. This sentence is the retained catalog sentence the proposal explicitly said "remains unchanged", and the literal-token requirement is an epics/story AC6 concern (present in `epics.md`), so no PRD edit is owed.

Contradiction sweep (item b): no conflicts found.

- NFR9 (`prd.md:262`) — "Release builds must use package references ... unless intentionally overridden" — coherent with FR21: explicit `true` is the intentional override; unset/`false` stays package-safe.
- FR22 (`prd.md:177`) and the goal at `prd.md:93` (no source-reference/submodule leakage into release output) remain consistent with package-safe defaults.
- Grep of `prd.md` for source-reference-mode language finds only lines 93, 176, and 282; no residual "Debug source references by default" wording anywhere in the PRD.
- Nothing applied weakens the AC4 completion gate, Tenants identity boundaries, or MVP scope — the PRD contains no text about those story mechanics, matching the proposal's "no product-goal change" classification (checklist 3.1).

## Remaining Gaps

None PRD-scoped. All approved PRD adjustments (FR21 rewrite, 8.1 guardrail, frontmatter registration) are present, verbatim where the proposal specified exact text, and untouched by subsequent commits. Remaining proposal work — Story 3.3 verification to `done`, Story 3.5 ecosystem-wide implementation under per-repository authority, AC4 closure pending Stories 1.20/2.12, and the section 4.7 documentation follow-ups — is story/epics/tracker execution owned outside the PRD and correctly absent from PRD text.
