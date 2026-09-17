# Story 8.3 Implementation Verification

## Disposition

The provider-neutral core and focused test implementation completed the initial
six adversarial review batches, the 2026-09-15 second-pass review, third-pass
chunks 1 through 4a, and a full-diff fourth pass. All mutable fourth-pass
findings are patched and verified.
They do not authorize Stories 8.4/8.5. The constructibility interpretations approved by
`AR-20260914-02` remain exact; no frozen authority byte was changed. On
2026-09-15 the human EventStore owner authorized and applied the V004
amendment-list and authority section 8.4 Server-ownership corrections.
The follow-up review's final-collision cancellation and independent-instrument
diagnostics findings are patched. The fourth pass hardened active-workflow
binding, malformed-parse cancellation precedence, and deterministic entropy
distinctness while strengthening existing cases; the current 291-case suite
passes with no failures or skips. Fifth-pass group 1 additionally replaced
base64url staging `Array.Clear` calls with non-elidable cryptographic zeroing
and single-sourced the protected format label from its canonical UTF-8 bytes.
The independent chunk-1 follow-up now exercises fail-closed behavior in an
invariant-globalization process and pins multibyte UTF-8 field ceilings.

## Content Binding

Hash streams are the SHA-256 of sorted `sha256sum` output, including each
relative path, and exclude generated `bin/` and `obj/` content.

| Inventory | Files | SHA-256 |
| --- | ---: | --- |
| `src/Hexalith.EventStore.PayloadProtection` (`*.cs`, `*.csproj`) | 35 | `f6cf266c83d1d79ca23b5e92547aff5e908d29bea43e93ae36dac3c0434711f5` |
| `tests/Hexalith.EventStore.PayloadProtection.Tests` source/project/manifest | 12 | `0a5e5064a3567c119bf0dbc6e99f1f5eb79e83ecfeb573509ef8a4e590911c2a` |
| Core project | 1 | `c73a8db3b4eb994adbbdf5bd90ea9e9d9bacac5b5ac9dd792ff3021f56e4fbe9` |
| Test project | 1 | `5b29d17454fd11c65965c6cc66deb70571f7c995d9512384f28b38d37185aed5` |
| Vector execution manifest | 1 | `dfa6a44ccdab595af6081d7b2c752e5f01870aeb74265d76940e43c5ba336ea8` |
| GitHub focused lane | 1 | `10c2dd3c86918712759fe0abfc8352d4b129b74c28af96213da9e08966208017` |
| Local focused lane | 1 | `8d55cc01e219d5ca4be2875b5bfb108a125063154633f5735cf787a0c8aaf7b3` |
| Required-lane workflow guard | 1 | `581ac10f4ecc0ccfc254618473afba5a967adb266ced92d73504a927d084cb83` |

The production inventory contains one internal documented type per C# file:
limits/exceptions/results/context/material, strict canonical text/ULID/base64url,
HXP2 envelope, HXAD AAD, HXPM manifest, restricted RFC 6901,
payload-proportional byte-range JSON indexing, root snapshot and atomic event
transformation, AES-GCM, delayed entropy/material generation, owned-buffer
observation, and closed diagnostics. The project is
`IsPackable=false`, references only Contracts directly, has no public types or
registration, and has no Azure, DAPR, Server, domain, Parties, or UI dependency.

The test inventory contains the required `Envelope`, `AadPath`,
`Cryptography`, `JsonTransform`, `LimitsAndConcurrency`, and `Diagnostics`
areas plus three one-type-per-file bounded helpers and a separate execution
manifest. Tests load the linked Story 8.2 G-001, NIST, and ownership fixtures
read-only rather than relying only on duplicated constants. The manifest
assigns inherited V001-V003 and 48 Story 8.3 vectors V004-V048/V135-V136/V138;
all 51 identifiers have a corresponding xUnit trait, the complete suite
contains 291 cases, and none is skipped.

