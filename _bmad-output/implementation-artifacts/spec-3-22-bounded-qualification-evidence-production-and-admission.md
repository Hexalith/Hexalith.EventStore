---
title: 'Story 3.22: Bounded qualification evidence production and admission'
type: 'refactor'
created: '2026-10-21'
status: 'done'
approved: '2026-10-21'
approval: 'approve and stop'
route: 'dispatch'
review_loop_iteration: 1
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
- [x] Resume: profile and remove the full-scanner bottleneck with dynamically balanced, bounded concurrency and equivalent assignment discovery; retain complete tracked-file coverage and credential rejection controls.
- [x] Resume: distinguish exactly enrolled frozen MSBuild captures from active build inputs; enforce unchanged membership/bytes, reject activation, retain adjacent-file package authority, and transport the full inventory without OS argument truncation.
- [x] Continuation: `SecretsProtectionTests` source recognition — decode structurally bound source captures and nested tool-output JSON before applying language-aware rules; retain complete scanning of every scalar, strict bounds, seal failures, original line reporting and adjacent credential rejection. Recognize `.cs.source` language snapshots without excluding their contents. Add bounded Go runtime-value and Python keyword/dictionary/interpolated-URI recognition only where the complete context proves nonliteral material; injection controls must reject literal substitutions and adjacent credentials. Keep the locked broker qualifier and all frozen evidence byte-identical.
- [x] Continuation: `WorkloadBindingAuthorityTests.QueryAuthority.cs`, `NativeSourceMetadataReaderTests.cs` — generate current credential fixtures at runtime. Preserve the frozen wrong-secret fixture through a narrowly proven negative-test context (complete wrong-secret branch and denial assertion), with controls for changed literals, removed/changed assertions, unrelated contexts and adjacent credentials. Preserve existing general deterministic-literal rejection and all original seals; do not add directory/hash allowlists.
- [x] Continuation: `ContractsPackageDependencyTests.FrozenEvidence.cs` — carry MSBuild task Properties/RemoveProperties into traversed bridge projects, inspect wildcard task inputs and item transforms, and retain each imported target's owning MSBuildThisFileDirectory. Add focused reproductions of all four reported activation bypasses. Keep the exact frozen inventory, supported mode/framework evaluation and structured argument transport.
- [x] Continuation: `tools/p1r_isolation.py` — mount a fresh invocation-owned evidence directory rather than an arbitrary output parent. Preserve `--out` packet identity, same-path host/container storage, strict command grammar, socket exclusion and owned cleanup. Add a submitted-mount control using a parent with an unrelated private file.
- [x] Continuation: `tools/p1r_evidence_storage.py` — select only the root command journal for attempt totals; reject unsupported representations and nonboolean holds before object publication; bound file enumeration/index accumulation before retaining excessive metadata. Add round-trip and overflow controls, including a nested commands.json.
- [x] Continuation: `tools/tests/test_p1r_published_qualification.py` — cover the production default-verifier branch through package creation and independent validation with only subprocess execution stubbed. Verify external signature references, enclosing context and refusal after payload deletion.
- [x] Continuation: `tools/p1r-published-consumers/README.md` — update the runnable repair example and containment guidance for admitted external attempt directories.
- [x] Required full secrets gate passes after final review corrections: **982 passed**, zero failures/skips, 546.739 seconds, no timeout. All previous failures remain preserved below and are superseded by this closing result.

- [x] Final review corrections: close all ten demonstrated boundary defects and subsequent parent findings below; add focused regressions, preserve all passing controls and exact history, then repeat affected full gates.

**Acceptance Criteria:**

- Given 5,116 synthetic commands, when all eligible attempt files are exported, then their combined bytes stay within 10 MiB and excerpts within 2,048 bytes, independently of output volume.
- Given genuine external output, when witnesses/imports/cleanup evaluate it, then complete verified bytes determine results and every matrix rejection stays fail-closed.
- Given historical fixtures, when regressions run, then seal and adjacent-credential rejection remain effective without packet changes.
- Given verification, when recorded, then exact commands, bounded outcomes and unmet dependencies are explicit; authority remains unchanged.

## Implementation Notes

- Session authorization, 2026-10-21: after the missing Story 9.1 record and failing preflight were reported, the user explicitly replied `I authorize`. This authorizes implementation of Story 3.22 in the existing working tree despite that prerequisite. It does not create a GitHub approval receipt, change the actual preflight FAIL, or grant qualification/readiness or publication authority. Preserve all pre-existing concurrent changes.
- Runtime baseline: the existing AppHost is running; `aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive` succeeded before implementation. This session did not start it and does not own its cleanup.

## Spec Change Log

- 2026-10-21 continuation, review iteration 1: executable MSBuild edge reproductions, arbitrary parent mount exposure and unbounded index enumeration exposed missing non-frozen constraints. Added concrete continuation tasks and context-sensitive scanner work needed by the existing full-gate acceptance criterion. Avoid silently skipped build destinations, broad parent mounts and unconditional source exemptions. KEEP all preservation constraints in Continuation design and preservation; frozen intent is unchanged.

- 2026-10-21: implementation authorization was explicitly granted in the session after the missing external preflight was reported. Frozen intent remains unchanged. Added the boundary-measurement CLI to delivered-file scanner enrollment after review required its reader integration.

## Continuation design and preservation

The resumed review found missing executable-edge and external-directory constraints in the non-frozen plan. Re-derive those internal seams and the still-incomplete scanner recognition using the continuation tasks above. The inherited implementation is already committed at `b6d1807035fba2e485c8587ac3581d669d1036e5`; there are no changes from this continuation to revert. Preserve that existing work rather than reverting user-owned commits. KEEP: all existing storage/reader contracts, deduplication, budgets, legacy validation, privacy suppression, full scanner inventory/grammar, exact historical bytes and seals, frozen MSBuild enrollment, passing Python/Contracts controls and concurrent Story 2.13 work. All diagnostics and complete logs remain outside the checkout. Keep implementation changes unstaged through verification; the explicitly invoked build workflow requires a final local commit after the repository commit-message preflight. No remote Git mutation, dependency update, external authority or historical retirement is authorized.

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
| Resume blind 1: build-mode activation | medium / patch | The guard forces Debug/source; Release-only imports and package-mode imports bypass it. Evaluate supported modes and add conditional controls. |
| Resume blind 2: inner framework activation | medium / patch | Outer-only evaluation omits TargetFramework-conditioned imports. Evaluate every declared inner framework and preserve reference properties. |
| Resume blind 3: transitive bridge activation | medium / patch | Only immediate ProjectReference paths are checked. Traverse referenced projects with cycle detection and effective reference properties. |
| Resume blind 4: MSBuild task activation | medium / patch | Executable MSBuild task Projects attributes are absent from evaluated ProjectReference items. Inspect executable preprocessed XML as well as evaluated edges. |
| Resume blind 5: comments mistaken for imports | medium / patch | Any absolute comment line is currently treated as an import. Recognize the preprocessor's actual boundary records and test ordinary comments. |
| Resume blind 6: reverse alias activation | medium / patch | Resolving a symlink before checking containment loses its archived lexical identity. Check both lexical and physical paths and reject archive link substitutions. |
| Resume edge 1: assignment backtracking | high / patch | Colon backtracking loses a credential-named assignment and misclassifies a noncredential prefix. Preserve full-grammar backtracking and classify the final captured name. |
| Resume edge 2: conditional activation | medium / patch | Independently reproduced Release/package-mode imports; shares resume blind 1 correction. |
| Resume edge 3: bridge activation | medium / patch | Independently reproduced a bridge project importing frozen bytes; shares resume blind 3 correction. |
| Resume edge 4: target-added reference | medium / patch | A BeforeTargets item introduces a frozen ProjectReference after evaluation. Inspect executable preprocessed target bodies; add a control. |
| Resume verification gap 1: missing CI-mode controls | medium / patch | Existing activation fixtures are unconditional or permanently false; the normal CI lane uses Release/packages. Add mode-conditioned imports and references, using evaluation only (all local builds remain Debug/source). |
| Resume verification other 1: colon capture mismatch | high / patch | A credential-named colon assignment ending in an equals sign reproduces the same prefilter bypass as resume edge 1. Add both reproductions and a noncredential negative control. |
| Resume follow-up blind 1: composed property/item task paths | medium / patch | Separate fragment checks miss an evidence path assembled from properties or item expressions. Expand complete expressions with a bounded operation budget and cover both forms. |
| Resume follow-up blind 2: task bridge and alias destinations | medium / patch | Literal task destinations do not receive graph or physical-path checks. Traverse existing task destinations and reject lexical/physical archive identities; add bridge and alias controls. |
| Resume parent verification: OS argument ceiling | medium / patch | The complete exact inventory exceeds the Linux per-argument limit. Transport the same deduplicated paths as structured JSON through both PowerShell process boundaries, outside the checkout; the full authority gate verifies enforcement and membership. |

