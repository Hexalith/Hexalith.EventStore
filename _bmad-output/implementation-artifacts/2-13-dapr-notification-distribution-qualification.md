---
title: Dapr Notification Distribution Qualification
story_id: '2.13'
story_key: 2-13-dapr-notification-distribution-qualification
epic: '2'
status: done
created: 2026-10-05
updated: 2026-10-25
owner: SignalR transport owner
supporting_requirements: [FR16, NFR5, NFR12, NFR15, NFR16]
context:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/ux.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-10-05.md
  - docs/concepts/dapr-infrastructure-boundary.md
  - docs/architecture/dapr-infrastructure-exceptions.yaml
---

# Story 2.13: Dapr Notification Distribution Qualification

As an operator, I want notification distribution to use a qualified Dapr path wherever feasible, so infrastructure choice does not leak into application delivery contracts.

## Scope And Current Disposition

**Done on 2026-10-25 by an accepted exception.** The owner kept the opt-in SignalR Redis backplane under a scoped AD-34 exception and did not adopt a Dapr replacement. See [Disposition — 2026-10-25](#disposition--2026-10-25). Story 2.8 retains FR16 contract ownership. NFR5's primary-owner gap stays with Story 2.16; this story supplied supporting distribution and compatibility evidence. PRD §8.4 and AD-1/AD-8/AD-9/AD-10/AD-12 govern the work.

`ConfigureBackplane` in `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs` reads `BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS` and calls `AddStackExchangeRedis` only when one is set. Gateway compiles this source through linked inputs. Both `src/Hexalith.EventStore/Hexalith.EventStore.csproj` and `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj` reference `Microsoft.AspNetCore.SignalR.StackExchangeRedis`. This coupling is now the accepted exception `signalr-replica-distribution` in `docs/architecture/dapr-infrastructure-exceptions.yaml`.

Dependencies: Story 2.8's delivered notification contracts and existing authorization/group semantics are the compatibility baseline. Story 3.17 inventories the exception's paths and enforces them in its guard. Existing production, profile and release gates are unchanged.

## Disposition — 2026-10-25

**Decision.** On 2026-10-25 the owner accepted a scoped exception for the retained Redis backplane. The project has one maintainer, so the decision is `single-maintainer-attested`, not independent. No Dapr replacement, host-graph transition or Redis retirement happens. Source, test, project, AppHost and deployment files are unchanged by this disposition.

**Gaps that blocked a Dapr replacement:**

| Gap | Observation |
| --- | --- |
| Tenant enrollment | CFD-1 topics are `{tenantId}.{projectionType}.projection-changed`. `pubsub.redis` (Redis Streams) treats the shipped wildcard subscription `*.*.projection-changed` (`deploy/dapr/subscription-projection-changed.yaml`) as one literal stream, so no concrete tenant topic is delivered. Fan-out worked only with exact per-tenant subscriptions and an operator sidecar reload (`DaprRedisStreams_ExactTopicCatalogRollout_AddsTenantOnBothSidecars`, which failed sidecar readiness in the final full-class run and passed in a separate recheck). New tenants are not enrolled automatically. |
| Replica restart on a reused endpoint | With stock daprd `1.18.7`, the only stock runtime tested, the surviving host's remote ETag actor call fails after its peer restarts on the same internal gRPC endpoint. The case passed only with a disposable patched daprd built outside the repository (SHA-256 `a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`). No released runtime with the fix was identified. The stock-runtime failures are recorded at `git show bbfd0fada14ce11e137365637238af4fec2f7f7f:_bmad-output/implementation-artifacts/evidence/story-2-13/qualification-2026-10-21-restart-trace.md`. The final run gave the patched build to both proof sidecars. A restart on a fresh endpoint passes on stock daprd. |
| Production profile | The MVP target, self-hosted Kubernetes at `192.168.1.30`, runs an isolated one-replica Dapr `1.18.1`, below the AD-26 `1.18.3` floor. Story 3.21 selects no durable broker, runtime or binding-capable AD-36 issuer. Azure and AWS are outside MVP (owner, 2026-10-20). |

**Alternatives considered:** Dapr pub/sub fan-out with a per-replica consumer group (`consumerID: "{uuid}"`) is blocked by all three gaps. The `bindings.azure.signalr` output binding was not run: the owner excluded Azure from MVP on 2026-10-20, and the binding does not own self-hosted negotiation or group membership. One fixed topic with tenant filtering in the application would avoid enrollment, but changes the Story 2.8 / CFD-1 topic contract and the AD-36 topic-bound provenance. One EventStore replica with no multi-replica freshness claim (AD-26) needs no exception.

**What the Dapr candidate did prove locally** (test-only exact-topic `pubsub.redis` profile, two hosts, independent sidecars, shared `eventstore` app ID):

- Signal and scoped detail reached authorized clients on both hosts, with matching Dapr persisted readback and real projection composition.
- Other-tenant clients received nothing.
- Duplicate hints did not create duplicate completion.
- The serialized metadata ceilings (16 entries, 2,048 UTF-8 bytes) held on both hosts.
- Provider connection-reset and silent-discard outages ended with bounded caller cancellation, restored delivery and reconciled readback.
- Restart on a fresh endpoint passed with fresh authorization and rejoin. Restart on a reused endpoint passed only with the patched daprd.

The final full distribution class was 5 passed / 1 failed. The failure was a sidecar that did not become ready within 60 seconds, before the catalog case started.

The Redis backplane's own cross-host delivery passed in `SignalRRedisBackplaneRuntimeProofTests` in the final run. The same test failed twice with first-host readiness cancellation in the earlier 6ca1b4cb run; that cause was not established. When Redis is unreachable, the host still answers HTTP 200 and logs a `FailOpenFailure` diagnostic after about 6 seconds, and cross-replica messages are dropped. The final run is recorded under Verification below.

At disposition time on 2026-10-25, the existing Debug build of `c5d1c280d` also ran the transport-independent hub and backplane-wiring tests. They passed 39 of 39, with no errors, skips or not-run cases, between `17:36:20` and `17:36:22` UTC:

```sh
tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests -class '*ProjectionChangedHubTests' -class '*SignalRBackplaneWiringTests' -class '*SignalRHubEndpointTests'
```

They cover unauthenticated, wrong-tenant, missing-tenant and malformed-group joins, plus the opt-in backplane registration.

**Exception scope.** The exception covers cross-replica hub messages through `Microsoft.AspNetCore.SignalR.StackExchangeRedis` `10.0.13`. It accepts a dependency that shipped before AD-34, for Development/test multi-instance runs; production runs one EventStore replica and needs no exception. Allowed paths are the SignalR registration, options, options-validation and Development-only runtime-proof files plus the two host projects, all listed in the register. Allowed configuration keys are `EventStore:SignalR:BackplaneRedisConnectionString` and `EVENTSTORE_SIGNALR_REDIS`. Negotiation, authentication, tenant authorization and group joins stay in the application hub. The exception does not grant:

- production eligibility, because AD-26 still decides the freshness-signal replica posture;
- any other Redis client, path or key;
- any fallback from a failed Dapr operation.

The register records the removal trigger.

**Acceptance outcome:**

| AC | Outcome |
| --- | --- |
| AC1 | Met. Gaps, alternatives and the owner are in this section and the register. |
| AC2 | Met locally for the Dapr candidate under the test-only profile. No Dapr design was selected, so delivery "through the selected Dapr design" does not apply; the retained Redis design's cross-host delivery passed in the Redis proof. |
| AC3 | Met for signal and detail, metadata bounds, fresh-endpoint restart and rejoin, and duplicates. Reused-endpoint restart passed only with the patched daprd. |
| AC4 | Partly met. The Dapr candidate's outages ended in bounded, cancellable failure. The retained Redis path fails open: hints are dropped while HTTP still succeeds. Canonical UI `Stale`/`Degraded`/`Unavailable`/`Unknown` states were not exercised; this story does not change UI freshness behavior. The open items are in `deferred-work.md`. |
| AC5 | Met for anonymous or expired connections, wrong, missing or malformed tenants, invalid provenance and denied rejoins. Conflicting tenant claims and in-place revocation were not established; they are in `deferred-work.md`. |
| AC6 | Not applicable. No replacement was approved, so host graphs stay unchanged and there is nothing to roll back. |
| AC7 | Met through the exception branch. Under AD-12, the record is this story's Verification section rather than an immutable packet. |


## Required Comparison

Compare the current self-hosted Redis topology with both candidates: Dapr pub/sub fan-out to every locally connected hub and `bindings.azure.signalr` output binding. Select no design by component name alone. Record exact runtime/client/component/provider versions, process topology, subscriptions, app IDs, scopes, authentication, and the supported operating envelope.

For each candidate record connection/negotiation ownership, transport compatibility, group/user targeting, authenticated group membership and rejoin, scale-out and replica delivery, ordering, at-least-once duplicates, outage/recovery/backpressure, tenant authorization, operational cost/limits, credentials, rollback, and observable gaps. A topic with competing consumers that delivers to one replica cannot prove delivery to all hubs holding relevant connections. An output binding does not by itself prove equivalence to self-hosted SignalR connection management.

## Acceptance Criteria

### AC1 — Operation-Level Design Or Gap Decision

**Given** the existing self-hosted topology and both Dapr candidates, **when** the capability comparison is evaluated, **then** each required operation/guarantee has exact-profile evidence or a recorded unresolved/gap result, alternatives, and an accountable owner; convenience and incomplete investigation cannot establish a permanent exception.

**Given** a suitable Dapr design is proven, **when** Architecture/Security/Test review the evidence, **then** the supported design and envelope are explicit. If neither candidate can meet required behavior, retain the specific reproducible gap and request an architecture-owner decision for an isolated Redis exception with exact paths, evidence, owner, and review/removal trigger. This story/proposal does not preapprove that decision.

### AC2 — Real Replica Delivery

**Given** at least two real application hosts with independent Dapr sidecars and clients connected to each host, **when** a projection confirms a changed tenant/domain/projection, **then** all authorized relevant clients on both hosts receive the compatible freshness signal through the selected Dapr design; clients outside the authorized scope receive none.

**And** retained evidence identifies which host owns each connection, subscription/fan-out behavior, group membership, and persisted projection result/version. A mock broadcaster, one host, one selected replica, API acceptance, or notification alone does not satisfy the gate.

### AC3 — Contract, Bounds, Reconnect, And Duplicate Behavior

**Given** signal-only and detail clients, **when** notifications cross replicas and clients disconnect/reconnect/rejoin or a host restarts, **then** `ProjectionChanged` and additive `ProjectionChangedDetail`, scoped groups, opaque metadata keys, maximum 16 entries/2,048 serialized UTF-8 bytes, and metadata-value suppression above Debug remain compatible.

**Given** duplicate/out-of-order delivery or a reconnect gap, **when** the client refreshes, **then** repeated signals do not create duplicate user-visible completion or mutate domain state, and projection/read-model evidence governs freshness and success. Record the supported duplicate/reconciliation behavior without inventing delivery ordering or exactly-once guarantees.

### AC4 — Outage And Honest Freshness

**Given** sidecar, component/provider, replica, or network unavailability, **when** publishing, distributing, reconnecting, or querying fails, **then** failure/recovery is bounded and cancellable and preserves honest `Stale`, `Degraded`, `Unavailable`, and fail-safe `Unknown` behavior under canonical UX authority.

**And** no automatic direct Redis/provider fallback, forged projection-confirmed success, unbounded retry, exposed credentials/internal endpoints, or silent authorization downgrade occurs. Retain outage, restore, reconnect, and client readback evidence.

### AC5 — Tenant And Group Denials

**Given** anonymous, unauthorized, wrong-tenant, conflicting/malformed tenant, forged group/scope, or expired-authentication input, **when** connections negotiate, join/rejoin, or receive a cross-replica notification, **then** application authorization fails before disclosure and no denied client joins/receives an unauthorized group or triggers downstream work.

**And** component scopes/app IDs never create tenant grants. Include two tenants with equivalent projection names, clients on both hosts, and reconnect after authorization changes; correlation and safe reason codes reveal no payload or tenant-private group data.

### AC6 — Atomic Transition And Rollback

**Given** an approved qualified replacement, **when** it is implemented under the existing execution gates, **then** both host dependency graphs, linked-source registration/options, credentials/component configuration, AppHost/deployment/subscription assets, scopes/health, documentation, and tests change together under AD-9.

**And** direct Redis runtime packages/configuration retire only after cross-instance compatibility and rollback proof. Preserve current behavior until the transition is qualified. A separately accepted exception must enter the final 3.17 inventory/guard with exact approved scope; no global provider exemption is allowed.

### AC7 — Completion Packet

**Given** completion is requested, **when** Architecture/Security/Operations/Test evaluate the immutable packet, **then** it binds source/spec/profile/component identities, comparison and decision, exact commands/results/UTC interval, replica/group/tenant scenario matrix, logical persisted readback, compatibility/rollback, redaction, limitations, and the approved disposition.

**And** completion records either a proven Dapr replacement or a separately accepted evidence-backed exception. Unknown suitability, missing service access, skipped replica/denial tests, or missing approval remains blocked/unproven; this planning artifact remains backlog until that future work passes.

## Implementation Tasks

**2026-10-23 evidence policy:** do not write under `evidence/`; record commands, pass/fail counts, commit SHA and CI run link in this story (architecture AD-12).

- [x] Evaluate both host projects and the linked SignalR source, then record the graph, contract and authorization baseline. The original `evidence/story-2-13/qualification.md` was removed under AD-12; the baseline is summarized under Scope and Disposition.
- [x] Add the two-candidate operation and profile matrix. It is summarized under Disposition and in the register.
- [x] Add the two-host, independent-sidecar proof: `SignalRDaprDistributionRuntimeProofTests.cs`, `SignalRDaprDistributionFixture.cs`, `SignalRDaprDistributionNode.cs` and `SignalRDaprNetworkFaultProxy.cs`, with `SignalRDaprProjectionDispatchProofTests.cs` for real projection composition. The Redis baseline test is unchanged.
- [x] Implement and run the signal/detail, restart/rejoin, duplicate, outage, metadata-bound and tenant-denial scenarios. The latest results are under Verification.
- [x] Record the design or gap decision. `docs/architecture/dapr-infrastructure-exceptions.yaml` now holds the accepted exact-scope exception.
- [x] Not applicable under the accepted exception: the transition task was conditional on a qualified replacement. Redis host graphs, AppHost and deployment assets stay unchanged.
- [x] Update `docs/concepts/dapr-infrastructure-boundary.md`, the register, architecture/PRD status statements, this story and `sprint-status.yaml` with the disposition and the 3.17 handoff.


## Verification Handoff

The proof tests are listed under Implementation Tasks. Build the integration test project, then run its executable with `-class` or `-method` filters, as in the final run below. The reused-endpoint restart case passes only when `HEXALITH_STORY_2_13_DAPRD_PATH` points at the patched daprd. With stock daprd it is expected to fail, which makes it the probe named in the register's removal trigger. These Tier-3 tests are not gated in CI.

## Earlier Verification Runs

Nine October 25 local runs before the final one repeated the qualification on successive revisions; one of them was a pinned Go reconstruction of the patched runtime. They are kept in Git history and were removed here to keep the story readable. Read them with `git show c5d1c280d8c1c1a207cd159d817f614e93c93130:_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md`. Its earlier sections, mainly the "2696cf21 Continuation", hold the runtime paths, lifecycle commands, container identities and deps hashes that the final run below refers to as "preceding". Runs before October 23 lived in evidence folders removed by `6a0b6441f6d78a3007678a1d0ec336e9cb8ff961`, also in Git history.

The final run below is recorded as it was written. Its closing status sentence, which says the story was backlog and the register empty, predates the disposition above.


## Verification — October 25 c5d1c280 Continuation

Source revision: `c5d1c280d8c1c1a207cd159d817f614e93c93130`; CI run/link: none.
The Debug source-reference solution build passed. Runtime executions recorded
**112 passes and one failure**, including a separate passing catalog recheck.
The complete distribution class remains **5 passed / 1 failed**: the recheck
does not resolve its initial sidecar-readiness failure. No behavioral correction
is claimed; this Verification append is the only implementation change.

The starting tree was clean. HEAD, all root-declared checkouts and 17,613 measured
tracked/untracked source/configuration files stayed unchanged through build and
tests. Builds was `0c735503728e18de93937b1495abebcfa6e33f9a`, Commons
`1663028fb6d9f46a0bf17919edeb21bb99be1587`, Tenants
`3b60914ace5201a711fb51c1036d3679e0512ec8` and Platform
`5fa798f7d860a021feb8cccc35331271e3c9c802`. The build changed 56 selected outputs;
all 550 measured executable/DLL/deps/runtimeconfig outputs stayed identical from
build completion through all twelve before/after test boundaries. Hashes of the
selected daprd, installed daprd and Aspire CLI also stayed stable. Dapr CLI and
.NET installation files were not included in that executable hash inventory.
This establishes incremental-build and observed byte
stability, not complete compiler-input correspondence, atomic module-load identity
or clean reproducibility. No files were staged or committed, and no branch,
submodule, dependency, installed runtime or production graph was changed by this task.

The selected fixture runtime and installed runtime retain the exact paths,
versions and SHA-256 identities in the preceding 2696cf21 Verification section:
retirement `a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`,
installed `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.
Observed SDK/.NET/xUnit/Dapr CLI/Aspire/Docker versions remain
`10.0.402`/`10.0.13`/`4.0.2`/`1.18.2`/`13.6.1`/`29.8.2`;
the exercised integration deps still name Dapr.Client `1.18.10` and
Microsoft.AspNetCore.SignalR.Client `10.0.13`. No Go tests or runtime builds were
repeated. System `go version` returned 127 (`go: command not found`); the earlier
pinned reconstruction and its failed full-suite results remain historical evidence.

Temporary receipts, reduced lifecycle output and scenario captures are under
`/tmp/story-2-13-20261025-c5d1c280-70d9j8_a`; no committed packet or validator
was introduced. Every recorded build/lifecycle/test command received:

```sh
export EnableKeycloak=false
export HEXALITH_STORY_2_13_DAPRD_PATH=/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement
export HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-c5d1c280-70d9j8_a/scenario-output
export R10A2_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-c5d1c280-70d9j8_a/redis-output
IT=tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests
SERVER=tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests
APPHOST=src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

Only the two proof sidecars consume the retirement selection; composed AppHosts
and Direct retain installed daprd. Build and test commands ran sequentially,
with read-only observations during distribution. All test rows have zero runner
errors, skips and not-run cases. UTC intervals below are on 2026-10-25.

| Exact command, expanding the aliases above | Result | UTC interval |
| --- | --- | --- |
| `dotnet build Hexalith.EventStore.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Passed; 0 warnings/errors | `16:12:08.991372–16:14:40.011229` |
| `"$IT" -method '*DaprRedisStreams_ReplicaRestart_ReauthorizesAndReconcilesPersistedState'` | 1 passed | `16:15:24.215133–16:16:40.579186` |
| `"$IT" -class '*SignalRDaprDistributionRuntimeProofTests'` | 5 passed, 1 failed; no `-stopOnFail` | `16:16:45.964126–16:22:49.276060` |
| `"$SERVER" -class '*ETagActorTests' -class '*SignalRProjectionChangedBroadcasterTests' -class '*DaprProjectionChangeNotifierTests' -class '*DaprProjectionChangeNotifierSignalRTests' -class '*ProjectionChangedNotificationValidatorTests' -class '*DomainServiceWorkloadAssertionHandlerTests'` | 102 passed | `16:22:52.433813–16:22:53.285620` |
| `"$IT" -class '*SignalRDaprProjectionDispatchProofTests' -class '*ValidProjectionRoundTripE2ETests'` | 2 passed | `16:23:00.105879–16:23:59.173669` |
| `"$IT" -class '*SignalRRedisBackplaneRuntimeProofTests'` | 1 passed | `16:24:02.269306–16:27:15.258183` |
| `"$IT" -method '*DaprRedisStreams_ExactTopicCatalogRollout_AddsTenantOnBothSidecars'` | Separate recheck: 1 passed | `16:27:52.285368–16:28:28.307272` |

The failed catalog case never reached rollout: `SignalRDaprDistributionFixture.StartAsync`
line 66 failed starting the first sidecar, PID `61713`. Node `StartSidecarAsync`
line 227 polls `/v1.0/healthz`; `WaitForHealthyAsync` line 380 reported that
`http://127.0.0.1:42985/` did not become healthy within **60 seconds**. Retained
diagnostics repeatedly report `dapr is not ready: [runtime internal-placement-service]`
(the component order also varies). Near the deadline they show a cancelled
`tenants` invocation and subscription startup. These bounded diagnostics identify
the failed phase, not why startup stalled. The read-only command
`docker logs --since 2026-10-25T16:21:40Z --until 2026-10-25T16:22:55Z dapr_placement`
returned eight lines; its retained warning subset contains three cancelled-stream
warnings without a proven association to the pending readiness component.
The original failure has no catalog before/after manifest. No timeout budget was increased,
no failure was waived, and the one passing focused recheck supplies no causal fix.

The unchanged Development profile is the preceding section's exact-topic
`pubsub.redis` v1 / `consumerID: "{uuid}"`, `state.redis` v1, shared `eventstore`
identity and independent proof sidecars. Signal/detail, duplicate/reconciliation,
replacement-identity denied rejoins, anonymous/expired connections, wrong/missing/
malformed tenant joins and invalid provenance checks passed. Conflicting tenant
claims and in-place revocation remain unestablished. Caller budgets, authorization,
16-entry/2,048-serialized-byte ceilings and the 15-second signal wait are unchanged.

Both reused-endpoint runs proved initial second-host ETagActor ownership and Dapr
persisted version/ETag `1` → `2`, fresh authorized membership, acceptance of
exactly 2,048 serialized bytes and rejection of 2,129/2,050/2,060-byte vectors
on both hosts. The following observations are synthetic-state recovery, not
projection completion or canonical UI freshness; the fresh endpoint is comparison only.

| Run | Original → restored host / sidecar PIDs | Internal gRPC port | Logical Dapr read-model key |
| --- | --- | --- | --- |
| Focused reused | `12574/12651` → `21729/37924` | `40075` → `40075` | `story-2-13:tenant-a-3f0b01a46f01:restart-3f0b01a46f01-2` |
| Full-class reused | `54013/54107` → `60407/75427` | `42521` → `42521` | `story-2-13:tenant-a-f6d59f67d126:restart-f6d59f67d126-0` |
| Full-class fresh comparison | `40199/40236` → `47698/59671` | `40479` → `33895` | `story-2-13:tenant-a-44d9539c648b:restart-44d9539c648b-3` |

Reset closed **29** connections and refused **1,740** reconnects; publication
returned `DaprException` after **69.2077 ms**. Blackhole discarded **4,694** bytes
with zero proxy disconnects/rejections; publication cancelled after **1,999.6966 ms**.
Both hosts' faulted reads ended with `OperationCanceledException` in
**1,999.3008–2,006.2357 ms**, against the unchanged 2,000 ms caller / 5,000 ms
outer bounds. Both cases read restored versions `[1,1]` / ETags `["1","1"]`,
then final version/ETag `2` / `"2"`; denied clients received no hints during four
concurrent one-second signal/detail reads in each outage case. Publication
remains `unknown-until-reconciliation`, with no canonical UI freshness,
saturation/backpressure, retention or production-envelope proof.

The separate catalog recheck observed no new-tenant delivery before rollout
during two concurrent one-second reads. It stopped both sidecars and started
both after adding exact subscriptions, preserving hosts `92380`/`97264` and
connections `-WXGP47sBaZb8D_MAZr7LQ`/`8FgYRdtMxazP9OxfcOmjtQ`; sidecars changed
from `92429`/`97333` to `2745`/`2887`. Fresh-control signal/detail reached both,
with Dapr versions/ETags `1` → `[2,2]` and no other-tenant hints during two
concurrent one-second detail reads followed by two concurrent one-second
tenant-boundary signal observations. The after-rollout manifest, relative to
`HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY`, is
`topic-catalog-after-rollout-20261025T1627525359250Z-7d010a314c1142d1b6456ccaa458155b/manifest.json`.
It binds `AddedTopic` = `tenant-b-b07f76ea7a88.catalog-b07f76ea7a88.projection-changed`
to subscription hash `bf3116d42027a058548eff130a77f64b2f1b722e11a152196e26666ad63eb48d`
and `FreshControlTopic` = `tenant-b-b07f76ea7a88.catalog-fresh-b07f76ea7a88.projection-changed`
to subscription hash `49eb630b786ff31d91929291e0335013338be4ab066f72ba2ed6bc9d1f3384a9`.
This proves neither automatic enrollment nor live subscription refresh and
does not replace the failed full-class setup.

The real-projection case recorded command `Completed`, one event, `ProjectionBacked`
counts `1`/`1` and equal authoritative ETag `"Y291bnRlcg.xPjbnvfaK0-TUlm6OZ56iA"`.
Host/sidecar PIDs were `3991/4165` and `4428/4470`; authorized connections
`f_6fHVZMccZARsgLtj3uOw` and `l_11a_VYmoNuv7RQ3BJGzQ` received signal/detail
with scope `counter-status`, `change-kind=counter-projection-updated`. Other-tenant
connections `IH0R-y1wyhRzJQSncc3VJw` and `5XjNVoMoKGhg5jB2iGW5sg` received
neither in four concurrent one-second channel reads. Synthetic tenant
`tenant-a-02ba9aa91388459a882faceac9f43031` and its `.counter.projection-changed`
topic, aggregate `distribution-projection-f28fcf5cf15c45e7971ab0df94dba8a5`, and
correlation `a5a4b109-6d82-4ab8-a85c-8332464794c3` bind the logs and readback.
Its generated pubsub/state hashes match the preceding real-projection table;
its scenario-specific `subscription-0.yaml` hash is
`8ce29b936706cedbbe4e30c237caff9029d71a81aa2e9061b0fc851348838fce`.
This remains optional legacy detail under the test-only profile.

Redis's transport-only comparison recorded origin/target PIDs `9944`/`24827`,
signal delivery in **19 ms**, detail/clipping and the two-second disabled-backplane
negative control. Unreachable-Redis host `63493` accepted HTTP 200 while its
`RedisConnectionException` / `FailOpenFailure` diagnostic measured **5,961.0139 ms**;
HTTP acceptance proves no notification delivery or projection freshness. The
report's static `dotnet test ... --filter` template was not executed; the direct
executable command above is authoritative.

Current owning-output DLL SHA-256 identities follow. Their deps hashes match
the preceding 2696cf21 table where listed; the full 550-output inventories include
the actual host copies and both separate test runners.

| Output under its owning project's `bin/Debug/net10.0/` | SHA-256 |
| --- | --- |
| `Hexalith.EventStore.dll` | `2a7a74f0435bceb1f54d17fff7ebbb75c34488f3ec6c1384b03ba92e2db17fee` |
| `Hexalith.EventStore.Server.dll` | `a51873282c16c35124fe1bb6f187c072c2f68ff39ad1317a9115302e22222c10` |
| `Hexalith.EventStore.Gateway.dll` | `3fdd6de7e5b05d4160158a73cba9969636002d4678ccd46f153fa9082e7cb28e` |
| `Hexalith.EventStore.Sample.dll` | `09ca5d365f5d53257304ce764fdb1a8bdc39d53d667338ac3dde06fa7ee9440f` |
| `Hexalith.EventStore.AppHost.dll` | `099af411eaed6dcc94db4ec4812392a8791975db8a47e47a9bf6435a09a9bb09` |
| `Hexalith.EventStore.IntegrationTests.dll` | `99fd667bbabb87b24e6a45aec97255142de4ca90df5c15a5392e7e8360b517f0` |
| `Hexalith.EventStore.Server.Tests.dll` | `69bde96c13d42495cc9f4583137cee3fecdf0872263f47c7dea570bb3a8c296b` |

Lifecycle reused the exact ordered baseline and auxiliary commands in the preceding
2696cf21 section, with the same explicit AppHost and `--non-interactive` arguments.
Baseline start/wait/describe/stop preceded build (AppHost/CLI `57108/56909`). The
auxiliary start, EventStore/sample/sample-sidecar waits, EventStore/sidecar stops
and describe ran twice: after build (`90341/90191`), then before the catalog recheck
(`80528/80424`). Explicit AppHost stops followed server controls and the catalog
recheck. Projection/Direct fixtures owned their intervening AppHosts; final
describes followed Redis and the catalog cleanup. All lifecycle commands returned
0. Each auxiliary describe retained sample/sidecar Running/Healthy, EventStore
Finished with exit **134**, and its sidecar Finished with exit **0**; successful
CLI stop commands do not establish the cause of exit 134.

Read-only support inspections at `16:11:14`, `16:17:04` and `16:28:43 UTC`
matched the preceding Redis/placement/scheduler container IDs, image IDs/digests,
start times and running states; Redis remained `6.2.21`. Initial observation also
found a separate Tenants workspace's six sidecars on shared `50005`/`50006`
endpoints, including sample `99761` and EventStore `99966`, started at `16:10:13`.
The in-distribution observation retained first proof sidecar `44893` plus auxiliary sample,
admin, admin UI, sample API/UI and Tenants/API participants. These observations
establish no exclusive placement, backend ownership or unobserved-interval topology.
No unrelated process or shared service was stopped or reconfigured by this task.

Final explicit-AppHost describe found no running AppHost. The expanded
`16:28:43.247486 UTC` scan found no matching EventStore/Tenants native executables
or dotnet assembly/project paths, daprd executable or Dapr CLI with `--app-id`.
This is the observed selector's scope, not absence of every machine process.
Inotify stayed `1,048,576`; final accessible scanning counted 3,424 watch lines
across 8,079 readable fdinfo files, with 36 inaccessible/disappeared entries.
No sysctl write was needed.

The current full-class startup failure, prior projection/Direct and Redis failures/
identity ambiguities, pinned Go full-suite failures, historical Contracts validator
blocker and missing CI remain open. Passing current comparisons resolve none of
those causes. Production runtime/broker/binding-capable issuer, shipped enrollment,
canonical completion/freshness faults, atomic both-host transition/rollback and
authorized disposition remain gated. Story 2.16 retains NFR5 primary ownership.
Story `backlog`, spec/sprint `in-progress`, frozen intent, whole-story checkboxes
and `accepted_exceptions: []` are unchanged.

Post-append `bash scripts/check-committed-files.sh` passed (9,096 tracked entries),
and `git diff --check` passed. Preservation checks confirmed the prior story
prefix and unchanged active spec, sprint file and exception register.
