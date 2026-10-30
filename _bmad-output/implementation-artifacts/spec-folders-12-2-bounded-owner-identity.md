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

> **Qualification update — 2026-10-30:** The owner has now retired runtime
> PE/PDB/source-binding checks and the optional OS observer, as recorded in the
> scoped review. Keep process replacement, recovery and bounded-storage assertions.
> Current qualification uses repository tests and CI with the commit SHA. Earlier
> runtime-proof/observer commands and results below are historical; do not restore
> those helpers.

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

- 2026-10-30: Resumption review added four public dispatcher controls for covered pending deliveries against paged identity proof. Matching cases complete handoff and drain without refolding; contradictory bytes preserve the pre-commit checkpoint and selected view/generation. No runtime code, frozen intent, original baseline or deferred owner decision changed.

- 2026-10-30: Completed the five patch-delta follow-ups, then corrected legacy inline journal-proof arithmetic exposed by removal of the old sequence ceiling. Added public-API overflow regressions and explicit host-owned rollout quiescence guidance. Frozen behavior, original baseline and owner-deferred GC remain unchanged.

- 2026-10-30: The owner assigned the concurrent scoped review's direct fixes to this run. Corrected captured-prefix admission, inline sequence arithmetic, buffered session fencing and cleanup disposal; added the requested persisted-state controls and clarified the dispatcher deployment gate. The later recorded owner decision defers evidence garbage collection and converts runtime source-binding/OS-observer removal into a direct patch while retaining process-replacement coverage.

- 2026-10-29: Reconciled the resumed spec with the later owner-approved
  evidence-storage simplification. The seventh task's tool and packet retention
  describe the original delivery, not work to restore. Keep the implemented
  bounded storage, public transport, tests and rollout documentation; record new
  verification here under the current repository policy.

## Review Triage Log

### Current resumption review — 2026-10-30

All three layers completed before classification. The full original-baseline
input is `/tmp/bmad-12-2-review-lw4ec_qi.diff` (5,792,681,349 bytes); detailed
tracing used its 27-file bounded-identity source/test/guide navigation slice and
selected routing callers. Review was not exhaustive across the historical
evidence deletions or unrelated intervening changes. Each finding receives its
own verdict below before grouping. Previously logged locations and claims keep
their verdicts without duplicate follow-ups.

| Finding | Verdict / route | Evidence and decision |
| --- | --- | --- |
| B1: selected-capture replay does not reread the epoch | medium / carried defer | Carried from Resumption E1 and Current patch completion B1: the same `JournalAsync` return path remains. Preserve the existing stale-lease acknowledgement follow-up. |
| B2: retirement rejects admitted streamed chunk counts | medium / carried defer | Carried from Resumption Blind R2 and Current patch completion B2: the same `ReconcileRetiredPartitionOwnersAsync` admission mismatch remains. No additional deferred entry. |
| B3: null multipart events escape validation | medium / carried defer | Carried from Current patch completion B3: `RequireLegacyEvents` still skips null elements before the same intake dereference. Preserve its separate input-validation follow-up. |
| B4: multipart ingress exceeds complete-owner event admission | medium / carried defer | Carried from Current patch completion B4: the same per-part allowance can persist excessive scratch before completion rejects it. No duplicate follow-up. |
| B5: cross-host redirects bypass catalog invocation fencing | medium / carried defer | Carried from Fresh resumption B10 and Corrected-graph R1: the same `IsSameHost` condition remains. Preserve the client-side finding without a receiver authorization claim. |
| B6: same-host redirects retain Authorization | medium / carried defer | Carried from Corrected-graph R2 and Resumption B2: the same redirect-authentication branch remains. Keep the existing native-transport parity follow-up. |
| B7: a POST body returns after POST-to-GET followed by 307 | medium / carried defer | Carried from Resumption B3: `forceGet` still leaves the replay buffer available to the subsequent redirect. No duplicate entry. |
| B8: ordinary HTTP bodies are buffered before the first send | medium / carried defer | Carried from Corrected-graph R3 and Current-source B10: the same unconditional body capture remains. Preserve the existing streaming/memory follow-up. |
| B9: unavailable isolated redirect transport falls back beneath the guard | medium / defer | Parent confirmed both copied source files match current bytes and reran `/tmp/blind-12-2-http-probe/bin/Debug/net10.0/Probe.dll`. A derived `HttpClientHandler` makes isolated-copy creation return null; the original native transport follows a same-host redirect to an unlisted invocation, returning 200 with zero catalog acquisitions. The routing files are unchanged since this invocation's initial revision and independent of bounded identity. This proves a client-side fencing gap, not a receiver authorization bypass. |
| B10: dispatch-lease release has no retry deadline | medium / carried defer | Carried from Corrected-graph R5 and Resumption B6: the same CAS loop with `CancellationToken.None` remains. Preserve the existing contention follow-up. |
| E1: selected replay can acknowledge a stale lease during paged proof reads | medium / carried defer | Carried from Resumption E1 and B1 above at the same `JournalAsync` path. The reviewer reproduced `AlreadyCaptured` with current epoch 2 and lease epoch 1; the existing root and follow-up remain unchanged. |
| VG1: paged pending-journal proof lacks a dispatcher commit regression | medium / patch fixed | Accepted as pre-verified. Added four public dispatcher controls covering both owner contracts: matching proof completes handoff/drain without a second fold, and contradictory bytes preserve the pre-commit checkpoint and selected view/generation. Implementer and parent each ran all four cases successfully. |

VG1 and B9 have separate roots: one direct regression-test correction and one
pre-existing routing follow-up. The ten carried findings introduce no additional
patches or ledger entries. Frozen intent, original baseline and the unresolved
broad regression results remain unchanged.

### Current patch completion review — 2026-10-30

All three layers returned before classification. The full original-baseline input is
`/tmp/story-12-2-review-ti_ul84i.diff`; detailed tracing prioritized this run's
five-file delta at `/tmp/story-12-2-current-patch.diff` and bounded projection
callees. This is not an exhaustive review of all intervening features. Edge-case
and verification-gap layers returned no findings. Each blind finding is classified
below before grouping; previously logged findings retain their earlier verdicts.

| Finding | Verdict / route | Evidence and decision |
| --- | --- | --- |
| B1: current-capture acknowledgement lacks an epoch reread | medium / carried defer | Carried from Resumption E1 at the same `JournalAsync` return path. Keep the existing stale-lease acknowledgement follow-up without another entry. |
| B2: retirement rejects otherwise admitted streamed chunk counts | medium / carried defer | Carried from Resumption Blind R2 at `ReconcileRetiredPartitionOwnersAsync`; the same event-count versus streamed-chunk-count mismatch remains. No duplicate follow-up. |
| B3: null multipart events escape malformed-input validation | medium / defer | `RequireLegacyEvents` skips null elements; `ValidateRequest` then dereferences `SequenceNumber` before the dispatch exception boundary. The original baseline already has the same predicate and call ordering. Record this independent pre-existing input-validation defect. |
| B4: multipart intake persists more events than the complete-owner admission | medium / defer | Each of up to the admitted part count can persist 256 events, with aggregate count checked at completion. This proves scratch-write amplification before refusal, not acceptance of an oversized completed owner. The original multipart design has the same missing cumulative ingress counter. |
| B5: a large handler chunk can exceed canonical batch admission | low / rejected | The raw candidate ceiling does not override the separate frozen one-MiB canonical-manifest limit, which rejects oversized writes. Adaptive fragmentation changes storage/recovery behavior for unusually large chunks; no bounded-identity regression or lost persisted state was demonstrated. |
| B6: legacy journal proof still permits sequence arithmetic overflow | medium / patch fixed | Removing the former 10,000-sequence guard exposed unchecked `lastSequenceNumber * 32` and digest-offset multiplication. A declared `(1L << 59) + 1` count wraps to a one-digest length. Use overflow-safe vector/count validation and prove refusal through the public journal-proof method. |
| B7: namespace/count comparison accepts a contradictory pending reservation | false / rejected | The accepted manifest and proof use the separately sealed local reference, not the reread reservation's redundant fields. Reservation creation compares the complete canonical reference and identity installation validates its binding. No supported concurrent transition or incorrect proof/owner acceptance from the alleged extra-field mismatch was demonstrated. |
| B8: concurrent ordinal test needs a larger owner | false / rejected | The rejection branch runs after complete identity storage regardless of leaf count; the new one-event recovery interleaving already detects duplicate persisted acceptance under guard removal. Existing large-owner matrix cases cover tree construction, with no uncovered failing outcome identified. |
| B9: add another abort/reservation interleaving | false / rejected | The reread rejects any non-Accumulating phase before acceptance; the next iteration also checks phase before writes. Existing pending/accepted-owner abort and delayed-installer controls cover retirement and selected-state preservation. No missing frozen-matrix outcome or broken abort path was demonstrated. |
| B10: rollout pause instruction lacks operational detail | low / patch fixed | Clarify that host-owned submission and retry/resume workers must stop and in-flight calls return, with workers kept stopped until all listed process versions are compatible. This is a direct guide correction and introduces no SDK pause switch or deployment API. |

The two direct corrections are independent. B3 and B4 are separate pre-existing
roots and are deferred separately; B1 and B2 carry their existing entries.

### Review Findings — patch-delta review 2026-10-30 (`dd02dd054`)

Scope: only the delta that applied the feature-scoped review's ten items, `a5b55e8c3..dd02dd054` restricted to this spec's 13 owned files (+289/−622). That slice is byte-identical to the diff the layers read before the commit landed. The EXT-PROTECTION files in the same commit, `deferred-work.md` and this spec are excluded. All four layers (blind, edge-case, verification-gap, acceptance) returned, and every finding received a verdict before grouping. Result: 25 findings, 0 decision-needed, 5 patch (9 findings), 2 defer (both carried), 14 rejected.

- [x] [Review][Patch] Test the post-reservation session fence's rejection branch (medium) [`src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildDispatcher.cs:1089`]
  - **Why it matters:** the reread hands the final session CAS (`:1131`) a fresh ETag. These six conditions are now the only barrier against accepting the same ordinal twice; before the patch the stale ETag made that impossible. Only the success path is tested.
  - **Fix:** add a test that completes the same ordinal concurrently while identity append/seal runs, via `InMemoryReadModelStore.BatchFaultHook` or `ConcurrentWriteBeforeTrySave`. Assert `AlreadyCompleted`, `AcceptedAggregateCount == 1` and a single-append `InventoryFingerprint`.
  - Sources: verification-gap (pre-verified), blind.
- [x] [Review][Patch] Rollout guide: name the gate operators can actually enforce (medium) [`docs/guides/named-projection-authoring.md:285`]
  - **"For new owners" reads as new aggregates.** In fact the dispatcher writes paged evidence for every owner a rebuild captures; `legacyManifest` is true only when resuming an old inline manifest.
  - **The stated condition can't be met.** Readers ship in the same binaries as the dispatcher, so "support paged checkpoints before that dispatcher is deployed" is impossible in a rolling upgrade. The enforceable gate is: start no shared rebuild until every reader, writer and maintenance process runs a paged-capable version.
  - Sources: blind, acceptance.
- [x] [Review][Patch] Delete the unreachable `capturedOwner is null || …` guard (low) [`src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs:823`]
  - The scoped-review regression item dropped only `int offset = 0`.
  - A non-null `identities` implies a non-null owner, and `ValidateCheckpoint` bounds the proof count by `OwnerSequenceNumber` (`SharedProjectionCapturedIdentityStore.cs:322-326`). The whole condition is dead, and nothing after it reads `capturedOwner`.
  - Sources: acceptance, blind, edge-case.
- [x] [Review][Patch] Compare the reread reservation by identity, not a hand-built unsealed copy (low) [`src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildDispatcher.cs:1094`]
  - `capturedReference with { RootDigest = "", Height = 0, LastPosition = 0 }` duplicates the logic of `Describe` and `SealAsync` (`SharedProjectionCapturedIdentityStore.cs:70,145`).
  - If the public reference record gains a seal-time field, every buffered owner will `continue` until `RetryExhausted`, with no diagnostic.
  - **Fix:** compare `NamespaceHash` (which binds scope, source, operation and epoch) and `Count`.
  - Source: blind.
- [x] [Review][Patch] Write the aliasing sequence as `(1L << 59) + 1` (low) [`tests/Hexalith.EventStore.Client.Tests/Projections/SharedProjectionBoundedIdentityTests.cs:552`]
  - `576460752303423489L` is the value whose old offset wrapped to 0 and aliased digest 1, but nothing says so.
  - The reviewer's further claim that the 2^58 boundary is missing is false: the new admission is a single comparison, already covered by `2L` and `long.MaxValue`.
  - Source: blind.
