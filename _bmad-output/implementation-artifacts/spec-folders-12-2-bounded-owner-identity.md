---
title: 'Folders 12.2 prerequisite: bounded captured owner identity'
type: 'feature'
created: '2026-10-21'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: ae917d3e4dbab20b8982c5222a13611c3e3faf35
approved: '2026-10-21'
context: []
---

> **Resumption note — 2026-10-29:** The implementation and regression tests are
> already present. The later approved [evidence-storage simplification](spec-evidence-storage-simplification.md)
> supersedes this spec's qualification-script and committed-evidence requirements:
> commit `6a0b6441f6d78a3007678a1d0ec336e9cb8ff961` deliberately removed
> `tools/qualify-bounded-owner-identity.py` and the linked evidence packets.
> Preserve the historical results below as historical claims; their packet links
> are no longer available in this checkout. Current verification uses repository
> tests and CI, with commands, counts, source revision and CI availability recorded
> here. Do not recreate the retired evidence machinery. Frozen behavioral intent,
> consumption boundaries and the prohibition on commits/publishing remain unchanged.

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

- 2026-10-29: Reconciled the resumed spec with the later owner-approved
  evidence-storage simplification. The seventh task's tool and packet retention
  describe the original delivery, not work to restore. Keep the implemented
  bounded storage, public transport, tests and rollout documentation; record new
  verification here under the current repository policy.

## Review Triage Log

### Resumption review — 2026-10-29

All three layers completed before classification. The verification-gap layer found
no gaps. Each blind/edge finding below was classified independently before grouping.
The full historical-baseline diff was retained at
`/tmp/bounded-owner-resume-wrdegjlp.diff`; its bounded source/test navigation slice
was `/tmp/bounded-owner-source-review.diff`.

| Finding | Verdict / route | Evidence and decision |
| --- | --- | --- |
| Blind R1: accepted ordinary tail can change in a subsequent rebuild | high / defer | The executable `/tmp/bounded-owner-review-repro` captured 65 events, accepted ordinary event 66, then promoted a different payload at 66. Baseline `ae917d3e4dbab20b8982c5222a13611c3e3faf35` already compared only the retained inline prefix and checkpoint position; this is an older receipt-to-rebuild consistency defect, not caused by bounded identity retention. |
| Blind R2: streamed retirement uses a smaller chunk-count bound | medium / defer | Both baseline and current dispatcher admit `3 * MaxRebuildEventCount + 8` streamed chunks but reject retired counts above `MaxRebuildEventCount`. The unchanged mismatch can block terminal cleanup retries for a legal multi-chunk owner. |
| Blind R3: worker hardcodes a Debug/source build graph | low / patch | `WriteEvidence` emits a fixed Debug/source claim even though Integration Tests runs Release/package mode. Remove that unsupported diagnostic; actual runtime PE/PDB hashes remain authoritative. |
| Blind R4: Windows document paths are filtered out | medium / patch | `Capture` checks slash-separated `/src/` and `/tests/` before separator normalization, while portable PDB names may contain backslashes. Normalize only the selection path, retaining the original path for source reads and evidence. Group with Edge R1. |
| Blind R5: missing ordinary source silently falls back to embedded content | medium / patch | `ReadDocumentSource` falls through to embedded content when a non-generated current source path is absent. That can conceal a deleted current source from the runtime-source check. Require ordinary source existence and retain embedded fallback for generated documents; add a missing-source regression. |
| Blind R6: non-paged history fallback can buffer a large owner | low / rejected | The fallback is a real compatibility behavior, but production registers `EventStoreProjectionDeliveryHistoryReader`, whose `ReadPagesAsync` streams bounded pages. Restricting custom legacy readers or adding producer admission settings introduces new policy/branches for an uncommon compatibility path; no production regression or missing matrix outcome was demonstrated. |
| Blind R7: buffered contribution hash arrays can exceed a batch | medium / defer | Buffered `AccumulatePartitionAsync` still stores all contribution hashes in one manifest and the unchanged canonical batch validator rejects oversized metadata. The same representation and failure already existed within the former event boundary when combined inline identities and many contribution hashes exceeded one MiB. Output-metadata paging is separate from this change's identity-vector replacement. |
| Blind R8: add process interruption during identity reservation/sealing | false / rejected | No broken recovery outcome was demonstrated. Existing bounded tests interrupt preparation/installation/commit, reconstruct the coordinator and verify retirement; the live lane independently proves interrupted staging and promoted handoff through distinct processes. Every frozen interruption matrix row already ran and passed. |
| Blind R9: add cancellation at every identity traversal | false / rejected | This is an additional test proposal without a demonstrated cancellation defect or uncovered frozen matrix outcome. Existing durable interruption/retry tests already verify retained evidence and cleanup; the proposal alone does not establish an implementation failure. |
| Blind R10: terminated phases record but do not assert bounds | low / patch | Stage/promote call `WriteEvidence` before terminating, whereas `AssertBounds` currently runs only in resume/inherited paths. Assert the existing bounds at the common evidence-write point so every emitted worker phase enforces them. |
| Edge R1: Windows PDB names skip source checksums | medium / patch | Independently confirmed the same separator-sensitive selection as Blind R4. Group their shared root cause into one path-normalization correction. |

