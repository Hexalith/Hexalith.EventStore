---
title: 'Story 4.6: Isolated PostgreSQL Restart and Failure Recovery'
type: 'feature'
created: '2026-10-31'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
parent_story_key: '4-6-global-position-sharding-spec-renegotiation'
baseline_commit: 'cf34253ad846f6fd0131ed8c6bfcd8cb6d1ac0bb'
completion_scope: 'isolated-development-lane-only'
production_evidence_qualified: false
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-4-6-isolated-dapr-postgresql-sharding.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The existing synthetic two-host lane replaces hosts and sidecars but has no provider restart or interrupted-save recovery proof.

**Approach:** Extend only this test lane with graceful PostgreSQL stop/start, abrupt termination while a save is observably blocked, and a deliberately lost successful response. Preserve the same database storage and inspect exact durable authority before resuming with distinct reservations.

## Boundaries & Constraints

**Always:** Preserve the owner approval and downstream authorization recorded in the unchanged handoff. The current user authorizes implementation, independent review and isolated local runs; no repeated general approval checkpoint is needed. Preserve staged additions and concurrent edits; write unstaged changes only. Bind artifact manifests, environment, exclusive canonical UTC expiry (maximum one hour), topology, provider storage ownership and teardown before resource creation. Fence/drain at completion/expiry, remove all owned processes, containers, volumes and private scratch, verify absence, retain redacted ignored artifacts. Use central package pins and Debug. Record actual commands, versions, configuration, counts and limitations.

