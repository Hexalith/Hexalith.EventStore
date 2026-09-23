---
title: Phase 4 planning baseline reconciliation before sprint planning
date: 2026-09-23
type: sprint-change-proposal
status: approved-for-handoff
scope: major-planning-reconciliation
readiness_effect: none
sprint_status_effect: none
---

# Phase 4 Planning Baseline Reconciliation

## 1. Issue Summary And Decision Boundary

The trigger is the blocking baseline and lifecycle findings in `prd.md` §§11.3–11.4 and 12, especially OR5, OR6–OR8, OR14–OR15, OR25–OR29. The PRD is final as a requirements document but records implementation readiness `blocked` / `reject`. Epic input hashes, detailed UX status, ownership, and story lifecycle cannot currently be joined into one approved source identity. Sprint planning must use a corrected backlog and explicit gate state, not infer authority from historical `READY`, story `done`, a matching hash, or a passing individual packet.

The user approved this planning proposal on 2026-09-23 for handoff. That conversational approval does not supply a PRD gate-owner signature, an approved baseline manifest, or corrective implementation authorization. Preserve existing reports, sealed packets, superseded receipt trees, and historical observations. Do not mark G-BASELINE or G-READINESS `PASS`, regenerate `sprint-status.yaml`, release, deploy, or authorize consumer removal from this document. Before any corrective implementation handoff, satisfy the PRD §0 content-bound authorization rule (OR28). Editorial planning reconciliation does not itself grant that exception.

### Current evidence checked on 2026-09-23

| Source | Observed state | Consequence |
| --- | --- | --- |
| `prd.md` versus `epics.md` input register | Current PRD SHA-256 `1818a8f10b7c00560a96b3cb0738fcdc81c985de75773475d401729a769e0be9`; epics pins `b99effdb414209da373433d9b4d2075072ca950e89534b22b81155236f650662`. Architecture pin `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51` still matches its current bytes. | PRD input is stale. Repinning alone would repeat the OR14 failure. |
| Detailed UX | `ux.md` and its index say `final`; canonical `DESIGN.md` and `EXPERIENCE.md` say `draft`. Epics pins DESIGN `76be2697…` and EXPERIENCE `06de15b3…`; current digests are `3f4f0181ea24b5ed7544b6cb8482cd73b1aadba7ddbef47fd135e080a2d8365d` and `11f754031cb5f7f8c376787573c32ba9ac0c68c4718e6b4b86281f579e8c9cab`. | UI scope is not a final, approved, content-bound set. |
| UX open questions | `EXPERIENCE.md` assumes restore/import stay hidden and tenant provisioning is absent from Tenants & Access. Story 5.10 assumes a Create Tenant dialog. | Product/UX decisions are required before finalizing routes and acceptance. |
| Architecture | `architecture.md` is `draft`; AD-26 is `[ASSUMPTION]`. AD-10 omits Tenants from the named shared-JWT host set; AD-11/AD-26 do not carry the full PRD publication-state vocabulary. | Architecture owner review and explicit AD-26 ratification or approved replacement must precede epics renewal. |
| Story 6.1 | Canonical `spec-folded-snapshot.md` is `approved-authorized`, its normative SHA-256 recomputes as `0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2`, and its §19 names Jérôme Piquot and authorizes 6.2 for that digest. Wrapper says `done`, tracker says `review`, epics says `backlog` and spec absent; `epic-6-context.md` also calls all three spec artifacts absent. | Reconcile the historical approval and lifecycle sources. Do not call 6.2 delivered; it remains tracker `backlog`. |
| Story 3.15 | The retained validator exits 0 on current subject `7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274` and three receipts. Wrapper says `done`, tracker says `review`, while PRD §11.4 still names failed subject `aafe9040…` and 0/3 receipts. The packet states both owner roles map to one authenticated account and the Test Architect receipt is self-attested. | Update the historical/current distinction and submit the technical result to independent high-risk approval; parity evidence alone grants no release or production state. |
| Story 4.5 | Tracker, wrapper and epics say `done`. Running its retained `validate-evidence.py` exits 1: `EvidenceError: source drifted since the seal: tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`. Epics acceptance explicitly requires `in-progress` when source binding or attribution fails. | Keep the accepted race observation as history; current packet cannot close NFR7/NFR16 or authorize fencing. |
| Stories 5.2–5.4 | 5.2 wrapper `in-review`, tracker `done`, epics `backlog`; 5.3 wrapper/tracker `done`, epics describes `in progress`; 5.4 wrapper/tracker `done`, epics describes `backlog`. | Bring the plan to the supported bounded outcomes after review; Story 5.3 does not close all-host NFR3. |

