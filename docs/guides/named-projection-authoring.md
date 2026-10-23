[← Back to Documentation](../index.md)

# Named Projection and Read-Model Authoring

Use `IAsyncDomainProjectionHandler` for a projection that persists a named read model. Each handler owns one exact canonical `(Domain, ProjectionType)` route, is created in a dependency-injection scope, and may depend on `IReadModelStore` or `IReadModelBatchStore`.

```csharp
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

public sealed class InventoryDetailProjectionHandler(IReadModelBatchStore batchStore)
    : IAsyncDomainProjectionHandler
{
    public string Domain => "inventory";

    public string ProjectionType => "inventory-detail";

    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        var scope = new ReadModelBatchScope(
            "statestore",
            request.TenantId,
            request.Domain,
            request.AggregateId,
            ProjectionType,
            dispatchId);
        var model = new { EventCount = request.Events.Length, DispatchId = dispatchId };
        ReadModelBatchResult result = await batchStore.ExecuteAsync(
            new ReadModelBatch(
                scope,
                [ReadModelBatchOperation.Write(
                    $"{request.TenantId}:{request.Domain}:{request.AggregateId}:detail",
                    model,
                    ReadModelBatchConcurrency.LastWrite)]),
            cancellationToken).ConfigureAwait(false);

        return ReadModelBatchProjectionResultMapper.Map(result);
    }
}
```

The platform discovers public handlers and publishes their exact routes in operational metadata. Route names must be canonical kebab-case. A domain may expose several handlers with distinct projection types, but duplicate route pairs, case variants, and domains exceeding the configured route limit fail startup validation. Invocation order is ordinal by projection type, independent of registration order.

Treat `dispatchId` as the stable idempotency identity. Pass it as `ReadModelBatchScope.BatchId`; do not generate a replacement identity inside the handler. Await all persistence before returning, propagate the supplied cancellation token, and map `ReadModelBatchResult` through `ReadModelBatchProjectionResultMapper`. Only `Completed` and `AlreadyCompleted` prove durable success. Retryable, incomplete, indeterminate, malformed, or terminal results must not be reported as success.

Use `IReadModelStore` for an independent ETag-aware read-model update. Use `IReadModelBatchStore` when several same-store writes or deletes must converge as one coordinated unit. Domain code must not use a raw DAPR state client, invent batch marker keys, or add projection/query actors; those are platform responsibilities.

For a domain-owned dispatch ledger that is needed only during the supported redelivery horizon, use
`IReadModelExpiringStore.TrySaveAsync(..., timeToLive, ...)` for an independent conditional write, or set
`ReadModelBatchOperation.TimeToLive` on that ledger's batch operation. TTL applies only to the final
read-model value; the batch protocol's staging envelopes and terminal receipts retain their own platform
lifecycle. Do not put TTL on durable detail/index projections unless the product intentionally expires them.
The domain should size dispatch-ledger TTL from `ProjectionDispatchOptions.RedeliveryWindow` so retries and
ledger retention share one validated operational contract.

For source-owned retention that must survive delayed staging, retries, or process replacement, supply
an absolute deadline instead of a relative TTL:

```csharp
ReadModelBatchOperation.WriteExpiring(
    logicalKey,
    model,
    ReadModelBatchConcurrency.LastWrite,
    expiresAtUtc: sourceTimestamp.AddDays(retentionDays));
```

`ExpiresAtUtc` is immutable and inclusive: the value is absent at that UTC instant. Use the same
deadline for related model, integrity, position, and message-receipt operations; keep independently
durable checkpoints non-expiring. Relative `TimeToLive` and `ExpiresAtUtc` are mutually exclusive.
Deadline-bearing batches use a versioned v2 manifest and fingerprint. Existing v1 manifests,
fingerprints, relative TTL behavior, and the original write/record constructor signatures remain
supported. `WriteExpiring` has a separate name so historical `Write(..., default)`, null and
`TimeSpan` calls remain unambiguous. `WithConcurrency` and `SharedProjectionMutation.FromOperation` preserve the deadline through
journaling, streamed staging, promotion, and inherited shared generations.

