# Story 8.2 Payload-Protection Contracts And Golden Vectors

## Disposition

Story 8.2 implementation and technical verification completed on 2026-09-13
against EventStore baseline
`dfc0ac557c43363159b55bffb4d40feceab1f787` and approved replacement packet
`AR-20260913-01`. The exact Story 8.1 normative digest recomputes as
`de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.

This evidence does not authorize Story 8.3. Story 8.2's technical
contract/API/vector review is closed, but Story 8.3 remains predecessor-gated
until explicit successor authorization is recorded by the required named
owners. Stories 8.4-8.11 and G5 remain blocked by their own predecessor and
evidence gates.

## Implemented Boundary

- Added the approved public policy, canonical-path, erasure-state, occurrence,
  protected-snapshot, write-result, and persistence-completion contracts under
  `src/Hexalith.EventStore.Contracts/Security`, one documented public type per
  file.
- Added exactly six occurrence/completion-aware default members to
  `IEventPayloadProtectionService`. Existing required members remain abstract.
  Legacy defaults preserve cancellation and non-v2 delegation, never fabricate
  completion identity, reject legacy v2 claims, return the existing typed
  unsupported outcome for v2 reads, and fault explicitly for unsupported
  completion work.
- Added owner fixtures for V001/V002 G-001 and V003 NIST AES-256-GCM Count 0,
  a complete V001-V138 ownership partition, independently implemented Node.js
  and Python verifiers, and .NET contract/vector tests.
- Kept `tools/release-packages.json`, the existing Server no-op provider, and
  the Testing unreadable-provider fake byte-identical to the baseline.

No payload-protection engine, backend, Server persistence path, package/project,
topology, Parties source, external resource, or persisted data was introduced
or changed.

## Content Binding

| Artifact | SHA-256 |
| --- | --- |
| Story 8.1 normative range | `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e` |
| `tools/release-packages.json` | `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae` |
| Sorted complete Contracts/Security per-file hash stream | `a01dc5576702f08dc0a95caaa8a4158e457c2f1d85dd4337fc145d126a02fa2e` |
| Scoped implementation diff from the EventStore baseline | `ff0fd2ddebd68f4b8f944174667289e35680ba3af104adc580dd57ff037aaf9` |
| `IEventPayloadProtectionService.cs` | `bf642ba897581dae1870c524e10ee8c97824e4c4c1675ddcd0cbfe14f8f6781e` |
| `PayloadProtectionV2ContractTests.cs` | `02915db53d0a375aae067261961a5b9860b7dba833413e1bf39fe3f9c9b96abf` |
| Contracts test project | `1f996cc51b85147d641e0faedfd867bf379965b6d751643d95a1035eed474067` |
| Fixture manifest file | `cde3940952789d58f4b415c1fe36202f21cf93a534bd3f11741af3975f7570e9` |

The complete fixture manifest is
`tests/Hexalith.EventStore.Contracts.Tests/Security/Fixtures/PayloadProtectionV2/manifest.json`.
It binds these independently checked inputs:

| Input | SHA-256 |
| --- | --- |
| `g-001.json` | `a821f36321bd5a020b35f89b1e3d18e7dc3070e3dbf694db5c0413847a012610` |
| `nist-gcm-256-count0.json` | `528a81472dc6bd4db653bf01dc52a16d5e092c7bcde2ef622f42878ba794a319` |
| `vector-ownership.json` | `a3886eac22cf3b77c210ae2bb166f237980bc5e8cf1e9cbaf8c569aa6b4dc087` |
| Node.js verifier | `5b0892d8ed6fa3dbe29159b0a6777636206350ec43b0f33b22b2a706c040b06a` |
| Python verifier | `f6a5003726478c445635653c8c0bd4c73a92001cdd65c4bdacf217d094851cf9` |
| Python dependency lock | `70c867286e0e8fae9c36dff5d479b0a017c1b2892e03382cbf772608714cce74` |

All three implementations produced AAD SHA-256
`cb83c07f5aee433bdc0a36e70d80e9841063598a1a72a3a72044996245046e7b`,
envelope SHA-256
`247381efe70c9c4844af998ddec864b41665461b466bda418beb8edc0e5406be`,
and wrapper SHA-256
`35388a17c7d950f775378c47b6263d75088be358fbe6372a71f2bec55a3356c3`.

The package-only proof used version `999.0.0-ci-test`. Both archive validators
accepted exactly 14 packages. Their observed archive hashes were:

| Package | SHA-256 |
| --- | --- |
| `Hexalith.EventStore.Admin.Abstractions` | `6e486c82f95e59b9dca692992d78117febd56d5fd4cc081adb249f5065b267b8` |
| `Hexalith.EventStore.Admin.Cli` | `aba41f0303e4c65b1519d763a64dd7fb8f3ebf38c40c8a8efc8c39c8d1dec8e0` |
| `Hexalith.EventStore.Admin.Server` | `af830072cd83e6a6a2aa590995d84efff865b6a368d5bdc041aaac5305da2b80` |
| `Hexalith.EventStore.Aspire` | `83f055f30448bcae1600d3c281c74d0e063683e279f69f61431ba17886bc213e` |
| `Hexalith.EventStore.Client` | `82f474c94b3650e5c1c91326e1d584ddffbf18663c5af8e17321581a6c06deab` |
| `Hexalith.EventStore.Contracts` | `5b7700fdf4461451338ad34ace5df04d7a1729945208ae0cbee0473fdaa91ed7` |
| `Hexalith.EventStore.DomainService` | `67678233e10fd187595bc837a8575215c183b60af8688db71477be3434899e39` |
| `Hexalith.EventStore.Gateway` | `c945b19796cd0b7c78e36f3cd3f5ca39fc261329fb839b6306819fb8740d34ce` |
| `Hexalith.EventStore.RestApi.Generators` | `5aba66211819659e4907c8f3ae1cfc101617fa8c158bf02f137444f28d7ee58a` |
| `Hexalith.EventStore.Server` | `93f1f1deeddff870e84bf930bbb19c41ad1363a619e5ce1e7f75b5497035117d` |
| `Hexalith.EventStore.ServiceDefaults` | `e923ba197970b495e194fc40a27d921f43708478b1f5e5c9da0c1f2be52212ef` |
| `Hexalith.EventStore.SignalR` | `490d50116393790dc4013ec7c96d434429b8a4c91312fac9892ca66ca542ce5c` |
| `Hexalith.EventStore.Testing` | `4957e696562fd27caca74fe89ab2ece07056e48efe31347de2c62d18826c96bd` |
| `Hexalith.EventStore.Testing.Integration` | `2c455e1424aaa1b1af799a1044651327b677250b5a0efb6c354e3adb539ece37` |

## Verification Results

| Command | Result |
| --- | --- |
| Story 8.1 section 1.2 exact digest recomputation | PASS; replacement digest reproduced with unique markers, LF-only bytes, and no BOM |
| `node scripts/payload-protection/verify-golden-vectors.mjs` | PASS; Node v26.4.0/OpenSSL 3.5.7; V001-V003 executed; 135 future vectors gated |
| `python3 scripts/payload-protection/verify-golden-vectors.py` | PASS; Python 3.14.4/OpenSSL 3.5.5/cryptography 46.0.5; identical outputs |
| Focused xUnit v3 Story 8.2 class through the built test executable | PASS; 31/31 |
| `DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 dotnet test --project tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release --no-restore --minimum-expected-tests 2018` | PASS; rebuilt tested content, then passed 2,018/2,018 in 2 minutes 56 seconds |
| `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 -nodeReuse:false` | PASS; zero warnings and zero errors in 55.34 seconds |
| `python3 scripts/pack-release-packages.py /tmp/eventstore-story-8-2-reviewed-packages.i6Vzqa 0.0.0-ci-test` with MSBuild server reuse disabled | PASS; exact 14-package manifest packed at normalized version `999.0.0-ci-test` |
| `python3 scripts/validate-nuget-packages.py /tmp/eventstore-story-8-2-reviewed-packages.i6Vzqa` | PASS; exactly 14 archives |
| `python3 tools/validate-release-packages.py /tmp/eventstore-story-8-2-reviewed-packages.i6Vzqa 999.0.0-ci-test` | PASS; exactly 14 archives |
| `python3 scripts/validate-consumer-package-references.py /tmp/eventstore-story-8-2-reviewed-packages.i6Vzqa` with MSBuild server reuse disabled | PASS; 13 isolated library consumers and one isolated tool consumer |
| `git diff --check` | PASS; no whitespace errors |

The API-shape tests verify the exact six additive virtual default-interface
members and preservation of the four legacy required members. The compatibility
tests compile an implementation containing only those legacy required members,
exercise lossless delegation and all-default cancellation precedence, verify
occurrence context delivery to an override, reject mismatched context before
provider invocation, isolate every v2 recognition signal, enforce write-result
completion invariants, canonical-decode and decrypt V002, pin the named NIST
control, independently mutate every G-001 AAD field plus ciphertext and tag,
and scan call-scoped `ToString()` output for field-specific payload/key canaries.
The fixtures, authority, verifiers, and dependency lock are copied into and read
from test output. The full solution build also compiles the unchanged production
no-op and Testing fake providers.

## Limitations And Residual Gates

- Story 8.2 owns and executes only V001-V003. The ownership fixture maps every
  V001-V138 identifier exactly once; the other 135 are deliberately marked
  `predecessor-gated` for their owning Stories 8.3-8.11 and are not claimed as
  executed evidence.
- The NIST control is an independent comparison to the named CAVP vector, not a
  CAVP validation certificate.
- Package archive hashes describe this isolated verification run. Release
  signing, SBOM, provenance, vulnerability/license evidence, and the future
  14-to-16 package transition belong to Story 8.8/G5.
- No mock, interface, fixture, package build, or successful status here claims
  production payload protection, provider custody, Server persistence wiring,
  rollout readiness, erasure completion, rollback proof, or G5 closure.
- The shared build host exhibited MSBuild node contention with default server
  reuse. The clean package proof and supported Microsoft Testing Platform run
  therefore set `DOTNET_CLI_USE_MSBUILD_SERVER=0` and
  `MSBUILDDISABLENODEREUSE=1`; all recorded guarded runs completed successfully.
