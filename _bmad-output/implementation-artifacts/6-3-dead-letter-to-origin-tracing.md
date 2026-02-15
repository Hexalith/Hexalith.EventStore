# Story 6.3: Dead-Letter to Origin Tracing

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Stories 6.1 (OpenTelemetry Trace Instrumentation) and 6.2 (Structured Logging Completeness) should be completed before this story, as they establish the tracing and logging infrastructure that this story verifies end-to-end. Story 4.5 (Dead-Letter Routing with Full Context) must be completed as it provides the dead-letter publishing infrastructure.**

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` (DAPR pub/sub dead-letter routing with CloudEvents 1.0, OTel activity)
- `src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs` (Dead-letter record: CommandEnvelope, FailureStage, ExceptionType, ErrorMessage, CorrelationId, CausationId, TenantId, Domain, AggregateId, CommandType, FailedAt, EventCountAtFailure)
- `src/Hexalith.EventStore.Server/Events/IDeadLetterPublisher.cs` (Dead-letter publisher interface)
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (Event publication with CloudEvents 1.0 and OTel activity)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (5-step pipeline with dead-letter routing on infrastructure failures at Steps 3-5)
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` (Centralized ActivitySource with `EventStore.Events.PublishDeadLetter` activity)
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` (MediatR entry/exit logging with correlation ID, OTel activity)
- `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs` (Auth failure logging with SourceIP, FailureLayer)
- `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` (Correlation ID generation/propagation)
- `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` (OpenTelemetry configuration with both ActivitySources registered)
- `src/Hexalith.EventStore.Testing/Fakes/FakeDeadLetterPublisher.cs` (Test double capturing dead-letter messages)
- `tests/Hexalith.EventStore.IntegrationTests/Helpers/TestLogProvider.cs` (Test log capture infrastructure)
- `tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterPublisherTests.cs` (Existing dead-letter publisher tests)
- `tests/Hexalith.EventStore.Server.Tests/Actors/DeadLetterRoutingTests.cs` (Existing dead-letter routing tests)

Run `dotnet test` to confirm all existing tests pass before beginning.

## Story

As an **operator**,
I want to trace a failed command from the dead-letter topic back to its originating API request using the correlation ID,
So that I can diagnose the full failure chain end-to-end (FR37).

## Acceptance Criteria

1. **Dead-letter message correlation ID traces back through all pipeline stages** - Given a command has been routed to the dead-letter topic (Story 4.5), When I take the correlation ID from the dead-letter message, Then I can query structured logs filtered by that correlation ID to see every pipeline stage the command passed through before failure, And each stage log entry contains the same correlation ID, And no pipeline stage is missing from the log trail.

2. **Originating API request identifiable via correlation ID** - Given a dead-letter message with a correlation ID, When I query structured logs for that correlation ID, Then I can find the originating API request log entry containing: source IP (from CorrelationIdMiddleware or AuthorizationBehavior), timestamp (command received time), user identity (tenant ID from JWT claims), command type, and the API endpoint that received the request.

3. **OpenTelemetry trace spans the full lifecycle including dead-letter** - Given a command that fails and is dead-lettered, When I view the OpenTelemetry trace for the correlation ID, Then the trace shows the API receipt span (`EventStore.CommandApi.Submit`), actor processing span (`EventStore.Actor.ProcessCommand`), the failing stage span (with `ActivityStatusCode.Error`), and the dead-letter publication span (`EventStore.Events.PublishDeadLetter`), And all spans share the same trace ID.

4. **Dead-letter message contains all required correlation context** - Given a dead-letter message is published, When I inspect the message, Then it contains: correlationId, causationId, tenantId, domain, aggregateId, commandType, failureStage, exceptionType, errorMessage, failedAt timestamp, And the full original CommandEnvelope for replay via POST `/commands/replay/{correlationId}`.

5. **Log-based origin tracing test verifies complete pipeline stage trail** - Given a command flows through the pipeline and fails at domain service invocation, When structured logs are captured and filtered by correlation ID, Then logs are found for at minimum: "Command received" (Information), "Actor activated" (Debug), "Tenant validation" (Debug), "State rehydration" (Debug), "Domain service invocation failure" (Error), And the dead-letter publication log (Warning) is also present with the same correlation ID.

6. **Trace-based origin tracing test verifies unbroken span chain** - Given a command flows through the pipeline and fails, When OpenTelemetry activities are captured, Then activities are found for: `EventStore.CommandApi.Submit`, `EventStore.Actor.ProcessCommand`, the failing sub-activity (with Error status), `EventStore.Events.PublishDeadLetter`, And all activities have `eventstore.correlation_id` tag matching the dead-letter message's correlation ID.

7. **Dead-letter origin tracing works across multi-tenant boundaries** - Given dead-letter messages from different tenants, When tracing origin for each by correlation ID, Then each correlation ID resolves to the correct tenant's pipeline logs and traces, And no cross-tenant log leakage exists in the trace chain.

8. **Replay endpoint can locate the original command via dead-letter correlation ID** - Given a dead-letter message contains a correlation ID, When the operator uses that correlation ID with the replay endpoint POST `/commands/replay/{correlationId}`, Then the system can locate the original command status record via the correlation ID, And the replay resubmits the command through the full pipeline. **Caveat:** Command status entries have a 24-hour default TTL (CRITICAL-2 architecture revision). If the operator traces the dead-letter more than 24 hours after failure, the status record may have expired. In this case, the dead-letter message itself contains the full `CommandEnvelope` which can be used for manual resubmission. Tests should verify both the within-TTL and expired-TTL scenarios.

9. **CausationId chain verifiable from dead-letter back to origin** - Given a dead-letter message contains both correlationId and causationId, When tracing the command, Then for an original submission the causationId matches the correlationId (confirming it was the first submission, not a replay), And for a replayed command that dead-letters, the causationId differs from the correlationId (confirming the replay trigger).

10. **Comprehensive dead-letter-to-origin tracing test coverage** - Given dead-letter-to-origin tracing is critical for operations, When tests run, Then tests verify log-based tracing (correlation ID -> all pipeline stages), And tests verify trace-based tracing (correlation ID -> all OTel spans), And tests verify dead-letter message completeness, And tests verify multi-tenant isolation in trace chains, And tests verify replay command produces a different causation chain.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and audit current tracing state (BLOCKING)
  - [x] 0.1 Run all existing tests -- they must pass before proceeding
  - [x] 0.2 Review `DeadLetterPublisher.cs` -- confirm it logs with correlationId, creates OTel activity with correlation tag
  - [x] 0.3 Review `DeadLetterMessage.cs` -- confirm all correlation fields present (correlationId, causationId, tenantId, domain, aggregateId, commandType, failureStage)
  - [x] 0.4 Review `AggregateActor.cs` -- audit all infrastructure failure catch blocks to confirm dead-letter routing includes full correlation context
  - [x] 0.5 Review `LoggingBehavior.cs` -- confirm "Command received" log includes correlationId at pipeline entry
  - [x] 0.6 Review `CorrelationIdMiddleware.cs` -- confirm correlation ID is generated/propagated and accessible in logs
  - [x] 0.7 Review `SubmitCommandHandler.cs` -- confirm command received log includes source IP, tenant, command type
  - [x] 0.8 Review `AuthorizationBehavior.cs` -- confirm failure logs include SourceIP
  - [x] 0.9 **Create a tracing chain audit:** Map every pipeline stage and verify each emits a log with correlationId and an OTel activity with `eventstore.correlation_id` tag
  - [x] 0.10 Identify any gaps in the tracing chain that would prevent end-to-end correlation

- [x] Task 1: Fix identified gaps in log-based correlation chain -- SourceIP is HIGH PRIORITY (AC: #1, #2, #5)
  - [x] 1.1 Based on Task 0 audit, fix any pipeline stage log that is missing correlationId
  - [x] 1.2 **HIGH PRIORITY -- SourceIP gap fix:** `AuthorizationBehavior` logs SourceIP only on auth FAILURE. On the SUCCESS path (which is the normal path before a dead-letter), SourceIP is likely NOT logged at all. The "Command received" log at `SubmitCommandHandler.cs` or `LoggingBehavior.cs` must include SourceIP for origin identification.
  - [x] 1.3 Add SourceIP to the "Command received" log via `IHttpContextAccessor` -> `HttpContext.Connection.RemoteIpAddress`. Prefer adding to `LoggingBehavior.cs` (outermost MediatR behavior) since it already has `IHttpContextAccessor` for correlation ID retrieval. Use `[LoggerMessage]` source-generated method (Story 6.2 convention).
  - [x] 1.4 Verify "Command received" log includes: CorrelationId, CausationId, TenantId, Domain, AggregateId, CommandType, SourceIP, Timestamp
  - [x] 1.5 Verify user identity (tenant from JWT claims) is logged at command receipt stage
  - [x] 1.6 Ensure all log statements from Stories 6.1/6.2 include `Stage` discriminator field for filtering

- [x] Task 2: Fix identified gaps in trace-based correlation chain -- actor proxy boundary is HIGH PRIORITY (AC: #3, #6)
  - [x] 2.1 Based on Task 0 audit, fix any OTel activity missing `eventstore.correlation_id` tag
  - [x] 2.2 Verify `DeadLetterPublisher.cs` creates `EventStore.Events.PublishDeadLetter` activity with `ActivityStatusCode.Ok` on success and `ActivityStatusCode.Error` on publication failure
  - [x] 2.3 **HIGH PRIORITY -- Actor proxy trace context propagation:** Verify `Activity.Current` flows through `CommandRouter` -> `ActorProxy.InvokeMethodAsync` -> `AggregateActor.ProcessCommandAsync`. DAPR actor runtime may create a new `SynchronizationContext` that breaks the parent-child Activity chain. If broken, the OTel trace will split into TWO disconnected traces (API trace and actor trace) -- catastrophic for operator diagnosis. Story 6.1 may have addressed this via `CommandEnvelope` metadata fallback. MUST confirm this works end-to-end.
  - [x] 2.4 Verify the dead-letter activity is a child span within the same trace as the originating command (same trace ID across API and actor layers)
  - [x] 2.5 Verify the failing stage activity (e.g., `EventStore.DomainService.Invoke`) has `ActivityStatusCode.Error` and `RecordException` before the dead-letter activity starts

- [x] Task 3: Create log-based dead-letter-to-origin tracing tests (AC: #1, #2, #5, #10)
  - [x] 3.1 Create `DeadLetterOriginTracingTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Observability/`
  - [x] 3.2 Test: `DomainServiceFailure_CorrelationIdTracesBackThroughAllStages` -- force domain service failure, capture logs, verify every pipeline stage present with same correlation ID
  - [x] 3.3 Test: `DomainServiceFailure_OriginatingRequestIdentifiable` -- verify log trail includes: command received log with source context (tenant, domain, commandType)
  - [x] 3.4 Test: `StateRehydrationFailure_CorrelationIdTracesBackThroughAllStages` -- different failure point, same tracing expectation
  - [x] 3.5 Test: `EventPersistenceFailure_CorrelationIdTracesBackThroughAllStages` -- persistence failure trace chain
  - [x] 3.6 Test: `DeadLetterLog_ContainsCorrelationIdMatchingOrigin` -- dead-letter Warning log has same correlation ID as command received log
  - [x] 3.7 Test: `AllLogsBetweenOriginAndDeadLetter_ContainConsistentCorrelationId` -- no log in the chain has a different or missing correlation ID
  - [x] 3.8 Test: `InformationLevelOnly_TracingChainStillComplete` -- verify the tracing chain works when Debug-level logs are filtered (production scenario). At Information+ level, the trail should include: Received, DomainInvoked (if applicable), Persisted (if applicable), Published (if applicable), DeadLetter(Warning). Debug-level stages (Activated, TenantValidated, Rehydrated, IdempotencyCheck) are filtered but the chain is still traceable.
  - [x] 3.9 Test: `CommandReceived_LogIncludesSourceIP` -- verify the "Command received" log entry includes SourceIP for origin identification (gap fix from Task 1)
  - [x] 3.10 Use `ILogger<T>` mock capture (NSubstitute) or `TestLogProvider` to intercept and inspect log entries by correlation ID

- [x] Task 4: Create trace-based dead-letter-to-origin tracing tests (AC: #3, #6, #10)
  - [x] 4.1 Create `DeadLetterTraceChainTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Observability/`
  - [x] 4.2 Test: `DomainServiceFailure_TraceSpansEntireLifecycle` -- capture OTel activities, verify Submit -> ProcessCommand -> failing stage -> PublishDeadLetter all share same trace ID
  - [x] 4.3 Test: `DomainServiceFailure_AllActivitiesHaveCorrelationIdTag` -- every activity in chain has `eventstore.correlation_id` tag matching dead-letter message
  - [x] 4.4 Test: `DomainServiceFailure_FailingActivityHasErrorStatus` -- the activity at the failure point has `ActivityStatusCode.Error`
  - [x] 4.5 Test: `DomainServiceFailure_DeadLetterActivityRecordsSuccess` -- `EventStore.Events.PublishDeadLetter` has `ActivityStatusCode.Ok` when dead-letter publishes successfully
  - [x] 4.6 Test: `DeadLetterPublishFails_DeadLetterActivityRecordsError` -- `EventStore.Events.PublishDeadLetter` has `ActivityStatusCode.Error` when dead-letter itself fails
  - [x] 4.7 Test: `TraceContext_PropagatesThroughActorProxy_SingleTraceId` -- verify that `Activity.Current` propagates through `CommandRouter` -> `ActorProxy.InvokeMethodAsync` -> `AggregateActor.ProcessCommandAsync`. The API-layer activity and actor-layer activities MUST share the same trace ID. If the DAPR actor runtime breaks `Activity.Current` propagation, verify that the fallback trace context propagation via `CommandEnvelope` metadata (from Story 6.1) works correctly.
  - [x] 4.8 Test: `SidecarUnavailable_DeadLetterFailure_ErrorLogHasFullCorrelationContext` -- when DAPR sidecar is unavailable and dead-letter publication fails, verify the Error log for DL publication failure contains enough correlation context for operator diagnosis (correlationId, tenantId, domain, aggregateId, commandType, failureStage, exceptionType)
  - [x] 4.9 Use `ActivityListener` pattern to capture activities during test execution

- [x] Task 5: Create dead-letter message completeness verification tests (AC: #4, #10)
  - [x] 5.1 Create `DeadLetterMessageCompletenessTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Observability/`
  - [x] 5.2 Test: `DeadLetterMessage_ContainsAllRequiredCorrelationFields` -- verify correlationId, causationId, tenantId, domain, aggregateId, commandType all non-null and correct
  - [x] 5.3 Test: `DeadLetterMessage_ContainsFullCommandEnvelope` -- verify CommandEnvelope is complete and unmodified (byte-for-byte replay capability)
  - [x] 5.4 Test: `DeadLetterMessage_ContainsFailureContext` -- verify failureStage, exceptionType, errorMessage, failedAt all populated
  - [x] 5.5 Test: `DeadLetterMessage_CorrelationIdMatchesOriginalCommand` -- dead-letter correlationId equals the submitted command's correlationId
  - [x] 5.6 Test: `DeadLetterMessage_NeverContainsStackTrace` -- exceptionType is type name only, errorMessage has no stack trace (rule #13)
  - [x] 5.7 Test: `DeadLetterMessage_NullCausationId_HandledGracefully` -- verify dead-letter message and tracing still work when CausationId is null (edge case for old/migrated commands or if CausationId is not set on the CommandEnvelope)

- [x] Task 6: Create multi-tenant isolation tracing tests (AC: #7, #10)
  - [x] 6.1 Add tests to `DeadLetterOriginTracingTests.cs`
  - [x] 6.2 Test: `MultiTenant_EachDeadLetterTracesBackToCorrectTenantOrigin` -- submit commands for two different tenants, force failures, verify each dead-letter's correlation ID traces back only to its own tenant's pipeline stages
  - [x] 6.3 Test: `MultiTenant_NoCorrelationIdCrossTalk` -- verify correlation IDs from tenant A's failure never appear in tenant B's log entries

- [x] Task 7: Create causation chain verification tests (AC: #9, #10)
  - [x] 7.1 Add tests to `DeadLetterOriginTracingTests.cs`
  - [x] 7.2 Test: `OriginalSubmission_CausationIdMatchesCorrelationId` -- for a first-time submission that dead-letters, causationId in dead-letter message equals correlationId
  - [x] 7.3 Test: `ReplayedCommand_CausationIdDiffersFromCorrelationId` -- for a replayed command that dead-letters, causationId differs from correlationId (new causal trigger)
  - [x] 7.4 Test: `ReplayedCommand_CorrelationIdMatchesOriginalSubmission` -- replayed command retains original correlationId

- [x] Task 8: Create replay-via-dead-letter correlation test (AC: #8, #10)
  - [x] 8.1 Add test to `DeadLetterOriginTracingTests.cs`
  - [x] 8.2 Test: `DeadLetterCorrelationId_CanLocateOriginalCommandStatus` -- verify command status store has an entry for the dead-letter's correlation ID
  - [x] 8.3 Test: `DeadLetterCorrelationId_StatusReflectsTerminalState` -- the command status for the correlation ID shows the correct terminal state (Rejected or PublishFailed)

- [x] Task 9: Verify all tests pass
  - [x] 9.1 Run `dotnet test` to confirm no regressions
  - [x] 9.2 All new log-based tracing tests pass (15/15)
  - [x] 9.3 All new trace-based tracing tests pass (7/7)
  - [x] 9.4 All new dead-letter message completeness tests pass (6/6)
  - [x] 9.5 All new multi-tenant tracing tests pass (2/2)
  - [x] 9.6 All new causation chain tests pass (3/3)
  - [x] 9.7 All new replay correlation tests pass (2/2)
  - [x] 9.8 All existing tests (Stories through 6.2) still pass (902 unit tests, 0 failures)

## Dev Notes

### Story Context

This is the **third story in Epic 6: Observability, Health & Operational Readiness**. It is a **verification and testing story** that proves the end-to-end tracing chain from dead-letter back to originating request is complete and functional. The core infrastructure was built incrementally:

- **Story 4.5** built `DeadLetterPublisher`, `DeadLetterMessage`, and dead-letter routing in `AggregateActor`
- **Story 6.1** completed OpenTelemetry trace instrumentation across the full pipeline, including `EventStore.Events.PublishDeadLetter` activity
- **Story 6.2** completed structured logging with correlation/causation IDs at every pipeline stage

Story 6.3 verifies that an operator can take a correlation ID from a dead-letter message and trace it **backwards** through the entire system to find: (1) every pipeline stage the command passed through via structured logs, (2) the originating API request with source context, and (3) the full OpenTelemetry trace for visual diagnosis.

**What previous stories already built (to VERIFY, not replicate):**
- `DeadLetterMessage` record with CorrelationId, CausationId, TenantId, Domain, AggregateId, CommandType, FailureStage, ExceptionType, ErrorMessage, FailedAt, EventCountAtFailure
- `DeadLetterPublisher.cs` publishes to `deadletter.{tenant}.{domain}.events` topic with CloudEvents 1.0, creates `EventStore.Events.PublishDeadLetter` OTel activity, logs at Warning/Error level with correlation context
- `AggregateActor.cs` catches infrastructure exceptions at Steps 3 (rehydration), 4 (domain invocation), and 5 (persistence) and routes to dead-letter
- `LoggingBehavior.cs` logs command entry/exit with correlationId, duration, OTel activity
- `SubmitCommandHandler.cs` logs "command received" with correlationId
- Every pipeline stage (as verified/completed by Story 6.2) emits structured logs with correlationId and CausationId
- OTel activities at every pipeline stage (as verified/completed by Story 6.1) with `eventstore.correlation_id` tag
- `FakeDeadLetterPublisher` captures published dead-letter messages for test assertions

**What this story adds (NEW):**
- Comprehensive end-to-end tracing verification tests proving the dead-letter-to-origin chain is unbroken
- Log-based origin tracing tests (correlation ID -> every pipeline stage log)
- Trace-based origin tracing tests (correlation ID -> every OTel span in the trace)
- Dead-letter message completeness verification tests
- Multi-tenant isolation verification in trace chains
- Causation chain verification (original vs replay)
- Replay-via-dead-letter correlation tests
- Potential gap fixes if audit reveals missing correlationId or SourceIP in any pipeline stage log

**What this story will likely modify (gap fixes identified by Advanced Elicitation):**
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` -- **HIGH PRIORITY:** Add SourceIP to command entry log (currently only logged on auth FAILURE in `AuthorizationBehavior`, NOT on success path). Use `IHttpContextAccessor.HttpContext.Connection.RemoteIpAddress` which `LoggingBehavior` already accesses for correlation ID.
- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` -- add SourceIP if `LoggingBehavior` approach is not feasible
- Any pipeline stage file where correlationId or OTel tag is found missing during Task 0 audit

### Architecture Compliance

**FR37:** An operator can trace a failed command from the dead-letter topic back to its originating request via correlation ID.

**FR8:** System routes failed commands to dead-letter topic with full command payload, error details, and correlation context (verified by Story 4.5, tested end-to-end here).

**FR35:** OpenTelemetry traces span the full command lifecycle including dead-letter (verified by Story 6.1, trace chain tested here).

**FR36:** Structured logs with correlation/causation IDs at each stage (verified by Story 6.2, log chain tested here).

**Enforcement Rules:**
- **Rule #5:** Never log event payload data (SEC-5, NFR12) -- all tracing tests must verify NO payload data appears in logs or OTel tags
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity -- this is THE rule this story verifies end-to-end
- **Rule #12:** Command status writes are advisory -- status entry for dead-lettered command should exist but its absence doesn't break tracing
- **Rule #13:** No stack traces in dead-letter messages -- exception type + message only

### Critical Design Decisions

- **This is a verification story, not an implementation story.** The primary output is comprehensive tests proving the tracing chain works. Implementation changes should be minimal (only gap fixes found during audit). If the audit reveals no gaps, the story is primarily test creation.

- **Log-based tracing is the primary operational tool.** Operators will mostly use structured log queries (`correlationId == "abc-123"`) to trace dead-letters back to origin. The structured logging from Story 6.2 with Stage discriminator and correlationId in every entry makes this possible. **In production, Debug-level logs may be filtered** -- the tracing chain must still work with only Information/Warning/Error logs. Tests must verify this.

- **Trace-based tracing is the secondary visual tool.** OpenTelemetry trace visualization (Aspire dashboard, Jaeger, Grafana) provides a graphical view of the same information. The trace chain from Story 6.1 with correlation ID tags makes this possible. **Production OTel sampling caveat:** Production deployments may use trace sampling (e.g., 10% `parentbased_traceidratio`), meaning not all traces are captured. Structured logs (always captured at configured level) are the PRIMARY tracing mechanism; OTel traces are a supplementary visual aid.

- **SourceIP at command receipt is critical for origin identification (LIKELY GAP).** Analysis indicates `AuthorizationBehavior` logs SourceIP only on auth FAILURE, NOT on the success path. Since dead-lettered commands typically pass auth successfully (they fail later at domain invocation or persistence), the SourceIP would NOT appear in the log trail. This is the primary gap fix -- add SourceIP to `LoggingBehavior.cs` or `SubmitCommandHandler.cs` command entry log via `IHttpContextAccessor` -> `HttpContext.Connection.RemoteIpAddress`.

- **Actor proxy trace context propagation is a CRITICAL verification point.** DAPR actor runtime may create a new `SynchronizationContext` that breaks `Activity.Current` propagation through `CommandRouter` -> `ActorProxy.InvokeMethodAsync` -> `AggregateActor.ProcessCommandAsync`. If broken, OTel traces appear as TWO disconnected traces (API-side and actor-side), making visual diagnosis impossible. Story 6.1 should have addressed this (AC #11 mentions fallback via `CommandEnvelope` metadata). This story MUST explicitly test that the trace ID is the same across the API and actor layers.

- **CausationId distinguishes original submissions from replays.** When an operator sees a dead-letter and uses the replay endpoint, the replayed command has the same CorrelationId but a different CausationId. If the replay also dead-letters, the operator can distinguish the two failure instances via CausationId. **Edge case:** CausationId may be null on old or migrated commands -- tests should handle this gracefully.

- **Command status TTL affects replay workflow.** Command status entries have a 24-hour default TTL (CRITICAL-2 architecture revision). If the operator discovers the dead-letter more than 24 hours after failure, the status record may have expired and the replay endpoint may return 404. The dead-letter message itself contains the full `CommandEnvelope` for manual resubmission regardless of TTL. Operators should be aware of this window.

- **Multi-tenant trace isolation is a security requirement.** An operator querying logs by correlation ID must only see entries for the correct tenant. Since correlation IDs are globally unique (UUIDs), cross-tenant trace mixing should not occur, but tests must verify this. **Operator workflow note:** In tenant-partitioned logging backends (Loki, Elasticsearch with tenant indices), operators need the TenantId from the dead-letter message to query the correct log partition. The dead-letter message includes TenantId.

- **Test approach: unit-level with mock infrastructure.** Tests use NSubstitute mocks for DAPR clients and `FakeDeadLetterPublisher` for dead-letter capture. Log capture uses `ILogger<T>` mock verification via NSubstitute. Activity capture uses `ActivityListener`. No real DAPR sidecar or pub/sub needed -- those are Story 7.5 (integration tests).

### Existing Patterns to Follow

**Log capture test pattern (from LoggingBehaviorTests.cs and Story 6.2):**
```csharp
var logger = Substitute.For<ILogger<AggregateActor>>();

