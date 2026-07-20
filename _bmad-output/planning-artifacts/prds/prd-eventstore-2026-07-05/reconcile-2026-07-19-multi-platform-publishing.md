# Reconciliation — 2026-07-19 Correct Multi-Platform EventStore Publishing

- **Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md` (status `final`, approved by Administrator 2026-07-19)
- **PRD:** `_bmad-output/planning-artifacts/prd.md` (status `final`, updated 2026-07-19)
- **Verdict:** `gaps-found` (narrow) — the substantive corrective work is downstream-owned, but the PRD has one mandatory provenance gap and one recommended intent gap.

## 1. Input

Trigger: release v3.75.0 published all 14 manifest-governed packages correctly (hashes independently verified), but its container tag resolves to a single `linux/amd64` Docker image manifest instead of the exact two-platform OCI index (`linux/amd64` + `linux/arm64`) required by Story 1.20 Acceptance Boundary 8. Root cause: the shared Hexalith.Builds publisher hard-codes `--os linux --arch x64` and the release workflow performs no post-publish registry validation. The proposal selects a Direct Adjustment inside Epic 3: preserve v3.75.0 as immutable non-authorizing evidence and add focused Story 3.12 to produce a conforming later release, with validation that fails closed on any nonconforming registry object.

Downstream state at reconcile time: Story 3.12 already exists in `epics.md` (line 1902) and `sprint-status.yaml` (`3-12-multi-platform-eventstore-container-publishing-correction: review`).

## 2. Approved Changes In The Proposal

- **A.** Story 1.20 proof packet: add an "Observed v3.75.0 Non-Authorizing Release Evidence" section (14 package hashes, failed container identity); keep all `approved_*` fields null and `final_decision: still blocked` (proposal §4.1).
- **B.** New Story 3.12 under Epic 3: exact two-platform OCI-index publishing correction with positive and negative validation, corrective semantic release, evidence handoff to Story 1.20 (proposal §4.2).
- **C.** Sprint tracker: add Story 3.12 at `backlog`; retain 1.20/Epic 1/Epic 3 states (proposal §4.3).
- **D.** Hexalith.Builds shared publisher + tests: two-platform publishing, exact-set post-publish validation, negative fixtures — separate Builds-maintainer authority (proposal §2, §5).
- **E.** EventStore release caller/tests: consume the corrected shared contract; reject nonconforming registry evidence (proposal §2).
- **F.** `docs/ci.md`: document the exact two-platform release contract and evidence boundary during implementation (proposal §4.4).
- **G.** Explicit dispositions: PRD "No requirement change" (citing FR22, NFR9, NFR11, NFR16); Architecture "No decision change" (AD-11/AD-12/AD-22); UX no impact; MVP impact none (proposal §2 artifact table, §3, checklist 3.1–3.3, 4.3).

## 3. PRD-Scoped vs Downstream Classification

| Item | Classification | Owner artifact | Reasoning |
| --- | --- | --- | --- |
| A — 1.20 packet evidence section | Downstream-only | Story 1.20 packet / epics.md | Story evidence and approval-field mechanics; FR36 intent (owner-approved packet, fail-closed identity) is unchanged. |
| B — Story 3.12 creation | Downstream-only | epics.md | Story slicing/ACs inside existing Epic 3. FR-to-epic traceability (PRD §11.1) is unchanged: FR22 and FR25 already map to Epic 3. No new FR/NFR is minted. |
| C — Sprint tracker | Downstream-only | sprint-status.yaml | Sequencing/tracking. |
| D — Builds publisher + tests | Downstream-only (external) | Hexalith.Builds repo | Architecture/workflow mechanism under separate maintainer authority. |
| E — Release caller validation | Downstream-only | EventStore CI/tests | Test evidence and workflow mechanics. |
| F — docs/ci.md | Downstream-only | docs/ci.md | Operational documentation. |
| G — "no PRD change" disposition | PRD-scoped verification | prd.md | This is the item the PRD reconciler must confirm or rebut — see §4 and §5. |
| (Implicit) proposal provenance | PRD-scoped | prd.md frontmatter | The PRD enumerates every approved sprint-change proposal in `source_artifacts`; this proposal is missing. |

MVP scope: unchanged (proposal §3 "MVP impact: None"); PRD §9 needs no edit. Glossary: no new PRD-level term (OCI index, child digest, platform descriptor are release-mechanics vocabulary owned by epics/ci docs). Success metrics: no SM change; SM1–SM3 remain valid. Story 3.12 does not alter the FR-to-epic table.

## 4. Coverage Check Per Item (against existing PRD text)

The proposal claims coverage by FR22, FR25, NFR9, NFR11, NFR16, NFR17 (Story 3.12 header) and FR22/NFR9/NFR11/NFR16 (artifact table). Verified against the PRD:

| Proposal intent element | PRD anchor | Covered? |
| --- | --- | --- |
| Reproducible, submodule-independent release behavior | NFR9; FR22 (release commands assert package mode) | **Covered.** |
| Manifest-driven publish scope; exactly the 14 `tools/release-packages.json` IDs; no extra package | FR10, FR25, NFR11 | **Covered.** Proposal explicitly preserves the inventory and changes no manifest. |
| Shared Hexalith.Builds gates / SHA-pinned actions / shared workflow governance | FR25 | **Covered.** The publisher fix lands in the already-required shared Builds path. |
| Immutable tags/digests; v3.75.0 never overwritten | NFR17 ("immutable image tags"); FR36 fail-closed identity discipline (§6.8) | **Covered.** |
| .NET SDK container support, no Dockerfile | §8.1 constraint "Use .NET SDK container support, not Dockerfiles" | **Covered.** Proposal Technical Impact item 1 explicitly complies. |
| Real-evidence validation (raw registry bytes, digest binding, smoke) rather than reported success | NFR16 (persisted end-state evidence, not status codes); §6.3 done-evidence | **Covered in intent.** NFR16's letter targets integration tests over state stores/read models; the registry-inspection gate is the same evidence philosophy applied to release artifacts. Acceptable as-is; the enforcement locus is Story 3.12 ACs (epics-owned). |
| Container publish scope limited to the single `eventstore` repository | NFR11 (by analogy: "no packages outside the release inventory") | **Approximately covered.** NFR11's text is package-only; container-repository scope is currently enforced only by Story ACs. No edit required by this proposal (no scope change occurred), noted for completeness. |
| **Exact two-platform requirement: version tag must resolve to an OCI image index containing exactly `linux/amd64` and `linux/arm64`** | No FR, no NFR, no §8.1 bullet mentions platform set, multi-arch, or OCI index | **Not covered — see Gap 2.** |
| Fail-closed release gate: single-platform / wrong-media-type / extra-unknown-descriptor releases must fail, not report success | Not stated at PRD level; NFR16 nearest in spirit | **Not covered as requirement text — folded into Gap 2.** |
| Separate durable release-owner authority before external publication | §6.8 FR36 owner-approval discipline (for parity closure); story-level gates otherwise | **Covered at the level the PRD operates**; publication-authority mechanics are story/process-owned. |

## 5. Gaps And Proposed Minimal Edits

### Gap 1 (mandatory, provenance): proposal missing from `source_artifacts`

The PRD frontmatter lists every approved sprint-change proposal through `sprint-change-proposal-2026-07-19-openbao-secret-store.md` but not this one, and PRD §1 declares the baseline "derived from the approved sprint-change proposals". Even for a downstream-only proposal, the established convention (e.g., the 2026-07-13 closure proposal, listed despite being non-PRD-scoped) is to record it.

**Minimal edit** — in `prd.md` frontmatter, after the `sprint-change-proposal-2026-07-19-openbao-secret-store.md` line, insert:

```yaml
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md
```

`updated: 2026-07-19` already carries today's date; no other frontmatter change.

### Gap 2 (recommended, intent traceability): supported container platform set absent from the PRD

The binding release-acceptance rule — the EventStore container must publish as one OCI image index containing exactly `linux/amd64` and `linux/arm64` — exists only in Story 1.20 AC8 and now Story 3.12. The PRD's only container-shape statements are NFR17's "immutable image tags" and §8.1's "no Dockerfiles". Consequence: judged by PRD text alone, v3.75.0 satisfies every release requirement (reproducible, manifest-clean 14 packages, immutable tag, SDK containers) — yet the organization correctly treats it as a failed release. The requirement that made it fail is not represented at the altitude this PRD claims to own ("product requirement intent … constraints/guardrails"). Story 3.12's cited coverage (FR22/FR25/NFR9/NFR11/NFR16/NFR17) is therefore approximate: none of those anchors state the platform set.

This is a pre-existing under-specification surfaced by the proposal, not new intent introduced by it (AC8 predates the proposal). It is nonetheless the exact class of item the PRD exists to hold: which platforms the shipped product supports is deployment-scope intent, not workflow mechanics.

**Minimal edit (primary)** — add one bullet to §8.1 Repository And Build, immediately after "Use .NET SDK container support, not Dockerfiles.":

> - Publish the EventStore container as one immutable OCI image index containing exactly the supported platform set, `linux/amd64` and `linux/arm64`; release validation must fail closed on any other manifest shape or platform set.

**Alternative (if NFR-level anchoring is preferred for §11.2 traceability)** — extend NFR17's final sentence: replace "immutable image tags" with "immutable image tags that resolve to a validated OCI image index containing exactly the supported `linux/amd64` and `linux/arm64` platforms". Choose one form, not both.

Adopting Gap 2 requires PRD-owner sign-off because the approved proposal's artifact table says "PRD: No requirement change" (see §6). It codifies already-approved intent and changes no scope, story, or sequence; declining it is also coherent, but then the two-platform rule remains traceable only to epic-level acceptance boundaries and SM2-style requirement mapping for Story 3.12's core criterion stays approximate.

### Gap 3 (optional, consistency): §11.2 NFR story coverage rows

Story 3.12 is now a primary enforcement story for release NFRs, but §11.2 lists NFR9 → 3.5/3.8/3.11, NFR11 → 3.6, NFR16 → (…)/3.11/7.4, NFR17 → 5.6/7.3/7.6. Existing rows remain true ("primary coverage"), so no edit is strictly required. If the owner wants §11.2 to reflect where release-evidence enforcement now lives, the minimal edit is appending `, 3.12` to the NFR9, NFR11, NFR16, and NFR17 rows.

## 6. Conflicts With Prior PRD Decisions

- **No substantive conflict.** The proposal preserves every PRD invariant it touches: 14-package manifest scope (FR10/FR25/NFR11), package-reference release mode (FR22/NFR9), immutability of published artifacts (NFR17), no Dockerfile (§8.1), fail-closed non-authorizing treatment of the failed release and Story 1.20/Epic 1 non-done status (FR36, §6.8 done-evidence, SM6), MVP scope (§9), and the G5/Parties exclusions (§6.9, §9.3).
- **One disposition tension, not a requirement conflict:** the approved proposal asserts (artifact table; checklist 3.1) that no PRD text change is required. Gap 1 does not contradict that assertion — frontmatter provenance is bookkeeping, not a requirement change. Gap 2 does tension with it; this reconcile records the tension explicitly rather than silently editing requirement text against the approved proposal's own disposition. Recommended handling: apply Gap 1 now; put Gap 2 (one bullet, wording above) to the PRD owner as a documentation-of-intent amendment.
- The proposal's requirement citations are internally consistent with the PRD: FR22, FR25, NFR9, NFR11, NFR16, NFR17 all exist and say what the proposal claims; no dangling FR/NFR references.

## 7. Disposition Summary

| # | Item | Action |
| --- | --- | --- |
| 1 | Frontmatter `source_artifacts` entry for this proposal | **Apply** (mandatory, minimal, non-substantive) |
| 2 | Two-platform OCI-index publishing constraint (§8.1 bullet or NFR17 clause) | **Recommend to PRD owner** (codifies approved intent; proposal's "no PRD change" disposition must be consciously overridden or ratified) |
| 3 | §11.2 rows `+3.12` for NFR9/NFR11/NFR16/NFR17 | Optional consistency edit; apply only together with or after item 2 decision |
| A–F | Packet, story, tracker, Builds publisher, caller validation, ci.md | No PRD action — downstream-owned, already in flight (Story 3.12 at `review`) |
