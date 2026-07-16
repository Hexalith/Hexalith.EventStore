---
title: Shared Payload-Protection Engine Security Specification And ADR
story: "8.1"
status: draft-not-authorized
decision: proposed
story_8_2_authorized: false
created: 2026-07-16
last_updated: 2026-07-16
eventstore_source_sha: b200305978577530ee2e6ba9e92b886d26dc6f6f
story_baseline_sha: 76f122332216cc5d9b44a421bdbed3ab20d35f5e
parties_source_sha: 4378dede55d92e489caf7aad63d6c2892e6f856d
---

# Shared Payload-Protection Engine Security Specification And ADR

## 1. Document Control And Authorization

This file is the single normative authority required by Story 8.1. It combines
the security specification and ADR. Runtime implementation is forbidden until
the authorization gate in section 17 is `authorized` for the exact normative
content digest.

### 1.1 Decision state

| Field | Value |
| --- | --- |
| ADR status | Proposed; not approved |
| Story 8.2 authorization | **NOT AUTHORIZED** |
| Reason | Normative technical decisions are frozen; mandatory independent reviews and named content-bound approvals are absent. |
| Required artifact | `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md` |
| EventStore inspected source | `b200305978577530ee2e6ba9e92b886d26dc6f6f` (`main`, tag `v3.67.3`) |
| Story baseline | `76f122332216cc5d9b44a421bdbed3ab20d35f5e` |
| Parties inspected source | `4378dede55d92e489caf7aad63d6c2892e6f856d` (`Hexalith/Hexalith.Parties`, `main` retrieved 2026-07-16) |
| Current release inventory | 14 packages; `tools/release-packages.json` SHA-256 `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae` |
| Normative content SHA-256 | `efb419b5fa05d0b1d9bbf463261172cce181d5ada2c0c8d305751cc57497f440` |
| Embedded golden wrapper SHA-256 | `a032a68a60eeb442941dc59b0470f2e88195469d2a5db8626952dacd8b50b8a4` |
| Last source verification | 2026-07-16 |

### 1.2 Content-bound digest rule

The normative approval digest is SHA-256 over the exact UTF-8 bytes from the
line immediately after `<!-- BEGIN NORMATIVE CONTENT -->` through the line
immediately before `<!-- END NORMATIVE CONTENT -->`, including line endings.
The markers and all text outside them are excluded. The frozen artifact must use
LF (`0A`) line endings and no UTF-8 BOM. Approval evidence lives outside the
normative markers so adding detached approval records does not create a
self-referential digest. Any byte change inside the markers invalidates every
approval and resets Story 8.2 to `NOT AUTHORIZED`.

Fixture files, if introduced, are separately hashed. Their exact SHA-256 values
are normative fields inside the markers; changing a fixture therefore changes
the normative content and invalidates approval.

### 1.3 Approval and authorization record

| Role | Named approver | Decision | UTC timestamp | Normative SHA-256 | Evidence |
| --- | --- | --- | --- | --- | --- |
| Architect | Not recorded | Pending | — | — | Required |
| Security Reviewer | Not recorded | Pending | — | — | Required |
| EventStore owner | Not recorded | Pending | — | — | Required |
| Release owner | Not recorded | Pending | — | — | Required |
| Operations owner | Not recorded | Pending | — | — | Required |
| Parties maintainer | Not recorded | Pending | — | — | Required |
| Test Architect / independent vector reviewer | Not recorded | Pending | — | — | Required |

The Administrator approval dated 2026-07-16 applies only to the planning change
proposal. It does not approve this specification or authorize Story 8.2.

<!-- BEGIN NORMATIVE CONTENT -->

## 2. Normative Language, Scope, And Non-Goals

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**,
**SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **MAY**, and **OPTIONAL** are
normative as described by BCP 14 when capitalized.

### 2.1 Accepted scope

This specification freezes the EventStore-owned optional engine boundary,
`pdenc-v2` durable format, authenticated-data contract, policy discovery,
shared key mechanics, production-adapter boundary, backward-read behavior,
failure taxonomy, lifecycle resilience, threat controls, vectors, release
handoff, and Parties compatibility/rollback gates.

### 2.2 Non-goals

- It does not implement runtime code, create a package/project, provision a
  provider, modify topology, change the 14-package manifest, or write persisted
  data.
- It does not move legal or retention policy, erasure orchestration,
  certificates/reports, Art.20/Art.30 behavior, or UX/copy out of Parties.
- It does not claim that an interface, no-op, LocalDev, in-memory provider,
  mock, or configuration shape is production protection evidence.
- It does not authorize aggregate/event deletion or rewrite immutable history.
- It does not authorize removal of the Parties-local provider or rollback path.
- It does not expand the Phase 4 MVP; Epic 8 remains committed post-MVP work.

## 3. Requirement And Evidence Traceability

| Authority | Binding requirement | Normative sections | Required verification |
| --- | --- | --- | --- |
| FR37 | Optional EventStore-owned engine; v2 format/AAD; historical readers; policy/erasure seams; shared lifecycle; real backend; EventStore and Parties proof | 4-17 | Package/API inventory, owner goldens, real-backend state evidence, package-only consumer, dual-provider Parties proof, rollback after v2 writes |
| NFR19 | Fail closed; byte-stable versioning; bounded typed outcomes; zeroing; cache invalidation; production startup restrictions; tested rollout/downgrade/rollback | 6-15 | Negative/mutation vectors, lifecycle/cache/provider tests, startup matrix, no-leak evidence, rollback rehearsal |
| AD-23 | EventStore owns engine/contracts; providers/operators own custody; Parties retains domain policy | 4-5, 9-11, 16 | Responsibility approval, dependency guardrails, provider/custody review, consumer approval |
| July 16 ownership decision | Planning only; no Story 8.2 authorization, provider provisioning, manifest change, Parties edit, or rollback deletion | 1-2, 16-17 | Scope diff and approval-evidence validation |
| AC1 | Ownership/package/dependency/backend/custody boundaries | 4-5, 11 | Architecture and package/release review |
| AC2 | Test-vector-ready v2 envelope, AES-GCM, nonce/tag, AAD, paths, format | 6-8, 14 | Two-toolchain goldens plus parser/mutation vectors |
| AC3 | Exact historical routing, rollout, downgrade, rollback | 12-13 | Compatibility review and persisted post-v2 rollback rehearsal |
| AC4 | Exact policy/key-lifecycle contracts and operational names | 9-10 | Public API inventory, lifecycle/cache/resilience review |
| AC5 | Named real production backend and restrictions | 11 | Real-service conformance with failure injection and provider-state evidence |
| AC6 | Threat/misuse/no-leak model and vectors | 14-15 | Independent threat review, mutation corpus, leak sentinel extension |
| AC7 | Exact approved artifact, decisions, approvals, authorization | 1, 16-17 | Digest recomputation and named content-bound approvals |

Planning input identities inspected on 2026-07-16:

| Artifact | SHA-256 |
| --- | --- |
| `_bmad-output/planning-artifacts/prd.md` | `9b714861dbbe5d680928cccf674bcb87d4f22d9d5ed9796a6cff0f05a9589fa7` |
| `_bmad-output/planning-artifacts/architecture.md` | `e4ace5de57de6a72807e1bc1b690917afb3cea73b191aa4723e991cd6be5ad51` |
| `_bmad-output/planning-artifacts/epics.md` | `3b896d16afe22a39598f071eb12203c4675d81b572e81323287adc66662944aa` |
| `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16.md` | `9d2bc1d9a60b816795461eb0b5852cce486dae04638bb1281a9fc32355700fa1` |
| `docs/guides/payload-protection-and-crypto-shredding.md` | `2b4e9eb0a6659d72f5feab4fbe5ef92e7066c0b195db6a540f34cf23eb87e2fc` |

## 4. Ownership, Trust Boundaries, And Responsibility

### 4.1 Responsibility matrix

| Concern | EventStore platform | Provider / operator | Parties domain | Security | Operations | Release | Test |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Durable `pdenc-v2`, AAD, paths, backward readers | **Accountable and responsible** | Consulted for provider constraints | Consulted and supplies v1 fixtures | Approves | Informed | Verifies provenance | Owns goldens/mutations |
| Engine APIs, policy/erasure seams, key records/cache/retry/zeroing | **Accountable and responsible** | Supplies service constraints | Implements domain adapters/policy only | Approves threat controls | Approves runtime controls | Packages engine | Verifies contracts/lifecycle |
| Production root key and KEK material | No access/custody; consumes opaque versioned references and wrap operations | **Accountable and responsible** for Azure resource, key creation/rotation/retention, credentials, network, availability, and break glass | No custody through EventStore | Defines/approves custody policy | Operates and audits | No key material in build/release | Uses isolated test resource only |
| Per-write DEK creation, wrapping request, wrapped-DEK record | **Responsible** in engine; state remains tenant/domain/aggregate scoped | Backend performs KEK wrap/unwrap and returns safe typed outcomes | Requests erasure through domain workflow | Reviews hierarchy | Monitors/reconciles | Ships mechanics | Verifies persisted/provider state |
| Legal basis, retention, erasure decision/orchestration, certificates/reports, Art.20/Art.30, UX/copy | Provides generic seams and bounded outcomes only | Executes approved key operation; no legal decision | **Accountable and responsible** | Consulted | Executes approved runbook | Informed | Parties owns consumer evidence |
| Production enablement and environment policy | Validates fail-start matrix | **Accountable** for explicit enablement and approved backend configuration | Consumes only approved artifacts | Approves exceptions | **Responsible** for deployment/incident handling | Verifies immutable configuration provenance | Verifies startup matrix |
| Package inventory, SBOM, hashes, source-to-package provenance | Supplies projects/metadata | No package ownership | Pins exact approved packages | Reviews dependencies | Reviews deployability | **Accountable and responsible** | Package-only consumer checks |
| Approval and Story 8.2 authorization | Architect owns architecture disposition | Approves custody/operations boundary | Maintainer approves consumer boundary | Named reviewer approves security | Approves operability | Approves release delta | Independent reviewer approves vectors/evidence |

No row transfers production keys, credentials, provider private error detail, or
environment policy into EventStore. No row transfers reusable encryption,
format, key-cache, retry, or backend-conformance code into Parties.

### 4.2 Trust boundaries

1. **Domain process boundary:** domain code supplies typed policy decisions and
   values. It never sees KEK material, provider credentials, or wrapped-key
   implementation details.
2. **EventStore engine boundary:** the engine sees plaintext only inside the
   protect/authenticated-unprotect call, owns mutable DEK/plaintext buffers, and
   emits only ciphertext plus constructive safe metadata.
3. **State-store boundary:** immutable events/snapshots contain v2 envelopes;
   mutable key records contain wrapped DEKs and lifecycle state. State storage is
   not trusted with plaintext DEKs.
4. **Production backend boundary:** the Azure Key Vault data plane authenticates
   workload identity and performs KEK wrap/unwrap. It is not trusted with event
   plaintext or domain policy.
5. **Operator/control-plane boundary:** operators provision vault, network,
   RBAC, KEK rotation/retention, and break glass. Application identity cannot
   create, rotate, delete, recover, purge, or export KEKs.
6. **External/admin/telemetry boundary:** public and operator surfaces receive
   bounded reason codes and allowlisted metadata only. Authorization never
   relaxes the no-leak rule.
7. **Consumer/release boundary:** Parties and other modules consume exact package
   hashes and keep local rollback behavior until the G5 proof closes.

Compromise of the event/state store alone must not reveal plaintext. Compromise
of the application identity may permit unwrap during its authorization window;
network/RBAC, cache bounds, audit, and revocation limit but do not eliminate
that risk. Compromise of both stored ciphertext/wrapped DEKs and an authorized
KEK principal is outside confidentiality protection and requires provider and
operator incident controls.

## 5. Package, Dependency, Opt-In, And Environment Architecture

### 5.1 Package graph decision

Two future packable projects are selected:

| Package ID | Target | Owns | Allowed direct dependencies |
| --- | --- | --- | --- |
| `Hexalith.EventStore.PayloadProtection` | `net10.0` | Engine, codecs, policy discovery, generic key/backend/store contracts, DAPR-backed wrapped-key records, lifecycle/cache/resilience/audit, LocalDevelopment backend, DI/options/startup validation | `Hexalith.EventStore.Contracts`; `Dapr.Client`; Microsoft.Extensions abstractions/options/hosting/caching already centrally governed; BCL cryptography/JSON. No Azure SDK, domain assembly, UI, or server implementation dependency. |
| `Hexalith.EventStore.PayloadProtection.AzureKeyVault` | `net10.0` | Selected production adapter, Azure options/validation, Key Vault error classification, workload-identity client construction, wrap/unwrap/capability probe | Engine package; centrally versioned `Azure.Security.KeyVault.Keys` and `Azure.Identity`. No Contracts duplication, DAPR state implementation, domain code, certificate/report, or UX code. |

The adapter is a companion package because Azure SDK, credential, network, and
provider failure concerns must not become transitive dependencies of the
provider-neutral engine. The exact Azure package versions are deliberately not
guessed in Story 8.1: Story 8.2 must select centrally managed versions after the
mandatory source/API reverification, record them in its evidence, and keep every
`.csproj` reference versionless.

Existing `Hexalith.EventStore.Contracts` remains the owner of cross-package,
provider-neutral contracts. Story 8.2 may add the exact policy marker/interface
and erasure-state result contracts frozen in section 9 there. Backend SPI,
engine options, codecs, key-store records, and implementation types belong to
the engine package. The Azure adapter depends inward on the engine; neither
Contracts nor the engine depends on the adapter.

```text
Domain contracts / policy adapter
            |
            v
Hexalith.EventStore.Contracts  <---  Hexalith.EventStore.Server
            ^                              (existing hook only)
            |
Hexalith.EventStore.PayloadProtection
            ^
            |
Hexalith.EventStore.PayloadProtection.AzureKeyVault  ---> Azure Key Vault vault
```

The current manifest remains exactly 14 packages throughout Story 8.1. Story
8.2 creates both approved packable projects and changes the manifest and every
inventory statement/test atomically from **14 to 16**. A one-package partial
release, an engine without its selected production adapter, or an adapter hidden
inside another package is forbidden.

### 5.2 Public registration and options surface

The exact future registration surface is:

```csharp
IServiceCollection AddEventStorePayloadProtection(
    this IServiceCollection services,
    IConfigurationSection configuration);

IServiceCollection AddAzureKeyVaultPayloadProtectionBackend(
    this IServiceCollection services,
    IConfigurationSection configuration);
```

The configuration authority is `EventStore:PayloadProtection` with these exact
top-level fields:

| Field | Type/default | Rule |
| --- | --- | --- |
| `Mode` | `Disabled` | Closed enum: `Disabled`, `Enabled`. Missing/unknown is invalid when registration is called; omitted registration retains the existing no-op. |
| `Backend` | null | Closed enum: `LocalDevelopment`, `AzureKeyVault`; required only when enabled. Unknown values fail startup. |
| `WriteMode` | `V2` when enabled | Closed enum: `V2`, `ReadOnly`, `RegisteredLegacy`. `ReadOnly` rejects protect calls. `RegisteredLegacy` requires an exact named legacy writer and is only the rollback posture in section 13. |
| `LegacyWriterId` | null | Required only for `RegisteredLegacy`; exact case-sensitive registered ID. It never affects read routing. |
| `MaxProtectedValueBytes` | `1048576` | May be lowered, never raised above the v2 limit. |
| `KeyCacheTtl` | `00:01:00` | May be lowered; maximum five minutes. Section 10 further bounds lifecycle invalidation. |
| `KeyCacheEntries` | `1024` | Positive; maximum 4096 per process. |
| `OperationTimeout` | `00:00:10` | Positive; maximum 30 seconds. |

Provider-specific fields live under
`EventStore:PayloadProtection:AzureKeyVault`; section 11 freezes them. Secret
values and credentials are forbidden in these sections. Authentication uses the
workload identity credential chain selected in section 11.

Installing either package has no behavior. Omitting
`AddEventStorePayloadProtection` leaves the existing
`NoOpEventPayloadProtectionService` registered by Server. Calling it with
`Mode=Disabled` also leaves that concrete no-op service in place. Only an
explicit `Mode=Enabled`, valid backend registration, and successful startup
validation may replace `IEventPayloadProtectionService` with the engine. The
extension MUST use `Replace`/equivalent single-descriptor semantics and fail if
multiple protection services remain; resolution by registration order is
forbidden.

