---
title: 'Story 3.13 Deployed Runtime Parity Closure'
type: 'chore'
created: '2026-08-04'
status: 'done'
baseline_commit: '1d6e9321acfc416768c1c78e9facf573c9c41f71'
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Operators lack one verified chain from the Story 1.20-approved source/package bytes through a semantic release to a deployed two-platform OCI image. The proof packages are unrecoverable, while v3.77.2 uses a different source SHA.

**Approach:** Freeze both predecessors and assemble a support-safe, content-addressed crosswalk from independent checks. Produce a reproducible `fail-closed` review packet unless one exact lineage plus three content-bound acceptances satisfies Story 3.13.

## Boundaries & Constraints

**Always:** Preserve Stories 1.20/3.12; bind every field to one candidate and independent result; compare exact source, package, release, OCI, runtime, authority, and approval identities; retain raw registry bytes; distinguish environment from product failures; stay non-`done` unless AC4 passes.

**Ask First:** Any external or remote Git mutation; changes outside evidence/test/docs/story/status files; new authority/approval requests; or credentials beyond configured read-only task access.

**Never:** Splice candidate rows; infer identity from ancestry, tags, labels, branches, consumer SHAs, summaries, or prior approvals; rebuild proof packages; expose credentials; modify predecessors, runtime/release code, the package manifest, submodules, Epic 1, or consumers; claim `pass`/`done` with missing evidence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Complete lineage | Exact source/package/release/index chain, two runtime passes, three approvals | `pass`; immutable index recorded | Later byte changes invalidate approvals |
| Approved proof | `fa2d1c...` hashes, but package bytes or release provenance unavailable | `fail-closed`; blocker and owner recorded | Never substitute bytes |
| Corrective release | v3.77.2 chain at source `77a9a442...` | `fail-closed` for source mismatch | Ancestry is insufficient |
| Splice or tool gap | Mixed candidates or unavailable verification | Reject or record blocker/consequence/rerun trigger | Unavailable never means pass |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`, its proof packet, and `evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/` -- read-only approved identity; freeze the full 40-file tree because the passing 33-entry manifest omits approvals.
- `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` -- read-only v3.77.2 release/workflow/index/runtime evidence and historical failed releases.
- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md` and the exact evidence directory named below -- new crosswalk, raw evidence, blockers, checksums, and review subject.
- `tools/release-packages.json` -- read-only exact 14-package inventory and uniqueness authority.
- `references/Hexalith.Builds/Github/publish-containers/` -- read-only validator/smoke reuse; validation is SemVer/tag-first, while smoke uses bounded local Docker state.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` -- new verifier; reuse JSON mutation/hash/root patterns from adjacent packaging tests.
- `docs/ci.md:253` -- replace only the stale Story 3.12-to-1.20 deployed-closure ownership paragraph.
- Story 3.13 record and `sprint-status.yaml` -- truthful lifecycle only; predecessors and Epic 1 stay unchanged.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/` -- freeze predecessor identities, run safe independent checks, and record candidate rows/blockers.
- [x] `3-13-deployed-runtime-parity-closure-proof-packet.md` and `identity-crosswalk.json` -- create a versioned, citation-complete, checksum-bound verdict and approval subject with no cross-lineage fallback.
- [x] `DeployedRuntimeParityClosureTests.cs` -- enforce schema, exact sets, hashes, verdict, approval binding, and both prohibited splices without dependencies.
- [x] `docs/ci.md`, Story 3.13 record, and `sprint-status.yaml` -- correct ownership and record commands/results; move only to `in-review` for reproducible fail-closed evidence, or `done` solely after AC4.

### Review Findings

- [x] [Review][Patch] Add a repository-owned reviewer roster and hash-bound receipt loading [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:926]
- [x] [Review][Patch] Require all recovered package archives and hash their bytes [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:678]
- [x] [Review][Patch] Pin the selected package hash manifest to the approved identity [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:627]
- [x] [Review][Patch] Content-bind semantic-release provenance and the complete single lineage [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:663]
- [x] [Review][Patch] Validate deployment authority from its retained record, scope, identity, and validity [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:690]
- [x] [Review][Patch] Bind the OCI graph to its registry, immutable reference, and content-addressed evidence root [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:695]
- [x] [Review][Patch] Bind OCI provenance labels to the exact approved source revision [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:792]
- [x] [Review][Patch] Validate structured, support-safe runtime execution evidence instead of declared statuses [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:840]
- [x] [Review][Patch] Enforce the complete review-subject identity, limitation, blocker, and binding contract [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:949]
- [x] [Review][Patch] Exercise both prohibited cross-lineage splices through the closure evaluator [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:527]
- [x] [Review][Patch] Verify the core and predecessor checksum manifests inside the derived closure gate [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:545]
- [x] [Review][Patch] Retain and validate child-manifest and config response metadata [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/registry-readback.json:21]
- [x] [Review][Patch] Correct the impossible review-subject and registry-evidence chronology [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json:3]
- [x] [Review][Patch] Reject symlink-based evidence paths that escape the allowed root [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:615]
- [x] [Review][Patch] Record the claimed Markdown and Git diff hygiene command results [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:533]

