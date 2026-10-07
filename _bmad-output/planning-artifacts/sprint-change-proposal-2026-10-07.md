# Sprint Change Proposal: Readiness FAIL Ownership And Lifecycle Reconciliation

Date: 2026-10-07
Project: Hexalith.EventStore
Requested by: Administrator
Trigger: [Implementation readiness assessment, 2026-10-06](implementation-readiness.md), verdict FAIL
Review mode: Incremental, grouped; all five groups approved by the owner on 2026-10-07
Status: Approved by the owner on 2026-10-07 and applied to `epics.md`, `prd.md`, and `sprint-status.yaml` (uncommitted). See the Application Record.
Scope: Moderate. Backlog reorganization within the existing epics, plus two routed architecture/UX decisions

## 1. Issue Summary

The 2026-10-06 readiness assessment (intent: sprint planning) returned FAIL and stopped sprint-plan generation. It re-observes the result PRD §11.4 already computes, `Reject`, and adds current evidence:

1. **Gate controls are missing.** The corrective-work authorization, high-risk matrix, and MVP coverage validators and records do not exist. Stories 9.1 and 9.2 are `backlog`, and no story owns MVP coverage.
2. **The planning baseline is not reconciled.** All five `epics.md` `inputDocumentDigests` are stale. Architecture, DESIGN, and EXPERIENCE are `draft`, and EXPERIENCE holds two open assumptions.
3. **Ownership is incomplete.** The projection-cost spec, the AOT/trimming posture, the production profile, and the consumer-removal manifest and validator are absent. FR36-C3, FR36-C4, FR36-C5, NFR5, NFR13, NFR17-C5, and NFR18 have no primary story. G-TENANT, G-STATUS-ID, G-APPEND, G-OQ8, G-COMPAT, and G-AUTH-HOSTS have no owning story.
4. **Lifecycle accounts conflict** for Stories 5.2, 5.3, 5.4, and 6.1.

**Problem statement.** The PRD's computed `Reject` cannot move because most mandatory §11.4 gates have no owning story or validator, the planning baseline has drifted again, and four stories carry contradictory lifecycle accounts. This is an incomplete-planning and ownership problem, not a technical limitation.

### Evidence, re-verified 2026-10-06 to 2026-10-07

- The five digest mismatches reproduce exactly as the report records them. `prd.md` is `f6a2a0d6…` and `epics.md` is `8a4493d1…`; both were unchanged when this proposal was written.
- All eleven named artifacts are absent; `spec-folded-snapshot.md` is present.
- Lifecycle matrix, checked against each story's canonical spec:

| Story | Tracker | Story spec | `epics.md` narrative | Finding |
| --- | --- | --- | --- | --- |
| 5.2 | `done` | `in-review`, review loop 2; the 2026-09-06 review found 30 issues, 4 high, re-derived in the spec | "remains backlog" | The tracker `done` is unsupported. Commit `57fa0909` flipped `backlog`→`done` inside an unrelated IdempotencyChecker change. |
| 5.3 | `done` (guarded value) | `done`, review loop 8 | "in progress, loop 3" | Only `epics.md` is stale. |
| 5.4 | `done` since `fcd716c9` | `done`, 139/139 items checked | "remains backlog" | Only `epics.md` is stale; the PRD §11.3 facts are stale too. |
| 6.1 | `in-progress` | `in-progress` (reopened 2026-10-04, owner decision D1) | "backlog; artifact absent" | `epics.md` is stale. **Hazard:** `spec-folded-snapshot.md` frontmatter still reads `approved-authorized` / `story_6_2_authorized: true` until review patch P-D1 lands. |

- **UX conflict.** Both EXPERIENCE `[ASSUMPTION]` items contradict the current Admin UI. `/backups` (`Backups.razor`) shows **Import Stream** and **Restore**, and `/tenants` (`Tenants.razor`) has a **Create Tenant** dialog that Story 5.10's UX coverage relies on.
- **Concurrent work.** Other sessions are implementing Story 5.5 (uncommitted source changes, including `ProjectionChangeNotifierOptions` and workload JWT authentication) and the 6.1-P1R harness. This proposal does not touch those files.

## 2. Impact Analysis

### Epic impact

| Epic | Impact |
| --- | --- |
| 2 | Add successors 2.14 (G-TENANT) and 2.15 (G-STATUS-ID), and NFR owners 2.16 (NFR5) and 2.17 (NFR13). Update the FR12 and FR15 completion rules. Stories 2.2, 2.4, 2.5, and 2.9 keep `done` as bounded history. |
| 3 | Add 3.18 (G-COMPAT), 3.19 (G-PUBLICATION-AUTH / FR36-C3), and 3.20 (G-CONSUMER / FR36-C4–C5). Update the FR36 completion rule. |
| 4 | Add 4.16 (G-APPEND, envelope first) and 4.17 (G-OQ8). Story 4.15 is not reopened. |
| 5 | Add 5.11 (G-AUTH-HOSTS). Reconcile the 5.2, 5.3, and 5.4 narratives; the 5.2 tracker becomes `review`. |
| 6 | Story 6.3 explicitly owns G-NFR8. Add 6.7 (NFR18). Reconcile the 6.1 and 6.2 narratives. |
| 7 | Add 7.21 (NFR17-C5). |
| 8 | None. It stays post-MVP and outside the MVP aggregate. |
| 9 | Add 9.3 (G-BASELINE validator, OR8, OR15 guard), 9.4 (G-CLAUSE), and 9.5 (G-MVP-COVERAGE). Add the truthful-FAIL CI rule. |

No epic becomes obsolete. Epic 9 goes first, because Story 9.1 gates every corrective handoff under PRD §0.

### Artifact conflicts

- **PRD:** the ownership tables (§7.1, §11.1, §11.2, §11.4, §12), the NFR8 snapshot wording, the stale §11.3 lifecycle facts, SM2/SM3 notes, and frontmatter. Every gate result stays FAIL/BLOCKED.
- **Architecture** (routed, not edited here): AD-10 needs the Tenants boundary; AD-11 and AD-26 need the publication-lifecycle vocabulary; reviewer closure is outstanding; AD-26 is an unratified `[ASSUMPTION]`.
- **UX** (routed): the two EXPERIENCE assumptions conflict with the code and with Story 5.10; DESIGN and EXPERIENCE are `draft`.
- **Tracker:** 15 new `backlog` rows and Story 5.2 → `review`. The guarded comment blocks and the guarded 5.3 value stay untouched.
- **Digests:** not refreshed. PRD §11.3 prohibits hash-only refresh; refresh happens only through Story 9.3 after OR14.

## 3. Recommended Approach

**Direct adjustment** was selected by the owner.

