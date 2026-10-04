---
title: 'Story 6.6: Event Versioning And Upcasting Implementation'
type: 'feature'
created: '2026-10-04'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
story_key: '6-6-event-versioning-and-upcasting-implementation'
planning_revision: '2242ad55a1b678828df8aa093fd92399c29af5bf'
baseline_commit: '1329b35e52852952ecb2c94aabf100674e9691e3'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/6-6-implementation-map.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Persisted/wire events lack stable versioned identity, and consumers have no shared authenticated evolution pipeline. Processing and cancellation differ across durable boundaries.

**Approach:** Implement the approved AD-13 design in `spec-event-versioning-upcasting.md`, through its four ordered slices, across writers, readers, consumers and recovery.

## Boundaries & Constraints

**Always:** Follow approved schemas, budgets, signing purposes, outcomes and activation order exactly. Preserve stored bytes, MessageIds, actor commits, last-good state and compatibility adapters. Propagate cancellation to the defined durable boundary. Verify every consumer through one allow-listed reader. Material design changes return to the owner.

**Scope:** Local implementation, tests and existing CI gates, including one Npgsql `10.0.3` central pin in `references/Hexalith.Builds/Props/Directory.Packages.props` and a versionless Server reference. This named submodule edit is part of the approval scope; inspect its owning guidance first.

**Never:** Rewrite history, re-execute committed commands for publication resume, invent provider authority, relax gates or modify unrelated dependencies. Snapshot/projection-cost redesign and Epic 8 protection are excluded. Deployment, publication and offline retained-data migration require their existing separate authority; absent production qualification keeps activation fenced.

## I/O & Edge-Case Matrix

| Input/state | Required behavior |
| --- | --- |
| Current, legacy or mixed history | Canonical effective state; immutable stored evidence |
| Invalid identity/chain, unreadable payload or unavailable evidence | Approved typed outcome; no partial state, checkpoint or handler effect |
| Cancellation before/after commit | No pre-commit mutation; preserve committed truth and bounded recovery |
| Retry, resume, owner takeover or rollback | Same durable identities; fenced authority, idempotent effects and receipts |

</frozen-after-approval>

## Code Map

[Implementation map](6-6-implementation-map.md) M1–M8 records exact existing/new files, reusable symbols, normative sections, test anchors and gates. AD-13's approved digest is `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`; its preflight passed.

## Tasks & Acceptance

**Execution, in dependency order:**

- [ ] `src/Hexalith.EventStore.Contracts/Events/`, `src/Hexalith.EventStore.Client/Events/` — M1: add compatible metadata, registry, codecs and bounded upcaster interfaces; preserve constructors and legacy wire behavior.
- [ ] `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs`, `src/Hexalith.EventStore.Server/Events/`, `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` — M2: bounded evidence-writing V1 producer, exact actor save/readback and authenticated shared reader; keep slice 1 additive. Update invoker streaming before admission.
- [ ] `src/Hexalith.EventStore.Server/Control/`, `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj`, named Builds pin — M3: shared PostgreSQL adapter, fences, quota/queue, registry/epoch and scope claims before hold producers; V2 stays dormant.
- [ ] `src/Hexalith.EventStore.Contracts/Replay/`, `src/Hexalith.EventStore.Client/Handlers/`, `src/Hexalith.EventStore.DomainService/DomainQueryDispatcher.cs` — M4: private paged replay, async processing, verified reconstruction and scoped query intake; propagate original tokens and fence versioned cache access.
- [ ] `src/Hexalith.EventStore.Server/Projections/`, `src/Hexalith.EventStore.Client/Projections/`, `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs` — M5: verified full/incremental dispatch, certified named generations, checkpoint and query visibility; preserve legacy key ownership.
- [ ] `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`, `src/Hexalith.EventStore.Client/Subscriptions/` — M6: exact carriers/pins, membership and effect receipts; verify before marker/handler and preserve first-response/status authority.
- [ ] `src/Hexalith.EventStore/Controllers/`, `src/Hexalith.EventStore.Operations/`, `src/Hexalith.EventStore.Admin.UI/` — M7: signed same-event resume, capture/redrive, hold inventory and safe diagnostics; audit SDK/CLI/Admin filters and activate only approved slice changes.
- [ ] `tests/`, `scripts/verify-event-evolution.py`, `.github/workflows/ci.yml` — M8: production-path matrices, timed mutation checks, API/wire/package consumers and two-host provider evidence; close O-01–O-20 and owned follow-ups before final V2/major gate.

