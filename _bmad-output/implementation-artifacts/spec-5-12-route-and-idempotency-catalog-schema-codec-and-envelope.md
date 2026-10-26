---
title: '5.12 Route and idempotency catalog construction'
type: 'feature'
created: '2026-10-25'
status: 'in-progress'
baseline_commit: '8e7c12211c05d7c8db926148550cd3b45844ffd4'
route: 'dispatch'
review_loop_iteration: 0
story_id: '5.12'
story_key: '5-12-route-and-idempotency-catalog-schema-codec-and-envelope'
context:
  - _bmad-output/implementation-artifacts/epic-5-context.md
  - _bmad-output/implementation-artifacts/investigations/story-5-12-code-map.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stories 2.14 and 2.15 lack the shared AD-33 route/scope/version catalog.

**Approach:** Add Contracts declarations, a versioned envelope, codec/validator and Development/test catalog derived from declarations/composition bindings, checked against current resolvers.

**Owner direction (2026-10-25):** Complete this before 2.14; use a library/local catalog. Activation stays in 5.13; storage identities stay unchanged.

**Owner decisions (2026-10-26):** Contracts owns operation identifiers; ServiceDefaults retains authentication and compatible forwarding constants. Human platform grants use exact `eventstore:permission` matches, with no wildcard or administrator bypass. General ingress stays in endpoint metadata; platform declarations bind namespace and `RequiredHumanPermission` separately from workload-hop credentials. Implement tooling and a concrete gap report first, then obtain/implement real missing declarations. This story stays open until the complete Development catalog passes; 2.14 remains pending.

## Boundaries & Constraints

**Always:** Bind stable route IDs, generation, root/facet digests, exact target app/method/version, admitted credential/operation and command MessageId declaration/version digest. Join each command to AD-25 metadata: adapter, operation, descriptor schema/digest, retention, digest-key generations, logical secret map and consumer. Preserve existing wire/hash protocols. Record proof in this spec plus CI under the October 23 policy.

**Never:** Invent missing adapter/policy declarations; serialize secret values or request intent; change runtime authorization/routing, activate a catalog, deploy, or claim production/OQ8 readiness. Do not rebuild deleted evidence machinery. Namespace/grants never synthesize a tenant named `system`.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Result | Failure |
| --- | --- | --- | --- |
| Valid envelope | Complete declarations and bindings | Canonical UTF-8; stable SHA-256; exact decode/encode round trip | None |
| Invalid bytes | Duplicate/unknown fields, unsupported version, noncanonical JSON | Reject before accepting an envelope | Entry/field reason |
| Invalid mapping | Missing/duplicate/ambiguous key, join, declaration or facet mismatch | Reject | Safe diagnostic |
| Development | Explicit profile and complete declarations | Generated file equals effective command/query/projection routes | Drift fails |
| Override | Undeclared fallback or runtime override outside Development | Reject candidate | No production claim |

</frozen-after-approval>

## Code Map

See [investigation](investigations/story-5-12-code-map.md) for exact sources and missing inputs. Prefixes: `C=src/Hexalith.EventStore.Contracts`, `S=src/Hexalith.EventStore.Server`, `D=src/Hexalith.EventStore.ServiceDefaults`, `T=tests/Hexalith.EventStore.Contracts.Tests`.

