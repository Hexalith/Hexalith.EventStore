---
title: 'Story 1.10: Coordinated Read-Model Batch Writes'
type: 'feature'
created: '2026-07-11T00:00:00+02:00'
status: 'blocked'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
warnings:
  - 'oversized'
---

<intent-contract>

## Intent

**Problem:** Projection authors can only persist detail and index models through independent single-key calls, so partial failure can expose an inconsistent logical projection and a retry can reapply an already durable prefix.

**Approach:** Add an opt-in asynchronous same-store batch seam with immutable typed write/delete operations, stable identity and fingerprinting, explicit backend profiles, marker-gated resumable visibility, bounded reconciliation, and equivalent DAPR/in-memory outcomes while preserving `IReadModelStore` compatibility.

## Boundaries & Constraints

**Always:** Scope identity by store, tenant, domain, aggregate, projection type, and caller-supplied ULID batch id; fingerprint the versioned canonical ordered manifest with SHA-256; reject invalid/duplicate/mixed-store/oversized input before state access; distinguish `Completed`, `AlreadyCompleted`, `Conflict`, `Incomplete`, and `Indeterminate`; use DAPR JSON options; verify durable keys and receipt before success; preserve previous complete values until a resumable commit marker is durable; reconcile after dispatched cancellation with an internal bounded token; retain terminal receipts indefinitely; keep checkpoints unchanged; use `ConfigureAwait(false)`, XML-documented public APIs, and one C# type per file.

**Block If:** Live or deterministic evidence disproves marker-gated old-view visibility, retry convergence, conditional-write semantics, or same-identity reconciliation for the selected profile; return the spec to `blocked` rather than weakening these guarantees.

**Never:** Add members to `IReadModelStore`; claim cross-store atomicity; qualify a backend from DAPR metadata or a void transaction response; default repository Redis to transactional; switch profile or identity after ambiguity; report a durable prefix, deferred cleanup, or canceled flush as unproven success; advance delivery/rebuild checkpoints; modify production projection dispatch, AppHost/YAML, release inventory, or root-declared submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| New batch | Ordered heterogeneous same-store operations | Required values/deletes and terminal receipt are verified before `Completed` | Return structured conflict/incomplete/indeterminate outcome |
| Completed retry | Same identity and fingerprint | `AlreadyCompleted` without reapplying operations | No error expected |
| Identity reuse | Same identity, different fingerprint | No new logical mutation | Return identity `Conflict` |
| Resumable interruption | Pending envelopes and prepared/aborting marker | Reads expose old complete values; same identity resumes or aborts/compensates | Remain `Incomplete` until reconciliation proves a terminal state |
| Ambiguous dispatch | Transaction or state request may be durable | Re-read marker and all operation keys using an independent bounded token | Return proven terminal outcome or `Indeterminate` |
| Invalid manifest | Empty, duplicate key, mixed store, invalid ETag/identity, or configured limit exceeded | No state access | Throw deterministic validation exception |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs` -- released single-key contract; signatures and legacy behavior must remain compatible.
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` -- production adapter requiring raw-byte envelope decoding, both execution profiles, reconciliation, verification, and cleanup.
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs` -- DI root that must expose one default concrete singleton through both store interfaces without defeating consumer overrides.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` -- JSON-round-tripping deterministic fake requiring durable protocol state and operation-boundary failure hooks.
- `tests/Hexalith.EventStore.Client.Tests/Projections/RecordingDaprClient.cs` -- request-shape/stateful reconciliation test seam, not persisted-backend proof.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs` -- real DAPR/Redis fixture for authoritative end-state and qualification evidence.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- prevents domain-owned raw DAPR/store plumbing.

## Tasks & Acceptance

**Execution:**
- `src/Hexalith.EventStore.Client/Projections/{IReadModelBatchStore,ReadModelBatch,ReadModelBatchScope,ReadModelBatchOperation,ReadModelBatchConcurrency,ReadModelBatchResult,ReadModelBatchStatus,ReadModelBatchStoreProfile,ReadModelBatchOptions}.cs` -- add immutable one-type-per-file public contracts and typed write/delete factories; validate a maximum of 100 operations, 1,048,576 canonical bytes, 512 UTF-8 key bytes, scope components, explicit concurrency/delete modes, and caller-supplied ULID identity before dispatch.
- `src/Hexalith.EventStore.Client/Projections/` -- add one-type-per-file internal v1 canonical serializer/fingerprint, opaque scope-hashed marker key, marker/receipt state machine, pending envelope, reconciliation, and source-generated safe logging helpers; include ordinal/key/kind/type/canonical payload/concurrency/ETag in the SHA-256 fingerprint and never log raw keys, tenant data, payloads, or ETags.
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` -- implement `IReadModelBatchStore`; decode legacy raw JSON and versioned envelopes through `GetByteStateAndETagAsync`; default stores to `Resumable`; install and verify pending envelopes before marker commit, expose old values before commit, compensate conflicts, compact after commit, and retain the terminal receipt; for explicitly `TransactionQualified` stores prepare identity then issue exactly one ordered transaction containing logical operations plus completion evidence and verify all durable state afterward; reconcile ambiguous dispatch without changing identity/profile.
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs` -- validate and register batch options/profiles and alias the same default `DaprReadModelStore` singleton as `IReadModelStore` and `IReadModelBatchStore`, retaining `TryAdd` idempotence and deliberate consumer overrides.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` -- implement both interfaces with production-equivalent marker/envelope visibility, ETags, receipts, compaction, and deterministic hooks before dispatch, around each operation, around commit, during abort/cleanup, and after dispatched cancellation; preserve legacy JSON cloning and snapshot helpers.
- `tests/Hexalith.EventStore.Client.Tests/Projections/{ReadModelBatchStoreTests,ReadModelBatchFingerprintTests,DaprReadModelStoreTests,InMemoryReadModelStoreTests,RecordingDaprClient}.cs` -- cover every matrix row, validation boundary, golden v1 fingerprint, canonical heterogeneous serialization, order, both concurrency modes, legacy reads, old-view visibility, abort/compensation, retry convergence, cancellation timing, ambiguity, compaction, receipt retention, and exact transaction bytes/options; compare DAPR and fake observable results.
- `tests/Hexalith.EventStore.Client.Tests/Registration/ReadModelAndCursorRegistrationTests.cs` -- prove default reference equality, repeated registration, profile/options validation, and custom single/batch-store override behavior.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs` -- inspect Redis/DAPR detail, index, envelope/marker/receipt, cleanup, and seeded delivery/rebuild checkpoint keys for success, ETag conflict, duplicate/conflicting identity, partial-prefix recovery, cancellation, and compaction; keep Redis `Resumable` and add a separate opt-in conditional transaction qualification probe that fails closed on partial commit.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` and `docs/brownfield/development-guide.md` -- recognize the approved batch seam, forbid domain raw byte/transaction/marker plumbing, and document immediate execution, stable identity, qualification, cancellation reconciliation, indefinite receipts, and checkpoint ownership.

