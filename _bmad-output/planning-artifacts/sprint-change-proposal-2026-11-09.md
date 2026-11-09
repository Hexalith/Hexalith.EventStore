---
title: 'Dapr abstraction and Hexalith.Platform infrastructure ownership'
date: '2026-11-09'
status: 'applied'
mode: 'incremental'
approval: 'Owner approved P1-P5 and complete planning application on 2026-11-09; applied'
baseline_commit: '9bf5b587ba294a881996234f59dede8fb31dbddb'
verification_commit: 'dcdeab412428e5d4f74f834c4b730e016262ec7b'
change_scope: 'moderate; existing backlog ownership and acceptance correction'
---

# Sprint Change Proposal — Dapr And Hexalith.Platform Ownership

## 1. Issue Summary

The owner clarified during Story 4.16's Story 3.21 handoff check that EventStore must remain database agnostic through Dapr and infrastructure is managed by Hexalith.Platform. The triggering mismatch is responsibility assignment: the plans assign local concrete Dapr/component qualification to EventStore, while some shipped test support and a Platform integration test depend directly on Redis. Calling a Dapr generic state API also does not establish actor abstraction when application code constructs Dapr's private actor-state keys.

This is a correction to responsibility and abstraction boundaries. It does not withdraw the mandatory revision/create-once/whole-batch/authorized-erasure requirements, select a provider, or resolve the source-established missing Dapr capability. The owner reviewed the proposals incrementally, then replied “approve” to P5 and application of the complete P1–P5 documentation/backlog correction on 2026-11-09. The specified planning edits are applied; code repairs and Platform/Builds execution remain future work under the recorded stories and decisions.

## 2. Impact Analysis

### Code And Integration Findings

| Finding | Observed evidence | Disposition / owner |
| --- | --- | --- |
| Core event persistence uses Dapr actor state | [EventPersister](../../src/Hexalith.EventStore.Server/Events/EventPersister.cs) takes `IActorStateManager`; [AggregateActor](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs) owns saves. Logical actor read APIs already exist in [IAggregateActor](../../src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs). | Preserve these seams and actor ownership. This is supporting source evidence, not full runtime conformance. |
| Platform integration is declared and consumed | [Module declaration](../../module/hexalith.module-manifest.v2.json) requests logical state/pubsub/configuration capabilities and declares `platform-source`/`platform-package`; [CI](../../.github/workflows/ci.yml) selects `platform-package`. [PlatformEnvironmentFixture](../../tests/Hexalith.EventStore.Platform.IntegrationTests/PlatformEnvironmentFixture.cs) consumes a runner descriptor without starting services. Platform's existing `apphost.cs` and `PlatformDaprBindings` own the concrete composition. | Preserve descriptor consumption and the shared Platform host. No new host, runner, descriptor system or Platform changes in this proposal. |
| The module persistence smoke test bypasses Dapr | [GatewayPersistenceTests](../../tests/Hexalith.EventStore.Platform.IntegrationTests/GatewayPersistenceTests.cs) checks `/ready`, then performs `ConnectionMultiplexer` string writes/reads. It does not submit an EventStore operation whose persisted result is read through Dapr. The fixture requires a resource named `redis`. | Correct in EventStore through a later exact test proposal. Direct Redis reachability is not evidence for the EventStore persistence contract. Platform owns backend diagnostics. |
| A reusable package depends on Redis physical storage | [TestEnvironmentStreamReader](../../src/Hexalith.EventStore.Testing.Integration/TestEnvironmentStreamReader.cs) opens Redis, scans `AggregateActor` key suffixes and reads hash field `data`; its [project](../../src/Hexalith.EventStore.Testing.Integration/Hexalith.EventStore.Testing.Integration.csproj) references `StackExchange.Redis`. | Replace logical acceptance readback with an owning-actor/Dapr route. Story 3.18 must assess the public helper's compatibility before changing/removing it. Provider diagnostics cannot become a module package abstraction. |
| Admin has a private actor-key fallback | [DaprInfrastructureQueryService](../../src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs), `ComposeActorStateKey` / `ReadActorStateKeyAsync`, constructs `appId||actorType||actorId||stateKey` and calls generic `GetStateAsync` if the owner endpoint is absent. | Existing AD-1 violation remains owned by 3.17/5.6 and the Admin contract owners. Use owning-actor readback or the existing unavailable behavior; preserve authorization, redaction and UX evidence semantics. |
| Concrete infrastructure lifecycle remains inside qualification fixtures/tools | [Oq8PostgresqlFixture](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs) launches PostgreSQL and uses `docker exec ... psql`; [broker qualifier](../../tools/qualify-dapr-brokers.py) owns local broker/container lifecycle. | Plan a Platform-owned execution handoff. Preserve historical results, source identities and existing lanes until an approved replacement passes; no deletion, move, rerun or migration is authorized here. |
| Provider references require exact classification | The [exception register](../../docs/architecture/dapr-infrastructure-exceptions.yaml) contains the scoped SignalR Redis and Azure key-operation decisions. The Azure companion is non-packable; the selected Dapr/Kubernetes successor remains separately gated. | Preserve both decisions and their limits. This correction accepts, expands and revokes no exception. Redis test helpers are not covered by the SignalR exception. |
| The enforcement inventory is not delivered | `docs/architecture/dapr-infrastructure-inventory.yaml` is absent; [Story 3.17](../implementation-artifacts/3-17-dapr-boundary-inventory-and-enforcement.md) remains backlog. Its spec, epic and architecture contain stale exception summaries that disagree with the current register. | Reconcile summaries and finish the existing evaluated-graph guard work under 3.17. A source/package scan supplies no whole-module compliance verdict. |

### Epic And Artifact Impact

- Epics 1 and 2: preserve domain-centric SDK seams and public transport behavior. The scoped 2.13 exception remains; no domain host or persistence authority is added.
- Epic 3: 3.17 keeps module operation/dependency inventory and enforcement; 3.18 classifies public/configuration compatibility; 3.21 must consume Platform-executed concrete-profile qualification while EventStore supplies Dapr semantics and test vectors. 3.19 continues to consume exact accepted profile results, without production authority from a source screen.
- Epic 4: 4.16 keeps its complete backend-neutral commit contract and logical Dapr prevention oracles. Concrete profile setup, provider faults and backend diagnostics go to Platform. Missing mandatory capability keeps G-APPEND FAIL/BLOCKED.
- Epic 5: 5.6 keeps module declarations, configuration consumption and reader/default code changes; 5.7/5.8 compare these with Platform's rendered/loaded composition, isolation and migration/rollback results. 5.9 reconciles ownership and authoritative tool/runtime instructions without choosing new pins.
- Epic 6: preserve existing Dapr-only event-evolution rules; no application SQL or physical-store acceptance proof is introduced.
- Epic 7: 7.6/7.22 retain module secret/restore requirements and acceptance obligations; Platform owns provisioning, custody infrastructure and concrete restore execution. Admin still reports safe unavailable/unknown evidence where owner readback cannot be obtained.
- Epic 8: preserve the selected Dapr/Kubernetes successor and the deferred Azure exception. This correction changes neither cryptographic authority nor release/migration gates.
- Epic 9: preserve all existing approval, assurance and readiness gates; do not recreate deleted evidence machinery.
- PRD §8.4, architecture AD-1/AD-9/AD-26/AD-34, corresponding epic/spec ownership text, the boundary guide and operator documentation need coordinated updates. AD-26's ratification-subject impact must be explicitly assessed if its protected text changes.
- UX: no screen, route, navigation or lifecycle-state redesign. The canonical `ux.md` handoff retains typed evidence, authorization, redaction and Unknown/Unavailable behavior. Any Admin implementation must preserve its detailed UX contracts.

## 3. Recommended Approach

Use direct adjustment within existing stories. Clarify the policy first, align architecture and ownership next, then specify the concrete module test/readback repairs and Platform handoffs. This is a moderate backlog/acceptance correction; no new epic or FR/NFR identity is needed.

Rollback is not recommended: core Dapr seams and Platform declaration/descriptor integration should be retained, and historical completed qualification observations remain valid only for their original scope. An MVP reduction is not recommended: moving infrastructure ownership does not remove the missing mandatory persistence guarantee.

Effort: low for planning reconciliation, medium for module test/helper and Admin corrections, and presently unestimated for Platform capability delivery. Risk: medium for public helper/configuration compatibility and cross-repository sequencing; the unresolved Dapr primitive remains a blocking technical risk. No delivery date is promised without Platform's accepted execution scope. Preserve all lifecycle statuses, pins, historical observations, rejected guarantees and production gates.

## 4. Detailed Change Proposals — Incremental Review

### P1 — PRD §8.4 Ownership Sentence

Status: **APPLIED**. The owner approved P1 individually and then approved the complete P1–P5 planning application on 2026-11-09. The exact OLD/NEW text below is retained and applied to its stated target.

