# Post-Epic-4 R4-A5: Tier 3 Pub/Sub Delivery Proof

Status: ready-for-dev

<!-- Source: epic-4-retro-2026-04-26.md R4-A5 -->
<!-- Source: sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md Proposal 5 -->
<!-- Source: sprint-change-proposal-2026-04-28.md Proposal 5 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform maintainer responsible for runtime event distribution evidence**,
I want Tier 3 coverage proving command submission through DAPR pub/sub subscriber delivery and drain re-publish in a running Aspire topology,
So that Epic 4's mocked publisher and drain tests are backed by real runtime evidence before later security, observability, and operations stories rely on pub/sub delivery claims.

## Story Context

Epic 4 closed with strong unit and mocked-integration evidence for CloudEvents publication, tenant-domain topic derivation, persist-then-publish ordering, and drain retry mechanics. It did not prove that a running AppHost topology can deliver a published event through Redis-backed DAPR pub/sub to an actual subscriber, nor that a publish failure can leave command processing accepted and later re-publish the same persisted event range after recovery.

This story closes R4-A5 from the Epic 4 retrospective. It is deliberately a **Tier 3 evidence story**, not a source refactor. Story 4.1 already verifies `EventPublisher` CloudEvents metadata and topic naming through mocked `DaprClient` calls. Story 4.2 already verifies `UnpublishedEventsRecord`, reminders, retry count, and re-publish logic against mocked actor state and `IEventPublisher`. Story 4.3 records that live Tier 3 backpressure verification was deferred to this story. R4-A2b already resolved the dead `IBackpressureTracker` DI gap and left actor-level Step 2b as the backpressure enforcement path.

The execution agent must build the smallest reliable Tier 3 proof for the real DAPR path:

1. command API accepts a command;
2. aggregate actor invokes the sample counter domain service;
3. events are persisted;
4. `EventPublisher` publishes CloudEvents to `{tenant}.{domain}.events`;
5. an authorized subscriber receives the CloudEvents envelope and can assert metadata;
6. a controlled publish failure records `UnpublishedEventsRecord` while command processing remains accepted;
7. recovery/drain re-publishes the same persisted event range and clears the drain record.

## Acceptance Criteria

1. **Running topology proves command to subscriber delivery.** Given the Aspire AppHost is running with DAPR placement, scheduler, Redis state store, and Redis pub/sub, when a valid sample counter command is submitted through the EventStore command API, then the test observes command acceptance, event persistence evidence, publication completion, and receipt by a subscriber on the exact `{tenant}.{domain}.events` topic.

2. **Subscriber asserts CloudEvents metadata, not just payload receipt.** The subscriber proof captures and asserts at minimum: CloudEvents `type`, `source`, `id`, topic, tenant, domain, aggregate id or stream identity, and correlation id. The event payload may be inspected only enough to prove it is the expected sample counter event; do not log raw payload bytes or secrets.

3. **Subscriber authorization is explicit and scoped.** The topology used by the proof must not weaken the domain-service isolation rule from `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`: the `sample` app-id remains denied. If a new test subscriber app-id is introduced, it must be added to component `scopes` and `subscriptionScopes` only for the test topic(s), and it must not receive publish rights.

4. **Publish outage leaves command processing accepted and records drain state.** Given a controlled pub/sub outage or publisher failure after events are persisted, when a command is submitted, then command processing remains accepted, status reaches `PublishFailed` or equivalent documented terminal evidence, and actor state contains an `UnpublishedEventsRecord` with the correct correlation id, sequence range, event count, command type, retry count, and failure reason.

5. **Recovery/drain republishes the same persisted range.** Given the outage is cleared and the drain reminder/path is triggered, when recovery completes, then the subscriber receives the same persisted event range, the `UnpublishedEventsRecord` is removed, and command status moves to `Completed` or `Rejected` according to the original event kind.

6. **Drain integrity is not silently claimed beyond current scope.** This story proves the happy drain re-publish path for a complete persisted range. It does not close R4-A6. If missing first, middle, or last events in the persisted range are discovered during execution, record the evidence and leave `post-epic-4-r4a6-drain-integrity-guard` as the owning story unless the product/architecture owner explicitly expands scope.

7. **Environment limitations are separated from product failures.** Tier 3 evidence records Docker, DAPR placement, scheduler, AppHost resources, service health, topic/subscriber configuration, and logs/traces used. If the topology cannot run, the story records the exact infra blocker and does not mark product behavior as proven.

