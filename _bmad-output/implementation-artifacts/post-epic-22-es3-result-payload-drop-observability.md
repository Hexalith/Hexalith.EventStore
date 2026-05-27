# Post-Epic 22 ES-3: Result-Payload Drop Observability

Status: done

Context created: 2026-05-27
Story key: `post-epic-22-es3-result-payload-drop-observability`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-3)
Epic: Post-Epic-22 EventStore<->Parties Review Residuals
Scope: Minor (Developer). Add a dedicated warning at the `SubmitCommandHandler` result-payload drop site when a non-null actor/domain result payload is not returned because the final command status read by the handler is not known to be `Completed`.

## Story

As an EventStore operator diagnosing enriched command responses,
I want dropped result payloads to emit a dedicated no-payload warning with correlation and tenant metadata,
so that transient command-status read failures or non-completed terminal states do not make an enriched domain-service payload disappear without an observable signal.

## Background & Verified Residual

The ES-3 review finding is mostly valid on current code. `SubmitCommandHandler` already logs status-store read failures through `StatusReadForTrackingFailed` at `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:91`, so the status-read failure itself is observable. The residual gap is narrower: the later return expression at `SubmitCommandHandler.cs:168-172` silently drops a non-null `processingResult.ResultPayload` unless `finalStatus?.Status == CommandStatus.Completed`.

Current shape:

- `finalStatus` starts as null at `SubmitCommandHandler.cs:80`.
- The handler reads final status when either tracker exists or `processingResult.ResultPayload is not null` (`SubmitCommandHandler.cs:81-89`).
- Status read failures are advisory and logged, then processing continues (`SubmitCommandHandler.cs:90-92`).
- The response payload is returned only when processing was accepted, the actor returned a non-null result payload, and final status is `Completed` (`SubmitCommandHandler.cs:168-172`).
- If the status read failed, or if the final status is `Rejected`, `PublishFailed`, `TimedOut`, `Received`, `Processing`, `EventsStored`, or `EventsPublished`, the payload becomes null with no dedicated drop log.

This story only adds observability for that final drop. It must not return payloads for non-completed or unknown statuses, and it must not replace or suppress the existing status-read failure warning.

## Acceptance Criteria

1. **A non-null result payload drop emits one dedicated warning.**
   - Given `commandRouter.RouteCommandAsync` returns an accepted `CommandProcessingResult` with non-null `ResultPayload`
   - And the status read either fails or returns a status other than `Completed`
   - When `SubmitCommandHandler.Handle` returns
   - Then `SubmitCommandResult.ResultPayload` remains null
   - And exactly one dedicated `LogWarning` is emitted at the drop site after routing and status-read handling
   - And the warning is emitted if and only if `processingResult.Accepted == true`, `processingResult.ResultPayload is not null`, and either the final status read failed or `finalStatus.Status != CommandStatus.Completed`
   - And the warning is distinct from the existing `StatusReadForTrackingFailed` warning
   - And the warning includes stage `ResultPayloadDropped`.

2. **Completed final status still returns the payload and does not warn.**
   - Given `processingResult.Accepted == true`
   - And `processingResult.ResultPayload` is non-null
   - And `statusStore.ReadStatusAsync` returns `CommandStatus.Completed`
   - When `Handle` returns
   - Then the returned `SubmitCommandResult.ResultPayload` equals the actor/domain result payload
   - And the new payload-drop warning is not emitted.

3. **No payload content is logged.**
   - Given the dropped `ResultPayload` contains a distinctive sensitive sentinel
   - When the drop warning is emitted
   - Then no log entry contains result-payload content, serialized/deserialized payload values, command payload bytes, secrets, exception text from the status read, extension metadata values, or user-controllable display values
   - And the warning includes only this allowlist of envelope/control metadata: `CorrelationId`, `TenantId`, `AggregateId`, `CommandType`, `FinalStatus`, `StatusReadSucceeded`, and `Stage=ResultPayloadDropped`
   - And `ResultPayloadDropped` is mandatory, not optional or merely recommended.
   - And the allowlist is closed: no other structured properties may be attached to the `ResultPayloadDropped` log unless a reviewer explicitly approves them.
   - And the implementation does not log, serialize, stringify, hash, measure, count, or otherwise derive metadata from `processingResult.ResultPayload`.

