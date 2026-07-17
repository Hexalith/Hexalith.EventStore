---
title: Sprint Change Proposal - Split Story 2.7 Pre-Authorization Correction From Runtime Adoption
status: final
created: 2026-07-17
project: eventstore
mode: batch
scope_classification: moderate
approval: approved
approved_by: Administrator
approved_on: 2026-07-17
implemented_on: 2026-07-17
trigger_story: "2.7"
proposed_adoption_story: "2.12"
---

# Sprint Change Proposal: Split Story 2.7 Pre-Authorization Correction From Runtime Adoption

## 1. Issue Summary

Story 2.7 currently combines two independently gated phases:

1. an EventStore-owned pre-authorization correction to stale sample registrations and the live source-topology query-provenance proof; and
2. a Tenants dependency-identity adoption that is authorized only after Story 1.20 records an owner-approved runtime and package inventory.

The phases have opposite sequencing. The registration/provenance correction is a prerequisite for Story 1.20, while the current AC4-AC7 adoption work is downstream of Story 1.20. Keeping both in Story 2.7 makes its review state ambiguous: the prerequisite correction cannot be reviewed independently without appearing to require an unauthorized gitlink/package migration.

Concrete evidence is recorded in the active Story 2.7 and Story 1.20 artifacts. At EventStore commit `772cdfefa8163704de0f57042af5b0507c1ac771`, the corrected source-topology test reached the real assertion and failed twice with HTTP `404` / `query_projection_missing`. Base EventStore configuration registers `orders` and `inventory`, while the current sample service hosts only `counter` and `greeting`. The absent bindings cause the atomic operational-index load to suppress `admin:query-types:tenants`, so `HandlerAwareQueryRouter` falls back to a nonexistent projection. Fixing that registration/provenance path changes no consumer dependency identity and is required before Story 1.20 can select a candidate runtime.

This is an implementation-slicing correction, not a new product requirement or architectural pivot.

## 2. Impact Analysis

### Epic Impact

- Epic 2 remains valid and in progress.
- Add Story 2.12 as a focused adoption child. No new epic is required.
- Story 2.12 has a cross-epic activation dependency on Story 1.20. It must remain `backlog` until Story 1.20 durably authorizes migration.
- Epic order does not change. Story 2.7 remains the prerequisite correction; Story 1.20 closes its evidence/identity gate; Story 2.12 performs the authorized adoption.

### Story Impact

- **Story 2.7** keeps current AC1-AC2, receives the revised AC3 below, and keeps the current fail-closed scope boundary (current AC8, renumbered after the split). Current AC4-AC7 are removed. Its title becomes **Pre-Authorization Registration And Provenance Correction**.
- **Story 2.12** becomes **Tenants Runtime Identity Adoption And Package-Mode Validation**. It receives current Story 2.7 AC4-AC7 plus an explicit Story 1.20 activation criterion.
- **Story 1.20** retains its behavior and closure order. References that currently call Story 2.7 the owner of both phases must instead name Story 2.7 for the prerequisite correction and Story 2.12 for consumer adoption.
- Story 2.12 should be registered in `epics.md` and `sprint-status.yaml` as `backlog`; its implementation story file should not be created until Story 1.20 authorizes migration, because story-file creation activates ready-for-development semantics in this repository.

### Artifact Conflicts And Required Reconciliation

| Artifact | Impact |
| --- | --- |
| `prd.md` | No change. FR15, FR21, FR22, NFR9, NFR12, and NFR16 already cover the preserved scope. |
| `architecture.md` | No change. AD-22 already separates exact EventStore evidence identity from consumer-repository adoption. |
| `ux.md` | No change. The split changes sequencing and ownership, not UI behavior. |
| `epics.md` | Rewrite Story 2.7, add Story 2.12, and update Epic 2 sequencing/validation references that currently assign both phases to Stories 2.4-2.7. |
| Active Story 2.7 implementation artifact | Remove adoption phase and AC4-AC7; revise AC3; preserve the reproduced blocker, AC1-AC2, and fail-closed boundary. Preserve Story ID 2.7 and add an explicit split reference to Story 2.12. |
| `sprint-status.yaml` | Replace the sole-owner comment with split ownership; keep Story 2.7 at its actual execution status and add Story 2.12 as `backlog`. Do not represent Story 2.12 as active before Story 1.20 authorization. |
| Story 1.20 implementation story and proof packet | Preserve every Story 2.7 source-topology blocker reference; change only statements that also assign later Tenants adoption to Story 2.7. Point those adoption statements and the Tenants adoption register row to Story 2.12. |
| `deferred-work.md` and Story 1.16/1.20 execution spec | Keep the open source-topology blocker owned by Story 2.7. Update only an active path/key if the Story 2.7 artifact is renamed; do not rewrite historical references to the former outbound-header Story 2.7. |
| Historical Story 2.7 references | No change where they refer to the completed outbound DAPR routing-header story that was later renumbered to Story 2.10. Those references are historical evidence, not current adoption ownership. |

