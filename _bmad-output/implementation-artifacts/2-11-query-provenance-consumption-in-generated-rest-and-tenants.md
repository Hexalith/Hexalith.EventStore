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

## Adopted Consumer Evidence From Story 2.6 (owner decision, 2026-07-26)

The Story 2.6 code review (2026-07-26) found that
`2-6-tenants-ui-client-library-alignment-and-ux-evidence` authored and proved Tenants-UI
consumption behaviour that `planning-artifacts/epics.md:72` assigns exclusively to this story.
The owner ratified the overlap instead of reverting it, so this story adopts that work as its
Tenants-consumer evidence.

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

Two open correctness findings against the adopted code are recorded in the Story 2.6 review-findings
section and are 2.11-owned in substance: a `304` carrying lifecycle-only evidence discards a
previously-`Stale` snapshot and renders `Ready`; and `ResolveFreshness` = `Current` gates mutations
that `ProjectionLifecyclePolicy.CanMutate` denies. Both should be resolved before this story leaves
`review`. The Tenants maintainer approval and exact-SHA gates in this story's `done` criteria are
unaffected and remain open — the adopted commits are on Tenants `main` without that approval.
