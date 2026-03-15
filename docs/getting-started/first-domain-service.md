[← Back to Hexalith.EventStore](../../README.md)

# Build Your First Domain Service

In this tutorial you build an Inventory domain service that tracks product stock quantities via `AddStock` and `RemoveStock` commands. You define commands, events, state, and an aggregate, then send commands and observe events — following the same pattern as the Counter domain from the quickstart. This takes about 45 minutes.

> **Prerequisites:** [Quickstart](quickstart.md) — you should have the sample application running before starting this tutorial

## What You'll Build

The Inventory domain is a simple product inventory service. Two commands go in (`AddStock`, `RemoveStock`), two success events come out (`StockAdded`, `StockRemoved`), and one rejection event signals a business rule violation (`InsufficientStock`). A single `InventoryState` class tracks the current quantity, and an `InventoryAggregate` ties everything together with pure function handlers.

Both success and rejection events are persisted to the event stream and published to pub/sub. The key distinction is state evolution: success events are applied to state, while rejection events are audit events that are not applied to `InventoryState`.

The pure function contract is the same one you saw in the Counter domain: `Handle(Command, State?) → DomainResult`. Your aggregate receives a command and the current state, then returns success events, a rejection, or a no-op.

By the end of this tutorial, you will have:

- Created a complete Inventory domain (commands, events, state, aggregate)
- Registered it with EventStore automatically — zero configuration
- Sent commands with data payloads and observed events in the Aspire dashboard
- Triggered a business rule rejection
- Compared backend configurations to understand infrastructure portability

## Create the Domain Types

### Create the Project Structure

Create the following folders inside the sample project, alongside the existing `Counter/` folder:

```text
samples/Hexalith.EventStore.Sample/
  Counter/          ← existing (from quickstart)
  Inventory/
    Commands/
    Events/
    State/
```

The existing `Hexalith.EventStore.Sample.csproj` already has the correct project references (`Hexalith.EventStore.Client`, `Hexalith.EventStore.ServiceDefaults`, `Dapr.AspNetCore`). No `.csproj` changes are needed.

### Commands

Create `samples/Hexalith.EventStore.Sample/Inventory/Commands/AddStock.cs`:

```csharp
namespace Hexalith.EventStore.Sample.Inventory.Commands;

/// <summary>
/// Command to add stock to the inventory.
/// </summary>
public sealed record AddStock(int Quantity);
```

Create `samples/Hexalith.EventStore.Sample/Inventory/Commands/RemoveStock.cs`:

```csharp
namespace Hexalith.EventStore.Sample.Inventory.Commands;

/// <summary>
/// Command to remove stock from the inventory.
/// </summary>
public sealed record RemoveStock(int Quantity);
```

Commands are plain records with a `Quantity` property. Unlike the Counter commands (which carry no data), Inventory commands include the amount to add or remove.

### Events

Create `samples/Hexalith.EventStore.Sample/Inventory/Events/StockAdded.cs`:

```csharp
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Sample.Inventory.Events;

/// <summary>
/// Event indicating stock was added to the inventory.
/// </summary>
public sealed record StockAdded(int Quantity) : IEventPayload;
```

Create `samples/Hexalith.EventStore.Sample/Inventory/Events/StockRemoved.cs`:

```csharp
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Sample.Inventory.Events;

/// <summary>
/// Event indicating stock was removed from the inventory.
/// </summary>
public sealed record StockRemoved(int Quantity) : IEventPayload;
```

Create `samples/Hexalith.EventStore.Sample/Inventory/Events/InsufficientStock.cs`:

```csharp
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Sample.Inventory.Events;

/// <summary>
/// Rejection event indicating the removal was refused due to insufficient stock.
/// </summary>
public sealed record InsufficientStock(int Quantity) : IRejectionEvent;
```

Events carry data (like `Quantity`) because they are the system's permanent record. During state reconstruction, the framework replays these events through the `Apply` methods to rebuild the current state. This is why events must contain enough information to reconstruct the change they represent.

`StockAdded` and `StockRemoved` implement `IEventPayload` — they are persisted and published. `InsufficientStock` implements `IRejectionEvent` — it records that the command was refused and is also persisted and published for traceability.

### State

Create `samples/Hexalith.EventStore.Sample/Inventory/State/InventoryState.cs`:

```csharp
using Hexalith.EventStore.Sample.Inventory.Events;

namespace Hexalith.EventStore.Sample.Inventory.State;

/// <summary>
/// Aggregate state for the Inventory domain. Tracks the current stock quantity
/// and applies events to reconstruct state from event replay.
/// </summary>
public sealed class InventoryState
{
    /// <summary>Gets the current stock quantity.</summary>
    public int Quantity { get; private set; }

    /// <summary>Applies a stock added event.</summary>
    public void Apply(StockAdded e) => Quantity += e.Quantity;

    /// <summary>Applies a stock removed event.</summary>
    public void Apply(StockRemoved e) => Quantity -= e.Quantity;
}
```

The state class has `Apply` methods for success events only. There is no `Apply` method for `InsufficientStock` because rejection events do not represent a state transition in this domain model. The Counter domain follows the same pattern — `CounterState` has `Apply` methods for `CounterIncremented`, `CounterDecremented`, and `CounterReset`, but none for `CounterCannotGoNegative`.

### Aggregate

Create `samples/Hexalith.EventStore.Sample/Inventory/InventoryAggregate.cs`:

```csharp
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Inventory.Commands;
using Hexalith.EventStore.Sample.Inventory.Events;
using Hexalith.EventStore.Sample.Inventory.State;

namespace Hexalith.EventStore.Sample.Inventory;

/// <summary>
/// Inventory aggregate using the fluent EventStoreAggregate API.
/// Pure function handlers: Handle(Command, State?) → DomainResult.
/// </summary>
public sealed class InventoryAggregate : EventStoreAggregate<InventoryState>
{
    public static DomainResult Handle(AddStock command, InventoryState? state)
  {
    if (command.Quantity <= 0)
    {
      return DomainResult.NoOp();
    }

    return DomainResult.Success(new IEventPayload[] { new StockAdded(command.Quantity) });
  }

    public static DomainResult Handle(RemoveStock command, InventoryState? state)
    {
    if (command.Quantity <= 0)
    {
      return DomainResult.NoOp();
    }

        if ((state?.Quantity ?? 0) < command.Quantity)
        {
            return DomainResult.Rejection(new IRejectionEvent[] { new InsufficientStock(command.Quantity) });
        }

        return DomainResult.Success(new IEventPayload[] { new StockRemoved(command.Quantity) });
    }
}
```

The `static` keyword is critical — it enforces pure functions with no instance state mutation. Both handlers guard against non-positive quantities (`<= 0`) by returning `DomainResult.NoOp()`. The `RemoveStock` handler additionally checks whether enough stock is available and returns a rejection if not.

The tutorial handlers also guard against invalid non-positive quantities (`<= 0`) by returning `DomainResult.NoOp()`. This prevents accidental negative-flow behavior (for example, `RemoveStock(-1)` increasing inventory) while keeping the first tutorial focused on one rejection event type.

## Register and Run

No registration code is needed. `AddEventStore()` in `Program.cs` auto-discovers all `EventStoreAggregate<>` subclasses in the assembly at startup. The `InventoryAggregate` is found automatically by the assembly scanner. The sample project already references `Hexalith.EventStore.Client`, which contains the `EventStoreAggregate<T>` base class and the assembly scanner. Any public aggregate class in this project is discovered automatically — no `.csproj` changes, no `Program.cs` changes.

The naming convention engine converts `InventoryAggregate` to domain name `inventory` (PascalCase to kebab-case, "Aggregate" suffix stripped).

> **Note:** In this tutorial, you add the Inventory domain to the existing sample project for simplicity. In a real-world application, each domain service would typically be a separate .NET project referencing the `Hexalith.EventStore.Client` NuGet package. The same `AddEventStore()` call discovers aggregates from whichever assembly it scans.

Restart the AppHost:

```bash
$ aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

### Verify Discovery

After the application starts, open the Aspire dashboard and navigate to the **Structured Logs** tab for the `sample` service. You should see a log entry confirming the `inventory` domain was discovered by the assembly scanner.

> **Tip:** If you do not see the `inventory` domain in the startup logs, check these common issues:
>
> - The class extends `EventStoreAggregate<InventoryState>` (not just `InventoryState` alone)
> - The class is `public` (internal classes are not discovered)
> - The namespace matches the folder structure (`Hexalith.EventStore.Sample.Inventory`)
> - The file is saved and the project compiles without errors (`dotnet build` should produce zero warnings)

## Send a Command

### Get an access token

The CommandAPI requires a JWT token. If you still have a token from the quickstart, you can reuse it — tokens are valid for 5 minutes. If it has expired, request a fresh one:

```bash
$ curl -s -X POST http://localhost:8180/realms/hexalith/protocol/openid-connect/token \
  -d "grant_type=password" \
  -d "client_id=hexalith-eventstore" \
  -d "username=admin-user" \
  -d "password=admin-pass"
```

The response contains an `access_token` field. Copy its value.

> **Tip:** On Windows PowerShell 5.x, use:

```powershell
$ Invoke-RestMethod -Method Post -Uri "http://localhost:8180/realms/hexalith/protocol/openid-connect/token" -Body @{grant_type="password"; client_id="hexalith-eventstore"; username="admin-user"; password="admin-pass"} | Select-Object -ExpandProperty access_token
```

### Add stock

Open the Swagger UI (append `/swagger` to the `commandapi` URL in the Aspire dashboard), authorize with your token, and submit this request body to **POST /api/v1/commands**:

```json
{
    "tenant": "tenant-a",
    "domain": "inventory",
    "aggregateId": "product-1",
    "commandType": "AddStock",
    "payload": { "quantity": 10 }
}
```

`commandType` uses the short class name (`AddStock`), not the fully-qualified namespace. The `domain` field (`inventory`) tells the system which aggregate handles the command. The `payload` property names must match the C# record constructor parameters (case-insensitive). Unlike the Counter commands in the quickstart (which sent an empty `{}` payload), Inventory commands carry data — this is the first time you send a non-empty payload.

The API returns `202 Accepted` with a correlation ID:

```json
{
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

> **Tip:** If the command returns 404 or "unknown domain", verify that `"domain": "inventory"` is lowercase with no "Aggregate" suffix.

### Remove stock

Send a `RemoveStock` command to take 3 units out:

```json
{
    "tenant": "tenant-a",
    "domain": "inventory",
    "aggregateId": "product-1",
    "commandType": "RemoveStock",
    "payload": { "quantity": 3 }
}
```

After `AddStock(10)` and `RemoveStock(3)`, the aggregate's state has 7 units. Send a command that exceeds the available stock to see how Hexalith handles business rule violations:

```json
{
    "tenant": "tenant-a",
    "domain": "inventory",
    "aggregateId": "product-1",
    "commandType": "RemoveStock",
    "payload": { "quantity": 100 }
}
```

This command still returns `202 Accepted`. The API accepted and processed your command — the rejection is the _result_ of that processing, not a failure to process. The rejection is visible in the command status endpoint (`/api/v1/commands/status/{correlationId}`) and in the Aspire dashboard traces.

> **Tips:**
>
> - If `RemoveStock(100)` unexpectedly succeeds, verify the comparison in `Handle(RemoveStock ...)` is exactly `(state?.Quantity ?? 0) < command.Quantity` and that this branch returns `DomainResult.Rejection(...)`.
> - If the command returns 400 with a deserialization error, verify you are using lowercase `"quantity"` in the JSON payload to match the C# `Quantity` parameter.

## See the Events

Open the Aspire dashboard **Traces** tab. You should see traces for each command you sent. The traces show the same high-level 6-stage processing pipeline as the Counter domain, but now with `InventoryAggregate` as the domain service:

1. CommandAPI received the HTTP request
2. The command was routed to the aggregate actor
3. The actor invoked the `InventoryAggregate` domain service
4. The domain service produced a `StockAdded` (or `StockRemoved`) event
5. The event was persisted to the state store
6. The event was published to the pub/sub topic

For the rejected `RemoveStock(100)` command, the trace shows the `InventoryAggregate` returning a `DomainResult.Rejection` with an `InsufficientStock` rejection event. This is not an error — it is a business rule enforcement. The submission endpoint still returns `202 Accepted`; rejection details are surfaced through command status/traces, and the rejection event is also recorded/published for observability and audit history.

After the successful `AddStock(10)` and `RemoveStock(3)`, the `InventoryState.Quantity` is 7. The next command against `product-1` will receive this reconstructed state.

> **Tip:** If state looks wrong after multiple commands, verify the state operator in `Apply(StockRemoved e)` is subtraction (`Quantity -= e.Quantity`) and not addition.

## What Happened

Here is what happened when you sent those commands:

1. You sent an `AddStock` command with `{ "quantity": 10 }` to the CommandAPI via REST
2. The CommandAPI validated the request and authenticated your JWT token
3. DAPR activated an `InventoryAggregate` actor for `tenant-a:inventory:product-1` — `AddEventStore()` auto-discovered the aggregate at startup
4. The actor loaded the current `InventoryState` (empty for a new aggregate, so `Quantity` defaults to 0) and called the typed `Handle` method
5. The `InventoryAggregate` produced a `StockAdded` event with `Quantity = 10`
6. The event was persisted to the state store and published to the event topic
7. For `RemoveStock(3)`, the actor loaded state (now `Quantity = 10`), confirmed `10 >= 3`, and produced a `StockRemoved` event
8. For `RemoveStock(100)`, the actor loaded state (now `Quantity = 7`), found `7 < 100`, and returned an `InsufficientStock` rejection event that was recorded in the event stream

You wrote zero infrastructure code — DAPR handled state, messaging, and actor lifecycle.

## Swap the Backend

> **Note:** This section is a walkthrough — you compare YAML configurations side by side, but you do not run PostgreSQL locally. A hands-on backend swap exercise is coming in a future update.

DAPR component YAML files define which backend stores state and handles pub/sub. The application code does not change — only the YAML config changes.

The local development state store uses Redis. Here is the key line from `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`:

```yaml
spec:
    type: state.redis
    metadata:
        - name: redisHost
          value: "{env:REDIS_HOST|localhost:6379}"
```

The PostgreSQL alternative at `deploy/dapr/statestore-postgresql.yaml` replaces the type and connection metadata:

```yaml
spec:
    type: state.postgresql
    metadata:
        - name: connectionString
          value: "{env:POSTGRES_CONNECTION_STRING}"
```

The same principle applies to pub/sub — `deploy/dapr/pubsub-rabbitmq.yaml` replaces Redis pub/sub with RabbitMQ using the same component name and scoping rules.

The Aspire AppHost manages DAPR component files for local development. The key insight: your domain code — the `InventoryAggregate`, commands, events, state — is identical regardless of backend.

This is the infrastructure portability that DAPR provides — your domain logic is a pure function that does not know or care which database stores its events. See [Architecture Overview](../concepts/architecture-overview.md) for how this works at the system level.

## What You Learned

- **Pure function contract:** `Handle(Command, State?) → DomainResult` — the same pattern for every domain service
- **Auto-discovery:** `AddEventStore()` scans the assembly and finds your aggregate automatically — no registration code
- **Naming conventions:** `InventoryAggregate` becomes domain name `inventory` via convention engine (strip suffix, lowercase)
- **Three result types:** `DomainResult.Success` (events persisted and published), `DomainResult.Rejection` (rejection event returned, persisted, and published), `DomainResult.NoOp` (nothing happened)
- **State reconstruction:** `Apply` methods on state replay persisted events to rebuild current state — rejection events are excluded
- **Infrastructure portability:** Swap Redis for PostgreSQL (or any DAPR-supported backend) by changing YAML — zero code changes

You wrote zero infrastructure code. No Redis imports, no DAPR SDK references, no database connection strings. The Inventory domain service follows the same zero-infrastructure principle as the Counter.

### Ready to Build Your Own?

Here is the transferable recipe:

1. Create a new .NET project (or use an existing one)
2. Add the `Hexalith.EventStore.Client` NuGet package
3. Define your commands (sealed records), events (sealed records implementing `IEventPayload` / `IRejectionEvent`), state (class with `Apply` methods), and aggregate (extending `EventStoreAggregate<TState>` with static `Handle` methods)
4. Call `AddEventStore()` in your `Program.cs`

That is it. The convention engine discovers your aggregate, derives the domain name, and wires everything up.

## Next Steps

- **Next:** [Architecture Overview](../concepts/architecture-overview.md) — understand the design decisions behind the system
- **Related:** [Quickstart](quickstart.md), [Command Lifecycle](../concepts/command-lifecycle.md), [Identity Scheme](../concepts/identity-scheme.md), [Query & Projection API Reference](../reference/query-api.md)
