---
title: 'Story 4.15 v3 Round-4 Reseal'
type: 'bugfix'
created: '2026-09-19'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'ba7ac196e60db8820525961791eccfacec24633f'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The published v3 security receipt falsely approves nested-UNC redaction: the bound validator leaks a deep DFS path verbatim. The reverted round-3 repair also lacked independent falsification controls for deep UNC depth, scheme-prefixed UNC, and the `//` UNC branch, so its receipts could not be true.

**Approach:** Rebuild the accepted round-3 hardening on the known-green tree, add isolated mutation-sensitive controls and complete limitations, freeze one exact candidate, obtain independent architecture/security/test approval without changing bound bytes, then reseal and verify the post-restamp packet.

## Boundaries & Constraints

**Always:** Keep Story 4.14 and v1/v2 evidence immutable; preserve `spec: done` plus sprint `review`; derive `reviewedOn` from `V3_REVIEW_DATE`; bind current source and exact measured counts; hash-check bound files at review start/end; stop without receipts or commit if any reviewer rejects. Run Contracts restore/build in Release package mode and invoke the built assembly directly. After approvals, write receipts, propagate hashes, run both lanes, restamp all receipts, propagate again, and rerun both lanes plus all four validator modes on final bytes.

**Never:** Do not use `dotnet test`, pipe validator output through `tail`, edit bound bytes during review, fabricate approval, sync sprint status, change Story 4.14 observations, include the reverted submodule bumps, publish/push, or grant release/deployment/package/registry/runtime-pin/consumer/Folders authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Deep UNC | `\\corp\dfs\emea\it\profiles\Users\jdoe\salary.xlsx` | Entire private path is redacted; measurement text survives | `{0,3}` depth mutation turns its isolated fact red |
| Scheme UNC | `cfg:\\corp\users\kdoe\payroll.csv` | Entire private path is redacted | Re-added scheme lookbehind turns its separate fact red |
| Slash UNC | `file://corp/users/...` and `smb://fileserver/users/...` | Host and identifying suffix are redacted | Removing `//` from the UNC alternation turns the fact red |
| Known residual | Non-`users`/`home` UNC roots, generic drive/home shorthand, 8.3 paths, spaced terminal names, repr-doubled Windows paths, and `EvidenceError` text | Limitation 7 enumerates the residuals and states redaction precedes 256-character truncation | No completeness claim is made |
| Prior approval | Historical security receipt cannot encode dissent | Limitation 7 states the schema restriction and withdraws the false prior approval | New receipts bind only the round-4 subject |

</frozen-after-approval>

## Code Map

