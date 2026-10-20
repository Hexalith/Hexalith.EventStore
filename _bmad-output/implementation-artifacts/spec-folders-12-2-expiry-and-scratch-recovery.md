---
title: 'Folders 12.2 prerequisite: source expiry and interrupted scratch recovery'
type: feature
created: '2026-10-20'
status: in-review
baseline_commit: 'd4d05f871991ec2af305267f59dcefeae3f127ed'
route: dispatch
review_loop_iteration: 1
context:
  - '/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/proposal-12-2-sdk-expiry-and-scratch-recovery.md'
  - 'docs/guides/named-projection-authoring.md'
  - 'docs/concepts/projection-delivery.md'
---

<frozen-after-approval reason="User approved both prerequisites on 2026-10-20">

## Intent

Make recoverable read-model batches preserve an immutable source expiry through delayed execution, and let interrupted private scratch writes resume or abort without regressing the epoch fence. Both capabilities are approved together as EventStore-owned prerequisites for Folders 12.2. Implement them here, then qualify consumer adoption separately in Folders.

## Boundaries & Constraints

Preserve existing v1 manifests/fingerprints and legacy relative-TTL behavior. Version new deadline-bearing protocol material explicitly if required by the frozen fingerprint contract; keep all supported decoders, anchors and identity guards coherent. Preserve CAS, tenant/owner/operation/generation fences, immutable recovery, safe reads and the 100-operation/one-MiB limits. Never overwrite a newer value when an older expiring candidate settles. Reuse SDK storage/recovery seams; no consumer-local protocol. Exclude the separately owned >10,000 identity-vector prerequisite, admission/performance redesign, source events, content cleanup, package/submodule pin changes, publication, deployment, Git mutations and public v2 exposure. Do not load any skill from a repository references directory.

## I/O & Edge-Case Matrix

