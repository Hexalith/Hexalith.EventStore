---
title: Story 1.16 Review And Story 1.20 Proof Closure
status: approved-for-implementation
created: 2026-07-16
approved_at: 2026-07-16T11:39:40+02:00
approved_by: Administrator
project: eventstore
workflow: bmad-correct-course
mode: incremental
scope_classification: minor-planning-medium-execution
handoff_route: Developer agent
trigger_story: "1.20"
affected_stories:
  - "1.16"
  - "1.20"
---

# Sprint Change Proposal — Story 1.16 Review And Story 1.20 Proof Closure

## 1. Issue Summary

Story 1.20 cannot produce an owner-approved `available` projection/query parity
packet while Story 1.16 retains an unresolved follow-up-review recommendation and
the packet lacks a passing, approvable runtime tied to one clean committed SHA.
Exact package and container identities and named proof-result approval are also absent.

The blocked packet was correct when authored. The repository has since advanced:
Story 1.19 is now reviewed and `done`. A concurrent exact-SHA execution subsequently
tested clean candidate `85877902f8d60a466ab90cd8b68b53838863db1c`: the Release build
and broad regression lanes passed, but the live-sidecar lane failed reproducibly.
That evidence clears the stale Story 1.19 prerequisite and establishes a failed
candidate, not an approved runtime.

### 1.1 Trigger classification

- **Primary type:** incomplete evidence/review closure discovered during story
  execution.
- **Secondary type:** technical/release prerequisite drift discovered during the
  mandatory repository-identity audit.
- **Not a strategic pivot:** FR36 and AD-22 already require the intended result.
- **Not a scope-reduction request:** the Phase 4 MVP and Parties migration boundary
  remain unchanged.

### 1.2 Repository identity observed on 2026-07-16

| Field | Observed value | Evidentiary meaning |
| --- | --- | --- |
| Repository root | `/home/administrator/projects/hexalith/eventstore` | Correct owner repository for the proposed planning and evidence work. |
| Remote | `https://github.com/Hexalith/Hexalith.EventStore.git` | Canonical EventStore origin configured locally. |
| Branch | `main` | Repository-default working branch. |
| Failed candidate SHA | `85877902f8d60a466ab90cd8b68b53838863db1c` | Tested from a clean detached checkout; not approved because the live gate failed. |
| Tracking state | Local `HEAD` equals local `origin/main` | Local tracking equality only; no claim about an unfetched remote state. |
| Initial worktree audit | Clean | Established the repository state before the concurrent exact-SHA run. |
| Root submodules | Seven initialized `references/...` entries matching `.gitmodules` | Root-declared submodule rule satisfied. |
| Nested submodules | No nested module gitdirs detected | No accidental nested initialization detected. |

Root submodule identities observed:

| Path | Commit |
| --- | --- |
| `references/Hexalith.AI.Tools` | `f62b420045c195d4b3c8eb78c4c4e7955a0de7c9` |
| `references/Hexalith.Builds` | `640b59c1434e4e1e079771c401e11048772c7a27` |
| `references/Hexalith.Commons` | `b03469b13408530bb757d3d02279c2d772ee4848` |
| `references/Hexalith.FrontComposer` | `8b229698fb847f6763cf333560c47be6bd052478` |
| `references/Hexalith.Memories` | `0b5d0160bbc381a97bab08010dd064bf58f92f65` |
| `references/Hexalith.PolymorphicSerializations` | `501749b66cb4b6831765a16da3cb5af55410499a` |
| `references/Hexalith.Tenants` | `66f5b2e7b0fe2e519ad4af200d226632a923e51f` |

### 1.3 Concrete blocker evidence