Survivors group into restore order, boundary context, boundary classification, stable observation context, atomic publication, cache validation, summary outcome aggregation, private storage, mixed schema dispatch, anchored summary export, aggregate materialization bounds, manifest budget parity, output suppression and mount verification. All route to focused patches. The prior historical Contracts authority failure is superseded by the passing resumed full gate recorded below.


| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| Continuation blind 1: task property activation | medium / bad_spec | Task traversal copies parent properties and discards the MSBuild task Properties/RemoveProperties; independently reproduced conditional bridge activation. Add explicit effective task-property handling to the continuation plan. |
| Continuation blind 2: wildcard task activation | medium / bad_spec | The executable-input walk skips wildcard paths; a matching child can import frozen evidence. Require bounded expansion and inspect every admitted destination. |
| Continuation blind 3: item-transform activation | medium / bad_spec | Unresolved item transforms are silently skipped although the executable task reaches archived projects. Require evaluated transform handling or an explicit refusal. |
| Continuation blind 4: imported target directory | medium / bad_spec | MSBuildThisFileDirectory is overwritten with the top-level project directory, losing imported-target provenance. Preserve defining-file context and cover the alias reproduction. |
| Continuation blind 5: incomplete secrets gate | false / reject | The spec already records the gate failure and leaves both story and sprint incomplete; no successful result is claimed. Passing this existing acceptance gate remains required work. |
| Continuation blind 6: arbitrary external parent mount | high / bad_spec | initialize selects output.parent and sdk_arguments binds that complete tree; existing admission rejects sockets/mounts but does not isolate unrelated home-directory credentials. Require a fresh owned directory. |
| Continuation blind 7: nested command journal | medium / patch | export_attempt selects every basename commands.json and replaces totals. Restrict totals to the root journal. |
| Continuation blind 8: producer/reader field mismatch | medium / patch | retain accepts arbitrary representation and Store accepts nonboolean hold; resolve rejects both. Validate producer fields before publishing proof. |
| Continuation blind 9: inventory work before bounds | medium / bad_spec | sorted(rglob) and the files list grow before the final index storage budget runs. Enforce incremental enumeration and index bounds. |
| Continuation blind 10: stale runnable example | medium / patch | The repair example selects checkout-contained output while current admission rejects it. Correct the example and its containment explanation. |
| Continuation edge 1: task property activation | medium / bad_spec | Independent reflection reproduction passes the guard while SelectFrozen=true activates Captured.props. Shares blind 1's root cause. |
| Continuation verification gap 1: production signature lane | medium / patch | Package-lane tests inject synthetic_verifier, while default_verifier externalizes output; direct reader tests cannot catch lost enclosing context in package_outcome. Add the filed production-path round-trip and missing-payload control. |

| Continuation parent 1: escaped credential role | high / patch | Decoding then masking an arbitrary escaped JSON string could lose its credential field name. Keep credential-valued outer strings visible; the nested-scalar rejection control passes. |
| Continuation parent 2: dictionary literal operands | high / patch | General dictionary interpolation could admit a literal password prefix/suffix. Restrict recognition to complete broker endpoint/password-slot grammar; literal and adjacency controls pass. |
| Continuation parent 3: negative proof markers | medium / patch | Substring markers could recognize a commented or string-contained denial. Bind the actual executable method/branch/denial and reject both forged contexts. |
| Continuation parent 4: skipped transform metadata | medium / patch | Unresolved custom transforms still reached continue while a comment claimed refusal. Explicit custom-transform refusal and a positive frozen-activation control now run. |
| Continuation parent 5: frozen repair-driver example | medium / patch | The frozen driver's line 25 still requires checkout-contained attempts. Retire its runnable example and show current external synthetic verification without editing that driver. |
| Continuation parent 6: diff language substitution | medium / patch | A hunk marker alone initially granted Python context to unrelated output. Derive framed language from paths; pathless fragments receive only exact runtime credential-slot recognition. Shell/unknown/forged/mixed controls pass. |
| Continuation parent 7: pre-decoding exact seals | medium / patch | Two inherited scanner controls failed because transformed outer JSON reached original exact-seal recognition. Apply original recognizers before decoding; all 971 focused controls then passed. |
| Continuation parent 8: ordinary Aspire item filter | medium / patch | The full Contracts gate ran 102 tests with one failure on WithMetadataValue in the real AppHost. Preserve a conservative superset of its underlying references and effective batching metadata, with benign and frozen-bridge controls; retain the failed full-run log. |
| Continuation parent 9: metadata casing | medium / patch | A case-insensitive match fed a case-sensitive built-in metadata switch, so lowercase fullpath could erase a bridge. Normalize metadata names and cover a lowercase frozen bridge. |
| Continuation parent 10: decoded checkout boolean | medium / patch | The full secrets gate ran 978 tests with one finding in a newly decoded historical workflow's persist-credentials false option. Recognize only a complete bounded checkout-with boolean control; generated literal/suffix, unrelated-context and adjacent-credential controls protect rejection. Retain the failed full-run log. |
| Continuation parent 11: original seal mutation | medium / patch | The first checkout-boolean correction let a changed, already sealed workflow bypass its original byte requirement. Exclude original sealed-file identities from that new semantic recognition. All 978 scanner controls then passed, including the original exact-byte mutation controls; retain the stopped failing full-run record. |
| Continuation parent 12: unknown transform erasure | medium / patch | Replacing unknown transform metadata with an empty string could lose a nested-only bridge. Preserve unresolved metadata and refuse it; retain explicit OriginalItemSpec child/default/attribute overrides before conservative identity fallback. Both nested-bridge controls and all 41 focused frozen-input controls pass. |

### Resumed independent review triage (2026-10-21)

All review layers were launched before collection. Verification Gap returned no gaps. Edge Case returned the placement finding. Blind Hunter computed the required ten-finding floor and reported ten concrete cases, then its final response failed with a content-filter error. The parent completed that report from saved local fixtures and source inspection, independently rerunning all ten read-only guard probes: each exits 0 with PROBE_PASS while its existing benign local execution marker is present. Exact commands, complete logs and hashes are retained in `/tmp/story322-resume-audit-x_45xigy/reviews/parent-verification/results.json`; fixture sources are under `/home/administrator/.cache/story322-blind-x3dcvvhx/`. No original evidence was executed or changed. The interrupted reviewer is not recorded as a normal completed report.

| Finding | Verdict / route | Verified evidence and correction |
| --- | --- | --- |
| External selected checkout placement | medium / patch | Executor and controller admission check tooling ROOT before creating output, but independently selected source/Builds roots can contain full packets and the object store; create_packet already checks all three. The edge reviewer traced constructor writes and parent inspection confirms the omitted source guards. |
| Exec working directory | medium / patch | The workingdirectory fixture is accepted by the current guard while its local synthetic target runs from the Exec working directory; synthesized edges resolve against the parent project instead. |
| Comma-separated Exec properties | medium / patch | The comma-properties fixture is accepted because SelectFrozen=true,Other=false is treated as one value; the child process applies two properties and activates the local frozen fixture. |
| Target-time property output | medium / patch | The task-property-output fixture is accepted because ChildProject is queried as empty before its task assigns the project path; the empty expansion drops the executable edge. |
| Forged preprocessor owner comment | medium / patch | The boundary-comment-spoof fixture is accepted because a source comment can impersonate an SDK boundary, assigning a custom Exec task an exempt SDK owner. |
| Target-time ProjectReference configuration | medium / patch | The targettime-reference-configuration fixture is accepted because raw target-added references lose SetConfiguration while only evaluated references carry that protocol into traversal. |
| Imported MSBuildThisFileFullPath | medium / patch | The import-fullpath fixture is accepted because the defining-file binder handles Directory but FullPath is evaluated against the top-level project; the real imported task selects a different child. |
| Exec environment properties | medium / patch | The exec-environment fixture is accepted because EnvironmentVariables is discarded from the synthesized child context; the real child imports the local frozen fixture under that environment. |
| dotnet exec MSBuild.dll | medium / patch | The msbuild-dll fixture is accepted because all dotnet exec calls are treated as non-build helper execution, including the installed MSBuild.dll entry point. |
| Semicolon item transform inputs | medium / patch | The transform-semicolon-items fixture is accepted because filename metadata is applied to the complete semicolon string, losing the earlier Bridge.csproj alternative. |
| Directory-valued build destination | medium / patch | The exec-directory fixture is accepted because the selected destination is a directory and File.Exists is false; the real dotnet build resolves the project inside it. |
| Parent: conflicting standalone source roots | medium / patch | With outer source A, child source B and output inside B, the actual standalone CLI exits 2 but creates B/attempt, the object store and isolation.json before source mismatch refusal. A Docker-blocked parent reproduction is retained in reviews/standalone-conflicting-roots.json. Preserve every known source root for both early admission and rejected-run summary fallback. |

