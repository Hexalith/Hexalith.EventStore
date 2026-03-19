# Story 8.4: Domain Service Hot Reload

Status: done

## Story

As a daily developer,
I want to modify domain logic and restart only the domain service,
so that my inner development loop stays under 5 seconds.

## Acceptance Criteria

> **Note:** The epics AC (hot reload < 2s, topology continues running — UX-DR25) is **already satisfied** by Story 7.8's passing Tier 3 contract tests. No further validation of the hot reload mechanism is needed.

1. **Given** Story 8.2 added Greeting as a second domain alongside Counter,
   **When** the sample domain service restarts,
   **Then** both Counter and Greeting domains are discovered by `AssemblyScanner` and registered via `UseEventStore()`
   **And** commands for both domains route correctly to the restarted service via static registrations in `appsettings.Development.json`.

2. **Given** all Tier 1 test suites,
   **When** executed after this story's changes,
   **Then** all tests in Client.Tests, Contracts.Tests, Sample.Tests, and Testing.Tests pass with zero regressions.

### Stretch Goal (not required for story acceptance)

If time permits (1-hour timebox), investigate `dotnet watch` as an alternative to Aspire dashboard Stop/Start. Document the recommended inner-loop workflow with evidence. If `dotnet watch` is viable in Aspire, add an optional configuration without breaking the default launch.

## Context: What Already Exists

Story 7.8 (old epic structure, status: done) **already validated hot reload end-to-end** with Tier 3 contract tests. The architecture supports independent domain service restarts through four design pillars:

1. **DAPR Service Invocation (D7)**: `DaprDomainServiceInvoker` calls domain services via `DaprClient.InvokeMethodAsync` — runtime discovery, no compile-time coupling
2. **Stateless Domain Services**: Pure function contract `(CommandEnvelope, object?) -> DomainResult` — zero state between requests
3. **Actor-Based Processing**: Actors call domain services unidirectionally, no keepalive/session affinity
4. **DAPR Resiliency**: Automatic retry, circuit breaker, timeout during restart window

### Existing Hot Reload Tests (Story 7.8 — ALREADY PASSING)

File: `tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs`

- `ProcessCommand_AfterDomainServiceRestart_CompletesSuccessfully` — baseline → stop → start → post-restart command
- `ProcessCommand_DuringDomainServiceRestart_HandledByResiliency` — command during downtime retried by DAPR
- `CommandApi_DuringDomainServiceRestart_RemainsResponsive` — health + 202 acceptance while service is down

All 3 tests PASS. Total inner loop measured < 5 seconds. **These tests must NOT be recreated.**

### What Story 8.4 Must Complete

Since the contract test validation is done (Story 7.8), this story focuses on **multi-domain completeness** and **developer workflow documentation**:

1. **Fix missing Greeting domain service registration** — `appsettings.Development.json` lacks a static registration for the Greeting domain added by Story 8.2. Without this, Greeting commands fail with `DomainServiceNotFoundException`. This is the highest-value deliverable.
2. **Multi-domain hot reload validation** — verify both Counter and Greeting domains are discovered and routable after restart
3. **All Tier 1 tests pass** — zero regressions
4. _(Optional stretch)_ **Investigate `dotnet watch`** — determine if it adds value over the Aspire dashboard Stop/Start workflow already proven < 5s. Timebox: 1 hour max.

## Tasks / Subtasks

