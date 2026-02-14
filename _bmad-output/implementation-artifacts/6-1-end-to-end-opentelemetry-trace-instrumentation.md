# Story 6.1: End-to-End OpenTelemetry Trace Instrumentation

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Epic 5 stories (5.1-5.4) should ideally be completed or in-progress before this story, but Story 6.1 has no hard code dependency on Epic 5. The codebase through Epic 4 (Story 4.5) provides all the instrumentation points needed.**

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` (Story 3.11 -- centralized ActivitySource with all activity names and tag constants)
- `src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs` (CommandApi-specific ActivitySource)
- `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` (Aspire ServiceDefaults -- OpenTelemetry configuration, health checks)
- `src/Hexalith.EventStore.CommandApi/Program.cs` (DI registration and middleware pipeline)
- `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` (Correlation ID generation/propagation)
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` (MediatR outermost behavior with OTel activity)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (5-step pipeline with OTel activities per stage)
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (Publication with OTel activity)
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` (Dead-letter with OTel activity)
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` (Persistence with structured logging)
- `src/Hexalith.EventStore.Server/Actors/ActorStateMachine.cs` (State machine with checkpoint logging)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (Server DI registrations)

Run `dotnet test` to confirm all existing tests pass before beginning. Stories through 4.5 should have ~879 tests.

## Story

As an **operator**,
I want complete OpenTelemetry trace instrumentation spanning the full command lifecycle (Received -> Processing -> EventsStored -> EventsPublished -> Completed),
So that I can visualize the entire command flow in any OTLP-compatible collector (FR35).

## Acceptance Criteria

1. **Single distributed trace spans all pipeline stages** - Given a command is submitted and processed through the full pipeline, When I view traces in the Aspire dashboard (or Jaeger, Grafana/Tempo), Then a single distributed trace spans all stages: API receipt, MediatR pipeline, actor activation, domain invocation, event persistence, event publication, And no trace gaps exist between the API layer and actor processing layer.

2. **Named activities match architecture conventions** - Given the architecture specifies activity naming patterns, When traces are collected, Then each stage has a named activity: `EventStore.CommandApi.Submit` (API layer), `EventStore.Actor.ProcessCommand` (actor outer span), `EventStore.Actor.IdempotencyCheck`, `EventStore.Actor.TenantValidation`, `EventStore.Actor.StateRehydration`, `EventStore.DomainService.Invoke`, `EventStore.Events.Persist`, `EventStore.Events.Publish`, And dead-letter operations use `EventStore.Events.PublishDeadLetter`, And drain recovery uses `EventStore.Events.Drain`.

3. **Trace context propagates across all spans** - Given a command flows through API -> MediatR -> Actor -> DomainService -> EventPersist -> EventPublish, When trace context (correlation ID, causation ID) is set at the API layer, Then all child spans inherit the parent trace context, And the correlation ID is present as a tag on every activity in the trace, And W3C Trace Context headers propagate across DAPR service invocation boundaries automatically.

4. **All activity tags follow semantic conventions** - Given an activity is created for any pipeline stage, When tags are set, Then tags use the `eventstore.*` namespace: `eventstore.correlation_id`, `eventstore.tenant_id`, `eventstore.domain`, `eventstore.aggregate_id`, `eventstore.command_type`, `eventstore.event_count`, `eventstore.topic`, And success/failure status is recorded via `ActivityStatusCode.Ok` or `ActivityStatusCode.Error`.

5. **Server ActivitySource registered in ServiceDefaults** - Given the `EventStoreActivitySource.SourceName` is `"Hexalith.EventStore"`, When `ConfigureOpenTelemetry` runs, Then `.AddSource("Hexalith.EventStore")` is registered alongside `"Hexalith.EventStore.CommandApi"`, And all server-level activities (actor pipeline, event persistence, publication, dead-letter, drain) appear in collected traces.

