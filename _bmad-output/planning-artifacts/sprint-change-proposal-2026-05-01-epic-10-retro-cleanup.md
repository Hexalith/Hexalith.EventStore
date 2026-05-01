# Sprint Change Proposal - Epic 10 Real-Time Notification Confidence

**Date:** 2026-05-01
**Author:** Bob (Scrum Master)
**Project Lead:** Jerome
**Trigger:** Epic 10 retrospective (`epic-10-retro-2026-05-01.md`) action items R10-A1..R10-A8
**Mode:** Batch
**Scope Classification:** **Moderate** - backlog routing, runtime evidence stories, one security decision, and planning artifact cleanup. No PRD redefinition or architecture replan.

---

## 1. Issue Summary

Epic 10 delivered the SignalR real-time notification layer: signal-only projection change broadcasts, optional Redis backplane wiring, and automatic group rejoin in the client helper. The retrospective found that the implementation direction is sound, but final real-time notification confidence still depends on runtime proof and one tenant-isolation decision.

| ID | Finding | Evidence | Status |
|---|---|---|---|
| R10-A1 | Live SignalR end-to-end topology proof is still required | Retrospective found no active AppHost proof for command/projection update -> ETag regeneration -> SignalR signal -> client re-query -> visible refresh | Routed to existing `post-epic-11-r11a3-apphost-projection-proof` |
| R10-A2 | Redis backplane delivery is structurally configured but not proven in a multi-instance runtime | Story 10.2 verified DI and startup behavior, not client-on-instance-B receiving broadcast-from-instance-A | New runtime proof story |
| R10-A3 | Hub group join lacks tenant-aware authorization decision | `ProjectionChangedHub.JoinGroup` accepts projection type and tenant ID but does not currently check tenant claims before joining | New security decision/implementation story |
| R10-A4 | Planning artifacts use stale hyphen-scoped group/actor ID wording | PRD and epics still reference `{ProjectionType}-{TenantId}` while implementation uses `{ProjectionType}:{TenantId}` | Applied directly by this correction |
| R10-A5 | Client reconnect responsibilities need explicit docs | Rejoin restores future signals; missed signals are not replayed, so clients must query on connect/reconnect | New documentation/sample guidance story |
| R10-A6 | Operational evidence pattern for SignalR latency/reliability is not defined | NFR38 remains focused-test based without an agreed trace/browser/metric proof shape | New QA/DevOps evidence-pattern story |
| R10-A7 | Redis deployment isolation/channel-prefix policy is not settled | Shared Redis could allow cross-environment channel confusion unless deployment isolation or channel prefixing is explicit | New architecture/deployment policy story |
| R10-A8 | Retro follow-through still needs visible backlog tracking | R9/R10 action items should be visible as story rows, follow-up artifacts, or accepted non-action decisions | New tracking reconciliation story |

**Critical before final real-time notification confidence:** R10-A1, R10-A2, R10-A3.

---

## 2. Checklist Execution Summary

| Checklist Section | Status | Notes |
|---|---|---|
| 1. Trigger and context | Done | Trigger is Epic 10 retro findings with concrete story and code evidence |
| 2. Epic impact | Done | Epic 10 remains complete; confidence is blocked by runtime proof and security hardening |
| 3. Artifact conflicts | Done | PRD/epics needed separator normalization; sprint-status needed explicit routing |
| 4. Path forward | Done | Direct adjustment plus targeted follow-up stories is the best path |
| 5. Proposal components | Done | Detailed changes and handoff are below |
| 6. Final review/handoff | Action-needed | Proposal and status edits are prepared; implementation still requires Dev/QA/Architecture execution |

---

## 3. Impact Analysis

### Epic Impact

- **Epic 10 (`done`)** remains valid. The SignalR implementation model is not being rolled back or redefined.
- **Epic 11 / Epic 12 projection and sample UI confidence** are affected because their live proof path includes ETag regeneration, SignalR notification, client re-query, and visible UI refresh.
- **Production readiness** is affected by R10-A2 and R10-A3 until multi-instance delivery and tenant-aware group subscription behavior are proven or explicitly accepted.

### Story Impact

New standalone post-Epic-10 cleanup stories:

| New Story Key | Title | Source Action | Priority |
|---|---|---|---|
| `post-epic-10-r10a2-redis-backplane-runtime-proof` | Prove Redis backplane delivery with multiple EventStore instances and real clients | R10-A2 | High |
| `post-epic-10-r10a3-hub-group-authorization-decision` | Decide and implement tenant-aware hub group authorization or record accepted risk | R10-A3 | High |
| `post-epic-10-r10a5-client-reconnect-guidance` | Document client connect/reconnect query responsibilities | R10-A5 | Medium |
| `post-epic-10-r10a6-signalr-operational-evidence-pattern` | Define SignalR delivery latency and reliability evidence pattern | R10-A6 | Medium |
| `post-epic-10-r10a7-redis-channel-isolation-policy` | Review Redis deployment isolation and channel-prefix policy | R10-A7 | Medium |
| `post-epic-10-r10a8-r9-r10-follow-through-tracking` | Reconcile R9/R10 retro action items into visible tracking or accepted non-action decisions | R10-A8 | Medium |