## Verification Results

| Command | Result |
| --- | --- |
| `node scripts/payload-protection/verify-golden-vectors.mjs` | PASS V001-V003; frozen bytes unchanged |
| `python3 scripts/payload-protection/verify-golden-vectors.py` | PASS V001-V003; independent frozen bytes unchanged |
| `dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --no-restore -warnaserror -m:1 -nodeReuse:false -p:GenerateDocumentationFile=true` then `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --no-build --minimum-expected-tests 291 --fail-skips on --no-ansi` | PASS 291/291; zero warnings, failures, or skips after the independent chunk-1 follow-up |
| `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --no-build --filter-method Hexalith.EventStore.PayloadProtection.Tests.AadPathTests.V029_InertPlatformNormalizer_FailsClosedThroughCoreWriteSeams --minimum-expected-tests 1 --fail-skips on --no-ansi` | PASS 1/1; both `ProtectEvent` and `AadCodec.Write` fail closed with `PayloadProtectionCryptographicException` when the platform normalizer is inert |
| `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release --no-restore -warnaserror -m:1 -nodeReuse:false` then direct xUnit class execution for `ReleasePackageManifestTests` | PASS 115/115; the YAML-aware required-lane guard binds the active focused workflow command, 291-case floor, invariant-globalization invocation, and fail-skips policies |
| Focused `dotnet test --filter-method` runs for `MaterialGeneration_CancellationDuringFinalCollisionCleanupWins` and `ThrowingOperationsCounterListener_DoesNotSuppressDurationMeasurement` | PASS 1/1 each after the follow-up review patches; focused Release build passed with zero warnings/errors |
| `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --no-build --filter-method "Hexalith.EventStore.PayloadProtection.Tests.LimitsAndConcurrencyTests.V138_HostileConcurrentLoad_IsBoundedAndCancellableAsync" --output Detailed --no-ansi` | PASS 1/1; 16 hostile unprotect calls met at an in-core barrier; 1,398,212-character input; cancellation checkpoints 1/256/512; 92,572,992 process-wide allocated bytes; 65.168ms; observations only |
| The two exact `dotnet format style` commands and one-type/Allman/LF/trailing-whitespace scans below | PASS; zero focused-project formatter or structural-style violations. `dotnet format whitespace` is not used because the tracked `.gitattributes` forces LF while the repository-wide `.editorconfig` requests CRLF; the explicit tracked-LF scans pass. |
| `dotnet build` for the core and focused test projects with `--no-restore -warnaserror -p:GenerateDocumentationFile=true` | PASS sequentially; zero undocumented public/protected/internal-member warnings |
| `dotnet build src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --configuration Release --no-restore -m:1 -nodeReuse:false` | PASS; zero warnings/errors. The AOT and trim analyzers are `EnableAotAnalyzer`/`EnableTrimAnalyzer` project properties on the core, so they apply to every build of it, including the blocking focused lane, and no command-line flag is required. Scope limit: project properties do not propagate to project references, so the frozen `Hexalith.EventStore.Contracts` dependency is not analyzed. That dependency does not satisfy these analyzers at HEAD (12 IL2026/IL3050 diagnostics in `QueryResult.cs`, `EventStorePayloadSerialization.cs`, `DomainServiceWireResult.cs`, and `EventStorePayloadProtectionMetadataCarrier.cs`); fixing it is outside Story 8.3. This result therefore covers the core's own compilation only, which a control probe confirms is genuinely analyzed. |
| `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 -nodeReuse:false` | PASS; zero warnings/errors |
| `package_dir="$(mktemp -d /tmp/eventstore-story-8-3-packages.XXXXXX)"; python3 scripts/pack-release-packages.py "$package_dir" 999.0.0-ci-test; python3 scripts/validate-nuget-packages.py "$package_dir"; python3 tools/validate-release-packages.py "$package_dir" 999.0.0-ci-test` | PASS; exactly the existing 14 archives; PayloadProtection excluded |
| The exact dependency/surface/preservation commands below | PASS; one direct Contracts reference, no forbidden dependency/public type, and frozen solution/release manifest unchanged |
| `actionlint .github/workflows/payload-protection.yml .github/workflows/advisory-tests.yml` and `bash -n scripts/ci-local.sh` | PASS; both direct lanes require 291 tests, run the invariant-globalization regression, and fail on skips |
| `git diff --check` | PASS |

