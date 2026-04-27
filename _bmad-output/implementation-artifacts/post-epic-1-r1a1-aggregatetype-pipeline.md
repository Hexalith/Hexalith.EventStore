# Story Post-Epic-1 R1-A1: Thread Real AggregateType Through Persistence Pipeline

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform maintainer,
I want the real aggregate type threaded through the persistence boundary,
so that persisted event envelopes carry correct `AggregateType` metadata instead of the bounded-context domain name.

## Acceptance Criteria

1. `IEventPersister.PersistEventsAsync` accepts a new `string aggregateType` parameter and rejects null, empty, or whitespace values at the boundary.

2. `AggregateActor` passes the registered aggregate type it already knows at runtime into `PersistEventsAsync`; `EventPersister` and `FakeEventPersister` use that parameter instead of `identity.Domain`.

3. Persisted `EventEnvelope.AggregateType` equals the registered aggregate type (for example `counter`), not the domain/bounded context (for example `counter-domain`).

4. A Tier 1 test proves `EventPersister` populates `AggregateType` from the new parameter rather than `identity.Domain`.

5. A Tier 2/integration test proves events persisted through the aggregate actor carry the registered aggregate type end-to-end.

6. Existing callers and tests across Server, Testing, and Integration projects are updated to the new signature using named arguments where practical, and the affected build/test suites stay green.

## Tasks / Subtasks

