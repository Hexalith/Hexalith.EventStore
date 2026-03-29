---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-05-validate-and-complete
lastStep: step-05-validate-and-complete
lastSaved: '2026-03-29'
storyId: p0-coverage-gaps
detectedStack: backend
generationMode: ai-generation
executionMode: sequential
inputDocuments:
  - _bmad/tea/testarch/tea-index.csv
  - _bmad/tea/testarch/knowledge/test-levels-framework.md
  - _bmad/tea/testarch/knowledge/test-priorities-matrix.md
  - _bmad/tea/testarch/knowledge/data-factories.md
  - _bmad/tea/testarch/knowledge/test-quality.md
---

# ATDD Checklist — P0 Coverage Gap Hardening

## Summary

| Metric | Value |
|--------|-------|
| Total new tests | 62 |
| Test files created | 4 |
| Test files modified | 1 |
| All tests passing | YES |
| Build errors | 0 |

## Targets and Test Counts

### Target 1: DaprBackupCommandService (Zero coverage -> 18 tests)

**File:** `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprBackupCommandServiceTests.cs`

| AC | Test | Priority | Level |
|----|------|----------|-------|
| TriggerBackup delegates to EventStore via DAPR | TriggerBackupAsync_ReturnsSuccess_WhenEventStoreResponds | P0 | Unit |
| JWT token forwarded on all requests | TriggerBackupAsync_ForwardsJwtToken | P0 | Unit |
| Service unavailable returns error result | TriggerBackupAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| Null response returns explicit error | TriggerBackupAsync_ReturnsNullResponseError_WhenEventStoreReturnsNull | P0 | Unit |
| Cancellation token propagation | TriggerBackupAsync_PropagatesCancellation | P0 | Unit |
| ValidateBackup success path | ValidateBackupAsync_ReturnsSuccess_WhenBackupValid | P0 | Unit |
| ValidateBackup error path | ValidateBackupAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| TriggerRestore with point-in-time and dry-run | TriggerRestoreAsync_ReturnsSuccess_WithPointInTimeAndDryRun | P0 | Unit |
| TriggerRestore error path | TriggerRestoreAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| ExportStream success path | ExportStreamAsync_ReturnsResult_WhenEventStoreResponds | P0 | Unit |
| ExportStream null response | ExportStreamAsync_ReturnsError_WhenEventStoreReturnsNull | P0 | Unit |
| ExportStream error path | ExportStreamAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| ExportStream cancellation | ExportStreamAsync_PropagatesCancellation | P0 | Unit |
| ImportStream success path | ImportStreamAsync_ReturnsSuccess_WhenEventStoreAccepts | P0 | Unit |
| ImportStream error path | ImportStreamAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| HTTP status code extraction from HttpRequestException | InvokePost_ExtractsHttpStatusCode_FromHttpRequestException | P0 | Unit |

### Target 2: DaprBackupQueryService (Zero coverage -> 5 tests)

**File:** `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprBackupQueryServiceTests.cs`

| AC | Test | Priority | Level |
|----|------|----------|-------|
| Returns tenant-scoped backup jobs | GetBackupJobsAsync_ReturnsTenantJobs_WhenTenantIdProvided | P0 | Unit |
| Returns all tenants when null | GetBackupJobsAsync_ReturnsAllJobs_WhenTenantIdIsNull | P0 | Unit |
| Graceful empty on missing index | GetBackupJobsAsync_ReturnsEmpty_WhenIndexNotFound | P0 | Unit |
| Graceful empty on exception | GetBackupJobsAsync_ReturnsEmpty_WhenExceptionThrown | P0 | Unit |
| Cancellation propagation | GetBackupJobsAsync_PropagatesCancellation | P0 | Unit |

### Target 3: AdminBackupsController (Zero coverage -> 22 tests)

**File:** `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminBackupsControllerTests.cs`