### Technical Impact

- Story 2.7 may correct EventStore registration/configuration and the proof harness only. It changes no Tenants, EventStore, or Builds gitlink, package pin, container identity, or approved dependency graph.
- Story 2.12 owns the future source gitlink, package inventory/hash, conditional Gateway source/package policy, and dual-mode validation changes.
- No rollback of completed implementation is required.
- No deployment or runtime topology change is authorized by this proposal itself.

## 3. Recommended Approach

Use **Direct Adjustment**: split the active story within Epic 2 and preserve the existing cross-epic gate.

This is preferable because the code correction and the consumer adoption already have separate authority, evidence, and sequencing. The split removes a circular story lifecycle without changing MVP scope or architecture.

- **Effort:** Low for planning reconciliation; moderate for applying the cross-artifact ownership updates safely.
- **Risk:** Low technical risk; moderate traceability risk because active and historical artifacts both contain references to “Story 2.7.” The implementation must distinguish the current Tenants story from the historical outbound-header identifier.
- **Timeline impact:** Story 2.7 review is unblocked once its pre-authorization criteria pass. Story 2.12 adds no new work; it makes the already-deferred adoption work explicit and leaves its schedule dependent on Story 1.20.
- **MVP impact:** None. Scope is redistributed, not added or removed.

Rollback is not justified: no completed runtime work must be reverted. An MVP review is also unnecessary because the relevant FR/NFR baseline remains achievable and unchanged.

## 4. Detailed Change Proposals

### 4.1 Story 2.7 - Scope And Title

**Section:** Title and purpose

**OLD**

> Story 2.7: Tenants Compatibility And Package-Mode Validation

> This story is the sole Tenants compatibility and adoption owner; no duplicate Tenants-local story is required. Its work has two fail-closed phases.

**NEW**

> Story 2.7: Pre-Authorization Registration And Provenance Correction

> This story owns only the EventStore registration/proof-harness correction required before Story 1.20 can select a runtime. It changes no Tenants, EventStore, or Builds dependency identity. Authorized Tenants source/package adoption is owned by Story 2.12.

**Rationale:** The title and purpose must match the independently reviewable prerequisite work retained in Story 2.7.

### 4.2 Story 2.7 - Revised AC3

**Section:** Acceptance Boundary, AC3

**OLD**

> Given Story 1.20 is blocked, non-authorizing, incomplete, or lacks any required source, package, or approval identity, when this story is reviewed, then it remains `review`, no Tenants/EventStore/Builds gitlink is changed, and existing rollback paths remain intact.

**NEW**

> Given Story 1.20 is blocked, non-authorizing, incomplete, or lacks any required source, package, or approval identity, when AC1, AC2, and the scoped fail-closed boundary are satisfied, then Story 2.7 may enter `review` without changing any Tenants, EventStore, or Builds dependency identity, and existing rollback paths remain intact. Story 1.20 authorization is not a prerequisite for review of this pre-authorization correction.

**Rationale:** Story 2.7 must be reviewable as Story 1.20 prerequisite work. Review entry must neither imply nor require consumer migration.

### 4.3 Story 2.7 - Acceptance Criteria Removed From Scope

**Section:** Acceptance Boundary, current AC4-AC7

**OLD**

> 4. Given Story 1.20 authorizes migration and names the approved EventStore source SHA, when Debug/source mode is adopted, then `references/Hexalith.EventStore` gitlink and checkout both equal that SHA, no EventStore submodule content is edited, and only Tenants-root-declared submodules are initialized.
>
> 5. Given the approved package version and hashes, when Release/package mode restores from an isolated cache, then every resolved `Hexalith.EventStore*` asset is a package at the exact version, fetched bytes match the approved hashes, and the selected Builds commit already exposes that version.
>
> 6. Given Gateway is in the EventStore release manifest, when the dependency graph is aligned, then `Hexalith.EventStore.Gateway` follows the same conditional source/package policy as DomainService and the Release assets contain no mixed Gateway-project/DomainService-package graph or any EventStore project reference.
>
> 7. Given source and package modes are aligned, when validation runs, then Tenants preserves its domain-service, AppHost, and UI registration and passes the focused source/package restore, build, projection/query/provenance/freshness, and package-compatibility evidence.

**NEW**

