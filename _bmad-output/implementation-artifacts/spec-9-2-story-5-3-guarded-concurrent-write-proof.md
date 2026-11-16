---
title: '9.2 Story 5.3 guarded concurrent-write proof'
type: 'test'
created: '2026-11-16'
status: 'in-progress'
route: 'dispatch'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
review_loop_iteration: 0
baseline_commit: 'a40642d90bcf11dc866c0e1204d32eb78a7c238e'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-9-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** VG2 requires passing Story 5.3 evidence to reach the writer's final inventory check. Existing concurrent-write coverage exercises an ordinary story; the scanner and authentication repairs now permit attempting the governed path.

**Approach:** Add an explicit integration driver exercising both completion targets in disposable repositories, with real Story 5.3 business validation and synthetic GitHub transport facts. For each target, prove an unchanged write succeeds and a concurrent business-source edit refuses completion while preserving that edit.

## Boundaries & Constraints

**Always:** Use `run-with-temp`; keep scratch beneath `$TMPDIR`; await and clean owned children/containers. Execute unchanged lanes, seven scanner probes, selectors and bounds afresh on every evaluation/consumption. Bind complete private source, root gitlinks and runtime inputs. Label synthetic approvals/runs truthfully. Preserve historical labels and parent 9.2 `in-progress`.

**Never:** Fabricate business PASS, reuse saved evidence, bypass guards, weaken coverage/budgets, skip prerequisites, modify shared Git or dependencies/submodules, dispatch workflows, post approvals, change remote settings, or claim live seals, NFR/readiness or parent closure. CI provisioning and other consumers remain separate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Positive, each target | Exact private subject, complete genuine evidence, synthetic valid platform facts | Real consume/write changes only intended lifecycle fields | Any failed prerequisite refuses |
| Concurrent business edit, each target | Same candidate; fresh genuine consumption; source changes during final temporary-file preparation | `SOURCE_MISMATCH`; edit preserved; destination unchanged; temporary file removed | Assert genuine PASS evidence and boundary reached |
| Incomplete environment | Missing SDK, Dapr, Docker or required input | Explicit nonzero diagnostic | No skip or fabricated result |

</frozen-after-approval>

## Code Map

- `tools/phase_4_story_completion.py`: `complete`; authenticates consumption and rechecks inputs before replacement.
- `tools/phase_4_story_5_3.py`: inventory, materialization, Git-object and execution helpers; unchanged business validation.
- `tools/phase_4_transition.py`: `Control.evaluate`/`consume`; approval, run and candidate-equality checks.
- `tools/tests/test_phase_4_story_5_3.py`: private Git-object helpers and refusal controls.
- `tools/tests/test_phase_4_story_completion.py`, `tools/tests/phase_4_transport_fixture.py`: ordinary concurrency and HTTP controls.
- `tests/Hexalith.EventStore.Contracts.Tests/Deployment/ProductionProfileTests.cs`, `tools/tests/test_publication_authority.py`: generated negative-fixture canaries and non-echo assertions; unambiguous credential-reference metadata lookup.
- `deferred-work.md`: VG2. Latest private-authentication spec records 21 passing local lanes; rerun for new evidence.

## Tasks & Acceptance

**Execution:**
- [x] `tools/tests/check_phase_4_story_5_3_guarded_write.py` — add explicit driver with private snapshots and real commit/tree identities. Validate authored fixture messages with pinned commitlint before storing objects.
- [ ] Driver and `tools/tests/phase_4_transport_fixture.py` — bind HTTP facts to private inputs and genuine candidates. Per target: evaluate, consume/write, restore private bytes, then freshly consume with a source edit at final `.story-completion-` preparation. Six business executions; scope injection to that destination/prefix.
- [x] `tools/tests/test_phase_4_story_5_3.py` — fast identity, injection and preservation controls. Keep expensive execution outside ordinary discovery and the 15-minute fixture workflow.
- [x] Repair the six concurrent production-profile test-fixture scanner findings without changing scanner policy, production APIs, malformed-digest controls or assertion coverage; build/run `ProductionProfileTests`, run publication-authority controls, then run the complete scanner class before a fresh proof attempt.
- [x] This spec, `epic-9-context.md` and the VG2 ledger entry — record commands, counts, input identities, CI availability and disposition; retain history.

**Acceptance Criteria:**
- Given each target, when the driver runs, then its positive control writes only intended lifecycle fields after genuine evidence and shared consumption pass against synthetic platform facts.
- Given each positive subject, when business source changes at final temporary preparation during a fresh consumption, then completion returns `SOURCE_MISMATCH`, preserves the edit and original destination, and leaves no completion tempfile.
- Given failed or unavailable business evidence, when verification runs, then the driver fails explicitly and issues no success claim.
- Given local verification, when results are recorded, then distinguish synthetic platform facts, genuine business evidence and unavailable live assurance; parent 9.2 stays in progress.

## Implementation Notes

