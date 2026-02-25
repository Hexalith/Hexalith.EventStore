# Story 7.3: Production DAPR Component Configurations

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**Story 7.2 (Local DAPR Component Configurations) should be completed or in progress. The local configs establish the baseline patterns that production configs must mirror with only component type and connection metadata differences (NFR29).**

Verify these files/artifacts exist before starting:
- `deploy/dapr/statestore-postgresql.yaml` -- existing PostgreSQL state store config (has TODO placeholders)
- `deploy/dapr/statestore-cosmosdb.yaml` -- existing Cosmos DB state store config (has TODO placeholders)
- `deploy/dapr/pubsub-rabbitmq.yaml` -- existing RabbitMQ pub/sub config (has TODO placeholders)
- `deploy/dapr/pubsub-kafka.yaml` -- existing Kafka pub/sub config (has TODO placeholders)
- `deploy/dapr/resiliency.yaml` -- existing production resiliency policies
- `deploy/dapr/accesscontrol.yaml` -- existing production access control config
- `deploy/dapr/subscription-sample-counter.yaml` -- existing sample subscription template
- `deploy/README.md` -- existing minimal deployment README
- `src/Hexalith.EventStore.AppHost/DaprComponents/` -- local configs for comparison (NFR29 portability)

Run `dotnet build` to confirm the solution compiles before beginning.

## Story

As a **DevOps engineer**,
I want production-ready DAPR component configuration templates for multiple backends (PostgreSQL state store, Cosmos DB state store, RabbitMQ pub/sub, Kafka pub/sub, Azure Service Bus pub/sub),
so that I can deploy to different environments by changing only DAPR config files with zero application code changes, zero recompilation, zero redeployment of application containers (FR43, NFR29).

## Acceptance Criteria

1. **Production DAPR component config templates exist in `deploy/dapr/` directory** - Given production DAPR component config templates exist in the `deploy/` directory, When I swap local Redis configs for PostgreSQL state store and RabbitMQ pub/sub configs, Then the system functions correctly with zero application code changes, zero recompilation, zero redeployment of application containers (NFR29).

2. **Templates exist for all required backends** - Given the `deploy/dapr/` directory, Then templates exist for: PostgreSQL state store (`statestore-postgresql.yaml`), Cosmos DB state store (`statestore-cosmosdb.yaml`), RabbitMQ pub/sub (`pubsub-rabbitmq.yaml`), Kafka pub/sub (`pubsub-kafka.yaml`), Azure Service Bus pub/sub (`pubsub-servicebus.yaml`).

3. **Each template includes connection string placeholders and documentation for required secrets** - Given each production config template, Then connection parameters use environment variable placeholders (e.g., `{env:POSTGRES_CONNECTION_STRING}`) or TODO markers with documentation, And inline comments explain every required secret and how to provide it, And secrets are never stored in configuration files committed to source control (NFR14).

4. **Production resiliency policies are tuned for production workloads** - Given the production `resiliency.yaml`, Then retry policies use exponential backoff with higher retry counts than local (production-grade), And the `components.statestore` target is present with retry and circuit breaker policies (gap fix from Story 7.2), And timeout and circuit breaker thresholds are tuned for production latency expectations.

5. **Production access control is secure-by-default** - Given the production `accesscontrol.yaml`, Then `defaultAction: deny` is enforced, And commandapi can POST to `/**` for domain service invocation (D4), And domain service template shows zero allowed operations, And the sample domain service is intentionally omitted (production-only config).

