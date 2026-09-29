# Bounded 3.109.0 to 3.70.1 rollback rehearsal

Observed 2026-09-29. On 2026-09-29, Jérôme Piquot retargeted the pending P1R candidate to EventStore `3.109.0` and kept `3.70.1` as the rollback target. This is an isolated, executable storage-contract rehearsal. It is **not** a production deployment and not an approval of any part of the P1R prerequisite chain. The earlier [3.108.1 rehearsal](../../6-1-p1r-3108/rollback-probe/README.md) is unchanged and superseded as the candidate input.

## Exact package and checkout coordinates

| Coordinate | Result |
| --- | --- |
| Forward release | `v3.109.0` tag `818e28a8af421994e4f77e66327dc33a8c67ca5f`; published `Hexalith.EventStore.Server` with its transitive `Contracts` at `3.109.0`. Both `.nuspec` repository elements name `818e28a8…`. |
| Rollback release | `v3.70.1` tag `f13f9925fdca53efa2ab8c90d396ab106f91bb9c`; published `Server` with its transitive `Contracts` at `3.70.1` |
| Rollback archive build source | Both `3.70.1` `.nuspec` repository elements name `650faf053a98ed1c03c048b8e0d2e4d281b095cf`, the direct parent of tag `f13f9925…`. `git diff --quiet 650faf… f13f… -- src tools/release-packages.json` exited `0`. The package source commit and the tag commit remain distinct coordinates. |
| Later source checkout | `HEAD=489e5d76253f68c6cf53c443aa8755ad059e621e`, 28 commits after `v3.109.0`. It is not a source coordinate for the published `3.109.0` archives. |

The downloaded NuGet.org-signed archive hashes were:

| Archive | SHA-256 |
| --- | --- |
| `Server 3.109.0` | `c77537fc6a96cb56413652bdee76375551fe2d2ce17a902a954bf6e5eb1016b6` |
| `Contracts 3.109.0` | `38f8ab2f116b477a2b8588810ce6ef31d8155e6c6c4f4cbd9d2d8bf76e8d0952` |
| `Server 3.70.1` | `a9b1f8e8b83b069421951f386572042f67ef31a58f024310b6ff9ad794567f74` |
| `Contracts 3.70.1` | `ee6cb34319615cf2612b75c6298aad3c1862ee07ce3cb78fa39deee1c62b7c1a` |

The two `3.109.0` hashes equal the NuGet.org values in [../public-packages.json](../public-packages.json). `dotnet nuget verify --all` over those four archive paths exited `0` with 4 repository signatures. Every restored `.nupkg.metadata` names `https://api.nuget.org/v3/index.json` as the source.

## Storage record identity

The probe exercises `Hexalith.EventStore.Server.Events.EventEnvelope` and `SnapshotRecord`. Their Git blobs are identical at `v3.70.1`, `v3.106.0`, `v3.108.1`, `v3.109.0`, and `489e5d76`:

| Source file | Blob |
| --- | --- |
| `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs` | `3aa7eded…` |
| `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` | `4178805e…` |
| `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` | `f434b50d…` |

`src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs` is identical (`8c552052…`) from `v3.70.1` through `v3.109.0`. It changes only after the tag, to `30a032ab…` at `489e5d76`, where `RetainedFloor` is added. The published `3.109.0` archives therefore persist the same metadata shape as `3.70.1`. The probe does not write `AggregateMetadata`, and metadata written by the later checkout was not tested.

## Executed isolated state transition

The unchanged [Program.cs](Program.cs) is shared with the 3.108.1 rehearsal (SHA-256 `392df2b4…`). It was compiled against each published `Server` package version by [v3109/Probe.csproj](v3109/Probe.csproj) and the unchanged [v370/Probe.csproj](v370/Probe.csproj). [NuGet.Config](NuGet.Config) was the sole package source. Both fresh restores passed, in `5.25` and `4.89` seconds, and both Release builds passed with **0 warnings and 0 errors**. The probe writes and reads actual `EventEnvelope` and `SnapshotRecord` instances using the actor-state fallback `JsonSerializerOptions.Web` shape.

The disposable backing store was `redis:7.4-alpine`, image `sha256:ff02b58f971e7d7d156a1267e283fcbbeee91773b6aa36c49dac28ecfe28eadf`. It ran as container `hexalith-p1r-rollback-3109-20260929`, bound only to `127.0.0.1:54740`. The Dapr CLI was `1.18.2` and the local runtime `1.18.4`. A unique app ID, `eventstore-p1r-rollback-3109-20260929`, loaded only an isolated `state.redis` v1 component named `rollbackstore` (`redisHost=127.0.0.1:54740`) on HTTP port `45615`. The shared local Dapr placement, scheduler, Redis, and Zipkin containers were not stopped or modified.

