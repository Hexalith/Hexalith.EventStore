# Sprint Change Proposal - 2026-05-20 Admin UI Manual Retest Residuals

**Project:** Hexalith.EventStore
**Trigger:** Manual Admin UI retest after DW10 corrections
**Source evidence:** `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`
**Trace evidence:** `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/`
**Workflow:** bmad-correct-course, Batch mode
**Status:** Approved 2026-05-20 by Jerome - routed to DW11-DW15 backlog story files

## 1. Issue Summary

The 2026-05-20 manual Admin UI retest confirmed that the immediate DW10 fixes for metric zero rendering, stale State Inspector reopening, and redundant development-role copy are implemented and ready for validation. The same run also produced six residual findings, tracked as CC-1 through CC-6 in the source artifact.

These findings are not one bug. They fall into four classes:

- backend validation/runtime bugs that turn operator actions into HTTP 500 or timeouts;
- incomplete Admin API contracts where the UI lists resources but cannot load details;
- consistency-check false positives caused by reading DAPR actor state through reconstructed raw keys;
- scope/UX decisions for operations currently implemented as explicit `deferred` responses.

The trigger story is `post-epic-deferred-dw10-admin-ui-manual-retest-follow-up-polish`, whose scope intentionally deferred Issues 9, 11, 12, 15, 16, 17, and 18 until fixtures and live evidence existed. That evidence now exists in the handoff artifact.

## 2. Impact Analysis

### Epic impact

The affected baseline is the completed Admin UI Manual-Test Follow-Up Cluster in `sprint-status.yaml`, especially:

- `admin-ui-operator-action-and-dev-role-testability-fix`
- `admin-operational-index-populators`
- `admin-storage-snapshot-compaction-backup-operations`
- `admin-ui-consistency-and-tenant-clarity-polish`
- `post-epic-deferred-dw10-admin-ui-manual-retest-follow-up-polish`

The original epics remain valid. No completed epic should be reopened. The correct routing is new post-epic deferred follow-up rows that cite this proposal and the manual retest evidence.

### PRD impact

No PRD scope reduction is required. The findings conflict with existing Administration Tooling requirements that are already part of the v2 completed baseline:

- FR73: projection dashboard status, lag, throughput, error count, controls.
- FR76: storage, compaction, snapshot, and backup management.
- FR77: tenant management lifecycle clarity.
- FR78: dead-letter browse, search, retry, skip, archive.
- FR79: shared Admin API behind Web UI, CLI, and MCP.
- NFR40: Admin API response behavior.
- NFR44: DAPR-only admin data access.
- NFR46: role-based admin access.

### Architecture impact

Three architecture clarifications are needed:

1. Admin consistency checks must not bypass actor isolation by reconstructing DAPR actor-state keys with `DaprClient.GetStateAsync` unless that key format is an explicitly supported admin read contract. Project context already says actor state must go through `IActorStateManager`; operational tools need a public read/index contract instead of Redis/DAPR key guessing.
2. Projection detail needs a defined source of truth. The current list path can read `admin:projections:*`, but detail is forwarded to an EventStore endpoint that does not appear to exist for `GET /api/v1/admin/projections/{tenantId}/{projectionName}`.
3. Tenant lifecycle writes through EventStore command routing need a live Aspire/DAPR actor-path investigation. The observed timeout is not only a UI timeout; EventStore actor invocation waits until its configured timeout expires.

### UX impact

The UX spec principle "minimum friction to next step" applies directly. Operator actions must end in one of these states: success, explicit deferred/unsupported, or recoverable failure with enough context. Infinite spinners, unhandled 500s, and false anomalies break operator trust.

CC-3 is different from the others: Snapshot, compaction, backup, validation, and stream export currently return explicit deferred messages. That can be acceptable for this iteration if the UI presents the actions honestly before or during the click path. It should not be mixed with backend crash fixes.

## 3. Checklist Results