4. **Status-read failure remains advisory and produces both useful signals.**
   - Given `statusStore.ReadStatusAsync` throws after the router returns an accepted result with non-null payload
   - When `Handle` continues
   - Then the existing `StatusReadForTrackingFailed` warning is still emitted
   - And the new payload-drop warning is also emitted exactly once with `FinalStatus=Unavailable` and `StatusReadSucceeded=false`
   - And the new warning does not remove, rename, suppress, or change the existing `StatusReadForTrackingFailed` log
   - And `Handle` still returns a successful `SubmitCommandResult` with the request correlation id and null result payload.

5. **No unrelated pipeline behavior changes.**
   - Do not change `CommandsController.ParseOptionalResultPayload`; ES-1 owns parse depth/size handling.
   - Do not change actor checkpoint payload persistence; ES-2 already scrubbed `PipelineState`.
   - Do not change `CommandProcessingResult`, `SubmitCommandResult`, `SubmitCommandResponse`, `DomainResult`, or `DomainServiceWireResult` contracts.
   - Do not change advisory status-write/read semantics, activity tracking, stream tracking, rejection/backpressure exception behavior, status-store key scope, or command archive writes.

6. **Tier 1 tests pin the observable drop path.**
   - Add or update tests under `tests/Hexalith.EventStore.Server.Tests`, preferably near existing `SubmitCommandHandler` tests.
   - Cover: completed status plus non-null payload returns payload and no drop warning; non-completed status plus non-null payload drops payload and logs once with `StatusReadSucceeded=true`; status-read exception plus non-null payload logs both existing read failure and new drop warning with `StatusReadSucceeded=false`; non-completed status plus null `processingResult.ResultPayload` does not trigger a status read just for payload and does not log the new warning.
   - Include at least one payload-leak assertion using a sentinel payload string that must not appear in any captured log message, structured log state, or exception text.
   - Prefer assertions over the captured log event id/stage and structured fields instead of brittle full-message string comparisons.
   - Run a focused test filter for the affected `SubmitCommandHandler` tests and the existing payload-protection logging test.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm the handler data flow.** (AC: 1, 2, 4, 5)
  - [x] Re-read `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:80-92` and `:168-172`.
  - [x] Confirm `processingResult.ResultPayload is not null` is already part of the status-read gate.
  - [x] Confirm the existing `StatusReadForTrackingFailed` method remains unchanged except for any line-number drift.

- [x] **ST1 - Add a source-generated drop warning.** (AC: 1, 3, 4)
  - [x] Add a new `[LoggerMessage]` source-generated partial method in `SubmitCommandHandler.Log`, using the next local event id after 1106 unless another nearby event id has appeared.
  - [x] Set the generated method level to `Warning`; do not use an ad hoc inline `logger.LogWarning(...)` call.
  - [x] Required message shape includes `Stage=ResultPayloadDropped`; for example: `Result payload dropped because final command status was not Completed: CorrelationId={CorrelationId}, TenantId={TenantId}, AggregateId={AggregateId}, CommandType={CommandType}, FinalStatus={FinalStatus}, StatusReadSucceeded={StatusReadSucceeded}. Stage=ResultPayloadDropped`.
  - [x] Keep method parameters to the allowed envelope/control metadata only: `correlationId`, `tenantId`, `aggregateId`, `commandType`, `finalStatus`, and `statusReadSucceeded`.
  - [x] Do not pass `processingResult.ResultPayload`, command payload, `processingResult.ErrorMessage`, extension metadata values, display names, exception text from the status read, serialized objects, payload length/count/hash/type, or any domain-service result body into the log method.

- [x] **ST2 - Log exactly at the drop decision.** (AC: 1, 2, 3, 4, 5)
  - [x] Replace the current inline `ResultPayload = ... ? ... : null` expression with a small local variable/branch so the drop condition is explicit and testable.
  - [x] Define the drop condition as: accepted processing result, non-null `processingResult.ResultPayload`, and either the final status read failed or the final status read into `finalStatus` is not `CommandStatus.Completed`.
  - [x] When dropping, call the new log method once before returning the result with null payload.
  - [x] Use `finalStatus?.Status.ToString() ?? "Unavailable"` (or equivalent) for the logged status value so status-read failures do not throw.
  - [x] Log `StatusReadSucceeded=true` when the final status read call completes, even if no status record is returned; log `StatusReadSucceeded=false` on the status-read exception/drop path.
  - [x] Do not infer from earlier/intermediate statuses; log only at the point where the handler is actually about to discard the non-null result payload.
  - [x] Preserve `Log.CommandRouted(logger, request.CorrelationId)` behavior and the returned correlation id.

