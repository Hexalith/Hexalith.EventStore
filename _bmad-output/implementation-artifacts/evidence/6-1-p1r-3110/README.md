# EventStore 3.110.0 public package evidence

Observed 2026-10-01 UTC. Projects has already accepted EventStore `3.110.0` / `27279fe6431925a6ea046c3f89af61487185c7de` with Builds `4.29.1` / `21ce044ab465ccb2adab58b3d66e394ffbecf3c2`. **Status: archive proof recorded; `usable_as_prerequisite` remains false.** This record checks the 14 published `3.110.0` archives, their NuGet.org repository signatures, and a NuGet.org consumer replay. It does not close metadata rollback, actor/domain replay, operational rollback, or tag/checkout compatibility. The [3.109.0 evidence](../6-1-p1r-3109/README.md) directory is unchanged.

## Exact coordinates

| Coordinate | Value |
| --- | --- |
| Published package family | Version `3.110.0`, tag `v3.110.0`; `git rev-parse 'v3.110.0^{commit}'` resolved `27279fe6431925a6ea046c3f89af61487185c7de` |
| Release manifest | `tools/release-packages.json` at the tag: 14 IDs, SHA-256 `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`. `git diff --quiet v3.110.0 8096455e4f23f2912998e36738058b8e3d961be6 -- tools/release-packages.json` exited `0`. |
| EventStore source checkout | `8096455e4f23f2912998e36738058b8e3d961be6`, **28 commits after the tag**, with **44 changed `src` paths** (40 added, 4 modified, 0 deleted). It is a later source coordinate, not the source of the published `3.110.0` archives. Checkout compatibility stays open. |
| Release | [GitHub release `v3.110.0`](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.110.0), published `2026-09-29T18:07:59Z`, listing exactly the 14 manifest `.nupkg` assets |

Published NuGet.org and GitHub release archives are the only product artifacts considered here. The consumer below restores those published archives. No local EventStore build is substituted for a published package.

## Already accepted release validation bypass

The Projects owner packet already records this release's validation bypass, and Jérôme Piquot accepted that tuple at `2026-10-01T06:17:01Z` with the bypass included. This replay does not reopen that decision and does not treat the bypass as green CI proof.

- [Release Actions run 36608763986](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36608763986) published the release at tag commit `27279fe6431925a6ea046c3f89af61487185c7de`.
- The recorded release input is **`BYPASS_VALIDATION: true`**. The publication source proof is successful push [Commitlint run 36608682105](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36608682105), not the tag CI run.
- Independent tag CI [36608682063](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36608682063) succeeded at the same commit. Aspire and performance jobs were skipped. That success does not replace the release's selected Commitlint proof.
- Release-time Builds is `22a578b576a515d2af214fe81859447fffc97981`. The accepted P1R Builds revision remains `21ce044ab465ccb2adab58b3d66e394ffbecf3c2`.

## Published package hashes

For every manifest ID, the downloaded GitHub asset hash matched its release asset digest and size, and NuGet.org served the flat-container archive. The `.nuspec` metadata in both archives states version `3.110.0` and repository commit `27279fe6431925a6ea046c3f89af61487185c7de`. Every shared ZIP entry has identical bytes. The NuGet archive adds only `.signature.p7s`, which is why its whole-file hash differs from the release asset. `dotnet nuget verify --all` passed 14/14 with the NuGet.org repository signature certificate SHA-256 `1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D`. The EventStore-owned `tools/validate-release-packages.py` validated all 14 signed archives. **All 14 IDs are verified; none is unresolved.** URLs, sizes, registration timestamps, catalog entries, and per-package comparisons are in [public-packages.json](public-packages.json).