| Scenario | Given / When | Then |
| --- | --- | --- |
| Source deadline | A staged immutable deadline, when committed/recovered before, at or after it | The deadline cannot refresh; expired candidates cannot be resurrected; completion/checkpoints remain coherent. |
| Compatibility | Existing non-expiring or relative-TTL batches, when fingerprinted, staged or recovered | Original manifests, fingerprints and behavior stay supported. |
| Shared generations | Deadline-bearing mutations, when journaled, streamed, inherited, promoted and caught up | The same deadline survives every conversion and selected reads remain truthful. |
| Scratch retry/abort | An interrupted original scratch batch, when the same action retries or Abort runs | Original work is safely reconciled; Abort can return to Open without publishing incomplete rows. |
| Fences/races | A newer epoch/value or foreign/stale operation, when old recovery or abort attempts overlap | No fence regression, lost delivery, cross-owner mutation or newer-value overwrite occurs. |
| Real persistence | Real Dapr/Redis and a replaced coordinator/process, when delayed recovery and scratch failure run | Persisted contents/TTL and epoch state demonstrate the same guarantees without skipped acceptance. |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Client/Projections/ReadModelBatchOperation.cs`, `SharedProjectionMutation.cs`: currently retain only relative TimeToLive; add the minimum immutable deadline API and preserve it through cloning/conversion.
- `ReadModelBatchFingerprint.cs`, `ReadModelBatchManifestCodec.cs`, `ReadModelBatchProtocol.cs`, `ReadModelBatchProtocol.PendingRecovery.cs`, `ReadModelBatchRecoveryAnchor.cs`, adjacent batch envelopes/store access: fingerprint, decode and apply expiry at execution and recovery without changing historical v1 bytes. Inspect transactions and selected reads as well as compaction.
- `SharedProjectionEpochCoordinator.cs:596` seals scratch and epoch state together in TryWriteScratchAsync. AbortAsync around line2204 persists Aborting then CatchUp can recover the sealed Building value; RequireOperation around line2261 fails. Reconcile the immutable pending batch before advancing a phase and retain CAS/race safety. `DomainSharedProjectionRebuildDispatcher.cs` must drive the real named path safely.
- `tests/Hexalith.EventStore.Client.Tests/Projections/`: existing fingerprint golden vectors, DaprReadModelBatchTests recovery suites and SharedProjectionEpochCoordinatorTests; use new single-type regression files for this change.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainFencedSharedProjectionIntegrationTests.cs` and partitioned rebuild tests: actual production dispatcher/session assertions.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/`: existing real daprd/Redis fixtures and read-model/shared projection tests. Assert persisted end-state, expiry and resumed/aborted epochs; use disposable fixture-owned state only.
- Consumer repros (ordinary files): Folders `tests/Hexalith.Folders.Tests/Projections/OperationalStatus/OperationalStatusRetentionTests.cs` and `OperationalStatusCapacityTests.cs`; diagnostics currently assert the broken behavior. Consumer changes and final live acceptance are a subsequent parent-owned phase.

## Tasks & Acceptance

- [x] Add the smallest compatible absolute-deadline operation/mutation API and identity-bound durable encoding; preserve existing non-expiring/relative-TTL golden vectors and legacy manifests.
- [x] Enforce original deadlines in installation, staging, compaction, selected reads, pending recovery and recovery anchors. Cover one tick before/inclusive boundary, delayed execution, repeated recovery, expired receipt/model consistency and concurrent newer rows.
- [x] Correct pending private scratch-batch reconciliation before retry or epoch-phase advancement. Keep completed old generations readable, incomplete new rows invisible, admitted ordinary catch-up retained and stale/foreign operations fenced. Prefer an internal correctness repair with no new public scratch protocol.
- [x] Add Client regressions for meaningful failure phases/races and DomainService tests through the actual shared dispatcher: failed Accumulate then retry/Abort, subsequent ordinary growth and a fresh rebuild, multiple owners and replaced coordinator instances.
- [x] Add and execute real Dapr-backed expiry and scratch-recovery qualification, asserting raw persisted end-state and retained TTLs. Record exact commands and blockers separately from focused evidence; do not turn an unavailable/skipped lane into acceptance.
- [x] Run affected Client/DomainService projects individually in Debug/source mode; run the narrow live classes and relevant existing regression suites. Record source/binary/result hashes in an additive evidence file under this repository. Update this spec task status truthfully and document public deadline usage in `docs/guides/named-projection-authoring.md`.

- [x] Complete the approved proposal's changed-OS-process owner-scratch qualification: persist a pending
  production named-dispatcher scratch batch in one exited worker process, then retry or Abort in a
  distinct worker over the same fixture sidecar/state. Record PIDs, exact commands, raw pending and
  cleaned state, selected rows, ordinary delivery, and a fresh rebuild with zero skipped acceptance.

## Review-one implementation corrections

The independent review found concrete defects in the pre-review implementation. They are implementation
requirements within the approved frozen intent, not new product scope. Re-derive the EventStore-owned
code from the restored baseline, positively reusing the preserved successful implementation at
`/tmp/story12-2-sdk-review1-keep-567hwhe8/approved-working-tree/references/Hexalith.EventStore` after
checking it against every correction below. Source/test/docs were restored to the original SDK baseline;
Git history/index were not changed. All prior planning/evidence remains current and must not be restored
from the snapshot. Do not edit Folders consumer files; the parent restores/qualifies its preserved changes
only after this EventStore phase finishes. Keep this spec in-progress until parent review.

- [x] Preserve the original source-compatible `Write(..., default)` and null/TimeSpan calls, original
  four-argument method signature and mutation constructor/Deconstruct. Use an unambiguous minimal
  deadline API. Add real compile-time compatibility calls, not only reflective signature checks.
- [x] Handle a logically expired but physically retained previous value consistently for CreateOnly in
  both supported component profiles. Keep physical CAS protection and immutable retained evidence.
  A stopped retained batch must recover/abort successfully without weakening guards for an unexpired,
  foreign, malformed or newer previous value. Test pending recovery and repeated settlement.
- [x] Sample remaining retention once per final persistence decision in ordinary compaction, transaction
  assembly and terminal recovery anchors. Crossing the inclusive deadline cannot pass nonpositive TTL
  to Dapr or throw after commit; remove only the original own envelope or omit a now-expired candidate.
  Use deterministic moving-clock/fault cases covering each path and preserve newer writers.
- [x] Preserve expiry evidence when the first copy-on-write pointer inherits an existing anchor, and
  when a private/aborted same-key replacement selects a previous pointer. Recheck the chosen inherited
  deadline when an awaited physical read returns absent across the boundary. Expired evidence returns
  truthful absence; unexplained pre-deadline missing values remain unavailable. Choose the minimum
  SDK-owned metadata seam and document its internal/public decision, preserving old public constructors
  and implementations. Do not inspect domain value schemas or add consumer-local storage protocols.
- [x] Make the compensator's own legacy recovery interruption resumable. Before removing either of the
  two fully validated original own envelopes, retain sufficient bounded immutable compensation evidence
  to finish after a crash/lost response or CAS race. Preserve all scope, fingerprint, operation, original
  unchanged Building fence, phase and newer-value guards. Do not synthesize an unavailable original v1
  manifest or accept arbitrary initially insufficient/committed legacy evidence. Record the owner's
  necessary internal storage decision here; no independent public scratch protocol is authorized.
- [x] Preserve ordinary historical non-expiring domain JSON containing `$hxrme`; deadline-bearing values
  with that domain property must not receive Completed and then become unreadable. Resolve collisions
  through SDK-owned encoding/decoding rather than silently reserving a new property in old domain
  values. Preserve the existing platform-reserved envelope namespace and fail-closed malformed genuine
  metadata. Cover old values, new deadline values, nested fields and malformed/duplicate SDK evidence.
- [x] Restore the decoder's null-on-invalid contract: validate numeric envelope versions and non-null
  fingerprint shape before typed access/StartsWith. Cover malformed discriminator JSON kinds, missing/
  duplicate fields, v2 null fingerprint/deadline and malformed candidate/previous data.
- [x] Preserve the existing 16,384-byte logical selected-owner model limit despite final expiry wrapping.
  Use a bounded physical wrapper allowance and then enforce the original decoded logical limit, while
  preserving receipt, corruption and admission guards. Supported near-limit writes must prove owner
  completion immediately; oversized/invalid evidence must remain bounded and fail closed.
- [x] Add the pre-verified missing cases: Abort/compensation of a previous expiring value must restore
  only its original remaining physical Redis TTL and then physically expire; abandoned same-key COW
  replacement must select the old value one tick before and return absence at its original deadline.
- [x] Qualify interrupted scratch retry and Abort over real Dapr/Redis through the production named
  DomainSharedProjectionRebuildDispatcher, with no test-authored pending recovery/private-delete repair
  calls. Assert private cleanup, Open/non-Building epoch, preserved selected generation, ordinary
  delivery after recovery and fresh named rebuild. Cover fresh and populated selections and actual
  retained interruption phases; component-only dispatcher tests and coordinator-only live tests do not
  substitute for this required production integration.

Requalify the final affected Client/DomainService suites and all relevant live classes individually in
Debug/source mode. Existing live infrastructure is available and no Folders topology remains active.
The parent independently reran the temporary reviewer harness and reproduced the CreateOnly recovery,
anchor expiry, discriminator, decoder, profile discrepancy, nonpositive-TTL, legacy interruption and
owner-proof size failures. That diagnostic is `/tmp/story12-2-approved-sdk-review-repro.log`; use real
repository regressions and final source/PDB/binary/XML evidence for acceptance. Preserve the old SDK
`qualification-20261020` packet unchanged and write a new `qualification-review1` packet with exact final
sources, commands, counts, physical state/TTL, replacement scope and disposable namespace cleanup.
Do not claim Redis TransactionQualified atomicity; its forced partial-write/refusal control remains
unsupported and production Redis remains Resumable. Preserve the two existing optional Client skips
as non-acceptance; new required live/dispatcher cases must execute with zero skips.

## Verification

Use `.slnx` only for solution build/restore and test projects individually. Local builds use `-c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; preserve dependencies and do not initialize nested submodules. Existing built xUnit runners accept `-class`/`-method` and `-result-xml`. Complete focused tests before broadening. If the full topology/build is blocked, record the exact command/output, then use available focused projects and real Dapr fixture lanes. Parent is establishing the required Aspire baseline before implementation dispatch; do not start a competing topology.

