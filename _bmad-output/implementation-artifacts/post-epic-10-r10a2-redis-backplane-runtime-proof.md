# Post-Epic-10 R10-A2: Redis Backplane Runtime Proof

Status: done

<!-- Source: epic-10-retro-2026-05-01.md R10-A2 -->
<!-- Source: sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md Proposal 2 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer validating production SignalR readiness**,
I want a runtime proof that a Redis-backed SignalR backplane delivers projection-change signals across multiple EventStore instances,
So that FR56 multi-instance real-time notification confidence is based on observed delivery rather than DI wiring alone.

## Story Context

Story 10.2 verified the Redis backplane configuration path and DI activation. It proved `AddStackExchangeRedis()` wiring, `EVENTSTORE_SIGNALR_REDIS` fallback, fail-open startup behavior through `AbortOnConnectFail = false`, and single-instance fallback behavior. It did not prove that a client connected to one EventStore instance receives a `ProjectionChanged` signal broadcast from another EventStore instance through Redis.

The Epic 10 retrospective records this as R10-A2: Redis backplane delivery is structurally configured but not proven in a multi-instance runtime. This story closes that confidence gap with a Docker/Aspire-equipped Tier 3 proof. The proof must capture topology, Redis configuration, instance endpoints, the client connection target, the broadcast origin, and notification receipt evidence.

Current HEAD at story creation: `11298a5`.

## Acceptance Criteria

1. **A Redis-backed multi-instance topology is started deliberately.** Run a topology with SignalR enabled, `EventStore:SignalR:BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS` set to a real Redis endpoint, and at least two independently hosted EventStore server instances using the same backplane. Record the exact topology shape, Redis endpoint source, EventStore resource/process/container names, instance IDs if available, process IDs or equivalent runtime identities, and both EventStore instance endpoints.

2. **The test client connects to one specific instance.** Create or use a SignalR client that connects directly to instance B's concrete `/hubs/projection-changes` endpoint, joins a single `{projectionType}:{tenantId}` group, and records the actual hub URL used. Do not route this client through Aspire service discovery, a reverse proxy, or a load balancer that could hide which instance owns the connection.

3. **The broadcast originates from a different instance.** Trigger ETag regeneration and SignalR broadcast from instance A's concrete endpoint or from a test-only instance-A hook. The proof must show the command/request path or explicit server-side broadcast path used to make instance A the origin. If Aspire service discovery or a reverse proxy prevents deterministic origin selection, implement a narrowly scoped test-only deterministic route or evidence hook rather than accepting ambiguous evidence. Any hook must be unavailable in production, must not change the public hub payload, and must be documented in the evidence.

4. **Cross-instance delivery is observed.** The client connected to instance B receives exactly the expected signal for the joined projection/tenant after the instance A broadcast. The proof must include the received `projectionType`, `tenantId`, timestamp, connection target, broadcast origin, correlation/run id, and wait duration. Because the public hub payload intentionally contains only `projectionType` and `tenantId`, the proof must prevent stale-message false positives by using a unique projection/tenant pair per run when the chosen trigger path allows it, or by explicitly draining/resetting the client receive buffer and proving no pre-trigger message was observed before the instance A broadcast. Duplicate signals are allowed only if the evidence explains an at-least-once source; the primary assertion is that at least one valid cross-instance signal arrives within a bounded wait of no more than 10 seconds.

5. **The client re-query behavior is proven or explicitly bounded.** After receiving the signal, the proof either performs the expected projection re-query and records the response/ETag evidence, or records a bounded reason why this story only proves signal transport and leaves query refresh evidence to `post-epic-11-r11a3-apphost-projection-proof` / `post-epic-11-r11a4-valid-projection-round-trip`. Do not silently omit this part.

6. **Redis/backplane evidence is concrete.** Capture evidence that the SignalR backplane is actually enabled in the runtime topology for both EventStore instances before the positive broadcast is triggered. Acceptable evidence includes the Aspire Redis resource name or Redis endpoint, runtime config dump from both instances, logs showing Redis backplane startup, DI/type evidence from the running instances, Redis pub/sub observation, or equivalent evidence. The proof must also record each instance's runtime identity and backplane readiness signal; a static code citation alone does not satisfy this AC.

