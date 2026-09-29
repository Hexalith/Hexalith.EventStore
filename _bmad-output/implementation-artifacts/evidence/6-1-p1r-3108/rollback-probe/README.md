# Bounded 3.108.1 to 3.70.1 rollback rehearsal

Observed 2026-09-27. Jérôme Piquot's direction selected EventStore `3.108.1` for P1R and `3.70.1` as the rollback target. This is an isolated, executable storage-contract rehearsal, **not** a production deployment or approval of the complete P1R prerequisite chain.

## Exact package and checkout coordinates

| Coordinate | Result |
| --- | --- |
| Forward release | `v3.108.1` tag `b15ad59abca82d5980ef92a510c2379e05f4d46f`; published `Hexalith.EventStore.Server` and transitive `Contracts` `3.108.1` |
| Rollback release | `v3.70.1` tag `f13f9925fdca53efa2ab8c90d396ab106f91bb9c`; published `Server` and transitive `Contracts` `3.70.1` |
| Rollback archive build source | Both `3.70.1` `.nuspec` repository elements name `650faf053a98ed1c03c048b8e0d2e4d281b095cf`, the direct parent of tag `f13f9925...`. The tag commit changes only `CHANGELOG.md`; `git diff --quiet 650faf... f13f... -- src tools/release-packages.json` exited `0`. The package source commit and tag commit remain distinct coordinates. |
| Later source checkout | `main`/`HEAD=1cc6b44b4a71950fbd8f92469bebad3b0c9c05c5`, 55 commits after `v3.108.1`; earlier observed `a1dd24f8dff17da87f2e5e1be3e381ea5bfdf37e` is its ancestor. Neither later checkout is a source coordinate for the published `3.108.1` archives. |

The downloaded, NuGet.org-signed archive hashes were `Server 3.108.1=5b27e6512a8a1e2cafb774d5723f86bbe9835e748facfa26c1b0736af92a947a`, `Contracts 3.108.1=b11162ed66f8dd807d434de652a3b59a49da804b6a2610b8cb8472c1a60ef0c2`, `Server 3.70.1=a9b1f8e8b83b069421951f386572042f67ef31a58f024310b6ff9ad794567f74`, and `Contracts 3.70.1=ee6cb34319615cf2612b75c6298aad3c1862ee07ce3cb78fa39deee1c62b7c1a`. `dotnet nuget verify --all` over those four archive paths exited `0`.

## Executed isolated state transition

The same [Program.cs](Program.cs) was compiled against each published `Server` package version by [v3108/Probe.csproj](v3108/Probe.csproj) and [v370/Probe.csproj](v370/Probe.csproj), with the sole source in [NuGet.Config](NuGet.Config). Both fresh restores passed (`9.95` and `8.26` seconds), and both Release builds passed with **0 warnings and 0 errors**. The probe writes and reads actual `Hexalith.EventStore.Server.Events.EventEnvelope` and `SnapshotRecord` instances using the actor-state fallback `JsonSerializerOptions.Web` shape. Both record source files have identical Git blobs at `v3.70.1`, `v3.108.1`, `a1dd24f8`, and current `HEAD`.

The disposable backing store was `redis:7.4-alpine` image `sha256:ff02b58f971e7d7d156a1267e283fcbbeee91773b6aa36c49dac28ecfe28eadf`, container `hexalith-p1r-rollback-20260927`, bound only to `127.0.0.1:50087`. Dapr CLI was `1.18.2`, runtime `1.18.4`; a unique app ID `eventstore-p1r-rollback-20260927` loaded only an isolated `state.redis` v1 component named `rollbackstore`. The component's `redisHost` was `127.0.0.1:50087`; its HTTP port was `45605`.

The `3.108.1` binary wrote synthetic tenant `throwaway-tenant`, sequence `1`, payload `{"count":7}`, one extension, and a snapshot with count `7`. It read the persisted values back. After stopping and restarting the Dapr sidecar while retaining the same disposable Redis container, the `3.70.1` binary read and validated both records. The exact runs were:

```text
# Run from rollback-probe/v3108, with a fresh version-specific NUGET_PACKAGES directory:
DOTNET_CLI_HOME=/var/tmp/eventstore-p1r-rollback-20260927/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-p1r-rollback-20260927/packages-3108 dotnet restore Probe.csproj --configfile ../NuGet.Config --disable-parallel --no-http-cache -p:NuGetAudit=false
  exit 0; Restored Probe.csproj in 9.95 sec
DOTNET_CLI_HOME=/var/tmp/eventstore-p1r-rollback-20260927/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-p1r-rollback-20260927/packages-3108 dotnet build Probe.csproj --no-restore --configuration Release -warnaserror -p:NuGetAudit=false
  exit 0; Build succeeded, 0 Warning(s), 0 Error(s)

# Run from rollback-probe/v370, with a separate fresh package directory:
DOTNET_CLI_HOME=/var/tmp/eventstore-p1r-rollback-20260927/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-p1r-rollback-20260927/packages-370 dotnet restore Probe.csproj --configfile ../NuGet.Config --disable-parallel --no-http-cache -p:NuGetAudit=false
  exit 0; Restored Probe.csproj in 8.26 sec
DOTNET_CLI_HOME=/var/tmp/eventstore-p1r-rollback-20260927/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-p1r-rollback-20260927/packages-370 dotnet build Probe.csproj --no-restore --configuration Release -warnaserror -p:NuGetAudit=false
  exit 0; Build succeeded, 0 Warning(s), 0 Error(s)

docker run -d --rm --name hexalith-p1r-rollback-20260927 -p 127.0.0.1::6379 redis:7.4-alpine
  exit 0; container 57c25628943ed184720815012586ae075a510c487d32f6ac65b37cc7e23a03f5; allocated host port 50087
dapr run --app-id eventstore-p1r-rollback-20260927 --dapr-http-port 45605 --resources-path /var/tmp/eventstore-p1r-rollback-20260927/resources --log-level error
  sidecar health GET /v1.0/healthz/outbound returned 204; metadata listed rollbackstore state.redis v1
dotnet bin/Release/net10.0/Probe.dll write http://127.0.0.1:45605 p1r-31081-to-3701
  exit 0; write: event_sha256=201473320385113642a595e76e9d6676df4e362dd873c281d4dabc90f5266655 snapshot_sha256=45f1f5ddfbe39eaa7ab0c887273fdf8250c932d9883d75ef26425d98d5b8db2b sequence=1 snapshot_count=7
Ctrl-C to the first dapr run process
  exit 0; Exited Dapr successfully
dapr run --app-id eventstore-p1r-rollback-20260927 --dapr-http-port 45605 --resources-path /var/tmp/eventstore-p1r-rollback-20260927/resources --log-level error
  sidecar health GET /v1.0/healthz/outbound returned 204
# Run from rollback-probe/v370:
dotnet bin/Release/net10.0/Probe.dll read http://127.0.0.1:45605 p1r-31081-to-3701
  exit 0; read: event_sha256=201473320385113642a595e76e9d6676df4e362dd873c281d4dabc90f5266655 snapshot_sha256=45f1f5ddfbe39eaa7ab0c887273fdf8250c932d9883d75ef26425d98d5b8db2b sequence=1 snapshot_count=7
```

Direct inspection of the disposable Redis store after the rollback read found exactly two keys, `eventstore-p1r-rollback-20260927||p1r-31081-to-3701-{event,snapshot}`, both Redis hashes with `version=1`. Their `data` field SHA-256 values exactly matched the Dapr read hashes above; both had `tenantId=throwaway-tenant` and `sequenceNumber=1`. Thus the checked values survived the sidecar restart and old-package read without mutation. The sidecar was stopped (`Ctrl-C`, exit `0`), `docker stop hexalith-p1r-rollback-20260927` exited `0`, the `--rm` container disappeared, and `dapr list` showed no matching app ID. No non-throwaway state was touched.

## Executed disposable backup and restore

A separate run checked the full-copy path instead of merely restarting the sidecar. The `3.108.1` binary wrote the same synthetic types to a fresh Redis container (`hexalith-p1r-backup-source-20260927`, ID `51d1e99b3f9daa3b2430fb6f9adf213e4537b977aab900a3a5f2d136801086cd`, loopback port `49228`) through a fresh Dapr sidecar (`app-id=eventstore-p1r-backup-20260927`, HTTP port `45981`, `rollbackstore` pointing to that port). The unique key prefix was `p1r-backup-restore-31081-to-3701`. Its output was `event_sha256=d79473cda233f208e1513d475d58925286ac3a86bd12c2bf843112837ff03e31 snapshot_sha256=45f1f5ddfbe39eaa7ab0c887273fdf8250c932d9883d75ef26425d98d5b8db2b sequence=1 snapshot_count=7`, exit `0`. The event-byte hash differs from the first independent run; only within-run equality across the copy and old reader is claimed.

