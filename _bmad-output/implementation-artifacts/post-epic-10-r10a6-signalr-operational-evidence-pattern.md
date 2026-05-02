# Post-Epic-10 R10-A6: SignalR Operational Evidence Pattern

Status: review

<!-- Source: epic-10-retro-2026-05-01.md R10-A6 -->
<!-- Source: sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md Proposal 6 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **QA / DevOps engineer responsible for real-time notification confidence**,
I want a repeatable SignalR operational evidence pattern for delivery latency and reliability,
So that the team can prove NFR38 and runtime delivery health with traces, metrics, logs, browser/client evidence, and clear environment-failure classification.

## Story Context

Epic 10 delivered the SignalR real-time notification layer and closed focused implementation gaps: signal-only broadcasts after ETag regeneration, optional Redis backplane wiring, automatic group rejoin, tenant-aware group authorization, and a Tier 3 Redis cross-instance proof. The remaining R10-A6 gap is not another product behavior change. It is the evidence discipline for proving delivery latency and reliability in running environments.

Story 10.2 measured the controllable broadcaster dispatch budget against a mock hub context. R10-A2 later proved Redis-backed cross-instance delivery, but its evidence is intentionally transport-focused and bounds query refresh to the projection proof stories. R10-A5 documents that SignalR is an invalidation signal only and that clients must query on connect/reconnect. R10-A6 must connect those pieces into an operator-ready evidence pattern: what to capture, where to capture it, which timestamps define the latency budget, which built-in SignalR metrics are useful context, and how to separate product failures from local Docker/DAPR/Aspire/browser infrastructure blockers.

Current HEAD at story creation: `68b6957`.

## Acceptance Criteria

1. **Minimum proof artifacts are defined.** Create or update a concise evidence-pattern document at `docs/operations/signalr-operational-evidence.md` and a reusable Markdown schema/example under `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`. The pattern must name the mandatory artifacts for SignalR delivery confidence: evidence index, run identity, topology, commit SHA/build version, environment, SignalR config, authenticated client/group join, ETag/projection-change trigger, hub broadcast, client receipt, query refresh, latency calculation, reliability/control result, logs/traces/metrics links or excerpts, classification outcome, redaction notes, and environment blockers.

2. **Latency boundary is unambiguous.** Define NFR38 measurement points in UTC timestamps: trigger accepted, ETag regeneration completed or equivalent projection-change publish point, `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync` started/completed, client `ProjectionChanged(projectionType, tenantId)` receipt, client query started/completed, and UI refresh/render evidence when browser proof is in scope. The pattern must name these intervals explicitly: trigger-to-broadcast, broadcast-to-client-receipt, client-receipt-to-query-refresh, query-refresh-to-render when UI proof is in scope, and end-to-end trigger-to-refresh. The primary SignalR delivery budget is broadcast-to-client-receipt; the other intervals are query/UI follow-on diagnostics unless a future story changes NFR38 wording.

3. **p99 guidance is reproducible.** Define how many samples are required before claiming p99, how to compute the percentile, the maximum accepted threshold for the SignalR delivery interval, and how to report cold-start/warm-run outliers. Do not claim p99 from a single happy-path run. Require raw sample count, sample window, clock source, warmup exclusion rule, sorted nearest-rank calculation (`ceil(0.99 * sample_count)`), and threshold source. Require at least 100 valid samples before any repository evidence claims p99; smaller manual runs must be labeled "sample evidence only" and production p99 must be routed to the configured metrics/trace backend. Do not invent a threshold; cite the NFR38 source/configuration or record a deferred threshold-definition gap.

4. **Trace/log/browser evidence links the chain.** The pattern must require evidence linking ETag regeneration or projection-change trigger -> hub broadcast -> client receipt -> query refresh. It must include required correlation fields: evidence run id, operation id, correlation id, causation id when available, command/message id when applicable, event id or projection-change identifier when available, stream or aggregate id when applicable, projection type, tenant id or redacted tenant alias, group name, connection id or redacted client-session alias, connection target, broadcast origin, query response status, and ETag evidence. SignalR payload must remain only `projectionType` and `tenantId`; run ids and proof-only identifiers belong in proof metadata, logs, or run-unique test identifiers, not in the public hub payload.

