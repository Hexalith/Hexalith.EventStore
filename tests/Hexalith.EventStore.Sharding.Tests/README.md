# Isolated synthetic sharding lane

This test-only lane runs two independent .NET hosts and Dapr sidecars against one
disposable PostgreSQL database. Its actor implements exact tenant/domain admission
and durable reservation ceilings. It does not reference or register production
EventStore code. These synthetic results qualify no production section 7 gate,
provider profile, physical fencing guarantee, metadata wire format, or capacity.
The approved planning subject remains commit
`411005b4ccb2352249c83330c54967c6a22beba1`, successor blob
`160331d25451928ff3c3dea2300b65cab4f97c3b`, normative SHA-256
`995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`.
The original Story 4.6 remains awaiting operator evidence.

From the repository root, build both projects in Debug:

```bash
dotnet build tests/Hexalith.EventStore.Sharding.TestHost/Hexalith.EventStore.Sharding.TestHost.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*BindingTests'
```

Prerequisites: the pinned .NET SDK, a working local Docker server, installed
`~/.dapr/bin/daprd` with SQLite name resolution, and these locally available
images. The fixture uses `--pull=never` and never changes the installed runtime.
It resolves Docker's flag/environment/context precedence, requires a local
`unix:///...` or `npipe:////./pipe/...` endpoint, and pins that endpoint on every
server command. TCP, SSH, and other remote transports are rejected before
resource creation, including a TCP connection to localhost.

- PostgreSQL: `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`
- Placement: `ghcr.io/dapr/placement@sha256:6caf20016d115d4a7f133b9206b739a10abd9f558d76683b27be9ab60f759e26`
- Scheduler: `ghcr.io/dapr/scheduler@sha256:c9bb9ada0cd6a63cd92c26470da1985124e423432af4e39f09b96979fd1059c0`

Before live execution, the operator supplies an **exclusive** UTC expiry in
`EVENTSTORE_SHARDING_EXPIRES_UTC`, exactly `yyyy-MM-ddTHH:mm:ssZ`, later than now
and at most one hour away. Missing, invalid, expired, and overlong input fails
before any child process or resource creation. For example, set a reviewed
absolute value in your shell, then invoke the built executable directly:

```bash
export EVENTSTORE_SHARDING_EXPIRES_UTC='<operator-selected future UTC second>'
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*SyntheticShardingTests' -class '*LifecycleTests'
```

`ExpiryLifecycleTests` is explicit and does not run by default. To exercise the
actual deadline, supply an operator-selected UTC second about 10–20 seconds away
and run only that class with xUnit's explicit switch:

```bash
export EVENTSTORE_SHARDING_EXPIRES_UTC='<operator-selected short future UTC second>'
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ExpiryLifecycleTests' -explicit only
```

That explicit test proves lifecycle cancellation and teardown at the actual
operator deadline, including expiry during startup. It supplies no live
provider-state evidence for writes racing expiry. `ShardActorExpiryTests`
instead invokes the actual actor with isolated staged/committed state and pauses
its read, stage and save boundaries. Entry/read/stage expiry is a known denial;
once saving starts cancellation is uncertain and propagates. The gated fake's
save cancellation is known to precede its commit; only that instrumentation proves
unchanged state there. Focused local checks require no Docker:

```bash
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*FixturePreflightTests' -class '*TransportLifecycleTests' -class '*ShardActorExpiryTests'
```

Before the first container is created, the fixture writes `binding.json` with
the subject above, complete host/test output SHA-256 manifests, source hashes,
actual SDK/runtime/Docker/image identities, local environment, ports, private
topology, configuration hashes, creation time and operator expiry. The host
requires `EVENTSTORE_SHARDING_BINDING` (set by the fixture), verifies its exact
artifact manifest and subject, and fences/cancels writes at expiry. This is a
local execution binding under the recorded user authorization, not an invented
authenticated signature or production authority.
Cancellation is armed immediately after parsing expiry and covers preflight.
PostgreSQL readiness checks its final TCP listener and selected database.
Placement and scheduler versions are observed from bounded startup logs and
paired with their immutable image identities; their versions need not equal the
installed daprd version.

