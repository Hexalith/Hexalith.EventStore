---
title: 'Story 6.2: Folded Snapshot Implementation'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: 'e4532baa64f4cdabdbf766b9e38c36289a00adba'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-folded-snapshot.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Automatic snapshots store history-bearing `DomainServiceCurrentState` at the pre-command sequence, so cost grows with history; manual snapshots already store folded state.

**Approach:** Implement approved HX-FS-V1 at SHA-256 `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5` and `MaxSnapshotEnvelopeOverheadBytes=4096`: one Apply seam, exact committed head, safe operator evidence.

## Boundaries & Constraints

**Always:** Recompute the digest before edits; mismatch stops work. Apply §§2–17 and V1–V18 without changing normative bytes. Keep actor-owned batch save, events as replay authority, advisory automatic/fail-closed manual behavior, cancellation, and positional compatibility. Version-1 writes require qualified compact serialization, pre-admitted resources and exact byte measurement. Redact both Admin snapshot-key routes.

**Never:** Persist DSCS/history or unqualified capture; add a second fold/replay/domain call, provider storage, Epic 8, projection or upcasting work. No unproven deletion, unverified completion, background migration or event rewrite. Future writes may overwrite legacy snapshots.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Automatic | Qualified due command | Detached once-admitted Apply; version-1 fold at new head in event batch | Absent evidence, no-op, first threshold crossing, limits: advisory skip (§§5–8) |
| Manual/read | Complete prefix or legacy/corrupt/opaque/orphan/gap | Qualified fold at head; validate metadata/version/shape/sequence before using snapshots | §9.2 typed retain/bypass/failure; readback proves success (§§7–10) |
| Admin | Local/owner-sidecar snapshot key | Sequence, canonical size or unknown, version, protection/readability | Redact state, secrets, provider errors and stacks (§§11,13) |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- automatic batch/fence and manual witness; automatic currently writes pre-event DSCS, manual `AlreadyCurrent` accepts `>=`.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`, `EventStreamReader.cs` -- typed load precedes raw version check; broad load catch deletes, reader trusts snapshot ahead of head.
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs`, `DomainProcessorStateRehydrator.cs`; `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs` -- shared Apply/restore; current capture lacks deployment qualification.
- `src/Hexalith.EventStore.DomainService/BoundedV1DomainResultProducer.cs`; `src/Hexalith.EventStore.Server/DomainServices/BoundedV1DomainResponseParser.cs` -- once-admitted bytes exist, but optional state has no producer/parser path.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs` -- local and owner-sidecar inspector exposes raw snapshot JSON.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` -- verify digest, approval and no open decisions before code edits (V1; §18).
- [x] `src/Hexalith.EventStore.Contracts/Commands/DomainServiceRequest.cs`, `src/Hexalith.EventStore.Contracts/Results/DomainResult.cs`, `DomainServiceWireResult.cs`; `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` -- add defaulted init-only request/evidence/version (§§5,8,12).
- [x] `src/Hexalith.EventStore.DomainService/BoundedV1DomainSerializerProfile.cs`, `EventStoreDomainServiceExtensions.cs`; `src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs` -- qualify exact capture profile, reserve finite workspace before allocation, Apply detached admitted bytes (§§5.2,8).
- [x] `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs`, `BoundedV1DomainResultProducer.cs`, `BoundedV1WireResultAdmission.cs`, `BoundedV1WireResultResponse.cs`; `src/Hexalith.EventStore.Server/DomainServices/BoundedV1DomainResponseParser.cs`, `DaprDomainServiceInvoker.cs` -- carry optional evidence within current gates/fallbacks (§§8,12).
- [x] `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`, `EventStreamReader.cs`; `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- classify raw version, validate head/shape, measure then stage folded state under fence, keep manual readback and §9.2 outcomes (§§5–10).
- [x] `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`, `DaprConsistencyCommandService.cs` -- redact both snapshot-key routes and exception-bearing logs (§§11,13).
- [x] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs`, `AggregateActorManualSnapshotTests.cs`; `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs`, `SnapshotRehydrationTests.cs`; `tests/Hexalith.EventStore.DomainService.Tests/BoundedV1DomainResultProducerTests.cs`; `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs` -- prove V2–V18, including persisted parity, byte counts and compatibility (§15).

**Acceptance Criteria:**
- Given qualified admitted events, when a due snapshot commits, then version-1 folded state covers that head, equals manual replay and excludes history (V2–V7).
- Given absent qualification/evidence or policy/resource/shape/byte failure, when a valid command commits, then event bytes and result remain unchanged without snapshot-only commit (V5–V6,V15–V18).
- Given legacy, protected, malformed, orphaned or gapped data, when either read path runs, then §9.2 applies without partial authority (V8–V12,V17).
- Given either Admin inspector route reads a snapshot, when it returns, then only §13 safe fields appear and durable readback supports any completion (V13).

### Review Findings

Code review 2026-10-11, chunk 1 of 2: production `src/` from `2841e0c8..c284eb00` (31 files). Tests and `tools/` are chunk 2.

- [x] [Review][Patch] Automatic write replaces retained `Corrupt` (`AddressMismatch`, `SnapshotSequenceInvalid`, `SnapshotStateInvalid`) and `InfrastructureFailure` keys with no reason class. Decision: replace them with a support-safe reason class by extending the `replacementReasonClass` switch and the `CreateAutomaticReplacementAsync` allowlist, so keys still self-heal and log like opaque/unreadable replacements; add one test per class [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1507]
- [x] [Review][Patch] XML docs say `DomainReservationBytes`/`ActorReservationBytes` are "reserved before capture starts", but they are only validated declared upper bounds; `/process` has no request-wide budget (`ProcessCoreAsync` receives `budget: null`). Decision: keep the declared-limit validation plus the wire-gate check as the §8.6 interpretation and correct the wording [src/Hexalith.EventStore.Contracts/Snapshots/FoldedSnapshotCaptureLimits.cs]
- [x] [Review][Patch] Snapshot read uses `TryGetStateAsync<JsonElement>` while writers stage `SnapshotRecord`; Dapr 1.18.10 casts cached values and keeps them after save, so every later read in the same activation throws, becomes `SnapshotReadUnavailable`, full-replays with sequence 0 and rewrites the snapshot on each due command (manual returns `InfrastructureFailure`). `InMemoryStateManager.ReadValue<T>` converts to `JsonElement` and hides this from every test [src/Hexalith.EventStore.Server/Events/SnapshotManager.cs:277]
- [x] [Review][Patch] Actor-side qualification registry failure (duplicate enrollment throws in the singleton constructor) dead-letters every command instead of skipping capture as §12/V15 require; the domain side already swallows it [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1376]
- [x] [Review][Patch] Story 22.7c snapshot-load routing removed: opaque/unreadable loads no longer go through `ProtectedDataReadabilityDecisionFactory`, emit no event-7100 warning, and `Log.UnreadableProtectedSnapshot` is now uncalled [src/Hexalith.EventStore.Server/Events/SnapshotManager.cs:340]
- [x] [Review][Patch] Docs still describe interval-only automatic snapshots; automatic capture now needs a deployment qualification on both hosts, bounded V1 serialization and a `FoldedSnapshotCapture` profile, and legacy DSCS snapshots now fully replay [docs/guides/configuration-reference.md:148]
- [x] [Review][Patch] No successful capture test folds onto a non-empty pre-command state, typed or JSON DSCS; every passing capture starts from null state [tests/Hexalith.EventStore.DomainService.Tests/FoldedSnapshotCaptureTests.cs:24]
- [x] [Review][Patch] `IncludePostCommandState`/`SnapshotCaptureProfileId` are never verified through the real invoker and router; dropping the profile ID would silently stop all automatic snapshots [tests/Hexalith.EventStore.Server.Tests/DomainServices/VersionedWireRoundTripTests.cs:56]
- [x] [Review][Patch] Manual `InfrastructureFailure` refusal and the new `SnapshotReadUnavailable`/`SnapshotUnprotectUnavailable` mappings are untested; removing the refusal would overwrite a retained protected snapshot and report `Created` [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:2373]
- [x] [Review][Patch] The full Server regression never finished (exit 124), though the story records `done`; run the Server test assembly to completion through the fallback ladder after patches [tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj]
- [x] [Review][Patch] Manual post-save "durable" readback reads the actor cache, so `SnapshotReadbackMismatch` can never fire; clear the cache before the readback [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:2488]
- [x] [Review][Patch] Failure classes and the Admin-visible manual `ReasonCode` come from `ex.Message.StartsWith("Snapshot")`, so provider text can leak and "Snapshot protection metadata is invalid." becomes a class; use an allowlist [src/Hexalith.EventStore.Server/Events/SnapshotManager.cs:218]
- [x] [Review][Patch] Absent post-command evidence is logged as `SnapshotStateInvalid`, which misleads operators in mixed-version deployments [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:2579]
- [x] [Review][Patch] Optional capture rethrows any `OperationCanceledException`, not only caller cancellation, so a callback cancellation fails a valid command (§8.3) [src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs:97]
- [x] [Review][Patch] New snapshot warnings carry no correlation or aggregate identity and bypass `[LoggerMessage]` [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1484]
- [x] [Review][Patch] New `using System.Text*` directives violate `dotnet_sort_system_directives_first` [src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs:2]
- [x] [Review][Patch] New internal members lack the XML docs the repo baseline requires [src/Hexalith.EventStore.Server/Events/SnapshotEnvelopeInspector.cs:8]

**Rejected:**

- false — Pre-command due check defers the first interval crossing: §8.3 mandates evaluating at the admitted pre-command head and the second post-persist check.
- false — Orphan snapshot blocks its aggregate: §9.1 mandates bounded `SnapshotStreamUnavailable`; remediation is outside 6.2.
- false — Manual data/config refusals are `InfrastructureFailure`: §§5.3, 6.3 and 8.6 mandate bounded `InfrastructureFailure` with the reason class.
- false — Default interface members drop new arguments: §§8.3 and 12 mandate default delegation with absent evidence as advisory skip.
- false — `includePostCommandState:false` breaks old services: domain request binding does not disallow unmapped members.
- false — Synthetic capture metadata breaks parity: replay reads only metadata version, format, contract type, payload version, type name and sequence, and both paths use the same `ForApplyState` evolution registry.
- false — Parser budget exhaustion rejects valid results: capture limits keep events at or below 64 MiB and evidence at or below 6 MiB raw, under the 128 MiB live budget.
- false — Qualification is not bound to the serializer profile: capture decodes the exact admitted bytes with the shared replay deserializer, and `IsQualified` checks state type and limits.
- false — Test proofs are absent from the diff: this chunk excludes tests and tools by design.
- low — `PostCommandStateJson` appears in record `ToString()`: no code formats `DomainResult`, and events already print the same way.
- low — Duplicate `postCommandStateJson` is last-wins: same as `resultPayload` in this parser, from a trusted domain service.
- low — Capture payload copies are not zeroed: §8.2 requires private copies, not zeroing.
- low — Manual path reads event 1 for the aggregate type: §9.1 already fails closed when `RetainedFloor > 1`; only the reason class differs.
- low — `FoldedSnapshotCaptureLimits` ergonomics (18 positional parameters, one parameter name, repeated constants): no current defect.
- low — Preallocated bounded buffers and per-command serializer probe: deliberate strict reservation; the probe is tiny.
- low — Non-resumable converters buffer before the bound check: §8.6 accounts serializer scratch; the fix needs a custom bounded writer.
- low — Admin redaction reports `SizeBytes` 0, accepts unvalidated `state:0`, hard-codes codes: `canonicalSizeBytes` null reports unknown per §13; changing `SizeBytes` needs a DTO change.
- low — Unsupported actor serializer skips silently: requires indented actor JSON options.
- low — Custom processor can exceed the escaped-state reservation: trusted callback; server parser and actor re-bound it.
- low — Profile getter is re-read after `IsQualified`: matches the prior EC3 rejection.
- low — Domain-side capture catches do not log: the static router has no logger; the actor logs the skip.
- low — `ResolveAggregateTypeAsync` now runs before the no-op return: resolver failures are rare.
- low — Fully qualified names and implicit public interface members: cosmetic; `b57310fb` is already on `origin/main`.
- low — Commit `b57310fb` is typed `feat(tests)` but adds production code: already on `origin/main`.
- low — Legacy `CreateSnapshotAsync` overload measures with Web options: no production caller.

## Implementation Notes

- The implementation uses one qualified post-admission fold path and stages version-1 snapshots in the actor's event batch. The command reader validates the authoritative head and raw snapshot fields before typed binding; unverified direct reads retain malformed keys.
- `tools/folded-snapshot-compatibility/run.sh` exercises published pre-6.2 packages, a locally packed current version, binary and positional API use, optional interface fallbacks, and old/new wire and snapshot reads (V12).
- The unrelated `references/Hexalith.FrontComposer` pointer changed during this run and was left untouched.
- An unrelated `references/Hexalith.Builds` pointer change appeared during the review patch run and was left untouched.

## Spec Change Log

- 2026-10-11: Completed the implementation task checks and recorded focused verification. The frozen intent and approved normative snapshot specification were not changed.

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| BH1 Tenants Dapr ACL | medium — Tenants bootstrap invokes `POST api/v1/commands` through Dapr, while `tenants-api` is allowed only `GET /ready`; this is from the separate ACL change after the story baseline. | defer |
| BH2 manifest token key | medium — the manifest declares `EventStore:AppChannelToken`, while the runtime reads `APP_API_TOKEN`; this is from a separate manifest change. | defer |
| BH3 manifest topic | medium — the manifest fixes `eventstore-events`, while publisher conventions use domain and tenant topic names; this is from a separate manifest change. | defer |
| BH4 mutable capture input | medium — the router passes the admitted wire event list and mutable payload arrays directly to a qualified capture callback, so a callback can alter bytes returned after optional failure; §8.2 requires private detached input. | patch |
| BH5 oversized optional scalar | medium — the bounded parser throws when the optional JSON scalar exceeds its field limit, rejecting otherwise valid events instead of omitting only evidence under §8.3. | patch |
| BH6 workspace admission | false — `FoldedSnapshotCaptureLimits` checks the sum of simultaneous declared representation maxima against finite workspace and live budgets before a profile exists; trusted callback allocations remain an explicit contract limit. | reject |
| BH7 null protected state | medium — a protection provider may return null state with valid protected metadata, and the writer currently stages a version-1 record lacking folded content. | patch |
| BH8 enum numbering | medium — inserting `InfrastructureFailure` first in public `SnapshotLoadOutcome` changes existing numeric values and `default` from `Absent`. | patch |
| BH9 P1R admission receipt | medium — the separate P1R runner's refused case calls a standalone `admit` probe and does not exercise a protected historical operation through that guard; the receipt supports only the fixture decision. | defer |
| BH10 P1R candidate pin | false — `validate_execution_inputs` explicitly rejects candidates other than 3.119.0 before `load_support_table`; this source-specific executor is intentionally bound to that version. | reject |
| BH11 production enrollment | false — §8.6 and D13 require explicit deployment-owned qualification; unknown domain states must skip automatic capture and return bounded unsupported manual materialization. The registration API is present for domain owners. | reject |
| EC1 future DSCS manual overwrite | high — raw DSCS collision currently returns no snapshot to the manual actor, bypassing its `SequenceNumber > CurrentSequence` guard and permitting overwrite of an invalid future key. | patch |
| EC2 duplicate Admin metadata | medium — duplicate `protectionMetadata` makes `TryReadSnapshotProperty` false, which the inspector currently conflates with absence and labels `Unprotected`. | patch |
| EC3 mutable parity getter | low — a derived processor could violate its trusted immutable qualification declaration by changing the profile between checks. The public protocol change needed to defend against a deliberately unstable owner callback is disproportionate to this rare defect. | reject |
| VG1 pre-command threshold test | medium — fixed-return policy mocks cannot detect passing an inferred post-command sequence into the first due check; a sequence-sensitive actor test is missing for the first threshold crossing. | patch |
| BH12 pre-command copier cancellation | medium — `DomainProcessorBase` rethrows a copier's internal `OperationCanceledException` while the caller token is active, aborting an otherwise valid command before `HandleAsync`; §8.3 makes optional non-caller failures advisory. | patch |
| BH13 policy resolver cancellation | medium — both optional actor due checks rethrow an internal resolver cancellation with an active caller token; §8.3 requires the valid command or event commit to continue. | patch |
| BH14 automatic snapshot write cancellation | medium — `SnapshotManager` excludes all `OperationCanceledException` values from its advisory write catch, so an internally canceled protection hook aborts a valid event batch. | patch |
| BH15 snapshot read cancellation | medium — internal cancellation from state load or unprotection is rethrown although the caller token remains active, bypassing the typed infrastructure outcome and blocking replay. | patch |
| BH16 malformed evidence reason | low — nonempty malformed or over-depth evidence falls through to `SnapshotEvidenceAbsent`, which incorrectly describes a present but invalid value in operator logs. | patch |
| BH17 process page allocation | medium — `CheckpointedProcessRunner.ValidatePage` serializes an event to an unbounded byte array before applying `MaxEventBytes`; this is Epic 8 process work outside the snapshot intent. | defer |
| BH18 manifest token key | carried medium — the manifest declares `EventStore:AppChannelToken` while runtime reads `APP_API_TOKEN`, as recorded in BH2; the separate manifest change is outside Story 6.2. | defer |
| BH19 manifest topic | carried medium — the manifest's fixed `eventstore-events` topic differs from the publisher's tenant/domain topics, as recorded in BH3; the separate manifest change is outside Story 6.2. | defer |
| BH20 manifest dead-letter strategy | medium — the separate module manifest declares no dead-letter strategy while shipped pub/sub configuration enables dead-letter topics; this integration metadata is outside the snapshot intent. | defer |
| BH21 P1R admission receipt | carried medium — the standalone admission probe does not exercise a protected historical operation, as recorded in BH9; the separate P1R evidence work is outside Story 6.2. | defer |
| EC4 duplicate folded-state keys | medium — actor evidence accepts case-insensitive duplicate top-level state keys, while `RehydrateFromJsonObject` builds a case-insensitive dictionary and throws on the next read. | patch |
| EC5 legacy replacement diagnostic | low — a retained DSCS-shaped snapshot is overwritten through the ordinary writer without a replacement reason class, reducing migration visibility even though the event stream remains authoritative. | patch |
| EC6 large Admin snapshot inspection | medium — the existing owner-sidecar read is unbounded and the new redaction path parses the full legacy value; a large key can multiply allocations. The unbounded read predates Story 6.2. | defer |
| VG2 pre-command copier failure coverage | medium — no domain-route test makes the optional pre-command copier fail; a missing advisory catch would abort before `HandleAsync` without a test detecting it. | patch |
| VG3 pre-command copier cancellation | carried medium — the active-token `OperationCanceledException` path is the same defect as BH12. | patch |
| VG4 policy resolver cancellation | carried medium — the active-token policy cancellation at both due checks is the same defect as BH13; the existing test never cancels its token. | patch |

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` -- warnings-as-errors.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` -- full Server regression; run other changed projects individually.
- `git diff --check` -- no whitespace errors.

**Results (2026-10-11):**
- Approved normative SHA-256 recomputed as `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5`; section 19 names the owner, 4096-byte bound, no open decisions, and Story 6.2 authorization.
- Release solution build passed with 0 warnings and 0 errors. After review patches and the direct-load regression update, the focused Server actor/read/security matrix passed 361/361.
- DomainService project passed 566/566. Admin Server project passed 757, with 18 preexisting skipped DW2 ATDD cases. `bash tools/folded-snapshot-compatibility/run.sh`, `bash -n tools/folded-snapshot-compatibility/run.sh`, and `git diff --check` passed.
- The 5,000-element state measured 23,902 bytes; canonical full envelopes measured 24,207, 24,209, and 24,208 bytes at covered sequences 1, 100, and 1,000. Escaped compatibility metadata rejection preserved its serialized bytes and staged no snapshot.
- The initial full Server project command `timeout 600s dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build` exited 124 without a summary; its 120-second direct-run diagnostic also timed out. After review patches, the same full Server command completed in 7m 44s: 4,400 succeeded, 0 failed, and 25 preexisting DW1 ATDD skips. The review logged 15 earlier findings: seven patched, four rejected after checking, and four unrelated findings deferred.

**Review patch verification (2026-10-11):**
- All 17 `[Review][Patch]` findings above were patched. The final `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -m:1 --no-restore -v:q` passed with 0 warnings and 0 errors after the last code change. The focused actor, manual, snapshot manager, and protected-data matrix passed 150/150 after that change.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Release --no-build` passed 704/704. `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj --configuration Release --no-build` passed 757, with 18 preexisting DW2 ATDD skips. `bash tools/folded-snapshot-compatibility/run.sh` passed the published-package, binary, positional-API, wire, and old-reader checks.
- The final full Server command above passed 4,400/4,425 cases, with 25 preexisting skips and no failures. The approved normative SHA-256 remained `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5`, and `git diff --check` passed.

**Final review verification (2026-10-11):**
- After the review cancellation, evidence-shape and replacement-diagnostic patches, `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` passed with 0 warnings and 0 errors.
- `timeout 600s dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build` passed 4,413, failed 0, skipped 25 existing DW1 ATDD cases. The Client project passed 1,468/1,468, DomainService passed 708/708, and Admin Server passed 757 with 18 existing DW2 ATDD skips; each project ran individually.
- `bash tools/folded-snapshot-compatibility/run.sh`, `bash -n tools/folded-snapshot-compatibility/run.sh`, and `git diff --check` passed. The approved normative SHA-256 recomputed to `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5`.
- Matrix audit: the automatic row ran through `FoldedSnapshotCaptureTests` and `AggregateActorDomainResultTests`; the manual/read row ran through `AggregateActorManualSnapshotTests`, `SnapshotManagerTests`, and protected-data tests; the Admin row ran through `DaprActorQueryServiceTests`. All covering tests are active in the passing project runs.

