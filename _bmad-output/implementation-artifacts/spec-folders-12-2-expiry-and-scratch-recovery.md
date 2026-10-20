---
title: 'Folders 12.2 prerequisite: source expiry and interrupted scratch recovery'
type: feature
created: '2026-10-20'
status: in-progress
baseline_commit: 'd4d05f871991ec2af305267f59dcefeae3f127ed'
route: dispatch
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

- [ ] Add the smallest compatible absolute-deadline operation/mutation API and identity-bound durable encoding; preserve existing non-expiring/relative-TTL golden vectors and legacy manifests.
- [ ] Enforce original deadlines in installation, staging, compaction, selected reads, pending recovery and recovery anchors. Cover one tick before/inclusive boundary, delayed execution, repeated recovery, expired receipt/model consistency and concurrent newer rows.
- [ ] Correct pending private scratch-batch reconciliation before retry or epoch-phase advancement. Keep completed old generations readable, incomplete new rows invisible, admitted ordinary catch-up retained and stale/foreign operations fenced. Prefer an internal correctness repair with no new public scratch protocol.
- [ ] Add Client regressions for meaningful failure phases/races and DomainService tests through the actual shared dispatcher: failed Accumulate then retry/Abort, subsequent ordinary growth and a fresh rebuild, multiple owners and replaced coordinator instances.
- [ ] Add and execute real Dapr-backed expiry and scratch-recovery qualification, asserting raw persisted end-state and retained TTLs. Record exact commands and blockers separately from focused evidence; do not turn an unavailable/skipped lane into acceptance.
- [ ] Run affected Client/DomainService projects individually in Debug/source mode; run the narrow live classes and relevant existing regression suites. Record source/binary/result hashes in an additive evidence file under this repository. Update this spec task status truthfully and document public deadline usage in `docs/guides/named-projection-authoring.md`.

## Verification

Use `.slnx` only for solution build/restore and test projects individually. Local builds use `-c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; preserve dependencies and do not initialize nested submodules. Existing built xUnit runners accept `-class`/`-method` and `-result-xml`. Complete focused tests before broadening. If the full topology/build is blocked, record the exact command/output, then use available focused projects and real Dapr fixture lanes. Parent is establishing the required Aspire baseline before implementation dispatch; do not start a competing topology.

## Implementation Notes

Approval is the user's “I approve” response to the Folders proposal on 2026-10-20. Repository began clean on main; baseline HEAD is preserved above. No other developer work is included. Record choices, compatibility and exact validation below as implementation proceeds.

- Parent baseline: plain `aspire start --non-interactive --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json` built successfully but exited2 because its package-mode host lookup expected uninitialized nested Tenants. No submodule was initialized. `UseHexalithProjectReferences=true UseNuGetDeps=false aspire start --non-interactive --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json` succeeded. `aspire wait security-kuqbkukn --timeout45` and `aspire wait eventstore-fagujgzz --timeout45` (each with the same exact `--apphost` and `--non-interactive`; actual option syntax uses `--timeout 45`) confirmed healthy. All observed resources were Running/Healthy in `/tmp/story12-2-approved-sdk-aspire-baseline-ready.json`. Exact-target `aspire stop --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` succeeded before dispatch. Real Dapr/Redis/placement/scheduler infrastructure is already available.
