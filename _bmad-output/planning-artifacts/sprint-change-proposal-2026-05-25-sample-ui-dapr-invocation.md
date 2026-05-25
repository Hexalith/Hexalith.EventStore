> **⚠️ CORRECTION (2026-05-25, post-implementation):** This proposal's *root-cause analysis was wrong.*
> The migration below was implemented in full (AppHost sidecar, `DaprAppIdHandler`, DI re-point, ACL) and the
> `TaskCanceledException` **persisted**. Live tracing proved the real cause was a **DAPR actor placement
> outage** in the local environment (sidecar log: `error reading server preface: EOF`), which made *every*
> actor invocation hang to its timeout — not a UI sidecar/service-discovery problem. `docker restart
> dapr_placement dapr_scheduler` took the same query from 20.3 s → 0.2 s. The transport migration is
> **retained** (reasonable consistency with the rest of the topology) but it did **not** fix the trigger.
> See `sprint-change-proposal-2026-05-25-dapr-placement-actor-outage.md` for the verified root cause and the
> server-side placement health-check guardrail added to prevent silent recurrence.

# Sprint Change Proposal — Migrate Sample BlazorUI → EventStore to DAPR Service Invocation

- **Date:** 2026-05-25
- **Author:** Jérôme Piquot (with Claude Code)
- **Trigger:** `TaskCanceledException` on Sample BlazorUI → EventStore query calls + decision to standardize inter-service transport on DAPR
- **Scope classification:** Moderate (touches AppHost topology, DAPR access-control, and BlazorUI DI)
- **Related:** `sprint-change-proposal-2026-05-25.md` (the parallel Admin.UI → Admin.Server / D13 migration). This proposal applies the same pattern to the sample UI path.

---

## Section 1 — Issue Summary

The Sample BlazorUI Blazor Server app (`sample-blazor-ui`) queries the EventStore REST API through a named `HttpClient` ("EventStoreApi") whose `BaseAddress` is the logical host `https://eventstore`, resolved via **.NET Aspire service discovery** (`Microsoft.Extensions.ServiceDiscovery`). A runtime `TaskCanceledException` (inner `IOException`/`SocketException`, "operation aborted") surfaces from `EventStoreApiAuthorizationHandler.SendAsync` (line 13) via `ResolvingHttpDelegatingHandler` → `ResilienceHandler`.

This is the **same failure mode and the same root architecture gap** that was just addressed for the Admin.UI → Admin.Server hop (D13, supersedes ADR-P4). The Admin.UI's `DaprAppIdHandler` doc comment names the exact symptom: *"without [a sidecar], every API call would otherwise surface as an opaque request timeout during page interactions."*

**Two facts emerged during analysis:**

1. **`sample-blazor-ui` has no DAPR sidecar.** In `AppHost/Program.cs` (lines 117–123) it is added with `.WithReference(eventStore)` + `.WaitFor(eventStore)` (Aspire service discovery) but **no** `.WithDaprSidecar(...)`. Every other backend hop in the topology (`sample`, `tenants`, `eventstore-admin-ui` → its target) already uses DAPR service invocation.
2. **The exception is an orchestration/resolution failure**, not a transport-design fault. The most likely cause is running the UI without the orchestrated `eventstore` endpoint resolvable (or the HTTPS dev-cert + resilience timeout path on the Aspire-resolved endpoint).

The team has decided to route Sample BlazorUI → EventStore through DAPR service invocation for transport consistency with the rest of the topology (mTLS, ACL, centralized resiliency), mirroring the Admin.UI D13 change.

> ⚠️ **Caveat carried into this proposal:** migrating to DAPR makes the BlazorUI **hard-depend on its `daprd` sidecar**. Standalone `dotnet run` will fail *faster*, not succeed — the topology must be launched via `aspire run`. This is the correct fix, because the original timeout stems from resolving `eventstore` outside the orchestrated environment.

### Terminology clarification

The triggering request says "sample service," but the BlazorUI calls the **`eventstore`** app's `/api/v1/queries` endpoint. The `sample` app-id is a domain (command/event) service with **zero infrastructure access** and exposes no query API for the UI (see `accesscontrol.sample.yaml`: only `eventstore` may POST to it). Therefore the DAPR service-invocation **target app-id is `eventstore`**.

---

## Section 2 — Impact Analysis

### Current transport map

| Path | Transport | Notes |
|------|-----------|-------|
| Domain services (`sample`, `tenants`) → EventStore | **DAPR service invocation** | `dapr-caller-app-id`, `DaprInternal` auth, ACL |
| Admin.UI → Admin.Server | **DAPR service invocation** | D13 (`DaprAppIdHandler`, app-id `eventstore-admin`) |
| **Sample BlazorUI → EventStore** | **HTTP + Aspire SD** | ← target of this change |
| Sample BlazorUI → EventStore SignalR hub | **HTTP (resolved URL)** | `HubConnectionBuilder`; bypasses `HttpClientFactory` — **unchanged** |

