---
title: 'Story 4.6: Provider Commit Ambiguity and Interrupted Admission'
type: 'feature'
created: '2026-10-31'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
parent_story_key: '4-6-global-position-sharding-spec-renegotiation'
baseline_commit: 'ad383a96394307154b0026643245fdbb9a24dd4e'
completion_scope: 'isolated-development-lane-only'
production_evidence_qualified: false
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-4-6-isolated-postgresql-recovery.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The previous synthetic recovery lane does not fault the provider/Dapr commit acknowledgement boundary, interrupt admission there, or verify repeated recovery cycles and actual reservation executions.

**Approach:** Extend only the existing two-host test lane with an observable PostgreSQL protocol fault boundary, execution-attempt instrumentation and repeated same-storage recovery/replacement scenarios. Resolve uncertainty through complete durable inspection before distinct subsequent allocations.

## Boundaries & Constraints

**Always:** Preserve the recorded owner approval and downstream authorization for candidate `411005b4ccb2352249c83330c54967c6a22beba1`, blob `160331d25451928ff3c3dea2300b65cab4f97c3b`, normative digest `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`. The current request authorizes implementation, independent review and bounded isolated local execution without another general checkpoint. Preserve dirty baseline changes, comparing preimages before editing. Bind manifests, actual configuration/environment, exclusive canonical UTC expiry, storage ownership and teardown before execution. Keep one owned database/volume and two independent hosts/sidecars. Use Debug and existing dependencies. Record actual attempts, commands, versions, counts, complete durable outcomes and limitations; remove and independently verify all owned resources and scratch.