- **Direct adjustment (selected).** Assign every unowned gate and refinement to an explicit backlog story, reconcile lifecycle narratives now, and map owners in the PRD. Planning effort is medium; delivery effort is high. Planning risk is low, because owner assignment changes no gate result.
- **Rollback.** Not viable. There is nothing to revert, and the bounded `done` evidence remains valid for what it covers.
- **MVP review.** Viable but not chosen. Moving FR36-C3–C5, G-OQ8, and G-COMPAT post-MVP would remove about four gates and five stories, but it requires a PRD revision and re-validation and defers release, promotion, and Parties-removal claims.

Owner decisions recorded 2026-10-07:

- G-TENANT and G-STATUS-ID are owned by **new successor stories**, not by reopening 2.2, 2.4, 2.5, and 2.9.
- The Story 5.2 tracker becomes **`review`**, matching its canonical spec.
- G-APPEND is **envelope first**. If an operating envelope that excludes a second writer cannot be mechanically enforced on the AD-26 profile, Story 4.16 stops and requests a fencing scope change through correct-course.

The readiness verdict stays FAIL after this proposal is applied. Assigning an owner is not delivery.

## 4. Detailed Change Proposals

### Group 1: Lifecycle reconciliation (OR5, OR15). Approved.

**1.1 `sprint-status.yaml`, row `5-2`.** Preserve the concurrent session's uncommitted edits.

```text
OLD:
  5-2-admin-endpoint-authorization-and-tenant-filters: done
NEW:
  # Corrected 2026-10-07 (correct-course OR15). Commit 57fa0909 flipped this row
  # 'backlog' -> 'done' in an IdempotencyChecker change whose subject never mentions
  # a status change, while spec-5-2 records status 'in-review' at review loop 2.
  5-2-admin-endpoint-authorization-and-tenant-filters: review
```

**1.2 `epics.md` Story 5.2, current reconciliation.** Replace the whole paragraph.

> **Current reconciliation (2026-10-07):** Story 5.2 is in review, neither backlog nor done (reconciled by `sprint-change-proposal-2026-10-07.md`, OR15). `spec-5-2` records status `in-review` at review loop 2. All seven execution tasks are checked, and the 2026-09-06 review pass returned 30 findings, 4 of them high, which the spec re-derived. The review that would close the story has not run. `sprint-status.yaml` recorded this row `done` from commit `57fa0909` until it was corrected to `review` on 2026-10-07; that commit is an unrelated IdempotencyChecker change whose subject never mentions a status change. The gaps this paragraph formerly listed (recent-command clamp, 1 MiB JSON cap, negative and boundary evidence for the endpoint matrix) are now spec tasks, but no accepted review covers them yet. **No NFR1 or NFR2 coverage may be claimed from this story until its review closes.**

**1.3 `epics.md` Story 5.3.** Replace the whole paragraph.

> **Current reconciliation (2026-10-07):** Story 5.3 is done for the hosts its spec binds. `spec-5-3` is `done` at review loop 8, and the owner set the tracker to `done` on 2026-09-10. This was reconciled by `sprint-change-proposal-2026-10-07.md` (OR15); the paragraph formerly described the loop-3 partial state from 2026-09-08. Commit `293c69c4` pins the authentication-contract regressions. Completion is bounded: it does not establish NFR3 conformance for Tenants, generated hosts, or future JWT-binding hosts. That all-host contract is owned by Story 5.11 under G-AUTH-HOSTS (OR23).

**1.4 `epics.md` Story 5.4.** Replace the whole paragraph.

> **Current reconciliation (2026-10-07):** Story 5.4 is done. `spec-5-4` is `done`, and all 139 task and review items were checked after the 2026-09-22 chunked reviews. The tracker has been `done` since commit `fcd716c9`. This was reconciled by `sprint-change-proposal-2026-10-07.md` (OR15); the paragraph formerly said "remains backlog" and listed gaps from before implementation. The spec still sets `followup_review_recommended: true`. That recommendation is advisory, and no follow-up review has run.

**1.5 `epics.md` Story 6.1.** Replace the whole paragraph.

> **Current reconciliation (2026-10-07):** Story 6.1 is in progress, neither backlog nor done (reconciled by `sprint-change-proposal-2026-10-07.md`, OR5 and OR15; the paragraph formerly said the artifact was absent). `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` exists, but the 2026-10-04 code review reopened the story by owner decision D1, because the 2026-09-08 approval did not bind the final normative bytes. The §3 inventory must be re-baselined at current HEAD, and the owner must re-attest a new digest. Until review patch P-D1 lands, that artifact's frontmatter still reads `status: approved-authorized` and `story_6_2_authorized: true`. **The reopen supersedes those values, and they grant no Story 6.2 authority.** `spec-6-1` and the tracker both record `in-progress`.

**1.6 `epics.md` Story 6.2.** Replace the first sentence only.

- OLD: "Story 6.2 remains backlog and is unauthorized because `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` is absent."
- NEW: "Story 6.2 remains backlog and unauthorized: `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` exists, but Story 6.1 was reopened on 2026-10-04, and its frontmatter authorization is superseded until the owner re-attests a new digest."

**1.7 `prd.md` NFR8 row.**

- OLD: "…has status `approved-authorized`, binds normative SHA-256 `0b456b5f…`, sets `MaxSnapshotEnvelopeOverheadBytes` to 4096, and authorizes Story 6.2. This approval does not deliver the runtime outcome or resolve Story 6.1 lifecycle drift."
- NEW: "…was approved on 2026-09-08, binding normative SHA-256 `0b456b5f…` and setting `MaxSnapshotEnvelopeOverheadBytes` to 4096. Story 6.1 was reopened on 2026-10-04 (owner decision D1), so that approval no longer authorizes Story 6.2 until the owner re-attests a new normative digest. Its `approved-authorized` frontmatter is superseded until review patch P-D1 lands. This approval does not deliver the runtime outcome."
- The digest value is unchanged. It changes only after the owner re-attests.

**1.8 `prd.md` §11.3 bullets and the OR5 and OR15 rows.** Replace stale facts in place.

- §11.3 Story 6.1 bullet: the specification was reopened 2026-10-04 and grants no Story 6.2 authority.
- §11.3 Story 5.3 bullet: the epics narrative was reconciled 2026-10-07.
- OR5: restate with current facts (reopened, P-D1 pending, frontmatter superseded). It **remains blocking** until P-D1 lands and the owner re-attests.
- OR15, Stories 5.2–5.4 and 6.1:
  - 5.2: tracker `review`, spec `in-review`, epics in review.
  - 5.3 and 5.4: `done` in all three sources.
  - 6.1: `in-progress` with the frontmatter superseded.
- OR15 **remains blocking** for Story 4.5's packet-versus-label split and for the guard owned by Story 9.3.

Not touched: `spec-folded-snapshot.md` (P-D1 belongs to the resumed 6.1 build), Story 4.5 (routed to the Story 9.3 lifecycle guard), and the concurrent Story 5.5 work.

