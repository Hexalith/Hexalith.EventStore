---
title: 'Story 4.6 Follow-up: Isolated Dapr PostgreSQL Sharding Lane'
type: 'feature'
created: '2026-10-31'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
parent_story_key: '4-6-global-position-sharding-spec-renegotiation'
baseline_commit: '77e949a9d54444c43dcef2da17ea65f8c3674385'
completion_scope: 'isolated-development-lane-only'
production_evidence_qualified: false
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-4-6-local-position-mechanics.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation-2.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The local sharding candidate proves serial arithmetic with an in-memory state manager, but has no independent-host Dapr/PostgreSQL lane.

**Approach:** Add a test-only actor host and focused executable test project. Drive two independent host/sidecar pairs sharing one disposable PostgreSQL instance, inspect durable state, and prove bounded synthetic reservation semantics across restarts.

## Boundaries & Constraints

**Always:** Preserve the owner approval and downstream authorization in the handoff. This user request authorizes implementation and isolated local execution with artifact/environment/expiry/lifecycle binding before resource creation; do not request the same general authorization again. Record actual versions, configuration, artifact hashes, commands, counts, cleanup and limits. Retain run output in ignored test artifacts. Fence, drain and tear down owned resources at completion or exclusive UTC expiry, including partial initialization failure. Preserve concurrent edits. Use Debug and existing central package pins. Independent review and focused tests are required.

**Never:** Change production allocator, public contracts, approved specifications, handoff, original story/sprint state, append/provider-write seams, dependencies, or other users' infrastructure. Do not stage, commit or push. Synthetic results do not qualify any production section 7 gate or production capacity. Do not invent a production trace/profile, authenticated signature, or authority beyond this local lane.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Concurrent ranges | Two hosts, same admitted exact pair/generation | Every counter unique, consecutive disjoint ranges in allocation order; correct identity; durable final ceiling equals total reserved | Fail on any duplicate, loss or mismatch |
| Pair isolation | Shared tenant or domain, delimiters, case and Unicode variants | Independent counter/generation and injective physical actor identity | No normalization or reset |
| Restart and gaps | Persist unused reservation, restart both hosts and sidecars | New processes; same generation; next counter above persisted ceiling | No reconstruction from events |
| Admission | Missing, invalid or conflicting generation | Reject before allocation/state change; same-generation readmission preserves ceiling | Inspect durable state after rejection and restart |
| Bounds | Nonpositive count, final Int64 counter, overflowing batch | Last valid counter succeeds, overflow rejects with unchanged complete durable state | No staged mutation or reset |
| Lifecycle | Missing/invalid/expired binding, completion, partial startup failure | No execution without binding; owned processes/containers/scratch removed, teardown failures surface | No silent cleanup success |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.Tests/GlobalPositions/` — reference arithmetic and exact UTF-8 identity. Internal DTOs cannot be blindly reused with JSON serialization.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` — process/sidecar readiness, PostgreSQL digest and JSONB state-read pattern; avoid its production host/Sample/Redis coupling.
- `Oq8OwnedContainerLaunch`, `DockerPublishedPortResolver`, `Oq8BoundedLog`, `Oq8DiscoveryConfiguration` in that Fixtures folder — reusable small helpers or patterns. Private SQLite resolver avoids shared mDNS; NAMESPACE alone is insufficient.
- `tests/Directory.Build.props`, root build/package props, `.gitattributes`, `.editorconfig` — test imports, central Dapr 1.19.0 and xUnit 4.0.2 pins; C# one type per file, tracked LF.

## Tasks & Acceptance

**Execution:**