| Checklist item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | `post-epic-deferred-dw10-admin-ui-manual-retest-follow-up-polish` plus manual residual tests from 2026-05-20. |
| 1.2 Core problem | [x] Done | Residual Admin UI runtime failures after fixture-backed manual validation. Category: technical limitations discovered during validation plus scope clarification. |
| 1.3 Evidence | [x] Done | Source artifact includes trace IDs, Aspire logs, endpoints, symptoms, and suspect code. |
| 2.1 Current epic impact | [x] Done | Completed Admin follow-up cluster remains valid but needs new post-epic rows. |
| 2.2 Epic-level changes | [!] Action-needed | Add follow-up rows after approval; do not reopen completed epics. |
| 2.3 Remaining epics | [x] Done | Epic 22 tenant/query contracts are relevant to CC-5 but do not require PRD rewrite. |
| 2.4 New/obsolete epics | [x] Done | No new epic needed. |
| 2.5 Priority changes | [x] Done | Prioritize CC-1, CC-2, CC-4, CC-5 ahead of CC-3/CC-6. |
| 3.1 PRD conflicts | [x] Done | No scope change; current behavior violates existing admin requirements. |
| 3.2 Architecture conflicts | [!] Action-needed | Need architecture notes for projection detail source, consistency read contract, and tenant actor timeout path. |
| 3.3 UX conflicts | [!] Action-needed | Need deferred-operation UX policy and recoverable operator-action errors. |
| 3.4 Other artifacts | [!] Action-needed | Add sprint-status rows and implementation story files only after approval. |
| 4.1 Direct adjustment | [x] Viable | Best path for CC-1, CC-2, CC-4 UI rendering, and CC-6. |
| 4.2 Rollback | [x] Not viable | Completed admin work should not be rolled back; fixes are localized. |
| 4.3 MVP review | [x] Not viable | MVP/PRD remains valid; implementation and UX need follow-up. |
| 4.4 Recommended path | [x] Done | Hybrid: direct fixes plus one explicit product decision for deferred operations. |
| 5.1-5.5 Proposal components | [x] Done | Captured below. |
| 6.3 User approval | [x] Done | Approved by Jerome on 2026-05-20. |
| 6.4 sprint-status update | [x] Done | DW11-DW15 backlog rows added to sprint-status and story files created. |

## 4. Recommended Approach

Use a hybrid path:

- Direct Adjustment for CC-1, CC-2, CC-4, CC-5, and CC-6.
- Product/UX decision for CC-3 before implementing real backend storage operations.

Priority order:

1. CC-1 Dead-letter action request binding - high, narrow backend bug.
2. CC-4 Consistency false positives and Blazor dispatcher safety - high, operator-trust bug with clear evidence.
3. CC-2 Projection detail contract - high, incomplete Admin API contract.
4. CC-5 Tenant enable/list timeout - high, requires runtime actor investigation before product fix.
5. CC-3 Deferred storage operations UX policy - medium, because behavior is explicit and non-crashing.
6. CC-6 TypeCatalog navigation during disconnect - low/medium hygiene unless reproduced as visible user failure.

## 5. Detailed Change Proposals

### 5.1 Sprint-status routing

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**Section:** Admin UI Manual-Test Follow-Up Cluster.

**OLD:**

```yaml
  admin-ui-consistency-and-tenant-clarity-polish: done
```

**NEW:**

```yaml
  admin-ui-consistency-and-tenant-clarity-polish: done

  # Post-DW10 Admin UI manual retest residuals
  # Added by sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md.
  post-epic-deferred-dw11-admin-action-binding-and-projection-detail-contracts: backlog
  post-epic-deferred-dw12-consistency-actor-state-contract-and-dispatcher-fix: backlog
  post-epic-deferred-dw13-tenant-lifecycle-actor-timeout-investigation: backlog
  post-epic-deferred-dw14-admin-deferred-operations-ux-policy: backlog
  post-epic-deferred-dw15-admin-ui-blazor-navigation-hygiene: backlog
```