| Evidence | Current state | Consequence |
| --- | --- | --- |
| Story 1.16 source spec | `status: done`, `followup_review_recommended: true` | Story 1.20 cannot claim all prerequisite reviews are dispositioned. |
| Story 1.19 | Reviewed and `done` | Former prerequisite blocker is cleared, subject to exact-SHA rerun. |
| Story 1.20 story | `status: blocked`; sprint entry `in-progress` | Correct fail-closed state. |
| Story 1.20 packet | `candidate_source_sha: 85877902...`, `tested_runtime_sha: null`, `documentation_commit_sha: null`, `final_decision: still blocked` | The failed candidate is recorded without granting consumer migration authority. |
| Exact-SHA execution | Release build passed with 0 warnings/errors; broad unit/focused lanes passed; full live-sidecar failed 42/44; isolated named-dispatch class failed 5/6; isolated normal-delivery method failed 0/1 | Reproducible lifecycle-cleanup failure disqualifies the candidate. |
| Capability rows | All remain `still blocked` | Partial passing evidence cannot promote any row while the cross-cutting live gate fails. |
| NuGet proof | Manifest names 14 packages; approved versions and SHA-256 hashes absent | Inventory is known, artifact identity is not pinned. |
| Container proof | Repository resolves to `registry.hexalith.com/eventstore`; digest/platform provenance absent | Configuration identity is known, immutable deployed identity is not pinned. |
| Owner review | No named proof-result reviewer, date, durable source, or migration authorization | AD-22 approval gate remains open. |
| Runtime baseline | Repository and machine use SDK `10.0.301` / runtime `10.0.9`; central ASP.NET pins remain `10.0.9` | Architecture AD-11's required `10.0.302` / `10.0.10` baseline is not satisfied. |

### 1.4 Decision at proposal time

The current state remains `still blocked`. SHA `85877902...` is a tested **failed
candidate**, not an approved runtime. It must not be written to `tested_runtime_sha`,
used to authorize a consumer gitlink, or treated as package/container proof.

### 1.5 Concurrent-change reconciliation

After this proposal was drafted, another process updated the Story 1.20 story and
proof packet with the failed exact-SHA run, appended its lifecycle-cleanup item to
`deferred-work.md`, and checked out `references/Hexalith.Memories` at
`4102c10d22a5e065c1232cbc3b53eff9e786020b` while the root gitlink remains
`0b5d0160bbc381a97bab08010dd064bf58f92f65`. The submodule worktree is clean, but the
root therefore reports a modified gitlink. This workflow did not make, overwrite, or
attribute those concurrent changes. The proposal incorporates their observable
evidence and preserves them for ownership review.

## 2. Impact Analysis

### 2.1 Epic impact

Epic 1 remains viable as planned. Stories 1.14-1.19 are now recorded `done`; Story
1.20 remains the correct final parity-closure story. No epic needs to be added,
removed, redefined, or reordered.

Epic 3 is not reopened. The AD-11 mismatch receives a narrowly scoped blocking
corrective-work entry owned by the EventStore build/release maintainer. Story 1.20
remains evidence-only and does not absorb a runtime dependency change.

### 2.2 Story impact

| Story | Impact |
| --- | --- |
| 1.16 | Add a dated follow-up-review disposition tied to the final candidate runtime. Clear the flag only after the review genuinely approves. |
| 1.19 | No edit to implementation or status; update the Story 1.20 packet ledger to reflect that review is complete. |
| 1.20 | Clarify exact execution order, AD-11 precondition, tested-runtime versus documentation SHA, and separate source/package/container identity requirements. Keep non-`done` until the packet is `available`. |

### 2.3 Artifact conflicts and required adjustments

| Artifact | Status | Required change |
| --- | --- | --- |
| PRD | No conflict | FR36 already requires production-path proof, owner review, and exact runtime identity. No edit. |
| Architecture | Supports correction | AD-22 already defines exact source/package/container identity. AD-11 exposes a new blocking baseline mismatch. No architecture text edit. |
| UX | Not applicable | No EventStore UI behavior changes. Parties/local rollback remains unchanged. |
| Story 1.16 record | Action needed | Record exact-runtime follow-up review and final disposition. |
| Story 1.20 record | Action needed | Add explicit acceptance and sequencing gates. |
| Story 1.20 proof packet | Action needed | Preserve the failed candidate evidence, refresh prerequisite truth, and keep approved-runtime fields null. |
| Deferred-work ledger | Action needed | Preserve the concurrently added lifecycle-cleanup item and add the distinct AD-11 build/release corrective item with owner and closure conditions. |
| Sprint status | Action needed | Explain current blockers while preserving existing `in-progress` state. |
| Package/container inventory | No immediate mutation | Pins are generated and recorded only by the approved exact-SHA proof execution. |

