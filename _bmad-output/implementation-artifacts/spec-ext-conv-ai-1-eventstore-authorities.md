---
title: 'EXT-CONV-AI-1 EventStore source and publication authorities'
type: 'feature'
created: '2026-10-25'
status: 'in-review'
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

- [x] `src/Hexalith.EventStore.Contracts/Commands/CompleteCommandSource.cs`, `src/Hexalith.EventStore.ServiceDefaults/Authentication/CommandSourceAuthorityBinding.cs`: add distinct bounded native-source carrier and exact request binding; retain evolution rejection.
- [x] `src/Hexalith.EventStore.Server/Actors/AggregateActor.CommandSource.cs` and `AggregateActor.cs`: enroll exact scopes, replay complete immutable prefixes without snapshots, represent head zero explicitly, bind command/source bytes, and compare local metadata/native proof immediately before persistence. Retry recaptures authority.
- [x] `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` and workload handler; `src/Hexalith.EventStore.DomainService/CompleteCommandSourceVerifier.cs`: carry platform-created bindings; verify authenticated exact requests locally without source I/O. Missing issuer capability denies.
- [x] `src/Hexalith.EventStore.Server/Streams/SourcePublicationAuthority.cs`, `SourcePublicationAuthorityActor.cs`, corresponding contract files: implement both authority interfaces and independent durable enrollment, roster, anchor, admitted-original transition and immutable progress journals. Persist through actor state; expose exact recovery lookup.
- [x] `src/Hexalith.EventStore.Server/Streams/SourcePublicationNamespaceActor.cs`, `SourcePublicationIndexActor.cs` and Dapr transports: authenticate method/scope/expected revision/body using enrolled workload bindings; legacy uncredentialed calls deny. Independently authenticate inventory, all-writer enforcement and current enrollment; stored receipt strings grant nothing.
- [x] `src/Hexalith.EventStore.Server/Streams/SourcePublicationAuthority.cs`: independently observe source/projector and source/receiver acknowledgements before conditional proof retention; verify current permission on issuance/reuse/recovery. No authority-owner turn spans external I/O; no callbacks into requesting namespace/index actors or originating aggregate registration path.
- [x] `src/Hexalith.EventStore.Server/Configuration/SourcePublicationAuthorityServiceCollectionExtensions.cs`: explicit validated registration for producer, observer, registrar, reader and recovery roles; missing enrollment/current-epoch evidence fails closed.
- [x] `tests/Hexalith.EventStore.Server.Tests/Streams/SourcePublicationAuthorityTests.cs`, `Actors/AggregateActorCommandSourceTests.cs`, `tests/Hexalith.EventStore.DomainService.Tests/CompleteCommandSourceTests.cs`, `tests/Hexalith.EventStore.ServiceDefaults.Tests/Authentication/CommandSourceAuthorityTests.cs`, existing Client Streams suites: exercise matrix, tampering, incomplete prefixes, rollback, lost save/anchor acknowledgements, restart and poisoned predecessors; assert durable end-state.
- [x] `docs/implementation/ext-conv-ai-1-eventstore-authority-handoff.md`: list actual providers, registration, Conversations verifier adapter requirement, public trust/enrollment/ACL/storage inputs, inventory qualification, exact local commands/results and remaining external deliveries. Distinguish independent authority storage/epoch from co-restored primary state. Leave installed hashes/targets unverified unless evidenced.

**Acceptance Criteria:**

- Given an enrolled command, when processed, then exact complete source evidence verifies without actor re-entry and stale comparison prevents append.
- Given an independently enrolled namespace, when catalogue/deletion discovery runs, then zero-Agent Conversations remain represented and only authenticated consecutive receipts advance recovery.
- Given absent installation or poison, when queried/restarted, then availability remains denied and no entry is skipped.

## Implementation Notes

2026-10-25: Implemented the distinct complete-source carrier, exact RSA workload bindings and local SDK verifier; actor capture bypasses snapshots for enrolled sources and compares current authority/native metadata before persistence. Added the independently persisted authority journal, authenticated primary transports, conditional enrollment successors, immutable progress/receipt chains, poison preservation, and recovery across expired unmirrored issuance. The new ServiceDefaults test project is included in the modern solution. No dependencies or sibling repositories were changed by this build.

The shared branch advanced externally during implementation; preserve those commits and the original baseline above. No staging, commit, branch, push, checkout or submodule update was performed by this build. Pre-existing protection/notification work and concurrent unrelated AppHost/projection tests are excluded from this feature's review scope and remain preserved.

Four Debug/source-reference builds passed with zero warnings/errors. Executed selected cases: Server 88, DomainService 13, ServiceDefaults 3, Client 143; all 247 passed, zero failures/skips. Native command evidence is summarized in the installation handoff. These are component tests, with no installed authority or Live acceptance claim.

