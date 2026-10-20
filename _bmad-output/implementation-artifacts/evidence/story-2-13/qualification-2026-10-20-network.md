# Story 2.13 continuation — isolated network faults and topic catalog rollout

Run-start revision: `5d140359cc4021156ef32e8cb5e7bfa763a1f00c`. The frozen
baseline remains `f2b480b68a8bd5369b3cdf550340e0fadfb2332b`. The October 11,
October 19 and earlier October 20 packets remain unchanged. This packet supplies
local qualification evidence only; there is no accepted exception, approved
replacement, CI seal, production promotion or NFR5 closure. Story 2.13 remains
backlog and the existing Redis runtime remains unresolved.

## Target clarification and observed profiles

The user clarified: **“production is on local 192.168.1.30 kubernetes server.
Azure or AWS is not MVP.”** Self-hosted Kubernetes is the MVP target. Azure
SignalR remains the required comparison-only candidate; its service behavior
is unrun and no cloud service is provisioned or required for this local work.
Its lack of a service profile is not evidence of an intrinsic Dapr capability
gap or a permanent Redis exception.

The retained [read-only observation](runs-2026-10-20-network/kubernetes-observation.json)
identifies context `jpiquot@local`, Kubernetes `v1.34.9`, Dapr control-plane
images and the isolated EventStore sidecar on `1.18.1`. The observed
`ext-secrets-1-isolated/eventstore` deployment has one replica, `Production`
environment, and an isolated OIDC authority. Keycloak also has a public
`auth.tache.ai` ingress. No Secret resources were read and no cluster resources
were mutated. These observations identify existing infrastructure; they do
not select its isolated workload, issuer, component, or image for production
notification qualification. Dapr `1.18.1` differs from the local test runtime
`1.18.7` and is below AD-26's `1.18.3` minimum. No upgrade is performed here.

The [local profile identity](runs-2026-10-20-network/profile-identity.json)
records .NET SDK `10.0.402`, Dapr CLI `1.18.2`, daprd `1.18.7`, Aspire CLI
`13.6.1`, Dapr .NET `1.18.10`, SignalR Redis package `10.0.13`, Redis client
`3.4.5`, and the exact Builds gitlink. Components are `pubsub.redis/v1` with
per-sidecar `{uuid}` consumer groups and `state.redis/v1`, scoped to shared
app ID `eventstore`. Both real hosts have their own sidecar and channel token;
application processes receive no backplane setting. Logical publication and
readback enter through Dapr. Synthetic read models are separate from the
previous real sample projection composition.

Current `JwtWorkloadAssertionIssuer.CanBindResources` supports explicit
authority enrollment through `EnableResourceBindingRequests` plus client
credentials, as well as admitted symmetric Development signing. The missing
production gate is a **selected, enrolled and proven** binding-capable issuer
profile; it is not an inherent inability of all authority-mode code. Observed
deployment environment names contain neither the binding opt-in nor an
explicit notification transport selection. No grant, configuration, or scope
is inferred from the existence of Keycloak or a client-secret setting.

## Selected code and component paths

The disposable profile uses the existing
`SignalRDaprDistributionFixture.cs` and
`SignalRDaprDistributionRuntimeProofTests.cs`, plus the new test-only
`SignalRDaprNetworkFaultProxy.cs`. No additional deployment component path
was selected. Generated `pubsub.yaml`, `statestore.yaml`, and numbered
subscription YAML remain fixture-owned temporary files; exact bytes are
copied into each scenario packet before disposal. Both host graphs,
registration/options, AppHost, credentials and deployment assets retain
their current behavior.

The proxy forwards opaque TCP bytes from a random loopback port to the
existing loopback Redis service. It interprets no provider protocol, retains
no wire bytes, and touches only connections routed through this fixture.
Blocking severs those connections and rejects reconnects; restoration does
not restart either host or sidecar. The shared Redis, placement, scheduler,
and unrelated workloads remain running.

The catalog helper writes additional exact-topic subscriptions in that same
temporary directory. Its rollout reloads both owned sidecars and retains
the running hosts and SignalR connections. This is operator-managed catalog
enrollment, with sidecar restart and permitted replay. It does not implement
automatic arbitrary tenant discovery or ship a production catalog mechanism.

