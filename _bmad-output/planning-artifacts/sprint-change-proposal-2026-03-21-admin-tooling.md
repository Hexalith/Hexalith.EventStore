# Sprint Change Proposal: Administration Tooling Layer

**Date:** 2026-03-21
**Triggered by:** Strategic brainstorming session — 258 features identified for event store administration
**Change Scope:** Major — new v2 feature set across 7 new epics
**Recommended Approach:** Direct Adjustment (add to v2 roadmap)

---

## 1. Issue Summary

A brainstorming session on 2026-03-21 identified the need for comprehensive administration tooling for Hexalith.EventStore, serving three personas (Developer, DBA, AI Agent) across three interfaces (Blazor Web UI, CLI, MCP Server). The session produced 258 features organized into 16 themes with a 6-tier prioritized implementation roadmap.

The PRD already anticipated a v2 "Blazor operational dashboard" (Journey 3, UX-DR34-40). This change proposal expands that scope to a full Administration Tooling layer with:
- **Admin API Foundation** — shared service layer backed by DAPR, consumed by all three interfaces
- **Blazor Web UI** — domain-aware dashboard with deep links to external observability tools (not replicating Zipkin/Grafana/Aspire UIs)
- **CLI (`eventstore-admin`)** — scriptable .NET global tool for automation, CI/CD, and DBA operations
- **MCP Server** — programmatic access for AI agents with autonomous diagnosis and approval-gated remediation

**Evidence:** Brainstorming session document (`_bmad-output/brainstorming/brainstorming-session-2026-03-21-001.md`), architecture analysis confirming design fits existing codebase patterns.

---

## 2. Impact Analysis

### Epic Impact
- **Existing Epics 1-13:** No changes required. Admin tooling is purely additive.
- **New Epics 14-20:** 7 new epics added to the v2 roadmap.

### Artifact Conflicts
- **PRD:** 9 additions approved (no modifications to existing content):
  - Executive Summary: v2 scope expanded, DBA and AI Agent personas added
  - Success Criteria: Admin Tooling section added
  - Journeys: Journey 8 (DBA) and Journey 9 (MCP Agent) added
  - Post-MVP Roadmap: Phase 2 table expanded with admin components
  - Functional Requirements: FR68-FR82 added
  - Non-Functional Requirements: NFR40-NFR46 added
- **Architecture:** 4 additions approved:
  - NuGet package table: Admin.Abstractions + 3 non-packaged executables
  - ADR-P4: Three-interface architecture decision
  - ADR-P5: Observability deep-link strategy
  - DAPR building block table: Service Invocation added
  - Cross-cutting concerns: 3 new rows (admin data access, admin auth, observability integration)
- **UX Design:** 19 new UX-DRs (UX-DR41-59) across Web UI, CLI, and MCP
- **CI/CD:** Package count 6→7, new project builds, CLI tool publish
- **Aspire AppHost:** New `admin-server` resource with DAPR sidecar

### Technical Impact
- 4 new projects: `Admin.Abstractions`, `Admin.Server`, `Admin.Cli`, `Admin.Mcp`
- 3 new test projects
- `InternalsVisibleTo` added to `Server.csproj` for admin state store key access
- New NuGet dependencies: `System.CommandLine`, MCP SDK
- DAPR access control policy update: `admin-server` → `commandapi` invocation

---

## 3. Recommended Approach

**Direct Adjustment** — Add 7 new epics to the v2 roadmap.

**Rationale:**
- Zero risk to existing Epics 1-13 (v1 pipeline unaffected)
- PRD already planned v2 Operational Control Plane — this expands, not replaces
- Architecture analysis confirmed clean integration with existing codebase patterns
- 6-tier priority roadmap enables incremental delivery (Foundation → MLP → Differentiators → DBA Ops → DAPR → Ecosystem)

**Effort:** High (4 new projects, 258 features across 6 tiers)
**Risk:** Low (additive, no existing code changes, clean DAPR abstraction boundary)
**Timeline:** v2 scope — Foundation + Priority 1 (MLP) deliverable in first v2 sprint

