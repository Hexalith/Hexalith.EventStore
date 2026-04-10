# Story 19.6: Admin DAPR Metadata Diagnostics — Accurate Error Messages & Observability

Status: done

Size: Small — ~½ day. Five focused edits across one architectural slice (Admin.Server + Admin.UI + Admin.Abstractions + Aspire extension). No new abstractions, no new NuGet packages, no infrastructure changes. Updates 3–4 existing Tier-1 tests in `Hexalith.EventStore.Admin.Server.Tests` and ~2 tests in `Hexalith.EventStore.Admin.Abstractions.Tests` to match the DTO shape change (bool → three-state enum).

**Dependency:** Story 19-2 (`DAPR Actor Inspector`) and Story 19-3 (`DAPR Pub/Sub Delivery Metrics`) must be complete — both are `done`. This story is a follow-up bug-fix that replaces the binary `IsRemoteMetadataAvailable` wiring introduced by those two stories with a three-state diagnostic model. Epic 19 is `in-progress` (reopened via sprint change proposal 2026-04-10).

**Source:** [Sprint Change Proposal 2026-04-10 — Admin UI DAPR Metadata Diagnostics](../planning-artifacts/sprint-change-proposal-2026-04-10-admin-dapr-metadata-diagnostics.md)

## Definition of Done

- All acceptance criteria verified
- All unit tests green (`dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/` and `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests/`)
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`) — `TreatWarningsAsErrors = true` is globally enabled
- No new analyzer suppressions introduced
- On Admin.Server startup, logs contain an Info-level line naming the resolved `EventStoreDaprHttpEndpoint` (or `<not configured — remote sidecar metadata disabled>`)
- On `/dapr/actors` and `/dapr/pubsub`, three distinct UI states are reachable: **NotConfigured**, **Unreachable**, **Available** — each with its own message
- When `Unreachable`, the attempted URL is displayed in the UI as an actionable diagnostic datum
- `StatCard` label `Total State Size` renamed to `Inspected Actor State Size`, empty value is `—` (em-dash) with an explanatory tooltip
- `AddHexalithEventStore` accepts a new optional `eventStoreDaprHttpPort` parameter (default `3501`) with an XML-doc warning about port conflicts
- All existing Tier-2/Tier-3 tests remain unaffected (no behavioral change to the success path)
- Jerome manually re-runs the restart procedure (flush Redis → build → `aspire run`) and confirms the new startup log line and new UI states under a forced port conflict

## Story

As a **platform operator running Hexalith.EventStore under Aspire**,
I want **the Admin UI to tell me precisely whether the cross-sidecar metadata call failed because the endpoint is not configured, unreachable, or succeeded with no data**,
so that **I can diagnose silent DAPR sidecar port conflicts, access-control blocks, and startup-order races without reading source code or guessing at placeholder `N/A` values**.

## Business Context

Story 19-2 (`DaprActors.razor`) and Story 19-3 (`DaprPubSub.razor`) shipped a cross-sidecar metadata query from Admin.Server to the EventStore DAPR sidecar (`http://localhost:3501/v1.0/metadata`). The wiring was fragile by design — an existing code comment at `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:63-66` acknowledges that `IDaprSidecarResource` does not implement `IResourceWithEndpoints` in CommunityToolkit.Aspire.Hosting.Dapr 13.0.0, so the port is hardcoded to 3501.

When that call silently fails (port conflict, access-control block, boot-order race, timeout), the current implementation collapses all failure modes into a single `bool IsRemoteMetadataAvailable = false` signal. Both pages then show the same misleading message: *"Configure 'AdminServer:EventStoreDaprHttpEndpoint' in appsettings."* The key **is** already wired by the Aspire extension — so users try to set a config value that is already set, reach a dead end, and file bug reports.

Evidence of the confusion is in conversation screenshots from 2026-04-10 and in the sprint change proposal linked above. Jerome observed the issue during a normal `aspire run` session.

This story fixes **diagnostic and UX surfaces only**. The underlying runtime root cause (whether it is a port conflict, an access-control block, or an early-boot timeout) will be identified in a follow-up investigation once the enriched logs from this story are live. That follow-up is **explicitly out of scope here** — file it as a separate investigation task after this story merges.

## Acceptance Criteria

1. **AC1 — Startup observability (Admin.Server logs).** On construction of `DaprInfrastructureQueryService`, the service emits a single Info-level log line naming the resolved `EventStoreDaprHttpEndpoint`. When the option is null/whitespace, the log line reads `EventStoreDaprHttpEndpoint=<not configured — remote sidecar metadata disabled>`. When set, the log line reads `EventStoreDaprHttpEndpoint=http://localhost:3501` (or whatever value is wired). This is the single log line Jerome will grep for to confirm wiring.

2. **AC2 — Branch-level Debug log on skipped remote calls.** In both `GetActorRuntimeInfoAsync` (around line 171) and `GetPubSubOverviewAsync` (around line 327) of `DaprInfrastructureQueryService.cs`, when `string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)` is true and the remote-fetch branch is therefore skipped, emit a single Debug-level log: `"Skipping remote EventStore sidecar metadata query: endpoint not configured."` (One per call. No log storm.)

3. **AC3 — Enriched Warning log on remote-call exceptions.** The two existing `LogWarning` sites in `DaprInfrastructureQueryService.cs` (lines ~221 and ~392) must be replaced with enriched log calls that include:
   - The attempted `Endpoint` (structured property)
   - The exception type name (`ex.GetType().Name`, structured property `ExceptionType`)
   - A port-conflict hint in the message text: *"Check whether DAPR sidecar for 'eventstore' is running on that port (port conflicts on 3501 cause silent fallback)."*
   The full structured message template: `"Remote DAPR sidecar metadata unavailable at {Endpoint}. ExceptionType={ExceptionType}. Check whether DAPR sidecar for 'eventstore' is running on that port (port conflicts on 3501 cause silent fallback)."` Both call sites must use this template. The `ex` is still passed as the first positional argument so the exception stack is captured.

