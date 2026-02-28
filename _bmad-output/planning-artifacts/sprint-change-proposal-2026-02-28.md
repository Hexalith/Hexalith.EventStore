# Sprint Change Proposal -- Fluent Client SDK API

**Author:** Jerome
**Date:** 2026-02-28
**Status:** Approved
**Change Scope:** Minor-to-Moderate (additive epic, no rework)

## Section 1: Issue Summary

**Problem Statement:** The current `Hexalith.EventStore.Client` SDK requires domain service developers to explicitly configure DAPR infrastructure concerns (service registration via config store, resource naming, component configuration). This friction contradicts the PRD's core DX success criteria -- the "aha moment" should be "zero infrastructure code," but the current registration model still requires infrastructure knowledge.

**Discovery Context:** Identified during a structured brainstorming session (2026-02-28) using Assumption Reversal, SCAMPER, and Role Playing techniques. The session produced 19 ideas and a complete alternative API design validated against 3 developer personas (beginner, intermediate, expert).

**Evidence:** The brainstorming demonstrated that ALL DAPR interactions can be hidden behind conventions derived from domain type names, and that a two-line API skeleton (`AddEventStore()` / `UseEventStore()`) scales from zero-config beginner to multi-tenant enterprise expert without changing shape.

**Core Design Principle Discovered:** "The skeleton never changes." From zero-config beginner to multi-tenant enterprise expert, the same `AddEventStore()` / `UseEventStore()` pattern holds. Complexity always moves to options and domain types, never to the wiring pattern itself.

**Source Document:** `_bmad-output/brainstorming/brainstorming-session-2026-02-28.md`

## Section 2: Impact Analysis

### Epic Impact

| Epic | Status | Impact | Detail |
|------|--------|--------|--------|
| Epic 1: SDK & Contracts | Done | None | `IDomainProcessor`, `EventEnvelope`, identity scheme all stay |
| Epic 2: Command API Gateway | Done | None | REST endpoints, JWT auth, MediatR pipeline unchanged |
| Epic 3: Actor Processing | Done | None | Actor pipeline, state machine, event persistence unchanged |
| Epic 4: Event Distribution | Done | None | Pub/sub, CloudEvents, dead-letter unchanged |
| Epic 5: Security & Access Control | Done | None | Six-layer auth, DAPR policies unchanged |
| Epic 6: Observability | Done | None | OpenTelemetry, structured logging unchanged |
| Epic 7: Sample, Testing, CI/CD | Done | Minor | Sample updated to showcase fluent API as primary DX |
| Epic 8: Foundation & First Impression | In-progress | Minor | README code examples patched |
| Epic 9: Quickstart & Onboarding | In-progress | Minor | Quickstart code examples patched |
| Epic 10: Community Infrastructure | In-progress | None | API-agnostic |
| Epics 11-15 | Backlog | None-Moderate | Not yet written; will naturally reference fluent API |
| **Epic 16 (NEW)** | **Backlog** | **New** | Fluent Client SDK API -- 10 stories |

**Key finding:** Zero completed epics require rework. The fluent API is an additive convenience layer built on proven infrastructure.

### Story Impact

All 10 stories in Epic 16 are new. Three existing stories receive minor code example patches (8-2, 9-1, 7-1) as part of stories 16-7 and 16-9.

### Artifact Conflicts

| Artifact | Impact | Changes Required |
|----------|--------|-----------------|
| PRD | Minor | 2 FR amendments (FR22, FR42), 1 new FR (FR48), NuGet package table update |
| Architecture | Minor | Additive sections: project structure, convention naming patterns, enforcement rule #17, NuGet package table update |
| UX Specs | None | v2 concern, not affected |
| Sprint Status | Moderate | New epic 16 with 10 story entries |
| Sample Application | Minor | Updated in story 16-7 |
| README/Quickstart | Minor | Updated in story 16-9 |

### Technical Impact

- **Code changes:** All within `Hexalith.EventStore.Client` package (additive)
- **Infrastructure:** None -- convention engine generates same DAPR config format
- **Deployment:** None -- no changes to AppHost, DAPR components, or Aspire manifests
- **Testing:** New unit tests (16-8) + integration test (16-10); existing test suite untouched

## Section 3: Recommended Approach

**Selected Path:** Direct Adjustment -- one new additive epic, no rework, no scope reduction.

**Approach:** Option B (Layer) -- The new fluent API (`EventStoreAggregate`, `AddEventStore()`, auto-discovery, convention engine) is built as a higher-level convenience layer on top of the existing `IDomainProcessor` primitives.

**Rationale:**

