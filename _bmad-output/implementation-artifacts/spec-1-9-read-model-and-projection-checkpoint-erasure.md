---
title: 'Story 1.9: Read-Model And Projection Checkpoint Erasure'
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

**Problem:** Aggregate recreation can leave a higher projection-delivery checkpoint and stale read models behind, causing `ProjectionUpdateOrchestrator` to reject a new sequence-one stream as checkpoint drift and suppress projection delivery.

**Approach:** Add platform-owned, cancellation-aware erasure capabilities for ETag-protected, canonically addressed aggregate read-model values plus projection-scoped delivery and aggregate-specific rebuild checkpoints. Invoke the coordinated lifecycle operation only through the authenticated GlobalAdministrator REST boundary, serialize every delivery/rebuild/erase path for the same tenant/domain/aggregate/projection through a persisted DAPR projection-lifecycle actor, and prove against persisted state that completed erasure is tenant-safe, resumable, and permits sequence-one delivery for the same aggregate identity.

## Boundaries & Constraints

**Always:** Preserve ETag conflicts; treat an absent target as an idempotent success; validate the complete canonical target manifest and every required opt-in erasure capability before mutation; erase aggregate-owned read models first, the aggregate-specific rebuild checkpoint second, and the projection-scoped delivery checkpoint last; persist operation progress after each target; verify absence before reporting completion; distinguish denied, unsupported, active-rebuild, conflict, incomplete, canceled, and unknown outcomes from success; keep implementation in EventStore platform packages; use `ConfigureAwait(false)`; verify real persisted end state for tenant isolation and stale-checkpoint recovery.

**Block If:** A target was not created by the canonical aggregate-owned address factory; a target is legacy/opaque or represents a shared/index key; any required erasure capability is unavailable; a rebuild is active for the requested tenant/domain/projection; any delivery, poller, rebuild, or erasure entry path can bypass the projection-lifecycle actor; an atomic path is selected without an explicit backend-and-version allow-list entry backed by a persisted rollback probe; the authenticated boundary cannot prove GlobalAdministrator and tenant scope before target resolution or state access.

