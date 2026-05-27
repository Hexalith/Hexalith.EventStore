# Post-Epic 22 ES-5: Async Derived DomainResult Test Coverage

Status: done

Context created: 2026-05-27
Story key: `post-epic-22-es5-async-derived-domain-result-test-coverage`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-5)
Epic: Post-Epic-22 EventStore<->Parties Review Residuals
Scope: Minor (Developer). Verification-only coverage for sync and async aggregate handlers that return a `DomainResult` subtype.

## Story

As an EventStore platform maintainer supporting enriched domain command results,
I want regression coverage for sync and async aggregate handlers that return derived `DomainResult` types,
so that `EventStoreAggregate<TState>` continues to dispatch `Task<TDerivedDomainResult>` handlers correctly for downstream services such as Parties.

## Background & Verified Residual

ES-5 is not a runtime bug on current code. The original review suspected that handler discovery recognized only `Task<DomainResult>` and that a handler returning `Task<TDerivedDomainResult>` could fall through to the unexpected-return-type path. Current `EventStoreAggregate<TState>.DispatchCommandAsync` already handles that shape:

- `Task<DomainResult>` is matched directly.
- Any async handler recorded as `IsAsync` is awaited by `GetAsyncDomainResultAsync(Task asyncResult)`.
- `GetAsyncDomainResultAsync` reflects the completed task's public `Result` property and casts the value to `DomainResult`, which accepts any subtype.

The residual is test coverage. The story should lock observable aggregate dispatch behavior with explicit sync and async derived-result fixtures: the exact derived result instance returned by the handler must survive `ProcessAsync` dispatch. `ResultPayload` is one possible signal of derived-result preservation, but it is not the goal by itself. Current `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs` already contains `DerivedDomainResult`, `DerivedResultAggregate`, `ProcessAsync_DerivedDomainResultHandle_ReturnsDerivedResult`, and `ProcessAsync_AsyncDerivedDomainResultHandle_AwaitsAndReturnsDerivedResult`. The dev agent must first verify whether those tests satisfy this story in the current branch. If they do, no production or test change is needed beyond updating this story's Dev Agent Record and sprint status during implementation. If they are absent or insufficient, add the missing focused coverage there.

This story must stay verification-only unless the focused tests fail. Do not refactor aggregate dispatch, domain result contracts, result-payload propagation, server actors, or DAPR invocation as part of ES-5.

## Completion Paths

- **Path A - Existing coverage is sufficient:** confirm the current sync and async derived-result tests assert returned runtime type, subtype marker, and event payload; run focused/full client tests; update the Dev Agent Record, File List, Verification Status, and Change Log only.
- **Path B - Coverage is missing or insufficient:** add the smallest client aggregate tests needed to prove the derived result instance survives `ProcessAsync` for sync and async handlers; then run focused/full client tests and update evidence.
- **Path C - Focused tests fail against current runtime code:** fix only `EventStoreAggregate<TState>` dispatch, keep the change minimal, then rerun focused/full client tests and document the failing behavior that justified runtime edits.

## Acceptance Criteria

1. **Sync derived `DomainResult` handler coverage exists.**
   - Given an `EventStoreAggregate<TState>` test aggregate with a static `Handle` method returning a `DomainResult` subtype
   - And the returned subtype carries an observable marker and at least one event
   - When `ProcessAsync` dispatches the matching command
   - Then the returned value remains the derived subtype, not a base `DomainResult` rewrap
   - And the marker and event payload are asserted.

2. **Async derived `DomainResult` handler coverage exists.**
   - Given an `EventStoreAggregate<TState>` test aggregate with a static `Handle` method returning `Task<TDerivedDomainResult>` where `TDerivedDomainResult : DomainResult`
   - When `ProcessAsync` dispatches the matching command
   - Then dispatch awaits the task and returns the derived subtype
   - And the marker and event payload are asserted
   - And the test would fail if `DispatchCommandAsync` only accepted `Task<DomainResult>` without the generic-task fallback.