6. **Scoping, access control, and resiliency are structurally identical between local and production** - Given local and production configs, When comparing their structure, Then the only differences are component type, connection metadata, and tuning parameters (NFR29), **except** that production access control intentionally omits the local sample domain-service policy (AC #5), And key patterns (`{tenant}:{domain}:{aggId}:events:{seq}`) work identically across backends.

7. **Comprehensive deployment README documents the configuration guide** - Given `deploy/README.md`, Then it documents: which config files to use for each environment, how to substitute secrets, the backend compatibility matrix, step-by-step deployment instructions for Docker Compose/Kubernetes/Azure Container Apps, and how to validate the configuration.

8. **Production validation tests verify structural correctness** - Given production config validation tests exist, Then tests verify all expected YAML files are present in `deploy/dapr/`, And tests verify structural parity between local and production configs (same component names, same scoping patterns, same security posture).

## Tasks / Subtasks

- [x] Task 1: Add missing Azure Service Bus pub/sub configuration (AC: #2, #3)
  - [x] 1.1 Create `deploy/dapr/pubsub-servicebus.yaml` -- Azure Service Bus pub/sub config following the same three-layer scoping pattern as RabbitMQ and Kafka configs
  - [x] 1.2 Include connection string placeholder with environment variable pattern
  - [x] 1.3 Include comprehensive inline documentation matching the RabbitMQ/Kafka documentation depth (scoping architecture, adding subscribers, dynamic tenant provisioning, Service Bus-specific notes)
  - [x] 1.4 Include `enableDeadLetter: true` and dead-letter topic configuration

- [x] Task 2: Fix production resiliency.yaml -- add statestore target (AC: #4)
  - [x] 2.1 Add `components.statestore` target to `deploy/dapr/resiliency.yaml` with retry policy (exponential backoff) and circuit breaker
  - [x] 2.2 Ensure production retry counts and intervals are appropriately tuned (higher than local development values)
  - [x] 2.3 Add inline comments explaining the statestore resiliency rationale (transient state store failures should be retried before failing actor turns)

- [x] Task 3: Enhance connection string placeholders with environment variable patterns (AC: #3)
  - [x] 3.1 Update `statestore-postgresql.yaml` -- replace `TODO` with `{env:POSTGRES_CONNECTION_STRING}` pattern and document required format
  - [x] 3.2 Update `statestore-cosmosdb.yaml` -- replace `TODO` values with `{env:COSMOSDB_URL}`, `{env:COSMOSDB_KEY}`, `{env:COSMOSDB_DATABASE}`, `{env:COSMOSDB_COLLECTION}` patterns
  - [x] 3.3 Update `pubsub-rabbitmq.yaml` -- replace `TODO` with `{env:RABBITMQ_CONNECTION_STRING}` pattern
  - [x] 3.4 Update `pubsub-kafka.yaml` -- replace `TODO` values with `{env:KAFKA_BROKERS}`, `{env:KAFKA_AUTH_TYPE}` patterns
  - [x] 3.5 Verify no hardcoded secrets exist in any production config (NFR14 compliance)

- [x] Task 4: Write comprehensive deployment README (AC: #7)
  - [x] 4.1 Rewrite `deploy/README.md` with: overview, directory structure, backend compatibility matrix (Redis/PostgreSQL/Cosmos DB for state store, Redis/RabbitMQ/Kafka/Service Bus for pub/sub)
  - [x] 4.2 Document per-backend configuration: which files to deploy, which environment variables to set, secret management guidance
  - [x] 4.3 Document deployment steps for Docker Compose, Kubernetes, Azure Container Apps (Aspire publisher targets)
  - [x] 4.4 Document how to validate configuration after deployment
  - [x] 4.5 Document how to add new domain services and subscriber services to production configs

- [x] Task 5: NFR29 portability validation -- compare local vs production (AC: #6)
  - [x] 5.1 Compare `DaprComponents/statestore.yaml` (Redis) with `deploy/dapr/statestore-postgresql.yaml` and `statestore-cosmosdb.yaml` -- verify only component type and connection metadata differ
  - [x] 5.2 Compare `DaprComponents/pubsub.yaml` (Redis) with `deploy/dapr/pubsub-rabbitmq.yaml`, `pubsub-kafka.yaml`, and `pubsub-servicebus.yaml` -- verify only component type and connection metadata differ
  - [x] 5.3 Compare `DaprComponents/resiliency.yaml` with `deploy/dapr/resiliency.yaml` -- verify structural identity with only tuning parameter differences
  - [x] 5.4 Compare `DaprComponents/accesscontrol.yaml` with `deploy/dapr/accesscontrol.yaml` -- verify same security posture (only namespace/trust domain may differ for production, and sample domain-service policy is intentionally omitted in production)
  - [x] 5.5 Document any portability issues found and fix them

- [x] Task 6: Create production config validation tests (AC: #8)
  - [x] 6.1 Create `tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs`
  - [x] 6.2 Test: `AllProductionComponentFiles_ExistInDeployDaprDirectory` -- verify all 8 expected YAML files are present (2 state stores, 3 pub/subs, resiliency, accesscontrol, subscription)
  - [x] 6.3 Test: `ProductionStateStores_HaveActorStateStoreEnabled` -- verify `actorStateStore: "true"` in both PostgreSQL and Cosmos DB configs
  - [x] 6.4 Test: `ProductionStateStores_ScopedToCommandApiOnly` -- verify scopes contain only `commandapi`
  - [x] 6.5 Test: `ProductionPubSubs_HaveDeadLetterEnabled` -- verify `enableDeadLetter: "true"` in all 3 pub/sub configs
  - [x] 6.6 Test: `ProductionAccessControl_DefaultActionIsDeny` -- verify `defaultAction: deny`
  - [x] 6.7 Test: `ProductionAccessControl_CommandApiCanPostOnly` -- verify POST `/**` policy
  - [x] 6.8 Test: `ProductionAccessControl_NoSampleDomainService` -- verify sample is NOT in production access control
  - [x] 6.9 Test: `ProductionResiliency_HasStatestoreTarget` -- verify `components.statestore` target exists
  - [x] 6.10 Test: `ProductionResiliency_SidecarTimeoutIsFiveSeconds` -- verify `general: 5s` timeout
  - [x] 6.11 Test: `LocalAndProduction_StateStoreComponentNames_Match` -- verify both use `name: statestore`
  - [x] 6.12 Test: `LocalAndProduction_PubSubComponentNames_Match` -- verify both use `name: pubsub`
  - [x] 6.13 Test: `DeployReadme_Exists` -- verify `deploy/README.md` exists and is non-trivial

- [x] Task 7: Verify build and all tests pass (AC: all)
  - [x] 7.1 Run `dotnet build` to verify the solution compiles
  - [x] 7.2 Run `dotnet test` to ensure no regressions and new tests pass

## Dev Notes

### Story Context

This is the **third story in Epic 7: Sample Application, Testing, CI/CD & Deployment**. It creates production-ready DAPR component configuration templates that demonstrate NFR29 (zero code changes for backend swap). Unlike Story 7.2 which validated and completed the local development configs, this story focuses on completing the **production equivalents** in `deploy/dapr/`.

**What already exists (validate and COMPLETE, not replicate):**
- Story 5.1 created initial production configs as part of access control work
- `statestore-postgresql.yaml` and `statestore-cosmosdb.yaml` exist with TODO connection placeholders
- `pubsub-rabbitmq.yaml` and `pubsub-kafka.yaml` exist with TODO connection placeholders and comprehensive scoping docs
- `resiliency.yaml` exists with production-tuned policies (but missing statestore target)
- `accesscontrol.yaml` exists with production-appropriate posture (no sample service)
- `subscription-sample-counter.yaml` exists as a template example
- `deploy/README.md` exists but is minimal (2 sentences)

**What this story adds:**
- `pubsub-servicebus.yaml` -- Azure Service Bus pub/sub config (missing, required by AC)
- Statestore resiliency target in production `resiliency.yaml` (gap fix)
- Environment variable patterns replacing TODO placeholders (NFR14 compliance)
- Comprehensive deployment README with backend matrix and step-by-step guides
- Production config validation tests verifying structural correctness and parity with local
- NFR29 portability validation documentation

**What this story does NOT do:**
- Modify local development DAPR configs (that's Story 7.2)
- Run the system with production backends (that's Stories 7.4/7.5)
- Create CI/CD pipelines (that's Story 7.6)
- Create Aspire publisher manifests (that's Story 7.7)

### Architecture Compliance

**FR43:** Environment deployment via DAPR component config only -- zero application code changes.

**NFR29:** Switching between validated backend configurations must require only DAPR component YAML changes -- zero application code, zero recompilation, zero redeployment of application containers.

**NFR14:** Secrets (connection strings, JWT signing keys, DAPR component credentials) must never be stored in application code or configuration files committed to source control.

**NFR27:** The system must function correctly with any DAPR-compatible state store that supports key-value operations and ETag-based optimistic concurrency (validated: Redis, PostgreSQL).

**NFR28:** The system must function correctly with any DAPR-compatible pub/sub component that supports CloudEvents 1.0 and at-least-once delivery (validated: RabbitMQ, Azure Service Bus).

**Architecture decisions referenced:**
- **D1:** Event storage -- single-key-per-event, composite key `{tenant}:{domain}:{aggId}:events:{seq}`
- **D2:** Command status -- `{tenant}:{correlationId}:status` with 24h TTL
- **D4:** Access control -- per-app-id allow list, deny by default
- **D6:** Topic naming -- `{tenant}.{domain}.events`
- **D7:** Domain service invocation -- `DaprClient.InvokeMethodAsync`, mTLS via DAPR

**Enforcement rules:**
- **Rule #4:** Never add custom retry logic -- all retries are DAPR resiliency policies
- **Rule #14:** DAPR sidecar call timeout is 5 seconds

### Backend Compatibility Matrix

| Capability | Redis | PostgreSQL | Cosmos DB |
|-----------|-------|-----------|-----------|
| Multi-key transactions | No | Yes | Yes (within partition) |
| ETag optimistic concurrency | Yes | Yes | Yes |
| Range queries | Limited | Yes | Yes |
| Atomic batch writes | No | Yes | Depends on partition |

| Pub/Sub Feature | Redis Streams | RabbitMQ | Kafka | Azure Service Bus |
|----------------|--------------|----------|-------|-------------------|
| CloudEvents 1.0 | Yes | Yes | Yes | Yes |
| At-least-once delivery | Yes | Yes | Yes | Yes |
| Dead-letter support | Yes | Yes (DLX) | Yes | Yes (native DLQ) |
| Topic auto-creation | Yes | Yes | Yes | No (pre-create) |

### Existing Production Config Analysis

| File | Status | Action Needed |
|------|--------|---------------|
| `statestore-postgresql.yaml` | Has TODO placeholders | Replace with env var patterns |
| `statestore-cosmosdb.yaml` | Has TODO placeholders | Replace with env var patterns |
| `pubsub-rabbitmq.yaml` | Has TODO placeholder | Replace with env var pattern |
| `pubsub-kafka.yaml` | Has TODO placeholders | Replace with env var patterns |
| `pubsub-servicebus.yaml` | **MISSING** | Create new file |
| `resiliency.yaml` | Missing statestore target | Add `components.statestore` target |
| `accesscontrol.yaml` | Complete | No changes needed |
| `subscription-sample-counter.yaml` | Complete | No changes needed |
| `README.md` | Minimal (2 sentences) | Rewrite with comprehensive guide |

### Production vs Local Config Structural Comparison

| Aspect | Local (DaprComponents/) | Production (deploy/dapr/) |
|--------|------------------------|--------------------------|
| State store type | `state.redis` | `state.postgresql` / `state.azure.cosmosdb` |
| State store scoping | `commandapi` only | `commandapi` only (identical) |
| Pub/sub type | `pubsub.redis` | `pubsub.rabbitmq` / `pubsub.kafka` / `pubsub.servicebus` |
| Pub/sub scoping | Three-layer (identical pattern) | Three-layer (identical pattern) |
| Pub/sub dead letter | `enableDeadLetter: true` | `enableDeadLetter: true` (identical) |
| Access control | deny-by-default + sample | deny-by-default, no sample |
| Resiliency | Lower retry counts, shorter timeouts | Higher retry counts, longer timeouts |
| Connection | Env vars with localhost defaults | Env vars (no defaults -- must be set) |
| Component names | `statestore`, `pubsub` | `statestore`, `pubsub` (identical) |

### Testing Approach

Production config validation tests follow the same pattern as Story 5.1's `DaprAccessControlPolicyTests.cs` and Story 7.2's `DaprComponentValidationTests.cs`:

**Test file location:** `tests/Hexalith.EventStore.Server.Tests/DaprComponents/` (feature folder convention, Rule #2)

**Test approach:** Read YAML files from `deploy/dapr/` directory, use string assertions or YAML deserialization to verify structural correctness. Tests compare production configs against expected patterns and verify parity with local configs.

**IMPORTANT:** Follow existing test patterns in the project:
- NSubstitute + Shouldly assertion library
- File path resolution relative to solution root
- YAML content string assertions (or YamlDotNet if already available)

### Previous Story Intelligence

**From Story 7.2 (Local DAPR Component Configurations) -- direct predecessor:**
- Identified resiliency.yaml missing `components.statestore` target -- same gap exists in production
- Local configs are the baseline for NFR29 portability comparison
- DAPR component validation tests pattern established in `DaprComponentValidationTests.cs`
- Production config comparison was listed as Task 4 in Story 7.2

**From Story 5.1 (DAPR Access Control Policies):**
- Created initial production configs in `deploy/dapr/`
- 14 YAML validation tests in `DaprAccessControlPolicyTests.cs` -- **REUSE these test patterns**
- Test pattern: resolve YAML file path relative to solution root, read as string, assert on content

**From Story 4.3 (At-Least-Once Delivery & DAPR Retry Policies):**
- Created resiliency policies with per-backend effective retry count documentation
- Production resiliency.yaml has higher retry counts than local (correct production tuning)

### Git Intelligence

Recent commits show Epics 5-6 completion:
- `98d435a` Story 6.4 - Health check endpoints
- `b7f617c` feat: Story 6.4 - Implement Dapr health check endpoints
- `b9c126a` feat: Command API authorization, validation, Dapr domain service invoker
- `427bb29` feat: Stories 6.1-6.4 - Observability, telemetry & health check instrumentation

**Patterns from commits:**
- YAML validation tests (from Story 5.1) -- file read + content assertions
- NSubstitute + Shouldly for testing
- Feature folder organization (Rule #2)

### Technical Stack Reference

| Technology | Version |
|-----------|---------|
| DAPR Runtime | 1.16.6 |
| DAPR .NET SDK | Dapr.Client 1.16.0, Dapr.AspNetCore 1.16.1 |
| Aspire | 13.1.0 |
| .NET SDK | 10.0.102 |

### DAPR Component Type References

| Backend | DAPR Component Type | DAPR Docs Reference |
|---------|-------------------|-------------------|
| PostgreSQL state store | `state.postgresql` | Dapr state store PostgreSQL |
| Cosmos DB state store | `state.azure.cosmosdb` | Dapr state store Azure Cosmos DB |
| RabbitMQ pub/sub | `pubsub.rabbitmq` | Dapr pub/sub RabbitMQ |
| Kafka pub/sub | `pubsub.kafka` | Dapr pub/sub Kafka |
| Azure Service Bus pub/sub | `pubsub.azure.servicebus.topics` | Dapr pub/sub Azure Service Bus Topics |

### Project Structure Notes

**Modified files:**
- `deploy/dapr/statestore-postgresql.yaml` -- env var placeholders replacing TODOs
- `deploy/dapr/statestore-cosmosdb.yaml` -- env var placeholders replacing TODOs
- `deploy/dapr/pubsub-rabbitmq.yaml` -- env var placeholder replacing TODO
- `deploy/dapr/pubsub-kafka.yaml` -- env var placeholders replacing TODOs
- `deploy/dapr/resiliency.yaml` -- add `components.statestore` target
- `deploy/README.md` -- comprehensive rewrite

**New files:**
- `deploy/dapr/pubsub-servicebus.yaml` -- Azure Service Bus pub/sub config
- `tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs` -- ~13 tests

**No changes to `src/` application code.** This story is config templates + tests + documentation only.

### Scope Assessment

This is a **medium story** focused on completing production configs and adding validation. The existing production configs are substantially complete from Story 5.1. The primary work is:
- Create missing Azure Service Bus pub/sub config (following existing patterns)
- Fix resiliency statestore target gap
- Replace TODO placeholders with env var patterns
- Write comprehensive deployment README
- Write ~13 validation tests following existing patterns
- NFR29 portability validation

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 7.3: Production DAPR Component Configurations]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1: Event Storage Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#D2: Command Status Storage]
- [Source: _bmad-output/planning-artifacts/architecture.md#D4: DAPR Access Control]
- [Source: _bmad-output/planning-artifacts/architecture.md#D6: Pub/Sub Topic Naming]
- [Source: _bmad-output/planning-artifacts/architecture.md#D7: Domain Service Invocation]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR14 Secrets never in source control]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR27 Backend-agnostic state store]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR28 Backend-agnostic pub/sub]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR29 Zero code changes for backend swap]
- [Source: _bmad-output/planning-artifacts/architecture.md#Backend Compatibility Matrix]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 4 DAPR-only retries]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 14 DAPR sidecar call timeout 5 seconds]
- [Source: deploy/dapr/statestore-postgresql.yaml -- existing PostgreSQL state store config]
- [Source: deploy/dapr/statestore-cosmosdb.yaml -- existing Cosmos DB state store config]
- [Source: deploy/dapr/pubsub-rabbitmq.yaml -- existing RabbitMQ pub/sub config]
- [Source: deploy/dapr/pubsub-kafka.yaml -- existing Kafka pub/sub config]
- [Source: deploy/dapr/resiliency.yaml -- existing production resiliency policies]
- [Source: deploy/dapr/accesscontrol.yaml -- existing production access control]
- [Source: deploy/README.md -- existing minimal deployment README]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/ -- local configs for NFR29 comparison]
- [Source: _bmad-output/implementation-artifacts/7-2-local-dapr-component-configurations.md -- Predecessor story]
- [Source: _bmad-output/implementation-artifacts/7-1-sample-counter-domain-service.md -- Epic 7 predecessor]

## Dev Agent Record

### Agent Model Used
Claude Opus 4.6

### Debug Log References
None required.

### Completion Notes List

- Task 1: Created `deploy/dapr/pubsub-servicebus.yaml` with Azure Service Bus pub/sub config, three-layer scoping, dead-letter support, and comprehensive inline documentation matching RabbitMQ/Kafka depth.
- Task 2: Production `resiliency.yaml` already had `components.statestore` target with exponential backoff (maxRetries: 10) and circuit breaker. Inline comments already present. No changes needed.
- Task 3: Replaced all TODO placeholders with `{env:*}` patterns across 4 config files. Added format documentation comments for each connection parameter. Verified zero TODOs remain (NFR14).
- Task 4: Rewrote `deploy/README.md` from 2 sentences to comprehensive deployment guide: directory structure, backend compatibility matrix, per-backend env var tables, secret management, Docker Compose/Kubernetes/Azure Container Apps deployment steps, validation procedures, and guides for adding domain services and subscribers.
- Task 5: NFR29 portability validation confirmed structural parity: identical component names (`statestore`, `pubsub`), identical scoping patterns, identical security posture (`defaultAction: deny`), identical dead-letter configuration. Only differences are component type, connection metadata, and resiliency tuning parameters, with one intentional production-only exception: `accesscontrol.yaml` omits the local `sample` domain-service policy. No portability issues found.
- Task 6: Created 16 production validation tests (13 specified + 3 Theory variations) covering file existence, actorStateStore, scoping, dead-letter, access control, resiliency, component name parity, and README existence. All 16 pass.
- Task 7: Build succeeds with 0 errors/0 warnings. All 16 new tests pass. All 29 DAPR component tests (local + production) pass. Pre-existing AggregateActorTests and integration test failures are unrelated to this story.
- Review remediation (2026-02-25): Added dead-letter topic value assertions in production validation tests, hardened production template placeholders (`DAPR_TRUST_DOMAIN`, `DAPR_NAMESPACE`, `SUBSCRIBER_APP_ID`, `OPS_MONITOR_APP_ID`), and clarified Azure Container Apps deployment/readme guidance.

### Change Log

- 2026-02-16: Story 7.3 implementation complete. Created Azure Service Bus pub/sub config, replaced TODO placeholders with env var patterns, wrote comprehensive deployment README, validated NFR29 portability, added 16 production config validation tests.
- 2026-02-25: Code review remediation: corrected NFR29 parity wording for intentional sample-policy omission, strengthened dead-letter metadata tests, and improved production template/deployment guidance.

### File List

- `deploy/dapr/pubsub-servicebus.yaml` (new) -- Azure Service Bus pub/sub config
- `deploy/dapr/accesscontrol.yaml` (modified) -- trust-domain/namespace template env placeholders for safer production reuse
- `deploy/dapr/statestore-postgresql.yaml` (modified) -- env var placeholders replacing TODOs
- `deploy/dapr/statestore-cosmosdb.yaml` (modified) -- env var placeholders replacing TODOs
- `deploy/dapr/pubsub-rabbitmq.yaml` (modified) -- env var placeholder replacing TODO
- `deploy/dapr/pubsub-kafka.yaml` (modified) -- env var placeholders replacing TODOs
- `deploy/README.md` (modified) -- comprehensive deployment guide rewrite + explicit production template variable and ACA guidance
- `tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs` (new/modified) -- production config validation tests with dead-letter topic assertions
