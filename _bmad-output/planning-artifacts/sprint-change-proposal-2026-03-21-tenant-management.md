# Sprint Change Proposal — Integrate Hexalith.Tenants for Tenant Management

**Date:** 2026-03-21
**Author:** Jerome (via Correct Course workflow)
**Change Scope:** Moderate
**Status:** Approved (2026-03-21)

---

## Section 1: Issue Summary

**Problem Statement:** EventStore's planning artifacts (PRD, Epics, Architecture, UX Design) assume tenant management (FR77) will be built internally within the EventStore admin tooling (Epic 16 DBA Operations, Epic 17 CLI, Epic 18 MCP). However, a dedicated **Hexalith.Tenants** project already exists as a full DDD domain service built on Hexalith.EventStore itself, providing:

- 12+ commands (CreateTenant, DisableTenant, EnableTenant, AddUserToTenant, RemoveUserFromTenant, ChangeUserRole, SetTenantConfiguration, BootstrapGlobalAdmin, etc.)
- 10+ events with rejection event variants
- 2 aggregates (TenantAggregate, GlobalAdministratorsAggregate)
- 3 projections (TenantProjection, TenantIndexProjection, GlobalAdministratorProjection)
- Full validator set (FluentValidation)
- Mirrors EventStore's own project structure (Contracts, Client, Server, CommandApi, Aspire, AppHost, Testing)

**Discovery context:** Strategic decision to leverage existing bounded context rather than duplicating tenant domain logic inside EventStore.