**Acceptance Criteria:**

- Given the reviewed inputs and owner request, when preflight runs, then current approval/input checks pass and implementation traces to the approved sections.
- Given any supported history, when each consumer executes, then effective objects and persisted aggregate/projection end-state equal the canonical baseline with original bytes unchanged.
- Given invalid or cancelled work, when each durable boundary is exercised, then typed outcomes preserve last-good truth and produce zero forbidden mutation or disclosure.
- Given concurrent recovery and mixed fleets, when crash/rollback matrices run, then source evidence, fences, receipts and stable identities prevent duplicate effects or incompatible admission.
- Given completion, when all affected regressions, compatibility and provider lanes run, then required checks pass without unexpected skips; only proven obligations close. Missing production authority or required evidence leaves the story incomplete.

## Implementation Notes

- This run adds bounded payload/scratch primitives, event-view contracts, an authenticated raw-page contract, metadata tuple validation, and metadata-preserving actor mapping. The M1 registry/codecs, M2 bounded producer/authenticated reader, and M3–M8 runtime integrations and evidence are not complete; no task checkbox is marked complete.
- The workflow baseline is `1329b35e52852952ecb2c94aabf100674e9691e3`. During the run, `HEAD` advanced concurrently to `f3dc36b934336920b3c7e4bcec5cf52c6327df9d`; that commit and its unrelated Story 6.1 changes were preserved as external input.
- `VerifiedEffectiveCommandEvent.cs` was marked assume-unchanged and differed from `HEAD`. The implementation agent added its constructor validation before checking that path's original bytes. No pre-edit copy is available, so its current worktree content is preserved and the possibility of overlapping hidden local edits remains unresolved.
- Aspire was started for baseline inspection and stopped after dependent services remained waiting on Keycloak. No provider or live-sidecar qualification was obtained. The Builds package pin differs from its submodule `HEAD` and was left unchanged.

## Spec Change Log

- 2026-10-04 — Started Story 6.6 on owner request; recorded baseline `1329b35e52852952ecb2c94aabf100674e9691e3`, moved status to `in-progress`, and captured bounded implementation progress and remaining M1–M8 scope.
- 2026-10-04 — Review pass corrected metadata validation, event metadata mapping, raw-page gap/budget/ownership handling, and focused coverage; production integration and provider qualification remain outstanding.

## Review Triage Log

