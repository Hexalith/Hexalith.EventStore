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

1. Stories 1.14-1.19 are complete and reviewed, and Story 1.2 platform provenance is
   complete before lifecycle/provenance evidence is accepted.
2. Every parity capability is classified `available` or the packet remains `still blocked`;
   no partial consumer migration is authorized.
3. Evidence records source/test paths, exact commands, persisted read-back, environment,
   limitations, and rollback guidance; mock-only or HTTP-only proof cannot close a row.
4. A named EventStore owner reviews the completed exact-SHA evidence and records approval,
   date, durable source, accepted scope, limitations, and migration decision.
5. Under AD-22, the packet distinguishes and pins: the exact EventStore source commit;
   exact NuGet package IDs, versions, and hashes; and the exact container repository,
   immutable digest, and platform set. One identity must never stand in for another.
6. Consumer repositories verify both gitlink and checkout against the approved source SHA,
   or exact package/container identities when that is the approved consumption mode.
7. Any unresolved prerequisite, review, identity, production proof, or owner decision leaves
   the packet `still blocked`, Story 1.20 non-`done`, and Epic 1 `in-progress`.

Produces: `1-20-owner-approved-parity-closure-proof-packet.md`.

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

Summary: Produced and review-hardened a fail-closed parity proof packet. It authorizes no
consumer migration because Story 1.19 is still in review, no clean runtime SHA has been
selected and tested, package/container identities are not pinned, and no named EventStore
owner has approved the evidence.

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
- Production-path, package-build, package-consumer, and container publication gates: NOT RUN by design because the prerequisite gate failed before an approvable runtime SHA could be selected.

Residual risks: Story 1.19 still requires final review disposition; Story 1.16's retained
follow-up recommendation needs explicit reconciliation; exact source/package/container
identities and persisted production evidence remain absent; named owner approval remains
absent. No consumer migration is authorized.
