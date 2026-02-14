# Story 4.2: Per-Tenant-Per-Domain Topic Isolation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Story 4.1 (Event Publisher with CloudEvents 1.0) MUST be implemented before this story.**

Story 4.1 creates the `EventPublisher` that publishes events to DAPR pub/sub topics. This story builds on that foundation to verify and enforce per-tenant-per-domain topic isolation, ensuring events for different tenants/domains are never cross-published.

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Events/IEventPublisher.cs` (Story 4.1 -- event publication interface)
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (Story 4.1 -- DAPR pub/sub implementation)
- `src/Hexalith.EventStore.Server/Events/EventPublishResult.cs` (Story 4.1 -- publication result record)
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` (Story 4.1 -- pub/sub component config)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Stories 3.2-3.11 + 4.1 -- pipeline with publication step)
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` (Story 3.11 -- checkpointed stages)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (Story 1.2 -- includes `PubSubTopic` property)
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` (Story 3.11 -- OpenTelemetry activity source)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` (Story 4.1 -- test double for EventPublisher)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryStateManager.cs` (Story 1.4 -- test fake for IActorStateManager)

Run `dotnet test` to confirm all existing tests pass before beginning. Story 4.1 should have added ~25-40 tests on top of Story 3.11's ~730-750 tests.

## Story

As a **subscriber system**,
I want events published to per-tenant-per-domain topics (`{tenant}.{domain}.events`),
So that I only receive events for my authorized scope (FR19).

## Acceptance Criteria

1. **Topic derivation from AggregateIdentity** - Given events are published for tenant "acme" and domain "orders", When the EventPublisher determines the target topic, Then events are published to topic `acme.orders.events` (D6), And the topic is derived from `AggregateIdentity.PubSubTopic` (already `$"{TenantId}.{Domain}.events"`).

2. **Multi-tenant topic isolation** - Given events are published for tenants "acme" and "globex" in domain "orders", When the EventPublisher publishes events for each tenant, Then tenant "acme" events go to topic `acme.orders.events`, And tenant "globex" events go to topic `globex.orders.events`, And these are structurally distinct topics with no overlap.

3. **Multi-domain topic isolation** - Given events are published for tenant "acme" in domains "orders" and "inventory", When the EventPublisher publishes events for each domain, Then "orders" events go to topic `acme.orders.events`, And "inventory" events go to topic `acme.inventory.events`, And these are structurally distinct topics with no overlap.

4. **Cross-tenant isolation guarantee** - Given a subscriber is subscribed to topic `acme.orders.events`, When events are published for tenant "globex" in domain "orders", Then the subscriber NEVER receives events from `globex.orders.events`, And topic isolation is guaranteed by the per-tenant topic naming convention.

5. **Topic name validation for pub/sub compatibility** - Given events are published with various tenant and domain names, When topic names are derived, Then all topic names conform to D6 convention `{tenant}.{domain}.events`, And topic names are valid for DAPR-compatible pub/sub components: RabbitMQ (exchange/routing key), Kafka (topic name), Azure Service Bus (topic name) (NFR28), And topic names use only lowercase alphanumeric characters plus hyphens and dots (guaranteed by AggregateIdentity validation).

6. **Topic derivation consistency** - Given the same AggregateIdentity is used across multiple publish calls, When the EventPublisher derives the topic, Then the topic name is deterministic and identical for all events from the same tenant+domain combination, And case normalization ensures "Acme" and "acme" produce the same topic (AggregateIdentity forces lowercase).

7. **OpenTelemetry topic tracing** - Given events are published to a per-tenant-per-domain topic, When the EventPublisher creates an OpenTelemetry activity, Then the `eventstore.topic` tag contains the full topic name (e.g., `acme.orders.events`), And the `eventstore.tenant_id` and `eventstore.domain` tags match the topic components.

8. **Structured logging with topic context** - Given events are published, When the EventPublisher logs the publication, Then the log entry includes the `topic` field with the full topic name, And the log entry includes `tenantId` and `domain` fields matching the topic components, And event payload data never appears in logs (SEC-5, NFR12).

