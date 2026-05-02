# Post-Epic-10 R10-A3: Hub Group Authorization Decision

Status: ready-for-dev

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

- [ ] Task 0: Baseline and decision framing (AC: #1, #2, #6)
  - [ ] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [ ] 0.2 Inspect current hub negotiate and method invocation behavior with SignalR enabled.
  - [ ] 0.3 Confirm whether authenticated hub connections receive transformed `eventstore:tenant` claims.
  - [ ] 0.4 Add a Dev Agent Record decision block with the chosen path and rationale before source/test edits. Party-mode recommends `enforce-tenant-claims`; `accepted-signal-only-risk` requires explicit product/security risk acceptance.
  - [ ] 0.5 If the decision is accepted risk, record a dated revisit trigger tied to a production milestone or security review, not an open-ended "later".

- [ ] Task 1: Implement or document the authorization decision (AC: #3, #4, #5, #6, #7)
  - [ ] 1.1 If enforcing, add `[Authorize]` or equivalent policy to the hub or `JoinGroup`.
  - [ ] 1.2 If enforcing, preserve the required failure order: null/whitespace and colon validation -> authenticated user required -> tenant authorization -> quota check -> `_connectionGroups` mutation -> `Groups.AddToGroupAsync` -> rollback on add failure.
  - [ ] 1.3 If enforcing, reuse `ITenantValidator`/`ClaimsTenantValidator` semantics or document any intentional difference.
  - [ ] 1.4 If accepting risk, add the accepted-risk record with explicit mitigation and revisit trigger, without code changes that pretend to enforce authorization.
  - [ ] 1.5 Preserve current exception shape where practical; use `HubException` for caller-visible tenant-denial join failures unless ASP.NET Core authorization naturally returns the failure. Do not silently no-op unauthorized joins.
  - [ ] 1.6 If enforcing, pass the hub connection cancellation token to tenant validation and keep authorization failures out of the group-name tracking path.

- [ ] Task 2: Preserve group and client behavior (AC: #7, #8)
  - [ ] 2.1 Verify colon validation still rejects `projectionType` or `tenantId` containing `:`.
  - [ ] 2.2 Verify denied authorization does not create a `_connectionGroups` entry, call `Groups.AddToGroupAsync`, or consume per-connection group quota.
  - [ ] 2.3 Verify failed `Groups.AddToGroupAsync` rollback still works after authorization changes.
  - [ ] 2.4 Verify `LeaveGroup` and disconnect cleanup still remove tracked groups.
  - [ ] 2.5 If the client helper needs token guidance or behavior change, keep it narrowly scoped to token supply and rejoin error visibility. If the client helper does not change, explicitly record that reconnect behavior remains unchanged and covered by existing behavior.
  - [ ] 2.6 Verify the same connection can still join an allowed tenant after one or more denied joins.

- [ ] Task 3: Add focused test coverage (AC: #3-#9)
  - [ ] 3.1 Add allowed-tenant `JoinGroup` coverage.
  - [ ] 3.2 Add wrong-tenant and no-tenant-claims denial coverage that asserts no group add, no group tracking, and no quota consumption.
  - [ ] 3.3 Add global-admin coverage for the chosen rule, including the case-sensitive tenant comparison baseline for non-admin users.
  - [ ] 3.4 Add anonymous negotiate or anonymous hub-method coverage for the chosen hub authorization posture in hosted endpoint tests.
  - [ ] 3.5 Add colon-validation precedence coverage proving malformed group parts fail before auth/quota side effects.
  - [ ] 3.6 Add a same-connection denial-then-allowed test to prove authorization denial did not poison quota or tracking state.
  - [ ] 3.7 Re-run existing SignalR hub/client tests touched by the change.

- [ ] Task 4: Verification gates (AC: #1-#10)
  - [ ] 4.1 Run `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~SignalR|FullyQualifiedName~ClaimsTenantValidator|FullyQualifiedName~EventStoreClaimsTransformation"`.
  - [ ] 4.2 If client helper code changed, run `dotnet test tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj --no-restore`.
  - [ ] 4.3 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.
  - [ ] 4.4 If Aspire/runtime proof is attempted, capture exact commands and blockers in the Dev Agent Record; runtime proof is helpful but not required unless the implementation depends on hosted authentication behavior.

- [ ] Task 5: Story bookkeeping (AC: #10)
  - [ ] 5.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [ ] 5.2 Move this story and only this story from `ready-for-dev` to `review` at dev handoff.
  - [ ] 5.3 Leave R10-A2/R10-A5/R10-A6/R10-A7/R10-A8 status rows unchanged.

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

To be filled by dev agent.

### Decision Record

To be filled by dev agent. Required value: `enforce-tenant-claims` or `accepted-signal-only-risk`.

### Debug Log References

To be filled by dev agent.

### Completion Notes List

To be filled by dev agent.

### File List

To be filled by dev agent.

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
| 2026-05-02 | 0.3 | Advanced elicitation hardened decision evidence, reconnect reauthorization, cancellation, and denied-join recovery coverage. | Codex automation |
| 2026-05-01 | 0.2 | Party-mode review hardened decision ordering, authorization side effects, global-admin semantics, and test boundaries. | Codex automation |
| 2026-05-01 | 0.1 | Created ready-for-dev R10-A3 hub group authorization decision story. | Codex automation |

## Verification Status

Story creation plus party-mode review plus advanced elicitation only. Implementation verification is pending dev-story execution.
