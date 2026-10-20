---
title: '6.1 P1R 3.125.1 bounded verifier corrections V1–V3'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
approved: '2026-10-20'
approval_decision: 'explicit user instruction implementing retained proposal V1–V3'
route: 'dispatch'
baseline_commit: '0785238dfe88e5d85b282a2edbdcbd0494d51b34'
tracking_scope: 'verifier follow-up only; no sprint status changes'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-followup-20261020/proposal.json'
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/qualification-contract.md'
---

<frozen-after-approval reason="bounded work explicitly authorized by the user on 2026-10-20">

## Intent

**Problem:** V4's retained fixture sets metadata floor 5 while leaving its existing proof floor 1. Restore labels claim success despite failed assertions. Failed or unequal seeds can enter source/package semantic comparison.

**Approach:** Implement the already proposed V1–V3 in future verifier execution, with focused regressions and truthful failure observations. Environmental controls and preservation reporting are coordinated separately in the same user task.

## Boundaries & Constraints

**Always:** Keep 3.125.1 bound to v3.125.1 and source `55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`; bind verifier provenance separately. Retain every actual failed check and unattempted operation honestly. Keep owner acceptance, conformance, qualification and P1R usability pending/false. Keep Story 6.4 newer verified-origin API qualification separate.

**Never:** Modify historical evidence, frozen intent, archives, dependency declarations, production runtime, shared services or settings. Do not run the full matrix, publish, stage, commit, push or update submodules. Do not introduce retries that erase seed failure. Do not synthesize unattempted probe checks. Do not strip proof or replace its identity/incarnation/head/digest.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Coherent retained positive | Stopped task-owned writer, head 12, snapshot 9, two tenants, existing proof | Set metadata/proof floors 5; preserve all other metadata/proof fields, snapshot and retained event bytes; retain 5..12 | Refuse malformed preconditions |
| Mismatched refusal control | Metadata floor 5, existing proof floor 1 | Preserve original mutation under explicit control; append refuses and domain state stays unchanged | No positive qualification |
| Legacy and invalid inputs | Metadata without proof; invalid floor, erased/wrong identity/head/digest cases | Legacy absence retained; negative semantics unchanged | Never repair arbitrary bad proof |
| Restore response refusal | Instrumentation exits zero but append refused or restart fails | Actual effect/refusal/error and compatible/incompatible/unverified remain independent; required checks reject failure | No inferred append 13 from acceptance alone |
| Seed failure | Either tenant fails, partial checks, missing/unequal coordinates | Keep actual checks/outcomes and exact inventories; block dependent reader/comparison; equivalence cannot pass | No invented unattempted checks |
| Equivalent preconditions | Both branches seeded successfully to identical semantic persisted fixtures | Compare operations only with valid, equal seed prerequisites; dynamic bytes handled explicitly | Different contents refuse equivalence |

</frozen-after-approval>

## Code Map

- `tools/p1r_published_executor.py`: `mutate`, `seed`, `live_case`, `restore_case`, `checkout_case`, `case`, `Measurements`. Existing command retention, process ownership, provider inventory and predicate witnesses remain authoritative.
- `tools/p1r_check_witnesses.py`: independent semantic and predicate checks; preserve historical receipt validation. Extend future validation only if new witnesses require it.
- `tools/tests/test_p1r_published_executor.py`: existing fake executor and genuine snapshot witness fixtures; add bounded regressions here or a focused adjacent test module.
- Retained `diagnosis.json`, `proposal.json`, `inspect_v4.py`: read-only historical causality; never rewrite their counters or receipts.

## Tasks & Acceptance

**Execution:**

- [x] Correct retained mutation and preserve an explicit mismatched-proof variant; validate/pin before and after coordinates and source-proof preservation.
- [x] Derive restore labels and recovery observations from actual responses and all required checks, including persisted append 13, event set, floor, old bytes and fresh restart.
- [x] Capture both tenant seeds and exact persisted preconditions; stop dependent operations when invalid, and require equal successful preconditions for source/package equivalence. Preserve branch observations even on failure.
- [x] Run focused verifier regressions for every matrix row, including failed seed with only two attempted probe checks and unattempted remainder.

**Acceptance Criteria:**

- Given existing proof-bearing metadata, when the retained positive fixture is built, then only retention coordinates change and floor 5 agrees in metadata and proof.
- Given refused append or failed hydration, when restore reports, then no successful compatible restore is reported and strict aggregation remains nonpassing.
- Given any invalid or unequal seed, when comparison runs, then its actual checks remain visible and cannot support equivalence.

## Implementation Notes