The SDK stores deadline metadata with final values and computes physical retention from the remaining
time at execution or recovery. Dapr/Redis TTL rounds up to whole seconds; SDK single, bulk, and selected
generation reads still enforce the exact deadline. An expired candidate never reclaims a newer value.
Final values use the existing reserved SDK envelope namespace; supported owner proofs retain the
16,384-byte logical model limit and count actual physical bytes for admission. Use SDK readers to
interpret persisted values. For controlled-clock tests, configure
`ReadModelBatchOptions.TimeProvider` and pass the same clock to the three-argument
`SharedProjectionEpochCoordinator` constructor. `InMemoryReadModelStore.Snapshot<T>` unwraps final values
and enforces that clock while hiding pending batch envelopes; `SnapshotRaw` copies physical protocol
bytes for assertions. The in-memory fake does not emulate provider TTL deletion; use the real sidecar
lane to qualify physical retention.

`TransactionQualified` still requires separate provider atomicity and CAS qualification. The live Redis
profile remains `Resumable`: forcing the transactional profile can expose partially written rows before
its marker update fails. An `Indeterminate` result refuses completion; it does not establish rollback or
hidden-row safety for a misqualified provider.

## Compatibility and rebuild boundary

`IDomainProjectionHandler.Project(ProjectionRequest)` remains the synchronous, domain-only full-replay compatibility seam used by the released `/project` endpoint. Existing handlers do not need to migrate.

When a legacy handler must participate in named v2 dispatch, map it explicitly with `AddLegacyProjectionHandlerAdapter<THandler>(domain, projectionType)`. Unmapped legacy handlers remain v1-only, and an ambiguous legacy-plus-named registration for the same route is rejected.

Normal delivery invokes named handlers only after the server admits the exact metadata route and lifecycle state. A tenant/domain shared projection can implement `IAsyncDomainSharedProjectionRebuildHandler` to participate in the dedicated incremental rebuild protocol. Its `FinalizeAsync` method must remain side-effect-free and return only the canonical replacement plan.

When a committed shared rebuild also needs to reconcile an external index or another non-transactional system, implement `IAsyncDomainSharedProjectionRebuildCompletionHandler`. Put the bounded, opaque reconciliation manifest in `DomainProjectionRebuildPlan.CompletionState`. EventStore retains that state with the rebuild session and calls `CompleteRebuildAsync` only after the canonical batch is committed and verified. The completion operation must be idempotent: a retryable or indeterminate result leaves the canonical read model committed and causes later commit or verification retries to invoke completion again.

### Fenced shared projections

For a tenant and projection family that has several writers, implement `IAsyncDomainSharedProjectionEpochHandler` and opt in with `EpochEnabled`. Declare every required writer in `CreateScope`; each writer must register before capture. EventStore splits a named delivery into individual persisted envelopes, journals each `(source stream, global position)` with its canonical digest and checkpoint, and only reports the request complete when every envelope has crossed the fence. A captured position encountered during Building remains retryable until Commit proves the new generation. Redelivery with the same digest is idempotent; conflicting bytes fail the route and require operator disposition.

The shared journal admits at most 128 pending envelopes per tenant/family epoch. The next position returns `Backpressure` without acknowledgement; drain catch-up and retry that position before sending later envelopes.

The handler's `FoldCatchUpAsync` receives one journaled envelope and the selected physical generation. It owns the domain fold and returns logical `ReadModelBatchOperation` values. Read existing values through `coordinator.ReadGenerationAsync` for that generation. Do not write a physical generation key, select a generation, or advance a checkpoint in the handler. EventStore stages the complete replacement behind bounded chunks, atomically changes the reader selector on Commit, then drains post-capture envelopes. Abort drains into the prior generation before removing staging. Readers must use `ReadAsync` or `ReadManyAsync` and honor `IsStale` and `IsAvailable`; a delivery acknowledgement is not a freshness guarantee.

Private rebuild scratch writes retain their original immutable batch manifest. A retry or an epoch-phase
CAS first settles a pending scratch fence through the SDK recovery seam; it cannot replace the sealed
Building state with a newly planned fingerprint. Legacy pre-manifest scratch is compensated only when
the complete two-key Prepared/Aborting batch and its unchanged Building fence remain provable. A legacy
committed batch or incomplete legacy evidence stays unavailable for explicit owner disposition; the SDK
neither reconstructs missing identity bytes nor rolls back a committed decision. Keep original manifests
and receipts until the existing SDK reclamation protocol permits removal.

Recovery and rebuild readers use `StreamReadPageValidator.ValidateAndGetNextSequence` on every page. `FromSequence` is exclusive: reuse `LastSequenceReturned` as the next cursor without adding one. Validate the canonical tenant, domain, aggregate, event order, and every decoded event before folding or checkpointing. A missing, malformed, or conflicting envelope leaves durable work unresolved; it must not be skipped. An interrupted chunk reservation is retried from the authoritative source before acknowledgement.