**Never:** Change production allocator, public contracts, production DI, dependencies, approved predecessor/successor/handoff, prior completed specs, original story or sprint state. Do not stage, commit, push or use shared infrastructure. Do not claim production capacity, section 7 qualification, backup/restore, HA, physical fencing or split-brain proof. Never blindly retry an ambiguous reservation or equate a transport failure with unchanged durable state.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Graceful restart | Admission, ranges, unused ceiling, max Int64 digits | Same container/storage, admission/generation/ceiling survive; live hosts resume above all ceilings | Bound and observe actual stop/start |
| Provider unavailable | Warm admitted pair, unadmitted pair, attempted admission | No acknowledged positions or fallback; recovery inspects full records | Exceptions are uncertain, explicit denials known |
| Interrupted save | Exclusive SQL table lock; provider backend observably waits for relation lock | Abrupt kill, drain request, same storage starts; inspect complete absent/previous/next record as appropriate | Allow gaps; never assert no mutation from exception |
| Lost response | Consume successful HTTP result then throw before caller receives it | Uncertain caller outcome; exact durable reservation preserved; next distinct call exceeds it | Exactly one SDK HTTP send; no test retry |
| Later replacement | Recovered provider then both hosts/sidecars replaced | New PIDs; same full authority; next counters above durable and acknowledged ceilings | No authority reconstruction |
| Isolation/bounds | Shared tenant/domain, delimiter/case/Unicode variants and near-max strings | Exact independent pair records and decimal Int64 strings survive both provider recovery and host replacement | Missing admission remains rejected |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Sharding.Tests/ShardingFixture.cs`: reuse binding, bounded commands, lifecycle lock, activity drain, provider queries and exact ownership journal. Existing PostgreSQL image declares `/var/lib/postgresql` as its volume.
- New `ShardingFixture.ProviderRecovery.cs`: partial fixture for lifecycle operations, SQL lock observation and owned named-volume inspection/teardown. Keep long SQL lock holder tracked and bounded; no detached tasks.
- `RunCancellationHandler.cs`: wrap optional transport for deterministic discarded success; observe send count and prevent SDK retries without changing ordinary calls.
- `tests/Hexalith.EventStore.Sharding.TestHost/ShardActor.cs`: evict staged/cache state after every failed save, propagate uncertainty; pre-save rejection and save-started cancellation have different semantics.
- `SyntheticShardingTests.cs`, `LifecycleTests.cs`, `ShardActorExpiryTests.cs`: retain existing coverage, adjust only save-started cancellation expectations. New focused recovery and failure-state tests.

## Tasks & Acceptance

- [x] Fixture: bind a uniquely named, labeled provider volume and recovery configuration before creation; journal ownership and actual mount; compare same mount/container after recovery; remove and verify owned volumes even on partial startup failure.
- [x] Fixture/transport: record acknowledged-success, known-rejection and uncertain outcomes; discarded successful response; explicit single HTTP attempt. Configure scoped actor/component zero-retry resiliency using documented Dapr policies, hash/retain configuration; record limitations of built-in rerouting retries.
- [x] Host: clear staged/cache state on save failure and propagate uncertain outcome, including cancellation once saving begins. Deterministic failure tests prove a later runtime save cannot flush a failed pending reservation.
- [x] Recovery tests: graceful outage requests with original hosts alive; exact durable recovery; interrupted save using granted table lock plus observed blocked PostgreSQL transaction before SIGKILL; discarded response; no ambiguous retry; isolation, exact Int64 strings, uniqueness and later replacement. Assert accepted range sizes/identities independently.
- [x] README/spec: exact focused commands and limitations; final artifact hashes, versions, counts, cleanup, review triage; new follow-up spec for remaining evidence gaps.

**Acceptance Criteria:**

- Given each injected boundary, when the provider recovers, then complete durable records determine conservative next allocation and all acknowledged positions remain unique per exact pair/generation.
- Given recovered authority, when both independent host/sidecar pairs are subsequently replaced, then admission, exact generation and unused ceilings survive and allocations continue strictly above prior acknowledged and durable ceilings.
- Given run completion, startup failure or expiry, when disposal drains work, then exact owned resources including provider storage are absent and retained receipts verify it.

### Review Findings

Code review 2026-10-31 (`bmad-code-review 4.6`, full mode, four layers). The combined uncommitted lane diff covering this phase and `spec-4-6-provider-commit-ambiguity.md` was reviewed together; that spec holds the full triage (49 findings: 1 decision, 10 patches, 31 rejected). Entries owned by this phase:

- [ ] [Review][Patch] `ShardActorExpiryTests` name and summary still say requests "deny" at every boundary, but the `save` case now expects propagated cancellation (uncertain), not a denial; rename/re-document the case [tests/Hexalith.EventStore.Sharding.Tests/ShardActorExpiryTests.cs:19]
- [ ] [Review][Patch] README adds two consecutive blank lines before "## Same-storage provider recovery" (markdownlint MD012 under the repo's `default: true` config) [tests/Hexalith.EventStore.Sharding.Tests/README.md:139]

Rejected (this phase's code):

- Acceptance: blocked-save observation not tied to the interrupted request — `low`: the test runs one writer at a time and fixture queries take only `AccessShareLock`, so the `RowExclusiveLock` waiter is that save; key attribution is not visible in the parameterized upsert text and would need new instrumentation.
- Acceptance: after graceful restart only the main, zero-admission and bounds pairs allocate on live hosts — `low`: every pair's complete durable record is asserted after recovery and all pairs allocate after replacement; extra allocation steps expand the case.
- Acceptance: recorded `postgres-stop-signal`/`postgres-image-volumes` never asserted — `low`: the image is digest-pinned and `ProviderIdentityAsync` asserts the actual named-volume mount; adding guards is beyond a direct correction.
- Blind: throwing `ProcessIds` getter inside `Record(...)` drops `provider-stopped`/`provider-recovered` diagnostics — `low`: only on the already-failing path where an owned process exited; a guarded capture adds branching.
- Edge: `pg_sleep(25)` may expire before SIGKILL after two 12 s waits — `low`: a lapse fails loudly (interrupted request and lock holder would not error); a longer sleep collides with the 30 s command bound.
- Edge: no-mutation assertions on uncertain missing-admission reservations — `false`: a reservation has no code path that creates a record for an unadmitted pair, so absence tests the no-manufactured-authority invariant; the outage `Admit`, which could write, is handled as either outcome.

## Implementation Notes

No intent gaps or irreversible external effects: only owned disposable data is mutated/deleted. Existing staged files are the authorized baseline; no worktree is needed unless overlapping external edits appear. Snapshot and index evidence are retained under ignored `artifacts/sharding/recovery-implementation/`. Parent executes live runs after implementation and binding inspection. Implementation agent may build and run deterministic tests; do not run live resources. Keep every source file focused on one type.

## Spec Change Log

## Review Triage Log

Three context-free reviewers examined `/tmp/story46-recovery-review-2kzml74a.patch` (59,727 bytes). All results were collected before triage. The verification-gap reviewer reported no gaps. Each independent finding is retained below, including overlapping reports.

| Finding | Verdict / route | Evidence and correction |
|---|---|---|
| Blind 1: culture-dependent unit binding | medium / patch | Thai locale reproduced two failures; use invariant canonical timestamps and rerun under that locale. |
| Blind 2: expected exception could hide blocked retry | medium / patch | Generic exception assertion could swallow the retry guard. Expose per-call transport counts and discarded-success flag; assert them outside the expected exception. |
| Blind 3: PID equality without liveness | medium / patch | Retained Process.Id remains readable after exit. ProcessIds now rejects exited current processes at every recorded provider boundary. |
| Blind 4: unpropagated observation timeout | medium / patch | SQL command could outlast its local deadline. Link the observer token to actual command cancellation/drain and check it after each query. |
| Blind 5: volume-only startup stage untested | medium / patch | Add failure immediately after named-volume creation, before any container, and verify teardown. |
| Blind 6: non-cancellation staging failure untested | medium / patch | Add staged-then-thrown IOException case and prove later runtime save cannot publish the pending range. |
| Blind 7: unidentified surviving volume diagnostic | medium / patch | Label-query survivors could yield an empty failure message. Include their names in the cleanup exception and receipt. |
| Blind 8: final evidence not yet populated | false | The reviewed snapshot was an intermediate in-review record, not a completion claim. The existing recording task is fulfilled by actual final results below before status becomes done. |
| Edge 1: SQL query outlives local deadline | medium / patch | Same root cause as Blind 4; retain independently and apply the linked command/query token correction. |
| Edge 2: original hosts may have exited | medium / patch | Same root cause as Blind 3; retain independently and require current process liveness, not just matching IDs. |

The original blind reviewer independently rechecked all seven corrections, the live collection and sidecar identity check: no remaining issues. It also ran 20 focused deterministic cases under Thai culture, all passed, without changing files or starting live resources. No accepted defect is deferred. A pre-review deterministic failure exposed cleanup attempting Docker without a bound endpoint; that direct correction skips volume queries before resource-capable preflight. A concurrent live run exposed temporary port collisions between fixture classes; serialize the live classes and verify bound sidecar app identity during readiness. These runtime corrections are included in final regression coverage.

## Verification

Build each lane project with `dotnet build <project> --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal`. Invoke built test assembly directly with `-class '*ProviderRecoveryTests'`, deterministic classes, existing synthetic/lifecycle classes and explicit expiry. Build Server.Tests separately and run `-class '*GlobalPosition*'`. Capture exact expiry, full commands/results, manifests and cleanup outside scratch. No solution-level tests or production changes.


### Final verified development phase

Only this isolated development phase is complete. The production allocator,
public contracts, production source, dependencies, approved artifacts, handoff,
prior completed specs, original Story 4.6 and sprint state are unchanged. Original
Story 4.6 remains awaiting operator evidence. There is no CI run or production
qualification claim. No staging, commit, push, dependency update or deployment
was performed.

At the initial repository inspection the previous lane was staged against
`0aa1b0b6fae8ae9efefa6fe1cdb66d6277f1170f`. A concurrent session recorded it as
`cf34253ad846f6fd0131ed8c6bfcd8cb6d1ac0bb` before this implementation's snapshot.
That full revision is this phase's baseline and final HEAD. The index equals the
captured implementation snapshot; all phase changes remain unstaged/untracked.
No overlapping concurrent source edits required a worktree.

The approved subject remains candidate commit
`411005b4ccb2352249c83330c54967c6a22beba1`, successor blob
`160331d25451928ff3c3dea2300b65cab4f97c3b`, normative SHA-256
`995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`.
The preserved handoff records the owner's approval and downstream authorization;
this phase does not replace them or repeat a general authorization request.

### Exact final commands and results

Run from the repository root. All build commands exited 0 with zero warnings and
zero errors. The lane build also builds its referenced test host in Debug.
The separate Server.Tests build and focused regression passed before the last
fixture-only corrections; those corrections do not affect that project.

```bash
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal

dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*BindingTests' -class '*FixturePreflightTests' -class '*TransportLifecycleTests' -class '*ShardActorExpiryTests' -class '*ShardActorRecoveryTests'

LANG=th_TH.UTF-8 LC_ALL=th_TH.UTF-8 dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ShardActorRecoveryTests'

DAPR_HTTP_ENDPOINT=http://127.0.0.1:1 DAPR_GRPC_ENDPOINT=http://127.0.0.1:1 DAPR_API_TOKEN=${DAPR_API_TOKEN} APP_API_TOKEN=${APP_API_TOKEN} EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T09:20:00Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderRecoveryTests' -class '*SyntheticShardingTests' -class 'Hexalith.EventStore.Sharding.Tests.LifecycleTests'

EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T09:13:15Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ExpiryLifecycleTests' -explicit only

dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPosition*'
```

| Final case group | Passed | Failed | Skipped / not run |
|---|---:|---:|---:|
| Deterministic binding, preflight, transport, actor expiry/recovery | 44 | 0 | 0 |
| Live provider recovery, existing synthetic lane, five partial startup failures | 7 | 0 | 0 |
| Explicit expiry teardown | 1 | 0 | 0 |
| Existing v1/local global-position mechanics | 34 | 0 | 0 |
| Distinct final cases | 86 | 0 | 0 |
| Additional Thai-locale rerun of four already-counted cases | 4 | 0 | 0 |

Ignored console logs are under `artifacts/sharding/recovery-implementation/`:
`build-final-reviewed.log`, `deterministic-reviewed.log`, `thai-reviewed.log`,
`live-reviewed.log`, `expiry-reviewed.log`, `global-build.log`, `global-tests.log`.
The independent follow-up reviewer also passed 20 overlapping deterministic
cases under Thai culture. These repeated cases are not counted as new coverage.

Preserved earlier results: initial build failed with CA1062 and was corrected;
initial deterministic run had 42 passed/1 failed because simulated preflight
cleanup had no bound Docker endpoint; initial focused recovery passed 1/1; the
first combined live run had 5 passed/1 failed at readiness with a metrics response
on a colliding temporary port. After the collection/identity correction the
combined run passed 6/6, followed by final reviewed 7/7 after adding volume-only
startup coverage. The reviewer reproduced two Thai-locale failures before the
invariant timestamp correction. Earlier logs/bindings are retained rather than
being represented as final evidence. Every resource-bearing attempt was cleaned.

### Final artifact and environment binding

| Identity | Actual value |
|---|---|
| Main run | `sharding-9b90d7568bd44d3a86aeca06a57f3712` |
| Created UTC | `2026-10-31T09:10:18Z` |
| Exclusive expiry | `2026-10-31T09:20:00Z` |
| Binding SHA-256 | `45f9fe9d656638ba7c5b58b945dd5fbeb6dc642bd6f90bd86e65f73a09cca3a2` |
| Host DLL SHA-256 | `91bfc9f5e3e40a06bdef211463acf83866d73c9019d5d19b8a462619060fea75` |
| Test DLL SHA-256 | `d65c7ef0352bfb550368f9e1b589b075a172404fe190818033f0bdf162f9779b` |
| Source manifest SHA-256 | `a002bcc0e2fd0d07e15c47dfa574be571356422aa51d904ed59333069ef2b602` |
| Topology fingerprint SHA-256 | `fbe089292ec420a568b9736cbc8f064aae15dca89cfa7778e1b88246740abf08` |
| PostgreSQL component SHA-256 | `f5fb307e81d5807a5919ad88f031f31243c40b287e0b567b91cf21ce69ce02d4` |
| Discovery SHA-256 | `e8c17fdc9ef27ad04795ec7704320b300b521e4b76425b67d9f6f07b3bcfa372` |
| Resiliency SHA-256 | `c85b8b27b83d615b49827859f674227074fac77034b932481e0ad11e98fe95d4` |

The full 39-file source manifest and every host/test output/dependency hash were
rechecked after execution and match the pre-execution binding. The topology
fingerprint hashes compact, key-sorted UTF-8 JSON of binding configuration keys
`app-id`, `namespace`, `topology`, `ports`, `docker-endpoint`, `provider-storage`
and `resiliency-sha256`. Runtime/compiler dependency manifests, redacted configs,
actual commands, ownership, operations and provider reads remain in the run's
ignored artifact directory. `final-run-summary.json` preserves derived values;
`cleanup-audit.json` preserves the independent absence audit.

Actual environment: Ubuntu 26.04 LTS X64, SDK `10.0.403`, runtime `.NET 10.0.14`,
Dapr .NET packages `1.19.0`, xUnit runner `4.0.2`, Docker server `29.8.2`, daprd
`1.18.7` with SHA-256
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.
PostgreSQL is `18.4` (`18.4-1.pgdg13+1`); placement and scheduler each report
`1.18.1` in startup logs. The exact three pinned image digests are in the README
and each binding. The PostgreSQL image reports stop signal `SIGINT` and volume
`/var/lib/postgresql`. This observed version combination is not a ratified
production profile.

Configuration: `state.postgresql` component `v1`, `actorStateStore=true`, database
`sharding`, `public.state` JSONB values, per-run random credential, loopback host
port bindings, local Docker endpoint `unix:///var/run/docker.sock`, `sslmode=disable`,
`connect_timeout=5`. Two independent host/sidecar pairs share that provider and
private placement/scheduler with SQLite discovery. Scoped actor/component
`maxRetries: 0` configuration is hash-bound before launch; one SDK send and actual
successful-response discard are asserted outside expected exceptions. Internal
routing/component behavior is not claimed as a general exactly-once guarantee.