- `tools/validate-oq8-platform-evidence.py` -- reuse the round-3 root-fragment/tail design; remove the false depth comment, set the review date/counts, use direct assembly for `contracts-full`, retain fresh-capture subset checks plus historical exemption and temporary-file cleanup.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- restore accepted round-3 guards; split deep/scheme probes, add slash-UNC control, and cover limitation indexes 6 and 7.
- `.gitattributes` -- existing `global.json`/v3 line-ending authority; bind it as a v3 gate input without editing its bytes.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/` -- regenerate limitations, identity, execution, frozen subject, approved receipts, handoff, validator record, and manifest in dependency order.
- `_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json` -- receive final v3 artifact, manifest, subject, identity, and handoff hashes.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- append a canonical settlement correcting DW-521's historical counts after final measurement; do not rewrite existing entries.
- Round-3 scratch `backup/`, `reseal_subject.py`, and `reseal_receipts.py` -- reference only; adapt and inspect before use, and allow receipt writing only after three approvals.

## Tasks & Acceptance

**Execution:**
- [x] Validator and Contracts test file -- apply only the reviewed round-3 hardening, add the three isolated UNC facts and mutation controls, complete limitation 7, bind `.gitattributes`, and record the direct-assembly Contracts command.
- [x] V3 candidate artifacts -- execute the eight pre-review commands, measure focused/full counts, set `V3_REVIEW_DATE` and `reviewedOn` to `2026-09-19`, generate the exact subject, and verify candidate hashes.
- [x] Architecture, security, and test reviews -- run independently and concurrently against unchanged subject bytes; compare start/end hashes and stop on any rejection.
- [x] Receipts, handoff, manifest, selector, and DW-521 settlement -- issue only true approvals, perform the two-stage run/restamp/run sequence, and append the measured-count correction.
- [x] Git handoff -- verify no submodule bump, inspect agents/processes/bmad-loop, validate a `fix(oq8)` or `docs(oq8)` Conventional Commit with a body disclosing every validator behavior change, and commit only after all gates pass.

**Acceptance Criteria:**
- Given each specified regex mutation, when its isolated fact runs, then that fact fails for the intended leaked token and no other probe can mask it.
- Given the final post-restamp bytes, when focused OQ8 closure and full Contracts assemblies run, then their exact measured counts pass with zero failures/skips; default, final lifecycle, historical v1, and historical v2 validators all exit 0.
- Given the sealed packet, when reviewers and consumers inspect it, then limitation 7 is complete, the prior false security approval is explicitly withdrawn, all three receipts bind the same reviewed subject, and no external authority or submodule change is claimed.

## Implementation Notes

- Replaced the bounded nested-UNC branch with the reviewed root-fragment/tail design and proved deep, scheme-prefixed, and slash-form UNC handling with separate mutation-sensitive facts.
- Kept PostgreSQL subset checks fresh-only, preserved historical validation semantics, and made suffixless JSON output clean up temporary files after both success and serialization failure.
- Bound unchanged `.gitattributes` bytes as the fourteenth v3 gate input, recorded all eight limitations, and switched the full Contracts receipt to direct assembly execution.
- Froze final review subject `29880d6b3ebb9a67d69d0ff2f70afd138a7c81cfd2938fcd437de5d863ede913`; fresh independent architecture, security, and test reviews approved unchanged subject, limitations, identity, execution, and candidate-manifest hashes.
- Restamped all receipts after the first focused/full verification and propagated final receipt, handoff, manifest, and selector hashes before repeating every final gate.

## Spec Change Log

- 2026-09-19: Implemented and independently approved the round-4 reseal; final packet binds 14 gate inputs, 465 focused cases, and 2068 full Contracts cases.
- 2026-09-20: Added the packet-caller historical-exemption regression, re-froze and freshly reviewed the exact candidate, and completed DW-526 only after replacement receipts and final selector propagation existed.

## Review Triage Log

- Architecture: approved; historical lineage, Git identity, line-ending authority, limitations, and source-only authority boundaries reconcile.
- Security: approved; all three UNC controls are independently falsifiable, residuals and the prior false approval are disclosed, and stale receipts fail closed.
- Test: approved; a 13-case independent spot run passed, then both exact direct-assembly lanes passed before and after receipt restamping.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| VG-01 historical exemption lacks a caller-path test | medium | Pre-verified gap: the distinguishing test passes `historical: true` directly, while the CLI fixtures satisfy the fresh inequalities, so removing the flag from `validate_capture_packet` is undetected. Patch with a distinguishing packet-caller test. |
| BH-01 hash-bound YAML and props lack explicit EOL attributes | medium | `git check-attr` reports `eol: unspecified` for both workflows and `tests/Directory.Build.props`; checkout conversion can alter their raw hashes. This line-ending policy predates the reseal, so defer it. |
| BH-02 EditorConfig CRLF conflicts with C# LF attributes | medium | `.editorconfig` inherits CRLF for C# while `.gitattributes` requires LF, allowing editor-written CRLF bytes to hash differently despite Git normalization. This policy conflict predates the reseal, so defer it. |
| BH-03 support-safe scanners miss deep UNC paths | medium | A direct probe confirms `PRIVATE_PATH_RE` does not match `\\corp\dfs\Users\jdoe\salary.xlsx`, although the exception redactor does. The scanner defect predates this change, so defer it. |
| BH-04 whitespace-bearing UNC intermediates remain visible | medium | A direct probe leaves `\\corp\DFS Root\Users\jdoe\salary.xlsx` unchanged because intermediate segments exclude whitespace. This is a pre-existing residual under the frozen no-completeness boundary, so defer it. |
| BH-05 slash-bearing adjacent diagnostics may be swallowed | low | The probe `path=/home/jdoe/secret.txt version=1.2.3/foo` reduces to `path=<redacted-path>`, but the frozen matrix guarantees the numeric measurement case, not arbitrary slash-bearing tokens. A broader parser change is disproportionate to this diagnostic-only loss, so reject it. |
| BH-06 EvidenceError text can disclose CTRF-controlled paths | medium | `EvidenceError` is printed verbatim and deterministic-support names are interpolated into errors. The frozen matrix explicitly discloses EvidenceError text as a pre-existing residual, so defer it. |
| BH-07 historical mode is an unrestricted integrity opt-out | false | `historical=True` is reachable only from the internal, manifest-bound immutable Story 4.14 packet path; untrusted fresh capture validation always uses the default. The claimed unrestricted caller bypass does not occur. |
| BH-08 JSON writes reuse a predictable sibling temporary path | medium | `write_json` opens `<destination>.tmp`, so concurrent writers or a pre-existing link can collide; the current change only adds cleanup. The naming defect predates this story, so defer it. |
| BH-09 review conclusions are not semantically enforced | false | The normative receipt fields bind exact role, reviewer, scope, subject, limitations, decision, authority, and test commands; `findings` is intentionally narrative. Omitting a particular sentence does not bypass the bound approval contract. |
| BH-10 reviewer independence is not cryptographically authenticated | medium | Receipts bind names and timestamps but carry no reviewer-owned signature or immutable transcript. Actual independent reviews occurred here, but the evidence-model limitation predates this story, so defer it. |
| BH-11 two-stage execution lacks structured per-stage evidence | low | The receipt findings record both stages and the required process ran, but the verification array stores only final canonical command records. Adding a new evidence schema is disproportionate to this audit-detail gap, so reject it. |
| BH-12 direct assembly removes required TRX and coverage evidence | false | Direct assembly execution is an explicit frozen constraint, and neither TRX nor coverage artifacts are acceptance requirements; both exact lanes ran twice with zero failures or skips. |
| BH-13 the full Contracts count is not structurally bound | high | Only a subset of Contracts sources is gate-bound, so unrelated test additions can stale `2068`; DW-521 already documents this pre-existing enforcement defect. Defer it with the related CI gap. |
| BH-14 no required CI check executes the closure guards | high | The required-check gap is real and already owner-routed by DW-521; this reseal does not create or authorize repository-ruleset changes. Defer it with the unbound-count finding. |
| BH-15 DW-524 remains open after its fix lands | medium | The ledger still says the false nested-UNC approval is open even though the round-4 packet now withdraws and replaces it. Patch by appending a settlement without rewriting DW-524. |
| BH-16 the withdrawal does not identify a receipt digest | false | The active selector and manifest bind only the new round-4 receipt, while the withdrawal refers to the superseded approval at the same v3 lineage path. The intent does not require a separate revocation schema or digest. |
| BH-17 `in-review` contradicts completed implementation | false | `in-review` is the lifecycle state mandated by this workflow while independent review and triage are active; completion status is assigned only after review closes. |
| ECH-01 whitespace-bearing UNC intermediates leak | medium | Independent edge tracing confirms the same pre-existing whitespace-segment residual as BH-04. Defer it with that root cause. |
| ECH-02 predictable temporary siblings can collide | medium | Independent edge tracing confirms the same pre-existing `write_json` collision as BH-08. Defer it with that root cause. |
| ECH-03 limitation 7 falsely claims exhaustive redaction | false | Limitation 7 begins by saying redaction is intentionally incomplete, and the frozen matrix expressly makes no completeness claim. The alleged exhaustive consumer promise is absent. |

## Design Notes

The receipt schema requires `decision: approved`; withdrawal therefore belongs in the bound limitations rather than a dissent receipt. The append-only ledger likewise receives a new settlement entry instead of mutating DW-521.

## Verification

**Commands:**
- `dotnet restore tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -m:1 -p:Configuration=Release -p:UseHexalithProjectReferences=false` -- exact package-mode restore passes.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- focused measured total passes.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -noColor` -- full measured total passes.
- `.oq8-python/bin/python tools/validate-oq8-platform-evidence.py` and the `--lifecycle-mode final`, `--historical-v1-only`, and `--historical-v2-only` variants -- each exits 0 on final bytes.
- `git diff --check` -- clean.

**Observed results:**
- Pre-restamp: focused `465/465` in 163.853 seconds; full Contracts `2068/2068` in 247.829 seconds; zero errors, failures, skips, or not-run cases.
- Post-restamp: focused `465/465` in 242.330 seconds; full Contracts `2068/2068` in 247.331 seconds; zero errors, failures, skips, or not-run cases.
- Post-restamp v3 mutation control: `33/33` passed in 20.185 seconds with zero errors, failures, skips, or not-run cases.
- Default, final lifecycle, historical v1, and historical v2 validator modes each exited 0 against final bytes.
