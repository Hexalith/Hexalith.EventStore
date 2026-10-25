---
title: Dapr Notification Distribution Qualification
story_id: '2.13'
story_key: 2-13-dapr-notification-distribution-qualification
epic: '2'
status: backlog
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

This is an actionable backlog specification, not qualification evidence or permission to change runtime behavior in the planning reconciliation. Story 2.8 retains FR16 contract ownership. NFR5's existing primary-owner gap remains open; this story supplies supporting distribution/compatibility evidence rather than claiming whole-requirement closure. PRD §8.4 and AD-1/AD-8/AD-9/AD-10/AD-12 govern the work.

The current `ConfigureBackplane` in `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs` parses `BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS` and calls `AddStackExchangeRedis`. Gateway compiles this source through linked inputs; both `src/Hexalith.EventStore/Hexalith.EventStore.csproj` and `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj` reference `Microsoft.AspNetCore.SignalR.StackExchangeRedis`. This retained runtime coupling is unresolved, with no accepted exception.

Dependencies: Story 2.8's delivered notification contracts and existing authorization/group semantics are the compatibility baseline. Story 3.17 is a coordination handoff for final inventory/guard disposition, not a cyclic execution prerequisite; do not require 3.17 conformance before collecting this story's qualification evidence. Existing production/profile/release gates and any external-service provisioning authority remain required.

## Current Supporting Evidence

The [October 21 silent-fault continuation](evidence/story-2-13/qualification-2026-10-21-blackhole.md) retains a passing reset/blackhole pair with two-second caller cancellation, Dapr reconciliation and restored delivery on both proof hosts, under an explicitly recorded auxiliary AppHost/sample topology. The original standalone startup failure remains retained. Replica restart passed once in isolation but failed twice at the first-host post-restart signal wait in broader runs, so that delivery gap remains unresolved. Current Story 3.21 inputs still select no production broker/profile. No transition, exception, completion or owner approval is inferred; this original story remains backlog.

The [October 20 qualification continuation](evidence/story-2-13/qualification-2026-10-20.md) records the user's clarification to enforce the approved serialized metadata ceiling, shared producer/receiver/broadcaster enforcement, and disposable two-host restart/rejoin/readback evidence. The retained Redis integration remains unresolved. Dynamic-topic distribution, real projection-triggered detail, the production issuer/profile, complete outage/freshness/rollback qualification, and authorized disposition remain open; this story stays backlog and supplies the unresolved Story 3.17 handoff.

## Required Comparison

The [network/catalog/detail continuation](evidence/story-2-13/qualification-2026-10-20-network.md) advances local qualification with a two-sidecar provider connection-reset scenario, a fixture-only exact-topic catalog rollout, and optional domain-provided detail retained through actor persistence/publication. Legacy JSON/DataContract bytes, serialized bounds, replay idempotence, real two-host signal/detail plus persisted readback and tenant denials, and ordinary Direct regression pass. The earlier four-scenario run and final-detail binaries retain separate identities. The user identified self-hosted Kubernetes at `192.168.1.30` as MVP and excluded Azure/AWS; read-only inspection found an isolated one-replica Dapr `1.18.1` profile, distinct from local `1.18.7` and below AD-26's floor. A selected production durable broker/runtime/enrolled issuer, shipped topic enrollment, canonical completion/freshness consumer, full graph transition/rollback and disposition remain open. No whole-story task is marked complete.

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

- [ ] Evaluate `src/Hexalith.EventStore/Hexalith.EventStore.csproj` and `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj`, inspect linked `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`, `SignalROptions.cs`, `ProjectionChangedHub.cs`, and `IProjectionChangedClient.cs`, and record the graph/contract/authorization baseline in new `_bmad-output/implementation-artifacts/evidence/story-2-13/qualification.md`.
- [ ] Add the two-candidate operation/profile matrix to `_bmad-output/implementation-artifacts/evidence/story-2-13/qualification.md`, including negotiation/group/replica semantics, credentials, versions, supported envelope, gaps, and unresolved rows.
- [ ] Inspect `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` and add a design-specific two-host/independent-sidecar proof at `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionRuntimeProofTests.cs`; record the selected fixture/component paths before implementation and retain logical projection evidence in the qualification packet. Do not rewrite the existing Redis baseline as replacement proof.
- [ ] In `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionRuntimeProofTests.cs`, implement and run signal/detail, reconnect/rejoin/restart, duplicate/out-of-order, outage/recovery, metadata-bound, and tenant-denial scenarios; record exact commands/results and scenario-to-evidence identities in `_bmad-output/implementation-artifacts/evidence/story-2-13/qualification.md`.
- [ ] Retain the evidence-backed design or exact-gap decision in `_bmad-output/implementation-artifacts/evidence/story-2-13/qualification.md`; update `docs/architecture/dapr-infrastructure-exceptions.yaml` only for a separately accepted exact-scope exception, otherwise keep the row unresolved with its owner/trigger.
- [ ] After the qualified design and execution gates pass, change `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs` and `SignalROptions.cs`, both host `.csproj` files, `src/Hexalith.EventStore.AppHost/Program.cs`, `src/Hexalith.EventStore.AppHost/DaprComponents/`, and `deploy/dapr/subscription-projection-changed.yaml` plus explicitly selected component/profile files as one reviewed transition; enumerate those final component files in the packet, prove rollback in `SignalRDaprDistributionRuntimeProofTests.cs`, and retire Redis only after that proof.
- [ ] Publish `_bmad-output/implementation-artifacts/evidence/story-2-13/qualification.md`, update `docs/concepts/dapr-infrastructure-boundary.md` and `docs/architecture/dapr-infrastructure-exceptions.yaml` with the proven disposition and 3.17 handoff, and update this story plus `sprint-status.yaml` only when the completion gates pass. Keep historical packets/fixtures/validators unchanged.

## Verification Handoff

Inspect `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` and existing SignalR/Gateway tests as baseline inputs. Run the relevant projects individually; for xUnit v3, build the project then invoke its executable with `-class`/`-method` when a focused filter is needed. Record the exact future commands after the harness/design is chosen; do not present these baseline files as a passing run or as Dapr-replacement proof.

Selected-path tests must exercise application operations through Dapr and inspect logical persisted projection values through the proper Dapr state/actor boundary. Explicit setup/fault/physical-diagnostic tooling stays isolated from shipped paths and cannot substitute for Dapr correctness evidence. External services/resources are not provisioned by this backlog artifact.

## Verification — October 25 Unchanged Local Continuation