- [x] `tests/Hexalith.EventStore.Sharding.TestHost/` — minimal nonpackable test host (override inherited IsTestProject), test-only actor interface/request/state/result types with JSON-safe members. One actor per injectively encoded exact pair. Atomic persisted admission/ceiling, checked arithmetic before staging, explicit generation matching, internal test seed for near-overflow checks. No production references or DI registration.
- [x] `tests/Hexalith.EventStore.Sharding.Tests/` — focused xUnit project, disposable fixture, lifecycle/binding checks, live synthetic scenarios. Reuse allowed tiny helpers by compile links where sensible. Fresh unique app ID shared by both sidecars; private components, discovery, placement, scheduler and Postgres; loopback endpoints, random passwords and ports. Do not use shared control-plane infrastructure.
- [x] `tests/Hexalith.EventStore.Sharding.Tests/` — before execution write a run binding with approved subject, exact host/test/dependency/runtime identities, local environment, configuration/topology and explicit exclusive canonical UTC expiry supplied by operator environment (maximum one hour). Reject absent/invalid/expired input. Enforce expiry in host writes and fixture watchdog; cancel/drain work and stop all owned resources on expiry. Preserve redacted evidence and cleanup receipt outside deleted scratch.
- [x] `tests/Hexalith.EventStore.Sharding.Tests/README.md` — exact focused build/run instructions, binding variables, boundaries, limits, teardown and retained test artifacts. Keep configuration in the lane; no global solution or production edits required.
- [x] This spec — retain actual verification, independent review triage, exact artifact identity and remaining evidence gaps.

**Acceptance Criteria:**

- Given two hosts and sidecars, when both submit overlapping variable batches, then full returned identities and every reserved counter are unique per exact pair/generation and match PostgreSQL ceilings.
- Given completed or rejected operations, when durable state is read and both host/sidecar pairs are replaced, then admission, generation, unused ceilings, bounds and denial results remain correct.
- Given expiry or startup failure, when disposal runs, then only owned resources are removed and absence is verified; the binding predates workload execution.
- Given final scope review, when compared with the baseline, then production and approved artifacts remain byte-identical and local evidence is explicitly unqualified for production.

## Implementation Notes

The original Story 4.6 remains awaiting-operator; this follow-up uses parent_story_key without synchronizing its sprint row. The initial tree was clean; no worktree is necessary unless concurrent edits overlap. Repository baseline Aspire start/describe/stop completed before implementation; resource readiness was mixed, so it supplies lifecycle baseline only, not lane evidence. User authorization takes precedence over repeating the build workflow's general approval checkpoint. The implementation agent may build and inspect, but must leave live resource execution to the parent after the final binding is reviewable.

## Spec Change Log

## Review Triage Log

Three independent context-free reviewers read `/tmp/story46-isolated-review.patch` (89,790 bytes). All results were collected before triage. The accepted findings were fixed and verified. The follow-up reviewer confirmed the six original edge findings fixed, then confirmed the response-body correction and Int64 serialization/assertions with no remaining findings in those edits. No findings were deferred.

