---
title: 'Story 3.22: Bounded qualification evidence production and admission'
type: 'refactor'
created: '2026-10-21'
status: 'in-review'
approved: '2026-10-21'
approval: 'approve and stop'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'd01c6756c769dcbd8d4d5fff472d07339ebeb22a'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** P1R duplicates complete output and inventories into checkout evidence, driving repository growth.

**Approach:** Store full proof externally through a versioned producer/reader contract, export bounded attempt summaries, and exclude new generated output from Git admission.

## Boundaries & Constraints

**Always:** Preserve source seals, legacy readers, privacy and attempt outcomes. Excerpts: at most 2,048 UTF-8 bytes; aggregate committed attempt metadata: 10 MiB. Overflow exports totals and one external complete-index reference, never split indexes. Store identical payloads once, including repeated case/cleanup output. Bind representation, size/hash, command/status/timing, repository/source/candidate, producer/schema, attempt, locator, retention and disposition. Distinguish raw-byte and replacement-decoded hashes. Readers verify complete bytes and enclosing identity before parsing with bounded I/O. Diagnostics: 30-day retention; acceptance: durable immutable retrieval; holds preserve required bytes. Local storage confers no enduring acceptance.

**Never:** Delete historical evidence, rewrite approvals, repin digests, change selections, publish or infer qualification/readiness. Retirement is 3.23; shared `/pushall` is Hexalith.AI.Tools P5. Preserve concurrent work.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Required result |
| --- | --- | --- |
| New command | Valid external artifact/context | Verified complete text/bytes; bounded excerpt |
| Invalid proof | Missing, corrupt, truncated, wrong-size/attempt, substituted, unavailable or expired unheld artifact | Refuse witness; no excerpt fallback |
| Legacy | Original inline packet | Preserve original hash/validation rules |
| Invalid schema | Unknown version or mixed inline/external form | Reject without legacy fallback |
| Unsafe destination | Checkout-contained or substituted path | Refuse before writing |
| Large attempt | 5,116 commands or large inventory | One external payload per identity; bounded summary/reference |

</frozen-after-approval>

## Code Map

- `tools/p1r_published_executor.py`: `record/save`, signature/restore copies; isolation repeats `command_stdout`.
- `tools/p1r_qualification.py`: reuse validation/source binding; `regular()` is unbounded. Historical history scripts stay immutable.

## Tasks & Acceptance

**Execution:**

- [x] `tools/p1r_evidence_storage.py` — add immutable storage, strict context-bound output resolution, external inventory retention and aggregate summary export.
- [x] `tools/p1r_published_executor.py`, `tools/p1r_isolation.py` — use the contract for journals, repeated case/restore/cleanup outputs and preservation references. Keep source-binding validation authoritative.
- [x] `tools/p1r-published-executor.py`, `tools/p1r_isolation.py` — default full runs externally; preserve `--out` packet meaning, add summary export. Mount only the external root at the same private-container path; update strict grammar and ownership handoff. Preserve socket exclusions/cleanup ownership.
- [x] `tools/p1r_published_qualification.py`, `tools/p1r_check_witnesses.py`, `tools/p1r_qualification_runtime.py`, `tools/p1r_isolation_evidence.py` — resolve all content-dependent output through the common reader; retain exact legacy membership checks.
- [x] `tools/tests/test_p1r_evidence_storage.py` — test matrix, multibyte excerpts, overflow, deduplication, inventories and a real Python subprocess through `Executor.run`; synthetic controls forbid Docker/.NET. Adapt fixtures in `tools/tests/test_p1r_published_executor.py`, `tools/tests/test_p1r_published_qualification.py`, `tools/tests/test_p1r_isolation.py`, `tools/tests/test_p1r_isolation_hardening.py`.
- [x] `.gitignore` — append evidence archive/part/video, trace, command-journal and generated report/result/screenshot-tree exclusions after existing log/package negations; correct ordering comments. Test final-match behavior, ordinary summary JSON and source snapshot baselines.
- [x] `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.BoundedEvidence.cs` — add scanner controls using generated credentials; enroll delivered files in sibling `SecretsProtectionTests.Reconciliation.cs`. Adjust sibling `SecretsProtectionTests.EvidenceRecognition.cs` only for changed exact source blocks; preserve seal exemptions.
- [x] `.github/workflows/p1r-qualification.yml`, `tools/p1r-published-consumers/README.md` — enroll controls; document capture, materialization, retention, budgets and P5.

