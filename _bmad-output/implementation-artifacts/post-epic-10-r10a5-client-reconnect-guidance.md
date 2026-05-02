# Post-Epic-10 R10-A5: Client Reconnect Guidance

Status: done

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

6. **Client helper docs stay aligned with current API.** Guidance references `EventStoreSignalRClientOptions.AccessTokenProvider`, `RetryPolicy`, and `ConfigureHttpConnection` only as they exist today. Do not introduce a new public client API in this story unless the docs cannot be made truthful without it; if such an API is needed, record a deferred follow-up instead. Do not promise exact reconnect timing or attempt counts beyond the configured SignalR reconnect policy.

7. **No product behavior is changed accidentally.** The story is documentation/sample guidance by default. Do not change hub protocol, group format, query contract, retry behavior, token flow, or reconnect internals while completing R10-A5.

8. **Verification covers docs and any touched sample surface.** Run a markdown or link validation command when available. If only docs changed, no .NET test run is required. If sample `.razor` or client code changes are made, run the affected project/test commands individually and record results. Record a post-edit wording audit for replay/current-state overclaims in the touched docs and sample text.

9. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and both `last_updated` fields in `sprint-status.yaml` name R10-A5 and the documentation result. At code-review signoff, both become `done`.

## Party-Mode Hardening Notes

- Use this canonical contract sentence consistently in affected docs: "SignalR notifications are invalidation signals only. They do not contain projection data, ETags, command status, or a replay of missed signals. The Query API remains the authoritative source for current projection state."
- Query API reference placement: state the authoritative-state contract near the SignalR hub usage guidance, then show the connect/reconnect responsibility model.
- NuGet package reference placement: describe `EventStoreSignalRClient` group rejoin as internal future-signal continuity, and state that applications must trigger their own re-query from lifecycle events they can observe.
- Sample Blazor UI guide placement: explain practical UI lifecycle behavior for initial load, signal receipt, reconnect/resume, and user-triggered refresh.
- Prefer precise wording such as "authoritative source", "re-query", "known reconnect", "resume from sleep", and "known downtime". Avoid relying only on idioms such as "source of truth", "catch up", or "wake up".

## Advanced Elicitation Hardening Notes

- Treat reconnect, browser resume, page restore, network-online events, and user-visible stale-data recovery as application-owned lifecycle signals. The docs may name these examples, but must not claim the current helper raises a consumer-facing reconnect notification.
- Keep the guidance security-neutral: `AccessTokenProvider` explains token supply for the hub connection, while tenant authorization and authoritative projection data remain owned by existing Query API and hub authorization contracts.
- The post-edit wording audit should flag both explicit overclaims and softer phrases such as "keeps current", "stays synchronized", "missed update", "automatic recovery", or "real-time state" when they appear near SignalR guidance.
- If truthful docs require a reconnect callback, connection-state observable, or sample behavior change, record it as a deferred follow-up instead of widening this story beyond documentation/sample guidance.

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
- Do not present SignalR reconnect as a tenant authorization or current-state validation boundary. Reconnect restores transport continuity only.

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

