# Story 3.6 Architecture Decision Records - Round 2

**Story:** 3.6 Multi-Domain & Multi-Tenant Processing
**Date:** 2026-02-14
**Status:** Round 2 ADRs (Round 1 completed: ADR-1 through ADR-4)

## Context

Round 1 resolved 4 foundational ADRs:
- **ADR-1:** DomainServiceResolver has no application-level caching (DAPR sidecar caches)
- **ADR-2:** Tenant name validation uses current alphanumeric + dash regex
- **ADR-3:** Single `AggregateActor` type (domain is routing metadata, not code)
- **ADR-4:** Flat config store keys `{tenant}:{domain}:{version}` (not hierarchical)

Story 3.6 enhanced with these decisions now surfaces 2 new architectural questions that impact implementation scope and security posture.

## Current System Context

**6-Layer Defense Model (from architecture.md):**
1. API Gateway JWT validation
2. Claims transformation (`EventStoreClaimsTransformation`)
3. Controller tenant authorization (`CommandsController` line 51-66)
4. MediatR pipeline authorization (`AuthorizationBehavior` line 45-61)
5. Actor-level tenant validation (`TenantValidator` - Story 3.3)
6. DAPR access control policies (Story 5.1)

**Current Authorization Implementation (from AuthorizationBehavior.cs):**
- **Tenant authorization:** ENFORCED at Layer 3 (controller) - JWT must contain `eventstore:tenant` claim matching `request.Tenant`
- **Domain authorization:** CONDITIONAL enforcement at Layer 4 (MediatR) - **only enforced if user has domain claims**. If JWT contains `eventstore:domain` claims, command domain must match. If no domain claims, all domains allowed for that tenant.
- **Permission authorization:** CONDITIONAL enforcement at Layer 4 - only enforced if user has permission claims

**Current Claims Transformation (from EventStoreClaimsTransformation.cs):**
- Extracts `tenants` (array or space-delimited) → `eventstore:tenant` claims
- Extracts `domains` (array or space-delimited) → `eventstore:domain` claims
- Extracts `permissions` (array or space-delimited) → `eventstore:permission` claims
- Supports singular `tenant_id` or `tid` claims

**Config Store Registration Pattern (from Story 3.5):**
- Key: `{tenant}:{domain}:{version}` (defaults to `v1`)
- Value: `{ "appId": "...", "methodName": "...", "version": "..." }`
- Registration implies deployment configuration, not per-request authorization

---

## ADR-5: Domain-Level Authorization

### Context

The current 6-layer defense validates **TENANT authorization** but does **NOT universally validate DOMAIN authorization**. The existing implementation uses opt-in domain authorization:

```csharp
// AuthorizationBehavior.cs line 53
if (domainClaims.Count > 0 && !domainClaims.Any(d => string.Equals(d, command.Domain, ...)))
{
    throw new CommandAuthorizationException(...);
}
```

**Implication:** A tenant authorized for "orders" domain could potentially submit commands to "inventory" domain if:
- JWT contains `eventstore:tenant: tenant-a` but NO `eventstore:domain` claims
- Both `tenant-a:orders:service` and `tenant-a:inventory:service` are registered in config store
- No domain claims present = all domains allowed for that tenant

**Scenario:** Multi-tenant SaaS where Tenant A subscribes to "orders" module but NOT "inventory" module. Current system cannot enforce this boundary via JWT authorization alone.

### Options

#### Option A: No Domain-Level Authorization (Current Behavior)

**Approach:** Tenant has access to all their registered domains. Domain authorization is opt-in via JWT `eventstore:domain` claims.

**Trade-offs:**
- **Pros:**
  - Simplest JWT structure - only requires `eventstore:tenant` claim
  - Matches "platform operator" mental model where tenant registration = full access
  - Config store registration acts as authorization (deployment-time decision)
  - No JWT updates needed when adding new domain to existing tenant
  - Aligns with Story 3.6's focus on multi-domain processing, not multi-domain isolation
- **Cons:**
  - Cannot enforce per-domain authorization at runtime without JWT claims
  - SaaS subscription model (tenant pays per module) requires JWT updates for entitlement enforcement
  - Cross-domain command submission not prevented by authorization layer alone
  - Relies on application-level controls outside EventStore for domain access control

**Implementation Impact:** ZERO - current behavior, no code changes.

