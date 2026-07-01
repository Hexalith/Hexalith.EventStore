# Sprint Change Proposal — EventStore: metadata-rich, scope-aware projection-changed transport

- **Date:** 2026-06-20
- **Author:** Jerome (via Senior Developer review of ChatBot Story 10.6b)
- **Repo / submodule:** `Hexalith.EventStore`
- **Driving change:** ChatBot Epic 10, Story 10.6b — "Streaming AI response + Stop/Cancel" AC1 progress transport
- **Scope classification:** **Moderate** (additive framework contract change; back-compatible)
- **Status:** Approved for implementation
- **Release order:** **1 of 3** — EventStore must publish first. FrontComposer (proposal 2) then ChatBot (proposal 3) depend on this package version.
- **Companion proposals:**
  - `references/Hexalith.FrontComposer/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20-ai-response-progress-transport.md`
  - `<chatbot>/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20-ai-response-progress-transport.md`

---

## Section 1 — Issue Summary

ChatBot Story 10.6b shipped the full **metadata-only AI-response progress nudge** plumbing (contract, Fluxor action/reducer/effect, server projection, governed Stop/Cancel) but its headline capability (AC1 — progressive rendering) **does not run end-to-end**. The accepted ChatBot ADR (`docs/adrs/ai-response-streaming-transport.md`) mandates reusing "the existing SignalR projection-nudge model" rather than a bespoke channel.

The reuse target in this repo is **signal-only**:

- `IProjectionChangedBroadcaster.BroadcastChangedAsync(projectionType, tenantId, ct)` carries **only** `(projectionType, tenantId)`.
- `ProjectionChangedHub` groups clients by `{projectionType}:{tenantId}` — **tenant-scoped, not conversation-scoped**.
- The client method `IProjectionChangedClient.ProjectionChanged(projectionType, tenantId)` carries no ids/version/sequence/state.

ChatBot's nudge (`AiResponseProgressNudge`) requires `responseId / generationId / sourceVersion / sequence / state` so the client can run its stale/out-of-order fail-closed gate **before** re-querying. The signal-only channel physically cannot carry this, and its tenant-wide group would force every conversation watcher in a tenant to re-query on any projection change.

**Decision (from `correct-course`):** extend the projection-nudge model in this framework to carry a **bounded, opaque, metadata-only** detail payload plus an optional **group scope**, additively and back-compatibly. This stays metadata-only (no response text, chunks, prompts, content) — only the *channel shape* is extended, which the ChatBot ADR amendment authorizes as "a separate ADR with a proven need."

## Section 2 — Impact Analysis

- **Epic impact:** None to EventStore's own roadmap. This is an enabling framework change consumed by ChatBot Epic 10.
- **Story impact:** New EventStore story (suggest `ES-xx: Scope-aware metadata-rich projection-changed broadcast`). No existing EventStore story is invalidated.
- **Artifact conflicts:**
  - Architecture: the projection-changed transport section gains an additive "detail + scope" variant. ADR-18.5a (fail-open broadcast) semantics preserved.
  - Public API surface of `Hexalith.EventStore.Client` (interface) and the host hub change additively → **minor/feature version bump**, not breaking.
- **Technical impact:**
  - `Hexalith.EventStore.Client` — `IProjectionChangedBroadcaster`, `IProjectionChangedClient`, notification record.
  - `Hexalith.EventStore` (host) — `ProjectionChangedHub`, `SignalRProjectionChangedBroadcaster`, `ProjectionNotificationController`, `SignalROptions`.
  - `Hexalith.EventStore.Server` — `NoOpProjectionChangedBroadcaster`, `DaprProjectionChangeNotifier`.
  - Redis backplane already supported; scoped groups fan out unchanged.

## Section 3 — Recommended Approach

**Direct Adjustment — additive, back-compatible framework extension.** Keep the existing signal-only path 100% intact; add a parallel "detail" path and optional group scope. No consumer is forced to migrate.

- **Effort:** ~M (2–4 dev-days incl. tests + backplane verification).
- **Risk:** Low. Additive surface; existing callers/tests unaffected; new path defaulted off (no-op broadcaster unless `EventStore:SignalR:Enabled=true`).
- **Timeline impact:** Gates the ChatBot 10.6b completion; must publish before FrontComposer change starts.

**Safety constraint (non-negotiable):** the detail payload is an **opaque metadata map** (`IReadOnlyDictionary<string,string>`). The framework treats values as metadata, must not log values at `Information` level, and bounds map size/key count. Domain meaning lives in ChatBot; the framework stays content-agnostic so the "metadata-only" floor is enforceable at the framework boundary.

## Section 4 — Detailed Change Proposals

### 4.1 `IProjectionChangedBroadcaster` (`Hexalith.EventStore.Client/Projections/`)

```
OLD:
  Task BroadcastChangedAsync(string projectionType, string tenantId, CancellationToken cancellationToken = default);

NEW (additive — keep the old method):
  Task BroadcastChangedAsync(string projectionType, string tenantId, CancellationToken cancellationToken = default);

  // New: metadata-rich, optionally scoped broadcast.
  Task BroadcastChangedAsync(ProjectionChangedDetail detail, CancellationToken cancellationToken = default);
```

New record (Client project, alongside the interface):