The structural and preservation rows are replayable from the repository root
with these exact commands:

```bash
dotnet format style src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --verify-no-changes --no-restore --severity info
dotnet format style tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --verify-no-changes --no-restore --severity info
for file in $(find src/Hexalith.EventStore.PayloadProtection tests/Hexalith.EventStore.PayloadProtection.Tests -type f -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | sort); do count=$(rg -c '^\s*(?:internal|public|private|protected)\s+(?:(?:sealed|static|abstract|readonly|partial)\s+)*(?:class|record(?:\s+struct)?|interface|enum|struct)\b' "$file" || true); test "$count" = 1 || exit 1; done
! rg -n --pcre2 '^\s*(?:(?:internal|public|private|protected)\s+)?(?:(?:sealed|static|abstract|readonly|partial)\s+)*(?:class|record(?:\s+struct)?|interface|enum|struct|if|for|foreach|while|switch|catch|finally|else|try|using)\b[^;]*\{\s*$' src/Hexalith.EventStore.PayloadProtection tests/Hexalith.EventStore.PayloadProtection.Tests --glob '*.cs'
! rg -lU '\r$' src/Hexalith.EventStore.PayloadProtection tests/Hexalith.EventStore.PayloadProtection.Tests --glob '*.cs'
! rg -n '^using (Azure|Dapr|Hexalith\.EventStore\.Server|Hexalith\.Parties|Hexalith\.Commons)' src/Hexalith.EventStore.PayloadProtection --glob '*.cs'
! rg -n '^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|readonly\s+|partial\s+)*(class|record|interface|enum|struct)\b' src/Hexalith.EventStore.PayloadProtection --glob '*.cs'
rg -n '<(ProjectReference|PackageReference)' src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj
test -z "$(git diff --name-only -- Hexalith.EventStore.slnx tools/release-packages.json src/Hexalith.EventStore.Contracts tests/Hexalith.EventStore.Contracts.Tests scripts/payload-protection _bmad-output/implementation-artifacts/evidence/story-8-2 references/Hexalith.FrontComposer | awk '$0 != "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs"')"
sha256sum Hexalith.EventStore.slnx tools/release-packages.json
```

The content-binding streams are reproduced exactly with:

```bash
find src/Hexalith.EventStore.PayloadProtection -maxdepth 1 -type f \( -name '*.cs' -o -name '*.csproj' \) -print0 | sort -z | xargs -0 sha256sum | sha256sum
find tests/Hexalith.EventStore.PayloadProtection.Tests -type f \( -name '*.cs' -o -name '*.csproj' -o -name 'vector-execution.json' \) -not -path '*/bin/*' -not -path '*/obj/*' -print0 | sort -z | xargs -0 sha256sum | sha256sum
sha256sum src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj tests/Hexalith.EventStore.PayloadProtection.Tests/Fixtures/vector-execution.json
```

## Non-Blocking Repository Context

A supplemental invocation of the unrelated Server test project was not used as
Story 8.3 acceptance evidence:

```bash
dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build -- --minimum-expected-tests 201 --fail-skips on
```

It discovered 3,339 tests and reported 26 failures: deliberate DW1 red-phase
`FAIL_SKIP` scaffolds plus the pre-existing tracked-reusable-content scan over
unrelated artifacts. The Story 8.3 focused project has no skips or failures.

