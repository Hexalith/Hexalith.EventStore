---
title: '9.2 Mandatory assured G-OQ8 gate consumer'
type: 'feature'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
review_loop_iteration: 0
planning_baseline: '97da3e472a31a2106949e37ed196fbb6d543844b'
baseline_commit: '97da3e472a31a2106949e37ed196fbb6d543844b'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-9-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 9.2 authenticates transition evidence, but no actual gate consumer requires that authentication. G-OQ8's existing workflow reports design availability directly; that diagnostic cannot establish an assured high-risk gate decision.

**Approach:** Require authenticated `consume` in a G-OQ8 gate-decision consumer, invoked from the OQ8 and dedicated gate-pass workflows. Preserve offline availability checks. The decision covers governing-design availability only; other consumers keep the parent story incomplete.

## Boundaries & Constraints

**Always:** Reuse the existing closed transition request, genuine OQ8 validator/rejection suite and GitHub authentication. Accept only G-OQ8 with transition `high-risk-gate-pass`. Before issuing PASS, retrieve the completed successful dedicated evaluation attempt and recheck current main, workflow/control bytes, subjects/evidence, approvals, required level and predecessors. Bind the consumer code and decision contract as control inputs. Report computed required/achieved/downstream-minimum assurance and seal coordinates. A saved decision is a report of one invocation; later consumption must verify again through the platform. Preserve owner-written comment approvals and the authenticated-main-push 24-hour window.

**Never:** Accept a local result file as authority, interpret raw availability PASS as assured gate PASS, invent approval or live evidence, or broaden the decision to runtime conformance or NFR closure. No automatic dispatch, comment posting, settings changes, commit or push. Preserve candidate publication, `.github/workflows/ci.yml`, `docs/ci.md` and the offline matrix control. Do not implement separately owned business adapters or G-RUNTIME-PARITY. No readiness, release, deployment, migration or removal permission follows.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Authenticated decision | Exact successful evaluation, genuine OQ8 success and valid approvals | Bound G-OQ8 availability PASS with computed assurance | Exit 0 |
| Availability alone | Ordinary push/PR or dispatch without a decision request | Existing availability diagnostic; no assured decision issued | Existing diagnostic status |
| Seal/approval failure | Missing, failed, wrong or stale run; changed input; edited/missing approval | No effective PASS; preserve named verification failure | Exit 1 |
| Wrong scope | Other gate, transition or missing business adapter | Reject, regardless of generic CI success | Exit 1 or malformed-input exit 2 |
| Bypass/reuse | Caller-supplied result or previously saved decision | Cannot substitute for authenticated consumption | Reject unsupported input |
| Invalid input | Malformed JSON, unsafe path, incomplete run coordinates | Structured failure, no successful decision left behind | Exit 2 |

</frozen-after-approval>

## Code Map

- `tools/phase_4_transition.py`: `Control.consume` authenticates the run/artifact, reruns evaluation and checks main again; `CONTROL_FILES` binds executable/schema identities. `prerequisites` supports only the fixed OQ8 adapter and deliberately refuses other lifecycle consumers.
- `.github/workflows/oq8-governing-design.yml`: separate availability fixtures/live jobs; add assured-decision consumption.
- `.github/workflows/phase-4-high-risk-gate-pass.yml`: preserve candidate evaluation; adopt the same consumer.
- `tools/tests/test_phase_4_transition.py`, `tools/tests/phase_4_transport_fixture.py`: real CLI and genuine OQ8 execution with only HTTP transport substituted. Reuse those fixtures rather than mock successful verification.
- PRD glossary, G-OQ8/G-HIGH-RISK, OR10/13 and Story 9.2: authority boundaries. Story 9.3 owns general tracker reconciliation; 3.19/3.20 own publication/removal consumers.

## Tasks & Acceptance

**Execution:**