- `C/Commands/ICommandContract.cs`, `C/Queries/IQueryContract.cs` — missing version/security declarations.
- `S/DomainServices/DomainServiceResolver.cs` — targets; query dispatch overrides method to `query`.
- `S/Commands/IdempotencyIntentAdapterRegistry.cs` — adapter lookup lacks domain.
- `D/Authentication/EventStoreWorkloadOperations.cs` — public identifiers; D references C.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/planning-artifacts/architecture.md` and its companion `.memlog.md` — record the three owner choices once answered, removing the obsolete no-Contracts-dependency explanation without altering other decisions.
- [x] `C/Routing/RouteContractDeclaration.cs`, `RouteCatalogEnvelope.cs`, `RouteCatalogEntry.cs`, `IdempotencyCatalogEntry.cs`, `PlatformAdmissionDeclaration.cs` in that directory — define closed versioned models; one type per file; require explicit command grammar versions.
- [x] `C/Routing/RouteCatalogCodec.cs`, `RouteCatalogValidator.cs` — implement canonical encoding/declaration hashing, exact-byte decoding, semantic validation and bounded entry/field failures using BCL APIs.
- [x] `C/Authorization/EventStoreOperationIdentifiers.cs`, `D/Authentication/EventStoreWorkloadOperations.cs`, `JwtBearerAuthenticationContract.cs` in the latter directory — implement approved declaration ownership while preserving existing authentication behavior; 2.14 implements new platform enforcement.
- [x] `src/Hexalith.EventStore.Client/Routing/DevelopmentRouteCatalogBuilder.cs` and `tools/routing-catalog.cs` — compose declared contracts with value-free Development bindings, generate/check the catalog through the shared codec; refuse missing required inputs.
- [ ] `deploy/dapr/eventstore-routing-catalog.json` — generate only from a complete profile whose current effective routes are verified; never fill missing policy with defaults to make generation pass.
- [x] `T/Routing/RouteCatalogCodecTests.cs`, `RouteCatalogValidatorTests.cs`, `Fixtures/` in that directory — positive controls and checked-in negatives observed failing for every rejection, round trips and stable digests.
- [ ] `tests/Hexalith.EventStore.Server.Tests/DomainServices/DevelopmentRouteCatalogParityTests.cs`, `tests/Hexalith.EventStore.QueryRouting.Tests/DaprDomainQueryInvokerTests.cs` — compare actual resolution, query override and projection discovery; run through existing CI lanes.


**Acceptance Criteria:**
- Given complete declarations, when generation runs twice, then canonical bytes/digests match and runtime-target parity passes without changing dispatch behavior.
- Given changed declarations or malformed inputs, when validation/CI runs, then drift and each rejection fail beside a passing control.
- Given missing adapter/policy metadata or an unactivated production instance, when evaluated, then incompleteness/readiness remains explicit and this story cannot claim complete delivery from synthetic fixtures.

## Implementation Notes

The bounded tooling defines closed v1 construction inputs, deterministic catalog bytes, declaration/descriptor/idempotency-entry/facet digests, and an external root digest equal to SHA-256 of the exact retained envelope bytes. Synthetic complete fixtures use `fixture-*` targets and explicit test policies; they are not actual Development declarations. No deployment catalog or runtime adoption is included. V1 permits only workload command/query/projection target hops; general ingress remains endpoint metadata. Commands require exact declarations and individual AD-25 joins; query/projection fallbacks must enumerate finite discriminators, with exact declarations taking precedence.


- 2026-10-26 — Approved staged delivery is implemented through [catalog construction tooling](spec-route-catalog-construction-tooling.md). That bounded work item is complete after review and verification; this full story and its sprint row remain in-progress until complete real declarations and Development parity pass.

## Spec Change Log

## Review Triage Log

## Design Notes

Use a catalog-specific BCL codec. Human platform admission and downstream workload authorization are distinct. Missing AD-25 declarations block the complete catalog; configuration defaults cannot replace them.

## Verification

Bounded tooling is implemented and reviewed on source snapshot `54733d1b928c41f1e9fdf46841d043c8d482d648` plus reviewed working-tree patches and documentation. Final individual Debug/source builds passed with zero warnings/errors. Focused checks passed 74 Contracts and 37 Client cases; the compatibility lane passed 12 ServiceDefaults cases. Parent reran all full affected suites after review fixes: Contracts 1,315 passed/2 existing package-inventory skips; Client 2,217 passed/2 existing Dapr live-test skips; ServiceDefaults 16 passed. All 3,548 tests passed with no failures. CLI tests now use an isolated build, resolving the earlier shared-artifact collision. Three review layers completed; all catalog findings were resolved, with one unrelated query paging issue recorded in deferred-work.md. Exact commands, triage, and earlier failure boundaries are in the bounded tooling spec.

Direct CLI generation/validation exited 0 with golden root `d30010c9b1017f29c5ae9da9a39289d8865bfce91bd153b6000beaad0248f4c0`; actual-profile inspection exited 2 with four bindings/52 diagnostics and no deployment file. `git diff --check` and `bash scripts/check-committed-files.sh` passed. CI uses the existing Release/package lanes; it was not run in this session, so no CI URL is available. These results prove construction and compatibility, not a complete real profile or runtime-target parity.

### Bounded construction result (2026-10-26)

The [tooling spec](spec-route-catalog-construction-tooling.md) records the exact commands, source revision, test counts, golden root digest, and real-profile inspection. Its four configured bindings (`counter`, `greeting`, `global-administrators`, `tenants`) still lack authoritative route declarations and complete per-command AD-25 adapter/operation, policy, descriptor, retention, digest-generation, logical-map, and consumer metadata. The two existing `system` bindings also lack namespace/exact-human-permission declarations. Projection inventory and convention/dynamic fallback parity remain unverified. The offline report enumerates 52 missing-input diagnostics and explicitly limits its inventory to checked-in JSON configuration.

Synthetic fixture generation succeeds; actual-profile inspection exits nonzero and writes no deployment catalog. The full Development catalog and runtime-target parity tasks remain unchecked. Story 5.12 and its sprint row remain in-progress; Story 2.14 stays pending/backlog. Construction does not activate a catalog, implement platform admission, or establish production/OQ8 readiness.
