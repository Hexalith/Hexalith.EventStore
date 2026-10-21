---
title: 'Folders 12.2 prerequisite: bounded captured owner identity'
type: 'feature'
created: '2026-10-21'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: ae917d3e4dbab20b8982c5222a13611c3e3faf35
approved: '2026-10-21'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The named/shared production dispatcher retains one complete identity vector per owner. Coordinator promotion/seeding rejects histories beyond 10,000, making complete rebuild unavailable after ordinary growth.

**Approach:** Implement the approved prerequisite as separate EventStore-owned work: bounded identity retention and durable checkpoint handoff through public SDK transport. Folders supplies original streams, positions and identities.

## Boundaries & Constraints

**Always:** Preserve complete position/message identity conflicts, captured-prefix comparisons, source continuity, current-head completeness, CAS fencing, selected/inherited generations, catch-up and recoverable promotion. Keep 100-operation/one-MiB batches. Bind production-dispatch qualification to exact sources and binaries.

**Never:** Cap domain history, partition original owners, omit identities/views, rewrite retained events, add consumer persistence protocols, change pins, commit, push, publish or deploy. Preserve existing work/evidence. Package/release qualification and Folders acceptance remain separate.

## I/O & Edge-Case Matrix

| Input/state | Expected behavior |
|---|---|
| Complete original owner beyond 10,000 | Complete selected generation; bounded values |
| Exact retry; changed earlier identity | Duplicate proof; durable conflict without acknowledgement |
| Growth; independent owners | Complete subsequent rebuild and catch-up |
| Gap, alias, missing page or stale capture | Fail closed; preserve selected state |
| Concurrent writers; interrupted promotion; new process | Durable handoff resumes without losing fences or inherited proof |

</frozen-after-approval>

## Code Map

Repository-relative directory aliases below identify exact task locations:

- **Client:** `src/Hexalith.EventStore.Client/Projections/`. `SharedProjectionEpochCoordinator.cs` contains caps at 413/547, proof at 845/1917 and checkpoint copies at 2556/3241. Reuse retained batch recovery; preserve `ReadModelBatchProtocol.cs` limits.
- **Domain:** `src/Hexalith.EventStore.DomainService/`. `DomainSharedProjectionRebuildDispatcher.cs` allocates whole-owner arrays/set around 459, writes manifests around 583/1000 and promotes/seeds around 2747/2807. Reuse bounded transport parts and incremental fingerprints.
- **Server:** `src/Hexalith.EventStore.Server/Projections/`. `SharedProjectionSourceBeginProducer.cs` and `SharedProjectionSourceRosterActor.cs` independently cap captured owner sequence at 10,000; retain authoritative actor cuts and bounded transport.

## Tasks & Acceptance

**Execution:**
- [x] Client: add `SharedProjectionCapturedIdentityReference.cs`, `SharedProjectionCapturedIdentityPage.cs` and `SharedProjectionCapturedIdentityStore.cs` for sealed bounded storage, complete-prefix comparison and retirement.
- [x] Client: add `SharedProjectionEpochCoordinator.CapturedIdentity.cs`; update coordinator and `SharedProjectionCheckpoint.cs` for reference lookup, journal/catch-up proof, ordinary checkpoint preservation and fenced seeding. Preserve existing callable signatures and legacy inline decoding.
- [x] Domain: update dispatcher and `DomainSharedProjectionStoredPartitionManifest.cs` for bounded reference handoff, incremental position/message validation and retained evidence past scratch cleanup. Derive transport-part admission from configured event admission instead of a fixed owner-history ceiling.
- [x] Server: remove capture-specific sequence ceilings from producer/roster; preserve actor authority, append/erasure holds and source validation. Add beyond-boundary cases to the corresponding `tests/Hexalith.EventStore.Server.Tests/Projections/SharedProjectionSource*Tests.cs` files.
- [x] Add `tests/Hexalith.EventStore.Client.Tests/Projections/SharedProjectionBoundedIdentityTests.cs` and `tests/Hexalith.EventStore.DomainService.Tests/DomainPartitionedSharedProjectionRebuildTests.BoundedIdentity.cs`: exercise the matrix through persisted end-state and public dispatcher, including legacy/malformed prefixes and cross-page message duplicates.
- [x] Add `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionBoundedIdentityLiveTests.cs` and separate worker types: Dapr-backed production dispatch, interrupted staging/promoted handoff, actual process replacement and distinct PID evidence.
- [x] Add `tools/qualify-bounded-owner-identity.py`; retain exact commands, logs/results, source/PE/PDB checksums, runtime source checks and measured physical value/batch maxima under `_bmad-output/implementation-artifacts/evidence/folders-12-2-bounded-owner-identity/`. Document supported Folders consumption and reader rollout/rollback in `docs/guides/named-projection-authoring.md`.