| Finding | Verdict and disposition | Evidence / action |
|---|---|---|
| Blind 1: expiry after preflight | medium, patch | Real preflight commands precede cancellation; move deadline enforcement before commands and cover elapsed setup. |
| Blind 2: remote Docker context | high, patch | Inherited CLI context can target external infrastructure; require and record a resolved local socket endpoint. |
| Blind 3: arbitrary nonempty binding dictionaries | false | The live fixture constructs these maps from observed tool/image/configuration values, writes them in private scratch, and hashes exact host/test files. Placeholder maps exist only in deterministic unit fixtures. This local record explicitly supplies no signature or production authorization verification; there is no live path accepting those placeholders from an external binding author. Host artifact and subject checks remain enforced. |
| Blind 4: missing CID ownership | medium, patch | Engine creation can precede CID-file persistence; reconcile the unique owner label with exact planned names and verify absence. |
| Blind 5: missing crash recovery IDs | medium, patch | README manual recovery promise requires ownership IDs before final cleanup; journal planned/started resources immediately. |
| Blind 6: log persistence aborts cleanup | medium, patch | Unguarded diagnostic writes can bypass scratch removal; aggregate write errors and continue teardown. |
| Blind 7: disposed before successful teardown | medium, patch | A failed first disposal can prevent a second attempt; preserve failure evidence and retry unverified teardown. |
| Blind 8: caller cancellation without drain | medium, patch | WaitAsync does not cancel its underlying proxy task; track and drain actor/command work before resource removal. |
| Blind 9: no actor write in expiry scenario | medium, patch | Add actual actor-boundary expiry checks and label explicit live expiry as lifecycle-only; do not claim physical provider-write fencing from timer/teardown evidence. |
| Blind 10: reply-derived batch expectations | medium, patch | Correlate each submitted count with its reply and assert the independently calculated total and unused ceiling. |
| Blind 11: zero-ceiling admission restart | medium, patch | Persist and inspect a newly admitted pair without reserving, then restart and assert its generation and first reservation. |
| Blind 12: unbounded diagnostic capture | low, patch | Complete Docker logs and ReadToEnd bypass the declared bound; bound capture while reading and request bounded log tails. |
| Edge 1: incompatible actor serializer | medium, patch | Independent serializer/proxy probe produced InvalidDataContractException before HTTP. Enable JSON on both host runtime and proxy; pinned 1.19.0 API inspection confirms both properties. |
| Edge 2: inherited Dapr endpoint | high, patch | Endpoint variables can take precedence over ports and target another sidecar. Pin both endpoints and app ID; remove inherited channel tokens. |
| Edge 3: elapsed preflight deadline | medium, patch | Same verified deadline defect as Blind 1; retain this finding separately and enforce deadline before preflight. |
| Edge 4: log-write cleanup failure | medium, patch | Same verified teardown defect as Blind 6/7; retain this finding separately and continue cleanup across evidence failures. |
| Edge 5: missing CID after engine creation | medium, patch | Same verified ownership gap as Blind 4; retain this finding separately and exercise the missing-CID window. |
| Edge 6: no actual work drain | medium, patch | Same verified lifetime gap as Blind 8; retain this finding separately and drain tracked operations and transient processes. |
| Verification 1: actor expiry not exercised | medium, patch | Removing actor guards/tokens would leave timer-only tests green. Add direct actor calls after expiry and across a gated state read, then inspect committed and pending state. |
| Verification 2: returned counts supply expectations | medium, patch | Consistent under-allocation could pass current checks. Independently assert requested sizes, total 316 and the exact 13-counter gap. |
| Runtime 1: PostgreSQL temporary server | medium, patch | Initial live run had 1 passed/3 failed. Socket pg_isready accepted initialization/shutdown state; use final TCP/database readiness. |
| Runtime 2: unsupported control-plane version flag | medium, patch | Actual placement binary rejects --version. Record observed startup-log version and immutable image identity instead. |
| Follow-up edge 1: response body cancellation | medium, patch | All six original edge findings were confirmed fixed. The linked token ended at headers while HttpClient buffered afterward; retain cancellation through body buffering and test a body that stalls after headers. |
| Runtime 3: Int64 JSON-number precision | medium, patch | Second live attempt persisted near-max ceiling as 9223372036854776000. Encode test-only Int64 identity fields as JSON strings and assert exact provider state immediately after seeding, including max generation. |

## Verification

Build the two lane projects in Debug with `-p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal`, then invoke the built xUnit assembly directly. Run existing Server.Tests `-class '*GlobalPosition*'` as the production-v1/local-mechanics regression. Record full commands, counts, source/binary hashes and cleanup outcomes after execution. Run `git diff --check` and verify protected bytes against HEAD. No commit or CI run is requested.

### Baseline and initial verification

- Baseline HEAD: `77e949a9d54444c43dcef2da17ea65f8c3674385`. Initial working tree clean; all additions are confined to two new test directories and this follow-up record.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal` — exit 0; 0 warnings, 0 errors.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPosition*'` — exit 0; 34 passed, 0 failed/skipped.
- Both lane Debug project builds with the same build switches succeeded with 0 warnings/errors. `dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*BindingTests'` — exit 0; 22 passed, 0 failed/skipped before review fixes.
- `EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T08:00:00Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*SyntheticShardingTests' -class 'Hexalith.EventStore.Sharding.Tests.LifecycleTests'` — exit 1; 4 total, 1 passed, 3 failed, 0 skipped. This failed pre-qualification attempt exposed the two runtime findings above; it reached no two-host actor workload. Raw output: ignored `artifacts/sharding/implementation/live-tests-initial.log`.
- All four initial run cleanup receipts reported verified absence, independently confirmed using owned labels, process IDs and `/tmp/eventstore-<run-id>` absence. Failed runs were `sharding-2af5abfcbb024ee29c3fb5fa63330c87`, `sharding-453fb4a9e7254328a0182f1d31ac15d7`, `sharding-47db8f003ffe4f2f9202edfa6b3198b3`, and `sharding-a04add88a0b14b0fbabf75a7175e8ef3`.
- Pre-fix binary hashes were host `ab70bda4c07b6c3346c6ca446ba819339ca69e99755c28d29ec6816b3863ba58` and tests `6cdbfbb7e25506a5d1afdc3faf78f51ba2750887ee0dc2570a9c4d9e5657167f`; these do not identify the final tested artifact.

