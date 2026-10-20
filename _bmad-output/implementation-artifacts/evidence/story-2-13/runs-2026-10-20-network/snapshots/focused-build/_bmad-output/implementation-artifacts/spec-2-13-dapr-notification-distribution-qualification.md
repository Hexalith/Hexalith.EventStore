---
title: 'Dapr Notification Distribution Qualification'
type: 'feature'
created: '2026-10-11'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f2b480b68a8bd5369b3cdf550340e0fadfb2332b'
story_id: '2.13'
story_key: '2-13-dapr-notification-distribution-qualification'
context:
  - _bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md
  - _bmad-output/planning-artifacts/architecture.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The SignalR Redis backplane remains a provider dependency without an accepted exception. The Dapr callback proves local broadcast, not delivery to authorized clients on every replica.

**Approach:** Compare Dapr pub/sub fan-out and Azure SignalR output binding with Redis. Prove a design through two-host, two-sidecar projection delivery, then transition both host graphs and deployment assets together. If neither passes, record gaps and seek a scoped exception decision.

## Boundaries & Constraints

**Always:** Preserve Story 2.8's `ProjectionChanged` and additive `ProjectionChangedDetail`, authorized joins and rejoin, metadata bounds (16 entries; 2,048 serialized UTF-8 bytes), and Debug-only logging of values. Signals are hints; persisted projection evidence governs freshness and success. Bind identities, limits, review decisions, UTC tests, and rollback in an immutable packet. Follow AD-8/9/12/26 profile gates; Story 2.16 owns NFR5 primary evidence.

**Never:** Treat a single replica, mock, HTTP acceptance, or notification alone as proof. Assume competing consumers reach every hub or the binding supplies negotiation and membership. Add provider fallback, weaken authorization, expose credentials, forge `Current`, retire Redis before rollback proof, provision external service, or accept an exception without authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Scoped change | Persisted tenant A projection changes; A and B hosts each hold an authorized A client | Both clients receive signal and detail, then read back the same authoritative projection version; tenant B receives none | Missing replica delivery fails qualification |
| Reconnect or duplicate | Client reconnects/rejoins after auth change; repeated or reordered notice | Reauthorize group; re-query persisted state; no duplicate completion or domain mutation | Denied rejoin receives no tenant data |
| Outage | Sidecar, provider, host, or query unavailable | Bounded/cancellable failure, honest `Stale`, `Degraded`, `Unavailable`, or `Unknown`; restore re-queries | No silent fallback or unbounded retry |
| Denial | Anonymous, wrong/conflicting tenant, forged group, expired identity | Reject before group join or downstream work on both hosts | Safe reason code, no private group/payload disclosure |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`, `SignalROptions.cs` — `ConfigureBackplane` chooses Redis; Gateway compiles linked source.
- `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`, `IProjectionChangedClient.cs`, `SignalRProjectionChangedBroadcaster.cs` — groups and signal/detail wire contract.
- `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs`, `DaprProjectionChangeNotifier.cs` — signed Dapr callback and local broadcast; preserve provenance.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` — client/negative-control patterns; no sidecars or persisted change. `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` — two-process sidecar blueprint; shadow host is not proof.
- `src/Hexalith.EventStore.AppHost/Program.cs`, `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`, `deploy/dapr/subscription-projection-changed.yaml` — one-host profile and subscription assets.
- `src/Hexalith.EventStore/Hexalith.EventStore.csproj`, `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj` — both carry the Redis package. `docs/architecture/dapr-infrastructure-exceptions.yaml` has no accepted exception.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-2-13/qualification.md` — compare exact Redis, Dapr pub/sub and `bindings.azure.signalr` profiles across negotiation, targeting, fan-out, ordering, recovery, auth, credentials, cost, and rollback; mark unsupported/unrun rows.
- [ ] `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionRuntimeProofTests.cs`, `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionFixture.cs` — run two hosts with independent sidecars, shared app identity, per-host clients, projection trigger/readback, signal/detail bounds, and reconnect/duplicate/outage/denial cases; retain logs and component identity.
- [ ] `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`, `SignalROptions.cs`, both host `.csproj` files, `src/Hexalith.EventStore.AppHost/Program.cs`, `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`, and `deploy/dapr/subscription-projection-changed.yaml` — change atomically only after a candidate and profile pass; record any additional selected deployment component path before editing it.
- [ ] `docs/concepts/dapr-infrastructure-boundary.md`, `docs/architecture/dapr-infrastructure-exceptions.yaml`, `_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — record disposition and 3.17 handoff; accept an exception only after owner and Security/Operations/Test approval. Otherwise retain backlog.