The delayed material factory was exercised explicitly: V041 makes zero material
calls for an all-null selection and returns the exact caller-owned input bytes;
V045 rejects 4,097 paths with zero material/AES calls. V046 runs two concurrent
writers against one conditional reference reservation, forces their first
reference to collide, and observes a wholly fresh DEK/reference on the loser.
Success and authentication-failure tests observe DEK, selected-plaintext, and
decrypted-plaintext buffers only after `CryptographicOperations.ZeroMemory`.

## Final Review Audit Closure

The review-loop audit expanded exact coverage from 137 to 217 passing test
cases, the first independent-review patches expanded it to 226, and final
review hardening expanded it to 246, the second-pass review expanded it to 250,
the follow-up review added two focused regressions for a 252-case suite, the
third-pass codec review raised it to 254, and bounded-JSON review raised it to
263. Third-pass chunk 4a raised it to 290, without expanding the core into
Story 8.5:

- V004-V014 now assert that every locally invalid carrier/header exit performs
  zero key lookups. V007 separately covers forbidden alphabet, padding,
  mod-four-one, and decode/re-encode rejection; V008 exercises `02` and `ff`
  at each closed identifier/flag byte; V009 covers declared zero, exact maximum,
  maximum-plus-one, `uint` maximum, and actual length minus/plus one.
- V015/V016/V024 record the exact substituted key reference/version passed to
  the resolver before authentication. V018-V024 cover every constructible
  missing/null/empty/malformed/exact/max-plus-one/substitution source case.
- V027 independently changes manifest count, encoded path length, path bytes,
  and digest, and tests removed, plaintext-substituted, duplicate, relocated,
  and added wrapper sets with atomic unreadable output. V028-V031 cover
  delimiters, ASCII case, printable U+2400, C0/DEL boundaries, NFC rejection,
  strict pointer escaping, unsigned UTF-8 sorting, and resulting ordinals.
- V038/V039 now exercise the byte-core's depth rejection and cancellation both
  during bounded walking and after one internal encryption, without claiming
  policy discovery/getter behavior. V047 injects a duplicate ordinal separately
  and proves a reservation collision consumes wholly fresh key material.
  V135 accepts exactly the configured write maximum and rejects maximum-plus-one
  before material creation while retaining the immutable 1 MiB read maximum.
- Dedicated exit tests prove caller input is unchanged, no partial result is
  returned, and selected plaintext, decrypted plaintext, and transferred DEKs
  are cleared on configurable-limit, cancellation-after-partial-mutation, and
  authentication-after-partial-decryption exits. Diagnostics tests inspect the
  actual activity, metric, exception, and typed-result surfaces for closed
  names/tags/messages and hostile canary absence.
- Manifest prefix validation catches interposed ancestors such as `/a`,
  `/a-foo`, `/a/b`; manifest enumeration, sort, encoding, and chunked hashing
  have distinct cancellation proof. Writer byte/node/depth expansion is
  projected before material creation, and event reader aggregate bounds are
  enforced before lookup.
- Wrapper discovery accepts only literal `$pdenc`, materializes escaped paths
  incrementally under the 2,048-byte cap, and observes cancellation through
  wrapper maps, path construction, replacement copying, and sorting. Snapshot
  type IDs enforce strict lowercase kebab suffixes.
- Full reader tests cover V010-V012, escaped `~`/`/` member names, event and
  snapshot key outcomes, invalid-context pass-through, reserved markers,
  decoded duplicate aliases, exact 4,096 wrappers, and cumulative plaintext.
  V138 now gates 16 hostile unprotect calls inside the core.
- Final independent-review patches reject escaped reserved names before lookup,
  check cancellation before manifest enumeration and during raw property scans,
  short-circuit provably oversized text, and classify unsupported AES before
  external material or resolver calls.
- The sixth review batch cleared stack nonce prefixes, added bounded pointer
  decoding and disposal/final-scan cancellation, and proved exact canonical
  wrapper bytes, alternate raw token restoration, malformed carrier rejection,
  frozen vector ownership, exact writer/reader maxima, invalid-material cleanup,
  and snapshot callback isolation.
