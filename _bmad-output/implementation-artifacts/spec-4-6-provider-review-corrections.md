---
title: 'Story 4.6: Close Provider-Lane Review Findings'
type: 'bugfix'
created: '2026-10-31'
status: 'done'
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

- [x] `tests/Hexalith.EventStore.Sharding.TestHost/ShardActor.cs` and `tests/Hexalith.EventStore.Sharding.Tests/AttemptJournalTests.cs` -- separate observation failures from save outcomes; cover failures at start, completion, failed-save reporting and actor exit with complete persisted-state assertions.
- [x] `tests/Hexalith.EventStore.Sharding.Tests/ShardingFixture.cs` and focused fixture tests -- include proxy-phase authorization, always attempt independent absence verification, and diagnose false probes.
- [x] `tests/Hexalith.EventStore.Sharding.Tests/ProviderCommitAmbiguityTests.cs` -- inspect both durable records before fault arming and assert send counts before instrumentation removal, retaining independent cleanup/evidence actions.
- [x] `tests/Hexalith.EventStore.Sharding.Tests/ShardActorExpiryTests.cs` -- rename and document denial versus uncertain cancellation.
- [x] `tests/Hexalith.EventStore.Sharding.Tests/README.md` -- include all six proxy/journal/validator/experiment classes, correct timing claims, link the existing qualification follow-up and remove duplicate blank lines.
- [x] `_bmad-output/implementation-artifacts/spec-4-6-provider-commit-ambiguity.md` and `_bmad-output/implementation-artifacts/spec-4-6-isolated-postgresql-recovery.md` -- close only verified findings and append fresh results; preserve frozen intent and historical runs.

**Acceptance Criteria:**

- Given each accepted finding, when the final diff and tests are checked, then its correction has attributable evidence and no accepted defect is silently deferred.
- Given corrected source, when deterministic and bounded live regressions run, then complete durable outcomes, execution counts and owned-resource absence remain verified.
- Given phase completion, when protected paths and sprint state are compared with the baseline, then production behavior and parent qualification claims remain unchanged.

## Implementation Notes

Approved by the user in this session on 2026-10-31. The original baseline identifier is preserved. Concurrent Git work rebased the earlier security commit to `84f463af17dac5b55910f8b352f781c13143d745`, then recorded these corrections as `7ead2f24547c0669b86686340c58c83d883157be`. The exact correction source range is `84f463af17dac5b55910f8b352f781c13143d745..7ead2f24547c0669b86686340c58c83d883157be`; adjacent committed production changes belong to the concurrent session. This follow-up performed no staging, commit, merge, push or release. Final verification records remain ordinary working-tree edits. The publication coordination hold is honored.

All ten previously accepted findings are corrected. Journal observation failures carry their actual stage; secondary journal failures remain attached to the original exception. Tests inspect both persisted keys, pending state, a sentinel and the next distinct reservation. Cleanup probes run independently of disposal and false probes carry explicit causes. SDK sends are asserted as an independent final action before instrumentation removal. The README selects every relevant proxy/journal/validator/experiment/cleanup class.

## Spec Change Log

## Review Triage Log

### Independent correction review — 2026-10-31

All three context-free layers completed before triage. Edge-case review returned no findings. Blind review reported nine findings (floor: min(floor(sqrt(66.329) + 1), 10) = 9); verification review reported one regression gap. Every finding is retained below before grouping.

