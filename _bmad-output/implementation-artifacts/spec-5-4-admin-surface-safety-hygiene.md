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
| BH4-01 | `medium` — carried: the baseline-window `sprint-status.yaml` changes belong to other completed tracking work; Story 5.4 did not change the current working-tree copy. | defer |
| BH4-02 | `false` — carried: the 543-file baseline window contains independently committed stories and user work, while Story 5.4 remains identifiable by its own Admin-surface commits and current working-tree changes. | reject |
| BH4-03 | `medium` — Verified: `ConfirmationFacts` bounds text but does not redact credential-shaped values, so interpolated identifiers can expose support-unsafe content. | patch |
| BH4-04 | `medium` — Verified: import-file tenant, domain, and aggregate identifiers are rendered directly in `_importPreview`; a crafted file can display unbounded credential-shaped content. | patch |
| BH4-05 | `medium` — Verified: reset confirmation trusts the numeric component's `Min` metadata and sends a negative programmatically supplied position. | patch |
| BH4-06 | `medium` — Verified: replay checks presence and ordering but sends negative positions when both values preserve the required order. | patch |
| BH4-07 | `low` — Verified: the pre-existing UI requires `from < to` although the inclusive API/MCP contract permits a single-position replay; this blocks a narrow legitimate workflow. | defer |
| BH4-08 | `medium` — Verified pre-existing issue: snapshot mutations reuse `_loadCts`, so a concurrent refresh can cancel a write and escape through an uncaught cancellation. | defer |
| BH4-09 | `medium` — Verified pre-existing issue: snapshot clients surface HTTP 422 as `InvalidOperationException`, which the mutation handlers do not map to bounded UI feedback. | defer |
| BH4-10 | `medium` — Verified: after a denial following earlier success, later dead-letter tenant groups are neither attempted nor retained as failures, so their selections disappear. | patch |
| BH4-11 | `medium` — Verified verification weakness: the Production host test challenges one representative Admin route, so a newly anonymous controller action would not fail the acceptance gate. | patch |
| BH4-12 | `medium` — carried: bUnit proves only the focus interop call; authenticated browser `activeElement` evidence remains unavailable with the current fixture. | defer |
| BH4-13 | `false` — The tenant-creation contract has no 64-character maximum; that bound is specific to the MCP preview/execution envelope, so the UI is not bypassing a canonical tenant rule. | reject |
| BH4-14 | `false` — carried: changing this build's review bookkeeping is not a product correction and findings whose fix edits the build spec are rejected. | reject |
| EC4-01 | `medium` — Verified: repeated consistency check types remain duplicated in the persisted request and per-stream loop, repeating work and potentially anomaly counts. | patch |
| EC4-02 | `medium` — Verified duplicate of BH4-10: unattempted dead-letter groups after a denial are omitted from failures and the retained selection. | patch |
| EC4-03 | `medium` — Verified duplicate of BH4-03: credential-shaped confirmation facts are truncated but not redacted. | patch |
| EC4-04 | `maybe-false` — carried: final browser focus after projection success cannot be established without observing `activeElement` after Fluent dialog teardown. | defer |
| EC4-05 | `maybe-false` — carried: final browser focus after backup success cannot be established without observing `activeElement` after Fluent dialog teardown. | defer |
| EC4-06 | `maybe-false` — carried: final browser focus after consistency success cannot be established without observing `activeElement` after Fluent dialog teardown. | defer |
| EC4-07 | `maybe-false` — carried: final browser focus after snapshot success cannot be established without observing `activeElement` after Fluent dialog teardown or row removal. | defer |
| EC4-08 | `maybe-false` — carried: final browser focus after tenant success cannot be established without observing `activeElement` after dialog teardown and refresh. | defer |
| EC4-09 | `maybe-false` — carried: final browser focus after dead-letter success cannot be established without observing `activeElement` after selection-toolbar removal. | defer |
| EC4-10 | `medium` — Verified outside Story 5.4: an explicitly blank or null `CommandStatusPath` can fail or construct the wrong gateway request because the new status client does not validate it. | defer |
| EC4-11 | `medium` — carried duplicate of BH4-01: the tracking-file change is unrelated baseline-window history, not a Story 5.4 working-tree mutation. | defer |
| VG4-01 | `medium` — carried pre-verified gap: focus tests assert mocked interop only, not final browser `activeElement`; the authenticated denial-capable browser fixture remains unavailable. | defer |
| VG4-02 | `medium` — carried pre-verified gap: redirect safety is not composed through `AddAdminUI`, but that production token-client registration is Story 5.3 work in the mixed baseline window. | defer |
| VG4-03 | `medium` — Pre-verified gap outside Story 5.4: oversized unknown-length OIDC responses are not tested, so the Story 5.3 bounded-buffer path could regress while known-length tests stay green. | defer |
| BH5-01 | `medium` — carried from BH4-01: the baseline diff contains independently committed sprint-status tracking, while the current Story 5.4 working tree and verification leave `sprint-status.yaml` unchanged. | defer |
| BH5-02 | `false` — The Verification section defines the required command contract; executed outcomes are workflow evidence, and no story requirement says to duplicate those transient results in the spec. | reject |
| BH5-03 | `medium` — carried from VG4-01: bUnit proves the requested focus interop call, while final browser `activeElement` still requires the deferred authenticated denial-capable fixture. | defer |
| BH5-04 | `false` — Requiring a canonical tenant in the MCP consistency tool is the accepted Epic 5 tenant-isolation contract; the broader API/UI capability does not define this MCP write envelope. | reject |
| BH5-05 | `low` — The generic confirmation mismatch is demonstrated only for unsafe or overlong identifiers, and no additional current executable path beyond the separately verified import-file case is shown; a cross-surface guard would add disproportionate complexity. | reject |
| BH5-06 | `medium` — Verified pre-existing UI issue: identifiers can be rendered outside `ConfirmationFacts`, so credential-shaped server data could remain visible even when the facts component redacts it. | defer |
| BH5-07 | `medium` — Verified: the narrowed JWT regex requires both JSON segments to start with `eyJ`, so a valid compact JWT with another base64url payload prefix can evade redaction. | patch |
| BH5-08 | `medium` — Verified: credential detection omits camelCase JSON/query spellings such as `accessToken`, `refreshToken`, `idToken`, and `clientSecret`. | patch |
| BH5-09 | `false` — Every current Admin controller action has authorization metadata; the reported anonymous endpoint requires a hypothetical future action and does not occur in the reviewed tree. | reject |
| BH5-10 | `medium` — carried from BH4-08: snapshot writes reuse the load cancellation token, a pre-existing issue already deferred outside this story. | defer |
| BH5-11 | `medium` — carried from BH4-09: snapshot HTTP 422 handling remains a pre-existing issue already deferred outside this story. | defer |
| BH5-12 | `low` — carried from BH4-07: the UI excludes an inclusive single-position replay, a pre-existing narrow workflow limitation already deferred. | defer |
| BH5-13 | `medium` — Verified pre-existing issue: consistency export renders raw `Exception.Message` text in a toast; blame predates the Story 5.4 baseline. | defer |
| BH5-14 | `medium` — Verified pre-existing issue: consistency result display/export can expose raw `ErrorMessage`, anomaly `Details`, and the full serialized result. | defer |
| BH5-15 | `medium` — Verified pre-existing issue: capability refresh clears open consistency dialogs and initiator ids without restoring focus; the code predates the Story 5.4 baseline. | defer |
| EC5-01 | `false` — No current `SerializeResult` caller supplies a plaintext credential beneath a credential-named property; the trigger requires a hypothetical future result model, while current strings still pass through value detection. | reject |
| EC5-02 | `medium` — Verified: camelCase credential keys and percent-encoded URI user-info can bypass the shared marker detector and reach CLI, MCP, or UI support text. | patch |
| EC5-03 | `medium` — Verified: import preview redacts or truncates tenant/domain/aggregate identifiers but confirmation still submits the original tenant and JSON content, so the displayed target can differ from execution. | patch |
| EC5-04 | `medium` — Verified pre-existing Story 5.3 issue: trimming the full token endpoint URI can remove a trailing slash from an allowed query value and change OAuth resource semantics. | defer |
| EC5-05 | `false` — Earlier tenant groups are separate authorized operations completed before a later group is denied; the loop performs no calls after denial and retains denied/unattempted groups for retry. | reject |
| EC5-06 | `medium` — carried from BH4-01: the baseline window contains unrelated sprint-status history, but Story 5.4 has no current tracking-file mutation. | defer |
| VG5-01 | `medium` — Pre-verified gap: the newly recognized query-secret aliases are not exhaustively covered, so removing an alias can leak an observability URL while all current tests remain green. | patch |
| VG5-02 | `medium` — carried from VG4-01: final browser focus remains unverified beyond mocked interop and awaits the authenticated denial-capable E2E fixture. | defer |
| BH6-01 | `medium` — Verified: confirmation safety accepts control and Unicode-format characters, so an operator-visible target can be reordered or concealed while remaining executable. | patch |
| BH6-02 | `medium` — Verified: backup creation displays the raw tenant but submits its trimmed value, breaking exact confirmation binding. | patch |
| BH6-03 | `medium` — Verified: consistency trigger displays raw tenant/domain scopes but submits trimmed scopes. | patch |
| BH6-04 | `medium` — Verified: add-user displays the raw user id but submits its trimmed value. | patch |
| BH6-05 | `false` — No canonical domain/aggregate grammar is established for import, and aggregate identifiers intentionally accept non-whitespace values; bidi/control safety is handled by BH6-01. | reject |
| BH6-06 | `false` — The approved resource contract intentionally identifies a dead-letter selection by command and tenant counts; exact identifiers remain visible in the selected grid and are not required in the modal facts. | reject |
| BH6-07 | `medium` — Verified pre-existing issue: dead-letter loading and selection key only by `MessageId`, so a cross-tenant collision can drop or ambiguously select an entry. | defer |
| BH6-08 | `low` — Verified pre-existing issue: two dead-letter clamp paths append an ellipsis after taking 240 characters, producing 243-character support text. | defer |
| BH6-09 | `medium` — carried from VG5-02: mocked interop does not establish final browser `activeElement`; the authenticated denial-capable fixture remains unavailable. | defer |
| BH6-10 | `medium` — Verified pre-existing issue: deferred backup result messages containing one expected keyword are rendered verbatim without a bound or credential scan. | defer |
| BH6-11 | `false` — carried from BH3-11: production tenant clients construct fixed service-unavailable messages; the claimed raw backend diagnostic path is not reachable through the registered client. | reject |
| BH6-12 | `medium` — carried from BH5-13: consistency export still renders raw exception text, a pre-existing issue already deferred outside this story. | defer |
| BH6-13 | `false` — `Authorization: Basic` is already caught by the authorization key detector, and the review demonstrates no current Admin result producer for cookie or PEM material. | reject |
| BH6-14 | `medium` — carried from EC5-04: the Sample UI trailing-query-slash defect belongs to the pre-existing Story 5.3 token-provider twin. | defer |
| BH6-15 | `medium` — carried from EC3-14/EC3-16: Sample UI buffering and empty-response classification are pre-existing Story 5.3 token-acquisition work. | defer |
| BH6-16 | `medium` — carried from BH5-01: baseline-window sprint tracking belongs to independently committed work; Story 5.4 leaves the current tracker unchanged. | defer |
| EC6-01 | `medium` — Verified duplicate of BH6-02: backup tenant normalization occurs after confirmation rendering. | patch |
| EC6-02 | `medium` — Verified duplicate of BH6-03: consistency scope normalization occurs after confirmation rendering. | patch |
| EC6-03 | `medium` — Verified duplicate of BH6-04: add-user normalization occurs after confirmation rendering. | patch |
| EC6-04 | `medium` — Verified: a partly stale dead-letter selection can execute fewer commands than the confirmation count because only the all-stale case is rejected. | patch |
| EC6-05 | `false` — carried from EC5-05: earlier tenant groups are separately accepted operations, processing stops at denial, and denied/unattempted groups remain selected. | reject |
| EC6-06 | `false` — Successful projection operations are outside the intent's focus-restoration conditions, which are cancellation, validation failure, and denial. | reject |
| EC6-07 | `false` — Successful backup restore is outside the specified focus-restoration conditions. | reject |
| EC6-08 | `false` — Successful import is outside the specified focus-restoration conditions. | reject |
| EC6-09 | `false` — Successful consistency trigger is outside the specified focus-restoration conditions. | reject |
| EC6-10 | `false` — Successful consistency cancellation is outside the specified focus-restoration conditions. | reject |
| EC6-11 | `false` — Successful dead-letter submission is outside the specified focus-restoration conditions. | reject |
| EC6-12 | `false` — Successful snapshot policy create/edit is outside the specified focus-restoration conditions. | reject |
| EC6-13 | `false` — Successful snapshot policy deletion is outside the specified focus-restoration conditions. | reject |
| EC6-14 | `false` — Successful tenant creation is outside the specified focus-restoration conditions. | reject |
| EC6-15 | `false` — Successful add-user is outside the specified focus-restoration conditions. | reject |
| EC6-16 | `false` — Successful remove-user is outside the specified focus-restoration conditions. | reject |
| EC6-17 | `false` — Successful role change is outside the specified focus-restoration conditions. | reject |
| EC6-18 | `medium` — carried from BH5-01: the tracker state is historical baseline-window work and is unchanged in the Story 5.4 working patch. | defer |
| VG6-01 | `medium` — carried from VG4-03: unknown-length oversized OIDC discovery/token coverage remains a deferred Story 5.3 verification gap. | defer |
| VG6-02 | `medium` — Pre-verified gap: newly duplicated unsafe-target guards outside projection have no caller-path zero-request, safe-error, close, and focus tests. | patch |
| VG6-03 | `medium` — carried from VG5-02: the render-wait test proves mocked interop ordering but not final browser focus. | defer |

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

