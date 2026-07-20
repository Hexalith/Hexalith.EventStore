# Reconcile Verification: Centralize And Refresh The Hexalith NuGet Catalog (2026-07-18)

## Input

- **Proposal:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18.md` — "Centralize And Refresh The Hexalith NuGet Catalog", status final, approved by Administrator on 2026-07-18.
- **PRD:** `_bmad-output/planning-artifacts/prd.md` (frontmatter `updated: 2026-07-19`).
- **Applying commit:** `befa320c` "feat: centralize and refresh Hexalith NuGet catalog" (2026-07-18), which touched `prd.md` (12 lines), plus `architecture.md`, `epics.md`, `sprint-status.yaml`, `epic-3-context.md`, and added the proposal file itself.
- **Verification mode:** verify the out-of-band application is complete and faithful; do not re-apply.

PRD-scoped intent in the proposal is confined to section 4.1 (FR21 rewrite + guardrail 8.1 rewrite) and the artifact-conflict row for `prd.md` ("Clarify FR21 and repository guardrail 8.1: the sole source-owned NuGet version authority is `references/Hexalith.Builds/Props/Directory.Packages.props`; define 'latest' as latest validated compatible rather than an untested highest version"). Sections 4.2 (architecture), 4.3/4.4 (Story 3.5 / Story 3.11), and 4.5 (tracker) are downstream mechanics owned by `architecture.md`, `epics.md`, and `sprint-status.yaml` and do not require PRD text.

## What The Loop Applied (from `git show befa320c -- prd.md`)

1. **FR21** rewritten to the proposal's NEW paragraph verbatim (both sentences: Debug/Release mode sentence + Builds-catalog sole-authority sentence).
2. **Guardrail 8.1**: old bullet "Keep package versions centralized in `Directory.Packages.props`." replaced with two new bullets — a single-authority bullet and a latest-validated-compatible freshness bullet.
3. **Traceability 11.1** FR21 row: "Debug source references and Release package references" → "Shared Builds package catalog with Debug source references and Release package references".
4. **Traceability 11.2**: new `NFR9 | 3.5, 3.8, 3.11` row; `3.11` added to the NFR10 and NFR16 rows.
5. Proposal registered in frontmatter `source_artifacts`.

After `befa320c`, two same-day/next-day commits (`8dfb154f`, `1d83607c`) applied the separate approved proposal `sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md`, which amended the FR21 mode sentence (explicit `UseHexalithProjectReferences=true` opt-in; unset/false = package intent in every configuration), the 8.1 mode bullet, and the FR21 traceability row wording. That proposal explicitly states "The existing shared-catalog ownership sentence remains unchanged." These are sanctioned supersessions, not drift.

## Coverage Check Per Approved PRD-Scoped Item

| # | Approved item (proposal ref) | Current PRD evidence | Status |
| --- | --- | --- | --- |
| 1 | FR21 NEW sentence: every source-owned NuGet dependency version declared in `references/Hexalith.Builds/Props/Directory.Packages.props`; consumer props import the catalog and declare no local `PackageVersion`, version override, or fallback version property (§4.1) | `prd.md` line 176, FR21, final sentence — **verbatim** | Present |
| 2 | FR21 NEW sentence: Debug source references when explicitly enabled, Release package references by default (§4.1) | Applied verbatim in `befa320c`; first sentence subsequently replaced by the approved story-3-5-reconciliation proposal with a stricter formulation preserving the same intent (source only on explicit `true` + existing root-declared source; unset/false = package in every configuration). `prd.md` line 176, sentences 1-2 | Present (superseded wording, same intent) |
| 3 | 8.1: remove old "Keep package versions centralized in `Directory.Packages.props`" bullet; state Builds as sole authority (§4.1 + artifact table) | Old bullet gone; `prd.md` line 280: "Keep every source-owned NuGet dependency version in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming `Directory.Packages.props` files only configure CPM and import the shared catalog." | Present |
| 4 | 8.1: freshness policy — latest validated compatible; stable pins prefer latest stable; prerelease channels / aligned families / SDK coupling / major versions validated as units; exceptions record reason, evidence, removal trigger; no downgrade on unlisted/omitted search results (§4.1) | `prd.md` line 281 — faithful imperative-mood paraphrase covering every clause | Present |
| 5 | Traceability FR21 row reflects catalog ownership (implied by FR21 change) | `prd.md` line 381: "Epic 3 - Ecosystem-wide Builds package catalog with explicit source opt-in and package-safe defaults" (set by `befa320c`, wording later evolved by the 3.5 reconciliation) | Present |
| 6 | Story 3.11 declared NFR coverage (NFR9, NFR10, NFR16) visible in high-risk NFR traceability (§4.4 "Requirements covered") | `prd.md` line 410 `NFR9 | 3.5, 3.8, 3.11`; line 411 `NFR10 | 3.1, 3.11, 7.4`; line 415 NFR16 includes `3.11` | Present |
| 7 | Proposal registered in PRD frontmatter `source_artifacts` (checkpoint (c)) | `prd.md` line 41: `sprint-change-proposal-2026-07-18.md` | Confirmed |
| 8 | NFR9 text | `prd.md` line 262 unchanged — the proposal proposed no NFR9 wording change; unchanged text ("Release builds must use package references ... unless intentionally overridden") is consistent with the new FR21 | Correctly untouched |

FR-to-epic mapping for FR22/FR25 (also covered by Story 3.11 per §4.4) already resolves to Epic 3 in table 11.1; the PRD has no per-story FR table, so no further FR-level rows are required.

## Fidelity Issues

None material. Two deliberate, non-semantic deltas noted for the record:

1. **Freshness bullet paraphrase (prd.md:281).** The proposal reads "audit automation never downgrades a pin because a package is unlisted or omitted from search"; the PRD reads "never downgrade because search omits or unlists a package." The PRD form drops the "audit automation" qualifier, making the no-downgrade rule apply universally — a strengthening, not a contradiction. Other deltas ("aligned release families" → "aligned families"; "major-version compatibility ... validated as units" → "validate ... major upgrades as units"; dropped "available") are stylistic and preserve every clause of the approved policy.
2. **FR21 first sentence and 8.1 mode bullet no longer match the proposal's exact NEW text.** `befa320c` applied them verbatim; the later approved `sprint-change-proposal-2026-07-18-story-3-5-reconciliation.md` (commits `8dfb154f`, `1d83607c`) replaced them with a stricter explicit-opt-in formulation and left the shared-catalog sentence untouched by design. This is a sanctioned supersession chain, consistent with the verified proposal's intent (source only when explicitly enabled; package-safe defaults).

Contradiction scan: no residual "pinned centrally" / "package versions centralized" phrasing anywhere in the PRD; FR22, FR25, NFR10, NFR11, NFR16 remain consistent with single-catalog ownership; the exception-documentation clause in 8.1 accommodates enforcement-level allowlists (e.g., the Playwright test exception) without PRD contradiction. Nothing applied to the PRD contradicts other PRD requirements or the proposal.

## Remaining Gaps

- **PRD-scoped: none.** All approved PRD intent changes are represented in current PRD text, and the proposal is registered in `source_artifacts`.
- Out-of-PRD scope (owned elsewhere, verified only as touched by `befa320c`, not audited here): architecture AD-11/release-convention wording (`architecture.md`), Story 3.5 revision + Story 3.11 addition (`epics.md`), tracker key rename + `3-11-...: backlog` (`sprint-status.yaml`), Epic 3 context recompilation, and all Builds/EventStore implementation mechanics (catalog additions, wrapper cleanup, tests/scripts/docs/automation), which the proposal itself assigns to Stories 3.5/3.11.

**Verdict: faithfully-applied.**
