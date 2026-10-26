---
title: '5.12 Route and idempotency catalog construction'
type: 'feature'
created: '2026-10-25'
status: 'draft'
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

## Open Questions

1. **Vocabulary owner:** Contracts definitions with unchanged ServiceDefaults forwarding constants (recommended), or a new abstractions package? The first uses today's existing dependency; the second expands packaging and release scope.
2. **Human grants:** Reuse `eventstore:permission` with exact platform-operation matching (recommended), or add a new human claim? Exact matching rejects missing grants, wildcards and administrator bypass; a new claim also needs issuer/host mapping changes. Workload `eventstore:operation` remains separate.
3. **Ingress:** Keep general ingress policy in endpoint metadata and add namespace + `RequiredHumanPermission` to platform admission declarations (recommended), or catalog every ingress endpoint now? The first derives platform endpoint metadata from the same declaration while downstream rows retain workload credentials; the second expands 5.12 into 5.14's inventory.
4. **Missing inputs:** Supply authoritative adapter/descriptor/retention declarations for the current Development profile (recommended), or narrow the first delivery to construction tooling plus a failing incomplete-profile report? The latter permits useful implementation but does not complete 5.12 or unblock 2.14. No registrations were found in current Sample/host code.

## Code Map

See [investigation](investigations/story-5-12-code-map.md) for exact sources and missing inputs. Prefixes: `C=src/Hexalith.EventStore.Contracts`, `S=src/Hexalith.EventStore.Server`, `D=src/Hexalith.EventStore.ServiceDefaults`, `T=tests/Hexalith.EventStore.Contracts.Tests`.

- `C/Commands/ICommandContract.cs`, `C/Queries/IQueryContract.cs` — missing version/security declarations.
- `S/DomainServices/DomainServiceResolver.cs` — targets; query dispatch overrides method to `query`.
- `S/Commands/IdempotencyIntentAdapterRegistry.cs` — adapter lookup lacks domain.
- `D/Authentication/EventStoreWorkloadOperations.cs` — public identifiers; D references C.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/architecture.md` and its companion `.memlog.md` — record the three owner choices once answered, removing the obsolete no-Contracts-dependency explanation without altering other decisions.
- [ ] `C/Routing/RouteContractDeclaration.cs`, `RouteCatalogEnvelope.cs`, `RouteCatalogEntry.cs`, `IdempotencyCatalogEntry.cs`, `PlatformAdmissionDeclaration.cs` in that directory — define closed versioned models; one type per file; require explicit command grammar versions.
- [ ] `C/Routing/RouteCatalogCodec.cs`, `RouteCatalogValidator.cs` — implement canonical encoding/declaration hashing, exact-byte decoding, semantic validation and bounded entry/field failures using BCL APIs.
- [ ] `C/Authorization/EventStoreOperationIdentifiers.cs`, `D/Authentication/EventStoreWorkloadOperations.cs`, `JwtBearerAuthenticationContract.cs` in the latter directory — implement approved declaration ownership while preserving existing authentication behavior; 2.14 implements new platform enforcement.
- [ ] `src/Hexalith.EventStore.Client/Routing/DevelopmentRouteCatalogBuilder.cs` and `tools/routing-catalog.cs` — compose declared contracts with value-free Development bindings, generate/check the catalog through the shared codec; refuse missing required inputs.
- [ ] `deploy/dapr/eventstore-routing-catalog.json` — generate only from a complete profile whose current effective routes are verified; never fill missing policy with defaults to make generation pass.
- [ ] `T/Routing/RouteCatalogCodecTests.cs`, `RouteCatalogValidatorTests.cs`, `Fixtures/` in that directory — positive controls and checked-in negatives observed failing for every rejection, round trips and stable digests.
- [ ] `tests/Hexalith.EventStore.Server.Tests/DomainServices/DevelopmentRouteCatalogParityTests.cs`, `tests/Hexalith.EventStore.QueryRouting.Tests/DaprDomainQueryInvokerTests.cs` — compare actual resolution, query override and projection discovery; run through existing CI lanes.

**Acceptance Criteria:**
- Given complete declarations, when generation runs twice, then canonical bytes/digests match and runtime-target parity passes without changing dispatch behavior.
- Given changed declarations or malformed inputs, when validation/CI runs, then drift and each rejection fail beside a passing control.
- Given missing adapter/policy metadata or an unactivated production instance, when evaluated, then incompleteness/readiness remains explicit and this story cannot claim complete delivery from synthetic fixtures.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Use a catalog-specific BCL codec. Human platform admission and downstream workload authorization are distinct. Missing AD-25 declarations block the complete catalog; configuration defaults cannot replace them.

## Verification

Build affected test projects individually with `-c Debug -p:UseHexalithProjectReferences=true`; run focused built xUnit executables then full suites. CI uses Release/package mode. Record commands, counts, full revision and CI link. Planning: `git diff --check`, `bash scripts/check-committed-files.sh`; no runtime tests yet.