### 2.4 Technical and release impact

This proposal authorizes no runtime change by itself. The required AD-11 correction is
a separate build/release-owned commit. After that correction and repair or explicit
owner disposition of the reproduced lifecycle-cleanup defect, proof execution must use
one unchanged detached commit and must capture:

- SDK/runtime, DAPR, Docker/buildx, state-store, Redis, component, and platform
  environment identity;
- fresh Release build and positive per-project/xUnit evidence;
- persisted production-path evidence for every parity row;
- exactly the 14 manifest-governed NuGet packages at one proof version, with SHA-256
  hashes and package-only consumer validation;
- `registry.hexalith.com/eventstore` as the repository plus an immutable digest,
  non-empty platform set, and provenance mapping to the tested source SHA;
- named EventStore and release-owner dispositions.

Container publication is an external release operation. It requires the release
owner's authority and credentials and is not performed merely because this planning
proposal is approved.

### 2.5 Schedule, effort, and risk

- **Planning change effort:** Low.
- **Execution effort:** Medium; it includes a dependency/security pin correction,
  detached exact-SHA test execution, live persisted lanes, package proof, container
  provenance, and two owner reviews.
- **Timeline impact:** The technical gates are bounded, but elapsed time depends on
  availability of SDK/runtime `10.0.302` / `10.0.10`, live infrastructure, registry
  credentials, and named reviewers.
- **Implementation risk:** Medium because the baseline pin can affect the full build
  and package graph.
- **Closure-integrity risk if bypassed:** High; a false `available` decision could
  authorize Parties to remove rollback infrastructure against an untested artifact.

## 3. Recommended Approach

Use **Direct Adjustment** within the existing Epic 1 plan.

1. Preserve and assign the reproduced lifecycle-cleanup corrective item.
2. Record the AD-11 correction as separate blocking build/release work.
3. Repair or explicitly disposition the lifecycle defect and land the AD-11 correction.
4. Select the resulting clean runtime commit, or a reviewed descendant.
5. Run Story 1.16's follow-up review against that exact runtime.
6. Run the Story 1.20 detached production, package, and container gates without
   changing the candidate SHA.
7. Commit review and evidence documentation separately.
8. Obtain named proof-result approval and explicit migration authorization.
9. Change the packet and sprint status only when every gate is satisfied.

### 3.1 Alternatives considered

#### Potential rollback

Not viable. Stories 1.14-1.19 are complete and reviewed; rolling them back would not
create the missing exact-SHA proof or owner decision and would add compatibility and
data-integrity risk.

#### MVP review or scope reduction

Not needed. FR36 and AD-22 already express the correct gate. Reducing the evidence
requirement would weaken the approved consumer-removal safety boundary.

#### Add a new epic or parity story

Not needed. Story 1.20 already owns the closure. The AD-11 mismatch is a narrow
corrective item, not a new product capability.

## 4. Detailed Change Proposals

All five edits below were approved individually by Administrator during incremental
review on 2026-07-16. They remain proposed handoff edits until the complete proposal
receives explicit approval. The exact-SHA evidence refinement in sections 1, 4.2, 4.4,
and 4.5 was added after concurrent execution and is included in that pending
complete-proposal review.

### 4.1 Story 1.16 — follow-up-review disposition

**Artifact:** `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md`

**OLD:**

```yaml
followup_review_recommended: true
```

```text
- Follow-up review recommended: true because review-driven changes crossed
  public contract, client, gateway, generator, fake, documentation, and test surfaces.
```

**NEW, only after the review genuinely completes:**

```yaml
followup_review_recommended: false
```

```text
### <actual-date> — Follow-up review disposition

- active_story_id: 1.16
- reviewed_runtime_sha: <same 40-hex SHA as the Story 1.20 packet>
- reviewer: <named reviewer>
- durable_source: <commit or PR>
- scope: integrated lifecycle/provenance implementation at the candidate runtime
- findings_and_resolutions: <patched, deferred, rejected, and accepted findings>
- verification: <exact commands and results>
- residual_limitations: <explicit list or none>
- disposition: approved
```

