---
title: 'Reseal Story 4.15 after PublicationRecovery bound-source drift'
type: 'bugfix'
created: '2026-08-11'
status: 'done'
baseline_commit: '06e62b4dceae6df0587062968097b46caa51713a'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** [Integration Tests run 31483075631](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31483075631) passes live OQ8 and support cases, then fails committed validation with bound-source drift on `PublicationRecoveryActivationTests.cs` after commit `4b0a7b1d` changed that capability path past Story 4.15 land `e5fef514…`.

**Approach:** Reseal Story 4.15 onto landed source `4b0a7b1d3628a857f131cfbff99030714aefc747` (tree `21f98190…`), expand capture→landed Git-byte overrides, obtain fresh content-bound architecture/security/test receipts, and leave Story 4.14 immutable. No new live-sidecar capture is required for committed validation.

## Boundaries & Constraints

**Always:** Keep Story 4.14 bytes and committed Dapr runtime `1.18.1` unchanged. Keep all 24 non-evolved capability paths byte-stable at the new landed commit through the reseal commit. Bind approvals to one frozen subject. Record real named receipts before final validation. Use a `fix/…` branch before any commit.

**Ask First:** OQ8 design/admission/profile/public-contract changes; release/package/registry/deploy/pin/consumer authority; submodule changes; fabricating or skipping reviews; committing or pushing from `main`.

**Never:** Mutate `evidence/story-4-14/**`; fabricate receipts; weaken committed validation or ancestry; claim release/Folders/package/deploy authority; modify any of the 24 bound paths in the reseal commit; require a fresh live capture solely for this drift.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Committed after reseal | HEAD descends from `4b0a7b1d…`; 24 bound paths match; receipts bind subject | Committed validator succeeds | Fail closed on path or receipt drift |
| Capture vs new landed | 4.14 capture hashes unchanged; landed differs for recovery tests and evolved tooling | Overrides bridge capture→landed Git | Missing override → identity drift |
| Fresh CI lane | Runner capture + runtime `1.18.2` | Fresh stays independent of committed `1.18.1` | Do not relabel committed as fresh |
| Pre-review gap | Identity refreshed without three receipts | `--pre-review` may pass; final mode rejects | HALT until real receipts exist |

</frozen-after-approval>

## Code Map

- `tools/validate-oq8-platform-evidence.py:39-46,52-87,110-178,1199-1249,1914-2003` -- landed/closure pins, 24 bound paths, roster/limitations/docs hashes, hard-coded overrides, HEAD≡landed proof, committed/fresh/`--pre-review` modes.
- `evidence/story-4-14/e60a3777…/` -- immutable capture; `captureWorktreePaths` stay capture authority.
- `evidence/story-4-15/e5fef514…/` → move to `…/4b0a7b1d3628a857f131cfbff99030714aefc747/`; refresh identity, limitations, subject, pre-review, validator hash, reviews, handoff, closure manifest.
- `Oq8PlatformClosureTests.cs:14,480,931` -- retarget `LandedSource` / closure path literals.
- `4-8-eventstore-oq8-platform-evidence.yaml` -- v2 packet landed commit + closure directory digests.
- OQ8 public docs under `docs/{concepts,reference,guides}/` -- embed new landed SHA; refresh `EXPECTED_DOCUMENT_HASHES`.
- `integration.yml` / `docs/ci.md` -- read-only fresh-then-committed lane; no workflow change for this drift.
- Override landed Git SHA-256 (capture stays): recovery tests → `bfc4c851…b15b6d`; `integration.yml` → `a9fe1bcd…fccb91`; validator → `5c23fe25…572647`; keep existing `Program.cs` CRLF/LF override. Compute full digests from `git show 4b0a7b1d:<path>` during implementation.

## Tasks & Acceptance

**Execution:**
- [x] `tools/validate-oq8-platform-evidence.py` -- retarget landed commit/tree/closure, limitation text, and expand `landedGitByteOverrides`.
- [x] `evidence/story-4-15/**` -- move closure dir to new landed SHA; rewrite identity/subject/pre-review/validator hash; do not touch Story 4.14.
- [x] `reviews/{architecture,security,test}.json` -- after `--pre-review`, record real content-bound approvals for the new subject.
- [x] Packet YAML, `Oq8PlatformClosureTests.cs`, OQ8 docs -- finalize handoff/manifests/pins/doc hashes; reseal commit must not touch the 24 bound paths.

**Acceptance Criteria:**
- Given HEAD descends from `4b0a7b1d…` with bound paths unchanged since that commit, when committed validation runs, then it passes without a new live-sidecar capture.
- Given Story 4.14 artifacts after the change, when inspected, then capture bytes and committed runtime `1.18.1` are unchanged.
- Given the refreshed subject, when the three rostered receipts are present, then final validation and `Oq8PlatformClosureTests` pass; without them only `--pre-review` is allowed.

## Spec Change Log

## Design Notes

Land on `4b0a7b1d…` (first commit with the drifted recovery tests). Later `main` only evolved unbound tooling or unbound production code, so HEAD already matches all 24 bound paths at that pin. Capture hashes remain Story 4.14 truths; overrides express intentional Git-byte divergence. Reviews must rebind because subject/validator/document digests change.

## Verification

**Commands:**
- `python3 -m py_compile tools/validate-oq8-platform-evidence.py && python3 tools/validate-oq8-platform-evidence.py --pre-review` -- expected: pre-review identity passes.
- `python3 tools/validate-oq8-platform-evidence.py` -- expected: full committed closure only after real receipts.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` then built assembly filtered to `Oq8PlatformClosureTests` -- expected: all green.
- `git merge-base --is-ancestor 4b0a7b1d3628a857f131cfbff99030714aefc747 HEAD && git diff --check` -- expected: ancestry holds; whitespace clean; Story 4.14 and 24 bound paths untouched by reseal commit.

**Manual checks:**
- Confirm receipts use rostered reviewers, bind new subject/limitations digests, and keep every external-authority field false.

## Suggested Review Order

**Landed pin and overrides**

- Retarget Story 4.15 onto the PublicationRecovery capability commit.
  [`validate-oq8-platform-evidence.py:39`](../../tools/validate-oq8-platform-evidence.py#L39)

- Expand capture→landed Git-byte overrides for the drifted recovery tests.
  [`validate-oq8-platform-evidence.py:1198`](../../tools/validate-oq8-platform-evidence.py#L1198)

- Persist the same landed commit, tree, and override map in sealed identity.
  [`source-artifact-identity.json:3`](evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/source-artifact-identity.json#L3)

**Receipts and handoff**

- Confirm architecture/security/test receipts bind the refreshed subject only.
  [`architecture.json:1`](evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/reviews/architecture.json#L1)

- Check outer packet digests and false external-authority fields.
  [`4-8-eventstore-oq8-platform-evidence.yaml:36`](4-8-eventstore-oq8-platform-evidence.yaml#L36)

**Pins and docs**

- Retarget closure test landed-source and missing-path literals.
  [`Oq8PlatformClosureTests.cs:14`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L14)

- Embed the new landed SHA in public OQ8 handoff wording.
  [`architecture-overview.md:279`](../../docs/concepts/architecture-overview.md#L279)
