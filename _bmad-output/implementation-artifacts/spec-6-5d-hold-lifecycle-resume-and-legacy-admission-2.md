---
title: 'Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission'
type: 'feature'
created: '2026-09-30'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 6.5 integration invented hold, wait, resume, capacity, and legacy-admission mechanisms that still contain acceptance-breaking lifecycle, evidence, and verification gaps.

**Approach:** Turn the verified pass-1 groups and all 54 routed pass-2 findings into one reviewed 6.5d candidate that Story 6.5 can import without inventing another rule.

## Boundaries & Constraints

**Always:** Replace `[I-06]`, `[I-10]`, `[I-12]`, `[I-14]`–`[I-17]`, `[I-29]`–`[I-31]`, `[I-36]`, `[I-37]`, `[I-45]`, and `[I-46]`; own their codecs, known answers, purpose `2d`, quota charge/counter codecs, and every hold/wait exit. Preserve history and public identity. D-RESUME republishes the same committed events under the same MessageIds and never re-executes a command. Amend imported rules in place, especially C5's operation-namespace fence confirmed by E2-34. Every stored state needs bounded storage, erasure, activation, recovery, and an inventory-visible exit.

**Never:** Edit AD-13 or its `UNAPPROVED` receipt, the 6.5a–6.5c candidates/records, `story-6-5-*` history, runtime code, tests, or product docs. Do not reopen D-NFR12, D-CLOSE, D-RESUME, or D-SPLIT; authorize Story 6.6; self-approve; or claim provider behavior from local models.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Eligible resume | Retry-exhausted or drain-limit hold | Authenticated bounded window re-arms only unresolved publication | Stale evidence conflicts; unavailable evidence holds |
| Legacy resume | Reconciled status-6 execution | Restore the exact stored range and MessageIds without command execution | Missing or contradictory authority follows the approved policy below |
| Capacity wait | Multi-member pin batch exceeds a counter | Durable fair wait with deadlock-free reservation, discovery, and exact refund | Invalid arithmetic/evidence holds without partial admission |
| Held delivery or long stream | Delivery cannot progress, or full replay exceeds a bound | Indexed operator-visible state with a deterministic redrive or capability exit | No silent drop, truncation, or infinite invisible retry |

### Owner Decisions

- `FirstSendMembershipChangedHold` exits only through verified configuration restoration. A configuration or membership revision triggers re-evaluation; the broker supplies a fresh atomic zero-send proof, and the active configuration must satisfy C2's existing byte-identical `ContinueSamePin` conditions. A bounded versioned resolution may then continue the original pin. Manual action may request re-evaluation but cannot override incompatibility. There is no generic operator route mapping or abandonment path.
- A historical legacy status-6 execution without authoritative drain, dead-letter, status, or range evidence is a non-resumable incident. Resume fails closed with a stable evidence-unavailable reason; it never fabricates bytes or automatically re-executes the command. Slice 3 prevents recurrence by durably reading back a bounded resume capsule before legacy drain evidence is removed. The capsule binds tenant, domain, aggregate, tracking identity, exact range, ordered stored-event root and MessageIds, correlation, rejection classification, and cleanup source. Any privileged evidence-import facility is deferred to a separately approved migration story.

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md` -- expand the backlog record into the normative candidate and integration handoff.
- `_bmad-output/implementation-artifacts/story-6-5-review-pass-2-findings.md` -- source list for the 54 dispositions; only E2-34 arrived pre-verified.
- `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` at commit `288a6190` -- unapproved loop-1 source for owned rules, §8.1, §10, and §11 replacements.
- `_bmad-output/implementation-artifacts/spec-6-5c-publication-subscription-and-rollout.md` -- cite-only C1/C2/C5 contracts that 6.5d must amend in its handoff.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `src/Hexalith.EventStore.ApiServer/Controllers/ReplayController.cs`, and `src/Hexalith.EventStore.Operations/` -- inspect-only anchors for shipped drains, replay, dead-letter, actor-index, and telemetry behavior.
- `_bmad-output/implementation-artifacts/{sprint-status.yaml,deferred-work.md}` -- update only the 6.5d tracker row and its D-SPLIT ledger entry.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md` -- verify and disposition every routed finding; specify the owned mechanisms, exact codecs/keys/charges/exits/slices, independently recomputed known answers, mutation-killing verifier blocks, and complete integration handoff.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` -- close only the D-SPLIT 6.5d entry after the candidate satisfies its acceptance bar.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- advance only `6-5d-hold-lifecycle-resume-and-legacy-admission-spec` through the workflow states.

**Acceptance Criteria:**
- Given the pass-1 groups and 54 routed pass-2 findings, when the candidate is audited, then every item has exactly one evidence-backed disposition and every owned rule has an explicit replacement/handoff target.
- Given any state the candidate creates, when its lifecycle is traced, then storage, ceiling/charge, erasure, activation, inventory predicate, re-evaluation owner, and deterministic exit are all defined.
- Given an eligible evidence-required or reconciled legacy resume, when it succeeds, then only the same committed events and MessageIds are re-armed and no command, accepted member, pin, or first response is recreated.
- Given a first-send membership hold, when compatible configuration is restored, then fresh zero-send and exact-byte proofs resolve it to `ContinueSamePin`; incompatible configuration cannot be overridden or abandoned as if publication completed.
- Given a legacy status-6 record, when authoritative evidence is absent, then resume fails closed as a non-resumable incident; new slice-3 failures retain a read-back resume capsule before cleanup.
- Given candidate verification, when one known answer, framing rule, bound, transition, queue invariant, or model assertion is mutated, then the owning verifier fails.
- Given Story 6.5 integration, when 6.5d is imported, then §8.1, §10.2, §11.5, §11.6, and amended imported rules require no newly invented mechanism and AD-13 remains `UNAPPROVED`.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Follow the established two-artifact pattern: this `-2` file records execution and review; the unsuffixed file becomes the `candidate`. Keep all provider/crash vectors explicitly future-facing.

## Verification

**Commands:**
- Run every fenced `bash` verifier in the candidate verbatim -- expected: exact block/family/mutation counts, all fixed known answers, and every mutation rejected by its owning family.
- `python3 scripts/check-deferred-work.py` -- expected: success after the ledger disposition.
- `git diff --check` -- expected: no whitespace errors.
- Run the candidate's protected-path/hash integrity block -- expected: only 6.5d/bookkeeping paths changed and AD-13 still contains `ApprovalEvidence: UNAPPROVED`.