### Review Findings — Host/OpenAPI follow-up (2026-09-11)

- [x] [Review][Patch] Production discovery assertions follow redirects [tests/Hexalith.EventStore.Admin.Server.Host.Tests/HostBootstrapTests.cs:539]
- [x] [Review][Defer] AppHost advertises an unavailable Admin Swagger URL outside Development [src/Hexalith.EventStore.AppHost/Program.cs:374] — deferred: the unconditional publish-time environment value predates the Story 5.4 baseline, is outside this Host/OpenAPI chunk, and is already tracked in the deferred-work ledger.

#### Rejected — Host/OpenAPI follow-up (2026-09-11)

- [Rejected][false] The disabled OpenAPI factory duplicates the host gate and can hide a `Program.cs` regression — `HostBootstrapTests` exercises the real `Program.cs`; the separate disabled factory is only a Server.Tests test host, so a production-entry-point regression is still caught.
- [Rejected][false] Production omission is untested — base configuration supplies `false`, the Production theory also forces the less-safe `true` value, and the real environment guard makes omission introduce no distinct production branch; the unset binder case is independently covered in Development.
- [Rejected][low] Malformed Development OpenAPI configuration can stop host startup — confirmed with `EventStore__Admin__OpenApi__Enabled=not-a-boolean`, but this is pre-existing, explicit fail-fast behavior with a precise conversion diagnostic; silently accepting the typo would require an extra fallback branch and is not worth changing here.

