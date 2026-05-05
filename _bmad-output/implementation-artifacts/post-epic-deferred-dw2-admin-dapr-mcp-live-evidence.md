# Post-Epic Deferred DW2: Admin DAPR MCP Live Evidence

Status: ready-for-dev

<!-- Source: sprint-change-proposal-2026-05-04-deferred-work-triage.md - Proposal C / DW2 -->
<!-- Source: deferred-work.md - Admin DAPR, Epic 20 debugging, MCP, and runtime evidence deferrals through 2026-05-04 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an EventStore operator and maintainer,
I want live evidence for Admin DAPR diagnostics, Admin debugging tools, and the Admin MCP server,
so that runtime-sensitive administration features are proven in Aspire instead of trusted from unit tests and deferred review notes.

## Story Context

`deferred-work.md` has accumulated evidence gaps around Admin DAPR pages, remote sidecar metadata, health history, Epic 20 debugging tools, and Admin MCP runtime behavior. The deferred-work triage proposal groups those gaps into DW2 because they share one risk: the code is mostly implemented, but key operator workflows still need live proof against the Aspire/DAPR topology.

This story is an evidence and closure story. It should prefer running the current system, recording repeatable smoke evidence, and marking deferred-work dispositions over broad feature changes. Production code changes are allowed only when a smoke check exposes a real runtime defect or a narrow test/evidence helper is needed to make the proof repeatable. Do not absorb DW3 JSON/large-stream hardening, DW4 evidence-template validation, DW5 Admin UI runtime polish, or DW6 governance into this story.

Current HEAD at story creation: `41fc73da`.

## Acceptance Criteria

1. **Aspire runtime baseline is captured.** Given DW2 starts, when the developer runs the app with the repository's current Aspire instructions, then the evidence records the exact command, key environment flags, Aspire dashboard URL, Admin Server URL, Admin UI URL if used, CommandAPI URL, resource names, resource states, and any skipped resource such as Keycloak. If Docker, DAPR placement, scheduler, HTTPS certificate trust, or port conflicts block runtime, the evidence must classify the blocker and stop without claiming feature success.

2. **Admin DAPR component evidence covers the runtime surface.** Given the Admin API is reachable with an authorized token, when the DAPR Admin endpoints are exercised, then evidence must include observed results for components, sidecar, actors, pub/sub, resiliency, and health history. The proof must show endpoint URL, request command or browser path, expected result, observed result, and failure classification for each surface.

3. **Remote EventStore sidecar metadata status is proven.** Given Admin.Server uses local metadata for its own sidecar and optional remote EventStore sidecar metadata for actor, pub/sub, subscription, and sidecar details, when the smoke checks run, then evidence must show whether `RemoteMetadataStatus` is `Available`, `Unreachable`, or `NotConfigured` for sidecar, actors, and pub/sub. If remote metadata is unreachable, record the configured endpoint and diagnostic logs without treating local-only component results as full EventStore proof.

4. **DAPR Admin evidence does not hide degraded states.** Given any component, state-store probe, actor-state lookup, pub/sub metadata read, resiliency YAML read, or health-history query returns empty, degraded, parse-error, timeout, 404, 500, or 503, when the evidence is recorded, then the result must keep that state visible with a stable classification. Do not downgrade runtime failures to "passed" because unit tests cover the parser.

5. **Epic 20 debugging tools are smoke-tested on one seeded stream.** Given a known tenant, domain, aggregate id, event count, and correlation id are available from a seeded command flow, when the Admin debugging tools are exercised, then evidence must cover blame, bisect, step-through, sandbox, and trace map against that same stream or correlation. Each check must record input identifiers, expected shape, observed shape, truncation flags, timeout status, and whether the result was produced by Admin UI, Admin API, MCP, or a direct CommandAPI endpoint.

6. **Debugging evidence respects known scope limits.** Given blame, bisect, step-through, sandbox, and trace-map tools still have known JSON merge/delete and large-stream limitations, when DW2 closes, then the evidence must not claim those limitations are fixed. Failures or ambiguous results caused by JSON reconstruction, array diff semantics, missing delete detection, full-stream reads, or unbounded direct CommandAPI access must be dispositioned to DW3 or accepted as existing deferred work, not patched opportunistically in DW2.