## Implementation Notes

Approval is the user's “I approve” response to the Folders proposal on 2026-10-20. Repository began clean on main; baseline HEAD is preserved above. No other developer work is included. Record choices, compatibility and exact validation below as implementation proceeds.

- Parent baseline: plain `aspire start --non-interactive --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json` built successfully but exited2 because its package-mode host lookup expected uninitialized nested Tenants. No submodule was initialized. `UseHexalithProjectReferences=true UseNuGetDeps=false aspire start --non-interactive --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json` succeeded. `aspire wait security-kuqbkukn --timeout45` and `aspire wait eventstore-fagujgzz --timeout45` (each with the same exact `--apphost` and `--non-interactive`; actual option syntax uses `--timeout 45`) confirmed healthy. All observed resources were Running/Healthy in `/tmp/story12-2-approved-sdk-aspire-baseline-ready.json`. Exact-target `aspire stop --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` succeeded before dispatch. Real Dapr/Redis/placement/scheduler infrastructure is already available.

## Historical pre-review Client phase handoff

This historical handoff describes the superseded pre-review API spelling and qualification, not the current implementation. Client source/tests and authoring docs were implemented. Exact reports: `/tmp/eventstore-expiry-client-build.log`, `/tmp/eventstore-expiry-new-client.log` and `.xml` (25 passed), `/tmp/eventstore-expiry-regressions.log` and `.xml` (290 passed); no failures/skips, Debug/source build zero warnings/errors. Public `Write(..., expiresAtUtc: deadline)` preserves the original retention overload; SharedProjectionMutation preserves its original constructor/Deconstruct. Use one clock for ReadModelBatchOptions.TimeProvider and the three-argument coordinator. Snapshot<T> unwraps/ages final values; SnapshotRaw returns physical bytes. The parent has not independently accepted these claims yet.

Continuation checklist from the Client handoff (implemented below):

