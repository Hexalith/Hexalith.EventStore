# Folders Story 12.2 — EventStore producer qualification continuation

**Disposition: `not_fully_qualified`.** The strict bounded-worker observation gate is closed: fresh real-Dapr execution passed nine scenarios, with 30 workers and all 120 required module joins. Secret-guard, historical-policy, Domain and Folders preservation gates remain open. No Folders adoption or whole-story completion is claimed.

## Source selection and exact inputs

The user selected “use latest” after another session committed a draft packet and changed source/dependency inputs. S1 freezes that ordinary producer selection at root revision `cb414530223f77cc845450a4877a4472559de2ad` plus exact overlays. Historical checkout materialization has a separately recorded limitation below. Later mutable-origin edits are preserved and are outside this qualification. Successive isolated test stages preserve S1 and every earlier failure.

| Stage | Input root | Execution scope |
| --- | --- | --- |
| S1 | `/tmp/eventstore-folders-12-2-latest-20261022T155454Z` | Fresh Debug solution, producer/Core regressions, actor and bounded real-Dapr acceptance |
| S2 | `/tmp/eventstore-folders-12-2-guardfix-20261022T171346Z` | Cursor repair; fresh intermediate Server execution |
| S3 | `/tmp/eventstore-folders-12-2-guardtight-20261022T173000Z` | Empty-call tightening and generated signing-key fixture; fresh intermediate Server execution |
| S4 | `/tmp/eventstore-folders-12-2-guardfixtures-20261022T173900Z` | Positive fixture self-ingestion repair; fresh intermediate Server execution |
| Final | `/tmp/eventstore-folders-12-2-guardfinal-20261022T175200Z` | Cursor fixture self-ingestion repair and two additional owning-reader rows; fresh final Server execution |

All isolated input roots are deliberately retained build/review inputs. Runtime fixture directories are separately cleaned. The mutable EventStore checkout and mutable Folders checkout are not qualified by these isolated runs.

| Repository | Selected revision |
| --- | --- |
| EventStore root | `cb414530223f77cc845450a4877a4472559de2ad` |
| references/Hexalith.Tenants | `1e60af449c1fdc3f9801cf57e8847e00faab2394` |
| references/Hexalith.AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` |
| references/Hexalith.Commons | `1663028fb6d9f46a0bf17919edeb21bb99be1587` |
| references/Hexalith.Builds | `2d653b611c8efcffb4d27bd8715d5dbf24eaebfd` |
| references/Hexalith.FrontComposer | `dfac0818e5a40b19145a01333cc5fc00bcd4b47e` |
| references/Hexalith.PolymorphicSerializations | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` |
| references/Hexalith.Memories | `63327920814a293e0168986be212deb10b81dc00` |
| references/Hexalith.Platform | `3beff28eede537eb36e84cdb93900af2556202d7` |
| references/Hexalith.McpCli | `4b3e7ff5cb0ba32dbaf3be8d5f7606531fc22e69` |

S1 has 16,762 ordinary inputs in [snapshot-inputs.json](snapshot-inputs.json), archived in `ordinary-source.tar.gz` (SHA-256 `c340577211153a6d692a198f6818101174d286c904046de0166eaac82e960bab`). [Source archive verification](source-archive-verification.json) checks every member. [Tracked guard inputs](tracked-guard-inputs.json), [Debug binaries](debug-binary-freeze.json) and [frozen worker runtime](qualified-worker-runtime-inputs.json) pin the other selected inputs.

The final test overlay and its exact source/binary inventories are enrolled in [final-server-selection.json](final-server-selection.json). Its [identity audit](guard-final/input-identity-audit.json) justifies only the stated reuse; [reused-evidence.json](reused-evidence.json) names each reused S1 lane. They are fresh S1 executions, not reruns on the final test overlay. No earlier handoff positive result substitutes for current acceptance.