3. **Derived-result proof has a clear stop rule.**
   - If the existing `DerivedDomainResult` fixture already carries a distinguishing marker and the tests assert the returned runtime type plus marker, document that as sufficient proof that aggregate dispatch preserves the derived result instance.
   - If the existing fixture does not prove subtype preservation, add a focused test using `ResultPayload` or equivalent enriched data to prove the returned derived result is not flattened to a base `DomainResult`.
   - Do not add a payload/enriched-result test merely because `ResultPayload` exists; add it only when the marker/type assertions are absent or insufficient.
   - Do not route through server pipeline payload forwarding in this story; ES-1 through ES-4 already own result-payload controller, checkpoint, handler, and wire compatibility concerns.

4. **No runtime changes unless the regression test fails.**
   - Do not change `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` if the existing implementation passes the focused derived-result tests.
   - Do not change `DomainResult`, `DomainServiceWireResult`, `DaprDomainServiceInvoker`, `AggregateActor`, `SubmitCommandHandler`, or `CommandsController`.
   - Do not add packages or test infrastructure.
   - If runtime code must change because the focused coverage fails, keep the fix inside `EventStoreAggregate<TState>` dispatch and document the failing behavior in the Dev Agent Record.

5. **Focused Tier 1 verification is recorded.**
   - Run `dotnet test tests/Hexalith.EventStore.Client.Tests --filter "FullyQualifiedName~EventStoreAggregateTests.ProcessAsync_DerivedDomainResultHandle_ReturnsDerivedResult|FullyQualifiedName~EventStoreAggregateTests.ProcessAsync_AsyncDerivedDomainResultHandle_AwaitsAndReturnsDerivedResult"` if the test runner supports the expression.
   - If the filter expression is not supported, run a broader focused filter such as `dotnet test tests/Hexalith.EventStore.Client.Tests --filter "FullyQualifiedName~DerivedDomainResult"` or the full client unit project.
   - Run `dotnet test tests/Hexalith.EventStore.Client.Tests` before completion.
   - Record exact pass/fail counts and any filter fallback used.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm current dispatch implementation.** (AC: 2, 4)
  - [x] Re-read `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` around `DispatchCommandAsync`.
  - [x] Confirm the `Task asyncResult when handleInfo.IsAsync` branch still calls `GetAsyncDomainResultAsync`.
  - [x] Confirm `GetAsyncDomainResultAsync` awaits the task before reading `Result` and only accepts values assignable to `DomainResult`.

- [x] **ST1 - Reconfirm existing derived-result tests.** (AC: 1, 2, 3)
  - [x] Re-read `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`.
  - [x] Check whether `DerivedDomainResult`, `DerivedResultAggregate`, `ProcessAsync_DerivedDomainResultHandle_ReturnsDerivedResult`, and `ProcessAsync_AsyncDerivedDomainResultHandle_AwaitsAndReturnsDerivedResult` are present.
  - [x] If present and sufficient, do not duplicate tests. Record the exact test names as the ES-5 evidence.
  - [x] If absent or insufficient, add the missing sync and async derived-result coverage in this file, following the existing test style.

- [x] **ST2 - Add only missing coverage, if needed.** (AC: 1, 2, 3, 4)
  - [x] Prefer extending the existing `DerivedDomainResult` fixture instead of creating a parallel aggregate.
  - [x] Treat existing runtime-type plus marker assertions as sufficient if they prove the derived instance is preserved.
  - [x] Add a test-only `CompositeCommandResult : DomainResult` with an override of `ResultPayload` only if the existing marker/type assertions are absent or insufficient.
  - [x] Assert the returned object is the derived subtype.
  - [x] Assert subtype-specific marker/payload state and at least one event.
  - [x] Assert behavior only through `ProcessAsync`; do not assert private reflection details or require a specific implementation of `GetAsyncDomainResultAsync`.
  - [x] Keep runtime code unchanged unless the new tests expose an actual regression.

