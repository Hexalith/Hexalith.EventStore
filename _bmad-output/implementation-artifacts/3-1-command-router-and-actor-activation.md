# Story 3.1: Command Router & Actor Activation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **system operator**,
I want submitted commands routed to the correct aggregate actor based on the canonical identity scheme (`tenant:domain:aggregate-id`),
So that each aggregate has a dedicated processing context (FR3).

## Acceptance Criteria

1. **Command routed to correct actor** - Given a validated command arrives from the MediatR pipeline, When the SubmitCommandHandler processes the command, Then it derives the actor ID from AggregateIdentity (`tenant:domain:aggregate-id`) using the canonical derivation from Contracts (FR26), And it invokes the correct DAPR actor using the derived actor ID.

2. **AggregateActor activates and receives command** - When the CommandRouter invokes the DAPR actor proxy, Then the AggregateActor activates (cold or warm) and receives the command as a `CommandEnvelope`. The actor logs receipt at Information level with correlationId, tenantId, domain, aggregateId. For Story 3.1, the actor method is a STUB that returns a placeholder `CommandProcessingResult` indicating the command was received.

3. **CommandRouter registered in DI** - The CommandRouter is registered in DI via `AddEventStoreServer()` extension method (enforcement rule #10). The extension method also registers DAPR actor infrastructure via `AddActors()`.

4. **DAPR actor endpoints mapped** - The CommandApi Program.cs calls `app.MapActorsHandlers()` to expose the DAPR actor HTTP endpoints required for sidecar communication.

5. **SubmitCommand converted to CommandEnvelope** - The CommandRouter converts the API-layer `SubmitCommand` into an internal `CommandEnvelope` before passing to the actor. The conversion preserves all fields: Tenant, Domain, AggregateId, CommandType, Payload, CorrelationId, Extensions. CausationId and UserId are populated from available context (correlationId as initial causationId, "system" as placeholder userId until JWT claims flow is wired in Story 3.3).

6. **Existing tests unbroken** - All existing tests (68+) continue to pass after SubmitCommandHandler is modified to call the CommandRouter. Unit tests mock the CommandRouter. Integration tests use an in-memory actor stub or mock.

## Prerequisites

**BLOCKING: Epic 2 stories through 2.7 MUST be complete (done status) before starting Story 3.1.** Story 3.1 depends on:
- `SubmitCommandHandler` with status writing and command archiving (Stories 2.6, 2.7)
- `SubmitCommand` MediatR command record with all fields (Story 2.1)
- `CommandEnvelope` from Contracts package (Story 1.2)
- `AggregateIdentity` with `ActorId` derivation (Story 1.2)
- `DaprClient` registration via `AddDaprClient()` (Story 2.6)
- MediatR pipeline: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler (Stories 2.3, 2.5)
- Established ProblemDetails error handling chain (Stories 2.2, 2.5, 2.8)

**Before beginning any Task below, verify:** Run existing tests to confirm all Epic 2 artifacts are in place. All existing tests must pass before proceeding.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and existing artifacts (BLOCKING)
  - [x] 0.1 Run all existing tests -- they must pass before proceeding
  - [x] 0.2 Confirm `SubmitCommandHandler` exists with status write + archive write
  - [x] 0.3 Confirm `CommandEnvelope` exists in Contracts with all required fields
  - [x] 0.4 Confirm `AggregateIdentity` exists with `ActorId` property deriving `{tenant}:{domain}:{aggregateId}`
  - [x] 0.5 Confirm `DaprClient` is registered via `AddDaprClient()` in Program.cs
  - [x] 0.6 Confirm Server project has `Dapr.Actors` and `Dapr.Actors.AspNetCore` NuGet packages (add if missing)

- [x] Task 1: Create IAggregateActor interface (AC: #2)
  - [x] 1.1 Create `IAggregateActor` interface in `Server/Actors/` extending `Dapr.Actors.IActor`
  - [x] 1.2 Define single method: `Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)`
  - [x] 1.3 `CommandProcessingResult` is a new record in `Server/Actors/`: `record CommandProcessingResult(bool Accepted, string? ErrorMessage = null, string? CorrelationId = null)`
  - [x] 1.4 Namespace: `Hexalith.EventStore.Server.Actors`

- [x] Task 2: Create AggregateActor STUB implementation (AC: #2)
  - [x] 2.1 Create `AggregateActor` class in `Server/Actors/` extending `Dapr.Actors.Runtime.Actor` and implementing `IAggregateActor`
  - [x] 2.2 Constructor: `AggregateActor(ActorHost host, ILogger<AggregateActor> logger)` -- call `base(host)`
  - [x] 2.3 `ProcessCommandAsync` STUB implementation: log command receipt at Information level with structured properties (correlationId, tenantId, domain, aggregateId), return `new CommandProcessingResult(Accepted: true, CorrelationId: command.CorrelationId)`
  - [x] 2.4 Log format: `"Actor {ActorId} received command: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}"` -- NEVER log payload (rule #5, SEC-5)
  - [x] 2.5 Add XML doc comment noting this is a stub -- Steps 1-5 of the orchestration pipeline will be implemented in Stories 3.2-3.11

- [x]Task 3: Create CommandRouter (AC: #1, #3, #5)
  - [x]3.1 Create `ICommandRouter` interface in `Server/Commands/`: `Task<CommandProcessingResult> RouteCommandAsync(SubmitCommand command, CancellationToken cancellationToken = default)`
  - [x]3.2 Create `CommandRouter` class in `Server/Commands/` implementing `ICommandRouter`
  - [x]3.3 Constructor: `CommandRouter(IActorProxyFactory actorProxyFactory, ILogger<CommandRouter> logger)`
  - [x]3.4 `RouteCommandAsync` implementation:
    - Create `AggregateIdentity` from command (Tenant, Domain, AggregateId) -- this validates the identity components
    - Derive actor ID: `identity.ActorId` (format: `{tenant}:{domain}:{aggregateId}`)
    - Convert `SubmitCommand` to `CommandEnvelope` (AC #5): map all fields, set CausationId = CorrelationId (initial), UserId = "system" (placeholder)
    - Create actor proxy: `actorProxyFactory.CreateActorProxy<IAggregateActor>(new ActorId(actorId), nameof(AggregateActor))`
    - Invoke: `await proxy.ProcessCommandAsync(envelope).ConfigureAwait(false)`
    - Return the `CommandProcessingResult`
  - [x]3.5 Log at Information level before actor invocation: `"Routing command to actor: CorrelationId={CorrelationId}, ActorId={ActorId}"` -- NEVER log payload (rule #5)
  - [x]3.6 Catch `Exception` from actor invocation, log at Error level with correlationId and actorId, re-throw (let exception handlers deal with it)
  - [x]3.7 Use `ArgumentNullException.ThrowIfNull()` on public method parameters (CA1062)

- [x]Task 4: Create SubmitCommand to CommandEnvelope conversion (AC: #5)
  - [x]4.1 Create `SubmitCommandExtensions` class in `Server/Commands/` with static method `ToCommandEnvelope(this SubmitCommand command)` returning `CommandEnvelope`
  - [x]4.2 Map fields: Tenant -> TenantId, Domain -> Domain, AggregateId -> AggregateId, CommandType -> CommandType, Payload -> Payload, CorrelationId -> CorrelationId, Extensions -> Extensions (note: `Dictionary<string, string>?` converts implicitly to `IReadOnlyDictionary<string, string>?` in CommandEnvelope)
  - [x]4.3 Set CausationId = command.CorrelationId (for initial submission, correlation IS the causation; replays will have distinct causation in later stories)
  - [x]4.4 Set UserId = "system" (placeholder -- will be populated from JWT claims in Story 3.3 when tenant validation flows user identity through)
  - [x]4.5 Verify `CommandEnvelope` constructor validates all fields -- if it throws, let the exception propagate (it means the command was malformed and should have been caught by validation)

- [x]Task 5: Modify SubmitCommandHandler to call CommandRouter (AC: #1, #6)
  - [x]5.1 Add `ICommandRouter` as an additional dependency in `SubmitCommandHandler` primary constructor
  - [x]5.2 After the existing status write and archive write, call `await _commandRouter.RouteCommandAsync(request, cancellationToken).ConfigureAwait(false)`
  - [x]5.3 The actor invocation is AWAITED -- the handler waits for the actor to acknowledge receipt before returning. For Story 3.1, the stub actor returns instantly so there's no latency impact. (Note: latency implications must be revisited in Stories 3.7+ when actor does real work)
  - [x]5.4 If the actor invocation throws, let the exception propagate up through MediatR to the exception handler chain (ConcurrencyConflictExceptionHandler from Story 2.8 will handle concurrency exceptions; GlobalExceptionHandler handles the rest)
  - [x]5.5 Do NOT wrap the router call in try/catch -- unlike advisory status/archive writes (rule #12), actor invocation failure IS a real error that should surface to the consumer
  - [x]5.6 Log at Debug level after successful routing: `"Command routed to actor: CorrelationId={CorrelationId}"`

- [x]Task 6: Create AddEventStoreServer() extension method (AC: #3, #4)
  - [x]6.1 Create `ServiceCollectionExtensions` class in `Server/Configuration/` (or enhance if exists) with `AddEventStoreServer(this IServiceCollection services)` method
  - [x]6.2 Register `ICommandRouter` as `CommandRouter` (singleton or scoped -- singleton is preferred since it only holds IActorProxyFactory which is singleton)
  - [x]6.3 Register DAPR actors: `services.AddActors(options => { options.Actors.RegisterActor<AggregateActor>(); })`
  - [x]6.4 Return `IServiceCollection` for fluent chaining
  - [x]6.5 Namespace: `Hexalith.EventStore.Server.Configuration`

- [x]Task 7: Update CommandApi Program.cs (AC: #3, #4)
  - [x]7.1 Add `builder.Services.AddEventStoreServer()` call in Program.cs DI registration section (after `AddCommandApi()`)
  - [x]7.2 Add `app.MapActorsHandlers()` call in the middleware pipeline (after `app.MapControllers()` or equivalent)
  - [x]7.3 Verify DI registration order per architecture: AddServiceDefaults -> AddAuthentication -> AddAuthorization -> AddRateLimiter -> AddMediatR -> AddCommandApi -> AddEventStoreServer -> AddActors (included in AddEventStoreServer)

- [x]Task 8: Update existing ServiceCollectionExtensions in CommandApi (AC: #3)
  - [x]8.1 In `CommandApi/Extensions/ServiceCollectionExtensions.AddCommandApi()`, add `ICommandRouter` registration if not already done via `AddEventStoreServer()`
  - [x]8.2 Alternatively: `AddCommandApi()` calls `AddEventStoreServer()` internally to keep a single registration entry point. Choose whichever pattern is cleaner with existing code. Document the chosen approach.

- [x]Task 9: Write unit tests for CommandRouter (AC: #1, #5)
  - [x]9.1 `RouteCommandAsync_ValidCommand_CreatesCorrectActorId` -- verify actor ID = `{tenant}:{domain}:{aggregateId}`
  - [x]9.2 `RouteCommandAsync_ValidCommand_InvokesActorProxy` -- verify `IActorProxyFactory.CreateActorProxy` called with correct ActorId and actor type name
  - [x]9.3 `RouteCommandAsync_ValidCommand_PassesCommandEnvelope` -- verify `ProcessCommandAsync` called with correctly-mapped CommandEnvelope
  - [x]9.4 `RouteCommandAsync_ValidCommand_ReturnsActorResult` -- verify result from actor is returned
  - [x]9.5 `RouteCommandAsync_ActorThrows_PropagatesException` -- verify exceptions from actor proxy are not swallowed
  - [x]9.6 `RouteCommandAsync_CommandEnvelope_HasCorrectCorrelationId` -- verify correlationId preserved
  - [x]9.7 `RouteCommandAsync_CommandEnvelope_HasCausationIdEqualToCorrelationId` -- verify initial causation chain
  - [x]9.8 `RouteCommandAsync_NullCommand_ThrowsArgumentNullException` -- verify guard clause

- [x]Task 10: Write unit tests for SubmitCommandExtensions (AC: #5)
  - [x]10.1 `ToCommandEnvelope_ValidCommand_MapsAllFields` -- verify all fields correctly mapped
  - [x]10.2 `ToCommandEnvelope_NullExtensions_MapsAsNull` -- verify nullable extensions handled
  - [x]10.3 `ToCommandEnvelope_CausationId_EqualsCorrelationId` -- verify initial causation
  - [x]10.4 `ToCommandEnvelope_UserId_IsSystem` -- verify placeholder userId

- [x]Task 11: Write unit tests for AggregateActor (AC: #2)
  - [x]11.1 `ProcessCommandAsync_ValidCommand_ReturnsAccepted` -- verify stub returns Accepted=true
  - [x]11.2 `ProcessCommandAsync_ValidCommand_ReturnsCorrelationId` -- verify correlationId in result
  - [x]11.3 `ProcessCommandAsync_ValidCommand_LogsCommandReceipt` -- verify structured logging with correct properties

- [x]Task 12: Update existing SubmitCommandHandler tests (AC: #6)
  - [x]12.1 Update `SubmitCommandHandler` unit tests to provide `ICommandRouter` mock (NSubstitute)
  - [x]12.2 Configure mock `ICommandRouter.RouteCommandAsync` to return `new CommandProcessingResult(true)`
  - [x]12.3 Add test: `Handle_ValidCommand_RoutesToActor` -- verify `_commandRouter.RouteCommandAsync` called with correct command
  - [x]12.4 Add test: `Handle_RouterThrows_PropagatesException` -- verify exception propagation (not swallowed)
  - [x]12.5 Verify ALL existing SubmitCommandHandler tests still pass (status write, archive write behavior unchanged)

- [x]Task 13: Write integration tests (AC: #1, #2, #6)
  - [x]13.1 Create `FakeAggregateActor` in `Testing/Fakes/` implementing `IAggregateActor` -- records invocations for assertion, returns configurable results
  - [x]13.2 In `JwtAuthenticatedWebApplicationFactory`, override actor registration to use `FakeAggregateActor` instead of real DAPR actor
  - [x]13.3 `PostCommands_ValidCommand_RoutesToActor` -- submit command via POST, verify FakeAggregateActor received it with correct fields
  - [x]13.4 `PostCommands_ValidCommand_ActorReceivesCorrectTenant` -- verify tenant propagated
  - [x]13.5 `PostCommands_ValidCommand_ActorReceivesCorrectAggregateId` -- verify aggregateId propagated
  - [x]13.6 `PostCommands_ValidCommand_Returns202Accepted` -- verify existing 202 behavior unchanged
  - [x]13.7 `PostCommands_ValidCommand_ActorReceivesCommandEnvelope` -- verify SubmitCommand -> CommandEnvelope conversion
  - [x]13.8 `PostCommands_ActorThrows_Returns500ProblemDetails` -- verify exception propagation to GlobalExceptionHandler
  - [x]13.9 Verify all existing integration tests still pass (authentication, authorization, validation, replay, status)

- [x]Task 14: Verify CommandEnvelope DAPR serialization (AC: #2)
  - [x]14.1 Write a unit test that serializes `CommandEnvelope` to JSON and deserializes back -- verify roundtrip fidelity
  - [x]14.2 Verify `byte[] Payload` serializes as base64 string and deserializes correctly
  - [x]14.3 Verify nullable `Extensions` dictionary roundtrips correctly
  - [x]14.4 This catches serialization issues that would only surface at DAPR actor invocation time

- [x]Task 15: Run all tests and verify zero regressions (AC: #6)
  - [x]15.1 Run all existing tests -- zero regressions expected
  - [x]15.2 Run new tests -- all must pass
  - [x]15.3 Verify total test count (estimated: ~346 existing + ~25 new = ~371)

## Dev Notes

### Architecture Compliance

**FR3: Command Routing to Aggregate Actor:**
- Commands are routed to the correct DAPR actor based on `AggregateIdentity.ActorId` (`tenant:domain:aggregate-id`)
- The canonical identity tuple from FR26 is used for all actor addressing
- Each aggregate gets a dedicated actor instance via DAPR's virtual actor model

**Architecture Data Flow (Story 3.1 scope):**
```
SubmitCommandHandler (existing, modified)
    |-- Write Received status (Story 2.6, unchanged)
    |-- Archive original command (Story 2.7, unchanged)
    |-- NEW: Call CommandRouter.RouteCommandAsync(submitCommand)
         |-- Create AggregateIdentity from (tenant, domain, aggregateId)
         |-- Derive actorId = identity.ActorId
         |-- Convert SubmitCommand -> CommandEnvelope
         |-- Create DAPR actor proxy via IActorProxyFactory
         |-- Invoke proxy.ProcessCommandAsync(envelope)
         |-- AggregateActor.ProcessCommandAsync (STUB)
              |-- Log command receipt
              |-- Return CommandProcessingResult(Accepted: true)
    |-- Return SubmitCommandResult (existing)
```

**AggregateActor as Thin Orchestrator (architecture pattern):**
The architecture specifies a 5-step delegation pattern. Story 3.1 creates the actor SHELL with a stub method body. The 5 steps are implemented in subsequent stories:
1. Idempotency check -> Story 3.2
2. Tenant validation -> Story 3.3
3. State rehydration -> Story 3.4
4. Domain service invocation -> Story 3.5
5. State machine execution -> Story 3.11

**Enforcement Rules to Follow:**
- Rule #5: Never log event payload data -- only envelope metadata (SEC-5, NFR12)
- Rule #6: Use `IActorStateManager` for all actor state operations (not used yet in 3.1, but actor has access via `this.StateManager`)
- Rule #7: ProblemDetails for all API error responses
- Rule #9: correlationId in every structured log entry
- Rule #10: Register services via `Add*` extension methods
- Rule #12: Status/archive writes are advisory (existing behavior, unchanged)
- Rule #13: No stack traces in production error responses

### Critical Design Decisions

**F1 (Pre-mortem): CommandRouter is a SEPARATE component from SubmitCommandHandler.**
The handler is NOT turned into a monolith. It continues to write status and archive (advisory), then delegates routing to CommandRouter. The router is a clean, focused component with a single responsibility: derive actor ID, create proxy, invoke.

**F2 (ADR): Handler AWAITS actor invocation.**
The handler awaits the actor proxy call. For Story 3.1, the stub returns instantly (zero latency impact). For later stories when the actor does real work, latency implications must be revisited. DAPR actor proxy calls are synchronous from the caller's perspective -- `proxy.ProcessCommandAsync()` returns when the actor method completes. If processing takes >50ms (NFR1), a fire-and-forget pattern may be needed in Story 3.7+.

**F5 (Pre-mortem): Story scope is ONLY routing + activation.**
The AggregateActor is a STUB. It receives the command, logs it, and returns. No idempotency check, no tenant validation, no state rehydration, no domain invocation, no state machine. Those are Stories 3.2-3.11.

**F6 (First Principles): SubmitCommand -> CommandEnvelope conversion.**
`SubmitCommand` is the API-layer representation (MediatR command). `CommandEnvelope` is the internal processing representation (Contracts type). The router converts between them. This maintains the clean boundary between API concerns and processing concerns.

**F7 (Failure Mode): CommandEnvelope must be JSON-serializable.**
DAPR actor proxy calls serialize parameters as JSON over HTTP. `CommandEnvelope` contains `byte[] Payload` (serializes as base64) and nullable `Dictionary<string, string>? Extensions`. Both must roundtrip correctly. A dedicated serialization test validates this.

**F10 (Pre-mortem): No DAPR sidecar required for tests.**
Unit tests mock `IActorProxyFactory` and `IAggregateActor`. Integration tests use a `FakeAggregateActor` registered in the test DI container. No DAPR sidecar needed for Story 3.1 testing. Real DAPR integration testing is Tier 2 (Epic 7, Story 7.4).

**What Already Exists (from Stories 1.1-2.8):**
- `CommandEnvelope` in Contracts -- 9-parameter record (TenantId, Domain, AggregateId, CommandType, Payload, CorrelationId, CausationId, UserId, Extensions) + 4 computed properties (AggregateIdentity, EventStreamKeyPrefix, MetadataKey, SnapshotKey)
- `AggregateIdentity` in Contracts -- canonical tuple with `ActorId`, `EventStreamKeyPrefix`, `MetadataKey`, `SnapshotKey`, `PubSubTopic` derivations
- `SubmitCommand` MediatR command record with Tenant, Domain, AggregateId, CommandType, Payload, CorrelationId, Extensions
- `SubmitCommandHandler` -- writes Received status (Story 2.6) + archives command (Story 2.7) + returns SubmitCommandResult
- MediatR pipeline: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> SubmitCommandHandler
- `ICommandStatusStore` + `DaprCommandStatusStore` + `InMemoryCommandStatusStore` (Story 2.6)
- `ICommandArchiveStore` + `DaprCommandArchiveStore` + `InMemoryCommandArchiveStore` (Story 2.7)
- `ConcurrencyConflictException` + `ConcurrencyConflictExceptionHandler` (Story 2.8)
- IExceptionHandler chain: Validation -> Authorization -> ConcurrencyConflict -> Global
- `DaprClient` registered via `AddDaprClient()` (Story 2.6)
- `IDomainServiceInvoker` interface in `Server/DomainServices/` (exists but not implemented yet)
- `DomainResult` in Contracts with success/rejection/noop semantics

**What Story 3.1 Adds:**
1. **`IAggregateActor`** -- DAPR actor interface in Server/Actors/ extending IActor
2. **`AggregateActor`** -- DAPR actor STUB implementation in Server/Actors/
3. **`CommandProcessingResult`** -- result record in Server/Actors/
4. **`ICommandRouter`** -- routing abstraction in Server/Commands/
5. **`CommandRouter`** -- implementation using IActorProxyFactory in Server/Commands/
6. **`SubmitCommandExtensions`** -- SubmitCommand -> CommandEnvelope conversion in Server/Commands/
7. **`AddEventStoreServer()`** -- DI extension method in Server/Configuration/
8. **`FakeAggregateActor`** -- test fake in Testing/Fakes/
9. **Modified `SubmitCommandHandler`** -- now calls CommandRouter after status/archive writes
10. **Modified `Program.cs`** -- adds AddEventStoreServer() and MapActorsHandlers()

**DAPR Actor Registration Pattern:**
```csharp
// In Server/Configuration/ServiceCollectionExtensions.cs
public static IServiceCollection AddEventStoreServer(this IServiceCollection services)
{
    services.AddSingleton<ICommandRouter, CommandRouter>();
    services.AddActors(options =>
    {
        options.Actors.RegisterActor<AggregateActor>();
    });
    return services;
}

// In CommandApi/Program.cs
builder.Services.AddEventStoreServer();
// ...
app.MapActorsHandlers();
```

**Actor Interface & Implementation Pattern:**
```csharp
// In Server/Actors/IAggregateActor.cs
namespace Hexalith.EventStore.Server.Actors;

public interface IAggregateActor : IActor
{
    Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command);
}

// In Server/Actors/AggregateActor.cs
namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Aggregate actor -- thin orchestrator for command processing.
/// Story 3.1: STUB -- receives command and returns acknowledgment.
/// Stories 3.2-3.11 implement the 5-step delegation pipeline:
///   1. Idempotency check (3.2)
///   2. Tenant validation (3.3)
///   3. State rehydration (3.4)
///   4. Domain service invocation (3.5)
///   5. State machine execution (3.11)
/// </summary>
public class AggregateActor(ActorHost host, ILogger<AggregateActor> logger)
    : Actor(host), IAggregateActor
{
    public Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger.LogInformation(
            "Actor {ActorId} received command: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
            Host.Id,
            command.CorrelationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType);

        return Task.FromResult(new CommandProcessingResult(
            Accepted: true,
            CorrelationId: command.CorrelationId));
    }
}
```

**CommandRouter Pattern:**
```csharp
// In Server/Commands/CommandRouter.cs
namespace Hexalith.EventStore.Server.Commands;

public class CommandRouter(
    IActorProxyFactory actorProxyFactory,
    ILogger<CommandRouter> logger) : ICommandRouter
{
    public async Task<CommandProcessingResult> RouteCommandAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        string actorId = identity.ActorId;

        logger.LogInformation(
            "Routing command to actor: CorrelationId={CorrelationId}, ActorId={ActorId}",
            command.CorrelationId,
            actorId);

        CommandEnvelope envelope = command.ToCommandEnvelope();

        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(actorId),
            nameof(AggregateActor));

        return await proxy.ProcessCommandAsync(envelope).ConfigureAwait(false);
    }
}
```

**SubmitCommandHandler Modification:**
```csharp
// BEFORE (Stories 2.6-2.7):
public class SubmitCommandHandler(
    ICommandStatusStore statusStore,
    ICommandArchiveStore archiveStore,
    ILogger<SubmitCommandHandler> logger)
    : IRequestHandler<SubmitCommand, SubmitCommandResult>

// AFTER (Story 3.1):
public class SubmitCommandHandler(
    ICommandStatusStore statusStore,
    ICommandArchiveStore archiveStore,
    ICommandRouter commandRouter,
    ILogger<SubmitCommandHandler> logger)
    : IRequestHandler<SubmitCommand, SubmitCommandResult>
{
    public async Task<SubmitCommandResult> Handle(
        SubmitCommand request, CancellationToken cancellationToken)
    {
        // ... existing status write (advisory, unchanged) ...
        // ... existing archive write (advisory, unchanged) ...

        // NEW: Route to actor
        await commandRouter.RouteCommandAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return result;
    }
}
```

**Why actor invocation is NOT advisory (unlike status/archive writes):**
Status and archive writes are advisory per enforcement rule #12 -- their failure should never block the pipeline. Actor invocation is DIFFERENT. If the actor fails to process the command, the consumer must know (via the exception handler chain returning 500/409). A silently-swallowed actor failure would leave the command stuck in "Received" status forever with no way to recover. The exception propagates through MediatR back to the IExceptionHandler chain.

### Technical Requirements

**Existing Types to Use:**
- `CommandEnvelope` from `Hexalith.EventStore.Contracts.Events` -- internal processing representation
- `AggregateIdentity` from `Hexalith.EventStore.Contracts.Identity` -- canonical tuple with ActorId derivation
- `SubmitCommand` from `Hexalith.EventStore.Server.Pipeline.Commands` -- MediatR command
- `SubmitCommandResult` from same namespace -- result with CorrelationId
- `ICommandStatusStore` from `Hexalith.EventStore.Server.Commands` -- status read/write (Story 2.6)
- `ICommandArchiveStore` from `Hexalith.EventStore.Server.Commands` -- archive read/write (Story 2.7)
- `DaprClient` from `Dapr.Client` -- already registered
- `IActorProxyFactory` from `Dapr.Actors.Client` -- creates actor proxies
- `ActorId` from `Dapr.Actors` -- DAPR actor identifier
- `ActorHost` from `Dapr.Actors.Runtime` -- actor host context
- `Actor` from `Dapr.Actors.Runtime` -- base class for DAPR actors
- `IActor` from `Dapr.Actors` -- base interface for DAPR actor interfaces

**New Types to Create:**
- `IAggregateActor` -- DAPR actor interface (Server/Actors/)
- `AggregateActor` -- DAPR actor stub implementation (Server/Actors/)
- `CommandProcessingResult` -- actor method result record (Server/Actors/)
- `ICommandRouter` -- routing abstraction interface (Server/Commands/)
- `CommandRouter` -- routing implementation (Server/Commands/)
- `SubmitCommandExtensions` -- SubmitCommand -> CommandEnvelope conversion (Server/Commands/)
- `ServiceCollectionExtensions` -- AddEventStoreServer() (Server/Configuration/)
- `FakeAggregateActor` -- test fake (Testing/Fakes/)

**NuGet Packages Required:**
- `Dapr.Actors` (already in Server.csproj or needs adding) -- `IActor`, `ActorId`
- `Dapr.Actors.AspNetCore` (needs adding to Server.csproj or CommandApi.csproj) -- `AddActors()`, `MapActorsHandlers()`
- `Dapr.Actors.Client` (may need adding) -- `IActorProxyFactory`
- All existing packages remain unchanged

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.Server/
  Actors/
    IAggregateActor.cs              # NEW: DAPR actor interface extending IActor
    AggregateActor.cs               # NEW: STUB actor implementation
    CommandProcessingResult.cs      # NEW: Actor method result record
  Commands/
    ICommandRouter.cs               # NEW: Routing abstraction interface
    CommandRouter.cs                # NEW: Routing implementation with IActorProxyFactory
    SubmitCommandExtensions.cs      # NEW: SubmitCommand -> CommandEnvelope conversion
  Configuration/
    ServiceCollectionExtensions.cs  # NEW: AddEventStoreServer() DI extension

src/Hexalith.EventStore.Testing/
  Fakes/
    FakeAggregateActor.cs           # NEW: Test fake implementing IAggregateActor

tests/Hexalith.EventStore.Server.Tests/
  Commands/
    CommandRouterTests.cs           # NEW: Unit tests for CommandRouter
    SubmitCommandExtensionsTests.cs # NEW: Unit tests for conversion
  Actors/
    AggregateActorTests.cs          # NEW: Unit tests for actor stub

tests/Hexalith.EventStore.IntegrationTests/
  CommandApi/
    CommandRoutingIntegrationTests.cs # NEW: Integration tests for routing flow
  Serialization/
    CommandEnvelopeSerializationTests.cs # NEW: DAPR serialization roundtrip tests
```

**Existing files to modify:**
```
src/Hexalith.EventStore.Server/
  Pipeline/
    SubmitCommandHandler.cs         # MODIFY: Add ICommandRouter dependency, call after archive write

src/Hexalith.EventStore.CommandApi/
  Extensions/
    ServiceCollectionExtensions.cs  # MODIFY: Call AddEventStoreServer() or register ICommandRouter
  Program.cs                        # MODIFY: Add AddEventStoreServer() and MapActorsHandlers()

src/Hexalith.EventStore.Server/
  Hexalith.EventStore.Server.csproj # VERIFY/MODIFY: Ensure Dapr.Actors, Dapr.Actors.AspNetCore packages

tests/Hexalith.EventStore.Server.Tests/
  Pipeline/
    SubmitCommandHandlerTests.cs    # MODIFY: Add ICommandRouter mock
  Commands/
    SubmitCommandHandlerStatusTests.cs   # MODIFY: Add ICommandRouter mock
    SubmitCommandHandlerArchiveTests.cs  # MODIFY: Add ICommandRouter mock

tests/Hexalith.EventStore.IntegrationTests/
  Helpers/
    JwtAuthenticatedWebApplicationFactory.cs # MODIFY: Override actor registration with FakeAggregateActor
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for CommandRouter, SubmitCommandExtensions, AggregateActor
- `tests/Hexalith.EventStore.IntegrationTests/` -- Integration tests for end-to-end routing flow + serialization

**Test Patterns (established in Stories 1.6, 2.1-2.8):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- `WebApplicationFactory<CommandApiProgram>` for integration tests
- `TestJwtTokenGenerator` for creating JWT tokens with specific claims
- Primary constructors for DI injection
- Feature folder organization in test projects mirroring source
- NSubstitute for mocking `IActorProxyFactory`, `IAggregateActor`

**Unit Test Strategy for CommandRouter:**
Mock `IActorProxyFactory` using NSubstitute. Verify:
- Correct actor ID derivation from AggregateIdentity
- Correct CommandEnvelope creation from SubmitCommand
- Actor proxy invocation with correct parameters
- Exception propagation from actor

```csharp
var actorProxy = Substitute.For<IAggregateActor>();
actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
    .Returns(new CommandProcessingResult(true, CorrelationId: "test-correlation"));

var proxyFactory = Substitute.For<IActorProxyFactory>();
proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
    .Returns(actorProxy);

var router = new CommandRouter(proxyFactory, new TestLogger<CommandRouter>());
var result = await router.RouteCommandAsync(submitCommand);
result.Accepted.ShouldBeTrue();
```

**Unit Test Strategy for AggregateActor:**
Create actor using DAPR test utilities or direct construction with mock `ActorHost`. Verify:
- Stub returns Accepted=true
- CorrelationId preserved in result
- Structured logging (use TestLogger or verify ILogger calls)

**Integration Test Strategy:**
Register `FakeAggregateActor` in WebApplicationFactory. Override the `IActorProxyFactory` to return a `FakeAggregateActor` wrapper. Submit commands via POST `/api/v1/commands` and verify:
- FakeAggregateActor received the command
- All fields correctly mapped in CommandEnvelope
- 202 Accepted response unchanged
- Existing tests unbroken

**Alternative Integration Test Approach:** Since DAPR actor infrastructure is complex to mock in WebApplicationFactory, an alternative is to mock `ICommandRouter` at the integration test level:
```csharp
// In WebApplicationFactory
services.AddSingleton<ICommandRouter>(sp =>
{
    var mock = Substitute.For<ICommandRouter>();
    mock.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
        .Returns(new CommandProcessingResult(true));
    return mock;
});
```
This approach is simpler and verifies the handler-to-router wiring without needing DAPR actor infrastructure.

**Minimum Tests (~25):**

CommandRouter Unit Tests (8) -- in `CommandRouterTests.cs`:
1. `RouteCommandAsync_ValidCommand_CreatesCorrectActorId`
2. `RouteCommandAsync_ValidCommand_InvokesActorProxy`
3. `RouteCommandAsync_ValidCommand_PassesCommandEnvelope`
4. `RouteCommandAsync_ValidCommand_ReturnsActorResult`
5. `RouteCommandAsync_ActorThrows_PropagatesException`
6. `RouteCommandAsync_CommandEnvelope_HasCorrectCorrelationId`
7. `RouteCommandAsync_CommandEnvelope_HasCausationIdEqualToCorrelationId`
8. `RouteCommandAsync_NullCommand_ThrowsArgumentNullException`

SubmitCommandExtensions Unit Tests (4) -- in `SubmitCommandExtensionsTests.cs`:
9. `ToCommandEnvelope_ValidCommand_MapsAllFields`
10. `ToCommandEnvelope_NullExtensions_MapsAsNull`
11. `ToCommandEnvelope_CausationId_EqualsCorrelationId`
12. `ToCommandEnvelope_UserId_IsSystem`

AggregateActor Unit Tests (3) -- in `AggregateActorTests.cs`:
13. `ProcessCommandAsync_ValidCommand_ReturnsAccepted`
14. `ProcessCommandAsync_ValidCommand_ReturnsCorrelationId`
15. `ProcessCommandAsync_ValidCommand_LogsCommandReceipt`

SubmitCommandHandler Updated Tests (2) -- in existing test files:
16. `Handle_ValidCommand_RoutesToActor`
17. `Handle_RouterThrows_PropagatesException`

Serialization Tests (3) -- in `CommandEnvelopeSerializationTests.cs`:
18. `CommandEnvelope_JsonRoundtrip_PreservesAllFields`
19. `CommandEnvelope_ByteArrayPayload_SerializesAsBase64`
20. `CommandEnvelope_NullExtensions_RoundtripsCorrectly`

Integration Tests (5+) -- in `CommandRoutingIntegrationTests.cs`:
21. `PostCommands_ValidCommand_RoutesToActor`
22. `PostCommands_ValidCommand_Returns202Accepted`
23. `PostCommands_ValidCommand_ActorReceivesCorrectFields`
24. `PostCommands_ActorThrows_Returns500ProblemDetails`
25. `PostCommands_ExistingAuthTests_StillPass` (regression check)

**Current test count:** ~346 test methods across 45 test files (all epics through Stories 2.6-2.8). Story 3.1 adds ~25 new tests, bringing estimated total to ~371.

### Previous Story Intelligence

**From Story 2.8 (Optimistic Concurrency Conflict Handling):**
- `ConcurrencyConflictException` in Server/Commands/ -- will be thrown by actor code in Story 3.7 when ETag mismatch detected
- `ConcurrencyConflictExceptionHandler` with `FindConcurrencyConflict` helper that unwraps DAPR `ActorMethodInvocationException` wrapping
- **Cross-reference:** Story 2.8 H9 notes that real DAPR actor exception wrapping cannot be tested until Epic 3. When implementing Story 3.1, note that actor-thrown exceptions may be wrapped by DAPR proxy
- IExceptionHandler chain order: Validation -> Authorization -> ConcurrencyConflict -> Global

**From Story 2.7 (Command Replay Endpoint):**
- `ICommandArchiveStore` abstraction + implementations (DAPR + InMemory)
- `ArchivedCommandExtensions` with `ToArchivedCommand`/`ToSubmitCommand` factory methods -- follow same extension method pattern for `SubmitCommandExtensions`
- `catch (OperationCanceledException) { throw; }` pattern for advisory writes
- Advisory write pattern: try/catch, log Warning, continue (rule #12) -- applies to status/archive writes but NOT actor invocation

**From Story 2.6 (Command Status Tracking & Query Endpoint):**
- `ICommandStatusStore` + `CommandStatusOptions` with TtlSeconds and StateStoreName
- `DaprClient` registered via `AddDaprClient()` in Program.cs (DaprClient is already available for actor infrastructure)
- First advisory write pattern established

**From Story 2.5 (Endpoint Authorization & Command Rejection):**
- `AuthorizationBehavior` in MediatR pipeline -- verifies tenant/domain/command-type claims
- `HttpContext.Items["RequestTenantId"]` for passing tenant to error handlers
- JWT tenant claims flow through pipeline -- will be important for Story 3.3 actor-level tenant validation

**From Story 2.3 (MediatR Pipeline & Logging Behavior):**
- `EventStoreActivitySources.CommandApi` ActivitySource -- consider adding `EventStoreActivitySources.Actor` in Server package for actor-level tracing
- Correlation ID access: `HttpContext.Items["CorrelationId"]`

**Key Patterns (mandatory for all new code):**
- Primary constructors for DI: `public class Foo(IDep dep) : Base`
- Records for immutable data
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)
- Feature folder organization
- `namespace Hexalith.EventStore.{Project}.{Feature};`
- Registration via `Add*` extension methods (rule #10)

### Git Intelligence

**Recent commit patterns (last 5 merged):**
- `Stories 2.4 & 2.5: JWT Authentication & Endpoint Authorization (#24)` -- multi-story PRs are acceptable
- `Story 2.3: MediatR Pipeline & Logging Behavior + Story planning`
- `Story 2.2: Command Validation & RFC 7807 Error Responses`
- `Story 2.1: CommandApi Host & Minimal Endpoint Scaffolding + Story 2.2 context`
- `Story 1.6: Contracts Unit Tests (Tier 1) (#19)`

**Patterns observed:**
- Stories implemented sequentially in dedicated feature branches
- PR titles follow `Story X.Y: Description (#PR)` format
- Multiple stories sometimes bundled in single PRs
- Clean merge commits from pull requests

### Latency Design Note

**NFR1 compliance (50ms p99 for command submission):** In Story 3.1, the actor stub returns instantly, so the handler awaiting the actor call has zero latency impact. In later stories (3.7+) when the actor persists events, the actor method execution time adds to the HTTP response latency. If this exceeds the 50ms budget, the pattern must change to fire-and-forget (the handler fires the actor call without awaiting, returning 202 immediately). This decision is deferred to Story 3.7 where it can be measured with real persistence overhead.

### DAPR Actor Placement Note

Actor IDs derived from `AggregateIdentity.ActorId` (format `tenant:domain:aggregateId`) use colon separators. DAPR actor placement uses consistent hashing on the actor ID string. Colons are valid characters in DAPR actor IDs (they are opaque strings to the placement service). No character escaping needed.

### Project Structure Notes

**Alignment with Architecture:**
- `IAggregateActor` and `AggregateActor` in `Server/Actors/` per architecture directory structure
- `CommandRouter` in `Server/Commands/` alongside other command-processing components
- `AddEventStoreServer()` in `Server/Configuration/` per enforcement rule #10
- `FakeAggregateActor` in `Testing/Fakes/` alongside `InMemoryCommandStatusStore`, `InMemoryCommandArchiveStore`
- Test files mirror source structure in feature folders

**Dependency Graph:**
```
CommandApi -> Server -> Contracts
CommandApi/Program.cs -> Server/Configuration/ServiceCollectionExtensions (AddEventStoreServer)
Server/Commands/CommandRouter -> Dapr.Actors.Client (IActorProxyFactory)
Server/Actors/IAggregateActor -> Dapr.Actors (IActor)
Server/Actors/AggregateActor -> Dapr.Actors.Runtime (Actor, ActorHost)
Server/Commands/SubmitCommandExtensions -> Contracts (CommandEnvelope)
Testing/Fakes/FakeAggregateActor -> Server (IAggregateActor)
Tests: Server.Tests -> Server (unit testing CommandRouter, AggregateActor)
Tests: IntegrationTests -> CommandApi (via WebApplicationFactory)
```

**Package Dependency Boundaries (unchanged):**
```
Contracts (zero deps) <- Server (+ Dapr.Actors, Dapr.Client) <- CommandApi (+ Dapr.AspNetCore)
Testing -> Contracts + Server
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 3.1: Command Router & Actor Activation]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns - Actor Processing Pipeline]
- [Source: _bmad-output/planning-artifacts/architecture.md#AggregateActor as Thin Orchestrator]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure - Server/Actors/, Server/Commands/]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rules #5, #6, #9, #10, #12]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1: Event Storage Strategy - Actor-level ACID]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Flow - CommandRouter -> Actor activation]
- [Source: _bmad-output/planning-artifacts/architecture.md#Dependency Injection Registration Order]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR1: Command submission <50ms p99]
- [Source: _bmad-output/planning-artifacts/prd.md#FR3 - Command routing to aggregate actor]
- [Source: _bmad-output/planning-artifacts/prd.md#FR26 - Canonical identity tuple]
- [Source: _bmad-output/implementation-artifacts/2-8-optimistic-concurrency-conflict-handling.md]
- [Source: _bmad-output/implementation-artifacts/2-7-command-replay-endpoint.md]
- [Source: https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-actors/dotnet-actors-howto/ - DAPR .NET Actors SDK]
- [Source: https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-actors/ - DAPR Actors .NET SDK overview]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

None

### Completion Notes List

- All 6 prerequisites verified (Task 0): existing tests pass (413), all required types exist, DAPR packages present
- Created IAggregateActor interface, AggregateActor stub, CommandProcessingResult record in Server/Actors/ (Tasks 1-2)
- Created ICommandRouter, CommandRouter, SubmitCommandExtensions in Server/Commands/ (Tasks 3-4)
- Modified SubmitCommandHandler to call CommandRouter after advisory status/archive writes (Task 5)
- Created AddEventStoreServer() DI extension in Server/Configuration/ (Task 6)
- Updated Program.cs with AddEventStoreServer() and MapActorsHandlers() (Task 7)
- Task 8: Chose pattern where AddEventStoreServer() is called separately from AddCommandApi() in Program.cs, keeping clean separation between API and Server concerns
- Created FakeAggregateActor, FakeCommandRouter, TestServiceOverrides in Testing/Fakes/ (Tasks 13.1-13.2)
- Updated 4 integration test WebApplicationFactory instances to override ICommandRouter with FakeCommandRouter
- All 447 tests pass (34 new tests added): 8 CommandRouter + 4 SubmitCommandExtensions + 3 AggregateActor + 2 SubmitCommandHandler routing + 3 serialization + 5 integration routing + existing 413 tests with ICommandRouter mock updates
- Zero regressions confirmed across all test projects
- Code review fixes applied (448 tests pass after +1 new test):
  - H1: Removed catch-all in FakeCommandRouter that silently swallowed conversion exceptions
  - H2: Added cancellationToken.ThrowIfCancellationRequested() in CommandRouter before proxy creation
  - M1: Changed CommandRouter logging from Information to Debug (handler already logs at Information)
  - M2: Changed AddEventStoreServer() to use TryAddSingleton for idempotent registration
  - M3: Wrapped integration test actor-throws scenario in try/finally for reliable cleanup
  - M4: Added ServiceCollectionExtensionsTests.cs to File List
  - L1: Deleted spurious `nul` file from repo root
  - L2: Added ToCommandEnvelope_NullCommand_ThrowsArgumentNullException test

### Change Log

- 2026-02-14: Story 3.1 implementation complete -- Command Router & Actor Activation
- 2026-02-14: Code review fixes -- 8 issues fixed (2 HIGH, 4 MEDIUM, 2 LOW)

### File List

**New files:**
- src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs
- src/Hexalith.EventStore.Server/Actors/AggregateActor.cs
- src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs
- src/Hexalith.EventStore.Server/Commands/ICommandRouter.cs
- src/Hexalith.EventStore.Server/Commands/CommandRouter.cs
- src/Hexalith.EventStore.Server/Commands/SubmitCommandExtensions.cs
- src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs
- src/Hexalith.EventStore.Testing/Fakes/FakeCommandRouter.cs
- src/Hexalith.EventStore.Testing/Fakes/TestServiceOverrides.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/CommandRouterTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandExtensionsTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerRoutingTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/CommandRoutingIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/Serialization/CommandEnvelopeSerializationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/ServiceCollectionExtensionsTests.cs

**Modified files:**
- src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs
- src/Hexalith.EventStore.CommandApi/Program.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerStatusTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerArchiveTests.cs
- tests/Hexalith.EventStore.IntegrationTests/Helpers/JwtAuthenticatedWebApplicationFactory.cs
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/ConcurrencyConflictIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/AuthorizationIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/LoggingBehaviorIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/CommandApi/JwtAuthenticationIntegrationTests.cs