- `src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildDispatcher.cs`: `DeleteScratchKeysAsync` must reconcile pending private keys through IReadModelPendingBatchRecoveryStore using the exact `FencedScope(handler, identity)` before bounded conditional cleanup. Failures after the scratch install but before the fence install can be discovered only from that key. Thread validated scopes through callers; do not infer a global/custom scope from tenant/projection strings. Cover direct Abort and same-action retry through actual dispatcher with interrupted Accumulate, subsequent ordinary growth and a fresh rebuild, preserving selected generations.
- Assess raw owner-proof/read admission seams for deadline-wrapped values. Supported unexpired selection models must still qualify; expired evidence must not create success. Reuse the SDK deadline decoder and bound clock, preserving every existing owner-proof, identity and corruption guard.
- Add and execute the planned real daprd/Redis qualification: physical TTL after delayed staging/compaction, no installation after the original deadline, replaced coordinator/store and interrupted scratch recovery. Assert raw persisted state and truthful selected reads. Existing fixture infrastructure is running and no Aspire topology is active. Keep fixture resources disposable, then confirm cleanup. Run necessary focused suites first; document any broad-gate blocker separately. Do not stop after component-only evidence if the available live lane can execute.
- Preserve old fully installed Prepared scratch compensation, legacy committed decisions and fail-closed insufficient evidence. Avoid new scope or weakening any proof to satisfy tests.
- Preserve the original baseline. Another workspace process committed in-progress files in `509dbe3733956a86c2ee2fddfd9c0d0006bbfea3`; this workflow did not perform that Git action. Record final HEAD plus actual file and binary hashes.

Finish this EventStore-owned phase before Folders consumption; do not edit the Folders consumer code. Complete the additive SDK evidence and mark only qualified tasks. Parent owns subsequent consumer adoption and independent review.


## Historical pre-review EventStore completion record

The EventStore-owned implementation and local qualification are ready for parent review. The additive
[qualification record](evidence/story-12-2-sdk-expiry-scratch/qualification-20261020/qualification.json)
retains exact commands, both source-qualified binary groups, 892 source/config/resource hashes, portable
PDB source checks, XML result hashes, initial failed diagnostics and cleanup evidence. The parent owns
independent review and subsequent Folders consumption; neither is claimed here.

- `DeleteScratchKeysAsync` now reconciles original private batches from each known scratch key with the
  handler's validated `FencedScope`, including global scopes, before bounded conditional cleanup. New
  actual-dispatcher tests cover six interruption phases, direct Abort, same-action retry, multiple owners,
  preserved selection, subsequent named ordinary growth and a fresh rebuild. Owner-sequenced named
  delivery remains backpressured while Building; it resumes after Abort rather than being falsely
  acknowledged. A delayed scratch writer cannot regress an Abort completed by another coordinator.
- Deadline-wrapped selected owner proof uses the SDK decoder and the configured clock before existing
  metadata/receipt guards. Both supported component profiles qualify unexpired proof and refuse expired
  or corrupt evidence. Historical v1 fingerprints and relative retention golden vectors remain passing.
- Debug/source builds succeeded. Final full Client: 1,700 passed, zero failed, two existing opt-in
  isolated-endpoint cases skipped. These skips are recorded and are not acceptance evidence. Final full
  DomainService: 1,005 passed, zero failed/skipped, with its existing `HEXALITH_TENANTS_DOMAIN_ROOT`
  override pointing to the already initialized root-declared Tenants source. The initial unpinned layout
  failure is preserved; no nested submodule was initialized, substituted or changed.
- The real Dapr/Redis suite passed all 23 cases, including nine new expiry/scratch/refusal cases, with
  zero skips. Raw remaining TTL was 3,986 ms after delayed installation; the model and dependent receipt
  physically disappeared while the checkpoint remained. Delayed commit, compaction and terminal-anchor
  recovery preserve the original source deadline. Replacement means new Dapr clients, stores and
  coordinators over real persisted Redis state; the new tests do not claim a replacement test-runner OS
  process or sidecar process during recovery. After the fixture sidecar stopped, cleanup removed only
  its exact AppId namespace (20,772 keys); the remaining count is zero.
- The forced transaction diagnostic exposed partial visible rows before Redis rejected the existing
  Prepared-marker update. Explicit legacy v1 and deadline v2 controls show the same provider behavior;
  the SDK returns Indeterminate and refuses completion. This is no atomicity, rollback or hidden-row
  safety claim. The qualified production-default Redis profile remains Resumable.
- Legacy pre-manifest scratch compensation remains deliberately bounded: both matching own envelopes
  and the unchanged Building fence must prove a complete Prepared/Aborting two-key batch. A committed
  legacy decision, missing manifest or insufficient/partially missing legacy evidence remains fail-closed
  for explicit owner disposition. No missing fingerprint material is synthesized.

The original baseline and the externally advanced EventStore/Folders HEADs and recorded root gitlink
are retained separately in the evidence. No dependency/package pins, Git history, submodules, retained
source events, content cleanup or consumer code were changed by this phase.