No automatic assembly scan enables the engine. A domain policy registration is
necessary but insufficient. Package presence, provider options, or a Key Vault
endpoint never imply consent to protect new writes.

### 5.3 Mode and environment matrix

Environment comparison uses `IHostEnvironment.IsDevelopment()` exactly. A null,
missing, or empty environment is treated as `Production`. `Development` is the
only development environment; `Dev`, `Local`, `Test`, `Staging`, `Production`,
and custom names are non-development.

| Mode | Backend | Environment | Startup result | Runtime claim |
| --- | --- | --- | --- | --- |
| Registration omitted | none | any | Start; existing no-op | Protection disabled; never readiness proof |
| `Disabled` | none or configured but unused | any | Start with concrete no-op; emit one safe information event if stale backend config exists | Protection disabled; never readiness proof |
| `Enabled` | missing/unknown | any | **Fail startup** before accepting traffic | None |
| `Enabled` | `LocalDevelopment` | exact `Development` | Start only after local option validation | Development/test aid; never production proof |
| `Enabled` | `LocalDevelopment` | missing, Test, Staging, Production, custom | **Fail startup** | None |
| `Enabled` | `AzureKeyVault` | any non-development | Start only after companion package presence, option validation, cryptographic platform check, identity/RBAC/key-version capability probe | Configured production-capable adapter; production proof still requires section 11 conformance |
| `Enabled` | `AzureKeyVault` | `Development` | Allowed only with a real reachable Azure test vault and the same validation; no emulator/mock counts | Real-adapter development/integration run |
| `Enabled` | any | unsupported `AesGcm` or failed backend validation | **Fail startup** | None |
| `Enabled` + `ReadOnly` | approved backend | any otherwise-valid environment | Start only with v2 reader/backend healthy; every protect call fails closed | Controlled inspection/recovery only; no new protected or plaintext write |
| `Enabled` + `RegisteredLegacy` | approved v2 read backend plus exact named legacy writer | environment valid for both providers | Start only after section 13 rollback authorization and dual-reader probes | Explicit rollback writer; v2 remains readable and no plaintext fallback occurs |

Every enabled failure is fail-start. The engine MUST NOT downgrade to no-op,
plaintext, `json`, LocalDevelopment, stale cache, a different provider, or a
previous KEK version. Readiness remains false until validation succeeds. A later
provider outage produces bounded typed runtime failures; it does not mutate the
selected mode.

## 6. Canonical `pdenc-v2` Envelope

### 6.1 Event and snapshot carriers

The event serialization format identifier is exactly `json+pdenc-v2`. A
protected JSON value is replaced by an object containing exactly one member:

```json
{"$pdenc":"<canonical unpadded base64url of the binary envelope>"}
```

`$pdenc` is case-sensitive. Duplicate `$pdenc`, additional members, a non-string
value, padded/non-canonical base64url, or a wrapper nested below another selected
wrapper is malformed. Writer output is UTF-8 with the member order shown and no
insignificant whitespace. The reader accepts insignificant JSON whitespace and
object-member order outside the wrapper, but the wrapper itself has one member,
so there is no alternative order.

The writer first rejects invalid UTF-8, invalid JSON, duplicate object member
names under ordinal comparison, and any pre-existing `$pdenc` member. It selects
paths before modifying the tree. An explicitly selected non-null property may
hold a scalar, object, or array; its exact raw JSON token bytes are encrypted.
Null values are not protected and do not consume an ordinal. Empty strings,
empty arrays, and empty objects are protected when selected. An ancestor and a
descendant cannot both be selected; the policy result is rejected rather than
silently choosing one.

A v2 snapshot uses one `ProtectedSnapshotPayloadV2` object with exactly these
durable logical fields: `Format="json+pdenc-v2"`, `TypeName` equal to the
assembly-independent fully qualified snapshot type name, and `Envelope` equal
to the canonical unpadded base64url binary envelope. Story 8.2 supplies an
additive stable Contracts type and a converter that recognizes the equivalent
legacy `JsonElement` object shape after DAPR object deserialization. Unknown or
duplicate fields are rejected. The snapshot's whole serialized state is the
plaintext, payload kind is snapshot, property path is the empty JSON Pointer,
and field ordinal is zero.

### 6.2 Binary envelope grammar

All integers are unsigned big-endian. There is no alignment padding. The fixed
header is exactly 28 bytes:

| Offset | Width | Field | Required v2 value / rule |
| ---: | ---: | --- | --- |
| 0 | 4 | Magic | ASCII `HXP2` (`48 58 50 32`) |
| 4 | 1 | Envelope version | `02` |
| 5 | 1 | AEAD algorithm ID | `01` = `A256GCM-HX2` |
| 6 | 1 | Nonce construction ID | `01` = fresh-DEK field ordinal |
| 7 | 1 | Key-reference kind | `01` = canonical ULID DEK reference |
| 8 | 2 | Header length | `00 1C` (28); other values are unsupported/malformed |
| 10 | 2 | Key-reference byte length | `00 1A` (26) |
| 12 | 4 | DEK version | `1..4294967295`; initial write is 1 |
| 16 | 4 | Field ordinal | `0..65535`; must match nonce and AAD |
| 20 | 1 | Nonce length | `0C` (12) |
| 21 | 1 | Tag length | `10` (16) |
| 22 | 2 | Flags/reserved | `00 00`; nonzero is rejected |
| 24 | 4 | Ciphertext length | `1..1048576` and equal to actual ciphertext bytes |

The variable region follows in this exact order:

1. 26 ASCII bytes of uppercase canonical Crockford-base32 ULID `keyRef`;
2. 12 nonce bytes;
3. exactly `ciphertextLength` ciphertext bytes;
4. exactly 16 authentication-tag bytes;
5. end of input—trailing bytes are forbidden.

The complete envelope length is therefore `82 + ciphertextLength`, bounded to
83..1,048,658 bytes. The envelope contains a durable opaque DEK reference and
DEK version, not a KEK URI, credential, provider name, wrapped DEK, key bytes,
tenant/domain/type/path text, arbitrary extension, or provider-private blob.
The wrapped DEK and provider KEK-version reference live in the separate bounded
key record specified by section 10.

### 6.3 Parser and canonical-text rules

Parsing is staged and bounded:

1. Reject a wrapper over 1,398,211 base64url characters before decoding. Reject
   any character outside `A-Z a-z 0-9 - _`, any `=`, impossible length modulo
   four, or decode/re-encode mismatch.
2. Decode only into a buffer capped at 1,048,658 bytes. Read the 28-byte header
   without slicing by attacker-controlled lengths.
3. Validate magic, every closed identifier, fixed length, flags, key reference,
   ordinal, and checked total length before any variable allocation, state-store
   lookup, provider call, cache lookup, or log.
4. Validate canonical ULID text by parse-and-re-encode equality; lowercase or
   ambiguous Crockford spellings are rejected.
5. Reconstruct AAD only from verified runtime/storage identity plus the
   validated envelope fields. Perform authenticated decryption into an owned
   buffer. Expose or parse plaintext only after tag verification succeeds.
6. Require plaintext to be exactly one valid UTF-8 JSON value with no trailing
   token. Replacement parsing is bounded by `MaxProtectedValueBytes`.

Unknown magic/version/algorithm/nonce/key-reference IDs are never guessed.
Unknown fields cannot occur because the grammar is positional and the fixed
header length/flags are closed. Integer overflow, truncated input, extra input,
inconsistent lengths, invalid canonical text, or unsupported identifiers fail
before cryptographic/provider work and are mapped by section 12.

## 7. Canonical AAD And Property Paths

### 7.1 AAD binary schema

AAD version 1 is a length-delimited binary record. All lengths and integer
values are unsigned big-endian. It starts with this eight-byte header:

| Offset | Width | Value |
| ---: | ---: | --- |
| 0 | 4 | ASCII `HXAD` (`48 58 41 44`) |
| 4 | 1 | AAD schema version `01` |
| 5 | 1 | Payload kind: `01` event, `02` snapshot |
| 6 | 1 | Field count `09` |
| 7 | 1 | Reserved `00` |

Nine fields follow in ascending ID order. Each field is `fieldId:u8`,
`typeId:u8`, `length:u32`, then exactly `length` value bytes. Type `01` is
strict UTF-8; type `02` is a four-byte unsigned integer. No field is optional.

| ID | Type | Name | Source and bounds |
| ---: | ---: | --- | --- |
| 1 | 01 | Tenant | `AggregateIdentity.TenantId`; 1..256 UTF-8 bytes |
| 2 | 01 | Domain | `AggregateIdentity.Domain`; 1..128 UTF-8 bytes |
| 3 | 01 | Aggregate | `AggregateIdentity.AggregateId`; 1..256 UTF-8 bytes |
| 4 | 01 | Payload type | persisted `EventTypeName`, or frozen snapshot `TypeName`; 1..1024 UTF-8 bytes |
| 5 | 01 | Property path | section 7.2; event 1..2048 bytes, snapshot exactly zero bytes |
| 6 | 01 | DEK reference | the envelope's exact 26 ASCII ULID bytes |
| 7 | 02 | DEK version | exactly four bytes; equals envelope value |
| 8 | 01 | Serialization format | exact UTF-8 `json+pdenc-v2` (13 bytes) |
| 9 | 02 | Field ordinal | exactly four bytes; equals envelope value |

Total AAD length may not exceed 4096 bytes. Fields, types, order, count, and
lengths are closed; duplicate, missing, reordered, unknown, or trailing data is
invalid. Delimiter concatenation and JSON AAD are forbidden.

String sources are compared ordinal and case-sensitive; no trimming or case
folding occurs. Inputs must already be Unicode Normalization Form C and must not
contain unpaired surrogates, U+0000..U+001F, or U+007F. The engine uses strict
UTF-8 (`UTF8Encoding(false, true)`) and rejects non-NFC rather than normalizing,
because normalization could alias a durable identity. Missing and null are
invalid. Empty is distinct through a zero length and is permitted only for the
snapshot root path.

Authenticated identity sources are the actor/request/storage scope already
validated by EventStore. The engine must compare envelope key reference/version
with the tenant/domain/aggregate-scoped key record and must never take tenant,
domain, aggregate, type, path, or format from provider metadata or caller-supplied
opaque extensions.

### 7.2 Canonical property path profile

Paths use RFC 6901 JSON Pointer with these additional closed rules:

- Root is `""` and is used only for the whole protected snapshot. Event values
  require a non-root path beginning `/`.
- Each object segment is the actual serialized JSON member name, not the CLR
  property name. `~` becomes `~0` and `/` becomes `~1`; all other characters are
  their strict UTF-8 NFC spelling. Any other `~` escape is invalid.
- Array segments are canonical zero-based base-10 indices: `0` or a nonzero
  digit followed by digits. Leading plus/minus, whitespace, leading zero,
  exponent, and `-` append syntax are invalid.
- Duplicate object names, unresolved segments, scalar traversal, out-of-range
  indices, a selected path beneath a selected ancestor, and two policies
  resolving to the same pointer are hard failures before key creation.
- Property/member comparison is ordinal and case-sensitive. Composed NFC names
  are accepted; decomposed non-NFC spellings are rejected, not normalized.
- NUL/control characters are rejected by section 7.1 even though base RFC 6901
  can represent them.

The writer gathers every selected path, validates the set, sorts paths by their
UTF-8 byte sequence using unsigned lexicographic order, and assigns zero-based
field ordinals in that order. This makes path-to-ordinal assignment independent
of reflection, dictionary, or JSON member enumeration order.

## 8. Algorithms, Nonces, Limits, Metadata, And Goldens

### 8.1 Cryptographic identifiers and checks

| Concern | Frozen value |
| --- | --- |
| Content encryption | AES-256-GCM |
| Algorithm ID | byte `01`; display/diagnostic identifier `A256GCM-HX2` |
| DEK | 32 uniformly random bytes from the platform CSPRNG; fresh for each event or snapshot protection write |
| Nonce | 12 bytes; nonce construction ID `01` |
| Tag | fixed 16 bytes |
| Per-DEK invocation budget | maximum 65,536 selected values, ordinals 0..65,535; no further invocation after the write attempt |
| Platform gate | `AesGcm.IsSupported` must be true; instantiate `new AesGcm(dek, 16)`; unsupported platform fails startup |
| Auth failure | plaintext buffer remains unobserved and is zeroed; bounded `BytesMetadataMismatch`/consistency mapping in section 12 |

AES-128, variable tag sizes, truncated tags, CBC, unauthenticated encryption,
provider-selected algorithms, and algorithm negotiation are not v2. Any future
algorithm needs a new closed ID, new vectors, backward reader, and approved spec
revision; it must not reinterpret ID `01`.

### 8.2 Nonce construction and uniqueness argument

The engine generates one fresh 256-bit DEK and one new canonical ULID key
reference per protected event or snapshot write. All selected values in that
single payload share that DEK. Their nonce is:

```text
00 00 00 00 || uint64-big-endian(fieldOrdinal)
```

Although the envelope ordinal is u32, v2 caps it at 65,535. The writer validates
the complete, unique sorted path set and budget before generating/wrapping a DEK
or invoking AES-GCM. Thus no two invocations under one DEK receive the same
nonce. A DEK is never reused for a later event, snapshot, retry, migration, or
process. Re-encryption generates new key material; it may preserve the logical
key reference only by incrementing DEK version and writing a distinct key
record.

Event and snapshot key-record creation follows the ordered reservation protocol
in section 10.3: the scoped `Reserved` key record is durable before the engine
may return a v2 payload. A crash before reservation leaves neither; a crash
after reservation may leave a bounded orphan that cannot be mistaken for an
active payload. Retry creates a new DEK/reference. Concurrent actor writers
still use distinct fresh DEKs; cloned/restored instances must pass restore
admission and key-reference nonexistence checks before write. A key-reference
collision fails and retries with a wholly new DEK/reference before encryption.
CSPRNG/key-generation failure, uncertain DEK reuse, an unprovable durable key
reservation, duplicate path/ordinal, or exhausted budget fails before AES-GCM.
No random-nonce collision argument or shared persisted nonce counter is required.

### 8.3 DEK versus KEK version semantics

`keyRef` and `dekVersion` identify the content-encryption key record. They are
AAD and cannot change without authenticated re-encryption. The provider KEK
resource/version and wrap algorithm exist only inside the wrapped-DEK record.
KEK rotation rewraps the same DEK and atomically replaces that record's wrapped
bytes/provider version; it does not change `keyRef`, `dekVersion`, envelope,
nonce, AAD, ciphertext, or tag. DEK rotation/re-encryption creates fresh DEK
material and increments `dekVersion` for a preserved logical reference, or uses
a new reference for a new event/snapshot. Calling a KEK version `keyVersion` or
placing it in inner AAD is forbidden.

### 8.4 Constructive durable metadata allowlist

For stored v2 events/snapshots, metadata version 1 must be exactly:

| Field | Value |
| --- | --- |
| `State` | `Protected` |
| `MetadataVersion` | `1` |
| `Scheme` | `hexalith-pdenc-v2` |
| `KeyAlias` | null |
| `ContentHint` | `application/json` |
| `CompatibilityFlags` | exactly `format=json+pdenc-v2` and `envelope=pdenc-v2` |

No other flag, value, key alias, opaque extension, nonce, tag, ciphertext, key
reference, provider, endpoint, credential, wrapped key, error detail, or encoded
blob is permitted. Read validation checks this exact allowlist; an extra
innocuously named flag (`note`, `id`, `data`), differently cased name, base64/
hex-encoded value, Unicode-confusable name, padded value, or provider-specific
field becomes provider-opaque/malformed. Existing substring/secret-shaped checks
remain a second defense, not the allowlist.

After successful authenticated unprotection, callers receive bytes/state with
`EventStorePayloadProtectionMetadata.Unprotected()` so published/projected/admin
plaintext is not paired with a false at-rest `Protected` claim. At-rest metadata
is retained only on the stored envelope and audit evidence.

### 8.5 Embedded owner golden G-001

G-001 is an event property vector. Values are ASCII/UTF-8 and hexadecimal is
lowercase without separators.

