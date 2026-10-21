# Story 2.13 continuation — silent network fault qualification

The run started from `7217b5bf740b80ae92858378785b496e6b564285`; the frozen
spec baseline remains `f2b480b68a8bd5369b3cdf550340e0fadfb2332b`. Existing
October 11, 19 and 20 packets are unchanged. This continuation is local
Development qualification only. It supplies no owner approval, CI seal,
accepted exception, NFR5 closure or production eligibility. The original story
record remains backlog, while the active build spec and sprint row remain
in-progress. Both Redis backplane dependency graphs retain their behavior.

## Change and exact operating envelope

`SignalRDaprNetworkFaultProxy.cs` adds a silent bidirectional byte-discard
mode. Established TCP connections stay open, reconnects remain accepted, and
the proxy counts discarded bytes without logging or persisting their contents.
Each forwarding direction has one 16,384-byte working buffer. This deliberately
models an unresponsive provider route; it is not a kernel packet-loss,
TCP-retransmission, saturation or retention proof. The existing reset mode
still severs connections and refuses reconnects. Both modes affect only this
fixture's loopback route to the existing Redis service.

The existing network test now runs both modes. Before fault injection it proves
two independent hosts/sidecars hold authorized clients and deliver a scoped
tenant signal. During the fault, publication and each sidecar's Dapr read must
fail or cancel under the unchanged two-second caller budget and five-second
outer bound. A failed publication remains unknown, never proof of absence or
completion. After restoration, both sidecars must re-read version `1` with the
original ETag. An explicit Dapr write advances it to `2`, and a previously
unused recovery topic must deliver signal/detail on both hosts. Tenant-B
clients receive neither. The blackhole case additionally requires actual
discarded traffic and zero proxy-initiated resets or refused connections.

The [profile identity](runs-2026-10-21-blackhole/profile-identity.json) records
SDK `10.0.402`, .NET runtime `10.0.13`, Dapr CLI `1.18.2`, daprd `1.18.7`,
Dapr .NET `1.18.10`, SignalR `10.0.13`, Redis client `3.4.5`, Aspire `13.6.1`,
runtime binary hashes, actual Builds identities and local container identities.
Generated `pubsub.redis/v1` and `state.redis/v1` components and exact-topic
subscriptions remain temporary fixture inputs, scoped to app ID `eventstore`.
Each proof host has its own sidecar and channel token. Application processes
receive no Redis backplane setting. Logical application reads/writes and
publication enter through Dapr; provider/control-plane observations are
separate diagnostics. No additional deployment component path was selected.

## Baseline, failures and topology distinction

The required pre-edit baseline used `EnableKeycloak=false aspire run --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach
--non-interactive`, followed by `aspire wait eventstore --timeout 45` and
`aspire describe --format Json` with the same explicit AppHost. EventStore and
its sidecar were Healthy. Tenants and Tenants API were Finished, so this is
not a whole-topology health pass. `aspire stop` succeeded before edits.
[Sanitized baseline state](runs-2026-10-21-blackhole/baseline.json) retains no
environment values or dashboard credentials.

The first broad class run failed while waiting for a signal after replica
restart, then was canceled. Its separate reset case passed. The next focused
network run passed reset but the blackhole case failed before injection:
the second sidecar did not become ready within 60 seconds. Neither failure
is hidden or reclassified as a passing blackhole result. An unchanged focused
replica-restart rerun passed in 151.875 seconds; it does not erase the original
lost-signal observation.

[Startup diagnosis](runs-2026-10-21-blackhole/startup-diagnosis.json) identifies
sequential metadata probes from `AdminOperationalIndexHostedService.StartAsync`
to the fixture's inherited sample and Tenants registrations. Neither domain
service is supplied by the standalone synthetic fixture. Retained requests
spent approximately 12–16 seconds each before returning HTTP 500. This supplies
a plausible startup-budget explanation, not a definitive cause for the first
lost signal. No timeout or delivery assertion was relaxed.