5. **Built-in SignalR diagnostics are used correctly.** Document how ASP.NET Core SignalR server/client tracing and metrics can support, but not replace, product-specific evidence. Include `Microsoft.AspNetCore.SignalR.Server` and `Microsoft.AspNetCore.SignalR.Client` ActivitySource guidance for .NET 9+ / .NET 10 environments, and name the built-in `Microsoft.AspNetCore.Http.Connections` metrics such as `signalr.server.connection.duration` and `signalr.server.active_connections` as connection-health context rather than direct end-to-end delivery proof.

6. **Repository instrumentation gaps are explicit.** Inspect current EventStore SignalR and telemetry code and record whether the existing logs/activities can capture each required timestamp. If a required timestamp cannot be captured without new product or test-only instrumentation, create a deferred-work entry with owner, proposed location, and why it is needed. Do not silently add product behavior or public payload fields while defining the pattern.

7. **Environment failures are classified separately from product failures.** The pattern must define a decision table with owner/routing guidance for environment blockers, product failures, and deferred instrumentation gaps. Environment blockers must include Docker unavailable, Redis unavailable, DAPR placement/scheduler unavailable, Aspire AppHost not running, browser automation unavailable, auth/token setup failure, clock skew/unsynchronized timestamp sources, port conflict, load/evidence harness failure, and observability backend unavailable. Product failures must include no broadcast, wrong group, unauthorized join, no client receipt within the bounded wait, stale/duplicate evidence, query refresh failure, and latency budget breach. Instrumentation gaps must include missing server timestamp, missing client timestamp, missing correlation continuity, missing diagnostic source, and unsafe evidence/redaction risk.

8. **Reliability controls are required.** The evidence pattern must require at least one negative/control result appropriate to the proof shape: disabled SignalR/no hub, wrong tenant/group no receipt, disconnected client no stale replay claim, Redis disabled for cross-instance proof, or equivalent. It must also require one correlation-integrity control where evidence collection succeeds but a mismatched or missing run/correlation identifier causes the validation to fail. Controls must be framed as false-positive prevention, not as broader chaos testing.

9. **The pattern names where evidence is stored.** Evidence files for future runs must live under a story- or proof-specific folder in `_bmad-output/test-artifacts/` with dated filenames and a short evidence index. The pattern must include a reusable Markdown schema with required and optional fields, version marker, field descriptions, allowed classification values, and storage convention. It must forbid committing raw production logs, HAR files with sensitive payloads, tokens, connection strings, full network traces, production hostnames, user identifiers, or tenant identifiers that are not already safe test aliases; redacted values must use stable placeholders such as `<redacted-token>`, `<redacted-host>`, or `tenant-alias-001`.

10. **Existing story boundaries are preserved.** Do not re-prove R10-A2 Redis cross-instance delivery, R10-A3 tenant authorization, R10-A5 reconnect docs, R10-A7 Redis isolation policy, or R11-A3/R11-A4 query/projection round-trip behavior inside this story. R10-A6 defines the evidence pattern and may add narrow diagnostics/test harness guidance only when required to make the pattern executable.

11. **Verification demonstrates the pattern is usable.** Validate the final evidence-pattern document by walking one existing evidence artifact or story record through the schema, preferably `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/` plus a spot-check of R11-A3/R11-A4 projection proof evidence. Record which required fields are present, absent-but-deferred, or not applicable. The walk-through must prove an agent can fill the schema from one bounded run without asking for missing field meanings. It must not use Redis, tenant authorization, reconnect, or projection/query checks as new acceptance proof for this story. If docs only change, markdown/link checks are sufficient. If source/test instrumentation changes are made, run the affected project tests individually.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names R10-A6 and the evidence-pattern result. At code-review signoff, both become `done`.

## Scope Boundaries

