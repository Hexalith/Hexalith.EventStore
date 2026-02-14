# Story 3.6: Multi-Domain & Multi-Tenant Processing

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Stories 3.2-3.5 MUST be status=done before starting this story.**

Verify these files exist before starting:
- `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` (Story 3.2)
- `src/Hexalith.EventStore.Server/Actors/TenantValidator.cs` (Story 3.3)
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` (Story 3.4)
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` (Story 3.5)
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` (Story 3.5)
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs` (Story 3.5)

Run `dotnet test` to confirm all existing tests pass before beginning.

## Story

As a **platform operator**,
I want the system to process commands for multiple independent domains within the same EventStore instance, and multiple tenants within the same domain with isolated event streams,
So that a single deployment serves diverse workloads (FR24, FR25).

## Acceptance Criteria

1. **Given** multiple domain services are registered for different tenant+domain combinations **When** commands arrive for different domains (e.g., tenant1:orders, tenant1:inventory) **Then** each command is routed to the correct domain service based on domain **And** each domain maintains independent aggregate actors and event streams

2. **Given** multiple domain services are registered for different tenant+domain combinations **When** commands arrive for different tenants in the same domain (e.g., tenantA:orders, tenantB:orders) **Then** each tenant's event streams are fully isolated via composite key strategy (FR15, FR28) **And** actors for different tenants are independent even within the same domain

3. **Given** the EventStore system is running with existing tenants and domains **When** a new tenant or domain is added via configuration **Then** the system processes commands for the new tenant/domain without requiring system restart (NFR20)

4. **Given** commands arrive for tenant A and tenant B targeting the same domain and aggregate type **Then** actor IDs are distinct per tenant (canonical identity `tenant:domain:aggregate-id`) **And** no actor state leaks between tenants

5. **Given** the DomainServiceResolver is queried for a tenant+domain combination that is not registered **Then** a `DomainServiceNotFoundException` is thrown with the tenant and domain context **And** the command is rejected (not silently dropped)

6. **Given** a domain service returns a result **When** the result contains more events than the configured maximum (default 1000) **Then** the invocation is rejected with `DomainServiceException` **And** the command is not partially processed

7. **Given** domain service registrations use config store keys **When** a registration is looked up **Then** the key pattern follows `{tenant}:{domain}:{version}` format **And** version defaults to `v1` if not specified in the command

## Tasks / Subtasks

