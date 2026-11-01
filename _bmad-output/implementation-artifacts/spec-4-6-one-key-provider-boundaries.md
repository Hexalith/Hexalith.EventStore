---
title: 'Story 4.6: Ordinary One-Key Provider Boundaries'
type: 'feature'
created: '2026-11-01'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
context: []
parent_story_key: '4-6-global-position-sharding-spec-renegotiation'
production_evidence_qualified: false
completion_scope: 'bounded-isolated-development-lane-only'
baseline_commit: '7ead2f24547c0669b86686340c58c83d883157be'
---

<frozen-after-approval reason="Explicit user authorization to implement, independently review and validate this bounded lane">

## Intent

Qualify two observable failure windows of the existing synthetic actor's ordinary
one-key implicit-transaction save, following the evidence gap in
[the follow-up](spec-4-6-provider-ambiguity-qualification-follow-up.md).
Keep the actual save path and establish outcomes from complete durable state,
all submissions, SDK sends, actor stages and provider executions.

## Boundaries & Constraints

Preserve the reviewed uncommitted corrections as the authorized baseline; no
staging, commits, pushes, merges or releases. Preserve all existing specifications
and original story/sprint state. Change only the isolated test projects, their
README, this new phase record and ignored evidence. No production code, contracts,
dependencies, migrations or production infrastructure. Retain the approved subject
identities already enforced by RunBinding. No production qualification claim.

