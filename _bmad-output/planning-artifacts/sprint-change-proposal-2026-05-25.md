# Sprint Change Proposal — Migrate Admin.UI → Admin.Server to DAPR Service Invocation

- **Date:** 2026-05-25
- **Author:** Jérôme Piquot (with Claude Code)
- **Trigger:** `TaskCanceledException` on Admin.UI → Admin.Server HTTP calls + decision to standardize inter-service transport on DAPR
- **Scope classification:** Moderate (reverses ADR-P4; touches AppHost topology, DAPR access-control, and Admin.UI DI)

---

## Section 1 — Issue Summary

The Admin.UI Blazor Server app calls the Admin.Server REST API through a named `HttpClient` ("AdminApi") that resolves the logical host `https://eventstore-admin` via **.NET Aspire service discovery** (`Microsoft.Extensions.ServiceDiscovery`). A runtime `TaskCanceledException` (inner `IOException`/`SocketException` "operation aborted") surfaced from `AdminApiAuthorizationHandler.SendAsync` via `ResolvingHttpDelegatingHandler`.

Two distinct facts emerged during analysis:

1. **The exception is an orchestration/resolution failure**, not a transport-design fault. The most likely cause is running Admin.UI outside the Aspire AppHost (the `services__eventstore-admin__*` env vars are then absent, so `eventstore-admin` cannot resolve and the request times out).
2. **The Admin.UI → Admin.Server path is the *only* inter-service hop still on plain HTTP**, and it is so **by a documented architecture decision (ADR-P4 deviation)**. Every other backend hop already uses DAPR service invocation.

The team has decided to **reverse ADR-P4** and route Admin.UI → Admin.Server through DAPR service invocation for transport consistency, mTLS, ACL, and centralized resiliency.

> ⚠️ **Important caveat carried into this proposal:** migrating to DAPR does **not** fix the original `TaskCanceledException` if its root cause is "Admin.UI launched without its orchestrator". After migration the Admin.UI gains a **hard dependency on its `daprd` sidecar** — standalone `dotnet run` will fail faster, not succeed. The orchestration fix (always launch via `aspire run`, or provide a `dapr run` profile) is still required and is tracked as a follow-up action below.

---

## Section 2 — Impact Analysis

### Current transport map

| Path | Transport | Notes |
|------|-----------|-------|
| Domain services (`sample`, `tenants`) → EventStore | **DAPR service invocation** | `dapr-caller-app-id`, `DaprInternal` auth, ACL |
| Admin.Server → EventStore sidecar metadata | **DAPR** | `AdminServer__EventStoreDaprHttpEndpoint=http://localhost:3501` |
| **Admin.UI → Admin.Server** | **HTTP + Aspire SD** | ← target of this change (ADR-P4 deviation) |

### Affected artifacts

- **Code (Admin.UI):** `AdminUIServiceExtensions.cs` (the "AdminApi" `HttpClient` registration), `Hexalith.EventStore.Admin.UI.csproj` (add DAPR package). The 15 `Admin*ApiClient.cs` classes are **untouched** — they use relative paths against the named client.
- **AppHost topology:** `HexalithEventStoreExtensions.cs` (Admin.UI currently has *no* sidecar — must add one).
- **DAPR config:** `DaprComponents/accesscontrol.eventstore-admin.yaml` (currently states "no other service should directly invoke eventstore-admin" — must allow caller `eventstore-admin-ui`).
- **Architecture docs:** ADR-P4 must be updated/superseded.
- **Tests:** `Hexalith.EventStore.Admin.UI.Tests` (any assertion on BaseAddress / service discovery) and the new `tests/Hexalith.EventStore.Admin.UI.E2E/` (must run with the sidecar; assert end-state per retro rule R2-A6).

### Auth impact — none required

`AdminApiAuthorizationHandler` attaches a user **bearer token**. DAPR service invocation **forwards the `Authorization` header** to the target app, so Admin.Server's JWT validation (and therefore RBAC + tenant scoping) keeps working unchanged. We deliberately keep user-JWT auth rather than switching to `DaprInternal` allow-list auth, because Admin operations are user-scoped.

---

## Section 3 — Recommended Approach

**Direct adjustment** — minimal, reversible change to the transport layer only, keeping all API-client code and the auth model intact.

Use a **`dapr-app-id` header + local-sidecar base address**, which is more robust than host-based routing here because the repo's global `ConfigureHttpClientDefaults` (in `ServiceDefaults`) applies `AddServiceDiscovery()` to *every* client. Pointing the client at `http://localhost:{DAPR_HTTP_PORT}` makes the Aspire service-discovery resolver a no-op (literal host, not a logical name), avoiding any handler-ordering conflict between the SD resolving handler and DAPR's invocation handler.

(Alternative considered: `Dapr.AspNetCore`'s `InvocationHandler` with base `http://eventstore-admin`. Rejected as primary because it relies on host-name rewriting that collides with the global service-discovery default; it would require per-client SD suppression that the API doesn't expose cleanly.)

