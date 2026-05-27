# Post-Epic 22 ES-2: PipelineState Result-Payload Privacy Posture

Status: review

Context created: 2026-05-27
Story key: `post-epic-22-es2-pipeline-state-result-payload-privacy-posture`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-2)
Epic: Post-Epic-22 EventStore<->Parties Review Residuals
Scope: Moderate (Developer + architecture note). Choose and implement the privacy posture for optional domain-service `ResultPayload` values that are currently written into actor pipeline checkpoints.
Decision for implementation: **scrub `ResultPayload` from persisted `PipelineState` checkpoints**. Keep the normal no-crash terminal return payload in memory. Do not choose the "document acceptance with retention bound" option.

## Story

As the EventStore platform owner handling potentially sensitive downstream result payloads,
I want actor pipeline checkpoints to exclude `ResultPayload`,
so that crash-recovery state stored through DAPR actor state cannot retain PII or enriched domain-service result bodies between `EventsStored` and terminal completion.

## Background & Verified Residual

The review residual is confirmed in `AggregateActor`: successful domain-service `ResultPayload` values are copied into `PipelineState` at the `EventsStored` and `EventsPublished` checkpoints. These checkpoints are staged through `ActorStateMachine.CheckpointAsync` and committed via `IActorStateManager`, which means they can live in the configured actor state store during crash recovery.