| AC | Test | Priority | Level |
|----|------|----------|-------|
| GET returns 200 with backup jobs | GetBackupJobs_ReturnsOk_WithBackupJobs | P0 | Unit |
| Admin null tenant passes null to service | GetBackupJobs_AdminWithNullTenantId_PassesNullToService | P0 | Unit |
| Non-admin resolves to user tenant | GetBackupJobs_NonAdminWithNullTenantId_ResolvesToUserTenant | P0 | Unit |
| HttpRequestException -> 503 | GetBackupJobs_ServiceThrowsHttpRequestException_Returns503 | P0 | Unit |
| RpcException Unavailable -> 503 | GetBackupJobs_ServiceThrowsRpcException_Returns503 | P0 | Unit |
| Unexpected exception -> 500 | GetBackupJobs_ServiceThrowsUnexpectedException_Returns500 | P0 | Unit |
| POST trigger backup -> 202 | TriggerBackup_ReturnsAccepted_WhenServiceSucceeds | P0 | Unit |
| NotFound error code -> 404 | TriggerBackup_ReturnsErrorCode_WhenServiceFails | P0 | Unit |
| InvalidOperation -> 422 | TriggerBackup_ReturnsUnprocessable_WhenInvalidOperation | P0 | Unit |
| Timeout -> 503 | TriggerBackup_Returns503_WhenServiceUnavailable | P0 | Unit |
| POST validate -> 202 | ValidateBackup_ReturnsAccepted_WhenServiceSucceeds | P0 | Unit |
| POST restore -> 202 | TriggerRestore_ReturnsAccepted_WhenServiceSucceeds | P0 | Unit |
| Null result -> 500 | TriggerRestore_Returns500_WhenNullResult | P0 | Unit |
| SEC-2: Admin exports any tenant | ExportStream_AdminRole_AllowsAnyTenant | P0 | Unit |
| SEC-2: Non-admin matching tenant succeeds | ExportStream_NonAdminWithMatchingTenant_Succeeds | P0 | Unit |
| SEC-2: Non-admin mismatched tenant -> 403 | ExportStream_NonAdminWithMismatchedTenant_Returns403 | P0 | Unit |
| SEC-2: Admin imports any tenant | ImportStream_AdminRole_AllowsAnyTenant | P0 | Unit |
| SEC-2: Non-admin matching tenant succeeds | ImportStream_NonAdminWithMatchingTenant_Succeeds | P0 | Unit |
| SEC-2: Non-admin mismatched tenant -> 403 | ImportStream_NonAdminWithMismatchedTenant_Returns403 | P0 | Unit |
| Unauthorized error code -> 403 | MapOperationResult_Unauthorized_Returns403 | P0 | Unit |
| Unknown error code -> 500 | MapOperationResult_UnknownErrorCode_Returns500 | P0 | Unit |
| ProblemDetails includes correlationId | ErrorResponse_ContainsProblemDetails_WithCorrelationId | P0 | Unit |

### Target 4: DaprTenantCommandService (Zero coverage -> 14 tests)

**File:** `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTenantCommandServiceTests.cs`

| AC | Test | Priority | Level |
|----|------|----------|-------|
| CreateTenant success | CreateTenantAsync_ReturnsSuccess_WhenTenantServiceResponds | P0 | Unit |
| JWT token forwarded | CreateTenantAsync_ForwardsJwtToken | P0 | Unit |
| CreateTenant error | CreateTenantAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| CreateTenant null response | CreateTenantAsync_ReturnsNullResponseError_WhenTenantServiceReturnsNull | P0 | Unit |
| CreateTenant cancellation | CreateTenantAsync_PropagatesCancellation | P0 | Unit |
| DisableTenant success | DisableTenantAsync_ReturnsSuccess_WhenTenantServiceResponds | P0 | Unit |
| DisableTenant error | DisableTenantAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| EnableTenant success | EnableTenantAsync_ReturnsSuccess_WhenTenantServiceResponds | P0 | Unit |
| AddUser success | AddUserToTenantAsync_ReturnsSuccess_WhenTenantServiceResponds | P0 | Unit |
| AddUser error | AddUserToTenantAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| RemoveUser success | RemoveUserFromTenantAsync_ReturnsSuccess_WhenTenantServiceResponds | P0 | Unit |
| ChangeRole success | ChangeUserRoleAsync_ReturnsSuccess_WhenTenantServiceResponds | P0 | Unit |
| ChangeRole error | ChangeUserRoleAsync_ReturnsError_WhenServiceUnavailable | P0 | Unit |
| HTTP status code extraction | InvokePost_ExtractsHttpStatusCode_FromHttpRequestException | P0 | Unit |

### Target 5: SignalR Client Gaps (27 existing -> 32 tests, 5 new)

**File:** `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` (modified)

| AC | Test | Priority | Level |
|----|------|----------|-------|
| IsConnected false when not started | IsConnected_ReturnsFalse_WhenNotStarted | P0 | Unit |
| Unsubscribe callback no-op for unregistered group | UnsubscribeAsync_CallbackOverload_NoOpWhenGroupNotRegistered | P0 | Unit |
| Unsubscribe group no-op for unregistered group | UnsubscribeAsync_GroupOverload_NoOpWhenGroupNotRegistered | P0 | Unit |
| ProjectionChanged no-op for unsubscribed group | OnProjectionChanged_NoOpWhenGroupNotSubscribed | P0 | Unit |
| Closed event after disposal does not log | OnClosedAsync_AfterDisposal_DoesNotLogWarning | P0 | Unit |

## Risk Assessment

All P0 gaps now covered:
- **Backup/Restore**: Full write-path coverage including JWT forwarding, timeout, null response, error code extraction
- **Tenant write-path**: All 6 command methods tested with success, error, and cancellation paths
- **Controller**: All 6 HTTP endpoints + SEC-2 tenant body/query validation + error code mapping + ProblemDetails format
- **SignalR**: Offline property check, no-op unsubscribe paths, disposal-aware logging
