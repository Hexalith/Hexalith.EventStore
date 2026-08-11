---
title: 'Make CI time-hermetic and remove duplicate evidence validation'
type: 'bugfix'
created: '2026-08-11'
status: 'done'
baseline_commit: 'f14b5cd6dbf4e951ef3d0d956b16e1ef472bb781'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** [CI run 31493702634](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31493702634) fails because a Server test combines a fixed `2026-08-11T08:00Z` expiry with the system clock; production correctly returns `Expired` after that instant while the test permanently expects `Exact`. CI history also shows avoidable duplicate historical-evidence validation in the non-release-blocking integration lane, contributing red runs after fresh live evidence already passed.

**Approach:** Inject the existing fake-clock seam so the test executes at a fixed instant inside its retention window. Keep fresh OQ8 capture validation in Integration Tests, remove its redundant second validation of committed historical evidence (already covered by blocking Tier-1 closure tests), and document the resulting boundary.

## Boundaries & Constraints

**Always:** Preserve inclusive production expiry (`ExpiresAt <= now` is expired), the exact-to-redirected migration assertions, the blocking Release/package-mode build, package-consumer validation, product unit tests, provider verification, and fresh OQ8 capture validation. Keep the workflow and CI documentation consistent.

**Ask First:** Retiering or deleting Tier-1 evidence/meta-contract tests; changing shared `Hexalith.Builds`, release, security, package-publication, or sealed evidence artifacts; committing or pushing.

**Never:** Use `UtcNow`, extend the fixture expiry, skip/remove the failing test, weaken production expiry behavior, remove fresh live-evidence validation, or modify submodules. Do not fold the broader evidence-governance redesign into this fix.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Exact legacy source | Fake time is strictly between `processedAt` and `expiresAt` | Inspection is `Exact`, redirect succeeds, and reinspection returns the redirect without domain work | Any clock-dependent result fails the focused test |
| Expired legacy source | Runtime time is at or after expiry | Production continues to classify it as `Expired` | Existing expiry coverage remains unchanged |
| Fresh integration evidence | Live OQ8 capture and support results are produced | Capture-aware validation runs once and the artifact is uploaded | Missing or invalid fresh evidence still fails the lane |
| Committed historical evidence | Ordinary push/PR CI | Tier-1 closure tests remain the blocking authority; Integration Tests do not duplicate that current-checkout validation | Drift continues to fail the authoritative Tier-1 gate |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorFencingTests.cs:151-218` -- failing fixture; declare its fixed timestamps before actor creation and pass `FakeTimeProvider` at an instant inside the retention window.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs:57-84` -- existing `TimeProvider` injection seam; reuse unchanged.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:60,94,170-174` and `IdempotencyChecker.cs:270-273` -- production clock and inclusive expiry semantics; read-only.
- `.github/workflows/integration.yml:83-123` -- fresh OQ8 capture/support validation followed by a redundant argument-less committed validator call; retain the former and remove the latter.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- workflow ownership and fail-closed guardrails for the single fresh validator, shallow checkout, and retained blocking closure owner.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- retained Tier-1 committed-evidence authority; read-only.
- `.github/workflows/ci.yml:18-41` -- essential blocking build, consumer, and deterministic test wiring; read-only.
- `docs/ci.md` -- describe that Integration Tests validate fresh capture while Tier 1 owns committed historical closure.

## Tasks & Acceptance

**Execution:**
- [x] `AggregateActorFencingTests.cs` -- inject `FakeTimeProvider` into the exact-source fixture so calendar passage cannot alter the expected decision.
- [x] `.github/workflows/integration.yml` -- remove only the duplicate argument-less committed-evidence validator invocation.
- [x] `docs/ci.md` -- document the single-owner split between fresh integration evidence and committed Tier-1 closure.

**Acceptance Criteria:**
- Given the real wall clock is after the fixture expiry, when the focused exact-source test runs, then it still passes through the injected fixed clock and production code is unchanged.
- Given Integration Tests produce a valid fresh OQ8 capture, when validation runs, then capture-aware validation remains blocking and committed closure is not revalidated a second time in that workflow.
- Given ordinary CI runs, when deterministic tests and package validation execute, then all existing blocking product, provider, package, and committed-evidence gates remain enabled.

## Spec Change Log

## Design Notes

Across the last 30 CI runs, 14 failed and 7 were expected concurrency cancellations. Ten failures came from evidence/workflow meta-contract drift, two caught useful provider portability defects, and two were this clock time bomb. This change removes a demonstrably duplicate integration check without weakening the authoritative gate. Moving exhaustive evidence mutation suites or the Debug Tenants source-mode lane to advisory/path-scoped execution may have ROI, but needs a separate architecture decision because it changes blocking authority.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: clean Release build.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method Hexalith.EventStore.Server.Tests.Actors.AggregateActorFencingTests.LegacySource_ExactInspectionAndRedirectRetainOriginalEvidenceAndDoNoDomainWork -noColor` -- expected: one passing test.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Actors.AggregateActorFencingTests -noColor` -- expected: class passes.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: clean Release build.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests -noColor` -- expected: workflow guardrails pass.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests.FreshAndCommittedRuntimeModesRemainExact -noColor` -- expected: fresh capture accepts only its exact runtime and fails closed on mode drift.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests.ApprovedSourceOnlyHandoffPasses -noColor` -- expected: retained committed closure passes.
- `git diff --check` -- expected: no whitespace errors.

## Suggested Review Order

**Validation ownership**

- Keep fresh capture validation blocking while removing duplicate historical closure.
  [`integration.yml:118`](../../.github/workflows/integration.yml#L118)

- Enforce one fail-closed validator and retain the blocking Tier-1 owner.
  [`ReleasePackageManifestTests.cs:632`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L632)

- Explain the fresh-versus-committed validation ownership split.
  [`ci.md:56`](../../docs/ci.md#L56)

**Hermetic expiry behavior**

- Pin the exact-source scenario inside its retention window.
  [`AggregateActorFencingTests.cs:153`](../../tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorFencingTests.cs#L153)

- Prove inclusive expiry both at and after the boundary.
  [`AggregateActorFencingTests.cs:224`](../../tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorFencingTests.cs#L224)

**Checkout efficiency**

- Fetch only the commit required by fresh capture validation.
  [`integration.yml:36`](../../.github/workflows/integration.yml#L36)

- Guard against restoring obsolete full-history checkout constraints.
  [`ReleasePackageManifestTests.cs:683`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L683)

- Document why shallow history is now sufficient.
  [`ci.md:69`](../../docs/ci.md#L69)
