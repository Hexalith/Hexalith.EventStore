# WS-1 Command Flow Evidence - 2026-05-20

## Scope

This artifact records the clone-to-command walking skeleton proof for Story WS-1. It includes the AppHost/resource observations, one live `IncrementCounter` command, status completion, correlation telemetry, automated coverage, and local environment caveats.

No JWTs, connection strings, or secrets are recorded here.

## Environment

- Working directory: `D:\Hexalith.EventStore`
- .NET SDK: `10.0.300`
- Docker CLI: `29.4.3`
- Aspire CLI: `13.3.2`
- DAPR CLI/runtime: `1.17.1` / `1.17.7`
- Dev auth mode: `EnableKeycloak=false`
- Development JWT configuration used by tests and manual command: issuer `hexalith-dev`, audience `hexalith-eventstore`, tenant `tenant-a`, command permissions.

## AppHost Attempts

### Documented command - initial review finding

Command:

```powershell
EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

Result on final retry:

- AppHost build succeeded with 0 warnings and 0 errors.
- AppHost exited before registration at the prerequisite gate because the local Docker Desktop engine returned HTTP 500 for the Docker server-version probe.
- Aspire CLI log: `C:\Users\JeromePiquot\.aspire\logs\cli_20260520T101445_5a5ce1f5.log`
- Wrapper stdout log: `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/aspire-run-documented-enablekeycloak-false-2026-05-20.out.log`
- Wrapper stderr log: `_bmad-output/test-artifacts/ws-1-clone-to-command-flow-walking-skeleton/aspire-run-documented-enablekeycloak-false-2026-05-20.err.log`

The failing Docker engine probe was reproduced directly:

```powershell
docker version --format '{{.Server.Version}}'
```

Result:

```text
request returned 500 Internal Server Error for API route and version http://%2F%2F.%2Fpipe%2FdockerDesktopLinuxEngine/v1.54/version, check if the server supports the requested API version
```

### Documented command - P6 follow-up rerun

Command:

```powershell
$env:EnableKeycloak='false'; aspire run --detach --non-interactive --project 'src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj' --format Json
```

Result on 2026-05-20 after the review follow-up:

- AppHost detached successfully without `SKIP_PREREQUISITE_CHECK=true`.
- AppHost path: `D:\Hexalith.EventStore\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj`
- AppHost PID: `132088`
- Aspire CLI log: `C:\Users\JeromePiquot\.aspire\logs\cli_20260520T110928354_detach-child_5822811aa5864e63a14ccef2f69aa7a4.log`
- `eventstore-scerrkak` (`eventstore`): Running, Healthy, HTTP endpoint `http://localhost:8080`
- `sample-xgbfgxjd` (`sample`): Running, Healthy, HTTP endpoint `http://localhost:5189`
- `pubsub`: Running, Healthy
- `statestore`: Running, Healthy

The AppHost was stopped cleanly after the resource-health capture:

```powershell
aspire stop --apphost src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --non-interactive
```

### Diagnostic runtime proof

After the documented run was blocked by local Docker Desktop health, the topology was run diagnostically with only the local prerequisite check bypassed:

```powershell
SKIP_PREREQUISITE_CHECK=true EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

Observed through Aspire MCP:

- AppHost path: `D:\Hexalith.EventStore\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj`
- `eventstore-gbvwbeks` (`eventstore`): Running, Healthy
- `sample-gkqfgvey` (`sample`): Running, Healthy
- `pubsub`: Running, Healthy
- `statestore`: Running, Healthy
- EventStore HTTP endpoint: `http://localhost:8080`
- EventStore HTTPS endpoint: `https://localhost:7141`
- Sample HTTP endpoint: `http://localhost:5189`
- Sample HTTPS endpoint: `https://localhost:7157`

The diagnostic AppHost was stopped cleanly with:

```powershell
aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
```

## Live Command Submission

Submitted to:

```http
POST http://localhost:8080/api/v1/commands
```

Command summary:

- `messageId`: `dd19ef53-2cb1-4187-b17e-f0d49b8a54ba`
- `correlationId`: `dd19ef53-2cb1-4187-b17e-f0d49b8a54ba`
- `tenant`: `tenant-a`
- `domain`: `counter`
- `aggregateId`: `counter-ws1-eccad14b6dc5423888026707f95cbf5d`
- `commandType`: `IncrementCounter`
- `payload`: `{}`

