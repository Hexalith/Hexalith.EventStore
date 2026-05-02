# SignalR Operational Evidence Pattern

This pattern defines the minimum evidence needed to claim SignalR delivery confidence for projection-change notifications. It is a proof contract for QA, DevOps, and future automation runs. It does not change the product protocol: the public hub payload remains only `ProjectionChanged(projectionType, tenantId)`, and clients must re-query the Query API for current projection state.

Use the reusable schema in [_bmad-output/test-artifacts/signalr-operational-evidence-template.md](../../_bmad-output/test-artifacts/signalr-operational-evidence-template.md) for every new run.

## Evidence Storage

Store each run under a story- or proof-specific folder:

```text
_bmad-output/test-artifacts/<story-or-proof-key>/evidence-YYYY-MM-DD[-short-run-id].md
_bmad-output/test-artifacts/<story-or-proof-key>/index.md
```

The folder index should list the run id, date, commit SHA, environment, topology, result, classification, and links to the evidence files. Do not commit raw production logs, HAR files with sensitive payloads, bearer tokens, connection strings, full network traces, production hostnames, user identifiers, or tenant identifiers that are not already safe test aliases. Use stable placeholders such as `<redacted-token>`, `<redacted-host>`, `<redacted-connection-string>`, and `tenant-alias-001`.

## Mandatory Artifacts

Every evidence file must include:

| Artifact | Required content |
| --- | --- |
| Evidence index | Link to the run evidence from the proof folder index. |
| Run identity | Evidence run id, UTC run window, author/agent, commit SHA or build version, repository status if relevant. |
| Topology | AppHost/resources/processes, instance count, client target, broadcast origin, Redis/backplane state when in scope, DAPR/Keycloak/auth mode. |
| Environment | OS, .NET SDK/runtime, Docker/DAPR/Aspire availability, browser automation availability when browser proof is in scope, observability backend availability. |
| SignalR config | `EventStore:SignalR:Enabled`, backplane endpoint or explicit none, proof-only gates, hub URL, group format. |
| Authenticated client/group join | Token source or safe alias, tenant authorization result, group name, connection id or redacted client-session alias. |
| ETag/projection-change trigger | Trigger request or command identity, accepted timestamp, projection type, tenant alias, stream/aggregate id when applicable. |
| Hub broadcast | Broadcast origin, `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync` start and completion timestamps when available, group, success/fail-open log or trace. |
| Client receipt | UTC timestamp for client `ProjectionChanged(projectionType, tenantId)` receipt and the exact two-field payload observed. |
| Query refresh | Query start/end timestamps, HTTP status, ETag before/after or `304 Not Modified`, response classification. |
| Latency calculation | Raw intervals, sample count, percentile method when claiming p99, threshold source. |
| Reliability/control result | At least one false-positive control and one correlation-integrity control. |
| Logs/traces/metrics | Links or redacted excerpts for structured logs, traces, built-in SignalR diagnostics, and connection metrics. |
| Classification outcome | One of `pass`, `product-failure`, `environment-blocker`, `instrumentation-gap`, `sample-only`, or `inconclusive`. |
| Redaction notes | What was omitted or replaced and why the remaining aliases are stable enough for correlation. |
| Environment blockers | Any unavailable service/tool and the exact command, resource state, or condition that failed. |

## Latency Boundaries

All timestamps must be UTC. A run may use server and browser/client timestamps only when the clock source is declared. If clocks are not synchronized, use a same-process harness for latency measurement or classify cross-machine subtraction as diagnostic only.

Capture these points when available:

| Point | Description | Current repository capture |
| --- | --- | --- |
| `triggerAcceptedUtc` | Command, projection-change notification, or proof broadcast request accepted. | Available from HTTP/command logs or proof harness metadata, depending on proof shape. |
| `etagReadyUtc` | ETag regeneration completed, or equivalent projection-change publish point when an ETag is not part of the proof. | Not consistently emitted as a dedicated SignalR-ready timestamp; use ETag actor/query evidence when present. Deferred instrumentation may be needed for automated p99. |
| `broadcastStartUtc` | `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync` entered. | Missing today. Requires product or test-only instrumentation. |
| `broadcastCompletedUtc` | Broadcast call returned or fail-open warning emitted. | Development proof endpoint returns a UTC timestamp after the broadcast call; normal product path has Debug success log without timestamp-specific activity. |
| `clientReceiptUtc` | Client callback received `ProjectionChanged(projectionType, tenantId)`. | Available from proof clients or browser/test harnesses, not from `EventStoreSignalRClient` logs by default. |
| `clientQueryStartedUtc` | Client started the follow-up Query API call. | Available from client harness/browser proof or trace logs. |
| `clientQueryCompletedUtc` | Client received query response. | Available from client harness/browser proof or trace logs. |
| `uiRenderedUtc` | UI visibly refreshed after query, when browser proof is in scope. | Browser screenshot/snapshot metadata can support this, but must be paired with trigger/receipt/query timestamps. |