- Diagnostics now isolate process-wide listeners while proving exact activity,
  instrument, measurement-tag, and failure-outcome identities. Snapshot and
  full-reader regressions cover exact configured maxima, cleanup, caller
  mutation, malformed wire keys, authenticated residual markers, resolver
  cancellation precedence, and atomic reconstruction bounds.
- Final patch regressions reject invalid UTF-8 in writer input, protected
  carriers, and authenticated plaintext; exercise escaped source member names
  through the complete transform; require zero lookup for a nonzero snapshot
  ordinal; exercise the default production entropy generator and collision
  taxonomy; enforce no-leak formatting for all five sensitive records; and
  distinguish transferred snapshot plaintext from failure-owned cleanup.
- The second pass pins independent snapshot manifest, AAD, envelope, and
  base64url bytes; asserts path-to-plaintext ordinal alignment before material
  creation; closes empty/whitespace JSON and `\/` member-name coverage; cites
  the normative digest and sections in every production source; centralizes
  HXAD/HXPM, schema, key-length, and Crockford constants; removes a dead wrapper
  upper bound; derives pre-material payload kind from path shape; avoids runtime
  format-byte initialization; distinguishes pass-through diagnostics; and
  aligns snapshot success recording with result construction.
- The third-pass bounded-JSON patch directly enumerates parser-frame retained
  names so zeroing begins without allocating a values collection. Focused
  cleanup regressions observe failed/cancelled parse snapshots, abandoned
  rewrite output, and every earlier envelope buffer when a later wrapper is
  malformed. Boundary regressions prove exact/max-plus-one discovered paths,
  preserve an unselected over-limit path through a complete protected round
  trip, and authenticate literal multibyte UTF-8 member names.
- Third-pass chunk 4a binds the dedicated workflow from the required Contracts
  lane, makes buffer-zero assertions fail closed after observer callbacks,
  proves end-to-end cross-scope and wrapper-relocation rejection, derives every
  V017 field mutation from the frozen fixture, and adds injectivity, grammar,
  ceiling, empty-carrier, ciphertext, ULID, and array-index boundaries.
  Deterministic entropy is now safe beyond 255 fills and under shared concurrent
  use; invalid-material cleanup uses nonzero sentinels; vector metadata and all
  consumed NIST fixture fields are asserted explicitly.

Some registry wording cannot be implemented literally without contradicting
other frozen rules. HXP2 envelope version `02` is the required valid value, so
V008 accepts that byte at offset 4 and additionally rejects `03`/`ff`; treating
`02` there as unsupported would reject G-001. In V016, an ordinal-only change is
rejected by the required manifest/ordinal consistency check before lookup,
whereas a version change performs the exact substituted-version lookup and then
fails authentication. V023 has no nullable or caller-selected format source in
this story because the format is the frozen constant `json+pdenc-v2`; missing
and empty raw AAD fields plus the v1 substitution are nevertheless proven to
fail authentication. These constraints are reported rather than hidden by a
test-only public behavior.

## Frozen Limitation

V030 requires exact total AAD sizes 4,096 and 4,097 while the same normative
schema caps all eleven value fields such that the absolute mathematical maximum
is only 3,873 bytes:

```text
8-byte header + 11 * 6-byte field headers
+ (256 + 128 + 256 + 1024 + 2048 + 26 + 4 + 13 + 4 + 8 + 32)
= 3873
```

The current immutable `AggregateIdentity` contract further limits tenant and
domain to 64 ASCII characters, reducing the maximum constructible runtime AAD
to 3,617 bytes. V030 therefore tests exactly 3,617, then rejects a path field at
2,049 bytes. An AAD of 4,096 or 4,097 cannot be produced without violating a
field bound first. Changing a bound, the total boundary, or the vector would
alter frozen human-owned intent and requires renegotiation; this implementation
does not make that unauthorized change.

