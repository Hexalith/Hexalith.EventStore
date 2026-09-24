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

## Compatibility and rebuild boundary

`IDomainProjectionHandler.Project(ProjectionRequest)` remains the synchronous, domain-only full-replay compatibility seam used by the released `/project` endpoint. Existing handlers do not need to migrate.

When a legacy handler must participate in named v2 dispatch, map it explicitly with `AddLegacyProjectionHandlerAdapter<THandler>(domain, projectionType)`. Unmapped legacy handlers remain v1-only, and an ambiguous legacy-plus-named registration for the same route is rejected.

Normal delivery invokes named handlers only after the server admits the exact metadata route and lifecycle state. A tenant/domain shared projection can implement `IAsyncDomainSharedProjectionRebuildHandler` to participate in the dedicated incremental rebuild protocol. Its `FinalizeAsync` method must remain side-effect-free and return only the canonical replacement plan.

When a committed shared rebuild also needs to reconcile an external index or another non-transactional system, implement `IAsyncDomainSharedProjectionRebuildCompletionHandler`. Put the bounded, opaque reconciliation manifest in `DomainProjectionRebuildPlan.CompletionState`. EventStore retains that state with the rebuild session and calls `CompleteRebuildAsync` only after the canonical batch is committed and verified. The completion operation must be idempotent: a retryable or indeterminate result leaves the canonical read model committed and causes later commit or verification retries to invoke completion again.

### Fenced shared projections

For a tenant and projection family that has several writers, implement `IAsyncDomainSharedProjectionEpochHandler` and opt in with `EpochEnabled`. Declare every required writer in `CreateScope`; each writer must register before capture. EventStore splits a named delivery into individual persisted envelopes, journals each `(source stream, global position)` with its canonical digest and checkpoint, and only reports the request complete when every envelope has crossed the fence. A captured position encountered during Building remains retryable until Commit proves the new generation. Redelivery with the same digest is idempotent; conflicting bytes fail the route and require operator disposition.

The shared journal admits at most 128 pending envelopes per tenant/family epoch. The next position returns `Backpressure` without acknowledgement; drain catch-up and retry that position before sending later envelopes.

The handler's `FoldCatchUpAsync` receives one journaled envelope and the selected physical generation. It owns the domain fold and returns logical `ReadModelBatchOperation` values. Read existing values through `coordinator.ReadGenerationAsync` for that generation. Do not write a physical generation key, select a generation, or advance a checkpoint in the handler. EventStore stages the complete replacement behind bounded chunks, atomically changes the reader selector on Commit, then drains post-capture envelopes. Abort drains into the prior generation before removing staging. Readers must use `ReadAsync` or `ReadManyAsync` and honor `IsStale` and `IsAvailable`; a delivery acknowledgement is not a freshness guarantee.

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