7. **MCP startup and protocol smoke evidence is captured.** Given `Hexalith.EventStore.Admin.Mcp` is started with `EVENTSTORE_ADMIN_URL` and `EVENTSTORE_ADMIN_TOKEN`, when the MCP client initializes, then evidence must include startup command, environment redaction policy, initialize result, `tools/list` result, server name/version, and any stderr diagnostics. Stdout must remain reserved for MCP JSON-RPC protocol messages.

8. **Representative MCP read and write-preview tools are exercised.** Given the Admin API is reachable through the MCP server, when one representative read tool and one approval-gated write-preview tool are invoked, then evidence must show the JSON-RPC request shape, tool name, required arguments, expected result, observed result, and whether the write-preview avoided executing a destructive operation. Use existing tool contracts; do not add new tools unless an existing essential tool is missing.

9. **MCP session fallback is proven.** Given `InvestigationSession` provides tenant/domain context fallback for optional tool parameters, when the smoke uses session context and then omits supported optional scope arguments, then evidence must show that the follow-up tool call uses the session values. If no existing session tool can establish the context, record the gap as a precise defect or deferred decision instead of fabricating fallback proof.

10. **NFR43 MCP latency plan is explicit.** Given the deferred-work triage proposal calls out NFR43 single-resource MCP tool latency, when DW2 closes, then the evidence must include a repeatable latency measurement plan and at least one measured sample for a single-resource read tool. If the sample is local-machine-only, mark it as sample evidence, not a product SLA. Record timing method, warm/cold state, retry treatment, and whether Admin API latency is included.

11. **Evidence artifacts are durable and reviewable.** Store DW2 evidence under `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/`. At minimum include an index or evidence markdown file with tables for runtime baseline, Admin DAPR checks, Epic 20 debugging checks, MCP checks, latency samples, blockers, and deferred-work dispositions. Screenshots, console logs, JSON responses, or sanitized transcripts may be linked from that folder when useful.

12. **Deferred-work dispositions are updated narrowly.** Given DW2 evidence closes or routes one of the relevant bullets in `_bmad-output/implementation-artifacts/deferred-work.md`, when the story is moved to review, then each touched bullet must receive a clear disposition marker such as `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`, `RESOLVED`, `ACCEPTED-DEBT`, `DUPLICATE`, or `NO-ACTION`. Do not rewrite unrelated deferred-work sections or sweep old items that belong to DW6 governance.

13. **Security and redaction are preserved.** Given runtime evidence may include tokens, URLs, tenant ids, aggregate ids, event metadata, and MCP JSON-RPC payloads, when artifacts are saved, then bearer tokens, secrets, event payload data, and customer-sensitive values must be redacted. Envelope metadata needed to reproduce proof may be included when safe. Do not log JWTs, DAPR component secrets, state-store values, or command/event payloads.

14. **No deployment topology or product contract changes are introduced by evidence work.** DW2 must not change DAPR component YAML, access-control policy, public Admin API contracts, MCP protocol semantics, EventStore command/query contracts, Admin UI navigation, or production publish overlays unless a failing smoke check exposes a blocking runtime defect and the fix is narrow and tested. Any pressure to do those things belongs in DW3, DW4, DW5, or a new product/architecture decision.

15. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Change Log, Verification Status, and any new deferred-work dispositions. Move this story and its sprint-status row to `review` only after evidence artifacts and targeted validation are recorded. Move both to `done` only after code review signoff.

## Scope Boundaries

- Do not implement the DW3 JSON reconstruction or large-stream hardening work.
- Do not build the DW4 evidence-template validator.
- Do not fix Admin UI visual/accessibility polish items unless they directly block the DW2 smoke path.
- Do not change DAPR access-control YAML, component YAML, sidecar app ids, or publish overlays.
- Do not add broad performance/load tests; measure only the minimum MCP single-resource latency sample required by this story.
- Do not add new MCP tools when an existing tool can prove the runtime path.
- Do not run nested submodule initialization.
- Do not edit generated preflight JSON audit files.

## Implementation Inventory

