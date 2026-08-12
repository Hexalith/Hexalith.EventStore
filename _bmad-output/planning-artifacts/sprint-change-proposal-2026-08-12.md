---
project: eventstore
date: 2026-08-12
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved-for-implementation
trigger: story-3-13-terminal-evidence-failure
final_approved_by: Administrator
final_approved_on: 2026-08-12
handoff_recipients:
  - Product Owner
  - Developer
  - EventStore owner
  - Release owner
  - Test Architect
---

# Sprint Change Proposal — Story 3.13 Terminal Evidence Closure

**Author:** Amelia (Developer) via `bmad-correct-course`
**Change scope:** Moderate backlog and acceptance-boundary correction; no runtime, release,
registry, deployment, consumer, submodule, or predecessor-evidence mutation
**Status:** APPROVED FOR IMPLEMENTATION

## 1. Issue Summary

Story 3.13 was created to prove one exact lineage from the Story 1.20-approved source and package
bytes through a semantic release to a deployed two-platform OCI image. That positive proof is now
impossible for the frozen Story 1.20 basis:

- the exact 14 package archives at `999.1.20-proof.fa2d1c9910f8` are unavailable;
- the recorded recovery search is exhausted and recovered 0 of 14 archives;
- rebuilding lookalike packages is forbidden and would not recover the approved bytes;
- the conforming Story 3.12 release uses a different source and package identity;
- the retained proof index has no same-lineage semantic-release provenance or deployment
  authority; and
- twelve hardening passes have strengthened the negative evidence without changing this fact.

AC2 is therefore dead, not temporarily blocked. A further hardening or recovery pass against the
same frozen inputs cannot make the required byte identity exist.

A separate integrity defect was found while reviewing the predecessor evidence. Commit `089369bb`
performed an SDK-token sweep across frozen Story 1.20 evidence and changed exactly one
`environment.txt` file in each of these three trees:

- `38f85086fc2513e06fe85482dfade96578d649e5`
- `4983299103bfa5bbbd40e695767eb5ddbc1369d5`
- `ec0d35a082bcc70b090afa1c1544306008d767da`

Each tree now fails its `critical-evidence-sha256.txt` manifest on that file alone. Story 3.13 has
no authority to repair frozen Epic 1 bytes. Its selected `fa2d1c9910f8...` predecessor tree is
already restored and passes its 33-entry critical manifest; the three sibling corrupt trees do not
affect the truth of Story 3.13's terminal negative result.

## 2. Impact Analysis

### Epic impact

| Epic | Impact | Disposition |
| --- | --- | --- |
| Epic 1 | Three frozen Story 1.20 evidence trees fail their critical manifests. The Story 1.20 approved source/package decision itself remains complete. | Add post-closure maintenance Story 1.21 with explicit predecessor-repair authority. Keep Story 1.20 and Epic 1 `done`. |
| Epic 3 | Story 3.13 cannot satisfy its positive AC2 with the frozen basis and would otherwise remain permanently `in-progress`. | Re-scope 3.13 from positive parity proof to terminal deployed-parity disposition. A reviewed `unavailable` result can complete the story without authorizing an image. |
| Other epics | No dependency consumes Story 3.13 as migration or deployment authority. | No change. |

No top-level epic is added, removed, reordered, or moved into or out of MVP scope. Epic 1's
capability remains closed. Epic 3 remains `in-progress` until the re-scoped Story 3.13 packet is
accepted, after which it may close normally.

### Artifact impact