- [x] [Review][Defer] Captured-owner acknowledgement does not reread the epoch fence after paged reads [`src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs:838`] — deferred: carried from Resumption E1. It has been pre-existing since the baseline, and the existing ledger entry "Recheck the epoch fence before acknowledging a current-capture historical duplicate" already covers it. No duplicate entry. Source: blind.
- [x] [Review][Defer] The guide gives operators no safe procedure for reclaiming superseded identity evidence [`docs/guides/named-projection-authoring.md:281`] — deferred: carried with the owner-deferred evidence GC. The existing ledger entry was amended. Source: blind.

**Rejected:**

- **Blind: a reservation mismatch should fail loudly** — false. On the next iteration, `ReserveCapturedIdentityAsync` throws on the conflicting prior reservation (`DomainSharedProjectionRebuildDispatcher.cs:2881`). The call therefore fails after one retry, not at retry exhaustion.
- **Blind: the preparation-count test uses upcaster calls and pins exactly 2** — two parts:
  - The proxy claim is false. `UpcastRequest` and `PreparePartitionChunksAsync` each run once per loop pass (`:1001-1008`), and the exact count of 2 is the regression pin that was requested.
  - The pre-existing save-then-continue at the scratch reservation (`:1023-1030`) is low. It costs one extra preparation per owner during rare rebuilds, and fixing it would mean replicating the reread fence.
- **Blind: the streamed-evolution test does not pin which guard fired** — false. Exactly 2×66 evolution calls with zero folded streamed events can only come from `:592`. The buffered-fold and event-count throws produce different counts.
- **Blind: the streamed-evolution test stops before recovery and reads an internal key** — false on both counts.
  - Recovery: identical pages return early (`SharedProjectionCapturedIdentityStore.cs:353-357`) and an identical seal returns the stored reference (`:150-154`), so a stable retry recovers.
  - Internal key: `InventoryKey` is internal and DomainService.Tests has no `InternalsVisibleTo`. The literal key therefore follows the file's existing idiom.
- **Blind: the pending-contradiction test lacks a positive control** — false. The setup meets every other precondition: Building phase, operation `pending`, count 67, and a non-chunked delivery, which is `PayloadReady` (`SharedProjectionEpochCoordinator.cs:896`). Removing the digest check makes the method return true, which fails the test.
- **Blind: the selected-retirement test does not assert the message** — false. Without the `ActiveGeneration == reference.Epoch` clause, this setup reaches `TrySaveAsync` and erasure, which fails the snapshot assertions.
- **Blind: the older-receipt test lacks preconditions and a contradictory case** — false.
  - If either variant lost its proof, the guard would not fire, the receipt path would return `AlreadyCaptured`, and the test would fail. Both variants were red before the fix.
  - A changed payload reaches the same guard, which does not depend on the payload.
- **Blind: the worker evidence carries no build identity** — false. Every phase runs `dotnet` on the same test assembly (`ProjectionBoundedIdentityLiveTests.cs:48-49`), and the owner made CI plus the commit SHA the binding.
- **Blind: the evidence-simplification spec still lists the observer as kept** — false. That spec's frozen Never clause only names what it did not touch. It does not require the tool to remain.
- **Edge-case: the streamed test changes evolution, not part bytes** — false. `:592` compares the evolved digests whatever caused the change, and removing that guard fails the test.
- **Acceptance: no SHA or CI binding is recorded** — rejected, because the fix would edit this spec. Integration Tests and CI for `dd02dd054` were still running at review time.
- **Acceptance: the Verification section still instructs PDB checks** — rejected, because the fix would edit this spec.
- **Acceptance: an exact replay beyond retained proof gets `RebuildRequired`** — low.
  - This matches both the baseline and the approved patch. The event lies beyond retained proof, so the matrix's fail-closed row governs.
  - It arises only when an older nonpartitioned rebuild follows a capture, and fixing it needs ordinary-tail retention, which is already deferred.
- **Acceptance: `dd02dd054` mixes in EXT-PROTECTION work** — low. The commit is already pushed. Its Payload Protection failure comes from EXT-PROTECTION fixtures, starting with the missing commitlint CLI already deferred as VG2.

### Resumption review at a5b55e8c34892c30fb4feccefd5498dfe0363585 — 2026-10-30

All three required layers returned before classification. The cumulative input is
`/tmp/bounded-owner-a5b55e8-final-review-tcurw_it.diff` (9,016,876 bytes), with
historical evidence deletion bodies omitted and this claims file supplied only
to the edge-case layer. Blind review traced catalog HTTP fencing and installation
verification; edge review traced bounded identity storage, coordinator replay and
handoff, dispatcher transport/cleanup, and source capture; verification review
traced the separate EXT-PROTECTION installation/recovery tests and CI. None claims
exhaustive review of the 1,688 intervening file diffs.

Every finding is classified below before grouping. Four findings carry earlier
verdicts at the same locations without another verification or deferred entry.
The verification review's final compound finding is split into its two distinct
fixture failures. The concurrently added feature-scoped review below is a
separate input and remains preserved; this table does not close its patch or
decision items.

| Finding | Verdict / route | Evidence and decision |
| --- | --- | --- |
| B1: cross-host ordinary redirect bypasses catalog invocation fencing | medium / carried defer | Carried from Fresh resumption B10 and Corrected-graph R1: the same `IsSameHost` condition remains. Keep the existing client-side finding without claiming receiver authorization bypass or adding a duplicate follow-up. |
| B2: same-host redirect, including another port, retains Authorization | medium / carried defer | Carried from Corrected-graph R2: the same redirect-authentication branch remains. Preserve the existing native-transport parity finding and follow-up. |
| B3: a POST body reappears after POST-to-GET followed by 307 | medium / defer | `forceGet` clears `request.Content` but leaves `replayable` intact; the following 307 reaches the non-force branch and recreates the original body on GET. No upstream guard clears it. This unchanged catalog transport belongs to separate routing work. |
| B4: every ordinary HTTP body is buffered before first send | medium / carried defer | Carried from Corrected-graph R3 and Current-source B10: the same unconditional `CaptureReplayableContentAsync` call remains. Preserve the existing streaming/memory follow-up. |
| B5: catalog lease ends at response headers before remote work completes | maybe-false / defer (medium unverified) | The handler disposes its lease after the transport returns `HttpResponseMessage`, but no supported invoked method was demonstrated to continue catalog-sensitive work after headers. Settle with an actual Dapr streaming receiver and concurrent activation/quiescence probe; do not claim a demonstrated fence bypass. |
| B6: catalog lease release retries without a deadline | medium / carried defer | Carried from Corrected-graph R5: `ReleaseAsync` still uses its unbounded CAS loop and `CancellationToken.None`. Preserve the existing contention finding and follow-up. |
| B7: role stdout/stderr are buffered before output admission | medium / defer | `Role.request` uses `capture_output=True`; its 16-MiB stdout check runs after process exit and stderr has no admission. A verbose/broken role can exhaust verifier memory before refusal. This separate qualification tool is unchanged by bounded identity. |
| B8: OpenSSL helper inherits all role credentials and loader environment | medium / defer | `validate_profile` requires all three role values in the parent environment; `verify_statement` launches `openssl` without an `env` argument, unlike the isolated role launch. This verifies the broader credential exposure to that helper, without asserting an exploited executable/loader substitution. Independent qualification tooling owns the correction. |
| B9: installation can change between pathname validation and launch | medium / defer | Accepted installations need not be immutable: validation hashes pathname contents, then `Role.request` opens the executable/dependencies again during launch without an ownership lock or held file binding. A concurrent replacement can therefore invalidate the checked identity. No credential-theft exploit is claimed; this is separate installation consistency work. |
| B10: unresolved selected-RID native asset can disappear from closure | maybe-false / defer (medium unverified) | The resolver explicitly skips unresolved `runtimeTargets`, but the review did not demonstrate a required current-RID asset being omitted while a real role still loads it unpinned. Settle with a shipped native dependency, actual RID selection and omission/load controls. Keep the claim unverified rather than infer resolver semantics. |
| E1: current-capture duplicate acknowledgement does not reread the epoch fence | medium / defer | A changed epoch after the cached state/proof read can still reach `AlreadyCaptured`. Original baseline `ae917d3e4dbab20b8982c5222a13611c3e3faf35` already awaited `ReadOwnerCheckpointAsync` and returned without rereading that fence; bounded paging adds awaits but did not create the race. Record the pre-existing stale-lease acknowledgement defect without claiming demonstrated data loss. |
| E2: restore retired qualification runner/evidence or correct completion claim | false / rejected | The opening resumption note and Spec Change Log explicitly supersede that historical task with the approved evidence-storage simplification and prohibit restoring the retired machinery. The claimed missing current deliverable is disproved by those instructions; no spec rewrite or restoration is warranted. |
| VG1: closure completeness tests derive their inventory from the resolver under test | medium / defer | Accepted as pre-verified: removing `Hexalith.Commons.UniqueIds.dll` from the resolver result still passed all seven closure tests because their expected manifest and mutation loop used the same reduced inventory. This verification gap is in the separate, concurrently edited installation tool, not captured identity. |
| VG2: offline CI discovers a test that requires an uninstalled commitlint CLI | medium / defer | The paired-checkout test reads root `node_modules/@commitlint/cli/package.json`; the blocking payload-protection lane performs no npm installation and supplies no alternate CLI. A clean runner fails before that test's assertions. This is separate tooling CI work; no Git-message candidate was authored or used by this resumption. |
| VG3: installation fixture assumes a sibling Platform checkout | medium / defer | `ShippedRoleClosureTests` unconditionally builds from `ROOT.parent / 'platform'`; the workflow initializes root-declared submodules under `references/` and creates no sibling. Its clean-runner fixture path is unsupported. This is separate installation-test setup work. |
| VG4: recovery fallback lacks its native vector producer | medium / defer | The recovery fixture falls back to `references/Hexalith.Platform`, whose current test sources contain no `ProtectionQualificationLoop7RecoveryTests`. That fixture subsequently requires five generated vector files. The root-declared reference cannot provide this new producer merely by being initialized; independent recovery tooling must supply a qualified input. |

The eleven new surviving roots are independent and are deferred separately;
two remain explicitly unverified. No patch or implementation loopback follows
from these three layers. This does not resolve the concurrently added scoped
review, and the broad regression gate remains red.

### Review Findings — feature-scoped review 2026-10-30

Scope: unlike the earlier cumulative passes, this review read only the 30 files this
spec owns (`ae917d3e4..a5b55e8c3`, +3,813/−197). Separate abort-repair and CI wiring
are excluded. All four layers (blind, edge-case, verification-gap, acceptance)
returned. Every finding was verified against the code and given a verdict before
grouping. Result: 2 decision-needed (both resolved by the owner: one deferred, one
converted to a patch), 10 patch, 3 defer (2 new, 1 carried), 18 rejected.

- [x] [Review][Defer] Identity evidence is never reclaimed after a successful or superseded rebuild — medium. Owner decision 2026-10-30: defer to a follow-up. Reason: rebuilds are rare, and the guide already places this evidence under the operator's retention policy.
  Each rebuild writes, under a new namespace, all of:
  - one message-index key per event,
  - two records per 64-event leaf, plus index nodes,
  - one terminal batch marker per identity batch.

  `RetireAsync` runs only on abort, and even then reconciles batch markers rather than reclaiming them.
  Nothing reads message-index keys after `SealAsync`. Evidence of generations that are later superseded
  has no reclamation path at all. The earlier inline vector was overwritten each generation. This
  storage now grows with every rebuild: roughly 12 MB of leaf/node records plus the message keys per
  32,769-event owner per rebuild. Selected evidence must be kept for inheritance, so a correct fix needs
  a reachability/GC design, not a direct correction. Sources: blind, acceptance, edge-case.
  [`src/Hexalith.EventStore.Client/Projections/SharedProjectionCapturedIdentityStore.cs:272`]
