# Post-Epic Deferred DW11: Admin Action Binding and Projection Detail Contracts

Status: backlog

Context created: 2026-05-20
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`
Trace evidence:

- Retry: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/fa3fc5fd10b041f1162f2944d18953d9-*`
- Skip: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/b0a9110c494774fec42be2369b39da9f-*`
- Archive: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/54257a9188c24dd9a4ca6791cdf02b82-*`
- Projection detail: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/7ac735d7c7c4ef15fb7ba5791eb5fc46-*`

## Story

As an EventStore operator using Admin UI action and projection pages,
I want dead-letter actions and projection details to use valid backend contracts,
so that operator workflows fail recoverably instead of producing model-binding 500s or unsupported detail navigation.

## Scope

This story covers:

- CC-1 / Issue 9: Dead-letter Retry, Skip, and Archive fail with HTTP 500 before business logic.
- CC-2 / Issue 11: Projection list shows the fixture but projection detail returns "Projection not found".

This story does not cover:

- Consistency-check false positives (DW12).
- Tenant lifecycle actor timeouts (DW13).
- Snapshot, compaction, backup, validation, or export deferred UX (DW14).
- TypeCatalog disconnect noise (DW15).

## Evidence

Dead-letter actions reach `eventstore-admin` endpoints:

- `POST /api/v1/admin/dead-letters/tenant-a/retry`
- `POST /api/v1/admin/dead-letters/tenant-a/skip`
- `POST /api/v1/admin/dead-letters/tenant-a/archive`

The exception is raised before business logic:

```text
InvalidOperationException: Record type 'Hexalith.EventStore.Admin.Server.Models.DeadLetterActionRequest' has validation metadata defined on property 'MessageIds' that will be ignored. 'MessageIds' is a parameter in the record primary constructor and validation metadata must be associated with the constructor parameter.
```

Projection detail evidence:

- Admin UI calls `GET https://localhost:8091/api/v1/admin/projections/tenant-a/counter`.
- Admin Server invokes EventStore at `api/v1/admin/projections/tenant-a/counter`.
- EventStore responds `404 Not Found`.
- `DaprProjectionQueryService` uses `EnsureSuccessStatusCode()`, so fallback detail is not reached.
- Existing EventStore controller evidence shows `GET /api/v1/admin/projections/{tenantId}/{projectionName}/rebuild-status`, but not the requested detail endpoint.

## Acceptance Criteria

1. Dead-letter Retry, Skip, and Archive no longer fail model binding before business logic when the body is:

   ```json
   { "messageIds": ["manual-dlq-tenant-a-001"] }
   ```

2. `DeadLetterActionRequest` validation metadata is attached to the record constructor parameter, or the DTO is converted to an explicit class model compatible with ASP.NET Core validation.

3. Controller/API tests cover Retry, Skip, and Archive with a valid body and verify the request reaches the service layer instead of producing HTTP 500.

4. If a fixture message is visual-only and the backend cannot find it, the response is a recoverable business failure such as 404 or 422, not a model-binding 500.

5. Projection detail has one defined source of truth:
   - EventStore exposes `GET /api/v1/admin/projections/{tenantId}/{projectionName}`; or
   - Admin.Server builds detail from the admin projection index plus rebuild/status endpoints; or
   - the UI hides/disables unsupported detail navigation with an explicit message.

6. `DaprProjectionQueryService.GetProjectionDetailAsync` does not use `EnsureSuccessStatusCode()` in a way that prevents known 404/unsupported responses from being mapped or falling back intentionally.

7. Tests prove `/projections` list and detail behavior for the Counter projection fixture.

8. Manual retest records:

   ```text
   Issue 9: OK / KO
   Issue 11: OK / KO
   Notes / messages exacts:
   -
   ```

## Expected File Touches

Likely implementation files:

- `src/Hexalith.EventStore.Admin.Server/Models/DeadLetterActionRequest.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs`
- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs`

Likely tests:

- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDeadLettersControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprProjectionQueryServiceTests.cs`

## Validation

Run targeted tests first:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~AdminDeadLettersControllerTests|FullyQualifiedName~DaprProjectionQueryServiceTests" -m:1
```

After implementation, restart Aspire and rerun the manual Issue 9 and Issue 11 checks from the source evidence.

## Tasks

- [ ] Write failing tests for valid `DeadLetterActionRequest` bodies on Retry, Skip, and Archive.
- [ ] Fix dead-letter action request validation metadata.
- [ ] Decide projection detail source of truth.
- [ ] Add or adjust projection detail tests for 404/unsupported/detail-supported paths.
- [ ] Implement projection detail behavior.
- [ ] Run targeted tests and record results.
- [ ] Rerun manual Issue 9 and Issue 11 validation.