Existing story remains authoritative and should not be duplicated:

- `post-epic-11-r11a3-apphost-projection-proof` covers R10-A1's live topology proof for command -> projection -> ETag -> SignalR -> UI refresh evidence.

### Artifact Conflicts

- **PRD:** No product-scope change. FR51 and FR55 needed separator normalization from `{ProjectionType}-{TenantId}` to `{ProjectionType}:{TenantId}`.
- **Epics:** No story redefinition. Global FR51 and Story 10.1 text needed the same separator normalization.
- **Architecture:** No replan. Current architecture aligns with SignalR as invalidation and Redis backplane as production distribution support.
- **UX:** No screen-flow change in this proposal. R10-A5 will update guidance/sample expectations for connect/reconnect behavior.
- **Sprint status:** Add post-Epic-10 routing rows and link R10-A1 to the existing R11-A3 proof story.

---

## 4. Recommended Approach

**Direct adjustment with a runtime-proof and security-decision story cluster.**

Rollback is not useful: the SignalR design is coherent and the focused tests already improved disabled mode, ordering, DI wiring, Redis fail-open startup, and reconnect behavior. MVP review is not needed because requirements remain achievable. The gap is proof and production posture, so the right course is to add targeted follow-up stories and fix the planning wording drift.

**Sequence:**

1. Apply this proposal's planning/status edits.
2. Execute R10-A3 before treating tenant-scoped SignalR groups as production-secure.
3. Execute R10-A2 before claiming multi-replica SignalR delivery confidence.
4. Execute or cross-close R10-A1 through `post-epic-11-r11a3-apphost-projection-proof`.
5. Apply R10-A5/R10-A6/R10-A7 as documentation, evidence, and deployment-hardening follow-ups.
6. Use R10-A8 to reconcile R9/R10 action visibility and prevent another manual-only carry-forward.

**Risk level:** Medium until R10-A1/R10-A2/R10-A3 are resolved. If R10-A3 produces an accepted-risk decision instead of code, the risk must name its mitigation explicitly.

---

## 5. Detailed Change Proposals

### Proposal 1 - R10-A1 Existing Proof Routing

```text
Existing Story:
post-epic-11-r11a3-apphost-projection-proof

Instruction:
Treat this story as the authoritative live topology proof for R10-A1. It already requires command submission, event persistence, /project invocation, projection actor write, ETag regeneration, SignalR invalidation, and sample UI refresh evidence.

Rationale:
Creating a second live-topology proof story would split evidence ownership. R10-A1 and R11-A3 need the same running proof chain.
```

### Proposal 2 - `post-epic-10-r10a2-redis-backplane-runtime-proof`

```text
Story: Prove Redis backplane runtime delivery

Acceptance Criteria:
1. Start a topology with Redis backplane enabled and at least two EventStore server instances.
2. Connect a SignalR client to instance B and join a tenant/projection group.
3. Trigger ETag regeneration and SignalR broadcast from instance A.
4. Prove the client on instance B receives the signal and re-queries.
5. Capture Redis/backplane configuration, instance endpoints, client connection target, broadcast origin, and received notification evidence.

Rationale:
Story 10.2 proves wiring; FR56 requires real multi-instance delivery confidence.
```

### Proposal 3 - `post-epic-10-r10a3-hub-group-authorization-decision`

```text
Story: Tenant-aware hub group authorization decision

Acceptance Criteria:
1. Inspect SignalR authentication and claim availability on hub connections.
2. Decide between enforcing tenant claims in JoinGroup or recording an accepted-risk exception.
3. If enforcing, reject JoinGroup when tenantId is absent from authorized tenant claims and add tests.
4. If accepting risk, document why signal-only payloads are sufficient, what mitigates cross-tenant subscription, and when the decision must be revisited.
5. Preserve max-groups-per-connection and colon-reserved validation behavior.

Rationale:
Tenant-scoped group names are not the same as tenant-scoped authorization.
```

### Proposal 4 - R10-A4 Planning Artifact Normalization

```text
Files:
_bmad-output/planning-artifacts/prd.md
_bmad-output/planning-artifacts/epics.md

OLD:
{ProjectionType}-{TenantId}

NEW:
{ProjectionType}:{TenantId}

Rationale:
Implementation and tests use colon-scoped actor/group IDs. Planning text should not reintroduce the old separator assumption.
```

