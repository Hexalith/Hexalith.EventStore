---
title: 'Fix OQ8 lifecycle validation after Story 4.8 migration'
type: 'bugfix'
created: '2026-08-12'
status: 'done'
review_loop_iteration: 1
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
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- retain the existing nine lifecycle cases; add retired-key variants (quoted key, colon whitespace, alternate mapping indentation, empty/null/comment-only and quoted/complex values), near-match/out-of-section positives, and stale `createdOn`/`executedOn`/`reviewedOn` mutations that reach the intended date guards after resealing.
- [x] Story 4.15 evidence directory and `4-8-eventstore-oq8-platform-evidence.yaml` -- record corrected identities/counts, obtain fresh exact-subject reviews, then reseal inner and outer chains without changing capture evidence or authority.

**Acceptance Criteria:**
- Given the authoritative sprint status excludes Story 4.8, when all three OQ8 lifecycle modes run, then lifecycle validation passes and downstream invariants remain enforced.
- Given any Story 4.8 status row is introduced, when lifecycle validation runs, then it fails closed with a bounded error naming the retired key.
- Given the retired key is quoted, oddly spaced, empty/null, or assigned a scalar/collection value, when it is a direct `development_status` entry, then the same bounded retired-key error is returned; the same text outside that mapping and near-match keys do not trigger it.
- Given a required active status is missing, duplicated, or drifted, when validation runs, then the existing specific rejection remains intact.
- Given candidate subject/execution evidence or a final reviewer receipt carries the previous review date and its surrounding hashes are resealed, when validation runs, then the corresponding date-drift guard rejects it.
- Given changed validator bytes, when committed evidence is validated, then only a freshly reviewed, consistent binding chain passes.
- Given the corrected tree, when the focused OQ8 class and complete Contracts project run in Release, then all tests pass with zero build warnings or errors.

## Spec Change Log

- 2026-08-12 review loop 1: Adversarial edge-case and verification-gap review showed the text regex accepted quoted or null-valued Story 4.8 rows and that fresh-review dates lacked mutation coverage. The Code Map, tasks, acceptance criteria, and verification now require one structure-aware `development_status` parse, YAML-shape and scoping boundaries, and resealed stale-date mutations. This avoids semantically reintroducing the retired story or allowing future weakening of freshness guards to remain green. KEEP: positive Story 4.8 absence enforcement; bounded key-specific diagnostics; all nine lifecycle regression cases; exact active-status behavior; immutable Story 4.14 evidence and landed capability identity; three independent exact-subject approvals; unchanged external-authority exclusions.

## Design Notes

Treat Story 4.8 absence as a positive invariant over the parsed `development_status` mapping, not as a text-pattern omission. Active and retired-key rules must consume the same normalized direct-entry set. Reject merge keys and unsupported mapping structure rather than accepting an ambiguous semantic result.

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
