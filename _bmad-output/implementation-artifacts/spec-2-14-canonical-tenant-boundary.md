---
title: '2.14 Canonical tenant boundary'
type: 'feature'
created: '2026-10-25'
status: 'in-progress'
baseline_commit: '61fb1a68926792c6d32458a62df27d2f8aa6bd7a'
route: 'dispatch'
review_loop_iteration: 0
story_id: '2.14'
story_key: '2-14-canonical-tenant-boundary-in-generated-and-tenants-hosts'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Generated REST, gateway, Admin, and Tenants boundaries disagree on tenant selection and authorization. Several infer scope from grants or synthesize `system`.

**Approach:** Share a Contracts canonicalizer, propagate its result before downstream work, and move platform requests to the authenticated catalog scope. Preserve historical storage identities through an approved migration.

**Owner decisions (2026-10-25):** Complete 5.12 first, limited to its schema, codec, validator and Development/test catalog; activation stays in 5.13. Then make one coordinated SemVer-major explicit-scope transition across controlled consumers. Preserve existing storage identities and writable platform administration. Inventory actual managed-`system` use before retiring it: unused/retired history remains readable; active use requires owner reconciliation, never automatic freezing. Keep this draft pending the prerequisite and that inventory.

**Owner scope amendment (2026-10-27):** The 5.12 prerequisite is accepted declaration/schema/codec delivery and parity of declared executable targets/policies, subject to its package/validation delivery gate. Declaration construction/checking may pass independently of strict runtime certification. Story 5.13 owns runtime catalog authority, rejection of undeclared query/projection/actor targets, exhaustive runtime parity, metadata-outage target stability or rejection, cross-host agreement and activation. Consuming declarations does not require that activation or grant runtime authority. Keep this spec draft/pending: the managed-`system` inventory, owner-approved migration, NFR12 classification and assurance gates remain required; no automatic unblocking follows from this amendment.

**Owner migration decision (2026-11-02):** Apply the recommended source-level mapping and coordinated SemVer-major classification. Public single-tenant reads take the explicit canonical route tenant; cross-tenant/list and global reads take a cataloged platform scope with a narrow human read permission, preserving their existing result-level authorization. Cataloged platform commands retain their declared scope and operation permission. A trusted adapter maps authorized scope to existing writable `system` storage only after boundary validation. Keep legacy keys, topics and separate audit ownership; reject public `system` and mixed scopes. This approves source and test implementation in EventStore and Tenants once deployed managed-`system` use is inventoried and any active use is reconciled. It authorizes neither deployment nor data mutation. G-HIGH-RISK assurance remains required for closure.

## Boundaries & Constraints

**Always:** Apply AD-27 lowercase normalization and its 1–64-character grammar to request tenants and grants. Require one explicit request scope; grants authorize it but never select it. Preserve operation authorization, workload resource bindings, and distinct audit ownership. Consume Story 5.12's schema/codec; Story 2.12 is done. Obtain owner approval of the migration and classify public changes under NFR12 before implementation.

**Never:** Admit public `system`, even for administrators; forward it as request scope; rewrite persisted events; infer platform authority from identity; silently fold 5.12 into this story; change Story 2.15's status identifier contract or Story 5.10's provisioning ownership. No deployment or data migration is authorized here.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure |
| --- | --- | --- | --- |
| Authorized tenant | Explicit `Acme`, grant `ACME` | Route, store and join groups under `acme` | None |
| Invalid scope | Missing, duplicate, conflicting, malformed, unauthorized or reserved tenant | Reject before remote validation, routing, group join or state access | Safe bounded denial |
| Platform operation | Cataloged scope, required operation and admitted credential; channel authentication on internal hop | Authorize explicit platform scope without tenant | Reject mixed scopes or invalid credentials |
| Legacy history | Separate tenant/global histories and topics | Read unchanged, attributable history through approved scope mapping | Ambiguous ownership fails closed |

</frozen-after-approval>

## Deployed Inventory

On 2026-11-02, context `jpiquot@local` had one EventStore/Tenants pair in `ext-secrets-1-isolated`, using `registry-state`. Authenticated Redis `SCAN` found two tenant histories and no `system:tenants:system` key. Dapr state `GET` returned 204 for `projection:tenants:system` and `audit-tenants:system`; the tenant index had no `system` entry. A `system` storage prefix alone does not identify that managed tenant. This verifies absence only in this installation. Inventory each destination before rollout; reconcile any active managed `system` with its owner.

## Code Map

