---
title: 'Story 6.6: Event Versioning And Upcasting Implementation'
type: 'feature'
created: '2026-10-09'
status: 'in-progress'
baseline_commit: '75a08f0069d8c2495d9dff20a0deb84edb6cc638'
route: 'dispatch'
review_loop_iteration: 1
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
- Server `DomainServices/{BoundedV1DomainResponseParser,PendingV1WireEvent,DaprDomainServiceInvoker}.cs` and `Events/EventPersister.cs`: admit and retain a standalone numeric V1 `PayloadVersion` (1–1024) through the real bounded HTTP response and serialized wrapper. Keep the V2 `EventContractType` fence. A wire wrapper has no event-version declaration of its own; do not compare its CLR version 1 to the supplied event version or relabel the supplied version. Require JSON for a versioned serialized payload.
- Client `Events/{EventPayloadEvolutionRegistry,EventLogicalViewResolver}.cs` and registration: distinguish registered Apply/projection/subscriber event types from unrelated payload classes in a scanned assembly. Resolve short/full/alias names symmetrically for upcaster lookup; a recognized historical alias remains known when a particular version step is missing. Prefer a registered rename step over an old CLR type's terminal version, turn ambiguous lookup into a typed safe failure, and reject malformed current-version JSON before a projection handler can report completion. The live opt-in Dapr logical reader must accept stamped metadata V1 as a stored source and hand it to the JSON upcast path without activating metadata V2 or losing protection/digest checks.
- Server `Events/EventPublisher.cs` and `Projections/ProjectionEventWireBuilder.cs`, Client `Subscriptions/EventStoreDomainEventProcessor.cs`: add positive boundary tests for a V2 stamped publication, projection DTO version propagation into `/project` and `/project/v2`, and a renamed V1 subscription that reaches the current handler without completing a marker as unknown.

## Tasks & Acceptance

**Execution:**

- [ ] `src/Hexalith.EventStore.{Client,Server,DomainService,Contracts}/`, matching `tests/`, `scripts/{verify-dapr-logical-*,verify-event-evolution*,prepare-event-evolution-candidates*}`, `.github/workflows/{event-evolution-local-guards.yml,ci.yml}`: remove dormant groups/tests/guards, retain live code and obsolete public shells. Archive first-attempt 6.6 docs under `_bmad-output/implementation-artifacts/archive/story-6-6-first-attempt/` with the last containing commit; remove tracked `evidence/story-6-6/` from HEAD.
- [ ] Contracts/Client/DomainService/Server files in Code Map: implement version declaration/carriers, discovery and explicit registration, startup validation, shared read pipeline and V1/V2 fences on every read path, including the real bounded domain-response and serialized-wrapper path.
- [ ] Contracts `Events/` and Server append/read plus Client subscription paths: validate identity before staging/apply; apply strict AD-32 reads and writes.
- [ ] Projection/registration/aggregate files in Code Map: add token default method, keyed async registration and preferred Handle token overload.
- [ ] `tests/Hexalith.EventStore.{Contracts,Client,DomainService,Server}.Tests/`, `docs/concepts/event-versioning.md`: cover AC1–AC7, invalid chains, retry/no-effect, JSON actor round-trip and deployment order; document Dapr retry/dead-letter configuration. Include positive tests at the real parser/persister, publisher, projection wire and subscriber handler boundaries.

**Acceptance Criteria:**

- Given V1/V3 event types, when written, then V1 remains unstamped and V3 carries `PayloadVersion=3` through wire, storage, publication, stream, projection and subscription.
- Given mixed known history, when command rehydration (including snapshots), replay, `/project`, `/project/v2` or subscription reads it, then steps run once in order before deserialization; rename resolves and effective state matches current-version history without changing stored bytes or IDs.
- Given duplicate/invalid/dangling/incomplete registration, when starting, then fail with type/version; given a gap, future version, bad payload or throwing/null upcaster, when reading, then fail typed and support-safe with no effect or checkpoint advance; subscription returns retryable HTTP 503.
- Given malformed identity, when appending/reading, then fail with named component before staging/apply; only legacy GUID `MessageId` remains readable.
- Given a live caller token, when legacy projection, keyed async processor or aggregate Handle runs, then user code gets that token; existing sync contracts still compile.
- Given a real actor write/read test, when V1 history and a V2 write round-trip through JSON state and domain routing, then persisted/published envelopes, V1 bytes and rehydrated state match expectations.

## Implementation Notes

