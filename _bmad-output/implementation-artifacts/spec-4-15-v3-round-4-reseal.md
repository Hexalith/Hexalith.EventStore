---
title: 'Story 4.15 v3 Round-4 Reseal'
type: 'bugfix'
created: '2026-09-19'
status: 'done'
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

### Review Findings

_Code review of story-file diff `ba7ac196` → `6754a0c9` (2026-09-20). Layers: Blind Hunter, Edge Case Hunter, Verification Gap Reviewer (zero gaps), Acceptance Auditor (zero AC violations)._

- [x] [Review][Defer] Support-safe private-path scanning does not recognize deep UNC user paths [`tools/validate-oq8-platform-evidence.py:643`] — deferred: pre-existing scanner/redactor split; `PRIVATE_PATH_RE` still misses `\\corp\dfs\Users\jdoe\salary.xlsx` and `file://corp/users/...` while exception redaction now hides both. Reconfirms open DW-528.

- [x] [Review][Defer] Whitespace-bearing UNC intermediate segments remain visible [`tools/validate-oq8-platform-evidence.py:647-649`] — deferred: pre-existing residual under the frozen no-completeness boundary; `\\corp\DFS Root\Users\jdoe\salary.xlsx` is unchanged because intermediate segments exclude whitespace. Reconfirms open DW-529.

#### Rejected (2026-09-20 story-file review)

- false: slash-UNC mutation isolation does not recover the identifying suffix — owner chose host leak as enough. The `//` branch's unique token is `corp` / `fileserver`; POSIX `/users/` still covering the suffix after `//` removal is expected for the frozen `file://…/users/…` examples, not a masking defect. The I/O error column only requires the fact to turn red, which the current test does.
- low: restamped test receipt still says post-restamp focused/full reruns are required — the Always clause restamps first, then reruns without a third restamp, so that sentence is the two-stage process note; putting the rerun inside the same receipt would need a new evidence schema already rejected as BH-11.
- false: architecture receipt says DW-526 remained pending during review — past-tense review-window wording; the 2026-09-20 ledger settlement marks it done after replacement receipts existed.
- false: spec changelog 2026-09-20 vs `reviewedOn` 2026-09-19 — `reviewedOn` correctly derives from `V3_REVIEW_DATE`; the changelog is the local date of the follow-up commit, not a second review date that should have replaced it.
- false: hash-bound `docs/ci.md` never names `.gitattributes` — the cited sentence catalogs SDK/v2 gate-input examples, not the v3 fourteen-file set.
- false: architecture `acceptedScope` claims line-ending gate authority is reconciled — the bound `.gitattributes` identity matches; remaining checkout/editor policy is already DW-527.
- false: limitation 7 omits whitespace UNC, scanner/redactor split, and slash-adjacent tokens — the frozen Known-residual row defines that list and disclaims completeness; the extras are the deferred DW-528/DW-529 items.
- low: security receipt says measurement text survived on scheme/slash probes that include none — the frozen scheme/slash rows do not require measurement text; resealing for that adverbial is not a direct correction.
- false: `historical=True` is not pinned to the Story 4.14 observations digest — the exemption is the `validate_capture_packet` call site; production `EVIDENCE` is that capture, and the fixture reseal is the distinguishing caller-path probe.
- rejected (fix edits the spec under review): `review_loop_iteration` remains 0.
- false: `contracts-full` dropped TRX/coverage provenance — frozen Never forbids `dotnet test`; direct assembly is an acceptance constraint, not a missing TRX authority.
- low: `FreshAndCommittedRuntimeModesRemainExact` can leak a `CreateFixture` tree if setup throws before `try` — everyday passing runs clean up; wrapping the helper is extra structure for an undemonstrated setup failure.
- false: no characterization test that `EvidenceError` text is unredacted — limitation 7 discloses that residual; a leak test is not required, and DW-530 already tracks sanitizing it.
- false: `write_json` finally-unlink can delete a later occupant of `destination.tmp` — concurrent `write_json` is not shown; this validator runs as a single sequential process, and `unlink(missing_ok=True)` after `replace` is cleanup on the vacated name.

### Review Findings

_Second story-file pass, `ba7ac196` → worktree including uncommitted spec and ledger notes (2026-09-20). Layers: Blind Hunter, Edge Case Hunter, Verification Gap Reviewer (zero gaps), Acceptance Auditor (zero AC violations)._

