# Post-Epic-10 R10-A3: Hub Group Authorization Decision

Status: done

<!-- Source: epic-10-retro-2026-05-01.md R10-A3 -->
<!-- Source: sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md Proposal 3 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer hardening tenant-scoped real-time notifications**,
I want `ProjectionChangedHub.JoinGroup` to make an explicit tenant-authorization decision before adding a connection to a `{projectionType}:{tenantId}` group,
So that tenant-scoped SignalR subscriptions are either claim-enforced or deliberately accepted as a documented signal-only risk.

## Story Context

Epic 10 shipped the SignalR real-time notification layer: signal-only projection change broadcasts, optional Redis backplane support, and automatic client group rejoin. Story 10.1 deliberately used `{projectionType}:{tenantId}` groups and data-free `ProjectionChanged(projectionType, tenantId)` payloads, but it also recorded that any connected client could call `JoinGroup` for any tenant group. The signal-only model reduces data leakage, but group names still expose tenant/projection activity and can trigger unauthorized refresh behavior.

The Epic 10 retrospective records this as R10-A3: hub group join lacks a tenant-aware authorization decision. This story closes that security posture gap by requiring either enforcement through existing tenant claims or an explicit accepted-risk decision with revisit criteria. Current HEAD at story creation: `4a56c7c`.

## Acceptance Criteria

1. **SignalR authentication posture is inspected and recorded.** Document whether `/hubs/projection-changes` is reachable anonymously today, whether `Context.User` contains transformed `eventstore:tenant` claims during hub method invocation, how the .NET client supplies bearer tokens through `EventStoreSignalRClientOptions.AccessTokenProvider`, and whether the observed hosted behavior came from negotiate, WebSocket/SSE connection, or direct hub invocation evidence.

2. **The story records one explicit decision before implementation proceeds.** Add a short decision record in the Dev Agent Record naming exactly one chosen path: `enforce-tenant-claims` or `accepted-signal-only-risk`, the evidence inspected, the person/role accepting the decision, and the revisit trigger if risk is accepted. Do not leave this as an implicit implementation preference, and do not begin source changes beyond baseline inspection until the decision is recorded. Party-mode recommendation is `enforce-tenant-claims`; choosing `accepted-signal-only-risk` requires explicit product/security risk acceptance in the Dev Agent Record.

3. **If `enforce-tenant-claims` is chosen, unauthorized joins are rejected before group mutation.** `JoinGroup(projectionType, tenantId)` must reject missing, blank, unauthenticated, or non-matching tenant authorization claims before updating `_connectionGroups` or calling `Groups.AddToGroupAsync`. The comparison must remain case-sensitive and aligned with `ClaimsTenantValidator`; do not parse tenant claims ad hoc in the hub unless the behavior delegates to or exactly mirrors `ITenantValidator`. Pass `Context.ConnectionAborted` to the validator when available so disconnects do not leave avoidable authorization work running.

4. **If `enforce-tenant-claims` is chosen, global-admin semantics are deliberate.** Default to reusing `ClaimsTenantValidator` / `ITenantValidator` semantics so global administrators can join any tenant group without an `eventstore:tenant` claim for that tenant. If SignalR group subscription intentionally excludes the global-admin bypass, document the reason as a decision exception and cover it with tests. The chosen behavior must have tests.

5. **If `enforce-tenant-claims` is chosen, hub access requires authenticated users.** Apply `[Authorize]` to `ProjectionChangedHub` or equivalent hub-level/method-level policy, and ensure hosted endpoint tests prove anonymous negotiate or anonymous `JoinGroup` no longer succeeds when SignalR is enabled. Direct hub unit tests are not sufficient to prove middleware authorization.

6. **If `accepted-signal-only-risk` is chosen, the risk is explicit and bounded.** Record why signal-only payloads are sufficient for now, what still leaks or can be influenced by cross-tenant subscription, who may observe tenant/projection activity, how long the acceptance lasts, what mitigations exist (`MaxGroupsPerConnection`, no projection data, polling/query authorization), and which production milestone or security review must revisit the decision.

7. **Existing group safety behavior is preserved.** Colon-reserved validation, null/whitespace validation, max-groups-per-connection enforcement, rollback-on-`AddToGroupAsync` failure, `LeaveGroup`, and disconnect cleanup continue to behave as before.

8. **Client and reconnect behavior stay compatible.** `EventStoreSignalRClient.SubscribeAsync`, `StartAsync`, and reconnect rejoin continue to call `JoinGroup` with the same `{projectionType, tenantId}` arguments. If enforcement is chosen, token supply failures must surface as subscription/rejoin failures without silently marking the group authorized. Rejoin must use the current authenticated hub principal and must re-run tenant authorization for every restored group intent; stored client-side intent is not proof of current authorization after reconnect.