The four surviving corrections affect only qualification test helpers, with no
public API or production protocol change. The three pre-existing runtime issues
are recorded separately for follow-up; their existence is not a regression waiver.

### Original implementation review

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

### Resumed verification — 2026-10-29

Source revision: `5945ba8b3575f76597075e460bb9a973453272ea`.
The bounded implementation, tests and consumption documentation already exist;
this resumption changes only this spec. Concurrent planning, production, test and
staged documentation changes from other sessions were preserved. This resumption
did not change pins, source code, the Git index or history.
CI run: not triggered or claimed; the results below are local Debug/source-mode
runs using SDK 10.0.403 and .NET runtime 10.0.14.

Build commands:

```bash
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
```

All four builds succeeded with zero warnings and zero errors:
`Hexalith.EventStore.Client.Tests`, `Hexalith.EventStore.DomainService.Tests`,
`Hexalith.EventStore.Server.Tests`, and `Hexalith.EventStore.Server.LiveSidecar.Tests`.

Direct assembly verification commands (from the repository root):

```bash
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml /tmp/bounded-client.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainPartitionedSharedProjectionRebuildTests' -result-xml /tmp/bounded-domain.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SharedProjectionSourceBeginProducerTests' -class '*SharedProjectionSourceRosterActorTests' -result-xml /tmp/bounded-server.xml
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml /tmp/bounded-runtime-proof.xml
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml /tmp/bounded-client-full.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml /tmp/bounded-domain-full.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml /tmp/bounded-server-full.xml
```

| Run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Client bounded identity class | 49 | 0 | 0 |
| Domain partitioned rebuild class | 144 | 0 | 0 |
| Server source producer/roster classes | 30 | 0 | 0 |
| Runtime source-binding tests | 3 | 0 | 0 |
| Full Client suite | 2,248 | 1 | 2 |
| Full Domain suite | 1,276 | 0 | 1 |
| Full Server suite | 6,403 | 15 | 25 |

The broad gate remains failed, so status stays `in-progress`. Client's failure is
`DaprRoutingHeaderOwnershipGuardTests.HostSources_DelegateDaprRoutingHeaderOwnershipToThePlatformHandlerOnly`:
the guard detects header mutation in the existing `DaprRouteCatalogStateStore`.
Server failures are in `DaprInternalAuthenticationHandlerTests` (2),
`ActorBasedAuthIntegrationTests` (2), `QueryResponseProvenancePersistenceTests` (7),
`InteractionProtectionPersistenceTests` (2), `DeletionConsumptionOriginalTests`
(1), and `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`
(1). These are failures of the unchanged checkout outside the bounded-owner test
classes; this resumption neither fixes nor waives them. Client's two isolated-Dapr
skips and Domain's `EVENTSTORE_CONTENT_LIVE=1` content-persistence skip are separate
lanes. Historical 2026-10-21 counts above do not describe this source revision.

Live command:

```bash
BOUNDED_IDENTITY_EVIDENCE_DIR=/tmp/bounded-identity-current BOUNDED_IDENTITY_GENERATED_SOURCE_DIR=/tmp/bounded-identity-current/generated dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityLiveTests' -result-xml /tmp/bounded-live.xml
```

Three required large-owner cases completed: streamed 10,001, streamed 32,769 and
buffered 10,001. Each passed staging interruption, partial multi-owner handoff,
recovery, ordinary growth and repeated rebuild through three distinct processes.
The nine completed worker records passed runtime PE/PDB and current-source
checks; maximum observed physical/canonical batch bytes were 110,620, maximum
batch operations 68, identity bytes 12,302, checkpoint bytes 1,696 and owner
manifest bytes 1,254. These measurements cover only the completed cases.

