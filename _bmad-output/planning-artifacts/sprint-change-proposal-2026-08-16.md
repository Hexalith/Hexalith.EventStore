---
project: eventstore
date: 2026-08-16
workflow: bmad-correct-course
mode: batch
scope_classification: major
status: approved-for-major-replan-handoff
trigger: story-3-13-v3.94.1-immutable-provenance-failure
final_approved_by: Administrator
final_approved_on: 2026-08-16
handoff_status: routed
handoff_scope: product-manager-and-solution-architect-major-replan
sprint_tracking_mutation: deferred-until-planning-artifacts-are-updated-atomically
requested_directions:
  - publish-a-new-immutable-corrective-release
  - renegotiate-story-3.13-ac2-for-existing-v3.94.1
prior_identity_replacement_proposal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-14.md
prior_identity_replacement_disposition: preserve-as-implemented-history-supersede-positive-closure-expectation-only
prior_terminal_proposal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12.md
prior_terminal_proposal_disposition: remains-on-hold-for-old-fa2d1c99-basis
external_mutation_authorized: false
handoff_recipients:
  - Product Owner
  - Architect
  - Developer
  - Hexalith.Builds maintainer
  - EventStore owner
  - Release owner
  - Test Architect
---

# Sprint Change Proposal — Story 3.13 Dual-Track Provenance Correction

**Author:** Amelia (Developer) via `bmad-correct-course`
**Mode:** Batch
**Status:** APPROVED FOR MAJOR REPLAN HANDOFF
**Change scope:** Major acceptance-boundary and backlog correction. This proposal authorizes no
release, registry, NuGet, deployment, consumer, Git, submodule, or external-state mutation.

## 1. Issue Summary

