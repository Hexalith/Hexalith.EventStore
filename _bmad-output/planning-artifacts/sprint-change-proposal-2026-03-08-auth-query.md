# Sprint Change Proposal -- Actor-Based Authorization & Query API

**Author:** Jerome (with AI assistance)
**Date:** 2026-03-08
**Status:** Approved
**Change Scope:** Moderate (new epic, architectural refactor of auth pipeline, new endpoints)
**Amended:** 2026-03-08 (party mode review -- 6 amendments applied)

---

## Section 1: Issue Summary

**Problem Statement:** The current authorization model (Epics 2 and 5) encodes all permissions as JWT claims (`eventstore:tenant`, `eventstore:domain`, `eventstore:permission`). This creates three operational issues:

1. **Token bloat** -- Every tenant access, domain permission, and command type authorization must be encoded as JWT claims, producing large tokens on every request between client and server
2. **Tight coupling to identity provider** -- Permission changes require token re-issuance; the identity provider must understand EventStore's authorization model
3. **No application-managed authorization** -- The application cannot manage permissions dynamically at runtime; all authorization logic is baked into the identity provider's token issuance

**Discovery Context:** Identified during/after Epic 5 (Multi-Tenant Security & Access Control) implementation. The six-layer auth model works correctly but assumes the identity provider is the single source of truth for all authorization decisions. Real-world multi-tenant deployments need the application itself to manage tenant access and RBAC dynamically.

**Evidence:**
- `AuthorizationBehavior.cs` -- requires `eventstore:tenant`, `eventstore:domain`, `eventstore:permission` claims from JWT
- `EventStoreClaimsTransformation.cs` -- must parse complex JSON array claims (`tenants`, `domains`, `permissions`) from every token
- `CommandsController.cs` lines 48-61 -- duplicated inline tenant claim check before MediatR pipeline
- PRD "Future Considerations" already lists "External Authorization Engine" and "Advanced Authorization" as planned evolution

**Proposed Solution:** Introduce optional DAPR actor-based authorization validators. When configured, the API delegates tenant access and RBAC checks to named DAPR actors managed by the application, keeping JWT tokens lean (identity only). The claims-based model remains as the default fallback.

Additionally, add a **query endpoint** (`POST /api/v1/queries`) that routes to projection actors by tenant/domain, and **pre-flight validation endpoints** (`POST /api/v1/commands/validate`, `POST /api/v1/queries/validate`) for client applications to check authorization before submitting.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Status | Impact | Detail |
|------|--------|--------|--------|
| Epic 1: SDK & Contracts | Done | Minor | New query contracts added to `Contracts` package |
| Epic 2: Command API Gateway | Done | Moderate | `AuthorizationBehavior` refactored to use validator abstractions; controller-level duplication removed; new query + validation controllers added |
| Epic 3: Actor Processing | Done | None | `AggregateActor` pipeline unchanged |
| Epic 4: Event Distribution | Done | None | Pub/sub unchanged |
| Epic 5: Security & Access Control | Done | Minor | Six-layer model enhanced (layers 3-4 become pluggable); actor-based path adds layer variant |
| Epic 6: Observability | Done | None | OpenTelemetry patterns apply to new endpoints automatically |
| Epic 7: Sample, Testing, CI/CD | Done | Minor | Sample may demonstrate actor-based auth as optional config |
| Epic 8-14 | Done | None | Documentation-focused, no code impact |
| Epic 15 | In-progress | Minor | 15-1 (config reference) will need update for new settings |
| Epic 16: Fluent SDK | Done | None | Client-side convenience layer, unaffected |
| **Epic 17 (NEW)** | **Backlog** | **New** | Actor-Based Authorization & Query API -- 9 stories |

### Story Impact

**Modified stories:** None directly. Completed code in Epic 2 is refactored (not broken) by extracting inline logic into abstractions. All existing behavior is preserved as the default path.

**New stories:** 9 stories in Epic 17 (see Section 4).

