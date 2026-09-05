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

**Problem:** Story 3.14's corrective release `v3.96.2` and its frozen evidence packet are complete, but review left an EventStore-owned deferred backlog (test vacuity, codec hygiene, authority-snapshot completeness, docs/process) and a stale `sprint-status.yaml` still showing `3-14-corrective-oci-provenance-release: in-progress` while the original spec is `done`.

**Approach:** Deliver one scoped post-release hardening slice for the corrective-release evidence and governance surface without mutating the frozen `v3.96.2` packet, without a new release dispatch, and without Builds-owned workflow splits unless separately authorized.

## Boundaries & Constraints

**Always:** Keep `_bmad-output/implementation-artifacts/evidence/story-3-14/**` byte-immutable; keep live handler/pin updates coordinated so the frozen packet still validates; preserve Story 3.15 closure pins that bind handler digests; prefer real skips over early-return vacuous passes; document any live-codec change that does not rewrite retained `successful/tools/` bytes.

**Ask First:** Any release dispatch, authority reservation/consumption, Builds remote mutation, frozen-packet rewrite, or submodule pin rotation that changes executed release bytes.

**Never:** Rewrite `v3.96.2` or any retained packet byte; trust mutable tags or copied pass flags; implement Builds-owned DW-364 inside EventStore; reopen Story 3.13 disposition; claim positive FR36 / Story 3.15 closure from this follow-up; fabricate authority or receipts.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Windows governance path | Host is Windows | Named cases use explicit skip (or equivalent non-pass) rather than early `return` vacuous pass | Suite must not report those cases as passed-by-construction |
| Live codec edit | Change under `tools/release_evidence_handlers/` or dispatcher | Frozen packet still validates at identity `4d1a0c33…`; live pins updated together when required | Pin/hash mismatch fails closed with support-safe diagnostic |
| Authority snapshot truncation | Incomplete comment page retained | Validator rejects "exactly one authority and one receipt" claims | Diagnostic names incompleteness, not a false exact-one pass |
| Out-of-scope Builds split | DW-364 only | No EventStore file change; remains deferred to Hexalith.Builds | N/A |

</frozen-after-approval>

## Open Questions

- SCOPE — options: A test/governance hygiene only (DW-356, DW-363, DW-372) (smallest EventStore slice; no codec semantics) / B codec+authority hardening (DW-357, DW-358, DW-360, DW-361, DW-362) (touches live `v3.py`/dispatcher; may require pin updates; must not rewrite frozen packet) / C docs+tracker close only (DW-373, DW-374 + set sprint-status 3-14 to `done`) (no production-path code) / D A+C together / E B+C together / F keep A+B+C in one spec (accept multi-cluster risk and likely >1600-token pressure)
- CODEC_LIVE_MODEL — options: live-only (edit current `tools/` handlers/pins; retained packet `successful/tools/` stay historical non-executable copies) / introduce documented v4 handler path (DW-373 becomes in-scope procedure + allowlist entry) / defer all codec edits (choose SCOPE A or C only)
- BUILDS_DW364 — options: leave deferred in Hexalith.Builds (EventStore remains thin caller) / open a separate Builds-owned change request outside this spec (still no EventStore implementation here)

## Code Map

- `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md` -- original story; `status: done`; frozen intent and closed review chunks 1–4. Do not reopen as the active implementation file.
- `_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md` -- outcome record for `v3.96.2` / source `f343bb…` / identity `4d1a0c33…`.
- `_bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/` -- **immutable** packet (`release-identity.json`, `packet-sha256.txt`, `successful/`, `quarantine/`). Never edit.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- DW-356…364, DW-372…374, DW-397 cite this story; DW-359/DW-396 already dispositioned.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:230` -- `3-14-corrective-oci-provenance-release: in-progress` lags the done spec.
- `tools/release_evidence_handlers/v3.py` -- live v3 handler: `EXPECTED_PACKAGE_COUNT`, `canonical_bytes`, `_publisher_canonical_bytes`, `validate_identity` / `validate_packet_files` / `_validate_authority`, index `children[0]` digest check, issue-comment exact-one claims. Codec hygiene cluster lands here if SCOPE includes B.
- `tools/release_evidence_codec.py` -- thin facade; do not reintroduce uncalled helpers.
- `tools/validate-corrective-release-evidence.py` -- isolated dispatcher; `HANDLERS` keyed by packet codec digest; live `HANDLER_FILE_SHA256` / `HANDLER_PACKAGE_FILE_SHA256` must stay coherent with `v3.py` / `__init__.py`.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- Windows early-returns ~407–867; Builds pin/gitlink ancestor check; heavyweight publish theories (DW-372).
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs` -- `CanonicalReleaseIdentityBindsRetainedBytesAndRejectsMutations` (~377) packs multi-mutation scenarios; `RetainedAuthorityRejectsInvalidWindowAndEditedRecord` (~735) couples window theory to frozen timestamps.
- `.github/workflows/release.yml` -- thin caller pinned to Builds `22a578b5…`; `require-publication-authority: false`. Do not rotate casually.
- `Directory.Build.targets` -- provenance rebind/validate; frozen packet still carries truncated `*.created` labels historically.
- `docs/ci.md` (~279–409) -- corrective gate docs and 3.14→3.15 handoff; update only if SCOPE includes docs.
- `references/Hexalith.Builds/.../domain-release.yml` -- DW-364 owner; out of EventStore mutation scope.

**Reuse:** Existing mutation/governance tests and isolated dispatcher model.
**Do not change:** Frozen packet bytes; Story 3.13 disposition trees; Story 3.15 closure subject/receipts unless a chosen codec pin update is proven not to burn them.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release-2.md` -- lock SCOPE / CODEC_LIVE_MODEL / BUILDS_DW364 answers into the frozen block before coding.
- [ ] Chosen EventStore files from Code Map -- implement only the selected deferred cluster(s); leave non-selected DW entries untouched in the ledger (append-only).
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/{ContainerPublishingGovernanceTests,CorrectiveOciProvenanceReleaseTests}.cs` -- cover every in-scope I/O matrix row with non-vacuous assertions.
- [ ] `tools/validate-corrective-release-evidence.py` (and focused Contracts packaging tests) -- re-verify frozen packet identity `4d1a0c33…` still passes after any live tool change.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- set `3-14-corrective-oci-provenance-release` to `done` only if SCOPE includes tracker close (C/D/E/F).

**Acceptance Criteria:**
- Given the frozen Story 3.14 packet, when the corrective-release validator runs after this follow-up, then identity `4d1a0c33…` still passes and no retained evidence byte changed.
- Given the selected deferred cluster(s), when focused packaging suites run, then every in-scope matrix row is covered without Windows vacuous passes for in-scope cases.
- Given Builds-owned DW-364, when this follow-up completes, then EventStore still does not implement the workflow split.
- Given sprint-status, when tracker close is in scope, then `3-14-corrective-oci-provenance-release` reads `done` and matches the original done spec.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `python3 tools/validate-corrective-release-evidence.py --packet _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: pass for identity `4d1a0c33…` (adjust flags to the script's actual CLI if different)
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --filter "FullyQualifiedName~CorrectiveOciProvenanceReleaseTests|FullyQualifiedName~ContainerPublishingGovernanceTests"` -- expected: 0 failed; in-scope cases not vacuous-pass on Windows