The additive real-detail slice also selects `ProjectionResponse.cs`,
`ProjectionState.cs`, the actor's notification/publication partials, both
legacy response conversion paths in `ProjectionUpdateOrchestrator`, the
sample `CounterProjectionHandler`, and focused compatibility tests. Optional
`NotificationGroupScope` and `NotificationMetadata` preserve existing positional
constructors/deconstruction. Null members are omitted from JSON; new actor
DataContract members omit defaults and follow the existing members. Explicit
old/new reader vectors verify unchanged legacy bytes. Metadata is copied and
clipped with Contracts-owned serialized/count ceilings before actor persistence
or staging. Invalid scope is rejected before writes; blank scope without
metadata stays signal-only. Trusted tenant/projection routing and authoritative
`StateBytes` fingerprints remain unchanged.

Selected actor state retains these non-authoritative hints through staged
publication. After a successful actor commit, the original tenant-wide signal
is preserved and optional scoped detail uses the existing notifier overload.
Completed replay emits neither again. The sample opts in with static scope
`counter-status` and opaque `change-kind` metadata, independent of aggregate-ID
length. Named v2 dispatch outcomes remain unchanged; the selected detail seam
is the optional legacy full-replay `ProjectionResponse` path. No provider or
production profile is selected by this implementation choice.

## Scenario meaning

The network-fault scenario first verifies cross-host delivery and reads
synthetic version/ETag `1` through Dapr. It then removes both sidecars'
provider route. Publication and each sidecar's logical read must fail or
cancel under a two-second caller budget with a five-second outer test bound.
A failed publication remains unknown; no absence, completion, freshness,
or permission for a second effect is inferred. Both clients observe no detail
in the bounded fault window. After restoration both sidecars re-read the
unchanged persisted version/ETag, an explicit write changes it to `2`, and
a previously unused recovery topic supplies fresh signal/detail controls.
Tenant-B clients receive neither tenant-A signal nor detail. Existing
authorized connections remain connected. Final readback confirms hints did
not mutate persisted state.

The catalog scenario starts with tenant A's exact topic and two local
clients per tenant. Its positive A control reaches both replicas; a new
tenant B's concrete topic fails beside that control under the shipped
wildcard. The before-rollout snapshot retains the catalog and both runtime
subscription lists. The fixture then adds B's exact topic and a separate
never-published fresh control topic. Both sidecars restart and report both
new subscriptions; host and client identities remain unchanged. Fresh
signal/detail delivery uses that new control topic, excluding replay of the
pre-rollout B message. Both Dapr readbacks agree on synthetic version/ETag
`2`; A clients with equivalent projection/group names receive no B data.
This qualifies the bounded fixture rollout alternative, not unrestricted
dynamic discovery or a shipped production transition.

The full runtime class also retains the existing replica restart/rejoin,
serialized metadata rejection and exact-boundary control, tenant denials,
signed provenance denials, duplicate/reorder reconciliation, and sidecar
outage cases. No signal establishes projection-confirmed success.

That four-scenario run precedes the additive detail slice and retains its own
exact sources and binaries; it is not relabeled as a run on final detail
binaries. Final-source real projection composition separately submits
`IncrementCounter`, requires both signal and scoped detail on both real hosts,
then verifies projection-backed queries return count `1` and the same strong
ETag. Tenant B receives neither signal nor detail. The ordinary Direct
regression verifies compatible command/query/304 behavior with sample detail
enabled.

## Verification

The required baseline used `EnableKeycloak=false aspire run --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach
--non-interactive`, `aspire wait eventstore --timeout 45` and `aspire describe
--format Json` with the same explicit AppHost. The inspected resources were
Healthy. `aspire stop` succeeded before edits. Resource environment values
and dashboard access credentials are not retained in this packet.

Each [command receipt](runs-2026-10-20-network/) binds exact argument arrays,
UTC interval, exit code, redacted output, source/spec snapshots, and assembly
hashes before and after the command. Scenario manifests additionally retain
component bytes, sidecar metadata, process/connection identities, denial and
readback observations. These are consistency bindings, not owner attestations.

