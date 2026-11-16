---
title: '9.2 Mandatory high-risk NFR story-completion guard'
type: 'feature'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
review_loop_iteration: 0
planning_baseline: '5454625052fbbb358923210f83e16d1e8c73925c'
baseline_commit: '5454625052fbbb358923210f83e16d1e8c73925c'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-9-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 9.2 authenticates G-OQ8 availability decisions, but has no mandatory consumer for new high-risk NFR story-completion claims. The shared verifier refuses this transition while completion paths can still write lifecycle labels directly.

**Approach:** Require verification at the completion boundary, deriving story/NFR coverage independently of caller assertions. Guard both spec-frontmatter and tracker completion; refuse governed completion until genuine story-owned NFR evidence adapters exist.

**Owner decisions (2026-11-15, "apply recommended"):** Guard both newly written spec-frontmatter `done` and tracker `done`, deriving primary/supporting NFR coverage from bound PRD/epics. Deliver the mandatory refusal boundary now; separately owned genuine NFR adapters remain future work. This slice prevents bypass but cannot issue a live positive governed completion. A slice label never establishes parent-story or whole-NFR closure.

## Boundaries & Constraints

**Always:** Reuse registry-computed assurance, completed-run authentication, owner-written approvals, 24-hour main-push separation and pinned predecessor reruns. Cover NFR1–NFR4, NFR7, NFR10–NFR12 and NFR14–NFR17. Omitted `nfr_ids` cannot bypass derived coverage. Distinguish slice completion from parent/NFR closure. Preserve historical labels; revalidate newly consumed historical evidence. Bind consumer/control identities. Missing mapping, adapter, approval or seal refuses effective completion. Keep parent 9.2 `in-progress`.

**Never:** Substitute OQ8 availability, saved reports or synthetic evidence for NFR proof. No caller commands, deleted evidence machinery, dependency/submodule changes, approvals, dispatch, settings changes, commits or pushes. Preserve `.github/workflows/ci.yml` and `docs/ci.md`. Runtime parity, readiness and 3.19/3.20 remain separately owned. Live transition failure never blocks main.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Governed completion | Canonical high-risk claim; genuine NFR adapter is not yet delivered | Refuse completion and identify the unavailable adapter | Exit 1, even with valid assurance evidence |
| Missing prerequisite | Governed claim without an adapter, approval or seal | No effective completion; explain missing prerequisite | Exit 1 |
| Coverage bypass | Missing story identity, omitted NFR or conflicting mapping | Reject rather than downgrade the claim | Structured refusal |
| Drift or reuse | Changed claim, source, coverage, workflow, evidence or saved decision | Require fresh authenticated consumption | Exit 1 |
| Historical or ordinary lifecycle | Unchanged historical label or proven unguarded claim | Preserve applicable ordinary lifecycle behavior | Ambiguous coverage refuses |

</frozen-after-approval>

## Code Map

- `tools/phase_4_transition.py`: reuse `Control.consume`, approval authentication and isolated `rerun`; requests lack canonical story identity and `prerequisites` refuses completion.
- `tools/phase_4_gate_decision.py`: fresh-consumption/atomic-report patterns; preserve availability-only scope.
- `.agents/skills/bmad-build/{step-05-present.md,step-oneshot.md,sync-sprint-status.md}`: completion writers; tracker synchronization targets `review`, not `done`.
- `_bmad/custom/bmad-build.toml`: supported overrides. Defaults are overwritten on update; `on_complete` runs too late. Use persistent instructions for a pre-completion guard, never rendered snapshots.
- `_bmad-output/planning-artifacts/{prd,epics}.md`: coverage authority; ambiguity must refuse.

## Tasks & Acceptance

**Execution:**

- [x] `tools/phase_4_story_completion.py`, `tools/validate-phase-4-story-completion.py` — derive canonical claims; enforce the approved boundary with structured diagnostics.
- [x] `tools/{phase_4_transition.py,phase-4-transition.schema.json}` — bind canonical claim/coverage and retain missing-adapter refusal; preserve existing contracts.
- [x] `_bmad/custom/bmad-build.toml` — require the guard before approved completion writes; preserve review synchronization.
- [x] `.github/workflows/{phase-4-high-risk-nfr-story-done.yml,phase-4-transition-fixtures.yml}` — adopt the consumer and fixtures.
- [x] `tools/tests/test_phase_4_story_completion.py` — cover the matrix and actual completion paths, including omitted coverage and missing adapters; reuse `phase_4_transport_fixture.py`.
- [x] This spec, `epic-9-context.md`, `sprint-status.yaml` — record bounded delivery without parent closure.

**Acceptance Criteria:**

