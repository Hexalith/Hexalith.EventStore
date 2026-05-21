# Post-Epic Deferred DW13: Tenant Lifecycle Actor Timeout Investigation

Status: done

Context created: 2026-05-20
Context refreshed: 2026-05-20
Party-mode review hardening: 2026-05-20
Advanced elicitation hardening: 2026-05-20
Second elicitation hardening: 2026-05-20
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`
Manual runbook: `_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-deferred-entry-runbook.md`

## Story

As an EventStore administrator managing tenants,
I want tenant lifecycle commands to complete or fail with structured operational status,
so that Enable and Disable workflows do not leave the UI waiting on opaque DAPR actor timeouts.

## Scope

This story covers CC-5 / Issue 18 from the 2026-05-20 manual Admin UI retest:

- `Enable` on `manual-test-tenant-a` times out from Admin UI.
- Admin Server invokes EventStore through `POST /api/v1/commands`.
- EventStore logs actor invocation failures for `EnableTenant`.
- Tenant list/query paths also show timeout behavior in the same run.
- Trace evidence shows `TenantsProjectionActor/tenants:system:index/method/QueryAsync` ending in `TaskCanceledException`.

This story does not cover:

- Adding physical tenant deletion. Accepted behavior remains Enable/Disable because tenants are audit-retained.
- Dead-letter action binding or projection detail fallback. That is DW11.
- Consistency checker false positives or Blazor dispatcher fixes. That is DW12.
- Broad resiliency policy redesign. Change timeout/retry policy only if the reproduced root cause requires it and the change is proven by tests.
- Timeout-value-only changes. Increasing `HttpClient.Timeout`, DAPR timeout policy, or Polly timeout without a reproduced root cause, a regression test, and operator-visible failure semantics does not satisfy this story.

## Evidence To Preserve

User symptom:

```text
Failed to enable tenant.: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
```

Runtime evidence:

- Admin UI times out calling `https://eventstore-admin/api/v1/admin/tenants/manual-test-tenant-a/enable`.
- Admin Server invokes EventStore through `http://localhost:43206/v1.0/invoke/eventstore/method/api/v1/commands`.
- EventStore logs `ActorInvocationFailed`.
- Command context:
  - `TenantId=system`
  - `Domain=tenants`
  - `AggregateId=manual-test-tenant-a`
  - `CommandType=EnableTenant`
  - `ActorId=system:tenants:manual-test-tenant-a`
- EventStore exception observed:

  ```text
  TaskCanceledException: The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.
  ```

- Tenant read path also timed out around `GET /api/v1/admin/tenants` with:

  ```text
  TimeoutRejectedException: The operation didn't complete within the allowed timeout of '00:00:10'.
  ```

## Acceptance Criteria

1. Reproduce `EnableTenant` timeout under Aspire with `EnableKeycloak=false`, DAPR placement and scheduler running, and tenant `manual-test-tenant-a` created or clearly documented as fixture-unavailable. The evidence must include the command path `Admin.UI -> Admin.Server -> DAPR invoke eventstore -> EventStore command endpoint -> aggregate actor/projection/domain processor`.
2. Determine and document one concrete root cause with a falsifiable diagnostic decision record: failing hop, failure-class name, trace/log proof, fix chosen, and suspected causes ruled out. Valid failure classes include `actor-not-placed`, `actor-registered-but-blocked`, `dapr-acl-rejected`, `wrong-app-id-or-method-route`, `projection-actor-timeout`, `domain-processor-timeout`, `domain-processor-missing`, `envelope-id-mismatch`, `read-model-contention`, or a similarly precise observed class.
3. Add at least one red automated regression test before the fix and keep the final test at the narrowest seam that proves the identified failure class. The regression must exercise the relevant tenant/domain/aggregate/actor identity instead of stubbing above the failing boundary.
4. Define the tenant lifecycle completion contract before changing UI success behavior: either Enable/Disable completes synchronously within a bounded service-level timeout, or the API returns an accepted/pending result with operation ID and pollable status. Command submission success is not tenant-enabled success until the tenant state is confirmed.
5. Admin.Server maps long-running or failed tenant lifecycle operations to structured `AdminOperationResult` or `ProblemDetails` responses with stable classifications, preserved generated operation/correlation ID, and no stack trace or raw upstream body leakage.
6. Admin.Server status mapping is explicit: malformed client input maps to `400`, domain/business rejection maps to `422`, timeout or upstream unavailability maps to `503` or `504`, unexpected server defects map to `500`, and no timeout/unavailable/retryable state is returned as a generic `422`.
7. Admin UI recovers after timeout or failure, clears loading state, keeps the dialog/page usable, and tells the operator whether the operation is `submitting`, `pending/unknown`, `succeeded`, `failed retryable`, `unsupported/unavailable`, or `failed final`.
8. Admin UI does not claim success until the tenant state is confirmed by read model or command-status contract. On timeout, it communicates that the request may still be processing and offers a safe next action such as checking status or refreshing before retrying.
9. Tenant read failures remain distinct from tenant command failures. `GET /api/v1/admin/tenants` timeout, failed envelope, 403, 404, malformed payload, and unavailable EventStore paths must not collapse into silent empty lists or overwrite command failure state.
10. If no pollable command-status contract exists for the operation, UI and API copy must use `status unknown` or equivalent wording, not `pending`, unless a backend-owned pending state is actually available.
11. If a retry action is exposed after timeout or unknown status, the story must prove duplicate lifecycle commands are idempotent, safely rejected, or guarded by a command/operation ID; otherwise the primary recovery action is status check or refresh, not retry.
12. Operator personas are satisfied: Admin sees honest `status unknown` or confirmed lifecycle state with operation ID after timeout; Support can use correlation ID to find logs; Developer can distinguish command timeout from tenant-list read failure; Auditor never sees deletion semantics or fabricated lifecycle state.
13. Manual retest records the Issue 18 line with exact messages, links or IDs for the runtime evidence, and whether Redis/DAPR state was cold-start, warm-start, or reset-state:

   ```text
   Issue 18: OK / KO
   Notes / messages exacts:
   -
   State mode: cold-start / warm-start / reset-state
   ```