Matrix audit: command/tampering → CompleteCommandSourceTests and AggregateActorCommandSourceTests; stale/expiry → actor comparison and expired-cut generation cases; concurrency/restart → competing registrations and lost-command reply/restart cases; lost delivery/rollover → lost acknowledgement-proof and conditional successor cases; poison/foreign → durable quarantine and authenticated method/scope/epoch/role negatives. All belong to the selected classes and passed without skips.

Installation evidence/current-epoch verification and domain-specific source/receiver observation remain mandatory external bindings. The handoff identifies the Conversations adapter and provisional-empty catalogue boundary; no installed bindings, owner acceptance, Live qualification, Story 8.3 or RQ-1 attainment was fabricated.

## Spec Change Log

## Review Triage Log

2026-10-25 review: each reported outcome was checked against its named callers/guards; all confirmed entries below route to patch. Fixes preserve the approved intent and add no required public surface. Duplicate findings remain separately recorded.

| ID | Verdict / route | Evidence |
| --- | --- | --- |
| B01 | high / patch | WriteIndexAsync revalidates unchanged dispatch progress against a grown cut; its original source digest/offset cannot match. The isolated probe confirms publication-index-contended after an acknowledged source grows. |
| B02 | high / patch | The source check precedes awaited event preparation and protection leases; SaveStateAsync has no final source authority check. Revocation/expiry during preparation can commit; revalidate without clearing staged state. |
| B03 | high / patch | The actual /process endpoint rebuilds administrator extensions before the router hashes the received request. RebuildAsync strips or reorders those extensions, invalidating the original signed body. |
| B04 | high / patch | The independent journal accepts a first empty index without current roster/authority evidence or reconciliation; producer-signed admit/commit succeeds against a nonempty enrolled namespace. The journal must validate those semantics itself. |
| B05 | high / patch | Poison verification treats null ObserveAsync as a reference conflict, although null includes temporary unavailability. An unchanged source with a missing second observation can be permanently quarantined. |
| B06 | high / patch | Direct source/acknowledgement awaits do not use the constructed deadline or cancellation; a never-completing provider holds the primary actor turn indefinitely after client abandonment. |
| B07 | medium / patch | Renew-enrollment returns through PersistAsync before the common record/body capacity guard, allowing renewal records beyond the declared limit. |
| B08 | high / patch | Whole-state confirmation uses the 32 MiB bounded digest after SaveState, while raw journal bodies may reach 64 MiB before base64 expansion. The independent anchor can commit before confirmation becomes impossible. |
| B09 | medium / patch | CI explicitly enumerates unit-test projects and omits the new RSA project. Compile its tests into an existing configured test lane using the repository's shared-source pattern; preserve sealed CI workflow bytes. |
| B10 | medium / patch | Duplicated payloads can exceed ordinary transport limits before reaching the declared 16 MiB application-byte bound. Near-limit real transport verification and exact host/sidecar body-size installation guidance are missing. |
| E01 | high / patch | Same verified growth defect as B01: preserving historical progress must not make a new source cut impossible to reconcile. |
| E02 | medium / patch | AggregateIdentity permits a 386-character actor ID, but workload binding values are limited to 256; the full SourceClaim therefore rejects valid identities. Preserve exact joint identity binding within the existing bound. |
| E03 | high / patch | Same verified unbounded-observation defect as B06; creating a deadline without applying it to the awaited providers does not release the actor. |
| E04 | medium / patch | Same verified early-return capacity bypass as B07, applicable to repeated legitimate enrollment successors. |
| E05 | high / patch | Same verified serialized-state comparability defect as B08; preflight actual bounded serialization before staging or saving. |
| V01 | medium / patch | Regression gap: actor tests substitute InvokeCompleteSourceAsync and existing HTTP tests use ordinary process requests; deleting the new request-option assignment leaves all selected tests green. Add the concrete invoker/handler/receiver path. |
| V02 | medium / patch | Regression gap: successful verifier tests bypass the router, and the only new router case rejects missing evidence. No processor observes CompleteCommandSourceContext after an await; add that successful path. |


| R18 | medium / patch | RequiredDomains registration accepts noncanonical case/invalid identifiers (and lengths above AggregateIdentity's 64-character domain bound), but routing uses ordinal equality against canonical domains. Registering Conversation silently leaves conversation outside mandatory intake. Validate canonical domain identifiers at registration and cover refusal. |

## Verification

For each `lane` in `Server DomainService ServiceDefaults Client`, execute:

```bash
dotnet build "tests/Hexalith.EventStore.${lane}.Tests/Hexalith.EventStore.${lane}.Tests.csproj" -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet "tests/Hexalith.EventStore.${lane}.Tests/bin/Debug/net10.0/Hexalith.EventStore.${lane}.Tests.dll" -class '*CommandSource*' -class '*SourcePublication*' -class '*DomainServiceWorkloadAssertion*' -class '*DaprDomainServiceInvoker*'
```

Require passing selected cases; retain exact commands/counts in the handoff. Use the repository fallback ladder for blocked broad checks. Run no Live owner command or cohort attempt.