The approved 2026-08-14 identity replacement produced a complete, reproducible Story 3.13 packet
for source `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, release `v3.94.1`, package version
`3.94.1`, and OCI index
`sha256:ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd`.

The packet proves substantial positive evidence:

- all 14 manifest package archives were downloaded and independently SHA-256 hashed;
- GitHub release `v3.94.1`, workflow run `31781920404` attempt 1, and Builds execution
  `f75daebd4c522c081a6f62e274cf25e07971de69` bind to the selected source SHA;
- the immutable OCI index contains exactly `linux/amd64` and `linux/arm64`, and the retained raw
  index, child-manifest, and child-config bytes pass digest, size, media-type, and platform checks;
- bounded Production `/alive` checks returned HTTP 200 with zero redirects on both immutable
  child digests; and
- repository verification is green: predecessor critical manifest 33/33, selected predecessor
  tree 40/40, NuGet archives 14/14, Story 3.13 core 34/34, outer manifest 3/3, focused verifier
  190/190, full Contracts suite 1427/1427, and Release build with zero warnings or errors.

The same immutable packet also proves three blockers:

1. Both child configs contain the malformed literal `https` for
   `org.opencontainers.image.source`, `org.opencontainers.image.url`, and
   `org.opencontainers.image.documentation`; `org.opencontainers.image.revision` is absent.
2. The retained authority explicitly records `deployment_authorized: false`.
3. The content-bound review subject
   `6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97` has 0/3 required
   acceptances.

Published v3.94.1 config bytes are immutable. Their labels cannot be repaired in place, and the
version tag must not be re-pointed. Under the current Story 3.13 AC2 and verifier, v3.94.1
therefore remains fail-closed and Story 3.13 cannot become `done` as a positive deployed-parity
closure.

On 2026-08-16 the Administrator selected both available directions:

1. create a new immutable corrective release with valid provenance; and
2. renegotiate AC2 for the existing v3.94.1 packet.

These directions are compatible only if they produce different outcomes. The v3.94.1 packet may
be accepted as a final, non-authorizing evidence disposition, while a later immutable release must
satisfy the positive AD-11/AD-22 deployed-parity contract. Treating v3.94.1 as a positive,
deployment-grade pass would make the corrective release redundant and would weaken the safety
invariant the new release is intended to restore.

## 2. Impact Analysis

### Epic and story impact

| Epic / story | Impact | Proposed disposition |
| --- | --- | --- |
| Epic 1 / Story 1.20 | None. Its source/package parity decision remains historical and complete. | Keep `done`; do not rewrite its evidence. |
| Epic 3 / Story 3.12 | None. It remains the completed historical multi-platform publishing correction. | Keep `done`; do not reopen it to create a new release. |
| Epic 3 / Story 3.13 | Its current title and positive AC2 overstate what immutable v3.94.1 can prove. | Re-scope to **v3.94.1 Deployed Runtime Evidence Disposition**. Permit `done` only after three reviewers accept one content-bound, non-authorizing rejection disposition. |
| Epic 3 / new Story 3.14 | A release-mechanics defect can emit malformed or absent OCI provenance labels. | Add **Corrective OCI Provenance Release** in `backlog`. |
| Epic 3 / new Story 3.15 | Positive deployed parity still needs independent post-release validation and human acceptance. | Add **Corrected Deployed Runtime Parity Closure** in `backlog`, dependent on 3.14. |
| Epic 3 overall | Positive FR36 deployed-runtime closure moves from 3.13 to 3.15. | Keep `in-progress` through 3.15. |
| Other epics / consumers | No capability or authority changes. | No migration, Parties 8.6, G5, or deployment effect. |

Story 3.13 may complete before or in parallel with Story 3.14 because it disposes already-retained
v3.94.1 evidence. Story 3.15 begins only after Story 3.14 supplies a new immutable candidate. This
preserves backward-only dependencies and the independent release-versus-acceptance boundary used
by Stories 3.12 and 3.13.

### Artifact impact

| Artifact | Required adjustment after approval |
| --- | --- |
| `epics.md` | Re-scope 3.13; add 3.14 and 3.15; move positive FR36 deployed closure to 3.15. |
| `prd.md` | Preserve FR36; distinguish rejected-candidate disposition from positive deployed parity; update traceability from 3.13 to 3.15 for positive closure. |
| `architecture.md` AD-11 | Make the already-enforced OCI provenance-label contract explicit and keep immutable correction-by-later-version semantics. |
| `architecture.md` AD-22 | Replace the 2026-08-14 amendment's positive-closure expectation with a truthful v3.94.1 disposition and name 3.15 as the successor positive closure. |
| Story 3.13 story/spec | Change purpose, AC2, AC3 completion semantics, frozen intent, tasks, matrix, and lifecycle wording without changing retained v3.94.1 bytes. |
| Story 3.13 evidence | Preserve the current packet and subject as immutable failed evidence; create one small disposition envelope that cites them instead of recapturing registry/package/runtime evidence. |
| Story 3.13 verifier | Accept only the exact terminal rejected-candidate disposition as story-completable; continue rejecting any v3.94.1 `pass`, selected deployed identity, or deployment authorization. |
| New 3.14 artifacts | Release-mechanics fix, tests, separate release authority, new semantic release, and release evidence. |
| New 3.15 artifacts | Independent identity crosswalk, verifier, proof packet, and three content-bound acceptances for the new release only. |
| `sprint-status.yaml` | Keep 3.13 `in-progress` until its revised AC4 passes; add 3.14 and 3.15 as `backlog`; keep Epic 3 `in-progress`. |
| `docs/ci.md` | Document the v3.94.1 rejection and the 3.14-to-3.15 release/closure handoff. |

UX, runtime behavior, public APIs, data schemas, and consumer code are unaffected by this planning
change. A later 3.14 implementation may change release configuration or the shared publisher, but
only under its own implementation scope and maintainer authority.

### PRD and architecture impact

The MVP remains achievable and no requirement is removed. FR36 still requires exact identity
parity before consumer infrastructure removal. NFR9, NFR11, and NFR16 remain unchanged.

AD-11 is strengthened as documentation of the contract already enforced by Story 3.13. It is not
waived for v3.94.1. AD-22 continues to fail closed: a completed negative evidence story grants no
image selection, deployment, or consumer authority. Positive deployed-runtime parity is deferred
to Story 3.15, not deleted.

## 3. Options And Recommended Path

### Option 1 — Direct adjustment with two coordinated tracks (recommended)

- **Effort:** Medium for planning and v3.94.1 disposition; high for separately authorized release
  execution and final validation.
- **Risk:** Medium overall; release mutation remains high-risk and separately gated.
- **Result:** 3.13 terminates honestly on v3.94.1; 3.14 corrects publication; 3.15 restores positive
  deployed parity.
- **Why:** It satisfies both Administrator-selected directions without weakening AD-11 or
  rewriting immutable history.

### Option 2 — Roll back v3.94.1 or recent story work (not viable)

Rollback cannot change published config bytes, would discard valid package/index/runtime evidence,
and risks rewriting approved history. Neither the v3.94.1 tag nor registry objects may be deleted
or re-pointed.

### Option 3 — Waive malformed labels and call v3.94.1 a positive pass (rejected)

This would turn a proven provenance defect into an accepted deployment-grade identity, conflict
with current fail-closed tests and spec intent, and remove the reason for a corrective immutable
release. It also leaves `deployment_authorized: false` and 0/3 acceptances unresolved.

### Option 4 — Reduce or remove FR36 deployed parity (rejected)

The original MVP remains achievable through a later release. Removing the requirement would weaken
consumer-migration safety for schedule convenience.

**Selected approach:** Hybrid direct adjustment using Option 1.

## 4. Detailed Change Proposals

### 4.1 Re-scope Story 3.13 around the existing v3.94.1 packet

**Current title:** `Story 3.13: Deployed Runtime Parity Closure`

**Proposed title:** `Story 3.13: v3.94.1 Deployed Runtime Evidence Disposition`

**OLD story outcome:**

> Operators can select a conforming image after one exact v3.94.1 lineage passes AC2 and AC4.

**NEW story outcome:**

> Operators receive one owner-reviewed disposition of the immutable v3.94.1 candidate. The story
> may complete when the retained packet is accepted as rejected and non-authorizing; positive
> deployed-runtime parity remains open in successor Story 3.15.

This is a story-completion correction, not an artifact-conformance waiver. The machine-readable
result must keep:

```json
{
  "candidate": "v3.94.1",
  "candidate_disposition": "rejected-non-authorizing",
  "deployed_runtime_parity": "unavailable-for-v3.94.1",
  "selected_deployed_identity": null,
  "deployment_authorized": false
}
```

### 4.2 Replace Story 3.13 AC2

**OLD:**

> Source `80d12ef5…`, release `v3.94.1`, and all 14 packages at `3.94.1` map through one workflow
> run and durable release-owner authority to the exact index and both child/config identities;
> every provenance field passes.

**NEW:**

> The disposition envelope references the immutable v3.94.1 review subject
> `6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97` and proves that its
> source/package/workflow/index/child/config/runtime observations are complete, while its exact
> malformed provenance labels, absent revision, and `deployment_authorized: false` state are
> preserved. It records `candidate_disposition: rejected-non-authorizing`,
> `deployed_runtime_parity: unavailable-for-v3.94.1`, and
> `selected_deployed_identity: null`. It must not reinterpret the failed fields as passing.

**Rationale:** AC2 becomes satisfiable as an evidence-disposition criterion while remaining
fail-closed as a capability criterion.

### 4.3 Amend Story 3.13 AC3 and AC4 completion semantics

**AC3 NEW:**

> A missing or inconsistent fact still rejects the disposition envelope. A complete, accepted
> negative disposition may complete Story 3.13, but it never authorizes v3.94.1, closes positive
> FR36 deployed parity, or substitutes for Story 3.15.

**AC4 NEW:**

> The EventStore owner, Release owner, and Test Architect each accept the same content-bound
> disposition envelope and its referenced immutable `6cee8dad…` evidence subject. The receipts
> explicitly accept the rejected/non-authorizing result and the successor-work boundary. This
> planning approval is not an AC4 receipt.

The current 0/3 acceptance state remains truthful until those receipts exist. Do not infer a
receipt from the Administrator's selection of this correction path.

### 4.4 Story 3.13 implementation boundary

After approval:

1. Preserve all v3.94.1 evidence bytes, checksums, raw OCI objects, proof packet, and review subject.
2. Create one content-bound disposition envelope that references the existing subject and this
   approved proposal.
3. Update the focused verifier so only that exact rejected/non-authorizing shape is story-
   completable. Any `pass`, non-null selected identity, `deployment_authorized: true`, omitted
   malformed label, or cross-lineage splice remains rejected.
4. Collect three new receipts for the disposition envelope.
5. Move Story 3.13 to `done` only after the envelope and all receipts pass. Keep Epic 3
   `in-progress` because 3.14 and 3.15 remain.

No registry readback, package download, runtime smoke, or review-subject regeneration is required
unless an independent verifier proves a retained checksum mismatch.

### 4.5 Add Story 3.14 — Corrective OCI Provenance Release

**Requirements covered:** FR22, FR25, NFR9, NFR11, NFR16, NFR17; governed by AD-11 and AD-12.

As an EventStore release owner,
I want a new semantic release whose package, workflow, OCI graph, and config provenance all bind to
one exact source SHA,
so that a successor parity story can validate a deployment-grade candidate without mutating
v3.94.1.

**Acceptance boundary:**

1. Reproduce the v3.94.1 label defect in focused release-contract tests before changing release
   configuration or the shared publisher.
2. Correct the owning layer so both platform configs contain identical, valid values:
   - `org.opencontainers.image.source`: the exact public EventStore repository URI;
   - `org.opencontainers.image.url`: an absolute public HTTPS project/release URI;
   - `org.opencontainers.image.documentation`: an absolute public HTTPS documentation URI;
   - `org.opencontainers.image.revision`: the exact 40-character release source SHA; and
   - `org.opencontainers.image.version`: the exact semantic release version.
3. Keep v3.94.1 resolvable as failed, non-authorizing evidence. Do not delete, overwrite, re-point,
   or reclassify its tag, packages, index, children, configs, or release record.
4. Before any external write, obtain a separate durable release-owner authority record binding the
   repository, new semantic version, exact source SHA, exact registry/repository, 14-package scope,
   `linux/amd64` and `linux/arm64`, owner, date, rationale, and validity window.
5. Publish exactly the 14 manifest packages at the new version and one immutable OCI index for
   `eventstore` with exactly the two required platform children.
6. Independently verify all package hashes, raw index/child/config digests and sizes, media types,
   config platforms, provenance labels, and bounded Production smokes.
7. Record the exact workflow run/attempt, Builds execution SHA, publisher/validator identities,
   source SHA, package manifest hash, release tag, and immutable index digest.
8. Hand the complete release packet to Story 3.15. Story 3.14 itself authorizes no consumer
   migration, deployment, Parties 8.6, or G5.

**Explicit exclusions:** no EventStore runtime behavior change; no package/container inventory
change; no Dockerfile; no v3.94.1 mutation; no consumer update; no credential, signing, SBOM, or
attestation expansion unless separately approved.

### 4.6 Add Story 3.15 — Corrected Deployed Runtime Parity Closure

**Requirements covered:** FR36, NFR12, NFR16; governed by AD-11, AD-12, and AD-22.

As an EventStore release owner,
I want the new Story 3.14 release independently mapped and accepted as one exact deployed-runtime
lineage,
so that operators have a positive, deployment-grade identity without relying on v3.94.1.

**Acceptance boundary:**

1. Depend only on completed Stories 1.20 and 3.14. Reference Story 3.13's v3.94.1 rejection as
   historical negative evidence; never splice it into the new candidate.
2. Independently map one exact source SHA, all 14 package IDs/versions/hashes, semantic release,
   workflow run/attempt, Builds execution SHA, durable release authority, OCI index, both
   children/configs, valid provenance labels, and Production runtime results.
3. Revalidate raw bytes and relationships instead of copying Story 3.14 pass flags.
4. Fail closed on any missing, mutable-only, expired, inconsistent, or mixed-lineage fact.
5. Obtain EventStore-owner, Release-owner, and Test-Architect acceptance of one unchanged
   content-bound subject before `done`.
6. Record the exact validated index digest as the positive deployed identity. Story completion
   alone still grants no deployment or consumer migration authority.

### 4.7 PRD traceability edits

**FR36 / deployed-runtime note — replace the current v3.94.1 positive pin:**

> Story 1.20 remains the completed source/package parity gate. Story 3.13 records the immutable
> v3.94.1 candidate as rejected and non-authorizing because its config provenance is malformed and
> its retained authority forbids deployment. Story 3.15 owns positive deployed-runtime parity for
> the separately authorized corrective release produced by Story 3.14. Neither result reopens
> Story 1.20 or authorizes Parties 8.6, G5, deployment, or consumer migration.

Update the FR36, NFR9, NFR11, and NFR16 traceability rows to include Stories 3.14 and 3.15 where
their release/evidence responsibilities apply.

### 4.8 Architecture edits

**AD-11 — add the explicit provenance-label rule:**

> Each released platform config carries the same exact semantic version and source identity.
> `org.opencontainers.image.source`, `.url`, and `.documentation` are absolute public HTTPS URIs;
> `.revision` equals the exact 40-character release source SHA; and `.version` equals the semantic
> release version. The labels are independently compared with release/workflow provenance. A
> malformed, absent, platform-divergent, or mismatched required label fails the release evidence
> gate. An immutable failed release remains resolvable and is corrected only by a later semantic
> version.

**AD-22 — replace the 2026-08-14 Story 3.13 positive amendment:**

> **Scoped disposition — Story 3.13 v3.94.1 evidence, recorded 2026-08-16.** The exact v3.94.1
> package, workflow, index, child/config, and runtime evidence is retained, but both configs have
> malformed source/URL/documentation labels, no revision label, and no deployment authorization.
> Story 3.13 may complete only as a content-bound rejected/non-authorizing disposition. Positive
> deployed-runtime parity moves to Story 3.15 after Story 3.14 produces a new immutable release.
> No v3.94.1 waiver, reclassification, deployment, consumer migration, Parties 8.6, G5, or
> Story 1.20 change is authorized.

### 4.9 Sprint tracking edits after approval

Add these entries without removing historical comments:

```yaml
  3-13-v3-94-1-deployed-runtime-evidence-disposition: in-progress
  3-14-corrective-oci-provenance-release: backlog
  3-15-corrected-deployed-runtime-parity-closure: backlog
