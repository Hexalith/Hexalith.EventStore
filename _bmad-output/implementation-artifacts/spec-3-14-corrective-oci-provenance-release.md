---
title: 'Story 3.14 Corrective OCI Provenance Release'
type: 'bugfix'
created: '2026-08-20'
status: 'in-review'
baseline_commit: 'c21bd749154d701c3b7d68e40d1008d3475e35c4'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Latest release `v3.95.0` still inherits the .NET SDK multi-RID defect that truncates URL labels to `https` and omits source revision. Neither it nor immutable `v3.94.1` is deployment-grade.

**Approach:** Correct EventStore label inputs and the shared Builds publisher/validator, then produce one separately authorized release from the latest exact green `main`. Derive its version at authorization; never hard-code the current `3.96.0` projection.

## Boundaries & Constraints

**Always:** Preserve `v3.94.1`/`v3.95.0` as immutable failed evidence; keep EventStore a thin caller and shared mechanics in Builds; publish exactly 14 manifest packages and one two-platform `eventstore` index; bind retained raw bytes and both child smokes to one canonical version/source/run/Builds/authority lineage.

**Ask First:** Any Git mutation, release dispatch/approval, external write, credential use, or authority creation/reservation/consumption. Spec approval permits only named EventStore and Builds file edits.

**Never:** Rewrite an existing release; trust mutable names, ancestry, labels alone, copied pass flags, or projected `3.96.0`; fabricate authority/receipts; add Dockerfiles, runtime/dependency/inventory changes, deployment, consumer migration, signing, SBOM, or attestation.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Real multi-RID archive | Source SHA and version | Both configs contain identical exact source, release, SHA-pinned docs, revision, and version labels | Any invalid/divergent label fails |
| Candidate selection | Live release/package/registry destinations | Resolve an absent version newer than all | Ambiguity, stale read, or collision blocks |
| Authorized publication | Unexpired one-use GitHub authority for run/attempt and scope | All writes match one reservation, consumed once | Missing, expired, replayed, wrong-role, or mismatch blocks |
| Partial publication | Any write succeeds before a later failure | Preserve the partial version as immutable non-authorizing evidence | Retry requires a new version and new authority |
| Complete publication | Packages, OCI bytes, and both smokes pass | Emit canonical evidence for 3.15 without selecting a deployed identity | Environment/product failures remain distinct and blocking |

</frozen-after-approval>

## Code Map

- `Directory.Build.targets:21` -- bind five provenance labels to exact publisher inputs without colon truncation.
- `references/Hexalith.Builds/Github/publish-containers/{publish-containers.sh,oci_registry_validator.py,publication_preflight.py}` -- safely forward labels, validate raw configs, and enforce one-use GitHub authority before writes.
- `references/Hexalith.Builds/Github/publish-containers/tests/` -- real publisher, authority replay/mismatch, and label mutation fixtures.
- `.github/workflows/release.yml:79` -- keep the thin caller and rotate reusable workflow/action identity together to one reviewed corrected Builds SHA.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/{ContainerPublishingGovernanceTests.cs,CorrectiveOciProvenanceReleaseTests.cs}` -- pin the caller and run real archive/evidence cases without changing Story 3.13 fixtures.
- `tools/release-packages.json` and `tools/{release_evidence_codec.py,validate-corrective-release-evidence.py}` -- 14-package authority and canonical identity codec/verifier.
- `docs/ci.md:227` and Story 3.14 artifacts -- document gates, authority lifecycle, evidence, and 3.15 handoff.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs` -- reproduce the real multi-RID `https`/missing-revision archive defect before corrections.
- [x] `Directory.Build.targets` and `references/Hexalith.Builds/Github/publish-containers/` -- correct label transport, raw-config validation, and one-use authority with mutation tests.
- [x] `tools/release_evidence_codec.py`, `tools/validate-corrective-release-evidence.py`, `.github/workflows/release.yml`, governance tests, and `docs/ci.md` -- add canonical evidence, rotate one Builds identity, and document operation.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/` and `references/Hexalith.Builds/Github/publish-containers/tests/` -- run the named package-mode, publisher, archive, package, and smoke checks without writes.
- [x] `_bmad-output/implementation-artifacts/evidence/story-3-14/` and `_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md` -- after separate authority, resolve/publish once and retain all bytes; quarantine any partial identity.

**Acceptance Criteria:**
- Given all matrix scenarios, when focused suites run, then every row executes with mutation-proven, zero-skipped coverage.
- Given an authorized release, when evidence is reverified, then one canonical identity binds repository, version/tag, source, workflow, corrected Builds/helpers, authority, all packages, OCI bytes/labels, and both smokes.
- Given the 3.14 packet, when handed to 3.15, then it selects no deployed identity or mutation authority.

## Spec Change Log

## Design Notes

Resolve from authorized green `main`; source/destination change invalidates the `3.96.0` projection. Bind authenticated GitHub authority to run/attempt, helper hashes, scope, expiry, and one-use consumption.

## Verification

**Commands:**
- `pwsh -NoProfile -File references/Hexalith.Builds/Tools/test-publish-containers.ps1` -- expected: all shared fixtures pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: 0 warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectiveOciProvenanceReleaseTests` -- expected: all matrix cases pass, none skipped.
- `python3 tools/pack-release-packages.py /tmp/eventstore-3-14-packages 0.0.0-ci-test && python3 tools/validate-release-packages.py /tmp/eventstore-3-14-packages 0.0.0-ci-test` -- expected: exactly 14 valid packages.
- `bash -n scripts/validate-publication-preflight.sh && bash -n references/Hexalith.Builds/Github/publish-containers/publish-containers.sh && git diff --check` -- expected: syntax and hygiene pass in both owning repositories.
