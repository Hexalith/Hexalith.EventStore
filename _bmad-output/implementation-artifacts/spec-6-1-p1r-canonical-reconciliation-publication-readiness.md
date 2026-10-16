---
title: '6.1-P1R canonical reconciliation and publication readiness'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'dispatch'
baseline_commit: '0b38a562cd4fb6191a0c3c78a44b4c48d1f05390'
reconciled_canonical_source: 'a2016e88f4d0da9b953fe6f4394f8b8fd4e27f72'
review_loop_iteration: 0
context:
  - /home/administrator/projects/hexalith/projects/references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - /home/administrator/projects/hexalith/projects/references/Hexalith.AI.Tools/hexalith-git-instructions.md
---

<frozen-after-approval reason="explicit user authorization for separate isolated reconciliation and implementation">

## Intent

**Problem:** The completed source recovery and tooling hardening have not been reconciled with canonical main, and release disables one-use publication authority. Story 6.1 cannot rely on this source or any future successor.

**Approach:** Produce a separate reviewable canonical reconciliation and fail-closed publication workflow, with fresh exact-byte regression evidence and an explicit later landing/publication handoff. Complete this authorized source-only package without changing prerequisite acceptance.

## Boundaries & Constraints

**Always:** Implement only in isolated checkout `/home/administrator/projects/hexalith/p1r-canonical-reconciliation-20261016`, detached at the baseline above. Original owner is `/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore`; read its existing hardening, recovery and enrollment records as immutable inputs. New evidence goes only in original owner's `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/` (E). Preserve all canonical production/security changes, historical evidence and exact four dated helpers. Keep P1R unusable and Story 6.1 blocked; acceptance/sprint/pins unchanged. Retain failed verification attempts. Use synthetic authority only inside isolated tests with no external writes.

**Never:** Stage, commit, push, publish, dispatch workflows, create authority/approvals, update dependency pins, initialize nested submodules, move original HEAD/branches/gitlinks/indexes, overwrite original hardening or Projects draft, reserve/select 3.122.1 or any successor. No sprint key. The task explicitly authorizes implementation and independent review; workflow checkpoints add no new permission requirement.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior |
| --- | --- | --- |
| Reconciliation | Full 26-path manifest and canonical security overlap | All paths retained; generated fixture and canonical production/security preserved |
| Missing authority | Missing reservation/owner/issue or genuine matching comment | Refuse before package writes |
| Wrong authority | Wrong version/source/run/attempt/identity/owner | Refuse |
| Lifetime/replay | Expired authority or previously consumed nonce/identity | Refuse, including publish phase |
| Valid authority | Same reserved identity and real unexpired owner authority | Existing publisher consumes once before first write; production approval still required |
| Weak source proof | Bypass=true, stale main or missing/unsuccessful CI, P1R or Commitlint | Refuse |
| Historical version | Proposed 3.122.1 and unauthenticated registry | No reservation/selection; authentication failure is a blocker |

</frozen-after-approval>

## Code Map