**Guard:** While any field is missing or the review is not approved, retain `true` and
keep Story 1.20 blocked.

**Rationale:** Binds the review disposition to the runtime being approved instead of
clearing stale metadata administratively.

### 4.2 Story 1.20 proof packet — exact-SHA failure and readiness re-audit

**Artifact:** `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`

**OLD:**

```text
- Story 1.19 remains in review and is a hard blocker.
- The inspected checkout is dirty.
- Candidate discovery SHA is 26842d28...
- No runtime SHA, package/container pins, or owner approval exists.
```

**NEW:**

```text
### Exact-SHA failure and readiness re-audit — 2026-07-16

- repository: https://github.com/Hexalith/Hexalith.EventStore.git
- repository_root: /home/administrator/projects/hexalith/eventstore
- branch: main
- tracking_state: local HEAD equals local origin/main
- failed_candidate_sha: 85877902f8d60a466ab90cd8b68b53838863db1c
- detached_candidate_worktree: clean before and after the attempted gates
- root_submodules: seven declared and initialized
- nested_submodules: none detected
- tested_runtime_sha: not selected

The candidate passed the warning-free Release build and broad unit/focused lanes.
The full live-sidecar lane failed 42/44. The isolated named-dispatch class failed
5/6, and the isolated normal-delivery method failed 0/1 because the lifecycle hash
remained present. This reproducible result disqualifies the candidate. Passing
subsets remain non-authorizing, and the candidate does not authorize consumer
migration.

Prerequisite ledger changes:

- Story 1.19: done; review approved and focused persisted/live evidence
  recorded. Its former review blocker is cleared, subject to exact-SHA rerun.
- Story 1.16: done but follow-up review disposition remains open. Hard blocker.
- Lifecycle cleanup: reproducible normal-delivery failure is recorded as scoped
  corrective work. Hard blocker until repaired or explicitly dispositioned by
  the responsible owner and a later candidate passes the complete lane.
- AD-11 security baseline: repository and machine remain on SDK 10.0.301 /
  runtime 10.0.9 instead of required SDK 10.0.302 / ASP.NET 10.0.10.
  Hard blocker before selecting or publishing the proof runtime.
- Package identity: exact 14-package inventory confirmed, but approved
  versions and SHA-256 hashes remain absent.
- Container identity: repository registry.hexalith.com/eventstore confirmed,
  but immutable digest, platform set, and SHA provenance remain absent.
- Owner review: named approval and durable approval source remain absent.

Final decision remains still blocked.
```

**Rationale:** Removes a stale prerequisite, preserves the failed exact-SHA evidence,
and prevents either the clean checkout or passing subsets from being mislabeled as an
approved runtime.

### 4.3 Story 1.20 — acceptance and execution order

**Artifact:** `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`

**OLD:**

```text
1. Stories 1.14-1.19 are complete and reviewed...
5. Under AD-22, the packet distinguishes and pins source, packages,
   and container identity.
7. Any unresolved prerequisite, review, identity, production proof,
   or owner decision leaves the packet still blocked.
```

**NEW:**

```text
1. Stories 1.14-1.19 are complete and reviewed; Story 1.16 additionally
   has a dated follow-up-review disposition tied to the candidate runtime.
   Its historical `spec-1-11...` filename does not weaken active identity 1.16.

5. Before a runtime SHA is selected, the committed candidate satisfies
   architecture AD-11, including SDK 10.0.302 and ASP.NET 10.0.10, or a
   later approved replacement baseline. Any required pin correction belongs
   to a scoped build/release corrective commit, not this evidence-only story.

6. The selected candidate is tested from a clean detached checkout. The same
   40-hex commit is present before and after every production-path, package,
   and container gate.

7. `tested_runtime_sha` identifies the unchanged runtime commit.
   `documentation_commit_sha` identifies the later evidence-only commit that
   records review results and approvals. A documentation commit never
   substitutes for the tested runtime.

8. Under AD-22, the packet separately pins:
   - exact EventStore source SHA;
   - all 14 NuGet package IDs, one exact version, and SHA-256 per package;
   - container repository, immutable digest, platform set, and provenance
     mapping to the tested runtime SHA.

9. Story 1.16 follow-up review and the final Story 1.20 packet each receive
   the required named review. Proof-result approval records reviewer, date,
   durable source, accepted scope, limitations, and explicit migration
   authorization.

10. Any unresolved prerequisite, security baseline, review, runtime identity,
    production-path result, package/container pin, or owner decision keeps
    `final_decision: still blocked`, Story 1.20 non-done, and Epic 1
    in progress.
```