Name intervals explicitly:

| Interval | Formula | Role |
| --- | --- | --- |
| Trigger-to-broadcast | `broadcastStartUtc - triggerAcceptedUtc`, or `broadcastCompletedUtc - triggerAcceptedUtc` when start is missing and the limitation is declared. | Diagnostic for projection/ETag/server pipeline delay. |
| Broadcast-to-client-receipt | `clientReceiptUtc - broadcastCompletedUtc` or, with better instrumentation, `clientReceiptUtc - broadcastStartUtc`. | Primary SignalR delivery budget for R10-A6 evidence. |
| Client-receipt-to-query-refresh | `clientQueryCompletedUtc - clientReceiptUtc`. | Query follow-on diagnostic. |
| Query-refresh-to-render | `uiRenderedUtc - clientQueryCompletedUtc`. | UI proof diagnostic when browser evidence is in scope. |
| End-to-end trigger-to-refresh | `clientQueryCompletedUtc - triggerAcceptedUtc`, or `uiRenderedUtc - triggerAcceptedUtc` for browser claims. | Overall user-visible flow; not the primary NFR38 budget unless a future story changes the requirement. |

NFR38 source text in `_bmad-output/planning-artifacts/prd.md` defines SignalR "changed" signal delivery from ETag regeneration to connected client receipt within 100ms at p99. For this operational pattern, the primary SignalR interval is broadcast-to-client-receipt because the broadcaster boundary is the closest currently inspectable handoff from product code to SignalR transport. Trigger, query, and UI intervals are diagnostics around that primary delivery measurement.

## p99 Claims

A single happy-path run proves path viability only. Do not label it p99.

Before repository evidence claims p99:

- Capture at least 100 valid SignalR delivery samples after warmup.
- Record the raw sample count, sample window, clock source, topology, commit/build, and threshold source.
- Exclude cold-start samples only with a written warmup rule decided before measurement.
- Sort the valid sample values ascending.
- Use nearest-rank p99: `rank = ceil(0.99 * sample_count)`, one-based; p99 is the value at that rank.
- Report cold-start and warm-run outliers separately.
- Use `100ms` only when citing NFR38 from `_bmad-output/planning-artifacts/prd.md` or equivalent versioned requirement/configuration. If a future environment config overrides the threshold, cite that source.
- Label fewer than 100 samples as `sample evidence only`.
- Route production p99 claims to the configured metrics/trace backend rather than hand-curated local markdown.

The existing `BroadcastChangedAsync_P99Dispatch_RemainsUnder100Milliseconds` test protects the controllable broadcaster dispatch path with 50 measured in-process samples. It is regression coverage, not end-to-end p99 evidence for a real connected client.

## Correlation Fields

Required fields, using safe aliases where needed:

- Evidence run id.
- Operation id and trace id/span id when available.
- Correlation id and causation id when available.
- Command/message id when applicable.
- Event id or projection-change identifier when available.
- Stream or aggregate id when applicable.
- Projection type.
- Tenant id or redacted tenant alias.
- Group name.
- Connection id or redacted client-session alias.
- Connection target.
- Broadcast origin.
- Query response status.
- ETag evidence before and after refresh, or `304 Not Modified`.

Proof-only identifiers belong in evidence metadata, logs, trace tags, test projection/tenant names, or test harness state. Do not add run ids, ETags, aggregate ids, command status, trace ids, or read-model data to the public SignalR payload.

## Diagnostics

Product-specific evidence is mandatory. Built-in SignalR diagnostics support diagnosis but cannot replace proof that a specific projection-change signal reached a specific client.

Useful built-in diagnostics:

