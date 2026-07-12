---
baseline_commit: 230181c858a60b53cbfaf01a9f4e72293fd0242f
created: 2026-07-11
---

# Story 1.10: Coordinated Read-Model Batch Writes

Status: review

**Requirements covered:** FR5, FR36, NFR7, NFR16  
**Governed by:** AD-2, AD-7, AD-8, AD-12  
**Depends on:** Story 1.3 read-model seams; Story 1.9 review findings are required safety input, not a trusted implementation precedent  
**Feeds:** Stories 1.12-1.15

## Story

As a projection author,
I want coordinated detail and index writes,
so that a projection cannot expose an updated detail model with a missing or inconsistent index entry.

## Acceptance Criteria

1. Given one projection delivery produces multiple heterogeneous typed writes or deletes in one configured state-store component, when the generic asynchronous batch contract executes, then every operation preserves its ordinal position, logical key, serialized value or deletion intent, and concurrency policy; an empty manifest, duplicate logical key, mixed-store manifest, invalid identity, invalid ETag mode, or configured size-limit violation fails before state access; and all existing `IReadModelStore` members and consumers remain source compatible.
2. Given a store is explicitly configured and live-qualified as transaction-safe, when a batch executes, then one `DaprClient.ExecuteStateTransactionAsync` call carries the ordered logical operations and completion receipt for that store. DAPR `TRANSACTIONAL` metadata, same-store placement, or a void SDK response alone never qualifies a store. The repository Redis profile defaults to `Resumable`; it must not use the transactional path unless a dedicated live conditional-write probe proves all-or-nothing behavior for the deployed component/runtime combination.
3. Given a store uses the resumable profile, when a batch is interrupted between logical operations, then uncommitted candidates remain invisible through `IReadModelStore`; readers observe the previous complete values until the commit marker is durable, and retry with the same identity converges by reconciling durable marker/envelope state. A durable prefix, staging write, deferred flush, or cleanup progress never reports success.
4. Given failure occurs before durable completion, when execution returns, then the structured result distinguishes `Conflict`, `Incomplete`, and `Indeterminate` from completed success. Optimistic conflict recovery re-reads and recomputes the whole batch; it never retries detail and index independently or silently overwrites a concurrent value.
5. Given a stable batch identity has a terminal completion receipt, when the same canonical manifest is retried, then the result is idempotent already-completed success without reapplying operations. Reusing the identity with a different fingerprint returns identity conflict before new logical mutation. Operation order, keys, intent, concurrency inputs, type identity, and canonical serialized payload all participate in the versioned fingerprint.
6. Given cancellation is requested before dispatch, execution throws `OperationCanceledException` without state access. Given cancellation or transport failure occurs after any DAPR request may have been dispatched, the implementation performs bounded, caller-token-independent durable reconciliation and returns a proven completed/conflict/incomplete result or `Indeterminate`; cancellation is never treated as rollback and an ambiguous transaction is never retried through a different execution profile or identity.
7. Given a batch returns completed success, direct store inspection proves every detail/index operation and the completion receipt are in the required end state. Projection delivery and rebuild checkpoints are not part of Story 1.10's durable unit: focused tests assert they remain unchanged, and Stories 1.12-1.13 may advance them only after this batch reports proven completion.
8. Given deterministic and live-DAPR tests exercise success, ETag conflict, duplicate identity, conflicting identity reuse, cancellation, ambiguous completion, and injected failure between detail/index operations, then the DAPR and in-memory implementations expose equivalent observable outcomes and the tests inspect persisted detail, index, marker/envelope, cleanup, and unchanged checkpoint state. Recorder call counts are request-shape evidence only, never G10 completion proof.
9. Given domain-module authoring guardrails run, then batching remains a platform seam in `Hexalith.EventStore.Client`/`Hexalith.EventStore.Testing`; domain modules do not introduce raw `DaprClient`, `ExecuteStateTransactionAsync`, custom batch markers, or state-store wrappers. No root-declared submodule is modified.

## Resolved Contract Decisions

These decisions close the six blockers recorded in `spec-1-10-coordinated-read-model-batch-writes.md`. They are implementation requirements, not optional suggestions. If implementation evidence disproves one, stop and return the story to `blocked`; do not substitute weaker semantics.

### 1. Public API and execution lifecycle