> Removed from Story 2.7 and preserved as Story 2.12 adoption criteria 2-5 below.

**Rationale:** These criteria mutate or validate the approved consumer dependency identity and are downstream of Story 1.20.

### 4.4 New Story 2.12 - Tenants Runtime Identity Adoption And Package-Mode Validation

**Requirements covered:** FR15, FR21, FR22, NFR9, NFR12, NFR16

**Owner / review boundary:** Amelia (Developer); the Tenants maintainer reviews compatibility, the exact Tenants commit, and exact dependency identities. The EventStore/release-owner approvals remain sourced from Story 1.20 and are not recreated here.

**Focused validation:** Separate Debug/source and Release/package restores/builds; scoped Tenants Contracts, Integration, UI, and Server tests; exact package-byte/hash verification; no mixed source/package EventStore graph.

As a Tenants release maintainer,
I want Tenants to adopt only the owner-approved EventStore runtime identity in source and package modes,
So that consumer migration is reproducible, maintainer-approved, and tied to the exact Story 1.20 evidence.

**Acceptance Criteria:**

1. **Activation gate.** Given Story 1.20 has not durably recorded `final_decision: available`, `authorize_consumer_migration: true`, a 40-hex `tested_runtime_sha`, named EventStore and release-owner approvals, and the approved package version plus SHA-256 inventory, when Story 2.12 activation is evaluated, then it remains `backlog`, no implementation story file is created, and no Tenants, EventStore, or Builds dependency identity changes.
2. **Source adoption.** Given Story 1.20 authorizes migration and names the approved EventStore source SHA, when Debug/source mode is adopted, then `references/Hexalith.EventStore` gitlink and checkout both equal that SHA, no EventStore submodule content is edited, and only Tenants-root-declared submodules are initialized.
3. **Package adoption.** Given the approved package version and hashes, when Release/package mode restores from an isolated cache, then every resolved `Hexalith.EventStore*` asset is a package at the exact version, fetched bytes match the approved hashes, and the selected Builds commit already exposes that version.
4. **Graph alignment.** Given Gateway is in the EventStore release manifest, when the dependency graph is aligned, then `Hexalith.EventStore.Gateway` follows the same conditional source/package policy as DomainService and the Release assets contain no mixed Gateway-project/DomainService-package graph or any EventStore project reference.
5. **Adoption evidence.** Given source and package modes are aligned, when validation runs, then Tenants preserves its domain-service, AppHost, and UI registration and passes the focused source/package restore, build, projection/query/provenance/freshness, and package-compatibility evidence. Completion records the Tenants maintainer-approved commit and exact accepted Tenants SHA.

**Rationale:** This story preserves current AC4-AC7 without allowing them to block or contaminate Story 2.7's pre-authorization review.

### 4.5 Epic And Tracker Sequencing

**OLD**

> Story 2.7 is the sole Tenants compatibility/adoption owner. Its EventStore-owned correction is a Story 1.20 prerequisite; dependency migration remains blocked until Story 1.20 authorizes exact identities.

**NEW**

> Story 2.7 solely owns the EventStore pre-authorization registration/provenance correction and may reach review without dependency-identity changes. Story 2.12 owns Tenants runtime identity adoption and remains backlog until Story 1.20 authorizes the exact source/package identities.

**Rationale:** The planning and execution trackers must expose the actual dependency direction and avoid contradictory status expectations.

### 4.6 Story 1.20 Adoption Register

**OLD**

> Tenants | Existing EventStore Story 2.7, `Tenants Compatibility And Package-Mode Validation` | `review` | Correct the source-topology proof now; after authorization, align the source/package graph.

**NEW**

> Tenants prerequisite | Story 2.7, `Pre-Authorization Registration And Provenance Correction` | active/reviewable before authorization | Correct the stale registration/source-topology proof without changing dependency identities.
>
> Tenants adoption | Story 2.12, `Tenants Runtime Identity Adoption And Package-Mode Validation` | `backlog` | Activate only after Story 1.20 is `available` and authorizes migration; then align and prove the exact source/package graph with Tenants maintainer approval.

**Rationale:** Story 1.20 must continue to depend on Story 2.7 while authorizing, but not executing, Story 2.12.

## 5. Implementation Handoff

### Scope Classification

**Moderate:** backlog reorganization and cross-artifact ownership reconciliation are required, but product scope and architecture remain unchanged.

### Recipients And Responsibilities

