# Story 14.8: Disaster Recovery Procedure

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator responsible for data integrity,
I want a documented disaster recovery procedure for the event store,
so that I can recover from data loss scenarios.

## Acceptance Criteria

1. `docs/guides/disaster-recovery.md` documents backup strategies per DAPR state store backend: Redis, PostgreSQL, Azure Cosmos DB (FR55)
2. The page documents recovery steps for each backend with step-by-step procedures
3. The page documents data verification procedures to validate event stream integrity after restore
4. The page documents RTO/RPO considerations per deployment environment and backend
5. Content is specific to the DAPR state store backends documented in this project (Redis for development, PostgreSQL for on-premise production, Azure Cosmos DB for cloud)
6. The page follows the standard page template (back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1, intro paragraph, prerequisites blockquote, content sections, next steps)
7. The page is self-contained — an operator arriving from search can understand the DR procedures without reading prerequisite pages (FR43); DAPR terms defined on first use
8. All internal links resolve to existing files
9. markdownlint-cli2 validation passes with zero errors

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/disaster-recovery.md` (AC: all)
  - [x] 1.1 Write page header: back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1 title "Disaster Recovery Procedure", intro paragraph explaining this page covers backup strategies, recovery procedures, and data verification for the event store across all supported DAPR state store backends, and prerequisites blockquote linking to `../getting-started/prerequisites.md` and deployment guides (AC: #6, #7)
  - [x] 1.2 Write "Understanding the Data Model" section (AC: #3, #7):
    - Explain the event store data model from an operator perspective: event streams (write-once keys with pattern `{tenant}:{domain}:{aggregateId}:events:{sequence}`), aggregate metadata keys, snapshots, and command status entries
    - Explain that events are append-only and immutable — the primary source of truth
    - Explain the persist-then-publish pattern: state store is the source of truth; pub/sub subscriptions can be rebuilt from events
    - Explain command status entries have 24-hour TTL (ephemeral, not critical for DR)
    - Explain snapshots are performance optimization only — can be rebuilt by replaying events
    - Define DAPR terms on first use (state store, sidecar, component)
  - [x] 1.3 Write "RTO/RPO Considerations" section (AC: #4):
    - Define RTO (Recovery Time Objective) and RPO (Recovery Point Objective) for operators
    - Document RTO/RPO characteristics per backend:
      - Redis (development): RPO = last persistence snapshot (if AOF/RDB enabled), RTO = container restart time. No production durability guarantee.
      - PostgreSQL (on-premise production): RPO = last WAL commit (near-zero with synchronous replication), RTO = restore time from backup + WAL replay
      - Azure Cosmos DB (cloud): RPO = automatic backups (configurable, default 4-hour interval with continuous backup option), RTO = automatic failover (minutes with multi-region)
    - Include a summary table comparing RTO/RPO per backend
    - Note that NFR22 requires zero data loss — PostgreSQL WAL + Cosmos DB continuous backup achieve this
  - [x] 1.4 Write "Redis Backup and Recovery (Development)" section (AC: #1, #2, #5):
    - Document Redis persistence options: RDB snapshots and AOF (Append Only File)
    - Note this is for development environments only — not recommended for production event store data
    - Document backup procedure: `BGSAVE` for RDB snapshots, AOF file copy
    - Document restore procedure: stop Redis, replace dump.rdb or AOF file, restart Redis
    - Note: for local Docker Compose development, data loss on restart is expected behavior; re-run the sample to regenerate test data
  - [x] 1.5 Write "PostgreSQL Backup and Recovery (Production)" section (AC: #1, #2, #5):
    - Document PostgreSQL as the recommended production on-premise backend
    - Document backup strategies:
      - `pg_dump` for logical backups (full database or specific tables)
      - `pg_basebackup` for physical backups with WAL archival
      - Continuous archiving with WAL for point-in-time recovery (PITR)
    - Document DAPR state store table structure: state table with `key`, `value`, `etag`, `expiredate` columns
    - Document restore procedure step-by-step:
      1. Stop EventStore application (prevent new writes)
      2. Restore PostgreSQL from backup (pg_restore or PITR)
      3. Verify event stream integrity (see verification section)
      4. Restart EventStore application
      5. Verify actor rehydration succeeds
    - Document point-in-time recovery procedure using WAL replay
    - Include example `pg_dump` and `pg_restore` commands with DAPR state store database
  - [x] 1.6 Write "Azure Cosmos DB Backup and Recovery (Cloud)" section (AC: #1, #2, #5):
    - Document Azure Cosmos DB automatic backup (periodic and continuous modes)
    - Document periodic backup: default 4-hour interval, 2 copies retained, configurable
    - Document continuous backup: point-in-time restore within retention window (7 or 30 days)
    - Document restore procedure:
      1. Identify target restore point (timestamp)
      2. Create restore request via Azure Portal or `az cosmosdb restore`
      3. Restore creates a new Cosmos DB account (cannot restore in-place)
      4. Update DAPR component configuration to point to restored account
      5. Verify event stream integrity
      6. Switch traffic to restored instance
    - Document multi-region failover: automatic failover with Azure Cosmos DB multi-region write
    - Include Azure CLI commands for restore operations
  - [x] 1.7 Write "Data Verification Procedures" section (AC: #3):
    - Document how to verify event stream integrity after restore:
      1. Check aggregate metadata keys: verify sequence numbers are consistent
      2. Verify event key continuity: no gaps in `{tenant}:{domain}:{aggregateId}:events:{1..N}` sequence
      3. Test actor rehydration: activate a known aggregate and verify state matches expected
      4. Verify snapshot consistency: snapshots should reflect state at their recorded sequence number
    - Document verification commands per backend:
      - PostgreSQL: SQL queries to check key patterns, sequence gaps, row counts
      - Azure Cosmos DB: queries via Azure Portal or SDK to verify partition data
    - Document tenant isolation verification: confirm no cross-tenant data leakage in restored data
    - Note: events are immutable write-once keys — if present, they are correct (cannot be partially written due to actor-level ACID)
  - [x] 1.8 Write "Pub/Sub Recovery" section (AC: #2):
    - Explain that pub/sub is NOT the source of truth — events in the state store are
    - Document that missed pub/sub deliveries during outage are handled by:
      - DAPR retry policies (at-least-once delivery)
      - Dead-letter topics for failed deliveries
      - The persist-then-publish pattern ensures events survive pub/sub outages
    - Document how to republish events from state store if needed (actor reactivation triggers publish of unpublished events via checkpointed state machine)
    - Document dead-letter topic recovery: inspect `deadletter.{tenant}.{domain}.events` for failed messages
  - [x] 1.9 Write "Disaster Recovery Runbook" section (AC: #2):
    - Provide a condensed step-by-step DR runbook for each scenario:
      - Scenario 1: State store data loss (full database loss)
      - Scenario 2: Partial data corruption (specific tenant/aggregate affected)
      - Scenario 3: Pub/sub system failure (messages lost but state store intact)
      - Scenario 4: Complete environment failure (all infrastructure lost)
    - Each scenario: detection signals, immediate actions, recovery steps, verification
  - [x] 1.10 Write "Next Steps" section: Links to:
    - [Troubleshooting Guide](troubleshooting.md) — operational error resolution
    - [DAPR Component Configuration Reference](dapr-component-reference.md) — state store backend configuration
    - [Security Model](security-model.md) — security considerations during recovery
    - [Deployment Progression Guide](deployment-progression.md) — environment comparison
    - Deployment guides per environment (Docker Compose, Kubernetes, Azure Container Apps)
- [x] Task 2: Update cross-references in existing documentation (AC: #8)
  - [x] 2.1 Update `docs/fr-traceability.md`: change FR55 status from `GAP` to `COVERED` with link to `docs/guides/disaster-recovery.md`
  - [x] 2.2 Update `docs/guides/troubleshooting.md` Next Steps section: add link to `[Disaster Recovery Procedure](disaster-recovery.md)` if not already present
  - [x] 2.3 Update `docs/guides/deployment-progression.md`: add link to disaster recovery guide in the operations references if applicable
  - [x] 2.4 Verify all internal links in the new page resolve to existing files
- [x] Task 3: Validation (AC: all)
  - [x] 3.1 Verify the page structure follows the page template convention (back-link, H1, intro paragraph, prerequisites blockquote, content sections, next steps)
  - [x] 3.2 Verify all internal links resolve to existing files
  - [x] 3.3 Run markdownlint-cli2 on `docs/guides/disaster-recovery.md` to ensure CI compliance
  - [x] 3.4 Verify each backend section covers all three required elements: backup strategy, recovery steps, and data verification
  - [x] 3.5 Verify FR55 coverage: operator can follow a documented disaster recovery procedure
  - [x] 3.6 Verify DAPR terms are defined on first use (self-contained requirement, FR43)
  - [x] 3.7 Verify RTO/RPO considerations are documented per backend

## Dev Notes

### Architecture Patterns & Constraints

- **This is a DOCUMENTATION-ONLY story.** No application code changes. Primary output is `docs/guides/disaster-recovery.md` with cross-reference updates to existing guides.
- **FR55 is the primary requirement:** "An operator can follow a documented disaster recovery procedure for the event store"
- **FR60 is contextual:** "An operator can understand where event data is physically stored based on their DAPR state store configuration and what persistence guarantees each backend provides" — the DR guide builds on this by documenting what to back up and how to restore it
- **FR43 (self-contained pages):** The page must be navigable without reading prerequisite pages. Define DAPR terms on first use: "state store" (database backend), "sidecar" (helper process), "component" (YAML configuration), "pub/sub" (event distribution).
- **NFR22 (zero data loss):** The architecture mandates zero data loss via persist-then-publish pattern. DR procedures must demonstrate how this is achieved per backend.
- **Target audience is OPERATORS, not developers.** Use operational language. Assume reader knows infrastructure (databases, backups, Kubernetes) but NOT Hexalith.EventStore internals or DAPR.

### Event Store Data Model (Critical for DR)

The event store persists data using DAPR state store with composite keys:

| Data Type | Key Pattern | Mutability | DR Priority |
|-----------|------------|------------|-------------|
| Events | `{tenant}:{domain}:{aggregateId}:events:{sequence}` | Write-once, immutable | **CRITICAL** — source of truth |
| Metadata | `{tenant}:{domain}:{aggregateId}:metadata` | Updated (ETag concurrency) | **HIGH** — tracks sequence numbers |
| Snapshots | `{tenant}:{domain}:{aggregateId}:snapshot` | Updated periodically | **LOW** — can be rebuilt from events |
| Command Status | `{tenant}:{correlationId}:status` | TTL 24h, ephemeral | **NONE** — auto-expires, advisory only |

**Key insight for DR:** Events are the ONLY critical data. Metadata and snapshots can be reconstructed. Command status is ephemeral. The DR guide should prioritize event backup above all else.

### State Store Backend Characteristics

| Backend | Environment | Persistence | Transaction Support | Backup Method |
|---------|------------|-------------|-------------------|---------------|
| Redis | Docker Compose (dev) | Optional (AOF/RDB) | No multi-key transactions | BGSAVE / AOF copy |
| PostgreSQL | Kubernetes (on-prem) | Full ACID durability | Multi-key transactions | pg_dump / pg_basebackup + WAL |
| Azure Cosmos DB | Azure Container Apps | Multi-region redundant | Within partition | Automatic backup (periodic/continuous) |

### DAPR State Store Table Structure (PostgreSQL)

DAPR creates a `state` table in the configured database:

```sql
-- DAPR state store table structure
CREATE TABLE state (
    key TEXT NOT NULL PRIMARY KEY,
    value JSONB NOT NULL,
    etag TEXT NOT NULL,
    insertdate TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updatedate TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    expiredate TIMESTAMP WITH TIME ZONE
);
```

Verification queries should target this table structure for integrity checks.

### Persist-Then-Publish Pattern (D2 Architecture Decision)

The event store follows a strict ordering:
1. Events persisted to state store first (atomic via actor state manager)
2. Publication to pub/sub only after successful persistence
3. Checkpointed state machine tracks each step (8 states: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut)
4. On crash recovery: resumes from last checkpoint — events already persisted are safe

**DR implication:** State store backup captures ALL committed events. Pub/sub can be rebuilt — it is a distribution mechanism, not a source of truth.

### Security Constraints During Recovery

| Constraint | Requirement | DR Implication |
|-----------|-------------|----------------|
| SEC-1 | EventStore owns all envelope metadata | Restored data must preserve server-populated metadata |
| SEC-2 | Tenant validation BEFORE state rehydration | Multi-tenant backups must maintain tenant isolation during restore |
| SEC-3 | Command status queries are tenant-scoped | No cross-tenant data leakage in restored backups |
| SEC-5 | Event payload never in logs | DR procedures must NOT log event payloads during verification |

### Existing Documentation Cross-References

| Page | Action |
|------|--------|
| `docs/fr-traceability.md` | Change FR55 from `GAP` to `COVERED` with link to `docs/guides/disaster-recovery.md` |
| `docs/guides/troubleshooting.md` | Add link to disaster recovery guide in Next Steps |
| `docs/guides/deployment-progression.md` | Add link to disaster recovery guide if applicable |

### DAPR Component Configuration Files (for reference in DR procedures)

| File | Purpose |
|------|---------|
| `deploy/dapr/statestore-postgresql.yaml` | PostgreSQL state store config — connection string, table name |
| `deploy/dapr/statestore-cosmosdb.yaml` | Azure Cosmos DB state store config — endpoint, database, collection |
| `deploy/dapr/resiliency.yaml` | Retry/circuit breaker policies |

### Project Structure Notes

- File location: `docs/guides/disaster-recovery.md` (per architecture-documentation.md — epics reference this as either in troubleshooting.md or a dedicated page; a dedicated page is appropriate given the depth of content required)
- `docs/guides/` already contains 7 guides (deployment-docker-compose, deployment-kubernetes, deployment-azure-container-apps, deployment-progression, dapr-component-reference, security-model, troubleshooting) — this is the 8th guide
- No new directories needed
- No application code changes — documentation-only story with cross-reference updates
- Reference source code and deployment files for accurate backup/restore commands but do NOT modify them

### Previous Story Intelligence (14-7)

Key learnings from Story 14-7 (Troubleshooting Guide):
- **Page template confirmed:** back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1, intro, prerequisites, content, next steps pattern is consistent across all Epic 14 stories
- **Mermaid diagrams must include `<details>` text descriptions** for accessibility (NFR7) — include if adding any diagrams
- **markdownlint-cli2 must pass** — run validation before completion
- **Internal links verified manually** — ensure all links point to existing files
- **Code blocks need language hints** (`yaml`, `bash`, `json`, `sql`) for syntax highlighting
- **YAML/command examples should be copy-pasteable** with inline comments
- **Cross-references to deployment guides work well** — readers appreciate being directed to environment-specific pages for detailed setup
- **Review findings pattern:** ensure all database names, table structures, and CLI commands match actual DAPR state store behavior. Do NOT guess — verify against DAPR documentation.
- **back-link text:** Use `[<- Back to Hexalith.EventStore](../../README.md)` (ASCII arrow `<-`, not Unicode)
- **Commit pattern:** `feat(docs): <description> (Story 14-8)`
- **Branch pattern:** `docs/story-14-8-disaster-recovery-procedure`
- **Pre-existing markdownlint issues:** 3 pre-existing errors in dapr-component-reference.md at lines 722-724 — not related to this story, ignore them

### Git Intelligence

Recent commits for Epic 14:
- `850b5a7` Merge pull request #86 from Hexalith/docs/story-14-7-troubleshooting-guide
- `d07f00e` feat(docs): Add troubleshooting guide (Story 14-7)
- `206d011` Merge pull request #85 from Hexalith/docs/story-14-6-security-model-documentation
- `c3574c7` feat(docs): Add security model documentation (Story 14-6)
- `09a7ec9` Merge pull request #84 from Hexalith/docs/story-14-5-dapr-component-configuration-reference

Pattern: Each story creates a single `docs/guides/` page, creates a feature branch `docs/story-14-X-<slug>`, commits with `feat(docs):` prefix, and merges via PR.

### Markdownlint Rules

Configuration in `.markdownlint-cli2.jsonc`:
- `MD013`: disabled (no hard wrap)
- `MD014`: disabled (allow `$` prefix)
- `MD033`: allow `<details>`, `<summary>`, `<br>`, `<img>`, `<picture>`, `<source>`
- `MD024`: `siblings_only: true` (duplicate headings OK in different sections)
- `MD041`: disabled (nav links before H1 OK)
- `MD046`: `style: fenced` (fenced code blocks only)
- `MD048`: `style: backtick` (backtick fences only)
- `MD007`: `indent: 4` (4-space list indentation)
- `MD029`: `style: ordered` (sequential ordered list numbering)
- `MD036`: enabled (no bold-as-heading)
- `MD060`: disabled

### Content Voice & Tone

- **Voice:** Second person ("you"), active voice
- **Tone:** Professional-operational — "Before restoring, stop the EventStore application to prevent new writes" not "The system should be stopped prior to restoration activities"
- **Audience:** Operators/SREs who manage infrastructure. Assume they know PostgreSQL, Azure, Kubernetes but NOT Hexalith internals or DAPR specifics.
- **DAPR handling:** Explain what DAPR does in context. e.g., "DAPR uses a state store component (a database configuration defined in YAML) to persist event data"
- **DR style:** Be precise with commands. Use numbered steps. Include verification at every stage. Show expected output where helpful.
- **Command examples:** Use `bash` code blocks with `$` prefix. Use `sql` blocks for PostgreSQL queries. Include inline comments.

### Target Length

This is a comprehensive disaster recovery guide covering 3 backends with backup procedures, restore procedures, verification steps, and runbook scenarios. Target 350-500 lines. Each backend section should be thorough (50-80 lines) — operators need precise, copy-pasteable commands.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 7.8 — Disaster Recovery Procedure acceptance criteria]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR55 — Disaster recovery procedure]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR60 — Data storage and persistence guarantees]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR43 — Self-contained pages]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#NFR22 — Zero data loss requirement]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D1 — Single-Key-Per-Event with Composite Keys]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D2 — Persist-Then-Publish pattern]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#NFR25 — Checkpointed state machine recovery]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#SEC-1 through SEC-5 — Security constraints]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#GAP-14 — DR gap acknowledged]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md — State store backend comparison table]
- [Source: docs/page-template.md — Standard page structure]
- [Source: docs/guides/troubleshooting.md — Previous story page pattern]
- [Source: docs/guides/dapr-component-reference.md — DAPR component configuration details]
- [Source: docs/guides/deployment-docker-compose.md — Docker Compose deployment reference]
- [Source: docs/guides/deployment-kubernetes.md — Kubernetes deployment reference]
- [Source: docs/guides/deployment-azure-container-apps.md — Azure Container Apps deployment reference]
- [Source: docs/guides/deployment-progression.md — Environment comparison]
- [Source: docs/guides/security-model.md — Security model reference]
- [Source: docs/fr-traceability.md — FR55 currently GAP status]
- [Source: deploy/dapr/statestore-postgresql.yaml — PostgreSQL state store DAPR component]
- [Source: deploy/dapr/statestore-cosmosdb.yaml — Azure Cosmos DB state store DAPR component]
- [Source: _bmad-output/implementation-artifacts/14-7-troubleshooting-guide.md — Previous story patterns and learnings]

## Change Log

- 2026-03-02: Created disaster recovery procedure guide (docs/guides/disaster-recovery.md) covering Redis, PostgreSQL, and Azure Cosmos DB backup/recovery. Updated cross-references in fr-traceability.md (FR55 GAP→COVERED), troubleshooting.md, and deployment-progression.md.
- 2026-03-02: Senior code review fixes applied: corrected Redis restore command, corrected Kubernetes deployment name in DR runbook commands, aligned troubleshooting Command API port examples to 8080, and replaced Cosmos verification CLI metadata command with data-integrity queries.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- markdownlint-cli2 validation: 0 errors on docs/guides/disaster-recovery.md
- All 9 internal links verified to resolve to existing files

### Completion Notes List

- Created comprehensive disaster recovery guide (529 lines) covering 3 DAPR state store backends
- Redis section: RDB/AOF persistence options, backup/restore procedures for development environments
- PostgreSQL section: pg_dump, pg_basebackup, WAL archiving with PITR, step-by-step restore procedure
- Azure Cosmos DB section: periodic and continuous backup modes, az CLI restore commands, multi-region failover
- Data verification section: SQL queries for PostgreSQL integrity checks, tenant isolation verification
- Pub/sub recovery section: persist-then-publish guarantees, dead-letter topic recovery, automatic republication
- DR runbook with 4 scenarios: full data loss, partial corruption, pub/sub failure, complete environment failure
- Updated FR55 from GAP to COVERED in fr-traceability.md
- Added DR links to troubleshooting.md and deployment-progression.md Next Steps sections
- DAPR terms defined on first use: state store, sidecar, component, pub/sub (FR43 compliance)
- RTO/RPO comparison table with zero data loss analysis per backend (NFR22 compliance)
- Senior review corrections merged: actionable restore/runbook commands validated, Command API troubleshooting examples aligned with current AppHost port, and Cosmos verification guidance now validates restored data instead of container metadata

### File List

- docs/guides/disaster-recovery.md (NEW)
- docs/fr-traceability.md (MODIFIED — FR55 GAP→COVERED)
- docs/guides/troubleshooting.md (MODIFIED — added DR link in Next Steps)
- docs/guides/deployment-progression.md (MODIFIED — added DR link in Next Steps)
- _bmad-output/implementation-artifacts/sprint-status.yaml (MODIFIED — story status)
- _bmad-output/implementation-artifacts/14-8-disaster-recovery-procedure.md (MODIFIED — task checkboxes, dev record, status)

## Senior Developer Review (AI)

### Review Date

2026-03-02

### Outcome

Approved after fixes.

### Findings and Fixes

1. **HIGH** — Invalid Redis restore command in `docs/guides/disaster-recovery.md` (`docker compose down redis` does not target a single service).
  - **Fix:** Replaced with `docker compose stop redis`.

2. **HIGH** — Incorrect Kubernetes deployment name in DR restore steps (`eventstore-commandapi`) inconsistent with project deployment naming (`commandapi`).
  - **Fix:** Updated both scale commands to `deployment commandapi`.

3. **HIGH** — Troubleshooting commands referenced port `5001` while current AppHost/Command API defaults to `8080`.
  - **Fix:** Updated port-conflict examples and status curl example to `8080` in `docs/guides/troubleshooting.md`.

4. **MEDIUM** — Azure Cosmos DB verification section used a container metadata command that does not verify restored event data integrity.
  - **Fix:** Replaced with explicit data-verification queries (`COUNT` + tenant key-prefix inspection) in `docs/guides/disaster-recovery.md`.
