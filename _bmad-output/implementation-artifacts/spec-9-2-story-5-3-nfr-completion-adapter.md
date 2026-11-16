---
title: '9.2 Story 5.3 NFR3/NFR4 completion adapter'
type: 'feature'
created: '2026-11-16'
status: 'done'
route: 'dispatch'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
review_loop_iteration: 0
baseline_commit: 'eecc0e4da70a0d64aaeff4fb75cdc06f94eb7563'
context: []
---

<frozen-after-approval reason="owner explicitly requested implementation and local verification of this bounded slice">

## Intent

**Problem:** The mandatory completion boundary has no genuine story-owned NFR adapter. Historical Story 5.3 completion does not prove its declared NFR3/NFR4 scope.

**Approach:** Add the fixed `story-5-3-auth-secrets-v1` adapter and explicit validator. Execute current authentication and repository secret evidence, bind every input and inventory membership, and enable shared evaluation/consumption and both completion targets only when the complete declared coverage is supported. Evidence gaps remain truthful failures.

## Boundaries & Constraints

**Always:** Independently derive NFR3/NFR4 from PRD §11.2 and Story 5.3. Bind only canonical Story 5.3 identity and its gateway, Admin host and sample hosts, never all hosts. Require the explicit validator, fixed commands, positive discovery counts, zero failed/skipped evidence, bounded execution/output, process-tree cleanup and token-free scratch. Bind adapter/test/build code, every root tracked file consumed by the repository scan, inventory membership and build inputs. Preserve authenticated completed-run consumption, registry-computed assurance, 24-hour separation, exact approvals and pinned predecessor reruns. Every completion reruns genuine evidence; saved PASS reports never qualify. Unsupported stories or incomplete coverage refuse.

**Never:** Narrow the denominator to existing green tests; caller commands; treat fixtures as live authority; change historical labels; close parent 9.2 or whole NFRs; change dependencies/submodules, protected CI/docs, settings, approvals or workflows remotely; stage/commit/push. Preserve all initial uncommitted work. Use run-with-temp for disposable verification workspaces. User's instruction to finish authorized implementation supersedes workflow planning confirmation and dirty-tree checkpoints.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Full genuine evidence | Exact supported claim, complete inputs, passing fixed validator, valid approval and completed run | Fresh shared consumption may complete either spec or tracker, slice scope only | Closed content-bound report |
| Historical evidence gaps | Missing scanner capability, missing test lane, failing/skipped/empty evidence | Refuse complete NFR3/NFR4 claim, identify precise gap | Stable diagnostic; no lifecycle write |
| Unsupported claim | Another story, extra/omitted coverage, or unbound spec identity | Refuse | Existing coverage/adapter diagnostic |
| Drift/reuse | Edited/new/deleted input, changed validator/build config, saved report or candidate reuse | Rerun and refuse stale authority | No lifecycle write |
| Assurance failure | Missing/invalid approval, early attestation, wrong/unfinished seal | Refuse before completion | Shared assurance diagnostic |

</frozen-after-approval>

## Evidence inspected before freeze

Story 5.3 review pass 8 explicitly deferred YAML credential sequences, qualified NetworkCredential/ClientSecretCredential constructors, XML CDATA, space-separated CLI secret flags and private octet JWKs. Current SecretsProtectionTests still skips sequence values and lacks the other recognizers. These are mandatory NFR4 gaps, with no waiver. Re-running the existing green tests alone is insufficient. The adapter must expose these gaps through executable capability probes and retain refusal until they are corrected; this slice need not repair the business scanner. Epics permits an explicit symmetric override outside Development while current implementation/spec prohibits Production use; report this discrepancy without weakening the stronger current posture or silently redefining coverage.

## Code Map

- `tools/phase_4_transition.py`: Control.prepare, prerequisites, bounded_process, evaluate/consume; extend fixed adapter routing, preserve OQ8 semantics.
- `tools/phase_4_story_completion.py`: derive/bind and complete; replace unconditional governed refusal only for positively verified complete adapter coverage; recheck all inputs before atomic writes.
- `tools/phase-4-transition.schema.json`: closed binding/result/completion variants.
- `tools/tests/test_phase_4_story_completion.py`, `test_phase_4_transition.py`, `phase_4_transport_fixture.py`: real CLI with synthetic HTTP platform facts, never stub business success as genuine evidence.
- `tests/Hexalith.EventStore.Server.Tests/{Authentication,HealthChecks,Security}`: real gateway/shared JWT and probe tests, all SecretsProtectionTests partials and whole root tracked scan.
- `tests/Hexalith.EventStore.Admin.Server.Host.Tests`: full real host regression lane; Production probes/protected route and token-validation matrix.
- `tests/Hexalith.EventStore.Sample.Tests`, `Hexalith.EventStore.AppHost.Tests`, `Hexalith.EventStore.Admin.UI.Tests`: bound sample API/UI, ephemeral credentials/model and Admin UI token channels.
- `Directory.Build.{props,targets}`, `Directory.Packages.props`, `global.json`, project files, root-declared Builds configuration: build authority; no dependency changes.

