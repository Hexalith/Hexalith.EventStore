---
title: 'Story 3.14 Follow-up — Corrective Release Hardening'
type: 'refactor'
created: '2026-09-05'
status: 'done'
route: 'dispatch'
baseline_commit: 'b43d64f906665e2bf3015eb2d3f16b771598d352'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md'
  - '{project-root}/_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.14's corrective release `v3.96.2` and frozen evidence packet are complete, but governance tests still vacuous-pass on Windows, a few authority/mutation Facts are brittle or over-packed, heavyweight container-publish theories still tax the CI Contracts gate, docs/process leftovers remain, and `sprint-status.yaml` still shows `3-14-corrective-oci-provenance-release: in-progress` while the original spec is `done`.

**Approach:** Ship SCOPE D — test/governance hygiene (DW-356, DW-363, DW-372) plus docs/tracker close (DW-373, DW-374, sprint-status → `done`). Defer all live codec edits. Leave Builds-owned DW-364 deferred in Hexalith.Builds.

## Boundaries & Constraints

**Always:** Keep `_bmad-output/implementation-artifacts/evidence/story-3-14/**` byte-immutable; leave live `tools/release_evidence_handlers/**` and corrective-release dispatcher pins unchanged; prefer real skips over early-return vacuous passes; keep EventStore a thin release caller.

**Ask First:** Any release dispatch, authority reservation/consumption, Builds remote mutation, frozen-packet rewrite, submodule pin rotation, or live codec/handler edit.

**Never:** Rewrite `v3.96.2` or any retained packet byte; edit live codec/handler/dispatcher bytes in this follow-up; implement Builds-owned DW-364 inside EventStore; reopen Story 3.13; claim Story 3.15 / FR36 closure; fabricate authority or receipts.

**Decisions (2026-09-05):**
- SCOPE = D (A+C): DW-356, DW-363, DW-372 + DW-373, DW-374 + set `3-14-corrective-oci-provenance-release` to `done`.
- CODEC_LIVE_MODEL = defer-codec (no live `v3.py` / dispatcher changes; DW-373 is documentation-only).
- BUILDS_DW364 = leave-deferred in Hexalith.Builds.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Windows governance path | Host is Windows | Named governance cases use explicit skip (or equivalent non-pass), not early `return` vacuous pass | Suite must not report those cases as passed-by-construction |
| Authority window theory | Invalid window vs edited record | Distinct theories/assertions; window failure does not fall through to opaque summary-mismatch | Clear diagnostic naming the failed rule |
| Packed mutation Fact | Independent retained-byte mutations | Split into focused cases so the first failure does not hide the others | Each case fails independently |
| Heavyweight publish theories | CI Contracts gate | Theories marked/re-tiered so the fast Contracts lane is not paying full `dotnet publish` cycles by default | Slow lane or explicit opt-in remains available |
| Out-of-scope Builds split | DW-364 only | No EventStore file change; remains deferred to Hexalith.Builds | N/A |
| Tracker close | Original 3.14 spec already `done` | `sprint-status.yaml` key `3-14-corrective-oci-provenance-release` reads `done` | Do not reopen the original done spec |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` -- original story; `status: done`. Do not reopen as the active implementation file.
- `_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md` -- outcome record for `v3.96.2` / identity `4d1a0c33…`.
- `_bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/` -- **immutable** packet. Never edit.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- DW-356, 363, 372, 373, 374 in scope; DW-357–362, 364, 397 stay deferred; append-only ledger (DW-374: record stale-pin note rather than rewriting history).
- `_bmad-output/implementation-artifacts/sprint-status.yaml:230` -- set `3-14-corrective-oci-provenance-release` to `done`.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- Windows early-returns ~407–867 (DW-356); heavyweight publish theories (DW-372).
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs` -- `CanonicalReleaseIdentityBindsRetainedBytesAndRejectsMutations` (~377) and `RetainedAuthorityRejectsInvalidWindowAndEditedRecord` (~735) (DW-363).
- `docs/ci.md` (~279–409) -- document how a later corrective release adds a `v4` evidence handler without executing codec changes here (DW-373).
- `.github/workflows/release.yml` -- thin caller pinned to Builds `22a578b5…`; read-only in this follow-up.
- `tools/release_evidence_handlers/v3.py`, `tools/validate-corrective-release-evidence.py` -- **do not change**; freeze-verify only.

