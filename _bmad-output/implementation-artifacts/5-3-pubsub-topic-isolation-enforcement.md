# Story 5.3: Pub/Sub Topic Isolation Enforcement

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**This is the third story in Epic 5: Multi-Tenant Security & Access Control Enforcement.**

Story 5.1 (DAPR Access Control Policies) must be complete -- it established the DAPR-level access control policies, pub/sub component scoping, and state store scoping. Story 5.2 (Data Path Isolation Verification) should be complete -- it validated the three-layer isolation model for command routing and actor processing. This story (5.3) focuses specifically on enforcing that event subscribers only receive events from tenants they are authorized to access, completing the pub/sub isolation guarantee.

Verify these files/resources exist before starting:
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (existing -- publishes events to per-tenant-per-domain topics)
- `src/Hexalith.EventStore.Server/Events/IEventPublisher.cs` (existing -- publisher interface)
- `src/Hexalith.EventStore.Server/Events/TopicNameValidator.cs` (existing -- validates D6 topic format)
- `src/Hexalith.EventStore.Server/Events/ITopicNameValidator.cs` (existing -- validator interface)
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` (existing -- dead-letter publication)
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` (existing -- PubSubName, DeadLetterTopicPrefix)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (existing -- PubSubTopic property: `{tenant}.{domain}.events`)
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` (existing -- should have scoping from Story 5.1)
- `deploy/dapr/pubsub-rabbitmq.yaml` (existing -- should have scoping from Story 5.1)
- `deploy/dapr/pubsub-kafka.yaml` (existing -- should have scoping from Story 5.1)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` (existing -- test helper with topic tracking)
- `tests/Hexalith.EventStore.Server.Tests/Events/TopicIsolationTests.cs` (existing -- Story 4.2 topic isolation tests)
- `tests/Hexalith.EventStore.Server.Tests/Actors/MultiTenantPublicationTests.cs` (existing -- actor-level topic routing tests)

Run `dotnet test` to confirm all existing tests pass before beginning.

## Story

As a **security auditor**,
I want pub/sub topic isolation enforced so that event subscribers only receive events from tenants they are authorized to access,
So that cross-tenant event leakage is impossible (FR29).

## Acceptance Criteria

1. **DAPR pub/sub scoping rules restrict subscriber app-ids to authorized topics** - Given events are published to per-tenant-per-domain topics (Story 4.2, D6 pattern `{tenant}.{domain}.events`), When a subscriber subscribes to a tenant's topic, Then DAPR pub/sub scoping rules restrict which app-ids can subscribe to which topics.

2. **Subscriber authorized for tenantA cannot subscribe to tenantB's topics** - Given subscriber app "subscriber-a" is authorized for tenant "acme" topics only, When subscriber-a attempts to subscribe to `globex.orders.events`, Then DAPR rejects the subscription because subscriber-a is not in the subscription scoping for globex topics.

3. **Subscription scoping configured via DAPR component metadata (not application code)** - Given the pub/sub scoping is defined in DAPR component YAML files (`pubsub.yaml`, `pubsub-rabbitmq.yaml`, `pubsub-kafka.yaml`), When the system is deployed, Then subscription scoping is enforced by the DAPR sidecar based on component metadata, And no application code changes are required to add or restrict subscriber access.

4. **Unauthorized subscription attempts rejected by DAPR** - Given a subscriber app-id is not listed in the subscription scoping for a topic, When it attempts to subscribe, Then DAPR rejects the subscription at the sidecar level, And the rejection is observable in DAPR sidecar logs.

5. **Publisher-side scoping already enforced (Story 5.1 verification)** - Given Story 5.1 configured `publishingScopes` restricting event topic publishing to `commandapi` only, When this story's tests run, Then tests verify the publisher-side scoping is still correctly configured and has not regressed.

6. **Dead-letter topics follow same isolation pattern** - Given dead-letter topics use the pattern `deadletter.{tenant}.{domain}.events`, When subscription scoping is configured for dead-letter topics, Then only authorized operational app-ids can subscribe to dead-letter topics, And subscriber app-ids authorized for regular event topics cannot automatically access dead-letter topics (separate authorization).

7. **Subscription scoping configuration documented for onboarding** - Given a new subscriber service needs to receive events for specific tenants, When a DevOps engineer reviews the DAPR component YAML, Then clear comments document: how to add a new subscriber app-id, how to scope it to specific tenant topics, the difference between `publishingScopes`, `subscriptionScopes`, and `allowedTopics`, and the procedure for granting/revoking tenant topic access.

8. **Local and production configurations have consistent subscriber scoping topology** - Given local (`AppHost/DaprComponents/pubsub.yaml`) and production (`deploy/dapr/pubsub-*.yaml`) configurations exist, When both are compared, Then the subscriber scoping topology is logically consistent (same isolation model, same restriction patterns), And production configs use placeholder app-ids with documentation for deployment-time substitution.