The coordinator retains entry hashes and performs bounded runtime discovery controls plus whole-host preservation strategy outside this implementation dispatch. The implementation agent owns verifier code and focused regressions only. Do not execute runtime containers or modify this frozen block. Read-only diff inspection replaces staging under the user's explicit no-stage constraint.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence / route |
| --- | --- | --- |
| blind-1 | medium | Package reader error is overwritten by source outcome; return must represent either branch error. Patch within authorized V1–V3. |
| blind-2 | medium | Recovery derives only three sequence observations and omits secondary refusal and hydration event-count failures. Patch within authorized V1–V3. |
| blind-3 | medium | persisted_preconditions removes proof identity/digest dynamics without validating the complete original proof against each seed. Patch within authorized V1–V3. |
| blind-4 | medium | retained_metadata checks snapshot coordinate 9 but not the finite Counter folded state/envelope. Patch within authorized V1–V3. |
| blind-5 | medium | runtime.stream returns None for multiple snapshots; interval-1000 expected None can therefore accept duplicate unexpected snapshots. Patch within authorized V1–V3. |
| blind-6 | medium | Future seed witnesses are required only by optional fields/check IDs; the bound corrected executor must select the future grammar. Patch within authorized V1–V3. |
| blind-7 | medium | Provider pairs are matched from any successful output; exact branch database/query/command and timing references are missing. Patch within authorized V1–V3. |
| blind-8 | medium | Provider bytes are hashed but inventory coordinates are not independently derived from the bytes. Patch within authorized V1–V3. |
| blind-9 | medium | checkout reader exception keeps prepared state while discarding partial primary/append observations. Patch within authorized V1–V3. |
| blind-10 | medium | Unattempted reader claim uses a flag without checking retained actor commands. Patch within authorized V1–V3. |
| blind-11 | medium | After-action source guard failure leaves prior effect outcome and loses completed observations in case exception handling. Patch within authorized V1–V3. |
| edge-1 | medium | Same demonstrated asymmetric paired-reader error as blind-1. Patch within authorized V1–V3. |
| edge-2 | medium | Proof codecVersion/headSequence/retainedFloor permit bool or float equality in integer preconditions. Patch within authorized V1–V3. |
| edge-3 | medium | Same demonstrated incomplete recovery assertion propagation as blind-2. Patch within authorized V1–V3. |
| verification-1 | medium | Explicit workflow filename enrollment omits the new correction module; verified against p1r-qualification.yml. Patch within authorized V1–V3. |
| verification-2 | medium | Expected fixture digest is generated by the same helper; a seventh-digit truncation mutant passes current fake fixture tests. Patch within authorized V1–V3. |
| verification-3 | medium | Same paired-reader outcome failure as blind-1; verified surrounding return and generic checkout behavior. Patch within authorized V1–V3. |

## Verification

- `python3 -m unittest discover -s tools/tests -p 'test_p1r_published_executor.py'` — all executor regressions pass; run any new focused module explicitly.
- Relevant independent witness/qualification tests if their validation paths change.
- `git diff --check` — no whitespace errors in modified source/tests.

## Final literal-key correction

The final retained V4 byte control exposed a parser that accepted only shortened fake fixture keys. Both executor and independent witness parsing now admit the actual `eventstore||AggregateActor||<actor-id>||<state-key>` layout and require matching actor/state/payload tenant identity. Three new regressions cover literal keys, mismatches and supported simple fixture forms. The failed construction attempt and invalidated runtime attempt remain retained; frozen intent is unchanged.

All 17 review findings were fixed in their grouped verifier patches. No deferred implementation finding remains. This status does not accept qualification, Story 6.4, owner gates or conformance.

## Completion evidence

Final focused suites passed: 184 executor, 31 corrections and 68 published-qualification tests. The unchanged preparation suite passed 54 tests in the isolated verifier checkout after a retained shared-checkout failure; the retained narrow SIGINT rerun passed. Final literal V4 fixture construction reproduced both new controls byte-for-byte. Final bounded live controls passed 70/70 positive restore and 54/54 negative refusal checks; independent v2 recovery validation passed 13/13 and executor cleanup passed 7/7. The verified owned lock reproduced Dapr registration deadlines with tenant A12/B-first-append refusal. Historical V4 causality remains unresolved.

All six runtime invocations and their cleanup proofs are retained. Whole-host preservation failed on attempts 4–6 due to changes outside owned identities; no container was filtered or altered to repair that result. This task performed no staging, commit, push, publication, production-runtime/dependency change or full qualification matrix. Concurrent external commit f6b545cd02632f662136d0b4b8e6952baa7afec3 absorbed earlier task files; it remains untouched. A separately named qualification still requires exclusive host/repository ownership, full required lanes, independent import, conformance and owner decisions. See `evidence/6-1-p1r-31251-followup-20261020/verifier-corrections-v1/README.md`.
