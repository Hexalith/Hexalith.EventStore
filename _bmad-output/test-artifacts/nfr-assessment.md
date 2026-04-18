---
stepsCompleted:
    - step-01-load-context
    - step-02-define-thresholds
    - step-03-gather-evidence
    - step-04a-subagent-security
    - step-04b-subagent-performance
    - step-04c-subagent-reliability
    - step-04d-subagent-scalability
    - step-04e-aggregate-nfr
    - step-05-generate-report
lastStep: step-05-generate-report
workflowType: testarch-nfr-assess
gate_decision: CONCERNS
overall_risk: HIGH
lastSaved: "2026-04-18"
inputDocuments:
    - _bmad-output/planning-artifacts/prd.md
    - _bmad-output/planning-artifacts/architecture.md
    - _bmad-output/planning-artifacts/epics.md
    - _bmad/tea/testarch/knowledge/adr-quality-readiness-checklist.md
    - _bmad/tea/testarch/knowledge/nfr-criteria.md
    - _bmad/tea/testarch/knowledge/error-handling.md
    - _bmad/tea/testarch/knowledge/test-quality.md
    - _bmad/tea/testarch/knowledge/ci-burn-in.md
    - _bmad/tea/testarch/knowledge/playwright-config.md
    - _bmad/tea/testarch/knowledge/playwright-cli.md
---

# NFR Assessment Report — Hexalith.EventStore

**Assessment Date:** 2026-04-18
**Assessor:** Murat (Test Architect)
**Scope:** Full system NFR assessment — 46 PRD NFRs × 8 ADR Quality Readiness categories
**Prior Assessment:** 2026-03-29 (archived at `archive/nfr-2026-03-29/`)

---

## Step 1 — Context & Knowledge Base Loaded

### Configuration

| Setting | Value |
|---|---|
| `test_artifacts` | `_bmad-output/test-artifacts` |
| `tea_browser_automation` | `auto` |
| `test_framework` | `playwright` |
| `risk_threshold` | `p1` |
| `communication_language` | English |

### Knowledge Fragments Loaded

**Core (always):**
- `adr-quality-readiness-checklist.md` — 8 categories, 29 criteria scoring framework
- `test-quality.md` — Test Definition of Done (deterministic, isolated, explicit, <300 lines, <1.5 min)

**Extended (NFR-relevant):**
- `nfr-criteria.md` — PASS/CONCERNS/FAIL matrix for Security/Performance/Reliability/Maintainability
- `error-handling.md` — Scoped exception handling, retry validation, telemetry, graceful degradation
- `ci-burn-in.md` — CI quality gates, burn-in loops, artifact policy
- `playwright-config.md` — Env switching, timeouts, artifacts (UI test framework context)
- `playwright-cli.md` — Token-efficient automation (browser-automation `auto` mode)

### Primary Artifacts Loaded

| Artifact | Lines | Purpose |
|---|---|---|
| `_bmad-output/planning-artifacts/prd.md` | 981 | 46 NFRs across 8 domains (NFR1-NFR46) |
| `_bmad-output/planning-artifacts/architecture.md` | 1195 | 5 SEC constraints, 17 enforcement rules, NFR coverage matrix |
| `_bmad-output/planning-artifacts/epics.md` | 1740 | Epic-level feature scope (21 epics) |

### NFR Inventory (from PRD)

| Domain | NFR Range | Count | Summary |
|---|---|---|---|
| Performance | NFR1-NFR8 | 8 | Command ≤50ms p99, E2E ≤200ms p99, append ≤10ms p99, rehydration ≤100ms for 1K events, ≥100 cmd/sec |
| Security | NFR9-NFR15 | 7 | TLS 1.2+, JWT validation, audit logging, no payload in logs, triple-layer tenant isolation, secrets mgmt |
| Scalability | NFR16-NFR20 | 5 | Horizontal scale, 10K aggregates/instance, 10 tenants, constant rehydration via snapshot, dynamic config |
| Reliability | NFR21-NFR26 | 6 | 99.9% availability, zero event loss, deterministic replay, no duplicate persistence, optimistic concurrency |
| Integration | NFR27-NFR32 | 6 | Backend portability (Redis/Postgres, RabbitMQ/ASB), OTLP, Aspire publishers |
| Rate Limiting | NFR33-NFR34 | 2 | Per-tenant 1K cmd/min, per-consumer 100 cmd/sec, 429+Retry-After |
| Query Pipeline | NFR35-NFR39 | 5 | ETag pre-check ≤5ms, cache hit ≤10ms, miss ≤200ms, SignalR ≤100ms, 1K query/sec |
| Admin Tooling | NFR40-NFR46 | 7 | Admin API ≤500ms read, UI ≤2s load, CLI ≤3s, MCP ≤1s, 10 concurrent users, RBAC |
| **Total** | **NFR1-NFR46** | **46** | |

### Architectural Inputs

