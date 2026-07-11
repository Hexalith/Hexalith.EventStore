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

**Problem:** Projection authors can persist a detail model and its index only through independent single-key calls, so failures can expose inconsistent state and retries can apply a logical delivery more than once.

**Approach:** Add an asynchronous, additive, same-store batch capability for heterogeneous typed writes and deletes, with stable batch identity, optimistic-concurrency inputs, structured outcomes, deterministic recovery, and equivalent observable DAPR and in-memory behavior. Preserve every existing single-key API.

## Boundaries & Constraints

**Always:** Keep each batch within one configured state-store component; preserve operation order and per-operation key, write/delete intent, and concurrency policy; bind a stable batch identity to a canonical operation fingerprint; return idempotent success only for a completed matching batch and predictable conflict for identity reuse with a different fingerprint; distinguish success, conflict, incomplete, and indeterminate outcomes; treat cancellation after dispatch as potentially committed; verify persisted detail, index, marker, and any in-scope checkpoint before success; use DAPR-compatible JSON serialization; keep DAPR and in-memory semantics equivalent; use `ConfigureAwait(false)` and one C# type per file.

**Block If:** Transaction eligibility and fallback policy are not selected; the allowed visibility of partial resumable progress is not selected; the durable unit's checkpoint ownership is not selected; the flush/cancellation result contract is not selected; or batch marker scope, retention, and cleanup policy are not selected.

**Never:** Add required members to `IReadModelStore`; claim cross-store atomicity; interpret DAPR's `TRANSACTIONAL` capability or a void transaction response as rollback/completion proof; retry an ambiguously completed transaction under a different identity; report deferred flush, cancellation, or a durable prefix as success; integrate the synchronous projection handler or production idempotency/checkpoint dispatcher work owned by Stories 1.12-1.13 without an explicit scope decision; modify root-declared submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First matching batch | Unique identity and ordered same-store writes/deletes | All required state and completion evidence are durably accepted before success | Conflict/incomplete/indeterminate is structured, never collapsed to success |
| Completed retry | Same identity and canonical fingerprint | Idempotent success without applying operations twice | No error expected |
| Identity reuse | Same identity with different fingerprint | No new operation is applied | Predictable identity conflict |
| Optimistic conflict | Any expected ETag does not match | Never reports success; preserves truthful persisted evidence | Structured conflict with retry-safe state |
| Interrupted resumable execution | Durable prefix plus incomplete marker | Same identity resumes or safely compensates according to the selected visibility model | Incomplete until every required state is verified |
| Ambiguous transaction/cancellation | Request may have reached DAPR | Read durable evidence before classification | Indeterminate unless completion or conflict can be proven |
| Duplicate/empty/oversized input | Invalid operation manifest | No state access or mutation | Deterministic validation failure |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs` -- released single-key read/write/erase surface; must remain source compatible.
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` -- DAPR adapter and natural implementation point for an additive batch capability; transaction values require DAPR-compatible serialization.
- `src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs` -- existing single-key retry policy; batch conflict recovery must re-read/recompute the whole batch rather than retry keys independently.
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs` -- must expose one `DaprReadModelStore` instance through both single-key and additive batch interfaces.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` -- deterministic JSON-round-tripping fake; lacks atomic batch, resumable progress, and operation-boundary failure injection.
- `src/Hexalith.EventStore.Server/Projections/ProjectionStateEraser.cs` -- closest same-store DAPR transaction and ordered resumable precedent; its Boolean outcome is insufficient for this story.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` -- currently writes projection state before a separate checkpoint and can swallow checkpoint failure; adoption is a separate explicit scope choice.
- `tests/Hexalith.EventStore.Client.Tests/Projections/RecordingDaprClient.cs` -- transaction recorder suitable for transaction-shape and ambiguous-outcome tests.
- `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelWritePolicyTests.cs` -- proves only sequential detail/index writes today, not coordination.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs` -- live Redis/DAPR fixture for persisted detail/index/marker/checkpoint evidence.

## Tasks & Acceptance

**Execution:**
- `src/Hexalith.EventStore.Client/Projections/` -- add one-type-per-file batch interface, typed write/delete operation factories, concurrency policy, immutable manifest/fingerprint, marker, and structured result contracts after the blocked decisions select their exact semantics.
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` -- implement the selected transaction and/or resumable protocol, including durable reconciliation after ambiguous failure.
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs` -- register the same concrete singleton for single-key and batch interfaces.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` -- implement equivalent observable semantics plus deterministic conflict, cancellation, before-operation, after-durable-completion, and partial-failure injection.
- `tests/Hexalith.EventStore.Client.Tests/Projections/` -- cover validation, serialization, operation ordering, success, ETag conflict, duplicate identity, conflicting identity reuse, cancellation, ambiguous completion, injected partial failure, retry, and DI aliasing.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs` -- assert persisted detail, index, marker, and selected checkpoint state for success, conflict, duplicate, cancellation, and recovery through real DAPR/Redis.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- prove domain modules consume the platform seam and do not introduce raw DAPR batch plumbing.