After stopping that sidecar, `redis-cli SAVE` returned `OK` and exit `0`; the source Redis contained exactly two keys. The copied RDB `/var/tmp/eventstore-p1r-backup-restore-20260927/restore/dump.rdb` had SHA-256 `a649f1002fb82a4769c27d416f1b3033732f154a55ca2883ba1dd3348a5f275c`. The source container was stopped. A fresh restored container (`hexalith-p1r-backup-restore-20260927`, ID `f29bc6e963f55be68bc977a5f04c0354ec259125e84b65e8cd21874eae5b3013`) mounted only that throwaway RDB at `/data`, allocated loopback port `51339`, and reported `DBSIZE=2` before Dapr started. The component's `redisHost` was then changed to `127.0.0.1:51339` and the same Dapr app ID/HTTP port restarted. The `3.70.1` binary read both records with exit `0` and exactly the writer's event and snapshot hashes, sequence and count. Direct inspection of both restored Redis `data` fields matched those hashes, both `version` fields remained `1`, and tenant/sequence values matched. The restored Redis still contained exactly two keys.

The operational command sequence was:

```text
docker run -d --rm --name hexalith-p1r-backup-source-20260927 -p 127.0.0.1::6379 redis:7.4-alpine
  exit 0; source port 49228
dapr run --app-id eventstore-p1r-backup-20260927 --dapr-http-port 45981 --resources-path /var/tmp/eventstore-p1r-backup-restore-20260927/resources --log-level error
  health GET /v1.0/healthz/outbound returned 204; rollbackstore loaded
# Run from rollback-probe/v3108:
dotnet bin/Release/net10.0/Probe.dll write http://127.0.0.1:45981 p1r-backup-restore-31081-to-3701
  exit 0; event=d79473cda233f208e1513d475d58925286ac3a86bd12c2bf843112837ff03e31 snapshot=45f1f5ddfbe39eaa7ab0c887273fdf8250c932d9883d75ef26425d98d5b8db2b
Ctrl-C to dapr run
  exit 0
docker exec hexalith-p1r-backup-source-20260927 redis-cli SAVE
  exit 0; OK
docker cp hexalith-p1r-backup-source-20260927:/data/dump.rdb /var/tmp/eventstore-p1r-backup-restore-20260927/restore/dump.rdb
  exit 0; sha256=a649f1002fb82a4769c27d416f1b3033732f154a55ca2883ba1dd3348a5f275c
docker stop hexalith-p1r-backup-source-20260927
  exit 0
docker run -d --rm --name hexalith-p1r-backup-restore-20260927 -p 127.0.0.1::6379 -v /var/tmp/eventstore-p1r-backup-restore-20260927/restore:/data redis:7.4-alpine
  exit 0; restored port 51339; redis-cli DBSIZE returned 2
# Change only the temporary rollbackstore component redisHost to 127.0.0.1:51339.
dapr run --app-id eventstore-p1r-backup-20260927 --dapr-http-port 45981 --resources-path /var/tmp/eventstore-p1r-backup-restore-20260927/resources --log-level error
  health GET /v1.0/healthz/outbound returned 204; rollbackstore loaded
# Run from rollback-probe/v370:
dotnet bin/Release/net10.0/Probe.dll read http://127.0.0.1:45981 p1r-backup-restore-31081-to-3701
  exit 0; event=d79473cda233f208e1513d475d58925286ac3a86bd12c2bf843112837ff03e31 snapshot=45f1f5ddfbe39eaa7ab0c887273fdf8250c932d9883d75ef26425d98d5b8db2b
Ctrl-C to dapr run; docker stop hexalith-p1r-backup-restore-20260927
  both exit 0; no matching container or dapr app remained
```

The disposable RDB was deleted from its own temporary restore directory; deletion from the host initially returned `Permission denied` because the Redis image owns the mounted file as UID `999`, so a one-off `redis:7.4-alpine` container with `--user 0` removed only `/data/dump.rdb` from that mounted throwaway directory. `test ! -e` then passed. No production backup, real tenant stream, writer quiescence, application redeployment, or domain replay was exercised.

