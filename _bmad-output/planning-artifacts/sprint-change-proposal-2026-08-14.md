---
project: eventstore
date: 2026-08-14
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved-for-implementation
trigger: story-3-13-replace-unrecoverable-proof-basis-with-v3.94.1
final_approved_by: Administrator
final_approved_on: 2026-08-14
existing_terminal_proposal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12.md
existing_terminal_proposal_disposition: remains-on-hold-not-deleted-not-implemented-not-this-path
existing_gate_proposal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12-story-3-13-step-3-gate.md
handoff_recipients:
  - Product Owner
  - Developer
  - EventStore owner
  - Release owner
  - Test Architect
---

# Sprint Change Proposal — Story 3.13 Exact Identity Replacement To v3.94.1

**Author:** Amelia (Developer) via `bmad-correct-course`
**Mode:** Batch
**Status:** APPROVED FOR IMPLEMENTATION
**Change scope:** Moderate acceptance-boundary correction. No runtime, release, registry,
deployment, consumer, submodule, or predecessor-evidence mutation in this proposal file.

## 1. Issue Summary

Story 3.13 was written to map one exact lineage from the Story 1.20-approved source
`fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` and package version `999.1.20-proof.fa2d1c9910f8`
through a semantic release to a two-platform OCI image.

That positive proof is dead for the frozen 1.20 basis:

- the Administrator stated on 2026-08-14 that the 14 original proof archives **do not exist**;
- NuGet.org, GitHub Packages, and local search recovered 0 of 14;
- rebuilding lookalikes is forbidden;
- Story 3.12's `v3.77.2` is a different source (`77a9a442…`) and was only a rejected
  other-lineage row, not “the current release.”

The Administrator then selected a **new exact identity**, not a splice and not the held
terminal-`unavailable` path:

- release tag **`v3.94.1`**
- source commit **`80d12ef5eee71a9fe3ea7be51171da4a71b69a28`** (equals current `HEAD`)
- GitHub release: https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.94.1
- NuGet.org returns HTTP 200 for `Hexalith.EventStore.Contracts` `3.94.1`

`fa2d1c99` is an ancestor 200 commits behind `v3.94.1`. Ancestry still does not satisfy
exact-source equality. This proposal **replaces** the Story 3.13 selected identity; it does
not treat `v3.94.1` as a descendant of the proof packages.

The 2026-08-12 terminal-`unavailable` proposal remains on hold and is **not** this path.
The 2026-08-12 Step 3 gate remains the historical halt record for the old basis.

## 2. Impact Analysis

### Epic impact

| Epic / story | Impact | Disposition |
| --- | --- | --- |
| Epic 1 / Story 1.20 | None. Source/package parity stays the approved `fa2d1c99` / `999.1.20-proof.fa2d1c9910f8` decision. | Keep `done`. Do not repair sibling evidence trees from Story 3.13. |
| Epic 3 / Story 3.12 | None. `v3.77.2` stays historical corrective-release evidence. | Keep `done`. |
| Epic 3 / Story 3.13 | Selected exact identity becomes `80d12ef5` / `v3.94.1` / package version `3.94.1`. | Keep `in-progress` until the new packet passes AC1–AC4. |
| Epic 3 overall | Still open on Story 3.13 only. | Keep `in-progress`. |
| Other epics | No consumer migration, G5, or Parties 8.6 gate change. | No change. |

No epic is added, removed, reordered, or moved into or out of MVP scope.

### Artifact impact

