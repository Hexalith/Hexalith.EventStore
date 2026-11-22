---
title: 'Story 9.2 Phase 4 readiness decision'
type: 'feature'
created: '2026-11-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
baseline_commit: '10c716a4fa885a0c2a30574373e44944788560c8'
context: ['{project-root}/AGENTS.md']
---

<frozen-after-approval reason="Product and Test owner approval of readiness scope">

## Intent

Define the first sealable readiness request and the subsequent authenticated decision required by PRD §11.4. Use the existing dedicated workflow to evaluate the candidate and authenticate the completed attempt. A Product owner can record readiness only after the same approved baseline passes every mandatory gate.

## Boundaries & Constraints

The request binds one clean, approved baseline and exact head, the matrix and owner registry, the readiness workflow and verifier, and the command, subject, evidence, result and applicable approval identities for each of the 15 prerequisites:

G-BASELINE, G-OQ8, G-TENANT, G-STATUS-ID, G-APPEND, G-CLAUSE, G-NFR8, G-COMPAT, G-AUTH-HOSTS, G-RUNTIME-PARITY, G-PUBLICATION-AUTH, G-CONSUMER, G-NFR18, G-NFR-OWNERSHIP and G-MVP-COVERAGE.

Evaluation verifies those inputs and the bound owner approval under the registry level and 24-hour rule. Consumption authenticates the completed successful evaluation and verifies the same inputs again. Compute G-HIGH-RISK from the assurance records, including the sealed readiness attempt, before recording G-READINESS from the 16 predecessor results. Then derive READY from all 17 passing rows at the lowest achieved assurance across the applicable high-risk inputs. The candidate request and seal contain the prerequisite identities and do not depend on the downstream READY record.

On 22 November the user selected staged delivery. Exercise the full decision sequence with synthetic fixtures. Enable live acceptance only through fixed, authenticated owner-provided evidence bindings for all 15 gates. An unimplemented binding returns Reject. Gate owners retain their business validators, including the 9.3 baseline and 9.5 coverage controls. Story 2.14 retains its NFR2 completion adapter. Real missing, unavailable, conflicting, stale or failed evidence yields Reject. Product and Test scope approval is distinct from the registered Product owner's authenticated attestation on an actual decision.