### Review Findings

_Chunk 1 follow-up review — Host, MCP, CLI, and documentation (2026-09-12)._

- [x] [Review][Patch] Require an explicit `tenantId` for consistency previews and execution so every confirmation names an exact tenant; keep `domain` optional to mean all domains within that tenant [src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyWriteTools.cs:26]
- [x] [Review][Patch] Derived MCP target and endpoint fields can truncate even when every raw parameter passes preview/execution validation [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:100]
- [x] [Review][Patch] Optional backup description and consistency scopes are not normalized before preview and execution, producing redacted, whitespace, or differently omitted values [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:26]
- [x] [Review][Patch] Consistency trigger neither validates advertised check-type names nor serializes them to the server's enum contract, so invalid inputs reach transport and valid confirmed calls bind as HTTP 400 [src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyWriteTools.cs:26]
- [x] [Review][Patch] Projection reset and replay allow negative event positions to reach the protected downstream endpoint [src/Hexalith.EventStore.Admin.Mcp/Tools/ProjectionWriteTools.cs:112]
- [x] [Review][Patch] The exhaustive write-tool invalid-input gate covers only one empty first argument and misses caller-boundary unsafe, overlong, optional-scope, enum, and numeric cases [tests/Hexalith.EventStore.Admin.Mcp.Tests/WriteToolIntentGateTests.cs:119]
- [x] [Review][Patch] Session context stores the full scope but reports a truncated scope that later queries do not use [src/Hexalith.EventStore.Admin.Mcp/Tools/SessionTools.cs:55]
- [x] [Review][Patch] MCP result sanitization exposes continuation cursors, opaque projection configuration, and consistency error/raw-detail fields [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:154]
- [x] [Review][Patch] The `ping` tool bypasses the bounded and support-safe MCP result sanitizer [src/Hexalith.EventStore.Admin.Mcp/Tools/ServerTools.cs:35]
- [x] [Review][Patch] Unsafe-marker detection permits Bearer tokens, JWT-shaped values, JSON secret fields, `client_secret`, and URI user-info through preview and result text [src/Hexalith.EventStore.Admin.Abstractions/Security/UnsafeMarkerDetection.cs:14]
- [x] [Review][Patch] HTTP 400 responses are mislabeled as `server-error` instead of bounded invalid input [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:135]
- [x] [Review][Patch] The reusable-component inventory says 16 but lists 15 components [docs/brownfield/component-inventory.md:18]
- [x] [Review][Patch] Backup Swagger and MCP-client XML still promise a full backup although the registered backend is deferred [src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs:55]
- [x] [Review][Defer] Valid tenant identifiers `admissions`, `export-stream`, and `import-stream` collide with fixed backup controller routes [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:36] — deferred: the collision predates this change and requires a route/versioning or tenant-compatibility decision outside the Story 5.4 deferred-backup boundary.
- [x] [Review][Defer] CLI inventory names `.eventstore-admin-profiles.json` instead of `~/.eventstore/profiles.json` [docs/brownfield/component-inventory.md:61] — deferred: this unchanged, pre-existing sentence is already tracked from the earlier Story 5.4 chunk review.
- [x] [Review][Defer] Authentication documentation omits three published-UI settings from its exhaustive table, omits the symmetric `AllowedAlgorithms` rule, and permits Development HTTP token endpoints that the Aspire helper rejects [docs/guides/configuration-reference.md:419] — deferred: these are Story 5.3 authentication/AppHost findings in the mixed baseline window, which Story 5.4 explicitly excludes from rework.

