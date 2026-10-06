---
title: '6.1-P1R EventStore runtime remediation'
type: 'bugfix'
created: '2026-10-06'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'd24a03569ed0c8e4d773e24e630a2eab2ec9f074'
work_package_id: '6.1-P1R-remediation'
owner: 'EventStore Developer'
owner_scope_approval: 'approved'
scope_approved: '2026-10-06'
repair_source: 'a7404a1d9edf3bf7d125851001c0699ff664abf0'
builds_input: 'ba4ca78c3868a4757cb92d912a54c8a237871b54'
context:
  - '{project-root}/../projects/_bmad-output/implementation-artifacts/spec-6-1-p1r-remediation.md'
  - '{project-root}/../projects/_bmad-output/implementation-artifacts/6-1-p1r-compatibility-scenarios.md'
---

<frozen-after-approval reason="repository-local runtime scope; exact package and release decisions remain separately owned">

## Intent

**Problem:** Historical P1R investigation found seven incompatible scenario families. Current source fixes several defects but still permits invalid writer floors, protected-marker plaintext fallback, incomplete replay validation and loss of signed handler-query admission proof.

**Approach:** Reproduce each family on the pinned current source, repair remaining defects through existing platform seams, and collect new persisted regression evidence. This scope implements EventStore source remediation and prepares qualification; full handoff completion additionally requires authorized published candidate/rollback evidence and owner decisions.

## Boundaries & Constraints

**Always:** Preserve legacy constructors/defaults, compatible calls, event bytes, tenant isolation and supported protected/evolved replay. Use Debug/project references locally, a single writer and invocation-owned fixtures. Keep bookkeeping separate from domain writes. Record base revision plus diff hashes, Builds, fixture and loaded assembly identities in new evidence.

**Never:** Alter sealed verification evidence, acceptance, Projects planning/pins, Builds catalog or readiness. Invent authority/watermarks, infer effect success from null dispatch, or permit incapable old writers. Follow approved AD-17 mutation freeze/forward recovery while no capable rollback family is qualified; never restore away later committed writes. Publication/deployment needs separately bound owner authorization.

## I/O & Edge-Case Matrix

| Family | Input/state | Required behavior |
| --- | --- | --- |
| metadata-read/write | Missing legacy floor; floor 5/head 12/snapshot 9; floor 0 or beyond head+1 | Default to 1; append 13 preserving floor; invalid evidence rejects before allocation/writes. |
| invalid-evidence | Unreadable payload, unknown type, protected-marker mismatch, envelope MetadataVersion 987 | Reject the complete replay batch before Apply/Handle; valid legacy and supported protected/evolved inputs succeed. |
| query-wire | Actor, workload, delegation, scopes, audience and signed proof | Preserve JSON/DataContract authority through handler/projection routing; missing or incapable protected authority rejects before access. |
| projection-wire | Global position 987 and scoped cursor | Preserve exact position and binding; unknown/zero never authorizes a watermark. |
| mixed-api | Legacy calls; fenced/effect calls; recovery null/false/true | Preserve compatible calls and tri-state; persist valid effects, reject stale/unauthorized/unsupported operations honestly. |
| checkout | Pinned source versus historical binaries; later Reminder/evolution additions | Separate already-effective fixes, source defects and immutable incompatibilities; qualify additions independently. |

</frozen-after-approval>

## Code Map

Paths are relative to this repository. Existing floor checks/preservation and `LegacyEventReadGuard` already reject bad reader floors/version 987; reuse them. Unknown-type/unreadable JSON rejection currently happens per event, after earlier Apply calls can run.

