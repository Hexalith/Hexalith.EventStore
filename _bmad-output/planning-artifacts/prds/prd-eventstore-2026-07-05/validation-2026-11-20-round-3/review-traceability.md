# EventStore requirements traceability review

The three original traceability findings are resolved for the cases they identified. The full handoff still contains two high, one medium and one low finding. All line references below are to the examined working tree. Planning documents are under `_bmad-output/planning-artifacts/`, and story specifications and the sprint tracker are under `_bmad-output/implementation-artifacts/`.

## Prior findings

| Original finding | Disposition | Evidence |
| --- | --- | --- |
| 4. Multiple clause owners and inconsistent NFR17 roles | Resolved | FR4-C2 names 1.2, FR4-C5 names 1.16, FR34-C10 names 7.10 and FR34-C11 names 7.20. The required contributions have their own column. The detailed stories give 7.11 persisted readback, 7.14 shell and routes, and 7.19 operator journeys. Story 7.20 must collect the latter two before closing C11. For NFR17-C1, C3 and C4, 7.6, 7.8 and 7.9 also appear as primary in §11.2, their own story declarations and the reader output. The named roles remain subject to Story 9.4 owner confirmation. See PRD lines 426, 429, 462–473 and 665, and epics lines 545, 1147, 5985, 6130, 6193, 6261, 6324 and 6934. |
| 10. Historical 6.5c and 6.5d supporting mappings | Resolved | PRD §11.2.2, lines 679–686, identifies the four historical contributions and the approved design receipt. The actual coverage reader returned NFR7 for 6.5c, NFR7/NFR12 for 6.5d, and no NFRs for 6.5a or 6.5b. Numeric Story 6.5 retains NFR7/NFR12/NFR18/NFR19 and is the sole completion-writer key for these five records. The calculated digest of the integrated specification bytes preceding the approval marker matches the recorded digest. See epics lines 5412–5472, `spec-event-versioning-upcasting.md` §12, and `tools/phase_4_story_completion.py` lines 110–157. |
| 14. November stories missing from epic summaries | Resolved | Epic 2 reaches 2.19, Epic 5 reaches 5.15, and Epic 7 reaches 7.23. The tracker has 2.14 in progress and 2.15 in review. Stories 2.18, 2.19, 5.15 and 7.23 are backlog. The detailed 2.14 and 2.15 status paragraphs agree. See epics lines 396, 422, 438, 2152 and 2186 and sprint tracker lines 96, 98, 109, 110, 223 and 274. |

## Additional findings

### High · FR7 clause owners disagree with the stories expected to supply acceptance

PRD lines 436–438 and the matching epic ledger assign FR7-C3, C4 and C5 to 1.15, 1.18 and 1.19. Story 1.15 omits FR7 from its requirements declaration. Stories 1.18 and 1.19 explicitly say “supporting FR7.” Story 1.17 claims “Primary FR7” without a clause qualifier. These stories do describe the relevant persistence, duplicate-handling and paged-rebuild behavior. Their declared authority conflicts with the ledger and with the rule that supporting stories cannot close the requirement. A reader that checks only the NFR union cannot detect this. See epics lines 344–346, 471, 1091, 1202, 1258 and 1314, and PRD OR7 at line 785.

**Correction.** Confirm the disjoint FR7 assignments with the owners and name the corresponding clause and role in each detailed story. Have the G-CLAUSE check compare these declarations with both ledgers. Keep technical evidence and owner approval as separate conditions for closure.

### High · Corrective stories still require a superseded authorization record

Epic 9’s overview still tells implementers to use the Story 9.1 workflow. Story 9.4 explicitly requires “Story 9.1 passed, plus a Story 9.1 authorization record for gate G-CLAUSE.” Stories 3.19, 3.21, 5.13 and 7.21 also require that record. The Story 9.1 reconciliation says its validator, schema, workflow, tests and fixtures were deleted. PRD §0 and G-BASELINE specify recording owner-authorized corrective work in the owning spec under AD-12. Following the detailed prerequisite would make a mandatory corrective story wait for a retired artifact. See epics lines 455, 3275, 3352, 5041, 7026, 8024 and 8155, and PRD lines 120 and 731.

**Correction.** Update the active prerequisites to identify owner authorization in the applicable spec, the exact subject and required CI evidence. Date the superseded execution instructions wherever history needs to remain visible. Preserve the separate high-risk approval controls.

### Medium · Story 9.4 asks for FR15 slices in a ledger that contains none

Story 9.4 asks the FR12, FR15 and FR36 completion rules to “name the same slices as the ledger.” All 49 rows in that ledger match PRD §7.1 exactly. They contain FR12 and FR36 clauses. FR15’s six approved slices reside in PRD §11.1 and the epic completion rule, outside that ledger. A literal equality check for FR15 fails even when its six owners, both requirements inventories and every defined clause agree. See epics lines 327–381, 475 and 8162, and PRD line 615.

**Correction.** State where each comparison gets its denominator. Compare FR15’s six named ownership slices against PRD §11.1. Compare FR12 and FR36 with the stable-clause ledger. Adding new stable identifiers would require a separately approved scope decision.

### Low · Epic 3’s overview labels an active qualification story as backlog

The overview calls 3.21 “Backlog.” Both the tracker and the parent qualification spec record `in-progress`. This can misdirect the handoff for the Dapr and broker evidence that Story 3.19 needs. See epics line 404, sprint tracker line 152 and `spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md` line 5.

**Correction.** Date the earlier label or use the tracker’s current state and link to the parent specification. An in-progress story still owes its qualification evidence.

## Verification

- Compared the complete normative inventories. All 37 FRs and 19 NFRs matched exactly.
- Compared 49 unique clause IDs, each primary story and the complete supporting lists across both ledgers. All matched. All 49 evidence identity cells in the epic ledger are pending.
- Ran the repository’s `coverage()` against all 146 story headings. It accepted 143, including 139 of 142 numeric headings. The existing exceptions were superseded Stories 3.22 and 3.23 and historical evidence ledger 4.8. Inspected primary and supporting sets separately for each of the five NFR17 clause stories.
- Verified the PRD SHA-256 `4644f2b282a3ee7ff860869fd49a620cd32316284cdb594b32d45a6fea663a4b` and epic SHA-256 `9d488d02e056d656b33c2316cde4c06a00dbc424ce9ea50172c8ad5ecbbcda76` on EventStore HEAD `db5db24707a2468e69cf86016c3bba2f3fb5d516`.