#### Rejected

- [Rejected][false] Callable-write inventory can silently miss a current mutation — all current write tools and all current `AdminOperationResult` POST helpers are enumerated; the proposed failure requires a hypothetical future tool outside both conventions.
- [Rejected][false] Preview permissions have already drifted from controller authorization policies — every current `Admin`/`Operator` preview value matches its controller policy; only a hypothetical future policy change is described.
- [Rejected][false] Ordinary string bounding produces undetectably false values — the required bound is intentional and truncation is visibly marked with `...`; no current generic MCP result contract requires arbitrary strings to remain lossless.
- [Rejected][false] Discovery omission lacks proof for non-Production environments — `IsDevelopment()` excludes every non-Development environment, and the acceptance criterion specifically requires the real Production-host proof that exists.
- [Rejected][low] A 240-character cut can split a UTF-16 surrogate pair — the edge case is real but unlikely for everyday Admin identifiers/descriptions, and fixing it adds a special-case branch for negligible impact.

### Review Findings

_Group 1 adversarial review — Host, MCP, CLI, and documentation (2026-09-12)._

- [x] [Review][Patch] Consistency trigger uses a string-array client contract that the real enum-bound controller rejects, and it accepts unadvertised check-type values [src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyWriteTools.cs:35]
- [x] [Review][Patch] Consistency trigger permits an omitted tenant even though an Operator request is silently narrowed to a tenant the preview cannot name [src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyWriteTools.cs:22]
- [x] [Review][Patch] Optional backup and consistency inputs are not normalized, so blank values render misleading preview parameters or reach execution as ambiguous scopes [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:26]
- [x] [Review][Patch] MCP write boundaries accept path-normalizing identifiers, invalid tenant grammar, and negative projection positions that still perform an HTTP request [src/Hexalith.EventStore.Admin.Mcp/Tools/ProjectionWriteTools.cs:112]
- [x] [Review][Patch] Individually bounded parameters can compose a target or endpoint that is truncated before confirmation while execution uses the full values [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:100]
- [x] [Review][Patch] MCP result sanitization does not protect cursors, opaque configuration, raw consistency details, error/status messages, or common credential shapes [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:154]
- [x] [Review][Patch] Caller-level intent-gate tests cover only one blank argument and do not exercise unsafe, overlong, optional-scope, enum, path, or numeric cases across all write tools [tests/Hexalith.EventStore.Admin.Mcp.Tests/WriteToolIntentGateTests.cs:119]
- [x] [Review][Patch] Published inventory is already miscounted and its test checks selected literals instead of comparing documentation with the CLI/MCP/component assemblies [docs/brownfield/component-inventory.md:18]
- [x] [Review][Patch] MCP documentation promises a preview for every omitted or false confirmation although invalid unconfirmed calls correctly return validation errors [docs/brownfield/component-inventory.md:76]
- [x] [Review][Patch] Completion-script inventory assertions embed LF-only multi-line literals and fail on Windows-generated CRLF output [tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/CompletionScriptsTests.cs:107]
- [x] [Review][Patch] Backup Swagger and MCP-client XML still promise a full backup although the registered backend always returns Deferred [src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs:55]
- [x] [Review][Defer] CLI inventory still names `.eventstore-admin-profiles.json` instead of `~/.eventstore/profiles.json` [docs/brownfield/component-inventory.md:61] — deferred: unchanged pre-existing text, already tracked by the earlier Story 5.4 review.
- [x] [Review][Defer] Authentication documentation omits published-UI quick-reference settings and symmetric-mode rules, and permits Development HTTP token endpoints that publish composition rejects [docs/guides/configuration-reference.md:419] — deferred: Story 5.3 authentication/AppHost content from the mixed baseline window; Story 5.4 explicitly excludes reworking that boundary.
- [x] [Review][Defer] Valid tenant `admissions` collides with the fixed backup route and cannot reach the deferred tenant-backup action [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:36] — deferred: pre-existing controller-route ambiguity requiring a route/versioning or tenant-compatibility decision outside Story 5.4.