Shared epoch state, chunks, receipts, and control-index membership are tenant-scoped operational data. The caller must authorize and audit offboarding, satisfy retention and legal-hold policy, and drain the epoch before `OffboardTenantAsync`. New durable catalog types and non-synthetic shared data remain gated on accountable data-owner approval and a restore drill covering stream, projection, checkpoint, audit, and tenant-key order (AD-28).

### Package contract evidence

The [CI package validation flow](../ci.md) packs the 14-package release inventory and restores isolated consumers without project references. The Contracts, Client, and DomainService consumers also execute R3–R4 probes. The Client consumer exercises capture, journaling, staging, Commit, catch-up, idempotent replay, and the 128-entry backpressure limit through a single Client package reference and an in-process store. For local validation, a `0.0.0-ci-test` pack request creates synthetic `999.0.0-ci-test` artifacts; that version is not a published release.

```bash
python3 scripts/pack-release-packages.py ./nupkgs 0.0.0-ci-test
python3 scripts/validate-nuget-packages.py ./nupkgs
python3 scripts/validate-consumer-package-references.py ./nupkgs
EVENTSTORE_PACKAGE_CONTRACT_DIR=./nupkgs \
  tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests \
  -class '*ProjectionPackageContractTests'
```

Record the semantic-release-derived published version, source SHA, and successful package-only result before Works adopts the SDK. R3–R4 acceptance also requires the focused Dapr/Redis epoch and 10,000-member capture, stage, and delivery results. The AD-28 data-owner approval and restore drill remain separate gates for new durable types or non-synthetic shared data.

## Related APIs

- `IAsyncDomainProjectionHandler` and `DomainProjectionHandlerResult` — named asynchronous handler seam and closed outcome contract
- `IReadModelStore` — independent ETag-aware persistence
- `IReadModelExpiringStore` — independent ETag-aware persistence with a per-write TTL
- `IReadModelBatchStore` and `ReadModelBatchProjectionResultMapper` — coordinated persistence and truthful outcome mapping
- `IAsyncDomainSharedProjectionRebuildHandler` and `IAsyncDomainSharedProjectionRebuildCompletionHandler` — atomic shared-view replacement and durable post-commit reconciliation
- `AddLegacyProjectionHandlerAdapter<THandler>` — explicit compatibility bridge for one legacy handler and one named route

### Deadline format rollout and rollback

Deploy compatible SDK readers to every read and recovery process before enabling `WriteExpiring`
writes. Deadline batches use v2 manifests and pending envelopes; compacted values use the reserved
`$hxrmb` v3 wrapper. Older readers can deserialize an unsupported wrapper as ordinary model JSON
and return default-valued fields. Mixed reader versions therefore cannot safely consume deadline rows.

Rollback requires compatible readers while any new-format row or recovery evidence remains. Stop
deadline writers and settle or age out their original evidence using the compatible SDK before
returning to an older reader. A writer rollback alone does not remove persisted new-format state;
do not rewrite historical bytes or implement a domain-local decoder or recovery protocol.

### Retained private recovery metadata and tenant offboarding

Private retained batches keep payload-free terminal fences at
`hxrmb:v1:marker:{scope-hash}:retired:{SHA256(fingerprint)}` (using the marker prefix defined by the SDK).
Version 1 fences retain the two validated legacy participant digests; version 2 fences record the
Completed or Aborted decision and original operation count. Version 2 participant proofs use the
same fence key plus `:participant:{ordinal}`. Each proof is sealed before installation and retains
original scope, key, ordinal, candidate/previous SHA-256 digests, retention and concurrency mode/ETag digest.
Each version 2 record is bounded to 64 KiB to allow worst-case JSON escaping of valid scope/key inputs; version 1 legacy fences also allow 64 KiB physical JSON without changing their canonical bytes; the original batch still supports 100 operations and a one-MiB
canonical manifest. The legacy allowance covers all three copies of six 512-byte scope components,
two 512-byte keys, their worst-case sixfold JSON escaping, and 4 KiB of fixed fields/digests. The proofs survive manifest and receipt reclamation so an exited delayed
installer's exact envelope can be conditionally compensated without republishing old private state.
The separate legacy compensation payload has a 4,259,840-byte physical encoding bound to allow
base64 expansion independently of the original one-MiB manifest limit; it is reclaimed after settlement.

