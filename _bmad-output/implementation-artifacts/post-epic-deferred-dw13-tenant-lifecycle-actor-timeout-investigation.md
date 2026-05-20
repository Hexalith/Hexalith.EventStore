# Post-Epic Deferred DW13: Tenant Lifecycle Actor Timeout Investigation

Status: ready-for-dev

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

- [ ] Reproduce and classify the timeout path. (AC: 1, 2)
  - [ ] Start Aspire in dev mode using the repository instructions: `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`.
  - [ ] Confirm DAPR placement and scheduler are running before testing actor flows.
  - [ ] Record whether Redis/DAPR state is cold-start, warm-start, or reset-state before the manual run.
  - [ ] Exercise `/tenants` as Admin, create or reuse `manual-test-tenant-a`, then Disable and Enable.
  - [ ] Capture EventStore, Admin.Server, Admin.UI, and DAPR actor trace/log entries with the same correlation ID when available.
  - [ ] Write the diagnostic decision record in this story's Dev Agent Record before implementing the fix.
- [ ] Follow the actor and query routing chain. (AC: 2, 7)
  - [ ] Verify Admin.Server `DaprTenantCommandService` sends `EnableTenant` to EventStore `api/v1/commands` with tenant `system`, domain `tenants`, aggregate `manual-test-tenant-a`.
  - [ ] Verify EventStore routes to the expected aggregate actor ID `system:tenants:manual-test-tenant-a`.
  - [ ] Verify tenant list queries route through `TenantsProjectionActor` with actor ID `tenants:system:index`, matching the existing QueryRouter regression.
  - [ ] Check DAPR access-control YAML and app IDs before assuming application logic is hung.
- [ ] Fix the root cause with the smallest behavioral change. (AC: 2, 3)
  - [ ] Prefer correcting actor registration, routing, access-control, command-envelope, or projection-query defects over increasing timeouts.
  - [ ] If a timeout remains expected, return an honest pending/retryable result and define the follow-up polling/status flow.
  - [ ] Prefer one simplest sufficient root cause before widening scope. Do not fix both command and query paths unless evidence shows both are independently defective.
- [ ] Harden Admin.Server operation classification. (AC: 5, 6)
  - [ ] Preserve the generated correlation ID in timeout/unavailable results after a command body has been created.
  - [ ] Do not map every `AdminOperationResult.Success=false` to `422`; implement the explicit status mapping table from the Acceptance Criteria.
  - [ ] Keep raw EventStore response bodies and exception stack traces out of `AdminOperationResult.Message` and `ProblemDetails.Detail`.
- [ ] Harden Admin UI recovery. (AC: 7, 8, 9)
  - [ ] Ensure `_isOperating` is always cleared and `InvokeAsync(StateHasChanged)` is used after async work.
  - [ ] Keep the lifecycle dialog or page in a state where the operator can retry or inspect the operation.
  - [ ] Show the returned operation/correlation ID when it helps the operator find logs or command status.
  - [ ] Do not mutate the visible tenant status optimistically after timeout or unknown status.
  - [ ] If retry remains visible after timeout, prove duplicate Enable/Disable commands are idempotent or safely rejected; otherwise prefer check-status or refresh.
- [ ] Run targeted chaos probes when feasible. (AC: 7, 8, 9, 11, 12)
  - [ ] DAPR EventStore invocation unavailable.
  - [ ] Tenants domain processor unavailable.
  - [ ] `TenantsProjectionActor` hangs or times out while command submission succeeds.
  - [ ] Command times out client-side but later completes.
  - [ ] User double-clicks or retries after timeout.
  - [ ] Browser disconnects during lifecycle operation.
- [ ] Add focused tests. (AC: 3, 5, 6, 7, 8, 9)
  - [ ] Add or update Admin.Server tests for command timeout/unavailable classification and correlation ID preservation.
  - [ ] Add or update controller tests for non-422 timeout/unavailable behavior if controller mapping changes.
  - [ ] Add or update Admin.UI tests proving lifecycle failure clears loading, keeps retry/status action available, and does not let tenant list refresh failure overwrite command failure.
  - [ ] Add or update a happy-path Enable/Disable test to prove status classification did not regress successful lifecycle operations.
  - [ ] Add or update EventStore/QueryRouter/Tenants tests only for the concrete runtime root cause.
- [ ] Validate and record evidence. (AC: 1, 2, 13)
  - [ ] Run the narrowest affected test projects individually.
  - [ ] Rerun the Issue 18 manual flow and record exact messages and relevant trace/log IDs.
  - [ ] Confirm no 100-second `HttpClient.Timeout` is observed in EventStore logs for `EnableTenant` after the fix, or record the explicit pending/async contract that replaces synchronous completion.

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

TBD by dev agent.

### Debug Log References

- Add Aspire structured log IDs, console log snippets, trace IDs, and command correlation IDs here.

### Completion Notes List

- Do not mark this story done until the automated regression and manual Issue 18 evidence are both recorded, or until manual evidence is explicitly operator-deferred with a dated reason.

### File List

- TBD by dev agent.