7. **Fail-open and local-only false positives are excluded.** The proof must include mandatory negative/control evidence showing no valid cross-instance delivery within the same bounded wait when the Redis backplane is disabled or isolated/unreachable, and must fail if the client is accidentally connected to the same instance that originated the broadcast. A single-process, single-instance, same-process multiple-hub, or shared-DI-container proof does not satisfy this story.

8. **SignalR group and payload contract are pinned.** The proof uses one explicit projection/tenant pair and records the exact joined group string. If the proof uses a run-unique projection/tenant pair, the run id must be encoded only in those existing fields or in evidence metadata, never as an added hub payload field. The only public hub payload remains `ProjectionChanged(projectionType, tenantId)`: no projection state, ETags, aggregate IDs, command status, correlation/run id, domain event body, PII, or serialized aggregate/read-model state may be added to the hub message for this story.

9. **Operator-facing failure behavior is recorded.** If Redis is unavailable or misconfigured, command/query processing and local single-instance operation remain fail-open. Evidence must capture the observable warning or diagnostic signal, whether the app starts, and whether local-only SignalR behavior is intentionally out of scope for cross-instance proof.

10. **Tier 3 execution evidence is saved.** Save a concise evidence file under `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/` with a dated filename such as `evidence-YYYY-MM-DD.md`. The file must include test run id, timestamp, git commit, Aspire/apphost command or manual command, Redis resource/container identity and endpoint, EventStore instance A/B endpoints and runtime identities, client target instance, action origin instance, joined group, expected payload, observed payload, wait duration, positive result, negative/control result, relevant log excerpts or links, and Docker/DAPR/Aspire constraints if the proof cannot run in the current environment.

11. **Reproduction lane is explicit.** The story records whether the proof runs in normal CI, a Docker-gated integration lane, a nightly/manual Tier 3 lane, or a local reproduction path. It must include the exact command(s), required services, port isolation expectations, and cleanup expectations.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and both `last_updated` fields in `sprint-status.yaml` name R10-A2 and the result. At code-review signoff, both become `done`.

## Scope Boundaries