## Historical parent consumer qualification before independent review

Folders consumes absolute deadlines in its retained writer and preserves them through the actual
serialized partition/final-operation conversion. The latter conversion was exposed by a failed
real-provider retention assertion (rebuilt selected row TTL=-1), reproduced by the new partition
regression before the fix, then corrected without weakening the assertion. Final component runs pass
4,195 tests with zero failures and two existing optional Domain skips; the ten handler cases include
the delayed-partition regression. The Debug/source solution build has zero warnings/errors. The
real-provider acceptance rerun is underway; its final result is not claimed by this pre-review note.

The parent independently matched all 892 SDK source hashes, all 21 qualified SDK binary hashes and
the six retained SDK XML records. Subsequent Folders builds have zero drift across those 892 source
files; each of seven inspected consumer/runtime SDK Client portable PDBs contains all 270 current
Client source checksums. Distinct build-graph binary groups are retained rather than equated.

This review covers the approved two SDK capabilities from the original EventStore baseline above,
plus their new Folders consumption from `ec3523ada1b32362bf9f31c555b95c312b8df4b1`. It does not
claim completion/re-review of every historical Story 12.2 change. Whole-story acceptance remains
open for the separate >10,000 owner-identity prerequisite, package/PD10 qualification and observed
SM4 acknowledgement latency. The required Release/package gate exits 1 because EventStore 3.125.1
lacks the deadline API (CS1739/expiresAtUtc); its no-restore runtime fallback preserves the three
frozen PD10 artifact mismatches and passes all 36 client checks. No publication or pin change is
authorized. The qualification packet's original ready-for-review spec hash is preserved there; this
administrative review transition does not rewrite that historical packet.

## Review Triage Log

All three layers completed before triage. Every finding received an individual verdict before grouping;
verification gaps are accepted on their filed execution/search evidence. No finding is deferred.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| blind-1 | high | bad_spec | The parent reran the retained CreateOnly replacement harness: a valid original envelope over an expired physical wrapper returns invalid-recovery-evidence. Installation keeps previous bytes while the recovery guard requires null. |
| blind-2 | medium | bad_spec | The moving-clock harness reaches ArgumentOutOfRangeException after commit; compaction, transaction assembly and anchor cleanup separately sample the deadline check and TTL. |
| blind-3 | high | bad_spec | The anchor/private-replacement harness throws selected-value-missing at expiry. The first-pointer branch reads a decoded object and leaves priorDeadline null. |
| blind-4 | high | bad_spec | The parent reproduced compensation stopping after restoring its fence; retry returns recovery-manifest-unavailable. This is evidence destroyed by the new compensator itself, rather than initially insufficient legacy evidence. |
| blind-5 | medium | bad_spec | A valid domain object with a top-level $hxrme receives Completed then JsonException/Nested expiry evidence in the parent rerun; old non-expiring objects are also classified by that newly introduced property. |
| blind-6 | medium | patch | FromBytes newly calls GetInt32 before kind validation and StartsWith before null validation. The harness reproduces InvalidOperationException where the baseline decoder returned null; a direct type/null validation repair preserves its contract. |
| blind-7 | medium | bad_spec | The same expired-physical CreateOnly replacement completes under Resumable and returns transaction-dispatch Indeterminate under TransactionQualified in the parent harness. The transaction path retains empty create-only ETag. |
| blind-8 | high | bad_spec | The parent reproduced a 12,807-byte logical owner model expanding to 17,126 bytes; Execute reports Completed, then owner proof rejects it before decoding against the old 16,384-byte limit. |
| blind-9 | medium | bad_spec | The new TimeSpan?/DateTimeOffset four-argument overload pair makes a formerly valid fourth default literal ambiguous (CS0121); preserving only the binary method signature did not preserve source calls. |
| blind-10 | medium | bad_spec | The actual live scratch test manually recovers pending state and authors a delete batch after Abort. It does not execute the changed dispatcher cleanup; the approved proposal explicitly requires real production dispatcher qualification. |
| edge-1 | high | bad_spec | The same retained CreateOnly evidence mismatch is independently traced and reproduced as blind-1; verdict rendered before grouping. |
| edge-2 | medium | bad_spec | The transaction CreateOnly physical-CAS mismatch is independently traced and reproduced as blind-7; verdict rendered before grouping. |
| edge-3 | high | bad_spec | The compensator removes its fence envelope before scratch settlement and then cannot validate a retry; parent reproduction confirms the exact described interruption. |
| edge-4 | medium | bad_spec | The deadline check and remaining TTL are separate clock reads at the cited persistence paths; the moving-clock reproduction proves nonpositive provider retention is reachable. |
| edge-5 | medium | bad_spec | ReadGenerationAsync chooses an inherited deadline before awaiting its physical read, then throws unconditionally on null without rechecking that chosen deadline. Expiry during that await reaches the claimed unavailable result. |
| verification-gap-1 | medium | patch | Pre-verified gap: existing Abort tests seed non-expiring previous values and live expiry tests commit new candidates. No exercised restoration assertion would catch loss of the previous physical TTL. |
| verification-gap-2 | medium | patch | Pre-verified gap: inherited-expiry and abandoned-replacement tests do not combine same-key replacement with an expiring selected previous pointer. Omitting PreviousExpiresAtUtc survives those separate tests. |
| verification-other-1 | high | bad_spec | The parent reproduced valid retained CreateOnly recovery refusal; installed previous bytes contradict the unchanged create-only previous-null recovery guard. |

