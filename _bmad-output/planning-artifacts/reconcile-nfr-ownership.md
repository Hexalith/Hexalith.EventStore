# Reconciliation: NFR Story Ownership

**Input:** `_bmad-output/planning-artifacts/epics.md`  
**Target cross-check:** `_bmad-output/planning-artifacts/prd.md` §11.2 and the clause/readiness gates  
**Disposition:** **FAIL/BLOCKED.** This is a declaration audit, not delivery evidence. It does not authorize a story-status change, requirement closure, implementation readiness, release, migration, or downstream handoff.

## Methodology

- All 111 `### Story` sections were examined, including 109 bold `**Requirements coverage:**` declarations, Story 3.16's unbolded `Requirements coverage:` declaration, and Story 4.8's non-executable `**Historical requirements coverage:**` ledger.
- A declaration is **primary** only when the story's own coverage statement applies `primary`, `primary ownership`, or equivalent ownership language to that NFR. `Supports`, `supporting`, `investigative support`, `related`, explicit evidence-preservation language, and explicit no-closure/no-ownership language are **supporting**.
- In wording such as `Supports primary FR9, NFR14, ...`, `primary` modifies the named FR; the governing verb for the NFR is `Supports`, so the NFR is supporting.
- Hyphen/en-dash notation is not silently expanded. A literal endpoint is recorded separately as a **range endpoint**; an interior identifier is recorded only in **range-only interior caveats** and is not counted as an individual NFR declaration. This preserves the rule already stated in PRD §11.2.
- Story IDs below are exhaustive for the NFR declarations in story coverage statements. They establish ownership intent only; they do not establish implementation or evidence closure.

## Exact declaration classification

| NFR | Primary declarations | Supporting: individually named | Supporting: literal range endpoints | Range-only interior caveats (not counted as individual declarations) | Primary-owner disposition |
| --- | --- | --- | --- | --- | --- |
| NFR1 | 5.2, 5.5, 5.7 | 2.8, 2.10, 3.10, 5.4, 7.2, 7.7, 8.3, 8.6 | 7.3 (`NFR1–NFR2`), 7.4 (`NFR1–NFR2`), 7.19 (`NFR1–NFR2`), 8.1 (`NFR1–NFR4`), 8.5 (`NFR1–NFR4`), 8.7 (`NFR1–NFR2`), 8.9 (`NFR1–NFR4`), 8.11 (`NFR1–NFR4`) | — | Declared. |
| NFR2 | 5.2, 5.5, 5.6, 5.7, 5.8, 5.10, 7.2 | 1.5, 1.9, 1.10, 1.14, 2.5, 3.10, 7.1 | 7.3 (`NFR1–NFR2`), 7.4 (`NFR1–NFR2`), 7.19 (`NFR1–NFR2`), 8.7 (`NFR1–NFR2`) | 8.1, 8.5, 8.9, 8.11 (each `NFR1–NFR4`) | Declared, but the corrected complete tenant contract lacks an approved primary slice. Blocking semantic gap. |
| NFR3 | 5.3 | 8.3, 8.6 | — | 8.1, 8.5, 8.9, 8.11 (each `NFR1–NFR4`) | Declared, but 5.3 is bounded and does not own Tenants, generated-host, or future-host conformance. Blocking semantic gap. |
| NFR4 | 5.3, 7.6 | 5.4, 8.6 | 8.1 (`NFR1–NFR4`), 8.5 (`NFR1–NFR4`), 8.9 (`NFR1–NFR4`), 8.11 (`NFR1–NFR4`) | — | Declared. |
| NFR5 | — | 7.1 | — | — | **Primary unassigned; blocking.** |
| NFR6 | 1.18, 7.1 | 1.6 (reference-only; delegates completion to 1.18), 1.10, 2.8, 4.1, 4.3, 4.6, 6.4 | — | — | Declared. |
| NFR7 | 4.1, 4.2, 4.4, 4.5, 4.9, 4.10, 4.11, 4.12, 4.13, 4.14, 4.15, 5.1 | 1.3, 1.15, 1.17, 1.18, 1.19, 4.6, 4.8 (historical-only), 6.4, 6.5, 6.6, 7.8, 7.11, 8.1, 8.2, 8.4, 8.5, 8.7, 8.9, 8.10, 8.11 | — | — | Declared. Historical Story 4.8 has no closure authority. |
| NFR8 | 1.19, 6.2, 6.4 | 1.2, 1.9, 1.13, 1.16, 2.11, 4.7, 6.1, 6.3 | — | — | Declared. |
| NFR9 | 3.8, 3.11, 3.14 | 2.12, 3.3, 3.4, 3.5, 3.6, 3.12, 3.13 (related), 3.15, 3.16 | 8.1 (`NFR9–NFR12`), 8.8 (`NFR9–NFR11`), 8.11 (`NFR9–NFR12`) | — | Declared. |
| NFR10 | 3.1, 3.7, 7.10, 7.12, 7.13 | 3.8, 3.11 | — | 8.1 (`NFR9–NFR12`), 8.8 (`NFR9–NFR11`), 8.11 (`NFR9–NFR12`) | Declared. |
| NFR11 | 3.8, 3.14 | 3.6, 3.9, 3.12, 3.15, 3.16, 7.9 | 8.8 (`NFR9–NFR11`) | 8.1, 8.11 (each `NFR9–NFR12`) | Declared. |
| NFR12 | 1.17, 3.13, 3.15 | 1.20, 2.7, 2.8, 2.12, 3.16, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 7.5, 8.2, 8.4, 8.9, 8.10 | 8.1 (`NFR9–NFR12`), 8.11 (`NFR9–NFR12`) | — | Declared, but the expanded public-surface inventory is not reconciled to complete, disjoint primary slices. Blocking semantic gap. |
| NFR13 | — | 2.4, 2.9 | — | — | **Primary unassigned; blocking.** |
| NFR14 | 7.5, 7.14 | 1.8, 1.11, 2.3, 2.5, 2.6, 2.10 | 2.11 (`NFR14–NFR16`) | — | Declared. |
| NFR15 | 7.4, 7.19 | 1.16, 2.6, 2.8, 3.10, 7.3, 7.5, 7.20 | — | 2.11 (`NFR14–NFR16`) | Declared. |
| NFR16 | 1.20, 3.10, 3.13, 3.14, 4.9, 4.10, 4.11, 4.12, 4.13, 4.14, 4.15, 5.8, 7.3, 7.11 | 1.2, 1.3, 1.4, 1.5, 1.9, 1.10, 1.13, 1.14, 1.15, 1.17, 1.18, 1.19, 1.21 (evidence-only), 2.7, 2.8, 2.12, 3.1, 3.2, 3.4, 3.5, 3.6, 3.8, 3.11, 3.12, 3.15, 4.2, 4.4, 4.5, 4.7, 4.8 (historical-only), 6.4, 6.6, 7.1, 7.6, 7.7, 7.8, 7.9, 7.10, 7.12, 7.13, 7.19, 7.20, 8.7, 8.8, 8.9, 8.10 | 2.11 (`NFR14–NFR16`), 8.1 (`NFR16–NFR17`), 8.11 (`NFR16–NFR17`) | — | Declared. Historical Story 4.8 has no closure authority. |
| NFR17 | 3.14, 5.6, 5.8, 7.7 | 3.12, 5.7, 5.9, 7.6, 7.8, 7.9, 7.10, 8.6 | 8.1 (`NFR16–NFR17`), 8.11 (`NFR16–NFR17`) | — | Declared, but clause NFR17-C5 (crypto-shred boundaries) is unassigned. Blocking clause gap. |
| NFR18 | — | 6.5, 6.6 | — | — | **Primary unassigned; blocking.** Supporting references do not own the required documentation outcome. |
| NFR19 | 8.11 | 6.5, 6.6, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.9, 8.10 | — | — | Declared for the separately gated post-MVP commitment; not Phase 4 MVP readiness. |