| Artifact | Required correction |
| --- | --- |
| `epics.md` Story 3.13 | State that deployed-mode mapping uses the owner-approved replacement exact SHA `80d12ef5` / `v3.94.1` after the 1.20 proof archives were declared nonexistent. Keep 1.20/3.12 unmodified. |
| `prd.md` FR36 / deployed-runtime note | Clarify that Story 1.20 still owns source/package parity; Story 3.13 may close deployed mode on a later owner-approved exact release SHA when the 1.20 proof bytes are gone. |
| `architecture.md` AD-22 | Add a dated Story 3.13 scoped amendment: deployed-mode exact SHA for this story is `80d12ef5` / `v3.94.1`. Confer no Parties 8.6, G5, or splice authority. |
| Story 3.13 spec frozen intent | Human-renegotiated: problem/approach/matrix target `v3.94.1` as the selected lineage; retain the old proof row as historical fail-closed, not as the selected candidate. |
| Story 3.13 record, proof packet, evidence | New content-addressed tree under `evidence/story-3-13/80d12ef5…/<index-digest>/`. Do not overwrite the `fa2d1c99` fail-closed packet. |
| Verifier | Bind the new selected identity; keep rejecting splices of 1.20 proof rows with `v3.94.1` or `v3.77.2` artifacts. |
| `sprint-status.yaml` / `docs/ci.md` | Stay `in-progress` until the new packet is complete; `done` only after AC4 on the new subject. |
| Prior AC4 comments | Void. They accept subject `394292a2…` of the old fail-closed proof packet. |

UX is unaffected. Runtime/release/registry/package/deployment/consumer/submodule bytes are not
changed by approving this proposal. Implementation after approval is **read-only evidence
assembly** against the already-published `v3.94.1` artifacts.

### Technical impact

After approval, AC2 becomes satisfiable **only if** independent checks prove one chain:

- source `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`
- all 14 `tools/release-packages.json` IDs at version `3.94.1`, each archive SHA-256 verified
- one GitHub release / workflow run / attempt / Builds execution SHA / publisher identity
- one immutable OCI index with `linux/amd64` and `linux/arm64` children and retained
  child/config response metadata
- Production `/alive` on both platform children
- three acceptances of the **new** review-subject SHA-256

A live NuGet 200 for Contracts `3.94.1` is inventory smoke, not the 14-archive proof.
Missing or mixed-lineage evidence still fail-closes. Story 3.13 still authorizes no
consumer migration, publication, registry mutation, or G5.

## 3. Recommended Approach

### Selected — Direct adjustment: replace the Story 3.13 exact identity

- **Effort:** Medium. Planning text plus a new evidence packet and verifier bindings.
  No thirteenth hardening pass on the old packet.
- **Risk:** Medium if anyone treats ancestry or the old AC4 comments as sufficient.
  Low if the new subject is content-bound and the old packet stays historical.
- **Rationale:** The owner declared the proof archives gone and named `v3.94.1` as the
  replacement exact SHA. That is a new baseline, not a splice. Terminal-`unavailable`
  would close the story with no deployed identity; the owner rejected that in favor of
  the live release.

### Rejected — Potential rollback

Cannot recreate the missing 1.20 archives and would risk approved Epic 1 history.

### Rejected — Implement the 2026-08-12 terminal-`unavailable` proposal

That path remains on hold. The owner chose a positive replacement identity instead.

### Rejected — Splice `v3.94.1` onto `fa2d1c99` / `999.1.20-proof.*`

Ancestry is not exact equality. The new chain must be entirely `80d12ef5` / `3.94.1`.

## 4. Detailed Change Proposals

### Story / epic — `epics.md` Story 3.13

**Section:** Focused validation, user story, and AC2 mapping sentence.

OLD:

> I want deployed runtime identity mapped back to the approved source/package parity evidence

NEW:

> I want deployed runtime identity mapped back to one owner-approved exact EventStore
> source/package/release lineage. After the Story 1.20 proof archives were declared
> nonexistent, that exact lineage is `80d12ef5` / `v3.94.1` / package version `3.94.1`.
> Stories 1.20 and 3.12 remain unmodified historical predecessors.

**Section:** Second Given/When/Then (“approved EventStore source SHA”)

OLD: maps “the approved EventStore source SHA and package IDs/versions/hashes” without naming
the replacement.

