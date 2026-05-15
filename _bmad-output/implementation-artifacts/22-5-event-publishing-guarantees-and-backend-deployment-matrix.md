# Story 22.5: Event Publishing Guarantees and Backend Deployment Matrix

Status: done

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
- DAPR publish acceptance is not subscriber success. EventStore may prove that a persisted event was accepted by DAPR or retained for retry, but subscriber processing, subscriber dead-letter routing, and projection convergence require separate proof and separate evidence labels.
- Partial-batch publication must be conservative: if any event in a persisted batch has an ambiguous or failed publish outcome, the recovery path must assume previously accepted events can be duplicated and later persisted events must not be skipped or reordered.
- Backend support must be explicit for local Redis, RabbitMQ, Kafka, and Azure Service Bus deployment files. Unsupported ordering/session/dead-letter features must be called out rather than implied.
- Dead-letter behavior has two distinct meanings: EventStore infrastructure dead-letter messages for command processing failures, and DAPR subscriber dead-letter handling after delivery retry exhaustion. Keep both documented and tested separately.
- This story may add metadata or docs for ordering/session keys, but must not reopen payload-protection semantics owned by Stories 22.7a-22.7d or replay/read APIs owned by Story 22.6.

## Guarantee Boundaries

- Durable means EventStore event persistence is the source of truth: events are written before the first publish attempt, and failed publication is represented by recoverable unpublished-event state or a stable infrastructure failure path.
- At-least-once means persisted unpublished events are retried until DAPR accepts publication, a bounded drain record remains, or EventStore records an explicit infrastructure failure/dead-letter state. It does not mean exactly-once delivery or successful subscriber processing.
- Per-aggregate ordering means EventStore publishes and drains events in persisted aggregate sequence order, and sets or documents backend-specific routing/session/partition metadata where supported. Transport delivery and subscriber processing order remain backend-dependent.
- Backend claims must be labeled as proven, configured, unsupported, or documented-only. Do not use "supported" for Redis, RabbitMQ, Kafka, or Azure Service Bus behavior without a matching proof level.
- A backend row is not implementation-complete until it identifies the exact EventStore guarantee boundary, the DAPR/backend guarantee boundary, the subscriber responsibility boundary, and the evidence that supports each boundary.
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
   - And an ambiguous DAPR publish result must be treated as retryable or operationally failed until EventStore can prove accepted publication or retained recovery state.

2. **Duplicate tolerance and consumer idempotency are public guidance.**
   - Given a downstream projection receives the same persisted event more than once
   - When it processes published CloudEvents
   - Then docs and tests show the stable idempotency fields consumers should use: tenant, domain, aggregate, sequence number, correlation ID, causation ID, message ID, event type, and topic.
   - And EventStore does not promise subscriber-side exactly-once processing or hide backend duplicate delivery realities.
   - And guidance must state that consumers should deduplicate by EventStore identity/sequence metadata, not by broker delivery IDs, subscriber retry counts, or transport-specific receipt handles.

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
   - And non-retryable drain failure, event-count mismatch, missing persisted event, or corrupted stream evidence must preserve enough metadata for manual repair while avoiding payload disclosure.

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
   - And any backend row that lacks live proof must be labeled `configured` or `documented-only`, not `proven`, even when the DAPR component file appears syntactically correct.

7. **Documentation and evidence are recorded before review.**
   - Reference docs, deployment docs, configuration docs, and operations docs describe the guarantee matrix and operational recovery path.
   - Focused Server publisher/drain tests and any applicable integration tests pass individually.
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [x] **ST0 - Baseline current publishing guarantees and classify gaps.** (AC: 1, 2, 3, 4, 5, 6)
    - [x] Read this story, Epic 22, PRD FR96-FR98, architecture publishing/replay/protection contract notes, Stories 22.1-22.4, and `docs/concepts/command-lifecycle.md` before code edits.
    - [x] Inventory `EventPublisher`, `EventPublishResult`, `AggregateActor` publish/drain paths, `UnpublishedEventsRecord`, drain exceptions, `DrainReasonCodes`, `DeadLetterPublisher`, `EventPublisherOptions`, and `EventDrainOptions`.
    - [x] Inventory local and deployment pub/sub components: `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`, `deploy/dapr/pubsub-rabbitmq.yaml`, `deploy/dapr/pubsub-kafka.yaml`, and `deploy/dapr/pubsub-servicebus.yaml`.
    - [x] Inventory existing docs and tests for command lifecycle, event envelope, configuration, DAPR component reference, drain failure reason codes, PubSubDeliveryProofTests, DeadLetterTests, and publisher/drain unit tests.
    - [x] Record a decision table for durability, at-least-once, duplicate tolerance, ordering/session metadata, partial publish handling, drain retry, dead-letter routing, and backend proof status.
    - [x] In the decision table, separate four proof boundaries: EventStore persisted-event guarantee, DAPR publish-acceptance guarantee, broker delivery behavior, and subscriber processing/dead-letter behavior.