- Do not change the public SignalR hub payload. It remains `ProjectionChanged(projectionType, tenantId)`.
- Do not add projection state, ETags, aggregate IDs, command status, trace ids, run ids, or serialized read-model data to SignalR messages.
- Do not redefine NFR38 product semantics beyond naming the measurable intervals and evidence thresholds.
- Do not treat built-in connection metrics as proof that a specific projection-change signal reached a client.
- Do not require persistent containers or a new default Redis dependency for normal local development.
- Do not add durable SignalR delivery semantics, a new SignalR retry policy, product SLO enforcement, client SDK/API redesign, long-running soak tests, production dashboards, alert rules, or a distributed tracing platform migration.
- Do not add broad observability platform work, dashboards, or admin UI features unless recorded as deferred follow-up.
- Do not commit raw production diagnostics, tokens, connection strings, HAR files with sensitive data, or full network traces.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| Evidence-pattern target | `docs/` or `_bmad-output/implementation-artifacts/` | Add the concise SignalR operational evidence pattern where future dev/QA agents can find it |
| Existing R10-A2 evidence | `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/` | Use as the first schema-walk example; do not rewrite successful runtime proof unless needed |
| SignalR broadcaster logs | `src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs` | Inspect existing EventIds 1084/1085 for broadcast sent/fail-open evidence |
| SignalR hub logs | `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs` | Inspect join/leave/connect/disconnect and tenant-denial logging fields |
| Runtime proof endpoints | `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs` | Cite existing Development-only proof identity/broadcast hook boundaries |
| SignalR client helper | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` | Inspect callback receipt, reconnect, and client logging boundaries |
| Service defaults | `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` | Inspect OpenTelemetry registration and OTLP exporter behavior |
| Activity source constants | `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` and `src/Hexalith.EventStore/Telemetry/EventStoreActivitySources.cs` | Reuse existing naming/tag conventions; do not invent incompatible source names |
| Architecture observability rules | `_bmad-output/planning-artifacts/architecture.md` | Preserve correlation/causation and structured logging requirements |
| Query/sample docs | `docs/reference/query-api.md`, `docs/guides/sample-blazor-ui.md` | Reference query refresh and sample UI evidence boundaries if needed |

## Tasks / Subtasks

- [x] Task 0: Baseline and evidence inventory (AC: #1, #6, #10)
  - [x] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [x] 0.2 Inspect R10-A2 evidence and current SignalR story records for fields already captured.
  - [x] 0.3 Inspect current SignalR logs, proof endpoints, client helper, and telemetry registration for available timestamps and identifiers.
  - [x] 0.4 Identify missing instrumentation or proof-harness gaps as deferred work unless a very small docs/test-only change is required to make the pattern usable.
  - [x] 0.5 Confirm no public SignalR payload, group format, authorization, Redis, reconnect, query, or UI behavior change is needed for this evidence-pattern story.

- [x] Task 1: Define the evidence schema (AC: #1, #2, #4, #7, #8, #9)
  - [x] 1.1 Add `docs/operations/signalr-operational-evidence.md` and `_bmad-output/test-artifacts/signalr-operational-evidence-template.md` with a Markdown schema, version marker, required/optional field labels, allowed classification values, and an evidence index pattern.
  - [x] 1.2 Require UTC timestamps for all latency points and note clock-source assumptions when client/browser and server run on different machines; define trigger-to-broadcast, broadcast-to-client-receipt, client-receipt-to-query-refresh, query-refresh-to-render, and end-to-end trigger-to-refresh.
  - [x] 1.3 Require evidence run id, operation id, correlation id, causation id when available, command/message id when applicable, event id or projection-change identifier when available, stream/aggregate id when applicable, projection type, tenant id or safe tenant alias, group name, connection id or client-session alias, connection target, broadcast origin, and query response/ETag evidence.
  - [x] 1.4 Require at least one false-positive control and explain which product failure it guards against; include a correlation-integrity control where mismatched/missing identifiers make validation fail.
  - [x] 1.5 Define allowed evidence storage under `_bmad-output/test-artifacts/<story-or-proof-key>/evidence-YYYY-MM-DD*.md`.
  - [x] 1.6 Add a safety note: redact tokens, secrets, connection strings, production hostnames, unsafe tenant/user identifiers, tenant-sensitive payloads, raw production logs, and network traces with stable placeholders.

- [x] Task 2: Define latency and p99 calculation guidance (AC: #2, #3, #5)
  - [x] 2.1 Name the primary SignalR delivery interval and separately name query refresh and UI render intervals.
  - [x] 2.2 State that a single run proves path viability, not p99.
  - [x] 2.3 Define the minimum sample set for p99 claims and require raw sample count, sample window, accepted clock source, sorted nearest-rank percentile method, threshold source, and outlier/warmup handling in evidence.
  - [x] 2.4 State how built-in SignalR connection metrics and ActivitySources support diagnosis without replacing product-specific delivery observations.
  - [x] 2.5 If the current OpenTelemetry setup does not collect `Microsoft.AspNetCore.SignalR.Server` / `.Client`, record the required configuration as deferred work or a future instrumentation story.

- [x] Task 3: Define failure classification and blocker routing (AC: #7, #8, #10)
  - [x] 3.1 Add a decision table separating environment blockers from product failures.
  - [x] 3.2 Include Docker, Redis, DAPR placement/scheduler, Aspire, auth/token, browser automation, observability backend, and port conflict blockers.
  - [x] 3.3 Include no broadcast, wrong group, unauthorized join, no receipt, stale/duplicate evidence, query refresh failure, and latency breach product failures.
  - [x] 3.4 Add instrumentation-gap routing for missing server timestamp, missing client timestamp, missing correlation continuity, missing diagnostic source, unsafe evidence/redaction risk, and p99 sample insufficiency.
  - [x] 3.5 Route Redis isolation/channel-prefix policy questions to R10-A7 and query/UI round-trip proof to R11-A3/R11-A4.

- [x] Task 4: Validate against existing evidence (AC: #11)
  - [x] 4.1 Walk the R10-A2 runtime proof evidence through the schema and mark present/missing fields.
  - [x] 4.2 If R11-A3/R11-A4 evidence exists, spot-check the query refresh fields against that proof without editing those stories.
  - [x] 4.3 Record schema gaps as future evidence obligations, not retroactive failures of completed stories unless the old evidence made a false claim.
  - [x] 4.4 Add one intentionally incomplete or mismatched evidence example in the template or verification notes that demonstrates the schema rejects missing correlation/latency fields.
  - [x] 4.5 Run markdown/link validation when practical; if unavailable, record the command attempted and the blocker.

- [x] Task 5: Story bookkeeping (AC: #12)
  - [x] 5.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [x] 5.2 Move this story and only this story from `ready-for-dev` to `review` at dev handoff.
  - [x] 5.3 Leave R10-A5/R10-A7/R10-A8 and all non-R10 rows unchanged.

## Dev Notes

### Architecture Guardrails

- SignalR is an invalidation channel. Query endpoints remain authoritative for projection data, current ETags, tenant authorization, and UI state.
- R10-A6 is an evidence-pattern story. Prefer docs/schema plus narrow verification over source changes.
- The primary proof chain is trigger -> ETag/projection-change readiness -> broadcast -> client receipt -> query refresh. Do not skip client receipt and infer it from server-side logs alone.
- Built-in connection metrics (`signalr.server.connection.duration`, `signalr.server.active_connections`) can show connection health, transport type, and closure status. They cannot prove that a named `ProjectionChanged(counter, tenant)` signal reached the intended client.
- SignalR hub/client ActivitySources in .NET 10 can help trace hub invocations, but product-specific tags still need to identify projection type, tenant id, group, broadcast origin, and client target in safe metadata.
- Treat browser evidence as optional for transport-only proof and required for user-visible UI-refresh claims. Browser screenshots alone are not enough without the trigger/receipt/query timestamps.
- If server and browser clocks differ, prefer a same-process harness for latency measurement or record clock-skew assumptions. Do not subtract timestamps across unsynchronized machines without caveat.
- Use run-unique projection/tenant identifiers or a proven buffer-drain step to avoid stale signal false positives.
- Product failures should be actionable. Environment blockers should name the missing service/tool and the exact command or condition that failed.
- The evidence schema must be falsifiable: missing required timestamps, mismatched run/correlation identifiers, or absent client receipt evidence must fail validation instead of being described as inconclusive success.
- The schema is a Markdown contract for human-run and automation-run proofs, not a new product protocol. Use safe proof metadata and redaction aliases rather than changing SignalR payload, hub group format, or query contracts.

### Current-Code Intelligence

- `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync()` logs broadcast success at Debug and broadcast failure at Warning. It does not currently emit an Activity or latency histogram around the broadcast call.
- `ProjectionChangedHub.JoinGroup()` is `[Authorize]`, validates group parts, delegates tenant checks to `ITenantValidator`, and logs join/leave/connect/disconnect plus tenant-denial events.
- `SignalRRuntimeProofEndpoints` are Development-only and require `EventStore:SignalR:RuntimeProof:Enabled=true`; they already expose runtime identity and deterministic proof broadcasts for Tier 3 tests.
- `EventStoreSignalRClient` invokes consumer callbacks from `OnProjectionChanged`, rejoins groups after reconnect, logs unexpected closed connections, and prunes server-rejected rejoin groups.
- `AddServiceDefaults()` registers OpenTelemetry logging, ASP.NET Core/runtime/http metrics, and tracing with `builder.Environment.ApplicationName` and `Hexalith.EventStore`; SignalR-specific ActivitySource names may require explicit registration before they appear in traces.
- Existing server telemetry uses `EventStoreActivitySource` and `EventStoreActivitySources` naming/tag conventions. Follow the architecture's correlation/tenant/domain tag model if adding future instrumentation.

### Previous Story Intelligence

- Story 10.1 pinned the signal-only model and ETag-before-broadcast ordering for direct and pub/sub paths.
- Story 10.2 distinguished controllable local dispatch timing from true end-to-end latency and kept Redis runtime proof out of the structural wiring story.
- Story 10.3 established automatic rejoin but recorded missed-signal catch-up as consumer-owned.
- R10-A2 created a deterministic two-instance Redis proof and evidence file. Reuse that evidence style and improve the reusable schema; do not duplicate the proof.
- R10-A3 enforced tenant-aware group authorization. Evidence patterns should expect authenticated joins and should classify unauthorized joins as product/security failures unless the test intentionally covers a negative control.
- R10-A5 keeps reconnect guidance documentation-focused. R10-A6 may reference reconnect/resume as evidence scenarios but must not change client reconnect semantics.

### Testing Standards

- Docs/schema-only work should run markdown and link checks when available.
- Test-only proof harness updates belong under `tests/Hexalith.EventStore.IntegrationTests` and must remain Tier 3/manual or Docker-gated if they require Redis, Docker, DAPR, or browsers.
- If source instrumentation is added to EventStore SignalR code, run focused server/SignalR tests individually and avoid solution-level `dotnet test`.
- If browser evidence is required, use Playwright only against local/AppHost resources and capture a bounded screenshot or trace path, not raw production traffic.

### Latest Technical Information

- Microsoft Learn for ASP.NET Core SignalR diagnostics says SignalR server/client tracing uses `DiagnosticSource` and `Activity`; the .NET SignalR server ActivitySource is `Microsoft.AspNetCore.SignalR.Server`, and the client ActivitySource is `Microsoft.AspNetCore.SignalR.Client` in .NET 9+ / .NET 10 environments.
- Microsoft Learn for ASP.NET Core built-in metrics lists `Microsoft.AspNetCore.Http.Connections` SignalR metrics including `signalr.server.connection.duration` and `signalr.server.active_connections`, with attributes such as closure status and transport.
- The repository currently uses `Microsoft.AspNetCore.SignalR.Client` and `Microsoft.AspNetCore.SignalR.StackExchangeRedis` version `10.0.5`, and OpenTelemetry packages `1.15.x` in `Directory.Packages.props`.

### Project Structure Notes

- Prefer a short docs artifact or implementation-artifact note for the reusable evidence schema; future proof runs should write dated evidence under `_bmad-output/test-artifacts/`.
- Existing runtime proof files are already under `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/`.
- Do not place long diagnostic blobs in story files. Story files should link to or summarize evidence.
- Expected BMAD edits during dev are the evidence-pattern document, this story file, possibly `deferred-work.md`, and `sprint-status.yaml` bookkeeping.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md#Proposal-6`] - R10-A6 acceptance criteria and rationale.
- [Source: `_bmad-output/implementation-artifacts/epic-10-retro-2026-05-01.md`] - R10-A6 action item and NFR evidence gap.
- [Source: `_bmad-output/implementation-artifacts/10-1-signalr-hub-and-projection-change-broadcasting.md`] - Signal-only model and ETag-before-broadcast ordering.
- [Source: `_bmad-output/implementation-artifacts/10-2-redis-backplane-for-multi-instance-signalr.md`] - NFR38 controllable dispatch boundary.
- [Source: `_bmad-output/implementation-artifacts/10-3-automatic-signalr-group-rejoining-on-reconnection.md`] - Reconnect/rejoin behavior and missed-signal limitation.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof.md`] - Tier 3 runtime proof and evidence schema precedent.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a3-hub-group-authorization-decision.md`] - Tenant-aware join authorization expectations.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a5-client-reconnect-guidance.md`] - Reconnect responsibility and signal-only documentation boundaries.
- [Source: `src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs`] - Broadcast success/failure logging and fail-open behavior.
- [Source: `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`] - Hub authorization, group join, and structured logging.
- [Source: `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs`] - Development-only proof identity and broadcast endpoints.
- [Source: `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs`] - Client receipt/reconnect behavior and logging boundaries.
- [Source: `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`] - OpenTelemetry and metrics registration.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Observability`] - Structured logging and OpenTelemetry guardrails.
- [Source: Microsoft Learn, `https://learn.microsoft.com/aspnet/core/signalr/diagnostics?view=aspnetcore-10.0`] - SignalR logging, tracing, and ActivitySource guidance.
- [Source: Microsoft Learn, `https://learn.microsoft.com/aspnet/core/log-mon/metrics/built-in?view=aspnetcore-10.0`] - ASP.NET Core built-in SignalR metrics.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Deferred Decisions / Follow-ups

