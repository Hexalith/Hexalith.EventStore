# EventStore 3.109.0 public package and source evidence for pending Projects 6.1-P1R

Observed 2026-09-29 UTC. On 2026-09-29 Jérôme Piquot retargeted the pending Projects P1R candidate from `3.108.1` to the published EventStore `3.109.0` family and decided to qualify it despite its release validation bypass; the `3.70.1` rollback target is unchanged. **Status: candidate pending.** This record gathers owner evidence only. It does not accept the candidate, the release bypass, the later source checkout, or the rollback for any role. The Projects acceptance record for `3.106.0` and its rollback is unchanged, and the superseded [3.108.1 evidence](../6-1-p1r-3108/README.md) is kept as-is.

## Exact coordinates

| Coordinate | Value |
| --- | --- |
| Published package family | Version `3.109.0`, tag `v3.109.0`; `git rev-parse 'v3.109.0^{commit}'` resolved `818e28a8af421994e4f77e66327dc33a8c67ca5f` |
| Release manifest | `tools/release-packages.json` at the tag: 14 IDs, SHA-256 `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae` (the same bytes as at `v3.108.1`). `git diff --quiet v3.109.0 489e5d76253f68c6cf53c443aa8755ad059e621e -- tools/release-packages.json` exited `0`. |
| EventStore source checkout | `489e5d76253f68c6cf53c443aa8755ad059e621e` (the Projects gitlink at `1152c83`), **28 commits after the tag**. It is a later source coordinate, not the source of any published `3.109.0` archive. |
| Release-time Builds | `22a578b576a515d2af214fe81859447fffc97981`, executed by the release run below |
| Candidate Builds | The committed Projects Builds gitlink, a descendant of `85ca19bc99b137f0825d6a441199164653f537e9`. `git merge-base --is-ancestor 22a578b5… 85ca19bc…` exited `0` (184 commits later). The Projects owner packet names the exact SHA. |

Published NuGet.org and GitHub release archives are the only product artifacts considered here. Local restores and builds below consume those published archives; no local build of EventStore source is substituted for a published package.

## Release provenance and the validation bypass

- The [GitHub release](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.109.0) was published at `2026-09-26T11:31:17Z` and lists exactly the 14 manifest `.nupkg` assets, with no extra `.nupkg` asset.
- [Release Actions run 36238526310](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36238526310) ran `.github/workflows/release.yml` by `workflow_dispatch` at `head_sha=818e28a8af421994e4f77e66327dc33a8c67ca5f`, attempt `1`, conclusion `success`. Its exact-source proof job `108394631248` and publication job `108394643289` both succeeded.
- **The run used `BYPASS_VALIDATION`.** The job `108394631248` log shows the step environment `BYPASS_VALIDATION: true`. At the tag, `release.yml` maps `bypass-validation=true` to `source_ci_workflow=commitlint.yml`. The publication job log shows `SOURCE_CI_WORKFLOW: commitlint.yml` and `HEXALITH_RELEASE_SOURCE_CI_WORKFLOW: commitlint.yml`. The exact-source proof was therefore the successful push [Commitlint run 36237813667](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36237813667), **not** a successful CI run.
- **Tag CI failed.** [CI run 36237813612](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36237813612) (push, `head_sha=818e28a8…`) concluded `failure`. Job `ci / contracts` (`108392695564`) failed 3 of 2129 Contracts tests, with 2126 passing, 0 skipped, and exit code `2`. `build-and-test`, `semantic-release-governance`, and `tenants-source-mode` passed. `aspire-tests` and `performance-tests` were skipped. The failing tests are Story 4.15 OQ8 v5 evidence-governance tests, not library runtime tests:
  - `Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests.CheckedInRepositoryFullValidationPassesWithoutMutation`: `OQ8 evidence validation failed: Story 4.15 v5 reviewed packet, selector, or lifecycle validation failed`
  - `Hexalith.EventStore.Contracts.Tests.Packaging.Oq8V5CandidateTests.V5SubjectPacketRemainsInactiveAndRejectsChangedInputs`: `oq8-v5-packet: V5 source tree changed outside reviewed evidence and selector`
  - `Hexalith.EventStore.Contracts.Tests.Packaging.Oq8V5CandidateTests.V5DraftValidatorRejectsAuthorityAndSourceMutations`: `oq8-v5-packet: V5 source tree changed outside reviewed evidence and selector`