**Current live blocker:** concurrent production edits occurred after the builds.
The buffered 32,769 worker, followed by inherited-owner canaries, correctly failed
with `Runtime PDB source differs: /home/administrator/projects/hexalith/eventstore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceEndpointInventory.cs`.
The remaining live run was stopped because it could no longer bind results to
one unchanged source/binary graph. Final XML reports three passed and four failed:
three source-drift failures (buffered 32,769 and both small inherited-owner cases)
plus one canceled 10,001-event omitted-owner case. Two focused process-replacement
canaries were not executed before cancellation; they are not passes or skips.
The owned runner, worker, sidecar and temporary component directory were cleaned
up; external containers were left untouched. This is a source-drift qualification failure,
not evidence of a bounded-identity runtime defect. Do not extrapolate the three
completed cases to the entire current live matrix or to the concurrent edits.
No complete live acceptance, overall qualification, package/release readiness or
Folders acceptance is claimed. Rebuild and rerun the live class against a stable
source tree after the concurrent work settles; the broad failures above also
remain unresolved.

### Stable snapshot verification — 2026-10-29

The implementation, regression tests and reader rollout/rollback documentation were
already present. This run changes only this verification record; it preserves the
original `baseline_commit`, frozen intent and `in-progress` status. No bounded-owner
production defect was found. No source/test edits, dependency changes, Git index or
history mutations, publication, deployment or retired qualification-tool restoration
were performed by this run. CI run/link: none for this run; these are local results
using SDK 10.0.403, .NET 10.0.14 and Dapr runtime 1.18.7.

Origin HEAD: `8348bc491ace70dbd0d84c2320a6e4372479bbb9`. Concurrent dirty changes
belonging to other sessions were preserved. Shared-checkout builds used:

```bash
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
```

All four ultimately succeeded with zero warnings/errors. The first Server build
failed with three `CS0246` errors while a concurrent authentication change referenced
the not-yet-created `EventStoreHumanCredentialRequirement` in
`EventStoreHttpHostSecurityExtensions.cs:54` and `EventStoreHttpEndpointInventory.cs:36,109`.
The unchanged command passed after that session supplied the type. Its initial and
retry logs remain separately under `/tmp/bounded-20261029-server-build.log` and
`/tmp/bounded-20261029-server-build-retry.log`.

The following direct-assembly commands ran from the original repository root:

```bash
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml /tmp/bounded-20261029-client.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainPartitionedSharedProjectionRebuildTests' -result-xml /tmp/bounded-20261029-domain.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SharedProjectionSourceBeginProducerTests' -class '*SharedProjectionSourceRosterActorTests' -result-xml /tmp/bounded-20261029-server.xml
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE=/tmp/bounded-20261029-runtime.json dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml /tmp/bounded-20261029-runtime.xml
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml /tmp/bounded-20261029-client-full.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml /tmp/bounded-20261029-domain-full.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml /tmp/bounded-20261029-server-full.xml
```

| Shared-checkout run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Client bounded identity class | 49 | 0 | 0 |
| Domain partitioned rebuild class | 144 | 0 | 0 |
| Server source producer/roster classes | 30 | 0 | 0 |
| Runtime source-binding preflight | 3 | 0 | 0 |
| Full Client suite | 2,248 | 1 | 2 |
| Full Domain suite | 1,286 | 0 | 1 |
| Full Server suite | 6,418 | 13 | 25 |

These shared runs used their earlier successful Debug/source builds. Another session
was observed running a Server test build during the full Server run; production
and test sources also continued changing. The broad counts are observed test-process
outcomes, not qualification of one fixed source/binary graph or of later edits.

Client's failure is
`DaprRoutingHeaderOwnershipGuardTests.HostSources_DelegateDaprRoutingHeaderOwnershipToThePlatformHandlerOnly`
(the additional setter is `DaprRouteCatalogStateStore`). Server's failures are
`CustomHostActorCallbackRateLimitTests.RawDaprMappingUsesTrustedDelegateProvenanceForIndependentBoundedQuotasAsync`,
`DeletionConsumptionOriginalTests.UnavailableCurrentAuthorityOrUnsettledStateCannotReleaseOriginal(condition: "restored")`,
`SignalRRuntimeProofMetadataTests.BroadcastReceipt_ReducedSerializedCap_MatchesDeliveredMetadata`,
`SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`, seven
`QueryResponseProvenancePersistenceTests.PersistedFreshness_TraversesRouterHandlerControllerAndGatewayClient`
cases, and `ActorBasedAuthIntegrationTests.ActorRbacValidation_CommandMessageCategoryPassedCorrectly`
plus `ActorTenantValidation_Authorized_CommandAccepted`. No bounded matrix test failed.
These failures were retained without attributing them to this feature, fixing foreign
work or waiving the regression gate. Client's two isolated-Dapr skips, Domain's one
content-live skip and Server's 25 existing opt-in/ATDD skips are separate from the
required bounded matrix.