**Acceptance Criteria:**

- Given 5,116 synthetic commands, when all eligible attempt files are exported, then their combined bytes stay within 10 MiB and excerpts within 2,048 bytes, independently of output volume.
- Given genuine external output, when witnesses/imports/cleanup evaluate it, then complete verified bytes determine results and every matrix rejection stays fail-closed.
- Given historical fixtures, when regressions run, then seal and adjacent-credential rejection remain effective without packet changes.
- Given verification, when recorded, then exact commands, bounded outcomes and unmet dependencies are explicit; authority remains unchanged.

## Implementation Notes

- Session authorization, 2026-10-21: after the missing Story 9.1 record and failing preflight were reported, the user explicitly replied `I authorize`. This authorizes implementation of Story 3.22 in the existing working tree despite that prerequisite. It does not create a GitHub approval receipt, change the actual preflight FAIL, or grant qualification/readiness or publication authority. Preserve all pre-existing concurrent changes.
- Runtime baseline: the existing AppHost is running; `aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive` succeeded before implementation. This session did not start it and does not own its cleanup.

## Spec Change Log

- 2026-10-21: implementation authorization was explicitly granted in the session after the missing external preflight was reported. Frozen intent remains unchanged. Added the boundary-measurement CLI to delivered-file scanner enrollment after review required its reader integration.

## Review Triage Log

Every finding is recorded before grouping. All accepted corrections use existing internal contracts and demonstrated states; no frozen intent, public authority or historical evidence changes are required.

