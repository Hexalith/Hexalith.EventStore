# Sprint Change Proposal — Self-Routing ETag Architecture for Query Pipeline

**Date:** 2026-03-13
**Author:** Jerome (with AI assistance)
**Scope Classification:** Moderate

---

## Section 1: Issue Summary

**Problem Statement:** The `epics.md` planning artifact has been restructured with new functional requirements (FR61-FR64) and updated existing FRs (FR53, FR54) that describe a **self-routing ETag architecture** for the query pipeline. The current implementation (Epic 18, stories 18-1 through 18-6) delivered a **lookup-based ETag system** where the query endpoint must know which ETag actor to call. The new requirements encode the projection type directly in the ETag value, eliminating server-side routing state.

**Discovery Context:** Identified during sprint planning refresh on 2026-03-13. The epics.md was restructured with 4 new FRs and refined descriptions for the query pipeline epic (old Epic 9, now Epic 8). Cross-referencing old story specs against new requirements revealed an architectural delta — not just a reorganization.

**Evidence:**

| FR | Old (Implemented) | New (Required) |
|----|-------------------|----------------|
| FR53 | Query endpoint calls ETag actor by known ID | Endpoint **decodes** projection type from self-routing ETag in `If-None-Match` header |
| FR54 | Query actor cache with state store | In-memory page cache, **no state store persistence** |
| FR61 | *(not specified)* | ETag format: `{base64url(projectionType)}.{guid}` |
| FR62 | *(not specified)* | `IQueryResponse<T>` compile-time `ProjectionType` enforcement |
| FR63 | *(not specified)* | Query actor discovers projection type at runtime from first cold call |
| FR64 | *(not specified)* | Documentation: recommend short projection type names |

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 18 only.** Stories 18-1 through 18-6 remain `done` — they delivered a working lookup-based system.
- Epic 18 status changes from `done` to `in-progress` to accommodate new stories.
- No other epics affected.

### Story Impact

**Existing stories (no changes):**

| Story | Status | Notes |
|-------|--------|-------|
| 18-1 through 18-6 | `done` | Delivered lookup-based ETag system |

**Stories to remove from backlog:**

| Story | Reason |
|-------|--------|
| 18-7-signalr-client-auto-rejoin-on-connection-recovery | Already delivered by old 18-5 (FR59) |
| 18-8-sample-blazor-ui-with-three-refresh-patterns | Already delivered by old 18-6 (FR60) |

**New stories needed:**

| Story | FRs | Description |
|-------|-----|-------------|
| 18-7-self-routing-etag-format-and-endpoint-decode | FR61, FR53 (updated) | Change ETag value from plain GUID to `{base64url(projectionType)}.{guid}`. Update query endpoint to decode projection type from `If-None-Match` header. Malformed ETags = cache miss (safe degradation). |
| 18-8-iquery-response-enforcement-and-runtime-discovery | FR62, FR63, FR64 | Add `IQueryResponse<T>` with mandatory `ProjectionType` field. Query actor discovers projection type from first cold call response. Remove static metadata dependency for runtime routing. Document short projection type name guidance. |

### Artifact Conflicts

| Artifact | Impact | Action |
|----------|--------|--------|
| sprint-status.yaml | Epic 18 needs new story entries | Replace incorrect 18-7/18-8 backlog entries with correct ones |
| epics.md | Already updated (uncommitted) | Commit after sprint status is aligned |
| Architecture | May need update for self-routing ETag design | Review after story specs are created |
| Story files | Old 18-1 through 18-6 unchanged | New story specs needed for 18-7, 18-8 |

### Technical Impact
- **Code changes required.** ETag actor, query endpoint, query actor cache, and client contracts will be modified.
- Self-routing ETags change the HTTP wire format (ETag header value).
- Backward compatibility consideration: clients with old-format ETags should degrade gracefully (cache miss, not error).

---

## Section 3: Recommended Approach

**Selected Path:** Direct Adjustment — add 2 new stories to Epic 18.

**Rationale:**
- The existing lookup-based system works and is complete
- The self-routing architecture is an enhancement, not a fix
- Two focused stories cover the full delta cleanly
- No rollback needed — the existing system remains functional during migration
- Story 18-7 (ETag format) should be implemented before 18-8 (runtime discovery depends on self-routing ETags)

**Effort Estimate:** Medium (2 stories, each medium complexity)
**Risk Level:** Low-Medium (wire format change requires backward compatibility handling)
**Timeline Impact:** Extends Epic 18 by 2 stories

---

## Section 4: Detailed Change Proposals

### 4.1 Sprint Status Update

**File:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

```
OLD:
  epic-18: in-progress
  18-1-etag-actor-and-projection-change-notification: done
  18-2-3-tier-query-actor-routing: done
  18-3-query-endpoint-with-etag-pre-check-and-cache: done
  18-4-query-contract-library: done
  18-5-signalr-real-time-notifications: done
  18-6-sample-ui-refresh-patterns: done
  18-7-signalr-client-auto-rejoin-on-connection-recovery: backlog
  18-8-sample-blazor-ui-with-three-refresh-patterns: backlog
  epic-18-retrospective: optional

NEW:
  epic-18: in-progress
  18-1-etag-actor-and-projection-change-notification: done
  18-2-3-tier-query-actor-routing: done
  18-3-query-endpoint-with-etag-pre-check-and-cache: done
  18-4-query-contract-library: done
  18-5-signalr-real-time-notifications: done
  18-6-sample-ui-refresh-patterns: done
  18-7-self-routing-etag-format-and-endpoint-decode: backlog
  18-8-iquery-response-enforcement-and-runtime-discovery: backlog
  epic-18-retrospective: optional
```

**Rationale:** Replaces incorrect backlog entries (FR59/FR60 already delivered) with correct new work items (FR61-FR64, updated FR53).

### 4.2 New Story: 18-7 Self-Routing ETag Format and Endpoint Decode

**To be created via:** `/bmad-bmm-create-story`

**Scope:**
- FR61: ETag format `{base64url(projectionType)}.{guid}`
- FR53 (updated): Query endpoint decodes projection type from `If-None-Match` header
- Backward compatibility: old-format ETags treated as cache miss
- NFR35: 5ms p99 ETag pre-check (preserved)

**Dependencies:** Stories 18-1 through 18-4 (done)

### 4.3 New Story: 18-8 IQueryResponse Enforcement and Runtime Discovery

**To be created via:** `/bmad-bmm-create-story`

**Scope:**
- FR62: `IQueryResponse<T>` compile-time `ProjectionType` enforcement
- FR63: Query actor discovers projection type from first cold call
- FR64: Documentation guidance for short projection type names
- Query actor projection mapping resets on DAPR idle timeout deactivation

**Dependencies:** Story 18-7 (self-routing ETags must exist for runtime discovery to be useful)

---

## Section 5: Implementation Handoff

**Change Scope:** Moderate — New development work required.

**Handoff:**
- **Scrum Master:** Update sprint-status.yaml (Section 4.1), then create story specs via `/bmad-bmm-create-story` for 18-7 and 18-8
- **Development team:** Implement stories sequentially (18-7 → 18-8)
- **Verification:** All Tier 1 + Tier 2 tests pass after each story

**Success Criteria:**
1. Sprint status reflects correct new stories
2. Story specs created for 18-7 and 18-8 with full acceptance criteria
3. Implementation delivers self-routing ETag architecture per FR61-FR64
4. Backward compatibility with old ETag format (graceful degradation)
5. All existing tests continue to pass
