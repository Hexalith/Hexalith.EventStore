# Sprint Change Proposal — Admin UI DAPR pages: subscriptions count + resiliency wiring

**Date:** 2026-04-10
**Author:** Jerome (driven via Correct Course workflow)
**Scope:** Minor — direct dev implementation
**Status:** Implemented and verified

---

## 1. Issue Summary

While reviewing the Admin UI DAPR pages, three observations were raised:

1. **`/dapr` — `Subscriptions: N/A`**: the Subscriptions stat card shows `N/A` and `HTTP endpoints: N/A` even when the EventStore sidecar is running and has active subscriptions. The user asked whether this is expected.
2. **`/dapr/resiliency` — "Resiliency configuration not available"**: every policy section is `N/A` and the page shows the empty-state message asking the operator to set `AdminServer:ResiliencyConfigPath` in `appsettings`. The user asked for this to be wired up so the page works out of the box.
3. **`/dapr/health-history`**: the user wanted verification that the page is actually complete.

### Evidence

- `/dapr` payload from `DaprInfrastructureQueryService.GetSidecarInfoAsync()` was hardcoding `0` for subscriptions and HTTP endpoints with the comment `// Subscriptions not exposed in SDK 1.16.1`. The DAPR `/v1.0/metadata` endpoint of the remote `eventstore` sidecar **does** expose both — the same code path is already used by `GetPubSubOverviewAsync` and `GetActorRuntimeInfoAsync`. The comment is obsolete.
- `appsettings.json` for `Hexalith.EventStore.Admin.Server.Host` does **not** set `AdminServer:ResiliencyConfigPath`. With no path, `GetResiliencySpecAsync` returns `DaprResiliencySpec.Unavailable` and the page renders the empty-state. The YAML file itself exists at `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml`.
- `/dapr/health-history` (`DaprHealthHistory.razor`, 719 lines) is fully implemented: time-range selection (1h/6h/24h/3d/7d), heatmap, per-component drill-down, status-transition log, uptime calculations, all null-checks. Backend wired through `DaprHealthQueryService.GetComponentHealthHistoryAsync` via `AdminHealthController`. Flag `HealthHistoryEnabled` defaults to `true`. **No gaps detected.**

### Diagnosis matrix

| Page | Symptom | Verdict |
|---|---|---|
| `/dapr` Subscriptions/HTTPEndpoints | `N/A` | **Bug** — backend hardcoded 0; remote metadata not queried |
| `/dapr/resiliency` empty-state | All `N/A` | **Config gap** — works as designed when path unset; no auto-wiring under Aspire |
| `/dapr/health-history` | OK | **Complete** — no change |

---

## 2. Impact Analysis

### Epic impact

- **Story 19.6 (Admin DAPR observability)** continuation. No new epic, no story renumbering, no resequencing.
- The `RemoteMetadataStatus` pattern introduced in 19.6 (already used by `DaprPubSubOverview` and `DaprActorRuntimeInfo`) is now propagated to `DaprSidecarInfo` for consistency.

### Story impact

- No new story file. Treated as a follow-up bug fix on the existing 19.x admin observability surface, consistent with the recent series of admin-UI fix PRs (sprint-change-proposal-2026-04-07, -04-08, -04-09 chains).

### Artifact conflicts

| Artifact | Impact |
|---|---|
| PRD | None — no requirement change |
| Architecture | None — no new component, no contract change beyond the additive `DaprSidecarInfo` fields |
| UX | None — same stat-card layout; only the rendering rule for "N/A" is refined |
| Sprint plan | None |

### Technical impact

Minor and localized:

- **Backend**: `DaprInfrastructureQueryService.GetSidecarInfoAsync()` now fetches the remote `eventstore` sidecar `/v1.0/metadata` to populate subscription + HTTP endpoint counts, and reports a `RemoteMetadataStatus`.
- **Contract**: `DaprSidecarInfo` gains two fields (`RemoteMetadataStatus`, `RemoteEndpoint`) — additive on a positional record. All callers updated.
- **UI**: `DaprComponents.razor` distinguishes "0 (sidecar reachable)" from "unknown (sidecar unreachable)" using the new status, avoiding the misleading `N/A` when the actual count is `0`.
- **Aspire wiring**: `HexalithEventStoreExtensions.AddHexalithEventStore` accepts a new optional `resiliencyConfigPath` parameter and injects it as `AdminServer__ResiliencyConfigPath` env var on the Admin.Server resource. AppHost `Program.cs` resolves the path through the existing `ResolveDaprConfigPath` helper.
- **No change** to `Admin.Server.Host/appsettings.json` — operators don't need to maintain a duplicate entry.

