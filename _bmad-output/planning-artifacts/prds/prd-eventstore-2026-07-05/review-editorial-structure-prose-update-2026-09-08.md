# Editorial Review (Structure + Prose) — 2026-09-08 PRD Update

Scope: today's uncommitted delta to `_bmad-output/planning-artifacts/prd.md` (working tree vs `HEAD`, commit `8312bced`) plus the new `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-08-nfr3-nfr4-authentication-ratification.md`. Only the STRUCTURE and PROSE lenses ran, restricted to the sections named in the brief: frontmatter `source_artifacts`, §1 last paragraph, §1.1, §4, §6.1/§6.4 done-evidence paragraphs, §6.8, §7 (NFR3/4/5/7/8/12/17/18/19), the FR5/FR16/FR33/FR34 rows in §6, §8.1, §9.1, §9.2, §10, §11.1–§11.3, §12. Content correctness, requirement adequacy, and whether findings were "properly fixed" are out of scope and left to other reviewers.

## Method notes

- Diffed the working tree against `HEAD` for `prd.md` to isolate exactly what changed today; read the sprint-change-proposal in full since it is entirely new.
- Parsed the frontmatter YAML programmatically (`yaml.safe_load`) — valid, 59 unique `source_artifacts` entries, all resolve to files that exist in the repo.
- Programmatically checked every markdown table's pipe count per row against its header across the whole document — no column-count mismatches anywhere, including the two tables named in the brief (§11.1 now 3 columns, §11.2 now 2 columns — both confirmed).
- Extracted every `§N`/`§N.M` reference and every heading in the document and confirmed each reference resolves to a real heading. No broken section references.
- Extracted every `OR<n>[a]` reference in the body and confirmed each one exists as a row in the §12 table, and vice versa. No orphaned or missing OR items.
- Checked heading sequence (`##`/`###` numbering) for gaps, duplicates, or orphans — none; 0–13 all present and sequential, all `###` subsections correctly nested under their `##` parent.
- Checked for real em/en-dash characters anywhere in `prd.md` and the new proposal — zero. The entire document (both before and after today's edit) uses plain ASCII hyphens for both numeric ranges (`NFR1-NFR4`) and parenthetical asides (`Epic 1 - Domain author...`). Today's additions follow this convention consistently, so hyphen usage itself is not inconsistent — see Finding 5 for the one place the *word* "en-dash" is used to describe this hyphen.
- Cross-checked the sprint-change-proposal's quoted "Approved Changes" against the corresponding `prd.md` §7 rows byte-for-byte.

## Findings

### Structure

**S1 — §12 table rows are not in ascending order (prd.md:522-536).**
The brief specifically asked me to check this. Current row order: OR1, OR2, OR3, OR4, OR5, OR6, OR7, OR8a, OR8, **OR12, OR10, OR11**, OR9. Everything through OR8/OR8a is fine, but from there the table jumps to OR12, back down to OR10, then OR11, then finally OR9 — the opposite of ascending order, and OR9 ends up last even though it is lower-numbered than the three items ahead of it. This is a table introduced from scratch today (the whole owed-refinements table replaces a 3-line paragraph), so the disorder is entirely delta-introduced. It costs a reader scanning by ID a nontrivial amount of back-and-forth.
Fix: reorder to OR1, OR2, OR3, OR4, OR5, OR6, OR7, OR8a, OR8, OR9, OR10, OR11, OR12 (move the OR9 row up to immediately follow OR8, and resequence OR10/OR11/OR12 in that order). No IDs need to change, only row position — so this is a pure cut/paste reorder, safe to apply mechanically.

**S2 (secondary, lower confidence) — OR8a / OR8 adjacency reads backwards (prd.md:531-532).**
`OR8a` (a lettered sub-item) is listed immediately *before* `OR8`, the number it appears to be a sub-letter of. Normal convention is base-number-then-letter (`OR8`, then `OR8a`). Here they are also about unrelated things — OR8a is "bind the correctness gate" (a test-lane control), OR8 is "add a guard that diffs PRD §7 against the epics.md inventory" — so the letter-suffix relationship is coincidental, not hierarchical, which makes the reversed order more likely to misread as intentional grouping than it should. I did not fold this into the S1 fix because renumbering `OR8a`/`OR8` (rather than just reordering) would touch IDs that may be cited elsewhere; flagging for a human call rather than prescribing a specific rename.

### Prose

**P1 — §10 intro sentence's tag taxonomy doesn't match the tags actually used (prd.md:390).**
"Metrics are marked **achieved**, **live**, or **not met**." But the metrics in this section are actually tagged `(achieved 2026-07-05)`, `(achieved 2026-08-01)`, `(achieved)`, `(partially met)` [SM2], `(not met)` [SM6], and — for SM4, SM7, and all of SM8–SM12 — **no tag at all**. "Live" never appears as a per-metric tag; it only appears once, as the section heading "Live delivery metrics." "Partially met" is used but isn't one of the three categories the intro sentence promises. A reader who takes the intro sentence literally will go looking for a "live" tag that doesn't exist and be surprised by "partially met."
Suggested replacement:
> Metrics are marked **achieved**, **partially met**, or **not met**; a metric without a tag is still being measured with no interim disposition to report.

**P2 — FR33 switches from comma- to semicolon-separated listing, inconsistent with its sibling FR34 in the same delta (prd.md:260 vs 270).**
FR33 (today's rewrite): "...must introduce folded snapshots; reduce projection replay cost so that replaying an already-current projection performs zero event reads; add projection sequence guards; support event schema versioning/upcasting; reject an event whose metadata identity components are absent or not ULID-safe rather than accepting it; and add cancellation-token seams..." — none of these list items contain an internal comma, so there's no disambiguation need for semicolons.
FR34, edited in the same delta and structurally identical (a long serial list of remediation actions, one of them longer than any FR33 item), keeps plain commas throughout: "...document at-least-once unordered delivery, add poison/dead-letter handling, bound in-memory deduplication, ... and restore IntegrationTests CI coverage such that the lane runs on every push to `main` and asserts persisted state for at least the FR23, FR27, and FR30 paths."
This is a same-day, same-section style inconsistency. Suggested fix — revert FR33 to commas to match FR34 and the rest of §6:
> Cost and evolution remediation must introduce folded snapshots, reduce projection replay cost so that replaying an already-current projection performs zero event reads, add projection sequence guards, support event schema versioning/upcasting, reject an event whose metadata identity components are absent or not ULID-safe rather than accepting it, and add cancellation-token seams to published processing/query/projection interfaces.

**P3 — Ambiguous referent in the new "Parties" glossary entry (prd.md:151).**
"...references to Parties Story 8.6 and Parties Story 8.7 are Parties stories, not EventStore Epic 8 stories, which independently exist as Azure Key Vault Production Adapter Conformance and Server Persistence And Snapshot Integration." The reader can't tell from the sentence which of the two named EventStore stories (8.6 or 8.7) corresponds to which title — "which independently exist as X and Y" leaves the 8.6↔X / 8.7↔Y pairing to inference.
Consider:
> ...references to Parties Story 8.6 and Parties Story 8.7 are Parties stories, not EventStore Epic 8 stories: EventStore's own Story 8.6 is Azure Key Vault Production Adapter Conformance, and its own Story 8.7 is Server Persistence And Snapshot Integration.

**P4 — Missing article / number agreement in the §6.1 done-evidence closer (prd.md:194).**
"These remain open residual risk measured by SM8 and bounded by SM-C4, not delivered coverage." "These" (plural, referring to the listed evasions) takes "risk" as a bare singular noun with no article — reads as a dropped "a" or "an".
Suggested replacement:
> These remain an open residual risk, measured by SM8 and bounded by SM-C4, not delivered coverage.

**P5 — "en-dash range" mislabels the actual character used, twice in the new §11.2 text (prd.md:473, 494).**
Both instances — "Stories whose sections declare coverage only through an en-dash range are footnoted..." (line 473) and "Range-notation footnote: the following stories declare coverage through an en-dash range..." (line 494) — call the separator an "en-dash," but every range in this table (and everywhere else in the document, per the method notes above) is written with a plain ASCII hyphen (e.g. `5.4-5.5`, `7.2-7.4`). There is no en dash (`–`) anywhere in the file. This is a terminology/character mismatch introduced today.
Suggested replacement (both occurrences): replace "en-dash range" with "hyphen range" (or "hyphenated range").

**P6 (borderline, cross-artifact — flagging, not asserting which side is wrong) — the sprint-change-proposal's "Ratified NFR4, unchanged" blockquote does not match `prd.md`'s actual NFR4.**
`sprint-change-proposal-2026-09-08-nfr3-nfr4-authentication-ratification.md:71-73` presents NFR4 as "unchanged from the 2026-09-06 text":
> No committed configuration, ... Development and test credentials are injected through user-secrets, environment variables, ephemeral developer tooling, or runtime-generated fixtures and cannot be loaded as a non-Development fallback.

But `prd.md:308` (§7, today's text) reads:
> No committed configuration, ... Development and test credentials are injected only through this closed list of channels - .NET user-secrets, environment variables, runtime-generated test fixtures, and the Aspire AppHost parameter/secret mechanism - and cannot be loaded as a non-Development fallback. Adding a channel to this list requires a proposal.

The channel list was narrowed/renamed ("ephemeral developer tooling" → dropped; ".NET user-secrets" and "the Aspire AppHost parameter/secret mechanism" → added) and a new closing sentence was added, yet the proposal that is supposed to be the sole authorizing artifact for today's PRD edit explicitly labels its quoted text "unchanged." I'm not asserting which document is wrong — that's a content/provenance question for another reviewer — but as a matter of internal consistency, a document that quotes another verbatim and labels the quote "unchanged" should match byte-for-byte, and this one doesn't. Likely fix: update the proposal's blockquote to the text actually in `prd.md` (since that's the file already carrying the detailed channel list), or, if the closed list was never approved, that's a provenance question outside this lens's scope.

### Checked, no defect found

- Frontmatter YAML parses cleanly; 59 unique, all-existing `source_artifacts` entries, no duplicates.
- Every markdown table in the document (including §11.1's new 3-column and §11.2's new 2-column shape) has a pipe count matching its header on every row.
- Every `§N`/`§N.M` cross-reference resolves to a real heading; heading numbering 0–13 has no gaps, duplicates, or orphans.
- Every `OR`-number referenced in body text exists as a row in the §12 table (OR1–OR12, OR8a all accounted for); no orphaned OR references.
- Glossary (§4) alphabetical ordering is correct for every term added today (Break-glass, Canonical-Intent Descriptor, Corrective Release, Durable Admission, Folders, G5, Goldens, OQ8, Owner Roles, Parity, Parity Packet, Parties, `pdenc-v2`, Production Path, Provenance Classification, Retention Tier all sit in correct alphabetical position).
- "Owning stories" (§11.1 header) vs. "Declaring stories" (§11.2 header) initially looked like the kind of terminology drift the brief warned about, but each is glossed inline immediately below its table (§11.1: "declared... owns the requirement"; §11.2: "declares... whether as primary or supporting coverage, not the same as delivered/owned") — the distinction (owning = the one true primary owner; declaring = any story that mentions the NFR at all) is deliberate and explained, not accidental drift. Not flagged as a defect.
- "Loss class" terminology (NFR7, SM11, SM-C5) is used consistently throughout today's additions.
- No real em/en-dash character usage inconsistency (see method notes) beyond the mislabeling in P5.
- No repeated-word or double-space typos found in a full-text scan of the added lines.

### Noted but explicitly out of scope (handing to other reviewers, not included in the actionable list)

- The §11.2 "Range-notation footnote" (prd.md:494) cites specific NFR/story combinations ("NFR2 and NFR3 in 8.1, 8.5, 8.9, 8.11"; "NFR10 in 8.1, 8.8, 8.11") that do not appear to match the table rows directly above it — e.g., neither NFR2's nor NFR3's row contains 8.1, 8.5, 8.9, or 8.11 at all, while NFR1's and NFR4's rows do contain exactly that pattern; NFR10's row has no 8.x entries at all. This smells like a mislabeling (possibly NFR1/NFR4 were meant instead of NFR2/NFR3), but confirming the correct story-to-NFR mapping requires auditing `epics.md`, which is a content-correctness exercise outside the structure/prose lenses. Flagging for the content/adversarial reviewer rather than prescribing a fix I can't verify.