Source revision: `749767072b8c2ea2abc8a23540ca65471d078c49`. CI run link: none;
these are local checks, and no CI run, approval or production authority is supplied.
New results follow architecture AD-12's October 23 story-file policy. Historical
packets were not restored or rewritten. The deleted required restart report was
read through `git show bbfd0fada14ce11e137365637238af4fec2f7f7f:_bmad-output/implementation-artifacts/evidence/story-2-13/qualification-2026-10-21-restart-trace.md`.

No application, fixture, runtime, dependency or production configuration changed.
The selected fixture-only executable is
`/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`, version
`1.18.7-local-2-13-retirement`, SHA-256
`a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`.
Installed daprd remains `1.18.7`, SHA-256
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`;
Dapr CLI is `1.18.2`, SDK `10.0.402`, runtime `.NET 10.0.13` and the xUnit
in-process runner `4.0.2`. Go requalification is unrun: `go version` exits 127
(`go: command not found`), and the retained disposable source tree lacks `go.mod`.
Partial retained runtime sources and executables remain untouched.

The candidate's historical upstream revision is
`b32a815fa2260983e181b95272ea555fe17d3895`. The following read-only Git references
remain available at the revision immediately before the approved evidence deletion:

```sh
git show b7ba9bb1fdff85fb47817d35506bd6ca81b23d70:_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-concurrency-retirement/candidate-from-upstream.patch
git show b7ba9bb1fdff85fb47817d35506bd6ca81b23d70:_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-concurrency-retirement/candidate-runtime-identity.json
git show b7ba9bb1fdff85fb47817d35506bd6ca81b23d70:_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-concurrency-retirement/retirement-runtime-build.json
git show b7ba9bb1fdff85fb47817d35506bd6ca81b23d70:_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-runtime-recovery/runtime-acquisition.json
git show b7ba9bb1fdff85fb47817d35506bd6ca81b23d70:_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-runtime-recovery/go.mod.source
git show b7ba9bb1fdff85fb47817d35506bd6ca81b23d70:_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-runtime-recovery/go.sum.source
```

The upstream patch SHA-256 is
`f4981813f47bcbb8779cf7e5578db74ca0ec19b7841a615581df7fda7f31d515`;
the manifest hashes are `go.mod` =
`d218290004fa0583025e5a21d6478e6c1dfa07953102eb0288a0cdf433ae50d8`
and `go.sum` =
`0a7d34bc98a876eddfe1fc6e1b4ba7cb2b50d4f0b0c87a0b35493f9915181ce0`.
The historical identity contains complete compiled dependency records, including
grpc-go `v1.83.2`, and Go `1.26.9` executable SHA-256
`e90c4e12df236ea0c2e7536fd68da8a47fc0b585c19588bf6c1a70cfc296a0f9`.
Acquisition records bind the Go archive to
`42d158b4d8f7b61ac0a830567c940a86098fb7aac52e467a5ebec03ef5cc2f8d`
and the Dapr source archive to
`43c5df99b6f9ecfac15313c49728969d8fc2ea9d13b4cb6777725311e496ba50`.
The build receipt's `.command` field retains the exact build argv, including
`GOTOOLCHAIN=local`, `CGO_ENABLED=0`, `-mod=readonly`, `-p=4`, `-tags=allcomponents`
and all linker assignments. These are historical records, not a current build.
The named Go executable, both acquired archives, and the candidate tree's
`go.mod`/`go.sum` are now absent; the dependency cache was not revalidated.
The incomplete tree cannot reproduce the runtime today. No inputs were reconstructed
and no current runtime source correspondence or reproducibility is claimed.

The initial no-build Aspire start failed because Debug outputs were absent.
After compilation, the Linux `docker` shim failed its prerequisite check. Read-only
inspection found Windows `docker.exe` already connected to Docker Desktop
`29.8.2`, with the existing local Dapr Redis, placement and scheduler services
reachable. A temporary `/tmp/story-2-13-20261025-cli/docker` adapter executes
`"/mnt/c/Program Files/Docker/Docker/resources/bin/docker.exe" "$@"`; only these
commands prepend its directory to `PATH`. No daemon was installed or started,
container settings changed, or production cluster accessed.

Support identities were inspected retrospectively at `06:33:16–06:34:02 UTC`,
after all test commands. `docker.exe inspect dapr_redis dapr_placement dapr_scheduler`
and `docker.exe image inspect` supplied the following container/image bindings:

| Service | Observed version | Configured image | Observed repository digest |
| --- | --- | --- | --- |
| `dapr_redis` | `6.2.21` from `docker.exe exec dapr_redis redis-server --version` | `docker.io/redis:6` | `redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf` |
| `dapr_placement` | `1.18.7` from retained service startup log markers | `docker.io/daprio/dapr:1.18.7` | `daprio/dapr@sha256:0ac4fadf0016be85065cdebab952a8350e468cf9283d26777b0307a14a10aecc` |
| `dapr_scheduler` | `1.18.7` from retained service startup log markers | `docker.io/daprio/dapr:1.18.7` | `daprio/dapr@sha256:0ac4fadf0016be85065cdebab952a8350e468cf9283d26777b0307a14a10aecc` |

The startup-log inspection used
`docker.exe logs --since 2026-10-24T11:35:00Z --until 2026-10-24T11:36:00Z dapr_placement`
and the same command naming `dapr_scheduler`; both exposed `ver=1.18.7`.
Placement does not support `--version` (that read-only probe exited 2).
`aspire --version` reported `13.6.1+3751e615ad660621f2a7aa4c390ef48e08a0eed8`;
`/home/administrator/.aspire/bin/aspire` SHA-256 was
`82d2aacd2432040cb3f9391e39e3ea7ed3d6e6c0143c4e0dfe38cfbb04b02bc0`.
Container start times precede the run, but no pre-run image IDs, digests or Aspire
CLI hash were captured. These observations identify currently inspectable support
artifacts and cannot retroactively prove their identity during the tests.

The compiled baseline ran using
`PATH="/tmp/story-2-13-20261025-cli:$PATH" EnableKeycloak=false aspire run --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach --no-build --non-interactive --format Json`.
`aspire wait eventstore --timeout 45 --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`
passed, and sanitized `aspire describe` showed EventStore, sample and admin
resources running while the unbuilt Tenants host was Finished. This is a partial
local topology, not a full-graph or production pass. The owned baseline stopped
through explicit-AppHost `aspire stop` before the composed projection/Direct pair.
For subsequent distribution checks the exact auxiliary sequence was:

```sh
PATH="/tmp/story-2-13-20261025-cli:$PATH" EnableKeycloak=false aspire run --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach --no-build --non-interactive --format Json
aspire wait sample --timeout 45 --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire wait sample-dapr-cli --status up --timeout 45 --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire resource eventstore stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire resource eventstore-dapr-cli stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
```

The start output is `/tmp/story-2-13-20261025-runtime-support-start.log` (AppHost PID
55697, CLI PID 55617). Wait/stop success acknowledgements remain in this session's
tool history; separate file receipts for those four commands were not retained.
`aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive`
was reduced to `/tmp/story-2-13-20261025-support-state.json` at `06:19:36.591365 UTC`,
during the distribution class. It records `sample-tzvbmepv` and
`sample-dapr-cli-gjxpkesf` Running, `eventstore-nebtddpp` and
`eventstore-dapr-cli-uhxupama` Finished, and `tenants-jxdpqcnu` Finished. This
corroborates the auxiliary topology during that run without supplying an absent
pre-scenario receipt.

| Exact command | Result |
| --- | --- |
| `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Failed: four existing CS0618 errors in the root-declared Tenants dependency: three `ActorException` uses in `TenantReadAuthorityQueryHandler.cs`, one `ActorResponseMismatch` use in `GetTenantAuditQueryHandler.cs`. No suppression or dependency change. |
| `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:BuildProjectReferences=false --no-restore` | Initial fallback failed with CS0006 for the missing AppHost reference assembly; passed after the following focused AppHost build, with 0 warnings/errors. This does not pass the failed dependency graph. |
| `dotnet build src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:BuildProjectReferences=false --no-restore` | Passed, 0 warnings/errors. |
| `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Passed, 0 warnings/errors. |
| `tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests -class '*ETagActorTests' -class '*SignalRProjectionChangedBroadcasterTests' -class '*DaprProjectionChangeNotifierTests' -class '*DaprProjectionChangeNotifierSignalRTests' -class '*ProjectionChangedNotificationValidatorTests' -class '*DomainServiceWorkloadAssertionHandlerTests'` | 102 passed, 0 failed/skipped/errors; 0.581 s. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprProjectionDispatchProofTests' -class '*ValidProjectionRoundTripE2ETests'` | 2 passed, 0 failed/skipped/errors; 147.067 s; `06:12:52.091054–06:15:19.825513 UTC`. Real two-host signal/detail, persisted query readback and tenant denials, plus ordinary Direct projection readback. Captured host/integration/AppHost/runtime hashes remained stable. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -method '*DaprRedisStreams_ReplicaRestart_ReauthorizesAndReconcilesPersistedState'` | 1 passed, 0 failed/skipped/errors; 82.434 s; `06:16:53.932644–06:18:16.987218 UTC`. Captured host/integration/AppHost/sample/runtime assemblies and deps remained stable. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprDistributionRuntimeProofTests'` | 6 passed, 0 failed/skipped/errors; 323.480 s; `06:18:36.219086–06:24:00.324991 UTC`, without `-stopOnFail`. Reused/fresh endpoint restart, provider reset/blackhole, exact-topic catalog and scoped delivery/readback all passed; captured assemblies/deps/runtime identities remained stable. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRRedisBackplaneRuntimeProofTests'` | 1 passed, 0 failed/skipped/errors; 178.183 s; `06:24:15.065871–06:27:13.910424 UTC`. Captured assemblies/deps/runtime identities remained stable; the historical ambiguous measurement is unchanged. |
| `bash scripts/check-committed-files.sh` | Passed, 9,029 tracked files checked. |
| `git diff --check` | Passed. |

