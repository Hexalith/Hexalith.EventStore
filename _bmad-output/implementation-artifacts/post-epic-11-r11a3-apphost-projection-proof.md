# Post-Epic-11 R11-A3: AppHost Projection Proof

Status: review

<!-- Source: epic-11-retro-2026-04-30.md - Action item R11-A3 -->
<!-- Source: epic-12-retro-2026-04-30.md - R12-A5 carry-forward backlog -->
<!-- Source: epic-10-retro-2026-05-01.md - Action item R10-A1 live SignalR topology proof routed here by sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md -->
<!-- Source: sprint-change-proposal-2026-04-30-opentelemetry-audit-fix.md - AppHost proof unblocked -->
<!-- Source: docs/guides/sample-blazor-ui.md - Smoke-Test Evidence Pattern -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer hardening the server-managed projection builder,
I want a trace-backed AppHost proof of the complete sample projection path,
so that Epic 11 confidence is based on running evidence across command submission, event persistence, `/project`, projection actor write, query, ETag, SignalR, and UI refresh.

## Story Context

Epic 11 shipped the Mode B server-managed projection path, but its retrospective explicitly recorded that no fresh full AppHost projection proof was captured. Epic 12 partially addressed the sample UI behavior, but it did not capture runtime logs or traces proving the full server-managed path. R11-A3 closes that evidence gap.

This is a verification and evidence story, not a feature story. Reuse the current AppHost topology in `src/Hexalith.EventStore.AppHost/Program.cs`: `eventstore`, `sample`, `sample-blazor-ui`, DAPR sidecars, Redis-backed `statestore`/`pubsub`, and SignalR enabled by `EventStore__SignalR__Enabled=true`. Do not add a second projection path, bypass DAPR, or replace the sample UI flow with a synthetic-only test unless the running proof exposes a real blocker.

## Acceptance Criteria

1. **AppHost starts from the documented developer command.** Run `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` or document why the equivalent `dotnet run --project src/Hexalith.EventStore.AppHost` path was used. `eventstore`, `sample`, `sample-blazor-ui`, `statestore`, and `pubsub` must be running before proof begins. If DAPR slim mode requires manual `placement` and `scheduler`, record the exact commands.

2. **Resource evidence is captured before exercising the flow.** Paste the Aspire resource snapshot into this story's Dev Agent Record or `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/`. The snapshot must include endpoint URLs used for EventStore and the sample Blazor UI, the effective sample UI tenant/projection settings used during the browser proof, the proof-window start timestamp, and the unique command `messageId` / correlation ID that will be followed through the rest of the evidence.

3. **Command submission is proven through the real HTTP API or sample UI.** Submit at least one `IncrementCounter` command for tenant `sample-tenant`, domain `counter`, aggregate `counter-1`, and record the HTTP response status, correlation ID, `Location`, and `Retry-After` headers. A green UI submission alone is not sufficient because docs state it only proves HTTP acceptance; if the sample UI is used, capture the browser network request/response or pair it with a direct API submission that records those headers.

4. **Event persistence is evidenced.** Record either an Aspire trace/log entry or API evidence showing the accepted command reached event persistence. The evidence must include the same proof identity from AC #2: command `messageId`, correlation ID, aggregate ID, and timestamp close enough to the submitted command to prevent accidentally stitching together evidence from separate runs.

5. **The sample `/project` endpoint is invoked.** Capture logs or traces showing EventStore invoked `sample` `POST /project` with a `ProjectionRequest` tied to the same proof identity. The proof must confirm the sample endpoint is the real handler in `samples/Hexalith.EventStore.Sample/Program.cs`, not the malformed-response fault path.

6. **Projection actor write is evidenced.** Capture logs or traces showing `EventReplayProjectionActor.UpdateProjectionAsync` accepted the returned projection state for projection type `counter` and tenant `sample-tenant`.

7. **ETag regeneration is evidenced.** Capture logs, response headers, or trace evidence showing a new ETag was generated after the projection write. A query response with an `ETag` header is acceptable only when it is tied to the same command/projection proof and the ETag is shown to route to the `counter` projection, either by self-routing decode evidence or matching server log fields.

