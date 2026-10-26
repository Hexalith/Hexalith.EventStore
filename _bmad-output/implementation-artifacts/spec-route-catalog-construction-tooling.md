---
title: 'Route catalog construction tooling'
type: 'feature'
created: '2026-10-26'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '8e7c12211c05d7c8db926148550cd3b45844ffd4'
context:
  - _bmad-output/implementation-artifacts/epic-5-context.md
  - _bmad-output/implementation-artifacts/investigations/story-5-12-code-map.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Catalog construction has no shared schema/codec, and actual domain adapter/policy declarations are incomplete.

**Approach:** Deliver the approved first stage: closed catalog contracts, canonical codec/validator, compatible operation definitions, offline builder/tool and a precise incomplete-profile report. Fixtures prove construction; the full Development catalog remains pending real declarations.

**Owner decisions (2026-10-26):** Contracts owns operation vocabulary; ServiceDefaults owns authentication and forwards existing constants unchanged. Human platform grants use exact `eventstore:permission`, with no wildcard/admin bypass; workload grants remain separate. General ingress stays in endpoint metadata; platform declarations carry namespace plus `RequiredHumanPermission`. Tooling comes first; do not invent idempotency policies.

## Boundaries & Constraints

**Always:** Keep operation literals/public symbols compatible. Require explicit command MessageId versions, stable route IDs, target app/method/version, credential/operation, declaration/facet/root digests and complete AD-25 entries joined to commands. Platform human admission and downstream workload credentials are distinct. Namespace grammar is disjoint from tenant IDs. Use the repository baseline, one C# type per file and existing BCL dependencies.

**Never:** Change runtime routing/authentication/enforcement, persisted identities, existing fingerprint protocols, deployment or submodules. No activation/signing service. Never serialize key values or request intent. Never generate the canonical deployment file from incomplete inputs. Completion of this bounded item leaves Story 5.12 and its sprint row in-progress, and 2.14 pending.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Failure |
| --- | --- | --- | --- |
| Complete fixture | Explicit routes, bindings and adapter metadata | Stable canonical bytes/digests; decode/encode exact round trip | None |
| Invalid encoding | Duplicate/unknown fields, unsupported version, noncanonical bytes | Reject before accepting catalog | Bounded reason |
| Invalid semantics | Duplicate/ambiguous routes, missing declarations, wrong digests/joins/generations, invalid scope/credential pairing | Reject | Route/field diagnostic |
| Unsafe override | Undeclared fallback or non-Development runtime override | Reject | Bounded reason |
| Incomplete profile | Actual Development bindings without required declarations | Nonzero tool exit, concrete missing-input report, no deployment catalog written | No secret values |

</frozen-after-approval>

## Code Map

Full sources are in the investigation. `C=src/Hexalith.EventStore.Contracts`, `D=src/Hexalith.EventStore.ServiceDefaults`, `L=src/Hexalith.EventStore.Client`.