#### Option B: Domain Authorization via JWT Claims (Explicit, Opt-In)

**Approach:** If JWT contains `eventstore:domain` claims, enforce domain authorization. If no domain claims, all registered domains allowed. **Current implementation.**

**Trade-offs:**
- **Pros:**
  - Flexibility for both models: platform operators (no domain claims) and SaaS entitlements (domain claims)
  - Zero breaking changes - existing deployments without domain claims continue working
  - Aligns with current code (`AuthorizationBehavior.cs` line 53)
  - Domain authorization is explicit opt-in, not implicit enforcement
- **Cons:**
  - Inconsistent enforcement model - tenant always enforced, domain conditionally enforced
  - Requires JWT issuer to understand EventStore's authorization model (when to include domain claims)
  - Security relies on external JWT issuer policy, not EventStore guarantee
  - Pre-mortem Scenario 1 risk: If JWT issuer mistakenly omits domain claims, over-authorization occurs

**Implementation Impact:** ZERO - current behavior, document in architecture.

#### Option C: Domain Authorization via Config Store (Implicit Registration = Authorization)

**Approach:** Registration in config store IS the authorization. If `tenant-a:orders:service` exists, tenant-a is authorized for orders domain. No JWT domain claims needed.

**Trade-offs:**
- **Pros:**
  - Single source of truth for domain access: config store
  - No JWT complexity - only `eventstore:tenant` claim required
  - Aligns with deployment-time configuration model (NFR20 dynamic registration)
  - Prevents "unregistered domain" vs. "registered but not authorized" ambiguity
  - Config store already drives domain service resolution (DomainServiceResolver)
- **Cons:**
  - Config store becomes security-critical (already flagged as Red Team H2)
  - Cannot distinguish "not deployed" vs. "deployed but not authorized for this tenant" at runtime
  - SaaS subscription changes require config store updates (operational overhead)
  - Pre-mortem Scenario 3 risk amplified - config store corruption affects both routing AND authorization
  - Violates separation of concerns - config store is infrastructure, not authorization policy

**Implementation Impact:** HIGH - `AuthorizationBehavior` would query `DomainServiceResolver` to check if `{tenant}:{domain}:{version}` exists before allowing command.

#### Option D: Mandatory Domain Authorization (Always Enforce)

**Approach:** JWT MUST contain `eventstore:domain` claims. Reject commands if no domain claims present.

**Trade-offs:**
- **Pros:**
  - Consistent enforcement - tenant AND domain always validated
  - Explicit authorization model, no implicit assumptions
  - Clear security posture: zero trust, verify both tenant and domain
  - Prevents accidental over-authorization due to missing claims
- **Cons:**
  - **BREAKING CHANGE** - all existing JWTs must add domain claims
  - Increased JWT size (1-10 domains per tenant, ~50-100 bytes)
  - JWT issuer complexity - must maintain tenant-to-domain entitlement mapping
  - Dynamic domain addition requires JWT reissuance (conflicts with NFR20 "no restart" promise)
  - Story 3.6 scope inflation - mandates JWT issuer changes outside EventStore

**Implementation Impact:** MEDIUM - change `if (domainClaims.Count > 0 && ...)` to `if (!domainClaims.Any(...))` in `AuthorizationBehavior.cs`.

### Recommendation

**Option A: No Domain-Level Authorization (Document Current Behavior)**

**Rationale:**

1. **Story 3.6 Scope Alignment:** Story 3.6 is about multi-domain/multi-tenant **processing**, not per-domain **authorization**. AC #1-#7 focus on routing, isolation, and registration. Authorization granularity is out of scope.

2. **Deployment Model:** Config store registration is a deployment-time decision made by platform operators. Registration implies "this tenant+domain combination is active and routable." Separating "deployed" from "authorized" creates operational complexity without clear FR requirement.

3. **Existing FR Coverage:** FR30-FR34 define tenant-scoped authorization, not domain-scoped. "A tenant can only access their own commands, events, and status queries." No FR states "A tenant can only access specific domains within their tenant scope."

4. **Security Analysis:** Pre-mortem analysis (Scenario 1, 4, 6) focuses on tenant isolation breaches, not domain isolation. Six-layer defense protects tenant boundaries. Domain boundaries are logical partitions within a tenant's scope.

5. **DAPR ACL Layer (Story 5.1):** External DAPR policies can enforce domain-level routing restrictions if needed. EventStore focuses on tenant isolation; DAPR enforces service-to-service policies.