The initial live Dapr/PostgreSQL JSONB round trip rounded a numeric ceiling from
`9223372036854775806` to `9223372036854776000`, causing Int64 deserialization to
fail after restart. The isolated candidate therefore writes its Int64
`Generation`, `Ceiling`, `SeedCeiling`, and `First` members as decimal JSON strings
and reads them exactly. The bounds scenario uses `long.MaxValue` as its generation
and checks typed PostgreSQL state and string digits immediately after seeding,
then again across restarts. A focused serialization regression checks maximum
and adjacent values. This encoding belongs only to these test DTOs; it supplies
no public metadata wire-format conformance evidence.

Every run chooses a fresh app ID shared only by its two sidecars. PostgreSQL,
placement, scheduler and SQLite discovery belong to that run. Network listeners
use loopback host bindings; passwords and ports are random. Namespace alone is
not used for discovery isolation. No Redis, Sample application, production DI,
global solution edit, or shared control-plane service is involved.

The ordinary live test checks 64 overlapping variable batches through both
sidecars, 12 exact-pair variants, rejection end states, unused reservations,
two replacement cycles of both host/sidecar pairs, the final Int64 counter and
overflow with unchanged complete JSONB state. Batch replies are correlated to
their submitted counts and an independently fixed total of 316 counters. A
separate admission is restarted at ceiling zero before its first reservation.
Five injected startup failures exercise teardown after named-volume creation, PostgreSQL, placement,
the first actor host pair, and engine creation with a missing CID file.
There is no throughput benchmark or representative production trace.

Completion, startup failure and exclusive expiry fence new work, cancel actual
actor HTTP and command tasks, drain them and the lifecycle lock, stop exact owned
process trees, remove exact recorded container IDs, anonymous volumes and the owned named provider volume, verify
absence, and remove private scratch. Missing CIDs are reconciled only by an exact
planned name with the unique run ownership label. A separate label query checks
that no owned container remains. Cleanup aggregates diagnostic failures while
continuing teardown and permits retries; prior failures remain in the receipt.
The expiry watchdog runs in the test process; forcibly killing that process or
the machine prevents its cleanup handler from running. Hosts still fence writes
at their bound expiry. `ownership.json` is atomically refreshed outside scratch
before each container launch and immediately after every recorded container or
host/sidecar process starts. It contains planned names, labels, recovered IDs,
PIDs and process start times without credentials. Inspect that journal for manual
recovery after an external interruption, checking process start time before
acting on a PID; never prune other users' resources.

Evidence is retained in ignored `artifacts/sharding/<run-id>/`, outside deleted
scratch: binding, sources, redacted configuration and bounded child logs,
commands, operation/provider observations, and `cleanup.json`. Secrets and
scratch paths are redacted; exact artifact/configuration hashes remain available.
Command stdout/stderr are streamed into bounded captures, retaining exact small
results and a marked tail for larger output. Docker log retrieval also uses an
explicit bounded tail.
Keep console/xUnit output in the same ignored artifact policy. An absence check
or teardown error fails the run. No commit or CI run is implied.

