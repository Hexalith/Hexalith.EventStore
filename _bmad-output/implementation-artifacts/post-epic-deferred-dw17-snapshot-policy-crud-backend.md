# Post-Epic Deferred DW17: Snapshot Policy CRUD Backend

Status: done

Context created: 2026-05-21
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-admin-deferred-quick-wins.md`
Source proposal status: Approved by Jerome on 2026-05-21 via Party Mode review follow-up.
Source baseline: `_bmad-output/implementation-artifacts/post-epic-deferred-dw14-admin-deferred-operations-ux-policy.md`
Previous story: `_bmad-output/implementation-artifacts/post-epic-deferred-dw16-manual-snapshot-creation-backend.md`
Related completed baseline: `_bmad-output/implementation-artifacts/16-2-snapshot-management-and-auto-snapshot-policies.md`
Party-mode review fixes applied: 2026-05-21
Advanced elicitation refinements applied: 2026-05-21

## Story

As an EventStore operator using the Admin API, Admin UI, CLI, or MCP surfaces,
I want snapshot policy set/delete requests to persist real aggregate-type policy configuration and be used by command-time automatic snapshot decisions,
so that snapshot policies shown on `/snapshots` are no longer deferred placeholders and actually control automatic snapshot cadence.

## Decision Locked For This Story

Implement the Group A DW17 backend slice from the 2026-05-21 sprint change proposal. This story replaces the `Deferred` responses for `SetSnapshotPolicyAsync` and `DeleteSnapshotPolicyAsync` with real EventStore-owned snapshot policy persistence plus command-time policy resolution.

This is not just CRUD. A policy written through Admin.Server must be visible through `GetSnapshotPoliciesAsync` and must be consulted by the automatic snapshot engine during aggregate command processing. A dev implementation that only writes UI index rows without changing runtime snapshot interval resolution is incomplete.

## Scope

This story covers snapshot policy CRUD only:

- Admin.Server `DaprStorageCommandService.SetSnapshotPolicyAsync` and `DeleteSnapshotPolicyAsync`.
- EventStore upstream admin routes for policy set/delete using the route/DTO contract below.
- DAPR state index persistence for `admin:storage-snapshot-policies:all` and `admin:storage-snapshot-policies:{tenant}`.
- A runtime policy resolver used by `SnapshotManager.ShouldCreateSnapshotAsync` or a nearby server-owned service so command-time automatic snapshots honor persisted policies.
- Focused Admin.Server, EventStore.Server, Admin.Abstractions, Admin.UI/CLI client regression tests, and Tier 2/Aspire evidence when available.

Manual snapshot creation remains DW16. Stream export remains DW18. Compaction, backup, validation, restore, and import remain deferred/accepted debt. Per-aggregate policies, scheduled policies, policy history/audit UI, policy metrics, and policy autocomplete are out of scope.

## Route And Contract Decisions

These contracts are locked for DW17 unless implementation discovers an existing upstream route with the same behavior:

| Surface | Method | Route | Body/query | Response | Auth |
| --- | --- | --- | --- | --- | --- |
| Admin.Server external set | `PUT` | `api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy` | Query `intervalEvents`; no body | `AdminOperationResult` | `AdminAuthorizationPolicies.Operator` + `AdminTenantAuthorizationFilter` |
| Admin.Server external delete | `DELETE` | `api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy` | No body | `AdminOperationResult` | `AdminAuthorizationPolicies.Operator` + `AdminTenantAuthorizationFilter` |
| EventStore upstream set | `PUT` | `api/v1/admin/storage/snapshot-policy` | JSON `SnapshotPolicySetRequest(TenantId, Domain, AggregateType, IntervalEvents)` | `AdminOperationResult` | Operator/Admin + tenant authorization from forwarded bearer token |
| EventStore upstream delete | `DELETE` | `api/v1/admin/storage/snapshot-policy` | JSON `SnapshotPolicyDeleteRequest(TenantId, Domain, AggregateType)` | `AdminOperationResult` | Operator/Admin + tenant authorization from forwarded bearer token |

Admin.Server must call the EventStore app-id configured by `AdminServerOptions.EventStoreAppId` and forward the original `Authorization: Bearer ...` header from `IAdminAuthContext.GetToken()`. EventStore, not Admin.Server alone, is the final enforcement point for the upstream mutation.

`SnapshotPolicySetRequest` and `SnapshotPolicyDeleteRequest` may be separate records or a single request record if the implementation keeps nullability and validation clean. They belong in `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/` unless an existing contract already provides the same shape.

## Policy Identity And Precedence

DW17 supports exact active policies only. Wildcard tenant, wildcard domain, tenant-only policy, domain-only persisted policy, disabled policy, policy priority, and policy history are out of scope.

Policy identity is:

```text
canonicalTenantId | canonicalDomain | canonicalAggregateType
```

- Tenant/domain must be canonicalized through the same structural rules used for aggregate identity and storage keys.
- Aggregate type is required, trimmed, and matched with `StringComparer.OrdinalIgnoreCase` to prevent duplicate logical policies caused only by casing.
- Persisted `SnapshotPolicy.AggregateType` should preserve the first created casing unless a later update intentionally records a new display casing in the Dev Agent Record.
- Null/empty tenant, domain, or aggregateType is invalid; there is no global persisted policy row in this story.

Runtime snapshot interval resolution order is:

1. Exact persisted policy for `(tenantId, domain, aggregateType)`.
2. Existing static `SnapshotOptions.TenantDomainIntervals["tenantId:domain"]`.
3. Existing static `SnapshotOptions.DomainIntervals["domain"]`.
4. Existing static `SnapshotOptions.DefaultInterval`.

Deleting a persisted exact policy removes only step 1 and returns runtime behavior to the static fallback chain above. If a cache is used, delete must invalidate the exact policy entry immediately or guarantee a documented bounded stale window no longer than 30 seconds.

## Consistency And Conflict Decisions

Policy set/delete updates two active-list indexes because product needs both global admin listing and tenant-scoped listing to remain fast and complete:

```text
admin:storage-snapshot-policies:all
admin:storage-snapshot-policies:{tenantId}
```

Index mutation rules:

- Use a single EventStore-side snapshot policy repository/service to own row matching, sort order, ETag retry behavior, operation ids, and runtime policy lookup. Controllers and actors must not duplicate list-mutation logic.
- Use DAPR ETag optimistic concurrency for each list update, with exactly 5 immediate read/modify/`TrySaveStateAsync` attempts and no custom sleep/backoff loop.
- DAPR does not provide an atomic transaction across the `all` and tenant-scoped keys in this story. DW17 therefore requires bounded optimistic retries, typed non-success on unreconciled partial completion, safe reconciliation logging, and deterministic normalization on the next mutation for the same tenant.
- Operation semantics are last-write-wins after successful bounded ETag retry. Concurrent successful updates must converge to one active row per policy identity.
- A request returns success only after both `all` and tenant-scoped indexes reflect the same set/delete result.
- If either scope cannot be saved after retries, return `AdminOperationResult(false, operationId, safeMessage, "UpstreamUnavailable")` and log safe reconciliation details. Do not claim success on partial mutation.
- On the next successful set/delete for the same tenant, the repository must repair stale duplicate or missing entries in both scopes as part of normal list normalization.
- Delete of a missing policy returns `NotFound`. If one index contains a stale row and the other does not, delete repairs both indexes to the deleted state and returns success if the policy existed in either scope.

## Acceptance Criteria

1. **Admin.Server delegates snapshot policy writes to EventStore instead of returning deferred.**
   - Given an Operator/Admin calls `PUT /api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy?intervalEvents={n}`
   - When tenant authorization passes
   - Then `DaprStorageCommandService.SetSnapshotPolicyAsync` invokes an EventStore app-id upstream route through DAPR service invocation, forwards the bearer token, and returns the typed `AdminOperationResult`.
   - And it no longer returns `OperationId=deferred-snapshot-policy-set` / `ErrorCode=Deferred` for the happy path.
   - And `DeleteSnapshotPolicyAsync` follows the same pattern for delete requests.
   - And compaction, manual snapshot, backup, restore, import, and export behavior is not changed in this story.

2. **EventStore owns the upstream policy routes and authorization.**
   - Given a set/delete request with tenant, domain, and aggregateType
   - When EventStore receives the upstream policy route
   - Then the route authenticates the forwarded bearer token and enforces Operator/Admin plus tenant authorization before state mutation.
   - And direct EventStore route calls with no token, invalid issuer/audience, no tenant claim, wrong tenant, missing Operator/Admin permission, or malformed permissions are rejected before state mutation.
   - And request validation rejects blank tenant/domain/aggregateType, invalid structural characters, and invalid `intervalEvents`.
   - And invalid input returns a typed `AdminOperationResult` with `ErrorCode=RejectedValidation`, not an unstructured 500.
   - And missing/invalid credentials or tenant mismatch are rejected before any policy index update.

3. **Policy set persists an idempotent create/update row in both index scopes.**
   - Given a valid policy set request
   - When no matching row exists
   - Then EventStore appends `SnapshotPolicy(tenantId, domain, aggregateType, intervalEvents, CreatedAtUtc)` to both `admin:storage-snapshot-policies:all` and `admin:storage-snapshot-policies:{tenantId}`.
   - When a matching row already exists for the same canonical tenant/domain/aggregateType
   - Then the existing row is updated in place with the new interval, duplicates are not created, and `CreatedAtUtc` is preserved unless a conscious model decision in the implementation notes says otherwise.
   - And the operation id is deterministic for the policy key, such as `snapshot-policy-set-{sha256(canonicalTenant|canonicalDomain|canonicalAggregateType)}`.
   - And repeated identical PUTs are successful idempotent updates.

4. **Policy delete removes the row from both index scopes.**
   - Given a valid delete request for an existing policy
   - When EventStore processes the delete
   - Then the matching policy is removed from `admin:storage-snapshot-policies:all` and `admin:storage-snapshot-policies:{tenantId}`.
   - And the operation returns success with a deterministic operation id such as `snapshot-policy-delete-{sha256(canonicalTenant|canonicalDomain|canonicalAggregateType)}`.
   - Given the policy does not exist
   - When delete is requested
   - Then the operation returns a typed non-success `AdminOperationResult` with `ErrorCode=NotFound`.
   - And clean not-found paths with no stale index rows must not mutate either index.

5. **Index updates are portable and concurrency-safe.**
   - Given concurrent set/delete requests for the same or adjacent policies
   - When multiple replicas update snapshot policy indexes
   - Then the implementation uses DAPR state-store ETag reads/writes (`GetStateAndETagAsync` + `TrySaveStateAsync`) or an equivalent existing optimistic-concurrency helper.
   - And updates retry exactly 5 immediate read/modify/save attempts following existing index patterns such as `DaprStreamActivityTracker`.
   - And if retries are exhausted for either index scope, the operation returns `UpstreamUnavailable` with safe copy rather than claiming success.
   - And storage/concurrency failures that occur after one scope was saved are reported as non-success partial completion and logged with safe repair details; they are not represented as rollback or success.
   - And successful concurrent writes are last-write-wins with one row per policy identity after normalization.
   - And no backend-specific key scan, Redis-only command, or persistent-container assumption is introduced.

6. **Runtime automatic snapshots consult persisted policies.**
   - Given a policy exists for tenant/domain/aggregateType
   - When a command for that aggregate type is processed and event persistence reaches or exceeds the configured interval since the last snapshot
   - Then the automatic snapshot decision uses the persisted policy interval instead of only static `SnapshotOptions`.
   - And the exact precedence is persisted tenant/domain/aggregateType policy, then static tenant-domain option, then static domain option, then static default option.
   - And the bridge must account for aggregate type, not just domain; a policy for `orders/OrderAggregate` must not alter `orders/InvoiceAggregate`.
   - And the default implementation path is to extend `ISnapshotManager.ShouldCreateSnapshotAsync` with `aggregateType` and keep persisted policy lookup in one EventStore-owned runtime path, not in controllers or actor-local ad hoc reads.
   - And runtime lookup must not create new DAPR reads on every command if a reasonable cache or actor-local resolution path is needed for performance; any cache must invalidate on set/delete or use a bounded stale window no longer than 30 seconds.
   - And deleting a policy returns the runtime to static fallback behavior.

7. **Interval boundary is enforced consistently.**
   - Given a policy set request with `intervalEvents < SnapshotOptions.MinimumInterval`
   - When the request is processed
   - Then it is rejected with `RejectedValidation`.
   - Given a policy set request with an unreasonably high value
   - When the request is processed
   - Then it is rejected above a documented maximum of `100000` events.
   - And the Admin UI's historical `min=1` field does not relax backend enforcement.
   - And tests prove boundary values: `9` rejected, `10` accepted, `100000` accepted, `100001` rejected.

8. **Read paths show real policy state after writes.**
   - Given a policy is set or deleted through the Admin API
   - When `GetSnapshotPoliciesAsync(tenantId)` reads `admin:storage-snapshot-policies:{tenantId}`
   - Then it returns the persisted state with no fixture fallback.
   - And admin/global scope reads use `admin:storage-snapshot-policies:all`.
   - And result ordering is deterministic, at minimum tenant ascending, domain ascending, aggregate type ascending.
   - And empty indexes still return an empty list with the existing missing-writer log behavior.

9. **Existing clients and UI succeed without UX rewrites.**
   - Given `/snapshots` Add/Edit/Delete policy flows call existing `AdminSnapshotApiClient` methods
   - When the backend returns success
   - Then existing success toast, dialog close, and reload behavior works without a new UI story.
   - And the named flows covered by this story are: Admin.UI Add Policy, Edit Policy, Delete Policy; CLI `snapshot policies`, `snapshot set-policy`, `snapshot delete-policy`; and `AdminSnapshotApiClient.GetSnapshotPoliciesAsync`, `SetSnapshotPolicyAsync`, `DeleteSnapshotPolicyAsync`.
   - And validation/auth/not-found failures surface through those flows as typed non-success results or existing ProblemDetails behavior with operator-meaningful copy.
   - And existing tests that expected deferred policy set/delete responses are updated to expect real success and typed failure paths.
   - And manual snapshot deferred UX from DW14/DW16 is not changed by this story.
   - And CLI snapshot policy commands and API client tests are updated where their assertions assumed deferred responses.

10. **Tests and evidence prove CRUD, runtime effect, and non-regression.**
   - Admin.Server tests prove DAPR invocation, bearer-token forwarding, typed result preservation, timeout/unavailable safe messages, and no EventStore call for unrelated deferred operations.
   - EventStore.Server tests prove upstream route auth/tenant validation, set, update, delete, not-found, validation boundaries, deterministic operation ids, both index scopes, concurrency conflict retry, and rollback/no-partial-mutation behavior.
   - Snapshot runtime tests prove persisted aggregate-type policies change `ShouldCreateSnapshotAsync` behavior and deleted policies return to static fallback.
   - Admin.UI tests prove Add/Edit/Delete policy success, typed non-success, busy-state cleanup, reload, and no regression to manual snapshot deferred behavior.
   - CLI/API client tests prove `snapshot policies`, `snapshot set-policy`, and `snapshot delete-policy` handle success and typed failure responses.
   - Tier 2 or Aspire-backed evidence captures set -> read -> runtime snapshot threshold where the environment is available; if blocked, record the exact blocker.

## Tasks / Subtasks

- [x] Reconfirm baseline and route inventory before editing. (AC: 1, 2, 9)
  - [x] Read `DaprStorageCommandService.SetSnapshotPolicyAsync`, `DeleteSnapshotPolicyAsync`, `AdminStorageController.SetSnapshotPolicy`, `DeleteSnapshotPolicy`, `AdminSnapshotApiClient`, CLI snapshot commands, and current snapshots page tests.
  - [x] Confirm whether DW16 has landed. If DW16 is still only `ready-for-dev`, keep this story independent and do not rely on DW16 upstream route code.
  - [x] Confirm the current EventStore host has no implemented upstream routes for policy set/delete, or if DW16 introduced an admin storage controller shell, extend it rather than creating a duplicate route family.
  - [x] Preserve existing external Admin.Server routes: `PUT` and `DELETE api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy`.

- [x] Add or reuse policy request contracts. (AC: 2, 3, 4, 7)
  - [x] Add `SnapshotPolicySetRequest(TenantId, Domain, AggregateType, IntervalEvents)` and `SnapshotPolicyDeleteRequest(TenantId, Domain, AggregateType)` under `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/`, or document the existing equivalent used instead.
  - [x] Reuse the existing `SnapshotPolicy` record for persisted/read rows.
  - [x] Add model/serialization tests in `tests/Hexalith.EventStore.Admin.Abstractions.Tests` if a new DTO or reason-code constants are introduced.
  - [x] Do not add a second operation-result envelope; use `AdminOperationResult`.

- [x] Implement EventStore upstream policy routes. (AC: 2, 3, 4, 7)
  - [x] Add `PUT api/v1/admin/storage/snapshot-policy` and `DELETE api/v1/admin/storage/snapshot-policy` under `src/Hexalith.EventStore/Controllers` using the request DTOs above.
  - [x] Authenticate the forwarded bearer token and enforce Operator/Admin plus tenant authorization before any DAPR state mutation.
  - [x] Validate tenant/domain/aggregateType using existing identity/name validation utilities where possible; otherwise use the same structural rules enforced by `AggregateIdentity`.
  - [x] Validate interval minimum with `SnapshotOptions.MinimumInterval` and maximum with a local documented constant `MaxSnapshotPolicyIntervalEvents = 100000`.
  - [x] Return typed `AdminOperationResult` values for success, validation, unauthorized/tenant mismatch, not found, state conflict exhaustion, and unexpected safe failures.
  - [x] Add route-contract tests for set/delete success, `RejectedValidation`, missing token, bad token, wrong tenant, missing permission, missing policy delete, and state-store unavailable.

- [x] Implement ETag-safe policy index writer. (AC: 3, 4, 5, 8)
  - [x] Add a focused EventStore-side snapshot policy repository/service for policy index updates rather than embedding list mutation in the controller.
  - [x] Read and write both `admin:storage-snapshot-policies:all` and `admin:storage-snapshot-policies:{tenantId}` with ETag-based optimistic concurrency, using exactly 5 immediate read/modify/`TrySaveStateAsync` attempts.
  - [x] Ensure set/update and delete are applied consistently to both index scopes. A returned success means both scopes are consistent.
  - [x] If one scope fails after the other succeeds, return `UpstreamUnavailable`, log safe reconciliation details, and ensure the next successful mutation for the same tenant normalizes both scopes.
  - [x] Do not describe partial two-key failures as rollback. The required behavior is detect, return typed non-success, log safe reconciliation detail, and repair through normal next-mutation normalization.
  - [x] Normalize matching keys consistently: tenant and domain follow `AggregateIdentity` canonicalization; aggregateType matching is `StringComparer.OrdinalIgnoreCase`.
  - [x] Sort lists deterministically before save.
  - [x] Test stale-index repair: tenant scope missing/global present and global missing/tenant present for both set and delete.

- [x] Wire Admin.Server command methods to EventStore. (AC: 1, 9)
  - [x] Change `SetSnapshotPolicyAsync` to call `PUT api/v1/admin/storage/snapshot-policy` on EventStore app-id with `SnapshotPolicySetRequest`.
  - [x] Change `DeleteSnapshotPolicyAsync` to call `DELETE api/v1/admin/storage/snapshot-policy` on EventStore app-id with `SnapshotPolicyDeleteRequest`.
  - [x] Preserve bearer-token forwarding via `IAdminAuthContext.GetToken()`.
  - [x] Add a test that captures the outgoing `Authorization` header and proves the bearer token is forwarded.
  - [x] Sanitize service invocation failure messages. Do not leak raw exception text, provider details, connection strings, or state-store keys to operators.
  - [x] Keep `TriggerCompactionAsync` deferred. Keep `CreateSnapshotAsync` behavior aligned with DW16 state. Keep backup/export/import untouched.

- [x] Bridge persisted policies into automatic snapshot resolution. (AC: 6, 7, 10)
  - [x] Locate the single runtime decision path: `AggregateActor` Step 5b calls `snapshotManager.ShouldCreateSnapshotAsync(command.TenantId, command.Domain, persistResult.NewSequenceNumber, lastSnapshotSequence)`.
  - [x] Prefer updating `ISnapshotManager.ShouldCreateSnapshotAsync` to include `aggregateType`, then update `SnapshotManager`, `FakeSnapshotManager`, `AggregateActor`, and every NSubstitute setup/assertion atomically, similar to Story 7.1's tenantId signature change.
  - [x] If the implementation keeps `ISnapshotManager` stable instead, record the reason in the Dev Agent Record and prove there is still exactly one EventStore-owned runtime policy lookup path.
  - [x] Add an EventStore.Server-owned policy resolver backed by the snapshot policy repository or an approved cache. The resolver must check exact persisted tenant/domain/aggregateType policy first, then fall back to static tenant-domain/domain/default options.
  - [x] Avoid per-command state-store reads without a performance decision. Acceptable options include short in-memory TTL cache, actor-local cache refreshed after policy changes, or a single scoped resolver with a bounded stale window no longer than 30 seconds documented in tests.
  - [x] Ensure delete invalidates or expires the runtime policy view so fallback behavior resumes.
  - [x] Add tests for precedence and aggregate-type isolation: exact policy wins, static tenant-domain fallback, static domain fallback, static default fallback, delete returns to fallback, and one aggregate type does not affect another.

- [x] Update tests across affected surfaces. (AC: 1-10)
  - [x] Update Admin.Server `DaprStorageCommandServiceTests` and `DaprStorageServiceTests`: set/delete now invoke EventStore and preserve typed results; compaction still returns deferred with zero upstream calls.
  - [x] Update `AdminStorageControllerTests` only where expectations need real success or typed failure assertions.
  - [x] Add EventStore.Server route/service tests for set/update/delete/not-found/validation/concurrency and runtime policy application.
  - [x] Update `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/SnapshotsPageTests.cs` deferred set/delete assertions to success/typed failure, while preserving manual snapshot deferred tests.
  - [x] Update `AdminSnapshotApiClientTests` and `Admin.Cli.Tests` snapshot policy command tests for success and typed failures.
  - [x] Add a test proving rejected auth/tenant failures happen before any DAPR policy state read, not merely before write.
  - [x] Add a regression test where a policy for one aggregate type does not affect another aggregate type in the same domain.
  - [x] Add a regression test proving the runtime decision receives aggregate type, either through the updated `ISnapshotManager` signature or through the documented single resolver path.
  - [x] Add untouched-operation regression tests proving manual snapshot, compaction, backup, restore, import, and export behavior is unchanged by DW17.

- [x] Validate and capture evidence. (AC: 8, 10)
  - [x] Run focused Admin.Abstractions tests if DTOs changed.
  - [x] Run focused Admin.Server storage command/controller tests.
  - [x] Run focused EventStore.Server snapshot/policy tests. If `Hexalith.EventStore.Server.Tests` still hits the known CA2007 build blocker, record the unchanged blocker and run the narrowest compilable subset.
  - [x] Run focused Admin.UI snapshots tests and Admin.Cli snapshot policy tests.
  - [x] If possible, run Aspire with `EnableKeycloak=false`, set a policy, read it back, process enough commands to cross the new interval, and save sanitized API/state evidence under `_bmad-output/test-artifacts/post-epic-deferred-dw17-snapshot-policy-crud-backend/`.
  - [x] Update this story's Dev Agent Record, File List, Verification Status, and Change Log before moving to review.

### Review Findings

- [x] [Review][Patch] Runtime snapshot policy lookup misses admin aggregate-type policies and trusts caller extensions [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:403] — fixed by resolving aggregate type from the EventStore-owned command type catalog, registering the resolver, falling back safely to domain only when catalog lookup is unavailable, and adding runtime coverage.
- [x] [Review][Patch] DAPR access-control policy does not allow the new snapshot-policy DELETE invocation [src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml:39] — fixed by allowing `DELETE` for `eventstore-admin` service invocation.
- [x] [Review][Patch] Existing Admin.Server storage service tests still expect deferred snapshot-policy writes [tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStorageServiceTests.cs:242] — fixed by updating Admin.Server storage service tests to assert real EventStore delegation and typed invocation failure behavior.
- [x] [Review][Patch] Two-index stale repair does not reconcile missing rows across scopes [src/Hexalith.EventStore/Services/DaprSnapshotPolicyRepository.cs:56] — fixed by dual-reading global and tenant indexes, preserving existing creation timestamps across either scope, and merging repair policies during bounded ETag mutations.
- [x] [Review][Patch] Invalid policy requests can be authorized before validation and return unauthorized instead of RejectedValidation [src/Hexalith.EventStore/Controllers/AdminStorageCommandController.cs:66] — fixed by adding side-effect-free request validation before tenant/RBAC authorization for set and delete routes, with controller regression coverage.
- [x] [Review][Patch] Required AC10 route, concurrency, and runtime-threshold coverage is incomplete [tests/Hexalith.EventStore.Server.Tests/Controllers/DaprSnapshotPolicyRepositoryTests.cs:17] — fixed by adding repository stale-index repair/runtime aggregate-type matching tests, controller validation-order tests, resolver catalog tests, and actor snapshot-policy aggregate-type coverage.

## Dev Notes

### Current State To Preserve

- `DaprStorageCommandService.SetSnapshotPolicyAsync` and `DeleteSnapshotPolicyAsync` currently return `AdminOperationResult(false, "deferred-snapshot-policy-set/delete", ..., "Deferred")` without calling EventStore. DW17 replaces only these policy methods.
- `DaprStorageQueryService.GetSnapshotPoliciesAsync` already reads `admin:storage-snapshot-policies:{tenantId ?? "all"}` and returns `[]` when the index is missing. This story must make those keys real, not replace the query surface.
- `AdminStorageController` already exposes external Admin.Server routes for policy set/delete, requires the Operator policy, applies `AdminTenantAuthorizationFilter`, and maps typed business outcomes to HTTP 200. Preserve the external contract.
- `SnapshotPolicy` already exists in `Admin.Abstractions/Models/Storage/SnapshotPolicy.cs` with `TenantId`, `Domain`, `AggregateType`, `IntervalEvents`, and `CreatedAtUtc`. Reuse it for read/persisted rows.
- `Snapshots.razor`, `AdminSnapshotApiClient`, and CLI snapshot commands already call the correct Admin.Server endpoints. Existing UI behavior should flip to success when `AdminOperationResult.Success == true`; no broad UX rewrite is expected.
- DW14 made manual snapshot, compaction, backup, validation, and export honest-deferred. DW17 should remove deferred results only for snapshot policy set/delete.

### Runtime Snapshot Policy Bridge

Story 7.1 implemented automatic snapshot intervals through static `SnapshotOptions`:

- `SnapshotOptions.DefaultInterval = 100`.
- `SnapshotOptions.MinimumInterval = 10`.
- `SnapshotOptions.DomainIntervals` and `TenantDomainIntervals`.
- `SnapshotManager.ShouldCreateSnapshotAsync(tenantId, domain, currentSequence, lastSnapshotSequence)` resolves static tenant-domain/domain/default options.
- `AggregateActor` Step 5b calls `ShouldCreateSnapshotAsync` after event persistence and before the actor `SaveStateAsync()` batch.

DW17 must add a persisted policy layer keyed by `(tenantId, domain, aggregateType)`. The preferred implementation is to make the runtime decision signature explicit:

```csharp
ShouldCreateSnapshotAsync(tenantId, domain, aggregateType, currentSequence, lastSnapshotSequence)
```

Update `ISnapshotManager`, `SnapshotManager`, `FakeSnapshotManager`, `AggregateActor`, and every NSubstitute setup/assertion atomically. This avoids an invisible domain-only bridge and keeps the aggregate-type requirement visible at compile time.

If the dev agent keeps `ISnapshotManager` stable, the Dev Agent Record must explain why that shape is safer in this codebase and tests must prove there is still exactly one EventStore-owned runtime lookup path. In either shape, the runtime lookup must live in `SnapshotManager` or an injected EventStore-owned resolver used by `SnapshotManager`; it must not be duplicated in controllers or added as actor-local ad hoc DAPR reads.

Do not make a domain-only persisted policy; that would violate the approved DW17 scope.

### Policy Key And Index Shape

Use active-list indexes:

```text
admin:storage-snapshot-policies:all
admin:storage-snapshot-policies:{tenantId}
```

Policy identity is the canonical tuple:

```text
{tenantId}|{domain}|{aggregateType}
```

For matching, tenant/domain use aggregate-identity canonicalization and aggregateType is compared ordinal-ignore-case. Do not add wildcard, tenant-only, domain-only, or disabled-policy semantics in this story.

Do not store policies under aggregate snapshot keys. Aggregate snapshots remain actor state at:

```text
{tenantId}:{domain}:{aggregateId}:snapshot
```

Policy indexes are admin/runtime configuration. They are not the event stream source of truth and must not write, delete, rewrite, compact, or archive event keys.

### Concurrency And DAPR State

Existing admin/storage indexes use DAPR state ETags for optimistic concurrency. Follow the `DaprStreamActivityTracker` pattern: read value + ETag, compute updated list, `TrySaveStateAsync`, retry a small bounded number of times, then return/log safe failure.

DAPR state management supports optimistic concurrency through ETags and per-operation metadata such as TTL. Use those APIs where needed; do not introduce custom retry loops around infrastructure beyond the existing bounded ETag retry pattern for index mutation.

External references checked during story creation:

- [Dapr state management overview](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/) - state-store building block and ETag/concurrency model.
- [Dapr state API reference](https://docs.dapr.io/reference/api/state_api/) - state API behavior and metadata reference.

### Validation Rules

- Minimum accepted interval: `SnapshotOptions.MinimumInterval` (`10` today).
- Maximum accepted interval: `100000` events for this story.
- Reject blank tenant/domain/aggregateType.
- Reject structurally invalid tenant/domain using the same rules as aggregate identity and admin route validation.
- Treat aggregate type as an identifier, not a display name. Do not log it as trusted identity until it has passed validation.
- Return typed `RejectedValidation` results for invalid user input; do not throw unhandled exceptions from controllers.

### Security And Safety Guardrails

- EventStore upstream policy write routes must not be `[AllowAnonymous]`.
- Authorization and tenant check happen before validation details that could leak tenant existence, before state reads, and before state writes. Tests must prove rejected auth/tenant requests do not touch DAPR state at all.
- Use `sub`/role/tenant claims through existing auth helpers; do not switch to user-controllable `name`.
- Logs, operation results, UI toasts, and evidence must not include event payloads, snapshot state JSON, provider exception text, raw state-store keys, secrets, key aliases, or connection strings.
- Do not add new packages or infrastructure dependencies.
- Do not add Redis-specific commands or backend-specific scans.
- Do not change DAPR access-control/AppHost files unless a narrow runtime failure proves the invocation path requires it.

### Likely Files To Modify

- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/*` - policy request DTO or constants if needed.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs` - replace policy deferred returns with EventStore invocation.
- `src/Hexalith.EventStore/Controllers/*` - add EventStore-owned upstream snapshot policy routes.
- `src/Hexalith.EventStore/Services/*` or nearby EventStore-owned folder - policy index writer/resolver if a new helper is warranted.
- `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs` - only if adding aggregateType to the runtime signature.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` - persisted policy lookup/fallback or signature update.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` - pass aggregate type or invoke the policy resolver.
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs` - keep test double aligned if `ISnapshotManager` changes.
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/**`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStorageCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStorageControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/**`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/SnapshotsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminSnapshotApiClientTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/*`
- `_bmad-output/test-artifacts/post-epic-deferred-dw17-snapshot-policy-crud-backend/`

### Testing Notes

Recommended first-pass commands:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests --configuration Release --filter "FullyQualifiedName~SnapshotPolicy" -m:1
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprStorageCommandService|FullyQualifiedName~AdminStorageController" -m:1
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~SnapshotsPage|FullyQualifiedName~AdminSnapshotApiClient" -m:1
dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests --configuration Release --filter "FullyQualifiedName~Snapshot" -m:1
```

For EventStore.Server tests, prefer focused filters around the new policy writer/resolver and snapshot manager/aggregate actor behavior. The repository records pre-existing `Hexalith.EventStore.Server.Tests` build issues from CA2007 warnings treated as errors; record any unchanged blocker instead of marking it as DW17-caused.

Aspire manual validation, when available:

```powershell
$env:EnableKeycloak = 'false'
aspire run --project .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Use the dev JWT shape from `AGENTS.md`: issuer `hexalith-dev`, audience `hexalith-eventstore`, `tenants` JSON array, and permissions/roles sufficient for Operator/Admin storage actions.

### Failure-Mode Test Matrix

| Failure mode | Expected result |
| --- | --- |
| Missing/invalid bearer token | Request rejected before policy state read/write; no index mutation. |
| Bad issuer/audience, no tenant claim, tenant mismatch, or insufficient Operator/Admin permission | Request rejected before policy state read/write; no index mutation. |
| Blank or structurally invalid tenant/domain/aggregateType after auth passes | Typed `RejectedValidation`; no unstructured 500 and no index mutation. |
| `intervalEvents` less than 10 or greater than 100000 | Typed `RejectedValidation`; no index mutation. |
| PUT same policy twice | Success both times; one row in each scope; `CreatedAtUtc` preserved unless implementation records otherwise. |
| PUT same key with new interval | Success; row updated in both scopes; no duplicate. |
| DELETE existing policy | Success; row removed from both scopes; runtime falls back to static interval. |
| DELETE missing policy with no stale index rows | Typed `NotFound`; no mutation. |
| DELETE missing policy with stale row in one scope | Repair both scopes to deleted state and return success because an active row existed in at least one scope. |
| Concurrent same-key updates | Last successful write wins, bounded ETag retry, no duplicate active policies. |
| One scope update conflict/exhaustion before any save | Return `UpstreamUnavailable`; no success claim; safe copy and reconciliation log. |
| One scope saved and second scope later exhausts conflicts or fails | Return `UpstreamUnavailable`; no success claim; safe partial-completion/reconciliation log; next successful same-tenant mutation normalizes both scopes. |
| Policy for one aggregate type | Does not change snapshot cadence for another aggregate type in same tenant/domain. |
| Runtime bridge omits aggregate type | Test failure; implementation must either update `ISnapshotManager` signature or document and prove a single alternate resolver path. |
| Cached runtime policy after delete | Cache invalidates immediately or expires within documented <=30s window, then static fallback resumes. |
| DAPR state unavailable | Safe typed failure; no raw provider details or connection strings in operation result. |

## Previous Story Intelligence

- DW14 intentionally made deferred backend operations honest in the UI. DW17 must remove deferred behavior only from snapshot policy set/delete after real backend support exists; manual snapshot, compaction, backup, validation, restore, import, and export remain separately owned.
- DW16 locks the pattern that Admin.Server is a facade and EventStore owns write-side operations. Reuse that ownership boundary: Admin.Server forwards JWT and typed results; EventStore performs state/index/runtime behavior.
- Story 7.1 already implemented static snapshot interval resolution and warned that changing `ISnapshotManager.ShouldCreateSnapshotAsync` is dangerous because string-parameter swaps and NSubstitute setup mismatches can silently make tests meaningless. If the runtime signature changes, update all call sites and mock setups atomically.
- Story 16.2 built the UI/client/CLI surfaces for snapshot policies. The UI already succeeds when `AdminOperationResult.Success == true`; avoid a UI rewrite unless tests prove the existing success path is broken.
- `DaprStreamActivityTracker` provides the closest local ETag retry pattern for admin/storage indexes. Use it as the model for portable index mutation.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-admin-deferred-quick-wins.md#4.2-DW17--Snapshot-policy-CRUD-backend`] - approved DW17 scope, state key, runtime wiring, interval validation, and tests.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw14-admin-deferred-operations-ux-policy.md`] - current deferred UX baseline and no-fake-success rules.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw16-manual-snapshot-creation-backend.md`] - prior Group A backend handoff, EventStore ownership boundary, safe operation-id and typed-result patterns.
- [Source: `_bmad-output/implementation-artifacts/16-2-snapshot-management-and-auto-snapshot-policies.md`] - existing UI/client/CLI surfaces and snapshot policy behavior.
- [Source: `_bmad-output/implementation-artifacts/7-1-configurable-aggregate-snapshots.md`] - existing snapshot manager, aggregate actor snapshot flow, static interval rules, and warning about `ISnapshotManager` signature changes.
- [Source: `_bmad-output/planning-artifacts/prd.md#FR76`] - admin storage management includes snapshot policies and snapshot creation.
- [Source: `_bmad-output/planning-artifacts/architecture.md#D1-Event-Storage-Pattern`] - DAPR actor state atomicity and write-once event keys.
- [Source: `_bmad-output/planning-artifacts/architecture.md#DAPR-State-Store-Keys-D1-D2`] - state key conventions including snapshot keys.
- [Source: `_bmad-output/planning-artifacts/architecture.md#ADR-P4-Admin-Tooling--Three-Interface-Architecture-Over-Single-DAPR-API`] - Admin UI/API/CLI/MCP boundary.
- [Source: `_bmad-output/project-context.md`] - .NET 10, DAPR, Aspire, testing, logging, and no-payload-leak guardrails.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs`] - current deferred policy methods and reusable DAPR invocation helper.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageQueryService.cs`] - existing `admin:storage-snapshot-policies:*` read path.
- [Source: `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs`] - external Admin.Server policy routes and typed result mapping.
- [Source: `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/SnapshotPolicy.cs`] - existing policy DTO.
- [Source: `src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs`] - minimum/default interval and static fallback options.
- [Source: `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`] - current automatic snapshot interval decision.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`] - command-time snapshot decision call site.
- [Source: `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs`] - local ETag retry pattern for DAPR state indexes.
- [Source: Dapr official docs - state management overview](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/) - state-store/ETag concurrency reference.
- [Source: Dapr official docs - state API reference](https://docs.dapr.io/reference/api/state_api/) - state API behavior and metadata reference.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `aspire run --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --detach --non-interactive --format Json` initially failed during AppHost build with an MSBuild named-pipe timeout; after Release/Debug builds, detached `--no-build` Aspire runs succeeded.
- First live API smoke used stale Debug binaries and still returned `ErrorCode=Deferred`; stopped Aspire, rebuilt Debug, restarted, then policy set/read/delete succeeded.
- Sanitized live evidence: `_bmad-output/test-artifacts/post-epic-deferred-dw17-snapshot-policy-crud-backend/aspire-smoke-evidence.md`.

### Completion Notes List

- Story context engine analysis completed on 2026-05-21. Comprehensive developer guide created for snapshot policy CRUD backend implementation.
- Party-mode review fixes applied on 2026-05-21: route/DTO contract, policy identity, runtime precedence, consistency model, ETag retry semantics, delete/stale-index repair behavior, cache invalidation bound, auth matrix, and concrete test gates added.
- Advanced elicitation refinements applied on 2026-05-21: runtime bridge preference, two-key consistency semantics, auth-before-state-read proof, and partial-failure repair wording tightened.
- Implemented EventStore-owned snapshot policy set/delete routes using `SnapshotPolicySetRequest` and `SnapshotPolicyDeleteRequest`, with operator/tenant checks before DAPR policy state access.
- Added `DaprSnapshotPolicyRepository` to persist policy rows into both global and tenant-scoped active-list indexes using 5-attempt ETag retries, deterministic operation ids, deterministic ordering, validation bounds, typed not-found/upstream-unavailable outcomes, and a 30-second runtime lookup cache invalidated on mutation.
- Wired Admin.Server policy set/delete methods to EventStore service invocation with bearer-token forwarding; compaction and other unrelated deferred operations remain unchanged.
- Extended the snapshot decision path to pass aggregate type into `ISnapshotManager`, resolve exact persisted policies before static fallback options, and keep aggregate-type isolation visible in tests.
- Updated Admin UI/API client and CLI snapshot policy tests so policy writes expect real success or typed non-success outcomes instead of `Deferred`.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw17-snapshot-policy-crud-backend.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/post-epic-deferred-dw17-snapshot-policy-crud-backend/aspire-smoke-evidence.md`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/SnapshotPolicyDeleteRequest.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/SnapshotPolicySetRequest.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Snapshot/SnapshotDeletePolicyCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Snapshot/SnapshotSetPolicyCommand.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs`
- `src/Hexalith.EventStore.Server/Events/ISnapshotPolicyResolver.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs`
- `src/Hexalith.EventStore/Controllers/AdminStorageCommandController.cs`
- `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore/Services/DaprSnapshotPolicyRepository.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Common/SerializationRoundTripTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotDeletePolicyCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotSetPolicyCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStorageCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/SnapshotsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminSnapshotApiClientTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStorageCommandControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/DaprSnapshotPolicyRepositoryTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs`

### Verification Status

- `dotnet test tests\Hexalith.EventStore.Admin.Abstractions.Tests --configuration Release --filter "FullyQualifiedName~SnapshotPolicy|FullyQualifiedName~SerializationRoundTrip" -m:1 --no-restore` passed: 33 tests.
- `dotnet test tests\Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprStorageCommandService|FullyQualifiedName~AdminStorageController" -m:1 --no-restore` passed: 17 tests.
- `dotnet test tests\Hexalith.EventStore.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprSnapshotPolicyRepository|FullyQualifiedName~SnapshotManager|FullyQualifiedName~AggregateActorDomainResult|FullyQualifiedName~AdminStorageCommandController" -m:1 --no-restore` passed: 86 tests.
- `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~SnapshotsPage|FullyQualifiedName~AdminSnapshotApiClient" -m:1 --no-restore` passed: 30 tests.
- `dotnet test tests\Hexalith.EventStore.Admin.Cli.Tests --configuration Release --filter "FullyQualifiedName~Snapshot" -m:1 --no-restore` passed: 19 tests.
- `dotnet build .\Hexalith.EventStore.slnx --configuration Release -m:1 --no-restore` passed with 0 warnings and 0 errors.
- `dotnet build .\Hexalith.EventStore.slnx -m:1 --no-restore` passed with 0 warnings and 0 errors after stopping Aspire.
- Aspire live smoke with `EnableKeycloak=false` passed for Admin.Server policy set -> tenant read -> delete -> tenant read; runtime resource state was healthy and the apphost was stopped after capture.

### Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-21 | 2.0 | Implemented DW17 snapshot policy CRUD backend, EventStore-owned DAPR policy indexes, runtime aggregate-type policy resolution, Admin.Server forwarding, UI/CLI/client test updates, and live Aspire set/read/delete evidence. | Codex |
| 2026-05-21 | 1.2 | Applied advanced elicitation refinements: preferred aggregate-type runtime signature, single runtime lookup path, explicit two-key non-atomic consistency model, auth-before-state-read test gate, and partial-failure repair semantics. | Codex |
| 2026-05-21 | 1.1 | Applied Party Mode review fixes: exact route/DTO contracts, policy identity and precedence, two-index consistency/failure model, 5-attempt ETag conflict rule, stale-index repair, cache invalidation bound, auth matrix, and concrete test gates. | Codex |
| 2026-05-21 | 1.0 | Created ready-for-dev DW17 story with EventStore-owned snapshot policy set/delete, DAPR state indexes, runtime snapshot-policy resolution, validation boundaries, ETag concurrency, tests, and evidence guidance. | Codex |