9. **Tests cover positive and negative authorization paths.** Add focused tests for allowed tenant, wrong tenant, unauthenticated or no-tenant-claims denial, global-admin behavior for the chosen rule, colon validation still winning for malformed group parts, no `Groups.AddToGroupAsync` call on denied authorization, no `_connectionGroups` entry and no quota consumption after denial, same-connection recovery by joining an allowed tenant after denial, and reconnect/rejoin behavior if client helper changes are required.

10. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and both `last_updated` fields in `sprint-status.yaml` name R10-A3 and the chosen decision. At code-review signoff, both become `done`.

## Scope Boundaries

- Do not change the SignalR group format. It remains `{projectionType}:{tenantId}`.
- Do not put projection data, ETags, aggregate IDs, command statuses, or authorization details into SignalR payloads.
- Do not redesign query/command authorization, JWT issuance, Keycloak realm mapping, or the eventstore claim transformation model.
- Do not solve Redis backplane runtime proof here; that is `post-epic-10-r10a2-redis-backplane-runtime-proof`.
- Do not solve reconnect documentation here; that is `post-epic-10-r10a5-client-reconnect-guidance`.
- Do not weaken fail-open broadcast behavior. Broadcast failures must still not break ETag regeneration or command processing.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| Hub group authorization target | `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs` | Add or decide against tenant-aware join authorization while preserving group validation/quota behavior |
| Hub endpoint mapping | `src/Hexalith.EventStore/Program.cs` | Confirm authentication/authorization middleware remains before `MapHub` and endpoint behavior is tested |
| Claims transformation | `src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs` | Reuse normalized `eventstore:tenant` claim semantics from JWT `tenants`, `tenant_id`, or `tid` |
| Tenant validation | `src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs` | Reuse case-sensitive tenant comparison and global-admin bypass semantics if enforcement is chosen |
| Tenant validator abstraction | `src/Hexalith.EventStore/Authorization/ITenantValidator.cs` | Prefer existing abstraction over duplicating tenant-claim logic in the hub |
| SignalR client token hook | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs` | Existing `AccessTokenProvider` is the intended bearer-token supply mechanism for authenticated hubs |
| SignalR client group calls | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` | Preserve `JoinGroup`/`LeaveGroup` argument shape and reconnect rejoin behavior |
| Hub unit tests | `tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs` | Extend focused hub tests for allow/deny paths and quota preservation |
| Hub endpoint tests | `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubEndpointTests.cs` | Add anonymous/authenticated negotiate or endpoint behavior coverage as appropriate |
| Claims tests | `tests/Hexalith.EventStore.Server.Tests/Authentication/EventStoreClaimsTransformationTests.cs` | Reference existing claim normalization behavior; add only if new claim handling is introduced |
| Tenant validator tests | `tests/Hexalith.EventStore.Server.Tests/Authorization/ClaimsTenantValidatorTests.cs` | Use as behavioral precedent for no-claims, wrong-tenant, multiple-tenant, case-sensitive, and global-admin behavior |

## Tasks / Subtasks

