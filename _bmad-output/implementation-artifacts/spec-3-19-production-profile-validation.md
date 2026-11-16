---
title: '3.19 Production-profile schema and structural validation'
type: 'feature'
created: '2026-11-16'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'df976e0051b9e2eaa9d3592b3054a0317d83d0cb'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform 4.9 needs an executable EventStore-owned profile contract. The approved draft template/binding design has no final schema, encoding or validator.

**Approach:** Implement the profile-construction slice of Story 3.19: one versioned contract, canonical codec and structural validator, with synthetic vectors. Platform consumes this owner implementation. This slice does not complete 3.19 or establish a qualified inventory.

## Boundaries & Constraints

**Always:** Bind all AD-26 production-proof fields and the approved template/binding split. Require caller-supplied expected app/component IDs from the composing inventory; a profile cannot establish its own complete coverage. Keep infrastructure and resolved digests separate: template plus provider/topology/tool pins versus the complete document. Return structural validity, digest and path/reason issues; authorization and qualification remain unevaluated.

**Never:** Publish `deploy/dapr/production-profile.yaml`, select a target, attest proof, issue lifecycle records, implement signing/approval infrastructure, duplicate Builds' `ReleaseEvidenceCodec`, change catalog codecs or mark either parent story complete. Do not modify the content-bound Platform preparation package or infer permission from a digest.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Complete synthetic | Closed v1 profile with linked instances/references | Structural success and reproducible digests | No authority claim |
| Incomplete | Null target, missing section, instance or reference | Structural refusal | Named path/reason |
| Malformed | Duplicate/unknown keys or IDs, wrong types/version, invalid digest | Refusal | No input-value echo |
| Binding mismatch | Wrong infrastructure digest or dangling app/component link | Refusal | Identify binding |
| Noncanonical | Alternate whitespace/order/encoding or trailing data | Refusal on decode | Encoder produces canonical bytes |
| Unproven | Complete fields with synthetic/unverified evidence references | Structural result only | Qualification remains unevaluated |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/architecture.md`, AD-26/AD-5 — inventory/isolation requirements; target ratification remains pending.
- `_bmad-output/planning-artifacts/epics.md`, 3.19 — construction and issuance gates.
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogCodec.cs` — closed JSON/error conventions; preserve its catalog-specific preimages.
- `tests/Hexalith.EventStore.Contracts.Tests/Routing/RouteCatalogV2Tests.cs` — golden/digest and refusal patterns.
- Platform `docs/contracts/ad26-preparation/{profile-template.draft.json,release-binding-contract.draft.md}` — approved design inputs, not an adopted schema or qualified target; available at `../../../../docs/contracts/ad26-preparation/` relative to this spec.

## Tasks & Acceptance

**Execution:**
- [ ] `docs/architecture/production-profile-v1.md` — define the closed versioned grammar and digest domains below, including full AD-26 field-to-contract mapping and structural-versus-authority distinction.
- [ ] `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfile.cs` and `Deployment/ProductionProfile*.cs` — add contract, section/result/issue types and `ProductionProfileValidationContext.cs` for expected instance IDs; one type per file. Required selections are non-null.
- [ ] `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileValidator.cs` — compare expected-instance coverage, unique scoped identities, app/component/physical-target links, per-app actor isolation and credential-reference-only fields; missing context refuses. Collect deterministic path/code issues without logging values.
- [ ] `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileCodec.cs` — implement closed parsing, canonical bytes, separate digest computations and expected-digest comparison; reject duplicates before deserialization and capture caller-owned buffers.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Deployment/ProductionProfileTests.cs`, `Deployment/Fixtures/` and the test `.csproj` — add independently calculated golden bytes/digests, matrix mutations and fixture copying; assert structural success never represents qualification or authority.

**Acceptance Criteria:**
- Given complete synthetic inputs, when encoded and decoded, then every declared inventory/binding field survives and independent golden digests match.
- Given a release or environment-binding change alone, when digests are computed, then the resolved digest changes while the infrastructure digest remains unchanged; changing a provider pin changes both.
- Given malformed or under-declared inputs, when validation runs, then it refuses with stable path/reason issues and exposes no raw credential values.
- Given structural validation, when its result is consumed, then it conveys no release availability, production promotion or authenticated qualification.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Proposed v1 encoding follows existing owner conventions: strict JSON at the eventual `.yaml` slot, UTF-8 without BOM/whitespace/final LF, ordinal object keys, default BCL string escaping, decimal integers and lowercase SHA-256. Reject comments, duplicate/unknown keys, floats, noncanonical bytes, depth over 32 and documents over 4 MiB. Identity sets sort ordinally by scoped ID; restore-order arrays retain their declared order. Digests never contain themselves.

Require `schemaVersion: 1`, a fixed `purpose: eventstore-resolved-production-profile`, and sections `template`, `infrastructure`, `release`, `environment`, `applications`, `components`, `catalogs`, `protection`, `issuer`, `freshness`, `restore`, `evidence`, plus recomputed `infrastructureDigest`. Infrastructure holds provider/topology/tool pins only. Other sections retain exact runtime/CLI and loaded identities, Scheduler/placement, scopes/ACL/token/resiliency, secret contract/floor and credential references, catalog/AD-34 digests, mandatory append/erasure tuple/writers, replica posture and complete ordered restore coverage. Evidence references retain owners, qualification/source identities and coverage associations; presence is not authentication. The documentation maps every AD-26 requirement to typed required fields before implementation.

Hash infrastructure with purpose `platform-infrastructure-profile`, version 1, template and infrastructure only. Hash the complete resolved document separately; return its digest outside its own preimage. These are new owner profile encodings, not modifications to routing or release-evidence protocols.

## Verification

- Under `run-with-temp`, copy owner sources and required root-declared sibling sources into `$TMPDIR`. Run `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --disable-build-servers`, then `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net11.0/Hexalith.EventStore.Contracts.Tests.dll -class '*ProductionProfileTests'`. Retain commands, nonzero case count and logs outside scratch; wait for children.
- Compare golden bytes and digests with an independent small Python calculation under `run-with-temp`; exercise the full matrix beside a complete synthetic positive control.
- Check unchanged catalog golden vectors and the existing Contracts tests; this scope needs no deployed runtime. All evidence remains construction-only.