| Area | File / artifact | Expected use |
|---|---|---|
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md` | DW2 scope and acceptance direction |
| Deferred source | `_bmad-output/implementation-artifacts/deferred-work.md` | raw Admin/DAPR/MCP/runtime evidence deferrals to close or route |
| AppHost | `src/Hexalith.EventStore.AppHost/Program.cs` | Aspire resource names, DAPR sidecars, Keycloak toggle, endpoint wiring |
| Admin DAPR controller | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs` | Admin DAPR API endpoints for components, sidecar, actors, pub/sub, resiliency |
| Admin health controller | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminHealthController.cs` | health-history endpoint surface |
| DAPR query service | `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs` | local/remote metadata behavior and resiliency YAML parsing |
| Health history service | `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs` and `DaprHealthHistoryCollector.cs` | component health snapshots and query bounds |
| Debugging services | `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` | blame, bisect, step, sandbox, trace-map service invocation timeouts |
| Debugging controllers | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` and `AdminTracesController.cs` | HTTP proof paths for Epic 20 tools |
| Admin UI pages | `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor` and related components | optional browser proof for debugging workflows |
| MCP host | `src/Hexalith.EventStore.Admin.Mcp/Program.cs` | stdio server startup, required environment variables, stderr logging |
| MCP tools | `src/Hexalith.EventStore.Admin.Mcp/Tools/*.cs` | `ping`, read tools, approval-gated write-preview tools, session behavior |
| Evidence folder | `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/` | required runtime proof artifacts |

## Current Code Intelligence