**Acceptance Criteria:**
- Given 10,001 and 32,769 events on one original owner, when the production dispatcher rebuilds, ordinary growth reaches 32,770 and rebuilding repeats, then all required views, checkpoints and historical duplicate proofs remain correct with bounded storage and batches.
- Given multiple owners and changed early/middle/final positions, payloads or message identities, when retry, promotion or seed runs, then exact duplicates succeed, every contradiction fails closed, and no owner's proof substitutes for another's.
- Given writer races, interrupted staging/promotion or process replacement, when operations resume, then selected/inherited reads and catch-up remain correct, both epoch/checkpoint CAS fences hold, and cleanup preserves referenced proof.

## Implementation Notes

2026-10-21: Human approved this spec and directed implementation to continue. Work and evidence belong to this EventStore repository. Preserve the frozen intent and all pre-existing work; no Git staging or commits are authorized.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence and decision |
| --- | --- | --- |
| Blind 1: Git normalizes bound evidence | medium / patch | Generated sources contain CRLF while global C# attributes normalize to LF. Preserve this story's evidence bytes with a narrow attribute override; check filtered/raw hashes without staging. |
| Blind 2: legacy seed replaces paged proof | medium / patch | The legacy branch validates a paged prefix, then can write an inline checkpoint. Preserve a fully matching paged reference; require the bounded API to extend paged evidence. Fresh legacy inline input remains subject to unchanged batch limits; introduce no domain-history cap. |
| Blind 3: aborted page inventories retain complete identity arrays | medium / patch | Retirement erases nodes/messages but retains their full inventory copies. Reclaim each page inventory after its referenced records are erased; retain the reservation retirement fence and prove retry safety. |
| Blind 4: retiring an old reservation after a later epoch | low / rejected | The coordinator deliberately cannot infer whether an older reference is inherited selected proof. Production abort retires before reopening and tests protect inherited references. Supporting arbitrary late direct-API retirement requires a new eligibility protocol; uncommon misuse of the required ordering does not justify that complexity. Clarify the ordering in API documentation. |
| Blind 5: serial message-index read cost | low / rejected | The read cost is real and the packet measures phase/run elapsed time. There is no supplied latency SLA; the named gateway explicitly configures infinite client timeout. Bulk/concurrent I/O changes recovery behavior beyond a direct correction. Retain observed timings and the operational limitation. |
| Blind 6: combined real source-actor/HTTP live path | low / rejected | The Dapr lane explicitly calls the real public production dispatcher; it does not claim full HTTP/Folders acceptance. Producer and actual roster actor boundary behavior passed separately. A combined host/HTTP lane requires substantial new infrastructure beyond this SDK dispatcher qualification. |
| Blind 7: live stage exits after completion | medium / patch | Worker Stage returns Completed before exiting, so the live interruption claim is too broad. Interrupt a durable staging transition before completion and resume it in the next process. Group with Edge 2. |
| Blind 8: partial multi-owner handoff untested live | medium / patch | The seed crash occurs before the first owner participant. Move the interruption to the second owner's seed after installing its epoch participant; prove the first owner's checkpoint and handoff fence survive. |
| Blind 9: buffered negative cases absent | medium / patch | The buffered contract has a separate input preparation path. Add changed-prefix, cross-page alias and abort/reclamation assertions for that contract, respecting early rejection before reservation where applicable. |
| Blind 10: successful >10,000 transport parts absent | low / rejected | The named test claims configured admission and proves it. Source production pages contain up to 256 events and the tested histories require far fewer parts; reconstruction uses the configured count with no other fixed 10,000 part cap. A full many-thousand-request lane is disproportionate to this guard edit. Add cheap explicit configured-boundary rejection assertions. |
| Edge 1: older receipt poisons legacy checkpoint migration | high / patch | Reviewer reproduced sequence-3 legacy proof being copied into indexed sequence 1, then valid growth fails validation. EnsureCheckpoint must use the authoritative retained checkpoint for position/token/sequence checks before writing; add the demonstrated replay/growth regression. |
| Edge 2: partial staging claim unsupported | medium / patch | Same missing process interruption as Blind 7; preserve the three-process lane while interrupting before Stage completes. |
| Verification 1: no installed-record erasure assertions | medium / patch | Pre-verified regression gap: omitting EraseAsync would still pass fence-only tests. Assert multi-leaf/index node, message-index and page-inventory removal after abort and retirement retry, while retaining the fence. This is a separate verification correction alongside Blind 3. |
| Verification 2: pending-write head above 10,000 untested | medium / patch | Pre-verified regression gap: the large-owner test's first capture succeeds, bypassing the other removed guard. Exercise initially blocked capture plus pending-write reconciliation at both boundary sizes and preserve complete transport assertions. |

All layer findings were classified before grouping. No intent/spec loopback is needed: surviving
fixes are direct corrections or demonstrated regression tests, with no new public surface.
The starting full packet remains preserved as pre-review evidence; it is superseded for final
acceptance once the corrected source/binary graph is requalified.

## Design Notes

Select an additive versioned schema: immutable leaves of at most 128 entries, bounded content-addressed index nodes and a constant-size authenticated root/count reference. Bind namespace to scope, source, operation and epoch. Entries retain original position, sequence and canonical digest. An operation-scoped durable message-hash index detects distant duplicates. Compare evolved folding passes with stored pages; never allocate whole-owner arrays/set in the streamed path.