## Tasks / Subtasks

- [x] Reproduce and classify the timeout path. (AC: 1, 2)
  - [x] Start Aspire in dev mode using the repository instructions: `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`.
  - [x] Confirm DAPR placement and scheduler are running before testing actor flows.
  - [x] Record whether Redis/DAPR state is cold-start, warm-start, or reset-state before the manual run.
  - [x] Exercise `/tenants` as Admin, create or reuse `manual-test-tenant-a`, then Disable and Enable.
  - [x] Capture EventStore, Admin.Server, Admin.UI, and DAPR actor trace/log entries with the same correlation ID when available.
  - [x] Write the diagnostic decision record in this story's Dev Agent Record before implementing the fix.
- [x] Follow the actor and query routing chain. (AC: 2, 7)
  - [x] Verify Admin.Server `DaprTenantCommandService` sends `EnableTenant` to EventStore `api/v1/commands` with tenant `system`, domain `tenants`, aggregate `manual-test-tenant-a`.
  - [x] Verify EventStore routes to the expected aggregate actor ID `system:tenants:manual-test-tenant-a`.
  - [x] Verify tenant list queries route through `TenantsProjectionActor` with actor ID `tenants:system:index`, matching the existing QueryRouter regression.
  - [x] Check DAPR access-control YAML and app IDs before assuming application logic is hung.
- [x] Fix the root cause with the smallest behavioral change. (AC: 2, 3)
  - [x] Prefer correcting actor registration, routing, access-control, command-envelope, or projection-query defects over increasing timeouts.
  - [x] If a timeout remains expected, return an honest pending/retryable result and define the follow-up polling/status flow.
  - [x] Prefer one simplest sufficient root cause before widening scope. Do not fix both command and query paths unless evidence shows both are independently defective.
- [x] Harden Admin.Server operation classification. (AC: 5, 6)
  - [x] Preserve the generated correlation ID in timeout/unavailable results after a command body has been created.
  - [x] Do not map every `AdminOperationResult.Success=false` to `422`; implement the explicit status mapping table from the Acceptance Criteria.
  - [x] Keep raw EventStore response bodies and exception stack traces out of `AdminOperationResult.Message` and `ProblemDetails.Detail`.
- [x] Harden Admin UI recovery. (AC: 7, 8, 9)
  - [x] Ensure `_isOperating` is always cleared and `InvokeAsync(StateHasChanged)` is used after async work.
  - [x] Keep the lifecycle dialog or page in a state where the operator can retry or inspect the operation.
  - [x] Show the returned operation/correlation ID when it helps the operator find logs or command status.
  - [x] Do not mutate the visible tenant status optimistically after timeout or unknown status.
  - [x] If retry remains visible after timeout, prove duplicate Enable/Disable commands are idempotent or safely rejected; otherwise prefer check-status or refresh.
