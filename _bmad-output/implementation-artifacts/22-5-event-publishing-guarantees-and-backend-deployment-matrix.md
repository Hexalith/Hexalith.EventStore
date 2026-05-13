# Story 22.5: Event Publishing Guarantees and Backend Deployment Matrix

Status: ready-for-dev

Context created: 2026-05-12
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR96-FR98, with direct dependency awareness for Stories 22.1-22.4 public gateway, projection adapter, authorization, and query policy boundaries.

## Story

As a downstream projection owner,
I want EventStore-published events to be durable, at-least-once, and causally ordered per aggregate where supported,
so that Parties projections can rely on EventStore publication guarantees.

## Event Publishing Contract

- EventStore owns publish-after-persist guarantees for domain success and rejection events. Domain services produce payload events; EventStore persists, envelopes, publishes, drains, and dead-letters.
- The durable guarantee is persist-first: once events are written to the aggregate stream, a publication failure must not lose them. Recovery is through `UnpublishedEventsRecord` plus actor reminders and drain retry.
- Delivery is at-least-once, not exactly-once. Downstream projection owners must treat duplicate CloudEvents as normal and use tenant/domain/aggregate/sequence/correlation metadata for idempotency.
- Per-aggregate causal order is guaranteed by persisted aggregate sequence numbers. Pub/sub delivery order is backend-dependent and must be documented per supported DAPR component.
- Backend support must be explicit for local Redis, RabbitMQ, Kafka, and Azure Service Bus deployment files. Unsupported ordering/session/dead-letter features must be called out rather than implied.
- Dead-letter behavior has two distinct meanings: EventStore infrastructure dead-letter messages for command processing failures, and DAPR subscriber dead-letter handling after delivery retry exhaustion. Keep both documented and tested separately.
- This story may add metadata or docs for ordering/session keys, but must not reopen payload-protection semantics owned by Stories 22.7a-22.7d or replay/read APIs owned by Story 22.6.

## Guarantee Boundaries

- Durable means EventStore event persistence is the source of truth: events are written before the first publish attempt, and failed publication is represented by recoverable unpublished-event state or a stable infrastructure failure path.
- At-least-once means persisted unpublished events are retried until DAPR accepts publication, a bounded drain record remains, or EventStore records an explicit infrastructure failure/dead-letter state. It does not mean exactly-once delivery or successful subscriber processing.
- Per-aggregate ordering means EventStore publishes and drains events in persisted aggregate sequence order, and sets or documents backend-specific routing/session/partition metadata where supported. Transport delivery and subscriber processing order remain backend-dependent.
- Backend claims must be labeled as proven, configured, unsupported, or documented-only. Do not use "supported" for Redis, RabbitMQ, Kafka, or Azure Service Bus behavior without a matching proof level.
- Operator-visible diagnostics must use metadata only: tenant, domain, aggregate, stream/version or sequence range, event type, topic, correlation/causation/message IDs, retry state, backend, timestamp, and failure reason. Never include event payload bytes, protected data, connection strings, or subscriber credentials.

## Current Implementation Intelligence

