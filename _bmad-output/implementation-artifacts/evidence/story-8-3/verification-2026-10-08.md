# Story 8.3 Pre-review Verification — 2026-10-08

This records the pre-review execution of the Story 8.3 checkout. Subsequent review corrections and their exact final-source verification are recorded separately in `verification-2026-10-08-postreview.md`. The existing
core implementation satisfies the focused checks below without a production,
test, dependency, or workflow change in this resumed implementation run. The
implementation handoff adds this record and its saved receipts only. Independent
review and the owner's final disposition remain separate.

This record supersedes the **current** 291-case execution/count and source/test/CI
binding claims in `verification.md`, together with its current full-solution and
normal shared-pack PASS claims. It does not rewrite or invalidate that document's
historical executions. `preflight.md`, `verification.md`, AR-20260914-01, and
AR-20260914-02 retain their original bytes and identities. No new owner approval
is inferred from successful checks.

The sole implementation authority was the complete Story 8.3 spec and all three
frontmatter context files: `epic-8-context.md`, the shared payload-protection
authority, and `requirements-amendment-approval.md`. The normative digest remains
`de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.
The AR-20260914-02 constructibility interpretations remain exact, including the
3,617-byte constructible AAD maximum, 3,873-byte schema maximum, and 4,096-byte
defensive cap. Durable metadata/Unprotected pairing remains Server-owned.
This handoff authorizes no work in later stories; the G5/Parties and Story 8.7
blocks remain in force.

## Content and frozen inventory

The saved [binding receipt](verification-2026-10-08/binding.json) records every
source-file digest, frozen comparison, result counter, package digest, and saved
log/TRX digest. Hash streams are SHA-256 over path-sorted `sha256sum` output,
including repository-relative paths; generated `bin/` and `obj/` are excluded.
The implementation checkout HEAD was
`9542d3c9f48bf9ce1c57f2ef68904703eaba56cc`.

| Inventory | Files | SHA-256 stream |
| --- | ---: | --- |
| Core source/project | 35 | `f6cf266c83d1d79ca23b5e92547aff5e908d29bea43e93ae36dac3c0434711f5` |
| Focused test source/project/vector manifest | 12 | `5cb0379fa000d8b9a40bdecc55973ad22e62a71c5da065faa52f1430a953e083` |
| Approved Contracts/Security inventory | 46 | `a01dc5576702f08dc0a95caaa8a4158e457c2f1d85dd4337fc145d126a02fa2e` |
| Current complete Contracts/Security inventory | 53 | `53cdc3bf1bed8594e8d63fdc529975060fed3db96d7536f78048eea8ebe4e340` |

Every one of the original 46 Security files was compared byte-for-byte with
approved baseline `e8886ec4c277460de3d3208b3fc0b9c261c4967d`; all are identical.
The current directory also has seven committed additive Identity files:
`IIdentityHistoryCustody.cs`, `IIdentityHistoryEvent.cs`,
`IdentityAdmissionEvidence.cs`, `IdentityAdmissionScope.cs`,
`IdentityHistoryCustodyEvidence.cs`, `IdentityHistoryPolicy.cs`, and
`IdentityOperationCatalog.cs`. These are disclosed separately rather than
silently treating the current 53-file stream as the approved 46-file stream.
The frozen crypto Contracts surface and fixtures did not change.

The shared authority retains SHA-256
`542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82`.
AR-20260914-01 retains
`4b12f54fd24f62083760e9c6ef73ad57edb2c76d15f1fae07503bbe82ad9b068`;
AR-20260914-02 retains
`1d511941c09d12e1d3a09a82968fc82737dcd786b0b35082c75584b6e7358537`.
The 14-entry release manifest retains
`6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`.

The current focused workflow is bound to
`c977305f089183c7652292f4ea49fa3aaef935cca86868afb3e7745d21698bd1`,
the local lane to
`3a08210f35c41d56002612a508f2211bfe4207eb8837c4702ea8634a62b2cb4a`,
and the Contracts required-lane/package guard to
`097e8ac76c2e2c75e60a417d4c99b23034c5fcd1b2d17a6bd876d8cca147ad38`.
Both lanes bind the 292-test minimum and the separate invariant-globalization
case, and fail on skips. The core remains internal, provider-neutral,
`IsPackable=false`, with one direct Contracts reference, no package references,
no registration/public types, and no Azure, Dapr, Server, domain, Parties, or UI
dependency. The solution and release manifest exclude the core.

## Focused results

Environment: .NET SDK 10.0.401, runtime 10.0.12, xUnit 4.0.1. The saved TRX files
contain 292/292 Passed result rows for each full focused run and 1/1 Passed for
the invariant run; all failure, error, aborted, not-executed, and skip outcomes
are zero. The full suite executes the frozen G-001/NIST cases, all 51 required
vector identifiers V001–V048/V135/V136/V138, snapshot/tag tamper, owned-buffer
clearing, closed failure surfaces, safe record formatting, and the sibling-prefix
positive regression. V138 remains an observation, not a portable performance
threshold.

| Check | Result and saved receipt |
| --- | --- |
| Focused Release restore/build with package-mode Commons dependencies | Exit 0; build zero warnings/errors; output retained in the session transcript |
| Focused Release suite, 292 minimum and fail-skips | 292/292, zero failures/skips; [log](verification-2026-10-08/focused-tests.log), [TRX](verification-2026-10-08/payload-protection-results.trx.xml) |
| Invariant-globalization V029 fail-closed case | 1/1; [log](verification-2026-10-08/invariant-tests.log), [TRX](verification-2026-10-08/invariant-results.trx.xml) |
| Focused source/Debug restore/build | Exit 0, zero warnings/errors; [restore](verification-2026-10-08/debug-restore.log), [build](verification-2026-10-08/debug-build.log) |
| Focused source/Debug suite | 292/292, zero failures/skips; [log](verification-2026-10-08/debug-tests.log), [TRX](verification-2026-10-08/debug-results.trx.xml) |
| Frozen Contracts compatibility/API class | 31/31, zero failures/skips/unrun; root independently executed and supplied the [receipt](verification-2026-10-08/contracts-compatibility-tests.log) |
| Contracts required-lane/package class | 115/115, zero failures/skips/unrun; [build](verification-2026-10-08/contracts-build.log), [class receipt](verification-2026-10-08/contracts-packaging-tests.log) |
| Core and focused-test `dotnet format style` | Both exit 0; [core](verification-2026-10-08/core-format.log), [tests](verification-2026-10-08/tests-format.log); successful formatter logs are empty |
| Structural/dependency/preservation scan | PASS: 34 production and 10 test C# files, one type per file, Allman, LF, final newline, no trailing whitespace; production normative citations; no forbidden dependency/public types; solution/manifest exclusion |
| `actionlint` for both affected workflows, `bash -n scripts/ci-local.sh` | Exit 0 |
| Scoped `git diff --check` | Exit 0 before the dated evidence was added; the new evidence also passed the handoff check |

The exact focused commands were:

```bash
dotnet restore tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false
dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --no-restore --configuration Release -warnaserror -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=false -p:GenerateDocumentationFile=true
dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --no-build --configuration Release --minimum-expected-tests 292 --fail-skips on --no-ansi -p:UseHexalithProjectReferences=false --results-directory /tmp/eventstore-story-8-3-verification-20261008 --report-xunit-trx --report-xunit-trx-filename payload-protection-results.trx
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --no-build --configuration Release --filter-method Hexalith.EventStore.PayloadProtection.Tests.AadPathTests.V029_InertPlatformNormalizer_FailsClosedThroughCoreWriteSeams --minimum-expected-tests 1 --fail-skips on --no-ansi -p:UseHexalithProjectReferences=false --results-directory /tmp/eventstore-story-8-3-verification-20261008 --report-xunit-trx --report-xunit-trx-filename invariant-results.trx
dotnet restore tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:NuGetAudit=false
dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug --no-restore -warnaserror -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -p:GenerateDocumentationFile=true
dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 292 --fail-skips on --no-ansi -p:UseHexalithProjectReferences=true -p:NuGetAudit=false --results-directory /tmp/eventstore-story-8-3-verification-20261008 --report-xunit-trx --report-xunit-trx-filename debug-results.trx
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release --no-restore -warnaserror -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=false
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests -nocolor
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Security.PayloadProtectionV2ContractTests -failSkips -noColor
dotnet format style src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --verify-no-changes --no-restore --severity info
dotnet format style tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --verify-no-changes --no-restore --severity info
actionlint .github/workflows/payload-protection.yml .github/workflows/advisory-tests.yml
bash -n scripts/ci-local.sh
```

The Debug source restore/build explicitly disabled NuGet audit to obtain bounded
source validation. It supplies compilation/execution evidence only, and does
not establish a fresh dependency-audit pass. An attempted Contracts restore
stalled and was cancelled; the saved [restore log](verification-2026-10-08/contracts-restore.log)
is not counted as a completed restore. Its subsequent no-restore build and
direct class execution passed with existing assets.

Both independent frozen verifiers passed V001–V003:
`node scripts/payload-protection/verify-golden-vectors.mjs` and
`python3 scripts/payload-protection/verify-golden-vectors.py`. The canonical
envelope SHA-256 was
`247381efe70c9c4844af998ddec864b41665461b466bda418beb8edc0e5406be`;
the wrapper was
`35388a17c7d950f775378c47b6263d75088be358fbe6372a71f2bec55a3356c3`.
Runtime versions were Node 26.4.0/OpenSSL 3.5.7 and Python
3.14.4/cryptography 46.0.5/OpenSSL 3.5.5 (27 Jan 2026). Frozen fixtures,
verifiers, requirements, Contracts test project, and contract/API test hashes
are in `binding.json`.

## Broad gates and package limits

The broad build requirement is **unmet on the current checkout**. Neither
failure was repaired, cleaned, or hidden, and no old full-solution PASS is
carried forward:

```bash
dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=false
dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=true
```

Release exited 1 with six missing `TenantStatus`/`TenantRole` compilation errors
in `Server.Tests/Authorization/TenantsAuthorizationContractMappingTests.cs`;
zero warnings. See [Release failure](verification-2026-10-08/solution-build.log).
Source/Debug exited 1 with 45 duplicate generated Tenants controller/type/member
errors in `references/Hexalith.Tenants.Api`; zero warnings. See
[Debug failure](verification-2026-10-08/solution-debug-build.log). These belong
outside Story 8.3 and the focused core results do not resolve them.

The normal shared pack command was attempted and interrupted after stalled
restore work; exit 137 is preserved in the session transcript:

```bash
python3 scripts/pack-release-packages.py /tmp/eventstore-story-8-3-verification-20261008/packages 999.0.0-ci-test
NuGetAudit=false python3 scripts/pack-release-packages.py /tmp/eventstore-story-8-3-verification-20261008/packages 999.0.0-ci-test
```

The [default pack log](verification-2026-10-08/pack.log) and
[environment-variable attempt log](verification-2026-10-08/pack-audit-disabled.log)
are retained. The second command's environment variable did **not** disable the
tracked `NuGetAudit=true` MSBuild property; it is not a successful disabled-audit
lane, and neither attempt establishes the normal restore-based release gate.

A bounded fallback packed each of the 14 existing manifest projects into a
fresh separate directory using existing restored assets. For each exact
manifest `project` value, it ran:

```bash
dotnet pack <manifest-project> --configuration Release --no-restore --output /tmp/eventstore-story-8-3-verification-20261008/packages-no-restore -p:Version=999.0.0-ci-test -p:GeneratePackageOnBuild=false -p:UseHexalithProjectReferences=false -m:1 -nodeReuse:false
python3 scripts/validate-nuget-packages.py /tmp/eventstore-story-8-3-verification-20261008/packages-no-restore
python3 tools/validate-release-packages.py /tmp/eventstore-story-8-3-verification-20261008/packages-no-restore 999.0.0-ci-test
```

All 14 individual pack commands exited 0, exactly 14 `.nupkg` archives resulted,
and both validators exited 0. PayloadProtection is absent. The exact per-project
commands are in the [fallback pack log](verification-2026-10-08/pack-no-restore.log);
[NuGet validation](verification-2026-10-08/nuget-validation.log) and
[release validation](verification-2026-10-08/release-validation.log) are saved.
Archive names, sizes, and digests are bound in `binding.json`; archives remain
under `/tmp` and are not added to the repository. This demonstrates the existing
14-package output/exclusion with cached restores. It is not a normal full
release-gate or fresh audit pass.

A supplemental standalone source/Debug core build after the packaging mode
changes exited 1 with MSB3243/CS1704 mixed package/source
`Hexalith.Commons.UniqueIds` identities. A matching focused source restore exited
0, but the subsequent standalone build still failed the same way. The exact
commands and outputs are retained in
[first build](verification-2026-10-08/core-debug-build.log),
[restore](verification-2026-10-08/debug-restore-after-pack.log), and
[second build](verification-2026-10-08/core-debug-build-after-restore.log):

```bash
dotnet build src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --configuration Debug --no-restore -warnaserror -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -p:GenerateDocumentationFile=true
dotnet restore tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:NuGetAudit=false
dotnet build src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --configuration Debug --no-restore -warnaserror -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -p:GenerateDocumentationFile=true
```

No dependency fix or additional retry was made. The earlier complete source/Debug
focused build and its 292 passing result rows are retained as the successful
source-graph execution; this supplemental failure is separately disclosed.

## Living-source recheck

The root agent independently rechecked the authoritative living sources on
2026-10-08. [NIST SP 800-38D](https://csrc.nist.gov/pubs/sp/800/38/d/final)
remains the November 2007 final. The
[Rev.1 second pre-draft page](https://csrc.nist.gov/pubs/sp/800/38/d/r1/2prd)
still states there is no actual draft document; comments closed July 31 and
submitted comments were added September 1. The .NET 10
[required-tag-size AesGcm constructor](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.-ctor?view=net-10.0)
and [ZeroMemory](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-10.0)
documentation remain consistent with the frozen profile. Supplementary checks
covered the platform support gate and portable AES-256/12-byte nonce/16-byte tag
profile in [cross-platform cryptography](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography),
and the canonical [RFC 5116](https://www.rfc-editor.org/rfc/rfc5116),
[RFC 6901](https://www.rfc-editor.org/rfc/rfc6901), and
[RFC 4648](https://www.rfc-editor.org/rfc/rfc4648) pages. These dated checks require
no profile change and do not alter historical preflight evidence.

Receipt durability correction: the three original TRX byte streams are preserved
unchanged as `.trx.xml` files because repository `*.trx` ignore rules exclude
the original names. The binding keys and receipt names point to these durable
copies; result rows, counters, sizes, and content hashes are unchanged.
