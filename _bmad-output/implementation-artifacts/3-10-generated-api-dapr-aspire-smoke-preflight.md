---
created: 2026-07-15
story_id: "3.10"
story_key: 3-10-generated-api-dapr-aspire-smoke-preflight
status: done
supersedes: 3-8-generated-api-dapr-aspire-smoke-preflight.md
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 3.10: Generated API DAPR/Aspire Smoke Preflight

Status: done

## Reissue Decision

This is the active identity for completed historical Story 3.8. Its implementation,
commands, live-topology results, and review record remain in the superseded file.

## Strengthened Evidence Rule

The preflight remains read-only by default, classifies environment/topology/sidecar failures
separately from product failures, discovers Sample/EventStore/Redis and optional Tenants
resources, and emits support-safe diagnostics. Any successful generated-API smoke evidence
must include both a persisted event and persisted read-model/query-state read-back. HTTP
status, ETag/`304`, process health, or mock state alone cannot close the state-evidence gate.
This clarification does not reopen the completed tooling implementation.