| Factor | Assessment |
|--------|------------|
| Implementation effort | Medium -- focused Client-package work, ~10 stories |
| Timeline impact | Minimal -- inserts before backlog documentation epics |
| Technical risk | Low -- additive layer over proven infrastructure |
| Team morale/momentum | Positive -- exciting DX improvement, not rework |
| Long-term sustainability | High -- convention-based API reduces config surface |
| Stakeholder alignment | Strong -- directly amplifies PRD success criteria |
| Backward compatibility | Full -- `IDomainProcessor` remains as expert escape hatch |

**Alternatives considered and rejected:**

- **Option A (Replace):** Replace `IDomainProcessor` entirely. Rejected because the existing contract is correct and the server-side pipeline depends on it. Replacement would require reworking Epics 1, 3, and 7 for zero architectural benefit.
- **Rollback:** Nothing to roll back -- completed work is the foundation the fluent API builds on.
- **MVP scope reduction:** Not needed -- MVP is fully delivered by Epics 1-7 (done). The fluent API enhances DX without changing scope.

**Progressive disclosure achieved:**

| Level | API | Uses |
|-------|-----|------|
| Beginner | `EventStoreAggregate` + `AddEventStore()` | Conventions do everything |
| Intermediate | `[EventStoreDomain("custom")]` + `OnConfiguring()` | Selective overrides |
| Expert | `IDomainProcessor` + explicit registration | Full manual control |

## Section 4: Detailed Change Proposals

### 4.1 PRD Changes

**FR22 Amendment:**

OLD:
- FR22: A domain service developer can register their domain service with EventStore by tenant and domain via configuration

NEW:
- FR22: A domain service developer can register their domain service with EventStore by tenant and domain via explicit configuration or automatically via convention-based assembly scanning

---

**FR42 Amendment:**

OLD:
- FR42: A developer can install EventStore client packages via NuGet to build and register domain services

NEW:
- FR42: A developer can install EventStore client packages via NuGet to build and register domain services, with a zero-configuration quickstart via convention-based `AddEventStore()` registration and auto-discovery of domain types

---

**New FR48:**

- FR48: A domain service developer can implement a domain aggregate by inheriting from EventStoreAggregate with typed Apply methods, as a higher-level alternative to implementing IDomainProcessor directly, with convention-based DAPR resource naming derived from the aggregate type name

---

**NuGet Package Table Update:**

OLD:
| `Hexalith.EventStore.Client` | Domain service SDK for registration and integration | Domain service developers |

NEW:
| `Hexalith.EventStore.Client` | Domain service SDK with convention-based fluent API (`AddEventStore`/`UseEventStore`), auto-discovery, and explicit `IDomainProcessor` registration | Domain service developers |

### 4.2 Architecture Changes

**Project Structure Addition** (under `Hexalith.EventStore.Client/`):

```
├── Aggregates/
│   ├── EventStoreAggregate.cs          # High-level base class (wraps IDomainProcessor)
│   └── EventStoreProjection.cs         # Projection base class
├── Attributes/
│   └── EventStoreDomainAttribute.cs    # [EventStoreDomain("name")] override
├── Conventions/
│   └── NamingConventionEngine.cs       # Type name -> kebab-case resource names
├── Discovery/
│   └── AssemblyScanner.cs              # Auto-discovery of aggregate/projection types
├── Configuration/
│   ├── EventStoreOptions.cs            # Global cross-cutting options (new)
│   └── EventStoreDomainOptions.cs      # Per-domain options (new)
├── Registration/
│   ├── HostExtensions.cs               # UseEventStore() activation (new)
│   └── (existing files preserved)
```

---

**Convention Engine Naming Patterns:**

| Input | Convention | Output |
|-------|-----------|--------|
| Type name to domain | Strip "Aggregate"/"Projection" suffix, kebab-case | `OrderAggregate` → `order` |
| Domain to state store | `{domain}-eventstore` | `order-eventstore` |
| Domain to pub/sub topic | `{tenant}.{domain}.events` (per D6) | `acme.order.events` |
| Domain to command endpoint | `{domain}-commands` | `order-commands` |
| Attribute override | `[EventStoreDomain("custom")]` replaces derived name | `[EventStoreDomain("billing")]` → `billing` |

**5-Layer Cascade:**

| Layer | Source | Example |
|-------|--------|---------|
| 1. Convention defaults | Domain type name derivation | `OrderAggregate` → `order` |
| 2. Global code options | `AddEventStore(options => ...)` | Custom serializer for all domains |
| 3. Domain self-config | `OnConfiguring()` override | Per-domain state store component |
| 4. External config | `appsettings.json` / environment variables | Deployment-time overrides |
| 5. Explicit override | `Configure<EventStoreDomainOptions>(...)` | Full manual control |

---

**New Enforcement Rule:**