- [x] **ST1 - Freeze publish contract metadata.** (AC: 1, 2, 3)
    - [x] Define the public fields downstream consumers use for idempotency and ordering. Prefer existing envelope fields unless a backend-specific DAPR metadata field is required.
    - [x] Decide whether EventStore should set explicit DAPR metadata for ordering/session/partition keys, such as aggregate identity or tenant/domain/aggregate composite, and document why for each backend.
    - [x] Preserve current CloudEvents 1.0 behavior and `cloudevent.id` compatibility unless a SemVer-relevant change is explicitly approved.
    - [x] Add focused tests that pin CloudEvent metadata and event data fields without requiring a live sidecar where unit-level fakes can prove it.
    - [x] Ensure logs and activity tags continue to avoid event payload, protected payload, secrets, subscriber credentials, and backend connection strings.
    - [x] Record whether ordering/session/partition metadata is emitted as DAPR metadata, CloudEvent extension metadata, event payload metadata, documentation-only guidance, or a deferred backend-specific decision.

- [x] **ST2 - Harden publish failure and drain recovery semantics.** (AC: 1, 4)
    - [x] Verify partial publish behavior: if some events publish before failure, the drain path remains at-least-once and duplicate-tolerant without losing unpublished persisted events.
    - [x] Verify `UnpublishedEventsRecord.EventCount` matches the persisted sequence range and mismatch handling keeps a stable failure reason instead of silently deleting recovery state.
    - [x] Verify missing event, state-store failure, DAPR unavailable, publish failed, and unknown drain errors map to documented `DrainReasonCodes`.
    - [x] Verify drain success removes the record only after successful publish and unregisters reminders without hiding unregister failures.
    - [x] Verify `pending_command_count` is not decremented prematurely on publish failure and remains bounded/clean after terminal or drain-success paths according to existing backpressure semantics.
    - [x] Record the unpublished-record lifecycle decision before code completion: pending/publishing remains retryable on retryable failure; published/removed only after confirmed DAPR publish success; failed/dead-lettered only after configured retry exhaustion or stable non-retryable failure.
    - [x] Prove partial publish behavior: if events 1..N publish and event N+1 fails, the next drain resumes without skipping, losing, or publishing later aggregate events out of persisted sequence.
    - [x] Prove crash/restart and reminder-overlap behavior: repeated drain may duplicate stable event IDs/metadata, but cannot delete recovery state before confirmed success.
    - [x] Prove ambiguous publish outcomes are handled fail-safe: no successful command completion may depend solely on a partially observed broker/DAPR response when persisted events still need recovery tracking.

- [x] **ST3 - Clarify dead-letter policy boundaries.** (AC: 4, 5)
    - [x] Document the difference between EventStore command infrastructure dead letters from `DeadLetterPublisher` and DAPR subscriber dead-letter topics after delivery retry exhaustion.
    - [x] Confirm dead-letter topic naming: `{DeadLetterTopicPrefix}.{tenant}.{domain}.events` for EventStore dead letters and backend component `deadLetterTopic` for DAPR subscriber routing.
    - [x] Add or update tests for dead-letter message metadata, trace correlation, sanitized error fields, and topic naming.
    - [x] Record manual recovery expectations for drain records that cannot be retried automatically due to missing persisted events or event-count mismatch.
    - [x] Ensure docs and tests name which failures go to EventStore infrastructure dead-letter records and which failures are only DAPR subscriber delivery outcomes.

- [x] **ST4 - Build the backend deployment matrix.** (AC: 3, 5, 6)
    - [x] Update docs with a matrix for Redis, RabbitMQ, Kafka, and Azure Service Bus covering durability, duplicate delivery, ordering/session/partition support, topic creation, dead-letter support, retry/resiliency knobs, scoping requirements, and known limitations.
    - [x] Make the matrix explicit about what DAPR guarantees, what the backend guarantees, and what EventStore guarantees above DAPR.
    - [x] Use matrix columns equivalent to `Backend`, `Required DAPR settings`, `Ordering boundary`, `Routing/session/partition mechanism`, `At-least-once mechanism`, `Dead-letter capability`, `Resiliency config location`, `Proof level`, and `Known caveats`.
    - [x] Verify production component examples do not imply wildcard scoping, default-open subscriber permissions, dev-only test subscribers, or hardcoded secrets.
    - [x] Add deployment notes for Azure Service Bus topic pre-creation and session support if session ordering is claimed.
    - [x] Add deployment notes for Kafka partition key behavior if per-aggregate ordering is claimed.
    - [x] Add a "claim status" column with values equivalent to `proven`, `configured`, `documented-only`, `unsupported`, and `not proven`; do not infer live proof from component YAML alone.