Root-cause groups: CreateOnly retained recovery (blind-1/edge-1/verification-other-1); remaining TTL
sampling (blind-2/edge-4); initial COW anchor metadata (blind-3); legacy compensation interruption
(blind-4/edge-3); domain discriminator collision (blind-5); decoder validation (blind-6); transaction
logical absence (blind-7/edge-2); owner-proof size (blind-8); overload ambiguity (blind-9); actual live
dispatcher coverage (blind-10); inherited-read await race (edge-5); previous-value physical restoration
coverage (verification-gap-1); abandoned COW expiry coverage (verification-gap-2). Bad-spec groups
trigger this implementation loop. The three patch entries survive as explicit corrections; lower work
is not independently patched on the invalidated pre-review implementation.

## Spec Change Log

- 2026-10-20, review loop 1: the eighteen independently graded findings above exposed gaps in the
  non-frozen implementation requirements. Added explicit logical-expiry/physical-CAS CreateOnly
  behavior, one-sample retention decisions, anchor/previous/awaited COW expiry metadata, resumable
  self-interruption of bounded legacy compensation, domain JSON collision compatibility, malformed
  envelope validation, original owner-proof logical size, unambiguous source-compatible API calls,
  physical previous-value restoration and real production-dispatcher live qualification. Known-bad
  states avoided are permanently pending valid batches, indefinite restored retention, legitimate
  expiry becoming unavailable, accepted unreadable values, proof rejection after completion and
  new recovery code destroying the evidence it needs to resume. Frozen intent/baseline is unchanged.
  KEEP every positive invariant and prior exact-source packet documented in
  `/tmp/story12-2-sdk-review1-keep-567hwhe8/README.md`. Scoped source/test/docs were preserved there
  before restoration to their owning baselines; no Git history, index, pins, dependency versions,
  external commits, planning/evidence or unrelated files were reverted. The parent owns Folders
  consumption after the re-derived EventStore implementation passes qualification.

## Review-one implementation decisions

- The deadline API is `ReadModelBatchOperation.WriteExpiring(key, value, concurrency, expiresAtUtc)`.
  The original four-argument `Write` and five-field mutation constructor/Deconstruct remain intact,
  including compile-time `default`, null and `TimeSpan` calls. The parent must adopt the distinct
  method in Folders after SDK qualification; consumer files remain untouched in this phase.
- Final expiry wrappers use version 3 of the existing platform-reserved `$hxrmb` namespace. The new
  `$hxrme` property is not reserved: historical domain JSON with that property, including nested fields,
  remains ordinary data. V1 batch manifests/fingerprints and relative retention bytes remain unchanged;
  deadline manifests/fingerprints use v2 and pending deadline envelopes use v2.
- Validated source-deadline evidence travels through internal init-only metadata on `RawStateEntry`
  and the existing `ReadModelEntry<T>` record. Public entry constructors, Deconstruct and store
  implementations stay source/binary compatible. This SDK-owned seam preserves initial COW anchor
  deadlines without inspecting domain payload schemas or adding a public inheritance protocol.
- Legacy scratch compensation retains the two fully validated original envelope byte arrays under
  the original batch's immutable `:scratch-compensation` key, referenced by its existing marker. The
  evidence uses the existing one-MiB bound; no missing original manifest/fingerprint material is
  synthesized. The marker enters Aborting before removal. Private scratch settles first; the own
  fence remains discoverable until a terminal Aborted decision and the final original-fence CAS.
  Consequently another coordinator cannot legitimately advance that fence while private work remains.
  Successful settlement reclaims the evidence; existing conditional private receipt cleanup also
  reconciles referenced participants and reclaims interrupted or unreferenced evidence before its
  terminal marker. Lost responses, CAS races and later Open fences are regression covered.
- Transaction CAS preflight finishes all SDK awaits before final synchronous TTL assembly. Ordinary
  compaction, transaction candidates and terminal anchors each use one sampled remaining retention
  for both the inclusive-expiry decision and provider TTL. Expired candidates are omitted/deleted only
  under their own CAS; later rows survive. Forced Redis TransactionQualified remains an unsupported
  partial-write/refusal diagnostic; production Redis stays Resumable.