- [x] **ST3 - Validate and close verification.** (AC: 5)
  - [x] Run focused derived-result test filter or document the filter fallback.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Client.Tests`.
  - [x] Update the Dev Agent Record, File List, Verification Status, and Change Log before moving the story to review/done.

## Dev Notes

### Current State Of Files To Verify

`src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`

- Current behavior: `DispatchCommandAsync` invokes the reflected `Handle` method, directly awaits `Task<DomainResult>`, then handles other async tasks through `Task asyncResult when handleInfo.IsAsync`.
- Required attention: `Task<DerivedDomainResult>` does not match `Task<DomainResult>` because generic task types are invariant, so the fallback branch is the important behavior to pin.
- Must preserve: command short-name lookup, JSON payload deserialization, envelope-aware handler arguments, `ConfigureAwait(false)`, and the unexpected-return-type exception for non-`DomainResult` results.

`tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`

- Current observed state: already contains sync and async derived-result tests using `DerivedDomainResult` and `DerivedResultAggregate`.
- Required attention: verify those tests run green and provide enough ES-5 evidence. If they already satisfy AC1 and AC2, avoid adding duplicates.
- Possible gap: the current `DerivedDomainResult` marker may already be sufficient because the goal is derived instance preservation, not payload handling for its own sake. Add a lightweight derived result with an overridden `ResultPayload` only if the existing tests do not prove returned runtime type plus subtype-specific state through `ProcessAsync`.

### Files To Avoid Editing Unless Tests Fail

`src/Hexalith.EventStore.Contracts/Results/DomainResult.cs`

- Current behavior: base result validates event/rejection mixing and exposes virtual `ResultPayload => null`.
- ES-5 must not alter this public contract.

`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`, `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`, `src/Hexalith.EventStore/Controllers/CommandsController.cs`

- Current behavior: these files are owned by ES-1 through ES-4 and server-side result-payload handling.
- ES-5 is client aggregate dispatch coverage only. Do not widen the story into server payload forwarding.

### Previous Story Intelligence

ES-1 (`post-epic-22-es1-result-payload-parse-dos-guard`) is complete. It added controller-side result-payload parsing bounds and no-payload warnings. ES-5 must not change controller parsing.

ES-2 (`post-epic-22-es2-pipeline-state-result-payload-privacy-posture`) is complete. It scrubbed `ResultPayload` from persisted actor checkpoints while preserving normal terminal payload returns. ES-5 must not change actor checkpoint behavior.

ES-3 (`post-epic-22-es3-result-payload-drop-observability`) is complete. It added a no-payload warning when a non-null result payload is dropped because final status is not known to be `Completed`. ES-5 must not change handler logging.

ES-4 (`post-epic-22-es4-domain-service-wire-result-backcompat-test`) is complete in the current workspace. It added `DomainServiceWireResultTests.DeserializesLegacyWireJsonWithoutResultPayloadAsNull` under `Hexalith.EventStore.Contracts.Tests`. ES-5 is independent and should not edit that test.

Do not modify, normalize, or stage unrelated ES4 artifacts while implementing ES-5. Existing ES4 story/status/test changes in the workspace belong to prior work and are outside this story's authorship and review scope.

### Git Intelligence

Recent commits show the ES-1 through ES-4 result-payload hardening sequence has already landed:

- `712b6d31 test(contracts): add domain service wire result backcompat fixture (#265)`
- `d0192b2c test(server): avoid secret-like payload sentinel`
- `cb1e045a fix(server): log result payload drops`
- `098c28d2 feat(story-4.11): Preserve no-op result payloads`
- `73c55513 docs(story): close ES-2 code review (#262)`

Keep ES-5 narrow: verify or add client aggregate derived-result tests, then close as coverage.

### Latest Technical Information

- Microsoft documents `Task<TResult>` as the generic task form whose completed value is exposed through the `Result` property. ES-5's fallback branch depends on reading that property after awaiting the task. Source: <https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1?view=net-10.0>
- Microsoft documents `Type.GetProperty(String)` as the reflection API used to retrieve a public property by name from the runtime task type. Source: <https://learn.microsoft.com/dotnet/api/system.type.getproperty?view=net-10.0>

### Project Context Reference

Apply `_bmad-output/project-context.md`:

- Target .NET SDK `10.0.300` and `net10.0`.
- Use xUnit v3 and Shouldly in tests, matching the repository's testing rules.
- Run targeted test projects individually.
- Treat warnings as build-breaking.
- Do not add packages or abstractions for this focused verification story.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-5`] - residual scope: implementation already correct; add sync and async derived-result regression coverage.
- [Source: `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`] - aggregate dispatch implementation and async fallback branch.
- [Source: `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`] - current aggregate dispatch test suite and observed derived-result tests.
- [Source: `src/Hexalith.EventStore.Contracts/Results/DomainResult.cs`] - base result type and virtual `ResultPayload`.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es1-result-payload-parse-dos-guard.md`] - prior controller parse hardening boundary.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es2-pipeline-state-result-payload-privacy-posture.md`] - prior actor checkpoint privacy posture.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es3-result-payload-drop-observability.md`] - prior handler payload-drop logging boundary.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es4-domain-service-wire-result-backcompat-test.md`] - prior wire compatibility test boundary.
- [Source: `_bmad-output/project-context.md`] - project rules for .NET, tests, dependencies, and workflow.
- [External: Task<TResult> Class](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1?view=net-10.0)
- [External: Type.GetProperty Method](https://learn.microsoft.com/dotnet/api/system.type.getproperty?view=net-10.0)

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Loaded BMad dev-story workflow and project context.
- 2026-05-27: Ran `aspire run --detach --non-interactive --apphost src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` with `EnableKeycloak=false`; previous apphost instance stopped by Aspire CLI, new detached apphost started, dashboard reported at `https://localhost:17017/login?t=3e84886ef4a5815b01147d189f20072a`.
- 2026-05-27: Ran `aspire describe --apphost src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json --non-interactive`; relevant resources were running/healthy, including eventstore, eventstore-admin, eventstore-admin-ui, sample, sample-blazor-ui, tenants, Dapr sidecars, and statestore.
- 2026-05-27: Re-read `EventStoreAggregate.DispatchCommandAsync`; confirmed the `Task asyncResult when handleInfo.IsAsync` fallback still calls `GetAsyncDomainResultAsync`.
- 2026-05-27: Re-read `GetAsyncDomainResultAsync`; confirmed it awaits the task before reflecting public `Result` and accepts values assignable to `DomainResult`.
- 2026-05-27: Re-read `EventStoreAggregateTests`; confirmed `DerivedDomainResult`, `DerivedResultAggregate`, `ProcessAsync_DerivedDomainResultHandle_ReturnsDerivedResult`, and `ProcessAsync_AsyncDerivedDomainResultHandle_AwaitsAndReturnsDerivedResult` are present and sufficient.
- 2026-05-27: Ran focused filter `dotnet test tests\Hexalith.EventStore.Client.Tests --filter "FullyQualifiedName~EventStoreAggregateTests.ProcessAsync_DerivedDomainResultHandle_ReturnsDerivedResult|FullyQualifiedName~EventStoreAggregateTests.ProcessAsync_AsyncDerivedDomainResultHandle_AwaitsAndReturnsDerivedResult"`; passed 2, failed 0, skipped 0.
- 2026-05-27: Ran `dotnet test tests\Hexalith.EventStore.Client.Tests`; passed 399, failed 0, skipped 0.

