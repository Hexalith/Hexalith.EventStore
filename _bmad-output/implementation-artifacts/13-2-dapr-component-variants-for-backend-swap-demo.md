# Story 13.2: DAPR Component Variants for Backend Swap Demo

Status: review

## Story

As a developer following the first domain service tutorial,
I want pre-configured DAPR component YAML files for Redis and PostgreSQL,
so that I can experience a backend swap with zero code changes.

## Acceptance Criteria

1. `samples/dapr-components/redis/` contains DAPR component YAML files configured for Redis (default local dev backend)
2. `samples/dapr-components/postgresql/` contains DAPR component YAML files configured with PostgreSQL state store (pub/sub remains Redis — only the state store swaps)
3. The backend swap is achievable by pointing DAPR to a different component directory — no application code changes
4. Each YAML file includes inline comments explaining every field per the code example patterns in the architecture document

## Exact File Manifest

The dev agent must create exactly these files:

| Action | File | Purpose |
| ------ | ---- | ------- |
| CREATE | `samples/dapr-components/redis/statestore.yaml` | Redis state store component for local dev |
| CREATE | `samples/dapr-components/redis/pubsub.yaml` | Redis pub/sub component for local dev |
| CREATE | `samples/dapr-components/postgresql/statestore.yaml` | PostgreSQL state store component for backend swap |
| CREATE | `samples/dapr-components/postgresql/pubsub.yaml` | PostgreSQL pub/sub component (Redis — pub/sub backend unchanged in swap) |

No other source/configuration files. Do NOT modify existing files in `src/`, `deploy/`, `tests/`, or `samples/Hexalith.EventStore.Sample/`. BMAD workflow tracking artifacts under `_bmad-output/implementation-artifacts/` may be updated automatically by workflow execution.

## Tasks / Subtasks