Add:

```text
## Closure Execution Order

1. Repair or explicitly disposition the recorded lifecycle-cleanup defect.
2. Land the AD-11 security-baseline correction under its owning build/release work.
3. Select the resulting clean committed runtime SHA.
4. Run and disposition Story 1.16 follow-up review against that SHA.
5. Run all detached exact-SHA persisted production-path gates.
6. Build and hash the exact 14-package inventory.
7. Publish/inspect the container and record immutable digest/platform provenance.
8. Commit the evidence-only documentation update separately.
9. Obtain named EventStore and release-owner approval.
10. Change the packet to available and update sprint status only if every gate passes.
```

**Rationale:** Makes the identity and approval sequence executable and prevents a
documentation commit from masquerading as runtime proof.

### 4.4 Deferred-work ledger — AD-11 corrective item

**Artifact:** `_bmad-output/implementation-artifacts/deferred-work.md`

**OLD:** The concurrent exact-SHA run added a lifecycle-cleanup corrective item, but no
owning corrective item records the distinct AD-11 mismatch.

**NEW:**

```text
## Deferred from: Story 1.20 correct-course readiness audit (2026-07-16)

- source_spec:
  `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`
- status: open-blocking
- owner: EventStore build/release maintainer
- summary: Land the architecture AD-11 .NET/ASP.NET security baseline before
  selecting Story 1.20's tested runtime SHA.
- evidence:
  - `global.json` pins SDK `10.0.301`;
  - the installed SDK is `10.0.301` and host/runtime is `10.0.9`;
  - `Directory.Packages.props` retains ASP.NET package pins at `10.0.9`;
  - architecture AD-11 requires SDK `10.0.302` and ASP.NET `10.0.10`
    before the next implementation or release slice.
- consequence: No current commit may be promoted to `tested_runtime_sha`, and
  no package/container proof publication may be accepted, while this mismatch
  remains.
- closure:
  1. Update the repository seed and central ASP.NET pins to the AD-11 baseline,
     or record named architecture-owner approval for a newer replacement.
  2. Install and capture the matching SDK/runtime.
  3. Restore and build `Hexalith.EventStore.slnx` in Release.
  4. Run the focused package/runtime validation owned by the correction.
  5. Commit the correction independently.
  6. Use that resulting commit, or a reviewed descendant, as the Story 1.20
     candidate runtime.
- reopen_trigger: Any Story 1.20 packet update, package build, container
  publication, or owner-approval request that names a runtime without satisfying
  this baseline.
```

**Rationale:** Gives the baseline correction a durable owner without mixing code into
the evidence-only closure story or overwriting the separately recorded lifecycle defect.

### 4.5 Sprint status — preserve fail-closed state

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:**

```yaml
# Cannot become done while its projection/query owner packet remains
# `still blocked`.
# Explicitly excludes payload-protection G5 and Parties Story 8.7;
# those are Epic 8.
1-20-owner-approved-parity-closure-and-runtime-pin: in-progress
```

**NEW:**

```yaml
# Story 1.19 review is complete. Story 1.20 remains in progress while:
# - Story 1.16 follow-up review lacks a dated exact-runtime disposition;
# - candidate 85877902... failed the live-sidecar gate reproducibly;
# - the lifecycle-cleanup corrective item remains unresolved;
# - the AD-11 SDK/ASP.NET security-baseline corrective item is open;
# - no later clean committed runtime SHA has passed all detached production gates;
# - exact NuGet versions/hashes and container digest/platform provenance are absent;
# - named EventStore and release-owner approval is absent.
# `final_decision: still blocked` cannot transition this story or Epic 1 to done.
# Explicitly excludes payload-protection G5 and Parties Story 8.7;
# those are Epic 8.
1-20-owner-approved-parity-closure-and-runtime-pin: in-progress
```