- [x] [Review][Patch] Remove the runtime source-binding proof and the OS observer: delete `ProjectionBoundedIdentityRuntimeProof`(`Tests`), `tools/bounded_worker_observer.py`, its tests and its CI step. Keep the live process-replacement tests. Owner decision 2026-10-30: qualification is CI plus the commit SHA, per the evidence simplification. Original finding (medium):
  - **Drift failures:** `ProjectionBoundedIdentityRuntimeProof.Capture` fails every live phase when any
    src/tests file changes after the build. This aborted two recorded 2026-10-29 live runs and forced
    the snapshot workflow.
  - **Observer never runs:** `bounded_worker_observer.py` executes only when
    `BOUNDED_IDENTITY_MAP_OBSERVER`/`BOUNDED_IDENTITY_MAP_DIR` are set. No automated lane sets them,
    yet CI runs its 11-test suite.
  - **Why it's a decision:** the frozen Always clause binds qualification to exact sources and binaries,
    while the later evidence simplification makes tests + CI the proof. Source: blind.
  [`tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionBoundedIdentityRuntimeProof.cs:1`]
- [x] [Review][Patch] Delivery past the captured identity count falls into the unseeded legacy-receipt branch instead of `RebuildRequired` [`src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs:794`]
  - **Regression:** baseline returned `RebuildRequired` when `OwnerSequenceNumber` exceeded the seeded
    identities. Now `ReadDigestAsync` returns null for both "no identities" and "beyond the count".
  - **Impact:** a contradictory delivery (position ≤ capture watermark, sequence > count) is classified
    against the previous epoch's receipt. It can even be acknowledged `AlreadyCaptured`.
  - **Fix:** restore the explicit guard when the checkpoint carries a reference or inline digests, and
    drop the leftover `capturedOwner is null` / `int offset = 0` at :817–821.
- [x] [Review][Patch] Inline `ReadDigestAsync` offset overflows for sequence ≥ 2^58; compare against `inline.LongLength / 32` before multiplying [`src/Hexalith.EventStore.Client/Projections/SharedProjectionCapturedIdentityStore.cs:264`]
- [x] [Review][Patch] Buffered owner reservation invalidates the session ETag [`src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildDispatcher.cs:1076`]
  - **Cause:** `StoreCapturedEventsAsync` saves the session (`PendingCapturedIdentityReference`). The
    final `TrySaveAsync` at :1113 then always fails once with the stale ETag.
  - **Cost:** every new buffered owner reruns handler chunk preparation and the full identity
    revalidation, including per-event message reads.
  - **Fix:** reread and fence-check the session after reservation.
- [x] [Review][Patch] Dispose the `JsonDocument` read in `EraseAsync` [`src/Hexalith.EventStore.Client/Projections/SharedProjectionCapturedIdentityStore.cs:424`]
- [x] [Review][Patch] Rollout guide says "before allowing paged checkpoints to be written", but no such switch exists [`docs/guides/named-projection-authoring.md:285`]
  The effective gate is deploying the new rebuild dispatcher, which writes paged evidence for every new owner. State that directly.
- [x] [Review][Patch] Test: `RetireAsync` refuses the currently selected reference with no later epoch (`ActiveGeneration == reference.Epoch` clause untested) [`src/Hexalith.EventStore.Client/Projections/SharedProjectionCapturedIdentityStore.cs:290`]
- [x] [Review][Patch] Test: `CanPromoteCapturedJournalOwnerIdentityAsync` returns false for a pending delivery that contradicts the sealed identity [`src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.CapturedIdentity.cs:53`]
- [x] [Review][Patch] Test: `MatchesCheckpointPositionAsync` as the sole rejecter, i.e. an accepted N+1 event at a different position in the next capture [`src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.CapturedIdentity.cs:154`]
- [x] [Review][Patch] Test: streamed fold second pass rejects changed part bytes ("Owner evolution changed across streamed fold passes") [`src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildDispatcher.cs:592`]
- [x] [Review][Defer] Server mapped-only ordinary delivery still caps an owner at 10,000 events [`src/Hexalith.EventStore.Server/Projections/ProjectionSourceGuardDelivery.cs:290`] — deferred: pre-existing.
  The same cap is in `ProjectionDeliveryFingerprint.cs:218`, `ProjectionSourceGuardHistoryReader.cs:89` and
  `NamedProjectionDispatchCoordinator.cs:639/648/1133`. It applies to shared-epoch routes only under mapped-only
  streamed delivery. It is outside this spec's Code Map. Medium.
- [x] [Review][Defer] Accepted ordinary-tail identities are not retained across generations [`src/Hexalith.EventStore.Client/Projections/SharedProjectionCapturedIdentityStore.cs:218`] — deferred: carried E1 / Resumption Blind R1 (high).
  New consequence: an exact at-least-once replay of an accepted ordinary event after a later generation records a
  durable `IdentityConflict`. `RepeatedOmissionPreservesPagedAndInlineOwnerProofAfterOrdinaryGrowth` asserts this
  for both paged and inline proof, so it is pre-existing. The existing ledger entry is updated; no duplicate is added.

**Rejected:**

- **Blind: diff not self-contained** — false. This is an artifact of the review diff: shared files carry abort-repair hunks from `c613e55ae`, and CI wiring was excluded on purpose.
- **Blind / acceptance / edge-case: streamed duplicate message returns `Indeterminate`, not `Failed`** — low.
  - The producer's `RequireSuccess` throws on both statuses, and nothing is acknowledged or promoted.
  - Legacy resume compares digests that cover `MessageId`.
  - A typed fix needs a new exception contract, and the trigger is corrupted source history.
- **Blind / edge-case: server capture no longer rejects owners above 10,000** — low. The spec task removes these ceilings, the server cannot see the domain's configured admission, and the domain rejects the owner loudly.
- **Blind / verification-gap: inline 10,000 cap removed** — false. `ReadModelBatchValidation.cs:45` enforces the 1-MiB canonical manifest, as owner-decided on 2026-10-21.
- **Blind: content digest relies on reflection JSON** — low. The risk is a future schema change, and the fix is a redesign.
- **Blind: read amplification** — low. Carries Original Blind 5: there is no latency SLA and the live lane completes.
- **Blind / edge-case: identity batches rewrite the epoch state** — low. It is the existing `TryWriteScratchAsync` fence. Owner-sequenced deliveries back off while Building, and CAS exhaustion is resumable.
- **Blind: late `IReadModelConditionalEraser` check** — low. `DaprReadModelStore` implements it; a custom store fails loudly.
- **Blind / acceptance: public store API surface** — low.
  - The store never seeds, so no reachable path bypasses the promoted-cleanup gate.
  - Removing shipped public API would be a breaking change.
- **Blind: slot `Level` unchecked** — false. A corrupt slot throws before any write, so it still fails closed.
- **Blind: journal promotion skips `LastPosition`/`ValidateComplete`** — false. The dispatcher runs both promotion checks (`:2826-2837`).
- **Edge-case: retire throws after the epoch advances** — false. Abort uses `deferOpen`, retirement precedes `CompleteAbortAsync`, and `BeginAsync` requires `Open`. Carries Original Blind 4.
- **Edge-case: `OwnerSequenceNumber` 0 drops reference validity** — low. `DomainProjectionDispatcher.cs:910` rejects sequence ≤ 0, so only an external unsequenced caller on a sequenced owner could trigger it.
- **Edge-case: observer needs Python ≥ 3.11** — low. CI and local runs use 3.12+.
- **Verification-gap: C# observer request contract not exercised** — low. Tooling only. Consistent with the Corrected-graph VG1 rejection, and subsumed by the runtime-proof decision.
- **Acceptance: default `MaxRebuildEventCount` is 10,000** — false. The spec mandates configured admission, and the guide sets 40,000 for Folders.
- **Acceptance: paged seed has no direct checkpoint-CAS race test** — low. The shared seed core is covered by the legacy extension-race test.
- **Acceptance: spec lacks CI evidence** — rejected, because the fix would edit the spec under review.

### Current-source completion review — 2026-10-30

All three layers returned before classification. Their input was the preserved
historical-baseline diff at `/tmp/bounded-owner-current-review-input-z5hqokbm.diff`,
including intervening features and a deletion inventory without retired evidence
bodies. Detailed tracing covered bounded storage, handoff, inheritance, public
dispatch, source capture, process replacement and the observer; review was not
exhaustive across the other 1,241 diff blocks. Every finding is classified below
before grouping. Earlier matching rows retain their verdict without another
verification or deferred entry.

| Finding | Verdict / route | Evidence and decision |
| --- | --- | --- |
| B1: published qualification loses a failing producer exit through `tee` | medium / defer | The separate `published-compatibility` step supplies no explicit shell or `pipefail`; parent confirmed `bash -e -c 'false \| tee /dev/null'` exits 0. This job was added by intervening published-compatibility work and does not run the bounded live class. |
| B2: abort repair permits a target/source-guard overlap | medium / defer | `IsValidAbortRepairRequest` checks uniqueness inside each collection but not their intersection. A present target can satisfy its guard, be deleted, then fail the subsequent `VerifyRepairSourceAsync`, leaving the reserved repair unable to finish. This separate repair API is unchanged in this invocation and is not called by bounded identity. |
| B3: absent certificate after a failed save does not stop repair | maybe-false / defer (medium unverified) | The null reread branch can reach deletion, but `IReadModelStore.TrySaveAsync` defines false as ETag conflict, and no certificate-erasure caller was established. Demonstrate a conforming store returning conflict followed by absence, or a supported concurrent certificate removal, before claiming loss of retained evidence. This is separate repair work. |
| B4: abort repair clones input before bounded admission | medium / defer | `RepairAbortMetadataAsync` serializes and deserializes the caller's complete request before its collection and 256-KiB checks. Oversized input therefore incurs duplicate allocation before rejection. This unchanged repair API is independent of bounded identity. |
| B5: completed repair retry fails after closeout | medium / carried defer | Carried from Fresh resumption B2: `RequireOperation(Aborting)` still precedes the completed-receipt check at the same location. Preserve its existing verdict and follow-up without duplication. |
| B6: content ledger accepts malformed persisted task/fence entries | medium / defer | `ReadLedgerAsync` checks collection existence/count only; an invalid task key or nonpositive quiesced fence survives. The upload guard then compares a positive supplied fence against that negative value. This is a persisted-state validation gap in separate content work, not a claim that domain authorization is bypassed. |
| B7: content metadata ledger has no byte admission | medium / carried defer | Carried from Corrected-graph R9: the same entry-count/metadata-size mismatch remains in `Canonicalize` and `MutateAsync`. No additional ledger entry or corruption claim. |
| B8: valid multiple fixture endpoints collide | medium / carried defer | Carried from Corrected-graph R7: the same `ResourceId`-only client key collides for distinct declared endpoints. Preserve the existing fixture follow-up. |
| B9: cross-host redirect skips catalog invocation fencing | medium / carried defer | Carried from Fresh resumption B10 and Corrected-graph R1: the same `IsSameHost` condition remains. Preserve the client-side verdict without a receiver authorization claim or another ledger entry. |
| B10: ordinary redirect handling buffers every request body | medium / carried defer | Carried from Corrected-graph R3: `CaptureReplayableContentAsync` still reads the complete content before the first send. Preserve the existing independent routing follow-up. |
| E1: an accepted ordinary tail may change in a later rebuild | high / carried defer | Carried from Resumption review Blind R1 and Fresh resumption B4: the retained-prefix comparison and checkpoint-position check still omit ordinary-tail receipt identity. This pre-existing defect is not a bounded-retention regression. |
| VG1: existing observer regressions are not selected by CI | low / patch | Accepted as a pre-verified gap: the 11-test observer suite, including FIFO rejection, passes locally but neither the selected Python commands nor the observer-disabled live lane runs it in CI. Add the existing suite to the current Linux Python checks. This needs no new protocol and differs from the earlier rejected C# caller-test proposal. |

The surviving roots are independent. VG1 is the sole direct correction; five
separate issues are deferred (one explicitly unverified), and six findings carry
earlier decisions without duplicate follow-ups. No frozen intent, public surface,
storage schema or implementation loopback changes.

### Corrected-graph completion review — 2026-10-30

All three layers returned before classification. Edge-case review returned no
findings; verification review returned one coverage proposal. Blind review used
the cumulative historical-baseline diff at
`/tmp/bounded-owner-resume-20261030-review-h7ssf61w.diff` (historical deletion
bodies omitted), so its findings also include intervening work from other
features. Every finding is classified separately below, before grouping.