### Affected artifacts

- **Code (BlazorUI):**
  - `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` — the "EventStoreApi" `HttpClient` registration (lines 50–54).
  - **New:** `samples/Hexalith.EventStore.Sample.BlazorUI/Services/DaprAppIdHandler.cs` — mirror of the Admin.UI handler.
  - `CounterQueryService.cs` is **untouched** — it uses a relative path (`/api/v1/queries`) against the named client, which DAPR forwards verbatim.
  - The `.csproj` is **untouched** — `DaprAppIdHandler` is a plain `DelegatingHandler` that only sets HTTP headers; **no Dapr SDK package is required.**
- **AppHost topology:** `src/Hexalith.EventStore.AppHost/Program.cs` (lines 117–123) — `sample-blazor-ui` currently has *no* sidecar; add one.
- **DAPR config:** `DaprComponents/accesscontrol.yaml` (governs callers of `eventstore`) — add `sample-blazor-ui` as an allowed caller (for production deny-by-default parity; optional under dev `defaultAction: allow`).
- **Architecture docs:** `architecture.md` transport map and the AppHost line-112 inline comment ("consumes EventStore via Aspire service discovery") — update to reflect DAPR invocation.

### Out of scope / unchanged

- **SignalR** (`EventStore__SignalR__HubUrl`): the hub connection is built with `HubConnectionBuilder`, which does not use `HttpClientFactory` and cannot carry the `dapr-app-id` header through the standard pipeline. It already receives the AppHost-resolved endpoint URL via env var and is **left as-is**. The triggering exception is on the query path only.
- The 0-infra `sample` domain sidecar and its ACL.

---

## Section 3 — Recommended Approach

**Option 1 — Direct Adjustment.** Implement the change as a focused fix using the proven Admin.UI D13 pattern.

- **Effort:** Low–Medium.
- **Risk:** Low — the pattern is already implemented, reviewed, and running for the Admin.UI path.
- **Rationale:** Restores transport consistency across the entire topology; removes the last Aspire-SD HTTP hop on the sample path; eliminates the opaque-timeout failure mode. No epic re-scope, no PRD/MVP impact.

Options 2 (Rollback) and 3 (MVP Review) are **not viable / not applicable** — there is nothing to roll back and no scope to reduce.

---

## Section 4 — Detailed Change Proposals

### 4.1 — AppHost: add the sidecar

**File:** `src/Hexalith.EventStore.AppHost/Program.cs` (lines 117–123)

**OLD:**
```csharp
IResourceBuilder<ProjectResource> blazorUi = builder.AddProject<Projects.Hexalith_EventStore_Sample_BlazorUI>("sample-blazor-ui")
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithExternalHttpEndpoints()
    // SignalR HubConnectionBuilder bypasses Aspire service discovery (it doesn't use HttpClientFactory),
    // so we must pass the resolved eventstore endpoint URL explicitly.
    .WithEnvironment("EventStore__SignalR__HubUrl", ReferenceExpression.Create($"{eventStoreHttps}/hubs/projection-changes"));
```

**NEW:**
```csharp
// Sample BlazorUI invokes EventStore via DAPR service invocation (mirrors Admin.UI D13):
// the sidecar tags outbound calls with `dapr-app-id: eventstore`. It references no
// state store / pub/sub component — service invocation only, zero infrastructure access
// (same isolation rationale as the sample and admin-ui sidecars).
IResourceBuilder<ProjectResource> blazorUi = builder.AddProject<Projects.Hexalith_EventStore_Sample_BlazorUI>("sample-blazor-ui")
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithExternalHttpEndpoints()
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions {
            AppId = "sample-blazor-ui",
        }))
    // SignalR HubConnectionBuilder bypasses Aspire service discovery (it doesn't use HttpClientFactory),
    // so we must pass the resolved eventstore endpoint URL explicitly.
    .WithEnvironment("EventStore__SignalR__HubUrl", ReferenceExpression.Create($"{eventStoreHttps}/hubs/projection-changes"));
```

**Rationale:** A sidecar is required for the UI to perform DAPR service invocation; no component references keeps infrastructure access at zero.

### 4.2 — New `DaprAppIdHandler` for the sample UI

**File (new):** `samples/Hexalith.EventStore.Sample.BlazorUI/Services/DaprAppIdHandler.cs`

