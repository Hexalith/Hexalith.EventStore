# Rebuild Semantics

## Resolved decisions

| Concern | Contract |
| --- | --- |
| Handler semantics | The current `IDomainProjectionHandler` is full-replay: invoke it only with the complete event prefix through the requested boundary. A future incremental handler must receive prior operation-staged state plus exactly one contiguous page. |
| Paging | `ProjectionOptions.RebuildPageSize` is positive and defaults to 256. Reads are ordered, contiguous, and bounded by `toPosition`; paging never changes projection meaning. |
| Staging | Each rebuild operation isolates candidate actor/detail/index output from live reads. The existing live `projection-state` and read-model keys remain unchanged before promotion. |
| Equivalence surface | Compare every operation-owned live surface: projection-actor state, required detail/index models, persisted freshness projection versions, and rebuild checkpoints. |
| Promotion | Promote only after all required projections have durable successful outcomes. Use the Story 1.15 batch/resumable-marker seam; where one store transaction cannot cover all surfaces, use explicit resumable boundaries and never claim cross-store atomicity. |
| Checkpoints | Save rebuild checkpoints only after promotion is proven durable. Page reads may record non-completion progress only at a safe resumable boundary. Delivery checkpoints are out of scope and remain unchanged. |
| Lifecycle | Surface `Rebuilding` while work is active. Clear it after durable promotion or terminal failure/cancellation cleanup. Only persisted `ProjectionBacked` freshness evidence is authoritative. |
| Versions | Projection versions come from persisted `IReadModelFreshness`; `ETag` remains an opaque cache validator and cannot be copied into a projection version. |
| Failure | Cancellation, handler failure, store failure, conflict, gap, safety-limit exhaustion, or indeterminate promotion preserves the last complete live view and cannot produce operation success. |
| Safety bounds | `ProjectionOptions.RebuildMaxPrefixEventCount` defaults to 10,000 and `ProjectionOptions.RebuildMaxPrefixBytes` defaults to 67,108,864 (64 MiB); both reject non-positive values. Exceeding either returns `rebuild_prefix_safety_limit_exceeded`. |
| Sequencing | Stories 1.15 and 1.17 are complete; use their coordinated-batch and named-dispatch seams without a Story 1.19-local substitute. |

## Governing Architecture Boundary

| Concern | Binding consequence |
| --- | --- |
| Story identity | Current Story 1.19 is the active reissue of historical rebuild Story 1.14. Historical evidence remains at its original path; current Story 1.14 is the distinct AD-7 projection-erasure story. |
| Dispatch | AD-19 retains `ProjectionDispatchResponse` v2 and `ProjectionDispatchOutcome` as the cross-service carrier. The server persists the normalized route/checkpoint matrix; rebuild introduces no separate public result family. |
| Production profile | Existing DAPR/Redis evidence proves Development/test replay correctness only. A production claim additionally requires the complete content-bound AD-26 PostgreSQL v1, durable-broker, OpenBao, resiliency, and independent-sidecar profile. |
| Tenant | AD-27 canonicalizes exactly one explicit tenant before route, lifecycle, staging, checkpoint, model, or state access; missing, conflicting, invalid, inferred, or public `system` scope fails closed. |
| App channel | AD-28 validates non-Development `/project` and `/project/v2` requests with `dapr-api-token` against startup `APP_API_TOKEN`; caller app ID, mTLS, and ACL success are not substitutes. |
| Admin attribution | AD-29 applies to rebuild start, cancel, resume, and promotion. Authenticated operator or bounded delegation survives the resumable mutation/audit unit; audit failure cannot silently permit the action. |
| Erasure | Story 1.19 rebuild neither authorizes nor proves AD-7 projection removal or the AD-30 full-erasure workflow. A typed `Erased` delivery is handled only under its separately authorized policy workflow. |
| Correlation | AD-32 propagates the first-boundary `X-Correlation-ID` unchanged through orchestration and projection calls; correlation is not a GUID or rebuild/status identity. |
| Catalog | AD-33 resolves every required projection route from one activated generation. Staging, outcome reconciliation, and promotion bind stable route-entry IDs and root/facet digests; non-Development overrides or drift fail readiness. |

## Operation contract

1. Preserve the controller's pre-existing `Running` precondition and enumerate the same tracked aggregate identities.
2. Establish the requested prefix boundary from `toPosition` and the highest available aggregate sequence.
3. Read contiguous pages from the last safe rebuild boundary without exposing a page result as live state.
4. For the current full-replay handler, accumulate the complete prefix within both approved safety ceilings and invoke `/project` once the prefix is complete. Do not invoke it with each page as if that page were the full stream.
5. Persist handler outputs as operation-scoped candidates. Required detail/index candidates and actor projection state remain invisible to live readers.
6. Record bounded, distinguishable outcomes for each required projection. Any incomplete, failed, or indeterminate outcome prevents operation success.
7. Promote candidates through the approved coordinated batch/resumable protocol. Promotion must be retryable under the same operation identity.
8. Read back durable output and freshness evidence before saving the rebuild completion checkpoint.
9. Mark the operation `Succeeded` only after all required outputs and rebuild checkpoints are proven. On failure or cancellation, retain or restore the previous live view and clear stale `Rebuilding` lifecycle state.

