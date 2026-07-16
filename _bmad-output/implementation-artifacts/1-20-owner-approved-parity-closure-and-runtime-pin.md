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
   including SDK `10.0.302`, ASP.NET `10.0.10`, and an installed
   `Microsoft.NETCore.App` `10.0.10` runtime, or a later replacement documented with
   the named architecture owner, approval date, durable source, rationale and exact
   candidate/toolchain/ASP.NET/runtime scope, and an unexpired `expires_at` value. The executable
   readiness preflight rejects a mismatched exact baseline and any missing, blank,
   malformed, expired, or out-of-scope replacement record before candidate gates. Any
   required pin correction belongs to scoped build/release corrective work, not this
   evidence-only story.
6. The selected candidate is tested from a clean detached checkout. The same 40-hex commit
   is present before and after every production-path, package, and container gate.
7. `tested_runtime_sha` identifies the unchanged runtime commit and equals A's
   `candidate_source_sha`; that real commit is the sole direct parent of evidence commit A,
   whose changed paths are restricted to `_bmad-output/`. Final durable approvals precede A,
   which records the results and approval references while keeping
   `documentation_commit_sha: null` and all decision/migration guards blocked. Pointer-only
   commit B, whose direct parent is A, changes only `documentation_commit_sha` to A's 40-hex
   SHA. The field never identifies B, so neither commit self-references.
8. Under AD-22, the packet separately pins the exact EventStore source SHA; all 14 NuGet
   package IDs, one exact version, and SHA-256 per package; and the container repository,
   immutable digest whose value equals the raw-manifest SHA-256, exact `linux/amd64` and
   `linux/arm64` platform set, and provenance mapping to the tested runtime SHA. Consumer
   repositories verify both gitlink and checkout against the approved source SHA, or those
   exact package/container identities when that is the approved consumption mode.
9. Story 1.16 follow-up review and the final Story 1.20 packet each receive the required
   named review. External container publication requires a durable release-owner authority
   record created before the registry operation and naming the owner, date, durable source,
   rationale, exact repository/tag/source-SHA scope, and an unexpired `expires_at` value.
   The record is copied and hashed, then revalidated at a fresh action timestamp immediately
   before publication after the candidate HEAD and source cleanliness are rechecked. Ignored
   inputs are limited to generated `bin`/`obj`; the authority record, action time, hashes, and
   actual publish properties are bound into provenance. Missing, blank, malformed, expired,
   or out-of-scope authority is rejected. This
   evidence-integrity preflight neither grants human authority nor replaces registry access
   control. After all evidence exists, the distinct release-owner disposition and named
   EventStore-owner approval must exist durably before evidence commit A.
10. Any unresolved prerequisite, security baseline, review, runtime identity,
    production-path result, package/container pin, publication authority, owner decision,
    evidence commit A, or valid pointer-only commit B keeps `final_decision: still blocked`,
    `authorize_consumer_migration: false`, `status: blocked`, Story 1.20 non-`done`, and
    Epic 1 `in-progress`.

Produces: `1-20-owner-approved-parity-closure-proof-packet.md`.

## Closure Execution Order

1. Repair or explicitly disposition the recorded lifecycle-cleanup defect.
2. Land the AD-11 security-baseline correction under its owning build/release work.
3. Select the resulting clean committed runtime SHA.
4. Run and disposition Story 1.16 follow-up review against that SHA.
5. Run all detached exact-SHA persisted production-path gates.
6. Build and hash the exact 14-package inventory.
7. Recheck the candidate HEAD and clean tracked/untracked source, allowing ignored inputs only
   under generated `bin`/`obj`; at a fresh action timestamp revalidate a durable release-owner
   authority record naming the owner, date, source, rationale, exact repository/tag/source-SHA
   scope, and `expires_at` value. Reject missing, blank, malformed, expired, out-of-scope, or
   dirty input before publication. Pin the raw manifest hash to the immutable digest and require
   exactly `linux/amd64` and `linux/arm64` before accepting container inspection evidence.