The initial shared-tree live command was:

```bash
BOUNDED_IDENTITY_EVIDENCE_DIR=/tmp/bounded-20261029-live BOUNDED_IDENTITY_GENERATED_SOURCE_DIR=/tmp/bounded-20261029-live/generated dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityLiveTests' -result-xml /tmp/bounded-20261029-live.xml
```

Its first streamed 10,001-event worker correctly rejected concurrent edits with
`Runtime PDB source differs: /home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/RouteCatalogDevelopmentRedis.cs`.
The unqualifiable run was canceled and its owned processes stopped. Its XML records
zero passed/two failed: that source-drift failure and the canceled streamed 32,769
case. The remaining seven cases did not run; they are not passes or skips.

To complete the live matrix independently of ongoing edits, the complete current
source/build-input tree and root-declared submodule sources were copied to
`/tmp/bounded-owner-20261029-snapshot` using:

```bash
rsync -a --exclude='.git' --exclude='bin/' --exclude='obj/' --exclude='node_modules/' --exclude='.agents/' --exclude='.codex/' --exclude='.claude/' --exclude='.venv/' --exclude='_bmad/' --exclude='_bmad-output/' --exclude='evidence/' --exclude='/references/*/references/' /home/administrator/projects/hexalith/eventstore/ /tmp/bounded-owner-20261029-snapshot/
```

Source bytes were independent copies; no branch or nested submodule was created,
initialized or updated. Earlier whole-tree captures caught two transient foreign
compile failures with the same LiveSidecar build command: `CS0104` for ambiguous
`IServer` in `HttpHostSecurityLiveTests.cs:112`, then `CS1929` for comparer-based
`Contains` without LINQ in `EventStoreDomainServiceSecurityExtensions.cs:116`.
Those failed logs remain `/tmp/bounded-20261029-snapshot-live-build.log` and
`/tmp/bounded-20261029-snapshot-live-build-retry.log`. No foreign source was patched
and no mixed production revision was constructed. After the broad runs, the whole
tree was recaptured and its unchanged build command passed with zero warnings/errors
(`/tmp/bounded-20261029-snapshot-live-build-final.log`).

The successful capture is bound to:

- UTC capture: `2026-10-29T18:24:23.545301+00:00`.
- Origin HEAD: `8348bc491ace70dbd0d84c2320a6e4372479bbb9`, including the then-current
  tracked and untracked dirty overlay, rather than HEAD alone.
- Tracked overlay diff SHA-256:
  `5dfd55c270f69426be663419374a2e26ebde7c9c4b41c571a23770389e268eaa`.
- Complete 19,990-file input manifest SHA-256 (compact, key-sorted `sourceFiles` JSON):
  `71ddf0ae753e4f4c9dfee7145f05370b5b83f3ad4bd3e7c53398ae17aacbad7e`.
  All copied file hashes matched the origin at capture and remained unchanged after
  the live run. The manifest includes untracked file content hashes; the tracked
  diff hash alone does not cover that overlay.
- Origin/status/submodule revisions and file hashes:
  `/tmp/bounded-20261029-snapshot-origin.json`; tracked overlay:
  `/tmp/bounded-20261029-origin.diff`. Earlier failed captures remain separately
  suffixed `-initial` and `-second`.

The following commands ran from `/tmp/bounded-owner-20261029-snapshot`:

```bash
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE=/tmp/bounded-20261029-snapshot-runtime.json dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml /tmp/bounded-20261029-snapshot-runtime.xml
BOUNDED_IDENTITY_EVIDENCE_DIR=/tmp/bounded-20261029-snapshot-live BOUNDED_IDENTITY_GENERATED_SOURCE_DIR=/tmp/bounded-20261029-snapshot-live/generated dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityLiveTests' -result-xml /tmp/bounded-20261029-snapshot-live.xml
```

Snapshot preflight: **3 passed, zero failed/skipped/errors**. Complete live class:
**9 passed, zero failed/skipped/errors**, `2026-10-29T18:24:43.1600176+00:00` through
`2026-10-29T19:06:34.8697013+00:00` (2,511.813 seconds). Each scenario completed all
required phases; all 30 worker PIDs were distinct:

| Owner events | Contract | Inherited replay | Stage / promote / resume / inherited PIDs |
| ---: | --- | --- | --- |
| 129 | Streamed | No | 1527132 / 1529234 / 1529332 |
| 129 | Buffered | No | 1532201 / 1533697 / 1534235 |
| 129 | Streamed | Yes | 1513076 / 1515978 / 1516077 / 1519715 |
| 129 | Buffered | Yes | 1520365 / 1522427 / 1522948 / 1527054 |
| 10,001 | Streamed | No | 1536814 / 1579747 / 1580759 |
| 32,769 | Streamed | No | 1625865 / 1758982 / 1760674 |
| 10,001 | Buffered | No | 1880261 / 1913172 / 1913494 |
| 32,769 | Buffered | No | 1948531 / 2068696 / 2069573 |
| 10,001 | Buffered | Yes | 2177267 / 2208633 / 2209151 / 2243233 |