8. **Existing lower-tier guarantees remain green.** Tier 1 unit test projects remain unchanged in count and pass. Tier 2 `Hexalith.EventStore.Server.Tests` remains at the pre-story baseline plus any deliberate new mocked helper tests added for this story. Tier 3 additions are isolated to `tests/Hexalith.EventStore.IntegrationTests` or an equivalent test-support project.

9. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and both `last_updated` fields in `sprint-status.yaml` name R4-A5 and the result. At code-review signoff, both become `done`. Do not touch R4-A6 or R4-A8 status rows.

## Scope Boundaries

- Do not refactor `EventPublisher`, `AggregateActor`, or DAPR component files unless the Tier 3 proof exposes a real defect.
- Do not make the `sample` domain service a subscriber. It is intentionally blocked by component scopes and `subscriptionScopes`.
- Do not absorb R4-A6 drain integrity guard work. Missing-event detection is a separate story.
- Do not absorb R4-A8 story-numbering comment cleanup.
- Do not change Keycloak behavior. Tier 3 contract tests may continue using `EnableKeycloak=false` and symmetric JWTs as the existing fixture does.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| AppHost topology | `src/Hexalith.EventStore.AppHost/Program.cs` | Resource names, DAPR app ids, sample service, Redis state/pubsub wiring |
| Local pub/sub component | `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` | Component scopes plus `publishingScopes` and `subscriptionScopes` |
| Pattern subscription | `src/Hexalith.EventStore.AppHost/DaprComponents/subscription-sample-counter.yaml` | Pattern reference only; do not activate `sample` as a subscriber |
| Publisher | `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` | CloudEvents metadata and `PublishEventAsync` behavior |
| Actor pipeline | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | Persist-then-publish path, `PublishFailed`, drain record, reminder, drain re-publish |
| Drain record | `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs` | `drain:{correlationId}` state key and reminder name |
| Current Tier 3 fixture | `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs` | AppHost startup with `EnableKeycloak=false` and healthy resource waits |
| Command lifecycle tests | `tests/Hexalith.EventStore.IntegrationTests/ContractTests/CommandLifecycleTests.cs` | Existing command submit/poll helper shape |
| Mocked publisher tests | `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs` | Metadata expectations to mirror in runtime proof |
| Mocked drain tests | `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` and `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs` | Existing drain behavior baseline |

## Tasks / Subtasks