Each finding is individually graded before grouping. All expose demonstrated states in existing private destination/graph helpers and the existing fail-closed intent. Their smallest fixes add no public surface and remain local corrections, so they route to focused patches rather than changing frozen intent. Keep all prior passing controls, exact frozen inventory, historical bytes and authority limits. The changed Contracts guard requires fresh focused and full Contracts verification; the unchanged scanner implementation retains its prior full-gate evidence plus a fresh delivered-file scan.

## Resumed verification work

- The user explicitly resumed the same implementation authorization and requested resolution of both verification blockers. No Story 9.1 approval receipt is created; the actual prior preflight result and authority boundaries remain unchanged.
- The prior scanner bottleneck was repeated quoted-value regex parsing and CPU work serialized across approximately 6 GB of tracked inputs. External per-file and per-stage profiling is retained at `/tmp/story-3-22-resumed-vcxqr3gl/`. Scanner concurrency is dynamically balanced and bounded to four workers over the unchanged complete inventory. Assignment discovery filters noncredential candidates before value parsing and preserves the original grammar, including overlapping matches and colon backtracking.
- Frozen MSBuild evidence requires exact original path/byte enrollment, separately from active input discovery. An initial two-collection inventory cleared the 94 reported raw-policy failures but exposed additional retained historical projects that also require frozen classification. The final exact enrollment binds 984 original MSBuild paths/bytes, with old seals independently unchanged. Existing seals remain authoritative and unchanged; no historical hashes are repinned.
- First resumed focused scanner run: 947/948 passed; one randomized invalid-version control generated a syntactically valid NuGet version (`8+` followed by generated material). A separate diagnostic compared original and optimized matcher captures and retained the colliding generated fixture. The fixture now produces an unambiguously invalid version; the metadata allowance is unchanged.
- Read-only Aspire baseline: `aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive` exited 0 and reported that no AppHost is currently running. These scanner and packaging checks do not require runtime orchestration; this session owns no AppHost cleanup.

## Prior verification (retained record)

This is the prior implementation's diagnostic record, retained without altering its logs or outcomes. The resumed results below supersede its timeout and Contracts failure; they do not establish a full secrets-gate PASS. Prior records and logs remain outside the checkout at `/tmp/story-3-22-final-verification-b0967k59/`; `verification-summary.json` retains exact argv, timing, exits and log hashes. The two initial storage errors were retained, corrected, and superseded by the green Python runs below.

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

## Resumed verification results

All full diagnostics remain outside the checkout at `/tmp/story-3-22-resumed-vcxqr3gl/`. Its `verification-summary.json` records exact argv, exits, timings and log hashes. All local builds used Debug and local project references. Active-input inspection evaluates Release/package conditions without building or restoring that mode.

| Exact command | Closing result | External log |
| --- | --- | --- |
| `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --no-restore -m:1` | Exit 0; zero warnings/errors | `server-build-final.log` |
| `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --no-restore -m:1` | Exit 0; zero warnings/errors | `contracts-build-transport.log` |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*ContractsPackageDependencyTests*'` | Exit 0; **96 passed**, zero failures/skips; 378.564 seconds | `contracts-full-final.log` |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*ContractsPackageDependencyTests*' -method '*FrozenMsBuild*' -method '*AuthorizedRootPackageAdapter*'` | Exit 0; **35 passed**, zero failures/skips | `contracts-final-controls.log` |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SecretsProtectionTests*'` | Exit 1; **950 run, 949 passed, 1 failed**, zero skips; **453.517 seconds**, no timeout; 168 findings in 125 files | `secrets-full-closing.log` |

The same ten Python commands in the prior table were rerun: **532 executions passed**. Their current exact commands and individual results are in `python-verification-summary.json`; logs use the corresponding `python-` prefix. The full scanner's 949 passing controls include original seal/credential rejection, assignment-grammar equivalence, large-journal detection and dynamic partition coverage. A separate original-versus-optimized comparison over the earlier run's 126 reported files produced **3,028 matching assignments and zero differences**; `scanner-residual-equivalence-final.json` records that result. The frozen MSBuild controls reject edits, additions, removals, renames, link substitution, active imports/references, mode/framework activation, transitive bridges and composed task paths. Adjacent new tracked and untracked package overrides still fail authority validation. Structured argument transport preserves the entire exact exclusion array at both process boundaries.

The scanner timeout is corrected, but the required gate remains failed. Independent review classified the 168 unchanged findings as 164 source/runtime-context recognitions and four deterministic credential fixtures. There are 110 findings in retained P1R 3.12.51 source-bearing receipts/history, 30 in Story 2.13, 21 in Story 3.21, three in current test fixtures, and four in the current broker qualifier. Reported source contexts include escaped Python source, C# source snapshots, Go runtime values and diagnostic structures. They require bounded context recognition with positive credential-injection controls; whole-source masking or directory/hash allowlisting would not satisfy the gate's rejection requirement.

The four deterministic fixtures comprise three current test assignments and one frozen negative-test snapshot. Preserving the scanner's existing literal rejection and the snapshot's immutable bytes prevents declaring that finding cleared. A separately justified fixture disposition or context-aware rejection proof is still required; none is fabricated here. The broker qualifier is also bound by the existing runtime lock, so changing its source merely to avoid a match would alter qualification identity. That source and its pin remain unchanged. Full path/line findings and classification are external in `scanner-findings-final.json` and `scanner-residual-classification.json`; no credential values are reproduced here.

All 35,581 tracked historical evidence files retain their original raw hashes and membership. The new 984-member MSBuild inventory binds original paths/bytes at resumed HEAD `f4d29263bd9d22dd432a6a464ee145e57eff3acd`; previous seals are untouched. Frozen intent remains byte-identical. No historical evidence, approvals or pins were rewritten; no dependency updates, staging, commits, pushes or Story 3.23 work occurred.

All resumed review findings were individually triaged above and corrected. Story status remains **in-review**, and sprint status remains **in-progress**, because the full secrets gate is still failed. The prior session authorization continues to authorize this implementation; the absent external Story 9.1 receipt and actual preflight FAIL remain unchanged. Diagnostics confer no qualification, readiness, publication or enduring acceptance authority.


## Continuation verification results

Current full logs and exact command records are outside the checkout at `/tmp/story-3-22-completion-22sche_s/`. Each run record binds its argv, working directory, timing, exit, timeout disposition and complete-log SHA-256. Prior failures above and their original logs remain preserved. These results concern local implementation verification only.

- Both test projects build in Debug with local project references using the previously recorded exact build commands, `--no-restore -m:1`; both exit 0 with zero warnings/errors. The last Server build is `server-build-seal-final.json`; final Contracts build evidence is `/tmp/story-3-22-unresolved-transform-build-complete.log`.
- The same ten exact Python commands recorded above pass **540 test executions**: storage 33 in each of normal and optimized mode; executor 215; published qualification 70; isolation 43; hardening 29 in each mode; preparation 54; corrections 31; registration 3. `python-verification-summary.json` binds every command and complete log.
- The affected credential fixtures pass **101 tests**, zero failures/skips, with `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*WorkloadBindingAuthorityTests*' -class '*NativeSourceMetadataReaderTests*'`; `credential-fixtures.json` records the exact command.
- All **978 scanner controls** pass, zero failures/skips, with the full SecretsProtectionTests class filter excluding only `TrackedReusableContent_DoesNotContainUsableSecrets`; `secrets-all-controls-final.json` records the exact command. This includes original seals, transformed scalar/diff contents, adjacent credentials and generated-literal substitutions.
- An intermediate full Contracts run passes **107 tests** in 563.217 seconds (`contracts-full-corrected.json`). The final guard additionally passes **41 focused controls** in 33.283 seconds (`/tmp/story-3-22-unresolved-transform-focused-complete.log`). These were intermediate controls; the completed full-gate results below supersede their pending state.
- `matrix-audit.json` maps all six frozen matrix rows to 13 covering storage tests that ran in both normal and optimized suites. It includes complete raw/replacement-decoded proof, UTF-8 bounds, unavailable/corrupt/expired/context-substituted refusal, legacy and mixed-schema rules, destination substitution, and the 5,116-command/large-inventory budget.
- `historical-after-final.json` verifies all **35,581** tracked historical evidence files retain exact raw bytes and membership. Frozen intent is byte-identical. No historical evidence, approvals, selections, digest pins, dependency versions or Git history were changed by this continuation.

- Required full secrets gate: `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SecretsProtectionTests*'` exits 0 with **979 passed**, zero failures/skips, no timeout; `secrets-full-final.json` and `.log` retain the complete record. This supersedes the historical 168 findings and the continuation's decoded-workflow/byte-seal failures without changing their retained evidence.
- Required full Contracts gate: `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*ContractsPackageDependencyTests*'` exits 0 with **109 passed**, zero failures/skips, in 535.635 seconds, no timeout; `contracts-full-final.json` and `.log` retain the complete record. Actual AppHost evaluation also passes both supported modes without executing targets (`/tmp/story-3-22-unresolved-transform-apphost-complete.log`).
- All implementation tasks and six frozen matrix rows are verified. Required full gates pass; story enters final independent review. The inherited Story 9.1 preflight FAIL and absent external receipt remain factual records under the existing session authorization. P5 rollout, Story 3.23 retirement and durable acceptance storage remain separately owned.