- Owner proof admits a bounded 22,016-byte physical wrapper, decodes through the SDK, then enforces
  the original 16,384-byte logical model limit and all original identity/receipt/corruption guards.
  Final v3 wrappers encode their bytes as base64url to prevent default JSON escaping from multiplying
  physical size. Decoding also accepts existing standard-base64 v3 evidence. Raw physical byte counts
  remain truthful, and the production gateway retains its unchanged 32,768-byte admission guard.
  Component coverage includes escape-heavy near-limit and oversized values in both profiles; the live
  SDK proof passes through the production gateway for both ordinary and escape-heavy 16,384-byte rows.
- A coordinator waits for its own active promoted cleanup before foreground reclamation; the task
  table is temporary scheduling state only. A fresh coordinator still validates durable terminal
  receipt/CAS evidence and backpressures while another worker has a Committed cleanup batch. After
  that worker settles, retry succeeds without recreating a deleted receipt. Deterministic same/fresh
  coordinator overlap tests retain selected-generation readability and the strict terminal guard.

Final qualification is recorded additively under `evidence/story-12-2-sdk-expiry-scratch/qualification-review1`.
The `qualification-20261020` packet remains unchanged. Status stays in-progress for parent review.


## Review-one qualified SDK handoff

The current SDK phase is implemented and locally qualified. The additive
[qualification record](evidence/story-12-2-sdk-expiry-scratch/qualification-review1/qualification.json)
retains exact commands, logs/XML, distinct runtime binary/PDB groups, complete scoped source hashes,
PDB document checks, failed diagnostics, raw persistence observations and exact namespace cleanup.
The owner status remains in-progress; parent review and Folders adoption are still required.

- Debug/source builds have zero warnings/errors. Full Client: 1,736 passed, zero failed, two existing
  optional isolated-endpoint skips (not acceptance evidence). Full DomainService: 1,005 passed with no
  failures/skips, using its existing root-Tenants source override. Focused Client 195/195 and actual
  dispatcher component 10/10 also passed.
- Real Dapr/Redis qualification passed 36 outer cases with zero failures/skips: 20 expiry/scratch/owner
  proof, six existing batch, eight existing epoch and two pending owner-scratch OS-process cases. The
  four child worker invocations each passed 1/1 with zero skips. Persist-to-recovery PID pairs are
  2327730 to 2328206 and 2328699 to 2328831; each original worker exited before recovery. The controller
  observed original pending bytes after exit. Recovery used the production named dispatcher, followed
  by physical private cleanup, preserved selected state, ordinary delivery for two owners and a fresh
  named rebuild. The fixture sidecar/state remained shared; this is an owning-worker OS-process change,
  not a sidecar restart. The earlier instance-only cases remain separately identified.
- The retained failures were investigated, not classified as pre-existing flakes. A background promoted
  cleanup could overlap foreground reclamation and recreate a just-erased delete receipt; the same
  coordinator now drains its active work, while fresh coordinators rely on terminal/CAS checks and
  retryable backpressure. Two post-read outage fixtures now drain maintenance before arming their
  next-read fault, so background work cannot consume the intended foreground failure.
- Generic abandoned streamed-metadata cleanup remains bounded by the baseline terminal guard: a
  Committed receipt whose physical participants have disappeared is not reinterpreted as terminal or
  accepted through a new marker-only recovery protocol. The overlap test demonstrates retry after the
  original worker finishes. Approved interrupted owner-scratch recovery instead reuses the retained
  original immutable manifest/envelope evidence. Initially insufficient legacy/no-manifest or committed
  legacy evidence remains fail-closed; no unavailable original fingerprint material is synthesized.
- Redis production remains Resumable. Forced TransactionQualified is only partial-write/refusal
  evidence, not provider atomicity or rollback qualification. Package/release gates, publication,
  >10,000 identity vectors, Folders adoption and whole-story acceptance are not claimed.

## Parent qualification audit before independent review

The parent independently verified all 137 review-one evidence artifacts, all 1,512 recorded source
files and the current Client/Domain/live-process binary groups without mismatches. All six frozen
matrix rows are covered by 51 passing recorded cases in the parent matrix audit. The Folders source
adoption also passed 4,195 component cases and the full real-provider Aspire/Dapr acceptance (1/1,
zero skips, 295.344 seconds), with current SDK PDB sources matching all273 Client and179 Domain
documents in the consumed runtime graphs. Source and62 captured consumer assembly/PDB hashes
remained unchanged through the live run.

[The parent qualification packet](../../../../_bmad-output/implementation-artifacts/evidence/story-12-2-approved-sdk-source-qualification-20261020/qualification.json)
records those checks and the separate package/PD10/performance blockers. The sealed owner packet
retains its original in-progress spec snapshot; this status transition and subsequent triage are
planning-only changes after that seal. Owner baseline and frozen intent are unchanged.

