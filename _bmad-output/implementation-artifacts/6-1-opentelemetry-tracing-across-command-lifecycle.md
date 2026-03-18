# Story 6.1: OpenTelemetry Tracing Across Command Lifecycle

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Stories in Epics 1-5 must be completed before this verification runs.**

**Note:** This is a **verification story**. The OpenTelemetry tracing infrastructure is already fully implemented across previous stories (old epic numbering: Story 6.1 "End-to-End OpenTelemetry Trace Instrumentation", plus incremental work in Epics 2-4). This story formally verifies the complete tracing model against the new Epic 6 acceptance criteria and fills any remaining gaps. If verification uncovers a non-trivial issue (architectural flaw, missing trace chain, or change requiring > 30 minutes of work), STOP and escalate to the user rather than fixing inline.

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` (OpenTelemetry configuration with dual ActivitySource registration)
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` (centralized ActivitySource with 13 activity names and 10+ tag constants)
- `src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs` (CommandApi ActivitySource with Submit, QueryStatus, Replay)
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` (MediatR outermost behavior with Submit activity)
- `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` (Correlation ID generation/propagation)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (5-step pipeline with OTel activities per stage + traceparent fallback)
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (Publication with ActivityKind.Producer)
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` (Dead-letter with ActivityKind.Producer)
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` (QueryStatus activity)
- `src/Hexalith.EventStore.CommandApi/Controllers/ReplayController.cs` (Replay activity)

Run `dotnet test tests/Hexalith.EventStore.Server.Tests/` to confirm all existing tests pass before beginning.

## Story

As an **operator**,
I want distributed traces spanning the full command lifecycle (Received -> Processing -> EventsStored -> EventsPublished -> Completed),
So that I can visualize the complete pipeline from submission to completion in the Aspire dashboard (FR35, UX-DR24).

**Note:** This is a **verification story**. The complete OpenTelemetry trace instrumentation was built incrementally through Epics 2-4 and formally completed in old Story 6.1 ("End-to-End OpenTelemetry Trace Instrumentation"). This story verifies the full implementation against the new Epic 6 acceptance criteria, confirms FR35/NFR31/Rule 9 compliance, and fills any remaining test gaps. Expected new code: 0 lines. Expected new tests: 0 (unless gaps discovered).

## Acceptance Criteria

1. **Single distributed trace spans all pipeline stages** -- Given a command is submitted and processed through the full pipeline, When I view traces in the Aspire dashboard (or Jaeger, Grafana/Tempo), Then a single distributed trace spans all stages: API receipt, MediatR pipeline, actor activation, domain invocation, event persistence, event publication (FR35), And no trace gaps exist between the API layer and actor processing layer, And traces are visible in the Aspire Traces tab (UX-DR24).

2. **Named activities match architecture conventions** -- Given the architecture specifies activity naming patterns, When traces are collected, Then each stage has a named activity matching the `EventStore.{Component}.{Action}` pattern: `EventStore.CommandApi.Submit` (API layer), `EventStore.Actor.ProcessCommand` (actor outer span), `EventStore.Actor.IdempotencyCheck`, `EventStore.Actor.TenantValidation`, `EventStore.Actor.StateRehydration`, `EventStore.DomainService.Invoke`, `EventStore.Events.Persist`, `EventStore.Events.Publish`, And dead-letter operations use `EventStore.Events.PublishDeadLetter`, And drain recovery uses `EventStore.Events.Drain`.

3. **Trace context propagates across all spans including DAPR actor proxy** -- Given a command flows through API -> MediatR -> Actor -> DomainService -> EventPersist -> EventPublish, When trace context (correlation ID, causation ID) is set at the API layer, Then all child spans inherit the parent trace context, And the correlation ID is present as a tag on every activity in the trace (Rule 9), And W3C Trace Context headers propagate across DAPR service invocation boundaries automatically, And when `Activity.Current` does NOT flow through the actor proxy, explicit traceparent fallback via `CommandEnvelope.Extensions` restores the parent context.

4. **All activity tags follow semantic conventions** -- Given an activity is created for any pipeline stage, When tags are set, Then tags use the `eventstore.*` namespace: `eventstore.correlation_id`, `eventstore.tenant_id`, `eventstore.domain`, `eventstore.aggregate_id`, `eventstore.command_type`, `eventstore.event_count`, `eventstore.topic`, And success/failure status is recorded via `ActivityStatusCode.Ok` or `ActivityStatusCode.Error`.

5. **Both ActivitySources registered in ServiceDefaults** -- Given `EventStoreActivitySource.SourceName` is `"Hexalith.EventStore"` and `EventStoreActivitySources.CommandApi` is `"Hexalith.EventStore.CommandApi"`, When `ConfigureOpenTelemetry` runs, Then `.AddSource("Hexalith.EventStore")` and `.AddSource("Hexalith.EventStore.CommandApi")` are both registered, And all server-level activities (actor pipeline, event persistence, publication, dead-letter, drain) appear in collected traces.

6. **Traces exportable to any OTLP-compatible collector** -- Given the system runs with an `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable set, When traces are exported, Then they are sent to the configured OTLP endpoint (NFR31), And the Aspire dashboard displays the full distributed trace, And Jaeger/Grafana/Tempo can visualize the same traces.