---

## 3. Recommended Approach

**Option 1 — Direct Adjustment** (selected). All three concerns are addressed with localized changes inside the existing 19.x scope. No rollback. No MVP review. No PRD impact.

### Rationale

- Effort: **Low** (≤ 1 day, including tests)
- Risk: **Low** — additive contract change, the pattern (`RemoteMetadataStatus`) is already proven on two siblings
- The resiliency wiring follows option **c1** from the design discussion: the AppHost owns the absolute path because it already owns every other DAPR resource path; pushing it through an env var avoids a duplicated `appsettings.json` entry and avoids fragility from working-directory-relative paths.
- Why not option (a) — manual `appsettings.json` entry: forces every operator to maintain a duplicated path and breaks if the AppHost ever moves the YAML.
- Why not option (b) — auto-resolution with relative paths: depends on the runtime working directory, fragile across `dotnet run` vs `aspire run` vs Test Explorer.
- Why not "read from sidecar" (originally considered): **DAPR `/v1.0/metadata` does not expose the resiliency spec** (verified via Microsoft Learn / DAPR docs reference). Confirmed fields: `id`, `runtimeVersion`, `enabledFeatures`, `actors`, `components`, `httpEndpoints`, `subscriptions`, `extended`, `appConnectionProperties`, `scheduler`, `workflows`. Resiliency would require a separate non-existent endpoint or embedding the YAML as a Configuration store entry.

---

## 4. Detailed Change Proposals

### Change A — Wire Subscriptions/HttpEndpoints from remote eventstore sidecar metadata

**File:** `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprSidecarInfo.cs`

Two new positional record fields, mirroring `DaprPubSubOverview`:

```csharp
public record DaprSidecarInfo(
    string AppId,
    string RuntimeVersion,
    int ComponentCount,
    int SubscriptionCount,
    int HttpEndpointCount,
    RemoteMetadataStatus RemoteMetadataStatus,  // NEW
    string? RemoteEndpoint)                     // NEW
```

XML doc updated to clarify that `SubscriptionCount` / `HttpEndpointCount` reflect the **remote eventstore sidecar** (not the local admin sidecar, which never references pub/sub) and are only meaningful when `RemoteMetadataStatus == Available`.

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`

`GetSidecarInfoAsync` rewritten: keeps local-sidecar lookup for `AppId`, `RuntimeVersion`, `ComponentCount`, then performs an HTTP GET against `{EventStoreDaprHttpEndpoint}/v1.0/metadata` (same `"DaprSidecar"` named HttpClient and same exception handling as `GetPubSubOverviewAsync` / `GetActorRuntimeInfoAsync`). Counts the `subscriptions` and `httpEndpoints` arrays. Returns `RemoteMetadataStatus = NotConfigured | Available | Unreachable` accordingly.

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor`

Subscriptions stat card refined:

```razor
Value="@(_sidecar.RemoteMetadataStatus == RemoteMetadataStatus.Available ? _sidecar.SubscriptionCount.ToString() : "N/A")"
Subtitle="@(_sidecar.RemoteMetadataStatus == RemoteMetadataStatus.Available ? $"{_sidecar.HttpEndpointCount} HTTP endpoints" : "HTTP endpoints: N/A")"
Title="@GetRemoteMetadataTitle(_sidecar)"
```

New helper `GetRemoteMetadataTitle(DaprSidecarInfo)` produces a tooltip explaining whether the remote eventstore sidecar is reachable, unreachable, or not configured. Now `0` legitimate subscriptions display as `0` (sidecar reachable, no subs) instead of being rendered as `N/A`.

### Change B — Auto-inject resiliency YAML path under Aspire

**File:** `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`

`AddHexalithEventStore` gets a new optional parameter `string? resiliencyConfigPath = null`. When provided, the extension calls `adminServer.WithEnvironment("AdminServer__ResiliencyConfigPath", resiliencyConfigPath)`. Comment explains why the AppHost owns the path (DAPR resources directory ownership, no working-directory fragility, no duplicate appsettings entry).