V038/V039 policy discovery/getter behavior remains in Story 8.5's explicitly
frozen ownership boundary. Story 8.3 covers the applicable core half of those
registry lines: bounded cycle-equivalent/depth rejection, cancellation during
the JSON walk, and atomic cancellation after partial internal protect/unprotect
mutation. No policy-discovery, getter-fault mapping, or durable-lifecycle
capability is claimed by this core.

The implementation is an internal, non-packable core only. It does not supply
historical routing, policy discovery, key storage/provider lifecycle, snapshot
or Server integration, registration, publication, production certification,
or V127/V128 no-leak approval evidence. The NIST test is an informal vector
comparison, not a CAVP validation certificate.

## Third-Pass Review Addendum (historical chunk-1 checkpoint, 2026-09-15)

The rows above have been rebound to the implementation after the third
independent review. This addendum records what that review changed.

### Suite count

At this checkpoint the focused suite was **254 cases**. The third pass added two regressions:
`EnvelopeTests.V004_WriterNonceDerivation_IsEnforced` and
`AadPathTests.V031_NonBmpPointerToken_IsBudgetedByUtf8ByteCount`. The blocking lane floor
was raised to 254 in `scripts/ci-local.sh` and in the then-proposed `.github/workflows/ci.yml`
`payload-protection` job. That workflow placement was subsequently refuted and replaced by
the owner-authorized `.github/workflows/payload-protection.yml`; the current 291-case state is
bound in the tables above. Both new cases were mutation-verified: disabling the guard each
one covers turns that case, and only that case, red.

### AC3 scope boundary for observed zeroing

AC3 requires that every engine-owned sensitive buffer is *observed* zeroed. The approved
reading for Story 8.3 is: `ISensitiveBufferObserver` instruments every buffer that
**survives a codec call** — the DEK, selected and decrypted plaintext, transformed output,
`ProtectedPathManifest.Encoded`/`Commitment`, and `PayloadProtectionEnvelope.Nonce`/
`Ciphertext`/`Tag` — through `PayloadProtectionCore`, `PayloadCryptography`,
`BoundedJsonDocument`, and `PayloadProtectionMaterialGenerator`.

Buffers that are internal to a single codec call — `AadCodec.Write`'s six per-field identity
arrays, `Base64UrlCodec`'s decode and encode staging, `ProtectedPathManifestCodec`'s
`encodedPath` and `descendantPrefixes` copies — are zeroed unconditionally on every exit with
`CryptographicOperations.ZeroMemory`, but are **not** routed through the
observer. They never escape their call frame, so no caller can observe them at all. This is a
deliberate scope boundary, not an omission; instrumenting them would add an observer parameter
to four internal codecs for buffers with no reachable observer. Each of those four codecs now
documents the caller's ownership and zeroing obligation at its return seam.

### Canonical NFC rejection no longer depends on ICU being present

`CanonicalText` previously gated non-NFC input solely on `string.IsNormalized`. Under
globalization-invariant mode that API returns `true` for every input, so a decomposed spelling
was accepted into durable AAD — the exact durable-identity aliasing normative section 7.1
forbids. Reproduced before the fix: `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` failed
`V029_UnicodeCanonicalization_IsRejectNotNormalize(value: "Te\u0301", valid: False)` at 251/252.

