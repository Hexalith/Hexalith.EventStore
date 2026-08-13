---
title: 'Fix OQ8 lifecycle validation after Story 4.8 migration'
type: 'bugfix'
created: '2026-08-12'
status: 'in-review'
review_loop_iteration: 6
baseline_commit: 'e20063981f6ff79c386f9050ac0f5874e3e4b05f'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI run 31593347938 fails 21 Contracts tests because the OQ8 closure validator still requires `4-8-durable-admission-evidence-ledger: backlog`. The approved August migration made Story 4.8 a non-executable evidence ledger and requires its sprint-status row to remain absent.

**Approach:** Align the fail-closed lifecycle validator with the migration: accept the authoritative absence, explicitly reject any reintroduced Story 4.8 row, preserve all executable Epic 4 status checks, and rebind the corrected validator only after fresh content-bound architecture, security, and test review.

## Boundaries & Constraints

**Always:** Keep Story 4.8 absent and reject its retired key; preserve exact, unique statuses for Epic 4 and Stories 4.9–4.15; retain nine lifecycle regression cases; obtain fresh independent architecture, security, and test approval for the corrected subject before issuing receipts or handoff metadata.

**Ask First:** Changes to landed-source or capability-path identity, external authority, public OQ8 semantics, or immutable Story 4.14 evidence; inability to obtain all three approvals.

**Never:** Restore the orphan row; weaken identity, manifests, receipt binding, or bounded failures; copy old approvals to changed bytes; mutate Story 4.14 evidence; claim external authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Authoritative lifecycle | Story 4.8 row absent; required Epic 4 rows exact and unique | Candidate, final-lifecycle, and committed validators continue past lifecycle checks | Fail later only for an independent violated invariant |
| Retired row returns | Any exact Story 4.8 sprint-status row, with any value | Validator rejects the lifecycle | Bounded, deterministic lifecycle error naming the retired key |
| Executable status drifts | Required row missing, duplicated, or wrong | Existing fail-closed behavior is unchanged | Existing missing/ambiguous or drift error names the affected key |

</frozen-after-approval>

## Code Map

- `tools/validate-oq8-platform-evidence.py:1572-1616` -- lifecycle parsing and the stale Story 4.8 requirement; derive active and retired-key decisions from one structure-aware `development_status` mapping parse without adding a runtime dependency.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs:286-352,510-569,687-719` -- lifecycle and semantic-mutation theories; retain the nine existing lifecycle cases, add YAML-shape boundaries, and prove stale candidate/reviewer dates fail after identity resealing.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:232-240` -- authoritative current state; read-only for this fix.
- `_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md:27-35` -- read-only migration authority; the post-correction readiness report at lines 257-260 confirms exclusion.
- `_bmad-output/implementation-artifacts/evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/` -- bound execution, reviews, handoff, and manifest; freshly review and reseal in dependency order.
- `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml:37-55` -- outer identities; update last.

## Tasks & Acceptance

**Execution:**
- [x] `tools/validate-oq8-platform-evidence.py` -- parse the direct entries of `development_status` once, enforce active exact/unique statuses from that normalized mapping, and reject the retired Story 4.8 key regardless of quoting, colon spacing, indentation, or empty/null/complex value. Scope detection to that mapping and fail closed on duplicate keys, merge keys, or unsupported structure.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- retain the existing nine lifecycle cases; add retired-key variants (quoted and escaped key, colon whitespace, alternate mapping indentation, empty/null/comment-only and quoted/complex values), normalized duplicate keys, inline/indented multi-document markers, shadow target declarations at non-root indentation, aliased/tagged `development_status` mappings with block and inline values, malformed top-level scalar/flow syntax, scoped tagged-anchor aliases, BOM and non-printable-source rejection, bounded hostile-key diagnostics, near-match/out-of-section positives, and stale `createdOn`/`executedOn`/`reviewedOn` mutations that reach the intended date guards after resealing.
- [x] Story 4.15 evidence directory and `4-8-eventstore-oq8-platform-evidence.yaml` -- record corrected identities/counts, obtain fresh exact-subject reviews, then reseal inner and outer chains without changing capture evidence or authority.