### Actual recovery observations and matrix audit

The final recovery case contains 44 acknowledged successful actor operations,
7 known rejections, and 6 uncertain actor outcomes. It acknowledges 68 distinct
full positions in 23 ranges across ten admitted exact pairs, including eight
pair-isolation variants, zero-ceiling admission and the Int64 bounds pair.
The 13-counter unused acknowledged range remains reserved. The caller-lost
response reserves 17 additional counters durably and leaves that full gap before
the next distinct allocation. The synthetic overflow seed is not generated
history and is not counted as acknowledged allocation.

Both graceful and abrupt recovery retained container, volume/mount and PostgreSQL
system identifier `7702756785381728305`. Graceful stop observed `false|0|false`
(running/exit/OOM); SIGKILL observed `false|137|false`. Before SIGKILL, PostgreSQL
reported an active `INSERT INTO state ... ON CONFLICT ... DO UPDATE` transaction
waiting for a `RowExclusiveLock`, with the exclusive lock holder in `PgSleep`.
All four original host/sidecar processes remained alive. Both replacement pairs
subsequently had new PIDs and continued above inspected durable ceilings.

For the main pair, the interrupted reservation's inspected ceiling remained 16;
the test permits either complete ceiling 16 or 27. The lost response persisted
ceiling 36, then resumed at 37. Its final post-replacement ceiling is 39. Outage
admission was absent on durable inspection; a committed complete zero-ceiling
admission is also a permitted uncertain result. No uncertain write receives an
unchanged-state assertion or an automatic reservation retry. Missing-admission
reservations remain unable to manufacture authority.