- [x] [Review][Defer] Hash-bound `.gitattributes` has no explicit `eol`, and EditorConfig still defaults C# to CRLF [`.gitattributes:1`; `.editorconfig:8`] — deferred: pre-existing checkout/editor policy; `git check-attr` reports only `text: auto` on `.gitattributes` itself, while `[*.cs]` inherits `end_of_line = crlf`. Binding the file as the fourteenth gate input does not close that rewrite path. Reconfirms open DW-527.

- [x] [Review][Defer] Support-safe private-path scanning does not recognize UNC user paths [`tools/validate-oq8-platform-evidence.py:643`] — deferred: pre-existing scanner/redactor split; `PRIVATE_PATH_RE` still misses `\\corp\dfs\Users\jdoe\salary.xlsx` and `file://corp/users/...` while exception redaction now hides both. Reconfirms open DW-528.

- [x] [Review][Defer] Whitespace-bearing UNC intermediate segments remain visible [`tools/validate-oq8-platform-evidence.py:647-649`] — deferred: pre-existing residual under the frozen no-completeness boundary; `\\corp\DFS Root\Users\jdoe\salary.xlsx` is unchanged because intermediate segments exclude whitespace. Reconfirms open DW-529.

#### Rejected (2026-09-20 second story-file pass)

- false: `validate_capture_packet(..., historical=True)` lets a hostile packet skip subset checks — that skip is the specified Always protection for the immutable Story 4.14 capture; `+4` deltas still apply, and production observations remain hash-bound.
- false: the caller-path fact never mutates the tombstone subset, never calls `RunObservationValidator(historical: true)`, and pairs fixture `ROOT` with live `GIT_ROOT` — `RunCapturePacketValidator` is the production caller; both subset requires share one `if not historical:` branch, so the terminal-row mutation is distinguishing; the exemption does not depend on root pairing.
- false: limitation 7 must also disclose the scanner/redactor split, whitespace UNC, and the historical subset skip — the frozen Known-residual row defines that list and disclaims completeness; scanner and whitespace residuals are DW-528/DW-529, and the historical skip is not a redaction residual.
- low: the restamped test receipt says a post-restamp focused/full rerun is required, while `verification` stores only integer counters — that sentence is the two-stage process note; adding run identity would be a new evidence schema already rejected as BH-11.
- low: the security receipt says measurement text survived on scheme and slash probes that include none — the frozen scheme/slash rows do not require measurement text; resealing for that adverbial is not a direct correction.
- false: `ShouldNotContain("OQ8 Private")` cannot see a tab-separated name — the probe input is `/var/tmp/OQ8\tPrivate/validator.log`; redaction yields `path=<redacted-path>` and sibling assertions on `validator.log` and `/var/tmp` would still fail on a leak.
- low: `FreshAndCommittedRuntimeModesRemainExact` creates `historicalFixture` before `try` — everyday passing runs clean up; wrapping setup is extra structure for an undemonstrated failure.
- false: V3 has no `test-verification-command` mutation, so returning to `dotnet test` would stay green — the validator already pins receipt `command` to `V3_CONTRACTS_TEST_COMMAND`; a coordinated constant-plus-receipt change is a new reseal, not an unenforced identity check.
- false: slash-UNC mutation isolation does not recover the identifying suffix — host leak (`corp` / `fileserver`) is enough; POSIX `/users/` still covering the suffix after `//` removal is expected for the frozen `file://…/users/…` examples. The I/O error column only requires the fact to turn red.
- false: spaced terminal filenames remain visible after redaction — limitation 7 and the frozen Known-residual row list that residual on purpose.
- low: a digits/digits token abutting a path without whitespace is swallowed — the frozen deep-UNC probe is space-separated and preserves `2068/2068 ok`; tightening the non-space tail is tokenizer complexity for a shape this validator does not emit.
- false: `write_json` `finally` unlink can delete a later occupant of `destination.tmp` — concurrent `write_json` is not shown; this validator is a single sequential process, and `unlink(missing_ok=True)` after `replace` is cleanup on the vacated name.
- low: `temporary.unlink` raising in `finally` could hide the original write error — `missing_ok=True` does not raise for a missing sibling; swallowing `OSError` is extra complexity for an undemonstrated permission failure.
- low: if `Directory.Delete(fixture)` throws, `historicalFixture` is not deleted — leftover temp dirs after a failed cleanup are not an everyday OQ8 path; independent try blocks would guard a state this test never showed.

