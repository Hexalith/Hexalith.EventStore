# Sprint Change Proposal: Story 9.2 Transition Workflow And NFR1 Alignment

Date: 2026-10-07
Project: Hexalith.EventStore
Requested by: Administrator
Review mode: Batch, selected by the owner in this session
Status: Approved by Administrator in this session on 2026-10-07; both edits applied to `epics.md` (uncommitted)
Scope: Minor; two corrections in `epics.md`, with no backlog reorganization

## 1. Issue Summary

Story 9.2 still requires its validator to run in "a blocking, required check", without distinguishing merge checks from transition seals. Its transition AC names only high-risk PASS and `done`. The current [PRD Assurance Control](prd.md#4-glossary) and [architecture AD-12](architecture.md#ad-12---high-risk-verification-requires-persisted-evidence-adopted) instead require a dedicated workflow beside the matrix validator, with a required seal for each guarded transition. A truthful live FAIL blocks that transition and never `main`.

The `epics.md` NFR1 inventory also retains the single anonymous-exception wording. [PRD §7](prd.md#7-cross-cutting-non-functional-requirements) already permits the two closed exception classes: probes, and enumerated data-free framework assets and authentication-protocol callbacks on interactive UI hosts only. PRD §9.2 separately excludes interactive UI hosts from the canonical MVP production profile until human interactive login exists.

These are propagation gaps following the 2026-10-07 owner decisions, rather than new product requirements. The user identified both gaps in this session.

### Verified evidence

The source baseline inspected for this proposal is Git `HEAD` `c60503c13069fde13f329f2166ff39c90c3661c4`.

| Source | Observation |
| --- | --- |
| `epics.md:7583–7592`, Story 9.2 | The CI AC says "a blocking, required check"; the transition AC covers PASS and `done` only. |
| `epics.md:110`, Requirements Inventory | NFR1 says "The only anonymous exception" and permits probes only. |
| `prd.md:201`, Assurance Control | A required seal binds one of six guarded transitions, exact head SHA, workflow-file digest, and authenticated CI identity; an owner attestation is checked by the consuming transition. |
| `architecture.md:218`, AD-12 | Distinguishes required merge-blocking fixtures, non-required live evaluation, and required transition seals; requires predecessor validation inside the consuming sealed run. |
| `prd.md:361`, NFR1 | Contains the expanded, closed anonymous-exception policy. |
| `prd.md:507,543,609` | Excludes interactive UI hosts without human login from the MVP production profile and records that scope reason in NFR1 coverage. |
| `epics.md:389`, Epic 9 implementation notes | Already separates merge-blocking fixtures from live evaluations that publish a truthful PASS/FAIL on every push to `main`. |
| `sprint-status.yaml:325` | Story 9.2 is `backlog`. No standalone Story 9.2 spec/wrapper was found. |
| Matrix and validator paths named in Story 9.2 | Both artifacts are absent. This proposal does not claim delivered enforcement. |

The existing `sprint-change-proposal-2026-10-07.md` and architecture-routing proposal remain historical records. This separate dated supplement preserves them.

## 2. Impact Analysis

### Epics and stories

| Area | Impact |
| --- | --- |
| Epic 9 / Story 9.2 | Extend the existing acceptance criteria to own the transition workflow and its seal checks. Retain OR10, OR13, G-HIGH-RISK, the 17-gate inventory, roster-dependent assurance levels, and the Story 9.1 prerequisite. |
| Epic 5 / NFR1 consumers | Align the global inventory with the current PRD. The authenticated fallback remains mandatory; the asset/callback class applies only to interactive UI hosts. |
| Stories 3.19, 3.20, 9.3, and 9.5 | Consume the same Assurance Control for publication, promotion, removal, baseline PASS, or readiness. No replacement owner, changed sequence, or implementation authorization is introduced. |
| Story 7.16 | Human interactive login remains deferred runtime work. Its planning completion does not admit interactive UI hosts into production. |
| Other epics | The full epics file was scanned. No epic needs to be added, removed, renumbered, or reordered for these two corrections. |

### Artifact conflicts and follow-up boundaries

- **PRD:** Its Assurance Control, OR10, NFR1, SM10, scope boundary, and NFR1 traceability already express the owner decisions. No PRD edit is proposed.
- **Architecture:** AD-12 supplies the transition contract. AD-16 still says that only the three probes carry `AllowAnonymous`; its amendment after the PRD decision remains routed to `bmad-architecture`. The implementation-status rows also retain pre-decision descriptions. These are documented follow-ups, outside this two-correction application.
- **Story 5.14:** Its UI-host dependency and AC still describe the item 7.2 decision as pending. Before that story's spec freezes, reconcile those passages with the decided production exclusion and amended AD-16. This proposal does not add Story 5.14 edits to the requested batch.
- **UX:** The top-level handoff and detailed DESIGN/EXPERIENCE were scanned. These corrections change no screen or flow and implement no login. Existing UX status and assumption reconciliation remains with OR14 and G-BASELINE.
- **Tracker and input digests:** Story IDs and lifecycle states stay as recorded. `inputDocumentDigests` are not refreshed; renewed baseline approval still requires substantive OR14 reconciliation through Story 9.3.
- **CI and runtime:** This application edits planning text only. Story 9.2 later implements the matrix validator and workflow under its Story 9.1 authorization. The sealed `.github/workflows/ci.yml` and `docs/ci.md` inputs are not edited without a planned Story 4.15 reseal.

G-HIGH-RISK remains FAIL/BLOCKED, and readiness remains blocked/reject. Updating these acceptance criteria supplies no seal, approval receipt, release, deployment, or consumer-removal authority.

## 3. Recommended Approach

**Direct adjustment, minor scope.** Replace the two Story 9.2 ACs with the explicit contract below and copy the PRD NFR1 requirement into the epics inventory verbatim. Both edits fit the existing plan and retain its owners and sequence.

| Option | Assessment |
| --- | --- |
| Direct adjustment | Selected. Low planning effort and low risk; it removes two concrete contradictions before Story 9.2's spec freezes. |
| Rollback | Not warranted. No completed runtime work needs reverting. |
| MVP review | Not needed. The PRD already records the UI-host production exclusion; this proposal propagates that decision without expanding MVP scope. |

Estimated planning application and focused validation: under half a day. No sprint resequencing is required. Story 9.2's future workflow implementation is part of its existing scope; its developer should estimate delivery when writing the spec. The material implementation risk is accepting an unsealed or stale result as authority, addressed by the negative fixtures below.

## 4. Detailed Change Proposals

### A. Story 9.2 — CI and guarded-transition acceptance criteria

**Artifact:** `epics.md`, Story 9.2, Acceptance Criteria.

**OLD:**

```text
**Given** the matrix and validator
**When** CI runs
**Then** the validator runs in a blocking, required check whose result is retrieved from the CI platform for the exact head SHA and workflow-file digest, never read from an author-supplied file
**And** the matrix inputs are content-hashed so that edits fail the check.

**Given** a high-risk gate or a story recorded against a high-risk NFR
**When** a transition to PASS or `done` is attempted
**Then** the guarded transition requires the passing validator result and approval at the required assurance level (OR13).
```

**NEW:**

```text
**Given** the matrix, validator, and CI fixture suite
**When** CI runs
**Then** a required fixture job blocks merges and proves every rejection path with an observed failing negative fixture beside a positive control
**And** a separate, non-required live-evaluation job runs on every push to `main` and publishes a retrievable, content-bound PASS or FAIL without blocking merges; its result is evidence only and never a seal
**And** the matrix inputs are content-hashed so that edits invalidate the result, and the jobs use a new workflow file or the Story 9.1 workflow under the Epic 9 truthful-FAIL CI rule.

**Given** a high-risk gate result to PASS, a story recorded against a high-risk NFR to `done`, readiness, `release-available`, `production-promoted`, or consumer removal is attempted
**When** its dedicated transition workflow, owned by this story beside the matrix validator, runs for that one guarded transition
**Then** the transition is effective only when its validator retrieves a passing result from the CI platform for a required run of that workflow on the exact head SHA and workflow-file digest, under the platform's authenticated CI identity, bound to that transition and its subject and evidence identities
**And** it requires approval at the registry-computed Assurance Control level (OR10, OR13); a missing or failing seal blocks only that transition and never `main`
**And** required means enforcement by the guarded transition's validator, never a branch-protection or ruleset check on `main` that a bypass push can skip.

**Given** an owner attestation or ratification, including an AD-26 record, is consumed by a guarded transition
**When** that transition's sealed run validates the record
**Then** the record binds its required assurance level, subject digest, and authenticated attestation evidence, and the consuming run checks the required level and 24-hour separation window and computes and labels the achieved level
**And** the seal attaches to the transition rather than the owner record; the record never seals itself, and every downstream record carries the lowest assurance level of its inputs.

**Given** a guarded transition consumes a predecessor validator result, such as `evidence-validated`
**When** its sealed run checks that predecessor
**Then** it retrieves the validator from the pinned commit bound by the predecessor record and re-runs it inside the consuming transition's sealed run
**And** a validator-identity mismatch or failing re-validation voids the predecessor and blocks the consuming transition.

**Given** the transition seal and predecessor rejection paths
**When** their fixtures run
**Then** a missing or failing seal, a non-required live result offered as a seal, an author-supplied result file, an unauthenticated CI identity, a wrong transition, stale head SHA, changed workflow-file digest, changed subject or evidence identity, and mismatched or failing predecessor validation are each proven rejected by an observed failing negative fixture beside a positive control
**And** a truthful live FAIL leaves merges unblocked while the attempted guarded transition remains blocked; no guard is green by construction.
```

**Rationale:** Implements the distinction already required by the PRD Assurance Control and AD-12. It covers all six guarded transitions, makes seals retrievable and content-bound, prevents a branch-protection bypass from granting authority, and tests predecessor and owner-record handling. Existing classification, roster/identity checks, 24-hour attestation rejection, and G-RUNTIME-PARITY re-mint criteria remain in place.

### B. NFR1 — Requirements Inventory

**Artifact:** `epics.md`, Requirements Inventory → Non-Functional Requirements, NFR1.

**OLD:**

```text
NFR1: Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. The only anonymous exception is the health/liveness/readiness probe endpoints (`/health`, `/alive`, `/ready`), which are explicitly pinned `AllowAnonymous` and support-safe (AD-16); the fail-closed default is never weakened to reach probes.
```

**NEW:**

```text
NFR1: Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. The only anonymous exceptions are the health/liveness/readiness probe endpoints (`/health`, `/alive`, `/ready`), which are explicitly pinned `AllowAnonymous` and support-safe (AD-16), and, on interactive UI hosts only, an enumerated set of static framework assets and authentication-protocol callback endpoints that carry no tenant, operational, or user data, each explicitly pinned `AllowAnonymous`, support-safe, and enumerated by endpoint-metadata tests (AD-16); the fail-closed default is never weakened to reach either exception.
```

**Rationale:** Copies the current PRD NFR1 text exactly. It permits only enumerated, data-free assets/callbacks on interactive UI hosts; it grants no anonymous page, dashboard, API, tenant, or operational access. PRD §9.2 continues to exclude interactive UI hosts from the MVP production profile until human login exists, and Story 7.16 is not pulled forward.

### PRD, architecture, and UX proposals

No edits to those documents are included in this batch. The architecture AD-16 and Story 5.14 follow-ups are recorded in Section 2; they require their own reconciliation before the affected spec freezes. No diagram or wireframe change is needed for these two corrections.

## 5. Implementation Handoff

**Classification:** Minor. Administrator explicitly approved both edits with "continue and approve" in this session. The Developer applied the two exact replacements to `epics.md`; application and validation are recorded below. No subagent dispatch has occurred.

| Recipient | Responsibility |
| --- | --- |
| Developer, completed | Applied proposals A and B with exactly one source match each, rejected source drift, validated the resulting text, and preserved all other content. |
| Story 9.2 developer, after Story 9.1 authorization | Build the matrix validator, fixture/live jobs, and dedicated transition workflow; prove the specified rejection and positive-control cases before acceptance. |
| Architecture owner, separate follow-up | Reconcile AD-16 to the approved NFR1 decision through `bmad-architecture`, then reconcile Story 5.14's stale pending-decision wording. |
| Story 9.3 owner | Perform substantive baseline reconciliation and approval before any input digest refresh. |

### Success criteria

1. The epics NFR1 requirement equals the current PRD NFR1 requirement after removing only each document's ID/table wrapper.
2. Story 9.2 explicitly distinguishes merge-blocking fixtures, non-required live evaluation on every push to `main`, and a required dedicated workflow run for one guarded transition.
3. All six transition classes are covered, and required enforcement occurs at the transition validator rather than relying on `main` branch protection.
4. Seals bind exact head SHA, workflow digest, authenticated CI identity, transition, subject, and evidence. Non-required or author-supplied results fail as seals.
5. Consuming sealed runs check attestations/ratifications, compute achieved assurance, preserve the 24-hour window and downstream minimum, and re-run pinned predecessor validators.
6. Story 9.2 stays `backlog`; the tracker, input digests, other story sections, evidence packets, source code, workflows, submodules, and existing proposals are preserved.
7. G-HIGH-RISK stays FAIL/BLOCKED and readiness stays blocked/reject. The proposal and planning edits convey no production or external mutation authority.

### Pre-application validation record

Focused validation passed on 2026-10-07 using a read-only `python3` assertion script. Both exact OLD blocks matched current `epics.md` once. Both replacements were simulated in memory; the NFR1 requirement matched the current PRD exactly, all six transition classes and seal controls were present, existing matrix/assurance/parity criteria were preserved, and all other epics text was unchanged. The proposal passed LF/final-newline/whitespace and local-link/anchor checks. `git diff --check` returned exit 0 with no output.

| Observation | SHA-256 |
| --- | --- |
| Current `epics.md` bytes | `a7474fc1edce5212689eed383e4424683ce5e821e08ff7b69cb99e082a19dc03` |
| Proposed `epics.md` bytes, simulated only | `0697679b32390edf4d8719e5e1423e8dcbd77d01c6fc7ba50f2d754e11d1565f` |

These digests identify the reviewed source and preview; they are not baseline approval or assurance receipts. Runtime tests are not warranted for these planning edits; actual Story 9.2 enforcement tests remain implementation deliverables. Both edits were subsequently approved and applied as recorded below.

## Checklist Record

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Trigger | [x] | Story 9.2 AC and epics NFR1 inventory identified by the user. |
| 1.2 Problem | [x] | Incomplete propagation of existing owner decisions. |
| 1.3 Evidence | [x] | Exact source passages and current PRD/AD-12 contract verified. |
| 2.1 Current epic | [x] | Epic 9 remains viable with the Story 9.2 extension. |
| 2.2 Epic changes | [x] | Existing AC extension only; NFR1 inventory alignment. |
| 2.3 Remaining epics | [x] | Dependencies and full-file epic/story inventory scanned. |
| 2.4 New/obsolete epics | [N/A] | None. |
| 2.5 Resequencing | [N/A] | Existing prerequisites retained. |
| 3.1 PRD | [x] | Already authoritative for both decisions; no edit. |
| 3.2 Architecture | [x] | AD-12 used; remaining AD-16 and status-text drift routed separately. |
| 3.3 UX | [N/A] | Sources scanned; no screen, flow, or login implementation change. |
| 3.4 Other artifacts | [x] | CI, tracker, absent implementation artifacts, and digest boundaries assessed. |
| 4.1 Direct adjustment | [x] | Viable; selected recommendation. |
| 4.2 Rollback | [N/A] | No completed implementation to revert. |
| 4.3 MVP review | [N/A] | Existing PRD scope decision retained. |
| 4.4 Path selection | [x] | Minor direct adjustment. |
| 5.1–5.5 Components | [x] | Sections 1–5 include issue, impacts, alternatives, exact edits, and handoff. |
| 6.1 Checklist review | [x] | Analysis, batch review, and approval complete. |
| 6.2 Proposal accuracy | [x] | Exact-match simulation, PRD equality, scope/control checks, whitespace, and local links passed. |
| 6.3 Explicit approval | [x] | Administrator approved both corrections with "continue and approve" in this session on 2026-10-07. |
| 6.4 Tracker changes | [N/A] | No new/removed stories or status changes. |
| 6.5 Handoff | [x] | Minor planning application completed by the Developer; Story 9.2 implementation remains subject to its Story 9.1 authorization. Separate follow-up responsibilities are defined in Section 5. |

## Workflow Execution Log

- Activation customization resolved successfully: no prepend/append steps, persistent facts, or completion override.
- Required Hexalith baseline and repository guidance loaded; BMM configuration resolves English output and expert communication for Administrator.
- User selected Batch review for both corrections.
- Proposal prepared without applying either edit. No commit, push, dependency update, or submodule mutation was performed.
- Focused proposal validation passed. A concurrent change to the detailed UX `.memlog.md` was observed and preserved.
- Steps 4–5 completed: Administrator replied "continue and approve", accepting both exact corrections and authorizing their application.
- Developer handoff completed by applying both planning edits. Runtime implementation remains with the Story 9.2 developer after Story 9.1 authorization; AD-16/Story 5.14 and baseline reconciliation remain the separate follow-ups in Section 5.
- Step 6 completed: both approved edits applied, post-application validation passed, approval/application recorded, and follow-up owners and prerequisites retained.

## Application Record

Applied on 2026-10-07 at approximately 13:50:44 UTC, from source Git `HEAD` `c60503c13069fde13f329f2166ff39c90c3661c4`.

The application script extracted the two approved OLD/NEW blocks from this proposal, required exactly one match for each, compared the resulting NFR1 text with the current PRD, and required the resulting epics digest to equal the reviewed preview digest before writing. It also rechecked the source bytes immediately before application.

| File | SHA-256 after application | Change |
| --- | --- | --- |
| `epics.md` | `0697679b32390edf4d8719e5e1423e8dcbd77d01c6fc7ba50f2d754e11d1565f` | Story 9.2 CI/transition AC extension and verbatim PRD NFR1 alignment. |

The digest records application identity only. Story 9.2 stays `backlog`, G-HIGH-RISK stays FAIL/BLOCKED, and readiness stays blocked/reject. Input digests were not repinned. No commit, push, workflow change, dependency update, or submodule mutation was performed. Concurrent UX memlog and Playwright artifacts were preserved.

### Post-application validation

The read-only `python3` verification script returned exit 0 and confirmed:

- Applied epics bytes equal the two approved replacements against `git show HEAD:_bmad-output/planning-artifacts/epics.md` and match the reviewed preview digest.
- NFR1 matches the current PRD exactly; all six transition classes and the seal, attestation, predecessor, and rejection-fixture criteria are present.
- Existing matrix, assurance, and G-RUNTIME-PARITY criteria, every story ID, and all input-digest frontmatter are preserved.
- Story 9.2 remains `backlog`, and PRD readiness remains blocked/reject.
- Markdown whitespace, LF/final newline, and all proposal document links and anchors pass.

`git diff --check -- _bmad-output/planning-artifacts/epics.md` and `git diff --no-index --check -- /dev/null _bmad-output/planning-artifacts/sprint-change-proposal-2026-10-07-story-9-2-nfr1.md` produced no findings. The epics diff contains only the approved NFR1 and Story 9.2 changes (26 insertions, 8 deletions). Runtime tests were not run for these planning-only edits; the future Story 9.2 implementation must provide its acceptance evidence before the story can close.