**Never:** Change production allocator, public contracts, dependencies, approved records, completed specs or original story/sprint status. Stage, commit or push. Retry an ambiguous reservation. Infer unchanged state from a transport exception, manufacture admission readiness, reset generation or reconstruct authority from maxima. Claim production capacity, backup/restore, physical fencing, HA or section 7 qualification.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Before commit | Actual provider transaction blocked before forwarding COMMIT | Observable boundary; interruption rolls back complete write; previous record or absent admission only if instrumentation proves no commit/retry | Caller remains uncertain; inspect durable outcome |
| Lost commit acknowledgement | PostgreSQL COMMIT completion withheld before Dapr receives it | Direct durable read proves complete committed admission/reservation while response is withheld; drop connection and inspect again | No replay; unused ranges permitted |
| Interrupted admission | Fresh exact pair at both boundaries | Absent authority remains unable to reserve; committed complete authority retains exact generation and zero ceiling | Inspect before first distinct allocation; conflicting generation rejected |
| Repeated cycles | At least two cycles, acknowledged and unused ranges, provider restart then both pairs replaced | Same storage identity; every complete state survives; new allocations exceed every acknowledged/durable ceiling | All actual sends, actor entries/save attempts and proxy commits counted; detect duplicates |
| Isolation and Int64 | Shared tenant/domain, delimiter/case/Unicode variants, near-max strings | Independent exact records and decimal Int64 strings survive every cycle | Overflow rejects before mutation |
| Lifecycle | Completion, startup failure, expiry | Proxy connections/tasks/listener drained along with existing owned resources | Absence verification and cleanup failures remain visible |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Sharding.Tests/ShardingFixture.cs` and `.ProviderRecovery.cs`: reuse binding, source/output manifests, owned volume, lifecycle lock, commands, durable reads, restart and teardown. Optional proxy mode must leave existing direct-provider regressions available. Record new configuration before creation.
- New `PostgresFaultProxy.cs` and supporting single-type files: dependency-free loopback TCP proxy, PostgreSQL framed messages, frontend COMMIT and backend CommandComplete COMMIT gates. Target the exact state key using a test-only PostgreSQL AFTER INSERT OR UPDATE trigger emitting a unique NOTICE; a per-fault nontransactional sequence counts executions including rolled-back writes and later retries. AFTER avoids double counting an upsert's INSERT and UPDATE paths. Only a connection with the matching provider notice may satisfy a fault. Keep trigger instrumentation until the request drains and assert sequence/proxy counts outside expected exceptions. Fail if the selected runtime never exposes a marker. Do not log credentials or raw startup/authentication frames. Gate completion is explicit; no timed crash inference. Count all observed commit executions, including unexpected retries.
- `tests/Hexalith.EventStore.Sharding.TestHost/ShardRequest.cs`, `ShardActor.cs` and new attempt-journal helper: optional test operation identifier with durable per-host actor-entry/save-start/save-complete observations. Preserve constructor compatibility and arithmetic/state semantics; no deduplication that would hide duplicate execution. Retain journals across replacements. Fixture prevents reusing mutating operation IDs and asserts actual journal counts separately from expected exceptions.
- Optional proxy-mode transaction witness: the observed daprd 1.18.7 component skips explicit transactions for a one-operation save. Stage a second test-only witness key containing the operation ID and exact next state alongside the unchanged reservation record, solely in the pre-bound instrumentation mode. Inspect both records for previous/absent versus complete committed outcomes. This forces and qualifies only the synthetic multi-operation provider transaction path; direct one-key/autocommit fault qualification remains a separate gap.
- New fixture partial and `ProviderCommitAmbiguityTests.cs`: arm one fault with no competing mutations, observe boundary, inspect state, explicitly break connection, drain outcome, restart same provider and replace both host/sidecars. Compare all exact records before/after every recovery. New focused proxy tests prove framing, gating, cancellation and observation counters without Docker.

## Tasks & Acceptance

- [x] Implement and test deterministic provider/Dapr fault proxy and owned teardown.
- [x] Bind optional proxy configuration and operation-attempt evidence; retain actual retries and duplicate execution detection.
- [x] Cover both commit boundaries for reservations and admissions through at least two full recovery/replacement cycles, uniqueness, ceilings, isolation and exact Int64 persistence.
- [x] Extend lane README, complete independent review and triage, run focused faults and existing lane regressions, independently audit absence and protected bytes.
- [x] Record results here and remaining evidence gaps in a new follow-up spec, preserving prior records.

**Acceptance Criteria:**

- Given an armed provider fault, when the request fails, then an observed PostgreSQL boundary and all actual attempts establish its classification and complete durable outcome.
- Given each recovery and replacement, when allocation resumes, then acknowledged identities stay unique and every new range exceeds the inspected durable and acknowledged ceiling in its exact generation.
- Given missing or uncertain admission, when inspected, then only a complete matching durable record enables reservations; no fallback creates authority.
- Given disposal or expiry, when work drains, then every owned resource including the proxy listener and sessions is absent and independently checked.

### Review Findings

Code review 2026-10-31 (`bmad-code-review 4.6`, full mode, four layers: blind, edge-case, verification-gap, acceptance). Reviewed the combined uncommitted lane diff against HEAD `61e6fa853` (40 files, both this phase and the recovery phase it builds on). 49 raw findings: 1 decision-needed, 10 patch entries (8 owned by this phase, 2 by `spec-4-6-isolated-postgresql-recovery.md`), 0 defer, 31 rejected.

- [x] [Review][Decision] Resolved 2026-10-31 by the owner: accept the disclosed two-key scope; the finding is rejected and the one-key gap remains owned by `spec-4-6-provider-ambiguity-qualification-follow-up.md` (no `deferred-work.md` entry). One-key implicit-transaction save path still has no COMMIT-boundary fault — Intent/Problem: "does not fault the provider/Dapr commit acknowledgement boundary". Proxy mode stages the test-only `commit-witness` key, so every faulted save is the two-key explicit-transaction path (`ShardActor.cs:139-143`); the ordinary one-key authority write (daprd 1.18.7 `Multi` shortcut) is never faulted. The change came through the Spec Change Log/Code Map, not a renegotiation of the frozen intent, and is disclosed in `spec-4-6-provider-ambiguity-qualification-follow-up.md`. Options: accept the disclosed two-key scope with the one-key gap owned by the follow-up spec, or extend this phase with a one-key wire fault (e.g. hold `Sync` before execution / hold `ReadyForQuery` after the implicit commit).
- [ ] [Review][Patch] README focused deterministic command omits four new test classes; `-class '*PostgresFaultProxyTests' -class '*AttemptJournalTests'` selects neither `PostgresFaultProxyFailureTests`/`PostgresFaultProxyIsolationTests` nor `ShardAttemptValidatorTests`/`FaultExperimentTests`, and the lane has no CI, so a documented rerun skips the tests behind the README's isolation/drain/validator/evidence claims [tests/Hexalith.EventStore.Sharding.Tests/README.md:261]
- [ ] [Review][Patch] Journal I/O sits inside the save `try`: a `Record("save-complete")` failure after a committed save clears cache, journals `save-failed` and throws (caller sees uncertain failure for a durable success); a `save-start` failure is journaled as a failed save that never started; a throwing `save-failed`/`actor-exit` record masks the original exception [tests/Hexalith.EventStore.Sharding.TestHost/ShardActor.cs:164]
- [ ] [Review][Patch] Proxy-mode run binding records the wrong authorizing specs — `configuration["authorization"]` names only the recovery and isolated-lane specs even when `providerCommitProxy` is true; this spec is never bound [tests/Hexalith.EventStore.Sharding.Tests/ShardingFixture.cs:164]
- [ ] [Review][Patch] README says SDK sends are asserted "before trigger removal and provider restart", but `FaultAsync` runs `sends.ShouldBe(1)` in the action after `CompleteCommitFaultAsync`, which drops the trigger/function/sequence; reorder the final evidence actions (or correct the sentence) [tests/Hexalith.EventStore.Sharding.Tests/ProviderCommitAmbiguityTests.cs:158]
- [ ] [Review][Patch] A proxy `DisposeAsync` that reports retained protocol/accept failures throws before `VerifyProxyAbsentAsync` runs, so listener absence is never probed and the receipt records `proxyListenerAndSessionsAbsent=false` for a stopped listener (AC4 "independently checked" on the failure path); split the dispose and probe `try` blocks [tests/Hexalith.EventStore.Sharding.Tests/ShardingFixture.cs:433]
- [ ] [Review][Patch] A failed proxy absence probe (`VerifyProxyAbsentAsync` returns false: proxy not stopped, or a foreign listener on the port) adds no entry to `failures`, so teardown throws "Owned-resource teardown failed: " with an empty cause [tests/Hexalith.EventStore.Sharding.Tests/ShardingFixture.cs:433]
- [ ] [Review][Patch] README says both durable keys are inspected before and after every fault, but `FaultAsync` reads only the witness before arming; admission faults never inspect the fresh pair's reservation key beforehand [tests/Hexalith.EventStore.Sharding.Tests/ProviderCommitAmbiguityTests.cs:133]
- [ ] [Review][Patch] README "Provider COMMIT and interrupted admission" section links no follow-up spec, unlike the recovery section; add `spec-4-6-provider-ambiguity-qualification-follow-up.md` [tests/Hexalith.EventStore.Sharding.Tests/README.md:284]

Rejected:

- VG/blind/edge (3 reports) COMMIT completion FIFO assumes `CommandComplete("COMMIT")` — `low`: a COMMIT answered by `ROLLBACK` tag or `ErrorResponse` leaves a stale queued ID, but the pinned `Multi` flow rolls back failed operations instead of committing and the misattribution fails closed in ledger reconciliation; the fix adds settlement branches.
- Acceptance: proxy sessions not independently checked (counters only) — `low`: session tasks are awaited and sockets disposed in each session's `finally`; an OS socket-table probe adds new instrumentation.
- Acceptance: proxy torn down before draining in-flight work — `false`: `DisposeAsync` cancels `_work` before cleanup, the host fence cancels the same save at expiry, and breaking a held gate first is what unblocks a gated actor turn; fixture `docker exec psql` reads bypass the proxy.
- Acceptance: Inspect executions excluded from attempt accounting — `low`: Inspect never saves (a stray save would break the save-start/ledger equality); registering read-only IDs changes `BindOperation` reuse semantics.
- Acceptance: missing `<param>` XML docs on new internal members — `low`: matches existing lane members (`InitializeAsync` `readExpiry`/`beforePreflight`/`resolveDockerEndpoint` were already undocumented), CS1591 not enforced here; a doc sweep is beyond a direct correction.
- Blind/edge (2 reports) `BeginProviderRestart`/`EndProviderRestart` race `AcceptAsync` — `low`: a session accepted inside a few-instruction window can miss or keep the expected-disconnect mark; hiding a failure additionally needs a later truncated frame on that exact session; the fix adds a lock.
- Blind: pinned daprd/components-contrib version recorded but not enforced — `false`: boundaries are observed, not inferred; a runtime that skips explicit COMMIT times out in `AwaitCommitFaultAsync` and breaks ledger/save-start reconciliation.
- Blind: zero-retry resiliency load not verified — `low`: retries would surface as extra SDK sends, actor entries, sequence executions or ledger entries, all asserted; a load check needs log-level/metadata plumbing.
- Blind: commit-ambiguity cycles never SIGKILL the provider — `low`: the frozen matrix requires provider restart, and SIGKILL recovery is covered by the recovery lane; a crash cycle is new scope.
- Blind: `extended`/`cancel` InlineData rows coupled — `false`: framing only affects `IsCommit` and break/cancel only affect `GateAsync` exit; no code path depends on the combination.
- Blind: two-key witness failure paths untested — `low`: witness staging/save failures share the single bare `catch`/save `catch` already exercised; adding Nth-call failure injection is beyond a direct correction.
- Blind: validator lacks full duplicate-saved, split-PID, `Sends != 1` cases — `low`: duplicate stages already fail the `SequenceEqual` exercised by `illegal-save-failed`; the remaining clauses need new switch arms and only matter on deliberate deletion.
- Blind: live proxy expiry runs no traffic — `low`: held-gate cancellation is covered at socket level; an in-flight live expiry scenario is new scope.
- Blind: interrupted admission never re-admitted after proven absence — `low`: beyond the frozen matrix row; absence and conflicting-generation rejection are asserted.
- Blind: faults always enter via node 0 without placement assertion — `low`: coverage expansion; journal PIDs are retained for diagnosis.
- Blind: two-segment frame writes without `NoDelay` (Nagle/delayed ACK) — `maybe-false`, low if true: latency-only on loopback, no correctness effect; would be settled by timing a gated vs direct run.
- Blind: journal host identity is PID-only — `low`: PID reuse inside one bounded run is implausible and would also fail the replacement-PID disjointness assertion loudly; adding start time changes the record shape.
- Edge: unnamed statement/portal kept after simple `Query`; failed Parse/Bind keeps COMMIT flag — `low`: a conforming client (pgx) never executes a destroyed or failed portal, and a phantom COMMIT fails ledger reconciliation; the error-correlation half needs new tracking.
- Edge: gate reached after the 12 s wait is never broken — `low`: the case has already failed, waits are bounded (15 s exit, 25 s settle), direct `psql` reads do not block on row locks; a second break guard is extra branching.
- Edge: sidecar kill during `RestartAsync` resets a proxied connection — `low`: a reset would fail loudly, and every live replacement in both cycles drained cleanly; a replacement allowance adds lifecycle branches.
- Edge: validator canonical-route check rejects routing-mismatch rejections — `false`: no proxy-mode caller passes an `actorId` override; only `SyntheticShardingTests` does, without proxy mode, so no ID is journaled.
- Edge: "connection-attributed COMMIT ledger" claim unchecked — `false`: each entry carries `ConnectionId`, markers come only from the connection receiving the exact NOTICE, and the validator matches the ledger to save starts, successes and faults as the README states.

## Implementation Notes

No intent gaps. Only owned disposable data is mutated. Changes remain unstaged. Baseline source copies, protected hashes and index identity are in ignored `artifacts/sharding/commit-ambiguity-implementation/`. Existing dirty recovery changes are the authorized input; use additive files and guarded localized edits, switching to isolation if overlapping external changes appear. Implementation agent may build and run deterministic tests; parent performs live runs after inspecting the binding. No unrelated Aspire topology is started for this explicitly isolated lane.

The initial source/output snapshot included prior uncommitted recovery work. This phase changes only the additional 22 files recorded in `phase-files.json`, including two new specs. Concurrent production work advanced HEAD to `5e3c2f0e60b114cdaab52594ccbb254829d40e0f`; it is external to this phase and preserved. The initial Server.Tests build intersected that work and failed with eight missing route-catalog symbol errors. The same Debug command passed after that commit settled; all 34 freshly built global-position cases passed.

Pre-review verification: clean Debug lane build; 56 deterministic cases passed; two new live cases passed (eight actual provider faults plus injected proxy-startup cleanup); seven existing provider/synthetic/startup cases passed. A broad expiry suffix filter exposed a new-class naming collision: the proxy expiry case passed, then the original case correctly rejected the already-expired shared deadline. Rename only the new class to `ProviderProxyExpiryTests`, preserving the original documented filter. Rebuild passed and 12 focused deterministic cases passed. Each explicit expiry case is subsequently run alone with its own bound future deadline. Failed diagnostic attempts and complete cleanup remain retained rather than replaced by success claims.

## Spec Change Log

- Initial live diagnosis: the first explicit COMMIT gate correctly timed out with zero observed COMMIT messages after successful ordinary saves. The pinned provider optimizes one-operation saves without an explicit transaction. Add a bound optional second witness key to reach its actual multi-operation transaction boundary; preserve ordinary mode, exact reservation authority, per-attempt evidence and all before/after durable assertions. The initial failed run remains diagnostic and unqualified; final evidence must be rebound after rebuilding.

## Review Triage Log

Three independent context-free reviewers examined the phase diff against its captured dirty baseline: `/tmp/story46-commit-ambiguity-review-0zqanv2l.patch` (103,404 bytes). All three returned before triage. The blind review's finding floor was ten. Each finding is retained separately below, including duplicate reports. The full review input is retained in ignored artifacts; corrections remain within the authorized lane.

| Finding | Verdict / route | Verified evidence and required correction |
|---|---|---|
| Blind 1: failed boundary/assertion skips final evidence | medium / patch | `FaultAsync` only breaks the gate in `finally`; later counter/durable reads are skipped on failure. Capture settled request, all attempts and both durable records before teardown even when the experiment fails. |
| Blind 2: SDK task is not actor termination | medium / patch | No actor-exit or save-failed observation precedes trigger removal. Observe server termination before final accounting; a timeout must retain uncertainty and diagnostics. |
| Blind 3: unreconciled global provider ledger | medium / patch | Final global COMMIT counts are merely recorded. A provider replay outside an armed fault can write the same value again without changing actor-stage counts or durable state. Reconcile total transactions against saves and observed fault completions. |
| Blind 4: missing or illegal save stages accepted | medium / patch | The validator only checks save counts are at most one. Record actual submitted outcomes and require each complete permitted stage sequence. |
| Blind 5: journal attribution is unchecked | medium / patch | Grouping by operation ID alone does not match the full request, actor route or owned host incarnation, and ignores unknown mutations. Reconcile every mutating entry with its submission and host. |
| Blind 6: truncated frame swallowed | medium / patch | `EndOfStreamException` inherits `IOException` and is accepted as ordinary disconnect. Separate explicit fault/teardown disconnects from unexpected framing failure and retain the latter. |
| Blind 7: prepared portal lifecycle is stale | low / patch | Close and idle transaction completion do not invalidate maps; an invalid reused COMMIT portal can be classified as a gate. Directly correct the supported protocol map lifecycle and add a negative probe; this is not future-runtime qualification. |
| Blind 8: accept failure skips session await | medium / patch | Awaiting a faulted accept task exits disposal before collecting all session tasks. Drain every accepted task before reporting aggregated accept/protocol failures. |
| Blind 9: connection/reset negative probes absent | medium / patch | Existing exact-marker probes use one connection and never test reset at idle ReadyForQuery. Add observable negative cases for both boundaries. |
| Blind 10: proxy startup failure has no sessions | medium / patch | Failure immediately after listener creation precedes hosts. Add proxy-enabled failure at the existing first-node-ready boundary and assert actual sessions accepted/drained. |
| Edge 1: failed experiment loses final outcomes | medium / patch | Independently confirms Blind 1 at `FaultAsync`'s exceptional path. Preserve its report and apply the same final evidence correction. |
| Verification 1: duplicate rejection validator untested | medium / patch | Existing tests retain duplicate entries but do not submit them to the actual validator. A weakened actor-entry check would miss duplicated rejected operations. Feed duplicate rejected executions through that consumer and assert rejection; a single no-save rejection must pass. |

All twelve findings were corrected. The independent verification reviewer rechecked the implementation, confirmed eleven rows, then identified that the connection/reset negative probes still covered only BeforeCommit. Both probes were parameterized for acknowledgement loss, including backend COMMIT responses; eight focused isolation cases passed. The same reviewer then confirmed all twelve rows addressed. No accepted defect was deferred. Review commands were read-only and created no resources or scratch.

The parent also corrected two implementation details before final evidence: failed-save cache eviction must precede journal I/O, and clean EOF must not turn the proxy's own peer cancellation into a false protocol failure. Focused real-socket and actor tests passed. The final implementation records full submitted requests/outcomes, validates journal route/PID/stage attribution, observes actor exit, reconciles the complete COMMIT ledger, preserves failed-experiment evidence and drains sessions even after accept failure. This strengthens only the bounded synthetic evidence; it is not a general exactly-once or physical-fencing guarantee.

## Verification

Build the lane Debug project with `-p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal`. Run the built executable with focused proxy/attempt/fault classes and all deterministic lane classes. Bind an explicit UTC expiry before running the new live class, then existing provider/synthetic/lifecycle and explicit expiry regressions. Run Server.Tests `-class '*GlobalPosition*'` if its build is available. Capture exact commands/results and manifests. Generate review diffs without staging. Verify index and protected preimages and independent resource absence before completion.

### Final verified development phase

Only this isolated development phase is done. Final scope is 33 files: 31 lane files and two new specs, superseding the pre-review 22-file count above. The captured dirty recovery baseline is preserved, including 28 untouched lane files. Production allocator, public contracts, dependencies, approved records, completed specs and original Story 4.6/sprint status were not changed by this phase. The original story remains awaiting operator evidence. No staging, commit, push, deployment or CI run was performed.

Concurrent work advanced HEAD first to `5e3c2f0e60b114cdaab52594ccbb254829d40e0f` (routing), then to `61e6fa853d81cf822d67d6abfd6a130bdbb14ce2` (CI and packaging tests). Those changes were preserved. All external production source differences equal current HEAD, and the index equals current HEAD. The final bound lane source/output manifests still match actual files after those unrelated commits. No overlapping lane edits required a worktree.

The approved subject and owner/downstream authorization remain unchanged. This run binding is local execution evidence under that authorization, not a new authenticated signature or production authority. No production section 7 evidence row is qualified.

### Exact final commands and counts

From the repository root; command output is retained under ignored `artifacts/sharding/commit-ambiguity-implementation/`.

```bash
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal

dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*BindingTests' -class '*FixturePreflightTests' -class '*TransportLifecycleTests' -class '*ShardActorExpiryTests' -class '*ShardActorRecoveryTests' -class '*PostgresFaultProxy*' -class '*AttemptJournalTests' -class '*ShardAttemptValidatorTests' -class '*FaultExperimentTests'

EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T16:10:00Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderCommitAmbiguityTests' -class '*ProviderRecoveryTests' -class '*SyntheticShardingTests' -class 'Hexalith.EventStore.Sharding.Tests.LifecycleTests'

EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T15:40:25Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ExpiryLifecycleTests' -explicit only

EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T15:42:46Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderProxyExpiryTests' -explicit only

dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPosition*'
```

All final commands exited 0. Both project builds had zero warnings/errors. The lane build includes its test host. The final proxy-expiry wrapper selected the canonical future UTC second before spawning the exact command; its environment/arguments are retained in `verified-proxy-expiry-command.json`.

| Final case group | Passed | Failed | Skipped / not run |
|---|---:|---:|---:|
| Deterministic binding/preflight/transport/actor/protocol/accounting/failure-path | 84 | 0 | 0 |
| New eight-fault live scenario and proxy startup failures at listener/first node | 3 | 0 | 0 |
| Existing provider recovery, synthetic concurrency and five startup failures | 7 | 0 | 0 |
| Direct and proxy explicit expiry | 2 | 0 | 0 |
| Freshly built production-v1/local global-position regression | 34 | 0 | 0 |
| Distinct final cases | 130 | 0 | 0 |

Final logs: `verified-build.log`, `verified-deterministic.log`, `verified-live.log`, `verified-expiry-direct.log`, `verified-expiry-proxy-current.log`, `global-build-retry.log`, `global-tests-fresh.log`. The combined live run took 204.946 seconds; this is test execution time, not a capacity measurement. Additional focused and earlier passing reruns are retained without counting them as distinct coverage.

Preserved failed attempts: the initial single-key live boundary case timed out because the real provider emitted no explicit COMMIT (1 passed/1 failed); its failed path did not retain the complete final fault ledger/durable inspection, so it supplies no boundary proof. Review corrected that evidence-loss path. A suffix collision selected both explicit expiry classes against one deadline (proxy passed; direct rejected expired input), then the new class was renamed. One invalid internal-wildcard CLI filter exited 3 before test discovery and was corrected. A later operator deadline was already past when invoked and was rejected before resources; the final proxy run used a newly bound future second. The initial Server.Tests build intersected concurrent source edits and failed with eight missing-symbol errors; the same fresh build later passed. These failures, actual outputs and cleanup receipts remain retained and are not represented as successful qualification.

### Final artifact, configuration and environment binding

Main run: `sharding-93d5709f0ed64861a91509248ab41535`, created `2026-10-31T15:35:41Z`, exclusive expiry `2026-10-31T16:10:00Z`. Its `binding.json` predates owned storage/container/proxy creation and binds the approved candidate, blob and normative digest, environment, exact implementation/dependency outputs, 64 source files, configuration, private topology, storage ownership, instrumentation and teardown.

| Identity | SHA-256 |
|---|---|
| Binding | `e8a8a9680117adedd30e7191f8dab128d45dddee7f5e02f89ea55890eaabb2eb` |
| Host DLL | `a20edad6ca0a22dfe856c1e1e3fa39e0096bf388acac7325ec37b34dbd5ebed9` |
| Test DLL | `bae1e86d4a5c14aae6cf9c3328fc88e825e6b3628152b69a934a2489c3b122ef` |
| Sources JSON file | `1a0fe6a04004000be589b31415393eb1ccad776c84ae039083a822808cafaed2` |
| Bound compact source manifest | `d4dcce7e81d5e8ba12974c4f987fba41b7ee8513595130c9ddc1ad5d73fd48cb` |
| Complete configuration fingerprint | `92d9c6893ee96ee532ffb2ab3c5a761aaa1a3f1f478318c585743c4ca838643e` |
| PostgreSQL component | `e142e4badec0280ea51d97daf20e6172619ab6466d89a2048fd479e4fbb4387f` |
| Discovery | `3059f9d9fa5f548f538ebf2668ce544a2a5349c196d4708481446dc4d09ee1fb` |
| Resiliency | `cc7e6de88bb38e88e53239b16f1ad9a66e61ae5ac2e3fa81e91b8ab45fe17b8a` |

The independent summary verified all 102 host-output and 195 test-output files plus all 64 sources against the pre-execution binding. The configuration fingerprint is SHA-256 of compact UTF-8 JSON with sorted configuration keys. Full source/output maps, exact commands, redacted configuration, per-host flushed journals and observations are retained with the run; derived checks are in `final-run-summary.json` and `final-independent-summary.log`.

Observed environment: Ubuntu 26.04 LTS X64; .NET SDK 10.0.403 and runtime 10.0.14; xUnit runner 4.0.2; Dapr .NET packages 1.19.0; daprd 1.18.7 (SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`); Docker 29.8.2; PostgreSQL 18.4 (`18.4-1.pgdg13+1`); placement/scheduler each 1.18.1 from actual startup logs. Immutable image identities are unchanged from the lane README and present in each binding. Version-tag source inspection is retained in `provider-source-identity.json`; it explains the observed one-operation transaction shortcut without changing installed components.