**Acceptance Criteria:**
- Given one logical projection batch contains heterogeneous typed writes or deletes in one configured store, when it is flushed, then every operation retains its key, value/delete intent, and selected concurrency policy, and existing single-key consumers remain compatible.
- Given the selected backend policy classifies a store as transaction-safe, when the batch flushes, then exactly one DAPR state transaction carries the ordered operations and completion evidence; given any other backend, when it flushes, then the selected resumable protocol reports partial progress truthfully and retry converges.
- Given durable completion has not been proven, when conflict, failure, cancellation, or ambiguity occurs, then the result is not success and the same identity can reconcile without losing already durable state.
- Given a completed identity is retried with the same fingerprint, when it is flushed, then the result is idempotent success without reapplication; given a different fingerprint, then no new operation runs and identity conflict is returned.
- Given a batch reports success, when the production state store is inspected, then detail, index, completion marker, and every explicitly in-scope checkpoint contain the required end state.
- Given deterministic and live-DAPR lanes exercise success, conflict, duplicate identity, cancellation, and failure between logical detail/index operations, when verification completes, then assertions inspect persisted state rather than only calls or HTTP status.

## Spec Change Log

## Review Triage Log

## Design Notes

The additive interface avoids another breaking expansion of `IReadModelStore`. Transaction capability must be an explicit semantic policy rather than raw DAPR metadata: the repository has evidence that Redis can advertise `TRANSACTIONAL` while a conditional multi-operation request partially commits and provides no per-operation result. A DAPR exception or canceled client call is therefore not proof of rollback.

The current production projection path cannot be cited as coordinated proof: it persists projection state, then separately saves a checkpoint, and an existing failure path leaves the state durable while swallowing checkpoint failure. Whether Story 1.10 replaces that ordering or only supplies the batch primitive is an unresolved scope decision with different persisted outcomes.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx` -- expected: dependencies restore in package-reference mode.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: no warnings or errors.
- `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class Hexalith.EventStore.Client.Tests.Projections.ReadModelBatchStoreTests` -- expected: contract, DAPR transaction/reconciliation, fake parity, and recovery cases pass.
- `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ReadModelBatchLiveSidecarTests` -- expected: persisted DAPR/Redis end-state cases pass.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj` -- expected: domain authoring guardrails pass.
- `git diff --check` -- expected: no whitespace errors.

## Auto Run Result

Status: blocked

Blocking condition: intent gap

Unanswered decisions:

- What makes a DAPR store transaction-safe: explicit configured allow-list, metadata capability, or attempted transaction? Is any fallback permitted after an ambiguous transaction call?
- May a resumable batch temporarily expose a completed detail write with a stale/missing index, or must readers use staging/promotion or marker-aware reads so partial progress is never visible?
- Does the durable batch include the projection delivery/rebuild checkpoint, or only detail, index, and batch marker, leaving checkpoint integration to Stories 1.12-1.13?
- Is the public model an immediate `FlushAsync(batch)` result or a deferred batch builder with a separate flush lifecycle, and does cancellation return a structured indeterminate result or throw after reconciliation?
- How are marker keys scoped, how long are completed markers retained, and what safe cleanup mechanism prevents unbounded growth without breaking duplicate detection?
- Are duplicate operation keys rejected, and what limits apply to operation count/payload size and ETag modes?

Evidence gathered:

- `IReadModelStore`, `DaprReadModelStore`, `ReadModelWritePolicy`, and `InMemoryReadModelStore` expose only single-key semantics; no batch contract, marker, structured result, or batch failure injection exists.
- DAPR Client 1.18.4 accepts raw `StateTransactionRequest` operations and returns no per-operation transaction result.
- The repository's Redis/DAPR evidence shows a conditional multi-delete can partially commit despite advertised transaction capability, so capability text alone cannot support an atomic-success claim.
- `ProjectionUpdateOrchestrator` persists projection state and checkpoint separately and intentionally tolerates checkpoint failure after the model write, so checkpoint adoption cannot be inferred from the current path.
- Existing tests prove sequential detail/index merging and erasure retry, but no live persisted test proves coordinated batch success, duplicate identity, recovery marker, or checkpoint end state.