- [defer] `IEventUpcaster`/`IV1Downserializer` have no registry or execution pipeline (Blind Hunter 1; Edge Case Hunter 4). The baseline had no shared evolution pipeline, and the added interfaces do not change runtime behavior; M1 remains incomplete and this capability must be implemented before the story can be done.
- [defer] Domain-service invocation does not negotiate writer mode/fingerprint or authenticate raw-source reads (Blind Hunter 2 and 9). These runtime paths were absent before this partial implementation; M2 remains incomplete and activation is still fenced.
- [medium / patch, resolved] `EventPersister` accepted invalid metadata versions and partial identity pairs (Blind Hunter 3; Edge Case Hunter 1). It now validates the V1/V2 tuple before state reads, protection, global-position reservation, or writes; `EventPersisterTests` passed 38/38.
- [false] `VerifiedEffectiveCommandEvent` lacks basic validation (Blind Hunter 4). The current constructor validates positive sequence, exact digest/signature sizes, canonical contract identity, bounded payload version, serialization format, and route key ID.
- [false] `EventContractIdentityValidator` rejects no consecutive-hyphen rule (Blind Hunter 5; Edge Case Hunter 2). The approved schema explicitly allows repeated interior hyphens and accepts any lower-case alphanumeric endpoint.
- [medium / patch, resolved] `AuthenticatedRawEventPage` accepted an empty page while `StartSequence <= ActorHead` (Blind Hunter 6). It now rejects this gap; bounded nonempty pages remain valid. Focused page tests passed 5/5.
- [medium / patch, resolved] `AuthenticatedRawEventPage` did not charge optional evidence payloads against the raw source budget (Blind Hunter 7; Edge Case Hunter 3). It now caps envelopes and sidecars together at 128 MiB and exposes the separate 64 MiB post-unprotection check; production reader integration remains in the deferred M2 work.
- [medium / patch, resolved] `AuthenticatedRawEventPage` retained caller-owned `IReadOnlyPayload` references (Blind Hunter 8). It now takes private copies after checking the aggregate source budget; mutation-isolation tests passed in the focused 5/5 page run.
- [low / rejected] `DomainServiceWireEvent.Payload` remains a mutable array (Blind Hunter 10). This was already the positional DTO contract before version metadata was added, and the reviewed production response path introduces no additional shared owner; a compatibility-preserving ownership wrapper would add complexity for a non-routine misuse.
- [medium / patch, resolved] No focused test proved version metadata survives invoker conversion and persistence (Verification Gap primary finding). A focused invoker-to-persister route test now asserts the persisted triplet; `DaprDomainServiceInvokerTests` passed 40/40.
- [medium / patch, resolved] `AggregateActor.ToContractEventEnvelope` dropped the new event contract type and payload version when building domain-service current state (Verification Gap other finding). The mapping now copies both values; `AggregateActorDomainResultTests` passed 32/32.
- [false] Sidecar evidence exceeding the readable payload ceiling (Edge Case Hunter 3). Raw/source evidence and readable payloads have distinct approved budgets; the page charges raw envelopes plus sidecars against 128 MiB, while `ValidateReadablePayloadBytes` checks post-unprotection bytes against 64 MiB. The missing production caller for that check is tracked in the deferred M2 work.

## Design Notes

One cohesive feature uses the existing four-slice policy. Slice 1 activates no breaking behavior; slice 2 establishes shared prerequisites; slice 3 integrates consumers and safety routes; slice 4 requires full migration/fleet/provider/major compatibility evidence before V2. Dormant preparation cannot claim activation.

## Verification

- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations` — passed; approved digest `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`; 20 obligations and 47 implementation follow-ups remain open.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, 0 warnings/errors. Focused built-DLL classes: EventPersisterTests 38/38, DaprDomainServiceInvokerTests 40/40, AggregateActorDomainResultTests 32/32.
- `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, 0 warnings/errors; BoundedPayloadPrimitiveTests 4/4.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed, 0 warnings/errors; AuthenticatedRawEventPageTests 5/5. A full Contracts assembly run before the final page-test additions passed 2,187 tests with 2 package-inventory skips because `EVENTSTORE_PACKAGE_CONTRACT_DIR` was unset.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` — passed, 0 warnings/errors.
- `git diff --check 1329b35e52852952ecb2c94aabf100674e9691e3..HEAD` and `git diff --check` — passed.
- The `dotnet test` MTP invocation for Contracts.Tests reported zero tests and exit code 5; focused and full Contracts execution used the built xUnit assembly. Aspire dependents waited on Keycloak; no live-sidecar/provider run, full affected regression set, package/API compatibility gate, or two-host qualification completed. The acceptance matrix therefore remains open and the story is incomplete.
- Exact focused evidence and the environment limitation are recorded in `evidence/story-6-6/verification-2026-10-04.md`.