**Acceptance Criteria:**
- Given the authoritative sprint status excludes Story 4.8, when all three OQ8 lifecycle modes run, then lifecycle validation passes and downstream invariants remain enforced.
- Given any Story 4.8 status row is introduced, when lifecycle validation runs, then it fails closed with a bounded error naming the retired key.
- Given the retired key is quoted, oddly spaced, empty/null, or assigned a scalar/collection value, when it is a direct `development_status` entry, then the same bounded retired-key error is returned; the same text outside that mapping and near-match keys do not trigger it.
- Given the lifecycle file contains multiple YAML documents, a non-initial BOM, or a non-printable source character, when lifecycle validation runs, then it fails closed with bounded output; unrelated supported top-level content in the single document remains outside lifecycle scope.
- Given a required active status is missing, duplicated, or drifted, when validation runs, then the existing specific rejection remains intact.
- Given candidate subject/execution evidence or a final reviewer receipt carries the previous review date and its surrounding hashes are resealed, when validation runs, then the corresponding date-drift guard rejects it.
- Given changed validator bytes, when committed evidence is validated, then only a freshly reviewed, consistent binding chain passes.
- Given the corrected tree, when the focused OQ8 class and complete Contracts project run in Release, then all tests pass with zero build warnings or errors.

## Spec Change Log

- 2026-08-12 review loop 1: Adversarial edge-case and verification-gap review showed the text regex accepted quoted or null-valued Story 4.8 rows and that fresh-review dates lacked mutation coverage. The Code Map, tasks, acceptance criteria, and verification now require one structure-aware `development_status` parse, YAML-shape and scoping boundaries, and resealed stale-date mutations. This avoids semantically reintroducing the retired story or allowing future weakening of freshness guards to remain green. KEEP: positive Story 4.8 absence enforcement; bounded key-specific diagnostics; all nine lifecycle regression cases; exact active-status behavior; immutable Story 4.14 evidence and landed capability identity; three independent exact-subject approvals; unchanged external-authority exclusions.
- 2026-08-12 review loop 2: Blind and edge-case review showed the dependency-free YAML subset accepted a colon without YAML separation for active rows, normalized non-YAML Unicode whitespace, parsed unrelated top-level syntax, and did not reject tagged, anchored, or aliased values consistently. Tighten the non-frozen parser design and test matrix to decode the supported YAML scalar forms exactly, require a real block-mapping separator for ordinary entries, keep the retired-key diagnostic conservative for every requested spelling, ignore document markers outside the target mapping, and fail closed on unsupported direct-entry values. This avoids treating malformed YAML as an authoritative active status or letting unsupported YAML constructs bypass lifecycle validation. KEEP: all loop-1 guarantees; duplicate top-level and deeper-indentation rejection; quoted/empty/null/collection retired-key coverage; scoped near-match and out-of-mapping positives; stale-date guards; exact-subject evidence rebinding and external-authority exclusions.
- 2026-08-12 review loop 3: Final blind, edge-case, and verification-gap review showed the strict subset still ignored additional YAML documents/BOM-prefixed declarations, admitted non-printable source characters, reflected hostile duplicate keys without a bound, rejected unrelated top-level aliases despite the documented scope, and lacked normalized-duplicate/escaped-key coverage. Require exactly one YAML document, permit a BOM only at stream start, validate YAML-printable source characters before parsing, keep attacker-controlled diagnostics generic and bounded, scope unsupported-node rejection to `development_status`, and exercise normalized duplicates plus every supported numeric escape width. This avoids retired-key bypass through a second document and diagnostic/control-character injection while preserving intentional top-level scoping. KEEP: every loop-1 and loop-2 regression; strict separator and ASCII syntax-whitespace behavior; exact active values; unsupported direct-value and explicit-key rejection; immutable Story 4.14 evidence; fresh exact-subject reviews; unchanged external-authority exclusions.
- 2026-08-12 review loop 4: Final review found that inline/indented document markers and aliased or property-wrapped `development_status` keys with inline values could evade duplicate-mapping detection; it also found that tagged anchors used only by unrelated top-level aliases were rejected and malformed top-level tokens were silently skipped. Define a bounded top-level grammar that rejects every additional document marker regardless of inline content/indentation, detects normalized target keys before considering their value shape, tracks supported anchors through node properties, permits unrelated resolved aliases, and rejects unrecognized top-level syntax. This prevents hidden retired mappings while preserving the intended outside-target scope. KEEP: all prior-loop lifecycle, scalar, printability, bounded-output, date, evidence, authority, and full-suite guarantees.
- 2026-08-12 review loop 5: Exact-subject test review showed the top-level scan still skipped an indented shadow `development_status` mapping and accepted an unclosed flow value on an unrelated root key. Detect any normalized target declaration regardless of indentation and reject it unless it is the single supported root block mapping; validate unrelated root values only within the explicitly supported scalar/empty-block subset and reject malformed or unsupported flow syntax before lifecycle acceptance. This closes the remaining hidden-retired-row and malformed-root bypasses without attempting general YAML compatibility. KEEP: all loop-1 through loop-4 cases and successful focused/full verification behavior.