For a separately bound retry, the existing AppHost was started to supply the
sample dependency. Its [sanitized topology](runs-2026-10-21-blackhole/support-topology.json)
records the auxiliary EventStore/sample and their sidecars, with the same
Tenants limitation. The auxiliary EventStore retains Direct; its startup is
not Dapr fan-out evidence. Proof clients still connect only to the two
independently identified fixture hosts. This additional topology is part of
that retry's operating envelope and cannot be silently omitted when reusing it.
The supported broad retry reproduced the post-restart signal timeout in
85.019 seconds, so startup-budget pressure is not a sufficient explanation.
The callback invokes `ETagActor.RegenerateAsync` before broadcasting; actor
placement/routing after restart is a diagnostic lead, not an established cause.
The replica delivery gap remains open with the original signal-wait bound.

The final diagnostic phase adds failure-only fixture capture and targeted
controller Debug receipt logging. A capture explicitly records failed
diagnostics rather than a passing scenario, keeps the 15-second signal wait,
and has a separate ten-second capture budget. Exception excerpts use the fully
redacted log path. The diagnostic focused restart passed, then the full class
passed all five cases on the final fixture/Node/retention source identity.
The failure-only capture did not trigger in either diagnostic run. These passes
do not erase the earlier two signal timeouts or establish their cause.

New captures retain copied MSBuild inputs with a `.source` suffix and explicit
original-path mappings. The [retention transformation](runs-2026-10-21-blackhole/retention-transform.json)
renamed 70 earlier files in this new packet without changing their bytes or
any of the 17 already emitted manifest/receipt JSON files. The verifier follows
the declared aliases for those earlier captures. Historical packets remain
unchanged. The new packet contains no executable MSBuild file surface.

## Verification

<!-- RESULTS -->
| Check | Receipt | Result |
| --- | --- | --- |
| Full Debug source-reference graph | [graph-build.json](runs-2026-10-21-blackhole/graph-build.json) | Passed; 0 warnings/errors; 29.02 s |
| Final diagnostic/retention Debug build | [diagnostic-retention-build.json](runs-2026-10-21-blackhole/diagnostic-retention-build.json) | Passed; 0 warnings/errors; 15.04 s |
| Final diagnostic runtime class | [diagnostic-distribution-tests.json](runs-2026-10-21-blackhole/diagnostic-distribution-tests.json) | **5 passed**, 0 errors/failed/skipped; 244.489 s; `06:54:10.097079`–`06:58:14.777832` UTC on October 21 |
| Final diagnostic focused restart | [replica-restart-diagnostic.json](runs-2026-10-21-blackhole/replica-restart-diagnostic.json) | 1 passed; 75.327 s |
| Earlier supported reset/blackhole pair | [supported-network-tests.json](runs-2026-10-21-blackhole/supported-network-tests.json) | 2 passed; 85.559 s; earlier fixture/binary identity retained separately |
| Earlier unchanged focused restart | [replica-restart.json](runs-2026-10-21-blackhole/replica-restart.json) | 1 passed; 151.875 s |
| Required existing Release Contracts executable | [required-contracts-tests.json](runs-2026-10-21-blackhole/required-contracts-tests.json) | 2,351 total: **2,348 passed, 1 failed, 2 skipped**, 0 errors; 414.412 s |
| Existing guarded-sprint fallback | [guarded-sprint-fallback.json](runs-2026-10-21-blackhole/guarded-sprint-fallback.json) | 3 passed, 0 failed/skipped/errors; 0.134 s |
| Narrow YAML/link/frozen-intent handoff check | [handoff-check.json](runs-2026-10-21-blackhole/handoff-check.json) | Passed; accepted-exception list remains empty |

| Retained non-passing run | Receipt | Observed result |
| --- | --- | --- |
| Initial broad class, canceled after failure | [distribution-tests.json](runs-2026-10-21-blackhole/distribution-tests.json) | 2 started: 1 passed, 1 failed; exit 58; post-restart first-host signal timeout |
| Standalone reset/blackhole pair | [network-tests.json](runs-2026-10-21-blackhole/network-tests.json) | Reset passed; blackhole failed its second-host startup before injection; 1 failed of 2 |
| Supported broad class | [final-distribution-tests.json](runs-2026-10-21-blackhole/final-distribution-tests.json) | Stopped on restart timeout: 1 failed of 1; 85.019 s |
<!-- END RESULTS -->