Final Server.Tests PE SHA-256: `1f60703a55926ba86b6e254c7705f4abec0de1199b2a223b71ec1aa9060babbd`; PDB SHA-256: `a76aaed82212398263c9482887b54641fdf60d237967c0a709443a07b72c9b15`; MVID: `9515f8fb-5cdf-4083-9d0f-39200071af3e`. Its separate [PDB source audit](source-binding-guardfinal/summary.json) passed. The 63-pair S1 audit and each intermediate/final test binding remain separately retained. Generated documents use checksum-authenticated embedded bytes; ordinary disk sources remain authoritative. Explicit generated disk differences are retained in those summaries.

## Changes and secret-material findings

SDK fixture signing/channel/client values now use runtime cryptographic generation. The diff-source cursor now strictly decreases across LF/CRLF and deleted targets. Arbitrary empty calls no longer qualify as runtime sources; the narrow literal-free `Resources.OfType<ParameterResource>()` form and existing exact cancellation/configuration/cryptographic recognizers remain. Positive-control fixture construction retains its runtime inputs and original assertions. No assertion, deadline, retry, admission rule, storage limit or package pin was changed by this task.

| Decision | How it works and benefit | Cost or constraint |
| --- | --- | --- |
| Runtime fixture generation | Host/client share cryptographically generated material; authentic behavior and rejection assertions still execute | Values vary each run, so both sides must use the same fixture value; affected checks verify this |
| Worker lifetime capture | Worker waits for a separate observer to authenticate actual living OS maps; short phases cannot disappear between samples | Linux/Python observer overhead is opt-in and remains inside the existing phase watchdog |
| Authenticated generated source | Embedded generated bytes must match portable PDB checksums; ordinary source still comes from disk | Requires valid embedded source or matching disk bytes; failures remain failures |
| Preserve immutable negative evidence | Keep original bytes/hashes and classify findings individually | Global guard cannot pass while usable historical material remains forbidden; rewriting or broadly excluding it is outside this task |
| Preserve sealed-control assertion | Keep both the selected source-language behavior and the existing expectation visible | The stale expectation remains failed; deliberately restoring a false positive or changing the assertion was not chosen |

The [original 21-location analysis](original-21-secret-analysis.json) authenticated all 21 as unchanged at the initial investigation. Its classifications are 2 usable offline fixture signing keys, 3 literal test client secrets, 1 detector reproduction, 3 verified negative probes, and 12 verified false positives (5 runtime sources, 3 resource references, 2 nulls, 2 cryptographic generation). No provider authentication was attempted or inferred. Exact locations, file/line hashes and the offline signing check are retained.

Original immutable-history blocker: the original graph contained reusable fixture material at `internal-actor-admission-20261022/final-source.patch:577,1300` (SHA-256 `d8d028f2155b6e3a58eccc81d58f55fbb85ba4ca600a25b5afdffb5a74a039ca`) and `review-source.patch:481` (SHA-256 `c17f28cc74090e96d6a17e0531ef02f982f924ecd4437b4567dda8991288cb0d`). [Historical Git identity](original-history-git-identity.json) checks those older bytes separately from the current paths. Changing those captured lines and dependent seals would violate the historical-evidence constraint. Excluding evidence broadly or admitting usable keys would violate the detection constraint. Later external rewrites are recorded in [historical-guard-input-delta.json](historical-guard-input-delta.json); those different bytes cannot retrospectively qualify the original sealed graph.

The latest selected internal-actor mirror also has an exact construction limitation: 217 ignored seal members are absent and 261 captured `.cs` members have CRLF converted to LF. Every such mismatch is verified against seal-matching origin bytes in [internal-actor-snapshot-seal-explanation.json](guard-final/internal-actor-snapshot-seal-explanation.json). The current mutable-origin packet passes all 3410 members of its current seal; the isolated mirror fails its own check. Neither result restores the original21 older byte identities. Original packet verification, ordinary producer source binding and actual runtime-map coverage remain separately scoped; no full historical-packet preservation claim is made for the isolated checkout.

The selected latest graph also tracks archived source copies and test receipts that were absent from the initial 21-location graph. [Fresh guard findings](fresh-secret-guard-findings.json) distinguish the final XML display from the complete pinned owning-reader inventory and per-location semantic classifications. A path/language-context false positive remains an actual guard failure; no archive or packet is exempted wholesale.