The separate production-v1/local-mechanics regression is:

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPosition*'
```

## Same-storage provider recovery

Live classes share a nonparallel xUnit collection to avoid races between fixtures
selecting temporary ports; each fixture still runs two independent hosts/sidecars.
Sidecar readiness also checks the bound application identity before actor calls.

The focused recovery scenario uses the same PostgreSQL container and a uniquely
named volume mounted at `/var/lib/postgresql` throughout graceful stop/start and
abrupt SIGKILL/start. Its planned name, ownership label and explicit removal
requirement are in the binding before creation. The ownership journal retains
the actual container, volume/mount and process identities. Recovery compares the
exact mount and PostgreSQL system identifier. Completion, partial startup failure
and expiry remove the named volume explicitly and verify no owned volume remains.

```bash
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ShardActorRecoveryTests'
export EVENTSTORE_SHARDING_EXPIRES_UTC='<operator-selected future UTC second>'
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderRecoveryTests'
```

Observable fault boundaries are the confirmed stopped container, a granted
exclusive `public.state` lock plus a PostgreSQL `RowExclusiveLock` waiter before
SIGKILL, and a fully received successful HTTP response discarded before the actor
proxy receives it. The tracked lock-holder command runs `pg_sleep(25)` within
the fixture's 30-second command timeout; the two boundary waits are separately bounded.
Failure to observe either SQL boundary fails the test. Both original host/sidecar
pairs stay alive during provider recovery; they are then replaced and continuity
is checked again. A graceful stop requires exit 0, and SIGKILL requires exit 137.
The image's configured stop signal is SIGINT (PostgreSQL fast shutdown).

Observations distinguish `acknowledged-success`, `known-rejection` and `uncertain`.
Only explicit rejection replies receive a no-mutation assertion. Exceptions never
trigger a test retry. An uncertain reservation permits either the prior complete
record or one complete additional range, and durable inspection determines the
next distinct allocation. A deliberately lost successful response leaves its full
17-counter reservation as a gap. Failed saves clear the actor's staged/cache state
and propagate, including cancellation after saving starts. Deterministic fakes
exercise both commit-before-failure and failure-before-commit outcomes.

The HTTP transport records sends and rejects a second SDK send. Recovery checks
assert one send outside the expected exception, plus actual discarded success for
the lost-response case. The lane loads a
scoped actor/component retry policy with `maxRetries: 0`, and binds its exact YAML
hash. This follows Dapr's [retry policy](https://docs.dapr.io/operations/resiliency/policies/retries/retries-overview/)
and [target configuration](https://docs.dapr.io/operations/resiliency/targets/).
Dapr's [built-in routing retries](https://docs.dapr.io/operations/resiliency/policies/retries/override-default-retries/)
and component internals remain runtime-specific; no general exactly-once transport
guarantee is claimed. A repeated reservation would fail the complete durable
outcome assertions. The lost-response injection is on the caller transport after
a successful sidecar reply; it does not emulate every network partition.

The recovery scenario checks eight exact pair variants, zero-ceiling admission,
near-max Int64 string generation/ceiling persistence, missing-authority denial,
unused ceilings, uniqueness of all acknowledged counters and subsequent process
replacement. It is a bounded synthetic development check. It qualifies no
production capacity, backup/restore, HA, split-brain, physical fencing or full
production section 7 evidence category. Remaining work is recorded in
`_bmad-output/implementation-artifacts/spec-4-6-recovery-qualification-follow-up.md`.

## Provider COMMIT and interrupted admission

`ProviderCommitAmbiguityTests` enables an optional loopback PostgreSQL protocol
proxy. Ordinary synthetic/provider regressions keep their direct connection.
Before creation, the binding records both proxy/provider ports, its exact source
and output manifests, plaintext framing requirement (`sslmode=disable`), retained
attempt-journal directory, execution counters and owned teardown. The proxy never
logs startup, authentication, credentials or raw protocol payloads.

The installed daprd 1.18.7 pins `components-contrib` v1.18.6. Its PostgreSQL v1
[`Multi` implementation](https://github.com/dapr/components-contrib/blob/v1.18.6/common/component/postgresql/v1/postgresql.go#L469-L493)
skips an explicit transaction for one state operation (lines 474–479). Proxy mode
therefore stages a second test-only `commit-witness` key containing the operation
ID and exact next authority in the same `SaveStateAsync`. This selects the
provider's real multi-operation transaction/COMMIT path. Both durable keys are
inspected before and after every fault and recovery; the witness never supplies
admission authority. This experiment qualifies only its synthetic two-key path.
The direct one-key path is unchanged and receives no COMMIT-boundary claim.

For each fault a unique test-only `AFTER INSERT OR UPDATE` trigger matches the
exact synthetic state key, increments a nontransactional sequence and emits a
unique PostgreSQL NOTICE. Only the connection receiving that exact NOTICE can
reach the selected gate. The proxy withholds either the frontend COMMIT before
forwarding, or the backend `CommandComplete(COMMIT)` before Dapr receives it.
The test observes the actual boundary, directly inspects the complete durable
record while the response is withheld, explicitly breaks the connection, drains
the request and inspects again. It never replays an ambiguous operation.

Eight faults cover reservations and fresh admissions at both boundaries across
two full same-storage provider-restart and two-host/sidecar-replacement cycles.
Before-COMMIT absence/previous state is asserted only with observed instrumentation
proving no forwarded COMMIT or retry. Lost acknowledgements require complete
committed authority, preserving exact generation and zero admission ceiling.
Every subsequent distinct allocation follows durable inspection and exceeds its
inspected/acknowledged ceiling. The test checks 63 acknowledged counters, unused
durable ranges, eight exact pair variants, retained missing-authority rejection,
decimal Int64 strings across every cycle and overflow before mutation.

Optional operation IDs observe actual host actor entry, save start and successful
save completion, failed saves after cache eviction, and actor exit in flushed
per-host JSONL files retained across replacements. A caller exception alone does
not establish actor completion: fault accounting waits for the flushed exit.
The fixture refuses mutating ID reuse; the actor performs no deduplication.
Every runtime duplicate therefore remains visible. All SDK sends, exact-key
sequence executions and NOTICE/COMMIT/completion counts are asserted outside
expected exceptions, before trigger removal and provider restart. This timing
preserves the evidence even though PostgreSQL sequences need not preserve their
latest values across a database crash. A runtime that never exposes the exact
NOTICE fails instead of inferring readiness.

Journal failures fail the experiment independently from persistence. A failed
start observation prevents saving and evicts staged state; a failed completion
observation leaves the successful save distinguishable. Failed-save and actor-exit
reporting retain the original exception and attach secondary journal failures.

Final accounting reconciles every full submitted mutation, SDK outcome, exact
actor route and owned host PID with its permitted ordered execution stages. The
complete connection-attributed COMMIT ledger must match actor save starts,
successful saves and the explicitly observed fault completions; unexplained
provider replay, duplicate no-save rejection or journal attribution fails the
case. Observation/assertion failures still break observed gates, drain and record
the request, await actor exit and attempt all independent durable/attempt reads
before owned storage is deleted. Diagnostic timeout evidence remains visible.

```bash
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*PostgresFaultProxyTests' -class '*PostgresFaultProxyFailureTests' -class '*PostgresFaultProxyIsolationTests' -class '*AttemptJournalTests' -class '*ShardAttemptValidatorTests' -class '*FaultExperimentTests' -class '*FixtureProxyCleanupTests'
export EVENTSTORE_SHARDING_EXPIRES_UTC='<operator-selected future UTC second>'
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderCommitAmbiguityTests'
```

Completion, injected startup failure after proxy creation and expiry cancel the
listener and both directions of every accepted session, observe their tasks and
independently probe listener absence. Counts and listener/session absence are in
the retained ownership/cleanup receipts. Startup failures cover both listener
creation and the existing first-node point with accepted provider sessions.
Unexpected truncated frames and listener failures fail the run while teardown
still drains every accepted task. An explicit live proxy expiry case is:

```bash
export EVENTSTORE_SHARDING_EXPIRES_UTC='<operator-selected UTC second allowing proxy startup>'
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderProxyExpiryTests' -explicit only
```

Focused dependency-free tests cover fragmented/malformed frames, both COMMIT
gates, simple/extended protocol execution, exact NOTICE selection, explicit
connection break, cancellation with a held gate, observation counters and
append-only actor attempts. This lane supplies bounded synthetic development
evidence; it does not qualify production capacity, backup/restore, HA, physical
fencing, split-brain or production section 7. Remaining qualification work is in
the [provider ambiguity follow-up](../../_bmad-output/implementation-artifacts/spec-4-6-provider-ambiguity-qualification-follow-up.md).

## Ordinary one-key provider boundaries

`ProviderOneKeyTests` binds a separate `provider-one-key` profile. It retains the
actor's existing ordinary single-key `SaveStateAsync`; the proxy no longer implies
that a companion witness must be staged. The two-key COMMIT class remains a
separate regression.

The one-key profile selects only the pinned PostgreSQL v1 component's exact
no-TTL INSERT/upsert or etag UPDATE SQL and the complete `$1` key from supported
Parse/Bind/Execute frames. Text and binary JSONB/bool/etag formats are decoded
strictly. Named statements survive idle; portals expire at idle and honor Close.
Unknown workload SQL, simple-query writes, explicit transactions, partial or
multiple write executions, malformed parameters and unexplained completions fail.
The exact pgx `-- ping` empty query and pinned ordinary Get queries are supported.

The pre-execution gate holds the complete Execute frame before forwarding it;
its independent exact-key trigger sequence must remain zero. The post-commit gate
requires the exact trigger NOTICE, a successful one-row CommandComplete, and
ReadyForQuery with idle status. It buffers CommandComplete and all following
response frames through that idle observation. Neither CommandComplete nor Sync
alone establishes readiness. Explicit break or cancellation drains both relay
directions; cancellation cannot manufacture a boundary.

Component schema initialization is scoped to initial startup and replacement-host
startup. The binding declares that allowance, and the proxy retains connection,
stage, UTC and normalized SQL hashes for those windows. After both hosts are
ready, the workload allowlist applies again, including to execution of previously
prepared statements. The measured one-key save ledger permits zero explicit
COMMITs. Initialization metadata transactions do not represent actor saves.

Every ordinary or faulted write records its complete key/value, statement, portal,
connection, forwarding, command completion and idle commit. Final accounting
matches every registered mutation and one SDK send with one ordered actor turn
and its exact full provider transition. Extra executions and retries fail; faulted
operations are never replayed. Whole-table snapshots retain complete provider
rows, including `xmin`, binary/TTL flags, timestamps and row identity. The live
scenario rejects unexpected keys or witnesses, checks exact four-field state and
Int64 decimal strings, and compares complete snapshots through graceful then
SIGKILL recovery and two replacements of both host/sidecar pairs. Admission and
reservation faults at both boundaries run in each cycle. Subsequent distinct
allocations skip committed abandoned ranges; denials and final overflow preserve
complete rows.

```bash
dotnet build tests/Hexalith.EventStore.Sharding.TestHost/Hexalith.EventStore.Sharding.TestHost.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet build tests/Hexalith.EventStore.Sharding.Tests/Hexalith.EventStore.Sharding.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*OneKeyProtocolTests' -class '*OneKeyAttemptValidatorTests' -class '*OneKeyAttemptJournalTests'
export EVENTSTORE_SHARDING_EXPIRES_UTC='<authorized future exclusive UTC second within one hour>'
dotnet tests/Hexalith.EventStore.Sharding.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sharding.Tests.dll -class '*ProviderOneKeyTests'
```

Implementation verification on 2026-11-01 built the isolated projects in Debug
with zero warnings/errors and passed 146 focused cases (49 new one-key cases and
97 existing dependency-free regressions). The initial build failed CA1822 for a
helper lacking instance access; marking it static resolved the failure. Raw
commands/results are retained under ignored
`artifacts/sharding/one-key-phase-20261101/implementation-*.log`. Independent
review, live execution and independent cleanup audit belong to the coordinator's
phase verification record; the implementation agent ran no live infrastructure.
This remains synthetic local development evidence, with no production
qualification, capacity, HA, backup/restore, physical fencing or split-brain claim.