The pre-build observation `/tmp/story-2-13-20261025-before-build.json` found no
files in the selected host, AppHost and integration Debug directories; it did not
inventory the separate sample or Server output directories. The failed integration
graph build reported host, sample and Server outputs before failing in Tenants.
The subsequent successful Server.Tests build also reported host and Server outputs.
The focused AppHost and integration builds created their previously missing target
assemblies with `BuildProjectReferences=false`, reusing the available referenced
outputs. These logs are not compiler invocation/source inventories and do not prove
that every referenced assembly was rebuilt from the stated source revision.

The key tested output hashes below come from the matching before/after entries in
`/tmp/story-2-13-20261025-focused-reused.json`,
`/tmp/story-2-13-20261025-full-distribution.json` and
`/tmp/story-2-13-20261025-retained-redis.json`. They agree across all three receipts.
Paths are relative to the project directory's `bin/Debug/net10.0/`:

| Project directory / output | SHA-256 |
| --- | --- |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.dll` | `533afaef2194a6d9353639b13d5edecc06cfb54dd25dfd308d3f0dd41ced9bf9` |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.deps.json` | `3f179472439522e23160c53799a5db3d694a866a17089c7be2c575bf82312beb` |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.Server.dll` | `bd99c5ef167b3a3c3ccb91e54e482a1d092bf6eb721f92df65f6f24f37e69847` |
| `samples/Hexalith.EventStore.Sample` / `Hexalith.EventStore.Sample.dll` | `b115a99733bc204c9a028e914ee06a816dd4bdde240b9164539e1db4932beb66` |
| `samples/Hexalith.EventStore.Sample` / `Hexalith.EventStore.Sample.deps.json` | `231e7d0b311eb55a2693b79c88e63a79393d584a1238db6c7c6b9b45c3f46538` |
| `tests/Hexalith.EventStore.IntegrationTests` / `Hexalith.EventStore.IntegrationTests.dll` | `218750c0f40929d5adb9a87f0793f59107ee0bbe7dc49c93a33972549e3a9ca7` |
| `tests/Hexalith.EventStore.IntegrationTests` / `Hexalith.EventStore.IntegrationTests.deps.json` | `f9fdd5d585d2d60782a67f09f9f5f68717f364e97c73daa29c7b5ed9ad6f071e` |
| `src/Hexalith.EventStore.AppHost` / `Hexalith.EventStore.AppHost.dll` | `5205a219c2b58cf62c3b3667b02183644b40f368e4d1c5319f3acfb9bafa2812` |
| `src/Hexalith.EventStore.AppHost` / `Hexalith.EventStore.AppHost.deps.json` | `1db1fae3dfafb0ec5252cf317baab996a925e47b91b0ae8ab60161a79d90c7c1` |

The integration directory's `Hexalith.EventStore.Server.dll` copy has the same
`bd99c5ef167b3a3c3ccb91e54e482a1d092bf6eb721f92df65f6f24f37e69847` hash.
No separate Server dependency-file hash or runtime module-load inventory was
captured; the host dependency-file identity is listed instead. The earlier
projection/Direct pair receipt captured host,
Server, integration and AppHost DLLs with these same hashes, but omitted deps files
and the actual sample output directory. Later captures do not repair that earlier
coverage gap. Before/after stability establishes unchanged measured bytes; it does
not establish their correspondence to all current sources or a reproducible build.

Integration commands above use the temporary Docker CLI adapter and
`HEXALITH_STORY_2_13_DAPRD_PATH=/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`.
The composed fixture's own AppHost and the ordinary Direct path retain the installed
runtime; the explicit selection applies to the two distribution proof sidecars.
The Redis command also sets `R10A2_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-redis-output`,
so its existing runtime capture writes outside the repository. Local command output
and before/after hashes remain temporary run output, not a new committed packet.

Read-only examination of the historical failed projection/Direct logs finds
`Named projection metadata refresh failed for 'sample/v1/counter'` before projection
state exists, and metadata denials with reason `issuer-invalid`. Current source
registers the workload-assertion handler on every named factory client, including
the per-app metadata client. These observations narrow the prior failed hop but do
not establish its cause. Current passes cannot erase those failures, prove a fix,
or resolve the historical Redis identity ambiguity. No behavioral correction is
claimed.

Cleanup used
`aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`;
the following explicit-AppHost `aspire describe` found no running AppHost. A
read-only workspace product-process scan at `06:27:48.806899 UTC` found no selected
proof host or candidate sidecar remaining. The temporary Docker CLI adapter was
removed. The inotify ceiling stayed at its original `1,048,576` throughout; an
accessible per-fd scan found 2,590 watch lines (9,114 files read, 71 unreadable
entries), so no restoration or other sysctl write was needed. Unrelated processes
and the shared Dapr containers were preserved. Final installed/candidate runtime
hashes still match those above, and the spec's frozen block is byte-for-byte
unchanged.

Remaining gates are the failed complete source-reference dependency graph, current
Go/source concurrency requalification, the unexplained historical regression and
identity failures, a CI result, the selected production runtime/durable broker/enrolled
issuer, shipped topic enrollment, canonical completion/freshness fault behavior,
atomic both-host transition/rollback and authorized disposition. The earlier Contracts
validator failure was not rerun or reclassified, and no validator exemption was added.
Story 2.16 retains NFR5 primary ownership. This story remains backlog, the active
spec/sprint remain in-progress, whole-story tasks remain unchecked, and
`accepted_exceptions: []` is unchanged.

## Verification — October 25 Current-Workspace Requalification

Source revision: `aa46e2526ab33b2f4b220478c8acc097f7ad01b0`. CI run link: none;
these are local checks only. The workspace already had different Platform and
Tenants checkouts from its recorded gitlinks, at
`eecea3fa467d27226ef6173c19f853add36bb26c` and
`c5291bb7648a5c1d77f1c2d3a08c9cae034eca93` respectively. They were preserved.
Builds was `1783c0a377615063f3222c1d28f3ab513af4e72f`. This task changed no
application, fixture, runtime, dependency, production configuration, frozen spec
or status; its implementation edit is limited to this Verification section.
Subsequent review logging belongs to the active spec.

The current source graph was rebuilt separately before the scenarios because its
revision differs from the earlier qualification. The complete Debug source-reference
solution build passed, including both host graphs, with zero warnings and errors.
Its measured before/after inventory records 56 changed output files and no measured
source or runtime executable changes. This current-workspace result does not repair
the earlier failed build or establish the cause of any historical regression. It
is an incremental build, not a clean reproducibility or compiler-input proof.

All commands below ran sequentially. Before and after each scenario command, the
local recorder measured 12,490 source/configuration files across the root and
root-declared submodules, 545 assembly/deps/runtimeconfig/executable files in the
selected host, Gateway, Server, sample, AppHost and test output directories, and the
candidate/installed daprd and Aspire executables. Those measured bytes and HEAD
remained stable during all five test commands. The pre-build source inventory
covered the root plus Builds, Tenants and Commons; the broader root-declared
submodule inventory began before the scenario runs. These observations establish
stability between captures, not atomic launch identity or complete reproducibility.

After the test runs and cleanup, concurrent workspace activity advanced HEAD to
`5be0c309f457d0af1f169bc591609fb4be6473ce` and the Platform checkout to
`7600292aa7dce6ad710b81b54e996d40daa55e49`. The parent commit's only tracked
difference from the tested revision is the Platform gitlink; 75 measured Platform
source/configuration paths changed after the final runtime cleanup. Other measured
sources differ only by this Verification append, while measured binaries and
runtime executables remain unchanged. This task did not commit, stage, push or
update that pointer. Results remain bound to the run-start revision and earlier
checkout bytes; no new runtime run at the later HEAD or Platform checkout is claimed.

The fixture-only runtime remained
`/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`, version
`1.18.7-local-2-13-retirement`, SHA-256
`a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`.
The installed `1.18.7` daprd remained
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.
SDK `10.0.402`, .NET `10.0.13`, Dapr CLI `1.18.2`, Aspire CLI
`13.6.1+3751e615ad660621f2a7aa4c390ef48e08a0eed8` and xUnit runner `4.0.2`
were observed. The candidate's upstream/reconstruction limitations recorded above
still apply: `go version` exits 127 (`go: command not found`), and a read-only
`rg --files` search of the retained disposable tree finds no `go.mod` or `go.sum`.
Go concurrency/source requalification remains unrun. No runtime was rebuilt or
installed and no missing input was reconstructed.

Pre-test support inspection at `08:59:59.454015 UTC` bound Redis `6.2.21` to
container `8d0788f8322fdb91a51f3960cb9a0ceab4a7b892f27bacf2147e97fc4d471922`,
placement to `f1e0a30a3a93a1542ee5ad74856e44d483a94f7a8e5ef769f398827815cc1da6`,
and scheduler to `8623af8b7bbd8ebd3d73221af72615162475f3f8a7190432e09e64e51befc826`.
Their configured images were `redis:6` and `daprio/dapr:1.18.7`; their image IDs
and repository digests matched the earlier support table and the post-test
inspection. No container or daemon was created, stopped or reconfigured.

The initial baseline used the existing Debug outputs: `aspire run --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach
--no-build --non-interactive --format Json`, followed by `aspire wait eventstore
--timeout 45 --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
--non-interactive` and `aspire describe --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json
--non-interactive`. EventStore, sample and Tenants were Running; the sample-api
sidecar was Finished, so this was not a full-topology health claim. Baseline
AppHost PID `10161` and CLI PID `10134` stopped through explicit-AppHost
`aspire stop` before the build.

After the build, distribution support used the same exact auxiliary sequence
shown above: detached no-build `aspire run`, `aspire wait sample --timeout 45`,
`aspire wait sample-dapr-cli --status up --timeout 45`,
`aspire resource eventstore stop`, then `aspire resource eventstore-dapr-cli stop`,
each with the explicit AppHost and `--non-interactive`. All five commands have
separate local receipts. Auxiliary AppHost PID `51399` and CLI PID `51362` owned
this topology. The in-run `aspire describe` observation at `09:03 UTC` recorded
`sample-mrsxrmya` and `sample-dapr-cli-wkpffeat` Running, with `eventstore-ywdmgvzg`
and `eventstore-dapr-cli-uerhtvdc` Finished. It stopped through explicit-AppHost
`aspire stop` before the projection/Direct pair, whose fixtures owned their own
AppHosts and retained the installed runtime.

Every command's environment prepended
`/tmp/story-2-13-20261025-requalification-kv3ylsvi/cli` to `PATH` and set
`EnableKeycloak=false`. Its temporary Docker adapter used the existing Windows
`docker.exe` exactly as in the earlier continuation. Integration commands selected
`HEXALITH_STORY_2_13_DAPRD_PATH=/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`;
this selection applies only to the two proof sidecars. Fixture captures used
`HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-requalification-kv3ylsvi/scenario-output`
and Redis output used
`R10A2_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-requalification-kv3ylsvi/redis-output`.
Commands, logs and before/after observations remain temporary local run output in
that parent directory, not a committed packet or CI result.

| Exact command | Result and UTC interval on 2026-10-25 |
| --- | --- |
| `dotnet build Hexalith.EventStore.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Passed, 0 warnings/errors; `08:59:17.615447–09:01:16.158826 UTC`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -method '*DaprRedisStreams_ReplicaRestart_ReauthorizesAndReconcilesPersistedState'` | 1 passed, 0 failed/skipped/errors; 43.156 s; `09:02:11.399174–09:02:54.745301 UTC`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprDistributionRuntimeProofTests'` | 6 passed, 0 failed/skipped/errors; 142.652 s; `09:02:59.598099–09:05:22.450789 UTC`; no `-stopOnFail`. |
| `tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests -class '*ETagActorTests' -class '*SignalRProjectionChangedBroadcasterTests' -class '*DaprProjectionChangeNotifierTests' -class '*DaprProjectionChangeNotifierSignalRTests' -class '*ProjectionChangedNotificationValidatorTests' -class '*DomainServiceWorkloadAssertionHandlerTests'` | 102 passed, 0 failed/skipped/errors; 0.515 s; `09:05:39.476305–09:05:40.375393 UTC`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprProjectionDispatchProofTests' -class '*ValidProjectionRoundTripE2ETests'` | 2 passed, 0 failed/skipped/errors; 63.483 s; `09:05:49.754751–09:06:53.467884 UTC`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRRedisBackplaneRuntimeProofTests'` | 1 passed, 0 failed/skipped/errors; 179.917 s; `09:07:15.192857–09:10:15.298195 UTC`. |
| `bash scripts/check-committed-files.sh` | Passed; 9,036 tracked files checked. |
| `git diff --check` | Passed. |

The generated Redis report `redis-output/evidence-2026-10-25-091015Z.md` includes
the static template `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter SignalRRedisBackplaneRuntimeProofTests`.
That template was not executed. The direct executable invocation in the table
and `retained-redis.json` is authoritative for this run; retained report and
historical bytes remain unchanged.

The six distribution cases cover reused/fresh internal endpoint restart, provider
reset/blackhole, exact-topic catalog rollout, and scoped signal/detail with Dapr
persisted readback. The real projection case separately proves signal/detail and
equal authoritative projection readback from both hosts, plus tenant denials.
The profile remains test-only `pubsub.redis` v1 with `consumerID: "{uuid}"`,
`state.redis` v1, exact `dapr.io/v2alpha1` subscriptions, shared app ID `eventstore`
and independent sidecars. No shipped wildcard subscription, production topic
enrollment, broker or profile was selected. The bounds, authorization, caller
budgets and existing 15-second distribution signal wait were unchanged.

The retained real-projection `manifest.json` in
`scenario-output/real-projection-distribution-20261025T0906412847533Z-4ef691a79c0443c8972c69179f497967/`
records `09:06:41.2847533–09:06:45.7642255 UTC`, command `Completed`, event count
`1`, and `ProjectionBacked` query provenance. In this redacted summary, A and B
alias the two synthetic tenants; their tenant, topic, aggregate and correlation
values are omitted. The connection columns bind each client to its owning host:

| Proof node | Host / sidecar PID | Tenant A connection | Tenant B connection | Persisted count | Authoritative ETag |
| --- | --- | --- | --- | --- | --- |
| First | `48137` / `48234` | `SXC0E2sChdZ61sdAvCI7kw` (`FirstConnectionId`) | `C2dpsPsFXk1PzA2AhuqKLg` (`TenantBFirstConnectionId`) | `1` | `"Y291bnRlcg.1cR6FV4HOUq3ZT10jiy1kg"` |
| Second | `48657` / `48760` | `O0VwdvEQKOtB2wnhp9Nubw` (`SecondConnectionId`) | `TJ7xhDEJYhTANgh1i71wcQ` (`TenantBSecondConnectionId`) | `1` | `"Y291bnRlcg.1cR6FV4HOUq3ZT10jiy1kg"` |

Both authorized A clients received signal and scoped detail. The manifest records
`OtherTenantReceivedSignalOnEitherHost=false` and
`OtherTenantReceivedDetailOnEitherHost=false`; neither B client received A's
signal or detail. Its limits remain exact-topic Development profile, optional
legacy projection-response detail, no production issuer/profile, no dynamic-topic
coverage, and no transition or rollback approval.

That same real-projection manifest binds these generated configuration bytes:

| Generated file | SHA-256 |
| --- | --- |
| `pubsub.yaml` | `483c2de2d90aa876a80efe6499edc98a82903a46ddd98a79a9f610ec2719b001` |
| `statestore.yaml` | `8cfd8564018ac754f5d3f14acfc215f3d2caf186cb1e14b7b377b1d4660d023a` |
| `subscription-0.yaml` | `1725943f8491ff88c78a786d6591d7072873fb40e9049249ecd8258139ba3509` |

These identities apply to this scenario only; generated configuration bytes can
differ across the other scenarios.

Key output SHA-256 values below agree before/after every test command. Paths are
relative to each project's `bin/Debug/net10.0/`; the integration-directory copies
of the host, Server, sample and AppHost assemblies match their owning outputs.
The Server.Tests rows come from matching before/after entries in
`focused-server.json` and identify the separate runner that executed the 102 tests.

| Project / output | SHA-256 |
| --- | --- |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.dll` | `dfd8b7d1b735ca1832373eb24919cfea5150a7a86ffa87b53f48bc51904c5231` |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.deps.json` | `89e89b25a82349d764ede5a7cf238e713c34cce9bbdb1900f5e07c5034379388` |
| `src/Hexalith.EventStore.Server` / `Hexalith.EventStore.Server.dll` | `48c1442630a3a96461a32317171e1515547baedfbcbf9e35a25c451c81d0ae1b` |
| `samples/Hexalith.EventStore.Sample` / `Hexalith.EventStore.Sample.dll` | `5cbfb0c3f902c5d33311c86b0e82a2d2a301f3492fa36332b8b2dda85905668e` |
| `samples/Hexalith.EventStore.Sample` / `Hexalith.EventStore.Sample.deps.json` | `231e7d0b311eb55a2693b79c88e63a79393d584a1238db6c7c6b9b45c3f46538` |
| `tests/Hexalith.EventStore.IntegrationTests` / `Hexalith.EventStore.IntegrationTests.dll` | `cbd543671baa347f6e51f7739e01451a96ab4c7716389d561f10814dfb746256` |
| `tests/Hexalith.EventStore.IntegrationTests` / `Hexalith.EventStore.IntegrationTests.deps.json` | `f9fdd5d585d2d60782a67f09f9f5f68717f364e97c73daa29c7b5ed9ad6f071e` |
| `src/Hexalith.EventStore.AppHost` / `Hexalith.EventStore.AppHost.dll` | `ebe6e60b686ded767fd2ab6f37c57ad02df8dc075b01265437dca29579502239` |
| `src/Hexalith.EventStore.AppHost` / `Hexalith.EventStore.AppHost.deps.json` | `1db1fae3dfafb0ec5252cf317baab996a925e47b91b0ae8ab60161a79d90c7c1` |
| `tests/Hexalith.EventStore.Server.Tests` / `Hexalith.EventStore.Server.Tests.dll` | `fcd1d2765a3c4408e7550bc78a5c0e12214fe69a9a37cdc28efcb0b0c09a0328` |
| `tests/Hexalith.EventStore.Server.Tests` / `Hexalith.EventStore.Server.Tests.deps.json` | `902d267452353b89570dbb43da8eb4aae9ee82fa1b0e7f2771aff9ac4d3e83ac` |

Final explicit-AppHost `aspire stop` and `aspire describe` found no running
AppHost. A read-only independent product-process scan at `09:10:52.649891 UTC`
found no proof host, selected candidate sidecar or installed EventStore/sample
sidecar remaining. The shared Dapr containers retained their pre-test identities.
The temporary Docker adapter was removed. The inotify ceiling remained
`1,048,576` at every command boundary; the final accessible scan found 2,617 watch
lines across 8,698 readable fdinfo files, with 40 inaccessible entries. No sysctl
write or unrelated-process interruption occurred.

No historical failure was reproduced in these current checks, so no behavioral
correction or explanation is claimed. The earlier projection failures and Redis
binary-identity ambiguity remain historical unresolved measurements. Current Go
concurrency/source requalification and CI remain absent; the separate Contracts
validator blocker was not rerun, reclassified or exempted. Production runtime,
durable broker, binding-capable issuer, shipped enrollment, canonical freshness
fault behavior, atomic both-host transition/rollback and authorized disposition
remain gated. Original story `backlog`, active spec/sprint `in-progress`, unchecked
whole-story tasks and `accepted_exceptions: []` remain unchanged.

## Verification — October 25 Current-Revision Continuation

Source revision: `340b1d1a9f5f259d70ade0aa88e969d5a5e855c5`. CI run link: none;
these are local checks under AD-12, with no CI, approval or production authority.
The working tree was clean at run start. HEAD and all root-declared submodule
checkouts stayed unchanged through the build and five runtime test commands; Builds was
`1783c0a377615063f3222c1d28f3ab513af4e72f`, Commons
`1663028fb6d9f46a0bf17919edeb21bb99be1587`, Tenants
`14133e96d2c5efb13270580f920133079707398b` and Platform
`1b7a00381794bb7ecf40caf643c63c50758a81eb`. Implementation changes are limited to
this Verification section; the active spec separately records the continuation's
review log. Neither changes application, fixture, runtime, dependency or deployment
sources, frozen intent, statuses or exception register.
This task staged or committed no files and updated no branch or submodule.

After the tests and cleanup, concurrent activity advanced HEAD to
`245ded36b78b960b57728f845f9a8aae62275908`. The `09:52:37 UTC` observation found
nine measured product/test source paths changed by that separate projection-work
commit, plus this Verification append; measured binaries and runtime executables
were unchanged. The new commit also changes README outside the selected source
inventory. This task preserved that work. The results below remain bound to
`340b1d1a9f5f259d70ade0aa88e969d5a5e855c5`; no runtime run at the later HEAD or
correspondence between its changed sources and the tested binaries is claimed.

The complete Debug source-reference solution build passed with zero warnings and
errors. The before/after inventory found 57 changed selected output files and no
measured source or runtime executable changes. At every command boundary, the
local recorder measured 12,553 source/configuration files across the root and its
root-declared submodules, 545 assembly/deps/runtimeconfig/executable files in the
selected host, Gateway, Server, sample, AppHost and test directories, and the
installed daprd, selected daprd and Aspire executables. All measured bytes stayed
stable during the five test commands below. These are incremental build and
before/after observations, not clean reproducibility, complete compiler-input or
atomic launch-identity proof. Historical failed builds and binary-identity
measurements remain unchanged.

The explicitly selected fixture-only runtime remained
`/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`, version
`1.18.7-local-2-13-retirement`, SHA-256
`a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`.
Installed daprd `1.18.7` remained
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`;
Aspire CLI remained
`82d2aacd2432040cb3f9391e39e3ea7ed3d6e6c0143c4e0dfe38cfbb04b02bc0`.
Observed tools were SDK `10.0.402`, .NET `10.0.13`, Dapr CLI `1.18.2`, Aspire
`13.6.1+3751e615ad660621f2a7aa4c390ef48e08a0eed8` and xUnit runner `4.0.2`.
`go version` again exited 127 (`go: command not found`), and
`rg --files /tmp/eventstore-2-13-retirement-oduc267u -g 'go.mod' -g 'go.sum'`
returned no matches. Current Go concurrency/source requalification remains unrun;
the earlier reconstruction limitations still apply. No missing inputs were
reconstructed and no runtime was rebuilt or installed.

