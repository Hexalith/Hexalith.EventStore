---
title: 'Route catalog construction tooling'
type: 'feature'
created: '2026-10-26'
status: 'in-progress'
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
- [ ] `_bmad-output/planning-artifacts/architecture.md`, `architecture/architecture-eventstore-2026-07-05/.memlog.md` beneath planning-artifacts — record approved decisions; correct the obsolete dependency/owner-decision statements without changing unrelated architecture.
- [ ] `C/Routing/` — add declarations, envelope, route/idempotency entries and platform admission models, with a closed versioned field set and explicit missing-data validation. Document canonical digest preimages without self-reference.
- [ ] `C/Routing/RouteCatalogCodec.cs`, `RouteCatalogValidator.cs` — encode/decode/hash using BCL APIs; strict duplicate/unknown-field handling and semantic validation. Keep this catalog-specific.
- [ ] `C/Authorization/EventStoreOperationIdentifiers.cs`, `D/Authentication/EventStoreWorkloadOperations.cs` — centralize identifiers with compatible forwarding constants; declare the existing human permission claim in its authentication owner without altering enforcement.
- [ ] `L/Routing/DevelopmentRouteCatalogBuilder.cs`, `tools/routing-catalog.cs` — provide offline generation/validation from explicit declarations and safe composition bindings, and inspect current Development input gaps. Small file-based developer tool; no new operator CLI/package. Incomplete inspection must report its inventory limits honestly.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Routing/`, `tests/Hexalith.EventStore.Client.Tests/Routing/`, `tests/Hexalith.EventStore.ServiceDefaults.Tests/` — positive/negative fixture matrix, builder/gap diagnostics, real Development configuration coverage, CLI success/failure and operation compatibility. Use existing CI-executed projects.
- [ ] This spec's Verification and the parent [5.12 spec](spec-5-12-route-and-idempotency-catalog-schema-codec-and-envelope.md) — record precise missing declarations and results; full catalog/runtime parity remains unchecked in the parent.

**Acceptance Criteria:**
- Given complete fixtures, when library/tool generation repeats, then bytes/digests agree and every matrix rejection is observed beside a passing control.
- Given actual incomplete Development inputs, when inspection runs, then safe actionable gaps are reported without writing a purported complete catalog or changing runtime behavior.
- Given existing consumers, when affected builds/tests run, then existing operation values and authentication behavior remain compatible.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Baseline captured before code changes: `aspire run --detach --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` exited 0; `aspire describe` reported all listed resources Running/Healthy. The owned AppHost was stopped before implementation to release build resources.

Build affected projects individually in Debug/source mode. Run focused built xUnit executables with single-dash filters, then full affected suites. Verify tool fixture success and real-profile nonzero failure with no output catalog. Record commands/counts/full revision and CI availability. `git diff --check` and `bash scripts/check-committed-files.sh` must pass. Do not stage, commit or push; use a temporary diff including untracked files for review.