9. **Topic isolation tests verify subscriber cannot access unauthorized tenant events** - Given automated tests exist in `tests/Hexalith.EventStore.Server.Tests/`, When `dotnet test` runs, Then tests verify: subscription scoping configuration is present in all pub/sub YAML files, subscriber app-ids are restricted per configuration, dead-letter topic subscription is separately scoped, and local/production configs are topologically consistent.

10. **Dynamic tenant provisioning does not require YAML changes for the publisher** - Given NFR20 requires no system restart for new tenants, When a new tenant is onboarded, Then the `commandapi` publisher can publish to the new tenant's topic without YAML changes (wildcard or unrestricted publish scope), And subscriber access for the new tenant requires only adding the subscriber app-id to the tenant's subscription scope in the YAML config (acceptable operational step documented).

11. **Subscription scoping strategy handles the static YAML vs dynamic topics tension** - Given DAPR pub/sub scoping uses static YAML while tenant topics are dynamic, When the subscription scoping strategy is designed, Then it uses a documented approach that balances security with operational flexibility: either allowedTopics patterns, per-subscriber broad grants with application-level filtering, or a documented operational procedure for updating scopes when tenants change.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [x] 0.1 Run all existing tests -- they must pass before proceeding
  - [x] 0.2 Review existing `AppHost/DaprComponents/pubsub.yaml` -- understand scoping added by Story 5.1 (publishingScopes, scopes)
  - [x] 0.3 Review existing `deploy/dapr/pubsub-rabbitmq.yaml` -- understand production pub/sub scoping from Story 5.1
  - [x] 0.4 Review existing `deploy/dapr/pubsub-kafka.yaml` -- understand production pub/sub scoping from Story 5.1
  - [x] 0.5 Review existing `TopicIsolationTests.cs` and `MultiTenantPublicationTests.cs` -- understand what publisher-side isolation is already tested
  - [x] 0.6 Review Story 5.1 `AccessControlPolicyTests.cs` -- understand what pub/sub scoping tests already exist
  - [x] 0.7 Research DAPR 1.16 pub/sub subscription scoping behavior:
    - How `subscriptionScopes` metadata works per pub/sub component type (Redis, RabbitMQ, Kafka)
    - How `allowedTopics` restricts which topics an app can subscribe to
    - Whether wildcard patterns are supported in subscription scoping
    - How DAPR rejects unauthorized subscriptions (error behavior, logging)
  - [x] 0.8 Identify the gap between Story 5.1's publisher-side scoping and this story's subscriber-side scoping