NEW: name `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, release `v3.94.1`, and the 14-package
inventory at `3.94.1`. Keep fail-closed and non-reopening of 1.20/3.12/Epic 1.

**Rationale:** Human renegotiation of the selected identity.

### PRD — `prd.md`

**Section:** FR36 deployed-runtime traceability (around the Parties/1.20 note).

ADD: Story 3.13 deployed-mode closure may use a later owner-approved exact release SHA when
the Story 1.20 proof package bytes are gone. Story 1.20 remains the source/package parity
gate for Parties 8.6. `v3.94.1` / `80d12ef5` is that later SHA for Story 3.13 only.

**Rationale:** FR36 stays; only the deployed-mode exact pin is updated.

### Architecture — `architecture.md` AD-22

ADD a dated scoped amendment after the Story 2.12 exception:

> **Scoped amendment — Story 3.13 deployed-mode exact SHA, recorded 2026-08-14.**
> The Story 1.20 proof archives at `999.1.20-proof.fa2d1c9910f8` do not exist. For Story 3.13
> deployed-mode closure only, the owner-approved exact EventStore SHA is
> `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, mapped to release `v3.94.1` and the 14
> manifest packages at version `3.94.1`. This does not reopen Story 1.20, authorize Parties
> 8.6 against `v3.94.1`, authorize G5, or permit splicing 1.20 proof hashes with `v3.94.1`
> artifacts.

**Rationale:** Same pattern as the existing dated Story 2.12 exception.

### Spec — `spec-3-13-deployed-runtime-parity-closure.md`

Human-renegotiate the `<frozen-after-approval>` block:

| Field | New text |
| --- | --- |
| Problem | Operators need one verified chain from `80d12ef5` / `v3.94.1` / `3.94.1` packages through the published release to a two-platform OCI image. The 1.20 proof archives do not exist. |
| Approach | Keep the `fa2d1c99` packet as historical fail-closed evidence. Assemble a new content-addressed packet for `v3.94.1` only. |
| Always | Preserve 1.20/3.12; one candidate; independent checks; non-`done` unless AC4 passes on the new subject. |
| Never | Splice 1.20 proof rows with `v3.94.1` or `v3.77.2`; infer identity from ancestry; rebuild the missing proof packages; claim `done` with missing `v3.94.1` evidence. |
| Matrix | Complete `v3.94.1` lineage → `pass`. Historical proof / `v3.77.2` / splice → `fail-closed`. |

**Rationale:** Frozen intent cannot change without this owner renegotiation.

### Story record and evidence

- Keep `_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c99…/` immutable.
- Create a new tree for `80d12ef5` / the `v3.94.1` index digest.
- Rebind proof packet, crosswalk, roster (reuse issue comment 5290564372 only if the
  three-role mapping is unchanged), and a new review subject.
- Tasks 4–7 and 9 become actionable again against `v3.94.1` and stay unchecked until
  independent evidence exists.
- Prior commit comments on subject `394292a2…` do not count.

### Sprint / CI

- `3-13-deployed-runtime-parity-closure: in-progress`
- `epic-3: in-progress`
- `docs/ci.md`: Story 3.13 now targets `v3.94.1` / `80d12ef5`; old proof packet is historical.

## 5. Implementation Handoff

**Scope:** Moderate — PO/DEV planning edits, then Developer evidence assembly.

**Product Owner**

- Approve or reject this proposal. Approval implements the identity replacement, not
  terminal-`unavailable`.
- Keep Epic 1 / Stories 1.20 and 3.12 `done`.

**Developer (after approval only)**

1. Apply the `epics.md`, `prd.md`, `architecture.md`, spec, story, sprint-comment, and
   `docs/ci.md` edits above.
2. Download and SHA-256 all 14 `3.94.1` archives from durable public sources.
3. Bind GitHub release `v3.94.1`, workflow run/attempt, Builds SHA, and publisher identity
   to `80d12ef5`.