- [x] **ST5 - Expand backend-specific tests and evidence.** (AC: 1, 3, 4, 6)
    - [x] Preserve existing Redis local `PubSubDeliveryProofTests` evidence and extend it only when the local Aspire topology supports the required proof.
    - [x] Add focused integration or documented manual proof paths for RabbitMQ, Kafka, and Azure Service Bus. If CI cannot run them, record exact commands, environment prerequisites, and deferred automation status.
    - [x] Prove publish-after-persist recovery with a deterministic failure trigger and drain recovery for every backend claimed as supported.
    - [x] Prove duplicate tolerance by replaying/draining a previously published event or otherwise forcing an at-least-once duplicate path.
    - [x] Prove per-aggregate causal ordering where the backend supports it; explicitly mark unsupported or unverified backends in docs and Verification Status.
    - [x] Separate proof boundaries: unit tests for persistence/publish/drain state, component/config inspection for backend settings, Docker/Aspire integration only when infrastructure is available, and manual evidence only for environment-gated backends.
    - [x] Include explicit negative assertions for no exactly-once claims, no payload/secret logging, no ad hoc application retry loops replacing DAPR resiliency config, and no changes to payload protection, query contracts, or replay APIs.
    - [x] Include a deterministic proof that subscriber success is not required for EventStore to clear publish recovery state unless the implementation explicitly waits for subscriber acknowledgements, which current DAPR pub/sub publishing does not provide.

- [x] **ST6 - Align public docs and generated references.** (AC: 2, 5, 7)
    - [x] Update `docs/concepts/command-lifecycle.md` with at-least-once, duplicate tolerance, partial publish, and drain semantics.
    - [x] Update `docs/concepts/event-envelope.md` with consumer idempotency and ordering guidance for pub/sub subscribers.
    - [x] Update `docs/guides/configuration-reference.md` and DAPR component reference docs with backend matrix and settings.
    - [x] Update `docs/operations/drain-failure-reason-codes.md` if any reason code or operator action changes.
    - [x] Update generated API docs for `EventPublisherOptions`, `EventDrainOptions`, `EventPublishResult`, `UnpublishedEventsRecord`, or public Testing fakes if public XML docs change.

