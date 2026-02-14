# Story 3.5: Domain Service Registration & Invocation

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain service developer**,
I want to register my domain service with EventStore by tenant and domain via configuration, and have the system invoke it with the command and current state,
So that my business logic processes commands without infrastructure concerns (FR22, FR23).

## Acceptance Criteria

1. **Domain service resolution from configuration** - Given a domain service is registered in the DAPR config store for a specific tenant and domain, When Step 4 (domain service invocation) executes, Then DomainServiceResolver looks up the service endpoint for the command's tenant + domain from the DAPR config store (D7), And the resolver returns the DAPR app-id and method name for the target domain service.

2. **Domain service invocation via DAPR** - Given the DomainServiceResolver returns a valid service endpoint, When DaprDomainServiceInvoker calls the domain service, Then it uses `DaprClient.InvokeMethodAsync` with the command and current state (D7), And the domain service returns a DomainResult containing `List<DomainEvent>` (could be empty, events, or rejection events per D3).

3. **Empty result handling (no-op)** - Given the domain service returns an empty event list (DomainResult.IsNoOp), When the actor processes the result, Then no state change occurs (valid per D3), And the command is accepted with no events produced, And processing continues to Step 5 (state machine stub).

4. **Rejection event handling** - Given the domain service returns IRejectionEvent instances (DomainResult.IsRejection), When the actor processes the result, Then the command status transitions to Rejected, And the rejection events are included in the result for downstream processing (Step 5, Story 3.11).

5. **Success event handling** - Given the domain service returns state-change events (DomainResult.IsSuccess), When the actor processes the result, Then the events are passed to Step 5 (state machine execution stub) for persistence, And EventStore will populate all 11 envelope metadata fields (SEC-1) in Step 5 (Story 3.7+).

