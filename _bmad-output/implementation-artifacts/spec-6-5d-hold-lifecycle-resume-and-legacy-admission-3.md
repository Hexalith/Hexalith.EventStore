---
title: 'Story 6.5d: Recovery Review Corrections'
type: 'bugfix'
created: '2026-10-04'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '5e32d07a6ac7a1bf70cc0ca554ea9928145b65f6'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Four decisions and 26 recovery-review corrections remain open. Legacy recovery strands valid ranges; verification does not establish claimed completion.

**Approach:** Close these findings within the approved simplification for Story 6.5 integration.

## Boundaries & Constraints

**Always:** Preserve original frozen intent, D-NFR12/D-CLOSE/D-RESUME, archives, public bytes, events, MessageIds and 54 dispositions. Use approved bounded coverage and actual results. Attribute historical evidence to its revision. Acceptance and focused review precede completion.

**Never:** Change runtime/tests/CI, architecture, dependencies/submodules, Git history, 6.5a–6.5c or AD-13 approval. Authorize 6.6, re-execute domain commands, fabricate authority or claim provider proof.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Legacy | Maximum-width capsule and restart | Original range drains; generations never reset | Bad authority refuses unchanged |
| Resume | Expiry, accepted members, stage limits | Cancel before effects, finish afterward; unresolved-only dispatch | Public reasons and exact refunds |
| Queue | Missing plan, conflicting batch, full shard | Park affected subject; preserve global fairness | No partial grant or consumed refused ticket |
| Held/inventory | Capture/erase race, repair, foreign tenant churn | No orphan; original request; isolated paging | Per-row incidents and closed reasons |

## Approved Decisions

Owner approved all recommendations on 2026-10-04: “do recommended”.

1. **RD1:** Insufficient combined active/staged/retained headroom returns `resume_capacity_hold`; readiness unchanged, no guaranteed resume reserve.
2. **RD2:** Repair binds the failed legacy generation and is consumed on reclaim through the existing owner/recovery intent; later failures need new proof.
3. **RD3:** Ordinary owners use authenticated fence/generation CAS; Operations alone uses its lease. Existing external-effect identity/readback remains required.
4. **RD4:** Eight deterministically addressed shards and one global allocator/header; a full target shard refuses without consuming the ticket. No shard schedulers or flexible placement.

</frozen-after-approval>

## Code Map

Supporting paths below are in `6-5d-simplification/`.

- [Execution record](spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md): original intent/authorization and exact RD/RP requirements; later review supersedes completion claims.
- [Candidate](spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md): D1 fencing, D3 recovery, D6 ledger/queue, D7 capture, D8 registry.
- `verify.py`: reuse CAS/sources/transactions; retain original repair requestHash and compare admitted capture predecessor against erase/recreation.
- `obligations.md`, `known-answers.json`, independent constructors: authoritative caps, retained framing, literal construction.
- Acceptance/mutation scripts and evidence documents: preservation, owning failures, reproducibility.
- Sibling `sprint-status.yaml`, `deferred-work.md`: parent owns 6.5d/D-SPLIT only; preserve RW1.
- `correction-checkpoint.json.gz` and `pre-correction-evidence.tar.gz` preserve this run's initial outside-scope digests/gitlinks and original artifact bytes. Keep the earlier recovery checkpoint/history unchanged; use this run's pinned baseline for ancestry/scope validation. Parent owns execution records and trackers; implementation subagent edits supporting artifacts/candidate only and reports individual RD/RP evidence for parent finalization. Do not spawn further agents.
- Pin the decompressed correction checkpoint SHA-256 `35855fdc2e0ce9a8f1662458bae2544db85f98bfd6c26b0397cc1ecc8a0bee01` and approved correction frozen-block SHA-256 `4fe2612ae350650cf6108f42afdc74f8e77f0450a0b1a1dbe8911535c1b81e88`; no weakening or self-refresh of those pins. Existing original frozen/protected/archive pins remain required. Record the historical checkpoint as historical evidence; it cannot authorize unrelated changes predating this correction run.

## Tasks & Acceptance

**Execution:**

- [x] `verify.py`, candidate — implement approved RD choices and RP1/RP4–RP15/RP18–RP21: lifecycle, shards, authority, outcomes, keys, readiness.
- [x] `verify.py`, `obligations.md`, `known-answers.json`, constructors, `mutations.py`, `reviews/focused-regressions.py` — RP16/RP17/RP22–RP24: owning cases, caps, literals, observed counts.
- [x] `acceptance.py`, evidence documents/manifests/probes/outputs — RP3/RP25/RP26: ancestry and committed/worktree scope, protected hashes, revision-bound evidence. Preserve original checkpoint; snapshot evidence before replacement.
- [x] Execution records, sibling trackers and review artifacts — RP2: individually account for findings; finalize after parent acceptance and three focused layers with the disclosed launch limitation.

**Acceptance Criteria:**