- [x] **ST7 - Validate and record evidence.** (AC: 7)
    - [x] Run focused Server tests for publisher/drain/dead-letter behavior, starting with `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "EventPublisher|EventDrain|PersistThenPublish|DeadLetter|UnpublishedEvents|DrainReason"`.
    - [x] Run `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "PubSubDeliveryProof|DeadLetter"` only with Docker and a running Aspire/DAPR environment.
    - [x] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests` if Testing fakes/builders change.
    - [x] Run docs/markdown validation where available.
    - [x] Update Dev Agent Record, File List, Verification Status, and Change Log.

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
- Do not mark RabbitMQ, Kafka, or Azure Service Bus rows as `proven` without live backend evidence or an explicitly attached manual proof artifact.
- Do not use broker-specific receipt IDs, offsets, partitions, lock tokens, sessions, or delivery counts as the primary cross-backend idempotency contract for downstream consumers.

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
- 2026-05-14T12:03:49+02:00 - ST0 implementation start: story and sprint status moved to in-progress, controlling Epic 22/FR96-FR98/ADR-P9 context loaded, and publisher/drain/dead-letter/config/docs/test inventory completed before product code edits.

### Implementation Plan

- Work story tasks in order. ST0 records the baseline and proof boundaries; ST1 will pin public event metadata and decide whether backend-neutral routing metadata should be emitted or documented only; ST2 will harden partial publish and drain semantics with focused tests before runtime changes; ST3-ST6 will align dead-letter boundaries, backend matrices, evidence paths, and public docs; ST7 will run focused validation and record evidence.

### ST0 Publishing Guarantees Decision Table

| Concern | EventStore persisted-event guarantee | DAPR publish-acceptance guarantee | Broker delivery behavior | Subscriber processing/dead-letter behavior | Baseline decision / gap |
| --- | --- | --- | --- | --- | --- |
| Durability | `AggregateActor` persists events before `EventPublisher` runs; `PublishFailed` stores `UnpublishedEventsRecord` after failed publish. | `DaprClient.PublishEventAsync` success means DAPR accepted the publish call only. | Backend durability varies: Redis depends on Redis persistence, RabbitMQ durable queues, Kafka retention, Service Bus native durability. | Subscriber success is not observed by EventStore. | Document durable as EventStore commit plus drain state, not uniform broker durability. |
| At-least-once | Drain reads persisted sequence range and republishes until success or stable failure state remains. | Ambiguous/failed publish returns `EventPublishResult(false, PublishedCount, reason)` or propagates cancellation. | Delivery may duplicate depending on backend/retry. | Consumers must deduplicate using EventStore metadata. | Keep full-range drain retry; do not claim exactly-once. |
| Duplicate tolerance | Partial publish followed by drain can duplicate already accepted events with same stable CloudEvent IDs and envelope metadata. | `PublishedCount` records observed success count but does not prove subscriber receipt. | Broker redelivery is normal. | Consumers own idempotent projection handling. | Docs/tests need explicit duplicate guidance by tenant/domain/aggregate/sequence/correlation/causation/message/event type/topic. |
| Ordering/session metadata | Persisted `sequenceNumber` is gapless per aggregate and drain loads ranges ordered by sequence. | Current publish metadata sets `cloudevent.type`, `cloudevent.source`, and `cloudevent.id`; no DAPR partition/session key is emitted. | Redis/RabbitMQ/Kafka/Service Bus ordering claims require backend-specific configuration and proof. | Subscriber processing order is not guaranteed by EventStore. | ST1/ST4 must decide and document routing/session/partition metadata per backend. |
| Partial publish handling | Failed batch records the whole persisted sequence range, so retry is conservative and duplicate-tolerant. | `PublishedCount` exposes partial acceptance evidence only. | Broker may have accepted events before the failure. | Subscriber may see duplicates after drain. | ST2 needs focused proof that later events are not skipped and recovery state is retained. |
| Drain retry | `UnpublishedEventsRecord` tracks correlation, sequence range, count, command type, rejection flag, retry count, and last failure. | Failed drain publish updates retry state; successful publish removes record after save. | DAPR resiliency handles sidecar/broker retries; app has no custom retry loop in publisher. | Subscriber retry/dead-letter is separate from EventStore drain. | Existing reason codes are stable; docs need operator-safe manual repair language. |
| Dead-letter routing | `DeadLetterPublisher` handles command infrastructure failures with `{prefix}.{tenant}.{domain}.events`. | DAPR component `deadLetterTopic` covers subscriber delivery exhaustion. | Backend DLQ/DLX/topic capabilities differ. | Subscriber dead-letter outcome is not EventStore command dead-letter outcome. | ST3 must keep both concepts separate in docs/tests. |
| Backend proof status | Unit tests prove publisher/drain decisions without live sidecar. | Redis local integration proof exists through `PubSubDeliveryProofTests`. | RabbitMQ/Kafka/Service Bus component YAML exists but is config evidence only. | `DeadLetterTests` and subscriber tests are environment-gated. | Matrix rows must use `proven`, `configured`, `documented-only`, `unsupported`, or `not proven`; no live proof inferred from YAML. |

### ST1 Publish Metadata Decision

| Contract surface | Decision | Consumer guidance / backend impact | Evidence |
| --- | --- | --- | --- |
| Public idempotency fields | Use the flat event envelope fields: `tenantId`, `domain`, `aggregateId`, `sequenceNumber`, `correlationId`, `causationId`, `messageId`, `eventTypeName`, and topic. | Consumers deduplicate by EventStore identity and sequence metadata, not broker receipt IDs, retry counts, offsets, lock tokens, or delivery counts. | `PublishEventsAsync_SingleEvent_PublishesStableIdempotencyAndOrderingFields`. |
| CloudEvents metadata | Preserve existing CloudEvents keys only: `cloudevent.type`, `cloudevent.source`, and `cloudevent.id` with `{correlationId}:{sequenceNumber}`. | Maintains current CloudEvents 1.0 compatibility and subscriber behavior. | Existing `EventPublisherTests` plus new ST1 test. |
| DAPR raw payload | Do not emit `rawPayload`; keep DAPR CloudEvents wrapping enabled for tracing and message identity behavior. | Subscribers continue receiving CloudEvents with the flat envelope as data. | ST1 test asserts no `rawPayload` metadata. |
| Kafka partition metadata | Do not emit `partitionKey` or `__key` yet. | Kafka per-aggregate partition guidance is documentation/configuration evidence until a live backend proof validates the policy. | DAPR docs confirm Kafka supports per-call `partitionKey`/`__key`; ST1 defers emission. |
| Azure Service Bus session/partition metadata | Do not emit `SessionId` or `PartitionKey` yet. | Service Bus session ordering can be documented as configured/manual proof only until topics/subscriptions are session-enabled and tested. | DAPR docs confirm Service Bus supports per-call `SessionId`/`PartitionKey`; ST1 defers emission. |
| RabbitMQ/Redis ordering metadata | No backend-neutral ordering metadata emitted. | EventStore sequence order is guaranteed at persist/publish call order; broker/subscriber delivery order remains backend-dependent. | ST1 test pins absence of backend-specific DAPR metadata. |

### ST2 Unpublished Record Lifecycle Decision

| State | Decision | Recovery / cleanup rule | Evidence |
| --- | --- | --- | --- |
| Initial publish failed before or during batch | Store an `UnpublishedEventsRecord` for the full persisted sequence range, even when `EventPublishResult.PublishedCount` proves some earlier events were accepted. | The next drain republishes the complete range in persisted sequence order. Duplicate publication is allowed; skipping later persisted events is not. | `ProcessCommand_PartialPublishFailed_RecordContainsFullPersistedSequenceRange`; `ReceiveReminder_PartialPublishRecovery_RePublishesCompleteRecordedRangeInOrder`. |
| Drain retry failed | Increment retry count and preserve the record with a bounded failure reason. | Reminder remains registered; no pending-count decrement or record deletion happens on failed drain. | Existing drain failure tests and reason-code tests. |
| Drain integrity failure | Preserve the record and classify with a stable `DrainReasonCodes` value. | Missing events and event-count mismatch require manual repair; automatic retry alone is not expected to fix corrupted evidence. | `ReceiveReminder_DrainRangeMissingPersistedEvent_PreservesRecordAndDoesNotPublish`; `ReceiveReminder_DrainRecordEventCountMismatch_PreservesRecordAndDoesNotPublish`; `Dw8DrainReasonClassifierTests`. |
| Drain publish success | Remove the record only after publish succeeds and state is saved; unregister reminder afterward and log unregister failure without rolling back success. | Command status moves to `Completed` or `Rejected` based on the original record. | Existing drain success/reminder/status tests. |
| Crash/restart or reminder overlap | Retry remains duplicate-tolerant because the record holds the persisted sequence range and the publisher emits stable envelope and CloudEvent IDs. | Repeated drain may duplicate but cannot delete recovery state before confirmed publish success. | Existing multiple-drain and recorded-range tests plus ST1 metadata test. |

### ST3 Dead-Letter Boundary Decision

| Surface | Owner | Topic / metadata | What it proves | What it does not prove |
| --- | --- | --- | --- | --- |
| EventStore command infrastructure dead letter | EventStore application via `DeadLetterPublisher` | `{DeadLetterTopicPrefix}.{tenant}.{domain}.events`; CloudEvent type `deadletter.command.failed` | Command processing hit an infrastructure failure before a durable publish/drain path existed. | Subscriber delivery failed, broker exhausted subscriber retries, or a persisted event was lost. |
| DAPR subscriber dead letter | DAPR pub/sub runtime plus subscription/component config | Component `deadLetterTopic` and subscription `deadLetterTopic` values, usually `deadletter...` topics | Subscriber processing failed after delivery retry exhaustion. | EventStore command processing failed or EventStore should retain a drain record after DAPR accepted publication. |
| Drain record manual repair | EventStore actor state | `UnpublishedEventsRecord` metadata: correlation ID, sequence range, event count, command type, retry count, last failure reason | Persisted events still need confirmed publish acceptance or operator repair. | Payload visibility, subscriber credentials, or subscriber delivery outcome. |

### ST5 Evidence Classification

| Proof boundary | Automated evidence in this workspace | Environment-gated / manual evidence | Status |
| --- | --- | --- | --- |
| EventStore persist-first and drain recovery | `PersistThenPublishResilienceTests`, `EventDrainRecoveryTests`, `Dw8DrainReasonClassifierTests` | N/A | `unit` proven. |
| CloudEvent and envelope idempotency fields | `EventPublisherTests` | N/A | `unit` proven. |
| Local Redis publish/drain recovery | Existing `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs` requires Docker/Aspire/DAPR. | Run only with the Aspire environment active. | `Docker/Aspire integration` proof exists but was not run in this unit-test-only pass. |
| RabbitMQ backend behavior | `ProductionDaprComponentValidationTests` and `PubSubTopicIsolationEnforcementTests` inspect YAML/scoping/dead-letter config. | Manual proof path documented in `docs/guides/dapr-component-reference.md`. | `configured`, live behavior not proven. |
| Kafka backend behavior | YAML/scoping/dead-letter config inspection only. | Manual partition-key/order proof path documented; EventStore does not emit `partitionKey`/`__key` yet. | `configured`; partition ordering `not proven`. |
| Azure Service Bus backend behavior | YAML/scoping/dead-letter config inspection only. | Manual session/DLQ proof path documented; EventStore does not emit `SessionId` yet. | `configured`; session ordering `not proven`. |
| Negative claims | Docs/tests assert no exactly-once claim from backend table, no payload/secret logging, no custom publisher retry loops, and no subscriber-success dependency for publish recovery cleanup. | N/A | Guarded by focused tests and documentation language. |

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- Party-mode review completed on 2026-05-13 and applied story hardening for guarantee boundaries, backend proof levels, unpublished-record lifecycle, drain idempotency, dead-letter separation, operator metadata, and file-level guardrails.
- Advanced elicitation completed on 2026-05-13 and applied bounded story hardening for DAPR publish-acceptance boundaries, partial-batch ambiguity, backend proof labels, consumer idempotency fields, and subscriber-dead-letter separation.
- 2026-05-14 ST0 completed: baseline inventory confirmed persist-first plus drain recovery as the EventStore durability boundary, current CloudEvent metadata compatibility, no emitted backend-neutral partition/session metadata, conservative full-range drain after partial publish, separated EventStore infrastructure dead letters from DAPR subscriber dead-letter routing, and classified RabbitMQ/Kafka/Service Bus evidence as configured/documented rather than proven.
- 2026-05-14 ST1 completed: pinned the cross-backend publish contract to the existing flat envelope fields plus stable CloudEvents metadata, deliberately avoided new DAPR `rawPayload`, Kafka partition, and Service Bus session/partition metadata until backend-specific proof exists, and added focused unit coverage for the public idempotency/ordering surface.
- 2026-05-14 ST2 completed: proved partial publish fail-safe behavior stores and drains the complete persisted sequence range, confirmed drain failure records remain retryable and bounded by stable reason codes, and recorded the unpublished-record lifecycle decision.
- 2026-05-14 ST3 completed: documented EventStore infrastructure dead letters versus DAPR subscriber dead letters, tightened dead-letter CloudEvent metadata coverage, and added manual-repair guidance for non-auto-retryable drain records without exposing payloads or secrets.
- 2026-05-14 ST4 completed: added the backend deployment matrix with explicit EventStore/DAPR/broker/subscriber boundaries, claim statuses, Kafka partition-key and Service Bus session caveats, and extended component inspection coverage to Azure Service Bus.
- 2026-05-14 ST5 completed: recorded proof boundaries, preserved Redis integration evidence as environment-gated, added RabbitMQ/Kafka/Service Bus manual proof paths, and added doc tests that keep configured backends from being mislabeled as proven.
- 2026-05-14 ST6 completed: aligned command lifecycle, event envelope, configuration, DAPR component, drain reason, and deploy docs with at-least-once, duplicate tolerance, backend matrix, and dead-letter boundary language. Generated API docs were not refreshed because no public XML/API surface changed.
- 2026-05-14 ST7 completed: focused story validation, markdown validation, and listed unit regression projects passed. Aspire MCP reported no running AppHost in this workspace, so integration pub/sub proof remains environment-gated. Full Server.Tests was attempted and failed in unrelated command status/error handling/replay/actor integration areas outside the touched files; focused Story 22.5 slices passed.

### File List

- `_bmad-output/implementation-artifacts/22-5-event-publishing-guarantees-and-backend-deployment-matrix.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterPublisherTests.cs`
- `docs/concepts/command-lifecycle.md`
- `docs/concepts/event-envelope.md`
- `docs/guides/configuration-reference.md`
- `docs/guides/dapr-component-reference.md`
- `docs/operations/drain-failure-reason-codes.md`
- `deploy/README.md`
- `tests/Hexalith.EventStore.Server.Tests/Security/PubSubTopicIsolationEnforcementTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- YAML validation passed for `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- `git diff --check` passed for the story artifact, sprint status, and run log with line-ending conversion warnings only.
- `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-5-event-publishing-guarantees-and-backend-deployment-matrix.md` passed with 0 errors.
- Party-mode review completed on 2026-05-13 and is recorded below.
- Advanced elicitation completed on 2026-05-13 and is recorded below.
- 2026-05-14 ST0 validation: context/code/config/docs/tests inventory completed and recorded in the ST0 decision table. No product code changed and no runtime tests were required for this baseline documentation task.
- 2026-05-14 ST1 validation: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~EventPublisherTests" --no-restore` passed 23/23.
- 2026-05-14 ST2 validation: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~PersistThenPublishResilienceTests|FullyQualifiedName~EventDrainRecoveryTests|FullyQualifiedName~Dw8DrainReasonClassifierTests" --no-restore` passed 47/47.
- 2026-05-14 ST3 validation: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~DeadLetterPublisherTests|FullyQualifiedName~DeadLetterMessageTests|FullyQualifiedName~DeadLetterRoutingTests" --no-restore` passed 36/36.
- 2026-05-14 ST4 validation: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~PubSubTopicIsolationEnforcementTests" --no-restore` passed 9/9.
- 2026-05-14 ST5 validation: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~ProductionDaprComponentValidationTests|FullyQualifiedName~PubSubDeliveryProofTests" --no-restore` passed 21/21 in Server.Tests; integration `PubSubDeliveryProofTests` are in `tests/Hexalith.EventStore.IntegrationTests` and remain environment-gated.
- 2026-05-14 ST6 validation: `npx markdownlint-cli2 docs/concepts/command-lifecycle.md docs/concepts/event-envelope.md docs/guides/configuration-reference.md docs/guides/dapr-component-reference.md docs/operations/drain-failure-reason-codes.md deploy/README.md _bmad-output/implementation-artifacts/22-5-event-publishing-guarantees-and-backend-deployment-matrix.md` passed with 0 errors.
- 2026-05-14 ST7 focused validation: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "EventPublisher|EventDrain|PersistThenPublish|DeadLetter|UnpublishedEvents|DrainReason" --no-restore` passed 180/180.
- 2026-05-14 ST7 listed unit regressions: Client.Tests passed 386/386; Contracts.Tests passed 328/328; Sample.Tests passed 74/74; Testing.Tests passed 98/98.
- 2026-05-14 ST7 Aspire/integration check: `mcp__aspire__.list_apphosts` returned no AppHosts within `D:\Hexalith.EventStore`; integration `PubSubDeliveryProof|DeadLetter` tests were not run because the required Aspire/DAPR topology was not available.
- 2026-05-14 ST7 full Server.Tests attempt: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --no-restore` failed 24, passed 1875, skipped 25. Failures were outside Story 22.5 touched files: command status whitespace detail, validation ProblemDetails null handling, query not-found internal-detail assertion, replay controller expectations, and actor integration/persistence/tombstoning acceptance paths.
- 2026-05-14 ST7 diff validation: `git diff --check` passed with line-ending conversion warnings only.