- [x] Task 1: Enhance subscriber-side scoping in local pub/sub configuration (AC: #1, #2, #3, #4, #10, #11)
  - [x] 1.1 Review and enhance `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`:
    - Verify `publishingScopes` from Story 5.1 is correct: only `commandapi` can publish
    - Add or enhance `subscriptionScopes` metadata to demonstrate tenant-scoped subscriber access
    - Add example subscriber entries showing how a subscriber would be scoped to specific tenants
    - Document the dynamic topic tension: publisher has wildcard access, subscribers are per-tenant
  - [x] 1.2 Add `allowedTopics` or `subscriptionScopes` entries showing:
    - `commandapi` can subscribe to any topic (for internal needs if applicable)
    - Example subscriber app-id scoped to specific tenant topics only
    - Dead-letter topics scoped separately from regular event topics
  - [x] 1.3 CRITICAL: Understand DAPR pub/sub scoping limitations:
    - DAPR `scopes` field on Component controls which app-ids can USE the component at all
    - `publishingScopes` and `subscriptionScopes` are metadata-level controls that may not be supported by all pub/sub components
    - `allowedTopics` restricts which topics an app can subscribe to
    - If DAPR pub/sub scoping is insufficient for dynamic per-tenant topics, document the recommended approach (e.g., separate pub/sub components per tenant, application-level filtering, or operational procedures)
  - [x] 1.4 Document the subscriber scoping strategy in YAML comments:
    - Explain the distinction between publisher-side (Story 5.1) and subscriber-side (this story) scoping
    - Document how to add a new subscriber app-id
    - Document how to grant a subscriber access to a specific tenant's topics
    - Document the operational procedure for new tenant onboarding

- [x] Task 2: Enhance subscriber-side scoping in production pub/sub configurations (AC: #3, #8)
  - [x] 2.1 Enhance `deploy/dapr/pubsub-rabbitmq.yaml`:
    - Add subscriber scoping metadata matching the local development pattern
    - Use placeholder subscriber app-ids with documentation for deployment-time configuration
    - Add comments explaining how to scope subscribers per tenant
    - Document RabbitMQ-specific subscription scoping behavior (exchanges, bindings, topic permissions)
  - [x] 2.2 Enhance `deploy/dapr/pubsub-kafka.yaml`:
    - Add subscriber scoping metadata matching the local development pattern
    - Use placeholder subscriber app-ids with documentation for deployment-time configuration
    - Add comments explaining how to scope subscribers per tenant
    - Document Kafka-specific subscription scoping behavior (consumer groups, ACLs, topic authorization)
  - [x] 2.3 Ensure dead-letter topic subscription scoping is consistent across all configs:
    - Dead-letter topics (`deadletter.{tenant}.{domain}.events`) should have separate subscriber scoping from regular event topics
    - Only authorized operational/monitoring app-ids should subscribe to dead-letter topics
  - [x] 2.4 Add documentation comments to each YAML explaining:
    - The subscriber scoping strategy
    - How to add/remove subscriber access
    - The difference between local (development) and production scoping
    - NFR20 implications for dynamic tenant provisioning

- [x] Task 3: Create PubSubTopicIsolationEnforcementTests.cs (AC: #1, #2, #5, #6, #9)
  - [x] 3.1 Create `tests/Hexalith.EventStore.Server.Tests/Security/PubSubTopicIsolationEnforcementTests.cs`
  - [x] 3.2 Test: `LocalPubSubYaml_HasSubscriptionScoping_RestrictsSubscribers` -- Verify local pubsub.yaml contains subscription scoping metadata (subscriptionScopes or allowedTopics)
  - [x] 3.3 Test: `LocalPubSubYaml_PublishingScopes_OnlyCommandApiCanPublish` -- Regression test: verify Story 5.1's publisher scoping is intact (commandapi only can publish)
  - [x] 3.4 Test: `LocalPubSubYaml_DeadLetterTopics_SeparateSubscriptionScope` -- Verify dead-letter topic subscription is scoped separately from regular event topics
  - [x] 3.5 Test: `ProductionRabbitMqYaml_HasSubscriptionScoping_RestrictsSubscribers` -- Verify production RabbitMQ config has subscriber scoping
  - [x] 3.6 Test: `ProductionKafkaYaml_HasSubscriptionScoping_RestrictsSubscribers` -- Verify production Kafka config has subscriber scoping
  - [x] 3.7 Test: `AllPubSubConfigs_SubscriptionScopingTopology_Consistent` -- Verify local and production configs have the same logical subscriber scoping patterns
  - [x] 3.8 Test: `AllPubSubConfigs_CommandApiPublishScope_NotRestricted` -- Verify commandapi can publish to any topic (wildcard or unrestricted) for NFR20 dynamic tenant support
  - [x] 3.9 Test: `AllPubSubConfigs_DomainServices_NoPubSubAccess` -- Regression test: verify domain services (`sample`) have no pub/sub access at all (Story 5.1 enforcement)

- [x] Task 4: Create SubscriptionScopingDocumentationTests.cs (AC: #7, #8, #10, #11)
  - [x] 4.1 Create `tests/Hexalith.EventStore.Server.Tests/Security/SubscriptionScopingDocumentationTests.cs`
  - [x] 4.2 Test: `LocalPubSubYaml_ContainsSubscriberOnboardingDocumentation` -- Verify YAML comments document how to add a new subscriber
  - [x] 4.3 Test: `LocalPubSubYaml_ContainsTenantScopingDocumentation` -- Verify YAML comments document how to scope a subscriber to specific tenants
  - [x] 4.4 Test: `ProductionPubSubYamls_ContainDeploymentSubstitutionGuidance` -- Verify production configs document placeholder values and deployment-time substitution
  - [x] 4.5 Test: `AllPubSubConfigs_DocumentDynamicTenantStrategy` -- Verify YAML comments document the NFR20 dynamic tenant provisioning strategy and its interaction with static YAML scoping

- [x] Task 5: Verify TopicNameValidator prevents cross-tenant topic confusion (AC: #1, #2)
  - [x] 5.1 Review existing `TopicIsolationTests.cs` for coverage of tenant topic isolation
  - [x] 5.2 If not already covered, add tests to verify:
    - TopicNameValidator rejects topic names that don't match D6 pattern
    - Different tenants always produce structurally disjoint topic names
    - Topic names are deterministic (same identity always produces same topic)
  - [x] 5.3 NOTE: Most of this is already covered by existing Story 4.2 tests. Only add tests if gaps are identified.

- [x] Task 6: Verify no regressions and full pipeline works (AC: #5, #8, #9)
  - [x] 6.1 Run `dotnet test` to confirm all existing + new tests pass
  - [x] 6.2 Verify that Story 5.1 publisher scoping tests still pass (regression check)
  - [x] 6.3 Verify that Story 4.2 topic isolation tests still pass (regression check)
  - [x] 6.4 Confirm the subscriber scoping strategy is documented consistently across all YAML files

## Dev Notes

### Story Context

This is the **third story in Epic 5: Multi-Tenant Security & Access Control Enforcement**. It completes the pub/sub isolation guarantee by focusing on the **subscriber side** of topic access control. Story 4.2 implemented per-tenant-per-domain topic naming. Story 5.1 implemented publisher-side scoping (only `commandapi` can publish). This story adds subscriber-side scoping to prevent unauthorized event consumption.

**What already exists (BUILD ON, not replicate):**
- Per-tenant-per-domain topic naming: `{tenant}.{domain}.events` (Story 4.2, D6)
- EventPublisher publishes to `AggregateIdentity.PubSubTopic` (Story 4.1)
- DeadLetterPublisher publishes to `deadletter.{tenant}.{domain}.events` (Story 4.5)
- TopicNameValidator enforces D6 topic format (Story 4.2)
- Publisher-side scoping: only `commandapi` can publish (Story 5.1)
- Component-level scoping: only `commandapi` and authorized apps in `scopes` (Story 5.1)
- Domain services have zero pub/sub access (Story 5.1)
- Comprehensive topic isolation tests (Story 4.2: `TopicIsolationTests.cs`, `MultiTenantPublicationTests.cs`)
- Pub/sub scoping YAML validation tests (Story 5.1: `AccessControlPolicyTests.cs`)

**What this story adds (NEW):**
- Subscriber-side scoping in all pub/sub YAML configurations
- Dead-letter topic subscriber scoping (separate from regular event topics)
- Subscriber onboarding documentation in YAML comments
- NFR20 dynamic tenant strategy documentation
- Subscriber scoping validation tests
- Local-production consistency verification for subscriber scoping

**What this story modifies (EXISTING):**
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` -- ADD subscriber scoping and documentation
- `deploy/dapr/pubsub-rabbitmq.yaml` -- ADD subscriber scoping and documentation
- `deploy/dapr/pubsub-kafka.yaml` -- ADD subscriber scoping and documentation

### Architecture Compliance

- **FR29:** Pub/sub topic isolation enforcement -- subscribers only receive events from authorized tenants
- **NFR13:** Multi-tenant data isolation enforced at all three layers -- this story completes the pub/sub layer
- **NFR20:** Dynamic tenant provisioning without system restart -- publisher-side unrestricted, subscriber-side requires operational YAML update (documented)
- **NFR28:** Pub/sub must work with any DAPR-compatible component (Redis, RabbitMQ, Azure Service Bus, Kafka)
- **D4:** DAPR access control -- domain services have zero pub/sub access (verified)
- **D6:** Topic naming `{tenant}.{domain}.events` -- subscriber scoping must work with this pattern
- **SEC-5:** Event payload data never in logs -- DAPR subscription scoping does not log payload data
- **Rule #4:** No custom retry logic -- subscription scoping is infrastructure-level, handled by DAPR sidecar

### Critical Design Decisions

- **Publisher-side scoping is DONE (Story 5.1).** This story focuses exclusively on subscriber-side scoping. The `commandapi` app-id already has unrestricted publish access (wildcard) to support NFR20 dynamic tenant provisioning. Do not change publisher scoping.

- **DAPR pub/sub scoping has component-type-specific behavior.** The scoping mechanisms (`subscriptionScopes`, `allowedTopics`, `scopes`) may behave differently across Redis, RabbitMQ, and Kafka. The story must research and document component-specific behavior:
  - **Redis Streams:** `allowedTopics` metadata restricts which topics an app can subscribe to
  - **RabbitMQ:** `allowedTopics` + exchange-level bindings control subscription access
  - **Kafka:** `allowedTopics` + consumer group ACLs control subscription access
  - If `subscriptionScopes` is not natively supported by all components, document the alternative approach

- **Static YAML vs dynamic topics is the core tension (NFR20).** Topic names include tenant IDs (`acme.orders.events`, `globex.orders.events`), so new tenants create new topics. DAPR YAML is static. Resolution:
  - Publisher: wildcard (`commandapi` can publish to any topic) -- already done in Story 5.1
  - Subscriber: must be explicitly granted access per tenant topic. This is an acceptable operational step because:
    1. New subscribers are onboarded infrequently
    2. Subscriber access is a security-critical decision requiring human approval
    3. The YAML change is minimal and well-documented
  - Document this operational procedure clearly in YAML comments

- **Dead-letter topic subscription is SEPARATE from regular event topic subscription.** A subscriber authorized for `acme.orders.events` should NOT automatically be able to subscribe to `deadletter.acme.orders.events`. Dead-letter topics contain failed command payloads and error context -- access should be limited to operational/monitoring tools.

- **No application code changes expected.** This story is primarily YAML configuration + validation tests + documentation. The subscriber scoping is enforced by DAPR sidecar infrastructure, not application code.

- **Example subscriber app-id for demonstration.** Since v1 has no actual subscriber services (the EventStore publishes events, external services subscribe), the YAML configs should include a well-documented example subscriber (e.g., `example-subscriber`) showing the scoping pattern. This serves as documentation and as a template for future integrations.

### DAPR Pub/Sub Scoping Mechanisms

**Component-level scoping (`scopes` field on Component):**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  scopes:
    - commandapi          # Can use this pub/sub component
    - example-subscriber  # External subscriber can also use this component
    # sample (domain service) is NOT listed -- zero pub/sub access
```

**Topic-level scoping (`allowedTopics` metadata per app-id):**
```yaml
spec:
  metadata:
    - name: allowedTopics
      value: "acme.orders.events,acme.inventory.events"
      # Restricts which topics this component allows
```

**Publishing scoping (`publishingScopes`):**
```yaml
spec:
  metadata:
    - name: publishingScopes
      value: "commandapi=*"  # Only commandapi can publish (any topic)
```

**Subscription scoping (`subscriptionScopes`):**
```yaml
spec:
  metadata:
    - name: subscriptionScopes
      value: "example-subscriber=acme.orders.events,acme.inventory.events"
```

> **IMPORTANT: Verify these metadata field names against DAPR 1.16 documentation during Task 0.7.** Field names and behavior vary by pub/sub component type. Some components may use `allowedTopics` per app-id, others may use `subscriptionScopes` as a separate metadata entry. The research in Task 0.7 is critical.

### Hexalith.EventStore Pub/Sub Topology

```
                          DAPR Pub/Sub Scoping
                          =====================

┌──────────────┐
│  commandapi  │ ──── PUBLISH ────► {tenant}.{domain}.events     (any topic - wildcard)
│              │ ──── PUBLISH ────► deadletter.{tenant}.{domain}  (any dead-letter topic)
└──────────────┘

┌──────────────┐
│   sample     │ ──── DENIED ─────► any topic (zero pub/sub access, Story 5.1)
└──────────────┘

┌──────────────────┐
│ example-subscriber│ ── SUBSCRIBE ──► acme.orders.events          (authorized tenant)
│                  │ ── SUBSCRIBE ──► acme.inventory.events        (authorized tenant)
│                  │ ── DENIED ──────► globex.orders.events        (unauthorized tenant)
│                  │ ── DENIED ──────► deadletter.acme.orders.*    (dead-letter separate)
└──────────────────┘

┌──────────────────┐
│  ops-monitor     │ ── SUBSCRIBE ──► deadletter.*                 (authorized for dead-letter)
│                  │ ── DENIED ──────► acme.orders.events          (no regular event access)
└──────────────────┘
```

### Existing Patterns to Follow

**DAPR Component YAML conventions (from existing files):**
- Use `apiVersion: dapr.io/v1alpha1`
- Use `kind: Component` for pub/sub components
- Comments document the architectural decision reference (e.g., "D6", "FR29", "NFR20")
- Environment variable substitution: `{env:VARIABLE|default}`

**Test pattern for YAML validation (from existing `AccessControlPolicyTests.cs` and `ResiliencyConfigurationTests.cs`):**

The test project does NOT have a YAML parsing library. Use string-based validation matching the existing pattern:

```csharp
// File path construction pattern:
private static readonly string LocalPubSubPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "Hexalith.EventStore.AppHost", "DaprComponents", "pubsub.yaml"));

private static readonly string ProductionRabbitMqPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "deploy", "dapr", "pubsub-rabbitmq.yaml"));

[Fact]
public void LocalPubSub_HasSubscriptionScoping_RestrictsSubscribers()
{
    string content = File.ReadAllText(LocalPubSubPath);
    // Verify subscription scoping exists (exact field depends on DAPR research)
    content.ShouldContain("subscriptionScopes");  // or "allowedTopics" per research
}
```

**Test project conventions:**
- NSubstitute for mocking, Shouldly for assertions
- **No YAML parsing library** -- use `File.ReadAllText` + `ShouldContain`/`ShouldNotContain`
- Feature folder organization: security tests in `tests/Hexalith.EventStore.Server.Tests/Security/`
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- File paths via `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", ...))`

### Mandatory Coding Patterns

- YAML must use valid DAPR Component format (`apiVersion: dapr.io/v1alpha1`, `kind: Component`)
- All YAML comments reference the architectural decision (D6, FR29, NFR13, NFR20)
- Tests use Shouldly assertions and xUnit
- No application code changes expected -- this story is YAML configuration + validation tests + documentation
- `ConfigureAwait(false)` on any async test operations
- Feature folder organization: tests in `tests/Hexalith.EventStore.Server.Tests/Security/`
- No new NuGet dependencies needed

### Project Structure Notes

**Modified files:**
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` -- ADD/enhance subscriber scoping, documentation
- `deploy/dapr/pubsub-rabbitmq.yaml` -- ADD subscriber scoping, documentation
- `deploy/dapr/pubsub-kafka.yaml` -- ADD subscriber scoping, documentation

**New files:**
- `tests/Hexalith.EventStore.Server.Tests/Security/PubSubTopicIsolationEnforcementTests.cs` -- Subscriber scoping validation tests
- `tests/Hexalith.EventStore.Server.Tests/Security/SubscriptionScopingDocumentationTests.cs` -- Documentation presence tests

**Alignment with unified project structure:**
- Security tests go in `tests/Hexalith.EventStore.Server.Tests/Security/` (existing folder, contains `StorageKeyIsolationTests.cs`, `AccessControlPolicyTests.cs` from Story 5.1)
- DAPR component configs remain in `AppHost/DaprComponents/` (local) and `deploy/dapr/` (production)
- No new project folders needed

### Previous Story Intelligence

**From Story 5.1 (DAPR Access Control Policies):**
- Established `scopes: [commandapi]` on pub/sub component -- restricts component access
- Added `publishingScopes: commandapi=*` -- only commandapi can publish
- Domain services (`sample`) have zero pub/sub access (not in `scopes`)
- Dead-letter topic publishing scoped to `commandapi` only
- `AccessControlPolicyTests.cs` validates: `PubSubYaml_HasScopes_RestrictsAppAccess` and `PubSubYaml_DeadLetterTopics_ScopedToCommandApiOnly`
- **Subscriber-side scoping was explicitly deferred to this story** (see Story 5.1 Dev Notes: "Story 5.3 (Pub/Sub Topic Isolation Enforcement) will address the subscriber-side scoping in detail")

**From Story 5.2 (Data Path Isolation Verification):**
- Three-layer isolation model validated: actor identity, DAPR policies, command metadata
- Data path isolation is tested at command routing, actor processing, and domain service invocation levels
- Layer 2 (DAPR policies) tested via YAML validation, not runtime tests (deliberate design choice)
- Runtime verification of DAPR enforcement deferred to Epic 7 (Tier 2/3 tests with DAPR test containers)

**From Story 4.2 (Per-Tenant-Per-Domain Topic Isolation):**
- Topic pattern: `{tenant}.{domain}.events` (D6)
- `AggregateIdentity.PubSubTopic` returns the topic name
- TopicNameValidator validates D6 format with regex: `^[a-z0-9]([a-z0-9-]*[a-z0-9])?\.([a-z0-9]([a-z0-9-]*[a-z0-9])?\.events$`
- MaxTopicLength = 249 (Kafka limit)
- TopicIsolationTests verify: different tenants -> different topics, same tenant -> same topic, concurrent tenants -> no cross-contamination, case normalization, edge cases (hyphens, single chars, max length)
- MultiTenantPublicationTests verify: full actor pipeline routes events to correct per-tenant topics

**From Story 4.1 (Event Publisher with CloudEvents 1.0):**
- EventPublisher uses `DaprClient.PublishEventAsync` with CloudEvents metadata
- `EventPublisherOptions.PubSubName` = `"pubsub"` (default)
- `EventPublisherOptions.DeadLetterTopicPrefix` = `"deadletter"`
- Dead-letter topic pattern: `deadletter.{tenant}.{domain}.events`
- CloudEvents id: `{correlationId}:{sequenceNumber}` (enables subscriber idempotency)

**From Story 5.1 Dev Notes - NFR20 Tension Resolution:**
> "Use wildcard (`*`) for `publishingScopes` on `commandapi`, which allows publishing to any topic. Subscriber scoping is more constrained -- external subscribers will need their subscription scopes updated when new tenants are onboarded. This is acceptable because: (1) The event store itself (publisher) is trusted and can publish to any topic, (2) Subscriber access is a downstream concern managed by the subscriber's DAPR configuration, (3) Story 5.3 (Pub/Sub Topic Isolation Enforcement) will address the subscriber-side scoping in detail."

### Git Intelligence

Recent commits show Epic 4 completion and Epic 5 start:
- `452962a` feat: Stories 4.2 & 4.3 - Topic isolation and at-least-once delivery (#38)
- `42bcd85` feat: Implement at-least-once delivery and DAPR retry policies
- `72d7a53` Story 4.1: Event Publisher with CloudEvents 1.0 (#37)
- `226a260` Story 3.11: Actor state machine and checkpointed stages (#36)

Patterns:
- DAPR component YAML configs use standard CRD format
- Security tests in dedicated `Security/` folder
- Test libraries: xUnit, Shouldly, NSubstitute (no YAML parser)
- Tests use string-based YAML content validation

### Testing Requirements

**New test classes (2):**

1. `PubSubTopicIsolationEnforcementTests.cs` -- ~8 tests:
   - Local pub/sub subscription scoping present
   - Publisher scoping regression check (commandapi only)
   - Dead-letter separate subscription scope
   - Production RabbitMQ subscription scoping
   - Production Kafka subscription scoping
   - Local-production topology consistency
   - CommandApi publish scope unrestricted (NFR20)
   - Domain services zero pub/sub access (regression)

2. `SubscriptionScopingDocumentationTests.cs` -- ~4 tests:
   - Subscriber onboarding documentation in local YAML
   - Tenant scoping documentation in local YAML
   - Deployment substitution guidance in production YAMLs
   - Dynamic tenant strategy documentation

**Total estimated: ~12 new tests**

### Failure Scenario Matrix

| Scenario | Expected Behavior | Enforcement |
|----------|-------------------|-------------|
| Authorized subscriber subscribes to authorized tenant topic | ALLOWED | DAPR scoping allows |
| Authorized subscriber subscribes to unauthorized tenant topic | DENIED | DAPR subscription scoping rejects |
| Domain service attempts to subscribe to any topic | DENIED | Not in component `scopes` (Story 5.1) |
| Unknown app-id attempts to subscribe | DENIED | Not in component `scopes` |
| Subscriber attempts dead-letter topic subscription | DENIED (unless explicitly authorized) | Separate dead-letter scoping |
| New tenant topic created dynamically | Publisher can publish immediately | Wildcard publish scope |
| Subscriber needs access to new tenant | Requires YAML update (documented) | Operational procedure |
| commandapi subscribes to topic (internal use) | ALLOWED | In component `scopes` |

### DAPR Version Compatibility Note

This story targets DAPR 1.16.x. The pub/sub scoping mechanisms (`allowedTopics`, `publishingScopes`, `subscriptionScopes`) may have evolved between DAPR versions. **Task 0.7 research is critical** to verify exact field names and behavior for each pub/sub component type:

- **DAPR docs to check:** "Scope Pub/sub topic access" and "Scope Components to Applications"
- **Component-specific docs:** Check Redis, RabbitMQ, and Kafka pub/sub component metadata schemas
- **Breaking changes:** Check DAPR 1.15 -> 1.16 changelog for pub/sub scoping changes

### Adding New Subscriber Services

When a new subscriber service needs to receive events for specific tenants, the following changes are required:

1. **`pubsub.yaml` (local)**: Add the subscriber app-id to `scopes` (component-level access). Add the subscriber to `subscriptionScopes` or `allowedTopics` with the specific tenant topics it's authorized for.
2. **`pubsub-rabbitmq.yaml` (production)**: Same pattern as local, with production app-id.
3. **`pubsub-kafka.yaml` (production)**: Same pattern as local, with production app-id.
4. **Update tests**: Add the new subscriber app-id to topology consistency tests.
5. **Dead-letter access**: If the subscriber needs dead-letter topic access, add it SEPARATELY from regular topic access.

This procedure should be documented as comments in each YAML file.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 5, Story 5.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR29 Pub/sub topic isolation]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR13 Multi-tenant data isolation at all three layers]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR20 Dynamic tenant provisioning without restart]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR28 DAPR-compatible pub/sub component support]
- [Source: _bmad-output/planning-artifacts/architecture.md#D4 DAPR Access Control]
- [Source: _bmad-output/planning-artifacts/architecture.md#D6 Pub/Sub Topic Naming]
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs]
- [Source: src/Hexalith.EventStore.Server/Events/TopicNameValidator.cs]
- [Source: src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs]
- [Source: src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs]
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs#PubSubTopic]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml]
- [Source: deploy/dapr/pubsub-rabbitmq.yaml]
- [Source: deploy/dapr/pubsub-kafka.yaml]
- [Source: tests/Hexalith.EventStore.Server.Tests/Events/TopicIsolationTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Actors/MultiTenantPublicationTests.cs]
- [Source: _bmad-output/implementation-artifacts/5-1-dapr-access-control-policies.md]
- [Source: _bmad-output/implementation-artifacts/5-2-data-path-isolation-verification.md]
- [Source: DAPR docs: Scope Pub/sub topic access]
- [Source: DAPR docs: Scope Components to Applications]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

### Implementation Plan

**Task 0 Findings (2026-02-15):**
- All 595 server unit tests pass. Integration tests have pre-existing Keycloak infrastructure failures (unrelated).
- Local pubsub.yaml: correctly uses `sample=` (empty=deny) pattern. `commandapi` unlisted = unrestricted. CORRECT.
- Production RabbitMQ/Kafka: use `commandapi=*` which is a BUG — `*` is a literal topic name, NOT a wildcard. Dapr does not support wildcards in scoping. Fix: change to match local pattern (deny unauthorized apps, leave commandapi unlisted for unrestricted access).
- Dapr scoping is runtime-level, works identically for Redis, RabbitMQ, Kafka, all others.
- `subscriptionScopes` uses identical format to `publishingScopes`: `"app1=topic1,topic2;app2=topic3"`.
- `protectedTopics` metadata available for defense-in-depth on sensitive topics.
- Unauthorized publish: HTTP 403. Unauthorized subscribe: subscription silently filtered, warning logged.
- AccessControlPolicyTests.cs uses YamlDotNet for parsing — new tests should follow same pattern.

### Completion Notes List

- Task 0: Verified all 595 existing unit tests pass. Reviewed all YAML configs and existing test coverage. Researched DAPR 1.16 pub/sub scoping: discovered that `commandapi=*` in production configs is a bug (DAPR treats `*` as literal topic name, not wildcard). No wildcard support exists. Correct approach: leave `commandapi` unlisted for unrestricted access.
- Task 1: Enhanced local `pubsub.yaml` with comprehensive three-layer scoping architecture documentation, subscriber onboarding guide (5-step procedure), NFR20 dynamic tenant strategy, DAPR 1.16 scoping field reference, example subscriber/ops-monitor entries, and dead-letter topic separation documentation.
- Task 2: Fixed production RabbitMQ and Kafka configs -- removed `commandapi=*` bug (literal topic, not wildcard), changed to empty publishingScopes/subscriptionScopes (commandapi unlisted = unrestricted). Added subscriber onboarding documentation, placeholder app-ids for deployment substitution, component-specific notes, and NFR20 dynamic tenant strategy.
- Task 3: Created PubSubTopicIsolationEnforcementTests.cs with 8 tests covering: subscription scoping presence (local, RabbitMQ, Kafka), publisher scoping regression (Story 5.1), dead-letter topic separation, local-production topology consistency, commandapi unrestricted publish scope (NFR20), and domain service zero-access regression (Story 5.1).
- Task 4: Created SubscriptionScopingDocumentationTests.cs with 4 tests covering: subscriber onboarding documentation, tenant scoping documentation, deployment substitution guidance, and NFR20 dynamic tenant strategy documentation.
- Task 5: Verified existing TopicNameValidatorTests.cs and TopicIsolationTests.cs comprehensively cover D6 pattern validation, cross-tenant topic disjointness, and topic derivation determinism. No gaps found -- no additional tests needed.
- Task 6: Full regression suite passes (607 server tests, 0 failures). Story 5.1 publisher scoping tests pass. Story 4.2 topic isolation tests pass. Subscriber scoping strategy documented consistently across all YAML files.
- Code Review Auto-Fix (2026-02-15): Activated explicit subscriber/dead-letter subscription scopes in local and production pub/sub YAMLs, added unauthorized-subscription sidecar-log observability guidance, and aligned security assertions with authorized-subscriber topology.
- Code Review transparency note: Working tree includes additional unrelated changes outside Story 5.3 scope; this story file list tracks only Story 5.3 and review-pass artifacts.

### Senior Developer Review (AI)

- Review date: 2026-02-15
- High/Medium findings resolved in this pass:
  - Active subscriber-side scoping now enforced in YAML values (not documentation-only).
  - Dead-letter subscription scope is explicitly separated from regular event scopes.
  - Unauthorized subscription handling observability is documented at DAPR sidecar log level.
  - Access-control tests updated for authorized-subscriber topology while preserving `sample` deny posture.
- Verification run: `PubSubTopicIsolationEnforcementTests`, `SubscriptionScopingDocumentationTests`, `AccessControlPolicyTests` => 27 passed, 0 failed.

### Change Log

- 2026-02-15: Story 5.3 implementation complete -- subscriber-side pub/sub topic isolation enforcement
  - Enhanced local pubsub.yaml with subscriber scoping documentation and examples
  - Fixed production RabbitMQ/Kafka configs: removed `commandapi=*` bug (DAPR treats * as literal, not wildcard)
  - Created PubSubTopicIsolationEnforcementTests.cs (8 tests)
  - Created SubscriptionScopingDocumentationTests.cs (4 tests)
  - All 607 server tests pass (12 new, 595 existing, 0 regressions)
- 2026-02-15: Code review auto-fixes applied
  - Activated explicit subscriber/dead-letter scope values in local and production pub/sub configs
  - Added unauthorized subscription sidecar-log observability documentation
  - Updated access-control assertions for authorized-subscriber component scopes
  - Re-ran targeted security suite: 27/27 passing

### File List

- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` (modified) -- Enhanced with subscriber scoping documentation, NFR20 strategy, three-layer architecture docs, subscriber onboarding guide
- `deploy/dapr/pubsub-rabbitmq.yaml` (modified) -- Fixed commandapi=* bug, added subscriber scoping, deployment documentation, RabbitMQ-specific notes
- `deploy/dapr/pubsub-kafka.yaml` (modified) -- Fixed commandapi=* bug, added subscriber scoping, deployment documentation, Kafka-specific notes
- `tests/Hexalith.EventStore.Server.Tests/Security/PubSubTopicIsolationEnforcementTests.cs` (new) -- 8 subscriber scoping validation tests
- `tests/Hexalith.EventStore.Server.Tests/Security/SubscriptionScopingDocumentationTests.cs` (new) -- 4 documentation presence tests
- `tests/Hexalith.EventStore.Server.Tests/Security/AccessControlPolicyTests.cs` (modified) -- Adjusted pub/sub scope assertions for authorized subscriber app-ids while preserving sample deny guarantees
