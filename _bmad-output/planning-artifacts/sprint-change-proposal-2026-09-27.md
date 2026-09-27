# Sprint Change Proposal: Split and compact Story 6.5

**Date:** 2026-09-27
**Status:** Approved by Administrator for the backlog and story-file reorganization on 2026-09-27; implemented
**Scope:** Moderate backlog reorganization within Epic 6
**Review mode:** One complete proposal

## 1. Issue summary

Story 6.5 is an architecture gate, but its working story file is 1,010 lines (330,991 bytes). The actionable story occupies roughly the first 52 lines; a 36-loop change log, review triage, and design notes occupy roughly 950 lines. The separate 1,008-line normative artifact is an unapproved candidate with ten open v37 findings (`BH37-1` through `BH37-10`). The story currently asks one work item to settle writer evidence, shared reads/projections, publication/delivery, compatibility, and final cross-cutting approval. This obscures the remaining work and makes review difficult.

The trigger is the Administrator's request to create several tracked stories and compact the Story 6.5 file. The current uncommitted edits to the story and `epics.md`, and the untracked normative draft, are user work to preserve. The earlier v37 instruction to stop rederivation remains recorded; this proposal does not itself begin another design iteration or alter the approval receipt.

## 2. Impact analysis

| Artifact or area | Impact |
| --- | --- |
| Epic 6 | Add three specification work stories (6.5a–6.5c). Keep 6.5 as the integrated AD-13 approval gate and 6.6 as the implementation story. Explicit execution order is 6.5a, 6.5b, 6.5c, then 6.5 approval, then 6.6. The tracker explicitly supports lettered split-story IDs, and existing 6.5/6.6 references remain stable. |
| Story 6.5 file | Retain intent, constraints, code map, short acceptance gate, and verification. Move review/change history, triage, and design notes verbatim to linked historical files; target at most 150 lines for the active story. |
| Normative artifact | Keep `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` as the single content-bound AD-13 artifact. No change or approval is implied by splitting the backlog. Its current `UNAPPROVED` receipt stays unchanged. |
| PRD | FR33 and clause FR33-C5 continue to have one final owner, Story 6.5. The three new stories support that owner; no requirement or MVP scope change is needed. |
| Architecture | AD-13 still requires one approved specification and vectors before runtime work. No architecture rule changes. |
| UX | No UI flow or screen changes; current support-safe Type Catalog/stream/replay evidence remains the requirement. |
| Epic context and tracker | Add the three work stories to Epic 6 context and the sprint tracker as backlog. Preserve 6.5 `in-progress` and 6.6 `backlog`; do not mark the spec approved or implementation authorized. |
| Code, infrastructure, deployment | None in this correction. The implementation risks remain in Story 6.6 and its approval gate. |

No future epic becomes obsolete or needs resequencing. Epic 7 may later display evidence, and Epic 8 remains optional; neither is pulled into these specification stories.

## 3. Recommended approach

Use direct backlog adjustment. Three independently reviewable specification work stories feed one integration and human approval gate. The existing normative file stays single so its exact-byte approval rule and Story 6.6 authorization sentence retain their established meaning. This is **medium** planning effort and **medium** coordination risk: cross-story terms, codecs, and bounds must be reconciled before final approval. It adds three backlog checkpoints before the existing implementation start; it does not claim a calendar estimate or runtime delivery.

Rollback would discard useful draft/review evidence. Reducing FR33 or changing the MVP would leave the required evolution contract unresolved. Neither is recommended.

## 4. Detailed change proposals

### 4.1 Epic 6 backlog: add focused specification stories

**OLD:** Story 6.5 owns all event-evolution design and approval; Story 6.6 follows it directly.

**NEW:** Retain those IDs and introduce these three backlog stories in `epics.md`, each with its own short story file:

| Story | Deliverable and acceptance boundary | Open v37 findings routed here |
| --- | --- | --- |
| **6.5a — Event contract, writer, and migration evidence spec** | Freeze canonical identity/version metadata, registry and fingerprint, bounded V1/V2 writer admission, actor save/readback provenance, no-op and command outcomes, retained V1 migration, and exact legacy compatibility. Supply source inventory, schemas, numeric bounds, failure/cancellation and rollout vectors for these seams. Produce a reviewed section candidate for the common normative artifact; no runtime changes or independent 6.6 authorization. | `BH37-1`, `BH37-2`, `BH37-6`, `BH37-7`, `BH37-8` |
| **6.5b — Verified read, replay, and projection spec** | Freeze authenticated raw source, shared upcast/read pipeline, bounded replay and timeline, aggregate/projection/query identity, successor byte ownership, scratch budgets, checkpoint/last-good-state and cancellation outcomes. Supply cross-consumer equivalence and tamper/race vectors; no runtime changes or independent 6.6 authorization. | `BH37-3`, `BH37-4`, `BH37-5`, `BH37-10` |
| **6.5c — Publication, subscription, and rollout spec** | Freeze pinned publication bytes, broker membership and route effects, subscriber poison/ack decisions, transport and legacy JSON handoff, migration/rollback sequencing, provider capability probes, and release-safe operational vectors. Reconcile the time-offset/delivery-digest wording. Produce a reviewed section candidate; no runtime changes or independent 6.6 authorization. | `BH37-9` |

Each new story must say that findings are tracked for later resolution; its creation does not silently authorize a v38 rewrite. Story 6.5 remains responsible for resolving conflicts among the three candidates, ensuring all ten open findings have an accepted disposition, updating the one normative artifact, validating its exact content digest, and obtaining the named human approval that explicitly authorizes Story 6.6. Story 6.6 gains explicit dependencies on 6.5a–6.5c **and** the final 6.5 gate. This preserves the current user edit that allows 6.5 to remain in progress during review.

### 4.2 Compact the active Story 6.5 file

**OLD:** `## Implementation Notes` contains the full `## Spec Change Log`, `## Review Triage Log`, and `## Design Notes` through line 1,003.

**NEW:** Replace that span with a short handoff and three exact-content links:

```markdown
## Implementation Notes

The current AD-13 candidate is `spec-event-versioning-upcasting.md` and remains
unapproved. Stories 6.5a–6.5c own focused specification work; this story owns
integration, disposition of all open findings, content-bound approval, and
explicit authorization for Story 6.6. No runtime work starts from a work-story
review or an unapproved draft.

Historical material, retained verbatim for traceability:
- `story-6-5-review-change-log.md`
- `story-6-5-review-triage.md`
- `story-6-5-design-notes.md`

The latest review snapshot is v37 with BH37-1 through BH37-10 open. The prior
request for no further rederivation remains part of that history.
```

Place those three files beside the active story in `_bmad-output/implementation-artifacts/`. Preserve all historical text, finding IDs, verdicts, rejected points, signed fixture references, and the existing user edits exactly; add only a clearly separate header saying the files are historical review material, not additional approval authority. Keep the `<frozen-after-approval>` block, story front matter, code map, and verification section in the active file. Replace its broad task checklist with four final-gate checks: the three work-story outputs are reviewed; cross-section schemas/bounds/vectors are consistent; every open finding has a documented disposition; the unchanged six-field human receipt validates the exact normative bytes. Do not copy the normative specification into the story files.

### 4.3 Align planning and tracking text

| File and section | OLD | NEW |
| --- | --- | --- |
| `epics.md`, Story 6.5 | One story writes and approves every contract. | One story integrates the 6.5a–6.5c candidates, closes cross-section decisions, and owns the sole approval gate; keep its current in-progress/unapproved reconciliation. |
| `epics.md`, Story 6.6 | Depends on approved Story 6.5. | Depends on completed 6.5a–6.5c work and valid approved Story 6.5; otherwise backlog/unauthorized. |
| `epic-6-context.md`, Stories and Cross-Story Dependencies | Lists 6.1–6.6 and 6.5→6.6. | List 6.5a–6.5c as preparation, then 6.5→6.6 as final gate; do not claim runtime value from any spec. |
| `sprint-status.yaml`, Epic 6 | 6.5 in-progress; 6.6 backlog. | Add 6.5a–6.5c backlog through the sprint-planning tracker workflow; preserve existing statuses and avoid a manual tracker edit prohibited by the current Story 6.5 constraints. |