- [x] Task 1: Add Greeting domain service registration and validate multi-domain (AC: #1)
    - [x] 1.1 **Verify naming convention**: Confirmed `NamingConventionEngine` derives `"greeting"` from `GreetingAggregate` — verified in `tests/Hexalith.EventStore.Sample.Tests/Registration/MultiDomainRegistrationTests.cs:40` and `GreetingAggregateTests.cs:25`.
    - [x] 1.2 **Verify request routing**: `DomainServiceRequestRouter.ProcessAsync` uses `GetRequiredKeyedService<IDomainProcessor>(request.Command.Domain)` — generic keyed service resolution handles both Counter and Greeting dynamically. No changes needed.
    - [x] 1.3 Added the missing Greeting registration to `src/Hexalith.EventStore.CommandApi/appsettings.Development.json`:
        ```json
        "tenant-a|greeting|v1": {
          "AppId": "sample",
          "MethodName": "process",
          "TenantId": "tenant-a",
          "Domain": "greeting",
          "Version": "v1"
        }
        ```
    - [x] 1.4 Grepped `tests/Hexalith.EventStore.IntegrationTests/` — no registration-count assertions or Registrations-object checks that would break. All Tier 3 tests use `"counter"` domain and load `appsettings.json` (not Development).
    - [x] 1.5 Sample.Tests: 43/43 passed. Both domains discovered: `EventStore activated: 2 domains (greeting [Aggregate: GreetingAggregate], counter [Aggregate: CounterAggregate])`.
    - [x] 1.6 No multi-domain restart issues encountered.

- [x] Task 2: Validate existing tests — zero regressions (AC: #2)
    - [x] 2.1 Client.Tests: 293/293 passed
    - [x] 2.2 Contracts.Tests: 267/267 passed
    - [x] 2.3 Sample.Tests: 43/43 passed
    - [x] 2.4 Testing.Tests: 67/67 passed
    - [x] 2.5 No pre-existing failures encountered in Tier 1 suites.

- [x] Task 3: _(Optional — 1 hour timebox)_ Investigate `dotnet watch` for the sample domain service (Stretch Goal)
    - [x] 3.1 `dotnet watch run --project samples/Hexalith.EventStore.Sample/` works standalone: app starts on port 5189, watch process waits for file changes to trigger rebuild+restart. Confirmed working with `Microsoft.NET.Sdk.Web`.
    - [x] 3.2 Documented in Completion Notes. `dotnet watch` works standalone → proceeding to Task 4.

- [x] Task 4: _(Optional — only if Task 3 passes)_ Investigate Aspire watch mode integration (Stretch Goal)
    - [x] 4.1 Aspire 13.1.2 and `CommunityToolkit.Aspire.Hosting.Dapr` 9.7.0 do NOT support launching project resources with `dotnet watch`. `AddProject<T>()` uses `dotnet run` internally. No `.WithWatchMode()`, `.WithCommand("watch")`, or equivalent API exists.
    - [x] 4.2 N/A — not supported.
    - [x] 4.3 Documented: Aspire does not support watch mode for project resources. The canonical workflow is Aspire dashboard Stop/Start (proven < 5s in Story 7.8). The alternative `dapr run --app-id sample --app-port 5189 -- dotnet watch run` alongside Aspire was NOT implemented per story instructions.
    - [x] 4.4 Inner-loop workflow recommendation documented in Completion Notes.

## Dev Notes

### THIS IS A MULTI-DOMAIN COMPLETENESS + VALIDATION STORY

The hot reload **mechanism** is already validated by Story 7.8's contract tests. This story:

1. **Fixes a real gap**: Greeting domain registration missing from `appsettings.Development.json` (Story 8.2 omission)
2. **Validates multi-domain restart**: Counter + Greeting both discoverable and routable after service restart
3. _(Optional stretch)_ Investigates `dotnet watch` as alternative to Aspire dashboard Stop/Start

### Architecture: Why Hot Reload Works

The EventStore architecture decouples domain services from the server via DAPR service invocation:

```
Developer edits CounterProcessor.cs
  → Aspire dashboard: Stop → Start "sample" resource (~1-2s rebuild + restart)
  → DAPR sidecar detects new process, re-establishes connection (~1s)
  → Next command from CommandApi actor calls DaprClient.InvokeMethodAsync("sample", "process", ...)
  → Routed to new process with updated logic
  → EventStore, Redis, pub/sub, actors — all untouched
```

Key files in this flow:

- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` — calls `DaprClient.InvokeMethodAsync` (line 54-56)
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` — resolves `sample` app-id from static registrations in `appsettings.Development.json`
- `samples/Hexalith.EventStore.Sample/Program.cs` — registers domains and maps `/process` endpoint
- `src/Hexalith.EventStore.AppHost/Program.cs` — Aspire topology, sample has DAPR sidecar with app-id `sample`

### Domain Service Registration (How CommandApi Finds the Sample)

Static registration in `src/Hexalith.EventStore.CommandApi/appsettings.Development.json`:

```json
"EventStore": {
  "DomainServices": {
    "Registrations": {
      "tenant-a|counter|v1": {
        "AppId": "sample",
        "MethodName": "process",
        "TenantId": "tenant-a",
        "Domain": "counter",
        "Version": "v1"
      }
    }
  }
}
```

**CONFIRMED GAP**: Story 8.2 added `GreetingAggregate` but did NOT add a static registration for the Greeting domain in either `appsettings.Development.json` or `appsettings.json`. Task 3.1 must add `tenant-a|greeting|v1` registration pointing to `sample` app-id. Without this, Greeting commands will fail with `DomainServiceNotFoundException`.

### DAPR Sidecar Configuration for Sample

```yaml
# AppHost/Program.cs line 69-73
.WithDaprSidecar(sidecar => sidecar
.WithOptions(new DaprSidecarOptions {
AppId = "sample",
Config = accessControlConfigPath,
}))
```

The sample service has:

- Zero infrastructure access (no state store, no pub/sub references — D4, AC #13)
- Access control: `defaultAction: deny`, no outbound operations
- Only responds to incoming POST invocations from `commandapi`

### Canonical Developer Workflow (Proven in Story 7.8)

1. Edit `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs`
2. In Aspire dashboard: Stop → Start `sample` resource
3. Send test command via curl or Swagger UI
4. **Total: < 5 seconds**

---

### Context for Optional Tasks 3-4 Only (skip if not pursuing stretch goal)

**`dotnet watch` considerations:**

- Sample project uses `Microsoft.NET.Sdk.Web` — `dotnet watch` natively supported
- DAPR sidecar may temporarily disconnect when `dotnet watch` kills/restarts the process — sidecars reconnect via health probes
- Aspire 13.1 launches projects via `dotnet run` — `dotnet watch` may not be supported natively

**DAPR resiliency during restart** (validated by Story 7.8 contract test):

- Retry: constant 1s, max 3 retries; Timeout: 5s; Circuit breaker: trips after 3 consecutive failures

### WARNING: Pre-Existing Test Failures

There are pre-existing Tier 3 test failures and 1 pre-existing Tier 2 failure (`ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel`). These are NOT regressions. Do NOT attempt to fix them.

### Coding Conventions (from .editorconfig)

- File-scoped namespaces: `namespace X.Y.Z;`
- Allman braces (new line before `{`)
- Private fields: `_camelCase`
- 4-space indentation, CRLF, UTF-8
- Nullable enabled, implicit usings enabled
- Warnings as errors (`TreatWarningsAsErrors = true`)

Do NOT:

- Recreate hot reload contract tests (already exist in Story 7.8)
- Modify the DaprDomainServiceInvoker, DomainServiceResolver, or resiliency configuration
- Change the sample domain service's `/process` endpoint
- Modify DAPR component files (statestore.yaml, pubsub.yaml, accesscontrol.yaml)
- Change the Aspire topology in ways that break the default (non-watch) launch

### Key Package Versions (from Directory.Packages.props)

| Package                              | Version  |
| ------------------------------------ | -------- |
| xUnit                                | 2.9.3    |
| Shouldly                             | 4.3.0    |
| .NET SDK                             | 10.0.103 |
| Aspire.AppHost.Sdk                   | 13.1.2   |
| CommunityToolkit.Aspire.Hosting.Dapr | 9.7.0    |
| Dapr.Client                          | 1.16.1   |

### Project Structure Notes

```
src/Hexalith.EventStore.CommandApi/
  appsettings.Development.json        <- MODIFY: add Greeting registration (Task 1.1)
src/Hexalith.EventStore.AppHost/
  Program.cs                          <- MAY MODIFY: conditional watch mode (optional Task 4)
  DaprComponents/                     <- DO NOT MODIFY
samples/Hexalith.EventStore.Sample/
  Program.cs                          <- DO NOT MODIFY (logic)
  Properties/launchSettings.json      <- MAY MODIFY: add watch profile (optional Task 4)
tests/
  Hexalith.EventStore.IntegrationTests/
    ContractTests/HotReloadTests.cs   <- ALREADY EXISTS: DO NOT RECREATE
```

### Previous Story Intelligence (Story 8.3)

- Story 8.3 is a validation/audit story for NuGet client packages — no domain logic changes
- Pattern: this epic is about validation and completion, NOT rewriting existing code
- Key learning: check what already exists before creating new code

### Previous Story Intelligence (Story 7.8 — Old Epic)

- Hot reload contract tests: 3/3 PASS
- Aspire resource lifecycle API: `ResourceCommands.ExecuteCommandAsync("sample", "resource-stop/start", ct)`
- Inner loop timing: < 5 seconds validated
- `dotnet watch` was explicitly marked OUT OF SCOPE in Story 7.8 ("the UX validation of `dotnet watch` is a documentation/walkthrough concern, not a test concern")
- This is what Story 8.4 picks up

### Git Intelligence

Recent commits (2026-03-18/19):

- `0f9b28f` feat: Implement multi-domain support with Greeting aggregate and update routing logic
- `c0c611f` refactor: Improve code readability by adjusting method formatting and adding ClearFailure method
- `f22e5ee` feat: Update sprint status and add Story 8.2 for Counter Sample Domain Service
- `96e725f` feat: Complete Story 8.1 Aspire AppHost & DAPR topology with prerequisite validation

All Epic 8 work has been validation/completion pattern — minimal changes to working code. Story 8.2 added GreetingAggregate as second domain.

### Existing Files to Potentially MODIFY

| File                                                                | Change                                                             |
| ------------------------------------------------------------------- | ------------------------------------------------------------------ |
| `src/Hexalith.EventStore.CommandApi/appsettings.Development.json`   | Add Greeting domain service registration (Task 1.1)                |
| `src/Hexalith.EventStore.AppHost/Program.cs`                        | Conditional watch mode for sample (optional Task 4, additive only) |
| `samples/Hexalith.EventStore.Sample/Properties/launchSettings.json` | Add watch-friendly profile if needed (optional Task 4)             |

### Existing Files — DO NOT MODIFY

| File                                                                         | Reason                                    |
| ---------------------------------------------------------------------------- | ----------------------------------------- |
| `src/Hexalith.EventStore.Server/DomainServices/*`                            | Domain service invocation works correctly |
| `src/Hexalith.EventStore.AppHost/DaprComponents/*`                           | DAPR config is correct                    |
| `samples/Hexalith.EventStore.Sample/Program.cs`                              | Sample service works correctly            |
| `tests/Hexalith.EventStore.IntegrationTests/ContractTests/HotReloadTests.cs` | Already done in Story 7.8                 |

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 8, Story 8.4]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR25, Jerome persona, "inner loop" moments]
- [Source: _bmad-output/planning-artifacts/architecture.md — D7 DAPR service invocation, resiliency policies]
- [Source: _bmad-output/implementation-artifacts/7-8-domain-service-hot-reload-validation.md — Previous hot reload validation (DONE)]
- [Source: _bmad-output/implementation-artifacts/8-3-nuget-client-package-and-zero-config-registration.md — Previous story in Epic 8]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs — Current Aspire topology]
- [Source: src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs — DAPR service invocation]
- [Source: samples/Hexalith.EventStore.Sample/Program.cs — Sample service endpoint]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