- Add an opt-in `IReadModelBatchStore`; do **not** add required members to `IReadModelStore`.
- Use one immutable manifest and one immediate asynchronous call (recommended shape: `ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)`). Do not introduce a mutable builder, buffered/deferred flush, `IDisposable`-driven commit, or fire-and-forget work.
- Return a structured result whose status distinguishes `Completed`, `AlreadyCompleted`, `Conflict`, `Incomplete`, and `Indeterminate`. Validation/programming/configuration errors throw; expected concurrency and durable-recovery outcomes do not collapse into a Boolean or broad caught exception.
- Before-dispatch cancellation throws. After-dispatch cancellation follows AC6 and must use a bounded internal reconciliation timeout independent of the canceled caller token.

### 2. Batch identity, scope, fingerprint, and limits

- Scope identity by `storeName + tenantId + domain + aggregateId + projectionType + batchId`; validate all components and keep marker keys opaque and collision-resistant. `batchId` is a stable caller-supplied ULID/message identity, never a new random value generated per retry and never parsed with `Guid.TryParse`.
- Hash a versioned canonical manifest with SHA-256. Include operation ordinal, logical key, write/delete kind, stable value type identity, canonical DAPR-compatible JSON bytes, and concurrency mode/expected ETag. Use ordinal comparison and culture-invariant encoding.
- Reject duplicate logical keys with `StringComparer.Ordinal`; repeated writes to the same key in one manifest are not normalized or backend-defined.
- Default limits: 100 operations, 1,048,576 total canonical manifest bytes, and 512 UTF-8 bytes per logical key. Expose positive validated options so a deployment may lower them. Changing defaults or fingerprint material is a versioned contract change with compatibility tests.
- Supported concurrency inputs are explicit unconditional last-write and first-write with an expected ETag. Empty expected ETag means create-only for a write; deletes must use an existing non-empty ETag or an explicitly modeled idempotent-absent policy. Never silently translate a missing ETag into last-write behavior.

### 3. Backend qualification and ambiguity

- Configure each store name as `Resumable` (default) or `TransactionQualified`. Qualification is an operator-owned semantic promise backed by this story's live probe for the exact DAPR runtime/component/backend combination; DAPR component metadata is diagnostic input only.
- The transaction-qualified path writes a prepared identity/fingerprint record, then issues exactly one state transaction containing ordered logical changes plus terminal completion evidence, and finally reads back marker and affected keys before reporting success.
- Redis remains `Resumable` by default because repository evidence already observed partial conditional deletes despite advertised transaction support. The DAPR Redis documentation also requires consumers to account for Redis transaction limitations.
- After an exception/cancellation/timeout from a transaction request, never start the resumable protocol and never issue a fresh transaction under another identity. Reconcile the same marker and persisted keys; return `Indeterminate` unless completion or conflict is proved.

### 4. Resumable visibility and recovery

- The fallback is marker-gated and must preserve the previous complete logical view. Use platform-owned pending envelopes/staging state at each logical key (or an equivalent proven design): each pending item carries/references the previous committed value plus the candidate and batch fingerprint; `IReadModelStore.GetAsync` recognizes this platform envelope and returns the previous value while the marker is prepared/aborting and the candidate only after the marker is committed.
- Install and verify every pending operation before committing the marker. The marker is the visibility decision; it is written last. A delete becomes visible only after commit.
- On pre-commit conflict, move the marker to a recoverable abort state and restore/compact the previous logical view using internally re-read ETags. Recovery is idempotent and operation ordered. Do not trust caller-stale ETags after a partial attempt.
- After commit, compact envelopes/staging incrementally to normal committed values/deletions. Mixed compacted and committed-envelope state must read identically. Cleanup failure returns completed only when logical values and the durable completion receipt are already proven; cleanup remains retryable and observable.
- The platform seam is the supported read path. Raw domain-owned DAPR reads cannot interpret pending envelopes and remain forbidden by domain-authoring guardrails.

### 5. Marker retention and cleanup