- [x] Task 1: Verify and extend DomainServiceResolver for multi-tenant multi-domain routing (AC: #1, #2, #5)
  - [x] 1.1 Ensure DomainServiceResolver correctly maps `tenant:domain:version` -> service endpoint for multiple registrations
  - [x] 1.2 Verify config store lookup supports dynamic tenant/domain additions (AC: #3, NFR20)
  - [x] 1.3 Handle missing registrations with `DomainServiceNotFoundException` (AC: #5)
- [x] Task 2: Verify AggregateIdentity-based actor isolation (AC: #1, #2, #4)
  - [x] 2.1 Confirm CommandRouter derives distinct actor IDs for different tenant+domain combos
  - [x] 2.2 Validate that `AggregateIdentity.ActorId` produces unique IDs per tenant:domain:aggregate-id
  - [x] 2.3 Verify no shared state between actors for different tenants
- [x] Task 3: Verify composite key isolation in state store (AC: #2, #4)
  - [x] 3.1 Confirm event keys include tenant prefix: `{tenant}:{domain}:{aggId}:events:{seq}`
  - [x] 3.2 Confirm snapshot keys include tenant prefix: `{tenant}:{domain}:{aggId}:snapshot`
  - [x] 3.3 Confirm metadata keys follow `{tenant}:{domain}:{aggId}:metadata` pattern (from Story 3.4 AggregateMetadata)
  - [x] 3.4 Verify tenant A's keys are structurally disjoint from tenant B's keys
- [x] Task 4: Integration tests for multi-domain/multi-tenant scenarios (AC: #1-#5)
  - [x] 4.1 Test: Two domains within same tenant route to different domain services
  - [x] 4.2 Test: Two tenants within same domain have isolated actors and event streams
  - [x] 4.3 Test: Dynamic tenant/domain addition without restart (config store update)
  - [x] 4.4 Test: Unregistered tenant+domain returns DomainServiceNotFoundException
  - [x] 4.5 Test: Actor state isolation between tenants (no cross-tenant leakage)
- [x] Task 5: Unit tests for DomainServiceResolver multi-tenant routing
  - [x] 5.1 Test: Resolver returns correct endpoint for each tenant+domain combo
  - [x] 5.2 Test: Resolver throws DomainServiceNotFoundException for unknown combo
  - [x] 5.3 Test: Resolver refreshes config without restart
- [x] Task 6: Verify DomainServiceResolver has no application-level caching (AC: #3, ADR-1)
  - [x] 6.1 Unit test: mock `DaprClient.GetConfiguration`, invoke resolver twice with same tenant+domain, assert `GetConfiguration` called TWICE (not cached)
  - [x] 6.2 Integration test: update config store registration, invoke resolver immediately, assert new registration returned
- [x] Task 7: Add domain service response size validation (AC: #6)
  - [x] 7.1 Extend existing `DomainServiceOptions.cs` (from Story 3.5) with `MaxEventsPerResult` (default 1000) and `MaxEventSizeBytes` (default 1_048_576)
  - [x] 7.2 Create `DomainServiceException.cs` in `Server/DomainServices/` (fields: TenantId, Domain, Reason, EventCount?, EventSizeBytes?)
  - [x] 7.3 Validate event count AND individual event payload size in `DaprDomainServiceInvoker.InvokeAsync()` AFTER deserialization
  - [x] 7.4 Log WARNING with CorrelationId when `DomainResult.IsNoOp` (empty events) to detect silent failures
  - [x] 7.5 Test: Response exceeding max events throws DomainServiceException
  - [x] 7.6 Test: Single event exceeding max payload size throws DomainServiceException
  - [x] 7.7 Test: No-op result logs WARNING
- [x] Task 8: Validate config store key pattern includes version (AC: #7, ADR-4)
  - [x] 8.1 Update `DomainServiceResolver` key pattern from Story 3.5's `{tenant}:{domain}:service` to `{tenant}:{domain}:{version}` (replace `:service` suffix with version)
  - [x] 8.2 Default version to `v1` when not specified; read version from `command.Extensions["domain-service-version"]` or default to `v1` if key not present
  - [x] 8.3 Validate version format against regex `^v[0-9]+$`, normalize to lowercase via `ToLowerInvariant()`
  - [x] 8.4 Log INFO when version defaults to v1 (audit trail for implicit decisions)
  - [x] 8.5 Test: Versioned key lookup resolves correctly
  - [x] 8.6 Test: Invalid version formats rejected: `"version1"` (no v prefix), `"v1a"` (non-numeric), `"v1:evil"` (injection)
  - [x] 8.7 Test: Version `"V1"` normalized to `"v1"`
- [x] Task 9: Document deployment security requirements
  - [x] 9.1 Document DAPR sidecar port 3500 network isolation requirement (Red Team H1)
  - [x] 9.2 Document config store write access restrictions (Red Team H2)
  - [x] 9.3 Note Story 5.1 (DAPR ACLs) as blocking for production multi-tenant deployments
- [x] Task 10: Verify existing tests pass (~459+ from Story 3.5)

## Dev Notes

### Story Context

This story validates and extends the multi-domain/multi-tenant architecture established in Stories 3.1-3.5. The core infrastructure (CommandRouter, AggregateIdentity, TenantValidator, DomainServiceResolver) was built in prior stories. Story 3.6 focuses on **proving** these components work correctly in multi-tenant/multi-domain scenarios through comprehensive testing and any necessary extensions.

**Key insight:** Much of the multi-tenant isolation is already architecturally guaranteed by the canonical identity scheme (`tenant:domain:aggregate-id`) that permeates all addressing. This story verifies those guarantees and fills any gaps.

### Architecture Compliance

- **FR24:** Multiple independent domains within same EventStore instance
- **FR25:** Multiple tenants within same domain, each with isolated event streams
- **FR15/FR28:** Composite key strategy for storage isolation: `{tenant}:{domain}:{aggId}:events:{seq}`
- **FR26:** Canonical identity tuple `tenant:domain:aggregate-id` drives all addressing
- **NFR20:** Adding new tenant/domain requires no restart -- config store changes take effect dynamically
- **D7:** Domain service endpoint resolved from DAPR config store: `tenant:domain:version -> appId + method`
- **D1:** Event storage keys include tenant prefix for structural isolation

### Security Constraints

- **SEC-2:** Tenant validation occurs BEFORE state rehydration (already enforced in Story 3.3)
- **SEC-3:** Command status queries are tenant-scoped
- **Triple-layer isolation:** API gateway -> actor identity -> state store keys -> pub/sub topics -> DAPR policies

### Critical Design Decisions

- **Multi-domain routing** relies on DomainServiceResolver looking up `tenant:domain:version` in DAPR config store (D7). Each domain maps to a different `appId + method` endpoint.
- **Multi-tenant isolation** is structurally enforced by:
  1. Actor ID derivation: `AggregateIdentity.ActorId` includes tenant, creating physically separate actors per tenant
  2. State store keys: All keys prefixed with `{tenant}:{domain}:{aggId}:...`
  3. TenantValidator: SEC-2 validation at actor level (Story 3.3)
  4. DAPR access control policies: External enforcement layer
- **Dynamic registration** (NFR20): DomainServiceResolver reads from DAPR config store on each invocation. No caching, no static configuration files, no restart needed.

### Architecture Decision Records (from Advanced Elicitation)

**ADR-1: DomainServiceResolver Caching -- NO CACHE**
- Read from DAPR config store on every invocation (DAPR sidecar caches internally)
- Zero stale data risk, perfect NFR20 compliance, simplest implementation
- ~1-2ms overhead per command is acceptable within 200ms e2e budget (NFR2)
- If profiling reveals bottleneck, revisit with config store change notifications

**ADR-2: Tenant Name Validation -- CURRENT VALIDATION (alphanumeric + dash)**
- Already implemented in `AggregateIdentity.cs`: `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`
- Prevents injection attacks (no colons, control chars, non-ASCII)
- Human-readable keys for debugging and monitoring
- If international tenant names needed, implement slug mapping at API boundary

**ADR-3: Actor Type Strategy -- SINGLE AggregateActor TYPE**
- Domain is routing metadata, not code -- domain-specific logic lives in domain services (D7)
- NFR20 compliance: adding a domain requires only config store registration, not code deployment
- DAPR actors require static registration; per-domain types would violate NFR20

**ADR-4: Config Store Key Schema -- FLAT PATTERN `{tenant}:{domain}:{version}`**
- Consistent with event key pattern (D1) and actor ID pattern
- Simple exact-key lookup, no hierarchical parsing
- Version field enables blue/green domain service deployments
- Default version to `v1` when not specified in command
- Version validated against `^v[0-9]+$` regex, normalized to lowercase

**ADR-5: Domain-Level Authorization -- NO DOMAIN AUTH (v1)**
- Tenant-level auth enforced (6-layer defense); domain auth is NOT enforced
- Tenants can access all their registered domains (simpler for v1 platform operator model)
- FR30-FR34 define tenant-scoped authorization, not domain-scoped
- Future: optional `eventstore:domain` JWT claims for SaaS scenarios
- Zero code changes required

**ADR-6: Tenant Offboarding -- OUT OF SCOPE (v1)**
- Tenant offboarding is an operational concern, not an EventStore feature for v1
- Config deletion = commands rejected with `DomainServiceNotFoundException` (graceful degradation)
- Event data retention, actor cleanup, and GDPR compliance deferred to Story 8.x
- Document soft-delete and hard-delete operational runbooks

### Security Analysis (from Advanced Elicitation)

**Red Team Findings:**
- **CRITICAL (H1):** Direct DAPR sidecar invocation bypasses HTTP security layers 1-4. Mitigation: network isolation (pod-level), Story 5.1 DAPR ACLs blocking for production.
- **CRITICAL (H2):** Config store poisoning can redirect domain service registrations. Mitigation: restrict config store write access to admin service accounts only.
- **HIGH (1.2):** Actor rebalancing race conditions mitigated by TenantValidator (Story 3.3 SEC-2).
- **ELIMINATED:** Key collision (composite keys), case sensitivity bypass (lowercase normalization), aggregate ID injection (regex validation), envelope tampering (SEC-1).

**FMEA High-RPN Items:**
- **RPN 120:** Config propagation delay across instances -- mitigated by no-cache strategy (ADR-1)
- **RPN 112:** Stale cache after config update -- eliminated by no-cache strategy (ADR-1)
- **RPN 96:** Actor activation timeout for large aggregates -- mitigated by snapshots (Story 3.9)

**Security Audit Findings (applied):**
- Domain service response size limits added (AC #6, max 1000 events per result)
- Config store write access restrictions documented (deployment requirement)
- DAPR sidecar network isolation documented (deployment requirement)

**Round 2 Findings (applied):**
- Version field injection prevention: validate `^v[0-9]+$`, normalize lowercase (Task 8.3)
- Per-event payload size limit added: `MaxEventSizeBytes` 1MB default (Task 7.2)
- No-op telemetry: log WARNING when domain service returns empty event list
- Version defaulting audit: log INFO when version defaults to v1 (Task 8.4)
- ADR-5: No domain-level authorization for v1 (intentional simplification)
- ADR-6: Tenant offboarding deferred to Story 8.x

**Round 2 Known Risks (documented, not blocking):**
- Config store is single point of failure under no-cache strategy (ADR-1). Circuit breaker deferred to Story 6.x.
- Version transitions require 5-minute overlap (both v1 and v2 registered simultaneously). Document in operational runbook.
- Domain services returning persistent no-ops are silent failures. Monitoring/alerting is operational concern.

**Deferred to future stories (out of scope for 3.6):**
- GDPR data lineage metadata on EventEnvelope (Story 8.x)
- Tenant isolation audit/verification tool (Story 8.x)
- Tenant lifecycle management / offboarding automation (Story 8.x)
- Immutable audit events for security failures (Story 6.x)
- Config store signed registrations (Story 6.x)
- Config store circuit breaker / emergency cache (Story 6.x)
- Multi-tenant threat model document (Story 6.x)
- Issuer validation in ClaimsTransformation (Story 5.x)
- Saga chunking / continuation tokens for >1000 events (Story 3.11)

### Previous Story Intelligence

**From Story 3.3 (Tenant Validation):**
- TenantValidator validates command tenant against actor identity BEFORE state rehydration
- Six-layer defense model is established
- UserId flows from JWT `sub` claim through CommandEnvelope

**From Story 3.4 (Event Stream Reader):**
- EventStreamReader uses composite key pattern `{tenant}:{domain}:{aggId}:events:{seq}` (D1)
- Tenant prefix in keys ensures structural isolation at storage level
- Parallel batching for performance (F5)

**From Story 3.5 (Domain Service Invocation):**
- DomainServiceResolver does config store lookup for `tenant:domain:version -> appId + method`
- DaprDomainServiceInvoker calls via `DaprClient.InvokeMethodAsync`
- DomainServiceNotFoundException thrown for unregistered tenant+domain combos
- DomainServiceResolver is DI-registered singleton (F2)

### Git Intelligence

Recent commits show Epic 2 completion (Stories 2.4-2.9: JWT Auth, Endpoint Authorization, Command Status, Rate Limiting, Concurrency). The codebase follows established patterns:
- Primary constructors with DI
- NSubstitute for mocking, Shouldly for assertions
- Feature folder organization
- `ConfigureAwait(false)` on all async calls

### Mandatory Coding Patterns

- Primary constructors: `public class Foo(IDep dep) : Base`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions
- Feature folder organization
- **Rule #4:** No custom retry logic (DAPR resiliency only)
- **Rule #5:** Never log event payload data
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** CorrelationId in every structured log entry

### Project Structure Notes

- Alignment with unified project structure:
  - `src/Hexalith.EventStore.Server/DomainServices/` - DomainServiceResolver, DaprDomainServiceInvoker
  - `src/Hexalith.EventStore.Server/Actors/` - AggregateActor
  - `src/Hexalith.EventStore.Server/Commands/` - CommandRouter
  - `src/Hexalith.EventStore.Contracts/Identity/` - AggregateIdentity
- Tests: `tests/Hexalith.EventStore.Server.Tests/` mirroring source structure
- Integration tests: `tests/Hexalith.EventStore.IntegrationTests/`

### Testing Requirements

**Unit Tests (~25-30 new):**
- DomainServiceResolver multi-tenant routing (5-6 tests)
- DomainServiceResolver versioned key lookup (3-4 tests)
- DomainServiceResolver version validation and normalization (3-4 tests)
- AggregateIdentity isolation verification (3-4 tests)
- CommandRouter multi-domain routing (3-4 tests)
- Cross-tenant state isolation (3-4 tests)
- Domain service response event count validation (2-3 tests)
- Domain service response event payload size validation (2-3 tests)

**Integration Tests (~10-12 new):**
- Multi-domain processing within single tenant
- Multi-tenant processing within same domain
- Dynamic tenant/domain addition without restart
- Unregistered tenant+domain error handling
- Actor state isolation verification
- Versioned config key resolution
- Domain service response exceeding max events
- Domain service response exceeding max event size

**Total estimated new tests: ~35-42**
**Total after story: ~494-501 tests**

### Deployment Security Requirements (Task 9)

**CRITICAL - DAPR Sidecar Network Isolation (Red Team H1, Task 9.1):**
- The DAPR sidecar (default port 3500) MUST be network-isolated to the pod/container
- Direct sidecar access bypasses HTTP security layers 1-4 (API gateway, JWT auth, rate limiting, request validation)
- Use Kubernetes NetworkPolicy or equivalent to restrict sidecar access to the EventStore container only
- Document in XML doc comments on `DomainServiceResolver.cs`

**CRITICAL - Config Store Write Access Restrictions (Red Team H2, Task 9.2):**
- Config store write access MUST be restricted to admin service accounts only
- Config store poisoning can redirect domain service registrations to malicious endpoints (e.g., `tenant-a:orders:v1` → attacker-controlled service)
- Use DAPR component-level RBAC or infrastructure-level access controls
- The `DomainServiceOptions.ConfigStoreName` references the target config store component
- Document in XML doc comments on `DomainServiceOptions.cs` and `DomainServiceResolver.cs`

**BLOCKING - Story 5.1 DAPR ACLs Required for Production (Task 9.3):**
- Story 5.1 (DAPR app-level access control policies) is BLOCKING for production multi-tenant deployments
- Without DAPR ACLs, any sidecar-accessible service can invoke any other service via `DaprClient.InvokeMethodAsync`
- This means a compromised tenant's domain service could invoke another tenant's domain service
- Story 5.1 adds `app-level` access policies in DAPR configuration to restrict service-to-service communication

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.6]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1 Event Storage Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#D7 Domain Service Invocation]
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns - Multi-Tenant Isolation]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-2 Tenant Validation]
- [Source: _bmad-output/planning-artifacts/architecture.md#D8 Rate Limiting - Per-tenant scoping]
- [Source: _bmad-output/planning-artifacts/architecture.md#Naming Patterns - DAPR State Store Keys]
- [Source: _bmad-output/implementation-artifacts/3-3-tenant-validation-at-actor-level.md]
- [Source: _bmad-output/implementation-artifacts/3-4-event-stream-reader-and-state-rehydration.md]
- [Source: _bmad-output/implementation-artifacts/3-5-domain-service-registration-and-invocation.md]

## Change Log

- Extended `IDomainServiceResolver.ResolveAsync` with `version` parameter (default "v1") and updated `DomainServiceResolver` key pattern from `{tenant}:{domain}:service` to `{tenant}:{domain}:{version}` (ADR-4)
- Added version extraction from `command.Extensions["domain-service-version"]` in `DaprDomainServiceInvoker` with format validation (`^v[0-9]+$`) and lowercase normalization
- Extended `DomainServiceOptions` with `MaxEventsPerResult` (default 1000) and `MaxEventSizeBytes` (default 1MB)
- Created `DomainServiceException` for response limit violations (event count, payload size)
- Added response size validation in `DaprDomainServiceInvoker.ValidateResponseLimits()`
- Added no-op WARNING logging for empty domain service results (silent failure detection)
- Updated `DomainServiceNotFoundException` with version context
- Added deployment security documentation (Red Team H1/H2, Story 5.1 blocking)
- Added 47 new tests across unit, integration, and contract test projects (528 → 575 total)

### Code Review Fixes (2026-02-14)

- **[H1]** Fixed `DaprDomainServiceInvoker.InvokeAsync` to pass extracted `version` to `DomainServiceNotFoundException` constructor (was always showing "v1" regardless of requested version)
- **[H2]** Added missing Task 4.3 integration test: `PostCommands_DynamicTenantAddition_SucceedsAfterReconfiguration` (AC #3, NFR20)
- **[M1]** Optimized `ValidateResponseLimits` to reuse a single `MemoryStream` instead of allocating per-event `byte[]` via `SerializeToUtf8Bytes`
- **[M2]** Changed `DomainServiceResolver` to throw `DomainServiceException` on corrupted config store JSON instead of silently returning null (distinguishes corruption from missing registration; improves Red Team H2 attack detection)
- **[M3]** Documented `.github/instructions/codacy.instructions.md` staged change (line-ending normalization, not story-related)
- Updated test: `ResolveAsync_MalformedJson_ReturnsNull` → `ResolveAsync_MalformedJson_ThrowsDomainServiceException`
- Test count: 575 → 576 total (1 new integration test)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Build error CS0313: nullable int `ShouldBeGreaterThan` — resolved by using `.Value` accessor
- NSubstitute mock signature mismatch after adding `version` parameter — resolved by updating all mock setups to 4-parameter signature

### Completion Notes List

- Task 1: DomainServiceResolver extended for `{tenant}:{domain}:{version}` key pattern with multi-tenant/multi-domain routing
- Task 2: AggregateIdentity actor isolation verified — different tenants/domains produce distinct ActorIds
- Task 3: Composite key isolation verified — all state store keys include tenant prefix, no cross-tenant overlap
- Task 4: Integration tests cover multi-domain routing, multi-tenant isolation, dynamic additions, error handling
- Task 5: Covered by Task 1 tests (DomainServiceResolver multi-tenant routing)
- Task 6: Explicit test verifies GetConfiguration called on every invocation (no caching per ADR-1)
- Task 7: Response size validation added — event count limit, individual payload size limit, DomainServiceException
- Task 8: Version extraction from command extensions, format validation, lowercase normalization, audit logging
- Task 9: Deployment security documented in XML doc comments and story artifact (H1: sidecar isolation, H2: config store RBAC, Story 5.1 blocking)
- Task 10: All 576 tests pass (0 failures, 0 skipped) — updated after code review fixes

### File List

**New Files:**
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceException.cs` — Exception for domain service response limit violations
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/MultiTenantRoutingIntegrationTests.cs` — Multi-tenant/multi-domain integration tests

**Modified Files:**
- `src/Hexalith.EventStore.Server/DomainServices/IDomainServiceResolver.cs` — Added `version` parameter to `ResolveAsync`
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` — Version normalization, format validation, security documentation
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` — Version extraction, response validation, no-op logging, `IOptions<DomainServiceOptions>`
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs` — Added `MaxEventsPerResult`, `MaxEventSizeBytes`, security documentation
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceNotFoundException.cs` — Added `Version` property and version context in message
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceResolverTests.cs` — Multi-tenant routing, version validation, normalization tests
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs` — Version extraction, response validation, exception tests
- `tests/Hexalith.EventStore.Contracts.Tests/Identity/AggregateIdentityTests.cs` — Actor isolation, composite key isolation tests
- `tests/Hexalith.EventStore.Server.Tests/Commands/CommandRouterTests.cs` — Multi-tenant/multi-domain routing tests
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — Status updated to review
- `_bmad-output/implementation-artifacts/3-6-multi-domain-and-multi-tenant-processing.md` — Task checkboxes, change log, file list, dev agent record