| Finding | Verdict / route | Evidence and decision |
| --- | --- | --- |
| R1: cross-host redirect skips the catalog fence | medium / carried defer | Carried from Fresh resumption B10: same `SendOrdinaryAsync` location and `IsSameHost` condition, unchanged. Retain the earlier client-side verdict and existing follow-up; no additional ledger entry or receiver authorization claim. |
| R2: same-host redirect retains Authorization | medium / defer | Parent reran `/tmp/catalog-review-harness-k848cut_/bin/Debug/net10.0/harness.dll`: native HttpClient cleared the bearer, while the actual catalog handler forwarded it. The ordinary redirect implementation is unchanged from invocation HEAD and belongs to the separate routing feature. |
| R3: ordinary HTTP bodies are buffered without a byte budget | medium / defer | `RouteCatalogStateHttpClientFilter` wraps factory clients; `SendOrdinaryAsync` calls `ReadAsByteArrayAsync` before the first send whenever ordinary automatic redirects are enabled. No caller-side size guard precedes it, so streaming uploads acquire whole-body memory costs. This unchanged routing implementation is independent of captured identity. |
| R4: human policy admits bearer plus channel header | medium / defer | The same source harness reports `mixed_human_and_channel_authorized=True`. The actual requirement rejects an assertion header but does not reject `dapr-api-token`; the named bearer scheme and host-inventory middleware add no such header check. This is a credential-kind consistency issue in the separate authentication work, not evidence that an invalid bearer gains authorization. |
| R5: completed lease release can retry forever | medium / defer | `RouteCatalogDispatchLease.DisposeAsync` awaits `ReleaseAsync`, whose CAS loop and backoff use `CancellationToken.None` without an attempt/deadline limit. Sustained competing updates can hold a completed request beyond its cancellation. Backend exceptions still propagate; no blanket backend-outage hang is claimed. The routing authority is unchanged and independent of this identity protocol. |
| R6: optional OS observer blocks opening a FIFO | low / patch | Parent created a FIFO and called the existing `regular_file` in an isolated subprocess; it blocked until the one-second watchdog killed/reaped it. `fstat` cannot reject the FIFO before blocking `open` returns. Add `O_NONBLOCK` to the existing held-descriptor open and verify prompt rejection with a retained failure receipt. This directly corrects the kept helper; no retired machinery is restored. |
| R7: multiple endpoints collide in fixture client storage | medium / defer | Parent reran `/tmp/descriptor-review-harness-elkm8044/bin/Debug/net10.0/harness.dll`: the real descriptor reader accepts two endpoint names, then the actual fixture throws duplicate-key `ArgumentException` for `eventstore`. Client storage keys only `ResourceId`. This separate topology-fixture migration is not used by the bounded Dapr lane. |
| R8: active content may execute through a browser consumer | maybe-false / defer (medium unverified) | The public content helper returns the receipt media type without attachment/CSP headers, but repository search finds no consuming route. Upload policy is delegated to the domain authority. Establish a real authenticated browser consumer that permits active content and lacks an outer execution restriction before claiming an exploit. This unchanged content feature is independent of bounded identity. |
| R9: content ledger has entry-count admission only | medium / defer | Canonicalization allows 1,024 changes with 16 metadata values of 2,048 characters. A dimension-valid illustrative pending ledger serializes to 33,823,805 bytes, beyond the documented 16-MiB fixture transport, while `MutateAsync` has no serialized-byte admission. This proves a metadata/transport admission mismatch, not that an oversized rejected write corrupts persisted state. The separate content implementation is unchanged. |
| R10: catalog lifecycle histories grow in one state cell | medium / defer | Prepare appends immutable attempt bindings and rollback appends completed transitions; neither collection is capped, segmented or pruned. Every acquisition/release reads or rewrites that same authority value. Repeated legitimate activations can therefore outgrow its transport/storage envelope. This is separate catalog lifecycle work. |
| VG1: C# observer nonzero-exit branch lacks an automated caller test | low / rejected | Accepted as a pre-verified test gap. The optional environment-gated branch currently rejects nonzero exits; the normal live lane does not enable it, and no frozen matrix outcome depends on it. Adding a subprocess/stub protocol for this uncommon diagnostic path exceeds a direct correction. Existing native helper tests and the complete source-bound live matrix remain in place. |

The survivors have distinct root causes. R6 is the sole direct correction;
eight independent routing/authentication/content/fixture entries are deferred,
and R1 carries its existing entry without duplication. No frozen intent or
public API change, code re-derivation or review loopback is required.

### Fresh resumption review — 2026-10-30

All three layers completed before classification. The edge-case and verification-gap
layers returned no findings. Blind review used the preserved historical-baseline
diff `/tmp/bounded-owner-20261030-review-wzgt3ttd.diff` (deletion bodies omitted),
with the bounded integration slice available for navigation. Each finding was
classified before grouping; none shares a root cause with another new correction.

| Finding | Verdict / route | Evidence and decision |
| --- | --- | --- |
| B1: overflow admits an out-of-reservation identity leaf | medium / patch | Parent reran `/tmp/bounded-review-api-repros/bin/Debug/net10.0/repro.dll`: a 64-entry page at `long.MaxValue - 62` is accepted into a 64-event reservation and remains after retirement. Compare the start against the remaining reserved range before addition; add a persisted-state regression. |
| B2: completed abort metadata repair cannot retry after closeout | medium / defer | The same executable proves that an identical repair retry throws after abort reopens the epoch: `RequireOperation(Aborting)` precedes the completed-receipt check. The unrelated repair API is unchanged from this invocation's original HEAD `52fae16c7463621c82cb7ea266823852640b4738`; bounded identity does not call it. |
| B3: lowered 64-operation profile rejects a 68-operation leaf | low / rejected | Parent reproduced the configured rejection. The approved lane uses the existing 100-operation profile; lower deployment limits are admission restrictions, and adaptive leaf batching would add a separate storage/recovery design for an uncommon custom profile. Preserve the fail-closed configured limit rather than expand this correction into that design. |
| B4: ordinary tail can change in a later rebuild | high / carried defer | Carried from 2026-10-29 Blind R1: same prefix-comparison location and claim, with the existing receipt-to-rebuild consistency defect unchanged. Retain its prior verdict and follow-up; do not append another deferred entry. |
| B5: retirement chunk bound is smaller than accumulation bound | medium / carried defer | Carried from 2026-10-29 Blind R2: the same unchanged `ReconcileRetiredPartitionOwnersAsync` mismatch remains. Retain its prior verdict and existing follow-up. |
| B6: buffered contribution hashes exceed one manifest | medium / carried defer | Carried from 2026-10-29 Blind R7: the same unchanged buffered output-metadata representation remains. Retain its prior verdict and existing follow-up. |
| B7: streamed contribution hash root and working set grow with output | medium / defer | Parent compared the original baseline's `chunkHashes` list and `pageDigests` manifest construction with current code: both representations and admission formula already existed. Sufficiently high configured output admission can exceed the unchanged manifest limit; output-metadata paging is separate from identity-vector replacement. |
| B8: worker wait ignores suite cancellation | low / patch | `RunProcessReplacementAsync` passes only a private 20-minute timeout to the child wait; the test cancellation token is first used after that wait. Link cancellation and await killed-child exit so canceling an expensive suite promptly stops its worker. |
| B9: CI discards native bounded-worker diagnostics | low / patch | The integration job uploads TRX/coverage only, while worker JSON/logs default to temporary directories. Route existing runtime captures under `TestResults` and include them in the existing artifact upload. This retains native test diagnostics without restoring retired qualification tooling or committed evidence. |
| B10: cross-host redirect can bypass the catalog invocation fence | medium / defer | `SendOrdinaryAsync` invokes `RequiresCatalogFence` only for `IsSameHost`, so a localhost-to-127.0.0.1 Dapr invocation hop goes directly to the transport. This routing implementation is unchanged from invocation HEAD and independent of bounded identity; record the client-side bypass without claiming a receiver authorization bypass. |

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
implementation, regression tests and reader rollout/rollback guide were already
present. Initial inspection found no missing implementation; review subsequently
identified the three direct corrections recorded below. The initial verification
in this section predates those corrections and stays tied to its own sources and
binaries. The original baseline and frozen intent are preserved, and unresolved
broad failures prevent completion. No pins, Git index/history, publication,
deployment or retired qualification machinery were changed by this task. CI
run/link: none; these are local SDK 10.0.403 / .NET 10.0.14 results.

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

**Interrupted initial live attempt:** the complete nine-case snapshot run was
active at implementation handoff but ended when that agent returned. Parent
attempted to resume session `30002` and received `Unknown process id 30002`;
the runner, its sidecar and worker had exited, with no final XML. Fourteen worker
records and `/tmp/bounded-20261030-snapshot-live.log` remain under the original
temporary paths. This incomplete attempt has no completed-lane acceptance count.
Parent restarted the full class in its own active session against the unchanged
snapshot, using separate `bounded-20261030-final-live` output paths. Retained
2026-10-29 results remain tied to their earlier graphs. The broad gate is still
red; no overall regression, package/release or Folders qualification is claimed.

### Fresh review corrections — 2026-10-30

All ten blind findings were classified before grouping; the other two layers
returned no findings. Three direct corrections were applied: reject overflowing
identity-page ranges before persistence, link worker waits to suite cancellation
and await killed-child exit, and retain existing native bounded-worker diagnostics
in the integration job's existing artifact upload. No public API or storage
schema changed. Three pre-existing issues were appended to `deferred-work.md`;
three previously deferred findings were carried without duplicate ledger entries,
and the uncommon lowered-operation-profile proposal was rejected as logged.

The overflow regression first failed against the original production check
(`should throw ... but did not`), then passed with the correction. Its assertions
verify no change to the epoch, reservation or store entry count and prove that
abort/retirement still completes. The first preparatory test build failed with
`CS7036` because the new test omitted the required `AbortAsync` fold argument;
the test call was corrected, the unchanged build command passed, and only then
was the expected red regression run. Both preparatory logs remain under
`/tmp/bounded-20261030-fixes-client-red-build{,-retry}.log`.

Commands from the repository root:

```bash
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true > /tmp/bounded-20261030-fixes-client-build.log 2>&1
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true > /tmp/bounded-20261030-fixes-live-build.log 2>&1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -method '*SharedProjectionBoundedIdentityTests.OverflowingLeafSequenceRangeCannotPersistOutsideItsRetirementInventory' -xml /tmp/bounded-20261030-fixes-overflow-red.xml > /tmp/bounded-20261030-fixes-overflow-red.log 2>&1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml /tmp/bounded-20261030-fixes-client.xml > /tmp/bounded-20261030-fixes-client.log 2>&1
BOUNDED_IDENTITY_EVIDENCE_DIR=/tmp/bounded-20261030-fixes-results/TestResults/live-sidecar/bounded-identity BOUNDED_IDENTITY_GENERATED_SOURCE_DIR=/tmp/bounded-20261030-fixes-results/TestResults/live-sidecar/bounded-identity/generated dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*ProjectionBoundedIdentityLiveTests.FocusedProcessReplacementExercisesPartialStageAndOwnerHandoff' -result-xml /tmp/bounded-20261030-fixes-live.xml > /tmp/bounded-20261030-fixes-live.log 2>&1
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE=/tmp/bounded-20261030-fixes-runtime.json dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml /tmp/bounded-20261030-fixes-runtime.xml > /tmp/bounded-20261030-fixes-runtime.log 2>&1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml /tmp/bounded-20261030-fixes-client-full.xml > /tmp/bounded-20261030-fixes-client-full.log 2>&1
actionlint .github/workflows/integration.yml
git diff --check
```

The two corrected-project builds passed with zero warnings/errors. Runtime proof
passed **9/9**, bounded Client tests **50/50**, and streamed/buffered 129-event
process canaries **2/2**, all without failures/skips/errors/not-run cases.
`actionlint` and the whitespace check passed. Full Client rerun: **2,249 passed,
one failed, two skipped**, zero errors/not-run, exit 1; the same existing
routing-header ownership guard remains the only failure. The Domain/Server broad
results above belong to their earlier builds and were not relabeled as reruns
against this corrected graph.

Parent independently checked the six canary worker records, all distinct PIDs,
all eight actual DLL/PDB hashes and all 1,523 source-document hashes, including
captured generated bytes. Every record matches the final runtime preflight;
compact, key-sorted runtime proof SHA-256:
`f6526b7a515ff9fa9e0698bee67816d7b140e7693bf0aeb71687050490c58cbb`.
Maximum physical/canonical batch: **55,055 bytes**, maximum operations **68**;
identity **10,446 bytes**, checkpoint **1,664 bytes**, owner manifest **1,246
bytes**. These measurements cover the two corrected-graph canaries, not the
complete large-owner lane. Audit: `/tmp/bounded-20261030-fixes-parent-audit.json`.
Native output includes six worker JSON/log pairs and 70 generated-source captures
beneath the configured `TestResults/live-sidecar/bounded-identity` directory.