**Acceptance Criteria:**
- Given completed predecessors, when closure begins, then committed identities are hash-checked without modification or inference.
- Given a candidate, when verified, then every field belongs to one lineage and every package/platform/digest/runtime relation has independent evidence.
- Given missing, unavailable, expired, mutable-only, or inconsistent evidence, when evaluated, then the verdict is `fail-closed`, names a blocker/reopen trigger, and changes no external or predecessor state.
- Given a complete passing packet, when the EventStore owner, Release owner, and Test Architect accept the same content-bound subject, then—and only then—Story 3.13 may be `done` without authorizing any external mutation or migration.

## Spec Change Log

- 2026-08-04: Applied all 15 code-review patches; kept the story `in-progress` because AC2 and AC4
  still require externally supplied evidence and acceptance.
- 2026-08-04: Applied the second review-hardening pass without changing frozen intent. The
  fail-closed subject, Git-object predecessors, exact package directory, release/authority lineage,
  OCI reports/provenance, runtime bounds, support-safety rules, roster, and durable receipts now
  have independent mutation coverage. AC1 and AC3 pass; AC2 and AC4 remain fail-closed with 0/3
  acceptances, so the story stays non-`done`.

## Verification

**Commands:**
- `(cd _bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594 && sha256sum -c critical-evidence-sha256.txt)` -- expected: all 33 listed predecessor files pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings/errors.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --filter FullyQualifiedName~DeployedRuntimeParityClosureTests` -- expected: all focused tests pass.
- `npx markdownlint-cli2 docs/ci.md && git diff --check` -- expected: documentation and diff checks pass.

## Suggested Review Order

**Decision and identity chain**

- Start with the fail-closed decision and precise closure boundary.
  [`proof-packet.md:5`](3-13-deployed-runtime-parity-closure-proof-packet.md#L5)

- Inspect derived checks and blockers before trusting any declared verdict.
  [`identity-crosswalk.json:431`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json#L431)

- Confirm the fail-closed subject content-binds decision, evidence, blockers, and limitations.
  [`review-subject.json:1`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json#L1)

**Derived verifier boundaries**

- Follow the single entry point that derives pass or fail from raw evidence.
  [`DeployedRuntimeParityClosureTests.cs:1496`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L1496)

- Review canonical lineage material before individual release and authority gates.
  [`DeployedRuntimeParityClosureTests.cs:3558`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3558)

- Bind semantic-release provenance to one exact retained release event.
  [`DeployedRuntimeParityClosureTests.cs:1709`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L1709)

- Enforce durable, scoped authority with explicit chronology and canonical lineage.
  [`DeployedRuntimeParityClosureTests.cs:1836`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L1836)

**OCI, runtime, and acceptance evidence**

- Derive the exact registry graph from retained raw responses and descriptors.
  [`DeployedRuntimeParityClosureTests.cs:2000`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L2000)

- Bind bounded Production runtime facts to the selected platform children.
  [`DeployedRuntimeParityClosureTests.cs:2311`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L2311)

- Require exact, durable, subject-addressed receipts without self-authentication.
  [`DeployedRuntimeParityClosureTests.cs:2498`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L2498)

**Mutation-proof verification**

- Prove a complete synthetic lineage passes before targeted mutations reject.
  [`DeployedRuntimeParityClosureTests.cs:452`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L452)

- Exercise release provenance and canonical-lineage mutations with refreshed bindings.
  [`DeployedRuntimeParityClosureTests.cs:955`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L955)

- Corrupt registry endpoints, statuses, references, and raw response bindings independently.
  [`DeployedRuntimeParityClosureTests.cs:1083`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L1083)

- Mutate runtime execution, bounds, tool identity, and preflight ordering independently.
  [`DeployedRuntimeParityClosureTests.cs:1231`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L1231)

- Reject receipt schema, decision, limitation, roster, and durable-source tampering.
  [`DeployedRuntimeParityClosureTests.cs:1351`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L1351)

**Integrity and lifecycle**

- Reproduce the acyclic core manifest before trusting derived conclusions.
  [`evidence-core-sha256.txt:1`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/evidence-core-sha256.txt#L1)

- Verify the outer layer anchors crosswalk and review-subject bytes without cycles.
  [`evidence-sha256.txt:1`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/evidence-sha256.txt#L1)

- Review truthful open tasks and why AC2 and AC4 remain non-done.
  [`3-13-deployed-runtime-parity-closure.md:191`](3-13-deployed-runtime-parity-closure.md#L191)

- Confirm sprint tracking preserves the external blockers during review.
  [`sprint-status.yaml:218`](sprint-status.yaml#L218)