- Add `Microsoft.AspNetCore.SignalR.Server` and `Microsoft.AspNetCore.SignalR.Client` ActivitySource registration in `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` if default SignalR spans are required by future proof automation.
- Add an Activity or structured timing log around `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync()` start/completion if automated p99 delivery evidence needs an unambiguous server-side broadcast boundary.
- Add proof harness or optional client diagnostics for UTC client receipt timestamps; do not add proof metadata to the public SignalR payload.
- Improve correlation continuity across trigger, ETag regeneration, broadcast, client receipt, and query refresh with safe trace tags or proof metadata in a future instrumentation story.

### Debug Log References

- Baseline: current HEAD `5279247a833f5b9fdfaf655485ceca2ba0dc4e37`; story row confirmed `ready-for-dev`, then moved to `in-progress`.
- Aspire baseline: repo AppHost was already running; `mcp__aspire__.doctor` showed no failed checks, and `mcp__aspire__.list_resources` showed EventStore/sample/admin resources healthy before docs work.
- Red check: required files `docs/operations/signalr-operational-evidence.md` and `_bmad-output/test-artifacts/signalr-operational-evidence-template.md` were absent before implementation.
- Validation: `npx --yes markdownlint-cli2 "docs/operations/signalr-operational-evidence.md"` passed; `npx --yes markdown-link-check "docs/operations/signalr-operational-evidence.md"` checked 3 links and passed.
- Validation: `npx --yes markdownlint-cli2 "_bmad-output/test-artifacts/signalr-operational-evidence-template.md"` passed; `npx --yes markdown-link-check "_bmad-output/test-artifacts/signalr-operational-evidence-template.md"` found no hyperlinks and passed.
- Regression: first `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` attempt was blocked by the running EventStore resource locking DLLs; stopped `eventstore-hxqerrbv`, reran successfully.
- Regression: first `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` attempt was blocked by the running Sample resource locking DLLs; stopped `sample-shcupzvy`, reran successfully.
- Cleanup: restarted `eventstore-hxqerrbv` and `sample-shcupzvy`; Aspire resources returned to Running/Healthy.