**Acceptance Criteria:**
- Given any operation manifest and outcome from the matrix, when deterministic Client/Testing tests run against the DAPR adapter and fake, then both expose the specified status and logical read visibility while existing single-key tests remain green.
- Given an explicitly qualified backend, when execution completes, then exactly one ordered DAPR transaction contains the logical operations and completion evidence, and success follows read-back proof rather than the void response.
- Given the repository Redis profile, when a failure is injected between detail and index persistence, then `IReadModelStore` readers see the previous complete pair until marker commit and same-identity retry converges without independently retrying keys.
- Given any completed response, when live Redis is inspected, then detail/index/deletion intent and the retained receipt match the canonical manifest, cleanup is logically transparent, and representative delivery/rebuild checkpoints are unchanged.
- Given existing consumers and domain guardrails, when Release build and focused lanes run, then `IReadModelStore` source compatibility, DI override semantics, legacy stored JSON, and platform ownership remain intact.

## Spec Change Log

## Review Triage Log

## Design Notes

The resumable protocol uses a marker as the visibility decision: pending envelopes retain old and candidate representations; old values are returned in prepared/aborting states, candidates in committed state, and mixed committed-envelope/compacted state reads identically. A pre-commit conflict transitions through abort and restores the old view using freshly read ETags. Cleanup may be retried after proven completion, but the compact terminal receipt is never expired in this story. Transaction qualification is an operator-owned semantic promise backed by a live conditional-write probe; capability metadata is diagnostic only.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx` -- expected: restore succeeds.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class Hexalith.EventStore.Client.Tests.Projections.ReadModelBatchStoreTests` -- expected: batch contract/protocol tests pass.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --configuration Release` -- expected: testing fake suite passes.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Release` -- expected: authoring guardrails pass.
- `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ReadModelBatchLiveSidecarTests` -- expected: live persisted-state and checkpoint-isolation scenarios pass.
- `git diff --check` -- expected: no whitespace errors.

## Auto Run Result

Status: blocked
Blocking condition: intent gap

The following decisions remain observably ambiguous after inspecting the resolved story and current code:

- Custom DI overrides: choose whether a pre-registered `IReadModelStore` must also implement `IReadModelBatchStore` (otherwise batch resolution is absent/fails) or whether the default DAPR batch service coexists with the custom single-key store (breaking the promised reference equality).
- Options contract: freeze the store-profile lookup shape, unknown-store behavior, validation timing, and default bounded reconciliation timeout.
- Delete concurrency: name and define the allowed delete policies, including whether idempotent-absent delete is public and how it differs from expected-ETag delete.
- Result contract: freeze the public payload for identity versus optimistic conflicts, cleanup-pending completion, recovery reason, and durable receipt/fingerprint evidence; the status enum alone cannot express these outcomes without lossy interpretation.
- Batch-id validation: choose strict ULID parsing or the broader existing EventStore message-identity rule; these accept different caller inputs.
- Live interruption proof: specify the sanctioned real-DAPR mechanism for forcing failure between resumable logical operations without weakening production encapsulation or substituting deterministic fake evidence.
- Transaction qualification: specify how a passing opt-in probe is recorded and how that evidence enables `TransactionQualified` configuration for the exact runtime/component/backend combination.

Evidence: `IReadModelStore` has four single-key members; `DaprReadModelStore` and `InMemoryReadModelStore` have no batch/profile/result model; current DI registers only `TryAddSingleton<IReadModelStore, DaprReadModelStore>()`; the live fixture lacks generic batch-key failure injection/inspection; and the ready story requires both custom override preservation and default reference equality without selecting behavior when a custom store is not batch-capable.
