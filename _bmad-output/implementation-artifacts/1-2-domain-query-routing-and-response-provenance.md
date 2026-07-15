---
created: 2026-07-15
story_id: "1.2"
story_key: 1-2-domain-query-routing-and-response-provenance
status: done
supersedes:
  - 1-2-domain-query-handler-routing.md
  - 2-8-query-response-provenance-contract-and-route-aware-gateway-etag.md
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.2: Domain Query Routing And Response Provenance

Status: done

## Reissue Decision

The July 15 approved restructure merges the completed domain-query routing work with the
completed EventStore-owned query-provenance enforcement formerly tracked as Story 2.8.
The historical files above retain the implementation, test commands, reviews, and exact
runtime evidence. This reissue changes planning ownership only; it does not reopen them.

## Preserved Acceptance Boundary

- Domain query handlers are discovered, advertised through operational metadata, invoked
  through `/query`, and fall back to the projection route when no handler is registered.
- The selected route stamps exactly `ProjectionBacked`, `HandlerComputed`, or `Unknown`;
  consumers never infer provenance from ETag, projection type, payload, or HTTP success.
- Handler-computed and unknown routes expose no projection ETag/version/staleness evidence.
  Projection-backed routes may carry only genuine persisted projection evidence.
- `QueryResponseMetadata` remains additive and compatible through the router, handler,
  gateway, and typed client; freshness-dependent requests fail closed when evidence is
  unavailable.
- Focused routing/compatibility tests plus real gateway-path persisted-state evidence are
  required. The completed parent artifacts record these proofs and their reviews.

## Current Boundary

Generated REST and Tenants consumer rendering is Story 2.11. Tenants producer cleanup is
Story 4.7. Neither may weaken or duplicate this completed platform contract.