## Tasks & Acceptance

- [x] `tools/phase_4_story_5_3.py`, `tools/validate-story-5-3-nfr.py`: fixed adapter/explicit validator with complete source inventory and genuine bounded evidence, including executable capability probes for historical gaps.
- [x] Shared transition, completion writer and schema: route the exact supported coverage through fresh evidence and assurance; keep unsupported/incomplete claims refused.
- [x] `tools/tests/`: exercise both targets, genuine business execution positive controls, missing/failing evidence, membership/source drift, approvals/seals, saved-report reuse and schema. Distinguish genuine business controls from synthetic platform fixtures; record any unavailable full positive evidence honestly.
- [x] This spec and `epic-9-context.md`: exact verification/source identity, limitations and next slice; retain 9.2 in-progress and historical labels.

**Acceptance Criteria:**

- Given complete supported evidence, when evaluate and consume run, then the same fixed validator reruns and its content-bound evidence appears in the closed result contract.
- Given the current scanner gaps, when new completion is attempted, then the complete claim refuses with precise executable gap evidence rather than narrowing coverage.
- Given existing assurance and predecessor tests, when this slice is verified, then their behavior remains intact.

## Implementation Notes

- Exact `story-5-3-auth-secrets-v1` routing binds canonical Story 5.3 slice identity and independently derived NFR3/NFR4. Both targets use fresh shared completed-run consumption; unsupported stories, incomplete inputs/coverage, approvals or seals refuse.
- Complete root membership, content, selected root submodules, pinned Git objects/history, bound text attributes, SDK/Dapr inputs and verified fresh private NuGet archive/extracted bytes are content-bound. Normal checkout EOL conversion is authenticated separately from actual execution bytes; Git replacements and arbitrary filters cannot change inputs. No caller executable/command or saved PASS is accepted.
- Fixed 21-lane plan runs all named evidence with positive XML discovery and zero tolerated failures/skips/not-run; execution is Linux-bound, token-free and limited by fixed time/output budgets. Detached process cleanup remains bounded. Support-safe diagnostics retain method IDs, bound source locations, compiler codes and hashes.
- Every genuine business rerun remains FAIL for known scanner defects, three original fixture findings, and an incomplete integration lane. Successful host/build/package/provider/script lanes do not manufacture complete coverage. The full output and input identities are recorded in the [handoff](story-9-2-story-5-3-nfr-adapter-handoff.md).
- Owner work advanced HEAD externally to `97c02f7bf99a694528a2b15f0b3b0a6e5fb10e9d`; original baseline is preserved. Initial uncommitted guard, historical labels, parent status, protected CI/docs, customization and external work were preserved. No staging, commits, pushes, local dependency/submodule updates, approvals, workflow dispatch or settings changes were performed.


## Spec Change Log

- 2026-11-16: Review identified execution-input and scratch-environment defects. Correct normalization, pinned Git provenance, runtime/tool paths, dependency content binding, diagnostics and schema completeness without changing coverage, accepting caller commands, or weakening failures. KEEP: genuine source-linked probes, complete fixed lanes, whole-tree approval subject, independent coverage, authenticated run consumption, registry levels, predecessor reruns and both atomic completion targets.


## Review Triage Log

All patch entries below are resolved in the final source and focused verification. VG2 remains explicitly deferred, without a successful governed write claim. The parent read the full task-owned delta and review corrections. Additional executable corrections: EC2 installed SDK symlink aliases bind/recheck target bytes; EC3 bounded Git packs use explicit per-object verification instead of demanding unrelated history connectivity; EC4 published NuGet redundant separators normalize safely while canonical collisions/traversal refuse; EC5 the identity constant is named unambiguously so this slice adds no scanner finding. Their regression checks ran and passed, with the final genuine full diagnostic confirming the corrected pipeline.