Configuration: local `unix:///var/run/docker.sock`; loopback-only private PostgreSQL, placement, scheduler and two independent host/sidecar pairs; SQLite discovery; `state.postgresql` v1 with `actorStateStore=true`, database `sharding`, `public.state` JSONB, random per-run credential, `sslmode=disable`, `connect_timeout=5`; scoped actor/component `maxRetries: 0`. Proxy mode adds the bound exact-key AFTER trigger/sequence and the second `commit-witness` key. Both keys are saved through the real multi-operation transaction path. This instrumented two-key profile is not a ratified production profile, and qualifies no fault boundary on the ordinary one-key implicit-transaction path.

### Durable outcomes, attempts and matrix audit

The main case has 77 actor-call outcomes: 61 acknowledged successes (including inspections), eight known rejections and eight uncertain fault outcomes. It registers 58 mutating submissions, retains 254 journal entries, and reconciles exactly 50 COMMIT attempts: 42 ordinary successful transactions and eight instrumented faults. There are 46 backend COMMIT completions: all 42 ordinary saves plus four withheld acknowledgements. Each fault has exactly one SDK send, actor entry/save-start/save-failed/actor-exit sequence, provider sequence execution, matching NOTICE, COMMIT attempt and reached gate. No retry or duplicate execution was observed. Negative tests verify the actual validator rejects duplicates, unexplained execution, wrong attribution, illegal stages and extra provider commits.

