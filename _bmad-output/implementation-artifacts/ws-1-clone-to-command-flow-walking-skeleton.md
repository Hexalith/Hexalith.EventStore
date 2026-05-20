# Story WS-1: Clone-to-Command Flow Walking Skeleton

Status: done

<!-- Source: _bmad-output/planning-artifacts/epics.md - Story WS-1 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer evaluating the foundation sequence,
I want the thinnest EventStore command path proven end to end,
so that foundation work remains anchored to observable user value.

## Story Context

WS-1 is the bootstrap/readiness slice that must be verified before another implementation pass over Epics 1-8. This is an evidence-first story: first prove the current clone-to-command path from a clean local run; only change product code, tests, or docs when the live evidence or existing Tier 3 coverage exposes a gap.

The current repository already contains much of the intended surface:

- AppHost topology defines `eventstore` and `sample` resources, with the sample domain service using the EventStore registration path and DAPR sidecar app id `sample`. [Source: src/Hexalith.EventStore.AppHost/Program.cs]
- `POST /api/v1/commands` is implemented in `CommandsController`; it returns `202 Accepted`, `Location: /api/v1/commands/status/{correlationId}`, `Retry-After: 1`, and a body containing the correlation id. [Source: src/Hexalith.EventStore/Controllers/CommandsController.cs]
- `GET /api/v1/commands/status/{correlationId}` is implemented in `CommandStatusController`, tenant-scoped from JWT claims. [Source: src/Hexalith.EventStore/Controllers/CommandStatusController.cs]
- The Counter sample handles fieldless `IncrementCounter` commands and emits `CounterIncremented`. [Source: samples/Hexalith.EventStore.Sample/Counter/Commands/IncrementCounter.cs; samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs]
- Existing Tier 3 tests already exercise the live signed command path and command lifecycle. Reuse or extend these instead of adding duplicate AppHost fixtures. [Source: tests/Hexalith.EventStore.IntegrationTests/ContractTests/LiveCommandSurfaceSmokeTests.cs; tests/Hexalith.EventStore.IntegrationTests/ContractTests/CommandLifecycleTests.cs]

The canonical WS-1 local topology is the repo-documented dev mode: `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`. In this mode the Command API listens on `http://localhost:8080`, authentication uses the development symmetric JWT configuration, and the developer can verify the flow without the Keycloak container.

## Acceptance Criteria

1. Given a clean checkout and prerequisites installed, when the AppHost starts in the documented dev mode, then the EventStore API and one sample domain service are both running and healthy enough to receive requests.
2. Given the AppHost is running, when a signed sample `IncrementCounter` command is submitted through `POST /api/v1/commands`, then the API returns `202 Accepted` with a non-empty correlation id, `Location` status URL, and `Retry-After: 1`.
3. Given the submitted command correlation id, when `GET /api/v1/commands/status/{correlationId}` is polled, then the command reaches `Completed` within the story's timeout and reports `eventCount >= 1`.
4. Given the command lifecycle has run, when Aspire logs or traces are inspected, then at least one structured log entry or distributed trace can be filtered by the same correlation id.
5. The story records durable evidence under `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/`, including resource state, request/response summary, final status payload, correlation id, and log/trace evidence.
6. Existing or added automated coverage pins the walking skeleton. If existing tests already satisfy AC #1-#3, do not duplicate them; update names/assertions only when needed to make the WS-1 intent explicit.
7. `_bmad-output/implementation-artifacts/sprint-status.yaml` remains the sprint source of truth and advances this story only after evidence and validation are recorded.

## Tasks / Subtasks