| Input | Exact value |
| --- | --- |
| DEK | `000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f` |
| Nonce | `000000000000000000000000` |
| Tenant / domain / aggregate | `tenant-a` / `parties` / `party-01` |
| Payload type | `Hexalith.Parties.Contracts.Events.PartyCreated` |
| Property path | `/email` |
| Key reference | `01J00000000000000000000000` |
| DEK version / ordinal | `1` / `0` |
| Format | `json+pdenc-v2` |
| Plaintext UTF-8 | JSON value `"alice@example.com"` |
| Plaintext hex | `22616c696365406578616d706c652e636f6d22` (19 bytes) |

Exact AAD (184 bytes):

```text
485841440101090001010000000874656e616e742d610201000000077061727469657303010000000870617274792d303104010000002e486578616c6974682e506172746965732e436f6e7472616374732e4576656e74732e5061727479437265617465640501000000062f656d61696c06010000001a30314a30303030303030303030303030303030303030303030300702000000040000000108010000000d6a736f6e2b7064656e632d763209020000000400000000
```

| Output | Exact value |
| --- | --- |
| Ciphertext | `2cddd9b7d649c3d870c9c4457449bffabd2e74` |
| Tag | `6e0dc9d44bb1b8837a8346859be6e2fa` |
| AAD SHA-256 | `32a811c9b5f69365c2ecd15d56e991ee680fa90d46b43aac15487692cf5fb4d3` |

Exact binary envelope (101 bytes):

```text
4858503202010101001c001a00000001000000000c1000000000001330314a30303030303030303030303030303030303030303030300000000000000000000000002cddd9b7d649c3d870c9c4457449bffabd2e746e0dc9d44bb1b8837a8346859be6e2fa
```

| Derived artifact | Exact value |
| --- | --- |
| Envelope base64url | `SFhQMgIBAQEAHAAaAAAAAQAAAAAMEAAAAAAAEzAxSjAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwAAAAAAAAAAAAAAAALN3Zt9ZJw9hwycRFdEm_-r0udG4NydRLsbiDeoNGhZvm4vo` |
| Envelope SHA-256 | `a8c336f261a0c7d9c1ccea948535889badcba341e2bebb097250c85a48dd9573` |
| Exact wrapper | `{"$pdenc":"SFhQMgIBAQEAHAAaAAAAAQAAAAAMEAAAAAAAEzAxSjAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwAAAAAAAAAAAAAAAALN3Zt9ZJw9hwycRFdEm_-r0udG4NydRLsbiDeoNGhZvm4vo"}` |
| Wrapper SHA-256 | `a032a68a60eeb442941dc59b0470f2e88195469d2a5db8626952dacd8b50b8a4` |

Independent reproduction on 2026-07-16 produced identical bytes with Node.js
`v26.4.0` (`node:crypto`, OpenSSL `3.5.7`) and Python `3.14.4`
`cryptography 46.0.5` (system OpenSSL `3.5.5`). The crypto-core commands below
must print ciphertext followed by tag:

```bash
node -e "const c=require('crypto').createCipheriv('aes-256-gcm',Buffer.from('000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f','hex'),Buffer.alloc(12),{authTagLength:16}),a=Buffer.from('485841440101090001010000000874656e616e742d610201000000077061727469657303010000000870617274792d303104010000002e486578616c6974682e506172746965732e436f6e7472616374732e4576656e74732e5061727479437265617465640501000000062f656d61696c06010000001a30314a30303030303030303030303030303030303030303030300702000000040000000108010000000d6a736f6e2b7064656e632d763209020000000400000000','hex'),p=Buffer.from('22616c696365406578616d706c652e636f6d22','hex');c.setAAD(a,{plaintextLength:p.length});const x=Buffer.concat([c.update(p),c.final()]);console.log(Buffer.concat([x,c.getAuthTag()]).toString('hex'))"
python3 -c "from cryptography.hazmat.primitives.ciphers.aead import AESGCM; print(AESGCM(bytes.fromhex('000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f')).encrypt(bytes(12),bytes.fromhex('22616c696365406578616d706c652e636f6d22'),bytes.fromhex('485841440101090001010000000874656e616e742d610201000000077061727469657303010000000870617274792d303104010000002e486578616c6974682e506172746965732e436f6e7472616374732e4576656e74732e5061727479437265617465640501000000062f656d61696c06010000001a30314a30303030303030303030303030303030303030303030300702000000040000000108010000000d6a736f6e2b7064656e632d763209020000000400000000')).hex())"
```

Expected output from both:
`2cddd9b7d649c3d870c9c4457449bffabd2e746e0dc9d44bb1b8837a8346859be6e2fa`.

The independently downloaded NIST CAVP GCM corpus
`https://csrc.nist.gov/CSRC/media/Projects/Cryptographic-Algorithm-Validation-Program/documents/mac/gcmtestvectors.zip`,
file `gcmEncryptExtIV256.rsp`, `[Keylen=256, IVlen=96, PTlen=0,
AADlen=0, Taglen=128]`, Count 0, expects tag
`bdc1ac884d332457a1d2664f168c76f0` for key
`b52c505a37d78eda5dd34f20c22540ea1b58963cf8e5bf8ffa85f9f2492505b4`
and IV `516c33929df5a3284ff463d7`. Both toolchains reproduced that tag exactly.
This informal vector comparison is not a CAVP validation certificate.

## 9. Policy Discovery And Public Contract Inventory

### 9.1 EventStore-owned public contracts

Story 8.2 adds one type per file under
`Hexalith.EventStore.Contracts.Security` in the existing
`Hexalith.EventStore.Contracts` assembly. The signatures are normative:

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PersonalDataAttribute : Attribute;

public enum PayloadProtectionPayloadKind
{
    Event = 1,
    Snapshot = 2,
}

public enum PersonalDataPolicyDecision
{
    Abstain = 0,
    Protect = 1,
}

public sealed record PersonalDataPolicyContext(
    AggregateIdentity Identity,
    object Root,
    object Owner,
    PropertyInfo Property,
    object Value,
    string JsonPointer,
    string PayloadTypeName,
    PayloadProtectionPayloadKind PayloadKind);

public interface IPersonalDataPolicy
{
    int ContractVersion => 1;
    string PolicyId { get; }
    int PolicyVersion { get; }
    int Order { get; }
    PersonalDataPolicyDecision Evaluate(PersonalDataPolicyContext context);
}

public enum PayloadErasureState
{
    Active = 0,
    Pending = 1,
    Invalidating = 2,
    Invalidated = 3,
    Deleted = 4,
    Unknown = 5,
    Unavailable = 6,
    Denied = 7,
}

public sealed record PayloadErasureStateRequest(
    AggregateIdentity Identity,
    string? KeyReference,
    uint? DekVersion);

public sealed record PayloadErasureStateResult(
    PayloadErasureState State,
    long LifecycleEpoch,
    string ReasonCode,
    DateTimeOffset ObservedAtUtc);