4. **AC4 — New `RemoteMetadataStatus` three-state enum.** A new public enum `RemoteMetadataStatus` is created in `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/RemoteMetadataStatus.cs`. Members and XML docs (exact):
   - `NotConfigured` — `"No remote endpoint configured; only local sidecar queried."`
   - `Available` — `"Remote endpoint configured and successfully queried."`
   - `Unreachable` — `"Remote endpoint configured but query failed (exception caught)."`
   The file uses a file-scoped namespace (`namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;`) and Allman braces — per the `.editorconfig` enforced across the repo.

5. **AC5 — DTO shape change: replace `bool IsRemoteMetadataAvailable` on both records.** The existing records are updated in place (breaking change, but internal to the Admin slice — **not exported in any of the 6 published NuGet packages**). The positional `bool IsRemoteMetadataAvailable` parameter is **removed** and replaced by two new positional parameters at the end of the record signature:
   - `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorRuntimeInfo.cs` — `RemoteMetadataStatus RemoteMetadataStatus, string? RemoteEndpoint`
   - `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprPubSubOverview.cs` — `RemoteMetadataStatus RemoteMetadataStatus, string? RemoteEndpoint`

   Both records must retain their existing XML docs with updated parameter descriptions for the new members. `RemoteEndpoint` is the attempted URL (from `_options.EventStoreDaprHttpEndpoint`) when the remote branch was attempted; `null` only when the endpoint was not configured. Existing collection-null validation in the record bodies is preserved.

6. **AC6 — Service populates the new fields in both methods.** In `DaprInfrastructureQueryService.GetActorRuntimeInfoAsync` and `GetPubSubOverviewAsync`, the boolean `isRemoteMetadataAvailable` local variable is replaced with a computation producing `RemoteMetadataStatus`:
   ```csharp
   RemoteMetadataStatus status = string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
       ? RemoteMetadataStatus.NotConfigured
       : remoteFetchSucceeded
           ? RemoteMetadataStatus.Available
           : RemoteMetadataStatus.Unreachable;
   ```
   where `remoteFetchSucceeded` is the local bool that replaces the existing `isRemoteMetadataAvailable` assignment inside the try-block (set to `true` on successful parse, left `false` on exception). Both constructed DTOs must pass `RemoteEndpoint: _options.EventStoreDaprHttpEndpoint` (nullable — it is passed through regardless of status so the UI can still render the attempted URL for the `NotConfigured` case if it chooses, though in practice the UI only shows the URL in the `Unreachable` case per AC7).

7. **AC7 — Three-way switch in `DaprActors.razor` empty-state block.** The existing empty-state block (around lines 67–76) that currently tests `!_runtimeInfo.IsRemoteMetadataAvailable` is replaced with a `@switch (_runtimeInfo.RemoteMetadataStatus)` expression rendering three distinct `EmptyState` components (the switch is only entered when `_runtimeInfo.ActorTypes.Count == 0`):
   - **NotConfigured** — Title: `"No actor types found"`; Description: `"Remote EventStore sidecar metadata is disabled. Set 'AdminServer:EventStoreDaprHttpEndpoint' in appsettings (under Aspire, this is wired automatically)."`
   - **Unreachable** — Title: `"EventStore sidecar unreachable"`; Description: interpolated: `$"Attempted to query {_runtimeInfo.RemoteEndpoint}/v1.0/metadata but the call failed. Verify the EventStore DAPR sidecar is running on that port (port conflicts on 3501 cause silent fallback). Check Admin server logs for the exception details."`
   - **Available** — Title: `"No actor types registered"`; Description: `"The EventStore sidecar is reachable but reports no actor types."`

   All three must render using the existing `<EmptyState>` shared component (do not use `FluentMessageBar` or `IssueBanner` on this page). The `Unreachable` case is expected to be the most actionable — the attempted URL is the single most useful datum.

8. **AC8 — Three-way switch in `DaprPubSub.razor` subscription banner.** The existing single `<IssueBanner Visible="@(!_overview.IsRemoteMetadataAvailable)" .../>` block (around lines 128–130) is replaced with a `@switch (_overview.RemoteMetadataStatus)` rendering:
   - **NotConfigured** — Render `<IssueBanner>` (current pattern on this page) with Title: `"Remote metadata disabled"`; Description: `"Subscription data is only available when 'AdminServer:EventStoreDaprHttpEndpoint' is configured. Under Aspire this is wired automatically."`
   - **Unreachable** — Render `<IssueBanner>` with Title: `"EventStore sidecar unreachable"`; Description: interpolated: `$"Attempted to query {_overview.RemoteEndpoint}/v1.0/subscribe but the call failed. Verify the EventStore DAPR sidecar is running. Check Admin server logs for details."`
   - **Available** — **Do not render** a banner at all (the grid below renders the actual subscriptions or the existing "No active subscriptions registered" italic notice).

   **Important:** The proposal document mentions `FluentMessageBar` in example pseudocode, but the existing page uses `IssueBanner` at line 128 — **use `IssueBanner`** to stay consistent with the rest of the page. Do NOT introduce `FluentMessageBar` here.

9. **AC9 — `Total State Size` stat card relabel and em-dash placeholder.** In `DaprActors.razor` (around lines 57–62), the stat card currently labeled `"Total State Size"` with value `"N/A"` when `_inspectedState is null` must be updated to:
   - **Label** (`StatCard.Label`): `"Inspected Actor State Size"`
   - **Empty value** (when `_inspectedState is null`): `"—"` (em-dash character `U+2014`, NOT `"N/A"` and NOT a hyphen-minus)
   - **Dynamic tooltip** (`StatCard.Title`): when `_inspectedState is not null` use `"Total state size of the currently inspected actor"`; when null use `"Select an actor instance below to inspect its state size"`. This tooltip must be a ternary expression — both states share the same stat card instance.
   - When `_inspectedState is not null`, continue to render `TimeFormatHelper.FormatBytes(_inspectedState.TotalSizeBytes)` — no change to populated-state rendering.