8. **Query result returns projected state.** Call `POST /api/v1/queries` or use the sample UI query path and prove the returned payload includes the expected counter count after the command. The query must use domain/projection `counter`, tenant `sample-tenant`, aggregate/entity `counter-1`, and query type `get-counter-status`. Capture a baseline count before command submission and an after count after projection delivery; if pre-existing state makes the baseline non-zero, prove the expected delta rather than assuming the final count should be `1`.

9. **SignalR invalidation is evidenced.** Capture either server log evidence from `SignalRProjectionChangedBroadcaster`, sample UI log evidence from `SignalRClientStartup` / refresh callbacks, or browser-visible behavior showing the projection change signal arrived. SignalR carries no projection data; the proof must show the client re-queried or refreshed after the signal. A manual refresh or a direct query proving updated state can support AC #8, but it must not be counted as AC #9 unless SignalR delivery or refresh-after-signal evidence is present; missing SignalR evidence is a blocker or deferred owner decision, not a pass.

10. **Sample UI refresh behavior is evidenced with artifacts.** Open the sample Blazor UI from the Aspire endpoint and exercise at least one refresh pattern page. Prefer `/pattern-silent-reload` because it should update after a signal without manual refresh. Include a screenshot or browser notes showing before/after count, and preserve either the browser query request or page configuration evidence proving the UI used tenant `sample-tenant`, projection `counter`, and aggregate/entity `counter-1`. If browser automation is unavailable, record the exact manual steps and observed result.

11. **No projection-path regression errors are present.** During the bounded proof window from AC #2 through the final query/UI refresh, `eventstore` logs must not contain `QueryNotFoundException`, malformed projection response errors, unhandled `ProjectionUpdateOrchestrator` failures, or SignalR hub mapping failures. Record the log source, time range, and filter terms used for the negative search. If unrelated startup warnings exist, classify them separately and do not hide projection failures.

12. **Evidence is repeatable.** Create `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/README.md` with the commands, endpoints, payloads, timestamps, screenshots/log references, and known caveats needed to repeat the proof. Include an AC evidence matrix mapping AC #1-#13 to the exact artifact file, log excerpt, screenshot, trace ID, or caveat that satisfies it.

13. **No product scope is silently expanded.** If the proof fails because R11-A1 or R11-A2 has not landed, record the blocker and leave the fix to those stories. This story may add a small verification script or runbook only if it reduces evidence drift; it must not implement checkpoint tracking, polling mode, or new projection contracts.

## Tasks / Subtasks