Bind fresh output/source manifests, versions, local environment, ownership,
exclusive canonical UTC second expiry and fence/drain/teardown before live
execution. The user commissions local execution and delegate-selected expiry
within the existing one-hour limit. Independently verify cleanup afterward.
Keep prior two-key regressions. Never replay ambiguous operations or hide runtime
retries. Missing boundaries and unexplained attempts fail experiments.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Before execution | Exact admission/reservation upsert selected on wire | Hold execution-capable frame; zero sequence executions; prior full state/absence | Break, drain, verify every attempt |
| After implicit commit | Exact upsert success and idle ReadyForQuery | Withhold successful response until idle commit observed; direct read shows complete next state | Break before Dapr acknowledges; retain uncertain outcome |
| Recovery | Settled faults followed by provider restart and host replacement | Exact generations and ceilings survive; distinct allocations exceed all committed ceilings | Missing authority and stale generations deny unchanged |
| Invalid evidence | Wrong key, malformed/unsupported framing, provider error, duplicate execution | No manufactured readiness; complete ledger rejects unexplained observations | Fail with retained diagnostics and cleanup |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Sharding.TestHost/AttemptJournal.cs`: currently couples witness enablement to proxy existence. Separate one-key selection while preserving two-key behavior. `ShardActor.cs` already saves one key when witness disabled; retain that implementation.
- `tests/Hexalith.EventStore.Sharding.Tests/PostgresProxySession.cs`, `PostgresFrame.cs`, `PostgresFaultProxy.cs`: framed plaintext loopback relay; existing exact NOTICE and explicit COMMIT gates must survive. Extend with strict exact-key upsert selection, statement/portal attribution, complete execution ledger and response withholding through successful idle completion. Do not mistake CommandComplete or Sync alone for commit.
- Pinned components-contrib v1.18.6 PostgreSQL v1 `Multi(single operation)` executes directly on the pool. `doSet` uses key=$1, value=$2, isbinary=$3 and optional etag=$4. Observe actual protocol; strict Parse/Bind/Execute support can reject unsupported simple-query writes. Retain prepared statements across idle, clear portals at idle, and handle Close. Withhold CommandComplete while reading through successful ReadyForQuery(I); include ordinary saves in the ledger and require zero explicit COMMITs in one-key mode.
- `ShardingFixture.cs`, `ShardingFixture.CommitAmbiguity.cs`, `ShardingFixture.ProviderRecovery.cs`: reuse binding, journals, owned provider restart, replacement, failure-path diagnostics and verified teardown. New partial fixture file may isolate one-key behavior.
- `RegisteredShardOperation.cs`, `ShardAttemptValidator.cs`: bind full request/route/PID and ordered stage evidence. Add a separate one-key validator if that keeps two-key checks strict.
- `ProviderCommitAmbiguityTests.cs`: reviewed baseline and two-key regression; preserve byte-for-byte. Add a separate ordinary one-key live class and focused protocol/accounting tests. Test helpers may be extended.
- `artifacts/sharding/one-key-phase-20261101/`: initial tracked-file hashes, full HEAD, uncommitted diff and status already captured; retain commands/results/reviews here. This directory is ignored.

## Tasks & Acceptance

**Execution:**

- [x] `tests/Hexalith.EventStore.Sharding.Tests/` and `tests/Hexalith.EventStore.Sharding.TestHost/AttemptJournal.cs`: add bound one-key mode, real wire gates and complete attribution without forcing a second save key.
- [x] New focused protocol/validator tests: cover exact selection, real pre-execution and post-commit gates, response withholding, cancellation, malformed/error evidence and unexpected replays.
- [x] New live test: admission and reservation at both boundaries over two provider recovery/replacement cycles; inspect complete durable records and all application state rows, prove absent witnesses, exact Int64 generation/ceiling strings, pair isolation, denial, abandoned committed ranges and final overflow preservation.
- [x] `tests/Hexalith.EventStore.Sharding.Tests/README.md` and this record: document commands, actual outcomes, failed attempts and remaining gaps; independently review implementation and independently inspect owned cleanup receipts/resources.

**Acceptance Criteria:**

- Given either mutation, when the exact fault is observed, then one SDK send and one ordered actor turn reconcile with all provider attempts; pre-execution has zero forwarded execution and zero trigger calls, post-commit has one execution and full durable next state before acknowledgement.
- Given completed faults, when same-storage PostgreSQL recovery and two-host/sidecar replacement occur, then complete authority and missing admissions remain exact and distinct subsequent reservations preserve generation and ceilings without replay.
- Given ordinary and faulted operations, when final accounting and cleanup run, then no unregistered save, provider-only retry, second key or owned resource is unexplained.

## Implementation Notes

Planning: no unresolved user intent gaps; local synthetic mutations are disposable
and explicitly authorized. Footprint is confined to isolated test code and
evidence. Existing dirty tree is explicitly authorized; no branch change needed.
The user requested implementation through review and validation, authorizing
continuation without a second approval checkpoint. Parent story status stays
untouched. Implementation agent must not run live infrastructure; report build
and focused checks to the coordinator, who will bind and run the live phase.

## Spec Change Log

## Review Triage Log

First independent review: three context-free reviewers read the full 163432-byte
HEAD-to-working-tree diff, including untracked additions. Edge reviewer returned
no findings. Every other finding is recorded individually below; all route to
private test/harness patches under the existing intent. No baseline correction
or approved specification needs changing.

| Finding | Verdict | Evidence and disposition |
|---|---|---|
| Blind 1: arbitrary initialization SQL | medium | `ValidateSql` bypasses validation while initialization is active; unknown application writes would have only hashes. Restrict this allowance to identified pinned schema/metadata/read SQL; retain scoped observations. |
| Blind 2: error before Execute | medium | `_error` can be set by a prior backend error while last status remains idle; Execute checks only status. Reject this path and test that no gate is announced. |
| Blind 3: non-write completion tags | medium | A queued non-write accepts any CommandComplete without validating its tag. Validate supported read completions and reject COMMIT masquerading as a read. |
| Blind 4: missing read completion | medium | ReadyForQuery clears pending non-write entries without checking completion absent an error. Require the supported completion/error sequence. |
| Blind 5: ordinary save with fault metadata | medium | `saved` classification precedes fault-field checks. Require all fault metadata absent for ordinary saves and test each corruption. |
| Blind 6: invalid generation accepted by oracle | medium | Expected admission transition does not independently reject nonpositive generation. Add legality checking and zero/negative generation corruptions. |
| Blind 7: faulted row overwritten before recovery | high | Immediate follow-up reservation replaces the committed-fault row before restart. Defer the next distinct reservation until full recovery/replacement snapshots are checked. |
| Blind 8: stale generation after recovery | medium | Generation denial currently runs before recovery only. Assert unchanged complete rows for stale-generation attempts after provider recovery and host replacement. |
| Blind 9: high ceiling not faulted | medium | Faulted reservation currently has a small ceiling; only the untouched bound pair has a large ceiling. Seed the faulted pair above 2^53 before reservations and preserve its exact digits throughout faults. |
| Blind 10: formats/invalid binary values | medium | Socket cases tie UPDATE to binary and use a uniform format code. Add text-etag UPDATE, mixed formats, and malformed binary JSONB/etag cases. |
| Verification gap 1: authorization self-comparison | medium | Live expected authorization is produced by the same helper as the binding; existing deterministic tests cover only old modes. Assert the one-key phase filename independently in deterministic and deserialized-live checks. |
| Independent fix verification: startup CREATE TABLE shape | medium | Follow-up edge reviewer confirmed the eleven fixes by inspection, then matched startup SQL to pinned source and found two literals omitted whitespace preserved by normalization. Correct those exact literals and use actual multiline source-shaped positive test inputs; remaining migration shapes matched. |

## Verification

Build isolated host/tests in Debug with `-p:UseHexalithProjectReferences=true -m:1`.
Run built xUnit assemblies with single-dash class filters: new focused tests,
existing dependency-free regressions, then fresh-expiry one-key and existing
two-key/recovery live classes. Keep raw output ignored. Record exact commands,
counts and cleanup checks. Compare protected baseline hashes afterward.


### Implementation handoff (2026-11-01)

The implementation adds a bound ordinary one-key mode, strict protocol/key/value
selection, pre-Execute and successful-idle gates, complete ordinary/faulted write
accounting, full provider-row snapshots and a separate two-cycle live class. The
original two-key class and pre-existing corrections were preserved. Initial and
replacement schema initialization windows retain connection-attributed SQL hashes;
workload execution revalidates cached statements against the pinned Get/Set/ping
allowlist. No production files, dependencies, story/sprint state or resources were
changed. No live infrastructure was executed by the implementation agent.

Commands executed from repository root:

```bash
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*OneKeyProtocolTests' -class '*OneKeyAttemptValidatorTests' -class '*OneKeyAttemptJournalTests' -class '*BindingTests' -class '*FixturePreflightTests' -class '*TransportLifecycleTests' -class '*ShardActorExpiryTests' -class '*ShardActorRecoveryTests' -class '*PostgresFaultProxyTests' -class '*PostgresFaultProxyFailureTests' -class '*PostgresFaultProxyIsolationTests' -class '*AttemptJournalTests' -class '*ShardAttemptValidatorTests' -class '*FaultExperimentTests' -class '*FixtureProxyCleanupTests'
```

The final build passed with zero warnings/errors, including its referenced host.
The final built-assembly run passed 146 cases, zero failures/skips/not-run: 49 new
one-key cases and 97 existing dependency-free regressions. Raw output:
`artifacts/sharding/one-key-phase-20261101/implementation-build-final.log` and
`implementation-focused-final.log`. An initial build failed CA1822 for a helper
that did not access instance state; marking it static resolved the failure. The
initial focused run passed 46 cases; an intermediate regression filter passed 98
cases because `*AttemptJournalTests` also included the one new journal case.
Independent review, fresh-bound live execution and independent cleanup audit
remain the coordinator's next steps. Acceptance is not yet claimed.

### Coordinator verification (2026-11-01 local date)

The final isolated build passed with zero warnings/errors. Three independent
reviewers produced eleven initial findings; a follow-up independent source check
found the CREATE TABLE whitespace issue. All twelve findings were patched and
verified; none were deferred. The reviewed final scenario preserves two separate
faulted reservation rows, seeded above 2^53, unchanged until complete snapshots
have passed after provider recovery and host replacement. Subsequent distinct
allocations follow those checks. Stale generations deny without changing full
provider rows at both stages.

Commands below ran from repository root. The ignored
`artifacts/sharding/one-key-phase-20261101/` directory retains each named command's
exact argument vector, UTC start/end, exit code and supplied expiry in
`<name>.command.json`, together with `<name>.log`. No CI identity exists for these
local executions. The approved subject remains candidate
`411005b4ccb2352249c83330c54967c6a22beba1`, successor blob
`160331d25451928ff3c3dea2300b65cab4f97c3b`, normative SHA-256
`995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`.

| Retained command name | Actual command / result |
|---|---|
| `final-build` | `dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal` — exit 0, zero warnings/errors; referenced host built too. |
| `final-focused` | Built assembly with explicit positive class filters listed below — exit 0, **176 passed**, zero failed/skipped/not-run. |
| `live-one-key` | `dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderOneKeyTests'` — exit 0, **1 passed**, 98.859 seconds. |
| `live-preserved-regressions` | Same assembly, `-class '*ProviderCommitAmbiguityTests' -class '*ProviderRecoveryTests' -class '*SyntheticShardingTests' -class '*LifecycleTests'` — exit 0, **13 passed**, zero failed/skipped, one explicit expiry case not run by default; 191.959 seconds. Three overlapping transport cases are not extra distinct coverage. |
| `live-exclusive-expiry` | Same assembly, `-class '*ExpiryLifecycleTests' -explicit only` — exit 0, **1 passed**, zero failed/skipped/not-run; separately executes the preceding explicit case. |
| `live-proxy-expiry` | Same assembly, `-class '*ProviderProxyExpiryTests' -explicit only` — exit 0, **1 passed**, zero failed/skipped/not-run. |
| `global-position-build` | `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal` — exit 0, zero warnings/errors. |
| `global-position-tests` | `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPosition*'` — exit 0, **34 passed**, zero failed/skipped/not-run. |
| `readme-lint` | `npx --no-install markdownlint-cli2 tests/Hexalith.EventStore.Sharding.Tests/README.md` — exit 0, zero issues. |

