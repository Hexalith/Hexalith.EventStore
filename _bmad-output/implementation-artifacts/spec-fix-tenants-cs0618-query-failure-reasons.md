---
title: 'Fix Tenants obsolete query failure reason references'
type: 'bugfix'
created: '2026-10-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Four obsolete query failure reason references in the root-declared Tenants dependency fail the EventStore IntegrationTests source-reference build with CS0618 under warnings-as-errors.

**Approach:** In `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/TenantReadAuthorityQueryHandler.cs`, replace all three `QueryAdapterFailureReason.ActorException` references with `QueryAdapterFailureReason.QueryExecutionFailure`. In the adjacent `GetTenantAuditQueryHandler.cs`, replace `QueryAdapterFailureReason.ActorResponseMismatch` with `QueryAdapterFailureReason.ResponseMismatch`. Preserve existing error strings, behavior, warnings-as-errors, dependencies, and unrelated work. Build the Tenants host in Debug with `UseHexalithProjectReferences=true`, run the existing tests for both handlers, then rerun the complete EventStore IntegrationTests source-reference build. Report exact commands, results, and remaining blockers. Do not commit, push, or mark Story 2.13 complete.

</frozen-after-approval>

## Implementation Notes

- Investigation: both obsolete symbols alias the requested replacements in EventStore's `QueryAdapterFailureReason`; the wire values remain `actor-exception` and `actor-response-mismatch`. The owning Tenants checkout is clean on `main`; existing EventStore changes are outside this repair and must be preserved.
- Validation targets: Tenants `src/Hexalith.Tenants/Hexalith.Tenants.csproj`; existing classes `Hexalith.Tenants.Server.Tests.Queries.TenantReadAuthorityQueryHandlerTests` and `Hexalith.Tenants.Server.Tests.Queries.GetTenantAuditQueryHandlerTests` in `tests/Hexalith.Tenants.Server.Tests`; EventStore `tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj`. Build Debug with `-m:1 -p:UseHexalithProjectReferences=true`; run the built xUnit v3 executable with class filters. Do not disable project-reference builds in the final gate.
- This is a standalone build repair. Story 2.13 artifacts and sprint status are context only and will not be modified.
- Implemented exactly four symbolic replacements in the two requested Tenants handlers. Byte comparisons against their owning repository's HEAD confirm every other byte is unchanged; `git diff --check` passes. No warning configuration or dependency files changed.
- From `references/Hexalith.Tenants`: `dotnet build src/Hexalith.Tenants/Hexalith.Tenants.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true` passed with 0 warnings and 0 errors (38.57 seconds).
- From `references/Hexalith.Tenants`: `dotnet build tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true` passed with 0 warnings and 0 errors (41.18 seconds).
- From `references/Hexalith.Tenants`: `tests/Hexalith.Tenants.Server.Tests/bin/Debug/net10.0/Hexalith.Tenants.Server.Tests -class '*TenantReadAuthorityQueryHandlerTests' -class '*GetTenantAuditQueryHandlerTests'` passed: 285 tests, 0 errors, 0 failed, 0 skipped, 0 not run (0.391 seconds).
- From the EventStore root: `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true` passed with 0 warnings and 0 errors (54.52 seconds). The complete dependency graph compiled, including Tenants, Tenants.Api, and EventStore.AppHost; no `BuildProjectReferences=false` fallback or warning suppression was used. This clears the four-CS0618 build blocker recorded for Story 2.13. No remaining blockers were observed in the requested validation; other Story 2.13 qualification gates were not reassessed.
- Final scope: only the two requested Tenants source files and this standalone BMAD repair/validation record were changed by this task. Existing EventStore work, Story 2.13 artifacts, and sprint status were preserved. No commit or push was performed. This record's done status applies only to the four-reference repair, not Story 2.13.
- Validation stdout/stderr is retained in `/tmp/tenants-cs0618-20261025-crYsAd/` as `tenants-host-build.log`, `tenants-handler-tests-build.log`, `tenants-handler-tests.log`, and `eventstore-integration-build.log`. Commands are captured using `set -o pipefail` and `2>&1 | tee <log>` so a failing command remains a failure.

## Review Triage Log

- Low, rejected: the independent reviewer suggested new literal wire-reason assertions because existing native-prefix rejection tests assert only unsuccessful results. This is a pre-existing coverage improvement, with no demonstrated regression or unverified wire value: `ActorException` directly aliases `QueryExecutionFailure = "actor-exception"`, and `ActorResponseMismatch` directly aliases `ResponseMismatch = "actor-response-mismatch"`. New test scaffolding would exceed a simple correction for this mechanical rename; byte comparison and the existing handler tests verify the requested repair. No actionable review findings or deferred changes remain.