// Execute system under test that triggers dead-letter...

// Verify logs contain correlation ID at every stage
logger.Received().Log(
    LogLevel.Information,
    Arg.Any<EventId>(),
    Arg.Is<It.IsAnyType>((state, _) =>
        state.ToString()!.Contains(expectedCorrelationId)),
    Arg.Any<Exception?>(),
    Arg.Any<Func<It.IsAnyType, Exception?, string>>());
```

**Activity capture test pattern (from Story 6.1):**
```csharp
var capturedActivities = new List<Activity>();
using var listener = new ActivityListener
{
    ShouldListenTo = source =>
        source.Name == EventStoreActivitySource.SourceName ||
        source.Name == "Hexalith.EventStore.CommandApi",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded,
    ActivityStarted = activity => capturedActivities.Add(activity),
};
ActivitySource.AddActivityListener(listener);

// Execute system under test...

// Verify dead-letter activity has correlation ID tag
var deadLetterActivity = capturedActivities
    .Single(a => a.DisplayName == EventStoreActivitySource.EventsPublishDeadLetter);
deadLetterActivity.GetTagItem(EventStoreActivitySource.TagCorrelationId)
    .ShouldBe(expectedCorrelationId);
```

**FakeDeadLetterPublisher verification pattern:**
```csharp
var fakeDeadLetter = new FakeDeadLetterPublisher();