The two existing dirty worktree files, `deferred-work.md` and `spec-3-15-corrected-deployed-runtime-parity-closure.md`, predate this proposal. Their bytes and the evidence trees are outside this edit.

## 2. Impact Analysis

**Epics and stories.** Epics 2 and 7 need owner-assigned NFR5 and NFR13 slices; Epic 7 also owns the Admin route/UX amendments. Epics 1, 2, 3, 4, 5, and 6 need clause ownership or lifecycle reconciliation. Epic 3's 3.15 packet may support only its bounded FR36-C2 result. Epic 4's 4.5 historical observation remains useful, but its current validation is red. Epic 6's 6.1 spec gate is not a runtime-delivery result. Epic 8 stays committed post-MVP and G5 remains separate.

**Requirements and MVP.** No MVP reduction or expansion is proposed. FR1–FR36, NFR1–NFR18, and every stable PRD §7.1 clause remain in the MVP coverage denominator; FR37/NFR19 remain post-MVP. Missing primary slices are FR36-C3/C4/C5, NFR17-C5, NFR5, NFR13, and NFR18; NFR2, NFR3, and NFR12 have incomplete semantic ownership. The PRD also requires NFR7 append fencing, OQ8 authority, all-host authentication, compatibility, and other failed mandatory gates. Closing planning conflicts alone cannot make readiness `READY`.

**Architecture, UX, and secondary artifacts.** Architecture AD-10, AD-11, AD-26 and their review record; detailed UX and top-level handoffs; the epics Requirements Inventory, FR/NFR maps, affected story sections, and Epic 6 context; story wrappers; tracker; PRD §§11.3–11.4/12; gate and coverage manifests/validators all need ordered reconciliation. Later code, CI, deployment, and evidence work is required by individual gates, but this proposal changes no runtime contract or deployment asset. Existing historical reports remain audit evidence, not current approval.

## 3. Recommended Approach, Effort, And Risk

**Path: direct planning adjustment with guarded corrective successors.** First settle the owner decisions and substantive source conflicts; then update story ownership and lifecycle records from evidence; then bind one approved manifest; then plan corrective implementation against the still-failed gates. No rollback of completed code or evidence is justified by these documentation conflicts. MVP review is unnecessary unless owners choose to change the fixed FR/NFR scope through a separate approved change.

This is a **major planning correction** because product, architecture, UX, epic, tracker, release, security, and test authority intersect. Estimate: several review cycles across the owners for the planning baseline, plus separate implementation/evidence work of unknown duration for the mandatory gates. The critical path is owner decisions → architecture/UX closure → epics/ownership and lifecycle correction → baseline guard and same-digest approvals → sprint planning; a readiness re-run follows only after every remaining gate passes. The main risk is promoting a technically valid but incorrectly attributed or stale story into release/readiness authority. Keep every gate failed until its exact current-subject validator and approval controls pass.

## 4. Detailed Change Proposals

Each entry is an **old → proposed** edit, not an edit made by this proposal. The named owner must review exact wording and evidence before mutation. Preserve accepted historical claims as historical, rather than deleting or relabelling packet bytes.

### A. PRD and baseline register — Product owner, with Test and Architecture owners

1. **§11.3 PRD/epics identity.** Old: epics' pre-update PRD digest is treated as the current input. New: record current PRD digest only in a dated reconciliation row, then bind final settled PRD bytes in the approved manifest after all upstream reviews. Explain that a digest match proves integrity, not approval. Keep `blocked` / `reject`.
2. **§11.3/§11.4 G-RUNTIME-PARITY and OR15.** Old: subject `aafe9040…`, 0/3 receipts and tracker `in-progress`. New: record that this was the 2026-09-10 snapshot; current subject `7d64f87e…` passes the retained technical validator with 3/3 receipts, wrapper `done`, tracker `review`. Require G-HIGH-RISK independent-identity and sealed-CI assessment before treating that gate as authorizing, and retain the four false non-authority flags. Replace the obsolete exact-subject publication-authority path/claim only after the release owner defines an approved current-subject record and validator; do not move receipts between subjects.
3. **§11.3/§12 OR5 and OR15.** Old: 6.1 absent/backlog and 5.4 review/in-progress; 5.3 current tracker/wrapper outcome is narrated as a September 10 snapshot. New: date and separate historical state from current wrapper/tracker/spec findings in the table above. Keep 4.5 current acceptance red, 6.2 backlog, and 4.15 bounded source-only closure.
4. **§11.4 G-BASELINE, G-CLAUSE, G-NFR-OWNERSHIP, G-MVP-COVERAGE.** Old: gate contracts exist but their manifests/validators and complete ownership do not. New: retain `FAIL/BLOCKED` and add the approved correction stories, exact commands, evidence identities, and independent same-manifest approval after they exist. Keep G-READINESS `Reject` throughout this planning pass.