| # | Rule | Category |
|---|------|----------|
| 17 | Convention-derived resource names use kebab-case; type suffix stripping is automatic (Aggregate, Projection); attribute overrides are validated at startup for non-empty, kebab-case compliance | Naming |

---

**NuGet Package Table Update** (mirrors PRD change):

OLD:
| `Hexalith.EventStore.Client` | Domain service SDK for registration and integration | Domain service developers |

NEW:
| `Hexalith.EventStore.Client` | Domain service SDK with convention-based fluent API (`AddEventStore`/`UseEventStore`), auto-discovery, and explicit `IDomainProcessor` registration | Domain service developers |

### 4.3 New Epic 16: Fluent Client SDK API (Phase 1 Foundation)

**Epic Statement:** A domain service developer can build event-sourced aggregates using a convention-based fluent API (AddEventStore/UseEventStore) with auto-discovery, eliminating explicit DAPR configuration for the common case while preserving IDomainProcessor as an expert escape hatch.

**FRs covered:** FR22 (amended), FR42 (amended), FR48 (new)

**Dependency:** Epics 1-7 (done). Must complete before Epics 12-15 (documentation deep dives).

**Stories:**

| Story | Title | Key Deliverable |
|-------|-------|----------------|
| 16-1 | EventStoreAggregate Base Class | Base class wrapping IDomainProcessor + EventStoreProjection<T> |
| 16-2 | EventStoreDomain Attribute and Naming Convention Engine | Type name -> kebab-case resource names + attribute override |
| 16-3 | Assembly Scanner and Auto-Discovery | Automatic finding of aggregate/projection types |
| 16-4 | AddEventStore Extension Method with Global Options | One-line DI registration with EventStoreOptions |
| 16-5 | UseEventStore Extension Method with Activation | Runtime activation of DAPR subscriptions/middleware |
| 16-6 | Five-Layer Cascading Configuration | EventStoreOptions, EventStoreDomainOptions, cascade resolution |
| 16-7 | Updated Sample with Fluent API | Counter sample using EventStoreAggregate as primary path |
| 16-8 | Unit Tests for Convention Engine and Discovery | Comprehensive test coverage for new layer |
| 16-9 | README and Quickstart Code Example Updates | Documentation patches showing fluent API |
| 16-10 | Integration Test for Fluent API Registration Path | E2E validation through full command lifecycle |

### 4.4 Sprint Status Update

New entries added to `sprint-status.yaml` after Epic 10, before Epic 11, with dependency comment noting Epic 16 must complete before Epics 12-15.

## Section 5: Implementation Handoff

### Change Scope Classification: Minor-to-Moderate

- **Minor:** No fundamental replan, no rework of completed code, no MVP scope change
- **Moderate:** A full new epic (10 stories) needs implementation

### Handoff

| Role | Responsibility |
|------|---------------|
| PM/Architect (this workflow) | Sprint Change Proposal produced, artifact edits approved |
| SM (sprint planning) | Update sprint-status.yaml, create stories via create-story workflow |
| Dev (Jerome) | Implement Epic 16 stories |

### Success Criteria

1. `builder.AddEventStore()` discovers and registers all aggregate types from the calling assembly
2. `app.UseEventStore()` activates DAPR subscriptions using convention-derived topic names
3. `EventStoreAggregate` with `Apply` methods works end-to-end through the actor pipeline
4. Convention-derived names match D6 topic naming pattern (`{tenant}.{domain}.events`)
5. All 5 configuration layers cascade correctly
6. Existing `IDomainProcessor`-based tests continue to pass (zero regression)
7. Updated Counter sample runs successfully with `aspire run`

### Implementation Sequence

```
Stories 16-1, 16-2     (base class + naming)      -- foundation, no dependencies
    ↓
Story 16-3             (assembly scanner)          -- depends on 16-1, 16-2
    ↓
Stories 16-4, 16-5     (AddEventStore/UseEventStore) -- depends on 16-3
    ↓
Story 16-6             (cascading config)          -- depends on 16-4
    ↓
Stories 16-7, 16-8     (sample + unit tests)       -- depends on 16-1 through 16-6
    ↓
Story 16-9             (docs patches)              -- depends on 16-7
    ↓
Story 16-10            (integration test)          -- depends on all above
```

### Deferred to Future Epics

The brainstorming session identified Phases 2 and 3 beyond the Phase 1 Foundation:

| Phase | Scope | Deferral Rationale |
|-------|-------|--------------------|
| Phase 2: Expert Override Pillars | Per-domain serializers, multi-tenancy patterns, custom DAPR components | Not needed for MVP DX improvement |
| Phase 3: Selective Activation | Per-domain/per-capability activation, service archetype presets | Advanced scenario, post-MVP |

These can be added as Epics 17-18 when Phase 1 is validated and the documentation initiative completes.