- Owner approved this spec and requested implementation on 2026-11-16. The frozen block matches the approved draft. The existing baseline is preserved; parent tracker already reads `in-progress` and needs no synchronization write.
- Pre-edit disposable Aspire baseline: `aspire start --isolated --non-interactive --format Json`, `aspire describe --non-interactive`, and `aspire stop --non-interactive` each exited 0. Logs remain ignored scratch under `evidence/guarded-write-parent/baseline-2/`.
- The first two full attempts retained genuine FAIL results: the fixed 180-second gateway build bound blocked both; the first also had one 15-second authentication smoke request timeout. All later passing lanes remain separate evidence and never substitute for the failed prerequisite.
- Run 3 passed the unchanged gateway build in 100.45 seconds, then genuinely executed 360 gateway cases. The reusable-content scanner refused six concurrent production-profile test-fixture locations in the two files above. The parent assessed a minimal test-only prerequisite repair as within the approved boundaries: generate endpoint/unknown-field canaries at runtime and assert non-echo of those actual values; rename the JsonNode metadata accessor `Credential` to `CredentialReference`; generate the Python unknown-field canary and assert its absence from refusal diagnostics. Fixed malformed-digest sentinels, scanner policy, production APIs, selectors and budgets remain unchanged. This clarification does not alter frozen intent or establish positive completion.
- The first fixture repair passed all 130 production-profile and 23 publication-authority cases but retained three scanner ambiguities. The follow-up keeps direct JsonNode credential-reference lookups at the three metadata call sites and removes the helper, while assigning the Python canary directly from `secrets.token_hex(32)` and reading it back for the non-echo assertion. Both C# methods retain their original sentinel assertion as well as the generated-canary assertion, preserving all existing malformed-input coverage.
- Direct Python subscript assignment still entered the scanner's embedded-literal context. The final fixture uses a plain `record.update` mapping containing only the runtime-generated canary, then reads back that value for the same explicit schema-refusal and non-echo assertions. No static canary or scanner exemption is introduced. Targeted failures remain retained under ignored scratch, and full-proof execution still requires fresh complete business evidence.

## Spec Change Log

## Review Triage Log

## Verification