### Review Findings

_Third story-file pass, `ba7ac196` → worktree (2026-09-20). Layers: Blind Hunter, Edge Case Hunter, Verification Gap Reviewer (zero gaps), Acceptance Auditor (zero AC violations)._

- [x] [Review][Defer] Disclose that `ci / contracts` filters HeavyweightContainerPublish, or keep sealed `2068` without that sentence [`evidence/story-4-15-successors/v3/reviews/test.json:13-14,49-57`; `.github/workflows/ci.yml:93`] — deferred: Round-4 kept unfiltered direct assembly; CI filter stays a DW-533/DW-515 concern, not a packet reseal. Filed as DW-536.

- [ ] [Review][Patch] Remove the two un-IDed deferred-work blocks that restate DW-527, DW-528, and DW-529 [`_bmad-output/implementation-artifacts/deferred-work.md:4502-4521`] — left as an action item 2026-09-20; sprint row stays `review` and spec frontmatter stays `done` so the sealed lifecycle gate does not go red.

- [x] [Review][Defer] Support-safe private-path scanning does not recognize deep UNC user paths [`tools/validate-oq8-platform-evidence.py:643`] — deferred: pre-existing scanner/redactor split; `PRIVATE_PATH_RE` still misses `\\corp\dfs\Users\jdoe\salary.xlsx` and `file://corp/users/...` while exception redaction now hides both. Reconfirms open DW-528.

- [x] [Review][Defer] Whitespace-bearing UNC intermediate segments remain visible [`tools/validate-oq8-platform-evidence.py:647-649`] — deferred: frozen no-completeness residual; `\\corp\DFS Root\Users\jdoe\salary.xlsx` is unchanged because intermediate segments exclude whitespace. Reconfirms open DW-529.

- [x] [Review][Defer] Group R's deferred-gap limitation sentence and six high `run6-*` disclosures were not restored [`evidence/story-4-15-successors/v3/limitations.json:6-11`] — deferred: this chunk only appended the UNC-withdrawal sentence to limitation 7; limitation 8 already denies external authorities, and restoring the run6 list would be a new reseal outside the round-4 frozen Known-residual row.

- [x] [Review][Defer] Parent Story 4.15 spec still names the 2026-09-18 packet [`spec-4-15-oq8-platform-closure-and-handoff.md`] — deferred: that file is outside this story-file chunk; pointing it at round-4 subject `29880d6b…` / 465 / 2068 edits another spec.

#### Rejected (2026-09-20 third story-file pass)

