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

## Verification — October 25 d41a3c55 Continuation

Source revision: `d41a3c55a684491d02f61c5ea98e0f74332b5d4d`. CI run link: none;
these are local checks under AD-12. The clean starting tree, HEAD and all
root-declared submodule checkouts remained unchanged through the commands below.
The complete Debug source-reference solution build passed, including both host
graphs. It changed 56 selected output files, with no measured source or runtime
changes. Before/after captures of 12,556 source/configuration files and 545 selected
assembly/deps/runtimeconfig/executable files stayed unchanged during all five test
commands. This is incremental-build and execution-stability evidence, not clean
reproducibility or atomic launch-identity proof. The only implementation edit is
this Verification append; no behavioral correction is claimed.

The explicitly selected fixture runtime was
`/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`, version
`1.18.7-local-2-13-retirement`, SHA-256
`a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`.
Installed daprd `1.18.7` remained SHA-256
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.
Observed tools were SDK `10.0.402`, .NET `10.0.13`, xUnit runner `4.0.2`, Dapr
CLI `1.18.2` and Aspire CLI `13.6.1+3751e615ad660621f2a7aa4c390ef48e08a0eed8`.
`go version` exited 127 (`go: command not found`), and
`rg --files /tmp/eventstore-2-13-retirement-oduc267u -g 'go.mod' -g 'go.sum'`
found no manifests. Current Go/source concurrency requalification remains unrun;
the earlier runtime reconstruction limitations still apply.

The recorder set all four environment variables below for every recorded command,
including build, lifecycle and focused-server commands. For shell replay:

```sh
export EnableKeycloak=false
# Integration-proof runtime/output settings; also present on the other commands.
export HEXALITH_STORY_2_13_DAPRD_PATH=/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement
export HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-d41a3c55-nxrvyikt/scenario-output
export R10A2_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-d41a3c55-nxrvyikt/redis-output
```

Only the integration proofs consume the runtime/output settings. The candidate
selection applies only to the two proof sidecars; composed AppHosts and the
ordinary Direct fixture use installed daprd. Local logs/captures remain
temporary output under that parent directory, with no committed packet or validator.
The existing `/usr/bin/docker` worked and reported server `29.8.2`; no adapter
or daemon change was needed.

The baseline and auxiliary topology used these exact commands:

```sh
APPHOST=src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json
aspire wait eventstore --timeout 45 --apphost "$APPHOST" --non-interactive
aspire describe --apphost "$APPHOST" --format Json --non-interactive
aspire stop --apphost "$APPHOST" --non-interactive
# After the build, start the auxiliary topology and retain its sample resource.
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json
aspire wait sample --timeout 45 --apphost "$APPHOST" --non-interactive
aspire wait sample-dapr-cli --status up --timeout 45 --apphost "$APPHOST" --non-interactive
aspire resource eventstore stop --apphost "$APPHOST" --non-interactive
aspire resource eventstore-dapr-cli stop --apphost "$APPHOST" --non-interactive
```

All lifecycle commands passed. Baseline AppHost/CLI PIDs were `99943`/`99686`,
with inspected project/sidecar resources Running/Healthy; auxiliary PIDs were
`36828`/`36742`. During the focused restart, sanitized `aspire describe` at
`10:22:02 UTC` showed sample/sample-sidecar Running/Healthy and
eventstore/eventstore-sidecar Finished. Explicit-AppHost `aspire stop` ended the
auxiliary topology before the projection/Direct pair. Read-only process inspection
also found unrelated FrontComposer `eventstore`/`sample` sidecars sharing placement
`50005` and scheduler `50006`; those peers were preserved. This run therefore
establishes no isolated two-replica placement or exclusive sample-dispatch claim.
Pre/post `docker inspect` and `docker image inspect` found unchanged Redis,
placement and scheduler container IDs, start times, image IDs and digests, matching
the earlier support table. No shared service was reconfigured or stopped.

The build and five test commands ran sequentially; read-only observations also ran
while tests were active. Every test row has zero failures, skips and runner errors.

| Exact command | Result and UTC interval on 2026-10-25 |
| --- | --- |
| `dotnet build Hexalith.EventStore.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Passed, 0 warnings/errors; `10:17:12.667964–10:21:07.993883`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -method '*DaprRedisStreams_ReplicaRestart_ReauthorizesAndReconcilesPersistedState'` | 1 passed; `10:21:48.650747–10:22:43.258977`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprDistributionRuntimeProofTests'` | 6 passed; `10:22:52.118230–10:25:24.867240`; no `-stopOnFail`. |
| `tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests -class '*ETagActorTests' -class '*SignalRProjectionChangedBroadcasterTests' -class '*DaprProjectionChangeNotifierTests' -class '*DaprProjectionChangeNotifierSignalRTests' -class '*ProjectionChangedNotificationValidatorTests' -class '*DomainServiceWorkloadAssertionHandlerTests'` | 102 passed; `10:25:38.851785–10:25:39.812927`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprProjectionDispatchProofTests' -class '*ValidProjectionRoundTripE2ETests'` | 2 passed; `10:25:46.221836–10:26:48.382832`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRRedisBackplaneRuntimeProofTests'` | 1 passed; `10:27:16.020545–10:30:10.647651`. |

The Redis report's static `dotnet test ... --filter` template was not executed;
the direct executable command above is authoritative. Key tested DLL hashes below
agree before/after all five test commands; paths are relative to the owning
project's `bin/Debug/net10.0/`:

| Output | SHA-256 |
| --- | --- |
| `Hexalith.EventStore.dll` | `8c1f6be1f7ed24bb5fecb3e73739deb9bb08d4cfc0f81b89fac3e227f7c57007` |
| `Hexalith.EventStore.Server.dll` | `ab3b350237867d32233ea4c644cf1f20c7de55def351826a516c6624e660c08d` |
| `Hexalith.EventStore.Gateway.dll` | `b014cdc0fe249410f4a809707d483161b27a95eec58b4c8c4cd12c640ac2453f` |
| `Hexalith.EventStore.Sample.dll` | `71d0ed4c2fba233fc8274e4f9c8dc3891d6f418796982b4b5d8b029657a579d7` |
| `Hexalith.EventStore.AppHost.dll` | `a2492dbafdd80dac6525406f38be064b1d893d741303d95ae059d8f046d2410d` |
| `Hexalith.EventStore.IntegrationTests.dll` | `cc02398d94acfb4b8e371caf8225a8565dc3c2cf92c88f5ced7041cdbec617d0` |
| `Hexalith.EventStore.Server.Tests.dll` | `986089c57d77e399a5ea6054b4d33ce44634829f98d818651b8edd556eb58f71` |

The exact-topic Development profile remained `pubsub.redis` v1 with
`consumerID: "{uuid}"`, `state.redis` v1 and `dapr.io/v2alpha1` subscriptions,
shared app ID `eventstore` and independent proof sidecars. Focused/full-class
reused-endpoint captures proved initial actor ownership on the second host and
Dapr persisted version/ETag `1` → `2`. The second replica's process replacement
and retained internal gRPC endpoint were:

| Run | Original host PID | Restored host PID | Original sidecar PID | Restored sidecar PID | Original internal gRPC port | Restored internal gRPC port |
| --- | --- | --- | --- | --- | --- | --- |
| Focused restart | `55116` | `57668` | `55182` | `87547` | `42209` | `42209` |
| Full distribution class | `562` | `2218` | `682` | `28483` | `41149` | `41149` |

Both restart manifests record acceptance of exactly 2,048 serialized metadata bytes
on both hosts and rejection of the 2,129-, 2,050- and 2,060-byte vectors on both
hosts. Each run records the same three distinct rejected callback event IDs on
its two hosts, alongside the accepted boundary control.

Reset disconnected 33 connections and rejected 1,554 reconnects;
blackhole discarded 3,766 bytes without proxy disconnects or reconnect rejection.
Both hosts' reads ended with `OperationCanceledException` in 2,000.0968–2,004.5586 ms
against the unchanged 2,000 ms caller budget/5,000 ms outer bound. Both fault cases
read back versions `[1,1]` and ETags `["1","1"]` after restore, then final version
`2`/ETag `"2"`. These synthetic checks establish no canonical UI freshness or
real projection completion; outage publication remains unknown until reconciliation.

The separate real-projection proof recorded command `Completed`, one event,
`ProjectionBacked` provenance, count `1` on both hosts and identical authoritative
ETag `"Y291bnRlcg.L89o_pBtNUSyl3AYpOMRvA"`. Both authorized clients received
signal/detail with scope `counter-status` and `change-kind=counter-projection-updated`.
Tenant B received neither during four concurrent one-second channel reads.
The manifest's test-generated synthetic identifiers correlate its command,
projection and callback logs; they confer no isolation from the shared topology
described above:

| Identifier | Test-generated value |
| --- | --- |
| Tenant | `tenant-a-f789caf82b61499c9bbd855884efbe0b` |
| Exact topic | `tenant-a-f789caf82b61499c9bbd855884efbe0b.counter.projection-changed` |
| Aggregate | `distribution-projection-38c954518c324beda9c662fe143db56c` |
| Correlation | `6a3db09c-399b-4329-9cbe-65cb6c77ef76` |

| Proof node | Host / sidecar PID | Authorized connection | Other-tenant connection |
| --- | --- | --- | --- |
| First | `18627` / `18663` | `5UoADJ964-2eJs0HXuRI0w` | `XkREK-LVgyv8mdhO9VFdGw` |
| Second | `20023` / `20284` | `pyV9ghpS7ilwXhJ6NyvXGw` | `ChKhRC-Cv1fyUGm8Tx1gKQ` |

That scenario's generated SHA-256 identities were `pubsub.yaml` =
`483c2de2d90aa876a80efe6499edc98a82903a46ddd98a79a9f610ec2719b001`,
`statestore.yaml` = `8cfd8564018ac754f5d3f14acfc215f3d2caf186cb1e14b7b377b1d4660d023a`
and `subscription-0.yaml` = `e2a8afede65000a8fbd279acf8bb7bfcc707c09eb8cb03e1fc7ce3cd36feeefa`;
these bytes identify only that scenario. Authorization, serialized metadata bounds,
caller budgets and the existing 15-second signal deadline were unchanged.

Final explicit-AppHost `aspire stop`/`aspire describe` found no running AppHost;
the `10:30:51 UTC` product-process scan found no workspace proof host or selected
candidate sidecar. Other workspaces' processes were preserved. The inotify ceiling
remained `1,048,576` at every captured boundary; the final accessible scan counted
3,796 watch lines across 18,591 readable fdinfo files, with 37 inaccessible entries.
No sysctl write was needed.

The historical projection failures and Redis identity ambiguity remain unexplained;
current passes do not resolve them. Go requalification and CI remain absent, and
the separate Contracts validator blocker was not rerun, reclassified or exempted.
Production runtime/durable broker/enrolled issuer, shipped topic enrollment,
canonical freshness fault behavior, atomic both-host transition/rollback and owner
disposition remain gated. Original story `backlog`, active spec/sprint `in-progress`,
unchecked whole-story tasks, frozen intent and `accepted_exceptions: []` are unchanged.

Documentation checks: `bash scripts/check-committed-files.sh` passed (9,039 tracked
files checked); `git diff --check` passed.

## Verification — October 25 Pinned Go Reconstruction And Controls

Source revision: `d41a3c55a684491d02f61c5ea98e0f74332b5d4d`. Historical input
revision: `b7ba9bb1fdff85fb47817d35506bd6ca81b23d70`; Dapr upstream:
`b32a815fa2260983e181b95272ea555fe17d3895`. CI run/link: none; these are local
checks. No Go-retirement job was found in the repository's tracked
`.github/workflows`; this does not describe uninspected remote reusable workflows.

The missing-source/toolchain reconstruction gap is resolved, but the existing Go
qualification does **not** pass. Two full race runs exposed separate failures:
a deadline error-chain assertion and a data race in the upstream scaling test's
reflective membership assertion. Both were reproduced on pristine pinned upstream
source. The original failing tests and historical results remain unchanged;
passing focused controls and diagnostic variants do not replace those failures.
No acquisition input remains missing.

All reconstruction, caches, logs and diagnostic overlays are disposable output
under `/tmp/story-2-13-go-20261025-7_9k48d6`. No evidence packet or validator was
added to the repository. The only task edit is this append; the pre-existing story
and spec changes are preserved. The installed and retained fixture runtimes,
repository dependencies, production graphs, original story `backlog`, spec/sprint
`in-progress`, unchecked whole-story tasks and accepted-exception register remain
unchanged. No .NET tests were rerun: no application source or selected runtime was
changed. No runtime was installed or substituted into a fixture.

Reconstruction used the historical `runs-2026-10-21-concurrency-retirement/`
patch, build receipt and source inventory, plus the original acquisition and
manifest records from `runs-2026-10-21-runtime-recovery/`, all read with `git show`
from the historical input revision above. Five local Go test files were recovered
from `runs-2026-10-21-concurrency-retirement/snapshots/retirement-runtime-build/external/`:
`local_cancellation_test.go.source`, `local_concurrency_test.go.source`,
`local_recovery_test.go.source`, `local_retirement_test.go.source` and
`local_retirement_pool_test.go.source`. They were copied to their original router
or manager paths without editing. The reconstructed tree matches all **5,587**
entries in `before-candidate-build-source-inventory.json`: **4,441 files** and
**1,146 directories**, including recorded modes, with zero missing, extra or
changed entries. The upstream control tree has all **4,436** original archive
files byte-identical and only the historical cancellation/recovery test files added.

Acquisition and patch commands (all exit 0) were:

```sh
R=/tmp/story-2-13-go-20261025-7_9k48d6
S="$R/dapr-b32a815fa2260983e181b95272ea555fe17d3895"
U="$R/upstream-control/dapr-b32a815fa2260983e181b95272ea555fe17d3895"
H="$R/history"
O="$R/overlays"
GO="$R/go/bin/go"
curl --fail --location --show-error --connect-timeout 20 --max-time 180 --output "$R/go1.26.9.linux-amd64.tar.gz" https://go.dev/dl/go1.26.9.linux-amd64.tar.gz
curl --fail --location --show-error --connect-timeout 20 --max-time 180 --output "$R/dapr-source.tar.gz" https://codeload.github.com/dapr/dapr/tar.gz/b32a815fa2260983e181b95272ea555fe17d3895
```

The following are **equivalent replay steps**, arranged for a fresh disposable
`R` after the downloads and before running the patch commands below; they are not
additional test executions. `git show` recovery and `tarfile.extractall` were
performed in the measured run. No separate `chmod` was needed then: extracted
modes and the five recovered `0644` tests already matched the historical inventory.
The final mode loop below makes that outcome explicit for a replay under another
umask. For a new run, create `R` with `mktemp -d /tmp/story-2-13-go-replay-XXXXXXXX`
and derive the other aliases from it before downloading. Run from the EventStore
repository; all outputs remain under `R`.

```sh
python3 - "$R" <<'PY'
from pathlib import Path
import hashlib, json, shutil, subprocess, sys, tarfile
r = Path(sys.argv[1])
h = r / "history"
rev = "b7ba9bb1fdff85fb47817d35506bd6ca81b23d70"
base = "_bmad-output/implementation-artifacts/evidence/story-2-13/"
c = "runs-2026-10-21-concurrency-retirement/"
l = "concurrency-closing-2026-10-21/review-lifetime-controls/"
def recover(relative):
    out = h / relative
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(subprocess.check_output(["git", "show", rev + ":" + base + relative]))
    return out