**Rationale:** Updates reader and automation context without making a false status
transition.

### 4.6 Intentionally unchanged artifacts

- `prd.md`, `architecture.md`, and UX artifacts;
- source, tests, package projects, `tools/release-packages.json`, and central pins;
- DAPR/AppHost topology and persisted data;
- any root-declared submodule content or pointer;
- Parties source, gitlink, packages, deployment, and rollback infrastructure.

## 5. Implementation Handoff

### 5.1 Scope classification

**Minor planning change with medium execution effort.** The existing epic/story model,
requirements, and architecture remain valid. Execution crosses build/release,
verification, and owner-approval boundaries but does not require backlog
reorganization or fundamental replanning.

### 5.2 Recipients and responsibilities

| Recipient | Responsibility |
| --- | --- |
| Projection lifecycle owner | Own or explicitly disposition the reproducible named-delivery lifecycle-cleanup failure, with a scoped implementation/review artifact before rerun. |
| EventStore build/release maintainer | Own the AD-11 corrective commit, install/capture the required toolchain, and produce clean Release/package/container evidence. |
| Independent Story 1.16 reviewer | Review the integrated lifecycle/provenance runtime at the exact candidate SHA and record findings, verification, residuals, and disposition. |
| Developer/evidence owner | Run the packet's detached exact-SHA gates, collect persisted evidence, keep runtime and documentation SHAs distinct, and update the proof packet truthfully. |
| EventStore release owner | Authorize any registry publication, approve the exact 14-package versions/hashes and immutable image digest/platform provenance. |
| Named EventStore owner | Review the completed packet, accept or reject residual limitations, and explicitly set consumer-migration authorization. |
| Parties maintainer | After an `available` decision, verify source gitlink/checkout or exact package/container identity. No Parties mutation is authorized here. |

### 5.3 Success criteria

1. AD-11 is satisfied by the candidate runtime or a named architecture owner approves
   a newer replacement baseline.
2. Story 1.16's follow-up-review flag is cleared only with a dated, named,
   exact-runtime review disposition.
3. The reproduced named-delivery lifecycle-cleanup failure is repaired or explicitly
   dispositioned, and one later unchanged 40-hex commit passes every required detached
   production-path gate.
4. Persisted detail, index, marker, lifecycle, checkpoint, key-ring, batch, delivery,
   rebuild, and cursor evidence closes every parity row.
5. Exactly 14 manifest packages are built at one version, hashed, validated, and
   consumed without project-reference substitution.
6. The immutable EventStore image digest and platform set map to the tested SHA.
7. Named EventStore and release owners approve the exact evidence and limitations in
   durable sources.
8. The packet changes to exactly `final_decision: available` and explicitly authorizes
   migration; otherwise all current non-done states remain.
9. Parties rollback infrastructure remains intact until Parties independently verifies
   the approved artifact identity.

