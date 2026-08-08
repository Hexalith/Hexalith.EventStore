# Evidence Environment

Captured on `2026-08-08`.

| Component | Captured value |
| --- | --- |
| OS | Linux, Ubuntu 26.04 LTS, WSL2 kernel `6.6.87.2-microsoft-standard-WSL2`, x86_64 |
| .NET SDK | `10.0.302` |
| Test runtime | 64-bit .NET `10.0.10` |
| xUnit.net | `3.2.2+728c1dce01` |
| DAPR CLI | `1.18.0` |
| DAPR runtime | `1.18.1` |
| DAPR .NET packages | `1.18.5` |
| Redis image | `redis:6`; image ID/repo digest `sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf` |
| DAPR placement image | `daprio/dapr:1.18.1`; image ID/repo digest `sha256:b42eeb03c4300938226b7a5d7a15db5513e69e1d55570967c290d670c7612df2` |
| DAPR scheduler image | `daprio/dapr:1.18.1`; image ID/repo digest `sha256:b42eeb03c4300938226b7a5d7a15db5513e69e1d55570967c290d670c7612df2` |
| Zipkin image | `openzipkin/zipkin:latest`; image ID/repo digest `sha256:bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb` |
| Hexalith.Builds source | `824d7ef100455423aabbcd399c8364074000b2e0` (`v4.23.0-39-g824d7ef`) |

## Effective actor-state component

`append-durability-race.json.providerProfile` commits the exact generated `statestore.yaml` content supplied through `daprd --resources-path`, SHA-256 `58a5745c497cee3c164e0b8e84c50672affd1f8ff459d61f71938fdfd342f4fa`, the randomized scoped Dapr application id, and the production allocator type exercised. The component is Dapr `state.redis` `v1`, Redis host `localhost:6379`, empty password, and `actorStateStore: true`. The fixture creates this scoped file in a temporary directory, so the committed runtime capture—not the deleted temporary path—is the reproduction authority.

## Clean-environment prerequisites

- Docker must be available with Redis port `6379`, Dapr placement port `50005`, and scheduler port `50006` free.
- Install Dapr CLI `1.18.0` and .NET SDK `10.0.302`.
- Install Python 3, `jq`, `rg`, and GNU `find`/`sed`/`sha256sum` for receipt redaction and integrity validation.
- Pin the self-hosted runtime and images before running the commands in `commands.md`:

```bash
docker pull redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf
docker tag redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf redis:6
docker pull daprio/dapr@sha256:b42eeb03c4300938226b7a5d7a15db5513e69e1d55570967c290d670c7612df2
docker tag daprio/dapr@sha256:b42eeb03c4300938226b7a5d7a15db5513e69e1d55570967c290d670c7612df2 daprio/dapr:1.18.1
docker pull openzipkin/zipkin@sha256:bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb
docker tag openzipkin/zipkin@sha256:bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb openzipkin/zipkin:latest
dapr init --runtime-version 1.18.1
dapr --version
```

Re-run from the repository root:

```bash
dotnet --version
dapr --version
docker ps --format '{{.Names}}|{{.Image}}|{{.Status}}' | sort
docker inspect dapr_redis dapr_placement dapr_scheduler dapr_zipkin --format '{{.Name}}|{{.Image}}|{{.Config.Image}}'
docker image inspect redis:6 daprio/dapr:1.18.1 openzipkin/zipkin --format '{{.RepoTags}}|{{.Id}}|{{json .RepoDigests}}'
git submodule status -- references/Hexalith.Builds
rg -n 'Dapr.Client|Dapr.AspNetCore|Dapr.Actors|xunit.v3' references/Hexalith.Builds/Props/Directory.Packages.props
```