- [x] `tools/phase_4_gate_decision.py`, `tools/validate-phase-4-gate-decision.py` — implement the G-OQ8 decision boundary around shared authenticated consumption; emit closed bound decision/diagnostics with controlled 0/1/2 exits.
- [x] `tools/phase-4-transition.schema.json`, `tools/phase_4_transition.py` — define the decision contract and bind new consumer identities; preserve all existing authentication and refusal behavior.
- [x] `.github/workflows/oq8-governing-design.yml` — add manual decision-request/run coordinates and a dedicated assured-decision job with read permissions and always-uploaded diagnostics; ordinary availability jobs retain their triggers and narrow meaning.
- [x] `.github/workflows/phase-4-high-risk-gate-pass.yml` — route consume mode through the actual gate consumer; keep evaluation artifacts compatible with existing authentication.
- [x] `tools/tests/test_phase_4_gate_decision.py`, `tools/tests/test_phase_4_transition.py` — exercise both real workflow entry paths, the I/O matrix, code/workflow drift and genuine OQ8 positive/negative controls. Prove raw diagnostic success cannot issue an assured decision.
- [x] `.github/workflows/phase-4-transition-fixtures.yml` — run new consumer fixtures as part of the existing fixture check; keep failed decision attempts separate from required main checks.
- [x] `_bmad-output/planning-artifacts/{prd,epics}.md`, `_bmad-output/implementation-artifacts/{epic-9-context.md,sprint-status.yaml}` and this spec — record bounded adoption, commands/results and outstanding consumers. Preserve the parent story's `in-progress` state.

**Acceptance Criteria:**

- Given either actual G-OQ8 decision entry point, when no authenticated consumption succeeds, then it cannot publish an effective gate PASS.
- Given ordinary availability CI, when it runs, then it needs no approval and produces no assured gate claim or new blocking live check.
- Given other transitions, when this slice completes, then their existing fail-closed behavior remains, and no broader Story 9.2 completion is claimed.

## Implementation Notes

- Implemented the narrow `tools/validate-phase-4-gate-decision.py` boundary over shared `Control.consume`. Only G-OQ8, `high-risk-gate-pass`, file subjects and no NFR-completion claim are accepted. It requires positive evaluation run/attempt coordinates and accepts the existing closed transition request through `--request` or `PHASE4_REQUEST_JSON` with `--request-env`.
- `hexalith.eventstore.phase-4-gate-decision.v1` is a closed availability-only report retaining exact input/request/subject/workflow bindings, computed required/achieved/downstream-minimum assurance, authenticated approval/predecessor facts and platform seal coordinates. Failure clears achieved assurance and seal; malformed and unsupported invocations emit structured exit-2 diagnostics and replace prior reports atomically. Saved decisions/candidates and local seal arguments are unsupported authority inputs.
- Added both consumer files to `CONTROL_FILES`; the existing schema is already bound. The maximum combined binding inventory is now 209. OQ8 workflow bytes remain bound by its existing subject/predecessor inventory; the dedicated evaluation workflow remains the only accepted seal source.
- Ordinary OQ8 fixture/live availability jobs retain their triggers and diagnostic behavior. Only manual dispatch with any decision input activates `assured-decision`, using read permissions and always-uploaded diagnostics. Dedicated gate-pass evaluation still publishes compatible candidate artifacts; consume mode now issues the same actual gate decision. The existing fixture job timeout is 15 minutes to accommodate the additional genuine consumer suite.
- The genuine OQ8 rejection suite previously required exactly two workflow jobs and no manual inputs. Its exact contract now includes the optional dispatch inputs and manual-only job while retaining exact assertions for both existing availability jobs. Consumer tests execute the actual shell steps from both workflows, substituting only HTTP transport; they also prove genuine validator and fixture failures cannot PASS.
- PRD, epics, Epic 9 context and tracker record this bounded adoption. Parent Story 9.2 stays `in-progress`: other mandatory consumers, missing business adapters, Stories 3.19/3.20 and G-RUNTIME-PARITY remain outstanding. No live assurance, NFR closure, readiness, release, deployment, migration or removal authority is claimed.
- Review fixes disable CLI abbreviations, verify the actual workflow environment mappings, cover file/environment size boundaries and document the complete operator path in `docs/architecture/oq8/README.md`. The README request example executes locally without approvals or platform mutations.
- The orchestrator read the full implementation diff and the complete review-fix delta. Every frozen I/O scenario is exercised by the final consumer suite: authenticated decisions and independent labels; diagnostic-only availability/manual guards; seal/approval/input failures; wrong scope; saved-result rejection; malformed, oversized and symbolic inputs. All 16 covering tests ran and passed without skips. Existing other-transition refusal is covered by the 38-test transition suite.
- This scoped spec is complete. The approved requirement to keep parent Story 9.2 `in-progress` and prohibit commits/pushes takes precedence over the workflow's generic final tracker/commit instructions; no parent lifecycle advancement or Git mutation is performed.

## Spec Change Log

- 2026-11-15: Implemented the approved G-OQ8 consumer slice. Frozen intent and scope are unchanged. No dispatch, approval posting, settings mutation, commit or push was performed.

## Review Triage Log