## Final review triage

All three required layers completed before triage. Every filed outcome below was verified against the cited implementation and complete external reproductions in `/tmp/story322-blind-probe-ra3jftco/confirmed-reproductions.json`; all 14 reproduction log hashes were checked. The four executable-edge controls each report guard success followed by actual execution of synthetic frozen input. No shared assemblies or historical evidence were changed by the reviewers.

| Finding | Verdict / route | Verified evidence and smallest correction |
| --- | --- | --- |
| Final blind 1: property-function task destination | medium / patch | The unresolved-expression branch skips a custom Projects property function; the actual synthetic target reaches the frozen import. Refuse unproven custom executable destinations using the existing defining-file classification. |
| Final blind 2: task-item AdditionalProperties | medium / patch | Raw item metadata is enrolled but FrozenTaskProperties reads only task attributes; actual MSBuild applies the item's AdditionalProperties. Carry that existing effective-property metadata into the existing child variants. |
| Final blind 3: escaped task properties | medium / patch | The bridge sees literal tr%75e in inspection while actual MSBuild decodes true. Decode the existing task-property protocol correctly or explicitly refuse unsupported escaped controls before traversal. |
| Final blind 4: direct Exec build invocation | medium / patch | The executable-edge selection omits Exec Command; the direct archived project invocation passes inspection and executes. Apply the existing frozen-path/refusal guard to this demonstrated direct build route without introducing a shell interpreter. |
| Final blind 5: escaped JSON credential name | high / patch | The JSON reader decodes the property name but never scans that decoded name with its scalar value; the plain-key positive control rejects the same value. Preserve the decoded credential role and original outer line. |
| Final blind 6: self-declared C# capture | high / patch | A valid content digest proves bytes, not the claimed language; JSON credential content labeled Program.cs bypasses the colon-assignment check and is masked outside. Ensure structured JSON keeps its credential semantics regardless of a self-declared source extension. |
| Final blind 7: Go raw string | high / patch | C# lexical state does not recognize Go backticks, so literal text within a Go raw string receives a runtime-field exemption. Restrict the Go runtime exemption to real Go code, including raw-string handling. |
| Final blind 8: standalone initialization summary | medium / patch | IsolatedRunner construction occurs before summary handling. The refused endpoint retains isolation failure/cleanup but omits the requested bounded summary. Add the existing failure-summary behavior to this entry point and preserve ownership/privacy on rejected destinations. |
| Final blind 9: nested inventory schema | medium / patch | The common reader returns decoded inventory contents without visiting nested schema markers; an unsupported nested reference is returned unchanged. Validate nested references with bounded recursion/cycle handling under the same operation budget. |
| Final blind 10: mixed signature excerpt marker | medium / patch | signature_verified dispatches only on output_ref/output_schema, so valid legacy output plus output_excerpt is accepted. Use the common complete marker set and reject this mixed boundary form. |
| Final edge 1: property-function task destination | medium / patch | Independently confirmed custom property-function bridge activation at the same unresolved-expression branch. Shares final blind 1's correction after independent verification. |
| Final edge 2: escaped JSON credential name | high / patch | Independently confirmed the Unicode-escaped property-name bypass and the rejecting plain-key control. Shares final blind 5's correction after independent verification. |
| Final edge 3: standalone initialization summary | medium / patch | Existing-parent admission raises before summary export, independently of the rejected-endpoint reproduction. Share final blind 8's correction and cover both admission states. |
| Final verification other 1: standalone initialization summary | medium / patch | Verified constructor refusal occurs before export in main and that existing tests cover the separate published-executor entry point. Share final blind 8's correction, adding a real standalone CLI control with Docker blocked. |

The fourteen individually classified findings form ten correction groups: unproven task destinations, task-item properties, task-property escaping, direct Exec activation, decoded credential names, self-declared source language, Go raw-string context, initialization summary, nested reference validation, and signature marker dispatch. Each smallest correction remains in an existing private helper/entry point, introduces no public surface, and covers a reproduced state. The captured intent and plan already require these refusals and preserved outcomes; no intent/spec loopback or historical rollback is needed. All groups route to focused patches in the same implementation agent; no findings are deferred.


Parent verification of the final patches identified the following adjacent regressions before accepting the handoff. They share the existing private correction groups; none changes frozen intent or historical authority.

| Finding | Verdict / route | Evidence and correction |
| --- | --- | --- |
| Final parent 1: interrupted summary exit | medium / patch | The new standalone failure handler initially mapped KeyboardInterrupt to 2 although initialization records 130. Preserve 130 and add an interrupted-initialization summary control; that focused control passes. |
| Final parent 2: Exec expansion truncation | medium / patch | Direct Exec expansion took 257 values without refusing overflow, unlike the existing project-edge bound. Assert at most 256 before consuming values; the 258-alternative regression passes. |
| Final parent 3: host separator in executable name | medium / patch | Actual AppHost evaluation encountered an Aspire RID helper invoking a quoted backslash dotnet exec path on Linux, parsed as a build destination. Normalize separators only for executable identity, preserve the non-build exec distinction, and cover it without executing SDK targets. |
| Final parent 4: SDK reference annotations | medium / patch | The code-coverage target consumes SDK-generated AnnotatedProjects and nearest-framework metadata that raw source expansion cannot simulate. Map only the proven SDK reference protocol to a conservative actual/source reference union and inspect real child frameworks plus the outer context. Preserve custom additions and effective properties; no package-wide exemption is permitted. |
| Final parent 5: package JSON identity | medium / patch | Post-review scanner controls ran 980 tests with one failure in PackageVersionExemption_IsLimitedToDependencyObjects. Appending .json to an existing package.json identity and ignoring its dependency-object context broke an existing public metadata rule. Preserve actual JSON identities and existing bounded dependency-object recognition alongside decoded-key rejection; retain the failing run record. |

The final local commit is required by step-05 of the explicitly invoked bmad-build workflow, subject to the owning repository's pinned commitlint 21.1.0 and exact-message preflight. Earlier no-commit statements remain historical observations from those verification points; this finalization grants no push, merge, publication or qualification authority. The explicit candidate has passed the pinned local CLI (`commitlint-final.json`); preserve that validation evidence and revalidate any replacement.


| Finding | Verdict / route | Evidence and correction |
| --- | --- | --- |
| Final parent 6: built-in batched item identity | medium / patch | The post-review full Contracts run has 126 tests, 125 passing and one refusal in Admin.Abstractions.Tests. Microsoft.Testing.Platform.MSBuild 2.5.1 batches the current project through `_ProjectToTestWithTFM` using `TargetFramework=%(_TargetFramework.Identity)` and declared TargetFrameworks. Resolve that built-in identity into separate bounded variants, preserving explicit metadata overrides and unknown custom-metadata refusal. |
| Final parent 7: decoded key role grammar | medium / patch | The post-review full secrets run has 981 tests, 980 passing, with 30 findings in three unchanged files. Whole arbitrary property names were passed directly to IsCredentialName, broadening the original assignment-name grammar to slash-qualified file-count and Kubernetes reference keys. Require the original full assignment-name grammar before decoded credential-role recognition; keep all scalar/value scanning, escaped/plain parity and adjacent credential rejection. No path/hash exemption is permitted. |
| Final parent 8: published CLI foreign failure summary | high / patch | An actual CLI run with invalid inputs, Docker blocked and an existing foreign output exited 2 but included `private-control.txt` in the complete summary index (`/tmp/story322-published-summary-boundary-l7hwukbq/probe-result.json`). Bind export to actual Executor/controller ownership, retain partially initialized owned attempts, and never infer ownership of a rejected packet or isolation sibling; this shares the standalone CLI ownership correction's root cause. |

## Post-review closing verification

The ten Python commands completed successfully again: **548 test executions**, including storage 36 in each mode and isolation 45. `post-review-python-verification-summary.json` binds every exact command and complete log. The subsequent published CLI ownership correction receives additional targeted verification below.

All **980 scanner controls** excluding the full tracked scan pass (`/tmp/story322-review-fixes-scanner-controls-complete.log`). All **58 focused Contracts controls** pass (`/tmp/story322-review-fixes-contracts-focused-protocol.log`), and the actual AppHost passes both evaluation-only modes (`/tmp/story322-review-fixes-apphost-annotated.log`). These focused results do not replace the two required full gates.

