---
created: 2026-07-05
source_story_key: TEST-1-1-generated-api-smoke-preflight
source_epic: "TEST-1 - Test And CI Recovery"
source_action: "Epic D retrospective action item 4"
source_files:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-generated-api-smoke-preflight.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - docs/brownfield/development-guide.md
  - docs/brownfield/integration-architecture.md
  - docs/guides/troubleshooting-dapr-actor-placement.md
---

# Story TEST-1.1: Generated API DAPR/Aspire Smoke Preflight

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **developer validating generated API proofs**,
I want **a local DAPR/Aspire smoke preflight that reports environment readiness, sidecar state, and generated API endpoints**,
so that **runtime blockers are classified support-safely before they are accepted as evidence against generated REST behavior**.

## Story Context

This story is the Developer follow-through for Epic D retrospective action item 4. Epic D delivered generated REST controllers, Sample and Tenants external API proofs, and guardrails that keep generated controllers out of interactive UI hosts. The remaining validation gap is local runtime evidence.

D5 ultimately produced strong Sample runtime evidence: generated `sample-api` endpoint, `200` plus ETag, `304`, and Redis persisted end-state after a Counter increment. D6 could not run its Aspire smoke because local placement and scheduler binaries were missing. D7 proved generated Tenants API behavior in integration tests, but also exposed endpoint wait and DAPR actor-placement diagnostic issues.

The preflight must not replace generator tests or integration tests. It is a local diagnostic gate that runs before a developer records "Aspire smoke blocked" or treats a generated API endpoint failure as a product defect.

Source of truth:

- `_bmad-output/implementation-artifacts/deferred-work.md` top entry dated 2026-07-05.
- `_bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md` action item 4 and readiness assessment.
- `_bmad-output/planning-artifacts/architecture.md` AD-9 and AD-12.
- `docs/brownfield/development-guide.md` local topology and VM bootstrap.
- `docs/guides/troubleshooting-dapr-actor-placement.md` actor-placement diagnostics.
- `tests/Hexalith.EventStore.Testing.Integration.Tests/DaprTestPrerequisiteDiagnosticsTests.cs` support-safe diagnostics baseline.

## Acceptance Criteria

1. **Preflight entry point is explicit and local.**
   - Add a local helper under `scripts/`, for example `scripts/generated-api-smoke-preflight.sh`.
   - The default mode is read-only: it checks prerequisites and a running topology but does not start Docker containers, placement, scheduler, or Aspire.
   - Any mode that starts placement, scheduler, or Aspire requires an explicit flag and documents the exact command it runs.
   - The helper works from the repository root and does not use `.sln` files or recursive submodule commands.
   - Tenants-specific checks are optional and run only when the Tenants submodule/API host is available.

2. **Environment prerequisite checks classify infrastructure blockers before product checks.**
   - Check Docker daemon access.
   - Check `aspire` CLI availability.
   - Check DAPR CLI/runtime availability and local runtime binaries needed by this repo: `daprd`, `placement`, and `scheduler`.
   - Check whether placement and scheduler are reachable on the local ports expected by the AppHost defaults or configured environment.
   - When a prerequisite is missing, report `blocked` with an actionable support-safe message and the minimal bootstrap command from repo docs.
   - Do not continue to generated API product checks after a required infrastructure blocker.

3. **Aspire topology discovery reports generated API resources and endpoints.**
   - Use Aspire CLI diagnostics, preferably `aspire describe`, to discover resource state, endpoint URLs, and environment values.
   - Report at least `eventstore`, `sample-api`, Sample domain service, Redis/statestore, and their DAPR sidecars when present.
   - Report `tenants-api` only when present; absence of Tenants must be `not-applicable`, not failure.
   - Prefer HTTP URLs for local VM smoke calls when both HTTP and HTTPS endpoints exist.
   - Fail support-safely if a required generated API resource is running but has no published HTTP endpoint.

4. **DAPR sidecar diagnostics are captured before generated API calls.**
   - For EventStore and generated API sidecars, report DAPR HTTP endpoint or port, app id, and metadata availability.
   - Query sidecar metadata where possible and report EventStore actor runtime readiness: `hostReady` and placement connection state.
   - Distinguish sidecar missing, sidecar unhealthy, placement disconnected, scheduler unavailable, and access-control denied.
   - Do not print DAPR API tokens, bearer tokens, JWT payloads, connection strings, private network addresses, raw EventStore payloads, or raw exception stacks.