- [x] Task 0: Baseline and wording audit (AC: #1, #5, #7)
  - [x] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [x] 0.2 Grep docs and sample text for `reconnect`, `rejoin`, `replay`, `catch-up`, `ProjectionChanged`, `SignalR`, and `If-None-Match`.
  - [x] 0.3 Also grep for `real-time state`, `live synchronized`, `guaranteed update`, `missed updates`, `stream`, and `event replay` under `docs/`, `samples/`, and relevant client package README/package text.
  - [x] 0.4 Identify every sentence that could make a consumer believe missed signals are replayed, reconnect makes the client current, or SignalR carries current projection data.
  - [x] 0.5 Confirm no runtime SignalR client/server behavior files changed unless a documented blocker forced escalation.
  - [x] 0.6 Confirm `EventStoreSignalRClient` does not currently expose a consumer callback for reconnect completion.
  - [x] 0.7 After edits, repeat the overclaim grep on touched files and record whether each remaining match is intentional context, a corrected sentence, or unrelated wording.

- [x] Task 1: Update reference documentation (AC: #1, #2, #3, #6)
  - [x] 1.1 In `docs/reference/query-api.md`, add or tighten a short "connect and reconnect responsibilities" subsection near the SignalR hub usage guidance.
  - [x] 1.2 State the normal connect flow: query for baseline state, subscribe/join the group, then re-query with `If-None-Match` when `ProjectionChanged` fires.
  - [x] 1.3 State the reconnect flow: group rejoin restores future signals; consumers that know a reconnect/resume happened must re-query for current authoritative state.
  - [x] 1.4 State the reconnect flow without implying guaranteed recovery: after initial connect, known reconnect, resume from sleep, or known downtime, clients should re-query the Query API for the projections they display.
  - [x] 1.5 Say the current helper rejoins groups internally but does not expose a public reconnected event for consumer refresh logic.
  - [x] 1.6 In `docs/reference/nuget-packages.md`, align the SignalR package description with the same responsibility boundary.
  - [x] 1.7 Add a concise "Do / Do not" contract block where it fits: do query on initial load, re-query after known reconnect/resume/downtime, and treat notifications as refresh hints; do not treat notifications as projection data, command status, ETags, or replayed missed notifications.
  - [x] 1.8 Avoid documenting exact reconnect attempt timing unless the text explicitly attributes it to the configured SignalR retry policy and not to an EventStore contract.

- [x] Task 2: Update sample guidance (AC: #2, #3, #4, #5)
  - [x] 2.1 In `docs/guides/sample-blazor-ui.md`, add a concise table for initial load, signal receipt, reconnect/resume, and user-triggered refresh behavior.
  - [x] 2.2 Name pattern behavior: notification waits for user refresh, silent reload auto-queries after debounce, selective refresh lets components refresh independently.
  - [x] 2.3 State that the sample demonstrates query-after-signal behavior, not replay of changes missed during disconnect.
  - [x] 2.4 If the sample overview or pattern page text overclaims reconnect catch-up, update only the wording needed to make it truthful.
  - [x] 2.5 If the sample guide mentions browser resume or network recovery, phrase it as a consumer-owned reason to re-query, not as behavior implemented by the sample helper.

- [x] Task 3: Preserve implementation boundaries (AC: #6, #7)
  - [x] 3.1 Do not modify `EventStoreSignalRClient` unless a concrete documentation truthfulness blocker is found.
  - [x] 3.2 If a public reconnect notification API is needed, add a deferred-work row instead of widening this docs story silently.
  - [x] 3.3 Do not change hub authorization, Redis backplane behavior, group naming, retry policy defaults, or query API behavior.
  - [x] 3.4 If docs expose a limitation that materially harms adopter experience, capture the follow-up with owner, trigger, and why R10-A5 intentionally did not implement it.

- [x] Task 4: Verification gates (AC: #8)
  - [x] 4.1 Run `npx markdownlint-cli2 "docs/**/*.md" "_bmad-output/implementation-artifacts/post-epic-10-r10a5-client-reconnect-guidance.md"` if the local Node toolchain supports it; otherwise record the unavailable command.
  - [x] 4.2 Run `npx lychee --config lychee.toml docs/reference/query-api.md docs/reference/nuget-packages.md docs/guides/sample-blazor-ui.md` if link checking is practical in the environment; otherwise record the blocker.
  - [x] 4.3 If any `.razor` file changes, run `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj --no-restore`.
  - [x] 4.4 If SignalR client source changes despite the default boundary, run `dotnet test tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj --no-restore`.
  - [x] 4.5 If validation cannot run, record the attempted command or tool, the failure reason, any manual spot-check performed, and whether an equivalent CI check is configured. Do not write "CI should catch it" unless that check is known to exist.
  - [x] 4.6 If only markdown documentation changes are made, no .NET tests are required. If sample `.razor` text changes are made, build the sample project when practical. If SignalR runtime code changes are made, run relevant SignalR tests or record explicit approval/deferral because behavior changes are out of scope by default.
  - [x] 4.7 Record the exact post-edit overclaim grep terms and the touched-file scope in the Dev Agent Record.

- [x] Task 5: Story bookkeeping (AC: #9)
  - [x] 5.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [x] 5.2 Move this story and only this story from `ready-for-dev` to `review` at dev handoff.
  - [x] 5.3 Leave R10-A2/R10-A3/R10-A6/R10-A7/R10-A8 status rows unchanged.

### Review Findings

Code review completed 2026-05-02 by `/bmad-code-review` (Blind Hunter, Edge Case Hunter, Acceptance Auditor — all three layers ran successfully). Acceptance Auditor reports all 9 ACs satisfied. Findings below come from cross-file consistency and sample-text wording checks the auditor did not raise.

- [x] [Review][Decision] Sample/doc step ordering inconsistency — Resolved: option (b). `docs/reference/query-api.md` Connect and Reconnect Responsibilities section now states the numbered order is a recommended reading order, not a required execution order; `EventStoreSignalRClient` queues `JoinGroup` calls until `StartAsync()` and no `ProjectionChanged` callback fires before that point. Sample code left as-is per story scope boundary.
- [x] [Review][Patch] `NotificationPattern.razor:22` — replaced "data stays stable until they click 'Refresh Now'" with "the displayed data is held until they click 'Refresh Now'". [`samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor:22`]
- [x] [Review][Patch] Documented `AccessTokenProvider` vs `ConfigureHttpConnection` precedence in `docs/reference/query-api.md` and `docs/reference/nuget-packages.md` — `ConfigureHttpConnection` runs after `AccessTokenProvider` is wired and a delegate that sets `connectionOptions.AccessTokenProvider` will override the dedicated option. [`docs/reference/query-api.md`, `docs/reference/nuget-packages.md`]
- [x] [Review][Patch] Tightened residual "real-time" framing — `docs/reference/query-api.md:5` abstract and `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/Index.razor:6-12` paragraph now use "projection change invalidation signals" for consistency with the new canonical wording.
- [x] [Review][Patch] `query-api.md` step 1 wording — replaced "on connect or component initialization" with "at the lifecycle moment your component or service exposes first (typically component initialization or service startup)". [`docs/reference/query-api.md`]
- [x] [Review][Patch] Wording polish — (a) "Seamless after received signals" is now "Refreshes seamlessly after received signals" in `Pages/Index.razor:76` and `Pages/SilentReloadPattern.razor:10`; (b) `docs/guides/sample-blazor-ui.md:14` table row now says "after a debounce window that follows a received signal"; (c) `sample-blazor-ui.md` SignalR section now uses "each `ProjectionChanged` callback" consistently with the rest of the docs; (d) `SilentReloadPattern.razor:19` plural form aligned: "receives counter projection change signals".
- [x] [Review][Defer] `docs/community/roadmap.md:42` — SignalR helper description omits the invalidation-signal contract. Deferred, pre-existing; out of declared three-doc scope. [`docs/community/roadmap.md:42`]
- [x] [Review][Defer] `README.md:71` — "real-time read-model refresh" wording in top-level repo README. Deferred, pre-existing; out of declared three-doc scope. [`README.md:71`]

Dismissed as noise (5): "Real-time monitoring → Live monitoring" (intentional per advanced-elicitation hardening flagging "real-time state"); alleged rejoin-vs-no-callback contradiction (false positive — diff explicitly reconciles); triple-duplication of the canonical sentence (intentional per Party-Mode hardening note); "command status" in the negative list (intentional per Party-Mode canonical wording); `SilentReloadPattern.razor:19` not mentioning reconnect/sleep miss (AC #5 threshold is "materially misleading").

## Dev Notes

### Architecture Guardrails

- SignalR is an invalidation channel. Query endpoints remain the source of projection data, current ETags, and tenant authorization decisions.
- Automatic reconnect is about transport recovery. Automatic group rejoin is about future subscription continuity. Neither mechanism replays missed messages.
- Reconnect completion does not prove the UI is current. It only means the transport recovered and the helper attempted to restore tracked groups for future notifications.
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

Codex GPT-5

### Deferred Decisions / Follow-ups

- None required for this story. The docs now name the current helper limitation: `EventStoreSignalRClient` rejoins groups internally after reconnect but does not expose a public reconnected callback for consumer refresh logic.

### Debug Log References

- 2026-05-02T18:30:58+02:00 - Baseline: HEAD `e76adff`; story status confirmed `ready-for-dev`; sprint row moved to `in-progress`.
- Baseline audit command: `rg -n -i "reconnect|rejoin|replay|catch-up|ProjectionChanged|SignalR|If-None-Match|real-time state|live synchronized|guaranteed update|missed updates|missed update|stream|event replay|keeps current|stays synchronized|automatic recovery" docs samples src\Hexalith.EventStore.SignalR tests\Hexalith.EventStore.SignalR.Tests`.
- Targeted audit scope: `docs/reference/query-api.md`, `docs/reference/nuget-packages.md`, `docs/guides/sample-blazor-ui.md`, sample Blazor UI pages/components/services, and `src/Hexalith.EventStore.SignalR`.
- Public reconnect callback check: `rg -n "public .*Reconnect|Reconnected|event .*Reconnect|OnReconnectedAsync" src\Hexalith.EventStore.SignalR\EventStoreSignalRClient.cs src\Hexalith.EventStore.SignalR\EventStoreSignalRClientOptions.cs`; only private/internal reconnect handling exists.
- Post-edit overclaim audit command: `rg -n -i "reconnect|rejoin|replay|catch-up|ProjectionChanged|SignalR|If-None-Match|real-time state|live synchronized|guaranteed update|missed updates|missed update|keeps current|stays synchronized|automatic recovery|always current" docs\reference\query-api.md docs\reference\nuget-packages.md docs\guides\sample-blazor-ui.md samples\Hexalith.EventStore.Sample.BlazorUI\Pages\Index.razor samples\Hexalith.EventStore.Sample.BlazorUI\Pages\NotificationPattern.razor samples\Hexalith.EventStore.Sample.BlazorUI\Pages\SilentReloadPattern.razor samples\Hexalith.EventStore.Sample.BlazorUI\Pages\SelectiveRefreshPattern.razor samples\Hexalith.EventStore.Sample.BlazorUI\Components\CounterValueCard.razor samples\Hexalith.EventStore.Sample.BlazorUI\Components\CounterHistoryGrid.razor samples\Hexalith.EventStore.Sample.BlazorUI\Services\CounterQueryService.cs src\Hexalith.EventStore.SignalR\EventStoreSignalRClient.cs src\Hexalith.EventStore.SignalR\EventStoreSignalRClientOptions.cs`.
- Post-edit audit result: remaining matches are corrected contract language, intentional sample/client context, or unrelated SignalR/client internals; `always current` no longer appears in touched sample wording.

### Completion Notes List

- Added connect/reconnect responsibility guidance to the Query API reference, including the canonical signal-only contract, baseline query flow, query-after-signal flow, reconnect/resume re-query responsibility, and current helper callback limitation.
- Aligned the NuGet package guide with the same SignalR boundary, including Do/Do not guidance and current `EventStoreSignalRClientOptions` surface (`AccessTokenProvider`, `RetryPolicy`, `ConfigureHttpConnection`).
- Expanded the sample Blazor UI guide with an initial load, signal receipt, reconnect/resume, and user-triggered refresh table; clarified that the sample demonstrates query-after-signal behavior, not missed-signal replay.
- Tightened sample overview and silent reload page wording so it no longer implies SignalR keeps data always current or catches up after downtime.
- Preserved implementation boundaries: no SignalR runtime, hub protocol, group format, retry, token flow, query contract, Redis, or authorization behavior changed.
- Aspire state checked before edits; key resources were healthy. `sample-blazor-ui` was stopped briefly to release the executable lock for build validation, then restarted and confirmed running healthy.

### File List

- `_bmad-output/implementation-artifacts/post-epic-10-r10a5-client-reconnect-guidance.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/reference/query-api.md`
- `docs/reference/nuget-packages.md`
- `docs/guides/sample-blazor-ui.md`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/Index.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor`

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-02 | 1.0 | Implemented R10-A5 client reconnect guidance, sample wording corrections, validation evidence, and dev handoff bookkeeping. | Codex |
| 2026-05-02 | 0.3 | Advanced elicitation hardened lifecycle-signal boundaries, reconnect timing wording, post-edit overclaim audit, and deferred follow-up routing. | Codex automation |
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

## Advanced Elicitation

- Date/time: 2026-05-02T14:53:57+02:00
- Selected story key: `post-epic-10-r10a5-client-reconnect-guidance`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-10-r10a5-client-reconnect-guidance`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Comparative Analysis Matrix
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction
- Findings summary: The story already preserved the signal-only boundary, but needed stronger guardrails around lifecycle signals, reconnect timing wording, adopter-facing overclaim audits, and follow-up routing if truthful docs require a future reconnect observable.
- Changes applied: Added advanced elicitation hardening notes; tightened AC #6 and #8; added reconnect-as-transport-only scope guidance; expanded audit, docs, sample, boundary, and verification subtasks; added a reconnect-currentness guardrail to Dev Notes; and recorded this dated canonical trace.
- Findings deferred: Dev-story execution must decide whether existing documentation or sample UI text actually requires edits, run the post-edit overclaim audit, and record any needed reconnect callback or connection-state observable as deferred work rather than adding API surface in R10-A5.
- Final recommendation: ready-for-dev

## Verification Status

- `aspire doctor`: pass for .NET SDK and Docker; warnings only for multiple/older dev certificates and deprecated Claude Code MCP config.
- Aspire resources inspected through MCP before edits; `eventstore`, `sample`, `sample-blazor-ui`, Dapr components, and Keycloak were running healthy.
- `npx markdownlint-cli2 "docs/**/*.md" "_bmad-output/implementation-artifacts/post-epic-10-r10a5-client-reconnect-guidance.md"`: failed because the pre-existing story file task list uses two-space nested unordered-list indentation that violates MD007. The touched documentation files were not identified as failures.
- `npx markdownlint-cli2 "docs/reference/query-api.md" "docs/reference/nuget-packages.md" "docs/guides/sample-blazor-ui.md"`: passed, 0 errors.
- `npx lychee --config lychee.toml docs/reference/query-api.md docs/reference/nuget-packages.md docs/guides/sample-blazor-ui.md`: npm could not determine an executable. Direct installed `lychee` was found on PATH.
- `lychee --config lychee.toml docs/reference/query-api.md docs/reference/nuget-packages.md docs/guides/sample-blazor-ui.md`: passed, 19 total links, 18 OK, 0 errors, 1 excluded.
- `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj --no-restore`: first attempt failed because the Aspire-running sample executable locked `Hexalith.EventStore.Sample.BlazorUI.exe`.
- After stopping only the `sample-blazor-ui` Aspire resource, `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj --no-restore`: passed, 0 warnings, 0 errors.
- Documented unit regression tests were attempted in parallel first; `Hexalith.EventStore.Contracts.Tests` passed, while the other parallel test builds hit file locks from concurrent builds and running Aspire resources.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore`: passed, 281 tests.
- After stopping Aspire `sample` and `eventstore` resources to release locked binaries, `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-restore`: passed, 334 tests.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --no-restore`: passed, 63 tests.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --no-restore`: passed, 78 tests.
- Stopped Aspire resources were restarted after validation; `eventstore`, `sample`, and `sample-blazor-ui` were confirmed running healthy.
- SignalR runtime/client source was not modified; `tests/Hexalith.EventStore.SignalR.Tests` was not required for this docs/sample-text story.
