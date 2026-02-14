# Story 4.3: At-Least-Once Delivery & DAPR Retry Policies

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Story 4.2 (Per-Tenant-Per-Domain Topic Isolation) MUST be implemented before this story.**

Story 4.2 adds the `TopicNameValidator` and comprehensive multi-tenant topic isolation tests. Story 4.1 provides the `EventPublisher` that publishes events via `DaprClient.PublishEventAsync`. This story configures DAPR resiliency policies for pub/sub delivery retries, sets up dead-letter topic routing for retry exhaustion, and ensures subscribers handle idempotent processing.

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Events/IEventPublisher.cs` (Story 4.1 -- event publication interface)
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (Story 4.1 -- DAPR pub/sub implementation, NO custom retry logic)
- `src/Hexalith.EventStore.Server/Events/EventPublishResult.cs` (Story 4.1 -- publication result record)
- `src/Hexalith.EventStore.Server/Events/ITopicNameValidator.cs` (Story 4.2 -- topic validation interface)
- `src/Hexalith.EventStore.Server/Events/TopicNameValidator.cs` (Story 4.2 -- D6 topic name validation)
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` (Story 4.1 -- pub/sub component config)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Stories 3.2-3.11 + 4.1 -- pipeline with publication step)
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` (Story 3.11 -- checkpointed stages)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (Story 1.2 -- includes `PubSubTopic` property)
- `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml` (Stories 1.5/3.1 -- current resiliency config)
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` (Story 1.5 -- Redis pub/sub component)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` (Stories 4.1/4.2 -- test double)
- `deploy/dapr/resiliency.yaml` (production resiliency config template)
- `deploy/dapr/pubsub-rabbitmq.yaml` (production RabbitMQ pub/sub template)

Run `dotnet test` to confirm all existing tests pass before beginning. Stories 4.1 + 4.2 should have ~780-810 tests.

## Story

As a **subscriber system**,
I want at-least-once delivery guarantee for published events with DAPR retry policies handling transient failures,
So that no events are silently lost during distribution (FR18, NFR22).

## Acceptance Criteria

1. **DAPR resiliency policies for pub/sub delivery** - Given events are published to the pub/sub topic, When DAPR delivers events to a subscriber, Then DAPR resiliency policies (configured in resiliency component) automatically retry delivery on transient failures, And retry configuration targets the `components.pubsub` section (not just app-level retries), And outbound retry policy handles publisher-to-sidecar failures, And inbound retry policy handles sidecar-to-subscriber delivery failures.

2. **No application-level retry logic** - Given the EventPublisher implementation exists (Story 4.1), When reviewing the code, Then ZERO custom retry logic exists in `EventPublisher.cs` (enforcement rule #4), And the EventPublisher makes a single `DaprClient.PublishEventAsync` call per event, And DAPR's sidecar-level resiliency policies handle all transient failure retries, And this is verified by automated tests.

3. **Retry exhaustion leads to dead-letter path** - Given events are published and the subscriber fails to acknowledge delivery, When DAPR exhausts all configured retries, Then the event follows the dead-letter topic path, And the dead-letter topic name follows the convention `deadletter.{tenant}.{domain}.events`, And dead-letter topic configuration is set on the pub/sub subscription.

4. **Subscriber idempotent processing contract** - Given at-least-once delivery means possible duplicate events, When a subscriber receives events, Then it can detect duplicates using the CloudEvents `id` field (`{correlationId}:{sequenceNumber}` -- unique per event), And the subscriber contract documents that idempotent processing is REQUIRED, And the sample domain service demonstrates idempotent event handling.

5. **Exponential backoff retry policy with conservative outbound retries** - Given the DAPR resiliency configuration targets pub/sub, When configuring retry policies, Then the pub/sub outbound retry uses exponential backoff with `maxInterval: 10s` and `maxRetries: 3` (local) / `maxRetries: 5` (production) -- conservative because Story 4.4's recovery mechanism handles prolonged outages, And the pub/sub inbound retry uses exponential backoff with `maxInterval: 30s` and `maxRetries: 10` (local) / `maxRetries: 20` (production), And retry configuration is separate from the app-level retry policy.

6. **Circuit breaker for pub/sub with fast-fail behavior** - Given the pub/sub system experiences repeated failures, When the circuit breaker threshold is reached, Then the circuit breaker opens and prevents further publish attempts (fast-fail), And any AggregateActor attempting to publish during open circuit receives immediate failure (transitions to PublishFailed without waiting), And the circuit breaker configuration uses `consecutiveFailures > 5` (local) / `> 10` (production), And circuit breaker `maxRequests: 1` means only 1 probe request in half-open state -- conservative to prevent flooding a recovering broker.

7. **Pub/sub component dead-letter configuration** - Given the pub/sub component YAML configurations exist for local and production, When dead-letter topics are configured, Then each pub/sub component has dead-letter support enabled, And dead-letter configuration works with Redis (local), RabbitMQ (production), Kafka (production), and Azure Service Bus (production) (NFR28), And there are TWO levels of dead-letter routing: (1) component-level default `deadletter` topic (this story), (2) per-subscription tenant-scoped dead-letter topics (Story 4.5).

8. **Timeout policies for pub/sub outbound and inbound** - Given pub/sub calls can hang indefinitely without timeout, When timeout policies are configured, Then outbound timeout is 10s (publisher -> sidecar -> broker path), And inbound timeout is 30s (broker -> sidecar -> subscriber processing path), And timeout policies prevent hung sidecars from blocking actor turns indefinitely.

9. **OpenTelemetry tracing for retry context** - Given events are published and DAPR retries delivery, When observing telemetry, Then the `EventStore.Events.Publish` activity (from Story 4.1) captures the initial publish attempt, And DAPR sidecar provides retry-level tracing via its own OpenTelemetry integration, And the EventPublisher logs at Warning level when the `EventPublishResult` indicates failure (retry exhaustion reached the EventPublisher).

10. **Verification that DAPR resiliency augments built-in component retries** - Given each pub/sub component has built-in retry behavior, When DAPR resiliency policies are applied, Then the policies AUGMENT (not replace) the component's built-in retries, And this interaction is documented in the resiliency YAML comments with effective retry count calculations per backend, And tests verify that all policy names referenced in targets exist in the policies section (prevent silent typo-based policy disabling).

11. **At-least-once guarantee scoping** - Given events are persisted to the state store before publication, When publication fails (partial or total), Then events are NEVER lost from the state store (NFR22), And partial publication (events 1-2 published, 3-5 not) is acceptable because subscribers must be idempotent, And recovery of unpublished events from the state store is handled by Story 4.4's drain mechanism, And the at-least-once guarantee from state store to subscriber requires BOTH this story (retry policies) AND Story 4.4 (recovery drain).

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [ ] 0.1 Run all existing tests -- they must pass before proceeding
  - [ ] 0.2 Review `EventPublisher.cs` (Story 4.1) -- confirm ZERO custom retry logic exists (rule #4 compliance)
  - [ ] 0.3 Review current `resiliency.yaml` (local) -- note it only targets `apps.commandapi`, NOT `components.pubsub`
  - [ ] 0.4 Review current `pubsub.yaml` (local) -- note it has no dead-letter configuration
  - [ ] 0.5 Review `deploy/dapr/resiliency.yaml` (production) -- note it only targets `apps.commandapi`
  - [ ] 0.6 Review `deploy/dapr/pubsub-rabbitmq.yaml` and `deploy/dapr/pubsub-kafka.yaml` -- note no dead-letter configuration
  - [ ] 0.7 Review `FakeEventPublisher.cs` -- understand configurable failure modes for retry testing

- [ ] Task 1: Update local DAPR resiliency configuration for pub/sub (AC: #1, #5, #6)
  - [ ] 1.1 Edit `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml`
  - [ ] 1.2 Add `pubsubRetryOutbound` policy: exponential backoff, `maxInterval: 10s`, `maxRetries: 3` (conservative -- Story 4.4 recovery handles prolonged outages)
  - [ ] 1.3 Add `pubsubRetryInbound` policy: exponential backoff, `maxInterval: 30s`, `maxRetries: 10`
  - [ ] 1.4 Add `pubsubBreaker` circuit breaker: `consecutiveFailures > 5`, `interval: 10s`, `timeout: 30s`, `maxRequests: 1` (single probe in half-open state)
  - [ ] 1.5 Add `pubsubTimeout` timeout policy: `10s` (outbound publisher -> sidecar -> broker path)
  - [ ] 1.6 Add `subscriberTimeout` timeout policy: `30s` (inbound broker -> sidecar -> subscriber processing path)
  - [ ] 1.7 Add `targets.components.pubsub` section with `outbound.retry: pubsubRetryOutbound`, `outbound.timeout: pubsubTimeout`, `inbound.retry: pubsubRetryInbound`, `inbound.timeout: subscriberTimeout`, circuit breaker
  - [ ] 1.8 Add comments documenting that resiliency policies AUGMENT built-in pub/sub component retries
  - [ ] 1.9 Preserve existing `targets.apps.commandapi` configuration unchanged

- [ ] Task 2: Update production DAPR resiliency configuration for pub/sub (AC: #1, #5, #6)
  - [ ] 2.1 Edit `deploy/dapr/resiliency.yaml`
  - [ ] 2.2 Add `pubsubRetryOutbound` policy: exponential backoff, `maxInterval: 15s`, `maxRetries: 5` (conservative -- Story 4.4 recovery handles prolonged outages)
  - [ ] 2.3 Add `pubsubRetryInbound` policy: exponential backoff, `maxInterval: 60s`, `maxRetries: 20`
  - [ ] 2.4 Add `pubsubBreaker` circuit breaker: `consecutiveFailures > 10`, `interval: 60s`, `timeout: 60s`, `maxRequests: 1`
  - [ ] 2.5 Add `pubsubTimeout` timeout policy: `10s` (outbound)
  - [ ] 2.6 Add `subscriberTimeout` timeout policy: `30s` (inbound)
  - [ ] 2.7 Add `targets.components.pubsub` section with outbound/inbound policies including timeouts
  - [ ] 2.8 Add documentation comments explaining policy rationale, augmentation behavior, and effective retry count per backend

- [ ] Task 3: Configure dead-letter topic on local pub/sub component (AC: #3, #7)
  - [ ] 3.1 Update `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` -- add `deadLetterTopic` metadata
  - [ ] 3.2 Dead-letter topic naming: `deadletter` (DAPR routes undeliverable messages to this topic by default; the actual per-tenant dead-letter routing is handled at subscription level in Story 4.5)
  - [ ] 3.3 Add comments explaining that per-tenant dead-letter topic naming (`deadletter.{tenant}.{domain}.events`) will be configured per-subscription in Story 4.5

- [ ] Task 4: Configure dead-letter topics on production pub/sub components (AC: #7)
  - [ ] 4.1 Update `deploy/dapr/pubsub-rabbitmq.yaml` -- add dead-letter configuration metadata
  - [ ] 4.2 Update `deploy/dapr/pubsub-kafka.yaml` -- add dead-letter configuration metadata (if file exists)
  - [ ] 4.3 Add `enableDeadLetter: true` and default `deadLetterTopic: deadletter` metadata
  - [ ] 4.4 Verify dead-letter configuration is compatible with RabbitMQ, Kafka, Azure Service Bus (NFR28)

- [ ] Task 5: Create EventPublisherOptions dead-letter topic support (AC: #3)
  - [ ] 5.1 Add `DeadLetterTopicPrefix` property to `EventPublisherOptions` -- default: `"deadletter"`
  - [ ] 5.2 Add helper method `GetDeadLetterTopic(AggregateIdentity identity)` returning `$"{DeadLetterTopicPrefix}.{identity.TenantId}.{identity.Domain}.events"`
  - [ ] 5.3 This is for Story 4.5 to use when explicitly routing to dead-letter topics, but the configuration belongs in 4.3

- [ ] Task 6: Create DAPR resiliency configuration validation tests (AC: #1, #5, #6, #8, #10)
  - [ ] 6.1 Create `ResiliencyConfigurationTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Configuration/`
  - [ ] 6.2 Test: `LocalResiliency_ContainsPubSubOutboundRetryPolicy` -- parse local resiliency.yaml, verify `pubsubRetryOutbound` exists with exponential policy
  - [ ] 6.3 Test: `LocalResiliency_ContainsPubSubInboundRetryPolicy` -- verify `pubsubRetryInbound` exists with exponential policy
  - [ ] 6.4 Test: `LocalResiliency_ContainsPubSubCircuitBreaker` -- verify `pubsubBreaker` exists
  - [ ] 6.5 Test: `LocalResiliency_TargetsPubSubComponent` -- verify `targets.components.pubsub` section exists
  - [ ] 6.6 Test: `ProductionResiliency_ContainsPubSubRetryPolicies` -- verify production config has pub/sub policies
  - [ ] 6.7 Test: `ProductionResiliency_HigherRetryLimits` -- verify production has higher maxRetries than local
  - [ ] 6.8 Test: `LocalResiliency_ContainsPubSubTimeoutPolicy` -- verify `pubsubTimeout` (10s) and `subscriberTimeout` (30s) exist
  - [ ] 6.9 Test: `ProductionResiliency_ContainsPubSubTimeoutPolicy` -- verify production timeout policies exist
  - [ ] 6.10 Test: `Resiliency_AllTargetPolicyNamesExistInPolicies` -- cross-reference validation: every policy name referenced in `targets` section must exist in `policies` section (prevents silent YAML typo-based policy disabling)

- [ ] Task 7: Create EventPublisher no-retry compliance tests (AC: #2)
  - [ ] 7.1 Create `EventPublisherRetryComplianceTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [ ] 7.2 Test: `PublishEventsAsync_TransientFailure_NoRetry_ReturnsFailed` -- Verify EventPublisher calls `PublishEventAsync` exactly once per event (no retry loop)
  - [ ] 7.3 Test: `PublishEventsAsync_SingleCallPerEvent_Verified` -- Mock DaprClient, verify each event gets exactly 1 `PublishEventAsync` call
  - [ ] 7.4 Test: `EventPublisher_NoRetryPolicyImported_NoPollyReference` -- Code inspection test: verify no `Polly`, `System.Net.Http.Retry`, or custom retry patterns in EventPublisher source
  - [ ] 7.5 Test: `PublishEventsAsync_DaprClientException_CaughtNotRetried` -- Verify exception from DaprClient is caught and returned as failure, not retried