8. After all results exist, obtain the named EventStore-owner proof approval and the
   release-owner's distinct final disposition in durable external sources.
9. Create evidence commit A recording the results and approval references while preserving
   `documentation_commit_sha: null`, `final_decision: still blocked`, and
   `authorize_consumer_migration: false`.
10. Create direct-child pointer-only commit B changing only `documentation_commit_sha` to
    A's 40-hex SHA. Verify A is a single-parent evidence-only child of its equal
    candidate/tested-runtime identity, and verify B's one-field diff. This structural check
    substitutes for no proof or approval;
    only then may a separately authorized later status-only transition be considered. Until that
    transition passes every independent gate, retain `status: blocked` and the current
    sprint/Epic states.

## Current Sprint-Change Proposal Implementation File Inventory

This inventory describes the current Story 1.16/1.20 proposal implementation. It is
separate from the historical Auto Run `Files changed` list and Dev Agent `File List` below.

| File | Current proposal action |
| --- | --- |
| `1-20-owner-approved-parity-closure-proof-packet.md` | Preserve failed-run evidence and guards; add the observed audit, executable AD-11 gate, non-self-referential two-commit pin, and fail-closed publication-authority evidence procedure. |
| `1-20-owner-approved-parity-closure-and-runtime-pin.md` | Record acceptance, authority, commit sequencing, current inventory, and blocked closure order. |
| `deferred-work.md` | Preserve the one existing AD-11 `open-blocking` corrective item without duplication. |
| `sprint-status.yaml` | Preserve the approved Story 1.20 blocker comments and `in-progress` statuses; refresh `last_updated`. |
| `spec-1-11-complete-projection-freshness-lifecycle.md` | Verify only; retain `followup_review_recommended: true` with no disposition edit. |
| `epic-1-context.md` | Restore canonical endpoints and retain durable behavior, release, and compatibility constraints. |
| `spec-1-16-1-20-sprint-change-proposal.md` | Track this implementation and verification; preserve the frozen approval block unchanged. |

Concurrent runtime, test, branch, submodule, Parties, and persisted-data work is excluded
from this proposal inventory and remains owned by its existing changes.

## Review Triage Log

### Review Findings

- [x] [Review][Patch] [high] Restore Story 1.16 to fail-closed review state until a named disposition is tied to the committed candidate runtime [_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md:9]
- [x] [Review][Patch] [high] Make the AD-11 mutation fixture actually vary repository and installed SDK versions [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:318]
- [x] [Review][Patch] [high] Reject mixed ASP.NET patch bands across every effective 10.x central package pin [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:240]
- [x] [Review][Patch] [high] Compare package output with the literal approved 14-package inventory instead of trusting candidate-owned validators [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1047]
- [x] [Review][Patch] [medium] Generate the NuGet SHA-256 manifest with portable relative package filenames [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1051]
- [x] [Review][Patch] [high] Require the checksum manifest to cover exactly all 14 approved package filenames [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1433]
- [x] [Review][Patch] [high] Restore and rebuild the container project from fresh exact-candidate outputs immediately before publication [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1151]
- [x] [Review][Patch] [high] Recheck source and submodule cleanliness after the publication-capable build [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1163]
- [x] [Review][Patch] [high] Bind provenance to the digest produced by this publication rather than resolving a mutable tag afterward [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1166]
- [x] [Review][Patch] [high] Verify evidence commit A keeps the story and Epic 1 status guards blocked, not only packet front matter [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1318]
- [x] [Review][Patch] [medium] Add a negative A/B fixture where candidate and tested-runtime SHAs differ [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:549]
- [x] [Review][Patch] [medium] Hash consumer-fetched raw manifest bytes and compare them with the approved image digest [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1496]
- [x] [Review][Patch] [medium] Replace the obsolete current-work attribution check with a reproducible historical revision check [_bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md:223]

Applied and verified on 2026-07-16: the complete proposal verification script passes
fail-fast, including every added positive and negative fixture; `git diff --check` also passes.

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