**File:** `src/Hexalith.EventStore.AppHost/Program.cs`

```csharp
string resiliencyConfigPath = ResolveDaprConfigPath("resiliency.yaml");
// ...
HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
    eventStore,
    adminServer,
    adminUI,
    eventStoreAccessControlConfigPath,
    adminServerAccessControlConfigPath,
    resiliencyConfigPath);
```

Reuses the existing `ResolveDaprConfigPath` static helper, which already produces an absolute path that resolves correctly under both `dotnet run` and `aspire run` working directories.

**File:** `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs`

XML doc on `ResiliencyConfigPath` updated to reflect the new automatic injection in dev:

> *In Aspire development: auto-injected by `HexalithEventStoreExtensions` from the AppHost's resolved `DaprComponents/resiliency.yaml` location — no manual setting required.*

`Admin.Server.Host/appsettings.json` is **left unchanged** (no manual entry needed).

### Change C — Health-history page

No code change. Verified complete during investigation; report retained for traceability.

### Test changes

- `DaprSidecarInfoTests`: updated all four tests for the new constructor signature, plus assertions for the new fields.
- `DaprInfrastructureQueryServiceTests` (`GetSidecarInfoAsync_*`): existing happy-path test extended with `RemoteMetadataStatus.NotConfigured` assertions; **3 new tests** added — `GetSidecarInfoAsync_PopulatesSubscriptionAndHttpEndpointCounts_FromRemoteSidecar`, `GetSidecarInfoAsync_ReturnsZeroCounts_WhenRemoteSidecarMissingArrays`, `GetSidecarInfoAsync_ReturnsUnreachable_WhenRemoteSidecarFails`. Local `FakeHandler` added to mock remote sidecar HTTP responses.
- `AdminDaprControllerTests`: constructor invocation updated.
- `DaprComponentsPageTests`: `CreateSidecarInfo()` helper updated.

---

## 5. Verification

| Check | Result |
|---|---|
| `dotnet build Hexalith.EventStore.slnx --configuration Release` | ✅ 0 errors, 0 warnings |
| `Admin.Abstractions.Tests` (full) | ✅ 404/404 |
| `Admin.Server.Tests` — `DaprInfrastructureQueryServiceTests` + `DaprPubSubQueryServiceTests` + `AdminDaprControllerTests` | ✅ 31/31 |
| `Admin.UI.Tests` — `DaprComponentsPageTests` | ✅ 8/8 |
| Pre-existing failures on `main` (`DaprTenantQueryServiceTests` ×7, Admin.UI Bunit suite ×30) | ⚠️ Verified pre-existing via `git stash` baseline run — **not introduced by this change** |

---

## 6. Implementation Handoff

**Scope classification:** Minor — direct implementation by the development team (this session).

**Deliverables:**

- Code changes listed in Section 4 (already applied)
- Sprint change proposal (this document)
- Tests passing for all directly-affected suites
- No appsettings.json changes required for operators

**Operator validation steps** (post-merge, when running `aspire run`):

1. Open `https://localhost:60034/dapr` — verify "Subscriptions" shows the actual count (e.g. 1) instead of `N/A`, and the subtitle shows the HTTP endpoint count.
2. Hover the Subscriptions stat card — tooltip should read `Remote eventstore sidecar reachable at http://localhost:3501`.
3. Open `https://localhost:60034/dapr/resiliency` — verify the page now loads the full retry/timeout/circuit-breaker policies from `resiliency.yaml` (no "configuration not available" empty-state).
4. Open `https://localhost:60034/dapr/health-history` — verify still functional (no regression).

**Out of scope** (deferred):

- Pre-existing `DaprTenantQueryServiceTests` failures and Admin.UI Bunit suite failures on `main` — to be triaged separately.
- Production deployment story for `ResiliencyConfigPath` (currently driven by AppHost only; production needs its own env var or mounted-file convention — already documented in `AdminServerOptions.ResiliencyConfigPath` XML doc).

---

## 7. Sources

- DAPR Metadata API reference — https://docs.dapr.io/reference/api/metadata_api/
- DAPR Resiliency overview — https://docs.dapr.io/operations/resiliency/resiliency-overview/
- Prior story 19-6 sprint change proposal — `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-10-admin-dapr-metadata-diagnostics.md`