- [x] **ST3 - Add focused tests.** (AC: 1, 2, 3, 4, 6)
  - [x] Add a dedicated test class such as `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerResultPayloadTests.cs`, or extend an existing `SubmitCommandHandler*Tests` file if that better matches local style.
  - [x] Use `InMemoryCommandArchiveStore`, `ICommandStatusStore`/`ICommandRouter` substitutes, and a simple capturing `ILogger<SubmitCommandHandler>` following existing test logger patterns.
  - [x] Completed final status case: seed/read `CommandStatus.Completed`, return a payload, assert payload preserved and no `ResultPayloadDropped` log.
  - [x] Non-completed final status case: return `CommandStatus.PublishFailed` or `Rejected`, assert payload null and one `ResultPayloadDropped` warning with exact `Stage`, exact allowed metadata fields, `FinalStatus`, and `StatusReadSucceeded=true`.
  - [x] Status-read failure case: make `ReadStatusAsync` throw, assert existing `StatusReadForTrackingFailed` and new `ResultPayloadDropped` warnings are both present, `FinalStatus=Unavailable`, `StatusReadSucceeded=false`, and result payload null.
  - [x] Null actor payload case: router returns accepted result with null payload, assert no extra read solely for payload and no drop warning, even if a non-completed final status is otherwise present in the test fixture.
  - [x] Payload leak case: use a sentinel like `SECRET-RESULT-PAYLOAD-DO-NOT-LOG` and assert it is absent from captured log messages, structured log state, and exception text.
  - [x] Assert the new log's event id/stage and structured fields where the local test logger supports it; avoid depending only on a full formatted message string.
  - [x] Assert the warning carries no extra structured properties beyond the closed allowlist when the local test logger exposes property bags.

- [x] **ST4 - Validate and record evidence.** (AC: all)
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitCommandHandler"`.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData"` or a matching focused payload-protection filter.
  - [x] Validate that a warning is emitted when a non-null result payload is dropped for a non-completed status or status-read failure, and that the captured log message/state contains no payload value or serialized payload content.
  - [x] Run `dotnet build Hexalith.EventStore.slnx --configuration Release` if the focused tests pass and no unrelated long-running local state blocks it.
  - [x] Update the Dev Agent Record, File List, Verification Status, and Change Log before moving the story to review.

### Review Findings

- [x] [Review][Decision] Clarify `StatusReadSucceeded` semantics when the final status read returns null — `SubmitCommandHandler` logged `StatusReadSucceeded=false` whenever `finalStatus is null` (`src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:168`), so a successful status-store read that returned null for a missing or expired key was indistinguishable from an exception/failure path and had no accompanying `StatusReadForTrackingFailed` handler warning. The status-store contract allows null for missing/expired keys (`src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs:22`), and the original tests covered thrown read failure but not successful-null status. Resolved by tracking read-call success separately from status-record presence and adding a successful-null-status regression test.

## Dev Notes

### Current State Of Files To Update

`src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`

- Current behavior: writes advisory `Received` status, archives the command, routes to the aggregate actor, optionally reads final status for trackers/payload forwarding, tracks admin activity, handles rejected/backpressure cases, then returns a `SubmitCommandResult`.
- Current payload issue: `processingResult.ResultPayload` is returned only when `finalStatus?.Status == CommandStatus.Completed`. Otherwise it is intentionally dropped, but that drop has no dedicated log.
- Required change: add one warning only when a non-null payload is dropped because the final status read into `finalStatus` is not `Completed` or because final status is unavailable after a read failure.
- Must preserve: advisory status-store semantics, command archive behavior, activity tracker behavior, stream tracker behavior, rejection/backpressure exception paths, `ConfigureAwait(false)` style, source-generated logging style, and the no-payload-in-logs rule.

`tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs`

- Current behavior: covers basic correlation id return and the no-trackers/no-read shape for the four-argument constructor.
- Reuse guidance: keep the existing no-read assertion true for null payloads and no trackers. Add new tests in a focused sibling file if extending this file would make it too dense.

`tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerStatusTests.cs`

- Current behavior: covers `Received` status writes and warning on status-write failures.
- Reuse guidance: test helpers here are useful for command creation and status-store substitutes, but ES-3 is about final status read/drop behavior, not initial status writes.

`tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`

- Current behavior: proves `SubmitCommandHandler` does not log raw command payload bytes.
- Required attention: either extend this style or add a dedicated ES-3 test proving the result payload sentinel is not logged by the new warning.

### Recommended Implementation Shape

Prefer a small explicit branch over a clever expression:

```csharp
string? resultPayload = null;
bool statusReadSucceeded = finalStatus is not null;
if (processingResult.Accepted && processingResult.ResultPayload is not null) {
    if (finalStatus?.Status == CommandStatus.Completed) {
        resultPayload = processingResult.ResultPayload;
    }
    else {
        Log.ResultPayloadDropped(
            logger,
            request.CorrelationId,
            request.Tenant,
            request.AggregateId,
            request.CommandType,
            finalStatus?.Status.ToString() ?? "Unavailable",
            statusReadSucceeded);
    }
}

