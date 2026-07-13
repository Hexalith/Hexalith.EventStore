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

## Compatibility and rebuild boundary

`IDomainProjectionHandler.Project(ProjectionRequest)` remains the synchronous, domain-only full-replay compatibility seam used by the released `/project` endpoint and the current rebuild flow. Existing handlers do not need to migrate.

When a legacy handler must participate in named v2 dispatch, map it explicitly with `AddLegacyProjectionHandlerAdapter<THandler>(domain, projectionType)`. Unmapped legacy handlers remain v1-only, and an ambiguous legacy-plus-named registration for the same route is rejected.

Normal delivery invokes named handlers only after the server admits the exact metadata route and lifecycle state. Rebuild does not invoke persistence-capable named handlers yet; incremental rebuild staging, resume, and promotion belong to the dedicated rebuild protocol. Do not call `/project/v2` from a custom rebuild path.

## Related APIs

- `IAsyncDomainProjectionHandler` and `DomainProjectionHandlerResult` — named asynchronous handler seam and closed outcome contract
- `IReadModelStore` — independent ETag-aware persistence
- `IReadModelBatchStore` and `ReadModelBatchProjectionResultMapper` — coordinated persistence and truthful outcome mapping
- `AddLegacyProjectionHandlerAdapter<THandler>` — explicit compatibility bridge for one legacy handler and one named route