public interface IErasureStateProvider
{
    int ContractVersion => 1;
    ValueTask<PayloadErasureStateResult> GetStateAsync(
        PayloadErasureStateRequest request,
        CancellationToken cancellationToken = default);
}
```

All public/internal members receive project-standard XML documentation in Story
8.2. `ContractVersion` 1 is exact; a semantic change requires additive types or
a new version. Context records are call-scoped and never persisted. Policies
must not retain `Root`, `Owner`, `Value`, or reflection objects.

### 9.2 Discovery, precedence, and errors

The engine performs deterministic reflection over public readable instance
properties using the shared EventStore JSON naming policy. It walks objects and
arrays with reference-cycle detection, resolves the exact JSON value and RFC
6901 pointer, then evaluates policies.

1. The built-in attribute policy ID is `eventstore-personal-data-attribute-v1`,
   version 1, order 0. `[PersonalData]` always returns `Protect`.
2. Explicit `IPersonalDataPolicy` registrations are sorted by `Order` ascending,
   then `PolicyId` ordinal. IDs are unique printable ASCII kebab-case, 1..64;
   versions are positive. Duplicate/invalid identities fail startup.
3. Decisions are monotonic: any `Protect` selects the value; `Abstain` has no
   effect. There is no `DoNotProtect` decision and no policy may override an
   attribute or another positive selection.
4. Null values are skipped. A selected object/array protects the whole subtree
   and suppresses descendant evaluation. Conflicting ancestor/descendant or
   duplicate pointer selection fails before key creation as section 7 requires.
5. An unreadable property getter, policy exception, undefined enum value,
   pointer/value disagreement, serialization mismatch, or cancellation fails
   protection. Cancellation propagates; other failures are bounded internally
   and never fall back to plaintext.
6. If nothing is selected, no DEK/key record is created and the original format
   and bytes are returned unprotected. This is not a policy/readiness claim.

The engine assembly never references a domain assembly. A domain registers its
policy instance explicitly. The Parties adapter ID is
`parties-personal-data-v1-compat`; it recognizes the exact commit-pinned
`Hexalith.Parties.Contracts.PersonalDataAttribute` and natural-person rule from
Appendix B while Parties migrates to the EventStore attribute. It uses fully
qualified attribute names, so the two `PersonalDataAttribute` types are not
confused. Removal of that adapter requires Parties dual-provider/path fixtures
to prove equivalent selections.

`IErasureStateProvider` is checked once before key reservation and on every
unprotect before a cached or newly unwrapped DEK can be used. Parties supplies an
adapter from its erasure record/certificate state. An absent Parties erasure
record maps explicitly to `Active` at the current epoch; it is not `Unknown`.
`Pending` blocks new writes but permits reads unless domain policy says
otherwise; `Invalidating`, `Invalidated`, and `Deleted` block reads and writes;
`Unknown` is consistency failure; `Unavailable` is transient provider failure;
`Denied` is permanent until authorization changes. Non-cancellation exceptions
map to `Unavailable` without parsing text.

### 9.3 Selection boundaries

- Events protect selected serialized property values. EventStore does not embed
  domain-specific legal rules or infer sensitivity from names such as `email`.
- Snapshots protect the whole serialized snapshot if any policy selects any
  reachable value. Per-field snapshot encryption is forbidden in v2 because
  snapshot materialization/type evolution needs one deterministic wrapper.
- `ISerializedEventPayload` must supply valid JSON whose selected pointers and
  values agree with the typed event object; disagreement fails. Arbitrary binary
  payloads cannot use field-level v2 until a separately versioned policy/format
  is approved.
- Attributes and policies are selection inputs only. They never select backend,
  KEK, key path, retention, deletion, failure mapping, or diagnostic content.

## 10. Key Hierarchy, Lifecycle, Storage, Cache, Resilience, Audit, And Zeroing

### 10.1 Hierarchy and identities

| Layer | Owner and identity | Rule |
| --- | --- | --- |
| Provider root | Azure service/operator; never exposed | Provider trust anchor only. No EventStore identifier or operation references raw root material. |
| KEK | Operator-created versioned Azure Key Vault key selected in section 11 | Wraps/unwraps DEKs. Full provider key/version ID exists only in the bounded key record and adapter memory. Rotation creates a new provider version. |
| DEK | Engine-generated 32-byte value | Fresh per protected event/snapshot write; encrypts at most 65,536 values; always zeroed outside a bounded cache entry/operation. |
| Durable DEK reference | Canonical uppercase ULID `keyRef` plus positive u32 `dekVersion` | Stored in v2 envelopes and key records; tenant/domain/aggregate scope is external and verified. Never a KEK version. |
| Provider key/version ID | Exact opaque Azure versioned key URI | Stored only in key record. Never placed in event metadata, metric, public error, certificate/report, or log. |
| Display fingerprint | Lowercase first 16 hex chars of SHA-256 over the exact provider key/version ID | Diagnostic/audit correlation only. Never lookup, selection, authorization, wrap input, state-key segment, or uniqueness proof. |
| Existing `KeyAliasFingerprint` | Existing 16-character SHA-256 prefix contract | Preserved for workflow/audit compatibility only and kept distinct from `keyRef`, provider ID, and the record's full SHA-256 integrity hashes. |

Fingerprint collisions do not merge records or operations. Audit entries retain
unique `AuditId`/`OperationId`; a collision may display the same 16 characters
and is reported as `fingerprint-collision-observed` when detected. Migration
recomputes a fingerprint only from the exact original provider ID in trusted
adapter memory and never uses a historical fingerprint to recover that ID.

### 10.2 Scope digest and state-key grammar

`scopeDigest` is lowercase 64-character SHA-256 hex over `HXSC`, schema byte
`01`, then tenant, domain, and aggregate encoded as field IDs 1..3 using the
section 7 `typeId=01/u32-length/strict-UTF8` grammar. `tenantDomainDigest` uses
magic `HXTD`, schema `01`, and fields 1..2. The digest is an address, not
authorization; every loaded record is compared to the full authenticated
identity retained inside the record.

The configured `KeyStateStoreName` defaults to `statestore`. The selected DAPR
component must support strong-consistency reads, ETags, TTL for leases, and
multi-operation transactions. Missing capabilities fail startup. Exact keys:

| Record | Exact key grammar / bound |
| --- | --- |
| Wrapped DEK | `eventstore:pp:v2:key:{scopeDigest}:{keyRef}:{dekVersion}`; one bounded record |
| Scope index head | `eventstore:pp:v2:index-head:{scopeDigest}` |
| Scope index page | `eventstore:pp:v2:index:{scopeDigest}:{page:D10}`; max 256 unique keyRef/version pairs per page |
| Lifecycle | `eventstore:pp:v2:lifecycle:{scopeDigest}` |
| Tenant/domain write fence | `eventstore:pp:v2:fence:{tenantDomainDigest}` |
| Operation/idempotency | `eventstore:pp:v2:operation:{operationId}` |
| Distributed read lease | `eventstore:pp:v2:lease:{scopeDigest}:{operationId}`; maximum TTL 45 seconds |
| Reconciliation work | `eventstore:pp:v2:reconcile:{operationId}` |
| Audit | `eventstore:pp:v2:audit:{workflowId}:{auditId}` |

No query-all/query-prefix operation is allowed for tenant work. Index pages are
the only enumeration authority and are scope-addressed. Page/head/key creation
is one ETag transaction. Index overflow, duplicate entry, missing page, or
record/index disagreement is a consistency failure and blocks deletion proof.

The wrapped-DEK record schema version 1 contains only: full tenant/domain/
aggregate identity; `keyRef`; `dekVersion`; record state (`Reserved`, `Active`,
`RewrapPending`, `Invalidating`, `Invalidated`, `Deleted`); backend ID; exact
provider key/version ID; wrap algorithm ID; wrapped DEK bytes (1..512); lifecycle
epoch; creating operation ULID; created/updated UTC timestamps; record ETag; and
SHA-256 integrity hashes of canonical non-secret fields/wrapped bytes. It never
contains plaintext DEK, event/snapshot plaintext, nonce/tag/ciphertext,
credential, access token, connection string, provider error, or arbitrary map.

### 10.3 Create/persist and orphan protocol

Because the current provider hook executes before the actor event save and is
also used outside actor state, the key store is deliberately separate and
ordered—not falsely described as cross-store atomic:

1. Read erasure/lifecycle state and fence epoch strongly; require an allowed
   active state.
2. Generate keyRef, DEK, and operation ID; wrap the DEK through the backend.
3. Transactionally conditional-create `Reserved` wrapped-DEK record, index
   entry, index head, and operation marker **before** producing a v2 result.
4. Encrypt and return protected bytes. EventPersister preserves its existing
   all-protection-before-actor-write behavior and stages no plaintext.
5. After the actor event/snapshot save succeeds, an additive completion hook
   conditionally moves the referenced record `Reserved` to `Active`. A stored
   event that references `Reserved` is readable and schedules reconciliation;
   the stored event itself proves the reservation is not orphaned.
6. Failure before event save leaves only a `Reserved` orphan. A reconciler may
   delete it after 24 hours only after a scoped production-path check proves no
   event/snapshot/fence references it. Uncertainty retains/quarantines it.

A key record always precedes its v2 payload, so a committed payload never depends
on a key record that was merely intended. Wrap success followed by state-write
failure creates no payload and is reconciled by operation ID. No failure path
persists plaintext or retries with no-op.

### 10.4 Lifecycle, rotation, deletion, and ambiguous mutation

- **Create/generate:** engine owns DEK/keyRef/operation generation; adapter owns
  KEK wrap. Conditional state creation supplies idempotency.
- **KEK rotation/rewrap:** operator rotates KEK. Engine lists scope index pages,
  unwraps under the recorded old version, wraps under the selected new version,
  and conditionally changes one key record through `RewrapPending`. Old version
  remains usable until every record and audit verifies. Envelope/AAD is unchanged.
- **DEK re-encryption:** not authorized for immutable events by this ADR. New
  writes receive new DEKs. Snapshot rebuild receives a new DEK.
- **Invalidation/deletion:** existing crypto-shredding approval transitions the
  lifecycle to `Invalidating` and increments epoch before key removal. New reads/
  writes and leases are refused. After leases drain and caches acknowledge
  zeroing, wrapped-DEK records are invalidated/deleted transactionally by indexed
  page, evidence is verified, and the irreversible workflow advances. Provider
  KEK deletion is separate operator policy and cannot be inferred from record
  deletion.
- **Restoration:** never lowers lifecycle/fence epoch. Restore admission in
  section 13 controls any restored wrapped record.
- **Ambiguity:** create/rewrap/delete operations have stable ULID operation IDs
  and durable phases. Timeout triggers status/provider reconciliation; a mutation
  is never blindly replayed or declared complete from an exception.
- **Break glass:** may restore availability only through a named, time-bounded,
  audited operator decision allowed by provider/Parties policy. It cannot change
  a terminal EventStore lifecycle state, bypass AAD/authentication, expose keys,
  or enable plaintext fallback.

### 10.5 Cache and deletion-safety protocol

The cache stores mutable DEK byte arrays only after successful unwrap and record
validation. Cache key is `(scopeDigest,keyRef,dekVersion,lifecycleEpoch,
providerKeyVersionId)`. Defaults are 1024 entries and 60 seconds absolute TTL;
hard maxima are 4096 entries and five minutes. There is no sliding expiration,
cross-tenant key, disk/distributed plaintext cache, or plaintext-value cache.

Every unprotect operation performs a strong `IErasureStateProvider`/lifecycle
epoch read and acquires a distributed read lease **before** cache lookup. Thus a
cache hit never substitutes for current erasure state. `Invalidating` atomically
blocks new leases; the lifecycle operation waits for explicit lease release or
the maximum 45-second TTL before it can record accepted invalidation/deletion.
Only after leases drain does it broadcast invalidation, require live-instance
acknowledgements, zero matching entries, and advance the irreversible state.
Pub/sub invalidation improves speed; strong state plus leases provides
correctness. A missing acknowledgement/instance uncertainty prevents a completed
deletion claim.

Unwrap uses per-cache-key single flight. Waiter cancellation detaches that waiter
without cancelling an unwrap still needed by others; provider/global shutdown
cancels it. Failed, denied, missing, invalidated, cancelled, or partially built
entries are never cached. Eviction, expiry, replacement, invalidation, failed
construction, and disposal zero the owned array in a `finally` path. A caller
receives an operation-owned copy, zeroed after AES disposal; it never owns the
cache array.

### 10.6 Retry, timeout, and circuit breaker

| Operation/failure | Policy |
| --- | --- |
| Strong state read, wrap, unwrap: 408/429/5xx/socket/timeout | Maximum 3 total attempts within 10 seconds; delays 200 ms then 500 ms with cryptographic ±20% jitter. Honor `Retry-After` only when nonnegative, <=2 seconds, and inside remaining budget. |
| Authentication/authorization, not-found/deleted, invalid request/options, malformed data, cryptographic/auth failure | No retry. Map constructively to denied/missing/deleted/consistency outcomes. |
| Caller cancellation | Immediate propagation; no retry and no failure-cache entry. |
| Create/rewrap/invalidate/delete ambiguous mutation | No blind retry. Persist/re-read operation phase and reconcile provider/state outcome by stable operation ID first. |

The backend circuit breaker is keyed by backend ID plus endpoint fingerprint,
not tenant/key. Five transient failures in 30 seconds open it for 30 seconds.
One half-open probe is allowed. A failed probe doubles the open interval to a
maximum five minutes; a successful probe closes and resets. Permanent failures
do not trip the transient breaker but are counted safely. Open breaker maps to
`ProviderUnavailable`; it never selects another backend or plaintext.

### 10.7 Zeroing limits

DEKs, decrypted plaintext buffers, and intermediate unwrapped/wrapped-operation
buffers owned by the engine are mutable arrays rented/allocated to exact bounds.
Every success, auth failure, provider failure, cancellation, rotation, erasure,
cache eviction, and disposal path calls
`CryptographicOperations.ZeroMemory` in `finally` before returning/releasing the
buffer. Strings, immutable JSON text, exception messages, and log scopes never
contain key/plaintext material.

This is evidence only for buffers the engine owns. It is not proof that the GC,
JIT, OS, provider SDK, HSM, crash dump, swap, hardware, or another process copy
retains no material. Memory-dump controls, process isolation, dump policy, and
provider guarantees remain Operations/Security responsibilities.

### 10.8 Audit and telemetry names

No new DAPR actor type or reminder is selected. Reconciliation is a bounded
hosted worker over explicit work keys. Introducing an actor/reminder later
requires an approved spec revision.

ActivitySource and Meter names are both
`Hexalith.EventStore.PayloadProtection`. Exact activities:

- `EventStore.PayloadProtection.Protect`
- `EventStore.PayloadProtection.Unprotect`
- `EventStore.PayloadProtection.Key.Wrap`
- `EventStore.PayloadProtection.Key.Unwrap`
- `EventStore.PayloadProtection.Key.Rewrap`
- `EventStore.PayloadProtection.Key.Invalidate`
- `EventStore.PayloadProtection.Key.Reconcile`

Exact metric instruments:

- `eventstore.payload_protection.operations` counter
- `eventstore.payload_protection.duration` histogram milliseconds
- `eventstore.payload_protection.provider_failures` counter
- `eventstore.payload_protection.key_cache_hits` counter
- `eventstore.payload_protection.key_cache_misses` counter
- `eventstore.payload_protection.key_cache_entries` up/down counter
- `eventstore.payload_protection.lifecycle_transitions` counter

Metric tags are closed low-cardinality `operation`, `result`, `reason_code`,
`backend_id`, `format_version`, and `cache_result`. Tenant/domain/aggregate,
keyRef/fingerprint, provider URI/version, payload type/path, operation/workflow/
correlation IDs, exception message, and payload size/content are forbidden metric
tags.

Engine audit records extend the existing workflow audit model with exact safe
fields: AuditId, OperationId, optional WorkflowId, optional CorrelationId,
tenant/domain/optional aggregate scope as allowed by the existing audit policy,
operation, from/to state, lifecycle/fence epoch, backend ID, format version,
key/provider **display fingerprints only**, attempt count, duration, result,
stable reason code, decision actor ID, and UTC timestamp. No raw durable/provider
key ID, state-store key, endpoint, credential, request/response body, exception/
stack, nonce/tag/ciphertext/wrapped key, plaintext, certificate/report body, or
arbitrary extension is audit-safe.

## 11. Production Backend And Custody Boundary

### 11.1 Selected resource, key, and wrap profile

The selected production backend is an **ordinary Azure Key Vault Premium vault**
(`Microsoft.KeyVault/vaults`), not a Managed HSM pool and not a generic secret
store. Its KEK is a provider-generated, non-exportable `RSA-HSM` key of exactly
3072 bits with key operations restricted to `wrapKey` and `unwrapKey`. The exact
wrap algorithm ID is `RSA-OAEP-256`; the .NET adapter uses
`KeyWrapAlgorithm.RsaOaep256`. RSA1_5, RSA-OAEP/SHA-1, software RSA, EC, an
exportable/imported private key, and local RSA unwrap are forbidden.

This profile is selected because an ordinary Premium vault supplies an
HSM-protected asymmetric KEK, versioned key URIs, Azure RBAC, private endpoints,
rotation, audit integration, and the stable Azure .NET SDK without the separate
cluster/security-domain/RBAC operating model of Managed HSM. Managed HSM is a
credible higher-isolation alternative but is not an interchangeable endpoint:
only it supports symmetric `oct-HSM`/AES-KW, and adopting it requires a new
approved adapter/profile. Standard Key Vault, DAPR secret stores, application
certificates, local files, environment secrets, and LocalDevelopment are
rejected as production key custody.

One environment owns one vault and one KEK name; production and non-production
never share a vault, managed identity, or KEK. A KEK may wrap DEKs from multiple
tenants because DEK records/AAD remain tenant scoped; an operator may choose
more vaults/KEKs for isolation, but a tenant can move only through the approved
rewrap protocol. New wraps use the one enabled current KEK version selected by a
strong metadata refresh; old records retain their exact versioned URI for
unwrap. Rotation never aliases or deletes an old version before every retained
wrapped DEK and backup obligation is closed.

### 11.2 Exact adapter options and startup probe

`EventStore:PayloadProtection:AzureKeyVault` has this closed surface:

| Field | Exact rule |
| --- | --- |
| `VaultUri` | Required absolute HTTPS ordinary-vault data-plane URI with no user-info, query, fragment, or path beyond `/`; exact sovereign-cloud suffix must match the deployment cloud. Managed-HSM suffixes are rejected. |
| `KeyName` | Required 1..127 Azure-valid characters; a name only, never a URI/version. |
| `ManagedIdentityClientId` | Optional canonical GUID selecting a user-assigned identity; absent selects system-assigned identity. Required when the host has more than one eligible identity. |
| `ExpectedKeySize` | Fixed `3072`; any other configured value is invalid. |
| `WrapAlgorithm` | Fixed `RSA-OAEP-256`; any other configured value is invalid. |
| `KeyVersionRefreshInterval` | Default five minutes; positive, maximum one hour. Refresh failure keeps historical unwrap by exact recorded version but blocks new wrap once the last strongly validated selection is older than the interval. |
| `RequestTimeout` | Default ten seconds; positive, maximum 30 seconds and also bounded by section 10.6. |

Unknown provider fields fail options validation so misspellings cannot weaken the
profile. Options contain no tenant secret, client secret, certificate, access
token, private key, wrapped DEK, or connection string.

Every non-Development environment constructs exactly
`ManagedIdentityCredential` (system- or configured user-assigned); credential
chains, Azure CLI, IDE, environment client secrets, workload fallback, and
interactive authentication are forbidden. Exact `Development` may use an
explicit developer credential chain only when connecting to the isolated real
test vault; its principal is distinct and never accepted as production proof.

Before readiness, the adapter must:

1. validate the closed URI/options/environment profile and TLS platform;
2. authenticate and `GetKey(KeyName)` through the data plane;
3. require an enabled, time-valid, 3072-bit `RSA-HSM` current version whose
   allowed operations include only/at least wrap and unwrap and whose returned
   versioned ID has the configured vault/name;
4. perform a startup capability probe by wrapping and unwrapping a fresh
   32-byte random probe with `RSA-OAEP-256`, compare in fixed time, and zero both
   input/output; retain no probe record;
5. validate the state-store capabilities and lifecycle/fence consistency; and
6. publish readiness with backend/profile/version fingerprints only.

Any mismatch fails startup. A probe is capability evidence, not production
conformance or permission to create/delete/rotate a KEK.

### 11.3 Custody, RBAC, network, and API/SDK policy

The application has a dedicated managed identity assigned at the individual KEK
scope where supported. Its only built-in data-plane role is **Key Vault Crypto
Service Encryption User** (`e147488a-f6f5-4113-8e2d-b22465e65bf6`), which reads
key metadata and performs wrap/unwrap. It has no vault control-plane role and no
key create/import/update/rotate/delete/recover/backup/restore/purge/release,
secret, certificate, role-assignment, or network permission. If organizational
policy requires a custom role, its data actions are exactly key read metadata,
wrap, and unwrap and are no broader.

Operators use separate just-in-time identities for provisioning/rotation and a
separately approved break-glass/purge path. The application never supplies or
logs credentials and never calls the Key Vault control plane. Production uses a
private endpoint, private DNS, disabled public network access, no trusted-service
bypass, outbound allowlisting to the vault data plane and Entra token endpoint,
and diagnostic logs routed to the approved security sink. TLS interception that
changes endpoint identity is forbidden.

Story 8.2 uses centrally versioned stable (non-preview) `Azure.Identity` and
`Azure.Security.KeyVault.Keys` releases that support `net10.0`. Its preflight
must reverify the official service/SDK documentation, record exact NuGet
versions and hashes, dependency lock/SBOM, Azure SDK assembly/file versions, and
the effective Key Vault REST service API version emitted by the client. Preview
service APIs, an unrecorded SDK default change, or version override is not
authorized. SDK retries are disabled or configured so the combined adapter
behavior has exactly the budget in section 10.6, not nested retry multiplication.

### 11.4 Constructive failure mapping

Classification uses Azure SDK status/error-code types and cancellation state,
never message text:

| Observed condition | Public outcome | Retry / breaker / mutation rule |
| --- | --- | --- |
| Token acquisition failure or HTTP 401 | `ProviderDenied` | Permanent for the operation; no data-plane retry or breaker trip; readiness false until identity is repaired. |
| HTTP 403 or network-policy denial | `ProviderDenied` | Permanent; no retry. A separately identified DNS/socket reachability failure remains unavailable, not denied. |
| Versioned key 404, disabled/expired/not-yet-valid key, or service deleted-key response | `MissingKey` or `KeyInvalidatedOrDeleted` only when lifecycle evidence proves deletion; otherwise `ConsistencyMismatch` | No blind retry; strong lifecycle/provider reconciliation. Never fall back to current key version. |
| 408, 429, 500, 502, 503, 504, transient socket/DNS/TLS-connect failure | `ProviderUnavailable` | Section 10.6 bounded retry/`Retry-After` and transient breaker. TLS certificate/hostname validation failure is permanent configuration/security failure and fails readiness. |
| Caller cancellation | cancellation propagates | No retry, no breaker count, owned buffers zeroed. |
| 400/unsupported algorithm/key size/type, malformed response, returned ID/version mismatch | `ConsistencyMismatch` | Permanent; no retry; readiness false for profile errors. |
| Wrap response received but key-record transaction times out | no v2 result | Reconcile operation/state; never repeat wrap as if known absent. |
| Rewrap/delete/rotation timeout | existing readable record/lifecycle remains authoritative | Reconcile exact operation and provider version; never claim completion from timeout. |

### 11.5 Deletion, retention, backup, and rollback truthfulness

Vault soft delete and purge protection are mandatory with a 90-day immutable
retention policy. They protect the shared KEK from accidental/malicious removal
but mean KEK deletion is deliberately recoverable during that period. KEK
deletion is therefore neither the per-subject erasure mechanism nor immediate
crypto-erasure evidence. It would also strand every other DEK wrapped by that
version and violate historical read/rollback retention.

Per-scope erasure deletes/invalidate all wrapped-DEK records and live cached DEK
copies under section 10. That makes online ciphertext unreadable, but a final
crypto-erasure/Parties completion claim is allowed only when the evidence ledger
also accounts for every state-store replica, export, backup, restore point,
escrow, diagnostic artifact, and disaster-recovery copy that could contain the
wrapped DEK. Each copy must be destroyed or pass its immutable expiry while the
KEK remains protected and access controlled. Unknown copies keep the workflow
pending/operator-required. Neither EventStore nor Azure can certify that one
primary-row delete erased untracked copies.

Retained KEK versions and v2 key records are mandatory for historical reads and
the post-v2 rollback in section 13. Removing them to accelerate erasure without
closing their full indexed scope is forbidden. A terminal Parties certificate
must distinguish `online access invalidated` from `all approved recoverable key
copies expired/destroyed` and cite the latter evidence before claiming completed
crypto-erasure.

### 11.6 Required real-service conformance environment

G5 uses a dedicated Azure subscription/resource group with an ordinary Premium
vault, provider-generated RSA-HSM-3072 KEK, 90-day purge protection, private
endpoint/private DNS, public access disabled, diagnostic logs, a dedicated
user-assigned managed identity, and a self-hosted ephemeral test runner inside
the VNet. No production tenant data or production key is used. Infrastructure
deployment identity is separate from the runtime identity.

The evidence run must record Azure resource IDs as redacted hashes, region,
vault SKU/resource type, returned HSM platform/key attributes and version
fingerprint, RBAC assignment ID/role/scope, network posture, SDK/API/package
versions, UTC interval, source/spec/package hashes, and cleanup/retention state.
It must persist and inspect a real wrapped-DEK record and v2 event/snapshot,
restart and read them, rotate/rewrap, and exercise 401, 403, exact-version 404,
disabled/expired key, 429 with bounded `Retry-After`, timeout, DNS/network/5xx
simulation at the adapter boundary, cancellation, open/half-open breaker,
ambiguous state mutation, stale cache plus invalidation, and post-v2 rollback.

A mock HTTP handler validates classification logic but not custody. An interface,
unit fake, emulator, LocalDevelopment/in-memory backend, ordinary software key,
public-endpoint sample, or DAPR component shape cannot satisfy this gate.

## 12. Compatibility And Typed Read Routing

### 12.1 Routing precedence

Each event or snapshot is routed independently. Mixed streams do not inherit a
mode from their first/latest record. The reader applies this precedence before
domain use, publication, projection, replay, rebuild, admin decoding, or snapshot
materialization:

1. Parse `eventstore.protection` through the existing carrier. Provider-opaque
   metadata is returned through its bounded mapped reason; bytes are not parsed
   as plaintext and no provider is called.
2. Classify reserved serialization format and bounded byte shape. Metadata,
   format, wrapper/marker shape, and selected route must agree.
3. Route v2 to the shared engine, v1 to the exact registered legacy reader,
   redacted to the redacted pass-through, and legacy/unprotected to pass-through.
4. For protected routes, authenticate before parsing/using plaintext. On a mixed
   stream, stop at the first unreadable sequence and report only its bounded
   decision; do not skip, partially apply, advance a projection checkpoint, or
   publish later events.

### 12.2 Event/read compatibility matrix

| Metadata and stored format/shape | Route | Outcome |
| --- | --- | --- |
| Metadata missing (`Legacy()`), `json`, valid JSON, no v1 marker/v2 wrapper | Legacy plaintext pass-through | Readable; returned metadata is legacy/unprotected. This is compatibility, not proof it was never sensitive. |
| Metadata missing/unprotected, non-reserved historical custom format, no detectable reserved wrapper | Legacy plaintext pass-through | Readable byte-for-byte. Reserved prefix `json+pdenc-` is never treated as custom plaintext. |
| Exact metadata v1 `Unprotected`, `json`, valid JSON, no protected marker | Unprotected pass-through | Readable. |
| Missing/unprotected metadata, exact `json-redacted`, valid JSON with no encrypted marker | Redacted pass-through | Readable redacted bytes; format stays `json-redacted`; never re-protect automatically and never claim original data is recoverable. |
| Exact Parties protected metadata v1 plus `json+pdenc-v1` and bounded `$enc` marker shape | Registered legacy reader ID `parties-pdenc-v1` | Reader decides readable or existing typed unreadable result. Shared engine does not reinterpret v1 cryptography. |
| Missing legacy metadata plus `json+pdenc-v1` and bounded v1 marker shape | Same registered legacy reader | Compatibility with current Parties behavior; absence of reader is `ProviderOpaqueUnsupportedOperation`, never plaintext. |
| Exact v2 allowlist metadata plus `json+pdenc-v2` and one or more valid `$pdenc` wrappers | Shared v2 engine | Per-wrapper bounded parse, key lookup, AAD construction, authenticated decrypt, JSON replacement; readable metadata becomes unprotected. |
| V2 metadata/format but no wrapper, v2 wrapper with missing/unprotected/v1 metadata, v1 format with v2 wrapper, `json` with any protected wrapper, or redacted format with protected marker | No provider | `BytesMetadataMismatch`. |
| Protected metadata with unknown scheme/format or reserved `json+pdenc-*` version | No provider | `ProviderOpaqueUnsupportedOperation`. |
| Carrier metadata schema above current version | No provider | `UnknownMetadataVersion`. |
| Carrier malformed/forbidden/duplicate/unknown field | No provider | `MalformedMetadata`. |
| V2 malformed/truncated/oversized/non-canonical envelope, closed-ID violation, invalid path/AAD input, or auth-tag failure | V2 parser/decrypt boundary | `BytesMetadataMismatch`; no provider text or partial plaintext. |
| V2 key record absent while lifecycle is active/unknown | Key store | `MissingKey`. |
| V2 lifecycle records an accepted invalidation/deletion at or after the record epoch | Key store | `KeyInvalidatedOrDeleted`; cache cannot override it. |
| V2 key record identity/tenant/domain/aggregate/version/epoch disagrees | No unwrap | `ConsistencyMismatch`. |
| Backend unauthenticated/forbidden | Backend | `ProviderDenied`; no exception-text parsing. |
| Backend timeout/network/throttle/5xx/open breaker | Backend | `ProviderUnavailable`, retryability determined by section 10. |
| Caller cancellation | Any | `OperationCanceledException` propagates; it is not converted to unreadable/provider failure. |

The existing reason taxonomy is sufficient; Story 8.2 does not add an enum
merely to mirror provider status codes. Safe internal reason codes may be more
specific for metrics/audit but must map constructively to the bounded public
reason. Provider exception messages, key aliases, URIs, request IDs, and payload
bytes are not classification input.

### 12.3 Snapshot matrix

| Stored snapshot | Behavior |
| --- | --- |
| `ProtectionMetadata=null`, ordinary legacy state | Treat as legacy unprotected and return state. |
| Metadata missing/unprotected but state has a v1/v2 protected wrapper shape | `ConsistencyMismatch`/`BytesMetadataMismatch`; retain snapshot and fall back to replay. |
| Exact Parties v1 metadata plus its protected snapshot wrapper | Route to `parties-pdenc-v1` snapshot reader. |
| Exact v2 metadata plus `ProtectedSnapshotPayloadV2` | Validate type/format/envelope, use AAD kind 2/path empty/ordinal 0, authenticate, then deserialize exact declared state type. |
| Unknown/malformed/opaque/unreadable protected snapshot | Record canonical snapshot-load decision, retain stored snapshot, return no snapshot so canonical event replay can continue or fail closed. |
| Corrupt unprotected legacy snapshot | Existing behavior may delete it and replay; this deletion exception never applies to protected/opaque state. |

V2 snapshot protection failure remains advisory only when **no plaintext or
partial protected snapshot is staged** and the command's protected event save
can complete independently. It skips snapshot creation and records a safe
failure. `throwOnFailure=true` still propagates. An existing unreadable protected
snapshot is retained. This preserves current availability without creating a
plaintext snapshot fallback.

## 13. Rollout, Mixed History, Downgrade, Migration, Restore, And Rollback

### 13.1 Dual-read, single-write rollout

There is no dual-write. Deployment is phased:

1. **Baseline:** no engine registration; existing no-op/Parties provider writes
   current formats.
2. **Dual-reader dark deploy:** deploy the v2-capable engine/adapter and exact
   legacy readers with `WriteMode=RegisteredLegacy` (Parties) or `ReadOnly`.
   Exercise legacy, v1, redacted, snapshot, and synthetic v2 reads; no production
   v2 write occurs.
3. **Fleet/read fence:** every serving instance publishes an immutable capability
   record containing source SHA, package hashes, normative spec digest, backend
   ID, and supported read/write versions. Operations verifies no old reader
   remains. A tenant/domain-scoped write fence is atomically advanced to v2 with
   an epoch and approval ID.
4. **V2 single write:** set `WriteMode=V2`. New selected values write only v2;
   unselected values remain ordinary `json`. Existing history is read in place.
5. **Soak/expand:** monitor bounded failures, provider/cache state, projection
   checkpoints, and persisted evidence before enabling another tenant/domain.

A v2 writer checks the current fence epoch on every protect operation and binds
it to the key record. A node missing the approved digest/capability or observing
an older/newer/ambiguous fence rejects the write. Rolling deployment must never
enable v2 while a v1-only reader can accept traffic.

### 13.2 Historical data and migration posture

- Legacy-unprotected, `json+pdenc-v1`, `json-redacted`, and v2 events remain in
  immutable history and are read by their recorded route. Engine enablement does
  not scan/rewrite them.
- In-place event re-encryption or format rewrite is forbidden. A domain may
  append a new policy-approved state event, but the old immutable event remains
  governed by retention and its original reader.
- A snapshot is an advisory cache and may be rebuilt from canonical replay into
  v2 after all source events are readable. Replacement never proves historical
  event migration and never deletes an unreadable protected snapshot merely to
  hide the failure.
- KEK rewrap changes only the atomic wrapped-DEK record as section 8.3 specifies.
  Optional DEK re-encryption of persisted events is not authorized by this ADR.
- Legacy readers and their fixtures remain supported until a later approved ADR
  proves no retained history/backup/consumer needs them. Time alone is not proof.

### 13.3 Downgrade rule and irreversible watermark

Before returning the first v2 payload, the engine conditionally advances the
tenant/domain fence record to durable `HighestWrittenFormat=v2` and binds its
epoch to the `Reserved` key record. This state-store transaction precedes the
separate actor save; if that save later fails, the conservative watermark may
advance without a committed v2 event. It never decreases, including after
erasure, restore, or rollback. A binary/package set that cannot read v2 must
refuse startup or tenant traffic when this watermark exists.

After a v2 write, **full downgrade to a v1-only/no-op EventStore is impossible**
without stranding immutable history and is forbidden. A supported rollback
means changing the writer while retaining the exact v2 reader, Azure backend,
key records, and lifecycle/restore enforcement. `Mode=Disabled`, removal of the
engine/adapter, deletion of v2 keys, or treating v2 as opaque is not rollback.

### 13.4 Exercised post-v2-write rollback

The Parties rollback path is exact:

1. Stop/drain new commands for the scoped tenant/domain and reconcile ambiguous
   lifecycle/provider mutations.
2. Verify the last-known-good v2-capable release, spec/fixture digests, Azure
   read backend, retained `parties-pdenc-v1` reader/writer, and all v2 key records.
3. Atomically advance the write fence epoch from `V2` to
   `RegisteredLegacy(parties-pdenc-v1)` with named operator approval. Never
   decrement the highest-written watermark.
4. Deploy/configure `Mode=Enabled`, the Azure backend for v2 reads, and the
   exact retained Parties legacy provider for new v1 writes. No-op/plaintext is
   not a fallback.
5. Read a persisted mixed stream containing legacy, v1, redacted where
   applicable, and a real v2 event; rehydrate domain state; execute a command
   that appends a v1 event; persist, publish, project/rebuild, query/admin-read,
   and restart/re-read it. Assert state-store end state, key records, provider
   key identity, formats by sequence, checkpoint, and no-leak evidence.
6. Exercise a protected v2 snapshot plus replay fallback and prove that an
   unreadable protected snapshot is retained.
7. Keep the v2 engine/adapter and keys installed indefinitely for retained v2
   history. Returning to v2 writes requires a new fence epoch and fresh preflight.

This rollback addresses engine/release defects. It cannot bypass an Azure
outage: v2 history still requires its approved read backend and therefore fails
closed while unavailable.

For domains without an approved legacy writer, rollback is `ReadOnly` and new
protected writes stop until v2 is repaired. Writing plaintext to preserve
availability is forbidden.

### 13.5 Restore and irreversible workflow preservation

Restore admission runs before any read/write service becomes ready. It compares
the backup manifest's highest format/fence/lifecycle epochs and key-record set
with the live irreversible watermark, invalidation/deletion audit, and provider
state. A backup that could resurrect an invalidated/deleted key, lower a format
or lifecycle watermark, omit required v2 keys, reference a different provider
resource/version, or create ambiguity enters `Quarantined` or
`OperatorDecisionRequired`; it never auto-repairs or activates.

Approved restoration cannot reverse `Invalidated`, `Deleted`, or `Completed`
crypto-shredding transitions. Protected unreadable events remain immutable and
fail closed. Protected unreadable snapshots remain stored and are bypassed for
replay. Any operator decision is attributable, bounded, and audited through the
existing restored-backup workflow contracts.

## 14. Threat Model And Misuse Cases

### 14.1 Assets, actors, and attacker capabilities

Protected assets are event/snapshot plaintext, DEKs, KEK use authority, tenant/
aggregate identity and policy selections, wrapped-key lifecycle state, immutable
history readability, irreversible erasure evidence, and the integrity/availability
of projection/replay/publish/admin paths. Section 4 trust boundaries are the
data-flow model.

Actors are: an authenticated tenant caller; domain policy code; EventStore
runtime; state/event-store operators; Azure/identity/network operators; a
Security/Operations break-glass actor; package/release infrastructure; Parties
governance; and readers/reviewers of diagnostics/evidence. An attacker may
control request JSON and metadata, replay/copy/tamper stored bytes, know another
tenant's IDs/ciphertext/keyRef, race processes, crash/restart/clone hosts, induce
provider/state-store faults, obtain log/evidence access, restore old backups,
deploy a stale binary/configuration, or compromise the event/state store alone.

The model does not claim confidentiality after simultaneous compromise of
ciphertext/wrapped records and a currently authorized KEK principal, after
plaintext reaches authorized domain code, or against physical/provider/OS
memory compromise. Those residuals require identity, network, process, custody,
retention, and incident controls; they never justify plaintext fallback.

### 14.2 Threat and misuse register

| ID / threat or misuse | Required prevention/detection | Bounded failure | Residual risk / owner / verification |
| --- | --- | --- | --- |
| T01 Cross-tenant/domain/aggregate ciphertext or key-record substitution | All identity fields are trusted runtime inputs in AAD; scope-addressed records are compared to full identity before unwrap. | `ConsistencyMismatch` or tag failure; no plaintext/provider detail. | Authorized identity/state compromise can rewrite both; Security/Platform own T01 mutation and compromised-store review. |
| T02 Event/snapshot type, property path, ordinal, format, keyRef, or DEK-version substitution | Closed envelope plus injective AAD and exact runtime type/path reconstruction. | `BytesMetadataMismatch`/`ConsistencyMismatch`. | A caller able to influence trusted identity/type validation is outside codec control; Platform owns field-by-field mutations. |
| T03 Nonce reuse, duplicate ordinal, DEK reuse across retry/restart/clone, or budget exhaustion | Fresh CSPRNG DEK/reference per attempt, deterministic unique ordinal nonce, full path-set validation, 65,536 bound, durable reservation. | Protect fails before AES or payload persistence. | Catastrophic RNG/platform compromise; Security owns concurrency/crash/injected-RNG tests and platform gate. |
| T04 JSON Pointer/Unicode/duplicate-name ambiguity or policy traversal confusion | Strict UTF-8/NFC/RFC6901 profile, duplicate rejection, deterministic sorting, cycle detection, typed-to-serialized value agreement. | Protection/parser failure; no key when selection unresolved. | Serializer evolution may change legitimate names; Platform/Parties own compatibility fixtures and approval for changes. |
| T05 Metadata tampering, encoded/obfuscated secret fields, wrapper smuggling, or version confusion | Exact metadata/wrapper allowlists, reserved-format precedence, duplicate/unknown rejection, no arbitrary extensions. | `MalformedMetadata`, `UnknownMetadataVersion`, `ProviderOpaqueUnsupportedOperation`, or mismatch before provider. | Novel carrier parser defects; Security owns mutation corpus and leak sentinel. |
| T06 Malformed, truncated, extended, integer-overflow, deeply nested, or oversized envelope/JSON DoS | Pre-decode text cap, checked fixed header/total lengths, 1 MiB value/AAD bounds, bounded JSON depth/object graph and no lookup before parse validation. | `BytesMetadataMismatch`; bounded allocation/CPU. | Many individually bounded requests can exhaust service; Operations owns external rate/resource limits and load tests. |
| T07 Tag/ciphertext/AAD tamper or padding-oracle style observation | AES-GCM authenticates before parse/use; one bounded public failure and constant-shape safe output; no partial plaintext. | Plaintext-or-failure only. | Timing across state/provider routes may reveal broad availability/lifecycle class; Security owns timing review and public taxonomy approval. |
| T08 Missing/deleted/disabled/wrong-version KEK or wrapped DEK | Exact versioned URI, lifecycle proof, no use-current-version fallback, restore admission. | Missing/deleted/consistency outcome; no plaintext. | Key loss irreversibly loses history; Provider/Ops own backup/rotation/retention and disaster rehearsal. |
| T09 Provider identity denial, outage, throttle, network partition, TLS substitution, or retry storm | Deterministic managed identity, private endpoint/TLS, least privilege, bounded retry/jitter/breaker/single-flight. | Denied/unavailable; no no-op/alternate backend. | Shared backend outage blocks protected reads/writes; Ops owns SLO/capacity/runbook and injected failures. |
| T10 Stale cache or in-flight read bypasses accepted erasure | Strong epoch read, distributed lease before cache, invalidation blocks leases, drain/acks/zero before accepted deletion. | Read blocked/pending; completion withheld on uncertainty. | Host suspension past TTL or unknown replicas require conservative operator state; Platform/Ops own race/partition tests. |
| T11 Crash/timeout between wrap, reservation, event save, activation, rewrap, or delete | Stable operation IDs, durable phases, record-before-payload ordering, reconciliation, retained Reserved readability, proof before orphan cleanup. | No plaintext; ambiguous stays pending/reconcile. | Permanent control-store loss can leave unprovable state; Platform/Ops own crash-point matrix and repair policy. |
| T12 Backup/restore resurrects invalidated key or lowers v2/lifecycle fence | Monotonic external watermark/epoch comparison before readiness; quarantine/operator decision; backup-copy erasure ledger. | Restore quarantined; traffic not served. | Unknown/unindexed backups delay final erasure; Ops/Parties own inventory and certificate truthfulness. |
| T13 Downgrade, partial fleet, malicious config, or package substitution writes unreadable/plaintext history | Content-bound approved digest, package hashes/capability records, write fence, highest-format watermark, fail-start matrix, retained v2 reader. | Startup/write rejected. | Compromised release/control plane can forge evidence; Release/Security own signing/provenance and rollback rehearsal. |
| T14 Policy omission, exception, malicious domain adapter, or overbroad selection | Monotonic positive policies, built-in attribute cannot be overridden, deterministic registered IDs, exceptions fail writes; Parties keeps legal policy. | No selected fields leaves bytes unchanged; policy error never plaintext fallback. | Incorrect annotations/abstention can leave intended data unprotected; Parties owns policy tests/review and classification inventory. |
| T15 Key ID, provider error, payload, credential, or tenant data leaks through diagnostics/evidence/admin/export | Constructive allowlists in section 15, low-cardinality tags, bounded reason codes, redacted fingerprints, leak sentinel across all surfaces. | Unsafe output construction fails test/operation; raw exception never crosses boundary. | Memory/crash dumps and downstream authorized plaintext misuse remain; Security/Ops/domain owners control those systems. |
| T16 Excessive runtime identity can unwrap broadly or administer KEKs | Key-scoped wrap/unwrap/read-only RBAC, no control plane, separate JIT operator and purge identity, audit/anomaly monitoring. | Unauthorized operation denied. | Compromised runtime may unwrap any record within its scope while access remains; Identity/Ops own rapid revocation and scope reduction. |
| T17 Rewrap swaps DEK, wrong KEK version, or destroys old version early | Unwrap-old/wrap-new fixed-time DEK comparison where possible, conditional record integrity/ETag, envelope unchanged, old version retained until verified. | Consistency failure; old Active record remains. | Provider/operator malicious substitution with valid privileges; Security/Ops own separation of duties and sample/full verification. |
| T18 False readiness, G5, erasure, or rollback claim based on mocks/status only | Evidence must inspect real persisted payload/key/provider/fence/cache state and exact hashes; approvals are content-bound. | Gate remains not authorized/incomplete. | Evidence pipeline compromise; independent reviewer and Release own reproduction/provenance. |

Threat acceptance is not implicit. Every residual listed above must be accepted by
the named owner in the content-bound approval record or Story 8.2 remains blocked.

## 15. No-Leak Contract And Verification Matrix

### 15.1 Plaintext and safe-output invariant

Authenticated unprotect is binary: it returns the complete authenticated
plaintext value/state or one bounded typed failure with no plaintext. Before
`AesGcm.Decrypt` verifies the tag, plaintext bytes MUST NOT be parsed, decoded to
string, materialized as JSON/CLR state, passed to policy/domain code, published,
projected, checkpointed, cached, logged, measured, exported, inspected by admin,
or used to construct an error. An implementation may allocate the destination
buffer required by the API, but it remains engine-owned, unobserved, and is
zeroed on failure.

Safe output is constructed from a per-surface allowlist. Broad object/exception/
request serialization followed by redaction is forbidden. Authorization to an
admin, operator, privacy workflow, or evidence reviewer does not relax the
allowlist.

### 15.2 Constructive no-leak matrix

| Surface | Exact allowed classes | Always forbidden | Future proof |
| --- | --- | --- | --- |
| Source-generated logs | Stable event ID/name, operation, bounded result/reason, backend ID, format, duration bucket, redacted display fingerprint, optional existing policy-approved correlation/workflow ID | Payload/plaintext/ciphertext/wrapped key/nonce/tag/AAD/path/type/keyRef/provider URI or error/request ID, credential/token, state key, arbitrary exception/`ToString()` | Logger capture plus leak sentinel on success and every fault. |
| Activities/traces | Section 10 activity names and closed low-card tags; trace/span IDs | Event/aggregate/tenant identifiers unless existing tracing policy explicitly hashes them; all cryptographic/provider/payload fields and exception messages | In-memory exporter assertions including baggage/events/status descriptions. |
| Metrics | Exact instruments/tags in section 10.8 | High-cardinality scope, key, path/type, operation/correlation ID, endpoint, payload bytes/size, exception text | Meter listener asserts tag-name/value allowlist and cardinality corpus. |
| Exceptions | Public exception type plus bounded enum/reason code and safe generic message | Inner provider/JSON/crypto exception crossing boundary; raw input, IDs, URI, response headers/body | Traverse complete exception graph, `Data`, stack formatting, and serialized ProblemDetails. |
| HTTP ProblemDetails/admin | Existing constructive type/title/status plus canonical readability decision/stage and allowlisted safe extensions | Raw bytes/metadata/provider response, key alias/ref/version/URI/fingerprint unless explicitly allowed, exception/trace dump, plaintext hint | Contract/integration snapshots for every route and hostile extension names/encodings. |
| Audit/evidence | Section 10.8 audit fields; hashes/counts/resource fingerprints; exact commands/versions; pass/fail | Fixture secrets outside test-only files, live key/credential, provider full ID, payload or reversible encoded/hashed low-entropy personal data, response dump | Schema validator plus evidence-bundle recursive leak scan. |
| Event/API exports | Authenticated, authorized domain result with `Unprotected()` metadata, or bounded unreadable record | Stored protected blob passed as plaintext; partial/decrypted-before-auth value; key/lifecycle internals | Stream/replay/export tests for mixed history and every unreadable outcome. |
| Art.20 portability | Parties-approved authenticated domain projection only | Generic EventStore invention of legal scope; encrypted blob/key/provider details; unreadable data silently omitted | Parties consumer proof with counts and explicit bounded failure. |
| Art.30 processing record | Parties-approved process metadata and safe engine operation/result categories | Subject payload, DEK/KEK identifiers, provider details, arbitrary diagnostics | Parties schema/approval and leak sentinel. |
| Erasure certificates/reports | Parties-approved workflow ID, scope allowed by policy, stages/timestamps/counts, online-invalidation versus all-copy completion wording | Key material/URI/ref, wrapped bytes, payload, provider response, false finality while backups remain | Parties certificate/report golden plus backup-ledger and no-leak assertions. |
| Crash dumps/support bundles | Disabled/restricted by Operations; only separately approved sanitized artifacts | Process memory, heap/core dump, environment, token cache, raw logs/state/event records | Deployment-policy audit and hostile support-bundle inspection. |

`ProtectedDataLeakSentinel` is extended, not replaced. Its corpus must include the
exact plaintext, every JSON scalar/subsequence, ciphertext, wrapper/base64url,
nonce/tag/AAD, wrapped DEK, full and partial keyRef/provider URI/version, state
keys, credentials/tokens, percent/JSON/base64/base64url/hex encodings, Unicode
normalization/confusables, reversed/chunked spellings, Azure request/error IDs,
and exception messages. Tests seed unique canaries rather than common words and
scan structured values before rendering plus UTF-8/UTF-16 rendered artifacts.
Hashing is not redaction for low-entropy personal data.

### 15.3 Fixed key-record companion for G-001

The provider wrap is randomized RSA-OAEP and therefore its ciphertext is not a
byte-stable owner golden. G-001 instead has this exact structural companion;
real conformance must prove a fresh 384-byte wrapped value unwraps to the fixed
32-byte DEK without logging either:

| Field | Exact expected value |
| --- | --- |
| Scope identity | tenant `tenant-a`, domain `parties`, aggregate `party-01` |
| Key reference / DEK version | `01J00000000000000000000000` / `1` |
| State / lifecycle epoch | `Reserved` / `1` before persist; `Active` / `1` after completion |
| Backend ID | `azure-key-vault-rsa-hsm-v1` |
| Provider fixture URI | `https://hx-pp-golden.vault.azure.net/keys/eventstore-pp/00000000000000000000000000000001` (test identity only) |
| Provider display fingerprint | `35d27f91320be06c` (first 16 lowercase hex characters of SHA-256 over that exact URI; test recomputes it) |
| Wrap algorithm / wrapped length | `RSA-OAEP-256` / exactly 384 bytes |
| Operation ID | canonical test ULID `01J00000000000000000000001` |