Page-read progress is not completion. A resumable boundary is safe only when retry can reconstruct the same complete prefix and candidate outputs without making partial work live.

## Page and replay invariants

- Aggregate sequence starts at 1 and is gapless. The canonical oracle is `AggregateReplayer.Replay<TState>`; tests must exercise its rejection of gaps, duplicates, and a non-1 start rather than duplicating that logic.
- `toPosition` selects the same inclusive event prefix for paged rebuild and canonical replay.
- The page corpus includes event counts `0`, `pageSize`, `pageSize + 1`, `N × pageSize`, and `N × pageSize + 1`, plus a fixture larger than two pages.
- Exact page-size multiples require a correct completion signal; a full page alone is not proof that another page exists.
- Serialization uses the platform JSON configuration so replay and production handler inputs are semantically identical.

## Persisted evidence matrix

| Scenario | Required persisted evidence |
| --- | --- |
| More than two pages | Promoted actor state, detail/index models, freshness versions, and rebuild checkpoints equal canonical replay through the same position. |
| Empty stream | No fabricated events or partial live state; terminal status and checkpoint follow the defined empty-stream behavior. |
| Exact page boundary | No duplicate terminal read, skipped event, or false continuation/completion. |
| Bounded `toPosition` | Persisted outputs and checkpoint equal canonical replay through that position, excluding later events. |
| Mid-rebuild cancellation | Previous live actor/detail/index values and versions remain; no successful completion checkpoint; lifecycle is not left `Rebuilding`. |
| Handler/store failure | Previous live values remain; per-projection failure is distinguishable; operation is not `Succeeded`. |
| Resume/retry | The same operation or approved retry identity converges from a safe boundary to the canonical end state without reapplying a partial live result. |
| Safety ceiling exceeded | Structured failure reason, previous live values intact, no completion checkpoint, no stale `Rebuilding`. |

Deterministic tests must use the real orchestrator, `/project` handler path, and captured persistent
stores/actor state. Recorder call counts are request-shape evidence only. Live DAPR/Redis evidence
is Development/test correctness evidence. Production-authorizing evidence must use the complete
AD-26 profile and retain its profile/catalog digests, canonical tenant, app-channel authentication,
Admin attribution when applicable, and propagated correlation identity.

## Compatibility and sequencing gates

- Story 1.15 supplies the approved `IReadModelBatchStore`/marker-gated resumable primitive. Story 1.19 must not recreate that protocol.
- Story 1.17 named multi-projection dispatch is complete. Consume its named-handler seam for required detail/index outcomes while preserving the domain-keyed full-replay actor path; do not silently narrow the equivalence surface or create a local dispatch substitute.
- Story 1.18 owns `MessageId` delivery deduplication and delivery-checkpoint advancement. Story 1.19 advances only rebuild checkpoints after promotion.
- Preserve released signatures on `IReadModelStore`, `ProjectionLifecycleState`, `QueryResponseMetadata`, and `ProjectionRebuildCheckpoint`. Additive optional types or interfaces are allowed.
- Preserve the immediate/poller full-replay path, operator pause/resume/cancel/retry behavior, bounded `toPosition`, active-index cleanup, and controller authorization behavior unless this contract explicitly changes them.

## Safety-bound gate

The temporary complete-prefix strategy is bounded by `ProjectionOptions.RebuildMaxPrefixEventCount` (default 10,000) and `ProjectionOptions.RebuildMaxPrefixBytes` (default 67,108,864 bytes / 64 MiB). Validation rejects either value when non-positive. Exceeding either bound returns `rebuild_prefix_safety_limit_exceeded`, keeps candidate work non-live, does not advance a completion checkpoint, and clears terminal lifecycle correctly.

## Traceability

- Active Story 1.19 acceptance criteria: FR7, FR33, FR36, NFR7, NFR8, NFR16. Historical Story 1.14 is the retained implementation/evidence source.
- Controlling invariant: AD-20. Supporting invariants: AD-2, AD-5–AD-8, AD-12–AD-15, AD-19, AD-26–AD-30, AD-32–AD-33.
- Brownfield defect: `ProjectionUpdateOrchestrator.DeliverProjectionForRebuildAsync` currently reads bounded pages and writes page-only handler output to the actor's live `projection-state` key.
- Canonical replay oracle: `AggregateReplayer.Replay<TState>`.