- The [release evidence artifact 10905232490](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36238526310/artifacts/10905232490) is `release-evidence-36238526310-1`, with GitHub digest and downloaded ZIP SHA-256 `cc9677ed56933e1adeef960fd3a3d3a9d3d8c5cf38e573a927e3d96c9188393b`. Its `3.109.0/preflight/publication-identity.json` has SHA-256 `b81cdb3aa37296b705e3a4191047480955789b286c3e446da083f941b43b49dd`. That file binds source `818e28a8…`, the 14 manifest IDs and manifest SHA-256 above, source proof `ci_workflow=commitlint.yml` run `36237813667`, and Builds `action_sha` and `workflow_sha` `22a578b576a515d2af214fe81859447fffc97981`. The same run's container OCI validation and `/alive` smoke results passed for `linux/amd64` and `linux/arm64`.
- The release executed Builds `22a578b5…`. The candidate Builds is a later descendant whose `Github/publish-containers` differs, and this release did not qualify that later revision with the tag.

**EventStore Owner disposition item (open):** the owner decided on 2026-09-29 to qualify `3.109.0` despite the bypass. That decision does not make the release CI green. The owner must still record a disposition of the bypassed CI proof and of the three failing tag Contracts OQ8 tests before any acceptance. Neither may be reported as green CI.

## Published package hashes

For every manifest ID, the downloaded GitHub asset hash matched its release asset digest and size, and NuGet.org served the flat-container archive. The `.nuspec` metadata in both archives states version `3.109.0` and repository commit `818e28a8af421994e4f77e66327dc33a8c67ca5f`. Every shared ZIP entry has identical bytes. The NuGet archive adds only `.signature.p7s`, which is why its whole-file hash differs from the release asset. `dotnet nuget verify --all` passed 14/14 with the NuGet.org repository signature certificate SHA-256 `1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D`. The EventStore-owned `tools/validate-release-packages.py` validated all 14 signed archives. **All 14 IDs are verified; none is unresolved.** URLs, sizes, registration timestamps, catalog entries, and per-package comparisons are in [public-packages.json](public-packages.json).

