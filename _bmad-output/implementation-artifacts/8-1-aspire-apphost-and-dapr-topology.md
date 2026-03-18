# Story 8.1: Aspire AppHost & DAPR Topology

Status: review

## Story

As a developer,
I want to start the complete EventStore system with a single Aspire command,
so that I have a working local development environment in minutes.

## Acceptance Criteria

1. **Given** the AppHost project is configured,
   **When** `dotnet aspire run` is executed,
   **Then** EventStore server, sample domain service, DAPR sidecars, state store, and message broker all start (FR40, UX-DR21)
   **And** all services are visible in the Aspire dashboard.

2. **Given** prerequisites are missing (Docker, .NET SDK, DAPR),
   **When** startup fails,
   **Then** clear, actionable error messages identify exactly what's needed with installation links (UX-DR22).

## Context: What Already Exists

The Aspire AppHost and DAPR topology are **already substantially implemented** from previous epic work. This story validates, hardens, and completes the existing implementation.

### Existing AppHost Topology (`src/Hexalith.EventStore.AppHost/Program.cs`)

- **DAPR access control** configuration resolution with `FileNotFoundException` fallback (D4, FR34)
- **`AddHexalithEventStore()`** custom Aspire extension wiring: in-memory state store (actor-enabled) + pub/sub + CommandApi DAPR sidecar
- **Keycloak OIDC** (optional, `EnableKeycloak` flag) with realm-as-code import, JWT Bearer wiring (D11)
- **Sample domain service** with DAPR sidecar — zero infrastructure access (D4, AC #13)
- **Blazor UI sample** with service discovery and SignalR hub wiring
- **Publisher environments**: Docker Compose, Kubernetes, Azure Container Apps via `PUBLISH_TARGET` env var

### Existing Aspire Extensions (`src/Hexalith.EventStore.Aspire/`)

- `HexalithEventStoreExtensions.AddHexalithEventStore()` — provisions state store, pub/sub, wires DAPR sidecar
- `HexalithEventStoreResources` — record exposing `StateStore`, `PubSub`, `CommandApi` for further customization
- AppPort intentionally omitted for Aspire Testing compatibility (auto-detects from resource model)

### Existing ServiceDefaults (`src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`)

- OpenTelemetry: structured JSON console logging, metrics (AspNetCore, HttpClient, Runtime), tracing with health check filtering
- Health checks: `/health` (all), `/alive` (liveness), `/ready` (readiness) with dev/prod response modes
- Service discovery and standard resilience handler on all HttpClient instances

### Existing DAPR Components (`src/Hexalith.EventStore.AppHost/DaprComponents/`)

- `statestore.yaml` — Redis state store with actor support, commandapi-scoped
- `pubsub.yaml` — Redis pub/sub with three-layer scoping (component, publishing, subscription)
- `accesscontrol.yaml` — Per-app-id allow list (commandapi trusted, sample denied)
- `resiliency.yaml` — Retry, circuit breaker, timeout policies per component type
- `configstore.yaml` — Redis configuration store, commandapi-scoped
- `subscription-sample-counter.yaml` — Declarative subscription template (inactive for sample)

### What This Story Must Complete

The existing code was built incrementally across multiple stories. This story:
1. **Validates** the full topology starts and all services are healthy via `dotnet aspire run`
2. **Adds prerequisite validation** with actionable error messages and installation links (AC #2)
3. **Verifies** all services appear correctly in the Aspire dashboard
4. **Ensures** existing Tier 1 and Tier 2 tests pass with the current topology
5. **Documents** the startup experience and any fixes applied

## Tasks / Subtasks

- [x] Task 1: Add prerequisite validation with actionable error messages (AC: #2) — **core deliverable**
  - [x] 1.1 Add a `PrerequisiteValidator` static helper class in `src/Hexalith.EventStore.AppHost/` that checks for required tools before `DistributedApplication.CreateBuilder(args)`
  - [x] 1.2 Skip all prerequisite checks when any of these conditions are true: `CI=true`, `SKIP_PREREQUISITE_CHECK=true`, or `PUBLISH_TARGET` is set (publish generates manifests and does not need Docker/DAPR locally). CI environments manage their own prerequisites; this validator is a developer UX feature for `dotnet aspire run` only.
  - [x] 1.3 Check Docker availability: run `docker info` (5s timeout) and verify exit code 0. On failure, output: `"Docker is not running or not installed. Install Docker Desktop: https://docs.docker.com/get-docker/ — then start Docker Desktop and retry."`
  - [x] 1.4 Check DAPR CLI availability: run `dapr --version` (5s timeout) and verify exit code 0. On failure, output: `"DAPR CLI is not installed. Install DAPR: https://docs.dapr.io/getting-started/install-dapr-cli/ — then run 'dapr init' and retry."` **Note:** CommunityToolkit.Aspire.Hosting.Dapr may manage DAPR sidecar binaries without the CLI. If investigation shows the CLI is not strictly required, downgrade this check to a warning instead of a blocking error.
  - [x] 1.5 Check DAPR runtime initialized: parse `dapr --version` stdout for `Runtime version:` line. The runtime is NOT initialized if: (a) the line reads `Runtime version: n/a`, OR (b) the `Runtime version:` line is entirely absent from the output. On failure, output: `"DAPR runtime is not initialized. Run 'dapr init' (recommended for full local development) or 'dapr init --slim' (minimal, no Docker components) and retry."`
  - [x] 1.6 All prerequisite checks must produce a single, consolidated error message listing ALL missing prerequisites (not fail-fast on the first one)
  - [x] 1.7 Prerequisite checks must NOT throw exceptions — use `Console.Error.WriteLine` with clear formatting and `Environment.Exit(1)` for clean exit
  - [x] 1.8 Call `PrerequisiteValidator.Validate()` as the first line of `Program.cs`, before `DistributedApplication.CreateBuilder(args)`

- [ ] Task 2: Validate full topology startup (AC: #1) — **verification, not new code; manual steps**
  - [ ] 2.1 Run `dotnet aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` and verify all services start successfully
  - [ ] 2.2 *(Manual verification)* Verify in Aspire dashboard: `commandapi`, `sample`, `sample-blazor-ui`, `keycloak` (if enabled) all appear with "Running" status. This requires visual inspection of the dashboard UI — it cannot be automated by a CLI dev agent.
  - [ ] 2.3 *(Manual verification)* Verify health endpoints respond: `GET /health` (200), `GET /alive` (200), `GET /ready` (200) on commandapi. Use `curl` against the running topology.
  - [ ] 2.4 *(Manual verification)* Validate `EnableKeycloak=false` startup path — verify topology starts without Keycloak and commandapi falls back to symmetric key auth
  - [ ] 2.5 Fix any startup failures — document root cause and resolution in Dev Agent Record

- [x] Task 3: Verify existing tests pass — zero regressions (AC: #1)
  - [x] 3.1 Run Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Sample.Tests/ && dotnet test tests/Hexalith.EventStore.Testing.Tests/`
  - [x] 3.2 Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
  - [x] 3.3 If any test failures are caused by this story's changes, fix them. Pre-existing failures unrelated to this story should be documented but NOT fixed.

## Dev Notes

### WARNING: 75 Pre-Existing Tier 3 Test Failures

There are 75 pre-existing Tier 3 test failures on `main` (out of 192 total). These existed BEFORE this story and are NOT regressions. **Do NOT attempt to fix them in this story.** Only fix failures that are directly caused by changes made in this story. Document pre-existing failures in the Dev Agent Record if encountered.

### Critical: This Is a Validation & Hardening Story, Not a Greenfield Build

The AppHost topology is already implemented. Do NOT:
- Rewrite `Program.cs` — it is correct and battle-tested from Stories 5.1, 5.5
- Change DAPR component YAML files — they are production-ready
- Modify `HexalithEventStoreExtensions.cs` — the Aspire extension is correct
- Modify `ServiceDefaults/Extensions.cs` — observability and health checks are complete
- Change publisher environment wiring — Docker, K8s, ACA targets are correct
- Alter Keycloak realm configuration — it was validated in Story 5.5

### Architecture: AppHost Topology Structure

```
AppHost (Aspire orchestrator)
├── commandapi (port 8080)
│   ├── DAPR sidecar (app-id: commandapi)
│   │   ├── statestore (state.in-memory, actor-enabled)
│   │   ├── pubsub (pubsub.redis)
│   │   ├── configstore (configuration.redis)
│   │   └── accesscontrol.yaml (D4 policies)
│   └── .NET app (REST API + actor host + event publisher)
├── sample (port 8081)
│   ├── DAPR sidecar (app-id: sample)
│   │   └── accesscontrol.yaml (zero infrastructure access)
│   └── .NET app (domain service)
├── sample-blazor-ui
│   └── .NET Blazor app (HTTP/HTTPS endpoints, SignalR)
├── keycloak (port 8180, optional via EnableKeycloak)
│   └── hexalith-realm.json (realm-as-code)
└── Publisher environments (PUBLISH_TARGET env var)
    ├── docker → Docker Compose
    ├── k8s → Kubernetes
    └── aca → Azure Container Apps
```

### PrerequisiteValidator Design

Create a minimal static class that performs prerequisite checks before Aspire builder initialization. This is NOT middleware or a hosted service — it runs synchronously at the very start of `Program.cs`.

**Design boundary:** The validator checks tool *availability* (are Docker/DAPR installed and running?), NOT operational readiness (is there enough disk space? are ports free? are DAPR components healthy?). Operational failures are caught by Aspire's own startup diagnostics and DAPR sidecar health checks. Do not expand scope beyond availability checks.

**Linux Docker permissions:** On Linux, `docker info` may fail with "permission denied" even when Docker is installed, if the user is not in the `docker` group. The validator's error message says "not installed" which is misleading in this case. Optionally, parse stderr for "permission denied" and provide targeted guidance: `"Docker is installed but your user lacks permission. Run: sudo usermod -aG docker $USER — then log out and back in."` This is a nice-to-have enhancement, not a blocker for story completion.

```csharp
// src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs
namespace Hexalith.EventStore.AppHost;

internal static class PrerequisiteValidator
{
    public static void Validate()
    {
        // Skip in CI environments and publish scenarios — CI manages its own prerequisites,
        // and `aspire publish` generates manifests without needing Docker/DAPR locally.
        // GitHub Actions sets CI=true; other CI systems can set SKIP_PREREQUISITE_CHECK=true.
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("SKIP_PREREQUISITE_CHECK"), "true", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PUBLISH_TARGET")))
        {
            return;
        }

        List<string> errors = [];

        // Check Docker (docker info can be slow ~2-3s when Docker Desktop is starting)
        if (!IsCommandAvailable("docker", "info"))
            errors.Add("Docker is not running or not installed.\n  Install: https://docs.docker.com/get-docker/\n  Then start Docker Desktop and retry.");

        // Check DAPR CLI + runtime
        (bool cliAvailable, string? daprOutput) = RunCommand("dapr", "--version");
        if (!cliAvailable)
        {
            errors.Add("DAPR CLI is not installed.\n  Install: https://docs.dapr.io/getting-started/install-dapr-cli/\n  Then run 'dapr init' and retry.");
        }
        else if (daprOutput is null
            || daprOutput.Contains("Runtime version: n/a", StringComparison.OrdinalIgnoreCase)
            || !daprOutput.Contains("Runtime version:", StringComparison.OrdinalIgnoreCase))
        {
            // CLI is installed but runtime is not initialized (n/a or line absent entirely)
            errors.Add("DAPR runtime is not initialized.\n  Run 'dapr init' (recommended for full local development)\n  or 'dapr init --slim' (minimal, no Docker components) and retry.");
        }

        if (errors.Count > 0)
        {
            Console.Error.WriteLine("Prerequisites missing:");
            Console.Error.WriteLine();
            foreach (string error in errors)
            {
                Console.Error.WriteLine($"  - {error}");
                Console.Error.WriteLine();
            }
            Environment.Exit(1);
        }
    }

    private static bool IsCommandAvailable(string command, string args)
        => RunCommand(command, args).Success;

    private static (bool Success, string? Output) RunCommand(string command, string args)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            // WaitForExit returns false on timeout — must check before accessing ExitCode.
            // StandardOutput.ReadToEnd() is called after WaitForExit — safe here because
            // dapr/docker output is small (<1KB). For large-output commands, use async reads
            // to avoid pipe buffer deadlocks.
            bool exited = process?.WaitForExit(TimeSpan.FromSeconds(5)) ?? false;
            if (!exited) return (false, null);
            string output = process!.StandardOutput.ReadToEnd();
            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, null);
        }
    }
}
```

Usage in `Program.cs` (add as first line):
```csharp
PrerequisiteValidator.Validate();

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
// ... existing code unchanged ...
```

### Key Package Versions (from Directory.Packages.props)

| Package | Version |
|---|---|
| Aspire.AppHost.Sdk | 13.1.2 |
| CommunityToolkit.Aspire.Hosting.Dapr | 13.0.0 |
| Aspire.Hosting.Keycloak | 13.1.2-preview.1.26125.13 |
| Aspire.Hosting.Docker | 13.1.2-preview.1.26125.13 |
| Aspire.Hosting.Kubernetes | 13.1.2-preview.1.26125.13 |
| DAPR SDK | 1.16.1 |
| .NET SDK | 10.0.103 |

### Existing Files to Modify

| File | Change |
|---|---|
| `src/Hexalith.EventStore.AppHost/Program.cs` | Add `PrerequisiteValidator.Validate()` as first line |

### New Files to Create

| File | Purpose |
|---|---|
| `src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs` | Prerequisite validation with actionable error messages |

### Existing Files — DO NOT MODIFY (beyond the one-line Program.cs addition)

| File | Reason |
|---|---|
| `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` | Aspire extension is correct |
| `src/Hexalith.EventStore.Aspire/HexalithEventStoreResources.cs` | Record is correct |
| `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` | ServiceDefaults fully implemented |
| `src/Hexalith.EventStore.AppHost/DaprComponents/*.yaml` | DAPR components production-ready |
| `src/Hexalith.EventStore.AppHost/KeycloakRealms/hexalith-realm.json` | Realm validated in Story 5.5 |
| `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` | Project file is correct |
| `src/Hexalith.EventStore.AppHost/Properties/launchSettings.json` | Launch settings correct |

### Do NOT Unit Test the PrerequisiteValidator

The `PrerequisiteValidator` is infrastructure glue that calls external processes (`docker`, `dapr`). Unit testing it requires mocking `Process.Start`, which is brittle and low-value. The real validation is manual: run `dotnet aspire run` and observe the error output when prerequisites are missing. The existing Tier 3 test suite implicitly validates that the AppHost starts successfully.

### Coding Conventions (from .editorconfig)

- File-scoped namespaces: `namespace X.Y.Z;`
- Allman braces (new line before `{`)
- Private fields: `_camelCase`
- Async suffix on async methods
- 4-space indentation, CRLF, UTF-8
- Nullable enabled, implicit usings enabled
- Warnings as errors (`TreatWarningsAsErrors = true`)

### Project Structure Notes

- AppHost is the Aspire orchestrator — NOT a deployed service
- `Hexalith.EventStore.Aspire` is a published NuGet package for external AppHost consumers
- `Hexalith.EventStore.ServiceDefaults` provides shared config for all services in the topology
- DAPR components live in `DaprComponents/` and are copied to output via `<Content>` items

### Previous Story Intelligence (Story 7.3)

- Story 7.3 completed per-consumer rate limiting; all Tier 1 (659 pass), Tier 2 (1504/1505), Tier 3 (130 pass, 75 pre-existing failures)
- Pre-existing Tier 3 failures (75/192) are NOT regressions — they existed on main before any changes
- AppHost topology has been used for Tier 3 E2E testing since Story 5.5 (Keycloak security tests)
- Key learning: DAPR sidecar startup can be slow — `WaitFor(keycloak)` pattern prevents race conditions

### Git Intelligence

Recent commits (2026-03-18):
- `93d0230` Implement per-consumer rate limiting (Story 7.3)
- `3edd174` feat: Implement per-consumer rate limiting alongside existing per-tenant limits
- `e2eeec8` feat: Update sprint status and add Story 7.2 for Per-Tenant Rate Limiting
- `ff7a64c` Merge Story 7.1: Configurable Aggregate Snapshots
- `fecba85` feat: Complete Story 7.1 Configurable Aggregate Snapshots

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 8, Story 8.1]
- [Source: _bmad-output/planning-artifacts/architecture.md — D9 MinVer, D10 CI/CD, D11 Keycloak]
- [Source: _bmad-output/planning-artifacts/prd.md — FR40, FR42, FR43, FR44]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs]
- [Source: src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs]
- [Source: src/Hexalith.EventStore.ServiceDefaults/Extensions.cs]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/]
- [Source: _bmad-output/implementation-artifacts/7-3-per-consumer-rate-limiting.md — Previous story intelligence]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build: 0 errors, 0 warnings (Release configuration)
- Tier 1: 659/659 pass (Contracts: 267, Client: 293, Sample: 32, Testing: 67)
- Tier 2: 1504/1505 pass (1 pre-existing failure: `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` — same as Story 7.3, NOT a regression)
- Task 2 (topology startup) requires manual verification by Jerome — subtasks 2.1–2.5 are manual steps that cannot be automated by a CLI dev agent

### Completion Notes List

- Created `PrerequisiteValidator` static class with consolidated prerequisite checking for Docker and DAPR CLI/runtime
- Validator skips checks in CI (`CI=true`), when `SKIP_PREREQUISITE_CHECK=true`, or during publish (`PUBLISH_TARGET` set)
- Docker check: runs `docker info` with 5s timeout, provides installation link on failure
- DAPR check: runs `dapr --version` with 5s timeout, checks both CLI availability and runtime initialization (detects `Runtime version: n/a` or missing line)
- All errors collected into consolidated list before output — no fail-fast
- Uses `Console.Error.WriteLine` + `Environment.Exit(1)` — no exceptions thrown
- Added `PrerequisiteValidator.Validate()` as first line of `Program.cs` before `DistributedApplication.CreateBuilder(args)`
- No unit tests per Dev Notes: "Do NOT Unit Test the PrerequisiteValidator" — infrastructure glue calling external processes

### Change Log

- 2026-03-18: Implemented Story 8.1 — Created PrerequisiteValidator, wired into Program.cs, verified Tier 1+2 tests pass

### File List

- `src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs` (NEW)
- `src/Hexalith.EventStore.AppHost/Program.cs` (MODIFIED — added `using` and `PrerequisiteValidator.Validate()` call)