### Review Findings

<!-- Code review: 2026-05-14 — Claude Sonnet 4.6 — Blind Hunter + Edge Case Hunter + Acceptance Auditor -->

#### Decision-Needed

- [x] [Review][Decision] D1 — AC4: No test for actor restart / reminder overlap during drain — resolved: added `ReceiveReminder_ReminderOverlap_SecondCallIsDuplicateTolerant` to EventDrainRecoveryTests.cs.
- [x] [Review][Decision] D2 — AC5: Azure Service Bus lacks a dedicated per-backend scoping test — resolved: added `ProductionServiceBusYaml_HasSubscriptionScoping_RestrictsSubscribers` to PubSubTopicIsolationEnforcementTests.cs.

#### Patches

- [x] [Review][Patch] P1 — NSubstitute `Arg.Do` setup called with `await` — premature mock invocation [tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs, tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterPublisherTests.cs]
- [x] [Review][Patch] P2 — Discarded `_ =` on `ShouldNotBeNull()` silently drops assertion [tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs]
- [x] [Review][Patch] P3 — `cloudevent.id` composite format `{correlationId}:{sequenceNumber}` not documented in event-envelope.md [docs/concepts/event-envelope.md]
- [x] [Review][Patch] P4 — `deploy/README.md` consumer dedup note omits `cloudevent.id` from the field list [deploy/README.md]
- [x] [Review][Patch] P5 — Production YAML files not checked for presence of dev-only `eventstore-test-subscriber` grant [tests/Hexalith.EventStore.Server.Tests/Security/PubSubTopicIsolationEnforcementTests.cs]
- [x] [Review][Patch] P6 — `PubSubBackendDocs_RecordHonestProofLevels` has no negative assertion that RabbitMQ, Kafka, and Service Bus rows do not use the word `proven` [tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs]