The required Release command was exactly
`./tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests`.
It ran the existing Release assembly; no new Release assets were produced.
Its failure was
`ContractsPackageDependencyTests.SharedConsumerAuthorityValidatorPassesForEveryTrackedMsBuildSurfaceAsync`:
the existing package-authority validator reported 10,898 errors in already
tracked retained evidence, mainly historical October 20 MSBuild snapshots.
The then-untracked new packet appeared zero times in that output. No historical
packet or validator exemption was changed. The two package checks skipped
because `EVENTSTORE_PACKAGE_CONTRACT_DIR` was unset. Existing Release coverage
is distinct from the current Debug source-reference builds and runtime proof.
The original general runner did not recognize the dotted apphost name as an
assembly argument; the separately retained Release binary sample was taken
during that run, with modification times preceding it. The focused fallback
uses the corrected runner and binds the executed Release assembly directly.

The final full class includes the reset and blackhole modes, replica
restart/authorization/serialized bounds, synthetic duplicate/denial/readback
controls and exact-topic catalog rollout. It supplies no newly run real-domain
projection composition, ordinary Direct regression or canonical UI completion
proof. Those October 20 results stay on their original source/binary identities.

Each [command receipt](runs-2026-10-21-blackhole/) retains exact argument arrays,
UTC interval, exit code, output hash, source/spec snapshots, and before/after
assembly identities. Passing scenario manifests retain generated component
bytes, Dapr metadata, replica/connection identities, readback and fault counts.
The original failed/interrupted commands stay separately identified. Other
workspace activity advanced HEAD to `ae917d3e4dbab20b8982c5222a13611c3e3faf35`;
this agent made no Git mutation. Command receipts independently bind their own
start/end revisions and exact bytes. The original run-start revision and
frozen spec baseline are preserved.

The [final cleanup observation](runs-2026-10-21-blackhole/closing-observations-final.json)
finds every recorded fixture process gone, all proxy ports closed, no owned
sidecar or temporary component directory, and unchanged shared Redis,
placement and scheduler container identities. Both owned auxiliary AppHost
instances stopped through the Aspire CLI. No sysctl was changed. The local
verifier is `python3 -B _bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-blackhole/verify-packet.py`.

## Disposition and Story 3.17 handoff

The current Story 3.21 artifacts were inspected again rather than relying on
historical missing-profile notes. Its `deploy/dapr/qualification/runtime-lock.json`
still records `productionEligible: false` and `selectedBroker: null`.
The ES-DIAG follow-up also retains `selected_broker: null`; its approval is
planning intent, not AD-26 ratification or publication authority. Neither
EventStore nor Platform has `deploy/dapr/production-profile.yaml` at the
declared slot. [Retained authority observations](runs-2026-10-21-blackhole/authority-observation.json)
bind the inspected bytes and absent paths.

The self-hosted Kubernetes MVP target remains unchanged. No cluster operation,
credential enrollment, external service provisioning or production transition
was performed. Azure SignalR remains the required comparison-only unrun
candidate. This continuation changes only local fault evidence; the earlier
three-candidate operation matrix and its accountable gap owners remain valid.

Required next decisions and proofs remain: a selected durable broker/runtime
and enrolled bound-provenance issuer; shipped tenant enrollment; canonical
completion/freshness behavior under faults; saturation/backpressure, retention
and production-path qualification; and an atomic both-host/profile transition
with compatible rollback. Architecture, Security, Operations, Test and the
transport owner must approve the resulting disposition. Story 2.16 retains
NFR5 primary ownership. Keep `accepted_exceptions: []`, Story 2.13 backlog, and
the retained Redis paths in Story 3.17's unresolved inventory/guard handoff.