### Group 2: Epic 9 gate-validator stories. Approved.

**2.0 Truthful-FAIL CI rule.** Added to the Epic 9 implementation notes; Stories 9.3–9.5, 3.19, and 3.20 cite it.

> **Truthful-FAIL CI rule (2026-10-07):** A gate validator's fixture suite runs as a blocking check. Its live gate evaluation runs on every push to `main` and publishes a retrievable PASS/FAIL result without blocking merges, so a truthful FAIL never blocks unrelated work. Gate validators use a new workflow file or the Story 9.1 workflow, and never edit `.github/workflows/ci.yml` or `docs/ci.md` unless a Story 4.15 reseal is planned.

**2.1 New Story 9.3.**

```markdown
### Story 9.3: Planning Baseline Manifest And Drift Guard

As a Product owner,
I want one content-bound manifest of the planning baseline and guards that compare requirements, ownership, statuses, and lifecycle values across it,
So that the PRD, architecture, UX, epics, tracker, and story records cannot silently diverge again and a hash-only refresh can never pass as reconciliation.

**Requirements coverage:** Primary OR8, the G-BASELINE validator, and the OR15 guarded lifecycle comparison; supporting OR5 and OR14.

**Architecture constraints:** None new.

**Dependencies:** Story 9.1 passed, plus a Story 9.1 authorization record for gate G-BASELINE. Minting an approved manifest also requires OR14 completion (architecture reviewer closure and AD-26 ratification, resolved detailed-UX assumptions and final statuses, renewed epics); building the validator and guards does not wait for it.

**Acceptance Criteria:**

**Given** the manifest at `_bmad-output/implementation-artifacts/evidence/phase-4-planning-baseline.json`
**When** the validator command bound by this story runs
**Then** it binds the SHA-256 of `prd.md`, `architecture.md`, the detailed DESIGN and EXPERIENCE documents, `ux.md`, `epics.md`, `sprint-status.yaml`, every story record it names, the relevant evidence digests, and the Story 9.1 authorization registry
**And** it rejects any digest mismatch, `draft` status, open `[ASSUMPTION]`, active or unreconciled corrective-work authorization, and any approval that predates the bytes it binds.

**Given** the PRD and `epics.md`
**When** the drift guard runs
**Then** it compares PRD FR/NFR text with the epics Requirements Inventory, and PRD §7.1 and §11 primary ownership with each story's declared coverage
**And** every divergence fails with the requirement or clause ID and both texts.

**Given** every story key in `sprint-status.yaml`
**When** the lifecycle comparison runs
**Then** the tracker value, the story spec or wrapper frontmatter, and the `epics.md` current-reconciliation statement agree, or a dated reconciliation record explains the difference
**And** a tracker `done` without a spec `done` fails, with Stories 4.5, 5.2, 5.3, 5.4, and 6.1 as checked fixtures.

**Given** the current baseline
**When** the validator first runs on `main`
**Then** it reports FAIL and lists every cause it finds
**And** the story closes on that truthful result; it never refreshes, mints, or approves a digest to make the gate pass.

**Given** the validator's rejection paths and CI
**When** its tests and workflow run
**Then** each rejection is proven by a checked-in negative fixture observed failing, alongside a positive control, and no guard is green by construction
**And** the workflow follows the Epic 9 truthful-FAIL CI rule.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md`.
```

**2.2 New Story 9.4.**

```markdown
### Story 9.4: Stable Clause Ledger And All-Clauses Validator

As a Product owner,
I want PRD §7.1's stable clauses mirrored in `epics.md` with one primary slice and an evidence identity each, checked by an all-clauses-required validator,
So that no parent requirement closes on a status label or on a partial set of clauses.

**Requirements coverage:** Primary OR7 and gate G-CLAUSE; supporting SM2.

**Architecture constraints:** None new.

**Dependencies:** Story 9.1 passed, plus a Story 9.1 authorization record for gate G-CLAUSE. The clause owners assigned by `sprint-change-proposal-2026-10-07.md` (FR12-C2 to 2.14; FR12-C3 and FR12-C4 to 2.15; FR36-C3 to 3.19; FR36-C4 and FR36-C5 to 3.20; NFR17-C5 to 7.21) are present in `epics.md`.

**Acceptance Criteria:**

**Given** PRD §7.1
**When** the story completes
**Then** `epics.md` contains a Stable Clause Ledger listing every clause ID with exactly one primary slice and an evidence-identity field that reads `pending` until passing evidence exists
**And** the FR12, FR15, and FR36 completion rules name the same slices as the ledger.

**Given** the validator command bound by this story
**When** it runs
**Then** it rejects missing, duplicate, unknown, or unassigned clauses, any divergence between the ledger and PRD §7.1, and any parent requirement recorded as closed while one of its clauses lacks passing content-bound evidence
**And** it rejects closure that rests on a story status alone.

**Given** the current baseline
**When** the validator first runs on `main`
**Then** it reports FAIL and lists every clause without passing evidence
**And** the story closes on that truthful result.

**Given** the validator's rejection paths and CI
**When** its tests and workflow run
**Then** each rejection is proven by a negative fixture observed failing beside a positive control
**And** the workflow follows the Epic 9 truthful-FAIL CI rule.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md`.
```

**2.3 New Story 9.5.**

