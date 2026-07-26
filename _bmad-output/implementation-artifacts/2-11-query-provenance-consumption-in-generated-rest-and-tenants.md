---
created: 2026-07-15
story_id: "2.11"
story_key: 2-11-query-provenance-consumption-in-generated-rest-and-tenants
status: review
split_from: 2-8-query-response-provenance-contract-and-route-aware-gateway-etag
platform_owner: 1-2-domain-query-routing-and-response-provenance
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.11: Query Provenance Consumption In Generated REST And Tenants

Status: review

## Consumer-Only Review Boundary

- Generated REST forwards projection version, lifecycle/freshness, ETag, served-at,
  warnings, and paging only when Story 1.2 supplies valid `ProjectionBacked` evidence.
- `HandlerComputed`, `Unknown`, missing, or invalid provenance omits projection-backed
  headers and renders `Unknown`; no consumer derives lifecycle from ETag, HTTP success,
  payload fields, or SignalR.
- `304` requires a strong gateway-authoritative validator permitted by Story 1.2.
- Persisted-state real-gateway tests must prove evidence origin. Existing platform tests
  remain historical Story 2.8 evidence now adopted by Story 1.2.
- Until Story 4.7 has Tenants maintainer approval, affected Tenants aliases remain
  `Unknown`; this child does not edit the producer.

`done` requires independent consumer-path review plus the Tenants maintainer-approved
PR/commit, exact Tenants SHA, accepted scope, and focused persisted-path evidence.

## Tenants Consumer Evidence Owned By Story 2.11 (owner decision, 2026-07-27)

The 2026-07-27 owner decision retains the exclusive boundary in `planning-artifacts/epics.md:72`.
This story owns all Tenants-UI gateway provenance/lifecycle classification, transport plumbing,
304 policy, and gateway consumer verification. Story 2.6 cites this evidence but does not adopt,
re-prove, or sign it off; Story 2.6 owns only host alignment and presentation of already-classified
state. This supersedes the 2026-07-26 ratified-overlap wording.

Adopted from Tenants commits `56c506c` and `8ab537e`
(`references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs`):

- The `Provenance is not QueryResponseProvenance.ProjectionBacked` gate and fail-closed `Unknown`
  fallback in `ResolveFreshness` and `ResolveNotModifiedFreshness`.
- The lifecycle-precedes-legacy-`IsStale` authoritative selection rule.
- The four call-site rewrites from `result.Metadata?.IsStale == true` to
  `freshness is ReadModelFreshnessState.Stale` (tenant detail, user tenants, global administrators,
  tenant audit).
- The ten supporting theories in
  `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs`.

The 2026-07-27 working-tree patch extends the owned evidence in:

- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs` —
  normalize and transport lifecycle separately from freshness; fail closed on null/untrusted 304
  metadata; and keep retained rows consistent with the resolved state.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs` —
  prove 200 lifecycle precedence across every consuming surface and assert the null/untrusted 304
  matrix through lifecycle, row, visible kind, and reason.

Focused gateway evidence is **191/191 passing** on the uncommitted 2026-07-27 patch. One correctness
finding remains open and blocks this story from leaving `review`: `ResolveFreshness == Current` still
permits `TenantCorrectionStartIntent` to gate on legacy freshness when
`ProjectionLifecyclePolicy.CanMutate` denies mutation. Rows now transport lifecycle, so the missing
intent-policy fix is explicit rather than structurally impossible.

The accepted admin-authored authority chain at `11d69920526f9881ad8c2216b28e82e497543c67`
covers the published baseline (`56c506c`, `8ab537e`, `a0c6d83`). The 2026-07-27 consumer patches are
uncommitted, so this story still requires a new exact Tenants SHA and maintainer approval for them.
