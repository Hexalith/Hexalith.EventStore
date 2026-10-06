---
title: '6.1-P1R EventStore runtime remediation'
type: 'bugfix'
created: '2026-10-06'
status: 'in-progress'
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

## Spec Change Log

## Review Triage Log

## Verification

Before code edits run the AppHost through the Aspire workflow and record resource state. Build affected test projects individually with `-c Debug -p:UseHexalithProjectReferences=true`; invoke their built xUnit assemblies with single-dash class/method filters. Run affected suites after focused checks. Live sidecar lanes must assert persisted end-state, fresh restart and owned cleanup/shared-resource preservation. Record exact blockers and focused evidence without weakening gates. Actual published Release/package lanes begin only after exact candidate and capable rollback publication authority is supplied.