Concurrent work was preserved: while this task ran, another session advanced HEAD to `da1435b91681482371d65b7aa030c162509a8d62` and changed tracked `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj`. It is outside this task's owned diff and was not reverted. The allocator/contracts and approved artifacts remain protected; only the two new test directories and this record belong to this implementation. Final artifact hashes bind the actual tested lane independently of that unrelated history movement.

### Corrected candidate, second runtime attempt

- `dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*BindingTests' -class '*FixturePreflightTests' -class '*TransportLifecycleTests' -class '*ShardActorExpiryTests'` — exit 0; 37 passed, 0 failed/skipped.
- `DAPR_HTTP_ENDPOINT=http://127.0.0.1:1 DAPR_GRPC_ENDPOINT=http://127.0.0.1:1 DAPR_API_TOKEN=synthetic-parent-token APP_API_TOKEN=synthetic-parent-token EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T08:30:00Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*SyntheticShardingTests' -class 'Hexalith.EventStore.Sharding.Tests.LifecycleTests'` — exit 1; 5 total, 4 passed, 1 failed, 0 skipped. All four controlled startup failures passed, including engine-created/missing-CID recovery. The synthetic workload passed concurrency and first restart checks, then failed exact near-Int64 provider readback. The complete JSONB ceiling had been rounded from seeded `9223372036854775806` to `9223372036854776000`; this is an observed local pipeline failure, not a qualified provider guarantee.
- All five environments reported verified cleanup and no owned containers remained. The main failed run was `sharding-ff064c2af1174dc08e018fd44e0359c7`; its observations preserve the rounding and actual version outputs. Raw logs: ignored `artifacts/sharding/implementation/final-deterministic-tests.log` and `final-live-tests.log`. These names describe the attempted final checks and do not assert success.
- Tested pre-string-encoding DLLs: host `8014434657d9d3e5f0616c7d7aa9f6d3f9330fa53d9bb03663b221857f999089`, tests `6618c23a875427d1c3203a56d940b21588230cf28f18ac73c892e23f441366b3`. Final artifact identity follows after the corrective run.
- The independently checked correction snapshot was `/tmp/story46-final-review.patch` (138,460 bytes). The follow-up confirmed all six original edge findings fixed and raised the one body-cancellation finding recorded above.
- The external concurrent commit also changed `package-lock.json`, `tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationHost.cs` and added `ProviderVerificationControllerActivator.cs`; all were preserved outside this task's scope.

### Final verified implementation

The isolated development phase is complete. Production allocator/contracts, approved successor/predecessor/wrapper, original handoff, and original Story 4.6/sprint state are unchanged by this task. The original story remains `awaiting-operator`. Nothing was staged, committed, pushed, published or deployed. There is no CI run link for this local execution.

The new host registers only the synthetic actor. Each exact pair has an injective Base64URL UTF-8 actor ID and one atomic admission/ceiling record. Int64 identity fields in this candidate use exact decimal JSON strings after the observed numeric-rounding failure; this is not an implementation of the approved public metadata envelope. The overflow seed is an explicit test seam, not naturally generated production history.

The fixture now pins a local Docker endpoint and per-node Dapr endpoints; uses private discovery/control-plane/provider resources; journals ownership before final cleanup; reconciles missing CID files by exact planned name plus owner label; bounds output; fences and drains actual requests/commands; and continues teardown across diagnostic failures with retryable failure evidence. Four injected startup paths include a missing-CID engine object. Deterministic actor tests inspect both pending and committed state at entry/read/stage/save expiry boundaries. The real expiry run proves lifecycle teardown, independently of physical provider-write fencing.

