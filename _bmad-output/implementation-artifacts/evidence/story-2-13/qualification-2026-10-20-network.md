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
Final verification results are being collected.
<!-- END RESULTS -->

The preliminary `network-outage` receipt passed 1/1 in `116.163` seconds.
Its code/binaries were stable, but the non-frozen spec notes changed during
that run; it is retained separately rather than substituted for the final
stable-input class run. It observed 29 severed provider connections, a
`DaprException` publish failure in about 69 ms and two cancelled reads in
about two seconds. The full final class supplies the final-source outcome.

## Exact gaps and next actions

| Operation | Finding and locally feasible next action | Owner |
| --- | --- | --- |
| Dynamic tenant topics | `ProjectionNotificationTopicAttribute.Bind` and the deployed subscription select literal `*.*.projection-changed`; the notifier publishes concrete tenant/projection topics. The new test qualifies a coordinated exact-topic catalog reload. A supported production mechanism still needs an authoritative bounded topic inventory, update/recovery/removal protocol, catalog/profile identity, scopes and rollout proof on the selected broker/runtime. | SignalR / Architecture / Platform deployment |
| Real projection-triggered scoped detail | `ProjectionResponse` carries only type/state; actor-owned `ProjectionState` adds tenant and opaque bytes. `EventReplayProjectionActor.UpdateProjectionAsync`, promotion and replay publication call only the signal overload after persistence. No real post-persistence path supplies group scope or bounded detail. Select an owner-defined metadata/scope source and compatibility vectors, propagate it through dispatch and actor persistence/publication, then extend `SignalRDaprProjectionDispatchProofTests` to require detail on both hosts with persisted query readback and tenant denials. Publishing synthetic detail after a real signal would not close this gate. | Projection / SignalR / Architecture / Test |
| Production bound provenance | The issuer client supports enrolled authority requests, but no selected production authority/enrollment is proven for this target. Bind authority, workload/client identity, audience/operation, allowed tenant/topic resources, trust, secret custody and lifetime to the selected profile; verify issuance and negative binding cases before enabling PubSub. | Security / Platform deployment |
| Runtime and broker profile | The inspected cluster's Dapr `1.18.1` and Redis component are distinct from this local `1.18.7` Development profile. Select a compliant exact runtime/broker profile through existing AD-26/Story 3.21 gates and re-run the two-host suite there before claiming production equivalence. | Platform deployment / Story 3.21 |
| Outage, freshness and limits | Connection reset, caller cancellation and Dapr reconciliation are bounded local evidence. Blackhole/partition, saturation/backpressure, retention/expiry, combined retries and canonical `Stale`/`Degraded`/`Unavailable`/`Unknown` UI states remain unproven. Use an isolated fault and canonical client profile. Story 2.16 retains NFR5 primary evidence. | Operations / UX / Test / Story 2.16 |
| Azure output binding | Comparison-only and outside MVP per the user. Negotiation, authorized membership and service capacity remain unrun; no suitability rejection or cloud provisioning follows. | SignalR / Architecture |
| Atomic transition and rollback | Test-sidecar reload/restoration is not host/package/deployment rollback. After candidate/profile review, bind both host graphs, registration/options, components, AppHost and deployment assets into one reviewed change and prove restoration of the compatible prior path before Redis retirement. | Platform deployment / Test |
| Disposition | Required transport/Architecture/Security/Operations/Test decisions and the approved profile are absent. Keep `accepted_exceptions: []`, story backlog, and the unresolved Story 3.17 inventory/guard handoff. | Named reviewing owners |

The three-candidate comparison in the October 19 packet remains the operation
baseline. This packet advances only the local connection-reset and exact-topic
catalog-rollout rows, and applies the user-confirmed self-hosted MVP scope.
It neither rejects Dapr for dynamic tenants nor approves a permanent Redis
exception. Rollback, capacity and production authority remain unproven.