- [x] Run targeted chaos probes when feasible. (AC: 7, 8, 9, 11, 12)
  - [x] DAPR EventStore invocation unavailable.
  - [x] Tenants domain processor unavailable.
  - [x] `TenantsProjectionActor` hangs or times out while command submission succeeds.
  - [x] Command times out client-side but later completes.
  - [x] User double-clicks or retries after timeout.
  - [x] Browser disconnects during lifecycle operation.
- [x] Add focused tests. (AC: 3, 5, 6, 7, 8, 9)
  - [x] Add or update Admin.Server tests for command timeout/unavailable classification and correlation ID preservation.
  - [x] Add or update controller tests for non-422 timeout/unavailable behavior if controller mapping changes.
  - [x] Add or update Admin.UI tests proving lifecycle failure clears loading, keeps retry/status action available, and does not let tenant list refresh failure overwrite command failure.
  - [x] Add or update a happy-path Enable/Disable test to prove status classification did not regress successful lifecycle operations.
  - [x] Add or update EventStore/QueryRouter/Tenants tests only for the concrete runtime root cause.
- [x] Validate and record evidence. (AC: 1, 2, 13)
  - [x] Run the narrowest affected test projects individually.
  - [x] Rerun the Issue 18 manual flow and record exact messages and relevant trace/log IDs.
  - [x] Confirm no 100-second `HttpClient.Timeout` is observed in EventStore logs for `EnableTenant` after the fix, or record the explicit pending/async contract that replaces synchronous completion.

### Review Findings

- [x] [Review][Patch] Remove the `Hexalith.Tenants` submodule rewind or document an explicitly reviewed rollback. [`Hexalith.Tenants`:1]
- [x] [Review][Patch] Sanitize tracked runtime evidence so local absolute paths and Aspire login tokens are not committed. [`_bmad-output/implementation-artifacts/post-epic-deferred-dw13-tenant-lifecycle-actor-timeout-investigation.md`:336]
- [x] [Review][Patch] Preserve upstream timeout/unavailable/authorization status codes instead of collapsing typed ProblemDetails responses to `rejected`/422. [`src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`:279]
- [x] [Review][Patch] Classify unexpected post-operation-ID defects as `unexpected`/500, not blanket `unavailable`/503. [`src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`:303]
- [x] [Review][Patch] Carry `operationId` and `errorCode` from ProblemDetails extensions through the UI client even when `detail` omits them. [`src/Hexalith.EventStore.Admin.UI/Services/AdminTenantApiClient.cs`:316]
- [x] [Review][Patch] Add coverage for accepted lifecycle command followed by tenant-list refresh failure so command result state is not obscured. [`tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs`:345]
- [x] [Review][Patch] Reconcile acceptance evidence: the original timeout was not reproduced, the regression seam is above the actor boundary, chaos/browser probes are lower-confidence where runtime coverage was not performed, and full Admin.UI verification currently has one unrelated-looking failure. [`_bmad-output/implementation-artifacts/post-epic-deferred-dw13-tenant-lifecycle-actor-timeout-investigation.md`:343]

## Dev Notes

### Current State To Preserve

- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs` is the Admin.Server command facade. It creates `CreateTenant`, `DisableTenant`, `EnableTenant`, user, and role commands and posts them to EventStore's `api/v1/commands` through DAPR service invocation. It forwards the bearer token and bounds calls with `AdminServerOptions.ServiceInvocationTimeoutSeconds`.
- `DaprTenantCommandService.SubmitCommandAsync` currently generates a correlation ID before posting the command, but timeout and generic failure paths return `OperationId="error-no-operation"`. For this story, once a command attempt has a correlation ID, the result should preserve it unless no command was created.
- `AdminTenantsController` currently maps every failed tenant command result to `422 Unprocessable Entity`. That is correct for domain/business rejections only if the message is sanitized; it is misleading for timeout, unavailable, or pending command states.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` handles list/detail/user tenant reads through EventStore's query endpoint. It already has specific failed-envelope classification and a timeout-to-`TimeoutException` path. Preserve the distinction between semantic upstream failures and transport/unavailable failures.
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` already uses `_isOperating` and `finally` blocks with `InvokeAsync(StateHasChanged)` for lifecycle actions. Preserve this pattern and make any new status/pending UI fit the existing Fluent UI page rather than adding a new UI framework.
- Existing tests to extend first: `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTenantCommandServiceTests.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminTenantsControllerTests.cs`, `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs`, and `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminTenantApiClientTests.cs`.

### Required Failure Classification

Admin.Server and Admin.UI must share stable semantics instead of inferring meaning from raw exception text:

| Failure or result | Backend status | Stable code | UI state | Required operator message behavior |
| --- | --- | --- | --- | --- |
| Command accepted and tenant state confirmed | `202` or final success contract | `accepted` / `completed` | `succeeded` | Success copy may say tenant is enabled/disabled only after confirmation. |
| Command accepted but final tenant state not yet confirmed | `202` | `pending` | `pending/unknown` | Say the request was accepted and provide operation/correlation ID plus status/refresh action. |
| DAPR/EventStore timeout after command was submitted | `503` or `504` | `timeout` | `pending/unknown` or `failed retryable` | Say the request timed out and may still be processing; do not claim final failure without status proof. |
| EventStore or DAPR unavailable/routing failure | `503` | `unavailable` | `unsupported/unavailable` or `failed retryable` | Tell the operator the backend is unavailable and preserve operation/correlation ID if one exists. |
| Domain/business rejection | `422` | rejection type or `rejected` | `failed final` | Show sanitized rejection summary; no stack traces or raw response body. |
| Malformed client input | `400` | `invalid-request` | `failed final` | Show field/actionable validation message. |
| Unexpected server defect | `500` | `unexpected` | `failed retryable` | Generic failure copy plus correlation ID; no internals. |

`OperationId="error-no-operation"` is allowed only before any command/correlation ID has been generated. Once `SubmitCommandAsync` has built a command body, timeout and unavailable paths must preserve the generated operation/correlation ID.

### Decision Questions Before Coding

Answer these in the Dev Agent Record before changing behavior:

1. Is Enable/Disable intended to complete synchronously, or is it accepted-then-reconciled?
2. What exact source confirms final tenant lifecycle state: command status, tenant read model, event stream, or another contract?
3. Are duplicate Enable/Disable commands idempotent, safely rejected, or guarded by a stable command/operation ID?
4. Which layer owns timeout classification for this flow: Admin.Server facade, EventStore command pipeline, DAPR resiliency policy, or a combination?
5. Are the command timeout and tenant-list read timeout one shared root cause or two independent failures?
6. What is the simplest sufficient root cause proven by evidence?

### Lifecycle Operation Contract Decision

Complete this mini ADR in the Dev Agent Record before changing API or UI success semantics:

```markdown
#### Lifecycle Operation Contract Decision

- Decision:
- Options considered:
- Why selected:
- Consequences:
- Tests proving contract:
```

### Failure Mode Matrix

The implementation and tests must cover or explicitly rule out each row. A mocked Admin.Server-only test is not enough for rows where the reproduced evidence crosses DAPR/EventStore actor boundaries unless the story records why the selected seam is equivalent to the runtime failure.

| Failure mode | Required proof | Minimum expected behavior |
| --- | --- | --- |
| EventStore command timeout after `EnableTenant` submission | Trace/log proof at EventStore command or actor invocation boundary, plus regression at the closest reproducible seam | Structured timeout/unavailable response, preserved operation/correlation ID, no raw exception leakage |
| DAPR ACL or routing failure | App ID, method route, and DAPR config evidence | `503` or precise unavailable/routing classification; not `422`; operator can retry/check after config fix |
| Tenants domain processor missing or unavailable | Proof that EventStore attempted or could not attempt the configured Tenants `/process` route | Backend-owned unavailable/failure classification; no UI inference from generic exception |
| `TenantsProjectionActor` timeout for `tenants:system:index` | QueryRouter/projection actor trace or focused test using the expected actor ID and actor type | Tenant list unavailable/degraded state; never silent empty list |
| Tenant list read timeout after command acceptance | Separate command and read-path evidence | Command result remains visible; list failure does not overwrite lifecycle operation state |
| Command succeeds but read model lags | Command-status or read-model reconciliation proof | UI says request accepted/status unknown until confirmation; no `Tenant enabled` success copy before confirmation |
| User retries or double-clicks after timeout | Idempotency/safe-rejection proof with command or operation identity | No duplicate unsafe lifecycle mutation; UI guides status check/refresh when retry is not proven safe |
| Browser disconnects during lifecycle operation | UI/client recovery proof or documented operator-deferred runtime evidence | Operation result remains recoverable through status/log correlation; no stuck loading state after reconnect/reload |

### UI State Requirements

- The visible states are `submitting`, `pending/unknown`, `succeeded`, `failed retryable`, `unsupported/unavailable`, and `failed final`.
- No success toast may say `Tenant enabled` or `Tenant disabled` until the read model or command-status contract confirms that state.
- On timeout, use honest copy such as `Enable request timed out. The operation may still be processing.` and expose a safe next action: check status, refresh, or retry only when idempotency/safety is clear.
- The tenant row must not be optimistically overwritten after timeout. A transient uncertain-state indicator is acceptable; fabricated certainty is not.
- Technical IDs belong in toast details, dialog footer, or expandable technical details, not as the primary operator-facing headline.
- Command failure and tenant-list failure are separate UI concepts. A list refresh error must not replace the lifecycle command result, and a lifecycle command timeout must not make the tenant grid look empty.
- Admin persona: sees honest lifecycle state, `status unknown`, or confirmed failure with a safe next action.
- Support persona: receives operation/correlation ID sufficient to locate Aspire/EventStore/Admin logs.
- Developer persona: can tell whether the failure is command-path, read-path, routing, projection, or infrastructure.
- Auditor persona: never sees Delete semantics or a fabricated lifecycle transition.

### Definition Of Done: Diagnostic Proof

Before moving this story to done, the Dev Agent Record must include:

1. Root cause category and the exact failing component or hop.
2. Reproduction command and environment, including `EnableKeycloak=false`, DAPR placement, and scheduler status.
3. Trace/log IDs proving the failing path and the fixed path.
4. The red regression test that failed before the fix and passes after it.
5. Evidence that write failure and tenant-list read failure were tested or explicitly proven to share one fixed root cause.
6. Evidence that other suspected causes from the investigation checklist were ruled out.
7. Manual Issue 18 retest result with exact UI messages.
8. Confirmation that the fix did not merely increase timeout values.
9. Confirmation that no optimistic tenant-state mutation occurs after timeout or unknown status.
10. Confirmation that command failure and tenant-list failure remain separate in API responses and UI state.
11. State-mode note for manual evidence: cold-start, warm-start, or reset-state.
12. Lifecycle Operation Contract Decision mini ADR.
13. Results or explicit deferral for each targeted chaos probe.

### Root Cause Decision Record Template

Complete this in the Dev Agent Record before implementing or marking done:

```markdown
#### Root Cause Decision Record