Final source execution HEAD: `da1435b91681482371d65b7aa030c162509a8d62`, plus the uncommitted lane files. Baseline remains the original full revision in frontmatter. Concurrent committed changes are preserved. The actual tested artifacts are:

| Artifact / binding | Identity |
|---|---|
| Host DLL SHA-256 | `e7cb1ee949aa055d50341cfe1bc917ccfccb89d8a4b646e5713c6288695c1fdc` |
| Test DLL SHA-256 | `53b02aab6d7ac547bb89c6f187f544aa99096ba98c97b241dc87b50446f69792` |
| Source manifest hash | `d9ecd77a1a0d2a01272c7971cea50db4c7fa54242e871fe4741242a82074bf96` |
| Main run | `sharding-34a1bd059b2e46d6b3fc517a2c5eecd2` |
| Binding created / exclusive expiry | `2026-10-31T08:05:34Z` / `2026-10-31T08:30:00Z` |
| PostgreSQL component SHA-256 | `e4de8482211cac5f954bf93a77bc76df71c316a4c5f7fc786fc84d4d675216b0` |
| Private discovery SHA-256 | `0f9e9cac8258afee445bdd864ee2dd3d6d2429e02743b38bf43a4e83ce1c46ad` |
| Topology fingerprint SHA-256 | `427d7e239142d85eb67549493cfb6086201d3a789c1959d4d0177f9008fa748f` |

The topology fingerprint hashes compact key-sorted UTF-8 JSON of the main binding's `app-id`, `namespace`, `topology`, `ports`, and `docker-endpoint` configuration fields. All host/test output files were rehashed after execution and still match their pre-execution manifests. The source manifest contains 33 lane/supporting files. Full manifests, command output, configuration, ownership and cleanup records remain in ignored `artifacts/sharding/`; none is added as a committed evidence packet.

Actual environment: Ubuntu 26.04 LTS x64; .NET SDK `10.0.403`, runtime `.NET 10.0.14`; Dapr .NET packages `1.19.0`; xUnit runner `4.0.2`; daprd `1.18.7` with SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`; Docker server `29.8.2`; PostgreSQL `18.4` (`18.4-1.pgdg13+1`); placement and scheduler both `1.18.1`, observed from their startup logs. The different sidecar/control-plane versions are recorded, not presented as a qualified production combination. All three exact image digests are in the lane README and run binding.

Provider profile: isolated synthetic `state.postgresql` version `v1`, `actorStateStore=true`, database `sharding`, table `public.state` with JSONB values, per-run random PostgreSQL credential, `sslmode=disable`, `connect_timeout=5`, local socket Docker endpoint `unix:///var/run/docker.sock`, loopback host port bindings. The run uses two independent host/sidecar pairs, one private PostgreSQL, one private placement and one private scheduler container, and SQLite discovery. It does not copy production resiliency or claim a production-equivalent security/resource profile. Raw credentials remain confined to deleted private scratch; retained configuration is redacted and its actual bytes are hash-bound.

### Exact final commands and counts

```bash
dotnet build tests/Hexalith.EventStore.Sharding.TestHost/Hexalith.EventStore.Sharding.TestHost.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal

dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*BindingTests' -class '*FixturePreflightTests' -class '*TransportLifecycleTests' -class '*ShardActorExpiryTests'

DAPR_HTTP_ENDPOINT=http://127.0.0.1:1 DAPR_GRPC_ENDPOINT=http://127.0.0.1:1 DAPR_API_TOKEN=synthetic-parent-token APP_API_TOKEN=synthetic-parent-token EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T08:30:00Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*SyntheticShardingTests' -class 'Hexalith.EventStore.Sharding.Tests.LifecycleTests'

EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T08:06:00Z dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ExpiryLifecycleTests' -explicit only

dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPosition*'
```

All builds above exited 0 with zero warnings/errors. The final lane build includes the host project; the explicit host-only build was also verified during implementation. Final test invocations exited 0:

| Lane | Passed | Failed | Skipped / not run |
|---|---:|---:|---:|
| Deterministic binding/preflight/transport/actor expiry | 40 | 0 | 0 |
| Live synthetic + four injected startup failures | 5 | 0 | 0 |
| Explicit real expiry | 1 | 0 | 0 |
| Existing v1 + local global-position regression | 34 | 0 | 0 |
| Total distinct final cases | 80 | 0 | 0 |