Accepted response summary:

- HTTP status: `202 Accepted`
- `Location`: `http://localhost:8080/api/v1/commands/status/dd19ef53-2cb1-4187-b17e-f0d49b8a54ba`
- `Retry-After`: `1`
- Body contained the non-empty correlation id.

Note: the manual response was captured before the DTO null-omit fix and included `resultPayload: null`. The subsequent contract-unit and command-flow integration test pass verifies the current accepted-command response shape preserves the original correlation-only JSON when no result payload exists.

## Final Command Status

Polled:

```http
GET http://localhost:8080/api/v1/commands/status/dd19ef53-2cb1-4187-b17e-f0d49b8a54ba
```

Final status summary:

- `status`: `Completed`
- `eventCount`: `1`
- `timestamp`: `2026-05-20T10:03:58.2012103+00:00`

## Correlation Telemetry

Aspire distributed trace evidence:

- Status-query trace: `c0c24dff863d4ddca09633b7061e7229`
- Trace attributes included `eventstore.correlation_id=dd19ef53-2cb1-4187-b17e-f0d49b8a54ba`.

Structured log evidence from `eventstore-gbvwbeks`:

- Trace id: `efdfff7ddfecb4794267625ee1d0c449`
- Log id `228`: invoked domain service `AppId=sample` with the same correlation id.
- Log id `229`: domain service completed with `EventCount=1` and the same correlation id.
- Log id `252`: events persisted with `EventCount=1` and the same correlation id.
- Log id `253`: events published with `EventCount=1`, topic `tenant-a.counter.events`, and the same correlation id.
- Log id `265`: command status found with `Status=Completed` and the same correlation id.

## Automated Coverage

Focused WS-1 AppHost prerequisite coverage:

```powershell
dotnet test tests\Hexalith.EventStore.AppHost.Tests\Hexalith.EventStore.AppHost.Tests.csproj
```

Result: Passed, 4 tests, 0 failed.

Focused WS-1 integration coverage:

```powershell
dotnet test tests\Hexalith.EventStore.IntegrationTests\Hexalith.EventStore.IntegrationTests.csproj --filter "FullyQualifiedName~LiveCommandSurfaceSmokeTests|FullyQualifiedName~CommandLifecycleTests"
```

Result: Passed, 10 tests, 0 failed.

Unit test projects run individually:

```powershell
dotnet test tests/Hexalith.EventStore.Client.Tests
dotnet test tests/Hexalith.EventStore.Contracts.Tests
dotnet test tests/Hexalith.EventStore.Sample.Tests
dotnet test tests/Hexalith.EventStore.Testing.Tests
```

Results:

- `Hexalith.EventStore.Client.Tests`: Passed, 398 tests.
- `Hexalith.EventStore.Contracts.Tests`: Passed, 511 tests.
- `Hexalith.EventStore.Sample.Tests`: Passed, 74 tests.
- `Hexalith.EventStore.Testing.Tests`: Passed, 144 tests.

## Product Changes Made During WS-1

- `PrerequisiteValidator` now uses a lightweight Docker server-version probe instead of `docker info`, and the command timeout is 120 seconds to tolerate slow Docker Desktop startup.
- The prerequisite validator has an injectable runner and regression coverage for the slow Docker probe case.
- `SubmitCommandResponse.ResultPayload` is omitted from JSON when it is null, preserving the accepted-command correlation-only response contract while still allowing enriched payloads when present.
- `PrerequisiteValidatorTests` were moved from the Tier 3 integration test project into the new Tier 1 `Hexalith.EventStore.AppHost.Tests` project.

## Caveats

- The initial exact documented CLI run could not register an AppHost because local Docker Desktop returned HTTP 500 for `/version`. A later review-follow-up rerun succeeded without bypassing prerequisite checks and confirmed healthy `eventstore`, `sample`, `pubsub`, and `statestore` resources.
- Local DAPR placement/scheduler binaries were not available under `%USERPROFILE%\.dapr\bin`; only `daprd.exe` was present. No manual placement/scheduler startup evidence was possible in this Windows environment.
- `Hexalith.EventStore.Server.Tests` was not run; project context documents a pre-existing CA2007 warning-as-error build failure in that test project.