- Do not redesign the SignalR hub protocol, group format, projection notification contract, or ETag actor contract.
- Do not implement tenant-aware `JoinGroup` authorization here; that is `post-epic-10-r10a3-hub-group-authorization-decision`.
- Do not change client reconnect behavior; that is Story 10.3 and follow-up R10-A5.
- Do not make local single-instance Aspire dev depend on Redis by default.
- Do not weaken fail-open behavior. SignalR broadcast failure must not break ETag regeneration or command processing.
- Do not treat DI wiring tests as sufficient runtime proof. They remain useful regression tests, but R10-A2 needs a real cross-instance delivery observation.
- Do not change authorization, tenant isolation, projection naming, group naming, or public SignalR payload semantics. If deterministic proof requires product behavior changes outside test-only diagnostics or runtime topology, stop and update the story instead of absorbing the change during implementation.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| SignalR DI/backplane wiring | `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs` | Verify and reuse `BackplaneRedisConnectionString`, env var fallback, and `AddStackExchangeRedis` with `AbortOnConnectFail = false` |
| Hub group behavior | `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs` | Join instance-B client to `{projectionType}:{tenantId}` group; preserve max group and colon validation |
| SignalR broadcast path | `src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs` | Confirm group broadcast payload and fail-open warning behavior |
| Hub endpoint mapping | `src/Hexalith.EventStore/Program.cs` | Confirm `/hubs/projection-changes` is mapped only when SignalR is enabled |
| AppHost topology | `src/Hexalith.EventStore.AppHost/Program.cs` | Extend or test around Aspire topology without forcing Redis backplane into default local dev |
| Existing backplane tests | `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRBackplaneWiringTests.cs` | Keep as structural regression coverage; do not confuse with runtime proof |
| Tier 3 Aspire fixture pattern | `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs` | Reuse environment snapshot, no-Keycloak setup, health waits, and resilient HTTP defaults |
| Docker/Redis proof precedent | `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs` | Reuse Redis/Docker environment discipline and evidence style where applicable |
| Pub/Sub evidence pattern | `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs` | Model bounded polling, exact evidence capture, and no silent infrastructure assumptions |
| SignalR client helper | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` | Prefer this helper if it can target a specific instance URL and expose receipt callbacks cleanly |

## Tasks / Subtasks

- [x] Task 0: Baseline and proof design (AC: #1, #2, #3, #7, #8, #11)
  - [x] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [x] 0.2 Decide the proof topology: two EventStore processes in one Aspire test, two explicit local `dotnet run` processes, or a containerized composition. Record why the chosen path gives deterministic instance A/B evidence.
  - [x] 0.3 Identify how each EventStore instance gets the same Redis backplane connection string.
  - [x] 0.4 Identify how the client will connect directly to instance B without load-balancer ambiguity.
  - [x] 0.5 Identify how the broadcast will originate from instance A without relying on chance routing.
  - [x] 0.6 Pick the exact proof pair, for example `projectionType=counter` and `tenantId=tenant-a`, and record the joined group string. Prefer a run-unique pair when the selected trigger path can support it; otherwise record the client-buffer drain/reset step used to exclude stale messages.
  - [x] 0.7 Decide the CI/manual execution lane, command line, required services, port isolation, and cleanup expectations before writing the harness.

- [x] Task 1: Build or configure the multi-instance proof harness (AC: #1, #2, #3, #6, #7, #9, #11)
  - [x] 1.1 Add a Tier 3 test fixture or manual proof harness that starts or targets two EventStore instances.
  - [x] 1.2 Ensure both instances have `EventStore__SignalR__Enabled=true`.
  - [x] 1.3 Ensure both instances have the same real Redis backplane endpoint through `EventStore__SignalR__BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS`.
  - [x] 1.4 Capture both instance base URLs, resource/process/container names, process IDs or equivalent runtime identities, and the Redis endpoint used by the backplane.
  - [x] 1.5 Add mandatory negative/control paths that fail the proof if only one instance is present, if the backplane setting is absent, if Redis is disabled/isolated/unreachable, if the client target equals the broadcast origin, or if a matching message arrives before the intentional instance-A trigger.
  - [x] 1.6 If a test-only endpoint or service hook is required, gate it to Development/Test configuration and record why it is not available in production.
  - [x] 1.7 Preserve fail-open command/query behavior when Redis is unavailable and capture the warning/diagnostic evidence.
  - [x] 1.8 Add a readiness gate that waits for both instances to report SignalR enabled, the same Redis backplane endpoint, and a distinct runtime identity before the client joins the group.

- [x] Task 2: Connect the instance-B SignalR client (AC: #2, #4, #8)
  - [x] 2.1 Create a SignalR client targeting instance B's exact hub URL.
  - [x] 2.2 Subscribe to `ProjectionChanged(projectionType, tenantId)` and capture received payloads with timestamps.
  - [x] 2.3 Join the intended group, using the same projection type and tenant that the broadcast origin will use.
  - [x] 2.4 Assert the public payload is exactly the signal-only `projectionType` and `tenantId` shape; do not add state, ETags, aggregate IDs, command status, event body, or PII.
  - [x] 2.5 Wait for the join call to complete before triggering the instance A broadcast.

- [x] Task 3: Trigger the instance-A broadcast (AC: #3, #4, #5)
  - [x] 3.1 Prefer a real command/projection flow if deterministic instance A routing is available.
  - [x] 3.2 If deterministic command routing is not available, add a narrowly scoped test-only endpoint or fixture-only service hook that invokes `IProjectionChangedBroadcaster` in instance A with the target projection/tenant.
  - [x] 3.3 If using a test-only hook, guard it so it is unavailable in production, does not bypass normal hub group semantics, records the instance-A runtime identity, and explains why it does not change product behavior.
  - [x] 3.4 After broadcast, wait for the instance-B client receipt with a bounded timeout no longer than 10 seconds.
  - [x] 3.5 Perform the projection re-query or explicitly record why query refresh is bounded to sibling projection-proof stories.

- [x] Task 4: Assert and record evidence (AC: #4, #6, #7, #8, #9, #10)
  - [x] 4.1 Assert the received payload has the expected `projectionType` and `tenantId`.
  - [x] 4.2 Assert the client connection target is instance B and the broadcast origin is instance A.
  - [x] 4.3 Assert the proof used a real Redis backplane endpoint and not only the default local hub lifetime manager; include both-instance runtime evidence gathered after startup, not only static configuration.
  - [x] 4.4 Record Redis/backplane startup evidence from logs, config, runtime introspection, or Redis observation.
  - [x] 4.5 Record negative/control outcomes for disabled or isolated/unreachable Redis, accidental same-instance routing, and pre-trigger stale-message detection.
  - [x] 4.6 Save an evidence markdown file under `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/` using the required dated schema from AC #10.

- [x] Task 5: Verification gates (AC: #1-#12)
  - [x] 5.1 Run the targeted Tier 3 proof on a Docker/DAPR/Aspire-equipped host.
  - [x] 5.2 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.
  - [x] 5.3 If source or test files changed, run the relevant focused test project(s) individually.
  - [x] 5.4 If Docker, DAPR placement/scheduler, Redis, or local port constraints block the proof, record the exact blocker in the evidence file and leave the story in `review` only if code is complete but environment proof is pending; otherwise keep it `in-progress`.

- [x] Task 6: Story bookkeeping (AC: #12)
  - [x] 6.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [x] 6.2 Move this story and only this story from `ready-for-dev` to `review` at dev handoff.
  - [x] 6.3 Leave R10-A3/R10-A5/R10-A6/R10-A7/R10-A8 status rows unchanged.

### Review Findings

Code review date: 2026-05-02. Three parallel layers (Blind Hunter, Edge Case Hunter, Acceptance Auditor). 73 raw findings → 2 decision-needed, 15 patch, 12 deferred, ~22 dismissed.

#### Decision-needed (resolved)

- [x] [Review][Decision] AC#7 isolated/unreachable Redis cross-instance control — **Resolved**: spec uses "disabled OR isolated/unreachable"; the disabled-case control already excludes the cross-instance false-positive class. The isolated/unreachable branch is scoped to AC#9 (fail-open), not duplicated as a cross-instance control. Documented in evidence file (Negative Controls section).
- [x] [Review][Decision] AC#7 same-instance routing exercised control — **Resolved**: runtime guards on lines 49/79/80 already force the test to fail on accidental same-instance routing. No separate exercised control is added; the guards are documented in the evidence file (Negative Controls section) so reviewers can see the spec's "must fail" requirement is satisfied structurally.

#### Patch

- [x] [Review][Patch] Wrap test body in try/finally so evidence file is saved even on assertion failure [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:36-128]
- [x] [Review][Patch] Replace tautological `PingAsync() >= TimeSpan.Zero` with explicit success check (PingAsync throws on failure; the `>= Zero` comparison can never fail) [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:138]
- [x] [Review][Patch] Broaden `AssertRedisAvailable` catch from `RedisConnectionException` to a wider Redis exception or general `Exception` so timeout/socket/argument failures surface with the same diagnostic [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:130-144]
- [x] [Review][Patch] Use `DateTimeOffset.UtcNow` instead of `DateTimeOffset.Now` for evidence filename and Timestamp; runId uses UtcNow but filename/Timestamp use local time, causing DST/cross-machine inconsistency [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:375,383]
- [x] [Review][Patch] Assert `disabledA.BroadcastAsync(...)` returned a successful response before checking no signal — currently `_ = await ...` silently passes if the broadcast itself failed [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:111]
- [x] [Review][Patch] Assert `unreachableRedis.BroadcastAsync(...)` succeeded (or record the failure mode explicitly) — currently `_ = await ...` discards the result [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:123]
- [x] [Review][Patch] Make Redis endpoint configurable via env var (e.g., `R10A2_REDIS`) with `localhost:6379` fallback so concurrent test runs / dual checkouts don't collide on a shared Redis [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:24]
- [x] [Review][Patch] Validate request body in `/broadcast` and return `BadRequest` for null body or whitespace fields instead of letting `ArgumentException.ThrowIfNullOrWhiteSpace` produce 500 [src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs:42-54]
- [x] [Review][Patch] Redact credentials in `/identity`'s `BackplaneRedisConnectionString` field (or mask password component) — production-shaped configs may include `password=...` [src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs:38,71-79]
- [x] [Review][Patch] Capture EventStore log excerpts (Redis backplane startup lines, broadcast warnings) into evidence Diagnostics section — currently only configuration is echoed [evidence file Diagnostics block + ReadLogs at tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:318-331]
- [x] [Review][Patch] Extend evidence Environment section with Docker prerequisite, port-isolation rule (uses `GetFreeTcpPort`), and CI gating note [_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-05-02-150535.md:9-13]
- [x] [Review][Patch] Capture observable warning/diagnostic when `unreachableRedis.BroadcastAsync` runs (AC#9 demands the warning text, not just HTTP 200) [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:121-124]
- [x] [Review][Patch] Reference `EventStore:SignalR:RuntimeProof:Enabled` configuration key explicitly in evidence Configuration section so reviewers can verify the production-disabled posture without reading source [evidence file Configuration block]
- [x] [Review][Patch] Move `app.MapSignalRRuntimeProofEndpoints()` inside the `if (signalROptions?.Enabled == true)` block in Program.cs for symmetry with public hub mapping; the inner two-layer gate (Dev + flag) already prevents production exposure but call-site symmetry is defense-in-depth [src/Hexalith.EventStore/Program.cs:51-55]
- [x] [Review][Patch] Remove tautological `waitDuration.ShouldBeLessThanOrEqualTo(PositiveWait)` — `WaitForSignalAsync` already throws on timeout, so successful return implies wait < PositiveWait [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:81]

#### Deferred

- [x] [Review][Defer] AC#8 explicit hub payload arity assertion — `connection.On<string,string>` only validates arity 2; SignalR client silently drops extra args [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:63] — deferred, requires hub-protocol-level instrumentation outside this story's scope
- [x] [Review][Defer] /identity does not introspect `RedisHubLifetimeManager<ProjectionChangedHub>` DI registration; verifies config only, not runtime backplane wiring [src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs:26-39] — deferred, evidence-strengthening enhancement
- [x] [Review][Defer] dotnet run --no-build fragility across Debug/Release configurations [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:185-195] — deferred, current build process handles correctly
- [x] [Review][Defer] GetFreeTcpPort race window between `listener.Stop()` and `Process.Start` [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:270-276] — deferred, small window in practice
- [x] [Review][Defer] Process.Kill races with WaitForExitAsync; tree-kill may leak orphaned children [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:235-250] — deferred, 10s fallback exists
- [x] [Review][Defer] Six EventStore processes spawned with no max concurrency throttle [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:38-120] — deferred, works on current dev/CI machines
- [x] [Review][Defer] WaitForReadyAsync 45s timeout may be insufficient on slow CI runners [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:292] — deferred, current envs handle it
- [x] [Review][Defer] `Environment.Remove` only clears two keys; other inherited `EventStore__*` env vars may leak into child processes [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:211-213] — deferred, defensive cleanup
- [x] [Review][Defer] Unreachable Redis port `localhost:6390` may be in use by another local service [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:119] — deferred, low collision probability
- [x] [Review][Defer] ReadLogs `TakeLast(30)` enumerates entire `ConcurrentQueue` every call; queue is unbounded [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:318-331] — deferred, tests are short-lived
- [x] [Review][Defer] HubConnection without `WithAutomaticReconnect()` and `connection.StartAsync` without cancellation token [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:60-64] — deferred, deterministic test behavior preferred
- [x] [Review][Defer] `broadcast.ProcessId.ShouldBe(identityA.ProcessId)` doesn't prove path went through Redis — payload carries no origin tag [tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs:79] — deferred, mitigation is captured in patch P10 (log capture) and P-DI introspection defer

## Dev Notes

### Architecture Guardrails

- The SignalR broadcast remains signal-only: `ProjectionChanged(projectionType, tenantId)`. Do not send projection state, ETags, aggregate IDs, or command status through the hub.
- SignalR groups use `{projectionType}:{tenantId}`. That matches the ETag/projection-change scope and relies on colon validation in both client and server paths.
- The Redis backplane is production distribution infrastructure. Local default AppHost behavior may remain single-instance with no backplane requirement.
- Redis backplane failures are fail-open for command/query processing. The proof may fail when Redis is absent, but the product behavior must not start throwing command failures because SignalR cannot publish.
- A DAPR-managed Redis instance may be reused, but production may also use a dedicated Redis to avoid DAPR state/pubsub contention.
- AppHost changes must preserve existing Aspire conventions. Avoid persistent containers for this proof unless the implementation records why state persistence is required and how cleanup is handled.
- Prefer an Aspire/AppHost-managed Redis resource or the app model's existing Redis wiring for the proof. An ad hoc external Redis server is acceptable only if the evidence records why Aspire wiring could not provide deterministic A/B proof.
- SignalR remains a notification mechanism, not a data consistency mechanism. Clients must re-query bounded read models after a signal and must tolerate missed, duplicated, or delayed signals; this story may prove transport without claiming replay or catch-up semantics.
- The proof must correlate a received signal to the current run without expanding the public hub payload. Prefer run-unique projection/tenant values when valid for the chosen harness; otherwise drain the client receive buffer before triggering and record the zero-message pre-trigger observation.
- Runtime identity evidence must distinguish process/resource identity from logical hub group identity. A proof that cannot demonstrate instance A and instance B are distinct processes/resources must fail as ambiguous rather than claiming Redis backplane delivery.
- If Redis pub/sub channel names, database index, or channel prefix are observable, record them as evidence only. Do not decide the broader Redis channel isolation policy here; that remains scoped to `post-epic-10-r10a7-redis-channel-isolation-policy`.

### Current-Code Intelligence

- `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs` currently enables the Redis backplane only when SignalR is enabled and a non-empty backplane connection string is available from config or `EVENTSTORE_SIGNALR_REDIS`.
- `ConfigureBackplane()` already uses `StackExchange.Redis.ConfigurationOptions.Parse(redis)` and sets `AbortOnConnectFail = false`, preserving fail-open startup behavior from Story 10.2.
- `src/Hexalith.EventStore/Program.cs` maps `/hubs/projection-changes` only when `EventStore:SignalR:Enabled` is true.
- `src/Hexalith.EventStore.AppHost/Program.cs` enables SignalR for the EventStore resource when the Blazor sample UI is present, but it does not currently create a second EventStore resource or wire a SignalR Redis backplane by default.
- `AspirePubSubProofTestFixture` assumes DAPR's local Redis at `localhost:6379` for state/pubsub proof. This story may reuse that endpoint only if the fixture proves SignalR backplane settings point to it.
- `SignalRBackplaneWiringTests` prove `RedisHubLifetimeManager<ProjectionChangedHub>` registration in DI, but not actual cross-process message delivery.

### Testing Standards

- This is a Tier 3 runtime proof. It likely requires Docker, DAPR placement/scheduler, Redis, and a host that can bind multiple EventStore endpoints.
- Keep Tier 3 tests in `tests/Hexalith.EventStore.IntegrationTests` and use a dedicated xUnit collection if the fixture mutates process environment variables.
- Do not run solution-level `dotnet test`. Run affected projects individually.
- If the proof uses manual commands rather than an automated test, the evidence file must be specific enough for another developer to reproduce it.
- The proof should use bounded waits and precise failure messages, following the style in `PubSubDeliveryProofTests`.
- Positive delivery wait must be bounded to 10 seconds or less unless the evidence justifies a tighter/looser value before execution. Negative controls must use the same or shorter wait and assert no valid cross-instance delivery.
- Evidence file minimum sections: Run Identity, Environment, Topology, Configuration, Positive Proof, Negative Controls, Query Refresh Boundary, Logs/Diagnostics, Results, and Cleanup.

### Latest Technical Information

No new external research is required for story creation. The implementation should use the existing .NET 10 / ASP.NET Core SignalR Redis backplane package already referenced by the repo: `Microsoft.AspNetCore.SignalR.StackExchangeRedis`.

### Project Structure Notes

- SignalR server-side hub code now lives in `src/Hexalith.EventStore/SignalRHub/`, not the older `Hexalith.EventStore.CommandApi` path cited in Story 10.2.
- AppHost orchestration lives in `src/Hexalith.EventStore.AppHost/Program.cs`.
- Existing Tier 3 fixture patterns live under `tests/Hexalith.EventStore.IntegrationTests/Fixtures/`.
- Expected BMAD edits during dev are this story file, a test evidence file, and `sprint-status.yaml` bookkeeping.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md#Proposal-2`] - R10-A2 acceptance criteria and rationale.
- [Source: `_bmad-output/implementation-artifacts/epic-10-retro-2026-05-01.md`] - R10-A2 action item: prove Redis backplane delivery with real Redis and multiple EventStore instances.
- [Source: `_bmad-output/implementation-artifacts/10-2-redis-backplane-for-multi-instance-signalr.md`] - Completed structural Redis backplane audit and gap-fill baseline.
- [Source: `_bmad-output/implementation-artifacts/10-1-signalr-hub-and-projection-change-broadcasting.md`] - Signal-only broadcast model and direct/pubsub notification chain.
- [Source: `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`] - Current Redis backplane configuration path.
- [Source: `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`] - Group join and hub endpoint contract.
- [Source: `src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs`] - Broadcast implementation and fail-open behavior.
- [Source: `src/Hexalith.EventStore/Program.cs`] - Conditional hub mapping.
- [Source: `src/Hexalith.EventStore.AppHost/Program.cs`] - Current Aspire topology and SignalR environment wiring.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRBackplaneWiringTests.cs`] - Existing structural backplane tests.
- [Source: `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs`] - Docker/Redis/Aspire evidence precedent.
- [Source: `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs`] - Bounded Tier 3 proof pattern.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Baseline: `aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` started, but the wrapper failed to set `EnableKeycloak=false`; Aspire showed EventStore/admin resources waiting on Keycloak. The apphost was stopped before builds/tests to clear output locks.
- Red phase: focused R10-A2 integration proof initially failed before implementation, then failed on a malformed double-slash hub URL; the harness URL construction was corrected to target the concrete instance-B hub endpoint.
- Green/refactor: `SignalRRedisBackplaneRuntimeProofTests` passed and generated `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-05-02-150535.md`.
- Verification: Release build and standard unit-test projects passed individually.

### Completion Notes List

- Added Development-only SignalR runtime proof endpoints gated by `EventStore:SignalR:RuntimeProof:Enabled=true`; endpoints expose runtime identity/config evidence and invoke the existing `IProjectionChangedBroadcaster` without changing the public hub payload.
- Added a Tier 3 integration proof that starts two distinct EventStore processes, connects a SignalR client directly to instance B, broadcasts from instance A through the Redis backplane, asserts receipt within 10 seconds, and records process identities/endpoints.
- Added negative controls for absent Redis backplane, stale pre-trigger messages, same-instance ambiguity, and unreachable Redis fail-open startup/broadcast behavior.
- Query refresh is explicitly bounded to the sibling projection-proof stories in the evidence file.

### File List

- `_bmad-output/implementation-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-05-02-150535.md` (initial v1.0 run, superseded)
- `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-05-02-133041Z.md` (post-review v1.1 run, expanded schema)
- `src/Hexalith.EventStore/Program.cs`
- `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/SignalRRedisBackplaneProofTestCollection.cs`

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-02 | 1.1 | Code review applied: 2 decisions resolved + 15 patches (evidence schema expansion, request-validation, secret redaction, env-var Redis endpoint, log capture, try/finally evidence save, broadened catch, UtcNow consistency, gating symmetry). Story moved to done. | Claude Opus 4.7 |
| 2026-05-02 | 1.0 | Implemented R10-A2 Redis backplane runtime proof, saved Tier 3 evidence, and moved story to review. | GPT-5 Codex |
| 2026-05-02 | 0.3 | Advanced elicitation hardened stale-message controls, runtime identity readiness, and Redis evidence boundaries. | Codex automation |
| 2026-05-01 | 0.2 | Party-mode review applied: tightened deterministic A/B topology, Redis negative controls, evidence schema, payload boundary, and test-hook isolation. | Codex automation |
| 2026-05-01 | 0.1 | Created ready-for-dev R10-A2 Redis backplane runtime proof story. | Codex automation |

## Party-Mode Review

Date: 2026-05-01T19:19:05+02:00

Selected story key: `post-epic-10-r10a2-redis-backplane-runtime-proof`

Command/skill invocation used: `/bmad-party-mode post-epic-10-r10a2-redis-backplane-runtime-proof; review;`

Participating BMAD agents: Winston (Architect), Amelia (Developer), Murat (Test Architect), Sally (UX/Adopter Experience)

Findings summary:

- Deterministic two-instance topology needed stronger proof: independently hosted EventStore instances, direct instance-B client target, and instance-A broadcast origin.
- False-positive exclusion needed mandatory Redis-disabled or Redis-isolated controls, plus same-instance routing guardrails.
- Redis/backplane evidence needed concrete runtime proof from both instances rather than static DI/code evidence.
- SignalR group and payload semantics needed explicit pinning so the Tier 3 proof does not expand the public hub contract.
- Evidence needed a reproducible schema with topology, commands, runtime identities, payload, timings, logs, and control results.
- AppHost/Aspire and test-only diagnostics needed constraints to avoid leaking proof hooks into production behavior.

Changes applied:

- Expanded AC #1-#4 to require concrete A/B endpoints, runtime identities, direct client targeting, deterministic origin, correlation/run id, and bounded waits.
- Strengthened AC #6-#7 with both-instance Redis evidence and mandatory negative/control paths.
- Added AC #8-#11 for payload boundary, fail-open operator evidence, evidence artifact schema, and reproduction lane.
- Updated scope boundaries, tasks, architecture guardrails, and testing standards to make the proof reproducible and to isolate any test-only hook.

Findings deferred:

- None. All party-mode findings were story-hardening clarifications and did not change product scope, architecture policy, or cross-story contracts.

Final recommendation: ready-for-dev

## Advanced Elicitation

Date: 2026-05-02T10:35:17+02:00

Selected story key: `post-epic-10-r10a2-redis-backplane-runtime-proof`

Command/skill invocation used: `/bmad-advanced-elicitation post-epic-10-r10a2-redis-backplane-runtime-proof`

Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Pre-mortem Analysis; Self-Consistency Validation; Architecture Decision Records

Reshuffled Batch 2 method names: Security Audit Personas; Chaos Monkey Scenarios; Reverse Engineering; Occam's Razor Application; Lessons Learned Extraction

Findings summary:

- The signal payload intentionally lacks a run id, so the story needed an explicit stale-message false-positive control without expanding the public hub contract.
- Redis backplane evidence needed a readiness gate from both instances before broadcast, not only post-hoc logs or static configuration.
- Test-only broadcast hooks needed stronger wording around runtime identity evidence and production unavailability.
- Redis channel isolation details may be useful evidence, but the policy decision belongs to R10-A7 and must not be absorbed by this proof story.

Changes applied:

- Tightened AC #4 and AC #8 to require run-unique projection/tenant values when possible, or an explicit pre-trigger receive-buffer drain/reset when not possible, while preserving the two-field public hub payload.
- Tightened AC #6 and Tasks 1/4 to require both-instance runtime identity and backplane readiness evidence before the positive broadcast.
- Expanded negative controls to include pre-trigger stale-message detection and ambiguous same-instance routing.
- Added architecture guardrails for runtime identity evidence and Redis channel observations without changing broader channel-isolation policy.

Findings deferred:

- Redis channel/database/prefix isolation policy remains deferred to `post-epic-10-r10a7-redis-channel-isolation-policy`.
- Whether the production app exposes any diagnostic identity endpoint remains a dev-story implementation choice; this story only permits narrowly scoped Development/Test proof hooks.

Final recommendation: ready-for-dev

## Verification Status

Implementation complete. Runtime proof, Release build, focused integration test, and standard unit-test projects passed under `bmad-dev-story`.
