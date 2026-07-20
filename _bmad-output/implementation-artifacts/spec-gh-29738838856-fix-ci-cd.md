---
title: 'Fix live-sidecar CI: pull PostgreSQL image and close startup-readiness race'
type: 'bugfix'
created: '2026-07-20'
status: 'done'
review_loop_iteration: 0
route: 'one-shot'
baseline_commit: 'fcbb233e0d635550c6befd2b2e99e1105007374f'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI run `29738838856` (`Integration Tests` / `live-sidecar` job) failed because `Oq8PostgresqlFixture.VerifyPrerequisitesAsync()` calls `docker image inspect postgres:17.6` and fails fast (fail-closed by design, no implicit pull) — but `.github/workflows/integration.yml` never pulled that image, so every run hit "No such image: postgres:17.6". While fixing this, the human asked to move to the latest PostgreSQL release; bumping to `postgres:18.4` then exposed a pre-existing but now much more frequent startup race in the fixture's readiness check.

**Approach:** Add a retried `docker pull` step to `integration.yml` before the test run, bump both the workflow and `Oq8PostgresqlFixture.PostgresImage` to `postgres:18.4`, and change the fixture's readiness probe from the default Unix-socket `pg_isready` to an explicit TCP (`-h 127.0.0.1 -p 5432`) check — the Unix socket becomes ready before the TCP listener does, and Docker's published port completes the client handshake and then resets it during that gap, surfacing as a Dapr "connection reset by peer" state-store init failure.

## Boundaries & Constraints

**Always:** Keep the workflow's `postgres` tag and `Oq8PostgresqlFixture.PostgresImage` identical; validate the fix by actually running the live-sidecar suite (not just inspecting the YAML); use the repo's existing `nick-fields/retry` pin (matching `dapr-init`) for the new pull step.

**Ask First:** Any change to `Oq8PostgresqlFixture`'s container orchestration beyond the readiness-check fix (e.g. switching to GitHub Actions `services:`), any digest pinning, dependabot ecosystem changes, or caching — all raised by review and deferred rather than bundled here.

**Never:** Weaken the fixture's fail-closed `VerifyPrerequisitesAsync` prerequisite check; modify submodule (`references/Hexalith.Builds`) files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Fresh GitHub Actions runner, image not cached | `docker image inspect postgres:18.4` would fail | New retried `docker pull postgres:18.4` step succeeds before tests run | Retry (3x, 15s wait) absorbs transient registry hiccups |
| Postgres container just started | Unix socket accepting, TCP listener not yet bound | `pg_isready -h 127.0.0.1 -p 5432` reports not-ready and the fixture retries (500ms, up to 60 attempts) | Fixture surfaces `InvalidOperationException` with last error if never ready |
| Postgres TCP listener bound | `pg_isready -h 127.0.0.1 -p 5432` accepting | Fixture proceeds to start hosts/sidecars against a stable connection | N/A |

</frozen-after-approval>

## Code Map

- `.github/workflows/integration.yml` -- adds the retried `docker pull postgres:18.4` step ahead of the live-sidecar test run.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` -- bumps `PostgresImage` to `postgres:18.4` and switches the readiness probe to an explicit TCP check.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- records follow-up hardening (tag-sync guardrail test, dependabot docker ecosystem, digest pinning, caching, `services:` redesign) raised by review and explicitly deferred by the human.

## Tasks & Acceptance

**Execution:**
- [x] `.github/workflows/integration.yml` -- add a `nick-fields/retry`-wrapped `docker pull postgres:18.4` step between "Initialize Dapr" and "Run Live-Sidecar Integration Tests" -- the fixture's own prerequisite check never pulls, only inspects.
- [x] `Oq8PostgresqlFixture.cs` -- bump `PostgresImage` to `postgres:18.4` -- move to the latest PostgreSQL release per human direction.
- [x] `Oq8PostgresqlFixture.cs` -- change the `pg_isready` prerequisite probe to `-h 127.0.0.1 -p 5432` -- close the Unix-socket-vs-TCP-listener startup race exposed by the version bump.
- [x] `deferred-work.md` -- log the five review-raised hardening items the human chose to defer rather than bundle into this fix.

**Acceptance Criteria:**
- Given a runner without `postgres:18.4` cached, when the `live-sidecar` job runs, then the new pull step succeeds and `Oq8PostgresqlFixture.VerifyPrerequisitesAsync()` no longer fails with "No such image".
- Given the PostgreSQL container has just started, when `IdempotencyAdmissionOq8PostgresqlTests` runs, then the fixture waits for the TCP listener (not just the Unix socket) before starting the EventStore hosts/sidecars.
- Given the full `Hexalith.EventStore.Server.LiveSidecar.Tests` suite, when run repeatedly, then all 48 tests pass consistently (verified 8/8 isolated runs plus 2 consecutive full-suite 48/48 runs after the fix, versus 1/5–4/5 failures before it).

## Spec Change Log

## Design Notes

The readiness-race root cause was confirmed empirically: instrumenting a manual `postgres:18.4` container start showed the Unix-socket `pg_isready` reporting ready at ~0.7s while the TCP listener (`-h 127.0.0.1 -p 5432`) only became ready at ~1.3s; a raw host-side TCP connect succeeded throughout, meaning Docker's published-port proxy accepts the client handshake immediately and resets it once it discovers the backend isn't listening yet -- exactly matching Dapr's "connection reset by peer" failure. Checking the TCP listener directly closes this gap without adding a debounce/stability loop.

## Verification

**Commands:**
- `actionlint .github/workflows/integration.yml` -- expected: no findings.
- `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release --no-restore -p:UseHexalithProjectReferences=false -warnaserror -m:1` -- expected: zero warnings and errors.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release --no-restore -p:UseHexalithProjectReferences=false` -- expected: 48/48 passing, repeatably.

**Results (2026-07-20):**
- `actionlint` passed with no findings on the updated workflow.
- Focused Release build passed with zero warnings/errors (`-warnaserror`).
- `IdempotencyAdmissionOq8PostgresqlTests.ConcurrentFirstWriters_HostFailover_PreserveOneFencedExecutionAndExactReplay` passed 8/8 in isolation after the TCP-readiness fix (vs. 1/5 passing with `postgres:18.4` and the original Unix-socket check).
- Full `Hexalith.EventStore.Server.LiveSidecar.Tests` suite passed 48/48 twice consecutively after the fix.

## Suggested Review Order

**CI image availability (the reported failure)**

- The missing pull was the direct cause of run `29738838856`'s failure; retry wrapper matches the existing `dapr-init` convention.
  [`integration.yml:64`](../../.github/workflows/integration.yml#L64)

**PostgreSQL version bump**

- Moved to the latest PostgreSQL release per explicit human direction, verified against the same digest as the `latest` tag.
  [`Oq8PostgresqlFixture.cs:28`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs#L28)

**Startup-readiness race (regression surfaced by the bump)**

- Root-cause fix for the flake the version bump made far more frequent; see Design Notes for the empirical timing evidence.
  [`Oq8PostgresqlFixture.cs:141`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs#L141)

**Review follow-up**

- Five hardening items raised by adversarial review (tag-sync guardrail, dependabot docker ecosystem, digest pinning, caching, `services:` redesign) explicitly deferred by the human.
  [`deferred-work.md:3`](deferred-work.md#L3)
