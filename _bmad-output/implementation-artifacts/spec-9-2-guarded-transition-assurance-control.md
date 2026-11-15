---
title: '9.2 Guarded-transition Assurance Control'
type: 'feature'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
review_loop_iteration: 0
planning_baseline: '2dcf708cbab0d4ad0cf72f947d0b0a1444e251a1'
baseline_commit: '2dcf708cbab0d4ad0cf72f947d0b0a1444e251a1'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-9-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 9.2's completed matrix validator checks seal shape offline. It cannot authenticate CI results, attestations or predecessors; ACs 5, 7 and 8 remain deferred.

**Approach:** Implement six dedicated transition workflows and a shared consumer verifier. Separate candidate validation from consumption: the consumer retrieves a completed successful run from GitHub; a running job cannot seal itself.

**Owner decisions (2026-11-15, "apply recommended"):**

- Guard new story-completion claims against NFR1–4, 7, 10–12 and 14–17. Reconcile SM3/G-HIGH-RISK wording; preserve historical labels, but revalidate historical evidence when consumed by a new transition.
- Accept only owner-written GitHub issue/PR conversation comments containing approval JSON bound to role, transition, subject digest and required level. Fetch by comment ID; verify numeric identity, body digest and platform timestamps. Edited/deleted approvals fail. Generate copyable payloads; the owner posts them.
- Apply the 24-hour window from the authenticated latest subject-changing push to `main` throughout this control; preserve AD-26's exact ratification subject. Missing push history blocks verification.
- Deliver one shared verifier and six thin workflows, with a concrete integration test using the existing G-OQ8 validator. Leave Stories 3.19/3.20's absent lifecycle consumers blocked. Story 9.2 remains incomplete wherever actual consumers have not adopted mandatory verification.

## Boundaries & Constraints

**Always:** Bind repository, transition, exact head SHA, workflow bytes, request, matrix/registry, subjects and evidence. Authenticate Actions through platform run/check-suite metadata and humans through numeric registry identities. Compute required/achieved assurance and downstream minimum. Recheck approvals and predecessors during consumption. Missing prerequisites fail closed.

**Never:** Treat live results, uploaded local files or dispatcher identity as seals. Execute caller-provided shell commands. Restore deleted evidence machinery, mint approvals, evaluate G-RUNTIME-PARITY, grant actual release/deployment/removal authority, change GitHub settings, commit or push. Preserve `.github/workflows/ci.yml`, `docs/ci.md` and the completed offline control.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Valid fixture | Exact completed run, authenticated approval after 24 h, passing pinned predecessors | Assurance subcondition passes at computed level | Exit 0 |
| Forged/stale seal | Missing/failed/live/local run, wrong repository/transition/head/workflow/subject/evidence, unauthenticated CI | Reject the attempted transition | Exit 1, stable code |
| Approval | Nonhuman, alias/self-labelled independent, wrong role/level/digest, edited/deleted record, short window | Reject | Exit 1, stable code |
| Predecessor | Wrong validator identity or failing rerun | Void predecessor and reject consumption | Exit 1 |
| Unavailable | Unbound gate, missing business prerequisite, incomplete platform history, API failure | No permission | Exit 1 |
| Bad input | Malformed/unsafe request, path or archive | Reject without traceback or extraction escape | Exit 2 |

</frozen-after-approval>

## Code Map

- `tools/validate-phase-4-gate-risk-matrix.py`: reuse JSON/path helpers, digests, registry/roles and transition IDs. Its seal/attestation checks remain offline.
- `tools/tests/test_phase_4_gate_risk_matrix.py`: Git/CLI fixtures and independent assertions. `tools/tests/release_publication_fixture.py`: transport substitution. `tools/validate-oq8-governing-design.py`: concrete gate integration.
- `_bmad-output/planning-artifacts/architecture.md`: AD-11/12 define assurance/consumption; AD-26 defines push-time ratification.

## Tasks & Acceptance

**Execution:**

- [x] `tools/phase-4-transition.schema.json` — closed request/result contract: transition, head, subject/evidence bindings, required level, approval references and pinned predecessor identities. No committed live evidence records.
- [x] `tools/phase_4_transition.py` and `tools/validate-phase-4-transition.py` — stdlib `evaluate`/`consume` CLI; retrieve run/attempt, check suite, exact workflow blobs and bounded artifacts. Enforce approvals, timestamps and fixed transition prerequisites; missing adapters block. Rerun approved pinned predecessor code/inputs in scratch with fixed argv, bounded output/time and no inherited token. Emit bound JSON with 0/1/2 exits and copyable approval payloads.
- [x] `tools/tests/test_phase_4_transition.py` — real CLI, substituted HTTP transport, positive controls and observed negatives per rejection; all six transitions, pagination/errors, timestamps, existing OQ8 validator integration and blocked missing consumers.
- [x] `.github/workflows/phase-4-{high-risk-gate-pass,high-risk-nfr-story-done,readiness,release-available,production-promoted,consumer-removal}.yml` — six dedicated dispatch workflows; evaluate or consume one attempt, read permissions only, pinned actions, always upload diagnostics.
- [x] `.github/workflows/phase-4-transition-fixtures.yml` — fixtures on PR/main/dispatch; prepare its required-check name without settings mutation. `.gitattributes` and `.editorconfig` — preserve hashed workflow LF bytes.
- [x] `_bmad-output/planning-artifacts/{prd,epics}.md`, `_bmad-output/implementation-artifacts/{epic-9-context.md,sprint-status.yaml}` and this spec — reconcile decisions, adoption limits and verification; preserve blocked gates and completed work.

