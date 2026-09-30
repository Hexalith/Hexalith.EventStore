---
title: 'Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission Spec'
type: 'feature'
created: '2026-09-30'
status: 'backlog'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

## Intent

Design the hold, wait, resume, capacity, and legacy-admission mechanisms that Story 6.5 integration had to invent. Deliver them as a reviewed section candidate that Story 6.5 imports exactly as it imported 6.5a–6.5c (owner decision D-SPLIT, 2026-09-30). This is specification work, not runtime implementation or approval of Story 6.6.

## Boundaries & Constraints

**Scope (owner, 2026-09-30).** This story owns the following; Story 6.5 keeps every other `[I-nn]` rule and all imports.
- The integration rules `[I-06]`, `[I-10]`, `[I-12]`, `[I-14]`, `[I-15]`, `[I-16]`, `[I-17]`, `[I-29]`, `[I-30]`, `[I-31]`, `[I-36]`, `[I-37]`, `[I-45]` and `[I-46]`.
- Their codecs and known answers.
- The charge-record codec and counters of the `[I-29]` quota ledger, including the `resume-window` kind.
- Signing purpose `2d`.
- Every exit of a hold or wait state, wherever the hold's record is defined.

**Completeness bar (acceptance, not guidance).** Every rule must meet all of the following:
- **Exits.** Every state it creates has a stated exit, and every indefinite or operator-gated hold is admitted to the hold inventory by predicate.
- **Storage.** Every stored record has a charge or ceiling and a tenant offboarding and erasure rule.
- **Activation.** It names its §10 slice.
- **Codecs.** Every new record or codec has tags and an independently recomputed known answer, checked by an embedded verifier.
- **Sources.** It reads only sources that exist, or records the candidate defines.
- **Closed sets.** Every reason-code or outcome set is closed for every raiser.

**Routing (owner, 2026-09-30).** Fix acceptance-breaking findings now: an ungrounded rule, a boundary with a non-deterministic outcome, overstated verification, or any gap against the completeness bar. Defer only what Story 6.5 can close by citation or as a Story 6.6 verification obligation. Never defer anything that needs a new record, codec, state or exit.

**Always:**
- D-RESUME re-arms publication of the same committed events under the same MessageIds and never re-executes the command.
- Amend imported 6.5a–6.5c rules in place, starting with the 6.5c C5 closure fence (E2-34), rather than contradicting them.
- Preserve history, MessageId, sequence, correlation and public compatibility, or classify each change under NFR12.

**Never:**
- Edit the AD-13 artifact or its `UNAPPROVED` receipt, the 6.5a–6.5c candidates or their `-2` records, `story-6-5-*` history, runtime code, tests or docs.
- Reopen owner decisions D-NFR12, D-CLOSE, D-RESUME or the 6.5a–6.5c owner decisions.
- Authorize Story 6.6 or self-approve.

## Inputs

- The loop-1 `[I-nn]` text in `spec-event-versioning-upcasting.md` at `288a6190` (whole-file SHA-256 `c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715`). It is unapproved draft input, not authority.
- Review pass 1 groups G-A…G-C and G-E…G-J and intent gap G-D, in the Review Triage Log of [Story 6.5](spec-6-5-event-versioning-and-upcasting-spec.md). For G-J, this story owns only the codecs of `[I-12]`, `[I-31]` and `[I-37]`.
- The 54 review-pass-2 findings routed here from [the raw findings](story-6-5-review-pass-2-findings.md). Only E2-34 is verified. Triage must verify every other claim before acting on it.
  - VG2-2, VG2-3, VG2-4, VG2-O1, VG2-O2, VG2-O3.
  - BH2-1 to BH2-10, BH2-16, BH2-18, BH2-19.
  - E2-1 to E2-26, E2-28 to E2-32, E2-34, E2-35, E2-36, E2-38.
- The other 13 findings return to Story 6.5 ([routing table](../planning-artifacts/sprint-change-proposal-2026-09-30.md), §4.5).

## Tasks & Acceptance

- [ ] Triage and verify every routed finding, and re-check each owned pass-1 group against the loop-1 text.
- [ ] Specify each owned mechanism to the completeness bar: publication resume and legacy status-6 resume; hold inventory and redrive; pin-capacity waits and quota ledger; legacy execution-scope claims; the long-stream activation record; status mapping, polling signals and reason codes.
- [ ] Supply codecs, independently recomputed known answers, and `bash` verifier blocks whose count is itself checked.
- [ ] Write an integration handoff. It names the `[I-nn]` text each section replaces, the §8.1, §10.2, §11.5 and §11.6 rows that change, and every imported rule amended in place.

**Acceptance Criteria:**

- Given any hold, wait or legacy record this story defines, when the candidate is checked, then it has an exit, a bounded charge, an erasure rule, an activation slice and a checked codec, and no command, delivery or pin can be stranded.
- Given a resume of a retry-exhausted, drain-limit or reconciled legacy status-6 execution, when the operation runs, then the same committed events publish under the same MessageIds with no command re-execution, and the 6.5c closure fence is amended consistently.
- Given Story 6.5 integration, when this work is reviewed, then its candidate and dispositions import without new integration rules; this story alone authorizes no runtime work.

## Verification

Run the candidate's verifier blocks. Mutating one known answer or one model assertion must fail them. Check the routed finding IDs against their dispositions. Keep the AD-13 receipt `UNAPPROVED`.