All four large-owner scenarios proved durable interrupted staging, partial second-owner
handoff, recovery, ordinary growth (including 32,770), repeated rebuild and historical
duplicate/conflict proof. Inherited cases additionally proved unchanged replay from
an omitted original owner in a fourth process.

All 30 records identify one runtime graph, whose four actual PE/PDB pairs match their
recorded hashes and whose 1,523 ordinary/embedded-generated source document SHA-256
values match the captured sources. Compact, key-sorted runtime-proof JSON SHA-256:
`3c8c02de3004d8d92e169925760aae9ddc4292f8ac6b17c24afbfb71b5163ac3`.
Maximum physical value/canonical batch: **110,620 bytes**; maximum batch: **68
operations**; identity: **12,302 bytes**; checkpoint: **1,696 bytes**; owner manifest:
**1,258 bytes**. The 100-operation/one-MiB limits remain satisfied. Worker records and
generated-source captures remain local under `/tmp/bounded-20261029-snapshot-live`;
the final local audit is `/tmp/bounded-20261029-final-audit.json`.

The frozen matrix maps to actual passing current results as follows. Every named
unit method below was found as `Pass` in the focused XML; none was skipped. The live
methods all passed in the final snapshot XML.

| Frozen matrix row | Passing coverage |
| --- | --- |
| Complete original owner beyond 10,000; bounded values | Client `CompleteOriginalOwnerUsesBoundedNodesAndRetainsHistoricalProofAfterGrowth`; Domain `ProductionDispatcherRebuildsGrowsAndRebuildsCompleteOriginalOwner`; Server `OriginalOwnersStreamBeyondTheFormerCaptureSequenceBoundary` and `AuthoritativeCaptureRetainsOriginalOwnerBeyondFormerSequenceBoundary`; all four large-owner live cases. |
| Exact retry; changed earlier identity | Client `EveryRetainedPrefixPageIncludingPartialLeafIsComparedBeforePromotion`; Domain `DispatcherRejectsChangedRetainedEarlyMiddleAndFinalIdentitiesBeforePromotion`; exact historical retry/conflict assertions in the complete-owner and live cases. |
| Growth; independent owners | Client `MultipleOriginalOwnersKeepDistinctProofAndCoveredPendingJournalCatchUp`; Domain complete-owner rebuild/growth cases; all four large-owner live cases and the three inherited-owner live cases. |
| Gap, alias, missing page or stale capture | Client `DistantMessageDuplicatesMissingAndCorruptNodesFailClosed` and `InheritedOwnerKeepsExactNextSequenceAndGapChecks`; Domain `DispatcherRejectsCrossPageMessageAliasAndAbortRetainsDurableRetirementInventory` and `StaleCaptureIsRejectedBeforePromotionAndFreshRetryCanCommit`. |
| Concurrent writers; interrupted promotion; new process | Client `ConcurrentEpochChangeCannotBeOverwrittenByAStaleCheckpointSeed` and `AbortRetiresPreparedPagesWithoutAllowingDelayedInstallers`; Domain `DispatcherCommitKeepsDeliveryAndNextRebuildFencedUntilSeedingCompletes`; all nine live process-replacement cases. |

Fixture cleanup completed without xUnit errors; the snapshot runner, all 30 workers
and its sidecar targeting app port 33641 have exited. The five pre-existing Docker
containers remained running; this run did not restart or remove them. The snapshot
and local results were retained for inspection.

The full live matrix is now verified for this exact captured dirty source/binary
graph, superseding the earlier source-drift blocker only for that graph. It does not
qualify subsequent concurrent edits. The broader regression gate remains red, so
status remains `in-progress`; no overall regression qualification, package/release
readiness or Folders acceptance is claimed.

### Clean-checkout resumption and verification — 2026-10-29

This resumption started from clean `main` at
`7e136f7e0a10140e35d86510423d58eaaa4392c9`. The complete spec was read;
frontmatter `context: []` contains no additional files to load. Inspection found
the bounded implementation, regression coverage and consumption/rollback guide
already present, with no owned defect requiring a source or test change. This
run changes only this verification record and preserves the frozen intent,
original `baseline_commit` and `in-progress` status. No dependency pins, Git
index/history, publication or deployment were changed, and the retired
qualification tool/evidence packets were not recreated. CI run/link: none;
these are local SDK 10.0.403 / .NET 10.0.14 results.

