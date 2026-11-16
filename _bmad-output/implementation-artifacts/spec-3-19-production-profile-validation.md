---
title: '3.19 Production-profile schema and structural validation'
type: 'feature'
created: '2026-11-16'
status: 'done'
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
- [x] `docs/architecture/production-profile-v1.md` — define the closed versioned grammar and digest domains below, including full AD-26 field-to-contract mapping and structural-versus-authority distinction.
- [x] `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfile.cs` and `Deployment/ProductionProfile*.cs` — add contract, section/result/issue types and `ProductionProfileValidationContext.cs` for expected instance IDs; one type per file. Required selections are non-null.
- [x] `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileValidator.cs` — compare expected-instance coverage, unique scoped identities, app/component/physical-target links, per-app actor isolation and credential-reference-only fields; missing context refuses. Collect deterministic path/code issues without logging values.
- [x] `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileCodec.cs` — implement closed parsing, canonical bytes, separate digest computations and expected-digest comparison; reject duplicates before deserialization and capture caller-owned buffers.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Deployment/ProductionProfileTests.cs`, `Deployment/Fixtures/` and the test `.csproj` — add independently calculated golden bytes/digests, matrix mutations and fixture copying; assert structural success never represents qualification or authority.

**Acceptance Criteria:**
- Given complete synthetic inputs, when encoded and decoded, then every declared inventory/binding field survives and independent golden digests match.
- Given a release or environment-binding change alone, when digests are computed, then the resolved digest changes while the infrastructure digest remains unchanged; changing a provider pin changes both.
- Given malformed or under-declared inputs, when validation runs, then it refuses with stable path/reason issues and exposes no raw credential values.
- Given structural validation, when its result is consumed, then it conveys no release availability, production promotion or authenticated qualification.

## Implementation Notes

- Implemented the focused construction slice in the owner Contracts library, with one type per file. All frozen intent remains unchanged. The full parent 3.19 publication/authority story remains incomplete; no production profile was created.
- Preserved baseline `df976e0051b9e2eaa9d3592b3054a0317d83d0cb`. Concurrent external commits advanced HEAD during implementation, including committing the contract types. The retained full baseline diff includes unrelated concurrent changes; `artifacts/production-profile-v1-verification/production-profile.diff` isolates this spec's files. Diff inspection used a disposable private index to preserve concurrent work. The final build-workflow local commit contains only this construction slice and its owner tracking; no remote operation or parent-workspace commit is performed.
- Matrix audit: complete synthetic and unproven rows are covered by `IndependentGoldenBytesAndBothDigestDomainsRoundTripEveryField`; incomplete, malformed and binding mismatch rows by `StructuralMatrixRefusesWithStableSafeReasonsBesidePositiveControl`, `CallerInventoryCannotBeMissingEmptyDuplicateOrUnderDeclared` and `ClosedDecodeMatrixRefusesWithoutEchoingInput`; noncanonical row by the closed-decode theory and canonical-order fact. The initial 80 focused cases ran with zero skips/failures; the review corrective pass expanded this to 129 passing cases. All 233 routing regressions and independent Python vectors passed. Full Contracts verification passed with preserved scratch Git metadata: 1,654 cases, zero failures, two pre-existing release-package-inventory skips. Logs and reproduction script are retained under `artifacts/production-profile-v1-verification/`.

## Spec Change Log

## Review Triage Log

All three review layers completed before triage. Every finding below was fixed and verified in the corrective pass; nothing was deferred. Each accepted finding below is a direct check/test correction on demonstrated reachable state, with no new public surface; route: `patch`. Duplicate reports remain individually recorded and share one implementation fix.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| BH1: OpenBao floor | medium | patch | Architecture inventory requires 2.7.1 / 2.6.4; the comparison accepts a lower self-declared floor. Tighten the existing version predicate. |
| BH2: OpenBao pin | medium | patch | The observed server version is compared only to its floor, so it can contradict its selected provider version. |
| BH3: Actor credential alias | medium | patch | Isolation compares scoped IDs and associations but never the OpenBao logical name/key/generation shared by distinct IDs. |
| BH4: Reverse credential association | medium | patch | Credential component IDs only resolve; an actor credential omitted from the component list bypasses its isolation checks. |
| BH5: Issuer credential binding | medium | patch | Issuer bootstrap checks only reference existence, allowing a token associated exclusively with another app. |
| BH6: DNS target alias | medium | patch | Overlap uses case-insensitive raw host strings; a trailing DNS dot evades the same-target check. |
| BH7: State restore opt-out | medium | patch | Restore coverage trusts Stateful except scheduler/placement, permitting a known state.postgresql component to opt out. |
| BH8: Evidence subjects | medium | patch | Evidence subject digests are required and shaped but never compared with referenced section identities. |
| BH9: Evidence coverage | medium | patch | Section evidence checks association IDs only; arbitrary nonempty coverage passes. |
| BH10: Topology provider roles | medium | patch | Topology provider links only check existence, so PostgreSQL can supply all three management/workload/Rancher selections. |
| BH11: Package/image identity | medium | patch | Application ImageDigest is checked against any artifact kind, including module-package in the fixture. |
| BH12: Validator size | medium | patch | Validate hashes a successful graph without the codec size check; the reviewer reproduced success above 4 MiB. |
| VG1: Component evidence regression | medium | patch | Pre-verified gap: only app evidence association is mutated; removal of component checking remains undetected. |
| VG2: Placement restore regression | medium | patch | Pre-verified gap: the existing omission removes a stateful actor, leaving the independent non-stateful placement requirement untested. |
| VG3: Diagnostic paths regression | medium | patch | Pre-verified gap: code/sort checks never assert field paths, so root-only diagnostics would pass. |
| EH1: DNS target alias | medium | patch | Independently reproduced trailing-dot host alias acceptance; same cause as BH6. |
| EH2: Actor credential alias | medium | patch | Independently reproduced same logical credential under different IDs; same cause as BH3. |
| EH3: Issuer credential binding | medium | patch | Independently reproduced unrelated token as bootstrap; same cause as BH5. |
| EH4: Evidence binding | medium | patch | Independently reproduced unrelated coverage and subjects; same causes as BH8/BH9. |
| EH5: Validator size | medium | patch | Independently reproduced Validate success while Encode refuses size-limit; same cause as BH12. |

## Design Notes

Proposed v1 encoding follows existing owner conventions: strict JSON at the eventual `.yaml` slot, UTF-8 without BOM/whitespace/final LF, ordinal object keys, default BCL string escaping, decimal integers and lowercase SHA-256. Reject comments, duplicate/unknown keys, floats, noncanonical bytes, depth over 32 and documents over 4 MiB. Identity sets sort ordinally by scoped ID; restore-order arrays retain their declared order. Digests never contain themselves.

Require `schemaVersion: 1`, a fixed `purpose: eventstore-resolved-production-profile`, and sections `template`, `infrastructure`, `release`, `environment`, `applications`, `components`, `catalogs`, `protection`, `issuer`, `freshness`, `restore`, `evidence`, plus recomputed `infrastructureDigest`. Infrastructure holds provider/topology/tool pins only. Other sections retain exact runtime/CLI and loaded identities, Scheduler/placement, scopes/ACL/token/resiliency, secret contract/floor and credential references, catalog/AD-34 digests, mandatory append/erasure tuple/writers, replica posture and complete ordered restore coverage. Evidence references retain owners, qualification/source identities and coverage associations; presence is not authentication. The documentation maps every AD-26 requirement to typed required fields before implementation.

Hash infrastructure with purpose `platform-infrastructure-profile`, version 1, template and infrastructure only. Hash the complete resolved document separately; return its digest outside its own preimage. These are new owner profile encodings, not modifications to routing or release-evidence protocols.

## Verification

- Under `run-with-temp`, copy owner sources and required root-declared sibling sources into `$TMPDIR`. Run `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --disable-build-servers`, then `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net11.0/Hexalith.EventStore.Contracts.Tests.dll -class '*ProductionProfileTests'`. Retain commands, nonzero case count and logs outside scratch; wait for children.
- Compare golden bytes and digests with an independent small Python calculation under `run-with-temp`; exercise the full matrix beside a complete synthetic positive control.
- Check unchanged catalog golden vectors and the existing Contracts tests; this scope needs no deployed runtime. All evidence remains construction-only.

### Final construction verification (2026-11-16)

- Command: `run-with-temp bash artifacts/production-profile-v1-verification/verify.sh`.
- Exact required Debug project build with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --disable-build-servers`: zero warnings/errors. Direct xUnit invocation: 129/129 profile tests and 233/233 Routing tests passed; full Contracts run: 1,703 cases, zero failures, two skips because `EVENTSTORE_PACKAGE_CONTRACT_DIR` was not supplied. Independent Python: both canonical-byte vectors and both SHA-256 digests passed.
- Full-suite packaging tests require verified Git history and root-declared siblings; scratch copies preserve local Git metadata and origins, without initializing nested submodules. A corrective run exposed a reused MSBuild worker referencing an already-removed private TMPDIR. The retained reproduction sets `MSBUILDDISABLENODEREUSE=1` and `DOTNET_CLI_USE_MSBUILD_SERVER=0`; the complete rerun above passed. Failed and final logs are retained outside scratch. No production code/build gate was weakened.
- Verified the complete matrix again after review fixes. Additional passing tests assert section subject/semantic evidence associations, component-only evidence omission, placement restore omission, exact safe nested Path/Code pairs, credential aliases/reverse bindings, issuer bootstrap binding, topology/image-kind/version mismatches, DNS aliases and consistent four-MiB refusal. Golden fixtures use 90 distinct synthetic subjects; no evidence is authenticated.
- `git diff --check` passed. Seven approval-bound Platform preparation files match their recorded byte counts and SHA-256 values; the frozen intent is unchanged. The production profile slot remains absent. Parent Story 3.19 remains in progress, with publication, ratification, qualified inventory and issuance work still outstanding.
- Local records: `artifacts/production-profile-v1-verification/{build.log,production-profile-tests.log,routing-tests.log,contracts-tests.log,python-golden.log,boundaries.json,verified-inputs.json,production-profile-final.diff,commitlint.log}`. `verified-inputs.json` binds 37 implementation/document/test files by SHA-256. No CI execution or remote publication is claimed.
