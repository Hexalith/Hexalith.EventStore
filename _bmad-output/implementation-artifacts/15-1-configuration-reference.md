# Story 15.1: Configuration Reference

Status: done

## Story

As a developer tuning Hexalith for their environment,
I want a complete configuration reference for all system knobs,
so that I can understand and adjust every configurable setting.

## Acceptance Criteria

1. **Given** a developer navigates to `docs/guides/configuration-reference.md` **When** they read the page **Then** the page documents every configurable setting: environment variables, DAPR component fields, Aspire configuration, application settings
2. **And** each setting includes: name, description, default value, valid values, and an example
3. **And** settings are organized by category (application, DAPR, infrastructure)
4. **And** the page follows the standard page template (back-link, H1, summary paragraph, prerequisites callout, content sections, Next Steps footer)
5. **And** the page is self-contained — a reader arriving from search can understand it without reading prerequisites (FR43, NFR10: max 2-page prereq depth)
6. **And** FR21 status in `docs/fr-traceability.md` is updated from GAP to COVERED referencing this page

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/configuration-reference.md` (AC: #1-5)
  - [x] 1.1 Add page template structure (back-link, H1, summary, prerequisites, Next Steps)
  - [x] 1.2 Write "Application Settings" section covering all `EventStore:*` config sections
  - [x] 1.3 Write "Authentication & JWT" section covering `Authentication:JwtBearer` options
  - [x] 1.4 Write "Fluent Client SDK Configuration" section covering `EventStoreOptions` and `EventStoreDomainOptions` with 5-layer cascade
  - [x] 1.5 Write "DAPR Infrastructure" summary section with links to `dapr-component-reference.md` (do NOT duplicate DAPR YAML)
  - [x] 1.6 Write "Environment Variables" section covering OTEL, DAPR, and infrastructure env vars
  - [x] 1.7 Write "Aspire Orchestration" section covering AppHost configuration
  - [x] 1.8 Write "Health & Observability" section covering endpoints and OpenTelemetry
  - [x] 1.9 Add quick-reference summary table at end with all settings
- [x] Task 2: Update `docs/fr-traceability.md` — set FR21 to COVERED (AC: #6)
- [x] Task 3: Add cross-link from related guides to the new page (AC: #5)
  - [x] 3.1 Add reference in `docs/guides/dapr-component-reference.md` Next Steps or Related
  - [x] 3.2 Add reference in `docs/guides/deployment-progression.md` Next Steps or Related
- [x] Task 4: Validate with `markdownlint-cli2` and verify all internal links resolve

### Review Follow-ups (AI)

- [x] [AI-Review][HIGH] AC #2 requires valid values per setting; added explicit valid values in the quick-reference table for documented settings. [docs/guides/configuration-reference.md]
- [x] [AI-Review][HIGH] AC #1 complete-knobs gap for Layer 4 fixed by documenting `EventStore:Domains:{domain}` with examples. [docs/guides/configuration-reference.md]
- [x] [AI-Review][HIGH] Dev Agent Record file-list discrepancy resolved by reconciling `File List` to current review/fix working tree reality.
- [x] [AI-Review][MEDIUM] Story status/task consistency corrected after follow-up closure; story advanced from in-progress to done.

## Dev Notes

### Configuration Categories to Document

The codebase contains 20+ configuration categories and 60+ individual settings. Organize by these major sections:

#### Section 1: Application Settings (`appsettings.json`)

**Authentication (`Authentication:JwtBearer`)**
- `Authority` (string, empty) — OIDC authority URL for production
- `Audience` (string, empty) — Expected JWT audience claim (required)
- `Issuer` (string, empty) — Expected JWT issuer claim (required)
- `SigningKey` (string, empty) — Symmetric key for dev/test (min 32 chars for HS256)
- `RequireHttpsMetadata` (bool, true) — HTTPS for OIDC metadata
- Validation: Either `Authority` OR `SigningKey` must be set; `Issuer` + `Audience` always required

**Rate Limiting (`EventStore:RateLimiting`)**
- `PermitLimit` (int, 100) — Max requests per window per tenant
- `WindowSeconds` (int, 60) — Sliding window duration
- `SegmentsPerWindow` (int, 6) — Window segments
- `QueueLimit` (int, 0) — Queue depth; 0 = immediate 429 rejection
- Health endpoints (`/health`, `/alive`, `/ready`) are excluded from rate limiting

**Extension Metadata (`EventStore:ExtensionMetadata`)**
- `MaxTotalSizeBytes` (int, 4096)
- `MaxKeyLength` (int, 128)
- `MaxValueLength` (int, 2048)
- `MaxExtensionCount` (int, 32)

**Event Publisher (`EventStore:Publisher`)**
- `PubSubName` (string, "pubsub") — DAPR pub/sub component name
- `DeadLetterTopicPrefix` (string, "deadletter") — Dead-letter topic format: `{prefix}.{tenant}.{domain}.events`

**Event Drain / Recovery (`EventStore:Drain`)**
- `InitialDrainDelay` (timespan, 30s) — Delay before first drain attempt after pub failure
- `DrainPeriod` (timespan, 1min) — Recurring retry interval
- `MaxDrainPeriod` (timespan, 30min) — Upper bound for retry intervals

**Snapshots (`EventStore:Snapshots`)**
- `DefaultInterval` (int, 100) — Events between snapshots (minimum: 10)
- `DomainIntervals:{domainName}` (int) — Per-domain overrides

**Command Status (`EventStore:CommandStatus`)**
- `TtlSeconds` (int, 86400) — Status entry TTL (24h default)
- `StateStoreName` (string, "eventstore") — DAPR state store name

**Domain Services (`EventStore:DomainServices`)**
- `ConfigStoreName` (string, "configstore") — DAPR config store name
- `InvocationTimeoutSeconds` (int, 5) — Sidecar call timeout
- `MaxEventsPerResult` (int, 1000) — Max events per domain result
- `MaxEventSizeBytes` (int, 1048576) — Max single event size (1 MB)
- `Registrations:{key}` (object) — Static registrations keyed by `"{tenant}|{domain}|{version}"`

**OpenAPI (`EventStore:OpenApi`)**
- `Enabled` (bool, true) — Enable `/swagger` endpoint

#### Section 2: Fluent Client SDK Configuration (Programmatic)

**EventStoreOptions** (global)
- `EnableRegistrationDiagnostics` (bool, false)
- `DefaultStateStoreSuffix` (string, "eventstore") — Produces `{domain}-eventstore`
- `DefaultTopicSuffix` (string, "events") — Produces `{domain}.events`

**EventStoreDomainOptions** (per-domain)
- `StateStoreName`, `TopicPattern`, `DeadLetterTopicPattern` — Override conventions per domain

**5-Layer Configuration Cascade** (priority low-to-high):
1. Convention defaults (NamingConventionEngine)
2. Global code options (EventStoreOptions)
3. Domain self-config (OnConfiguring override)
4. External config (appsettings.json)
5. Explicit override (ConfigureDomain callback)

#### Section 3: Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | (empty) | OpenTelemetry OTLP exporter endpoint |
| `DAPR_HTTP_PORT` | (auto) | Override DAPR sidecar HTTP port |
| `REDIS_HOST` | "localhost:6379" | Redis host for DAPR components |
| `REDIS_PASSWORD` | (empty) | Redis password |
| `DAPR_TRUST_DOMAIN` | "hexalith.io" | SPIFFE trust domain for mTLS |
| `DAPR_NAMESPACE` | "hexalith" | K8s namespace for access control |
| `POSTGRES_CONNECTION_STRING` | (N/A) | PostgreSQL connection string (production) |
| `RABBITMQ_CONNECTION_STRING` | (N/A) | RabbitMQ connection string (production) |
| `SUBSCRIBER_APP_ID` | (N/A) | Production subscriber app-id |
| `OPS_MONITOR_APP_ID` | (N/A) | Operations monitor app-id |

#### Section 4: Aspire Orchestration (AppHost)

- `EnableKeycloak` — Enable Keycloak identity provider (set "false" to disable)
- `PUBLISH_TARGET` — Aspire publisher target: "docker", "k8s", or "aca"

#### Section 5: DAPR Infrastructure (link to dapr-component-reference.md)

Do NOT duplicate DAPR YAML. Instead, provide a brief overview table of DAPR component types and link to `dapr-component-reference.md` for full details:
- State Store (`state.redis` / `state.postgresql` / `state.azure.cosmosdb`)
- Pub/Sub (`pubsub.redis` / `pubsub.rabbitmq` / `pubsub.kafka` / `pubsub.azure.servicebus`)
- Configuration (`configuration.redis`)
- Resiliency (retry, timeout, circuit breaker policies)
- Access Control (deny-by-default, SPIFFE trust domain)

#### Section 6: Health & Observability

- `/health` — Full health checks
- `/alive` — Liveness checks (tagged "live")
- `/ready` — Readiness checks (tagged "ready")
- Status codes: Healthy/Degraded = 200, Unhealthy = 503
- OpenTelemetry: Metrics (AspNetCore, HttpClient, Runtime), Tracing (custom activity sources + AspNetCore + HttpClient)
- Structured JSON console logging with UTC timestamps

#### Section 7: Quick-Reference Summary Table

End the page with a single table listing every configurable key, type, default, and category. This is the "all system knobs" quick scan.

### Existing Documentation to Reference (NOT Duplicate)

- **`docs/guides/dapr-component-reference.md`** — Extensive DAPR component YAML per backend (Redis, PostgreSQL, Cosmos DB, RabbitMQ, Kafka, Service Bus). Link heavily, do not duplicate.
- **`docs/guides/deployment-progression.md`** — Environment-specific configuration format differences and backend compatibility matrix. Link for "which backend for which environment" context.
- **`docs/reference/nuget-packages.md`** — Package-level configuration namespaces (`EventStoreOptions`, `EventStoreDomainOptions`, `SnapshotOptions`).
- **`docs/guides/security-model.md`** — Authentication flow details. Link for JWT/auth deep dive.

### Project Structure Notes

- **Output file:** `docs/guides/configuration-reference.md`
- Existing guides in `docs/guides/`: deployment-docker-compose, deployment-kubernetes, deployment-azure-container-apps, deployment-progression, dapr-component-reference, security-model, troubleshooting, disaster-recovery
- Configuration reference fills the last planned slot in `docs/guides/` for Phase 2 operations docs
- File naming convention: lowercase, hyphen-separated, descriptive (NFR26)

### Page Template Requirements

Follow the established page structure (confirmed in Story 14-8 and all prior docs):

```markdown
[<- Back to Hexalith.EventStore](../../README.md)