The real provider record captures the random wrapped-byte SHA-256 as run
evidence; no fixed equality is expected between runs. A deterministic stub may
exercise record serialization but cannot claim RSA-OAEP or production evidence.

### 15.4 Normative vector families

Every row is implemented in Story 8.2 as named test cases/fixtures. Unless a row
says otherwise, expected behavior is: no plaintext/output mutation, no retry,
owned buffers zeroed, bounded safe diagnostics, and no provider call if the
failure is locally decidable.

| IDs | Inputs/mutations and boundary | Expected result and evidence |
| --- | --- | --- |
| V001-V003 positive | G-001 encrypt/decrypt in owner implementation; exact Node/Python reproduction; NIST CAVP AES-256-GCM Count 0 | Exact AAD/cipher/tag/envelope/wrapper hashes; complete plaintext only after auth; tool/version/command output recorded. |
| V004-V009 carrier/parser | One-bit change at every header field; truncate at every fixed/variable boundary; append byte; invalid/padded/non-canonical base64url; unknown IDs/flags; declared 0, max, max+1, `uint32` max and actual mismatch | Mismatch/opaque as section 12; bounded allocation, zero Key Vault/state lookups for invalid local grammar, no crash/overflow. |
| V010-V017 crypto mutation | Flip each bit of nonce, tag, ciphertext; substitute algorithm/version/keyRef/DEK version/ordinal; correct ciphertext with wrong AAD | Authentication/mismatch only, no partial JSON/string, destination zeroed; exact-version lookup only after valid grammar. |
| V018-V030 AAD identity | Change/missing/null/empty tenant, domain, aggregate, type, path, format, keyRef/version/ordinal; delimiter-bearing values; case change; composed/decomposed Unicode; NUL/control/unpaired surrogate; total 4096 and 4097 | Strict accept only valid exact boundary; every substitution fails auth/validation and cannot cross scope. |
| V031-V040 JSON/path/policy | `~0`/`~1`, invalid `~`, `/0` versus `/00`/`/-`, arrays, duplicate JSON names, unresolved/scalar traversal, ancestor+descendant, repeated pointer, cycles, throwing getter/policy, null versus empty selected value | Deterministic UTF-8 path order/ordinal; invalid selection fails before key creation; null skipped; empty protected; no domain assembly dependency. |
| V041-V048 nonce/concurrency | 0, 1, 65,535, 65,536, and 65,537 selected values; parallel writers; forced keyRef collision; injected repeated DEK; process crash/restart and cloned instance | Ordinals/nonces unique through budget; 65,537 and any uncertainty fail before AES; retry uses wholly fresh DEK/ref; reservations prove crash posture. |
| V049-V058 create/lifecycle | Crash/timeout after wrap, after Reserved transaction, after encryption return, before/after actor save, before activation; orphan at 23:59/24:00; duplicate operation; record/index disagreement | No payload before durable record; stored Reserved readable/reconciled; cleanup only with no-reference proof; ambiguity stays pending; no plaintext fallback. |
| V059-V068 rotation/deletion | Latest-version new wrap, old exact-version read, KEK rewrap unchanged envelope, attempted DEK swap, old KEK disabled/deleted, invalidate during reads, lease expiry/ack loss, denied deletion, recovered key, backup restored after erasure | Correct old read/rewrap identity; invalidation blocks new leases/cache; no completed deletion/erasure on uncertainty; restore quarantined; immutable event unchanged. |
| V069-V078 cache/memory | Hit/miss/single-flight/thundering herd; TTL/size edges; stale epoch; pub/sub loss; waiter cancellation; global cancellation; eviction/disposal; success/auth/provider/cancel/rotation/erasure zeroing | Strong state+lease precede every hit; one unwrap; no failed entry; all engine-owned DEK/plaintext arrays observed zero at release; claim limited to owned buffers. |
| V079-V091 Azure failures | Real success/restart; token failure/401; 403; exact-version 404; disabled/expired key; 408/429 (`Retry-After` negative, 0..2s, >2s); timeout/socket/DNS/500/502/503/504; TLS hostname failure; caller cancel | Exact section 11 mapping; max 3 attempts/10s, 200/500ms jitter, no nested SDK retries; cancellation propagates; provider text absent. |
| V092-V097 breaker/mutation | Five transients/30s, open rejection, concurrent half-open, successful/failed probes, doubled interval cap, timeout during rewrap/delete | Exact state/timing with one probe; no alternate backend; ambiguous mutation reconciles operation rather than blind replay. |
| V098-V106 startup/config | Registration omitted; Disabled with stale config; Enabled missing/unknown; LocalDevelopment in absent/Development/Test/Staging/Production/custom; unsupported AesGcm; wrong vault suffix/key type/size/ops/algorithm/RBAC/network | Exact section 5/11 start or fail-start result; absent is Production; no readiness/G5 claim from no-op/LocalDev/mock. |
| V107-V119 compatibility | Legacy missing metadata, explicit unprotected, custom format, redacted, v1 with/without metadata, v2, unknown reserved version, malformed metadata, shape/format disagreement, protected/unreadable snapshots, mixed sequence failure | Exact routing/typed outcomes; per-record route; authenticate before use; no checkpoint/publish after first unreadable; protected snapshot retained. |
| V120-V126 rollout/rollback | Partial fleet, stale capability/spec/package hash, v2 fence race, first real v2 write, attempt full no-op/v1 downgrade, seven-step Parties rollback, ReadOnly domain rollback | Stale writer/startup rejected; watermark monotonic; v2 remains readable; persisted mixed history and subsequent v1 write/restart/project proof; never plaintext fallback. |
| V127-V134 no-leak | Each success/failure across all section 15.2 surfaces; hostile metadata/error/URI/canary encodings; authorized admin/export/Art.20/Art.30/certificate/report; support bundle | Only constructive allowlists; recursive structured/rendered scans find none of the sentinel corpus; certificate does not overclaim backup erasure. |
| V135-V138 limits/load | Envelope/value/AAD/path/graph depth/index page/cache/concurrent request exact maxima and max+1; many bounded hostile inputs | Exact accept/reject boundaries, no unbounded allocation/provider amplification, stable service resource limits/load evidence. |