6. **Zero Breaking Change:** Current deployments continue working. SaaS scenarios requiring domain entitlements can use Option B (opt-in JWT claims) without code changes.

**Decision:**
- **Tenant authorization:** MANDATORY - enforced at Layers 3, 4, 5
- **Domain authorization:** OPTIONAL - enforced at Layer 4 ONLY IF `eventstore:domain` claims present
- Config store registration does NOT imply authorization (registration is routing metadata)
- Document in architecture: "Domain-level authorization is opt-in via JWT claims. To enforce per-domain access control, include `eventstore:domain` claims in JWT. If no domain claims present, tenant has access to all registered domains."

**Action Items:**
1. Document opt-in domain authorization model in `architecture.md` (Cross-Cutting Concerns section)
2. Add example JWTs to `docs/security/jwt-claims.md` showing both models:
   - Platform operator: `{ "eventstore:tenant": "ops-tenant" }` (all domains)
   - SaaS entitlement: `{ "eventstore:tenant": "saas-customer", "eventstore:domain": ["orders", "payments"] }` (restricted)
3. Update Story 2.5 (Endpoint Authorization) documentation with domain authorization clarification
4. Add integration test: `TenantWithoutDomainClaims_CanAccessAllRegisteredDomains()`

---

## ADR-6: Tenant Offboarding Strategy

### Context

Story 3.6 defines tenant **onboarding** (dynamic registration via config store, NFR20), but tenant **offboarding** is undefined. When a tenant is removed from the system, what happens to their:
- **Config registrations:** `{tenant}:{domain}:{version}` keys in DAPR config store
- **Event streams:** `{tenant}:{domain}:{aggId}:events:{seq}` keys in DAPR state store
- **Snapshots:** `{tenant}:{domain}:{aggId}:snapshot` keys
- **Actors:** Active `AggregateActor` instances in DAPR placement service
- **Command status:** Status query results
- **Pub/sub subscriptions:** `{tenant}.{domain}.events` topics

**Pre-mortem References:**
- Scenario 5 (Performance Cliff): 50 tenants × 1000 aggregates = 50K actors. What happens when tenant 25 is removed?
- Scenario 3 (Config Store Split-Brain): Config propagation delay. Partially offboarded tenant creates inconsistent state.
- Red Team H2: Config store write access must be restricted. Who can delete tenant registrations?

**Current System State:**
- No tenant lifecycle APIs defined (Story 3.6 is processing-focused, not admin-focused)
- No soft delete or tombstone pattern in state store keys
- DAPR actors auto-deactivate after 60-min idle timeout (DAPR default)
- DAPR state store keys persist indefinitely unless explicitly deleted

### Options

#### Option A: Soft Delete (Remove Config, Reject Commands, Events Remain)

**Approach:**
- **Config store:** Delete `{tenant}:{domain}:{version}` keys (or set `enabled: false` flag)
- **Event streams:** RETAIN all `{tenant}:{domain}:{aggId}:events:{seq}` keys
- **New commands:** Rejected with `DomainServiceNotFoundException` (no config = not routable)
- **Actors:** Idle actors auto-deactivate after 60 minutes
- **Event replay:** Historical events remain queryable via direct state store access (operational tooling)

**Trade-offs:**
- **Pros:**
  - Data preservation for compliance (GDPR right to erasure requires explicit deletion, not default)
  - Rollback possible - re-add config registration to reactivate tenant
  - Event history available for audit, analytics, or re-onboarding
  - Minimal operational risk - no cascading deletes
  - Aligns with immutable event log principle (events are historical facts)
- **Cons:**
  - State store size grows indefinitely (storage cost for inactive tenants)
  - Key enumeration (if exposed) reveals offboarded tenant IDs
  - No automatic garbage collection of tenant data
  - Requires separate data retention policy enforcement (Story 8.x scope)
  - Zombie data risk if offboarding process incomplete (config deleted, but events remain without policy)

**Implementation Impact:**
- ZERO code changes (current behavior - no offboarding implementation)
- Document operational runbook: "Tenant Offboarding: Delete config store keys. Events persist. Implement retention policy separately."
- Story 8.x dependency: Data retention and GDPR compliance

#### Option B: Hard Delete (Remove Config + Events + Actors)

