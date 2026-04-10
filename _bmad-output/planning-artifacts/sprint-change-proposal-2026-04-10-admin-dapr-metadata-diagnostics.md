# Sprint Change Proposal — Admin UI DAPR Metadata Diagnostics

**Date:** 2026-04-10
**Author:** Scrum Master (bmad-correct-course workflow)
**Requested by:** Jerome
**Trigger:** User observation during normal `aspire run` session
**Scope classification:** Minor (direct adjustment, one fix story)
**Mode:** Incremental refinement (5 edits approved individually)

---

## Section 1 — Issue Summary

While running the Hexalith.EventStore AppHost under Aspire, the Admin UI shows
misleading placeholder values and error banners on DAPR-related pages:

- **DAPR Actor Inspector** (`/dapr/actors`):
  - `Total State Size: N/A`
  - `Active Instances: N/A` for every registered actor type
    (`AggregateActor`, `ETagActor`, `ProjectionActor`)
- **DAPR Subscriptions** (`/dapr/pubsub`):
  - Banner: *"EventStore sidecar unreachable. Subscription data unavailable.
    Configure 'AdminServer:EventStoreDaprHttpEndpoint' in appsettings."*

The messages suggest the user should set a configuration key, but investigation
showed that the key **is** being injected correctly by the Aspire extension
(`HexalithEventStoreExtensions.cs:86` — `AdminServer__EventStoreDaprHttpEndpoint`
set to `http://localhost:3501`). The actual failure mode is the HTTP call from
Admin.Server → EventStore DAPR sidecar silently returning an exception, which
the current code collapses into the same boolean `IsRemoteMetadataAvailable=false`
as the "not configured" case.

### Evidence

- User screenshots of both pages (provided in conversation).
- Code inspection of `DaprInfrastructureQueryService.cs` lines 171, 221, 288–397.
- Razor markup inspection of `DaprActors.razor:57–70` and `DaprPubSub.razor:128–130`.
- Aspire extension inspection of `HexalithEventStoreExtensions.cs:67–92`.

### Categorisation

**Issue type:** Misleading error messages + missing observability.
The underlying runtime failure (likely port 3501 conflict or silent DAPR fallback
per CommunityToolkit 13.0.0 limitation) is present in code *comments* but not
exposed to users or operators. This proposal addresses the diagnostic and UX
gap; the structural limitation (no dynamic port discovery) remains blocked on
CommunityToolkit upstream.

---

## Section 2 — Impact Analysis

### Epic Impact

- **Current work:** follow-up to story **19-2** (cross-sidecar metadata queries)
  per the code comment at `HexalithEventStoreExtensions.cs:63–66`. That story
  shipped the feature with fragile wiring and generic error surfaces.
- **No new epic required.** Fits inside the existing Admin UI epic as a bug-fix
  story.
- **No future epics invalidated.** No re-sequencing required.

### Story Impact

- **New story:** *"Fix misleading DAPR metadata error messages and add
  diagnostic logging"* (recommended ID: next available under the Admin UI epic).
- **In-flight stories:** none affected.
- **Completed stories:** none rolled back.

### Artifact Conflicts

| Artifact | Impact |
|---|---|
| PRD (`prd.md`) | None — no scope change |
| Epics (`epics.md`) | Add one story under Admin UI epic |
| Architecture (`architecture.md`) | None — wiring topology unchanged |
| UX specifications | Minor — two stat-card labels + two empty-state messages |
| `sprint-status.yaml` | Add new story entry with status `backlog` |

### Technical Impact

