---
title: '2.14 Canonical tenant boundary'
type: 'feature'
created: '2026-10-25'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_id: '2.14'
story_key: '2-14-canonical-tenant-boundary-in-generated-and-tenants-hosts'
context:
  - _bmad-output/implementation-artifacts/epic-2-context.md
  - _bmad-output/implementation-artifacts/investigations/story-2-14-code-map.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Generated REST, gateway, Admin, and Tenants boundaries disagree on tenant selection and authorization. Several infer scope from grants or synthesize `system`.

**Approach:** Share a Contracts canonicalizer, propagate its result before downstream work, and move platform requests to the authenticated catalog scope. Preserve historical storage identities through an approved migration.

**Owner decisions (2026-10-25):** Complete 5.12 first, limited to its schema, codec, validator and Development/test catalog; activation stays in 5.13. Then make one coordinated SemVer-major explicit-scope transition across controlled consumers. Preserve existing storage identities and writable platform administration. Inventory actual managed-`system` use before retiring it: unused/retired history remains readable; active use requires owner reconciliation, never automatic freezing. Keep this draft pending the prerequisite and that inventory.

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

## Code Map

Exact paths, callers, tests and migration inventory are in [the investigation](investigations/story-2-14-code-map.md). Path prefixes below are repository-relative: `C=src/Hexalith.EventStore.Contracts`, `G=src/Hexalith.EventStore`, `A=src/Hexalith.EventStore.Admin.Server`, `T=references/Hexalith.Tenants`.

- `C/Identity/AggregateIdentity.cs` owns durable keys and legacy topics; retain historical identity support.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` resolves raw/claim/system scope; generated positive fixtures currently lack authentication.
- `G/Authorization/ClaimsTenantValidator.cs` bypasses validation for administrators; actor validation needs a local precheck. Gateway packaging links this source.
- `G/Authorization/AdminTenantAuthorizationFilter.cs` and `A/Authorization/AdminTenantAuthorizationFilter.cs` default missing scope from the first grant.
- `T/src/Hexalith.Tenants.Contracts/Identity/TenantIdentity.cs` addresses platform storage; changing its constant would select different histories.

## Tasks & Acceptance

**Execution:**
- [ ] This spec and `T/docs/audit-stream-rollout.md` — after 5.12, inventory deployed managed-`system` use, record migration/classifications and reconcile its policy. Keep platform administration writable on existing storage; no active tenant retirement is approved.
- [ ] `C/Identity/TenantCanonicalizer.cs` (new), `C/Rest/RestTenantSource.cs`, `C/Rest/RestApiAttribute.cs` — define shared boundary validation and approved explicit-scope metadata using 5.12 declarations.
- [ ] `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` — validate selected scope/grants and forward canonical values; add compiled matrix tests.
- [ ] `G/Authorization/`, `G/Pipeline/AuthorizationBehavior.cs`, `G/Controllers/`, `G/SignalRHub/ProjectionChangedHub.cs` — update inventoried callers, including actor prechecks, status and replay tenant discovery; preserve their separate operation/identifier policies.
- [ ] `A/Authorization/AdminTenantAuthorizationFilter.cs`, `A/Controllers/AdminStreamsController.cs`, `A/Services/DaprStreamQueryService.cs` — remove inferred scope and share canonical comparisons.
- [ ] `T/src/` paths inventoried in the investigation — migrate API, UI gateway, bootstrap, authority queries and McpCli metadata together; preserve legacy storage/topic mappings and audit separation. Any missing shared McpCli capability requires its owner's contract before adoption.
- [ ] `tests/` suites in the investigation and `T/tests/` — cover every matrix row, canonical propagation, zero downstream work and unchanged persisted histories. Bind runtime tests into `.github/workflows/integration.yml` and Tenants' owning CI; retain existing deterministic CI lanes.

**Acceptance Criteria:**
- Given all boundary implementations, when the same matrix runs through compiled controllers and real host paths, then they agree on scope and denial and no invalid request reaches downstream work.
- Given legacy histories, when approved platform access/replay runs, then Dapr-path readback proves preserved events, topics and separate tenant/global attribution.
- Given completion review, when the migration, NFR12 classifications and test/CI results are evaluated, then FR12-C2 and FR15's boundary slice close only with the required G-HIGH-RISK assurance.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Approved direction separates request scope from durable identity: an authorized catalog mapping selects existing storage. The shared `system` partition holds all Tenants aggregates; it must remain writable for platform administration. A managed tenant literally named `system` is a separate inventory item. No blanket rename, audit union or public alias. Scope literals/credential vocabulary come from 5.12.

## Verification

Build affected projects individually in Debug/source mode; run focused built xUnit executables, then their full suites. The investigation lists projects and CI gaps. Record commands, counts, full commit SHA and CI link here under the October 23 evidence policy; do not restore deleted authorization machinery. Planning checks: `git diff --check` and `bash scripts/check-committed-files.sh`.