Seal complete evidence before publishing its reference. Compare every retained prefix page, including partial leaves. Seed via a recoverable batch matching checkpoint and current epoch ETags after pending-batch reconciliation and rereads. Hold handoff until all owners are seeded. Preserve references during ordinary growth and inherited generations. Give identity evidence a durable retirement inventory independent of scratch; abort retirement must prevent delayed installers from recreating deleted evidence. Missing/corrupt nodes fail closed. New readers support inline history; old readers cannot safely consume paged checkpoints.

## Verification

Build affected test projects individually in Debug/source mode with `-m:1 -p:UseHexalithProjectReferences=true`. Run focused xUnit classes via built assemblies, then complete affected suites and opted-in Dapr/process lanes. Record exact final commands, failures and successful results separately. Verify portable PDB document checksums against captured sources and hash actual runtime/worker binaries; keep differing build graphs distinct. Exact source qualification and the broader regression limitation are recorded below.

### Parent acceptance audit — 2026-10-21

Implemented all seven tasks. Independently inspected the source/test diff against the baseline,
checked final XML for every frozen matrix row, and verified the 50 mandatory cases all ran and passed.
All four Dapr parent cases passed across three distinct worker PIDs each, for streamed and existing
Folders buffered-owner contracts at 10,001 and 32,769 events, growth and repeated rebuild.

[Qualification and consumption report](evidence/folders-12-2-bounded-owner-identity/README.md) and
[parent audit](evidence/folders-12-2-bounded-owner-identity/parent-acceptance-audit.json) bind the final
packet. Its 96 file hashes, 3,654 source hashes and 86 PE/PDB hashes match; no runtime/source drift
was found. Physical/canonical maximum: 110,556 bytes; maximum batch: 68 operations.

The complete broad Server suite ran and remains red: 6,216 passed, 6 failed, 25 skipped.
All required Server boundary cases passed. The six failures involve baseline-identical secret-scan
inputs or manifest-test setup, independently confirmed with a zero-byte diff for all 137 implicated
paths. Client: 1,930 passed, two existing isolated-Dapr opt-in skips. Domain: 1,091 passed.
No matrix-covering test was skipped. The packet correctly retains `qualified: false` for the
overall regression gate; this story does not claim package/release or Folders acceptance.

The all-inclusive unstaged diff (including untracked evidence) was archived at
`/tmp/eventstore-bounded-owner-all-ov8ud3w0.diff`; the source-only view was read alongside the
underlying source/test files. No Git index changes were made.

### Review corrections — 2026-10-21

All patch findings are corrected. Parent inspected the production and test changes against the
pre-review captured sources. Focused results: Client 63/63, Domain bounded 33/33, producer 5/5,
real-process canaries 2/2; no skips. The canaries now stop after a staging participant is installed
before stage completion and after the second owner's epoch participant while its checkpoint is
absent. First-owner seed, held Backpressure fence and unchanged pending epoch are explicitly proved.
Generated-source raw/filtered Git hashes match under the exact-byte evidence attribute.

The mandatory full matrix now contains 74 cases. Full source/binary requalification follows;
pre-review packet and focused correction evidence remain unchanged.

### Final post-review qualification — 2026-10-21

Completed all implementation, review corrections and source-bound capability qualification.
The final packet is `run-20261021T152119Z`: 74/74 mandatory cases passed, no required skips or
qualification errors, and six Dapr scenarios each used three distinct processes. All 108 packet
hashes, 3,654 current source hashes and 86 PE/PDB hashes match. Maximum batch: 68 operations /
110,556 bytes; identity 12,302 bytes, checkpoint 1,696 bytes and owner manifest 1,258 bytes.

The complete regression gate remains failed and the packet correctly retains `qualified: false`.
Client: 1,936 passed / 2 existing skips. Domain: 1,104 passed / one unchanged process-sweep test
failed; that exact test passed a single isolated retry against the same binaries. Server: 6,218
passed / six baseline-identical failures / 25 existing skips. Process scenarios: 6/6 passed.
The full-run failures were preserved; no unrelated runtime, tests or historical evidence were altered.

[Final report and supported consumption](evidence/folders-12-2-bounded-owner-identity/README.md) and
[post-review parent audit](evidence/folders-12-2-bounded-owner-identity/parent-acceptance-audit-postreview.json)
record all five matrix rows, exact runtime PIDs, byte bounds, baseline comparisons and the isolated
retry. All 14 review findings were triaged; direct corrections were verified, remaining low findings
were rejected with evidence. No work was deferred and no owned finding remains open.

The workflow commit instruction was not performed because the user explicitly prohibited commits.
No Git staging, commits, push, publication, deployment or dependency pin changes were performed.
Package/release qualification and Folders acceptance remain separate.

Final complete unstaged diff, including all untracked evidence, archived at `/tmp/eventstore-bounded-owner-final-fipq0ags.diff`.