The build and five test commands ran sequentially; read-only inspections,
including the in-distribution `aspire describe`, also ran while tests were active.
The recorded build, lifecycle and test environments set `EnableKeycloak=false` and
prepended `/tmp/story-2-13-20261025-current-l8gqsdns/cli` to `PATH`. Its temporary
`docker` adapter executed the existing
`/mnt/c/Program Files/Docker/Docker/resources/bin/docker.exe` with unchanged
arguments. Integration commands set
`HEXALITH_STORY_2_13_DAPRD_PATH=/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`,
`HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-current-l8gqsdns/scenario-output`
and `R10A2_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-current-l8gqsdns/redis-output`.
The selected runtime applies only to the two distribution proof sidecars; the
composed AppHost and ordinary Direct fixture retain installed daprd. Commands,
logs and observations remain temporary local output under that parent directory,
not committed evidence or a CI result.

Baseline lifecycle used the following exact commands before the build:

```sh
aspire run --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach --no-build --non-interactive --format Json
aspire wait eventstore --timeout 45 --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json --non-interactive
aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
```

Baseline AppHost PID `64442` and CLI PID `64079` started successfully; inspected
project and sidecar resources were Running/Healthy. This local observation grants
no production-profile qualification. After the build, the same detached no-build
start created auxiliary AppHost PID `3588` and CLI PID `3151`, followed by:

```sh
aspire wait sample --timeout 45 --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire wait sample-dapr-cli --status up --timeout 45 --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire resource eventstore stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
aspire resource eventstore-dapr-cli stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
```

Each start/wait/stop has a separate local command receipt. At `09:42:53 UTC`,
during the distribution class, `aspire describe` recorded `sample-cyqkvsmk` and
`sample-dapr-cli-mpfrczfc` Running/Healthy, with `eventstore-uhkfjatz` and
`eventstore-dapr-cli-pdcezqbr` Finished. Explicit-AppHost `aspire stop` ended the
auxiliary topology before the projection/Direct pair, whose fixtures owned their
own AppHosts.

Pre-test support inspection at `09:34:38 UTC` and post-test inspection at
`09:48:19 UTC` returned the same container IDs, image IDs and start times:
Redis `8d0788f8322fdb91a51f3960cb9a0ceab4a7b892f27bacf2147e97fc4d471922`,
placement `f1e0a30a3a93a1542ee5ad74856e44d483a94f7a8e5ef769f398827815cc1da6`
and scheduler `8623af8b7bbd8ebd3d73221af72615162475f3f8a7190432e09e64e51befc826`.
Their configured images and pre-test repository digests matched the earlier support table
(`redis:6` / `c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`,
`daprio/dapr:1.18.7` / `0ac4fadf0016be85065cdebab952a8350e468cf9283d26777b0307a14a10aecc`).
No shared container or daemon was created, stopped or reconfigured.