- Failing hop:
- Failure class:
- Trace/log IDs:
- Reproduction command:
- Environment notes:
- Fix selected:
- Alternatives ruled out:
- Regression test:
- Manual Issue 18 result:
```

### Root-Cause Investigation Checklist

Investigate these in order and record the answer in the Dev Agent Record:

1. Infrastructure readiness: DAPR placement and scheduler are running; sidecars show the expected actor types.
2. DAPR access control: `eventstore-admin`, `eventstore`, and `tenants` app IDs are permitted for the exact service invocation and actor paths used by the flow.
3. Aggregate actor routing: `EnableTenant` reaches EventStore's aggregate actor for actor ID `system:tenants:manual-test-tenant-a`.
4. Tenants domain processor: the Tenants service exposes the `/process` route and EventStore domain-service registry points to the correct DAPR app ID and path.
5. Command envelope validity: `messageId`, `correlationId`, and any identifiers accepted by EventStore validators use the expected ID format. Project rules prefer ULIDs for message/correlation/aggregate/causation IDs; do not silently continue using GUID-shaped IDs if validation rejects or misroutes them.
6. Projection query routing: list-tenants uses actor ID `tenants:system:index` and actor type `TenantsProjectionActor`, as pinned in `QueryRouterTests.RouteQueryAsync_ListTenants_UsesTenantsProjectionActorAtSystemIndex`.
7. Actor or projection deadlock: check whether `TenantsProjectionActor` is blocked, repeatedly reactivated, waiting on state, or timing out because a previous actor turn is stuck.

### Architecture And Technology Guardrails

- .NET target is `net10.0`; warnings are treated as errors. Keep nullable annotations clean.
- Use existing DAPR, Aspire, Fluent UI, xUnit v3, Shouldly, and NSubstitute patterns. Do not add libraries for this investigation.
- DAPR actor state must remain behind actor boundaries. Do not bypass aggregate/projection actors with raw Redis or state-store reads to "fix" lifecycle state.
- Do not add ad hoc retry loops in application code. Retry/timeout behavior belongs to existing HTTP/DAPR/Aspire resiliency configuration unless the story proves a local facade needs a bounded status/pending classification.
- Tenant lifecycle is owned by Hexalith.Tenants. EventStore Admin consumes lifecycle commands and projections; it must not create a parallel tenant store.
- Never log command payloads, event payloads, secrets, JWTs, or PII. Use tenant/domain/aggregate IDs, command type, correlation ID, actor type, and stage.
- DAPR docs current to 2026-05-20 confirm actor idle timeout defaults to 60 minutes and actor scan interval to 30 seconds; idle deactivation is not a 30-second command timeout explanation by itself. See <https://docs.dapr.io/developing-applications/building-blocks/actors/actors-runtime-config/>.
- DAPR resiliency docs current to 2026-05-20 confirm timeout policies terminate long-running operations where possible and return errors; unspecified DAPR timeout falls back to the request client. See <https://docs.dapr.io/operations/resiliency/policies/timeouts/> and <https://docs.dapr.io/operations/resiliency/resiliency-overview/>.
- Microsoft `HttpClient` docs state the default `HttpClient.Timeout` is 100 seconds and the request task is cancelled when it is reached. This matches the EventStore-side `TaskCanceledException` evidence and should be classified deliberately rather than shown as a raw exception. See <https://learn.microsoft.com/dotnet/fundamentals/runtime-libraries/system-net-http-httpclient#timeouts>.

### Previous Story Intelligence

- DW11 fixed Admin action binding and projection detail fallback. Preserve its rule: do not hide timeout/cancellation/authorization/malformed response failures behind fallback behavior.
- DW12 owns consistency checker actor-state contract plus dispatcher fixes and is already in progress in sprint status. This DW13 story may reference DW12 as a dependency only if tenant list timeout evidence overlaps the consistency trace, but it must not implement DW12's consistency checker changes.
- Recent QueryRouter work fixed the weak ActorProxy path and added tests proving list-tenants routes to `TenantsProjectionActor`. Use those tests as the first regression reference before adding new routing abstractions.

### Testing Guidance

Run test projects individually:

```powershell
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests
```

If the root cause is inside EventStore query or actor routing, add a focused Server.Tests slice and run the smallest matching filter. Be aware of the documented pre-existing full `Hexalith.EventStore.Server.Tests` CA2007 warning-as-error issue; do not claim new failure ownership without a focused reproduction.

Runtime verification requires Aspire and DAPR:

```powershell
$env:EnableKeycloak = 'false'
aspire run --project .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Use the manual runbook Issue 18 steps:

1. Role `Admin`.
2. Open `/tenants`.
3. Create or reuse `manual-test-tenant-a`.
4. Disable, then Enable.
5. Verify status returns to active or record the exact structured pending/failed state.
6. Confirm no Delete action appears.
7. Filter and clear the tenant list.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md` section 5.4.
- `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md` CC-5 / Issue 18.
- `_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-deferred-entry-runbook.md` Issue 18 manual flow.
- `_bmad-output/planning-artifacts/architecture.md` DAPR actor/resiliency and tenant lifecycle validation decisions.
- `_bmad-output/planning-artifacts/epics.md` FR90 and Epic 16.5 tenant management context.
- `_bmad-output/implementation-artifacts/post-epic-deferred-dw11-admin-action-binding-and-projection-detail-contracts.md`.
- `_bmad-output/implementation-artifacts/post-epic-deferred-dw12-consistency-actor-state-contract-and-dispatcher-fix.md`.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`.
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`.
- `src/Hexalith.EventStore.Admin.UI/Services/AdminTenantApiClient.cs`.
- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex.

### Debug Log References

- 2026-05-21T10:55:34+02:00 - Dev workflow started. Attempted baseline Aspire run with `EnableKeycloak=false aspire run --detach --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json --non-interactive`; startup failed before AppHost evaluation because `global.json` requires .NET SDK `10.0.300` with `rollForward: latestPatch`, while the default PATH exposed SDK `10.0.103`. Aspire CLI log path redacted to the local user profile.
- 2026-05-21T11:07:47+02:00 - Installed .NET SDK `10.0.300` user-locally under the developer profile using Microsoft `dotnet-install.ps1` so the pinned `global.json` could be honored.
- 2026-05-21T11:09:23+02:00 - Started Aspire with `EnableKeycloak=false`, `MSBUILDDISABLENODEREUSE=1`, and `DOTNET_CLI_USE_MSBUILD_SERVER=0`; Aspire dashboard login token and local CLI log path redacted; AppHost PID `23924`; CLI PID `24952`.
- 2026-05-21T11:10:00+02:00 - `aspire describe --format Json --non-interactive` showed `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `tenants`, `statestore`, `pubsub`, and DAPR sidecars running and healthy. Docker placement/scheduler evidence: `dapr_placement` and `dapr_scheduler` containers running.
- 2026-05-21T11:12:00+02:00 - Runtime Admin.Server flow with dev JWT (`role=global-admin`, `eventstore:tenant=system`) on cold-start state: initial `GET http://localhost:8090/api/v1/admin/tenants` returned `200 []`.
- 2026-05-21T11:12:04+02:00 - `POST /api/v1/admin/tenants` created `manual-test-tenant-a`; Admin.Server response `202 {"success":true,"message":null,"errorCode":null,"operationId":"01KS4WWT9M92CR4CPZ4NHBK3V4"}`; follow-up list returned `200 [{"status":0,"tenantId":"manual-test-tenant-a","name":"Manual Test Tenant A"}]`.
- 2026-05-21T11:12:08+02:00 - `POST /api/v1/admin/tenants/manual-test-tenant-a/disable` returned `202 {"success":true,"message":null,"errorCode":null,"operationId":"01KS4WWXNWF5HE8TRBF2D0Z6X1"}`; follow-up list returned status `1` (Disabled).
- 2026-05-21T11:12:11+02:00 - `POST /api/v1/admin/tenants/manual-test-tenant-a/enable` returned `202 {"success":true,"message":null,"errorCode":null,"operationId":"01KS4WX0TEG405YHWW3QYBB12Z"}`; follow-up list returned status `0` (Active). No EventStore `HttpClient.Timeout` or `ActorInvocationFailed` appeared for this operation in filtered logs.
- EventStore log evidence for `01KS4WX0TEG405YHWW3QYBB12Z`: `Command received` for `EnableTenant`, actor ID `system:tenants:manual-test-tenant-a`, `Domain service completed: AppId=tenants, ResultType=Success`, `Events persisted`, `Events published`, and `Command completed summary ... Status=Completed, DurationMs=48.247`.
- 2026-05-21 code review patch verification: `dotnet test tests\Hexalith.EventStore.Admin.Server.Tests --no-restore --configuration Release --filter "FullyQualifiedName~AdminTenantsControllerTests|FullyQualifiedName~DaprTenantCommandServiceTests" -m:1` passed with 32/32 tests.
- 2026-05-21 code review patch verification: `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests --no-restore --configuration Release --filter "FullyQualifiedName~TenantsPageTests|FullyQualifiedName~AdminTenantApiClientTests" -m:1` passed with 38/38 tests.
- 2026-05-21 broad Admin UI verification: `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests --no-restore --configuration Release -m:1` failed one unrelated-looking test, `JsonViewerTests.JsonViewer_ShowsWarning_WhenJsonIsInvalid`; 804/805 tests passed.