The unchanged `SealedP1RRemediationSourceCapture_HidesOnlyTheKnownFalsePositive` assertion requires a finding for benign `new CancellationToken(true)` at sealed `source-candidate.diff:2590`. The selected `.diff` source-language recognizer correctly yields no finding. Historical bytes and their seal still match. Closure requires changing that assertion or restoring conservative false-positive behavior; neither was done. The separate [dropped hunk-header detection gap](guard-hunk-header-gap.json) also remains explicit.

## Fresh executions and scoped reuse

| Lane | Pass | Fail | Skip | Execution root |
| --- | ---: | ---: | ---: | --- |
| [guardfinal-server-full](guardfinal-server-full.xml) | 6479 | 2 | 25 | Final |
| [guardfix-server-full](guardfix-server-full.xml) | 6459 | 10 | 25 | S2 |
| [guardtight-server-full](guardtight-server-full.xml) | 6476 | 3 | 25 | S3 |
| [guardfixtures-server-full](guardfixtures-server-full.xml) | 6477 | 2 | 25 | S4 |
| [guardfix-cursor-regressions](guardfix-cursor-regressions.xml) | 10 | 0 | 0 | S2 |
| [guardtight-focused](guardtight-focused.xml) | 27 | 0 | 0 | S3 |
| [guardfixtures-focused](guardfixtures-focused.xml) | 34 | 0 | 0 | S4 |
| [guardfinal-focused](guardfinal-focused.xml) | 36 | 0 | 0 | Final |
| [client-full](client-full.xml) | 2073 | 0 | 2 | S1 |
| [testing-full](testing-full.xml) | 155 | 0 | 0 | S1 |
| [contracts-full](contracts-full.xml) | 2442 | 0 | 2 | S1 |
| [domain-full](domain-full.xml) | 1163 | 1 | 0 | S1 |
| [domain-full-repeat](domain-full-repeat.xml) | 1164 | 0 | 0 | S1 |
| [domain-failure-focused](domain-failure-focused.xml) | 1 | 0 | 0 | S1 |
| [runtime-preflight](runtime-preflight.xml) | 3 | 0 | 0 | S1 |
| [actor-live](actor-live.xml) | 1 | 0 | 0 | S1 |
| [bounded-live](bounded-live.xml) | 9 | 0 | 0 | S1 |
| [public-throttling-health](public-throttling-health.xml) | 33 | 0 | 0 | S1 |
| [testing-integration-full](testing-integration-full.xml) | 48 | 0 | 0 | S1 |

These are separate executions, not additive totals. All failure messages, skips, XML hashes and source scopes are retained in [fresh-test-results.json](fresh-test-results.json). The 547-name affected Server inventory is joined only to the final full XML in [affected-server-regressions.json](affected-server-regressions.json). Python observer regressions passed all 10 cases.

The first selected S1 Server execution was interrupted after a proven cursor loop; it has no completed XML and is never reported as passing. Its exact process identity, triage stack, cancellation and private-dump removal are retained. The initial new cursor test build failed CA1062; its narrow parameter-validation correction and successful rebuild remain separate. All interrupted/failed earlier acceptance, Domain failures and tooling audit failures remain preserved.

The current Domain full-suite failure occurred during unmeasured history setup under the unchanged 100ms deadline. The unchanged focused check and separate full repeat passed; these diagnostic outcomes do not erase the failed run. [domain-failure-analysis.json](domain-failure-analysis.json) and the earlier sealed continuation retain the distinct earlier Domain failures.

Exact argv, cwd, environment overrides, UTC times, PID, elapsed time and exit status are in each `*-command.json` receipt and its log. [commands.json](commands.json) indexes completed command receipts before this summarizer; its own completed receipt is retained separately. Main build flags are `-c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false`. Test-only successors use `--no-restore -p:BuildProjectReferences=false` and unchanged authenticated producer outputs; evaluated project properties/references are retained.

## Actual runtime mappings and cleanup

The bounded worker stays alive after loading its four required CLR modules while a separate Python process reads actual `/proc` mappings. The observer verifies PID/start ticks before/after, opened mapped PE device/inode, PE/PDB hashes and extra SDK mappings, and retains raw maps/errors. The original parent phase watchdog owns cancellation and its deadline. The audit requires the exact nine scenarios, all phases and all original storage bounds.