The final focused invocation was:

```bash
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*BindingTests' -class '*FixturePreflightTests' -class '*TransportLifecycleTests' -class '*ShardActorExpiryTests' -class '*ShardActorRecoveryTests' -class '*PostgresFaultProxyTests' -class '*PostgresFaultProxyFailureTests' -class '*PostgresFaultProxyIsolationTests' -class '*AttemptJournalTests' -class '*ShardAttemptValidatorTests' -class '*FaultExperimentTests' -class '*FixtureProxyCleanupTests' -class '*OneKeyProtocolTests' -class '*OneKeyAttemptValidatorTests'
```

The one-key run and retained live regression batch supplied
`EVENTSTORE_SHARDING_EXPIRES_UTC=2026-10-31T23:58:00Z`; the one-key binding was
created at `2026-10-31T23:28:08Z`. Explicit expiry regressions used separately
recorded deadlines `2026-10-31T23:33:55Z` and `2026-10-31T23:34:22Z` selected and
recorded before execution. `live-plan.json` and the two expiry plan records bind
environment, ownership and fence/drain/teardown intent. Each fixture wrote its
own binding before resource creation: 102 host files, 195 test output files and
78 source files, exact configuration and immutable image identities. Observed
profile: SDK 10.0.403, .NET 10.0.14, daprd 1.18.7, PostgreSQL 18.4,
placement/scheduler 1.18.1, Docker 29.8.2, Linux x64, local Unix Docker socket,
two independent hosts/sidecars and plaintext loopback provider transport.