- `post-review-contracts-full.json`: exact full Contracts command above; **126 run, 125 passed, one failed**, no timeout, 252.571 seconds. The remaining built-in identity protocol correction is recorded as final parent 6.
- `post-review-secrets-full.json`: exact full secrets command above; **981 run, 980 passed, one failed**, no timeout, 543.240 seconds. All 30 reported path/line findings are retained in the complete log; final parent 7 records their exact name-role root cause.
- At those failed-run checkpoints, both required gates remained open. Keep the story in review and make no final commit until corrected full results pass. Previous green runs and all failures remain preserved as version-specific diagnostics.


## Completion verification

All closing diagnostics remain outside the checkout at `/tmp/story-3-22-completion-22sche_s/`. `verification-summary.json` records exact commands, exits, timings, log hashes and final source identities; earlier failures remain preserved.

The corrected full gates both exit 0 without timeout: **982 secrets checks passed** in 546.739 seconds and **133 Contracts checks passed** in 729.585 seconds, with zero failures/skips (`closing-secrets-full.json` / `.log` and `closing-contracts-full.json` / `.log`). These runs cover the final C# sources. After the isolated published CLI ownership correction, all **58 additional Python checks** and the delivered-file secrets scan pass:

| Exact command | Closing result | External log |
| --- | --- | --- |
| `PYTHONPATH=tools/tests:tools python3 -m unittest test_p1r_published_executor.ReviewedExecutorSummaryTests test_p1r_published_executor.SeparateRootExecutorTests test_p1r_evidence_storage.ReviewedStorageTests.testSummaryPreservesOuterCleanupLaneAndInitializationFailures` | 13 passed, 5.560 seconds | `closing-published-cli-ownership.log` |
| `python3 -m unittest discover -s tools/tests -p test_p1r_isolation.py` | 45 passed, 4.232 seconds | `closing-isolation-cli-routing.log` |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method '*CanonicalReconciliationDelivery*'` | One passed; final delivered files scanned | `closing-delivery-scan-after-cli.log` |

The preceding ten Python commands passed **548 executions**, including both normal and optimized storage suites. The closing targeted checks cover every changed CLI constructor/routing seam, actual Docker-blocked foreign packet/sibling rejection, owned partial initialization and cleanup summaries. Both Debug/source builds passed with zero warnings/errors; the changed credential-fixture classes passed all 101 checks. No Release/package build or Aspire lifecycle operation was required.

All three final review layers completed. Every finding was individually triaged and corrected; **nothing was deferred**. The matrix audit maps all six frozen scenarios to registered passing controls (`matrix-audit-closing.json`). All **35,581 historical paths/bytes are unchanged**, with no missing or added historical files (`historical-closing.json`); the frozen intent remains byte-identical (`frozen-intent-closing.json`). `git diff --check` passes.

The exact local commit candidate passed the repository's pinned commitlint CLI: `node node_modules/@commitlint/cli/cli.js --config commitlint.config.mjs --edit /tmp/story-3-22-completion-22sche_s/commit-message.txt --verbose` (`commitlint-final.json` / `.log`). The invoked workflow requires a local commit; no push is performed. P5 rollout, Story 3.23 retirement, durable acceptance storage and external Story 9.1 authority remain separately owned, as required by frozen intent. These results establish implementation verification only and confer no qualification/readiness or publication authority.

### Review Findings

Code review 2026-10-21, chunk A of 3: Python evidence storage/producers/readers under `tools/**`, `.gitignore`, `.github/workflows/p1r-qualification.yml` and `tools/p1r-published-consumers/README.md`, `d01c6756c..c91f52027`. Chunk B (secret-scanner tests) and chunk C (Contracts frozen MSBuild evidence) are still unreviewed. Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor; none failed. Only `.gitignore` and `DeployedRuntimeParityClosureTests.cs` changed in scope after `30260abc7` (concurrent `c91f52027`).

- [x] [Review][Patch] Holds cannot be applied, so every new-format packet stops validating 30 days after capture (owner decision 2026-10-21: separate hold record) — No producer or CLI passes `Store(hold=True)`. `hold` and `retained_until` sit inside the immutable manifest, whose digest is bound into command rows and receipts, so a hold cannot be added later either. `resolve` then refuses every external witness, packet validation, boundary measurement and complete index. Spec: "holds preserve required bytes". Resolution: readers consult a separate, owner-written hold record outside the hashed manifest, so a hold can be added after capture without changing bound receipts. (blind+edge+auditor) [tools/p1r_evidence_storage.py:264]
- [x] [Review][Patch] `prepare`/`create_packet` packets have no external-location rule (owner decision 2026-10-21: external, refuse up front) — `create_packet` writes `packet.json`, `source-binding.json` and `inputs.json` before the default-verifier package lane's `Store(directory.parent / ".p1r-artifacts")` refuses a checkout-contained parent (matrix: "refuse before writing"). Imported receipts are copied byte-for-byte (about 4.2 KB per external command row, so about 20.5 MiB for 5,116 rows) and are neither Git-excluded nor 10 MiB-bounded if committed. Resolution: treat qualification packets like executor packets and refuse a checkout-contained or enclosing-checkout destination before writing anything; the bounded summary stays the Git artifact. [tools/p1r_published_qualification.py:1163] (auditor+blind)
- [x] [Review][Patch] Summary export stores a second copy of every packet file (owner decision 2026-10-21: keep copy, add byte cap) — `export_attempt` retains each packet member, including `commands.json` and copied `archives/*.nupkg`, in the object store. That doubles per-attempt disk use and has no aggregate byte cap, against "store identical payloads once". Resolution: keep the immutable copy, which makes the complete index verifiable, and add an aggregate export byte budget. [tools/p1r_evidence_storage.py:524] (blind+auditor)
- [x] [Review][Patch] Reject mixed inline/external isolation references — a wrapper that still carries `evidence_context`/`external_root` but has been downgraded to inline `content` and inline command `output` rows still yields `receipt_outcome` = `passed` (reproduced). New-format markers must require external preservation and command rows; only hash-only `output: None` rows may stay inline. The `change_isolation` test helper relies on the downgrade. [tools/p1r_published_qualification.py:1295] [tools/p1r_isolation_evidence.py:39]
- [x] [Review][Patch] The qualified-snapshot witness resolves the whole run-root journal for each qualifying case — every external output is re-read and re-hashed per case, with a fresh 256 MiB budget, just to find one `docker inspect` row. Select the matching rows first and resolve only their output. [tools/p1r_check_witnesses.py:324]
- [x] [Review][Patch] Check `--summary-out` before the run starts — the destination is only checked inside `export_attempt` after the run, so an existing or packet-contained destination turns a completed run (up to 24 h) into exit 2 with no summary, and there is no re-export command. [tools/p1r-published-executor.py:128] [tools/p1r_isolation.py:898]
- [x] [Review][Patch] Add external-form fixtures for the migrated readers — the CLI relay of `command_stdout_command`, the external run-root `commands.json` in `validate_qualified_snapshot`, and `assess_lane` via `import_receipt`/`validate_packet` are only exercised with inline data. Reverting or dropping the enclosing argument at each site passes every test (verification-gap mutation runs). [tools/p1r-published-executor.py:84] [tools/p1r_check_witnesses.py:324] [tools/p1r_published_qualification.py:1734]
- [x] [Review][Patch] Run the documented default external `--out` layout in a test — no test runs without `--out` or with `HEXALITH_P1R_EVIDENCE_ROOT`. Assert the `attempt-<uuid>/packet` and isolation sibling paths, and that two runs get distinct fresh parents. [tools/p1r-published-executor.py:58]
- [x] [Review][Patch] Assert the summary `completed` outcome — every summary test asserts `failed`; no clean packet is exported and checked for `completed` with exit 0. [tools/p1r_evidence_storage.py:559]
- [x] [Review][Patch] Add refusal tests for the controller's executor-packet placement guard — cover a `--out` outside `external_root`, equal to `output`, or already existing, each with no Docker call. [tools/p1r_isolation.py:594]
- [x] [Review][Patch] Test the enclosing-checkout refusal — no fixture puts a `.git` above a nested checkout. [tools/p1r_evidence_storage.py:93]
- [x] [Review][Patch] Cover the new `measure-boundary` `--source-root` and tooling binding branches — the only CLI test mocks `source_binding` with a constant and never checks its arguments. [tools/p1r-published-qualification.py:58]
- [x] [Review][Patch] Bind the in-container handoff to the owned attempt directory and pin it with a test — the guard now only requires "not under ROOT", and the existing `root` mutation passes that predicate and fails later for an unrelated reason. Require `run.parent == Path(wrapper_output).parent` and add a mutation that puts the run inside ROOT. [tools/p1r_isolation.py:134]
- [x] [Review][Patch] Make the socket-inside-external-root test reach the socket check — `IsolatedRunner(self.store.root / "run")` is refused by the "must be fresh" guard first, and the generic `assertRaises(InvalidPacket)` passes anyway. [tools/tests/test_p1r_evidence_storage.py:230]
- [x] [Review][Patch] `--summary-out` doubles the initialization error and cleanup in `execution.json` — `construct()` keeps the partly built Executor, whose `__init__` already appended the error and ran cleanup; `perform()` then appends and cleans up again. Without the flag, `worker` is `None`. [tools/p1r-published-executor.py:37]
- [x] [Review][Patch] An expired artifact is reported as "invalid artifact retention date" — `InvalidPacket` subclasses `ValueError`, so the expiry `require` inside the `try` is caught and relabeled. Evaluate expiry after the parse block. [tools/p1r_evidence_storage.py:262]
- [x] [Review][Patch] An empty `HEXALITH_P1R_EVIDENCE_ROOT` silently selects the current working directory — use the default whenever the variable is empty. [tools/p1r_evidence_storage.py:128]
- [x] [Review][Patch] Move the `Reviewed*` test classes above `if __name__ == "__main__"` — running the files directly skips them; CI `discover` still collects them. [tools/tests/test_p1r_published_executor.py:2972] [tools/tests/test_p1r_published_qualification.py:2066]
- [x] [Review][Patch] Exclude the remaining unambiguous generated-evidence formats — `.xz`, `.bz2`, `.zst`, `.rar`, split archives (`*.001`, `*.z01`), `.dmp`/`.gcdump`/`.etl` dumps and `.mkv` are still admitted. Leave images alone, since source-owned screenshot baselines stay tracked. [.gitignore:513]
- [x] [Review][Patch] Observe-path planning bytes are read twice — the parsed planning document and `observation_sha256` come from separate `regular()` reads, so a concurrent change can bind a digest of different bytes. Hash the bytes that were parsed. (parent observation) [tools/p1r-published-executor.py:103]
- [x] [Review][Defer] External-evidence validation depends on the live validator host [tools/p1r_isolation.py:389] — deferred: unverified (medium if true). `validate_external_root` and `materialize` re-check current mountinfo, socket identity, directory ownership by the reading user and availability of the prior attempt root at validation time. To settle it, run `create_packet`/`validate_packet` on a new-format receipt inside a fresh isolated runner and as a different user.

Rejected:

- false (resolved before this review completed): the `.gitignore` 3.22 block broke the CI-gated `DeployedRuntimeParityClosureTests.GitignoreLastPatternsReincludeEvidencePackageArchivesAndExcludeGenericNupkgs` at `30260abc7`. Concurrent `c91f52027` made the selected Story 3.13 packet negations final and re-pointed the guard; rebuilt and re-run at HEAD: 1 passed, 0 failed. Its ordering comments were updated in the same commit.
- false: no admission enforcement beyond `.gitignore` — the spec assigns shared `/pushall` admission enforcement to Hexalith.AI.Tools P5.
- false: `measure-boundary` source and input checks on legacy receipts — the command drafts from new receipts, source binding stays authoritative, and the earlier review (Blind 2) requested this context.
- false: summary command totals only from the root journal — required by the continuation task.
- low: absolute host paths and non-portable summary locators — the local diagnostic store is documented as never portable or durable.
- low: no summary verifier — the summary is diagnostic-only and no criterion requires re-derivation.
- low: export aborts on symlink, FIFO, non-dict, NaN or >256 MiB packet members — no producer reaches these, and the failure is loud (`summary_exported: false`).
- low: destinations inside `--source-root`/`--source-builds-root` checkouts are not refused — this needs an explicit misconfiguration, and the fix adds guards.
- low: inconsistent `source`/`candidate` derivations — each reader uses its producer's derivation; refactor only.
- low: non-UTF-8 preserved file raises `UnicodeDecodeError` — preserved files are ASCII `json.dumps` output; tampering still fails closed.
- low: README carries the secret-gate and MSBuild paragraphs and no real `--summary-out` example — cosmetic.
- low: `measure-boundary` ignores `--source-builds-root` without tooling pins — unlikely, and the fix adds a guard.
- low: duplicate `inputs_sha256` assignment, unverified redundant `raw_output_sha256`, id-keyed hash cache — harmless today; the cache keys stay referenced in `files`.
- low: non-executor isolated commands bind `source`/`candidate`/`inputs` as null — such commands have no selection and carry no qualification authority.
- low: hardening fixture uses `TemporaryDirectory(dir=ROOT)` — deliberate (source paths must sit inside the workspace) and cleaned via `addCleanup`.
- low: test helpers materialize with the artifact's own context — test-only.
- low: partial summary left after a write failure, `errors_count` double count, `raw_output_bytes` 0 for hash-only rows, local-process-control receipts counted, summary under `reports/` ignored — failure-only or cosmetic.
- low: >256 MiB command output raises before the row is recorded — loud refusal; no producer shown reaching it.
- low: the ancestor `.git` rule is environment-sensitive — consistent with "outside every checkout"; the transient foreign `/tmp/.git` is no longer present.
- low: unbounded `os.walk` in `validate_external_root` — stat-only, and bounded by the attempt's own object count (live-host dependence is deferred above).


## Review-patch continuation verification (2026-10-21)

All 20 recorded patch findings are implemented. Owner-written artifact holds extend retention without changing bound manifests or receipts; qualification packet destinations are rejected before capture; packet-copy, manifest and complete-index export bytes share a 256 MiB ceiling. External isolation markers cannot fall back to inline preservation or command output. Snapshot witnesses select only the exact owned ID/name inspection rows and preserve inline/external parity. Both lifecycle checks share one verified-byte budget. CLI destination preflight, initialization cleanup, default layouts, planning-byte identity, source/tooling transport and ownership handoff have dedicated controls. Generated trace/archive/dump/video admission is covered while ordinary summaries and source image baselines remain allowed.

The final ten sequential commands below all pass: **581 test executions**, zero failures/skips. Each runs with `TMPDIR=/home/administrator/.cache/story322-tests-bNa3ravn`; complete command/environment, timing, exit, log-hash and unchanged before/after source records are at `/tmp/story-3-22-review-patches-senPa0E9/final/verification-summary.json`. Complete logs remain external. No C# sources changed.

| Exact command | Passed | Seconds |
| --- | ---: | ---: |
| `python3 -m unittest discover -s tools/tests -p test_p1r_evidence_storage.py` | 45 | 31.724 |
| `python3 -O -m unittest discover -s tools/tests -p test_p1r_evidence_storage.py` | 45 | 31.686 |
| `python3 -m unittest discover -s tools/tests -p test_p1r_published_executor.py` | 225 | 349.279 |
| `python3 -m unittest discover -s tools/tests -p test_p1r_published_qualification.py` | 73 | 45.625 |
| `python3 -m unittest discover -s tools/tests -p test_p1r_isolation.py` | 47 | 6.086 |
| `python3 -m unittest discover -s tools/tests -p test_p1r_isolation_hardening.py` | 29 | 180.777 |
| `python3 -O -m unittest discover -s tools/tests -p test_p1r_isolation_hardening.py` | 29 | 224.992 |
| `python3 -m unittest discover -s tools/tests -p test_p1r_qualification.py` | 54 | 147.817 |
| `python3 -m unittest discover -s tools/tests -p test_p1r_verifier_corrections.py` | 31 | 3.576 |
| `python3 -m unittest discover -s tools/tests -p test_p1r_snapshot_registration.py` | 3 | 37.411 |

The initial 218-test executor diagnostic failed on a stale expectation for duplicated cleanup; the exact two-attempt/single-error correction is covered by the closing 225-test executor run. Its captured failure and incomplete-log disposition remain preserved in the parent diagnostic directory. The intermediate optimized storage run encountered a transient enclosing `.git` marker above shared temporary fixtures; its complete failed log and original source identities are retained. The separate TMPDIR avoids that interference without changing the enclosing-checkout refusal. Preparation emitted an existing non-failing `/proc` ResourceWarning; all 54 preparation checks passed.

Parent acceptance audit: all six frozen matrix rows map to registered tests in both passing 45-test storage suites, with verified complete-log hashes. `/tmp/story322-resume-audit-x_45xigy/matrix-audit.json` records the mapping. The delivered-file secret gate (`dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method '*CanonicalReconciliationDelivery*'`) passes **1/1**, zero failures/skips, with exact command and complete log in that same audit directory. The previous full 982-test secrets and 133-test Contracts gates retain matching complete-log hashes and C# source identities (`retained-full-gates-audit.json`).

Frozen intent remains byte-identical; all 36,210 historical entries present at resumed HEAD `1748b16b11fb63b885dfa2fd4a9c7a420ea103a9` remain unchanged (`preservation-audit.json`). The original baseline is preserved. The complete cumulative diff from that original baseline is retained externally; it includes earlier concurrent committed work, which this continuation preserves. Parent audit inspected the resumed tooling diff and the post-audit corrections.

The pre-existing live-validator-host dependence remains deferred as recorded above. Durable acceptance storage, P5 rollout, Story 3.23 retirement and the absent external Story 9.1 authority remain separately owned. These implementation checks confer no qualification/readiness or publication authority. Independent review and local workflow finalization follow this verification record.


## Independent-review correction handoff, 2026-10-21

All eleven filed correction groups (selected-checkout output admission and the ten reproduced MSBuild executable contexts) are implemented, along with the parent's adjacent conflicting standalone/child source-root finding. Constructors preflight selected source and Builds destinations before output or object-store creation. Actual published and standalone CLI paths preserve both known selections for admission and rejected-run summary fallback; neither path can adopt a rejected checkout destination. Regression controls block Docker and assert no attempt, artifact-store or summary writes.

The frozen-input inspector preserves Exec working-directory and explicit environment context, command-property precedence, comma-separated switches and escaped values, raw reference configuration/platform/framework metadata, imported defining-file identity, individual semicolon-separated item identities and direct MSBuild.dll entry points. Directory destinations select one proven project or refuse ambiguity. Unproven target-time task output properties remain unresolved and fail closed. Literal source comments cannot impersonate generated preprocessor ownership boundaries; existing SDK/Aspire controls remain passing. The frozen inventory seal and historical inputs are untouched.

Focused verification only was run in this correction pass, as requested; parent-owned full verification remains pending. Exact command records, source hashes and complete-log hashes are in `/tmp/story322-review-corrections-p1m2kv0e/closing-verification.json`; the ten original external fixtures and their assertion refusals are individually retained in `results.json` and complete logs in that directory.

- `TMPDIR=/home/administrator/.cache/story322-tests-bNa3ravn dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method '*FrozenMsBuild*'`: **76 passed**, zero failures/skips, 122.449 seconds. This includes all eleven new executable-context controls and the original escape, SDK ownership, Aspire helper, metadata and preservation controls. All ten supplied external probe fixtures now refuse through the actual rebuilt validator.
- `TMPDIR=/home/administrator/.cache/story322-tests-bNa3ravn PYTHONPATH=tools/tests:tools python3 -m unittest test_p1r_published_executor.SeparateRootExecutorTests test_p1r_published_executor.ReviewedExecutorSummaryTests test_p1r_isolation.SeparateSourceMountTests test_p1r_isolation.AutomaticRoutingTests test_p1r_isolation.PrivateEndpointTests`: **29 passed**, 5.504 seconds, before the adjacent standalone-root correction. Their original result remains preserved.
- `TMPDIR=/home/administrator/.cache/story322-tests-bNa3ravn PYTHONPATH=tools/tests:tools python3 -m unittest test_p1r_isolation.SeparateSourceMountTests.testConflictingStandaloneAndChildRootsRefuseBeforeAnyWritesIncludingSummaryFallback`: **one passed**, 0.003 seconds, after that final correction, covering summary off/on. No wider Python repeat was run.
- The ordinary Debug/source build command (`dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --no-restore -m:1`) was blocked by existing mixed dependency assets: MSB3243 and CS1704 report duplicate `Hexalith.Commons.UniqueIds` references. `/tmp/story322-contracts-review-build.log` preserves that failure. The focused compilation adds `-p:BuildProjectReferences=false`, uses existing dependency artifacts, and succeeds with zero warnings/errors (`/tmp/story322-contracts-review-build-final.log`). No dependency configuration was changed and no gate was weakened; this focused result does not replace the parent's full build or Contracts gate.

The earlier 581-test Python record, failed diagnostics, source identities and acceptance audits remain version-specific records above. Current C# changes require the parent's new full Contracts verification; earlier full-gate results are not claimed for these new sources. This handoff grants no qualification, readiness, publication or durable acceptance authority. The previously accepted live-validator-host deferral remains unchanged.


## Final parent verification and workflow completion (2026-10-22)

All twelve independently triaged closing findings (the eleven reviewer findings and the adjacent standalone-root finding) are corrected and verified. All ten saved synthetic MSBuild contexts now refuse through the rebuilt validator, with actual assertion failures recorded per case. No new findings are deferred. The pre-existing live-validator-host deferral remains unchanged. Blind Hunter's interrupted final report and the parent's independent recovery remain explicitly recorded; the other two review layers returned normally.

The final parent checks below all exit 0 with no skipped tests or timeout. Complete exact argv, environment, timings, complete-log hashes and unchanged before/after delivered-source identities are in `/tmp/story322-resume-audit-x_45xigy/post-review/verification-summary.json`. These checks supersede the pending state in the handoff above. The prior 581-execution Python run and 76 focused frozen-build controls remain retained version-specific evidence; the parent repeats all affected CLI/source/summary classes, the entire isolation module and a real producer round trip after the final correction.

| Exact command | Result | Seconds |
| --- | --- | ---: |
| `python3 -m unittest test_p1r_published_executor.SeparateRootExecutorTests test_p1r_published_executor.ReviewedExecutorSummaryTests test_p1r_isolation.SeparateSourceMountTests test_p1r_isolation.AutomaticRoutingTests test_p1r_isolation.PrivateEndpointTests` | 30 passed | 5.126 |
| `python3 -m unittest discover -s tools/tests -p test_p1r_isolation.py` | 49 passed | 4.126 |
| `python3 -m unittest test_p1r_evidence_storage.EvidenceStorageTests.testRealPythonSubprocessThroughExecutorRunUsesVerifiedExternalOutput` | 1 passed | 0.215 |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method *GitignoreLastPatternsReincludeEvidencePackageArchivesAndExcludeGenericNupkgs` | 1 passed | 0.415 |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method *CanonicalReconciliationDelivery*` | 1 passed | 0.916 |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class *ContractsPackageDependencyTests*` | 144 passed | 871.843 |

The required full Contracts gate passes **144 tests**, zero errors/failures/skips, in **871.843 seconds**. The delivered-file secret check covers the final changed C# guard and Python sources. Scanner implementation and its credential-rejection controls remain unchanged; its previous complete 982-test gate retains matching implementation identity and verified complete logs. Frozen intent, all 36,210 starting historical entries and every reference gitlink remain unchanged (`post-review/preservation-audit.json`). No original evidence, approval, source seal, selection or digest pin is rewritten.

One broader build limitation remains factual: `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --no-restore -m:1` exits 1 with MSB3243/CS1704 duplicate `Hexalith.Commons.UniqueIds` dependency references. The unchanged complete failure is retained at `/tmp/story322-contracts-review-build.log`. The narrow fallback adds `-p:BuildProjectReferences=false`, compiles the changed test project with zero warnings/errors using the existing dependency artifacts, and passes; the full Contracts test gate above is independently green. No dependency configuration or warning policy was changed to hide that limitation.

The exact local commit message passed pinned commitlint **21.1.0**, zero problems/warnings: `node node_modules/@commitlint/cli/cli.js --config commitlint.config.mjs --edit /tmp/story322-resume-audit-x_45xigy/commit-message.txt --verbose`. Its exact-message hash and complete validation log are retained in `commitlint.json` / `.log`. Workflow status is `done`; sprint tracking moves to `review` under step-05. The workflow creates a local commit only. P5, Story 3.23 retirement, durable acceptance storage, the absent external Story 9.1 record and all qualification/readiness/publication authority remain separately owned.

### Review Findings (code review 2026-10-22, chunks B+C)

Code review 2026-10-22 of chunk B and chunk C, `d01c6756c..2444752e1`, narrowed to 13 files (2,743 diff lines).

- Chunk B, secret-scanner source recognition: `SecretsProtectionTests.*`, `WorkloadBindingAuthorityTests.QueryAuthority.cs`, `NativeSourceMetadataReaderTests.cs`.
- Chunk C, frozen MSBuild evidence: `ContractsPackageDependencyTests.FrozenEvidence.cs`, `ContractsPackageDependencyTests.cs`, `scripts/validate-eventstore-package-authority.ps1`.

Layers: Blind Hunter, Edge Case Hunter, Verification Gap and Acceptance Auditor; none failed. Still unreviewed: the chunk-A patch-application delta (`c91f52027..HEAD` over `tools/**`, `.gitignore` and the p1r workflow), called chunk A2.

- [ ] [Review][Patch] Speed up the frozen-MSBuild guard so CI `contracts` regains its timeout margin (owner decision 2026-10-22: speed up in-test; no sealed workflow edit). The full-repository `VerifyFrozenEvidenceNotBuildInputAsync` walk raised the lane from 11m33s (`fcf78d6ca`, run 39159607831) to between 18m18s (`c8a0e9af8`) and 20m54s (`a964048c3`, run 39738043827). The limit is `timeout-minutes: 25` (`.github/workflows/ci.yml:92`), and the local class run takes 871.8 s. A timeout cancels the whole lane. `ci.yml` is a worktree-hashed OQ8 v3 gate input, so its timeout stays unchanged. Resolution: run the per-project MSBuild evaluations with bounded parallelism and drop duplicate evaluations. Keep the identical inspected set and fail-closed assertions, and record the before and after class timings. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs:220] (acceptance-auditor+verification-gap)
- [ ] [Review][Patch] Tool-output diff views drop hunk-header text, so a credential after `@@` is never scanned. `SourceDiffView` opens a fence on each `@@ … @@` line without copying the line, and the raw `output` string is masked. Git's function context such as `@@ -30,3 +30,3 @@ DB_PASSWORD = "<literal>"` passes `FindViolations` but fails the pre-change raw scan (reproduced by reflection). Emit the header line outside the fence in both views, and add a header-text case to `SourceContexts_DiffLanguageNeverConcealsLiteralCredentials`. [tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.SourceContexts.cs:186] (verification-gap+edge-case-hunter)
- [ ] [Review][Patch] Custom `Exec` commands that the direct-invocation parser does not recognize are never checked for evidence paths. `FrozenDirectExecTask` returns null for the following, and the Exec loop then drops them:
  - `dotnet clean`
  - `dotnet <path>/MSBuild.dll` without `exec`
  - host options before the verb
  - response files
  - multi-line commands, whose newlines merge into one argv

  So `dotnet clean <evidence>/Captured.csproj` and `dotnet "$(MSBuildToolsPath)/MSBuild.dll" <evidence>/Captured.csproj` pass the guard, while real MSBuild runs the archived project (reproduced). Fix: apply the existing evidence-marker check to every expanded non-SDK `Exec` command and its `WorkingDirectory`, and add both reproductions to `FrozenMsBuildEvidence_CustomExecutableEdgesRemainFailClosedAsync`. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.FrozenEvidence.cs:904] (blind-hunter+acceptance-auditor+edge-case-hunter)
- [ ] [Review][Patch] A recursive `**` in target-time project globs matches only one directory level. `ExpandFrozenBuildPaths` passes `**` to `Directory.EnumerateFileSystemEntries`, which treats it like `*`. `<Projects Include="**/Captured.csproj"/>` with `<MSBuild Projects="@(Projects)"/>` passes the guard, while MSBuild builds the archived project four levels down (reproduced). Expand a `**` segment to the parent plus every descendant directory within the existing 4,096-entry budget, and add a nested-glob activation case. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.FrozenEvidence.cs:1204] (acceptance-auditor+edge-case-hunter)
- [ ] [Review][Patch] Two NuGet rejection controls still put a raw `RandomSecret()` in a version slot, so they fail about 1.5% of runs. This change found that base64 can parse as a valid version (`7+Abc…`) and fixed `SecretsProtectionTests.NuGetEvidence.cs:68` with a `credential-` prefix. The same problem remains at `:994` and `:1032`. In 80 local runs, `NuGetDependencyMetadata_AllowsVersionMetadataAndRanges("[8.14.0-alpha,8.14.0)")` failed once. Use `"credential-" + RandomSecret()` at both sites. [tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs:994] [tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs:1032] (verification-gap)
- [ ] [Review][Patch] Evaluated ProjectReference global-property metadata is not trimmed. `GlobalPropertiesToRemove`, `UndefineProperties`, `Properties`, `AdditionalProperties` and `Set*` are split on `;` without `TrimEntries`. Whitespace after `;` has two effects: a removal becomes a no-op, so the guard inspects a different global-property context than MSBuild; or a ` Name` key is produced and evaluation fails. Add `StringSplitOptions.TrimEntries`, as `FrozenReferencedTaskProperties` already does. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.FrozenEvidence.cs:1005] (edge-case-hunter)
- [x] [Review][Defer] JSON credential keys that contain `/`, spaces or `@` are not scanned [tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.SourceContexts.cs:77] — deferred: pre-existing. `{"db/password": "<literal>"}`, `{"db password": …}` and `{"@token": …}` produce no violation from either `FindViolations` or the pre-change `FindViolationsCore` (reflection probe, 2026-10-22). The new JSON walker requires a strict key-name pattern, and the raw grammar skips these keys. (edge-case-hunter)
- [x] [Review][Defer] Credential-named JSON properties with array or object values are not scanned [tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.SourceContexts.cs:74] — deferred: pre-existing. `{"password": ["<literal>"]}` and the ARM-parameter shape `{"adminPassword": {"value": "<literal>"}}` produce no violation either way (reflection probe, 2026-10-22). The walker checks scalar values only. (edge-case-hunter)