```markdown
### Story 9.5: Phase 4 MVP Coverage Manifest And Validator

As a Product owner,
I want one manifest that enumerates every Phase 4 MVP requirement, clause, and metric denominator with its owner, lifecycle, evidence, and blocking gate,
So that MVP completion and readiness are computed rather than asserted.

**Requirements coverage:** Primary OR27 and gate G-MVP-COVERAGE; supporting OR17, SM9, SM10, and SM12.

**Architecture constraints:** None new.

**Dependencies:** Story 9.1 passed, plus a Story 9.1 authorization record for gate G-MVP-COVERAGE; Story 9.4 for the clause inventory; Story 9.2 for approval at the Assurance Control level.

**Acceptance Criteria:**

**Given** `_bmad-output/implementation-artifacts/evidence/phase-4-mvp-coverage.json`
**When** `python3 tools/validate-phase-4-mvp-coverage.py _bmad-output/implementation-artifacts/evidence/phase-4-mvp-coverage.json` runs
**Then** the manifest enumerates FR1-FR36, NFR1-NFR18, every stable §7.1 clause, and the `interactive_ui_hosts`, `nfr1_surfaces`, and `high_tier_evidence_cases` inventories
**And** each entry binds its primary owner, lifecycle state, exact evidence digest, validator command and result, approval, and the mandatory gate that blocks it when not passed.

**Given** the validator
**When** it evaluates the manifest
**Then** it rejects omissions, duplicate ownership, `done` unsupported by passing evidence, stale identities, unapproved or failed evidence, incomplete metric inventories, and `N/A` for any MVP ID
**And** it rejects any FR37 or NFR19 entry counted toward the MVP.

**Given** the current baseline
**When** the validator first runs on `main`
**Then** it reports FAIL with every open requirement, clause, and gate
**And** the story closes on that truthful result.

**Given** a manifest digest offered for approval
**When** the approval is recorded
**Then** it uses the Story 9.2 Assurance Control and carries the computed label, `single-maintainer-attested` while the registry names one human
**And** each rejection path is proven by a negative fixture beside a positive control, and the workflow follows the Epic 9 truthful-FAIL CI rule.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md`.
```

**2.4 Index updates.**

- Epic List, Epic 9:
  - Refinements owned: OR7, OR8, OR10, OR13, OR27, OR28.
  - Gates: G-HIGH-RISK, G-CLAUSE, G-MVP-COVERAGE, and the G-BASELINE validator with the OR15 lifecycle guard.
  - Story set: 9.1–9.5.
  - Implementation notes gain the truthful-FAIL CI rule.
- Epic 9 header: "Stories 9.1–9.2 were added by `sprint-change-proposal-2026-09-26.md`; Stories 9.3–9.5 by `sprint-change-proposal-2026-10-07.md`; all start in `backlog`."
- Tracker rows after `9-2`:
  - `9-3-planning-baseline-manifest-and-drift-guard`
  - `9-4-stable-clause-ledger-and-all-clauses-validator`
  - `9-5-phase-4-mvp-coverage-manifest-and-validator`

### Group 3: Epic 2 corrective successors and NFR owners. Approved.

**3.1 New Story 2.14.**

```markdown
### Story 2.14: Canonical Tenant Boundary In Generated Controllers And The Tenants Host

As a security owner,
I want every generated controller and the Tenants API host to canonicalize and validate tenants through one shared contract before routing,
So that mixed-case, missing, conflicting, or reserved tenants can never cross a tenant boundary.

**Requirements coverage:** Primary FR12-C2, OR20, NFR2's corrected tenant-boundary contract, and FR15's platform-operation and tenant-boundary slice; gate G-TENANT. Supporting: Stories 2.2, 2.4, 2.5, and 2.12, whose `done` labels remain non-authorizing for the corrected contract.

**Architecture constraints:** AD-27 (`Contracts` owns the canonicalizer and grammar), AD-10, and AD-28.

**Dependencies:** Story 2.12; a Story 9.1 authorization record for gate G-TENANT. Tenants-host changes land in the Tenants repository under its owner. Boundary with Story 5.10: Story 5.10 keeps the guard against provisioning `system` as a managed tenant; this story owns request-boundary rejection and the distinct platform-operation scope.

**Acceptance Criteria:**

**Given** a generated controller or a Tenants route receiving a request tenant and `eventstore:tenant` grants
**When** the boundary evaluates them
**Then** both are normalized to lowercase and checked against the AD-27 grammar through the shared `Contracts` canonicalizer
**And** missing, duplicate, conflicting-after-normalization, grammar-invalid, unauthorized, and reserved `system` inputs are rejected before routing or state access, with zero downstream work observed.

**Given** a platform-wide operation
**When** it executes
**Then** it uses a distinct, authenticated, cataloged platform-operation scope
**And** no path synthesizes or forwards `system` as a request tenant.

**Given** compiled generated-controller tests and Tenants runtime tests
**When** they run in the CI lane this story binds
**Then** mixed-case positives and every fail-closed negative above pass in both
**And** approval uses the G-HIGH-RISK Assurance Control at its computed level.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the approved corrective successor for OR20.
```

**3.2 New Story 2.15.**

```markdown
### Story 2.15: MessageId-Only Command-Status Identity

As an API consumer,
I want command status to be selected only by a `MessageId` valid under my endpoint's declared contract version,
So that a correlation identifier can never address a status resource.

**Requirements coverage:** Primary FR12-C3, FR12-C4, and OR21; gate G-STATUS-ID. Supporting: Story 2.9, which remains `done` for the absolute-or-absent `Location` behavior while its `MessageId ?? CorrelationId` fallback is superseded.

**Architecture constraints:** AD-17 and AD-32 (correlation is never status identity).

**Dependencies:** A Story 9.1 authorization record for gate G-STATUS-ID.

**Acceptance Criteria:**

**Given** a versioned contract manifest
**When** it is validated
**Then** it assigns MessageId grammar v1 or v2 to every affected endpoint and generated contract
**And** no caller can select or override the grammar version.

**Given** an accepted command
**When** the status `Location` is computed
**Then** only a `MessageId` valid for the endpoint's version selects an absolute gateway-authoritative status URI
**And** every `MessageId ?? CorrelationId` fallback is removed from the generator, its tests, and its documentation.

**Given** compiled and runtime tests for both grammars
**When** they run in the CI lane this story binds
**Then** undeclared, ambiguous, or caller-selected versions; missing, blank, invalid-for-version, or non-canonical v2 `MessageId` values; and a `CorrelationId` that differs from or appears without `MessageId` each omit `Location` or reject safely
**And** explicit v1 compatibility is preserved, the manifest records an NFR12 compatibility classification for every changed public surface, and approval uses the G-HIGH-RISK Assurance Control at its computed level.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the approved corrective successor for OR21.
```

**3.3 New Story 2.16.** Story 5.5, being built concurrently, touches `ProjectionChangeNotifierOptions`; Story 2.16 re-baselines against whatever lands.

```markdown
### Story 2.16: Bounded SignalR Detail Metadata

As an operator,
I want projection-change detail metadata bounded and never logged above Debug,
So that notification payloads stay small and support-safe.

**Requirements coverage:** Primary NFR5 and OR25, limited to the existing SignalR detail-metadata contract. Supporting: Story 2.8 (transport contract) and Story 2.13 (Dapr notification distribution).

**Architecture constraints:** None new.

**Dependencies:** A Story 9.1 authorization record for gate G-NFR-OWNERSHIP.

**Acceptance Criteria:**

**Given** `ProjectionChangeNotifierOptions` defaults
**When** detail metadata at exactly 16 entries and 2048 total UTF-8 bytes, and at one entry or one byte beyond, is sent
**Then** deliveries at the limit succeed and deliveries beyond it are bounded exactly as the existing contract specifies
**And** keys are treated as opaque, with no allow-listed key set.

**Given** captured framework logs above Debug level
**When** notifications carrying metadata are sent
**Then** no metadata value appears in them
**And** metadata values may appear only at Debug level or below.

**Given** the validation command this story binds
**When** it runs
**Then** it executes the tests above
**And** NFR5 is not extended to DAPR, generated APIs, or other response types.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the NFR5 primary owner required by OR25.
```

**3.4 New Story 2.17.**

```markdown
### Story 2.17: Generated-Code And Source-Generator Build Quality