- **5 files touched**, all within the Admin slice + Aspire extension:
  1. `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
  2. `src/Hexalith.EventStore.Admin.Server/Contracts/*` (new enum)
  3. `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor`
  4. `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor`
  5. `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`
- **Test impact:** Tier-1 tests in `Hexalith.EventStore.Admin.Server.Tests` will
  need updating to match the new DTO shape (3-4 cases). No Tier-2/Tier-3 test
  changes expected.
- **No infrastructure changes.** No DAPR component YAML changes. No NuGet
  package dependencies added.
- **Breaking change surface:**
  - `AddHexalithEventStore` gains one optional parameter at the end (source-
    compatible for named-arg callers).
  - `HexalithEventStoreResources` record: deferred / optional item.
  - Admin DTO contract (`IsRemoteMetadataAvailable` → `RemoteMetadataStatus`
    enum + `RemoteEndpoint`). Internal to Admin.Server ↔ Admin.UI, not part
    of the 6 published NuGet packages (Contracts, Client, Server, SignalR,
    Testing, Aspire).

---

## Section 3 — Recommended Approach

### Selected path: **Option 1 — Direct Adjustment**

**Rationale:**

- **Effort:** Low. All 5 edits are local changes in a single architectural
  slice; no refactoring, no new abstractions, no dependency bumps.
- **Risk:** Low. No runtime behaviour changes for the success path; changes
  only affect the failure-path messaging and an optional Aspire parameter.
- **Timeline impact:** negligible — fits inside one sprint as a single story.
- **Alternatives rejected:**
  - *Option 2 (Rollback)*: story 19-2 shipped useful functionality; rollback
    would lose the feature for a UX polish issue.
  - *Option 3 (MVP review)*: no scope or requirement impact; MVP unchanged.

### Effort estimate

- **Implementation:** ~2–4 hours (a junior-to-mid dev, guided by the edit
  proposals below).
- **Tests:** ~1 hour (update 3–4 existing test cases).
- **Manual verification:** ~30 minutes (restart Aspire, re-check both pages).
- **Total:** ½ day of dev effort.

### Risk assessment

| Risk | Likelihood | Mitigation |
|---|---|---|
| Enum contract breaks an unseen consumer | Low | Admin DTOs are not in published NuGets; greppable within repo |
| Port parameter addition breaks Aspire tests | Low | New param is optional with unchanged default (3501) |
| UX copy regresses localisation | None | Project is English-only per bmm config |

---

## Section 4 — Detailed Change Proposals

All five edits below were refined incrementally with the user and approved
individually. They form a single cohesive fix story.

### Edit 1 — Observability in `DaprInfrastructureQueryService.cs`

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`

**A) Constructor — add startup Info log** stating the resolved endpoint:

```csharp
_logger.LogInformation(
    "DaprInfrastructureQueryService initialized. EventStoreDaprHttpEndpoint={Endpoint}",
    string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
        ? "<not configured — remote sidecar metadata disabled>"
        : _options.EventStoreDaprHttpEndpoint);
```

**B) Branch-level Debug log** (~lines 171, 327) before skipping a null-endpoint
remote call:

```csharp
if (string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint))
{
    _logger.LogDebug(
        "Skipping remote EventStore sidecar metadata query: endpoint not configured.");
}
else
{
    // existing remote-fetch branch
}
```

**C) Enrich exception logs** (lines 221, 392) with exception type and a
port-conflict hint:

```csharp
catch (Exception ex)
{
    _logger.LogWarning(
        ex,
        "Remote DAPR sidecar metadata unavailable at {Endpoint}. " +
        "ExceptionType={ExceptionType}. Check whether DAPR sidecar for " +
        "'eventstore' is running on that port (port conflicts on 3501 " +
        "cause silent fallback).",
        _options.EventStoreDaprHttpEndpoint,
        ex.GetType().Name);
}
```

**Rationale:** Right now the silent fallback is invisible in logs; admins see
"N/A" in the UI with zero breadcrumbs. Info at startup + Debug on silent
skip + enriched Warning on exception lets Jerome distinguish
*not-configured* vs *port-conflict* vs *access-control-denied* purely from
Admin.Server logs.

---

### Edit 2 — Distinct `Unreachable` state on the server

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
+ Admin contracts

**A) New enum** (new file in Admin.Server.Contracts or equivalent):

```csharp
public enum RemoteMetadataStatus
{
    /// <summary>No remote endpoint configured; only local sidecar queried.</summary>
    NotConfigured,

    /// <summary>Remote endpoint configured and successfully queried.</summary>
    Available,

    /// <summary>Remote endpoint configured but query failed (exception caught).</summary>
    Unreachable,
}
```

**B) DTO updates** — replace `bool IsRemoteMetadataAvailable` with the enum
and expose the attempted endpoint:

```csharp
public sealed record ActorRuntimeInfo(
    // ... existing members ...
    RemoteMetadataStatus RemoteMetadataStatus,
    string? RemoteEndpoint);

public sealed record PubSubOverview(
    // ... existing members ...
    RemoteMetadataStatus RemoteMetadataStatus,
    string? RemoteEndpoint);
```

**C) Service population**:

```csharp
var status = string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
    ? RemoteMetadataStatus.NotConfigured
    : remoteFetchSucceeded
        ? RemoteMetadataStatus.Available
        : RemoteMetadataStatus.Unreachable;

return new ActorRuntimeInfo(
    /* existing fields */,
    RemoteMetadataStatus: status,
    RemoteEndpoint: _options.EventStoreDaprHttpEndpoint);
