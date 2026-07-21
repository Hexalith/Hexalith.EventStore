---
title: 'Resolve main rebase conflicts'
type: 'bugfix'
created: '2026-07-21'
status: 'draft'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture.md'
  - '{project-root}/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Local `main` contains two validated commits that cannot be pushed because `origin/main` advanced with a parallel fix touching the same domain-invocation resilience code, integration fixtures, tests, and Story 1.20 record. A mechanical side selection would discard stronger cancellation, persisted-state, or cleanup guarantees.

**Approach:** Rebase the local commits onto `origin/main`, reconcile each conflict by preserving the strongest compatible behavior from both lineages, verify the integrated implementation, and push the resulting linear history without force.

## Boundaries & Constraints

**Always:** Preserve caller cancellation as `OperationCanceledException`; classify only the configured invocation timeout as `DomainServiceException`; keep domain invocation free from host-default HTTP retry/timeout handlers; preserve durable persisted-state assertions, Redis ownership-safe restoration, final root-declared submodule pointers, and Story 1.20's fail-closed `blocked` state.

**Ask First:** Any resolution that changes public contracts, weakens a test or Story 1.20 gate, drops a local or remote capability, requires a merge commit, or cannot retain a linear non-force push.

**Never:** Force-push, initialize or update nested submodules, stack competing resilience pipelines, use real-time sleeps for timeout tests, authorize consumer migration, mark Story 1.20 or Epic 1 done, or overwrite unrelated user changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Configured timeout | Named domain call exceeds the validated timeout | One attempt is bounded by a `TimeProvider`-driven timeout | Log timeout context and throw `DomainServiceException` |
| Caller cancellation | Caller token is canceled before completion | Cancellation propagates unchanged | Do not misclassify or wrap it as timeout/failure |
| Host resilience defaults | Host configures default HTTP retries/timeouts | Domain named client contains no inherited `ResilienceHandler` | Other named clients retain their defaults |
| Shared Redis cleanup | Topology-owned keys existed before the test or changed ownership | Original value and TTL are restored only while ownership still matches | Aggregate cleanup failures without overwriting foreign state |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` and `DomainServiceHttpClientBuilderFilter.cs` -- named-client registration and deterministic removal of inherited resilience handlers.
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` and `DomainServiceOptions.cs` -- cancellation classification, bounded invocation, logging, and option documentation.
- `tests/Hexalith.EventStore.Server.Tests/` -- deterministic unit and host-registration coverage using `FakeTimeProvider`.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/` and `EventStore/` -- durable bootstrap/status evidence and conflict-contract coverage.
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/` and `Security/AspireTopologyFixture.cs` -- shared HTTP policy, DAPR endpoints, activation retry classification, and ownership-safe Redis restoration.
- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` -- additive conflict merge that must remain blocked.
- `references/Hexalith.Builds` and `references/Hexalith.Memories` -- final fast-forward gitlinks replayed by the second local commit.

## Tasks & Acceptance

**Execution:**
- [ ] Rebase `f3e036bf` onto `origin/main`; retain the remote builder-filter and `TimeProvider` implementation while preserving compatible local documentation and reusable fixture policies.
- [ ] Resolve test and fixture conflicts in favor of deterministic timeout tests, persisted DAPR evidence, required endpoint properties, and ownership-guarded Redis restoration; keep local helpers only when wired and covered.
- [ ] Merge the Story 1.20 narratives additively without changing its blocked status or historical evidence meaning.
- [ ] Replay `026b039b`, verify final Builds/Memories/Tenants gitlinks, and reword the pointer-only commit to the repository-required `build(deps)` type.
- [ ] Run focused builds/tests, conflict-marker and whitespace checks, inspect the final range, validate all rebased commit messages, then push `main` normally.

**Acceptance Criteria:**
- Given host-wide HTTP resilience defaults, when the domain invocation client is built, then only that named client excludes inherited resilience handlers and executes a single bounded attempt.
- Given configured timeout versus caller cancellation, when either token fires, then deterministic tests prove distinct exception classification and logging behavior.
- Given bootstrap, concurrency, activation, and topology-cleanup paths, when focused unit/integration tests run, then persisted identities and cleanup ownership guarantees remain covered.
- Given the completed rebase, when the final range is inspected, then it is linear over `origin/main`, contains no conflict markers, preserves Story 1.20 as blocked and the intended final gitlinks, passes commitlint, and pushes without force.

## Spec Change Log

## Design Notes

The two first commits are parallel implementations of the same review findings. Prefer the remote filter over the experimental order-sensitive removal extension, and the remote `TimeProvider` cancellation source over wall-clock `CancelAfter`. The local contract-test helpers survive only if the merged fixtures consume them; duplicate or orphaned resilience logic is removed.

## Verification

**Commands:**
- `git diff --check && ! rg -n '^(<<<<<<<|=======|>>>>>>>)' src tests _bmad-output` -- expected: no whitespace errors or conflict markers.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: succeeds without warnings/errors.
- Run the built Server.Tests assembly for configuration, invoker, and isolation classes, then the full assembly -- expected: all tests pass.
- Build IntegrationTests and run the non-topology resilience/policy/concurrency classes; run live DAPR/Aspire lanes when infrastructure is available -- expected: focused lanes pass or exact environmental blockers are reported.
- `npx --no-install commitlint --from origin/main --to HEAD --verbose` -- expected: every rebased commit passes.
- `git push origin main` -- expected: fast-forward push succeeds.