### Artifact Conflicts

| Artifact | Impact | Changes Required |
|----------|--------|-----------------|
| PRD | Moderate | Amend FR31, FR32; add FR49-FR52 (query endpoint, validation endpoints, actor-based auth) |
| Architecture | Moderate | Amend six-layer auth model (layers 3-4 pluggable); add query controller, validation endpoints, new options classes, new actor proxy types |
| UX Specs | None | No UI impact |
| Sprint Status | Minor | New Epic 17 with 9 stories |
| OpenAPI/Swagger | Minor | 3 new endpoints auto-documented |

### Technical Impact

| Area | Impact |
|------|--------|
| Code changes | `CommandApi` (refactor auth, add controllers), `Contracts` (query types), `Server` (query router) |
| New abstractions | `ITenantValidator`, `IRbacValidator` (with `messageCategory` for read/write discrimination) interfaces + 2 implementations each (claims, actor) |
| New configuration | `EventStoreAuthorizationOptions` with optional actor names |
| New endpoints | `POST /api/v1/queries`, `POST /api/v1/commands/validate`, `POST /api/v1/queries/validate` |
| New actor contracts | `ITenantValidatorActor`, `IRbacValidatorActor` (interfaces only -- implementation is application's responsibility) |
| Infrastructure | None -- uses existing DAPR actor infrastructure |
| Deployment | None -- backward compatible, opt-in |

---

## Section 3: Recommended Approach

**Selected Path:** Direct Adjustment -- one new additive epic with targeted refactoring of the auth pipeline.

**Rationale:**

| Factor | Assessment |
|--------|------------|
| Implementation effort | Medium -- ~9 stories, focused on CommandApi + Contracts |
| Timeline impact | Minimal -- no rework of completed epics |
| Technical risk | Medium -- auth pipeline refactoring is sensitive but the change is mechanical (extract to interface) |
| Backward compatibility | Full -- claims-based auth remains the default; actor-based is opt-in via settings |
| Long-term sustainability | High -- aligns with PRD "Future Considerations" for external authorization engines |
| Token size reduction | Significant -- JWT carries identity only; tenant/RBAC managed by application actors |

**Alternatives considered:**

- **OpenFGA/OPA integration:** More standard but adds external infrastructure dependency. DAPR actors are already native to the system. Could be added later as another `ITenantValidator`/`IRbacValidator` implementation.
- **Keep claims-only:** Works but doesn't address token bloat or application-managed authorization. Already noted as a future evolution in the PRD.
- **Middleware-based validation:** Would work but violates the existing MediatR pipeline pattern. Using behaviors keeps authorization in the same architectural layer.

---

## Section 4: Detailed Change Proposals

### 4.1 PRD Changes

**FR31 Amendment:**

```
OLD:
- FR31: The system can authorize command submissions based on JWT claims
  for tenant, domain, and command type

NEW:
- FR31: The system can authorize command and query submissions based on
  JWT claims (default) or by delegating to application-managed DAPR
  actors for tenant validation and RBAC checks (opt-in via configuration)
```

**FR32 Amendment:**

```
OLD:
- FR32: The system can reject unauthorized commands at the API gateway
  before they enter the processing pipeline

NEW:
- FR32: The system can reject unauthorized commands and queries at the
  API gateway before they enter the processing pipeline, using either
  claims-based or actor-delegated authorization
```

**New FR49:**
- FR49: An API consumer can submit a query to the EventStore via a REST endpoint with tenant, domain, aggregate ID, query type, and payload, which routes to the appropriate projection actor

**New FR50:**
- FR50: An API consumer can check whether a specific command type is authorized for a given tenant and user via a pre-flight validation endpoint, without submitting the command

**New FR51:**
- FR51: An API consumer can check whether a specific query type is authorized for a given tenant and user via a pre-flight validation endpoint, without submitting the query

**New FR52:**
- FR52: A deployment operator can configure optional DAPR actor names for tenant validation and RBAC validation in application settings, enabling the application to manage authorization dynamically at runtime instead of relying on JWT claims

### 4.2 Architecture Changes

**Six-Layer Auth Model Update:**

```
OLD (layers 3-4):
  Layer 3: Endpoint Authorization -- [Authorize] attribute with claims
  Layer 4: MediatR AuthorizationBehavior -- inline tenant x domain x
           command type claim checks

NEW (layers 3-4):
  Layer 3: Endpoint Authorization -- [Authorize] attribute (identity only)
  Layer 4: MediatR AuthorizationBehavior -- delegates to ITenantValidator
           and IRbacValidator (claims-based OR actor-based per config)
```

**New Configuration Section:**

```json
{
  "EventStore": {
    "Authorization": {
      "TenantValidatorActorName": null,
      "RbacValidatorActorName": null
    }
  }
}
```

- `null` (default) = claims-based authorization (current behavior)
- Non-null = DAPR actor-based authorization (application-managed)

**New API Endpoints:**

| Method | Path | Purpose | Auth |
|--------|------|---------|------|
| POST | `/api/v1/queries` | Submit a query for processing | JWT |
| POST | `/api/v1/commands/validate` | Pre-flight command authorization check | JWT |
| POST | `/api/v1/queries/validate` | Pre-flight query authorization check | JWT |

**New Project Structure (additions to CommandApi):**

```
CommandApi/
├── Controllers/
│   ├── QueriesController.cs              # POST /api/v1/queries (NEW)
│   ├── CommandValidationController.cs    # POST /api/v1/commands/validate (NEW)
│   └── QueryValidationController.cs     # POST /api/v1/queries/validate (NEW)
├── Configuration/
│   └── EventStoreAuthorizationOptions.cs # TenantValidatorActorName, RbacValidatorActorName (NEW)
├── Authorization/
│   ├── ITenantValidator.cs               # Interface (NEW)
│   ├── IRbacValidator.cs                 # Interface (NEW)
│   ├── ClaimsTenantValidator.cs          # Extracted from current code (NEW)
│   ├── ClaimsRbacValidator.cs            # Extracted from current code (NEW)
│   ├── ActorTenantValidator.cs           # DAPR actor proxy (NEW)
│   └── ActorRbacValidator.cs             # DAPR actor proxy (NEW)
```

**New Contracts (in Hexalith.EventStore.Contracts):**

```
Contracts/
├── Queries/
│   ├── SubmitQueryRequest.cs             # Tenant, Domain, AggregateId, QueryType, Payload
│   └── SubmitQueryResponse.cs            # Result payload
├── Validation/
│   ├── ValidateCommandRequest.cs         # Tenant, Domain, CommandType, AggregateId? (optional, for fine-grained ACL)
│   ├── ValidateQueryRequest.cs           # Tenant, Domain, QueryType, AggregateId? (optional, for fine-grained ACL)
│   └── ValidationResult.cs              # IsAuthorized, Reason
├── Authorization/
│   ├── ITenantValidatorActor.cs          # Actor interface for app-managed tenant validation
│   └── IRbacValidatorActor.cs            # Actor interface for app-managed RBAC validation
```

**Query Processing Flow:**

```
POST /api/v1/queries
  → JWT auth (identity only when actor-based)
  → MediatR Pipeline:
    1. LoggingBehavior
    2. ValidationBehavior (SubmitQueryRequest)
    3. AuthorizationBehavior (ITenantValidator + IRbacValidator)
    4. QueryHandler → QueryRouter → ProjectionActor (by tenant/domain)
  → SubmitQueryResponse (projection result)
```

**Validation Endpoint Flow:**

```
POST /api/v1/commands/validate  (or /queries/validate)
  → JWT auth
  → Extract userId from token
  → ITenantValidator.ValidateAsync(userId, tenantId)
  → IRbacValidator.ValidateAsync(userId, tenantId, messageType, messageCategory)
  → 200 OK { isAuthorized: true } or 403 { isAuthorized: false, reason: "..." }
  → 503 Service Unavailable if configured validator actor is unreachable
```

### 4.3 New Epic 17: Actor-Based Authorization & Query API

**Epic Statement:** The EventStore supports application-managed authorization via optional DAPR actor validators and a query API with projection actor routing, reducing JWT token size and enabling dynamic runtime authorization management.

**FRs covered:** FR31 (amended), FR32 (amended), FR49, FR50, FR51, FR52

**Dependency:** Epics 1-3, 5 (done). Should complete before Epic 15 remaining stories.

**Stories:**

| Story | Title | Key Deliverable |
|-------|-------|----------------|
| 17-1 | Authorization Options and Validator Abstractions | `EventStoreAuthorizationOptions`, `ITenantValidator`, `IRbacValidator` (with `messageCategory` param for read/write discrimination), claims-based implementations extracted from current code + characterization unit tests for existing auth behavior |
| 17-2 | Actor-Based Validator Implementations | `ActorTenantValidator`, `ActorRbacValidator` using DAPR actor proxy, conditional DI registration, `AuthorizationServiceUnavailableException` with 503 + `Retry-After` failure mode, `TestTenantValidatorActor`/`TestRbacValidatorActor` in Testing package + unit tests |
| 17-3 | Refactor AuthorizationBehavior | Use `ITenantValidator`/`IRbacValidator`; remove controller-level tenant duplication + regression tests proving zero behavioral change |
| 17-4 | Query Contracts | `SubmitQueryRequest`, `SubmitQueryResponse`, `ValidateCommandRequest` (with optional `AggregateId`), `ValidateQueryRequest` (with optional `AggregateId`), validators in Contracts package + unit tests |
| 17-5 | Queries Controller and Query Router | `POST /api/v1/queries`, MediatR pipeline, `IQueryRouter` routing to projection actor, 404 error contract when projection actor not found + unit tests |
| 17-6 | Projection Actor Contract | `IProjectionActor` with `QueryAsync(QueryEnvelope) → QueryResult` (opaque `JsonElement Payload`), actor proxy in Server + unit tests |
| 17-7 | Command Validation Endpoint | `POST /api/v1/commands/validate` pre-flight authorization check + unit tests |
| 17-8 | Query Validation Endpoint | `POST /api/v1/queries/validate` pre-flight authorization check + unit tests |
| 17-9 | Integration and E2E Tests | E2E query endpoint through full pipeline, E2E validation endpoints, actor-based auth flow with test actors, 503 failure mode verification |

**Amendment Notes (Party Mode Review):**

| # | Amendment | Source |
|---|-----------|--------|
| A1 | `IRbacValidator.ValidateAsync` includes `messageCategory` parameter (`"command"` / `"query"`) for read/write discrimination | Winston (Architect) |
| A2 | `ValidateCommandRequest` and `ValidateQueryRequest` include optional `AggregateId` for future fine-grained ACL support | Mary (Analyst) |
| A3 | Actor-based validators fail closed with **503 Service Unavailable** (not 403) + `Retry-After` header when configured actor is unreachable; new `AuthorizationServiceUnavailableException` + exception handler | Winston (Architect), Amelia (Dev) |
| A4 | `IProjectionActor` method signature: `QueryAsync(QueryEnvelope) → QueryResult` with opaque `JsonElement Payload` | Winston (Architect) |
| A5 | Unit tests embedded in Stories 17-1 through 17-8; Story 17-9 renamed to "Integration and E2E Tests" only; characterization tests in 17-1 run before refactoring | Murat (Test Architect) |
| A6 | Query endpoint (17-5) includes 404 error contract for missing projection actors | Mary (Analyst) |

### 4.4 Implementation Sequence

```
Story 17-1  (abstractions + claims extraction)     -- foundation, no dependencies
    ↓
Story 17-2  (actor-based implementations)          -- depends on 17-1
    ↓
Story 17-3  (refactor AuthorizationBehavior)       -- depends on 17-1, 17-2
    ↓
Stories 17-4, 17-6  (query contracts + projection)  -- depends on 17-1 (parallel OK)
    ↓
Story 17-5  (queries controller)                   -- depends on 17-3, 17-4, 17-6
    ↓
Stories 17-7, 17-8  (validation endpoints)          -- depends on 17-3 (parallel OK)
    ↓
Story 17-9  (tests)                                -- depends on all above
```

### 4.5 Sprint Status Update

New entries for `sprint-status.yaml`:

```yaml
  # Epic 17: Actor-Based Authorization & Query API
  # DEPENDENCY: Epics 1-3, 5 (done). Complete before Epic 15 remaining stories.
  # SOURCE: sprint-change-proposal-2026-03-08-auth-query.md
  epic-17: backlog
  17-1-authorization-options-and-validator-abstractions: backlog
  17-2-actor-based-validator-implementations: backlog
  17-3-refactor-authorization-behavior: backlog
  17-4-query-contracts: backlog
  17-5-queries-controller-and-query-router: backlog
  17-6-projection-actor-contract: backlog
  17-7-command-validation-endpoint: backlog
  17-8-query-validation-endpoint: backlog
  17-9-unit-and-integration-tests: backlog
  epic-17-retrospective: optional
```

---

## Section 5: Implementation Handoff

### Change Scope Classification: Moderate

- **Moderate:** Auth pipeline refactoring (sensitive but mechanical), new endpoint family (queries), new validation endpoints, new configuration
- **Not Major:** No fundamental replan. Claims-based auth stays as default. Existing tests pass without modification.

### Handoff

| Role | Responsibility |
|------|---------------|
| PM/Architect (this workflow) | Sprint Change Proposal produced, artifact edits approved |
| SM (sprint planning) | Update sprint-status.yaml, create stories via create-story workflow |
| Dev (Jerome) | Implement Epic 17 stories |

### Success Criteria

1. Claims-based authorization continues to work identically when no actor names are configured (zero regression)
2. When `TenantValidatorActorName` is configured, tenant access checks delegate to the named DAPR actor
3. When `RbacValidatorActorName` is configured, command/query type checks delegate to the named DAPR actor, with `messageCategory` distinguishing commands from queries
4. `POST /api/v1/queries` routes to projection actors by tenant/domain and returns `QueryResult` with opaque `JsonElement Payload`
5. `POST /api/v1/queries` returns 404 when the requested projection actor does not exist
6. `POST /api/v1/commands/validate` and `POST /api/v1/queries/validate` return authorization status without processing (with optional `AggregateId` for fine-grained checks)
7. When a configured validator actor is unreachable, the system returns **503 Service Unavailable** with `Retry-After` header (fail closed, not 403)
8. JWT tokens need only carry user identity when actor-based authorization is enabled
9. All existing Tier 1 and Tier 2 tests continue to pass
10. New endpoints have OpenAPI documentation via Swagger
11. Each story (17-1 through 17-8) includes its own unit tests; Story 17-9 covers integration and E2E only

### Backward Compatibility

| Aspect | Guarantee |
|--------|-----------|
| Default behavior | Unchanged -- claims-based auth when settings are null |
| Existing endpoints | `POST /api/v1/commands`, status, replay all unchanged |
| JWT claims | Still supported and processed when present |
| DAPR policies | D4 access control unchanged |
| NuGet packages | No breaking changes in any published package |

### Deferred to Future

| Consideration | Rationale |
|---------------|-----------|
| OpenFGA/OPA integration | Can be added as additional `ITenantValidator`/`IRbacValidator` implementations |
| Per-command ACLs | State-dependent rules beyond simple type-based RBAC |
| Query caching | Projection actor caching strategy (separate concern) |
| GraphQL query endpoint | Alternative query interface (separate epic if needed) |