**Evidence:** Hexalith.Tenants repository (https://github.com/Hexalith/Hexalith.Tenants) includes Hexalith.EventStore as a git submodule and is already a functional event-sourced service.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Stories Affected | Impact |
|------|-----------------|--------|
| Epic 14 (Admin API Foundation) | 14-4 (Aspire integration) | Aspire AppHost must orchestrate Hexalith.Tenants as a peer resource with its own DAPR sidecar. Admin API surface excludes tenant operations — they live in Hexalith.Tenants CommandApi. |
| Epic 16 (Admin Web UI — DBA Operations) | 16-5 (tenant management) | **Scope change:** UI consumes Hexalith.Tenants.Client packages. Sends commands to Tenants CommandApi, reads TenantReadModel/TenantIndexReadModel projections for display. EventStore admin is a consumer, not owner. |
| Epic 17 (Admin CLI) | 17-5 (tenant subcommand) | **Scope change:** CLI tenant subcommands call Hexalith.Tenants CommandApi directly via its client packages. No EventStore Admin API proxy needed. |
| Epic 18 (Admin MCP Server) | 18-5 (tenant context) | **Scope change:** Tenant context tools query Hexalith.Tenants for tenant metadata. Investigation session state remains in EventStore MCP server — only tenant data retrieval is externalized. |
| Epics 1-13 (core pipeline) | None | No impact. Multi-tenancy at contract level (`tenant:domain:aggregate-id`, JWT claims, triple-layer isolation) is unchanged. EventStore enforces tenant isolation; Hexalith.Tenants manages tenant lifecycle. |
| Epic 15 (Admin Web UI Core) | Minor | Tenant list page in navigation (`system > tenant > domain > aggregate > event`) fetches from Hexalith.Tenants projections. |
| Epics 19-20 | None | No impact. |

### Artifact Conflicts

| Artifact | Section | Change Needed |
|----------|---------|---------------|
| PRD | FR77 traceability table | Update from `FR77 → Epic 16` to `FR77 → Epic 16 (via Hexalith.Tenants)` with clarification that lifecycle, users, configuration are managed by peer service |
| PRD | Success Criteria (self-service resolution) | Clarify tenant suspension is via Hexalith.Tenants UI integration |
| Architecture | Package/component tables | Add "Peer Services" table documenting Hexalith.Tenants as co-orchestrated Aspire resource |
| Architecture | Cross-cutting concern #11 (Admin Data Access) | Add clause: tenant operations delegated to Hexalith.Tenants via Client SDK |
| UX Design | Tenant Scope Selector | Tenant list fetched from Hexalith.Tenants projection API (TenantIndexReadModel) |
| UX Design | Form & Configuration Patterns | Tenant settings forms route through Hexalith.Tenants API |

### Technical Impact

- **NuGet dependency:** Stories 16-5, 17-5, 18-5 will depend on `Hexalith.Tenants.Client` and `Hexalith.Tenants.Contracts` NuGet packages
- **Aspire topology:** AppHost adds Hexalith.Tenants as a peer resource with its own DAPR sidecar, state store, and pub/sub topics
- **Integration testing (Tier 3):** Full admin test scenarios will require Hexalith.Tenants running in the Aspire topology
- **No code impact on existing implementation:** All affected stories are in backlog status

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — modify existing story scopes within the current epic structure.

**Rationale:**
1. **Zero rework risk** — all affected stories (16-5, 17-5, 18-5) are in backlog, no code has been written
2. **Scope narrows** — consuming an existing service is less work than building tenant domain logic from scratch
3. **Architectural integrity** — tenant management is a separate bounded context; building it inside EventStore would violate DDD principles
4. **Proven implementation** — Hexalith.Tenants already has aggregates, commands, events, projections, and validators using the same patterns as EventStore

**Effort estimate:** Low — story scope adjustments only; net reduction in implementation effort
**Risk level:** Low — dependency on Hexalith.Tenants NuGet packages being published before stories 16-5/17-5/18-5 can be implemented
**Timeline impact:** None — no change to epic ordering or sprint plan

---

## Section 4: Detailed Change Proposals

### 4.1 PRD Changes

#### FR77 Traceability Table

**OLD:**
```
| FR77 | 16 | Tenant management |
```

**NEW:**
```
| FR77 | 16 (via Hexalith.Tenants) | Tenant management — lifecycle, users, configuration managed by Hexalith.Tenants peer service; EventStore admin UI/CLI/MCP consume its API |
```

#### Success Criteria — Self-Service Resolution

**OLD:**
```
Self-service resolution: Common operational tasks (tenant suspension, domain version rollback,
dead-letter inspection/replay) completable entirely through the UI without developer involvement
```

**NEW:**
```
Self-service resolution: Common operational tasks (tenant suspension via Hexalith.Tenants UI
integration, domain version rollback, dead-letter inspection/replay) completable entirely
through the UI without developer involvement
```

### 4.2 Architecture Changes

#### Add Peer Services Table (after Non-packaged admin components table)

```markdown
**Peer Services (external bounded contexts, co-orchestrated via Aspire):**

| Service              | Purpose                                                             | Integration                                |
| -------------------- | ------------------------------------------------------------------- | ------------------------------------------ |
| `Hexalith.Tenants`   | Tenant lifecycle management (create, enable/disable, users, roles,  | Aspire peer resource with own DAPR sidecar |
|                      | configuration). Owns FR77 implementation.                           | Admin UI/CLI/MCP consume its Client SDK    |

Hexalith.Tenants is itself an event-sourced service built on Hexalith.EventStore. In the Aspire
topology, it runs as an independent service with its own DAPR sidecar, state store, and pub/sub
topics. EventStore admin tools (Web UI, CLI, MCP) consume Hexalith.Tenants.Client packages for
tenant operations — they never bypass the Tenants command pipeline.
```

#### Cross-Cutting Concern #11 (Admin Data Access)

**OLD:**
```
All admin reads go through DAPR state store using identical key derivation from Contracts
(`AggregateIdentity`). All admin writes delegated to CommandApi via DAPR service invocation
— never bypass the command pipeline
```

**NEW:**
```
All EventStore admin reads go through DAPR state store using identical key derivation from
Contracts (`AggregateIdentity`). All EventStore admin writes delegated to CommandApi via DAPR
service invocation — never bypass the command pipeline. Tenant operations (FR77) are delegated
to Hexalith.Tenants via its Client SDK — EventStore does not own tenant state.
```

### 4.3 UX Design Changes

#### Tenant Scope Selector

**OLD:**
```
- Options: "All Tenants" (default) + list of registered tenants
```

**NEW:**
```
- Options: "All Tenants" (default) + list of registered tenants
- Tenant list fetched from Hexalith.Tenants projection API (TenantIndexReadModel)
```

#### Form & Configuration Patterns

**OLD:**
```
Configuration views (tenant settings, domain service registration, system settings) follow a
consistent form layout:
```

**NEW:**
```
Configuration views (tenant settings via Hexalith.Tenants API, domain service registration,
system settings) follow a consistent form layout:
```

### 4.4 Story Scope Changes

#### Story 16-5: tenant-management-quotas-onboarding-comparison

**OLD scope:** Build tenant management UI from scratch within EventStore admin — CRUD, quotas, onboarding, comparison. Implies building a tenant domain model inside EventStore.

**NEW scope:** Build tenant management admin UI that consumes Hexalith.Tenants.Client NuGet packages. The UI sends commands (CreateTenant, DisableTenant, AddUserToTenant, etc.) to the Hexalith.Tenants CommandApi peer service and reads projections (TenantReadModel, TenantIndexReadModel, GlobalAdministratorReadModel) for display. EventStore admin is a consumer, not the owner of tenant domain logic.

#### Story 17-5: tenant-subcommand-list-quotas-verify

**OLD scope:** Build CLI tenant subcommands that call EventStore Admin API for tenant operations (list, quotas, verify).

**NEW scope:** CLI tenant subcommands call Hexalith.Tenants CommandApi directly via its client packages. Commands like `eventstore-admin tenant list`, `tenant disable`, `tenant add-user` delegate to Hexalith.Tenants rather than to an EventStore-internal tenant API. The Admin API (Epic 14) does NOT need to proxy tenant operations.

#### Story 18-5: tenant-context-and-investigation-session-state

**OLD scope:** MCP server manages tenant context internally, storing investigation session state with tenant awareness built into EventStore's admin backend.

**NEW scope:** MCP server's tenant context tools query Hexalith.Tenants for tenant metadata (status, users, configuration) and use that to scope investigation sessions. Tenant-aware MCP tools (e.g., "list tenants", "show tenant config", "switch tenant context") delegate to Hexalith.Tenants API. Investigation session state remains in EventStore MCP server — only tenant data retrieval is externalized.

#### Story 14-4: admin-server-aspire-resource-integration

**Additional scope:** Aspire AppHost must orchestrate Hexalith.Tenants as a peer resource with its own DAPR sidecar alongside the EventStore Admin.Server.

---

## Section 5: Implementation Handoff

**Change scope classification:** Moderate — backlog reorganization needed (story scope updates + artifact edits).

### Handoff Responsibilities

| Role | Responsibility |
|------|---------------|
| **SM/PO (Jerome)** | Apply approved artifact edits to PRD, Architecture, UX Design, and Epics documents |
| **Architect** | Validate Aspire topology design for peer service co-orchestration when Epic 14 begins |
| **Dev team** | Consume Hexalith.Tenants.Client packages in stories 16-5, 17-5, 18-5 when they enter development |

### Prerequisites

1. Hexalith.Tenants NuGet packages (`Hexalith.Tenants.Contracts`, `Hexalith.Tenants.Client`) must be published before stories 16-5, 17-5, 18-5 can be implemented
2. Hexalith.Tenants Aspire integration package must be available for story 14-4

### Success Criteria

- [ ] PRD FR77 traceability updated
- [ ] Architecture doc includes Peer Services table and updated cross-cutting concern #11
- [ ] UX Design Tenant Scope Selector references Hexalith.Tenants projection API
- [ ] Stories 16-5, 17-5, 18-5 scope descriptions updated in epics document
- [ ] Story 14-4 scope expanded to include Hexalith.Tenants Aspire orchestration
- [ ] Sprint status unchanged (all affected stories remain in backlog)
