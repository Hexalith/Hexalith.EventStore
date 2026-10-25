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
