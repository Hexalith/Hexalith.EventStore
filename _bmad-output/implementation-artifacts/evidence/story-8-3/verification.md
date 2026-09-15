# Story 8.3 Implementation Verification

## Disposition

The provider-neutral core and focused test implementation completed six
independent adversarial review batches with every surviving Story 8.3 finding
patched and verified. They do not authorize Stories 8.4/8.5. The
constructibility interpretations approved by `AR-20260914-02` remain exact;
no frozen authority byte was changed.

## Content Binding

Hash streams are the SHA-256 of sorted `sha256sum` output, including each
relative path, and exclude generated `bin/` and `obj/` content.

| Inventory | Files | SHA-256 |
| --- | ---: | --- |
| `src/Hexalith.EventStore.PayloadProtection` (`*.cs`, `*.csproj`) | 35 | `0091e0e1ca5df251ea7b6848f3b07b0b61706bea8a15d3367c502e62a625a5c3` |
| `tests/Hexalith.EventStore.PayloadProtection.Tests` source/project/manifest | 12 | `95bf5c61f7affe592373b2ab8e51ce5eae1d5bf5ed24bd664e48364c2ff3f06d` |
| Core project | 1 | `c73a8db3b4eb994adbbdf5bd90ea9e9d9bacac5b5ac9dd792ff3021f56e4fbe9` |
| Test project | 1 | `5b29d17454fd11c65965c6cc66deb70571f7c995d9512384f28b38d37185aed5` |
| Vector execution manifest | 1 | `3cc4898d645fb0cf31481abebfe5730d0869d385b13d8f96960c58841bd75297` |
| GitHub focused lane | 1 | `911f7bbcae8e3be7a166ebd89ca8ef708dd3ae6eb1699171c4a636f35cf5d68f` |
| Local focused lane | 1 | `21adfb774006812eaeb85888a1dd93a8b8197ee1a39672a1e8264f5fd3de0d25` |

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
executes 246 cases, and none is skipped.

## Verification Results

| Command | Result |
| --- | --- |
| `node scripts/payload-protection/verify-golden-vectors.mjs` | PASS V001-V003; frozen bytes unchanged |
| `python3 scripts/payload-protection/verify-golden-vectors.py` | PASS V001-V003; independent frozen bytes unchanged |
| `dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --no-restore -warnaserror -m:1 -nodeReuse:false` then `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --no-build --minimum-expected-tests 246 --fail-skips on --no-ansi` | PASS 246/246; zero warnings, failures, or skips; rerun after final independent review fixes |
| `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --no-build --filter-method "Hexalith.EventStore.PayloadProtection.Tests.LimitsAndConcurrencyTests.V138_HostileConcurrentLoad_IsBoundedAndCancellableAsync" --output Detailed --no-ansi` | PASS; 16 hostile unprotect calls met at an in-core barrier; 1,398,212-character input; cancellation checkpoints 1/256/512; 92,576,352 process-wide allocated bytes; 70.559ms; observations only |
| The two exact `dotnet format style` commands and one-type/Allman/LF scans below | PASS; zero formatter or structural-style violations |
| `dotnet build` for the core and focused test projects with `--no-restore -warnaserror -p:GenerateDocumentationFile=true` | PASS sequentially; zero undocumented public/protected/internal-member warnings |
| `dotnet build src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --configuration Release --no-restore -m:1 -nodeReuse:false` | PASS; zero warnings/errors. The AOT and trim analyzers are `EnableAotAnalyzer`/`EnableTrimAnalyzer` project properties on the core, so they apply to every build of it, including the blocking focused lane, and no command-line flag is required. Scope limit: project properties do not propagate to project references, so the frozen `Hexalith.EventStore.Contracts` dependency is not analyzed. That dependency does not satisfy these analyzers at HEAD (12 IL2026/IL3050 diagnostics in `QueryResult.cs`, `EventStorePayloadSerialization.cs`, `DomainServiceWireResult.cs`, and `EventStorePayloadProtectionMetadataCarrier.cs`); fixing it is outside Story 8.3. This result therefore covers the core's own compilation only, which a control probe confirms is genuinely analyzed. |
| `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 -nodeReuse:false` | PASS; zero warnings/errors |
| `package_dir="$(mktemp -d /tmp/eventstore-story-8-3-packages.XXXXXX)"; python3 scripts/pack-release-packages.py "$package_dir" 999.0.0-ci-test; python3 scripts/validate-nuget-packages.py "$package_dir"; python3 tools/validate-release-packages.py "$package_dir" 999.0.0-ci-test` | PASS; exactly the existing 14 archives; PayloadProtection excluded |
| The exact dependency/surface/preservation commands below | PASS; one direct Contracts reference, no forbidden dependency/public type, and frozen solution/release manifest unchanged |
| `actionlint .github/workflows/ci.yml` and `bash -n scripts/ci-local.sh` | PASS; both direct lanes require 246 tests and fail on skips |
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
test -z "$(git diff --name-only -- Hexalith.EventStore.slnx tools/release-packages.json src/Hexalith.EventStore.Contracts tests/Hexalith.EventStore.Contracts.Tests scripts/payload-protection _bmad-output/implementation-artifacts/evidence/story-8-2 references/Hexalith.FrontComposer)"
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
review hardening expanded it to 246, without
expanding the core into Story 8.5:

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
