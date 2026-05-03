# Post-Epic-10 R10-A7: Redis Channel Isolation Policy

Status: done

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

2. **Production isolation decision is explicit.** Add a dated decision record before source changes choosing exactly one primary production policy: `separate-redis-per-isolation-boundary`, `shared-redis-with-required-channel-prefix`, or `shared-redis-with-accepted-risk`. The record must name the decision owner/role, the isolation boundary dimensions, the Redis topology, the evidence reviewed, the affected environments, the residual risk, and the revisit trigger. `shared-redis-with-accepted-risk` must include an owner-approved exception, an expiry or revisit date, and a statement that no Redis channel-isolation guarantee is provided for the accepted boundary.

3. **Isolation boundary is defined in deployment terms.** The policy must define the boundary as the tuple of environment, deployed application/product instance, cluster or namespace when relevant, tenant or tenant lane when Redis is intentionally shared across tenant-isolated runtimes, and test/CI lane when Redis is shared by parallel runs. It must explicitly state whether tenant separation is required at the Redis SignalR-channel level, application authorization level, or both. It must also state that SignalR Redis channel separation is not tenant authorization and does not replace JWT claims, `ProjectionChangedHub.JoinGroup` authorization, query authorization, or DAPR access-control policies.

4. **If shared Redis is allowed, channel-prefix requirements are concrete.** Specify the prefix format, allowed characters, source of value, deploy-time owner, example values for local/dev/test/prod, collision-risk handling, and whether the prefix must include environment and application identity. Outside local single-app development, the prefix must be non-empty after trimming, stable across restarts, safe for Redis channel names, and must distinguish every shared isolation boundary; include tenant or test-lane identity only when Redis is intentionally shared across those scopes and document the cardinality/privacy tradeoff. Do not include secrets, tenant IDs with sensitive meaning, connection strings, or raw deployment names that leak production topology into public logs.

5. **If a first-class EventStore option is required, implement it narrowly.** Add `EventStore:SignalR:BackplaneRedisChannelPrefix` or a similarly named option only if the selected decision requires an EventStore-owned setting and no existing SignalR/StackExchange.Redis configuration path can express the required prefix safely and documentably. The option must be scoped only to SignalR Redis backplane channel prefixing, not general Redis naming, tenancy, cache, pub/sub, or security abstraction. Existing Redis connection parsing remains authoritative for connectivity; document precedence explicitly if both raw `channelPrefix=` and an EventStore-owned option are present, and fail validation rather than silently accepting conflicting prefix values. The implementation must set `ConfigurationOptions.ChannelPrefix` before `AddStackExchangeRedis`, preserve `AbortOnConnectFail = false`, validate null/empty/invalid values according to the selected policy, and not change the public hub payload or group format.

6. **If the raw connection-string option is accepted, document it completely.** If no first-class option is added, production guidance must show how to set `channelPrefix=...` in the existing Redis connection string path and explain why that is sufficient. Add a deferred-work entry if a first-class option is deliberately postponed.

7. **Runtime and evidence guidance prevents false confidence.** Update deployment or operational documentation so future runtime proofs record redacted Redis endpoint identity, database/index when used, effective channel-prefix decision, same-prefix positive case, different-prefix negative/control case when practical, deploy-time owner for the selected setting, and the expected fail-open behavior when Redis is unavailable. If source configuration changes are made, prove that the configured prefix affects the actual SignalR Redis backplane configuration rather than only binding to an unused option.

8. **Existing runtime proof boundaries remain intact.** Do not re-prove R10-A2 cross-instance delivery, R10-A3 tenant authorization, R10-A5 reconnect guidance, or R10-A6 operational evidence pattern in this story. This story may update those documents only by adding narrow links or routing notes.

