---
title: 'Story 5.4: Admin Surface Safety Hygiene'
type: 'feature'
created: '2026-09-07'
status: 'done'
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
| BH2-01 | `medium` — Verified: the baseline-window `sprint-status.yaml` rewrite comes from Epic 3 tracking commits, not Admin hygiene. | defer |
| BH2-02 | `medium` — Verified: `docs/guides/configuration-reference.md` still documented only public `EventStore:OpenApi` after the Admin Development-only gate shipped. | patch |
| BH2-03 | `medium` — Verified: Create Backup gained facts and `backup-create-button` but cancel/401/403 did not capture or restore that initiator. | patch |
| BH2-04 | `medium` — Verified: snapshot create/edit/create-snapshot mounted facts without initiator ids or cancel/denial restore. Completed-toast half is `false`: storage writes return HTTP 200. | patch |
| BH2-05 | `false` — Product export uses `JsonSerializerDefaults.Web` camelCase and the import error text teaches that schema. | reject |
| BH2-06 | `false` — Closing on local validation and restoring the initiator is the story AC; the toast presents the error. | reject |
| BH2-07 | `medium` — Verified: dead-letter batching broke only on `ForbiddenAccessException`, so `UnauthorizedAccessException` continued later tenants and skipped focus. | patch |
| BH2-08 | `false` — Skip controller docs say the command is removed from the queue; both copy lines describe that outcome. | reject |
| BH2-09 | `false` — Completions list registered stub commands; invocation already returns `ExitCodes.Error`. | reject |
| BH2-10 | `false` — Production does not map discovery routes, so 404 cannot carry an OpenAPI/Swagger document. | reject |
| BH2-11 | `medium` — Verified: OQ8 validator/test remints and submodule gitlinks are later-story/other-work in the same baseline window. | defer |
| BH2-12 | `medium` — Verified: `epic-3-retrospective: done` versus a rejected retro file is Epic 3 tracking, not Admin hygiene. | defer |
| BH2-13 | `maybe-false` — `RestoreAsync` has no after-render wait; whether Fluent's trap still owns focus after `HideAsync` needs the deferred browser fixture. | defer |
| BH2-14 | `false` — Intent requires focus restore after cancel, validation, or denial; unexpected errors keep the dialog for retry. | reject |
| BH2-15 | `false` — Every current write tool matches the `bool confirm=false` inventory filter; a different future signature is not present. | reject |
| BH2-16 | `low` — Rejected: Development `Enabled=true` is already supplied by `appsettings.Development.json`; no user-facing defect. | reject |
| EC2-01 | `medium` — Verified duplicate of BH2-07: unauthorized write exceptions continued later tenant groups. | patch |
| EC2-02 | `medium` — Verified: `OnImportConfirm` restored focus while the import modal stayed mounted. | patch |
| EC2-03 | `medium` — Verified: whitespace identifiers passed import validation because only `null` was rejected. | patch |
| EC2-04 | `low` — Rejected: a UTF-16 cut at the 240-character bound is unlikely in everyday support-safe preview use. | reject |
| EC2-05 | `medium` — Verified duplicate of BH2-03: Create Backup cancel did not restore the create control. | patch |
| EC2-06 | `medium` — Verified duplicate of BH2-04: snapshot cancel paths did not restore their initiators. | patch |
| EC2-07 | `medium` — Verified duplicate of BH2-02: configuration-reference omitted Admin discovery. | patch |
| EC2-08 | `medium` — Verified duplicate of BH2-01: sprint-status mutation is outside this story. | defer |
| VG2-01 | `medium` — Pre-verified gap: Create Backup facts had no populated-state assertion. | patch |
| VG2-02 | `medium` — Pre-verified gap: Create Backup cancel did not assert `backup-create-button` focus restore. | patch |
| VG2-03 | `medium` — Pre-verified gap: snapshot create/edit/create-snapshot facts were unpinned. | patch |
| VG2-04 | `medium` — Pre-verified gap: tenant generic-exception redaction lacked sentinel tests. | patch |
| VG2-05 | `medium` — Pre-verified gap: consistency API `InvalidOperationException` lacked a trigger test. | patch |
| BH3-01 | `medium` — carried Verified: the baseline-window `sprint-status.yaml` rewrite comes from Epic 3 tracking commits, not Admin hygiene. | defer |
| BH3-02 | `false` — The spec-artifact changelog/`review_loop_iteration` observation is fixed only by editing this build's spec, which triage rejects. | reject |
| BH3-03 | `false` — The intent-contract matrix names projection reset/replay, not pause/resume; those writes keep the pre-existing generic confirmation. | reject |
| BH3-04 | `false` — Backup validate/export and compaction are outside the intent-contract destructive set; compaction stays a visibly deferred control. | reject |
| BH3-05 | `medium` — Verified: live CLI mutation commands still have no confirmation gate. This story only required unavailable commands to return `ExitCodes.Error`; the live-CLI gap is pre-existing. | defer |
| BH3-06 | `false` — Skip facts describe no processing and the dialog body describes queue removal; both match skip, they do not conflict. | reject |
| BH3-07 | `false` — Tenant confirmation impact names the requested mutation; accepted-versus-completed wording is the post-call result language, which already says request accepted. | reject |
| BH3-08 | `medium` — Verified: `ConfirmationFacts` renders `Target`/`Impact`/`RequiredPermission` with no 240-character bound, so identifier interpolation can exceed the Always bounded-output rule. | patch |
| BH3-09 | `maybe-false` — carried `RestoreAsync` has no after-render wait; whether Fluent's trap still owns focus after `HideAsync` needs the deferred browser fixture. | defer |
| BH3-10 | `false` — Bulk processing already breaks on 401/403; continuing later confirmed tenant groups after a non-denial failure is the remaining batch, not a post-denial mutation. | reject |
| BH3-11 | `false` — `ServiceUnavailableException` messages are fixed client strings, and `AdminOperationResult.Message` is the API operator field, not a raw exception/stack leak. | reject |
| BH3-12 | `false` — carried Every current write tool matches the `bool confirm=false` inventory filter; a different future signature is not present. | reject |
| BH3-13 | `low` — carried Rejected: Development `Enabled=true` is already supplied by `appsettings.Development.json`; no user-facing defect. | reject |
| BH3-14 | `false` — `security-model.md` documents JWT OIDC discovery, not Admin OpenAPI/Swagger; those are different surfaces. | reject |
| BH3-15 | `medium` — carried Verified: AppHost still advertises `{adminServerHttps}/swagger/index.html`; topology wiring is outside this story. | defer |
| BH3-16 | `false` — A mixed baseline-window diff is a dirty-tree process fact, not an Admin-hygiene product defect. | reject |
| EC3-01 | `maybe-false` — carried duplicate of BH3-09/BH2-13: success-path `HideAsync` then `RestoreAsync` is the same unproven Fluent trap race. | defer |
| EC3-02 | `maybe-false` — carried duplicate of BH3-09: dead-letter success unmount uses the same restore helper. | defer |
| EC3-03 | `maybe-false` — carried duplicate of BH3-09: consistency success unmount uses the same restore helper. | defer |
| EC3-04 | `maybe-false` — carried duplicate of BH3-09: snapshot success unmount uses the same restore helper. | defer |
| EC3-05 | `maybe-false` — carried duplicate of BH3-09: tenant success unmount uses the same restore helper. | defer |
| EC3-06 | `maybe-false` — carried duplicate of BH3-09: backup restore/import success unmount uses the same restore helper. | defer |
| EC3-07 | `false` — Validate and export dialogs are outside the intent-contract destructive list that requires `ConfirmationFacts`. | reject |
| EC3-08 | `false` — An empty `events` array is a legal export (`Exported 0 events`); local import already rejects a missing or non-array `events` property. | reject |
| EC3-09 | `low` — Verified: `GetRestoreFocusId` interpolates raw `BackupId` into an HTML id, unlike snapshot policy ids which are encoded. Everyday backup ids are ULID-safe, but the restore initiator was added by this story. | patch |
| EC3-10 | `false` — `RestoreAsync` already no-ops on a blank id; inventing a fallback target would violate exact-initiator restoration. | reject |
| EC3-11 | `false` — Create/confirm controls stay disabled until required identifiers are non-whitespace, so blank facts cannot be confirmed. | reject |
| EC3-12 | `low` — carried Rejected: a UTF-16 cut at the 240-character bound is unlikely in everyday support-safe preview use. | reject |
| EC3-13 | `medium` — Verified: Admin token-provider OIDC empty/non-JSON handling lives in the Story 5.3 authentication surface, not this Admin hygiene envelope. | defer |
| EC3-14 | `medium` — Verified: `LoadIntoBufferAsync` size-limit classification is Story 5.3 token-acquisition code in the same baseline window. | defer |
| EC3-15 | `medium` — Verified: `expires_in` parsing is Story 5.3 token-acquisition code in the same baseline window. | defer |
| EC3-16 | `medium` — Verified: the Sample BlazorUI token-provider twin is Story 5.3, not Admin surface hygiene. | defer |
| EC3-17 | `medium` — Verified: publish-mode Tenants source inclusion is topology/authentication work this story forbids entering. | defer |
| EC3-18 | `medium` — carried duplicate of BH3-01: sprint-status mutation is outside this story. | defer |
| EC3-19 | `false` — duplicate of EC3-07: validate/export are not in-scope destructive confirms. | reject |
| EC3-20 | `false` — Cancel-focus on validate/export is outside the intent-contract destructive set. | reject |
| VG3-01 | `medium` — Pre-verified gap: Create Backup and snapshot create-policy/edit-policy/create-snapshot now close and restore on 401/403, but tests still cover only cancel plus sibling denials, so those four handlers can regress silently. | patch |
| VG3-02 | `medium` — Pre-verified gap: named-client `AllowAutoRedirect=false` is proven only when tests call `AddHttpClient` directly, not through `AddAdminUI`. That composition is Story 5.3 token acquisition, not this hygiene envelope. | defer |

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

