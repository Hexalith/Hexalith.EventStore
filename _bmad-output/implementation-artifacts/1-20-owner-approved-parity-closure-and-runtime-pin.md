---
created: 2026-07-15
story_id: "1.20"
story_key: 1-20-owner-approved-parity-closure-and-runtime-pin
status: blocked
baseline_revision: 26842d284f2da91399b7891bf7b5880ce2f6b561
followup_review_recommended: true
supersedes: 1-15-owner-approved-parity-closure-and-runtime-pin.md
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.20: Owner-Approved Parity Closure And Runtime Pin

Status: blocked

## Reissue Decision

This is the active identity for the unstarted historical Story 1.15. The earlier file
retains its discovery notes. Execution must re-read current status and evidence rather
than treating story creation as owner approval.

## Acceptance Boundary

1. Stories 1.14-1.19 are complete and reviewed, Story 1.2 platform provenance is
   complete before lifecycle/provenance evidence is accepted, and Story 1.16 additionally
   has a dated follow-up-review disposition tied to the candidate runtime. Its historical
   `spec-1-11...` filename does not weaken active identity 1.16.
2. Every parity capability is classified `available` or the packet remains `still blocked`;
   no partial consumer migration is authorized.
3. Evidence records source/test paths, exact commands, persisted read-back, environment,
   limitations, and rollback guidance; mock-only or HTTP-only proof cannot close a row.
4. A named EventStore owner reviews the completed exact-SHA evidence and records approval,
   date, durable source, accepted scope, limitations, and migration decision.
5. Before a runtime SHA is selected, the committed candidate satisfies architecture AD-11,
   including SDK `10.0.302` and ASP.NET `10.0.10`, or a later approved replacement
   baseline. Any required pin correction belongs to scoped build/release corrective work,
   not this evidence-only story.
6. The selected candidate is tested from a clean detached checkout. The same 40-hex commit
   is present before and after every production-path, package, and container gate.
7. `tested_runtime_sha` identifies the unchanged runtime commit.
   `documentation_commit_sha` identifies the later evidence-only commit that records review
   results and approvals. A documentation commit never substitutes for the tested runtime.
8. Under AD-22, the packet separately pins the exact EventStore source SHA; all 14 NuGet
   package IDs, one exact version, and SHA-256 per package; and the container repository,
   immutable digest, platform set, and provenance mapping to the tested runtime SHA. Consumer
   repositories verify both gitlink and checkout against the approved source SHA, or those
   exact package/container identities when that is the approved consumption mode.
9. Story 1.16 follow-up review and the final Story 1.20 packet each receive the required
   named review. External container publication requires explicit release-owner authority
   before the registry operation. That execution authority is not proof approval; after
   publication, the completed identity and provenance evidence requires a distinct
   release-owner disposition, while final proof approval and migration authorization remain
   with the named EventStore owner.
10. Any unresolved prerequisite, security baseline, review, runtime identity,
    production-path result, package/container pin, publication authority, or owner decision
    keeps `final_decision: still blocked`, Story 1.20 non-`done`, and Epic 1 `in-progress`.

Produces: `1-20-owner-approved-parity-closure-proof-packet.md`.

## Closure Execution Order

1. Repair or explicitly disposition the recorded lifecycle-cleanup defect.
2. Land the AD-11 security-baseline correction under its owning build/release work.
3. Select the resulting clean committed runtime SHA.
4. Run and disposition Story 1.16 follow-up review against that SHA.
5. Run all detached exact-SHA persisted production-path gates.
6. Build and hash the exact 14-package inventory.
7. Record explicit release-owner authority for the external registry operation, then
   publish and inspect the container and record immutable digest/platform provenance.
8. Commit the evidence-only documentation update separately.
9. Obtain named EventStore-owner proof approval, including explicit migration
   authorization, and the release-owner's separate final disposition of the completed
   package/container identities and provenance.
10. Change the packet to `available` and update sprint status only if every gate passes.

## Review Triage Log

### 2026-07-16 — Review pass

- intent_gap: 0
- bad_spec: 0
- patch: 17: (high 9, medium 7, low 1)
- defer: 1: (high 1, medium 0, low 0)
- reject: 4: (high 0, medium 2, low 2)
- addressed_findings:
  - `[high]` `[patch]` Preserved the story's fail-closed lifecycle by keeping a `still blocked` packet non-`done` and making the story status consistently `blocked`.
  - `[high]` `[patch]` Replaced prose-only parity commands with literal repository-root commands for every capability lane.
  - `[high]` `[patch]` Added a detached clean-checkout gate that rejects ignored source and configuration inputs.
  - `[high]` `[patch]` Required fresh Release rebuilds so pre-existing assemblies cannot be credited as exact-SHA evidence.
  - `[high]` `[patch]` Added method-list and positive XML-total guards so zero-match xUnit filters cannot pass a gate.
  - `[low]` `[patch]` Added a no-index whitespace check that covers the untracked proof packet.
  - `[medium]` `[patch]` Replaced the vague file-existence assertion with a reproducible all-cited-path check.
  - `[high]` `[patch]` Strengthened release inventory verification from count/uniqueness to the exact 14-package ID set.
  - `[high]` `[patch]` Added exact NuGet and immutable container consumer verification procedures.
  - `[high]` `[patch]` Added clean consumer and EventStore-submodule checks to the gitlink/checkout handoff.
  - `[medium]` `[patch]` Expanded the erasure evidence map through the conditional store and lifecycle admission surfaces.
  - `[medium]` `[patch]` Expanded the batching evidence map through concrete Dapr and in-memory implementations.
  - `[medium]` `[patch]` Expanded lifecycle/provenance evidence through freshness, gateway, controller, cache, generator, and E2E carriers.
  - `[medium]` `[patch]` Expanded delivery-safety evidence through reconciler, retry, outbox, dispatch, and persistence owners.
  - `[medium]` `[patch]` Expanded rebuild evidence through checkpoint, lifecycle, promotion, boundary, cancellation, failure, and resume surfaces.
  - `[medium]` `[patch]` Added the persisted data-protection/key-ring evidence path to cursor compatibility.
  - `[high]` `[patch]` Made the cross-cutting build, test, package, container, compatibility, and owner-disposition gate auditable.