for name in ("candidate-from-upstream.patch", "before-candidate-build-source-inventory.json",
             "candidate-runtime-identity.json", "retirement-runtime-build.json"):
    recover(c + name)
for name in ("runtime-acquisition.json", "go.mod.source", "go.sum.source"):
    recover("runs-2026-10-21-runtime-recovery/" + name)
for relative in (c + "retirement-churn.overlay.json", c + "cancel-guard-mutant.overlay.json",
                 l + "caller-lease-candidate.overlay.json", l + "caller-lease-mutant.overlay.json",
                 l + "lifetime-controls.overlay.json", l + "full-router-manager.overlay.json"):
    overlay = json.loads(recover(relative).read_text())
    for replacement in overlay["Replace"].values():
        recover(replacement.split(base, 1)[1])
archives = {
    "go1.26.9.linux-amd64.tar.gz": "42d158b4d8f7b61ac0a830567c940a86098fb7aac52e467a5ebec03ef5cc2f8d",
    "dapr-source.tar.gz": "43c5df99b6f9ecfac15313c49728969d8fc2ea9d13b4cb6777725311e496ba50",
}
for name, expected in archives.items():
    if hashlib.sha256((r / name).read_bytes()).hexdigest() != expected:
        raise SystemExit("archive hash mismatch: " + name)
    with tarfile.open(r / name) as archive:
        archive.extractall(r, filter="data")
s = r / "dapr-b32a815fa2260983e181b95272ea555fe17d3895"
u = r / "upstream-control" / s.name
with tarfile.open(r / "dapr-source.tar.gz") as archive:
    archive.extractall(u.parent, filter="data")
for name in ("local_cancellation_test.go", "local_concurrency_test.go", "local_recovery_test.go",
             "local_retirement_test.go", "local_retirement_pool_test.go"):
    recovered = recover(c + "snapshots/retirement-runtime-build/external/" + name + ".source")
    folder = "pkg/api/grpc/manager" if name == "local_retirement_pool_test.go" else "pkg/actors/router"
    shutil.copyfile(recovered, s / folder / name)
    if name in ("local_cancellation_test.go", "local_recovery_test.go"):
        shutil.copyfile(recovered, u / folder / name)
for relative, entry in json.loads((h / c / "before-candidate-build-source-inventory.json").read_text()).items():
    (s / relative).chmod(entry["mode"])
PY
```

This constructs `U` directly from the unpatched archive and copies only the two
historical test files into it. Apply the recorded patch to `S` only:

```sh
patch --dry-run -p1 -d "$S" -i "$H/runs-2026-10-21-concurrency-retirement/candidate-from-upstream.patch"
patch -p1 -d "$S" -i "$H/runs-2026-10-21-concurrency-retirement/candidate-from-upstream.patch"
```

The Go executable, patch and manifest hashes still require the checks in the
table below before Go execution; the measured run checked them with Python
`hashlib.sha256(...).hexdigest()` and compared all source entries with the
historical inventory.

Archives were SHA-256 checked before extraction with Python `tarfile.extractall`
using `filter='data'`. The patch changed only `pkg/actors/router/router.go`,
`pkg/api/grpc/manager/manager.go` and `pkg/api/grpc/manager/pool.go`. Verified hashes:

| Input | SHA-256 |
| --- | --- |
| Go `1.26.9` Linux amd64 archive | `42d158b4d8f7b61ac0a830567c940a86098fb7aac52e467a5ebec03ef5cc2f8d` |
| Extracted `go/bin/go` | `e90c4e12df236ea0c2e7536fd68da8a47fc0b585c19588bf6c1a70cfc296a0f9` |
| Dapr upstream archive | `43c5df99b6f9ecfac15313c49728969d8fc2ea9d13b4cb6777725311e496ba50` |
| Candidate patch | `f4981813f47bcbb8779cf7e5578db74ca0ec19b7841a615581df7fda7f31d515` |
| Candidate `go.mod` | `d218290004fa0583025e5a21d6478e6c1dfa07953102eb0288a0cdf433ae50d8` |
| Candidate `go.sum` | `0a7d34bc98a876eddfe1fc6e1b4ba7cb2b50d4f0b0c87a0b35493f9915181ce0` |

One initial historical lookup failed: `git show b7ba9bb1fdff85fb47817d35506bd6ca81b23d70:_bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-concurrency-retirement/runtime-sources/pkg/actors/router/local_recovery_test.go.source`
exited **128**, reporting that the path does not exist in that revision. The
missing input was resolved at `snapshots/retirement-runtime-build/external/local_recovery_test.go.source`
under the same historical retirement directory; its recovered SHA-256 is
`3fd1da4b1fcca0b7258ea34a6c38ecace09d7ab9696f517a501bf0d7f8702548`.
This was a resolved lookup error, not a substitute source version.

The following shorthand expands the exact recorded Go invocation prefixes; paths
are relocated into the new disposable directory, while historical test arguments,
patch, versions and expectations are preserved:

```sh
gc() {
    env GOTOOLCHAIN=local GOPATH="$R/gopath" GOCACHE="$R/gocache" "$GO" -C "$S" "$@"
}
gt() {
    test_source=$1
    shift
    env GOTOOLCHAIN=local GOPATH="$R/gopath" GOCACHE="$R/gocache" CGO_ENABLED=1 "$GO" -C "$test_source" test -race -mod=readonly -p=4 "$@"
}
```

`gc mod download -json` passed (`10:44:27.823634–10:44:49.157302 UTC`): **498**
modules, zero download errors; every downloaded archive/module-manifest sum
matched the unchanged historical `go.sum`. `gc mod verify` passed with
`all modules verified` before tests. grpc-go remains `v1.83.2`. `"$GO" version`
reported `go1.26.9 linux/amd64`; effective race settings were `GOOS=linux`,
`GOARCH=amd64`, `GOAMD64=v1`, `GOTOOLCHAIN=local`, `CGO_ENABLED=1`, `CC=gcc`,
`CXX=g++`, empty `GOFLAGS`, `GOPROXY=https://proxy.golang.org,direct` and
`GOSUMDB=sum.golang.org`. `gcc --version` reported Ubuntu `15.2.0-16ubuntu1`
(`15.2.0`) before tests and at later environment inspection; historical C-compiler
identity was not pinned, so C-toolchain equivalence is not claimed.

Existing overlay sources came from the historical retirement directory and
`concurrency-closing-2026-10-21/review-lifetime-controls/`. Only overlay absolute
paths were remapped. All nine later-review replacement-hash comparisons matched
the historical command receipts. Effective supplemental source hashes were:

| Source / mutation | SHA-256 |
| --- | --- |
| Resolver churn test | `c841f36e020487f6ab3a94cbddeb498b9b0ff5aeb1a0541b0c3fa496d206a1bf` |
| Pool lifetime controls | `c6d9d2c35460b3c2d47c549181c14007d5cdce458948b34cf158f157b8e2380f` |
| Post-retirement caller controls | `73cc473d8c4fd028cc9a9b0058d98e4dbcf654b816b4c04c70187e9ba337c007` |
| Second-lease test | `75acb8ef33ee4a4edd87d2dc68fd3e3132c6b5327f1fea088e9da2a7f6ef8b29` |
| Cancellation-guard mutant router | `2a4ab419256c71e413d9124a8458f1abb373bd8b5ee9703b32224b32a241a642` |
| Extra-release mutant router | `961213204b4013cdbd3d68e15b07aaf7bc2928af8d96d47b031de70abe934942` |

Exactly **six historical overlays** and **two diagnostic overlays** were executed.
The focused historical lane used no overlay. Extracted `candidate-probe` and
`mutant-probe` files were not executed. For the mappings below, aliases are:

```sh
C="$H/runs-2026-10-21-concurrency-retirement"
L="$H/concurrency-closing-2026-10-21/review-lifetime-controls"
A="$S/pkg/actors/router"
M="$S/pkg/api/grpc/manager"
D="$R/diagnostics"
```

Each arrow is an exact JSON `Replace` key → value after expanding these aliases:

| Overlay file | Target → replacement |
| --- | --- |
| `$O/retirement-churn.overlay.json` | `$A/local_retirement_churn_test.go` → `$C/local_retirement_churn_test.go.source` |
| `$O/cancel-guard-mutant.overlay.json` | `$A/router.go` → `$C/cancel-guard-mutant.go.source` |
| `$O/caller-lease-candidate.overlay.json` | `$A/review_caller_lease_test.go` → `$L/retained-double-release/caller_lease_test.go` |
| `$O/caller-lease-mutant.overlay.json` | `$A/review_caller_lease_test.go` → `$L/retained-double-release/caller_lease_test.go`; `$A/router.go` → `$L/retained-double-release/router.go` |
| `$O/lifetime-controls.overlay.json` | `$M/review_lifetime_pool_test.go` → `$L/review_lifetime_pool_test.go.source`; `$A/review_retirement_context_test.go` → `$L/review_retirement_context_test.go.source` |
| `$O/full-router-manager.overlay.json` | `$M/review_lifetime_pool_test.go` → `$L/review_lifetime_pool_test.go.source`; `$A/review_retirement_context_test.go` → `$L/review_retirement_context_test.go.source`; `$A/review_caller_lease_test.go` → `$L/retained-double-release/caller_lease_test.go`; `$A/local_retirement_churn_test.go` → `$C/local_retirement_churn_test.go.source` |
| `$D/deadline.overlay.json` | `$A/local_cancellation_test.go` → `$D/local_cancellation_test.go` |
| `$D/scaling-membership.overlay.json` | `$M/manager_test.go` → `$D/manager_test.go` |

To reconstruct the six historical overlay JSON files, read the recovered originals
under `C` or `L`, replace the source prefix
`/tmp/eventstore-2-13-retirement-oduc267u/dapr-b32a815fa2260983e181b95272ea555fe17d3895`
with `S` and the history prefix
`/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/story-2-13`
with `H` in all keys/values, then write `{"Replace": mapping}` under `O` using the
same basename. This is the path-only transformation used in the measured run.
The two diagnostic JSON files contain only their single mapping shown above.

The deadline diagnostic copies `$A/local_cancellation_test.go` to
`$D/local_cancellation_test.go` and makes only this zero-context diff (tab indentation is
preserved). The original `require.ErrorIs` assertion is unchanged:

```diff
--- a/pkg/actors/router/local_cancellation_test.go
+++ b/pkg/actors/router/local_cancellation_test.go
@@ -67,0 +68,2 @@
+				deadlineAt, _ := ctx.Deadline()
+				t.Logf("diagnostic: status=%s ctxErr=%v afterDeadline=%s channel=%s error=%v", status.Code(callErr), ctx.Err(), time.Since(deadlineAt), conn.GetState(), callErr)
```

The scaling diagnostic copies `$M/manager_test.go` to `$D/manager_test.go` and
makes only this diff; it is an investigation variant, not the historical suite:

```diff
--- a/pkg/api/grpc/manager/manager_test.go
+++ b/pkg/api/grpc/manager/manager_test.go
@@ -99 +99,2 @@
-	assert.Contains(t, uniqueConns, c)
+	_, reused := uniqueConns[c]
+	assert.True(t, reused, "pooled connection identity must be reused")
```

The test commands below ran sequentially. Counts distinguish top-level tests
from nested subtests; repeated runs count each invocation. Every row has zero
skips. Expected-red mutation failures are explicitly separate from unexpected
qualification failures.

| Exact command, using the prefixes above | Result: passed/failed counts | UTC interval, 2026-10-25 |
| --- | --- | --- |
| `gt "$S" ./pkg/actors/router ./pkg/api/grpc/manager -run 'TestConcurrency\|TestLocalRetirement\|TestLocalEvaluation\|TestReviewCanceled' -count=1 -timeout=1m -v` | Exit 0, passed; top 11/0, sub 11/0; 0 race reports. | `10:45:11.176239–10:47:14.924884` |
| `gt "$S" -overlay="$O/retirement-churn.overlay.json" ./pkg/actors/router ./pkg/api/grpc/manager -count=1 -timeout=3m -v` | Exit 1, failed; top 19/1, sub 22/1; 0 race reports. | `10:47:20.039545–10:47:28.157738` |
| `gt "$S" ./pkg/actors/router -run '^TestLocalEvaluationCallerCancellationRetainsChannel/deadline$' -count=20 -timeout=1m -v` | Exit 0, passed; top 20/0, sub 20/0; 0 race reports. | `10:48:11.395042–10:48:22.036204` |
| `gt "$U" ./pkg/actors/router -run '^TestLocalEvaluationCallerCancellationRetainsChannel/deadline$' -count=50 -timeout=1m -v` | Exit 1, failed; top 47/3, sub 47/3; 0 race reports. | `10:49:04.209874–10:49:22.973146` |
| `gt "$S" -overlay="$R/diagnostics/deadline.overlay.json" ./pkg/actors/router -run '^TestLocalEvaluationCallerCancellationRetainsChannel/deadline$' -count=50 -timeout=1m -v` | Exit 1, failed; top 48/2, sub 48/2; 0 race reports. | `10:49:43.531805–10:49:58.605067` |
| `gt "$S" -overlay="$O/cancel-guard-mutant.overlay.json" ./pkg/actors/router -run 'TestConcurrencyCancelledUnavailable\|TestReviewCanceledUnavailable' -count=1 -timeout=1m -v` | Exit 1, expected red; top 1/1, sub 0/0; 0 race reports. | `10:50:12.772950–10:50:16.550216` |
| `gt "$S" -overlay="$O/caller-lease-candidate.overlay.json" ./pkg/actors/router -run '^TestReviewRetiringCallerKeepsOtherLease$' -count=1 -timeout=30s -v` | Exit 0, passed; top 1/0, sub 0/0; 0 race reports. | `10:50:16.579773–10:50:21.275323` |
| `gt "$S" -overlay="$O/caller-lease-mutant.overlay.json" ./pkg/actors/router -run '^TestReviewRetiringCallerKeepsOtherLease$' -count=1 -timeout=30s -v` | Exit 1, expected red; top 0/1, sub 0/0; 0 race reports. | `10:50:21.317372–10:50:26.336475` |
| `gt "$S" -overlay="$O/lifetime-controls.overlay.json" ./pkg/actors/router ./pkg/api/grpc/manager -run '^TestClosing(FirstRetirement\|BlockingClose\|CallerContextAfterRetirement)' -count=1 -timeout=30s -v` | Exit 0, passed; top 3/0, sub 8/0; 0 race reports. | `10:50:26.379581–10:50:33.421843` |
| `gt "$S" -overlay="$O/full-router-manager.overlay.json" ./pkg/actors/router ./pkg/api/grpc/manager -run . -count=1 -timeout=30s -v` | Exit 1, failed; top 23/1, sub 31/0; 2 race reports. | `10:50:33.475894–10:50:42.822005` |
| `gt "$U" ./pkg/api/grpc/manager -run '^TestGetAppClient_Scaling$' -count=20 -timeout=1m -v` | Exit 1, failed; top 19/1, sub 0/0; 1 race reports. | `10:51:18.242723–10:51:20.968446` |
| `gt "$S" -overlay="$R/diagnostics/scaling-membership.overlay.json" ./pkg/api/grpc/manager -run '^TestGetAppClient_Scaling$' -count=20 -timeout=1m -v` | Exit 0, passed; top 20/0, sub 0/0; 0 race reports. | `10:51:42.406506–10:51:46.141134` |

The focused controls exercised overlapping actor, reminder and stream RPCs,
healthy-server `Unavailable`, transport recovery, caller cancellation/deadline,
retirement exclusion, final-lease closure and repeated retirement. The dedicated
lifetime run additionally passed **500** simultaneous first-retirement/acquisition/
final-release races (acquisition preceded retirement in **259** rounds), both
blocking-Close paths, and all six actor/reminder/stream cancellation/deadline
cases after retirement. Replacement channels remained live; old channels closed
after their final lease. The original pool concurrency test ran **50** rounds
with **32** held leases. Each full churn run passed **25** channels beside
**25,000** resolver updates; no `ResetConnectBackoff` is used. These bounded local
controls establish no untested deployment envelope.

The cancellation-guard mutant removed only `ctx.Err() == nil`. Its discriminating
control observed the cancelled channel become `Shutdown` instead of
`TransientFailure`; the older healthy-channel control still passed. The
extra-release mutant inserted `cancel(false)` before the existing deferred
release, and the second-lease control rejected premature `Shutdown`. Thus both
negative controls failed at their intended assertions, rather than at setup or
compilation.

The unexpected deadline failure is `TestLocalEvaluationCallerCancellationRetainsChannel/deadline`
at `local_cancellation_test.go:68`: `require.ErrorIs(t, callErr, ctx.Err())` saw
`ctx.Err() == nil` while the RPC returned
`rpc error: code = DeadlineExceeded desc = stream terminated by RST_STREAM with error code: CANCEL`.
The unchanged candidate's 20 focused repetitions passed; the pristine upstream
control failed **3/50**, and a logging-only candidate overlay failed **2/50**.
The overlay preserved the assertion and recorded a `Ready` channel with nil
`ctx.Err()` **988.106 microseconds** and **1.42247 milliseconds** after the caller's
deadline. Its test-source SHA-256 is
`f906af27feefc8cf95edc31f18259e1439743854ed30041673208ae83979e3cb`.

Pinned grpc-go `v1.83.2` explains this ordering: `internal/transport/http2_server.go:601`
starts an independent timeout callback that sends `RST_STREAM CANCEL`, while
`internal/transport/http2_client.go:1286` converts that reset to `DeadlineExceeded`
by comparing the deadline timestamp with `time.Now()`, independently of
`ctx.Err()`. Dapr's `pkg/actors/router/router.go:274` checks `ctx.Err()` before
classifying the error; when it is still nil, `DeadlineExceeded` takes the permanent
error path and `Call` unwraps it. The retirement predicate at line 309 requires
`Unavailable`, so this outcome does not enter it. The source and diagnostic
observations support a deadline-callback ordering failure in the existing strict
error-chain control, also present without the retirement patch. No runtime
normalization or deadline-test expectation change was made; cancellation
qualification remains failed.

The second failure is `TestGetAppClient_Scaling`, `manager_test.go:99`, with **two**
data-race reports in the final full-overlay run. The read stack is
`assert.Contains` → `containsElement` → `ObjectsAreEqual` → `reflect.DeepEqual`;
the first writer is Go `1.26.9` `context.AfterFunc` (`src/context/context.go:329`)
via grpc-go `v1.83.2` `grpcsync.CallbackSerializer.run`
(`internal/grpcsync/callback_serializer.go:111`). The second is Go `context.withCancel`
(`src/context/context.go:278`) via `context.WithCancel` and grpc-go
`transport.NewHTTP2Client` (`internal/transport/http2_client.go:212`), reached from
`addrConn.createTransport`. Both race reports identify `sync/atomic.AddInt32`
against the reflective read of live connection/context state. Pinned testify `v1.11.1`
`assert/assertions.go:936` iterates map keys and uses deep equality, which inspects
the other live connection when map iteration reaches it before the identical
pointer. The pristine upstream scaling test reproduced **one failed/19 passed**
iterations with a race report. After that reproduction, a scratch diagnostic
replaced only the membership assertion with `_, reused := uniqueConns[c]` and
`assert.True(t, reused, "pooled connection identity must be reused")`; **20/20**
iterations passed under `-race`. Diagnostic test-source SHA-256:
`dc5471a36a8026f5ee0aeb6dd0b143151595725a69b410b705fe4da8d66ea0bd`.
Native map membership is a supported proposed test correction; it was not applied
to the reconstructed historical test or the repository. The original full-suite
race failure remains a failure, and this diagnostic does not qualify that suite.

The historical build command also passed (exit 0,
`10:51:54.106687–10:54:47.901507 UTC`), with only disposable paths relocated:

```sh
env GOTOOLCHAIN=local GOPATH="$R/gopath" GOCACHE="$R/gocache" CGO_ENABLED=0 "$GO" -C "$S" build -mod=readonly -p=4 -tags=allcomponents '-ldflags=-X=github.com/dapr/dapr/pkg/buildinfo.version=1.18.7-local-2-13-retirement -X=github.com/dapr/dapr/pkg/buildinfo.gitcommit=b32a815fa2260983e181b95272ea555fe17d3895 -X=github.com/dapr/dapr/pkg/buildinfo.gitversion=v1.18.7-local-2-13-retirement -X=github.com/dapr/kit/logger.DaprVersion=1.18.7-local-2-13-retirement' -o "$R/daprd-retirement" ./cmd/daprd
"$R/daprd-retirement" --version
"$GO" version -m "$R/daprd-retirement"
```