| Scenario | Expected outcome |
| --- | --- |
| Exact approved inputs and authenticated completed attempt | G-HIGH-RISK, G-READINESS and READY are derived in order for the same source set |
| Omitted or duplicated prerequisite, changed subject, lost approval or unavailable evidence | Reject, with the blocking input identified |
| Wrong attempt, unsuccessful run or candidate output alone | Reject at authenticated consumption |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/prd.md` §11.4 and OR1 define the ordered decision and the evidence requirements per gate.
- `tools/phase_4_transition.py` provides `Control.evaluate`, `Control.consume` and pinned predecessor reruns. Before this slice its readiness branch returned `BUSINESS_PREREQUISITE_MISSING`.
- `tools/phase-4-transition.schema.json` defines the closed request and result formats. `.github/workflows/phase-4-readiness.yml` already dispatches evaluate and consume with read-only permissions.
- `tools/validate-phase-4-gate-risk-matrix.py` provides the gate inventory and downstream minimum rules. The offline matrix result provides structural diagnostics.

## Tasks & Acceptance

- [x] Extend `tools/phase-4-transition.schema.json` and `tools/phase_4_transition.py` with a readiness-specific evidence contract and verifier. Resolve each gate through its owning validator and authenticate its required evidence.
- [x] Connect the readiness consume path to the ordered G-HIGH-RISK, G-READINESS and READY decision and return a content-bound decision with per-gate refusals.
- [x] Extend `tools/tests/test_phase_4_transition.py` with a positive control and refusal fixtures for incomplete inventory, source drift, stale evidence, missing approval and failed seal.
- Given a failed readiness input, when evaluating or consuming the request, then the decision identifies the blocked input and preserves Reject.

## Implementation Notes

The dedicated readiness workflow now accepts a request for one clean source tree and 15 named prerequisites. The OQ8 and Tenant consumers authenticate their evidence. Each of the other 13 prerequisites returns a named refusal until its owner supplies a verifier. Consumption checks the completed attempt, reruns the evidence checks and emits the three ordered records. Candidate evaluation supplies no final decision.

On the pre-edit Aspire baseline, the AppHost built and startup exited 2 because the CLI could not write `/home/agent/.aspire`. The resource query reported no running AppHost. The readiness controls run through the Python transition CLI.

## Spec Change Log

## Review Triage Log

| Review finding | Verdict and evidence |
| --- | --- |
| Blind 1 · Aggregate approval identity | High. The aggregate used role names even though the independent roster distinguishes humans by numeric ID. Verify independence against the aggregate author. |
| Blind 2 · Downstream assurance label | Medium. The READY inventory check accepted an unknown declared level. Apply the matrix's downstream validation to the stored declarations before deriving a new decision. |
| Blind 3 · Owner evidence changes during consumption | High. An early owner's attempt or approval could change while later owners were being checked. Recheck mutable references before issuing the decision. |
| Blind 4 · Empty dispatch coordinates | Medium. Argument parsing exited before the workflow could write its Reject report. Exercise empty and nonnumeric inputs at the CLI. |
| Blind 5 · Duplicate nested field | Medium. JSON decoding discarded the parent gate for a duplicate field. Preserve gate attribution in the refusal. |
| Blind 6 · Baseline tree lookup | Medium. The default diagnostic assigned a failed tree lookup to G-READINESS. Associate it with G-BASELINE. |
| Blind 7 · Gate key versus embedded identity | Medium. The result schema validated the inventory key and the nested gate independently. Require them to match. |
| Blind 8 · Assurance on a successful report | Medium. The result schema admitted a READY report with an achieved or downstream level of none. Require achieved assurance on this path. |
| Blind 9 · Two enrolled owner verifiers together | Medium. The full-sequence fixture substituted all owner results. Exercise the two enrolled consumers under the same readiness request. |
| Blind 10 · Implementation account | Low. The Code Map described the pre-implementation refusal in present tense. The Implementation Notes record the two enrolled and 13 deferred bindings. |
| Edge 1 · Thirteen unavailable owner bindings | False as a defect. The staged delivery decision explicitly requires these gates to refuse until an owner provides qualifying evidence. |
| Edge 2 · Artifact-only standard control | Medium. The initial request required a run coordinate even for an artifact approval. The implemented contract supports paired null coordinates for standard controls and checks the exact identity digests. |
| Edge 3 · Other downstream records | Medium. Checking only READY could overlook another malformed downstream record. Apply the matrix validator to every declared record. |
| Edge 4 · Missing seal coordinates | Medium. The workflow supplies empty strings to the CLI when coordinates are omitted. The structured refusal must survive argument conversion. |
| Edge 5 · Superseded owner attempt | High. A later rerun could supersede an earlier gate's qualification during consumption. Confirm current attempt IDs before issuing. |
| Edge 6 · Result under wrong gate key | Medium. The result schema could accept a nested gate ID different from its inventory key. Bind the values in the schema. |
| Verification 1 · OQ8 evidence identity | Medium. The original hosted-job test checked success and missing jobs, but did not assert that replacing a successful job alters the evidence digest and invalidates the prior claim. |
| Verification 2 · Readiness attempt changes during consumption | Medium. A static wrong-attempt fixture passed even without the final current-attempt check. Exercise a rerun between the two lookups. |
| Verification 3 · Gate-specific sealed mismatch | Medium. Existing mutations failed before candidate comparison. Change only one sealed gate record and require a refusal naming that gate. |
| Verification 4 · Dispatch diagnostics | Medium. An invocation with empty seal coordinates exited 2 and omitted the JSON report. Assert the machine-readable Reject on the normal dispatch arguments. |

## Verification

Baseline on `10c716a4fa885a0c2a30574373e44944788560c8`: 147 risk-matrix, 18 gate-decision and 32 story-completion fixtures passed. The live matrix exited 1. Five structural standard controls passed and all 12 high-risk gates lacked achieved assurance.

The reviewed implementation passed 260 local tests on changes based on the recorded source revision:

| Local command or suite | Result |
| --- | --- |
| `python3 -B -m unittest discover -s tools/tests -p test_phase_4_transition.py` | All 62 discovered cases passed in isolated workers, including the 10 readiness cases |
| `python3 -B -m unittest discover -s tools/tests -p test_phase_4_gate_decision.py` | 18 passed |
| `python3 -B -m unittest discover -s tools/tests -p test_phase_4_gate_risk_matrix.py` | 147 passed |
| `python3 -B -m unittest discover -s tools/tests -p test_phase_4_story_completion.py` | 32 passed |
| `python3 -B tools/tests/check_phase_4_readiness_schema.py` | 1 passed, including positive and negative Draft 2020-12 checks |

The combined fixture ran the actual OQ8 and Tenant consumers against synthetic platform responses and supplied synthetic owner results for the remaining 13 gates. It exercised authenticated consumption and late changes to run attempts and comments. The live matrix command, `python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json`, exited 1. Five standard controls passed. All 12 high-risk rows reported achieved assurance of none. G-READINESS reported missing seal and attestation. Parent Story 9.2 remains in progress.