No PRD, architecture, or UX wording change is required because their requirements and gate remain true. If a later review elects to renumber 6.6 or split the approved artifact itself, that would require a separate impact check across all existing references and the approval digest.

## 5. Implementation handoff and success criteria

**Classification:** Moderate backlog reorganization. The Product Owner updates Epic 6 story definitions and tracking; the Developer/architect role splits the story file without loss, creates concise work-story files, and reconciles the normative draft only when that design work is authorized. The named human approver alone can approve the exact AD-13 artifact and authorize 6.6. The approval record cannot be supplied by this proposal or by an assistant.

Success means: (1) 6.5a–6.5c each have a bounded deliverable and backlog status; (2) the active 6.5 story is at most 150 lines and all original historical material remains findable and intact; (3) Epic 6, context and tracker agree on dependencies; (4) the normative artifact remains `UNAPPROVED` until its full content-bound human gate passes; (5) 6.6 remains unauthorized meanwhile. Verify document links, every `BH37-*` owner, historical-content equality, and whitespace with narrow document checks.

## 6. Change-navigation checklist

| Checklist section | Status and finding |
| --- | --- |
| 1. Trigger and evidence | [x] Story 6.5 size, 36 review loops, separate unapproved draft, and ten open findings establish the issue. |
| 2. Epic and future impact | [x] Epic 6 remains viable with three preparation stories and unchanged final gate; no new epic or priority change. |
| 3. Artifact conflicts | [x] PRD, AD-13 and UX remain compatible; Epic 6, context, story files and tracker need alignment. |
| 4. Options | [x] Direct adjustment selected; rollback and MVP scope change evaluated and rejected. |
| 5. Proposal and handoff | [x] Changes, ownership, sequence, risks and success criteria specified above. |
| 6. Final review | [x] Administrator approved the complete proposal in conversation on 2026-09-27. The backlog, story files, and tracker were updated; the separate AD-13 artifact remains unapproved. |

## 7. Approval and execution log

- **Approval:** Administrator said “I approve” to this complete Sprint Change Proposal. This authorizes only the reorganization described here; it is **not** the separate content-bound AD-13 approval and does not authorize Story 6.6.
- **Artifacts updated:** `epics.md`, `epic-6-context.md`, the active Story 6.5 file, three focused child-story files, three exact-content historical files, and `sprint-status.yaml`.
- **Tracker reconciliation:** The pinned sprint-planning parser supplied the three new keys. Its full generator dry run also exposed pre-existing unsafe changes: an orphaned completed Story 2.12 key and a custom Story 4.6 `awaiting-operator` status that it would replace with `backlog`. A targeted parser-derived insertion preserved all existing tracker entries and guarded comments while adding the three child stories as `backlog`.
- **Historical-content proof:** The extracted change-log body is 33,811 bytes (`SHA-256 b59ef707bd23c22454defb304b52ab64fcc15c531a5fb8fa042f8c73ee271981`); triage is 106,474 bytes (`8cad32ebbd9d6c7a400ef365bc958ec945c404461a2dfcc5ab8b47f812165258`); design notes are 184,287 bytes (`f311f92338f889f25623fb71e666bac53b389aa08106bf8314bfebd437e5acd1`). Only separate historical headers were added. The active Story 6.5 file is 68 lines.
- **Validation:** Document structure, whitespace, all ten `BH37-*` rows, new backlog keys, preserved existing statuses, and the normative `UNAPPROVED` receipt passed focused checks. `sprint_plan.py validate --status-file _bmad-output/implementation-artifacts/sprint-status.yaml` still reports `valid: false` for three issues present in `HEAD`: the old `generated` timestamp format, Story 4.6's custom `awaiting-operator` status, and an action item with status `rejected`. They were left untouched by this correction.
- **Handoff:** Product Owner owns backlog sequencing and child-story acceptance; Developer/architect roles own the three specification work items and final Story 6.5 reconciliation. A named human approver owns the separate exact-content AD-13 receipt. Story 6.6 starts only after that receipt validates.