Current hot spots:

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:422-430` stores `domainResult.ResultPayload` in the `EventsStored` checkpoint.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:468-476` stores `domainResult.ResultPayload` in the `EventsPublished` checkpoint.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:503-511` stages it in the publish-failed path before cleanup.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1515` returns `existingPipeline.ResultPayload` on resume from `EventsStored`.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1597-1615` carries `existingPipeline.ResultPayload` through the resume publish-failed path.
- `src/Hexalith.EventStore.Server/Actors/PipelineState.cs:15,23` documents the property as preserved for crash-recovery resume.
- `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs:300-385` currently asserts resume preserves a stored result payload. This test must be inverted.

Important boundaries:

- `IdempotencyRecord` already does **not** persist `ResultPayload`; `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyRecordTests.cs:59-74` locks that behavior.
- `SubmitCommandHandler` only forwards a payload when processing accepted and final status is `Completed` (`src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:167-173`). ES-3 owns adding observability when that payload is dropped; do not implement ES-3 here.
- ES-1 already handled JSON parse depth and size guard in `CommandsController`; do not reopen controller parsing or the ES-1 tests.

## Acceptance Criteria

1. **New pipeline checkpoints never carry `ResultPayload`.**
   - Given a domain service returns a successful `DomainResult` with non-null `ResultPayload`
   - When `AggregateActor` stages `Processing`, `EventsStored`, `EventsPublished`, or `PublishFailed` pipeline states
   - Then every `PipelineState` passed to `CheckpointAsync` has `ResultPayload == null`
   - And `PipelineState.ResultPayload` is not used as a new-write field anywhere in `AggregateActor`
   - And the `PipelineState` XML doc is updated to state that the field is retained only for legacy in-flight checkpoint deserialization and must remain null for new checkpoint writes.

2. **Normal no-crash terminal responses still return the enriched payload.**
   - Given the domain result is successful, contains one or more events, and exposes a non-null `ResultPayload`
   - When `ProcessCommandAsync` completes without crash-resume
   - Then `CommandProcessingResult.ResultPayload` still equals the domain result payload
   - And `SubmitCommandHandler` can still forward the payload when final status is `Completed`
   - And `IdempotencyRecord` continues not to persist `ResultPayload`.

3. **Crash-resume deliberately drops checkpointed payloads.**
   - Given an `EventsStored` pipeline state exists with a legacy non-null `ResultPayload`
   - When `ProcessCommandAsync` resumes from that checkpoint
   - Then persisted events are still published exactly once
   - And domain service invocation and event re-persistence are still skipped
   - And the returned `CommandProcessingResult.ResultPayload` is `null`
   - And the pipeline key is still cleaned up
   - And this loss is documented as the accepted privacy tradeoff: event durability and terminal lifecycle correctness are preserved, but enriched response-body replay after a crash is not.

4. **Publish-failure and diagnostic paths remain payload-safe.**
   - Given publication fails after events are persisted
   - When normal or resume publish-failure handling stages terminal state, idempotency state, drain records, status records, logs, and dead-letter data
   - Then no result-payload content is written to `PipelineState`, logs, `CommandStatusRecord`, `IdempotencyRecord`, `UnpublishedEventsRecord`, or `DeadLetterMessage`
   - And failure reason, rejection event type, event count, drain registration, and pending-command-count behavior are unchanged.

5. **Tier 1 tests pin the privacy posture and the preserved no-crash behavior.**
   - Add or update tests in `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs`
   - Include a no-crash success case using a test `DomainResult` subclass that overrides `ResultPayload`; assert the returned result preserves the payload while every checkpointed `PipelineState.ResultPayload` is null
   - Replace `ProcessCommand_CrashAtEventsStored_Resume_PreservesResultPayload` with a regression-sensitive test that seeds a legacy `EventsStored` checkpoint containing payload content and asserts the resumed result payload is null
   - Keep the existing assertions that resume does not invoke the domain service, does not re-persist events, publishes the already-persisted events, decrements pending count, and cleans up the pipeline key
   - Keep `IdempotencyRecordTests.ToResult_DoesNotPreserve_EventCountAndResultPayload` green.

6. **Architecture posture is recorded in code-adjacent documentation.**
   - Add a concise note in `PipelineState` XML docs or a local code comment near checkpoint construction: pipeline checkpoints are crash-recovery control state and must not retain domain-service result bodies
   - The Dev Agent Record must explicitly state why the team chose scrubbing over a retention-bound acceptance: DAPR actor state is external state, actor state can be externally queried, and result payloads may contain PII.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm the data flow.** (AC: 1, 2, 3)
  - [x] Re-read `AggregateActor.cs:418-430`, `:466-495`, `:503-522`, `:1424-1515`, and `:1586-1615`.
  - [x] Re-read `PipelineState.cs`, `ActorStateMachine.cs`, `CommandProcessingResult.cs`, `IdempotencyRecord.cs`, and `SubmitCommandHandler.cs:167-173`.
  - [x] Confirm `DomainServiceWireResult.FromDomainResult` only carries `ResultPayload` for success results and `DaprDomainServiceInvoker.ToDomainResult` maps it back through a `DomainResult` override.

- [x] **ST1 - Scrub persisted pipeline checkpoints.** (AC: 1, 3, 4, 6)
  - [x] In normal success processing, remove `ResultPayload: domainResult.ResultPayload` from `EventsStored` and `EventsPublished` `PipelineState` creation.
  - [x] In normal publish-failed processing, ensure the staged `PublishFailed` `PipelineState` also uses null payload.
  - [x] In resume success processing, stop passing `existingPipeline.ResultPayload` to `CompleteTerminalAsync`; pass null or omit the argument.
  - [x] In resume publish-failed processing, stop carrying `existingPipeline.ResultPayload` into the `PublishFailed` state and `CreatePublishFailedResult`.
  - [x] Update `PipelineState` XML docs so future developers do not reintroduce payload persistence for crash replay convenience.

- [x] **ST2 - Preserve normal terminal payload behavior.** (AC: 2)
  - [x] Leave `CompleteTerminalAsync(... resultPayload: domainResult.ResultPayload)` unchanged on the normal no-crash completed path.
  - [x] Leave `CommandProcessingResult.ResultPayload` and `SubmitCommandResult.ResultPayload` contracts unchanged.
  - [x] Leave `SubmitCommandHandler` payload forwarding logic unchanged; ES-3 owns drop observability.
  - [x] Leave `IdempotencyRecord` unchanged; it already omits result payload by design.

- [x] **ST3 - Update Tier 1 tests.** (AC: 1, 2, 3, 4, 5)
  - [x] Add a private test-only `DomainResult` subclass in `StateMachineIntegrationTests` that returns a non-null `ResultPayload`.
  - [x] Add a no-crash test proving returned `CommandProcessingResult.ResultPayload` is preserved and all checkpointed `PipelineState.ResultPayload` values are null.
  - [x] Invert the existing crash-resume payload test to assert a seeded legacy payload is dropped on resume.
  - [x] Add or extend publish-failure coverage if the existing checks do not verify payload-safe `PublishFailed` checkpoint staging.
  - [x] Keep assertions on domain-service non-invocation, no event re-persistence, publish call count, pending-count decrement, and cleanup.

- [x] **ST4 - Validate and record evidence.** (AC: all)
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~StateMachineIntegrationTests|FullyQualifiedName~IdempotencyRecordTests"` and record pass/fail counts.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~AggregateActor"` if the first command is green.
  - [x] Run `dotnet build Hexalith.EventStore.slnx --configuration Release` unless a pre-existing workspace issue blocks it; classify any unrelated failure explicitly.
  - [x] Update the Dev Agent Record, File List, Verification Status, and Change Log before moving the story to `review`.

## Dev Notes

### Current State Of Files To Update

`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`