| Finding | Verdict / route | Evidence |
|---|---|---|
| Blind 1: cache eviction can replace the original journal/save error | low / reject | A second state-manager cleanup exception could replace it, but both paths still fault the experiment. This unreproduced combination supplies no accepted reservation or incorrect durable-success claim; adding another exception-policy guard is more than a direct correction for an uncommon diagnostic failure. |
| Blind 2: exception Data has no proven remoting retention | low / reject | The correction guarantees in-process original exception identity and retained malformed/incomplete journals fail validation. It does not claim secondary Data is serialized by Dapr. A new transport diagnostic mechanism would add complexity for combined observation failures rather than correct a demonstrated successful-but-invalid experiment. |
| Blind 3: filesystem failure remains active through actor exit | low / reject | The persistent failure deliberately exercises the original masking defect and each primary stage is asserted separately. No bad behavior was demonstrated for a later successful append; adding transient-failure permutations is coverage expansion rather than a direct correction. |
| Blind 4: partial write/flush failure lacks a dedicated test | low / reject | Current tests inject real append I/O failure; the same RecordAttempt catch classifies later I/O errors. A partial line is rejected on journal parsing and a missing stage by the validator. Dedicated stream/flush injection needs additional test mechanisms without a demonstrated escaping failure. |
| Blind 5: empty ledger and acknowledged-success registration can mask the negative test's rejection reason | medium / patch | Stage validation currently runs before COMMIT reconciliation, but the broad exception assertion could stay green after stage checking regresses. Add an otherwise-valid positive accounting control and assert the exact stage-error category for the incomplete observations. |
| Blind 6: disposal-failure fixture has no accepted session | false / reject | The already-executed AcceptFailureStillDrainsPreviouslyAcceptedSessionAsync covers an accepted stalled session and accept failure, asserting zero active/one drained. The new fixture test adds independent listener probing and receipt/scratch assertions after disposal fails; the combined coverage already exercises both responsibilities. |
| Blind 7: authorization helper test does not observe persisted binding | medium / patch | Confirmed: changing initialization to AuthorizationFor(false) leaves helper tests green. Assert the actual retained binding in the proxy and direct-provider live callers. Group with Verification 1. |
| Blind 8: generic final-action test does not lock private caller ordering | low / reject | FaultExperimentTests verifies independent evidence actions after failures, not private caller instruction placement. Current FaultAsync runs its independent SDK assertion before CompleteCommitFaultAsync and retained request observations record sends before instrumentation removal. Additional order instrumentation would mirror that implementation rather than fix an observed bad execution. |
| Blind 9: predecessor update contradicts completed-spec prohibition | false / reject | Both target phase files have status in-progress, with the accepted patch rows still open at baseline. The user-approved execution task explicitly names findings closure and verification appends there. No status-done spec or either frozen phase intent is changed. |
| Verification 1: generated proxy binding lacks authorization assertion | medium / patch | Accepted as filed: the helper is tested, but the live caller never asserts Configuration["authorization"] in retained binding.json. Group with Blind 7 and add actual serialized binding assertions. |

Surviving groups: the isolated negative validator regression and generated-binding assertions. Both need only focused test edits; neither changes runtime behavior, public surface or the approved intent. No finding is deferred.


## Verification

Build the sharding test project in Debug with `-p:UseHexalithProjectReferences=true -m:1`, then invoke its built assembly directly with xUnit `-class` selectors. Run all dependency-free classes, including new journal/cleanup cases; run the documented provider-ambiguity, recovery and synthetic regressions with fresh bindings and separate explicit expiry runs. Reuse the Server.Tests build and run `-class '*GlobalPosition*'` for production-v1/local mechanics. Check README formatting and `git diff --check`. Record exact commands, counts, baseline revision, actual expiry and cleanup results here; raw output remains ignored local artifacts. No CI run is implied.

### Executed verification — 2026-10-31

Both Debug builds passed with zero warnings and zero errors. Direct xUnit assembly runs passed all 143 distinct selected cases: 97 deterministic, 10 live provider/recovery/synthetic/startup, two explicit expiry cases and 34 global-position regressions. Nothing was skipped or not run in those selections. README markdownlint and `git diff --check` passed. The first deterministic attempt had 96 passed/one failed because the extended rejection test inherited the save-failure fake's default; explicitly selecting the fake's intended behavior corrected that test setup. The original failed log is retained. Final runs all passed. No CI run exists for this local validation.

Exact commands, from the repository root:

```bash
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*BindingTests' -class '*FixturePreflightTests' -class '*TransportLifecycleTests' -class '*ShardActorExpiryTests' -class '*ShardActorRecoveryTests' -class '*PostgresFaultProxy*' -class '*AttemptJournalTests' -class '*ShardAttemptValidatorTests' -class '*FaultExperimentTests' -class '*FixtureProxyCleanupTests'
EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T22:56:42Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderCommitAmbiguityTests' -class '*ProviderRecoveryTests' -class '*SyntheticShardingTests' -class 'Hexalith.EventStore.Sharding.Tests.LifecycleTests'
EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T22:41:25Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderProxyExpiryTests' -explicit only
EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T22:41:40Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ExpiryLifecycleTests' -explicit only
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPosition*'
node /home/administrator/.npm/_npx/3c2a9ea6c4b6e0a2/node_modules/markdownlint-cli2/markdownlint-cli2-bin.mjs tests/Hexalith.EventStore.Sharding.Tests/README.md
git diff --check
```

The recorded expiries identify these completed runs, not reusable future deadlines. Every later live run requires its own future expiry and binding. Logs and bindings are retained under ignored `artifacts/sharding/`, with console logs named `provider-review-*` and the independent check in `provider-review-independent-audit.json`.