```csharp
/// Opaque, metadata-only detail for a projection-changed broadcast.
/// Values are treated as metadata by the framework and MUST NOT carry
/// authoritative content. GroupScope (when present) narrows the SignalR
/// group below tenant level (e.g. a conversation id).
public sealed record ProjectionChangedDetail(
    string ProjectionType,
    string TenantId,
    string? GroupScope,
    IReadOnlyDictionary<string, string> Metadata);
```

**Rationale:** lets ChatBot pass `responseId/generationId/sourceVersion/sequence/state` through the framework without the framework knowing AI semantics, and without a second channel.

### 4.2 `IProjectionChangedClient` (host) — additive client method

```
OLD:
  Task ProjectionChanged(string projectionType, string tenantId);

NEW (additive):
  Task ProjectionChanged(string projectionType, string tenantId);
  Task ProjectionChangedDetail(string projectionType, string tenantId, string? groupScope, IReadOnlyDictionary<string,string> metadata);
```

### 4.3 `ProjectionChangedHub` — optional group scope

```
OLD:
  public async Task JoinGroup(string projectionType, string tenantId)
  // group name = $"{projectionType}:{tenantId}"

NEW (additive overload + scoped group naming):
  public async Task JoinGroup(string projectionType, string tenantId)                  // unchanged
  public async Task JoinGroup(string projectionType, string tenantId, string? scope)   // new
  // group name = scope is null/empty ? $"{projectionType}:{tenantId}"
  //                                  : $"{projectionType}:{tenantId}:{scope}"
  // Matching LeaveGroup(projectionType, tenantId, scope) overload.
```

- `[Authorize]` + `ITenantValidator.ValidateAsync` tenant gate **unchanged** and still runs first.
- `scope` validated for the same reserved-colon / charset rule already applied to `projectionType`/`tenantId`.
- Per-connection group cap (default 50) unchanged; scoped groups count against it.

**Rationale:** satisfies the ChatBot ADR's "rejoin only server-authorized **project/conversation** groups" reconnect requirement; without scope, AI progress would re-query every conversation in a tenant.

### 4.4 `SignalRProjectionChangedBroadcaster` — implement detail path

- Implement `BroadcastChangedAsync(ProjectionChangedDetail detail, ct)`: build group `{detail.ProjectionType}:{detail.TenantId}[:{detail.GroupScope}]`, call `Clients.Group(group).ProjectionChangedDetail(detail.ProjectionType, detail.TenantId, detail.GroupScope, detail.Metadata)`.
- Preserve **fail-open** semantics (ADR-18.5a): broadcast exceptions caught/logged, never break the caller.
- **Bound + redact:** reject/clip metadata maps over a configured key/byte cap; never log metadata **values** above `Debug`.

### 4.5 `NoOpProjectionChangedBroadcaster` — implement new overload as `Task.CompletedTask`.

### 4.6 DAPR pub/sub path (optional, only if cross-process broadcast is used)

```
OLD:  record ProjectionChangedNotification(string ProjectionType, string TenantId, string? EntityId = null);
NEW:  record ProjectionChangedNotification(string ProjectionType, string TenantId, string? EntityId = null,
                                           string? GroupScope = null,
                                           IReadOnlyDictionary<string,string>? Metadata = null);
```

`ProjectionNotificationController POST /projections/changed`: when `Metadata` present, forward via the detail overload; else current behavior. **Recommendation:** ChatBot should call the broadcaster **in-process** for AI progress ticks (lower latency, Redis backplane still fans out across instances), making this DAPR change optional — include it only if a cross-process producer is expected.

### 4.7 `SignalROptions` — add bounds config

- `MaxDetailMetadataEntries` (default e.g. 16), `MaxDetailMetadataBytes` (default e.g. 2048). Used by 4.4 to enforce metadata-only-and-bounded.

### 4.8 Tests (new)

- Detail broadcast reaches a scoped group and **not** the tenant-wide group, and vice-versa.
- Back-compat: signal-only `ProjectionChanged` path and existing group naming unchanged.
- Tenant authorization still rejects unauthorized scoped joins.
- Metadata bound/clip enforced; values not logged above `Debug`.
- Fail-open preserved on broadcast exception.
- Redis backplane: detail message fans out across two hub instances.

## Section 5 — Implementation Handoff

- **Scope:** Moderate. Route to **EventStore DEV** (with a brief Architect sign-off on the additive Client surface + version bump).
- **Deliverables:** additive `IProjectionChangedBroadcaster`/`IProjectionChangedClient` surface, scoped hub groups, bounded metadata-only detail broadcast, tests, **published package version**.
- **Success criteria:**
  1. Existing signal-only consumers (e.g. FrontComposer `BadgeCountService`) build and pass unchanged.
  2. New detail + scope path covered by tests incl. Redis backplane and auth.
  3. New version published and pinned so FrontComposer (proposal 2) can consume it.
  4. No metadata **value** appears in logs above `Debug`; map bounds enforced.
- **Do NOT:** carry response text/chunks/prompts/content in `Metadata`; break the signal-only method or group naming; change `[Authorize]`/tenant-validation ordering.

## Section 6 — Approval

- [x] Approved for implementation — 2026-07-01
- [ ] Approved with changes (noted below)
- [ ] Rejected / revise

Notes: Approved by Administrator. Route remains EventStore DEV, with Architect sign-off on the additive Client surface and version bump before dependent FrontComposer and ChatBot proposals consume the package.