### Proposal 5 - `post-epic-10-r10a5-client-reconnect-guidance`

```text
Story: Document reconnect responsibilities

Acceptance Criteria:
1. Docs state that SignalR is an invalidation signal, not a data replay stream.
2. Clients perform an initial query on connect.
3. Clients re-query after reconnect/group rejoin because missed signals are not replayed.
4. Sample UI guidance names which refresh patterns auto-query and which wait for user action.
5. SignalR helper docs avoid claiming missed-signal catch-up.

Rationale:
Automatic group rejoin restores future notification delivery only.
```

### Proposal 6 - `post-epic-10-r10a6-signalr-operational-evidence-pattern`

```text
Story: Define SignalR operational evidence pattern

Acceptance Criteria:
1. Define minimum proof artifacts for SignalR delivery latency and reliability.
2. Include trace/log/browser evidence linking ETag regeneration to hub broadcast to client receipt to query refresh.
3. Include p99 latency measurement guidance for NFR38.
4. Define how to classify environment failures separately from product failures.

Rationale:
Focused unit/DI tests are valuable but do not prove the runtime delivery budget.
```

### Proposal 7 - `post-epic-10-r10a7-redis-channel-isolation-policy`

```text
Story: Review Redis isolation and channel-prefix policy

Acceptance Criteria:
1. Document whether production guidance requires separate Redis per environment/tenant boundary.
2. Decide whether SignalR Redis backplane channel prefix configuration is required.
3. If required, add or route a configuration story.
4. If not required, document deployment assumptions and isolation boundaries.

Rationale:
Shared Redis deployments need explicit isolation policy to avoid accidental cross-environment signal distribution.
```

### Proposal 8 - `post-epic-10-r10a8-r9-r10-follow-through-tracking`

```text
Story: Reconcile R9/R10 retro action visibility

Acceptance Criteria:
1. List all R9 and R10 action items with current status.
2. Map each item to a story row, follow-up artifact, or accepted non-action decision.
3. Avoid duplicate stories for items already covered by existing post-epic work.
4. Update sprint-status comments or proposal references so future agents can find the routing.

Rationale:
Retro follow-through should not depend on remembering prose buried in retrospective files.
```

---

## 6. Sprint Status Changes

Add this section after the Epic 10 retrospective row:

```yaml
  # Post-Epic-10 Real-Time Notification Confidence (sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md)
  # R10-A1 is routed to post-epic-11-r11a3-apphost-projection-proof because that story already covers command -> ETag -> SignalR -> UI refresh evidence.
  # R10-A4 colon-scoped group/actor ID planning cleanup applied directly in this course correction.
  post-epic-10-r10a2-redis-backplane-runtime-proof: backlog
  post-epic-10-r10a3-hub-group-authorization-decision: backlog
  post-epic-10-r10a5-client-reconnect-guidance: backlog
  post-epic-10-r10a6-signalr-operational-evidence-pattern: backlog
  post-epic-10-r10a7-redis-channel-isolation-policy: backlog
  post-epic-10-r10a8-r9-r10-follow-through-tracking: backlog
```

---

## 7. Implementation Handoff

**Scope:** Moderate.

**Bob / Scrum Master**

- Preserve R10-A1 routing to `post-epic-11-r11a3-apphost-projection-proof`.
- Ensure the R10 cleanup rows remain visible in `sprint-status.yaml`.
- Use R10-A8 to reconcile R9/R10 follow-through without duplicate stories.

**Dev / QA**

- Execute R10-A2 with real Redis backplane and multiple EventStore instances.
- Pair R10-A1 evidence with the existing R11-A3 AppHost proof story.

**Architect / Dev**

- Resolve R10-A3 by enforcing tenant-aware hub group authorization or recording a deliberate accepted-risk decision.
- Resolve R10-A7 deployment isolation/channel-prefix policy.

**Tech Writer / QA / DevOps**

- Complete R10-A5 client guidance.
- Complete R10-A6 operational evidence pattern.

---

## 8. Success Criteria

The course correction is successful when:

1. Sprint status carries the post-Epic-10 cleanup cluster.
2. R10-A1 is discoverably routed to the existing AppHost projection proof story.
3. Redis backplane runtime delivery has a dedicated proof story.
4. Tenant-aware hub group authorization has a dedicated decision/implementation story.
5. Planning artifacts use `{ProjectionType}:{TenantId}` for projection/tenant actor and group IDs.
6. Client reconnect guidance, SignalR operational evidence, Redis isolation policy, and R9/R10 follow-through tracking are visible backlog work.

Bob (Scrum Master): "This is a direct course correction. Epic 10 stands. The next work is runtime proof, tenant authorization, and evidence discipline."