Both identity commands exited 0. The new binary reports
`1.18.7-local-2-13-retirement`, is **318,006,519 bytes**, and has SHA-256
`af3d5fe552b177613fc5489f8ecef936087b414902d5b4ecd5690fe545d540ad`.
All **420** compiled dependency records and **four** replacement records exactly
match the historical `candidate-runtime-identity.json`, including versions and
module checksums. The Go version, build tags and linker assignments match. The
binary hash differs from the retained fixture binary: the historical command has
no `-trimpath` and this build uses different absolute source/cache paths. This is
an exact-input reconstruction with a separately identified output, not a
byte-for-byte reproducibility claim. Its `--version` invocation was the only
execution of the rebuilt daprd; no sidecar was launched with it.

After all tests and the build, `gc mod verify` again exited 0 with
`all modules verified` (`10:55:46.874402–10:56:00.132899 UTC`). The complete
5,587-entry candidate inventory and manifest hashes were unchanged. Before this
append, all **9,039** tracked file hashes matched the task-start capture, and
HEAD remained `d41a3c55a684491d02f61c5ea98e0f74332b5d4d`. Installed
`/home/administrator/.dapr/bin/daprd` remained SHA-256
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`;
retained `/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement` remained
`a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`.
The Go-source controls are now executed and their failures investigated; the two
original full-suite failures, missing CI run and all production/disposition gates
remain open. No story completion, approval or production qualification is claimed.

Documentation checks after the initial append: `bash scripts/check-committed-files.sh`
exited 0 (**9,039** tracked entries checked), and `git diff --check` exited 0.
The documentation review added replay extraction/recovery/mode steps, the eight
executed overlay mappings, the exact diagnostic diffs and the race writer frames.
No runtime or .NET tests were repeated for these documentation-only corrections.

Review triage for this Verification append; edge-case and verification-gap
reviewers reported no additional findings. Historical tests/results and statuses
remain unchanged.

| Finding | Severity | Documentation correction |
| --- | --- | --- |
| Reconstruction replay gaps | Medium | Added Git recovery, checked archive extraction, source-mode normalization and pristine-control steps; labelled equivalent replay. |
| Overlay mapping gaps | Medium | Recorded all six executed historical and two diagnostic overlay mappings; excluded unused probe files. |
| Deadline diagnostic not recoverable | Medium | Included the exact logging-only diff with the original assertion and both diagnostic mappings. |
| Race writer stacks compressed | Low | Added pinned `context.AfterFunc`/callback-serializer and `context.withCancel`/HTTP2-client locations. |
| Documentation validation missing | Low | Recorded the initial checks and reran both after these corrections. |

After the documentation corrections, `bash scripts/check-committed-files.sh`
again exited 0 (**9,039** tracked entries). The first `git diff --check` found
space-before-tab/trailing-whitespace in the embedded diff context (exit 2); the
snippets now use exact zero-context diffs. The final `git diff --check` exited 0.

## Verification — October 25 7012502b Continuation

Source revision: `7012502bc094484eb2d4687badb98cb3b5e52b73`; CI run/link: none.
The Debug build and focused restart passed. All six distribution cases passed
their assertions, but **21 measured DLLs changed during that command**, leaving
stable-binary distribution qualification unproven. A separately copied server
output passed 102 controls. This task changes only this Verification section;
no behavioral correction or production qualification is claimed.

The starting tree was clean; Builds was
`6e40ea5f6243391bea80084b8ddd695df58fb1ff`. The complete incremental Debug
source-reference build covered both host graphs, changed 56 selected output files,
and changed none of 12,677 measured source/configuration files or runtime/CLI
executables. This is not clean reproducibility or complete compiler-input proof.
The initial Aspire baseline used existing outputs; proof commands followed the
recorded build. HEAD stayed unchanged through the recorded commands.

By build completion, `build.json` already recorded
`_bmad-output/implementation-artifacts/spec-ext-protection-1-owner-delivery.md`
modified; that spec was outside the measured source inventory. Later concurrent
edits appeared in payload-protection source and
`tests/Hexalith.EventStore.Server.Tests/Security/InteractionProtectionPersistenceTests.cs`.
Status also recorded untracked `SnapshotProtectionTargetReader.cs` and
`InteractionSnapshotProtectionTests.cs`; they were visible in status but outside
the tracked-source hash inventory. These changes and the later DLL outputs were
preserved. No rebuild followed that drift, and its DLL writer was not traced.
Results do not establish correspondence to the later source tree.

The fixture-only runtime remained
`/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`, version
`1.18.7-local-2-13-retirement`, SHA-256
`a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`.
Installed daprd `1.18.7` remained
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.
Observed SDK/.NET/xUnit/Dapr CLI/Aspire CLI versions were
`10.0.402`/`10.0.13`/`4.0.2`/`1.18.2`/`13.6.1`.
No runtime or dependency was rebuilt, installed or updated. Temporary output is
under `/tmp/story-2-13-20261025-7012502b-vabwqdna`; no packet or validator was
added to the repository. Recorder-managed commands used:

```sh
export EnableKeycloak=false
export HEXALITH_STORY_2_13_DAPRD_PATH=/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement
export HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-7012502b-vabwqdna/scenario-output
export R10A2_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-7012502b-vabwqdna/redis-output
```

The candidate applies only to the two proof sidecars; AppHosts retain installed
daprd. Build/tests ran sequentially; read-only inspections also ran during tests.

| Exact command | Result and UTC interval on 2026-10-25 |
| --- | --- |
| `dotnet build Hexalith.EventStore.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Passed, 0 warnings/errors; `11:18:46.463866–11:22:04.204078`. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -method '*DaprRedisStreams_ReplicaRestart_ReauthorizesAndReconcilesPersistedState'` | 1 passed, 0 failures/errors/skips; `11:25:12.923427–11:26:00.237873`. Selected binaries/runtime hashes stayed unchanged; one unrelated tracked source changed. |
| `tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class '*SignalRDaprDistributionRuntimeProofTests'` | 6 passed, 0 failures/errors/skips; `11:26:03.719922–11:28:46.625642`; no `-stopOnFail`. **Identity-ambiguous:** 21 DLL changes; no stable-binary qualification. |
| `/tmp/story-2-13-20261025-7012502b-vabwqdna/isolated-server-controls/Hexalith.EventStore.Server.Tests -class '*ETagActorTests' -class '*SignalRProjectionChangedBroadcasterTests' -class '*DaprProjectionChangeNotifierTests' -class '*DaprProjectionChangeNotifierSignalRTests' -class '*ProjectionChangedNotificationValidatorTests' -class '*DomainServiceWorkloadAssertionHandlerTests'` | Separate copied-output subject: 102 passed, 0 failures/errors/skips; `11:33:24.420819–11:33:25.367752`; cwd was the copied output directory. |

The stable focused restart proved initial second-host actor ownership, host/
sidecar PIDs `27812`/`27869` replaced by `28654`/`43150`, retained internal gRPC
port `45091`, and Dapr persisted version/ETag `1` → `2`. Both hosts accepted
2,048 serialized metadata bytes and rejected 2,129/2,050/2,060-byte vectors.
Authorization, caller budgets and the 15-second signal wait were unchanged.
This is synthetic-state recovery, not real projection completion or UI freshness.
The exact capture is
`scenario-output/replica-restart-reused-internal-endpoint-20261025T1125132062878Z-6af80e27d2704fc3bda1054fc2de5a2b/manifest.json`
under the temporary parent above. Its observations identify logical read-model key
`story-2-13:tenant-a-126d31059aa4:restart-126d31059aa4-1` for shared app ID
`eventstore`: Dapr `statestore` readback of the synthetic `version` object records
`InitialVersion=1`, `InitialETag="1"`, `FinalVersion=2`, `FinalETag="2"`.

The ambiguous full class covers reused/fresh endpoint restart, reset/blackhole,
exact-topic catalog rollout and scoped delivery/readback. Runtime and integration
runner hashes stayed unchanged, but host/Gateway/Server/Server.Tests outputs
changed. Representative before → after SHA-256 values are:

| Output | Completed-build / focused identity | Full-class after identity |
| --- | --- | --- |
| Root host `Hexalith.EventStore.dll` | `a68eda7c3cb15c1e94b411e5786ac8000ec82c9eaf6d200d02ac5684ff14697c` | `3519644be58274bf6e969b916db5a39958f4f758ead4bae6c12ab0a82f4403a9` |
| Root host copy of `Hexalith.EventStore.Server.dll` | `995642b4ebdb589ce4b1c13fa1c81e3f4d11536a2dd1457913f9352ffa72afd9` | `a12bf0b57d0fb3bdcb88e03958833070356895c279b5b670595a599fce1fb19d` |
| Integration runner DLL | `6196f9734b820e9c32a12dea319536ccb45a095b3d01e2f2c140c8fa167e965e` | unchanged |

For bounded isolation, the then-current Server.Tests output was copied into `/tmp`
after the drift. All **682** files matched stable before/after source-copy
inventories and stayed unchanged during the 102 controls. Its runner DLL/deps
hashes were `4c4f50b28ee07a2c9de057860bfff9d03164ffc84349ee9a959981fedf3a84d2` /
`902d267452353b89570dbb43da8eb4aae9ee82fa1b0e7f2771aff9ac4d3e83ac`;
its Server DLL was `3d9af9948ecfef74338719ea49a62849a91b70b94351ce08f1dd750589f3a523`.
This separately identifies observed output, without compiler-input/source
correspondence or repair of the ambiguous distribution run.

The following are **equivalent preparation/replay steps**, derived from the
recorded inline Python copy command, `server-copy.json` and `isolated-server.json`.
They were not additional copy or test executions during this documentation fix.
Run from the repository root; a replay creates a new temporary subject rather
than overwriting the measured copy:

```sh
python3 - <<'PY'
from pathlib import Path
import hashlib, json, shutil, tempfile
def inventory(root):
    return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(root.rglob('*')) if p.is_file()}
source = Path('tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0').resolve()
parent = Path(tempfile.mkdtemp(prefix='story-2-13-server-replay-'))
destination = parent / 'isolated-server-controls'
before = inventory(source)
shutil.copytree(source, destination, copy_function=shutil.copy2)
after, copied = inventory(source), inventory(destination)
if before != after or copied != before:
    raise SystemExit('Unstable source or copy mismatch; do not run tests')
(parent / 'copied-files.json').write_text(json.dumps(copied, indent=2) + '\n')
print(destination)
PY
```

For a replay, run the exact six-filter server command in the table with its
executable prefix and cwd changed to the printed destination. Compare its
post-run `inventory(destination)` with `copied-files.json`; any difference fails
the stability check. The following read-only comparison describes validation of
the **existing measured receipts and copy**, not another test execution:

```sh
python3 - <<'PY'
from pathlib import Path
import hashlib, json
parent = Path('/tmp/story-2-13-20261025-7012502b-vabwqdna')
copy = json.loads((parent / 'server-copy.json').read_text())
run = json.loads((parent / 'isolated-server.json').read_text())
destination = Path(copy['destination'])
current = {str(p.relative_to(destination)): hashlib.sha256(p.read_bytes()).hexdigest()
           for p in sorted(destination.rglob('*')) if p.is_file()}
if not (copy['source_before'] == copy['source_after'] == copy['copied']
        == run['before'] == run['after'] == current):
    raise SystemExit('Copy or execution inventory mismatch')
if run['exit_code'] != 0 or run['changes'] or len(current) != 682:
    raise SystemExit('Recorded result or file count mismatch')