5. **Sample generated API smoke is optional but complete when requested.**
   - Add an explicit option such as `--sample-api-smoke` or `--run-smoke` for HTTP calls against the generated `sample-api` surface.
   - In `EnableKeycloak=false` local mode, use a dev JWT with issuer `hexalith-dev`, audience `hexalith-eventstore`, tenant `tenant-a`, and command/query permissions without printing the token.
   - Exercise the generated Sample command endpoint when available: `POST /api/tenant-a/counter/counter-1/increment` with a matching body.
   - Exercise the generated Sample query endpoint: `GET /api/tenant-a/counter/counter-1`.
   - Verify accepted command behavior (`202`, `Location`, `Retry-After`) and query cache behavior (`200` with ETag, then `304` with `If-None-Match`) when the smoke path is run.
   - If the generated command endpoint is not available, report that as a generated API product failure, not an environment blocker.

6. **Smoke evidence includes persisted or read-model evidence when available.**
   - A successful smoke must not rely only on `202`, `200`, or `304`.
   - If Redis/statestore endpoint details are available, verify the relevant Counter stream/projection/read-model end-state changed or remained consistent with the observed response.
   - If state evidence cannot be read locally, report `state-evidence-unavailable` and do not label the smoke as full evidence.
   - Persisted evidence output must stay bounded and must not dump raw event payloads, raw EventStore metadata, connection strings, or secrets.

7. **Output is machine-readable enough for story records and support-safe enough for logs.**
   - Print a concise human summary with categories: `environment`, `aspire`, `dapr`, `generated-api`, `state-evidence`, and `next-action`.
   - Provide an optional `--json` output mode for Dev Agent Records and future automation.
   - Exit with distinct status categories for success, blocked environment, topology not running, generated API failure, and state-evidence failure.
   - Redact bearer tokens, compact JWTs, DAPR API tokens, passwords, connection strings, non-local private addresses, real issuer URLs, emails, concrete tenant IDs beyond known dev fixtures, and raw exception traces.

8. **Focused tests protect diagnostics and script behavior.**
   - Add tests for prerequisite classification and support-safe redaction, reusing `Hexalith.EventStore.Testing.Integration` helpers when that reduces duplication.
   - Add tests or shell validation for script argument parsing and dry-run behavior.
   - Add AppHost/topology tests only if the implementation encodes resource names or launch-profile assumptions.
   - Keep tests using xUnit v3 and Shouldly; do not use solution-level `dotnet test`.

9. **Verification proves the story without requiring every local dependency to be present.**
   - Run:
     ```bash
     bash -n scripts/generated-api-smoke-preflight.sh
     dotnet test tests/Hexalith.EventStore.Testing.Integration.Tests/
     dotnet test tests/Hexalith.EventStore.AppHost.Tests/
     dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false
     ```
   - If the implementation changes only scripts and no AppHost assumptions, document why `AppHost.Tests` is not required.
   - Optionally run the live smoke when Docker, DAPR placement/scheduler, and Aspire are available:
     ```bash
     EnableKeycloak=false scripts/generated-api-smoke-preflight.sh --sample-api-smoke
     ```
   - Record exact blockers with support-safe diagnostics when live smoke cannot run.

## Tasks / Subtasks

- [ ] **Task 1: Preflight current evidence and tool shape** (AC: 1, 2, 7)
  - [ ] Read `deferred-work.md`, Epic D retrospective action item 4, D5/D6/D7 smoke evidence, and local topology docs.
  - [ ] Decide whether shell-only implementation is enough or whether reusable C# diagnostics belong in `Hexalith.EventStore.Testing.Integration`.
  - [ ] Keep the default mode read-only and avoid persistent containers by default.

- [ ] **Task 2: Implement environment and topology checks** (AC: 2, 3)
  - [ ] Check Docker, Aspire CLI, DAPR CLI/runtime, `daprd`, `placement`, and `scheduler`.
  - [ ] Detect placement/scheduler reachability on configured/default local ports.
  - [ ] Parse Aspire resource state and endpoint data.
  - [ ] Classify missing prerequisites separately from generated API failures.

- [ ] **Task 3: Implement DAPR sidecar diagnostics** (AC: 4)
  - [ ] Query sidecar metadata where endpoints are available.
  - [ ] Report app id, DAPR HTTP endpoint/port, metadata availability, and EventStore actor placement readiness.
  - [ ] Distinguish missing sidecar, unhealthy sidecar, placement disconnected, scheduler unavailable, and access-control denial.

- [ ] **Task 4: Implement optional Sample generated API smoke** (AC: 5, 6)
  - [ ] Generate or obtain a local dev JWT without printing it.
  - [ ] Call the generated Sample command endpoint and query endpoint.
  - [ ] Verify `202`, `Location`, `Retry-After`, `200` with ETag, and `304`.
  - [ ] Read persisted/read-model evidence when Redis/statestore is discoverable.