### B. Architecture — Architecture owner, with Security, Release, Deployment, and Test owners

1. **AD-10.** Old named shared-JWT hosts: EventStore, Admin Server Host, Sample API, future hosts. New: include Tenants and generated-host fixtures explicitly in the versioned contract/fingerprint inventory, with a fail-closed conformance gate; retain AD-28 separation and the already defined JWT safety rules. This closes the design ownership gap, not the implementation gap.
2. **AD-11 and AD-26.** Old: immutable candidate and production proof are described without the PRD's complete `built` → `evidence-candidate-published` → `evidence-validated` → `release-available` → `production-promoted` transitions. New: carry the exact state names, independent owner records and predecessor/subject bindings; no candidate/3.15 pass implies release or promotion. Bind AD-26 to the canonical production-profile inventory and its absent-current-file posture.
3. **AD-26 decision.** Old: `[ASSUMPTION]`. New: retain draft until the architecture and deployment owners explicitly ratify the self-managed Kubernetes/PostgreSQL v1 profile or approve a replacement, recording dissent, evidence, exact profile identity, and review disposition. Do not silently convert an assumption to `[ADOPTED]`.
4. **Review closure.** Resolve outstanding substantive reviewer findings against the current spine, retain the append-only architecture history, rerun the relevant review/lint lanes, and obtain explicit owner ratification before epics digest renewal.

### C. Detailed UX and UI story acceptance — Product/UX owner, with Admin and Tenants owners

1. **EXPERIENCE Open Questions.** Old: restore/import hidden under an unconfirmed assumption. Proposed interim decision: keep controls and routes hidden where no approved useful read-only surface exists; product owner confirms that choice or defines the exact safe route, capability state, and acceptance. Update `EXPERIENCE.md`, `DESIGN.md` only where visual treatment changes, `ux.md`, and Story 7.14/7.19/7.20 as applicable after decision.
2. **Tenants & Access.** Old: no tenant-provisioning affordance in UX, while Story 5.10 assumes Create Tenant. Proposed interim decision: keep provisioning absent from this Admin information architecture until the product and Tenants owners approve an authorized flow; revise Story 5.10's dialog acceptance accordingly or explicitly add a guarded route/flow. The final decision must name owner, scope, denial behavior, and evidence.
3. **Finality.** Old: top-level `ux.md`/index `final`, detailed spines `draft`. New: either keep all handoff labels draft while decisions remain open, or finalize detailed DESIGN/EXPERIENCE after the two decisions, current-source review, UX validation, and owner approval, then update top-level handoff. Do not merely change the frontmatter. Preserve the existing review files as reviews of their stated snapshots.
4. **Story 7.14 route contract.** Old: machine-validated route list omits live `/types`. New: add `/types` under Streams & Events, retain events/commands/aggregates inner tabs and one implementation; bind route-manifest validation. This prevents a finalized UX from disagreeing with executable acceptance.

### D. Epics, requirements, and story wording — Epic owner, Product owner, and Test owner