**Rationale:** Keep completed rows done. Add focused follow-up workstreams with traceable ownership instead of reopening broad Admin epics.

### 5.2 Story: DW11 Admin action binding and projection detail contracts

**Proposed story ID:** `post-epic-deferred-dw11-admin-action-binding-and-projection-detail-contracts`

**Scope:** CC-1 and CC-2.

**Acceptance criteria:**

1. Dead-letter Retry, Skip, and Archive no longer fail model binding before business logic when the body is `{ "messageIds": ["manual-dlq-tenant-a-001"] }`.
2. `DeadLetterActionRequest` validation metadata is attached to the record constructor parameter or the DTO is converted to an explicit class model compatible with ASP.NET Core validation.
3. Controller/API tests cover Retry, Skip, and Archive with valid body and verify the request reaches the service layer instead of producing HTTP 500.
4. If a fixture message is visual-only and the backend cannot find it, the response is a recoverable 404/422 style business failure, not a model-binding 500.
5. Projection detail has one defined source of truth:
   - either EventStore exposes `GET /api/v1/admin/projections/{tenantId}/{projectionName}`;
   - or Admin.Server builds detail from the admin projection index plus rebuild/status endpoints;
   - or the UI hides/disables unsupported detail navigation with an explicit message.
6. `DaprProjectionQueryService.GetProjectionDetailAsync` does not use `EnsureSuccessStatusCode()` in a way that prevents the existing fallback/error mapping from running on known 404/unsupported responses.
7. Tests prove `/projections` list and detail behavior for the Counter projection fixture.

**Code references:**

- `src/Hexalith.EventStore.Admin.Server/Models/DeadLetterActionRequest.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs`
- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDeadLettersControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprProjectionQueryServiceTests.cs`

### 5.3 Story: DW12 Consistency actor-state contract and dispatcher fix

**Proposed story ID:** `post-epic-deferred-dw12-consistency-actor-state-contract-and-dispatcher-fix`

**Scope:** CC-4 and UI trace note from CC-4.

**Acceptance criteria:**

1. Consistency checks no longer report missing events 1..N for streams whose events exist under DAPR actor-state keys.
2. `DaprConsistencyCommandService` stops reconstructing raw keys such as `{tenant}:{domain}:{aggregateId}:events:{sequence}` and `{tenant}:{domain}:{aggregateId}:metadata` unless that format is proven to match the supported storage contract for the current actor implementation.
3. Preferred fix: read through an EventStore/admin stream-read contract or an admin-maintained consistency index, not by bypassing actor isolation.
4. Sequence continuity and metadata consistency tests cover the `tenant-a/counter/counter-1` shape with 18 events and no false positives.
5. Projection-position warnings are either made granular or explicitly labeled as coarse-grained warnings, not unexplained anomalies.
6. `Consistency.razor` uses `await InvokeAsync(StateHasChanged)` in async completion/finally paths that can run outside the Blazor renderer dispatcher.
7. Manual retest for Issues 16 and 17 returns no unexplained anomaly cluster for the seeded Counter stream.

**Code references:**

- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs` if present, otherwise add targeted bUnit coverage in the existing UI test project.

### 5.4 Story: DW13 Tenant lifecycle actor timeout investigation

**Proposed story ID:** `post-epic-deferred-dw13-tenant-lifecycle-actor-timeout-investigation`

**Scope:** CC-5.

**Acceptance criteria:**

1. Reproduce `EnableTenant` timeout under Aspire with `EnableKeycloak=false`, DAPR placement/scheduler running, and the manual tenant `manual-test-tenant-a`.
2. Determine whether the timeout is caused by actor placement, actor registration, access control, projection actor query path, command routing, or a blocked TenantsProjectionActor.
3. Add the narrowest automated regression test possible for the identified failure class.
4. Admin.Server maps long-running or failed tenant lifecycle operations to a structured `AdminOperationResult` with correlation/operation ID and no stack trace leakage.
5. Admin UI recovers after timeout, clears loading state, and tells the operator whether the operation is still pending, failed, unsupported, or retryable.
6. If tenant lifecycle commands are intentionally asynchronous, define the polling/status contract before changing the UI to claim success.