Additional repository-required local Aspire smoke commands:

```bash
UseHexalithProjectReferences=true aspire start --no-build --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive
aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive
aspire wait eventstore --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --status healthy --timeout 30 --non-interactive
aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
```

Start/wait/stop succeeded. Description recorded 29 Running/Healthy resources and
one `tenants` resource Finished with exit code **134**. EventStore reached Healthy;
the whole topology was not green. This smoke check used existing outputs with
`--no-build`, so it does not qualify the corrected source/binary graph. The owned
isolated AppHost stopped; the separate pre-existing Tenants AppHost was untouched.
Resource state and command logs remain locally under
`/tmp/bounded-20261030-aspire-*`.

### Completed live matrix and parent audit — 2026-10-30

The restarted command ran from `/tmp/bounded-owner-20261030-snapshot`:

```bash
BOUNDED_IDENTITY_EVIDENCE_DIR=/tmp/bounded-20261030-final-live BOUNDED_IDENTITY_GENERATED_SOURCE_DIR=/tmp/bounded-20261030-final-live/generated dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityLiveTests' -result-xml /tmp/bounded-20261030-final-live.xml > /tmp/bounded-20261030-final-live.log 2>&1
```

Full live class: **9 passed, zero failed/skipped/errors/not-run**, exit 0;
`2026-10-30T06:06:26.9685491+00:00` through
`2026-10-30T06:49:52.5112204+00:00` (**2,605.663 seconds**). Parent independently
read every passing XML case and all 30 worker records. All 30 PIDs are distinct;
staging stops after the first installed participant, promotion stops during the
second owner's checkpoint handoff, and subsequent processes prove recovery,
ordinary growth, repeated rebuild, historical identity/conflict checks and, for
inherited scenarios, unchanged replay from an omitted original owner.

| Owner events | Contract | Inherited replay | Stage / promote / resume / inherited PIDs |
| ---: | --- | --- | --- |
| 129 | Streamed | No | 1810855 / 1811721 / 1811740 |
| 129 | Buffered | No | 1813426 / 1814615 / 1814899 |
| 129 | Streamed | Yes | 1040672 / 1042637 / 1042890 / 1046413 |
| 129 | Buffered | Yes | 1046477 / 1047711 / 1048618 / 1051262 |
| 10,001 | Streamed | No | 1051943 / 1090826 / 1091903 |
| 32,769 | Streamed | No | 1143176 / 1294000 / 1294931 |
| 10,001 | Buffered | No | 1489486 / 1541521 / 1542378 |
| 32,769 | Buffered | No | 1591866 / 1709424 / 1710992 |
| 10,001 | Buffered | Yes | 942122 / 980639 / 981023 / 1040095 |

Every worker's runtime proof equals the snapshot preflight
`1b23f3d60166ef21a36c85c74dc56e0fdac6047368c6a16fc913803811216f97`.
All eight actual DLL/PDB files and 1,523 ordinary/generated source documents
match their recorded hashes, including PDB checksums. All 20,427 captured input
files still match manifest hash
`5e1f53b92a693554cb1d32b3093d5fcd6d2f92c3ff49bf4aa808f46fa0a11460`.
Maximum physical/canonical batch: **110,620 bytes**, maximum operations **68**;
identity **12,302 bytes**, checkpoint **1,696 bytes**, owner manifest **1,258
bytes**. Every phase satisfies the 100-operation/one-MiB limits. Parent checked
all interruption observations and inherited checkpoint hash equality. Runner
`941555`, sidecar `941747` and all 30 workers have exited; no owned process remains.
Audit: `/tmp/bounded-20261030-final-parent-audit.json`.

This complete live matrix qualifies the original captured snapshot graph at
`d98b014a3821dcef17709ff7a6c87fd6903e6c6b`. It predates the three review corrections
and was not relabeled as execution against their later binaries. The corrected
graph separately passed 50 bounded Client tests, nine runtime-proof tests and
two process canaries as recorded above; its eight actual DLL/PDB files and all
1,523 recorded source documents still match at the final parent audit. The full
large-owner class was not repeated against those corrected binaries. Every named
frozen-matrix unit method in the earlier mapping ran and passed; the new overflow
regression also passed, with no matrix-covering skips.

The final shared-checkout HEAD observed was
`7b1df62102efb19da03553a890c62eb5ad6aeafe`, advanced by other work during this
session. Source/binary identities above, rather than that HEAD alone, bind each
run. Final owned changes are the identity range guard, its regression, worker
cancellation cleanup, CI native-diagnostic retention, this spec and three
appended deferred entries. Foreign edits, including the separate authentication
spec, were preserved. No Git staging/commit/push, dependency pin change,
publication, deployment or retired evidence machinery restoration was performed.

The broad gate remains red: corrected Client has one existing failure, and the
last full Server run has 12 existing failures; the separate no-build Aspire smoke
also recorded the failed Tenants resource. No waiver or unrelated fix was made.
Status returns to `in-progress`; generic workflow done/commit steps are not
performed. No final-workspace regression, package/release or Folders acceptance
is claimed. CI run/link: none; all new results are local.

Final complete unstaged diff, including untracked files and omitting historical deletion bodies, retained at `/tmp/bounded-owner-20261030-final-1ykzfpqk.diff`. The Git index was not staged.

### Corrected snapshot completion and fresh broad gates — 2026-10-30

The complete spec was read; `context: []` names no additional inputs. Bounded
storage, public transport, prior corrections, regression tests and reader rollout/
rollback guidance were already present. Initial verification added this record;
the subsequent completion review also corrected the kept optional observer and
its regression coverage, as recorded below. Existing source, test, workflow and
documentation edits
were preserved. The retired qualification script and committed evidence packets
were not restored. All commands below ran sequentially; every process was
awaited to completion. CI run/link: none; these are local SDK **10.0.403**,
.NET **10.0.14** and Dapr runtime **1.18.7** results.

A fresh independent source copy was captured at
`2026-10-30T07:13:25.450687+00:00` from origin HEAD
`7b1df62102efb19da03553a890c62eb5ad6aeafe` plus its existing dirty overlay:

```bash
rsync -a --exclude='.git' --exclude='bin/' --exclude='obj/' --exclude='node_modules/' --exclude='.agents/' --exclude='.codex/' --exclude='.claude/' --exclude='.venv/' --exclude='_bmad/' --exclude='_bmad-output/' --exclude='evidence/' --exclude='/references/*/references/' /home/administrator/projects/hexalith/eventstore/ /tmp/bounded-owner-resume-20261030-wTasPd/
```

All **21,275** copied files matched origin at capture and remained unchanged at
the final parent audit. The compact, key-sorted `sourceFiles` manifest SHA-256 is
`989b38c9f9ee0051f24cb6d97881d9c63cf9adb52d48ad140eeeccc3c969dfcf`;
the captured tracked-overlay diff SHA-256 is
`28c53aa224e8a57bc156a0a418c2efe64524edbf318beffb89c76b4f1e1eeba4`.
The complete manifest remains at
`/tmp/bounded-owner-resume-20261030-wTasPd-origin.json` and the overlay at
`/tmp/bounded-owner-resume-20261030-origin.diff`. No branch or nested submodule
was created, initialized or updated.

Snapshot commands (the variable names the exact retained directory and result
prefix; separate logs use the corresponding `-*-build.log` or XML basename):

```bash
bounded_run=/tmp/bounded-owner-resume-20261030-wTasPd
cd "$bounded_run"
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml "$bounded_run-client.xml"
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainPartitionedSharedProjectionRebuildTests' -result-xml "$bounded_run-domain.xml"
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SharedProjectionSourceBeginProducerTests' -class '*SharedProjectionSourceRosterActorTests' -result-xml "$bounded_run-server.xml"
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE="$bounded_run-runtime.json" BOUNDED_IDENTITY_GENERATED_SOURCE_DIR="$bounded_run-live/generated" dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml "$bounded_run-runtime.xml"
BOUNDED_IDENTITY_EVIDENCE_DIR="$bounded_run-live" BOUNDED_IDENTITY_GENERATED_SOURCE_DIR="$bounded_run-live/generated" dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityLiveTests' -result-xml "$bounded_run-live.xml"
```

All four snapshot builds passed with **zero warnings/errors**. All five test
commands exited 0 with zero failures, skips, xUnit errors or not-run cases:

| Snapshot run | Passed |
| --- | ---: |
| Client bounded identity class | 50 |
| Domain partitioned rebuild class | 144 |
| Server source producer/roster classes | 30 |
| Runtime source-binding preflight | 9 |
| Complete live process-replacement class | 9 |

The complete live class ran from
`2026-10-30T07:19:22.0235811+00:00` through
`2026-10-30T08:02:54.6622645+00:00`; xUnit duration **2,612.756 seconds**.
This run includes the overflow range guard and worker cancellation correction,
closing the earlier corrected-graph large-owner verification gap. All four
10,001/32,769-event Streamed/Buffered cases completed staging interruption,
partial second-owner handoff, recovery, ordinary growth (including 32,770),
repeated rebuild and historical duplicate/conflict checks. The three inherited
cases additionally completed unchanged replay from an omitted original owner;
the two remaining cases are the short process-replacement canaries.

Parent independently checked all nine passing live XML cases, all **30 distinct
worker PIDs** and complete phase sets, durable interruption/handoff observations,
inherited checkpoint hash equality and every named frozen-matrix unit method.
Every worker runtime array equals the preflight, whose compact, key-sorted JSON
SHA-256 is
`4fb2dba0985e36971aa81cf9f479b998f0e8325969ff266b65d09575c981a6c1`.
All **eight** actual PE/PDB files and **1,523** ordinary/generated source
documents match their recorded hashes and PDB checksums. Maximum physical/
canonical batch: **110,620 bytes**; maximum operations: **68**; identity:
**12,302 bytes**; checkpoint: **1,696 bytes**; owner manifest: **1,258 bytes**.
Every phase satisfies the 100-operation/one-MiB limits. Runner, all workers and
the owned sidecar targeting app port **33689** exited. Independent audit:
`/tmp/bounded-owner-resume-20261030-wTasPd-parent-audit.json`; native worker
JSON/logs and generated-source captures remain under
`/tmp/bounded-owner-resume-20261030-wTasPd-live`.

The broad suites ran separately from the owning checkout because Server's secret
guard requires the real Git index. Another session advanced shared HEAD to
`28ccd32c95ab521755d12deee5d7ad16927e7c92`; that external work and all dirty
changes were preserved. The same HEAD was observed before and after the broad runs.
Each suite received its own fresh successful Debug/source build with zero
warnings/errors. Their commands were:

```bash
bounded_run=/tmp/bounded-owner-resume-20261030-wTasPd
cd /home/administrator/projects/hexalith/eventstore
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml "$bounded_run-shared-client-full.xml"
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml "$bounded_run-shared-domain-full.xml"
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml "$bounded_run-shared-server-full.xml"
```

| Fresh shared-checkout suite | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| Full Client | 2,249 | 1 | 2 | 1 |
| Full Domain | 1,286 | 0 | 1 | 0 |
| Full Server | 6,431 | 12 | 25 | 1 |

All three report zero xUnit errors and not-run cases. The last suite finished at
`2026-10-30T08:14:18.2188230+00:00`. Build logs use
`-shared-{client,domain,server}-build.log`; test logs share each XML's basename.
Client's sole failure is the previously recorded routing-header ownership guard.
Server's 12 failures are the previously recorded callback-quota, SignalR
metadata-cap and secret-guard cases, seven query-provenance cases and two
actor-auth cases. Existing separate-lane skips remain two Client isolated-Dapr
tests, one Domain content-live test and 25 Server opt-in/ATDD cases. No bounded
matrix test failed or was skipped. No foreign failure was fixed or waived.

The complete corrected snapshot is now verified for its exact captured sources
and runtime images. Shared-checkout broad results belong to their separately
built assemblies and source-inspection environment; they are not relabeled as
the snapshot or as qualification of a final unchanged workspace. The broad gate
remains red, so status stays `in-progress`. No overall regression, package/
release or Folders acceptance is claimed. The original baseline and frozen intent
are unchanged. This invocation performed no Git staging, commit, push,
dependency pin change, publication or deployment.

### Completion review correction and final checks — 2026-10-30