#### Rejected

- [Rejected][false] Bounding ordinary MCP strings corrupts required opaque values — the 240-character bound is an explicit story requirement, continuation cursors are forbidden output rather than round-trip inputs, and no current caller requires an identifier above the bound.
- [Rejected][low] UTF-16 truncation can split a surrogate pair — real but unlikely for everyday Admin identifiers/descriptions, and correcting it requires an extra boundary branch for negligible practical benefit.
- [Rejected][false] Confirmation must bind to a previous preview with a nonce — the story explicitly forbids adding a preview nonce protocol and defines `confirm=true` as the complete intent gate.
- [Rejected][false] The callable-write inventory currently misses mutations outside `*WriteTools` — every current callable write and `AdminOperationResult` POST helper is enumerated; the alleged miss requires a hypothetical future convention violation.
- [Rejected][low] UTF-16 truncation can split a surrogate pair (duplicate edge-case report) — rejected for the same low-frequency, disproportionate-fix reason above.

### Review Findings — Host/OpenAPI chunk (2026-09-21)

- [x] [Review][Defer] AppHost advertises an unavailable Admin Swagger URL outside Development [src/Hexalith.EventStore.AppHost/Program.cs:374] — deferred: pre-existing topology wiring outside this Host/OpenAPI chunk; Story 5.4 forbids later DAPR/topology stories; already tracked in the deferred-work ledger from 2026-09-10 and 2026-09-11.

#### Rejected — Host/OpenAPI chunk (2026-09-21)

- [Rejected][false] Mapped Development OpenAPI/Swagger is anonymous — Story 5.4 Design Notes and the I/O matrix make Development discovery a local aid; `Always` restricts the three-probe anonymous rule to outside Development; `DevelopmentPipeline_WithExplicitEnablement_MapsDiscovery` is the specified unauthenticated 200 proof.
- [Rejected][false] `ProductionEndpointMetadata_ExposesOnlyTheThreeHealthProbesAnonymously` cannot catch a discovery leak — that test inventories AD-16 `IAllowAnonymous` probes; `ProductionPipeline_AlwaysOmitsDiscovery` already asserts 404 on `/openapi/v1.json`, `/swagger`, and `/swagger/index.html` with redirects disabled, including `Enabled=true`.
- [Rejected][false] Non-Development environments other than Production are untested — `IsDevelopment()` is the same false branch for Staging/Test/custom names; the acceptance criterion requires the real Production host, which the theory covers.
- [Rejected][false] `DevelopmentPipeline_WithDiscoveryUnset_OmitsDiscovery` overlays null rather than a missing key — `GetValue<bool>` treats a later null the same as an absent key (`false`); that is the fail-closed replacement for the old default-`true` fallback.
- [Rejected][false] `ProductionAdminServerHostFactory` no longer tests shipped `appsettings.json` `Enabled: false` — the Production theory pins both `true` and `false`; the parameterless factory default `true` is the less-safe overlay on the `IsDevelopment()` gate.
- [Rejected][false] `AdminOpenApiDisabledFactory` copies the host gate and cannot reach the true branch — Host.Tests exercise real `Program.cs`; the factory is a Server.Tests double that must keep discovery unmapped.
- [Rejected][false] `appsettings.Development.json` `Enabled: true` still ships with the app — explicit Development enablement is the specified local switch; a container left on `ASPNETCORE_ENVIRONMENT=Development` is that environment, not Production.
- [Rejected][low] Empty or non-boolean `EventStore:Admin:OpenApi:Enabled` throws during host build — `GetValue<bool>` fail-fast on malformed values predates this gate change; a `TryParse` swallow would add a branch for an operator typo that already surfaces a precise conversion diagnostic.

### Review Findings — Host+MCP+CLI+docs chunk (2026-09-21)

- [x] [Review][Patch] MCP `SafeText` plus `JwtRegex` redacts fully-qualified type names that look like three 8+ dotted tokens [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:226]
- [x] [Review][Patch] Session context still accepts non-canonical tenant ids that write tools now reject [src/Hexalith.EventStore.Admin.Mcp/Tools/SessionTools.cs:23]
- [x] [Review][Patch] Composed backup description and consistency domain preview-match rejections are untested [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:38]
- [x] [Review][Patch] Ping empty-health payload after the details-to-message reshape is untested [src/Hexalith.EventStore.Admin.Mcp/Tools/ServerTools.cs:34]
- [x] [Review][Defer] Live CLI mutation commands still have no confirmation gate [src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ProjectionPauseCommand.cs:10] — deferred: pre-existing; this story only required unavailable commands to return `ExitCodes.Error`; already tracked in the deferred-work ledger.
- [x] [Review][Defer] CLI inventory still names `.eventstore-admin-profiles.json` [docs/brownfield/component-inventory.md:61] — deferred: pre-existing sentence left beside the rewritten backup inventory; already tracked.
- [x] [Review][Defer] `configuration-reference.md` JWT, AppHost, and publish-mode copy sit beside Admin discovery [docs/guides/configuration-reference.md:139] — deferred: Story 5.3 / topology content in the mixed baseline window; already tracked.