The original legacy Aborted marker is also intentionally retained at its original marker key to
prevent provider ETag reuse. These terminal records contain no model or previous-value payload,
but do retain tenant and owner scope, keys, identities, digests and conditional ETag digests. They are
scope-identifying tenant operational metadata, not anonymous data. Inventory these deterministic
keys with the owning state component and include them in the tenant's retention, backup/restore,
and legal-hold decisions. The accountable data owner and operator own those decisions.
`OffboardTenantAsync` does not enumerate or delete terminal fences, participant proofs or retained
legacy Aborted markers. An owner-authorized provider-level purge requires every relevant original
writer drained or terminated, stale-work recreation prevented, and retention/legal-hold obligations
satisfied. Deleting these records while old work can resume removes the recovery guarantee; there
is no automatic fence TTL or arbitrary safe live deletion procedure.

Promoted cleanup runs with an SDK cancellation budget. A failed or budget-cancelled attempt keeps
its durable cleanup intent and increments `eventstore.projection.cleanup.failures` on the existing
`Hexalith.EventStore` meter. The closed `reason` tag is `failure` or `budget-expired`; `disposition`
is `foreground-retry`. No scope, state key or payload is emitted. `BeginAsync` retries both chunk
and streamed-metadata cleanup before starting another epoch; existing terminal phase operations
also reconcile their corresponding durable cleanup. Waiting for background cleanup only drains the
current attempt and does not prove payload removal. Cancelling a caller's wait leaves shared work
running, and its singleton gate remains held until the worker exits. Operators should observe the
counter and arrange a subsequent owning SDK operation when there is no foreground traffic.

A version-2 terminal fence also retains an immutable exact-scope index at
`hxrmb:v1:marker:{scope-hash}:retired`. This supplies the original fingerprint and bounded
participant count for known-marker recovery after the receipt is gone; it is not a global scan.
Named partitioned rebuilds retain a payload-free owner inventory at the original partition-manifest
key plus `:retired`, recording exact rebuild identity, owner ordinal and chunk/transport counts.
These records have the same tenant metadata, offboarding limitation and purge prerequisites above.
Repeated named Abort reconciles the original pending and already-cleaned owner identities from this
inventory, even after the session's cleanup flags have advanced. This is the supported production
trigger for a late original scratch envelope after successful Abort. Original manifests that never
had an inventory remain unavailable when neither the original accepted manifest nor the retained
inventory proves the original owner counts. Missing or corrupt required inventory refuses successful
terminal cleanup and leaves the exact Open epoch and private evidence unchanged. Compatible readers/maintainers
must retain these records while relevant original writers can still resume.

The exact-scope index has the same 64 KiB canonical physical bound as its version-2 fence.
Each accepted-owner inventory has a fixed shape: the original five-field rebuild identity, one
owner ordinal and two integer counts, plus an optional constant-size captured-identity reference;
it contains no chunk hashes, domain state or payload arrays.
Reconciliation checks its exact original identity/ordinal and bounds chunk/transport counts by
`ProjectionDispatchOptions.MaxRebuildEventCount`; each transport part has at most 16 known fragment
identities. There is one such inventory per accepted owner, under the existing accepted-owner limit.
These inventories and indexes intentionally have no TTL. Restore their original immutable identities
and matching fences/proofs with the owning epoch/session before relevant writers resume; restoring
payload/session bytes without the matching terminal metadata can recreate stale work. The data owner
must include the indexes and owner inventories in the same retention, legal-hold and ordered restore
drill as the other tenant operational metadata. OffboardTenantAsync does not enumerate or delete them;
provider-level purge uses the same owner-authorized, drained-writer prerequisites above.

A compensated retained scratch attempt keeps its immutable Aborted decision. The SDK derives its
next private batch ID as `scratch-retry-v1:` plus SHA-256 of the original complete scope hash and
terminal fingerprint. Recovery follows those validated deterministic retirement links, including the
separate legacy replacement identity, before retry or cleanup. Each ID remains bounded, no caller
identity changes, and original fences/indexes/participant proofs survive; a late original installer
cannot commit the replacement or reuse its provider ETag. These per-attempt records share the same
tenant metadata retention, ordered restore, offboarding limitation and drained-writer purge rules.
No retry TTL or mutable attempt catalog is introduced. Independent dependency cancellation emits
`reason=failure`; `budget-expired` requires the shared maintenance budget token to be cancelled.