As a domain author,
I want generated controllers and the source-generator packages to build cleanly under the repository's quality rules,
So that consuming the generator never introduces warnings, nullability gaps, or identifier-parsing defects.

**Requirements coverage:** Primary NFR13 and OR26. Supporting: Stories 2.2, 2.4, and 2.9.

**Architecture constraints:** None new.

**Dependencies:** A Story 9.1 authorization record for gate G-NFR-OWNERSHIP.

**Acceptance Criteria:**

**Given** a representative compiled consumer of the REST source generator
**When** it builds with warnings as errors
**Then** the generated output and the generator packages build with zero warnings
**And** they conform to EventStore code style, nullable annotations, ULID identifier rules (no `Guid.TryParse` on message, correlation, aggregate, or causation identifiers), and `ConfigureAwait(false)`.

**Given** the validation command this story binds
**When** it runs in CI
**Then** a seeded violation of each rule fails it
**And** byte-stable or deterministic generator output is out of scope.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the NFR13 primary owner required by OR26.
```

**3.5 Index and rule updates.**

- FR12 completion rule: four clause slices. FR12-C1 is Story 2.2; FR12-C2 is Story 2.14; FR12-C3 and FR12-C4 are Story 2.15. Story 2.9 is retained as supporting history for the absolute-or-absent `Location` delivery.
- FR15 completion rule: a sixth slice, platform operations versus the tenant boundary, owned by Story 2.14.
- Epic List, Epic 2:
  - Story set: 2.1–2.17.
  - NFR5 and NFR13 now have primary owners (2.16 and 2.17).
  - The 2.13 note about "existing NFR5 readiness debt" now points to Story 2.16.
- Tracker rows after `2-13…`:
  - `2-14-canonical-tenant-boundary-in-generated-and-tenants-hosts`
  - `2-15-messageid-only-command-status-identity`
  - `2-16-bounded-signalr-detail-metadata`
  - `2-17-generated-code-and-generator-build-quality`

### Group 4: Epic 3, 4, and 5 gate owners. Approved, with G-APPEND envelope first.

**Build-then-issue pattern.** When a gate ends on an owner-authenticated act, the story has two parts:

- **Build:** schema, validator, fixtures, and CI. This part ends on a truthful FAIL.
- **Issue:** the record or receipt, which is required before the story is `done`.

**4.1 New Story 3.18.**

```markdown
### Story 3.18: Public-Surface Compatibility Baseline And Release Gate

As a release owner,
I want every public EventStore surface inventoried and baselined, with the SemVer policy enforced in the release lane,
So that no release can break consumers silently.

**Requirements coverage:** Primary OR22, gate G-COMPAT, and NFR12's expanded public-surface inventory slice. Supporting: Story 2.15 compatibility classifications and Story 3.17.

**Architecture constraints:** AD-11.

**Dependencies:** A Story 9.1 authorization record for gate G-COMPAT.

**Acceptance Criteria:**

**Given** the manifest-governed release package set
**When** the inventory is generated
**Then** a manifest lists every public surface of each package, with source/binary API and wire baselines
**And** an omitted package or surface fails.

**Given** a change to a public surface
**When** the release-lane command bound by this story runs
**Then** SemVer, deprecation, and removal policy checks pass or fail against the baselines
**And** an incompatible change passes only with an approved SemVer-major proposal, which does not waive inventory, migration, or evidence.

**Given** representative consumers that reference packages only
**When** they build and run against the candidate
**Then** they pass
**And** approval uses the G-HIGH-RISK Assurance Control at its computed level.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the G-COMPAT owner required by OR22.
```

**4.2 New Story 3.19.**

```markdown
### Story 3.19: Publication Authority Record And Canonical Production Profile

As a release owner,
I want release availability and production promotion recorded as authenticated, predecessor-bound records over one canonical production profile,
So that no evidence result is ever relabelled as release or promotion authority.

**Requirements coverage:** Primary FR36-C3, OR29, and gate G-PUBLICATION-AUTH.

**Architecture constraints:** AD-26 (once ratified) and AD-11.

**Dependencies:** Owner ratification of AD-26 or an approved replacement; Story 5.7 for the production component contents; Story 3.15 (`done` for FR36-C2); Story 9.2 for the Assurance Control; a Story 9.1 authorization record for gate G-PUBLICATION-AUTH.

**Acceptance Criteria:**

**Given** `deploy/dapr/production-profile.yaml` authored as the single production-profile inventory slot
**When** the validator bound by this story runs
**Then** it computes the canonical-byte SHA-256 of that file and requires it as the complete production-profile inventory
**And** it rejects absent, under-declared, unknown, or self-only profiles.

**Given** the Publication Authority Record schema
**When** a record is validated
**Then** it requires one valid predecessor-bound state chain with issuance, expiry, revocation, and invalidation
**And** existing candidate-publication evidence cannot be recorded as `release-available` or `production-promoted`.

**Given** the build steps are complete
**When** the release owner and then the deployment owner issue their records for the unchanged Story 3.15 subject
**Then** the `release-available` entry is release-owner-authenticated and the `production-promoted` entry is deployment-owner-authenticated, binding the canonical profile digest and an immutable deployment identity
**And** the story is `done` only after both records validate under the Assurance Control; until then the validator reports a truthful FAIL under the Epic 9 truthful-FAIL CI rule.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the FR36-C3 owner required by OR29.
```

**4.3 New Story 3.20.**

```markdown
### Story 3.20: Consumer-Removal Authority Manifest And Validator

As a consuming-module owner,
I want every consumer and every removal proposal registered and authorized against the promoted runtime,
So that no consumer removes local projection or query infrastructure without valid authority.

**Requirements coverage:** Primary FR36-C4, FR36-C5, OR24, and gate G-CONSUMER.

**Architecture constraints:** AD-22 and AD-26.

**Dependencies:** Story 3.19; a Story 9.1 authorization record for gate G-CONSUMER.

**Acceptance Criteria:**

**Given** `_bmad-output/implementation-artifacts/evidence/consumer-removal-manifest.json`
**When** `python3 tools/validate-consumer-removal-authority.py _bmad-output/implementation-artifacts/evidence/consumer-removal-manifest.json` runs
**Then** the manifest enumerates every root-declared or Phase-4-referenced consumer, whether or not removal is proposed
**And** each entry binds repository and commit, the canonical production-profile digest, mode matrix, removal-subject digest, role-registry identity, consumer-owner receipt, decision, issuance, validity, and invalidation.

**Given** a new consumer or removal proposal
**When** code changes are proposed
**Then** the consumer or proposal is registered first
**And** missing, unknown, under-declared, or self-only profile entries fail.

**Given** a consumer with no proposed removal
**When** `N/A` is recorded
**Then** the bound consumer diff proves no removal and both the consumer owner and the validator attest it
**And** a proposal can never be `N/A`.