```

Rename the 3.13 key only when the story filename/spec key and every repository reference are
updated atomically. If the tracking system treats keys as immutable identities, keep the existing
key and change only its display title; do not create duplicate Story 3.13 rows.

## 5. Implementation And Handoff Plan

### Product Owner / Architect

- Approve the distinction between v3.94.1 evidence disposition and positive deployed parity.
- Apply the PRD, epic, architecture, and traceability changes as one coherent planning update.
- Keep Stories 1.20 and 3.12 `done`; keep Epic 3 `in-progress` through Story 3.15.

### Developer

- Implement only the approved Story 3.13 disposition changes first, preserving all retained
  v3.94.1 bytes.
- Create Story 3.14/3.15 specs after the planning artifacts and sprint tracker are updated.
- Reproduce and fix the label-generation defect under Story 3.14; use focused tests before release.
- Do not publish, push, commit, update submodules, or mutate external state without the separate
  authorities required by the owning action.

### Hexalith.Builds maintainer

- Determine whether the defect is owned by EventStore MSBuild configuration, shared publisher
  argument handling, .NET SDK container metadata, or their interaction.
- Approve any shared publisher change and exact Builds revision before EventStore consumes it.

### EventStore owner / Release owner / Test Architect

- Review and accept the Story 3.13 negative disposition envelope if accurate.
- Separately authorize and review Story 3.14 release work.
- Accept Story 3.15 only after independent positive-lineage verification.

### Sequencing and success criteria

1. Approve and apply the planning correction.
2. Complete Story 3.13's negative disposition using the retained v3.94.1 packet and three new
   receipts; no external release is required for this step.
3. Diagnose and test the release-label fix under Story 3.14.
4. Obtain separate durable release authority, then publish one new immutable semantic release.
5. Independently validate and collect three acceptances under Story 3.15.
6. Close Epic 3 only when Story 3.15 records a positive exact deployed identity.

The correction succeeds when v3.94.1 is preserved as rejected/non-authorizing evidence, the new
release satisfies the full AD-11 provenance contract, Story 3.15 positively closes FR36, and no
consumer, deployment, Parties 8.6, G5, Story 1.20, or historical artifact is changed by inference.

## 6. Change Analysis Checklist Results

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Story 3.13 exposed the immutable v3.94.1 provenance and authority failure. |
| 1.2 Core problem | [x] | Failed approach plus newly selected dual requirement: immutable bytes cannot satisfy current positive AC2, while stakeholders want both disposition and correction. |
| 1.3 Evidence | [x] | 14/14 packages, exact OCI graph, two Production passes, malformed `https` labels, absent revision, `deployment_authorized: false`, subject `6cee8dad…`, 0/3 receipts, all local verification green. |
| 2.1 Current epic viability | [x] | Epic 3 remains viable with a 3.13 disposition and 3.14/3.15 successor sequence. |
| 2.2 Epic-level changes | [x] | Re-scope 3.13; add two Epic 3 stories; preserve predecessors. |
| 2.3 Remaining epics | [x] | No other epic criteria change; consumer and G5 gates remain untouched. |
| 2.4 New epic need | [N/A] | No new top-level epic; two focused Epic 3 stories are sufficient. |
| 2.5 Order / priority | [x] | 3.13 may close independently; 3.14 precedes 3.15; Epic 3 stays open. |
| 3.1 PRD conflict | [x] | FR36 is preserved; positive traceability moves to 3.15. MVP scope is unchanged. |
| 3.2 Architecture conflict | [x] | AD-11 must state the provenance-label invariant; AD-22 must record the negative v3.94.1 disposition and successor. |
| 3.3 UX conflict | [N/A] | No UI or interaction impact. |
| 3.4 Other artifacts | [x] | Story/spec, verifier, disposition evidence, release configuration/tests, CI docs, and sprint tracker require coordinated updates. |
| 4.1 Direct adjustment | [x] Viable | Medium planning/disposition effort; high separately authorized release effort; medium overall risk. |
| 4.2 Potential rollback | [x] Not viable | Cannot repair immutable published bytes or justify discarding verified evidence. |
| 4.3 MVP review | [x] Viable without reduction | Core goals remain achievable; no deferral outside MVP is required. |
| 4.4 Selected path | [x] | Hybrid direct adjustment: negative v3.94.1 disposition plus corrective release and successor closure. |
| 5.1 Issue summary | [x] | Trigger, exact evidence, contradiction, and dual selection recorded. |
| 5.2 Impact summary | [x] | Epic, PRD, architecture, story, evidence, tests, CI docs, and tracking impacts recorded. |
| 5.3 Recommended path | [x] | Selected path and rejected alternatives include effort, risk, and rationale. |
| 5.4 MVP / action plan | [x] | MVP unchanged; five-step sequence and dependencies recorded. |
| 5.5 Handoff plan | [x] | PO, Architect, Developer, Builds maintainer, owners, and Test Architect responsibilities recorded. |
| 6.1 Checklist review | [x] | Every applicable analysis item is addressed; implementation remains approval-gated. |
| 6.2 Proposal accuracy | [x] | Cross-checked against PRD FR36/NFR9/NFR11/NFR16, AD-11/AD-22, Stories 3.12/3.13, sprint tracking, immutable evidence, and current verification. |
| 6.3 Explicit final approval | [x] | Administrator explicitly approved the exact proposal on 2026-08-16. |
| 6.4 Sprint status update | [!] Action-needed | Deferred to the major-replan handoff so epics, story identities, and tracker rows change atomically; adding tracker rows before the stories exist would create planning drift. |
| 6.5 Handoff confirmation | [x] | Routed to Product Manager / Solution Architect, with PO, Developer, Builds maintainer, owners, and Test Architect responsibilities preserved in Section 5. |

## 7. Approval And Handoff Record

The Administrator reviewed the complete batch proposal and explicitly approved it on 2026-08-16.
The change is classified **major** and is routed to Product Manager / Solution Architect for the
atomic PRD, architecture, epic, story, and sprint-tracking replan defined above. The approved
proposal and its detailed old-to-new edits are the handoff deliverables.

This approval authorizes the planning/backlog correction and its scoped repository implementation
handoff. It does not authorize a release, registry or NuGet mutation,
deployment, consumer migration, Git commit/push/branch change, submodule update, Parties 8.6, G5,
or any human acceptance by inference. Story 3.14 external publication still requires its separate,
durable release-owner authority record.

### Workflow execution log

- **Issue addressed:** immutable v3.94.1 provenance cannot satisfy the existing positive Story
  3.13 contract, while both a truthful AC2 disposition and a corrective release are required.
- **Scope:** major.
- **Artifact finalized:** this Sprint Change Proposal, including exact story, PRD, architecture,
  sprint-tracking, implementation, and handoff changes.
- **Routed to:** Product Manager / Solution Architect for atomic replan; Product Owner / Developer
  for subsequent backlog implementation; Builds maintainer, EventStore owner, Release owner, and
  Test Architect for their separately gated responsibilities.
- **Tracker state:** intentionally unchanged during routing. Story 3.13 remains `in-progress`, and
  Stories 3.14/3.15 must be added only with their approved epic/story definitions.
