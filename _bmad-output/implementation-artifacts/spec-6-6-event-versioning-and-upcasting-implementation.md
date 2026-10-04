---
title: 'Story 6.6: Event Versioning And Upcasting Implementation'
type: 'feature'
created: '2026-10-04'
status: 'in-progress'
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

## Spec Change Log

## Review Triage Log

## Design Notes

One cohesive feature uses the existing four-slice policy. Slice 1 activates no breaking behavior; slice 2 establishes shared prerequisites; slice 3 integrates consumers and safety routes; slice 4 requires full migration/fleet/provider/major compatibility evidence before V2. Dormant preparation cannot claim activation.

## Verification

- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations` — approved inputs, digest and corruption controls pass.
- Individual affected Debug builds/tests with source references; built xUnit assemblies for focused selection. M1–M8 define meaningful unit/live evidence and baseline fallback commands.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` — final package-mode build passes; run all affected project, API/wire/consumer and live-sidecar gates individually.
- `git diff --check` — clean whitespace. Record exact commands/results and source/profile identities under `evidence/story-6-6/`.