- [x] Task 1: Prepare runtime proof environment (AC: #1, #2)
  - [x] Start Docker if needed and record whether DAPR placement/scheduler were started manually.
  - [x] Start the AppHost with `EnableKeycloak=false` unless validating the Keycloak path is explicitly needed.
  - [x] Capture resource state and endpoint URLs before submitting commands.
  - [x] Pick and record one unique command `messageId`, correlation ID, proof-window start timestamp, and aggregate ID for the evidence chain.

- [x] Task 2: Submit the counter command through the real surface (AC: #3)
  - [x] Use the sample UI command form or `POST /api/v1/commands`.
  - [x] Record command payload, HTTP status, correlation ID, `Location`, and `Retry-After`.
  - [x] If using the sample UI path, capture browser network evidence for the command response headers because the visible form only reports submission success/failure.
  - [x] Preserve JWT/auth setup details if direct API calls are used.

- [x] Task 3: Capture server-side projection evidence (AC: #4, #5, #6, #7, #11)
  - [x] Capture command/event persistence logs or traces tied to the correlation ID.
  - [x] Capture `/project` invocation against the `sample` resource.
  - [x] Capture projection actor write and ETag regeneration evidence.
  - [x] Search logs for projection-path errors during the bounded proof window and record the source, time range, and filter terms.

- [x] Task 4: Prove query and UI refresh behavior (AC: #8, #9, #10)
  - [x] Capture the baseline counter count before command submission.
  - [x] Query counter state through `POST /api/v1/queries` or the sample UI query service.
  - [x] Exercise `/pattern-silent-reload` and record before/after count.
  - [x] Preserve query request evidence showing tenant `sample-tenant`, domain/projection `counter`, aggregate/entity `counter-1`, and query type `get-counter-status`.
  - [x] Capture SignalR or refresh-after-signal evidence, not just command submission feedback or manual reload behavior.

- [x] Task 5: Persist repeatable evidence (AC: #12, #13)
  - [x] Create `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/README.md`.
  - [x] Add screenshots, log excerpts, trace IDs, payloads, and caveats under the same folder.
  - [x] Add an AC evidence matrix that maps AC #1-#13 to concrete artifact paths or explicit blockers.
  - [x] If a blocker is found, record the blocker precisely and route it to the owning follow-up story.

- [x] Task 6: Run validation checks (AC: #11, #12)
  - [x] `dotnet test tests/Hexalith.EventStore.Sample.Tests`
  - [x] Optional, if server-side projection code changed: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~ProjectionUpdateOrchestratorTests|FullyQualifiedName~EventReplayProjectionActorTests"`
  - [x] Record all test results in the Dev Agent Record.

## Dev Notes

### Existing Implementation To Reuse

- `src/Hexalith.EventStore.AppHost/Program.cs` wires `eventstore`, `sample`, `sample-blazor-ui`, DAPR sidecars, and `EventStore__SignalR__Enabled=true`.
- `samples/Hexalith.EventStore.Sample/Program.cs` maps the real `/project` endpoint to `CounterProjectionHandler.Project(request)` unless `EventStore:SampleFaults:MalformedProjectResponse` is enabled.
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs` returns `ProjectionResponse("counter", { count })` after applying counter events.
- `src/Hexalith.EventStore/Controllers/CommandsController.cs` exposes `POST /api/v1/commands`, returns `202 Accepted`, `Location`, and `Retry-After`.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` exposes `POST /api/v1/queries`, supports `If-None-Match`, and sets an `ETag` response header when available.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs` queries `/api/v1/queries` for tenant `sample-tenant`, domain `counter`, aggregate/entity `counter-1`, and query type `get-counter-status`.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor` subscribes to SignalR and reloads after projection change notifications.
- `docs/guides/sample-blazor-ui.md#smoke-test-evidence-pattern` defines the minimum evidence format. Use it instead of writing a free-form "smoke test passed" note.

### Implementation Guardrails

- Do not treat a successful command submit as projection proof. Command acceptance happens before domain processing, event persistence, projection writes, ETag regeneration, SignalR delivery, and UI refresh.
- Do not call the sample domain service `/project` directly as the only proof. Direct calls can validate the handler, but R11-A3 requires EventStore-driven DAPR service invocation.
- Do not use SignalR as the source of projection state. SignalR is an invalidation signal; the Query API response is the state proof.
- Do not add new public contracts, change `ProjectionRequest` / `ProjectionResponse`, or alter `EventReplayProjectionActor` unless the proof identifies a real defect that blocks R11-A3.
- Do not initialize nested submodules. If any submodule setup is needed, update only root-level submodules and record the command.
- Keep proof artifacts under `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/`.

### Suggested Evidence Shape

Minimum artifact set:

- `README.md` with run command, environment, resource snapshot, endpoints, command payload, query payload, results, and caveats.
- `resources-before.txt` and, if useful, `resources-after.txt`.
- `eventstore-logs.txt`, `sample-logs.txt`, and `sample-blazor-ui-logs.txt` excerpts scoped to the proof window.
- `trace-ids.txt` listing trace IDs or correlation IDs used to connect command, `/project`, projection actor, query, ETag, and SignalR evidence.
- `silent-reload-before.png` and `silent-reload-after.png` or a documented reason screenshots were unavailable.

### Architecture And Version Notes

- Package versions are centrally pinned in `Directory.Packages.props`: Dapr `1.17.7`, Aspire `13.2.2`, .NET extensions `10.x`, xUnit v3, Shouldly, and NSubstitute. Do not add packages for a proof-only story unless a verification script cannot be written with the existing stack.
- Aspire CLI is the intended local orchestration entry point for this repository. Use the project-specific command from `AGENTS.md` when possible.
- DAPR actor runtime is single-threaded per actor turn. The proof should capture runtime behavior, not infer it from unit tests.
- DAPR state ETags are opaque strings and state stores may support optimistic concurrency. For R11-A3, only observe ETag generation/query headers; do not introduce state-store implementation details.

### Previous Story Intelligence

- R11-A1 and R11-A2 are ready-for-dev predecessor hardening stories. If they have not landed, this story can still prove the current sample path, but any full-replay or polling limitation must be called out instead of patched here.
- Epic 12 already proved sample UI pattern behavior in a broad sense, but it did not capture the server-side projection trace chain. Reuse its UI evidence pattern; do not repeat a UI-only smoke as proof.
- The OpenTelemetry audit fix proposal states that R11-A3 remains valid and is unblocked by restored AppHost build/start behavior.
- Lessons ledger L09 requires repeatable sample UI smoke evidence with commands, resource state, browser target, observed results, and log/trace/screenshot references.

### Party-Mode Review Guardrails

- `CounterCommandForm` submits the real `/api/v1/commands` request, but the page UI does not display response headers. Use browser network capture, Aspire trace details, or a direct API call to satisfy AC #3.
- `SilentReloadPattern` loads tenant and projection settings from configuration at runtime. Evidence should prove the effective values, not infer them from defaults.
- Prefer log stages `DomainServiceInvocationSucceeded`, `ProjectionStateUpdated`, `Projection state persisted`, ETag response headers, and SignalR broadcast/refresh evidence as the connected proof chain.

### Advanced Elicitation Guardrails

- Use one proof identity for the whole run. The command `messageId`, correlation ID, tenant, domain, aggregate ID, and proof-window timestamps should appear consistently in the command response, persistence evidence, `/project` invocation, projection actor write, query evidence, and UI/SignalR notes.
- Treat existing counter state as normal. Capture baseline and after counts and assert the delta caused by the selected command instead of resetting state solely to make the count start at zero.
- Keep JWT and auth evidence safe. Record token issuer, audience, tenant claim, permissions, and expiry metadata, but do not paste full bearer tokens or signing secrets into artifacts.
- Use bounded waits for eventual projection delivery and SignalR refresh. Record the retry cadence and final timeout; if projection or SignalR evidence does not arrive within the documented window, classify the story as blocked or needs-story-update instead of accepting a partial proof.
- Prefer an evidence matrix over prose-only notes. Each acceptance criterion should point to a concrete artifact path, trace ID, log excerpt, screenshot, or explicit blocker so code review can audit the proof quickly.
- Do not add a verification script by default. A small script is acceptable only when it reduces evidence drift; the minimum sufficient output for this story is a repeatable runbook plus captured artifacts.

### Useful API Payloads

Command submission shape:

```json
{
  "messageId": "<unique-id>",
  "tenant": "sample-tenant",
  "domain": "counter",
  "aggregateId": "counter-1",
  "commandType": "IncrementCounter",
  "payload": {}
}
```

Query shape:

```json
{
  "tenant": "sample-tenant",
  "domain": "counter",
  "aggregateId": "counter-1",
  "queryType": "get-counter-status",
  "payload": {},
  "entityId": "counter-1",
  "projectionType": "counter"
}
```

With `EnableKeycloak=false`, use the development JWT rules from `appsettings.Development.json`: issuer `hexalith-dev`, audience `hexalith-eventstore`, signing key `DevOnlySigningKey-AtLeast32Chars!`, tenant claim containing `sample-tenant`, and permissions including `command:submit` and `query:read` or wildcards. Evidence artifacts should record the non-secret claims used, not the full bearer token.

## References

- `_bmad-output/implementation-artifacts/epic-11-retro-2026-04-30.md` - R11-A3 action item and evidence gap.
- `_bmad-output/implementation-artifacts/epic-12-retro-2026-04-30.md` - R12-A5 carry-forward and partial R11-A3 evidence note.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-30-opentelemetry-audit-fix.md` - AppHost build/start blocker correction.
- `docs/guides/sample-blazor-ui.md` - sample UI evidence pattern and command-feedback semantics.
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` - projection builder runtime flow.
- `src/Hexalith.EventStore.AppHost/Program.cs` - AppHost runtime topology.
- `samples/Hexalith.EventStore.Sample/Program.cs` - sample `/project` endpoint.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor` - SignalR-driven silent reload pattern.
- Dapr actor runtime docs: https://docs.dapr.io/developing-applications/building-blocks/actors/actors-features-concepts/
- Dapr state API docs: https://docs.dapr.io/reference/api/state_api/
- Aspire CLI docs: https://learn.microsoft.com/dotnet/aspire/cli/overview

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- AppHost final proof run: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/aspire-run-nokeycloak-sampletenant.log`
- Evidence runbook and AC matrix: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/README.md`
- Resource snapshot: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/resources-before.txt`
- Proof identity and payloads: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/final-proof-identity.json`, `final-command-payload.json`, `final-query-payload.json`
- API responses: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/final-baseline-query-response.txt`, `final-command-response.txt`, `final-after-query-response-5s.txt`
- Trace and log excerpts: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/trace-ids.txt`, `eventstore-logs.txt`, `sample-logs.txt`, `sample-blazor-ui-logs.txt`, `negative-log-search.txt`
- Browser evidence: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/silent-reload-env-override-before.png`, `silent-reload-env-override-after.png`, `silent-reload-final-after-direct-command.png`
- Validation result: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/test-results.txt`

### Completion Notes List

- Captured an AppHost-managed no-Keycloak proof with `eventstore`, `sample`, `sample-blazor-ui`, `statestore`, `pubsub`, and Dapr sidecars healthy. The final run overrode the sample UI tenant to `sample-tenant` so browser and API evidence used the same tenant/domain/aggregate.
- Submitted final direct API command `r11a3-430ea44d14c8478791f67322165590b4` with correlation id `r11a3-832d7bc77883465895ce5ba94a180024`. Baseline query count was 2, command returned HTTP 202, and follow-up query returned count 3 with a new `counter` ETag.
- Captured EventStore trace/log evidence for command receipt, domain service success, sample `/process` and `/project` Dapr invocations, event persistence, publication, ETag regeneration, ProjectionActor query, and ETagActor query.
- Projection actor state-write debug messages were not emitted at the active log level. The runbook records this caveat and ties AC #6 to the successful `/project` invocation, post-command ProjectionActor query, count delta, and ETag regeneration.
- Captured SignalR connection and UI silent reload evidence: browser count moved from 2 to 3 after the direct API command while the page stayed open, refresh count moved to 2, and the last UI command timestamp remained unchanged.
- Bounded negative log search found no projection-path errors during the final proof window.
- Stopped the AppHost after evidence capture. The first test attempt was blocked by AppHost process file locks; after stopping the proof run, `dotnet test tests/Hexalith.EventStore.Sample.Tests` passed 63/63. Optional server projection tests were not run because no server-side projection code changed.

### File List

- `_bmad-output/implementation-artifacts/post-epic-11-r11a3-apphost-projection-proof.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/README.md`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/resources-before.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/trace-ids.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/eventstore-logs.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/sample-logs.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/sample-blazor-ui-logs.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/negative-log-search.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/test-results.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/final-proof-identity.json`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/final-query-payload.json`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/final-command-payload.json`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/final-baseline-query-response.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/final-command-response.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/final-after-query-response-5s.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/proof-identity.json`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/query-payload.json`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/command-payload.json`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/baseline-query-response.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/command-response.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/after-query-response-5s.txt`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/silent-reload-env-override-before.png`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/ui-silent-reload-env-override-before-snapshot.md`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/silent-reload-env-override-after.png`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/ui-silent-reload-env-override-after-snapshot.md`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/silent-reload-final-after-direct-command.png`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/ui-silent-reload-final-after-direct-command-snapshot.md`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/silent-reload-current.png`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/ui-silent-reload-current-snapshot.md`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/aspire-run.log`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/aspire-run-nokeycloak.log`
- `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/aspire-run-nokeycloak-sampletenant.log`

### Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-05-01 | 1.0 | Captured AppHost projection proof artifacts, validation result, and moved story to review. | GPT-5 Codex |

## Party-Mode Review

- Date/time: 2026-05-01T11:00:21+02:00
- Selected story key: `post-epic-11-r11a3-apphost-projection-proof`
- Command/skill invocation used: `/bmad-party-mode post-epic-11-r11a3-apphost-projection-proof; review;`
- Participating BMAD agents: Bob (Scrum Master), Winston (Architect), Amelia (Developer Agent), Murat (Master Test Architect), Paige (Technical Writer), Sally (UX Designer), John (Product Manager)
- Findings summary:
  - Bob: Story is ready in structure, but AC #3 needed explicit handling for the fact that sample UI feedback does not expose HTTP headers.
  - Winston: The runtime proof must tie ETag evidence to the `counter` projection rather than accept an unrelated validator header.
  - Amelia: The implementation path has concrete log stages available; the story should name the useful ones so execution does not drift into vague log collection.
  - Murat: Browser-visible before/after count is insufficient unless the tenant, projection, aggregate/entity, and query type are preserved with the evidence.
  - Paige: The repeatable evidence shape is strong; the story needed a small guardrail section so future implementers do not miss the command-header and query-payload proof obligations.
  - Sally: Silent reload proof should show that the UI refreshed from query state after a signal, not only that a command button showed success.
  - John: No product scope expansion is needed; this remains a verification/evidence story.
- Changes applied:
  - Clarified AC #2 to require effective sample UI tenant/projection settings in resource evidence.
  - Clarified AC #3 and Task 2 to require browser network capture or direct API evidence for command response headers when using the UI.
  - Clarified AC #7 to require the ETag evidence to route to the `counter` projection.
  - Clarified AC #10 and Task 4 to require UI/query evidence tied to `sample-tenant`, `counter`, `counter-1`, and `get-counter-status`.
  - Added Party-Mode Review Guardrails with concrete execution traps and useful log stages.
- Findings deferred:
  - No deferred product or architecture decisions. Checkpoint tracking and polling behavior remain owned by R11-A1/R11-A2 and must not be patched in this proof story.
  - `project-context.md` preload was unavailable; no generated project-context artifact was found in this repository.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- Date/time: 2026-05-01T13:34:30+02:00
- Selected story key: `post-epic-11-r11a3-apphost-projection-proof`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-11-r11a3-apphost-projection-proof`
- Batch 1 method names:
  - Self-Consistency Validation
  - Red Team vs Blue Team
  - Architecture Decision Records
  - Pre-mortem Analysis
  - Failure Mode Analysis
- Reshuffled Batch 2 method names:
  - Security Audit Personas
  - Comparative Analysis Matrix
  - Chaos Monkey Scenarios
  - Occam's Razor Application
  - Lessons Learned Extraction
- Findings summary:
  - The story was structurally ready, but the evidence chain could still pass with stitched evidence from multiple commands unless one proof identity was carried through every artifact.
  - Existing counter state made a final-count-only assertion ambiguous; the story needed baseline and delta proof.
  - Manual refresh evidence could accidentally satisfy both query and SignalR criteria; SignalR delivery or refresh-after-signal now remains a distinct obligation.
  - Negative-log proof needed a bounded time window and filter terms so startup noise and projection-path failures are not mixed.
  - JWT evidence needed enough claim detail for repeatability without preserving bearer tokens or signing secrets.
  - Reviewability improves if AC #1-#13 are mapped to explicit artifacts instead of scattered prose.
- Changes applied:
  - AC #2, #4, and #5 now require one command/proof identity across the resource snapshot, command response, persistence evidence, `/project` invocation, and follow-on proof.
  - AC #8 and Task 4 now require baseline and after counts, with delta-based proof when state already exists.
  - AC #9 now states manual refresh or direct query evidence cannot substitute for SignalR delivery evidence.
  - AC #11 now requires bounded proof-window search metadata, including log source, time range, and filter terms.
  - AC #12 and Task 5 now require an AC evidence matrix covering AC #1-#13.
  - Added Advanced Elicitation Guardrails for proof identity, state baseline handling, JWT evidence hygiene, bounded waits, evidence matrix reviewability, and minimal script scope.
- Findings deferred:
  - No product-scope or architecture-policy changes were applied.
  - Checkpoint tracking and polling mode remain owned by R11-A1/R11-A2 if the runtime proof exposes related blockers.
- Final recommendation: `ready-for-dev`