| Required module | PE SHA-256 | PDB SHA-256 | CLR/PE MVID |
| --- | --- | --- | --- |
| Hexalith.EventStore.Client | `3da99689f31cb780aa3d9f3016d05f1f0cf7bb8c4270356a680bd3a6e954835d` | `c4be817e0661d6af1a66a91fa7db851d992d6b4f1a93f3d93fb10eb9256d20b9` | `5a6a693e-603e-4be3-a583-2c6909b2c547` |
| Hexalith.EventStore.DomainService | `0e28ec8108f4b2a3e3f3fdf7f33f7de14c861ced6e5fc0b5fdc5c716b1c21bdd` | `1e953462bd25819e57e658044849b36bb74a6c3e7cf3e655cd6828cf45a16a4c` | `71888051-a860-4dcd-9af2-2c5d78c1406a` |
| Hexalith.EventStore.Server | `bd6ba21a2876fd58ecfc06ee61807d98d201c624aef2d9d7ed9b8f3666c45562` | `f6068efed0aee6cd65054af4d67652d5345f7f47d2272a816439f0fef233c6fe` | `61029048-4507-462f-a9e0-6b8fdb1db8a8` |
| Hexalith.EventStore.Server.LiveSidecar.Tests | `dca3d48aa21849c9b5cbaff826c4190299ed3cffb8e7e5a16c73fc5b406d76b1` | `b3f6920b073c26c1c1e339df71ffa04cf2cfbd34558898e21af8f24c9af54112` | `df2d7f97-f198-48d4-86d6-5aa0e0ca797d` |