| Artifact | Required correction |
| --- | --- |
| `epics.md` | Reclassify Story 3.13 as a terminal evidence/disposition story; add Story 1.21 as post-closure evidence maintenance. |
| `prd.md` | Clarify FR36 traceability: Story 1.20 satisfies source/package parity; Story 3.13 may close deployed mode as `unavailable`, which authorizes no deployed identity. |
| `architecture.md` | Extend AD-22 with terminal-unavailability semantics and the requirement for a fresh, separately approved baseline before any future positive deployed claim. |
| Story 3.13 record and spec | Replace the impossible positive AC2 and endless reopen loop with terminal negative closure criteria. Dispose Tasks 4-7 as impossible for the frozen basis, not passed. |
| Story 3.13 proof packet and evidence schema | Record `deployed_runtime_parity: unavailable`, `selected_deployed_identity: null`, the exhausted recovery basis, and the absence of authorization. Rebind the content-addressed review subject once. |
| Story 3.13 verifier | Permit only the exact terminal-unavailable shape when all negative facts and mutation prohibitions hold; continue rejecting any passing or deployable identity claim. Do not add another general hardening pass. |
| `sprint-status.yaml` and `docs/ci.md` | Move 3.13 to `review` only after the re-scoped packet is internally complete; move it to `done` only after AC4 acceptance. Add Story 1.21 as backlog maintenance without reopening Epic 1. |
| `deferred-work.md` | Bind the existing HIGH integrity entry to Story 1.21; remove it only when that story completes. |

UX, runtime code, release workflows, registry state, packages, deployments, consumers, submodules,
Stories 1.20/3.12, and existing Story 1.20 evidence bytes are unaffected by this proposal.

### Technical and operational impact

The change makes the existing safety result final and truthful:

- `done` will mean the investigation and disposition are complete, not that deployed parity is
  available;
- no OCI index, child image, tag, release, or deployment becomes approved;
- any operator or consumer asking for deployed parity receives `unavailable`;
- future positive deployed parity requires a new owner-approved source/package baseline and a new
  same-lineage release under a separately approved story; and
- the three corrupt sibling trees are repaired only by Story 1.21 under explicit Epic 1 evidence
  authority.

## 3. Recommended Approach

### Selected — Direct adjustment with terminal negative closure

- **Effort:** Low to medium. Planning/story text, the terminal evidence schema, focused verifier
  semantics, review subject, tracking, and one new maintenance story must change together.
- **Risk:** Low if the distinction between `done` and `available` is enforced structurally. The main
  risk is accidentally presenting story completion as deployment authorization.
- **Timeline:** No thirteenth hardening pass. Re-scope and rebind the final packet once, collect the
  three existing roster acceptances, and close. Story 1.21 is independent backlog maintenance.
- **Rationale:** The evidence already proves non-recoverability and cross-lineage mismatch. Keeping
  the story open cannot create missing package bytes. A terminal `unavailable` result preserves
  AD-22 fail-closed behavior while ending an otherwise infinite review loop.

### Rejected — Potential rollback

Rollback is not viable. It cannot recreate the missing 14 package archives, would risk rewriting
approved or published history, and would conflate three repairable checksum drifts with the
unrecoverable package-byte problem.

### Rejected — Continue recovery or hardening

Another pass is not viable. The durable search avenues are exhausted, the original bytes remain
absent, and additional verifier hardening cannot alter lineage identity.

### Rejected — Treat the current proof as available

This would violate FR36 and AD-22. Hash lists, ancestry, a compatible release, or a valid OCI graph
cannot substitute for the missing exact package bytes and same-lineage release provenance.

### MVP impact

No MVP requirement is weakened. FR36 continues to forbid consumer infrastructure removal without
matching source/package/deployed evidence. Source/package parity remains available through Story
1.20. Deployed mode is explicitly unavailable, so no consumer or operator may rely on it. A future
positive deployed-mode capability is additive work requiring a fresh baseline, not unfinished work
against the dead Story 1.20 package basis.

## 4. Detailed Change Proposals

### 4.1 Story 3.13 purpose and classification

**Story:** 3.13 Deployed Runtime Parity Closure
**Section:** Classification and story statement

**OLD:**

> I want deployed runtime identity mapped back to the approved source/package parity evidence, so
> that operators can select a conforming image.

**NEW:**