- [x] Task 1: Create `samples/dapr-components/redis/` directory with component YAML files (AC: #1, #4)
  - [x] 1.1 Create `samples/dapr-components/redis/statestore.yaml` — Redis state store with inline field comments
  - [x] 1.2 Create `samples/dapr-components/redis/pubsub.yaml` — Redis pub/sub with inline field comments
  - [x] 1.3 Verify YAML validity (valid DAPR component schema)
- [x] Task 2: Create `samples/dapr-components/postgresql/` directory with component YAML files (AC: #2, #4)
  - [x] 2.1 Create `samples/dapr-components/postgresql/statestore.yaml` — PostgreSQL state store with inline field comments
  - [x] 2.2 Create `samples/dapr-components/postgresql/pubsub.yaml` — Redis pub/sub (same as redis/ variant — see Dev Notes)
  - [x] 2.3 Verify YAML validity (valid DAPR component schema)
- [x] Task 3: Validate backend swap flow (AC: #3)
  - [x] 3.1 Verify both directories are structurally identical (same filenames, same component names)
  - [x] 3.2 Verify only `statestore.yaml` differs between redis/ and postgresql/ (pub/sub stays Redis in both)
  - [x] 3.3 Verify both directories use identical component names (`statestore`, `pubsub`) — this is what makes the swap zero-code-change

## Dev Notes

### CRITICAL: Purpose and Scope

These YAML files are **documentation assets**, not runtime infrastructure. They demonstrate FR9 (backend swap with zero code changes) for the first domain service tutorial. The actual AppHost development topology uses Aspire-generated DAPR configuration (see `src/Hexalith.EventStore.AppHost/DaprComponents/`).

**These files are NOT used by Aspire.** They exist so a developer reading the tutorial can:
1. See a complete, self-contained Redis DAPR config
2. See the same config swapped to PostgreSQL
3. Understand that only the state store type changes — no app code changes

### CRITICAL: Both Files Required in Each Directory

DAPR loads ALL component YAML files from a `--resources-dir` directory. If `pubsub.yaml` is missing from `postgresql/`, DAPR won't find a pub/sub component and event publishing will fail. Both `statestore.yaml` AND `pubsub.yaml` must exist in each variant directory for a working topology.

### CRITICAL: What Changes Between Redis and PostgreSQL

**ONLY the state store component differs.** The pub/sub component stays Redis in both variants because:
- FR9 demonstrates a *state store* backend swap (event persistence)
- Pub/sub backend swap is a separate concern (not in scope for this demo)
- Keeping pub/sub identical makes the swap demo clearer — exactly ONE file changes

| Component | `redis/` Variant | `postgresql/` Variant | Changed? |
| --------- | ---------------- | -------------------- | -------- |
| `statestore.yaml` | `state.redis` type | `state.postgresql` type | YES |
| `pubsub.yaml` | `pubsub.redis` type | `pubsub.redis` type | NO |

### CRITICAL: Component Name Must Be "statestore"

Both Redis and PostgreSQL state store YAML files MUST use `metadata.name: statestore`. This is the component name the application references. If the name differs between variants, the swap would require code changes (violating FR9).

### CRITICAL: Simplified vs Production YAML

These sample YAML files must be **simplified** compared to the production templates in `deploy/dapr/`:
- **NO** environment variable substitution (`{env:...}`) — use hardcoded localhost values
- **NO** three-layer pub/sub scoping complexity — simple single-scope setup
- **NO** external subscriber configurations — just `commandapi`
- **YES** inline comments explaining every field — this is the primary educational value
- **YES** `actorStateStore: "true"` on state store (required for aggregate actors)

The goal is **developer comprehension**, not production readiness. Production DAPR configs already exist in `deploy/dapr/`.

### Exact YAML Content — Redis State Store

`samples/dapr-components/redis/statestore.yaml`:

```yaml
# DAPR State Store — Redis (Default Local Development)
# =====================================================
# Stores aggregate state, event streams, snapshots, and command status.
# This is the default backend for local development with Docker.
#
# Backend swap demo: To switch to PostgreSQL, run DAPR with the
# postgresql/ resources directory instead:
#   dapr run --resources-dir ./samples/dapr-components/postgresql/ -- dotnet run
# No application code changes needed.
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  # Component name referenced by the application. Must match across all
  # backend variants to enable zero-code-change swaps.
  name: statestore
spec:
  # Redis state store component type.
  # Full reference: https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-redis/
  type: state.redis
  version: v1
  metadata:
    # Redis server address. Default Docker Desktop Redis runs on localhost:6379.
    - name: redisHost
      value: "localhost:6379"
    # Redis password. Empty string for default local Redis without auth.
    - name: redisPassword
      value: ""
    # Required for DAPR actor state management. The EventStore uses DAPR actors
    # to process commands, so the state store must support actor state.
    - name: actorStateStore
      value: "true"
# Scope to commandapi only. Domain services (e.g., the sample Counter service)
# never access the state store directly — they receive commands via service
# invocation and return events. This enforces the zero-trust architecture.
scopes:
  - commandapi
```

### Exact YAML Content — Redis Pub/Sub

`samples/dapr-components/redis/pubsub.yaml`:

```yaml
# DAPR Pub/Sub — Redis Streams (Local Development)
# ==================================================
# Distributes domain events to subscribers after command processing.
# Topic pattern: {tenant}.{domain}.events (e.g., "sample.counter.events")
#
# This component is identical in both redis/ and postgresql/ variants.
# The backend swap demo changes only the state store, not the message broker.
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  # Component name referenced by the application. Must match across all variants.
  name: pubsub
spec:
  # Redis Streams pub/sub component type.
  # Full reference: https://docs.dapr.io/reference/components-reference/supported-pubsub/setup-redis-pubsub/
  type: pubsub.redis
  version: v1
  metadata:
    # Redis server address. Same Redis instance as the state store in local dev.
    - name: redisHost
      value: "localhost:6379"
    # Redis password. Empty string for default local Redis without auth.
    - name: redisPassword
      value: ""
    # Enable dead-letter topic for messages that exhaust delivery retries.
    - name: enableDeadLetter
      value: "true"
    # Default dead-letter topic name. Per-subscription dead-letter routing
    # (deadletter.{tenant}.{domain}.events) is configured separately.
    - name: deadLetterTopic
      value: "deadletter"
# Scope to commandapi only. Domain services have zero pub/sub access.
scopes:
  - commandapi
```

### Exact YAML Content — PostgreSQL State Store

`samples/dapr-components/postgresql/statestore.yaml`:

```yaml
# DAPR State Store — PostgreSQL (Backend Swap Demo)
# ===================================================
# Stores aggregate state, event streams, snapshots, and command status.
# This variant demonstrates FR9: infrastructure backend swap with zero
# code changes. The application code is identical — only this YAML differs.
#
# Prerequisites: A PostgreSQL instance running on localhost:5432.
# Quick start with Docker:
#   docker run -d --name dapr-postgres -p 5432:5432 \
#     -e POSTGRES_PASSWORD=dapr-demo -e POSTGRES_DB=daprstate \
#     postgres:16-alpine
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  # Component name MUST be "statestore" — same as the Redis variant.
  # This is what makes the swap zero-code-change: the application references
  # "statestore" regardless of which backend implements it.
  name: statestore
spec:
  # PostgreSQL state store component type.
  # Full reference: https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql/
  type: state.postgresql
  version: v1
  metadata:
    # PostgreSQL connection string (libpq format — space-separated, NOT semicolons).
    # Format: host=<host> port=<port> user=<user> password=<pass> dbname=<db> sslmode=disable
    # For local development, sslmode=disable avoids TLS certificate setup.
    - name: connectionString
      value: "host=localhost port=5432 user=postgres password=dapr-demo dbname=daprstate sslmode=disable"
    # Required for DAPR actor state management — same requirement as Redis.
    - name: actorStateStore
      value: "true"
    # DAPR creates a default table named "state" in the target database.
    # Uncomment to use a custom table name:
    # - name: tableName
    #   value: "daprstate"
# Scope to commandapi only — same security posture as Redis variant.
scopes:
  - commandapi
```

### Exact YAML Content — PostgreSQL Pub/Sub (Same as Redis)

`samples/dapr-components/postgresql/pubsub.yaml`:

This file is **identical** to `samples/dapr-components/redis/pubsub.yaml`. Copy it verbatim. Only the header comment changes slightly:

```yaml
# DAPR Pub/Sub — Redis Streams (Local Development)
# ==================================================
# Distributes domain events to subscribers after command processing.
# Topic pattern: {tenant}.{domain}.events (e.g., "sample.counter.events")
#
# NOTE: This file is identical to the redis/ variant. The backend swap demo
# changes only the state store (statestore.yaml), not the message broker.
# In production, you might also swap pub/sub (e.g., to RabbitMQ or Kafka),
# but that is a separate concern demonstrated in deploy/dapr/.
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  # Component name referenced by the application. Must match across all variants.
  name: pubsub
spec:
  # Redis Streams pub/sub component type.
  # Full reference: https://docs.dapr.io/reference/components-reference/supported-pubsub/setup-redis-pubsub/
  type: pubsub.redis
  version: v1
  metadata:
    # Redis server address. Same Redis instance as the state store in local dev.
    - name: redisHost
      value: "localhost:6379"
    # Redis password. Empty string for default local Redis without auth.
    - name: redisPassword
      value: ""
    # Enable dead-letter topic for messages that exhaust delivery retries.
    - name: enableDeadLetter
      value: "true"
    # Default dead-letter topic name. Per-subscription dead-letter routing
    # (deadletter.{tenant}.{domain}.events) is configured separately.
    - name: deadLetterTopic
      value: "deadletter"
# Scope to commandapi only. Domain services have zero pub/sub access.
scopes:
  - commandapi
```

### Project Structure Notes

- **Location:** `samples/dapr-components/` is a new directory at the same level as `samples/Hexalith.EventStore.Sample/`
- **Alignment with D2:** Architecture Decision D2 specifies `samples/dapr-components/redis/` and `samples/dapr-components/postgresql/` as the locations for backend variant YAML files
- **Not in .slnx:** These are YAML documentation assets, not .NET projects — do NOT add to solution file
- **Not in CI:** These YAML files are not validated by CI (no DAPR runtime in docs-validation pipeline). YAML schema correctness is verified manually during review.

### Relationship to Existing DAPR Components

| Location | Purpose | Used By |
| -------- | ------- | ------- |
| `src/Hexalith.EventStore.AppHost/DaprComponents/` | Aspire local dev topology | Aspire AppHost (runtime) |
| `deploy/dapr/` | Production backend templates | Kubernetes/Azure deployment |
| `samples/dapr-components/` (NEW) | Documentation demo — backend swap | Tutorial readers (educational) |

The new files are **simplified, educational versions** of the production configs. They share the same component names and structure but use hardcoded localhost values and omit advanced scoping.

### DO NOT

- Do NOT modify existing YAML files in `src/Hexalith.EventStore.AppHost/DaprComponents/`
- Do NOT modify existing YAML files in `deploy/dapr/`
- Do NOT add these files to the `.slnx` solution
- Do NOT add `{env:...}` variable substitution — use hardcoded localhost values
- Do NOT add three-layer pub/sub scoping (publishingScopes, subscriptionScopes) — keep it simple
- Do NOT add external subscriber scopes (example-subscriber, ops-monitor) — `commandapi` only
- Do NOT add resiliency or access control YAML to `samples/dapr-components/` — out of scope for FR9 demo
- Do NOT change the pub/sub backend in the postgresql/ variant — only state store changes
- Do NOT add configstore.yaml — configuration store is not relevant to the backend swap demo
- Do NOT create README.md, Docker Compose files, shell scripts, or any automation in `samples/dapr-components/`
- Do NOT modify `.gitignore`, CI workflows (`.github/workflows/`), or `Hexalith.EventStore.slnx`

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Epic 6 / Story 6.2]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md, FR9 — backend swap with zero code changes]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md, D2 — samples/dapr-components/ structure]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml — Redis state store reference]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml — Redis pub/sub reference]
- [Source: deploy/dapr/statestore-postgresql.yaml — PostgreSQL state store reference]
- [Source: _bmad-output/implementation-artifacts/13-1-sample-integration-test-project.md — previous story patterns]