**Reuse:** Existing packaging governance / corrective-release test harnesses.
**Do not change:** Frozen packet; live codec/handler/dispatcher; Builds reusable workflow; Story 3.15 receipts/subjects.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- replace in-scope Windows early-return vacuous passes with real skips (DW-356); mark/re-tier heavyweight container-publish theories out of the default CI Contracts gate (DW-372).
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs` -- decouple authority-window theory from frozen timestamps and split the packed retained-byte mutation Fact (DW-363).
- [x] `docs/ci.md` -- document the v4 evidence-handler succession procedure for a future corrective packet without changing live handlers (DW-373).
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- append a DW-374 clarification that the ledger's historical Builds pin text is stale relative to the current release pin (do not rewrite prior entries).
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- set `3-14-corrective-oci-provenance-release` to `done`.
- [x] Frozen packet + focused packaging tests -- re-verify identity `4d1a0c33…` still passes and live tools are untouched.

**Acceptance Criteria:**
- Given Windows hosts, when in-scope governance cases run, then they skip (or otherwise non-pass) instead of vacuous-pass via early `return`.
- Given authority-window and retained-byte mutation coverage, when a single scenario fails, then other independent scenarios still execute as separate cases with distinct diagnostics.
- Given the CI Contracts gate, when the default lane runs, then heavyweight full-publish theories are not required payment for that lane.
- Given the frozen Story 3.14 packet and unchanged live tools, when the corrective-release validator runs, then identity `4d1a0c33…` still passes.
- Given tracker close, when sprint-status is read, then `3-14-corrective-oci-provenance-release` is `done`.
- Given DW-364, when this follow-up completes, then EventStore still does not implement the Builds workflow split.

## Implementation Notes

- 2026-09-05 — SCOPE D landed without touching live codec/handler/dispatcher or the frozen Story 3.14 packet. Packet validator still passes `sha256:4d1a0c33…`.
- Heavyweight theories use `Category=HeavyweightContainerPublish`; Contracts CI excludes them via `--filter-not-trait`. Focused local verification uses `Category!=HeavyweightContainerPublish` inside `--filter` because MTP rejects combining `--filter` with `--filter-not-trait`.
- Reverted an accidental `epic-5` / `5-1` sprint-status flip back to `backlog` (out of scope for this follow-up).
- Added `PosixGovernanceCasesSkipOnWindowsInsteadOfVacuousEarlyReturn` so the Windows skip matrix row is covered on Linux hosts.
- Focused packaging suite (CorrectiveOci + ContainerPublishing + ReleasePackageManifest, excluding heavyweight): 210 passed / 0 failed / 0 skipped.

## Spec Change Log

- 2026-09-05 -- SCOPE D follow-up landed: Windows governance cases skip instead of vacuous-pass; authority-window and retained-byte mutations are split; heavyweight container-publish theories are trait-filtered out of the default Contracts CI lane; `docs/ci.md` documents v4 handler succession; DW-374 stale-pin note appended; sprint-status `3-14-corrective-oci-provenance-release` set to `done`. Live codec/handler/dispatcher and frozen packet untouched.

## Review Triage Log

- blind: mis-indented column-0 method closers / trailing blanks in two Windows `else` methods — **low** — real cosmetic indentation at `ContainerPublishingGovernanceTests.cs` ~548 and ~934; brace matching still closes the method, not the class. Route: **patch**.
- blind: unify all Windows guards to flat early-skip and delete `else` — **false** — the two `SetUnixFileMode` methods need `else` so CA1416 keeps platform-gated APIs unreachable on Windows; early `Assert.Skip` alone left those APIs in reachable IL and failed the analyzer. Evidence: implementer note and successful Release build with the `else` shape.
- blind: `PosixGovernanceCasesSkipOnWindows` only counts Skip windows and uses `>= 7` — **medium** — a new `IsWindows()` + `return;` would not fail. Route: **patch** (assert every `IsWindows()` window uses `Assert.Skip` and forbids `return;`, pin exact count).
- blind + verification-gap: Windows skip path never executes on Linux CI — **medium** (unverified on Windows runners) — Contracts CI is Linux-only; source guard is the feasible binder. Route: **defer**.
- blind + verification-gap: heavyweight filter removes fail-closed provenance from Contracts gate — **high** — `ContainerPublicationRejectsMalformedProvenanceInputs` is msbuild-only (`ValidateContainerProvenanceInputs`) yet was trait-filtered; only remaining in-gate contact is success-path `ContainerPublicationDefaultsTagToProvenanceVersion`. Route: **patch** (drop trait from malformed theory; keep trait on real `PublishContainer` cases).
- blind: no advisory/nightly job runs remaining heavyweight PublishContainer tests — **low** — matrix allows slow/opt-in; documenting the filter/opt-in command is enough without a new workflow. Route: **patch** (docs note only).
- blind: `docs/ci.md` omits Heavyweight trait / MTP filter constraint — **low** — developers will hit the MTP `--filter` + `--filter-not-trait` conflict. Route: **patch** (same docs note).
- blind: `DocumentationAndContainerDefaults` does not bind v4 succession markers — **medium** — DW-373 prose can vanish unnoticed. Route: **patch**.
- blind: `ReleasePackageManifestTests` does not assert method traits still match the CI filter — **medium** — filter string and traits can drift. Route: **patch**.
- blind: spec Verification stubs / empty triage / status — **false** — fix would be editing this build's spec; rejected per triage rules.
- blind: `CanonicalReleaseIdentityAcceptsAndRejectsRepositoryScopedRoleEvidence` still packs accept+reject — **medium** — DW-363 split incomplete for that Fact. Route: **patch**.
- blind: duplicate `updated_at = created_at+1s` cases — **low** — rejected; one is identity mutation coverage, one is retained-authority coverage; everyday harm unlikely and collapsing adds coupling.
- blind: extract shared packet-copy helper — **low** — rejected; fix adds complexity beyond a direct correction.
- edge-case: residual vacuous `return;` undetected by weak source scan — **medium** — same root as PosixGovernance tighten. Grouped with that **patch**.
- edge-case: tasks/Code Map attribute DW-372 to `ContainerPublishingGovernanceTests` while traits live on `CorrectiveOciProvenanceReleaseTests` — **false** — code placement is correct for the heavyweight theories; fixing attribution would edit this build's spec (rejected).
- verification-gap Other: `ContainerPublicationDefaultsTagToProvenanceVersion` weak sole in-gate Validate contact — **medium** — addressed by restoring malformed theory to the gate (same high patch).

## Verification

**Commands:**
- `python3 tools/validate-corrective-release-evidence.py` (packet path per script CLI) against `_bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: pass for identity `4d1a0c33…`; live tools unchanged
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --filter "FullyQualifiedName~CorrectiveOciProvenanceReleaseTests|FullyQualifiedName~ContainerPublishingGovernanceTests"` -- expected: 0 failed; in-scope Windows cases not vacuous-pass
- `rg -n "3-14-corrective-oci-provenance-release:" _bmad-output/implementation-artifacts/sprint-status.yaml` -- expected: value `done`