**Approach:**
- **Config store:** Delete `{tenant}:{domain}:{version}` keys
- **Event streams:** DELETE all `{tenant}:{domain}:{aggId}:events:{seq}` keys for tenant
- **Snapshots:** DELETE all `{tenant}:{domain}:{aggId}:snapshot` keys
- **Actors:** Deactivate all tenant actors via DAPR actor delete API (if supported)
- **Pub/sub:** Unpublish/delete `{tenant}.{domain}.events` topics

**Trade-offs:**
- **Pros:**
  - Complete tenant removal - zero data footprint after offboarding
  - Storage cost optimization (no zombie data)
  - Clear security posture - offboarded tenant data fully erased
  - Aligns with "right to be forgotten" compliance (GDPR Article 17)
  - No partial offboarding risk (all or nothing)
- **Cons:**
  - **IRREVERSIBLE** - no rollback possible
  - Data loss risk if offboarding initiated accidentally
  - Complex operational procedure - multi-step delete across config, state store, pub/sub
  - State store key enumeration required (`SCAN` for `{tenant}:*`) - performance impact at scale
  - Pre-mortem Scenario 5 risk: Mass delete during peak load could overwhelm state store
  - No event history for audit or analytics after deletion
  - DAPR actor delete API may not be universally supported (backend-specific)