> I want a terminal, owner-reviewed disposition of deployed runtime parity for the frozen Story
> 1.20 basis, so that operators can distinguish an approved deployed identity from a conclusively
> unavailable one without weakening fail-closed behavior.

Classify Story 3.13 as a completed-investigation/evidence-disposition story, using the existing
Story 1.13 precedent: a `done` story may record that the capability remains unavailable, provided
the unavailable state is explicit and authorizes nothing.

**Rationale:** Story completion must describe completed work. Capability availability is a separate,
machine-readable decision.

### 4.2 Replace dead AC2

**Story:** 3.13
**Section:** Acceptance Criterion 2

**OLD:**

> Prove one exact source/package/release/deployed identity chain. One approved source SHA and the
> exact 14 package bytes must map through one release and authority to one OCI index and two child
> images/configs.

**NEW:**

> Record the terminal deployed-parity disposition for the frozen Story 1.20 basis. The packet must
> prove that the exact 14 approved package archives are unrecoverable after the recorded exhaustive
> search, that no retained candidate has one exact source/package/release/OCI lineage, and that both
> prohibited cross-lineage splices fail. It must record
> `deployed_runtime_parity: unavailable`, `selected_deployed_identity: null`, and no deployment or
> consumer authority. The result is terminal for this basis rather than blocked on another search.

**Rationale:** This is the strongest true statement the evidence can support. It cannot be converted
into a positive parity claim by more analysis.

### 4.3 Preserve fail-closed safety in AC3

**Story:** 3.13
**Section:** Acceptance Criterion 3

**OLD:**

> Any missing or inconsistent identity keeps Story 3.13 non-`done` indefinitely.

**NEW:**

> Any missing or inconsistent identity keeps deployed parity unavailable and prevents any image
> authorization. Once the exact terminal-unavailable facts are content-bound and accepted under
> AC4, the evidence-disposition story may become `done`. Reopening positive deployed parity requires
> a new owner-approved source/package baseline and same-lineage release in a separately approved
> successor story; it cannot reopen this frozen packet.

**Rationale:** Fail-closed governs capability use, not whether a completed negative investigation
must remain permanently open.

### 4.4 Retain content-bound acceptance in AC4

**Story:** 3.13
**Section:** Acceptance Criterion 4

**OLD:**

> The EventStore owner, Release owner, and Test Architect accept a complete passing identity
> crosswalk before the story may become `done`.

**NEW:**

> The EventStore owner, Release owner, and Test Architect accept the same content-bound terminal
> packet, including its `unavailable` decision, exhausted-search basis, limitations, and explicit
> non-authorization. Only then may Story 3.13 become `done`.

The previously ratified reviewer roster remains valid. No approval is inferred from this planning
proposal; the final re-bound packet still requires its own durable receipts.

### 4.5 Dispose the current implementation loop

**Story:** 3.13
**Sections:** Tasks 4-9, proof packet, spec, verifier

**OLD:**

- Tasks 4-7 remain open pending recovery of the 14 archives and a same-lineage release.
- The proof packet lists eight reopen triggers against the frozen basis.
- Every fail-closed result forces the story to remain non-`done`.

**NEW:**

- Mark the positive branches of Tasks 4-7 `not applicable — terminally impossible for frozen
  basis`; do not mark them passed.
- Replace “Blockers And Reopen Triggers” with “Terminal Findings And Future Re-baseline
  Preconditions.”
- Preserve every observed failure and the 0/14 recovery result.
- Change the evaluator so the exact terminal-unavailable packet is a valid completed disposition,
  while any `pass`, non-null deployed identity, inferred authority, missing blocker, or cross-lineage
  splice still fails.
- Rebind the review subject once after the approved edits. Do not perform another broad hardening
  review of the same proof machinery.
- Move to `review` for AC4 receipts; after all three valid receipts, set Story 3.13 and its spec to
  `done`, then close Epic 3 if no other non-optional story remains open.

**Rationale:** The implementation must stop optimizing an impossible positive branch and validate
the terminal outcome that the evidence actually establishes.