Terminal receipts with a validated SDK recovery anchor have a separate 41,984-byte physical ceiling:
the unchanged 16 KiB control allowance plus one maximally escaped six-component scope and logical key,
and 4 KiB of anchor fields/digests. Removing only the SDK anchor/count fields must still leave a control
record within its original 16 KiB bound; unknown fields remain charged. Complete/interrupted recovery,
logical-key discovery and read-only receipt proof share this bounded typed reader. This allowance does
not enlarge a model, manifest, pending control record or participant-proof limit.

A maintenance budget setup failure is also a `failure`: it retains durable cleanup, releases the
scheduling gate, and completes existing waits. A subsequent owning operation can retry cleanup after
the configuration is corrected; budget setup never leaves a permanent gate entry.

### Complete original owner histories and bounded captured identity evidence

Partitioned named rebuilds now seal a version-1 `SharedProjectionCapturedIdentityReference` instead
of copying one digest vector per owner into a manifest and checkpoint. The namespace binds the
projection scope, original source stream, rebuild operation and epoch. Immutable leaves contain at
most 64 original positions, owner sequences and canonical event identities; index nodes contain at
most 128 authenticated children. The root reference has constant size. An operation-scoped durable
message-hash index rejects duplicates across distant leaves. Every retained prefix page, including a
partial final leaf, is compared before promotion and seeding. Missing or corrupt proof fails closed.

Folders consumes the existing public shared-rebuild transport: original owner IDs, original sequence
numbers and original global positions, bounded `AccumulatePart` requests (at most 256 events and
two MiB), then `AccumulateComplete` with the complete history fingerprint. Configure
`ProjectionDispatchOptions.MaxRebuildEventCount` for the admitted complete history (for example,
40,000 for the 32,770-event qualification). Transport part admission derives from that configured
limit. This admission setting is an operational bound, not a domain-history truncation rule. The
source actor remains authoritative; owners and retained events are unchanged. Existing Folders
handlers implementing `IAsyncDomainSharedProjectionStreamingRebuildHandler` can consume this transport
without a local persistence protocol or a handler interface migration. The SDK reconstructs the admitted
complete input for that buffered-owner contract; its transient memory follows the configured event
admission. `IAsyncDomainSharedProjectionStreamedOwnerHandler` additionally consumes input as a bounded
stream and avoids materializing the complete owner input. Batches retain the existing 100-operation / one-MiB canonical-manifest limits.

Checkpoint handoff holds the promoted epoch until every original owner is seeded. Each seed uses
recoverable batching with both the current epoch and checkpoint ETags. Ordinary growth retains its
sealed identity reference. Later selected or inherited generations retain earlier proof. Identity
inventory lives outside disposable partition scratch; accepted-owner retirement inventory carries
an optional constant-size identity reference. Abort first fences identity installers irreversibly,
then reconciles deterministic original batch identities (including preparation before any participant
was installed) and reclaims nodes, message indexes and page inventories. Its bounded reservation
inventory and retirement fence remain. Start `RetireAsync` after abort and before starting another
epoch; a retirement already fenced can be retried safely.
A selected or inherited reference cannot be retired by an older operation. There is no automatic
TTL or background deletion of selected evidence. Include identity inventories, nodes, message indexes,
checkpoints and retained batch metadata in the same ordered backup/restore and retention policy.

Upgrade every reader, writer and maintenance process before allowing paged checkpoints to be written.
New readers continue decoding legacy inline `CapturedEventDigests`; the existing four-argument
`SharedProjectionCheckpoint` constructor and legacy coordinator methods remain callable. Legacy seed
retries preserve existing paged proof; extending it uses `SeedCapturedOwnerIdentityAsync`. Rebuild retries
also resume an old inline owner manifest persisted before session acceptance: they validate every
retained digest and preserve that immutable manifest through handoff. New owner manifests use paged
evidence. Old readers
cannot safely interpret paged checkpoints. Rollback after the first paged checkpoint therefore requires
a compatible reader or a coordinated restore of the complete pre-upgrade epoch/checkpoint/proof state
with writers drained. Removing reference fields or deleting proof is not a rollback procedure.

Qualify a change with source-mode Debug builds, the complete affected unit suites and the opted-in
Dapr process lane, then record the commands, pass/fail counts, commit SHA and CI run link in the story
or spec. This is EventStore source qualification; package/release qualification, Folders acceptance,
pin changes, publishing and deployment remain separate work.