| Manifest package | NuGet.org signed archive SHA-256 | GitHub release asset SHA-256 |
| --- | --- | --- |
| `Hexalith.EventStore.Contracts` | `38f8ab2f116b477a2b8588810ce6ef31d8155e6c6c4f4cbd9d2d8bf76e8d0952` | `fcee6d53ff034c86730d5b8f2f9afb5aeb237da747dfbe898922e23001fbe699` |
| `Hexalith.EventStore.Client` | `0cf1b8142d2abc854625984da0ca91b74bec5948209e7dcd926e915e87ab96db` | `10708d5566e6770947df088d1b0c795f103a286c16337454142b00b58727d65b` |
| `Hexalith.EventStore.Server` | `c77537fc6a96cb56413652bdee76375551fe2d2ce17a902a954bf6e5eb1016b6` | `a49485911595dbbdd0b07470c2cbd251c1e78d7d635cd4fcc376dfb66813b17e` |
| `Hexalith.EventStore.SignalR` | `88f4ebd9c6f0e04eacd0b6d9ab9b8f47720548ca07146b6a800950751ff57049` | `585a4bf86a0ccdfdb83e5d61ef5d8ccc76639aafcee73a685a99641924a6afb7` |
| `Hexalith.EventStore.Testing` | `483f5fcaa876e810505f487f124e961864584bb7e3219d43bb4d6369eff5523a` | `37756e460f26cb3d3a3d8aefe17e76f48a50e32e49614cda136b7e0301cddbb9` |
| `Hexalith.EventStore.Testing.Integration` | `68d6a18086bd5202d007badca08375bb7ff58dc078e773493402f50859d9d3f2` | `3cedf30faf52f22b75b60fb17290c785198d145cf5c819279bdb6f9cc5348d21` |
| `Hexalith.EventStore.Aspire` | `6f75d1323c7c119139e94af824fe0a8b2d0924fcdfdea83b8a71bed04ecfe9e4` | `9538f723a7f8b75f30e8581e5ec30031dc4d144d0dbf8b2ec7e56a4ff689c980` |
| `Hexalith.EventStore.ServiceDefaults` | `ea6e05c09fd152d8b1431f4bc6703179e882506a4d8e604ab2d3fda57420b3a7` | `6e0da041c6b5c1a24a89d6478df61208bc760a1d6617cf6d0bfef324e947cdbd` |
| `Hexalith.EventStore.DomainService` | `d92894ded0cf9279c4b6510adbe4ed0dd3f239c2d4df14a8a18c669c158aa5ac` | `f04d7bf2be3e1275d6458dda8faa8b93078eac4cd47dbd60c1bb25492b39b6c8` |
| `Hexalith.EventStore.RestApi.Generators` | `0d76b503d353c4c518f83b0ae5ef0978509b6edd4195385a85267fe7a84fa193` | `f858dc95db76f9fb947bae2f5cd2efa868cfcc16f5a00d42c1b94d916bcc49e2` |
| `Hexalith.EventStore.Gateway` | `9c5cbd73b46cfb27b0fb996adf49797acb81e658152622c80e6a50e9c323244f` | `f0ce147d9cc23372e552477be5727c8bc1a42eb671d8573aefcc84e8f32d71d9` |
| `Hexalith.EventStore.Admin.Abstractions` | `ed626c1f89dc4da469037acacedad8f5956b4d6830f7b7315752f36d80227824` | `ede56f064590092034081d3ef8d02e818ee366d0caff1b25e48848c601eec827` |
| `Hexalith.EventStore.Admin.Cli` | `67a6613eea5c664e537c647ee841aeadbdda827349b218ebf98afad63e9ee2cf` | `eb857929b90faf9c73323b48cf45f540ba90d19f3329ade9fd377f3159f6b6e7` |
| `Hexalith.EventStore.Admin.Server` | `9d68787cb700f2de270a3dfd05c5404fbd2132f4b2341abb990245bdb3f534f5` | `6f0574b3b2786a879aa88754bb5ab9f296a1d4ff34e15e7c0359477388ec5551` |

The NuGet semver1 registration leaf for `Hexalith.EventStore.Aspire` returned HTTP `404`, and its gzip semver2 leaf returned HTTP `200`. Its flat-container archive, signature, and consumer restore all passed. The other 13 IDs resolved through the semver1 leaf.

## Public archive replay

From this evidence directory, `python3 verify_public_packages.py` downloads the live GitHub release metadata and all 14 GitHub and NuGet.org archives. It checks:

- the tagged manifest, its hash against the current checkout, and the tag commit;
- the recorded and live SHA-256 digests and sizes;
- the `.nuspec` repository commits;
- byte equality of every shared ZIP entry, and that the NuGet repository signature is the only added entry;
- all 14 signatures with `dotnet nuget verify --all`;
- the EventStore release-package contract.

The replay uses a temporary download directory. On 2026-09-29 it exited `0` with `PASS: 14 public packages match recorded hashes, tagged source metadata, release assets, ZIP payloads, signatures, and manifest contract`. The replay is repeatable while the public archives remain available.

## Independent consumption