**Given** Parties, which has an applicable removal proposal
**When** the manifest is first validated
**Then** Parties fails until its consumer owner issues a valid `consumer-removal-authorized` receipt
**And** no consumer removes local infrastructure without one; the validator otherwise reports a truthful FAIL under the Epic 9 truthful-FAIL CI rule.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the FR36-C4 and FR36-C5 owner required by OR24.
```

**4.4 New Story 4.16.**

```markdown
### Story 4.16: Append Write-Once Conformance Through An Enforced Operating Envelope

As an operator,
I want the supported production profile to make a second writer to an event key mechanically impossible,
So that committed events can never be silently overwritten.

**Requirements coverage:** Primary OR4, NFR7 class (c), and SM11; gate G-APPEND. Story 4.5's capture and DW-326 are input evidence only.

**Architecture constraints:** AD-5, AD-26 (once ratified), and the Story 3.17 Dapr boundary qualification.

**Dependencies:** Story 3.17; owner ratification of AD-26; a Story 9.1 authorization record for gate G-APPEND. **Path decision (owner, 2026-10-07):** envelope first. If the envelope cannot be mechanically enforced on the AD-26 profile, the story stops and requests a fencing scope change through correct-course; it does not implement fencing on its own authority.

**Acceptance Criteria:**

**Given** the AD-26 production profile
**When** the operating envelope is defined
**Then** it names every mechanism that excludes a second writer to an event key, including component scoping and ACLs, the actor-only write path, and placement-failover behavior
**And** each mechanism is enforced by configuration or code that a test can falsify.

**Given** the `same-key-overwrite-raw-durable-write-lost` scenario from Story 4.5
**When** it is replayed against the enforced envelope through the production path
**Then** the second write cannot occur or is rejected, and no committed event is lost
**And** removing any one envelope mechanism makes the test fail.

**Given** the evidence
**When** G-APPEND is evaluated
**Then** the result binds provider, topology, writer inventory, test, and envelope identities
**And** risk acceptance or deferral cannot substitute for this proof, and approval uses the G-HIGH-RISK Assurance Control at its computed level.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the G-APPEND owner required by OR4.
```

**4.5 New Story 4.17.**

```markdown
### Story 4.17: OQ8 Governing-Design Authority Import

As an architecture owner,
I want the OQ8 governing design reproducible inside EventStore's evidence boundary,
So that OQ8 authority no longer depends on bytes that EventStore cannot verify.

**Requirements coverage:** Primary OR11 and gate G-OQ8. Supporting FR27, NFR7, and NFR16. Story 4.15 is not reopened.

**Architecture constraints:** PRD §1.1 governing design identity.

**Dependencies:** A Story 9.1 authorization record for gate G-OQ8. Sealed OQ8 v3 inputs are not edited outside a planned Story 4.15 reseal.

**Acceptance Criteria:**

**Given** the design `docs/exit-criteria/oq8-idempotency-design.md` in `github.com/Hexalith/Hexalith.Folders` at commit `a9cfea91c8a987ef7a836c216e633a92321fc3c2` with SHA-256 `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`
**When** it is imported
**Then** EventStore retains one permitted form: an immutable copy with a recorded owner permission, a complete approved normative projection, or a signed or content-addressed Folders attestation
**And** the retained bytes re-verify to that SHA-256 from inside the repository.

**Given** the imported authority
**When** the identity is propagated
**Then** the PRD, architecture, epics, the OQ8 evidence packet, the validator, and CI bind the full repository, path, commit, and SHA-256
**And** a mismatch or absence fails G-OQ8.

**Given** the sealed OQ8 v3 inputs, including `v3.py`, `.github/workflows/ci.yml`, and `docs/ci.md`
**When** this story changes validators or CI
**Then** it does so in a new validator version or a new workflow file
**And** it does not edit sealed inputs unless a Story 4.15 reseal is planned, and the Story 4.15 v4 source-only packet still passes default validation.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the G-OQ8 owner required by OR11.
```

**4.6 New Story 5.11.**

```markdown
### Story 5.11: Shared Versioned JWT Contract And All-Host Conformance

As a security owner,
I want every externally reachable or JWT-binding host to consume one versioned JWT contract,
So that no host validates tokens with a weaker, hand-rolled subset.

**Requirements coverage:** Primary OR23, gate G-AUTH-HOSTS, and NFR3's all-host conformance slice. Supporting: Story 5.3, which remains `done` for its bound hosts.

**Architecture constraints:** AD-10 and AD-28.

**Dependencies:** Story 5.3; a Story 9.1 authorization record for gate G-AUTH-HOSTS. Tenants changes land in the Tenants repository under its owner.

**Acceptance Criteria:**

**Given** one versioned JWT contract with a fingerprint
**When** EventStore, Admin, Sample, Tenants, and generated-host fixtures start
**Then** each consumes that contract and reports its fingerprint
**And** Tenants no longer uses a hand-rolled validation subset.

**Given** the host inventory
**When** the conformance test runs
**Then** it enumerates every externally reachable or JWT-binding host, including hosts added later
**And** any host that does not consume the contract fails the test.

**Given** each conforming host
**When** negative tests run
**Then** Production, break-glass, algorithm, issuer, audience, signature, lifetime, role, and tenant violations are rejected
**And** the story binds the exact release evidence, and approval uses the G-HIGH-RISK Assurance Control at its computed level.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the G-AUTH-HOSTS owner required by OR23.
```

**4.7 Index updates.**

- Epic List:
  - Epic 3 story set: 3.1–3.20. Its FRs-covered line adds the FR36 C3–C5 authority slices.
  - Epic 4 story set: 4.1–4.17.
  - Epic 5 story set: 5.1–5.11.
- FR36 completion rule: C1–C5 are all required.
- Tracker rows:
  - `3-18-public-surface-compatibility-baseline-and-release-gate`
  - `3-19-publication-authority-record-and-canonical-production-profile`
  - `3-20-consumer-removal-authority-manifest-and-validator`
  - `4-16-append-write-once-conformance-through-enforced-envelope`
  - `4-17-oq8-governing-design-authority-import`
  - `5-11-shared-versioned-jwt-contract-and-all-host-conformance`

### Group 5: Remaining owners, PRD mapping, and architecture/UX routing. Approved.

**5.1 G-NFR8 is owned by Story 6.3.** No new story. The PRD G-NFR8 and OR16 rows name "primary owner: backlog Story 6.3".

**5.2 New Story 6.7.**

```markdown
### Story 6.7: AOT And Trimming Posture Reference

As a platform maintainer,
I want the AOT and trimming posture documented and tied to the build,
So that no package claims AOT or trimming compatibility while reflection conventions remain load-bearing.

**Requirements coverage:** Primary NFR18, OR6, and gate G-NFR18. Supporting: Stories 6.5 and 6.6.

**Architecture constraints:** None new.

