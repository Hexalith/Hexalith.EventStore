---
title: 'Story 7.1 Delivery Contract and Poison Handling'
type: 'feature'
created: '2026-11-26'
status: 'in-progress'
story_key: '7-1-delivery-contract-and-poison-handling'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'ed66aef9516a3426a2a66e95c343be2a0d6284dd'
context:
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem.** Subscriber redelivery can repeat handler effects, and rejection or exhausted retries can acknowledge a delivery without preserving recoverable evidence.

**Approach.** Enforce Story 7.1 end to end with scoped marker admission, durable capture before acknowledgement, and authorized recovery evidence. Cover FR34-C1 and NFR6, with supporting NFR2, NFR5, and NFR16.

## Boundaries & Constraints

**Always.** Delivery is at least once and unordered. CloudEvent `id` equals persisted `MessageId`. External deduplication uses the received `(source, id)` per subscription. EventStore uses persisted `MessageId` within the admitted subscription. Sequence is aggregate-local. Operations owns poison retention and replay under AD-31. Keep producer drain, immutable events, and existing projection sequence guards intact.

**Never.** Expose payloads, protected bytes, credentials, claims, raw errors, or hidden tenant evidence. Release ownership on elapsed time or unknown effects alone. Enable production recovery without AD-31 gates, including durable audit, qualified Platform topology, and immutable release identity. This scope includes module code, contracts, docs and isolated verification. Production rollout and Admin UI are outside scope.

## I/O & Edge-Case Matrix

| Given | When | Then | Refusal |
|---|---|---|---|
| Completed marker | Exact redelivery | Acknowledge with zero effects | Conflicting content stays retryable or enters proven poison capture |
| Owned or ambiguous marker | Duplicate, cancellation, handler fault, lost write, or restart | Confirmed no-effect may release by fence. Proven dispatch reconciles to completion | Unknown effect stays pending or durably quarantined. No redispatch without proof |
| Development marker store | Capacity, retention or concurrent acquisition is reached | Compact eligible entries deterministically and enforce count | Retry under pressure while active and ambiguous entries are protected |
| Terminal rejection or configured attempt/age exhaustion | Capture or redelivery runs | Ack after persisted, scope-and-content-matched capture. Reuse one logical record | Failure, timeout, conflict and unknown remain retryable unless durable quarantine is proven |
| Visible poison record | Authorized list/count or audited retry, skip, archive | Scoped metadata, age and opaque cursor. Stable action identity and persisted outcome | Deny before lookup. Unavailable evidence stays unavailable. Accepted is pending |

</frozen-after-approval>

## Code Map

- Client `Subscriptions/{EventStoreDomainEventProcessor,EventStoreDomainEventsOptions,*MarkerStore,EventStorePayloadFingerprint}.cs` owns dispatch, subscription scope and fenced marker admission. Legacy rows without content proof remain retryable.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs` binds the generic CloudEvent callback. `DomainRouteCatalogSidecarMetadata.cs` checks subscription metadata. Preserve channel and catalog admission.
- Operations `Endpoints/DeadLetterOperationsEndpointExtensions.cs` acknowledges proven capture through the actor's item and index transaction. The Admin controller and Dapr adapters expose scoped evidence using Client `IQueryCursorCodec` and `QueryCursorScope`.
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` already sets stable source and ID. Named projection live tests exercise dispatcher, persist and checkpoint seams.

## Tasks & Acceptance

**Execution**

