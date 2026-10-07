---
title: '6.1-P1R unblock candidate CI: exempt the sealed remediation source capture from the secrets scan'
type: 'bugfix'
created: '2026-10-06'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '283b07a52c9c70e1c940164a7011ee8c3ad98b2d'
work_package_id: '6.1-P1R-remediation'
context:
  - '{project-root}/../projects/_bmad-output/implementation-artifacts/spec-6-1-p1r-remediation.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore CI run 37511904998 at `283b07a5` (the pushed P1R remediation head) fails its only blocking test failure in `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`, which flags `_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/source-candidate.diff:2590`. That line is a C# `CancellationToken` construction inside a captured test diff: the live `.cs` file passes because the scanner applies C# expression rules by file extension, while the `.diff` copy is classified as a literal. The diff is SHA-256-bound by the sealed packet's `SHA256SUMS` and cannot be edited. Without a green `ci.yml`, the Release workflow can only publish a remediated candidate under `bypass-validation`, the limitation 3.110.0 carried.

**Approach:** Following the existing `6-1-p1r-3110/verification` precedent in `ExplicitEvidenceArtifactPathPattern`, exempt exactly that one sealed capture file from the reusable-content scan, and add a regression test proving the exemption is limited to that path while sibling packet files, the pre-review receipt copy, and the qualification packet stay scanned.

**Decisions (user, 2026-10-06):** Target this CI-unblocking slice only; leave the change uncommitted. No push, release, gitlink, Projects planning, `.gitattributes` or sealed-evidence edits.

</frozen-after-approval>

## Implementation Notes

- Reproduced before editing: Debug/project-reference build of `tests/Hexalith.EventStore.Server.Tests`, then `-class Hexalith.EventStore.Server.Tests.Security.SecretsProtectionTests` reported 56 total, 1 failed, with the same `source-candidate.diff:2590` violation as CI run 37511904998.
- Root cause confirmed in `IsRecognizedSourceExpression`: C# runtime-expression rules apply by path extension, so a `.diff` copy of a C# constructor call falls through to literal classification.
- Changed only `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs`. Added `6-1-p1r-remediation/source-candidate\.diff` to `ExplicitEvidenceArtifactPathPattern` as an exact file, not a directory, and added two facts: `SealedP1RRemediationExemption_IsLimitedToTheSourceCapture` and `SealedP1RRemediationSourceCapture_RemainsBytePreserved`.
- Decision: bind the exemption to the sealed bytes (`220af5d8…56aa`, the packet's `SHA256SUMS` entry) so an edit fails loudly instead of escaping the scan. Normalize only CRLF pairs, because the capture has zero CR bytes while `text=auto` permits CRLF checkouts. Rejected reusing `story-5-3-retired-captures.json`: retirement marks captures non-authoritative because they contain secrets, which is false for this active evidence.
- Mutation check: widening the pattern to `6-1-p1r-remediation/.+` and altering one hash character failed both new facts; the fixed file was then restored.
- Not changed: sealed evidence, `.gitattributes` (a sealed gate input), the retired-captures manifest, Projects planning and gitlinks. No Aspire baseline was taken, because the change touches only a test-source scanner with no runtime or AppHost surface.
- Review patches:
  - `ReadTrackedText` now verifies the pinned content hash before skipping the capture, the same way `IsRetiredEvidenceCapture` does. An altered capture therefore fails even a filtered run of `TrackedReusableContent_DoesNotContainUsableSecrets`, with an actionable message.
  - The earlier two facts are replaced by three:
    - `SealedP1RRemediationExemption_IsLimitedToTheSourceCapture`: across the real tracked remediation/qualification packet files, the capture is tracked and is the only exempt file; near-miss paths are not exempt.
    - `SealedP1RRemediationSourceCapture_ExemptionRequiresItsSealedContent`: the pinned hash equals the packet's `SHA256SUMS` entry, a CRLF form passes, and an appended byte fails.
    - `SealedP1RRemediationSourceCapture_HidesOnlyTheKnownFalsePositive`: a direct scan of the capture reports exactly `:2590`.
  - A second mutation run (one hash character, and the pattern widened to the whole packet) failed the filtered main scan and both scope/seal facts; the patched file was restored.
- Release provenance: this change is test-only, and no `src/` or runtime file changed. The commit that turns `ci.yml` green will have a new SHA, not `283b07a5`. The existing source packet binds `d24a0356` plus its captured diff, not that commit. Any candidate published from the green commit therefore needs its own source/package binding in the later published-package qualification lane. Existing qualification packets will correctly report source drift.
- Two pre-existing follow-ups were appended to this repository's `deferred-work.md`: diff-aware scanner classification, and a missing `text eol=lf` rule for the P1R packets. They went to EventStore's own ledger, since EventStore owns both.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Blind 1: path exemption skips every rule for the whole capture | low | patch | The bytes are fixed, so the residual risk is future rules. A direct-scan fact now asserts exactly one known violation at `:2590`. |
| Blind 2: hash fact is separable from the exemption | medium | patch | A filtered or quarantined run skipped an altered capture by path alone. The hash is now asserted inside `ReadTrackedText`, and the mutation run proves a filtered main scan fails. |
| Blind 3: scope test used made-up paths and overclaimed qualification coverage | low | patch | It now enumerates the real tracked packet files and asserts the capture is tracked; only boundary near-misses stay synthetic. |
| Blind 4a: "byte-preserved" name overstated CRLF normalization | low | patch | Renamed to content semantics, with a comment explaining the LF-only seal. |
| Blind 4b: packet lacks a `text eol=lf` rule | medium | defer | Verified `attr/text=auto`. Pre-existing, and `.gitattributes` is a sealed gate input. |
| Blind 5: pinned constant is not tied to `SHA256SUMS` | low | patch | It is now asserted equal to the `SHA256SUMS` line for `source-candidate.diff`. |
| Blind 6: failure message not actionable | low | patch | The message now says to restore the content and never re-pin without rescanning. |
| Blind 7: root cause is extension-only classification of diffs | low | defer | A valid improvement idea, beyond the user-approved precedent approach; recorded in `deferred-work.md`. |
| Blind 8: spec lacks Code Map/Tasks/Verification and notes | false | reject | The one-shot route keeps only frontmatter, Intent and Implementation Notes. The notes were written while the review ran and record the hash-binding and manifest decisions. |
| Blind 9: release SHA is not linked to sealed evidence | low | patch | A provenance note was added; candidate binding belongs to the later package lane. |
| Blind 10: wording inside the frozen block; unresolvable `context` path | low | reject | The frozen wording is human-owned. The `{project-root}/../projects/...` form matches the sibling EventStore P1R specs. |

## Verification

- Before the fix: `-class Hexalith.EventStore.Server.Tests.Security.SecretsProtectionTests` reported 56 total, 1 failed (`source-candidate.diff:2590`).
- Final: the Debug/project-reference `-warnaserror` build of `tests/Hexalith.EventStore.Server.Tests` had 0 warnings and 0 errors. The secrets class reported 59 total, 0 failed. The full Server.Tests run reported 3,820 total, 0 failed and 25 pre-existing DW1 skips, the same skip count CI reports. `git diff --check` passed.
- Not run locally: the CI Release/package-mode lane. Confirmation requires pushing a commit and a green `ci.yml` run, which the user has not authorized.