9. **Tests match the chosen path.** If source configuration changes are made, add focused unit/configuration tests for option binding, prefix validation, missing-prefix failure when the selected shared-Redis policy requires one, accepted-risk/no-prefix behavior if that path is selected, documented precedence or conflict handling, and `ConfigureBackplane` behavior, including preserving `AbortOnConnectFail = false` and setting the expected channel prefix. Prefer unit/configuration tests unless an existing lightweight Redis/SignalR fixture already supports a narrow control proof; do not turn this story into a broad infrastructure proof. If docs/policy only change, run markdown/link checks when available and record why code tests are not required.

10. **AppHost and publish guidance are checked.** Inspect `src/Hexalith.EventStore.AppHost/Program.cs`, publish/deployment guidance, and any Docker/Kubernetes/ACA notes for Redis reuse assumptions. If production overlays should require separate Redis or a prefix, document the required environment variables/configuration keys, who supplies them for AppHost, CI/test lanes, and production, and whether local development remains unchanged. Include an operator decision table covering when each allowed policy is appropriate, required configuration, required evidence, and production suitability.

11. **Secrets and operational safety are preserved.** The policy and any logs/evidence must redact passwords, connection strings with credentials, access keys, access tokens, Redis endpoints containing secrets, hostnames or topology details that are not safe for repo artifacts, and tenant-identifying values when required. Avoid committing raw `ConfigurationOptions.ToString()` output unless it is proven safe or sanitized. Prefix values are configuration metadata, not credentials, but examples in repo artifacts must be sanitized.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names R10-A7 and the selected isolation policy. At code-review signoff, both become `done`.

## Scope Boundaries

- Do not change the SignalR hub route, public hub payload, or group format. The hub remains `/hubs/projection-changes`, and the client payload remains `ProjectionChanged(projectionType, tenantId)`.
- Do not treat Redis channel prefixing as tenant isolation. Tenant authorization remains claim/query/hub authorization work.
- Do not make persistent containers or a shared production Redis dependency mandatory for ordinary local development.
- Do not change DAPR state store, DAPR pub/sub, command processing, projection state storage, or Redis keys used by actors.
- Do not expand runtime proof endpoints beyond the Development/Test-only posture created by R10-A2.
- Do not commit raw production Redis connection strings, secrets, tokens, HAR files, or full network traces.
- Do not redesign Redis infrastructure, introduce Redis ACL/mTLS/key-management work, change Keycloak/auth behavior, alter DAPR service invocation, or add new environment provisioning unless the selected policy explicitly requires documentation-only deployment guidance.
- Do not introduce SignalR tenant routing, hub restructuring, new Redis multiplexing abstractions, projection semantics changes, cache key namespacing, or general Redis topology refactoring.
- Do not initialize or update nested submodules.

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