| Finding | Verdict / route | Verified evidence and correction |
| --- | --- | --- |
| Blind 1: restore normalization order | medium / patch | `execute` calls `restore_observations` on new `output_command` responses before normalization; materialize the pending receipt before deriving observations. |
| Blind 2: boundary CLI context | medium / patch | `measure-boundary` still calls the changed materializer without enclosing identity; derive and validate the existing per-receipt context at that entry point. |
| Blind 3: discarded boundary materialization | medium / patch | The first loop validates materialized cases but classification reads original external cases; consume verified cases while preserving the original receipt digest. |
| Blind 4: observe context rollover | medium / patch | `save` changes candidate/inputs after earlier manifests were retained; establish stable capture identity and distinguish generated observation outputs. |
| Blind 5: non-atomic object publication | medium / patch | Final hash paths are visible before writes finish, so concurrent identical capture sees incomplete bytes; publish completed private temporary files atomically. |
| Blind 6: unchecked warm cache | low / patch | Cached references survive payload deletion or root substitution and capture reports success; directly remove or validate the unchecked cache shortcut. Missing witnesses already refuse downstream. |
| Blind 7: missing outer summary outcome | medium / patch | Published CLI exports only the executor directory, so outer cleanup failure and pre-executor failure are omitted; summarize the overall owned attempt and include its isolation proof. |
| Blind 8: public existing store directories | high / patch | Existing 0755 directories are adopted while objects are 0444; enforce private directory access without breaking host/private-container ownership handoff. |
| Blind 9: excerpt-only mixed form | medium / patch | `materialize` omits the external marker `output_excerpt` from dispatch; use the same marker set as the common command reader. |
| Blind 10: substituted summary destination | medium / patch | Summary creation uses unanchored paths after its initial check; use anchored exclusive/no-follow creation and validate destination identity. |
| Blind 11: aggregate materialization expansion | medium / patch | Whole-tree normalization repeatedly reads and decodes identical full outputs with only per-object bounds; reuse verified payloads and enforce an aggregate operation budget. |
| Edge 1: restore finalization | medium / patch | Same verified producer ordering defect as Blind 1; keep this finding independently recorded and share its correction. |
| Edge 2: boundary reader claim | medium / patch | Both the missing caller context and later use of original cases are present; share Blind 2/3 corrections. |
| Edge 3: observation rollover | medium / patch | Earlier None-bound manifests become unreadable after generated inputs are assigned; share Blind 4 correction. |
| Edge 4: concurrent duplicate object | medium / patch | A second writer reads the first writer's unfinished final path; share Blind 5 correction. |
| Edge 5: manifest budget mismatch | medium / patch | A legal 70,001-character argv produces a manifest above the resolver's fixed 64 KiB ceiling; align producer/reader bounds and add a round-trip control. |
| Edge 6: mixed excerpt form | medium / patch | Inline output plus output_excerpt bypasses recursive dispatch; share Blind 9 correction. |
| Edge 7: retain=False privacy | high / patch | External capture ignores the existing suppression flag and writes raw bytes/excerpts; preserve hash-only suppressed output and prohibit using it as proof. |
| Edge 8: failed lane omitted from summary | medium / patch | Executor JSON lacks a passed/exit outcome and can have failed assertions without errors; include bounded receipt/assertion totals and truthful overall outcome. Shares Blind 7's incomplete outcome aggregation. |
| Verification gap 1: fabricated external mount | medium / patch | Removing the SDK external bind still passes two lifecycle controls because inspection is fabricated; assert exact submitted mount arguments and derive fixture inspection from them. |
| Verification other 1: boundary CLI context | medium / patch | Same concrete caller omission as Blind 2; share correction and add a real CLI control. |
| Verification other 2: boundary refusal classification | medium / patch | The layer reproduced inline refused versus external supported classification; share Blind 3 correction and protect equivalent classifications. |
| Parent verification 1: missing cached artifact exception | medium / patch | The full storage suite exposed an unwrapped FileNotFoundError in the new aggregate cache. Map filesystem lookup failure back to InvalidPacket; the original missing-artifact import regression now passes. |
| Parent verification 2: strict external inventory JSON | medium / patch | New inventory parsing used permissive json.loads, unlike the legacy duplicate-member/nonfinite rejection. Restore those checks and require canonical-json representation; focused duplicate/nonfinite/malformed/representation controls pass. |

Survivors group into restore order, boundary context, boundary classification, stable observation context, atomic publication, cache validation, summary outcome aggregation, private storage, mixed schema dispatch, anchored summary export, aggregate materialization bounds, manifest budget parity, output suppression and mount verification. All route to focused patches. The unrelated historical Contracts authority failure remains a separately recorded verification blocker.

## Verification

Final local verification is diagnostic evidence; implementation and review are complete, while story closure remains blocked by the two required gates below. Full records and logs are outside the checkout at `/tmp/story-3-22-final-verification-b0967k59/`; `verification-summary.json` retains exact argv, timing, exits and log hashes. The two initial storage errors were retained, corrected, and superseded by the green final runs below.

