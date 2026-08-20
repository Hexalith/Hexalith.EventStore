---
title: 'Frozen Story 1.20 Evidence Integrity Repair'
type: 'bugfix'
created: '2026-08-20'
status: 'done'
baseline_commit: '1e5abd261339c831347b4717f5d311a214f97059'
review_loop_iteration: 0
story_id: '1.21'
story_key: '1-21-frozen-story-1-20-evidence-integrity-repair'
evidence_owner: 'Administrator'
test_architect: 'bmad:murat'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Commit `089369bb8fa34117c1d5f912f5cbe80ab07fa9a3` changed one captured workload-version line in three frozen Story 1.20 evidence trees, so each 33-entry critical manifest now fails despite Story 1.20 and Epic 1 remaining complete.

**Approach:** Bind an exact repair subject to the introducing commit, its sole parent `f670892f0826de2097e9f47175f5caf5c5ad346a`, and the three parent blobs; after this spec receives `[A] Approve`, restore only those bytes, add a future-drift guardrail, and obtain content-bound verification from `bmad:murat`.

## Boundaries & Constraints

**Always:** Treat `[A] Approve` from `Administrator` as the separate EventStore evidence-owner authorization for only this frozen repair subject. Under `_bmad-output/implementation-artifacts/evidence/story-1-20/`, the repair diff is exactly these three `100644` preimage-to-parent-blob transitions: `38f85086fc2513e06fe85482dfade96578d649e5/environment.txt`, `e4bfbbf98ea8d3faa91ac8b1bcd0a4be13fa2b77` to `a9fccff513e0c86813ed1edd129df5fc31355ef2`; `4983299103bfa5bbbd40e695767eb5ddbc1369d5/environment.txt`, `b1f2274b6f84ce754ac53b62b0a0a94a6ee4c408` to `53122faef296332df79199d2886e45215f84a720`; and `ec0d35a082bcc70b090afa1c1544306008d767da/environment.txt`, `878ac4b6bb0fa70b5a748666d84070515eae585f` to `c48af936a2893f0305e9330ec1197c161e841c84`. Keep supporting spec, subject, test, verification, ledger, and tracker artifacts distinct from that repair diff. Preserve unrelated Story 3.14 work.

**Ask First:** Halt before predecessor writes if approval is absent, Git identities or modes differ, any affected path has user changes, the repair would exceed the exact three paths, or the authority/verification contract must materially change.

**Never:** Do not regenerate manifests, normalize captures to the current SDK, modify the already-restored `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` tree, reconstruct missing packages, touch source/release/registry/runtime/consumer/submodule state, or alter Story 1.20's decision, approved identities, consumer authorization, or Epic 1 status.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Authorized repair | Exact commit, parent, paths, modes, and blobs match | Restore each file byte-for-byte from its parent blob; every critical manifest passes 33/33 | Halt on any mismatch before writes |
| Broader drift | Any other predecessor-evidence difference is detected | No repair executes | Report the unexpected path or identity |
| Missing proof packages | Each 14-entry `nuget-sha256.txt` references absent archives | Report `0/14 available` separately from content integrity | Never relabel, rebuild, restore, or infer packages |
| Verification mismatch | Test Architect result does not bind the authorized subject and result packet | Story remains `in-review` | Do not close ledger or tracker |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/evidence/story-1-20/{38f85086...,4983299103...,ec0d35a0...}/environment.txt` -- exact three-file repair surface; each current line 6 says `10.0.302-manifests.1641d827`, while the pinned parent blob says `10.0.300-manifests.1641d827`.
- The sibling `critical-evidence-sha256.txt` and `nuget-sha256.txt` files -- read-only integrity and availability authorities; expected environment SHA-256 values are `ae3a92f3...`, `a32d875c...`, and `9fef028e...`.
- `_bmad-output/implementation-artifacts/evidence/story-1-21/` -- canonical repair subject, its SHA-256 pin, post-repair result packet, and Test Architect verification; records must bind exact identities and explicit outcomes without self-asserted authority.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/FrozenStory120EvidenceIntegrityRepairTests.cs` -- focused exact-scope, blob/hash/mode, manifest, mutation, broader-path, and independent package-availability guardrail; reuse Git and checksum patterns from `DeployedRuntimeParityClosureTests.cs:4550`.
- `_bmad-output/implementation-artifacts/deferred-work.md:1265` and `sprint-status.yaml:78` -- close only after bound verification; leave Story 1.20 and `epic-1` unchanged.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-1-21/{repair-subject.json,repair-subject-sha256.txt}` -- materialize the canonical subject and validate approval, commit/parent, paths, modes, preimage/target blobs, and hashes before repair.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/FrozenStory120EvidenceIntegrityRepairTests.cs` -- add positive, mutation, broader-scope, and independent `0/14` package-availability guardrails.
- [x] `_bmad-output/implementation-artifacts/evidence/story-1-20/{38f85086fc2513e06fe85482dfade96578d649e5,4983299103bfa5bbbd40e695767eb5ddbc1369d5,ec0d35a082bcc70b090afa1c1544306008d767da}/environment.txt` -- restore only the authorized files from their pinned parent blobs.
- [x] `_bmad-output/implementation-artifacts/evidence/story-1-21/{repair-result.json,test-architect-verification.json}` -- bind the post-repair result to the subject and record `bmad:murat` verification.
- [x] `_bmad-output/implementation-artifacts/{deferred-work.md,sprint-status.yaml}` -- reconcile the HIGH item and Story 1.21 tracking only after every completion gate passes.