Final console logs are `artifacts/sharding/implementation/verified-deterministic-tests.log`, `verified-live-tests.log`, and `verified-expiry-test.log`; the earlier global-position build/test output is also retained at `artifacts/sharding/implementation/global-regression-build.log` and `global-regression-tests.log`. Redirecting these command outputs did not change their arguments. Earlier failed attempts and their identities remain recorded above instead of being replaced by the final success.

The live main run proves 64 overlapping variable-sized reservations through both ingress sidecars, exactly 316 counters in that concurrent workload, and 15 exact tenant/domain pairs overall. Across all successful reservations it grants 423 counters in 92 ranges. It checks two complete replacement cycles of both independent host/sidecar pairs, persisted zero-ceiling admission, unused reservation ceilings, exact max-generation/Int64 ceilings, and complete JSONB equality for 14 denial operations: three generation conflicts, three invalid counts, two invalid generations, two missing admissions, three overflows, and one routing mismatch. Every unused range remains a permitted gap; response arrival order is not used as allocation order.

The explicit expiry environment was created at `2026-10-31T08:05:34Z`, reached both actor runtimes ready, fenced at its exclusive `2026-10-31T08:06:00Z` deadline, and completed verified teardown at `2026-10-31T08:06:02Z`. Across failed and successful live attempts, all 15 environments have verified cleanup: 28 containers removed with anonymous volumes, 28 host/sidecar processes absent, 550 transient command processes absent, private scratch removed, and no container with the unique sharding ownership label remains. Independent post-run checks verified actual container/PID/scratch absence. Retained artifacts are intentional test evidence, not running resources.

Formatting checks covered all 29 owned new files, including untracked content, for LF/no-BOM/trailing whitespace; `git diff --check` passed. The index is empty. At the first final scope check, protected source drift was the concurrent committed Gateway project change. A later concurrent commit also changed route-catalog builder/filter sources; all drift equals current HEAD and none belongs to this task. A concurrent Platform gitlink change is preserved unstaged. This task has no local production edits. Approved specification and handoff hashes remain unchanged.

### Remaining evidence gaps

No production section 7 row is qualified. Representative production trace, ratified provider/topology/resource/security profile, comparative tenant/domain/composite capacity and production peak evidence remain unavailable. This synthetic lane supplies no production capacity figure.

Still required separately: provider restart/failover and backup/restore; physical fencing/split-brain and old-writer controls; distributed lifecycle inventory/admission/retirement and generation lineage; real aggregate/event/CloudEvent identity and retry-result fidelity; strict public metadata/opaque wire round trips and mixed-version consumer behavior; committed-source cursor/checkpoint/rebuild/migration proof; provider key/value/page/generation operational bounds; hot-shard/lifecycle cost; rollout/rollback and content-bound Story 4.5 applicability/approval. The local Int64 seed does not measure production exhaustion behavior. The expiry tests do not prove that cancellation can undo an already accepted provider write.

The watchdog runs in the test process. External termination of that process or the machine can prevent automatic teardown; immediate retained ownership journals support exact manual recovery. No such interruption occurred here. CI integration and production qualification remain future work; the explicit local commands above are the verified lane.

Final workspace history moved again after test execution to `ed719ed52e571d2a82274f12aaf2d38f2c75ae80`, adding unrelated route-catalog source/tests. The parent worktree also has the external unstaged `references/Hexalith.Platform` pointer change from `f2b327edd5cf5d9895d8cce1424f6b6b0c04734f` to `32c2f26fec75b01f72eb8bb314e7c84a96903175`. Both are preserved. A deliberately strict final baseline check first reported `Protected file changed src/Hexalith.EventStore.Client/Routing/VersionedRouteCatalogBuilder.cs`; investigation established that this file and `GeneratedRouteCatalogFilter.cs` belong to the concurrent committed change and match current HEAD. The revised scope check verifies protected approved artifacts exactly, confirms each external source drift equals current HEAD, and checks all owned source hashes against the executed run. It does not reset or absorb concurrent work.