- Original hardening `reviewed-source-hardening.json` and `delivery-manifest.json` bind 26 paths and evidence; parent validated every entry. E's `input-validation-and-reconciliation-plan.json` identifies per-path applicability against baseline. Enrollment base is `210b90da172cc7ca946fbb06cfd44390bffa707b`.
- `tools/tests/test_p1r_published_executor.py`: three-way merge generated uuid credential rendering with hardened fixture. Canonical `test_p1r_published_qualification.py` already has generated rendering and must remain canonical.
- `.github/workflows/release.yml`: explicit dispatch, production environment, publication-enable variable, pinned Bexec `22a578b576a515d2af214fe81859447fffc97981`, OIDC and source proof. Require all three successful push proofs and refuse bypass. Do not hardcode a reservation.
- `scripts/validate-publication-preflight.sh`, `.releaserc.json`: reuse existing version/issue/owner validation and verify/publish hooks. Publish hook already precedes first NuGet write.
- Approved Builds `Github/publish-containers/publication_preflight.py` at Bexec: reuse identity builder, genuine GitHub authority validation and live one-use consumption. Read objects via sibling original `../Hexalith.Builds` Git; do not initialize anything. Identity binds exact run/attempt and contract hashes. Expose candidate identity/hash read-only before production approval; publish must rederive it. No owner grant is produced.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/TrustedPublishingReleaseTests.cs`: update old authority=false/bypass expectations coherently. Canonical secret/evidence tests are in Server.Tests/Security.

## Tasks & Acceptance

**Execution:**

- [x] Isolated closure: apply all 26 reviewed paths with recorded three-way applicability, retain credential fixture changes, verify four exact helper hashes and three new Python files. No `src/` or pins change.
- [x] Release workflow/local helpers: implement mandatory authority wiring with actual future reservation/issue/owner inputs, candidate identity exposure, same-run identity verification, strict exact-source CI/P1R/Commitlint checks before publication. Preserve protected production, secrets/OIDC, provenance, destinations and publisher pins.
- [x] Regression controls: exercise actual workflow helper/preflight calls and approved publisher engine for valid authority and all matrix refusals; test consumption-before-write and replay, plus source-proof failures. Register new controls in preparation CI. No disconnected imitation validator.
- [x] Verification: run five P1R suites individually and optimized hardening controls, authority controls, canonical packaging and secret/evidence tests with narrow .NET fallback as necessary. Capture exact argv/results/source hashes in E; report environment blockers honestly.
- [x] E: retain reconciliation source manifest, complete diff including untracked delivered sources, independent review/triage, fresh release/version/destination observations, preservation and concrete remaining handoff. Parent performs observations and final preservation independently.

**Acceptance Criteria:**

- Given canonical baseline, when applying the delivered diff, then the complete closure and guarded workflow are reviewable without changing original work or production/security behavior.
- Given every matrix case, when exercising real helpers, then executed regression evidence proves the stated pass/refusal boundary and exact hashes remain stable.
- Given this source-only delivery, when handed off, then authorized landing/freeze/CI, real authority, guarded publication, observed selection/qualification and every downstream prerequisite remain explicit and unaccepted.

## Implementation Notes

User requested a separate package and explicitly authorized isolated reconciliation, implementation, verification and independent review. No intent gaps or external mutations are needed. Parent handles network observations and original-checkout preservation; implementation agent may focus on isolated reconciliation, workflow and tests. Read-only remote observations are authorized. Original source/evidence validation passed before implementation. Snapshot capture is in progress. No existing spec is resumed.


Completed the source-only package: 34 delivered paths include the complete 26-path closure. Final diff SHA-256 is `006a4bfee5d3e3e9a0eeef8f4a2f23eadf81a567bf99ca9541f5e5517b84b603`. Source hashes are stable for 6,518 inputs; all 266 Python tests, 36 optimized controls, 179 snapshot tests, 64 packaging tests, 778 canonical secret/evidence controls and 24 compiler evaluations pass. Fresh harmless real-Docker smoke passes with exact private/outer cleanup. Original 201,238 EventStore and 5,799 Projects files, HEADs, branches, indexes and workspace gitlinks are preserved. No commit/push/publication occurred.

The new bundle's actual canonical-reader audit found six retained-evidence ingestion flags. Source 34-path scanning passes. This remains a bulk-evidence landing blocker under an explicit authorized disposition/boundary; no exemption or retirement approval was created. Historical audit-entrypoint restrictions are recorded separately. Current canonical CI failed, and current source analysis predicts minor 3.123.0 while 3.122.1 remains historical. Neither version is reserved/selected. `done` applies only to this reconciliation and wiring package; P1R remains unusable, Story 6.1 blocked, acceptance and sprint state unchanged. See [handoff](evidence/6-1-p1r-canonical-reconciliation-20261016/publication-readiness-handoff.md).

## Spec Change Log

## Review Triage Log

| ID | Verdict | Evidence / disposition |
| --- | --- | --- |
| B1 | medium | Forged replay JSON reproduced; authenticated bot issuer check and nonblocking regression pass. |
| B2 | false | Exact interpreter refusal is intentional; supported preparation/validation uses the same isolated wrapper. |
| B3 | low | Pre-existing unsupported external Git metadata layout; ordinary self-contained checkout required. |
| B4 | low | Stale candidate attempt now explicitly refuses before download; rerun-all/new authority control passes. |
| B5 | low | Pre-existing earliest-allocation scratch-only failure; reject disproportionate partial-state cleanup redesign. |
| B6 | low | Handoff explicitly requires original absolute invocation/input/binary/receipt retention. |
| B7 | low | Immutable historical auditor separates integrity/errors; do not reuse as new operational qualification. |
| B8 | medium | Historical auditor lacks final source check after waits; retained unchanged and deferred to fresh operational audit. |
| B9 | low | Historical entrypoint assumes its governed proposal/layout; immutable helper stays a CI dependency. |
| B10 | low | Fresh same-source real-Docker smoke passed with both process and outer cleanup. |
| P1 | medium | Canonical scanner found literal endpoint fixtures; UUID generation fixes them without changing the scanner. |

All findings were triaged separately before grouping. Three context-free reviewers completed; blind reviewer independently rechecked direct fixes and found no new related defect. Full details and retained historical limitations are in [independent review](evidence/6-1-p1r-canonical-reconciliation-20261016/independent-review.md).

## Verification

Run individual P1R unittest discovery suites plus optimized hardening. Run new publication tests against exact Bexec objects. Build/run focused Contracts packaging and Server secret/evidence/snapshot classes in Debug/source mode using existing root-declared dependencies via explicit properties; record unavailable inputs without initializing nested dependencies. Parent repeats checks with source hashes before/after, both whitespace checks, Projects production-authority validation and preservation.

Independent review: three context-free reviewers completed. Edge reviewer returned no findings; verification reviewer found no gaps. The ten blind findings and parent scanner finding are individually triaged in [independent-review.md](evidence/6-1-p1r-canonical-reconciliation-20261016/independent-review.md). B1 (medium), B4 (low) and P1 (medium) route to direct patches. B2 is false for the documented wrapper-only profile. B3/B5 are low pre-existing layout/allocation limitations; B6 is a retention clarification; B7/B9 are low historical-entrypoint limitations and B8 is a medium historical-entrypoint race. Their exact required helper hashes remain immutable, and their main entrypoints are not permitted operational qualification for a new tuple. B10 receives a fresh live smoke attempt.

Canonical refresh: final delivery observed main advance to `a2016e88f4d0da9b953fe6f4394f8b8fd4e27f72`, a single canonical test-helper fix outside the delivery. Both isolated checkouts were refreshed; all 34 delivered source hashes and all review hunks are unchanged. The original baseline field remains historical. Fresh Server build, source bindings, relevant .NET controls, all Python suites, compiler profiles, version/destination observations and live smoke are repeated; no original checkout/index/gitlink was moved. Current canonical CI was in progress at observation; old failed CI remains historical.

Refresh completion: all 266 Python tests and 36 optimized controls, refreshed Debug build, 179 snapshot tests, 778 secret/evidence controls, 64 packaging checks, 24 compiler profiles and real-Docker cleanup smoke pass against the refreshed source. All 6,518 bound inputs remain stable and all 34 reviewed source bytes/hunks remain unchanged. The latest retained-bundle audit finds nine flags across 308 files, including preserved pre-refresh captures. This is a pending bulk-evidence landing disposition, not a source-control failure or an invented approval. The handoff and final manifest carry that boundary.