Before the builds, all 19,990 files in the retained snapshot input manifest were
rehashed against the checkout. Only the ext-protection Python test, its two
Python cache files and its acceptance document differed; captured .NET source
and build inputs matched. A follow-up addition inventory found no added
EventStore-owned source/build/config input; 134 new Platform submodule files and
two McpCli test files were outside the bounded runtime build graph. The local
comparison record is `/tmp/bounded-7e136f7e0-input-comparison.json`. This establishes
equivalence of the participating inputs at the audit time, not equality of the
entire workspace or of subsequent concurrent edits.

All four project builds below succeeded with zero warnings/errors. Commands ran
sequentially from the repository root; their separate logs are
`/tmp/bounded-7e136f7e0-{client,domain,server,live}-build.log`.

```bash
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
```

The following direct-assembly commands ran sequentially against those builds:

```bash
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml /tmp/bounded-7e136f7e0-client.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainPartitionedSharedProjectionRebuildTests' -result-xml /tmp/bounded-7e136f7e0-domain.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SharedProjectionSourceBeginProducerTests' -class '*SharedProjectionSourceRosterActorTests' -result-xml /tmp/bounded-7e136f7e0-server.xml
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE=/tmp/bounded-7e136f7e0-runtime.json dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml /tmp/bounded-7e136f7e0-runtime.xml
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml /tmp/bounded-7e136f7e0-client-full.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml /tmp/bounded-7e136f7e0-domain-full.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml /tmp/bounded-7e136f7e0-server-full.xml
```

| Run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Client bounded identity class | 49 | 0 | 0 |
| Domain partitioned rebuild class | 144 | 0 | 0 |
| Server source producer/roster classes | 30 | 0 | 0 |
| Runtime source-binding preflight | 3 | 0 | 0 |
| Full Client suite | 2,248 | 1 | 2 |
| Full Domain suite | 1,286 | 0 | 1 |
| Full Server suite | 6,418 | 13 | 25 |

All runs reported zero xUnit errors and zero not-run tests. Focused runs and the
Domain suite exited 0; full Client and Server exited 1. Every frozen-matrix unit
method named in the preceding verification table was present and passed,
including all boundary and mutation theory rows. The focused checks completed
between `2026-10-29T19:58:11.7337115+00:00` and
`2026-10-29T20:02:55.4205762+00:00`; broad suites completed by
`2026-10-29T20:12:32.9764694+00:00`. Logs share each XML's basename with `.log`.

Client's failure remains
`DaprRoutingHeaderOwnershipGuardTests.HostSources_DelegateDaprRoutingHeaderOwnershipToThePlatformHandlerOnly`
(the additional setter is `DaprRouteCatalogStateStore`). Server's 13 failures
match the preceding stable-snapshot section: the callback quota, restored
deletion, SignalR metadata-cap and secret-guard cases, seven query-provenance
cases and two actor-auth cases. No bounded-owner test failed. Existing skips
remain the two Client isolated-Dapr tests, one Domain content-live test and 25
Server opt-in/ATDD cases. These failures were preserved without changing foreign
authentication, routing or secret-scan work or waiving the broad regression gate.

The fresh preflight authenticated 1,523 source-document records and the four
loaded PE/PDB pairs. Those files still matched their recorded hashes after the
broad runs. Compact, key-sorted preflight JSON SHA-256:
`9d2ddbf678c22183f9442c630acdcc7b2b744eafb4cf1f4a3c49fe87705201b5`.
All four PE/PDB pairs differ from the retained snapshot binaries; source
equivalence does not establish binary equivalence. The prior complete live
matrix remains **9 passed, zero failed/skipped/errors** for its exact retained
snapshot graph, with the 30 distinct worker PIDs and bounded measurements
recorded above. That completed lane was independently checked and was not
rerun; it did not execute against the newly built root PE/PDB pairs.

After focused verification, unrelated authentication/rate-limit source and test
edits and submodule work resumed during the broad runs. The comparison record
separately retains the later source drift; no rebuild was performed to chase
those edits. The new counts are outcomes of the previously built test
assemblies, with source-inspection tests reading the shared checkout. They do
not qualify one final unchanged workspace graph or any later edits. Foreign
changes were preserved. The bounded implementation remains present and its
focused matrix passes, but the broad gate remains red; no overall regression,
package/release or Folders acceptance is claimed.


### Resumption review corrections and final audit — 2026-10-29