- Current behavior: orchestrates command processing; writes `PipelineState` through `ActorStateMachine.CheckpointAsync`; commits with `StateManager.SaveStateAsync`; cleans pipeline state on terminal completion.
- Current payload issue: `domainResult.ResultPayload` is copied into `PipelineState` at `EventsStored`, `EventsPublished`, and publish-failed staging. Resume paths also read `existingPipeline.ResultPayload` and can return it after crash recovery.
- Required change: new `PipelineState` instances must use null payload. Resume must ignore any legacy payload already present in existing state.
- Must preserve: tenant validation before state access, event persistence before publish, no re-persistence on `EventsStored` resume, pending-count decrement, advisory status writes, dead-letter/drain behavior, and normal no-crash terminal payload return.

`src/Hexalith.EventStore.Server/Actors/PipelineState.cs`

- Current behavior: public record for in-flight command lifecycle checkpoints, with optional `ResultPayload`.
- Required change: update documentation so `ResultPayload` is legacy/deserialization-only and not a field for new checkpoint writes. Prefer keeping the property for compatibility with old in-flight serialized state rather than removing it.
- Must preserve: constructor compatibility for existing tests and any pre-existing state serialized with the field.

`tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs`

- Current behavior: includes the crash-resume test `ProcessCommand_CrashAtEventsStored_Resume_PreservesResultPayload`, which now encodes the undesired privacy posture.
- Required change: replace or rename that test to assert a legacy stored payload is dropped, and add a no-crash payload test so the public response contract does not regress.
- Must preserve: current state-machine assertions around resume and cleanup.

### Files To Read But Avoid Editing Unless Needed

- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` - confirms checkpoint writes go through `SetStateAsync`; no logic change expected.
- `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs` - terminal in-memory result contract; do not remove `ResultPayload`.
- `src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs` - already omits event count and result payload; no change expected.
- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` - ES-3 owns drop observability; do not modify for ES2 unless tests expose an unavoidable coupling.
- `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs` and `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` - wire and mapping source of successful result payloads; no change expected.

### Implementation Guardrails

- Do not add encryption, hashing, redaction text, or placeholders for `ResultPayload` inside `PipelineState`. The checkpoint value must be null, not a transformed copy.
- Do not remove `ResultPayload` from `CommandProcessingResult`, `SubmitCommandResult`, `SubmitCommandResponse`, `DomainResult`, or `DomainServiceWireResult`; those are outside the ES2 residual.
- Do not change ES-3 behavior in `SubmitCommandHandler.cs:167-173`; payload-drop logging is the next backlog row.
- Do not log payload content, even in tests. Use sentinel strings only in in-memory assertions.
- Treat any old non-null `PipelineState.ResultPayload` as legacy data to ignore and clean up, not as data to propagate.
- Preserve `ConfigureAwait(false)` style in touched async code.

### Previous Story Intelligence

ES-1 (`post-epic-22-es1-result-payload-parse-dos-guard`) was already implemented in the dirty workspace and marks the result-payload parsing boundary as bounded and payload-safe:

- `CommandsController` now has a 64 KiB char-count guard and explicit `JsonDocumentOptions { MaxDepth = 64 }`.
- `CommandsControllerResultPayloadTests` covers oversized, over-depth, valid object/array/scalar/json-null, null/whitespace, and malformed payloads.
- Do not change controller parsing here. ES2 is actor checkpoint privacy, not HTTP response parsing.

### Git Intelligence

Recent commits show this workspace is in post-Epic hardening mode:

- `abf39b43 chore: update submodule commits for Hexalith.Commons and Hexalith.Tenants`
- `8aa20c18 Keycloak Dev Fast-Start: persistent container & port config`
- `2c37ae2c Add DAPR actor placement health check and fix ETag proxy`

Use focused tests and avoid broad refactors. The worktree already had uncommitted ES1 code/status changes when this story was created; do not revert them.

### Latest Technical Information

- DAPR actor state is persisted through the configured external actor state store. DAPR documents actor state as stored in external state stores and externally queryable by key namespace; this makes result-payload minimization a real privacy control, not just an internal object-cleanup preference. Source: <https://docs.dapr.io/reference/api/actors_api/#querying-actor-state-externally>
- DAPR's state overview recommends TTL for actor state so state is eventually removed, but ES2 should not depend on TTL to justify storing payloads. Scrubbing the field avoids retaining sensitive content even if TTL is absent, backend-specific, delayed, or misconfigured. Source: <https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/#time-to-live-ttl-on-actor-state>
- Current repo context pins DAPR runtime `1.17.7` and .NET package family `1.17.9`; do not add or upgrade packages for this story.

### Project Context Reference

Apply `_bmad-output/project-context.md` rules:

- No payloads in logs; only envelope metadata.
- DAPR actor state must go through `IActorStateManager`.
- Command status writes are advisory and must not block the pipeline.
- Tenant validation before state rehydration must remain intact.
- Tests use xUnit v3, Shouldly, and NSubstitute.
- Run targeted test projects individually; avoid solution-level `dotnet test` as a first step.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-2`] - residual scope and posture choices.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:422-430`] - `EventsStored` checkpoint currently stores payload.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:468-476`] - `EventsPublished` checkpoint currently stores payload.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1424-1515`] - resume path currently returns `existingPipeline.ResultPayload`.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1586-1615`] - resume publish-failed path carries `existingPipeline.ResultPayload`.
- [Source: `src/Hexalith.EventStore.Server/Actors/PipelineState.cs`] - pipeline checkpoint DTO to document.
- [Source: `src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs`] - idempotency state already omits result payload.
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:167-173`] - terminal forwarding gate; ES-3 owns drop observability.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs:300-385`] - existing test to invert.
- [Source: `_bmad-output/project-context.md`] - no-payload logging and DAPR actor-state rules.
- [External: DAPR actor state external querying docs](https://docs.dapr.io/reference/api/actors_api/#querying-actor-state-externally)
- [External: DAPR actor-state TTL guidance](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/#time-to-live-ttl-on-actor-state)

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Aspire baseline started with `aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; MCP detected the AppHost, with Keycloak unhealthy and dependent services waiting. AppHost was stopped before validation to release build locks.
- 2026-05-27: Red test run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~StateMachineIntegrationTests|FullyQualifiedName~IdempotencyRecordTests"` failed as expected: 3 failed, 13 passed, 16 total. Failures covered checkpoint payload persistence, crash-resume payload replay, and publish-failed terminal payload retention.
- 2026-05-27: Green targeted run for the same filter passed: 0 failed, 16 passed, 16 total.
- 2026-05-27: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~AggregateActor"` passed: 0 failed, 72 passed, 72 total.
- 2026-05-27: `dotnet build Hexalith.EventStore.slnx --configuration Release` passed: 0 warnings, 0 errors.
- 2026-05-27: Full affected-project regression `dotnet test tests/Hexalith.EventStore.Server.Tests` found unrelated existing expectation drift: 6 failed, 2110 passed, 25 skipped, 2141 total. Failing tests were health-check count/readiness assertions and local admin DAPR inbound-policy assertions, outside the touched actor checkpoint payload files.

### Completion Notes List

- Scrubbed new `PipelineState` checkpoints so `Processing`, `EventsStored`, `EventsPublished`, and `PublishFailed` writes do not retain domain-service `ResultPayload` content.
- Updated resume paths to ignore legacy non-null `PipelineState.ResultPayload` values instead of returning or re-staging them.
- Preserved the normal no-crash completed path by leaving `CompleteTerminalAsync(... resultPayload: domainResult.ResultPayload)` unchanged.
- Extended Tier 1 actor tests with a payload-bearing `DomainResult` subclass, no-crash checkpoint-scrubbing coverage, legacy crash-resume payload-drop coverage, and publish-failed payload-safety coverage.
- Chose scrubbing over retention-bound acceptance because DAPR actor state is external state, actor state can be externally queried by key namespace, and domain-service result payloads may contain PII or enriched downstream response bodies.

### File List

- `_bmad-output/implementation-artifacts/post-epic-22-es2-pipeline-state-result-payload-privacy-posture.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Actors/PipelineState.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs`

## Verification Status

Implemented and ready for review.

- PASS: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~StateMachineIntegrationTests|FullyQualifiedName~IdempotencyRecordTests"` (16 passed).
- PASS: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~AggregateActor"` (72 passed).
- PASS: `dotnet build Hexalith.EventStore.slnx --configuration Release` (0 warnings, 0 errors).
- NOTE: `dotnet test tests/Hexalith.EventStore.Server.Tests` has unrelated existing failures in health-check count/readiness tests and local admin DAPR access-control tests (6 failed, 2110 passed, 25 skipped). No failed test references `AggregateActor`, `PipelineState`, `StateMachineIntegrationTests`, `IdempotencyRecord`, or result-payload checkpoint behavior.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-27 | 0.1 | Created ready-for-dev post-Epic-22 ES-2 story: scrub `ResultPayload` from persisted actor `PipelineState` checkpoints while preserving normal no-crash terminal response payloads. | Codex |
| 2026-05-27 | 1.0 | Implemented checkpoint payload scrubbing, legacy resume payload dropping, publish-failed payload safety, and Tier 1 regression coverage. | GPT-5 Codex |

## Story Completion Status

Ultimate context engine analysis completed - comprehensive developer guide created.