return result with { ResultPayload = resultPayload };
```

This keeps the current contract intact: payloads are returned only after completed final status is known, and status-read failures remain advisory but observable.

### Scope Boundaries

- Do not surface result payloads when final status is unknown. The new warning is observability, not a behavior relaxation.
- Do not include `processingResult.ErrorMessage` in the new warning. Even if available, it can carry domain-controlled text and is not needed for this drop signal.
- The warning MUST include `Stage=ResultPayloadDropped`, and it MAY log only these fields: `CorrelationId`, `TenantId`, `AggregateId`, `CommandType`, `FinalStatus`, `StatusReadSucceeded`, and the stage. This is a closed allowlist; treat all other values as disallowed unless a reviewer explicitly approves them.
- Do not log, serialize, stringify, hash, measure, count, or otherwise derive metadata from `processingResult.ResultPayload`.
- Avoid high-cardinality free text. Use stable structured fields, not payload-derived strings or arbitrary metadata bags.
- Do not remove, rename, suppress, or change the existing `StatusReadForTrackingFailed` log. The new warning is additional observability for the payload-drop decision.
- Do not add a new `ProblemDetails` shape or controller behavior. This handler returns the raw string to `CommandsController`; ES-1 already owns parsing and safe degradation to JSON/null.
- Do not add packages or logging abstractions. Use `Microsoft.Extensions.Logging` source-generated `[LoggerMessage]`, matching the nested `Log` class already in `SubmitCommandHandler`.
- Do not update Parties code. The proposal states ES-3 has no Parties-side follow-up.

### Previous Story Intelligence

ES-1 (`post-epic-22-es1-result-payload-parse-dos-guard`) is complete. It added controller-side result-payload parsing defenses: a 64 KiB char-count cap, explicit `JsonDocumentOptions { MaxDepth = 64 }`, and no-payload warnings for oversized or malformed result payloads. ES-3 must not change controller parsing.

ES-2 (`post-epic-22-es2-pipeline-state-result-payload-privacy-posture`) is complete. It scrubbed `ResultPayload` from persisted actor `PipelineState` checkpoints while preserving the normal no-crash terminal return payload. ES-3 starts after that posture: the handler may receive a non-null in-memory payload from normal completion, but crash-resume payload replay is intentionally null.

ES-2 validation notes also matter here:

- `dotnet build Hexalith.EventStore.slnx --configuration Release` passed with 0 warnings and 0 errors on 2026-05-27.
- Full `Hexalith.EventStore.Server.Tests` had unrelated existing failures in health-check readiness/count and local admin DAPR access-control tests; do not attribute those to ES-3 unless the failing set changes and points at this handler.

### Git Intelligence

Recent commits show the repo is in focused post-Epic hardening mode:

- `2a9fd3e8 fix(server): harden result payload handling`
- `3bfd4895 fix: update package paths to use Unix-style format for consistency`
- `e887c068 fix(events): publish system tenant events on domain topic`
- `abf39b43 chore: update submodule commits for Hexalith.Commons and Hexalith.Tenants`
- `8aa20c18 Keycloak Dev Fast-Start: persistent container & port config`

Keep this work narrow and avoid refactoring surrounding pipeline code.

### Architecture And Project Context

Apply `_bmad-output/project-context.md` and `_bmad-output/planning-artifacts/architecture.md`:

- Structured command/event logs must carry `correlationId`; use source-generated logger patterns already present near the code.
- Never log event payload data, command payloads, secrets, or user-controllable display names as trusted identity. For ES-3, that prohibition includes the domain-service result payload string.
- Command status queries are tenant-scoped; status reads must keep the existing `(tenant, correlationId)` call shape.
- Command status writes and reads used for tracking are advisory; failures must not block command processing.
- Preserve the EventStore pipeline ordering and do not introduce custom error response shapes.
- Tests use xUnit v3, Shouldly, and NSubstitute. Run targeted test projects individually.

### Aspire Baseline

Before creating this story, the AppHost was started with:

`EnableKeycloak=false aspire run --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach --non-interactive --format Json --log-level Warning`

Observed baseline on 2026-05-27:

- AppHost build succeeded with 0 warnings and 0 errors.
- Dashboard was available at `https://localhost:17017/login?t=d56fb1341ea49a12d4fa862bcbd70806`.
- `aspire describe --format Json` showed core resources running and healthy, including `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, `statestore`, and related DAPR sidecars.
- Aspire emitted a warning while trying to stop a stale prior instance socket, then continued and started the detached apphost.

### Latest Technical Information

No version-sensitive package or integration change is required for ES-3. Current repo context pins .NET `10.0.300` / `net10.0`, Aspire CLI/AppHost family `13.3.x`, DAPR runtime `1.17.7`, and DAPR .NET packages `1.17.9`. Do not add or upgrade packages for this story.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-3`] - residual scope: add warning at the payload-drop site; log envelope metadata only; no Parties follow-up.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es1-result-payload-parse-dos-guard.md`] - prior controller parse hardening and no-payload logging boundaries.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es2-pipeline-state-result-payload-privacy-posture.md`] - prior actor checkpoint privacy posture and preserved no-crash terminal payload behavior.
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:80-92`] - final status read gate and existing `StatusReadForTrackingFailed` warning.
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:168-172`] - current payload return/drop expression to replace with explicit branch.
- [Source: `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs`] - actor result contract containing optional `ResultPayload`.
- [Source: `src/Hexalith.EventStore.Server/Pipeline/Commands/SubmitCommand.cs`] - handler result contract containing optional `ResultPayload`.
- [Source: `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs`] - `Completed` is the only status allowed to forward payload.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs`] - existing handler test style and no-trackers/no-read pin.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`] - existing payload-leak testing pattern.
- [Source: `_bmad-output/project-context.md`] - logging, testing, and advisory command-status rules.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Structured-Logging-Pattern`] - structured logging fields and never-log payload rule.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: `aspire describe --format Json` initially reported no running apphost.
- 2026-05-27: `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach --non-interactive --format Json --log-level Warning` started AppHost successfully; dashboard URL emitted and resources later described healthy.
- 2026-05-27: Red run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitCommandHandler"` failed as expected because `ResultPayloadDropped` was missing.
- 2026-05-27: First green run was blocked by the detached Aspire `eventstore` process locking `Hexalith.EventStore.Server.dll`; `aspire stop --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` released the lock.
- 2026-05-27: Green run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitCommandHandler"` passed: 18 passed.
- 2026-05-27: Payload-protection run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData"` passed: 1 passed.
- 2026-05-27: Code-review fix run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitCommandHandlerResultPayloadTests"` passed: 5 passed.
- 2026-05-27: Code-review fix payload-protection rerun `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData"` passed: 1 passed.
- 2026-05-27: Code-review fix broader handler rerun `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitCommandHandler"` passed: 19 passed.
- 2026-05-27: Code-review fix build rerun `dotnet build Hexalith.EventStore.slnx --configuration Release` passed with 0 warnings and 0 errors.
- 2026-05-27: `dotnet build Hexalith.EventStore.slnx --configuration Release` passed with 0 warnings and 0 errors.
- 2026-05-27: Broader `dotnet test tests/Hexalith.EventStore.Server.Tests` completed with new handler tests passing, but failed on existing unrelated health-check count, admin DAPR access-control, and DAPR scheduler fixture prerequisites.
- 2026-05-27: Standard unit project checks passed: `Hexalith.EventStore.Client.Tests` 399 passed, `Hexalith.EventStore.Contracts.Tests` 512 passed, `Hexalith.EventStore.Sample.Tests` 74 passed, `Hexalith.EventStore.Testing.Tests` 144 passed.