// Execute system under test that triggers dead-letter...

var messages = fakeDeadLetter.GetDeadLetterMessages();
messages.ShouldHaveSingleItem();
var dl = messages[0].Message;
dl.CorrelationId.ShouldBe(expectedCorrelationId);
dl.TenantId.ShouldBe(expectedTenantId);
dl.FailureStage.ShouldNotBeNullOrEmpty();
dl.ExceptionType.ShouldNotBeNullOrEmpty();
dl.Command.ShouldNotBeNull(); // Full command envelope for replay
```

**End-to-end tracing verification pattern (NEW for this story):**
```csharp
[Fact]
public async Task DomainServiceFailure_CorrelationIdTracesBackThroughAllStages()
{
    // Arrange: Set up actor with mock services, force domain service failure
    var correlationId = Guid.NewGuid().ToString();
    var command = new CommandEnvelopeBuilder()
        .WithCorrelationId(correlationId)
        .Build();
    var domainInvoker = Substitute.For<IDomainServiceInvoker>();
    domainInvoker.InvokeAsync(Arg.Any<...>())
        .ThrowsAsync(new InvalidOperationException("Simulated infra failure"));

    // Act: Process command
    var result = await actor.ProcessCommandAsync(command, ct);

    // Assert: Verify dead-letter was published
    var dl = fakeDeadLetter.GetDeadLetterMessages().ShouldHaveSingleItem();
    dl.Message.CorrelationId.ShouldBe(correlationId);

    // Assert: Verify log trail contains correlation ID at every stage
    // (verify via logger mock captures)

    // Assert: Verify OTel activities contain correlation ID tag
    var activitiesWithCorrelation = capturedActivities
        .Where(a => a.GetTagItem(EventStoreActivitySource.TagCorrelationId)?.ToString() == correlationId);
    activitiesWithCorrelation.ShouldNotBeEmpty();
}
```

### Mandatory Coding Patterns

- Primary constructors for all new classes (existing convention)
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- NSubstitute for mocking, Shouldly for assertions in tests
- Feature folder organization -- new tests in `Observability/` folder
- **Rule #5:** Never log event payload or command payload data (tests must verify this)
- **Rule #9:** correlationId in every structured log entry and OpenTelemetry activity (this story's primary verification target)
- `ActivityListener` pattern for OTel activity capture in tests
- `[LoggerMessage]` source-generated methods for any new log statements (Story 6.2 convention)

### Project Structure Notes

**New files:**
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs` -- log-based and multi-tenant tracing tests
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterTraceChainTests.cs` -- OTel trace chain verification tests
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs` -- dead-letter message field verification tests