### Completion Notes List

- Added the operator-facing SignalR operational evidence pattern under `docs/operations/`, covering mandatory artifacts, UTC latency boundaries, p99 rules, correlation fields, diagnostics, failure classification, reliability controls, storage/redaction policy, instrumentation gaps, and a walk-through of existing R10-A2/R11-A3 evidence.
- Added a reusable `signalr-operational-evidence/v1` Markdown template under `_bmad-output/test-artifacts/` with required/optional fields, allowed classification values, reliability controls, correlation-integrity rejection, redaction rules, and deferred-work table.
- Preserved story boundaries: no product SignalR payload, group format, authorization, Redis, reconnect, query, UI, or telemetry code changed.
- Validated the pattern against existing R10-A2 Redis backplane evidence and R11-A3 projection/UI proof evidence; old evidence gaps are documented as future obligations, not retroactive failures.

### File List

- `_bmad-output/implementation-artifacts/post-epic-10-r10a6-signalr-operational-evidence-pattern.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`
- `docs/operations/signalr-operational-evidence.md`

## Party-Mode Review

- Date/time: 2026-05-02T18:47:48+02:00
- Selected story key: `post-epic-10-r10a6-signalr-operational-evidence-pattern`
- Command/skill invocation used: `/bmad-party-mode post-epic-10-r10a6-signalr-operational-evidence-pattern; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect), Paige (Technical Writer)
- Findings summary:
  - Winston: The story needed a sharper evidence contract so implementation does not expand into improvised observability work; canonical correlation fields, latency windows, p99 criteria, proof artifact files, and sibling-story exclusions needed to be explicit.
  - Amelia: The story was conceptually implementable but needed concrete target paths, schema shape, directly verifiable acceptance criteria, and clearer separation between docs/schema work and product instrumentation.
  - Murat: The evidence pattern needed stronger falsifiability: required timestamps and identifiers, false-positive controls, environment/product/instrumentation routing, and a validation path that rejects missing correlation or latency evidence.
  - Paige: The document needed enough structure for future agents and operators to fill the evidence schema without inventing placement, field meanings, redaction rules, or proof sufficiency.
- Changes applied:
  - Added concrete expected artifact paths for the operations document and reusable evidence template.
  - Defined the primary SignalR delivery interval as broadcast-to-client-receipt and named follow-on diagnostic intervals.
  - Added required correlation, identity, connection, query/ETag, and safe-redaction fields.
  - Strengthened p99 guidance with sample count, sample window, clock source, nearest-rank method, and threshold-source requirements.
  - Added instrumentation-gap classification, blocker routing, and a required correlation-integrity negative control.
  - Tightened validation against existing R10-A2 and R11 evidence without re-proving sibling story behavior.
- Findings deferred:
  - Actual product instrumentation, OpenTelemetry collection changes, dashboards, alerting, long-running soak tests, and client API redesign remain out of scope unless a future implementation story accepts them.
  - Exact final wording and field layout of the Markdown schema remain implementation details constrained by the updated acceptance criteria.
- Final recommendation: `ready-for-dev`

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-02 | 1.0 | Implemented SignalR operational evidence pattern, reusable evidence template, schema walk-through, markdown/link validation, and moved story to review. | GPT-5 Codex |
| 2026-05-02 | 0.2 | Party-mode review hardened evidence schema, latency boundaries, p99 guidance, controls, and blocker routing. | Codex automation |
| 2026-05-02 | 0.1 | Created ready-for-dev R10-A6 SignalR operational evidence pattern story. | Codex automation |

## Verification Status

Implementation complete. Markdown lint/link checks passed for the operations doc and evidence template. Standard unit-test projects passed individually after temporarily stopping AppHost resources that were locking build outputs:

- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` — 334 passed.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` — 281 passed.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` — 63 passed.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj` — 78 passed.

AppHost `eventstore` and `sample` resources were restarted after the test run and reported Running/Healthy.