### Review Findings — Host/OpenAPI chunk (2026-09-10)

- [x] [Review][Defer] Operator catalog still omits Admin discovery while that file is OQ8-sealed — deferred: do not remint the already-drifting public-document seal from this Host slice; `api-contracts.md` already documents the Admin gate; add the catalog paragraph on the Docs chunk / later OQ8 public-document remint.
- [x] [Review][Patch] Development omitted/null OpenAPI flag is unpinned [tests/Hexalith.EventStore.Admin.Server.Host.Tests/HostBootstrapTests.cs:151]
- [x] [Review][Defer] AppHost always advertises Admin Swagger UI [src/Hexalith.EventStore.AppHost/Program.cs:374] — deferred: pre-existing topology wiring outside this Host/OpenAPI slice; Story 5.4 forbids entering later DAPR/topology stories, and published/non-Development Admin hosts now 404 that URL by design.

#### Rejected — Host/OpenAPI chunk (2026-09-10)

- [Rejected][false] Brownfield overview/inventory omitted from this diff lack the discovery contract — this chunk excluded those files; `docs/brownfield/api-contracts.md` already documents the Admin Development-only gate, and `project-overview.md` is a package table.
- [Rejected][false] `DevelopmentPipeline_WithExplicitEnablement_MapsDiscovery` never sets `Enabled=true` — the real Development host factory loads `appsettings.Development.json`, which sets the flag `true`.
- [Rejected][false] Production omitted-setting row is untested — `ProductionPipeline_AlwaysOmitsDiscovery(true)` already proves configuration cannot expose discovery outside Development; omitted binds to `false` under `GetValue<bool>`.
- [Rejected][false] Production 404s must also assert body emptiness and `/swagger/swagger-initializer.js` — unmapped endpoints return framework 404 without OpenAPI/Swagger payloads; initializer is the same SwaggerUI middleware as `index.html`.
- [Rejected][false] Production discovery is not proven together with probes and protected routes — `ProductionPipeline_LeavesProbesAnonymousAndProtectedAdminRouteChallenged` uses the same factory type; probes are mapped before the OpenAPI `if`.
- [Rejected][false] Development disable does not re-hit authenticated Admin routes — `MapControllers()` is unconditional after the OpenAPI gate.
- [Rejected][false] Development tests omit the `/swagger` prefix — `RoutePrefix = "swagger"` serves prefix and `index.html`; Production already probes `/swagger`.
- [Rejected][low] Development enablement asserts only HTTP 200 — `MapOpenApi()` / `UseSwaggerUI()` on the real host return the document and UI; sibling `AdminOpenApiDocumentTests` already pin document shape.
- [Rejected][false] Disabled OpenAPI factory copies the gate and does not force Development — Host.Tests pin `Program.cs`; this factory is a Server.Tests double with `Enabled=false`.
- [Rejected][false] Enabled OpenAPI factory maps unconditionally and ignores `Enabled=true` — that factory exists to generate documents for schema tests, not to prove the host gate.
- [Rejected][false] `AddAdminApi` still always calls `AddAdminOpenApi()` and the XML invites ungated mapping — DI registration does not expose routes; mapping is host-gated.
- [Rejected][false] `Enabled=true` outside Development is a silent 404 — intended fail-closed behavior; configuration cannot expose discovery outside Development.
- [Rejected][false] `GetValue<bool>` is an implicit default rather than `GetValue(..., false)` — a missing key already binds to `false`.
- [Rejected][false] Credential comment/tests still use `user:password` / username `user` — those are placeholders; fixture passwords are randomized.
- [Rejected][false] Credential log test only searches formatted `Message` — `LogInformation` already passes `SanitizeEndpoint(...)` as `{Endpoint}`, so structured state is the sanitized value.
- [Rejected][false] Timeline `CancellationToken` is captured at argument index 6 — that index matches `IStreamQueryService.GetStreamTimelineAsync` today.

