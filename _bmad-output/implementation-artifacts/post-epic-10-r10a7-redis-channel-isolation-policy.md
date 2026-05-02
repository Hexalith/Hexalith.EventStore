# Post-Epic-10 R10-A7: Redis Channel Isolation Policy

Status: ready-for-dev

<!-- Source: epic-10-retro-2026-05-01.md R10-A7 -->
<!-- Source: sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md Proposal 7 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform architect / DevOps engineer responsible for production SignalR topology**,
I want an explicit Redis deployment isolation and SignalR channel-prefix policy,
so that shared Redis infrastructure cannot accidentally distribute projection-change signals across environments, applications, tenants, or test lanes.

## Story Context

Epic 10 delivered SignalR projection-change notifications, optional Redis backplane support, reconnect behavior, tenant-aware hub group authorization, and a Tier 3 cross-instance Redis proof. The remaining R10-A7 gap is policy and configuration discipline: the current implementation accepts `EventStore:SignalR:BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS`, parses it into `StackExchange.Redis.ConfigurationOptions`, and sets `AbortOnConnectFail = false`, but it does not expose an EventStore-owned channel-prefix option or document the production isolation boundary.

Microsoft's SignalR Redis backplane guidance says a Redis backplane should be close to the SignalR app in production and that different SignalR apps sharing one Redis server should use different channel prefixes. StackExchange.Redis also supports `channelPrefix` / `ConfigurationOptions.ChannelPrefix` for pub/sub operations. This story must decide how Hexalith.EventStore applies those capabilities without weakening the existing fail-open behavior or creating another tenant-isolation mechanism that developers can confuse with hub authorization.

Current HEAD at story creation: `e76adff`.

## Acceptance Criteria

1. **Current Redis/backplane posture is inspected and recorded.** Document how the EventStore server currently enables SignalR, resolves the Redis backplane endpoint, sets `AbortOnConnectFail = false`, maps the hub, and enables SignalR in the AppHost/sample topology. Include whether a channel prefix can already be supplied through the raw StackExchange.Redis connection string and whether any EventStore-specific `ChannelPrefix` option exists.

2. **Production isolation decision is explicit.** Add a dated decision record choosing exactly one primary production policy: `separate-redis-per-isolation-boundary`, `shared-redis-with-required-channel-prefix`, or `shared-redis-with-accepted-risk`. The record must name the decision owner/role, the intended isolation boundary, the evidence reviewed, and the revisit trigger.

3. **Isolation boundary is defined in deployment terms.** The policy must define whether the boundary is environment, application instance, tenant, cluster, namespace, or test lane. It must explicitly state that SignalR Redis channel separation is not tenant authorization and does not replace JWT claims, `ProjectionChangedHub.JoinGroup` authorization, query authorization, or DAPR access-control policies.

4. **If shared Redis is allowed, channel-prefix requirements are concrete.** Specify the prefix format, allowed characters, source of value, example values for local/dev/test/prod, collision-risk handling, and whether the prefix must include environment and application identity. Do not include secrets, tenant IDs with sensitive meaning, connection strings, or raw deployment names that leak production topology into public logs.

5. **If a first-class EventStore option is required, implement it narrowly.** Add `EventStore:SignalR:BackplaneRedisChannelPrefix` or a similarly named option only if the decision requires an EventStore-owned setting instead of relying on raw `channelPrefix=` inside the connection string. The implementation must set `ConfigurationOptions.ChannelPrefix` before `AddStackExchangeRedis`, preserve `AbortOnConnectFail = false`, validate null/empty/invalid values, and not change the public hub payload or group format.

6. **If the raw connection-string option is accepted, document it completely.** If no first-class option is added, production guidance must show how to set `channelPrefix=...` in the existing Redis connection string path and explain why that is sufficient. Add a deferred-work entry if a first-class option is deliberately postponed.

7. **Runtime and evidence guidance prevents false confidence.** Update deployment or operational documentation so future runtime proofs record Redis endpoint identity, effective channel-prefix decision, same-prefix positive case, different-prefix negative/control case when practical, and the expected fail-open behavior when Redis is unavailable.

8. **Existing runtime proof boundaries remain intact.** Do not re-prove R10-A2 cross-instance delivery, R10-A3 tenant authorization, R10-A5 reconnect guidance, or R10-A6 operational evidence pattern in this story. This story may update those documents only by adding narrow links or routing notes.