- [ ] Task 8: Create subscriber idempotency contract tests (AC: #4)
  - [ ] 8.1 Create `SubscriberIdempotencyTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [ ] 8.2 Test: `CloudEventsId_UniquePerEvent_CorrelationIdPlusSequence` -- Verify the CloudEvents `id` format `{correlationId}:{sequenceNumber}` is globally unique
  - [ ] 8.3 Test: `CloudEventsId_SameEventPublishedTwice_IdenticalId` -- Verify re-publishing the same event produces the same CloudEvents `id` (enables dedup)
  - [ ] 8.4 Test: `CloudEventsId_DifferentEvents_SameCorrelation_DifferentIds` -- Verify events within the same command have different `id` values (different sequence numbers)
  - [ ] 8.5 Test: `CloudEventsId_DifferentCorrelations_SameSequence_DifferentIds` -- Verify events from different commands with same sequence have different `id` values

- [ ] Task 9: Create at-least-once delivery behavior tests (AC: #1, #3, #6, #8, #9)
  - [ ] 9.1 Create `AtLeastOnceDeliveryTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [ ] 9.2 Test: `PublishEventsAsync_Success_EventsDeliveredAtLeastOnce` -- Full pipeline: events persisted -> published -> verify FakeEventPublisher received all events
  - [ ] 9.3 Test: `PublishEventsAsync_PartialFailure_SomeEventsDelivered` -- Verify partial delivery: first N events published, rest failed (at-least-once allows partial)
  - [ ] 9.4 Test: `PublishEventsAsync_TotalFailure_EventsSafeInStateStore` -- Verify events remain in state store when publication fails completely (persist-then-publish guarantee)
  - [ ] 9.5 Test: `AggregateActor_PublishFailed_EventsNotLostInStateStore` -- Full actor pipeline: events persisted, publish fails, verify events are still in state store (NFR22)
  - [ ] 9.6 Test: `AggregateActor_PublishFailed_StatusTransitionsToPublishFailed` -- Verify state machine transitions correctly on publish failure
  - [ ] 9.7 Test: `CircuitBreaker_Open_PublisherReceivesImmediateFailure` -- Verify that when circuit breaker is open, AggregateActor receives immediate failure and transitions to PublishFailed without waiting (fast-fail behavior)

- [ ] Task 10: Create EventPublisherOptions dead-letter tests (AC: #3)
  - [ ] 10.1 Create `EventPublisherOptionsTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Configuration/` (or add to existing)
  - [ ] 10.2 Test: `DeadLetterTopicPrefix_Default_IsDeadletter` -- Default value
  - [ ] 10.3 Test: `GetDeadLetterTopic_ValidIdentity_ReturnsCorrectPattern` -- `deadletter.acme.orders.events`
  - [ ] 10.4 Test: `GetDeadLetterTopic_DifferentTenants_DifferentTopics` -- Tenant isolation in dead-letter

- [ ] Task 11: Verify all tests pass
  - [ ] 11.1 Run `dotnet test` to confirm no regressions
  - [ ] 11.2 All new resiliency configuration tests pass
  - [ ] 11.3 All new retry compliance tests pass
  - [ ] 11.4 All new subscriber idempotency tests pass
  - [ ] 11.5 All new at-least-once delivery tests pass
  - [ ] 11.6 All existing Story 4.1 + 4.2 tests still pass

## Dev Notes

### Story Context

This is the **third story in Epic 4: Event Distribution & Dead-Letter Handling**. It ensures that published events reach subscribers with an at-least-once delivery guarantee, relying entirely on DAPR resiliency policies for retry behavior. The story bridges the gap between Story 4.2 (topic isolation) and Story 4.4 (persist-then-publish resilience for pub/sub outages).

**What Stories 4.1 and 4.2 already implemented (to BUILD ON, not replicate):**
- `EventPublisher` publishes events using `DaprClient.PublishEventAsync` -- NO custom retry logic (rule #4 compliant)
- On failure: catches exception, logs Error, returns `EventPublishResult(false, ...)` -- does NOT rethrow, does NOT retry
- CloudEvents metadata includes unique `id` = `{correlationId}:{sequenceNumber}` per event (subscriber dedup key)
- `FakeEventPublisher` supports configurable failure and partial failure modes
- `TopicNameValidator` validates D6 topic format compliance
- Per-tenant-per-domain topic isolation tested comprehensively
- OpenTelemetry activity `EventStore.Events.Publish` with `eventstore.topic` tag

**What this story adds (NEW):**
- DAPR resiliency policies targeting `components.pubsub` (outbound + inbound retry, circuit breaker)
- Dead-letter topic configuration on pub/sub components (local + production)
- `EventPublisherOptions.DeadLetterTopicPrefix` and `GetDeadLetterTopic()` helper (for Story 4.5)
- Comprehensive tests verifying: no application-level retry, DAPR resiliency configuration validity, subscriber idempotency contract, at-least-once delivery behavior, dead-letter topic naming

**What this story modifies (EXISTING):**
- `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml` -- add pub/sub-specific retry policies and circuit breaker
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` -- add dead-letter topic metadata
- `deploy/dapr/resiliency.yaml` -- add production pub/sub retry policies
- `deploy/dapr/pubsub-rabbitmq.yaml` -- add dead-letter metadata
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` -- add dead-letter topic prefix

**Key insight: At-least-once delivery is a DAPR infrastructure guarantee, not an application concern.**
DAPR's pub/sub building block natively provides at-least-once delivery. The EventPublisher (Story 4.1) makes a single publish call per event -- DAPR handles retries at the sidecar level via resiliency policies. This story's primary value is:
1. CONFIGURING DAPR resiliency policies specifically for pub/sub (currently only app-level policies exist)
2. CONFIGURING dead-letter topics for retry exhaustion (preparation for Story 4.5)
3. PROVING through tests that no application-level retry exists
4. DOCUMENTING the subscriber idempotency contract

### Architecture Compliance

- **FR18:** At-least-once delivery guarantee for published events
- **NFR22:** Zero events lost under any tested failure scenario
- **NFR24:** After pub/sub recovery, all events persisted during outage must be delivered (DAPR retry + persist-then-publish)
- **NFR28:** System must function with any DAPR-compatible pub/sub component (validated: RabbitMQ, Kafka, Azure Service Bus)
- **Rule #4:** Never add custom retry logic -- all retries are DAPR resiliency policies
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **Rule #12:** Command status writes are advisory -- failure never blocks pipeline
- **Rule #13:** No stack traces in production error responses
- **SEC-5:** Event payload data never in logs
- **D6:** Topic naming `{tenant}.{domain}.events`
- **ADR-P2:** Persist-then-publish event flow (events safe in state store; pub/sub is distribution mechanism)

### Critical Design Decisions

- **DAPR resiliency policies AUGMENT built-in component retries, not replace them.** Each DAPR pub/sub component (Redis, RabbitMQ, Kafka, Azure Service Bus) has its own built-in retry behavior. Applying a DAPR resiliency policy adds a second retry layer on top of the component's native retry. This can cause repetitive message clustering. The resiliency YAML must document this interaction clearly so operators understand the effective retry count = component retries x resiliency retries.

- **Outbound vs inbound retry policies serve different purposes.**
  - **Outbound** (`targets.components.pubsub.outbound`): Retries for the EventPublisher -> DAPR sidecar -> pub/sub broker path. Protects against transient broker unavailability when publishing.
  - **Inbound** (`targets.components.pubsub.inbound`): Retries for the pub/sub broker -> DAPR sidecar -> subscriber application path. Protects against subscriber application failures during event processing.
  - Both need separate policies because failure modes are different (publishing vs consuming).

- **Dead-letter topic configuration is at the SUBSCRIPTION level, not the component level.** DAPR allows dead-letter topics per subscription. For Story 4.3, we add the DEFAULT dead-letter topic on the component metadata. Story 4.5 will configure per-subscription dead-letter topics for fine-grained tenant-scoped dead-letter routing.

- **EventPublisherOptions.GetDeadLetterTopic() is for Story 4.5 but belongs here.** The configuration for dead-letter topic naming convention is a 4.3 concern (resiliency configuration), but the actual dead-letter routing implementation is Story 4.5. We add the option now so 4.5 doesn't need to modify the configuration class.

- **Circuit breaker prevents cascading failures during prolonged pub/sub outages.** If the pub/sub system is completely down, the circuit breaker opens after consecutive failures, preventing the EventPublisher from wasting resources on doomed publish calls. This complements Story 4.4's persist-then-publish resilience: when the circuit breaker opens, the actor transitions to PublishFailed, and events remain safe in the state store.

- **Subscriber idempotency is a CONTRACT, not an enforcement mechanism.** Story 4.3 documents that subscribers MUST handle duplicates (at-least-once = possible duplicates). The CloudEvents `id` field (`{correlationId}:{sequenceNumber}`) is globally unique per event and deterministic (same event always produces the same id). Subscribers can use this as a deduplication key. The EventStore does NOT enforce idempotent processing on subscribers -- that's the subscriber's responsibility.

- **DAPR sidecar acceptance ≠ broker persistence.** When `DaprClient.PublishEventAsync` returns successfully, the event has been accepted by the DAPR sidecar -- NOT necessarily persisted to the broker. The sidecar forwards to the broker with its own retry. If the sidecar crashes between acceptance and broker delivery, the event is lost from the pub/sub path (but safe in the state store per persist-then-publish). This is why Story 4.4's recovery drain is critical for production at-least-once guarantees.

- **Effective retry count = component built-in retries × resiliency retries.** Each backend has different built-in behavior:
  - **Redis Streams**: 0 built-in retries → effective = resiliency retries only (3 local / 5 production)
  - **RabbitMQ**: publisher confirms with ~3 internal retries → effective ≈ 3 × 3 = 9 (local) / 3 × 5 = 15 (production)
  - **Kafka**: `retries` config default = 2147483647 → effectively infinite internal retries → resiliency retries rarely fire
  - **Azure Service Bus**: SDK retries default 3 → effective ≈ 3 × 3 = 9 (local) / 3 × 5 = 15 (production)
  Document this multiplication in resiliency YAML comments so operators understand backend-specific behavior.

- **Timeout policies prevent indefinite actor turn blocking.** Without timeouts, a hung pub/sub call can block an actor turn indefinitely, starving other operations. The outbound timeout (10s) limits how long the EventPublisher waits for the sidecar->broker path. The inbound timeout (30s) limits subscriber processing time. Both are critical for system liveness.

- **Production at-least-once requires BOTH Story 4.3 AND Story 4.4.** Story 4.3 alone provides retry-based delivery for transient failures. For prolonged outages (broker down, network partition), Story 4.4's persist-then-publish drain mechanism recovers unpublished events from the state store. Without 4.4, events that exhaust retries go to dead-letter but are never re-delivered from source. Deployment recommendation: do not promote to production without both stories complete.

- **Policy name typos silently disable resiliency.** If a target references a policy name that doesn't exist in the `policies` section, DAPR silently ignores it -- no error, no retry, no protection. The cross-reference validation test (Task 6.10) guards against this. Every policy name in `targets` must have a matching entry in `policies`.

### Existing Patterns to Follow

**Current resiliency.yaml (local -- to EXTEND, not replace):**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: resiliency
spec:
  policies:
    retries:
      defaultRetry:
        policy: constant
        duration: 1s
        maxRetries: 3
    timeouts:
      daprSidecar:
        general: 5s
    circuitBreakers:
      defaultBreaker:
        maxRequests: 1
        interval: 10s
        timeout: 30s
        trip: consecutiveFailures > 3
  targets:
    apps:
      commandapi:
        retry: defaultRetry
        timeout: daprSidecar
        circuitBreaker: defaultBreaker
```

**Target resiliency.yaml after this story (local):**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: resiliency
spec:
  policies:
    retries:
      defaultRetry:
        policy: constant
        duration: 1s
        maxRetries: 3
      # Pub/sub outbound: publisher -> sidecar -> broker
      # Conservative: Story 4.4 recovery drain handles prolonged outages
      pubsubRetryOutbound:
        policy: exponential
        maxInterval: 10s
        maxRetries: 3
      # Pub/sub inbound: broker -> sidecar -> subscriber app
      pubsubRetryInbound:
        policy: exponential
        maxInterval: 30s
        maxRetries: 10
    timeouts:
      daprSidecar:
        general: 5s
      # Pub/sub outbound: prevent hung sidecar->broker calls from blocking actor turns
      pubsubTimeout: 10s
      # Pub/sub inbound: allow more time for subscriber processing
      subscriberTimeout: 30s
    circuitBreakers:
      defaultBreaker:
        maxRequests: 1
        interval: 10s
        timeout: 30s
        trip: consecutiveFailures > 3
      # Pub/sub circuit breaker for prolonged broker outages
      pubsubBreaker:
        maxRequests: 1
        interval: 10s
        timeout: 30s
        trip: consecutiveFailures > 5
  targets:
    apps:
      commandapi:
        retry: defaultRetry
        timeout: daprSidecar
        circuitBreaker: defaultBreaker
    # IMPORTANT: These policies AUGMENT built-in pub/sub component retries.
    # Effective retry count = component built-in retries x resiliency retries.
    components:
      pubsub:
        outbound:
          retry: pubsubRetryOutbound
          timeout: pubsubTimeout
          circuitBreaker: pubsubBreaker
        inbound:
          retry: pubsubRetryInbound
          timeout: subscriberTimeout
```

**EventPublisher single-call pattern (from Story 4.1 -- to VERIFY, not change):**
```csharp
// EventPublisher.cs -- ZERO retry logic. Single PublishEventAsync call per event.
// DAPR sidecar handles retries via resiliency policies configured in resiliency.yaml.
await daprClient.PublishEventAsync(
    pubSubName,
    topic,
    eventEnvelope,
    metadata,
    cancellationToken).ConfigureAwait(false);
publishedCount++;
```

**CloudEvents id for subscriber deduplication (from Story 4.1):**
```csharp
// CloudEvents `id` = `{correlationId}:{sequenceNumber}` -- globally unique per event
var metadata = new Dictionary<string, string>
{
    ["cloudevent.id"] = $"{correlationId}:{eventEnvelope.SequenceNumber}",
    // ...
};
```

**EventPublisherOptions pattern (to extend):**
```csharp
public record EventPublisherOptions
{
    public string PubSubName { get; init; } = "pubsub";
    // NEW: Dead-letter topic prefix for retry exhaustion routing
    public string DeadLetterTopicPrefix { get; init; } = "deadletter";

    public string GetDeadLetterTopic(AggregateIdentity identity)
        => $"{DeadLetterTopicPrefix}.{identity.TenantId}.{identity.Domain}.events";
}
```

### Mandatory Coding Patterns

- Primary constructors: `public class ClassName(ILogger<ClassName> logger)`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization
- **Rule #4:** No custom retry logic -- DAPR resiliency handles retries. This is the CENTRAL enforcement of this story.
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **SEC-5:** Event payload data never in logs

### Project Structure Notes

**New files:**
- `tests/Hexalith.EventStore.Server.Tests/Configuration/ResiliencyConfigurationTests.cs` -- DAPR resiliency YAML validation tests
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherRetryComplianceTests.cs` -- verify no application-level retry
- `tests/Hexalith.EventStore.Server.Tests/Events/SubscriberIdempotencyTests.cs` -- CloudEvents id uniqueness / dedup tests
- `tests/Hexalith.EventStore.Server.Tests/Events/AtLeastOnceDeliveryTests.cs` -- at-least-once delivery behavior tests
- `tests/Hexalith.EventStore.Server.Tests/Configuration/EventPublisherOptionsTests.cs` -- dead-letter topic configuration tests (or add to existing)

**Modified files:**
- `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml` -- add pub/sub retry + circuit breaker policies and targets
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` -- add dead-letter topic metadata
- `deploy/dapr/resiliency.yaml` -- add production pub/sub retry + circuit breaker policies
- `deploy/dapr/pubsub-rabbitmq.yaml` -- add dead-letter metadata
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` -- add DeadLetterTopicPrefix and GetDeadLetterTopic()

### Previous Story Intelligence

**From Story 4.1 (Event Publisher with CloudEvents 1.0):**
- EventPublisher makes single `DaprClient.PublishEventAsync` call per event -- ZERO retry logic
- On failure: catches exception, returns `EventPublishResult(false, publishedCount, ex.Message)` -- does NOT rethrow
- CloudEvents metadata: `id` = `{correlationId}:{sequenceNumber}` (globally unique, deterministic dedup key)
- `EventPublishResult(bool Success, int PublishedCount, string? FailureReason)` -- partial failure supported
- EventPublisher is transient-scoped in DI (stateless)
- 755 total tests pass (730 + 25 new)
- EventPersister changed to return `EventPersistResult` (envelopes for publication)
- AggregateActor: EventsStored -> [publish] -> EventsPublished -> Completed OR PublishFailed

**From Story 4.2 (Per-Tenant-Per-Domain Topic Isolation):**
- TopicNameValidator validates D6 `{tenant}.{domain}.events` format
- FakeEventPublisher has topic-aware querying (GetPublishedTopics, GetEventsForTopic, AssertNoEventsForTopic)
- Thread-safe ConcurrentDictionary for concurrent topic tracking
- AggregateIdentity forces lowercase, validates `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`
- Max topic length: 136 chars (64+1+64+7), well within all backend limits

**From Story 3.11 (Actor State Machine):**
- ActorStateMachine checkpoints pipeline stages via `SetStateAsync` (staged, not committed)
- Terminal states: Completed, Rejected, PublishFailed, TimedOut
- Advisory status writes wrapped in try/catch per rule #12
- PublishFailed terminal state: pipeline cleanup + idempotency record + SaveStateAsync atomically

**Current EventPublisher.cs (Story 4.1) -- lines 55-116:**
```
try {
    for each event:
        daprClient.PublishEventAsync(...)  // SINGLE call, no retry
        publishedCount++;
    return EventPublishResult(true, publishedCount, null);
} catch (Exception ex) {
    return EventPublishResult(false, publishedCount, ex.Message);  // No rethrow, no retry
}
```
This is EXACTLY the correct pattern for rule #4 compliance. This story VERIFIES this through automated tests.

### Subscriber Dedup Pattern (for documentation / sample domain service)

Subscribers MUST deduplicate using the CloudEvents `id` field. Recommended pattern:

```csharp
// Subscriber idempotent processing pattern
// CloudEvents id = "{correlationId}:{sequenceNumber}" -- globally unique, deterministic
public async Task HandleEventAsync(CloudEvent cloudEvent, CancellationToken ct)
{
    string eventId = cloudEvent.Id; // e.g., "cmd-abc-123:5"

    // Check if already processed (using state store, database, or in-memory cache)
    if (await _deduplicationStore.HasBeenProcessedAsync(eventId, ct))
    {
        _logger.LogInformation("Duplicate event skipped: EventId={EventId}", eventId);
        return; // Acknowledge to DAPR (200 OK) -- do not redeliver
    }

    // Process the event
    await ProcessEventAsync(cloudEvent, ct);

    // Mark as processed AFTER successful processing
    await _deduplicationStore.MarkProcessedAsync(eventId, ct);
}
```

### Effective Retry Count Table

| Backend | Built-in Retries | Resiliency Outbound (local/prod) | Effective Outbound | Notes |
|---------|-----------------|----------------------------------|-------------------|-------|
| Redis Streams | 0 | 3 / 5 | 3 / 5 | No built-in retry; resiliency is the only layer |
| RabbitMQ | ~3 (publisher confirms) | 3 / 5 | ~9 / ~15 | Multiplicative: component × resiliency |
| Kafka | 2147483647 (default) | 3 / 5 | ∞ (effectively) | Kafka's internal retries dominate; resiliency rarely fires |
| Azure Service Bus | 3 (SDK default) | 3 / 5 | ~9 / ~15 | Multiplicative: SDK × resiliency |

**Important:** Resiliency retries wrap the entire component call. If the component internally retries 3 times and fails, the resiliency layer retries the full component call again. This can cause message clustering under load. The conservative outbound retries (3 local / 5 production) mitigate this risk.

### Production Deployment Warning

**Story 4.3 + Story 4.4 are BOTH required for production at-least-once guarantee.**
- Story 4.3 alone: Retry-based delivery for transient failures. Events that exhaust retries go to dead-letter.
- Story 4.4 adds: Recovery drain from state store for events that were never published (sidecar crash, prolonged outage, circuit breaker open).
- Recommendation: Do not promote to production without both stories complete. Document this dependency in deployment runbook.

### Git Intelligence

Recent commits show the progression through Epic 4:
- `72d7a53` Story 4.1: Event Publisher with CloudEvents 1.0 (#37)
- `226a260` Story 3.11: Actor state machine and checkpointed stages (#36)
- `f79aabe` Story 3.10: State reconstruction from snapshot + tail events (#35)
- Patterns: Primary constructors, records, ConfigureAwait(false), NSubstitute + Shouldly
- DI registration via `Add*` extension methods in `ServiceCollectionExtensions.cs`
- Feature folder organization throughout
- DAPR component YAML files in `DaprComponents/` (local) and `deploy/dapr/` (production)

### Testing Requirements

**Resiliency Configuration Tests (~10 new):**
- Local resiliency.yaml: pubsubRetryOutbound policy exists, pubsubRetryInbound policy exists, pubsubBreaker exists
- Local resiliency.yaml: targets.components.pubsub section exists
- Local resiliency.yaml: pubsubTimeout (10s) and subscriberTimeout (30s) exist
- Production resiliency.yaml: pub/sub policies exist, higher retry limits than local
- Production resiliency.yaml: timeout policies exist
- Cross-reference validation: all target policy names exist in policies section (prevents silent typo-based disabling)

**Retry Compliance Tests (~4 new):**
- EventPublisher calls PublishEventAsync exactly once per event (no retry loop)
- EventPublisher has no Polly/retry imports
- DaprClient exception caught and returned, not retried

**Subscriber Idempotency Tests (~4 new):**
- CloudEvents id unique per event (correlationId:sequenceNumber)
- Same event re-published = same id (deterministic)
- Different events same correlation = different ids
- Different correlations same sequence = different ids

**At-Least-Once Delivery Tests (~6 new):**
- Success path: all events delivered
- Partial failure: some events delivered
- Total failure: events safe in state store
- Actor pipeline: publish fails, events remain in state store (NFR22)
- Actor pipeline: publish fails, status transitions to PublishFailed
- Circuit breaker open: publisher receives immediate failure (fast-fail behavior)

**Dead-Letter Configuration Tests (~3 new):**
- EventPublisherOptions default dead-letter prefix
- GetDeadLetterTopic returns correct pattern
- Different tenants = different dead-letter topics

**Total estimated: ~27-32 tests (new)**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 4, Story 4.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR18 At-least-once delivery guarantee]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR22 Zero events lost]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR24 Pub/sub recovery delivery]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR28 Any DAPR-compatible pub/sub]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 4 No custom retry]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 12 Advisory status writes]
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P2 Persist-then-publish event flow]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5 Event payload never in logs]
- [Source: _bmad-output/implementation-artifacts/4-1-event-publisher-with-cloudevents-1-0.md]
- [Source: _bmad-output/implementation-artifacts/4-2-per-tenant-per-domain-topic-isolation.md]
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs#Lines 55-116 -- ZERO retry logic]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml -- current config targets apps only]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml -- no dead-letter config]
- [Source: deploy/dapr/resiliency.yaml -- production config targets apps only]
- [Source: DAPR docs: https://docs.dapr.io/operations/resiliency/policies/retries/retries-overview/]
- [Source: DAPR docs: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-deadletter/]
- [Source: DAPR docs: https://docs.dapr.io/operations/resiliency/resiliency-overview/]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