1. **Input register.** Old: stale PRD and detailed UX digests. New: after A–C are approved, perform a substantive PRD §7.1/§11-to-architecture-to-UX-to-epics diff, record each decision, and then pin final SHA-256 values in one approved baseline manifest and the epics frontmatter. Never refresh hashes before the body and ownership review.
2. **Requirements Inventory and maps.** Old: broad FR/NFR text and cross-cutting declarations can conceal missing primary slices. New: mirror every stable §7.1 clause and its all-clauses-required closure rule, assign one disjoint primary owner to FR36-C3/C4/C5 and NFR17-C5, and add approved corrective ownership for NFR2, NFR3, and NFR12's missing semantic slices. Name exact evidence and validator for each; supporting coverage remains supporting.
3. **NFR5, NFR13, NFR18.** Old: `Unassigned`. Proposed assignments for owner approval: a SignalR transport corrective successor of Story 2.8 owns **only** NFR5's 16-entry/2048-byte opaque detail metadata and above-Debug log suppression; a REST generator corrective successor of Story 2.2 owns **only** NFR13's warnings-as-errors/code-style/nullable/ULID/`ConfigureAwait(false)` build quality; a platform documentation story owns NFR18 and `docs/reference/aot-and-trimming-posture.md`. Existing stories are not retroactively marked complete from these assignments. Each successor gets an exact proof command and digest.
4. **Story 6.1 and Epic 6 context.** Old: `epics.md` says spec absent and 6.1 backlog; context says all three specs absent. New: state that the 6.1 spec exists, carries normative digest `0b456b5f…` and named approval, and authorizes only 6.2 for that digest. Update context to distinguish the present 6.1 spec from absent 6.3/6.5 specs. Reconcile tracker review versus wrapper done through the story reviewer; only then set a common 6.1 lifecycle. Leave 6.2 backlog and no Epic 6 runtime-delivery claim.
5. **Stories 3.15, 4.5, 5.2–5.4.** Old epics' `Current reconciliation` prose reflects older snapshots. New: carry the dated current evidence table from §1, and separate bounded historical acceptance from today's validator result. For 4.5, align the lifecycle with its own failed-packet acceptance while preserving the historical FR31 observation. For 5.3, record bounded host completion and open all-host NFR3. For 5.4, record bounded completion and its explicit browser-focus follow-up. For 5.2, await review disposition rather than infer completion from tracker `done`.

### E. Lifecycle and status guard — Story owners, tracker owner, Test owner

| Story | Proposed reconciliation action | Required proof before common status |
| --- | --- | --- |
| 3.15 | Review wrapper `done` versus tracker `review` on subject `7d64f87e…`; update PRD/epics to the bounded result. | Retained parity validator, current role receipts, reviewer disposition, and G-HIGH-RISK control; no release/promotion claim. |
| 4.5 | Treat tracker/epics `done` as non-authorizing under current acceptance; use `in-progress` for current-packet closure until source binding, attribution, and CI guard pass. | Re-sealed current-source packet and `validate-evidence.py` exit 0; preserve original race observation and never rewrite old evidence to imply fencing. |
| 5.2 | Align tracker `done` and epics `backlog` to wrapper `in-review` through a recorded review decision; tracker vocabulary is `review`. | Reviewer sign-off on exact Admin boundary evidence; otherwise remain review. |
| 5.3 | Align epics' old `in-progress` prose to wrapper/tracker `done` for the bounded three-host story. | Retained Story 5.3 closure; separate all-host corrective story and G-AUTH-HOSTS remain failed. |
| 5.4 | Align epics' old `backlog` prose to wrapper/tracker `done` for its bounded scope. | Retained Story 5.4 closure; preserve deferred browser `activeElement` proof. |
| 6.1 | Reconcile tracker `review` with wrapper `done` and approved spec, then update epics and Epic 6 context. | Recomputed normative digest, named approval, review disposition, exact 6.2 authorization; no runtime completion. |

Add a machine-checked transition guard that compares tracker key, wrapper state, epics reconciliation, evidence subject/digest, and the story's acceptance preconditions. It must reject stale subjects, unsupported `done`, contradictory gates, and status edits that merely copy another file. Do not regenerate the tracker as a substitute for item-by-item disposition; preserve its guarded comment blocks and run the named Contracts test if a later approved edit changes them.

## 5. Ordered Handoff And Success Criteria