#### Lifecycle Operation Contract Decision

- Decision: Admin.Server command submission remains `202 Accepted`, while Admin UI treats lifecycle state as `status unknown` until the tenant read model confirms Active or Disabled.
- Options considered: synchronous final lifecycle success; accepted-then-reconciled through read-model confirmation; accepted with pollable command-status contract.
- Why selected: the current public Admin.Server contract returns accepted command operation IDs and the UI already refreshes the tenant list; no Admin UI pollable command-status contract is wired for tenant lifecycle operations in this story.
- Consequences: UI no longer says `Tenant enabled` or `Tenant disabled` on `202`; it says the request was accepted, shows the operation ID, and asks the operator to refresh/check status. Timeout/unavailable responses preserve operation ID and are classified as `504`/`503`, not generic `422`.
- Tests proving contract: `TenantsPage_EnableAccepted_ShowsStatusUnknownCopyAndDoesNotClaimEnabled`, `EnableTenantAsync_ThrowsServiceUnavailableWithProblemDetail_WhenGatewayTimeout`, `EnableTenant_ReturnsGatewayTimeout_WhenCommandServiceClassifiesTimeout`.

#### Root Cause Decision Record

- Failing hop: Admin.Server tenant command facade/controller/UI lifecycle boundary for timeout and accepted-operation semantics; runtime actor hop was healthy in the 2026-05-21 cold-start verification.
- Failure class: `timeout-classification-gap` plus `operation-id-preservation-gap`; envelope ID format was also corrected from GUIDs to sortable ULIDs to match EventStore command conventions.
- Trace/log IDs: create `01KS4WWT9M92CR4CPZ4NHBK3V4`; disable `01KS4WWXNWF5HE8TRBF2D0Z6X1`; enable `01KS4WX0TEG405YHWW3QYBB12Z`.
- Reproduction command: `EnableKeycloak=false aspire run --detach --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json --non-interactive` with `MSBUILDDISABLENODEREUSE=1` and `DOTNET_CLI_USE_MSBUILD_SERVER=0`, followed by Admin.Server HTTP calls on `http://localhost:8090`.
- Environment notes: SDK `10.0.300` installed user-local; DAPR placement/scheduler running as Docker containers; DAPR/Redis state was cold-start for the manual flow because initial tenant list returned `[]`.
- Fix selected: generate ULID message/correlation IDs in `DaprTenantCommandService`; preserve generated operation ID on timeout/unavailable after command body creation; map Admin.Server failures explicitly (`400`, `422`, `503`, `504`, `500`); surface ProblemDetails detail in UI client; change UI accepted lifecycle copy to `status unknown` instead of final success.
- Alternatives ruled out: no timeout-value-only change; no DAPR/Aspire resiliency timeout increase; no raw actor state access; no EventStore/Tenants actor routing change because current runtime evidence completed through aggregate actor and Tenants domain processor.
- Regression test: `EnableTenantAsync_PreservesOperationId_WhenInvocationTimesOutAfterCommandBodyExists` failed against the previous `error-no-operation` timeout path and now passes; controller and UI tests cover status mapping and unknown-status copy.
- Manual Issue 18 result:

  ```text
  Issue 18: OK for cold-start retest; original timeout not reproduced.
  Notes / messages exacts:
  - Create tenant returned 202 with operation ID 01KS4WWT9M92CR4CPZ4NHBK3V4.
  - Disable tenant returned 202 with operation ID 01KS4WWXNWF5HE8TRBF2D0Z6X1; follow-up tenant list confirmed Disabled.
  - Enable tenant returned 202 with operation ID 01KS4WX0TEG405YHWW3QYBB12Z; follow-up tenant list confirmed Active.
  - No 100-second EventStore HttpClient.Timeout or ActorInvocationFailed log was observed for the EnableTenant retest.
  State mode: cold-start
  ```