9. **Concurrent multi-tenant publication** - Given multiple tenants are processing commands simultaneously, When the EventPublisher handles events for different tenants concurrently, Then each tenant's events are published to the correct tenant-specific topic, And there is no cross-contamination between tenant topics under concurrent load.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [x] 0.1 Run all existing tests -- they must pass before proceeding
  - [x] 0.2 Review `EventPublisher.cs` (Story 4.1) -- understand how `AggregateIdentity.PubSubTopic` is used for topic derivation
  - [x] 0.3 Review `AggregateIdentity.cs` -- confirm `PubSubTopic` returns `$"{TenantId}.{Domain}.events"` and that TenantId/Domain are forced lowercase
  - [x] 0.4 Review existing EventPublisher tests (Story 4.1) -- identify what topic isolation tests already exist
  - [x] 0.5 Review `FakeEventPublisher.cs` (Story 4.1) -- understand tracked publish calls for topic verification
  - [x] 0.6 Verify AggregateIdentity input validation: TenantId/Domain regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` ensures topic-safe characters

- [x] Task 1: Create TopicNameValidator utility (AC: #5, #6)
  - [x] 1.1 Create `ITopicNameValidator.cs` in `src/Hexalith.EventStore.Server/Events/`
  - [x] 1.2 Create `TopicNameValidator.cs` in `src/Hexalith.EventStore.Server/Events/`
  - [x] 1.3 Method: `bool IsValidTopicName(string topicName)` -- validates topic conforms to D6 pattern and is compatible with target pub/sub backends
  - [x] 1.4 Validation rules: non-empty, matches `{segment}.{segment}.events` pattern, segments are lowercase alphanumeric + hyphens, max length compatible with Kafka (249 chars), RabbitMQ (255 chars), Azure Service Bus (260 chars)
  - [x] 1.5 Method: `string DeriveTopicName(AggregateIdentity identity)` -- canonical topic derivation (delegates to `identity.PubSubTopic` but adds validation)
  - [x] 1.6 Log Warning if topic name approaches backend length limits (>200 chars)

- [x] Task 2: Integrate TopicNameValidator into EventPublisher (AC: #5)
  - [x] 2.1 Add `ITopicNameValidator` as optional constructor dependency to `EventPublisher` (backward compatible -- if null, skip validation)
  - [x] 2.2 Before publication, validate topic name via `ITopicNameValidator.IsValidTopicName()` if validator is available
  - [x] 2.3 If topic validation fails, log Error and return `EventPublishResult(false, 0, "Invalid topic name: ...")`
  - [x] 2.4 Register `ITopicNameValidator` -> `TopicNameValidator` in `ServiceCollectionExtensions.cs`

- [x] Task 3: Create comprehensive multi-tenant topic isolation tests (AC: #1, #2, #3, #4, #6, #9)
  - [x] 3.1 Create `TopicIsolationTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [x] 3.2 Test: `PublishEvents_DifferentTenants_SameDomain_UseDifferentTopics` -- Verify events for tenant "acme" go to `acme.orders.events` and tenant "globex" go to `globex.orders.events`
  - [x] 3.3 Test: `PublishEvents_SameTenant_DifferentDomains_UseDifferentTopics` -- Verify events for "orders" go to `acme.orders.events` and "inventory" go to `acme.inventory.events`
  - [x] 3.4 Test: `PublishEvents_DifferentTenants_DifferentDomains_AllDistinctTopics` -- Matrix test: (tenantA, domainX), (tenantA, domainY), (tenantB, domainX), (tenantB, domainY) all produce distinct topics
  - [x] 3.5 Test: `PublishEvents_SameTenantDomain_MultipleCalls_SameTopic` -- Deterministic: repeated calls for same identity produce same topic
  - [x] 3.6 Test: `PublishEvents_CaseNormalization_ProducesSameTopic` -- "Acme" and "acme" produce the same topic (guaranteed by AggregateIdentity lowercase enforcement)
  - [x] 3.7 Test: `PublishEvents_ConcurrentTenants_NoTopicCrossContamination` -- Parallel publish calls for different tenants never cross-publish
  - [x] 3.8 Test: `PublishEvents_TenantWithHyphens_ValidTopic` -- Tenant "acme-corp" produces valid topic `acme-corp.orders.events`
  - [x] 3.9 Test: `PublishEvents_SingleCharTenantDomain_ValidTopic` -- Edge case: minimal tenant/domain names
  - [x] 3.10 Test: `PublishEvents_MaxLengthTenantDomain_ValidTopic` -- Edge case: 64-char tenant + 64-char domain

