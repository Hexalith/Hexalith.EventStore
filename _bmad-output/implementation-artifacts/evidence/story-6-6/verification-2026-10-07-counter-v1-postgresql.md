# Story 6.6: Counter bounded V1 writing and PostgreSQL logical recovery

The owner directed this run to derive the missing application declarations and
qualification inputs from the repository. The resulting
[Counter declaration and registration](verification-2026-10-07-counter-v1-serialization.md)
is now exercised through the actual Sample host, domain-service router, Dapr
invocation and EventStore aggregate actor. The parent Story 6.6 remains
**in progress**; its original baseline and frozen intent are preserved.

## Application and runtime inputs

Counter's six emitted payload types have explicit exact legacy aliases, `json`
format and source-derived byte ceilings: five two-byte marker records and the
309-byte maximum framework termination rejection. Its specialized serializers
have no general JSON options or converter graph to infer. The committed
application shape and independent compatibility tests define these local
declarations. This supplies Counter's concrete V1 writer input, not a complete
authenticated event-evolution registry or deployment catalog.

The local qualification profile uses .NET runtime **10.0.12**, Dapr **1.18.4**,
the repository's `state.postgresql` v1 component with `actorStateStore: true`,
and its tracked resiliency configuration. Two independent EventStore processes
and their sidecars share that component; a separate Sample process and sidecar
execute the real bounded writer. The PostgreSQL image is the fixture's pinned
`postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
Placement and scheduler use
`daprio/dapr@sha256:68bb6057abbd3cc1267ad895a73415426ba77f2b451de99693aac54b45ea7d0e`.

This is a **Testing** profile with loopback sidecars, fixture-issued symmetric
credentials, private SQLite service discovery and Redis pub/sub. It does not
represent the AD-26 Kubernetes/OpenBao/durable-broker production profile. The
sole production inventory path, `deploy/dapr/production-profile.yaml`, remains
owned by Story 3.19 and its separate authenticated publication/ratification
requirements. No production identity, release, traffic or promotion is claimed.

## Executed logical-state assertions

The [retained test](counter-v1-postgresql-2026-10-07/CounterPostgresqlLogicalReadbackProbe.cs)
submits an increment through the public command boundary, then reads the
persisted event and aggregate metadata through each sidecar's public Dapr
actor-state API. It checks exact `{}` payload bytes, the existing CLR alias and
format, addressed identity, V1 metadata, application digest and sequence one.

The same idempotency key is submitted through the other host, after stopping
one host/sidecar, and after restarting that host/sidecar. Each response preserves
the original **command** message identity and result. Independently read events
preserve their own **event** message identity, all envelope metadata, extensions,
payload bytes and digest. Aggregate head stays one, and the actual Sample
`/process` boundary runs exactly once. Command and event message identities are
separate values in the existing contract.

No SQL query, provider key, direct database read or provider-signed evidence is
used for these assertions. Container setup uses the existing fixture's
PostgreSQL readiness probe. This proves current logical actor values and
duplicate recovery for this local V1 path; it does not prove historical
generation authentication, lost-ack reconciliation, complete mixed-version
consumer equivalence or every crash boundary.

## Isolation and reproducibility

[The runner](counter-v1-postgresql-2026-10-07/verify-counter-postgresql.py)
copies the live test project into a temporary workspace. The original OQ8
fixture is an exact-source input to an earlier sealed packet, so it is preserved.
The copy adds a Dapr actor-state read helper, records the owning source path for
the existing diagnostic redactor, and places fixture scratch under that
workspace for exact cleanup. Its private discovery configuration disables hot
reload to avoid the observed machine file-watch exhaustion. Real Sample and
EventStore runtime source is unchanged by this instrumentation.

Project references resolve to the owning source paths to avoid duplicate
MSBuild identities through temporary symlinks. Every subprocess command and
timeout is recorded before launch. Timeout handling stops the owned process
group; a retained fixture container identity supplies exact PostgreSQL cleanup.
Control-plane cleanup addresses only this run's unique container names/IDs.

The runner retains consumed script bytes, instrumented source, original input
hashes, managed test/real-host runtime hashes, commands, logs, XML, sanitized
fixture observations and cleanup. It checks exactly one passing test, no errors
or skips, unchanged original source and unchanged images during execution.
Earlier failed attempts remain recorded: temporary-project reference paths,
file-watch exhaustion, a test tenant outside the fixture credential's grants,
one unavailable Docker-forwarded port, and the corrected command/event identity
assertion. No production gate or expected domain behavior was weakened.

Reproduce from this repository:

```sh
python3 _bmad-output/implementation-artifacts/evidence/story-6-6/counter-v1-postgresql-2026-10-07/verify-counter-postgresql.py
```

## Results and remaining scope

| Check | Result and capture |
| --- | --- |
| Final isolated Debug/source build | Zero warnings/errors; [build log](counter-v1-postgresql-2026-10-07/runs/20261007T075741946126Z/build.log). |
| Final logical readback/recovery test | One passed; zero failures, skips or assembly errors; [XML](counter-v1-postgresql-2026-10-07/runs/20261007T075741946126Z/test.xml), [commands](counter-v1-postgresql-2026-10-07/runs/20261007T075741946126Z/commands.json). |
| Exact runtime and observed end state | [Inputs and image hashes](counter-v1-postgresql-2026-10-07/runs/20261007T075741946126Z/inputs.json), [sanitized observations](counter-v1-postgresql-2026-10-07/runs/20261007T075741946126Z/fixture-evidence/observations.json). |
| Owned resources | [Fixture cleanup](counter-v1-postgresql-2026-10-07/runs/20261007T075741946126Z/fixture-cleanup.json) confirms processes stopped and scratch removed; both owned control-plane cleanup commands passed. The original four Dapr containers remain. |
| Sample compatibility/regression | 17 focused and 175 full-suite tests passed without skips; [evidence](verification-2026-10-07-counter-v1-serialization.md). |
| Required package-mode Release solution build | Zero warnings/errors; 31.90 seconds; [build log](counter-v1-postgresql-2026-10-07/validation/release-build.log). |
| Current amendment and historical approval preflights | Both `--mutations` runs passed as separate session-tool observations; current preflight rejected eleven policy mutations and retained the V2 fence. Historical digest remains `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`. |

These checks close the Counter local declaration/writer and logical recovery
prerequisites only. Full registry and immutable artifact admission, qualified
loader observations, all shared consumer integrations, control/recovery
capabilities, migration/fleet compatibility, production profile authority and
the rest of M1–M8 remain open. No parent task or O-row is marked complete, and
V2 admission remains fenced. This run does not replace the parent workflow's
remaining acceptance and review stages.