**Code references:**

- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
- EventStore command routing and tenant/projection actor implementations reached by `POST /api/v1/commands`.

### 5.5 Story: DW14 Admin deferred operations UX policy

**Proposed story ID:** `post-epic-deferred-dw14-admin-deferred-operations-ux-policy`

**Scope:** CC-3.

**Decision needed:** Are explicit deferred operation responses acceptable for this iteration?

**Recommended decision:** Accept current backend deferred responses for this iteration, but make the UI pre-communicate deferred/unsupported state before an operator clicks destructive or heavy operations.

**Acceptance criteria if accepted as deferred:**

1. Snapshot creation, compaction, backup creation, backup validation, and stream export display an explicit deferred/unsupported state in the UI.
2. Buttons either render disabled with a reason, or the confirmation dialog clearly states the operation is deferred before submission.
3. Manual tests mark Issue 15 as `OK - deferred explicit` when the UI and response are truthful.
4. No operation displays a fake success for unsupported backend work.
5. Restore and import remain gated by explicit confirmation and risk copy if visible.

**Acceptance criteria if real backend work is approved instead:**

1. Split into separate backend stories for snapshot job model, non-destructive compaction model, backup engine/manifest, validation, bounded export, restore/import safety.
2. Update PRD/architecture before implementation because backup/restore and compaction affect event immutability, portability, protected-data behavior, and disaster recovery semantics.

**Code references:**

- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor`
- Admin.Server storage/backup command services and controllers.

### 5.6 Story: DW15 Admin UI Blazor navigation hygiene

**Proposed story ID:** `post-epic-deferred-dw15-admin-ui-blazor-navigation-hygiene`

**Scope:** CC-6.

**Acceptance criteria:**

1. TypeCatalog tab/filter URL updates do not call JS interop or navigation after circuit disposal/disconnect.
2. Existing TypeCatalog deep links remain stable:
   - `/types?tab=events`
   - `/types?tab=commands`
   - `/types?tab=aggregates`
   - `/types?type=CounterAggregate`
3. Tests cover idempotent URL updates and user-left-page/disconnect-safe guards.
4. If the current tests already cover this behavior, classify CC-6 as trace noise and document why no code change is needed.

**Code references:**

- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogUrlIdempotencyAtddTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs`

## 6. Implementation Handoff

**Scope classification:** Moderate.

The change does not require PRD redefinition, but it does require backlog reorganization into several follow-up stories because the residuals cross Admin.Server, EventStore, Admin.UI, runtime DAPR actor behavior, and product UX policy.

**Handoff recipients:**

- Developer agent: DW11, DW12, DW15.
- Developer agent plus runtime investigation: DW13.
- Product owner/architect decision: DW14, especially if real snapshot/backup/compaction backend work is approved.

**Suggested implementation sequence:**

1. DW11 first: removes hard 500s and unblocks projection detail behavior.
2. DW12 second: fixes false-positive consistency checks before more manual validation.
3. DW13 third: isolates tenant lifecycle timeout; may reveal infrastructure setup or actor-routing regression.
4. DW14 in parallel as a decision item; implement UI policy if deferred behavior remains accepted.
5. DW15 last unless TypeCatalog produces visible user failure.

**Validation expectations:**

- Run targeted Admin.Server tests first for the touched services/controllers.
- Run targeted Admin.UI bUnit tests for the touched pages.
- Restart Aspire after Admin.Server/Admin.UI changes and rerun the manual evidence block from the source artifact.
- Preserve the known guidance to run test projects individually.

## 7. Approval Gate

Jerome approved the proposal as written on 2026-05-20.

Applied routing:

- Added DW11-DW15 backlog rows to `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- Created story handoff files for DW11-DW15 under `_bmad-output/implementation-artifacts/`.
- Kept implementation work separate from this Correct Course approval step.