- `C/Commands/ICommandContract.cs`, `C/Queries/IQueryContract.cs` lack catalog metadata; use additive declaration models, preserving existing callers.
- `D/Authentication/EventStoreWorkloadOperations.cs` contains public constants; D already references C.
- `src/Hexalith.EventStore/appsettings.json`, `appsettings.Development.json` supply current Development bindings; query method is `query`, distinct from command method.
- `src/Hexalith.EventStore.DomainService/IIdempotencyIntentAdapter.cs` and Server registry define real adapter semantics; no product adapter registration was found. Configuration includes keys: extract only explicit safe fields.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/planning-artifacts/architecture.md`, `architecture/architecture-eventstore-2026-07-05/.memlog.md` beneath planning-artifacts — record approved decisions; correct the obsolete dependency/owner-decision statements without changing unrelated architecture.
- [x] `C/Routing/` — add declarations, envelope, route/idempotency entries and platform admission models, with a closed versioned field set and explicit missing-data validation. Document canonical digest preimages without self-reference.
- [x] `C/Routing/RouteCatalogCodec.cs`, `RouteCatalogValidator.cs` — encode/decode/hash using BCL APIs; strict duplicate/unknown-field handling and semantic validation. Keep this catalog-specific.
- [x] `C/Authorization/EventStoreOperationIdentifiers.cs`, `D/Authentication/EventStoreWorkloadOperations.cs` — centralize identifiers with compatible forwarding constants; declare the existing human permission claim in its authentication owner without altering enforcement.
- [x] `L/Routing/DevelopmentRouteCatalogBuilder.cs`, `tools/routing-catalog.cs` — provide offline generation/validation from explicit declarations and safe composition bindings, and inspect current Development input gaps. Small file-based developer tool; no new operator CLI/package. Incomplete inspection must report its inventory limits honestly.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Routing/`, `tests/Hexalith.EventStore.Client.Tests/Routing/`, `tests/Hexalith.EventStore.ServiceDefaults.Tests/` — positive/negative fixture matrix, builder/gap diagnostics, real Development configuration coverage, CLI success/failure and operation compatibility. Use existing CI-executed projects.
- [x] This spec's Verification and the parent [5.12 spec](spec-5-12-route-and-idempotency-catalog-schema-codec-and-envelope.md) — record precise missing declarations and results; full catalog/runtime parity remains unchecked in the parent.

**Acceptance Criteria:**
- Given complete fixtures, when library/tool generation repeats, then bytes/digests agree and every matrix rejection is observed beside a passing control.
- Given actual incomplete Development inputs, when inspection runs, then safe actionable gaps are reported without writing a purported complete catalog or changing runtime behavior.
- Given existing consumers, when affected builds/tests run, then existing operation values and authentication behavior remain compatible.

## Implementation Notes

- Closed v1 models are additive: existing command/query interfaces and runtime dispatch remain unchanged. Commands declare MessageId grammar 1 or 2 and require individual AD-25 entries. Query/projection fallback declarations enumerate a finite discriminator set; exact keys take precedence. General ingress is outside this schema.
- Platform admission binds `platform:<name>` plus an exact human permission. The colon makes the namespace disjoint from tenant identifiers. Message/projection target hops remain workload operations. The new human claim constant documents the approved existing claim; it does not change authorization enforcement.
- Canonical UTF-8 uses ordinal object-property ordering, BCL default string escaping, decimal integers, explicit nulls, no BOM/whitespace/newline, and stable collection ordering. Reader generation order remains significant. Declaration, descriptor, entry, and facet digests use separate purpose/version preimages. Idempotency content hashes every entry field except its own digest. The root digest is returned separately and equals SHA-256 of the exact retained canonical envelope bytes; `Decode` can compare an expected external root.
- `tools/routing-catalog.cs` is a file-based offline developer tool with `generate`, `validate`, and `inspect` commands. Construction inputs reject unknown, duplicate, and missing fields. Generation validates everything before writing a temporary sibling and atomically replacing its output; failed validation or replacement leaves an existing file intact. CLI reads stop at the 4 MiB limit plus one byte. The tool uses a regular .NET build (`PublishAot=false`), consistent with the reflection-based library APIs.
- Golden inputs, bytes, and digests are synthetic and reproducible with the independent Python stdlib producer in `tests/Hexalith.EventStore.Contracts.Tests/Routing/Fixtures/generate-fixtures.py`. They contain schema metadata and logical references only, not key values or request intent. They establish no actual domain policy.


## Spec Change Log

- 2026-10-26: Implemented the bounded construction delivery. Kept the parent story and sprint row in-progress; complete Development generation, runtime parity, activation, and 2.14 adoption remain outside this delivery.


## Review Triage Log

Three independent review layers completed on 2026-10-26. Edge-case review returned no findings. Each finding below was classified before grouping. The patch routes correct demonstrated implementation/test defects without changing approved intent or adding public API. B2–B4 share the inspection JSON/configuration-semantics root cause; other rows remain separate.