Rejected:

- low: CRLF checkouts change the frozen inventory hash, since 84 of the 984 files are only `text=auto` (blind, auditor, edge). All CI runs on ubuntu and the owner works in WSL. A fix needs either `.gitattributes`, which is a sealed OQ8 v3 gate input (`tools/validate-oq8-platform-evidence.py:289`), or blob hashing.
- low: the joined `-getProperty` argument (about 37.5K characters) exceeds the Windows 32,767-character command-line limit (blind, edge). There is no Windows lane, the Linux 128 KiB per-argument cap is not reached, and a fix needs response files or chunking.
- low: a copied preprocessor boundary comment hides a real import (blind, auditor, edge). It requires a byte-identical copy of an MSBuild `-preprocess` boundary, including the absolute checkout path, inside a source project, which cannot happen by accident. A fix adds per-occurrence counting.
- low, maybe-false: evaluation results depend on the last restore (blind). Package-delivered targets come from the NuGet cache, not from evidence, so the worst case is differing SDK-edge coverage.
- false: the wrong-secret exemption is not bound to a path or hash (blind). The continuation task forbids directory and hash allowlists, and the only exempted literal is `new string('x', 64)`.
- low: the fail-closed decode branches are untested, and the budget is charged twice for nested JSON (blind). The only effect is fail-closed false positives, above about 128 MiB of nested JSON or on duplicate keys. The full CI scan passes.
- low: a self-sealed capture's claimed `path` selects path-keyed rules (blind, edge). Producers record the real source path, so the leniency matches the live file's. Only a deliberately false path reaches it.
- false/low: the forged-hunk test summary overstates, and fences can be injected in `SourceDiffView` (blind, edge). The summary refers to ordinary Python recognition, which forged hunks do not get; they get only the exact-slot `p1r-python-fragment` rule. Fence injection needs a diffed line that starts with three backticks. In Markdown, that reproduces how the scanner already reads the original file.
- low: the 15-second wall-clock bound for the 4 MiB journal (blind, edge). The bound is loose, and a deterministic work counter would add complexity.
- low: an inventory failure does not name the changed path (blind). Shouldly already prints the actual count and hash. Naming the path would require storing the inventory list.
- low: no parent-directory symlink mutation, and no guard for Windows symlink privilege (blind, edge). The parent-link assertion exists, and there is no Windows lane.
- false: the PowerShell wrapper's `exit $LASTEXITCODE` (blind). The shared validator sets `$ErrorActionPreference = 'Stop'`, throws on any non-zero evaluation and exits 1 explicitly, so success leaves 0.
- low: items produced by tasks (`CreateItem` with `Output ItemName`) bypass static item expansion (auditor). This needs a static item plus a task output of the same name that points into evidence. A fix adds a branch for the same adversarial-construct class as the comment collision.
- low: the new adjacent-file rows do not exercise the frozen exclusion list, and the composed seal path is untested (auditor, verification-gap). The exclusion is an exact path list, so adjacency cannot widen without changing how the list is derived, and the seal logic itself is tested. Harm requires deleting the seal call, and the owner declined mutation-kill guards in the 6.6 rescope.
- low: five changed files are missing from the delivered-path list (auditor). That list covers untracked delivery files. All five are tracked, and the full tracked scan passed in CI at `a964048c3`.
- false: Go accepts identifier-shaped values (auditor, edge). In Go code, an unquoted identifier is a runtime reference, not a literal; only a false capture path reaches this case (see the self-sealed capture finding).
- low: a lone-surrogate escape raises `InvalidOperationException` (edge). The failure is loud, and no tracked file triggers it.
- low: `IndexOutOfRangeException` when no `{` follows the wrong-secret signature (edge). The failure is loud, and it needs a malformed file that still contains the exact signature and branch.
- low: in `Exec`, a space-separated option value such as `-c Release` is counted as a second destination (edge). The check fails closed with an explicit message, and a fix needs an option-arity table.
- low: an evaluated ProjectReference to a missing project causes a process error (edge). The failure is loud, and the build is already broken in that case.
- false: a self-reference whose properties grow causes unbounded recursion (edge). MSBuild itself loops on such a project.
- low: a macOS `/var` symlinked temporary root, and case-insensitive filesystems (edge). There is no macOS lane.
- low: synchronous git stdout/stderr reads in `GetTrackedMsBuildFiles` (edge). `git ls-files` writes negligible stderr.
- low: a typographic apostrophe in the temporary path breaks the PowerShell quoting (edge). Such temporary paths are unrealistic.
- false: pwsh may prompt for input without `-NonInteractive` (edge). The arguments file is written immediately beforehand with both mandatory keys.