The checked-in consumer disables inherited central package management for its exact package references. From [consumer/](consumer/), the .NET SDK `10.0.401` project used only `https://api.nuget.org/v3/index.json` and a fresh `NUGET_PACKAGES` directory. It restored all 13 library package references at exactly `3.109.0`, and the Release build finished with `0 Warning(s)` and `0 Error(s)`. `obj/project.assets.json` listed exactly the 13 EventStore IDs at `3.109.0` and no project libraries. Each restored `.nupkg.metadata` names NuGet.org as the source, and all 13 restored archives hash to the NuGet.org SHA-256 values above. The compiled smoke source names `IAsyncDomainProjectionHandler`, `IReadModelStore`, `IReadModelBatchStore`, `ReadModelWritePolicy`, `IDomainQueryHandler`, `IQueryCursorCodec`, and `QueryCursorScope`. The 14th package, `Hexalith.EventStore.Admin.Cli`, installed as version `3.109.0` into a separate tool path from an archive hashing to `67a6613e…`. `eventstore-admin --help` exited `0` and listed its commands, and `--version` printed `3.109.0+818e28a8af421994e4f77e66327dc33a8c67ca5f`.

This establishes restore, compilation, and CLI startup. It does not exercise a live Dapr deployment or prove the runtime behavior of every package. The source association rests on the exact-source release run, its preflight artifact, and the embedded package metadata. No bit-identical rebuild of any package from the tag or independent build attestation was performed.

Commands and results (the archive command runs from this evidence directory; the remaining commands run from `consumer/`):

```text
python3 verify_public_packages.py
  exit 0; PASS: 14 public packages match recorded hashes, tagged source metadata, release assets, ZIP payloads, signatures, and manifest contract
DOTNET_CLI_HOME=/var/tmp/eventstore-3109-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-3109-replay/packages dotnet restore Consumer.csproj --configfile NuGet.Config --disable-parallel --no-http-cache -p:NuGetAudit=false
  exit 0; Restored checked-in Consumer.csproj in 18.54 sec
DOTNET_CLI_HOME=/var/tmp/eventstore-3109-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-3109-replay/packages dotnet build Consumer.csproj --no-restore --configuration Release -warnaserror -p:NuGetAudit=false
  exit 0; Build succeeded; 0 Warning(s); 0 Error(s)
DOTNET_CLI_HOME=/var/tmp/eventstore-3109-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-3109-replay/tool-packages dotnet tool install Hexalith.EventStore.Admin.Cli --version 3.109.0 --tool-path /var/tmp/eventstore-3109-replay/tool --configfile NuGet.Config
  exit 0; tool version 3.109.0 installed from the fixture's sole NuGet.org source
/var/tmp/eventstore-3109-replay/tool/eventstore-admin --help
  exit 0; CLI usage and commands printed
```

`NuGetAudit=false` applied only to this independent smoke consumer, to isolate package resolution. It was not applied to the EventStore release run. The consumer `bin/` and `obj/` outputs were removed after the run; only the checked-in sources remain.

## What 3.109.0 changes relative to the superseded 3.108.1 candidate

`v3.108.1` is an ancestor of `v3.109.0`, 46 commits earlier. `git diff --name-status v3.108.1 v3.109.0 -- src` reports 57 paths (40 added, 17 modified). Several behaviors that the 3.108.1 packet recorded as later-checkout-only are **inside the published 3.109.0 release**.

In the NuGet packages:

- trusted-effect contracts under `Contracts/Effects` and `IAggregateActor.ProcessTrustedEffectAsync`;
- the changed `CommandStatusQueryResponse`, which adds `EventCount` and `TenantId`.

In the `src/Hexalith.EventStore` host shipped as the `registry.hexalith.com/eventstore:3.109.0` container:

- the `wrk-` identifier reservation in `CommandsController`;
- `DaprAppChannelTokenValidator` (`APP_API_TOKEN`).

The seven scoped consumer API files above have identical Git blobs at `v3.106.0`, `v3.108.1`, `v3.109.0`, and `489e5d76`.