## Auto Run Result

Status: blocked
Blocking condition: prerequisite and owner-approval gate remains unresolved

Summary: Produced and review-hardened a fail-closed parity proof packet. Story 1.19's review
is complete and is no longer a prerequisite blocker. The packet authorizes no consumer
migration because Story 1.16's follow-up review remains undispositioned, the only tested
clean candidate failed the live-sidecar gate, no replacement runtime has passed every gate,
package/container identities are not pinned, and no named EventStore owner has approved the
completed evidence.

Files changed:

- `1-20-owner-approved-parity-closure-and-runtime-pin.md` — recorded the workflow baseline, review triage, follow-up recommendation, and blocked outcome.
- `1-20-owner-approved-parity-closure-proof-packet.md` — added the prerequisite ledger, parity matrix, exact-SHA gate harness, identity pins, consumer guards, verification evidence, and fail-closed decision.
- `sprint-status.yaml` — kept Story 1.20 and Epic 1 in progress while closure remains blocked.
- `deferred-work.md` — recorded the generic dev-auto finalization guard gap exposed by this fail-closed story.

Review findings breakdown: 17 patches applied (high 9, medium 7, low 1); 1 high
pre-existing workflow issue deferred; 4 review findings rejected as non-actionable or
inapplicable to an intentionally blocked packet.

Follow-up review recommendation: `true`; patched counts are high 9, medium 7, low 1.
The weighted medium/low score is `22` (`3 x 7 + 1 x 1`), and high-severity patches also
independently require follow-up review.

Verification performed:

- Exact release manifest inventory: PASS; all and only the 14 approved package IDs are present.
- Cited-path audit: PASS; all 105 unique repository paths cited by the packet exist.
- xUnit v3 zero-match probe: PASS; a missing class returned zero with `total="0"`, while the positive class listed six methods, validating the packet's explicit guards.
- Tracked and no-index whitespace checks: PASS; the tracked diff and complete untracked packet are clean.
- Packet structural checks: PASS; all nine closure classifications remain `still blocked`, and migration authorization remains false.
- Exact-SHA production-path gate: FAILED for candidate `85877902...`; the full
  live-sidecar lane and both isolated reproductions retained the lifecycle-cleanup defect.
- Package-build, package-consumer, and container-publication gates: NOT RUN after the
  reproducible live failure; Story 1.19's completed review is no longer the blocker.

Residual risks: Story 1.19's review is complete and is no longer a blocker. Story 1.16's
retained follow-up recommendation still needs explicit reconciliation; the failed candidate
does not supply exact source/package/container identities or complete persisted production
evidence; named owner approval remains absent. No consumer migration is authorized.

## Dev Agent Record

### Debug Log

- 2026-07-16: Re-read current sprint/story evidence. Story 1.19 is now `done` with an
  approved review disposition; Story 1.16 still retains an undispositioned follow-up flag.
- Selected clean candidate `85877902f8d60a466ab90cd8b68b53838863db1c` and created a
  detached checkout with only the seven root-declared submodules initialized.
- Release solution build passed with 0 warnings/errors. Eighteen unit/focused projects,
  Testing.Integration, AppHost, and Admin UI E2E passed.
- Full live-sidecar validation failed 2/44. The named-dispatch class reproduced 1/6, and
  its normal-delivery method reproduced 1/1, meeting the workflow's three-consecutive-failure
  HALT condition.

### Completion Notes

- Story remains fail-closed and non-authorizing. No runtime code was changed because this
  closure story has no implementation task authorizing a projection lifecycle patch.
- Updated the proof packet with the current exact-SHA results and recorded a scoped
  corrective item in `deferred-work.md`.
- Package, container, provenance E2E, and owner-approval gates were not run after the
  reproducible live regression failure.

## File List

- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`
- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`

## Change Log

- 2026-07-16: Recorded the failed exact-SHA completion attempt and kept Story 1.20
  fail-closed pending a scoped lifecycle-cleanup fix, remaining review disposition, release
  identity gates, and named owner approval.
