# Sprint Change Proposal — Query Response Route-Provenance Contract

- **Date:** 2026-07-07
- **Author / Facilitator:** Amelia (Developer), running Correct-Course
- **Change owner:** Winston (System Architect)
- **Trigger action item:** "Write an explicit handler/projection route-provenance contract before generated REST or UI code treats gateway ETags as projection-backed evidence." (Epic 1 retro Action #1; `sprint-status.yaml` open Epic 1 action; Epic 2 retro Action #7)
- **Mode:** Incremental
- **Scope classification:** Moderate (architecture-contract authoring + backlog reorganization)

---

## Section 1 — Issue Summary

The platform already carries query/projection evidence metadata across the gateway (invariant **AD-14**) and treats projection notifications as freshness signals only (invariant **AD-8**). Stories 1.2, 1.3, 2.2, and 2.5 built and merged the end-to-end `QueryResponseMetadata` pipe (`ETag`, `IsStale`, `ProjectionVersion`, `ServedAt`, `Paging`, `WarningCodes`).

What was never written is a **per-route provenance contract**: nothing labels a query response as **projection-backed** vs **handler-computed**, so evidence fields cannot be trusted differentially.

**Problem statement.** Because provenance is undefined:

- The gateway ETag is, by construction, a random-GUID change token (`SelfRoutingETag.GenerateNew` → `Guid.NewGuid()`, `ETagActor.cs:30-43`) keyed only by `(projectionType, tenant)`. It carries zero version/freshness meaning.
- `QueriesController` back-fills that projection-type ETag onto **handler-computed** responses by name fallback (`QueriesController.cs:152-164`: `result.ProjectionType ?? request.ProjectionType ?? request.Domain`), even though `HandlerAwareQueryRouter` intentionally leaves `ProjectionType` null for handler routes.
- A domain producer (Tenants) aliases `ProjectionVersion := ETag` (`references/Hexalith.Tenants/.../TenantQueryResult.cs:23-29, 50-54`).

Consequently a generated REST controller (Story 2.2 headers/304) or a UI freshness indicator (the UX "Projection freshness indicator" / Tenants stale badge) can render a gateway-level ETag or fabricated version as **projection-confirmed "current/stale" evidence** — an unfalsifiable trust claim. This must be nailed down before further UI/generated-API consumers (Epics 5–7, the deferred D6 handoff, and the REST-generator-hardening backlog) build on the metadata.

**Issue type:** architectural-contract gap / latent correctness-and-trust defect. Not a technical limitation, pivot, or misunderstanding.

**Discovery / evidence trail.**

- Born as a **deferred medium finding on Story 1.2** (`spec-1-2-domain-query-handler-routing.md:139, 151`; `deferred-work.md:7-9`).
- Promoted to **Epic 1 retro Action #1** (`epic-1-retro-2026-07-07.md:145-147`, Critical Path `:179`).
- Re-confirmed as **Epic 2 retro Action #7** ("Reconcile gateway metadata provenance…").
- The consumer side is specced: UX "Projection freshness indicator" (`EXPERIENCE.md:103, 65`; `DESIGN.md:225`) and generated 304/projection-version headers (`spec-2-2:38-39`).

---

## Section 2 — Impact Analysis

**Epic impact.**

- Epics 1 and 2 are `done`. The originating epic cannot "finish" the item; it was accepted and carried forward precisely so downstream epics don't build on undefined provenance. This is the "before" in the trigger.
- **No epic is invalidated.** The metadata pipe (AD-14) stands; this adds a provenance *label* on top of it — additive, not a rework.
- **Epic 4** gains one implementation story (4.7). **Epics 5 and 7** (Admin/tenant UI, Story 7.2 UI honesty) are gated: any new story rendering current/stale/version evidence must cite AD-15 or avoid projection-backed claims for handler routes.

**Story impact.**

- New **Story 4.7** (Epic 4) — the route-aware gateway + provenance label + guardrail tests.
- Shipped Epic 2 stories (2.2, 2.4) are **reconciled by Story 4.7 guardrails**, not rewritten (they are `done`).
- Epic 3 Story 3.2 (DAPR ETag timeout) is ETag-adjacent but orthogonal — one cross-reference, no rewrite.

**Artifact conflicts.**

- **Architecture** — new invariant AD-15; AD-14 line hardened; conventions row updated; capability map rows updated. (Core deliverable.)
- **PRD** — FR4 and NFR8 gain a provenance clause. MVP unaffected.
- **Epics** — FR4/NFR8 inventory mirror; new "Query Response Provenance Gate"; Story 4.7.
- **UX** — "Projection freshness indicator" bound to provenance; AD-15 traceability row.
- **deferred-work.md** — provenance entry reconciled; stale HIGH note reconciled; D6 handoff kept decoupled.
- **sprint-status.yaml** — Story 4.7 added; the two provenance action items closed with notes.

**Technical impact (implementation, handed off via Story 4.7).**

- Add a provenance classification on `QueryResult` / `QueryRouterResult` → `EventStoreQueryResult`.
- Make `QueriesController` route-aware: no projection ETag/version/freshness for non-`ProjectionBacked` responses.
- Tenants conformance fix (stop aliasing `ProjectionVersion := ETag`) — **submodule change requiring explicit maintainer approval**.
- Guardrail tests on the real gateway path (Tier 2/3, AD-12/NFR16).

---

## Section 3 — Recommended Approach

**Option 1 — Direct Adjustment. [Selected]** Write the invariant into `architecture.md`, propagate thin clauses into PRD/UX/epics, add one Epic 4 implementation story with guardrail tests, add a provenance gate, reconcile the deferred-work ledger. Effort: Medium. Risk: Low. The metadata pipe already exists, so this is additive.

**Option 2 — Rollback. [Rejected]** Nothing to roll back; the pipe is correct. The gap is an unwritten contract, not wrong code.

**Option 3 — MVP Review. [Rejected]** MVP scope is unaffected; freshness UI is already in scope via FR15/NFR8/UX. No goals change.

**Rationale.** The trigger literally asks to *write a contract*; the correct home is an architecture invariant (AD-15) that constrains AD-14's existing merge rules. Implementation is a small, well-bounded gateway change plus one submodule conformance fix, tracked as Story 4.7 so the "write" and "enforce" halves are separable. Decoupling the D6 read-model-freshness handoff keeps this change small and honest: a route stays `HandlerComputed`/`Unknown` (consumers render `unknown`) until it sources genuine freshness.

**Decisions taken (with the user):**

1. Implementation story placement → **new Story 4.7 in Epic 4** (evidence-integrity epic; backlog, no active-story disruption).
2. D6 read-model-freshness handoff → **decoupled, stays deferred**.
3. B5 action-item status → **marked `done`** (the write-the-contract action is fulfilled by AD-15; enforcement tracked as Story 4.7).

---

## Section 4 — Detailed Change Proposals

All edits below are **applied** to the planning/implementation artifacts as part of this proposal.

### Architecture (`architecture.md`)

- **AD-15 — Query Response Provenance Is Explicit And Route-Bound** (new invariant, after AD-14). Defines the `ProjectionBacked | HandlerComputed | Unknown` classification and five rules: ETag is an opaque cache validator (never version/freshness evidence); the gateway must not attach a projection ETag/version/freshness to non-`ProjectionBacked` responses (kills the handler-route name fallback); `ProjectionVersion`/`IsStale` authoritative only for `ProjectionBacked` sourced from `IReadModelFreshness` (no `ProjectionVersion := ETag`); consumers render `Current`/`Stale` only for `ProjectionBacked`, else `Unknown`; guardrail evidence is persisted-path, not mock.
- **AD-14 hardened** — the "gateway may fill ETag from the strong validator" rule now reads "only for `ProjectionBacked` routes and only as an opaque cache validator — never as projection-version or freshness evidence (see AD-15)."
- **Consistency Conventions** "Cursors and ETags" row — adds "An ETag is a cache validator, not projection evidence; version/freshness claims require `ProjectionBacked` route provenance (AD-15)."
- **Capability→Architecture map** — AD-15 added to the FR1-FR10, FR11-FR16, and FR34-FR35 rows.

### PRD (`prd.md`) and Epics inventory (`epics.md`)

- **FR4** — appends: "carrying an explicit query-response provenance classification (projection-backed, handler-computed, or unknown) that governs whether that evidence is projection-backed."
- **NFR8** — appends: "freshness/version evidence is authoritative only for query responses whose route provenance is projection-backed, and handler-computed or unknown-provenance responses must not be presented as current or stale."

### Epics (`epics.md`)

- **Query Response Provenance Gate** (new execution gate) — no new UI/generated-API story rendering current/stale/version may proceed until it cites AD-15 or avoids projection-backed claims for handler routes; Story 4.7 owns the contract; Epic 2 stories reconciled by 4.7 guardrails.
- **Story 4.7: Query Response Provenance Contract And Route-Aware Gateway ETag** (Epic 4) — full ACs for handler-route (`HandlerComputed`, no projection ETag), projection-route (`ProjectionBacked`, genuine version/freshness), Tenants alias removal (submodule approval noted), and consumer rendering rules. Sequencing note keeps D6 out of scope.

### UX (`EXPERIENCE.md`, `DESIGN.md`)

- **Projection freshness indicator** now renders `current`/`stale` only for projection-backed provenance; handler-computed/unknown render `unknown`.
- New **AD-15** traceability row in EXPERIENCE.md; DESIGN.md component note updated.

### Deferred-work (`deferred-work.md`)

- Route-provenance entry marked reconciled; new dated reconciliation section owns the handoff to AD-15/Story 4.7; the 2026-07-04 HIGH freshness-header note reconciled (metadata-field half fixed by Story 1.2, provenance half owned by 4.7); D6 handoff explicitly kept as a separate deferred platform item.

### Sprint status (`sprint-status.yaml`)

- `4-7-query-response-provenance-contract-and-route-aware-gateway-etag: backlog` under epic-4.
- Epic 1 provenance action → `done` (+ note pointing to AD-15 / Story 4.7 / gate).
- Epic 2 retro Action #7 → `done` (+ reconciliation note).

---

## Section 5 — Implementation Handoff

**Scope: Moderate** — architecture-contract authoring plus backlog reorganization. Planning-artifact edits are applied by this proposal; code enforcement is handed off.

**Story Rewrite Gate (mandatory) — evaluated and satisfied.** No **active** story file requires an old→new rewrite: Epics 1–2 are `done`; Epics 3–7 stories are `backlog` (no story files yet). Forward impact is carried by the new Query Response Provenance Gate + AD-15; Epic 2's shipped behavior is reconciled by Story 4.7's guardrails. No active AC is superseded.

**Handoff.**

| Recipient | Responsibility |
| --- | --- |
| Winston (System Architect) | Owns AD-15 wording; confirms the contract is final. Reviews Story 4.7 gateway-provenance design. |
| John (Product Manager) | Owns Story 4.7 placement and the Query Response Provenance Gate; sequences it before Epic 5/7 freshness-UI stories. |
| Amelia (Developer) | Implements Story 4.7 when scheduled: provenance label on the query result chain, route-aware `QueriesController`, guardrail tests on the real gateway path. |
| Sally (UX Designer) | Confirms the freshness-indicator provenance binding and normalizes the component across the DESIGN/EXPERIENCE spines. |
| Tenants maintainer | Approves the submodule conformance fix (remove `ProjectionVersion := ETag`) before it lands. |

**Success criteria.**

- Architecture carries AD-15 and the hardened AD-14 rule (done).
- A handler-route query response provably carries no projection ETag/version on the real gateway path; a projection-backed route carries a genuine one (Story 4.7 tests).
- No producer aliases `ProjectionVersion := ETag`; guardrail prevents reintroduction.
- Any new UI/generated-API freshness story cites AD-15 or avoids projection-backed claims for handler routes.

---

## Appendix — Key Evidence References

- `architecture.md` — AD-14 (query evidence merge rules), AD-8 (freshness signal), new AD-15.
- `QueriesController.cs:152-164` — handler-route projection ETag name-fallback (the gateway conflation site).
- `ETagActor.cs:30-43` / `SelfRoutingETag.GenerateNew` — ETag is a random-GUID change token, not a version.
- `ReadModelFreshnessExtensions.ToQueryResponseMetadata` — the correct genuine-provenance source.
- `references/Hexalith.Tenants/.../TenantQueryResult.cs:23-29, 50-54` — `ProjectionVersion := ETag` aliasing (the producer conflation site).
- `spec-2-2:38-39` — generated 200/304 header emission (kept ETag and projection-version separate; not the conflation source).
- `deferred-work.md:7-9, 39, 51` — provenance and freshness-header ledger entries.
- `epic-1-retro-2026-07-07.md:145-147, 179` — origin of the action item.