---

## 4. Detailed Change Proposals

### New Epics

#### Epic 14: Admin API Foundation & Abstractions
Shared service interfaces, DTOs, and Admin.Server REST API backed by DAPR. The single backend consumed by Web UI (in-process), CLI (HTTP), and MCP (HTTP). Aspire integration with dedicated DAPR sidecar.

**FRs covered:** FR79, FR82
**NFRs covered:** NFR40, NFR44, NFR46
**Also:** ADR-P4, ADR-P5
**Dependencies:** Epics 1-2 (Contracts types, state store key patterns)

**Stories (high-level):**
- 14.1: Admin.Abstractions — service interfaces (IEventStreamService, IProjectionService, IHealthService) and DTOs
- 14.2: Admin.Server — DAPR-backed service implementations using identical key derivation from Contracts
- 14.3: Admin.Server — REST API controllers with JWT auth and role-based access
- 14.4: Admin.Server — Aspire resource integration in AppHost with DAPR sidecar
- 14.5: Admin API — OpenAPI spec with Swagger UI at `/swagger`

#### Epic 15: Admin Web UI — Core Developer Experience
Blazor Fluent UI shell with the minimum feature set that makes a developer say "I can't live without this." Activity feed, stream browser, aggregate state inspector, projection dashboard, command palette, dark mode.

**FRs covered:** FR68, FR69, FR70, FR71, FR73, FR74, FR75
**NFRs covered:** NFR41, NFR45
**UX-DRs:** UX-DR34-49
**Dependencies:** Epic 14

**Stories (high-level):**
- 15.1: Blazor shell — FluentUI layout, navigation, command palette (Ctrl+K), dark mode
- 15.2: Activity feed — last N active streams with type, timestamp, status
- 15.3: Stream browser — drill into any stream, unified command/event/query timeline
- 15.4: Aggregate state inspector — before/after snapshots per event, state diff viewer
- 15.5: Projection dashboard — status, lag, errors, controls (pause/resume/reset)
- 15.6: Event type catalog — searchable registry with schemas and relationships
- 15.7: Health dashboard — vital signs with deep links to Zipkin/Grafana/Aspire
- 15.8: Deep linking and breadcrumbs for every view

#### Epic 16: Admin Web UI — DBA Operations
Storage management, snapshot controls, backup/restore, tenant management, dead-letter queue, consistency checker. Enables Journey 8 (Maria).

**FRs covered:** FR76, FR77, FR78
**NFRs covered:** NFR41
**Dependencies:** Epic 15

**Stories (high-level):**
- 16.1: Storage growth analyzer with trend projection and treemap breakdown
- 16.2: Snapshot management — view, create, configure auto-snapshot policies
- 16.3: Compaction manager — configure and trigger compaction with space reclaimed metrics
- 16.4: Backup & restore console — schedule, verify integrity, point-in-time restore
- 16.5: Tenant management — quotas, onboarding wizard, comparison, isolation verification
- 16.6: Dead-letter queue manager — browse, search, retry, skip, archive with bulk operations
- 16.7: Consistency checker — on-demand verification with anomaly reporting

#### Epic 17: Admin CLI (`eventstore-admin`)
.NET global tool calling Admin API over HTTP. Scriptable, pipe-friendly, CI/CD-ready.

**FRs covered:** FR79, FR80
**NFRs covered:** NFR42
**UX-DRs:** UX-DR50-55
**Dependencies:** Epic 14

**Stories (high-level):**
- 17.1: CLI scaffold — `System.CommandLine` root with global options (`--url`, `--token`, `--format`)
- 17.2: `stream` subcommand — query, list, events, state-at-position
- 17.3: `projection` subcommand — list, status, pause, resume, reset
- 17.4: `health` subcommand — exit codes for CI/CD gates
- 17.5: `tenant` subcommand — list, quotas, verify isolation
- 17.6: `snapshot` and `backup` subcommands
- 17.7: Connection profiles and shell completion scripts
- 17.8: dotnet tool packaging and distribution

