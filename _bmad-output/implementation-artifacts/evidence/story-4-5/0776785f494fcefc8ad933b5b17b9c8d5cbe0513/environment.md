# Evidence Environment

Captured `2026-08-25` UTC (the loop-4 re-capture; the run's own `session.armedAtUtc` is the
authority). Every date in this packet is UTC, so a reader comparing a document date against a
capture timestamp never sees a spurious one-day offset from a local-time clock. This capture
supersedes the `2026-08-08` and earlier `2026-08-26`-labelled captures entirely.

| Component | Captured value |
| --- | --- |
| OS | Linux, Ubuntu 26.04 LTS, WSL2 kernel `6.6.87.2-microsoft-standard-WSL2`, x86_64 |
| .NET SDK | `10.0.302` |
| Test runtime | 64-bit .NET `10.0.10` |
| xUnit.net | `3.2.2+728c1dce01` |
| DAPR CLI | `1.18.0` |
| DAPR runtime (`daprd --version`, the exact binary the fixture launches) | `1.18.1` |
| DAPR .NET packages | `1.18.5` |
| Redis image | `docker.io/redis:6` (`redis_version:6.2.21`) |
| Redis image ID | `sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf` |
| Redis repository digest (pullable) | `redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf` |
| Redis persistence | `appendonly no`; `save 3600 1 300 100 60 10000` |
| DAPR placement image | `docker.io/daprio/dapr:1.18.2` |
| DAPR placement image ID | `sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59` |
| DAPR scheduler image | `docker.io/daprio/dapr:1.18.2` |
| DAPR scheduler image ID | `sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59` |
| DAPR control-plane repository digest (pullable) | `daprio/dapr@sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59` |
| Zipkin image | `openzipkin/zipkin:latest`; image ID `sha256:bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb` |
| Hexalith.Builds source | `22a578b576a515d2af214fe81859447fffc97981` (`v4.24.0-37-g22a578b`) |

## Image ID versus repository digest

`docker inspect <container> --format '{{.Image}}'` returns the **local image ID** — the digest of
the image configuration on this host. It is not a registry repository digest and generally does not
work in `docker pull name@sha256:...`. The two coincide for these images on this host, which is
exactly why the distinction is easy to get wrong; the table records both, and the pin block below
uses only the repository-digest form. The capture records them separately as `*ImageIdObserved`
and `redisRepoDigestsObserved`.

## Control-plane image versus runtime version

`dapr init` on this host installed placement and scheduler container images tagged `1.18.2` while
the `daprd` binary under `~/.dapr/bin` is `1.18.1`. **The fixture launches that binary directly**,
so the provider profile this packet claims — DAPR runtime `1.18.1`, `state.redis`, `redis:6` — is
the one the capture actually exercised. The `1.18.2` control-plane images are recorded, digest and
all, so the divergence is pinned and disclosed rather than left to be re-discovered as an apparent
mislabel.

The runtime version in `append-durability-race.json.providerProfile.daprRuntimeObserved` is read at
capture time from `daprd --version`; the Redis, placement and scheduler image references and IDs
come from `docker inspect`; the persistence settings come from `redis-cli config get`. None of
these are source literals, and `validate-evidence.py` asserts the values recorded here match the
values the capture observed.

## Control-plane host ports

`dapr init` publishes the placement and scheduler container ports (`50005`/`50006`) on host ports
that vary by CLI version and platform. On this host they are published as `6050` and `6060`. The
Story 4.5 fixture probes both candidate pairs in order and uses whichever answers.

**The reviewed capture exercised the new `6050`/`6060` branch.** The focused, mutation and restored
focused runs were taken with no port forwarder running, so `ResolveReachablePortAsync` fell through
`50005`/`50006` to `6050`/`6060`. The capture records the probe order and the resolved values under
`providerProfile.controlPlanePorts`, and `validate-evidence.py` asserts
`placementResolved == 6050` — a value the old hardcoded `OperatingSystem.IsWindows()` predicate
could never have produced on this platform. This closes the loop-4 finding that the advertised port
fix was untested by the capture that sealed it.