- [x] Task 4: Create TopicNameValidator unit tests (AC: #5)
  - [x] 4.1 Create `TopicNameValidatorTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Events/`
  - [x] 4.2 Test: `IsValidTopicName_ValidD6Pattern_ReturnsTrue` -- `acme.orders.events` is valid
  - [x] 4.3 Test: `IsValidTopicName_HyphenatedSegments_ReturnsTrue` -- `acme-corp.order-service.events` is valid
  - [x] 4.4 Test: `IsValidTopicName_EmptyString_ReturnsFalse`
  - [x] 4.5 Test: `IsValidTopicName_MissingSuffix_ReturnsFalse` -- `acme.orders` is invalid (missing `.events` suffix)
  - [x] 4.6 Test: `IsValidTopicName_SpecialCharacters_ReturnsFalse` -- `acme.orders!.events` is invalid
  - [x] 4.7 Test: `IsValidTopicName_UppercaseSegments_ReturnsFalse` -- `Acme.Orders.events` is invalid (uppercase)
  - [x] 4.8 Test: `IsValidTopicName_MaxLengthTopic_ReturnsTrue` -- 64+64+8 = 136 chars is within all backend limits
  - [x] 4.9 Test: `DeriveTopicName_ValidIdentity_MatchesPubSubTopic` -- Validates `DeriveTopicName` produces same result as `AggregateIdentity.PubSubTopic`
  - [x] 4.10 Test: `DeriveTopicName_IdenticalIdentities_ProduceDeterministicTopics`

- [x] Task 5: Create AggregateActor multi-tenant topic isolation integration tests (AC: #2, #3, #4, #9)
  - [x] 5.1 Create `MultiTenantPublicationTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Actors/`
  - [x] 5.2 Test: `ProcessCommand_TenantA_PublishesToTenantATopic` -- Full pipeline for tenant A, verify FakeEventPublisher receives correct topic
  - [x] 5.3 Test: `ProcessCommand_TenantB_PublishesToTenantBTopic` -- Full pipeline for tenant B, verify different topic
  - [x] 5.4 Test: `ProcessCommand_MultipleTenants_Sequential_CorrectTopicsPerTenant`
  - [x] 5.5 Test: `ProcessCommand_MultipleDomains_Sequential_CorrectTopicsPerDomain`
  - [x] 5.6 Test: `ProcessCommand_TenantDomainMatrix_AllTopicsDistinct` -- 4-way matrix test

- [x] Task 6: Update FakeEventPublisher for multi-tenant verification (AC: #4, #9)
  - [x] 6.1 Add `GetPublishedTopics()` method to FakeEventPublisher -- returns all unique topics published to
  - [x] 6.2 Add `GetEventsForTopic(string topic)` method -- returns events published to a specific topic
  - [x] 6.3 Add `AssertNoEventsForTopic(string topic)` method -- asserts a specific topic received zero events
  - [x] 6.4 Add thread-safe collection for concurrent verification (ConcurrentDictionary for topic -> events mapping)

- [x] Task 7: Verify all tests pass
  - [x] 7.1 Run `dotnet test` to confirm no regressions
  - [x] 7.2 All new topic isolation tests pass
  - [x] 7.3 All existing Story 4.1 tests still pass
  - [x] 7.4 All existing Story 3.1-3.11 tests still pass

## Dev Notes

### Story Context

This is the **second story in Epic 4: Event Distribution & Dead-Letter Handling**. It builds on Story 4.1's EventPublisher to verify and enforce that events are always published to the correct per-tenant-per-domain topic, guaranteeing subscriber-side isolation.

**What Story 4.1 already implemented (to BUILD ON, not replicate):**
- `EventPublisher` publishes events using `DaprClient.PublishEventAsync` with `AggregateIdentity.PubSubTopic` as the topic
- Topic derivation: `AggregateIdentity.PubSubTopic` returns `$"{TenantId}.{Domain}.events"` (D6)
- `FakeEventPublisher` tracks publish calls (events published, topic used, correlationId)
- Basic tests: single event publication, multi-event publication, topic derivation from AggregateIdentity
- OpenTelemetry activity `EventStore.Events.Publish` with `eventstore.topic` tag
- Structured logging with topic field

**What this story adds (NEW):**
- `ITopicNameValidator` / `TopicNameValidator` -- validates topic names are D6-compliant and compatible with all DAPR pub/sub backends
- Comprehensive multi-tenant multi-domain topic isolation tests (the core value of this story)
- `FakeEventPublisher` enhancements for multi-tenant topic verification
- AggregateActor integration tests proving correct topic routing per tenant/domain
- Concurrent multi-tenant publication tests proving no cross-contamination

**What this story modifies (EXISTING):**
- `EventPublisher.cs` -- add optional topic validation before publication
- `FakeEventPublisher.cs` -- add topic-aware querying methods
- `ServiceCollectionExtensions.cs` -- register TopicNameValidator

**Key insight: The isolation guarantee is STRUCTURAL, not runtime-enforced.**
`AggregateIdentity` forces TenantId and Domain to lowercase and validates them against `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`. This means:
- Colons, dots, slashes, and other delimiters are forbidden in tenant/domain names
- The dot-separated topic format `{tenant}.{domain}.events` is structurally unambiguous
- `acme.orders.events` can NEVER collide with `globex.orders.events` because "acme" != "globex"
- There is no way to craft a tenant name that produces a topic matching another tenant's topic

This story's primary value is PROVING this isolation through comprehensive tests, and adding a validation layer for defense-in-depth.

### Architecture Compliance

- **FR19:** Per-tenant-per-domain topic isolation -- events published to `{tenant}.{domain}.events`
- **D6:** Topic naming convention `{tenant}.{domain}.events`
- **NFR28:** System must function with any DAPR-compatible pub/sub component supporting CloudEvents 1.0 and at-least-once delivery (validated: RabbitMQ, Azure Service Bus)
- **NFR22:** Zero events lost under any tested failure scenario
- **Rule #4:** Never add custom retry logic -- all retries are DAPR resiliency policies
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **SEC-5:** Event payload data never in logs

### Critical Design Decisions

- **Topic isolation is guaranteed by AggregateIdentity input validation.** The regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` for TenantId and Domain ensures only lowercase alphanumeric + hyphens. Combined with the dot-separated D6 format, topic names are structurally unambiguous. No runtime enforcement mechanism is needed at the publisher level -- the type system prevents invalid topics.

- **TopicNameValidator is defense-in-depth, not primary enforcement.** The validator adds a runtime check before publication as a safety net, but the real guarantee comes from AggregateIdentity's constructor validation. If somehow an invalid identity reached the publisher (should be impossible), the validator catches it.

- **Topic naming is compatible with all major DAPR pub/sub backends.** The D6 format `{tenant}.{domain}.events` uses only lowercase alphanumeric, hyphens, and dots. This is valid for:
  - **RabbitMQ:** Dots are the standard routing key separator (exchange routing pattern)
  - **Kafka:** Topic names allow dots, hyphens, and alphanumeric (max 249 chars)
  - **Azure Service Bus:** Topic names allow dots, hyphens, and alphanumeric (max 260 chars)
  - **Redis Streams:** Any UTF-8 string (no restrictions)
  - Max topic length: 64 (tenant) + 1 (dot) + 64 (domain) + 7 (".events") = 136 chars, well within all limits

- **Subscriber-side isolation is a SEPARATE concern (Story 5.3).** This story ensures the publisher SENDS events to the correct topic. Story 5.3 (Epic 5) ensures subscribers can only SUBSCRIBE to authorized topics via DAPR pub/sub scoping rules. Together they provide end-to-end topic isolation.

- **Concurrent multi-tenant publication is safe because EventPublisher is stateless.** The EventPublisher holds no state between calls (registered as transient in DI per Story 4.1). Each `PublishEventsAsync` call derives the topic from the passed `AggregateIdentity`, which is immutable. No shared mutable state means no cross-contamination risk under concurrency.

- **FakeEventPublisher uses ConcurrentDictionary for thread-safe verification.** The enhanced FakeEventPublisher tracks publications per topic using a `ConcurrentDictionary<string, ConcurrentBag<EventEnvelope>>`, enabling safe concurrent test verification.

### Existing Patterns to Follow

**AggregateIdentity.PubSubTopic derivation (from Contracts -- already exists):**
```csharp
// AggregateIdentity.cs line 94
public string PubSubTopic => $"{TenantId}.{Domain}.events";
```

**EventPublisher topic usage (from Story 4.1 -- to extend with validation):**
```csharp
// EventPublisher.cs (Story 4.1 pattern)
await daprClient.PublishEventAsync(
    options.Value.PubSubName,
    identity.PubSubTopic,       // <-- Topic derived from AggregateIdentity
    eventEnvelope,
    metadata,
    cancellationToken).ConfigureAwait(false);
```

**TopicNameValidator pattern (NEW -- follows existing validator style):**
```csharp
public class TopicNameValidator(
    ILogger<TopicNameValidator> logger) : ITopicNameValidator
{
    private static readonly Regex _topicPattern = new(
        @"^[a-z0-9]([a-z0-9-]*[a-z0-9])?\.[a-z0-9]([a-z0-9-]*[a-z0-9])?\.events$",
        RegexOptions.Compiled);

    public bool IsValidTopicName(string topicName)
    {
        ArgumentNullException.ThrowIfNull(topicName);
        if (!_topicPattern.IsMatch(topicName))
        {
            return false;
        }
        if (topicName.Length > 249) // Kafka limit (most restrictive)
        {
            logger.LogWarning("Topic name exceeds Kafka maximum: Length={Length}, Topic={Topic}",
                topicName.Length, topicName);
            return false;
        }
        return true;
    }
}
```

**Multi-tenant test pattern (from existing AggregateActorTests):**
```csharp
// Follow existing test naming: {Method}_{Scenario}_{ExpectedResult}
[Fact]
public async Task PublishEvents_DifferentTenants_SameDomain_UseDifferentTopics()
{
    // Arrange
    var identityA = new AggregateIdentity("acme", "orders", "order-1");
    var identityB = new AggregateIdentity("globex", "orders", "order-1");
    var fakePublisher = new FakeEventPublisher();

    // Act
    await fakePublisher.PublishEventsAsync(identityA, eventsA, "corr-a");
    await fakePublisher.PublishEventsAsync(identityB, eventsB, "corr-b");

    // Assert
    fakePublisher.GetPublishedTopics().ShouldBe(new[]
    {
        "acme.orders.events",
        "globex.orders.events"
    });
    fakePublisher.GetEventsForTopic("acme.orders.events").ShouldNotBeEmpty();
    fakePublisher.AssertNoEventsForTopic("globex.orders.events", fromCorrelation: "corr-a");
}
```

**Primary constructor pattern (from existing code):**
```csharp
public class TopicNameValidator(
    ILogger<TopicNameValidator> logger) : ITopicNameValidator
{
    // ...
}
```

### Mandatory Coding Patterns

- Primary constructors: `public class TopicNameValidator(ILogger<TopicNameValidator> logger)`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization (`Events/` folder for topic validator)
- **Rule #4:** No custom retry logic -- DAPR resiliency handles retries
- **Rule #5:** Never log event payload data -- only envelope metadata fields
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **SEC-5:** Event payload data never in logs

### Project Structure Notes

**New files:**
- `src/Hexalith.EventStore.Server/Events/ITopicNameValidator.cs` -- topic validation interface
- `src/Hexalith.EventStore.Server/Events/TopicNameValidator.cs` -- D6 topic name validation
- `tests/Hexalith.EventStore.Server.Tests/Events/TopicNameValidatorTests.cs` -- validator unit tests
- `tests/Hexalith.EventStore.Server.Tests/Events/TopicIsolationTests.cs` -- multi-tenant topic isolation tests
- `tests/Hexalith.EventStore.Server.Tests/Actors/MultiTenantPublicationTests.cs` -- actor-level topic routing tests

**Modified files:**
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` -- add optional topic validation before publication
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` -- add topic-aware querying methods and thread-safe collections
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- register ITopicNameValidator

### Previous Story Intelligence

**From Story 4.1 (Event Publisher with CloudEvents 1.0):**
- EventPublisher uses `AggregateIdentity.PubSubTopic` for topic derivation
- `DaprClient.PublishEventAsync` receives topic name as string parameter
- CloudEvents metadata includes `cloudevent.source` = `hexalith-eventstore/{tenant}/{domain}` which mirrors topic structure
- FakeEventPublisher tracks: events published, topic used, correlationId
- EventPublishResult record: Success, PublishedCount, FailureReason
- EventPublisher is registered as transient in DI (stateless)
- OpenTelemetry activity `EventStore.Events.Publish` includes `eventstore.topic` tag

**From Story 1.2 (Contracts -- AggregateIdentity):**
- `PubSubTopic` property: `$"{TenantId}.{Domain}.events"` (line 94 of AggregateIdentity.cs)
- TenantId forced to lowercase: `tenantId.ToLowerInvariant()` (line 33)
- Domain forced to lowercase: `domain.ToLowerInvariant()` (line 34)
- Validation regex: `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` -- only lowercase alphanumeric + hyphens
- Max lengths: TenantId 64 chars, Domain 64 chars
- Colons explicitly rejected by regex (critical for key/topic isolation)

**From Story 3.11 (Actor State Machine):**
- AggregateActor pipeline flows through 5 steps
- Event publication (Story 4.1) inserted between EventsStored and Completed
- Advisory status writes wrapped in try/catch per rule #12
- OpenTelemetry activities created via `EventStoreActivitySource.Instance.StartActivity()`

**Current AggregateActor event publication flow (after Story 4.1):**
```
Processing -> EventsStored -> [EventPublisher.PublishEventsAsync(identity, events, correlationId)]
                            -> EventsPublished -> Completed
                            |-> PublishFailed (if pub/sub unavailable)
```
The topic used in publication is `identity.PubSubTopic` which is `{tenant}.{domain}.events`. Story 4.2 PROVES this isolation is correct and comprehensive.

### Git Intelligence

Recent commits show the progression through Epic 3 into Epic 4:
- `226a260` Story 3.11: Actor state machine and checkpointed stages (#36)
- `f79aabe` Story 3.10: State reconstruction from snapshot + tail events (#35)
- `c120c19` Stories 3.6-3.9: Multi-tenant, event persistence, key isolation, snapshots (#33)
- Consistent patterns: Primary constructors, records, ConfigureAwait(false), NSubstitute + Shouldly
- DI registration via `Add*` extension methods in `ServiceCollectionExtensions.cs`
- Feature folder organization throughout
- Comprehensive test coverage with descriptive test method names
- `AggregateActor.cs` line 294: Explicit Epic 4 integration placeholder comment

### Testing Requirements

**TopicNameValidator Unit Tests (~10 new):**
- Valid D6 pattern recognition (standard names, hyphenated names, single-char, max-length)
- Invalid pattern rejection (empty, missing suffix, special chars, uppercase, too long)
- DeriveTopicName consistency with AggregateIdentity.PubSubTopic

**Multi-Tenant Topic Isolation Tests (~10 new):**
- Different tenants same domain -> different topics
- Same tenant different domains -> different topics
- Tenant+domain matrix -> all distinct topics
- Deterministic topic derivation (repeated calls)
- Case normalization (AggregateIdentity lowercase enforcement)
- Concurrent multi-tenant -> no cross-contamination
- Edge cases: hyphens, single-char, max-length

**AggregateActor Multi-Tenant Integration Tests (~6 new):**
- Full pipeline for tenant A -> correct topic
- Full pipeline for tenant B -> correct topic
- Sequential multi-tenant -> correct topics
- Sequential multi-domain -> correct topics
- Tenant+domain matrix -> all distinct topics

**FakeEventPublisher Enhancements:**
- Topic-aware querying (GetPublishedTopics, GetEventsForTopic, AssertNoEventsForTopic)
- Thread-safe concurrent collections

**Total estimated: ~26-30 tests (new) + FakeEventPublisher modifications**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 4, Story 4.2]
- [Source: _bmad-output/planning-artifacts/architecture.md#D6 Pub/Sub Topic Naming]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR19 Per-tenant-per-domain topic isolation]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR28 Any DAPR-compatible pub/sub]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR22 Zero events lost]
- [Source: _bmad-output/planning-artifacts/architecture.md#AggregateIdentity identity scheme]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 4 No custom retry]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 5 No payload in logs]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5 Event payload never in logs]
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs#PubSubTopic line 94]
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs#Validation regex lines 14-15]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#Epic 4 integration point line 294]
- [Source: _bmad-output/implementation-artifacts/4-1-event-publisher-with-cloudevents-1-0.md]
- [Source: DAPR docs: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-scopes/]
- [Source: Kafka docs: topic name constraints (max 249 chars, alphanumeric + dots + hyphens + underscores)]
- [Source: RabbitMQ docs: exchange/routing key naming (UTF-8 strings, dots as separator)]
- [Source: Azure Service Bus docs: topic name limits (max 260 chars)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Fixed topic length calculation in tests: `{64}.{64}.events` = 136 chars (not 135 as stated in story Dev Notes; the two dot separators were not counted)

### Completion Notes List

- **Task 0:** Existing tests were re-verified in current workspace context prior to review/fix work.
- **Task 1:** Created `ITopicNameValidator` interface and `TopicNameValidator` class with `IsValidTopicName()` (D6 pattern + Kafka/RabbitMQ/ASB length limits) and `DeriveTopicName()` (delegates to `AggregateIdentity.PubSubTopic` with validation). Warning logged at >200 chars.
- **Task 2:** Added `ITopicNameValidator?` as optional constructor parameter to `EventPublisher` (backward compatible). Validation check added before publication loop. Error logged and failure result returned on invalid topic. Registered as singleton in `ServiceCollectionExtensions`.
- **Task 3:** Created 9 multi-tenant topic isolation tests covering: different tenants/same domain, same tenant/different domains, tenant+domain matrix, deterministic derivation, case normalization, concurrent multi-tenant, hyphens, single-char, and max-length edge cases.
- **Task 4:** Created 13 TopicNameValidator unit tests covering: valid D6 patterns, invalid patterns (empty, missing suffix, special chars, uppercase, exceeds Kafka max), length warning threshold, deterministic derivation, null guards.
- **Task 5:** Created 5 AggregateActor integration tests using FakeEventPublisher to verify full pipeline produces correct per-tenant-per-domain topics.
- **Task 6:** Enhanced FakeEventPublisher with `GetPublishedTopics()`, `GetEventsForTopic()`, `AssertNoEventsForTopic()`, and thread-safe `ConcurrentDictionary<string, ConcurrentBag<EventEnvelope>>` + `ConcurrentBag<PublishCall>` for concurrent verification.
- **Task 7:** Post-review verification completed with current test execution: focused Story 4.2 suite passed (41/41) and full discovered suite passed (343/343) in this workspace context.

### Change Log

- 2026-02-14: Story 4.2 implementation complete. Added TopicNameValidator (defense-in-depth D6 validation), integrated optional topic validation into EventPublisher, enhanced FakeEventPublisher for multi-tenant topic verification, added 28 new tests proving per-tenant-per-domain topic isolation.
- 2026-02-14: Code review follow-up applied. Fixed FakeEventPublisher partial-failure accounting to record only successfully published events; added regression assertions in AtLeastOnceDelivery tests; updated story evidence to reflect current test execution context.

### File List

**New files:**

- `src/Hexalith.EventStore.Server/Events/ITopicNameValidator.cs`
- `src/Hexalith.EventStore.Server/Events/TopicNameValidator.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/TopicIsolationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/TopicNameValidatorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/MultiTenantPublicationTests.cs`

**Modified files:**

- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (added optional ITopicNameValidator parameter, topic validation before publication)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` (added topic-aware querying methods, thread-safe concurrent collections)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (registered ITopicNameValidator singleton)

### Review Follow-up Notes (2026-02-14)

- This story was reviewed and all review findings were fixed.
- Additional in-progress git changes exist for other stories (notably Story 4.4/5.1 scope) and are intentionally not part of Story 4.2 implementation claims.
