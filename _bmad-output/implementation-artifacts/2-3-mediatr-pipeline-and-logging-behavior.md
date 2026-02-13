# Story 2.3: MediatR Pipeline & Logging Behavior

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **developer**,
I want the CommandApi to use a MediatR pipeline with LoggingBehavior as the outermost behavior, producing structured logs with correlation and causation IDs at each pipeline stage,
So that every command can be traced through the API layer.

## Acceptance Criteria

1. **LoggingBehavior is outermost** - Given a command is submitted to the API, When it flows through the MediatR pipeline, Then LoggingBehavior is the first behavior in the pipeline (outermost), wrapping all subsequent behaviors including ValidationBehavior.

2. **Structured log entry/exit** - LoggingBehavior logs entry with correlation ID, command type, tenant, and domain at `Information` level, and logs exit with the same fields plus duration in milliseconds.

3. **No payload in logs** - Event payload data never appears in logs (SEC-5, NFR12). Only command metadata (tenant, domain, aggregateId, commandType, correlationId) may be logged. The `Payload` and `Extensions` fields of the command MUST NOT be logged.

4. **OpenTelemetry activity** - Basic OpenTelemetry activities are created for the command submission span using an `ActivitySource` named with the `EventStore.CommandApi.Submit` pattern per architecture. The activity includes tags for correlationId, tenant, domain, commandType.

5. **Pipeline order enforced** - Pipeline order is: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler. LoggingBehavior captures validation failures and unhandled exceptions in its exit log.

## Tasks / Subtasks