`Oq8PostgresqlFixture` — owned by Story 4.14 and hash-bound by the sealed Story 4.14 and 4.15
packets (`tools/validate-oq8-platform-evidence.py`, invoked from `integration.yml`) — hard-codes
`50005`/`50006` and must not be edited by this story. The two **full-suite** receipts were
therefore captured with the two ports forwarded so that collection could start:

```bash
# any TCP forwarder works; socat shown for brevity
socat TCP-LISTEN:50005,bind=127.0.0.1,fork,reuseaddr TCP:127.0.0.1:6050 &
socat TCP-LISTEN:50006,bind=127.0.0.1,fork,reuseaddr TCP:127.0.0.1:6060 &
```

No Story 4.5 observation depends on the forwarder: the two capturing tests pass on either layout,
and the committed captures were taken with it down.

## Effective actor-state component

`append-durability-race.json.providerProfile` commits three forms of the generated `statestore.yaml`
supplied through `daprd --resources-path`:

- `stateStoreComponentCanonicalYaml` — the component with its per-run terminal `scopes:` block
  removed. This is the form the digest is taken over, so `stateStoreComponentSha256` binds the
  provider configuration identity and is stable across runs. It is
  `06284f20919e20ca08439ada6811d1d6612a1ffd76e11cded9d9fa5767ae52d4`.
- `stateStoreComponentHashedYaml` — the text actually hashed by the run. In a clean run it equals
  the canonical form; the `state-store-component-identity` perturbation makes it the raw scoped
  document instead, which is how the packet proves the digest is not a per-run nonce.
- `stateStoreComponentYaml` — the verbatim file, including the randomized scoped Dapr application
  id. Retained for exact reproduction but deliberately not the hashed identity.

`DaprTestContainerFixture.StripTerminalScopes` and the validator's re-derivation
(`component.split("\nscopes:", 1)[0]`) must agree. `StripTerminalScopes` throws if the generator
ever emits a top-level key after `scopes:`, and `StateStoreComponentCanonicalizationTests` pins the
agreement — including that divergent shape.

The component is Dapr `state.redis` `v1`, Redis host `localhost:6379`, empty password, and
`actorStateStore: true`. The fixture creates this scoped file in a temporary directory, so the
committed runtime capture — not the deleted temporary path — is the reproduction authority.

## Clean-environment prerequisites

- Docker must be available with Redis port `6379` free, and the Dapr placement and scheduler
  services reachable on either `50005`/`50006` or `6050`/`6060`.
- Install Dapr CLI `1.18.0` and .NET SDK `10.0.302`.
- Install Python 3, `jq`, `rg` (with PCRE2), and GNU `find`/`sed`/`sha256sum` for receipt redaction
  and integrity validation.
- Pin the self-hosted runtime and images before running the commands in `commands.md`. These use
  repository digests, which is the form `docker pull` accepts:

```bash
docker pull redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf
docker tag redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf redis:6
docker pull daprio/dapr@sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59
docker tag daprio/dapr@sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59 daprio/dapr:1.18.2
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
docker image inspect redis:6 daprio/dapr:1.18.2 openzipkin/zipkin --format '{{.RepoTags}}|{{.Id}}|{{json .RepoDigests}}'
docker exec dapr_redis redis-cli config get appendonly
docker exec dapr_redis redis-cli config get save
git submodule status -- references/Hexalith.Builds
rg -n 'Dapr.Client|Dapr.AspNetCore|Dapr.Actors|xunit.v3' references/Hexalith.Builds/Props/Directory.Packages.props
```

## Undeclared profiles present in the full-suite receipts

`live-sidecar-test-results.json` and `post-mutation-live-sidecar-test-results.json` include the
`Oq8Postgresql` collection, which starts a PostgreSQL actor-state profile owned by Story 4.14. That
profile is **not** part of the Story 4.5 provider claim; every Story 4.5 observation comes from the
Dapr `1.18.1` / `state.redis` / `redis:6` profile recorded above. The PostgreSQL image and version
are documented in the Story 4.14 packet, not here.