- Given RD/RP findings, when audited, then each has a disposition and no acceptance blocker remains.
- Given refusal/crash, when persisted recovery runs, then identities, charges, monotone owners and discovery survive.
- Given bounded checks/corruptions, when executed, then matrix rows pass and mutations fail their named owning checks.
- Given a committed successor, when acceptance reruns, then ancestry/scope, protected inputs, archives, intent, submodules and independent literals validate.

## Implementation Notes

Planning HEAD: `7d9832ee8a17e9990556760e525df35ea134a7ea`, clean `main`. No irreversible actions; artifact-only footprint. Current epic context/6.5c preserve terminal fences/public bytes.

Approval resume: HEAD is now `5e32d07a6ac7a1bf70cc0ca554ea9928145b65f6`; pre-existing history changed externally between turns. Only this draft is untracked. Preserve the complete current workspace; current correction checkpoints capture this baseline separately from historical recovery evidence. The original 6.5d source baseline/frozen intent remain unchanged.

At review dispatch, the parent implementation audit passed all original seven criteria and four frozen matrix rows; zero-event replay provides the fifth observation. Current verifier independently exits 0 with 1,462 refusals, 23 correction groups, 82 restarts, 67 persisted ordinals and 131 redrives. Six mutations and seven focused removals fail exact owning labels. Preservation/independent construction and four simulated Git refusal cases pass. Completion then awaited the review layers; the final completion and disclosed launch limitation are recorded below.

## Spec Change Log

## Review Triage Log

Parent findings were reproduced and corrected before reviewer dispatch:

| Finding | Verdict / evidence | Route |
| --- | --- | --- |
| PA1 | high: stale capture deleted the exact object now owned and charged by a completed replacement capture. Erase/recreate case now preserves that replacement without stale advancement. | patch — resolved |
| PA2 | high: equal-payload provider generation/fence transfer at transaction admission permitted a stale resume. Store.transaction now compares every changed participant with its captured provider token; separate generation/fence/both cases preserve concurrent state. | patch — resolved |
| PA3 | high: generation-only transfer during external capture advanced the stale owner. Capture now preserves both admitted generation and fence; fresh retry completes the current charged capture. | patch — resolved |
| PA4 | high: generation/fence transfer during a resume effect advanced stale intent. Resume advancement now checks the admitted provider token; byte restart after refusal finishes the original irreversible effect. | patch — resolved |

### Three-layer correction review — individual verdicts

All three layers were dispatched before any result was triaged. Two were fresh context-free reviewers. The third fresh launch twice failed with `agent thread limit reached`; the parent reused the read-only investigator for the verification lens. It had prior investigation context and did not implement the change. No layer was skipped; the launch limitation is explicit rather than claimed as fresh isolation.

| Finding | Verdict / evidence | Route |
| --- | --- | --- |
| CB1 | high: parent reproduced erase/recreate/capture/redrive before stale capture returned: redriving replacement retained its charge but lost its object. Current phase whitelist excludes this valid retained owner. | patch — resolved |
| CB2 | high: parent reproduced both generation-only and fence-only transfer during live-drain write; stale legacy_transition advanced to draining. Its transaction starts after the transfer and compares only payload. | patch — resolved |
| CE1 | high: independently checked the same redriving replacement path; charged object is deleted while valid replacement control remains. Parent probe confirms this exact consequence. | patch — resolved |
| CE2 | high: independently checked admitted ownership transfer during legacy restore; equal payload bypasses current advance comparison. Both token components are reproduced. | patch — resolved |
| CV1 | medium: pre-verified regression gap; a single expiry-preservation removal passes the suite and extends a legal three-page cursor past its original deadline. Current implementation is correct; owning behavior coverage is missing. | patch — resolved |
| CV2 | medium: pre-verified regression gap; modulo-seven placement passes all scenarios but rejects legal ticket 8. A bounded two-cycle feature case is missing. | patch — resolved |
| CV3 | medium: pre-verified regression gap; dropping 256 header precharges passes the suite and underfunds an exact-boundary reserve. Exact-fit/one-byte-under coverage is missing. | patch — resolved |

Each finding received its own verdict before grouping. Five roots remain: CB1/CE1, CB2/CE2, CV1, CV2 and CV3. All have direct corrections within existing intent and public surface. The bounded coverage additions test advertised queue behavior, authorization lifetime and quota readiness; RW1's per-guard deferral remains unchanged. Active simplification authorizes focused correction without reverting working behavior or changing the historical review counter.

## Verification

Run `python3` on the supporting `verify.py`, `acceptance.py`, `mutations.py`, `reviews/focused-regressions.py`; run `git diff --check`. Parent audits persisted states, matrix coverage and independent constructors; no per-guard sweep.

Baseline: verifier exits 0; acceptance exits 1 (`AssertionError: Git history changed`). Preserve pre-fix results; record corrected evidence separately. Provider and integrated approval remain pending.