print('682 matching files; recorded execution exit 0')
PY
```

The real-projection/Direct pair and Redis regression were **not run**. Compiled
AppHost `obj/Debug/net10.0/Aspire/references/*ProjectMetadata.g.cs` embeds absolute
shared EventStore/sample/Admin/Tenants project paths; `AspireContractTestFixture`
uses that AppHost, so copying outputs alone cannot isolate launches. Redis resolves
its project from `AppContext.BaseDirectory` and invokes `dotnet run --no-build`;
11 top-level integration-output identities differ from the captured host cohort,
including gRPC, Protobuf and Admin dependencies. No existing override redirects
those launches, and no path patching, mixed-cohort reconstruction or shared-output
overwrite was performed. These regressions remain required.

These are the ordered lifecycle commands and results from the existing receipts
(receipt basenames are comments). The three untimed diagnostic describes are also
listed; their successful outputs were observed in the original tool calls. No
topology command was rerun for this documentation correction:

```sh
APPHOST=src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
aspire describe --apphost "$APPHOST" --format Json --non-interactive # untimed initial observation: exit 0; no AppHost
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json # baseline-start: exit 0
aspire wait eventstore --timeout 45 --apphost "$APPHOST" --non-interactive # baseline-ready: exit 0
aspire describe --apphost "$APPHOST" --format Json --non-interactive # baseline-state: exit 0
aspire stop --apphost "$APPHOST" --non-interactive # baseline-stop: exit 0
# The recorded Debug build ran here.
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json # support-start: exit 0
aspire wait sample --timeout 45 --apphost "$APPHOST" --non-interactive # support-sample-ready: exit 0
aspire wait sample-dapr-cli --status up --timeout 45 --apphost "$APPHOST" --non-interactive # support-sidecar-ready: exit 0
aspire resource eventstore stop --apphost "$APPHOST" --non-interactive # support-eventstore-stop: exit 16
aspire describe --apphost "$APPHOST" --format Json --non-interactive # untimed: exit 0; support-stop-diagnostic.json
aspire resource eventstore stop --apphost "$APPHOST" --non-interactive # support-eventstore-stop-retry: exit 16
aspire stop --apphost "$APPHOST" --non-interactive # support-recovery-stop: exit 0
aspire describe --apphost "$APPHOST" --format Json --non-interactive # untimed: exit 0; no AppHost
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json # support-recovery-start: exit 0
aspire wait eventstore --timeout 45 --apphost "$APPHOST" --non-interactive # support-recovery-eventstore-ready: exit 0
aspire wait sample --timeout 45 --apphost "$APPHOST" --non-interactive # support-recovery-sample-ready: exit 0
aspire wait sample-dapr-cli --status up --timeout 45 --apphost "$APPHOST" --non-interactive # support-recovery-sidecar-ready: exit 0
aspire resource eventstore stop --apphost "$APPHOST" --non-interactive # support-recovery-eventstore-stop: exit 0
aspire resource eventstore-dapr-cli stop --apphost "$APPHOST" --non-interactive # support-recovery-eventstore-sidecar-stop: exit 0
aspire describe --apphost "$APPHOST" --format Json --non-interactive # support-recovery-state: exit 0
# Focused restart ran; full distribution was active during the next observation.
aspire describe --apphost "$APPHOST" --format Json --non-interactive # support-state-during: exit 0
# Full distribution finished before cleanup.
aspire stop --apphost "$APPHOST" --non-interactive # final-stop: exit 0
aspire describe --apphost "$APPHOST" --format Json --non-interactive # final-state: exit 0
```

The baseline was Running/Healthy and stopped before build. First auxiliary
`aspire wait sample --timeout 45` and
`aspire wait sample-dapr-cli --status up --timeout 45` passed, but
`aspire resource eventstore stop` failed twice with exit **16**, at
`11:22:34.405707–11:22:53.889040` and `11:23:17.351533–11:23:35.720047 UTC`,
leaving `eventstore-ywfdjexm` Unknown. Every wait/resource command used the explicit
AppHost and `--non-interactive`; no scenario had started. Full `aspire stop`
succeeded. A fresh start added the EventStore wait before the sample waits;
EventStore and `eventstore-dapr-cli` resource stops then passed. The `11:25:00`
and in-class `11:26:27 UTC` describes showed sample/sidecar Running/Healthy and
EventStore/sidecar Finished. Later success does not explain the retained failures.

Final explicit-AppHost stop/describe at `11:29 UTC` found no owned AppHost.
The `11:33:54 UTC` independent scan found no owned proof host or candidate sidecar;
it preserved unrelated ChatBot EventStore Dapr peers `66358`/`66760`, observed to
have started at `11:33:16 UTC`, after distribution ended. Pre/post Docker inspection
found Redis/placement/scheduler container IDs, start times, image IDs and digests
unchanged, matching the earlier support table; Redis reported `6.2.21` and Docker
`29.8.2`. This continuation made no shared-service or unrelated-process changes;
these observations do not exclude changes by other workspace activity. Inotify remained
`1,048,576`; cleanup counted 3,614 watch lines with one inaccessible fdinfo entry,
so no sysctl restoration was needed.

Prior Go full-suite failures, historical projection/Redis identity failures,
the separate Contracts validator blocker and missing CI remain unresolved; no
exemption was added. Production runtime/broker/enrolled issuer, shipped enrollment,
canonical freshness faults, atomic both-host transition/rollback and authorized
disposition remain gated. Original story `backlog`, spec/sprint `in-progress`,
frozen intent, unchecked whole-story tasks and `accepted_exceptions: []` are unchanged.

Documentation checks: `bash scripts/check-committed-files.sh` passed (9,041 tracked
files checked); `git diff --check` passed. Final parent checks after review also
passed (9,050 tracked entries), and confirmed all 682 copied-output hashes, frozen
intent, sprint status and prior story contents. Concurrent HEAD
`fd083a91b67a3bca10016f1f645da913c7ef526e` was observed after the measured runs;
no runtime result is attributed to that revision. This continuation staged or
committed no files.


## Verification — October 25 6ca1b4cb Continuation

Run-start/source revision: `6ca1b4cb9d422017138541036b3c23397265de98`.
CI run/link: none; these are local checks. The first command capture already
included edits to `SourcePublicationAuthority.cs` and its two test/fixture files.
Concurrent stream work and shared-output rebuilds were preserved. This task's
only repository edit is this Verification append; it changed no application,
fixture, runtime, dependency, production graph or status and performed no staging,
commit, push, branch or submodule update.

The Debug source-reference solution build passed, including both host graphs,
with zero warnings/errors, but four measured sources changed during it. It is an
incremental shared-workspace build, not an exact-SHA compiler-input or reproducible
build proof. At each recorded command boundary, the local recorder captured
16,861 source/configuration files across the repository and root-declared
submodules, 550 selected top-level executable/DLL/deps/runtimeconfig outputs, and
the selected/installed daprd and Aspire executables. HEAD, checkouts and those
three executable identities remained unchanged during all commands. Builds was
`6e40ea5f6243391bea80084b8ddd695df58fb1ff`, Commons
`1663028fb6d9f46a0bf17919edeb21bb99be1587`, Tenants
`14133e96d2c5efb13270580f920133079707398b` and Platform
`7c56ad6352a74edec62450668e106ec8d6011e40`.

The explicitly selected fixture-only runtime remained
`/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`, version
`1.18.7-local-2-13-retirement`, SHA-256
`a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`.
Installed daprd `1.18.7` remained SHA-256
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.
SDK `10.0.402`, .NET `10.0.13`, xUnit `4.0.2`, Dapr CLI `1.18.2` and Aspire
CLI `13.6.1+3751e615ad660621f2a7aa4c390ef48e08a0eed8` were observed.
The unchanged recorded integration deps manifest lists exercised `Dapr.Client`
`1.18.10` and `Microsoft.AspNetCore.SignalR.Client` `10.0.13`; broader dependency
coherence remains unproven.
No runtime was rebuilt, installed or replaced; no Go checks were repeated.
The earlier pinned Go full-suite deadline and reflective-membership race failures
remain unresolved measurements; their reconstructed inputs were not modified.

Temporary logs, command receipts and captures are under
`/tmp/story-2-13-20261025-6ca1b4cb-tci2bcly`; no committed packet or validator
was introduced. Every recorded command received these environment settings:

```sh
export EnableKeycloak=false
export HEXALITH_STORY_2_13_DAPRD_PATH=/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement
export HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-6ca1b4cb-tci2bcly/scenario-output
export R10A2_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-6ca1b4cb-tci2bcly/redis-output
IT=tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests
SERVER=tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests
APPHOST=src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

Only the two distribution proof sidecars consume the candidate selection;
composed AppHosts and the ordinary Direct fixture retain installed daprd. Docker
`29.8.2` worked without an adapter or daemon change. The build and test commands
ran sequentially; read-only inspections also ran during tests. Aliases above
expand to the exact test argument vectors below. All test rows have zero skips
and runner errors; failed assertions/readiness remain failures.

| Command | Result and UTC interval on 2026-10-25 |
| --- | --- |
| `dotnet build Hexalith.EventStore.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Passed, 0 warnings/errors; `12:59:41–13:03:02`. Four sources and 46 outputs changed. |
| `"$IT" -method '*DaprRedisStreams_ReplicaRestart_ReauthorizesAndReconcilesPersistedState'` | 1 passed; `13:03:54–13:04:46`. Measured source/output identities stable. |
| `"$IT" -class '*SignalRDaprDistributionRuntimeProofTests'` | 6 passed; `13:04:51–13:07:31`; no `-stopOnFail`. Outputs stable; four unrelated stream sources changed. |
| `"$SERVER" -class '*ETagActorTests' -class '*SignalRProjectionChangedBroadcasterTests' -class '*DaprProjectionChangeNotifierTests' -class '*DaprProjectionChangeNotifierSignalRTests' -class '*ProjectionChangedNotificationValidatorTests' -class '*DomainServiceWorkloadAssertionHandlerTests'` | 102 passed; `13:07:35–13:07:36`. Measured sources/outputs stable. |
| `"$IT" -class '*SignalRDaprProjectionDispatchProofTests' -class '*ValidProjectionRoundTripE2ETests'` | 2 passed assertions; `13:07:44–13:08:55`. **Identity-ambiguous:** 21 outputs changed. |
| `"$IT" -class '*SignalRRedisBackplaneRuntimeProofTests'` | 1 failed; `13:09:00–13:09:46`. First-host readiness cancellation; 11 outputs changed. |
| `"$IT" -class '*SignalRDaprProjectionDispatchProofTests' -class '*ValidProjectionRoundTripE2ETests'` | Separate stable cohort B: 1 passed (Direct), 1 failed (real projection notification); `13:11:06–13:12:34`. Measured sources/outputs stable. |
| `"$IT" -class '*SignalRRedisBackplaneRuntimeProofTests'` | Separate recheck: 1 failed; `13:13:18–13:16:36`. Unreachable-Redis host readiness cancellation; 39 outputs changed. |
| `env Logging__LogLevel__Hexalith.EventStore.Server.Actors.EventReplayProjectionActor=Debug Logging__LogLevel__Hexalith.EventStore.Server.Projections.DaprProjectionChangeNotifier=Debug Logging__LogLevel__Hexalith.EventStore.Server.Projections.ProjectionUpdateOrchestrator=Debug "$IT" -class '*SignalRDaprProjectionDispatchProofTests'` | Separate stable diagnostic cohort C: 1 failed; `13:19:01–13:19:57`. Measured sources/outputs stable; only category logging added. |

The four build-time source changes were `SourcePublicationFeed.cs`,
`SourcePublicationAuthorityActor.cs`, `SourcePublicationAuthorityFixture.cs` and
`SourcePublicationAuthorityTests.cs`. During the full distribution class they were
`AggregateActor.CommandSource.cs`,
`SourcePublicationAuthorityServiceCollectionExtensions.cs`,
`SourcePublicationAuthorityActor.cs` and `SourcePublicationAuthorityTests.cs`.
Other changes appeared between commands. Eight outputs changed during baseline
startup, six owning Server outputs during the sample readiness command, and the
sample's Client DLL during auxiliary shutdown. Their writers were not traced.
None of this supplies complete source correspondence for the tested binaries.
Before/after stability also does not provide atomic launch/module-load identity.

The successful focused restart and six-case class used output cohort A. Both
reused-endpoint captures proved initial ETagActor ownership on the second host,
new process/connection identities, fresh authorization/rejoin and Dapr persisted
version/ETag `1` → `2`:

| Run | Original → restored host PID | Original → restored sidecar PID | Original → restored internal gRPC port | Logical Dapr read-model key |
| --- | --- | --- | --- | --- |
| Focused | `77641` → `78944` | `77700` → `99664` | `33071` → `33071` | `story-2-13:tenant-a-af83c34094e8:restart-af83c34094e8-0` |
| Full class | `9793` → `10872` | `9854` → `33059` | `45189` → `45189` | `story-2-13:tenant-a-84355fe118b1:restart-84355fe118b1-0` |
| Full class — fresh-endpoint comparison | `63452` → `64827` | `63565` → `80045` | `34867` → `44529` | `story-2-13:tenant-a-c6f5683f44cc:restart-c6f5683f44cc-2` |

The fresh-endpoint row is comparison-only evidence and does not qualify
reused-endpoint recovery.

Both hosts accepted exactly 2,048 serialized metadata bytes and rejected the
2,129/2,050/2,060-byte vectors. The full class also passed the fresh-endpoint
comparison, scoped signal/detail and tenant denials, duplicates/reconciliation,
exact-topic catalog reload, provider reset and silent blackhole cases. Catalog
reload stops and restarts both sidecars while product hosts and their connections
remain running. Reset
closed 28 connections and refused 1,736 reconnects; blackhole discarded 4,694
bytes with no proxy disconnect/rejection. Both hosts' faulted reads cancelled in
2,000.1045–2,006.9617 ms against the unchanged 2,000 ms caller/5,000 ms outer
bounds. Reset publication failed with `DaprException` after 70.4399 ms; blackhole
publication failed with `OperationCanceledException` after 1,999.8448 ms.
Restore returned versions `[1,1]`, ETags `["1","1"]`, then final version
`2`/ETag `"2"`; denied clients received no hints. Outage publication remains
`unknown-until-reconciliation`. These are synthetic state checks, not real
projection completion, canonical UI freshness, retention or backpressure proof.

The fixture profile remains test-only `pubsub.redis` v1 with
`consumerID: "{uuid}"`, `state.redis` v1, exact `dapr.io/v2alpha1` subscriptions,
shared app ID `eventstore` and independent sidecars. Authorization, metadata
ceilings, cancellation/readback budgets and existing signal waits were unchanged.
No production broker, topic enrollment, issuer or profile was selected.

The identity-ambiguous real-projection execution retained command `Completed`,
one event, `ProjectionBacked` counts `1`/`1`, identical authoritative ETag
`"Y291bnRlcg.A7WGyE1_q0eIMDl4PfxTTg"`, signal/detail on both hosts, and no
other-tenant signal/detail during four concurrent one-second channel reads.
Its first host/sidecar PIDs were `16517`/`16582`, second `16923`/`17078`;
authorized connections were `HW4rOrqxpO7QGFsiEmdL5A` and
`kdeyW6FS9cyWBn7PIOXPyg`. Synthetic tenant
`tenant-a-2477134149374f9eada96ccb9a8d47ac`, aggregate
`distribution-projection-9800417bfbfd48d29b164759e5cbcbb9`, correlation
`4a54f96f-be59-4bdb-ba8c-91d2b1353b5b` and exact topic
`tenant-a-2477134149374f9eada96ccb9a8d47ac.counter.projection-changed`
bind these observations. Its generated component hashes were `pubsub.yaml` =
`483c2de2d90aa876a80efe6499edc98a82903a46ddd98a79a9f610ec2719b001`,
`statestore.yaml` =
`8cfd8564018ac754f5d3f14acfc215f3d2caf186cb1e14b7b377b1d4660d023a`,
and `subscription-0.yaml` =
`2792edad70c567b3b778473d0dfa06a6d6b2e44986f3d846dcb23b401a48a8cf`.
Those configuration identities apply only to that scenario, and none repairs its
21-DLL execution ambiguity.

Stable cohort B was separately assessed after cleanup: all 550 output hashes
matched the `13:09:51` capture at `13:11:04`, then remained stable during the
bounded rerun. The real-projection case failed at the **first** signal read,
`SignalRDaprProjectionDispatchProofTests.cs:86`, through `ReadNoticeAsync` at
lines 206/210. Its query-readback and final CaptureEvidenceAsync were never
reached. For synthetic aggregate
`distribution-projection-1bdb8827a9ba4c82a407c8aa6f91d029`, tenant
`tenant-a-e1636273bc104fac99c672dcd9e97ee9` and correlation
`2eae5aa4-4d65-4bdc-909b-7c3b37dd0738`, retained logs show event persistence,
event publication and command completion; two sample metadata calls and
`sample/method/project` returned HTTP 200. No correlated projection-persistence,
notifier or callback record follows. Projection persistence logging is Debug,
and `PromoteReplayAsync` can retain a notifier failure only in diagnostics, so
this absence proves neither missing persistence nor broker loss. The nearby
`issuer-invalid` and named-metadata failure concern another ordinary Direct
aggregate; they do not establish this failed case's cause. Direct's test passed.

Cohort C used a later, separately identified set of outputs, with no new build by
this task. The focused diagnostic changed only the three logging categories in
the command table. It again failed at the first notice, but exposed a correlated
**pre-publication** failure for aggregate
`distribution-projection-5bd00d3bfb3a4f91aa9a2271311d4725`, tenant
`tenant-a-fb58f10a73d44c65a9524715f480a092`, correlation
`db0f3264-4342-4e9d-8bba-4e621d2a2b76`: sample metadata HTTP 200 was followed
by `Named projection metadata refresh failed for 'sample/v1/counter'` at
`AdminOperationalIndexHostedService.RefreshAsync:111`, through
`NamedProjectionDispatchCoordinator.ResolveCatalogEntryAsync:1430` /
`SupportsMappedOnlyStreamedDeliveryAsync:60`, then
`ProjectionUpdateOrchestrator.DeliverProjectionAsync:130` logged
`ProjectionUpdateFailed` for that exact aggregate. Source places this failing
attempt before `/project`, projection-state persistence and notification.
The response body and precise unsuccessful metadata field were not captured;
no persisted projection readback or success manifest exists for this failure.
Proof-host failure diagnostics remain filtered/capped at 40 lines. This narrows
cohort C's failed attempt but does not explain cohort B or the historical failures,
and supports no speculative behavioral correction or authentication relaxation.

Both Redis executions failed overall with `TaskCanceledException` at
`WaitForReadyAsync:524` / `StartAsync:397`. The first failed at test line 43
(first host). The recheck reached line 230, starting `eventstore-unreachable-redis`
with `localhost:6390`, after positive signal/detail/clipping checks and the
backplane-disabled two-second negative control. Its report records positive host
PIDs `52716`/`69266`, signal delivery in 83 ms and the negative control, but no
unreachable-Redis result. The saved report's static `dotnet test ... --filter`
command was not run; the direct executable invocations above are authoritative.
Neither partial positive observations nor subsequent readiness justify a complete
Redis compatibility pass; both executions also had binary drift.

Representative SHA-256 identities distinguish the stable execution cohorts.
A = focused restart/full distribution/102 controls, B = failed projection/Direct
rerun, C = failed focused logging diagnostic. Paths are relative to their
project's `bin/Debug/net10.0/`; Host rows identify its actual linked/copied inputs.

| Output | Cohort A | Cohort B | Cohort C |
| --- | --- | --- | --- |
| `Host / Hexalith.EventStore.dll` | `bb9a7e4bdf8accbde44a4db0d7230595ce52023d32f8ae3663f44c17995537ac` | `397ff178adf5349a3cb0d1da0783627b9bde2003c48c3fc222d4bc128c3ba26f` | `0a38d49b7556c63877edaa7f3df7eef17663efb4c8e2b1d0dac3079e579830d7` |
| `Host / Hexalith.EventStore.Server.dll` | `bf4964b8a9fc9f04b2dd3915a5fb27235541559448b95a7d5f510e9718cd1ba9` | `aa3fad5bafe3aef194691c2f1a9d9b00a2de63744de5bf2e377d6159be3a703b` | `63ef9eba354a65ae9828760febdc74619e79da5507fb7a894ea1158b374a49ca` |
| `Host / Hexalith.EventStore.Gateway.dll` | `14edf73cfb2678fcbf2abfb82b19c2923ad46bc5cc9def0dca8a70518d18e04d` | `f7d9a6d4fd55410a4ddaf669565356b8c433824845ace9a2eebc04b4d99e04a4` | `e6bbe027ea82f3d3c9cece1bcd092f10c4a65fd4f3f9dff486876706c8dde6d3` |
| `Sample / Hexalith.EventStore.Sample.dll` | `006d2e1056a937c61a322f131849677fee7fb26267ee726179f9c4dbbfa51552` | `006d2e1056a937c61a322f131849677fee7fb26267ee726179f9c4dbbfa51552` | `dd22072aa8ee10ea9c4031992f02fafed171bb01db19313709aaef087c2fbd54` |
| `Hexalith.EventStore.Server.Tests / Hexalith.EventStore.Server.Tests.dll` | `c397ac910e4608df5360f7df5a641e59826368320b31f8f9ecfb16e259d6ccf9` | `21540e7128b0d77cc0ca7dae9c1177c0ed31fb8004f67153187b194fed559bb0` | `0f8d80ab1f116de1076c68beeb6cd9eb63e56680e25e5e1ba84eff21aae700af` |

| Output unchanged across A/B/C | SHA-256 |
| --- | --- |
| `Host / Hexalith.EventStore.deps.json` | `89e89b25a82349d764ede5a7cf238e713c34cce9bbdb1900f5e07c5034379388` |
| `Sample / Hexalith.EventStore.Sample.deps.json` | `231e7d0b311eb55a2693b79c88e63a79393d584a1238db6c7c6b9b45c3f46538` |
| `AppHost / Hexalith.EventStore.AppHost.dll` | `d37145fc9ab0ba9bca17e08bcc7bc12a01ac056018d55b70ce0ca177c2af2147` |
| `AppHost / Hexalith.EventStore.AppHost.deps.json` | `1db1fae3dfafb0ec5252cf317baab996a925e47b91b0ae8ab60161a79d90c7c1` |
| `Hexalith.EventStore.IntegrationTests / Hexalith.EventStore.IntegrationTests.dll` | `61c0baea7ec130c6a30523ba16866b6e8c1e41f174d191fd4da92eff3dc0af0f` |
| `Hexalith.EventStore.IntegrationTests / Hexalith.EventStore.IntegrationTests.deps.json` | `f9fdd5d585d2d60782a67f09f9f5f68717f364e97c73daa29c7b5ed9ad6f071e` |
| `Hexalith.EventStore.Server.Tests / Hexalith.EventStore.Server.Tests.deps.json` | `902d267452353b89570dbb43da8eb4aae9ee82fa1b0e7f2771aff9ac4d3e83ac` |

These representative identities and full temporary inventories establish
observed byte cohorts, not correspondence to all current sources, dependency
coherence, atomic launch identity or a reproducible build. The ambiguous
executions and distinct failed cohorts are not replaced by another result.

Lifecycle commands below all passed and used the explicit AppHost. Baseline
AppHost/CLI PIDs were `41980`/`41776`, with inspected projects/sidecars
Running/Healthy. Auxiliary PIDs were `45385`/`44797`:

```sh
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json
aspire wait eventstore --timeout 45 --apphost "$APPHOST" --non-interactive
aspire describe --apphost "$APPHOST" --format Json --non-interactive
aspire stop --apphost "$APPHOST" --non-interactive
# Run the Debug build, then start the auxiliary sample topology.
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json
aspire wait eventstore --timeout 45 --apphost "$APPHOST" --non-interactive
aspire wait sample --timeout 45 --apphost "$APPHOST" --non-interactive
aspire wait sample-dapr-cli --status up --timeout 45 --apphost "$APPHOST" --non-interactive
aspire resource eventstore stop --apphost "$APPHOST" --non-interactive
aspire resource eventstore-dapr-cli stop --apphost "$APPHOST" --non-interactive
aspire describe --apphost "$APPHOST" --format Json --non-interactive
# Focused restart, full distribution and server controls ran here.
aspire stop --apphost "$APPHOST" --non-interactive
```

The `13:03:50` and in-distribution `13:06:09 UTC` describes showed
`sample-ycfhfcee` / `sample-dapr-cli-qptvftnf` Running/Healthy and
`eventstore-njtdesux` / `eventstore-dapr-cli-xbgxpzrm` Finished. Subsequent
projection fixtures owned and stopped their own AppHosts. Explicit-AppHost
`aspire describe` at `13:09:50`, `13:16:48` and `13:22:18 UTC` found no AppHost
running for this repository. A read-only process scan at `13:22:40 UTC` found no
owned proof host or selected candidate sidecar remaining. Unrelated Projects and
Tenants workspace processes were preserved.

At `13:20:27 UTC`, separate Tenants AppHost `eventstore`/`sample` sidecars
PIDs `95449`/`96202` were observed with resources under
`/home/administrator/projects/hexalith/tenants/src/Hexalith.Tenants.AppHost/DaprComponents`.
Their `/proc` start times were `13:17:51.340`/`13:17:51.900 UTC`, before cohort C.
Background logs also contain other domains and earlier synthetic aggregates.
This is an external topology/shared-state overlap observation, not attribution of
any failure to those processes, an isolated-placement proof or authorization to
stop them. Earlier absence observations cannot establish exclusivity throughout
another run.

Pre/post Docker inspection found unchanged Redis/placement/scheduler container
IDs, image IDs, start times and running states, matching the earlier support
identity table. Current start times were `09:19:21 UTC`; Redis reported `6.2.21`,
placement/scheduler used the `daprio/dapr:1.18.7` image digest
`0ac4fadf0016be85065cdebab952a8350e468cf9283d26777b0307a14a10aecc` and Redis used
`c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`.
This task did not reconfigure, restart or stop those services. Inotify remained
`1,048,576` at every recorded command boundary. The `13:22:17 UTC` accessible
scan counted 3,586 watch lines in 20,065 readable fdinfo files, with 37
inaccessible/disappeared entries; no sysctl write was needed.

Qualification remains incomplete: the current stable real-projection failures,
failed/identity-ambiguous Redis regressions, historical projection/identity and
Go failures, separate unrerun Contracts validator blocker and missing CI are
retained. No validator exemption, whole-story completion, production runtime/
broker/issuer selection, shipped enrollment, canonical freshness fault proof,
atomic both-host transition/rollback or authorized disposition is supplied.
Story 2.16 retains NFR5 primary ownership. Original story `backlog`, active
spec/sprint `in-progress`, frozen intent, unchecked whole-story tasks and
`accepted_exceptions: []` remain unchanged.

Documentation checks: `bash scripts/check-committed-files.sh` passed (9,091
tracked entries checked) and `git diff --check` passed before this append; both
passed when rerun after it and after review corrections. Prior story contents,
frozen intent, spec baseline/status and sprint/exception bytes were checked for
preservation. Five documentation findings were corrected from existing receipts;
edge-case and verification-gap reviewers found no additional issues. No runtime
tests were repeated for those corrections.


## Verification — October 25 dee84974 Continuation

Run-start/source revision: `dee8497458bfc92e094ac66fb977b2eddabd6c93`. CI run/link: none;
these are local checks under architecture AD-12. The first recorded command
already saw a concurrent edit to
`docs/implementation/ext-conv-ai-1-eventstore-authority-handoff.md`; it is preserved
and outside this continuation. Implementation adds this Verification section;
review triage is recorded in the active spec. This continuation changed no application, fixture, runtime,
dependency or production graph and made no staging, commit, push, branch or
submodule update.

The complete incremental Debug source-reference solution build passed, including
both host graphs, with zero warnings/errors. It changed **45** selected output
files and none of **17,581** measured source/configuration files. Before and after
each command, the temporary recorder measured those tracked and untracked source/
configuration files across the root and its root-declared submodules, **550**
selected top-level executable/DLL/deps/runtimeconfig outputs, the installed and
selected daprd executables, Aspire CLI, HEAD and checkouts. All those measured
identities remained stable during the first four test commands below. During
Redis, six concurrent source files changed: `CompleteCommandSourceContext.cs`,
`DomainServiceRequestRouter.cs`, `SourcePublicationAuthority.cs`,
`SourcePublicationAuthorityActor.cs`, `SourcePublicationAuthorityClient.cs` and
`SourcePublicationOperationContext.cs`, under DomainService and Server/Streams.
The Redis command retained stable outputs, runtime/CLI hashes, HEAD and checkouts;
its result does not establish correspondence to those later sources. No rebuild
followed the drift. Git status also retained concurrent authority spec and handoff
documentation edits, outside the source hash inventory. These are incremental build and execution-stability observations, not a clean reproducibility, complete
compiler-input or atomic module-load proof.

Builds was `e171c5a788eab183b32683431b084198c9b8d373`,
Commons `1663028fb6d9f46a0bf17919edeb21bb99be1587`,
Tenants `8804c10b8a900860ea2394c81eff78d23bb3c0a4` and
Platform `67e2d42e5aa6a92fb0a20ea899790d4ea7e0b56f`.
The explicitly selected fixture-only runtime remained
`/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement`, version
`1.18.7-local-2-13-retirement`, SHA-256
`a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6`.
Installed daprd `1.18.7` remained SHA-256
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.
Observed tools were SDK `10.0.402`, .NET `10.0.13`, xUnit runner `4.0.2`, Dapr
CLI `1.18.2`, Aspire CLI
`13.6.1+3751e615ad660621f2a7aa4c390ef48e08a0eed8` and Docker `29.8.2`.
The integration dependency manifest lists `Dapr.Client` `1.18.10` and
`Microsoft.AspNetCore.SignalR.Client` `10.0.13`. No Dapr runtime was rebuilt,
installed or substituted, and no dependency was updated. No Go checks were
repeated; the existing pinned full-suite deadline and reflective-membership race failures remain open.

Temporary command receipts, reduced lifecycle output, logs and scenario captures
are under `/tmp/story-2-13-20261025-dee84974-rnnufpr2`; no committed packet or validator
was added. Every recorded build/lifecycle/test command received:

```sh
export EnableKeycloak=false
export HEXALITH_STORY_2_13_DAPRD_PATH=/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement
export HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-dee84974-rnnufpr2/scenario-output
export R10A2_EVIDENCE_DIRECTORY=/tmp/story-2-13-20261025-dee84974-rnnufpr2/redis-output
IT=tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests
SERVER=tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests
APPHOST=src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

Only the two distribution proof sidecars consume the selected candidate; composed
AppHosts and the ordinary Direct fixture retain installed daprd. The build and
five test commands ran sequentially. Read-only observations also ran during tests.
All test rows have zero failures, skips and runner errors.

| Exact command, expanding the aliases above | Result and UTC interval on 2026-10-25 |
| --- | --- |
| `dotnet build Hexalith.EventStore.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true` | Passed, 0 warnings/errors; `14:13:01–14:15:23`. |
| `"$IT" -method '*DaprRedisStreams_ReplicaRestart_ReauthorizesAndReconcilesPersistedState'` | 1 passed; `14:16:06–14:16:52`. |
| `"$IT" -class '*SignalRDaprDistributionRuntimeProofTests'` | 6 passed; no `-stopOnFail`; `14:16:56–14:19:18`. |
| `"$SERVER" -class '*ETagActorTests' -class '*SignalRProjectionChangedBroadcasterTests' -class '*DaprProjectionChangeNotifierTests' -class '*DaprProjectionChangeNotifierSignalRTests' -class '*ProjectionChangedNotificationValidatorTests' -class '*DomainServiceWorkloadAssertionHandlerTests'` | 102 passed; `14:19:22–14:19:23`. |
| `"$IT" -class '*SignalRDaprProjectionDispatchProofTests' -class '*ValidProjectionRoundTripE2ETests'` | 2 passed; `14:19:30–14:20:34`. |
| `"$IT" -class '*SignalRRedisBackplaneRuntimeProofTests'` | 1 passed; `14:20:38–14:23:40`. |

The Redis report `redis-output/evidence-2026-10-25-142340Z.md` records origin
PID `71068`, target PID `4281`, and cross-instance signal delivery in **21 ms**.
With the backplane disabled, no matching cross-instance signal arrived during
the two-second negative-control window. The unreachable-Redis host, PID `82832`
using `localhost:6390`, started and returned HTTP 200 for the proof broadcast;
its `RedisConnectionException`/`FailOpenFailure` broadcast diagnostic records
**5747.1851 ms**. HTTP acceptance does not establish notification delivery or
projection freshness. This is a transport-only compatibility comparison, with
the concurrent source-drift limitation above. The report's static
`dotnet test ... --filter` command is an unexecuted template; the direct
executable invocation above is authoritative.
Current passing runs neither establish the cause nor resolve the earlier
projection/Direct failures, Redis failures or binary-identity ambiguity. No
behavioral correction is claimed.

The test-only profile remains `pubsub.redis` v1 with `consumerID: "{uuid}"`,
`state.redis` v1, exact `dapr.io/v2alpha1` subscriptions, shared app ID `eventstore`
and independent proof sidecars. The catalog test stops both sidecars and starts
both after adding an exact tenant topic, preserving product hosts and connections;
it proves no automatic tenant enrollment or live subscription refresh. The
scoped-delivery, duplicate/reconciliation and denial assertions passed. Existing
authorization, metadata ceilings, caller budgets and the 15-second distribution
signal wait were unchanged.

The synthetic-distribution manifest and passing test code identify the denial
coverage: on both hosts, anonymous and expired identities fail connection;
authenticated wrong-tenant and missing-tenant-grant identities fail ordinary
joins and scoped joins using `forged-scope`, and their `malformed:tenant` joins
fail. An authorized tenant-A client also makes a failing malformed-tenant join
on the first host; the wrong-tenant denial text omits tenant A. Both tenant-B
clients have positive own-tenant signal/detail controls and receive no tenant-A
hints during the bounded publication, rejoin and recovery observations. Missing,
wrong-tenant-, wrong-projection-, wrong-topic-bound and expired provenance are
denied beside valid-provenance controls; 17-entry and multibyte-oversized metadata
are denied on both hosts beside valid metadata controls. On first-host reconnect,
a replacement tenant-B identity cannot rejoin tenant A's ordinary or scoped group;
a fresh tenant-A identity rejoins. The restart cases separately deny other-tenant
ordinary/scoped rejoins on the restored second host and require fresh membership.
These commands exercise replacement-identity denial, not in-place credential
revocation, and do not establish conflicting-tenant-claim coverage.

The nine existing scenario manifests map the measurements above and below to
exact captures. Paths are relative to
`HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY` (`scenario-output/` under the temporary
parent); no capture was rewritten for these documentation corrections.

| Measurements | Relative manifest path |
| --- | --- |
| Focused reused-endpoint restart | `replica-restart-reused-internal-endpoint-20261025T1416070837251Z-188302e0ce8c4fb29211718094fd5a64/manifest.json` |
| Full-class reused-endpoint restart | `replica-restart-reused-internal-endpoint-20261025T1416570325432Z-7e2f30ea20684e52ac19182905f17a45/manifest.json` |
| Connection reset/refusal | `provider-network-outage-20261025T1417429688497Z-26290a00ec0a41e98d8f4ace8faba6d3/manifest.json` |
| Silent blackhole | `provider-network-blackhole-20261025T1417521057561Z-5a70d212e68e4df6a4f872deb8656929/manifest.json` |
| Scoped delivery, denial and reconciliation | `synthetic-distribution-20261025T1418031133088Z-6883a725d4f444c8b80317b523212947/manifest.json` |
| Full-class fresh-endpoint comparison | `replica-restart-fresh-internal-endpoint-20261025T1418276090972Z-5d13f37a113b4daeb8a4c5a62db9ff41/manifest.json` |
| Catalog before rollout | `topic-catalog-before-rollout-20261025T1419111936959Z-3a015e555c4f4adbacf2d2eb5d645586/manifest.json` |
| Catalog after rollout | `topic-catalog-after-rollout-20261025T1419111936959Z-45baa726384d4352ac87d530a15da3ac/manifest.json` |
| Real projection signal/detail/readback | `real-projection-distribution-20261025T1420223533912Z-9c83331ed9a5440e89c9b76366aaca29/manifest.json` |

Both reused-endpoint captures establish initial ETagActor ownership on the second
proof host and Dapr readback of version/ETag `1` → `2`. Each restarts that process,
requires a fresh authorized rejoin and accepts exactly 2,048 serialized metadata
bytes on both hosts while rejecting 2,129/2,050/2,060-byte vectors on both.

| Run | Original → restored host PID | Original → restored sidecar PID | Original → restored internal gRPC port | Logical Dapr read-model key |
| --- | --- | --- | --- | --- |
| Focused reused endpoint | `68432` → `69355` | `68458` → `79620` | `36043` → `36043` | `story-2-13:tenant-a-faa4ff35cd76:restart-faa4ff35cd76-1` |
| Full-class reused endpoint | `84322` → `85645` | `84349` → `96716` | `34969` → `34969` | `story-2-13:tenant-a-c0cf21e79f5c:restart-c0cf21e79f5c-0` |
| Full-class fresh-endpoint comparison | `16638` → `18417` | `16837` → `31662` | `39627` → `46601` | `story-2-13:tenant-a-40bbf617efff:restart-40bbf617efff-0` |

The fresh endpoint remains comparison-only evidence and does not resolve
reused-endpoint failures. These synthetic read-model checks establish no real
projection completion or canonical UI freshness.

Reset disconnected **27** connections and refused
**1786** reconnects; silent blackhole discarded
**5135** bytes with zero proxy disconnects/rejections.
Both hosts' reads cancelled with `OperationCanceledException` in
1998.8370–2004.9188 ms
against the unchanged 2,000 ms caller budget and 5,000 ms outer bound.
Reset publication returned `DaprException` after 58.7324 ms;
blackhole publication cancelled after 1999.5951 ms.
Restore read versions `[1,1]` and ETags `["1","1"]`, followed by final version
`2`/ETag `"2"` in both cases; denied clients received no hints.
Outage publication remains `unknown-until-reconciliation`. These cases prove no
real projection completion, canonical UI freshness, saturation/backpressure,
retention or production envelope.

The separate real-projection proof records command `Completed`, one event,
`ProjectionBacked` query provenance, persisted count `1` on both hosts and
identical authoritative ETag `"Y291bnRlcg.pE_u4thN7EmsQyET6cCVeQ"`. Both authorized clients
received signal/detail with scope `counter-status` and
`change-kind=counter-projection-updated`. Other-tenant signal and detail flags
were false during four concurrent one-second bounded channel reads; those
negative observations cover those windows only.

| Correlation identifier | Test-generated value |
| --- | --- |
| Tenant | `tenant-a-80c88aeedaa942be93b1e7de6d83eb0a` |
| Exact topic | `tenant-a-80c88aeedaa942be93b1e7de6d83eb0a.counter.projection-changed` |
| Aggregate | `distribution-projection-b4922122373e42ceada782d51af68cd6` |
| Correlation | `3f1a4301-bcaf-48f0-bb59-a36e97934ea6` |

| Proof node | Host / sidecar PID | Authorized connection | Other-tenant connection |
| --- | --- | --- | --- |
| First | `65628` / `65707` | `5mB3hKDlyLpQBw2ejiqefg` | `HbDpeo8bhg8hW17dBYefFw` |
| Second | `65786` / `65813` | `n3ysX9mGQ_4yc5LcdDY0Mw` | `ZIPaCG05ls-DP4t6paQUsw` |

Generated component SHA-256 identities for that real-projection scenario only:
`pubsub.yaml` = `483c2de2d90aa876a80efe6499edc98a82903a46ddd98a79a9f610ec2719b001`,
`statestore.yaml` = `8cfd8564018ac754f5d3f14acfc215f3d2caf186cb1e14b7b377b1d4660d023a`,
and `subscription-0.yaml` =
`0b330d8531002d1fb62b0e78604243427f8e299672c3485d40d76963f34a159f`.
This remains optional legacy projection-response detail in the exact-topic
Development profile, with no production issuer/profile or transition approval.

Key measured output SHA-256 values below agree before/after all five test commands.
Paths are relative to each project's `bin/Debug/net10.0/`; these hashes identify
observed bytes and do not supply atomic launch identity or clean reproducibility.

| Project / output | SHA-256 |
| --- | --- |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.dll` | `9b00bd75aacdce6be7480d560e1c973c49c10d4e2b7508bed7e32b1f71035ce7` |
| `src/Hexalith.EventStore` / `Hexalith.EventStore.deps.json` | `89e89b25a82349d764ede5a7cf238e713c34cce9bbdb1900f5e07c5034379388` |
| `src/Hexalith.EventStore.Server` / `Hexalith.EventStore.Server.dll` | `466208fa2cddccb6b6e70817a96f073b7ed31607e724dfe0610a5132e2bba018` |
| `src/Hexalith.EventStore.Gateway` / `Hexalith.EventStore.Gateway.dll` | `f6962d0ad7282834538239e706a10eebf321f66d5bae9ceb1246ef460c24dbf8` |
| `samples/Hexalith.EventStore.Sample` / `Hexalith.EventStore.Sample.dll` | `6691e38b2116c8598b45c0b4edef611d65479c5eef37fd2c2099a28af506b105` |
| `samples/Hexalith.EventStore.Sample` / `Hexalith.EventStore.Sample.deps.json` | `231e7d0b311eb55a2693b79c88e63a79393d584a1238db6c7c6b9b45c3f46538` |
| `src/Hexalith.EventStore.AppHost` / `Hexalith.EventStore.AppHost.dll` | `334d34ea96b838c849c93c2a188e9a48912498bcf0f1f203139262eabf552586` |
| `src/Hexalith.EventStore.AppHost` / `Hexalith.EventStore.AppHost.deps.json` | `1db1fae3dfafb0ec5252cf317baab996a925e47b91b0ae8ab60161a79d90c7c1` |
| `tests/Hexalith.EventStore.IntegrationTests` / `Hexalith.EventStore.IntegrationTests.dll` | `e48b5daac2596fff14cf5c5b6d900c75e918c2222960dff690a44972177778e8` |
| `tests/Hexalith.EventStore.IntegrationTests` / `Hexalith.EventStore.IntegrationTests.deps.json` | `f9fdd5d585d2d60782a67f09f9f5f68717f364e97c73daa29c7b5ed9ad6f071e` |
| `tests/Hexalith.EventStore.Server.Tests` / `Hexalith.EventStore.Server.Tests.dll` | `1a210270e78e569f78f64b553c895723984b29ccd986e630ff64c0e9079812da` |
| `tests/Hexalith.EventStore.Server.Tests` / `Hexalith.EventStore.Server.Tests.deps.json` | `902d267452353b89570dbb43da8eb4aae9ee82fa1b0e7f2771aff9ac4d3e83ac` |

Baseline AppHost/CLI PIDs `58304`/`57939` started,
all inspected project/sidecar resources were Running/Healthy, and the explicit
AppHost stop passed before build. Auxiliary PIDs were
`55594`/`55430`. Each lifecycle command had a
separate receipt; exact ordering was:

```sh
aspire describe --apphost "$APPHOST" --format Json --non-interactive # initially no AppHost
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json
aspire wait eventstore --timeout 45 --apphost "$APPHOST" --non-interactive
aspire describe --apphost "$APPHOST" --format Json --non-interactive
aspire stop --apphost "$APPHOST" --non-interactive
# Run the recorded Debug build, then start auxiliary sample support.
aspire run --apphost "$APPHOST" --detach --no-build --non-interactive --format Json
aspire wait eventstore --timeout 45 --apphost "$APPHOST" --non-interactive
aspire wait sample --timeout 45 --apphost "$APPHOST" --non-interactive
aspire wait sample-dapr-cli --status up --timeout 45 --apphost "$APPHOST" --non-interactive
aspire resource eventstore stop --apphost "$APPHOST" --non-interactive
aspire resource eventstore-dapr-cli stop --apphost "$APPHOST" --non-interactive
aspire describe --apphost "$APPHOST" --format Json --non-interactive
# Focused restart, full distribution and server controls run here.
# During distribution, an additional read-only describe/process/support observation ran.
aspire stop --apphost "$APPHOST" --non-interactive
# Projection/Direct and Redis proofs ran, then final describe confirmed no AppHost.
aspire describe --apphost "$APPHOST" --format Json --non-interactive
```

The `14:16:03` and in-distribution `14:17:56 UTC` describes showed
`sample-cevmadhj`/`sample-dapr-cli-vmfxrtxx` Running/Healthy and
`eventstore-wvsaqmrj`/`eventstore-dapr-cli-ytdwzkqe` Finished.
Projection fixtures subsequently owned and stopped their own AppHosts.
Lifecycle output was reduced to public process/resource identity, state and
timestamps; environment/parameter values and dashboard authentication URLs are
not retained in those reduced logs.

Pre-test Docker inspection (`14:12:23 UTC`) and in-distribution/final inspection
found the same Redis/placement/scheduler container IDs, image IDs, start times
and running states as the preceding support identity table; start times remain
`09:19:21 UTC`. Redis reported `6.2.21`, image digest
`c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`;
placement/scheduler retained `daprio/dapr:1.18.7`, image digest
`0ac4fadf0016be85065cdebab952a8350e468cf9283d26777b0307a14a10aecc`.
This continuation reconfigured, restarted or stopped no shared service.

Final explicit-AppHost describe found no running AppHost. The independent
`14:24:29 UTC` product-process scan found no owned proof host or selected
candidate sidecar remaining. Unrelated Parties and Projects workspace processes
were preserved; their observed placement/scheduler endpoints were separate from
this proof's `50005`/`50006`. The in-distribution process receipt records the
two proof `eventstore` sidecars and the auxiliary topology's `sample`,
`eventstore-admin`, `eventstore-admin-ui`, `sample-api`, `sample-blazor-ui`,
`tenants` and `tenants-api` sidecars (and their CLI wrappers where present) on
those shared endpoints at that instant. These bounded observations establish no
exclusive placement, backend ownership or unobserved-interval topology guarantee.
Inotify remained `1,048,576` at every recorded boundary; final accessible scanning
counted **3597** watch lines in **16694** readable fdinfo
files, with **36** inaccessible/disappeared entries. No sysctl
write or unrelated-process interruption occurred.

The inherited Contracts blocker is historical: `git show bbfd0fada14ce11e137365637238af4fec2f7f7f:_bmad-output/implementation-artifacts/evidence/story-2-13/qualification-2026-10-21-restart-trace.md`
lines 160–167 retains the earlier blackhole command
`./tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests`,
with **2,348 passed, 1 failed and 2 skipped**. Its package-authority validator
reported **10,898 errors** in already tracked historical retained MSBuild evidence.
This supplies the inherited failure's identity, not a current Contracts result;
no current Contracts rerun or validator exemption is supplied.

Current passes do not erase the historical failures or qualify the production
path. The existing Go failures, separate unrerun Contracts validator blocker,
missing CI run, selected production runtime/durable broker/binding-capable issuer,
shipped topic enrollment, canonical completion/freshness faults, atomic both-host
transition/rollback and authorized disposition remain open. Story 2.16 retains
NFR5 primary ownership. Original story `backlog`, active spec/sprint `in-progress`,
frozen intent, unchecked whole-story tasks and `accepted_exceptions: []` remain
unchanged.

Documentation checks after this append: `bash scripts/check-committed-files.sh`
passed (**9,096** tracked entries checked), and `git diff --check` passed.
Pre-review preservation checks confirmed the original story prefix, unchanged
active spec (including frozen intent and baseline), sprint status and exception
register; HEAD remained the run-start revision at those checks.

These five documentation corrections use existing receipts, manifests and test
source; no runtime test or topology command was repeated.

Parent review subsequently observed concurrent commit
`8fb04342d487e8ec1df80a15a75f967227201294`, outside the tested revision and reviewed
change. The active spec gains only this continuation's review triage; its frozen
intent, baseline and status remain unchanged.