- `src/Hexalith.EventStore.Server/Events/{AggregateMetadata,EventStreamReader,EventPersister}.cs`: legacy floor compatibility and writer validation.
- `src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs`: incorrectly returns Readable for protected markers with absent/unprotected metadata; reuse existing unreadable reason taxonomy and metadata validation.
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`: resolve/deserialize the complete supplied replay batch before domain application; preserve registered evolution and protection validation.
- `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs`: enforce complete preflight on the supported direct legacy replay route while retaining cancellation, captured-input ownership, typed failures and detached partial state.
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs`: additive DataContract annotations preserve all existing wire fields and unknown legacy position defaults.
- `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs`: drops IdentityAdmissionProof; concrete Server QueryRouter already forwards it.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`: existing owned sidecars and persisted readback/restart fixtures.

## Tasks & Acceptance

**Execution:**

- [x] `tests/Hexalith.EventStore.Server.Tests/Events/{AggregateMetadata,EventStreamReader,EventPersister}Tests.cs` -- reproduce legacy defaults and invalid evidence; add writer floor refusal before effects; repair EventPersister without changing compatible constructors.
- [x] `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionHookTests.cs` and `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorInfrastructureFailureTests.cs` -- reproduce marker mismatch; repair no-op unprotection; assert zero domain invocation and unchanged event/snapshot/metadata inventory, counting bookkeeping separately.
- [x] `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs` -- prove late invalid type/payload/version/format causes zero Apply/Handle calls; implement rehydrator preflight and retain valid protected/evolution coverage.
- [x] `tests/Hexalith.EventStore.QueryRouting.Tests/HandlerAwareQueryRouterTests.cs` -- preserve signed proof with all authority fields, legacy null and fallback forwarding; repair router.
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Events/P1RRemediationPersistenceTests.cs` -- add owned restore/append/restart coverage: floor 5, head 12, covering snapshot, append 13, prior hashes and second tenant unchanged. Exercise persisted fenced/trusted-effect success and stale/unauthorized denial using existing actor admission fixtures.
- [x] `_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/README.md` -- collect all seven family results, wire/cursor/recovery regressions and independent Reminder/evolution lanes; bind raw receipts and inventories. Record source/package qualification separately and missing/failed/skipped/incompatible evidence as nonpassing.

**Acceptance Criteria:**

- Given malformed retained or replay evidence, when writer/hydration executes, then no new domain event/state mutation or domain application occurs and committed inventories remain unchanged.
- Given a valid restored retained stream, when a capable writer appends and fresh actors restart, then state, floor, sequence, prior hashes and tenant isolation survive.
- Given complete supported authority/capabilities, when routed operations execute, then authenticated evidence and persisted effects survive; incapable routes fail before access/effects.
- Given only source proof, when remediation is reported, then published qualification, exact owner decisions and P1R usability remain pending.

## Implementation Notes

- 2026-10-06: Required planning context resides in the sibling Projects repository; corrected context references without changing those documents. Before implementation, preserved the existing working-tree diff and SHA-256 inventory in `/tmp/p1r-runtime-baseline-4q6e8au1`. Existing replay/cancellation/evolution changes are user work and must be preserved.

- 2026-10-06: Implemented retained-floor refusal, no-op protection refusal, complete hydration/direct replay preflight and signed handler-query forwarding. The required wire regression also reproduced an absent projection DataContract; added compatible annotations without changing constructors. New [source evidence](evidence/6-1-p1r-remediation/README.md) keeps package qualification, owner decisions and usability pending.

- 2026-10-06 review patches: detached supported replay inputs before converters, checked complete V1 provenance and case-insensitive JSON metadata aliases, scanned protected snapshot dictionary/JSON format entries, and made owned cleanup continue after individual failures while retaining nonpassing summaries. Added converter observation hooks to the two existing cancellation fixtures without changing their prior behavior. All 15 findings were individually triaged; six pre-existing issues were appended to `deferred-work.md`.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Blind 1: V1 metadata with versioned provenance | high | patch | `PrepareContractEventEnvelope` validates only version/format; the reproduced V1 envelope with non-null contract type/payload version reaches Apply. Add the same contradictory-V1 refusal already used by `LegacyEventReadGuard`, before deserialization. |
| Blind 2: converter replaces later caller-owned envelope | high | patch | Tail preparation interleaves source-list reads with converters; the probe replaces an unknown second event during the first conversion and both apply. Capture references and private envelope payload bytes before conversion; this is a local correction to the introduced preflight with no public API change. |
| Blind 3: inconsistent tail sequence/head | high | defer | The probe reproduces gaps and advertised-head mismatch, but the same missing validation exists in the captured pre-build rehydrator. Production actor callers obtain the tail from `EventStreamReader`; additional direct-client stream consistency admission is pre-existing work. |
| Blind 4: cross-identity command hydration | high | defer | Direct `ProcessAsync` can apply foreign envelopes because neither existing processor passes command identity into the rehydrator. This is unchanged from the captured baseline; the production actor reads by command aggregate identity. A separate change must bind direct hydration envelopes to command identity. |
| Blind 5: null enumerable entries skipped | medium | defer | The enumerable branch still skips null exactly as in the captured pre-build implementation. Consistent direct-input scalar admission is a pre-existing defect, rather than a regression introduced by whole-batch preparation. |
| Blind 6: negative direct replay target succeeds | medium | defer | `LegacyReplayInput` filters every event out for a negative target and returns successful empty replay. Its hash matches the pre-build inventory; scalar target admission was already absent and was not changed by this remediation. |
| Blind 7: dictionary protected snapshot accepted | medium | patch | The public snapshot hook accepts object carriers, including the reproduced dictionary, while the new marker guard checks only typed V2/JSON carriers. Inspect dictionary format entries before granting readability without serializing arbitrary business state. |
| Blind 8: conflicting snapshot format aliases | high | patch | Lowercase `format=json` short-circuits lookup of `Format=json+pdenc-v2`, so the reproduced JSON carrier is readable. Inspect every case-insensitive format entry and refuse if any carries a protected marker. |
| Blind 9: first cleanup failure aborts cleanup/receipt | medium | patch | `command` raises on inspect/remove failure inside `finally`, preventing later owned cleanup and receipt writing. Catch each owned cleanup/inspection failure, continue all owned attempts and preserve a failing summary; retain exact label checks. |
| Blind 10: absent DomainService test receipts | false | reject | The producer and both producer-test changes predate this build: their hashes exactly match the captured user inventory. This remediation changed no DomainService file, so the asserted missing affected-project verification does not apply to its source changes; the source binding identifies those pre-existing edits explicitly. |
| Edge 1: conflicting snapshot format aliases | high | patch | Independently reproduced the same readable protected snapshot as Blind 8; group these findings under the format-marker scan correction. |
| Edge 2: cancelling/throwing termination getter | medium | defer | The getter invocation is unchanged from the pre-build `EventStoreAggregate` hash and can report its non-cancellation exception after cancelling the token. This belongs to the existing cancellation implementation and needs a separate getter-boundary regression. |
| Edge 3: PascalCase JSON version/format ignored | high | patch | JSON preflight reads only camelCase metadata, although payload serialization accepts case-insensitive names. The probe supplies PascalCase version 987/protected format and invokes Apply; resolve metadata aliases case-insensitively and reject contradictory duplicates before conversion. |
| Edge 4: V1 metadata with versioned provenance | high | patch | Independently reproduced Blind 1; the existing direct replay/writer checks confirm that V1 versioned provenance is malformed. Group under complete application-metadata preflight. |
| Verification gap 1: private-copy scratch boundary untested | medium | defer | Trust the reviewer's filed test/search evidence: 42/43 one-MiB events isolate the new scratch ceiling whereas existing cases do not. The producer and tests are byte-identical to the captured pre-build user changes; add that focused boundary regression in their separate work. |

Patch resolution: the same implementation agent corrected every patch group. Parent focused verification passes 147 Client cases, 377 Server runtime cases and 135 authority/wire/recovery cases with no skips; eight mocked cleanup controls verify continued cleanup and retained failure summaries. The final live invocation `live-attempt-e64c1b50` passes all five required tests, reports no cleanup errors or remaining owned resources, and preserves all shared-container identities/state. The false finding was rejected on the recorded baseline evidence, and every defer entry was appended separately.

## Verification

Before code edits run the AppHost through the Aspire workflow and record resource state. Build affected test projects individually with `-c Debug -p:UseHexalithProjectReferences=true`; invoke their built xUnit assemblies with single-dash class/method filters. Run affected suites after focused checks. Live sidecar lanes must assert persisted end-state, fresh restart and owned cleanup/shared-resource preservation. Record exact blockers and focused evidence without weakening gates. Actual published Release/package lanes begin only after exact candidate and capable rollback publication authority is supplied.

- 2026-10-06 parent acceptance audit: verified all six execution tasks and all seven matrix families against the source diff, tests and retained receipts. Client 1,039, Contracts 2,252 (two package-inventory skips), Server 3,758 (25 pre-existing DW1 skips), QueryRouting 19 and five independent live tests have zero failures in their final runs. All required source regressions executed without skips. Source/configuration and receipt output hashes match; the 146-file packet checksum and whitespace checks pass. The approved frozen block and historical sealed evidence are unchanged. Source proof leaves package qualification, owner decisions and P1R usability pending.

- 2026-10-06 final parent verification after review patches: five individual Debug/project-reference builds have zero warnings/errors. Client 1,056, Contracts 2,252 (two package-inventory skips), Server 3,769 (25 pre-existing DW1 skips), QueryRouting 19 and five independent live tests report 7,074 passed tests, no failures and 27 existing nonpassing skips. Eight additional mocked cleanup controls pass. All required source regressions execute without skips. Review is complete with all patch groups corrected and six pre-existing findings deferred. The completion workflow's local commit includes required replay/cancellation prerequisites; independent pre-existing producer/controller/evolution-outcome changes remain outside that commit. Published qualification and owner decisions remain pending.