**Never:** Add members to the released `IReadModelStore` or `IProjectionCheckpointTracker` interfaces in this release; accept state-store names, keys, or ETags from the REST caller; erase legacy opaque keys or shared/index keys as if they were aggregate-owned; delete an operator-wide rebuild checkpoint or active-rebuild index; delete event streams, snapshots, broker history, backups, audit evidence, cryptographic keys, or shared index keys; modify root-declared submodules; catch an arbitrary transaction failure and assume no mutation occurred; select an atomic path from same-store routing or DAPR's `TRANSACTIONAL` capability string; describe the repository Redis path as atomic; use test-only key assertions as runtime authorization.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Matching single-key erase | Canonical aggregate-owned address, present key, and internally read current ETag | Key is absent and completion is reported | No error expected |
| Absent single-key erase | Canonical aggregate-owned address and missing key | Idempotent completion with no mutation | No error expected |
| Stale single-key erase | Present key and stale ETag | Value remains and conflict is reported | No exception for the conflict |
| Unsupported or non-canonical target | Legacy/opaque address, shared index, or missing capability | Validate the whole manifest and perform no mutation | Return `Unsupported` without revealing target state |
| Interrupted coordinated erase | Persisted operation record shows some model targets erased and checkpoints retained | No success; the same operation ID resumes from persisted progress and converges after transient failure | Distinguish conflict, incomplete, cancellation, and unknown outcome |
| Cross-tenant request | Authenticated scope and requested identity disagree | No target is resolved, read, or mutated | Return denial without revealing state |
| Active rebuild | Requested tenant/domain/projection has an active operator rebuild | No mutation | Return `ActiveRebuild`; caller may retry after the rebuild is terminal |
| Concurrent delivery and erase | Two replicas address the same tenant/domain/aggregate/projection | One projection-lifecycle actor turn owns the scope; delivery is deferred while erasure is active | No process-local lock is accepted as proof |
| Stale checkpoint recovery | Fresh identity has a seeded higher projection-scoped delivery checkpoint and first normal command produces sequence one | Completed erase removes the gate and delivery persists projection state | No event-stream reset/delete and no `CheckpointDriftDetected` suppression |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs` -- released read-model persistence contract; currently exposes read/save/conditional-save only.
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` -- DAPR implementation; `TryDeleteStateAsync` requires explicit first-write concurrency and does not itself define portable absent-key semantics.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` -- deterministic ETag fake; conditional removal must not delete a newer concurrent value.
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs` -- aggregate-wide delivery checkpoint contract; currently has no ETag-bearing read or erase method.
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs` -- owns `projection-checkpoints:{ActorId}` persistence.
- `src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs` and `ProjectionRebuildCheckpointStore.cs` -- already own tenant/domain/projection/aggregate-scoped rebuild progress; erasure is limited to the requested aggregate-specific row and must reject an active rebuild.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` -- reads the delivery checkpoint and suppresses delivery when it exceeds the stream maximum.
- `src/Hexalith.EventStore.Server/Actors/` -- home for the persisted projection-lifecycle actor that serializes delivery, rebuild, and erasure across replicas for one tenant/domain/aggregate/projection scope.
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- server registration surface; currently does not register `IReadModelStore` or an erasure coordinator.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs` and the Gateway/Server admin invocation seam -- authenticated public surface; erasure requires the Admin policy, tenant authorization, and an explicit EventStore GlobalAdministrator check before target resolution.
- `src/Hexalith.EventStore.DomainService/` -- canonical SDK mapping for a registered projection target factory; domain modules declare logical aggregate-owned slots but do not receive raw DAPR lifecycle plumbing.
- `tests/Hexalith.EventStore.IntegrationTests/` -- required real-state-store proof surface for Redis/DAPR behavior and sequence-one delivery.

## Tasks & Acceptance

**Execution:**
- Preserve `IReadModelStore` and `IProjectionCheckpointTracker` unchanged. Add an opt-in `IReadModelConditionalEraser` capability implemented by `DaprReadModelStore` and `InMemoryReadModelStore`; keep delivery/rebuild checkpoint erase capabilities internal to Server unless an external consumer is proven. Resolve every required capability before the first mutation and return `Unsupported` when any implementation has not opted in.
- Add a platform-owned `ProjectionReadModelAddress` and factory. The factory accepts a validated `AggregateIdentity`, validated projection name, and registered logical slot; it resolves the configured store and emits a canonical key containing tenant, domain, projection, aggregate, and slot. Only addresses produced by this factory are accepted. The same address must be used for writes and erasure. Legacy opaque keys are denied until migrated/rebuilt, and shared/index slots are excluded from whole-key erasure.
- Migrate delivery checkpoint addressing from aggregate-wide `projection-checkpoints:{ActorId}` to projection-scoped `projection-checkpoints:{ActorId}:{projectionName}`. Before projection-scoped delivery or erasure is enabled for an identity, a one-time aggregate-scoped DAPR migration actor must resolve every registered projection, conditionally copy the legacy high-water mark to each projection-specific key, verify every copy, and only then conditionally delete the legacy key and persist a completed migration marker. Conflict, cancellation, or unknown outcome leaves projection delivery/erasure deferred and the migration retryable. After the marker is complete, delivery reads only projection-specific keys and coordinated erasure deletes only the requested projection's key; there is no indefinite dual-read fallback and no projection-scoped request silently deletes the aggregate-wide key.
- Erase the aggregate-specific rebuild checkpoint at `(tenant, domain, projectionName, aggregateId)` after read-model targets and before the delivery checkpoint. Never erase the `AggregateId = null` operator-scope row or active indexes. Reject the whole request before mutation when the target projection has an active rebuild.
- Add a persisted DAPR `ProjectionLifecycleActor`, addressed by tenant/domain/aggregate/projection. Route immediate delivery, polling delivery, rebuild delivery, and erasure through this actor. A process-local semaphore is insufficient. The actor persists `{operationId, scope, targetManifestDigest, phase, perTargetOutcomes}` before mutation, defers delivery while phase is erasing, records progress after every target, rejects a different active operation, and resumes the same operation ID after crash/retry.
- For each target, read its ETag internally and conditionally erase it. Absent is complete; changed ETag is conflict and must not delete the newer value; after an ambiguous transport failure, read back the target and classify absent as complete, same ETag as retryable incomplete, changed ETag as conflict, and unverifiable state as unknown. Verify all targets absent before marking the actor operation completed and releasing queued delivery.
- Treat every currently supported production backend, including the repository Redis/DAPR store, as resumable-only. Do not call `ExecuteStateTransactionAsync` for this operation on Redis. A future atomic adapter is opt-in only through an explicit backend-component-and-version policy entry whose persisted failure probe proves rollback of conditional multi-delete; default is false. In-memory may use one lock internally but must expose the same structured outcomes.
- Add the authenticated REST operation under the existing Admin projection surface. The request contains tenant, domain, aggregate, projection, logical slot IDs, and a stable operation ID only. Require the Admin authorization policy, tenant authorization filter, forwarded application credentials, and an explicit EventStore GlobalAdministrator decision before resolving the target factory or touching state. Ordinary Operator callers are denied.
- Register the coordinator, actor, target factory, address/configuration policy, erasure capabilities, and admin invocation seam through the platform DI extensions. Domain modules may register logical projection slots but must not implement raw DAPR erasure plumbing.
- `tests/Hexalith.EventStore.Client.Tests/Projections/` and `tests/Hexalith.EventStore.Server.Tests/Projections/` -- cover additive interface compatibility, canonical address validation, shared/legacy denial before mutation, conditional erase, actor serialization, resumable progress, structured outcomes, cancellation, duplicate operation IDs, checkpoint migration/scope, active-rebuild refusal, and DI resolution.
- `tests/Hexalith.EventStore.IntegrationTests/` -- prove the Redis path is resumable rather than claimed atomic, persisted tenant isolation through the authenticated production endpoint, cross-replica actor exclusion, delivery/rebuild checkpoint scope, and stale-checkpoint recovery without deleting event state.

**Acceptance Criteria:**
- Given a present read-model key and its current ETag, when conditional erasure completes, then the key is absent; given a stale ETag, when erasure is attempted, then the value remains and conflict is returned.
- Given existing third-party implementations of released `IReadModelStore` or `IProjectionCheckpointTracker`, when this release is consumed, then those interfaces have no new members and the implementations remain source/binary compatible; coordinated erasure returns `Unsupported` before mutation unless the required additive capabilities are registered.
- Given an absent read-model, delivery-checkpoint, or aggregate-specific rebuild-checkpoint key, when erasure is retried, then the operation treats that target as complete without side effects.
- Given a request containing raw/legacy/shared targets or a canonical target outside the authenticated tenant/domain/aggregate/projection scope, when validation runs, then no target state is resolved, read, disclosed, or mutated.
- Given an authorized canonical target manifest and no active rebuild, when coordinated erasure reports success, then every aggregate-owned read-model target, the requested projection's aggregate-specific rebuild checkpoint, and its projection-scoped delivery checkpoint are durably absent; the operator-scope rebuild row, active indexes, other projections, and other tenants are unchanged.
- Given a failure, cancellation, conflict, or ambiguous backend outcome after any mutation, when the coordinator returns, then it does not report success and exposes a retry-safe structured outcome.
- Given the same stable operation ID after interruption, when erasure is retried, then the actor resumes its persisted manifest/progress and converges; given a different operation ID while erasure is active, then it conflicts without mutation.
- Given concurrent projection delivery/rebuild and erasure requests on different replicas, when they target the same tenant/domain/aggregate/projection, then the persisted projection-lifecycle actor serializes them and no read model or checkpoint can be recreated during completed erasure.
- Given the repository Redis/DAPR backend, when coordinated erasure executes, then it uses the persisted resumable protocol and never claims transaction rollback or atomic completion from same-store placement or the `TRANSACTIONAL` capability string.
- Given tenant A and tenant B persisted state, when tenant A erasure is requested, then ownership is validated before mutation and tenant B state remains unchanged and undisclosed.
- Given a fresh unique identity with a directly seeded stale projection-scoped delivery checkpoint and canonical read model, when the authenticated production erasure completes and its first normal command is submitted, then the event stream's first persisted event is sequence one and projection state is persisted without checkpoint-drift suppression; the proof never resets or deletes an event stream.
- Given domain-module authoring guardrails, when the feature is built and tested, then no raw DAPR state access or lifecycle plumbing is introduced into a domain module.

## Spec Change Log

- 2026-07-11 -- Human resolution selected the full approved lifecycle contract: additive capability interfaces, canonical platform-derived aggregate targets, projection-scoped delivery plus aggregate-specific rebuild cleanup, GlobalAdministrator REST invocation, persisted actor serialization/resume, resumable-only Redis behavior, and fresh-identity persisted proof without event deletion.

## Review Triage Log

## Design Notes

Local `Dapr.Client` 1.18.4 exposes `TryDeleteStateAsync` and `ExecuteStateTransactionAsync`, but the transaction call returns no per-operation result. More importantly, a persisted probe against the repository's Redis/DAPR path produced a partial two-delete commit when one ETag conflicted; Redis advertised `TRANSACTIONAL` while warning that rollback is unsupported. Same-store routing or the capability string therefore cannot select an atomic path safely.

Caller-owned keys are format-agnostic. Existing examples include aggregate keys without a tenant prefix and a shared singleton index, so `(AggregateIdentity, storeName, key, etag)` cannot prove ownership or safely erase shared entries. The approved contract must introduce an enforceable ownership envelope/factory or move target derivation behind a trusted platform seam.

The selected ownership contract is the canonical typed-address factory. External callers provide only scope, logical slot IDs, and operation ID. A logical slot is registered as aggregate-owned or shared; the factory refuses shared slots and produces canonical aggregate-owned addresses that bind every `AggregateIdentity` component plus projection name and slot. Legacy opaque keys are not grandfathered into coordinated erasure.

The selected checkpoint contract follows the approved tenant/domain/aggregate/projection scope. Delivery checkpoints migrate to projection-specific keys. Story 1.9 also erases the existing aggregate-specific rebuild-progress row for that projection, but refuses active rebuilds and never deletes operator-scope lifecycle or index state.

The selected concurrency contract is a persisted DAPR projection-lifecycle actor, not the current process-local `KeyedSemaphore` and not an unfenced lease. Every immediate, polling, rebuild, and erasure path for the scope must execute through the same actor turn. The operation record and per-target progress are durable resume evidence; completion requires read-back verification.

The selected caller is the existing authenticated Admin REST surface with Admin policy plus tenant filtering and an explicit EventStore GlobalAdministrator check. The public request never carries physical storage coordinates. The selected persisted proof seeds stale projection state for a fresh identity, erases it through that production boundary, then creates the first real event normally; test-only event-stream deletion/reset is forbidden.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx` -- expected: dependencies restore in the repository's selected package-reference mode.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` -- expected: no new warnings or errors.
- `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class Hexalith.EventStore.Client.Tests.Projections.DaprReadModelStoreTests` -- expected: conditional DAPR erase contract passes.
- `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class Hexalith.EventStore.Client.Tests.Projections.InMemoryReadModelStoreTests` -- expected: fake parity, concurrency, cancellation, and retry tests pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` -- expected: focused erasure/checkpoint/orchestrator classes pass; use built xUnit v3 assembly class filters for triage.
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj` -- expected: GlobalAdministrator/tenant authorization, safe request shape, denial-before-resolution, and endpoint result mapping pass.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj` -- expected: unmodified domain authoring guardrails pass.
- `dotnet test tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj` -- expected: persisted Redis/DAPR erasure and tenant-isolation lane passes.
- `git diff --check` -- expected: no whitespace errors.

## Auto Run Result

Status: blocked

Blocking condition: intent gap

Resolved decisions (human-selected option 1):

- Every currently supported production backend is resumable-only; Redis never selects an atomic path. Future atomic behavior is opt-in only for an explicitly allow-listed backend/component/version with a persisted rollback proof.
- Only canonical `ProjectionReadModelAddress` targets derived from aggregate identity, projection, and registered aggregate-owned slot are erasable. REST callers cannot provide physical keys, stores, or ETags; legacy opaque and shared/index keys are denied.
- Delivery checkpoints become projection-specific through the mandatory verified migration above. This story also erases the requested projection's aggregate-specific rebuild checkpoint, while active rebuilds, operator-scope rows, and indexes are protected.
- The authenticated Admin REST surface requires tenant authorization and explicit GlobalAdministrator. A persisted DAPR projection-lifecycle actor serializes every immediate, polling, rebuild, and erasure path across replicas and durably resumes partial work.
- Released `IReadModelStore` and `IProjectionCheckpointTracker` remain unchanged; erasure uses additive opt-in capabilities and fails `Unsupported` before mutation when a capability is absent.
- Persisted recovery proof seeds stale projection state for a fresh unique identity, invokes the production erasure endpoint, and then creates its first event normally at sequence one. Event-stream reset/delete is forbidden.

Evidence gathered:

- `Dapr.Client` 1.18.4 exposes conditional delete and same-store transaction APIs, but transaction execution returns no per-operation result.
- A persisted Redis/DAPR probe observed one delete commit while a sibling ETag-conflicted delete remained; Redis warned that transaction rollback is unsupported.
- Existing read-model keys are caller-defined and include non-tenant-prefixed keys plus a shared singleton index, so `(AggregateIdentity, storeName, key, etag)` is not an enforceable ownership boundary.
- `ProjectionCheckpointTracker` keys delivery state by `AggregateIdentity.ActorId` without a projection identifier, exposes no ETag-bearing read, and `AddEventStoreServer` currently provides no erasure invocation surface.
- `ProjectionUpdateOrchestrator` still reads this checkpoint and suppresses delivery on drift, so the underlying defect is real even though the safe contract remains unresolved.