- ASP.NET Core SignalR tracing uses `DiagnosticSource` and `Activity`; in .NET 9+ / .NET 10, the server ActivitySource is `Microsoft.AspNetCore.SignalR.Server` and the .NET client ActivitySource is `Microsoft.AspNetCore.SignalR.Client`. Register these with OpenTelemetry when a proof needs hub/client activity spans. See [Microsoft Learn: SignalR diagnostics](https://learn.microsoft.com/aspnet/core/signalr/diagnostics?view=aspnetcore-10.0).
- ASP.NET Core built-in metrics under `Microsoft.AspNetCore.Http.Connections` include `signalr.server.connection.duration` and `signalr.server.active_connections`. Treat these as connection-health context with attributes such as transport and closure status, not as direct end-to-end delivery proof. See [Microsoft Learn: ASP.NET Core built-in metrics](https://learn.microsoft.com/aspnet/core/log-mon/metrics/built-in?view=aspnetcore-10.0).

Current repository gap: `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` registers tracing sources for the application name and `Hexalith.EventStore`, but it does not explicitly add `Microsoft.AspNetCore.SignalR.Server` or `Microsoft.AspNetCore.SignalR.Client`. Add that in a future instrumentation story if SignalR ActivitySource spans are required in the default telemetry path.

## Failure Classification

| Classification | Conditions | Owner/routing |
| --- | --- | --- |
| `environment-blocker` | Docker unavailable; Redis unavailable; DAPR placement/scheduler unavailable; Aspire AppHost not running; browser automation unavailable; auth/token setup failure; clock skew or unsynchronized timestamp sources; port conflict; load/evidence harness failure; observability backend unavailable. | DevOps or test environment owner. Record exact command/resource failure and retry after environment is fixed. |
| `product-failure` | No broadcast; wrong group; unauthorized join outside an intentional negative control; no client receipt within bounded wait; stale or duplicate evidence accepted as success; query refresh failure; latency budget breach. | Product engineering owner for the affected EventStore, SignalR, auth, query, or client path. |
| `instrumentation-gap` | Missing server timestamp; missing client timestamp; missing correlation continuity; missing diagnostic source; unsafe evidence/redaction risk; fewer than 100 samples while claiming p99. | Observability/test owner. Create deferred work with proposed location and why it is needed. |
| `sample-only` | Bounded manual run proves path viability but lacks sample count, clock discipline, or production-grade metrics for p99. | QA/test owner. Do not promote to NFR proof. |
| `pass` | Required artifacts present, controls pass, correlation is continuous, latency threshold and sample rules are satisfied for the claim being made. | Story/proof owner. |

Route Redis isolation or channel-prefix policy questions to `post-epic-10-r10a7-redis-channel-isolation-policy`. Route query/UI round-trip proof to `post-epic-11-r11a3-apphost-projection-proof` or `post-epic-11-r11a4-valid-projection-round-trip`.

## Reliability Controls

Every run must include at least one false-positive prevention control appropriate to the proof shape:

- Disabled SignalR or no hub: no receipt should occur.
- Wrong tenant/group: no receipt should occur for the subscribed client.
- Disconnected client: do not claim stale replay after reconnect.
- Redis disabled for cross-instance proof: no cross-instance receipt should occur.
- Equivalent control that would fail if the proof accepted unrelated or stale messages.

Every run must also include one correlation-integrity control: evidence collection may succeed mechanically, but validation must fail when the run id, correlation id, projection-change identifier, or other declared correlation field is mismatched or missing.

These controls prevent false positives. They are not a substitute for broader chaos testing.

## Current Instrumentation Inventory

| Evidence need | Current support | Deferred work if stronger proof is needed |
| --- | --- | --- |
| Authenticated join and group | `ProjectionChangedHub.JoinGroup()` validates auth/tenant access, rejects colons, tracks group count, and logs join/leave/connect/disconnect at Debug. | Capture Debug logs or add safe test harness metadata for connection/session aliases. |
| Broadcast success/failure | `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync()` sends to `{projectionType}:{tenantId}`, logs Debug success EventId 1084 and Warning fail-open EventId 1085. | Add Activity or structured timing around broadcast start/completion for p99 automation. |
| Development deterministic broadcast | `SignalRRuntimeProofEndpoints` exposes Development-only identity and broadcast endpoints gated by `EventStore:SignalR:RuntimeProof:Enabled=true`. | Keep proof-only; do not expose in production. Consider adding optional server timestamp names if future automation needs them. |
| Client receipt | `EventStoreSignalRClient` invokes callbacks from `OnProjectionChanged`; no built-in receipt timestamp or reconnect callback is exposed publicly. | Add test harness receipt timestamping or future client diagnostics without changing the public hub payload. |
| Reconnect/rejoin | Client helper rejoins tracked groups internally after reconnect and prunes server-rejected groups. | Public reconnect callback is deferred by R10-A5; applications own re-query after known downtime. |
| OpenTelemetry | Service defaults register `Hexalith.EventStore`, ASP.NET Core, HTTP client, runtime metrics, and JSON console logs. | Add SignalR server/client ActivitySource registration and product tags in a future instrumentation story. |
| ETag/query refresh | Query API returns ETags and supports `If-None-Match`; R11-A3 evidence shows direct query and UI refresh artifacts. | Dedicated timestamp continuity from ETag regeneration to broadcast start is still a gap for full automation. |

Deferred work entries:

| Gap | Owner | Proposed location | Why needed |
| --- | --- | --- | --- |
| Missing broadcast start timestamp | EventStore server/observability | `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync()` Activity or structured timing log | Required for unambiguous trigger-to-broadcast and broadcast-to-client calculations without relying on endpoint completion only. |
| Missing default SignalR ActivitySource registration | Service defaults/observability | `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` | Required before built-in SignalR server/client spans appear consistently in traces. |
| Missing client receipt timestamp in reusable helper | Client/test tooling | Test harness around `EventStoreSignalRClient` or optional diagnostics in client package | Required for repeatable delivery latency measurement outside ad hoc proof clients. |
| Missing correlation continuity across trigger, ETag, broadcast, and client receipt | EventStore telemetry/test tooling | Product Activity tags or proof metadata schema | Required for falsifiable automated validation and production trace correlation. |

## Schema Walk-Through

Existing evidence: `_bmad-output/test-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof/evidence-2026-05-02-133041Z.md`.

| Schema field | Status | Notes |
| --- | --- | --- |
| Run identity | Present | Run id, UTC timestamp, commit SHA recorded. |
| Environment | Present | Tier 3 lane, Docker/.NET prerequisites, command, Redis endpoint, cleanup, port isolation. |
| Topology | Present | Two EventStore instances, A/B endpoints, process ids, Redis endpoint. |
| SignalR config | Present | SignalR enabled, Redis backplane, proof endpoint gate, client target, broadcast origin. |
| Authenticated client/group join | Partially present | Group and target are present; auth/token details are implicit in harness setup and should be explicit in future evidence. |
| Trigger accepted | Present for proof endpoint | Broadcast origin endpoint and observed payload recorded; exact accepted timestamp should be named in future schema usage. |
| Broadcast start/completion | Partially present | Broadcast result has origin and receipt timing; broadcast start is not captured. |
| Client receipt | Present | Observed payload and UTC receipt timestamp recorded. |
| Query refresh | Not applicable | R10-A2 correctly bounds query refresh to R11-A3/R11-A4. |
| Latency calculation | Sample-only | Single observed receipt after 31ms; not a p99 claim. |
| Reliability/control | Present | Redis-disabled, stale-message, same-instance guard, unreachable Redis fail-open. |
| Logs/traces/metrics | Partially present | Log excerpts present; trace/metrics links absent. |
| Classification | Present by result, should be explicit | Positive proof passed; future evidence should set `classification: pass` or `sample-only`. |
| Redaction notes | Partially present | No production secrets shown; future evidence should include an explicit redaction section. |
| Environment blockers | Present when relevant | DAPR config store warning shown as fail-open context. |

Spot-check: `_bmad-output/test-artifacts/post-epic-11-r11a3-apphost-projection-proof/README.md` covers query refresh and browser/UI evidence. It includes command identity, correlation id, tenant/domain/aggregate, query payloads, ETags before/after, SignalR negotiate/connect/group join trace ids, browser screenshots, and sample logs. It also records that direct SignalR broadcast/client receipt Debug logs were not captured at the active log level. For this R10-A6 schema, that is a useful query/UI companion artifact, not a replacement for client receipt latency evidence.

Intentional rejection example:

```yaml
classification: product-failure
validation_result: fail
reason: client receipt timestamp is present, but correlation id does not match the trigger evidence run id
missing_or_mismatched:
  evidence_run_id: r10a6-20260502-001
  trigger_correlation_id: r10a6-20260502-001
  client_receipt_correlation_id: r10a6-20260502-previous-run
```

This evidence must fail even if a `ProjectionChanged(counter, tenant-a)` message was received, because the schema cannot prove that the receipt belongs to the current trigger.