**Effort:** ~0.5–1 day incl. tests. **Risk:** Medium (handler ordering + sidecar dependency). **Timeline:** 1 story.

---

## Section 4 — Detailed Change Proposals

### 4.1 `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj`

```xml
<ItemGroup>
  <PackageReference Include="Dapr.AspNetCore" />   <!-- version from Directory.Packages.props (1.17.9) -->
</ItemGroup>
```

### 4.2 New file: `src/Hexalith.EventStore.Admin.UI/Services/DaprAppIdHandler.cs`

```csharp
namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Routes the request through the local DAPR sidecar by tagging it with the
/// target application id. The named client's BaseAddress points at the sidecar
/// (http://localhost:{DAPR_HTTP_PORT}); DAPR forwards the full path + headers
/// (including Authorization) to the target app.
/// </summary>
public sealed class DaprAppIdHandler(string appId) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.TryAddWithoutValidation("dapr-app-id", appId);
        return base.SendAsync(request, cancellationToken);
    }
}
```

### 4.3 `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs` (lines ~82–88)

OLD:
```csharp
_ = builder.Services.AddHttpClient("AdminApi", client => {
    client.BaseAddress = new Uri(builder.Configuration["EventStore:AdminServer:BaseUrl"]
        ?? "https://eventstore-admin");
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .AddHttpMessageHandler<AdminApiAuthorizationHandler>();
```

NEW:
```csharp
// Route to Admin.Server via DAPR service invocation (supersedes ADR-P4).
// BaseAddress targets THIS app's DAPR sidecar; DaprAppIdHandler tags the
// request with the target app-id. Service discovery becomes a no-op on a
// literal localhost address, so it no longer conflicts with DAPR routing.
string daprHttpPort = builder.Configuration["DAPR_HTTP_PORT"] ?? "3500";
_ = builder.Services.AddHttpClient("AdminApi", client => {
    client.BaseAddress = new Uri($"http://localhost:{daprHttpPort}");
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .AddHttpMessageHandler<AdminApiAuthorizationHandler>()
    .AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore-admin"));
```

> Validation point: confirm the API-client relative paths (e.g. `/api/v1/streams`) map 1:1 to Admin.Server method paths — DAPR preserves the path after the sidecar host.

### 4.4 `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` (lines ~158–166)

OLD:
```csharp
if (adminUI is not null) {
    _ = adminUI
        .WithReference(adminServer)
        .WaitFor(adminServer)
        .WithExternalHttpEndpoints();
}
```

NEW:
```csharp
if (adminUI is not null) {
    _ = adminUI
        .WithReference(adminServer)
        .WaitFor(adminServer)
        .WithExternalHttpEndpoints()
        // Supersedes ADR-P4: Admin.UI now invokes Admin.Server via DAPR.
        // No state store / pub/sub references — service invocation only.
        .WithDaprSidecar(sidecar => sidecar
            .WithOptions(new DaprSidecarOptions {
                AppId = "eventstore-admin-ui",
            }));
}
```

Update the XML-doc comment on lines 158–160 ("Admin.UI does not use DAPR directly…") accordingly.

### 4.5 `src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.eventstore-admin.yaml`

Reverse the "no peer may invoke" stance and grant the UI caller (keep `defaultAction: allow` for self-hosted dev; tighten to `deny` + explicit policy for production overlays):

```yaml
spec:
  accessControl:
    defaultAction: allow   # self-hosted dev; production overlay should set deny
    trustDomain: "public"
    policies:
      - appId: eventstore-admin-ui
        defaultAction: allow
        trustDomain: "public"
        namespace: "default"
```

Replace the header comment block that asserts Admin.Server is "not a Dapr service target for peer workloads".

### 4.6 Architecture docs

Add an ADR superseding **ADR-P4**, recording: decision reversed, rationale (transport consistency, mTLS, ACL, central resiliency), and the retained user-JWT auth model.

### 4.7 Tests

- Update any `Admin.UI.Tests` assertion tied to the old BaseAddress / service discovery.
- `tests/Hexalith.EventStore.Admin.UI.E2E/` must run with the sidecar and assert Admin.Server end-state, not just HTTP 2xx (retro rule R2-A6).

---

## Section 5 — Implementation Handoff

- **Scope:** Moderate. Reverses an ADR and changes AppHost topology + DAPR ACL, but is contained to one transport path.
- **Route to:** Developer agent (implementation) + a doc update for the ADR (PO/architect sign-off on the ADR reversal).
- **Success criteria:**
  1. `aspire run` brings up an `eventstore-admin-ui` resource with a healthy `daprd` sidecar.
  2. Admin.UI pages load data through DAPR (verify the call traverses the sidecar — `dapr-app-id` header present, trace shows two sidecars).
  3. User JWT still flows; RBAC/tenant scoping unchanged.
  4. Tier-1 + Admin.UI tests green; E2E asserts end-state.
