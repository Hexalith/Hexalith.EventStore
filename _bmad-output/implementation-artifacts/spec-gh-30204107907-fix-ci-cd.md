---
title: 'Fix stale CI governance expectations after approved changes'
type: 'bugfix'
created: '2026-07-26'
status: 'done'
baseline_commit: '15604a64344fc31e3cdc004fbb1e5744266692dd'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `30204107907` fails the Tier 1 Contracts lane because three governance assertions still describe states that were intentionally superseded: the earlier Hexalith.Builds release pin and the pre-authorization Story 1.20 proof inputs. Current-main run `30205134514` reproduces the same three failures while restore, Release build, package-consumer validation, and the other CI jobs pass.

**Approach:** Align the governance tests with the approved repository state. Preserve the intentionally re-pinned release workflow and authorized Story 1.20 closure, while retaining assertions that the frozen proof packet still contains the exact fail-closed transforms that produced that closure.

## Boundaries & Constraints

**Always:** Keep the change within the two failing Contracts test files; retain exact immutable SHA equality and release/development gitlink separation; assert the finalized follow-up flag and sprint-status closure as postconditions; preserve the packet assertions for front-matter scoping and exact indented blocker-to-completion transformation.

**Ask First:** Any discovery that the `f75daebd4c522c081a6f62e274cf25e07971de69` release pin is invalid, that Story 1.20 closure was not authorized, or that production workflow/proof artifacts must change rather than their stale test expectations.

**Never:** Revert the release caller to `cf04c419378dfe1bd3c41a9244b5e3283092056e`; restore `followup_review_recommended: true` or the removed Story 1.20 blocker comments; weaken immutable-pin, package-count, proof-transform, or closure-state coverage; modify submodules or unrelated existing worktree changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Release governance | Caller and execution mapping both use approved `f75daebd…`; development gitlink differs | Exact-pin and one-mapping assertions pass | Any mismatch or use of a moving ref fails closed |
| Authorized follow-up | Spec front matter contains one `false` and no `true` recommendation | Closure postcondition passes while both packet transforms remain scoped to front matter | Duplicate, unresolved, or missing flag fails |
| Authorized sprint | Sprint status contains the two completion comments and no old blocker boundaries | Closure postcondition passes while the packet retains exact indented source/emission patterns | Mixed, reverted, or malformed state fails |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- owns the approved immutable Hexalith.Builds release SHA and validates its workflow mapping.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs` -- validates the frozen proof transforms and their current authorized source-state postconditions.
- `.github/workflows/release.yml` -- authoritative intentional `f75daebd…` caller/execution pin and `expected-package-count: 14`; inspection only.
- `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md` -- authoritative finalized follow-up flag; inspection only.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- authoritative Story 1.20 completion state; inspection only.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- replace the obsolete approved release SHA expectation with the intentional `f75daebd…` pin so the guard matches the fail-closed release caller.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs` -- assert one resolved front-matter flag and no unresolved flag; assert completed sprint comments are present and obsolete blocker boundaries absent, without removing packet-transform integrity checks.

**Acceptance Criteria:**
- Given current `main`, when the three tests that failed in run `30204107907` execute, then all three pass without changing workflow or proof artifacts.
- Given the Contracts test project in Release/package mode, when its full test assembly executes, then all 765 tests pass with zero failures and zero skips.
- Given the focused project build, when warnings are treated as errors, then it completes with zero warnings and zero errors.

## Spec Change Log

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -warnaserror -m:1` -- expected: zero warnings and zero errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method '*ReleaseCallerPinsSharedExecutionAndOneMappingWithoutCommentAuthority'` -- expected: one passing test.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method '*PacketFollowupSpecTransformResolvesFrontMatterFlagOnly'` -- expected: one passing test.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method '*PacketAuthorizingSprintTransformMatchesNestedBlockerComments'` -- expected: one passing test.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll` -- expected: the complete Contracts assembly passes with zero failures and zero skips.

## Suggested Review Order

**Immutable release authority**

- Bind governance to the intentional fail-closed shared release revision.
  [`ContainerPublishingGovernanceTests.cs:12`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L12)

**Authorized proof closure**

- Require one resolved recommendation key while preserving both frozen transforms.
  [`ProofPacketValidatorIntegrityTests.cs:143`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs#L143)

- Bind completion comments, Epic 1, and Story 1.20 into one exact closure block.
  [`ProofPacketValidatorIntegrityTests.cs:246`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs#L246)

**Review follow-up**

- Record stale operator SHA guidance without expanding this repair's approved boundary.
  [`deferred-work.md:570`](deferred-work.md#L570)