- Given a governed claim, when an approved completion path runs, then effective `done` requires fresh authenticated verification.
- Given no genuine adapter, when completion is attempted, then it refuses with the blocker identified.
- Given existing OQ8 behavior, when this slice ships, then its scope and other transition refusals remain accurate.

## Implementation Notes

- The completion CLI is the atomic writer for both spec-frontmatter and tracker `done`; persistent customization applies before step-05-present and step-oneshot writes. Installed skill defaults and rendered snapshots are untouched, and normal tracker synchronization still targets `review`.
- Canonical claims bind spec path, persisted/filename/tracker story identity, target, slice scope and the content-derived coverage digest. Coverage reconciles PRD §11.2 primary/supporting declarations, range-only interiors and supplemental supporting tables with the epics story declaration. Missing or conflicting mappings refuse. Committed planning authority is required for ordinary local completion; coverage edits cannot silently remove a guard.
- Story transition requests require a canonical `story` claim; `nfr_ids` may be omitted there, but a supplied inventory must equal independently derived coverage. Other transitions retain their existing inventory contract. Story bindings include the consumer module/CLI and persistent customization, with a 212-input maximum; other transitions retain 209.
- Dedicated consume mode uses fresh shared `Control.consume`; completed-run authentication, numeric owner identities, unedited approvals, registry-computed assurance, authenticated 24-hour main-push separation and pinned predecessor reruns remain shared. Missing story-owned NFR adapters still refuse after valid assurance, including when the genuine OQ8 predecessor passes. Failed invocations issue structured diagnostics and never write a completion label; saved reports are not input authority.
- Exact historical labels are preserved without new authority, including old specs without a story key. A new consumption cannot use that historical no-op path. New ordinary slice completion cannot close parent Story 9.2; its tracker remains `in-progress`. Genuine NFR adapters, other consumers, runtime parity, readiness and Stories 3.19/3.20 remain separately owned.
- `.github/workflows/ci.yml`, `docs/ci.md`, dependencies, submodules and repository settings are unchanged. No approvals, dispatches, commits or pushes were performed.

## Spec Change Log

- 2026-11-16: Implemented the owner-approved mandatory refusal boundary and local fixture coverage without changing frozen intent or claiming parent/NFR closure.
- 2026-11-16: Applied review corrections to canonical identity/scalar parsing, existing coverage formats, CRLF preservation, final input checks and diagnostic preparation. Added regressions for every reported path, including validation without writing and historical tracker no-ops. Preserved shared authentication, genuine predecessor execution, unavailable-adapter refusal and ordinary slice scope.

## Review Triage Log

- Medium, patched: parent audit found the completion definition missing from the root schema union. Added the completion variant and title; retained fixtures now select every emitted ordinary/refused report through the root union and enforce its closed fields.

Review pass 1 (2026-11-16): all three layers completed. Each finding was checked against its caller and implementation; verification-gap findings also include isolated mutation evidence. Every surviving entry is a direct correction to an existing input, parser or write path, with no new public surface. Duplicate findings remain individually recorded below and share one implementation fix.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1 | high | patch | An uncommitted generic filename has no independent numeric identity; copying a governed spec and changing its key to 9.2 can reach ordinary completion. Require a matching numeric filename for a new spec; preserve established committed generic identities. |
| BH2 | high | patch | `frontmatter` only counts plain keys, so an additional quoted `story_key` is ignored and contradictory YAML identity reaches ordinary completion. Reject alternate-key duplicates and unsupported identity structures. |
| BH3 | medium | patch | Actual Story 2.8 derives extra NFR5 because the supplemental row names Story 2.13 as owner and mentions preserving Story 2.8. Parse the owning follow-up separately from contextual references. |
| BH4 | medium | patch | Canonical Story 3.16 has an unbolded `Requirements coverage:` declaration; the current bold-only parser rejects it. Accept both existing formats. |
| BH5 | medium | patch | Inline comments on scalar status or story key, including installed template syntax, are treated as value bytes and rejected. Support the existing scalar/comment form without accepting aliases or conflicting identities. |
| BH6 | low | patch | A CRLF spec with status as the last frontmatter field receives an extra carriage return from replacement plus the retained delimiter. Preserve its original line terminator. |
| BH7 | medium | patch | An edit injected during temporary-file creation occurs after snapshot validation and is overwritten. Recheck destination and bound inputs after preparation, immediately before replacement. Same root cause as EC2. |
| BH8 | medium | patch | A directory or symlink diagnostic destination fails only after an ordinary label write. Prepare and validate output before lifecycle mutation. Same root cause as EC1. |
| BH9 | high | patch | An external `--result` equal to `--request` is permitted; refusal diagnostics replace the canonical input. Reject input/output collisions, including parser-error reporting. |
| EC1 | medium | patch | Independently reproduced directory/symlink output refusal after the spec changed to done. Fix with BH8. |
| EC2 | medium | patch | Independently reproduced an edit during temporary-file creation being overwritten with PASS. Fix with BH7. |
| VG1 | medium | patch | Making `if write:` unconditional leaves all 21 tests green. Add actual CLI dry-run success for both targets and assert unchanged spec/tracker bytes. |
| VG2 | medium | patch | Removing the historical tracker return leaves all 21 tests green. Add committed unchanged tracker/spec snapshots and assert historical-only output and unchanged bytes. |