```

(Same pattern in `GetPubSubOverviewAsync`.)

**Rationale:** The UI currently cannot distinguish "endpoint not set" from
"endpoint set but unreachable". This is why both surface the misleading
"configure the endpoint" text. A three-state enum closes that gap.

**Breaking-change note:** internal to Admin slice; not exported in any of the
6 published NuGet packages.

---

### Edit 3 — Accurate UI error messages

**Files:**
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor` (~lines 67-70)
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor` (~lines 128-130)

**A) `DaprActors.razor`** — three-way switch replacing the single empty state:

```razor
@if (!_isLoading && _runtimeInfo is not null && _runtimeInfo.ActorTypes.Count == 0)
{
    @switch (_runtimeInfo.RemoteMetadataStatus)
    {
        case RemoteMetadataStatus.NotConfigured:
            <EmptyState Title="No actor types found"
                        Description="Remote EventStore sidecar metadata is disabled.
                                     Set 'AdminServer:EventStoreDaprHttpEndpoint' in appsettings
                                     (under Aspire, this is wired automatically)." />
            break;

        case RemoteMetadataStatus.Unreachable:
            <EmptyState Title="EventStore sidecar unreachable"
                        Description="@($"Attempted to query {_runtimeInfo.RemoteEndpoint}/v1.0/metadata but the call failed.
                                        Verify the EventStore DAPR sidecar is running on that port
                                        (port conflicts on 3501 cause silent fallback).
                                        Check Admin server logs for the exception details.")" />
            break;

        case RemoteMetadataStatus.Available:
            <EmptyState Title="No actor types registered"
                        Description="The EventStore sidecar is reachable but reports no actor types." />
            break;
    }
}
```

**B) `DaprPubSub.razor`** — same pattern for the subscriptions banner:

```razor
@if (_overview is not null)
{
    @switch (_overview.RemoteMetadataStatus)
    {
        case RemoteMetadataStatus.NotConfigured:
            <MessageBar Intent="MessageBarIntent.Info" Title="Remote metadata disabled">
                Subscription data is only available when
                'AdminServer:EventStoreDaprHttpEndpoint' is configured.
                Under Aspire this is wired automatically.
            </MessageBar>
            break;

        case RemoteMetadataStatus.Unreachable:
            <MessageBar Intent="MessageBarIntent.Warning" Title="EventStore sidecar unreachable">
                @($"Attempted to query {_overview.RemoteEndpoint}/v1.0/subscribe but the call failed.
                   Verify the EventStore DAPR sidecar is running. Check Admin server logs for details.")
            </MessageBar>
            break;
    }
}
```

**Rationale:** each state gets the correct guidance. `Unreachable` uses
`Warning` intent (not `Info`) to signal an actual malfunction. The attempted
URL is the single most actionable diagnostic datum.

---

### Edit 4 — Clarify `Total State Size` stat card

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor` (~lines 57-62)

**Change:**

```razor
<FluentGridItem xs="6" sm="6" md="4">
    <StatCard Label="Inspected Actor State Size"
              Value="@(_inspectedState is not null
                         ? TimeFormatHelper.FormatBytes(_inspectedState.TotalSizeBytes)
                         : "—")"
              Severity="neutral"
              Title="@(_inspectedState is not null
                         ? "Total state size of the currently inspected actor"
                         : "Select an actor instance below to inspect its state size")" />
</FluentGridItem>
```

**Key changes:**

1. **Label:** `Total State Size` → `Inspected Actor State Size` (makes per-actor
   nature explicit).
2. **Empty value:** `"N/A"` → `"—"` (em dash; convention for *awaiting input*).
3. **Dynamic tooltip** explains the affordance when empty.

**Rationale:** pure UX affordance fix; zero behaviour change, zero new code
paths, zero test impact. Addresses the root cause of the user's original
confusion — *"I see N/A everywhere and can't tell which ones are bugs vs.
intentional"*.

---

### Edit 5 — Harden port 3501 wiring in Aspire extension

**File:** `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` (~lines 32-92)

**A) Make the port a parameter of the extension method:**

