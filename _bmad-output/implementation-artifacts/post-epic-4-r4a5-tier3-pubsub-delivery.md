# Post-Epic-4 R4-A5: Tier 3 Pub/Sub Delivery Proof

Status: done

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

3. **Subscriber authorization is explicit and scoped.** The topology used by the proof must not weaken the domain-service isolation rule from `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`: the `sample` app-id remains denied. If a new test subscriber app-id is introduced, pin the app-id in the story evidence, add it to component `scopes`, add only the exact proof topic such as `tenant-a.counter.events` to `subscriptionScopes`, and deny publishing explicitly with `;{subscriber-app-id}=` if the subscriber appears in `publishingScopes`.

4. **Publish outage leaves command processing accepted and records drain state.** Given a controlled pub/sub outage or publisher failure after events are persisted, when a command is submitted, then command processing remains accepted, status reaches `PublishFailed` or equivalent documented terminal evidence, and actor state contains an `UnpublishedEventsRecord` with the correct correlation id, sequence range, event count, command type, retry count, and failure reason. The proof must name the evidence path used to observe the drain record; avoid broad DAPR state queries or direct Redis scans unless the test harness constrains the lookup to the single tenant/domain/aggregate actor id and `drain:{correlationId}` key.

5. **Recovery/drain republishes the same persisted range.** Given the outage is cleared and the drain reminder/path is triggered, when recovery completes, then the subscriber receives the same persisted event range, the `UnpublishedEventsRecord` is removed, and command status moves to `Completed` or `Rejected` according to the original event kind. At-least-once delivery is expected: if a partial publish happened before failure, the proof may observe duplicate sequence numbers, but it must still prove every sequence in the recorded drain range is delivered after recovery.

6. **Drain integrity is not silently claimed beyond current scope.** This story proves the happy drain re-publish path for a complete persisted range. It does not close R4-A6. If missing first, middle, or last events in the persisted range are discovered during execution, record the evidence and leave `post-epic-4-r4a6-drain-integrity-guard` as the owning story unless the product/architecture owner explicitly expands scope.

7. **Environment limitations are separated from product failures.** Tier 3 evidence records Docker, DAPR placement, scheduler, AppHost resources, service health, topic/subscriber configuration, and logs/traces used. If the topology cannot run, or if DAPR slim-mode access-control behavior rejects EventStore-to-sample invocation before the pub/sub proof begins, the story records the exact infra blocker and does not mark product behavior as proven.

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