| Cycle / fault | Final complete authority | Forwarded / completed COMMIT |
|---|---|---|
| 0 reservation before commit | Main ceiling remains `1` | 0 / 0 |
| 0 admission before commit | Both authority/witness absent | 0 / 0 |
| 0 reservation lost acknowledgement | Main ceiling `8` | 1 / 1, acknowledgement withheld |
| 0 admission lost acknowledgement | Generation `9223372036854775707`, ceiling `0`, matching witness | 1 / 1, acknowledgement withheld |
| 1 reservation before commit | Main ceiling remains `10` | 0 / 0 |
| 1 admission before commit | Both authority/witness absent | 0 / 0 |
| 1 reservation lost acknowledgement | Main ceiling `17` | 1 / 1, acknowledgement withheld |
| 1 admission lost acknowledgement | Generation `9223372036854775706`, ceiling `0`, matching witness | 1 / 1, acknowledgement withheld |

All failed caller outcomes remain uncertain. Before-commit unchanged state is established by actual target-provider notice, withheld COMMIT, explicit disconnect, terminated actor and single-attempt evidence. After-commit outcomes are inspected while PostgreSQL's acknowledgement is withheld and again after request/actor termination. Both complete records are checked; reservation gaps are permitted and retained. No uncertain reservation is replayed.

