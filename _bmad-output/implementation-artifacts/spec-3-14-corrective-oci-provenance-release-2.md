---
title: 'Story 3.14 Follow-up — Corrective Release Hardening'
type: 'refactor'
created: '2026-09-05'
status: 'draft'
route: 'dispatch'
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
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- replace in-scope Windows early-return vacuous passes with real skips (DW-356); mark/re-tier heavyweight container-publish theories out of the default CI Contracts gate (DW-372).
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs` -- decouple authority-window theory from frozen timestamps and split the packed retained-byte mutation Fact (DW-363).
- [ ] `docs/ci.md` -- document the v4 evidence-handler succession procedure for a future corrective packet without changing live handlers (DW-373).
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` -- append a DW-374 clarification that the ledger's historical Builds pin text is stale relative to the current release pin (do not rewrite prior entries).
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- set `3-14-corrective-oci-provenance-release` to `done`.
- [ ] Frozen packet + focused packaging tests -- re-verify identity `4d1a0c33…` still passes and live tools are untouched.

**Acceptance Criteria:**
- Given Windows hosts, when in-scope governance cases run, then they skip (or otherwise non-pass) instead of vacuous-pass via early `return`.
- Given authority-window and retained-byte mutation coverage, when a single scenario fails, then other independent scenarios still execute as separate cases with distinct diagnostics.
- Given the CI Contracts gate, when the default lane runs, then heavyweight full-publish theories are not required payment for that lane.
- Given the frozen Story 3.14 packet and unchanged live tools, when the corrective-release validator runs, then identity `4d1a0c33…` still passes.
- Given tracker close, when sprint-status is read, then `3-14-corrective-oci-provenance-release` is `done`.
- Given DW-364, when this follow-up completes, then EventStore still does not implement the Builds workflow split.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `python3 tools/validate-corrective-release-evidence.py` (packet path per script CLI) against `_bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: pass for identity `4d1a0c33…`; live tools unchanged
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --filter "FullyQualifiedName~CorrectiveOciProvenanceReleaseTests|FullyQualifiedName~ContainerPublishingGovernanceTests"` -- expected: 0 failed; in-scope Windows cases not vacuous-pass
- `rg -n "3-14-corrective-oci-provenance-release:" _bmad-output/implementation-artifacts/sprint-status.yaml` -- expected: value `done`