Review pass 1: all three layers completed against the task-owned delta, preserving the initial uncommitted guard. Every finding is recorded before deduplication. No tool review is human approval. Direct corrections below retain the frozen intent and complete denominator.

| ID | Verdict | Route | Evidence / required correction |
| --- | --- | --- | --- |
| BH1 | high | patch | Private Git index has unborn HEAD, no origins or historical objects; real packaging/integration lanes need authenticated Git provenance. Preserve exact metadata needed by fixed checks in scratch, with no shared Git mutation. |
| BH2 | high | patch | Submodule ls-tree honors local replacement objects, allowing content to diverge from the authenticated gitlink. Disable replacement objects for all evidence Git reads. |
| BH3 | medium | patch | Fixed integration lanes need Dapr runtime/CLI and source-host prerequisites absent from the private environment/workflow. Bind/expose existing required runtime inputs or report exact prerequisites; do not install or update dependencies locally. |
| BH4 | medium | patch | Inner execution loses the selected dotnet PATH; host tools installed outside /usr/bin become unavailable. Preserve a controlled SDK/tool PATH in every child. |
| BH5 | high | patch | Mutable NuGet fallback content is outside the content-bound evidence. Execute from verified private package content and bind consumed dependency bytes, without changing pinned versions. |
| BH6 | medium | patch | Shared evaluation retains coarse lane codes but deletes useful build/test failure evidence. Preserve bounded support-safe diagnostics through the existing result/artifact channel. |
| BH7 | medium | patch | Scanner-capabilities lane says PASS although six mandatory capabilities fail. Retain all probe results and make the lane FAIL whenever a required capability misses. |
| BH8 | medium | patch | Root schema accepts duplicate/invented fixed lane/capability inventories that runtime rejects. Align closed schema with the fixed runtime contract and retain negative schema tests. |
| BH9 | medium | patch | Root schema accepts governed PASS without passing evidence/adapter binding. Require complete business evidence for a governed PASS. |
| BH10 | medium | patch | Retained tests stop before bound-validator/report forwarding. Add genuine bound-validator failure integration; saved synthetic success is forbidden. |
| BH11 | medium | patch | Final verification/source identities were not yet written during the in-progress review snapshot. Complete docs after final verification; preserve the historical partial results separately. |
| EC1 | high | patch | global.json is intentionally checked out CRLF while Git stores LF; raw blob comparison refuses every normal supported checkout. Authenticate Git-normalized content and separately bind actual execution bytes, using bound text attributes and no arbitrary filters. |
| VG1 | medium | patch | Independently confirms BH10: a wrong isolated-validator executable leaves existing fixtures green. Retain a genuine bound failure-path test and exact diagnostic forwarding. |
| VG2 | medium | defer | Final guarded business-source write recheck has no reachable genuine success fixture while real NFR evidence fails. Add this regression before any full governed success is claimed; do not fabricate business PASS. Current writes remain unreachable on genuine incomplete evidence. |


## Verification

The [handoff](story-9-2-story-5-3-nfr-adapter-handoff.md) records every exact command, result, source identity, fixed-lane outcome and remaining blocker. Final targeted suites pass **367/367**, zero skipped, plus **18/18** Draft 2020-12 contract fixtures. Workflow actionlint and `git diff --check` pass. The full frozen-source diagnostic exits **1**, preserving all six missed scanner probes, the three original scan findings and the integration output-limit refusal. All project builds and full Release solution build pass under the documented environment pins. Host/script lanes produce genuine successes; complete governed positive consumption is not established.

No delivery CI, live approval or seal is claimed. Parent Story 9.2 remains in-progress. Full positive write/concurrent-source coverage is explicitly deferred as VG2 until genuine complete evidence can reach it; every current missing prerequisite refuses instead of creating synthetic business success.


Final lifecycle verification: `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-9-2-story-5-3-nfr-completion-adapter.md --target spec --write` returned ordinary-slice PASS and wrote only this bounded slice label. The same command with `--target tracker` (without `--write`) refused parent completion with `BUSINESS_PREREQUISITE_MISSING`; parent 9.2 remains in-progress. `run-with-temp bash scripts/check-committed-files.sh` passed for 10,576 checked files. Protected files and all initially preserved tracker/customization/historical-spec bytes match the task-start snapshots. All verification children were awaited; no CI delivery or live assurance was created.