**Acceptance Criteria:**

- Given each guarded attempt, when consumption runs, then only an authenticated exact-input successful dedicated run can satisfy assurance, and missing business prerequisites still block the transition.
- Given any changed bound input, when consumption reruns, then the old result is rejected.
- Given a failed transition attempt, when its workflow finishes, then its failure affects that attempt and creates no new required failing check on `main`.
- Given the existing OQ8 validator, when the end-to-end fixture runs, then genuine validator success plus synthetic platform approval passes assurance, missing approval fails, and absent lifecycle consumers remain blocked.

## Implementation Notes

- `evaluate --request <json> --result <json>` emits candidate evidence only. `consume --request <same-json> --run-id <id> --attempt <attempt> --result <json>` retrieves the completed successful dedicated evaluation attempt. The six dispatch workflows enforce their own transition; only the separate fixture workflow runs on PR/main. No repository settings change is made. Prepared required-check name: `Phase 4 transition fixtures`.
- The closed contract accepts file digests, numeric comment references and fixed predecessor identities, never executable commands. Requests and diagnostics remain local or workflow artifacts. Every repository input and the verifier/schema/workflow bytes are checked against exact-head Git blobs. Current `main` must still name that exact head.
- Actions authentication checks repository/fork, dispatch event, main head, run/attempt, dedicated workflow identity, successful Actions app check suite and linked attempt job/check run. Artifact selection binds run/attempt, platform digest, expiry and head; bounded ZIP reads reject unsafe paths, links, duplicates, encryption and oversized contents without extraction.
- Human approvals are exact JSON issue/PR conversation comments. Numeric registry IDs and separate role comments govern; aliases and tool identities cannot satisfy independence. API creation/update timestamps and body digests detect edits; missing comments and history fail closed. Diagnostics provide copyable approval payloads; no comment is posted by this implementation.
- The 24-hour clock is the latest authenticated subject-changing push in the connected main activity history, including force pushes and merges. No commit timestamp substitutes for unavailable history. AD-26 uses its exact append-race-plus-section subject and separately verifies the whole-file digest at the approval's issuance head. Outside-subject edits preserve the ratification; current transition results still bind the complete current request and head.
- `oq8-governing-design-v1` is the sole approved fixed adapter. It retrieves code and complete inputs from the predecessor's bound commit, compares approved validator identity and current request inputs, then runs the genuine existing validator and its complete rejection suite with fixed argv in private scratch. Child environment excludes tokens and Python injection; output/time are bounded. The resulting assurance minimum cannot exceed authenticated approvals or fall below the roster requirement.
- Review hardening pins matrix row invariants and the OQ8 business declaration, reads local files within bounds, handles corrupt compressed artifacts, inspects push history incrementally and confirms newly introduced paths through authenticated commit trees. Success checks main again. Linux process groups and child-subreaper support terminate and reap predecessor descendants; predecessor execution fails closed on other platforms. Configuration inputs retain LF bytes and the result contract permits the full 207-input union.
- Stories 3.19/3.20 lifecycle consumers, mandatory story/readiness consumers and other business adapters remain absent and blocked. G-RUNTIME-PARITY is not evaluated. Story 9.2 stays in progress until actual consumers mandate verification. The OQ8 integration demonstrates only this assurance subcondition; no readiness, release, deployment or removal authority is granted.
- This scoped implementation spec is complete. The parent sprint story remains `in-progress` because mandatory adoption and business prerequisites remain incomplete; no commit or push is authorized by this spec.

## Spec Change Log

- 2026-11-15: Implemented approved guarded-transition control and reconciled active SM3/G-HIGH-RISK scope to NFR1–4, 7, 10–12 and 14–17. Historical completion labels remain unchanged; new consumption revalidates evidence.

## Review Triage Log