- [x] Task 0: Baseline and decision framing (AC: #1, #2, #6)
  - [x] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [x] 0.2 Inspect current hub negotiate and method invocation behavior with SignalR enabled.
  - [x] 0.3 Confirm whether authenticated hub connections receive transformed `eventstore:tenant` claims.
  - [x] 0.4 Add a Dev Agent Record decision block with the chosen path and rationale before source/test edits. Party-mode recommends `enforce-tenant-claims`; `accepted-signal-only-risk` requires explicit product/security risk acceptance.
  - [x] 0.5 If the decision is accepted risk, record a dated revisit trigger tied to a production milestone or security review, not an open-ended "later".

- [x] Task 1: Implement or document the authorization decision (AC: #3, #4, #5, #6, #7)
  - [x] 1.1 If enforcing, add `[Authorize]` or equivalent policy to the hub or `JoinGroup`.
  - [x] 1.2 If enforcing, preserve the required failure order: null/whitespace and colon validation -> authenticated user required -> tenant authorization -> quota check -> `_connectionGroups` mutation -> `Groups.AddToGroupAsync` -> rollback on add failure.
  - [x] 1.3 If enforcing, reuse `ITenantValidator`/`ClaimsTenantValidator` semantics or document any intentional difference.
  - [x] 1.4 If accepting risk, add the accepted-risk record with explicit mitigation and revisit trigger, without code changes that pretend to enforce authorization.
  - [x] 1.5 Preserve current exception shape where practical; use `HubException` for caller-visible tenant-denial join failures unless ASP.NET Core authorization naturally returns the failure. Do not silently no-op unauthorized joins.
  - [x] 1.6 If enforcing, pass the hub connection cancellation token to tenant validation and keep authorization failures out of the group-name tracking path.

- [x] Task 2: Preserve group and client behavior (AC: #7, #8)
  - [x] 2.1 Verify colon validation still rejects `projectionType` or `tenantId` containing `:`.
  - [x] 2.2 Verify denied authorization does not create a `_connectionGroups` entry, call `Groups.AddToGroupAsync`, or consume per-connection group quota.
  - [x] 2.3 Verify failed `Groups.AddToGroupAsync` rollback still works after authorization changes.
  - [x] 2.4 Verify `LeaveGroup` and disconnect cleanup still remove tracked groups.
  - [x] 2.5 If the client helper needs token guidance or behavior change, keep it narrowly scoped to token supply and rejoin error visibility. If the client helper does not change, explicitly record that reconnect behavior remains unchanged and covered by existing behavior.
  - [x] 2.6 Verify the same connection can still join an allowed tenant after one or more denied joins.

- [x] Task 3: Add focused test coverage (AC: #3-#9)
  - [x] 3.1 Add allowed-tenant `JoinGroup` coverage.
  - [x] 3.2 Add wrong-tenant and no-tenant-claims denial coverage that asserts no group add, no group tracking, and no quota consumption.
  - [x] 3.3 Add global-admin coverage for the chosen rule, including the case-sensitive tenant comparison baseline for non-admin users.
  - [x] 3.4 Add anonymous negotiate or anonymous hub-method coverage for the chosen hub authorization posture in hosted endpoint tests.
  - [x] 3.5 Add colon-validation precedence coverage proving malformed group parts fail before auth/quota side effects.
  - [x] 3.6 Add a same-connection denial-then-allowed test to prove authorization denial did not poison quota or tracking state.
  - [x] 3.7 Re-run existing SignalR hub/client tests touched by the change.

- [x] Task 4: Verification gates (AC: #1-#10)
  - [x] 4.1 Run `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~SignalR|FullyQualifiedName~ClaimsTenantValidator|FullyQualifiedName~EventStoreClaimsTransformation"`.
  - [x] 4.2 If client helper code changed, run `dotnet test tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj --no-restore`.
  - [x] 4.3 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.
  - [x] 4.4 If Aspire/runtime proof is attempted, capture exact commands and blockers in the Dev Agent Record; runtime proof is helpful but not required unless the implementation depends on hosted authentication behavior.

- [x] Task 5: Story bookkeeping (AC: #10)
  - [x] 5.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [x] 5.2 Move this story and only this story from `ready-for-dev` to `review` at dev handoff.
  - [x] 5.3 Leave R10-A2/R10-A5/R10-A6/R10-A7/R10-A8 status rows unchanged.

## Dev Notes

### Architecture Guardrails

- SignalR remains an invalidation channel. Query endpoints remain authoritative for projection data and authorization-sensitive data access.
- Tenant group membership is not a substitute for query authorization. Even after enforcement, query and command paths must keep validating tenant claims independently.
- If `[Authorize]` is added to the hub, client token flow must use the existing `AccessTokenProvider` hook. Do not add a second custom token parameter to `JoinGroup`.
- Tenant matching should stay ordinal/case-sensitive to match `ClaimsTenantValidator`; do not trim or normalize tenant IDs unless the existing validator changes too.
- A denied join must not leave stale entries in `_connectionGroups`, otherwise a malicious client can consume quota without joining real groups.
- Required `JoinGroup` ordering for the enforcement path is: input validation, authentication, tenant authorization, quota check, group tracking, SignalR group add, rollback only after a successful-path add failure. This ordering keeps malformed input diagnostics stable and prevents authorization failures from touching group state.
- Accepted-risk is an architecture/product risk decision, not a shortcut implementation path. If chosen, do not add partial enforcement code that implies stronger isolation than the recorded risk actually provides.
- Do not treat client-side reconnect intent as authorization evidence. The hub must validate the current principal on every `JoinGroup` call, including replayed joins after reconnect.
- If `ITenantValidator.ValidateAsync` is used, prefer `Context.ConnectionAborted` over `CancellationToken.None` so disconnecting clients do not keep tenant authorization work alive unnecessarily.

### Current-Code Intelligence

- `ProjectionChangedHub.JoinGroup` currently validates null/whitespace inputs, rejects colons, tracks groups per connection, enforces `MaxGroupsPerConnection`, rolls back tracking when `Groups.AddToGroupAsync` fails, and logs EventId 1080.
- `ProjectionChangedHub.LeaveGroup` derives the same group name and removes the connection from SignalR groups plus local tracking.
- `ClaimsTenantValidator` already implements the desired tenant claim behavior: global-admin bypass, `eventstore:tenant` claim lookup, no-claim denial, and ordinal tenant comparison.
- `EventStoreClaimsTransformation` maps JWT `tenants`, `tenant_id`, and `tid` into normalized `eventstore:tenant` claims and adds `ClaimTypes.NameIdentifier` from `sub`.
- `EventStoreSignalRClientOptions.AccessTokenProvider` already exists for authenticated SignalR hubs; use it before adding any new client option.
- Current code has no constructor dependency on `ITenantValidator`. If enforcement is selected, adding that dependency is expected; update hub unit test factory setup instead of duplicating tenant-claim parsing inside `ProjectionChangedHub`.

### Previous Story Intelligence

- Story 10.1 chose signal-only broadcasts to avoid a second projection-data delivery surface. Keep that model.
- Story 10.1 explicitly recorded the no-authorization hub gap as acceptable only for that story, not as a permanent production decision.
- Story 10.3 established automatic rejoin after reconnect. If this story enforces auth, reconnect failures should be observable and should not pretend the subscription was restored.
- R10-A2 is a separate runtime proof story. Do not block this security decision on Redis backplane evidence.

### Testing Standards

- Keep focused hub authorization tests in `tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs` unless hosted middleware behavior is required.
- Hosted endpoint authentication behavior belongs in `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubEndpointTests.cs` or a sibling factory; use it for anonymous/authenticated hub boundary proof.
- Direct hub tests should pin authorization ordering and group bookkeeping side effects: denied joins must not call `Groups.AddToGroupAsync`, must not consume quota, and must not require rollback cleanup.
- Use existing Shouldly/NSubstitute style and do not introduce a new test framework.
- Do not run solution-level `dotnet test`; run affected projects individually.

### Latest Technical Information

- Microsoft Learn for ASP.NET Core SignalR states that groups are an application-managed routing mechanism, not a security feature; access should be protected with ASP.NET Core authentication and authorization, and revoked access requires explicit removal from groups.
- Microsoft Learn for ASP.NET Core SignalR authentication documents that browsers send bearer tokens for WebSockets/SSE through the SignalR access-token mechanism because browser APIs cannot set arbitrary headers for those transports.
- Microsoft Learn documents `[Authorize]` on hubs or hub methods and custom authorization handlers for hub method authorization. Use these built-in mechanisms before inventing custom token parsing.

### Project Structure Notes

- SignalR server-side code lives in `src/Hexalith.EventStore/SignalRHub/`.
- SignalR client helper code lives in `src/Hexalith.EventStore.SignalR/`.
- Authentication and tenant authorization primitives live in `src/Hexalith.EventStore/Authentication/` and `src/Hexalith.EventStore/Authorization/`.
- Expected BMAD edits during dev are this story file, focused source/test changes for the chosen decision, and `sprint-status.yaml` bookkeeping.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md#Proposal-3`] - R10-A3 acceptance criteria and rationale.
- [Source: `_bmad-output/implementation-artifacts/epic-10-retro-2026-05-01.md`] - R10-A3 action item and retrospective security gap.
- [Source: `_bmad-output/implementation-artifacts/10-1-signalr-hub-and-projection-change-broadcasting.md`] - Signal-only model and original no-authorization caveat.
- [Source: `_bmad-output/implementation-artifacts/10-3-automatic-signalr-group-rejoining-on-reconnection.md`] - Reconnect/rejoin expectations.
- [Source: `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`] - Current group join, validation, quota, and cleanup behavior.
- [Source: `src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs`] - Current tenant-claim validation behavior.
- [Source: `src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs`] - JWT-to-eventstore claim normalization.
- [Source: `src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs`] - Existing SignalR bearer token supply hook.
- [Source: `tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs`] - Existing hub behavior tests.
- [Source: Microsoft Learn, `https://learn.microsoft.com/aspnet/core/signalr/groups?view=aspnetcore-10.0`] - Groups are application-managed and not a security feature.
- [Source: Microsoft Learn, `https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0`] - ASP.NET Core SignalR authentication, access tokens, and hub authorization.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Decision Record

Decision: `enforce-tenant-claims`

Decision owner/accepting role: Dev-story implementer acting on the party-mode architecture/security recommendation for tenant-scoped real-time notifications.

Evidence inspected before source/test edits:

- Current HEAD at baseline: `5753f796035019a7b3883e1bd69018e5ea5759f9`.
- Story status was `ready-for-dev` in `sprint-status.yaml` before this dev-story run moved it to `in-progress`.
- Aspire baseline was started with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; the Aspire resource list showed `eventstore`, `eventstore-admin`, `sample`, `sample-blazor-ui`, `tenants`, `statestore`, and `pubsub` running and healthy.
- Runtime anonymous negotiate evidence: `POST http://localhost:8080/hubs/projection-changes/negotiate?negotiateVersion=1` returned `200 OK` with SignalR negotiate JSON before authorization changes, proving the hosted hub negotiate endpoint was anonymously reachable when SignalR was enabled.
- Source/method invocation evidence: `ProjectionChangedHub` had no `[Authorize]`, `Program.cs` mapped the hub after `UseAuthentication()` and `UseAuthorization()`, and direct hub tests showed `JoinGroup("order-list", "acme")` called `Groups.AddToGroupAsync` without tenant authorization.
- Claim transformation evidence: `EventStoreClaimsTransformation` maps JWT `tenants`, `tenant_id`, and `tid` into normalized `eventstore:tenant` claims, and `ConfigureJwtBearerOptions` preserves inbound JWT claim names.
- Validator evidence: `ClaimsTenantValidator` uses global-admin bypass, `eventstore:tenant` claims, no-claim denial, and ordinal case-sensitive tenant comparison through the existing `ITenantValidator` abstraction.
- Client token evidence: `EventStoreSignalRClientOptions.AccessTokenProvider` already wires SignalR bearer-token supply through `HttpConnectionOptions.AccessTokenProvider`; no custom hub method token parameter is needed.
- Hosted behavior evidence source: baseline runtime proof covered anonymous negotiate; method mutation behavior came from source and direct hub unit tests; transformed tenant claims were confirmed through authentication/claims-transformation source and existing tests, with hosted authenticated coverage added by this story.

Rationale: tenant group names expose tenant/projection activity and can trigger unauthorized refresh behavior. The existing platform already has JWT authentication, normalized `eventstore:tenant` claims, and `ITenantValidator` semantics. Reusing those semantics is narrower and stronger than accepting a signal-only risk.

Global-admin semantics: reuse `ClaimsTenantValidator` / `ITenantValidator`, so global administrators can join any tenant group without a matching tenant claim.

Accepted-risk revisit trigger: not applicable because this story enforces tenant claims instead of accepting the risk.

### Debug Log References

- 2026-05-02T16:07:23+02:00 - Baseline HEAD `5753f796035019a7b3883e1bd69018e5ea5759f9`; worktree clean before status updates.
- 2026-05-02T16:07:23+02:00 - Aspire baseline started and resource list inspected; core resources were running and healthy.
- 2026-05-02T16:07:23+02:00 - Anonymous SignalR negotiate returned `200 OK` before enforcement.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~SignalR|FullyQualifiedName~ClaimsTenantValidator|FullyQualifiedName~EventStoreClaimsTransformation"` initially failed at compile time after red tests, then passed after hub enforcement: 58 passed, 0 failed.
- `dotnet test tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj --no-restore` passed: 32 passed, 0 failed.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-restore` passed: 334 passed, 0 failed.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore` passed: 281 passed, 0 failed.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --no-restore` passed: 63 passed, 0 failed.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --no-restore` passed: 78 passed, 0 failed.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore` initially exposed a pre-existing scanner false positive spanning multiple C# string literals in `SecretsProtectionTests`; after tightening the regex to stay within one string literal, the full server test project passed: 1706 passed, 0 failed.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` passed after all changes: 0 warnings, 0 errors.

### Completion Notes List

- Task 0 completed. Chosen decision is `enforce-tenant-claims`; accepted-risk path is not used.
- Task 1 completed. `ProjectionChangedHub` now requires authorized hub access, rejects unauthenticated direct invocations, delegates tenant checks to `ITenantValidator`, preserves global-admin/case-sensitive semantics, and performs tenant authorization before quota or group tracking.
- Task 2 completed. Existing group validation, quota, rollback, leave, and disconnect cleanup behavior is preserved. Client helper code did not change; `SubscribeAsync`, `StartAsync`, and reconnect still replay the same `JoinGroup(projectionType, tenantId)` intent, and the hub now reauthorizes those calls against the current principal.
- Task 3 completed. Added focused hub and hosted endpoint tests for positive/negative authorization, global-admin behavior, case-sensitive tenant matching, validation precedence, denial side effects, recovery after denial, and authenticated versus anonymous negotiate.
- Task 4 completed. Verification gates passed, including focused server tests, SignalR client tests, documented unit tests, full server tests, and release build.
- Task 5 completed. Story status and sprint status moved to `review`; neighboring post-Epic-10 rows were left unchanged.

### File List

- `_bmad-output/implementation-artifacts/post-epic-10-r10a3-hub-group-authorization-decision.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`
- `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs`
- `tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubEndpointTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs`

## Party-Mode Review

Date: 2026-05-01T22:50:36+02:00

Selected story key: `post-epic-10-r10a3-hub-group-authorization-decision`

Command/skill invocation used: `/bmad-party-mode post-epic-10-r10a3-hub-group-authorization-decision; review;`

Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect), Sally (UX/Adopter Experience)

Findings summary:

- Winston: The decision path must be structurally unavoidable before implementation, with explicit global-admin semantics, client-visible denial behavior, validation precedence, and no group-state mutation before authorization.
- Amelia: The implementation path needs a pinned failure order and should avoid ad hoc tenant-claim parsing; endpoint authorization must be proven with hosted tests, not only direct hub tests.
- Murat: The test plan needs branch-aware coverage and negative controls proving denied joins do not call `Groups.AddToGroupAsync`, do not mutate `_connectionGroups`, and do not consume quota.
- Sally: Adopter experience depends on predictable unauthorized-join failures, visible reconnect/rejoin failures, and a stronger leakage statement if risk is accepted instead of enforced.

Changes applied:

- Strengthened AC #2 to require the decision before source/test implementation and to make `enforce-tenant-claims` the party-mode recommendation while preserving `accepted-signal-only-risk` as an explicit product/security risk path.
- Tightened AC #3-#6 and AC #8-#9 around validator alignment, global-admin semantics, hosted authorization proof, accepted-risk leakage bounds, reconnect authorization, and no-side-effect denial tests.
- Added task-level failure ordering, no-state-mutation assertions, hosted endpoint test placement, colon-validation precedence, and reconnect unchanged-or-visible guidance.
- Added Dev Notes for required `JoinGroup` ordering, accepted-risk governance, expected `ITenantValidator` dependency, and direct-vs-hosted test boundaries.

Findings deferred:

- Final decision between `enforce-tenant-claims` and `accepted-signal-only-risk` remains with the dev-story/product-security decision record because choosing the risk path is an architecture/product governance decision.
- Broader tenant authorization redesign, tenant normalization, wildcard semantics, token refresh redesign, and repository-wide SignalR documentation are out of scope unless the selected implementation changes client-visible behavior.

Final recommendation: ready-for-dev

## Advanced Elicitation

Date: 2026-05-02T10:53:02+02:00

Selected story key: `post-epic-10-r10a3-hub-group-authorization-decision`

Command/skill invocation used: `/bmad-advanced-elicitation post-epic-10-r10a3-hub-group-authorization-decision`

Batch 1 method names: Security Audit Personas; Red Team vs Blue Team; Architecture Decision Records; Failure Mode Analysis; Comparative Analysis Matrix

Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction

Findings summary:

- Security review: The story already identifies the tenant-group leak, but the decision record needed to capture evidence source, accepting role, and a finite revisit trigger if risk is accepted.
- Red/blue review: Reconnect group replay is the main bypass-shaped edge case; stored client intent must not substitute for current hub principal authorization.
- Architecture review: `ITenantValidator` reuse is still the right boundary, with `Context.ConnectionAborted` as the natural cancellation token for hub method validation.
- Failure analysis: Denied joins need recovery coverage because a stale `_connectionGroups` entry or consumed quota would make one failed authorization attempt affect later valid joins.
- Simplicity review: No new group format, token parameter, tenant normalization rule, or SignalR payload data is needed for this story.

Changes applied:

- Expanded AC #1-#3 to require evidence-source clarity, decision ownership/revisit details, unauthenticated denial, and validator cancellation guidance.
- Hardened AC #8-#9 and Tasks 2-3 so reconnect replay must reauthorize and denied joins must leave no tracking or quota side effects while still allowing a later valid join.
- Added Dev Notes forbidding reconnect intent as authorization evidence and recommending `Context.ConnectionAborted` for `ITenantValidator.ValidateAsync`.

Findings deferred:

- The actual `enforce-tenant-claims` versus `accepted-signal-only-risk` decision remains deferred to dev-story execution and product/security governance.
- Any broader SignalR token-refresh UX, tenant normalization policy, hub method authorization policy object, or group-format redesign remains out of scope for this story.

Final recommendation: ready-for-dev

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-02 | 1.1 | Code review applied: sanitized hub error message, wrapped validator exceptions, pruned denied rejoins in client, refactored colon-ordering test, strengthened unauth quota recovery assertion. | bmad-code-review |
| 2026-05-02 | 1.0 | Implemented `enforce-tenant-claims` SignalR hub group authorization with hosted auth proof, focused allow/deny tests, and verification gates. | GPT-5 Codex |
| 2026-05-02 | 0.3 | Advanced elicitation hardened decision evidence, reconnect reauthorization, cancellation, and denied-join recovery coverage. | Codex automation |
| 2026-05-01 | 0.2 | Party-mode review hardened decision ordering, authorization side effects, global-admin semantics, and test boundaries. | Codex automation |
| 2026-05-01 | 0.1 | Created ready-for-dev R10-A3 hub group authorization decision story. | Codex automation |

## Verification Status

Implementation verification complete. The chosen decision is `enforce-tenant-claims`. Code-review patches applied and re-verified: 59/59 focused SignalR/Claims server tests pass, 32/32 SignalR client tests pass, full release build clean (0 warnings, 0 errors).

## Review Findings

Date: 2026-05-02
Reviewer: bmad-code-review (Blind Hunter + Edge Case Hunter + Acceptance Auditor)
Counts: 2 decision-needed, 3 patch, 12 deferred, 13 dismissed.

### Decision Needed

- [x] [Review][Decision] **Hub error message echoes `tenantValidation.Reason` to client** — Resolved: sanitize. Hub now throws generic `HubException("Tenant authorization failed.")` and logs the validator's Reason at warn level (EventId 1084). Three direct hub tests updated to assert the generic message.
- [x] [Review][Decision] **AC #8 silent rejoin failure in `EventStoreSignalRClient.JoinAllGroupsAsync`** — Resolved: prune-on-`HubException` + log at error. Server-side denials prune `_subscribedGroups` so reconnects no longer retry forever; transient transport failures retain the existing warn-level retry-via-reconnect path. No new public API surface.

### Patch

- [x] [Review][Patch] **Test name oversells colon-vs-auth ordering coverage** [`tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs`] — Resolved: renamed to `JoinGroup_ProjectionTypeContainsColon_RejectsBeforeAuthorization`; uses a substitute `ITenantValidator` and asserts `DidNotReceive` + no group add after the colon-malformed call. No follow-up valid call confounds the assertion.
- [x] [Review][Patch] **Validator exceptions propagate raw to client** [`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`] — Resolved: validator call wrapped in try/catch. `OperationCanceledException` rethrown; others logged via EventId 1085 and surfaced as `HubException("Tenant authorization unavailable.")`. New direct hub test `JoinGroup_TenantValidatorThrows_LogsAndThrowsGenericHubException` covers the path.
- [x] [Review][Patch] **`JoinGroup_UnauthenticatedUser_...` test does not assert tracking/quota state remains untouched** — Resolved: extended the test to flip `Context.User` to an authorized principal on the same connection and assert a follow-up `JoinGroup` succeeds with `maxGroupsPerConnection: 1`, proving the prior denial did not consume quota or mutate tracking.

### Deferred

- [x] [Review][Defer] **`ConcurrentDictionary<string, HashSet<string>>` inner set is not thread-safe** [`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:30,62-86`] — pre-existing; per-connection lock on the inner `HashSet` covers the read-modify-write but the await on `AddToGroupAsync` happens outside the lock and the rollback re-acquires the lock on a possibly-replaced set. Not introduced by R10-A3, but the new tenant-validate await widens the window between auth and group-add. **Why:** ordering invariant remains valid; race only manifests with concurrent JoinGroup on same connection (SignalR per-connection ordering reduces likelihood).
- [x] [Review][Defer] **TOCTOU between auth and `AddToGroupAsync` (token rotation mid-call)** [`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:51-73`] — Validator decision is fixed at the time of check; if claims are revoked between validate and add, the join still succeeds. Inherent to async authorization; pre-existing platform-level concern. Periodic re-validation belongs in a follow-up policy story.
- [x] [Review][Defer] **`SecretsProtectionTests` regex tightening may miss multi-line raw-string secrets** [`tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs:127-131`] — Adding `\r\n` exclusions inside the bracket classes prevents the scanner from spanning multiple string literals (the documented fix), but also blocks detection inside C# raw string literals (`"""..."""`) that legitimately contain newlines. Out-of-scope cleanup for R10-A3; revisit when scanning verbatim/raw strings.
- [x] [Review][Defer] **Cached principal on reconnect; access token expiry mid-connection** [`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:38-57`] — SignalR caches the connection's `HttpContext.User` on negotiate. Subsequent `JoinGroup` calls re-read `Context.User` (covered by the validator), but the underlying authentication ticket does not expire mid-connection unless the access-token provider refreshes. Token-refresh policy belongs to a separate cross-cutting story.
- [x] [Review][Defer] **`LeaveGroup` has no input validation and no auth check** [`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:97-108`] — `LeaveGroup` does not validate null/whitespace/colon, and after `[Authorize]` only blocks anonymous callers. An authenticated tenant-A user calling `LeaveGroup("...", "tenantB")` cannot evict tenant-B's tracked entries because tracking is per-`ConnectionId`; impact is bounded but the symmetry with `JoinGroup` is missing. Pre-existing.
- [x] [Review][Defer] **`OnDisconnectedAsync` race with in-flight `JoinGroup`** [`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:62-86,117-122`] — If disconnect runs while a JoinGroup awaits between tenant-validate and tracking, disconnect `TryRemove`s the dictionary entry; concurrent JoinGroup later locks the orphan `HashSet` and adds to it; subsequent disconnect cleanup misses it. Pre-existing.
- [x] [Review][Defer] **`ClaimsTenantValidator.ValidateAsync` does not validate the tenantId argument** [`src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs`] — Hub guards null/whitespace before calling, but other callers can pass empty/whitespace tenant values; would compare against an empty string rather than throwing. Defensive enhancement for the validator; pre-existing.
- [x] [Review][Defer] **`global_admin` claim brittle to `"1"`/`"yes"`/`"True "` values** [`src/Hexalith.EventStore/Authorization/GlobalAdministratorHelper.cs`] — `bool.TryParse` only accepts `True`/`False` (case-insensitive). Tokens issued with `"1"` or `"yes"` silently lose admin privilege. Pre-existing claim-matching behavior; document or normalize in a separate cross-cutting story.
- [x] [Review][Defer] **`SubscribeAsync` race during `Reconnecting` state** [`src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs:108-110`] — Subscriptions added while state transitions out of `Connected` are cached but not joined; the next `Reconnected` event picks them up via `JoinAllGroupsAsync`. Tight race window during state transition. Pre-existing.
- [x] [Review][Defer] **Whitespace/control characters survive identifier validation** [`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs:38-45`] — `ArgumentException.ThrowIfNullOrWhiteSpace` rejects only fully-whitespace values; `"acme \t"` passes through and is concatenated into a group name. Case-sensitive comparison with the claim then surprises the caller. Identifier regex hardening belongs to a separate input-validation story.
- [x] [Review][Defer] **Tenant claim name (`eventstore:tenant`) is hardcoded in test helpers** [`tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs:CreateUser`] — Tests pass even if the validator's claim-name expectation drifts because the helper hardcodes the same string the validator expects. A pinned constant (e.g., `EventStoreClaimTypes.Tenant`) shared by validator and test would catch drift. Pre-existing pattern.
- [x] [Review][Defer] **Hosted Tier-2 `JoinGroup` test gap** [`tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubEndpointTests.cs`] — Hosted coverage proves anonymous negotiate is rejected and authenticated negotiate is accepted; it does not invoke `JoinGroup` end-to-end through `EventStoreClaimsTransformation` and the JWT-bearer claim-mapping pipeline. AC #5 is met by the combination of `[Authorize]` + hosted negotiate + direct hub tests, but a runtime-level JoinGroup proof would close the claim-name/transformation drift risk. Coverage enhancement.

### Dismissed

- `[Authorize]` attribute + explicit `Context.User?.Identity?.IsAuthenticated` check (defense in depth, intentional).
- Negotiate hosted test asserting only `200 OK` (separate JoinGroup unit/auth tests cover method-level enforcement).
- `LeaveGroup` cross-tenant eviction (mitigated by `[Authorize]` + per-`ConnectionId` tracking scope).
- Dev story self-marks status to `review` in same diff (this is the documented BMAD workflow).
- `ClaimsTenantValidator` source not in diff (used as default `tenantValidator` in tests; behavior verified by its own test project).
- `Context.User` non-null with null `Identity` (handled by `?.` chain).
- Disconnect-cleanup idempotent removal (no observable fault).
- Lock-on-replaced-`HashSet` pattern fragility (no observable bug post-rollback).
- AddToGroupAsync rollback on duplicate-join (`addedToTracking=false` correctly skips rollback).
- AC #10 wording defect ("both `last_updated` fields") — spec text defect, single field correctly updated.
- Task 0.4 sequencing only verifiable via Debug Log narration (acceptable for a static review).
- Task 4.4 Aspire/runtime proof recorded as Debug Log narration (AC permits "if attempted").
- `ClaimsTenantValidator` behavior unverified in diff (out of scope; covered by the validator's own tests).