7. **Each activity includes the correlationId** -- Given any pipeline stage creates an activity, When tags are set, Then `eventstore.correlation_id` is present on every activity in the trace (Rule 9), And event payload data never appears as a tag (Rule 5, SEC-5, NFR12).

8. **CommandApi controller activities for all three endpoints** -- Given the CommandApi has three endpoints, When each endpoint handles a request, Then `EventStore.CommandApi.Submit` spans the POST `/commands` flow (via LoggingBehavior, not duplicated in controller), And `EventStore.CommandApi.QueryStatus` spans the GET `/commands/status/{id}` flow, And `EventStore.CommandApi.Replay` spans the POST `/commands/replay/{id}` flow, And each activity includes correlation ID and tenant ID tags.

9. **Comprehensive trace test coverage** -- Given trace instrumentation is critical for operations, When tests run, Then tests verify ActivitySource registration for both sources, And tests verify activity creation with correct names for each pipeline stage, And tests verify required tags (correlation ID, tenant ID) on activities, And tests verify ActivityStatusCode on success/failure paths, And tests verify distributed trace propagation across actor proxy boundary.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and baseline (BLOCKING)
  - [x] 0.1 Run all Tier 1 tests -- confirm all pass (baseline: >= 659)
  - [x] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` -- record actual pass count as baseline. **Baseline note:** Use the actual count from this run. Do NOT reconcile with historical baselines from other stories.
  - [x] 0.3 Inventory existing OpenTelemetry source files and confirm all 10 prerequisite files exist (see Prerequisites section)
  - [x] 0.4 Inventory existing telemetry test files and counts:
    - `EventStoreActivitySourceTests.cs` -- count tests
    - `CommandApiTraceTests.cs` -- count tests
    - `EndToEndTraceTests.cs` -- count tests
    - `OpenTelemetryRegistrationTests.cs` -- count tests
    - `DeadLetterTraceChainTests.cs` -- count tests

- [x] Task 1: Verify dual ActivitySource registration in ServiceDefaults (AC: #5)
  - [x] 1.1 Read `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`
  - [x] 1.2 Confirm `.AddSource("Hexalith.EventStore")` is registered alongside `.AddSource("Hexalith.EventStore.CommandApi")`
  - [x] 1.3 Confirm `AddAspNetCoreInstrumentation()` with health-check exclusion filters (/health, /alive, /ready)
  - [x] 1.4 Confirm `AddHttpClientInstrumentation()` is present
  - [x] 1.5 Confirm OTLP exporter is conditional on `OTEL_EXPORTER_OTLP_ENDPOINT` env var (AC #6)

- [x] Task 2: Verify Server ActivitySource completeness (AC: #2, #4)
  - [x] 2.1 Read `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs`
  - [x] 2.2 Confirm SourceName is `"Hexalith.EventStore"`
  - [x] 2.3 Confirm all architecture-required activity names exist: ProcessCommand, IdempotencyCheck, TenantValidation, StateRehydration, DomainService.Invoke, Events.Persist, Events.Publish, Events.Drain, Events.PublishDeadLetter, BackpressureCheck, StateMachineTransition
  - [x] 2.4 Confirm all tag key constants follow `eventstore.*` namespace: correlation_id, tenant_id, domain, aggregate_id, command_type, event_count, topic, exception_type, failure_stage, deadletter_topic

- [x] Task 3: Verify CommandApi ActivitySources (AC: #8)
  - [x] 3.1 Read `src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs`
  - [x] 3.2 Confirm SourceName is `"Hexalith.EventStore.CommandApi"`
  - [x] 3.3 Confirm activity name constants: Submit, QueryStatus, Replay
  - [x] 3.4 Read `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` -- confirm Submit activity created here (NOT in CommandsController) to avoid duplication
  - [x] 3.5 Read `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` -- confirm QueryStatus activity with `ActivityKind.Server`, tags, status codes
  - [x] 3.6 Read `src/Hexalith.EventStore.CommandApi/Controllers/ReplayController.cs` -- confirm Replay activity with `ActivityKind.Server`, tags, status codes

- [x] Task 4: Verify AggregateActor pipeline activities (AC: #1, #2, #3)
  - [x] 4.1 Read `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
  - [x] 4.2 Confirm ProcessCommand outer activity (`ActivityKind.Internal`) with all common tags
  - [x] 4.3 Confirm child activities for all 5 pipeline steps: IdempotencyCheck, TenantValidation, StateRehydration, DomainService.Invoke, Events.Persist
  - [x] 4.4 Confirm `SetActivityTags()` helper sets: correlation_id, tenant_id, domain, aggregate_id, command_type
  - [x] 4.5 Confirm `ActivityStatusCode.Ok` on success and `ActivityStatusCode.Error` + `AddException()` on failure for all activities
  - [x] 4.6 **CRITICAL:** Confirm traceparent fallback mechanism -- when `Activity.Current` is null (DAPR actor proxy boundary), verify that traceparent/tracestate are read from `CommandEnvelope.Extensions` and parent context is restored (AC #3)

- [x] Task 5: Verify EventPublisher and DeadLetterPublisher activities (AC: #1, #2)
  - [x] 5.1 Read `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` -- confirm `EventStore.Events.Publish` activity with `ActivityKind.Producer`, tags (correlation_id, tenant_id, domain, aggregate_id, event_count, topic), and status codes
  - [x] 5.2 Read `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` -- confirm `EventStore.Events.PublishDeadLetter` activity with `ActivityKind.Producer`, tags (correlation_id, tenant_id, domain, aggregate_id, command_type, failure_stage, exception_type, deadletter_topic), and status codes
  - [x] 5.3 Confirm both publishers set `ActivityStatusCode.Error` + `AddException()` on failure paths

- [x] Task 6: Verify CorrelationId middleware (AC: #7)
  - [x] 6.1 Read `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs`
  - [x] 6.2 Confirm: accepts `X-Correlation-ID` header, validates GUID format, generates new GUID if missing/invalid, stores in `HttpContext.Items["CorrelationId"]`, echoes in response headers
  - [x] 6.3 Confirm LoggingBehavior extracts correlation ID from HttpContext or generates fallback GUID

- [x] Task 7: Verify trace test coverage (AC: #9)
  - [x] 7.1 Review `tests/Hexalith.EventStore.Server.Tests/Telemetry/OpenTelemetryRegistrationTests.cs` -- confirm tests for dual ActivitySource registration, naming patterns, tag namespace
  - [x] 7.2 Review `tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs` -- confirm tests for ProcessCommand activity, child activities, tags, status codes, traceparent fallback
  - [x] 7.3 Review `tests/Hexalith.EventStore.Server.Tests/Telemetry/CommandApiTraceTests.cs` -- confirm tests for Submit, QueryStatus, Replay activities
  - [x] 7.4 Review `tests/Hexalith.EventStore.Server.Tests/Telemetry/EventStoreActivitySourceTests.cs` -- confirm tests for activity creation, tag setting, naming conventions
  - [x] 7.5 Review `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterTraceChainTests.cs` -- confirm tests for dead-letter trace chain, trace ID continuity, error status propagation
  - [x] 7.6 Identify test gaps. Potential gaps to check:
    - [x] 7.6.1 Coverage assessment: Drain activity (`EventStore.Events.Drain`) -- verify direct test coverage or document justified indirect coverage
    - [x] 7.6.2 Coverage assessment: BackpressureCheck activity -- verify direct test coverage or document justified indirect coverage
    - [x] 7.6.3 Coverage assessment: StateMachineTransition activity -- verify direct test coverage or document justified indirect coverage
    - [x] 7.6.4 Review: Rule 5 compliance -- confirm no test or production code sets event payload data as activity tags

- [x] Task 8: Final verification
  - [x] 8.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings, zero errors
  - [x] 8.2 Run all Tier 1 tests -- confirm pass count (baseline: >= 659)
  - [x] 8.3 Run all Tier 2 tests -- confirm pass count (baseline: use actual count from Task 0.2)
  - [x] 8.4 Confirm all 9 acceptance criteria are satisfied
  - [x] 8.5 Report final test count delta
  - [x] 8.6 Write verification report in Completion Notes (test inventory per file, gaps found, new tests added, deviations observed)

**Effort guidance:** The OpenTelemetry tracing infrastructure is already comprehensively implemented from old Story 6.1 and incremental work in Epics 2-4. Expect ~30-45 minutes for verification. This is primarily a read-verify story -- **expected new tests: 0** unless gaps discovered. Most tasks are file reads confirming existing code matches ACs. Write zero code unless a genuine gap is found. If you find a non-trivial issue, STOP and escalate.

## Dev Notes

### CRITICAL: This is a Verification Story

The OpenTelemetry tracing infrastructure is **already fully implemented** across previous stories:
- **Old Story 6.1** ("End-to-End OpenTelemetry Trace Instrumentation", status: done) completed the full tracing model:
  - Added `.AddSource("Hexalith.EventStore")` to ServiceDefaults (the critical fix making server activities visible)
  - Added QueryStatus and Replay controller activities
  - Added ActivityKind correctness on all activities (Server, Internal, Producer)
  - Added ActivityStatusCode completeness on all activities
  - Added `Activity.AddException()` on all failure paths
  - Added traceparent fallback via `CommandEnvelope.Extensions` for DAPR actor proxy boundary
  - Created 19 new telemetry tests across 3 test files
  - Post-review follow-up: explicit `ActivityKind` assignments and focused test validation (716/716 passing)
- **Epics 2-4** added activities incrementally at each layer:
  - Story 2.1: AggregateActor pipeline activities
  - Story 3.11: EventStoreActivitySource centralized naming
  - Story 4.1: EventPublisher activity
  - Story 4.3: BackpressureCheck activity
  - Story 4.4: Drain activity
  - Story 4.5: DeadLetterPublisher activity with trace chain tests

This story formally verifies the COMPLETE tracing model against the new Epic 6 acceptance criteria.

### Architecture Compliance

- **FR35:** Complete OpenTelemetry trace instrumentation spanning full command lifecycle (named activities per architecture pattern)
- **NFR2:** Trace instrumentation adds minimal overhead (within 200ms e2e budget) -- `Activity.IsAllDataRequested` checked for expensive operations
- **NFR31:** Traces exportable to any OTLP-compatible collector (Aspire dashboard, Jaeger, Grafana/Tempo)
- **UX-DR24:** Traces visible in the Aspire Traces tab
- **Rule 5:** Never log event payload data (SEC-5, NFR12) -- activity tags must not include payload data
- **Rule 9:** correlationId in every structured log entry and OpenTelemetry activity
- **Architecture OpenTelemetry Activity Naming:**
  - Command API: `EventStore.CommandApi.{verb}` (Submit, QueryStatus, Replay)
  - Actor processing: `EventStore.Actor.{operation}` (ProcessCommand, IdempotencyCheck, TenantValidation, StateRehydration, BackpressureCheck, StateMachineTransition)
  - Domain invocation: `EventStore.DomainService.Invoke`
  - Event persistence: `EventStore.Events.Persist`
  - Event publishing: `EventStore.Events.Publish`
  - Dead-letter: `EventStore.Events.PublishDeadLetter`
  - Drain: `EventStore.Events.Drain`

### Critical Design Decisions (Already Implemented)

- **Two ActivitySources, one trace.** `"Hexalith.EventStore.CommandApi"` handles API-layer activities. `"Hexalith.EventStore"` handles server/actor-layer activities. Both registered in ServiceDefaults. Activities join into a single distributed trace via W3C Trace Context.

- **No duplicate Submit activity.** `LoggingBehavior.cs` creates `EventStore.CommandApi.Submit` (outermost MediatR pipeline behavior). `CommandsController.cs` does NOT create a second Submit activity.

- **DAPR handles cross-boundary trace propagation automatically.** `DaprClient.InvokeMethodAsync` and `DaprClient.PublishEventAsync` propagate W3C `traceparent`/`tracestate` headers via sidecar-to-sidecar communication.

- **Traceparent fallback for actor proxy boundary.** When `Activity.Current` is null after DAPR actor proxy invocation, `AggregateActor` reads traceparent/tracestate from `CommandEnvelope.Extensions` and restores the parent context. This is written by `SubmitCommandExtensions` in the CommandApi layer.

- **ActivityKind semantics.** `Server` for incoming requests (controller actions). `Internal` for in-process operations (actor pipeline stages). `Producer` for event publication and dead-letter. These semantics drive correct trace visualization in tools like Jaeger/Grafana.

- **.NET 10 native `Activity.AddException()`.** Used instead of deprecated OpenTelemetry `RecordException()` extension -- no external package dependency needed.

### Key Source Files

| File | Purpose |
|------|---------|
| `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` | OpenTelemetry config: dual ActivitySource registration, ASP.NET Core + HttpClient instrumentation, OTLP exporter |
| `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` | Centralized ActivitySource: 13 activity names, 10+ tag constants, singleton instance |
| `src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs` | CommandApi ActivitySource: Submit, QueryStatus, Replay constants |
| `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` | MediatR outermost behavior: Submit activity, correlation ID extraction, structured logging |
| `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` | Correlation ID: generate/validate from X-Correlation-ID header, store in HttpContext |
| `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | 5-step pipeline activities, SetActivityTags helper, traceparent fallback |
| `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` | Publish activity (ActivityKind.Producer), event_count + topic tags |
| `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` | DeadLetter activity (ActivityKind.Producer), failure_stage + exception_type tags |
| `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` | QueryStatus activity (ActivityKind.Server), tenant-scoped search |
| `src/Hexalith.EventStore.CommandApi/Controllers/ReplayController.cs` | Replay activity (ActivityKind.Server), original correlation tracking |

### Existing Test Coverage Summary

**OpenTelemetryRegistrationTests.cs (8 tests):**
1. ServiceDefaults registers both activity sources
2. All activity names follow `EventStore.{Component}.{Action}` pattern
3. All tag keys follow `eventstore.*` namespace
4. EventStoreActivitySource source name is `"Hexalith.EventStore"`
5. EventStoreActivitySources CommandApi source name is `"Hexalith.EventStore.CommandApi"`
6. Submit constant matches architecture
7. QueryStatus constant matches architecture
8. Replay constant matches architecture

**EndToEndTraceTests.cs (9 tests):**
1. ProcessCommand creates ProcessCommand activity
2. ProcessCommand with no ambient activity uses traceparent fallback
3. ProcessCommand creates child activities for each stage (>= 5)
4. All activities have correlation_id tag
5. All activities have tenant_id tag
6. Successful command sets Ok status
7. Tenant mismatch sets Error status
8. EventPublisher creates Publish activity (Producer, Ok)
9. DeadLetterPublisher creates DeadLetter activity (Producer, Ok)

**CommandApiTraceTests.cs (4-5 tests):**
1. SubmitCommand through LoggingBehavior creates Submit activity (Server)
2. QueryStatus controller creates QueryStatus activity (Server, Ok)
3. QueryStatus controller sets Error on NotFound
4. Replay controller creates Replay activity (Server, Ok)
5. Replay controller sets Error on Conflict

**EventStoreActivitySourceTests.cs (3 tests):**
1. Activity creation when listener is active
2. Activities include correct tags when set
3. Activities follow naming convention

**DeadLetterTraceChainTests.cs (8 tests):**
1. Domain service failure spans entire lifecycle
2. API-to-Actor-to-DeadLetter uses single trace ID
3. All activities have correlation_id tag
4. Failing activities have Error status
5. DeadLetterPublisher records Ok on success
6. DeadLetterPublisher records Error on DAPR publish failure
7. Trace context propagates through actor proxy using traceparent fallback
8. Sidecar unavailable failure log contains full correlation context

**Total: ~30+ comprehensive telemetry tests across 5 test files.**

### Activity Hierarchy (Verified)

```
HTTP Request -> ASP.NET Core auto-instrumentation (root span)
  -> CorrelationIdMiddleware (sets correlation ID)
    -> LoggingBehavior: EventStore.CommandApi.Submit [Server]
      -> CommandRouter -> AggregateActor.ProcessCommandAsync
        -> EventStore.Actor.ProcessCommand [Internal] (with traceparent fallback)
          -> EventStore.Actor.IdempotencyCheck [Internal]
          -> EventStore.Actor.TenantValidation [Internal]
          -> EventStore.Actor.BackpressureCheck [Internal]
          -> EventStore.Actor.StateRehydration [Internal]
          -> EventStore.DomainService.Invoke [Internal]
          -> EventStore.Events.Persist [Internal]
          -> EventStore.Events.Publish [Producer] (via EventPublisher)
          -> EventStore.Events.PublishDeadLetter [Producer] (via DeadLetterPublisher, on failure)
          -> EventStore.Actor.StateMachineTransition [Internal]

HTTP Request -> EventStore.CommandApi.QueryStatus [Server]
HTTP Request -> EventStore.CommandApi.Replay [Server]
```

### Previous Story Intelligence

**Story 5.5 (E2E Security Testing with Keycloak)** -- status: done:
- Pure verification story pattern: read existing code, verify against ACs, fill gaps.
- Added 0 new tests (comprehensive existing coverage).
- Key learning: Verification stories should focus on confirming existing behavior, not refactoring.
- Key learning: DO NOT duplicate tests that already exist -- review first, add only what's missing.
- Tier 1: 659, Tier 2: 1482 at completion.

**Old Story 6.1 (End-to-End OpenTelemetry Trace Instrumentation)** -- status: done:
- Created 19 new tests across 3 test files.
- Added `.AddSource("Hexalith.EventStore")` to ServiceDefaults (highest-impact single-line change).
- Added controller activities for QueryStatus and Replay.
- Added ActivityKind correctness and ActivityStatusCode completeness.
- Added traceparent fallback for DAPR actor proxy boundary.
- Post-review: explicit ActivityKind assignments, 716/716 tests passing.
- Final: 876 total tests (662 Server + 157 Contracts + 48 Testing + 9 Client).

### Git Intelligence

Recent commits (Epic 5 completion):
- `09f68a4` Merge pull request #109 from Hexalith/feat/story-5-5-e2e-security-testing-done
- `d865a0c` feat: Complete Story 5.5 E2E Security Testing with Keycloak
- `c1659bc` Update sprint status and implement Story 5.5
- `726ccf8` feat: Update Story 5.4 for DAPR Service-to-Service Access Control
- `6f6cfaa` fix: tighten 5-3 review follow-up tests

### Anti-Patterns to Avoid

- **DO NOT re-implement tracing.** All trace instrumentation is complete. Verify only.
- **DO NOT modify ActivitySource definitions** unless a genuine architectural gap is found.
- **DO NOT add new activity names** unless the architecture requires them and they are missing.
- **DO NOT duplicate tests** that already exist. Review the 30+ existing telemetry tests FIRST, then add ONLY what's missing.
- **DO NOT add event payload data as activity tags.** Rule 5 / SEC-5 / NFR12 prohibit logging payload data.
- **DO NOT add new NuGet dependencies.** All required packages are already referenced.
- **DO NOT modify `CommandsController.cs`** for Submit activity -- LoggingBehavior handles it.

### Testing Standards

- **Framework:** xUnit 2.9.3 / Shouldly 4.3.0
- **Mocking:** NSubstitute 5.3.0
- **Activity capture pattern:** `ActivityListener` with `ShouldListenTo` filter and `AllDataAndRecorded` sampling
- **Parallel test isolation:** Tests use unique correlation IDs (GUID-based) and filter callbacks by both operation name AND correlation ID
- **CRITICAL:** `ActivityListener` MUST be registered BEFORE any code that creates activities. In test classes, register in constructor or class-level setup.
- **Test naming:** Descriptive method names following existing conventions

### Project Structure Notes

- No new project folders expected
- No new NuGet packages needed
- All test files in existing directories (Telemetry/, Observability/)
- New tests (if any) should be added to existing test classes, not new files

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 6, Story 6.1]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR35 OpenTelemetry traces spanning full command lifecycle]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR2 End-to-end command lifecycle within 200ms at p99]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR31 OTLP-compatible collector export]
- [Source: _bmad-output/planning-artifacts/architecture.md#OpenTelemetry Activity Naming table]
- [Source: _bmad-output/planning-artifacts/architecture.md#Structured Logging Pattern table]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 5 Never log event payload]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 9 correlationId everywhere]
- [Source: _bmad-output/planning-artifacts/architecture.md#UX-DR24 Aspire Traces tab]
- [Source: _bmad-output/implementation-artifacts/6-1-end-to-end-opentelemetry-trace-instrumentation.md -- old Story 6.1 (done)]
- [Source: src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs -- centralized activity naming]
- [Source: src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs -- CommandApi ActivitySource]
- [Source: src/Hexalith.EventStore.ServiceDefaults/Extensions.cs -- OTel configuration with dual sources]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs -- pipeline activities + traceparent fallback]
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs -- publication activity]
- [Source: src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs -- dead-letter activity]
- [Source: src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs -- MediatR pipeline activity]
- [Source: src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs -- correlation propagation]
- [Source: tests/Hexalith.EventStore.Server.Tests/Telemetry/ -- 4 telemetry test files]
- [Source: tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterTraceChainTests.cs -- trace chain tests]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

### Completion Notes List

- **Task 0:** All prerequisites verified. Tier 1: 659 tests passing (267 Contracts + 293 Client + 32 Sample + 67 Testing). Tier 2: 1482 tests passing. All 10 prerequisite source files exist. Telemetry test inventory: EventStoreActivitySourceTests (3), OpenTelemetryRegistrationTests (8), CommandApiTraceTests (5), EndToEndTraceTests (9), DeadLetterTraceChainTests (8) = 33 total test methods across 5 files.
- **Task 1 (AC #5):** Verified dual ActivitySource registration in ServiceDefaults/Extensions.cs: `.AddSource("Hexalith.EventStore.CommandApi")` (line 62) and `.AddSource("Hexalith.EventStore")` (line 63). AddAspNetCoreInstrumentation with /health, /alive, /ready exclusion filters confirmed. AddHttpClientInstrumentation confirmed. OTLP exporter conditional on `OTEL_EXPORTER_OTLP_ENDPOINT` confirmed (line 81).
- **Task 2 (AC #2, #4):** Verified EventStoreActivitySource.cs: SourceName = "Hexalith.EventStore". All 11 activity name constants present (ProcessCommand, IdempotencyCheck, TenantValidation, StateRehydration, DomainServiceInvoke, EventsPersist, EventsPublish, EventsDrain, EventsPublishDeadLetter, BackpressureCheck, StateMachineTransition). All 10 tag key constants follow `eventstore.*` namespace.
- **Task 3 (AC #8):** Verified EventStoreActivitySources.cs: CommandApi source = "Hexalith.EventStore.CommandApi". Submit, QueryStatus, Replay constants present. LoggingBehavior creates Submit activity with ActivityKind.Server (NOT in CommandsController -- no duplication). CommandStatusController creates QueryStatus activity with ActivityKind.Server, correlation ID + tenant ID tags, Ok/Error status codes. ReplayController creates Replay activity with ActivityKind.Server, correlation ID + tenant ID tags, Ok/Error status codes.
- **Task 4 (AC #1, #2, #3):** Verified AggregateActor.cs: ProcessCommand outer activity with ActivityKind.Internal. Child activities for all 5 pipeline steps (IdempotencyCheck, TenantValidation, BackpressureCheck, StateRehydration, DomainServiceInvoke) plus EventsPersist and EventsDrain. SetActivityTags helper correctly sets correlation_id, tenant_id, domain, aggregate_id, command_type. ActivityStatusCode.Ok on success, ActivityStatusCode.Error + AddException() on failure for all activities. **CRITICAL:** Traceparent fallback mechanism verified -- TryGetFallbackParentContext reads traceparent/tracestate from CommandEnvelope.Extensions and restores parent context via ActivityContext.TryParse when Activity.Current is null.
- **Task 5 (AC #1, #2):** Verified EventPublisher.cs: EventsPublish activity with ActivityKind.Producer, tags (correlation_id, tenant_id, domain, aggregate_id, event_count, topic), Ok/Error status codes with AddException. Verified DeadLetterPublisher.cs: EventsPublishDeadLetter activity with ActivityKind.Producer, tags (correlation_id, tenant_id, domain, aggregate_id, command_type, failure_stage, exception_type, deadletter_topic), Ok/Error status codes with AddException.
- **Task 6 (AC #7):** Verified CorrelationIdMiddleware.cs: accepts X-Correlation-ID header, validates GUID format via Guid.TryParse, generates new GUID if missing/invalid, stores in HttpContext.Items["CorrelationId"], echoes in response headers. LoggingBehavior extracts from HttpContext via CorrelationIdMiddleware.HttpContextKey or generates fallback GUID.
- **Task 7 (AC #9):** All 5 test files reviewed. Coverage is comprehensive: registration tests (8), end-to-end trace tests (9), command API trace tests (5), activity source tests (3), dead-letter trace chain tests (8). **Coverage assessment:** (7.6.1) Drain activity is covered indirectly through naming/assertion coverage and production-path verification; no dedicated OTel unit test due to `IRemindable` callback constraints. (7.6.2) BackpressureCheck is covered indirectly through naming/assertion coverage and production-path verification; no dedicated OTel unit test due to metadata pre-condition setup complexity. (7.6.3) StateMachineTransition is a forward-looking constant defined for naming consistency and is not currently started by production code. (7.6.4) Rule 5 compliance: verified. No event payload data set as activity tags anywhere in production code.
- **Task 8:** Build: 0 warnings, 0 errors. Tier 1: 659 passing. Tier 2: 1482 passing. Test delta: 0 new tests. All 9 acceptance criteria satisfied. No code changes required -- this was a pure verification story.

### Verification Report Summary

| Acceptance Criteria | Status | Evidence |
|---|---|---|
| AC #1: Single distributed trace | PASS | Dual ActivitySources registered, ProcessCommand with child activities, traceparent fallback |
| AC #2: Named activities match conventions | PASS | All 11 activity constants follow EventStore.{Component}.{Action} pattern |
| AC #3: Trace context propagates | PASS | W3C via DAPR + traceparent fallback via CommandEnvelope.Extensions |
| AC #4: Tags follow semantic conventions | PASS | All 10 tag constants use eventstore.* namespace, Ok/Error status on all activities |
| AC #5: Both ActivitySources registered | PASS | Extensions.cs lines 62-63 |
| AC #6: OTLP-compatible export | PASS | Conditional on OTEL_EXPORTER_OTLP_ENDPOINT (line 81) |
| AC #7: correlationId on every activity | PASS | SetActivityTags + controller tags + Rule 5 compliant |
| AC #8: CommandApi controller activities | PASS | Submit (LoggingBehavior), QueryStatus, Replay all verified |
| AC #9: Comprehensive test coverage | PASS | 33 telemetry test methods across 5 files, with documented indirect coverage/constraints for drain and backpressure and a forward-looking StateMachineTransition constant |

**Observations (non-blocking):**
- `StateMachineTransition` constant defined but not used in production code (no StartActivity call)
- Drain and BackpressureCheck activities exist in production but lack dedicated OTel unit tests (naming convention tests cover constants; activities are hard to exercise in unit tests due to IRemindable/metadata prerequisites)

### File List

No product code files modified -- pure verification story.
Artifact files modified: `_bmad-output/implementation-artifacts/6-1-opentelemetry-tracing-across-command-lifecycle.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`.
