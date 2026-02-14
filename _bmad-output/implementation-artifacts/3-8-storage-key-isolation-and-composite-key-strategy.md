# Story 3.8: Storage Key Isolation & Composite Key Strategy

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Stories 3.1-3.7 MUST have implementation artifacts before starting this story.**

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (Story 3.2 -- 5-step orchestrator)
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` (Story 1.2 -- 11-field envelope)
- `src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs` (Story 1.2)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` (Story 1.2 -- key derivation)
- `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` (Story 3.2)
- Event persistence infrastructure from Story 3.7 (IEventPersister, EventPersister)

Run `dotnet test` to confirm all existing tests pass before beginning.

## Story

As a **security auditor**,
I want event streams for different tenants to use isolated storage keys that are inaccessible to each other at the state store level,
So that multi-tenant data isolation is enforced at the storage layer (FR15, FR28).

## Acceptance Criteria

1. **Tenant-prefixed event keys** - Given events are persisted for multiple tenants, When I examine the state store keys, Then each event key includes the tenant prefix: `{tenant}:{domain}:{aggId}:events:{seq}` (D1), And tenant A's keys are structurally disjoint from tenant B's keys.

2. **Tenant-prefixed snapshot keys** - Given snapshots are created for aggregates, When I examine the snapshot state store keys, Then snapshot keys follow the same tenant-scoped pattern: `{tenant}:{domain}:{aggId}:snapshot`, And no snapshot key for tenant A can collide with any key for tenant B.

3. **Tenant-prefixed metadata keys** - Given aggregate metadata is stored, When I examine metadata keys, Then metadata keys follow the pattern: `{tenant}:{domain}:{aggId}:metadata`, And each tenant's metadata is structurally isolated from other tenants.

4. **No cross-tenant read path** - Given actor code processes commands for tenant A, When the actor reads events/metadata/snapshots, Then no API or actor code path can read events, metadata, or snapshots belonging to tenant B, And AggregateIdentity ensures all key derivation is scoped to the command's tenant.

5. **Backend-agnostic composite keys** - Given the composite key strategy uses string-based key patterns, When events are persisted to any DAPR-compatible state store, Then the key isolation works with Redis, PostgreSQL, Cosmos DB, or any other DAPR state store supporting key-value operations (NFR27), And no backend-specific features are required for isolation.

6. **Key format validation** - Given a key is derived from AggregateIdentity, When the tenant, domain, or aggregate ID contains special characters that could cause key collisions (e.g., colons in tenant ID), Then AggregateIdentity's existing validation rejects those inputs, And no key injection attack is possible via crafted identity components.

7. **Idempotency key isolation** - Given idempotency records are stored per actor, When examining idempotency keys, Then idempotency keys are scoped to the actor instance (which is already tenant-scoped via actor ID = `{tenant}:{domain}:{aggId}`), And idempotency records for tenant A cannot collide with records for tenant B.

8. **Command status key isolation** - Given command status entries use the pattern `{tenant}:{correlationId}:status` (D2), When status entries are queried, Then the tenant prefix prevents cross-tenant status information leakage, And status queries enforce JWT tenant matching (SEC-3, already implemented in Story 2.6).

9. **Negative cross-tenant access verification** - Given an actor processing commands for tenant-a, When a test attempts to read a state store key belonging to tenant-b using tenant-a's actor context, Then the read returns null/empty (DAPR actor scoping prevents cross-actor state access), And the composite key structure makes accidental cross-tenant reads structurally impossible even if actor scoping were bypassed.

10. **CommandRouter chain-of-custody verification** - Given a command arrives in the pipeline, When `CommandRouter` derives the actor ID for routing, Then it ALWAYS uses `command.AggregateIdentity.ActorId` (never manual string construction), And the tenant value flows unmodified from `CommandEnvelope` through `AggregateIdentity` to the actor ID.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and existing isolation mechanisms (BLOCKING)
  - [ ] 0.1 Run all existing tests -- they must pass before proceeding
  - [ ] 0.2 Confirm `AggregateIdentity` key derivation properties exist: `EventStreamKeyPrefix`, `MetadataKey`, `SnapshotKey`, `ActorId`
  - [ ] 0.3 Confirm `AggregateIdentity` validation rejects colons, control characters, and non-ASCII in tenant/domain/aggregateId
  - [ ] 0.4 Confirm `IdempotencyChecker` uses actor-scoped keys (via `IActorStateManager` which is already actor-scoped)
  - [ ] 0.5 Confirm `DaprCommandStatusStore` uses `{tenant}:{correlationId}:status` key pattern
  - [ ] 0.6 Document current key patterns used across the codebase