Target: [prd.md](prd.md#84-dapr-infrastructure-boundary), the ownership sentence in the fourth paragraph. Preserve every other sentence in that paragraph and the rest of §8.4.

**OLD:**

> Hexalith.Platform owns shared host composition and hosted self-managed Kubernetes rehearsal; EventStore owns reusable runtime semantics and Dapr operation/component qualification, and Hexalith.Builds owns shared tool/runtime/catalog pins.

**NEW:**

> EventStore owns backend-neutral runtime semantics, logical Dapr capability requirements, module declarations and reusable contract tests that exercise supported Dapr APIs and owning actor boundaries. Hexalith.Platform owns shared infrastructure composition and lifecycle, concrete provider/component configuration, and execution of qualification against each supported deployment profile; it supplies run-owned endpoints and exact loaded runtime/component identities to EventStore's contract tests. EventStore consumes these qualification results and retains responsibility for its application assertions; Platform infrastructure results alone cannot establish EventStore conformance. Hexalith.Builds owns shared tool/runtime/catalog pins.

**Rationale:** Make database independence and infrastructure ownership explicit while retaining EventStore's responsibility to state and verify its required guarantees. A module may reject an incapable Dapr profile without implementing or provisioning the backing provider. MVP scope, actor ownership, accepted exceptions and production/readiness gates remain unchanged.

### P2 — Architecture And Qualification Ownership

Status: **APPLIED**. The owner approved P2 individually and then approved the complete P1–P5 planning application on 2026-11-09. The exact edits below are retained and applied to their stated targets.

#### P2.A — Architecture AD-9 Ownership Clause

Target: [architecture.md](architecture.md), AD-9 Rule; replace this exact clause once, preserving the rest of the Rule.

**OLD:**

> EventStore owns reusable capability and module contracts, Platform owns the shared composition and hosted rendering, and Builds owns catalog/tool pins.

**NEW:**

> EventStore owns backend-neutral Dapr capability requirements, module declarations, reusable contract tests and application assertions; Hexalith.Platform owns infrastructure composition and lifecycle, concrete provider/component configuration, hosted rendering and execution of profile-specific qualification; Builds owns catalog/tool pins. Platform supplies run-owned endpoints and loaded runtime/component identities to the module tests. EventStore evaluates those results against its required semantics, and missing guarantees remain unresolved. Existing repository AppHosts are retained only as bounded historical/test fixtures until an approved Platform replacement is validated; they acquire no new shared-infrastructure responsibility.

Rationale: align shared-host, local/CI qualification and contract-test ownership with P1 without removing the existing annotation/YAML/loaded-sidecar parity requirement. AD-1 already requires Dapr and owning-actor access; it needs no weaker replacement.

Diagram impact: retain the existing architecture diagram's `IActorStateManager`, Dapr state and Dapr pub/sub edges. The logical runtime graph is unchanged; AD-9/AD-26 carry the corrected composition and qualification ownership. No provider connection or new infrastructure service edge is added to the module diagram.

#### P2.B — Architecture AD-26 Shared-Host Proof Ownership

Target: [architecture.md](architecture.md), the complete Shared-host proof ownership paragraph inside AD-26.

**OLD:**

> **Shared-host proof ownership (approved planning reconciliation, 2026-10-20).** Shared hosted composition and disposable Kubernetes rehearsal are Platform-owned. Story 3.21 supplies EventStore's qualified Dapr/runtime/broker inputs; Stories 5.7 and 7.6 supply module component and secret contracts; Builds supplies qualified catalog/tool and shared evidence-codec identities. The agreed profile template/binding mechanism must produce the complete canonical resolved inventory at `deploy/dapr/production-profile.yaml`, consumed by Story 3.19 and bound by Platform's composed release/deployment records. Rehearsal or candidate publication never selects a broker, replaces per-role ratification or supplies production authority.

**NEW:**

> **Shared-host proof ownership (2026-11-09 course correction).** Hexalith.Platform owns concrete infrastructure composition, provisioning, lifecycle, provider/component configuration and execution of qualification against each supported deployment profile, including disposable Kubernetes rehearsal and restore. EventStore supplies backend-neutral Dapr requirements, module declarations and reusable contract tests, and retains responsibility for application-level assertions through supported Dapr APIs and owning actor boundaries. Story 3.21 consumes Platform's exact runtime/component/broker observations and evaluates them against those requirements; Stories 5.7 and 7.6 supply module binding and secret-consumption contracts, and Builds supplies qualified catalog/tool and runner identities. Platform supplies run-owned endpoints and exact loaded identities to module tests, with provider diagnostics kept separate from logical Dapr acceptance readback. The existing profile/binding mechanism must produce the complete resolved inventory at `deploy/dapr/production-profile.yaml`, consumed by Story 3.19 and bound by Platform's composed release/deployment records. Infrastructure results, rehearsal or candidate publication alone never satisfy EventStore's contract, select a broker, replace per-role ratification or supply production authority. An unsupported mandatory persistence guarantee remains blocked.

Rationale: assign concrete qualification to its infrastructure owner while retaining exact production-profile binding, logical proof, the supported-capability blocker and owner ratification. The proposed PostgreSQL v1 production target and all other AD-26 requirements remain unchanged; it remains a Platform profile candidate rather than a universal module dependency.

#### P2.C — Architecture AD-34 Qualification Ownership

Target: [architecture.md](architecture.md), the final sentence of AD-34's Rule; preserve that sentence and append the exact ownership text.

**OLD:**

> envelope-falsification writers are fault-injection rows confined to exact test paths and never packable.

**NEW:**

> envelope-falsification writers are fault-injection rows confined to exact test paths and never packable. EventStore's Story 3.17 owns module operation/dependency coverage, required Dapr guarantees and consumption of exact qualification results. Hexalith.Platform owns concrete infrastructure setup, provider/component configuration, fault execution and provider diagnostics for profile qualification; EventStore supplies reusable Dapr-path assertions and logical acceptance readback. Missing Platform observations, an unqualified Dapr operation or unresolved module coverage prevents conformance. This assignment does not create an exception or a blanket test-tooling permission.

Rationale: preserve operation-level evidence and graph coverage while separating module enforcement from infrastructure execution. The stale exception summary is addressed separately in P5, without changing existing accepted decisions.

#### P2.D — Boundary Guide Qualification Paragraph

Target: [dapr-infrastructure-boundary.md](../../docs/concepts/dapr-infrastructure-boundary.md), Required Boundary, the paragraph beginning Backend selection.

**OLD:**

> Backend selection is expressed through component configuration. A change still requires profile-specific tests and data migration where necessary. Qualify ETags, transaction scope, TTL, ordering, cancellation, retries, failure classification, identity, and isolation as applicable to the operation. Sharing a physical backend does not create transactions across actors, state components, pub/sub, or external systems.

**NEW:**

> Hexalith.Platform selects and configures concrete backends through Dapr components within the approved deployment profile and owns infrastructure lifecycle, provider faults, diagnostics and profile-specific qualification execution. EventStore declares required Dapr capabilities, supplies reusable contract tests and application assertions, and consumes Platform-supplied run-owned endpoints and exact loaded identities. Acceptance writes and reads traverse supported Dapr APIs and owning actor boundaries. A profile change still requires compatibility tests and separately authorized data migration where necessary. Qualify ETags, transaction scope, TTL, ordering, cancellation, retries, failure classification, identity, and isolation as applicable to the operation. Missing guarantees remain unresolved; sharing a physical backend does not create transactions across actors, state components, pub/sub, or external systems. Existing repository infrastructure fixtures are retained until an approved Platform replacement is validated and do not authorize new module-owned infrastructure or provider clients.

Rationale: state the same boundary in the contributor-facing guide, with no component-name portability assumption or weakening of the existing exception policy.

#### P2.E — Story 3.17 Scope And Capability Handoff

Target: [3-17-dapr-boundary-inventory-and-enforcement.md](../implementation-artifacts/3-17-dapr-boundary-inventory-and-enforcement.md), the first sentence of Scope And Dependencies.

**OLD:**

> This backlog specification owns the final operation/dependency inventory and deterministic guard in the existing Contracts architecture/packaging test lane.

**NEW:**

> This backlog specification owns EventStore's module operation/dependency inventory, required Dapr capabilities, consumption of exact Platform qualification results and deterministic guard in the existing Contracts architecture/packaging test lane. Hexalith.Platform supplies concrete profile identities, value-free component/target/principal bindings and infrastructure qualification observations; this story does not provision or qualify a provider through a module-owned infrastructure implementation.

Target: the same spec's Approved 4.16 qualification handoff; replace this exact clause once.

**OLD:**

> 3.21 supplies capable runtime/component inputs;

**NEW:**

> 3.21 evaluates Platform-qualified runtime/component inputs against EventStore's required capabilities;

Rationale: keep missing/static/dynamic graph coverage and raw-call inventories in the module; neither an absent Platform result nor the wording “supplies capable” may invent a delivered capability. Existing acceptance and backlog status remain.

#### P2.F — Story 3.21 Frozen Ownership Amendment

Target: [spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md](../implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md), the exact two ownership sentences inside the frozen Approach. Adoption of the complete approved proposal is the explicit owner amendment for these sentences only; retain every other frozen byte, original identity and historical observation.

**OLD:**

> EventStore owns local Dapr/component qualification and the guarantee/failure handoff. Hexalith.Platform owns the shared composition, disposable self-hosted Kubernetes rehearsal and later exact deployment/restore bundle; Hexalith.Builds owns catalog, CLI and runner pins.

**NEW:**

> EventStore owns backend-neutral Dapr requirements, reusable contract tests, application assertions and evaluation of Platform qualification results for the guarantee/failure handoff. Hexalith.Platform owns concrete infrastructure composition and lifecycle, provider/component configuration and execution of local/CI and disposable self-hosted Kubernetes qualification, including the later exact deployment/restore bundle; Hexalith.Builds owns catalog, CLI and runner pins.

Rationale: correct the explicit responsibility conflict at its frozen source. Do not reopen the closed 1.19.1 refresh, superseded ES-DIAG, optional broker remediation or any historical run. Future Platform execution needs an accepted scope in its owning repository; this amendment grants no resource mutation, rerun, migration or dependency change.

#### P2.G — Story 3.21 Capability Handoff

Target: the same parent spec, Approved Story 4.16 Capability Handoff, its exact Supply/qualify sentence.

**OLD:**

> Supply/qualify an exact supported SDK/runtime/component capable of mandatory actor revision comparison, create-once event identity and whole-batch rollback, with no same-identity raw/TTL/delete bypass and preserved authorized erasure.

**NEW:**

> Define the module's required Dapr semantics and reusable test vectors, then evaluate Platform-supplied qualification of an exact supported SDK/runtime/component capable of mandatory actor revision comparison, create-once event identity and whole-batch rollback, with no same-identity raw/TTL/delete bypass and preserved authorized erasure. Platform owns concrete environment and provider/fault execution; EventStore owns the logical Dapr application assertions and rejection of an incapable profile.

Rationale: preserve every required guarantee and the private-build/provider-exception stop condition. The handoff is a capability requirement, not permission for EventStore to implement a PostgreSQL adapter.

#### P2.H — Epic Story 3.21 Ownership Clause

Target: [epics.md](epics.md), Story 3.21 Dependencies and ownership; replace the exact two sentences once.

**OLD:**

> EventStore owns local Dapr/component qualification; Platform owns the shared composition, disposable self-hosted Kubernetes rehearsal through its existing Story 4.17/shared-runtime work, and the later exact deployment/restore bundle. Builds owns CLI/runtime/control-plane catalog, embedded tool and runner alignment.

**NEW:**

> EventStore owns backend-neutral Dapr requirements, reusable contract tests, application assertions and evaluation of Platform qualification results; Platform owns concrete infrastructure composition and lifecycle, provider/component configuration and execution of local/CI qualification, disposable self-hosted Kubernetes rehearsal through its existing Story 4.17/shared-runtime work, and the later exact deployment/restore bundle. Builds owns CLI/runtime/control-plane catalog, embedded tool and runner alignment.

Rationale: keep the epic consistent with the amended parent spec, preserving the full broker denominator, failure predicates, current status and production authority gates.

#### P2.I — Existing Story Ownership Clarification

Target: [epics.md](epics.md), Stories **3.17, 5.6, 5.7, 5.8, 5.9, 7.6 and 7.22**. Insert the same paragraph immediately after each story's complete Architecture constraints paragraph; preserve its existing acceptance criteria, story IDs and status. This explicitly refines future execution ownership in those stories; historical observations and completed actions retain their original attribution.

**OLD:**

> No separate module/Platform qualification-ownership clarification follows the Architecture constraints paragraph in these seven stories.

**NEW:**

> **Execution ownership (2026-11-09 course correction).** EventStore owns this story's module semantics, logical Dapr capability requirements, declarations, code/configuration consumption and application assertions. Hexalith.Platform owns concrete shared infrastructure composition, provisioning/lifecycle, provider/component configuration and execution of profile-specific qualification, fault injection, custody/restore and migration rehearsal as applicable. Module tests consume Platform-supplied run-owned endpoints and exact loaded identities, and acceptance readback uses the supported Dapr/owning-actor boundary. This clarifies earlier execution-ownership wording without completing the story, accepting a missing capability, changing a provider/pin or authorizing migration/deployment; separately governed production execution remains gated.

Rationale: keep each existing story's semantic deliverable and acceptance responsibility while assigning infrastructure execution to Platform. Story 3.18 compatibility, isolation, migration order, secret custody, restore consistency and runtime-minor re-proof obligations all remain. Concrete provider tools belong to the separately scoped Platform qualification work, not new reusable module clients.

**AD-26 subject impact:** P2.B changes the protected ratification subject. The current subject is `b9e899ecd6bb456537358b23cb69a564e09bc2c7b199dd657075900b2d17503b`; the proposed subject is `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16`. Only the proposed Shared-host paragraph changes that subject; P2.A/P2.C and the other edits are outside it. Final adoption requires a coordinated architecture memlog entry and the existing fresh per-role ratification rules; this proposal starts no main-push window, republishes no profile and asserts no new authority. Preserve AD-26's target/status and the entire AD-5 append contract.

### P3 — Backend-Neutral Story 4.16 Contract Tests

Status: **APPLIED**. The owner approved P3 individually and then approved the complete P1–P5 planning application on 2026-11-09. The exact edits below are retained and applied to their stated targets.

#### P3.A — Frozen Approach And Profile Binding

Target: [Story 4.16 spec](../implementation-artifacts/spec-4-16-append-write-once-conformance-through-enforced-envelope.md), the exact fixture-reuse sentence in the frozen Approach. Adoption of the complete proposal explicitly amends only the quoted frozen sentences in P3.A; retain the rest of the frozen intent.

**OLD:**

> Reuse the existing race, lawful-control and multi-process PostgreSQL fixtures.

**NEW:**

> Reuse existing race synchronization, lawful controls and logical Dapr readback tests against Platform-supplied independent actor hosts; Platform owns concrete storage setup, lifecycle and fault execution.

Target: the same spec, the exact profile-binding sentence in frozen Always.

**OLD:**

> Bind provider, topology, runtime, writers, mechanisms and tests; read acceptance state through Dapr.

**NEW:**

> Bind the Platform-supplied profile, concrete runtime/component identities, topology, writers, mechanisms and tests; read acceptance state through the owning Dapr actor boundary.

Rationale: preserve exact-profile proof and the existing actor-state boundary while removing a PostgreSQL fixture as a module implementation dependency. No supported capability is inferred from a generic test interface.

#### P3.B — Commit Contract Serialization Point

Target: the same spec, Approved Commit Contract item 3, its final two sentences. Preserve every preceding create-once, current-ETag, TTL and late-collision rollback condition.

**OLD:**

> Native uniqueness/conditional mutation and affected-row validation provide the serialization point. PostgreSQL's existing transaction boundary is the initial qualification target, with provider work confined to its Dapr component implementation.

**NEW:**

> The qualified Dapr-owned primitive supplies the storage serialization point for the revision condition, create-once identity and entire actor batch. Platform qualifies that atomic behavior for each declared profile; provider-specific implementation stays inside the supported Dapr component. EventStore introduces no database-specific persistence, provider client, SQL/schema, credential or private actor-key access.

Rationale: require the same atomic storage behavior at the Dapr boundary without prescribing SQL row mechanics to EventStore. The complete approved commit contract still has seven items; items 1, 2 and 4–7 are unchanged.

#### P3.C — Qualification And Test Tasks

Target: the same spec, Capability qualification and subsequent delivery, the exact unchecked qualification task.

**OLD:**

> - [ ] With 3.17/3.21, qualify exact supported SDK/runtime/component semantics, every reachable raw write/erase path and the approved lifecycle-capability verifier trust/custody boundary. Preserve unresolved capability results; select no private build implicitly.

**NEW:**

> - [ ] With 3.17/3.21, define Dapr contract vectors and evaluate Platform-executed qualification of exact supported SDK/runtime/component semantics, every reachable raw write/erase path and the approved lifecycle-capability verifier trust/custody boundary. Preserve unresolved capability results; select no private build implicitly.

Target: the same task section, the exact unchecked fixture-extension task.

**OLD:**

> - [ ] Extend existing LiveSidecar.Tests and Server.Tests lanes for AC1–AC11 using AppendDurabilityRaceControl/Session, lawful actor and snapshot tests, infrastructure-failure/erasure tests and Oq8PostgresqlFixture. Add only bounded missing partition/readback controls; no new fixture stack or evidence machinery.

**NEW:**

> - [ ] Extend existing LiveSidecar.Tests and Server.Tests lanes for AC1–AC11 using the semantic synchronization in AppendDurabilityRaceControl/Session, lawful actor/snapshot and infrastructure-failure/erasure assertions, and Platform-supplied run-owned actor hosts and bounded fault controls. Acceptance writes and reads use supported Dapr/actor APIs. Keep concrete backend lifecycle and physical diagnostics in Platform qualification; do not extend Oq8PostgresqlFixture into a module-owned provider harness or create another fixture stack/evidence framework.

Rationale: retain the semantic test/control reuse while separating provider lifecycle and diagnostics. The actor-integration, cache/unknown handling, migration-prerequisite and final-production-proof tasks remain unchecked and unchanged. These are future tasks after supported capability and an accepted Platform execution scope exist.

#### P3.D — Common Acceptance Oracle

Targets: the same spec and [epics.md](epics.md), Story 4.16's Acceptance Criteria introduction; replace this exact sentence once in each target's active story section.

**OLD:**

> A mock, HTTP status or physical Redis/PostgreSQL snapshot alone cannot pass these criteria.

**NEW:**

> A mock, HTTP status or physical-store snapshot alone cannot pass these criteria. Execute the same logical Dapr contract suite on each explicitly supported Platform profile and bind its loaded identities; a pass on one profile supplies no result for another profile or arbitrary Dapr backends.

Rationale: preserve fresh owning-actor logical readback and exact event/coupled-state comparison. Database-independent code is not a claim that every state component supplies the required guarantees.

#### P3.E — AC4 Late-Collision Rollback

Targets: the same spec and Story 4.16 in epics.md, AC4 Required result and reuse; replace this exact final clause once in each active table. Keep the complete no-persisted-effect list and late-operation collision unchanged.

**OLD:**

> live PostgreSQL end-state is mandatory.

**NEW:**

> fresh logical end-state through the owning Dapr actor boundary on the Platform-supplied qualification profile is mandatory; backend diagnostics cannot satisfy this oracle.

Rationale: prove the same whole-batch atomicity through the module abstraction. The profile still needs real durable infrastructure; a mock or in-memory replacement cannot stand in for its live result.

#### P3.F — AC5 Suspended/Resumed Writer

Targets: the same spec and Story 4.16 in epics.md, AC5 Given/when; replace this exact clause once in each active table.

**OLD:**

> resume old writer while its database path remains usable

**NEW:**

> resume old writer while its Dapr persistence path to the backing store remains usable

Targets: the same AC5 Required result and reuse, its fixture-specific final two sentences.

**OLD:**

> Reuse `Oq8PostgresqlFixture` process lifecycle and race gate. Add only the bounded partition/resume control and logical actor read helper it lacks; killing the old process alone does not prove stale-writer rejection.

**NEW:**

> Reuse the existing race gates and independent-host controls supplied by Platform. Platform owns bounded partition/resume fault execution; module tests use logical actor readback. Attribute rejection to the mandatory revision check; a transport, startup or backing-store outage cannot satisfy this oracle. Killing the old process alone does not prove stale-writer rejection.

Rationale: retain two independent hosts, suspended state basis, live stale-writer persistence access, both commit orders, one winner and no overwritten coupled state. Only infrastructure execution ownership changes.

#### P3.G — AC7 Authorized Erasure

Targets: the same spec and Story 4.16 in epics.md, AC7 Required result and reuse; replace this exact fixture-reuse clause once in each active table. Preserve valid/invalid/expired/cross-aggregate/replay, raw DELETE/delete+insert/TTL and stale recreation cases.

**OLD:**

> Reuse existing trusted-effect lifecycle/erasure tests and the PostgreSQL fixture;

**NEW:**

> Reuse existing trusted-effect lifecycle/erasure assertions against the Platform-supplied profile through the owning Dapr actor boundary;

Rationale: keep governed erasure available, resumable and independently read back, without a provider client or physical-store oracle in the module suite. The approved lifecycle/HMAC custody design and its qualification gate remain unchanged.

#### P3.H — AC10 Falsification Ownership

Targets: the same spec and Story 4.16 in epics.md, AC10 Required result and reuse, the exact final sentence.

**OLD:**

> Reuse existing mutation/fault-injection conventions; no production bypass switch is shipped.

**NEW:**

> Platform executes separately authorized disposable mutation/fault setup under the existing conventions; EventStore supplies the corresponding Dapr assertions. No production bypass switch is shipped, and this criterion selects no private runtime build or provider exception.

Rationale: preserve separate revision/create-once barrier removal beside lawful controls, including metadata-only stale writes. A startup failure proves neither barrier. The current missing capability prevents this from becoming an instruction to construct an unsupported runtime.

#### P3.I — Current Execution And Historical Evidence Boundary

Target: the same spec; insert this new section immediately before `## Approved Commit Contract`, after the existing Capability Dependency And Execution Gate paragraph. Preserve that gate verbatim.

**OLD:**

> No Current Qualification Ownership section exists before the Approved Commit Contract.

**NEW:**

> ## Current Qualification Ownership
>
> EventStore owns the backend-neutral Dapr commit contract, actor integration and application assertions. Hexalith.Platform owns concrete qualification environments, provider/component setup and lifecycle, independent host/process controls, backing-store fault execution and physical diagnostics. Module tests consume Platform-supplied run-owned endpoints and exact loaded identities through the existing descriptor/runner contract. Any missing host/fault input requires a separately accepted extension by that contract's owner or leaves the lane blocked; do not invent a second descriptor, service or module provider client. Run the same logical suite against each declared supported profile, with fresh owning-actor reads after cache clearing/restart. Infrastructure diagnostics alone cannot pass an application assertion, and module test success supplies no untested-profile guarantee.
>
> Historical PostgreSQL/Redis fixtures, source screens, run results and the 2026-11-01 proposal retain their exact inputs, outcomes and original attribution. Their provider-specific fixture references are historical/reuse-study inputs, not current authority to extend module-owned infrastructure. Future execution instructions in those historical records are superseded by this ownership section; no fixture is deleted or moved before an approved Platform replacement is validated. The Dapr 1.19.1/PostgreSQL v1 negative finding remains a failure of that inspected combination. Missing supported mandatory revision/create-once/whole-batch/erasure enforcement keeps G-APPEND FAIL/BLOCKED. Private builds, provider exceptions, migrations and key distribution remain separate decisions.

Rationale: retain the source-established NO-GO and all historical observations without treating a database-specific reproduction or dependency table as a new module implementation plan. This paragraph adds no supported profile and no authority to provision or mutate anything.

#### P3.J — Epic Dependency Handoff

Target: epics.md, Story 4.16 Dependencies; replace this exact sentence once in the active story section.

**OLD:**

> Stories 3.17/3.21 qualify the exact operations and versions, including raw-call bypass and existing erasure-authority enforcement.

**NEW:**

> Stories 3.17/3.21 define the required Dapr operations and versions and evaluate Platform-executed qualification, including raw-call bypass and existing erasure-authority enforcement; Platform supplies the concrete profile environment and EventStore supplies logical application assertions.

Target: the same story, insert the following paragraph after its complete Dependencies paragraph. Existing acceptance rows remain the normative oracles as amended in P3.D–P3.H.

**OLD:**

> No separate execution-ownership paragraph follows Story 4.16's Dependencies paragraph.

**NEW:**

> **Execution ownership (2026-11-09 course correction).** EventStore's contract tests are backend neutral and use supported Dapr APIs and owning-actor readback against each explicitly supported Platform-supplied profile. Platform owns infrastructure lifecycle, concrete component configuration and bounded host/fault controls. Existing PostgreSQL/Redis fixtures and earlier observations remain historical inputs; future infrastructure execution follows Platform's accepted scope. A missing mandatory primitive remains a blocker, and a pass on one concrete profile proves no other profile. The owning spec's Current Qualification Ownership section governs this handoff without changing the production, migration, private-build or provider-exception gates.

Rationale: keep the active epic and owning spec coherent. P3 changes no AD-5 or AD-26 architecture bytes and therefore adds no ratification-subject change beyond the already accepted P2 candidate.

**Preservation requirements:** AC1–AC11 retain their identities and full adversary/positive-control denominator; only the exact wording above changes. Preserve the seven-part commit contract apart from P3.B's provider-specific implementation wording, lifecycle/HMAC qualification, raw HTTP/gRPC and reachable-alias coverage, metadata-only conflicts, exact original-batch Unknown reconciliation, failover ordering, erasure/recreation protection, 3.18 compatibility, 5.6/5.7 isolation/migration, 7.6/7.22 custody/restore, existing statuses and all historical Verification/Implementation Notes. No test has executed, task completed or new provider capability been demonstrated by this proposal.

### P4 — Dapr Acceptance Readback And Admin Fallback Repair

Status: **APPLIED**. The owner approved P4 individually with “äpprove” and then approved the complete P1–P5 planning application on 2026-11-09. The exact acceptance edits below are applied; the named code/test implementation tasks remain future story work.

#### P4.A — Story 7.11 Typed Helper Boundary

Target: [epics.md](epics.md), Story 7.11, the exact Then clause of the shared helper acceptance criterion. Preserve the following explicit identity/evidence-return requirement.

**OLD:**

> **Then** typed asynchronous seams can read EventStore event/metadata/status, projection detail/index/batch/lifecycle/checkpoint, publication/dead-letter, Admin audit, DAPR component, and CloudEvent evidence through their production storage/broker boundaries

**NEW:**

> **Then** typed asynchronous seams can read EventStore event/metadata/status, projection detail/index/batch/lifecycle/checkpoint, publication/dead-letter, Admin audit, DAPR component, and CloudEvent evidence through supported Dapr APIs and owning-actor boundaries on the Platform-supplied environment; reusable module helpers require no provider SDK, TCP database endpoint, private actor-key layout or backend credential

Rationale: close the shipped Redis helper gap without weakening exact persisted evidence or moving business assertions to Platform. Logical actor state keys within a supported owning-actor API are distinct from constructed physical storage keys; generic state access to actor storage remains prohibited.

#### P4.B — Story 7.11 Platform Persistence Scenario

Target: epics.md, Story 7.11; append this acceptance block after its current final acceptance criterion, before Story 7.12. Preserve the existing Current reconciliation paragraph and all other acceptance criteria.

**OLD:**

> No explicit Platform gateway-to-Dapr persistence acceptance criterion follows Story 7.11's current final acceptance criterion.

**NEW:**

> **Given** a validated Platform descriptor binds the run, synthetic tenant/data isolation, gateway, an enrolled synthetic domain operation and the owning actor/Dapr read path with the required private credentials
> **When** the Platform integration test submits one stable command through the authenticated EventStore gateway and observes its original outcome within a bounded cancellable wait
> **Then** fresh logical owning-actor readback proves the exact expected event identity, payload, sequence and available authoritative stream metadata, with the command's required durable status/publication relationships explicitly enumerated by the scenario
> **And** `/ready`, HTTP 202, raw Redis string writes/reads, a physical-store snapshot or a helper-created result cannot pass persistence acceptance; missing bindings/domain enrollment, denial, timeout or unavailable readback fail the lane without startup, fake data, broadened grants, unexpected skip or blind command resubmission.

Rationale: the current gateway smoke test writes an unrelated key directly to Redis. Its replacement must prove a real EventStore operation. Reuse an existing declared synthetic/sample operation supplied by Platform; if the selected composition has none, acceptance is blocked pending its owner's exact isolated composition change. Do not add a product test endpoint or quietly enlist an unrelated live domain. The isolated run's cleanup remains Platform-owned.

#### P4.C — Story 7.11 Descriptor And Public Helper Migration

Target: epics.md, Story 7.11; append the following two acceptance blocks immediately after the new P4.B block in the same Acceptance Criteria section, before Story 7.12.

**OLD:**

> No explicit descriptor-bound replacement and public compatibility criteria follow the proposed Platform persistence scenario.

**NEW:**

> **Given** logical readback uses an explicitly declared resource/module/endpoint tuple in the existing validated descriptor and its runner-owned private credential channel
> **When** the shared helper is called with a foreign tenant, missing/wrong owner or endpoint, disposed descriptor, cancellation, malformed/incomplete response or an unsupported authoritative field
> **Then** it rejects the call before disclosing cross-tenant state or reports the exact bounded failure; it constructs no physical actor key, scans no database and creates/stops no infrastructure
> **And** it never turns denial, timeout, unavailable metadata or missing credentials into an empty successful stream, NotFound or fabricated LastModified/ETag/retention metadata; fresh reads and finite count/byte/time limits remain explicit.
>
> **Given** `TestEnvironmentStreamReader.ReadStoredStreamAsync` is a public testing-package method returning `AggregateMetadata` and event envelopes
> **When** its Redis implementation is replaced by a logical Dapr reader
> **Then** Story 3.18 inventories source/binary/wire and behavioral compatibility, identifies actual consumers and approves the additive/deprecation/removal or SemVer-major migration before any incompatible change; a narrower actor response cannot be populated with invented legacy fields
> **And** shared-helper conformance and release remain blocked while the direct-provider read path is still reachable; this correction grants no temporary provider exception, automatic API removal, silent endpoint reinterpretation or new dependency/version choice.

Rationale: the current tuple's `AggregateMetadata` includes fields not supplied by `GetStreamMetadataAsync`. Reuse bounded existing actor methods for the values they actually expose, or an approved supported owning-actor state/read contract for the required authoritative fields. Any additive contract change is specified and compatibility-reviewed first. Do not silently map Redis `tcp` resources to a different transport or mint credentials.

#### P4.D — Story 3.18 Compatibility Case

Target: epics.md, Story 3.18; insert this acceptance block immediately before Current reconciliation, preserving its existing general API/wire/SemVer and package-only consumer gates.

**OLD:**

> No specific Dapr readback-helper and Admin lookup-source compatibility case precedes Story 3.18's Current reconciliation paragraph.

**NEW:**

> **Given** the public Testing.Integration stream reader, descriptor endpoint/credential bindings and Admin actor lookup result/source semantics change under the Dapr/Platform ownership correction
> **When** compatibility is classified before implementation and then validated against representative package-only consumers
> **Then** exact affected signatures, returned metadata, endpoint meaning, failure behavior, lookup-source values and serialized payloads are inventoried; existing general SemVer/deprecation/removal rules govern every incompatible change
> **And** no provider-dependent compatibility shim, fabricated legacy metadata, silent endpoint reinterpretation, forced package/catalog update or broadened actor-state grant is accepted as migration; unresolved compatibility keeps the affected implementation/release blocked.

Rationale: preserve public compatibility as a real prerequisite rather than promising that replacing Redis internals is automatically transparent. This criterion chooses no breaking-change policy on behalf of release owners.

#### P4.E — Story 5.6 Admin Owner-Read Requirement

Target: epics.md, Story 5.6, the exact And clause of reader/binding migration acceptance. Preserve the existing Given/When/Then and 3.18 compatibility dependency.

**OLD:**

> **And** Story 3.18 classifies public/configuration compatibility and verifies representative package-only consumers; additive bindings are preferred, breaking changes require their compatibility decision, and broad actor-store access is never a fallback.

**NEW:**

> **And** Story 3.18 classifies public/configuration compatibility and verifies representative package-only consumers; additive bindings are preferred, breaking changes require their compatibility decision, and broad actor-store access is never a fallback. Admin actor inspection uses the supported owning-sidecar/actor path; absent owner binding yields LookupUnavailable without generic state access to constructed private actor keys. Denial, timeout or unavailable ownership evidence is never reported as NotFound or successful empty state; preserve tenant/role authorization, payload/snapshot redaction, cancellation and bounded timeouts.

Rationale: remove the concrete `ComposeActorStateKey`/generic `GetStateAsync` fallback while preserving the existing owner-sidecar read path and safe response contract. Operational non-actor Dapr state probes remain separately classified; this is not a ban on every Admin `GetStateAsync` use.

#### P4.F — Story 7.19 Evidence-State Regression

Target: epics.md, Story 7.19; append this acceptance block after its current final acceptance criterion, before Story 7.20. No screen, route, model shape or new UI workflow is introduced by this planning edit.

**OLD:**

> No explicit actor-inspection owner-binding regression follows Story 7.19's current final acceptance criterion.

**NEW:**

> **Given** Admin actor inspection has no configured owner binding, or the owning Dapr path is denied, timed out or unavailable
> **When** the typed Admin result is rendered
> **Then** the existing unavailable/unknown evidence treatment is used without displaying a found actor, successful empty stream, NotFound or healthy ownership claim
> **And** bounded reason/source metadata, existing tenant/role authorization, snapshot/payload redaction and cancellation remain intact; available and authoritative-not-found controls through the valid owner path still pass, without direct database/private actor-key access or new UI endpoints.

Rationale: make the backend correction visible through the existing honest evidence states rather than masking it as an empty lookup. Compatibility of source labels/defaults remains under 3.18; no DTO constructor/serialization change is authorized by this criterion alone.

#### P4.G — Exact Code/Test Implementation Handoff

These are future tasks under the existing stories after the complete planning proposal is adopted and each dependency is ready. They are not code patches in this course-correction run.

| Owner / files | Current behavior | Required change and meaningful validation |
| --- | --- | --- |
| 7.11 — [GatewayPersistenceTests.cs](../../tests/Hexalith.EventStore.Platform.IntegrationTests/GatewayPersistenceTests.cs) | `/ready` plus direct Redis StringSet/StringGet of a test key. | Replace the database probe with the descriptor-bound authenticated synthetic EventStore command and exact original-outcome/logical actor readback scenario from P4.B. Preserve anonymous command-status rejection and native diagnostic observations. Require real enrolled command behavior; missing Platform inputs are a test-environment failure, not a skip or direct-store substitute. |
| 7.11 — [PlatformEnvironmentFixture.cs](../../tests/Hexalith.EventStore.Platform.IntegrationTests/PlatformEnvironmentFixture.cs) and [test project](../../tests/Hexalith.EventStore.Platform.IntegrationTests/Hexalith.EventStore.Platform.IntegrationTests.csproj) | Requires `redis/eventstore/tcp`; test project references StackExchange.Redis. | Consume only explicitly declared logical gateway/owning-Dapr bindings through the existing fixture/descriptor mechanism; remove the Redis requirement and test-project provider reference when its last use is gone. Preserve run/data/tenant identity, finite waits, private credentials and fixture-only disposal. Do not guess an owner app ID, endpoint, provider or token. |
| 7.11 + 3.18 — [TestEnvironmentStreamReader.cs](../../src/Hexalith.EventStore.Testing.Integration/TestEnvironmentStreamReader.cs) and [project](../../src/Hexalith.EventStore.Testing.Integration/Hexalith.EventStore.Testing.Integration.csproj) | Public helper opens Redis, scans actor suffixes and reads hash field `data`. | Introduce the compatibility-approved logical reader using supported actor APIs and verified owner bindings; remove the direct Redis implementation/reference only under the accepted consumer migration. Never invent legacy `AggregateMetadata` fields from the narrower `AggregateStreamMetadata` result. Keep real authoritative values, count/byte bounds, cancellation, redaction, foreign-tenant rejection before network access and exact error distinctions. |
| 7.11 — [DescriptorFixtureTests.cs](../../tests/Hexalith.EventStore.Testing.Integration.Tests/DescriptorFixtureTests.cs), [DescriptorPersistenceFixture.cs](../../tests/Hexalith.EventStore.Testing.Integration.Tests/DescriptorPersistenceFixture.cs) and existing descriptor tests in [Testing.Tests](../../tests/Hexalith.EventStore.Testing.Tests/TestEnvironmentDescriptorReaderTests.cs) | Redis resource binding/foreign-tenant tests and descriptor lifecycle/credential checks. | Test the selected logical endpoint/owner contract and refusal before client/state exposure. Preserve the generic descriptor's support for other explicitly declared resources; remove only the module reader's Redis dependence. Prove wrong/missing endpoint, foreign tenant, disposed run, cancellation and malformed/denied/unavailable observations, plus an intact logical positive control. Any descriptor-schema change requires its existing owner handoff first. |
| 5.6 + Admin owners — [DaprInfrastructureQueryService.cs](../../src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs) | Owner-sidecar actor reads when configured; otherwise composes a physical actor key for generic Dapr state reads. | Delete only the private-key composition/fallback path after owner binding/configuration compatibility is classified. Preserve the existing supported owner actor-state API; return bounded LookupUnavailable when the owner binding is absent. Distinguish authoritative absence from denial/transport/timeout and retain cancellation and all current redaction bounds. |
| 5.6 / 7.19 — existing [Admin service tests](../../tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprInfrastructureQueryServiceTests.cs) and [DaprActorsPageTests.cs](../../tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprActorsPageTests.cs) | Some tests set generic-state read responses or use `DaprStateStoreActorKeys` evidence. | Replace assumptions about private storage with a valid owner HTTP/actor positive control and authoritative NotFound control; add absent-owner/denied/timeout/unreachable cases that make zero generic actor-storage reads. Preserve payload/snapshot redaction, authorization and no false UI success. A synthetic unavailable-only test cannot replace the lawful owner control. |
| 3.18 — [DaprActorInstanceState.cs](../../src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorInstanceState.cs) and public testing-package consumers | Existing public constructors/default lookup-source text and raw-metadata helper result. | Inventory/classify behavioral and wire/source compatibility before altering signatures/defaults/source labels. Update misleading private-key XML prose when implementing the approved contract; preserve the public shape unless a separately approved compatible/additive or major migration says otherwise. |
| 3.17 — existing Contracts architecture/packaging lane and planned module operation inventory | Whole-runtime evaluated guard is still undelivered. | Cover the repaired test-support package and linked/transitive/generated inputs; reject provider-client/private actor-key leakage in reusable helpers without treating all tests or provider namespaces as exempt. Exact scoped SignalR/Azure decisions remain unchanged. Static package scans alone do not establish network enforcement. |

**Execution sequence:** obtain the existing Platform/runner contract's accepted logical endpoint, credential, enrolled synthetic operation and cleanup bindings; classify helper/Admin compatibility through 3.18; implement shared logical readback and migrate the gateway scenario; repair the Admin fallback and evidence regressions; remove unused provider references and enforce the evaluated guard. Missing inputs or compatibility leave those tasks blocked. Extend the existing descriptor only through its owning repository; no second runner/descriptor or proprietary service is introduced.

**Validation handoff:** run the focused Testing.Integration, Testing, Platform.Integration, Admin.Server and Admin.UI test projects appropriate to each actual change, plus the 3.17 Contracts guard and 3.18 package-only consumer checks. Live contract acceptance executes against the existing `platform-source` and `platform-package` environments with exact inputs/identities and no unexpected skips. No solution-wide test, provider deployment, pin change, sealed-workflow edit, test pass or runtime conformance is claimed by this plan. G-APPEND remains independently blocked on the mandatory primitive even after these readback repairs.

### P5 — Exception Truth, Platform Guidance And Final Handoffs

Status: **APPLIED**. The owner replied “approve” to P5 and complete P1–P5 planning application on 2026-11-09. The exact documentation/tracking edits below are applied. Runtime/test code repairs, Platform/Builds changes, dependency updates, infrastructure execution, migrations, private builds and provider exceptions remain with their stated future stories and decisions.

#### P5.A — Architecture AD-34 Current State And Gate Row

Target: [architecture.md](architecture.md), AD-34 Current state; replace the complete paragraph below. The exception register itself remains unchanged.

**OLD:**

> **Current state.** The register holds one accepted exception: the opt-in SignalR Redis backplane (`signalr-replica-distribution`, owner decision 2026-10-25, Story 2.13), limited to its SignalR registration, options and diagnostics paths and the two host projects, and granting no production eligibility. Known non-conformances, which are not exceptions and may not be extended or selected before their owning qualification, include production payload key operations (the Dapr Cryptography API is alpha and exact-version wrap/unwrap exists only as `Subtle*Alpha1` operations), and Admin's `DaprInfrastructureQueryService` reads of Dapr private actor-state keys. The list is non-exhaustive until the Story 3.17 inventory completes. This rule itself accepts no exception, records no qualification, and changes no readiness verdict; AD-26 ratification and production gates still fail closed.

**NEW:**

> **Current state.** The register retains two existing scoped decisions: `signalr-replica-distribution` (Story 2.13, owner decision 2026-10-25) and `payload-production-key-operations` (Story 8.6, owner decision 2026-11-03 with its exact correction/diagnostic limits). Their purposes, allowed paths, credentials and review/removal triggers remain those of the register; neither grants production eligibility or permission to expand provider use. Azure 8.6 and its non-packable companion remain incomplete/deferred; the selected Dapr/Kubernetes successor stays gated by 8.12 and its exact schema, custody and qualification requirements. Admin's private actor-key fallback, the Redis-specific reusable stream helper and direct Redis gateway persistence probe remain unresolved module boundary gaps under 3.17/5.6/7.11 and 3.18 compatibility. Planning corrections do not fix or qualify those paths. The inventory/guard remains backlog and non-exhaustive; this rule accepts no exception, records no qualification and changes no readiness verdict. AD-26 ratification, G-APPEND and production gates still fail closed.

Target: the same architecture, Dapr boundary inventory and guard status row; replace this exact cell text once.

**OLD:**

> No PRD §8.4 conformance claim; the register holds only the Story 2.13 SignalR Redis exception, and unresolved rows block conformance

**NEW:**

> No PRD §8.4 conformance claim; preserve the scoped Story 2.13 SignalR Redis and deferred Story 8.6 Azure decisions; unresolved module paths and missing profile qualification block conformance

Rationale: report the current register and selected successor accurately without creating, extending or revoking an exception, or implying that the three audited paths have been repaired.

#### P5.B — Story 3.17 Exception Reconciliation

Target: [epics.md](epics.md), Story 3.17 Dependencies; replace the exact coordination sentence.

**OLD:**

> Coordinate 2.13/8.6 qualification without blocking initial inventory/guard work or creating cycles.

**NEW:**

> Consume the current scoped 2.13/8.6 decisions and selected 8.12 qualification requirements without blocking initial module inventory/guard work or creating cycles; concrete profile execution remains Platform-owned.

Target: Story 3.17 Current reconciliation; replace the exact stale clause.

**OLD:**

> accepted exceptions are empty.

**NEW:**

> the register retains the scoped 2.13 SignalR Redis and deferred 8.6 Azure decisions, while the final evaluated inventory/guard and unresolved module/profile qualification remain undelivered.

Target: [3.17 spec](../implementation-artifacts/3-17-dapr-boundary-inventory-and-enforcement.md), frontmatter owner line.

**OLD:**

> owner: Platform architecture and packaging maintainer

**NEW:**

> owner: EventStore architecture and packaging maintainer; Platform supplies infrastructure qualification

Target: the same spec, AC3; replace the exact accepted-register sentence only.

**OLD:**

> The accepted register holds only the Story 2.13 SignalR Redis exception (owner decision 2026-10-25) and gains no other entry until a separately reviewed decision exists.

**NEW:**

> The accepted register retains the scoped Story 2.13 SignalR Redis and Story 8.6 Azure key-operation decisions, including their exact correction/diagnostic limits; further entries or scope changes require a separate reviewed decision, and neither existing entry authorizes the audited Redis helpers, private actor-key access or the selected Dapr/Kubernetes successor.

Target: the same spec, AC6; replace this exact follow-up clause.

**OLD:**

> including planned key operations under 8.6/draft amendment; the accepted 2.13 SignalR Redis exception enters the guard as an accepted row.

**NEW:**

> including selected 8.12 and deferred 8.6 qualification; the scoped 2.13 SignalR Redis and 8.6 Azure decisions enter the guard with their existing exact limits and review/removal triggers.

Target: the same spec, the Refine guide/inventory/register task; replace the exact stale disposition phrase.

**OLD:**

> preserving the accepted 2.13 row and the unresolved 8.6 row, and accepting no other exception without its separate exact-content decision.

**NEW:**

> preserving the scoped 2.13 and 8.6 decisions and their limits, retaining selected 8.12/deferred 8.6 qualification as unresolved, and accepting no new or broader exception without its separate exact-content decision.

Target: the same spec, the final Retain commands/results task; replace this exact stale follow-up phrase.

**OLD:**

> unresolved 2.13/8.6 migrations,

**NEW:**

> unresolved qualification/migration obligations including selected 8.12 and deferred 8.6, plus the scoped 2.13/8.6 dispositions,

Rationale: the guard must validate the actual accepted decisions and unresolved work rather than an obsolete zero/one-entry description. Preserve backlog status, analysis limits, all negative fixtures and existing AD-12 evidence-storage precedence; no inventory implementation or new evidence machinery is introduced.

#### P5.C — Boundary Guide Open Module Gaps

Target: [boundary guide](../../docs/concepts/dapr-infrastructure-boundary.md); insert the new section immediately before the existing Notification And Crypto Qualification heading, preserving all existing qualification history and exception bytes.

**OLD:**

> ## Notification And Crypto Qualification

**NEW:**

> ## Open Module Boundary Follow-ups
>
> The 2026-11-09 ownership audit found three unresolved paths: Testing.Integration's Redis/physical-actor-key stream reader, the Platform integration test's direct Redis persistence probe, and Admin's generic-state/private actor-key fallback. Stories 7.11/5.6 own the Dapr-path repairs, 3.18 owns public/behavioral compatibility, 7.19 preserves honest Admin evidence states, and 3.17 inventories/enforces the result. The approved SignalR and Azure decisions do not cover these paths. Until actual implementation and live/package validation pass, they remain known gaps rather than accepted diagnostics or conformant helpers.
>
> Platform owns concrete environment lifecycle, provider/component configuration and profile/fault/restore execution. EventStore contributes backend-neutral Dapr assertions and consumes the existing run-owned descriptor and private credential channels. Provider diagnostics cannot replace logical owning-actor acceptance. The unsupported mandatory append primitive remains independently blocked; changing ownership or repairing readback cannot supply it.
>
> ## Notification And Crypto Qualification

Rationale: preserve truthful current behavior while making the new ownership and code handoff discoverable to contributors. The older provider observations retain their original input identities and dates.

#### P5.D — Platform-Pinned Developer Instructions

Target: [README.md](../../README.md), Run and Debug in the Platform Workspace, the exact prerequisite sentence after the Platform/module description.

**OLD:**

> From the Platform root, with the pinned module tool, Aspire CLI `13.6.1` exactly, Docker, and `HEXALITH_DAPR_HOME` pointing at an isolated Dapr home (CLI `1.18.0` at `tools/dapr`, runtime `1.18.2` in `.dapr/bin`):

**NEW:**

> From the Platform root, follow that checkout's README and pinned tool/catalog instructions for the module tool, Aspire CLI, Docker and isolated `HEXALITH_DAPR_HOME`; concrete infrastructure and runtime selection belong to Platform/Builds rather than copied EventStore version literals:

Target: the same section, the exact resource startup clause in the debug paragraph.

**OLD:**

> starts only the `eventstore` gateway with its Dapr sidecar, the Dapr control plane and Redis,

**NEW:**

> starts the declared module within Platform's selected development topology,

Target: [CONTRIBUTING.md](../../CONTRIBUTING.md), Run and Debug, the exact Aspire CLI prerequisite.

**OLD:**

> - Aspire CLI `13.6.1` exactly; the tool refuses any other version with `HXR015`.

**NEW:**

> - The exact Aspire CLI selected by the current Platform checkout's pinned tool/catalog instructions; use its README rather than a copied EventStore version.

Target: the same section, the complete three-line Dapr-home prerequisite.

**OLD:**

> - `HEXALITH_DAPR_HOME` set to an absolute, isolated Dapr home: Dapr CLI `1.18.0` at `tools/dapr` and runtime
>   `1.18.2` at `.dapr/bin/{daprd,placement,scheduler}`. The Platform README shows how to prepare one from the
>   official release assets.

**NEW:**

> - `HEXALITH_DAPR_HOME` set to the absolute, isolated home prepared from the current Platform README's exact CLI/runtime inputs and layout. A candidate qualification lock in EventStore does not change Platform's selected development runtime.

Target: the same section, the exact two-line orchestration-package prerequisite; preserve the following NuGet-folder guidance.

**OLD:**

> - The local Aspire orchestration and dashboard packages at `13.6.1`: run `dotnet restore apphost.cs` from
>   Platform once before the first `debug` run. Tool restore alone does not install these SDK packages.

**NEW:**

> - The local orchestration/dashboard packages declared by Platform's `apphost.cs`: restore them from the Platform root as its README directs before the first run. Tool restore alone does not install these SDK packages.

Target: the same section, only the hard-coded Dapr-home export line inside the shell example. Preserve the following module command.

**OLD:**

> export HEXALITH_DAPR_HOME="$HOME/.hexalith/dapr/1.18.2"

**NEW:**

> : "${HEXALITH_DAPR_HOME:?Set HEXALITH_DAPR_HOME to the isolated home prepared from the Platform README}"

Target: the same section, the exact wrapped resource startup clause.

**OLD:**

> starts only the
> `eventstore` gateway with its Dapr sidecar, the Dapr control plane and Redis,

**NEW:**

> starts the declared module within Platform's selected development topology,

Rationale: preserve owner-manifest, run identity, Debug attach and exact-run cleanup guidance while delegating concrete topology and version selection to its authoritative repository. These edits select/install/update no tool, runtime or dependency and execute none of the documented commands.

#### P5.E — Deployment Documentation Scope

Target: [deploy/README.md](../../deploy/README.md), the complete opening paragraph after Deployment Configuration.

**OLD:**

> Production DAPR component configurations for deploying Hexalith.EventStore to different environments. Swap these YAML files to change backends with **zero application code changes, zero recompilation, zero redeployment** of application containers (NFR29).

**NEW:**

> This directory retains EventStore Dapr requirements, component templates and historical provider examples. Hexalith.Platform owns concrete backend/component selection within the approved profile, shared infrastructure composition and lifecycle, provisioning, and deployment/backup/restore execution. EventStore owns backend-neutral module semantics and Dapr-path acceptance assertions. A backend/runtime/component change requires the applicable compatibility, qualification and separately authorized migration work; exchanging YAML does not establish equivalent guarantees or a zero-redeployment result. The compatibility tables and commands below describe template/example coverage, not current production eligibility or deployment authority. Story 5.9 reconciles them against Platform's accepted rendered/loaded profile; AD-26 and the existing release/deployment gates remain binding.

Rationale: remove the unsupported unconditional hot-swap claim and clarify why provider examples can remain historical documentation while EventStore runtime behavior stays behind Dapr. This is no production-profile selection or provisioning recipe.

#### P5.F — Sprint Comments Only

Target: [sprint-status.yaml](../implementation-artifacts/sprint-status.yaml); replace each exact row below with its new comment plus the identical row. All non-comment YAML content, IDs, statuses and counts remain unchanged.

**OLD:**

>   3-17-dapr-boundary-inventory-and-enforcement: backlog

**NEW:**

>   # 2026-11-09: module Dapr inventory/guard consumes Platform qualification; existing scoped exceptions remain unchanged.
>   3-17-dapr-boundary-inventory-and-enforcement: backlog

**OLD:**

>   3-21-production-dapr-runtime-pin-and-durable-broker-qualification: in-progress

**NEW:**

>   # 2026-11-09: EventStore supplies requirements/assertions; Platform owns concrete profile execution; prior 1.19.1 results remain historical.
>   3-21-production-dapr-runtime-pin-and-durable-broker-qualification: in-progress

**OLD:**

>   4-16-append-write-once-conformance-through-enforced-envelope: backlog

**NEW:**

>   # 2026-11-09 scope/ownership: ../planning-artifacts/sprint-change-proposal-2026-11-09.md
>   # Backend-neutral Dapr tests consume Platform profiles; missing mandatory primitive keeps G-APPEND FAIL/BLOCKED.
>   4-16-append-write-once-conformance-through-enforced-envelope: backlog

**OLD:**

>   5-6-apphost-component-loading-and-sidecar-argument-parity: backlog

**NEW:**

>   # 2026-11-09: 5.6-5.9 retain module contracts/readers/parity/docs; concrete infrastructure execution belongs to Platform.
>   5-6-apphost-component-loading-and-sidecar-argument-parity: backlog

**OLD:**

>   7-11-persisted-state-evidence-and-read-back-helpers: backlog

**NEW:**

>   # 2026-11-09: Dapr/owning-actor helpers and gateway persistence repair; 3.18 compatibility and Platform descriptor inputs precede code.
>   7-11-persisted-state-evidence-and-read-back-helpers: backlog

Rationale: make the accepted correction discoverable without claiming implementation, moving lifecycle states or reopening closed diagnostic slices.

#### P5.G — Architecture Memlog Append

Target: [architecture memlog](architecture/architecture-eventstore-2026-07-05/.memlog.md); append the following entry after existing content. Preserve every original byte and frontmatter value.

**OLD:**

> No 2026-11-09 Dapr/Platform ownership-correction entry is appended.

**NEW:**

> - (decision, 2026-11-09) Applied the owner-approved Dapr/Hexalith.Platform planning correction from sprint-change-proposal-2026-11-09.md. EventStore keeps backend-neutral contracts, module declarations and Dapr/owning-actor assertions; Platform owns concrete infrastructure lifecycle/configuration and profile/fault/restore execution; Builds retains pins/runner contracts. AD-9/AD-34 and known-gap summaries change outside the AD-26 subject, and the coordinated AD-26 Shared-host proof ownership amendment changes that subject from b9e899ecd6bb456537358b23cb69a564e09bc2c7b199dd657075900b2d17503b to d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16 under its existing UTF-8/LF rule. AD-5 append semantics, AD-26 target/status, lifecycle values, runtime/component pins, historical observations and both scoped exception decisions are retained. Fresh per-role ratification and all existing final-proof gates remain; no push window, ratification, runtime capability, test pass, production selection or deployment is claimed by this planning application.

Rationale: record both outside-subject changes and the coordinated subject amendment exactly as AD-26 requires. This is a planning decision record, not a new authority/evidence mechanism.

#### P5.H — Exact Platform/Builds Follow-Up And Acceptance

This written handoff belongs in the final proposal; no external message, issue, PR or cross-repository edit is sent or made. The named owning repositories must accept their exact execution work before it is performed. Existing scenarios and rejected observations stay in their denominator.

| Owning repository / interface | Required follow-up | Acceptance condition |
| --- | --- | --- |
| Hexalith.Platform — existing module declaration, composition and test-environment handoff | Bind the selected run's gateway, existing enrolled synthetic domain operation and supported owning-actor read path, exact module/app/namespace identity, loaded runtime/component profile, data/synthetic-tenant isolation and private credential channels. Reuse current composition and descriptor mechanisms. | P4's command uses normal gateway authorization/admission; its exact durable result is read through the owning Dapr boundary. Missing actor/read or domain enrollment support is an explicit prerequisite failure, not invented endpoint/credential data or raw Redis access. |
| Hexalith.Platform — concrete profile/fault qualification | Own disposable backend/control-plane/host lifecycle, process suspend/resume and acknowledgement-loss controls, alias/isolation checks, destructive/TTL/erasure adversaries, provider diagnostics and cleanup. Consume EventStore's required semantic suite. | All applicable AC1–AC11 and module readback predicates run against the exact declared profile when a supported primitive exists; healthy lawful controls distinguish guard failures from transport/storage outages. Absent mandatory capability remains NO-GO; private builds and provider exceptions are not selected. |
| Hexalith.Platform — existing 3.21/5.7/7.6/7.22 handoffs | Supply exact qualification, rendered/loaded configuration, secret-reference/custody, migration/rollback and restore observations under accepted execution scope. Retain the complete broker scenario matrix and all five rejected 1.19.1 guarantees; optional remediation is not started by this proposal. | Story 3.21 consumes qualified results without changing broker selection or closing the parent from capture success; 5.7/7.22 retain isolation and restore consistency, and production waits on all existing profile/ratification gates. |
| Hexalith.Builds — existing pinned tool/catalog/runner and descriptor contract | Classify any missing logical endpoint/private credential or fault-control binding in the existing schema and runner; supply an exact compatible extension only through its owning change. Align pins only after the required qualification and accepted scope. | Module `platform-source`/`platform-package` tests consume the agreed handoff without provider credentials, module-invented descriptor fields, copied version literals or sealed-workflow edits. No catalog/gitlink/dependency update is performed here. |
| EventStore — existing 3.17/3.18/4.16/5.6/7.11/7.19 | Apply accepted planning text, then create/build the separately scoped module repair stories after their stated dependencies. | Actual code/test fixes, evaluated guard, package compatibility and live logical acceptance pass before conformance or completion claims. Planning approval alone supplies none of those results. |

**Final application scope:** apply only the exact P1–P5 documentation/backlog amendments in EventStore, plus proposal decision/verification bookkeeping. Preserve all code, components/locks, exception-register bytes, dependency/gitlinks, sealed workflows, historical provider observations and story lifecycle values. This complete proposal does not authorize implementation of P4 code, a runtime build, cross-repository changes, external communication or infrastructure execution. After application, route the moderate backlog correction through its written owner handoffs; it remains an implementation plan with explicit blocked dependencies.

## 5. Implementation Handoff

Classification: **Moderate** — Product Owner / Developer coordination for existing backlog and acceptance changes, with Architecture checking the preserved boundaries and ratification subject.

| Recipient | Responsibility after final approval | Success criterion |
| --- | --- | --- |
| EventStore planning / architecture | Apply accepted policy, architecture, epic/spec and guide edits; retain statuses and historical observations. | Ownership agrees across artifacts; no provider choice or missing guarantee is inferred away. |
| EventStore developer / test owners | Implement approved module Dapr tests and owning-actor readers; classify helper/configuration compatibility through 3.18; finish 3.17's actual evaluated-graph guard. | EventStore writes and acceptance reads traverse supported Dapr/actor APIs; no Redis/PostgreSQL key layout or provider credential is needed by the shared helper. |
| Hexalith.Platform owner | Accept an exact infrastructure/qualification handoff in Platform's own repository; supply isolated descriptors, loaded identities, profile/fault/restore observations and safe diagnostics. | Same module contract suite runs against each explicitly supported Platform profile, with truthful capability failures and no module-owned infrastructure lifecycle. |
| Hexalith.Builds owner | Align existing catalog/tool/runner contracts only through separately scoped work after the required qualification. | No implicit dependency refresh or sealed-workflow modification. |

Written handoff only: no owner message, issue, PR or external action has been sent. The current correction authorizes no private runtime, provider exception, key distribution, infrastructure mutation, dependency/submodule update, Git mutation, migration or deployment. Platform acceptance and any later implementation remain pending.

## Checklist And Verification

| Checklist items | Status | Result / remaining action |
| --- | --- | --- |
| 1.1–1.3 | [x] | Trigger, requirement clarification and concrete source/planning evidence recorded. |
| 2.1–2.5 | [x] | All nine epics assessed; existing stories can carry the correction; policy/ownership precedes implementation; no new epic or deletion proposed. |
| 3.1–3.2 | [x] | PRD and architecture conflicts identified, including AD-26 subject implications. |
| 3.3 | [N/A] | No UX design change; preserve current evidence and failure states. |
| 3.4 | [x] | Runtime, reusable helpers, fixtures, CI consumption, documentation and Platform integration inspected. |
| 4.1 | [x] | Direct adjustment recommended; planning low effort, module corrections medium. |
| 4.2 | [N/A] | Rollback not selected; retain functioning seams and historical observations. |
| 4.3 | [N/A] | MVP reduction not selected; mandatory guarantee and blockers remain. |
| 4.4 | [x] | Recommendation and cross-repository risk stated. |
| 5.1–5.3 | [x] | Issue, impacts and recommended approach recorded. |
| 5.4–5.5 | [x] | MVP impact, sequence, handoff roles and success criteria recorded. |
| 6.1–6.2 | [x] | P1-P5 approved and applied; all 12 target files match the reviewed candidate. |
| 6.3 | [x] | Owner explicitly approved P5 and complete planning application on 2026-11-09. |
| 6.4 | [x] | Approved sprint comments applied; all non-comment YAML, story IDs and lifecycle values preserved. |
| 6.5 | [x] | Written EventStore/Platform/Builds handoffs delivered in P4.G/P5.H; actual downstream acceptance/execution remains a future prerequisite, with no external message or cross-repository change. |

Audit baseline: EventStore HEAD `9bf5b587ba294a881996234f59dede8fb31dbddb`. During the audit an external commit, `dcdeab412428e5d4f74f834c4b730e016262ec7b`, recorded only the pre-existing Story 4.16 handoff-recheck edit. Its bytes match the audit-start copy; the four canonical planning inputs are unchanged. This workflow performed no Git mutation. All four canonical top-level planning artifacts were scanned with their complete byte identities retained in local scratch; relevant policy/story sections, UX handoff, module/CI declarations, owning Platform composition and identified call sites were inspected. The tracked project/package screen covered 62 `.csproj` files and found no direct SQL/broker-client imports in the screened non-orchestration, non-isolated-Azure `src` paths. This is a static screen: evaluated/transitive/linked/generated graphs, deployed configuration and live egress are not proven. Story 3.17's final inventory remains absent and backlog.

Checks: `python3 -B /tmp/eventstore-dapr-platform-course-cz20xqh2/check_proposal.py` verifies unchanged canonical planning inputs and pre-existing 4.16 bytes, exact OLD ownership text, new local links, proposal status and allowed changed paths. `git diff --check` checks whitespace. Results are recorded after execution below. No build, live Dapr/PostgreSQL/Redis test, infrastructure launch or new CI run is part of this planning audit.

Results: both commands passed (exit 0) at verification commit `dcdeab412428e5d4f74f834c4b730e016262ec7b`. The initial changed-path assertion detected the external 4.16 commit; read-only comparison confirmed only that path was committed and its bytes were preserved before the check was rerun. The proposal's own LF/whitespace/size checks also passed. Only this new draft is untracked; no canonical document, source, dependency, submodule or infrastructure was changed by this course-correction run.

**P1 decision and P2 preparation — 2026-11-09:** Recorded the owner's literal “approve” as acceptance of P1 for inclusion, retaining its exact replacement text and the final implementation-approval gate. `python3 -B /tmp/eventstore-dapr-platform-course-cz20xqh2/check_p2.py` passed (exit 0): nine exact replacements and seven scoped epic insertions simulated in memory; canonical planning hashes and the existing 4.16 note unchanged; the proposed 3.21 frozen amendment changes only the specified ownership sentences; local links, whitespace and AD-26 status-tag invariance pass. It independently derives the current/proposed AD-26 subjects above and preserves the AD-5 append contract and AD-26 target Rule. `git diff --check` passed (exit 0). No target edit was applied, Platform acceptance was claimed or new execution was authorized. P2 remains pending.

**P2 decision and P3 preparation — 2026-11-09:** Recorded the owner's next literal “approve” as acceptance of P2 for inclusion, retaining every exact replacement and the calculated AD-26 subject. `python3 -B /tmp/eventstore-dapr-platform-course-cz20xqh2/check_p3.py` passed (exit 0): 18 exact replacements and two ownership insertions simulated in memory; P1/P2 text preserved; all 11 AC rows remain identical between the simulated spec/epic; AC1/2/3/6/8/9/11 are byte-identical to their current rows; named rollback/failover/erasure/falsification predicates remain. The frozen amendment is limited to P3.A; the seven-part commit contract changes only the specified item-3 implementation wording. Historical Implementation Notes/Verification, the November 1 proposal, lifecycle values and both runtime locks remain unchanged. `python3 -B /tmp/eventstore-dapr-platform-course-cz20xqh2/check_p2.py` and `git diff --check` also passed (exit 0); P3 changes no architecture subject beyond P2. No target, test, infrastructure or dependency change was applied. P3 and final implementation approval remain pending.

**P3 decision and P4 preparation — 2026-11-09:** Recorded the owner's next literal “approve” as acceptance of P3 for inclusion. `python3 -B /tmp/eventstore-dapr-platform-course-cz20xqh2/check_p4.py` passed (exit 0): prior P1–P3 edit text preserved, two exact replacements and five Given/When/Then/And criteria simulated in four existing stories, story IDs/current reconciliation retained, ten source/contract hashes unchanged, and all new local links/whitespace/size checks pass. `check_p2.py`, `check_p3.py` and `git diff --check` also passed (exit 0); exact proposed subjects, AC1–AC11 and historical observations remain preserved. The current helper's raw-metadata return and the narrower actor metadata response were inspected; P4 explicitly requires compatibility classification instead of invented fields or a provider shim. No code/test API, credential/descriptor schema, dependency, live lane or infrastructure was changed. P4 and final implementation approval remain pending.

**P4 decision and complete P5 candidate — 2026-11-09:** Recorded “äpprove” as the owner's contextual approval of P4 for inclusion. `python3 -B /tmp/eventstore-dapr-platform-course-cz20xqh2/prepare_complete_candidate.py` passed (exit 0), composing P1–P5 into 12 existing documentation/tracking files with 67 bounded edits in scratch only. All prior exact edit text, source hashes, frozen-amendment limits, seven-part commit contract, AC1–AC11 agreement, story IDs/lifecycle values, exception-register bytes, locks/pins and historical observations are preserved. Installed YAML syntax/frontmatter and sprint semantic-equality checks passed. The complete AD-26 candidate remains `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16`; P5 adds no further subject change. `check_p2.py`, `check_p3.py`, `check_p4.py` and `git diff --check` pass (exit 0). The complete candidate diff and before/after hashes are local scratch at `/tmp/eventstore-dapr-platform-course-cz20xqh2/complete-candidate.diff` and `complete-candidate.json`, not committed evidence. Only this proposal is untracked; no target file has been changed. P5 and approval to apply the complete planning/documentation correction remain pending. Builds, live tests, infrastructure execution and new CI runs: none.

**Complete owner approval and planning application — 2026-11-09:** The owner replied “approve” to the explicit P5 and complete P1–P5 documentation/backlog application question. Re-read the exact proposal and revalidated the unchanged current sources at HEAD `dcdeab412428e5d4f74f834c4b730e016262ec7b`; applied all 67 bounded edits to the 12 reviewed target files through `apply_patch`, with no additional target changes. Recorded approval/application status and checklist completion in this proposal. The PRD, architecture/guide, epics, 3.17/3.21/4.16 specs, operator/developer guidance and sprint comments now express the accepted ownership split. P4's actual code/test changes remain future implementation work.

`python3 -B /tmp/eventstore-dapr-platform-course-cz20xqh2/verify_applied.py` passed (exit 0): all 12 target after-hashes exactly match the approved candidate; P1–P5 exact edit text is preserved; YAML/frontmatter, all sprint lifecycle values, AC1–AC11 agreement, specified frozen-amendment limits, historical observations, read-only register/code/manifest/lock inputs, new links, whitespace and size pass. `git diff --check` passed (exit 0). AD-26's applied subject is `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16`; the memlog records the change, and per-role ratification and G-APPEND/final production proof remain pending. The working tree contains only the 12 reviewed modified targets and this new proposal, with no staging, commit, push, submodule or dependency mutation. Builds/live tests/new CI: none, because the application changes planning/documentation and YAML comments only.

**Handoff and workflow completion:** Moderate backlog correction completed. The final Sprint Change Proposal, exact before/after amendments and implementation handoff are delivered here. EventStore development owns the 3.17 guard, 3.18 compatibility and 5.6/7.11/7.19 module repairs; Platform owns concrete infrastructure qualification and acceptance of its execution scope; Builds owns any later exact tool/catalog/runner contract change. P4.G and P5.H state files, prerequisites, sequence and success criteria. No external message or cross-repository acceptance is claimed, and those implementation dependencies remain open. The planning workflow is complete; module-wide conformance is not established by its completion.