- [ ] **Task 5: Make diagnostics support-safe and scriptable** (AC: 7, 8)
  - [ ] Add human output and optional JSON output.
  - [ ] Add redaction for tokens, JWTs, secrets, private addresses, URLs, emails, IDs, and raw traces.
  - [ ] Add focused tests for redaction and classification.
  - [ ] Add script argument/dry-run validation.

- [ ] **Task 6: Document usage and verify** (AC: 9)
  - [ ] Update local development or troubleshooting docs with the new preflight command.
  - [ ] Run focused tests and Release build.
  - [ ] Run live smoke when prerequisites exist, or record exact support-safe blockers.
  - [ ] Update the Dev Agent Record with evidence and remaining action-item status.

## Dev Notes

### Top Guardrails

1. **Do not make the helper mutate topology by default.** Read-only diagnostics are the default. Starting control-plane processes or Aspire requires an explicit flag.
2. **Do not weaken evidence rules.** A successful generated API smoke needs persisted/read-model evidence when available; HTTP status alone is not full proof.
3. **Do not print secrets.** Tokens, JWTs, DAPR API tokens, connection strings, private addresses, raw payloads, raw metadata, and stack traces must be redacted.
4. **Do not edit submodules.** Tenants checks are optional and conditional on the submodule state.
5. **Do not use `.sln` or solution-level `dotnet test`.** Use `Hexalith.EventStore.slnx` for restore/build and per-project tests.

### Current Code State Read During Story Creation

| File | Current state | Story change | Preserve |
| --- | --- | --- | --- |
| `scripts/ci-local.sh` | Existing local script convention with strict Bash settings and explicit tier flags. | Use the same plain local-helper style for the preflight script. | Clear usage text and non-recursive repo behavior. |
| `src/Hexalith.EventStore.AppHost/PrerequisiteValidator.cs` | Checks Docker and DAPR CLI/runtime before AppHost startup. | Do not duplicate blindly; either reuse concepts or keep the new helper focused on generated API smoke readiness. | Existing AppHost prerequisite behavior. |
| `src/Hexalith.EventStore.Testing.Integration/DaprDiagnostics.cs` and related helpers | Existing integration diagnostic helpers and support-safe classification baseline. | Reuse or extend if the preflight needs C# helper logic. | Support-safe diagnostics and redaction. |
| `tests/Hexalith.EventStore.Testing.Integration.Tests/DaprTestPrerequisiteDiagnosticsTests.cs` | Tests prerequisite messages and redaction. | Add preflight-specific diagnostic tests here or nearby. | Shouldly assertions and no raw secrets in diagnostics. |
| `docs/brownfield/development-guide.md` | Documents DAPR placement/scheduler bootstrap and `EnableKeycloak=false aspire run`. | Add usage after the preflight exists. | Existing VM/cloud gotcha guidance. |
| `docs/guides/troubleshooting-dapr-actor-placement.md` | Explains actor placement diagnostics via DAPR sidecar metadata. | Link or reuse as explanation for placement failures. | Distinction between environment/control-plane conditions and product defects. |
| `samples/Hexalith.EventStore.Sample.Api` | Dedicated generated external API host for Sample. | Use as the primary generated API smoke target. | External API host stays separate from Sample UI. |

### Implementation Hints

- Prefer a minimal Bash front end if it can reliably orchestrate local commands, but keep complex redaction/classification in C# if shell string handling becomes brittle.
- Prefer `aspire describe` for endpoint discovery rather than hard-coding dynamic ports. Fall back to documented defaults only when describe output is unavailable and the user supplied endpoints explicitly.
- Keep generated API product failures distinct from environment blockers. Example: missing placement is `blocked`; `sample-api` route returns 404 after the resource is healthy is a product failure.
- Use known dev fixture values (`tenant-a`, `counter-1`, `hexalith-dev`, `hexalith-eventstore`) for smoke. Do not emit the generated JWT or decoded claims.
- For state evidence, prefer bounded summaries such as stream sequence, projection version, and key category. Avoid dumping raw event bodies.

### References

- [Source: `_bmad-output/implementation-artifacts/deferred-work.md`] - preflight deferred-work entry.
- [Source: `_bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md`] - action item 4 and evidence-quality lessons.
- [Source: `_bmad-output/planning-artifacts/architecture.md` AD-9, AD-12] - topology alignment and persisted-evidence rules.
- [Source: `docs/brownfield/development-guide.md`] - local Aspire/DAPR bootstrap.
- [Source: `docs/guides/troubleshooting-dapr-actor-placement.md`] - actor-placement metadata diagnostics.
- [Source: `tests/Hexalith.EventStore.Testing.Integration.Tests/DaprTestPrerequisiteDiagnosticsTests.cs`] - support-safe diagnostics test baseline.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