#### Deferred

- [x] [Review][Defer] W1 — `pending_command_count` semantics not attested in this story's tests — pre-existing coverage in BackpressureTests/Dw1DrainHardeningAtddTests; story didn't change backpressure behavior
- [x] [Review][Defer] W2 — Drain record removal before `SaveStateAsync` confirms — pre-existing implementation behavior; not changed by this story
- [x] [Review][Defer] W3 — `PubSubBackendDocs_RecordHonestProofLevels` brittle path traversal and verbatim string matching — pre-existing pattern across entire ProductionDaprComponentValidationTests class
- [x] [Review][Defer] W4 — Ambiguous DAPR publish result handling untested at unit level — pre-existing behavior; story didn't change publish error handling logic
- [x] [Review][Defer] W5 — `ConfigureEventsInState` multiple stub overwrites on shared mock — pre-existing test helper design
- [x] [Review][Defer] W6 — `ProductionPubSubs_HaveDeadLetterEnabled` conflates EventStore vs DAPR dead-letter concepts — pre-existing test name and design
- [x] [Review][Defer] W7 — No test for drain record deletion when `SaveStateAsync` fails after successful publish — pre-existing gap
- [x] [Review][Defer] W8 — Azure Service Bus test conflates DAPR sidecar and Azure RBAC authorization layers — pre-existing test design

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-14 | 1.1 | Completed ST7 validation record and moved story to review. | Codex |
| 2026-05-14 | 1.0 | Completed ST6 public documentation alignment and markdown validation. | Codex |
| 2026-05-14 | 0.9 | Completed ST5 proof classification, manual backend proof paths, and honest-proof doc guard. | Codex |
| 2026-05-14 | 0.8 | Completed ST4 backend deployment matrix and Service Bus config inspection coverage. | Codex |
| 2026-05-14 | 0.7 | Completed ST3 dead-letter boundary docs and focused metadata coverage. | Codex |
| 2026-05-14 | 0.6 | Completed ST2 partial publish and drain lifecycle hardening evidence. | Codex |
| 2026-05-14 | 0.5 | Completed ST1 publish metadata contract and focused EventPublisher coverage. | Codex |
| 2026-05-14 | 0.4 | Completed ST0 baseline inventory and recorded publishing guarantees decision table. | Codex |
| 2026-05-13 | 0.3 | Applied advanced-elicitation hardening for publish acceptance boundaries, partial-batch ambiguity, proof labels, idempotency guidance, and dead-letter separation. | Codex automation |
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

