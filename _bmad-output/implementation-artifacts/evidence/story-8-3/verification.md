# Story 8.3 Implementation Verification

## Disposition

The provider-neutral core and focused test implementation are ready for
independent review. They do not authorize Stories 8.4/8.5. Literal completion
of frozen V030 is blocked by the normative inconsistency recorded below; no
frozen authority byte was changed to conceal it.

## Content Binding

Hash streams are the SHA-256 of sorted `sha256sum` output, including each
relative path, and exclude generated `bin/` and `obj/` content.

| Inventory | Files | SHA-256 |
| --- | ---: | --- |
| `src/Hexalith.EventStore.PayloadProtection` (`*.cs`, `*.csproj`) | 29 | `9a8d0e85f8f3416e0615cdbc51275c020bf8a91f0a5eb6d683ee6d875b2a1206` |
| `tests/Hexalith.EventStore.PayloadProtection.Tests` source/project/manifest | 11 | `610323256f1b9a2dd4206d63b63bd1c41877031bed551f6f859959861fbff01d` |
| Core project | 1 | `358feb7e012807a2e54da26ca5324e668a35cefeb7689dd49d5b424ef9b7e91f` |
| Test project | 1 | `5b29d17454fd11c65965c6cc66deb70571f7c995d9512384f28b38d37185aed5` |
| Vector execution manifest | 1 | `3cc4898d645fb0cf31481abebfe5730d0869d385b13d8f96960c58841bd75297` |

The production inventory contains one internal documented type per C# file:
limits/exceptions/results/context/material, strict canonical text/ULID/base64url,
HXP2 envelope, HXAD AAD, HXPM manifest, restricted RFC 6901, bounded JSON,
AES-GCM, delayed entropy/material generation, atomic event transformation,
owned-buffer observation, and closed diagnostics. The project is
`IsPackable=false`, references only Contracts directly, has no public types or
registration, and has no Azure, DAPR, Server, domain, Parties, or UI dependency.

The test inventory contains the required `Envelope`, `AadPath`,
`Cryptography`, `JsonTransform`, `LimitsAndConcurrency`, and `Diagnostics`
areas plus three bounded helpers and a separate execution manifest. Story 8.2
fixtures are linked read-only rather than copied or modified. The manifest
assigns inherited V001-V003 and 48 Story 8.3 vectors V004-V048/V135-V136/V138;
all 51 identifiers have a corresponding xUnit trait and none is skipped.

## Verification Results

| Command | Result |
| --- | --- |
| `node scripts/payload-protection/verify-golden-vectors.mjs` | PASS V001-V003; frozen bytes unchanged |
| `python3 scripts/payload-protection/verify-golden-vectors.py` | PASS V001-V003; independent frozen bytes unchanged |
| `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --minimum-expected-tests 48` | PASS 137/137; zero failed/skipped; latest complete run 3.557s |
| Complete Release suite with detailed stdout | PASS 137/137; V138 observed 16 concurrent hostile calls, 1,398,212-character input, cancellation checkpoints 1/256/512, 159,467,104 process-wide allocated bytes, and 87.471ms; these are observations, not a performance gate |
| `dotnet format style ... --verify-no-changes` for each new project plus an LF scan | PASS; code-style diagnostics clean and all C# source is LF-only per `.gitattributes` |
| `dotnet build src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --configuration Release -m:1 -nodeReuse:false -p:EnableAotAnalyzer=true -p:EnableTrimAnalyzer=true` | PASS; zero warnings/errors, latest run 2.48s |
| `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -nodeReuse:false` | PASS; zero warnings/errors, latest run 20.00s |
| Release pack into `/tmp/eventstore-story-8-3-packages.WsDViS` plus `scripts/validate-nuget-packages.py` and `tools/validate-release-packages.py` | PASS; exactly the existing 14 version `999.0.0-ci-test` archives; new project excluded |
| Dependency/scope scans | PASS; one direct Contracts project reference, no direct package reference, no forbidden provider/Server/domain dependency, no new public type, no `.slnx` or release-manifest entry |
| `git diff --check` | PASS |

The delayed material factory was exercised explicitly: V041 makes zero material
calls for an all-null selection and returns the exact caller-owned input bytes;
V045 rejects 4,097 paths with zero material/AES calls. V046 runs two concurrent
writers against one conditional reference reservation, forces their first
reference to collide, and observes a wholly fresh DEK/reference on the loser.
Success and authentication-failure tests observe DEK, selected-plaintext, and
decrypted-plaintext buffers only after `CryptographicOperations.ZeroMemory`.

## Step-3 Audit Closure

The post-implementation audit expanded exact coverage from 110 to 137 passing
test cases without expanding the core into Story 8.5:

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
