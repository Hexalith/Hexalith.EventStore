---
title: 'EXT-CONV-AI-1 EventStore source and publication authorities'
type: 'feature'
created: '2026-10-25'
status: 'in-progress'
baseline_commit: 'fd083a91b67a3bca10016f1f645da913c7ef526e'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '../agents/_bmad-output/planning-artifacts/external-dependency-register.md'
  - '../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-22.md'
  - '../conversations/docs/implementation/ext-conv-ai-1-runtime-qualification.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Conversations lacks authenticated complete command-source evidence and concrete publication authorities. Deliver EventStore providers enabling its complete tenant catalogue, authoritative denominator and ordered approved-deletion discovery, checkpoint and recovery. Deliver an installation handoff separating implemented components from unverified installations.

## Boundaries & Constraints

**Always:** Reuse EventStore persistence, admission, SDK transport and workload authentication. Bind authority to exact tenant/domain/stream, revision, incarnation and event bytes. Compare and append in the owning actor turn. Preserve unrelated protection changes.

**Never:** Trust caller proof fields, re-enter the originating actor to establish command authority, add domain persistence, install unaccepted bindings, execute Live qualification, change dependency acceptance, or implement Agents Story 8.3/RQ-1 Level 5.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure |
| --- | --- | --- | --- |
| Command | Enrolled exact complete prefix, including empty | Authenticate locally; compare before append | Missing/changed proof denies |
| Stale | Changed head/incarnation, expiry or revocation | No new append/progress | Unavailable/conflict |
| Concurrent/restart | Competing commands or registrations | Serialized writes; durable original recovery | No duplicate effect |
| Delivery loss | Persisted receipt, lost response or rollover | Independent lookup; same signal/revision | Unknown blocks advancement |
| Poison/foreign | Changed same-ID entry or tenant substitution | Quarantine/block prefix; no disclosure | Never skip |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`: owns rehydration, invocation and persistence; `Events/EventStreamReader.cs` and `SourceProofCodec` supply prefix/incarnation validation.
- `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs`: rejects evolution proof carriers; preserve that guard. Conversations requires unsnapshotted legacy JSON events, not effective-event evolution.
- `src/Hexalith.EventStore.ServiceDefaults/Authentication/QueryCallerAuthorityBinding.cs`: reuse signed request-digest binding and resource-capable issuer machinery.
- `src/Hexalith.EventStore.Server/Streams/SourcePublicationIndexActor.cs`: retain anchored conditional staging and progress validation. Namespace actor and existing client feed/dispatcher supply durable discovery/backfill.
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`: existing publication opt-in installs seams, not coverage. Protection's anchor-store default supplies no production journal.

## Tasks & Acceptance

**Execution:**

- [ ] `src/Hexalith.EventStore.Contracts/Commands/CompleteCommandSource.cs`, `src/Hexalith.EventStore.ServiceDefaults/Authentication/CommandSourceAuthorityBinding.cs`: add distinct bounded native-source carrier and exact request binding; retain evolution rejection.
- [ ] `src/Hexalith.EventStore.Server/Actors/AggregateActor.CommandSource.cs` and `AggregateActor.cs`: enroll exact scopes, replay complete immutable prefixes without snapshots, represent head zero explicitly, bind command/source bytes, and compare local metadata/native proof immediately before persistence. Retry recaptures authority.
- [ ] `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` and workload handler; `src/Hexalith.EventStore.DomainService/CompleteCommandSourceVerifier.cs`: carry platform-created bindings; verify authenticated exact requests locally without source I/O. Missing issuer capability denies.
- [ ] `src/Hexalith.EventStore.Server/Streams/SourcePublicationAuthority.cs`, `SourcePublicationAuthorityActor.cs`, corresponding contract files: implement both authority interfaces and independent durable enrollment, roster, anchor, admitted-original transition and immutable progress journals. Persist through actor state; expose exact recovery lookup.
- [ ] `src/Hexalith.EventStore.Server/Streams/SourcePublicationNamespaceActor.cs`, `SourcePublicationIndexActor.cs` and Dapr transports: authenticate method/scope/expected revision/body using enrolled workload bindings; legacy uncredentialed calls deny. Independently authenticate inventory, all-writer enforcement and current enrollment; stored receipt strings grant nothing.
- [ ] `src/Hexalith.EventStore.Server/Streams/SourcePublicationAuthority.cs`: independently observe source/projector and source/receiver acknowledgements before conditional proof retention; verify current permission on issuance/reuse/recovery. No authority-owner turn spans external I/O; no callbacks into requesting namespace/index actors or originating aggregate registration path.
- [ ] `src/Hexalith.EventStore.Server/Configuration/SourcePublicationAuthorityServiceCollectionExtensions.cs`: explicit validated registration for producer, observer, registrar, reader and recovery roles; missing enrollment/current-epoch evidence fails closed.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Streams/SourcePublicationAuthorityTests.cs`, `Actors/AggregateActorCommandSourceTests.cs`, `tests/Hexalith.EventStore.DomainService.Tests/CompleteCommandSourceTests.cs`, `tests/Hexalith.EventStore.ServiceDefaults.Tests/Authentication/CommandSourceAuthorityTests.cs`, existing Client Streams suites: exercise matrix, tampering, incomplete prefixes, rollback, lost save/anchor acknowledgements, restart and poisoned predecessors; assert durable end-state.
- [ ] `docs/implementation/ext-conv-ai-1-eventstore-authority-handoff.md`: list actual providers, registration, Conversations verifier adapter requirement, public trust/enrollment/ACL/storage inputs, inventory qualification, exact local commands/results and remaining external deliveries. Distinguish independent authority storage/epoch from co-restored primary state. Leave installed hashes/targets unverified unless evidenced.

**Acceptance Criteria:**

- Given an enrolled command, when processed, then exact complete source evidence verifies without actor re-entry and stale comparison prevents append.
- Given an independently enrolled namespace, when catalogue/deletion discovery runs, then zero-Agent Conversations remain represented and only authenticated consecutive receipts advance recovery.
- Given absent installation or poison, when queried/restarted, then availability remains denied and no entry is skipped.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

For each `lane` in `Server DomainService ServiceDefaults Client`, execute:

```bash
dotnet build "tests/Hexalith.EventStore.${lane}.Tests/Hexalith.EventStore.${lane}.Tests.csproj" -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet "tests/Hexalith.EventStore.${lane}.Tests/bin/Debug/net10.0/Hexalith.EventStore.${lane}.Tests.dll" -class '*CommandSource*' -class '*SourcePublication*' -class '*DomainServiceWorkloadAssertion*' -class '*DaprDomainServiceInvoker*'
```

Require passing selected cases; retain exact commands/counts in the handoff. Use the repository fallback ladder for blocked broad checks. Run no Live owner command or cohort attempt.