- [x] Task 1 - Establish the local baseline (AC: #1)
  - [x] Confirm prerequisites from project context: .NET 10 SDK, Docker, Aspire CLI, DAPR CLI/runtime.
  - [x] If running in slim DAPR mode, start placement and scheduler before AppHost startup.
  - [x] Start the topology with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`.
  - [x] Use Aspire MCP `list_apphosts` and `list_resources` to capture `eventstore` and `sample` resource state.

- [x] Task 2 - Submit the thinnest live command (AC: #2)
  - [x] Mint a dev JWT using the existing integration-test helper/configuration pattern: issuer `hexalith-dev`, audience `hexalith-eventstore`, signing key `DevOnlySigningKey-AtLeast32Chars!`, tenant `tenant-a`, domain `counter`, permissions covering command submit/query.
  - [x] Submit one fresh command to `POST /api/v1/commands` using the existing request contract: unique `messageId`, `tenant=tenant-a`, `domain=counter`, unique `aggregateId`, `commandType=IncrementCounter`, and payload `{}`.
  - [x] Verify the response is `202 Accepted` and capture the correlation id, `Location`, `Retry-After`, and body.

- [x] Task 3 - Observe completion and persisted event count (AC: #3)
  - [x] Poll `GET /api/v1/commands/status/{correlationId}` with the same JWT until terminal status or timeout.
  - [x] Assert terminal status is `Completed`, not `Rejected`, `PublishFailed`, or `TimedOut`.
  - [x] Assert final status reports `eventCount >= 1`.
  - [x] If status evidence is ambiguous, use the existing stream-read surface for the same tenant/domain/aggregate as stronger persistence proof instead of inventing a new endpoint.

- [x] Task 4 - Prove correlation in telemetry (AC: #4, #5)
  - [x] Use Aspire MCP `list_traces`, `list_trace_structured_logs`, and/or `list_structured_logs` for the `eventstore` resource to find the submitted correlation id.
  - [x] Record at least one matching trace or structured log entry in the evidence artifact.
  - [x] Do not include JWTs, command payload data beyond the minimal command shape, connection strings, or secrets in evidence.

- [x] Task 5 - Reuse and tighten automated coverage (AC: #6)
  - [x] First run the existing Tier 3 command-flow coverage: `LiveCommandSurfaceSmokeTests` and `CommandLifecycleTests`.
  - [x] If those tests already assert resource startup, signed command submission, status completion, and `eventCount >= 1`, record them as WS-1 coverage without duplicating fixtures.
  - [x] If any WS-1 AC is not pinned, extend the existing test classes or fixture rather than spinning up a second AppHost.
  - [x] Keep integration tests in the existing `[Collection("AspireContractTests")]` fixture pattern.

- [x] Task 6 - Close evidence and bookkeeping (AC: #5, #7)
  - [x] Create `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/command-flow-evidence-YYYY-MM-DD.md`.
  - [x] Include command used, resource snapshot summary, correlation id, POST response summary, final status response, telemetry match, tests run, and any known environment caveats.
  - [x] Move this story from `ready-for-dev` to `review` in `_bmad-output/implementation-artifacts/sprint-status.yaml` only after implementation evidence is complete.

## Dev Notes

### Existing APIs and Contracts

- Command submit route: `POST /api/v1/commands`. Request fields are `messageId`, `tenant`, `domain`, `aggregateId`, `commandType`, `payload`, optional `correlationId`, optional `extensions`. [Source: src/Hexalith.EventStore/Models/SubmitCommandRequest.cs]
- Status route: `GET /api/v1/commands/status/{correlationId}`. The status controller resolves tenant scope from JWT tenant claims and adds `Retry-After: 1` for non-terminal statuses. [Source: src/Hexalith.EventStore/Controllers/CommandStatusController.cs]
- OpenAPI examples already include an `IncrementCounter` command. Treat them as request-shape guidance, but prefer existing integration-test helpers for exact signing and polling behavior. [Source: src/Hexalith.EventStore/OpenApi/CommandExampleTransformer.cs]
- Optional persistence cross-check route: `POST /api/v1/streams/read`. Use only if final command status does not give sufficient evidence. [Source: src/Hexalith.EventStore/Controllers/StreamsController.cs]

### Auth and Environment Constraints

- Use `EnableKeycloak=false` for WS-1 unless deliberately validating the Keycloak path. This matches repository instructions and the current `AspireContractTestFixture`.
- Development JWTs must include issuer `hexalith-dev`, audience `hexalith-eventstore`, `tenants` JSON array, and command permissions. Existing tests use `TestJwtTokenGenerator`; reuse that pattern rather than hand-building security code. [Source: tests/Hexalith.EventStore.IntegrationTests/Helpers/TestJwtTokenGenerator.cs]
- Do not log or paste JWTs, secrets, connection strings, full command/event payloads, or protected data into evidence artifacts.
- DAPR slim mode may require manual placement and scheduler startup before `aspire run`. If that affects verification, record it as an environment caveat, not as product evidence.

### Testing Standards

- Run test projects individually; do not use a broad solution-level `dotnet test` as the story validation shortcut.
- Primary WS-1 test target:

```powershell
dotnet test tests/Hexalith.EventStore.IntegrationTests --filter "FullyQualifiedName~LiveCommandSurfaceSmokeTests|FullyQualifiedName~CommandLifecycleTests"
```

- If product code changes are required, also run the relevant unit test project(s) individually. The project context notes that `Hexalith.EventStore.Server.Tests` has a pre-existing CA2007 warning-as-error build failure; do not treat unrelated existing failures as WS-1 regressions, but record them if encountered.
- Integration tests require Docker and a running-compatible Aspire/DAPR environment.

### Project Structure Notes

- Evidence belongs under `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/`.
- Story bookkeeping belongs only in `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- Do not add new endpoint surfaces for this story unless the existing command/status/stream-read surfaces cannot prove the ACs.
- Do not initialize nested git submodules. Root-level submodule handling only, and only when needed.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story WS-1: Clone-to-Command Flow Walking Skeleton]
- [Source: _bmad-output/planning-artifacts/prd.md#Functional Requirements]
- [Source: _bmad-output/planning-artifacts/architecture.md#Command Submission and Processing]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#API-as-UX]
- [Source: _bmad-output/project-context.md]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs]
- [Source: src/Hexalith.EventStore/Controllers/CommandsController.cs]
- [Source: src/Hexalith.EventStore/Controllers/CommandStatusController.cs]
- [Source: samples/Hexalith.EventStore.Sample/Counter/Commands/IncrementCounter.cs]
- [Source: samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs]
- [Source: tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs]
- [Source: tests/Hexalith.EventStore.IntegrationTests/Helpers/TestJwtTokenGenerator.cs]
- [Source: tests/Hexalith.EventStore.IntegrationTests/ContractTests/LiveCommandSurfaceSmokeTests.cs]
- [Source: tests/Hexalith.EventStore.IntegrationTests/ContractTests/CommandLifecycleTests.cs]

### Review Findings

- [x] [Review][Decision] D1 — AC #1 not fully closed: documented `EnableKeycloak=false aspire run ...` command was blocked by local Docker Desktop HTTP 500 before and after the PrerequisiteValidator fix; no clean re-run without `SKIP_PREREQUISITE_CHECK=true` was recorded in evidence. → Decision: re-run required; story moved to in-progress.
- [x] [Review][Decision] D2 — Evidence-first scope: `IPrerequisiteCommandRunner` interface, `ProcessPrerequisiteCommandRunner` inner class, `InternalsVisibleTo` in AssemblyInfo.cs, and new `PrerequisiteValidatorTests.cs` were added as structural testability scaffolding beyond the minimal probe argument change. → Decision: scope accepted as justified quality improvement.
- [x] [Review][Patch] P1 — Stderr pipe deadlock and stdout read-after-wait. → Applied in uncommitted working-tree changes: concurrent `ReadToEndAsync()` for both stdout and stderr pipes started before `WaitForExit`. [src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs]
- [x] [Review][Patch] P2 — Kill-then-dispose race: `process.Kill(entireProcessTree: true)` is asynchronous; `Dispose()` could be called before the process exits, leaking OS handles. → Applied: added `process.WaitForExit()` after `Kill` on the timeout path. [src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs]
- [x] [Review][Patch] P3 — `JsonElement.Undefined` not suppressed by `WhenWritingNull`. → Applied: defensive Undefined guard added in `ParseOptionalResultPayload`. [src/Hexalith.EventStore/Controllers/CommandsController.cs]
- [x] [Review][Patch] P4 — Test timeout assertion too loose (`>= 90s` instead of `== CommandTimeout`). → Applied. [tests/Hexalith.EventStore.IntegrationTests/Configuration/PrerequisiteValidatorTests.cs]
- [x] [Review][Patch] P5 — Missing test coverage for Docker-fail, DAPR-not-installed, DAPR-not-initialized paths. P5(d) serialization test already present in SubmitCommandResponseTests.cs. → Applied: three new test cases added with `ScriptedRunner` stub. [tests/Hexalith.EventStore.IntegrationTests/Configuration/PrerequisiteValidatorTests.cs]
- [x] [Review][Patch] P6 — `PrerequisiteValidatorTests` belongs in a Tier 1 unit test project, not Tier 3. Requires creating a new `Hexalith.EventStore.AppHost.Tests` project and updating `InternalsVisibleTo`. → Applied: created `Hexalith.EventStore.AppHost.Tests`, moved the prerequisite validator tests out of Tier 3 integration tests, and updated `InternalsVisibleTo`. [tests/Hexalith.EventStore.AppHost.Tests/Configuration/PrerequisiteValidatorTests.cs; src/Hexalith.EventStore.AppHost/Properties/AssemblyInfo.cs]
- [x] [Review][Defer] W1 — 240 s worst-case startup gate with no user feedback between the two probes — pre-existing behavioral characteristic; not a defect introduced by this PR [src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs] — deferred, pre-existing
- [x] [Review][Defer] W2 — `InternalsVisibleTo` declared without strong-name token — pre-existing project practice (no strong naming in this codebase); minor surface-area note [src/Hexalith.EventStore.AppHost/Properties/AssemblyInfo.cs] — deferred, pre-existing
- [x] [Review][Defer] W3 — `Kill(entireProcessTree: true)` may terminate shared CI infrastructure processes — pre-existing concern for any process-killing probe; acceptable for a dev-only startup gate [src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs] — deferred, pre-existing
- [x] [Review][Defer] W4 — `Models.SubmitCommandResponse` redeclares `ResultPayload` without inheriting `JsonIgnore`; active controller path uses `Contracts.Commands.SubmitCommandResponse` directly so no runtime impact today, but the wrapper is a latent attribution gap [src/Hexalith.EventStore/Models/SubmitCommandResponse.cs] — deferred, pre-existing
- [x] [Review][Patch] P7 — `stderrTask` never awaited on success path; both tasks abandoned on timeout path. If `dapr --version` writes to stderr, `daprResult.Output` is empty string → false "DAPR runtime is not initialized" error blocking AppHost startup. On timeout: `Kill`+`WaitForExit` called but neither task is awaited, leaving background reads racing against `Dispose()`. → Applied: await both tasks on success path (stdout first, stderr fallback); drain both on timeout path via `Task.WhenAll`. [src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs]
- [x] [Review][Patch] P8 — `SlowDockerProbeRunner` threshold hardcoded at 90 s but `CommandTimeout` is 120 s; test passes only because 120 ≥ 90. If `CommandTimeout` were reduced to 60 s, `Success` returns false while `errors.ShouldBeEmpty()` still fires — a false-pass. → Applied: threshold derived from `PrerequisiteValidator.CommandTimeout - TimeSpan.FromSeconds(1)`. [tests/Hexalith.EventStore.AppHost.Tests/Configuration/PrerequisiteValidatorTests.cs]
- [x] [Review][Patch] P9 — `ScriptedRunner` ignores its `timeout` parameter and routes every non-`"docker"` command to the `dapr` result; a future third prerequisite check would silently receive the dapr stub, and timeout-boundary contracts are never exercised by any existing test. → Applied: replaced ternary with explicit `switch` expression; unknown commands return a failure with a diagnostic message. [tests/Hexalith.EventStore.AppHost.Tests/Configuration/PrerequisiteValidatorTests.cs]
- [x] [Review][Patch] P10 — No test covers the both-fail scenario (Docker and DAPR both missing simultaneously); a regression that short-circuits on the first failure would not be caught since all existing tests assert `ShouldHaveSingleItem()`. → Applied: added `GetMissingPrerequisites_WhenBothFail_ReturnsTwoErrors` test. [tests/Hexalith.EventStore.AppHost.Tests/Configuration/PrerequisiteValidatorTests.cs]
- [x] [Review][Patch] P11 — `IPrerequisiteCommandRunner` interface declaration uses K&R brace style (opening brace on same line), violating the Allman convention mandated by `.editorconfig`. → Applied: moved opening brace to new line. [src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs]
- [x] [Review][Defer] W5 — `Guid.TryParse` in `CorrelationIdMiddlewareTests.cs` lines 27 and 67 violates the ULID ID validation rule (CLAUDE.md R2-A7); pre-existing, not introduced by this story [tests/Hexalith.EventStore.IntegrationTests/Middleware/CorrelationIdMiddlewareTests.cs] — deferred, pre-existing
- [x] [Review][Defer] W6 — `Guid.TryParse` + `Guid.NewGuid()` in `Admin.Server.Host` correlation middleware production code violates the ULID ID validation rule; pre-existing, not touched by this story [src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs] — deferred, pre-existing
- [x] [Review][Defer] W7 — No unit tests cover `Validate()` env-var bypass paths (`CI`, `SKIP_PREREQUISITE_CHECK`, `DOTNET_RUNNING_IN_CONTAINER`, `PUBLISH_TARGET`); `Validate()` was not refactored for testability in this story [src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs] — deferred, pre-existing

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/command-flow-evidence-2026-05-20.md`
- `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/aspire-run-documented-enablekeycloak-false-2026-05-20.out.log`
- `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/aspire-run-documented-enablekeycloak-false-2026-05-20.err.log`
- `C:\Users\JeromePiquot\.aspire\logs\cli_20260520T101445_5a5ce1f5.log`
- `C:\Users\JeromePiquot\.aspire\logs\cli_20260520T110928354_detach-child_5822811aa5864e63a14ccef2f69aa7a4.log`
- Diagnostic runtime proof logs referenced in the evidence artifact, including status trace `c0c24dff863d4ddca09633b7061e7229` and command trace `efdfff7ddfecb4794267625ee1d0c449`.

### Implementation Plan

1. Capture baseline prerequisite and AppHost behavior before making product changes.
2. Add a focused regression around the slow Docker prerequisite probe, then update the validator to use a lighter server-version check with a longer timeout.
3. Preserve the accepted-command response contract by omitting null `resultPayload` values.
4. Re-run the existing WS-1 command-flow integration coverage and relevant unit projects individually.
5. Record manual command/status/telemetry evidence and move the story to review.

### Completion Notes List

- Verified prerequisites and captured the local Docker Desktop health failure that blocks the final exact documented CLI run.
- Captured diagnostic Aspire runtime evidence for healthy `eventstore`, `sample`, `pubsub`, and `statestore` resources after bypassing only the local prerequisite preflight.
- Submitted a live `IncrementCounter` command and observed `202 Accepted`, final `Completed` status, and `eventCount=1`.
- Captured correlation evidence through Aspire traces and EventStore structured logs.
- Added prerequisite validator regression coverage and tightened the Docker probe to avoid false negatives from slow `docker info` calls.
- Fixed `SubmitCommandResponse` JSON serialization so accepted-command responses omit null `resultPayload` while preserving future enriched responses.
- Recorded validation commands and caveats in the evidence artifact.
- Resolved review patch P6 by moving prerequisite validator regression tests into a dedicated Tier 1 AppHost test project and narrowing `InternalsVisibleTo` to that test assembly.
- Re-ran the documented `EnableKeycloak=false` AppHost path without `SKIP_PREREQUISITE_CHECK=true`; `eventstore`, `sample`, `pubsub`, and `statestore` were running and healthy, then the AppHost was stopped cleanly.

### File List

- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/ws-1-clone-to-command-flow-walking-skeleton.md`
- `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/command-flow-evidence-2026-05-20.md`
- `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/aspire-run*.log`
- `Hexalith.EventStore.slnx`
- `src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs`
- `src/Hexalith.EventStore.AppHost/Properties/AssemblyInfo.cs`
- `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandResponse.cs`
- `tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj`
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/PrerequisiteValidatorTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Configuration/PrerequisiteValidatorTests.cs` (deleted)

### Change Log

- 2026-05-20: Moved WS-1 into implementation, captured command-flow evidence, fixed the AppHost prerequisite probe, preserved accepted-command response JSON shape, and moved WS-1 to review.
- 2026-05-20: Addressed review patch P6 by moving prerequisite validator tests to Tier 1 AppHost tests, reran validation, captured clean documented AppHost startup, and returned WS-1 to review.