6. **Traces exportable to any OTLP-compatible collector** - Given the system runs with an `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable set, When traces are exported, Then they are sent to the configured OTLP endpoint (NFR31), And the Aspire dashboard displays the full distributed trace, And Jaeger/Grafana/Tempo can visualize the same traces.

7. **Trace instrumentation minimal overhead** - Given trace instrumentation adds processing time, When measuring end-to-end latency, Then the tracing overhead does not cause the system to exceed the NFR2 200ms e2e budget, And `Activity.IsAllDataRequested` is checked before setting expensive tags where applicable, And activities use `ActivityKind.Internal` for in-process operations and `ActivityKind.Client` for outgoing DAPR calls.

8. **CommandApi controller activities** - Given the CommandApi has three endpoints, When each endpoint handles a request, Then `EventStore.CommandApi.Submit` spans the POST `/commands` flow, And `EventStore.CommandApi.QueryStatus` spans the GET `/commands/status/{id}` flow, And `EventStore.CommandApi.Replay` spans the POST `/commands/replay/{id}` flow, And each activity includes correlation ID and tenant ID tags.

9. **Status query and replay endpoint tracing** - Given status query and replay are lightweight operations, When they execute, Then status query activity records the queried correlation ID and result status, And replay activity records the replayed correlation ID and submission result.

10. **Comprehensive trace test coverage** - Given trace instrumentation is critical for operations, When tests run, Then tests verify ActivitySource registration for both `"Hexalith.EventStore"` and `"Hexalith.EventStore.CommandApi"`, And tests verify that activities are created with correct names for each pipeline stage, And tests verify that required tags (correlation ID, tenant ID) are set on activities.

11. **Trace continuity across DAPR actor proxy boundary** - Given the CommandApi routes commands to AggregateActor via DAPR actor proxy (in-process), When `Activity.Current` flows through `CommandRouter` -> `ActorProxy.InvokeMethodAsync` -> `AggregateActor.ProcessCommandAsync`, Then the actor's `ProcessCommand` activity is a child span of the API-layer activity (not a disconnected root), And the trace ID is the same across the API and actor layers, And if `Activity.Current` does NOT flow through the actor proxy, explicit trace context propagation via `CommandEnvelope` metadata is added as a fallback.

12. **No duplicate Submit activity spans** - Given `LoggingBehavior.cs` already creates an `EventStore.CommandApi.Submit` activity wrapping the MediatR pipeline, When the POST `/commands` endpoint processes a request, Then there is exactly ONE `EventStore.CommandApi.Submit` activity in the trace (not duplicated at the controller level), And QueryStatus and Replay controllers create their own activities since they do NOT go through the MediatR pipeline.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [ ] 0.1 Run all existing tests -- they must pass before proceeding
  - [ ] 0.2 Review `EventStoreActivitySource.cs` -- catalog all defined activity names and tag constants
  - [ ] 0.3 Review `ServiceDefaults/Extensions.cs` -- confirm the gap: `"Hexalith.EventStore"` source NOT registered
  - [ ] 0.4 Review `AggregateActor.cs` -- verify activities exist for all 5 pipeline steps + drain + dead-letter
  - [ ] 0.5 Review `LoggingBehavior.cs` -- verify CommandApi activity span creation; **CRITICAL: determine if it already creates `EventStore.CommandApi.Submit` activity** -- if yes, Task 2 must NOT duplicate it in `CommandsController.cs`
  - [ ] 0.6 Review `EventPublisher.cs` and `DeadLetterPublisher.cs` -- verify publication activities
  - [ ] 0.7 Review `CorrelationIdMiddleware.cs` -- verify correlation ID generation and propagation
  - [ ] 0.8 **Audit `Activity.Current` flow through DAPR actor proxy** -- step through `CommandRouter` -> `ActorProxy.InvokeMethodAsync` -> `AggregateActor.ProcessCommandAsync` to determine if `Activity.Current` propagates or if the actor runtime creates a new execution context that breaks the parent-child chain

- [ ] Task 1: Register Server ActivitySource in ServiceDefaults (AC: #5) **CRITICAL FIX**
  - [ ] 1.1 In `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`, add `.AddSource("Hexalith.EventStore")` to the `WithTracing` configuration
  - [ ] 1.2 The line should be added alongside the existing `.AddSource("Hexalith.EventStore.CommandApi")`
  - [ ] 1.3 This single change makes ALL existing server-level activities visible to trace collectors:
    - `EventStore.Actor.ProcessCommand` (outer span)
    - `EventStore.Actor.IdempotencyCheck`
    - `EventStore.Actor.TenantValidation`
    - `EventStore.Actor.StateRehydration`
    - `EventStore.DomainService.Invoke`
    - `EventStore.Events.Persist`
    - `EventStore.Events.Publish`
    - `EventStore.Events.Drain`
    - `EventStore.Events.PublishDeadLetter`
    - `EventStore.Actor.StateMachineTransition`
  - [ ] 1.4 Verify: After this change, the Aspire dashboard shows actor-level trace spans

- [ ] Task 2: Add CommandApi controller-level activities (AC: #8, #9, #12)
  - [ ] 2.0 **CRITICAL PRE-CHECK:** Confirm from Task 0.5 whether `LoggingBehavior.cs` already creates the `EventStore.CommandApi.Submit` activity. If YES: do NOT add a Submit activity to `CommandsController.cs` (it would create a duplicate span). The Submit activity lives in LoggingBehavior which wraps the full MediatR pipeline. Only add activities to controllers for endpoints that bypass MediatR (QueryStatus, Replay).
  - [ ] 2.1 **CONDITIONAL -- only if LoggingBehavior does NOT create Submit activity:** In `CommandsController.cs`, wrap the POST `/commands` handler with activity `EventStore.CommandApi.Submit`. If LoggingBehavior already handles it, skip this subtask.
  - [ ] 2.2 In `CommandStatusController.cs`, wrap the GET `/commands/status/{id}` handler with activity `EventStore.CommandApi.QueryStatus` (this endpoint does NOT go through MediatR -- it reads directly from state store):
    ```csharp
    using Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
        EventStoreActivitySources.QueryStatus, ActivityKind.Server);
    activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
    ```
  - [ ] 2.3 In `ReplayController.cs`, wrap the POST `/commands/replay/{id}` handler with activity `EventStore.CommandApi.Replay`:
    ```csharp
    using Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
        EventStoreActivitySources.Replay, ActivityKind.Server);
    activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
    ```
  - [ ] 2.4 Set `ActivityStatusCode.Ok` on success, `ActivityStatusCode.Error` on failure for all controller activities
  - [ ] 2.5 Add activity name constants to `EventStoreActivitySources.cs`:
    ```csharp
    public const string Submit = "EventStore.CommandApi.Submit";
    public const string QueryStatus = "EventStore.CommandApi.QueryStatus";
    public const string Replay = "EventStore.CommandApi.Replay";
    ```

- [ ] Task 3: Verify and enhance ActivityKind on existing activities (AC: #7)
  - [ ] 3.1 Review all `StartActivity` calls in `AggregateActor.cs` -- ensure they use `ActivityKind.Internal` for in-process operations
  - [ ] 3.2 Review `DaprDomainServiceInvoker` -- if it creates an activity for the outgoing DAPR call, it should use `ActivityKind.Client`
  - [ ] 3.3 Review `EventPublisher.cs` -- publication to DAPR pub/sub should use `ActivityKind.Producer`
  - [ ] 3.4 Review `DeadLetterPublisher.cs` -- dead-letter publication should use `ActivityKind.Producer`
  - [ ] 3.5 If any existing activities don't specify `ActivityKind`, add the correct kind per above

- [ ] Task 4: Add ActivityStatusCode to all existing activities (AC: #4)
  - [ ] 4.1 Verify all activities in `AggregateActor.cs` set `activity.SetStatus(ActivityStatusCode.Ok)` on success and `activity.SetStatus(ActivityStatusCode.Error, errorMessage)` on failure
  - [ ] 4.2 Verify `EventPublisher.cs` sets status codes
  - [ ] 4.3 Verify `DeadLetterPublisher.cs` sets status codes
  - [ ] 4.4 Verify `LoggingBehavior.cs` sets status codes
  - [ ] 4.5 Add status codes to any activities where they're missing
  - [ ] 4.6 On error, use `activity.RecordException(exception)` before setting error status (standard OTel pattern)

- [ ] Task 5: Verify W3C Trace Context propagation across DAPR boundaries (AC: #3)
  - [ ] 5.1 Verify that DAPR automatically propagates W3C Trace Context headers on `DaprClient.InvokeMethodAsync` calls (domain service invocation)
  - [ ] 5.2 Verify that DAPR propagates trace context on `DaprClient.PublishEventAsync` calls (event publication)
  - [ ] 5.3 No code changes expected -- DAPR handles this automatically. Document this verification in Dev Notes
  - [ ] 5.4 If trace context is NOT propagating (e.g., DAPR sidecar needs configuration), add explicit `Activity.Current?.Context` propagation

- [ ] Task 6: Add `Activity.IsAllDataRequested` checks for performance (AC: #7)
  - [ ] 6.1 Review all `activity?.SetTag()` calls -- for simple string tags, `activity?.SetTag()` is already guarded by null-conditional
  - [ ] 6.2 For any expensive tag computation (serialization, string formatting), wrap with `if (activity?.IsAllDataRequested == true)`
  - [ ] 6.3 Verify that `Stopwatch.GetElapsedTime()` calls used for duration measurement are lightweight
  - [ ] 6.4 No allocation-heavy operations should occur inside activity setup

- [ ] Task 7: Create ActivitySource registration tests (AC: #10)
  - [ ] 7.1 Create `OpenTelemetryRegistrationTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Telemetry/`
  - [ ] 7.2 Test: `ServiceDefaults_RegistersBothActivitySources` -- verify that `ConfigureOpenTelemetry` registers both `"Hexalith.EventStore"` and `"Hexalith.EventStore.CommandApi"` sources
  - [ ] 7.3 Test: `EventStoreActivitySource_AllActivityNamesMatchArchitecture` -- verify all activity name constants match the documented architecture patterns
  - [ ] 7.4 Test: `EventStoreActivitySource_AllTagKeysFollowNamespace` -- verify all tag constants start with `eventstore.`

- [ ] Task 8: Create end-to-end trace activity tests (AC: #1, #2, #10)
  - [ ] 8.1 Create `EndToEndTraceTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Telemetry/`
  - [ ] 8.2 Test: `ProcessCommand_CreatesProcessCommandActivity` -- outer span created with correct name
  - [ ] 8.3 Test: `ProcessCommand_CreatesChildActivitiesForEachStage` -- all 5 pipeline stages create activities
  - [ ] 8.4 Test: `ProcessCommand_ActivitiesHaveCorrelationIdTag` -- every activity has `eventstore.correlation_id` tag
  - [ ] 8.5 Test: `ProcessCommand_ActivitiesHaveTenantIdTag` -- every activity has `eventstore.tenant_id` tag
  - [ ] 8.6 Test: `ProcessCommand_SuccessfulCommand_SetsOkStatus` -- successful processing sets `ActivityStatusCode.Ok`
  - [ ] 8.7 Test: `ProcessCommand_FailedCommand_SetsErrorStatus` -- failed processing sets `ActivityStatusCode.Error`
  - [ ] 8.8 Test: `EventPublisher_CreatesPublishActivity` -- publication creates `EventStore.Events.Publish` activity
  - [ ] 8.9 Test: `DeadLetterPublisher_CreatesDeadLetterActivity` -- dead-letter creates `EventStore.Events.PublishDeadLetter` activity

- [ ] Task 9: Create CommandApi controller activity tests (AC: #8, #9)
  - [ ] 9.1 Create `CommandApiTraceTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Telemetry/` (or appropriate test project)
  - [ ] 9.2 Test: `SubmitCommand_CreatesSubmitActivity` -- POST /commands creates `EventStore.CommandApi.Submit` activity
  - [ ] 9.3 Test: `QueryStatus_CreatesQueryStatusActivity` -- GET /status creates `EventStore.CommandApi.QueryStatus` activity
  - [ ] 9.4 Test: `ReplayCommand_CreatesReplayActivity` -- POST /replay creates `EventStore.CommandApi.Replay` activity
  - [ ] 9.5 Test: `ControllerActivities_IncludeCorrelationIdTag` -- all controller activities have correlation ID

- [ ] Task 10: Update existing telemetry tests if needed (AC: #10)
  - [ ] 10.1 Review `tests/Hexalith.EventStore.Server.Tests/Telemetry/EventStoreActivitySourceTests.cs`
  - [ ] 10.2 Add any missing activity name constant tests for new constants
  - [ ] 10.3 Verify test fixture properly uses `ActivityListener` to capture activities in tests

- [ ] Task 11: Verify all tests pass
  - [ ] 11.1 Run `dotnet test` to confirm no regressions
  - [ ] 11.2 All new telemetry registration tests pass
  - [ ] 11.3 All new end-to-end trace tests pass
  - [ ] 11.4 All new controller activity tests pass
  - [ ] 11.5 All existing Story 4.1-4.5 tests still pass

## Dev Notes

### Story Context

This is the **first story in Epic 6: Observability, Health & Operational Readiness**. It completes the end-to-end OpenTelemetry trace instrumentation that was built incrementally through Epics 2-4. Previous stories added "basic" OpenTelemetry activities at each layer as they were built. This story's job is to **verify completeness, fix gaps, and ensure the full distributed trace chain is unbroken** from API receipt through event publication.

**What Epics 2-4 already implemented (to VERIFY and COMPLETE, not replicate):**
- `EventStoreActivitySource.cs` in Server/Telemetry -- centralized ActivitySource with 10 activity names and 10 tag constants
- `EventStoreActivitySources.CommandApi` in CommandApi/Telemetry -- separate CommandApi ActivitySource
- Activities in `AggregateActor.cs` for all 5 pipeline steps (idempotency, tenant validation, rehydration, domain invocation, persistence) plus state machine, drain, and dead-letter
- Activities in `EventPublisher.cs` for event publication
- Activities in `DeadLetterPublisher.cs` for dead-letter publication
- Activity in `LoggingBehavior.cs` for MediatR pipeline entry
- `CorrelationIdMiddleware.cs` for correlation ID generation and propagation
- Structured logging with correlation IDs throughout all pipeline stages
- `ServiceDefaults/Extensions.cs` with basic OpenTelemetry configuration and OTLP exporter

**CRITICAL GAP IDENTIFIED -- Task 1 is the highest priority:**
- `ServiceDefaults/Extensions.cs` calls `.AddSource("Hexalith.EventStore.CommandApi")` but does NOT call `.AddSource("Hexalith.EventStore")` (the Server's `EventStoreActivitySource.SourceName`)
- This means ALL server-level activities are **invisible** to OpenTelemetry collectors
- Adding a single `.AddSource("Hexalith.EventStore")` line fixes this and makes ~10 existing activity types visible
- This is the most impactful single-line change in the entire story

**What this story adds (NEW):**
- `ServiceDefaults` registration of `"Hexalith.EventStore"` ActivitySource (CRITICAL FIX)
- CommandApi controller-level activities (`EventStore.CommandApi.Submit`, `EventStore.CommandApi.QueryStatus`, `EventStore.CommandApi.Replay`)
- Activity name constants in `EventStoreActivitySources.cs`
- `ActivityKind` correctness on all activities (Internal, Client, Producer, Server)
- `ActivityStatusCode` completeness on all activities
- `Activity.RecordException()` calls on failure paths
- Comprehensive trace test suite

**What this story modifies (EXISTING):**
- `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` -- add `"Hexalith.EventStore"` source registration
- `src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs` -- add activity name constants
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` -- add Submit activity
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` -- add QueryStatus activity
- `src/Hexalith.EventStore.CommandApi/Controllers/ReplayController.cs` -- add Replay activity
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- potentially add ActivityKind and RecordException
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` -- potentially add ActivityKind.Producer
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` -- potentially add ActivityKind.Producer

### Architecture Compliance

- **FR35:** Complete OpenTelemetry trace instrumentation spanning full command lifecycle (named activities per architecture pattern)
- **NFR2:** Trace instrumentation adds minimal overhead (within 200ms e2e budget) -- use `Activity.IsAllDataRequested` for expensive operations
- **NFR31:** Traces exportable to any OTLP-compatible collector (Aspire dashboard, Jaeger, Grafana/Tempo)
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **Rule #5:** Never log event payload data (SEC-5, NFR12) -- activity tags must not include payload data
- **Architecture OpenTelemetry Activity Naming:**
  - Command API: `EventStore.CommandApi.{verb}` (Submit, QueryStatus, Replay)
  - Actor processing: `EventStore.Actor.{operation}` (ProcessCommand, IdempotencyCheck, TenantValidation, StateRehydration)
  - Domain invocation: `EventStore.DomainService.Invoke`
  - Event persistence: `EventStore.Events.Persist`
  - Event publishing: `EventStore.Events.Publish`

### Critical Design Decisions

- **Activity hierarchy creates parent-child spans.** The outermost ASP.NET Core activity is the root. `EventStore.CommandApi.Submit` nests inside it. `EventStore.Actor.ProcessCommand` nests inside that (via DAPR trace propagation). Individual pipeline stages (IdempotencyCheck, TenantValidation, etc.) nest inside ProcessCommand. This hierarchy is automatic via `Activity.Current` context flow.

- **Two ActivitySources, one trace.** `"Hexalith.EventStore.CommandApi"` handles API-layer activities. `"Hexalith.EventStore"` handles server/actor-layer activities. Both must be registered in `ServiceDefaults` for the full trace to be visible. The activities join into a single distributed trace via W3C Trace Context propagation.

- **DAPR handles cross-boundary trace propagation automatically.** When `DaprClient.InvokeMethodAsync` calls a domain service, DAPR's sidecar-to-sidecar communication automatically propagates W3C `traceparent` and `tracestate` headers. Similarly, `DaprClient.PublishEventAsync` propagates trace context to subscribers. No manual propagation code is needed -- this is a DAPR runtime guarantee.

- **ActivityKind is semantically meaningful for trace visualization.** `ActivityKind.Server` for incoming requests (controller actions), `ActivityKind.Internal` for in-process operations (actor pipeline stages), `ActivityKind.Client` for outgoing service calls (domain service invocation), `ActivityKind.Producer` for event publication. Trace visualization tools use ActivityKind to render correct icons and groupings.

- **`Activity.RecordException()` is the standard OTel error pattern.** On error, call `activity.RecordException(exception)` followed by `activity.SetStatus(ActivityStatusCode.Error, errorMessage)`. `RecordException` adds the exception as an event on the span, visible in trace detail views. The error message must NOT include stack traces in production (rule #13 applies to error responses, not trace data -- trace spans may include exception details for operator debugging).

- **Subscriber tracing is OUT OF SCOPE for Story 6.1.** This story covers producer-side trace instrumentation (up through event publication). Downstream subscriber services (e.g., read models, projections) will receive W3C Trace Context headers from DAPR pub/sub automatically, but verifying subscriber trace continuity is a separate concern (Story 7.5 integration tests).

- **Domain service trace boundary.** When `DomainServiceInvoker` calls an external domain service via DAPR, the `DomainService.Invoke` activity covers the outgoing call. The domain service itself is responsible for creating its own server-side activity. This story only instruments the EventStore side of that boundary.

- **Formal benchmarking is OUT OF SCOPE.** AC #7 requires that tracing overhead stays within the NFR2 200ms budget, but formal benchmarking with statistical analysis is not required in this story. A simple manual verification that latency is reasonable suffices. Formal performance benchmarking may be added in a future story if needed.

- **Trace sampling for production.** The `OTEL_TRACES_SAMPLER` environment variable controls trace sampling in production. The Aspire dashboard always shows all traces during development (no OTLP endpoint required -- Aspire captures traces in-process). Production deployments should configure appropriate sampling (e.g., `parentbased_traceidratio` with 10% sample rate) to manage trace volume. This story does not add sampling configuration -- it relies on the standard OpenTelemetry SDK environment variable support.

- **Custom metrics are OUT OF SCOPE for Story 6.1.** This story focuses exclusively on distributed tracing. Custom metrics (counters, histograms for command processing latency, event throughput, etc.) are a separate concern that may be addressed in a future observability story.

- **ASP.NET Core auto-instrumentation creates the root span.** The `AddAspNetCoreInstrumentation()` call in ServiceDefaults automatically creates an HTTP server span for every incoming request. Controller-level activities (Submit, QueryStatus, Replay) become child spans of this auto-instrumented root. This is intentional -- the ASP.NET span captures HTTP-level details (method, route, status code) while the controller activity captures domain-level details (correlation ID, tenant ID).

- **`AggregateId` as a high-cardinality tag is acceptable.** While OpenTelemetry best practices warn against high-cardinality tags in metrics, this story deals with traces where high-cardinality tags are normal and expected. The `eventstore.aggregate_id` tag is essential for debugging specific aggregate instances in trace detail views.

### Existing Patterns to Follow

**ActivitySource singleton pattern (from EventStoreActivitySource.cs):**
```csharp
public static ActivitySource Instance { get; } = new(SourceName);
```

**Activity creation pattern (from AggregateActor.cs):**
```csharp
using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
    EventStoreActivitySource.ProcessCommand);
