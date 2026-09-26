# EventStore 3.108.1 public package and source evidence for pending Projects 6.1-P1R

Observed 2026-09-26 UTC; public archive and checked-in consumer replayed 2026-09-27 UTC. **Evidence only; EventStore Owner and Test Architect decisions remain pending.** The Projects acceptance record for 3.106.0 and its 3.70.1 rollback are unchanged.

## Exact release provenance

- Local `git rev-parse 'v3.108.1^{commit}'` resolved `b15ad59abca82d5980ef92a510c2379e05f4d46f`. `tools/release-packages.json` at that tag has 14 IDs and SHA-256 `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`. The [GitHub release](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.108.1) lists exactly those 14 `.nupkg` assets and published at `2026-09-24T16:22:33Z`.
- [Release Actions run 36025721331](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36025721331) has `head_sha=b15ad59abca82d5980ef92a510c2379e05f4d46f`, `workflow_dispatch`, and `success`. Its exact-source proof job and publication job both succeeded. The publication job includes successful source revalidation, restore, build, OIDC exchange, and Semantic Release steps. [Release evidence artifact 10820060935](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/36025721331/artifacts/10820060935) (downloaded ZIP SHA-256 `25499af3b7e37a2e71ce00ef9e0bbeffcfc20a81386d0f587ea3949dec3265b8`) contains `3.108.1/preflight/publication-identity.json`, binding the same source SHA, the 14 manifest IDs and hash, successful main push CI run `36017213712`, and release Builds execution SHA `22a578b576a515d2af214fe81859447fffc97981`. That release execution SHA is historical publication provenance. The proposed Builds P1R revision is `2326f983bad14d5398ee55bf2bdf6c86b63c39ea`; this exact release run did not qualify that newer revision with the tag, so its catalog/runner/schema tuple and tool packages require separate Builds Owner and Test Architect decisions.
- For every manifest ID, the downloaded GitHub asset hash matched its release asset digest; the [NuGet.org](https://api.nuget.org/v3/index.json) flat-container package was available; both archives' `.nuspec` metadata state version `3.108.1` and repository commit `b15ad59abca82d5980ef92a510c2379e05f4d46f`; and every shared ZIP entry has identical bytes. The NuGet archive adds only `.signature.p7s`, explaining why its whole-file hash differs from the GitHub asset. `dotnet nuget verify --all` passed 14/14, and the EventStore-owned `tools/validate-release-packages.py` validated all 14 signed archives under their canonical manifest filenames. All had NuGet.org repository signature certificate SHA-256 `1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D`. The signed NuGet hashes and asset hashes follow; URLs, sizes, registration publication timestamps, manifest projects, and per-package comparisons are in [public-packages.json](public-packages.json).

| Manifest package | NuGet.org signed archive SHA-256 | GitHub release asset SHA-256 |
| --- | --- | --- |
| `Hexalith.EventStore.Contracts` | `b11162ed66f8dd807d434de652a3b59a49da804b6a2610b8cb8472c1a60ef0c2` | `0812c2f1c4f7a76739ebe840dfbf6effb1c125e1544fce1deccb8fc7ba5280dc` |
| `Hexalith.EventStore.Client` | `1c050a0fec73979e2514313f6853601079dd916c6e713ea47be0e3dbffb345a1` | `6c545dbd3b20f572b63f7164f68182f338ddf5cb37fb1ad19dd86bd1078a9378` |
| `Hexalith.EventStore.Server` | `5b27e6512a8a1e2cafb774d5723f86bbe9835e748facfa26c1b0736af92a947a` | `b659a2c1741780366c3780227b9aec28ee3e1d0eee0cf71fb74bdea47eabc7fb` |
| `Hexalith.EventStore.SignalR` | `3ab60d871f678ed3e0e244a19218a50c82c12b571887191a59a82ad9c2e33c17` | `396cafbcd95a89fe66ec5e31d9f7946b8dc782c42bbfc63bcd49621bbeb89068` |
| `Hexalith.EventStore.Testing` | `c8beff3377b8645d859d1a429f3e16e2e0f192d61b09bea6385bea3669438d5b` | `92749f4de62653ff9b7aa8509881760e3e41a1cdabdc51f0fd38fcfc7d6c6cd1` |
| `Hexalith.EventStore.Testing.Integration` | `f33a155a434608a29226fdae89e2e166bebf3881fd27743adb99f6e294ab401f` | `b57bb575b923edef13701e3d62b3da3fd7de754a6f91354305fbea92716e7e43` |
| `Hexalith.EventStore.Aspire` | `13b6fe721afbd5559c9d6b1f5ca65eabd9abfc43fc3057097f472ddc259df5fd` | `886f81ab48be9e9077c4d3cdd01be5c58517a63ee9f98c37f8fc5b00fa3343aa` |
| `Hexalith.EventStore.ServiceDefaults` | `4aacb4274d8b4a43ca87c0d024cd1e2ab88f3514df1a9e6895c8cc37e872cf60` | `1a547af0063f3d0d2c987f64de876480585c2f7c7550d54eded0534c4065bd0e` |
| `Hexalith.EventStore.DomainService` | `dcab73f820288185043a694965d45327248a958005d451d458e6b55156d1c19f` | `aa5212dd4a43e03832fd4cfe1dc4d26ba2990cf9c1ea9fac627b26c0edc4924a` |
| `Hexalith.EventStore.RestApi.Generators` | `ed8d1332860b257fe2696236520c7cb0a5094f716f322e394e3baadeaa55667d` | `99223b44e0de72ae836eef1162c81a91b71613696f555827b4f88b1fa430c7fc` |
| `Hexalith.EventStore.Gateway` | `6af114803f75a245fe5688d0e9683cd476903c40188f81da9fe15ad1e01706e4` | `8dde0da32b83928ca8c35abbd37177e7dbb768b6cc68446199fff9c1a717a203` |
| `Hexalith.EventStore.Admin.Abstractions` | `0111ed8940cb3869e6f61e672b6720047160359ccaa973111ed4365a5dcd34dc` | `ffc06da622978d47939cad05a0d627066c4dbe3766b60a14ea31acf738ff3edf` |
| `Hexalith.EventStore.Admin.Cli` | `49c0113b467303745fdc0d0e5e2737abe5ac36abb61a03d4bad362022ed646df` | `fb076d28202b6d3181ed9b6b970dd292216a863f9ec90881c67d159f76d664c4` |
| `Hexalith.EventStore.Admin.Server` | `dc2d2149bdd2b3035fb8155ccd0ed3894e8dcada7b1ed24b726f4a9efb6c1d15` | `3b5454a456f8eeba56704227e1f49cca16cadcb8908d5c9d89b006d922614260` |

## Public archive replay

From this evidence directory, `python3 verify_public_packages.py` downloads the live GitHub release metadata plus all 14 GitHub and NuGet.org archives. It checks the exact tagged manifest and commit, recorded and live SHA-256 digests and sizes, `.nuspec` repository commits, byte equality of every shared ZIP entry, the sole added NuGet repository signature, all 14 signatures with `dotnet nuget verify --all`, and the EventStore release-package contract. The replay uses a temporary download directory and exited `0` with `PASS: 14 public packages match recorded hashes, tagged source metadata, release assets, ZIP payloads, signatures, and manifest contract` on 2026-09-27. The checked-in verifier and hashes make this result repeatable while the public archives remain available.

## Independent consumption

The checked-in consumer disables inherited central package management for its exact package references. From [consumer/](consumer/), the .NET SDK `10.0.401` project used only `https://api.nuget.org/v3/index.json` and a fresh `NUGET_PACKAGES` directory. It restored all 13 library package references at exactly `3.108.1` and built Release with `0 Warning(s)` and `0 Error(s)`. The compiled smoke source directly names `IAsyncDomainProjectionHandler`, `IReadModelStore`, `IReadModelBatchStore`, `ReadModelWritePolicy`, `IDomainQueryHandler`, `IQueryCursorCodec`, and `QueryCursorScope`. The 14th package, `Hexalith.EventStore.Admin.Cli`, installed as version `3.108.1` into a separate tool path; `eventstore-admin --help` exited `0` and listed its commands. `obj/project.assets.json` resolved exactly 13 EventStore IDs at `3.108.1` for the library consumer. This establishes restore, compilation, and CLI startup; it does not exercise a live Dapr deployment or prove runtime behavior of every package. The source association is supported by the successful exact-source release run, its preflight artifact, and embedded package metadata; no bit-identical rebuild of every package from the tag or independent build attestation was performed.

Commands and results (archive command from this evidence directory; remaining commands from `consumer/`):

```text
python3 verify_public_packages.py
  exit 0; PASS: 14 public packages match recorded hashes, tagged source metadata, release assets, ZIP payloads, signatures, and manifest contract
DOTNET_CLI_HOME=/var/tmp/eventstore-31081-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-31081-replay/packages dotnet restore Consumer.csproj --configfile NuGet.Config --disable-parallel --no-http-cache -p:NuGetAudit=false
  exit 0; Restored checked-in Consumer.csproj in 25.39 sec
DOTNET_CLI_HOME=/var/tmp/eventstore-31081-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-31081-replay/packages dotnet build Consumer.csproj --no-restore --configuration Release -warnaserror -p:NuGetAudit=false
  exit 0; Build succeeded; 0 Warning(s); 0 Error(s)
DOTNET_CLI_HOME=/var/tmp/eventstore-31081-replay/dotnet-home NUGET_PACKAGES=/var/tmp/eventstore-31081-replay/tool-packages dotnet tool install Hexalith.EventStore.Admin.Cli --version 3.108.1 --tool-path /var/tmp/eventstore-31081-replay/tool --configfile NuGet.Config
  exit 0; tool version 3.108.1 installed from fixture's sole NuGet.org source
/var/tmp/eventstore-31081-replay/tool/eventstore-admin --help
  exit 0; CLI usage and commands printed
```

`NuGetAudit=false` was confined to the independent smoke consumer to isolate package resolution; it was not applied to the EventStore release run. The NuGet semver1 and semver2 registration leaf for `Hexalith.EventStore.Aspire` returned HTTP `404`; its gzip semver2 leaf returned HTTP `200`, and its flat-container archive, signature, and consumer restore all passed.

## Newer checkout compatibility disposition needed

At inspection, `main` was `a1dd24f8dff17da87f2e5e1be3e381ea5bfdf37e`, **53 commits after the tag**. `git diff --name-status v3.108.1 HEAD -- src` reported 67 source paths (48 added, 19 modified), while `git diff --quiet v3.108.1 HEAD -- tools/release-packages.json` exited `0`. The seven named consumer API files compiled above have the same Git blob at `v3.106.0`, `v3.108.1`, and this checkout. The source checkout is a different, later product state; no 3.108.1 archive contains its post-tag changes.

The later checkout adds trusted-effect request/receipt/erasure contracts and processing; adds abstract methods to `IAggregateActor` (`ProcessTrustedEffectAsync`, `GetRetainedFloorAsync`, `EraseTrustedEffectEvidenceAsync`), which require source implementers to change; adds `EventCount` and `TenantId` status properties; reserves `wrk-` command/message IDs at the HTTP controller; and requires the configured `APP_API_TOKEN` header for allow-listed Dapr internal callers outside Development. These are observable API and behavior changes, so source/package interchangeability and rollback behavior need an EventStore Owner compatibility decision. A seven-file identity check and the package smoke do not settle those effects.

**Decision requested:** EventStore Owner accepts or rejects the exact tagged `3.108.1` package family as the P1R candidate and separately states whether, when, and under what validation the later checkout may be treated as compatible. Test Architect reviews the public provenance and consumption evidence and records an independent decision. No approval is inferred here.