### Previous Story & Git Intelligence

- Story 13-1 created `samples/Hexalith.EventStore.Sample.Tests/` in `samples/` folder (commit `fba3ddb`) — confirms `samples/` is the correct parent for new demo assets
- YAML files are not built/tested by CI (`docs-validation.yml` only covers .NET projects) — correctness verified by code review

### Verification Criteria (for Code Reviewer)

Since these YAML files have no CI validation, the reviewer must verify:

1. **Schema correctness:** Each file starts with `apiVersion: dapr.io/v1alpha1`, has `kind: Component`, and valid `metadata`/`spec` structure
2. **Component name parity:** Both `redis/statestore.yaml` and `postgresql/statestore.yaml` use `metadata.name: statestore`; both `pubsub.yaml` files use `metadata.name: pubsub`
3. **Only statestore differs:** `diff samples/dapr-components/redis/pubsub.yaml samples/dapr-components/postgresql/pubsub.yaml` should show only header comment differences
4. **PostgreSQL connection format:** Uses libpq space-separated format (`host=localhost port=5432 user=postgres ...`), NOT semicolons
5. **Inline comments:** Every metadata field has an explanatory comment above it
6. **No advanced scoping:** No `publishingScopes`, `subscriptionScopes`, or external subscriber app-ids — only `commandapi` in scopes
7. **No env vars:** No `{env:...}` patterns — hardcoded localhost values only

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

None — clean implementation with no errors or retries.

### Completion Notes List

- Created 4 DAPR component YAML files as exact documentation assets for the backend swap demo (FR9)
- Redis variant: `statestore.yaml` (state.redis) and `pubsub.yaml` (pubsub.redis) in `samples/dapr-components/redis/`
- PostgreSQL variant: `statestore.yaml` (state.postgresql) and `pubsub.yaml` (pubsub.redis — identical to redis variant) in `samples/dapr-components/postgresql/`
- All files use simplified, hardcoded localhost values with comprehensive inline comments for educational purposes
- Validated: YAML schema correctness, component name parity (`statestore`/`pubsub` across both variants), only statestore type differs between variants
- No .NET code changes — these are YAML documentation assets only
- Full Tier 1 regression suite passed (465 tests, 0 failures)

### Implementation Plan

Straightforward file creation following exact YAML content specified in story Dev Notes. No architectural decisions needed — content was fully specified.

### File List

- NEW: `samples/dapr-components/redis/statestore.yaml` — Redis state store component (state.redis)
- NEW: `samples/dapr-components/redis/pubsub.yaml` — Redis pub/sub component (pubsub.redis)
- NEW: `samples/dapr-components/postgresql/statestore.yaml` — PostgreSQL state store component (state.postgresql)
- NEW: `samples/dapr-components/postgresql/pubsub.yaml` — Redis pub/sub component (pubsub.redis, identical to redis variant)

## Change Log

- 2026-03-02: Created 4 DAPR component YAML files for Redis and PostgreSQL backend swap demo (Story 13-2)