- [x] `src/Hexalith.EventStore.Client/Subscriptions/` Add content-bound admission with qualified create-if-absent, fenced transitions and strong readback. Reconcile legacy identity from authoritative evidence or refuse ambiguity. Bound development retention and capacity.
- [x] `src/Hexalith.EventStore.Operations/` Persist attempt and age evidence and idempotent poison capture with scoped readback. Retain safe message/correlation/causation IDs, tenant/domain/aggregate, sequence, original time/topic, attempts, first/last failure and bounded reason/retryability.
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs` Bind capture-before-ack to validated delivery and configured exhaustion. Require explicit catalog policy for skipped outcomes. Keep upcast and handler semantics on replay.
- [ ] `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs` Extend DTOs compatibly. Align Operations adapters with authorized counts, oldest age, scoped cursors and idempotent audited action status. Reconcile retry via normal admission. Skip/archive preserve the source event.
- [ ] `deploy/dapr/` and `src/Hexalith.EventStore.AppHost/DaprComponents/` Align declared topics, credentials, retry, retention and catalog bindings. Enforce application age limits without broker expiry that can drop unretained events.
- [x] `docs/concepts/event-envelope.md`, `docs/reference/stream-replay-api.md` and `docs/guides/{dapr-component-reference,dapr-faq,disaster-recovery}.md` Align public identity, bounded marker retention, acknowledgement and recovery guidance. Supply Story 5.9's received identity example.
- [ ] `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs` Extend broker evidence with durable effects, markers, poison transfer and restart readback. Extend focused Client, DomainService, Server, Operations, Admin and structured topology tests for the matrix.

**Acceptance criteria**

- Given two aggregates with matching correlation and sequence and distinct persisted IDs, when both CloudEvents and one repeat reach the subscriber, then received IDs match persisted IDs and exactly two effects persist.
- Given duplicate, late, gapped or conflicting delivery, when the production handler/dispatcher runs, then persisted effects, stream head, counts and checkpoints obey their scoped single-delivery baseline.
- Given the fault and authorization matrix, when the exact broker and state profile run, then persisted readback proves acknowledgement, recovery and tenant isolation with no loss or duplicated effect.

## Implementation Notes

- Added content-bound marker fences and bounded development retention. Historical marker rows without content proof fail closed. The Dapr qualification probe distinguishes an explicit first-write rejection, including the observed Redis 1.19.1 response, from a transport or storage failure.
- Added an isolated Operations transport, scope-bound capture and pagination, and stable recovery action evidence. The live Operations smoke exercised the HTTP endpoint and durable actor index. Its duplicate observation left the attempt budget unchanged, the next failure retained one record, and restarting the host and sidecar preserved exact redelivery and conflict outcomes.
- Production callback and recovery wiring remain gated by the exact hosted broker and state profile, catalog and credential activation, immutable release identity, and Story 7.3 audit. The existing AppHost broker fixture halted during readiness with missing route-catalog configuration and inactive projection writer protocol. The isolated subscriber broker smoke verifies the identity example and persisted effects across restart. The combined Platform delivery, poison-transfer, authorization, and recovery matrix remains open.
- The callback verifies the admitted CloudEvent component, topic, type, source, and persisted ID before dispatch or poison observation. The explicit skip policy must agree with the committed subscription catalog. A replay uses the same processor admission and evolution path. Released durable markers remain bound to their original fingerprint, with a new fence for an exact retry.
- Operations reports unavailable indexed evidence without returning a partial count or exposing the underlying error. Resubmitting the same pending retry rearms a failed scheduler registration. A different action cannot replace one of the items while its previous action is pending.
- Dapr transport retries use exponential backoff with no finite inbound exhaustion. EventStore applies the separate attempt and age limits and acknowledges after handling or verified capture. This avoids Dapr 1.19.1's final broker disposition for bounded inbound exhaustion without a dead-letter destination.

## Spec Change Log

## Review Triage Log

## Verification

- Run affected test projects individually, including AppHost topology tests. Run the focused live-sidecar marker and named projection lanes and the broker contract lane with cleanup. Run `dotnet build Hexalith.EventStore.slnx -c Release`.
- Story 3.21's hosted broker admission and Story 7.3 audit remain open. Keep production closure pending until the exact Platform profile and AD-31 evidence pass.
- `dotnet build Hexalith.EventStore.slnx -c Release -m:3` passed in a disposable standard build layout with zero warnings and errors. The final run included the Redis probe fix and used the worktree over `df6a96ab2a5bae24fe5951eee5429bda223cc675`. The shared artifacts layout collided on two projects named `Hexalith.EventStore.Sample.Tests`.
- After the final broker assertion edit, the Release build of `tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj` passed with zero warnings and errors.
- Focused runs passed 75 Client subscription, 22 DomainService, 116 Operations, 96 Server, 14 AppHost topology, and three live-sidecar marker/projection tests. Full Admin Server passed 839 with 13 existing skips. The broader Server run reported a published Tenants fixture SHA mismatch. The broader AppHost run stalled and was stopped.
- The disposable Redis/Dapr broker check received the documented example source and both IDs, verified two total effects after an exact redelivery, and preserved them across subscriber restart. The disposable Operations check verified the persisted actor index, attempt budget, restart reconciliation and conflicting-content refusal.
- Follow-up checks passed 75 Client subscription, 26 DomainService callback, 120 Operations, 67 focused Admin, 22 Server configuration and callback, and nine AppHost catalog topology cases. The full Admin Server run passed 840 with 13 existing skips. The private Redis live-sidecar run passed released-fence reconciliation, duplicate and out-of-order named projection delivery, and projection gap closure with persisted checkpoint readback.
- The private rerun of `dotnet tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net11.0/Hexalith.EventStore.IntegrationTests.dll -class '*PubSubDeliveryProofTests'` exited 1. All three tests failed at fixture initialization with readiness HTTP 503, route catalog `configuration-missing`, and a projection writer-protocol activation timeout. The job removed all three of its containers. The broker matrix remains open.
- The read-only `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-7-1-delivery-contract-and-poison-handling.md --target spec` reported `SOURCE_MISMATCH` because of concurrent Story 3.21 edits to the planning epics. Running the same diagnostic with the committed planning sources in a disposable copy reported `ADAPTER_UNAVAILABLE` for NFR2 and NFR16 and required a fresh authenticated approval and seal.