For replay, use a fresh container name and unique key prefix, obtain the dynamically assigned Redis port from `docker port <name> 6379/tcp`, and write a Dapr component file equivalent to:

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

These two runs prove a **narrow positive**: the published `3.70.1` Server package can deserialize the synthetic event and snapshot JSON written through the Dapr state component by the published `3.108.1` Server package, after a sidecar restart and after a full copy of the disposable Redis RDB, with unchanged persisted bytes and selected semantics within each run. They also prove the shared constructors and record APIs compile at both versions. They do **not** execute either EventStore host, Dapr aggregate actor, command route, domain handler, replay, migration, protection metadata, idempotency state, query routing, or a real tenant stream. The local Dapr runtime was `1.18.4`, not a separately qualified 3.70.1 production runtime. Its unrelated local scheduler/placement connections emitted errors during the first sidecar run; the state component was healthy and no actor path was exercised. These smokes cannot establish safe rolling downgrade or blanket persisted-state compatibility.

API rollback is narrower still: `CommandStatusQueryResponse.cs` exists at `v3.108.1` but `git cat-file -e v3.70.1:src/Hexalith.EventStore.Contracts/Commands/CommandStatusQueryResponse.cs` exited `128` (`path ... not in v3.70.1`). That newer client-facing response type is unavailable to an old-package consumer. The current `1cc6b44b` checkout has 68 changed source paths after the tag (49 added, 19 modified), including four added abstract `IAggregateActor` methods (`ProcessTrustedEffectAsync`, `FenceTrustedEffectsAsync`, `GetRetainedFloorAsync`, `EraseTrustedEffectEvidenceAsync`) and post-tag trusted-effect/retention behavior. The older `a1dd24f8` checkout already had three of these methods; current `HEAD` adds the deletion fence. Both checked storage-record source files remain identical across the rollback tag, forward tag, and later source coordinates, but source/API and runtime compatibility of the later checkout remains a separate owner disposition. Neither later checkout replaces the tagged package coordinate.

Full actor/domain replay is a **material unresolved rollback blocker**. The [upgrade guide](../../../../../docs/guides/upgrade-path.md) explicitly warns that an old version can raise `UnknownEventException` on events written by a newer version. This probe's synthetic `RollbackProbe.Incremented` payload was deserialized as an envelope; no old domain handler registered that event type or rehydrated an aggregate. Newer idempotency, projection, protected-payload, and command-status state also remain untested. A successful generic JSON read cannot justify a rolling downgrade or conclude that the old host can process every post-upgrade stream.

The available tests do not furnish a safe cross-version replay command. [AppHost.csproj](../../../../../src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj) unconditionally references the current `src/Hexalith.EventStore` project and sample projects; [LiveSidecar.Tests.csproj](../../../../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj) unconditionally references current EventStore/Gateway/Server/Testing source projects; and [IntegrationTests.csproj](../../../../../tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj) likewise references current source and the AppHost. The live [DaprTestContainerFixture](../../../../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs) requires pre-existing `dapr init` infrastructure and uses `localhost:6379`. Thus the documented `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` or `aspire run --apphost src/Hexalith.EventStore.AppHost/...` would test current `1cc6b44b` source against shared local infrastructure, **not** the published `3.108.1` to `3.70.1` transition in throwaway state. Both public versioned container manifests resolve (`docker manifest inspect registry.hexalith.com/eventstore:3.108.1` and `:3.70.1`, exit `0`), but there is no checked-in isolated, version-selectable two-release topology with a representative domain service/handler and stream. Such a harness and known representative post-`3.108.1` stream are exact missing prerequisites for an actor replay claim. Neither of those unsafe or non-equivalent source-suite commands was used as a substitute.

**Proposed owner decision:** require an isolated exact-topology actor/domain replay and API check using representative post-`3.108.1` streams, plus an operational stop-writers/full-backup restore rehearsal, before qualifying operational `3.70.1` rollback; or explicitly accept these bounded storage-contract smokes and restrict rollback to a full backup restore taken before new writers, with a documented risk disposition for newer events. The disposable RDB copy here is evidence of mechanics only, not that operational rehearsal. No broader decision is inferred here. This record does not change the accepted `3.106.0` P1R record or approve P0 Stage 6/Story 6.1.