All three review layers completed. Each of the ten blind findings and the one
verification-gap proposal received its own verdict before grouping. One earlier
catalog finding was carried without another deferred entry; eight independent
findings from routing, authentication, content and fixture work were appended to
the deferred ledger. The optional observer caller-test proposal was rejected as
logged. The sole direct correction is the existing OS observer's FIFO open.
No captured-identity C# source, public API or storage schema changed in this run.

`tools/bounded_worker_observer.py` now opens its existing held descriptor with
`O_NONBLOCK`, allowing the existing `fstat` guard to reject FIFO image or symbol
paths before waiting for a writer. The new subprocess regression exercises both
paths, requires an exit-1 failure receipt containing the nonregular-file error
and raw mappings, and always kills/reaps a child that exceeds its two-second
deadline. This changes the kept helper, without restoring retired qualification
tools or committed evidence.

Commands from the owning repository root:

```bash
python3 -m unittest discover -s tools/tests -p 'test_bounded_worker_observer.py' -k fifo > /tmp/bounded-observer-fifo-iQ1zbQ/red.log 2>&1
python3 -m unittest discover -s tools/tests -p 'test_bounded_worker_observer.py' > /tmp/bounded-observer-fifo-iQ1zbQ/green.log 2>&1
python3 -m unittest discover -s tools/tests -p test_bounded_worker_observer.py -v > /tmp/bounded-owner-resume-20261030-observer-parent.log 2>&1
git diff --check
```

Before the production correction, the one new test method failed both PE/PDB
subcases by timeout (exit 1); both children were killed and reaped. Afterward,
all **11 observer tests passed**, with zero failures/errors/skips, exit 0. Parent
independently inspected the patch and red log, reran all 11 tests successfully,
and checked whitespace. The only Git diagnostic was the pre-existing Client
test file's CRLF normalization warning.

The completed 9/9 live matrix remains bound to its retained snapshot, with all
30 workers and runtime/source identities independently audited above. That run
did not enable the optional OS observer; the modified Python helper is verified
separately by its focused suite. No C# rebuild or repeat of the long live lane
was needed for this Python-only correction. The broad Client/Domain/Server
results remain their separately recorded runs; their 13 existing failures were
not fixed, hidden or waived. The broad gate therefore still prevents story
completion, and status returns to `in-progress`. No overall regression,
package/release or Folders acceptance is claimed. CI run/link: none.

Final unstaged cumulative diff, including untracked files and omitting historical
deletion bodies: `/tmp/bounded-owner-resume-20261030-final-gmem5_x8.diff`.
Final HEAD observed: `28ccd32c95ab521755d12deee5d7ad16927e7c92`; the Git
index has no staged diff. Frozen intent and the original baseline remain intact.
No owned test, worker, sidecar or review subprocess remains.

### Current-source verification — 2026-10-30

The complete spec was read; `context: []` supplies no additional inputs. The
bounded implementation, regression tests and rollout/rollback guidance were
already present; initial verification needed no source or test change. Completion
review subsequently enrolled the existing observer suite in CI and recorded five
independent follow-ups. Origin was clean `main` at
`4dde72ef8cce4252edcd793d012bc40bb668f746`. CI run/link: none; these are local
SDK **10.0.403**, .NET **10.0.14**, Dapr CLI **1.18.2** / runtime **1.18.7** results.

Build/test commands ran sequentially and every process was awaited. From the
owning repository root (the variable abbreviates the exact retained output path;
logs use the corresponding build or XML basename):

```bash
bounded_run=/tmp/bounded-owner-20261030-current-ySZ3fb
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml "$bounded_run/client.xml"
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml "$bounded_run/client-full.xml"
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainPartitionedSharedProjectionRebuildTests' -result-xml "$bounded_run/domain.xml"
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml "$bounded_run/domain-full.xml"
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SharedProjectionSourceBeginProducerTests' -class '*SharedProjectionSourceRosterActorTests' -result-xml "$bounded_run/server.xml"
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml "$bounded_run/server-full.xml"
python3 -m unittest discover -s tools/tests -p test_bounded_worker_observer.py -v
rsync -a --exclude='.git' --exclude='bin/' --exclude='obj/' --exclude='node_modules/' --exclude='.agents/' --exclude='.codex/' --exclude='.claude/' --exclude='.venv/' --exclude='_bmad/' --exclude='_bmad-output/' --exclude='evidence/' --exclude='/references/*/references/' /home/administrator/projects/hexalith/eventstore/ "$bounded_run/snapshot/"
cd "$bounded_run/snapshot"
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE="$bounded_run/runtime.json" BOUNDED_IDENTITY_GENERATED_SOURCE_DIR="$bounded_run/live/generated" dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityRuntimeProofTests' -result-xml "$bounded_run/runtime.xml"
BOUNDED_IDENTITY_EVIDENCE_DIR="$bounded_run/live" BOUNDED_IDENTITY_GENERATED_SOURCE_DIR="$bounded_run/live/generated" dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*ProjectionBoundedIdentityLiveTests' -result-xml "$bounded_run/live.xml"
```

All four builds passed with **zero warnings/errors**. Every test command reports
zero xUnit errors and not-run cases:

| Run | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| Client bounded identity | 50 | 0 | 0 | 0 |
| Domain partitioned rebuild | 144 | 0 | 0 | 0 |
| Server source producer/roster | 30 | 0 | 0 | 0 |
| Full Client | 2,249 | 1 | 2 | 1 |
| Full Domain | 1,286 | 0 | 1 | 0 |
| Full Server | 6,431 | 12 | 25 | 1 |
| Snapshot runtime preflight | 9 | 0 | 0 | 0 |
| Snapshot complete live class | 9 | 0 | 0 | 0 |

The observer suite separately passed **11/11**, with no failures/errors/skips.
Client's sole failure remains the routing-header ownership guard; Server's 12
remain the callback-quota, SignalR metadata-cap and secret-guard cases, seven
query-provenance cases and two actor-auth cases recorded above. Existing skips
remain separate opt-in/ATDD lanes. No bounded-matrix test failed or was skipped;
no foreign failure was fixed or waived.

The independent source capture at `2026-10-30T09:55:33.702827+00:00` matched all
**21,294** input files against origin; all captured bytes remain unchanged.
`snapshot-origin.json` retains the input manifest, whose compact, key-sorted
`sourceFiles` SHA-256 is
`2bad90424a08512647d8fb13bb022aa91e40b95c643eb9619c643ed75f474ec7`.
The complete live class ran from `2026-10-30T09:56:01.2008546+00:00` through
`2026-10-30T10:47:57.2676486+00:00` (**3,116.217 seconds**). All four large-owner
Streamed/Buffered scenarios completed interrupted staging, partial second-owner
handoff, recovery, growth through 32,770 where applicable, repeated rebuild and
historical identity/conflict checks; inherited scenarios also proved unchanged
replay from an omitted original owner.

Parent independently checked every passing live XML case and named frozen-matrix
unit method, all **30 distinct worker PIDs**, interruption/handoff observations,
inherited checkpoint hash equality and worker runtime arrays. All **eight** actual
PE/PDB files and **1,525** source documents match their recorded hashes and PDB
checksums, including captured generated bytes. Compact, key-sorted runtime proof
SHA-256: `5eba6db49d8162c171e22c051014ac264bffea68df9d478a5bc9ea84eba7b92c`.
Maximum physical/canonical batch: **110,620 bytes / 68 operations**; identity:
**12,302 bytes**; checkpoint: **1,696 bytes**; owner manifest: **1,258 bytes**.
Every phase meets the 100-operation/one-MiB limits. Runner, all workers and the
owned sidecar targeting app port **46295** exited. Final local audit:
`/tmp/bounded-owner-20261030-current-ySZ3fb/final-audit.json`.

Shared-checkout unit results and snapshot live results retain their separate
source/binary identities. HEAD remains unchanged; the later foreign edit to
`docs/implementation/ext-protection-1-owner-acceptance.md` was preserved. The
broad gate remains red, so status stays `in-progress`; no overall regression,
package/release or Folders acceptance is claimed. Frozen intent and the original
baseline remain intact. No Git staging/commit/push, dependency pin change,
publication, deployment, nested-submodule update or retired qualification-tool/
committed-evidence restoration was performed.

### Current-source review correction and closeout — 2026-10-30

All three review layers returned, and all 12 findings received individual verdicts
before grouping. One verification gap was corrected: the existing 11-test
`test_bounded_worker_observer.py` suite now runs in the Linux `live-sidecar` job
alongside its existing Python checks. Five independent follow-ups were appended
to `deferred-work.md`, one explicitly unverified; six previously logged findings
were carried without duplicate entries. No production or test source changed.

After the implementation agent's successful checks, parent independently ran:

```bash
python3 -m unittest discover -s tools/tests -p test_bounded_worker_observer.py -v > /tmp/bounded-owner-20261030-current-ySZ3fb/observer-parent-after-review.log 2>&1
actionlint .github/workflows/integration.yml
git diff --check
```

All commands exited 0; all **11 observer tests passed**, without failures, errors
or skips. Parent read the three-line workflow addition and rechecked the final
XML counts, frozen block and original baseline. The completed **9/9** live matrix
and **224** focused Client/Domain/Server passes remain tied to their recorded
source/binary graphs. The CI-selection change postdates the snapshot; no C#
rebuild or repeat of the long live lane was needed for this workflow-only edit,
and those results are not relabeled as a run of the final workflow. CI run/link:
none.

Owned changes are the CI enrollment, this spec and five appended deferred entries.
The foreign acceptance-document edit was preserved. The broader gate still has
one Client and 12 Server failures, so status returns to `in-progress`; no overall
regression, package/release or Folders acceptance is claimed. The generic done
and commit steps are not performed: the failed regression gate and explicit
no-commit boundary remain in force. No Git staging, commit, push, dependency pin
change, publication, deployment or retired evidence machinery restoration occurred.

Final unstaged cumulative diff, including untracked files and omitting historical
deletion bodies: `/tmp/bounded-owner-current-audit-z5hqokbm.diff`. Final HEAD:
`4dde72ef8cce4252edcd793d012bc40bb668f746`; the Git index has no staged changes.
All owned test, worker and sidecar processes exited.

### Fresh bounded verification and broad gates — 2026-10-30

The complete spec was read; `context: []` supplies no additional files. All seven
implementation tasks, regression tests and reader rollout/rollback guidance were
already present. Initial verification found no missing bounded behavior; a later
concurrent feature-scoped review added correction work recorded separately.
Initial checkout was clean
`main` at `a5b55e8c34892c30fb4feccefd5498dfe0363585`; HEAD remained unchanged.
CI run/link: none. These are local SDK **10.0.403** / .NET **10.0.14** results.

The .NET builds and test runs ran sequentially from the owning repository root,
with every process awaited. The independent Python/workflow checks also completed.
The variable below abbreviates the exact retained output directory; each build/XML
basename also identifies its separate `.log` file:

```bash
bounded_run=/tmp/bounded-owner-a5b55e8-iS9SFH
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml "$bounded_run/client.xml"
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml "$bounded_run/client-full.xml"
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml "$bounded_run/domain-full.xml"
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -method '*ProcessPublicationTests.RegistrationCostDoesNotGrowWithHistoricalPages' -method '*ProcessPublicationTests.FreshTenantRegistrationUsesTailAfterManyTenants' -result-xml "$bounded_run/domain-process-retry.xml"
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml "$bounded_run/server-full.xml"
python3 -m unittest discover -s tools/tests -p test_bounded_worker_observer.py -v
actionlint .github/workflows/integration.yml
git diff --check
```

All three builds passed with **zero warnings/errors**. All xUnit runs report zero
errors and not-run cases:

| Executed run | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| Client bounded identity class | 50 | 0 | 0 | 0 |
| Full Client | 2,249 | 1 | 2 | 1 |
| Full Domain | 1,284 | 2 | 1 | 1 |
| Isolated Domain process-registration retry | 2 | 0 | 0 | 0 |
| Full Server | 6,452 | 15 | 25 | 1 |

The full Domain XML contains **144/144 passing** partitioned-rebuild cases; the
full Server XML contains **30/30 passing** source producer/roster cases. These
subset counts come from the executed full suites; separate focused commands were
not run for those projects. Every named frozen-matrix unit method passed, including
all large-owner and mutation theory rows. No bounded-matrix case failed or was
skipped. The existing observer suite separately passed **11/11**, with zero
failures/errors/skips; `actionlint` passed.