Mutation tests must demonstrate that each named field is independently bound;
sampling one representative bit is insufficient for fixed headers/tags. Property-
based fuzzing supplements but never replaces the named corpus. Every failure test
records provider/state call counts, retry timing, buffer-zero observation method,
typed outcome, and leak-scan disposition.

## 16. Story 8.2 Implementation, Release, And Consumer Handoff

### 16.1 Change map (planning only)

Story 8.2 must refine this inventory against its exact authorized baseline, but
may not silently move a concern across the frozen boundaries:

| Class | Likely NEW/UPDATE artifacts | Frozen decisions implemented / evidence |
| --- | --- | --- |
| Contracts UPDATE | One type per file under `src/Hexalith.EventStore.Contracts/Security`: `PersonalDataAttribute`, `PayloadProtectionPayloadKind`, `PersonalDataPolicyDecision`, `PersonalDataPolicyContext`, `IPersonalDataPolicy`, `PayloadErasureState*`, `IErasureStateProvider`, stable v2 snapshot carrier/context/completion contracts; additive defaults on `IEventPayloadProtectionService` where compatibility requires | Sections 6-7 and 9; API approval/source compatibility and contract tests. Existing providers compile unchanged. |
| Engine NEW | `src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj`; one-type files for codec/AAD/path/policy traversal, options/DI, CSPRNG/AES, backend SPI, DAPR scoped record/index/fence/lease/operation/audit stores, lifecycle/cache/retry/breaker/reconciler, safe logs/telemetry | Sections 5-10 and 12-15; no Azure/domain dependency; analyzers/XML docs/`ConfigureAwait(false)`; owner goldens and named vectors. |
| Azure adapter NEW | `src/Hexalith.EventStore.PayloadProtection.AzureKeyVault/Hexalith.EventStore.PayloadProtection.AzureKeyVault.csproj`; options/validator, managed-identity factory, exact-version `CryptographyClient`, response classifier, startup capability probe, source-generated logs | Section 11; only stable centrally versioned Azure dependencies; mock classification plus real-vault conformance. |
| Server UPDATE | `ServiceCollectionExtensions`, `EventPersister`, `SnapshotManager`, and the smallest existing readability/restore boundary changes necessary for additive context, Reserved-to-Active completion, v2 fence/watermark, deterministic snapshot carrier, and exact typed routing | Sections 5, 8.2, 10.3, 12-13; preserve all-protection-before-write, event atomic flush, snapshot advisory semantics, fail-closed publish/rehydrate/projection/admin paths. |
| Testing UPDATE | Extend `src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs`; additive provider/state fault fixtures only | Section 15.2; reusable canary/structured-output inspection without embedding production behavior in fakes. |
| Unit tests NEW/UPDATE | New `tests/Hexalith.EventStore.PayloadProtection.Tests` and `tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests`; extend Contracts/Server/Client/Testing/Admin tests listed in Appendix A | Sections 6-15; G-001, V004-V138, API/metadata/readability/snapshot/persist/no-leak regression. xUnit v3, Shouldly, NSubstitute. |
| Real integration NEW/UPDATE | Adapter integration collection under `tests/Hexalith.EventStore.IntegrationTests/Security/PayloadProtection`; isolated Azure fixture/provisioning descriptor outside unit-test defaults; extend Aspire/DAPR topology only where the real test requires state capabilities | Sections 10-11 and 13-15; real persisted v2 event/snapshot/key/index/fence/provider evidence, restart, rewrap, failures, cache invalidation, restore, rollback. No secret/resource ID committed. |
| Solution/packages UPDATE | `Hexalith.EventStore.slnx`, `Directory.Packages.props`, project references, package metadata, `tools/release-packages.json`, validation scripts/tests, pack workflow inputs, SBOM/provenance | Sections 5.1 and 16.3; exactly two packable projects; atomic 14-to-16 inventory; package-only proof. |
| Documentation UPDATE | `docs/guides/payload-protection-and-crypto-shredding.md`, generated package inventory/readme/release notes and operator deployment/rotation/outage/restore/rollback runbooks at repository-standard approved paths | Sections 4-15; describe only implemented/proven behavior, distinguish no-op/configured/conformant/G5 states, link exact spec digest. |
| Parties consumer (separate authorized work) | EventStore attribute/policy and erasure adapters, retained `parties-pdenc-v1` reader/writer, dual-provider DI/fence, consumer fixtures/tests/runbook/certificate wording in exact Parties commit/PR | Sections 9, 11.5, 12-13, Appendix B; no direct EventStore-to-Parties dependency and no deletion of legacy path before G5. |