**Implementation Impact:**
- HIGH - requires new `TenantOffboardingService` with:
  - Config store delete via `DaprClient.DeleteConfiguration`
  - State store batch delete (DAPR SDK doesn't expose bulk delete - requires backend-specific implementation)
  - Actor deactivation (DAPR Actors SDK doesn't expose delete API - actors auto-deactivate on idle)
  - Pub/sub topic deletion (backend-specific, not abstracted by DAPR)
  - Idempotency and partial failure handling (multi-step distributed operation)

#### Option C: Out of Scope for EventStore (Operational Concern)

**Approach:**
- EventStore provides runtime processing, not lifecycle management
- Tenant offboarding handled by external admin tooling (not in EventStore scope)
- EventStore guarantees: If config deleted, commands rejected. If events deleted, state rehydration fails gracefully.
- Lifecycle policy (soft vs. hard delete, retention periods) determined by platform operators

**Trade-offs:**
- **Pros:**
  - Story 3.6 scope containment - focus on processing, not admin operations
  - Platform flexibility - operators choose soft delete, hard delete, or hybrid based on compliance needs
  - EventStore remains "building block" not "complete platform" (aligns with DAPR philosophy)
  - Defers complex lifecycle decisions to Story 6.x (Operations & Monitoring) or Story 8.x (Compliance)
  - No code complexity for v1 - operational runbooks document manual procedures
- **Cons:**
  - No automated offboarding workflow in v1
  - Operators must implement lifecycle logic using DAPR APIs directly
  - Risk of inconsistent offboarding procedures across deployments
  - Pre-mortem Scenario 3 risk remains unmitigated (partial config deletion)
  - Documentation burden - must provide clear runbooks for soft/hard delete procedures

**Implementation Impact:**
- ZERO code changes
- Document two runbooks:
  - `docs/operations/tenant-soft-delete.md` - config removal, event preservation
  - `docs/operations/tenant-hard-delete.md` - complete data removal (with warnings)
- Story 6.x or 8.x: Implement `TenantLifecycleService` with automated workflows

### Recommendation

**Option C: Out of Scope for EventStore (Document Runbooks, Defer to Future Story)**

**Rationale:**

1. **Story 3.6 Scope:** Story 3.6 AC #1-#7 focus on multi-domain/multi-tenant **processing**. Lifecycle management (onboarding automation, offboarding workflows) is not in acceptance criteria. NFR20 states "adding tenant/domain requires no restart" but does NOT mandate automated offboarding.

2. **v1 Scope Constraint:** Product brief emphasizes "solo developer, v1 scope deliberately sized." Tenant lifecycle management is a complex distributed operation (config + state store + pub/sub coordination, idempotency, rollback, audit logging). This exceeds v1 scope.

3. **Platform Operator Model:** EventStore targets platform operators (FR40-FR47), not end-user SaaS. Operators have infrastructure access and can execute manual DAPR operations. Automated lifecycle is a v2+ "nice to have," not v1 requirement.

4. **Compliance Deferral:** GDPR compliance (right to erasure, data retention policies) is explicitly deferred to Story 8.x (see Story 3.6 dev notes: "GDPR data lineage metadata on EventEnvelope (Story 8.x)").

5. **Flexibility Over Automation:** Different deployments have different lifecycle needs:
   - Development: Soft delete (rollback testing)
   - Staging: Hard delete (clean slate)
   - Production: Retention policy + archival (compliance)

   Prescribing one strategy in v1 limits flexibility.

6. **Graceful Degradation:** Current implementation already handles offboarding gracefully:
   - Config deleted → `DomainServiceNotFoundException` (commands rejected)
   - Events deleted → State rehydration returns empty state (actor initializes as new)
   - Actors idle → Auto-deactivate after 60 minutes (DAPR default)

   No code changes needed for basic functionality.

**Decision:**
- Tenant offboarding is **out of scope for Story 3.6 and v1**
- EventStore guarantees consistent behavior if external tooling deletes config/events
- Provide **two operational runbooks** documenting manual offboarding procedures:
  - **Soft delete:** Config removal, event preservation, rollback procedure
  - **Hard delete:** Complete removal, irreversibility warnings, compliance notes
- **Story 8.x dependency:** Automated `TenantLifecycleService` with retention policies, audit logging, and GDPR compliance

**Action Items:**
1. Create `docs/operations/tenant-soft-delete.md` runbook:
   - Delete `{tenant}:{domain}:{version}` keys via `dapr configuration delete`
   - Document expected behavior: Commands rejected, events retained, actors auto-deactivate
   - Rollback: Re-add config registration
   - Verification: Submit test command, expect `DomainServiceNotFoundException`

2. Create `docs/operations/tenant-hard-delete.md` runbook:
   - **WARNING:** Irreversible operation, verify tenant ID 3 times before execution
   - Step 1: Delete config keys
   - Step 2: Enumerate and delete state store keys (Redis: `SCAN` + `DEL`, PostgreSQL: `DELETE WHERE key LIKE '{tenant}:%'`)
   - Step 3: Delete pub/sub topics (backend-specific procedure)
   - Step 4: Document data retention compliance (export before delete if required)
   - Verification: Confirm zero keys remain for tenant

3. Document in `architecture.md` (Cross-Cutting Concerns):
   - "Tenant lifecycle (offboarding, retention policies) is operational concern, not EventStore runtime concern. See runbooks for manual procedures. Automated lifecycle management is Story 8.x scope."

4. Add to Story 8.x backlog:
   - `TenantLifecycleService` with automated soft/hard delete workflows
   - Data retention policy engine
   - GDPR compliance audit trail
   - Lifecycle event publishing (`TenantOffboarded`, `TenantDataErased`)

---

## Summary

### ADR-5: Domain-Level Authorization
- **Decision:** No domain-level authorization (Option A)
- **Enforcement:** Tenant authorization mandatory; domain authorization opt-in via JWT claims
- **Rationale:** Story 3.6 scope, zero breaking change, aligns with FR30-FR34 tenant focus
- **Impact:** Documentation updates, integration test

### ADR-6: Tenant Offboarding Strategy
- **Decision:** Out of scope for EventStore (Option C)
- **Approach:** Document manual runbooks, defer automation to Story 8.x
- **Rationale:** v1 scope constraint, platform operator model, compliance deferral
- **Impact:** Two operational runbooks, graceful degradation verification

### Story 3.6 Implementation Impact
- **Code changes:** ZERO for both ADRs (document current behavior)
- **Documentation additions:**
  - `architecture.md`: Domain authorization model, tenant lifecycle policy
  - `docs/security/jwt-claims.md`: Domain authorization examples
  - `docs/operations/tenant-soft-delete.md`: Soft delete runbook
  - `docs/operations/tenant-hard-delete.md`: Hard delete runbook
- **Test additions:**
  - Integration test: `TenantWithoutDomainClaims_CanAccessAllRegisteredDomains()`
  - Integration test: `ConfigDeleted_CommandsRejectedWithDomainServiceNotFoundException()`
- **Story 8.x backlog:** Automated `TenantLifecycleService`

### References
- Story 3.6: `_bmad-output/implementation-artifacts/3-6-multi-domain-and-multi-tenant-processing.md`
- Pre-mortem: `_bmad-output/implementation-artifacts/3-6-premortem-analysis.md`
- Architecture: `_bmad-output/planning-artifacts/architecture.md`
- Current implementation: `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs`