The `3.109.0` binary wrote the synthetic tenant `throwaway-tenant` with sequence `1`, payload `{"count":7}`, one extension, and a snapshot with count `7`, then read the persisted values back. The Dapr sidecar was then stopped and restarted while the same disposable Redis container was kept. After the restart, the `3.70.1` binary read and validated both records. The exact runs were:

```text
# Run from rollback-probe/v3109, with a fresh version-specific NUGET_PACKAGES directory:
DOTNET_CLI_HOME=/var/tmp/eventstore-p1r-rollback-3109-20260929/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-p1r-rollback-3109-20260929/packages-3109 dotnet restore Probe.csproj --configfile ../NuGet.Config --disable-parallel --no-http-cache -p:NuGetAudit=false
  exit 0; Restored Probe.csproj in 5.25 sec
DOTNET_CLI_HOME=/var/tmp/eventstore-p1r-rollback-3109-20260929/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-p1r-rollback-3109-20260929/packages-3109 dotnet build Probe.csproj --no-restore --configuration Release -warnaserror -p:NuGetAudit=false
  exit 0; Build succeeded, 0 Warning(s), 0 Error(s)

# Run from rollback-probe/v370, with a separate fresh package directory:
DOTNET_CLI_HOME=/var/tmp/eventstore-p1r-rollback-3109-20260929/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-p1r-rollback-3109-20260929/packages-370 dotnet restore Probe.csproj --configfile ../NuGet.Config --disable-parallel --no-http-cache -p:NuGetAudit=false
  exit 0; Restored Probe.csproj in 4.89 sec
DOTNET_CLI_HOME=/var/tmp/eventstore-p1r-rollback-3109-20260929/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-p1r-rollback-3109-20260929/packages-370 dotnet build Probe.csproj --no-restore --configuration Release -warnaserror -p:NuGetAudit=false
  exit 0; Build succeeded, 0 Warning(s), 0 Error(s)

docker run -d --rm --name hexalith-p1r-rollback-3109-20260929 -p 127.0.0.1::6379 redis:7.4-alpine
  exit 0; container 110b628c8c810a888f360ba03456c9fd0979184c1ec9f4d506fc853b05786e66; allocated host port 54740
dapr run --app-id eventstore-p1r-rollback-3109-20260929 --dapr-http-port 45615 --resources-path /var/tmp/eventstore-p1r-rollback-3109-20260929/resources --log-level error
  sidecar health GET /v1.0/healthz/outbound returned 204; metadata listed rollbackstore state.redis v1
# Run from rollback-probe/v3109:
dotnet bin/Release/net10.0/Probe.dll write http://127.0.0.1:45615 p1r-31090-to-3701
  exit 0; write: event_sha256=5fba2ba4fec0c7c0145dc6e394c8a8a5ca5a51dd71616f70039d4c830c5f7156 snapshot_sha256=3a1d0448fd86c621f374bbdfe2a4dd8cacaa427fd9e165f1f2409c2dbd9a6d02 sequence=1 snapshot_count=7
SIGINT to the first dapr run process group
  exit 0; Exited Dapr successfully
dapr run --app-id eventstore-p1r-rollback-3109-20260929 --dapr-http-port 45615 --resources-path /var/tmp/eventstore-p1r-rollback-3109-20260929/resources --log-level error
  sidecar health GET /v1.0/healthz/outbound returned 204; metadata listed rollbackstore state.redis v1
# Run from rollback-probe/v370:
dotnet bin/Release/net10.0/Probe.dll read http://127.0.0.1:45615 p1r-31090-to-3701
  exit 0; read: event_sha256=5fba2ba4fec0c7c0145dc6e394c8a8a5ca5a51dd71616f70039d4c830c5f7156 snapshot_sha256=3a1d0448fd86c621f374bbdfe2a4dd8cacaa427fd9e165f1f2409c2dbd9a6d02 sequence=1 snapshot_count=7
SIGINT to the second dapr run process group
  exit 0
```

After the rollback read, direct inspection of the disposable Redis store found exactly two keys, `eventstore-p1r-rollback-3109-20260929||p1r-31090-to-3701-{event,snapshot}`. Both are Redis hashes with `version=1`. Their `data` field SHA-256 values exactly match the Dapr read hashes above, and both have `tenantId=throwaway-tenant` and `sequenceNumber=1`. The checked values therefore survived the sidecar restart and the old-package read without mutation. `docker stop hexalith-p1r-rollback-3109-20260929` exited `0`, the `--rm` container disappeared, and `dapr list` showed no matching app ID. No non-throwaway state was touched.