### Review Findings — Host+MCP+CLI+docs chunk (2026-09-10)

- [x] [Review][Patch] MCP `backup-trigger` discovery and tests still present a completed backup [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:18]
- [x] [Review][Patch] Write-tool inventory counts only tools with `confirm` defaulting to false [tests/Hexalith.EventStore.Admin.Mcp.Tests/WriteToolIntentGateTests.cs:84]
- [x] [Review][Patch] Confirmed MCP results skip the 240-character support-safe bound [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:33]
- [x] [Review][Patch] Redacted or truncated preview values are not the values posted on confirm [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:43]
- [x] [Review][Patch] Intent-gate preview assertions omit the endpoint that confirm actually posts [tests/Hexalith.EventStore.Admin.Mcp.Tests/WriteToolIntentGateTests.cs:141]
- [x] [Review][Patch] Registered backup stub help text still describes working backup operations [src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupCommand.cs:15]
- [x] [Review][Defer] Bash health completions still advertise `--interval` [src/Hexalith.EventStore.Admin.Cli/Commands/Config/CompletionScripts.cs:65] — deferred: pre-existing; this change only rewrote backup and tenant inventories
- [x] [Review][Defer] Configuration-reference JWT, AppHost, and publish-mode UI grant docs sit in the same file as Admin OpenAPI — deferred: Story 5.3 / topology content in the baseline window, not this Admin hygiene envelope
- [x] [Review][Defer] CLI inventory still names `.eventstore-admin-profiles.json` [docs/brownfield/component-inventory.md:61] — deferred: pre-existing sentence left beside the rewritten backup inventory