Client's sole failure remains the routing-header ownership guard. Domain's two
full-run failures are `ProcessPublicationTests.RegistrationCostDoesNotGrowWithHistoricalPages`
and `FreshTenantRegistrationUsesTailAfterManyTenants`, both timing out in
`ProcessReadDeadline` during registration-store reads. Both passed one isolated
retry against the unchanged binaries; the original broad failures remain retained
and unwaived. Server's 15 failures comprise the earlier callback-quota, SignalR
metadata-cap, secret-guard, seven query-provenance and two actor-auth cases, plus
`EventStoreAuthorizationRegistrationTests.AddEventStore_MediatRPipelineOrdering_ExecutesInCorrectOrderAsync`
and two `DaprInternalAuthenticationHandlerTests` cases:
`DelegatedAdministratorCredential_BootstrapsTheGlobalAdministrator` and
`ValidSignedWorkloadCommandReachesRouter`. The pipeline assertion expects three
behaviors while the current catalog work registers four. These separate failures
were not changed or waived. Existing skips remain two Client isolated-Dapr cases,
one Domain content-live case and 25 Server opt-in/ATDD cases.

`unit-source-start.json` retains hashes for **4,568** EventStore-owned source/build
inputs. The final rehash in `unit-source-final.json` found no changed or missing
captured .NET source/build input. Five ext-protection Python files changed during
the runs, and independent ext-protection runtime-closure/test files were added;
all foreign edits were preserved. These local unit results describe their built
assemblies and source-inspection environment, not one unchanged whole workspace.

At that initial verification phase, the bounded production sources and test/helper
files were byte-identical to those in the retained `4dde72ef` live snapshot. Parent independently rechecked its nine
passing cases, 30 distinct worker PIDs, eight actual PE/PDB hashes and all **1,525**
source documents/PDB checksums. That **9/9** complete live result and its bounded
measurements remain historical evidence for that exact snapshot graph. The long
live lane was not repeated: no participating bounded source changed and the broad
gate remains red. No current-binary live qualification is claimed.

The repository-required baseline smoke used the local `aspire-orchestration`
skill and existing outputs:

```bash
UseHexalithProjectReferences=true aspire start --no-build --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive
aspire wait eventstore --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --status healthy --timeout 30 --non-interactive
aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive
aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
```

Start/describe/stop exited 0. The wait timed out after 30 seconds (exit **17**);
the final description showed all **30** resources Running with no EventStore
health report. This no-build smoke does not qualify the current source/binary
graph. Logs are `/tmp/bounded-a5b55e8-aspire-*`; the owned isolated AppHost stopped
before the builds.

The broad regression gate remains red, so status stays `in-progress`. Frozen
intent and the original baseline are unchanged. No overall regression, package/
release or Folders acceptance is claimed. No source/test change, dependency pin
change, Git staging/commit/push, publication, deployment, nested-submodule update
or retired qualification-tool/committed-evidence restoration was performed.


### Direct review corrections and final verification — 2026-10-30

The owner assigned the direct scoped-review fixes to this run. All ten patch
items are complete. The later recorded owner decision defers evidence garbage
collection and retires the runtime source-binding/OS-observer machinery; these
are no longer open design questions. The frozen block and original
`baseline_commit` are preserved verbatim. No storage schema, public API or pin
changed. The broader regression gate remains red, so status stays `in-progress`.

Production corrections restore `RebuildRequired` when delivery exceeds an
existing captured prefix, admit inline sequences before offset multiplication,
refresh and validate the buffered accumulation session after identity reservation,
and dispose cleanup `JsonDocument` values. Reader guidance now identifies
new-dispatcher deployment as the paged-write gate. Seven new test methods add
**13 cases**, including persisted-state controls for selected retirement, pending
journal contradiction, ordinary-tail position and changed streamed evolution.

Parent independently read the complete six-file correction diff and the red XML.
Before the corrections, the new Client controls recorded **7 passed / 4 failed**:
both paged/inline older-receipt replays returned `AlreadyCaptured`, one extreme
inline sequence aliased the first digest, and `long.MaxValue` threw. Domain red
controls recorded **1 passed / 1 failed**, reproducing three preparation passes
instead of the pre-existing two-pass scratch flow. After correction, focused
Client passed **61/61** and the two new Domain methods **2/2**, without skips,
errors or not-run cases. Logs/XML: `/tmp/bounded-review-a5b55e8-3ONbBk`.

Fresh parent verification ran sequentially from the owning repository root. All
processes were awaited. The directory variable abbreviates the exact retained
output path; separate build/XML basenames identify corresponding `.log` files.

```bash
bounded_final=/tmp/bounded-owner-final-a5b55e8-cwy2v5bf
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml "$bounded_final/client-full.xml"
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml "$bounded_final/domain-full.xml"
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml "$bounded_final/server-full.xml"
```

| Fresh full suite | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| Client | 2,260 | 1 | 2 | 1 |
| Domain | 1,288 | 0 | 1 | 0 |
| Server | 6,452 | 15 | 25 | 1 |

All three builds passed with **zero warnings/errors**; all suites report zero
xUnit errors/not-run cases. Their XML contains **61/61** bounded Client,
**146/146** partitioned Domain and **30/30** source producer/roster passes:
**237 relevant cases**, with every named frozen-matrix method and all new controls
present as Pass. The earlier two process-registration timeouts passed in this
fresh complete Domain run; its earlier red result remains retained separately.
Client's sole routing-header guard and Server's 15 catalog-pipeline, callback,
SignalR, secret-guard, authentication and query-provenance failures match the
initial verification above. No separate runtime failure was fixed or waived.
Existing opt-in/ATDD skips remain separate from bounded identity coverage.

Before helper retirement, the corrected graph also passed nine runtime-proof
tests and two process canaries; `commands.json` retains those exact intermediate
commands. Those results predate the approved cleanup and do not describe the
final test inventory. Retirement deleted the two runtime-proof C# files and the
observer/tool tests, removed worker runtime capture and parent runtime-array
comparison, and removed the observer CI step/generated-source environment.
Parent inspected every changed call site. All phase, distinct-PID, recovery,
inheritance and batch/value-bound assertions remain unchanged.

The implementation agent then built the final LiveSidecar project with zero
warnings/errors, passed `actionlint .github/workflows/integration.yml`, and found
no active retired-helper references. Parent ran the final current canaries:

```bash
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
BOUNDED_IDENTITY_EVIDENCE_DIR="$bounded_final/post-cleanup-live" dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*ProjectionBoundedIdentityLiveTests.FocusedProcessReplacementExercisesPartialStageAndOwnerHandoff' -result-xml "$bounded_final/post-cleanup-live.xml"
git diff --check
```

Post-cleanup streamed/buffered **129-event process canaries: 2/2 passed**, zero
failures/skips/errors/not-run, exit 0, **30.823 seconds**. Parent independently
checked all **six distinct worker PIDs**, interrupted staging and partial
second-owner handoff/recovery observations, absence of the retired runtime field,
and measured bounds. All six workers exited. Maximum physical/canonical batch:
**55,055 bytes / 68 operations**; identity **10,338 bytes**, checkpoint **1,664
bytes**, owner manifest **1,246 bytes**. These measurements cover these short
canaries, not a new full large-owner live run. The earlier complete 9/9 live
matrix remains historical evidence for its separately recorded graph.

`source-before.json` / `source-after.json` show unchanged .NET source/build inputs
through the broad suites and intermediate live checks. The final rehash found
exactly the four intended LiveSidecar input changes (two removals and two call-site
edits), with no added source/build inputs. Unit-suite participating sources stayed
unchanged; final LiveSidecar inputs received their own fresh build. Parent audit:
`/tmp/bounded-owner-final-a5b55e8-cwy2v5bf/post-cleanup-audit.json`.

All three required review layers completed and their individual verdicts are
recorded above: four carried findings, one rejected historical-artifact claim,
and eleven independent deferred follow-ups, two explicitly unverified. The
concurrent scoped review's original nine direct items and subsequently approved
helper retirement are now complete. Evidence garbage collection remains deferred
by the owner's decision. Foreign EXT-PROTECTION source, tests, tools and document
edits were preserved.

Source revision: `a5b55e8c34892c30fb4feccefd5498dfe0363585` plus the recorded dirty
changes. CI run/link: none; these are local SDK **10.0.403**, .NET **10.0.14** and
Dapr **1.18.7** results. The broad gate still has **16 failures**, so no overall
regression, package/release or Folders acceptance is claimed. The generic workflow
done/commit steps are not performed. No Git staging/commit/push, dependency change,
publication, deployment, nested-submodule update or retired-tool restoration ran.

Final cumulative diff, including untracked inputs and omitting historical evidence/deletion bodies: `/tmp/bounded-owner-final-reviewed-mrmhd5gk.diff`. The Git index remains unstaged. All owned verification processes were awaited.

### Pre-patch committed implementation verification — 2026-10-30

This section records execution before the concurrently added patch-delta review
corrections. It is retained as pre-patch evidence; later correction checks have
their own results below.

Read the complete spec; `context: []` names no additional files. The implementation,
direct review corrections, regression tests and rollout/rollback guidance are
already present at `dd02dd054ff28140a12d7b9a2b8cd3772c509051`. Inspection found no
missing bounded-owner behavior requiring a source or test change before that
review arrived. This initial phase added only this verification record,
preserving the frozen intent, original baseline
and `in-progress` status. CI run/link: none; these are local SDK **10.0.403**,
.NET **10.0.14** and Dapr runtime **1.18.7** results.

Commands ran sequentially from the owning repository root; all processes were
awaited. Logs use the corresponding build or XML basename under the directory
below. All four builds passed with **zero warnings/errors**.

```bash
bounded_run=/tmp/bounded-owner-dd02dd054-GWiU7LMj
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml "$bounded_run/client-full.xml"
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml "$bounded_run/domain-full.xml"
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml "$bounded_run/server-full.xml"
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
BOUNDED_IDENTITY_EVIDENCE_DIR="$bounded_run/live" dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*ProjectionBoundedIdentityLiveTests.FocusedProcessReplacementExercisesPartialStageAndOwnerHandoff' -method '*ProjectionBoundedIdentityLiveTests.FocusedInheritedOwnerReplayAfterProcessReplacement' -result-xml "$bounded_run/live.xml"
actionlint .github/workflows/integration.yml
git diff --check
```

| Current run | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| Full Client | 2,260 | 1 | 2 | 1 |
| Full Domain | 1,288 | 0 | 1 | 0 |
| Full Server | 6,457 | 15 | 25 | 1 |
| Short live process-replacement and inherited-replay cases | 4 | 0 | 0 | 0 |

All XML reports have zero errors/not-run cases. The executed full suites contain
**61/61** bounded Client, **146/146** partitioned Domain and **30/30** source
producer/roster passes: all **237** relevant unit cases, including the named
frozen-matrix methods and correction controls. These subset counts are extracted
from the full runs, not separate focused executions. Client's routing-header
ownership guard and Server's 15 pipeline, callback-quota, Dapr/actor-authentication,
SignalR metadata-cap, secret-guard and query-provenance failures match the previous
verification record. They remain unresolved and unwaived; separate opt-in/ATDD
skips do not cover the bounded matrix.

The four **129-event** live cases passed in **65.087 seconds**, using **14 distinct
worker PIDs**. Worker records confirm interrupted staging, partial second-owner
handoff/recovery and unchanged inherited replay/checkpoint hashes. Every phase
asserts bounds: maximum physical/canonical batch **55,055 bytes / 68 operations**,
identity **10,394 bytes**, checkpoint **1,664 bytes**, owner manifest **1,246 bytes**.
All 14 workers and the owned sidecar for app port **34225** exited. These are
current short-case measurements; the earlier complete large-owner live matrix
remains historical evidence for its recorded graph and was not rerun. Retired
runtime-proof and observer helpers remain absent. `actionlint` and whitespace
checks passed.

The required no-build isolated Aspire smoke reached EventStore Healthy in
**11.3 seconds**; description showed all **30** resources Running/Healthy, and
the owned AppHost stopped successfully. Its start/wait/describe/stop commands are
the same as the preceding baseline smoke; logs are `aspire-*` under this run's
directory. Existing outputs were used, so this is a topology smoke, not current
source qualification.