## 6. Change-Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Story 1.20 closure exposed Story 1.16's unresolved follow-up recommendation. |
| 1.2 Core problem | [x] Done | Incomplete evidence/review closure plus a newly discovered AD-11 prerequisite mismatch. |
| 1.3 Supporting evidence | [x] Done | Repository identity, story flags/status, failed exact-SHA live gate, packet null pins, package/container inventory, toolchain, and approval state inspected. |
| 2.1 Current epic viability | [x] Done | Epic 1 remains completable as planned. |
| 2.2 Epic-level changes | [N/A] Skip | No epic scope or acceptance change needed. |
| 2.3 Remaining epics | [x] Done | No future epic invalidated; Parties 8.6 remains gated. |
| 2.4 New/obsolete epics | [N/A] Skip | None. |
| 2.5 Ordering/priority | [x] Done | Story 1.20 remains the last parity closure; AD-11 correction precedes candidate selection. |
| 3.1 PRD conflict | [x] Done | FR36 already requires the proposed gate. |
| 3.2 Architecture conflict | [!] Action-needed | AD-22 supports closure; current pins violate AD-11's next-slice baseline. |
| 3.3 UX conflict | [N/A] Skip | No UI behavior change. |
| 3.4 Other artifacts | [!] Action-needed | Story records, proof packet, ledger, sprint comments, packages, container provenance, and approvals. |
| 4.1 Direct adjustment | [x] Viable | Low planning effort, medium execution effort/risk. |
| 4.2 Rollback | [x] Not viable | Adds risk without producing missing evidence. |
| 4.3 MVP review | [x] Not viable | No scope defect; weakening FR36 is unsafe. |
| 4.4 Recommended path | [x] Done | Direct Adjustment. |
| 5.1 Issue summary | [x] Done | Section 1. |
| 5.2 Impact/artifact needs | [x] Done | Section 2. |
| 5.3 Recommended approach | [x] Done | Section 3. |
| 5.4 MVP/action plan | [x] Done | MVP unchanged; closure sequence defined. |
| 5.5 Handoff plan | [x] Done | Section 5. |
| 6.1 Checklist completion | [x] Done | All applicable analysis items addressed. |
| 6.2 Proposal accuracy | [x] Done | Complete proposal reviewed after incorporating the concurrent failed-candidate evidence. |
| 6.3 User approval | [x] Done | Administrator explicitly approved implementation on 2026-07-16. |
| 6.4 Sprint-status update | [!] Action-needed | Developer handoff changes comments only and preserves Story 1.20/Epic 1 as non-done. |
| 6.5 Next steps/handoff | [x] Done | Minor-scope implementation route recorded for the Developer agent and named owners in Section 5. |

## 7. Approval State

- Incremental edit mode selected by default and accepted through use.
- All five detailed edits were individually approved by Administrator on
  2026-07-16.
- Administrator explicitly approved this complete proposal for implementation at
  `2026-07-16T11:39:40+02:00`, including the evidence refinement from the concurrent
  exact-SHA run.
- Scope is classified as Minor planning change with Medium execution effort. The
  primary route is the Developer agent, with the named lifecycle, build/release,
  review, and approval owners in Section 5.
- This workflow modified only this proposal. It did not modify the target story, proof
  packet, deferred-work ledger, sprint-status entry, runtime, source, test, package,
  container, submodule, or external repository. Concurrent modifications to three target
  artifacts and the Memories checkout were preserved and reconciled read-only.

## 8. Output-Path Decision

The workflow's date-only default path
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16.md` already
contains an unrelated approved payload-protection proposal. This collision-safe file
preserves that artifact and keeps the Story 1.16/1.20 correction independently
reviewable.

## 9. Workflow Execution Log

| Field | Value |
| --- | --- |
| Workflow | `bmad-correct-course` |
| Completion time | `2026-07-16T11:39:40+02:00` |
| User | Administrator |
| Mode | Incremental |
| Trigger | Story 1.16 follow-up review remained unresolved and Story 1.20 lacked an approvable runtime, package/container pins, and named owner approval. |
| Selected path | Direct Adjustment within Epic 1; no new epic, rollback, MVP reduction, PRD change, architecture change, or UX change. |
| Scope classification | Minor planning change with Medium execution effort. |
| User decision | Approved for implementation — `yes`. |
| Artifact modified by this workflow | This Sprint Change Proposal only. |
| Concurrent changes preserved | Story 1.20 story, proof packet, deferred-work ledger, and the `references/Hexalith.Memories` checkout. |
| Routed to | Developer agent, projection lifecycle owner, EventStore build/release maintainer, independent Story 1.16 reviewer, EventStore release owner, named EventStore owner, and Parties maintainer as defined in Section 5. |
| Handoff status | Complete: approved edit proposals, sequencing, ownership, fail-closed guards, and success criteria are recorded. |
| Implementation boundary | Apply the approved planning/evidence edits without promoting failed candidate `85877902...`; runtime repair, baseline correction, release publication, and named approvals remain separately owned execution work. |
