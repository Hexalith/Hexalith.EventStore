---
title: 'Story 3.13 Deployed Runtime Parity Closure'
type: 'chore'
created: '2026-08-04'
status: 'done'
baseline_commit: '98a2c9c772daea99bf8fc68f6d9bff84fd5df956'
review_loop_iteration: 0
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

**Acceptance Criteria:**
- Given completed predecessors, when closure begins, then committed identities are hash-checked without modification or inference.
- Given a candidate, when verified, then every field belongs to one lineage and every package/platform/digest/runtime relation has independent evidence.
- Given missing, unavailable, expired, mutable-only, or inconsistent evidence, when evaluated, then the verdict is `fail-closed`, names a blocker/reopen trigger, and changes no external or predecessor state.
- Given a complete passing packet, when the EventStore owner, Release owner, and Test Architect accept the same content-bound subject, then—and only then—Story 3.13 may be `done` without authorizing any external mutation or migration.

## Spec Change Log

## Verification

**Commands:**
- `(cd _bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594 && sha256sum -c critical-evidence-sha256.txt)` -- expected: all 33 listed predecessor files pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.DeployedRuntimeParityClosureTests` -- expected: all focused tests pass.
- `npx markdownlint-cli2 docs/ci.md && git diff --check` -- expected: documentation and diff checks pass.

## Suggested Review Order

**Decision and identity chain**

- Start with the fail-closed decision and precise closure boundary.
  [`proof-packet.md:5`](3-13-deployed-runtime-parity-closure-proof-packet.md#L5)

- Inspect the single-lineage check matrix and seven owned blockers.
  [`identity-crosswalk.json:426`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json#L426)

- Confirm approvals bind immutable hashes without entering the evidence hash cycle.
  [`review-subject.json:2`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json#L2)

**Independent evidence**

- Verify package recovery fails closed without rebuilding unavailable proof archives.
  [`package-availability.json:32`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/package-availability.json#L32)

- Trace registry bytes through exact index, child, and config descriptors.
  [`oci-validation.json:2`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/oci-validation.json#L2)

- Separate passing Development execution from failed Production-contract equivalence.
  [`runtime-verification.json:6`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/runtime-verification.json#L6)

- Reproduce the acyclic core evidence manifest before trusting derived conclusions.
  [`evidence-core-sha256.txt:1`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/evidence-core-sha256.txt#L1)

**Governance and lifecycle**

- Review truthful open tasks and why the story remains non-done.
  [`story-3-13.md:191`](3-13-deployed-runtime-parity-closure.md#L191)

- Check the sole operational-doc correction assigning deployed-runtime closure ownership.
  [`ci.md:253`](../../docs/ci.md#L253)

- Verify regenerated context preserves live lanes and ecosystem migration responsibility.
  [`epic-3-context.md:27`](epic-3-context.md#L27)

- Confirm shared CI flexibility remains distinct from immutable release execution pinning.
  [`epic-3-context.md:41`](epic-3-context.md#L41)

- Confirm sprint tracking advances only to review with blockers retained.
  [`sprint-status.yaml:214`](sprint-status.yaml#L214)

**Regression coverage**

- Read the derived closure evaluator and declarative-tampering controls first.
  [`DeployedRuntimeParityClosureTests.cs:284`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L284)

- Review raw OCI response and descriptor content-binding checks.
  [`DeployedRuntimeParityClosureTests.cs:218`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L218)

- Inspect acceptance uniqueness and prohibited cross-lineage splice rejection.
  [`DeployedRuntimeParityClosureTests.cs:477`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L477)