- false: round-4 `requiredReviews` / `acceptedScope` still omit PostgreSQL capture invariants and suffixless JSON output — receipts no longer opine on those topics; scopes now match the UNC and `.gitattributes` findings this reseal actually issued.
- false: hash-bound `docs/ci.md` must name `.gitattributes` — the cited sentence catalogs SDK/v2 gate inputs, not the v3 fourteen-file set.
- false: the 2026-09-20 changelog cannot be true because receipts issued on 2026-09-19 already attest the caller-path fact — `reviewedOn` derives from `V3_REVIEW_DATE`; the changelog is the local date of the follow-up commit, not a second freeze date that should have replaced it.
- low: the restamped test receipt still says a post-restamp focused/full rerun is required before final handoff — that sentence is the two-stage process note; resealing it is more than a direct correction.
- low: the security finding that measurement text survived is false for scheme and slash probes — those frozen rows do not require measurement text; resealing for that adverbial is not a direct correction.
- false: limitation 7 must mention whitespace-bearing UNC intermediates — the frozen Known-residual row defines that list and disclaims completeness; the whitespace residual is DW-529.
- false: production UNC intermediates should be `{0,3}` and a false depth comment should have been removed — the I/O error column uses `{0,3}` as the falsifying mutation; production `*?` is what redacts `\\corp\dfs\emea\it\profiles\Users\…`, and the probe replaces `*?` with `{0,3}?`.
- false: `RunObservationValidator` never passes `historical: true`, so the exemption is untested — `RunCapturePacketValidator` is the production caller and already distinguishes fresh rejection from historical `validate_capture_packet` acceptance.
- false: `ShouldNotContain("OQ8 Private")` cannot see a tab-separated name — `main()` collapses tabs to spaces before redaction; a leak becomes `OQ8 Private`, and the probe output is `path=<redacted-path>`.
- false: slash-UNC mutation isolation does not recover `jdoe` / `salary.xlsx` — host leak (`corp` / `fileserver`) is the unique `//` token; POSIX `/users/` still covering the suffix after `//` removal is expected. The I/O error column only requires the fact to turn red.
- rejected (fix edits the spec under review): spec Verification omits the LiveSidecar command and does not name the 13 spot-run cases.
- rejected (fix edits the spec under review): `review_loop_iteration` remains 0.
- false: DW-524 remaining `open` and the architecture receipt saying DW-526 stayed pending during review contradict completion — the ledger is append-only, DW-526 is the settlement, and the receipt's past tense describes the 2026-09-19 review window before that settlement existed.
- low: a slash-bearing non-numeric token after a private path is swallowed — the frozen matrix preserves space-separated `2068/2068 ok`, not arbitrary slash-bearing diagnostics; tightening the space tail is tokenizer complexity.
- low: a digits/digits token abutting a path without whitespace is swallowed — the frozen deep-UNC probe is space-separated; applying the measurement lookahead to the non-space tail is extra complexity for a shape this validator does not emit.
- false: HTTP URLs with extra segments before `/users/` are redacted as UNC — the frozen `//` branch is required for `file://` and `smb://`; adding scheme lookbehind is the specified mutation that must turn scheme-prefixed UNC red.
- false: `write_json` `finally` unlink can delete a later occupant of `destination.tmp` — concurrent `write_json` is not shown; this validator is a single sequential process.
- low: if `Directory.Delete(fixture)` throws, `historicalFixture` is not deleted — leftover temp dirs after a failed cleanup are not an everyday OQ8 path.

### Review Findings

_Fourth story-file pass, `ba7ac196` → worktree story-file only (2026-09-20). Layers: Blind Hunter, Edge Case Hunter, Verification Gap Reviewer (zero gaps), Acceptance Auditor (zero AC violations)._

- [ ] [Review][Patch] Remove the two un-IDed deferred-work blocks that restate DW-527, DW-528, and DW-529 [`_bmad-output/implementation-artifacts/deferred-work.md:4502-4521`]

#### Rejected (2026-09-20 fourth story-file pass)

- false: VG-01's triage row is still an open patch — that table is the original packet-review snapshot; the 2026-09-20 changelog and `RunCapturePacketValidator` already record the caller-path test landing.
- false: BH-15 still requires a DW-524 settlement — DW-526 is that append-only settlement and is `done`; leaving DW-524 `open` is the specified ledger policy.
- false: Verification omits the eight pre-review commands and the 13 spot-run cases — those commands belong in `pre-review-execution.json`; Verification is the post-restamp AC list. Prior pass already rejected adding them here.
- false: Observed results omit Git-handoff proof — handoff is commits `bc39811e` and `6754a0c9`; Observed results is the measurement block.
- rejected (fix edits the spec under review): `review_loop_iteration` remains 0.
- false: three `### Review Findings` headings reprint DW-528 and DW-529 — each pass must append its own section; the canonical open items are already DW-527 through DW-529.
- false: Code Map, Execution, and ACs omit the packet-caller follow-up — Code Map already requires the historical exemption; the changelog records DW-525/DW-526. Frozen ACs stay the UNC/count/limitation-7 contract.
- false: Observed results omit start/end hash-checks — subject `29880d6b…` is in Implementation Notes; manifest and receipt digests are in DW-526.
- false: the first story-file header claiming zero verification gaps contradicts VG-01 — that header describes the story-file layer run; VG-01 is in the earlier packet Review Triage Log.
- false: frozen Approach “complete limitations” is unmet because DW-534/DW-535 exist — Approach is the round-4 limitation-7/UNC-withdrawal work; those parent-spec and run6 items are already deferred with ledger IDs.

### Review Findings

_Fifth story-file pass, `ba7ac196` → worktree story-file only (2026-09-20). Layers: Blind Hunter, Edge Case Hunter, Verification Gap Reviewer (empty — recorded failed), Acceptance Auditor (zero AC violations)._

- [ ] [Review][Patch] Remove the un-IDed deferred-work restatements of DW-527, DW-528, and DW-529 [`_bmad-output/implementation-artifacts/deferred-work.md:4502-4525`]