| Manifest package | NuGet.org signed archive SHA-256 | GitHub release asset SHA-256 |
| --- | --- | --- |
| `Hexalith.EventStore.Contracts` | `5c32eada89b3d6424f89cb01929e5e18d3240c463f1f1a6e32a5fb380bd4d6f2` | `b179defdab690890fc509b3d5e5fbc4d1bed15060c1007c95495ed37e783bc63` |
| `Hexalith.EventStore.Client` | `2085e60de82eefb00d0e76ea48438ecbcb14f610b9f65dc926feb1886af845b1` | `250595e1a53f74230b9d87be4e0082fe8758c8b3e54479358300f498ec47d76d` |
| `Hexalith.EventStore.Server` | `ca676768798b2dd69035eaef9dce92a8a55db2c77d44ce3b0dda216fb7ed776a` | `159082ee8ae0d3c944f7d323be1477012814b8c623103aa6975770cc26ba0476` |
| `Hexalith.EventStore.SignalR` | `214a6fd7049595dc3e3b68b00061eccef9946b5a910e05bdc125ad3b90af9910` | `79ce6baaec2ec4a2d3c5cc12daf3d818052c917a068eb863f04fd9cb79cd89f8` |
| `Hexalith.EventStore.Testing` | `7730c52d01d22eca6bc29ad37c6292f8061861cfaf1b57c149a4e01dd0e16256` | `f2c984dfad7e1be2498a4b1b2179aa1af877e97c53efd8a1036d684e4b665af8` |
| `Hexalith.EventStore.Testing.Integration` | `d3776b8fe6712fb832912e1d3d0e51e8a36e6dce5b4fbc4cf9f836ea6116a3b6` | `6b10f4de8276a57930a1bebd45c988c4ac258b7b834750cb0f5e4ec5639b5601` |
| `Hexalith.EventStore.Aspire` | `c62a68872288cbd9cdbd99dfa9c2056103f3207c13e68dadad7dc9b7fdb212a1` | `8006ddfed4241ca2b46a4746bf75ffb6e9638e03eee302fee7faf3bd75a5dc3b` |
| `Hexalith.EventStore.ServiceDefaults` | `7f23addbfca231eac9c2a6b3cc73d9db82c7674478e5f5c79f90a73d7abeb5c4` | `fc3322cb166e60222b2a4da0d0cd876dd8e0302aaee73b7f963ba864bdf391be` |
| `Hexalith.EventStore.DomainService` | `ead5dfabb5d4cfc90739fa414b3c2f8806d388c8237f74737a864d3b7b518e93` | `5bfc62c365c29972f44d08413a0c8906a3a327c1e500db8802087f59a01461ce` |
| `Hexalith.EventStore.RestApi.Generators` | `4ec12bf4d7828c7a7dad55f63df8a8ccc499e6f5e65512364f74020b3b748f41` | `4a55482269bbb043509fc4d032113c003f533057a53e903c83269a7705214450` |
| `Hexalith.EventStore.Gateway` | `53c00763bb77e3c607bde2dd5cc535f96311cf91b7308fb2aa1bba41413d65b5` | `8da61ebc9eca5ad1b6e611131d4237884d09d76a81b3d5c81818144f27a5e884` |
| `Hexalith.EventStore.Admin.Abstractions` | `f4d675aceeeae749ac1fc84b8f71e76082b42fb86059b393bc703acf477de4ee` | `4342faa0622d0466cbb683784b743fe1e173eecb199dc23132ce30cbd0c534d7` |
| `Hexalith.EventStore.Admin.Cli` | `2b7afecda28815ec578a464dfb2a9e11d14b470d61fc7e443b532ab64498ac99` | `19221a152480fa65d80d3760ecba57ab1c2ce3c364c5bde9b36645b8c936ef8d` |
| `Hexalith.EventStore.Admin.Server` | `8518ccefc95e77964bc14f3d28ae7afe947dafc6ef75deecb5a1e24f7ca20370` | `6ba3c0ed05573cceae0b462feb2988d480d840508b710bee1f8fff4d94f0d981` |

The NuGet semver1 registration leaf for `Hexalith.EventStore.Aspire` returned HTTP `404`, and its gzip semver2 leaf returned HTTP `200`. Its flat-container archive, signature, and consumer restore all passed. The other 13 IDs resolved through the semver1 leaf.

## Public archive replay

From this evidence directory, `python3 verify_public_packages.py` downloads the live GitHub release metadata and all 14 GitHub and NuGet.org archives. It checks:

- the tagged manifest, its hash against the current checkout, and the tag commit;
- the recorded and live SHA-256 digests and sizes;
- the `.nuspec` repository commits;
- byte equality of every shared ZIP entry, and that the NuGet repository signature is the only added entry;
- all 14 signatures with `dotnet nuget verify --all`;
- the EventStore release-package contract.