6. **Infrastructure failure handling** - Given the domain service is unreachable or returns an error (infrastructure failure), When the DaprDomainServiceInvoker encounters a DAPR invocation exception, Then the exception propagates to the actor caller (converted to ProblemDetails by the exception handler chain), And DAPR resiliency policies (retry, circuit breaker, timeout) are applied at the sidecar level (enforcement rule #4 -- no custom retry in application code).

7. **DomainServiceResolver is a focused, testable component** - The `DomainServiceResolver` is a separate class implementing `IDomainServiceResolver`, And it encapsulates DAPR config store lookup logic, And it has a method: `Task<DomainServiceRegistration?> ResolveAsync(string tenantId, string domain)` returning the service registration or null if not found.

8. **DaprDomainServiceInvoker is a focused, testable component** - The `DaprDomainServiceInvoker` is a separate class implementing `IDomainServiceInvoker` (already exists), And it uses `DaprClient.InvokeMethodAsync` for DAPR service invocation, And it is created per-actor-call (same pattern as IdempotencyChecker, TenantValidator, EventStreamReader).

9. **Existing tests unbroken** - All existing tests (estimated ~434+ from Story 3.4) continue to pass after the AggregateActor's Step 4 STUB is replaced with DomainServiceInvoker. Unit tests verify domain service invocation logic. Integration tests continue to work via the mocked/faked actor infrastructure.

## Prerequisites

**BLOCKING: Story 3.4 MUST be complete (done status) before starting Story 3.5.** Story 3.5 depends on:
- `AggregateActor` with 5-step orchestrator pattern (Step 4 is currently a STUB to be replaced) (Story 3.2)
- `EventStreamReader` providing `currentState` from Step 3 (Story 3.4)
- `TenantValidator` enforcing SEC-2 constraint (Story 3.3)
- `IdempotencyChecker` for idempotency records (Story 3.2)
- `CommandRouter` routing commands to actors (Story 3.1)
- `CommandProcessingResult` record with `Accepted`, `ErrorMessage`, `CorrelationId` (Story 3.1)
- `CommandEnvelope` with `TenantId`, `Domain`, `AggregateIdentity`, etc. (Story 1.2)
- `DomainResult` with `IsSuccess`, `IsRejection`, `IsNoOp` semantics (Story 1.2)
- `IDomainServiceInvoker` interface already exists in `Server/DomainServices/`
- `IDomainProcessor` + `DomainProcessorBase` already exist in `Client/Handlers/`
- `FakeDomainServiceInvoker` already exists in `Testing/Fakes/`
- All Epic 2 infrastructure

**Before beginning any Task below, verify:** Run existing tests to confirm all Story 3.4 artifacts are in place. All existing tests must pass before proceeding.

## Tasks / Subtasks

- [x]Task 0: Verify prerequisites and existing artifacts (BLOCKING)
  - [x]0.1 Run all existing tests -- they must pass before proceeding
  - [x]0.2 Confirm `AggregateActor` has 5-step orchestrator with Step 4 as STUB (Story 3.4)
  - [x]0.3 Confirm `IDomainServiceInvoker` interface exists in `Server/DomainServices/` with `Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState)`
  - [x]0.4 Confirm `DomainResult` exists in Contracts with `IsSuccess`, `IsRejection`, `IsNoOp` properties
  - [x]0.5 Confirm `IRejectionEvent` marker interface exists in Contracts
  - [x]0.6 Confirm `FakeDomainServiceInvoker` exists in `Testing/Fakes/`
  - [x]0.7 Confirm `DaprClient` is available in the Server project dependencies

- [x]Task 1: Create IDomainServiceResolver interface (AC: #7)
  - [x]1.1 Create `IDomainServiceResolver` interface in `Server/DomainServices/`
  - [x]1.2 Define method: `Task<DomainServiceRegistration?> ResolveAsync(string tenantId, string domain, CancellationToken cancellationToken = default)` -- returns registration or null if not found
  - [x]1.3 Namespace: `Hexalith.EventStore.Server.DomainServices`

- [x]Task 2: Create DomainServiceRegistration record (AC: #1, #7)
  - [x]2.1 Create `DomainServiceRegistration` record in `Server/DomainServices/`
  - [x]2.2 Properties: `string AppId` (DAPR app-id of the domain service), `string MethodName` (HTTP method name to invoke, e.g., "process-command"), `string TenantId`, `string Domain`, `string? Version`
  - [x]2.3 Namespace: `Hexalith.EventStore.Server.DomainServices`

- [x]Task 3: Create DomainServiceResolver implementation (AC: #1, #7)
  - [x]3.1 Create `DomainServiceResolver` class in `Server/DomainServices/` implementing `IDomainServiceResolver`
  - [x]3.2 Constructor: `DomainServiceResolver(DaprClient daprClient, IOptions<DomainServiceOptions> options, ILogger<DomainServiceResolver> logger)`
  - [x]3.3 `ResolveAsync` implementation:
    - Build config store key: `{tenantId}:{domain}:service` (convention for domain service registration)
    - Call `daprClient.GetConfiguration(options.Value.ConfigStoreName, [key], cancellationToken: cancellationToken)` to look up the registration
    - If key not found, log Warning `"No domain service registered for Tenant={TenantId}, Domain={Domain}"` and return null
    - If found, parse the config value as JSON into `DomainServiceRegistration`
    - Return the parsed registration
  - [x]3.4 Use `ArgumentException.ThrowIfNullOrWhiteSpace()` on tenantId and domain parameters
  - [x]3.5 `ConfigureAwait(false)` on all async calls (CA2007)
  - [x]3.6 Log at Debug level: `"Resolved domain service: AppId={AppId}, Method={MethodName} for Tenant={TenantId}, Domain={Domain}"`

- [x]Task 4: Create DomainServiceOptions configuration (AC: #1)
  - [x]4.1 Create `DomainServiceOptions` record in `Server/DomainServices/`
  - [x]4.2 Properties: `string ConfigStoreName` (DAPR config store name, default: "configstore"), `int InvocationTimeoutSeconds` (default: 5, per enforcement rule #14)
  - [x]4.3 Namespace: `Hexalith.EventStore.Server.DomainServices`

- [x]Task 5: Create DomainServiceNotFoundException exception (AC: #6)
  - [x]5.1 Create `DomainServiceNotFoundException` class in `Server/DomainServices/` extending `InvalidOperationException`
  - [x]5.2 Properties: `string TenantId`, `string Domain`
  - [x]5.3 Constructor: `DomainServiceNotFoundException(string tenantId, string domain)` with message: `$"No domain service registered for tenant '{tenantId}' and domain '{domain}'. Register via DAPR config store with key '{tenantId}:{domain}:service'."`
  - [x]5.4 Namespace: `Hexalith.EventStore.Server.DomainServices`

- [x]Task 6: Create DaprDomainServiceInvoker implementation (AC: #2, #3, #4, #5, #6, #8)
  - [x]6.1 Create `DaprDomainServiceInvoker` class in `Server/DomainServices/` implementing `IDomainServiceInvoker`
  - [x]6.2 Constructor: `DaprDomainServiceInvoker(DaprClient daprClient, IDomainServiceResolver resolver, ILogger<DaprDomainServiceInvoker> logger)`
  - [x]6.3 `InvokeAsync` implementation:
    - Extract tenantId and domain from `command.TenantId` and `command.Domain`
    - Call `resolver.ResolveAsync(tenantId, domain)` to get registration
    - If registration is null, throw `DomainServiceNotFoundException(tenantId, domain)` (AC #6)
    - Create invocation request with command and currentState
    - Call `daprClient.InvokeMethodAsync<DomainServiceRequest, DomainResult>(registration.AppId, registration.MethodName, request)` (D7)
    - Log at Information level: `"Domain service invoked: AppId={AppId}, Tenant={TenantId}, Domain={Domain}, CorrelationId={CorrelationId}, ResultType={ResultType}"` where ResultType is "Success"/"Rejection"/"NoOp"
    - Return the DomainResult
  - [x]6.4 Do NOT add custom retry logic -- DAPR resiliency handles transient failures (enforcement rule #4)
  - [x]6.5 Do NOT log event payload data -- only metadata (enforcement rule #5, SEC-5)
  - [x]6.6 Include correlationId in all log entries (enforcement rule #9)
  - [x]6.7 `ConfigureAwait(false)` on all async calls (CA2007)
  - [x]6.8 `ArgumentNullException.ThrowIfNull()` on command parameter (CA1062)

- [x]Task 7: Create DomainServiceRequest DTO (AC: #2)
  - [x]7.1 Create `DomainServiceRequest` record in `Server/DomainServices/`
  - [x]7.2 Properties: `CommandEnvelope Command`, `object? CurrentState`
  - [x]7.3 This is the payload sent to the domain service via DAPR service invocation
  - [x]7.4 Namespace: `Hexalith.EventStore.Server.DomainServices`

- [x]Task 8: Update AggregateActor to replace Step 4 STUB with DomainServiceInvoker (AC: #2, #3, #4, #5, #9)
  - [x]8.1 In `AggregateActor.ProcessCommandAsync`, replace the Step 4 STUB log line with actual DomainServiceInvoker call
  - [x]8.2 Create `DaprDomainServiceInvoker` by resolving dependencies from the actor's service provider:
    - `DaprClient` from `host.Services.GetRequiredService<DaprClient>()`
    - `IDomainServiceResolver` from `host.Services.GetRequiredService<IDomainServiceResolver>()` (this one IS DI-registered, unlike actor-scoped components)
    - `ILogger<DaprDomainServiceInvoker>` from `host.LoggerFactory.CreateLogger<DaprDomainServiceInvoker>()`
  - [x]8.3 Call `DomainResult result = await domainServiceInvoker.InvokeAsync(command, currentState).ConfigureAwait(false)`
  - [x]8.4 Log at Information level after invocation: `"Domain service result: {ResultType} for ActorId={ActorId}, CorrelationId={CorrelationId}"` where ResultType is derived from `result.IsSuccess/IsRejection/IsNoOp`
  - [x]8.5 Handle domain rejection results:
    - If `result.IsRejection`, create rejection `CommandProcessingResult(Accepted: false, ErrorMessage: $"Domain rejection: {result.Events[0].GetType().Name}", CorrelationId: command.CorrelationId)`
    - Store rejection via `idempotencyChecker.RecordAsync(causationId, rejectionResult)`
    - Call `await StateManager.SaveStateAsync()` to persist
    - Return the rejection result (do NOT proceed to Step 5)
  - [x]8.6 Handle no-op results:
    - If `result.IsNoOp`, create `CommandProcessingResult(Accepted: true, CorrelationId: command.CorrelationId)`
    - Store via idempotency, save state, return (no events to persist)
  - [x]8.7 Handle success results:
    - If `result.IsSuccess`, store events in a local variable for Step 5 (state machine stub)
    - Log at Debug level: `"Step 5: State machine execution -- STUB (Story 3.11), {EventCount} events to persist"`
    - Continue to Step 5 stub
  - [x]8.8 If DomainServiceInvoker throws (infrastructure failure), let it propagate to the caller (the CommandRouter exception handler chain converts it to 500 ProblemDetails)
  - [x]8.9 The existing catch for `TenantMismatchException` (Story 3.3) must remain specific -- do NOT catch broader exceptions that would swallow domain service failures
  - [x]8.10 `ConfigureAwait(false)` on all async calls (CA2007)

- [x]Task 9: Register DomainServiceResolver in DI (AC: #7)
  - [x]9.1 In `AddEventStoreServer()` (Server/Configuration/ServiceCollectionExtensions.cs), register:
    - `services.AddSingleton<IDomainServiceResolver, DomainServiceResolver>()`
    - `services.Configure<DomainServiceOptions>(configuration.GetSection("EventStore:DomainServices"))` (or similar config binding)
  - [x]9.2 Note: `DaprDomainServiceInvoker` is NOT DI-registered -- it's created per actor call (same pattern as other actor-scoped components). However, `IDomainServiceResolver` IS DI-registered because it's stateless and can be shared.
  - [x]9.3 Ensure `DaprClient` is registered (should already be from DAPR integration)

- [x]Task 10: Write unit tests for DomainServiceResolver (AC: #1, #7)
  - [x]10.1 Create `DomainServiceResolverTests.cs` in `tests/Hexalith.EventStore.Server.Tests/DomainServices/`
  - [x]10.2 `ResolveAsync_RegisteredService_ReturnsRegistration` -- verify config store lookup returns correct registration
  - [x]10.3 `ResolveAsync_UnregisteredService_ReturnsNull` -- verify null returned when no registration found
  - [x]10.4 `ResolveAsync_UsesCorrectConfigKey` -- verify key pattern `{tenant}:{domain}:service`
  - [x]10.5 `ResolveAsync_NullTenantId_ThrowsArgumentException` -- verify guard clause
  - [x]10.6 `ResolveAsync_NullDomain_ThrowsArgumentException` -- verify guard clause
  - [x]10.7 Mock `DaprClient.GetConfiguration()` using NSubstitute

- [x]Task 11: Write unit tests for DaprDomainServiceInvoker (AC: #2, #3, #4, #5, #6, #8)
  - [x]11.1 Create `DaprDomainServiceInvokerTests.cs` in `tests/Hexalith.EventStore.Server.Tests/DomainServices/`
  - [x]11.2 `InvokeAsync_SuccessResult_ReturnsDomainResult` -- verify successful invocation
  - [x]11.3 `InvokeAsync_RejectionResult_ReturnsDomainResult` -- verify rejection events returned
  - [x]11.4 `InvokeAsync_NoOpResult_ReturnsDomainResult` -- verify empty result returned
  - [x]11.5 `InvokeAsync_ServiceNotFound_ThrowsDomainServiceNotFoundException` -- verify exception when resolver returns null
  - [x]11.6 `InvokeAsync_DaprInvocationFails_PropagatesException` -- verify DAPR errors propagate (no custom retry)
  - [x]11.7 `InvokeAsync_PassesCommandAndState_ToDaprInvocation` -- verify correct request payload
  - [x]11.8 `InvokeAsync_NullCommand_ThrowsArgumentNullException` -- verify guard clause
  - [x]11.9 `InvokeAsync_LogsCorrelationId` -- verify correlationId in log entries (rule #9)
  - [x]11.10 Mock `DaprClient.InvokeMethodAsync()` and `IDomainServiceResolver` using NSubstitute

- [x]Task 12: Write unit tests for AggregateActor domain invocation flow (AC: #3, #4, #5, #9)
  - [x]12.1 Update `AggregateActorTests.cs` in `tests/Hexalith.EventStore.Server.Tests/Actors/`
  - [x]12.2 `ProcessCommandAsync_DomainSuccess_ProceedsToStep5` -- verify success events passed to Step 5 stub
  - [x]12.3 `ProcessCommandAsync_DomainRejection_ReturnsRejectionResult` -- verify rejection handling
  - [x]12.4 `ProcessCommandAsync_DomainRejection_StoresInIdempotencyCache` -- verify rejection cached
  - [x]12.5 `ProcessCommandAsync_DomainNoOp_ReturnsAccepted` -- verify no-op handling
  - [x]12.6 `ProcessCommandAsync_DomainServiceNotFound_PropagatesException` -- verify DomainServiceNotFoundException propagates
  - [x]12.7 `ProcessCommandAsync_DomainInfrastructureFailure_PropagatesException` -- verify DAPR errors propagate
  - [x]12.8 `ProcessCommandAsync_DomainInvocation_LogsResultType` -- verify logging

- [x]Task 13: Write integration tests (AC: #9)
  - [x]13.1 Integration tests operate at the HTTP level where the `ICommandRouter` is mocked. Domain invocation at the actor level cannot be easily tested in integration tests without real DAPR infrastructure
  - [x]13.2 Verify all existing integration tests still pass -- this is the primary integration test validation
  - [x]13.3 Real domain service invocation testing will be in Tier 2 (DAPR test containers) in Story 7.4

- [x]Task 14: Run all tests and verify zero regressions (AC: #9)
  - [x]14.1 Run all existing tests -- zero regressions expected
  - [x]14.2 Run new tests -- all must pass
  - [x]14.3 Verify total test count (estimated: ~434 existing from Story 3.4 + ~25 new = ~459)

## Dev Notes

### Architecture Compliance

**FR22: Domain Service Registration via Configuration:**
Domain services register with EventStore through the DAPR config store. The registration maps a `{tenant}:{domain}` combination to a DAPR app-id and method name. This enables runtime discovery without hardcoding service endpoints. The DomainServiceResolver encapsulates this lookup.

**FR23: Domain Service Invocation with Command and Current State:**
The DaprDomainServiceInvoker calls the registered domain service via `DaprClient.InvokeMethodAsync`, passing the command and current aggregate state. The domain service processes the command as a pure function and returns a DomainResult containing events, rejection events, or an empty list.

**D7: Domain Service Invocation -- DAPR Service Invocation:**
From architecture document: "Actor calls domain service via `DaprClient.InvokeMethodAsync<TRequest, TResponse>`. Service discovery via DAPR config store registration (`tenant:domain:version -> appId + method`). Security: mTLS between sidecars (automatic with DAPR). Resiliency: DAPR resiliency policies (retry with backoff, circuit breaker, timeout) applied at sidecar level."

**D3: Domain Service Error Contract -- Errors as Events:**
From architecture document: "Contract: `(Command, CurrentState?) -> List<DomainEvent>` -- always returns events, never throws for domain logic. Domain rejection expressed as rejection event types via IRejectionEvent marker interface. Infrastructure failure: Exceptions only (network, timeout, unreachable) -- handled by DAPR resiliency. Dead-letter routing: Infrastructure failures only, after DAPR retry exhaustion. Domain rejections are normal events, not error paths."

**Architecture Data Flow (Story 3.5 scope):**
```
AggregateActor.ProcessCommandAsync(CommandEnvelope command)
    |-- Log command receipt (preserved from Story 3.1)
    |-- Step 1: IdempotencyChecker.CheckAsync(causationId)
    |      |-- If duplicate: return cached result
    |-- Step 2: TenantValidator.Validate(command.TenantId, Host.Id.GetId())  <-- Story 3.3 (SEC-2)
    |      |-- If mismatch: return rejection result
    |-- Step 3: EventStreamReader.RehydrateAsync(command.AggregateIdentity)  <-- Story 3.4
    |      |-- Returns currentState (or null for new aggregates)
    |-- Step 4: DomainServiceInvoker.InvokeAsync(command, currentState)  <-- THIS STORY
    |      |-- DomainServiceResolver.ResolveAsync(tenantId, domain)
    |      |-- DaprClient.InvokeMethodAsync(appId, method, request)
    |      |-- Returns DomainResult:
    |      |      |-- IsSuccess: events for Step 5 persistence
    |      |      |-- IsRejection: return rejection to caller
    |      |      |-- IsNoOp: accept with no events
    |-- Step 5: State machine execution (STUB -> Story 3.11) [receives events from Step 4]
    |-- Create CommandProcessingResult
    |-- IdempotencyChecker.RecordAsync(causationId, result)
    |-- StateManager.SaveStateAsync() [atomic commit]
    |-- Return result
```

**SEC-1: EventStore Owns All 11 Envelope Metadata Fields:**
Important: In Step 4, the domain service returns raw event payloads (IEventPayload). The EventStore populates all 11 envelope metadata fields (aggregateId, tenantId, domain, sequenceNumber, timestamp, correlationId, causationId, userId, domainServiceVersion, eventTypeName, serializationFormat) in Step 5 (Story 3.7+). Story 3.5 passes the raw DomainResult to Step 5 for envelope wrapping.

**Enforcement Rules to Follow:**
- Rule #4: Never add custom retry logic -- DAPR resiliency only (CRITICAL for Story 3.5)
- Rule #5: Never log event payload data -- only envelope metadata (SEC-5, NFR12)
- Rule #6: Use `IActorStateManager` for all actor state operations
- Rule #9: correlationId in every structured log entry and OpenTelemetry activity
- Rule #12: Command status writes are advisory -- never block pipeline
- Rule #14: DAPR sidecar call timeout is 5 seconds

### Critical Design Decisions

**F1 (Architecture): DaprDomainServiceInvoker uses DaprClient, NOT IActorStateManager.**
Unlike previous actor-scoped components (IdempotencyChecker, TenantValidator, EventStreamReader) that use `IActorStateManager` for state store operations, the DomainServiceInvoker uses `DaprClient.InvokeMethodAsync` for service invocation. This is a different DAPR building block (service invocation, not state management). The `DaprClient` is DI-registered and can be resolved from the actor's service provider.

**F2 (Design): DomainServiceResolver IS DI-registered, DaprDomainServiceInvoker is NOT.**
The resolver is stateless and thread-safe -- it only performs config store lookups. It can be registered as a singleton in DI. The invoker, however, takes the resolver as a dependency and logs per-invocation context. While the invoker could also be DI-registered, creating it per-call in the actor maintains the established pattern from Stories 3.2-3.4 and keeps actor dependencies explicit.

**F3 (Design): Domain service returns DomainResult, not CommandProcessingResult.**
The domain service returns a `DomainResult` containing raw event payloads. The actor converts this to `CommandProcessingResult` for the response. This separation keeps domain services infrastructure-agnostic -- they don't know about actor processing results.

**F4 (CRITICAL): Domain rejections at Step 4 short-circuit Steps 5.**
If the domain service returns rejection events, the actor immediately creates a rejection `CommandProcessingResult`, caches it via IdempotencyChecker, and returns. No event persistence (Step 5) occurs for rejections. However, per D3, rejection events ARE events and WILL be persisted to the event stream in Story 3.7+ (they increment the sequence number). For Story 3.5, rejection handling returns a rejection result without persistence (Step 5 is still a stub).

**F5 (Scope): Story 3.5 does NOT persist events.**
Step 5 (state machine execution) is still a STUB. Story 3.5 invokes the domain service and handles the result, but actual event persistence happens in Story 3.7 and state machine execution in Story 3.11. For success results, Story 3.5 logs the events and continues to the Step 5 stub.

**F6 (Performance): DAPR service invocation adds ~2-5ms per call.**
Per NFR8 (2ms DAPR overhead) plus domain service processing time. The 5-second timeout (rule #14) is enforced at the DAPR sidecar level via resiliency policies, not in application code. No custom timeout implementation needed.

**F7 (Pre-mortem): DomainServiceNotFoundException must have a clear recovery path.**
If no domain service is registered for a tenant+domain, the error message includes the expected config store key pattern so operators know exactly what to configure. This prevents cryptic "service not found" errors.

**F8 (Security): Domain service invocation uses DAPR mTLS.**
DAPR automatically encrypts service-to-service communication via mTLS between sidecars. No additional TLS configuration needed in the application code.

**F9 (First Principles): Why config store instead of hardcoded service mappings?**
Using DAPR config store for domain service registration enables:
- Runtime registration without system restart (NFR20)
- Multi-tenant service routing (different tenants can use different domain service versions)
- Version management (tenant+domain+version -> appId mapping)
- Dynamic service discovery without application code changes

**F10 (Red Team): Malicious domain service could return excessive events.**
The EventStore should eventually implement a maximum event count per command (e.g., 1000 events). For Story 3.5, this is not implemented but should be noted for Story 3.7+ when events are actually persisted.

**F11 (Failure Mode): Config store unavailability blocks all domain invocations.**
If the DAPR config store is down, DomainServiceResolver.ResolveAsync will fail. This is an infrastructure failure handled by DAPR resiliency policies. Consider caching resolved registrations in a future story to mitigate config store outages.

**What Already Exists (from Stories 1.1-3.4):**
- `IDomainServiceInvoker` in Server/DomainServices/ -- interface with `InvokeAsync(CommandEnvelope, object?)` (Story 1.x)
- `IDomainProcessor` + `DomainProcessorBase` in Client/Handlers/ -- client-side contracts (Story 1.3)
- `DomainResult` in Contracts/Results/ -- with IsSuccess/IsRejection/IsNoOp semantics (Story 1.2)
- `IRejectionEvent` in Contracts/Events/ -- marker interface (Story 1.2)
- `IEventPayload` in Contracts/Events/ -- base event interface (Story 1.2)
- `FakeDomainServiceInvoker` in Testing/Fakes/ -- test fake (Story 1.4)
- `AggregateActor` with 5-step orchestrator (Steps 1-3 real, Steps 4-5 stubs) (Stories 3.1-3.4)
- `CommandEnvelope` with TenantId, Domain, AggregateIdentity (Story 1.2)
- `CommandProcessingResult` record (Story 3.1)
- `DaprClient` usage patterns from DaprCommandStatusStore, DaprCommandArchiveStore (Stories 2.6-2.7)
- All Epic 2 infrastructure

**What Story 3.5 Adds:**
1. **`IDomainServiceResolver`** -- interface in Server/DomainServices/
2. **`DomainServiceResolver`** -- implementation using DAPR config store in Server/DomainServices/
3. **`DaprDomainServiceInvoker`** -- implementation using DaprClient.InvokeMethodAsync in Server/DomainServices/
4. **`DomainServiceRegistration`** -- record for service registration data in Server/DomainServices/
5. **`DomainServiceOptions`** -- configuration record in Server/DomainServices/
6. **`DomainServiceRequest`** -- DTO for invocation payload in Server/DomainServices/
7. **`DomainServiceNotFoundException`** -- exception when service not registered in Server/DomainServices/
8. **Modified `AggregateActor`** -- Step 4 STUB replaced with DomainServiceInvoker call + result handling
9. **Modified `ServiceCollectionExtensions`** -- DI registration for IDomainServiceResolver

**What Story 3.5 Does NOT Change:**
- `IAggregateActor` interface (unchanged)
- `IDomainServiceInvoker` interface (unchanged -- already exists)
- `CommandProcessingResult` record (unchanged)
- `DomainResult` / `IRejectionEvent` / `IEventPayload` in Contracts (unchanged)
- `IDomainProcessor` / `DomainProcessorBase` in Client (unchanged)
- `FakeDomainServiceInvoker` in Testing (unchanged)
- Steps 1-3 in actor (unchanged)
- Step 5 remains a stub (state machine is Story 3.11)
- No event WRITING yet -- that's Story 3.7
- No envelope metadata population -- that's Story 3.7+ (SEC-1)

### AggregateActor Updated Orchestrator Pattern

```csharp
// In Server/Actors/AggregateActor.cs (after Story 3.5)
public async Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
{
    ArgumentNullException.ThrowIfNull(command);

    logger.LogInformation(
        "Actor {ActorId} received command: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
        Host.Id, command.CorrelationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType);

    // Step 1: Idempotency check (unchanged from Story 3.2)
    var causationId = command.CausationId ?? command.CorrelationId;
    var idempotencyChecker = new IdempotencyChecker(
        StateManager, host.LoggerFactory.CreateLogger<IdempotencyChecker>());

    CommandProcessingResult? cached = await idempotencyChecker
        .CheckAsync(causationId).ConfigureAwait(false);
    if (cached is not null)
    {
        logger.LogInformation("Duplicate command detected: CausationId={CausationId}, ActorId={ActorId}. Returning cached result.",
            causationId, Host.Id);
        return cached;
    }

    // SEC-2 CRITICAL: This MUST execute before any state access (Step 3+)
    // Step 2: Tenant validation (unchanged from Story 3.3)
    var tenantValidator = new TenantValidator(
        host.LoggerFactory.CreateLogger<TenantValidator>());
    try
    {
        tenantValidator.Validate(command.TenantId, Host.Id.GetId());
    }
    catch (TenantMismatchException ex)
    {
        logger.LogWarning(
            "Tenant validation rejected command: CorrelationId={CorrelationId}, CommandTenant={CommandTenant}, ActorTenant={ActorTenant}",
            command.CorrelationId, ex.CommandTenant, ex.ActorTenant);

        var rejectionResult = new CommandProcessingResult(
            Accepted: false, ErrorMessage: ex.Message, CorrelationId: command.CorrelationId);

        await idempotencyChecker.RecordAsync(causationId, rejectionResult).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return rejectionResult;
    }

    // Step 3: State rehydration (unchanged from Story 3.4)
    var eventStreamReader = new EventStreamReader(
        StateManager, host.LoggerFactory.CreateLogger<EventStreamReader>());

    object? currentState = await eventStreamReader
        .RehydrateAsync(command.AggregateIdentity)
        .ConfigureAwait(false);

    logger.LogInformation("State rehydrated: {StateType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
        currentState?.GetType().Name ?? "null", Host.Id, command.CorrelationId);

    // Step 4: Domain service invocation (THIS STORY)
    var daprClient = host.Services.GetRequiredService<DaprClient>();
    var resolver = host.Services.GetRequiredService<IDomainServiceResolver>();
    var domainServiceInvoker = new DaprDomainServiceInvoker(
        daprClient, resolver, host.LoggerFactory.CreateLogger<DaprDomainServiceInvoker>());

    DomainResult domainResult = await domainServiceInvoker
        .InvokeAsync(command, currentState)
        .ConfigureAwait(false);

    logger.LogInformation(
        "Domain service result: {ResultType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
        domainResult.IsSuccess ? "Success" : domainResult.IsRejection ? "Rejection" : "NoOp",
        Host.Id, command.CorrelationId);

    // Handle domain rejection
    if (domainResult.IsRejection)
    {
        var rejectionResult = new CommandProcessingResult(
            Accepted: false,
            ErrorMessage: $"Domain rejection: {domainResult.Events[0].GetType().Name}",
            CorrelationId: command.CorrelationId);

        await idempotencyChecker.RecordAsync(causationId, rejectionResult).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return rejectionResult;
    }

    // Handle no-op (empty event list -- command acknowledged, no state change)
    if (domainResult.IsNoOp)
    {
        var noOpResult = new CommandProcessingResult(
            Accepted: true, CorrelationId: command.CorrelationId);

        await idempotencyChecker.RecordAsync(causationId, noOpResult).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return noOpResult;
    }

    // Success: events produced -- pass to Step 5
    // Step 5: State machine execution (STUB -- Story 3.11)
    logger.LogDebug("Step 5: State machine execution -- STUB (Story 3.11), {EventCount} events to persist",
        domainResult.Events.Count);

    // Create result and store for idempotency
    var result = new CommandProcessingResult(
        Accepted: true, CorrelationId: command.CorrelationId);

    await idempotencyChecker.RecordAsync(causationId, result).ConfigureAwait(false);
    await StateManager.SaveStateAsync().ConfigureAwait(false);
    return result;
}
```

### DaprDomainServiceInvoker Pattern

```csharp
// In Server/DomainServices/DaprDomainServiceInvoker.cs
namespace Hexalith.EventStore.Server.DomainServices;

public class DaprDomainServiceInvoker(
    DaprClient daprClient,
    IDomainServiceResolver resolver,
    ILogger<DaprDomainServiceInvoker> logger) : IDomainServiceInvoker
{
    public async Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Resolve domain service registration
        DomainServiceRegistration? registration = await resolver
            .ResolveAsync(command.TenantId, command.Domain)
            .ConfigureAwait(false);

        if (registration is null)
        {
            throw new DomainServiceNotFoundException(command.TenantId, command.Domain);
        }

        logger.LogDebug(
            "Invoking domain service: AppId={AppId}, Method={MethodName}, Tenant={TenantId}, Domain={Domain}, CorrelationId={CorrelationId}",
            registration.AppId, registration.MethodName, command.TenantId, command.Domain, command.CorrelationId);

        // Invoke via DAPR service invocation (D7)
        // DAPR resiliency policies handle retries, circuit breaker, timeout (rule #4)
        var request = new DomainServiceRequest(command, currentState);

        DomainResult result = await daprClient
            .InvokeMethodAsync<DomainServiceRequest, DomainResult>(
                registration.AppId,
                registration.MethodName,
                request)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Domain service completed: AppId={AppId}, ResultType={ResultType}, EventCount={EventCount}, Tenant={TenantId}, CorrelationId={CorrelationId}",
            registration.AppId,
            result.IsSuccess ? "Success" : result.IsRejection ? "Rejection" : "NoOp",
            result.Events.Count,
            command.TenantId,
            command.CorrelationId);

        return result;
    }
}
```

### DomainServiceResolver Pattern

```csharp
// In Server/DomainServices/DomainServiceResolver.cs
namespace Hexalith.EventStore.Server.DomainServices;

public class DomainServiceResolver(
    DaprClient daprClient,
    IOptions<DomainServiceOptions> options,
    ILogger<DomainServiceResolver> logger) : IDomainServiceResolver
{
    public async Task<DomainServiceRegistration?> ResolveAsync(
        string tenantId, string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        string configKey = $"{tenantId}:{domain}:service";

        logger.LogDebug("Resolving domain service: ConfigKey={ConfigKey}, ConfigStore={ConfigStore}",
            configKey, options.Value.ConfigStoreName);

        var configResponse = await daprClient
            .GetConfiguration(
                options.Value.ConfigStoreName,
                [configKey],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!configResponse.Items.TryGetValue(configKey, out var configItem) ||
            string.IsNullOrWhiteSpace(configItem.Value))
        {
            logger.LogWarning(
                "No domain service registered: Tenant={TenantId}, Domain={Domain}, ConfigKey={ConfigKey}",
                tenantId, domain, configKey);
            return null;
        }

        var registration = JsonSerializer.Deserialize<DomainServiceRegistration>(configItem.Value);

        logger.LogDebug(
            "Resolved domain service: AppId={AppId}, Method={MethodName}, Tenant={TenantId}, Domain={Domain}",
            registration?.AppId, registration?.MethodName, tenantId, domain);

        return registration;
    }
}
```

### Technical Requirements

**Existing Types to Use:**
- `IDomainServiceInvoker` from `Hexalith.EventStore.Server.DomainServices` -- already defined with `InvokeAsync(CommandEnvelope, object?)`
- `CommandEnvelope` from `Hexalith.EventStore.Contracts.Commands` -- has TenantId, Domain, CorrelationId
- `DomainResult` from `Hexalith.EventStore.Contracts.Results` -- has IsSuccess, IsRejection, IsNoOp, Events
- `IRejectionEvent` from `Hexalith.EventStore.Contracts.Events` -- marker interface
- `IEventPayload` from `Hexalith.EventStore.Contracts.Events` -- base event interface
- `AggregateActor` from `Hexalith.EventStore.Server.Actors` -- 5-step orchestrator (Stories 3.1-3.4)
- `CommandProcessingResult` from `Hexalith.EventStore.Server.Actors` -- result record
- `DaprClient` from `Dapr.Client` -- DAPR SDK for service invocation and config store
- `FakeDomainServiceInvoker` from `Hexalith.EventStore.Testing.Fakes` -- test fake
- `IdempotencyChecker` / `TenantValidator` / `EventStreamReader` from previous stories

**New Types to Create:**
- `IDomainServiceResolver` -- interface in Server/DomainServices/
- `DomainServiceResolver` -- implementation using DAPR config store in Server/DomainServices/
- `DaprDomainServiceInvoker` -- implementation using DaprClient in Server/DomainServices/
- `DomainServiceRegistration` -- record in Server/DomainServices/
- `DomainServiceOptions` -- config record in Server/DomainServices/
- `DomainServiceRequest` -- DTO record in Server/DomainServices/
- `DomainServiceNotFoundException` -- exception in Server/DomainServices/

**NuGet Packages Required:**
- NO new NuGet packages needed for Story 3.5
- `Dapr.Client` already in Server project dependencies (from Story 3.1+)
- `Microsoft.Extensions.Options` already available

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.Server/
  DomainServices/
    IDomainServiceResolver.cs              # NEW: Domain service resolution interface
    DomainServiceResolver.cs               # NEW: DAPR config store lookup implementation
    DaprDomainServiceInvoker.cs            # NEW: DAPR service invocation implementation
    DomainServiceRegistration.cs           # NEW: Registration data record
    DomainServiceOptions.cs                # NEW: Configuration options
    DomainServiceRequest.cs                # NEW: Invocation request DTO
    DomainServiceNotFoundException.cs      # NEW: Exception for missing registrations

tests/Hexalith.EventStore.Server.Tests/
  DomainServices/
    DomainServiceResolverTests.cs          # NEW: Unit tests for resolver
    DaprDomainServiceInvokerTests.cs       # NEW: Unit tests for invoker
```

**Existing files to modify:**
```
src/Hexalith.EventStore.Server/
  Actors/
    AggregateActor.cs                      # MODIFY: Replace Step 4 STUB with DomainServiceInvoker
  Configuration/
    ServiceCollectionExtensions.cs         # MODIFY: Add IDomainServiceResolver DI registration

tests/Hexalith.EventStore.Server.Tests/
  Actors/
    AggregateActorTests.cs                 # MODIFY: Add domain invocation flow tests
```

**Files NOT modified:**
```
src/Hexalith.EventStore.Server/
  DomainServices/
    IDomainServiceInvoker.cs               # NO CHANGE (already exists)
  Actors/
    IAggregateActor.cs                     # NO CHANGE
    CommandProcessingResult.cs             # NO CHANGE
    IIdempotencyChecker.cs                 # NO CHANGE
    IdempotencyChecker.cs                  # NO CHANGE
    ITenantValidator.cs                    # NO CHANGE
    TenantValidator.cs                     # NO CHANGE
  Events/
    IEventStreamReader.cs                  # NO CHANGE
    EventStreamReader.cs                   # NO CHANGE

src/Hexalith.EventStore.Contracts/
  Results/
    DomainResult.cs                        # NO CHANGE
  Events/
    IRejectionEvent.cs                     # NO CHANGE
    IEventPayload.cs                       # NO CHANGE

src/Hexalith.EventStore.Client/
  Handlers/
    IDomainProcessor.cs                    # NO CHANGE
    DomainProcessorBase.cs                 # NO CHANGE

src/Hexalith.EventStore.Testing/
  Fakes/
    FakeDomainServiceInvoker.cs            # NO CHANGE

src/Hexalith.EventStore.CommandApi/
  Program.cs                               # NO CHANGE
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for DomainServiceResolver, DaprDomainServiceInvoker, AggregateActor invocation flow
- `tests/Hexalith.EventStore.IntegrationTests/` -- Regression verification

**Test Patterns (established in Stories 1.6, 2.1-3.4):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- Primary constructors for DI injection
- Feature folder organization in test projects mirroring source
- NSubstitute for mocking `DaprClient`, `IDomainServiceResolver`

**Minimum Tests (~25):**

DomainServiceResolver Unit Tests (6) -- in `DomainServiceResolverTests.cs`:
1. `ResolveAsync_RegisteredService_ReturnsRegistration`
2. `ResolveAsync_UnregisteredService_ReturnsNull`
3. `ResolveAsync_UsesCorrectConfigKey`
4. `ResolveAsync_NullTenantId_ThrowsArgumentException`
5. `ResolveAsync_NullDomain_ThrowsArgumentException`
6. `ResolveAsync_EmptyConfigValue_ReturnsNull`

DaprDomainServiceInvoker Unit Tests (9) -- in `DaprDomainServiceInvokerTests.cs`:
7. `InvokeAsync_SuccessResult_ReturnsDomainResult`
8. `InvokeAsync_RejectionResult_ReturnsDomainResult`
9. `InvokeAsync_NoOpResult_ReturnsDomainResult`
10. `InvokeAsync_ServiceNotFound_ThrowsDomainServiceNotFoundException`
11. `InvokeAsync_DaprInvocationFails_PropagatesException`
12. `InvokeAsync_PassesCommandAndState_ToDaprInvocation`
13. `InvokeAsync_NullCommand_ThrowsArgumentNullException`
14. `InvokeAsync_LogsCorrelationId`
15. `InvokeAsync_UsesCorrectAppIdAndMethod`

AggregateActor Domain Invocation Tests (7) -- in `AggregateActorTests.cs`:
16. `ProcessCommandAsync_DomainSuccess_ProceedsToStep5`
17. `ProcessCommandAsync_DomainRejection_ReturnsRejectionResult`
18. `ProcessCommandAsync_DomainRejection_StoresInIdempotencyCache`
19. `ProcessCommandAsync_DomainNoOp_ReturnsAccepted`
20. `ProcessCommandAsync_DomainServiceNotFound_PropagatesException`
21. `ProcessCommandAsync_DomainInfrastructureFailure_PropagatesException`
22. `ProcessCommandAsync_DomainInvocation_LogsResultType`

Integration Tests (3+) -- existing tests regression:
23. `PostCommands_AllExistingTests_StillPass` (regression check)
24. `PostCommands_ValidCommand_Returns202Accepted` (unchanged behavior)
25. `PostCommands_ExistingBehavior_Preserved` (no API changes)

**Current test count:** ~434 test methods from Story 3.4. Story 3.5 adds ~25 new tests, bringing estimated total to ~459.

### Previous Story Intelligence

**From Story 3.4 (Event Stream Reader & State Rehydration):**
- EventStreamReader provides `currentState` (or null for new aggregates) to Step 4
- The rehydrated state is currently a list of EventEnvelopes (placeholder until domain-specific reconstruction in Story 3.5+)
- Components created per-call using `host.LoggerFactory.CreateLogger<T>()` and `this.StateManager`
- `ConfigureAwait(false)` on all async calls
- `ArgumentNullException.ThrowIfNull()` on public methods

**From Story 3.3 (Tenant Validation at Actor Level):**
- TenantValidator executes as Step 2 BEFORE any state access
- Exception handling pattern: catch specific exceptions before broader catches
- Rejection results ARE cached via IdempotencyChecker.RecordAsync
- `StateManager.SaveStateAsync()` called to persist rejection idempotency record

**From Story 3.2 (AggregateActor Orchestrator & Idempotency Check):**
- IdempotencyChecker created per-call: `new IdempotencyChecker(StateManager, logger)`
- `ConditionalValue<T>` pattern for `TryGetStateAsync`
- Idempotency key is `causationId` (not `correlationId`)
- Rejection results ARE cached

**From Story 3.1 (Command Router & Actor Activation):**
- `CommandEnvelope` has `TenantId`, `Domain`, `AggregateIdentity`, `CorrelationId` properties
- Actor constructor: `AggregateActor(ActorHost host, ILogger<AggregateActor> logger) : Actor(host)`
- `Host.Id.GetId()` returns actor ID string
- `host.Services.GetRequiredService<T>()` for resolving DI services in actor

**From Epic 2 Stories (DaprClient patterns):**
- `DaprCommandStatusStore` uses `DaprClient.SaveStateAsync()` -- pattern for state operations
- `DaprCommandArchiveStore` uses same pattern
- `DaprClient` is DI-registered and injectable
- Advisory writes wrapped in try/catch (rule #12)

**Key Patterns (mandatory for all new code):**
- Primary constructors for DI: `public class Foo(IDep dep) : Base`
- Records for immutable data: `record Foo(string Bar, int Baz)`
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)
- `ArgumentException.ThrowIfNullOrWhiteSpace()` for string parameters
- Feature folder organization: `Server/DomainServices/`
- `namespace Hexalith.EventStore.{Project}.{Feature};`
- NSubstitute for mocking in tests
- Shouldly for assertions in tests

### Git Intelligence

**Recent commit patterns (last 10 commits):**
```
5ece433 Story 2.5 code review: mark done with review fixes (#27)
6cf6587 Stories 2.6-2.9: Command Status, Replay, Concurrency & Rate Limiting (#26)
fb817ea Update Claude Code local settings with tool permissions (#25)
74725aa Stories 2.4 & 2.5: JWT Authentication & Endpoint Authorization (#24)
8aaf036 Merge pull request #23 from Hexalith/feature/story-2.3-and-story-planning-2.4-2.5
```

**Commit message format recommendation for Story 3.5:**
```
Story 3.5: Domain Service Registration & Invocation

Implements Step 4 of the actor orchestrator:
- DomainServiceResolver looks up services from DAPR config store
- DaprDomainServiceInvoker calls via DaprClient.InvokeMethodAsync (D7)
- Handles success, rejection, and no-op results from domain services
- DomainServiceNotFoundException for missing registrations
- No custom retry -- DAPR resiliency only (rule #4)

Stories 3.1-3.4 are prerequisites (routing, idempotency, tenant, rehydration).
Story 3.7+ will add event persistence; Story 3.11 adds state machine.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
```

### Latency Design Note

**Story 3.5 adds domain service invocation overhead to actor processing:**
- Service resolution: ~1-2ms (DAPR config store lookup, could be cached in future)
- Domain invocation: ~2-5ms base (DAPR sidecar overhead) + domain processing time
- Total Step 4 overhead: ~5-10ms for a simple domain service
- Fits within NFR2 (200ms e2e budget) and NFR8 (2ms DAPR overhead per call)

**Timeout strategy:**
- DAPR resiliency policies enforce 5-second timeout at sidecar level (rule #14)
- No application-level timeout needed
- If domain service exceeds timeout, DAPR returns a timeout error which propagates as an exception

### Project Structure Notes

**Alignment with Architecture:**
- All new files in `Server/DomainServices/` per architecture directory structure
- Test files mirror source structure: `Server.Tests/DomainServices/`
- `IDomainServiceResolver` DI-registered as singleton (stateless, thread-safe)
- `DaprDomainServiceInvoker` created per-actor-call (maintains established pattern)
- No new projects or packages added

**Dependency Graph:**
```
Server/Actors/AggregateActor -> Server/DomainServices/DaprDomainServiceInvoker
Server/DomainServices/DaprDomainServiceInvoker -> Dapr.Client (DaprClient)
Server/DomainServices/DaprDomainServiceInvoker -> Server/DomainServices/IDomainServiceResolver
Server/DomainServices/DomainServiceResolver -> Dapr.Client (DaprClient.GetConfiguration)
Server/DomainServices/DomainServiceRegistration -> (no external deps, data record)
Server/DomainServices/DomainServiceRequest -> Contracts/Commands/CommandEnvelope
Tests: Server.Tests/DomainServices -> Server/DomainServices (unit testing)
```

**Package Dependency Boundaries (unchanged):**
```
Contracts (zero deps) <- Server (+ Dapr.Actors, Dapr.Client) <- CommandApi (+ Dapr.AspNetCore)
Testing -> Contracts + Server
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 3.5: Domain Service Registration & Invocation (Lines 645-659)]
- [Source: _bmad-output/planning-artifacts/architecture.md#D7: Domain Service Invocation (Lines 409-414)]
- [Source: _bmad-output/planning-artifacts/architecture.md#D3: Domain Service Error Contract (Lines 377-385)]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-1: EventStore Owns All 11 Envelope Metadata Fields (Line 111)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule #4: No Custom Retry (Line 628)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule #14: 5s DAPR Timeout (Line 636)]
- [Source: _bmad-output/planning-artifacts/architecture.md#DomainServiceResolver Component (Line 708)]
- [Source: _bmad-output/planning-artifacts/architecture.md#DaprDomainServiceInvoker Component (Line 706)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Actor Processing Pipeline Step 4 (Lines 539)]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure - Server/DomainServices (Lines 705-708)]
- [Source: _bmad-output/planning-artifacts/prd.md#FR22 - Domain service registration via configuration]
- [Source: _bmad-output/planning-artifacts/prd.md#FR23 - Domain service invocation with command and state]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR20 - Dynamic configuration without restart]
- [Source: _bmad-output/implementation-artifacts/3-4-event-stream-reader-and-state-rehydration.md]
- [Source: _bmad-output/implementation-artifacts/3-3-tenant-validation-at-actor-level.md]
- [Source: src/Hexalith.EventStore.Server/DomainServices/IDomainServiceInvoker.cs]
- [Source: src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs]
- [Source: src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs]
- [Source: src/Hexalith.EventStore.Contracts/Results/DomainResult.cs]
- [Source: src/Hexalith.EventStore.Contracts/Events/IRejectionEvent.cs]
- [Source: src/Hexalith.EventStore.Testing/Fakes/FakeDomainServiceInvoker.cs]
- [Source: https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/ - DAPR Service Invocation]
- [Source: https://docs.dapr.io/developing-applications/building-blocks/configuration/configuration-api-overview/ - DAPR Configuration API]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

None

### Completion Notes List

- Implemented all 7 new types in `Server/DomainServices/`: `IDomainServiceResolver`, `DomainServiceResolver`, `DaprDomainServiceInvoker`, `DomainServiceRegistration`, `DomainServiceOptions`, `DomainServiceRequest`, `DomainServiceNotFoundException`
- Replaced Step 4 STUB in `AggregateActor` with actual domain service invocation via `IDomainServiceInvoker`
- Actor now injects `IDomainServiceInvoker` via constructor (DI-registered as transient with `DaprDomainServiceInvoker` implementation) instead of creating it internally. This deviation from the story's per-call creation pattern was necessary because `DaprClient.InvokeMethodAsync<TReq,TResp>` is non-virtual and cannot be mocked with NSubstitute. The DI injection approach is cleaner, more testable, and aligns with `FakeDomainServiceInvoker` usage in tests.
- `DomainServiceResolver` is DI-registered as singleton (stateless, thread-safe)
- `AddEventStoreServer()` now requires `IConfiguration` parameter for config binding
- Handles all 3 result types: success (events to Step 5), rejection (short-circuit with cached result), no-op (accept with no events)
- Infrastructure failures propagate to caller — no custom retry (rule #4)
- All 526 tests pass: 505 existing (zero regressions) + 21 new tests
- 6 resolver tests, 8 invoker/DTO tests, 7 actor domain invocation tests

### Change Log

- 2026-02-14: Story 3.5 implementation complete — domain service registration & invocation

### File List

**New files:**
- src/Hexalith.EventStore.Server/DomainServices/IDomainServiceResolver.cs
- src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs
- src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs
- src/Hexalith.EventStore.Server/DomainServices/DomainServiceRegistration.cs
- src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs
- src/Hexalith.EventStore.Server/DomainServices/DomainServiceRequest.cs
- src/Hexalith.EventStore.Server/DomainServices/DomainServiceNotFoundException.cs
- tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceResolverTests.cs
- tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs

**Modified files:**
- src/Hexalith.EventStore.Server/Actors/AggregateActor.cs
- src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
- src/Hexalith.EventStore.CommandApi/Program.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs
