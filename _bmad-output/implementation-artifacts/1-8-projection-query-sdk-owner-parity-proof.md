---
title: "1.8 Projection/Query SDK Owner Parity Proof"
type: "proof"
created: "2026-07-10"
status: "done"
baseline_commit: "f31777ae8dd3902f65a27777a04ee49d790a6e8f"
source_proposal: "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-10.md"
trigger_artifact: "../parties/_bmad-output/implementation-artifacts/8-6-ac1-llm-instructions.md"
---

# Story 1.8: Projection/Query SDK Owner Parity Proof

## Story

As an EventStore platform owner,
I want reviewed proof that the projection/query SDK can replace a non-trivial domain's local projection/query mechanics,
so that consuming modules can delete local rollback code only after EventStore owner evidence proves parity.

## Acceptance Criteria

1. Given the proof story starts, when repository context is loaded, then the current EventStore commit SHA intended for consuming modules is recorded.
2. Given the SDK proof is prepared, when source surfaces are inspected, then `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, `QueryCursorScope`, and domain-service projection/query registration APIs are cited by source path.
3. Given Parties Story 8.6 AC1 lists required proof items, when each item is evaluated, then G3 read-model erasure hooks, G10 index batching or approved equivalent, G6 freshness mapping, duplicate/out-of-order replay behavior, full rebuild verification, cursor scope compatibility, and the intended EventStore pin are each classified as `already available`, `additive API/test added`, or `blocked`.
4. Given any required proof item is not satisfied, when the proof packet is produced, then the final decision is `still blocked`, the missing API or behavior is named precisely, and no consuming story is authorized to mark the projection/query SDK row `available`.
5. Given additive code is needed, when implementation changes are made, then they remain generic EventStore SDK capabilities and do not add Parties-specific domain logic to EventStore.
6. Given a required proof item is satisfied, when validation evidence is recorded, then source paths, test paths, validation commands, and validation results are cited for that item.
7. Given every required item is satisfied, when the proof packet is finalized, then it records owner approval source, rollback note, known limitations, EventStore commit SHA, and final decision `available`.
8. Given the owner proof is available, when a consuming repo records it in its prerequisite matrix, then the consuming repo must still verify its checked-out EventStore pin matches the approved SHA before source migration or local rollback deletion starts.

## Required Proof Items

- G3 read-model erasure hooks.
- G10 index batching or approved SDK equivalent.
- G6 freshness mapping for `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`.
- Duplicate and out-of-order replay behavior.
- Full rebuild verification against aggregate replay.
- Cursor scope compatibility through `IQueryCursorCodec` and `QueryCursorScope`.
- Current EventStore commit SHA intended for consuming modules.

## Tasks

- [x] Record repository and proof context.
  - [x] Run `git rev-parse HEAD`.
  - [x] Read the EventStore project instructions and this story before source inspection.
  - [x] Preserve root-declared submodule discipline; do not run recursive submodule commands.
- [x] Inspect the SDK surfaces and classify each required proof item.
  - [x] Inspect projection/query handler seams and domain-service endpoint registration.
  - [x] Inspect read-model store, write policy, DAPR implementation, and in-memory testing fake.
  - [x] Inspect cursor codec/scope implementation and tests.
  - [x] Inspect projection update, rebuild, duplicate, out-of-order, and freshness-related tests.
- [x] Add or identify evidence.
  - [x] If already available, cite source paths, test paths, and validation commands.
  - [x] If additive code is required, implement only generic EventStore SDK capability and add focused tests.
  - [x] If blocked, stop and name the missing API or behavior precisely.
- [x] Produce the proof packet at `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md`.
  - [x] Include EventStore commit SHA.
  - [x] Include owner approval source.
  - [x] Include evidence by required proof item.
  - [x] Include rollback note and known limitations.
  - [x] Set final decision to `available` only when every item is satisfied; otherwise set `still blocked`.

### Review Findings

- [x] [Review][Patch] Classify the intended EventStore pin as a required proof item [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:28]
- [x] [Review][Patch] Reclassify G10 because sequential single-key writes do not prove batching or an approved equivalent [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:60]
- [x] [Review][Patch] Reclassify duplicate/out-of-order replay because the cited evidence does not prove projection-path idempotency [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:99]
- [x] [Review][Patch] Name the paged rebuild overwrite defect as a concrete full-rebuild blocker [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:121]
- [x] [Review][Patch] Record that the synchronous projection handler cannot call the asynchronous read-model persistence seam [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:169]
- [x] [Review][Patch] Record that domain-only routing and a single response cannot express both Parties projection handlers [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:180]
- [x] [Review][Patch] Expand the G3 blocker to include companion sequence/checkpoint erasure [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:41]
- [x] [Review][Patch] Mark proof-result owner approval as pending instead of reusing proposal authorization [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:4]
- [x] [Review][Patch] Correct the cursor evidence because unrelated key rings model key loss, not normal key rotation [_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md:144]

## Proof Packet Template

```markdown
# EventStore Projection/Query SDK Owner Proof Packet

- EventStore commit SHA:
- Owner approval source:
  - PR:
  - reviewer:
  - approval date:

## Evidence by Requirement

### Intended EventStore pin
- classification:
- source paths:
- test paths:
- validation command:
- result:

### G3 read-model erasure hooks
- classification:
- source paths:
- test paths:
- validation command:
- result:

### G10 index batching or approved equivalent
- classification:
- source paths:
- test paths:
- validation command:
- result:

### G6 freshness mapping
- classification:
- source paths:
- test paths:
- validation command:
- result:

### Duplicate and out-of-order replay
- classification:
- source paths:
- test paths:
- validation command:
- result:

### Full rebuild verification
- classification:
- source paths:
- test paths:
- validation command:
- result:

### Cursor scope compatibility
- classification:
- source paths:
- test paths:
- validation command:
- result:

## Rollback Note

## Known Limitations

## Final Decision

`available` or `still blocked`
```

## Dev Notes

This story exists because checked-out EventStore source files alone are not enough to unblock Parties Story 8.6 AC1. The consuming matrix row may become `available` only after the owner proof packet exists, every AC1 proof item is covered, and the consuming repo verifies the checked-out `references/Hexalith.EventStore` pin matches the approved EventStore SHA.

Initial correct-course inspection found these likely gaps or classifications to verify:

- `IReadModelStore` exposes `GetAsync`, `SaveAsync`, and `TrySaveAsync`; no public delete/erase hook was identified.
- `ReadModelWritePolicy` supports optimistic retry, `ApplyEventsAsync`, and `MergeAsync`, including multi-key aggregate/index write patterns in tests; no explicit public batch API was identified.
- `ReadModelFreshnessState` covers `Unknown`, `Current`, `Aging`, and `Stale`; it does not directly model all Parties states `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`.
- `QueryCursorCodecTests` and `QueryCursorScopeTests` strongly cover cursor opacity, wrong scope, wrong query type, tamper/key rotation, size limit, and purpose isolation.

Do not add Parties-specific domain logic to EventStore. If a proof item needs a domain adapter, either prove the adapter as consumer-owned or mark the owner proof `still blocked` with the exact missing EventStore API.

## Validation

Run focused validation first, then the broadest practical EventStore validation lane:

```bash
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.Testing.Tests/
dotnet test tests/Hexalith.EventStore.DomainService.Tests/
dotnet test tests/Hexalith.EventStore.Server.Tests/
dotnet build Hexalith.EventStore.slnx --configuration Release
git diff --check
```

If a broad lane is environment-blocked, record the exact blocker separately from the focused evidence that ran.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-10.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/prd.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`
- `src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs`
- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`
- `src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs`
- `src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`
- `src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs`
- `../parties/_bmad-output/implementation-artifacts/8-6-ac1-llm-instructions.md`
- `../parties/_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
- `../parties/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan

- Load EventStore owner context, current sprint tracking, and Parties AC1 gate artifacts before source inspection.
- Inspect the projection/query SDK seams and focused tests, classifying each required proof item as already available, additive API/test added, or blocked.
- Prefer proof-only evidence unless a generic EventStore SDK change is clearly justified; do not add Parties-specific logic.
- Produce a proof packet with source paths, test paths, validation commands, rollback note, known limitations, and a final decision.

### Debug Log References

- 2026-07-10T14:03:47+02:00 - Ran `git rev-parse HEAD`; EventStore SHA recorded as `f31777ae8dd3902f65a27777a04ee49d790a6e8f`.
- 2026-07-10T14:03:47+02:00 - Marked sprint status for `1-8-projection-query-sdk-owner-parity-proof` from `ready-for-dev` to `in-progress`.
- 2026-07-10T14:03:47+02:00 - Loaded EventStore instructions, project context files, Git instructions, sprint status, story file, and Parties AC1 gate artifacts.
- 2026-07-10T14:03:47+02:00 - Inspected SDK source surfaces for projection/query handlers, read-model store/write policy, cursor codec/scope, registration APIs, freshness metadata, replay, and rebuild orchestration.
- 2026-07-10T14:03:47+02:00 - Ran focused validation and Release build; all executed lanes passed.
- 2026-07-10T14:03:47+02:00 - Produced `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md` with final decision `still blocked`.
- 2026-07-10T15:41:22+02:00 - Applied code-review corrections: reclassified unsupported G10 and replay claims, recorded additional projection-seam blockers, made rebuild paging risk explicit, and corrected approval, pin, erasure, and cursor evidence.

### Completion Notes

- Recorded the intended EventStore consuming SHA: `f31777ae8dd3902f65a27777a04ee49d790a6e8f`.
- Cited all required SDK source surfaces, including `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, `QueryCursorScope`, and domain-service registration/dispatch APIs.
- Classified the intended EventStore pin and cursor scope compatibility as already available. G3 read-model erasure, G10 batching/equivalent, G6 freshness mapping, duplicate/out-of-order projection replay, and full rebuild verification remain blocked.
- Recorded additional blocking constraints: the synchronous projection handler cannot safely use the asynchronous read-model store, domain-only routing cannot register both Parties projection handlers, and paged rebuild delivery can overwrite full-replay state with a partial page.
- No additive SDK code was implemented; all identified blockers are named precisely per AC4, and the proof packet explicitly does not authorize consuming repos to mark the prerequisite row `available`.

### File List

**Added**
- `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md`

**Modified**
- `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-parity-proof.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-10 | 0.1 | Produced the EventStore projection/query SDK owner proof packet and recorded final decision `still blocked` because G3 erasure hooks, G6 full freshness mapping, and full rebuild verification against aggregate replay are not fully satisfied. | GPT-5 Codex |
| 2026-07-10 | 0.2 | Applied adversarial review corrections and expanded the `still blocked` decision with unsupported G10/replay classifications and concrete projection-seam limitations. | GPT-5 Codex |