| Exact command | Final result | External log |
| --- | --- | --- |
| `python3 -m unittest discover -s tools/tests -p test_p1r_evidence_storage.py` | 30/30 passed | `storage-corrected.log` |
| `python3 -O -m unittest discover -s tools/tests -p test_p1r_evidence_storage.py` | 30/30 passed | `storage-optimized-corrected.log` |
| `python3 -m unittest discover -s tools/tests -p test_p1r_published_executor.py` | 215/215 passed | `executor.log` |
| `python3 -m unittest discover -s tools/tests -p test_p1r_published_qualification.py` | 69/69 passed | `qualification.log` |
| `python3 -m unittest discover -s tools/tests -p test_p1r_isolation.py` | 42/42 passed | `isolation.log` |
| `python3 -m unittest discover -s tools/tests -p test_p1r_isolation_hardening.py` | 29/29 passed | `hardening.log` |
| `python3 -O -m unittest discover -s tools/tests -p test_p1r_isolation_hardening.py` | 29/29 passed | `hardening-optimized.log` |
| `python3 -m unittest discover -s tools/tests -p test_p1r_qualification.py` | 54/54 passed | `preparation.log` |
| `python3 -m unittest discover -s tools/tests -p test_p1r_verifier_corrections.py` | 31/31 passed | `corrections.log` |
| `python3 -m unittest discover -s tools/tests -p test_p1r_snapshot_registration.py` | 3/3 passed | `snapshot-registration.log` |

- Total: **532 Python test executions passed**, including the 5,116-command budget, UTF-8 excerpts, every matrix rejection, legacy controls, real subprocesses, real measurement CLI, restore/observation finalization, output suppression, concurrent publication, destination substitution and mount mutation controls.
- Final Server build: `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --no-restore -m:1`. Exit 0, zero warnings/errors; `server-build-final.json` and `.log`.
- Contracts build: `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --no-restore -m:1`. Passed with zero warnings/errors; `/tmp/story-3-22-contracts-build.log`.
- Focused scanner fallback: `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SecretsProtectionTests*' -method- '*TrackedReusableContent_DoesNotContainUsableSecrets'`. **946 passed, zero failures/skips**, including generated-credential, delivered-file, seal and adjacent-credential controls; `secrets-focused.json` and `.log`.
- Required full scanner: `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SecretsProtectionTests*'`. **Timed out at 1,200 seconds (wrapper exit 124; process stopped after cleanup grace)** with no completion report. The tracked workspace has 41,675 paths and about 6.0 GB of files. `secrets-full.json` and `.log` retain the timeout; the fallback does not establish a full-gate PASS.
- Required Contracts command: `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*ContractsPackageDependencyTests*'`. **64 run, 63 passed, 1 failed**. `SharedConsumerAuthorityValidatorPassesForEveryTrackedMsBuildSurfaceAsync` reports 10,898 errors on 94 retained historical MSBuild paths, none modified by this build. `/tmp/story-3-22-contracts-tests.log` retains the failure. No historical artifacts or gates were weakened.
- `git diff --check` passed. Ignore behavior is checked with `git check-ignore --no-index` in disposable repositories. No staging, commits, pushes, branch switches or dependency/submodule updates were performed in the main repository by this build.
- The starting baseline remains `d01c6756c769dcbd8d4d5fff472d07339ebeb22a`. A concurrent documentation-only commit advanced HEAD to `a23969a2c067c1c8ede1108172c065cfe4f15e33`; its two planning documents and concurrent uncommitted work, including Story 2.13 and `deferred-work.md`, are preserved. It changed no implementation files in this build.
- Review disposition: all three layers completed; all 22 findings were individually triaged and corrected in their shared groups, followed by two parent verification corrections. Frozen intent, historical approvals, source seals and selections remain intact. The spec stays `in-review` and sprint tracking stays `in-progress` until required gates pass.
- Local storage is diagnostic-only, with 256 MiB payload/aggregate materialization and 1 MiB manifest limits. P5 rollout, historical retirement, durable acceptance storage and external Story 9.1 authorization remain separately owned. No qualification/readiness authority is inferred.

**Implementation prerequisite disposition:** The initial Story 9.1 preflight failed because Story 3.22's record is absent; ES-DIAG is inapplicable. After this blocker was reported, the user explicitly authorized proceeding with implementation in this session as recorded above. Proceed under that session authorization; retain the actual preflight FAIL and do not fabricate external authority. Report separately owned P5 rollout truthfully.
