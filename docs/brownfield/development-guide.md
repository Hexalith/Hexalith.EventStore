# Development Guide — Hexalith.EventStore

> Build, test, and run locally. Mirrors and expands `CLAUDE.md`. See the hand-authored
> `docs/getting-started/` for quickstart and prerequisites.

## Prerequisites

- **.NET SDK 10.0.300** (pinned in `global.json`, roll-forward `latestPatch`).
- **Docker Desktop** (for Redis, integration tests, container builds).
- **DAPR CLI** + runtime (`daprd`, `placement`, `scheduler`).
- **Aspire CLI** (`aspire`).
- For E2E: Playwright browsers.

## Build

```bash
# Restore + build (use the .slnx — NEVER a .sln)
dotnet restore Hexalith.EventStore.slnx
dotnet build Hexalith.EventStore.slnx --configuration Release
```

Build settings (enforced repo-wide via `Directory.Build.props`): `net10.0`, `Nullable=enable`,
`ImplicitUsings=enable`, **`TreatWarningsAsErrors=true`**. Code style from `.editorconfig`:
file-scoped namespaces, Allman braces, `_camelCase` private fields, `Async` suffix, `I` interface
prefix, 4-space indent, CRLF, UTF-8.

## Test (run projects individually — not solution-level `dotnet test`)

### Tiered test matrix

| Tier | Project | Frameworks | Notes |
|------|---------|-----------|-------|
| 1 (unit) | `Contracts.Tests` | xUnit v3, Shouldly | |
| 1 | `Client.Tests` | + NSubstitute | |
| 1 | `Sample.Tests` | | Counter domain |
| 1 | `Testing.Tests` | | test-framework internals |
| 1 | `SignalR.Tests` | + NSubstitute | |
| 1 | `AppHost.Tests` | | app-model config |
| 1 | `Admin.Abstractions.Tests`, `Admin.Cli.Tests`, `Admin.Mcp.Tests`, `Admin.Server.Tests`, `Admin.Server.Host.Tests` | + NSubstitute | |
| 1 | `DeferredWorkGovernance.Tests`, `OperationalEvidence.Validator.Tests` | | |
| 2 (server) | `Server.Tests` | + AspNetCore.Mvc.Testing | **⚠ pre-existing build failure** (CA2007 as error) — excluded from baseline |
| 2 (component) | `Admin.UI.Tests` | bunit | Blazor components |
| 3 (integration) | `IntegrationTests` | Aspire.Hosting.Testing, Mvc.Testing, StackExchange.Redis, Testcontainers | needs Docker + Aspire |
| 3 (E2E) | `Admin.UI.E2E` | Playwright | browser automation |
| 4 (perf) | `perf/Hexalith.EventStore.LoadTests` | NBomber, NBomber.Http | throughput/latency |

```bash
# Tier 1 examples
dotnet test tests/Hexalith.EventStore.Contracts.Tests/
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.SignalR.Tests/

# Tier 3 (requires Docker + running Aspire env)
dotnet test tests/Hexalith.EventStore.IntegrationTests/
```

**Test conventions:** Shouldly fluent assertions only (never raw `Assert.*`), NSubstitute for mocks,
coverlet for coverage. Tier 2/3 tests MUST inspect **state-store end-state** (Redis key contents,
persisted CloudEvent body), not just API status codes or mock counts (R2-A6).

> **Known issue:** `tests/Hexalith.EventStore.Server.Tests` does not build (CA2007 warnings treated as
> errors). It is excluded from the CI baseline. Fix the CA2007 occurrences (add `.ConfigureAwait(false)`)
> before relying on it.

## Run the local topology (Aspire)

```bash
aspire run                       # default: from AppHost
# or explicitly
aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

- CommandApi: `http://localhost:8080` (use HTTP in VMs — dev HTTPS cert isn't fully trustable there).
- Aspire dashboard: `https://localhost:17017`.
- DAPR HTTP port fixed at **3501** (so Admin.Server can do cross-sidecar metadata queries).

**`EnableKeycloak=false`** falls back to symmetric-key JWT (no Keycloak container). AppHost changes
require restarting the Aspire app.

### Cursor Cloud / VM bootstrap (from CLAUDE.md)

```bash
sudo dockerd &>/tmp/dockerd.log &
sudo chmod 666 /var/run/docker.sock
$HOME/.dapr/bin/placement --port 50005 &
$HOME/.dapr/bin/scheduler --port 50006 --etcd-data-dir /tmp/dapr-scheduler-data &
EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

**VM gotchas:** DAPR slim mode does not auto-start placement/scheduler (start them first or actors
fail with "did not find address for actor"); access-control policies are deny-by-default and in slim
mode without mTLS service-to-service calls may be rejected with 403.

### Aspire diagnostics

Use Aspire MCP tools: `list resources`, `list structured logs`, `list console logs`, `list traces`,
`list trace structured logs`, `select apphost`/`list apphosts`. Inspect runtime state **before**
changing code. Use Playwright MCP for functional investigation (get endpoints from `list resources`).

## Building your own domain (the programming model)

```csharp
public sealed class CounterAggregate : EventStoreAggregate<CounterState>
{
    public static DomainResult Handle(IncrementCounter c, CounterState? s)
        => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });
}
public sealed class CounterState
{
    public int Count { get; private set; }
    public void Apply(CounterIncremented e) => Count++;
}
```

Register with two lines — `AddEventStore()` scans the assembly (reflection discovery of `Handle`/`Apply`),
`UseEventStore()` activates domains with convention-derived kebab-case names. Override the name with
`[EventStoreDomain("...")]`. See `samples/Hexalith.EventStore.Sample/Counter/`.

## Commits & branches

- **Conventional Commits** required (semantic-release): `feat:` (minor), `fix:` (patch),
  `docs:`/`refactor:`/`test:`/`chore:`/`perf:` (no bump), `feat!:`/`BREAKING CHANGE:` (major).
  Enforced by `commitlint`.
- Branches: `feat/…`, `fix/…`, `docs/…`. No direct commits to `main`; PRs only.
- Always `dotnet build && dotnet test` (relevant tiers) before committing.

## CI/CD note (discrepancy to be aware of)

`CLAUDE.md` and README badges reference **GitHub Actions** workflows (`ci.yml`, `docs-validation.yml`)
and a semantic-release pipeline, but **no `.github/workflows/` directory exists in this repository
checkout**. Likewise there is **no root `CHANGELOG.md`** despite README linking to it. The
semantic-release config (`.releaserc.json`, `package.json` devDeps, `commitlint.config.mjs`) is
present. Confirm whether CI lives in a parent/automation repo before assuming pushes trigger builds.