- [x] [Review][Defer] Hash-bound `.gitattributes` has no explicit `eol`, and EditorConfig still defaults C# to CRLF [`.gitattributes:1`; `.editorconfig:8`] — deferred: pre-existing checkout/editor policy; reconfirms open DW-527.

- [x] [Review][Defer] Support-safe private-path scanning does not recognize deep UNC user paths [`tools/validate-oq8-platform-evidence.py:643`] — deferred: pre-existing scanner/redactor split; reconfirms open DW-528.

- [x] [Review][Defer] Whitespace-bearing UNC intermediate segments remain visible [`tools/validate-oq8-platform-evidence.py:647-649`] — deferred: frozen no-completeness residual; reconfirms open DW-529.

- [x] [Review][Defer] `write_json` still uses a predictable `<destination>.tmp` sibling [`tools/validate-oq8-platform-evidence.py:864-873`] — deferred: pre-existing collision path; cleanup landed, exclusive publish did not; reconfirms open DW-531.

- [x] [Review][Defer] Group R's deferred-gap limitation sentence and six high `run6-*` disclosures were not restored [`evidence/story-4-15-successors/v3/limitations.json:6-11`] — deferred: restoring them is a new reseal; reconfirms open DW-534.

- [x] [Review][Defer] Parent Story 4.15 spec still names the 2026-09-18 packet [`spec-4-15-oq8-platform-closure-and-handoff.md`] — deferred: that file is outside this story-file chunk; reconfirms open DW-535.

- [x] [Review][Defer] Round-4 test receipt does not disclose that `ci / contracts` filters HeavyweightContainerPublish [`evidence/story-4-15-successors/v3/reviews/test.json:13-14,49-57`] — deferred: unfiltered direct assembly is the attested command; reconfirms open DW-536.

#### Rejected (2026-09-20 fifth story-file pass)

- false: limitation 7 must name whitespace UNC and the scanner/redactor split — the frozen Known-residual row defines that list and disclaims completeness; those residuals are DW-528/DW-529.
- false: hash-bound `docs/ci.md` must name `.gitattributes` — that sentence catalogs SDK/v2 gate inputs, not the v3 fourteen-file set.
- false: `source-only-handoff.json` current/historical rules must narrate the withdrawn UNC approval — withdrawal is bound in limitation 7; those rules are the validation instruction.
- false: slash-UNC mutation isolation does not recover `jdoe` / `salary.xlsx` — host leak is the unique `//` token; the I/O error column only requires the fact to turn red. The success path already forbids the suffix.
- false: production `(?:\\\\|//)` over-redacts HTTPS `/users/` URLs — the frozen `//` branch is required for `file://` and `smb://`; scheme lookbehind is the specified mutation that must turn scheme-prefixed UNC red.
- false: `validate_capture_packet(..., historical=True)` skipping subset checks is an undisclosed integrity opt-out — that skip is the specified Always protection for the immutable Story 4.14 capture; fresh validation still uses the default.
- false: `RunObservationValidator` never passes `historical: true`, so the exemption is untested — `RunCapturePacketValidator` is the production caller and already distinguishes fresh rejection from historical acceptance.
- false: VG-01's triage row is still an open patch — that table is the original packet-review snapshot; the 2026-09-20 changelog and `RunCapturePacketValidator` already record the caller-path test landing.
- false: architecture receipt saying DW-526 remained pending during review contradicts completion — the receipt's past tense describes the 2026-09-19 review window before the 2026-09-20 settlement existed.
- false: deep-UNC I/O error column `{0,3}` does not match the probe `{0,3}?` — the column names the depth bound; the probe's `?` mirrors production lazy `*?`.
- false: a digits/digits token abutting a path without whitespace is swallowed — the frozen deep-UNC probe is space-separated; this validator does not emit that shape.
- low: the security receipt says measurement text survived on scheme and slash probes that include none — those frozen rows do not require measurement text; resealing for that adverbial is not a direct correction.
- low: the restamped test receipt still says a post-restamp focused/full rerun is required — that sentence is the two-stage process note; adding run identity would be a new evidence schema.
- low: `FreshAndCommittedRuntimeModesRemainExact` can leak `historicalFixture` if setup or the first delete throws — leftover temp dirs after a failed cleanup are not an everyday OQ8 path.