Approved-run pre-fix commands/results are retained in `6-5d-simplification/pre-correction-checks.json`. At the refreshed baseline, the unchanged historical acceptance script exits 1 earlier: `AssertionError: ('protected recovery input changed', 'src/Hexalith.EventStore.Contracts/Commands/CommandStatusQueryResponse.cs')`. This is a pre-existing change captured by the new outside-scope checkpoint, not an authorized runtime correction. The historical original baseline remains evidence; current preservation uses the approved run baseline.

## Individual recovery correction closure — 2026-10-04

RD1–RD4 implement the owner-approved recommendations. All 26 patch findings are closed against final parent evidence and targeted review closure.

| Finding | Resolution / evidence |
| --- | --- |
| RP1 | Compact eventRoot/range restoration reaches draining with 1,000 maximum-width IDs and byte restart; legacy_cases preserves exact dispatch MessageIds. |
| RP2 | Both execution records and the child sprint entry finalize done only after parent gates and review closure; D-SPLIT evidence/status agree. |
| RP3 | acceptance.py pins current ancestry, committed/worktree/untracked scope, 8,252 protected inputs and original historical hashes; four simulated Git refusals pass. |
| RP4 | 256 addressed headers, shard-local capacity and scope generation/index reads; foreign unreadable entries and churn do not invalidate another scope. |
| RP5 | capsule_make authenticates existing capsule, refuses a pending request and preserves advanced owner state/generation/ordinal on reentry. |
| RP6 | Failure class derives from authenticated current drain evidence; deletion intent recovers after effect and unknown reasons refuse. |
| RP7 | Recovery UTC is mandatory; expired pre-effect preparation cancels and refunds, while irreversible recovery completes the original request. |
| RP8 | Inventory typed-owner validation produces per-row stale incidents for invalid reasons and non-object bodies. |
| RP9 | Failed selected grant uses a disposable transaction image, parks that row and grants an eligible younger subject without partial refund/grant. |
| RP10 | Materialization reads immutable admission operation-plan authority; absent/mismatched authority refuses unchanged. |
| RP11 | Held repair compares original retained route/attempt requestHash, including absent/corrupt repair; re-signed substituted UTC is refused. |
| RP12 | Capture retains admitted generation/fence across write, deletes true late orphan and preserves authenticated replacement through redrive; fresh retries complete. |
| RP13 | 16 KiB scope header fits maximum escaped identifiers; declaration, charge and literal cap agree. |
| RP14 | D6 states queue/256-header/epoch precharge and tenant scope-header formula; exact-fit/one-byte-under cases include recorded overhead. |
| RP15 | D3 and obligations restore all four closed signed eligibility values. |
| RP16 | Named correction cases restore signed-claim, legacy owner/source, route, repair, refund, zero-unresolved and maximum-width refusals. |
| RP17 | Named readback/CAS/bound/dispatch/artifact/handle/page cases inspect real owning state, exact refund and byte restart. |
| RP18 | Closed public count/cursor/admission reasons plus three literal public JSON answers independently construct. |
| RP19 | Any provided cursor validates; empty cursor refuses, three-page continuation retains original expiry and expires at that deadline. |
| RP20 | D8 has the RegistryCapacityHold owner/predicate/re-evaluation/exit row. |
| RP21 | Owners/outcomes/waits use retained publication-charge derivation; account/object locator framing is checked. |
| RP22 | Six corruptions and seven focused removals each require their exact named owning failure after clean controls. |
| RP23 | Five matrix observations register after assertions; 67 ordinals and 131 redrives come from persisted owners. |
| RP24 | Python and separate Node constructor parse normative ceilings; every record/control/public cap metadata is mutated. |
| RP25 | Pinned initial 38-file snapshot preserves old probes/output; attribution narrows historical reproducibility claims and current portable cases supply evidence. |
| RP26 | Old temporary paths are explicitly non-durable; current review uses retained compressed input plus baseline/source hashes and final closure. |

## Completion — 2026-10-04

All four tasks and all four correction acceptance criteria pass. Parent evidence covers the original seven criteria and four matrix rows, plus idle replay. Seven review findings were individually triaged and resolved through five focused corrections; targeted original-reviewer closure and all full parent gates passed. Two initial reviewers were context-free; the verification layer reused a read-only investigator after the tool thread-limit failure, as recorded above. No new deferral was added; RW1 remains unchanged.

Both execution records and the 6.5d sprint entry are done, aligning the approved RP2 tracker repair; the default sprint transition to review stops because done is later. D-SPLIT is resolved for the child-specification prerequisite. Story 6.5 integration, provider/architecture qualification and exact AD-13 approval remain pending; Story 6.6 is not authorized. No stage, commit, branch, push, dependency, submodule, runtime or test-project mutation was made.

Final source SHA-256: `0369d6eb950b64f947490cb3bab6124456ef39e3bb070de280e39bf4d929628d`. [Parent acceptance](6-5d-simplification/parent-acceptance.md), [review closure](6-5d-simplification/reviews/correction-closure.md) and [rerun instructions](6-5d-simplification/RECOVERY-EVIDENCE.md) retain evidence.