- [ ] Task 1: Extend the persistence contract (AC: #1)
  - [ ] 1.1 Update `src/Hexalith.EventStore.Server/Events/IEventPersister.cs` to add `string aggregateType` between `AggregateIdentity identity` and `CommandEnvelope command`
  - [ ] 1.2 Update `src/Hexalith.EventStore.Server/Events/EventPersister.cs` to validate `aggregateType` and use it when building `EventEnvelope`
  - [ ] 1.3 Update `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs` to match the new signature and envelope mapping

- [ ] Task 2: Thread the real value from the actor (AC: #2, #3)
  - [ ] 2.1 Update `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` so the `PersistEventsAsync` call passes the registered aggregate type rather than relying on the persister to derive it
  - [ ] 2.2 Confirm the aggregate type source is the actor-registration/convention name already used by the actor runtime, not a new ad-hoc derivation
  - [ ] 2.3 Verify no remaining persistence path still maps `AggregateType` from `identity.Domain`

- [ ] Task 3: Update direct callers and helpers (AC: #6)
  - [ ] 3.1 Update `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs` call sites
  - [ ] 3.2 Update logging/security tests that call `PersistEventsAsync` directly:
        `tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs`,
        `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`,
        `tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs`,
        `tests/Hexalith.EventStore.Server.Tests/Logging/CausationIdLoggingTests.cs`
  - [ ] 3.3 Update integration/security callers:
        `tests/Hexalith.EventStore.IntegrationTests/Events/EventPersistenceIntegrationTests.cs`,
        `tests/Hexalith.EventStore.IntegrationTests/Security/MultiTenantStorageIsolationTests.cs`
  - [ ] 3.4 Grep for remaining `PersistEventsAsync(` call sites and update any missed usages

- [ ] Task 4: Add regression coverage (AC: #4, #5)
  - [ ] 4.1 Add `EventPersisterTests.PersistEventsAsync_PopulatesAggregateTypeFromParameter_NotFromDomain`
  - [ ] 4.2 Add or extend an aggregate-actor integration test proving a persisted event from the Counter sample carries `AggregateType == "counter"`
  - [ ] 4.3 Assert the negative case implicitly: the persisted envelope must not contain `identity.Domain` in `AggregateType`

- [ ] Task 5: Validate the change set (AC: #6)
  - [ ] 5.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`
  - [ ] 5.2 Run targeted Tier 1 suites covering direct callers
  - [ ] 5.3 Run targeted Tier 2/integration coverage for actor persistence if the environment is available

## Dev Notes

### Scope Summary

This is a focused architectural bug-fix story. The architecture already distinguishes **Domain** from **AggregateType**; the implementation drift is in the persistence seam. The story should correct the seam, update all direct callers, and lock the behavior with regression tests.

### Why This Story Exists

- Story 1.1 deliberately added `AggregateType` to event metadata as a field distinct from `Domain`.
- Story 1.1 also left a deferred review item: `AggregateType: "unknown"` / derived-placeholder behavior needed a later pipeline change.
- Epic 1 retrospective elevated that gap to **R1-A1** because persisted events currently record semantically wrong metadata on every write.

### Current Code State

Relevant current touchpoints:

- `src/Hexalith.EventStore.Server/Events/IEventPersister.cs`
  - Current signature lacks `aggregateType`
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`
  - Current envelope mapping sets `AggregateType` from `identity.Domain`
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs`
  - Mirrors the same incorrect mapping
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
  - Current `PersistEventsAsync` call site does not pass aggregate type

### Semantic Guardrail: Domain vs AggregateType

- `Domain` is the bounded context / service namespace, for example `counter-domain`
- `AggregateType` is the specific aggregate resource name, for example `counter`
- They are intentionally different fields and must remain different in persisted metadata

Do not "simplify" them back into one concept. This story exists because that simplification already caused incorrect data.

### Architecture Constraints

- `architecture.md` already supports this change; no architectural redesign is needed
- Rule 17 applies: convention-derived resource names use kebab-case and the actor already has the registered aggregate type available
- SEC-1 still holds: EventStore owns metadata enrichment, so the actor/persister boundary is the correct place to supply this value
- This is pre-release code; no migration or compatibility shim is required for existing persisted development data

### Implementation Guidance

- Prefer named arguments at updated `PersistEventsAsync` call sites to avoid swap mistakes on the expanded signature
- Keep the `aggregateType` parameter explicit; do not hide it behind ambient context or a second derivation path
- Validation should fail fast on null/empty/whitespace aggregate types
- Avoid unrelated refactors in actor persistence or metadata composition while touching these files

### Testing Guidance

Targeted coverage should prove both the seam and the end-to-end behavior:

- Tier 1: direct `EventPersister` test asserting `AggregateType` comes from the parameter
- Tier 2/integration: aggregate actor persists a real event whose envelope reports the registered aggregate type
- Update existing direct-caller tests rather than cloning test scaffolding where possible

### Previous Story Intelligence

From [`_bmad-output/implementation-artifacts/1-1-core-identity-and-event-envelope.md`](D:\Hexalith.EventStore\_bmad-output\implementation-artifacts\1-1-core-identity-and-event-envelope.md):

- `AggregateType` was intentionally introduced as metadata distinct from `Domain`
- Story 1.1 already flagged the hardcoded placeholder / incorrect derivation as follow-up work
- Named-argument discipline was established as a guardrail for metadata-heavy signatures

From [`_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md`](D:\Hexalith.EventStore\_bmad-output\implementation-artifacts\1-5-commandstatus-enum-and-aggregate-tombstoning.md):

- Runtime-only constraints should be converted into explicit test guardrails where possible
- Tier 2 lifecycle coverage matters for framework invariants; do not stop at unit-only validation when the actor path is the actual consumer

### File Structure Notes

- Server contract and implementation remain in `src/Hexalith.EventStore.Server/Events/`
- Actor call-site change remains in `src/Hexalith.EventStore.Server/Actors/`
- Test-only helper alignment remains in `src/Hexalith.EventStore.Testing/`
- Do not move files or restructure folders for this story

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26.md`]
- [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md`]
- [Source: `_bmad-output/implementation-artifacts/1-1-core-identity-and-event-envelope.md`]
- [Source: `_bmad-output/planning-artifacts/architecture.md`]
- [Source: `src/Hexalith.EventStore.Server/Events/IEventPersister.cs`]
- [Source: `src/Hexalith.EventStore.Server/Events/EventPersister.cs`]
- [Source: `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs`]
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
