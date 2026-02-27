[← Back to Hexalith.EventStore](../../README.md)

# Quickstart

Clone the repository, run the sample application with .NET Aspire, send a command to the Counter domain, and watch the resulting event flow through the system. This guide is for .NET developers and takes about 10 minutes with prerequisites installed.

> **Prerequisites:** [Prerequisites](prerequisites.md)

## What You'll Build

The sample application includes a Counter domain service that processes three commands: `IncrementCounter`, `DecrementCounter`, and `ResetCounter`. Each command produces a corresponding event (`CounterIncremented`, `CounterDecremented`, `CounterReset`) that updates the `CounterState`.

Your domain logic is a pure function — you implement a `CounterProcessor` that receives a command and the current state, then returns events. The platform handles everything else: routing, persistence, snapshots, and pub/sub delivery.

DAPR handles message delivery and state storage — you don't write infrastructure code.

## Clone and Run

Clone the repository and navigate into the project directory:

```bash
$ git clone https://github.com/Hexalith/Hexalith.EventStore.git
$ cd Hexalith.EventStore
```

Start the Aspire AppHost, which launches the CommandAPI, the sample domain service, Redis, and Keycloak:

```bash
$ dotnet run --project src/Hexalith.EventStore.AppHost
```

> **Note:** The first run takes longer than usual because .NET restores NuGet packages and Docker pulls container images for Redis, Keycloak, and the DAPR sidecar.

Once the application starts, the terminal output includes the Aspire dashboard URL. Open it in your browser — the dashboard shows all running services and their endpoints.

## Send a Command

### Get an access token

The CommandAPI requires a JWT token for authentication. Keycloak runs as part of the Aspire topology and provides test accounts preconfigured for local development.

Open a new terminal and request a token using the `admin-user` test account. Keycloak runs on port 8180 as part of the Aspire topology:

```bash
$ curl -s -X POST http://localhost:8180/realms/hexalith/protocol/openid-connect/token \
  -d "grant_type=password" \
  -d "client_id=hexalith-eventstore" \
  -d "username=admin-user" \
  -d "password=admin-pass"
```

> **Note:** The `\` line continuation works in bash, Zsh, and PowerShell 7+. On Windows PowerShell 5.x, use the single-line alternative below.

The response contains an `access_token` field. Copy its value — you need it in the next step.

> **Tip:** On Windows PowerShell 5.x, use:

```powershell
$ Invoke-RestMethod -Method Post -Uri "http://localhost:8180/realms/hexalith/protocol/openid-connect/token" -Body @{grant_type="password"; client_id="hexalith-eventstore"; username="admin-user"; password="admin-pass"} | Select-Object -ExpandProperty access_token
```

### Submit the command via Swagger UI

Find the `commandapi` service in the Aspire dashboard and open its URL. Append `/swagger` to the URL to open the Swagger UI.

1. Click the **Authorize** button at the top of the page
2. In the **Value** field, paste the `access_token` you copied earlier — do not include the `Bearer ` prefix, Swagger adds it automatically
3. Click **Authorize**, then **Close**

Expand the **POST /api/v1/commands** endpoint, click **Try it out**, and use this request body:

```json
{
  "tenant": "tenant-a",
  "domain": "counter",
  "aggregateId": "counter-1",
  "commandType": "IncrementCounter",
  "payload": {}
}
```

Click **Execute**. The API returns `202 Accepted` with a response containing the correlation ID:

```json
{
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

The response also includes a `Location` header containing the full URL to the status endpoint. To check whether the command has been processed, open that URL from the Aspire dashboard's `commandapi` service — it follows the pattern `/api/v1/commands/status/{correlationId}` — or query it directly with curl using the same Bearer token.

## See the Event

Go back to the Aspire dashboard and open the **Traces** tab. You should see a trace for the command you just sent. The trace shows the full processing pipeline:

1. CommandAPI received the HTTP request
2. The command was routed to the aggregate actor
3. The actor invoked the `CounterProcessor` domain service
4. The domain service produced a `CounterIncremented` event
5. The event was persisted to the state store
6. The event was published to the pub/sub topic

The correlation ID from your command response links the request to all downstream processing. Click the trace to expand it and see timing for each stage.

You can also check the **Structured Logs** tab in the Aspire dashboard to see detailed log entries from each service.

## What Happened

Here is what happened when you sent that command:

1. You sent an `IncrementCounter` command to the CommandAPI via REST
2. The CommandAPI validated the request and authenticated your JWT token
3. DAPR activated a `CounterProcessor` actor for `tenant-a|counter|counter-1`
4. The actor loaded the current `CounterState` (empty for a new aggregate) and called the pure function
5. The `CounterProcessor` produced a `CounterIncremented` event
6. The event was persisted to the state store and published to the event topic

You wrote zero infrastructure code — DAPR handled state, messaging, and actor lifecycle.

## Next Steps

- **Next:** [Build Your First Domain Service](first-domain-service.md) — create your own domain from scratch
- **Related:** [Architecture Overview](../concepts/architecture-overview.md) — understand the design decisions behind the system
- **Related:** [Choose the Right Tool](../concepts/choose-the-right-tool.md) — compare Hexalith with alternatives
- **Related:** [Prerequisites](prerequisites.md) — review tool setup details
