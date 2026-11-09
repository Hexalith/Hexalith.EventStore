---
title: 'Readiness recovery: append delivery handoff and planning contract reconciliation'
date: '2026-11-09'
status: 'applied'
mode: 'batch'
approval: 'Owner approved P1-P6 with approve on 2026-11-09; planning amendments applied'
baseline_commit: '51700ffb1542bee1f64d12f6ad2d9bf2c988fe78'
change_scope: 'moderate; existing backlog and planning contract correction'
---

# Sprint Change Proposal — Readiness Recovery

## 1. Issue Summary

The latest user-supplied sprint-planning assessment returned **FAIL** before regenerating tracking. Three connected blockers remain: Story 4.16 has no qualified Dapr capability implementing its approved append contract or accepted Platform execution delivery; all five epic input digests differ from current sources; and architecture/story contracts lag settled PRD and UX decisions.

This proposal continues the already-applied [November 9 Dapr/Platform ownership correction](sprint-change-proposal-2026-11-09.md). Its P1–P5 approvals are retained. The [November 1 atomic conflict-protection scope and erasure-custody decision](sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md) also remain accepted. Neither decision qualified a runtime, secured an upstream commitment, obtained Platform acceptance, or authorized infrastructure execution. This proposal does not ask the owner to approve either decision again.

The review mode is Batch, selected by the user during this workflow. The owner replied “approve” to the complete P1–P6 proposal on 2026-11-09. The accepted planning amendments and written handoff are applied, as recorded in Approval Application below; infrastructure execution and remaining implementation are not authorized by that planning receipt. The default dated output already contains the applied ownership correction, so this separate suffixed proposal preserves that record.

### Evidence At The Inspected Baseline