### Completion Notes List

- Existing ES-5 coverage is sufficient; Path A applied.
- Sync derived-result proof: `ProcessAsync_DerivedDomainResultHandle_ReturnsDerivedResult` asserts the returned value is `DerivedDomainResult`, checks marker `sync-derived`, and asserts the emitted `ItemAdded` event payload name.
- Async derived-result proof: `ProcessAsync_AsyncDerivedDomainResultHandle_AwaitsAndReturnsDerivedResult` asserts the returned value is `DerivedDomainResult`, checks marker `async-derived`, and asserts the emitted `ItemAdded` event payload name.
- No production code, test code, packages, dispatch contracts, server actors, DAPR invocation, or payload forwarding code changed.

### File List

- `_bmad-output/implementation-artifacts/post-epic-22-es5-async-derived-domain-result-test-coverage.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Verification Status

Done. Verification-only Path A completed and clean code review passed. Focused derived-result filter passed 2/2, and full client unit project passed 399/399. No runtime or test source changes were needed because existing coverage already proves sync and async derived `DomainResult` instances survive `ProcessAsync` dispatch with subtype marker and event payload assertions.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-27 | 0.1 | Created ready-for-dev post-Epic-22 ES-5 story: verify or add sync and async derived `DomainResult` aggregate dispatch coverage. | Codex |
| 2026-05-27 | 1.0 | Completed ES-5 verification-only Path A; confirmed existing sync and async derived-result aggregate dispatch tests and recorded passing focused/full client test evidence. | Codex |
| 2026-05-27 | 1.1 | Completed senior code review with no actionable findings and moved story to done. | Codex |

## Story Completion Status

Done - verification-only Path A closed and senior code review completed with no actionable findings.