<!-- RESULTS -->
| Check | Exact command receipt | Result / UTC interval |
| --- | --- | --- |
| Earlier four-scenario runtime class | [final-distribution-tests.json](runs-2026-10-20-network/final-distribution-tests.json) | 4 passed, 0 errors, 0 failed; 507.089 s; `2026-10-20T07:31:53.746802+00:00`–`2026-10-20T07:40:21.052880+00:00` |
| Final Debug graph | [final-detail-graph-build.json](runs-2026-10-20-network/final-detail-graph-build.json) | Build passed, 0 warnings/errors; `2026-10-20T07:47:14.996900+00:00`–`2026-10-20T07:48:36.730710+00:00` |
| Final contract compatibility | [final-detail-contract-tests.json](runs-2026-10-20-network/final-detail-contract-tests.json) | 18 passed, 0 errors, 0 failed; 0.188 s; `2026-10-20T07:51:25.482500+00:00`–`2026-10-20T07:51:25.897552+00:00` |
| Final actor/state/orchestrator | [final-detail-server-tests.json](runs-2026-10-20-network/final-detail-server-tests.json) | 152 passed, 0 errors, 0 failed; 3.382 s; `2026-10-20T07:51:26.021490+00:00`–`2026-10-20T07:51:29.776852+00:00` |
| Final sample | [final-detail-sample-tests.json](runs-2026-10-20-network/final-detail-sample-tests.json) | 16 passed, 0 errors, 0 failed; 0.125 s; `2026-10-20T07:51:27.165558+00:00`–`2026-10-20T07:51:27.494196+00:00` |
| Final real projection signal/detail | [final-real-projection-detail.json](runs-2026-10-20-network/final-real-projection-detail.json) | 1 passed, 0 errors, 0 failed; 48.716 s; `2026-10-20T07:53:37.535608+00:00`–`2026-10-20T07:54:26.506964+00:00` |
| Final ordinary Direct regression | [final-ordinary-direct-regression.json](runs-2026-10-20-network/final-ordinary-direct-regression.json) | 1 passed, 0 errors, 0 failed; 27.603 s; `2026-10-20T07:54:49.974687+00:00`–`2026-10-20T07:55:17.915486+00:00` |
| Supporting client freshness | [supporting-client-freshness.json](runs-2026-10-20-network/supporting-client-freshness.json) | 102 passed, 0 errors, 0 failed; 0.294 s; `2026-10-20T07:52:15.151787+00:00`–`2026-10-20T07:52:15.747832+00:00` |
| Supporting credential conformance | [supporting-credential-conformance.json](runs-2026-10-20-network/supporting-credential-conformance.json) | 80 passed, 0 errors, 0 failed; 3.533 s; `2026-10-20T07:52:15.873456+00:00`–`2026-10-20T07:52:19.886274+00:00` |
<!-- END RESULTS -->

The preliminary `network-outage` receipt passed 1/1 in `116.163` seconds.
Its code/binaries were stable, but the non-frozen spec notes changed during
that run; it is retained separately rather than substituted for the final
stable-input class run. It observed 29 severed provider connections, a
`DaprException` publish failure in about 69 ms and two cancelled reads in
about two seconds. The later stable-input class supplies its four-scenario outcome.

The first detail graph build passed while whitespace normalization and its
state vector changed; it is superseded. The final detail graph build kept all
non-binary code/spec inputs unchanged. Its `sourcesBefore/After` map also
contains DLL paths extracted from the fixture; their expected build changes
are outputs, not source drift. The verifier uses the explicit four-path build-output classification and requires all other build inputs to stay unchanged. Ignored run-created binary copies were removed after hash checks; receipts retain their binary identities, and source snapshots remain retained.
The first short detail receipts omitted the executed contract/server/sample
test DLLs. The `final-detail-*` test receipts supersede them with each executed
assembly, deps/runtimeconfig and local EventStore dependency hashes. Final
runtime and supporting checks bind these identities too.

External workspace commits and root gitlink changes occurred during this
continuation; this agent did no Git mutation. The
[workspace observation](runs-2026-10-20-network/workspace-observation.json)
records the first movement. Earlier runner code sampled `runStartRevision`
at command completion; in `final-distribution-tests` that field identifies the
observed end revision, not a captured start revision. Source/spec/binary bytes
stayed equal across that run. Later receipts capture start and end revisions.
A revision movement alone is not a source or binary change, and the frozen
baseline remains unchanged.

