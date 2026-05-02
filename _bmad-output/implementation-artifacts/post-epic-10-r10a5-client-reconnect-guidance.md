# Post-Epic-10 R10-A5: Client Reconnect Guidance

Status: ready-for-dev

<!-- Source: epic-10-retro-2026-05-01.md R10-A5 -->
<!-- Source: sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md Proposal 5 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **developer integrating live projection refresh**,
I want the SignalR client documentation and sample guidance to state the connect and reconnect query responsibilities clearly,
So that consumers do not mistake automatic group rejoin for missed-signal replay or authoritative projection data delivery.

## Story Context

Epic 10 delivered a deliberately small SignalR contract: the hub broadcasts `ProjectionChanged(projectionType, tenantId)` as an invalidation signal, not projection state. Story 10.3 verified that `EventStoreSignalRClient` uses automatic reconnect and rejoins tracked groups after recovery. The remaining R10-A5 gap is documentation and sample guidance: rejoin restores future signal delivery, but it does not replay signals missed while the client was disconnected.

The Query API remains authoritative. Clients must load baseline state on connect and must perform a query when they know a reconnect occurred or when a UI surface resumes after being disconnected. `If-None-Match` remains the efficient refresh mechanism: `304 Not Modified` keeps cached UI state, while `200 OK` supplies replacement data and a new ETag.

Current HEAD at story creation: `34884ac`.

## Acceptance Criteria

1. **Docs state the signal-only contract without overclaiming.** `docs/reference/query-api.md`, `docs/reference/nuget-packages.md`, and any touched sample guide text state that SignalR sends invalidation signals only. They must not imply projection data, ETags, command status, or missed signals are delivered through SignalR.

2. **Initial connect responsibility is explicit.** Docs and sample guidance tell consumers to query the HTTP Query API on connect or component initialization to establish baseline state before relying on future `ProjectionChanged` callbacks.

3. **Reconnect responsibility is explicit and bounded.** Docs say automatic reconnect plus group rejoin restores future notifications only. Clients that need current state after downtime must re-query on reconnect, browser resume, page restore, or an equivalent lifecycle signal. If the current `EventStoreSignalRClient` does not expose a public reconnect callback, the docs must name that limitation instead of pretending the helper can notify consumers today.

4. **Sample UI refresh-pattern guidance is updated.** `docs/guides/sample-blazor-ui.md` names which sample patterns auto-query on signals and which wait for user action. It also states that the current sample pages demonstrate signal-triggered query refresh, not missed-signal replay.

5. **Existing sample code claims are truthful.** Inspect `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/Index.razor`, the three pattern pages, and shared query components for wording that overclaims reconnect catch-up. Update text or comments only where needed. Do not redesign the sample UI unless the existing text is materially misleading.

6. **Client helper docs stay aligned with current API.** Guidance references `EventStoreSignalRClientOptions.AccessTokenProvider`, `RetryPolicy`, and `ConfigureHttpConnection` only as they exist today. Do not introduce a new public client API in this story unless the docs cannot be made truthful without it; if such an API is needed, record a deferred follow-up instead.

7. **No product behavior is changed accidentally.** The story is documentation/sample guidance by default. Do not change hub protocol, group format, query contract, retry behavior, token flow, or reconnect internals while completing R10-A5.

8. **Verification covers docs and any touched sample surface.** Run a markdown or link validation command when available. If only docs changed, no .NET test run is required. If sample `.razor` or client code changes are made, run the affected project/test commands individually and record results.

9. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and both `last_updated` fields in `sprint-status.yaml` name R10-A5 and the documentation result. At code-review signoff, both become `done`.

## Party-Mode Hardening Notes

- Use this canonical contract sentence consistently in affected docs: "SignalR notifications are invalidation signals only. They do not contain projection data, ETags, command status, or a replay of missed signals. The Query API remains the authoritative source for current projection state."
- Query API reference placement: state the authoritative-state contract near the SignalR hub usage guidance, then show the connect/reconnect responsibility model.
- NuGet package reference placement: describe `EventStoreSignalRClient` group rejoin as internal future-signal continuity, and state that applications must trigger their own re-query from lifecycle events they can observe.
- Sample Blazor UI guide placement: explain practical UI lifecycle behavior for initial load, signal receipt, reconnect/resume, and user-triggered refresh.
- Prefer precise wording such as "authoritative source", "re-query", "known reconnect", "resume from sleep", and "known downtime". Avoid relying only on idioms such as "source of truth", "catch up", or "wake up".

## Scope Boundaries