The test acknowledges 63 unique complete positions in 32 ranges across 11 exact pair/generation partitions. Two uncertain committed reservations leave ten additional unused counters as gaps. Distinct resumed allocations begin above every inspected ceiling; the main pair finishes at ceiling `19` in generation `9223372036854775807`. Synthetic Int64 seeding is not counted as generated history. The bounds pair reaches `long.MaxValue`, then overflow rejects without mutation. Exact decimal strings, delimiter/case/Unicode pair isolation, missing admission rejection and generation conflicts survive both cycles.

Both graceful provider recoveries retain the same container/mount/named volume and system identifier `7702856094312161323`; stopped status is `false|0|false`. Each is followed by replacement of both host/sidecar pairs, with four new distinct PIDs, then inspection and allocation above durable authority. All witnessed zero-ceiling admissions survive replacement before their first reservation. The existing regression also passes its provider SIGKILL/blocked-save/caller-response-loss cases and 64 concurrent batches totaling 316 counters.

Every matrix row ran and passed: the eight-fault case covers both provider boundaries, interrupted admission, repeated recovery/replacement, isolation and Int64; deterministic socket/actor/accounting tests cover malformed frames, connection/reset selection, failed evidence capture and duplicate detection; listener/first-node startup failures and both explicit expiry tests cover owned teardown. The post-teardown ledger equals the validated pre-teardown ledger, with all 24 main-run sessions drained and no active session or listener.