`CanonicalText` now probes the platform normalizer once (U+00C5 must not report as Form D) and
throws `PayloadProtectionCryptographicException` on every call when normalization is inert,
matching the existing `AesGcm.IsSupported` platform-capability precedent in `PayloadCryptography`.
Verified after the fix: 254/254 with ICU present, and under
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` the engine fails closed on every protected operation
instead of accepting a non-NFC identity.

### Blocking CI lane restored

The first chunk-1 patch placed a `payload-protection` job in `.github/workflows/ci.yml`.
Chunk 3 later proved that Story 4.15's v3 gate reads and seals the live workflow, not only
the historical commit, so that placement was reverted. The human EventStore owner authorized
an equivalent direct-project job in the unsealed `.github/workflows/payload-protection.yml`.
It restores, builds with `-warnaserror`, and runs the project directly with the current
`--minimum-expected-tests 291 --fail-skips on`, mirroring `scripts/ci-local.sh`.
`Hexalith.EventStore.slnx`, `.github/workflows/ci.yml`, and the 14-package release inventory
remain unchanged; the engine remains outside the solution and package inventory.

## Third-Pass Review Addendum (chunk 2 of 6, 2026-09-16)

All seven bounded-JSON findings are closed. `JsonContainerFrame.Dispose` now
uses the dictionary's struct enumerator, avoiding the lazy values-collection
allocation that could precede retained-name zeroing. A warmed allocation
regression reports zero bytes allocated by disposal.

The other six findings were verification gaps rather than runtime failures.
Nine focused cases now prove owned input-snapshot zeroing on malformed and
cancelled parses, abandoned-output zeroing when rewrite cancellation arrives
after allocation, cleanup of a valid earlier envelope when a later wrapper is
malformed, exact 2,048-byte discovered-path acceptance and 2,049-byte
rejection, byte-exact preservation of an unselected over-limit member path,
and a complete protect/read round trip for a literal multibyte NFC member.
Both blocking lane floors were raised from 254 to 263. At that checkpoint review
chunks 3-6 remained pending; the current state through chunk 4a is recorded above.

## Third-Pass Review Addendum (chunks 3 and 4a, 2026-09-17)

Chunk 3's surviving core/cryptography findings and all 24 chunk-4a test findings
are closed. The dedicated workflow is guarded from the required Contracts lane;
the focused floor is 290; buffer observations now retain zeroing failures outside
the swallowed callback; and the suite proves complete end-to-end context binding,
wrapper relocation, fixture-derived V017 field coverage, deterministic entropy
concurrency, configured ceiling validation, and non-vacuous cleanup assertions.
Chunks 4b-6 remain pending and no later-story capability is claimed.

## Fourth-Pass Full-Diff Review Addendum (2026-09-17)

The full-diff Blind Hunter, Edge Case Hunter, Verification Gap, and acceptance
audit supersede the earlier partial-chunk plan. The required Contracts guard
now parses the workflow YAML, selects the active `Run PayloadProtection vectors`
step, and rejects job/step bypass conditions rather than accepting matching
comments. `BoundedJsonDocument` rechecks caller cancellation before mapping a
parser failure, with cleanup and cancellation precedence proven in the existing
case. `SequenceEntropy` encodes fill numbers above 255 and the existing
concurrent 300-fill case now requires 300 distinct DEKs. The replayable
preservation check permits exactly the Story-owned `ReleasePackageManifestTests`
edit while continuing to reject every other protected path. Focused verification
remains 290/290 and the Contracts guard remains 115/115, both with zero skips.

## Independent Chunk-1 Follow-up Addendum (2026-09-17)

The two surviving codec/wire-format review findings are closed. V021 and V022
now reject NFC values whose UTF-16 length is within the field limit but whose
strict UTF-8 encoding exceeds it. A new V029 regression exercises both
`ProtectEvent` and `AadCodec.Write`, and the blocking GitHub/local lanes rerun
that exact method in an invariant-globalization process. The required Contracts
guard parses and binds the extra workflow step and environment setting.

Both fixes were mutation-verified. Removing the `_normalizationIsFunctional`
guard made the dedicated invariant invocation fail 0/1, and replacing the
strict UTF-8 byte ceiling with `value.Length` made both focused V021 and V022
tests fail 0/1. Restored-source verification passed 291/291 plus the dedicated
invariant invocation 1/1, with no failures or skips.