- [ ] Task 1: Create StorageKeyIsolationAssertions test utility (AC: #4, #6)
  - [ ] 1.1 Create `StorageKeyIsolationAssertions` in `Testing/Assertions/` (test project, NOT production code)
  - [ ] 1.2 Provide static assertion methods for test verification:
    - `AssertKeyBelongsToTenant(string key, string expectedTenant)` -- asserts key starts with `{expectedTenant}:`
    - `AssertKeysDisjoint(string keyA, string keyB)` -- asserts two keys share no common prefix up to first segment
    - `AssertEventStreamKey(string key, AggregateIdentity identity)` -- validates full key structure matches identity
  - [ ] 1.3 These are test assertion helpers only -- NOT production runtime guards. Isolation is guaranteed by construction (AggregateIdentity validation), not by runtime checking.
  - [ ] 1.4 Namespace: `Hexalith.EventStore.Testing.Assertions`

- [ ] Task 2: Create comprehensive isolation test suite -- unit tests (AC: #1, #2, #3, #4, #6, #9, #10)
  - [ ] 2.1 Create `StorageKeyIsolationTests` in `Server.Tests/Security/`
  - [ ] 2.2 Test: Two different tenants produce structurally disjoint event stream key prefixes
  - [ ] 2.3 Test: Two different tenants produce structurally disjoint metadata keys
  - [ ] 2.4 Test: Two different tenants produce structurally disjoint snapshot keys
  - [ ] 2.5 Test: Same aggregate ID in different tenants produces different keys (no collision)
  - [ ] 2.6 Test: Same aggregate ID in same tenant but different domains produces different keys
  - [ ] 2.7 Test: AggregateIdentity rejects tenant IDs containing colons (key injection prevention)
  - [ ] 2.8 Test: AggregateIdentity rejects domain names containing colons
  - [ ] 2.9 Test: AggregateIdentity rejects aggregate IDs containing colons
  - [ ] 2.10 Test: Key prefix `{tenantA}:` never appears as a prefix of any key derived for `{tenantB}`
  - [ ] 2.11 Test: StorageKeyIsolationAssertions correctly validates keys belong to expected tenant
  - [ ] 2.12 Test: StorageKeyIsolationAssertions rejects keys that don't match expected tenant
  - [ ] 2.13 Test: CommandRouter always uses `command.AggregateIdentity.ActorId` for actor ID derivation (AC: #10)
  - [ ] 2.14 Test: Tenant value chain-of-custody -- CommandEnvelope.TenantId == AggregateIdentity.TenantId == first segment of all derived keys
  - [ ] 2.15 Test: URL-encoded colons (`%3A`) rejected by AggregateIdentity regex (percent sign not in allowed chars)
  - [ ] 2.16 Test: NEGATIVE -- reading key `{tenantB}:{domain}:{aggId}:events:1` returns null when queried from tenantA's context

- [ ] Task 3: Create multi-tenant isolation integration tests (AC: #1, #4, #5, #7, #9)
  - [ ] 3.1 Create `MultiTenantStorageIsolationTests` in `IntegrationTests/Security/`
  - [ ] 3.2 Test: Commands for tenant-a and tenant-b produce events with non-overlapping state store keys
  - [ ] 3.3 Test: Actor for tenant-a cannot access state store keys belonging to tenant-b (DAPR actor scope isolation via actor ID)
  - [ ] 3.4 Test: Idempotency records for tenant-a are invisible to tenant-b's actor instance
  - [ ] 3.5 Test: Full pipeline test -- submit commands for two tenants, verify complete key isolation in state store
  - [ ] 3.6 Test: NEGATIVE -- explicitly attempt to read tenant-b's event from tenant-a's actor, assert null/failure (AC: #9)
  - [ ] 3.7 Test: NEGATIVE -- submit command for tenant-a, verify tenant-b cannot retrieve those events via any code path

- [ ] Task 4: Create command status isolation tests (AC: #8)
  - [ ] 4.1 Create `CommandStatusIsolationTests` in `IntegrationTests/Security/` or extend existing status tests
  - [ ] 4.2 Test: Command status for tenant-a is not retrievable with tenant-b's JWT
  - [ ] 4.3 Test: Status key `{tenantA}:{correlationId}:status` structurally disjoint from `{tenantB}:{correlationId}:status` even with same correlationId

- [ ] Task 5: Document composite key strategy and future-proofing (AC: #5)
  - [ ] 5.1 Add XML doc comments to `AggregateIdentity` key derivation properties documenting: (a) the isolation guarantee, (b) why colons are forbidden in components, (c) the 4-layer isolation model
  - [ ] 5.2 Add inline comments in `EventPersister` (from Story 3.7) referencing FR15, FR28 isolation requirements
  - [ ] 5.3 Add warning comment in `EventPersister` or `AggregateActor`: "SECURITY: Never use `DaprClient.QueryStateAsync` or bulk state queries without explicit tenant filtering. DAPR query API does not enforce actor state scoping. See FR28."
  - [ ] 5.4 Add warning comment in `CommandRouter`: "SECURITY: Always derive actor ID from `AggregateIdentity.ActorId`. Never construct actor IDs via manual string concatenation. See FR15, FR28."

- [ ] Task 6: Verify all existing tests pass
  - [ ] 6.1 Run `dotnet test` to confirm no regressions
  - [ ] 6.2 All new isolation tests pass

## Dev Notes

### Story Context

This story is primarily a **verification and validation story** rather than a feature implementation story. The composite key isolation is already architecturally guaranteed by:

1. **AggregateIdentity** (Story 1.2) -- derives all keys with tenant prefix, validates no colons/injection in components
2. **DAPR Actor scoping** -- each actor instance has an ID = `{tenant}:{domain}:{aggId}`, and `IActorStateManager` scopes all state operations to that actor instance
3. **EventPersister** (Story 3.7) -- uses `identity.EventStreamKeyPrefix` for event keys, `identity.MetadataKey` for metadata
4. **DaprCommandStatusStore** (Story 2.6) -- uses `{tenant}:{correlationId}:status` pattern

The primary work in this story is creating a comprehensive test suite that **proves** the isolation guarantees hold under various scenarios, plus a small utility for audit/assertion purposes. There is minimal new production code -- mostly tests.

### Architecture Compliance

- **FR15:** Composite key strategy includes tenant, domain, and aggregate identity for isolation
- **FR28:** Storage key isolation -- event streams for different tenants inaccessible to each other at state store level
- **D1:** Single-key-per-event with pattern `{tenant}:{domain}:{aggId}:events:{seq}`
- **D2:** Command status at `{tenant}:{correlationId}:status`
- **NFR27:** Works with any DAPR-compatible state store supporting key-value operations
- **SEC-2:** Tenant validation before state access (Story 3.3)
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #11:** Write-once event keys

### 4-Layer Storage Isolation Model

This story verifies a defense-in-depth isolation model with 4 independent layers:

| Layer | Mechanism | Enforcement Point | What It Prevents |
|-------|-----------|-------------------|------------------|
| 1. Input Validation | AggregateIdentity regex rejects colons, control chars, non-ASCII | Construction time | Key injection attacks via crafted tenant/domain/aggregateId |
| 2. Composite Key Prefixing | All keys start with `{tenant}:` -- structurally disjoint per tenant | Key derivation | Accidental key collisions between tenants |
| 3. DAPR Actor State Scoping | `IActorStateManager` internally namespaces keys by `{actorType}\|\|{actorId}` | DAPR runtime | Cross-actor state access even with same logical key names |
| 4. JWT Tenant Enforcement | API layer (Story 2.5) + Actor layer (Story 3.3) validate tenant | Request processing | Unauthorized access even if key structure were compromised |

**Each layer is independently sufficient for basic isolation.** Combined, they provide defense-in-depth where failure at one layer does not compromise isolation (NFR13).

### Security Constraints

- **Key injection prevention:** AggregateIdentity validates all components against strict regex patterns. Tenant and domain must match `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` -- no colons, no special characters that could break key boundary parsing. Aggregate ID matches `^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$` -- also no colons. URL-encoded colons (`%3A`) are also rejected since `%` is not in the allowed character set.
- **Structural disjointness:** Since tenant is always the first segment and colons are forbidden in tenant/domain/aggregateId, the key prefix `{tenantA}:` can never be a prefix of any key derived for a different tenant.
- **Actor-level isolation:** DAPR actors provide process-level isolation per actor ID. `IActorStateManager` operations are automatically scoped to the actor instance. Tenant A's actor cannot access Tenant B's actor state.
- **Chain of custody:** The tenant value flows unmodified through: JWT claims -> API validation -> `CommandEnvelope.TenantId` -> `CommandEnvelope.AggregateIdentity` (constructed in record init) -> `CommandRouter` uses `AggregateIdentity.ActorId` -> DAPR actor activated with that ID -> `StateManager` scoped to that actor ID. At no point is the tenant re-parsed from a string or reconstructed from state.

### Critical Design Decisions

- **No runtime key-checking middleware needed.** The isolation is guaranteed by construction (AggregateIdentity derives keys, validation prevents injection). Adding runtime checks would be defense-in-depth but architecturally redundant and could mask bugs rather than preventing them.
- **StorageKeyIsolationAssertions is a test-only utility.** Lives in the Testing project, NOT production code. The correct approach is to verify at construction time (AggregateIdentity validation) not at access time.
- **DAPR actor state scoping is implicit.** When an actor uses `StateManager.SetStateAsync("foo", value)`, DAPR internally prefixes the key with the actor type and ID. This means even if two actors used the same logical key name, they would not collide. Our explicit tenant-prefixed keys provide an additional layer of isolation.
- **CommandRouter MUST use AggregateIdentity.ActorId.** Never manually construct actor IDs via string concatenation. The chain of custody from `CommandEnvelope` through `AggregateIdentity` to actor ID must be unbroken.

### Future-Proofing Warnings

- **NEVER use `DaprClient.QueryStateAsync` or bulk state queries without explicit tenant filtering.** DAPR's query API operates at the state store level, NOT at the actor level. It bypasses actor state scoping and can return keys from all tenants. Any future use of query APIs MUST include tenant-prefixed key filters. This is the single most likely vector for a future cross-tenant data leak.
- **NEVER bypass `IActorStateManager` with direct `DaprClient.GetStateAsync` / `SetStateAsync`.** Rule #6 exists specifically to prevent this. Direct state store access bypasses actor state namespacing.
- **Infrastructure access (Redis CLI, database queries) is out of scope** for application-level isolation but must be tenant-scoped in operational tooling. A `KEYS *:events:*` Redis command exposes all tenants' data.

### Previous Story Intelligence

**From Story 3.7 (Event Persistence):**
- EventPersister uses `identity.EventStreamKeyPrefix + sequenceNumber` for event keys
- AggregateMetadata stored at `identity.MetadataKey`
- All keys derived from AggregateIdentity -- tenant isolation is baked in

**From Story 3.3 (Tenant Validation):**
- Tenant validation occurs BEFORE state rehydration (SEC-2)
- If tenant doesn't match, command rejected before any state access

**From Story 3.6 (Multi-Domain/Multi-Tenant):**
- Verified multi-tenant and multi-domain processing with isolated event streams
- DomainServiceOptions.MaxEventsPerResult limits event count

**From Story 2.6 (Command Status):**
- Status key pattern: `{tenant}:{correlationId}:status`
- Status queries enforce JWT tenant matching (SEC-3)

### Git Intelligence

Recent commits show Epic 2 completion and Epic 3 stories 3.1-3.2 infrastructure. The codebase follows:
- Primary constructors with DI
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions
- Feature folder organization

### Mandatory Coding Patterns

- Primary constructors: `public class Foo(IDep dep) : Base`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions
- Feature folder organization
- **Rule #5:** Never log event payload data
- **Rule #6:** IActorStateManager for all actor state operations
- **Rule #9:** CorrelationId in every structured log entry
- **Rule #11:** Event store keys are write-once

### Project Structure Notes

- `src/Hexalith.EventStore.Testing/Assertions/` -- StorageKeyIsolationAssertions (new, test utility)
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` -- existing (doc enhancement only)
- `src/Hexalith.EventStore.Server/Commands/CommandRouter.cs` -- existing (add security warning comment)
- `tests/Hexalith.EventStore.Server.Tests/Security/` -- StorageKeyIsolationTests (new)
- `tests/Hexalith.EventStore.IntegrationTests/Security/` -- MultiTenantStorageIsolationTests (new)
- `tests/Hexalith.EventStore.IntegrationTests/Security/` -- CommandStatusIsolationTests (new)

### Existing Key Derivation (from AggregateIdentity.cs)

```csharp
// All key patterns derived from validated, colon-free components:
public string EventStreamKeyPrefix => $"{TenantId}:{Domain}:{AggregateId}:events:";
public string MetadataKey => $"{TenantId}:{Domain}:{AggregateId}:metadata";
public string SnapshotKey => $"{TenantId}:{Domain}:{AggregateId}:snapshot";
public string ActorId => $"{TenantId}:{Domain}:{AggregateId}";

// Validation ensures no colons in any component:
// TenantId: ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$ (max 64 chars)
// Domain:   ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$ (max 64 chars)
// AggregateId: ^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$ (max 256 chars)
```

### Testing Requirements

**Unit Tests (~16-20 new):**
- StorageKeyIsolationAssertions: assertion method correctness (3 tests)
- AggregateIdentity key disjointness: cross-tenant, cross-domain scenarios (8+ tests)
- Key injection prevention: colons, URL-encoded colons, control chars (3 tests)
- CommandRouter: always uses AggregateIdentity.ActorId (1 test)
- Chain-of-custody: tenant value consistency through pipeline (1 test)

**Integration Tests (~7-10 new):**
- Multi-tenant event key isolation (pipeline test)
- Actor state scope isolation
- Idempotency record isolation
- Command status isolation
- Full end-to-end multi-tenant isolation
- NEGATIVE: explicit cross-tenant read attempt (2 tests)

**Total estimated new tests: ~23-30**

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.8]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1 Event Storage Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR15 Composite key strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR28 Storage key isolation]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR27 Backend-agnostic state store]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC constraints]
- [Source: _bmad-output/implementation-artifacts/3-7-event-persistence-with-atomic-writes-and-sequence-numbers.md]
- [Source: _bmad-output/implementation-artifacts/3-3-tenant-validation-at-actor-level.md]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