## Executed disposable backup and restore

A separate run exercised the full-copy path instead of only restarting the sidecar:

1. The `3.109.0` binary wrote the same synthetic types to a fresh Redis container, `hexalith-p1r-backup-source-3109-20260929` (ID `4a5943b6c886f49bf77cc8e4d2bcd0fcfcc343f8c7f2891f8928a300551fd712`, loopback port `58435`). It wrote through a fresh Dapr sidecar (`app-id=eventstore-p1r-backup-3109-20260929`, HTTP port `45991`) using the unique key prefix `p1r-backup-restore-31090-to-3701`. Output: `event_sha256=58105c223b23b9c7a46b141fdeb66f7e46bc28d16bae53dfa9cab4d4fc5411f6 snapshot_sha256=0117b820bf04032327db0d27937b9316df076e0eb3581857775cc62c1db8da58 sequence=1 snapshot_count=7`, exit `0`.
2. After that sidecar stopped, the source Redis held exactly two keys whose `data` hashes matched the writer. `redis-cli SAVE` returned `OK` with exit `0`. The copied RDB had SHA-256 `a8d349989456a496b54c4fc273645d7f2e5e1614ce4b41627a02596b558251a7`, and the source container was then stopped.
3. A fresh restored container, `hexalith-p1r-backup-restore-3109-20260929` (ID `dfee91debffc2dde05ce997bc29f4e2d4b9d837b64b69dda9858614fa32e87a3`, loopback port `58441`), mounted only that throwaway RDB at `/data`. It reported `DBSIZE=2` before Dapr started.
4. The component's `redisHost` was changed to `127.0.0.1:58441`, and the same Dapr app ID and HTTP port were restarted. The `3.70.1` binary read both records with exit `0`, returning exactly the writer's event and snapshot hashes, sequence, and count.
5. Direct inspection of the restored Redis showed both `data` fields matching those hashes, both `version` fields at `1`, matching tenant and sequence values, and still exactly two keys.

The event and snapshot byte hashes differ between the two independent runs, and from the 3.108.1 rehearsal. Only within-run equality across the restart or copy and the old reader is claimed, and the cross-run difference was not investigated.

```text
docker run -d --rm --name hexalith-p1r-backup-source-3109-20260929 -p 127.0.0.1::6379 redis:7.4-alpine
  exit 0; source port 58435
dapr run --app-id eventstore-p1r-backup-3109-20260929 --dapr-http-port 45991 --resources-path /var/tmp/eventstore-p1r-backup-restore-3109-20260929/resources --log-level error
  health GET /v1.0/healthz/outbound returned 204; rollbackstore state.redis v1 loaded
# Run from rollback-probe/v3109:
dotnet bin/Release/net10.0/Probe.dll write http://127.0.0.1:45991 p1r-backup-restore-31090-to-3701
  exit 0; event=58105c223b23b9c7a46b141fdeb66f7e46bc28d16bae53dfa9cab4d4fc5411f6 snapshot=0117b820bf04032327db0d27937b9316df076e0eb3581857775cc62c1db8da58
SIGINT to dapr run
  exit 0
docker exec hexalith-p1r-backup-source-3109-20260929 redis-cli SAVE
  exit 0; OK
docker cp hexalith-p1r-backup-source-3109-20260929:/data/dump.rdb /var/tmp/eventstore-p1r-backup-restore-3109-20260929/restore/dump.rdb
  exit 0; sha256=a8d349989456a496b54c4fc273645d7f2e5e1614ce4b41627a02596b558251a7
docker stop hexalith-p1r-backup-source-3109-20260929
  exit 0
docker run -d --rm --name hexalith-p1r-backup-restore-3109-20260929 -p 127.0.0.1::6379 -v /var/tmp/eventstore-p1r-backup-restore-3109-20260929/restore:/data redis:7.4-alpine
  exit 0; restored port 58441; redis-cli DBSIZE returned 2
# Change only the temporary rollbackstore component redisHost to 127.0.0.1:58441.
dapr run --app-id eventstore-p1r-backup-3109-20260929 --dapr-http-port 45991 --resources-path /var/tmp/eventstore-p1r-backup-restore-3109-20260929/resources --log-level error
  health GET /v1.0/healthz/outbound returned 204; rollbackstore state.redis v1 loaded
# Run from rollback-probe/v370:
dotnet bin/Release/net10.0/Probe.dll read http://127.0.0.1:45991 p1r-backup-restore-31090-to-3701
  exit 0; event=58105c223b23b9c7a46b141fdeb66f7e46bc28d16bae53dfa9cab4d4fc5411f6 snapshot=0117b820bf04032327db0d27937b9316df076e0eb3581857775cc62c1db8da58
SIGINT to dapr run; docker stop hexalith-p1r-backup-restore-3109-20260929
  both exit 0; no matching container or dapr app remained
docker run --rm --user 0 -v /var/tmp/eventstore-p1r-backup-restore-3109-20260929/restore:/data redis:7.4-alpine rm -f /data/dump.rdb
  exit 0; test ! -e .../restore/dump.rdb exit 0
```

