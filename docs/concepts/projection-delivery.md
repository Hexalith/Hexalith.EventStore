# Projection delivery guarantees

The production named-projection path is an at-least-once, order-safe delivery protocol. It expects DAPR invocation or retry workers to repeat work and prevents a repeated, stale, gapped, or conflicting delivery from silently applying a detail or index projection twice.

## Guarantee boundary

The guarantee applies to asynchronous named dispatch through `/project/v2`, the registered named handler, and its durable read-model batch. The legacy `/project` compatibility endpoint cannot reserve by projection type and does not provide this pre-handler guarantee.

Delivery identity is scoped by `(tenant, domain, aggregate, projection type)`. EventStore's persisted event `MessageId` is the duplicate identity. `SequenceNumber` is positive, gapless, and ordered only within one aggregate stream; it is never a global order. The dispatch ID and `ReadModelBatchScope.BatchId` are the highest event's persisted `MessageId` and remain unchanged on every retry.

Before calling `/project/v2`, the server conditionally writes a projection-scoped reservation with a lease and fencing token. DAPR ETag/first-write concurrency is authoritative across replicas. Only the reserved routes are invoked; detail and index routes complete independently. The handler's required durable batch must return `Completed` or `AlreadyCompleted`, and any compatibility actor/ETag write must succeed, before one conditional delivery-state transition both records completion and advances the projection checkpoint.

The outcomes are deliberately fail-closed:

- a completed exact duplicate is skipped as `delivery_already_completed`;
- an active exact duplicate is deferred as `delivery_in_progress`;
- a missing aggregate-local sequence remains retryable as `delivery_gap`;
- changed identity or canonical content fails as `delivery_identity_conflict`;
- evidence older than the retained exact-receipt window requires reconciliation;
- an older writer overwriting a v2 row is `delivery_schema_regression`;
- unavailable reads or exhausted conditional writes remain retryable as `delivery_state_unavailable`.

A timeout, cancellation, malformed response, or lost response after invocation retains the same reservation and retry identity. Repeating the same batch lets the batch protocol return `AlreadyCompleted`, after which the server can finish the delivery row without applying the read model again. A deterministic handler failure may release only the matching fence. An expired reservation is reclaimed only for handlers whose unchanged batch identity proves replay safety; the higher token fences a late completion from the expired attempt.

## Retention and reconciliation

`EventStore:ProjectionDeliveryIdempotency` has these defaults and bounds:

| Setting | Default | Valid range |
| --- | ---: | ---: |
| `CompletedReceiptLimit` | `256` | `1`-`4096` |
| `ReservationLease` | `00:05:00` | `00:00:30`-`1.00:00:00` |
| `MaxStateTransitionAttempts` | `8` | `1`-`32` |

Compaction retains the newest exact receipts by count. It also preserves the first retained sequence and cumulative completed-prefix fingerprint; state-store TTL is not a correctness boundary. A full canonical history can still prove the completed prefix before appending a new suffix. An old standalone delivery below the receipt floor cannot be proven locally and records payload-free reconciliation work instead of invoking the handler.

The authorized maintenance endpoint
`POST /api/v1/admin/projections/{tenantId}/{projectionName}/delivery-reconciliation/{domain}/{aggregateId}`
requires `GlobalAdministrator` and an attributable operator identity. It reloads the exact authoritative EventStore prefix through the already-persisted checkpoint, validates identities/order/scope, recomputes the frozen chain, and hydrates bounded receipts with operator attribution. It preserves the sequence and never invokes a projection handler or changes detail/index/batch state. A short, unreadable, wrong-scope, or fingerprint-mismatched history remains rebuild-required.

## Migration and operations

Projection delivery v2 is a maintenance cutover, not a rolling upgrade. All old servers and retry workers must be quiesced, delivery state backed up, and non-zero sequence-only rows reconciled before activating the store-global `projection-delivery-writer-protocol` marker. Readiness requires the exact v2 marker. Once activated, rolling downgrade is forbidden; projection erasure never removes the global marker.

Follow the [v2 cutover and rollback runbook](../operations/projection-delivery-v2-cutover.md). Persisted-state rehearsal evidence is recorded in [projection delivery v2 evidence](../operations/projection-delivery-v2-evidence.md).