#### Targeted Chaos Probe Results

- DAPR/EventStore invocation unavailable: covered by Admin.Server facade tests that throw transport exceptions and classify as `unavailable` with preserved operation ID after command body creation.
- Tenants domain processor unavailable: current runtime evidence proves the configured `tenants` AppId `/process` path is reachable; unavailable classification is covered at the Admin.Server boundary.
- `TenantsProjectionActor` timeout while command submission succeeds: current list route returned `200` before and after lifecycle commands; existing QueryRouter regression remains the pinned actor-ID proof.
- Command times out client-side but later completes: covered by timeout regression preserving operation ID and UI copy that says operation may still be processing.
- User double-click/retry after timeout: UI primary guidance is refresh/check status; no new retry action was added.
- Browser disconnect during lifecycle operation: no browser-only runtime probe was added; recovery is covered only by non-optimistic UI state and operation ID copy, so this remains lower-confidence than the API and component-test evidence.

### Completion Notes List

- Implemented explicit tenant command failure classification in Admin.Server: `invalid-request`/400, rejected/422, unavailable/503, timeout/504, unexpected/500.
- Preserved generated operation/correlation IDs for timeout and unavailable command paths after a command body exists.
- Code review patch pass tightened HTTP failure classification so RFC 7807 `type` fields no longer collapse unavailable/timeout/authorization failures into `rejected`/422, and unexpected local defects now map to `unexpected`/500.
- Code review patch pass updated the Admin UI client to preserve ProblemDetails `operationId` and `errorCode` extension values even when `detail` omits them.
- Code review patch pass restored the `Hexalith.Tenants` submodule pointer to the parent repo baseline and redacted local paths/Aspire login-token evidence from this story.
- Changed tenant command message/correlation IDs from GUIDs to sortable ULIDs.
- Updated Admin UI lifecycle accepted-state messaging so it reports `status unknown` and operation ID until read-model confirmation, instead of claiming final enabled/disabled state immediately.
- Runtime Issue 18 cold-start retest did not reproduce the original timeout: create, disable, and enable completed through EventStore aggregate actor and Tenants domain processor; no 100-second EventStore `HttpClient.Timeout` observed.
- Full `Hexalith.EventStore.Admin.UI.Tests` currently has one unrelated-looking failure in Release: `JsonViewerTests.JsonViewer_ShowsWarning_WhenJsonIsInvalid`; targeted tenant UI/client tests pass.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw13-tenant-lifecycle-actor-timeout-investigation.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminTenantApiClient.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminTenantsControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTenantCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminTenantApiClientTests.cs`

### Change Log

- 2026-05-21: Implemented DW13 tenant lifecycle operation classification, accepted-state UI copy, ULID command IDs, focused regression tests, and cold-start Aspire manual retest evidence.
