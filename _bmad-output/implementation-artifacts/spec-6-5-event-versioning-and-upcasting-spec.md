---
title: 'Story 6.5: Event Versioning And Upcasting Spec'
type: 'feature'
created: '2026-09-26'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 36
baseline_commit: 'ec67e340856a2db990db413021460e56f8ecd332'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stable `IEventContract.EventType` exists, but persisted events and consumers still use CLR names without a payload schema version. Identity checks and cancellation behavior differ across append, replay, projection, and subscription paths, leaving event evolution unsafe to implement without a frozen contract.

**Approach:** Produce `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` as the versioned AD-13 gate. Inventory the current paths, fix the future metadata, registry, upcast, identity, failure, cancellation, and migration contracts, then obtain content-bound human approval that explicitly authorizes Story 6.6. This story changes no runtime behavior.

## Boundaries & Constraints

**Always:** Specify exact field names, types, defaults, grammar, uniqueness, legacy version, and mapping for canonical kebab-case identity; distinguish metadata-envelope, domain-service, and payload versions. Preserve stored bytes, `MessageId`, sequence, correlation, protection metadata, and actor-owned commits. Require one shared allow-listed read pipeline and pre-append/pre-dispatch identity checks. Define numeric chain/payload limits, typed failures, checkpoint/last-good-state and cancellation/commit behavior, additive legacy adapters, mixed-version rollout/rollback, provider-portable evidence, support-safe diagnostics, and a closed 6.6 slicing decision.

**Never:** Implement 6.6, edit runtime/tests/public contracts, change `sprint-status.yaml` manually, rewrite history on read, guess unknown types, load arbitrary types, silently complete poison deliveries, claim AOT/trimming support, or depend on Epic 8 protection. Do not self-approve or authorize 6.6 without named human approval, date, exact digest, and explicit authorization.

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Contracts/Events/{IEventContract,EventMetadata,EventEnvelope}.cs`, `src/Hexalith.EventStore.Client/Events/EventContractResolver.cs` -- validated stable type, CLR name, missing payload version; preserve envelope version.
- `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs`, `src/Hexalith.EventStore.Server/Events/{EventPersister,EventPublisher,EventStreamReader}.cs` -- CLR-name wire/storage/publication; reader lacks envelope identity checks.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `src/Hexalith.EventStore.Server/Projections/{ProjectionStreamPageValidation,ProjectionEventWireBuilder,NamedProjectionDispatchCoordinator}.cs` -- protection, strict page identity, and durable completion boundaries.
- `src/Hexalith.EventStore.Client/Aggregates/{ApplyMethodResolver,AggregateReplayer,EventStoreProjection}.cs`, `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` -- divergent legacy resolution and partial replay.
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs`, `src/Hexalith.EventStore.Client/Registration/EventStoreDomainEventsServiceCollectionExtensions.cs` -- separate registry and terminal skip; distinguish scoped filtering from poison.
- `src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs`, `src/Hexalith.EventStore.DomainService/{EventStoreDomainServiceExtensions,IDomainProjectionHandler,IAsyncDomainProjectionHandler,LegacyDomainProjectionHandlerAdapter}.cs` -- token gaps and adapter pattern.
- `src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/EventTypeInfo.cs`, `src/Hexalith.EventStore.Testing/Builders/EventEnvelopeBuilder.cs` -- public evidence and fixture compatibility.

## Tasks & Acceptance

- [ ] Stories 6.5a, 6.5b, and 6.5c provide reviewed, bounded section candidates and documented dispositions for all ten open `BH37-*` findings.
- [ ] Reconcile their shared schemas, byte codecs, numeric budgets, identity and cancellation matrices, compatibility rules, rollout order, and verification vectors into the single normative AD-13 artifact.
- [ ] Confirm that no decision remains open, recompute the exact normative content digest, and obtain the named human approval receipt that explicitly authorizes Story 6.6. An unapproved or stale receipt keeps this story in progress and 6.6 unauthorized.

**Acceptance Criteria:**

- Given the three child-story outputs, when Story 6.5 integrates them, then every producer and consumer has one consistent contract, every `BH37-1` through `BH37-10` finding has a documented disposition, and no runtime behavior is changed by this story.
- Given completion is requested, when the artifact and six-field receipt are verified against the exact approved bytes and scope, then only valid named human approval completes Story 6.5 and authorizes Story 6.6; otherwise the gate remains closed while this story may stay in progress.

## Implementation Notes

The current AD-13 candidate is `spec-event-versioning-upcasting.md` and remains unapproved. Stories 6.5a–6.5c own focused specification work; this story owns integration, disposition of open findings, content-bound approval, and explicit authorization for Story 6.6. Their reviews alone grant no runtime authority.

Historical review material is retained verbatim after a short file header:

- [Review change log](story-6-5-review-change-log.md)
- [Review triage](story-6-5-review-triage.md)
- [Design notes](story-6-5-design-notes.md)

The latest review snapshot is v37 with `BH37-1` through `BH37-10` open. Its prior instruction to stop rederivation remains recorded in the historical log; this backlog split does not revise the normative candidate.

## Verification

**Commands:**
- `git diff --no-index --check /dev/null _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` -- expected: no whitespace errors (exit 1 means the new file differs).

**Manual checks (if no CLI):**
- Compare Story 6.5 in `epics.md` against the artifact; verify its approval digest and authorization before marking Story 6.5 done or Story 6.6 authorized in the tracker. The child-story backlog entries grant no approval.