- [x] Task 0: Baseline policy and code inspection (AC: #1, #2, #3, #8, #10)
  - [x] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [x] 0.2 Inspect `SignalROptions`, `SignalRServiceCollectionExtensions`, `Program.cs`, AppHost SignalR wiring, and the R10-A2 runtime proof endpoints.
  - [x] 0.3 Record whether `channelPrefix=...` already works through the existing connection string parse path and whether a first-class EventStore option exists.
  - [x] 0.4 Review deployment/publish docs for shared Redis assumptions, environment naming, and secret-redaction guidance.
  - [x] 0.5 Add a decision record before source changes choosing one primary policy path, naming boundary tuple, deploy-time owner, evidence location, residual risk, and revisit trigger.

- [x] Task 1: Document the production isolation policy (AC: #2, #3, #4, #6, #7, #10, #11)
  - [x] 1.1 Define the isolation boundary and whether Redis must be separate per boundary or may be shared with a prefix.
  - [x] 1.2 If shared Redis is allowed, define the required prefix format and examples for local, test, staging, and production.
  - [x] 1.3 Explain that prefixes protect SignalR pub/sub channel separation only and do not replace auth or query authorization.
  - [x] 1.4 Add operational evidence requirements for endpoint identity, prefix evidence, positive delivery, negative/control isolation, and fail-open behavior.
  - [x] 1.5 Add a policy decision table covering dedicated Redis per boundary, shared Redis with required prefix, and shared Redis with accepted risk.
  - [x] 1.6 Add secret-redaction rules and sanitized examples for evidence and docs.

- [x] Task 2: Implement first-class prefix support only if required (AC: #5, #9)
  - [x] 2.1 Add a narrow `BackplaneRedisChannelPrefix` option only if the decision record requires it and the existing raw configuration path cannot safely satisfy the selected policy.
  - [x] 2.2 Apply the value to `ConfigurationOptions.ChannelPrefix` before registering the Redis backplane.
  - [x] 2.3 Preserve `AbortOnConnectFail = false` exactly. **N/A under docs-only path:** no source diff under `src/Hexalith.EventStore/SignalRHub/`; the existing `SignalRServiceCollectionExtensions.ConfigureBackplane` setting is preserved trivially because nothing was changed.
  - [x] 2.4 Validate null/empty/invalid prefix values according to the selected policy without breaking local single-app no-prefix development unless the policy explicitly forbids it.
  - [x] 2.5 Keep the existing `BackplaneRedisConnectionString` and `EVENTSTORE_SIGNALR_REDIS` behavior compatible.
  - [x] 2.6 Document and test precedence or conflict behavior if both raw `channelPrefix=` and an EventStore-owned prefix option are present. **N/A under docs-only path:** no first-class `BackplaneRedisChannelPrefix` option exists, so there is no precedence-or-conflict behavior to test. Precedence between `BackplaneRedisConnectionString` and `EVENTSTORE_SIGNALR_REDIS` is documented in `docs/guides/configuration-reference.md` and `docs/operations/redis-signalr-channel-isolation.md`.

- [x] Task 3: Tests and proof guidance (AC: #7, #8, #9)
  - [x] 3.1 If source changes are made, add focused tests for option binding/validation, missing-prefix failure under required-prefix policy, accepted-risk/no-prefix behavior when selected, redaction behavior, and Redis configuration output.
  - [x] 3.2 If runtime proof evidence is touched, add only prefix/effective-config evidence needed by the chosen policy and prove the setting reaches the actual SignalR Redis backplane configuration.
  - [x] 3.3 Record different-prefix negative/control guidance without requiring a full R10-A2 proof rerun unless the source change affects runtime behavior.
  - [x] 3.4 If docs only change, run markdown/link validation when available and record the result.

- [x] Task 4: Bookkeeping and verification (AC: #9, #12)
  - [x] 4.1 Update the Dev Agent Record with decision owner/role, chosen policy, and evidence reviewed.
  - [x] 4.2 Update File List, Change Log, and Verification Status.
  - [x] 4.3 Move only this story and its sprint-status row to `review` at dev handoff.
  - [x] 4.4 At code-review signoff, move only this story and its sprint-status row to `done`.

### Review Findings

Code review run: 2026-05-03 via `bmad-code-review` (Blind Hunter, Edge Case Hunter, Acceptance Auditor). Acceptance Auditor: all 12 ACs MET. Adversarial layers found internal inconsistencies and overstated claims requiring patches before `done`.

**HIGH / MEDIUM patches:**

- [x] [Review/Patch] Precedence claim for `BackplaneRedisConnectionString` vs `EVENTSTORE_SIGNALR_REDIS` is wrong [`docs/guides/configuration-reference.md:447-451`, `docs/operations/redis-signalr-channel-isolation.md:43-45`] — Doc says "EventStore key wins because it supplies the entire connection string." Actual code is `options.BackplaneRedisConnectionString ?? Environment.GetEnvironmentVariable("EVENTSTORE_SIGNALR_REDIS")` (`SignalRServiceCollectionExtensions.cs:46-47`). The env-var path is a `??` fallback, not a peer; standard ASP.NET configuration providers (e.g. `EventStore__SignalR__BackplaneRedisConnectionString`) override both. Rewrite the precedence text to describe the actual cascade and note `ValidateSignalROptions` rejecting whitespace.
- [x] [Review/Patch] AppHost SignalR enablement is unconditional, not Blazor-gated [`src/Hexalith.EventStore.AppHost/Program.cs:110` vs `docs/operations/redis-signalr-channel-isolation.md:47-49` and Dev Agent Record line 241] — Code is `eventStore.WithEnvironment("EventStore__SignalR__Enabled", "true")` with no `if`. Correct the policy doc and Debug Log claim.
- [x] [Review/Patch] Operator decision-table cell undersells shared-Redis prefix rules [`docs/operations/redis-signalr-channel-isolation.md` operator decision table] — Row "shared-redis-with-required-channel-prefix" requires only "non-empty `channelPrefix=...`", but the Channel Prefix Requirements section mandates lowercase `a-z 0-9 . -`, ≤96 chars, env+app scoped. Tighten the cell to reference the full grammar.
- [x] [Review/Patch] "Fail-open when Redis unavailable or misconfigured" is overstated [`docs/operations/redis-signalr-channel-isolation.md:143-150`] — `AbortOnConnectFail = false` only governs runtime connection loss. `ConfigurationOptions.Parse` throws synchronously on a malformed string and crashes startup. Qualify the claim ("connection loss" only, not "misconfiguration") or document the parse-failure path.
- [x] [Review/Patch] Empty-string `channelPrefix=` silently accepted by `ConfigurationOptions.Parse` [`docs/operations/redis-signalr-channel-isolation.md` prefix requirements] — Policy says "non-empty after trim" but no startup validator enforces this for the parsed prefix. Add an explicit "no enforcement; deploy-time discipline only — empty value falls back to no prefix" warning.
- [x] [Review/Patch] Case-sensitivity not made normative [`docs/operations/redis-signalr-channel-isolation.md` prefix grammar] — "lowercase" reads as style. Redis pub/sub channel names are byte-exact, so `hesr.PROD…` and `hesr.prod…` are different boundaries. State this explicitly as correctness, not style.
- [x] [Review/Patch] AC #11 redaction guidance does not name `ConfigurationOptions.ToString()` in the published policy [`docs/operations/redis-signalr-channel-isolation.md` redaction] — Spec AC #11 calls it out; the published doc only lists generic "no JWT/bearer/connection string password". Carry the specific API name forward, since the existing R10-A2 runtime proof emits this output.
- [x] [Review/Patch] Story Tasks 2.6 (`Document and test precedence or conflict behavior`) and 2.3 (`Preserve AbortOnConnectFail = false exactly`) are `[x]` despite no source change and no SignalR test run — Annotate both as N/A under the docs-only path with rationale (no first-class option exists → no precedence to test; no source diff under `src/SignalRHub/` → preserved trivially).

**LOW patches:**

- [x] [Review/Patch] Tenant-lane example `hesr.prod.eventstore.tenant-lane-a` collides with the cluster slot in the format `hesr.<env>.<app>[.<cluster>][.<lane>]` [`docs/operations/redis-signalr-channel-isolation.md` examples] — Position 4 is documented as `cluster-or-namespace-alias`; example uses tenant-lane there with no cluster. Either make slots explicitly delimited or change the example.
- [x] [Review/Patch] 96-char cap unsourced [`docs/operations/redis-signalr-channel-isolation.md` prefix grammar] — No reference to a Redis or SignalR limit. Cite source, drop the cap, or downgrade to advisory.
- [x] [Review/Patch] "Affected environments: Local development …" contradicts the local single-app exception in the same doc [`docs/operations/redis-signalr-channel-isolation.md` Decision Record] — Refine wording to "boundaries covered (where prefix discipline applies)".
- [x] [Review/Patch] `docs/index.md` routes the new policy under `## Guides` though target path is `operations/...` and ordering breaks [`docs/index.md:42`] — Either add a `## Operations` section or relocate the doc to `docs/guides/`.
- [x] [Review/Patch] Story Decision Record has duplicated `Redis Isolation Decision Evidence` line right under H3 [`post-epic-10-r10a7-redis-channel-isolation-policy.md` Dev Agent Record §Decision Record] — Paste artifact; remove the redundant line.
- [x] [Review/Patch] ACA / Kubernetes shared-Redis examples embed `password=<redacted>` literal inside connection-string copy-paste payloads [`docs/guides/deployment-azure-container-apps.md:302`, `docs/guides/deployment-kubernetes.md:404`] — Operators copy-paste secret payloads; mixing redaction syntax with literal example syntax is a footgun. Switch to `password=$REDIS_PASSWORD` or annotate the literal as a placeholder.
- [x] [Review/Patch] Verification Status note "passed after escaping a pre-existing inline placeholder" is untraceable [story §Verification Status] — Name the markdownlint rule id (e.g. MD033/MD038) and the file:line that was escaped.
- [x] [Review/Patch] Accepted-risk row promises "owner-approved exception record" with no schema or location [`docs/operations/redis-signalr-channel-isolation.md` operator decision table] — Specify where the exception record lives and required fields.
- [x] [Review/Patch] Normative policy uses "should/may" in places where the title is "Policy" [`docs/operations/redis-signalr-channel-isolation.md` various] — Upgrade to MUST/MUST NOT or rename the doc to "Guidance".
- [x] [Review/Patch] `EventStore:SignalR` options table omits `MaxGroupsPerConnection` constraints and the empty-string rejection in `ValidateSignalROptions` [`docs/guides/configuration-reference.md` SignalR options table] — Operators trying `BackplaneRedisConnectionString=""` to disable the backplane will hit an undocumented startup failure.
- [x] [Review/Patch] Decision table requires "collision check" evidence with no procedure [`docs/operations/redis-signalr-channel-isolation.md` decision table] — Add a 1-2-line algorithm (prefix registry, MONITOR/CLIENT LIST inspection, etc.).
- [x] [Review/Patch] "Local single-app development" carve-out never defined [`docs/operations/redis-signalr-channel-isolation.md` exemptions] — Distinguish single AppHost on dev box from parallel local instances sharing the same local Redis.
- [x] [Review/Patch] Tenant-prefix examples (`tenant-lane-a`) invite real tenant-ID leak [`docs/operations/redis-signalr-channel-isolation.md` examples] — Add explicit "MUST NOT use real tenant identifiers" warning aligned with AC #11 redaction rules.
- [x] [Review/Patch] Deferred-work entry for `BackplaneRedisChannelPrefix` does not name `SignalROptions`/`ValidateSignalROptions` or restate AC #5 precedence-fail invariant [`_bmad-output/implementation-artifacts/deferred-work.md` R10-A7 entry] — A future implementer reading the entry alone cannot reconstruct the AC #5 invariant.

**Deferred (not blockers; pre-existing or out of docs-only scope):**

- [x] [Review/Defer] Sprint-status `last_updated` does not advance same-day [`sprint-status.yaml`] — repo-wide timestamp-precision convention question.
- [x] [Review/Defer] Lifecycle `ready-for-dev → review` skipped a recorded `in-progress` transition in YAML [`sprint-status.yaml`] — repo-wide audit-trail concern, already flagged on a prior story.
- [x] [Review/Defer] No automated check that canonical policy literals appear in the canonical location [`docs/operations/redis-signalr-channel-isolation.md`] — out of docs-only scope; tooling effort.
- [x] [Review/Defer] Multi-AppHost CI lane same-prefix collision derivation policy [`docs/operations/redis-signalr-channel-isolation.md` boundary tuple] — needs design (env var? hash? lane-id) before encoding into the doc.

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

### Policy Wording Requirements

The implementation and docs must use one of these production decisions exactly:

- `separate-redis-per-isolation-boundary`
- `shared-redis-with-required-channel-prefix`
- `shared-redis-with-accepted-risk`

An isolation boundary means any scope where projection-change notifications must not cross: environment, deployed app/product instance, cluster or namespace when relevant, tenant when Redis is shared across tenant-isolated runtimes, and test/CI lane when Redis is shared across parallel runs. The decision must state whether tenant identity belongs in the Redis channel-prefix boundary or remains solely an application authorization boundary.

When shared Redis is allowed, the channel prefix must be non-empty outside local single-app development and must distinguish every shared isolation boundary. At minimum it must include environment and app identity. It must also include tenant or test-lane identity when Redis is intentionally shared across those scopes. Prefix examples in docs must be sanitized and must not expose secrets or sensitive topology.

Use this minimum evidence template in the story completion notes or linked artifact:

```text
Redis Isolation Decision Evidence
- Date:
- Decision:
- Decision owner/role:
- Isolation boundaries covered:
- Redis topology:
- Channel prefix requirement:
- Prefix source/config key:
- Deploy-time owner:
- Redaction note:
- Validation performed:
- Residual risk:
- Revisit trigger:
```

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
- A positive-only runtime proof is insufficient for shared Redis isolation. When live proof is practical, include a negative/control case with differently configured boundaries sharing Redis; otherwise record why configuration/unit proof is the selected gate.
- If validating redaction in code is not practical, require a review checklist item proving evidence artifacts do not contain raw Redis passwords, tokens, credentials, sensitive hostnames, or unsafe connection-string output.

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

GPT-5 Codex

### Decision Record

- Date: 2026-05-03
- Decision: `separate-redis-per-isolation-boundary`
- Decision owner/role: Platform architect / production DevOps owner
- Isolation boundaries covered: environment, deployed application/product instance, cluster or namespace when relevant, tenant or tenant lane when Redis is intentionally shared across tenant-isolated runtimes, and test/CI lane when Redis is shared by parallel runs.
- Redis topology: Production SignalR Redis backplane MUST use a dedicated Redis deployment per isolation boundary. Shared Redis is an exception path only and requires a non-empty StackExchange.Redis `channelPrefix` in the existing `EventStore:SignalR:BackplaneRedisConnectionString` / `EVENTSTORE_SIGNALR_REDIS` connection string.
- Channel prefix requirement: Not required for local single-app development or dedicated-per-boundary Redis. Required for any approved shared Redis exception outside local single-app development.
- Prefix source/config key: Existing Redis connection string `channelPrefix=...` parsed by `StackExchange.Redis.ConfigurationOptions.Parse`; no EventStore-specific `BackplaneRedisChannelPrefix` option is added.
- Deploy-time owner: The environment/platform owner that provisions Redis and supplies EventStore runtime configuration.
- Redaction note: Evidence must redact credentials, connection strings, unsafe hostnames, tokens, and sensitive tenant identifiers; sanitized prefix values may be recorded because they are configuration metadata, not credentials.
- Validation performed: Inspected current SignalR options/backplane wiring, AppHost SignalR enablement, runtime proof endpoints, deployment docs, Microsoft SignalR Redis backplane guidance, and StackExchange.Redis `channelPrefix` support.
- Residual risk: Shared Redis remains dependent on deploy-time configuration discipline; a missing or colliding prefix on a shared Redis exception can leak SignalR pub/sub messages across SignalR apps using that same Redis server.
- Revisit trigger: Add a first-class EventStore prefix option or startup validation if production deployments repeatedly require shared Redis, if prefix conflicts are observed, or if operators need separate policy enforcement outside the raw Redis connection string.

### Debug Log References

- Baseline: current HEAD `56f07b75d713c0eb94e329d71ebeac720278eb1e`; sprint row confirmed `ready-for-dev`, then moved to `in-progress`.
- Aspire baseline: repo AppHost was already running under `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; Aspire MCP resource list showed the EventStore, sample, admin, Keycloak, state store, and pub/sub resources running and healthy before docs work.
- Code inspection: `SignalROptions` exposes `Enabled`, `BackplaneRedisConnectionString`, and `MaxGroupsPerConnection`; no EventStore-owned channel-prefix option exists.
- Code inspection: `ConfigureBackplane()` resolves `BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS`, parses the full StackExchange.Redis connection string, and preserves `AbortOnConnectFail = false`.
- AppHost inspection: `src/Hexalith.EventStore.AppHost/Program.cs:110` unconditionally sets `EventStore__SignalR__Enabled=true` on the EventStore resource (the line is wired alongside the Blazor UI sample but is not gated on its presence). The AppHost does not provision or configure a Redis SignalR backplane by default.
- Documentation validation: `npx --yes markdownlint-cli2` passed for all changed docs; `markdown-link-check` passed for the new policy page, configuration reference, deployment guides, docs index, and SignalR operational evidence page.
- Regression: standard unit-test projects passed individually with `--no-build`: Client (334), Contracts (281), Sample (63), Testing (78).

### Completion Notes List

- Selected primary production policy `separate-redis-per-isolation-boundary` and documented shared Redis as an approved-exception path requiring `channelPrefix=...` through the existing StackExchange.Redis connection string.
- Added the canonical Redis SignalR channel isolation policy under `docs/operations/`, including boundary tuple, prefix format, sanitized examples, operator decision table, redaction rules, runtime evidence requirements, and first-class option revisit criteria.
- Kept source configuration unchanged: no `BackplaneRedisChannelPrefix` option was required because `ConfigurationOptions.Parse(...)` already supports `channelPrefix=...`, and dedicated Redis per boundary is the primary policy.
- Updated configuration, Docker Compose, Kubernetes, Azure Container Apps, docs index, and SignalR evidence docs with narrow links/routing notes.
- Added deferred-work tracking for the postponed first-class EventStore prefix option.

### File List

- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/post-epic-10-r10a7-redis-channel-isolation-policy.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/guides/configuration-reference.md`
- `docs/guides/deployment-azure-container-apps.md`
- `docs/guides/deployment-docker-compose.md`
- `docs/guides/deployment-kubernetes.md`
- `docs/index.md`
- `docs/operations/redis-signalr-channel-isolation.md`
- `docs/operations/signalr-operational-evidence.md`

## Party-Mode Review

- Date/time: 2026-05-02T19:13:13+02:00
- Selected story key: `post-epic-10-r10a7-redis-channel-isolation-policy`
- Command/skill invocation used: `/bmad-party-mode post-epic-10-r10a7-redis-channel-isolation-policy; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect), Paige (Technical Writer)
- Findings summary:
  - Winston: The policy branch needed a canonical isolation-boundary definition, explicit shared-prefix semantics, accepted-risk gating, and sharper exclusions from broader Redis/auth/topology work.
  - Amelia: Configuration precedence was the main implementation trap; first-class prefix support must be narrow, explicit, and tested without broad AppHost or Redis redesign.
  - Murat: The story risked false confidence unless evidence includes redacted endpoint/prefix posture and, where practical, negative/control isolation proof rather than positive delivery only.
  - Paige: Operator-facing docs needed a decision table, sanitized prefix examples, and a concrete decision-evidence template.
- Changes applied:
  - Strengthened acceptance criteria for dated policy decision evidence, boundary tuple definition, shared-prefix shape, deploy-time ownership, accepted-risk signoff, first-class option scope, precedence/conflict behavior, and redaction.
  - Added scope boundaries excluding broad Redis infrastructure redesign, tenant routing, hub restructuring, DAPR/Auth changes, topology refactors, and nested submodule updates.
  - Added task guidance for policy decision table, evidence template, missing-prefix validation, conflict handling, redaction review, and negative/control proof when practical.
  - Added policy wording requirements and evidence template to Dev Notes.
- Findings deferred:
  - The exact production policy choice and exact prefix format remain for `bmad-dev-story`; this pre-dev review requires the decision to be recorded before source changes but does not choose it.
  - Whether tenant identity belongs in the Redis prefix remains a topology decision to document explicitly; the story now requires that analysis rather than assuming tenant-scoped prefixes.
  - Live Redis/SignalR negative proof remains conditional on an existing lightweight fixture; otherwise focused configuration/unit tests plus documented evidence are acceptable.
- Final recommendation: `ready-for-dev`

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-03 | 1.1 | Code-review pass: applied 22 patches (1 HIGH precedence-rewrite + 7 MEDIUM + 14 LOW) addressing internal contradictions, normative wording, fail-open qualification, `ConfigurationOptions.ToString()` redaction warning, accepted-risk record schema, collision-check procedure, lane-id derivation, tenant-prefix safety, and AppHost claim correction. Story moved to `done`. | Claude Opus 4.7 |
| 2026-05-03 | 1.0 | Implemented Redis SignalR channel isolation policy docs, accepted raw connection-string `channelPrefix` path, deferred first-class option, and moved story to review. | GPT-5 Codex |
| 2026-05-02 | 0.2 | Party-mode review hardened Redis isolation decision, prefix, evidence, and test constraints. | Codex automation |
| 2026-05-02 | 0.1 | Created ready-for-dev R10-A7 Redis channel isolation policy story. | Codex automation |

## Verification Status

Implementation complete and code-review patches applied. Docs/policy-only path selected; no source configuration changes were required.

### Initial dev-handoff verification

- `npx --yes markdownlint-cli2 "docs/operations/redis-signalr-channel-isolation.md" "docs/guides/configuration-reference.md" "docs/guides/deployment-docker-compose.md" "docs/guides/deployment-kubernetes.md" "docs/guides/deployment-azure-container-apps.md" "docs/operations/signalr-operational-evidence.md" "docs/index.md"` — passed.
- `npx --yes markdownlint-cli2 "_bmad-output/implementation-artifacts/deferred-work.md"` — passed after wrapping the pre-existing `(merged <date>)` placeholder in backticks (markdownlint rule `MD033/no-inline-html`, formerly `_bmad-output/implementation-artifacts/deferred-work.md:283`, now line 287 after the R10-A7 entry was prepended).
- `npx --yes markdown-link-check` passed for `docs/operations/redis-signalr-channel-isolation.md`, `docs/guides/configuration-reference.md`, `docs/guides/deployment-docker-compose.md`, `docs/guides/deployment-kubernetes.md`, `docs/guides/deployment-azure-container-apps.md`, `docs/operations/signalr-operational-evidence.md`, and `docs/index.md`.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-build` — 334 passed.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-build` — 281 passed.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --no-build` — 63 passed.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --no-build` — 78 passed.

### Code-review patch pass (2026-05-03)

22 patches applied from the `bmad-code-review` Review Findings (1 HIGH precedence-rewrite, 7 MEDIUM, 14 LOW). 4 items deferred to repo-wide tooling/convention work and recorded in `deferred-work.md`. 0 items dismissed.

- `npx --yes markdownlint-cli2 "docs/operations/redis-signalr-channel-isolation.md" "docs/guides/configuration-reference.md" "docs/guides/deployment-docker-compose.md" "docs/guides/deployment-kubernetes.md" "docs/guides/deployment-azure-container-apps.md" "docs/operations/signalr-operational-evidence.md" "docs/index.md"` — re-ran after patches: 0 errors.
- `npx --yes markdownlint-cli2 "_bmad-output/implementation-artifacts/deferred-work.md"` — re-ran after the tightened R10-A7 deferred-work entry: 0 errors.
- `npx --yes markdown-link-check` re-run on all 7 touched docs: all internal anchors and external references resolve (new anchors `#local-single-app-development-defined`, `#required-prefix-shape-non-local-shared-redis`, `#lane-id-derivation`, `#source-change-policy` confirmed).
- No source code under `src/` was modified during the patch pass; the existing Tier 1 unit-test pass from the initial dev-handoff verification stands.