| Finding | Current evidence | Consequence |
| --- | --- | --- |
| Missing append primitive | [4.16 specification](../implementation-artifacts/spec-4-16-append-write-once-conformance-through-enforced-envelope.md), Capability Dependency And Execution Gate, Current Qualification Ownership, and November 9 handoff recheck. The recorded Dapr 1.19.1 / components-contrib 1.19.0 / PostgreSQL v1 source screen fails mandatory revision, create-once, whole-batch collision rejection and protected-erasure semantics. | G-APPEND stays FAIL/BLOCKED. A source screen, observational race-test PASS, runtime refresh or broker qualification supplies no prevention proof. This run performs no new upstream capability search or live test. |
| Unaccepted execution delivery | Applied ownership proposal §5 explicitly leaves Platform acceptance and implementation pending. Platform's [test-environment v1 contract](../../references/Hexalith.Platform/docs/contracts/test-environment-v1.md) distinguishes schema publication from runner adoption, Platform acceptance and integration qualification. | A written assignment is insufficient to start a qualification lane. Exact owner acceptance and supported handoff inputs remain required. |
| Five stale epic bindings | The five SHA-256 comparisons in Appendix A all mismatch. Architecture is `draft`; detailed DESIGN and EXPERIENCE and the top-level UX handoff are `final`. | G-BASELINE stays FAIL/BLOCKED. UX finality resolves the older draft-status assertion only; it supplies no baseline approval or readiness. |
| Settled security decision absent from architecture | [PRD NFR1](prd.md#7-cross-cutting-non-functional-requirements) permits the enumerated UI asset/callback exception class; AD-16 still permits only probes. Architecture's status table and Story 5.14 still describe a product-owner decision as pending. | Reconcile architecture and story acceptance to the existing product decision. Preserve the implemented non-UI security milestone and remaining parent-story blockers. |
| Route omission | [EXPERIENCE](ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md#handoff-boundaries) retains `/types` under Streams & Events; [TypeCatalog.razor](../../src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor) maps the live route. Story 7.14's exhaustive manifest list omits it. | Add the route and its existing inner-tab policy to 7.14; retain one host and one implementation. |

The existing [implementation-readiness report](implementation-readiness.md) is dated October 6 and contains subsequently superseded evidence-tool and UX-draft findings. It is historical evidence, not a fresh assessment of this checkout. The approved [evidence-storage simplification](../implementation-artifacts/spec-evidence-storage-simplification.md), PRD §8.1 and architecture AD-12 govern: proof is tests plus CI, recorded in the owning story/spec. Deleted evidence machinery is not a repair prerequisite.

## 2. Impact Analysis

### Epic And Story Impact

| Epic | Effect and retained ownership |
| --- | --- |
| 1 — Domain-centric services | No new domain work. Existing actor/SDK ownership and consumer parity obligations remain; append capability cannot be inferred from SDK adoption. |
| 2 — Integration surfaces | Preserve typed-client/generated-host boundaries. Shared security alignment reaches UI hosts only under NFR1's existing scope; Tenants repository changes remain with its owner. |
| 3 — Reproducible releases | 3.17 supplies operation/writer/alias coverage; 3.21 consumes Platform-executed runtime/component qualification; 3.18 supplies compatibility classification; 3.19 retains canonical profile/publication authority. No pin change or new evidence framework. |
| 4 — Event integrity | 4.16 remains the primary append owner, `draft` in its spec and `backlog` in tracking. Convert the general ownership assignment into the acceptance request in P1. Preserve its seven-part commit contract and AC1–AC11. |
| 5 — Fail-closed boundaries | 5.6/5.7 retain isolated bindings, reader/default/data migration, guarded initialization and rollback; 5.8 retains drift checks. 5.14's spec and tracker are `in-progress`; its epic narrative still says backlog. Reconcile that account without claiming parent completion. |
| 6 — Cost and evolution | No scope or lifecycle change. Preserve independent specification approvals and Dapr-only evolution boundaries. |
| 7 — Honest administration | 7.14 gains the missing route acceptance. 7.6 retains verifier custody requirements; 7.22 retains protected revision/erasure/restore consistency. 7.16's login backlog does not become implemented through an anonymous callback exemption. |
| 8 — Payload protection | Remains post-MVP. Its selected successor, authority inputs and qualification prerequisites are unaffected; it cannot supply the missing append primitive. |
| 9 — Planning controls | 9.3 owns substantive baseline and lifecycle reconciliation, including the 5.14 mismatch. OR14 and existing approval/assurance obligations remain. Re-specify missing guard work under the current evidence policy when scheduled. |

No epic is added, removed, renumbered or made obsolete. The feature goals remain viable only conditionally: Epic 4 cannot finish until its capability dependency and proof are delivered. Planning repair can progress while that dependency is blocked. General downstream implementation, MVP completion and production claims remain gated.

### Artifact And Technical Impact

PRD changes update stale baseline accounts; NFR1, NFR7, SM11 and the MVP UI-host exclusion keep their meaning. Architecture changes affect AD-16 and its known-gap table. AD-5's commit contract and AD-26's production subject need no change for these proposed security/route edits. The currently derived AD-26 subject is `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16`; recheck it after any later architecture amendment and record the subject impact in the existing memlog.

No runtime diagram change is needed: the actor-manager/Dapr transaction boundary, typed UI client and existing hosts remain. Qualification later needs concrete Platform-controlled host/fault execution and EventStore logical assertions. Provider credentials, private actor-key addressing, infrastructure lifecycle and physical diagnostics stay out of module acceptance helpers. This proposal changes no runtime code, deployment template, secret, component, dependency, CI workflow or submodule.

UX changes are handoff and traceability corrections after upstream edits. No screen, visual, wireframe, tab order or user journey is redesigned. The authenticated UI `/health` page remains distinct from an explicitly anonymous probe endpoint; matching a path name alone must never grant anonymity.

## 3. Recommended Approach

**Direct adjustment, moderate scope**, coordinated by the Product Owner and Developer with Architecture, Security, Test and Platform owners. Reuse existing story IDs, accepted ownership, runner contracts and test lanes.

| Option | Assessment | Effort and risk |
| --- | --- | --- |
| Direct adjustment | Recommended. Repair stale contracts and prepare an exact Platform intake; retain the supported-Dapr delivery dependency and all safety guarantees. | Low planning effort; medium cross-owner/compatibility effort; high unresolved append-delivery risk. |
| Rollback | Not selected. Reverting delivered security or SDK work does not create the mandatory persistence primitive and loses useful safeguards. | Material regression risk without an append benefit. |
| MVP reduction | Not selected. Excluding append races, narrowing writers, accepting overwrite risk or counting observation as prevention would violate NFR7/SM11. The existing UI-host production exclusion is already a settled scope decision. | Cannot satisfy the recorded exit contract. |

Sequence after approval:

1. Apply the AD-16 and related security-account corrections through `bmad-architecture`; amend 5.14 and 7.14 acceptance through `bmad-create-epics-and-stories`.
2. Record the prepared P1 intake in 4.16's existing non-frozen delivery sections. Platform and, if necessary, Builds must accept their execution/contract work in their owning repositories before it runs.
3. Under 9.3/OR14, reconcile source text, ownership, architecture decisions, UX references and lifecycle accounts. Preserve unresolved decisions and truthful FAIL results; do not repin digests to conceal them.
4. Obtain the required architecture closure, AD-26 ratification or approved replacement, and baseline validation/approval before renewing epic input bindings. Hash renewal is the last bookkeeping step of substantive reconciliation.
5. Rerun readiness. Generate tracking only when the readiness workflow permits it. These planning corrections alone cannot yield PASS while the mandatory append capability is missing.

Planning effort is low relative to implementation, but this audit supplies no calendar or person-day commitment. Platform intake, upstream delivery, qualification and subsequent migration cannot be dated until their owners accept exact scope and prerequisites. Existing unrelated corrective work continues only within its own authorization.

## 4. Detailed Change Proposals

### P1 — Story 4.16: Concrete Delivery Intake And Stop Conditions

**Targets:** 4.16 spec, Capability Dependency And Execution Gate / Current Qualification Ownership; matching dependency and current-reconciliation paragraphs in epics. Append a dated delivery note outside frozen intent; preserve the complete commit contract, AC1–AC11 and historical observations.

**OLD:**

> The scope decision is approved. The inspected SDK/runtime/component has no demonstrated mandatory primitive, so supported-Dapr capability delivery/qualification remains a blocking dependency.

The applied ownership proposal assigns Platform execution but explicitly records that acceptance and implementation remain pending.

**NEW — append this delivery disposition:**

> Delivery remains blocked on a supported released Dapr SDK/runtime/component combination implementing the complete approved contract. EventStore's persistence owner coordinates 3.17/3.21's capability intake and logical acceptance suite. Platform acceptance of concrete qualification execution is pending and is distinct from capability availability. Record the accepting owner, owning work reference, exact supported profile and contract version, prerequisites, supplied controls and acceptance command before execution. An unaccepted intake, missing control, unsupported primitive or unqualified erasure verifier leaves the affected lane blocked. Planning approval neither supplies owner acceptance nor permits migration around an assumed feature. Private runtime builds, provider exceptions and new activation fencing require separate exact-scope decisions.

**Prepared Platform/Builds acceptance request:**

| Required intake decision/input | Accountable role | Current disposition and completion condition |
| --- | --- | --- |
| Coordinate supported capability delivery | EventStore persistence owner with 3.17/3.21; upstream SDK/runtime/component maintainers own upstream delivery | No accepted upstream issue, implementation commitment or release date is evidenced. Reuse the approved contract and exact negative findings as the request. Require supported released identities, version/capability negotiation and complete semantics before qualification. No external request is sent by this run. |
| Accept infrastructure execution | Platform owner | Pending. Record an explicit Platform-owned work reference and accepted scope covering the selected disposable profile, independent actor hosts, lifecycle, controls, diagnostics and cleanup. Acceptance of work alone does not qualify a primitive. |
| Bind the existing runner handoff | Platform contract owner; Builds implements its runner/reader | Supply run/owner/attempt and synthetic isolation, enrolled lawful operation, owning-actor read route, exact expected and loaded module/runtime/component identities, readiness and private credential channel. Use the existing versioned descriptor. Its closed schema does not permit arbitrary fault-control fields; missing bindings require an owner-accepted versioned extension and explicit adoption, or remain a blocker. |
| Supply bounded adversarial controls | Platform execution owner; EventStore supplies assertions | Identify suspend-after-read/resume across independent hosts and placement transitions while persistence remains usable; commit-acknowledgement loss; raw actor and reachable generic aliases; isolation, TTL/delete and erasure cases; separately authorized disposable revision/create-once falsification. Bind controls to the run, including cleanup. Do not invent provider access in module tests. |
| Qualify erasure verifier custody | Security / 7.6 with the enforcing Dapr implementation and Platform | The design approval already exists. Qualify canonical capability bytes, purpose/partition/inventory/lifetime, key generations, value-free secret references, rotation, missing-key refusal, resumability and completion/replay. No key distribution precedes that qualification. |
| Accept application conformance | EventStore persistence and Test owners | AC1–AC11 run for each explicitly supported profile. Fresh owning-actor reads after cache clearing/restart prove exact durable events and coupled state. Lawful controls, both race orders, late-collision rollback, metadata-only conflicts, stale-host rejection, unknown outcomes and erasure must pass; separate falsification must fail for its intended reason. Physical diagnostics or startup/network failures cannot pass a prevention oracle. |
| Proceed beyond candidate proof | 5.6/5.7/3.18/7.22 owners, Platform deployment role and existing assurance owners | Only a credible qualified mechanism proceeds to their separately authorized isolation, guarded initialization, migration/rollback, compatibility, restore and fresh OQ8 obligations. Final proof requires the exact canonical ratified profile and remaining assurance gates. Builds changes pins only within its later accepted work. |

Record missing intake fields as **pending**, never fabricated owner names, endpoints, credentials, releases or dates. The request is complete enough for an owner to accept or reject; receipt is not claimed.

**Rationale:** Resolve what must be handed off and who accepts it, without presenting the previously approved assignment as delivered capability. A supported release is the already-approved strategy, not a new design choice. Retain 4.16 draft/backlog and G-APPEND FAIL/BLOCKED.

### P2 — Architecture AD-16 And Its Implementation Status Row

**Target:** AD-16 Rule in architecture; retain its ID and requirement bindings.

**OLD:**

> Every HTTP host configures an authenticated fallback authorization policy. Only `/health`, `/alive`, and `/ready` carry explicit `AllowAnonymous` metadata. Endpoint metadata tests enumerate exactly those support-safe exceptions; no probe exposes secret or tenant data. Any future anonymous asset or callback first requires a governed PRD change.

**NEW:**

> Every HTTP host configures an authenticated fallback authorization policy. Anonymous exceptions are limited to explicitly mapped, support-safe `/health`, `/alive`, and `/ready` probe endpoints and, on interactive UI hosts only, an explicitly enumerated set of static framework assets and authentication-protocol callback endpoints carrying no tenant, operational, or user data. Each exception carries explicit `AllowAnonymous` metadata and is enumerated by endpoint-metadata tests; no wildcard, route-prefix rule or weakened fallback grants anonymity. The authenticated Admin UI health page is not a probe exception. All other endpoints retain their named policy and credential requirements. Interactive UI hosts remain outside the canonical MVP production profile until their required human login exists, as PRD §9.2 requires.

**Target:** Implementation Status table, authenticated fallback / Dapr framework routes / actor invocation row.

**OLD excerpts:** `The fallback policy exists only in the DomainService SDK` and `interactive UI hosts wait for the product-owner decision, after which AD-16 is amended`.

**NEW — replace the row's current-account and next-action cells:**

> Story 5.14 records an implemented EventStore-owned HTTP-host/Operations security milestone and remains in progress. Its bound tests do not establish parent completion, actor-peer safety, cross-owner conformance or production readiness. PRD NFR1 already settles the UI asset/callback exception class; interactive UI hosts remain excluded from the canonical MVP production profile until human login exists.

> Story 5.14 retains host classification and framework/Operations authorization; 5.7/3.21 retain actor-invocation/runtime qualification, and the existing actor-context and audit prerequisites remain open. Reconcile UI conformance to NFR1/AD-16 through exact exception inventories and real-host tests, with each external host change accepted by its owner.

**Rationale and ripple:** Align to the settled product decision and the current 5.14 record. No blanket static-file permission or callback implementation is selected. Update the existing architecture memlog with subject impact; these edits lie outside AD-5's append paragraph and AD-26, whose subject must remain unchanged. Keep architecture `draft` until its complete closure conditions are satisfied.

### P3 — Story 5.14: Settled UI Guidance And Accurate Lifecycle Account

**Target:** 5.14 in epics, Dependencies and UI acceptance criterion; add a dated alignment note outside frozen intent in its spec.

**OLD dependency:**

> Interactive UI hosts wait for the product-owner decision on static assets and login callbacks (`sprint-change-proposal-2026-10-07-architecture-routing.md`, item 7.2).

**NEW dependency:**

> PRD NFR1 already permits the explicitly enumerated, data-free UI-host asset/callback exception class; AD-16 is reconciled to it in the same planning change. Interactive UI-host conformance requires exact endpoint inventories and real-host tests under that contract. These hosts remain outside the canonical MVP production profile until human login exists. Tenants changes remain with its repository owner.

**OLD UI criterion:**

> Given the interactive UI hosts (Admin UI, Sample Blazor UI), when the product-owner decision on static assets and login callbacks is still pending, then they are reported as failing conformance, with the conflict named, and this story adds no exemption.

**NEW UI criterion:**

> Given the interactive UI hosts, when endpoint metadata and real host pipelines are evaluated, then only explicitly enumerated NFR1-compliant probe, static-framework-asset and authentication-protocol-callback endpoints may be anonymous; all protected UI, API and tenant/operational/user-data surfaces retain authorization. Unexpected anonymous metadata, unnamed policies, prefix/wildcard exemptions and cross-host leakage fail conformance. Valid lawful controls remain functional. Missing human login or unqualified exception inventories cannot confer UI production eligibility or parent-story completion.

Amend the earlier all-host metadata criterion's `only /health, /alive, and /ready` clause to refer to the exact NFR1/AD-16 exceptions by host type. Non-UI hosts retain probes only. Preserve channel/fallback separation, Dapr framework policies, denial-before-effect and existing actor/audit prerequisites.

**OLD lifecycle account:** `Current reconciliation (2026-10-07): Backlog` followed by the older pre-implementation account.

**NEW current account:**

> Current reconciliation (2026-11-09): In progress, matching the owning spec and tracker. The EventStore-owned host-security milestone is recorded as implemented; parent completion, actor-context/protocol qualification, resumable audit, cross-owner and UI conformance remain pending. Earlier descriptions are historical and do not negate the implemented milestone or establish a new test result.

**Rationale:** Remove a superseded decision dependency and the observed lifecycle contradiction. This proposal runs no security tests and claims no new implementation. New UI work still needs its scoped implementation handoff; do not rewrite frozen intent or reopen delivered host work by implication.

### P4 — Story 7.14: Complete The Route Contract

**Target:** 7.14 route-manifest acceptance criterion in epics.

**OLD — exact relevant list segment:**

> `/streams/{tenant}/{domain}/{aggregate}`, `/events`, `/projections`

**NEW:**

> `/streams/{tenant}/{domain}/{aggregate}`, `/events`, `/types`, `/projections`

Append:

> `/types` belongs to Streams & Events / Type Catalog. The manifest preserves `tab=events`, `tab=commands`, and `tab=aggregates`; omission selects the catalog default, and an unknown tab falls back to that default with a bounded notice. Reload, bookmarks, browser history and any compatibility redirect preserve authorized safe state and resolve to one canonical implementation. The route and every permitted/default/unknown-tab case are included in completion tests.

**Rationale and UX effect:** This adopts the existing EXPERIENCE routing row and live route; it adds no dashboard tab, host, data API or duplicate page. Existing authorization, safe parameter handling and denied/cross-tenant failure criteria still apply.

### P5 — PRD And Story 9.3: Substantive Baseline Reconciliation

**Targets:** PRD §11.4 G-BASELINE current-result cell and OR14; 9.3 dependency/reconciliation wording in epics. Preserve FAIL/BLOCKED and the required approvals.

**OLD G-BASELINE status excerpt:**

> Architecture and detailed UX remain draft with two unconfirmed UX assumptions, detailed digests are stale, this PRD now differs from the epics input digest, lifecycle values conflict, and the baseline and corrective-authorization guards are absent.

**NEW status account:**

> Architecture remains draft and AD-26 remains unratified. Detailed DESIGN and EXPERIENCE and the top-level UX handoff are final; their finality does not approve an implementation baseline. All five recorded epic input digests differ from current sources. AD-16/NFR1, Story 7.14's /types manifest and current lifecycle accounts require substantive reconciliation, and the required baseline checks and approval remain pending under the current evidence-storage policy. Story 9.3 owns reconciliation and applicable drift checks; OR14 governs final source closure and approval. G-BASELINE remains FAIL/BLOCKED.

**OLD OR14 excerpts:** `the draft detailed DESIGN/EXPERIENCE digests already differ` and `detailed UX must resolve its two open assumptions`.

**NEW OR14 account:**

> The detailed UX pair is final and records explicit decisions for deferred restore/import and gated tenant provisioning. Reconcile those decisions and the /types routing contract with architecture and epics; retain historical snapshots without treating their hashes as current approval. Verify each earlier architecture obligation against the current spine, retain any still-open obligation, complete reviewer closure and obtain the required AD-26 ratification or approved replacement before epic input renewal. Then validate and approve one content-bound baseline. A hash-only refresh never establishes reconciliation or approval.

**OLD 9.3 dependency excerpt:** `resolved detailed-UX assumptions and final statuses, renewed epics`; older criteria name a committed evidence manifest and Story 9.1 registry/tooling.

**NEW 9.3 execution clarification:**

> Detailed UX finality is satisfied for the current pair; substantive source reconciliation, architecture closure and ratification, lifecycle agreement, validation and baseline approval remain open. Schedule 9.3 under AD-12's current story/spec-plus-CI policy. Re-specify any deleted validator when scheduling its actual guard work; do not restore deleted evidence packets, registries, manifests, seals or workflows merely because older text names them. Record input identities, commands, results, source SHA and CI link in the owning story/spec. Preserve required text/ownership/status/lifecycle drift checks and truthful initial FAIL behavior. Approval of this proposal does not deliver those checks or an approved baseline.

9.3's reconciliation must compare PRD FR/NFR text, stable-clause and primary ownership, affected AD constraints, UX route/security contracts, wrapper/frontmatter and tracker lifecycle accounts, and applicable approval provenance. Start with the concrete 5.14 drift documented here and retain existing OR15 obligations after checking their current evidence. Do not choose the most advanced label or change a tracker lifecycle to manufacture agreement.

The current five digest values in Appendix A are observations only. Do not copy them into `inputDocumentDigests`: the proposed source edits would change some of them, and OR14's ratification/approval prerequisites are still open. Renew all applicable bindings only after substantive reconciliation and the required acceptance, then check the final source set again.

**Rationale:** Correct stale facts while retaining the full baseline gate. This is a plan for existing 9.3 work, not an instruction to rebuild removed infrastructure or an assertion that absent checks passed. No new guard implementation is included in this proposal's application scope.

### P6 — UX Handoffs And Readiness Account

**Targets after P2–P5 are applied:** EXPERIENCE Client and credential boundary / Finalization decisions / routing / Handoff Boundaries; `ux.md`; UX index.

**OLD excerpts:** `The older probes-only architecture wording needs upstream reconciliation` and `Story 7.14's exhaustive route list still omits /types`.

**NEW handoff account:**

> AD-16 and Story 5.14 now reflect PRD NFR1's enumerated UI-host exception class; this is a planning-contract alignment, not tested UI conformance or production eligibility. Story 7.14's acceptance list includes /types and the existing Type Catalog tab/default policy; the route-manifest implementation and tests remain pending. OR14 and Story 9.3 still own substantive source reconciliation, lifecycle agreement, final input renewal and approval. Document finality confers no readiness or release authority.

Apply those statements only after their upstream edits exist. Preserve DESIGN, the visual references, route ownership, protected evidence states and source-snapshot hashes. Any new reconciliation snapshot must be clearly dated and must not overwrite the historical October 7 observation or form a circular source-approval claim.

If the readiness report is refreshed during the later readiness workflow, retain its historical assessment and append a dated assessment of the then-current inputs. Refer to this proposal and the simplification policy; do not carry forward missing deleted evidence tools as current blockers. Saving this proposal does not rerun the whole readiness assessment or change its verdict.

## 5. Implementation Handoff

**Classification: Moderate.** Product Owner / Developer coordination is required for backlog and acceptance reconciliation. Architecture/Security retain decision and ratification responsibilities. The existing supported-Dapr capability failure is a high technical risk; this proposal introduces no fundamental replacement architecture.

| Recipient | Reviewable deliverable and responsibility | Completion condition |
| --- | --- | --- |
| Product / planning owner | Approve or revise P1–P6 as one batch. Apply only the accepted planning amendments; preserve existing scope approvals. | Exact accepted edits and actual application state are recorded; unresolved Platform/upstream acceptance stays explicit. |
| Architect via `bmad-architecture` | P2 and architectural parts of P5; inspect current obligations and record memlog/AD-26 subject impact. | AD-16 matches settled NFR1; status accounts match bounded evidence; no unsupported finality or ratification claim. |
| Epic owner via `bmad-create-epics-and-stories` | P3/P4/P5 story amendments and P1 dependency note. | 4.16 AC1–AC11 remain intact, 5.14 account matches in-progress evidence, /types acceptance is complete, and 9.3 work is compatible with AD-12. |
| EventStore persistence / Test / 3.17 / 3.21 | Use P1 as the prepared capability and qualification intake; supply backend-neutral semantic vectors and logical oracles. | A qualified supported capability and complete profile-specific logical results exist before append integration or conformance claims. |
| Platform / Builds owners | Accept or reject P1 in their owning repositories; identify any existing-contract gap and propose an exact compatible versioned extension when necessary. | Explicit acceptance, qualified executing tool/profile, run-owned controls/endpoints and truthful observed loaded identities. Pending until those owners act. |
| UX owner | Apply P6 after upstream alignment; keep the final UX pair's behavior and historical snapshots. | Handoffs describe the amended contracts and distinguish planning closure from implementation. |
| 9.3 / Product / Test / tracker owners | Reconcile, validate and approve final planning inputs under OR14/current evidence policy, then request readiness again. | Required consistency checks and approvals bind the same final inputs; missing capability or ratification still yields FAIL. Tracking generation waits for the readiness workflow's permitted outcome. |

### Acceptance Of This Correction

1. The applied planning text agrees on NFR1 exceptions, UI production exclusion and /types ownership/parameter behavior, with no new anonymous endpoint or duplicated UI implementation.
2. The 4.16 handoff identifies outstanding upstream capability and Platform execution acceptance separately. No approved assignment is relabelled as delivered support, no guarantee is dropped, and 4.16's draft/backlog state remains truthful.
3. Baseline accounts acknowledge final UX and draft architecture. 9.3 has actionable reconciliation obligations under the current evidence policy; no digest refresh or architecture-final flag is used as a substitute for acceptance.
4. 5.14's epic narrative is reconciled to its in-progress spec/tracker without closing its parent or claiming a new test result. Other lifecycle changes require their own evidence.
5. The eventual checks identify exact commands, source SHA, pass/fail counts and CI link in the owning story/spec. This document is a planning deliverable and supplies zero new product/prevention passes.

### Workflow Record And Approval

| Checklist items | Status | Finding / disposition |
| --- | --- | --- |
| 1.1–1.3 | [x] | Trigger, technical limitation, stale-source conflicts and evidence identified. |
| 2.1–2.3, 2.5 | [x] | All nine epics screened; existing ownership and sequence retained; 4.16 completion is conditional. |
| 2.4 | [N/A] | No new, obsolete or removed epic. |
| 3.1–3.4 | [x] | PRD, architecture, UX, story, testing, infrastructure, compatibility and evidence-policy impacts mapped. |
| 4.1–4.4 | [x] | Direct adjustment selected; rollback and MVP reduction evaluated and rejected with reasons. |
| 5.1–5.5 | [x] | Five proposal sections, before/after edits, rationale, sequence, risks and role-based handoff supplied. |
| 6.1–6.2 | [x] | Proposal consistency, local links, source preservation, digest observations and scope checked as recorded below. |
| 6.3 | [x] | The owner explicitly approved complete P1–P6 with “approve” on 2026-11-09; earlier approvals retain their original scope. |
| 6.4 | [N/A] | No epic/story IDs are added or removed; tracking is not regenerated or edited by this proposal. Any later generation remains subject to readiness. |
| 6.5 | [x] | Written handoff and planning application are complete. Platform/Builds execution acceptance, capability delivery and implementation remain pending; no external message, issue or PR is sent. |

The owner’s explicit “approve” satisfies Step 5 for the complete proposal. P1–P6 planning amendments are applied and the moderate PO/Developer/Architecture handoff is recorded; Correct Course is complete for this planning scope. Platform acceptance, capability delivery, baseline approval and implementation remain separate pending work. The already-applied ownership correction is not reopened.

## Appendix A — Observed Epic Input Drift

SHA-256 is over the exact working-tree bytes at the inspected source commit. These observations neither renew the epic bindings nor approve the sources.

| Input | Recorded in epics | Observed |
| --- | --- | --- |
| `prd.md` | `b99effdb414209da373433d9b4d2075072ca950e89534b22b81155236f650662` | `01672ca4b2ec7b724a24e9e46b26788d89efc60aec6a678e824187b8b0beed73` |
| `architecture.md` | `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51` | `910360cd38e2e0f0fdfcc096ef74f9ea313913a769bf5071de928cbfbe9a4efa` |
| Detailed `DESIGN.md` | `76be269737ddacb5d5680862590893cd4e69d469222fd1eba554aa4084339a4f` | `d29c9db0f164f0c8f27f2b58bd25475489c2539135697d30b638c0964f0d5727` |
| Detailed `EXPERIENCE.md` | `06de15b349224772146fa279f9a61ca6c0fffe9d17774588cedd330690042eb6` | `0908cfba1fb59d7901c22a94dda388d68674387d0541bb737877e1fb3bebffba` |
| `ux.md` | `2927f97d4fe7262ec5886084a974d9d5c21240560e6563ab84f84b90e4dd670a` | `a135c9690bbd1dd157f00fdb389e7a5c8bec691e32826c5fdb12b97d07da95c1` |

The observed `epics.md` digest is `7ade087336d61c8403078672dd8b55ce9e6c1551e02e58661092ff983d0a3b62`. The unchanged tracker digest is `e816949eb2275142c16b78eab534e175015d814d5e27c2c81ae6c03dac6b7485`.

## Verification Before Approval

Source commit: `51700ffb1542bee1f64d12f6ad2d9bf2c988fe78`. Initial working tree was clean. At proposal preparation, only this new proposal was created; the prior applied proposal and canonical inputs were unchanged. The application below records subsequent approved amendments.

Executed validation:

- `python3 -B /tmp/check-eventstore-readiness-proposal-2026-11-09.py` — PASS, exit 0; 61 checks cover required sections, unique P1–P6 proposals, exact AD-16 and route OLD anchors, local links/fragments, five observed input mismatches, current lifecycle evidence, all existing story IDs, the 4.16 AC1–AC11 denominator, unchanged AD-26 subject, LF/whitespace/size, preservation of 15 input files and the sole new-proposal working-tree change. The checker remains local scratch, not new repository tooling.
- `git diff --check` — PASS, exit 0. The scratch checker separately checks the untracked proposal's whitespace because Git's tracked diff does not include it.

Product builds, live tests, prevention passes and new CI runs: **none**. The repository documentation lint configuration excludes `_bmad-output`; focused document checks are the relevant validation for this artifact. G-APPEND, G-BASELINE and readiness remain FAIL/BLOCKED. No approval, Platform acceptance, dependency change, infrastructure execution or tracking generation is inferred from document validation.

## Approval Application — 2026-11-09

Owner instruction: “approve”. Accepted pre-application proposal SHA-256: `be7aac7b15fe5151049c70049a11cb90009906430efd6afaf24f9946f2ed2f50`. This approves the reviewed P1–P6 planning amendments and their written handoff; it is not Platform acceptance, AD-26 ratification, baseline approval or runtime execution authority. Earlier approvals retain their existing scope.

Applied P1 to the 4.16 non-frozen delivery intake and epic account; P2 to AD-16 and the architecture status row; P3 to the 5.14 parent-story acceptance/current account and non-frozen spec note; P4 to 7.14’s /types and Type Catalog parameter acceptance; P5 to PRD baseline/OR14 and 9.3’s current-policy reconciliation; and P6 to EXPERIENCE, ux.md and the UX index after the upstream edits. G-BASELINE’s post-application wording records the now-applied P2–P4 alignment while preserving broader unresolved source/lifecycle work, rather than continuing to describe those specific edits as absent. Source references, update dates and the existing architecture memlog record provenance.

Architecture remains draft; its AD-26 subject remains `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16`. DESIGN, October 7 snapshot hashes, all frozen intent blocks, the complete 4.16 commit contract and AC1–AC11, historical test/source findings, epic input digests, tracker bytes/lifecycle values, the previous approved proposal and all runtime/dependency/submodule inputs remain unchanged. No tracking generation, live test, new CI run or external message occurs.

Application validation:

- `python3 -B /tmp/check-eventstore-readiness-application-2026-11-09.py` — PASS, exit 0; 353 focused checks cover exact approved application, local links/frontmatter, all story identities and requirement text, unchanged frozen intent and historical test notes, the complete append contract and AC1–AC11 agreement, the AD-26 subject, current-policy baseline wording, UX flow/snapshot preservation, untouched tracker/input pins, and the ten-file planning-only scope. The reviewed P1–P6 edit text remains unchanged in this proposal.
- `python3 .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace /tmp/eventstore-readiness-apply-xda3y5xl/lint-before` and the same command with `lint-after` — both PASS, exit 0, zero findings. Each scratch workspace contains the corresponding exact architecture bytes under the linter's expected filename; the repository's canonical path and draft status are retained.
- `bash scripts/check-committed-files.sh` — PASS, exit 0; 10,389 tracked files checked. The focused checker separately validates this untracked proposal's size, path and whitespace.
- `git diff --check` — PASS, exit 0.

The architecture skill's three independent reviewers (rubric/intent alignment, repository reality/currentness and adversarial divergence) all pass for this bounded application. One medium ambiguity was corrected: 7.14's generic unknown-parameter refusal now explicitly preserves only the already-approved `/types` unknown-tab fallback and grants no tenant or authorization exception. The adversarial reviewer rechecked and closed that finding. Full reviews and checker inputs remain local scratch under `/tmp/eventstore-readiness-apply-xda3y5xl`, not committed evidence or new repository machinery. These checks do not finalize or requalify the full architecture.

Correct Course is complete for the approved moderate planning correction. Written handoff is to PO/Developer/Architecture for the amended backlog, persistence/Test/3.17/3.21 for capability intake, Platform/Builds for separate execution acceptance, and 9.3/OR14 owners for the remaining baseline reconciliation and approval. No external communication was sent. Product tests, prevention passes and new CI runs: zero. G-APPEND, G-BASELINE and readiness remain FAIL/BLOCKED; tracking was not regenerated.