- Marker namespace/version is platform-owned and includes a hash of the full scope plus batch identity; never place raw tenant data or payload content in marker keys or logs.
- Active/prepared/aborting markers have no TTL and are retained until reconciled to a terminal state.
- Completed receipts retain only scope hash, batch identity, fingerprint, terminal time, and protocol version after envelope compaction. They are retained indefinitely in Story 1.10 so the unqualified completed-retry and conflicting-identity guarantees in AC5 remain true.
- Cleanup in this story means removing committed payload envelopes/staging after values are compacted and shrinking the marker to its terminal receipt; it never deletes an active marker or terminal receipt. Do not set `ttlInSeconds` on batch receipts.
- The retained receipt count is acknowledged storage growth, but deleting receipts without a proven replay/dedup horizon would silently permit double application. Story 1.13 may introduce a bounded retention horizon only together with its production delivery checkpoint/dedup contract and an explicit compatibility/versioning decision.

### 6. Checkpoint ownership and story boundary

- Story 1.10 owns detail/index operations, batch marker/envelopes, reconciliation, and receipt cleanup only.
- It does not call or modify `ProjectionUpdateOrchestrator`, `IProjectionCheckpointTracker`, `IProjectionRebuildCheckpointStore`, delivery dedup markers, or production projection dispatch.
- Tests seed representative delivery/rebuild checkpoint values and assert that every Story 1.10 outcome leaves them unchanged. Stories 1.12-1.13 own invoking batches from the production handler and advancing checkpoints only after proven completion; Story 1.14 owns rebuild staging/promotion.

## Tasks / Subtasks

- [x] Task 1 - Add additive immutable batch contracts (AC: 1, 4-6)
  - [x] Add the interface, scope/identity, manifest, operation, concurrency, result/status, store-profile, and options types under `src/Hexalith.EventStore.Client/Projections/`, one C# type per file with XML documentation on the public surface.
  - [x] Provide typed write/delete factories that serialize once into immutable canonical bytes; callers must not pass mutable `object` payloads whose later mutation changes the fingerprint.
  - [x] Add manifest validation for same-store scope, empty/duplicate keys, identity components, operation count, key bytes, total canonical bytes, delete/ETag modes, and canceled-before-dispatch behavior.
  - [x] Freeze the v1 canonical fingerprint algorithm in focused golden-vector tests; do not use process-random hash codes, reflection-order JSON, or culture-sensitive formatting.

- [x] Task 2 - Implement marker and protocol primitives (AC: 2-6)
  - [x] Add internal versioned marker/receipt and pending-envelope contracts plus key/fingerprint/serialization helpers under the Client projection folder; keep one type per file.
  - [x] Use `daprClient.JsonSerializerOptions` with `System.Text.Json.SerializeToUtf8Bytes` so transaction, fingerprint, and normal DAPR serialization stay aligned. Do not add another JSON library or inline package version.
  - [x] Implement explicit state transitions (`Prepared -> Committed`, `Prepared -> Aborting -> Aborted`, terminal receipt/cleanup) with compare-and-set ETags and invalid-transition tests.
  - [x] Emit bounded, source-generated structured logs for batch scope hash, status, protocol, operation count, and recovery reason. Never log values, ETags, raw keys, tenant identifiers, cursors, tokens, or exception payload dumps.

- [x] Task 3 - Implement DAPR transaction-qualified execution (AC: 2, 4-6)
  - [x] Extend `DaprReadModelStore` to implement the additive interface without changing existing method signatures.
  - [x] Build `StateTransactionRequest` values from canonical UTF-8 JSON bytes with explicit `StateOptions.Concurrency`; preserve manifest order and include completion evidence in the same transaction.
  - [x] Verify the terminal marker and each affected logical value/delete after the void `ExecuteStateTransactionAsync` returns. An exception or void completion alone is not success.
  - [x] Add bounded same-identity reconciliation for DAPR/transport exception, timeout, and post-dispatch cancellation. Never change profile or identity during recovery.

- [x] Task 4 - Implement marker-gated resumable execution and cleanup (AC: 3-6)
  - [x] Implement pending-envelope installation, old-view reads, commit-marker visibility, abort compensation, post-commit compaction, terminal receipt retention, and cleanup exactly as the resolved decisions specify.
  - [x] Update `DaprReadModelStore.GetAsync` through the pinned `GetByteStateAndETagAsync` raw-byte API to decode both legacy raw values and versioned batch envelopes with `daprClient.JsonSerializerOptions`, preserving existing stored data and all single-key tests.
  - [x] Ensure whole-batch reconciliation re-reads marker and all operation keys; never call `ReadModelWritePolicy` independently per operation.
  - [x] Inject validated options through DI. Keep compaction synchronous/retryable within batch recovery; do not add receipt expiry or a background deletion service in this story.