#### Rejected — Host+MCP+CLI+docs chunk (2026-09-21)

- [Rejected][false] Stripping `continuationToken` / `cursor` from MCP paged results hides pagination — Story 5.4 Never forbids exposing cursors; timeline paging still uses `fromSequence` / item sequence numbers.
- [Rejected][false] Treating `details` and `errorMessage` as raw-capable hides operator diagnostics — the 2026-09-12 patch required descriptorizing those fields; anomaly `Description` remains; `ping` already moved support-safe text onto `message`.
- [Rejected][low] Independently legal fields can fail composed target/endpoint preview-match — everyday tenant and projection names stay under 240 characters; the 64/180 projection case is already asserted as `invalid-input`.
- [Rejected][false] `consistency-trigger` requiring a tenant removed fleet-wide MCP checks — requiring a canonical tenant matches Epic 5 isolation and the earlier explicit-tenant patch; the Admin API/UI still allow platform-scoped runs.
- [Rejected][low] `Enum.TryParse` accepts numeric aliases such as `"0"` — everyday callers pass advertised names; confirmed posts already send integers, which the intent-gate pins and the Admin API binds.
- [Rejected][false] Confirmed `backup-trigger` still POSTs while CLI backup stubs fail — MCP confirm must attempt the deferred route; registered CLI `create|restore|list` must return `ExitCodes.Error`.
- [Rejected][false] `AdminOpenApiWebApplicationFactory` maps OpenAPI unconditionally — that factory generates schema documents; `HostBootstrapTests` is the real-host gate.
- [Rejected][false] Development discovery is anonymous — Story 5.4 Always limits the three-probe rule to outside Development; unauthenticated 200s are the specified local-aid proof.
- [Rejected][false] Malformed Development `Enabled` throws while mapping discovery — Production short-circuits before `GetValue<bool>`; Development fail-fast on a non-boolean is pre-existing binder behavior, already rejected on 2026-09-11.
- [Rejected][low] `SerializeResult_RestrictsMarkerRedactionToRawCapableKeys_PerD2` is stale — D2 still holds for marker substrings under ordinary keys; `SerializeResult_RedactsCredentialShapesUnderOrdinaryKeys` documents the JWT/credential contract.
- [Rejected][false] `StubCommandsTests` races on `Console.Error` without `[Collection("ConsoleTests")]` — the class already has that collection attribute.
- [Rejected][false] Dormant `BackupTriggerCommand` success tests imply backup still completes — `BackupCommand.Create` does not register that type; the spec preserves dormant engines without wiring them.

### Review Findings — Chunk 1 Host+MCP+CLI+docs (2026-09-21, bmad-code-review)

- [x] [Review][Patch] JWT-shaped redaction treats dotted type names as credentials, so legal projection and catalog names never reach preview or result text [src/Hexalith.EventStore.Admin.Abstractions/Security/UnsafeMarkerDetection.cs:38]
- [x] [Review][Patch] `session-set-context` still stores non-canonical tenant ids that write tools now reject [src/Hexalith.EventStore.Admin.Mcp/Tools/SessionTools.cs:23]
- [x] [Review][Patch] `consistency-trigger` accepts numeric `Enum.TryParse` tokens (`"0"` runs `SequenceContinuity`; `"99"` is guarded only by untested `IsDefined`) [src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyWriteTools.cs:48]
- [x] [Review][Patch] Health-link query secrets other than `client_secret` are not marked unsafe [src/Hexalith.EventStore.Admin.Abstractions/Security/UnsafeMarkerDetection.cs:44]
- [x] [Review][Patch] `backup-trigger` never exercises non-canonical tenant rejection at the tool boundary [tests/Hexalith.EventStore.Admin.Mcp.Tests/WriteToolIntentGateTests.cs:183]
- [x] [Review][Patch] `ValidateTenantId` 64-character bound is unpinned [tests/Hexalith.EventStore.Admin.Mcp.Tests/ToolHelperTests.cs:210]
- [x] [Review][Patch] Composed backup-description and consistency-domain preview-match failures are unproven [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:38]
- [x] [Review][Patch] Ping empty-health path is untested after the sanitizer reshape [src/Hexalith.EventStore.Admin.Mcp/Tools/ServerTools.cs:35]
- [x] [Review][Defer] `ValidateTenantId` allows reserved tenant `system` [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:122] — deferred: canonical grammar matches Epic 5; reserved-name rejection is Story 5.10.
- [x] [Review][Defer] `consistency-detail` embeds unvalidated `checkId` in the GET path and error text [src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyTools.cs:46] — deferred: pre-existing read tool; this chunk did not change `ConsistencyTools.cs`.
- [x] [Review][Defer] Published-UI `TokenEndpoint` / audience-parameter keys are missing from the configuration quick-scan table [docs/guides/configuration-reference.md:793] — deferred: Story 5.3 authentication content in the mixed baseline window; already tracked.

#### Rejected

