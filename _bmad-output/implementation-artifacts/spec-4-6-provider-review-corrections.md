---
title: 'Story 4.6: Close Provider-Lane Review Findings'
type: 'bugfix'
created: '2026-10-31'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
parent_story_key: '4-6-global-position-sharding-spec-renegotiation'
baseline_commit: 'f5f1e82f2b3ed7f8f5e96ce22a9c141804cb4fc4'
completion_scope: 'isolated-development-lane-only'
production_evidence_qualified: false
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-4-6-provider-commit-ambiguity.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-4-6-isolated-postgresql-recovery.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Ten accepted review findings remain unresolved in the implemented synthetic provider lane. Journal failures can misrepresent persistence outcomes, proxy cleanup can skip independent verification, and documented reruns omit relevant tests.

**Approach:** Close those findings as one reliability correction to the existing experiment. Preserve the accepted two-key transaction scope and verify the corrected failure handling and evidence collection.

## Boundaries & Constraints

**Always:** Keep persistence outcomes distinct from observation failures. Retain original save exceptions when recording diagnostics also fails; incomplete observations must fail the experiment. Preserve the recorded owner approval and downstream authorization. Before live runs bind fresh source/output manifests, environment, canonical exclusive UTC expiry, resource ownership and teardown. Use Debug and existing dependencies; record commands, counts and cleanup results.

**Never:** Change production allocator/contracts/DI, dependencies, approved specification bytes, completed specs or original story/sprint state. Extend qualification to the one-key path, production capacity, backup/restore, HA or physical fencing. Retry ambiguous reservations or infer unchanged state from exceptions. Stage, commit or push this follow-up automatically.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Journal failure | Recording fails before save, after save, or during error/exit reporting | Actual save stage and durable outcome remain distinguishable | Retain original errors; fail incomplete evidence |
| Proxy cleanup | Disposal throws or absence probe fails | Probe still runs; receipt contains outcome and a concrete cause | Continue other owned cleanup; fail visibly |
| Fault experiment | Reservation or fresh admission | Inspect reservation and witness before arming; assert SDK sends before removing instrumentation | Drain request and attempt independent evidence actions on failure |
| Binding | Direct-provider or proxy mode | Proxy mode includes its authorizing phase specification | Preserve direct-mode authority |
| Expiry | Before save or after save starts | Documentation distinguishes known denial from uncertain cancellation | Preserve existing runtime semantics |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Sharding.TestHost/ShardActor.cs`: isolate journal writes from save exception classification and prevent exit reporting from masking failures; reuse `AttemptJournal` and current cache eviction.
- `tests/Hexalith.EventStore.Sharding.Tests/ShardingFixture.cs`: conditional authorization binding and independent proxy disposal/absence checks; retain aggregate cleanup diagnostics.
- `tests/Hexalith.EventStore.Sharding.Tests/ProviderCommitAmbiguityTests.cs`: preserve `FaultExperiment` independent final actions and existing durable witness assertions.
- `tests/Hexalith.EventStore.Sharding.Tests/AttemptJournalTests.cs`: extend persisted-state and execution-stage assertions with deterministic journal failure coverage.
- `tests/Hexalith.EventStore.Sharding.Tests/ShardActorExpiryTests.cs`: correct naming/documentation only.
- `tests/Hexalith.EventStore.Sharding.Tests/README.md`: repair deterministic selection, timing claims, follow-up link and spacing.

## Tasks & Acceptance

**Execution:**

- [ ] `tests/Hexalith.EventStore.Sharding.TestHost/ShardActor.cs` and `tests/Hexalith.EventStore.Sharding.Tests/AttemptJournalTests.cs` -- separate observation failures from save outcomes; cover failures at start, completion, failed-save reporting and actor exit with complete persisted-state assertions.
- [ ] `tests/Hexalith.EventStore.Sharding.Tests/ShardingFixture.cs` and focused fixture tests -- include proxy-phase authorization, always attempt independent absence verification, and diagnose false probes.
- [ ] `tests/Hexalith.EventStore.Sharding.Tests/ProviderCommitAmbiguityTests.cs` -- inspect both durable records before fault arming and assert send counts before instrumentation removal, retaining independent cleanup/evidence actions.
- [ ] `tests/Hexalith.EventStore.Sharding.Tests/ShardActorExpiryTests.cs` -- rename and document denial versus uncertain cancellation.
- [ ] `tests/Hexalith.EventStore.Sharding.Tests/README.md` -- include all six proxy/journal/validator/experiment classes, correct timing claims, link the existing qualification follow-up and remove duplicate blank lines.
- [ ] `_bmad-output/implementation-artifacts/spec-4-6-provider-commit-ambiguity.md` and `_bmad-output/implementation-artifacts/spec-4-6-isolated-postgresql-recovery.md` -- close only verified findings and append fresh results; preserve frozen intent and historical runs.

**Acceptance Criteria:**

- Given each accepted finding, when the final diff and tests are checked, then its correction has attributable evidence and no accepted defect is silently deferred.
- Given corrected source, when deterministic and bounded live regressions run, then complete durable outcomes, execution counts and owned-resource absence remain verified.
- Given phase completion, when protected paths and sprint state are compared with the baseline, then production behavior and parent qualification claims remain unchanged.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Build the sharding test project in Debug with `-p:UseHexalithProjectReferences=true -m:1`, then invoke its built assembly directly with xUnit `-class` selectors. Run all dependency-free classes, including new journal/cleanup cases; run the documented provider-ambiguity, recovery and synthetic regressions with fresh bindings and separate explicit expiry runs. Reuse the Server.Tests build and run `-class '*GlobalPosition*'` for production-v1/local mechanics. Check README formatting and `git diff --check`. Record exact commands, counts, baseline revision, actual expiry and cleanup results here; raw output remains ignored local artifacts. No CI run is implied.