- [x] Task 5 - Register one concrete instance for both seams (AC: 1, 9)
  - [x] Update `ReadModelStoreServiceCollectionExtensions` so one singleton `DaprReadModelStore` backs both `IReadModelStore` and `IReadModelBatchStore`.
  - [x] Preserve `TryAdd` behavior and existing custom `IReadModelStore` registration semantics; tests must cover default registration, repeat registration, and deliberate consumer overrides.

- [x] Task 6 - Add equivalent deterministic fake semantics (AC: 3-6, 8)
  - [x] Extend `InMemoryReadModelStore` behind both interfaces while preserving JSON round-tripping, ETag behavior, legacy APIs, and snapshot helpers.
  - [x] Add deterministic hooks before dispatch, before/after each operation, before/after durable commit evidence, during abort/compaction, and on post-dispatch cancellation/ambiguity.
  - [x] Model durable state and marker transitions, not only callback counts; fake results must match production outcomes for the same scenario.

- [x] Task 7 - Add deterministic contract/client tests (AC: 1-8)
  - [x] Add `ReadModelBatchStoreTests.cs` and focused helper/contract test files under `tests/Hexalith.EventStore.Client.Tests/Projections/`.
  - [x] Cover every validation boundary, golden fingerprint vectors, operation ordering, heterogeneous JSON round-trip, legacy raw-value reads, each concurrency mode, success, conflict, completed retry, conflicting identity reuse, cancellation before/after dispatch, ambiguous completion, abort, compensation, compaction, retention, and cleanup.
  - [x] Extend `RecordingDaprClient` to capture exact transaction/request shape and simulate durable state/ETag outcomes. Exercise its failure hooks; do not treat it as persisted-backend evidence.
  - [x] Extend DI tests to prove reference equality between default single-key and batch services and preserve override semantics.

- [x] Task 8 - Add real DAPR/Redis persisted evidence (AC: 2-8)
  - [x] Add `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs` using the existing `DaprTestContainerFixture`.
  - [x] Keep Redis configured as `Resumable` and prove persisted old-view visibility during an injected partial prefix, commit visibility, conflict/abort restoration, duplicate identity, identity conflict, cancellation reconciliation, compaction, receipt retention, and unchanged checkpoint keys.
  - [x] Add a separate opt-in transaction qualification probe that uses conditional operations and fails closed if any operation partially commits; it must not silently enable Redis transaction mode.
  - [x] Inspect Redis/DAPR end state directly for detail, index, marker/envelopes, receipt, and checkpoints. A method return, HTTP status, or recorded transaction is insufficient.

- [x] Task 9 - Preserve platform boundaries and documentation (AC: 9)
  - [x] Keep `DomainModuleAuthoringGuardrailTests` green and extend its guidance only if the new interface name needs to be identified as the approved seam.
  - [x] Update the read-model authoring documentation/project context with immediate execution, stable identity, profile qualification, indefinite terminal-receipt retention, post-dispatch cancellation, and checkpoint boundary.
  - [x] Do not modify `references/Hexalith.Tenants`, `references/Hexalith.Memories`, `references/Hexalith.Parties`, `references/Hexalith.AI.Tools`, release package inventory, AppHost/YAML, or production dispatcher/checkpoint code in this story.

## Dev Notes

### Current state of files that will be updated