One-key artifact identity: `sharding-423176f63f3f4cad815bf295e7736cea`. Final
accounting reconciled **64 mutations, 224 journal records, six host PIDs and 48
write-frame observations**: 40 acknowledged saves, eight uncertain faults and
16 known rejections. Four fault executions were held without forwarding or
trigger execution; four committed with acknowledgements withheld. The eight
per-fault sequence samples were zero/one as appropriate before any restart.
Forty ordinary executions completed successfully. No unexplained actor/provider
replay or explicit workload transaction passed validation. The test checked 57
unique acknowledged counters, ten abandoned committed reservation counters,
80 complete application-state snapshots, two same-storage recoveries (graceful
then SIGKILL), and two replacements of both host/sidecar pairs. Missing admission,
exact generation strings, ceilings and the final Int64 overflow denial survived.
The actual actor save implementation is unchanged; no witness row was present.

The first live one-key experiment passed; no live experiment failed. Earlier
development failures remain recorded: the initial CA1822 build failure described
above and coordinator `-notclass` runner invocation (exit 3, unsupported option,
no tests executed). Positive class filters corrected the invocation. Intermediate
146/79-case runs are retained and are not counted as additional final coverage.

Independent script command:
`python3 artifacts/sharding/one-key-phase-20261101/verify_cleanup.py <the 13 paths in run-directories.json>`.
It passed **2,619 checks across all 13 invocations**, separately probing exact
container IDs, ownership labels, named volumes, recorded host/sidecar and
transient command PIDs, private scratch and bound listeners, and checking proxy
session drain. `independent-cleanup.json` retains actual Docker query vectors and
results. Each fixture cleanup receipt also passed. `source-drift-check.json` and
`output-drift-check.json` found no source or output drift in all thirteen bound
invocations. Redacted ignored evidence is retained; no evidence was force-added.