**Acceptance Criteria:**
- Given both candidates and Redis, when Architecture/Security/Test review the matrix, then every operation has exact-profile evidence or a named gap, alternative, and owner.
- Given two hosts with independent sidecars and authorized clients, when a projection changes, then both hosts' relevant clients receive scoped signals, Dapr readback proves the persisted version, and denied clients receive none.
- Given reconnect, duplicate, outage, and rollback scenarios, when they run under the chosen profile, then bounded failure and honest freshness survive and the prior compatible path can be restored.
- Given completion is requested, when reviewers inspect the packet, then it binds identities, commands, results, UTC interval, scenarios, limits, and an approved disposition.

## Implementation Notes

- The [qualification packet](evidence/story-2-13/qualification.md) records a passing test-only two-host, two-sidecar exact-topic Dapr proof, a real projection-triggered signal with projection-backed readback, and a scoped Redis exception proposal that has **not** been accepted.
- The shipped Redis Streams wildcard subscription did not deliver concrete tenant topics. Dynamic tenant subscription, real projection-triggered detail, full outage/freshness and rollback behavior, and owner approvals remain open. No production profile or backplane dependency changed; the original story remains backlog.
- The ordinary Aspire regression test body passed, but collection teardown failed while restoring the shared writer-protocol marker. The packet records the exact result and unresolved lock interference. The local inotify limit also remains elevated while unrelated processes hold more watches than the original limit.
- The [2026-10-19 continuation](evidence/story-2-13/qualification-2026-10-19.md) records a separate run-start revision, current runtime identities, expanded two-host denial/bounds/reconciliation scenarios, retained component bytes and redacted logs, and the unresolved serialized-versus-key/value metadata ceiling. It does not change the frozen baseline, accept an exception, or authorize the atomic runtime transition. Historical teardown and machine-state observations above remain historical; current results and cleanup are bound by the continuation.
- The [2026-10-20 continuation](evidence/story-2-13/qualification-2026-10-20.md) enforces the user-confirmed serialized metadata contract and records passing final-source tests: 93 focused cases, both two-host Dapr scenarios, real-projection composition, ordinary Direct regression, and the retained Redis baseline. Replica restart now proves fresh authorization/rejoin, persisted Dapr reconciliation, and per-host rejection of three oversized serialized vectors beside an exact-boundary control. Exact tested spec/profile bytes, superseded failures, commands and assembly identities are retained separately. Dynamic topics, real projection detail, production profile, broader outage/freshness, transition/rollback and owner disposition gates remain open; the original story remains backlog.
- The [network-fault continuation](evidence/story-2-13/qualification-2026-10-20-network.md) isolates both sidecars' Redis connections through a disposable loopback proxy. The selected test paths are `SignalRDaprDistributionFixture.cs`, `SignalRDaprDistributionRuntimeProofTests.cs`, and the new `SignalRDaprNetworkFaultProxy.cs`; generated component files remain fixture-only. The user identified self-hosted Kubernetes at `192.168.1.30` as the MVP production target and excluded Azure/AWS from MVP. Cloud binding behavior remains a comparison-only unrun candidate, not a service-provisioning prerequisite. Read-only cluster observations and local network-fault qualification do not select or authorize a production notification profile.

## Spec Change Log

- 2026-10-20 — The user resolved the metadata accounting conflict: "Enforce the serialized-byte limit in the approved story spec." The frozen intent is unchanged. Compact default System.Text.Json metadata sizing now includes punctuation and escaping across producer, receiver and broadcaster, with Contracts-owned ceilings. The [October 20 continuation](evidence/story-2-13/qualification-2026-10-20.md) separates the observed pre-fix gap from final-source qualification and replica restart evidence. Profile/owner gates and the retained Redis disposition remain unresolved.
- 2026-10-20 — The user clarified: "production is on local 192.168.1.30 kubernetes server. Azure or AWS is not MVP." This narrows the target; it approves no issuer, broker, runtime upgrade, cluster mutation, or production transition. The frozen intent and required candidate comparison are unchanged.

## Review Triage Log

## Verification

**Commands:**
- `aspire run` then `aspire describe` — establish baseline resource state before code edits.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Debug` — compile the focused proof project; invoke the built xUnit v3 executable with `-class` for focused runtime proof.
- `dotnet build Hexalith.EventStore.slnx -c Debug -m:1` — verify both host graphs; run relevant test projects individually and record exact commands/results in the packet.
- `git diff --check` — no whitespace errors.