| Order | Accountable owner | Deliverable and exit check |
| --- | --- | --- |
| 1 | Product owner with Architecture, UX, Epic, and Test owners | Approve the correction boundary and capture the current PRD/architecture/UX/epics/tracker/story/evidence inventory. Identify unchanged historical packets and preserve the two pre-existing dirty files. |
| 2 | Architecture owner with Deployment, Release, Security, and Test owners | Resolve AD-10, AD-11, AD-26 and reviewer findings; explicitly ratify or replace AD-26, retain `draft` until approved. |
| 3 | Product and UX owners with Admin/Tenants owners | Decide restore/import and tenant provisioning; reconcile Story 5.10 and `/types`; validate/finalize detailed UX and top-level handoff against the same source set. |
| 4 | Product and Epic owners with capability owners | Assign all missing primary slices, name corrective successors and proof, mirror §7.1, and update affected story acceptance/current-reconciliation text. Do not widen MVP or pull Epic 8 into it. |
| 5 | Story owners with tracker/Test owners | Disposition 3.15, 4.5, 5.2–5.4, and 6.1 individually; execute exact validators where available; preserve red outcomes; add lifecycle/subject guard before approved tracker mutations. |
| 6 | Test owner with Product, Architecture, UX, Epic, and tracker owners | Build FR/NFR/§7.1 drift and baseline validators, complete the content-bound manifest, check all statuses and evidence identities, obtain approvals over **one** manifest digest. G-BASELINE remains failed until its own validator and approvals pass. |
| 7 | Product owner with every gate evaluator | Plan separately authorized corrective stories for remaining failed mandatory gates, then run sprint planning on the reconciled backlog. Re-run implementation readiness only when every §11.4 mandatory gate and MVP coverage passes; persist a new report rather than reusing 2026-08-01 `READY`. |

**Sprint-planning entry condition:** one approved, content-bound PRD/architecture/detailed-UX/epics/tracker baseline; two UX decisions recorded; complete disjoint primary ownership; reviewed lifecycle dispositions; and explicit failed-gate corrective backlog with owners, dependencies, and safe authorization route. This permits planning of corrective work, not a `READY` or release verdict.

**Readiness exit condition:** every mandatory §11.4 validator and approval passes against the same current source/evidence subject, G-MVP-COVERAGE is total, G-HIGH-RISK controls are satisfied, and the Product owner persists a fresh aggregate report. A Story 3.15 technical pass, Story 6.1 spec approval, or sprint-status edit alone never satisfies it.

## 6. Checklist Disposition

| Checklist area | Status | Record |
| --- | --- | --- |
| 1 Trigger/context | [x] | PRD OR5/OR14/OR15 and the current digest, UX, ownership, and story evidence above. |
| 2 Epic impact | [x] | Epics 1–7 need mapped ownership, acceptance, or lifecycle work; Epic 8 remains post-MVP. No new product epic is required. |
| 3 Artifact conflict | [x] | PRD, architecture, detailed UX, epics, context, wrappers, tracker, validator/coverage evidence; no direct code edit authorized here. |
| 4 Path evaluation | [x] | Direct planning adjustment selected; rollback not supported by evidence; no MVP scope change proposed. |
| 5 Proposal/handoff | [x] | Old → proposed changes, owners, sequence, risks, and success checks above. |
| 6 Approval/implementation | [!] | User approved this proposal; product/architecture/UX/epic/test gate decisions, owner ratification, and implementation remain open. No tracker regeneration or readiness mutation performed. |

## 7. Approval And Workflow Execution Log

- **User decision:** “I approve” received 2026-09-23 in the conversation following presentation of this proposal. Scope: approval of the proposed correction order and handoff. The message is not an authenticated approval of any requirement, architecture decision, UX assumption, story evidence packet, baseline digest, or release/deployment gate.
- **Classification and route:** Major planning correction. Handoff package is this approved proposal for the Product Manager/Product owner and Solution Architect/architecture owner. They coordinate the UX owner, Epic 6 and other epic/story owners, tracker owner, Test Architect, Security, Release, Deployment, Admin, Tenants, and other capability owners named in §5. No external message or repository mutation is implied by this route.
- **Prepared handoff:** §4 contains the proposed artifact edits; §5 gives accountable roles, order, and exit checks. Owner decisions on AD-26, restore/import, and tenant provisioning are still required before those dependent edits can be finalized. PRD §0 authorization remains required before corrective implementation work is handed off.
- **Observed checks:** the Story 3.15 current-subject validator exited 0; the Story 4.5 evidence validator exited 1 on `DaprTestContainerFixture.cs` source drift; the Story 6.1 normative digest recomputed to the approved value. These results are source observations, not a readiness re-run.
- **Workflow effect:** proposal finalized and routed by this record. PRD, architecture, UX, epics, story wrappers, and `sprint-status.yaml` were not edited by this workflow. G-BASELINE and G-READINESS remain failed/blocked as recorded by the PRD pending exact current-subject validation and owner approval.