A second independent agent audit passed all thirteen invocations and retained
`second-cleanup-audit.json`: 68 fresh read-only Docker queries covered ownership
labels, 29 exact container IDs and provider-volume identities; `/proc` checks
covered 56 host/sidecar PID records and 2,251 transient command PID records;
thirteen scratch paths were absent and 200 bound-port probes returned connection
refused. Both expiry receipts were included. No resources were changed by either
independent audit.

Final preservation verification matched all 9,821 protected tracked regular files
against the invocation baseline, including every reviewed dirty correction,
production/configuration/dependency file, approved specification and story/sprint
record. Only the seven intended tracked isolated harness/README files differ;
thirteen isolated test source files and this new phase record are added locally.
`git diff --check` and untracked-file whitespace checks passed. HEAD remains
`7ead2f24547c0669b86686340c58c83d883157be`; index contents are unchanged. No staging,
commit, push, merge, release or publication occurred. Only this bounded phase is
complete; the publication hold remains in force.

### Remaining qualification gaps

Completion applies only to this instrumented synthetic development lane. The
original story/sprint state and follow-up checklist remain unchanged. This does
not qualify production, another runtime/provider version, deployed TLS/transport,
commit cancellation, arbitrary transaction errors or overlapping routing faults.
Real allocator/command/event result fidelity, admission lineage and lifecycle,
backup/restore, HA, physical fencing, split-brain, public wire/cursor contracts,
production capacity and operational/CI qualification remain unproven. The exact
trigger/sequence and plaintext proxy alter the tested profile. Sequence samples
precede recovery and supply no general crash-durable execution ledger. Host
journals cover this process-replacement experiment; they do not prove machine-loss
recovery. The watchdog lives inside the test process; externally killed process
or machine recovery still requires the exact ownership journal. The explicit
expiry cases cover retained default/two-key modes, not a new live one-key save
racing expiry. No full solution or broader production suite/CI run is claimed.