**Dependencies:** A Story 9.1 authorization record for gate G-NFR18.

**Acceptance Criteria:**

**Given** `docs/reference/aot-and-trimming-posture.md`
**When** it is reviewed
**Then** it states that AOT and trimming are not targets while reflection conventions remain load-bearing, and inventories those conventions
**And** its content digest and review are recorded.

**Given** the release package set
**When** the build guard bound by this story runs
**Then** it fails if any release package declares `IsAotCompatible` or `IsTrimmable` as true while the posture says AOT and trimming are not targets
**And** a seeded violation proves the guard fails.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the NFR18 owner required by OR6.
```

**5.3 New Story 7.21.**

```markdown
### Story 7.21: Crypto-Shred Boundary Documentation And Evidence

As a security owner,
I want the MVP's crypto-shred boundary documented and guarded,
So that no MVP surface claims crypto-shred guarantees that only post-MVP payload protection could provide.

**Requirements coverage:** Primary NFR17-C5. Supporting: Epic 8, which cannot substitute for this MVP ownership.

**Architecture constraints:** None new.

**Dependencies:** A Story 9.1 authorization record for gate G-NFR-OWNERSHIP.

**Acceptance Criteria:**

**Given** the boundary document at the path this story binds
**When** it is reviewed
**Then** it states what the existing Contracts and Admin crypto-shredding seams cover, and that the Epic 8 engine, physical erasure, and production key custody are not covered
**And** its content digest and review are recorded.

**Given** MVP documentation and public surfaces
**When** the guard bound by this story runs
**Then** it fails on any claim of crypto-shred beyond the documented boundary
**And** a seeded overclaim proves the guard fails.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07.md` as the NFR17-C5 owner.
```

**5.4 PRD ownership mapping.** This assigns owners only. Every gate result stays FAIL/BLOCKED, and the guarded substrings in the G-HIGH-RISK, G-PUBLICATION-AUTH, G-CONSUMER, and G-RUNTIME-PARITY rows are preserved.

- **§7.1 primary slices.** FR12-C2 is 2.14; FR12-C3 and FR12-C4 are 2.15; FR36-C3 is 3.19; FR36-C4 and FR36-C5 are 3.20; NFR17-C5 is 7.21.
- **§11.1.**
  - The FR12, FR15, and FR36 rows name those stories.
  - The closing note becomes: primary owners were assigned on 2026-10-07; assignment is not delivery.
- **§11.2 rows.**

  | NFR | Change |
  | --- | --- |
  | NFR2 | add 2.14 for the corrected contract |
  | NFR3 | add 5.11 |
  | NFR5 | primary 2.16 |
  | NFR12 | add 3.18 for the inventory |
  | NFR13 | primary 2.17 |
  | NFR17 | add 7.21 for C5 |
  | NFR18 | primary 6.7 |

- **§11.4 rows.** Each gets "Primary owner: backlog Story X":

  | Gate | Primary owner |
  | --- | --- |
  | G-BASELINE | 9.3 |
  | G-OQ8 | 4.17 |
  | G-TENANT | 2.14 |
  | G-STATUS-ID | 2.15 |
  | G-APPEND | 4.16, envelope first |
  | G-CLAUSE | 9.4 |
  | G-NFR8 | 6.3 |
  | G-COMPAT | 3.18 |
  | G-AUTH-HOSTS | 5.11 |
  | G-PUBLICATION-AUTH | 3.19 |
  | G-CONSUMER | 3.20 |
  | G-NFR18 | 6.7 |
  | G-NFR-OWNERSHIP | 2.14, 2.16, 2.17, 3.18, 5.11, 6.7, 7.21 |
  | G-MVP-COVERAGE | 9.5 |

- **§12.** The owner column names the primary story for OR4, OR6, OR7, OR8, OR11, OR15 (guard), OR16, OR20 through OR27, and OR29. All of them remain Blocking.
- **SM2 and SM3.** Append: primary owners assigned 2026-10-07; assignment is not delivery.
- **Frontmatter.**
  - `updated: 2026-10-07`.
  - `implementation_readiness_last_assessed: 2026-10-06`, with `implementation_readiness_last_baseline: a6fc951e2c01c1c816aed1d03c851c0639f12cdd`; the result stays `reject`.
  - Add `implementation-readiness.md` and this proposal to `source_artifacts`.

**5.5 `epics.md`.**

- Stories 6.7 and 7.21 are added.
- Epic 6 and Epic 7 entries show NFR18 and NFR17-C5 as primary-owned.
- Tracker rows:
  - `6-7-aot-and-trimming-posture-reference`
  - `7-21-crypto-shred-boundary-documentation-and-evidence`

**5.6 Route to `bmad-architecture`.** OR14; no edits in this workflow.

- Add the Tenants boundary to AD-10.
- Align AD-11 and AD-26 with `built` → `evidence-candidate-published` → `evidence-validated` → `release-available` → `production-promoted`.
- Complete reviewer closure.
- Obtain explicit owner ratification of AD-26 or an approved replacement. This unblocks Stories 3.19 and 4.16.
- Set the architecture status to final.

**5.7 Route to `bmad-ux`.** OR14; no edits in this workflow.

- Resolve both EXPERIENCE `[ASSUMPTION]` items against the conflicting evidence in §1. If the target information architecture hides restore and import and excludes tenant provisioning:
  - name the stories that remove the existing controls (likely 7.4 and 7.14);
  - correct Story 5.10's UX coverage, which relies on the Create Tenant dialog.
- Otherwise, revise EXPERIENCE to match the code.
- Then set DESIGN and EXPERIENCE to final.

**5.8 No digest refresh.** `inputDocumentDigests` is refreshed only through Story 9.3, after the OR14 work in 5.6 and 5.7.

## 5. Implementation Handoff

**Scope classification: Moderate.** It reorganizes the backlog: 15 new stories, one tracker correction, and narrative and PRD ownership updates. Two items go to the PM and Architect: AD-26 ratification and the UX information-architecture decision.

| Recipient | Responsibility |
| --- | --- |
| Developer agent, now | Apply Groups 1–5 to `epics.md`, `prd.md`, and `sprint-status.yaml` exactly as approved. Preserve concurrent uncommitted work. Validate the PRD and sprint-status guards in Contracts.Tests. |
| Owner | Record the dated bootstrap authorization in Story 9.1 before development, as its existing bootstrap rule requires. Issue each Story 9.1 authorization record before the matching corrective handoff. Attest under the Assurance Control. |
| `bmad-build` | Wave sequence below. |
| `bmad-architecture` | Group 5.6, including AD-26 ratification. |
| `bmad-ux` | Group 5.7. |
| Resumed Story 6.1 | Apply P-D1 first, so `spec-folded-snapshot.md` stops claiming 6.2 authority, then the remaining patches and the owner re-attestation. |