### Completion Notes List

- Added source-generated warning event 1107 `ResultPayloadDropped` at the explicit result-payload drop decision in `SubmitCommandHandler`.
- Preserved existing payload contract: non-null actor/domain result payloads are returned only when final status is known to be `Completed`; status-read failures and non-completed statuses still return null payloads.
- Preserved the existing `StatusReadForTrackingFailed` warning and advisory status-read semantics.
- Added focused tests covering completed payload preservation, non-completed payload drop logging, status-read exception logging, null-payload no-read behavior, and result-payload non-leakage.
- Code review clarified `StatusReadSucceeded`: the field now means the status-store read call completed successfully, even when the returned status record is null/missing.

### Implementation Plan

- Replace the inline return-payload ternary with a local `resultPayload` branch so the drop predicate is observable and testable.
- Add a source-generated warning with only closed allowlist metadata: correlation id, tenant id, aggregate id, command type, final status, and status-read success.
- Pin the behavior with tests that inspect event id, stage marker, structured fields, absence of extra application properties, and absence of sensitive result payload text.

### File List

- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerResultPayloadTests.cs`
- `_bmad-output/implementation-artifacts/post-epic-22-es3-result-payload-drop-observability.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Verification Status

Focused story verification passed. Broader server project test run has unrelated pre-existing/environmental failures outside this story's handler path.

