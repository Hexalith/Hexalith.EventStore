# Post-Epic Deferred DW13: Tenant Lifecycle Actor Timeout Investigation

Status: backlog

Context created: 2026-05-20
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`

## Story

As an EventStore administrator managing tenants,
I want tenant lifecycle commands to complete or fail with structured operational status,
so that Enable and Disable workflows do not leave the UI waiting on opaque DAPR actor timeouts.

## Scope

This story covers CC-5 / Issue 18:

- `Enable` on `manual-test-tenant-a` times out from Admin UI.
- Admin Server invokes EventStore through `POST /api/v1/commands`.
- EventStore logs actor invocation failures for `EnableTenant`.
- Tenant list/query paths also show timeout behavior in the same run.

This story does not cover:

- The decision to physically delete tenants. The accepted Admin UI behavior remains Enable/Disable rather than Delete.
- Dead-letter binding or projection detail (DW11).
- Consistency checker false positives (DW12).

## Evidence

User symptom:

```text
Failed to enable tenant.: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
```

Runtime evidence:

- Admin UI times out calling `https://eventstore-admin/api/v1/admin/tenants/manual-test-tenant-a/enable`.
- Admin Server invokes EventStore at `api/v1/commands`.
- EventStore logs `ActorInvocationFailed`.
- Command context:
  - `TenantId=system`
  - `Domain=tenants`
  - `AggregateId=manual-test-tenant-a`
  - `CommandType=EnableTenant`
  - `ActorId=system:tenants:manual-test-tenant-a`
- EventStore exception:

  ```text
  TaskCanceledException: The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.
  ```

- Query pipeline also shows tenant read timeouts around `GET /api/v1/admin/tenants`.

## Acceptance Criteria

1. Reproduce `EnableTenant` timeout under Aspire with:
   - `EnableKeycloak=false`
   - DAPR placement and scheduler running
   - tenant `manual-test-tenant-a`

2. Determine whether the timeout is caused by actor placement, actor registration, access control, projection actor query path, command routing, or a blocked TenantsProjectionActor.

3. Add the narrowest automated regression test possible for the identified failure class.

4. Admin.Server maps long-running or failed tenant lifecycle operations to a structured `AdminOperationResult` with correlation/operation ID and no stack trace leakage.

5. Admin UI recovers after timeout, clears loading state, and tells the operator whether the operation is still pending, failed, unsupported, or retryable.

6. If tenant lifecycle commands are intentionally asynchronous, define the polling/status contract before changing the UI to claim success.

7. Manual retest records:

   ```text
   Issue 18: OK / KO
   Notes / messages exacts:
   -
   ```

## Expected File Touches

Potential implementation files:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
- EventStore command routing and tenant/projection actor code reached by `POST /api/v1/commands`

Likely tests:

- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTenantCommandServiceTests.cs`
- Admin UI tenant page tests if a UI loading/error-state fix is needed.
- EventStore server tests for the identified actor/projection failure class.

## Validation

Run targeted unit tests based on the discovered root cause. For runtime proof, use the repository Aspire instructions:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
$env:EnableKeycloak = 'false'
aspire run --project .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Then rerun the Issue 18 manual lifecycle steps from the source evidence.

## Tasks

- [ ] Reproduce the tenant Enable timeout under Aspire.
- [ ] Inspect EventStore, Admin.Server, actor, and projection traces for the root cause.
- [ ] Classify the failure path.
- [ ] Add the narrowest regression test.
- [ ] Implement backend and/or UI recovery behavior.
- [ ] Run targeted tests and record results.
- [ ] Rerun manual Issue 18 validation.

