---
title: '2.14 Canonical tenant boundary'
type: 'feature'
created: '2026-10-25'
status: 'draft'
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

## Open Questions

- Managed-`system` inventory — **Absent/retired:** proceed. **Active:** provide environment and approved reconciliation. **Unknown:** retain draft.

## Code Map

The [investigation](investigations/story-2-14-code-map.md) lists callers/tests. Prefixes: `C=src/Hexalith.EventStore.Contracts`, `G=src/Hexalith.EventStore`, `T=references/Hexalith.Tenants`.

- `C/Routing/PlatformAdmissionDeclaration.cs` — reuse 5.12; `C/Identity/AggregateIdentity.cs` preserves keys.
- `G/Authorization/ClaimsTenantValidator.cs`, `ActorTenantValidator.cs`, `TenantValidationResult.cs` — bypass, raw call, missing canonical result.
- `T/src/Hexalith.Tenants.Contracts/Identity/TenantIdentity.cs`, `T/src/Hexalith.Tenants.Api/RestApiAssemblyInfo.cs` — storage and public `system` scope.

## Tasks & Acceptance

**Execution:**
- [ ] This spec, `T/docs/audit-stream-rollout.md` — record inventory, reconciliation and NFR12 classification.
- [ ] `C/Identity/TenantCanonicalizer.cs` (new), `C/Rest/RestTenantSource.cs`, `C/Rest/RestApiAttribute.cs`, `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` — validate explicit scope before dispatch and forward canonical values.
- [ ] `G/Authorization/`, `G/Controllers/`, `G/SignalRHub/ProjectionChangedHub.cs` — fix inventoried callers, actor precheck, status/replay and groups; retain separate policies.
- [ ] `A/Authorization/AdminTenantAuthorizationFilter.cs`, `A/Controllers/AdminStreamsController.cs`, `A/Services/DaprStreamQueryService.cs` — remove inference and share comparisons.
- [ ] `T/src/Hexalith.Tenants.Contracts/Routing/TenantCatalogDeclarations.cs` and `T/src/` investigation paths — declare read scopes; migrate API, UI, bootstrap, authority and McpCli; preserve identities/audits.
- [ ] `tests/`, `T/tests/`, `.github/workflows/integration.yml`, `T/.github/workflows/ci.yml` — run matrix and Dapr-path legacy readback in CI.

**Acceptance Criteria:**
- Given compiled controllers and hosts, when the matrix runs, then scope and denial agree with zero downstream work on invalid input.
- Given legacy histories, when platform access/replay runs, then Dapr readback preserves events, topics and distinct audit owners.
- Given completion review, when migration, NFR12 and CI evidence passes G-HIGH-RISK assurance, then FR12-C2 and FR15's boundary slice close.

## Implementation Notes

## Spec Change Log

- 2026-10-27: Applied the owner-approved declaration/runtime split to this frozen prerequisite only. Story 5.13 retains strict runtime enforcement, exhaustive parity and activation, including stable declared targets or rejection during metadata outages. Existing migration/inventory, compatibility, assurance and package gates remain open; status stays draft.

## Verification

Build Debug projects; run xUnit suites in the investigation. Record commands, counts, SHA and CI links.