### Sequencing

| Wave | Work | Unblocked by |
| --- | --- | --- |
| 0 | Apply this proposal. | Final approval |
| 1 | 9.1 with the owner bootstrap authorization. In parallel, and not gate-closing: architecture (5.6), UX (5.7), and resumed 6.1 starting with P-D1. | Wave 0 |
| 2 | 9.2; validator builds for 9.3 and 9.4. | 9.1 plus a record for each gate |
| 3 | 2.14, 2.15, 2.16, 2.17, 3.18, 5.11, 6.7, 7.21, 4.17, and 6.3 (spec). | 9.1 records |
| 3b | 4.16 (needs 3.17 and AD-26), 3.19 (needs AD-26, 5.7, and 9.2), then 3.20. | AD-26 ratification |
| 4 | 9.5 coverage manifest; 9.3 baseline minting after OR14; digest refresh through 9.3; readiness re-run (G-READINESS). | All of the above |

**In-flight work.** Stories 5.5, 6.1-P1R, 6.6, and 8.3 continue in other sessions as non-gate-closing work. Their results cannot count as gate evidence until Stories 9.1 and 9.2 exist and a matching authorization record binds them. This proposal neither pauses nor authorizes them.

### Success criteria

1. Every PRD §11.4 mandatory gate and every blocking refinement in §12 names a primary story in both `prd.md` and `epics.md`. No "unassigned" or "corrective successor or reopened" text remains in §7.1, §11.1, or §11.2.
2. The tracker, spec, and `epics.md` agree for Stories 5.2, 5.3, and 5.4. Story 6.1 agrees except for the explicitly superseded `spec-folded-snapshot.md` frontmatter, which is pending P-D1.
3. `sprint-status.yaml` has 15 new `backlog` rows and `5-2: review`. Guarded comment blocks and the guarded 5.3 value are intact.
4. The Contracts.Tests guards that read `prd.md` and `sprint-status.yaml` stay green: `CorrectedDeployedRuntimeParityClosureTests`, `DeployedRuntimeParityClosureTests`, and `ProofPacketValidatorIntegrityTests`.
5. No input or evidence digest is refreshed, and every gate result in PRD §11.4 remains FAIL/BLOCKED. The readiness verdict stays FAIL until Waves 1–4 complete.

## Checklist Record

| Item | Status | Note |
| --- | --- | --- |
| 1.1 Trigger | [x] | 2026-10-06 readiness FAIL; no single triggering story |
| 1.2 Problem type | [x] | Incomplete planning and ownership |
| 1.3 Evidence | [x] | Re-verified digests, absences, lifecycle matrix, and UX conflict |
| 2.1 Current epic | [x] | Epic 9 cannot complete as planned without 9.3–9.5 |
| 2.2 Epic-level changes | [x] | New stories in Epics 2–7 and 9; no new epic |
| 2.3 Remaining epics | [x] | Epic 8 unaffected (post-MVP) |
| 2.4 Obsolete or new epics | [x] | None obsolete; none new |
| 2.5 Order and priority | [x] | Epic 9 first; AD-26 ratification gates 3.19 and 4.16 |
| 3.1 PRD | [x] | Ownership mapping, NFR8 wording, stale facts |
| 3.2 Architecture | [!] | Routed: AD-10, AD-11, AD-26, reviewer closure |
| 3.3 UX | [!] | Routed: two assumptions contradict the code and Story 5.10 |
| 3.4 Other artifacts | [x] | `sprint-status.yaml`; CI rule for new validators; no `ci.yml` or `docs/ci.md` edits |
| 4.1 Direct adjustment | Viable | Selected |
| 4.2 Rollback | Not viable | Nothing to revert |
| 4.3 MVP review | Viable | Not selected by owner |
| 4.4 Recommended path | [x] | Direct adjustment |
| 5.1–5.5 Proposal components | [x] | Sections 1–5 |
| 6.1–6.2 Review | [x] | All groups approved incrementally |
| 6.3 Final approval | [x] | Owner approved on 2026-10-07 ("approve and apply") |
| 6.4 Sprint-status update | [x] | 15 `backlog` rows added; `5-2` → `review` |
| 6.5 Handoff confirmation | [x] | Section 5; Wave 1 is next |

## Application Record (2026-10-07)

Applied at HEAD `27ac3c628db75ac6a410c88689546edd4de60ae4`. The worktree already contained other sessions' uncommitted Story 5.5 and P1R changes; they were preserved, and none of them was touched. The edits were applied by a script that required exactly one match for every replacement and wrote nothing unless all of them matched. New story text was copied verbatim from the fenced blocks in this document.

| File | SHA-256 after application | Change |
| --- | --- | --- |
| `prd.md` | `ca97f4ef7dc83df6c742cde404f9218ff347fabe6af7dad871e9774f5b789f52` | Groups 1 and 5.4 |
| `epics.md` | `d4dc58be92b552cb13ce3611e086fcf73dc5ff73c17bb92a929ee337091d5bfa` | Groups 1–5 |
| `sprint-status.yaml` | `354a885c0ff5bfe4fa04f29b3d26f4d51e5b8b196bca329b027ae98d71f3b22a` | Groups 1–5 |

These digests are observation identifiers, not approval receipts. `epics.md` `inputDocumentDigests` was not refreshed.

Additions beyond the approved text, all mechanical and consistent with it:

- `epics.md` FR Coverage Map, FR36 line: one sentence naming Stories 3.19 and 3.20 for FR36-C3 to FR36-C5. This matches the Group 4.7 index update.
- `prd.md` frontmatter: two fields, `implementation_readiness_last_baseline_status: examined-dirty-unapproved` and `implementation_readiness_last_report`. They follow the existing field pattern and reflect the readiness report's own statement that it examined a dirty worktree.

**Validation.**

- Build: `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Release -p:NuGetAudit=false` succeeded with 0 warnings and 0 errors.
- Tests: `tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests` with `-class` for `CorrectedDeployedRuntimeParityClosureTests`, `DeployedRuntimeParityClosureTests`, `ProofPacketValidatorIntegrityTests`, and `Oq8PlatformClosureTests` reported 1005 total, 0 failed, 0 skipped, exit 0. These are every Contracts.Tests class that reads `prd.md` or `sprint-status.yaml`; no test reads `epics.md`.
- `git diff --check` is clean on all three files.
- No duplicate story IDs remain, and no "unassigned" or "corrective successor or reopened" ownership text remains in PRD §7.1, §11.1, or §11.2.

**Known remaining divergence.** The `epics.md` Requirements Inventory FR/NFR wording still differs from the PRD in places; for example, NFR3 and NFR5 are worded more narrowly in the epics. This substantive text reconciliation is part of the OR14 epics renewal that follows the architecture and UX approvals, and the Story 9.3 drift guard will enumerate it. It was not in the approved scope here.