[Runtime audit](bounded-runtime-audit.json), [120 module joins](worker-capture-audit.json), `worker-maps/` and `bounded-scenarios/` retain every worker PID/start tick, argv, native executable identity, raw map, required loaded MVID and source checksum. Docker creation/inspection receipts bind image IDs and digests; native daprd is version 1.18.7, SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`. SDK 10.0.402 and mapped .NET runtime 10.0.13 are recorded. Actor acceptance has 47 checks and 26 authenticated first-party OS-map observations; actor MVIDs are attributed from bound PE metadata, and its original PID-only receipts lack start ticks. They are not presented as bounded-worker CLR observations.

The final strict auditors passed [21 mutation controls plus stale-result/crash rejection](audit-controls-v3-result.json), including native executable substitution, failed source bindings, altered component contents/owned endpoints and extra unauthenticated first-party maps. [Nine additional controls](postreview-extra-controls.json) verify actual Unit-map rejection and new/ignored input detection. [Seven launcher controls](launcher-cleanup-v4-result.json) passed, including active-runner cancellation, runner crash, deliberate driver exception and wrapper death. They use actual disposable Dapr infrastructure with explicit process-tree surrogates at the real runner lifecycle boundary; they are not extra scenario acceptance runs. Every earlier control/result remains intact.

[Final expanded cleanup audit](closing-runtime-cleanup-v3.json) independently checked 78 owned containers, 91 exact PID/start identities, 20 explicitly limited PID-only records and 28 runtime/component directories across both continuations and fresh controls. PID-only records require current PID absence and retain original completed-child receipts; no start ticks are invented. [Guard-process cleanup](guard-resource-cleanup-v2.json) parses the retained actual final Unit maps and binds its path/device/inode plus source-verified PE/PDB, and checks all five Server executions have ended. The earlier narrower cleanup pass and the failed v2 formatting audit remain separate. Private-tool archives/removal receipts are retained separately. Deliberately retained isolated source/build inputs are not running runtime fixtures.

The [closing input audit](late-input-integrity.json) reports frozen producer integrity `True` and strict Folders preservation `False`. Folders source/project drift and Git head/branch/index/status are retained exactly; this task did not modify, restore or build Folders. Selected bin/obj observations are timestamped in that audit. External changes are preserved and cannot support a claim that all Folders source remained unchanged.

## Seal and remaining gates

The original 6101-file handoff seal is `0b4d5ade382ea889b78fdb5cba7783c7f0fbaa1bdc68d06646b4fc4ade732135`; the 5031-file initial continuation seal is `eeff5272baa8ab53fb1fef872aca9db1f7fd0fb1a094d6e71a12c263778df1ec`. Both were freshly reverified, and their failed/interrupted dispositions remain. This new directory is sealed only after finalization. `SHA256SUMS` covers every regular file except itself; verification checks the complete file set.

```sh
python3 verify.py
python3 verify.py --final-producer-inputs
python3 verify.py --current-inputs
```

Run these commands from this packet directory. The first verifies this immutable packet. The second additionally verifies the explicitly selected isolated producer/final-test inputs. The third checks file hashes in the original strict selected-input/Folders inventory and is expected to fail for recorded external Folders drift; Git metadata preservation is separately evaluated in the closing input audit. A valid seal authenticates the negative disposition; it is not acceptance.

- Original immutable-history credential material/policy blocker; external rewritten bytes do not retrospectively qualify the original sealed graph.
- Current fresh full Server failures, enumerated in fresh-test-results.json; all false positives remain failures until lawfully resolved.
- Selected internal-actor historical mirror omits217 ignored members and LF-normalizes261 captured sources; origin current3410-member seal passes, while the isolated mirror seal fails. Original21 lineage is separate.
- Unchanged sealed-capture assertion demands a verified historical false positive, conflicting with selected .diff source recognition; assertion and historical bytes are preserved.
- Selected guard drops diff hunk-header text: separate pre-existing false-negative gap.
- Preserved Domain full-suite nondeterminism, including the current100ms setup timeout; focused/repeat results are diagnostic.
- Strict Folders preservation predicate failed for external source/project drift. Exact deltas are retained; this task did not modify or restore Folders files.
- Fresh Folders source-reference adoption, exact assembled runtime/source binding, strict provider and10001-position authenticated recovery acceptance.
- Existing skipped/deferred debt, SM4 acknowledgement p95, Release/package/source equivalence/PD10, independent whole-story acceptance.

No staging, commit, push, package-pin change, publication or deployment was performed by this task. External Git/source/dependency mutations are recorded separately.

## Post-review closure and exact reuse boundary

The [final predicate record](post-review-resolution.json) requires successful ordinary-source archive and PDB/source validation as well as the complete runtime matrix, all strict audits, controls and cleanup. Its computed value generates the disposition above. [Post-review commands](post-review-commands.json) supplement the earlier command index. Executed earlier scripts and outputs remain unchanged; the prior draft README and qualification JSON are preserved byte for byte.

[Closing S1/S5 integrity](late-input-integrity-v2.json) freshly checks all 145,090 pinned producer paths, with no hash conflicts or changed bytes, and exact ordinary file sets of 16,762 S1 and 16,764 S5 entries. It reports 66 external Folders source/project deltas and zero selected bin/obj deltas; the earlier 61-delta closing observation is retained. Later mutable-origin changes remain outside the frozen selection.

The nine-case Dapr execution used the preserved original launcher. The successor `bounded-live-v2.py` adds a private runner session and orphan reaping/cleanup on success, failure and cancellation. It preserves scenario arguments, worker observer, phase watchdogs, assertions, storage bounds and production inputs. The seven fresh lifecycle controls cover these cleanup-only changes; the full matrix was not rerun on that launcher version. S1 producer outcomes reused for the final Server test overlay remain bounded by [reused-evidence.json](reused-evidence.json), the final identity audit and source binding.

The [final independent review](final-review-completion.json) found no remaining concrete defects in the scoped fixes. [Archived review inputs](final-review-inputs-cleanup.json) preserve both reviewed diffs and the final appended tooling changes; the private review directory is removed. [Final command index](final-command-index.json) joins all terminal command receipts. Both earlier packet seals were freshly reverified at18:42 UTC ([original](original-seal-preseal-command.json), [first continuation](initial-seal-preseal-command.json)). The exact final spec is retained in [spec-final.md](spec-final.md).

`SHA256SUMS` seals every regular file in this packet except itself. Seal creation and post-seal verification receipts are stored adjacent to the packet, so verification never adds files inside the sealed set. The final producer-input verification is expected to pass; the separate original Folders/current-input predicate remains expected to fail for external drift.