KEEP from the reviewed first attempt: the frozen intent and baseline; targeted retirement with archived first-attempt documents and last-containing commits; live opt-in Dapr reader and required public compatibility shells; version carriers, immutable JSON upcaster registry, strict identity checks, cancellation seams and the deployment guide. Preserve the exact-path Story 8.3 header-name scanner exemption, repaired ULID fixtures and deterministic invalid NuGet sample. Preserve meaningful V1/V2 actor JSON, ordered-chain, rename, projection, retry/no-effect and caller-token tests. The prior Release build and four project suites were green before review; re-derived code must recover those gates and add the missing real wire coverage.

## Spec Change Log

- 2026-10-09 review iteration 1: Blind 1–4, 6–11, Edge 1–2 and 4–5, and Gap 1–3 found that the first implementation's mocked actor test hid bounded-parser and serialized-wrapper failures, while registry alias/known-type and positive boundary coverage were incomplete. Expanded the non-frozen Code Map and task verification to require the real response-to-persistence path, the live logical reader, correct known-type and alias rules, JSON fail-closed behavior, and publisher/projection/subscriber boundary tests. The known-bad state is a green unit suite that still rejects or erases V2 events at production seams. KEEP the positive work listed in Implementation Notes; retain all frozen intent and V2 fences.

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Blind 1: bounded parser refuses V1 payload version | high | `BoundedV1DomainResponseParser.EventFieldAsync` throws on every non-null `payloadVersion`; `DaprDomainServiceInvoker.InvokeAsync` always uses this parser. The production writer cannot receive a versioned result. Keep; bad_spec. |
| Blind 2: bounded parser drops payload version | high | `ParseAsync` constructs `DomainServiceWireEvent` with only `MetadataVersion`, so even a relaxed admission would erase the version before the serialized wrapper. Keep; bad_spec. |
| Blind 3: persister compares wrapper version | high | `ToDomainResult` creates `SerializedDomainEventPayload`; `EventPersister` compares its undeclared CLR version 1 with supplied version 2 and then resets the stamp to null. Keep; bad_spec. |
| Blind 4: opt-in logical reader rejects stamped V1 | medium | `EventLogicalViewResolver.ResolveSource` accepts V1 only when `payloadVersion` is null. The live `AggregateActor` production-reader path calls it before client rehydration, so a stamped event is refused. Keep; bad_spec. |
| Blind 5: generic processor registration omits discovery | false | `AddEventStoreClient<TProcessor>` registers a processor, while `AddEventStore` is the aggregate/projection scan and `AddEventStoreDomainEvents` scans subscriber contracts. The design does not promise an upcaster scan from the generic processor-only registration. Reject. |
| Blind 6: unrelated payload types count as known | medium | `EventPayloadEvolutionRegistration.AddAssembly` adds every concrete `IEventPayload`; a shared assembly's unused versioned type can fail startup although no Apply, projection handler or subscription registers it. Keep; bad_spec. |
| Blind 7: stored short-name upcaster mismatch | medium | `TryResolveType` accepts an exact short type name but `FindStep` calls one-way `NameMatches(storedName, registeredFullName)`, which does not match that name. A known old event fails its valid chain. Keep; bad_spec. |
| Blind 8: old registered type skips rename | medium | `Read` returns as soon as the old CLR type's declared version matches, before checking a registered rename step at that version. The target handler never sees the rename. Keep; bad_spec. |
| Blind 9: ambiguous step escapes typed failure | medium | `FindStep` throws `InvalidOperationException` before the upcaster try/catch; subscription catches only `EventPayloadEvolutionException`. An ambiguous stored alias escapes the retryable disposition. Keep; bad_spec. |
| Blind 10: stamped non-JSON serialized event | high | `EventPersister` validates the numeric version but has no JSON guard for a serialized event, while the new registry parses JSON steps. A versioned binary event can be stored unreadably. Keep; bad_spec. |
| Blind 11: actor round-trip omits wire conversion | medium | The actor test mocks `IDomainServiceInvoker` and calls routing separately; it does not exercise bounded response parsing or `ToDomainResult`, so it missed Blind 1–3. Keep; bad_spec. |
| Blind 12: second AddEventStore scan | false | `AddEventStoreCore` already treats a second call as idempotent and skips aggregate/projection discovery. The new evolution scan follows that existing one-call contract; no newly promised second-call scan is shown. Reject. |
| Edge 1: serialized wrapper rejects version 2 | high | The same real `ToDomainResult` to `EventPersister` path as Blind 3 rejects a supplied V2 stamp against the wrapper's version 1. Keep; bad_spec, grouped with Blind 3. |
| Edge 2: historical alias with missing version is unknown | high | `Read` tests only `FindStep(name, storedVersion)` before deciding a name is unknown. A recognized alias whose step exists at another version returns unknown; subscription then completes its marker. Keep; bad_spec. |
| Edge 3: intermediate rename target rejected | false | AC4 explicitly requires each `TargetEventTypeName` to resolve to a known event type. An unregistered intermediate name is not a valid chain under this design. Reject. |
| Edge 4: ambiguous resolution uncategorized | medium | `TryResolveType` and `FindStep` throw `InvalidOperationException` outside a typed evolution failure; replay and subscription do not consistently translate it. Keep; bad_spec, grouped with Blind 9. |
| Edge 5: malformed current projection event passes | medium | For a known type already at current version, `Read` returns raw bytes and `UpcastRequest` forwards them without JSON validation. A handler that ignores payload can report completion for malformed JSON. Keep; bad_spec. |
| Gap 1: versioned publisher positive path untested | medium | `EventPublisherTests` cover V2 rejection and unstamped success but never assert that metadata V1 with `PayloadVersion=2` reaches `PublishEventAsync` retaining the stamp. Pre-verified gap; keep, bad_spec. |
| Gap 2: projection wire version untested | medium | Builder tests do not assert `StoredPayloadVersion`; dispatcher tests construct DTOs directly. Removing the copy would cause current V2 events to be upcast again. Pre-verified gap; keep, bad_spec. |
| Gap 3: subscriber rename/upcast untested | medium | Processor tests do not deliver a legacy name with a registry step. Without the registry read, the processor can mark it unknown and complete its marker. Pre-verified gap; keep, bad_spec. |