- PASS: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitCommandHandler"` (19 passed).
- PASS: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitCommandHandlerResultPayloadTests"` (5 passed).
- PASS: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData"` (1 passed).
- PASS: `dotnet build Hexalith.EventStore.slnx --configuration Release` (0 warnings, 0 errors).
- PASS: `dotnet test tests/Hexalith.EventStore.Client.Tests` (399 passed).
- PASS: `dotnet test tests/Hexalith.EventStore.Contracts.Tests` (512 passed).
- PASS: `dotnet test tests/Hexalith.EventStore.Sample.Tests` (74 passed).
- PASS: `dotnet test tests/Hexalith.EventStore.Testing.Tests` (144 passed).
- KNOWN/UNRELATED FAIL: `dotnet test tests/Hexalith.EventStore.Server.Tests` reported 33 failures unrelated to ES-3: health-check registration/readiness count expectations, local admin DAPR access-control policy expectations, and DAPR scheduler-dependent integration fixture prerequisites.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-27 | 0.1 | Created ready-for-dev post-Epic-22 ES-3 story: add no-payload warning when `SubmitCommandHandler` drops a non-null result payload because final status is not known to be `Completed`. | Codex |
| 2026-05-27 | 0.2 | Applied party-mode review hardening: mandatory `ResultPayloadDropped` stage, explicit allowed log metadata, stronger payload non-leakage tests, and preservation requirement for `StatusReadForTrackingFailed`. | Codex |
| 2026-05-27 | 0.3 | Applied advanced elicitation hardening: exact drop predicate, status-read failure semantics (`FinalStatus=Unavailable`, `StatusReadSucceeded=false`), closed structured-property allowlist, source-generated warning constraint, and stricter no-payload-derived metadata tests. | Codex |
| 2026-05-27 | 1.0 | Implemented ES-3 result-payload drop warning, focused tests, validation evidence, and moved story to review. | Codex |
| 2026-05-27 | 1.1 | Applied code-review fix to distinguish successful null final-status reads from status-read failures, added regression coverage, and moved story to done. | Codex |

## Story Completion Status

Implementation complete. Code review complete. Story moved to done.