- Do not add missed-signal replay, event buffering, projection data delivery, or command status delivery to SignalR.
- Do not change the SignalR payload. It remains `ProjectionChanged(projectionType, tenantId)`.
- Do not change group names. They remain `{projectionType}:{tenantId}`.
- Do not solve tenant-aware group authorization here; that is `post-epic-10-r10a3-hub-group-authorization-decision`.
- Do not prove Redis multi-instance delivery here; that is `post-epic-10-r10a2-redis-backplane-runtime-proof`.
- Do not define the operational latency evidence pattern here; that is `post-epic-10-r10a6-signalr-operational-evidence-pattern`.
- Do not add a Blazor-specific circuit handler or general connection-state observable unless a small docs correction is impossible without it. Prefer recording the limitation and routing a follow-up.
- Do not add a public reconnect callback to `EventStoreSignalRClient` in this story. Document the current helper boundary instead.
- Do not change hub contracts, payloads, group/retry/token logic, reconnect behavior, replay semantics, or server-side missed-signal buffering.
- Do not change sample UI behavior unless existing UI text or comments currently imply SignalR carries data, command status, ETags, or missed notifications.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| Query and SignalR reference | `docs/reference/query-api.md` | Clarify initial query, `If-None-Match`, signal-only callback, and reconnect re-query responsibility |
| Package guidance | `docs/reference/nuget-packages.md` | Clarify what `Hexalith.EventStore.SignalR` does and does not guarantee |
| Sample UI guide | `docs/guides/sample-blazor-ui.md` | Add refresh-pattern and reconnect responsibility guidance |
| Sample overview page | `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/Index.razor` | Inspect for overclaims and update text only if needed |
| Notification pattern | `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor` | Confirm it waits for user refresh after a signal |
| Silent reload pattern | `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor` | Confirm it auto-queries on a signal after debounce |
| Selective refresh pattern | `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SelectiveRefreshPattern.razor` | Confirm independent components own refresh behavior |
| Shared query service | `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` | Cite existing ETag / `If-None-Match` behavior; avoid reimplementing it |
| SignalR client helper | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` | Cite current reconnect and group rejoin behavior; do not change by default |
| Client options | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs` | Cite current token, retry policy, and HTTP connection options |
| Client tests | `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` | Use only if public docs force a client-helper code change |

## Tasks / Subtasks