No issues encountered.

### Completion Notes List

- **Core deliverable**: Added missing Greeting domain service registration to `src/Hexalith.EventStore.CommandApi/appsettings.Development.json` — `tenant-a|greeting|v1` pointing to `sample` app-id with `/process` method. This was the Story 8.2 omission that would have caused `DomainServiceNotFoundException` for Greeting commands in Development environment.
- **Multi-domain validation**: Both Counter and Greeting domains discovered and registered by `UseEventStore()` assembly scanner. Confirmed via test output: `EventStore activated: 2 domains (greeting [Aggregate: GreetingAggregate], counter [Aggregate: CounterAggregate])`.
- **Request routing**: `DomainServiceRequestRouter.ProcessAsync` uses generic keyed service resolution `GetRequiredKeyedService<IDomainProcessor>(request.Command.Domain)` — both domains route correctly without explicit per-domain routing code.
- **All Tier 1 tests pass**: 670 total (Client: 293, Contracts: 267, Sample: 43, Testing: 67) — zero regressions.
- **Stretch Goal — `dotnet watch` investigation**:
    - `dotnet watch run --project samples/Hexalith.EventStore.Sample/` works standalone: app starts on port 5189, watch detects file changes and triggers rebuild+restart.
    - Aspire 13.1.2 does NOT support launching project resources with `dotnet watch` — no `WithWatchMode()` or equivalent API.
    - **Canonical inner-loop workflow recommendation**: Use Aspire dashboard Stop/Start for the sample resource. Proven < 5s total (Story 7.8 contract tests). Steps: (1) Edit domain logic, (2) Aspire dashboard → Stop → Start "sample", (3) ~1-2s rebuild + restart, (4) ~1s DAPR sidecar reconnect, (5) Next command routes to updated logic. All other infrastructure (EventStore, Redis, pub/sub, actors) remains untouched.
    - Alternative (not implemented): `dapr run --app-id sample --app-port 5189 -- dotnet watch run` alongside Aspire for fully automatic hot reload. This bypasses Aspire lifecycle management and is not recommended for general use.

### Change Log

- 2026-03-19: Story 8-4 implemented — added Greeting domain service registration to appsettings.Development.json, validated multi-domain discovery and all Tier 1 tests (670/670 pass), investigated dotnet watch (works standalone, not supported by Aspire)

### File List

- `src/Hexalith.EventStore.CommandApi/appsettings.Development.json` — modified (added `tenant-a|greeting|v1` domain service registration)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — modified (story status: ready-for-dev → in-progress → review)
- `_bmad-output/implementation-artifacts/8-4-domain-service-hot-reload.md` — modified (task checkboxes, dev agent record, completion notes)