- **Follow-up action (separate, still required):** fix/standardize the **launch story** so Admin.UI is never run without its sidecar (always `aspire run`, or add a documented `dapr run` profile). This is what actually addresses the original `TaskCanceledException`.

---

## Resolved decisions (best practice) & implementation — 2026-05-25

1. **Production ACL posture** → Mirror the existing `accesscontrol.yaml` convention: keep top-level `defaultAction: allow` (self-hosted dev has no mTLS, so deny-by-default would block everything), add an **explicit per-appId policy** for `eventstore-admin-ui` (`deny` default + `/**` allow for GET/POST/PUT/DELETE), and document the production deny+mTLS flip in the header comment. *(Done.)*
2. **Resiliency overlap** → **Reversed from the initial proposal on evidence.** `DaprDomainServiceInvoker` already invokes DAPR through the **default** `HttpClient`, which carries the global `AddStandardResilienceHandler`. Stripping resilience only for the AdminApi client would be *inconsistent* with the established DAPR-invocation path. Decision: **keep the global defaults**; let DAPR `resiliency.yaml` own sidecar-level policy. *(No change — consistent.)*
3. **ADR handling** → ADRs are immutable; do not rewrite. ADR-P4 is about the *three-interface architecture* (unchanged), and the as-built UI/Server split made the transport an "ADR-P4 deviation". Recorded a **new ratified decision D13** in `architecture.md` and added a dated forward-reference note under ADR-P4. *(Done.)*

### What was implemented

| File | Change |
|------|--------|
| `Services/DaprAppIdHandler.cs` *(new)* | `DelegatingHandler` adding `dapr-app-id` (+ `dapr-api-token` when set) |
| `AdminUIServiceExtensions.cs` | AdminApi `BaseAddress` → sidecar (`DAPR_HTTP_ENDPOINT` / `http://localhost:{DAPR_HTTP_PORT}`), added `DaprAppIdHandler`; kept `AdminApiAuthorizationHandler` |
| `HexalithEventStoreExtensions.cs` | Added `eventstore-admin-ui` DAPR sidecar (no state/pubsub refs); updated comment |
| `accesscontrol.eventstore-admin.yaml` | Allow caller `eventstore-admin-ui`; rewrote header comment |
| `architecture.md` | Added D13; dated note under ADR-P4 |

> **No DAPR NuGet package added.** The `dapr-app-id` header approach needs only `System.Net.Http`. The official `Dapr.AspNetCore.InvocationHandler` was rejected because its host-name rewriting collides with the repo's global `AddServiceDiscovery()` default.

### Verification

- `dotnet build` AppHost chain (incl. Admin.UI + Aspire): **0 warnings, 0 errors**.
- Admin.UI unit tests: **827 pass**. One failure (`JsonViewerTests.JsonViewer_ShowsWarning_WhenJsonIsInvalid`) is **pre-existing on `main`** — reproduced with all changes reverted; unrelated to transport.

### Orchestration fix (the actual root cause of the original bug) — implemented

The original `TaskCanceledException` was a *launched-without-orchestration* failure that surfaced as an opaque mid-interaction timeout. Fixed by **fail-fast at startup**:

| File | Change |
|------|--------|
| `AdminUIServiceExtensions.cs` | New `RequireDaprSidecar()` — throws `InvalidOperationException` with actionable guidance (run via `aspire run`) when `DAPR_HTTP_ENDPOINT` / `DAPR_GRPC_ENDPOINT` / `DAPR_HTTP_PORT` / `DAPR_GRPC_PORT` are all unset. Mirrors the `DAPR_HTTP_PORT` discovery already used by `EventStore.Server` actor registration. |
| `Program.cs` | Calls `builder.RequireDaprSidecar()` — placed here (not in `AddAdminUI`) so test/E2E hosts that build the UI without a sidecar are unaffected. |
| `HostBootstrapTests.cs` | Updated to supply `DAPR_HTTP_PORT` via `WithWebHostBuilder(...UseSetting(...))`, modelling the new sidecar precondition. |

Both guard branches are now covered by the test suite (throws when unset; boots when set). Supported launch path is `aspire run` on the AppHost; a standalone `dapr run` profile was deliberately *not* added — it requires the rest of the topology (mDNS name resolution of `eventstore-admin`) to be up, so it offers little over `aspire run`.

### Still outstanding

- E2E (`tests/Hexalith.EventStore.Admin.UI.E2E/`) must run under the AppHost (with sidecars) and assert Admin.Server end-state (retro rule R2-A6). The current Playwright fixture builds the UI without a sidecar (page-render smoke only) and bypasses the guard by design.
- Pre-existing, unrelated: `JsonViewerTests.JsonViewer_ShowsWarning_WhenJsonIsInvalid` fails on `main` (reproduced with all changes reverted).