- [ ] Task 0: Baseline and topology discovery
  - [ ] 0.1 Record current git SHA and verify this story is still `ready-for-dev`.
  - [ ] 0.2 Read the files in the implementation inventory before editing.
  - [ ] 0.3 Record Tier 1 baseline by running the four unit test projects individually, per repository instructions.
  - [ ] 0.4 Record Tier 2 server-test baseline or cite the known pre-existing server-test build failure if still present.
  - [ ] 0.5 Start the AppHost with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` or the integration-test fixture equivalent.
  - [ ] 0.6 Capture resource health for `eventstore`, `sample`, Redis, DAPR placement, scheduler, and any test subscriber resource.

- [ ] Task 1: Add or adapt a scoped runtime subscriber
  - [ ] 1.1 Choose the smallest subscriber implementation that can be exercised from Tier 3 tests. Prefer a test-only subscriber if the AppHost can include it without weakening production defaults.
  - [ ] 1.2 If a new subscriber app-id is used, add it to `pubsub.yaml` `scopes` and `subscriptionScopes` for only the topic(s) used by the test, and do not grant publish rights.
  - [ ] 1.3 The subscriber must capture the DAPR topic and CloudEvents headers/body needed by AC #2.
  - [ ] 1.4 Document why the `sample` app-id remains denied by component scope.

- [ ] Task 2: Prove command to subscriber delivery
  - [ ] 2.1 Submit a unique `IncrementCounter` command using the existing JWT helper and `tenant-a` / `counter`.
  - [ ] 2.2 Poll command status until terminal using the existing `CommandLifecycleTests` helper pattern.
  - [ ] 2.3 Assert the terminal status is `Completed` and includes event-count evidence.
  - [ ] 2.4 Assert the subscriber received at least one event on `tenant-a.counter.events`.
  - [ ] 2.5 Assert CloudEvents metadata fields from AC #2, including correlation id and the `hexalith-eventstore/{tenant}/{domain}` source shape.

- [ ] Task 3: Prove publish failure records drain without rejecting the command
  - [ ] 3.1 Pick a deterministic failure trigger. Prefer a test-only pub/sub outage controlled through Aspire resource lifecycle or a test publisher fault path over timing-sensitive network disruption.
  - [ ] 3.2 Submit a unique command while publication is failing.
  - [ ] 3.3 Assert the command API response is accepted or otherwise matches the established `PublishFailed` contract from Story 4.2.
  - [ ] 3.4 Read actor state or status evidence proving `UnpublishedEventsRecord` exists with the correct correlation id, sequence range, event count, command type, retry count, and failure reason.
  - [ ] 3.5 Verify events were persisted before the failure was observed.

- [ ] Task 4: Prove recovery and drain re-publish
  - [ ] 4.1 Restore pub/sub availability.
  - [ ] 4.2 Trigger or wait for the drain reminder/path without relying on arbitrary sleeps longer than the configured drain period.
  - [ ] 4.3 Assert the subscriber receives the drained event range with the original correlation id.
  - [ ] 4.4 Assert the drain record is removed after successful re-publish.
  - [ ] 4.5 Assert advisory status moves from `PublishFailed` to `Completed` or `Rejected`, matching the original event kind.

- [ ] Task 5: Evidence and diagnostics
  - [ ] 5.1 Store test artifacts under `_bmad-output/test-artifacts/post-epic-4-r4a5-tier3-pubsub-delivery/`.
  - [ ] 5.2 Include AppHost command, resource health, subscriber topic configuration, command ids, correlation ids, and final status payloads.
  - [ ] 5.3 Include relevant Aspire structured logs, DAPR sidecar logs, or traces when available.
  - [ ] 5.4 If an environment issue blocks the proof, record it as environment-limited and leave product behavior unproven.

- [ ] Task 6: Final verification and bookkeeping
  - [ ] 6.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.
  - [ ] 6.2 Run the targeted Tier 3 test(s) for this story.
  - [ ] 6.3 Re-run Tier 1 projects individually.
  - [ ] 6.4 Re-run any Tier 2 tests changed by this story.
  - [ ] 6.5 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [ ] 6.6 Update `sprint-status.yaml` for this story only.

## Dev Notes

### Architecture Guardrails

- Topic derivation is `AggregateIdentity.PubSubTopic`: `{tenant}.{domain}.events`.
- `eventstore` publishes dynamically; do not replace this with a fixed-topic list because dynamic tenant provisioning requires new tenant topics without restart.
- DAPR scoping is deny-by-default only for app ids explicitly listed with an empty grant. Apps omitted from `publishingScopes` or `subscriptionScopes` may be unrestricted; do not misread omission as denial.
- The `sample` app-id is intentionally denied pub/sub access in local AppHost config. A subscriber proof must use an explicitly authorized subscriber app-id, not the domain service.
- Application code does not implement custom retry loops for pub/sub. DAPR resiliency handles transient publish failures; application drain handles prolonged failures after DAPR reports failure.
- Drain re-publish is at-least-once. Duplicate subscriber receipts during recovery are acceptable if the same persisted event range is re-delivered; subscriber idempotency remains the consumer responsibility.
- R4-A6 owns missing-event-range integrity. This story may report evidence, but it should not silently broaden into data-integrity policy work.

### Previous Story Intelligence

- Story 4.1 verified CloudEvents publication and topic routing with mocked `DaprClient`; use its metadata expectations as the runtime assertion contract.
- Story 4.2 verified drain record and reminder behavior with mocked actor state; use it as the state shape contract for `UnpublishedEventsRecord`.
- Story 4.3 recorded the live Tier 3 backpressure caveat and routed it to this story; do not leave this story with only mocked proof.
- R4-A2b removed the unused pipeline-level `IBackpressureTracker`; actor-level Step 2b remains the enforcement path and the two `BackpressureExceededException` classes remain intentionally separate.

### Testing Notes

- Use existing `AspireContractTestFixture` conventions unless the subscriber needs a dedicated fixture.
- Keep Tier 3 test names explicit, for example `PubSubDelivery_CommandPublished_SubscriberReceivesCloudEvent` and `PubSubDrain_PublishFailsThenRecovers_DrainsPersistedEvents`.
- Avoid long arbitrary sleeps. Prefer health checks, status polling, resource notifications, or bounded polling with diagnostic output.
- Capture every correlation id used in assertions; those ids are the fastest way to connect API response, status record, subscriber receipt, traces, and drain state.
- If Docker, DAPR placement, or scheduler is unavailable, stop and record the exact blocker rather than weakening the acceptance criteria.

## Dev Agent Record

### Agent Model Used

To be filled by dev agent.

### Debug Log References

To be filled by dev agent.

### Completion Notes List

To be filled by dev agent.

### File List

To be filled by dev agent.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-01 | 0.1 | Created ready-for-dev R4-A5 Tier 3 pub/sub delivery proof story. | Codex automation |

## Verification Status

Story creation only. Runtime, build, and Tier 3 tests are intentionally deferred to `bmad-dev-story`.