- AppHost defines resources `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, optional `keycloak`, `tenants`, and optional `eventstore-test-subscriber`. `EnableKeycloak=false` is a supported dev path using symmetric JWT fallback.
- `AdminDaprController` exposes `GET /api/v1/admin/dapr/components`, `/sidecar`, `/actors`, `/actors/{actorType}/state?id=...`, `/pubsub`, and `/resiliency` under the read-only admin authorization policy.
- `DaprInfrastructureQueryService` intentionally uses local DAPR SDK metadata for the Admin.Server sidecar and remote HTTP metadata from `AdminServerOptions.EventStoreDaprHttpEndpoint` for EventStore actors, pub/sub components, and subscriptions. Evidence must distinguish those sources.
- `GetPubSubOverviewAsync` parses the DAPR v1.17 direct `rules[]` subscription shape and tolerates the older wrapped `rules.rules[]` test-fixture shape. Runtime evidence should capture which shape appears in the live metadata payload if raw JSON is saved.
- `GetResiliencySpecAsync` reads the AppHost-injected `ResiliencyConfigPath`, caps file size at 1 MB, and reports unavailable/not-found/read-error/parse-error states. A parser unit test is not a substitute for live proof that the AppHost-injected path resolves.
- `DaprHealthHistoryCollector` captures component health periodically when `HealthHistoryEnabled` is true. A live proof may need to wait at least one capture interval or record why the timeline is empty.
- Epic 20 debugging paths in `DaprStreamQueryService` use longer per-call timeouts for expensive operations: blame/step/trace map use 30 seconds, bisect uses 60 seconds. Evidence must record timeouts distinctly from empty but successful results.
- The MCP host requires `EVENTSTORE_ADMIN_URL` and `EVENTSTORE_ADMIN_TOKEN`, logs to stderr, and reserves stdout for JSON-RPC. This is a protocol requirement for the smoke transcript.
- MCP server tools are assembly-discovered through `WithToolsFromAssembly()`. `tools/list` should therefore be the source of truth for which tool names are available in the live smoke.

## Latest Technical Notes

- DAPR metadata API v1.17 is the current stable reference for sidecar metadata and includes app id, components, actors, subscriptions, and extended metadata surfaces relevant to this story. Source: [DAPR metadata API](https://docs.dapr.io/reference/api/metadata_api/)
- The Model Context Protocol lifecycle requires initialize negotiation before normal client/server operation, so MCP smoke evidence should include initialize before tool calls. Source: [MCP lifecycle](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle)
- The MCP tools specification defines `tools/list` discovery and tool invocation behavior. Use the live `tools/list` response instead of hardcoding expected tool names from source inspection. Source: [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)

## Tasks / Subtasks

- [ ] Task 0: Baseline and evidence plan (AC: #1, #11, #13, #14)
    - [ ] 0.1 Re-read Proposal C / DW2 and the relevant Admin/DAPR/MCP entries in `deferred-work.md`.
    - [ ] 0.2 Create `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/`.
    - [ ] 0.3 Define an evidence index before running checks. The index must map each acceptance criterion to the command or tool used, target resource, dated artifact path, expected result, observed result, result classification, redaction note, and deferred-work disposition.
    - [ ] 0.4 Define evidence tables before running checks: runtime baseline, Admin DAPR, Epic 20 debugging, MCP, latency, blockers, dispositions, and "how to rerun" commands.
    - [ ] 0.5 Define redaction rules for tokens, secrets, payloads, customer identifiers, and raw state values.
    - [ ] 0.6 Define blocker classes before running checks: environment blocker, pre-existing product defect, story defect, known deferred debt, out-of-scope DW3-DW6 work, and evidence gap.

- [ ] Task 1: Run Aspire and capture resource baseline (AC: #1, #13)
    - [ ] 1.1 Start prerequisites according to repo guidance: Docker, DAPR placement, and DAPR scheduler as needed for the environment.
    - [ ] 1.2 Run `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` unless the developer intentionally chooses Keycloak proof.
    - [ ] 1.3 Record resource names, states, endpoints, dashboard URL, Admin Server URL, Admin UI URL if used, and CommandAPI URL.
    - [ ] 1.4 Record any runtime blocker with exact failing command/output excerpt and stop affected checks rather than claiming pass.
    - [ ] 1.5 Keep a stoplight table for each prerequisite and resource: passed, degraded-but-continue, or blocked-stop-here, with the dependent checks listed explicitly.

- [ ] Task 2: Capture Admin DAPR live evidence (AC: #2, #3, #4, #11, #13)
    - [ ] 2.1 Generate or obtain a dev JWT with read-only/admin claims without storing the token in evidence.
    - [ ] 2.2 Exercise `/api/v1/admin/dapr/components`, `/sidecar`, `/actors`, `/pubsub`, and `/resiliency`.
    - [ ] 2.3 Exercise health summary and component health-history endpoints after a bounded wait for at least one capture interval, or record empty-timeline classification.
    - [ ] 2.4 For sidecar, actors, and pub/sub, record `RemoteMetadataStatus` and `RemoteEndpoint` evidence separately from local component metadata.
    - [ ] 2.5 Record a `RemoteMetadataStatus` matrix for sidecar, actors, and pub/sub. Each surface must have a dated row for the observed `Available`, `Unreachable`, or `NotConfigured` state; do not use one global degraded-state result as proof for all surfaces.
    - [ ] 2.6 If a browser is used, capture screenshots only after redaction review and link them from the evidence index.

- [ ] Task 3: Capture Epic 20 debugging live evidence (AC: #5, #6, #11, #13)
    - [ ] 3.1 Seed one deterministic stream through CommandAPI or an existing sample flow and record tenant, domain, aggregate id, event count, and correlation id.
    - [ ] 3.2 Exercise blame, step-through, bisect, sandbox, and trace-map paths against the same seeded stream or correlation.
    - [ ] 3.3 Record expected shape and observed shape for each result, including truncation flags, timeout/failure state, whether evidence came from Admin API, Admin UI, MCP, or direct CommandAPI, and the canonical identifier block reused across all related artifacts.
    - [ ] 3.4 Route JSON reconstruction or large-stream concerns to DW3 instead of fixing them in this story.

- [ ] Task 4: Capture MCP live smoke and latency evidence (AC: #7, #8, #9, #10, #11, #13)
    - [ ] 4.1 Start `Hexalith.EventStore.Admin.Mcp` with `EVENTSTORE_ADMIN_URL` and a redacted `EVENTSTORE_ADMIN_TOKEN`.
    - [ ] 4.2 Capture initialize and `tools/list` transcript with stdout/stderr separated.
    - [ ] 4.3 Invoke one representative read tool, preferably `ping`, `health-status`, `health-dapr`, `stream-list`, or `stream-events` based on seeded data availability.
    - [ ] 4.4 Invoke one approval-gated write-preview tool without executing destructive changes; record the request, approval boundary, denied or unapproved behavior, sanitized preview output, and a before/after proof pair that no mutation occurred.
    - [ ] 4.5 Prove session context fallback for tenant/domain if supported. If fallback is absent, classify it explicitly as `feature absent`, `feature broken`, or `blocked by missing session-establishment path`; include reproduction steps and evidence path instead of implementing a new fallback design in this story.
    - [ ] 4.6 Measure at least one single-resource MCP read tool with a documented timing method, timer source, sample count, cold/warm state, retry treatment, min/average/max or raw individual durations, local environment caveat, and whether initialization and Admin API latency are included.

- [ ] Task 5: Close deferred-work and validation bookkeeping (AC: #11, #12, #15)
    - [ ] 5.1 Update only DW2-relevant `deferred-work.md` bullets with disposition markers.
    - [ ] 5.2 Run targeted tests only if production/test helper code changed. For evidence-only changes, validate markdown, links, and artifact completeness where tooling exists.
    - [ ] 5.3 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and evidence artifact references.
    - [ ] 5.4 Move this story and sprint-status row to `review` only after evidence is saved and blockers are classified.

## Dev Notes

### Architecture Guardrails

- Admin.Server is the shared backend for Admin UI, CLI, and MCP. Prefer proving existing Admin API surfaces instead of adding tool-specific bypasses.
- DAPR access control remains the service-to-service boundary. Do not relax sidecar policy to make smoke checks pass.
- DAPR component and metadata proof must distinguish local Admin.Server sidecar state from remote EventStore sidecar state.
- DAPR resiliency and component YAML are configuration contracts. Do not edit them for this evidence story unless an actual defect blocks all proof and the fix is reviewed as product/runtime behavior.
- Evidence can include endpoint names, status codes, component names, app ids, actor types, and envelope metadata, but not bearer tokens, component secrets, raw actor state, or event payloads.
- MCP stdout is protocol traffic. Send diagnostics to stderr and keep transcripts separated so protocol regressions are visible.
- Production code changes must pass the DW2 defect gate: a live smoke check proves an existing narrow defect, the fix is required to complete DW2 evidence, the defect and before/after evidence are recorded, and the change does not alter deployment topology, public Admin API contracts, MCP protocol or tool contracts, JSON reconstruction behavior, large-stream behavior, DAPR component/access-control YAML, or deferred DW3-DW6 scope.

### Party-Mode Review Clarifications

- Treat the evidence index as the contract for review. Every artifact under `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/` must be linked from the index with timestamp, command or tool, target resource, redaction note, result classification, and related blocker or deferred-work disposition.
- Preserve degraded states as first-class outcomes. Empty data, unavailable remote metadata, parse errors, timeouts, 404, 500, 503, and health-history gaps must remain visible as classified results instead of being collapsed into pass/fail prose.
- Use one deterministic seeded stream and correlation id for blame, bisect, step-through, sandbox, and trace-map evidence. Redact payloads, but keep tenant, domain, aggregate id, event count, correlation id, and structural metadata needed to reproduce the smoke.
- Keep MCP proof split by protocol phase: startup environment, initialize, `tools/list`, representative read tool, approval-gated write-preview behavior, session fallback, and latency sample. A tool-discovery success alone is not MCP evidence.
- If Admin UI is used for optional evidence, limit UI review to the tested path: no hidden critical status, keyboard reachability for tested controls, and no new unlabeled controls or hard-coded strings beyond existing patterns. Broader Admin UI polish remains DW5 scope.

## Advanced Elicitation Clarifications

The 2026-05-05 advanced-elicitation pass kept DW2 as an evidence-first story and tightened only the runtime proof contract. These notes are binding for dev-story execution unless a human product or architecture decision supersedes them.

### Runtime Evidence Guardrails

- Capture the runtime baseline as a stoplight table, not free prose: command, prerequisite state, target resource, expected result, observed result, blocker class, and whether later checks may continue.
- A blocked prerequisite stops only the dependent evidence slice. Remote EventStore metadata failure does not erase local Admin.Server component proof; missing seeded-stream data does not invalidate DAPR baseline evidence.
- When a smoke check fails, preserve the first failure shape before retrying. Later retries may be recorded, but the evidence index must keep the original degraded result visible.

### Cross-Surface Consistency Rules

- Use one canonical identifier block for seeded-stream evidence and repeat it across Admin API, Admin UI, MCP, and CommandAPI artifacts so reviewers can prove every tool targeted the same stream or correlation.
- Remote metadata evidence must remain per-surface. Sidecar, actors, and pub/sub each need an independent `RemoteMetadataStatus` row even when they share the same remote endpoint and failure mode.
- MCP write-preview proof needs a before/after non-mutation check. It is not enough to record "approval required"; the evidence should show the preview path stopped before any state change and that the inspected resource remained unchanged.
- Session fallback proof must distinguish `feature absent`, `feature broken`, and `blocked by missing session-establishment path`. Only the first is a deferred decision; the second is a defect.

### Latency and Validation Requirements

- The NFR43 sample must name the timer source, cold/warm state, sample count, and whether initialization time is included. If only one sample is practical, call it out as a single-sample baseline rather than implying statistical confidence.
- If production code is unchanged, validation for DW2 should prefer markdown/link checks plus artifact completeness review. Do not manufacture test churn to satisfy bookkeeping.
- If a live defect requires a narrow fix, record the before/after evidence pair and the exact acceptance criteria it unlocks, then stop once the blocked proof path is recovered.

### Previous Story Intelligence

- DW1 established the pattern for grouped deferred-work cleanup stories: close only the selected cluster, route unrelated pressure to later DW stories, and use dispositions rather than large rewrites of `deferred-work.md`.
- Epic 19 DAPR stories created the Admin DAPR runtime surfaces. Their code-review deferrals emphasize that remote metadata availability and pub/sub subscription shape must be proven live, not inferred from parser tests.
- Epic 20 debugging stories shipped blame, bisect, step-through, sandbox, and trace-map features with known limits around JSON reconstruction and full-stream reads. DW2 should prove runtime behavior without absorbing those known design gaps.
- Epic 18 MCP stories established stdio transport, read tools, diagnostic tools, write tools with approval gates, and session state. DW2 should prove startup, discovery, a representative read path, write-preview safety, and session fallback rather than inventing a new MCP protocol harness.

### Testing Guidance

- This story's primary validation is live evidence, not broad unit test expansion.
- If code changes are required, add the smallest targeted tests in the relevant Admin.Server, Admin.Mcp, Admin.UI, or integration test project.
- Run test projects individually per repository guidance. Do not run solution-level `dotnet test` as the validation gate.
- When using browser proof, use the configured Playwright/browser tooling and save screenshots or observations under this story's test-artifacts folder.
- When using command-line proof, capture commands and sanitized outputs in markdown rather than relying on memory.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md#Proposal-C-DW2-Admin-DAPR-and-MCP-Live-Evidence`] - DW2 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md`] - raw Admin/DAPR/MCP/runtime evidence deferrals.
- [Source: `_bmad-output/implementation-artifacts/19-1-dapr-component-status-dashboard.md`] - component-status dashboard context.
- [Source: `_bmad-output/implementation-artifacts/19-2-dapr-actor-inspector.md`] - actor inspector context.
- [Source: `_bmad-output/implementation-artifacts/19-3-dapr-pubsub-delivery-metrics.md`] - pub/sub admin context.
- [Source: `_bmad-output/implementation-artifacts/19-4-dapr-resiliency-policy-viewer.md`] - resiliency viewer context.
- [Source: `_bmad-output/implementation-artifacts/19-5-dapr-component-health-history.md`] - health-history context.
- [Source: `_bmad-output/implementation-artifacts/19-6-admin-dapr-metadata-diagnostics.md`] - remote metadata diagnostics context.
- [Source: `_bmad-output/implementation-artifacts/20-1-blame-view-per-field-provenance.md`] - blame tool context.
- [Source: `_bmad-output/implementation-artifacts/20-2-bisect-tool-binary-search-state-divergence.md`] - bisect tool context.
- [Source: `_bmad-output/implementation-artifacts/20-3-step-through-event-debugger.md`] - step-through debugger context.
- [Source: `_bmad-output/implementation-artifacts/20-4-command-sandbox-test-harness.md`] - sandbox context.
- [Source: `_bmad-output/implementation-artifacts/20-5-correlation-id-trace-map.md`] - trace-map context.
- [Source: `_bmad-output/implementation-artifacts/18-1-mcp-server-scaffold-stdio-transport.md`] - MCP stdio host context.
- [Source: `_bmad-output/implementation-artifacts/18-2-read-tools-stream-state-projection-schema-metrics.md`] - MCP read-tool context.
- [Source: `_bmad-output/implementation-artifacts/18-4-write-tools-with-approval-gates.md`] - MCP write-preview context.
- [Source: `_bmad-output/implementation-artifacts/18-5-tenant-context-and-investigation-session-state.md`] - MCP session fallback context.
- [Source: `src/Hexalith.EventStore.AppHost/Program.cs`] - Aspire topology and runtime flags.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`] - current DAPR metadata and resiliency behavior.
- [Source: `src/Hexalith.EventStore.Admin.Mcp/Program.cs`] - MCP startup and stdio behavior.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-04T18:01:38Z`, result `pass`.
- Party-mode pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-04T18:44:30Z`, result `pass`.