No AppHost/DAPR/deployment edit is automatic. Story 8.2 may add only the state
capability and isolated integration resources proven necessary by sections 10
and 11; production provisioning remains operator/IaC work under separate
authority. No Story 8.1 planning filename is a license to create credentials or
Azure resources.

### 16.2 Required implementation sequence and spec citations

Every Story 8.2 task/PR description and material code file must cite the exact
approved normative digest and the section(s) it implements. At minimum, work is
ordered behind these gates:

1. Recompute the spec digest/source identities and reverify living sources/SDK;
   mismatch stops all implementation.
2. Add compile-compatible Contracts and owner golden fixtures (sections 6-9,
   15.3); contract/API review must pass before engine wiring.
3. Implement strict parsers/AAD/policy/crypto with local negative vectors
   (sections 6-9, 14-15) before persistence or DI enablement.
4. Implement state hierarchy/reservation/lifecycle/cache/resilience/audit
   (section 10) with crash/fault tests before a real provider.
5. Implement the Azure adapter/profile and real isolated conformance (section
   11); a mock-only green build cannot advance the gate.
6. Wire Server additive context/completion/fence/snapshot/read routes (sections
   10, 12-13) and run all existing plus new project suites individually.
7. Produce package-only consumer and real persisted G5/Parties dual-provider/
   rollback evidence before release or legacy-path removal.

Source files that implement multiple concerns cite all applicable sections in
their XML/design documentation; tests encode vector IDs in names/traits so a
reviewer can trace every row. No implementation may resolve an ambiguity by
choosing a convenient default. A normative gap returns to this ADR, invalidates
approval after any content change, and blocks code depending on it.

### 16.3 Atomic package and provenance gate

The release inventory remains the manifest-driven **14 packages** until both
approved packable projects exist and pass their tests. The same Story 8.2 change
then atomically:

- adds `Hexalith.EventStore.PayloadProtection` and
  `Hexalith.EventStore.PayloadProtection.AzureKeyVault` to
  `tools/release-packages.json` (exact total 16), solution/project/package
  references, inventory assertions/guidance, pack validation, and release notes;
- keeps every `.csproj` versionless and pins Azure dependencies centrally;
- records source commit, approved spec digest, package IDs/versions, `.nupkg` and
  symbol-package SHA-256, dependency lock, license/vulnerability scan, SBOM and
  signed provenance/attestation for all released artifacts; and
- installs the resulting `.nupkg` files into a clean package-only consumer with
  no source/project references, restores from the staged feed, builds, starts in
  Disabled and valid/invalid Enabled matrices, and executes G-001 plus one real
  Azure wrap/persist/restart/read path.

A 15-package intermediate manifest, source-only success, locally substituted
project reference, absent adapter, mismatched package hash, or SBOM/provenance
gap blocks release. The two packages version together for the first release;
later independent versions require compatibility policy approval.

### 16.4 Evidence packet and G5 closure

The immutable evidence packet is indexed by source SHA and normative spec
digest and contains:

1. owner golden/fixture bytes, hashes and Node/Python/.NET reproduction outputs;
2. per-project restore/build/test commands, exact SDK/runtime/package versions,
   test counts and V001-V138 mapping;
3. real Azure environment descriptor and redacted provider/RBAC/network/key
   state, diagnostic-log correlation, persisted event/snapshot/key/index/fence/
   operation state before/after restart/rotation/invalidation;
4. leak-sentinel reports for structured and rendered outputs across every
   section 15 surface;
5. package inventory, package-only consumer, SBOM, signature/provenance and
   vulnerability/license results;
6. Parties exact source/package hashes, v1 fixtures, policy-selection
   equivalence, erasure-state adapter, v1/v2 dual readers, v2 single writer,
   protected snapshot/replay, and certificate/report wording; and
7. an exercised post-v2 rollback transcript following section 13.4, including
   a persisted mixed stream, v1 write after rollback, publish/projection/rebuild/
   admin/restart results, monotonic watermark/fence, provider/key state, and no
   plaintext fallback/leak.

Evidence records exact UTC times, commands, tool/library/service API versions,
result counts, artifact hashes/paths, limitations, reviewer identity, findings,
and disposition. HTTP success, mock invocation, interface shape, configuration,
or screenshots alone are insufficient. G5 closes only after EventStore, Azure,
release, independent test/security, Operations, and Parties evidence all bind to
the same artifacts. Until then the feature may be experimental but cannot be
called available, production-ready, erasure-complete, or safe to remove the
Parties legacy provider/DI path.

## 17. ADR Disposition, Open Decisions, And Authorization Rule

### 17.1 Disposition and accepted decisions

ADR disposition is **Proposed — technically specified, not approved**. The
accepted scope/non-goals are sections 2 and 4. Subject to content-bound approval,
this ADR selects:

- EventStore ownership of one optional provider-neutral engine and one ordinary
  Premium Azure Key Vault companion adapter while Parties retains legal/domain
  policy and its v1 rollback path;
- the exact binary `pdenc-v2`/AES-256-GCM/fresh-DEK ordinal-nonce/AAD/RFC6901/
  metadata contracts in sections 6-8;
- deterministic monotonic policy discovery and generic erasure-state contracts;
- a separately durable, scope-indexed wrapped-DEK lifecycle with ordered
  reservation, exact identities, leases/epochs, bounded cache/resilience,
  reconciliation, telemetry/audit and owned-buffer zeroing;
- RSA-HSM-3072/RSA-OAEP-256 in an ordinary Premium vault with managed identity,
  key-scoped wrap/unwrap RBAC, private networking, 90-day purge protection and
  backup-aware erasure evidence;
- exact historical per-record routing, dual-read/single-write rollout,
  immutable-history posture, irreversible v2 watermark, retained v2 reader and
  exercised Parties post-v2-write rollback; and
- the threat/no-leak/vector/evidence/package/consumer gates in sections 14-16.

No runtime implementation, package availability, production enablement, G5,
erasure completion, or Parties migration is accepted by this document alone.

### 17.2 Rejected alternatives

| Alternative | Rejection reason |
| --- | --- |
| Keep the reusable engine/key lifecycle only in Parties | Violates AD-23, duplicates durable security mechanics, and cannot supply an EventStore owner golden/G5 boundary. |
| Put Azure SDK code in Contracts, Server, or the engine package | Reverses dependency/custody boundaries and imposes provider dependencies on no-op/other consumers. |
| Ship only one new package or hide the adapter in an existing package | Makes production capability and dependency/release provenance ambiguous. |
| Select Standard/software Key Vault, a secret store, certificate/private key, LocalDevelopment, or mock as production | Does not meet the selected HSM-custody and real-service conformance profile. |
| Select Managed HSM/oct-HSM/AES-KW as interchangeable | It is a distinct resource, symmetric-key profile and operating/RBAC model; it needs a new approved adapter/profile. |
| Extend/reinterpret `pdenc-v1` or derive v2 behavior from marker shape | V1 has different paths/metadata and no AAD; reinterpretation would strand or misauthenticate history. |
| Delimiter/JSON AAD, CLR property paths, normalization-on-read, or ambiguous arrays | Not injective/byte-stable enough for durable authentication. |
| Shared long-lived DEK with random nonces/counter state | Creates cross-process collision/counter recovery risk; fresh per-payload DEKs give a closed invocation proof. |
| Put KEK version in inner AAD or wrapped DEK/provider URI in the event envelope/metadata | Prevents rewrap-only rotation or leaks provider/custody internals into immutable history. |
| Claim a cross-store atomic actor/key save | The current hook precedes actor persistence and the stores are distinct; ordered durable reservation and reconciliation are honest. |
| Prefix scans, cache-only erasure checks, pub/sub-only invalidation, or delete-on-TTL | Cannot prove scope completeness or prevent stale-key use after accepted erasure. |
| Retry every error, silently choose current KEK/another backend, or fall back to no-op/plaintext | Converts denial/tamper/outage into unreadable or exposed durable history. |
| Dual-write v1+v2, in-place event rewrite, or full v1/no-op downgrade after v2 | Increases sensitive copies, violates immutable history, or strands v2 records. |
| Delete the shared KEK per subject or call primary-row deletion completed crypto-erasure | Breaks unrelated history and ignores soft-delete, backups, replicas, caches and restore points. |
| Broadly serialize diagnostics/evidence and scrub strings afterward | Cannot constructively prove absence of encoded, nested, renamed or provider-originated secrets/payloads. |
| Authorize from mocks, HTTP status, interfaces, screenshots, or source-project consumers | Does not prove real custody, persisted state, package provenance, lifecycle behavior, rollback, or no-leak boundaries. |

### 17.3 Known limitations and migration posture

- Story 8.1 is a specification: the engine, contracts, state-store capabilities,
  Azure profile, vectors and rollback have not been implemented or independently
  proven. Existing production behavior is unchanged.
- The current protection hook needs additive context/completion/fence contracts;
  exact source compatibility must be demonstrated in Story 8.2.
- V2 protects valid JSON values up to 1 MiB and at most 65,536 selected fields;
  arbitrary binary field encryption and larger values need another format.
- Snapshots are whole-state protected when any selected value exists; this may
  cost more than field protection but avoids materialization ambiguity.
- Historical v1 retains its lack of AAD and original path rules. It is read only
  by the pinned legacy reader and cannot be upgraded in place.
- A shared environment KEK has an availability/authorized-principal blast
  radius. Per-tenant vault/KEK isolation is permitted through the same profile,
  but migration requires verified rewrap and operator capacity/cost ownership.
- Azure latency, cost, quotas, regional/network/identity outages and exact stable
  SDK behavior require real-environment measurement. Protected operations fail
  closed during unavailable reads/writes.
- Managed-memory zeroing is limited to engine-owned mutable buffers. It is not
  a process/OS/HSM/SDK erasure guarantee.
- Online wrapped-key invalidation can precede final crypto-erasure evidence;
  backups/restore points may keep the governance workflow pending for their
  approved retention period.

Migration is additive and in-place-read only: dark-deploy all readers, prove
fleet capability, advance a scoped fence, write v2 for new protected values,
retain legacy/v1/redacted readers and immutable events, rebuild advisory
snapshots only from readable history, rewrap DEKs without changing events, and
rollback the writer only while retaining the v2 engine/backend/keys. Section 13
is the sole migration/downgrade authority.

### 17.4 Open-decision and evidence register

No technical format, algorithm, identity, lifecycle, backend, compatibility,
rollback, package, or threat-control decision is intentionally deferred. These
remaining evidence decisions block Story 8.1 completion and Story 8.2:

| ID | Missing decision/evidence | Owner(s) | Classification / closure |
| --- | --- | --- | --- |
| OD-01 | Content-bound acceptance of architecture/package/API/migration and every owned residual risk | Named Architect and EventStore owner | **Blocking**; approve/reject the frozen digest with identity, UTC time and evidence reference. |
| OD-02 | Independent threat/no-leak review and independent reproduction of G-001, NIST comparison and structural key-record companion | Named Security Reviewer and Test Architect/vector reviewer | **Blocking**; record method, versions, findings, disposition and frozen digest. Author self-check is not independent. |
| OD-03 | Azure custody, private network, identity/RBAC, availability, retention/backup/restore, break-glass and truthful erasure boundary acceptance | Named Operations owner and Security Reviewer | **Blocking**; approve/reject section 11 and residuals for the frozen digest. No resource provisioning is requested. |
| OD-04 | Atomic two-package inventory, stable Azure dependency policy, SBOM/provenance/package-only and release-gate acceptance | Named Release owner | **Blocking**; approve/reject sections 5/16 for the frozen digest. |
| OD-05 | Parties policy/erasure/certificate ownership, exact v1 compatibility, retained legacy reader/writer and post-v2 rollback acceptance | Named Parties maintainer | **Blocking**; approve/reject sections 9 and 11-16 against the pinned Parties SHA. |
| OD-06 | Independent compatibility/rollback and complete structure/traceability review | Named independent reviewer(s) | **Blocking**; record review evidence and close every material finding against the frozen digest. |
| PF-01 | Select/reverify exact stable Azure SDK/NuGet and emitted service API versions under section 11.3 | Story 8.2 implementer, Security and Release | Implementation **preflight**, not an open architecture choice; any unsupported/preview-only result returns to ADR change control. |
| PF-02 | Verify the chosen DAPR state component actually supplies strong consistency, ETags, transactions and TTL | Story 8.2 implementer and Operations | Implementation **preflight**; a capability gap returns to ADR change control rather than weakening section 10. |

### 17.5 Content-bound approval and authorization algorithm

The approval subject is the normative SHA-256 computed by section 1.2 together
with EventStore SHA `b200305978577530ee2e6ba9e92b886d26dc6f6f`, Parties SHA
`4378dede55d92e489caf7aad63d6c2892e6f856d`, the external-source retrieval date
and the embedded fixture hashes. Each approval record outside the normative
markers must contain the approver's unambiguous name/role, `Approved` or
`Rejected`, UTC timestamp, exact normative digest, evidence reference and any
bounded conditions. A group alias, planning approval, author assertion, implied
silence, issue status, or approval of an earlier digest is invalid.

