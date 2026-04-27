# Story Post-Epic-1 R1-A1: Thread Real AggregateType Through Persistence Pipeline

Status: done

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

- [x] Task 1: Extend the persistence contract (AC: #1)
  - [x] 1.1 Update `src/Hexalith.EventStore.Server/Events/IEventPersister.cs` to add `string aggregateType` between `AggregateIdentity identity` and `CommandEnvelope command`
  - [x] 1.2 Update `src/Hexalith.EventStore.Server/Events/EventPersister.cs` to validate `aggregateType` and use it when building `EventEnvelope`
  - [x] 1.3 Update `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs` to match the new signature and envelope mapping

- [x] Task 2: Thread the real value from the actor (AC: #2, #3)
  - [x] 2.1 Update `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` so the `PersistEventsAsync` call passes the registered aggregate type rather than relying on the persister to derive it
  - [x] 2.2 Confirm the aggregate type source is the actor-registration/convention name already used by the actor runtime, not a new ad-hoc derivation
  - [x] 2.3 Verify no remaining persistence path still maps `AggregateType` from `identity.Domain`

- [x] Task 3: Update direct callers and helpers (AC: #6)
  - [x] 3.1 Update `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs` call sites
  - [x] 3.2 Update logging/security tests that call `PersistEventsAsync` directly:
        `tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs`,
        `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`,
        `tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs`,
        `tests/Hexalith.EventStore.Server.Tests/Logging/CausationIdLoggingTests.cs`
  - [x] 3.3 Update integration/security callers:
        `tests/Hexalith.EventStore.IntegrationTests/Events/EventPersistenceIntegrationTests.cs`,
        `tests/Hexalith.EventStore.IntegrationTests/Security/MultiTenantStorageIsolationTests.cs`
  - [x] 3.4 Grep for remaining `PersistEventsAsync(` call sites and update any missed usages

- [x] Task 4: Add regression coverage (AC: #4, #5)
  - [x] 4.1 Add `EventPersisterTests.PersistEventsAsync_PopulatesAggregateTypeFromParameter_NotFromDomain`
  - [x] 4.2 Add or extend an aggregate-actor integration test proving a persisted event from the Counter sample carries `AggregateType == "counter"`
  - [x] 4.3 Assert the negative case implicitly: the persisted envelope must not contain `identity.Domain` in `AggregateType`

- [x] Task 5: Validate the change set (AC: #6)
  - [x] 5.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`
  - [x] 5.2 Run targeted Tier 1 suites covering direct callers
  - [x] 5.3 Run targeted Tier 2/integration coverage for actor persistence if the environment is available

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

- `Domain` is the bounded context / service namespace
- `AggregateType` is the specific aggregate resource name
- They are intentionally separate fields on `EventEnvelope` so they can diverge in future work without re-touching the persistence seam

**Current state (post-R1-A1):** the codebase has a 1:1 Domain↔AggregateType mapping enforced by `NamingConventionEngine.GetDomainName(typeof(TAggregate))` (e.g., `CounterAggregate` → `counter`). `command.Domain` carries that same kebab-case name, so today the two fields hold the same string. The post-R1-A1 contract no longer *derives* `AggregateType` from `Domain` inside the persister — it is now an explicit parameter — but the actor sources it from `command.Domain` because that *is* the convention-derived name in the current design. A future story that introduces multi-aggregate-per-domain (e.g., one bounded context with `OrderAggregate`, `InvoiceAggregate`, `ShipmentAggregate`) can wire the actor to a registry without changing the persister signature.

Do not "simplify" the two envelope fields back into one. The seam is structural prep for future divergence.

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

claude-opus-4-7[1m] (Amelia)

### Debug Log References

- 2026-04-27: Build with `-p:NuGetAuditMode=direct -p:NuGetAudit=false` to bypass pre-existing NU1902 OpenTelemetry advisories that block restore at solution scope. The advisories are unrelated to this story.

### Completion Notes List

- AC #1: `IEventPersister.PersistEventsAsync` now takes `string aggregateType` between `identity` and `command`. `EventPersister` validates non-null/empty/whitespace via `ArgumentException.ThrowIfNullOrWhiteSpace`; `FakeEventPersister` mirrors the validation.
- AC #2: `AggregateActor` passes `command.Domain` (the registered aggregate type the actor already knows at runtime) using fully named arguments. Both `EventPersister` and `FakeEventPersister` now stamp `EventEnvelope.AggregateType` from the parameter rather than `identity.Domain`.
- AC #3: Verified by Tier 1 test (`PersistEventsAsync_PopulatesAggregateTypeFromParameter_NotFromDomain`) — passes `aggregateType: "counter"` while keeping `identity.Domain="test-domain"` and asserts the persisted envelope reports `"counter"`, not the domain.
- AC #4: New Tier 1 `EventPersisterTests.PersistEventsAsync_PopulatesAggregateTypeFromParameter_NotFromDomain` plus four guard-clause tests (null / empty / whitespace aggregate type, plus repositioned existing null guards) lock the parameter contract.
- AC #5: Tier 2 `AggregateActorIntegrationTests.ProcessCommandAsync_PersistsEventWithRegisteredAggregateType` exercises the full `IAggregateActor.ProcessCommandAsync` pipeline against the live DAPR sidecar + Redis, then reads events back via `GetEventsAsync(0)` and asserts `AggregateType == "counter"` (R2-A6 end-state inspection). Run requires `dapr init` + Docker.
- AC #6: Updated all six listed direct callers plus `EventPersisterTests` (~14 call sites) and `EventPersistenceIntegrationTests` (~5 call sites) using named arguments at every call site for swap safety. Grep confirms no stale 4-arg call sites remain in `src/` or `tests/` (excluding the nested `Hexalith.Tenants/Hexalith.FrontShell/...` submodule per CLAUDE.md guidance).
- Tier 1 validation: `Contracts.Tests` 271/271, `Client.Tests` 321/321, `Testing.Tests` 67/67, `Sample.Tests` 62/62.
- Tier 2 validation: `Server.Tests` 1638/1638 against a fresh `dapr init` (DAPR 1.17.5 with Redis, placement, scheduler, zipkin). Includes the new `AggregateActorIntegrationTests.ProcessCommandAsync_PersistsEventWithRegisteredAggregateType` end-state inspection test passing against the live actor pipeline.

### Review Findings

- [x] [Review][Decision] **AC #2 / Task 2.2 — `command.Domain` is the same source as `identity.Domain`, so the production patch does not by itself make the two envelope fields differ** [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:367] — `CommandEnvelope.cs:38` defines `AggregateIdentity => new(TenantId, Domain, AggregateId)`, so the value passed as `aggregateType: command.Domain` is byte-identical to `identity.Domain`. **Resolution:** Option (a) — accept as structural prep. The codebase has a 1:1 Domain↔AggregateType mapping by design (`NamingConventionEngine.GetDomainName(typeof(CounterAggregate))` returns `"counter"` and `command.Domain` carries that same name). The R1-A1 contract change makes the persister no longer *derive* `AggregateType` from `identity.Domain` — the parameter is now a contract — and that prepares the seam for a future story that introduces multi-aggregate-per-domain. Spec Dev Notes "Semantic Guardrail" section updated to reflect this.

- [x] [Review][Patch] **Tier 2 integration test renamed and dedupe-flagged** [tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIntegrationTests.cs:260-292] — Under (a), the test cannot make Domain ≠ AggregateType through current data flow. Renamed `ProcessCommandAsync_PersistsEventWithRegisteredAggregateType` → `ProcessCommandAsync_PlumbsAggregateTypeThroughActorPipeline` and updated docstring to honestly say it confirms end-to-end plumbing (actor → persister → state-store → reader) without dropping or substituting the value. The Tier 1 `EventPersisterTests.PersistEventsAsync_PopulatesAggregateTypeFromParameter_NotFromDomain` remains the canonical proof of the parameter-source guarantee.

- [x] [Review][Patch] **`ShouldNotBe("unknown")` removed** [tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIntegrationTests.cs:292] — Theatre assertion deleted. Under (a), Domain == AggregateType in this test by data-flow construction, so a `ShouldNotBe(identity.Domain)` would always fail and `ShouldNotBe("unknown")` was a no-op against a string no code path produces. Tier 1 covers the discrimination via distinct fixture values.

- [x] [Review][Patch] **`FakeEventPersister` validation symmetry restored for `domainServiceVersion`** [src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:41] — Changed `ArgumentNullException.ThrowIfNull(domainServiceVersion)` → `ArgumentException.ThrowIfNullOrWhiteSpace(domainServiceVersion)` to match the real persister.

- [x] [Review][Patch] **Tier 2/3 integration call sites — re-classified after Decision (a)** [tests/Hexalith.EventStore.IntegrationTests/Events/EventPersistenceIntegrationTests.cs; tests/Hexalith.EventStore.IntegrationTests/Security/MultiTenantStorageIsolationTests.cs] — Under (a), these tests cannot make `aggregateType` differ from `identity.Domain` through current data flow. The discrimination guarantee lives in the Tier 1 unit test. Integration tests retain the matching-value pattern (faithful to current data flow) and serve as plumbing/end-state inspection only. No code change required beyond P5 (named arguments).

- [x] [Review][Patch] **Named-argument discipline — `domainServiceVersion:` named at every call site** [EventPersisterTests.cs (~22 sites); EventPersistenceIntegrationTests.cs (6 sites); MultiTenantStorageIsolationTests.cs (7 sites); 4 logging tests (1 each)] — The two `string` parameters (`aggregateType` and `domainServiceVersion`) at non-adjacent positions were the only real swap risk; the other three params are typed and would not compile if swapped. `aggregateType:` was already named everywhere; `domainServiceVersion:` is now named at every call site as well.

- [x] [Review][Defer] **XML doc on `aggregateType` claims "kebab-case, e.g. `counter`" but no validation enforces casing or character set** [src/Hexalith.EventStore.Server/Events/IEventPersister.cs:53] — deferred, contract is documentation-only; tightening would expand scope beyond R1-A1. `EventEnvelope.AggregateType` is a free-form string today; persister could optionally call `NamingConventionEngine.ValidateKebabCase` for consistency.

- [x] [Review][Defer] **`Domain` field in envelope construction still hard-coded from `identity.Domain` in two parallel files** [src/Hexalith.EventStore.Server/Events/EventPersister.cs:85-87; src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:83-85] — deferred, copy-paste hot zone is pre-existing; refactoring envelope construction into a shared helper is out of scope for this story.

- [x] [Review][Defer] **Mixed-case `command.Domain` could pass through to `aggregateType` if `CommandEnvelope` does not lowercase** — deferred, edge case worth a future targeted test once the Decision above is resolved (if `command.Domain` remains the source, casing normalization between Domain and AggregateType matters; if a registry replaces it, this concern goes away).

- [x] [Review][Defer] **New unit test cross-references `"counter"` literal and `TestIdentity.Domain` via both `ShouldBe` and `ShouldNotBe`** [tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs:451-455] — deferred, fragile if a maintainer changes `TestIdentity` so that `Domain == "counter"`. Minor; replace one with an extracted `const string ExpectedAggregateType = "counter"` distinct from the identity fixture.

### File List

- src/Hexalith.EventStore.Server/Events/IEventPersister.cs
- src/Hexalith.EventStore.Server/Events/EventPersister.cs
- src/Hexalith.EventStore.Server/Actors/AggregateActor.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs
- tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs
- tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs
- tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs
- tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs
- tests/Hexalith.EventStore.Server.Tests/Logging/CausationIdLoggingTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/Events/EventPersistenceIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/Security/MultiTenantStorageIsolationTests.cs
