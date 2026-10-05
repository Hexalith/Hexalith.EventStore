---
title: 'Story 6.6: Shared Evolution Reader For Replay'
type: 'feature'
created: '2026-10-05'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '6-6-event-versioning-and-upcasting-implementation'
baseline_commit: 'd594b781666a8e42a82b1601db51d0ddf4ac19ca'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-6-6-dapr-only-amendment.md'
  - '{project-root}/_bmad-output/implementation-artifacts/6-6-implementation-map.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Replay and reconstruction still read typed actor history. The shared upcast path exists only as internal types, so a versioned event cannot be evolved on the production read path.

**Approach:** Add one Dapr-only production reader beside the existing logical page reader, and route replay and reconstruction through it. Preserve immutable application bytes and the current V1 write fence.

## Boundaries & Constraints

**Always:** Read aggregate events through `AggregateActor` and `IActorStateManager`. When a digest is present, verify it over the logical payload and metadata after unprotection. Historical V1 values without a digest stay readable. Fail closed on a missing or ambiguous allow-listed mapping, a bad digest, an incomplete prefix, or an unsupported version. Propagate the caller cancellation token to the actor read. Preserve legacy positional constructors and the additive metadata triplet. Zero-hop V1 results stay byte-compatible with today's typed read.

**Never:** Change projection dispatch, publication, subscription, or stream diagnostics. Open a database connection, add Npgsql, or treat logical readback as a provider receipt. Do not rewrite retained events or MessageIds. Do not register an invented production domain manifest. Do not admit V2 on the implicit V1 writer path. Do not enable physical capture, command re-execution, snapshot redesign, or Epic 8 protection.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Current or historical V1 | Addressed actor history, with or without a digest on older V1 | Replay and reconstruction match the canonical payload; stored bytes and MessageId stay unchanged | Digest mismatch fails closed before domain code |
| Unsupported history | Missing mapping, version 2, or a broken chain | No reconstructed state and no partial apply | Typed rejection; no silent skip |
| Implicit V1 write | Domain response carrying a versioned triplet | Existing refusal stays in place; this slice does not stage that write | Capability mismatch and persister version-2 refusal |
| Cancellation | Token cancelled during the actor read | No partial replay result is published | Cancellation stays distinct from domain rejection |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Client/Events/EventDomainRegistry.cs`, `EventUpcastChainExecutor.cs`, `EventLogicalViewResolver.cs`, `BoundedScratchAllocator.cs` — reuse the local registry, chain, resolver, and scratch ceiling. Do not turn them into a second pipeline.
- `src/Hexalith.EventStore.Client/Registration/EventEvolutionServiceCollectionExtensions.cs` — `AddEventStoreEventEvolutionManifestCandidate` accepts a caller-supplied pin only. Do not add a repository domain row.
- `src/Hexalith.EventStore.Server/Events/DaprLogicalEventReader.cs` — internal page reader, currently tests only. The new production reader calls it after digest, prefix, head, floor, and ETag checks.
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`, `SnapshotManager.cs`, `DomainServices/DaprAggregateStateReconstructor.cs`, `Actors/AggregateActor.cs` — typed replay and reconstruction today. Switch these reads to the production reader. Keep `EnsureEventsReadableForDomainAsync` digest enforcement.
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` and `Events/EventPersister.cs` — `ValidateLegacyWriterResponse` and the `metadataVersion == 2` refusal already fence V2. Do not weaken them.
- Leave `Projections/ProjectionUpdateOrchestrator.cs`, `Events/EventPublisher.cs`, `Client/Subscriptions/EventStoreDomainEventProcessor.cs`, and `Controllers/StreamsController.cs` unchanged.

## Tasks & Acceptance

**Execution:**

- [ ] `src/Hexalith.EventStore.Server/Events/DaprLogicalEventReader.cs` — add a new single-type production reader beside this file that returns an addressed logical page only after digest, prefix, head, floor, and ETag checks, using the existing registry and chain for a caller-supplied pin.
- [ ] `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` and `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` — read replay pages through that reader and keep zero-hop V1 bytes compatible.
- [ ] `src/Hexalith.EventStore.Server/DomainServices/DaprAggregateStateReconstructor.cs` and `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` — reconstruct and rehydrate through the same reader, and keep the existing digest check on the domain forward path.
- [ ] `tests/Hexalith.EventStore.Server.Tests/` — cover the matrix rows for replay and reconstruction, including digest mismatch, unsupported version, and cancellation. Assert the existing V2 write fence still rejects before mutation.

**Acceptance Criteria:**

- Given addressed V1 history, when replay or reconstruction runs, then the effective result matches canonical replay and stored application bytes stay unchanged.
- Given a missing mapping, bad digest, incomplete prefix, or version 2, when replay or reconstruction runs, then the outcome is typed and no partial state is applied.
- Given an implicit V1 command response with a versioned triplet, when the invoker and persister run, then they still reject it before actor mutation.
- Given cancellation during the actor read, when replay runs, then no partial result is published.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

`DaprLogicalEventReader` remains the page primitive. Replay and reconstruction share one production reader so they cannot drift. Logical readback shows that Dapr returned the same application value. It does not show physical bytes or an older committed generation. Projection, publication, subscription, and stream diagnostics stay on their current code until their deferred specs land.

## Verification

**Commands:**

- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` — expected: 0 warnings and 0 errors.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true` — expected: new replay and reconstruction tests pass, and the existing V2-fence tests stay green.
- `git diff --check` — expected: no whitespace errors.