- [Rejected][false] Staging/Test discovery is undescribed and untested — `IsDevelopment()` already omits mapping for every non-Development name; the AC requires the real Production host, which `ProductionPipeline_AlwaysOmitsDiscovery` covers.
- [Rejected][false] Development discovery is anonymous and the docs omit that the gate is the environment name — the I/O matrix maps Development discovery as a local aid; Always restricts the three-probe anonymous rule to outside Development.
- [Rejected][false] `AdminOpenApiWebApplicationFactory` no longer proves the Development×flag conjunction — that factory generates schema documents; `HostBootstrapTests` exercises real `Program.cs`.
- [Rejected][false] Consecutive hyphens such as `a--b` are illegal tenants — Epic 5 grammar `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` allows them.
- [Rejected][false] Preview names versus numeric `checkTypes` on the wire is a confirm/execute mismatch — `AdminApiClientConsistencyCommandTests` pins `"checkTypes":[0,1]` and the Admin binder accepts those enum values; the named preview is the same parsed set.
- [Rejected][false] Adding `configuration` and `details` to `IsRawCapableProperty` hides actor runtime config — that named-key descriptorizing is the 2026-09-12 accepted leak fix; structured actor `Configuration` is treated as opaque by the same rule.
- [Rejected][false] `JsonSecretFieldRegex` treating `"token"` rewrites pagination/correlation tokens under safe keys — ordinary token values do not contain `"token":`; `safeGuidance` mentioning `connectionString` still survives.
- [Rejected][low] `BearerTokenRegex` matches English `Bearer of …` and omits Basic/PEM/`Authorization:` — everyday Admin results do not carry those shapes; tightening the detector adds branches for a phrase that is not in the current models.
- [Rejected][false] `api-contracts.md` still lists backup POST routes as live `AdminFull` operations — that table is the Story 5.2 policy/body-limit inventory of callable HTTP actions; deferred semantics are already on MCP/CLI and the controller XML.
- [Rejected][false] Shell completions still offer `backup create|restore|list` without an unavailable hint — those are registered stub commands that already return `ExitCodes.Error`; the inventory documents that contract.
- [Rejected][false] MCP preview JSON still emits `description`/`warning` without a published contract — `component-inventory.md` already names target, impact, and required permission; extra fields do not replace them.
- [Rejected][false] JWT detection misses plus, slash, padding, or segments shorter than 8 — standard JWTs are base64url; the current pattern already matches `eyJ…` tokens.
- [Rejected][false] CamelCase `accessToken`/`clientSecret` keys are unmatched — Admin result models have no such properties; values that are JWT-shaped are already scanned.
- [Rejected][false] Parsed `password` or `access_token` properties skip field-name redaction — current Admin models serialized through `SerializeResult` do not expose those property names.
- [Rejected][low] Userinfo URLs with an empty password are unmatched — `scheme://user:@host` is not an everyday Admin health link; the existing userinfo pattern already catches `operator:password@`.

### Review Findings — Admin-surface slice (2026-09-21, bmad-code-review)

- [x] [Review][Patch] Projection reset/replay treat post-accept UI failures as a failed mutation and tear down the dialog [src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor:531]
- [x] [Review][Patch] ConfirmationFacts can show `[redacted]` while the original identifier is still submitted [src/Hexalith.EventStore.Admin.UI/Components/Shared/ConfirmationFacts.razor:43]
- [x] [Review][Patch] Dead-letter bulk success claims completed work, including when zero matching rows were attempted [src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor:990]
- [x] [Review][Patch] Dead-letter skip facts say "marked skipped" while the dialog body says permanently removed [src/Hexalith.EventStore.Admin.UI/Resources/AdminResources.resx:18]
- [x] [Review][Patch] Reset/replay still interpolate raw projection names and call the work "an async operation" beside accepted-request ConfirmationFacts [src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor:202]
- [x] [Review][Patch] `backup-trigger` preview-match can reject composed `target`/`endpoint` after a still-bounded description [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:36]
- [x] [Review][Patch] `ValidateEndpoint` trims a trailing slash from the full URI after query strings were allowed [src/Hexalith.EventStore.Admin.UI/Services/AdminApiAccessTokenProvider.cs:348]
- [x] [Review][Patch] Bounded OIDC reads map every buffering `HttpRequestException` to "exceeded size" and drop the empty-body mapping [src/Hexalith.EventStore.Admin.UI/Services/AdminApiAccessTokenProvider.cs:316]
- [x] [Review][Patch] `RestoreAsync` runs immediately after `StateHasChanged` with no render/`HideAsync` wait [src/Hexalith.EventStore.Admin.UI/Services/InitiatorFocusService.cs:22]
- [x] [Review][Patch] New unsafe-marker detectors miss fragment secrets, token-as-username URLs, and unbounded JWT header decode [src/Hexalith.EventStore.Admin.Abstractions/Security/UnsafeMarkerDetection.cs:45]
- [x] [Review][Patch] Most destructive 401 close/restore handlers are untested; only create-backup, snapshot create/edit/create-snapshot, and dead-letter mixed retry cover `UnauthorizedAccessException` [src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor:844]
- [x] [Review][Patch] Token-client redirect lock is proven only via `AddHttpClient`, not through `AddAdminUI` composition [src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:43]
- [x] [Review][Patch] Create Snapshot Policy reuses replace-policy impact copy [src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor:155]
- [x] [Review][Defer] Browser `activeElement` after Fluent dialog teardown is not proven [src/Hexalith.EventStore.Admin.UI/Services/InitiatorFocusService.cs:22] — deferred: spec already records this pending an authenticated Admin UI E2E fixture with controllable write-denial responses; bUnit only records `hexalithAdmin.focusElementById`.
- [x] [Review][Defer] Published-UI `TokenEndpoint` / audience-parameter keys are missing from the configuration quick-scan table [docs/guides/configuration-reference.md:793] — deferred: Story 5.3 authentication content; already tracked.
- [x] [Review][Defer] CLI inventory still names `.eventstore-admin-profiles.json` [docs/brownfield/component-inventory.md:61] — deferred: unchanged pre-existing sentence; already tracked.
- [x] [Review][Defer] Replay UI still requires `from < to` while the API/MCP treat the range as inclusive [src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor:597] — deferred: pre-existing inclusive-range mismatch; already tracked.
- [x] [Review][Defer] Capability refresh clears open consistency dialogs without restoring the initiator [src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor:489] — deferred: pre-existing Story 5.3-era path; not in this slice's Consistency hunks; already tracked.
- [x] [Review][Defer] MCP maps HTTP 403 to `unauthorized` / expired-token copy [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:174] — deferred: pre-existing Admin MCP error taxonomy; this slice only wrapped the existing mapping.
- [x] [Review][Defer] Consistency cancel focus ids embed raw `checkId` [src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor:1039] — deferred: unverified whether produced check ids can contain characters that break `getElementById`; sibling backup/snapshot initiators already encode.