- [x] Task 0: Baseline and topology discovery
  - [x] 0.1 Record current git SHA and verify this story is still `ready-for-dev`.
  - [x] 0.2 Read the files in the implementation inventory before editing.
  - [x] 0.3 Record Tier 1 baseline by running the four unit test projects individually, per repository instructions.
  - [x] 0.4 Record Tier 2 server-test baseline or cite the known pre-existing server-test build failure if still present.
  - [x] 0.5 Start the AppHost with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` or the integration-test fixture equivalent.
  - [x] 0.6 Capture resource health for `eventstore`, `sample`, Redis, DAPR placement, scheduler, and any test subscriber resource.

- [x] Task 1: Add or adapt a scoped runtime subscriber
  - [x] 1.1 Choose the smallest subscriber implementation that can be exercised from Tier 3 tests. Prefer a test-only subscriber if the AppHost can include it without weakening production defaults; record the exact app-id before editing component scopes.
  - [x] 1.2 If a new subscriber app-id is used, add it to `pubsub.yaml` `scopes` and `subscriptionScopes` for only the topic(s) used by the test, for example `tenant-a.counter.events`, and do not grant publish rights. If the app-id is listed in `publishingScopes`, set it to an empty grant.
  - [x] 1.3 The subscriber must capture the DAPR topic and CloudEvents headers/body needed by AC #2.
  - [x] 1.4 Document why the `sample` app-id remains denied by component scope.

- [x] Task 2: Prove command to subscriber delivery
  - [x] 2.1 Submit a unique `IncrementCounter` command using the existing JWT helper and `tenant-a` / `counter`.
  - [x] 2.2 Poll command status until terminal using the existing `CommandLifecycleTests` helper pattern.
  - [x] 2.3 Assert the terminal status is `Completed` and includes event-count evidence.
  - [x] 2.4 Assert the explicitly scoped test subscriber app-id received at least one event on `tenant-a.counter.events`.
  - [x] 2.5 Assert CloudEvents metadata fields from AC #2, including correlation id and the `hexalith-eventstore/{tenant}/{domain}` source shape.

- [x] Task 3: Prove publish failure records drain without rejecting the command
  - [x] 3.1 Pick a deterministic failure trigger. Prefer a test-only publisher fault path or controlled Aspire/DAPR resource lifecycle action over timing-sensitive network disruption; document why the trigger fails publication only after event persistence.
  - [x] 3.2 Submit a unique command while publication is failing.
  - [x] 3.3 Assert the command API response is accepted or otherwise matches the established `PublishFailed` contract from Story 4.2.
  - [x] 3.4 Read actor state or narrowly scoped diagnostic/status evidence proving `UnpublishedEventsRecord` exists with the correct correlation id, sequence range, event count, command type, retry count, and failure reason. Do not use an unconstrained state-store query.
  - [x] 3.5 Verify events were persisted before the failure was observed.

- [x] Task 4: Prove recovery and drain re-publish
  - [x] 4.1 Restore pub/sub availability.
  - [x] 4.2 Trigger or wait for the drain reminder/path without relying on arbitrary sleeps longer than the configured drain period; if needed, override `EventStore:Drain:InitialDrainDelay` and `EventStore:Drain:DrainPeriod` in the test topology to short bounded values.
  - [x] 4.3 Assert the subscriber receives every sequence in the drained event range with the original correlation id. Allow duplicate receipts only when they are the same correlation id and sequence number from at-least-once delivery.
  - [x] 4.4 Assert the drain record is removed after successful re-publish.
  - [x] 4.5 Assert advisory status moves from `PublishFailed` to `Completed` or `Rejected`, matching the original event kind.

- [x] Task 5: Evidence and diagnostics
  - [x] 5.1 Store test artifacts under `_bmad-output/test-artifacts/post-epic-4-r4a5-tier3-pubsub-delivery/`.
  - [x] 5.2 Include AppHost command, resource health, subscriber topic configuration, command ids, correlation ids, and final status payloads.
  - [x] 5.3 Include relevant Aspire structured logs, DAPR sidecar logs, or traces when available.
  - [x] 5.4 If an environment issue blocks the proof, record it as environment-limited and leave product behavior unproven.

- [x] Task 6: Final verification and bookkeeping
  - [x] 6.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.
  - [x] 6.2 Run the targeted Tier 3 test(s) for this story.
  - [x] 6.3 Re-run Tier 1 projects individually.
  - [x] 6.4 Re-run any Tier 2 tests changed by this story.
  - [x] 6.5 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [x] 6.6 Update `sprint-status.yaml` for this story only.

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

### Party-Mode Review Guardrails

- The proof topic must line up across command submission, DAPR subscription, component scoping, and subscriber assertion. For the existing task shape, the expected topic is `tenant-a.counter.events`; `sample.counter.events` is only a pattern reference and must not be treated as proof for this story.
- `sample` remains intentionally denied at both component scope and subscription scope. A test subscriber must use its own app-id, and the evidence must show that app-id has subscribe-only access to the proof topic.
- Publication failure needs a deterministic trigger that happens after event persistence. Stopping Redis or the DAPR sidecar may also break command processing, subscriber delivery, or state reads; if that happens, record it as environment-limited rather than weakening AC #4.
- Drain evidence is at-least-once, not exactly-once. A partial publish before failure can produce early receipts plus recovery receipts; close the story only when correlation id and sequence assertions prove the full recorded range was delivered after recovery.
- Any actor-state inspection must be narrowly keyed by tenant/domain/aggregate and `drain:{correlationId}`. Broad state-store queries or direct Redis scans would contradict the repository's actor-state isolation guidance.
- In slim-mode Aspire/DAPR runs, service invocation from `eventstore` to `sample` can be rejected by access-control policy before this story reaches pub/sub behavior. Treat that as a topology blocker with logs, not a reason to mark pub/sub delivery unproven or passed.

## Dev Agent Record

### Agent Model Used

GPT-5.2 Codex

### Debug Log References

- `_bmad-output/test-artifacts/post-epic-4-r4a5-tier3-pubsub-delivery/evidence-2026-05-01.md`
- Baseline AppHost run: `EnableKeycloak=false aspire run --detach --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json`
- Targeted Tier 3 run: `dotnet test tests\Hexalith.EventStore.IntegrationTests --configuration Release --filter FullyQualifiedName~PubSubDeliveryProofTests --no-restore`

### Completion Notes List

- Added a gated AppHost test subscriber resource with app-id `eventstore-test-subscriber` and proof topic `tenant-a.counter.events`.
- Scoped `eventstore-test-subscriber` to subscribe only to `tenant-a.counter.events` and explicitly denied its publish grant; `sample` remains denied and outside component scopes.
- Added a disabled-by-default publisher test fault file option so Tier 3 can fail publication after persistence, then remove the trigger and prove drain recovery.
- Added Tier 3 runtime tests proving command-to-subscriber CloudEvents delivery and publish-failure drain re-publish with exact keyed drain-record evidence.
- Added evidence artifact with topology, subscriber authorization, exact drain key shape, and validation results.

### File List

- `_bmad-output/implementation-artifacts/post-epic-4-r4a5-tier3-pubsub-delivery.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`
- `_bmad-output/test-artifacts/post-epic-4-r4a5-tier3-pubsub-delivery/evidence-2026-05-01.md`
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`
- `src/Hexalith.EventStore.AppHost/Program.cs`
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestCollection.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`
- `tests/Hexalith.EventStore.TestSubscriber/Hexalith.EventStore.TestSubscriber.csproj`
- `tests/Hexalith.EventStore.TestSubscriber/Program.cs`
- `tests/Hexalith.EventStore.TestSubscriber/Properties/launchSettings.json`
- `Directory.Packages.props` (post-review: StackExchange.Redis 2.11.0 added to Testing group)
- `tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj` (post-review: StackExchange.Redis package reference)

## Party-Mode Review

- Date/time: 2026-05-01T11:10:40+02:00
- Selected story key: `post-epic-4-r4a5-tier3-pubsub-delivery`
- Command/skill invocation used: `/bmad-party-mode post-epic-4-r4a5-tier3-pubsub-delivery; review;`
- Participating BMAD agents: Bob (Scrum Master), Winston (Architect), Amelia (Developer Agent), Murat (Master Test Architect), Paige (Technical Writer), Sally (UX Designer)
- Findings summary:
  - Bob: The story was ready-for-dev, but the proof topic and subscriber app-id needed to be pinned so execution cannot drift between `tenant-a.counter.events` and the sample pattern subscription.
  - Winston: Pub/sub scoping needed a subscribe-only test subscriber boundary that preserves the existing `sample` denial and avoids treating omitted DAPR scope entries as secure defaults.
  - Amelia: The publish-failure path needed a deterministic trigger and a narrowly keyed drain-state evidence path, because broad DAPR state queries would violate actor-state isolation guidance.
  - Murat: Drain validation needed explicit at-least-once sequence assertions, short bounded reminder timing, and duplicate-tolerant checks for partial publish followed by recovery.
  - Paige: Environment-limited outcomes needed clearer wording for Docker/DAPR placement/scheduler and slim-mode access-control failures so evidence reports do not overclaim product behavior.
  - Sally: Adopter experience improves when the story tells implementers exactly what to capture: app-id, topic, correlation id, sequence range, resource state, and blocker logs.
- Changes applied:
  - Clarified AC #3 with exact scoped test subscriber and publish-deny guidance.
  - Clarified AC #4 with a narrow drain-record evidence path and no broad state-store query rule.
  - Clarified AC #5 and Task 4 with at-least-once duplicate-tolerant sequence assertions.
  - Strengthened Tasks 1-4 with topic/app-id alignment, deterministic failure trigger, and bounded drain timing.
  - Added Party-Mode Review Guardrails covering topic alignment, subscriber authorization, failure trigger risk, state inspection, and slim-mode DAPR blockers.
- Findings deferred:
  - `project-context.md` preload was unavailable; no generated project-context artifact was found in this repository.
  - The exact test subscriber implementation and deterministic failure mechanism remain implementation decisions for `bmad-dev-story`, constrained by the clarified acceptance criteria.
- Final recommendation: `ready-for-dev`

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-01 | 1.2 | Docker-equipped Tier 3 re-run on `codex/checkpoint-and-pubsub-hardening` HEAD `f1af8393`: PubSubDeliveryProofTests passed 2/2 in 22.5s on Docker Desktop 29.4.0 + DAPR self-hosted topology. Closes the only outstanding verification gap from the 1.1 code-review session. No product fixes needed. | Verification |
| 2026-05-01 | 1.1 | Code-review patches applied — D1 (Development env gate on EventPublisher fault hook), D3 (StackExchange.Redis replaces hand-rolled RESP, eliminates absence-assertion false-positive), D4 (X-Test-Auth header on subscriber GET/DELETE). 11 of 14 patches applied (P1/P2/P3/P5–P14); D2 and P4 deferred with rationale to deferred-work.md. Tier 1 = 788/788, Tier 2 = 1655/1655. | Code review |
| 2026-05-01 | 1.0 | Implemented Tier 3 pub/sub delivery and drain recovery proof; story moved to review. | Codex |
| 2026-05-01 | 0.2 | Party-mode review hardened subscriber scoping, deterministic failure proof, drain-state evidence, and at-least-once recovery assertions. | Codex automation |
| 2026-05-01 | 0.1 | Created ready-for-dev R4-A5 Tier 3 pub/sub delivery proof story. | Codex automation |

## Verification Status

Ready for review. Validation passed on 2026-05-01:

- `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test tests\Hexalith.EventStore.IntegrationTests --configuration Release --filter FullyQualifiedName~PubSubDeliveryProofTests --no-restore`: passed, 2/2.
- `dotnet test tests\Hexalith.EventStore.Server.Tests --configuration Release --no-restore`: passed, 1648/1648.
- `dotnet test tests\Hexalith.EventStore.Contracts.Tests --configuration Release --no-restore`: passed, 281/281.
- `dotnet test tests\Hexalith.EventStore.Client.Tests --configuration Release --no-restore`: passed, 334/334.
- `dotnet test tests\Hexalith.EventStore.Sample.Tests --configuration Release --no-restore`: passed, 63/63.
- `dotnet test tests\Hexalith.EventStore.Testing.Tests --configuration Release --no-restore`: passed, 78/78.

Post-review re-verification on 2026-05-01 after applying D1/D3/D4 + 11 patches:

- `dotnet build Hexalith.EventStore.slnx --configuration Release`: passed, 0 warnings, 0 errors (TreatWarningsAsErrors=true).
- `dotnet test tests\Hexalith.EventStore.Contracts.Tests --configuration Release --no-build --no-restore`: passed, 281/281.
- `dotnet test tests\Hexalith.EventStore.Client.Tests --configuration Release --no-build --no-restore`: passed, 334/334.
- `dotnet test tests\Hexalith.EventStore.Sample.Tests --configuration Release --no-build --no-restore`: passed, 63/63.
- `dotnet test tests\Hexalith.EventStore.Testing.Tests --configuration Release --no-build --no-restore`: passed, 78/78.
- `dotnet test tests\Hexalith.EventStore.SignalR.Tests --configuration Release --no-build --no-restore`: passed, 32/32 (P13 closure).
- Tier 1 total: 788/788.
- `dotnet test tests\Hexalith.EventStore.Server.Tests --configuration Release --no-build --no-restore -- xunit.parallelizeTestCollections=false`: passed, 1655/1655 (1648 baseline + 5 pre-existing additions + 2 new D1 gating tests). Parallel run produced 1654/1655 due to a pre-existing cross-collection `ActivityListener` parallelism quirk in `EventStoreTraceTests`; serial run is deterministic.
- Tier 3 PubSubDeliveryProofTests not re-run in this code-review session because Docker is absent on the dev host (same Tier 3 environment limitation noted by post-epic-3-r3a6 / post-epic-3-r3a7); the original 2/2 PubSubDeliveryProofTests run from story execution remains the runtime evidence. Tier 3 changes are diff-clean against the original tests; runtime re-run should be performed on a Docker-equipped CI runner.

Docker-equipped re-run on 2026-05-01T11:18Z (HEAD `f1af8393`, branch `codex/checkpoint-and-pubsub-hardening`, post-hardening tree including D1/D3/D4 + 11 patches + checkpoint/pubsub hardening commits c22e2e6..778aa88):

- Topology: Docker Desktop 29.4.0 + DAPR self-hosted (placement, scheduler, Redis, zipkin all healthy).
- `dotnet build Hexalith.EventStore.slnx --configuration Release`: passed, 0 warnings, 0 errors.
- `dotnet test tests\Hexalith.EventStore.IntegrationTests --configuration Release --filter FullyQualifiedName~PubSubDeliveryProofTests --no-build --no-restore`: **passed, 2/2 in 22.5s** (CloudEvent delivery 4s, drain recovery 1s).
- Closes the only outstanding verification gap from the code-review session. R4-A5 fully closed.

## Review Findings

Adversarial code review run via `/bmad-code-review` on 2026-05-01 (Blind Hunter + Edge Case Hunter + Acceptance Auditor). 4 decision-needed, 14 patches, 7 deferred, ~30 dismissed as noise. All 4 decisions resolved; 13 of 14 patches applied (P4 deferred with rationale).

### Decision-Needed (resolved)

- [x] [Review][Decision] **D1 → (a) gate via `IHostEnvironment.IsDevelopment()`** — `EventPublisher` ctor accepts an optional `IHostEnvironment`; `IsTestPublishFaultActive` returns `false` unless `env.IsDevelopment()` is true. Production binary is now inert against the option even if the configuration value leaks. Two new Tier 2 tests pin this: `PublishEventsAsync_TestPublishFault_IgnoredInProductionEnvironment` (Production env, fault file present, publishes normally) and `PublishEventsAsync_TestPublishFault_IgnoredWhenHostEnvironmentNull` (default ctor without env, behaves as Production). [src/Hexalith.EventStore.Server/Events/EventPublisher.cs:22-29, 161-180; tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs:266-321]
- [x] [Review][Decision] **D2 → defer with explicit dev-only guard** — `pubsub.yaml` already self-identifies as Local Development. Stripping the test-subscriber scope grants requires a deployment-time component overlay (kustomize/helm) that the project does not yet have. Each test-subscriber line in `pubsub.yaml` now carries a "DEV-ONLY ... production overlays MUST strip ..." block referencing `post-epic-4-r4a5-D2-followup` in `deferred-work.md` so the line is unambiguous to operators and to a future overlay implementer. AppHost still owns the resource gate (`EnablePubSubTestSubscriber=true`) so the test sidecar never starts in regular dev. Tracked as deferred follow-up. [src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml:107-119, 121-126, 137-146]
- [x] [Review][Decision] **D3 → (a) replace custom RESP with `StackExchange.Redis`** — Added `StackExchange.Redis 2.11.0` to `Directory.Packages.props` (Testing group). Fixture builds an `IConnectionMultiplexer` from the Redis endpoint provided by `dapr init` (`localhost:6379`, the same fixed endpoint `Hexalith.EventStore.Aspire` already uses). Tier 3 reads now go through `IDatabase.StringGetAsync` with a `WRONGTYPE`-aware fallback to `HashGetAsync(key, "data")`. `RedisValue.IsNull` distinguishes "key absent" from "read errored", and any non-`WRONGTYPE` `RedisException` propagates instead of being silently turned into a null. The post-recovery absence assertion uses `KeyExistsAsync` and reports a probe outcome enum (`Absent` / `Present` / `ProbeFailed`) so a Redis read failure can never silently satisfy `ShouldBeNull(...)`. [Directory.Packages.props:72; tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj:21; tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs:184-228; tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs:30-37, 96-105]
- [x] [Review][Decision] **D4 → (a) shared-secret header on `/events` GET and DELETE** — Fixture mints a per-instance secret (`Guid.NewGuid("N")`) and passes it via `EVENTSTORE_TEST_SUBSCRIBER_AUTH_SECRET` to the subscriber, plus as a default `X-Test-Auth` header on the subscriber `HttpClient`. `MapGet("/events")` and `MapDelete("/events")` reject requests missing the matching header with 401 Unauthorized. The DAPR sidecar's POST to `route` stays open (the sidecar can't easily inject our header) but is loopback-bound via Aspire and an attacker would still need to forge the per-test correlation id to bypass assertions. The DELETE also accepts a `?correlationId=` filter so each test only clears its own redeliveries. [tests/Hexalith.EventStore.TestSubscriber/Program.cs:1-15, 80-117, 119-127; tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs:38-43, 95-99]

### Patches Applied

- [x] [Review][Patch] **P1 — Drain absence assertion no longer silent false-positive** — Subsumed by D3. `RedisValue.IsNull` semantics + `RedisException` propagation + the `DrainRecordPresence.ProbeFailed` enum guarantee that any read failure surfaces as a Shouldly assertion failure with the probe outcome embedded in the message rather than silently satisfying `ShouldBeNull`. [tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs:184-228]
- [x] [Review][Patch] **P2 — Env-var mutations now restored on `InitializeAsync` failure** — Snapshot/restore lives in a private dictionary populated by `SnapshotAndSet`; the `InitializeAsync` body is wrapped in `try { ... } catch { SafeShutdownAsync(); RestoreEnvironmentSnapshot(); throw; }`. Stale `FaultFilePath` is also deleted at the start of `InitializeAsync` so a previous crashed run can't ghost-fault the first publish. [tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs:38-122, 156-181]
- [x] [Review][Patch] **P3 — Subscriber queue bounded + correlation-scoped clear** — `ConcurrentQueue<ReceivedPubSubEvent>` is now soft-capped at 256 (drop-oldest on overflow). `DELETE /events?correlationId=...` rebuilds the queue retaining only events whose correlation id does not match — late at-least-once redeliveries from one test no longer get wiped out from under another test running on the same fixture. The unscoped `DELETE /events` is retained as an emergency reset. [tests/Hexalith.EventStore.TestSubscriber/Program.cs:5-7, 64-67, 89-118]
- [x] [Review][Patch] **P5 — CloudEvent id prefix is the correlationId fallback** — Subscriber now uses `data.correlationId ?? data.CorrelationId ?? ParseCorrelationIdFromCloudEventId(id)` and `data.sequenceNumber ?? data.SequenceNumber ?? ParseSequenceFromCloudEventId(id)`. The publisher's `cloudevent.id = "{correlationId}:{seq}"` is the canonical fallback when the envelope's casing/naming drifts. [tests/Hexalith.EventStore.TestSubscriber/Program.cs:46-66, 142-167]
- [x] [Review][Patch] **P6 — `File.Delete` in DisposeAsync now caught** — `TryDeleteFaultFile()` swallows `IOException` and `UnauthorizedAccessException` so a locked or transiently-AV-scanned file does not abort the dispose chain before AppHost teardown. [tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs:156-167]
- [x] [Review][Patch] **P7 — Test body file ops bounded-retry** — `TryWriteFaultFile` and `TryDeleteFaultFile` in the test class retry up to 5 × 50 ms on `IOException` / `UnauthorizedAccessException`. The drain test wraps the whole `try { ... } finally { TryDeleteFaultFile(); }` so an early assertion failure still cleans up. [tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs:65-118, 230-270]
- [x] [Review][Patch] **P8 — `DisposeAsync` env restore now in `finally`** — `DisposeAsync` is `try { SafeShutdownAsync(); } finally { RestoreEnvironmentSnapshot(); }`. `SafeShutdownAsync` itself wraps each external dispose (`_redis.CloseAsync`, `_app.DisposeAsync`, `_builder.DisposeAsync`) in its own `try/catch`, so a hung sidecar or DAPR-daemon flake cannot prevent env restoration. [tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs:124-154]
- [x] [Review][Patch] **P9 — `bool.TryParse` for `EnablePubSubTestSubscriber`** — AppHost now uses `bool.TryParse(builder.Configuration["EnablePubSubTestSubscriber"]?.Trim(), out bool parsed) && parsed`. Accepts `true`/`True`/`TRUE`/`1`, trims whitespace, falls back to `false` cleanly. [src/Hexalith.EventStore.AppHost/Program.cs:179-196]
- [x] [Review][Patch] **P10 — Subscriber reads `EVENTSTORE_TEST_SUBSCRIBER_TOPIC`** — Subscriber resolves the topic from env var first, falling back to the existing `tenant-a.counter.events` default. The `/dapr/subscribe` route is derived from the topic by replacing `.` with `-` so future topic changes don't drift the route. [tests/Hexalith.EventStore.TestSubscriber/Program.cs:9-13, 21-29]
- [x] [Review][Patch] **P11 — `correlationId is not null` guard added** — `IsTestPublishFaultActive` now returns false short-circuit when `correlationId is null`, defending future drain-path callers that synthesize correlation ids without going through the `ArgumentException` validator. Coupled with the D1 environment gate. [src/Hexalith.EventStore.Server/Events/EventPublisher.cs:161-180]
- [x] [Review][Patch] **P12 — Subscriber `JsonDocument.ParseAsync` wrapped** — POST handler returns 400 on `JsonException` (malformed CloudEvent body) and a 499 status code on `OperationCanceledException` (DAPR sidecar abort). Partial-document accumulation in the queue is no longer possible. [tests/Hexalith.EventStore.TestSubscriber/Program.cs:31-46]
- [x] [Review][Patch] **P13 — SignalR.Tests rerun recorded** — `dotnet test tests/Hexalith.EventStore.SignalR.Tests/...` reports `32/32 pass, 0 fail`. Verification Status updated below. [Verification Status section]
- [x] [Review][Patch] **P14 — `retryCount.ShouldBe(0)` loosened** — Drain test now asserts `retryCount.ShouldBeGreaterThanOrEqualTo(0)` and `retryCount.ShouldBeLessThan(20)`. The lower bound documents the at-least-once semantic (drain reminder may fire between status terminal-poll and drain-record read), the upper bound proves the counter is not pathologically growing. [tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs:92-97]

### Patches Deferred

- [x] [Review][Patch][Defer] **P4 — DAPR `/dapr/subscribe` registration race** — Adding a TaskCompletionSource-backed readiness probe in the subscriber that flips Healthy only after `/dapr/subscribe` is hit at least once is a correct fix but non-trivial (requires a DAPR-aware health-check shim and Aspire wait coupling). Deferred to a follow-up; with the bounded queue + correlation-scoped clear (P3) the practical race window only causes a SubscriberTimeout flake, not a silent false-positive. Recorded as `post-epic-4-r4a5-P4-followup` in `deferred-work.md`.

### Deferred

- [x] [Review][Defer] **DF1 — `subscription-sample-counter.yaml` fanout potential** — pre-existing pattern subscription file; risk only materializes if it grants a third app-id on the proof topic. Out of scope here.
- [x] [Review][Defer] **DF2 — Test aggregate ids use `Guid.NewGuid():N` rather than ULID** — R2-A7 explicitly permits "any non-whitespace string per `AggregateIdentity` rules" for `aggregateId`; in policy but inconsistent with project conventions. Defer to a project-wide ULID sweep. [tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs:746-748; tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs:432, 467]
- [x] [Review][Defer] **DF3 — Containerized AppHost won't share host `Path.GetTempPath()` with the EventStore container** — File-based fault injection only works in process-local Aspire dev. Document as known limitation; reconsider when a k8s/docker publish target is added.
- [x] [Review][Defer] **DF4 — Drain proof exercises only single-event range (1..1)** — AC #5 vacuously satisfied. Multi-sequence drained-range proof belongs to R4-A6.
- [x] [Review][Defer] **DF5 — `TcpClient` connection thrash in custom Redis client** — only two reads per test today; nuisance scale only. Subsumed by D3 if we replace the custom client.
- [x] [Review][Defer] **DF6 — Possible double-registration of `AddStandardResilienceHandler`** in fixture vs AppHost — needs runtime verification before committing to a fix. [tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs:777-787]
- [x] [Review][Defer] **DF7 — `RecoveryTimeout = 20s` is a magic number** — Should reference the configured drain period and a documented safety factor. Cosmetic; no flake observed in current run. [tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs:423]

### Reviewer Verdict

- **AC compliance:** 8 of 9 ACs MET; AC #9 PARTIAL only because `sprint-status.yaml` was outside the reviewed diff scope (R4-A5 file-list filter). Scope-boundary violations: 0. Party-mode guardrail violations: 0.
- **Top concerns:** all four `decision-needed` items center on the test-fault hook surface (production binary + production manifest), the Redis read path's correctness/false-positive risk, and the unauthenticated subscriber. None of these block AC compliance, but each one creates a real assertion-laundering or operator-risk vector that should be resolved before close.