- [x] Task 1: Create LoggingBehavior<TRequest, TResponse> (AC: #1, #2, #3, #5)
  - [x] 1.1 Create `LoggingBehavior.cs` in `CommandApi/Pipeline/` implementing `IPipelineBehavior<TRequest, TResponse>`
  - [x] 1.2 Inject `ILogger<LoggingBehavior<TRequest, TResponse>>` and `IHttpContextAccessor` for correlation ID access
  - [x] 1.3 Log entry at `Information`: correlationId, commandType (typeof TRequest), tenant, domain extracted from request if `SubmitCommand`
  - [x] 1.4 Measure duration using `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime()`
  - [x] 1.5 Log exit at `Information`: same fields plus `durationMs`
  - [x] 1.6 On exception: log at `Error` level with correlationId, commandType, exceptionType, exception message (NOT payload, NOT stack trace in structured fields), then rethrow
  - [x] 1.7 Ensure `ConfigureAwait(false)` on all async calls (CA2007), `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)

- [x] Task 2: Add OpenTelemetry ActivitySource for MediatR pipeline (AC: #4)
  - [x] 2.1 Define a static `ActivitySource` (e.g., `EventStoreActivitySources.CommandApi`) with name `"Hexalith.EventStore.CommandApi"`
  - [x] 2.2 In LoggingBehavior, start an activity named `EventStore.CommandApi.Submit` (or derive from request type)
  - [x] 2.3 Add tags: `eventstore.correlation_id`, `eventstore.tenant`, `eventstore.domain`, `eventstore.command_type`
  - [x] 2.4 Set activity status to `Error` on exception
  - [x] 2.5 Register the ActivitySource in OpenTelemetry tracing configuration in `ServiceCollectionExtensions.cs` or `ServiceDefaults/Extensions.cs`

- [x] Task 3: Register LoggingBehavior as outermost behavior (AC: #1, #5)
  - [x] 3.1 In `ServiceCollectionExtensions.AddCommandApi()`, add `cfg.AddOpenBehavior(typeof(LoggingBehavior<,>))` BEFORE `cfg.AddOpenBehavior(typeof(ValidationBehavior<,>))`
  - [x] 3.2 Add `services.AddHttpContextAccessor()` if not already registered (needed by LoggingBehavior)
  - [x] 3.3 Register the custom ActivitySource with OpenTelemetry `.AddSource("Hexalith.EventStore.CommandApi")`

- [x] Task 4: Simplify controller logging (AC: #2)
  - [x] 4.1 Review `CommandsController.Submit` method - the controller currently logs entry/exit manually
  - [x] 4.2 Since LoggingBehavior now captures the full pipeline, simplify or remove redundant controller-level logging to avoid duplicate log entries
  - [x] 4.3 Keep only minimal controller-level logging (e.g., HTTP-specific context not available in MediatR) if needed

- [x] Task 5: Write unit tests for LoggingBehavior (AC: #1, #2, #3, #5)
  - [x] 5.1 `LoggingBehavior_ValidRequest_LogsEntryAndExit` - verify Information-level logs with expected fields
  - [x] 5.2 `LoggingBehavior_ValidRequest_IncludesDurationMs` - verify duration is logged on exit
  - [x] 5.3 `LoggingBehavior_HandlerThrows_LogsErrorAndRethrows` - verify Error-level log with exception info, then exception propagates
  - [x] 5.4 `LoggingBehavior_NeverLogsPayload` - verify Payload bytes never appear in log output
  - [x] 5.5 `LoggingBehavior_NeverLogsExtensions` - verify Extensions dictionary never appears in log output
  - [x] 5.6 `LoggingBehavior_CreatesOpenTelemetryActivity` - verify activity is started with correct name and tags
  - [x] 5.7 `LoggingBehavior_ExceptionSetsActivityStatusError` - verify activity status set to Error on failure

- [x] Task 6: Write integration tests (AC: #1, #2, #4, #5)
  - [x] 6.1 `PostCommands_ValidRequest_LogsStructuredEntryAndExit` - verify structured log entries via test log sink
  - [x] 6.2 `PostCommands_InvalidRequest_HttpValidationPreventsLoggingBehavior` - verify HTTP-level validation prevents MediatR pipeline execution (ValidateModelFilter catches validation errors before MediatR)
  - [x] 6.3 `PostCommands_ValidRequest_PipelineOrderCorrect` - verify LoggingBehavior entry appears before handler, handler before LoggingBehavior exit

## Dev Notes

### Architecture Compliance

**MediatR Pipeline Order (Architecture Rule #3):** The pipeline MUST be ordered: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler. MediatR executes `AddOpenBehavior` registrations in order, with the first registered behavior being the outermost (wrapping all others). LoggingBehavior MUST be registered first.

**Structured Logging Pattern (Architecture, GAP-5 resolution):**

| Stage | Required Fields | Level |
|-------|----------------|-------|
| Command received (entry) | correlationId, tenantId, domain, aggregateId, commandType | Information |
| Command completed (exit) | correlationId, tenantId, aggregateId, status, durationMs | Information |
| Domain rejection | correlationId, tenantId, aggregateId, rejectionEventType | Warning |
| Infrastructure failure | correlationId, tenantId, aggregateId, exceptionType, message | Error |

For Story 2.3 scope, we implement the "Command received" and "Command completed" patterns at the MediatR pipeline level. The actor-level patterns (Domain rejection, Infrastructure failure) come in Epic 3.

**OpenTelemetry Activity Naming (Architecture):**

| Activity | Name Pattern |
|---------|-------------|
| Command API | `EventStore.CommandApi.Submit` |

**Enforcement Rules to Follow:**
- Rule #3: MediatR pipeline order: logging -> validation -> auth -> handler
- Rule #5: Never log event payload data -- envelope metadata only (SEC-5, NFR12)
- Rule #9: correlationId in every structured log entry and OpenTelemetry activity
- Rule #10: Register services via `Add*` extension methods -- never inline in Program.cs
- Rule #13: No stack traces in production error responses

### Critical Design Decisions

**What Already Exists (from Stories 2.1 and 2.2):**
- `ValidationBehavior<TRequest, TResponse>` in `CommandApi/Pipeline/` -- MediatR pipeline behavior that runs FluentValidation, throws `ValidationException` on failure
- `CorrelationIdMiddleware` in `CommandApi/Middleware/` -- generates/propagates GUID correlation IDs via `HttpContext.Items["CorrelationId"]`
- `ValidationExceptionHandler` + `GlobalExceptionHandler` in `CommandApi/ErrorHandling/` -- convert exceptions to RFC 7807 ProblemDetails
- `SubmitCommandRequestValidator` + `SubmitCommandValidator` -- FluentValidation rules at HTTP and MediatR levels
- OpenTelemetry tracing configured in `ServiceDefaults/Extensions.cs` with ASP.NET Core instrumentation
- Controller (`CommandsController`) currently has its own entry/exit logging with correlation ID

**What Story 2.3 Adds:**
1. **`LoggingBehavior<TRequest, TResponse>`** -- outermost MediatR behavior that logs structured entry/exit with correlation ID, command metadata, and duration
2. **Custom `ActivitySource`** -- for OpenTelemetry tracing of MediatR pipeline operations
3. **Pipeline order enforcement** -- LoggingBehavior registered BEFORE ValidationBehavior in `AddCommandApi()`
4. **Controller logging simplification** -- remove redundant entry/exit logs from controller since LoggingBehavior covers it

**Correlation ID Access in MediatR Behavior:**
The LoggingBehavior needs the correlation ID which is stored in `HttpContext.Items["CorrelationId"]`. Use `IHttpContextAccessor` (injected via DI) to access it. If HttpContext is unavailable (e.g., in non-HTTP scenarios), fall back to generating a new GUID or using "unknown".

**Extracting Command Metadata for Logging:**
The LoggingBehavior is generic (`TRequest`). To extract tenant/domain/aggregateId for structured logging:
- Check if `TRequest` is `SubmitCommand` and extract properties directly
- For other request types, log only the request type name
- NEVER use reflection to discover and log arbitrary properties (could leak payload data)

**OpenTelemetry ActivitySource Pattern:**
```csharp
// Static ActivitySource in a shared location
internal static class EventStoreActivitySources
{
    public static readonly ActivitySource CommandApi = new("Hexalith.EventStore.CommandApi");
}
```
Register in OpenTelemetry configuration: `.AddSource("Hexalith.EventStore.CommandApi")`

### Technical Requirements

**Existing Types to Use:**
- `SubmitCommand` from `Hexalith.EventStore.Server.Pipeline.Commands` -- MediatR command with Tenant, Domain, AggregateId, CommandType, Payload, CorrelationId, Extensions
- `SubmitCommandResult` from same namespace -- Result with CorrelationId
- `ValidationBehavior<TRequest, TResponse>` from `Hexalith.EventStore.CommandApi.Pipeline` -- existing behavior to wrap
- `CorrelationIdMiddleware` from `Hexalith.EventStore.CommandApi.Middleware` -- correlation ID constant `HttpContextKey = "CorrelationId"`
- `IHttpContextAccessor` from `Microsoft.AspNetCore.Http` -- access HttpContext in MediatR behaviors

**NuGet Packages Already Available (in Directory.Packages.props):**
- `MediatR` 14.0.0 -- `IPipelineBehavior<TRequest, TResponse>`, `AddOpenBehavior()`
- `Microsoft.Extensions.Logging.Abstractions` -- `ILogger<T>`, structured logging
- `System.Diagnostics.DiagnosticSource` -- `ActivitySource`, `Activity` (included in .NET 10 runtime)
- `Shouldly` 4.2.1 -- test assertions
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 -- WebApplicationFactory for integration tests

### Library & Framework Requirements

| Library | Version | Purpose |
|---------|---------|---------|
| MediatR | 14.0.0 | IPipelineBehavior, AddOpenBehavior |
| Microsoft.AspNetCore.Http | 10.0.0 | IHttpContextAccessor |
| System.Diagnostics | .NET 10 runtime | ActivitySource for OpenTelemetry |

No new NuGet packages needed. All dependencies are already available.

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.CommandApi/
├── Pipeline/
│   └── LoggingBehavior.cs              # NEW: Outermost MediatR pipeline behavior
├── Telemetry/
│   └── EventStoreActivitySources.cs    # NEW: Static ActivitySource definitions

tests/Hexalith.EventStore.Server.Tests/
└── Pipeline/
    └── LoggingBehaviorTests.cs         # NEW: Unit tests for LoggingBehavior

tests/Hexalith.EventStore.IntegrationTests/
└── CommandApi/
    └── LoggingBehaviorIntegrationTests.cs  # NEW: Integration tests
```

**Existing files to modify:**
```
src/Hexalith.EventStore.CommandApi/
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # MODIFY: Add LoggingBehavior registration + IHttpContextAccessor + ActivitySource
├── Controllers/
│   └── CommandsController.cs           # MODIFY: Simplify/remove redundant entry/exit logging

src/Hexalith.EventStore.ServiceDefaults/
└── Extensions.cs                       # MODIFY: Add .AddSource("Hexalith.EventStore.CommandApi") to tracing config
```

**Existing files to verify (no changes expected):**
```
src/Hexalith.EventStore.CommandApi/
├── Pipeline/
│   └── ValidationBehavior.cs           # VERIFY: Still works correctly when wrapped by LoggingBehavior
├── Middleware/
│   └── CorrelationIdMiddleware.cs      # VERIFY: HttpContextKey constant is accessible
└── Program.cs                          # VERIFY: No changes needed (middleware order unchanged)
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for LoggingBehavior
- `tests/Hexalith.EventStore.IntegrationTests/` -- Integration tests with WebApplicationFactory

**Test Patterns (established in Stories 1.6, 2.1, 2.2):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- `NullLogger<T>.Instance` for unit tests where logging is not under test
- `WebApplicationFactory<CommandApiProgram>` with extern alias for integration tests
- `ConfigureAwait(false)` on all async test methods

**Unit Test Strategy for LoggingBehavior:**
Use `Microsoft.Extensions.Logging.Testing.FakeLogger<T>` (if available in .NET 10) or a custom `TestLogger<T>` that captures log entries to verify:
- Correct log level (Information for entry/exit, Error for exceptions)
- Correct structured fields (correlationId, commandType, tenant, domain, durationMs)
- Absence of payload data in log entries
- Exception logging and rethrow behavior

**OpenTelemetry Test Strategy:**
Use `System.Diagnostics.ActivityListener` to capture activities in unit tests:
```csharp
var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "Hexalith.EventStore.CommandApi",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStarted = activity => capturedActivities.Add(activity),
};
ActivitySource.AddActivityListener(listener);
```

**Minimum Tests (10):**
1. `LoggingBehavior_ValidRequest_LogsEntryAtInformation`
2. `LoggingBehavior_ValidRequest_LogsExitWithDurationMs`
3. `LoggingBehavior_HandlerThrows_LogsErrorAndRethrows`
4. `LoggingBehavior_SubmitCommand_LogsTenantDomainCommandType`
5. `LoggingBehavior_NeverLogsPayloadOrExtensions`
6. `LoggingBehavior_CreatesActivityWithCorrectName`
7. `LoggingBehavior_ActivityIncludesCorrelationIdTag`
8. `LoggingBehavior_ExceptionSetsActivityStatusError`
9. `PostCommands_ValidRequest_LoggingBehaviorExecutesBeforeValidation` (integration)
10. `PostCommands_InvalidRequest_LoggingBehaviorCapturesValidationException` (integration)

### Previous Story Intelligence

**From Story 2.2 (Command Validation & RFC 7807 Error Responses):**
- All 231 tests pass (9 Client + 48 Testing + 147 Contracts + 10 Server + 17 Integration)
- Enhanced 3 existing files (ValidationExceptionHandler, GlobalExceptionHandler, SubmitCommandRequestValidator)
- Created 2 new files (SubmitCommandValidator, ValidationBehaviorTests)
- Key debug learnings: CS1501 (`IndexOfAny` with 5 char args not supported), `application/json` vs `application/problem+json` content type, FluentValidation `.When()` applying to entire rule chain

**Key Patterns Established:**
- `IExceptionHandler` pattern for converting exceptions to ProblemDetails
- Correlation ID from `HttpContext.Items["CorrelationId"]` (string key constant in `CorrelationIdMiddleware.HttpContextKey`)
- `ConfigureAwait(false)` on all async calls (CA2007 compliance)
- `ArgumentNullException.ThrowIfNull()` on all public methods (CA1062 compliance)
- Extern alias for WebApplicationFactory tests to resolve ambiguous `Program` type
- Store tenant in `HttpContext.Items["RequestTenantId"]` for error handlers

**Files Created in Stories 2.1 & 2.2 (relevant to 2.3):**
- `src/Hexalith.EventStore.CommandApi/Pipeline/ValidationBehavior.cs` -- wraps FluentValidation, throws `ValidationException`
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationExceptionHandler.cs` -- 400 ProblemDetails
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/GlobalExceptionHandler.cs` -- 500 ProblemDetails
- `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` -- `AddCommandApi()` with MediatR registration
- `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` -- correlation ID propagation
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` -- entry point with manual logging
- `src/Hexalith.EventStore.Server/Pipeline/Commands/SubmitCommand.cs` -- MediatR command record
- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` -- stub handler (returns correlationId)

### Git Intelligence

**Recent Commits (Last 5):**
- `489e959` Merge pull request #22 from Hexalith/feature/story-2.2-command-validation-rfc7807
- `d3f19fd` Story 2.2: Command Validation & RFC 7807 Error Responses
- `abd5f73` Update Claude Code local settings with tool permissions
- `2dce6f8` Merge pull request #20 from Hexalith/feature/story-2.1-commandapi-and-2.2-story
- `85fd090` Story 2.1: CommandApi Host & Minimal Endpoint Scaffolding + Story 2.2 context

**Patterns:**
- Feature branches named `feature/story-X.Y-description`
- PR-based workflow with merge commits
- Commit messages follow "Story X.Y: Title" format
- Stories build incrementally on main branch

**Key Code Conventions from Recent Commits:**
- Primary constructors for DI injection: `public class Foo(IDep dep) : Base`
- Records for immutable data: `public record SubmitCommand(...)`
- Minimal controller pattern: inject IMediator, delegate to MediatR
- Feature folder organization: Pipeline/, ErrorHandling/, Validation/, Middleware/, Models/

### Project Structure Notes

**Alignment with Architecture:**
- `LoggingBehavior` goes in `CommandApi/Pipeline/` per architecture's feature folder convention
- `EventStoreActivitySources` can go in `CommandApi/Telemetry/` -- new folder for telemetry concerns. Alternatively, this could live in `Server/` if it will be shared across CommandApi and Server projects in later stories. For now, keep it in CommandApi since that's the only consumer.
- Registration in `AddCommandApi()` per enforcement rule #10

**Current Test Count:** 231 tests (9 Client + 48 Testing + 147 Contracts + 10 Server + 17 Integration). Story 2.3 should add approximately 10 new tests bringing total to ~241.

**Dependency Graph Relevant to This Story:**
```
CommandApi -> Server -> Contracts
CommandApi -> ServiceDefaults
Tests: IntegrationTests -> CommandApi (via WebApplicationFactory)
Tests: Server.Tests -> Server + CommandApi
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.3: MediatR Pipeline & Logging Behavior]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns - MediatR Pipeline Behaviors]
- [Source: _bmad-output/planning-artifacts/architecture.md#Process Patterns - Structured Logging Pattern]
- [Source: _bmad-output/planning-artifacts/architecture.md#Process Patterns - OpenTelemetry Activity Naming]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rules #3, #5, #9, #10, #13]
- [Source: _bmad-output/planning-artifacts/architecture.md#Security-Critical Architectural Constraints - SEC-5]
- [Source: _bmad-output/implementation-artifacts/2-2-command-validation-and-rfc-7807-error-responses.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- MediatR 14.0.0 `RequestHandlerDelegate<TResponse>` takes a `CancellationToken` parameter (discovered via .NET 10 reflection). Tests use `new RequestHandlerDelegate<T>((_) => ...)` pattern.
- xUnit1030: `ConfigureAwait(false)` must NOT be used in test methods; only in production code (CA2007).
- `ConcurrentBag` does not preserve insertion order; switched to `ConcurrentQueue` for order-dependent integration tests.
- `ValidateModelFilter` runs FluentValidation at HTTP level BEFORE MediatR pipeline, so invalid requests never reach LoggingBehavior. Integration test 6.2 adjusted accordingly.
- `IWebHostBuilder.ConfigureLogging` not available; used `ConfigureServices(services => services.AddLogging(...))` instead.

### Completion Notes List

- Implemented `LoggingBehavior<TRequest, TResponse>` as outermost MediatR pipeline behavior with structured logging (correlation ID, command metadata, duration) and OpenTelemetry activity tracing.
- Created `EventStoreActivitySources.CommandApi` static ActivitySource with name `"Hexalith.EventStore.CommandApi"`.
- Registered LoggingBehavior BEFORE ValidationBehavior in `AddCommandApi()` to enforce pipeline order.
- Added `.AddSource("Hexalith.EventStore.CommandApi")` to ServiceDefaults OpenTelemetry tracing configuration.
- Removed redundant entry/exit logging from `CommandsController` (now handled by LoggingBehavior).
- 8 unit tests covering: entry/exit logs, duration, error handling, payload exclusion, extensions exclusion, OpenTelemetry activity creation, activity error status, fallback correlation ID.
- 3 integration tests covering: structured log capture, HTTP validation preventing pipeline execution, pipeline order verification.
- Total test count: 242 (up from 231), all passing with zero regressions.

### File List

**New files:**
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs`
- `src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs`
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/LoggingBehaviorTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/LoggingBehaviorIntegrationTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` (added HttpContextAccessor, LoggingBehavior registration)
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` (removed redundant logging, removed ILogger dependency)
- `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` (added ActivitySource to OpenTelemetry tracing)

### Change Log

- 2026-02-13: Story 2.3 implementation complete. Added MediatR LoggingBehavior with structured logging and OpenTelemetry tracing. 11 new tests (8 unit + 3 integration). Total: 242 tests passing.