9. **Tests match the chosen path.** If source configuration changes are made, add focused unit tests for option binding/validation and `ConfigureBackplane` behavior, including preserving `AbortOnConnectFail = false` and setting the expected channel prefix. If docs/policy only change, run markdown/link checks when available and record why code tests are not required.

10. **AppHost and publish guidance are checked.** Inspect `src/Hexalith.EventStore.AppHost/Program.cs`, publish/deployment guidance, and any Docker/Kubernetes/ACA notes for Redis reuse assumptions. If production overlays should require separate Redis or a prefix, document the required environment variables/configuration keys and whether local development remains unchanged.

11. **Secrets and operational safety are preserved.** The policy and any logs/evidence must redact passwords, connection strings with credentials, Redis endpoints containing secrets, access tokens, and production topology details that are not safe for repo artifacts. Prefix values are configuration metadata, not credentials.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names R10-A7 and the selected isolation policy. At code-review signoff, both become `done`.

## Scope Boundaries

- Do not change the SignalR hub route, public hub payload, or group format. The hub remains `/hubs/projection-changes`, and the client payload remains `ProjectionChanged(projectionType, tenantId)`.
- Do not treat Redis channel prefixing as tenant isolation. Tenant authorization remains claim/query/hub authorization work.
- Do not make persistent containers or a shared production Redis dependency mandatory for ordinary local development.
- Do not change DAPR state store, DAPR pub/sub, command processing, projection state storage, or Redis keys used by actors.
- Do not expand runtime proof endpoints beyond the Development/Test-only posture created by R10-A2.
- Do not commit raw production Redis connection strings, secrets, tokens, HAR files, or full network traces.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| SignalR options | `src/Hexalith.EventStore/SignalRHub/SignalROptions.cs` | Inspect/add any first-class channel-prefix option and validation |
| Backplane registration | `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs` | Preserve Redis parsing and `AbortOnConnectFail = false`; apply prefix only if required |
| Runtime proof endpoints | `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs` | Record whether effective prefix evidence needs to be exposed in Development/Test only |
| Hub contract | `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs` | Confirm group authorization/payload boundaries stay unchanged |
| SignalR runtime proof | `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` | Add only narrow prefix evidence/control if source changes require it; do not duplicate R10-A2 |
| Backplane wiring tests | `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRBackplaneWiringTests.cs` | Add focused configuration tests if a first-class prefix option is implemented |
| AppHost | `src/Hexalith.EventStore.AppHost/Program.cs` | Inspect local SignalR enablement and deployment environment assumptions |
| Deployment/docs | `docs/`, `_bmad-output/implementation-artifacts/deferred-work.md`, publish notes | Record policy, examples, accepted risk, or deferred first-class config work |
| Package versions | `Directory.Packages.props` | Current SignalR Redis backplane package is `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 10.0.5 |

## Tasks / Subtasks

- [ ] Task 0: Baseline policy and code inspection (AC: #1, #2, #3, #8, #10)
  - [ ] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [ ] 0.2 Inspect `SignalROptions`, `SignalRServiceCollectionExtensions`, `Program.cs`, AppHost SignalR wiring, and the R10-A2 runtime proof endpoints.
  - [ ] 0.3 Record whether `channelPrefix=...` already works through the existing connection string parse path and whether a first-class EventStore option exists.
  - [ ] 0.4 Review deployment/publish docs for shared Redis assumptions, environment naming, and secret-redaction guidance.
  - [ ] 0.5 Add a decision record before source changes choosing one primary policy path.

- [ ] Task 1: Document the production isolation policy (AC: #2, #3, #4, #6, #7, #10, #11)
  - [ ] 1.1 Define the isolation boundary and whether Redis must be separate per boundary or may be shared with a prefix.
  - [ ] 1.2 If shared Redis is allowed, define the required prefix format and examples for local, test, staging, and production.
  - [ ] 1.3 Explain that prefixes protect SignalR pub/sub channel separation only and do not replace auth or query authorization.
  - [ ] 1.4 Add operational evidence requirements for endpoint identity, prefix evidence, positive delivery, negative/control isolation, and fail-open behavior.
  - [ ] 1.5 Add secret-redaction rules for evidence and docs.

- [ ] Task 2: Implement first-class prefix support only if required (AC: #5, #9)
  - [ ] 2.1 Add a narrow `BackplaneRedisChannelPrefix` option if the decision record requires it.
  - [ ] 2.2 Apply the value to `ConfigurationOptions.ChannelPrefix` before registering the Redis backplane.
  - [ ] 2.3 Preserve `AbortOnConnectFail = false` exactly.
  - [ ] 2.4 Validate null/empty/invalid prefix values without breaking the existing no-prefix path.
  - [ ] 2.5 Keep the existing `BackplaneRedisConnectionString` and `EVENTSTORE_SIGNALR_REDIS` behavior compatible.

- [ ] Task 3: Tests and proof guidance (AC: #7, #8, #9)
  - [ ] 3.1 If source changes are made, add focused tests for option binding/validation and Redis configuration output.
  - [ ] 3.2 If runtime proof evidence is touched, add only prefix/effective-config evidence needed by the chosen policy.
  - [ ] 3.3 Record different-prefix negative/control guidance without requiring a full R10-A2 proof rerun unless the source change affects runtime behavior.
  - [ ] 3.4 If docs only change, run markdown/link validation when available and record the result.

- [ ] Task 4: Bookkeeping and verification (AC: #9, #12)
  - [ ] 4.1 Update the Dev Agent Record with decision owner/role, chosen policy, and evidence reviewed.
  - [ ] 4.2 Update File List, Change Log, and Verification Status.
  - [ ] 4.3 Move only this story and its sprint-status row to `review` at dev handoff.
  - [ ] 4.4 At code-review signoff, move only this story and its sprint-status row to `done`.

## Dev Notes

### Architecture Guardrails

- SignalR remains an invalidation channel. Redis channel prefixing changes backplane pub/sub routing only; it does not change EventStore domain data, projection state, ETags, query results, or hub payloads.
- Prefer deployment isolation when policy risk is unclear. A separate Redis per environment/application boundary is easier to reason about than a shared Redis that depends on every deployment setting the correct prefix.
- If shared Redis is accepted, make the prefix mandatory for all non-local multi-instance deployments and treat missing prefix as a deployment misconfiguration unless the decision record explicitly accepts the risk.
- Prefix format should be stable, low-cardinality, and environment/application scoped. Avoid tenant-scoped prefixes because they can leak tenant identities, multiply subscriptions, and confuse tenant authorization with backplane isolation.
- Do not change `{projectionType}:{tenantId}` SignalR group names. They are hub-level group routing, not Redis backplane channel names.
- Preserve fail-open behavior. Redis backplane startup or publish failures must not break command processing, ETag regeneration, or local single-instance operation.
- If using raw connection string `channelPrefix=...`, document that the value is parsed by StackExchange.Redis, not by EventStore option binding.
- If adding a first-class option, keep both paths deterministic: the EventStore option must either override the raw connection string prefix with a documented precedence rule or fail validation when both disagree. Do not allow silent conflicts.

### Current-Code Intelligence

- `SignalROptions` currently has `Enabled`, `BackplaneRedisConnectionString`, and `MaxGroupsPerConnection`. There is no EventStore-specific channel-prefix option at story creation.
- `SignalRServiceCollectionExtensions.ConfigureBackplane()` resolves Redis from `BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS`, parses it with `StackExchange.Redis.ConfigurationOptions.Parse(redis)`, and sets `AbortOnConnectFail = false`.
- Because the code parses the full StackExchange.Redis connection string, `channelPrefix=...` may already be usable through the raw Redis connection string path, but this repository does not document or test that as a production policy.
- `ProjectionChangedHub` is `[Authorize]` and delegates tenant authorization to `ITenantValidator`; do not weaken or bypass this when documenting Redis isolation.
- `src/Hexalith.EventStore.AppHost/Program.cs` enables SignalR for the EventStore resource when the Blazor UI is present, but it does not currently set a Redis backplane endpoint or channel prefix for the default local topology.
- R10-A2 added Development-only runtime proof endpoints and a Tier 3 Redis backplane proof. Those endpoints expose effective Redis connection-string evidence with secret redaction, but not a separate EventStore channel-prefix field at story creation.

### Previous Story Intelligence

- Story 10.1 pinned the signal-only payload and group format.
- Story 10.2 added structural Redis backplane wiring and preserved fail-open startup behavior.
- Story 10.3 established reconnect/rejoin semantics but left missed-signal catch-up to client query behavior.
- R10-A2 proved cross-instance Redis delivery and recorded that Redis channel/database/prefix isolation policy remains deferred to R10-A7.
- R10-A3 enforced tenant-aware group authorization. Redis prefixing must not be sold as a replacement for that authorization.
- R10-A5 and R10-A6 are documentation/evidence follow-ups. Reference them for client/operator guidance without absorbing their scope.

### Testing Standards

- Docs/policy-only work should run available markdown or link validation and record the command/result.
- Source configuration changes should be covered by focused server tests. Prefer existing Shouldly/xUnit patterns and run the affected test project individually.
- Runtime proof changes belong in Tier 3 integration tests and should stay Docker/Redis gated.
- Do not run solution-level `dotnet test`; run affected projects individually.
- If no source files change, do not invent a code test just to have one. The verification gate is policy clarity plus documentation validation.

### Latest Technical Information

- Microsoft Learn for ASP.NET Core SignalR Redis scale-out recommends keeping the Redis backplane in the same data center as the SignalR app for production and using different channel prefixes when one Redis server is shared by multiple SignalR apps.
- StackExchange.Redis configuration supports `channelPrefix` / `ConfigurationOptions.ChannelPrefix` as an optional prefix for pub/sub operations.
- The repository currently uses `Microsoft.AspNetCore.SignalR.StackExchangeRedis` version `10.0.5`.

### Project Structure Notes

- SignalR server code lives in `src/Hexalith.EventStore/SignalRHub/`.
- AppHost orchestration lives in `src/Hexalith.EventStore.AppHost/Program.cs`.
- Existing SignalR server tests live under `tests/Hexalith.EventStore.Server.Tests/SignalR/` and `tests/Hexalith.EventStore.Server.Tests/Integration/`.
- Existing runtime proof evidence lives under `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/`.
- Expected BMAD edits during dev are this story file, deployment/docs policy text, possibly `deferred-work.md`, optional narrow SignalR option/config tests, and `sprint-status.yaml` bookkeeping.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md#Proposal-7`] - R10-A7 acceptance criteria and rationale.
- [Source: `_bmad-output/implementation-artifacts/epic-10-retro-2026-05-01.md`] - R10-A7 action item and Redis channel-prefix risk.
- [Source: `_bmad-output/implementation-artifacts/10-2-redis-backplane-for-multi-instance-signalr.md`] - Original Redis backplane wiring story.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof.md`] - Runtime proof and deferred prefix-policy note.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a3-hub-group-authorization-decision.md`] - Tenant-aware hub authorization boundary.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a6-signalr-operational-evidence-pattern.md`] - Operational evidence schema and failure classification.
- [Source: `src/Hexalith.EventStore/SignalRHub/SignalROptions.cs`] - Current SignalR configuration surface.
- [Source: `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`] - Current Redis backplane registration and fail-open option.
- [Source: `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`] - Hub route, group authorization, and group format.
- [Source: `src/Hexalith.EventStore.AppHost/Program.cs`] - Current local/AppHost SignalR enablement.
- [Source: `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs`] - Current Tier 3 Redis backplane proof and evidence pattern.
- [Source: Microsoft Learn, `https://learn.microsoft.com/aspnet/core/signalr/redis-backplane`] - SignalR Redis backplane production and channel-prefix guidance.
- [Source: StackExchange.Redis docs, `https://stackexchange.github.io/StackExchange.Redis/Configuration.html`] - `channelPrefix` / `ConfigurationOptions.ChannelPrefix` support.

## Dev Agent Record

### Agent Model Used

To be filled by dev agent.

### Decision Record

To be filled by dev agent before source changes. Required value: `separate-redis-per-isolation-boundary`, `shared-redis-with-required-channel-prefix`, or `shared-redis-with-accepted-risk`.

### Debug Log References

To be filled by dev agent.

### Completion Notes List

To be filled by dev agent.

### File List

To be filled by dev agent.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-02 | 0.1 | Created ready-for-dev R10-A7 Redis channel isolation policy story. | Codex automation |

## Verification Status

Story creation only. Production isolation decision, documentation updates, optional prefix configuration, and verification are intentionally deferred to `bmad-dev-story`.