Any normative byte or incorporated fixture change invalidates all approvals,
sets `story_8_2_authorized: false`, and requires every mandatory review/approval
again. Non-normative approval/evidence additions do not change the digest but
must be validated against it.

Story 8.2 is `authorized` only when OD-01 through OD-06 are closed with no open
material finding; the Architect, Security Reviewer, EventStore owner, Release
owner, Operations owner, Parties maintainer and independent vector reviewer are
all named/dated `Approved` for the same digest; all required residual risks are
explicitly accepted; and the digest/source/fixture validation passes. Otherwise
the only valid decision is **NOT AUTHORIZED**. At this freeze, approvals and
independent reviews are absent, so Story 8.2 is **NOT AUTHORIZED**, Story 8.1
must remain non-`done`, and sprint Story 8.2 remains blocked.

## Appendix A. Existing Story 22.7 Baseline And Preservation Inventory

### A.1 Public contracts and storage metadata

| Existing seam at EventStore SHA `b2003059…` | Exact state | Preservation constraint |
| --- | --- | --- |
| `IEventPayloadProtectionService` (`bb6c2570…`) | Event protect/unprotect; metadata-aware additive overload; object and typed snapshot methods; typed `TryUnprotect*` defaults; cancellation propagates and otherwise unclassified exceptions map to `ProviderUnavailable`. Protect receives identity, event object/type, bytes, and format, but no property path, key identity/version, message ID, sequence, or global position. | Add context through backward-compatible default interface methods/contracts or engine-internal deterministic derivation. Existing providers must continue to compile and run. |
| `EventStorePayloadProtectionMetadata` (`e417fbbc…`) | Metadata version 1 with state, scheme, key alias, content hint, and up to eight compatibility flags. It explicitly excludes raw keys, nonces, tags, and provider-private blobs. | V2 payload bytes carry the bounded cryptographic envelope. Metadata remains a constructive, non-secret routing allowlist and changes only through intentional versioned migration. |
| `EventStorePayloadProtectionMetadataCarrier` (`55d1e7c0…`) | `eventstore.protection` bounded JSON carrier; unknown, malformed, secret-shaped, or unsupported values become provider-opaque; missing metadata maps to legacy. | Preserve fail-closed parsing. Existing name/substring checks remain defense in depth, not proof that encoded or innocuously named secrets are safe. |
| `PayloadProtectionResult`, `PayloadUnprotectionOutcome`, `Snapshot*Outcome` | Readable results carry bytes/state; unreadable results carry no plaintext and one typed reason. | Extend additively; never create a second failure channel or parse provider exception text. |
| `UnreadableProtectedDataReason` (`a05b6a4c…`) | Missing key, invalidated/deleted, unavailable, denied, consistency mismatch, malformed metadata, unknown version, opaque unsupported operation, bytes/metadata mismatch. | Reuse where semantically exact. Any new durable condition requires an explicit additive versioned taxonomy and safe reason code. |
| Readability decisions and workflow contracts | `ProtectedDataReadabilityDecision*`, crypto-shredding transitions/audit, and restored-backup admission provide canonical stages and irreversible/operator states. | Engine outcomes flow through these contracts; no parallel lifecycle or restore model. |

### A.2 Runtime path inventory

| Path | Current protection boundary | Frozen preservation requirement |
| --- | --- | --- |
| Persist | `EventPersister` (`a3bf264f…`) serializes and protects every event before allocating global positions, then stages protected bytes plus metadata and atomically flushes through the actor turn. | All protection succeeds before any event is staged; never plaintext-fallback. AAD cannot bind later fields unless Story 8.2 deliberately changes call order through an additive context and proves atomicity/idempotency. |
| Command rehydrate | `AggregateActor.EnsureEventsReadableForDomainAsync` reads metadata, rejects opaque state, calls typed unprotect, and throws support-safe `ProtectedDataUnreadableException` before domain use. | No parse/apply before authenticated decryption succeeds. |
| Publish | `EventPublisher` (`4a8c5670…`) rejects opaque metadata, calls typed unprotect, builds canonical readability decisions, re-stamps safe metadata, and only then publishes plaintext. | No plaintext or protected opaque bytes published; cancellation propagates; safe reason only. |
| Projection live/rebuild/retry | `ProjectionEventWireBuilder`, `ProjectionUpdateOrchestrator`, `EventStoreProjectionDeliveryHistoryReader`, and retry worker call the same typed unprotect boundary before projection delivery. | Live, retry, and rebuild use identical routing and failure semantics. No checkpoint advance for unreadable input. |
| Stream/replay API | `StreamsController` reads stored events, maps opaque/unreadable decisions to bounded ProblemDetails, and only returns/replays readable envelopes. | Preserve tenant scope, typed failure, and safe public errors. |
| Admin stream/timeline/state | `AdminStreamQueryController` calls typed unprotect before decoding payload JSON; `DaprStreamQueryService` reconstructs protected ProblemDetails from a constructive extension allowlist. | Admin authorization is not permission to expose ciphertext, plaintext on auth failure, key aliases, or provider text. |
| Snapshot persist/read/inspection | `SnapshotManager` (`83c0e047…`) protects before staging, validates metadata, treats creation as advisory, retains opaque/unreadable protected snapshots, and falls back to replay. Manual inspection fails closed without deletion. | V2 gets deterministic snapshot serialization/wrapper. Protected unreadable snapshots remain retained. Story 8.1 must decide whether advisory creation remains acceptable. |
| Restore/governance | Restored-backup admission and crypto-shredding transitions preserve irreversible watermarks, quarantine, and operator decisions. | Restored protected keys/data never silently reactivate; restored backups pass admission before reads/writes. |
| Diagnostics/errors | `ProtectedDataDiagnosticRedactor` (`ff53be1f…`), global error mapping, source-generated logs, and safe ProblemDetails expose bounded identifiers/reasons. | Construct safe outputs; do not serialize then scrub. |
| Testing | `ProtectedDataLeakSentinel` (`0beac709…`), contract/server/testing suites, fake unreadable provider, and integration harness cover existing boundaries. | Story 8.2 extends rather than replaces these tests and adds persisted/provider-state/rollback evidence. |

`EventStreamReader` itself reads stored envelopes only; command-time plaintext is
enforced later by the aggregate actor. This distinction is intentional and must
remain visible in tests so raw storage reads are never mistaken for plaintext
authorization.

## Appendix B. Exact Parties Consumer Provenance

`Hexalith.Parties` is not a root-declared EventStore submodule. No checkout was
initialized or modified. The consumer evidence below was read directly from the
official repository at exact commit
`4378dede55d92e489caf7aad63d6c2892e6f856d` on 2026-07-16.

| Git blob | Artifact | Relevant observed contract |
| --- | --- | --- |
| `df2c29fba23f0fad7d5c67d904b560538b952bd9` | `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs` | `json+pdenc-v1`, `json-redacted`, `$enc`, AES-256-GCM field markers `{alg,kv,n,t,c}`, random 12-byte nonce, 16-byte tag, no AAD, dot/bracket CLR-property paths, protected snapshot wrapper, LocalDev key mechanics, and owned key/plaintext zeroing. |
| `3a24939dd5c66ac4c3dbda89e60301e066a0d095` | `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs` | Metadata v1 scheme `parties-aes-gcm-json-fields`, compatibility flags `format=json+pdenc-v1` and `field-envelope=pdenc-v1`, bytes/metadata checks, typed provider failure mapping, and erasure-status lookup. |
| `521e66aa05107db6b66590d720fce991744a03c3` | `src/Hexalith.Parties.Contracts/PersonalDataAttribute.cs` | Exact `[AttributeUsage(AttributeTargets.Property, AllowMultiple=false, Inherited=true)]` property marker in namespace `Hexalith.Parties.Contracts`. |
| `821fa5979ba6cc26654771721f8883ca904d6e3f` | `src/Hexalith.Parties.Security/PersonalDataGraphInspector.cs` | Reflection discovery, `JsonPropertyNameAttribute`/camel-case name selection, recursive graphs/arrays, explicit natural-person rule, cycle suppression. |
| `efb8d7607bcbda4af6a65797f20ada4f9ca2109b` | `src/Hexalith.Parties.Contracts/Security/IPartyErasureRecordStore.cs` | Parties-local erasure status/certificate/report persistence; this is consumer governance evidence, not the future generic `IErasureStateProvider`. |
| `d9de19a1a429183c8aa8d3a24d7e3c0a60a0c5d6` | `src/Hexalith.Parties.Contracts/Security/IPartyKeyManagementService.cs` | Parties-local create/get/version/rotate/delete key abstraction. |
| `f711e11de182ae448af534b9f07b25959a91e38d` | `src/Hexalith.Parties.Contracts/Security/IKeyStorageBackend.cs` | Parties-local secret path/version, tenant wrapping metadata, and deletion backend. |
| `9f62c403ae8d791e63cb97390373b2941f7995d9` | `src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj` | Parties Security directly references EventStore Contracts (project in source mode/package otherwise); no production Key Vault/HSM SDK dependency exists. |

No type named `IPersonalDataPolicy` or `IErasureStateProvider` exists at this
Parties commit. Story 8.2 must therefore introduce generic EventStore-owned
interfaces and a Parties adapter; it must not falsely describe current
Parties-local interfaces as those contracts. `PersonalDataAttribute` is exact
consumer evidence, but direct EventStore dependency on a Parties assembly is
forbidden. Section 9 freezes an EventStore marker/policy seam and explicit
consumer precedence.

Official source links are commit-pinned, for example:

- `https://github.com/Hexalith/Hexalith.Parties/blob/4378dede55d92e489caf7aad63d6c2892e6f856d/src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`
- `https://github.com/Hexalith/Hexalith.Parties/blob/4378dede55d92e489caf7aad63d6c2892e6f856d/src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`
- `https://github.com/Hexalith/Hexalith.Parties/blob/4378dede55d92e489caf7aad63d6c2892e6f856d/src/Hexalith.Parties.Contracts/PersonalDataAttribute.cs`

## Appendix C. Authoritative External Source Register

All sources were retrieved on 2026-07-16. Story 8.2 MUST reverify every living
platform/provider source and record the resolved SDK/API versions before code.

| Authority and URL | Applicable revision/profile | Supersession status on retrieval | Decision use |
| --- | --- | --- | --- |
| NIST SP 800-38D, `https://csrc.nist.gov/pubs/sp/800/38/d/final` | Final, November 2007 | Current final; NIST revision is in progress | GCM security, 96-bit IV guidance, invocation bounds |
| NIST 2026 second pre-draft call, `https://csrc.nist.gov/News/2026/gcm-and-gmac-block-cipher-modes-of-operation` | Notice dated 2026-06-01; comments through 2026-07-31 | Not a final replacement | Mandatory Story 8.2 recheck; no draft-only behavior adopted |
| RFC 5116, `https://www.rfc-editor.org/rfc/rfc5116.html` | Standards Track, January 2008 | No superseding RFC identified | AEAD plaintext-or-FAIL, nonce uniqueness, injective application encoding |
| RFC 6901, `https://www.rfc-editor.org/rfc/rfc6901.html` | Proposed Standard, April 2013 | No superseding RFC identified | Candidate property-path grammar; application restrictions in section 7 |
| RFC 4648, `https://www.rfc-editor.org/rfc/rfc4648.html` | October 2006 | No superseding RFC identified | Canonical base64url spelling |
| NIST SP 800-57 Part 1 Rev.5, `https://csrc.nist.gov/pubs/sp/800/57/pt1/r5/final` | Final, May 2020 | Supersedes Rev.4; no later final listed | Key types, cryptoperiod, retention, lifecycle responsibility |
| NIST SP 800-38F, `https://csrc.nist.gov/pubs/sp/800/38/f/final` | Final, December 2012 | Rev.1 is draft, not replacement final | Key-wrapping comparison |
| NIST SP 800-88 Rev.2, `https://csrc.nist.gov/pubs/sp/800/88/r2/final` | Final, September 2025 | Supersedes Rev.1 | Crypto-erasure limits, copies/backups, sanitization evidence |
| .NET cross-platform cryptography, `https://learn.microsoft.com/dotnet/standard/security/cross-platform-cryptography` | .NET 10 platform documentation | Living documentation | Platform support and portable tag restrictions |
| .NET `AesGcm`, `https://learn.microsoft.com/dotnet/api/system.security.cryptography.aesgcm.-ctor?view=net-10.0` | .NET 10 API | Living documentation | Required tag-size constructor and startup checks |
| .NET `ZeroMemory`, `https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-10.0` | .NET 10 API | Living documentation | Owned mutable-buffer zeroing only |
| Azure Key Vault key details, `https://learn.microsoft.com/azure/key-vault/keys/about-keys-details` | Living Azure service documentation | Reverify API/SDK and service capability | Vault versus Managed HSM key/wrap capability |
| Azure soft-delete, `https://learn.microsoft.com/azure/key-vault/general/soft-delete-overview` | Living Azure service documentation | Reverify retention controls | Deletion, recovery, purge-delay limitation |
| Azure rotation, `https://learn.microsoft.com/azure/key-vault/keys/how-to-configure-key-rotation` | Living Azure service documentation | Reverify provider behavior | Versioned KEK rotation |
| Azure RBAC, `https://learn.microsoft.com/azure/key-vault/general/rbac-guide` | Living Azure service documentation; API `2026-02-01` changes noted | Reverify before deployment | Identity, least privilege, control/data-plane split |
| Azure network security, `https://learn.microsoft.com/azure/key-vault/general/network-security` | Living Azure service documentation | Reverify private-link restrictions before deployment | Private endpoint, disabled public access, control/data-plane distinction |
| Azure HSM-protected key transfer/profile, `https://learn.microsoft.com/azure/key-vault/keys/hsm-protected-keys-byok` | Living Azure service documentation | Reverify vault SKU/key-type support | Confirms RSA-HSM support is limited to Premium vault/Managed HSM; no BYOK is selected |
| Azure Identity production guidance, `https://learn.microsoft.com/dotnet/azure/sdk/authentication/best-practices` | Living Azure SDK guidance | Reverify exact stable package/API before implementation | Deterministic `ManagedIdentityCredential` in production rather than a fallback chain |
| Azure .NET `KeyWrapAlgorithm`, `https://learn.microsoft.com/dotnet/api/azure.security.keyvault.keys.cryptography.keywrapalgorithm` | Living stable Azure SDK API | Reverify exact package version/source | Exact `RsaOaep256` adapter identifier |
| ASP.NET Core environments, `https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-10.0` | ASP.NET Core 10 | Living documentation | Exact Development-only provider guard |

<!-- END NORMATIVE CONTENT -->

## 18. Independent Review And Reproduction Evidence

No independent review or approval has yet been recorded. This section is
intentionally non-normative evidence bound to normative digest
`efb419b5fa05d0b1d9bbf463261172cce181d5ada2c0c8d305751cc57497f440`.

Author verification is recorded for reproducibility but is explicitly **not**
the independent evidence required by OD-02/OD-06:

| Date | Actor/method | Result | Independence/disposition |
| --- | --- | --- | --- |
| 2026-07-16 | OpenAI Codex (GPT-5), structural/traceability/source/scope checks | Sections 2-17 populated; all AC/FR/NFR/AD references present; current package manifest remains 14 | Author self-check only; mandatory independent review remains open. |
| 2026-07-16 | Node.js 26.4.0/OpenSSL 3.5.7 and Python 3.14.4/cryptography 46.0.5 | Both reproduced G-001 ciphertext+tag and NIST AES-256-GCM Count 0 tag exactly | Two toolchains operated by the author; independent person/reviewer reproduction remains open. |
| 2026-07-16 | SHA-256 over section 1.2 LF byte range | `efb419b5fa05d0b1d9bbf463261172cce181d5ada2c0c8d305751cc57497f440` | Frozen approval subject; no approval implied. |

## 19. Change History

| Date | Change | Authorization impact |
| --- | --- | --- |
| 2026-07-16 | Created authoritative baseline, traceability, EventStore path inventory, exact Parties provenance, and source register. | Draft remains not authorized. |
| 2026-07-16 | Froze ownership/packages, v2 wire/AAD, compatibility/rollback, policy/lifecycle, Azure backend, threat/no-leak/vectors, Story 8.2 handoff, ADR disposition and authorization algorithm. | Normative digest frozen; remains not authorized pending independent review and named approvals. |