All three review layers completed. The triage table records all 11 findings
before grouping: four qualification-helper corrections were applied, three
pre-existing production issues were deferred to `deferred-work.md`, three
proposals were rejected, and the two Windows-path reports share one correction.
The verification-gap layer found no gaps. No production protocol or public SDK
surface changed in this resumption.

`ProjectionBoundedIdentityRuntimeProof` now normalizes separators for source
selection while retaining the original PDB path, and refuses missing ordinary
sources even when embedded bytes exist. Generated-source fallback remains
supported. The runtime-proof tests cover Windows and Unix selection plus missing
ordinary source. The process worker no longer emits the hardcoded `buildGraph`
claim and checks bounds at its common evidence-write point, including interrupted
stage/promote phases.

The following commands ran from the repository root. The first build failed with
`CA1062` on the new internal path predicate; its null guard was added and the same
build command passed with zero warnings/errors on retry. Both logs are retained.

```bash
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true > /tmp/bounded-review-fixes-build.log 2>&1
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true > /tmp/bounded-review-fixes-build-retry.log 2>&1
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE=/tmp/bounded-review-fixes-runtime.json dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml /tmp/bounded-review-fixes-runtime.xml > /tmp/bounded-review-fixes-runtime.log 2>&1
BOUNDED_IDENTITY_EVIDENCE_DIR=/tmp/bounded-review-fixes-live BOUNDED_IDENTITY_GENERATED_SOURCE_DIR=/tmp/bounded-review-fixes-live/generated dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*ProjectionBoundedIdentityLiveTests.FocusedProcessReplacementExercisesPartialStageAndOwnerHandoff' -result-xml /tmp/bounded-review-fixes-live.xml > /tmp/bounded-review-fixes-live.log 2>&1
```

Runtime-proof tests: **9 passed**. Streamed/buffered 129-event process-replacement
canaries: **2 passed**. Both XML files report zero failures, skips, errors and
not-run cases. The parent independently inspected these XMLs, all six distinct
stage/promote/resume worker PIDs, absence of `buildGraph`, every phase's measured
bounds, and all actual runtime PE/PDB hashes. All six records identify one graph
with 1,523 source documents; compact, key-sorted runtime JSON SHA-256 is
`5ac81cf64b2d9beda12c75b38e18e274407c9bdd104a5a32722f53083ab49003`.
Maximum physical/canonical batch: 55,055 bytes; maximum batch operations: 68;
identity: 10,366 bytes; checkpoint: 1,664 bytes; owner manifest: 1,246 bytes.
These measurements cover the two new canaries, not the complete large-owner lane.
The retained 9/9 large-owner/inheritance matrix remains tied to its earlier exact
snapshot graph; it was not rerun after the test-helper edits. The complete unit
suite outcomes above likewise remain evidence for their separately built graph;
unaffected unit suites were not repeated for qualification-helper-only changes.

The parent also independently checked the four earlier clean build logs, all
226 focused XML outcomes and every named frozen-matrix method, the three complete
suite XMLs, the retained 9/9 live XML and 30 distinct worker records, and preservation
of the frozen intent and original baseline. Final owned changes comprise the three
qualification test files, this spec and three appended deferred-work entries.
Foreign edits, submodule state and external Git history changes were preserved.
No Git staging, commit, push, publication, deployment or dependency change was made
by this run. The workflow's generic done/commit steps were not performed: the
explicit no-commit boundary and unresolved broader regression gate take precedence.
Status remains `in-progress`; no final workspace, package/release or Folders
qualification is claimed.

### Fresh resumption verification — 2026-10-30

The complete spec was read; `context: []` names no additional inputs. The bounded
implementation, regression tests and reader rollout/rollback guide are already
present. Inspection found no owned defect requiring a production or test change.
This resumption updates only this verification record, preserving the original
baseline, frozen intent and `in-progress` status. No pins, Git index/history,
publication, deployment or retired qualification machinery were changed by this
task. CI run/link: none; these are local SDK 10.0.403 / .NET 10.0.14 results.

Shared-checkout builds began at `52fae16c7463621c82cb7ea266823852640b4738`
with pre-existing security/integration edits. Another session advanced HEAD to
`d98b014a3821dcef17709ff7a6c87fd6903e6c6b`; that external work was preserved.
All four commands below passed with zero warnings/errors. Separate logs are
`/tmp/bounded-20261030-{client,domain,server,live}-build.log`.

```bash
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
```

The following direct-assembly commands ran against those builds. Each XML's
basename also identifies its `.log` file. Independent test processes overlapped;
no shared-checkout build was started while these tests ran.