```csharp
public static HexalithEventStoreResources AddHexalithEventStore(
    this IDistributedApplicationBuilder builder,
    IResourceBuilder<ProjectResource> eventStore,
    IResourceBuilder<ProjectResource> adminServer,
    IResourceBuilder<ProjectResource>? adminUI = null,
    string? eventStoreDaprConfigPath = null,
    string? adminServerDaprConfigPath = null,
    int eventStoreDaprHttpPort = 3501)
{
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentNullException.ThrowIfNull(eventStore);
    ArgumentNullException.ThrowIfNull(adminServer);
    ArgumentOutOfRangeException.ThrowIfLessThan(eventStoreDaprHttpPort, 1024);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(eventStoreDaprHttpPort, 65535);

    // ... existing code ...

    _ = eventStore
        .WithDaprSidecar(sidecar => sidecar
            .WithOptions(new DaprSidecarOptions {
                AppId = "eventstore",
                DaprHttpPort = eventStoreDaprHttpPort,
                Config = eventStoreDaprConfigPath,
            })
            .WithReference(stateStore)
            .WithReference(pubSub));

    _ = adminServer
        .WithReference(eventStore)
        .WithEnvironment(
            "AdminServer__EventStoreDaprHttpEndpoint",
            $"http://localhost:{eventStoreDaprHttpPort}")
        // ... rest unchanged ...
}
```

Remove the existing `const int EventStoreDaprHttpPort = 3501;` line.

**B) Update the XML doc** with the trade-off:

```csharp
/// <param name="eventStoreDaprHttpPort">
/// DAPR HTTP port for the EventStore sidecar. Defaults to 3501.
/// This port MUST be free on the host at startup — DAPR does not error on
/// port conflicts, it silently binds to a different port, which breaks
/// cross-sidecar metadata queries from Admin.Server. Override this
/// parameter if 3501 is occupied (e.g., by a prior daprd process or
/// another DAPR app). Diagnostic: on Windows, run
/// `netstat -ano | findstr :3501` before `aspire run`.
/// </param>
```

**C) Optional — expose the resolved port via `HexalithEventStoreResources`:**

```csharp
public sealed record HexalithEventStoreResources(
    IResourceBuilder<IDaprComponentResource> StateStore,
    IResourceBuilder<IDaprComponentResource> PubSub,
    IResourceBuilder<ProjectResource> EventStore,
    IResourceBuilder<ProjectResource> AdminServer,
    IResourceBuilder<ProjectResource>? AdminUI,
    int EventStoreDaprHttpPort);
```