**Acceptance Criteria:**
- Given the approved subject, when repair executes, then the repair diff contains exactly the three pinned parent blobs and all three critical manifests pass 33/33.
- Given proof-package availability is checked, when all referenced archives are absent, then each tree reports `0/14 available` independently without changing its NuGet manifest or claiming corruption.
- Given completion is requested, when the subject-bound result and Test Architect verification pass, then Story 1.21 closes while Story 1.20, its identities/authorization, and Epic 1 remain unchanged.

## Spec Change Log

- 2026-08-20: Completed the exact three-file repair, added immutable subject/result and future-drift guardrails, recorded bound Test Architect verification, and closed Story 1.21 tracking.
- 2026-08-20: Hardened authorization and closure evidence with a separate owner receipt, frozen-block and checksum pins, exact scope/chronology/conclusion validation, live observation recomputation, ignored-drift rejection, timeout cleanup, and LF-stable bytes; the approved frozen block and predecessor repair remained unchanged.

## Design Notes

The planning shorthand `10.0.301 -> 10.0.302` is not reconstruction authority: the actual Git diff is `10.0.300-manifests.1641d827 -> 10.0.302-manifests.1641d827`. Git objects and manifest SHA-256 values win over prose. The immutable pre-repair subject and post-repair result are separate so owner authorization can precede the write while Test Architect verification binds the resulting bytes.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings/errors.
- `dotnet exec tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.FrozenStory120EvidenceIntegrityRepairTests -noLogo` -- expected: all focused cases pass with zero skips.
- `for d in 38f85086fc2513e06fe85482dfade96578d649e5 4983299103bfa5bbbd40e695767eb5ddbc1369d5 ec0d35a082bcc70b090afa1c1544306008d767da; do (cd "_bmad-output/implementation-artifacts/evidence/story-1-20/$d" && sha256sum -c critical-evidence-sha256.txt); done` -- expected: 33/33 per tree; run the same loop for `nuget-sha256.txt` separately and record exactly 14 missing archives per tree.
- `git diff --check` plus an exact allowlist comparison for the predecessor repair paths -- expected: no whitespace errors and no fourth predecessor-evidence path.

**Observed:** Release build completed with zero warnings and errors. The focused class passed 7/7 with zero skips. Each critical manifest passed 33/33; each proof-package set reported 0/14 available independently; tracked, untracked, and ignored broader Story 1.20 drift were zero; the protected `fa2d1c99...` tree remained unchanged. Authorization receipt SHA-256: `25a01f60f8f231babb3db860dc8a59d2d46264f6cefe6db7f461fa615316d732`. Subject SHA-256: `ee5fb076bac380faa0b01ccd7aa96ec9f77955faa96f45c34aafc75d7bc8d26e`. Result SHA-256: `e22e1aef2d24fea81d49ce5e9f495d4ff8d02e989da8b37c1937b517a477e3ab`. Test Architect verification SHA-256: `183975a600f4a2a66f07672a4eb1e62b6bd5af5139dd2f5ff5e917ee5477366d`.

## Suggested Review Order

**Repair contract and authority**

- Start with the immutable approved scope and explicit repair boundaries.
  [`spec-1-21-frozen-story-1-20-evidence-integrity-repair.md:18`](spec-1-21-frozen-story-1-20-evidence-integrity-repair.md#L18)

- Canonical subject pins history, modes, blob identities, and three-file scope.
  [`repair-subject.json:1`](evidence/story-1-21/repair-subject.json#L1)

- Durable owner receipt binds interactive approval to subject and frozen spec bytes.
  [`evidence-owner-authorization.json:1`](evidence/story-1-21/evidence-owner-authorization.json#L1)

**Repair outcome**

- Each restored capture now matches its introducing commit parent blob.
  [`38f85086…/environment.txt:6`](evidence/story-1-20/38f85086fc2513e06fe85482dfade96578d649e5/environment.txt#L6)

- Each restored capture now matches its introducing commit parent blob.
  [`49832991…/environment.txt:6`](evidence/story-1-20/4983299103bfa5bbbd40e695767eb5ddbc1369d5/environment.txt#L6)

- Each restored capture now matches its introducing commit parent blob.
  [`ec0d35a0…/environment.txt:6`](evidence/story-1-20/ec0d35a082bcc70b090afa1c1544306008d767da/environment.txt#L6)

- Result packet records live integrity, availability, drift, and protected-tree outcomes.
  [`repair-result.json:1`](evidence/story-1-21/repair-result.json#L1)

- Murat verification binds subject and result hashes to an explicit pass.
  [`test-architect-verification.json:1`](evidence/story-1-21/test-architect-verification.json#L1)

**Guardrails and closure**

- Guardrail rejects mutated scope, receipts, stale results, and broader drift.
  [`FrozenStory120EvidenceIntegrityRepairTests.cs:83`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/FrozenStory120EvidenceIntegrityRepairTests.cs#L83)

- Closure logic recomputes evidence and validates chronology before accepting verification.
  [`FrozenStory120EvidenceIntegrityRepairTests.cs:238`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/FrozenStory120EvidenceIntegrityRepairTests.cs#L238)

- LF attributes stabilize JSON and checksum evidence across platforms.
  [`.gitattributes:12`](../../.gitattributes#L12)

- Deferred HIGH risk closes only after bound repair verification.
  [`deferred-work.md:1268`](deferred-work.md#L1268)

- Sprint tracking closes Story 1.21 without changing Story 1.20 or Epic 1.
  [`sprint-status.yaml:80`](sprint-status.yaml#L80)