- `EventPublisher.PublishEventsAsync` publishes persisted `EventEnvelope` instances through `DaprClient.PublishEventAsync` using `EventStore:Publisher:PubSubName` and the tenant/domain topic from `AggregateIdentity.PubSubTopic`.
- Published metadata currently sets `cloudevent.type`, `cloudevent.source`, and `cloudevent.id`. There is no explicit backend-neutral ordering/session/partition metadata yet.
- CloudEvent IDs currently use `{correlationId}:{sequenceNumber}`. Consumers can observe `correlationId`, `tenantId`, `domain`, `aggregateId`, and `sequenceNumber` in the event data payload.
- `EventPublisher` unprotects event payload bytes before publish through `IEventPayloadProtectionService.UnprotectEventPayloadAsync`. Story 22.5 must preserve no-payload-logging and record any protection concern as a Story 22.7 defer rather than changing protection policy here.
- Publish failures return `EventPublishResult(false, publishedCount, failureReason)` rather than throwing for most infrastructure failures. `OperationCanceledException` is intentionally propagated.
- `AggregateActor` writes events before publication. When publication fails after persistence, it moves the command to `PublishFailed`, stores an `UnpublishedEventsRecord`, registers a drain reminder after a successful state commit, and writes advisory command status.
- Drain records store `CorrelationId`, `StartSequence`, `EndSequence`, `EventCount`, `CommandType`, `IsRejection`, `FailedAt`, `RetryCount`, and `LastFailureReason`.
- Drain retry reads the persisted event range, validates `EventCount` against the stored sequence range, republishes through `IEventPublisher`, removes the drain record after successful publish, unregisters the reminder, and transitions status toward `Completed`.
- Drain failure classification uses stable `DrainReasonCodes`: `drain_event_count_mismatch`, `drain_missing_event`, `drain_publish_failed`, `drain_state_store_failure`, `drain_dapr_unavailable`, and `unknown`.
- `docs/operations/drain-failure-reason-codes.md` already documents drain observability codes. This story should keep docs and code aligned if categories change.
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` uses local Redis pub/sub with `enableDeadLetter=true`, `deadLetterTopic=deadletter`, strict component scopes, DAPR publishing/subscription scopes, and a dev-only `eventstore-test-subscriber` grant for Tier 3 proof tests.
- Production deployment component examples exist for RabbitMQ, Kafka, and Azure Service Bus under `deploy/dapr/`. They document scoping and dead-letter settings, but do not yet form a single guarantee matrix covering ordering/session keys, partition selection, retry/drain behavior, and backend limitations.
- `docs/guides/configuration-reference.md` documents `EventStore:Publisher` and `EventStore:Drain`, supported DAPR pub/sub component types, and environment variables for Redis/RabbitMQ/subscriber app IDs. It does not yet freeze backend-specific delivery guarantees.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs` proves Redis local pub/sub delivery and publish-after-persist drain recovery using the test subscriber and a Development-only publish fault file.
- Existing Tier 3 proof verifies subscriber receipt and drain recovery for one Redis-backed local topology. It does not yet prove RabbitMQ, Kafka, or Azure Service Bus ordering/dead-letter behavior.
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`, `PersistThenPublishResilienceTests.cs`, `EventPublisherRetryComplianceTests.cs`, and `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` are the focused unit-test entry points for publisher/drain behavior.

## Acceptance Criteria

1. **Publish-after-persist guarantee is explicit and enforced.**
   - Given EventStore persists success or rejection events for a command
   - When event publication succeeds, partially succeeds, fails, resumes, retries, drains, or dead-letters
   - Then the command lifecycle, status transitions, drain record state, and documentation define the durable at-least-once behavior without claiming exactly-once delivery.
   - And a persisted event must either be published, remain represented by a bounded recovery/drain record, or be surfaced through a stable operational failure path.
   - And durable means EventStore commit durability plus unpublished-event recovery state, not durable broker storage in every backend.
   - And a crash between publish success and recovery-state cleanup may cause duplicate publication, not event loss.

2. **Duplicate tolerance and consumer idempotency are public guidance.**
   - Given a downstream projection receives the same persisted event more than once
   - When it processes published CloudEvents
   - Then docs and tests show the stable idempotency fields consumers should use: tenant, domain, aggregate, sequence number, correlation ID, causation ID, message ID, event type, and topic.
   - And EventStore does not promise subscriber-side exactly-once processing or hide backend duplicate delivery realities.

3. **Ordering, partition, and session metadata are defined per backend.**
   - Given EventStore publishes events for one aggregate
   - When the selected DAPR pub/sub backend supports partition keys, ordering keys, sessions, or equivalent metadata
   - Then EventStore supplies or documents the required per-aggregate routing metadata and preserves causal order where supported.
   - And the backend matrix explicitly marks unsupported or not-yet-implemented ordering/session behavior for Redis, RabbitMQ, Kafka, and Azure Service Bus.
   - And each backend row identifies the ordering boundary, required DAPR component settings, routing/session/partition key mechanism, durability mechanism, dead-letter capability, proof level, and known caveats.
   - And Redis, RabbitMQ, Kafka, and Azure Service Bus claims distinguish EventStore sequence ordering from broker delivery order and subscriber processing order.

4. **Retry, drain, and dead-letter policy is stable and observable.**
   - Given publication fails before, during, or after a batch
   - When the drain mechanism retries or exhausts useful recovery paths
   - Then retry timing, drain record shape, failure reason codes, reminder behavior, command status behavior, dead-letter topic policy, and manual operator guidance are documented and covered by focused tests.
   - And `pending_command_count` and pipeline cleanup semantics remain correct when publication fails, drains later, or drain state cannot be saved.
   - And failed publish creates or preserves `UnpublishedEventsRecord`; successful drain cleans up only after confirmed publish success.
   - And repeated drain, actor restart, reminder overlap, process shutdown, and partial-batch publish failures are idempotent and duplicate-tolerant.
   - And operator diagnostics expose retry/failure metadata without payloads, secrets, protected data, or backend connection strings.

5. **DAPR component deployment matrix is actionable.**
   - Given an operator deploys EventStore with Redis, RabbitMQ, Kafka, or Azure Service Bus pub/sub
   - When they configure DAPR components and subscriber app IDs
   - Then docs identify required metadata/settings for topic scoping, publisher grants, subscriber grants, dead-lettering, retries/resiliency, ordering/session/partition behavior, topic pre-creation, and known limitations.
   - And dev-only `eventstore-test-subscriber` grants are not represented as production defaults.

6. **Backend-specific proof is honest.**
   - Given backend-specific integration tests cannot run for every supported backend in the local automation environment
   - When proof is recorded
   - Then Redis local proof, RabbitMQ/Kafka/Service Bus requirements, and any unsupported/deferred backend proofs are clearly separated.
   - And tests prove publish-after-persist recovery, duplicate delivery tolerance, per-aggregate causal ordering where supported, and dead-letter handling for each backend that this story claims as verified.
   - And backend proof is recorded as unit, component/config inspection, Docker/Aspire integration, manual/environment-gated proof, documented limitation, or not proven.
   - And DAPR subscriber dead-letter proof is not treated as proof of EventStore infrastructure dead-letter behavior, or vice versa.

7. **Documentation and evidence are recorded before review.**
   - Reference docs, deployment docs, configuration docs, and operations docs describe the guarantee matrix and operational recovery path.
   - Focused Server publisher/drain tests and any applicable integration tests pass individually.
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [ ] **ST0 - Baseline current publishing guarantees and classify gaps.** (AC: 1, 2, 3, 4, 5, 6)
    - [ ] Read this story, Epic 22, PRD FR96-FR98, architecture publishing/replay/protection contract notes, Stories 22.1-22.4, and `docs/concepts/command-lifecycle.md` before code edits.
    - [ ] Inventory `EventPublisher`, `EventPublishResult`, `AggregateActor` publish/drain paths, `UnpublishedEventsRecord`, drain exceptions, `DrainReasonCodes`, `DeadLetterPublisher`, `EventPublisherOptions`, and `EventDrainOptions`.
    - [ ] Inventory local and deployment pub/sub components: `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`, `deploy/dapr/pubsub-rabbitmq.yaml`, `deploy/dapr/pubsub-kafka.yaml`, and `deploy/dapr/pubsub-servicebus.yaml`.
    - [ ] Inventory existing docs and tests for command lifecycle, event envelope, configuration, DAPR component reference, drain failure reason codes, PubSubDeliveryProofTests, DeadLetterTests, and publisher/drain unit tests.
    - [ ] Record a decision table for durability, at-least-once, duplicate tolerance, ordering/session metadata, partial publish handling, drain retry, dead-letter routing, and backend proof status.

- [ ] **ST1 - Freeze publish contract metadata.** (AC: 1, 2, 3)
    - [ ] Define the public fields downstream consumers use for idempotency and ordering. Prefer existing envelope fields unless a backend-specific DAPR metadata field is required.
    - [ ] Decide whether EventStore should set explicit DAPR metadata for ordering/session/partition keys, such as aggregate identity or tenant/domain/aggregate composite, and document why for each backend.
    - [ ] Preserve current CloudEvents 1.0 behavior and `cloudevent.id` compatibility unless a SemVer-relevant change is explicitly approved.
    - [ ] Add focused tests that pin CloudEvent metadata and event data fields without requiring a live sidecar where unit-level fakes can prove it.
    - [ ] Ensure logs and activity tags continue to avoid event payload, protected payload, secrets, subscriber credentials, and backend connection strings.

- [ ] **ST2 - Harden publish failure and drain recovery semantics.** (AC: 1, 4)
    - [ ] Verify partial publish behavior: if some events publish before failure, the drain path remains at-least-once and duplicate-tolerant without losing unpublished persisted events.
    - [ ] Verify `UnpublishedEventsRecord.EventCount` matches the persisted sequence range and mismatch handling keeps a stable failure reason instead of silently deleting recovery state.
    - [ ] Verify missing event, state-store failure, DAPR unavailable, publish failed, and unknown drain errors map to documented `DrainReasonCodes`.
    - [ ] Verify drain success removes the record only after successful publish and unregisters reminders without hiding unregister failures.
    - [ ] Verify `pending_command_count` is not decremented prematurely on publish failure and remains bounded/clean after terminal or drain-success paths according to existing backpressure semantics.
    - [ ] Record the unpublished-record lifecycle decision before code completion: pending/publishing remains retryable on retryable failure; published/removed only after confirmed DAPR publish success; failed/dead-lettered only after configured retry exhaustion or stable non-retryable failure.
    - [ ] Prove partial publish behavior: if events 1..N publish and event N+1 fails, the next drain resumes without skipping, losing, or publishing later aggregate events out of persisted sequence.
    - [ ] Prove crash/restart and reminder-overlap behavior: repeated drain may duplicate stable event IDs/metadata, but cannot delete recovery state before confirmed success.

- [ ] **ST3 - Clarify dead-letter policy boundaries.** (AC: 4, 5)
    - [ ] Document the difference between EventStore command infrastructure dead letters from `DeadLetterPublisher` and DAPR subscriber dead-letter topics after delivery retry exhaustion.
    - [ ] Confirm dead-letter topic naming: `{DeadLetterTopicPrefix}.{tenant}.{domain}.events` for EventStore dead letters and backend component `deadLetterTopic` for DAPR subscriber routing.
    - [ ] Add or update tests for dead-letter message metadata, trace correlation, sanitized error fields, and topic naming.
    - [ ] Record manual recovery expectations for drain records that cannot be retried automatically due to missing persisted events or event-count mismatch.

- [ ] **ST4 - Build the backend deployment matrix.** (AC: 3, 5, 6)
    - [ ] Update docs with a matrix for Redis, RabbitMQ, Kafka, and Azure Service Bus covering durability, duplicate delivery, ordering/session/partition support, topic creation, dead-letter support, retry/resiliency knobs, scoping requirements, and known limitations.
    - [ ] Make the matrix explicit about what DAPR guarantees, what the backend guarantees, and what EventStore guarantees above DAPR.
    - [ ] Use matrix columns equivalent to `Backend`, `Required DAPR settings`, `Ordering boundary`, `Routing/session/partition mechanism`, `At-least-once mechanism`, `Dead-letter capability`, `Resiliency config location`, `Proof level`, and `Known caveats`.
    - [ ] Verify production component examples do not imply wildcard scoping, default-open subscriber permissions, dev-only test subscribers, or hardcoded secrets.
    - [ ] Add deployment notes for Azure Service Bus topic pre-creation and session support if session ordering is claimed.
    - [ ] Add deployment notes for Kafka partition key behavior if per-aggregate ordering is claimed.

- [ ] **ST5 - Expand backend-specific tests and evidence.** (AC: 1, 3, 4, 6)
    - [ ] Preserve existing Redis local `PubSubDeliveryProofTests` evidence and extend it only when the local Aspire topology supports the required proof.
    - [ ] Add focused integration or documented manual proof paths for RabbitMQ, Kafka, and Azure Service Bus. If CI cannot run them, record exact commands, environment prerequisites, and deferred automation status.
    - [ ] Prove publish-after-persist recovery with a deterministic failure trigger and drain recovery for every backend claimed as supported.
    - [ ] Prove duplicate tolerance by replaying/draining a previously published event or otherwise forcing an at-least-once duplicate path.
    - [ ] Prove per-aggregate causal ordering where the backend supports it; explicitly mark unsupported or unverified backends in docs and Verification Status.
    - [ ] Separate proof boundaries: unit tests for persistence/publish/drain state, component/config inspection for backend settings, Docker/Aspire integration only when infrastructure is available, and manual evidence only for environment-gated backends.
    - [ ] Include explicit negative assertions for no exactly-once claims, no payload/secret logging, no ad hoc application retry loops replacing DAPR resiliency config, and no changes to payload protection, query contracts, or replay APIs.

- [ ] **ST6 - Align public docs and generated references.** (AC: 2, 5, 7)
    - [ ] Update `docs/concepts/command-lifecycle.md` with at-least-once, duplicate tolerance, partial publish, and drain semantics.
    - [ ] Update `docs/concepts/event-envelope.md` with consumer idempotency and ordering guidance for pub/sub subscribers.
    - [ ] Update `docs/guides/configuration-reference.md` and DAPR component reference docs with backend matrix and settings.
    - [ ] Update `docs/operations/drain-failure-reason-codes.md` if any reason code or operator action changes.
    - [ ] Update generated API docs for `EventPublisherOptions`, `EventDrainOptions`, `EventPublishResult`, `UnpublishedEventsRecord`, or public Testing fakes if public XML docs change.

- [ ] **ST7 - Validate and record evidence.** (AC: 7)
    - [ ] Run focused Server tests for publisher/drain/dead-letter behavior, starting with `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "EventPublisher|EventDrain|PersistThenPublish|DeadLetter|UnpublishedEvents|DrainReason"`.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "PubSubDeliveryProof|DeadLetter"` only with Docker and a running Aspire/DAPR environment.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests` if Testing fakes/builders change.
    - [ ] Run docs/markdown validation where available.
    - [ ] Update Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Architecture and product guardrails:

- FR96-FR98 are controlling: publish durability, at-least-once delivery, per-aggregate causal order where backend support exists, backend deployment settings, and backend-specific proof.
- ADR-P9 is adjacent: downstream publishing, replay, and payload-protection behavior are platform contracts, not Parties-specific implementation details.
- Stories 22.1-22.4 own public gateway DTO/client/testing, projection adapter, tenant/RBAC, and query policy. Do not reopen those boundaries except for docs cross-links.
- Story 22.6 owns stream read/replay APIs and projection rebuild checkpoints. Do not implement replay APIs as a workaround for publication proof.
- Stories 22.7a-22.7d own payload/snapshot protection, unreadable protected data, crypto-shredding, and redaction. This story must preserve redaction and defer protection-policy gaps.
- DAPR resiliency belongs in DAPR/Aspire/resilience configuration, not ad hoc retry loops in `EventPublisher`.

Implementation traps to avoid:

- Do not claim exactly-once delivery. EventStore provides at-least-once delivery with consumer idempotency guidance.
- Do not claim ordered delivery for a backend without configured partition/session/order metadata and proof.
- Do not treat sequence-number order in persisted state as proof that a subscriber received messages in the same order.
- Do not delete drain records on ambiguous publish or state-store failures.
- Do not collapse EventStore dead-letter publication and DAPR subscriber dead-letter routing into one operational concept.
- Do not leave dev-only `eventstore-test-subscriber` grants in production-bound documentation as if they are real subscriber defaults.
- Do not expose payload bytes, protected data, connection strings, DAPR addresses, state-store keys, tokens, or subscriber credentials in logs, docs examples, ProblemDetails, or test artifacts.
- Do not run broad solution-level tests first. Use focused publisher/drain/dead-letter slices; integration tests require Docker and a running Aspire/DAPR environment.
- Do not let the backend matrix become marketing language. Every claim needs a proof level or an explicit caveat.
- Do not treat successful DAPR publish as proof of subscriber processing success.
- Do not remove or compact unpublished-event recovery state before the publish outcome and cleanup path are explicitly proven.

File-level guardrails:

- Expected touch points are publisher/drain/dead-letter runtime, unpublished-event record/state handling, DAPR pub/sub component files, deployment/configuration docs, and focused publisher/drain/dead-letter tests.
- Excluded unless a narrow clarification is required: payload protection policy from Stories 22.7a-22.7d, command/query/projection adapter contracts from Stories 22.1-22.4, stream replay/read APIs from Story 22.6, broad Admin UI behavior, and unrelated AppHost topology changes.

Current file intelligence:

- Publisher and drain runtime:
    - `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
    - `src/Hexalith.EventStore.Server/Events/EventPublishResult.cs`
    - `src/Hexalith.EventStore.Server/Events/IEventPublisher.cs`
    - `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
    - `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs`
    - `src/Hexalith.EventStore.Server/Actors/DrainReasonCodes.cs`
    - `src/Hexalith.EventStore.Server/Actors/DrainPublishException.cs`
    - `src/Hexalith.EventStore.Server/Actors/DrainStateStoreException.cs`
    - `src/Hexalith.EventStore.Server/Actors/DrainEventCountMismatchException.cs`
- Dead-letter runtime:
    - `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs`
    - `src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs`
    - `src/Hexalith.EventStore.Server/Events/IDeadLetterPublisher.cs`
- Configuration and deployment:
    - `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs`
    - `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs`
    - `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`
    - `deploy/dapr/pubsub-rabbitmq.yaml`
    - `deploy/dapr/pubsub-kafka.yaml`
    - `deploy/dapr/pubsub-servicebus.yaml`
- Docs:
    - `docs/concepts/command-lifecycle.md`
    - `docs/concepts/event-envelope.md`
    - `docs/concepts/identity-scheme.md`
    - `docs/guides/configuration-reference.md`
    - `docs/guides/dapr-faq.md`
    - `docs/operations/drain-failure-reason-codes.md`
    - `docs/guides/deployment-progression.md`
    - `docs/guides/deployment-azure-container-apps.md`
- Focused tests:
    - `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherRetryComplianceTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterPublisherTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/UnpublishedEventsRecordTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/DeadLetterRoutingTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/Dw8DrainReasonClassifierTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Security/PubSubTopicIsolationEnforcementTests.cs`
    - `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs`
    - `tests/Hexalith.EventStore.IntegrationTests/ContractTests/DeadLetterTests.cs`

Testing standards:

- Use xUnit v3, Shouldly, and NSubstitute where existing test projects already use them.
- Run test projects individually per repository guidance.
- Prefer unit tests for publisher/drain decisions before integration tests.
- Integration tests require Docker, DAPR placement/scheduler, and Aspire resources. Use `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` when following the repository's cloud VM path.
- `Hexalith.EventStore.Server.Tests` has known pre-existing CA2007 warning-as-error risk in broad runs. Use focused filters and record unrelated blockers exactly.

## Files Likely Touched

- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublishResult.cs`
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs`
- `src/Hexalith.EventStore.Server/Actors/DrainReasonCodes.cs`
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs`
- `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs`
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`
- `deploy/dapr/pubsub-rabbitmq.yaml`
- `deploy/dapr/pubsub-kafka.yaml`
- `deploy/dapr/pubsub-servicebus.yaml`
- `docs/concepts/command-lifecycle.md`
- `docs/concepts/event-envelope.md`
- `docs/guides/configuration-reference.md`
- `docs/operations/drain-failure-reason-codes.md`
- `docs/reference/api/**` only if generated public API docs are refreshed.
- Related focused test files under `tests/Hexalith.EventStore.Server.Tests` and `tests/Hexalith.EventStore.IntegrationTests`.

## Out of Scope

- Moving public command/query DTOs, gateway client behavior, or gateway fakes; Story 22.1 owns that.
- Moving projection adapter contracts, actor serialization, or query routing model; Story 22.2 owns that.
- Tenant lifecycle, membership, role, and permission validation; Story 22.3 owns that.
- Query paging, filtering, stale/degraded metadata, and query ProblemDetails taxonomy; Story 22.4 owns that.
- Stream read/replay APIs and operator-safe projection rebuild checkpoints; Story 22.6 owns that.
- Payload/snapshot protection hooks, unreadable protected data behavior, crypto-shredding, and cross-surface redaction policy; Stories 22.7a-22.7d own that.
- Broad Admin UI, CLI, MCP, Parties repository, or Hexalith.Tenants behavior changes.

## References

- `_bmad-output/planning-artifacts/epics.md#Story 22.5: Event Publishing Guarantees and Backend Deployment Matrix`
- `_bmad-output/planning-artifacts/prd.md#Public Gateway and Downstream Integration Contracts - v1.1 (FR83-FR104)`
- `_bmad-output/planning-artifacts/architecture.md#Publishing, Replay & Protection Contracts`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-parties-integration-contract-gaps.md`
- `_bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md`
- `_bmad-output/implementation-artifacts/22-2-projection-adapter-contract-and-generic-query-actor-model.md`
- `_bmad-output/implementation-artifacts/22-3-gateway-owned-tenant-and-rbac-enforcement.md`
- `_bmad-output/implementation-artifacts/22-4-query-behavior-policy-and-error-taxonomy.md`
- `_bmad-output/project-context.md`
- `_bmad-output/process-notes/story-creation-lessons.md#L08 - Party Review Vs. Elicitation`
- `docs/concepts/command-lifecycle.md`
- `docs/concepts/event-envelope.md`
- `docs/guides/configuration-reference.md`
- `docs/operations/drain-failure-reason-codes.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-12T21:02:38Z - Pre-dev hardening preflight passed via `_bmad-output/process-notes/predev-preflight-latest.json`.
- 2026-05-12T23:04:13+02:00 - Story creation context gathered from Epic 22, PRD FR96-FR98, architecture publishing/replay/protection notes, Stories 22.1-22.4, current publisher/drain/dead-letter implementation, local and production DAPR pub/sub component files, focused tests, project context, recent commits, and lessons ledger.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- Party-mode review completed on 2026-05-13 and applied story hardening for guarantee boundaries, backend proof levels, unpublished-record lifecycle, drain idempotency, dead-letter separation, operator metadata, and file-level guardrails.
- Advanced elicitation has NOT yet been run for this story.

### File List

- `_bmad-output/implementation-artifacts/22-5-event-publishing-guarantees-and-backend-deployment-matrix.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- YAML validation passed for `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- `git diff --check` passed for the story artifact, sprint status, and run log with line-ending conversion warnings only.
- `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-5-event-publishing-guarantees-and-backend-deployment-matrix.md` passed with 0 errors.
- Party-mode review completed on 2026-05-13 and is recorded below.
- Advanced elicitation has NOT yet been run for this story.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-13 | 0.2 | Applied party-mode review hardening for guarantee boundaries, backend proof levels, unpublished-record lifecycle, drain idempotency, and dead-letter separation. | Codex automation |
| 2026-05-12 | 0.1 | Created ready-for-dev story for event publishing guarantees and backend deployment matrix. | Codex automation |

## Party-Mode Review

- Date/time: 2026-05-13T09:06:14+02:00
- Selected story key: `22-5-event-publishing-guarantees-and-backend-deployment-matrix`
- Command/skill invocation used:
  `/bmad-party-mode 22-5-event-publishing-guarantees-and-backend-deployment-matrix; review;`
- Participating BMAD agents: John (Product Manager), Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
    - The story risked overclaiming uniform DAPR/backend guarantees across Redis, RabbitMQ, Kafka, and Azure Service Bus.
    - Durable, at-least-once, and per-aggregate ordering needed explicit EventStore-vs-backend-vs-subscriber boundaries.
    - `UnpublishedEventsRecord` lifecycle, partial publish, reminder overlap, crash/restart, and duplicate-publication semantics needed sharper pre-dev guardrails.
    - EventStore infrastructure dead letters and DAPR subscriber dead-letter routing needed separate proof and operational language.
    - Backend matrix rows needed proof levels, caveats, and operator-visible metadata requirements instead of broad "supported" claims.
- Changes applied:
    - Added `Guarantee Boundaries` defining durable persistence, at-least-once behavior, ordering scope, backend claim labels, and metadata-only diagnostics.
    - Tightened AC1, AC3, AC4, and AC6 around commit durability, backend matrix columns, drain idempotency, operator diagnostics, and proof classification.
    - Expanded ST2, ST4, and ST5 with unpublished-record lifecycle, partial-publish recovery, crash/restart duplication tolerance, backend matrix columns, proof boundaries, and negative assertions.
    - Added Developer Notes for backend-matrix honesty, subscriber-processing boundaries, unpublished-state cleanup safety, and file-level guardrails.
    - Updated Completion Notes, Verification Status, and Change Log with this dated party-mode review.
- Findings deferred:
    - Whether operators need a manual requeue/repair command remains a future product/operations decision unless already covered by existing drain tooling.
    - Whether all four backends must run in CI remains deferred to infrastructure availability; documentation-backed or manual proof must be labeled honestly.
    - Whether Azure Service Bus sessions become a required production recommendation and whether Kafka repartitioning constraints need a hard policy remain architecture decisions.
    - Payload protection, query contract, replay/read API, and broad UI/Admin behavior remain owned by their separate Epic 22 stories.
- Final recommendation: ready-for-dev after applied story updates.