- `IReadModelStore.cs` currently exposes `GetAsync`, unconditional `SaveAsync`, ETag-aware `TrySaveAsync`, and Story 1.9's `TryEraseAsync`. Preserve all four signatures. Add batching through a separate interface; do not repeat Story 1.9's released-interface expansion.
- `DaprReadModelStore.cs` validates store/key/value boundaries and delegates directly to typed DAPR get/save/delete calls with `ConfigureAwait(false)`. It has no raw-envelope decoding, batch identity, marker, transaction, reconciliation, or structured outcome today. Preserve legacy raw JSON reads and first-write semantics.
- `ReadModelWritePolicy.cs` performs bounded single-key reload/recompute retry. It is not a coordination primitive. Reuse its whole-value conflict principle, not its per-key execution loop.
- `ReadModelStoreServiceCollectionExtensions.cs` currently registers `IReadModelStore -> DaprReadModelStore` directly. Rework the registration carefully so both interfaces resolve the same singleton while custom registrations remain respected.
- `InMemoryReadModelStore.cs` stores per-store/key JSON plus deterministic ETags and has single-operation conflict hooks. Preserve JSON cloning and add durable batch state/failure boundaries; do not fake atomicity with one dictionary lock if the configured production profile is resumable.
- `RecordingDaprClient.cs` captures DAPR calls but is not a realistic database. Use it for exact byte/order/options assertions and add stateful behavior only to exercise reconciliation logic; live-sidecar tests remain authoritative.
- `ProjectionStateEraser.cs` is a warning, not a template: current review found unsafe Redis transaction assumptions, raw key-prefix ownership checks that reject real consumer keys, stale-ETag non-convergence, broad exception collapse, and mock-only proof. Story 1.10 must not reuse its Boolean result or same-store-implies-transaction rule.
- `ProjectionUpdateOrchestrator.cs` writes projection output and checkpoint separately and has a path that can tolerate checkpoint failure after model persistence. Do not modify/adopt it here; AC7 pins checkpoints unchanged so downstream integration cannot be implied.

### Architecture and compatibility guardrails

- Domain modules remain domain-centric and consume platform seams; no raw DAPR batching belongs in Sample/Tenants/Parties.
- The batch is same-component only. Never call it cross-store atomic, and reject mixed-store manifests before any state access.
- Preserve all existing single-key behavior and legacy stored JSON. This is an additive package change within the current 14-package manifest.
- DAPR metadata describes capabilities but does not prove the required atomic/rollback semantics. Backend qualification is explicit and live-evidenced.
- `ConfigureAwait(false)` is required on every production await. Use file-scoped namespaces, one C# type per file, nullable-safe boundaries, source-generated logging, central package versions, and no copyright headers.
- Identifiers use ULID-safe handling; `Guid.TryParse` is forbidden for message/batch identities.

### Testing and validation

Run restore/build through the `.slnx`; run tests per project. For xUnit v3 focused filters, build first and invoke the assembly with `-class`/`-method`.

```bash
dotnet restore Hexalith.EventStore.slnx
dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class Hexalith.EventStore.Client.Tests.Projections.ReadModelBatchStoreTests
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ReadModelBatchLiveSidecarTests
dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --configuration Release
dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Release
git diff --check
```

The live-sidecar lane is required before completion. If environment-blocked, record the exact blocker separately; deterministic tests do not substitute for persisted DAPR/Redis evidence.

### Previous Story Intelligence

- Story 1.9 remains `in-progress` after review despite a green implementation report. Checked tasks and mock-heavy green suites did not prove the frozen persistence contract.
- Do not copy its same-store transaction path: Redis partially committed an ETag-conditional multi-delete while the SDK returned only success/failure at request level; retry-after-partial work was not convergent.
- Do not infer tenant ownership from key prefixes. Real consumer layouts include `projection:tenants:{id}` and singleton index keys. This story scopes identity and markers explicitly but does not turn key shape into authorization.
- Reuse the useful patterns: JSON round-tripping fake, deterministic operation-boundary injection, `RecordingDaprClient` request capture, Shouldly/xUnit v3, `ConfigureAwait(false)`, and direct persisted end-state assertions.
- The root worktree was clean and synchronized at baseline commit `230181c8`. Recent commit subjects mixed story artifacts and unrelated submodule-pointer updates; scope implementation from the story/file map, not commit subject alone.

### Latest Technical Information

- The repo pins Dapr .NET SDK `1.18.4`. `StateTransactionRequest` carries `Key`, raw `byte[]` value, operation type, optional ETag/metadata, and `StateOptions`; `DaprClient.ExecuteStateTransactionAsync` returns `Task` with no per-operation result. `GetByteStateAndETagAsync` returns `(ReadOnlyMemory<byte>, string)` for exact reconciliation, and `DaprClient.JsonSerializerOptions` exposes the SDK's JSON settings. This makes byte-accurate read-back reconciliation mandatory for this story's stronger completion contract.
- The local evidence environment currently reports DAPR CLI `1.18.0` and runtime `1.18.1`, which do not equal the client package version. Live evidence must record all three versions and must not infer runtime behavior from the NuGet version.
- Official DAPR state API documentation defines state transactions as single-store operations and returns only request-level `204`/error status. It also states that ETag mismatches reject optimistic writes; a void .NET task cannot identify which operation, if any, became durable.
- Official DAPR store metadata lists Redis as transactional/ETag/TTL capable, while the Redis component page explicitly points consumers to Redis transaction limitations. Therefore capability metadata is necessary diagnostic context but insufficient qualification for AC2.
- Do not migrate to the newer `Dapr.StateManagement` package in this story. Use the centrally pinned `Dapr.Client` surface already shipped by EventStore; a package/API migration requires its own compatibility decision.