The initial checkout was clean. Separate concurrent EXT-PROTECTION tooling/CI
edits were first observed after the broad runs and were preserved; no bounded
.NET input change was observed. Test results describe their built assemblies and
source-inspection environment, not an unchanged whole workspace. Evidence garbage
collection remains owner-deferred. The broad gate still has **16 failures**, so
no overall regression, package/release or Folders acceptance is claimed. No Git
staging/commit/push, dependency change, publication, deployment, nested-submodule
update or retired-tool restoration was performed.

### Patch-delta corrections and focused verification — 2026-10-30

The five patch-delta review items are complete. The buffered session reread now
compares reservation namespace/count instead of reconstructing an unsealed
reference. The unreachable captured-owner guard is removed, and the inline
overflow control expresses its aliasing sequence as `(1L << 59) + 1`. The guide
now requires pausing shared rebuilds during a rolling upgrade, then starting or
resuming them only after every reader, writer and maintenance process supports
paged checkpoints. No public API, storage schema or dependency pin changed.

The new concurrency regression resumes a real receipt outage with its immutable
paged manifest installed and ordinal zero still unaccepted. An existing store
read observer completes the same request while the original is reserving its
identity. The original returns `AlreadyCompleted`; persisted owner count and
inventory fingerprint advance exactly once, and checkpoint handoff completes.
Removing only the post-reservation rejection guard made this final regression
fail with persisted count **2**, expected **1**. The dispatcher was then restored
to its exact pre-mutation bytes before final builds. The expected-red control
used the same Domain build command below followed by:

```bash
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -method '*ConcurrentOrdinalCompletionDuringIdentityReservationCannotAppendInventoryTwice' -result-xml "$bounded_run/patch-concurrency-final-mutation-red.xml"
```

Final commands ran sequentially from the owning repository root; every process
was awaited. `bounded_run` remains `/tmp/bounded-owner-dd02dd054-GWiU7LMj`;
build logs are `patch-{client,domain}-final-build.log`, and test logs share their
XML basename. Both final builds passed with **zero warnings/errors**.

```bash
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*SharedProjectionBoundedIdentityTests' -xml "$bounded_run/patch-client-final.xml"
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainPartitionedSharedProjectionRebuildTests' -result-xml "$bounded_run/patch-domain-final.xml"
git diff --check
```

Final bounded Client: **61/61 passed** (**7.251 seconds**). Final partitioned
Domain: **147/147 passed** (**202.290 seconds**), including the new regression.
Both runs exited 0 with zero failures, skips, errors or not-run cases. Whitespace
checks passed; Git reported only the existing Client test file's CRLF-to-LF
normalization warning. The earlier full-suite and four short live results remain
explicitly pre-patch; unrelated broad sweeps and the long live lane were not
repeated for these direct corrections.

Source revision is `dd02dd054ff28140a12d7b9a2b8cd3772c509051` plus these five
code/test/guide edits. CI run/link: none for this local verification. Frozen intent,
original baseline and all concurrent review/deferred/tooling edits are preserved.
Evidence garbage collection remains owner-deferred; the **16 pre-patch broad
failures** remain unresolved, so status stays `in-progress`. No overall regression,
package/release or Folders acceptance is claimed. No Git staging/commit/push,
dependency change, publication, deployment or retired-helper restoration ran.

### Final reviewed correction and parent verification — 2026-10-30

All three review layers completed. Ten blind findings have individual verdicts
above; the other two layers found no gaps in the five-file patch. B6 and B10
are fixed. Two new pre-existing validation/admission issues were appended to
`deferred-work.md`; two earlier findings were carried without duplicate entries.
The full cumulative review input includes intervening features and was not
exhaustively traced; detailed review covered this run's patch and bounded
projection dependencies.

Legacy journal proof now validates vector divisibility/count before multiplying,
and admits the pending sequence before checked offset arithmetic. Two public-API
regressions use pending sequences 1 and `(1L << 59) + 1`, including the latter
through public legacy checkpoint seeding. Both incorrectly returned true before
the correction (**2 failed**); afterward both and the legacy compatibility control
passed (**3/3**). No private-state fabrication or public API change was needed.
The guide now names host-owned submitter/retry shutdown, in-flight call draining,
quiescence throughout rollout and compatible-version restart.

The final Client Debug/source build passed with zero warnings/errors. Parent
read the complete correction diff and independently ran:

```bash
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml /tmp/bounded-owner-dd02dd054-GWiU7LMj/review-final-client-full.xml
```

Result: **2,262 passed / 1 failed / 2 skipped**, zero errors/not-run, exit **1**,
**95.116 seconds**. All **63 bounded-identity cases passed**, including both new
overflow rows. The sole failure is
`DaprRoutingHeaderOwnershipGuardTests.HostSources_DelegateDaprRoutingHeaderOwnershipToThePlatformHandlerOnly`:
`DaprRouteCatalogStateStore` remains an additional routing-header setter. The
last Domain focused result remains **147/147 passed** before this final Client-only
arithmetic correction. The earlier full Domain, full Server and four live results
retain their separately recorded sources and are not relabeled as reruns.

Source: `dd02dd054ff28140a12d7b9a2b8cd3772c509051` plus this run's recorded
code/test/guide edits. CI run/link: none for these local checks. The failed Client
guard and 15 previously recorded Server failures remain unresolved; status is
`in-progress`, with no overall qualification, package/release or Folders acceptance.
Frozen intent and the original baseline are preserved. All owned processes were
awaited; concurrent tooling/CI/review edits are preserved. No Git staging, commit,
push, dependency change, publication, deployment or retired-helper restoration ran.

### Current implementation resumption and verification — 2026-10-30

Read the complete spec; `context: []` names no additional inputs. The bounded
implementation, regression tests and rollout/rollback guide are already present.
No additional production or test change was needed. This resumption appends only
this verification record, preserving the frozen intent, original baseline and
`in-progress` status. CI run/link: none initiated or claimed by this resumption;
these are local SDK **10.0.403**, .NET **10.0.14**, Dapr CLI **1.18.2** / runtime
**1.18.7** results.

External Git work advanced the initial `dd02dd054ff28140a12d7b9a2b8cd3772c509051` checkout to
`6adfa33ae737d1c1baf0ce1f99847f0649f874e0` during initial inspection, then
recorded the nine already checked-out submodule revisions in
`e92e6694a93c79531368fa3bbf6c06462fd05484`. The latter commit changes only
gitlinks; root production, test and build configuration files are unchanged. The checked-out submodule
revisions are retained in `submodules.txt` and `submodules-after.txt` under the
directory below. All external work was preserved; this resumption performed no
Git staging, commit, push, branch or dependency mutation.

The .NET builds and test commands ran sequentially from the owning repository
root; every process was awaited. All four builds passed with **zero
warnings/errors**. Independent workflow and whitespace checks also completed.
Separate logs and XML remain under the exact directory named by `bounded_run`:

```bash
bounded_run=/tmp/bounded-owner-6adfa33a-bD6fCo
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true > "$bounded_run/client-build.log" 2>&1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -result-xml "$bounded_run/client-full.xml" > "$bounded_run/client-full.log" 2>&1
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true > "$bounded_run/domain-build.log" 2>&1
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -result-xml "$bounded_run/domain-full.xml" > "$bounded_run/domain-full.log" 2>&1
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true > "$bounded_run/server-build.log" 2>&1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -result-xml "$bounded_run/server-full.xml" > "$bounded_run/server-full.log" 2>&1
ASPNETCORE_URLS=http://127.0.0.1:0 dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*QueryResponseProvenancePersistenceTests' -result-xml "$bounded_run/server-query-ephemeral.xml" > "$bounded_run/server-query-ephemeral.log" 2>&1
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true > "$bounded_run/live-build.log" 2>&1
BOUNDED_IDENTITY_EVIDENCE_DIR="$bounded_run/live" dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*ProjectionBoundedIdentityLiveTests.FocusedProcessReplacementExercisesPartialStageAndOwnerHandoff' -method '*ProjectionBoundedIdentityLiveTests.FocusedInheritedOwnerReplayAfterProcessReplacement' -result-xml "$bounded_run/live.xml" > "$bounded_run/live.log" 2>&1
actionlint .github/workflows/integration.yml
git diff --check
```

| Current run | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: |
| Full Client | 2,262 | 1 | 2 | 1 |
| Full Domain | 1,289 | 0 | 1 | 0 |
| Full Server | 6,457 | 15 | 25 | 1 |
| Query-provenance retry on an ephemeral URL | 0 | 7 | 0 | 1 |
| Short live process-replacement and inherited-replay cases | 4 | 0 | 0 | 0 |

All XML reports have zero errors/not-run cases. The complete unit runs contain
**63/63** bounded Client, **147/147** partitioned Domain and **30/30** source
producer/roster passes: **240 relevant cases**, including every named frozen
matrix method and all correction controls. No bounded-matrix case failed or was
skipped. Existing opt-in/ATDD skips remain separate lanes.

Client's sole failure remains the routing-header ownership guard. Server's eight
non-binding failures remain the pipeline-ordering, callback-quota, SignalR
metadata-cap, secret-guard, two internal-authentication and two actor-authorization
cases previously recorded. Its seven query-provenance rows initially failed to
bind `127.0.0.1:5000`, which is held by an external Aspire process that was left
untouched. The focused environment-only retry removed that bind failure, but all
seven then returned **503 ServiceUnavailable** instead of **200 OK**. That retry
is a failure, not a passing environment control; no deeper cause is inferred
from the HTTP assertion alone, and no broad failure is fixed or waived.

The four **129-event** live cases passed in **74.089 seconds** using **14 distinct
worker PIDs**, all now exited. Native worker records verify interrupted staging,
partial second-owner handoff, recovery and unchanged inherited replay/checkpoint
hashes. Every phase enforces bounds: maximum physical/canonical batch **55,055
bytes / 68 operations**, identity **10,366 bytes**, checkpoint **1,664 bytes** and
owner manifest **1,246 bytes**. These measurements cover the current short cases;
the complete large-owner live matrix remains historical evidence for its recorded
graph and was not rerun. Retired runtime-proof, observer and qualification helpers
remain absent. Workflow lint and whitespace checks passed.

The current complete regression gates still fail, so status remains
`in-progress`; no overall regression, package/release or Folders acceptance is
claimed. Evidence garbage collection remains owner-deferred. No publication,
deployment, nested-submodule update or retired-helper restoration was performed.

### Resumption review correction and parent verification — 2026-10-30

All three review layers completed and all twelve findings were triaged above.
The sole patch adds `DispatcherCommitValidatesCoveredPendingDeliveriesAgainstPagedIdentity`
to `tests/Hexalith.EventStore.DomainService.Tests/DomainPartitionedSharedProjectionRebuildTests.BoundedIdentity.cs`.
Its four rows exercise buffered/streamed owners with matching/contradictory
pending sequence 66 beyond the first proof page. Matching commit proves handoff,
journal draining, duplicate retry and subsequent ordinary growth without a
second fold. Contradictory commit preserves the exact checkpoint captured after
journal admission, the selected view/generation and the pending delivery.

The target Debug/source build passed with zero warnings/errors; implementer
verification passed **4/4** with zero skips/errors/not-run. Its initial two
negative assertions took the checkpoint snapshot before journal admission;
that admission legitimately advances the checkpoint. Moving the snapshot to the
actual pre-rebuild boundary corrected the fixture without changing production
behavior or reducing the preservation assertions. Logs are under
`/tmp/bounded-pending-dispatcher-vIMy1r`.

Parent read the complete test correction and independently ran:

```bash
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -method '*DispatcherCommitValidatesCoveredPendingDeliveriesAgainstPagedIdentity' -result-xml /tmp/bounded-pending-dispatcher-vIMy1r/parent-tests.xml
```

Result: **4 passed / 0 failed / 0 skipped**, zero errors/not-run, exit **0**,
**1.273 seconds**. Scoped whitespace validation passed. Earlier complete suites,
**240 relevant unit cases** and four short live cases retain their separately
recorded results; they were not repeated for this test-only addition. These four
new cases bring unique relevant unit coverage executed in this run to **244**.
No bounded runtime code or shared test helper changed.

Source revision is `e92e6694a93c79531368fa3bbf6c06462fd05484` plus this run's
test, spec and deferred-ledger edits. Concurrent external Server security
source/test edits appeared after the broad runs and are preserved; those earlier
results do not qualify the later whole working tree. CI run/link: none for these
local checks. The last complete Client/Server sweeps still have **16 failing
tests**, so no overall regression, package/release or Folders acceptance is
claimed. The full large-owner live lane remains historical, as recorded above.
No Git staging/commit/push, dependency mutation or retired-tool restoration ran.