| ID | Finding | Verdict | Evidence and route |
| --- | --- | --- | --- |
| V1 | Nested construction duplicates lack effective tests | medium | Pre-verified by verification reviewer: Decode's byte comparison masks missing recursive duplicate checks; ReadInput tests cover only root duplicates. Patch nested declaration/binding/descriptor duplicates with exact diagnostic assertions and CLI output preservation. |
| B1 | Catalog domain grammar differs from runtime | medium | Validator's shared Name pattern permits 128 characters and forbids internal consecutive hyphens; AggregateIdentity, NamingConventionEngine, and DomainServiceResolver allow consecutive hyphens and cap domains at 64. Patch domain-specific validation and boundary coverage. |
| B2 | Inspection uses case-sensitive configuration merging | medium | TryGetProperty and both ordinal dictionaries drop lowercase sections or fail to overlay differently cased fields. Host configuration treats these keys case-insensitively. Patch inspection parsing/merging and tests. |
| B3 | Inspection accepts duplicate configuration properties | medium | JsonDocument lookups take the last duplicated Domain value, while host configuration rejects duplicated keys. Patch case-insensitive duplicate detection and tests. |
| B4 | Inspection rejects normal appsettings syntax | medium | Default JsonDocument parsing rejects comments, trailing commas, and BOM-prefixed bytes that the appsettings reader accepts. Patch only inspection parsing; strict catalog/construction input remains strict. |
| B5 | Inspection discards registration selectors | medium | Resolver selects dictionary keys (exact/pipe/sanitized wildcard), while inspection reports only embedded values. A greeting selector with counter fields produces a false counter inventory. Patch by validating selector/value agreement and refusing ambiguous inventory; no new public field needed. |
| B6 | Closed-schema encoding diagnostics need nested paths | false | The approved invalid-encoding row requires a bounded reason, which `$: invalid-json-or-closed-schema` and `$: duplicate-field` provide without disclosing input. Indexed route/field diagnostics are required for semantic failures and already supplied by the validator. No required path or acceptance behavior is missing. |
| B7 | CLI applies byte limit after reading whole file | medium | All three commands use File.ReadAllBytes before the 4 MiB codec guard, so large local files allocate unbounded buffers. Patch bounded reading before codec/inspection and test oversize refusal. |
| B8 | CLI output replacement can destroy previous valid bytes | medium | File.WriteAllBytes truncates the destination before write completion; its IOException handler cannot restore it. Patch a temporary sibling write followed by replacement, preserving previous output on failure. |
| B9 | CLI tests mutate shared build assets | medium | RunToolAsync executes dotnet run against shared project obj directories; the recorded NETSDK1047 packaging collision demonstrates interference. Patch the invocation to use isolated build artifacts and verify the CLI test. |
| B10 | Conditional query response loses default producer paging | medium | QueriesController only guards request policy inputs/version/warnings before 304; producer Paging survives normalization but has no 304 carrier in EventStoreGatewayClient. This is in concurrent query/provenance work, outside this item's explicit no-runtime-change intent. Defer with exact consumer evidence; preserve that work unchanged. |

## Verification

Baseline captured before code changes: `aspire run --detach --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` exited 0; `aspire describe` reported all listed resources Running/Healthy. The owned AppHost was stopped before implementation to release build resources.

Build affected projects individually in Debug/source mode. Run focused built xUnit executables with single-dash filters, then full affected suites. Verify tool fixture success and real-profile nonzero failure with no output catalog. Record commands/counts/full revision and CI availability. `git diff --check` and `bash scripts/check-committed-files.sh` must pass. Do not stage, commit or push; use a temporary diff including untracked files for review.

### Actual Development input gap

Inspection accepts normal appsettings comments/trailing commas/BOM and merges section/field names case-insensitively. It refuses duplicate keys, noncanonical selectors, and selector/value disagreements; only matching pipe and sanitized wildcard selectors are supported. It reads only whitelisted `TenantId`, `Domain`, `AppId`, `MethodName`, and `Version` fields from the checked-in base/Development JSON layers. The observed inventory is:

| Domain | Tenant pattern | App | Command method | Query method | Version |
| --- | --- | --- | --- | --- | --- |
| counter | `*` | sample | process | query | v1 |
| greeting | `*` | sample | process | query | v1 |
| global-administrators | system | tenants | process | query | v1 |
| tenants | system | tenants | process | query | v1 |

All four bindings still need authoritative command/query declarations, stable route IDs, MessageId versions, credential/operation declarations, and projection declarations/bindings. AD-25 inputs still needed per command: adapter/operation, policy version, descriptor schema/digest, retention tier, active/reader generations, logical secret map/generation, and consumer identity. Both existing `system` bindings additionally need platform namespace and exact human permission declarations. The tool reports 52 diagnostics: twelve categories for each binding, two platform-admission gaps, and two profile/inventory gaps.

This is a checked-in configuration inventory, not a claim that no adapters exist in any loaded runtime. It does not inspect environment/user secrets, AppHost injection, Dapr dynamic configuration, assembly declarations, adapter registrations, or runtime projection discovery. Existing key options cannot substitute for authoritative per-command declarations. Convention/dynamic fallbacks and complete runtime parity remain unverified. No canonical deployment catalog is written.

### Pre-review verification (2026-10-26)

Final construction-code snapshot: `54733d1b928c41f1e9fdf46841d043c8d482d648`, plus the final claim-compatibility assertion and documentation working-tree changes. An external process advanced and committed the shared workspace during implementation (first observed implementation HEAD `97faead54`, then `9f2565bec8d606f2a783fdfb55b8fe484e1223d2`, then the final snapshot above). This agent did not stage, commit, push, or change submodules. The per-entry ContentDigest implementation and expanded 67-case Contracts matrix are included in the final snapshot; all final runs below use that schema. Concurrent gateway/query/provenance changes and submodule pointers are not owned by this work.

Each affected project built individually with zero warnings/errors:

```bash
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -v quiet
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -v quiet
dotnet build tests/Hexalith.EventStore.ServiceDefaults.Tests/Hexalith.EventStore.ServiceDefaults.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -v quiet
```

Focused executable invocations:

```bash
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Routing.DevelopmentRouteCatalogBuilderTests'
dotnet tests/Hexalith.EventStore.ServiceDefaults.Tests/bin/Debug/net10.0/Hexalith.EventStore.ServiceDefaults.Tests.dll -class '*EventStoreOperationCompatibilityTests'
```

All focused cases passed: **67 Contracts, 12 Client, 12 ServiceDefaults**, with zero skips/failures. Full suites used the same respective `dotnet <assembly.dll>` command without `-class`:

| Project | Total | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: | ---: |
| Contracts | 1,310 | 1,308 | 2 | 0 |
| Client | 2,194 | 2,192 | 2 | 0 |
| ServiceDefaults | 16 | 16 | 0 | 0 |

Contracts' two existing skips require `EVENTSTORE_PACKAGE_CONTRACT_DIR`; Client's two existing live projection recovery cases require isolated Dapr endpoints. No new catalog test was skipped. The isolated full Contracts run also executed its heavyweight packaging checks and completed in 269.804 seconds. An earlier pre-entry-digest full Contracts run had 1,299 total with one `NETSDK1047` packaging failure because the concurrent Client CLI restore overwrote shared multi-RID assets; the serial rerun above resolved it. Initial source-mode `--no-restore` compilation also saw stale package/source `CS1704` assets; rebuilding with restore resolved it. No build gate was weakened.

Direct CLI checks:

```bash
dotnet run --file tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- generate tests/Hexalith.EventStore.Contracts.Tests/Routing/Fixtures/CompleteInput.json /tmp/route-catalog-fixture.json
dotnet run --file tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- validate /tmp/route-catalog-fixture.json d30010c9b1017f29c5ae9da9a39289d8865bfce91bd153b6000beaad0248f4c0
dotnet run --file tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- inspect src/Hexalith.EventStore/appsettings.json src/Hexalith.EventStore/appsettings.Development.json
```

Generation and validation exited **0**, produced exact golden bytes, and reported root `d30010c9b1017f29c5ae9da9a39289d8865bfce91bd153b6000beaad0248f4c0`. Real-profile inspection exited **2**, reported four bindings and 52 gaps, and left `deploy/dapr/eventstore-routing-catalog.json` absent. The CLI integration test additionally proves wrong-root failure and incomplete-input failure without creating a destination or overwriting an existing valid file.

`git diff --check` and `bash scripts/check-committed-files.sh` passed. A scoped temporary review diff, including any untracked owned files, is at `/tmp/route-catalog-tooling-review.patch`; `/tmp/route-catalog-owned-files.txt` lists all 34 owned files. Temporary logs and inspection output remain outside the repository. Existing `.github/workflows/ci.yml` executes all affected test projects in Release/package mode; CI was not invoked in this session and no CI run URL is available. Full real-catalog generation/runtime parity and activation remain pending in the parent story.

### Final review and verification (2026-10-26)

Completed all three required independent review layers. Parent triage verified and patched V1 and B1–B5/B7–B9; B6 was rejected against the approved bounded encoding-diagnostic contract. B10 is the one deferred issue, recorded in `deferred-work.md`: concurrent query code can lose producer paging metadata in a 304 response. It is outside this item's explicit no-runtime-change intent. No catalog findings remain unresolved.

The patches align domain grammar with runtime (64 characters, consecutive internal hyphens), handle appsettings syntax/casing and duplicate keys, validate registration selectors, bound file reads, and replace output through a temporary sibling. Regression coverage exercises nested duplicate construction fields with exact diagnostics, domain boundaries, configuration parsing, oversized files, failed output replacement/cleanup, and preservation of old bytes through an open handle. CLI integration builds the existing file-based tool once with `--artifacts-path` in a temporary directory and reuses its DLL; no shared build assets or new project are needed. A proposed Linux file-size-limit fault test was removed because it prevented CoreCLR startup rather than testing the tool; the final output-preservation checks pass.

Final source boundary: `54733d1b928c41f1e9fdf46841d043c8d482d648` plus the reviewed working-tree patches and documentation. No assistant commit, staging, push, dependency update, or submodule mutation occurred. Parent inspected the final patches and reran every build/full-suite command listed above sequentially. All builds exited 0 with zero warnings/errors. Focused changed-code checks passed **74/74 Contracts** and **37/37 Client**; the unchanged compatibility lane previously passed **12/12**.

| Final full suite | Total | Passed | Existing skips | Failed |
| --- | ---: | ---: | ---: | ---: |
| Contracts | 1,317 | 1,315 | 2 | 0 |
| Client | 2,219 | 2,217 | 2 | 0 |
| ServiceDefaults | 16 | 16 | 0 | 0 |

**3,548 passed, four existing skips, zero failures.** Contracts completed in 190.6 seconds; Client in 34.5 seconds; ServiceDefaults in 0.6 seconds. Client's final full run includes CLI generation/validation, rejection/output-preservation cases, and actual-profile inspection. Golden bytes/root and the four-binding/52-gap inventory remain unchanged. `git diff --check` and `bash scripts/check-committed-files.sh` passed; the latter checked 9,128 tracked entries. Final local logs are temporary at `/tmp/catalog-final-verification-9ux5m1dh`; no evidence packet was committed. CI was not invoked and no CI URL is claimed.

The bounded item is done. Full Story 5.12 and its sprint row remain in-progress pending actual declarations, complete Development generation, and runtime parity; 2.14 stays backlog. The canonical deployment catalog remains absent. Git history mutations were not performed, following the workspace preservation instruction.