#### Rejected

- [Rejected][false] `session-set-context` no longer treats blank `tenantId` as "leave tenant unchanged" — empty or whitespace tenant is invalid canonical input; omitting `tenantId` (`null`) still leaves tenant unchanged, which matches Epic 5 no-trim/no-repair.
- [Rejected][false] Explicit `TokenEndpoint` skips `HasSameOrigin` — that skip is the documented trusted-origin override; discovery still origin-checks.
- [Rejected][false] Accepted-request dialogs omit `RestoreAsync` — Always/AC restore focus after cancel, validation, or denial only; success hide+reload is outside that contract.
- [Rejected][false] `OnImportFileSelected` `ReadToEndAsync` lacks `ConfigureAwait(false)` — that await was not changed; Always applies to changed production awaits.
- [Rejected][low] Query secret aliases omit hyphenated names such as `access-token` — everyday Admin OAuth/query shapes use underscore names already in `QuerySecretRegex`; adding hyphen variants is extra detector surface for a rare identifier.

### Review Findings — MCP chunk (2026-09-21, bmad-code-review)

_Story 5.4 group 2: `src/Hexalith.EventStore.Admin.Mcp` and `tests/Hexalith.EventStore.Admin.Mcp.Tests` versus `da5accfc`._

- [ ] [Review][Patch] Unpaired UTF-16 surrogates in path segments throw from `Uri.EscapeDataString` before the tool `try/catch` [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:141]
- [ ] [Review][Patch] `projection-detail` discovery still promises configuration after results always replace it with `configurationStatus` [src/Hexalith.EventStore.Admin.Mcp/Tools/ProjectionTools.cs:40]
- [x] [Review][Defer] Read and list MCP tools still interpolate or trim the same tenant and path IDs that write tools now reject [src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyTools.cs:46] — deferred: pre-existing read/list tools were not in this mutation chunk; `consistency-detail` is already tracked.
- [x] [Review][Defer] Valid tenants `admissions`, `export-stream`, and `import-stream` collide with fixed backup controller routes [src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs:35] — deferred: pre-existing controller-route ambiguity outside this story's deferred-backup boundary; already tracked.
- [x] [Review][Defer] `ValidateTenantId` allows reserved tenant `system` [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:122] — deferred: canonical grammar matches Epic 5; reserved-name rejection is Story 5.10; already tracked.

#### Rejected — MCP chunk (2026-09-21, bmad-code-review)

- [Rejected][false] `backup-trigger` preview `endpoint` omits `includeSnapshots` and `description` query parameters — `WriteToolIntentGateTests` pins preview path versus confirm URI on purpose; `parameters` already carry both values.
- [Rejected][false] Preview `checkTypes` names versus numeric JSON on the wire is a confirm/execute mismatch — the intent-gate pins `"checkTypes":[0]`; the Admin binder accepts those enum values; the named preview is the same parsed set.
- [Rejected][low] Independently legal tenant plus projection names can fail composed `target`/`endpoint` preview-match — everyday names stay under 240 characters; the 64/180 case is already asserted as `invalid-input`.
- [Rejected][false] Requiring `tenantId` on `consistency-trigger` removed fleet-wide MCP checks — that requirement is the earlier explicit-tenant patch and matches Epic 5 isolation.
- [Rejected][false] Marking `configuration`, `details`, `errorMessage`, `continuationToken`, and `cursor` as raw-capable hides operator data — Story 5.4 Never forbids cursors; descriptorizing those keys is the 2026-09-12 leak fix.
- [Rejected][false] Legacy preview `description`/`warning` keys let agents skip `requiredPermission` — `WriteToolIntentGateTests.AssertPreview` already pins `requiredPermission`; extra aliases do not replace `target`/`impact`.
- [Rejected][false] Optional `domain` is trimmed while tenant IDs are not repaired — Epic 5 forbids repairing tenants; `OptionalWhitespaceInputs_AreOmittedConsistentlyFromPreviewAndExecution` pins blank domain as omit.
- [Rejected][low] `TriggerBackup_IncludeSnapshotsFalse_FlowsThrough` does not capture the confirm query string — production always writes `includeSnapshots={true|false}`; the true default is already gated.
- [Rejected][false] `ProjectionWriteToolsTests` omit negative-position and path-segment cases — `CallerBoundary_InvalidUnsafeOverlongScopeEnumPathAndPositionInputs_PerformZeroRequests` already covers those write-tool paths.
- [Rejected][false] HTTP 400 mapping discards ProblemDetails — `EnsureSuccessStatusCode` throws `HttpRequestException` without the body; the generic `invalid-input` text is the support-safe contract.
- [Rejected][false] `ping` failures omit `error: true` after `details` moved to `message` — ping reports `adminApiStatus`; `details` became a raw-capable key, so support-safe text had to move.