Every matrix row is covered by the final passing provider-recovery case; expiry,
preflight, transport and actor-failure edge cases have the additional passing
focused tests. All complete pair records are checked after each provider recovery
and after replacement, including exact decimal JSON-string generations/ceilings
at Int64 maximum and the final overflow denial. The existing synthetic regression
retains its 64 concurrent batches/316 counters, 12 exact pair variants and two
host/sidecar replacement cycles. Deterministic save-failure tests cover both
commit-before-failure and failure-before-commit, plus staged-then-thrown failure;
only fake instrumentation supplies their known durable outcomes.

### Teardown, scope and remaining limits

All 22 resource-bearing environments created by this phase, including earlier
attempts and startup/expiry failures, have verified receipts. Independent final
checks found all 48 containers, 78 host/sidecar processes and 1,734 transient
commands absent; all 22 named provider volumes and 22 private runtime scratch
directories were removed. No container or volume with the sharding owner label
remains. The new volume-only failure path is included. The final explicit expiry
run `sharding-637b387ccd2e4a0a9b6dd6e91afcceff` was bound at
`2026-10-31T09:12:47Z`, fenced at exclusive `2026-10-31T09:13:15Z`, and completed
verified teardown at `2026-10-31T09:13:17Z`. Retained ignored test artifacts are
intentional evidence. Temporary review scratch is removed after review.

Final formatting checks cover tracked and new files for LF/no-BOM/trailing
whitespace, with `git diff --check` passing. Exact protected-file and production
source hashes equal the initial snapshot, and the index is unchanged. The prior
approval/downstream authorization and original story status are preserved.

The new [follow-up spec](spec-4-6-recovery-qualification-follow-up.md) records
remaining commit/ack/admission fault windows, repeated failures, backup/restore,
physical fencing/failover, production profile/trace, comparative capacity, real
event/retry identity, wire/consumer/cursor/migration/lifecycle and Story 4.5 gaps.
None is waived. These results qualify no production-capacity, backup/restore,
HA or production section 7 gate. The watchdog remains in-process; external
termination of the test process or machine can prevent cleanup, and retained
ownership journals support exact manual recovery. No such external interruption
occurred in this phase.