Review pass 1 (2026-11-15): Edge Case Hunter returned no findings; Verification Gap Reviewer found no verification gaps. Blind Hunter findings are individually triaged below. Patch entries are internal corrections with no new public surface.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | low | patch | Argparse accepts abbreviated options while error-path destination recovery recognizes only `--result`. `--res existing.json --unknown` therefore cannot replace the earlier report. Disable abbreviations and test the exact supported argument contract. |
| BH-02 | low | reject | An independent JSON Schema probe confirms that unequal assurance labels validate structurally. Every actual consumer instead calls `Control.consume`, which recomputes registry approval and downstream minimum and cannot emit those labels; saved reports are explicitly never authority. Additional semantic schema guards for deliberately altered, unusable reports add complexity without protecting an everyday consumer path. |
| BH-03 | low | reject | The same probe confirms empty approval/predecessor arrays validate structurally. `check_approvals` requires all evaluator roles and `prerequisites` requires the genuine OQ8 predecessor before either entry point can PASS. Extra schema branches for hand-written reports do not authenticate them; later consumption always rechecks platform facts. |
| BH-04 | low | reject | The probe confirms a fabricated `foo`-only inventory validates structurally. `Control.prepare` binds all control/workflow inputs and the fixed OQ8 adapter checks its complete input inventory. No issued decision can omit them, and a saved decision is rejected as an input. Duplicating the complete runtime inventory in structural schema guards is disproportionate for this unreachable output. |
| BH-05 | low | reject | The tests check the emitted contract and independent control inventory rather than run a general JSON Schema engine. The orchestrator validated a genuine positive against the published schema successfully. Adding a new CI validator dependency solely to reject unauthenticated hand-written reports is disproportionate; their platform rejection is already exercised. |
| BH-06 | low | reject | A local caller would need a concurrent actor replacing its request path after the regular-file check. Actual workflow callers use environment requests; changed parsed requests still need matching authenticated approvals and sealed evaluation. Descriptor-relative traversal/nonblocking-open machinery for this exceptional local race is disproportionate. |
| BH-07 | low | reject | A concurrent actor could replace an output ancestor after its symlink check. Output directories are caller-selected reports, never trusted input or authority; workflow scratch is private to the attempt. Descriptor-relative output traversal adds complexity for an exceptional local race without changing the authentication boundary. |
| BH-08 | low | patch | Both shell routes are executed, but the dedicated gate-pass input-to-environment mappings lack exact assertions. Add mapping checks for both routes and retain the existing no-decision dispatch guard assertions. |
| BH-09 | low | patch | File and environment size guards exist, but their boundaries are not exercised by the new wrapper suite. Add oversized-input refusals with structured diagnostics, replacement of a previous report and zero platform requests. |
| BH-10 | low | patch | The OQ8 README documents only diagnostic availability. Add a complete operator example for the new manual decision path, including request preparation, owner approval payloads, evaluation coordinates and both consumer invocations. No live action is performed. |

All four patch entries are resolved. Six focused review-fix tests passed before the orchestrator's complete final run. No findings were deferred; the other consumers already excluded from this slice remain outstanding in the parent story.

## Verification

- Final post-review orchestrator verification: **312 tests passed, zero skipped**. Each command below ran on the final implementation with verbose method-level output retained.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_decision.py' -v` — 16/16 pass (126.340 s), including both workflow shell entry points, independent-roster propagation, exact CLI options, size boundaries, environment mappings and symbolic path refusals.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_transition.py' -v` — 38/38 pass (464.826 s), preserving existing authentication, AD-26, token isolation and other-transition refusal behavior.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py' -v` — 147/147 pass (13.338 s).
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py' -v` — 111/111 pass (7.310 s).
- `run-with-temp actionlint .github/workflows/oq8-governing-design.yml .github/workflows/phase-4-high-risk-gate-pass.yml .github/workflows/phase-4-transition-fixtures.yml` — exit 0.
- `bash scripts/check-committed-files.sh` — exit 0 (10,568 tracked files); the staged-file mode against a private temporary index also passes (15 checked), with the real index unchanged. Final new-file size/whitespace checks and `git diff --check` pass.
- Protected `.github/workflows/ci.yml`, `docs/ci.md`, offline matrix validator, matrix and registry are unchanged from the baseline. The real staging area remains unchanged.
- Source baseline: `97da3e472a31a2106949e37ed196fbb6d543844b` plus this uncommitted implementation. Delivery source SHA/CI link are not available; no commit, push or dispatch is authorized. Synthetic approvals prove test behavior only.
- Local verification logs are retained outside disposable job workspaces under ignored `_bmad-output/implementation-artifacts/evidence/9-2-mandatory-oq8-gate-consumer/`; these are scratch logs, never a seal or committed evidence packet.