## Ownership gaps and reconciliation notes

1. **No primary story declaration exists for NFR5, NFR13, or NFR18.** NFR5 is supported only by Story 7.1 (`epics.md:4701`); NFR13 only by Stories 2.4 and 2.9 (`epics.md:1537`, `epics.md:1775`); and NFR18 only by Stories 6.5 and 6.6 (`epics.md:4522`, `epics.md:4602`). These are blocking primary-owner gaps.
2. **PRD §11.2 falsely attributes NFR5 support to Story 2.8.** Story 2.8 declares NFR1, NFR6, NFR12, NFR15, and NFR16, but not NFR5 (`epics.md:1729`). The PRD row at `prd.md:569` must not be used as proof of Story 2.8/NFR5 ownership.
3. **NFR2 and NFR3 have primary declarations but not complete ownership of the corrected requirements.** The PRD itself records the missing corrected tenant-contract and all-host-contract ownership (`prd.md:483`). In particular, Story 5.3 owns only its production-authentication and committed-secret-removal slice (`epics.md:3806`), while NFR3 expressly requires Tenants, generated-host, and future-host conformance (`prd.md:341`).
4. **NFR12 has three primary declarations but no reconciled complete inventory ownership.** Stories 1.17, 3.13, and 3.15 declare primary ownership (`epics.md:1115`, `epics.md:2655`, `epics.md:2760`), while PRD §11.2 states that those declarations do not yet cover the expanded public-surface inventory (`prd.md:576`). Multiple primary declarations are not evidence that the slices are complete or disjoint.
5. **NFR17 top-level primary declarations do not close NFR17-C5.** The PRD's clause ledger leaves crypto-shred boundaries unassigned and explicitly forbids supporting Epic 8 work from substituting for missing MVP ownership (`prd.md:408`).

## Evidence and fail-closed handling

- Story coverage statements span `epics.md:412` through the Story 8 declarations. Story 3.16's non-bold declaration remains included as supporting evidence (`epics.md:2819`).
- Story 4.8 is historical, non-executable, and delegates all closure authority to Stories 4.9–4.15 (`epics.md:3258-3260`); it is therefore supporting/reference-only for NFR7 and NFR16.
- Range-only interior references remain discoverable in the table but cannot satisfy primary ownership or individual traceability without an explicit declaration.
- Until the three wholly unassigned NFRs and the NFR2, NFR3, NFR12, and NFR17-C5 semantic/clause gaps are reconciled into `epics.md` with content-bound evidence and every required gate passes, readiness remains `blocked/reject`.