- **SEC-1..SEC-5**: EventStore owns envelope metadata, tenant validation before state rehydration, tenant-scoped status queries, extension sanitization, no payload in logs
- **17 enforcement rules**: Includes never-log-payload (rule #5), write-once event keys (rule #11), advisory status writes (rule #12), 5s sidecar timeout (rule #14), mandatory snapshot config (rule #15), real Keycloak tokens for E2E (rule #16)
- **Architecture claims 32/32 NFR coverage** but the PRD evolved to 46 NFRs post-architecture (Query Pipeline NFR35-39 and Admin Tooling NFR40-46 added later) — this gap is itself an NFR traceability concern to assess

### Evidence Sources Identified (preliminary)

- **Code layer:** `src/` (9 projects), `samples/`
- **Test tiers:**
  - Tier 1 (unit): `tests/Hexalith.EventStore.{Contracts,Client,Sample,Testing,SignalR}.Tests`
  - Tier 2 (integration, needs DAPR): `tests/Hexalith.EventStore.Server.Tests`
  - Tier 3 (Aspire E2E): `tests/Hexalith.EventStore.IntegrationTests`
- **Test artifacts:** `_bmad-output/test-artifacts/traceability-report.md`, `test-review.md`, trace coverage matrix JSON
- **Recent sprint changes** (2026-04-01..2026-04-16): 20+ sprint change proposals touching auth, admin UI, Fluent UI v5, CI/CD container publishing, tenant mgmt — potential scope drift vs NFRs

### Confirmed — Ready to Proceed

- ✅ Implementation accessible (full repo checked out, net10.0)
- ✅ Evidence sources available (test projects, traceability, existing NFR baseline archived)
- ⚠️ **Gap flagged for later steps**: Architecture NFR coverage matrix references only 32 NFRs but PRD has 46. Query Pipeline (NFR35-39) and Admin Tooling (NFR40-46) need explicit coverage validation.

---

## Step 2 — NFR Categories & Thresholds

### 2.1 Selected Categories

The 8 ADR Quality Readiness categories, mapped to Hexalith PRD NFR domains:

| # | Category | PRD NFR Mapping |
|---|---|---|
| 1 | Testability & Automation | FR40-FR47 (three-tier testing), rule #16 (real Keycloak JWTs) |
| 2 | Test Data Strategy | NFR13 (tenant isolation), rule #5 (no payload in logs) |
| 3 | Scalability & Availability | NFR1-NFR8, NFR16-NFR20, NFR33-NFR34, NFR39 |
| 4 | Disaster Recovery | **GAP-14 (deferred)** — NFR21-NFR25 partial |
| 5 | Security | NFR9-NFR15, SEC-1..SEC-5 |
| 6 | Monitorability/Debuggability | NFR31, FR35-FR39, rule #9 (correlationId) |
| 7 | QoS/QoE | NFR1-NFR8, NFR35-NFR39, NFR40-NFR43, NFR33-NFR34 |
| 8 | Deployability | NFR32, FR40-FR47 (Aspire publishers) |

### 2.2 NFR Thresholds Matrix

#### Performance (NFR1-NFR8) — Source: PRD §Performance

| NFR | Threshold | Measurement |
|---|---|---|
| NFR1 | Command submission p99 ≤ 50ms | REST API → 202 Accepted under normal load |
| NFR2 | E2E command lifecycle p99 ≤ 200ms | API receipt → pub/sub event published |
| NFR3 | Event append p99 ≤ 10ms | Actor persist → state store ack |
| NFR4 | Actor cold activation p99 ≤ 50ms | Snapshot + tail events rehydrated |
| NFR5 | Pub/sub delivery p99 ≤ 50ms | Persistence → subscriber delivery ack |
| NFR6 | Full state reconstruction ≤ 100ms | From 1,000 events (no snapshot) |
| NFR7 | Concurrent throughput | ≥ 100 commands/sec per instance without latency breach |
| NFR8 | DAPR sidecar overhead p99 ≤ 2ms | Per building block call |

#### Query Pipeline (NFR35-NFR39) — Source: PRD §Query Pipeline Performance

| NFR | Threshold |
|---|---|
| NFR35 | ETag pre-check p99 ≤ 5ms (warm actor) |
| NFR36 | Query cache hit p99 ≤ 10ms |
| NFR37 | Query cache miss p99 ≤ 200ms |
| NFR38 | SignalR "changed" signal p99 ≤ 100ms |
| NFR39 | ≥ 1,000 concurrent queries/sec per instance |

#### Admin Tooling (NFR40-NFR46) — Source: PRD §Administration Tooling

| NFR | Threshold |
|---|---|
| NFR40 | Admin API p99: ≤ 500ms (read), ≤ 2s (write) |
| NFR41 | Admin Web UI initial load ≤ 2s; SignalR update ≤ 200ms |
| NFR42 | Admin CLI query results ≤ 3s (incl. .NET startup) |
| NFR43 | Admin MCP single-resource p99 ≤ 1s |
| NFR44 | 100% DAPR-abstracted data access (backend-agnostic) |
| NFR45 | ≥ 10 concurrent admin UI users without degradation |
| NFR46 | RBAC: read-only/operator/admin roles enforced |

#### Security (NFR9-NFR15 + SEC-1..SEC-5) — Source: PRD §Security + Architecture §Security-Critical

| NFR/SEC | Threshold |
|---|---|
| NFR9 | TLS 1.2+ mandatory (all API consumer ↔ Command API) |
| NFR10 | JWT validation (signature + expiry + issuer) on EVERY request pre-processing |
| NFR11 | Auth failures logged with source IP, tenant, command type — **JWT never logged** |
| NFR12 | Event payload NEVER logged (envelope metadata only) |
| NFR13 | Tenant isolation at 3 layers (actor identity + DAPR policies + command metadata) — single-layer failure must not compromise isolation |
| NFR14 | Zero secrets in code/config committed to source control |
| NFR15 | Service-to-service auth via DAPR access control policies |
| SEC-1 | EventStore owns all 11 envelope metadata fields |
| SEC-2 | Tenant validation BEFORE state rehydration |
| SEC-3 | Command status queries tenant-scoped (not correlationId-only) |
| SEC-4 | Extension metadata sanitized at API gateway (size, char, injection) |
| SEC-5 | Event payload data never appears in logs (framework-enforced) |

#### Scalability (NFR16-NFR20) — Source: PRD §Scalability

| NFR | Threshold |
|---|---|
| NFR16 | Horizontal scaling via replica addition + DAPR actor placement |
| NFR17 | ≥ 10,000 active aggregates per instance, latency within targets |
| NFR18 | ≥ 10 tenants per instance, no cross-tenant performance interference |
| NFR19 | State rehydration time constant regardless of total event count (snapshot + tail only) |
| NFR20 | New tenant/domain via DAPR config without restart/downtime |

#### Rate Limiting (NFR33-NFR34) — Source: PRD §Rate Limiting

| NFR | Threshold |
|---|---|
| NFR33 | Per-tenant: 1,000 cmd/min default (configurable); 429 + Retry-After on breach |
| NFR34 | Per-consumer: 100 cmd/sec default (configurable); 429 + Retry-After on breach |

#### Reliability (NFR21-NFR26) — Source: PRD §Reliability

| NFR | Threshold |
|---|---|
| NFR21 | 99.9%+ availability (HA DAPR control plane + multi-replica) |
| NFR22 | Zero event loss (state store crash, actor crash, pub/sub down, partition) |
| NFR23 | Deterministic replay from last checkpoint post state-store recovery — no manual steps |
| NFR24 | All events persisted during pub/sub outage delivered post-recovery (DAPR retry) |
| NFR25 | Actor crash between persist and publish → no duplicate persistence (checkpointed state machine) |
| NFR26 | Optimistic concurrency conflicts → HTTP 409 Conflict (never silent overwrite) |

#### Integration (NFR27-NFR32) — Source: PRD §Integration

| NFR | Threshold |
|---|---|
| NFR27 | Works on any DAPR state store (ETag + KV) — validated: Redis, PostgreSQL |
| NFR28 | Works on any DAPR pub/sub (CloudEvents 1.0 + at-least-once) — validated: RabbitMQ, Azure Service Bus |
| NFR29 | Backend swap = YAML-only; zero code/recompile/redeploy |
| NFR30 | Domain services invocable via DAPR service invocation (HTTP) — no framework lock-in |
| NFR31 | OpenTelemetry → any OTLP collector — validated: Aspire, Jaeger, Grafana/Tempo |
| NFR32 | Aspire-deployable to Docker Compose, Kubernetes, Azure Container Apps — no custom scripts |

#### Disaster Recovery — GAP-14 (deferred)

| Criterion | Threshold | Status |
|---|---|---|
| 4.1 RTO/RPO | **UNKNOWN** — not specified in PRD/architecture | → CONCERNS |
| 4.2 Failover (region/zone) | **UNKNOWN** — 99.9% availability implies some but mechanism undefined | → CONCERNS |
| 4.3 Backups (immutable + restore-tested) | **UNKNOWN** — GAP-14 defers to operational readiness | → CONCERNS |

#### Deployability — Source: Architecture + CLAUDE.md

| Criterion | Threshold |
|---|---|
| 8.1 Zero downtime | Aspire publishers targets K8s/ACA (rolling updates supported); blue-green/canary **not explicitly specified** |
| 8.2 Backward compatibility | Rule #11 (write-once event keys) + CRITICAL-1 (backward-compatible deserializers) — event-schema backward compat enforced |
| 8.3 Rollback | **UNKNOWN** — no automated rollback trigger specified in PRD |

### 2.3 Threshold Status Summary

| Status | Count | Categories |
|---|---|---|
| **Fully specified** | 37 NFRs | Performance, Query Pipeline, Admin Tooling, Security, Scalability, Rate Limiting, Reliability, Integration |
| **UNKNOWN** (→ CONCERNS) | 3 criteria | DR: RTO/RPO, Failover, Backup integrity |
| **UNKNOWN** (→ CONCERNS) | 2 criteria | Deployability: blue-green/canary, automated rollback |
| **Architecturally addressed** | 5 constraints | SEC-1..SEC-5 |

**Total measurable thresholds:** 46 NFRs + 5 SEC constraints = **51 measurable criteria**
**Thresholds UNKNOWN:** 5 criteria (all in DR + Deployability)

---

## Step 3 — Evidence Gathered

### 3.1 Test Inventory (by project, excluding bin/obj)

| Project | .cs files | Tier | Primary coverage |
|---|---|---|---|
| Hexalith.EventStore.Contracts.Tests | 23 | 1 | Aggregates, Commands, Events, Identity, Messages, Projections, Queries, Results, Validation |
| Hexalith.EventStore.Client.Tests | 14 | 1 | Aggregates, Commands, Conventions, Discovery, Handlers, Registration |
| Hexalith.EventStore.Testing.Tests | 10 | 1 | Assertions, Builders, Fakes |
| Hexalith.EventStore.Sample.Tests | 8 | 1 | Counter, Greeting, MultiTenant, Registration |
| Hexalith.EventStore.SignalR.Tests | 1 | 1 | SignalR registration (thin) |
| Hexalith.EventStore.Admin.Abstractions.Tests | 52 | 1 | Models, Storage, Streams, DAPR resiliency |
| Hexalith.EventStore.Admin.Cli.Tests | 50 | 1 | Client, Commands (incl. Snapshot), Formatting, Profiles |
| Hexalith.EventStore.Admin.Mcp.Tests | 31 | 1 | MCP tool contracts |
| Hexalith.EventStore.Admin.Server.Host.Tests | 2 | 1 | Host wiring |
| Hexalith.EventStore.Admin.Server.Tests | 58 | 1 | Authorization, Controllers, Services, IntegrationTests, OpenApi |
| Hexalith.EventStore.Admin.UI.Tests | 85 | 1 | Pages, Components, Layout, Services |
| Hexalith.EventStore.Server.Tests | **162** | 2 | Actors, Auth, Commands, DaprComponents, Events, Error, Health, Logging, Observability, Pipeline, Projections, Queries, Security, SignalR, Telemetry, Validation |
| Hexalith.EventStore.IntegrationTests | 56 | 3 | ContractTests (Chaos, InfrastructurePortability), EventStore (RateLimiting, JWT, Authorization, MultiTenantRouting, ConcurrencyConflict), Security (Keycloak E2E, DaprAccessControl, MultiTenantStorageIsolation), Serialization, Middleware |
| Hexalith.EventStore.Admin.UI.E2E | 6 | 3 | Admin UI end-to-end |
| **Total** | **558** | — | |

### 3.2 Evidence by NFR Domain

#### Performance (NFR1-NFR8, NFR35-NFR39) — **EVIDENCE: PARTIAL**

- **Present**: `ActorColdActivationTests`, `StateRehydrationBenchmarkTests`, `SnapshotCreationIntegrationTests`, `SnapshotRehydrationTests`, `AtLeastOnceDeliveryTests`, IntegrationTests latency assertions
- **Present (Query Pipeline)**: SignalR.Tests delivery timing, Server.Tests ETag/cache timing assertions
- **Missing**: No k6, NBomber, or BenchmarkDotNet-based sustained-load tests. No formal p99 lab. No CI-automated load benchmarks. Production load profile unknown.
- **Gap severity**: Latency thresholds (NFR1-NFR8, NFR35-NFR39) are **asserted within unit/integration tests with bounded timing** but NOT validated under the concurrent-load envelopes specified (≥100 cmd/sec, ≥1000 query/sec, 10K aggregates)

#### Security (NFR9-NFR15, SEC-1..SEC-5) — **EVIDENCE: STRONG**

- **Server.Tests/Security/** (11 files): AccessControlPolicy, DataPathIsolation, DomainServiceIsolation, ExtensionMetadataSanitizer, PayloadProtection, PubSubTopicIsolationEnforcement, SecretsProtection, SecurityAuditLogging, StorageKeyIsolation, SubscriptionScopingDocumentation, TenantInjectionPrevention
- **Server.Tests/Authorization/** (6 files): ActorRbacValidator, ActorTenantValidator, AuthorizationServiceUnavailable*, ClaimsRbacValidator, ClaimsTenantValidator
- **Server.Tests/Authentication/** (3 files): ConfigureJwtBearerOptions, EventStoreAuthenticationOptions, EventStoreClaimsTransformation
- **IntegrationTests/Security/** (8 files): KeycloakE2ESecurityTests, KeycloakE2ESmokeTests, DaprAccessControlE2ETests, CommandStatusIsolationTests, MultiTenantStorageIsolationTests (rule #16 satisfied: real Keycloak OIDC tokens)
- **Logging**: `PayloadProtectionTests` validates NFR12/SEC-5 at logging framework level
- **Not testable in-code**: NFR9 (TLS) — infrastructure concern; NFR14 (secrets hygiene) — CI secret-scanning responsibility

#### Reliability (NFR21-NFR26) — **EVIDENCE: STRONG**

- `ChaosResilienceTests` (IntegrationTests): state store crash, pub/sub outage, actor rebalance
- `PersistThenPublishResilienceTests`: crash between persist and publish (NFR25)
- `ActorStateMachineTests` + `StateMachineIntegrationTests`: checkpointed state machine (NFR23)
- `ActorConcurrencyConflictTests` + `ConcurrencyConflictExceptionHandlerTests` + `ConcurrencyConflictIntegrationTests`: optimistic concurrency → 409 (NFR26)
- `HealthChecks/` (6 files): DaprSidecar, StateStore, PubSub, ConfigStore + HealthCheckRegistration + WriteHealthCheckJsonResponse
- **Not testable in-code**: NFR21 (99.9% availability) — production SLO/topology concern

#### Scalability (NFR16-NFR20) — **EVIDENCE: PARTIAL**

- **Present**: `SnapshotBoundedRehydrationTests` (NFR19 FULL), `DynamicConfigurationTests` (NFR20 FULL), multi-replica integration tests (NFR16 PARTIAL)
- **Missing**: No 10K-aggregate volume test (NFR17 — NONE), no 10-tenant concurrency interference benchmark (NFR18 — PARTIAL)

#### Rate Limiting (NFR33-NFR34) — **EVIDENCE: STRONG**

- `PerTenantRateLimitingTests`, `PerConsumerRateLimitingTests`, `RateLimitingIntegrationTests` (IntegrationTests/EventStore/)
- 3 dedicated WebApplicationFactory helpers for rate-limiting scenarios

#### Integration (NFR27-NFR32) — **EVIDENCE: PARTIAL**

- `InfrastructurePortabilityTests` (IntegrationTests/ContractTests/)
- Default DAPR variant fully exercised in CI; Postgres/RabbitMQ/ASB variants configured but not CI-run
- `DomainServiceInvocationTests` (NFR30 FULL)
- OTLP Aspire validated; Jaeger/Tempo configuration present but not CI-exercised

#### Observability & Monitorability — **EVIDENCE: STRONG**

- `OpenTelemetryRegistrationTests` + Telemetry/ + Observability/ folders
- Health checks: sidecar + state store + pub/sub + config store
- Structured logging enforced via rule #5

#### Admin Tooling (NFR40-NFR46) — **EVIDENCE: PARTIAL**

- Admin.Server.Tests: RoleBasedAuthorization, Controllers, Services (architecture tests enforce DAPR-only paths — NFR44 FULL, NFR46 FULL)
- Admin.Cli.Tests: startup + exec tests (NFR42 FULL)
- Admin.UI.Tests: rendering assertions (NFR41 PARTIAL — no Lighthouse CI gate)
- **Missing**: No concurrent-UI load test (NFR45 — NONE)

#### Disaster Recovery — **EVIDENCE: ABSENT (GAP-14)**

- No RTO/RPO tests
- No multi-region failover drill
- No backup-restore automation
- Architecture GAP-14 explicitly defers to v2/v3 operational readiness

### 3.3 CI/CD Evidence

From `.github/workflows/ci.yml`:

| Stage | Scope | Status |
|---|---|---|
| `commitlint` | Conventional Commits enforcement | Required on PR |
| `build-and-test` → Build | `dotnet build --configuration Release` | 20-min timeout |
| `build-and-test` → Tier 1 | 11 test projects with `XPlat Code Coverage` | Required |
| `build-and-test` → Container validation | 6 projects built to tar (no push) | Required |
| `build-and-test` → CLI smoke test | Pack + install + `--help` verification | Required |
| `build-and-test` → Tier 2 | `Server.Tests` after `dapr init` | Required |
| `build-and-test` → Test Summary | Parse TRX, post to step summary | `if: always()` |
| `build-and-test` → Coverage Summary | Parse cobertura XML, overall + per-project | `if: always()` |
| `aspire-tests` (Tier 3) | `IntegrationTests` after `dapr init` | **`continue-on-error: true`** ⚠️ |

**Gaps**:
- Tier 3 failures do not fail the build (continue-on-error=true) — risk of silent regressions
- No coverage threshold enforcement (summary only, no gate)
- No jscpd duplication check
- No `npm audit` / OSS-Review-Toolkit / Snyk / `dotnet list package --vulnerable` step
- No performance benchmark job
- No DR drill or restore-test job

### 3.4 Previously Completed Traceability (2026-04-18)

The companion `traceability-report.md` (same date) provides per-NFR coverage mapping. Gate decision: **CONCERNS**. Key findings relevant to this NFR assessment:

- **FULL coverage**: NFR6, NFR10-13, NFR15, NFR19-20, NFR22-26, NFR30, NFR33-34, NFR42, NFR44, NFR46
- **PARTIAL coverage**: NFR1-5, NFR7 (perf latency — bounded assertions but no sustained load), NFR16, NFR18, NFR27-28, NFR31, NFR35-38, NFR40-41, NFR43
- **NONE coverage**: NFR17 (10K aggregates), NFR39 (1K concurrent queries/sec), NFR45 (10 concurrent admin users)
- **NOT-TESTABLE**: NFR8 (DAPR sidecar benchmark), NFR9 (TLS), NFR14 (secrets), NFR21 (availability SLO), NFR29 (YAML-only swap), NFR32 (Aspire publishers)

### 3.5 Evidence Gap Summary (→ CONCERNS in later assessment)

| Domain | Gap | Impact |
|---|---|---|
| Performance | No sustained-load benchmarks (k6/NBomber/BenchmarkDotNet absent) | All p99 targets empirically unvalidated under prescribed load envelopes |
| Scalability | NFR17 (10K aggregates), NFR45 (10 admin users) — zero coverage | Capacity ceilings unknown |
| Query Pipeline | NFR39 (1K queries/sec) — zero coverage | Query throughput ceiling unknown |
| Disaster Recovery | RTO/RPO undefined; no backup/restore drill; no multi-region failover | GAP-14 — unmitigated; GA blocker for regulated customers |
| Deployability | No automated rollback triggers; blue-green/canary not specified | CI smoke passes but prod rollback untested |
| CI quality gates | No coverage threshold, no vuln scan, Tier 3 continue-on-error | Regression drift possible |
| Integration variants | Postgres/RabbitMQ/ASB configured but not CI-exercised | Portability claim (NFR27-NFR28) rests on compilation, not execution |

---

## Step 4 — Domain Assessment (Subagent Mode, 4 parallel)

**Execution:** subagent × 4 parallel (auto mode, ~4 min elapsed vs ~12 min sequential).

### 4.1 Domain Risk Breakdown

| Domain | Risk | Gate | Findings (PASS / CONCERN / FAIL / NOT-TESTABLE) |
|---|---|---|---|
| Security | **LOW** | **PASS** | 10 PASS · 1 CONCERN · 0 FAIL · 1 NOT-TESTABLE (of 12) |
| Performance | **HIGH** | **CONCERNS** | 0 PASS · 15 CONCERN · 1 FAIL · 1 NOT-TESTABLE (of 17) |
| Reliability | **MEDIUM** | **CONCERNS** | 5 PASS · 1 CONCERN · 3 FAIL · 1 NOT-TESTABLE (of 10) |
| Scalability | **MEDIUM** | **CONCERNS** | 4 PASS · 4 CONCERN · 3 FAIL (of 11) |
| **OVERALL** | **HIGH** | **CONCERNS** | **19 PASS · 21 CONCERN · 7 FAIL · 3 NOT-TESTABLE** (of 50) |

Raw outputs: `nfr-security.json`, `nfr-performance.json`, `nfr-reliability.json`, `nfr-scalability.json`.

### 4.2 Aggregated Compliance Status

| Standard / SLA | Status | Source |
|---|---|---|
| OWASP Top 10 | **PASS** | Security |
| SOC2 | **PARTIAL** | Security (auth/audit logged; gitleaks CI gap) |
| GDPR | **PARTIAL** | Security (payload redaction strong; DR backup immutability undefined) |
| SLA 99.9% availability | **CONCERNS** | Reliability + Performance (99.9% implied; p99 envelope empirically unvalidated) |
| Zero data loss | **PASS** | Reliability |
| Load envelope validated (100 cmd/sec, 1K qps) | **FAIL** | Performance |
| Capacity envelope validated (10K aggregates, 10 tenants, 10 admin users) | **CONCERNS / FAIL** | Scalability |
| Disaster recovery (RTO/RPO/backup/failover) | **FAIL** | Reliability (GAP-14 deferred) |

### 4.3 Cross-Domain Risks

| # | Domains | Risk | Impact | Description |
|---|---|---|---|---|
| X1 | Performance + Scalability | **HIGH** | GA-blocker | No load-testing framework in repo. NFR7 (100 cmd/sec), NFR39 (1K qps), NFR17 (10K aggregates), NFR45 (10 admin users) are ALL unvalidated. A single k6/NBomber investment closes both domains' core capacity gaps. |
| X2 | Reliability + Scalability | **MEDIUM** | GA-blocker if DR sold | DR-Failover FAIL compounds with NFR16 CONCERN (single-replica Aspire, no HPA/KEDA manifests under `deploy/`). Topology-level resilience is unvalidated: if a replica fails, actor reassignment RTO is unmeasured. |
| X3 | Performance + Reliability | **MEDIUM** | Silent regressions | Tier-3 `aspire-tests` job is `continue-on-error: true` — chaos regressions (Reliability) and any future perf-gate regressions will not fail the build. |
| X4 | Performance + Admin-Tooling | **MEDIUM** | UX drift | NFR41 (UI LCP ≤ 2s, TTI) and NFR45 (10 concurrent users) have zero evidence. No Lighthouse CI + no concurrent-user Playwright. Admin UI is user-facing — perceived-perf regressions will not be caught pre-merge. |
| X5 | Security + Deployability | **LOW** | Hardening gap | NFR14 CONCERN (no gitleaks CI) + DAPR_TRUST_DOMAIN fallback default — both are belt-and-braces gaps that compound when deploying to a new cluster without runbook verification. |

### 4.4 Aggregated Priority Actions (P0/P1/P2)

**P0 — GA Blockers (must close before production cutover):**

1. **(Perf + Scal cross-cut)** Introduce load-testing framework at `perf/Hexalith.EventStore.LoadTests/` (k6 or NBomber). Scenarios: 100 cmd/sec sustained (NFR7), 1,000 qps sustained (NFR39), 10K active aggregates volume (NFR17). Wire into a perf-lab CI workflow emitting p99 percentiles per NFR.
2. **(Rel)** Define RTO/RPO for v1 GA **or** explicitly document the DR carve-out in the customer-facing SLA (closes GAP-14).
3. **(Rel)** Add backup immutability policy (WORM / object-lock) and a restore-integrity test (snapshot → restore → replay → hash compare).
4. **(Sec)** Add gitleaks or GitHub Advanced Security push-protection to `.github/workflows/ci.yml` (closes NFR14 CONCERN).

**P1 — Strongly recommended before GA:**

5. **(Rel)** Add DAPR control-plane failover chaos test (kill placement service, assert actor reassignment within target RTO).
6. **(Rel + Perf)** Remove `continue-on-error: true` from the `aspire-tests` CI job — or gate on a required subset of chaos tests — so reliability regressions fail the build.
7. **(Scal)** Add multi-replica Aspire test for NFR16 (2+ eventstore replicas + actor redistribution assertions). Publish reference HPA/KEDA manifests under `deploy/`.
8. **(Scal)** Extend `MultiTenantRoutingIntegrationTests` to 10+ concurrent tenants with parallel command dispatch (NFR18).
9. **(Perf)** Add BenchmarkDotNet project (`perf/Hexalith.EventStore.Benchmarks/`) with benchmarks for NFR3 (event append), NFR4 (cold activation), NFR6 (1K-event rehydration), NFR36/NFR37 (cache hit/miss) — statistically sound p50/p95/p99 output, main-branch regression tracking.
10. **(Perf)** Add Lighthouse CI job targeting `/admin` with LCP ≤ 2s / TTI ≤ 2s budgets (NFR41).
11. **(Perf + Scal)** Add Playwright concurrent-admin-UI test for NFR45 (10 parallel sessions measuring p95 page-load + SignalR fanout).

**P2 — Hardening (nice-to-have):**

12. **(Sec)** Document TLS 1.2+ at ingress + deploy-time smoke test rejecting TLS 1.0/1.1.
13. **(Sec)** Production runbook verifying `DAPR_TRUST_DOMAIN` + `DAPR_NAMESPACE` env vars per environment (NFR15 hardening).
14. **(Sec)** Token-revocation E2E test (Keycloak revocation propagation).
15. **(Rel)** Add CI job spinning up Jaeger/Tempo containers to verify OTLP spans arrive (close NFR31 PARTIAL).
16. **(Rel)** Extend `ActorStateMachineTests` to enumerate all 8 `CommandStatus` stages (audit completeness).
17. **(Perf)** Tighten `VersionFlagTests` process timeout from 5000ms to 3000ms per NFR42 target.
18. **(Perf)** Add DAPR sidecar Prometheus scraping + alert rule for p99 > 2ms (NFR8 ownership assigned to infra).
19. **(Scal)** Parameterize eventstore replica count at the Aspire AppHost level for k8s/aca publish targets.

### 4.5 Key Findings Highlights

**✅ Architectural Strengths (from subagent deep-reads):**

- **Framework-enforced payload redaction** — `EventEnvelope.ToString()` hard-codes `Payload = [REDACTED]`; `PayloadProtectionTests` does regex static-analysis on 4 hot-path source files (NFR12/SEC-5 proven at framework level, not convention).
- **Three-layer tenant isolation proven in isolation** — `StorageKeyIsolationTests.SharedStateManager_TenantPrefixedKeys_PreventCrossTenantRead` shows layer-2 key prefixing prevents cross-tenant reads even if layer-3 actor scoping is bypassed.
- **Tenant validator hardened** — rejects colons, dots, control chars, Cyrillic homoglyphs, fullwidth digits, URL-encoded colons, DEL chars, ≥65-char lengths (TenantInjectionPreventionTests).
- **Persist-then-publish state machine** — zero event loss under all tested failure modes; `UnpublishedEventsRecord` + drain reminder pattern; resume-from-EventsStored skips re-invoking domain (no duplicate persistence).
- **Real Keycloak OIDC** (rule #16) satisfied via `AspireTopology` fixture — not synthetic JWTs.
- **Rate limiting is rigorous** — per-tenant + per-consumer with chained limiter ordering, 429 + Retry-After in RFC 7231 delta-seconds format.

**⚠️ Non-obvious downgrades from subagent review (traceability FULL → NFR assessment CONCERN/FAIL):**

- **NFR6** (1K-event rehydration) — traceability rated FULL; actually a single-sample stopwatch against NSubstitute mock. No `StateRehydrationBenchmarkTests` file exists in the repo (grep-confirmed). → **CONCERN**.
- **NFR42** (CLI cold start ≤ 3s) — traceability rated FULL; actual assertion is `WaitForExit(5000)` (hang-detector, not p99 ≤ 3s). → **CONCERN**.
- **NFR39** (1K qps queries) — traceability rated NONE; per `nfr-criteria.md` "defined target + zero evidence = FAIL" (not CONCERN). → **FAIL**.
- **DR** — traceability didn't explicitly score; Admin.Cli "backup" commands are TENANT-LEVEL event-archive (auditability/export), NOT infrastructure DR. → all three DR criteria **FAIL**.

---

## Step 5 — Final Assessment Report

### Executive Summary

**Gate Decision: ⚠️ CONCERNS**
**Overall Risk: HIGH** (Performance domain)

Hexalith.EventStore exhibits **exceptional architectural quality** — security posture is defense-in-depth with framework-level payload redaction, triple-layer tenant isolation, and real Keycloak E2E validation; reliability primitives (persist-then-publish, checkpointed state machine, write-once event keys) are rigorously tested with chaos coverage proving zero event loss. However, **two capability gaps block GA**: (1) absence of any load-testing framework (k6/NBomber/BenchmarkDotNet/Artillery) means all 17 performance NFRs and 4 capacity envelopes (10K aggregates, 10 tenants, 100 cmd/sec, 1K qps, 10 admin users) are empirically unvalidated, and (2) disaster recovery is explicitly deferred (GAP-14) with no RTO/RPO, no failover drill, and no backup immutability — meaning `Admin.Cli` "backup" commands are tenant event-archive, not infrastructure DR. These gaps are tractable: a single perf-lab investment + a DR SLO definition close both.

**Assessment:** 19 PASS · 21 CONCERN · 7 FAIL · 3 NOT-TESTABLE (of 50 scored criteria)
**Blockers:** 4 P0 actions (load harness, RTO/RPO, backup integrity, gitleaks)
**High Priority Issues:** 7 P1 (chaos CI gate, multi-replica, concurrent tenant load, benchmarks, Lighthouse, UI load, failover drill)
**Recommendation:** **Do not cut v1 GA** until P0 actions complete or DR carve-out is contractually accepted. Security + reliability architectural foundations are ready; the empirical validation is not.

---

### Findings Summary — ADR Quality Readiness Checklist (8 categories, 29 criteria)

Mapping PRD NFRs + SEC constraints + subagent verdicts to the 29-criterion checklist:

| Category | Criteria Met | PASS | CONCERNS | FAIL | Overall Status |
|---|---|---|---|---|---|
| 1. Testability & Automation | 4/4 | 4 | 0 | 0 | ✅ PASS |
| 2. Test Data Strategy | 3/3 | 3 | 0 | 0 | ✅ PASS |
| 3. Scalability & Availability | 1/4 | 1 | 2 | 1 | ⚠️ CONCERNS |
| 4. Disaster Recovery | 0/3 | 0 | 0 | 3 | ❌ FAIL |
| 5. Security | 4/4 | 4 | 0 | 0 | ✅ PASS |
| 6. Monitorability/Debuggability/Manageability | 4/4 | 3 | 1 | 0 | ✅ PASS |
| 7. QoS & QoE | 0/4 | 0 | 4 | 0 | ⚠️ CONCERNS |
| 8. Deployability | 2/3 | 1 | 2 | 0 | ⚠️ CONCERNS |
| **Total** | **18/29 (62%)** | **16** | **9** | **4** | **⚠️ CONCERNS** |

**18/29 (62%) → Significant gaps** per scoring band.

### Category Detail

#### 1. Testability & Automation — ✅ PASS (4/4)

| Criterion | Status | Evidence |
|---|---|---|
| 1.1 Isolation (mock downstream deps) | ✅ | `Testing.Tests`, NSubstitute mocks across 558 test files |
| 1.2 Headless (API-accessible logic) | ✅ | REST Commands/Queries APIs; zero UI dependency for business logic |
| 1.3 State Control (seeding) | ✅ | `Testing` package with fixtures + Aspire testcontainer topology |
| 1.4 Sample Requests | ✅ | Samples project + OpenAPI + Admin.Cli.Tests exercise real payloads |

#### 2. Test Data Strategy — ✅ PASS (3/3)

| Criterion | Status | Evidence |
|---|---|---|
| 2.1 Segregation (multi-tenant) | ✅ | Tenant injection into ActorId + composite keys + KeycloakE2E cross-tenant 403 |
| 2.2 Generation (synthetic, no PII) | ✅ | Rule #5 + framework-level `ToString() = [REDACTED]`; test fixtures generate data |
| 2.3 Teardown | ✅ | `AspireTopology` fixture, write-once keys; idempotent reruns |

#### 3. Scalability & Availability — ⚠️ CONCERNS (1/4)

| Criterion | Status | Evidence |
|---|---|---|
| 3.1 Statelessness | ⚠️ | Actors are stateful by design; DAPR placement handles distribution (NFR16 CONCERN — no multi-replica test) |
| 3.2 Bottlenecks identified | ❌ | No load test identifies bottlenecks; NFR17 (10K aggregates) zero coverage |
| 3.3 SLA defined | ⚠️ | 99.9% defined but not validated (NFR21 NOT-TESTABLE; redundancy topology undocumented) |
| 3.4 Circuit breakers | ✅ | DAPR resiliency policies (rule #4); `ResiliencyConfigurationTests` validates config |

#### 4. Disaster Recovery — ❌ FAIL (0/3)

| Criterion | Status | Evidence |
|---|---|---|
| 4.1 RTO/RPO defined | ❌ | GAP-14 — not specified in PRD/architecture |
| 4.2 Failover automated/practiced | ❌ | No multi-region/AZ failover test; chaos is single-node |
| 4.3 Backups immutable + restore-tested | ❌ | No WORM policy, no restore-integrity test |

#### 5. Security — ✅ PASS (4/4)

| Criterion | Status | Evidence |
|---|---|---|
| 5.1 AuthN/AuthZ (OAuth/OIDC + RBAC) | ✅ | JWT validation with 1-min ClockSkew; real Keycloak E2E; 3-layer tenant isolation; admin RBAC 3-tier |
| 5.2 Encryption (at-rest + in-transit) | ✅ | TLS enforced at ingress (NFR9 infra); `RequireHttpsMetadata: true` in prod |
| 5.3 Secrets (no hardcoded) | ⚠️ | `SecretsProtectionTests` static analysis in Tier 1; **no gitleaks CI** (P0 action) |
| 5.4 Input validation | ✅ | `ExtensionMetadataSanitizer` — XSS, SQL, LDAP, path traversal, Unicode homoglyphs, control chars |

#### 6. Monitorability/Debuggability/Manageability — ✅ PASS (4/4)

| Criterion | Status | Evidence |
|---|---|---|
| 6.1 Tracing (W3C + correlationId) | ✅ | Rule #9: correlationId in every log + OpenTelemetry activity |
| 6.2 Logs (dynamic level, structured) | ✅ | Structured logging + `[LoggerMessage]` source generator; `PayloadProtectionTests` enforces hygiene |
| 6.3 Metrics (RED) | ✅ | OpenTelemetry → OTLP; `OpenTelemetryRegistrationTests` |
| 6.4 Config (externalized) | ⚠️ | DAPR config store + appsettings layering; NFR31 Jaeger/Tempo not CI-exercised |

#### 7. QoS & QoE — ⚠️ CONCERNS (0/4)

| Criterion | Status | Evidence |
|---|---|---|
| 7.1 Latency (P95/P99 targets) | ⚠️ | 17 NFRs with p99 targets; ONLY 2 tests compute true p99 (against mocks) |
| 7.2 Throttling (rate limiting) | ⚠️ | Rate-limiting mechanism is FULL (NFR33/34 PASS); but capacity-under-load untested |
| 7.3 Perceived performance (UI QoE) | ⚠️ | Admin UI component render tests only; no Lighthouse; no LCP/TTI budgets |
| 7.4 Degradation (friendly errors) | ⚠️ | ProblemDetails for all errors (rule #7); UX-DR10 suppresses info leaks; no end-user-facing UX load testing |

#### 8. Deployability — ⚠️ CONCERNS (2/3)

| Criterion | Status | Evidence |
|---|---|---|
| 8.1 Zero downtime (blue-green/canary) | ⚠️ | Aspire publish targets k8s/ACA (rolling supported); blue-green/canary NOT specified |
| 8.2 Backward compatibility | ✅ | Rule #11 (write-once event keys) + CRITICAL-1 (backward-compatible deserializers enforced) |
| 8.3 Automated rollback | ⚠️ | Health check probes present; rollback trigger NOT specified in PRD |

---

### Quick Wins (≤1 day each)

1. **Add gitleaks to CI** (Sec · P0 · 1h) — closes NFR14 CONCERN. Single GitHub Action + `.gitleaks.toml` allowlist for dev-only keys.
2. **Remove `continue-on-error: true`** from `aspire-tests` job (Rel + Perf · P1 · 0.5h) — immediately elevates chaos-test signal to gating.
3. **Tighten `VersionFlagTests` timeout** from 5000ms to 3000ms (Perf · P2 · 0.5h) — converts NFR42 hang-detector into real SLA assertion.
4. **Parameterize Aspire replica count** via `PUBLISH_TARGET`-aware `WithReplicas(...)` (Scal · P2 · 1d) — enables future multi-replica testing.
5. **Document TLS 1.2+ ingress requirement** + add staging smoke test (Sec · P2 · 0.5d) — closes NFR9 NOT-TESTABLE with a deploy-level check.

---

### Recommended Actions

#### Immediate — GA Blockers (P0)

1. **Build load-testing harness** — **P0 · 5-10d · Perf team**
   - Create `perf/Hexalith.EventStore.LoadTests/` (k6 or NBomber)
   - Scenarios: 100 cmd/sec (NFR7), 1K qps (NFR39), 10K aggregates (NFR17)
   - Wire into new `.github/workflows/perf-lab.yml` (nightly + on-demand)
   - Validation: p99 bands per NFR computed over ≥5-min steady state

2. **Define RTO/RPO for v1 GA (or explicit DR carve-out)** — **P0 · 2-3d · Product + Ops**
   - Decide: v1 SLA includes DR, or v1 documents "DR available in v2"
   - If v1 includes DR: add restore-integrity chaos test (snapshot → restore → replay → hash compare)

3. **Backup immutability policy + restore-integrity test** — **P0 · 3-5d · Ops**
   - Specify WORM mode (S3 Object Lock / Azure immutable blobs)
   - Add Tier-3 test that snapshots, restores to fresh cluster, and hash-compares event log

4. **Add gitleaks to CI** — **P0 · 1h · DevX**
   - `.github/workflows/ci.yml` new job with gitleaks-action
   - Enable GitHub Advanced Security push protection on the repo

#### Short-term — Next Milestone (P1)

5. **DAPR control-plane failover chaos test** — **P1 · 3-5d · Ops**
6. **Multi-replica Aspire test + reference HPA/KEDA manifests under `deploy/`** — **P1 · 3d · Platform**
7. **Extend multi-tenant test to 10+ concurrent tenants** — **P1 · 1d · QA**
8. **BenchmarkDotNet project** for NFR3/NFR4/NFR6/NFR36/NFR37 — **P1 · 3-5d · Perf**
9. **Lighthouse CI job** for Admin UI (NFR41) — **P1 · 1d · Frontend**
10. **Playwright concurrent-admin-UI test** for NFR45 — **P1 · 2d · QA**
11. **Remove `continue-on-error`** from Tier-3 chaos — **P1 · 0.5h · DevX**

#### Long-term — Backlog (P2)

12. **TLS enforcement at ingress** + deploy-time smoke test — **P2 · 0.5d · Ops**
13. **DAPR_TRUST_DOMAIN/NAMESPACE production runbook** — **P2 · 0.5d · Ops**
14. **Token revocation E2E test** (Keycloak revocation propagation) — **P2 · 2d · QA**
15. **Jaeger/Tempo CI smoke** (OTLP spans arrive) — **P2 · 1d · Observability**
16. **Extend ActorStateMachineTests** to all 8 CommandStatus stages — **P2 · 1d · Dev**
17. **DAPR sidecar Prometheus scraping + alert** (NFR8 infra ownership) — **P2 · 1d · Ops**
18. **Admin API p99 timing tests** (NFR40, NFR43) — **P2 · 2d · QA**
19. **Parameterize eventstore replica count** at Aspire AppHost level — **P2 · 1d · Platform**

---

### Monitoring Hooks (recommended)

**Performance:**
- [ ] **OTLP → Prometheus histograms** — `eventstore.command.submit.duration`, `eventstore.events.persist.duration`, `eventstore.pubsub.publish.duration`, `eventstore.query.etag.duration`, `eventstore.query.cache.hit.duration` — owner: Perf team, deadline: pre-GA
- [ ] **DAPR sidecar metrics scrape** — `dapr_http_server_latency_*` alert on p99 > 2ms (NFR8) — owner: Ops, deadline: pre-GA

**Security:**
- [ ] **Auth-failure log alerts** — aggregated by SourceIp + tenant (NFR11) — owner: Security, deadline: pre-GA
- [ ] **CommandStatus cross-tenant access attempt alert** (SEC-3 defense-in-depth) — owner: Security, deadline: post-GA

**Reliability:**
- [ ] **Health probe aggregation → uptime dashboard** (NFR21 SLO) — owner: Ops, deadline: pre-GA
- [ ] **UnpublishedEventsRecord depth metric + alert** — detects stuck pub/sub drain — owner: Ops, deadline: pre-GA

**Alerting Thresholds:**
- [ ] p99 command submit > 50ms → WARN, > 100ms → CRIT (NFR1)
- [ ] Event append p99 > 10ms → WARN, > 50ms → CRIT (NFR3)
- [ ] 429 rate per tenant > 5% sustained → tenant quota review (NFR33)

---

### Fail-Fast Mechanisms (already in place ✅ and recommended ⬜)

**Circuit Breakers (Reliability):**
- ✅ DAPR sidecar-level circuit breaker (rule #4) — validated by `ResiliencyConfigurationTests`

**Rate Limiting (Performance):**
- ✅ Per-tenant (NFR33) + Per-consumer (NFR34) with 429 + Retry-After — chained limiter ordering verified

**Validation Gates (Security):**
- ✅ JWT validation pre-pipeline (NFR10)
- ✅ Tenant validation pre-rehydration (SEC-2)
- ✅ Extension metadata sanitizer (SEC-4)

**Smoke Tests (Maintainability):**
- ✅ CLI `--help` smoke test in CI
- ✅ Container image build validation for 6 images
- ⬜ **New: Lighthouse UI budgets** (NFR41) — P1 action

---

### Evidence Gaps Checklist

| # | NFR / Gap | Owner | Deadline | Suggested Evidence |
|---|---|---|---|---|
| 1 | NFR1-NFR7 (command-path p99) | Perf team | pre-GA | k6 scenario: 100 cmd/sec × 10 min; p99 from `http_req_duration` |
| 2 | NFR35-NFR39 (query-path p99) | Perf team | pre-GA | k6 scenario: 1K qps × 10 min; p99 per endpoint |
| 3 | NFR17 (10K aggregate capacity) | Platform | pre-GA | Volume harness: activate 10K actors, measure memory + command latency |
| 4 | NFR18 (10 concurrent tenants) | QA | pre-GA | Extend `MultiTenantRoutingIntegrationTests` with `Parallel.ForEachAsync` |
| 5 | NFR45 (10 admin UI users) | QA | pre-GA | Playwright 10 parallel sessions + SignalR fanout |
| 6 | DR-RTO-RPO | Product + Ops | pre-GA | SLO definition OR carve-out doc |
| 7 | DR-Failover | Ops | pre-GA | Kill placement service → assert RTO |
| 8 | DR-Backups | Ops | pre-GA | WORM policy + restore-integrity test |
| 9 | NFR14 (secret scan in CI) | DevX | pre-GA | gitleaks in `ci.yml` |
| 10 | NFR8 (DAPR sidecar overhead) | Ops | post-GA | Prometheus scrape + alert |
| 11 | NFR41 (Admin UI LCP/TTI) | Frontend | pre-GA | Lighthouse CI budgets |
| 12 | NFR42 (CLI cold start real assertion) | Dev | post-GA | Per-subcommand 3s wall-clock test |
| 13 | NFR27-NFR28 (Postgres/RabbitMQ/ASB portability) | Platform | post-GA | CI matrix over DAPR component variants |

---

### Gate YAML Snippet

```yaml
nfr_assessment:
  date: "2026-04-18"
  feature_name: "Hexalith.EventStore — Full System"
  scope: "46 PRD NFRs + 5 SEC constraints"
  adr_checklist_score: "18/29"  # 62% — Significant gaps
  categories:
    testability_automation: "PASS"
    test_data_strategy: "PASS"
    scalability_availability: "CONCERNS"
    disaster_recovery: "FAIL"
    security: "PASS"
    monitorability: "PASS"
    qos_qoe: "CONCERNS"
    deployability: "CONCERNS"
  domain_risks:
    security: "LOW"
    performance: "HIGH"
    reliability: "MEDIUM"
    scalability: "MEDIUM"
  overall_risk: "HIGH"
  overall_status: "CONCERNS"
  findings:
    pass: 19
    concern: 21
    fail: 7
    not_testable: 3
    total: 50
  critical_issues: 4           # P0 blockers
  high_priority_issues: 7      # P1
  medium_priority_issues: 8    # P2
  blockers: true               # 4 P0 GA blockers
  quick_wins: 5
  evidence_gaps: 13
  compliance:
    owasp_top_10: "PASS"
    soc2: "PARTIAL"
    gdpr: "PARTIAL"
    sla_99_9: "CONCERNS"
    zero_data_loss: "PASS"
  recommendations:
    - "Introduce load-testing framework (k6 or NBomber) at perf/Hexalith.EventStore.LoadTests/ (P0)"
    - "Define RTO/RPO for v1 GA or explicit DR carve-out (P0)"
    - "Add backup immutability policy and restore-integrity test (P0)"
    - "Add gitleaks or GHAS push protection to CI (P0)"
    - "Remove continue-on-error from Tier-3 chaos tests (P1)"
  next_action: "Execute P0 actions. Re-run nfr-assess after perf-lab harness and DR decision land."
```

---

### Related Artifacts

- **PRD:** `_bmad-output/planning-artifacts/prd.md` (NFR1-NFR46)
- **Architecture:** `_bmad-output/planning-artifacts/architecture.md` (SEC-1..SEC-5, 17 enforcement rules, GAP-14)
- **Traceability (companion, same-day):** `_bmad-output/test-artifacts/traceability-report.md` (gate: CONCERNS)
- **Test inventory:** 558 test `.cs` files across 14 test projects, 3 tiers
- **Subagent JSON outputs (structured data):**
  - `_bmad-output/test-artifacts/nfr-security.json` — LOW risk, PASS
  - `_bmad-output/test-artifacts/nfr-performance.json` — HIGH risk, CONCERNS
  - `_bmad-output/test-artifacts/nfr-reliability.json` — MEDIUM risk, CONCERNS
  - `_bmad-output/test-artifacts/nfr-scalability.json` — MEDIUM risk, CONCERNS
- **Previous assessment (archived):** `_bmad-output/test-artifacts/archive/nfr-2026-03-29/`
- **CI workflow:** `.github/workflows/ci.yml` — Tier 1 (required) + Tier 2 (required) + Tier 3 Aspire (continue-on-error=true)

---

### Sign-Off

**NFR Assessment Status:**
- [ ] ✅ PASS — All NFRs meet requirements, ready for release
- [x] ⚠️ **CONCERNS** — NFRs have concerns, address before next release
- [ ] ❌ FAIL — Critical NFRs not met, BLOCKER for release

**Gate Status:** ⚠️ **CONCERNS**

**Critical Issues:** 4 (P0 GA blockers)
**High Priority Issues:** 7 (P1)
**Concerns:** 21 (largely empirical-evidence gaps for correctly-designed NFRs)
**Evidence Gaps:** 13

**Next Actions:**
- **Decision required:** Product + Ops commit to v1 DR scope (include or carve-out) — this is the #1 decision gating GA
- **Engineering plan:** Allocate 2-3 sprints to close P0 load-testing harness + P0 DR decisions + P0 gitleaks
- **Re-run `*nfr-assess`** after P0 landed to convert HIGH → MEDIUM overall risk
- **Consider `*trace`** (traceability refresh) after any P0 lands, to re-validate NFR coverage
- **Consider `*gate`** release-gate workflow after re-assessment passes

**Generated:** 2026-04-18
**Assessor:** Murat (Test Architect) via BMad `testarch-nfr` workflow v4.0
**Execution:** 4 parallel subagents (Security, Performance, Reliability, Scalability) + aggregation

---

<!-- Powered by BMAD-CORE™ -->
