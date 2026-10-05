---
title: Dapr Notification Distribution Qualification
story_id: '2.13'
story_key: 2-13-dapr-notification-distribution-qualification
epic: '2'
status: backlog
created: 2026-10-05
updated: 2026-10-05
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