```bash
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml /tmp/bounded-20261030-client.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainPartitionedSharedProjectionRebuildTests' -result-xml /tmp/bounded-20261030-domain.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SharedProjectionSourceBeginProducerTests' -class '*SharedProjectionSourceRosterActorTests' -result-xml /tmp/bounded-20261030-server.xml
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE=/tmp/bounded-20261030-runtime.json dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml /tmp/bounded-20261030-runtime.xml
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml /tmp/bounded-20261030-client-full.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml /tmp/bounded-20261030-domain-full.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml /tmp/bounded-20261030-server-full.xml
```

| Shared-checkout run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Client bounded identity class | 49 | 0 | 0 |
| Domain partitioned rebuild class | 144 | 0 | 0 |
| Server source producer/roster classes | 30 | 0 | 0 |
| Runtime source-binding preflight | 9 | 0 | 0 |
| Full Client suite | 2,248 | 1 | 2 |
| Full Domain suite | 1,286 | 0 | 1 |
| Full Server suite | 6,421 | 12 | 25 |

All runs reported zero xUnit errors and not-run cases. Focused runs and full
Domain exited 0; full Client and Server exited 1. Broad runs finished by
`2026-10-30T06:02:49.3195883+00:00`. Client's failure remains the routing-header
ownership guard. Server failures remain the callback-quota case, SignalR
metadata-cap case, secret guard, seven query-provenance cases and two actor-auth
cases named in the preceding verification records; the former restored-deletion
failure passed in this run. No bounded-owner test failed. Existing separate-lane
skips remain two Client isolated-Dapr tests, one Domain content-live test and 25
Server opt-in/ATDD cases. Foreign failures were preserved without weakening the
broad gate.

The fresh shared preflight authenticated 1,523 source-document records and four
loaded PE/PDB pairs. All eight actual image/symbol files still matched their
recorded hashes after the broad runs. Compact, key-sorted proof JSON SHA-256:
`1d28ef8f03f7233c3e23b33e577ddfa88c9007dafc661b169cb01758b51c31ef`.
These results describe their built assemblies and source checks at execution
time; they do not qualify subsequent shared-checkout edits.

To isolate the long live lane from ongoing external edits, the current complete
source/build-input tree and root-declared submodule sources were copied using:

```bash
rsync -a --exclude='.git' --exclude='bin/' --exclude='obj/' --exclude='node_modules/' --exclude='.agents/' --exclude='.codex/' --exclude='.claude/' --exclude='.venv/' --exclude='_bmad/' --exclude='_bmad-output/' --exclude='evidence/' --exclude='/references/*/references/' /home/administrator/projects/hexalith/eventstore/ /tmp/bounded-owner-20261030-snapshot/
```

The capture at `2026-10-30T05:55:54.571314+00:00` matched all 20,427 copied
files against clean origin `d98b014a3821dcef17709ff7a6c87fd6903e6c6b`.
`/tmp/bounded-20261030-snapshot-origin.json` retains the complete input manifest;
compact, key-sorted `sourceFiles` JSON SHA-256 is
`5e1f53b92a693554cb1d32b3093d5fcd6d2f92c3ff49bf4aa808f46fa0a11460`.
Every captured input remained unchanged after the unit runs. No branch or nested
submodule was created, initialized or updated.

The following commands ran from `/tmp/bounded-owner-20261030-snapshot`:

```bash
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE=/tmp/bounded-20261030-snapshot-runtime.json dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml /tmp/bounded-20261030-snapshot-runtime.xml
BOUNDED_IDENTITY_EVIDENCE_DIR=/tmp/bounded-20261030-snapshot-live BOUNDED_IDENTITY_GENERATED_SOURCE_DIR=/tmp/bounded-20261030-snapshot-live/generated dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityLiveTests' -result-xml /tmp/bounded-20261030-snapshot-live.xml
```

Snapshot build: zero warnings/errors, log
`/tmp/bounded-20261030-snapshot-live-build.log`. Snapshot preflight: **9 passed,
zero failed/skipped/errors/not-run**. Its four actual PE/PDB pairs and 1,523
source documents belong to a separate graph; compact, key-sorted proof JSON
SHA-256 is `1b23f3d60166ef21a36c85c74dc56e0fdac6047368c6a16fc913803811216f97`.

**Live lane pending at implementation handoff:** the complete nine-case snapshot
process run is still active. Its output is
`/tmp/bounded-20261030-snapshot-live.log`; worker records and generated-source
captures are under `/tmp/bounded-20261030-snapshot-live`. This pending run is not a
completed live acceptance claim. Retained 2026-10-29 live results remain tied to
their earlier graphs. The broad gate is still red, so status remains
`in-progress`; no overall regression, package/release or Folders qualification
is claimed.