### 4.6 Add Story 1.21 for the corrupt evidence trees

**Story:** 1.21 Frozen Story 1.20 Evidence Integrity Repair
**Classification:** Post-closure Epic 1 evidence maintenance; does not reopen Story 1.20 or Epic 1
**Initial status:** `backlog`

**NEW STORY:**

> As an EventStore evidence owner, I want the three SDK-token-sweep drifts repaired under explicit
> predecessor authority, so that each frozen Story 1.20 critical manifest again verifies without
> granting Story 3.13 authority over Epic 1 evidence.

Acceptance boundary:

1. Pin `089369bb` as the introducing commit and resolve each exact pre-sweep Git blob before any
   write.
2. Limit repair to the three `environment.txt` files under `38f85086...`, `4983299103...`, and
   `ec0d35a0...`; any broader difference halts the story.
3. Restore the exact pre-sweep bytes rather than normalize to the current SDK.
4. Verify every entry in each `critical-evidence-sha256.txt` manifest.
5. Verify and report `nuget-sha256.txt` separately. Missing proof archives remain unrecoverable and
   must not be relabeled as content corruption, restored, rebuilt, or inferred.
6. Add a focused guardrail that detects future byte drift in the frozen evidence set.
7. Record EventStore evidence-owner authorization and Test Architect verification of the exact
   repair diff.
8. Leave Story 1.20's decision, approved identities, consumer authorization, and Epic 1 status
   unchanged.

**Rationale:** The corruption is real and repairable from Git history, but its authority and
completion semantics belong to frozen Epic 1 evidence, not to deployed-mode Story 3.13.

### 4.7 Planning and architecture wording

**PRD FR36 traceability — NEW clarification:**

> Story 1.20 owns the available source/package parity decision. Story 3.13 owns the deployed-mode
> disposition and may complete as `unavailable`; that result authorizes no image and cannot satisfy
> a consumer's deployed-mode gate. Future positive deployed parity requires a fresh approved
> baseline and release.

**Architecture AD-22 — NEW clarification:**

> A deployed-mode evidence story may close with a terminal unavailable disposition when exact
> artifact bytes are proven unrecoverable and no same-lineage candidate exists. Story completion
> never converts unavailable evidence into parity. Only a new owner-approved source/package packet
> and same-lineage immutable release may support a later positive deployed identity.

**Epic 1 maintenance — NEW clarification:**

> Story 1.21 is post-closure evidence maintenance. Its backlog or execution status does not reopen
> the completed Epic 1 capability or alter Story 1.20's approval decision.

## 5. Implementation Handoff

**Scope classification:** Moderate — backlog reorganization and acceptance-contract changes require
Product Owner / Developer coordination, followed by owner and Test Architect receipts.

### Product Owner / architecture responsibilities

- Approve the distinction between Story 3.13 `done` and deployed parity `unavailable`.
- Approve Story 1.21 as post-closure evidence maintenance without reopening Epic 1.
- Approve the PRD/AD-22 traceability clarifications and future re-baseline trigger.

### Developer responsibilities

- Apply the exact Story 3.13 story/spec/proof/evaluator/status/docs changes above without touching
  predecessor bytes.
- Create Story 1.21 and bind the deferred HIGH finding to it; do not implement its repair without
  its explicit authority record.
- Preserve all existing user changes and avoid unrelated review findings.
- Run only focused schema/verifier, checksum, YAML, Markdown, and diff checks required by the
  approved edits.

### Owner and Test Architect responsibilities

- Review the final re-bound terminal-unavailable Story 3.13 subject.
- Supply the three durable AC4 receipts for that exact subject.
- Review Story 1.21 separately when its evidence-repair authority and implementation are ready.

### Success criteria