# Configuration Reference

One-paragraph summary explaining this is the complete configuration reference for all Hexalith.EventStore settings.

> **Prerequisites:** [Prerequisites](../getting-started/prerequisites.md), [Quickstart](../getting-started/quickstart.md)

## Content Sections...

## Next Steps

- **Next:** [Deployment Progression](deployment-progression.md) — Choose your deployment target
- **Related:** [DAPR Component Reference](dapr-component-reference.md), [Security Model](security-model.md), [Troubleshooting](troubleshooting.md)
```

### Formatting Conventions

- Code blocks: Always specify language (`csharp`, `json`, `yaml`, `bash`)
- Tables: Use for structured settings reference — columns: Setting, Type, Default, Description
- Callouts: Use `> **Note:**`, `> **Warning:**`, `> **Tip:**` (NOT `[!NOTE]`)
- Code examples: Use Counter domain names (`IncrementCounter`, `CounterProcessor`, `CounterState`)
- Voice: Second person ("you"), active voice, developer-to-developer tone
- DAPR: Explain what DAPR does in context; at "operational" depth for this guide type
- No YAML frontmatter (GitHub renders it as visible text)
- No hard wrap in markdown source

### Validation Requirements

- `markdownlint-cli2` must pass with project config (`.markdownlint-cli2.jsonc`)
- All internal links must resolve to existing files
- Heading hierarchy: H1 (page title, one only) -> H2 -> H3, never skip levels
- No bare code fences (always specify language)

### Branch & Commit Convention

- Branch: `docs/story-15-1-configuration-reference`
- Commit: `feat(docs): Add configuration reference (Story 15-1)`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 8, Story 8.1]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#Content Folder Structure]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR21]
- [Source: src/Hexalith.EventStore.CommandApi/appsettings.json]
- [Source: src/Hexalith.EventStore.CommandApi/Program.cs]
- [Source: src/Hexalith.EventStore.Client/Configuration/EventStoreOptions.cs]
- [Source: src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs]

### Previous Story Intelligence (from Story 14-8)

- Page template confirmed working: back-link, H1, intro, prerequisites blockquote, content, Next Steps
- Mermaid diagrams need `<details>` text descriptions for accessibility (NFR7) — likely not needed for this story
- markdownlint-cli2 must pass before completion
- Branch pattern: `docs/story-15-X-<description>`
- Commit pattern: `feat(docs): <description> (Story 15-X)`
- Code blocks need language hints for syntax highlighting
- Verify all internal links resolve to existing files
- Cross-reference updates are part of the story (update related guides' Next Steps)

### Git Intelligence

Recent commit pattern (last 10 commits):
```
bb1ee66 feat(docs): Add disaster recovery procedure (Story 14-8)
850b5a7 Merge pull request #86 from Hexalith/docs/story-14-7-troubleshooting-guide
d07f00e feat(docs): Add troubleshooting guide (Story 14-7)
206d011 Merge pull request #85 from Hexalith/docs/story-14-6-security-model-documentation
c3574c7 feat(docs): Add security model documentation (Story 14-6)
09a7ec9 Merge pull request #84 from Hexalith/docs/story-14-5-dapr-component-configuration-reference
0525b7a feat(docs): Add DAPR component configuration reference (Story 14-5)
bc80b1a Merge pull request #83 from Hexalith/docs/story-14-3-azure-container-apps-deployment-guide
f04bdf7 feat(docs): Add deployment progression guide (Story 14-4)
47a46e2 Merge pull request #82 from Hexalith/docs/story-14-3-azure-container-apps-deployment-guide
```

Pattern: Feature branch per story, single commit with `feat(docs):` prefix, merge via PR.

### Key Differentiator

The configuration reference must explicitly document how configuration enables **backend swapping with zero code changes** (Redis -> PostgreSQL, Docker -> Kubernetes -> Azure). This is Hexalith's core competitive differentiator and FR21's implicit requirement. Show the 5-layer configuration cascade as the mechanism that makes this possible.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- markdownlint-cli2: 0 errors on configuration-reference.md
- All internal links verified (7/7 resolve)
- Tier 1 tests: 465 passed, 0 failed (Contracts: 157, Client: 231, Sample: 29, Testing: 48)
- Build: 0 warnings, 0 errors

### Completion Notes List

- Created comprehensive configuration reference covering 45+ settings across 7 categories
- Verified all setting names, types, and defaults against source code (7 configuration option classes)
- Documented 5-layer configuration cascade as the mechanism enabling zero-code-change backend swapping
- DAPR infrastructure section links to dapr-component-reference.md without duplicating YAML
- Updated FR21 from GAP to COVERED in fr-traceability.md (summary counts updated: 43 covered, 17 gaps)
- Updated cross-links in dapr-component-reference.md (replaced placeholder) and deployment-progression.md (added new link)
- Quick-reference summary table at end lists all 45+ configurable settings with type, default, and category

### File List

- `docs/guides/configuration-reference.md` (modified) — Added Layer 4 `EventStore:Domains:{domain}` documentation and explicit valid-values coverage in quick-reference table
- `_bmad-output/implementation-artifacts/15-1-configuration-reference.md` (modified) — Review follow-ups closed, status updated to done, review outcome updated
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified) — Story status synced from in-progress to done

## Senior Developer Review (AI)

### Reviewer

Jerome

### Date

2026-03-02

### Outcome

Approved

### Summary

- Acceptance Criteria #1 and #2 gaps identified in review were fixed in `docs/guides/configuration-reference.md`.
- Story metadata was reconciled with current working-tree file changes.
- Story is now complete and marked done.

### Key Findings

1. **RESOLVED (HIGH)** — Added explicit per-setting valid values in the quick-reference table.
2. **RESOLVED (HIGH)** — Added missing Layer 4 external domain configuration section: `EventStore:Domains:{domain}`.
3. **RESOLVED (HIGH)** — Reconciled story file list with current review/fix working tree.
4. **RESOLVED (MEDIUM)** — Restored status/task consistency after review follow-up closure.

## Change Log

- 2026-03-02: Created configuration reference documentation covering all system knobs (application settings, authentication, fluent SDK, DAPR infrastructure, environment variables, Aspire orchestration, health/observability). Updated FR traceability and cross-links in related guides.
- 2026-03-02: Senior Developer Review (AI) completed. Added follow-up action items, set status to in-progress, and requested changes.
- 2026-03-02: Applied review fixes (valid-values coverage + Layer 4 domain external config), reconciled file list, and approved story completion.