## Tag-to-checkout drift (scoped compatibility diff)

`git log --oneline v3.109.0..489e5d76` lists 28 commits, and the history is linear from the tag. `git diff --name-status v3.109.0 489e5d76 -- src` reports **22 paths: 9 added, 13 modified, 0 deleted**. The release manifest is unchanged. The source changes are all in `Hexalith.EventStore.Server` and `Hexalith.EventStore.Testing`:

- **`AggregateMetadata.RetainedFloor`**: `src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs` changes from blob `8c552052…` (identical from `v3.70.1` through `v3.109.0`) to `30a032ab…`. The persisted record gains an optional positional `long RetainedFloor = 1`, and `EventPersister` now writes it. Metadata written by the later checkout therefore carries a field that no published package, including `3.109.0` and `3.70.1`, has written. Old readers are expected to ignore it under default System.Text.Json settings, but that was **not tested**.
- **Breaking interface changes for implementers and callers**:
  - `IAggregateActor` adds `FenceTrustedEffectsAsync`, `GetRetainedFloorAsync`, and `EraseTrustedEffectEvidenceAsync`.
  - `IIdempotencyTenantLifecycleActor` adds `CompleteTrustedEffectAsync`.
  - `ITrustedEffectRetentionGate` adds `CompleteAsync`.
  - `ITrustedEffectJointRetentionPolicy.EraseTenantAsync` gains a required `TrustedEffectAggregateErasure[]` parameter.
  - `IdempotencyTenantLifecycleRecord` gains persisted `TrustedEffects` and `PendingTrustedEffectIds` members.
  - Source implementers, including `FakeAggregateActor` in `Hexalith.EventStore.Testing`, must change.
- New trusted-effect erasure, deletion-fence, erasure-progress, and evidence-index actors and commands are added.

**Untested and not assumed compatible:**

- mixed-version actor calls across the added `IAggregateActor` members;
- reading `RetainedFloor`-bearing metadata with `3.109.0` or `3.70.1`;
- the tenant-erasure and deletion-fence behavior;
- the added idempotency lifecycle state;
- any source/package interchangeability between `489e5d76` and the `3.109.0` archives.

The checkout is a different, later product state, and no `3.109.0` archive contains its post-tag changes. Only the EventStore Owner can decide whether and under what validation the later checkout may be treated as compatible.

## Rollback evidence

The [isolated rollback rehearsal](rollback-probe/README.md) reports successful `3.109.0` to `3.70.1` event and snapshot reads in two cases: after a sidecar restart, and after a disposable Redis RDB backup/restore. The storage record sources (`Server/Events/EventEnvelope.cs`, `Server/Events/SnapshotRecord.cs`, and `Contracts/Events/EventEnvelope.cs`) have identical blobs at `v3.70.1`, `v3.106.0`, `v3.108.1`, `v3.109.0`, and `489e5d76`. `AggregateMetadata` is identical from `v3.70.1` through `v3.109.0` and changes only after the tag. Actor/domain replay and API downgrade remain material open gaps. The `3.70.1` signed packages embed source commit `650faf053a98ed1c03c048b8e0d2e4d281b095cf`, the parent of tag `f13f9925fdca53efa2ab8c90d396ab106f91bb9c`; those are distinct coordinates.

## Decisions requested (none inferred)

1. **EventStore Owner:** disposition the `BYPASS_VALIDATION` release proof (Commitlint instead of CI) and the three failing tag Contracts OQ8 governance tests. State whether the later `489e5d76` checkout may be treated as compatible, and under what validation, given the `RetainedFloor` and interface changes. Resolve the exact-topology rollback replay and API-downgrade boundary.
2. **Builds Owner:** accept or reject the candidate Builds gitlink with a tag that was released by Builds `22a578b5…`.
3. **Test Architect:** review the public provenance, the consumption, the bounded rollback evidence, and the open disposition items, then record an independent decision.

No broader approval is inferred here.