### Completion Notes List

- Created ready-for-dev story from first backlog row after DW1 in the Post-Epic Deferred Work Cleanup package.
- No implementation work has been performed for this story.
- No `project-context.md` file was present in the repository at story creation.
- Party-mode review applied low-risk pre-dev clarifications for evidence index format, blocker taxonomy, RemoteMetadataStatus matrix, MCP approval proof, latency sampling, seeded-stream consistency, degraded-state visibility, and the production-defect gate.
- Advanced elicitation applied low-risk handoff clarifications for stoplight runtime gating, cross-surface identifier consistency, non-mutation MCP preview proof, session-fallback classification, and latency evidence shape.

### Party-Mode Review - 2026-05-04T20:48:20+02:00

- Selected story key: `post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`
- Command/skill invocation used: `/bmad-party-mode post-epic-deferred-dw2-admin-dapr-mcp-live-evidence; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary: The story is viable as `ready-for-dev`, but review found decision-budget drift risk unless DW2 starts with a strict evidence index, fixed blocker taxonomy, independent RemoteMetadataStatus matrix, explicit MCP approval-boundary proof, precise NFR43 latency sampling, and a narrow production-defect gate.
- Changes applied: Added evidence-index mapping requirements; added blocker classes; clarified RemoteMetadataStatus matrix evidence; clarified MCP write-preview no-mutation proof; clarified session-fallback defect handling; clarified latency sample fields; added party-mode clarifications for degraded-state visibility, deterministic seed evidence, MCP phase separation, and optional Admin UI limits.
- Findings deferred: DW3 JSON reconstruction and large-stream hardening; DW4 evidence-template validation; DW5 broader Admin UI polish; DW6 governance; deployment topology, public Admin API, MCP contract, DAPR YAML, and access-control changes unless a narrow smoke-proven defect blocks DW2 evidence.
- Final recommendation: ready-for-dev

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Party-mode review and advanced elicitation traces are recorded inline; no status change was required.
- Markdown and YAML validation should be run before dev handoff if local tooling is available.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-05 | 0.3 | Applied advanced-elicitation hardening for runtime stoplight gating, MCP proof shape, and latency evidence requirements. | Codex automation |
| 2026-05-04 | 0.2 | Applied party-mode pre-dev review clarifications for DW2 evidence contract and scope gates. | Codex automation |
| 2026-05-04 | 0.1 | Created ready-for-dev DW2 Admin DAPR MCP live evidence story. | Codex automation |

### Advanced Elicitation - 2026-05-05T05:42:00+02:00

- Selected story key: `post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`
- Batch 1 method names: Pre-mortem Analysis; Red Team vs Blue Team; Comparative Analysis Matrix; Critique and Refine; Challenge from Critical Perspective
- Reshuffled Batch 2 method names: Architecture Decision Records; Self-Consistency Validation; Occam's Razor Application; Lessons Learned Extraction; Failure Mode Analysis
- Findings summary: The story was already viable after party-mode review, but elicitation exposed five handoff gaps: prerequisite failures needed an explicit stoplight continuation rule; seeded-stream evidence needed a shared identifier block across surfaces; MCP write-preview proof needed before/after non-mutation evidence; session fallback needed clearer absent-versus-broken classification; and NFR43 evidence needed a tighter latency-sampling contract.
- Changes applied: Added Advanced Elicitation Clarifications for runtime evidence guardrails, cross-surface consistency rules, and latency-validation requirements. Tightened Tasks 1.5, 3.3, 4.4, 4.5, 4.6, and 5.2, and updated Completion Notes, Verification Status, and Change Log.
- Findings deferred: Any production defect found during DW2 smoke remains subject to the existing narrow defect gate; DW3 JSON/large-stream work, DW4 evidence-template validation, DW5 Admin UI polish, DW6 governance, public Admin API or MCP contract changes, and deployment-topology/DAPR YAML changes remain out of scope unless a smoke-proven blocker forces a separately reviewed follow-up.
- Final recommendation: ready-for-dev
