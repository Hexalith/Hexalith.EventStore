---
title: 'Story 5.4: Admin Surface Safety Hygiene'
type: 'feature'
created: '2026-09-07'
status: 'in-progress'
review_loop_iteration: 0
followup_review_recommended: true
baseline_revision: 'da5accfca190fa8b3ba550a21e25ed177629b5bb'
baseline_commit: 'da5accfca190fa8b3ba550a21e25ed177629b5bb'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-ux-instructions.md'
warnings: [oversized]
deferred:
  - 'Browser-level activeElement proof pending an authenticated Admin UI E2E fixture with controllable write-denial responses.'
---

<intent-contract>

## Intent

**Problem:** The Admin surface still exposes API discovery in Production by default, lacks active exhaustive proof that unconfirmed MCP mutations perform zero work, reports unavailable CLI commands as successful, and presents destructive UI actions without one consistent target/impact/permission contract. Its published inventory also promises operations that are not callable.

**Approach:** Apply one fail-closed safety envelope across the Admin host, MCP, CLI, UI, and their documentation: Development-only discovery, explicit preview-before-execution semantics, truthful failure statuses, reusable resource-backed confirmation content, and outer-surface tests that prove no protected work occurs without intent.

## Boundaries & Constraints

**Always:** Keep only `/health`, `/alive`, and `/ready` anonymously reachable outside Development; require `confirm: true` before every MCP write call and expose target, impact, and exact `Operator` or `Admin` permission in the safe preview; return nonzero for unavailable CLI commands; use FrontComposer/Fluent UI V5 and resource-backed copy for destructive confirmation facts; restore focus to the initiating control after cancellation, validation failure, or denial; keep result language truthful about accepted versus completed work; use bounded, support-safe text and `ConfigureAwait(false)` on changed production awaits.