1. Story 3.13 records `deployed_runtime_parity: unavailable` and no selected or authorized image.
2. The exact 0/14 package result and cross-lineage mismatch remain preserved.
3. The terminal packet passes the focused evaluator without weakening positive-parity rejection.
4. Three content-bound AC4 receipts accept the exact negative disposition.
5. Story 3.13 and Epic 3 may close without changing Stories 1.20/3.12 or Epic 1.
6. Story 1.21 exists in backlog with exclusive scope over the three corrupt trees.
7. No thirteenth hardening or package-recovery pass is started against the frozen basis.

## 6. Change Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Story 3.13 deployed-runtime parity closure. |
| 1.2 Core problem | [x] | Failed approach / technical impossibility: exact proof archives are unrecoverable. |
| 1.3 Evidence | [x] | 0/14 package recovery, no same-lineage release, three independently verified checksum drifts. |
| 2.1 Current epic viability | [x] | Epic 3 remains viable after terminal-disposition re-scope. |
| 2.2 Epic-level change | [x] | Re-scope 3.13; add post-closure Story 1.21 maintenance. |
| 2.3 Remaining epics | [x] | No downstream epic requires a positive 3.13 result. |
| 2.4 New epic need | [N/A] | No new top-level epic is needed. |
| 2.5 Ordering/priority | [x] | Close 3.13 first; Story 1.21 is independent backlog work. |
| 3.1 PRD conflict | [x] | FR36 safety remains intact; traceability needs outcome clarification only. |
| 3.2 Architecture conflict | [x] | AD-22 needs terminal-unavailable semantics; no component change. |
| 3.3 UX conflict | [N/A] | No UI or journey impact. |
| 3.4 Other artifacts | [x] | Story/spec/proof/evaluator/status/docs/deferred ledger require synchronized edits. |
| 4.1 Direct adjustment | [x] Viable | Low-medium effort, low risk, no external mutation. |
| 4.2 Rollback | [x] Not viable | Cannot recreate missing bytes and risks historical evidence. |
| 4.3 MVP review | [x] Not required | Source/package parity remains available; deployed mode stays safely unavailable. |
| 4.4 Selected path | [x] | Direct adjustment plus independently owned evidence-maintenance story. |
| 5.1-5.5 Proposal components | [x] | Issue, impact, rationale, MVP effect, and handoff are defined. |
| 6.1 Checklist review | [x] | All applicable analysis items are addressed. |
| 6.2 Proposal accuracy | [x] | Claims were checked against repository artifacts and checksum manifests. |
| 6.3 Explicit approval | [x] | Administrator approved the complete proposal on 2026-08-12. |
| 6.4 Sprint status update | [x] | Story 1.21 was added as `backlog`; Story 1.20 and Epic 1 remain `done`; Story 3.13 remains `in-progress` pending implementation and AC4. |
| 6.5 Handoff confirmation | [x] | Moderate-scope handoff is active for Product Owner / Developer execution followed by content-bound owner and Test Architect acceptance. |

## 7. Approval And Workflow Handoff Record

**Decision:** Approved by Administrator on 2026-08-12.

**Scope classification:** Moderate.

**Artifacts modified by this Correct Course workflow:**

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

**Routed to:** Product Owner / Developer for the synchronized Story 3.13 acceptance-contract,
planning, proof-packet, focused-verifier, documentation, and tracking changes. The EventStore owner,
Release owner, and Test Architect then review and accept the exact re-bound terminal packet.

**Implementation boundary:** Approval authorizes the changes specified in this proposal. It does
not itself mark Story 3.13 `done`, approve a deployed identity, request or infer AC4 receipts, repair
predecessor evidence, publish or deploy artifacts, mutate a registry or consumer, or modify a
submodule. Story 1.21 remains backlog until its separate predecessor-repair authority is recorded.

**Workflow result:** The terminal decision is to close Story 3.13 through an accepted
`deployed_runtime_parity: unavailable` disposition and to route the three corrupt frozen Story 1.20
trees exclusively to Story 1.21. No thirteenth hardening or recovery pass is authorized against the
frozen basis.