The [investigation](investigations/story-2-14-code-map.md) gives paths/tests. Reuse 5.12 declarations; preserve durable keys; carry canonical scope before actor calls. Tenants `TenantIdentity` and `RestApiAssemblyInfo` separate storage from public scope. Prefixes: `C=src/Hexalith.EventStore.Contracts`, `G=src/Hexalith.EventStore`, `A=src/Hexalith.EventStore.Admin.Server`, `T=references/Hexalith.Tenants`, `M=references/Hexalith.McpCli`.

## Tasks & Acceptance

**Execution:**
- [x] This spec, `T/docs/audit-stream-rollout.md` — record inventory and NFR12 classification.
- [x] `C/Identity/TenantCanonicalizer.cs`, `C/Rest/`, `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` — validate explicit scope and forward canonical values.
- [x] `G/Authorization/`, `G/Controllers/`, `G/SignalRHub/ProjectionChangedHub.cs` — fix callers, precheck, status/replay and groups; retain policies.
- [x] `A/Authorization/`, `A/Controllers/AdminStreamsController.cs`, `A/Services/DaprStreamQueryService.cs` — remove inference; share comparisons.
- [x] `M/src/Hexalith.McpCli.Core/`, `M/src/Hexalith.McpCli.Abstractions/`, `M/tests/` — declare operation platform scope, reject public `system`, and test exact forwarding and conflict refusals.
- [ ] `T/src/Hexalith.Tenants.Contracts/Routing/TenantCatalogDeclarations.cs`, `T/src/` investigation paths — declare read scopes; migrate API, UI, bootstrap, authority, McpCli; preserve identities/audits.
- [ ] `tests/`, `T/tests/`, `.github/workflows/integration.yml`, `T/.github/workflows/ci.yml` — run matrix and Dapr-path legacy readback in CI.

**Acceptance Criteria:**
- Given compiled hosts, when the matrix runs, then scope agrees and invalid input causes zero downstream work.
- Given legacy histories, when platform replay runs, then Dapr readback preserves events, topics and audit owners.
- Given completion review, when NFR12, CI and G-HIGH-RISK pass, then FR12-C2 and FR15's boundary slice close.

## Implementation Notes