| Exact command | Result and UTC interval on 2026-10-25 |
| --- | --- |
| `dotnet build Hexalith.EventStore.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Passed, 0 warnings/errors; `09:35:21.344499–09:39:10.622712 UTC`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -method '*DaprRedisStreams_ReplicaRestart_ReauthorizesAndReconcilesPersistedState'` | 1 passed, 0 failed/skipped/errors; `09:40:03.132763–09:40:52.943200 UTC`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprDistributionRuntimeProofTests'` | 6 passed, 0 failed/skipped/errors; `09:41:00.248772–09:43:22.970352 UTC`; no `-stopOnFail`. |
| `tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests -class '*ETagActorTests' -class '*SignalRProjectionChangedBroadcasterTests' -class '*DaprProjectionChangeNotifierTests' -class '*DaprProjectionChangeNotifierSignalRTests' -class '*ProjectionChangedNotificationValidatorTests' -class '*DomainServiceWorkloadAssertionHandlerTests'` | 102 passed, 0 failed/skipped/errors; `09:43:34.725959–09:43:35.953143 UTC`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprProjectionDispatchProofTests' -class '*ValidProjectionRoundTripE2ETests'` | 2 passed, 0 failed/skipped/errors; `09:43:53.478650–09:45:00.653653 UTC`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRRedisBackplaneRuntimeProofTests'` | 1 passed, 0 failed/skipped/errors; `09:45:13.789260–09:48:04.678152 UTC`. |

The six distribution cases retain reused/fresh internal-endpoint restart,
provider reset/blackhole, exact-topic catalog rollout and scoped signal/detail
with Dapr persisted readback. Their test-only profile remains `pubsub.redis` v1
with `consumerID: "{uuid}"`, `state.redis` v1, exact `dapr.io/v2alpha1`
subscriptions, shared app ID `eventstore` and independent sidecars. Metadata
bounds, authorization, caller cancellation/readback budgets and the 15-second
signal wait are unchanged. No shipped topic enrollment or production profile is
selected. The generated Redis report's static `dotnet test ... --filter` template
was not executed; the direct executable command above is authoritative.

The focused and full-class reused-endpoint scenario manifests record initial
`ETagActor` ownership on the second proof host before stopping it, followed by
Dapr persisted version `1` → `2` and ETag `"1"` → `"2"` in both runs:

| Run | Initial second-host owner PID | Original → restored internal gRPC port |
| --- | --- | --- |
| Focused restart | `23909` | `46741` → `46741` |
| Full distribution class | `49400` | `41193` → `41193` |

Both manifests record `RenewInternalGrpcEndpoint=false`. This remains synthetic
state recovery, not real projection completion, canonical UI freshness,
production-profile qualification or transition/rollback approval.

The retained network manifests record 28 disconnected connections and 1,764
rejected reconnects for reset/refusal, and 4,961 discarded bytes for the silent
bidirectional blackhole. Both cases' reads on both proof hosts ended with
`OperationCanceledException` in about two seconds (2,001.5303–2,006.4609 ms),
with a 2,000 ms caller cancellation budget and below the 5,000 ms outer assertion bound.
After restore, Dapr reads returned versions `[1,1]` and ETags `["1","1"]`;
the final persisted version/ETag was `2` / `"2"` in both cases. Outage publication
remains unknown until reconciliation. These synthetic read-model checks prove no
real projection completion, canonical UI freshness, saturation/backpressure or
retention guarantee, production profile, or transition/rollback approval.

The real-projection capture records `09:44:47.5316445–09:44:52.6263983 UTC`,
command `Completed`, event count `1`, query provenance `ProjectionBacked`, and
scoped detail `counter-status` / `change-kind=counter-projection-updated`. A and B
below alias synthetic tenants; tenant, topic, aggregate and correlation values
are omitted. Both A clients received signal and detail;
`OtherTenantReceivedSignalOnEitherHost` and
`OtherTenantReceivedDetailOnEitherHost` were both false. All four other-tenant
signal/detail channels were checked concurrently with one-second bounded reads;
the negative observations cover those windows only.

| Proof node | Host / sidecar PID | Tenant A connection | Tenant B connection | Persisted count | Authoritative ETag |
| --- | --- | --- | --- | --- | --- |
| First | `47574` / `47675` | `wjWrtUVbY_MrE1A1xvtYuw` | `eYB1zxjPSy_W5RPoepCJpQ` | `1` | `"Y291bnRlcg.Sq5RSegP_0SXG2Ro7GCKDQ"` |
| Second | `48526` / `48581` | `94wEHMKN54FdkR3J6Wcqog` | `Fjyn-v6cTlMgdti2QLbNqg` | `1` | `"Y291bnRlcg.Sq5RSegP_0SXG2Ro7GCKDQ"` |

That scenario's generated bytes have SHA-256 `pubsub.yaml` =
`483c2de2d90aa876a80efe6499edc98a82903a46ddd98a79a9f610ec2719b001`,
`statestore.yaml` = `8cfd8564018ac754f5d3f14acfc215f3d2caf186cb1e14b7b377b1d4660d023a`
and `subscription-0.yaml` =
`cd62ad3ca42512990c5cc1683dfa43f570828836f6158945cb2418377379e9be`.
These identities apply only to this scenario. Its limitations remain the
exact-topic Development profile, optional legacy projection-response detail,
no production issuer/profile, no dynamic-topic coverage and no transition or
rollback approval.

Key output SHA-256 values agree before/after all five test commands. Paths below
are relative to each project's `bin/Debug/net10.0/`; the separate Server.Tests
runner is included. Integration-directory copies of the host, Server, sample
and AppHost assemblies match their owning outputs.

| Project / output | SHA-256 |
| --- | --- |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.dll` | `f9ca20b92d9e65c714a240c87f60fbf2c47a20d66152bee0b3964f66532f9903` |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.deps.json` | `89e89b25a82349d764ede5a7cf238e713c34cce9bbdb1900f5e07c5034379388` |
| `src/Hexalith.EventStore.Server` / `Hexalith.EventStore.Server.dll` | `4689669731ea8e7fbfc6ed4c97f6ca91f2aa89de1e24449a59ecb4eaa74f591d` |
| `src/Hexalith.EventStore.Gateway` / `Hexalith.EventStore.Gateway.dll` | `7162ec34b79f1dd43744f6c8b0d4ec1727904cb03b7d47e4a4ee54514d098e8a` |
| `samples/Hexalith.EventStore.Sample` / `Hexalith.EventStore.Sample.dll` | `f71e7d9dc7c404a2f8a0e4db2808733d359ea40ffa42613ee8b74896805a877b` |
| `samples/Hexalith.EventStore.Sample` / `Hexalith.EventStore.Sample.deps.json` | `231e7d0b311eb55a2693b79c88e63a79393d584a1238db6c7c6b9b45c3f46538` |
| `tests/Hexalith.EventStore.IntegrationTests` / `Hexalith.EventStore.IntegrationTests.dll` | `8a3c7b7865964a152d63c55b23ad0d639c5226b5c16ea547bf79e0b9ade42222` |
| `tests/Hexalith.EventStore.IntegrationTests` / `Hexalith.EventStore.IntegrationTests.deps.json` | `f9fdd5d585d2d60782a67f09f9f5f68717f364e97c73daa29c7b5ed9ad6f071e` |
| `src/Hexalith.EventStore.AppHost` / `Hexalith.EventStore.AppHost.dll` | `1368e15aec09eb65e23cbc4388c8da2267fc174fd65f7091e0481b8b1a4a0052` |
| `src/Hexalith.EventStore.AppHost` / `Hexalith.EventStore.AppHost.deps.json` | `1db1fae3dfafb0ec5252cf317baab996a925e47b91b0ae8ab60161a79d90c7c1` |
| `tests/Hexalith.EventStore.Server.Tests` / `Hexalith.EventStore.Server.Tests.dll` | `df6d0ff9c1b0beaf3ebab9138a0c6f9ad59747606aa0f53e27a6f1fd314b854a` |
| `tests/Hexalith.EventStore.Server.Tests` / `Hexalith.EventStore.Server.Tests.deps.json` | `902d267452353b89570dbb43da8eb4aae9ee82fa1b0e7f2771aff9ac4d3e83ac` |

Final explicit-AppHost `aspire stop` and `aspire describe` found no running
AppHost for this repository. The executable-filtered workspace scan at
`09:48:34 UTC` found no proof host or selected candidate sidecar remaining.
The initial broad scan matched its own short-lived Bash probe; that observation
was retained separately and the shell had exited before the corrected scan.
An expanded app-ID scan at `09:50:37 UTC` found pre-existing Dapr CLIs
`31705`/`31841` and installed-runtime children `32002`/`32196`, owned by DCP
`21166` in `/home/administrator/projects/hexalith/frontcomposer/src/Hexalith.FrontComposer.AppHost`.
Their cwd/config paths belong to that separate workspace; they started at
`09:30:13 UTC`, before these commands, with app IDs `eventstore`/`sample` and
placement/scheduler `localhost:50005`/`localhost:50006`. They were preserved.
These additional peers limit topology isolation: the two proof hosts' assertions
passed, but this run supplies no isolated two-replica placement or exclusive
sample-dispatch claim. Their source/binary and in-run behavior were not qualified.
The temporary Docker adapter was removed. The inotify
ceiling remained `1,048,576` at every command boundary; the accessible final scan
found 3,319 watch lines in 13,896 readable fdinfo files, with one inaccessible
entry. No sysctl write or unrelated-process interruption occurred.

No historical failure was reproduced, so no behavioral correction or explanation
is claimed. Prior projection regressions and Redis binary-identity ambiguity
remain unresolved historical measurements. Current Go concurrency/source
requalification and CI are absent; the separate Contracts validator blocker was
not rerun, reclassified or exempted. Production runtime/durable broker/enrolled
issuer, shipped topic enrollment, canonical completion/freshness fault behavior,
atomic both-host transition/rollback and authorized disposition remain gated.
Story 2.16 retains NFR5 primary ownership. Original story `backlog`, active
spec/sprint `in-progress`, unchecked whole-story tasks and
`accepted_exceptions: []` are unchanged.

Final documentation checks: `bash scripts/check-committed-files.sh` passed
(9,039 tracked files checked), and `git diff --check` passed.