Review pass 1 (2026-11-15): all findings below reproduce in the implementation or identify a verified missing test. Each correction is internal, adds no public request/CLI surface, and covers the demonstrated state; route: patch. Shared roots BH-06/ECH-02 and BH-10/ECH-01 are fixed together.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | medium | patch | `prepare` checks matrix shape/inventory but never calls row validation. Reclassified rows, unexpected keys and wrong assurance labels can pass; enforce pinned row invariants while permitting truthful unachieved rows. |
| BH-02 | medium | patch | The fixed OQ8 adapter ignores changed matrix command, trigger, evidence identity and pass condition. Pin its existing declared business bindings before accepting adapter success. |
| BH-03 | medium | patch | Corrupt DEFLATE content raises uncaught `zlib.error` from ZIP reads, bypassing controlled diagnostics. Catch decompression failure and observe exit 2 without a traceback. |
| BH-04 | medium | patch | `process.kill()` only kills the immediate predecessor. A spawned descendant survives limit failure and scratch deletion; terminate the execution group and wait for its work. |
| BH-05 | medium | patch | History eagerly exhausts 50 pages before inspecting the newest entry. A long valid history blocks even when the first entry establishes the change; inspect incrementally. |
| BH-06 | low | patch | Each request inventory permits 100 files, but result `binding.inputs` caps the combined inventory at 100. Accepted requests can emit invalid results; align the published result limit with the permitted union. |
| BH-07 | low | patch | `ad26_subject` validates LF endings outside the selected ratification lines. An unrelated CRLF line invalidates unchanged subject bytes, contrary to the exact subject rule; restrict that validation to selected lines. |
| BH-08 | low | patch | `local` checks size only after `read_local` reads the entire file. Bound the read itself while retaining the existing symlink/path safeguards. |
| BH-09 | medium | patch | New exact-byte OQ8 inputs `.editorconfig` and `.gitattributes` lack explicit LF checkout/editor rules. Windows/editor conversions can reject an unchanged checkout; protect both input files. |
| BH-10 | medium | patch | Main is checked only before approval reads and predecessor reruns. It can move before a successful consume returns; check it again at the success boundary. Actual consumers must also compare the bound head when applying their transition. |
| ECH-01 | medium | patch | A probe moved main during the last approval read and still obtained a seal for the previous head. Same root as BH-10. |
| ECH-02 | low | patch | A six-subject/91-evidence request returned PASS with 104 result inputs against a 100-item schema limit. Same root as BH-06. |
| ECH-03 | medium | patch | A file introduced by the authenticated latest push is absent at its before-commit. The verifier mistakes confirmed absence for unavailable history and cannot start its approval window; distinguish authenticated absence from API/history failure. |
| VG-01 | medium | patch | Verified test gap: transport fixtures return direct 200 binaries, replace the redirect handler and never observe headers. Add allowed/unsafe/subsequent redirect controls and credential isolation, including the real handler. |
| VG-02 | medium | patch | Verified test gap: the only process-limit fixture emits oversized output. A quiet hung predecessor has no timeout/reaping test; add one. |

All 15 findings are addressed by the 13 grouped corrections and regression tests. The implementation agent's 13 new controls passed in 126.194 s and six directly affected existing checks passed in 49.042 s. The orchestrator inspected the patched diff and ran the complete final suites below.

## Verification

- **Final post-review verification:** `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_transition.py' -v` — all 38 tests passed in 496.495 s, including every I/O-matrix scenario and the review regressions. No tests skipped.
- **Final existing controls:** `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py'` — 147 passed in 15.750 s; `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py'` — 111 passed in 8.348 s.
- **Final repository checks:** all seven new workflows pass `run-with-temp actionlint`; `bash scripts/check-committed-files.sh` passes (10,555 tracked files); `git diff --check` passes. All 13 untracked files pass separate path, size and whitespace checks. `git diff --exit-code HEAD -- .github/workflows/ci.yml docs/ci.md tools/validate-phase-4-gate-risk-matrix.py` confirms these protected files are unchanged.
- Working-tree implementation is based on `2dcf708cbab0d4ad0cf72f947d0b0a1444e251a1`. No implementation commit or delivery CI link exists yet; no commit, push or settings mutation was performed.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_transition.py'` — 24 tests passed in 327.927 s. Final AD-26 refinement: `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_transition.py' -k test_ad26` — all 3 passed in 40.626 s, including the added historical-issuance case (25 distinct transition tests covered across these runs).
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py'` — all 147 existing controls passed in 14.327 s.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py'` — all 111 existing OQ8 tests passed in 7.574 s; the complete suite also runs inside each successful transition predecessor check.
- `run-with-temp actionlint` on all seven new workflows, `bash scripts/check-committed-files.sh` (10,555 tracked files), and `git diff --check` — passed. New files also pass whitespace checks and stay below 1 MiB. Record future source SHA/CI link only after actual delivery.
- An exploratory adapter run exposed omitted scratch `.editorconfig`/`.gitattributes` dependencies; both are now pinned inputs and the corrected complete suite passes. No verification blocker remains locally. Live GitHub behavior is exercised through HTTP-substituted fixtures; actual delivery CI remains pending.