- **Product Owner / backlog maintainer:** apply the Story 2.7 rewrite and Story 2.12 backlog entry in `epics.md`; reconcile `sprint-status.yaml`; ensure no duplicate Tenants-local story is introduced.
- **Developer:** complete only Story 2.7 AC1-AC3 and its retained fail-closed boundary; do not modify dependency identities; submit Story 2.7 for review with live handler-routing and `HandlerComputed` provenance evidence.
- **Story 1.20 owner and release owner:** preserve the current fail-closed packet; after Story 2.7 passes, select and verify the exact runtime/package/container identities and record durable authorization.
- **Tenants maintainer / Developer:** only after Story 1.20 authorization, activate Story 2.12, adopt the exact approved identities, and produce source/package proof plus the exact accepted Tenants commit.

### Implementation Order

1. Apply the planning/story split and reconcile active ownership references.
2. Complete and review Story 2.7 without changing dependency identities.
3. Complete Story 1.20 exact-runtime evidence and authorization.
4. Activate and implement Story 2.12.
5. Review Story 2.12 with Tenants maintainer approval and exact dual-mode evidence.

### Success Criteria

- Story 2.7 contains no source/package adoption criteria and can enter review while Story 1.20 is non-authorizing.
- Story 2.12 contains the preserved current AC4-AC7 and an explicit fail-closed Story 1.20 activation gate.
- Story 1.20 still depends on Story 2.7's source-topology correction and points all later consumer adoption to Story 2.12.
- `epics.md`, active story artifacts, and `sprint-status.yaml` agree on IDs, titles, owners, statuses, and dependency direction.
- Active “sole adoption owner” references are removed; historical references to the former outbound-header Story 2.7 remain intact.
- No dependency identity, submodule content, package pin, or deployment artifact changes as part of the planning split.

### Approval And Routing Record

- Approved by Administrator on 2026-07-17.
- Applied to the Epic 2 plan, active Story 2.7, sprint status, and active Story 1.20 ownership references.
- Story 2.7 is routed to Developer review for the pre-authorization correction only.
- Story 2.12 is routed to the Product Owner / Developer backlog and remains inactive until Story 1.20 authorizes migration.
- No Story 2.12 implementation file or dependency-identity change was created by this course correction.

## Appendix A - Change Navigation Checklist

### 1. Understand The Trigger And Context

- [x] 1.1 Triggering story identified: Story 2.7.
- [x] 1.2 Core problem defined: one story combines a prerequisite correction with downstream, separately authorized adoption.
- [x] 1.3 Evidence recorded: reproducible source-topology `404/query_projection_missing`, stale registration mismatch, suppressed Tenants query-type index, and current Story 1.20 fail-closed identity state.

### 2. Epic Impact Assessment

- [x] 2.1 Epic 2 remains completable with a focused split.
- [x] 2.2 Modify Epic 2 by adding Story 2.12; no new epic.
- [x] 2.3 Remaining epics reviewed; only Story 1.20 cross-epic references require reconciliation.
- [x] 2.4 No epic becomes obsolete and no additional epic is required.
- [x] 2.5 No epic resequencing; explicit Story 2.7 -> Story 1.20 -> Story 2.12 dependency order.

### 3. Artifact Conflict And Impact Analysis

- [x] 3.1 PRD reviewed; no requirement or MVP change.
- [x] 3.2 Architecture reviewed; AD-22 already supports the split, so no architecture edit.
- [N/A] 3.3 UX reviewed; no UI flow or evidence-state change.
- [x] 3.4 Other artifacts identified: epics, active Story 2.7, sprint status, Story 1.20 story/proof packet, and narrowly scoped active ownership references.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment is viable; low planning effort, moderate reconciliation effort, low technical risk.
- [x] 4.2 Rollback is not viable or necessary; it provides no simplification.
- [x] 4.3 MVP review is unnecessary; scope and goals remain unchanged.
- [x] 4.4 Direct Adjustment selected for reviewability, authority separation, and long-term traceability.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary completed.
- [x] 5.2 Epic/artifact impact documented.
- [x] 5.3 Recommended path and alternatives documented.
- [x] 5.4 MVP impact and implementation order documented.
- [x] 5.5 Product Owner, Developer, Story 1.20 owner/release owner, and Tenants maintainer handoff defined.

### 6. Final Review And Handoff

- [x] 6.1 Applicable checklist sections addressed; pending actions are explicit.
- [x] 6.2 Proposal reviewed for consistency with PRD, architecture, active story artifacts, and sprint status.
- [x] 6.3 Administrator explicitly approved the proposal on 2026-07-17.
- [x] 6.4 Sprint status and approved story/epic ownership changes reconciled; Story 2.12 remains `backlog` without an implementation file.
- [x] 6.5 Handoff activated for Developer review of Story 2.7 and later Product Owner / Developer activation of Story 2.12.