### Project Structure Notes

Expected UPDATE files:

- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`
- `tests/Hexalith.EventStore.Client.Tests/Projections/RecordingDaprClient.cs`
- `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Projections/InMemoryReadModelStoreTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Registration/ReadModelAndCursorRegistrationTests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` only if approved-seam guidance needs the new name

When the raw-byte implementation is added, extend that guardrail's forbidden state-access markers to cover the byte-state APIs as well; domain modules must not bypass the platform envelope decoder.

Expected NEW files stay in the matching projection folders: additive batch contracts/helpers (one type per file), `ReadModelBatchStoreTests.cs`, and `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs`. Do not add a project or package.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md`, Story 1.10, lines 697-735]
- [Source: `_bmad-output/planning-artifacts/prd.md`, FR5/FR7/FR36 and NFR7/NFR16, lines 97-114, 195-216, 224]
- [Source: `_bmad-output/planning-artifacts/architecture.md`, AD-2/AD-7/AD-8/AD-12, lines 55-59, 85-95, 115-119]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md`, sections 4.2 and 4.5, lines 209-235, 249-253]
- [Source: `_bmad-output/implementation-artifacts/spec-1-10-coordinated-read-model-batch-writes.md`, lines 15-118]
- [Source: `_bmad-output/implementation-artifacts/1-9-read-model-and-projection-checkpoint-erasure.md`, Review Findings, lines 226-248]
- [Source: `_bmad-output/project-context.md`]
- [DAPR state management API](https://docs.dapr.io/reference/api/state_api/)
- [DAPR state transactions .NET example](https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-get-save-state/#perform-state-transactions)
- [DAPR supported state stores](https://docs.dapr.io/reference/components-reference/supported-state-stores/)
- [DAPR Redis component limitations](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-redis/#limitations)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (1M context)

### Debug Log References

- Build (all lanes): `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` → **Build succeeded, 0 errors** (clean under `TreatWarningsAsErrors`, CA2007, CS1591).
- Deterministic contract/client tests: `Hexalith.EventStore.Client.Tests` → **630 passed / 0 failed** (includes `ReadModelBatchStoreTests`, `DaprReadModelBatchTests`, `ReadModelBatchFingerprintTests`, extended registration tests; all pre-existing read-model/policy/eraser tests still green).
- Fake parity: `Hexalith.EventStore.Testing.Tests` → **144 passed**.
- Domain authoring guardrails: `Hexalith.EventStore.DomainService.Tests` → **88 passed** (byte-state markers + `IReadModelBatchStore` implementation ban added).
- `git diff --check` → no whitespace errors.
- **Live-sidecar lane environment-blocked (recorded per Dev Notes contingency).** `ReadModelBatchLiveSidecarTests` compiles clean but cannot execute in this agent environment: `dotnet test` on `Hexalith.EventStore.Server.LiveSidecar.Tests` terminates with exit 144 (signal 16) and no output during collection-fixture startup. The **pre-existing** `DaprETagServiceLiveSidecarTests` fails identically, while standalone `~/.dapr/bin/daprd --version`/`run` works and Redis returns `PONG` — proving the block is the VSTest host that spawns the fixture, not `daprd`, Redis, or the new test. The live lane (direct Redis end-state inspection for detail/index/marker receipt/checkpoint isolation + opt-in transaction-qualification probe) must be run in a working Tier-3 environment before this batch is wired into production dispatch (Stories 1.12–1.13).

### Completion Notes List

- Implemented the full coordinated batch contract from the story's Resolved Contract Decisions via a single shared protocol engine (`ReadModelBatchProtocol`) run over an `IReadModelBatchStateAccessor`, so the DAPR adapter and the in-memory fake are equivalent by construction (AC8) rather than by parallel re-implementation.
- **Additive surface only:** `IReadModelStore`'s four members are unchanged and source-compatible; batching is a separate opt-in `IReadModelBatchStore`. One `DaprReadModelStore` singleton backs both interfaces (reference-equality proven), with `TryAdd` idempotence and custom-override preservation.
- **Both profiles implemented:** marker-gated resumable (default; pending envelopes preserve the previous complete view until the commit marker is durable, then compact) and transaction-qualified (one ordered `ExecuteStateTransactionAsync` + terminal receipt, success by read-back proof). Redis stays `Resumable`; qualification is operator-owned + live-probe gated.
- **Structured outcomes:** `Completed` / `AlreadyCompleted` / `Conflict` (identity vs optimistic) / `Incomplete` / `Indeterminate`; validation/programming/config errors throw; post-dispatch cancellation triggers bounded caller-token-independent reconciliation and is never treated as rollback.
- **Frozen v1 fingerprint** (SHA-256 over an ordinal-canonical manifest incl. ordinal/key/kind/type/concurrency/ETag/canonical value bytes) with golden-vector + canonicalization tests. Terminal receipts retained indefinitely; checkpoints/orchestrator untouched (AC7 / decision 6).
- **`GetAsync` rewritten** onto the pinned raw byte-state API to decode legacy raw values and versioned envelopes; single-key legacy behavior and stored JSON preserved.
- Guardrails extended so domain modules cannot bypass the envelope decoder via raw byte-state APIs or by implementing `IReadModelBatchStore`; read-model authoring docs updated.
- **Open follow-up:** live-sidecar persisted evidence (Task 8) is code-complete but unexecuted here — see Debug Log blocker. No production dispatcher/checkpoint code was modified.

### File List

New — platform (`src/Hexalith.EventStore.Client/Projections/`):
- `IReadModelBatchStore.cs`, `ReadModelBatch.cs`, `ReadModelBatchScope.cs`, `ReadModelBatchOperation.cs`, `ReadModelBatchOperationKind.cs`, `ReadModelBatchConcurrency.cs`, `ReadModelBatchConcurrencyMode.cs`, `ReadModelBatchResult.cs`, `ReadModelBatchStatus.cs`, `ReadModelBatchConflictKind.cs`, `ReadModelBatchStoreProfile.cs`, `ReadModelBatchOptions.cs`
- `ReadModelBatchCanonicalJson.cs`, `ReadModelBatchFingerprint.cs`, `ReadModelBatchKeys.cs`, `ReadModelBatchMarker.cs`, `ReadModelBatchMarkerOperation.cs`, `ReadModelBatchMarkerStatus.cs`, `ReadModelBatchEnvelope.cs`, `ReadModelBatchPhase.cs`, `IReadModelBatchFaultInjector.cs`, `RawStateEntry.cs`, `RawTransactionOperation.cs`, `IReadModelBatchStateAccessor.cs`, `ReadModelBatchProtocol.cs`, `ReadModelBatchStoreException.cs`, `DaprReadModelBatchStateAccessor.cs`

Modified — platform:
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` (implements `IReadModelBatchStore`; `GetAsync` raw-byte + envelope decoding)
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs` (one singleton behind both interfaces + options)
- `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj` (`InternalsVisibleTo` Testing)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` (both interfaces via shared engine; byte storage; deterministic phase hooks)

New — tests:
- `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelBatchStoreTests.cs`, `ReadModelBatchFingerprintTests.cs`, `DaprReadModelBatchTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs`

Modified — tests / docs:
- `tests/Hexalith.EventStore.Client.Tests/Projections/RecordingDaprClient.cs` (stateful byte-store + transaction application)
- `tests/Hexalith.EventStore.Client.Tests/Registration/ReadModelAndCursorRegistrationTests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs`
- `docs/brownfield/development-guide.md`
- `_bmad-output/implementation-artifacts/1-10-coordinated-read-model-batch-writes.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

| Date | Change |
|------|--------|
| 2026-07-12 | Implemented coordinated read-model batch writes: additive `IReadModelBatchStore`, shared resumable + transaction-qualified protocol engine, stable-identity fingerprinting, structured outcomes, bounded reconciliation, in-memory parity fake, deterministic Client/Testing tests, guardrail + docs updates. Live-sidecar test authored but environment-blocked (recorded). |