**Never:** Change the public EventStore gateway's separate OpenAPI behavior; weaken Story 5.2 authorization/tenant/request-size boundaries or Story 5.3 authentication; rework Story 5.1 actor durability; activate deferred backup, restore, import, or compaction engines; add a preview nonce protocol; expose tokens, claims, raw payloads, stack traces, cursors, ETags, or hidden-resource existence; unskip DW2 evidence tests; enter later DAPR/OpenBao/topology stories; or modify/revert `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| API discovery | Admin host in Production, regardless of omitted or true discovery setting | `/openapi/v1.json` and `/swagger/*` are not mapped; probes remain reachable | `404` without discovery content or protected work |
| Local discovery | Admin host in Development with enabled/disabled setting | Enabled maps document/UI; disabled omits both | Existing authenticated Admin routes are unchanged |
| MCP preview | Any write tool with confirmation omitted or false | Safe JSON names action, target, impact, required permission, and executes zero HTTP calls | Invalid input remains a bounded error and also executes zero calls |
| MCP execution | Valid write tool with confirmation true | Exactly one expected Admin API request is attempted | Existing bounded/redacted Admin API error mapping remains |
| CLI placeholder | Invoked unavailable command | Diagnostic says unavailable and process returns `ExitCodes.Error` | Never returns success or claims work completed |
| UI destructive action | Reset/replay, dead-letter mutation, restore/import, policy deletion, tenant/user/role mutation, or consistency mutation | Confirmation identifies target, impact, and permission before execution | Cancel, validation, or denial performs no mutation and restores initiator focus |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Admin.Server.Host/Program.cs` and `appsettings*.json` -- current `EventStore:Admin:OpenApi:Enabled` fallback is `true`; make discovery Development-only with explicit local enablement.
- `tests/Hexalith.EventStore.Admin.Server.Host.Tests/HostBootstrapTests.cs` and `tests/Hexalith.EventStore.Admin.Server.Tests/OpenApi/` -- exercise the real environment-specific pipeline; factory-only conditional duplication is not completion evidence.
- `src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs`, `BackupWriteTools.cs`, `ConsistencyWriteTools.cs`, and `ProjectionWriteTools.cs` -- seven callable writes already short-circuit on `confirm=false`; enrich previews with exact permission without changing execution routes.
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/` -- add an active exhaustive zero-call/one-call inventory; do not reuse the skipped `Dw2McpProtocolGatesAtddTests` as the gate.
- `src/Hexalith.EventStore.Admin.Cli/Commands/StubCommands.cs`, `Commands/Backup/BackupCommand.cs`, and `tests/Hexalith.EventStore.Admin.Cli.Tests/StubCommandsTests.cs` -- unavailable backup commands currently print a warning and return success; preserve their deferred state but fail truthfully.
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/`, `Resources/`, and `AdminUIServiceExtensions.cs` -- introduce one Fluent, resource-backed confirmation-facts component/service seam rather than duplicating safety copy or styling.
- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor` and `Pages/{DeadLetters,Backups,Snapshots,Tenants,Consistency}.razor` -- destructive/high-impact confirmations and initiator-focus seams; Compaction remains visibly unavailable.
- `tests/Hexalith.EventStore.Admin.UI.Tests/` -- verify rendered target/impact/permission, zero work on cancel/denial/validation, accurate accepted/completed wording, and exact focus restoration.
- `docs/guides/configuration-reference.md`, `docs/brownfield/api-contracts.md`, `docs/brownfield/component-inventory.md`, and `docs/brownfield/project-overview.md` -- publish the safe discovery default and only the callable CLI/MCP inventory with confirmation and permission contracts.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Admin.Server.Host/Program.cs`, `src/Hexalith.EventStore.Admin.Server.Host/appsettings.json`, `src/Hexalith.EventStore.Admin.Server.Host/appsettings.Development.json`, `tests/Hexalith.EventStore.Admin.Server.Host.Tests/HostBootstrapTests.cs`, and `tests/Hexalith.EventStore.Admin.Server.Tests/OpenApi/` -- map OpenAPI and Swagger only when the environment is Development and the setting is explicitly enabled; prove Production omission, Development enable/disable, and unchanged anonymous probes through the real host.
- [x] `src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs`, `BackupWriteTools.cs`, `ConsistencyWriteTools.cs`, `ProjectionWriteTools.cs`, and `tests/Hexalith.EventStore.Admin.Mcp.Tests/` -- add an exact required-permission preview field and an exhaustive active inventory proving omitted/false confirmation makes zero requests while true confirmation attempts exactly the intended route once.
- [x] `src/Hexalith.EventStore.Admin.Cli/Commands/StubCommands.cs`, `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupCommand.cs`, and `tests/Hexalith.EventStore.Admin.Cli.Tests/StubCommandsTests.cs` -- return `ExitCodes.Error` for every unavailable command without wiring dormant implementations.
- [x] `src/Hexalith.EventStore.Admin.UI/Components/Shared/`, `src/Hexalith.EventStore.Admin.UI/Resources/`, `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs`, `src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor`, `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor`, `Backups.razor`, `Snapshots.razor`, `Tenants.razor`, `Consistency.razor`, and `tests/Hexalith.EventStore.Admin.UI.Tests/` -- render resource-backed target, impact, and permission facts before confirmation; retain Fluent UI V5; preserve denial/non-disclosure behavior; and restore the exact initiating control after cancellation, client validation, or forbidden response.
- [x] `docs/guides/configuration-reference.md`, `docs/brownfield/api-contracts.md`, `docs/brownfield/component-inventory.md`, `docs/brownfield/project-overview.md`, and their existing contract tests -- synchronize production discovery, MCP confirmation/permission, CLI availability, and accepted-versus-completed semantics with the executable surface; remove claims for unregistered commands and tools.

**Acceptance Criteria:**
- Given a real Production Admin host, when anonymous callers request every discovery or probe route, then OpenAPI and Swagger return `404`, only the three probes remain anonymously reachable, and protected routes still challenge.
- Given each callable MCP mutation, when confirmation is absent or false, then its preview names target, impact, and required permission and the transport records exactly zero requests; when true, exactly one expected route is attempted.
- Given an unavailable CLI command, when invoked, then stderr describes its unavailable state and the process returns `ExitCodes.Error` without success or completion language.
- Given any in-scope destructive UI action, when its dialog is shown, then target, impact, and required permission are visible in accessible resource-backed Fluent content before confirmation.
- Given a destructive UI action is cancelled, fails local validation, or is denied, when the interaction settles, then no mutation occurs, the support-safe state does not imply hidden-resource existence, and keyboard focus returns to the exact initiating control.
- Given Admin documentation and command/tool discovery are compared with the assemblies, when the inventory is checked, then every documented operation is callable and every deferred operation is explicitly unavailable rather than reported complete.
- Given implementation is complete, when focused Admin host, MCP, CLI, and UI test projects plus the Release solution build run, then all pass without warnings or regressions and `sprint-status.yaml` is unchanged.

## Spec Change Log

## Review Triage Log

| ID | Verdict and evidence | Route |
|---|---|---|
| BH-01 | `medium` — Verified: preview `parameters` reach `SerializeResult`, whose sanitizer preserves arbitrary strings under ordinary keys, so caller-supplied description/identifier content is not support-safe. | patch |
| BH-02 | `medium` — Verified: `SafeText` has no length limit, so preview and error strings can violate the bounded-output contract. | patch |
| BH-03 | `medium` — Verified: the intent-gate recorder asserts only request URI, leaving method and payload semantics unpinned. | patch |
| BH-04 | `medium` — Verified: inventory discovery is restricted by the `WriteTools` type-name suffix rather than callable tool metadata. | patch |
| BH-05 | `medium` — Verified: the backup MCP preview says a backup will start while the sole registered service returns a deferred result. | patch |
| BH-06 | `medium` — Verified: the backup-create confirmation impact promises asynchronous execution despite the explicit deferred contract in the same dialog. | patch |
| BH-07 | `medium` — Verified: restore impact says state can be replaced while adjacent copy says the operation uses a parallel stream and never overwrites originals. | patch |
| BH-08 | `medium` — Verified: import impact promises validation/import while the registered backend always returns deferred. | patch |
| BH-09 | `medium` — Verified: the export producer writes web/camel-case properties but import validation looks only for PascalCase, rejecting its own exported JSON. | patch |
| BH-10 | `medium` — Verified: missing or non-array `events` becomes count zero and does not fail validation, allowing malformed input to reach the endpoint. | patch |
| BH-11 | `medium` — Verified: the file-read catch renders unbounded raw `ex.Message` text. | patch |
| BH-12 | `medium` — Verified: import validation invokes focus restoration while the modal remains mounted, so the modal focus trap can prevent exact initiator focus. | patch |
| BH-13 | `medium` — Verified: replay validation records an inline error and immediately unmounts the dialog without presenting the error elsewhere. | patch |
| BH-14 | `medium` — Verified: projection reset/replay generic catches still render raw exception messages. | patch |
| BH-15 | `medium` — Verified: write methods in the real dead-letter client propagate `AdminApiProblemException` for 403, while the page denial branch catches `ForbiddenAccessException`. | patch |
| BH-16 | `medium` — Verified: the mixed-success dead-letter branch closes its dialog without restoring initiator focus after a denial. | patch |
| BH-17 | `medium` — Verified: tenant creation reports completion although the operation contract is accepted/asynchronous. | patch |
| BH-18 | `medium` — Verified: add-user, remove-user, and role-change success copy reports completed state for accepted operations. | patch |
| BH-19 | `low` — Verified: the legacy OpenAPI factory comment claims production parity while its copied conditional lacks the Development environment guard. The correction is direct. | patch |
| EC-01 | `medium` — Verified duplicate of BH-01: ordinary preview parameter strings bypass support-safe sanitization. | patch |
| EC-02 | `medium` — Verified duplicate of BH-10: a missing/non-array `events` property is accepted locally. | patch |
| EC-03 | `medium` — Verified duplicate of BH-12: validation attempts external focus before closing the import modal. | patch |
| EC-04 | `medium` — Verified: dead-letter batch processing continues to later tenant groups after detecting a denial, permitting additional mutations. | patch |
| EC-05 | `medium` — Verified duplicate of BH-16: mixed success/denial closes without focus restoration. | patch |
| EC-06 | `low` — Verified: hyphen-joined snapshot tuple IDs can collide for distinct tenant/domain/type tuples; a direct collision-safe encoding is warranted. | patch |
| EC-07 | `medium` — Verified: the consistency `InvalidOperationException` catch spans the API call and post-acceptance toast/reload, so a UI failure can mislabel accepted work as invalid. | patch |
| EC-08 | `medium` — Verified duplicate of BH-04: type-name filtering can omit a callable mutation. | patch |
| EC-09 | `medium` — Verified duplicate of EC-04/BH-16: a denied multi-tenant batch can continue and strand focus after partial completion. | patch |
| VG-01 | `medium` — Pre-verified gap: bUnit asserts only the interop call, not final browser `activeElement`. A browser patch was attempted, but the pre-existing E2E fixture cannot authenticate or provide a write-time denial backend, so the required initiators never render. | defer |
| VG-02 | `medium` — Pre-verified gap: several new fact call sites lack populated-state target/impact/permission assertions. | patch |
| VG-03 | `medium` — Pre-verified gap: completion tests use global string containment and do not pin command-scoped tenant/backup inventories for all shells. | patch |
| VG-04 | `medium` — Pre-verified gap: changed bounded fallback catches lack sentinel-bearing exception tests. | patch |
| VG-05 | `medium` — Pre-verified gap: denial cleanup is duplicated per handler but tests cover only sibling actions, allowing independent regressions. | patch |
| VG-06 | `medium` — Pre-verified duplicate of BH-16: no test or implementation restores focus after a mixed dead-letter success/denial. | patch |

## Design Notes

Treat the title as an Admin-boundary scope: the duplicate public gateway OpenAPI default is not part of this story. Production Admin discovery is unconditionally absent so an explicit configuration value cannot create a fourth anonymous surface. Development retains an explicit on/off switch.

Reuse one presentation component for confirmation facts, but keep each page responsible for operation-specific target/impact text and the policy it actually invokes. MCP keeps its existing boolean confirmation contract; completion proof is active transport instrumentation, not a stronger protocol or the skipped DW2 lane. CLI placeholders remain placeholders and fail with the existing generic error exit code.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx` -- expected: restore succeeds.
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Host.Tests/Hexalith.EventStore.Admin.Server.Host.Tests.csproj --configuration Release --no-restore` -- expected: real-host discovery/probe tests pass.
- `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests/Hexalith.EventStore.Admin.Mcp.Tests.csproj --configuration Release --no-restore` -- expected: every write intent gate passes.
- `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests/Hexalith.EventStore.Admin.Cli.Tests.csproj --configuration Release --no-restore` -- expected: unavailable commands fail truthfully.
- `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release --no-restore` -- expected: confirmation, no-work, and focus tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore` -- expected: zero warnings and errors.
- `git diff --check` -- expected: no whitespace errors and no diff for `sprint-status.yaml`.