The main fault run is `sharding-c4b58c17428649b18c308f1597d19f52`. It observed all eight armed fault boundaries across two provider recovery and both-host replacement cycles, with 58 submitted mutations reconciled against actual host journals and the provider COMMIT ledger. The bound host DLL SHA-256 is `125acfe9b1d2720709e23098b36ac99daee1b77145c3c1ce8aa8571ac9c09bee`; the test DLL SHA-256 is `e2dac036838a01b1bbb218caa43ee7d535071036c8130b73910a80800d3de7b6`; its binding SHA-256 is `8c56e6a6d25fe4d6a2936c5ec0eed2041c8a79fb14b02cf35ed6bbb0660d9395`. Actual versions: SDK 10.0.403, .NET 10.0.14, daprd 1.18.7, PostgreSQL 18.4, placement/scheduler 1.18.1 and Docker 29.8.2. These observed versions are not a ratified production profile.

All 12 live environments have verified cleanup receipts and an independent exact-ID/label/PID/socket/filesystem audit: 26 containers, 44 host/sidecar processes, 1,491 transient commands, 12 named provider volumes and 12 private runtime directories are absent. Proxy listeners/sessions are stopped and drained. All source and host/test output hashes match their pre-execution manifests. Approved predecessor/successor/handoff bytes, dependency wrapper and original sprint row equal the original baseline. No production source changes were authored; production source and protected hashes also match the pre-live snapshot after the external merge.

Matrix audit: journal failures are covered by eight `AttemptJournalTests` theory cases and persisted-state controls; proxy cleanup by three `FixtureProxyCleanupTests` cases; conditional binding by two `BindingTests` cases and live bindings; fault-record inspection/SDK timing by the passing provider fault case plus `FaultExperimentTests`; expiry semantics by four actor-boundary cases and both passing explicit lifecycle cases. Every covering case ran. This completes only the bounded development correction. The original Story 4.6 remains awaiting operator evidence, with the one-key path and production qualification gaps owned by the existing follow-up records.

### Final verification after review patches — 2026-10-31

Both surviving review groups were patched with three test-file edits. Journal cases now pass a complete supported registration/stage/COMMIT control before requiring the incomplete actual observations to fail specifically on missing execution stages. Both live main cases deserialize the real retained binding and compare the serialized authorization with the selected-mode authority. The verification reviewer independently rechecked those assertions and confirmed its gap resolved, with no direct defect. No finding is deferred.

The rebuilt Debug lane passed with zero warnings/errors. The implementation agent ran only its affected AttemptJournalTests (13/13 passed); the parent then reran the full 97 deterministic selections, all 10 live cases, both explicit expiry cases and all 34 global-position cases. Final result: 143 distinct selected cases passed, zero failed/skipped/not-run. Repeated cases are not counted as extra distinct coverage. README lint and whitespace checks passed again.

Build/deterministic/global commands are the same as recorded above; reviewed logs use `provider-review-rework-build.log`, `provider-review-deterministic-reviewed.log`, `provider-review-live-reviewed.log`, `provider-review-proxy-expiry-reviewed.log`, `provider-review-expiry-reviewed.log` and `provider-review-global-tests-reviewed.log`. The final live commands used these exact bindings:

```bash
EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T23:10:25Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderCommitAmbiguityTests' -class '*ProviderRecoveryTests' -class '*SyntheticShardingTests' -class 'Hexalith.EventStore.Sharding.Tests.LifecycleTests'
EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T22:54:43Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderProxyExpiryTests' -explicit only
EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T22:54:57Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ExpiryLifecycleTests' -explicit only
```

The final main fault run is `sharding-177689c5faaf4340bc7574557d6c0a29`, with all eight fault boundaries and both complete recovery/replacement cycles passing. Its binding SHA-256 is `770ed1e9dcea7ffd68ec166fcdec775127b15c239f165b338ccf5df8d9e7b4b7`; host DLL SHA-256 `ae241ad60bf77b6ea4f82acc4ae522b6eda13026867c644f87297f7d2460f079`; test DLL SHA-256 `02aa1cd8109e7151ecaed3ea855a660747577e0b0aa8acdc6f916953c94b8ebc`. All 65 source files and complete output manifests match each of the 12 final pre-execution bindings. Earlier run identities above remain historical results checked before the review edits and are not substituted for final evidence.

The final independent audit covers all 24 initial/final environments: 52 containers, 88 host/sidecar processes, 3,000 transient commands, 24 named provider volumes and 24 private runtime directories are absent. All proxy listeners/sessions are stopped and drained. The final 12 environments account for 26 containers, 44 host/sidecar processes and 1,509 commands. Results are retained in ignored `artifacts/sharding/provider-review-independent-audit-reviewed.json`. Production/protected hashes match the pre-live snapshot, and approved subject/sprint bytes still match the original baseline. The correction is done; original Story 4.6 remains awaiting operator evidence. No production qualification or CI success is claimed.

The source snapshot and earlier correction draft were committed externally during this run as recorded above. This workflow's subsequent test assertions and final records remain uncommitted. Automatic staging/commit is intentionally omitted to honor the approved frozen constraint; pushes, merges and releases remain on hold. No user work or unrelated concurrent commit was reverted.