## Design Notes

Treat Story 4.8 absence as a positive invariant over the parsed `development_status` mapping, not as a text-pattern omission. Active and retired-key rules must consume the same normalized direct-entry set. The dependency-free parser is a strict subset for this status schema: require exactly one YAML document and at most one stream-initial BOM; reject non-YAML-printable source characters; require YAML block-mapping separation for ordinary entries; decode plain, single-quoted, and YAML double-quoted scalar keys/values without Unicode-whitespace normalization; normalize before uniqueness checks; and reject merge keys, tags, anchors, aliases, collections, duplicate mappings, and unsupported indentation inside the target mapping. At top level, accept only recognized document markers/direct mapping entries and the explicitly supported scalar or empty-block value subset; reject malformed/unsupported flow values and unrecognized syntax; reject a second marker even when indented or carrying inline content; resolve node properties before target-key detection; detect normalized `development_status` declarations at any indentation and reject every alternate declaration or value shape; and allow unrelated aliases whose anchors resolve outside the target. Preserve the bounded retired-key diagnostic for the requested whitespace/value spellings even when their value syntax is malformed, and never reflect an attacker-controlled key in an unbounded diagnostic.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests.RequiredSprintStatusMustBeUnique -noColor` -- expected: 9/9 pass, including retired Story 4.8 rejection.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests.PreReviewCandidateInputsPassInIsolation -noColor` -- expected: isolated candidate pre-review passes; direct final-tree pre-review is not used because its lifecycle is intentionally final.
- `python3 tools/validate-oq8-platform-evidence.py --lifecycle-mode final && python3 tools/validate-oq8-platform-evidence.py` -- expected: final lifecycle and reviewed packet pass.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- expected: all discovered tests pass with zero skips.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests --no-build --configuration Release --logger "trx;LogFileName=Hexalith.EventStore.Contracts.Tests.trx" --results-directory "TestResults/Hexalith.EventStore.Contracts.Tests" --collect:"XPlat Code Coverage"` -- expected: CI-parity Contracts gate passes.
- `git diff --check` -- expected: no whitespace errors.

## Suggested Review Order

**Lifecycle validator**

- Structure-aware `development_status` parse replaces the stale Story 4.8 backlog regex.
  [`validate-oq8-platform-evidence.py:1658`](../../tools/validate-oq8-platform-evidence.py#L1658)

- Retired Story 4.8 key is a positive absence invariant over that same mapping.
  [`validate-oq8-platform-evidence.py:1703`](../../tools/validate-oq8-platform-evidence.py#L1703)

- Active Epic 4 statuses remain exact/unique; 4.8 row is no longer required.
  [`validate-oq8-platform-evidence.py:1739`](../../tools/validate-oq8-platform-evidence.py#L1739)

- Single-quoted YAML `''` escapes and non-ASCII leading whitespace fail closed.
  [`validate-oq8-platform-evidence.py:1576`](../../tools/validate-oq8-platform-evidence.py#L1576)

**Evidence reseal**

- Fresh architecture/security/test receipts bind the corrected subject digest.
  [`architecture.json:7`](evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/reviews/architecture.json#L7)

- Handoff receipts and outer packet identities follow the resealed chain.
  [`source-only-handoff.json:7`](evidence/story-4-15/4b0a7b1d3628a857f131cfbff99030714aefc747/source-only-handoff.json#L7)

- Outer platform evidence packet updated last after inner checksums.
  [`4-8-eventstore-oq8-platform-evidence.yaml:53`](4-8-eventstore-oq8-platform-evidence.yaml#L53)

**Regression tests**

- Nine lifecycle cases retained; retired key insertion rejects by name.
  [`Oq8PlatformClosureTests.cs:714`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L714)

- YAML-shape, scoping, duplicate-mapping, and deeper-indent boundaries.
  [`Oq8PlatformClosureTests.cs:821`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L821)

- Stale date mutations derive the prior day from the sealed fixture date.
  [`Oq8PlatformClosureTests.cs:2455`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L2455)