- **NFR12 classification:** The explicit request-scope transition is SemVer-major. `RestTenantSource.System` no longer authorizes a public generated request; claim-only REST selection, implicit Admin fallback, status grant enumeration, and public `system` requests change behavior. Tenants API/UI/bootstrap callers must send a cataloged platform scope for platform operations, while single-tenant reads use the canonical route tenant. Durable `system` actor IDs, event keys, topics, and distinct tenant/global audit ownership remain historical storage identities. This classification authorizes no rollout or data mutation.
- **Deployed inventory:** The 2026-11-02 `jpiquot@local` check in the Deployed Inventory section found no managed `system` tenant in that installation. Every rollout destination still needs its own authenticated inventory; any active managed `system` use requires owner reconciliation before deployment.
- **McpCli source migration (approved 2026-11-02):** The McpCli core now accepts an optional operation `PlatformScope`, validates its syntax and conflict with module `FixedTenant`, exposes it in discovery, and forwards it exactly while rejecting a conflicting per-call scope. Tenants source removes module-wide `FixedTenant = system`, declares `platform:tenants` on its nine decorated commands and two list reads, and leaves three single-tenant reads caller-scoped. Global-administrator commands and reads are not currently McpCli-decorated. Direct gateway single-tenant reads now require their canonical request scope and aggregate identifier to agree before authorization. The pinned published Tenants Contracts package still carries the legacy decoration; producer parity requires the coordinated McpCli/Tenants package release and source-to-package conformance proof.
- **Bound bootstrap:** The optional `Tenants:BootstrapBoundWorkloadEnabled` path keeps its existing exact signed command/subject proof. It also forwards the human bearer on the same Dapr request; the gateway independently authenticates that bearer with its installed JWT handler, matches its single subject to the signed principal, and checks the catalog's exact `global-administrators:bootstrap` permission before mapping to retained storage. The delegated-bearer path uses the same catalog permission. Both paths require focused runtime proof before closure.
- **Global audit readback:** The owner catalog now declares `get-global-administrator-audit` for `/api/global-administrators/audit` under `platform:tenants` and exact `tenants:read` human permission. The route binds the historical `system` aggregate inside the trusted adapter and the handler forces the `global-administrators` owner selector. The Tenants REST client routes legacy global-owner requests there; `/api/tenants/system/audit` remains denied. The selected owner reader still validates legacy shared evidence and keeps its distinct audit key. This source-level path needs Dapr readback before closure.
- **Closure evidence:** Runtime Dapr readback of legacy histories, topics and separate audit owners, full CI matrix, package gate, and G-HIGH-RISK assurance are still required before completion. Source-level changes do not close these gates.
- **Source/package compatibility gate:** Tenants CI's published-package lanes pin EventStore packages that predate `RestTenantSource.Platform` and per-route source metadata. Coordinated SemVer-major EventStore and McpCli package delivery, followed by Tenants dependency/pointer and decoration delivery, is needed before those lanes compile and the McpCli enrolled-package vectors can be renewed. The guarded EventStore superproject path resolver now avoids initializing a nested submodule. The focused local Dapr test reaches AppHost startup but refuses the existing backend because `Tenants:DevelopmentRouteCatalog` lacks retained owner custody and a digest ring. Do not invent keys or reset that backend; run the CI-owned disposable backend proof after the package gate.
- **Generated API regression lane:** After fixture migration, source-mode `TenantsApiGeneratedControllerTests` passes 67/67. Public `system`, mixed scope, and missing-scope denials remain explicit assertions.
- **McpCli verification:** Release Core.Tests builds with zero warnings/errors. Focused platform-scope tests pass 15/15; QueryValidationTests 115/115; CatalogTests 60/60. The built CLI passes describe/query/send platform-scope checks 3/3 with exact query request capture. The full Core.Tests run has 731 cases, 725 successes, four platform/benchmark skips and two failures: enrolled coverage rejects the pinned published Tenants package's `FixedTenant = system`, and the fresh catalog benchmark fails with that diagnostic plus a 716 ms construction over its 500 ms limit. The old package cannot qualify this source candidate.
- **Tenants Contracts verification:** Debug source-mode Contracts.Tests builds with zero warnings/errors. McpCli decoration passes 51/51 and query contract naming passes 6/6 after updating the seven-query count. The broader Contracts.Tests attempt was stopped after release-script fixtures failed because `references/Hexalith.Builds/Github/governed-provenance` is absent in the nested checkout; repository guidance forbids initializing that nested submodule.
- **Direct query scope alignment:** `QueriesControllerTests` passes 138/138, including mismatched single-tenant scope/aggregate rejection before authorization and canonical matching of case variants.
- **Server full-suite triage:** The first full Server.Tests run completed 5,724 cases with 27 failures and 25 skips. Story-related failures were migrated to explicit scope and passed in focused reruns (eight classes 354/354; nine additional classes 98/98; terminal-refusal class 26/26). One separate `SecretsProtectionTests` failure reports two paths in the unrelated payload-protection `PolicyLifecycleTests.cs`; that file and its values were left untouched. No clean full-suite rerun or CI pass is claimed.
- **Matrix audit:** Focused canonicalizer, generated-controller, Server, Admin and Tenants tests executed the explicit tenant, denial, and platform-admission rows. The legacy-history row has source-level owner isolation and a compiled CI-covered Dapr test, but that test has not executed successfully. The matrix and story acceptance therefore remain open.

## Spec Change Log

- 2026-11-02: Applied the approved minimal McpCli operation-scope extension and Tenants source decoration; direct single-tenant gateway reads now reject mismatched scope and aggregate before authorization. Published package and live conformance gates remain open.
- 2026-10-27: Applied the owner-approved declaration/runtime split to this frozen prerequisite only. Story 5.13 retains strict runtime enforcement, exhaustive parity and activation, including stable declared targets or rejection during metadata outages. Existing migration/inventory, compatibility, assurance and package gates remain open; status stays draft.

## Verification

Source-mode Debug builds passed for EventStore gateway, Admin Server, Server.Tests, Tenants API/server/UI and Tenants integration. Focused results: canonicalizer 22/22; generated REST 62/62; Admin 82/82; Tenants audit/catalog/bootstrap 329/329; Tenants UI REST 127/127; Tenants generated API 67/67; Server boundary classes 354/354 and 98/98; path resolver 37/37; terminal refusal 26/26. `TENANTS_SOURCE_CATALOG_PROFILE=true dotnet references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/bin/Debug/net10.0/Hexalith.Tenants.IntegrationTests.dll -method Hexalith.Tenants.IntegrationTests.AuthenticatedTenantsRouteTests.SixAuthenticatedDaprRestReadsMatchPersistedState` failed at AppHost startup: `Tenants:DevelopmentRouteCatalog must supply the retained owner custody and digest ring. An empty routing cell does not prove that the business backend is new.` The full Server run and unrelated security-test failure are recorded above. No CI run, live readback, or closure SHA is claimed.