```csharp
namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Routes outgoing EventStore query requests through the local DAPR sidecar using
/// <c>dapr-app-id</c> header-based service invocation, mirroring the Admin.UI D13 pattern.
/// </summary>
/// <remarks>
/// The named "EventStoreApi" client's <c>BaseAddress</c> points at this app's own DAPR
/// sidecar (<c>http://localhost:{DAPR_HTTP_PORT}</c>), so the request path is preserved
/// verbatim and DAPR forwards it to the target app named by <paramref name="appId"/>.
/// Header-based routing is used instead of <c>Dapr.AspNetCore.InvocationHandler</c>
/// (host-name rewriting) because the latter collides with the global
/// <c>AddServiceDiscovery()</c> default applied by ServiceDefaults. The
/// <c>Authorization</c> bearer header set by <see cref="EventStoreApiAuthorizationHandler"/>
/// is forwarded by DAPR unchanged, so EventStore's JWT/RBAC/tenant enforcement is preserved.
/// </remarks>
/// <param name="appId">The DAPR application id of the invocation target (<c>eventstore</c>).</param>
/// <param name="apiToken">Optional DAPR API token (<c>DAPR_API_TOKEN</c>), sent as <c>dapr-api-token</c> when set.</param>
public sealed class DaprAppIdHandler(string appId, string? apiToken) : DelegatingHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.Headers.TryAddWithoutValidation("dapr-app-id", appId);
        if (!string.IsNullOrEmpty(apiToken)) {
            _ = request.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
```

**Rationale:** Identical, proven pattern; no Dapr SDK dependency.

### 4.3 — BlazorUI: re-point the EventStoreApi client

**File:** `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` (lines 50–54)

**OLD:**
```csharp
// HttpClient for querying EventStore via Aspire service discovery
builder.Services.AddHttpClient("EventStoreApi", client =>
    client.BaseAddress = new Uri(builder.Configuration["EventStore:EventStoreUrl"]
        ?? "https://eventstore"))
    .AddHttpMessageHandler<EventStoreApiAuthorizationHandler>();
```

**NEW:**
```csharp
// HttpClient for querying EventStore via DAPR service invocation (mirrors Admin.UI D13).
// BaseAddress targets THIS app's DAPR sidecar; DaprAppIdHandler tags the request with the
// target app-id so DAPR routes it to eventstore. A literal localhost base address keeps the
// global AddServiceDiscovery() default a no-op. DAPR forwards the Authorization bearer header
// unchanged, so EventStore JWT/RBAC/tenant enforcement is preserved.
string daprHttpEndpoint = builder.Configuration["DAPR_HTTP_ENDPOINT"]
    ?? $"http://localhost:{builder.Configuration["DAPR_HTTP_PORT"] ?? "3500"}";
string? daprApiToken = builder.Configuration["DAPR_API_TOKEN"];
builder.Services.AddHttpClient("EventStoreApi", client => {
    client.BaseAddress = new Uri(daprHttpEndpoint);
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .AddHttpMessageHandler<EventStoreApiAuthorizationHandler>()
    .AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore", daprApiToken));
```

**Rationale:** Routes queries through the sidecar; auth handler and relative request paths unchanged. `CounterQueryService` requires no edits.

### 4.4 — DAPR access control: allow the new caller

**File:** `src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml`

Add, alongside the existing `eventstore-admin` policy:

```yaml
      # sample-blazor-ui: sample query UI -- invokes eventstore /api/v1/queries
      - appId: sample-blazor-ui
        defaultAction: deny
        trustDomain: "public"
        namespace: "default"
        operations:
          - name: /**
            httpVerb: ['GET', 'POST']
            action: allow
```

Also extend the header comment's "Allowed callers" list. **Rationale:** explicit allow for production deny-by-default parity; functionally optional under the dev `defaultAction: allow`.

### 4.5 — Documentation

- `architecture.md`: update the inter-service transport map so `sample-blazor-ui → eventstore` reads **DAPR service invocation**.
- `AppHost/Program.cs` line 112 comment: "consumes EventStore via Aspire service discovery (Story 18-6)" → "invokes EventStore via DAPR service invocation".

---

## Section 5 — Implementation Handoff

- **Scope:** Moderate (AppHost topology + DAPR ACL + UI DI), but implementation is straightforward — a proven, recently-reviewed pattern.
- **Recipient:** Developer agent (direct implementation).
- **Deliverables:** the five edits in Section 4.
- **Success criteria:**
  1. `aspire run` brings up `sample-blazor-ui` with a `daprd` sidecar.
  2. The BlazorUI counter page loads counter status with **no `TaskCanceledException`**; queries reach `eventstore` via the sidecar (`dapr-app-id: eventstore`).
  3. Bearer auth still enforced end-to-end (EventStore returns 401 without a valid token).
  4. SignalR "changed" signals still arrive (unchanged path).
  5. Solution builds with `TreatWarningsAsErrors` (no warnings); Tier 1 tests green.

### Optional follow-up (not blocking)

Mirror Admin.UI's `RequireDaprSidecar()` fail-fast guard in the BlazorUI entry point so a missing sidecar surfaces a clear startup error instead of a request-time timeout. Recommended for consistency; can be a separate small story.