The replay uses a temporary download directory. On 2026-10-01 it exited `0` with `PASS: 14 public packages match recorded hashes, tagged source metadata, release assets, ZIP payloads, signatures, and manifest contract`.

## Independent consumption

The checked-in consumer disables inherited central package management for its exact package references. From [consumer/](consumer/), the .NET SDK `10.0.401` project used only `https://api.nuget.org/v3/index.json` and a fresh `NUGET_PACKAGES` directory. It restored all 13 library package references at exactly `3.110.0`, and the Release build finished with `0 Warning(s)` and `0 Error(s)`. `obj/project.assets.json` listed exactly the 13 EventStore IDs at `3.110.0` and no project libraries. Each restored `.nupkg.metadata` names NuGet.org as the source, and all 13 restored archives hash to the NuGet.org SHA-256 values above. The compiled smoke source names `IAsyncDomainProjectionHandler`, `IReadModelStore`, `IReadModelBatchStore`, `ReadModelWritePolicy`, `IDomainQueryHandler`, `IQueryCursorCodec`, and `QueryCursorScope`.

The 14th package, `Hexalith.EventStore.Admin.Cli`, installed as version `3.110.0` into `/var/tmp/eventstore-3110-replay/tool` using the same NuGet.org-only config. The installed `.store` archive hashes to `2b7afecda28815ec578a464dfb2a9e11d14b470d61fc7e443b532ab64498ac99`. `eventstore-admin --help` exited `0` and listed its commands, and `--version` printed `3.110.0+27279fe6431925a6ea046c3f89af61487185c7de`.

This establishes restore, compilation, and CLI startup. It does not exercise a live Dapr deployment, prove the runtime behavior of every package, or show that `3.70.1` can read streams written by `3.110.0`. The current checkout remains a separate coordinate.

Commands and results (the archive command runs from this evidence directory; the remaining commands run from `consumer/`):

```text
python3 verify_public_packages.py
  exit 0; PASS: 14 public packages match recorded hashes, tagged source metadata, release assets, ZIP payloads, signatures, and manifest contract
DOTNET_CLI_HOME=/var/tmp/eventstore-3110-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-3110-replay/packages dotnet restore Consumer.csproj --configfile NuGet.Config --disable-parallel --no-http-cache -p:NuGetAudit=false
  exit 0; Restored checked-in Consumer.csproj in 12.13 sec
DOTNET_CLI_HOME=/var/tmp/eventstore-3110-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-3110-replay/packages dotnet build Consumer.csproj --no-restore --configuration Release -warnaserror -p:NuGetAudit=false
  exit 0; Build succeeded; 0 Warning(s); 0 Error(s)
DOTNET_CLI_HOME=/var/tmp/eventstore-3110-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-3110-replay/tool-packages dotnet tool install Hexalith.EventStore.Admin.Cli --version 3.110.0 --tool-path /var/tmp/eventstore-3110-replay/tool --configfile NuGet.Config
  exit 0; tool version 3.110.0 installed from the fixture's sole NuGet.org source
/var/tmp/eventstore-3110-replay/tool/eventstore-admin --help
  exit 0; CLI usage and commands printed
/var/tmp/eventstore-3110-replay/tool/eventstore-admin --version
  3.110.0+27279fe6431925a6ea046c3f89af61487185c7de
```

`NuGetAudit=false` applied only to this independent smoke consumer, to isolate package resolution. It was not applied to the EventStore release run. The consumer `bin/` and `obj/` outputs were removed after the run; only the checked-in sources remain.

## Still open

- Metadata rollback from `3.110.0` to `3.70.1`, including the `RetainedFloor` shape, is not tested here.
- Actor/domain replay, mixed-version calls, API downgrade, and operational rollback are not tested here.
- Interchangeability between checkout `8096455e4f23f2912998e36738058b8e3d961be6` and the published `3.110.0` archives is not established. The shared release-manifest bytes do not prove runtime compatibility.
- `usable_as_prerequisite` stays `false`. G-6 attempt 16 and `tests/tools/test_p1r_candidate_evidence.py` remain on their existing lanes and are not this proof.