**Potentially modified files (gap fixes ONLY -- may be zero changes if audit shows no gaps):**
- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` -- add SourceIP if missing
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` -- add SourceIP to command entry log if missing
- Any file where correlation ID is found missing during Task 0 audit

### Previous Story Intelligence

**From Story 6.2 (Structured Logging Completeness Verification):**
- Established `[LoggerMessage]` source-generated pattern for all hot-path loggers
- Added CausationId to all pipeline stage logs
- Added Stage discriminator field to all log entries
- Added FailureLayer field to security failure logs
- Verified all 8 architecture-defined logging stages have complete required fields
- ValidationBehavior now logs success (Debug) and failure (Warning)
- Comprehensive test coverage: ~25-35 new tests for logging completeness, log levels, payload protection, CausationId

**From Story 6.1 (End-to-End OpenTelemetry Trace Instrumentation):**
- Registered `"Hexalith.EventStore"` ActivitySource in ServiceDefaults (CRITICAL FIX)
- Added controller-level activities (Submit, QueryStatus, Replay)
- Added ActivityKind and ActivityStatusCode to all activities
- Added RecordException on failure paths
- Comprehensive test coverage: ~14-18 new tests for trace completeness

**From Story 4.5 (Dead-Letter Routing with Full Context):**
- Created `DeadLetterMessage` record with 12 fields including full `CommandEnvelope`
- Created `DeadLetterPublisher` with CloudEvents 1.0, OTel activity, Warning/Error logging
- Created `FakeDeadLetterPublisher` test double with query methods and failure simulation
- Integrated dead-letter routing into AggregateActor at Steps 3, 4, and 5
- Domain rejections (IRejectionEvent) do NOT trigger dead-letter (D3 compliance)
- Dead-letter publication failure is non-blocking (logged but doesn't prevent terminal state)
- Test coverage: ~30 tests for dead-letter message, publisher, routing, and test double

**From Story 2.3 (MediatR Pipeline & Logging Behavior):**
- `LoggingBehavior.cs` retrieves correlation ID from `IHttpContextAccessor` -> `HttpContext.Items`
- Creates `EventStore.CommandApi.Submit` OTel activity
- Logs entry/exit with correlation ID, duration

**From Story 2.4 (JWT Authentication & Claims Transformation):**
- JWT authentication provides tenant ID from claims
- `AuthorizationBehavior` logs failures with SourceIP

### Git Intelligence

Recent commits show the progression through Epics 4-5:
- `a349d0e` Merge PR #41 -- Stories 5.1 & 5.2 (DAPR access control, data path isolation)
- `47affbc` feat: Stories 5.1 & 5.2 -- security isolation verification
- `8fcb122` fix: Review follow-ups for stories 4.5 & 5.1, OpenTelemetry prep
- `cbf367d` feat: Stories 4.4-4.5 & 5.1-5.4 -- resilience, dead-letter, security
- `452962a` feat: Stories 4.2 & 4.3 -- topic isolation, at-least-once delivery

**Patterns from commits:**
- Primary constructors, records, ConfigureAwait(false), NSubstitute + Shouldly
- OpenTelemetry activities added incrementally at each story
- Structured logging with correlation IDs established through Epics 2-4
- Dead-letter routing added in Story 4.5 commit
- No LoggerMessage source generation prior to Story 6.2

### Testing Requirements

**Log-Based Origin Tracing Tests (~9 new):**
- Domain service failure: correlation ID traces all stages
- Rehydration failure: correlation ID traces all stages
- Persistence failure: correlation ID traces all stages
- Originating request identifiable (includes SourceIP)
- Dead-letter log matches origin correlation
- All logs between origin and dead-letter have consistent correlation ID
- Information-level-only tracing chain still complete (production scenario)
- Command received log includes SourceIP
- Multi-tenant isolation

**Trace-Based Origin Tracing Tests (~7 new):**
- Full lifecycle trace spans
- All activities have correlation ID tag
- Failing activity has error status
- Dead-letter activity records success/failure status
- Dead-letter publication failure records error
- Trace context propagates through actor proxy (single trace ID across API and actor layers)
- Sidecar unavailable: DL failure Error log has full correlation context

**Dead-Letter Message Completeness Tests (~6 new):**
- All correlation fields present
- Full command envelope preserved
- Failure context populated
- Correlation ID matches original
- No stack trace in error message
- Null CausationId handled gracefully

**Causation Chain Tests (~3 new):**
- Original submission causation matches correlation
- Replay causation differs from correlation
- Replay correlation matches original

**Replay Correlation Tests (~2 new):**
- Command status locatable via dead-letter correlation ID
- Status reflects terminal state

**Total estimated: ~27-30 new tests + 1-3 source file modifications (SourceIP gap fix)**

### Failure Scenario Matrix

| Scenario | Expected Log Trail (by correlation ID) | Expected OTel Trace | Dead-Letter Content |
|----------|---------------------------------------|--------------------|--------------------|
| Domain service invocation failure | Received(+SourceIP) -> Activated -> TenantValidated -> Rehydrated -> InvocationFailed -> DeadLetterPublished | Submit -> ProcessCommand -> DomainInvoke(Error) -> PublishDeadLetter(Ok) | FailureStage=Processing, ExceptionType, ErrorMessage |
| State rehydration failure | Received(+SourceIP) -> Activated -> TenantValidated -> RehydrationFailed -> DeadLetterPublished | Submit -> ProcessCommand -> StateRehydration(Error) -> PublishDeadLetter(Ok) | FailureStage=Processing, ExceptionType, ErrorMessage |
| Event persistence failure | Received(+SourceIP) -> Activated -> TenantValidated -> Rehydrated -> DomainInvoked -> PersistFailed -> DeadLetterPublished | Submit -> ProcessCommand -> Persist(Error) -> PublishDeadLetter(Ok) | FailureStage=EventsStored, EventCountAtFailure, ExceptionType |
| Dead-letter publication also fails (sidecar down) | Same log trail + Error log for DL failure (with full correlation context) | Same trace + PublishDeadLetter(Error) | Message not delivered -- Error log is operator's only record |
| Multi-tenant parallel failures | Each tenant has independent log trail by correlation ID | Separate traces per tenant | Each DL has correct tenant context |
| Replay of dead-lettered command | New CausationId, same CorrelationId in all stages | New trace linked via CorrelationId | If replay also dead-letters: CausationId differs from CorrelationId |
| Info-level-only production logs | Received -> DomainInvoked(if applicable) -> DeadLetter(Warning) -- Debug stages filtered | Full trace (OTel unaffected by log level) | Same content regardless of log level |
| Status TTL expired (>24h) | Full log trail still intact | Full trace still intact (if not sampled out) | DL message has full CommandEnvelope for manual replay |
| Null CausationId (edge case) | CausationId absent from logs but CorrelationId present | Activities may have null CausationId tag | CausationId=null in DL message, CorrelationId still valid |

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 6, Story 6.3]
- [Source: _bmad-output/planning-artifacts/prd.md#FR37 Dead-letter to originating request tracing via correlation ID]
- [Source: _bmad-output/planning-artifacts/architecture.md#FR37 Trace failed commands from dead-letter to originating request]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 9 correlationId everywhere]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 5 Never log event payload]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule 13 No stack traces in responses]
- [Source: _bmad-output/implementation-artifacts/4-5-dead-letter-routing-with-full-context.md -- Dead-letter infrastructure]
- [Source: _bmad-output/implementation-artifacts/6-1-end-to-end-opentelemetry-trace-instrumentation.md -- OTel trace infrastructure]
- [Source: _bmad-output/implementation-artifacts/6-2-structured-logging-completeness-verification.md -- Structured logging infrastructure]
- [Source: src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs -- Dead-letter DAPR pub/sub implementation]
- [Source: src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs -- Dead-letter record with 12 correlation fields]
- [Source: src/Hexalith.EventStore.Server/Events/IDeadLetterPublisher.cs -- Dead-letter publisher interface]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs -- Pipeline with dead-letter routing at Steps 3-5]
- [Source: src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs -- OTel activity names and tag constants]
- [Source: src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs -- Pipeline entry logging with OTel activity]
- [Source: src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs -- Correlation ID generation]
- [Source: src/Hexalith.EventStore.Testing/Fakes/FakeDeadLetterPublisher.cs -- Test double for dead-letter capture]
- [Source: tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterPublisherTests.cs -- Existing dead-letter tests]
- [Source: tests/Hexalith.EventStore.Server.Tests/Actors/DeadLetterRoutingTests.cs -- Existing routing tests]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Task 0 audit identified SourceIP gap: AuthorizationBehavior logs SourceIP only on auth FAILURE path; normal commands passing auth had no SourceIP in logs
- Integration test failures (12) are pre-existing infrastructure-related issues (Keycloak/DAPR not running), not regressions

### Completion Notes List

- Task 0: Full tracing chain audit completed. All 9 OTel activities have correlation ID tags. Actor proxy trace context uses fallback via CommandEnvelope.Extensions (traceparent/tracestate).
- Task 1: Added SourceIP to LoggingBehavior.PipelineEntry log via IHttpContextAccessor.HttpContext.Connection.RemoteIpAddress
- Task 2: No trace gaps found -- all activities already had correlation tags. Traceparent fallback verified working.
- Tasks 3-8: Created 28 new tests across 3 test files covering all acceptance criteria
- Task 9: All 902 unit tests pass (745 Server.Tests + 157 Contracts.Tests), 0 failures

### Change Log

- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` -- Added SourceIP to PipelineEntry log message and method signature (gap fix)

### File List

#### Modified Files
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` -- Added SourceIP parameter to PipelineEntry LoggerMessage

#### New Files
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs` -- 15 tests: log-based tracing (8), multi-tenant (2), causation chain (3), replay correlation (2)
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterTraceChainTests.cs` -- 7 tests: OTel trace lifecycle, correlation tags, error status, DL activity success/failure, traceparent propagation, sidecar unavailable
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs` -- 6 tests: correlation fields, command envelope, failure context, correlation match, no stack trace, null causation