Existing supporting seams passed independently: 102 client SDK cases cover
freshness classification, lifecycle transport, duplicate/contradictory metadata
falling back to `Unknown`, and unavailable queries. Eighty authentication and
local authority cases cover duplicate credentials, conflicting caller,
human/workload conflict, resource bindings and no downstream work on denial.
These use in-process/fake-response or enrolled local authority fixtures; they
are supporting checks, not two-host outage or production issuer proof. The
counter model contains count only, SignalR exposes hints, and the Admin
projection badge renders operational Running/Paused/Error/Rebuilding. This
fixture has no canonical five-state completion UI flow. A runnable consumer
combining persisted freshness/lifecycle evidence and completion reconciliation
under these faults is still required; no `Current` is synthesized to fill it.

The local verifier is `python3 _bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-20-network/verify-packet.py`. It checks retained bytes, stable test inputs, explicitly classified build outputs and replica identities. Closing observations confirm every retained fixture PID is gone, proxy ports are closed and temporary component directories are absent. The unrelated Story 4.16 AppHost and existing shared services remain running. `git diff --check` and the staged-diff equivalent passed. Post-test handoff notes are retained separately from tested spec bytes. The optional Markdownlint command `./node_modules/.bin/markdownlint-cli2 _bmad-output/implementation-artifacts/evidence/story-2-13/qualification-2026-10-20-network.md` could not run: the local binary is absent (exit 127). No dependency was installed.

## Exact gaps and next actions

| Operation | Finding and locally feasible next action | Owner |
| --- | --- | --- |
| Dynamic tenant topics | `ProjectionNotificationTopicAttribute.Bind` and the deployed subscription select literal `*.*.projection-changed`; the notifier publishes concrete tenant/projection topics. The new test qualifies a coordinated exact-topic catalog reload. A supported production mechanism still needs an authoritative bounded topic inventory, update/recovery/removal protocol, catalog/profile identity, scopes and rollout proof on the selected broker/runtime. | SignalR / Architecture / Platform deployment |
| Real projection-triggered scoped detail | Locally qualified for optional legacy `ProjectionResponse` detail: bounds before actor persistence/staging, old/new wire vectors, committed replay/idempotence vectors and real two-host signal/detail with persisted readback and tenant denials passed. Named v2 dispatch outcomes and production profile qualification are outside this selected seam. | Projection / SignalR / Test |
| Production bound provenance | The issuer client supports enrolled authority requests, but no selected production authority/enrollment is proven for this target. Bind authority, workload/client identity, audience/operation, allowed tenant/topic resources, trust, secret custody and lifetime to the selected profile; verify issuance and negative binding cases before enabling PubSub. | Security / Platform deployment |
| Runtime and broker profile | The inspected cluster's Dapr `1.18.1` and Redis component are distinct from this local `1.18.7` Development profile. Select a compliant exact runtime/broker profile through existing AD-26/Story 3.21 gates and re-run the two-host suite there before claiming production equivalence. | Platform deployment / Story 3.21 |
| Outage, freshness and limits | Connection reset, caller cancellation and Dapr reconciliation are bounded local evidence. Existing client conformance passes separately; a runnable canonical completion/freshness consumer under two-host faults, blackhole/partition, saturation/backpressure, retention/expiry and combined retry profiles remain absent. Story 2.16 retains NFR5 primary evidence. | Operations / UX / Test / Story 2.16 |
| Azure output binding | Comparison-only and outside MVP per the user. Negotiation, authorized membership and service capacity remain unrun; no suitability rejection or cloud provisioning follows. | SignalR / Architecture |
| Atomic transition and rollback | Test-sidecar reload/restoration is not host/package/deployment rollback. After candidate/profile review, bind both host graphs, registration/options, components, AppHost and deployment assets into one reviewed change and prove restoration of the compatible prior path before Redis retirement. | Platform deployment / Test |
| Disposition | Required transport/Architecture/Security/Operations/Test decisions and the approved profile are absent. Keep `accepted_exceptions: []`, story backlog, and the unresolved Story 3.17 inventory/guard handoff. | Named reviewing owners |

The three-candidate comparison in the October 19 packet remains the operation
baseline. This packet advances only the local connection-reset and exact-topic
catalog-rollout rows, optional real projection detail and supporting
client/credential conformance, and applies the self-hosted MVP scope.
It neither rejects Dapr for dynamic tenants nor approves a permanent Redis
exception. Rollback, capacity and production authority remain unproven.