SetActivityTags(activity, command);
// ... processing ...
activity?.SetStatus(ActivityStatusCode.Ok);
```

**Tag helper pattern (from AggregateActor.cs):**
```csharp
private static void SetActivityTags(Activity? activity, CommandEnvelope command)
{
    if (activity is null) return;
    activity.SetTag(EventStoreActivitySource.TagCorrelationId, command.CorrelationId);
    activity.SetTag(EventStoreActivitySource.TagTenantId, command.TenantId);
    activity.SetTag(EventStoreActivitySource.TagDomain, command.Domain);
    activity.SetTag(EventStoreActivitySource.TagAggregateId, command.AggregateId);
    activity.SetTag(EventStoreActivitySource.TagCommandType, command.CommandType);
}
```

**ServiceDefaults tracing registration (CURRENT -- missing Server source):**
```csharp
.WithTracing(tracing =>
{
    tracing.AddSource(builder.Environment.ApplicationName)
        .AddSource("Hexalith.EventStore.CommandApi")
        // MISSING: .AddSource("Hexalith.EventStore")  <-- Task 1 adds this
        .AddAspNetCoreInstrumentation(...)
        .AddHttpClientInstrumentation();
});
```

**Controller activity pattern (to add in Task 2):**
```csharp
[HttpPost]
public async Task<IActionResult> SubmitCommand(...)
{
    using Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
        EventStoreActivitySources.Submit, ActivityKind.Server);
    activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
    activity?.SetTag(EventStoreActivitySource.TagTenantId, tenantId);

    try
    {
        // ... existing handler logic ...
        activity?.SetStatus(ActivityStatusCode.Ok);
        return Accepted(...);
    }
    catch (Exception ex)
    {
        activity?.RecordException(ex);
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        throw; // Let GlobalExceptionHandler handle it
    }
}
```

**Test pattern for activity capture (use ActivityListener):**
```csharp
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStarted = activity => capturedActivities.Add(activity),
};
ActivitySource.AddActivityListener(listener);
```

**CRITICAL: ActivityListener timing.** The `ActivityListener` MUST be registered BEFORE any code that creates activities is executed. If the listener is registered after `StartActivity()`, those activities will be null (no listener was sampling). In test classes, register the listener in the constructor or `[ClassInitialize]`/class-level setup, not inside individual test methods after the system-under-test runs. If tests share an `ActivitySource`, use `[Collection]` to prevent parallel test interference with listener state.

### Mandatory Coding Patterns

- Primary constructors: existing controllers already use primary constructors
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization
- **Rule #5:** Never log event payload or command payload data (SEC-5, NFR12) -- never set payload data as activity tags
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity
- **Rule #13:** No stack traces in production error responses (but trace spans MAY record exceptions for operator debugging via `RecordException`)
- Activity names as `const string` in centralized source classes
- Null-conditional `activity?.SetTag()` pattern for all tag setting (activity may be null if no listener)

### Project Structure Notes

**New files:**
- `tests/Hexalith.EventStore.Server.Tests/Telemetry/OpenTelemetryRegistrationTests.cs` -- registration verification tests
- `tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs` -- trace completeness tests
- `tests/Hexalith.EventStore.Server.Tests/Telemetry/CommandApiTraceTests.cs` -- controller activity tests (may be in a more appropriate test project depending on test dependency structure)

**Modified files:**
- `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` -- add `"Hexalith.EventStore"` source registration (1 line)
- `src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs` -- add activity name constants (3 lines)
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` -- add Submit activity wrapper
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` -- add QueryStatus activity
- `src/Hexalith.EventStore.CommandApi/Controllers/ReplayController.cs` -- add Replay activity
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- add ActivityKind and RecordException if missing
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` -- add ActivityKind.Producer if missing
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` -- add ActivityKind.Producer if missing

### Previous Story Intelligence

**From Story 4.5 (Dead-Letter Routing with Full Context):**
- Added `EventStore.Events.PublishDeadLetter` activity and tags to `EventStoreActivitySource.cs`
- `DeadLetterPublisher.cs` creates activities with correlation tags and status codes
- Pattern: `using Activity? activity = EventStoreActivitySource.Instance.StartActivity(...)` followed by tag setting and status codes
- Test pattern: 879 total tests, NSubstitute + Shouldly
- Code review identified a gap in OpenTelemetry assertion coverage for `DeadLetterPublisherTests.cs` -- this story should ensure proper telemetry test coverage

**From Story 3.11 (Actor State Machine & Checkpointed Stages):**
- Created `EventStoreActivitySource.cs` with centralized activity naming
- Defined all pipeline stage activity constants
- Established the `SetActivityTags()` helper pattern in AggregateActor

**From Story 4.1 (Event Publisher with CloudEvents 1.0):**
- `EventPublisher.cs` creates `EventStore.Events.Publish` activity
- Uses `Stopwatch.GetElapsedTime()` for duration measurement
- Pattern: create activity -> set tags -> execute operation -> set status -> dispose

**From Story 2.3 (MediatR Pipeline & Logging Behavior):**
- `LoggingBehavior.cs` creates `EventStore.CommandApi.Submit` activity (outermost pipeline behavior)
- Propagates correlation ID from `CorrelationIdMiddleware` via `IHttpContextAccessor`
- Measures pipeline duration via `Stopwatch`

### Git Intelligence

Recent commits show the progression through Epics 3-5:
- `f74a9d9` feat: Stories 4.4-4.5 & 5.1-5.4 - Persist-then-publish resilience, dead-letter routing, and security policies
- `452962a` feat: Stories 4.2 & 4.3 - Topic isolation and at-least-once delivery (#38)
- `72d7a53` Story 4.1: Event Publisher with CloudEvents 1.0 (#37)
- `226a260` Story 3.11: Actor state machine and checkpointed stages (#36)
- Patterns: Primary constructors, records, ConfigureAwait(false), NSubstitute + Shouldly
- OpenTelemetry activities added incrementally at each story -- this story completes the picture
- DI registration via `Add*` extension methods in `ServiceCollectionExtensions.cs`

### Testing Requirements

**OpenTelemetry Registration Tests (~3 new):**
- ServiceDefaults registers both ActivitySources
- Activity name constants match architecture
- Tag key constants follow `eventstore.*` namespace

**End-to-End Trace Tests (~7 new):**
- ProcessCommand creates outer activity
- All 5 pipeline stages create child activities
- Correlation ID tag on all activities
- Tenant ID tag on all activities
- Success status set on successful command
- Error status set on failed command
- Publish and dead-letter activities created

**CommandApi Controller Activity Tests (~4 new):**
- Submit activity created
- QueryStatus activity created
- Replay activity created
- Controller activities include correlation ID

**Existing Test Updates (~minimal):**
- Review existing `EventStoreActivitySourceTests.cs` for any new constant coverage
- Ensure test fixtures use `ActivityListener` pattern

**Total estimated: ~14-18 new tests + 0-5 modified tests**

**Note:** Integration tests verifying the full trace chain (API -> Actor -> Persistence -> Publication with a single trace ID) are out of scope for this story. Such tests require a running DAPR sidecar and are better suited for Story 7.5 (End-to-End Contract Tests with Aspire Topology).

**Nice-to-have (not required):** Consider using `Activity.AddEvent()` to record lifecycle milestones within long-running activities (e.g., "snapshot loaded", "events applied", "domain service responded"). This provides richer trace detail without creating additional spans. If added, keep events lightweight (no payload data per Rule #5).

### Failure Scenario Matrix

| Scenario | Expected Trace Behavior | Activity Status |
|----------|------------------------|----------------|
| Successful command (events persisted + published) | Full trace: Submit -> ProcessCommand -> [Idempotency, TenantValidation, Rehydration, DomainInvoke, Persist, Publish] -> Ok | All Ok |
| Domain rejection (IRejectionEvent) | Same trace as success (rejections are normal events) | ProcessCommand Ok, terminal status Rejected |
| Domain service invocation failure | Trace shows failure at DomainService.Invoke, dead-letter activity | DomainServiceInvoke Error, PublishDeadLetter Ok/Error |
| Event persistence failure | Trace shows failure at Events.Persist, dead-letter activity | EventsPersist Error, PublishDeadLetter Ok/Error |
| Pub/sub publication failure | Trace shows failure at Events.Publish, drain recovery later | EventsPublish Error, ProcessCommand terminal PublishFailed |
| Dead-letter publication failure | Dead-letter activity shows error, but main trace unaffected | PublishDeadLetter Error (non-blocking) |
| Status query | QueryStatus activity with correlation ID | Ok or Not Found |
| Command replay | Replay activity, then full processing trace | Ok or Conflict |

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 6, Story 6.1]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR35 OpenTelemetry traces spanning full command lifecycle]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR2 End-to-end command lifecycle within 200ms at p99]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR31 OTLP-compatible collector export]
- [Source: _bmad-output/planning-artifacts/architecture.md#OpenTelemetry Activity Naming table]
- [Source: _bmad-output/planning-artifacts/architecture.md#Structured Logging Pattern table]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 5 Never log event payload]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 9 correlationId everywhere]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 13 No stack traces in responses]
- [Source: src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs -- centralized activity naming]
- [Source: src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs -- CommandApi ActivitySource]
- [Source: src/Hexalith.EventStore.ServiceDefaults/Extensions.cs -- OTel configuration (GAP: missing Server source)]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs -- pipeline activities]
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs -- publication activity]
- [Source: src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs -- dead-letter activity]
- [Source: src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs -- MediatR pipeline activity]
- [Source: src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs -- correlation propagation]
- [Source: _bmad-output/implementation-artifacts/4-5-dead-letter-routing-with-full-context.md -- previous story patterns]
- [Source: https://opentelemetry.io/docs/languages/dotnet/traces/best-practices/ -- OTel .NET best practices]
- [Source: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs -- .NET distributed tracing guide]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