### Review two — individual verdicts before grouping (2026-10-20)

All three context-free layers reported before triage. No finding has the same still-true
location/claim as a previous carried row; changed implementations and new claims were assessed anew.

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| R2-B1: Legacy same-action retry stays false after compensation (`SharedProjectionEpochCoordinator.cs:635`) | medium | Parent reran the binary-backed probe: four retries return false and scratch stays absent. New recovery-retaining fingerprint conflicts with the own legacy Aborted marker; supported fully validated legacy compensation must not strand the same action. |
| R2-B2: Interrupted cleanup rejects its own delete envelope (`DomainSharedProjectionRebuildDispatcher.cs:1327`) | high | Parent reran deterministic cleanup twice: the retry returns recovery-manifest-unavailable because the new preflight attempts original-owner recovery on the cleanup batch itself before its known deterministic batch can resume. |
| R2-B3: Expired skip breaks installed-prefix compensation (`ReadModelBatchProtocol.cs:518`) | medium | Parent reproduced an expired first write followed by occupied CreateOnly: Indeterminate/compensation-race despite no own unresolved writes. CompensateAsync requires an envelope for the skipped ordinal. |
| R2-B4: Legacy compensation accepts invalid selected generation (`ReadModelLegacyScratchRecovery.cs:54`) | medium | Parent reproduced ActiveGeneration=-1 through public RecoverPendingAsync: it reports resolved and removes private bytes. The partial legacy Building check omits complete epoch validation before destructive compensation. |
| R2-B5: Missing fixed v2 durable-identity golden vectors (`ReadModelBatchDeadlineTests.cs`) | medium | Current deadline test round-trips production encoder/decoder while only v1 has a fixed byte/fingerprint vector. A coordinated v2 serialization drift can silently change retained identity; a mixed-retention fixed vector is missing. |
| R2-B6: Missing delayed persisted-journal deadline recovery (`SharedProjectionExpiryAndScratchTests.cs`) | medium | Repository tests cover prepared journal restart and deadline mutation serialization separately. No case persists deadline-bearing prepared catch-up mutations, replaces the coordinator, crosses expiry and verifies physical absence plus coherent completion. |
| R2-B7: Missing physical Redis COW expiry coverage (`ProjectionExpiryAndScratchRecoveryLiveTests.cs`) | medium | The selected/inherited/abandoned COW deadline cases use a fake retaining physical bytes. Current live classes prove basic physical expiry but not that selected inherited/previous-pointer reads remain available after actual Redis removal. |
| R2-B8: Missing real-Dapr legacy compensation interruption coverage (`ReadModelLegacyScratchInterruptionTests.cs`) | medium | The new immutable legacy compensation row and transitions are tested only in recording/in-memory stores. Live named dispatcher cases use retained-manifest batches, leaving physical CAS/evidence cleanup after legacy-compensator interruption unqualified. |
| R2-B9: No near-limit proof through the real owner-proof HTTP endpoint (`ProjectionExpiryAndScratchRecoveryLiveTests.OwnerProof.cs:42`) | low | The near-limit case directly reads the real Dapr SDK proof then carries its actual result through the production gateway via fixed HTTP. Full endpoint routing/binding is unchanged and separately exercised by production HTTP harnesses. Remaining combination adds negligible confidence and requires more than a direct correction; reject under the low-finding rule. |
| R2-B10: Missing CreateOnly read-to-install newer-writer race (`ReadModelExpiryReviewRegressionTests.cs`) | medium | The expired-wrapper success cases and ordinary compaction races do not exercise a newer write after the physical expired-wrapper read but before installation/transaction dispatch. Both supported profiles need explicit persisted newer-byte preservation evidence for the changed CAS path. |
| R2-E1: Prepared scratch has no discoverable participant envelope (`SharedProjectionEpochCoordinator.cs:1581`) | high | Parent reran the actual dispatcher regression at BeforeInstallOperation ordinal0: Abort remains Accumulating/Indeterminate. MarkerPrepared is the same no-participant state; its original retained manifest exists, but key recovery and terminal reclamation never consume it. |
| R2-V1: Missing pre-expiry superseding-row compaction test (`ReadModelBatchProtocol.cs:646`) | medium | Pre-verified gap: the current race advances to the deadline, so IsCompacted accepts the newer row before the new pre-expiry preservation branch. Non-retained unexpired retry lacks its own persisted-newer-row assertion. |
| R2-V2: Missing promoted expiring pointer abandoned-replacement test (`SharedProjectionEpochCoordinator.cs:2968`) | medium | Pre-verified gap: current abandonment seeds a generation-zero anchor; streamed expiry writes an unrelated key. No test covers copying LatestExpiresAtUtc from an already promoted same-key pointer into PreviousExpiresAtUtc before Abort. |
