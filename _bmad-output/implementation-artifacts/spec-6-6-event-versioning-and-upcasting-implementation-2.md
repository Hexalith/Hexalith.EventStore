---
title: 'Story 6.6: Shared Evolution Reader For Replay'
type: 'feature'
created: '2026-10-05'
status: 'in-progress'
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

- [x] `src/Hexalith.EventStore.Server/Events/DaprLogicalEventReader.cs` — add a new single-type production reader beside this file that returns an addressed logical page only after digest, prefix, head, floor, and ETag checks, using the existing registry and chain for a caller-supplied pin.
- [x] `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` and `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` — read replay pages through that reader and keep zero-hop V1 bytes compatible.
- [x] `src/Hexalith.EventStore.Server/DomainServices/DaprAggregateStateReconstructor.cs` and `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` — reconstruct and rehydrate through the same reader, and keep the existing digest check on the domain forward path.
- [x] `tests/Hexalith.EventStore.Server.Tests/` — cover the matrix rows for replay and reconstruction, including digest mismatch, unsupported version, and cancellation. Assert the existing V2 write fence still rejects before mutation.

**Acceptance Criteria:**

- Given addressed V1 history, when replay or reconstruction runs, then the effective result matches canonical replay and stored application bytes stay unchanged.
- Given a missing mapping, bad digest, incomplete prefix, or version 2, when replay or reconstruction runs, then the outcome is typed and no partial state is applied.
- Given an implicit V1 command response with a versioned triplet, when the invoker and persister run, then they still reject it before actor mutation.
- Given cancellation during the actor read, when replay runs, then no partial result is published.

### Review Findings

Code review of `65ac85a2` on 2026-10-05. All four layers completed: Blind Hunter, Edge Case Hunter, Verification Gap, and Acceptance Auditor. Result: 4 decision-needed (resolved into P14–P17), 17 patch, 2 defer, 7 rejected.

- [x] [Review][Decision] D1 The caller pin has no production upcasters or validators — `FromCallerPin` builds the executor with an empty upcaster map and a no-op `EventVersionValidator` (`DaprProductionLogicalEventReader.cs:51-64`). Every hop throws `CapabilityMismatch`, so no versioned event can be evolved in production. The schema and identity validators that the pinned V descriptor names (`RegisteredEventVersionValidation`) never run, even for zero-hop events. Upcasters fail closed while validators fail open. Owner choice: keep this slice fence-only and record the binding seam and the skipped validation explicitly, or add an allow-listed binding seam now. Resolved 2026-10-05: owner chose fence-only and record it → P14.
- [x] [Review][Decision] D2 Shape of an evolved domain envelope — `CreateDomainEvent` (`DaprProductionLogicalEventReader.cs:224-235`) relabels an evolved event as `MetadataVersion = 1` with `EventTypeName = CanonicalType`. It also keeps the pre-upcast `ApplicationPayloadDigest` and the write-time `DomainServiceVersion`. `ApplyMethodResolver.TryResolve` matches CLR full or short names only, so a kebab-case canonical name finds no Apply method. This is latent until D1 adds bindings, and `UpcastReplayKeepsStoredBytesAndExposesEffectivePayload` locks the relabel in. Owner choice: fail closed on any evolved event until a verified route exists, stamp the current version's registered alias and clear the digest, or carry the triplet through a verified effective route. Resolved 2026-10-05: owner chose fail closed on evolved events → P15.
- [x] [Review][Decision] D3 Typed outcome for unsupported history on the actor paths — on the command path, a reader rejection (UnknownEventContract, LogicalDigestMismatch, CapabilityMismatch, ReplayRestartRequired) reaches the generic rehydration catch (`AggregateActor.cs:1309`). It is dead-lettered as an infrastructure failure, and the redactor drops the reason to the default code. Manual snapshot returns a generic `InfrastructureFailure`. The I/O matrix requires "Typed rejection". Owner choice: a stable, support-safe reason code on the existing dead-letter and manual-snapshot outcomes, a new typed rejection status, or deferral to the split diagnostics spec. Resolved 2026-10-05: owner chose a stable reason code → P16.
- [x] [Review][Decision] D4 Stored aggregate-type enforcement can wedge legacy streams — once a candidate is registered, every replayed event must match the expected aggregate type (`DaprLogicalEventReader.ReadCoreAsync`) and the manifest route. Replay takes that type from the resolver or, when the resolver returns null, from the first stored event (`AggregateActor.cs:1263`). Persist falls back to `command.Domain` (`AggregateActor.cs:4756-4766`). A stream whose events mix the resolved type and the fallback type then raises `AddressMismatch` on every command. The typed path never checked the type. Owner choice: fail closed and unify replay and persist resolution so the platform cannot write a type it later rejects, accept the domain-name fallback as an alias of the manifest route, or defer. Resolved 2026-10-05: owner chose fail closed with unified resolution → P17.
- [ ] [Review][Patch] P1 (high) Reject stored metadata-V2 events on the production replay path instead of relabelling them as V1 [src/Hexalith.EventStore.Server/Events/EventStreamReader.cs:155] — the production branch drops `LegacyEventReadGuard.RequireUnversioned`. `EventLogicalViewResolver` accepts `metadataVersion == 2` when the pin declares that payload version, and zero hops still set `Evolved`. `CreateDomainEvent` then strips the triplet, and the actor sends the V1-labelled envelope to the implicit-V1 invoker. Both version-2 tests pass only because the V1-only registry lacks payload version 2. Add a rejection, plus tests with a declared payload version 1 and an upcasting registry.
- [ ] [Review][Patch] P2 Charge the legacy array budget per event inside the production range read, including domain copies, and bound `ReconstructAddressedAsync` [src/Hexalith.EventStore.Server/Events/EventStreamReader.cs:166] — `arrayBudget.Add` runs only after `ReadRangeAsync` has kept the whole range. No `EventBufferBudget` is shared across pages. Domain copies are never charged. `ReconstructAddressedAsync` has no `LegacyEventArrayBudget` and preallocates two `List(count)`. A stream above 64 MiB is fully read into memory before `LegacyArrayLimit` fires; the typed path aborts at 64 MiB.
- [ ] [Review][Patch] P3 Map provider failures in the logical page reader the same way as the actor's readability boundary [src/Hexalith.EventStore.Server/Events/DaprLogicalEventReader.cs:108] — `ReadCoreAsync` sends `ProviderOpaque` envelopes to the provider and lets a provider exception through unchanged. `EnsureEventsReadableForDomainAsync` rejects opaque payloads first and maps provider exceptions to `ProtectedDataUnreadableException(ProviderUnavailable)`. Now that the reader runs first, the command path loses the typed protected-data reason, and manual snapshot skips its `ProtectedDataUnreadableException` catch (`AggregateActor.cs:2311`).
- [ ] [Review][Patch] P4 Remove the redundant second full read in manual-snapshot reconstruction [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:2390] — the actor runs a full production replay plus `EnsureEventsReadableForDomainAsync`, keeps only `AggregateType`, then has `ReconstructAddressedAsync` read 1..head again. That is two actor reads and three provider unprotects per event within the 30 s timeout (`:2225`), and the second read is not pinned to the first read's floor. Recommended fix: when the reader is present, skip the first pass and read the aggregate type with `ReadStoredAggregateTypeAsync`, with P2 bounding the range.
- [ ] [Review][Patch] P5 Stop building discarded, unzeroed plaintext domain copies for zero-hop replay [src/Hexalith.EventStore.Server/Events/DaprProductionLogicalEventReader.cs:151] — `ReadRangeAsync` copies the unprotected payload for every event. `EventStreamReader` discards the copies when `Evolved` is false, which is always the case in production with the current pin. The copies are never zeroed, unlike `protectedCopy`. Let callers that do not consume the domain view skip the copy. This also widens the open, verified-high ledger row "Clear distinct plaintext protection output", which this wiring makes reachable in production.
- [ ] [Review][Patch] P6 Test a successful multi-page range (more than 256 events) [tests/Hexalith.EventStore.Server.Tests/Events/DaprProductionLogicalEventReaderTests.cs] — no test completes even a two-event range. Changing `Math.Min(PageSize, count - offset)` to `Math.Min(PageSize, count)` keeps every test green, yet breaks every keyed replay or manual snapshot above 256 events. Assert the count, contiguous sequences across the page boundary, and the pinned floor.
- [ ] [Review][Patch] P7 Make the keyed manual-snapshot test discriminating and add a positive addressed-reconstruction control [tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorManualSnapshotTests.cs:261] — the hop candidate throws in the first full replay, so `ReconstructAddressedAsync` is never entered. A null resolver turns a bypass of the reader into `StateReconstructionFailed`, which still passes the test. Assert `ReasonCode == "InfrastructureFailure"`, configure a resolver registration, and add a non-upcasting keyed fact that reaches addressed reconstruction. VG3 is still open.
- [ ] [Review][Patch] P8 Test the split between stored and plaintext bytes with a non-identity protection provider [tests/Hexalith.EventStore.Server.Tests/Events/DaprProductionLogicalEventReaderTests.cs] — every successful reader test uses `NoOpEventPayloadProtectionService`. As a result, `return source;` in the zero-hop `CreateDomainEvent` branch, or passing `replay.StoredEvents` to reconstruction, would keep the suite green. Follow the pattern in `DaprLogicalEventReaderTests.cs:38-42`.
- [ ] [Review][Patch] P9 Add a positive canonical-equivalence test [tests/Hexalith.EventStore.Server.Tests/Events/DaprProductionLogicalEventReaderTests.cs] — `AddressedReconstructionOfCurrentV1ReachesCanonicalReplay` asserts `UnknownAggregateType`, and the only keyed command test (`RejectedLogicalReadFailsTheCommandBeforeInvoke`) proves rejection. AC1 needs typed and production rehydration of the same multi-event V1 history to produce equal `Events`, readable payloads, and the `DomainServiceCurrentState` given to the invoker. VG1 is still open.
- [ ] [Review][Patch] P10 (low) Assert typed outcomes at the replay and reconstruction entry points [tests/Hexalith.EventStore.Server.Tests/Events/DaprProductionLogicalEventReaderTests.cs] — digest mismatch, missing mapping, and incomplete prefix are tested only against the page or range reader. Reconstruction tests assert `Failed` but not `ErrorCategory` or the message, so the BH5 patch has no regression guard. `ReconstructAddressedAsync`'s `MissingEventException` branch and its cancellation path are untested.
- [ ] [Review][Patch] P11 (low) Fix the new ledger rows and update the rows this wiring makes stale [_bmad-output/implementation-artifacts/deferred-work.md:5375] — the five rows added by `65ac85a2`, plus the three split rows, have no `status: open`, use absolute `source_spec` paths, and sit under the Story 6.1 heading. Update the open rows "shared reader not wired into production paths" and "Clear distinct plaintext protection output" for the new production reachability.
- [ ] [Review][Patch] P12 (low) Delete the unused `SnapshotManager.ReadReplayPageAsync` [src/Hexalith.EventStore.Server/Events/SnapshotManager.cs:344] — nothing in `src` or `tests` calls it.
- [ ] [Review][Patch] P13 (low) Document that registering a candidate switches the domain's replay to the fail-closed reader [src/Hexalith.EventStore.Client/Registration/EventEvolutionServiceCollectionExtensions.cs:10] — the public remarks describe an inert candidate, but the actor now activates the allow-list reader for every aggregate in that domain.
- [ ] [Review][Patch] P14 Make the binding-free caller pin explicit and record the binding seam [src/Hexalith.EventStore.Server/Events/DaprProductionLogicalEventReader.cs:51] — from D1. State in the `FromCallerPin` remarks that the production pin binds no upcaster and runs no registered schema or identity validation. File a ledger row for the allow-listed upcaster and validator binding seam, which stays blocked on trusted loader and catalog closure.
- [ ] [Review][Patch] P15 Fail closed when production replay or reconstruction meets an evolved event [src/Hexalith.EventStore.Server/Events/EventStreamReader.cs:155] — from D2. Raise a typed `CapabilityMismatch` when `replay.Evolved` is true in `EventStreamReader` and `ReconstructAddressedAsync`, until a verified effective route exists. Update `UpcastReplayKeepsStoredBytesAndExposesEffectivePayload` to cover the refusal.
- [ ] [Review][Patch] P16 Give logical-read rejections a stable, support-safe reason code on the dead-letter and manual-snapshot outcomes [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1309] — from D3. Use an additive code and expose no message text or identifiers.
- [ ] [Review][Patch] P17 Resolve the replay and persist aggregate type through one helper [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1263] — from D4. Keep the strict type check, and make replay use the same resolver-then-`command.Domain` fallback as persist, so the platform cannot write a type that replay rejects.
- [x] [Review][Defer] Admin stream reconstruction still feeds typed event lists to `ReconstructAsync` [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs] — deferred, pre-existing: stream diagnostics stay on current code in this slice (Design Notes). The commit's claim that "replay and reconstruction share one Dapr reader" does not cover the seven Admin state-at, bisect, blame, step, and sandbox calls.
- [x] [Review][Defer] Cross-page metadata ETag on addressed reconstruction [src/Hexalith.EventStore.Server/DomainServices/DaprAggregateStateReconstructor.cs:199] — deferred, maybe-false and medium if true. Two open rows already track it. `EventStreamReader` re-checks the whole range with `RequireUnchangedMetadataAsync`, and `ReconstructAddressedAsync` does not. To settle it, show a supported same-head, same-floor ETag change within one actor turn across more than 256 events.

Rejected:

- BH8 Classification by message substring and message disclosure — low. The only caller, manual snapshot, discards the category and the message, and typed exceptions are more than a direct correction.
- BH13 No telemetry for which read path was selected — low. Adding logs or tags is new surface. The documentation part is kept as P13.
- BH14 `as SnapshotManager` and `is DaprAggregateStateReconstructor` checks — low. No decorated implementation exists, and the fix adds abstraction. The dead method is kept as P12.
- BH18, AA7, VG7 Status, triage-row, and iteration contradictions in this spec — rejected because the fix edits the spec under review. The sprint row is set by this review's status sync.
- BH3 and VG5 Second unprotect on the command path — rejected. The Code Map requires keeping `EnsureEventsReadableForDomainAsync` digest enforcement on the domain forward path.
- EC6 Preallocation above 100,000 events in `ReconstructAddressedAsync` — false. The only caller first runs `RehydrateAsync`, whose `LegacyEventArrayBudget(head)` throws above 32,768 events. Unbudgeted reconstruction is P2.
- AA9 `expectedActorHead: upToSequence` fails below the head — false. The only caller passes the current head, and any other value fails loudly as `SourceHeadChanged`.

## Implementation Notes

## Spec Change Log

## Review Triage Log

| Finding | Verdict / evidence | Route |
| --- | --- | --- |
| BH1 empty upcaster map and no closure check | false: `FromCallerPin` binds the caller pin's registry and no invented callable. A required hop throws `CapabilityMismatch`. `RequireSuppliedLocalClosure` needs a graph the actor does not have; the candidate constructor already checks the pin. | reject |
| BH2 zero-hop drops effective copies and skips the legacy version guard | false: zero-hop V1 must stay on the stored envelope. `EnsureEventsReadableForDomainAsync` still checks the digest. A version the registry rejects never reaches `RehydrationResult.Events`. | reject |
| BH3 same-version rewrite is not applied | false: zero-hop V1 is required to stay byte-compatible with the typed read, so the stored envelope remains the domain input. | reject |
| BH4 addressed reconstruction always starts at sequence 1 | false: the only caller passes the current head after a full rehydrate that already rejects a start below the retained floor. A shorter prefix is not a reached call. | reject |
| BH5 logical rejection text is replaced | medium: `ReconstructAddressedAsync` keeps `UnsupportedVersion` for three message fragments and otherwise returns `Unexpected` with the fixed text "Addressed logical replay was rejected." Digest, address, and restart reasons are dropped. `Unexpected` is the only fitting category for a digest mismatch. | patch |
| BH6 cross-page ETag and unauthenticated aggregate type | maybe-false: each page already compares its own before/after ETag, and `ReadCoreAsync` then checks tenant, domain, aggregate id, and sequence. A same-head/floor ETag change between pages was not demonstrated. The type read is rechecked by that page. | defer |
| BH7 evolved digest and uncleared range copies | false: `ToContractEventEnvelope` does not forward `ApplicationPayloadDigest`, and the stored-envelope digest check still runs. Range copies die with the thrown stack frame. | reject |
| BH8 spec still in progress and canonical reconstruction expects failure | false: this spec is `in-review` and its Verification section names three commands. The reconstruction fact stops at `UnknownAggregateType` because the substitute resolver returns null. | reject |
| BH9 new ledger rows omit status and use absolute paths | medium, not this slice: the three split rows and the Dapr review rows were already in the ledger. This reader change does not own that format. | defer |
| BH10 post-upgrade verifier timestamp and row comparison | maybe-false, not this slice: `utc()` now uses microseconds and the runner compares event maps by key. The claimed `'.'` versus `'+'` sort failure was not re-executed. | defer |
| BH11 validator hides IndexError text | medium, not this slice: `validate()` replaces `IndexError` and `StopIteration` with the exception type name. That runner is the Story 6.1 evidence tool. | defer |
| BH12 Tenants gitlink versus the reconciliation sentence | false: the reconciliation sentence describes its own completion commit. The Tenants gitlink move is a separate change since the baseline. | reject |
| EC1 snapshot prefix stays pre-upcast | false: a hop required by the production pin fails closed in `FromCallerPin` before a snapshot fold returns. | reject |
| EC2 upcast output can exceed the page cap | false: the production pin registers no upcaster, so an expanding hop cannot return a page. | reject |
| EC3 missing wire type defaults to the contract | medium, not this slice: the Story 6.1 runner uses `r.get("type", contract)`. | defer |
| EC4 duplicate event keys collapse in the runner map | medium, not this slice: the Story 6.1 runner builds `event_map` by key. | defer |
| VG1 command path never proves the invoker sees the logical view | medium: reader tests do not construct `AggregateActor` with a keyed candidate. Filed disposition stands. | patch |
| VG2 snapshot tail through the production reader is untested | medium: every new `RehydrateAsync` call passes `snapshot: null`. The tail limit exists in code and has no production-reader test. | patch |
| VG3 manual snapshot never enters addressed reconstruction | medium: existing manual-snapshot tests use a provider that is not `IKeyedServiceProvider`. | patch |
| VG4 `FromCallerPin` hop failure is untested | medium: the broken-chain fact uses `CreateReader`, not `FromCallerPin`. | patch |

## Design Notes

`DaprLogicalEventReader` remains the page primitive. Replay and reconstruction share one production reader so they cannot drift. Logical readback shows that Dapr returned the same application value. It does not show physical bytes or an older committed generation. Projection, publication, subscription, and stream diagnostics stay on their current code until their deferred specs land.

## Verification

**Commands:**

- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` — expected: 0 warnings and 0 errors.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true` — expected: new replay and reconstruction tests pass, and the existing V2-fence tests stay green.
- `git diff --check` — expected: no whitespace errors.