The surviving parser, persister, logical-reader, registry and boundary-test groups are all defects in this story's implementation. The parser and persister findings have distinct failure points; only the noted duplicate findings share root causes. They route to `bad_spec` because a full production path and the known-type/alias rules need to be explicit in the non-frozen implementation instructions before re-derivation. No surviving finding is deferred.

## Verification

**First attempt observed (2026-10-09, before review loopback):** Release solution build succeeded with zero warnings/errors. Contracts: 2,308 passed, 2 skipped (package inventory environment variable absent). Client: 1,448 passed. DomainService: 523 passed. Server: 4,279 passed, 25 skipped. `git diff --cached --check` passed. The full Contracts test run initially hit a transient executable-file lock while other suites ran; the affected test passed directly, and the isolated full rerun passed. Re-run these gates after re-derivation.

**First attempt matrix audit:** The actor JSON round-trip and registry tests covered legacy unstamped bytes and ordered chains; registry and projection tests covered rename, invalid versions, failed upcasters and no handler effect; Contracts identity tests covered named components and legacy GUID reads; projection, keyed processor and aggregate tests covered caller-token forwarding. These covering tests ran in the successful first-attempt suites. Recheck the matrix and new real wire coverage after re-derivation.

**Commands:**

- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` — no warnings/errors.
- `for p in Contracts Client DomainService Server; do dotnet test tests/Hexalith.EventStore.$p.Tests/Hexalith.EventStore.$p.Tests.csproj --configuration Release || break; done` — each affected project passes; use built xUnit v3 assemblies for focused filters.
- `git diff --check` — no whitespace errors; existing CI contains no retired evolution jobs.

## Pause Checkpoint — 2026-10-09

The user asked to save and stop during review-loop iteration 1. The implementation agent was interrupted; its current changes remain in the working tree. Tasks are intentionally unchecked, the story remains `in-progress`, and the second-pass implementation has not been staged or reviewed.

The agent reported that the Release solution build passed before its latest cleanup, and focused versioned wire, registry, identity, publisher, projection, subscription and snapshot tests passed. Its most recent full Contracts run had one tracked-file inventory failure because the retired evidence deletions were not staged. Its full Client run had 46 failures, mostly tests still referring to retired evidence; the agent was removing those tests and fixing an inline-payload regression. DomainService and Server full suites had not been rerun after cleanup. Treat these as provisional reports and rerun all gates.

On resume, inspect the full diff and worktree, finish the interrupted implementation, stage only Story 6.6 files, then run the step-03 task, matrix and verification audit before step-04 review. Concurrent unrelated Security work shares this checkout and must be preserved and excluded from staging. The Story 6.6 change in `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs` removes only exemptions and theory rows for retired snapshot fixtures; include that exact diff after inspection. Do not stage other Security edits or the unrelated new Story 8.4 spec. No commit or push was made.