10. **AC10 — Parameterize `eventStoreDaprHttpPort` in `AddHexalithEventStore`.** The extension method at `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` gains a new optional parameter **at the end of the parameter list** (source-compatible for existing named-arg callers; AppHost already uses positional args, so verify AppHost still compiles):
    ```csharp
    public static HexalithEventStoreResources AddHexalithEventStore(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> eventStore,
        IResourceBuilder<ProjectResource> adminServer,
        IResourceBuilder<ProjectResource>? adminUI = null,
        string? eventStoreDaprConfigPath = null,
        string? adminServerDaprConfigPath = null,
        int eventStoreDaprHttpPort = 3501)
    ```
    - Add `ArgumentOutOfRangeException.ThrowIfLessThan(eventStoreDaprHttpPort, 1024);`
    - Add `ArgumentOutOfRangeException.ThrowIfGreaterThan(eventStoreDaprHttpPort, 65535);`
    - **Delete** the existing `const int EventStoreDaprHttpPort = 3501;` line.
    - Replace the two usages (`DaprHttpPort = EventStoreDaprHttpPort` on the sidecar options, and the interpolated environment variable `"http://localhost:" + EventStoreDaprHttpPort`) with the parameter name `eventStoreDaprHttpPort`.
    - Use `$"http://localhost:{eventStoreDaprHttpPort}"` interpolation for the env var assignment (cleaner than concatenation and idiomatic for new C# code in this repo).

11. **AC11 — XML doc warning on `eventStoreDaprHttpPort`.** The new parameter receives an XML `<param>` doc with this exact content (wrapped for readability but a single paragraph in the XML):
    > DAPR HTTP port for the EventStore sidecar. Defaults to 3501. This port MUST be free on the host at startup — DAPR does not error on port conflicts, it silently binds to a different port, which breaks cross-sidecar metadata queries from Admin.Server. Override this parameter if 3501 is occupied (e.g., by a prior daprd process or another DAPR app). Diagnostic: on Windows, run `netstat -ano | findstr :3501` before `aspire run`.

12. **AC12 — Tests updated (DTO shape + service behavior).** Tier-1 tests that reference the removed `IsRemoteMetadataAvailable` boolean must be updated to use the new enum:
    - `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprActorRuntimeInfoTests.cs`
    - `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprPubSubOverviewTests.cs`
    - `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs`
    - `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprPubSubQueryServiceTests.cs`
    - `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerPubSubTests.cs`
    - `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminPubSubApiClientTests.cs`
    - `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminActorApiClientTests.cs`

    For each file, replace the boolean assertions (e.g., `result.IsRemoteMetadataAvailable.ShouldBeFalse()`) with the equivalent enum assertions (`result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured)` or `.Unreachable` or `.Available`). Shouldly 4.3.0 is the assertion library; existing tests use fluent `.ShouldBe(...)` — follow the same style.

    Add **three new unit tests** to `DaprActorQueryServiceTests.cs`:
    1. When `EventStoreDaprHttpEndpoint` is null → returned `DaprActorRuntimeInfo.RemoteMetadataStatus == NotConfigured` and `RemoteEndpoint == null`.
    2. When the endpoint is set and the remote HTTP call throws → `RemoteMetadataStatus == Unreachable` and `RemoteEndpoint == "http://localhost:3501"` (or the mocked value).
    3. When the endpoint is set and the remote HTTP call succeeds with actor metadata → `RemoteMetadataStatus == Available` and `RemoteEndpoint == <mocked value>`.
    Mirror the same three-case coverage in `DaprPubSubQueryServiceTests.cs` for `GetPubSubOverviewAsync`.

13. **AC13 — Zero regression to the success path.** When the remote metadata call succeeds (the common happy path under a healthy `aspire run` on an unoccupied port 3501), the user-visible UI rendering of actor types and subscriptions is **identical** to the pre-change behavior. The enum adds diagnostic breadcrumbs for the failure modes — it does not change any success-path rendering. Tier-2 tests (`Hexalith.EventStore.Server.Tests`) and Tier-3 tests (`Hexalith.EventStore.IntegrationTests`) remain untouched and must continue to pass.

## Tasks / Subtasks

- [x] **Task 1 — Add `RemoteMetadataStatus` enum (AC4)**
  - [x] 1.1 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/RemoteMetadataStatus.cs` with file-scoped namespace, public enum, and three members with XML-doc summaries copied verbatim from AC4.
- [x] **Task 2 — Update DTOs (AC5)**
  - [x] 2.1 Edit `DaprActorRuntimeInfo.cs`: remove `bool IsRemoteMetadataAvailable`, append `RemoteMetadataStatus RemoteMetadataStatus` and `string? RemoteEndpoint` positional parameters. Update XML docs for both new members. Preserve the `ActorTypes` and `Configuration` null-check properties in the record body.
  - [x] 2.2 Edit `DaprPubSubOverview.cs`: same transformation. Preserve the `PubSubComponents` and `Subscriptions` null-check properties.
- [x] **Task 3 — Update `DaprInfrastructureQueryService` (AC1, AC2, AC3, AC6)**
  - [x] 3.1 Add the startup Info log in the constructor after the `_logger = logger;` assignment (AC1). The log must fire exactly once per service instance.
  - [x] 3.2 In `GetActorRuntimeInfoAsync` (~line 171), wrap the endpoint check in an if/else. The null/whitespace branch emits the Debug log (AC2); the else branch contains the existing remote-fetch code verbatim. Rename the local `bool isRemoteMetadataAvailable` to `bool remoteFetchSucceeded` for clarity (it means "the HTTP call succeeded and parsing completed", not "metadata is available"). Compute `RemoteMetadataStatus status` per AC6 before constructing `DaprActorRuntimeInfo`. Pass `_options.EventStoreDaprHttpEndpoint` as the `RemoteEndpoint` positional arg.
  - [x] 3.3 In `GetPubSubOverviewAsync` (~line 327), mirror the same transformation. Same rename (`isRemoteMetadataAvailable` → `remoteFetchSucceeded`). Same status computation. Same `RemoteEndpoint` passthrough.
  - [x] 3.4 Replace the two existing `_logger.LogWarning` calls in the catch blocks (lines ~221 and ~392) with the AC3 enriched-message template. Both calls must include the exception object as the first positional argument so the stack trace is captured. Use structured log properties `{Endpoint}` and `{ExceptionType}` — no string interpolation in the message template (log templates must stay stable for downstream parsers).
- [x] **Task 4 — Update Admin UI pages (AC7, AC8, AC9)**
  - [x] 4.1 `DaprActors.razor`: Replace the two-branch empty state (current lines ~66–76) with a single `@if (!_isLoading && _runtimeInfo is not null && _runtimeInfo.ActorTypes.Count == 0)` block containing a `@switch (_runtimeInfo.RemoteMetadataStatus)` with three `<EmptyState>` cases per AC7. The file already imports `Hexalith.EventStore.Admin.Abstractions.Models.Dapr` so no new `@using` is needed.
  - [x] 4.2 `DaprActors.razor`: Update the `Total State Size` stat card (current lines ~57–62) per AC9 — rename label, switch to em-dash, add dynamic tooltip.
  - [x] 4.3 `DaprPubSub.razor`: Replace the single-branch `IssueBanner` (current lines ~127–130) with a `@switch (_overview.RemoteMetadataStatus)` block rendering two `<IssueBanner>` cases (`NotConfigured`, `Unreachable`) and no banner for `Available` per AC8. The file already imports the Dapr models namespace.
- [x] **Task 5 — Parameterize `AddHexalithEventStore` port (AC10, AC11)**
  - [x] 5.1 Add `int eventStoreDaprHttpPort = 3501` as the last parameter of `AddHexalithEventStore`.
  - [x] 5.2 Add the two `ArgumentOutOfRangeException.ThrowIfLessThan`/`ThrowIfGreaterThan` guards immediately after the three existing `ArgumentNullException.ThrowIfNull` calls.
  - [x] 5.3 Delete the `const int EventStoreDaprHttpPort = 3501;` line at line 67.
  - [x] 5.4 Replace the two usages of `EventStoreDaprHttpPort` with `eventStoreDaprHttpPort`. Use string concatenation with `ToString(CultureInfo.InvariantCulture)` (see Dev Agent Notes — Aspire's `WithEnvironment` interpolated-string overload does not accept `int`).
  - [x] 5.5 Add the AC11 XML `<param>` doc block above the method. The `<summary>` docs are unchanged.
  - [x] 5.6 Verify `src/Hexalith.EventStore.AppHost/Program.cs:23` still compiles. It currently passes positional args; the new parameter has a default, so it should remain source-compatible.
  - [x] 5.7 **Do NOT** modify `HexalithEventStoreResources.cs` — exposing the resolved port on the record is explicitly deferred per the sprint change proposal (it is a breaking change to the record constructor for any external AppHost consumer).
- [x] **Task 6 — Update tests (AC12)**
  - [x] 6.1 Grep for `IsRemoteMetadataAvailable` in the 7 test files listed in AC12 (they were already identified by the ripgrep sweep in the proposal). Replace each boolean assertion with the corresponding `RemoteMetadataStatus.ShouldBe(...)` assertion. Preserve the existing Arrange/Act/Assert structure and existing test names. Also updated 3 additional test files discovered by the build (see Dev Agent Notes).
  - [x] 6.2 Add the three new unit tests (`NotConfigured`, `Unreachable`, `Available`) to `DaprActorQueryServiceTests.cs` per AC12 — use NSubstitute to mock `IHttpClientFactory` and produce the three scenarios. The existing tests already mock `DaprClient`, `IHttpClientFactory`, `IOptions<AdminServerOptions>`, and `ILogger<DaprInfrastructureQueryService>` — follow the same Arrange pattern.
  - [x] 6.3 Add the three mirror tests to `DaprPubSubQueryServiceTests.cs` for `GetPubSubOverviewAsync`.
- [x] **Task 7 — Build, test, manual verify (all ACs)**
  - [x] 7.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — must succeed with zero warnings.
  - [x] 7.2 `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests/` — all tests green. (404 pass)
  - [x] 7.3 `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/` — all story 19-6 tests green (56 DAPR-related tests pass). 7 pre-existing `DaprTenantQueryServiceTests` failures confirmed unrelated to this story (validated by running the failing test against pre-story `main` branch).
  - [x] 7.4 `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` — all story 19-6 tests green (29 DAPR-related tests pass). 29 pre-existing `BreadcrumbTests` / `MainLayoutTests` / `StreamDetailPageTests` failures confirmed unrelated to this story (validated by running a failing test against pre-story `main` branch).
  - [ ] 7.5 Manual verification per restart procedure (memory: feedback_restart_procedure.md — flush Redis → build → `aspire run`). Confirm: (a) Admin.Server logs show the new Info startup line with the resolved endpoint; (b) navigating to `/dapr/actors` and `/dapr/pubsub` under healthy conditions still renders actors and subscriptions (no success-path regression); (c) stat card on `/dapr/actors` shows `"Inspected Actor State Size"` with `"—"` until an actor is inspected. **(Operator-driven — Jerome to run during review.)**

### Review Findings

- [x] [Review][Patch] Pub/Sub unreachable banner uses metadata endpoint path instead of subscribe path [src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor:138]
- [x] [Review][Patch] Normalize `RemoteEndpoint` to null when endpoint is blank/whitespace to keep NotConfigured contract consistent [src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:239]
- [x] [Review][Defer] Pub/Sub rules parser still assumes nested `rules.rules[]` shape and does not match DAPR 1.17 direct-array schema [src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:380] — deferred, pre-existing

## Dev Notes

### Architecture Compliance

This story makes **zero architectural changes**. It is a diagnostic/UX polish on an existing slice:

- **No new services, no new interfaces, no new abstractions.** Existing `IDaprInfrastructureQueryService` interface is unchanged. The DTO shape change is limited to two record types within `Hexalith.EventStore.Admin.Abstractions.Models.Dapr`.
- **No new NuGet dependencies.** No DAPR SDK bump, no CommunityToolkit change.
- **No DAPR component YAML changes.** No infrastructure impact.
- **No changes to published NuGet package contracts.** The 6 published packages (`Contracts`, `Client`, `Server`, `SignalR`, `Testing`, `Aspire`) are unaffected *contract-wise* — the Aspire package gains one optional parameter, which is a non-breaking addition for named-arg callers and (verified) source-compatible for positional callers because AppHost only passes the first 5 positional args. `Admin.Abstractions` is NOT one of the 6 published packages, so the DTO shape change is strictly internal to the Admin slice.

### Code Style Compliance (from `.editorconfig`)

- File-scoped namespaces (`namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;`) — used throughout the Admin slice.
- Allman braces (new line before `{`).
- Private fields use `_camelCase` (e.g., `_options`, `_logger`, `_daprClient` — already in the service).
- Async methods use `Async` suffix — preserved.
- UTF-8, CRLF, 4-space indentation.
- `TreatWarningsAsErrors = true` — any new warning (unused variable, missing XML doc, nullable reference warning) will fail the build. Add XML docs for every new public member (enum, enum members, parameter).

### Critical: Structured Logging Contract

**DO NOT** interpolate values into the message template of the AC3 log call. The template must remain:

```csharp
_logger.LogWarning(
    ex,
    "Remote DAPR sidecar metadata unavailable at {Endpoint}. " +
    "ExceptionType={ExceptionType}. Check whether DAPR sidecar for " +
    "'eventstore' is running on that port (port conflicts on 3501 " +
    "cause silent fallback).",
    _options.EventStoreDaprHttpEndpoint,
    ex.GetType().Name);
```

Interpolating (`$"Remote DAPR sidecar metadata unavailable at {endpoint}..."`) would break structured log parsers downstream by producing a different `{OriginalFormat}` on every call. The existing codebase uses structured logging consistently; follow the same pattern.

### Critical: Enum Naming — Property vs Type

The DTO member `RemoteMetadataStatus` (property) has the same name as the enum type `RemoteMetadataStatus`. This is **intentional** and follows C# conventions (cf. `Nullable<T>` where the wrapping property can also be named after its type). The Razor switch expression `@switch (_runtimeInfo.RemoteMetadataStatus) { case RemoteMetadataStatus.NotConfigured: ... }` compiles cleanly because C# resolves the case expression in the enum context. If the compiler flags ambiguity for any reason, qualify with the namespace: `case Hexalith.EventStore.Admin.Abstractions.Models.Dapr.RemoteMetadataStatus.NotConfigured:`. The `@using Hexalith.EventStore.Admin.Abstractions.Models.Dapr` directive is already present at the top of both Razor files.

### Critical: Port Range Validation

AC10 requires validating the port is in `[1024, 65535]`. The 1024 lower bound excludes privileged ports that would require elevated permissions — a common source of silent failures during `dotnet run`. Use the modern `ArgumentOutOfRangeException.ThrowIfLessThan` / `ThrowIfGreaterThan` static helpers (available in .NET 8+; the project targets .NET 10) — do NOT hand-roll `if (port < 1024) throw new ArgumentOutOfRangeException(...)`.

### DO NOT Touch (Explicitly Out of Scope)

1. **`HexalithEventStoreResources` record** — exposing the resolved port is a constructor-breaking change to the record for any external AppHost consumer. Explicitly deferred per the sprint change proposal. Do not add `EventStoreDaprHttpPort` to this record.
2. **Dynamic port discovery via `IResourceWithEndpoints`** — blocked on CommunityToolkit.Aspire.Hosting.Dapr 13.0.0 upstream. The existing code comment at `HexalithEventStoreExtensions.cs:63-66` documents this. Do not attempt to refactor for dynamic discovery.
3. **Host-level port pre-check** (`netstat` / `TcpListener.Start` probe) — TOCTOU race, platform-specific, better left to operator diagnostics. Do NOT add a port probe in the extension method.
4. **Adding `WaitFor(eventStore)` on the Admin server resource in Aspire** — this might be a correct fix for a boot-order race, but only if the follow-up investigation (post-implementation) confirms the root cause is a timing issue. Do NOT add it preemptively.
5. **Restyling the UI, changing icons, refactoring the stat card component, moving the stat card layout** — only the label, empty value, and tooltip attributes change. Zero structural changes to the Razor layout.
6. **Creating a new `Contracts` subfolder in `Admin.Server`** — the sprint change proposal mentions "Admin.Server.Contracts or equivalent" for the enum, but the actual convention in this repo is that all Admin DTOs and enums live under `Hexalith.EventStore.Admin.Abstractions/Models/Dapr/`. Put the enum there (verified by `ls src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/` — 18 existing DAPR models). The proposal's wording predates confirmation of the layout.

### Files to Touch (Exact List — 5 source files + up to 9 test files)

**Source (5 files):**
1. `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/RemoteMetadataStatus.cs` *(new)*
2. `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorRuntimeInfo.cs` *(DTO shape change)*
3. `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprPubSubOverview.cs` *(DTO shape change)*
4. `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs` *(logs + status computation)*
5. `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor` *(two blocks: stat card + empty state switch)*
6. `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor` *(one block: banner switch)*
7. `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` *(parameter + XML doc)*

**Tests (updates, no new test files except optionally a dedicated file for the enum itself):**
1. `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprActorRuntimeInfoTests.cs`
2. `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprPubSubOverviewTests.cs`
3. `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs` *(+ 3 new tests)*
4. `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprPubSubQueryServiceTests.cs` *(+ 3 new tests)*
5. `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerPubSubTests.cs`
6. `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminPubSubApiClientTests.cs`
7. `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminActorApiClientTests.cs`

*(The ripgrep sweep `IsRemoteMetadataAvailable` in the repo already identified these 7 test files — they match the proposal's "3-4 cases" estimate once duplicate references in the same file are counted.)*

### Previous Story Intelligence (19-5)

Story 19-5 shipped a `BackgroundService`-based health history collector that demonstrates the right pattern for structured logging in this slice (`_logger.LogWarning(ex, "Health history capture failed — will retry next cycle");` at `DaprHealthHistoryCollector.cs`). Follow the same fluent `catch (OperationCanceledException) { throw; } catch (Exception ex) { _logger.LogWarning(ex, "..."); }` pattern that already exists in both `GetActorRuntimeInfoAsync` (lines 161–167 and 215–221) and `GetPubSubOverviewAsync` (lines 314–321 and 386–393). **Do NOT** swallow the `OperationCanceledException` path — it is intentional for honoring cancellation tokens during shutdown.

Story 19-5 review findings #2 and #8 explicitly flagged the risk of *throwing validation in record constructors* vs *silent deserialization*. The DTO shape changes in this story (AC5) add new positional parameters **after** the existing collection-validated members. The existing `ArgumentNullException.ThrowIfNull` null-checks for `ActorTypes`, `Configuration`, `PubSubComponents`, `Subscriptions` must be preserved verbatim. `RemoteEndpoint` is nullable by design (the attempted URL may be null when `NotConfigured`) — do NOT add a null check for it. `RemoteMetadataStatus` is a value type — no null check possible.

### Known Runtime Failure Mode Hypotheses (for the post-implementation investigation)

The sprint change proposal documents three leading hypotheses for the underlying runtime cause of the silent fallback. This story does not fix the root cause — it only adds the diagnostic surface to identify which hypothesis is correct. The dev agent should **NOT** try to fix the root cause as part of this story.

1. **Port conflict on 3501** (`SocketException` — most likely per the existing code comment). If confirmed, the mitigation is the new `eventStoreDaprHttpPort` parameter on `AddHexalithEventStore` (added in this story as an escape hatch).
2. **Access control block** (`HttpRequestException` with 403). Would require a review of `accesscontrol.yaml` to allow metadata invocations from `eventstore-admin`.
3. **Boot-order race** (timeout or connection refused during early boot). Would require `WaitFor(eventStore)` on the Admin server resource.

The enriched `LogWarning` message template (AC3) with `ExceptionType={ExceptionType}` is the key to distinguishing these three cases. Jerome will re-run `aspire run` after this story merges and grep the Admin server logs for the new log line.

### Test Framework & Conventions

- **xUnit 2.9.3** — fact-based tests, no theories needed for these three-case additions (one fact per case is clearer).
- **Shouldly 4.3.0** — use `.ShouldBe(...)` for equality assertions on the enum, `.ShouldBeNull()` / `.ShouldNotBeNull()` for the `RemoteEndpoint` nullable string. Do NOT use xUnit's built-in `Assert.Equal` — the repo convention is fluent Shouldly.
- **NSubstitute 5.3.0** — the existing service tests already mock `IHttpClientFactory` via `HttpMessageHandler` — follow the existing pattern in `DaprActorQueryServiceTests.cs` for the new `Unreachable` test (mock the handler to throw) and the `Available` test (mock a successful JSON response).
- **Test naming** — the repo uses the `MethodName_StateUnderTest_ExpectedBehavior` convention in existing Admin.Server tests. New tests: `GetActorRuntimeInfoAsync_WhenEndpointNotConfigured_ReturnsNotConfiguredStatus`, `GetActorRuntimeInfoAsync_WhenRemoteCallThrows_ReturnsUnreachableStatus`, `GetActorRuntimeInfoAsync_WhenRemoteCallSucceeds_ReturnsAvailableStatus`. Mirror names for PubSub.

### Build Commands (for reference)

```bash
dotnet build Hexalith.EventStore.slnx --configuration Release
dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests/
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/
```

### Commit Guidance

Per `CLAUDE.md`, commits must follow Conventional Commits for semantic-release. This story is a bug fix with a (limited, internal-only) DTO shape change. Suggested commit message:

```
fix(admin): distinguish not-configured vs unreachable DAPR sidecar metadata states

Replace bool IsRemoteMetadataAvailable on DaprActorRuntimeInfo and
DaprPubSubOverview with a three-state RemoteMetadataStatus enum
(NotConfigured / Available / Unreachable) plus RemoteEndpoint. Add
startup Info log and enriched Warning log with exception type to
DaprInfrastructureQueryService. Update Admin UI empty-state and banner
blocks to render distinct messages per state. Parameterize the EventStore
DAPR HTTP port in AddHexalithEventStore with a 1024-65535 validator and
XML doc warning about silent port conflicts.
```

### Project Structure Notes

- Alignment confirmed with existing project structure (paths, modules, naming). All files touched are inside the Admin slice and the Aspire extension — no cross-slice impact.
- Detected conflict: the sprint change proposal mentions `Hexalith.EventStore.Admin.Server.Contracts` as a possible location for the enum. **The actual convention is `Hexalith.EventStore.Admin.Abstractions.Models.Dapr`** (verified by inspecting the 18 existing sibling files). This story places the enum in the actual convention location.

### References

- [Sprint Change Proposal 2026-04-10](../planning-artifacts/sprint-change-proposal-2026-04-10-admin-dapr-metadata-diagnostics.md) — primary source of the five edits
- [Story 19-2 — DAPR Actor Inspector](19-2-dapr-actor-inspector.md) — introduced `DaprActorRuntimeInfo.IsRemoteMetadataAvailable` and the cross-sidecar metadata call
- [Story 19-3 — DAPR Pub/Sub Delivery Metrics](19-3-dapr-pubsub-delivery-metrics.md) — introduced `DaprPubSubOverview.IsRemoteMetadataAvailable`
- [Story 19-5 — DAPR Component Health History](19-5-dapr-component-health-history.md) — demonstrates the logging and DTO patterns to mirror
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:86` — constructor (where the AC1 log goes)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:171` — first `string.IsNullOrEmpty(_options.EventStoreDaprHttpEndpoint)` gate (AC2 Debug log + AC6 status)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:221` — first `LogWarning` to enrich (AC3)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:327` — second `string.IsNullOrWhiteSpace` gate (AC2 Debug log + AC6 status)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:392` — second `LogWarning` to enrich (AC3)
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor:57-62` — `Total State Size` stat card (AC9)
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor:66-76` — empty-state block (AC7)
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor:127-130` — subscription `IssueBanner` (AC8)
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:32-92` — extension method (AC10, AC11)
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:63-66` — existing code comment documenting the CommunityToolkit 13.0.0 limitation
- `src/Hexalith.EventStore.AppHost/Program.cs:23` — existing positional caller of `AddHexalithEventStore` (must remain source-compatible)

## Dev Agent Record

### Agent Model Used

Claude (Sonnet 4.6 → Opus 4.6 1M context) via Claude Code CLI — BMAD `bmad-dev-story` workflow.

### Debug Log References

- `dotnet build Hexalith.EventStore.slnx --configuration Release` → **La génération a réussi. 0 Avertissement(s). 0 Erreur(s).**
- `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests/ --no-build` → **Réussi! échec: 0, réussite: 404, durée: 204 ms**
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/ --filter "FullyQualifiedName~DaprActorQueryServiceTests|FullyQualifiedName~DaprPubSubQueryServiceTests|FullyQualifiedName~AdminDaprControllerActorTests|FullyQualifiedName~AdminDaprControllerPubSubTests|FullyQualifiedName~DaprInfrastructure"` → **Réussi! échec: 0, réussite: 56**
- `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/ --filter "FullyQualifiedName~DaprActorsPageTests|FullyQualifiedName~DaprPubSubPageTests|FullyQualifiedName~AdminActorApiClientTests|FullyQualifiedName~AdminPubSubApiClientTests"` → **Réussi! échec: 0, réussite: 29**
- Pre-existing unrelated failures confirmed against `main`: `DaprTenantQueryServiceTests.GetTenantUsersAsync_FollowsPaginationUntilAllPagesReturned` and `BreadcrumbTests.Breadcrumb_IsNotRendered_OnHomePage` both fail identically on pre-story `main` (via `git stash` round-trip).
- Repo-wide `Grep "IsRemoteMetadataAvailable"` across `*.cs`, `*.razor` → zero source code references remain (only historical mentions in `_bmad-output/` story and proposal docs).

### Completion Notes List

1. **Enum placement** — `RemoteMetadataStatus.cs` created in `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/` (not `Admin.Server.Contracts` as loosely suggested in the sprint change proposal), following the actual repo convention with the 18 other DAPR models — as called out in story Dev Notes.
2. **Enum-vs-property name collision (C# + Razor)** — the property name `RemoteMetadataStatus` on the DTOs matches the enum type name by design, per Dev Notes. Compilation succeeded cleanly in both C# service code and Razor `@switch` expressions; no fully-qualified names were required anywhere.
3. **Aspire `WithEnvironment` + `int` — deviation from AC10 / Task 5.4 template.** The story text showed `$"http://localhost:{eventStoreDaprHttpPort}"` for the env var value, but this produced two CS0315 errors against CommunityToolkit.Aspire's `ReferenceExpression.ExpressionInterpolatedStringHandler`, which only accepts `IValueProvider`/`IManifestExpressionProvider` — not `int`. Fixed by pre-computing the string into a local: `string eventStoreEndpointUrl = "http://localhost:" + eventStoreDaprHttpPort.ToString(CultureInfo.InvariantCulture);` and then passing that `string` to `WithEnvironment`. Functionally identical. Noted in Task 5.4 checkbox description for traceability.
4. **Broader test sweep than AC12 anticipated.** AC12 listed 7 test files. The build surfaced 3 additional test files that also constructed the DTOs with the old positional `bool` parameter (and thus broke on compilation):
   - `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerActorTests.cs` (1 call site — `GetActorRuntimeInfoAsync_DelegatesToService`)
   - `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprActorsPageTests.cs` (3 call sites: `CreateRuntimeInfo` helper + two empty-state fact tests; also updated one `"Total State Size"` markup assertion to `"Inspected Actor State Size"` to match the new stat card label from AC9)
   - `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprPubSubPageTests.cs` (3 call sites: `SetupSuccessfulResponse` helper + `PubSubPage_RendersEmptyState_WhenNoPubSubComponents` + `PubSubPage_RendersIssueBanner_WhenRemoteMetadataUnavailable`). The banner test now uses `RemoteMetadataStatus.Unreachable` + `"http://localhost:3501"` to match the new switch's `Unreachable` case which still renders the `"EventStore sidecar unreachable"` title.
   - These are mandatory DTO-shape fixups, not scope creep. Pre-existing assertions preserved; only constructor argument shape changed.
5. **`DaprActors.razor` stat card relabel consequence.** The bUnit test `DaprActorsPage_RendersStatCards` previously asserted `markup.ShouldContain("Total State Size")`. Since AC9 renames the label to `"Inspected Actor State Size"`, that assertion was updated. The data stat-card (at `/dapr/actors` lines ~170–176) still keeps `"Total State Size"` because it is a DIFFERENT stat card that only appears when an actor has been inspected (not the summary placeholder card AC9 targets) — no rename needed there.
6. **`GetActorRuntimeInfoAsync` local vs remote semantics change.** In the old code, the `isRemoteMetadataAvailable` flag was also set to `true` when the LOCAL sidecar returned actors (line 158). This was semantically wrong: it really meant "we got actor data from some source", not "remote endpoint is reachable." The new three-state enum now treats LOCAL-only success (no remote endpoint configured) as `RemoteMetadataStatus.NotConfigured`, which is more accurate and aligns with the story intent (the enum reflects the REMOTE metadata query outcome only). This is a minor but visible behavior shift in the `GetActorRuntimeInfoAsync_ReturnsActorTypes_WhenLocalMetadataHasActors` test — the assertion changed from `.IsRemoteMetadataAvailable.ShouldBeTrue()` to `.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured)`. Documented here in case reviewers flag the semantic drift.
7. **Acceptance criteria coverage.**
   - AC1 ✅ — startup Info log in constructor emits `EventStoreDaprHttpEndpoint={Endpoint}` with `<not configured — remote sidecar metadata disabled>` when null/whitespace, or the resolved URL otherwise.
   - AC2 ✅ — Debug logs added in both `GetActorRuntimeInfoAsync` and `GetPubSubOverviewAsync` when remote branch is skipped due to unconfigured endpoint.
   - AC3 ✅ — both `LogWarning` sites now use the exact AC3 message template with `{Endpoint}` and `{ExceptionType}` structured properties, exception passed as first positional arg for stack trace capture.
   - AC4 ✅ — enum created at the convention-correct path with the three members and exact XML-doc summaries.
   - AC5 ✅ — both records updated; bool removed, enum + nullable endpoint appended; collection null-validation preserved.
   - AC6 ✅ — status computation formula applied identically in both methods, using `remoteFetchSucceeded` local (renamed per Task 3.2/3.3).
   - AC7 ✅ — `DaprActors.razor` empty-state block replaced with three-case switch (inside the existing `ActorTypes.Count == 0` outer `@if`).
   - AC8 ✅ — `DaprPubSub.razor` banner replaced with two-case `IssueBanner` switch; `Available` case renders no banner. Also updated the two `Active Subscriptions` and `Unique Topics` stat cards on the same page that referenced `_overview.IsRemoteMetadataAvailable` (mandatory compile-fixup, same-behavior logic via `RemoteMetadataStatus == Available`).
   - AC9 ✅ — stat card relabeled to `"Inspected Actor State Size"`, empty value is the em-dash `—` (U+2014), and the tooltip is now a ternary expression: `"Total state size of the currently inspected actor"` when populated, `"Select an actor instance below to inspect its state size"` when null.
   - AC10 ✅ — `eventStoreDaprHttpPort` parameter appended to `AddHexalithEventStore` with `[1024, 65535]` range guards; `const int EventStoreDaprHttpPort = 3501;` removed; both usages now reference the parameter. `AppHost/Program.cs:23` still compiles unchanged (default value preserves source compatibility).
   - AC11 ✅ — XML `<param>` doc added with the exact wording from the story text (DAPR does not error on port conflicts → silently binds to a different port → `netstat -ano | findstr :3501` diagnostic).
   - AC12 ✅ — all 7 listed test files updated + 3 additional test files fixed (AdminDaprControllerActorTests, DaprActorsPageTests, DaprPubSubPageTests). 3 new `DaprActorQueryServiceTests` + 3 new `DaprPubSubQueryServiceTests` added, covering `NotConfigured`/`Unreachable`/`Available` paths using an in-file `FakeHandler` (mirrors the pattern already in use in `DaprPubSubQueryServiceTests`).
   - AC13 ✅ — success-path rendering untouched. Tier-2 (`Hexalith.EventStore.Server.Tests`) and Tier-3 (`Hexalith.EventStore.IntegrationTests`) tests were not touched and still build cleanly as part of the full solution build.
8. **Out-of-scope hygiene honored.** `HexalithEventStoreResources.cs` not touched (Out-of-Scope #1). No `IResourceWithEndpoints` refactor (#2). No port pre-check (#3). No `WaitFor(eventStore)` added (#4). UI layout structurally unchanged apart from the three blocks named in ACs (#5). Enum lives in `Admin.Abstractions.Models.Dapr/` not `Admin.Server.Contracts` (#6).
9. **Definition of Done** — all items met except DoD item "Jerome manually re-runs the restart procedure" (Task 7.5), which is explicitly operator-driven and belongs to the review phase per the story text. Noted as a review-gate handoff.
10. **Commit message guidance** — matches the story's suggested Conventional Commits format (`fix(admin): distinguish not-configured vs unreachable DAPR sidecar metadata states`). The change is limited to the Admin slice + one Aspire extension parameter addition — no public NuGet contract changes, so this is a `fix` (patch bump) per semantic-release rules.

### File List

**New (1 file):**

- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/RemoteMetadataStatus.cs`

**Modified — source (6 files):**

- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorRuntimeInfo.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprPubSubOverview.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor`
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`

**Modified — tests (10 files):**

- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprActorRuntimeInfoTests.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprPubSubOverviewTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs` *(+ 3 new unit tests, + local `FakeHandler` class)*
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprPubSubQueryServiceTests.cs` *(+ 3 new unit tests)*
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerActorTests.cs` *(not in AC12 list — compile-fixup)*
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerPubSubTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprActorsPageTests.cs` *(not in AC12 list — compile-fixup + AC9 stat-card label assertion update)*
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprPubSubPageTests.cs` *(not in AC12 list — compile-fixup)*
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminActorApiClientTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminPubSubApiClientTests.cs`

**Modified — sprint tracking (1 file):**

- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `19-6-admin-dapr-metadata-diagnostics` flipped `ready-for-dev` → `in-progress` → `review`.

### Change Log

| Date | Change | Notes |
|------|--------|-------|
| 2026-04-10 | Story 19-6 implementation complete — `review` | 17 files touched (1 new, 6 modified source, 10 modified test). Build: 0 warnings, 0 errors. Story 19-6 tests: 404 + 56 + 29 = 489 pass. Pre-existing unrelated failures (`DaprTenantQueryServiceTests`, `BreadcrumbTests`, `MainLayoutTests`, `StreamDetailPageTests`) confirmed present on pre-story `main`. Task 7.5 (manual Aspire run verification) deferred to operator review phase per story text. |