4. Recapture digest-bound OCI index/child/config response metadata for the `v3.94.1` image.
5. Run Production `/alive` on both platform children and retain structured support-safe logs.
6. Emit a new fail-closed or pass packet from those bytes only. Rebind checksums.
7. Collect three new AC4 receipts for the new subject. Do not reuse `394292a2…` comments.
8. Stay non-`done` until AC4 passes.

**EventStore owner / Release owner / Test Architect**

- Accept only the new content-bound subject after the packet is internally complete.

**Success criteria**

1. Selected Story 3.13 identity is `80d12ef5` / `v3.94.1` / `3.94.1`.
2. Stories 1.20 and 3.12 and Epic 1 are unchanged.
3. No splice of 1.20 proof hashes with `v3.94.1` artifacts.
4. 2026-08-12 terminal proposal stays on hold and unimplemented.
5. Story remains `in-progress` until the new AC1–AC4 pass.
6. No runtime/release/registry/consumer mutation is authorized by this approval.

## 6. Change Analysis Checklist Results

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Story 3.13 deployed runtime parity closure. |
| 1.2 Core problem | [x] | Technical limitation: 1.20 proof archives do not exist. Owner selected `v3.94.1` as the replacement exact SHA. |
| 1.3 Evidence | [x] | Owner statement 2026-08-14; 0/14 proof archives; `v3.77.2` = `77a9a442`; `v3.94.1` = `80d12ef5` = HEAD; GitHub release exists; NuGet Contracts `3.94.1` HTTP 200. |
| 2.1 Current epic viability | [x] | Epic 3 remains viable if 3.13 retargets `v3.94.1`. |
| 2.2 Epic-level change | [x] | No epic add/remove/reorder. Story 3.13 acceptance identity changes. |
| 2.3 Remaining epics | [x] | No other epic acceptance change. Parties 8.6 stays on Story 1.20. |
| 2.4 New epic need | [N/A] | No new epic. |
| 2.5 Ordering/priority | [x] | No resequence. Resume 3.13 evidence assembly after approval. |
| 3.1 PRD conflict | [x] | FR36 preserved; deployed-mode pin clarified. MVP unchanged. |
| 3.2 Architecture conflict | [x] | AD-22 needs a dated Story 3.13 deployed-mode SHA amendment. |
| 3.3 UX conflict | [N/A] | No UI impact. |
| 3.4 Other artifacts | [x] | Spec, story, verifier, sprint, CI docs; new evidence tree. |
| 4.1 Direct adjustment | [x] Viable | Medium effort, medium risk if ancestry is misused. |
| 4.2 Potential rollback | [x] Not viable | Cannot recreate 1.20 archives. |
| 4.3 MVP review | [x] Not required | MVP unchanged. |
| 4.4 Selected path | [x] | Direct adjustment: replace exact identity with `v3.94.1`. |
| 5.1–5.5 Proposal parts | [x] | Issue, impact, path, edits, handoff recorded. |
| 6.1 Checklist review | [x] | Applicable items addressed. |
| 6.2 Proposal accuracy | [x] | Cross-checked against story, spec, epics, PRD FR36, AD-22, live `v3.94.1` tag/release/NuGet. |
| 6.3 Explicit approval | [x] | Administrator approved 2026-08-14. |
| 6.4 Sprint status update | [x] | Additive comments only; value stays `in-progress`. |
| 6.5 Handoff confirmation | [x] | Routed to Developer for v3.94.1 evidence assembly. |

## 7. Approval Record

Administrator approval was recorded on 2026-08-14. This approval authorizes the identity
replacement and read-only evidence assembly against published `v3.94.1` artifacts. It does
not mark Story 3.13 `done`, implement the 2026-08-12 terminal-`unavailable` path, or
authorize consumer migration, publication, registry mutation, or G5.
