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
its read, stage and save boundaries; these deterministic checks prove denial and
unchanged state at those boundaries. Focused local checks require no Docker:

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
Four injected startup failures exercise teardown after PostgreSQL, placement,
the first actor host pair, and engine creation with a missing CID file.
There is no throughput benchmark or representative production trace.

Completion, startup failure and exclusive expiry fence new work, cancel actual
actor HTTP and command tasks, drain them and the lifecycle lock, stop exact owned
process trees, remove exact recorded container IDs and anonymous volumes, verify
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