All 13 findings are resolved; none are deferred. The parent read the full implementation and complete review-fix delta. The final 32-test completion suite covers every frozen I/O row, both completion targets and the reported regressions, with no skipped tests. The approved frozen intent is unchanged. This bounded spec can finish; the parent tracker stays `in-progress`, and the approved prohibition on commits/pushes overrides generic workflow finalization instructions.

## Verification

- Source SHA at implementation start: `5454625052fbbb358923210f83e16d1e8c73925c`. The shared main advanced externally during verification to observed HEAD `eecc0e4da70a0d64aaeff4fb75cdc06f94eb7563`; the slice edits remain uncommitted. The original approved `baseline_commit` and `planning_baseline` are preserved. No Git mutation was performed by this implementation.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_story_completion.py' -v` — 21 passed, 0 failed. Exercises actual CLI writes and both dedicated workflow modes through HTTP-only fixtures, omitted coverage, missing adapters/approvals/seals, identity relabelling, source/control/workflow drift, saved-result refusal, historical labels, ordinary slices and the 212-input boundary. Customization resolution is exercised through the installed resolver.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py' -v` — 111 passed, 0 failed.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_decision.py' -v` — 16 passed, 0 failed; G-OQ8 availability-only consumers and fresh platform verification retain their existing meaning.
- `run-with-temp actionlint .github/workflows/phase-4-high-risk-nfr-story-done.yml .github/workflows/phase-4-transition-fixtures.yml` — passed.
- `run-with-temp python3 -B _bmad/scripts/resolve_customization.py --project-root /home/administrator/projects/hexalith/eventstore --skill /home/administrator/projects/hexalith/eventstore/.agents/skills/bmad-build --key workflow.persistent_facts` — passed; persistent pre-completion instructions resolve through the supported customization layer.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_transition.py' -v` — 38 passed, 0 failed, including all six transition refusals, authenticated approval/run controls and isolated predecessor/process cleanup regressions.
- Additional local `jsonschema.Draft202012Validator` check under `run-with-temp` — root schema is valid; actual CLI ordinary and refused completion reports both validate against the root contract (2/2). Uses the already-installed local package only; runtime and retained CI fixtures remain stdlib-only.
- `git diff --check` — passed.
- CI link: none for this uncommitted slice. Fixtures use synthetic platform approvals; no live approval or CI delivery is claimed.

### Final orchestrator verification after review fixes (2026-11-16)

All **344 targeted tests passed, zero skipped**, plus the same **32 completion tests under optimized Python**. These final runs supersede the initial 21-test completion evidence above:

- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_story_completion.py' -v` — 32/32, 31.151 s.
- `run-with-temp python3 -O -B -m unittest discover -s tools/tests -p 'test_phase_4_story_completion.py' -v` — 32/32, 31.667 s.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_transition.py' -v` — 38/38, 465.803 s.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_decision.py' -v` — 16/16, 126.334 s.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py' -v` — 147/147, 18.905 s.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py' -v` — 111/111, 9.069 s.
- Workflow `actionlint`, supported customization resolution, root Draft202012 schema validation of actual ordinary/refused reports, and `git diff --check` passed. `bash scripts/check-committed-files.sh` passed for 10,575 tracked files; new files also pass size and whitespace checks. Protected files and the real staging area are unchanged.
- A real-checkout dry run reports ordinary slice PASS for this spec and refuses parent tracker completion with `BUSINESS_PREREQUISITE_MISSING`; neither report grants NFR or parent closure. Finalization uses the actual guarded writer for the spec label.
- Review and verification retain original baseline `5454625052fbbb358923210f83e16d1e8c73925c`; externally advanced HEAD `eecc0e4da70a0d64aaeff4fb75cdc06f94eb7563` has identical committed bytes for every tracked path this slice changes. Unrelated committed work was preserved and excluded from the scoped review diff. No Git mutation was performed.
- Useful local logs and review diffs remain ignored scratch under `evidence/9-2-completion-guard-orchestrator/`; no committed evidence packet or live seal is created. Delivery CI link remains unavailable because this work is uncommitted and no dispatch is authorized.