## Advanced Elicitation

- Date/time: 2026-05-13T14:02:29+02:00
- Selected story key: `22-5-event-publishing-guarantees-and-backend-deployment-matrix`
- Command/skill invocation used:
  `/bmad-advanced-elicitation 22-5-event-publishing-guarantees-and-backend-deployment-matrix`
- Batch 1 method names:
    - Self-Consistency Validation
    - Red Team vs Blue Team
    - Architecture Decision Records
    - Failure Mode Analysis
    - Comparative Analysis Matrix
- Reshuffled Batch 2 method names:
    - Chaos Monkey Scenarios
    - First Principles Analysis
    - Occam's Razor Application
    - 5 Whys Deep Dive
    - Lessons Learned Extraction
- Findings summary:
    - The story was directionally sound but still let readers confuse EventStore persistence, DAPR publish acceptance, broker delivery, and subscriber processing as one guarantee.
    - Partial-batch and ambiguous publish outcomes needed fail-safe language so implementers do not skip later persisted events or delete recovery state after incomplete evidence.
    - Backend proof labels needed stricter semantics; component YAML inspection is not live proof for RabbitMQ, Kafka, or Azure Service Bus behavior.
    - Consumer idempotency guidance needed to prefer EventStore identity and sequence metadata over broker-specific delivery identifiers.
    - Dead-letter evidence needed sharper separation between EventStore infrastructure dead letters and DAPR subscriber delivery dead letters.
- Changes applied:
    - Added DAPR publish-acceptance versus subscriber-success language to the Event Publishing Contract.
    - Added conservative partial-batch publication and ambiguous-publish handling requirements to AC1, ST2, and implementation traps.
    - Tightened backend matrix requirements with explicit guarantee boundaries and a claim-status column.
    - Clarified downstream idempotency guidance to avoid broker-specific receipt handles as the cross-backend contract.
    - Added manual repair/no-payload-disclosure guidance for non-retryable drain failures and event-count mismatch cases.
    - Updated Dev Agent Record, Completion Notes, Verification Status, and Change Log with this dated advanced elicitation trace.
- Findings deferred:
    - Manual requeue/repair command shape remains a product/operations decision unless existing drain tooling satisfies it.
    - Required live proof for RabbitMQ, Kafka, and Azure Service Bus remains infrastructure-dependent and must be labeled honestly during implementation.
    - Exact Azure Service Bus session and Kafka partition-key policy remains an architecture decision to record before claiming ordered backend delivery.
    - Payload-protection policy, replay/read APIs, and query/gateway contract changes remain owned by sibling Epic 22 stories.
- Final recommendation: ready-for-dev after applied story updates.