#### Rejected — Host+MCP+CLI+docs chunk (2026-09-10)

- [Rejected][false] `requiredPermission` must use `AdminAuthorizationPolicies` constants — the spec requires the words `Operator` or `Admin`, not `AdminOperator` / `AdminFull`.
- [Rejected][false] Completions offering `backup create|restore|list` treats stubs as live engines — those are the registered commands; `BackupCommand.Create` and `BackupCommandTests` already fail them with `ExitCodes.Error`.
- [Rejected][false] Dormant `BackupTriggerCommand` success tests make backup callable — `BackupCommand.Create` does not register those types; the spec preserves dormant engines without wiring them.
- [Rejected][false] `AdminOpenApiWebApplicationFactory` always mapping OpenAPI hides a host-gate regression — `HostBootstrapTests` is the real-host proof; that factory exists to generate schema documents.
- [Rejected][false] Host tests omit Staging and published-as-Development discovery — `IsDevelopment()` matches the spec I/O matrix; Development `Enabled: true` is the specified local aid.
- [Rejected][false] MCP missing-env usage no longer names Bearer — the process still sends `Authorization: Bearer`; “authentication credential” is not a false scheme claim.
- [Rejected][false] Omitted and false confirmation previews are uncompared — both take `if (!confirm)` and `AssertPreview` already runs on each result.
- [Rejected][false] Invalid-input coverage only empties the first string, so empty `projectionName` is unproven — `ValidateRequired` already rejects every required pair; the confirm=true empty-tenant path is the inventory gate.
- [Rejected][low] Skipped DW2 ATDD still names `backup-create` / `backup-restore` — those tests stay skipped by spec; un-skipping them is a later DW2 story.
- [Rejected][low] `SafeText` can split a UTF-16 surrogate at the 240-character cut — everyday Admin ids are ASCII; a rune-safe slice adds a branch for a rare description.
- [Rejected][low] Whitespace-only strings skip `SafeText` replacement — `ValidateRequired` rejects blank ids; whitespace descriptions are not interpolated into target/impact.