### Teardown, preservation and remaining limits

All 36 resource-bearing environments created by this phase, including diagnostic and failed attempts, have verified cleanup receipts. Independent exact-ID/label/PID/socket/filesystem checks found all 76 containers, 130 host/sidecar processes, 4,438 transient commands, 36 named provider volumes and 36 private runtime scratch directories absent. Owned unit-test scratch is absent. Reviewers created no resources or scratch; parent-owned temporary review files are removed after retaining their artifact copies. Ignored evidence artifacts are intentionally retained.

An initially broad unit-scratch scan found `/tmp/sharding-cleanup-unit-0a58d21f84204e8baa74615a3aa40892`; its timestamps and historical receipt establish `2026-10-31T09:00:15Z`, before this phase. It is unowned pre-existing work and was preserved. The final ownership-scoped audit does not silently remove or claim it as this phase's cleanup. Scope and absence results are retained in `scope-audit.json`, `cleanup-audit.json` and `cleanup-totals.json`.

This phase supplies no production capacity, backup/restore, physical-fencing, split-brain, HA, real event/retry-result, public wire/cursor or production lifecycle qualification. Only the explicit synthetic two-key transaction path was faulted. The in-process watchdog still cannot guarantee teardown after external test-process or machine termination. The new [qualification follow-up](spec-4-6-provider-ambiguity-qualification-follow-up.md) records one-key fault coverage, real allocator/event identity, production lifecycle/inventory, backup/restore, physical failover, public consumers/cursors, capacity inputs, operational bounds, CI and external-watchdog gaps. No gap is waived or recast as another missing general authorization.