- [ ] Task 0: Baseline and wording audit (AC: #1, #5, #7)
  - [ ] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [ ] 0.2 Grep docs and sample text for `reconnect`, `rejoin`, `replay`, `catch-up`, `ProjectionChanged`, `SignalR`, and `If-None-Match`.
  - [ ] 0.3 Also grep for `real-time state`, `live synchronized`, `guaranteed update`, `missed updates`, `stream`, and `event replay` under `docs/`, `samples/`, and relevant client package README/package text.
  - [ ] 0.4 Identify every sentence that could make a consumer believe missed signals are replayed, reconnect makes the client current, or SignalR carries current projection data.
  - [ ] 0.5 Confirm no runtime SignalR client/server behavior files changed unless a documented blocker forced escalation.
  - [ ] 0.6 Confirm `EventStoreSignalRClient` does not currently expose a consumer callback for reconnect completion.

- [ ] Task 1: Update reference documentation (AC: #1, #2, #3, #6)
  - [ ] 1.1 In `docs/reference/query-api.md`, add or tighten a short "connect and reconnect responsibilities" subsection near the SignalR hub usage guidance.
  - [ ] 1.2 State the normal connect flow: query for baseline state, subscribe/join the group, then re-query with `If-None-Match` when `ProjectionChanged` fires.
  - [ ] 1.3 State the reconnect flow: group rejoin restores future signals; consumers that know a reconnect/resume happened must re-query for current authoritative state.
  - [ ] 1.4 State the reconnect flow without implying guaranteed recovery: after initial connect, known reconnect, resume from sleep, or known downtime, clients should re-query the Query API for the projections they display.
  - [ ] 1.5 Say the current helper rejoins groups internally but does not expose a public reconnected event for consumer refresh logic.
  - [ ] 1.6 In `docs/reference/nuget-packages.md`, align the SignalR package description with the same responsibility boundary.
  - [ ] 1.7 Add a concise "Do / Do not" contract block where it fits: do query on initial load, re-query after known reconnect/resume/downtime, and treat notifications as refresh hints; do not treat notifications as projection data, command status, ETags, or replayed missed notifications.

- [ ] Task 2: Update sample guidance (AC: #2, #3, #4, #5)
  - [ ] 2.1 In `docs/guides/sample-blazor-ui.md`, add a concise table for initial load, signal receipt, reconnect/resume, and user-triggered refresh behavior.
  - [ ] 2.2 Name pattern behavior: notification waits for user refresh, silent reload auto-queries after debounce, selective refresh lets components refresh independently.
  - [ ] 2.3 State that the sample demonstrates query-after-signal behavior, not replay of changes missed during disconnect.
  - [ ] 2.4 If the sample overview or pattern page text overclaims reconnect catch-up, update only the wording needed to make it truthful.

- [ ] Task 3: Preserve implementation boundaries (AC: #6, #7)
  - [ ] 3.1 Do not modify `EventStoreSignalRClient` unless a concrete documentation truthfulness blocker is found.
  - [ ] 3.2 If a public reconnect notification API is needed, add a deferred-work row instead of widening this docs story silently.
  - [ ] 3.3 Do not change hub authorization, Redis backplane behavior, group naming, retry policy defaults, or query API behavior.

- [ ] Task 4: Verification gates (AC: #8)
  - [ ] 4.1 Run `npx markdownlint-cli2 "docs/**/*.md" "_bmad-output/implementation-artifacts/post-epic-10-r10a5-client-reconnect-guidance.md"` if the local Node toolchain supports it; otherwise record the unavailable command.
  - [ ] 4.2 Run `npx lychee --config lychee.toml docs/reference/query-api.md docs/reference/nuget-packages.md docs/guides/sample-blazor-ui.md` if link checking is practical in the environment; otherwise record the blocker.
  - [ ] 4.3 If any `.razor` file changes, run `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj --no-restore`.
  - [ ] 4.4 If SignalR client source changes despite the default boundary, run `dotnet test tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj --no-restore`.
  - [ ] 4.5 If validation cannot run, record the attempted command or tool, the failure reason, any manual spot-check performed, and whether an equivalent CI check is configured. Do not write "CI should catch it" unless that check is known to exist.
  - [ ] 4.6 If only markdown documentation changes are made, no .NET tests are required. If sample `.razor` text changes are made, build the sample project when practical. If SignalR runtime code changes are made, run relevant SignalR tests or record explicit approval/deferral because behavior changes are out of scope by default.

- [ ] Task 5: Story bookkeeping (AC: #9)
  - [ ] 5.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [ ] 5.2 Move this story and only this story from `ready-for-dev` to `review` at dev handoff.
  - [ ] 5.3 Leave R10-A2/R10-A3/R10-A6/R10-A7/R10-A8 status rows unchanged.

## Dev Notes

### Architecture Guardrails

- SignalR is an invalidation channel. Query endpoints remain the source of projection data, current ETags, and tenant authorization decisions.
- Automatic reconnect is about transport recovery. Automatic group rejoin is about future subscription continuity. Neither mechanism replays missed messages.
- The sample's `CounterQueryService` already owns ETag caching and sends `If-None-Match`; reuse that as the documentation example instead of inventing another query helper.
- Current sample pages perform an initial query during component initialization. That is the baseline-state pattern R10-A5 should document.
- The current client helper logs unexpected closed events and rejoins tracked groups after reconnect, but it does not expose a public "reconnected" notification that UI components can subscribe to.

### Current-Code Intelligence

- `EventStoreSignalRClient.StartAsync()` starts the hub connection and calls `JoinAllGroupsAsync()` for groups subscribed before start.
- `EventStoreSignalRClient.SubscribeAsync()` tracks callbacks in `_subscribedGroups`; if already connected, it invokes `JoinGroup` immediately.
- `OnReconnectedAsync()` calls `JoinAllGroupsAsync()` internally. Exceptions while rejoining a group are logged and do not throw back to consumers.
- `EventStoreSignalRClientOptions.RetryPolicy` is optional. When null, the SignalR default retry sequence is used.
- `CounterQueryService.GetCounterStatusAsync()` sends `If-None-Match` when it has a cached ETag and keeps cached state on `304 Not Modified`.
- `NotificationPattern.razor` marks data changed and waits for the user to click refresh.
- `SilentReloadPattern.razor` debounces and re-runs the query automatically after a signal.
- `CounterValueCard.razor` and `CounterHistoryGrid.razor` in the selective pattern subscribe independently and refresh their own data.

### Previous Story Intelligence

- Story 10.1 established the signal-only hub contract and ETag-before-broadcast ordering.
- Story 10.2 is runtime backplane proof; do not use R10-A5 to prove multi-instance delivery.
- Story 10.3 verified reconnect and group rejoin but explicitly recorded the missed-signal limitation as consumer-owned.
- R10-A3 may add hub authorization later. Keep R10-A5 guidance compatible with bearer tokens through `AccessTokenProvider`.

### Testing Standards

- This is primarily a documentation story. Prefer markdown/link checks over broad .NET test runs when no source changes are made.
- If sample `.razor` text changes only, a sample project build is enough. Do not run solution-level `dotnet test`.
- If the dev agent changes public SignalR client code, SignalR client tests become mandatory.
- Record any unavailable tool, network, or environment blocker instead of claiming validation that did not run.

### Latest Technical Information

- Microsoft Learn for ASP.NET Core SignalR .NET clients documents `WithAutomaticReconnect()` as opt-in behavior and describes the default reconnect attempts before the connection becomes disconnected.
- Microsoft Learn for `IRetryPolicy` describes it as the abstraction that controls when reconnect attempts occur when passed to `WithAutomaticReconnect`.
- The repository currently uses `Microsoft.AspNetCore.SignalR.Client` version `10.0.5` from `Directory.Packages.props`.

### Project Structure Notes

- Documentation changes should stay under `docs/reference/` and `docs/guides/`.
- Sample UI code lives under `samples/Hexalith.EventStore.Sample.BlazorUI/`.
- SignalR client code lives under `src/Hexalith.EventStore.SignalR/`.
- Expected BMAD edits during dev are docs, possibly small sample text updates, this story file, and `sprint-status.yaml` bookkeeping.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md#Proposal-5`] - R10-A5 acceptance criteria and rationale.
- [Source: `_bmad-output/implementation-artifacts/epic-10-retro-2026-05-01.md`] - R10-A5 action item and reconnect overclaim risk.
- [Source: `_bmad-output/implementation-artifacts/10-3-automatic-signalr-group-rejoining-on-reconnection.md`] - Reconnect/rejoin implementation evidence and missed-signal limitation.
- [Source: `_bmad-output/implementation-artifacts/10-1-signalr-hub-and-projection-change-broadcasting.md`] - Signal-only model and ETag-before-broadcast context.
- [Source: `docs/reference/query-api.md`] - Current Query API, ETag, and SignalR usage guidance.
- [Source: `docs/reference/nuget-packages.md`] - Current `Hexalith.EventStore.SignalR` package description.
- [Source: `docs/guides/sample-blazor-ui.md`] - Current sample refresh pattern and smoke evidence guidance.
- [Source: `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs`] - Current automatic reconnect and group rejoin behavior.
- [Source: `src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs`] - Current token, retry, and HTTP connection options.
- [Source: `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs`] - Current ETag and `If-None-Match` behavior.
- [Source: Microsoft Learn, `https://learn.microsoft.com/aspnet/core/signalr/dotnet-client`] - ASP.NET Core SignalR .NET client reconnect behavior.
- [Source: Microsoft Learn, `https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.signalr.client.iretrypolicy`] - `IRetryPolicy` reconnect policy abstraction.

## Dev Agent Record

### Agent Model Used

To be filled by dev agent.

### Deferred Decisions / Follow-ups

To be filled by dev agent if the current helper API prevents truthful reconnect-refresh guidance.

### Debug Log References

To be filled by dev agent.

### Completion Notes List

To be filled by dev agent.

### File List

To be filled by dev agent.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-02 | 0.2 | Party-mode review hardened reconnect contract wording, implementation guardrails, and validation branches. | Codex automation |
| 2026-05-01 | 0.1 | Created ready-for-dev R10-A5 client reconnect guidance story. | Codex automation |

## Party-Mode Review

- Date/time: 2026-05-02T10:13:39+02:00
- Selected story key: `post-epic-10-r10a5-client-reconnect-guidance`
- Command/skill invocation used: `/bmad-party-mode post-epic-10-r10a5-client-reconnect-guidance; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: Reviewers agreed the story should stay documentation/sample-guidance focused; strengthen the canonical invalidation-only contract; state missed-signal replay negatively; document the current helper's lack of public reconnect callback; add precise audit terms; and make validation branches explicit for docs-only, sample `.razor`, and SignalR runtime changes.
- Changes applied: Added party-mode hardening notes with the canonical contract sentence and doc placement guidance; expanded scope boundaries to forbid reconnect callback/API and behavior changes; tightened wording-audit tasks; added "Do / Do not" contract guidance; and expanded validation fallback evidence requirements.
- Findings deferred: Dev-story execution must decide whether existing docs or sample UI wording is materially misleading and perform the actual documentation updates. No product, architecture, public API, hub contract, or sample behavior decision was made in this review.
- Final recommendation: ready-for-dev

## Verification Status

Story creation only. Documentation, sample, and build verification are intentionally deferred to `bmad-dev-story`.
