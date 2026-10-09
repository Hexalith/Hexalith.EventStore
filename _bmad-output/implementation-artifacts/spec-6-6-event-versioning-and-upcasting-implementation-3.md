---
title: 'Story 6.6: Event Versioning And Upcasting Implementation'
type: 'feature'
created: '2026-10-09'
status: 'in-progress'
baseline_commit: '75a08f0069d8c2495d9dff20a0deb84edb6cc638'
route: 'dispatch'
review_loop_iteration: 0
story_key: '6-6-event-versioning-and-upcasting-implementation'
context:
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Versioned events are refused; identity checks and caller cancellation have gaps.

**Approach:** Retire dormant code; implement the owner-approved Story 6.6 design and AC1–AC8 in `epics.md` (2026-10-09), which govern this spec.

## Boundaries & Constraints

**Always:** Server stores and forwards `PayloadVersion`; clients upcast known types before deserialization. Version 1 remains unstamped. Use actor-owned Dapr storage; allow legacy GUID `MessageId` on read. Enforce AD-32 correlation and causation grammar on every read and new write, with no legacy exception for those fields (owner decision B: no history to keep). Failures advance no state, checkpoint or handler effect; subscriptions retry. Preserve live code when removal changes behavior.

**Never:** Rewrite history, add SQL, enable metadata V2 or `EventContractType`, remove the V2 fence, add proof/loader models or CI lanes, or delete ignored evidence. Admin views show stored data.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Legacy | Version 1 or absent | Stored bytes unchanged, version absent | GUID `MessageId` readable |
| Chain | Versions 1–3; optional rename | Contiguous steps once; identities unchanged | Gap, invalid version/JSON or upcaster fails typed/retryable |
| Identity | Malformed append/read | Reject before staging/apply | Name component; expose no payload |
| Cancellation | Caller token | Same token reaches user code | Distinct from invalid event |

</frozen-after-approval>

## Code Map

Path shorthand: `Contracts`, `Client`, `DomainService` and `Server` mean the matching `src/Hexalith.EventStore.*` project roots.

- Contracts `Events/EventMetadata.cs`, `Results/DomainServiceWireEvent.cs`, `Streams/StreamReadEvent.cs`; Client `Subscriptions/EventStoreDomainEventEnvelope.cs`: version carriers; add attribute/upcaster and validator in Contracts/Client.
- Contracts `Results/DomainServiceWireResult.cs`, DomainService `BoundedV1*`, Server `DomainServices/{BoundedV1DomainResponseParser,DaprDomainServiceInvoker}.cs`: stamp/parse versions; validate serialized declarations.
- Server `Events/{EventPersister,EventPublisher,EventStreamReader,LegacyEventReadGuard}.cs`, `Actors/AggregateActor.cs`, `Projections/ProjectionEventWireBuilder.cs`: relax V1 refusals, preserve V2 fence, validate identities.
- Client `Handlers/DomainProcessorStateRehydrator.cs`, `Aggregates/AggregateReplayer.cs`, `Subscriptions/EventStoreDomainEventProcessor.cs`; DomainService `DomainProjectionDispatcher.cs`: use one upcaster pipeline; reuse bounded JSON and `ApplyMethodResolver`; preserve unknown-type behavior.
- DomainService `IDomainProjectionHandler.cs`, `LegacyDomainProjectionHandlerAdapter.cs`, `EventStoreDomainServiceExtensions.cs`; Client `Registration/EventStoreServiceCollectionExtensions.cs`, `Aggregates/{EventStoreAggregate,AggregateCommandHandleMethod}.cs`: propagate caller tokens compatibly.
- Client `Registration/EventEvolutionServiceCollectionExtensions.cs` activates Server `DaprProductionLogicalEventReader` via `AggregateActor`; preserve this live opt-in group and document the cleanup exception.
- Server tests: round-trip committed actor state through JSON; existing helpers retain object references.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.EventStore.{Client,Server,DomainService,Contracts}/`, matching `tests/`, `scripts/{verify-dapr-logical-*,verify-event-evolution*,prepare-event-evolution-candidates*}`, `.github/workflows/{event-evolution-local-guards.yml,ci.yml}`: remove dormant groups/tests/guards, retain live code and obsolete public shells. Archive first-attempt 6.6 docs under `_bmad-output/implementation-artifacts/archive/story-6-6-first-attempt/` with the last containing commit; remove tracked `evidence/story-6-6/` from HEAD.
- [x] Contracts/Client/DomainService/Server files in Code Map: implement version declaration/carriers, discovery and explicit registration, startup validation, shared read pipeline and V1/V2 fences on every read path.
- [x] Contracts `Events/` and Server append/read plus Client subscription paths: validate identity before staging/apply; apply strict AD-32 reads and writes.
- [x] Projection/registration/aggregate files in Code Map: add token default method, keyed async registration and preferred Handle token overload.
- [x] `tests/Hexalith.EventStore.{Contracts,Client,DomainService,Server}.Tests/`, `docs/concepts/event-versioning.md`: cover AC1–AC7, invalid chains, retry/no-effect, JSON actor round-trip and deployment order; document Dapr retry/dead-letter configuration.

**Acceptance Criteria:**

- Given V1/V3 event types, when written, then V1 remains unstamped and V3 carries `PayloadVersion=3` through wire, storage, publication, stream, projection and subscription.
- Given mixed known history, when command rehydration (including snapshots), replay, `/project`, `/project/v2` or subscription reads it, then steps run once in order before deserialization; rename resolves and effective state matches current-version history without changing stored bytes or IDs.
- Given duplicate/invalid/dangling/incomplete registration, when starting, then fail with type/version; given a gap, future version, bad payload or throwing/null upcaster, when reading, then fail typed and support-safe with no effect or checkpoint advance; subscription returns retryable HTTP 503.
- Given malformed identity, when appending/reading, then fail with named component before staging/apply; only legacy GUID `MessageId` remains readable.
- Given a live caller token, when legacy projection, keyed async processor or aggregate Handle runs, then user code gets that token; existing sync contracts still compile.
- Given a real actor write/read test, when V1 history and a V2 write round-trip through JSON state and domain routing, then persisted/published envelopes, V1 bytes and rehydrated state match expectations.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Observed (2026-10-09):** Release solution build succeeded with zero warnings/errors. Contracts: 2,308 passed, 2 skipped (package inventory environment variable absent). Client: 1,448 passed. DomainService: 523 passed. Server: 4,279 passed, 25 skipped. `git diff --cached --check` passed. The full Contracts test run initially hit a transient executable-file lock while other suites ran; the affected test passed directly, and the isolated full rerun passed.

**Matrix audit:** The actor JSON round-trip and registry tests cover legacy unstamped bytes and ordered chains; registry and projection tests cover rename, invalid versions, failed upcasters and no handler effect; Contracts identity tests cover named components and legacy GUID reads; projection, keyed processor and aggregate tests cover caller-token forwarding. These covering tests ran in the successful project suites above.

**Commands:**

- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` — no warnings/errors.
- `for p in Contracts Client DomainService Server; do dotnet test tests/Hexalith.EventStore.$p.Tests/Hexalith.EventStore.$p.Tests.csproj --configuration Release || break; done` — each affected project passes; use built xUnit v3 assemblies for focused filters.
- `git diff --check` — no whitespace errors; existing CI contains no retired evolution jobs.