#### Epic 18: Admin MCP Server
MCP server exposing admin operations as AI-callable tools. Enables Journey 9 (Claude).

**FRs covered:** FR79, FR81
**NFRs covered:** NFR43
**UX-DRs:** UX-DR56-59
**Dependencies:** Epic 14

**Stories (high-level):**
- 18.1: MCP server scaffold — stdio transport, HttpClient to Admin API
- 18.2: Read tools — stream query, aggregate state, projection health, schema discovery, metrics
- 18.3: Diagnostic tools — causation chain resolver, diff/comparison, consistency check
- 18.4: Write tools with approval gates — projection controls, backup trigger
- 18.5: Tenant context and investigation session state

#### Epic 19: Admin — DAPR Infrastructure Visibility
DAPR-specific monitoring: sidecar health, actor inspector, pub/sub delivery metrics, resiliency policy viewer, component health history.

**FRs covered:** FR75 (DAPR portion)
**Dependencies:** Epics 14, 15

**Stories (high-level):**
- 19.1: DAPR component status dashboard — sidecar health, state store, pub/sub
- 19.2: DAPR actor inspector — active actors, type, state size, placement
- 19.3: DAPR pub/sub delivery metrics — per-topic success/failure, retry counts
- 19.4: DAPR resiliency policy viewer — timeout/retry/circuit breaker configuration
- 19.5: DAPR component health history — timeline revealing patterns

#### Epic 20: Admin — Advanced Debugging (Blame/Bisect/Replay)
Breakthrough debugging features that differentiate Hexalith from all competitors.

**FRs covered:** FR70, FR71, FR72
**Dependencies:** Epic 15

**Stories (high-level):**
- 20.1: Blame view — per-field provenance for aggregate state (which event set each value)
- 20.2: Bisect tool — binary search through event history to find state divergence
- 20.3: Step-through event debugger — forward/backward stepping through events
- 20.4: Command sandbox — test commands against sandboxed aggregate copies
- 20.5: Correlation ID trace map — full cross-aggregate business transaction visualization

---

## 5. Implementation Handoff

**Change Scope: Major** — 7 new epics, 4 new projects, new deployment topology.

**Handoff Recipients:**
- **Architect (Jerome):** Validate ADR-P4 and ADR-P5 against implementation reality. Define Admin API contract (OpenAPI spec) before Epic 14 implementation begins.
- **Development team:** Implement Epics 14-20 following the priority sequence: 14 (Foundation) → 15+17+18 (parallel MLP) → 16 → 19 → 20.
- **DevOps:** Update CI/CD pipeline for 4 new projects, 7 NuGet packages, CLI tool publish. Add DAPR access control for `admin-server`.

**Success Criteria:**
- Epic 14 complete: Admin API serves stream queries, projection status, and health checks via REST with JWT auth
- Epic 15 MLP complete: A developer can browse streams, inspect aggregate state, check projection health, and navigate via command palette in the Blazor UI
- Epic 17 MLP complete: `eventstore-admin health` returns exit code 0/1/2; `eventstore-admin stream query` returns JSON
- Epic 18 MLP complete: An MCP-connected AI agent can query streams, check projections, and trace causation chains

**FR Coverage Map for New Epics:**

| FR | Epic | Description |
|----|------|-------------|
| FR68 | 15 | Recently active streams listing |
| FR69 | 15 | Unified command/event/query timeline |
| FR70 | 15 + 20 | Point-in-time state exploration |
| FR71 | 15 + 20 | Aggregate state diff |
| FR72 | 20 | Full causation chain tracing |
| FR73 | 15 | Projection management |
| FR74 | 15 | Event/command/aggregate type catalog |
| FR75 | 15 + 19 | Operational health + DAPR visibility |
| FR76 | 16 | Storage management |
| FR77 | 16 | Tenant management |
| FR78 | 16 | Dead-letter queue management |
| FR79 | 14 | Three-interface shared Admin API |
| FR80 | 17 | CLI output formats, exit codes, completions |
| FR81 | 18 | MCP structured tools with approval gates |
| FR82 | 15 | Observability deep links |