- Baseline source is `a40642d90bcf11dc866c0e1204d32eb78a7c238e`; private fixture messages passed the owning pinned commitlint CLI with zero problems or warnings before new tree/commit storage. All nine root gitlinks and complete required source/runtime inputs are retained in each ignored diagnostic inventory.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_story_5_3.py' -v`: updated suite 35/35 PASS (20.892s); six focused driver controls also pass, including an actual missing-repository-input failure through `main` with nonzero FAIL, no PASS/skip and preserved diagnostic.
- Individual completion discovery through `run-with-temp`: 32/32 PASS (102.119s). Initial transition discovery: 43 cases, 23 PASS/20 FAIL (1266.356s), all failures `PREDECESSOR_LIMIT` under high host contention. The serialized `-k test_real_oq8_evaluation_then_authenticated_consumption` representative later passes 1/1 (22.597s) with unchanged limits; this does not relabel the broad failed run.
- Initial full driver commands use `--logs _bmad-output/implementation-artifacts/evidence/story-9-2-guarded-write/run-1`, `run-2` and `run-3`, all through `run-with-temp`; all exit 1 without a positive candidate or writer invocation. Run 1 has 18/20 executed lane records PASS, 328/329 executed .NET cases PASS, scripts 56/56 and probes 7/7. Run 2 has 19/20 records PASS and all 329 executed .NET cases PASS, scripts 56/56 and probes 7/7. Run 3 executes all 21 records: 20 PASS/1 FAIL, 688/689 .NET cases PASS, scripts 56/56 and probes 7/7; its only gap is the genuine reusable-content scanner finding described above. Missing gateway tests in runs 1–2 remain explicit gaps.
- Private run-1 head/tree: `17f8ce04f5538579a33f6f6a1ec7eeeebda49532` / `cfbd7f1d3a5e92debc6dac985e9e76dc4fcb7f43`; inventory `92741c1b271ca6ea22fa8509f2d0e583b5aa67f6b939ae3ce6a9cd54ad3a623c`. Runs 2–3 head/tree: `95e346e0ba133c0d04e3e13065783cdb194b5f98` / `f0d0b5736ad654b4e38c8ea9396f3ef062e401eb`; inventory `f9a0b00c8490e599c0b47e39dc8a9879049d24e543319550f19e887deeab8a21`. Each binds 19,299 source files, with unchanged submodule-membership digest `047164279aafbc528ed2c6095eb2d7a7a7f21c2e0538125c4b672291bfef36c8`. No failed evidence is reused.
- Fixture prerequisites were checked serially in fresh wrapper-private snapshots with unchanged fixed Release build options and native 180-second bounds. Direct `-class Hexalith.EventStore.Contracts.Tests.Deployment.ProductionProfileTests` passes 130/130; individual publication-authority discovery passes 23/23; final direct `-class Hexalith.EventStore.Server.Tests.Security.SecretsProtectionTests` passes all 299/299. The exact wrapper commands and child invocations are retained in ignored `verify_fixture_repairs_2.py` and `verify_fixture_repairs_4.py` diagnostics beside their logs. Earlier partial scanner refusals and one ad hoc runner tuple error remain historical failures. No production/scanner code, selectors, coverage or budgets were changed.
- `run-with-temp python3 -B tools/tests/check_phase_4_story_5_3_guarded_write.py --logs _bmad-output/implementation-artifacts/evidence/story-9-2-guarded-write/run-4` exits 1 after four fresh business executions. Executions 1–3 each PASS all 21 fixed lanes, 689/689 .NET cases, scripts 56/56 and all seven scanner probes. The real spec write passes exact shared consumption and changes only intended lifecycle bytes; the next fresh consumption reaches final temporary preparation exactly once, returns `SOURCE_MISMATCH`, preserves the business edit and original destination, and removes its completion tempfile. The driver restores and rechecks its owned private inputs between targets. The tracker evaluation then executes all 21 lanes but FAILS `integration-auth:STORY_EVIDENCE_FAILED`: 28/29 authentication cases pass and `KeycloakAuthenticationTests.ConcurrentTenantCommands_EventsRemainIsolated` hits the unchanged 15-second `HttpClient.Timeout` (134.771s cohort). Its other 20 lanes pass, giving 688/689 .NET cases, scripts 56/56 and probes 7/7. Tracker candidate, positive write and concurrent-write boundary are not reached; executions 5–6 remain unproven. Host load rose from 17.20 at launch to approximately 79 during tracker authentication. This is a partial local proof, not a complete VG2 PASS.
- Run-4 private head/tree is `f21ddd9cc0a59019569b0582e872d486fae9ee30` / `62151abf8c04f596b928e8b2111632d197e82951`; all four reports bind inventory `224ea7c7a527e2ca42f0cbf571e34bdc987570282962fbd8147133b68b9bbf85`, 19,299 source files and all nine root gitlinks. All four have identical package digest `89fad1c6c99a09c9673e511b88a398eea917264be6255809a01f8733bf13a50c` and toolchain digest `de0d197f32e813c21f0053d4a3b71eab5ef06481a0e35f4c94a463eb9510b1de`. `spec-outcome.json` retains the observed spec boundary result; failed tracker evidence remains separate. The command and all owned children exited, and wrapper-private scratch was removed.
- Concurrent shared history is preserved separately: `70bf20dceaafd37229236e11d279a5d6ea434759` adds the separately owned 75-line Story 3.19 infrastructure-scope spec; later `0be0c922883d4ea61b18e843e24472162261d934` adds typed Gateway query bindings, its contract attribute/emitter/tests and correlation middleware changes. Approved baseline/frontmatter and ongoing private input identities remain unchanged. The captured private proof does not validate that later production head; a future complete attempt must capture current code afresh.
- Final serial controls against current working source: individual `test_phase_4_story_5_3.py` discovery through `run-with-temp` passes 35/35 in 26.842s; individual completion discovery passes 32/32 in 76.771s; focused transition discovery with `-k test_real_oq8_evaluation_then_authenticated_consumption` passes 1/1 in 38.264s. Exact commands/logs are `final-adapter-controls.log`, `final-completion-controls.log` and `final-transition-representative.log` under the ignored diagnostic root above. No broad transition rerun or additional expensive full attempt is claimed; the original broad failure and incomplete tracker matrix remain open. All serial jobs exited and cleaned their scratch.
- Parent independently read the complete baseline diff including untracked files and separately owned concurrent commits, checked the recorded spec outcome, and verified all five changed code files match the run-4 source inventory. `run-with-temp bash scripts/check-committed-files.sh` passes for 10,651 tracked files; `git diff --check` passes. The approved frozen block is unchanged (SHA-256 `a55352ccdca8e48bde9c20ef27aa957c69d82c9de49fcae95947417b9df0a79b`). The matrix audit leaves both tracker write/race outcomes unproven, so the workflow remains at step-03 acceptance verification; step-04 review and completion have not run.
- No live approval, authenticated seal or delivery CI covers these private local subjects. The expensive driver remains outside ordinary discovery and the 15-minute fixture workflow; CI provisioning and other consumers remain separate. All completed jobs have awaited owned children and removed wrapper-private scratch.

### Remaining validation plan (required outcomes, not observed results)

- A fresh `run-with-temp python3 -B tools/tests/check_phase_4_story_5_3_guarded_write.py --logs <new-ignored-directory>` must prove both positive writes and both concurrent-edit refusals, with six complete passing evidence executions and cleanup. Current source must be captured anew; the tracker paths remain unproven.
- Individual adapter, completion and transition discovery through `run-with-temp` must pass. Observed serial results and any broad-gate blockers are recorded separately above; the historical failed transition run is not relabelled by a focused PASS.
- `git diff --check` must remain clean. Any failed prerequisite must retain its exact diagnostic and prevent a completion claim.
