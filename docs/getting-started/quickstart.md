[← Back to Hexalith.EventStore](../../README.md)

# Quickstart

Clone the repository, run the sample application with .NET Aspire, send a command to the Counter domain, and watch the resulting event flow through the system. This guide is for .NET developers and takes about 10 minutes with prerequisites installed.

> **Prerequisites:** [Prerequisites](prerequisites.md)

## What You'll Build

The sample application includes a Counter domain service that processes three commands: `IncrementCounter`, `DecrementCounter`, and `ResetCounter`. Each command produces a corresponding event (`CounterIncremented`, `CounterDecremented`, `CounterReset`) that updates the `CounterState`. The same local topology also exposes a query endpoint and an optional real-time projection refresh path used by the sample Blazor UI.

Your domain logic lives in a `CounterAggregate` class that extends `EventStoreAggregate<CounterState>`. Each command gets a typed `Handle` method that receives the command and current state, then returns events. State is reconstructed via `Apply` methods on `CounterState`. The platform handles everything else: routing, persistence, snapshots, and pub/sub delivery.

DAPR handles message delivery and state storage — you don't write infrastructure code.

## Clone and Run

Clone the repository and navigate into the project directory:

```bash
$ git clone https://github.com/Hexalith/Hexalith.EventStore.git
$ cd Hexalith.EventStore
$ git submodule update --init references/Hexalith.Builds references/Hexalith.Tenants
```

The root Tenants submodule is a local runtime prerequisite. A plain `aspire run` resolves its
domain-service and external API host projects by path and fails before starting any resources when
either project is unavailable. It does not initialize submodules automatically.

Start the Aspire AppHost, which launches the CommandAPI, Tenants domain and API services, the sample
domain service, Redis, and Keycloak:

```bash
$ aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

> **Note:** The first run takes longer than usual because .NET restores NuGet packages and Docker pulls container images for Redis, Keycloak, and the DAPR sidecar.
>
Local Keycloak identities and passwords are generated for each AppHost run, so persistent Keycloak
container reuse is intentionally unavailable. Generated credentials stay inside Aspire resource
environment wiring; the quickstart neither prints them nor asks you to configure matching values.

Once the application starts, the terminal output includes the Aspire dashboard URL. Open it in your browser — the dashboard shows all running services and their endpoints.

## Send a Command

The public command API requires authentication. For the clean-clone local flow, use the sample UI:
Aspire supplies that resource with its generated tenant identity and the UI obtains and caches its
token without displaying the password or signing key.

1. In the Aspire dashboard, open the `sample-blazor-ui` resource.
2. Open any of the three counter pattern pages.
3. In **Send Commands**, select **Increment**.
4. Confirm that the page shows the green `Last command: increment-counter` acknowledgement.

The UI generates a fresh sortable message identifier and submits the typed `IncrementCounter`
command for `tenant-a`, domain `counter`, aggregate `counter-1`. The acknowledgement means that the
authenticated command submission was accepted; use the next section to observe downstream work.

## See the Event

Go back to the Aspire dashboard and open the **Traces** tab. You should see a trace for the command you just sent. The trace shows the full processing pipeline:

1. CommandAPI received the HTTP request
2. The command was routed to the aggregate actor
3. The actor invoked the `CounterAggregate` domain service
4. The domain service produced a `CounterIncremented` event
5. The event was persisted to the state store
6. The event was published to the pub/sub topic

Open the matching command trace to see timing for each stage and correlate it with the UI submission
time.

You can also check the **Structured Logs** tab in the Aspire dashboard to see detailed log entries from each service.

If you open the sample Blazor UI from the dashboard, you'll see the complementary read side in action: it uses the query API to fetch projection data and can subscribe to projection change notifications to refresh when new events land. The UI includes three concrete refresh patterns: persistent notification, silent reload, and selective component refresh.

The green command feedback shown by the sample UI means the HTTP command submission was accepted. It does not prove final domain processing or projection completion. Use the command status endpoint, Aspire traces, logs, and query results when you need end-to-end proof. See [Sample Blazor UI](../guides/sample-blazor-ui.md) for the pattern trade-offs and smoke-test evidence format.

## What Happened

Here is what happened when you sent that command:

1. You sent an `IncrementCounter` command to the CommandAPI via REST
2. The CommandAPI validated the request and authenticated your JWT token
3. DAPR activated a `CounterAggregate` actor for `tenant-a|counter|counter-1` — `AddEventStore()` auto-discovered the aggregate at startup
4. The actor loaded the current `CounterState` (empty for a new aggregate) and called the typed `Handle` method
5. The `CounterAggregate` produced a `CounterIncremented` event
6. The event was persisted to the state store and published to the event topic

You wrote zero infrastructure code — DAPR handled state, messaging, and actor lifecycle.

## Next Steps

- **Next:** [Build Your First Domain Service](first-domain-service.md) — create your own domain from scratch
- **Related:** [Query & Projection API Reference](../reference/query-api.md) — inspect the read-model and real-time endpoints exposed by the same sample
- **Related:** [Sample Blazor UI](../guides/sample-blazor-ui.md) — understand the refresh patterns and command feedback semantics
- **Related:** [Architecture Overview](../concepts/architecture-overview.md) — understand the design decisions behind the system
- **Related:** [Choose the Right Tool](../concepts/choose-the-right-tool.md) — compare Hexalith with alternatives
- **Related:** [Prerequisites](prerequisites.md) — review tool setup details
