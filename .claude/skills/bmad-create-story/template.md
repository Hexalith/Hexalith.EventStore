# Story {{epic_num}}.{{story_num}}: {{story_title}}

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a {{role}},
I want {{action}},
so that {{benefit}}.

## Acceptance Criteria

1. [Add acceptance criteria from epics/PRD]

## Tasks / Subtasks

- [ ] Task 1 (AC: #)
  - [ ] Subtask 1.1
- [ ] Task 2 (AC: #)
  - [ ] Subtask 2.1

## Dev Notes

- Relevant architecture patterns and constraints
- Source tree components to touch
- Testing standards summary

### Testing Standards (project-wide rules — apply to every story)

- **Tier 1 (Unit):** xUnit 2.9.3 + Shouldly + NSubstitute. No DAPR runtime, no Docker.
- **Tier 2 / Tier 3 (Integration) — REQUIRED end-state inspection:** If the story creates or modifies Tier 2 (`Server.Tests`) or Tier 3 (`IntegrationTests`) tests, each test MUST inspect state-store end-state (e.g., Redis key contents, persisted `EventEnvelope`, CloudEvent body, advisory status record). Asserting only API return codes, mock call counts, or pub/sub call invocations is forbidden — that is an API smoke test, not an integration test. *Reference:* Epic 2 retro R2-A6; precedent fixes in Story 2.1 (`CommandRoutingIntegrationTests` missing `messageId`) and Story 2.2 (persistence integration test rewrote to inspect Redis directly).
- **ID validation:** Any controller / validator handling `messageId`, `correlationId`, `aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse` on these fields is forbidden. *Reference:* Epic 2 retro R2-A7; precedent fix in Story 2.4 `CommandStatusController`.

### Project Structure Notes

- Alignment with unified project structure (paths, modules, naming)
- Detected conflicts or variances (with rationale)

### References

- Cite all technical details with source paths and sections, e.g. [Source: docs/<file>.md#Section]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