The Redis image owns the mounted RDB as UID `999`, so a one-off `redis:7.4-alpine` container running with `--user 0` removed only `/data/dump.rdb` from that throwaway directory. The probe `bin/` and `obj/` outputs and the temporary work directories were removed afterwards. No production backup, real tenant stream, writer quiescence, application redeployment, or domain replay was exercised.

For replay, use a fresh container name and a unique key prefix, get the dynamically assigned Redis port from `docker port <name> 6379/tcp`, and write a Dapr component file equivalent to:

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: rollbackstore
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: 127.0.0.1:<allocated-port>
  - name: redisPassword
    value: ""
```

## Compatibility boundary

These two runs prove a **narrow positive**. The published `3.70.1` Server package can deserialize the synthetic event and snapshot JSON that the published `3.109.0` Server package wrote through the Dapr state component. This holds after a sidecar restart and after a full copy of the disposable Redis RDB, with unchanged persisted bytes and the selected semantics within each run. The runs also prove that the shared constructors and record APIs compile at both versions.

They do **not** execute:

- either EventStore host or a Dapr aggregate actor;
- a command route, domain handler, replay, or migration;
- protection metadata, idempotency or trusted-effect state, or query routing;
- a real tenant stream.

The local Dapr runtime was `1.18.4`, not a separately qualified 3.70.1 production runtime. These smokes cannot establish a safe rolling downgrade or blanket persisted-state compatibility.

API rollback is narrower still:

- `CommandStatusQueryResponse.cs` exists at `v3.109.0` (and at `v3.108.1`), but `git cat-file -e v3.70.1:src/Hexalith.EventStore.Contracts/Commands/CommandStatusQueryResponse.cs` exited `128`. That newer client-facing response type is unavailable to an old-package consumer.
- The `3.109.0` packages ship trusted-effect contracts and `IAggregateActor.ProcessTrustedEffectAsync`. The `3.109.0` host container adds the `wrk-` identifier reservation and Dapr app-channel `APP_API_TOKEN` validation. None of these exist at `v3.70.1`, so any state or client behavior that depends on them has no old-version counterpart.
- The later `489e5d76` checkout adds `AggregateMetadata.RetainedFloor`, three `IAggregateActor` members, and further trusted-effect erasure state. Rollback from that checkout is a separate, untested coordinate.

Full actor/domain replay is a **material unresolved rollback blocker**. The [upgrade guide](../../../../../docs/guides/upgrade-path.md) warns that an old version can raise `UnknownEventException` on events written by a newer version. This probe's synthetic `RollbackProbe.Incremented` payload was deserialized as an envelope; no old domain handler registered that event type or rehydrated an aggregate.

The available tests do not provide a safe cross-version replay command. The AppHost, the LiveSidecar tests, and the integration tests all reference current source projects and shared local Dapr infrastructure. They would test `489e5d76` source, **not** the published `3.109.0` to `3.70.1` transition in throwaway state, so none of them was used as a substitute. An isolated, version-selectable two-release topology with a representative domain service and a known post-`3.109.0` stream is the exact missing prerequisite for an actor replay claim.

**Proposed owner decision:**

- **Option 1:** require an isolated exact-topology actor/domain replay and API check using representative post-`3.109.0` streams, plus an operational stop-writers/full-backup restore rehearsal, before qualifying operational `3.70.1` rollback.
- **Option 2:** explicitly accept these bounded storage-contract smokes, restrict rollback to a full backup restore taken before new writers, and document a risk disposition for newer events and APIs.

The disposable RDB copy here shows the mechanics only; it is not that operational rehearsal. No broader decision is inferred. This record does not change the accepted `3.106.0` P1R record or approve P0 Stage 6 or Story 6.1.
