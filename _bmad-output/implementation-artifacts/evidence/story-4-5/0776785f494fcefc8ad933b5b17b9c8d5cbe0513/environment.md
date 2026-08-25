# Evidence Environment

Captured on `2026-08-26` (re-capture and re-seal; supersedes the `2026-08-08` capture).

| Component | Captured value |
| --- | --- |
| OS | Linux, Ubuntu 26.04 LTS, WSL2 kernel `6.6.87.2-microsoft-standard-WSL2`, x86_64 |
| .NET SDK | `10.0.302` |
| Test runtime | 64-bit .NET `10.0.10` |
| xUnit.net | `3.2.2+728c1dce01` |
| DAPR CLI | `1.18.0` |
| DAPR runtime (`daprd --version`, the exact binary the fixture launches) | `1.18.1` |
| DAPR .NET packages | `1.18.5` |
| Redis image | `docker.io/redis:6` (`redis_version:6.2.21`); image ID `sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf` |
| Redis persistence | `appendonly no`; `save 3600 1 300 100 60 10000` |
| DAPR placement image | `daprio/dapr:1.18.2`; image ID `sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59` |
| DAPR scheduler image | `daprio/dapr:1.18.2`; image ID `sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59` |
| Zipkin image | `openzipkin/zipkin:latest`; image ID `sha256:bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb` |
| Hexalith.Builds source | `22a578b576a515d2af214fe81859447fffc97981` (`v4.24.0-37-g22a578b`) |

## Control-plane image versus runtime version

`dapr init` on this host installed placement and scheduler container images tagged `1.18.2` while
the `daprd` binary under `~/.dapr/bin` is `1.18.1`. **The fixture launches that binary directly**,
so the provider profile this packet claims — DAPR runtime `1.18.1`, `state.redis`, `redis:6` — is
the one the capture actually exercised. The `1.18.2` control-plane images are recorded here so the
divergence is disclosed rather than left to be re-discovered as an apparent mislabel.

The runtime version in `append-durability-race.json.providerProfile.daprRuntimeObserved` is read at
capture time from `daprd --version`; the Redis image reference and image ID come from
`docker inspect dapr_redis`; the persistence settings come from `redis-cli config get`. None of
these are source literals.

## Control-plane host ports

`dapr init` publishes the placement and scheduler container ports (`50005`/`50006`) on host ports
that vary by CLI version and platform. On this host they are published as `6050` and `6060`. The
fixture probes both candidate pairs and uses whichever answers, so a capture can be reproduced on
either layout.

## Effective actor-state component

`append-durability-race.json.providerProfile` commits two forms of the generated `statestore.yaml`
supplied through `daprd --resources-path`:

- `stateStoreComponentCanonicalYaml` — the component with its per-run `scopes:` list removed.
  `stateStoreComponentSha256` is the SHA-256 of **this** form, so the digest binds the provider
  configuration identity and is stable across runs. It is
  `06284f20919e20ca08439ada6811d1d6612a1ffd76e11cded9d9fa5767ae52d4`.
- `stateStoreComponentYaml` — the verbatim file, including the randomized scoped Dapr application
  id. This form is retained for exact reproduction but is deliberately not the hashed identity,
  because its digest changes on every run.

The component is Dapr `state.redis` `v1`, Redis host `localhost:6379`, empty password, and
`actorStateStore: true`. The fixture creates this scoped file in a temporary directory, so the
committed runtime capture — not the deleted temporary path — is the reproduction authority.

## Clean-environment prerequisites

- Docker must be available with Redis port `6379` free, and the Dapr placement and scheduler
  services reachable on either `50005`/`50006` or `6050`/`6060`.
- Install Dapr CLI `1.18.0` and .NET SDK `10.0.302`.
- Install Python 3, `jq`, `rg` (with PCRE2), and GNU `find`/`sed`/`sha256sum` for receipt redaction
  and integrity validation.
- Pin the self-hosted runtime and images before running the commands in `commands.md`:

```bash
docker pull redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf
docker tag redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf redis:6
docker pull openzipkin/zipkin@sha256:bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb
docker tag openzipkin/zipkin@sha256:bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb openzipkin/zipkin:latest
dapr init --runtime-version 1.18.1
dapr --version
~/.dapr/bin/daprd --version
```

Re-run from the repository root:

```bash
dotnet --version
dapr --version
~/.dapr/bin/daprd --version
docker ps --format '{{.Names}}|{{.Image}}|{{.Status}}' | sort
docker inspect dapr_redis dapr_placement dapr_scheduler dapr_zipkin --format '{{.Name}}|{{.Config.Image}}|{{.Image}}'
docker exec dapr_redis redis-cli config get appendonly
docker exec dapr_redis redis-cli config get save
git submodule status -- references/Hexalith.Builds
rg -n 'Dapr.Client|Dapr.AspNetCore|Dapr.Actors|xunit.v3' references/Hexalith.Builds/Props/Directory.Packages.props
```

## Undeclared profiles present in the full-suite receipt

`live-sidecar-test-results.json` and `post-mutation-live-sidecar-test-results.json` include the
`Oq8Postgresql` collection, which starts a PostgreSQL actor-state profile owned by Story 4.14. That
profile is **not** part of the Story 4.5 provider claim; every Story 4.5 observation comes from the
Dapr `1.18.1` / `state.redis` / `redis:6` profile recorded above. The PostgreSQL image and version
are documented in the Story 4.14 packet, not here.