> **Optional / can defer**: this is a constructor-breaking change to the record.
> Defer if any external AppHost consumes `HexalithEventStoreResources`.

**Rationale:** makes the fragile port behaviour discoverable via XML docs and
IntelliSense, and gives operators an escape hatch when 3501 is occupied.

**Explicitly out of scope:**

- Dynamic port discovery via `IResourceWithEndpoints` — blocked on
  CommunityToolkit 13.0.0 upstream (see existing code comment lines 63-66).
  Revisit when CommunityToolkit ships that interface for
  `IDaprSidecarResource`.
- Host-level port-availability pre-check — adds TOCTOU races and
  platform-specific code; better left to operator-level diagnostics.

---

## Section 5 — Implementation Handoff

### Change scope classification

**Minor** — implementable directly by the dev team, no backlog
reorganisation required.

### Handoff recipients

| Role | Responsibility |
|---|---|
| **Dev team (Amelia)** | Implement all 5 edits in a single story; update Admin.Server.Tests to match new DTO shape; manual verification via `aspire run` → check both DAPR pages |
| **Scrum Master** | Create the new story under the Admin UI epic; update `sprint-status.yaml` |
| **Jerome (reviewer)** | After implementation: re-run the restart procedure (flush Redis → build → `aspire run`); confirm Admin server logs show the new startup Info log with resolved endpoint; confirm both DAPR pages show actionable messages when unreachable |

### Success criteria

1. **Observability**: On Admin.Server startup, logs contain an Info-level line
   naming the resolved `EventStoreDaprHttpEndpoint` (or stating
   `<not configured>`).
2. **Failure-mode distinction**: On the Admin UI, DAPR Actor Inspector and
   DAPR Subscriptions pages show **different** messages for
   *not-configured* vs. *unreachable* states.
3. **Actionable diagnostics**: When the state is *unreachable*, the UI displays
   the attempted URL (`http://localhost:3501` by default).
4. **UX polish**: `Total State Size` stat card renders `"—"` with an
   explanatory tooltip ("Select an actor instance below…") instead of `"N/A"`.
5. **Port override**: `AddHexalithEventStore` accepts an
   `eventStoreDaprHttpPort` optional parameter; default 3501; XML doc warns
   about port conflicts.
6. **Tests green**: all Tier-1 tests in `Hexalith.EventStore.Admin.Server.Tests`
   pass; all existing Tier-2/Tier-3 tests remain unaffected.

### Open investigation item (post-implementation)

After the observability changes land, Jerome should re-run `aspire run` and
check the Admin server logs for the new `LogWarning` on cross-sidecar metadata
failure. The enriched log will show the exception type — which will finally
identify whether the underlying runtime cause is:

- A **port conflict** (`SocketException`) on 3501 → workaround via the new
  `eventStoreDaprHttpPort` parameter, investigate what's holding 3501.
- An **access control** block (`HttpRequestException` with 403) → review
  `eventStoreDaprConfigPath` to allow metadata invocations from
  `eventstore-admin`.
- A **timeout** during early boot → add `WaitFor(eventStore)` on the Admin
  server resource (a subsequent small fix if needed).

This diagnostic cannot be completed as part of the current story because the
enriched logs don't exist yet. Filing it as a follow-up investigation task is
sufficient; no additional proposal is required at this stage.

---

## Checklist Completion Summary

| Section | Status |
|---|---|
| 1. Understand trigger and context | ✅ Done |
| 2. Epic impact assessment | ✅ Done (no epic changes required) |
| 3. Artifact conflict analysis | ✅ Done |
| 4. Path forward evaluation | ✅ Done — Option 1 selected |
| 5. Sprint Change Proposal components | ✅ Done |
| 6. Final review and handoff | Pending user approval of this document |

---

**Approval required:** Jerome to review this complete proposal and confirm
yes/no/revise.
