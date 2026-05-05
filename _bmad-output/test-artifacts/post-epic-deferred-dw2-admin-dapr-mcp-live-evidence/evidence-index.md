# DW2 Admin DAPR MCP Live Evidence

Story: `post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`
Evidence started: 2026-05-05T13:50:07+02:00
Evidence folder: `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/`

## Redaction Rules

| Data class | Rule | Evidence handling |
|---|---|---|
| Bearer tokens and JWTs | Always redact fully | Use `<REDACTED_TOKEN>` only |
| Component secrets and connection strings | Always redact fully | Use `<REDACTED_SECRET>` only |
| Event and command payload bodies | Do not store raw payloads | Store envelope metadata and shape only |
| Tenant, domain, aggregate, correlation ids | Keep only deterministic test identifiers or safe local sample identifiers | Redact customer-like values |
| Raw state-store values | Do not store | Store key name only when needed and safe |
| URLs and localhost ports | Allowed | Record exact localhost URLs and ports |
| MCP JSON-RPC payloads | Allowed after token/payload review | Store method, tool name, arguments shape, redacted values |

## Blocker Classes

| Class | Meaning | Dependent checks |
|---|---|---|
| environment blocker | Docker, DAPR, Aspire, certificate, port, or local prerequisite prevents runtime proof | Stop only affected runtime slice |
| pre-existing product defect | Live behavior fails in already-shipped code and is outside DW2 narrow defect gate | Preserve failure and route |
| story defect | DW2 evidence/test scaffold is wrong or internally inconsistent | Fix inside story if narrow |
| known deferred debt | Existing deferred limitation explains the result | Preserve and disposition to matching DW story |
| out-of-scope DW3-DW6 work | Pressure belongs to JSON/large-stream, evidence-schema, UI polish, or governance follow-up | Do not patch in DW2 |
| evidence gap | Smoke path cannot prove the required claim with current tooling | Record exact missing proof path |

## Acceptance Criteria Map

| AC | Command or tool | Target resource | Artifact path | Expected result | Observed result | Classification | Redaction note | Deferred-work disposition |
|---:|---|---|---|---|---|---|---|---|
| 1 | `EnableKeycloak=false aspire run --detach --non-interactive --format Json --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; Aspire MCP `list_resources` | AppHost resources | Runtime baseline below | Runtime baseline lists command, flags, dashboard/API URLs, resource states, skipped Keycloak | Resources running/healthy; Keycloak skipped intentionally | passed | Dashboard login token redacted | N/A |
| 2 | HTTP Admin DAPR smoke | `eventstore-admin` | `admin-dapr-smoke-summary.json`; per-surface JSON artifacts | components, sidecar, actors, pub/sub, resiliency, health history covered | 8/8 endpoints returned 200 | passed | JWT not stored; response scrubbed for secret-like keys | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` |
| 3 | HTTP Admin DAPR smoke | remote EventStore metadata surfaces | `admin-dapr-sidecar-response.json`; `admin-dapr-actors-response.json`; `admin-dapr-pubsub-response.json` | sidecar, actors, pub/sub each report `Available`, `Unreachable`, or `NotConfigured` | Sidecar, actors, and pub/sub each reported enum value `1` = `Available`; remote endpoint `http://localhost:3501` | passed | Endpoint is localhost only | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` |
| 4 | HTTP Admin DAPR smoke | degraded Admin DAPR surfaces | Admin DAPR table below | Empty/degraded/error states remain visible | Pub/sub subscriptions observed empty but preserved as `subscriptions: []`; no HTTP degraded state hidden | passed | Raw state values omitted | `NO-ACTION` for empty subscription list; not a failure |
| 5 | CommandAPI seed plus Admin debugging smoke | event stream/debugging endpoints | `seeded-stream-summary.json`; `debugging-smoke-summary.json`; `debugging-*-shape.json` | blame, bisect, step-through, sandbox, trace-map exercised on same identifiers | Five-command seeded stream; all five debug surfaces returned 200 with shapes recorded | passed | Raw payload/body omitted from shape artifacts | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` |
| 6 | Evidence review | Epic 20 debugging limitations | `debugging-smoke-summary.json`; dispositions table | JSON/large-stream limits not claimed fixed | Evidence is smoke-only against a five-event stream; JSON reconstruction and large-stream concerns remain routed to DW3 | passed | Payload omitted | `ACCEPTED-DEBT` / `STORY:...` on relevant bullets |
| 7 | MCP stdio smoke | `Hexalith.EventStore.Admin.Mcp` | `mcp-stdout-transcript.jsonl`; `mcp-stderr.txt`; `mcp-smoke-summary.json` | initialize, tools/list, server name/version, stderr diagnostics captured | Initialize succeeded; server `hexalith-eventstore-admin`; 28 tools listed; diagnostics on stderr | passed | Token env redacted; stdout transcript body-shaped only | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` |
| 8 | MCP tool smoke | one read and one write-preview tool | `mcp-smoke-summary.json`; sanitized stdout/stderr transcripts | read succeeds or classified; write preview avoids destructive mutation | `stream-events` read returned shape; `consistency-trigger confirm=false` returned preview shape; before/after `consistency-list` calls succeeded | passed | Tool result bodies redacted to shape | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` |
| 9 | MCP session smoke | InvestigationSession fallback | `mcp-smoke-summary.json`; sanitized stdout/stderr transcripts | omitted optional scope args use session or are classified absent/broken/blocked | `session-set-context` then `stream-list` without tenant/domain succeeded; `session-get-context` showed active context | passed | Deterministic tenant/domain only | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` |
| 10 | MCP latency sample | `health-dapr` single-resource read | `mcp-smoke-summary.json`; Latency table below | timing method, cold/warm, sample count, retries, min/avg/max or raw durations recorded | 3 warm samples: 8.378 ms, 1.895 ms, 1.411 ms; sample-only, not SLA | sample-only | Token redacted | N/A |
| 11 | Markdown artifact review | evidence folder | this index | Required evidence tables and links are durable | Created index skeleton | passed | No secrets stored | Pending |
| 12 | `deferred-work.md` edit | DW2-relevant bullets | dispositions table below | Touched bullets carry clear disposition markers | Pending | evidence gap | N/A | Pending |
| 13 | Manual redaction review | all artifacts | this index and linked artifacts | Tokens, secrets, payloads, and sensitive values omitted | Redaction policy defined before capture | passed | Applies to all future artifacts | Pending |
| 14 | Git diff review | topology/contracts | final diff | No topology/product contract change unless smoke-proven narrow defect | Pending | evidence gap | N/A | Pending |
| 15 | Story bookkeeping | story and sprint status | story file; sprint-status.yaml | Dev Agent Record, File List, Change Log, Verification Status updated; review status only after evidence | Pending final validation/bookkeeping | evidence gap | N/A | Pending |

## Runtime Baseline Stoplight

| Time | Command | Prerequisite or resource | Expected result | Observed result | Stoplight | Blocker class | May dependent checks continue? | Artifact |
|---|---|---|---|---|---|---|---|---|
| 2026-05-05T13:50:07+02:00 | N/A | Evidence plan | Tables exist before checks | Created this index skeleton | passed | N/A | Yes | this index |
| 2026-05-05T13:51:21+02:00 | `EnableKeycloak=false aspire run --detach --non-interactive --format Json --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` | AppHost | Starts detached | AppHost PID 101960; dashboard `https://localhost:17017` with login token redacted | passed | N/A | Yes | Aspire CLI output in terminal session |
| 2026-05-05T13:51:36+02:00 | Aspire MCP `list_resources` | eventstore / eventstore-admin / eventstore-admin-ui / sample / sample-blazor-ui / tenants | Running + Healthy | All listed project resources Running + Healthy | passed | N/A | Yes | Aspire MCP resource data |
| 2026-05-05T13:51:36+02:00 | Aspire MCP `list_resources` | eventstore-dapr-cli / eventstore-admin-dapr-cli / sample-dapr-cli / tenants-dapr-cli | Running + Healthy | All listed DAPR CLI sidecars Running + Healthy | passed | N/A | Yes | Aspire MCP resource data |
| 2026-05-05T13:51:36+02:00 | Aspire MCP `list_resources` | `statestore`, `pubsub` | Running + Healthy | Both DAPR components Running + Healthy | passed | N/A | Yes | Aspire MCP resource data |
| 2026-05-05T13:51:36+02:00 | AppHost config | Keycloak | Skipped when `EnableKeycloak=false` | No `keycloak` resource present; symmetric JWT dev mode used | passed | N/A | Yes | Runtime command and resource data |

## Admin DAPR Checks

| Time | Surface | Endpoint or path | Expected result | Observed result | RemoteMetadataStatus | RemoteEndpoint | Classification | Artifact |
|---|---|---|---|---|---|---|---|---|
| 2026-05-05T13:52:51+02:00 | components | `/api/v1/admin/dapr/components` | Live component list or classified degraded result | 200; `statestore` component returned healthy/state.redis with capabilities | N/A | N/A | passed | `admin-dapr-components-response.json` |
| 2026-05-05T13:52:51+02:00 | sidecar | `/api/v1/admin/dapr/sidecar` | Local Admin sidecar plus remote status if configured | 200; appId `eventstore-admin`; runtime `1.17.4`; remote metadata available | Available | `http://localhost:3501` | passed | `admin-dapr-sidecar-response.json` |
| 2026-05-05T13:52:51+02:00 | actors | `/api/v1/admin/dapr/actors` | Actor metadata plus remote status | 200; AggregateActor, ProjectionActor, ETagActor active counts visible | Available | `http://localhost:3501` | passed | `admin-dapr-actors-response.json` |
| 2026-05-05T13:52:51+02:00 | pub/sub | `/api/v1/admin/dapr/pubsub` | Pub/sub metadata plus remote status and subscription shape | 200; `pubsub` component visible; `subscriptions: []` preserved | Available | `http://localhost:3501` | passed; empty subscriptions visible | `admin-dapr-pubsub-response.json` |
| 2026-05-05T13:52:51+02:00 | resiliency | `/api/v1/admin/dapr/resiliency` | Resiliency YAML read state | 200; configuration available with retry/timeout/circuit-breaker target bindings | N/A | N/A | passed | `admin-dapr-resiliency-response.json` |
| 2026-05-05T13:52:51+02:00 | health history | `/api/v1/admin/health`, `/health/dapr`, `/health/dapr/history` | Component health timeline or empty-timeline classification | 200; current DAPR health and history `hasData=true`, 5 entries, not truncated | N/A | N/A | passed | `admin-dapr-health-*.json` |

## RemoteMetadataStatus Matrix

| Time | Surface | RemoteMetadataStatus | RemoteEndpoint | Observed evidence | Classification | Artifact |
|---|---|---|---|---|---|---|
| 2026-05-05T13:52:51+02:00 | sidecar | Available | `http://localhost:3501` | JSON enum value `1`; EventStore sidecar metadata reachable | passed | `admin-dapr-sidecar-response.json` |
| 2026-05-05T13:52:51+02:00 | actors | Available | `http://localhost:3501` | JSON enum value `1`; actor runtime metadata reachable | passed | `admin-dapr-actors-response.json` |
| 2026-05-05T13:52:51+02:00 | pub/sub | Available | `http://localhost:3501` | JSON enum value `1`; pub/sub metadata reachable; subscriptions empty | passed | `admin-dapr-pubsub-response.json` |

## Canonical Seeded Stream Identifier Block

| Field | Value |
|---|---|
| TenantId | `tenant-a` |
| Domain | `counter` |
| AggregateId | `counter-dw2-20260505135410` |
| EventCount | 5 |
| CorrelationId | `d5bf0400-f474-49c4-b833-f83f77287a45` |
| Source | `seeded-stream-summary.json` |
| Payload policy | Payloads are not stored; only envelope shape and identifiers are recorded |

## Epic 20 Debugging Checks

| Time | Tool | Surface | Expected shape | Observed shape | Truncated? | Timeout status | Classification | Artifact |
|---|---|---|---|---|---|---|---|---|
| 2026-05-05T13:54:13+02:00 | blame | Admin API | Field provenance or classified failure | 200; shape `atSequence,timestamp,isTruncated,isFieldsTruncated,tenantId,domain,aggregateId,fields` | shape artifact only | no timeout | passed | `debugging-blame-shape.json` |
| 2026-05-05T13:54:13+02:00 | bisect | Admin API | Divergence search shape or classified failure | 200; shape includes `goodSequence,divergentSequence,totalSteps,isTruncated,steps` | shape artifact only | no timeout | passed | `debugging-bisect-shape.json` |
| 2026-05-05T13:54:13+02:00 | step-through | Admin API | Event step trace or classified failure | 200; shape includes event/state fields but raw bodies omitted | shape artifact only | no timeout | passed | `debugging-step-through-shape.json` |
| 2026-05-05T13:54:13+02:00 | sandbox | Admin API | Preview/sandbox result or classified failure | 200; shape includes `outcome,producedEvents,resultingStateJson,stateChanges`; raw bodies omitted | shape artifact only | no timeout | passed | `debugging-sandbox-shape.json` |
| 2026-05-05T13:54:13+02:00 | trace-map | Admin API | Correlation trace map or classified failure | 200; shape includes command status, produced events, affected projections | shape artifact only | no timeout | passed | `debugging-trace-map-shape.json` |

## MCP Checks

| Time | Phase | Command or JSON-RPC method | Tool | Expected result | Observed result | Classification | Stdout artifact | Stderr artifact |
|---|---|---|---|---|---|---|---|---|
| 2026-05-05T13:57:00+02:00 | startup | `dotnet run --no-build --configuration Release --project src/Hexalith.EventStore.Admin.Mcp/Hexalith.EventStore.Admin.Mcp.csproj` | N/A | Env accepted; stdout reserved for JSON-RPC | Stdio session completed; diagnostics emitted to stderr; process terminated by smoke harness after stdin close | passed | `mcp-stdout-transcript.jsonl` | `mcp-stderr.txt` |
| 2026-05-05T13:57:00+02:00 | initialize | `initialize` | N/A | Server name/version and capabilities | Server `hexalith-eventstore-admin`, version `1.0.0+7b0a6d...`, capabilities `logging`, `tools` | passed | `mcp-stdout-transcript.jsonl` | `mcp-stderr.txt` |
| 2026-05-05T13:57:00+02:00 | discovery | `tools/list` | N/A | Live tool list | 28 tools; required tools present (`ping`, `health-dapr`, `stream-list`, `session-*`, `consistency-trigger`) | passed | `mcp-stdout-transcript.jsonl` | `mcp-stderr.txt` |
| 2026-05-05T13:57:00+02:00 | representative read | `tools/call` | `stream-events` | Read result or classified failure | 22.506 ms; result shape `totalCount,continuationToken,items` | passed | `mcp-stdout-transcript.jsonl` | `mcp-stderr.txt` |
| 2026-05-05T13:57:00+02:00 | write preview | `tools/call` | `consistency-trigger` | Preview/approval boundary without mutation | `confirm=false` returned preview/action/endpoint/parameters/warning shape; before/after `consistency-list` calls succeeded | passed | `mcp-stdout-transcript.jsonl` | `mcp-stderr.txt` |
| 2026-05-05T13:57:00+02:00 | session fallback | `tools/call` | `session-set-context`, `stream-list`, `session-get-context` | Session values reused or gap classified | Session set for tenant/domain; `stream-list` omitted tenant/domain and returned stream-list shape; context still active | passed | `mcp-stdout-transcript.jsonl` | `mcp-stderr.txt` |

## Latency Samples

| Time | Tool | Timer source | Cold/warm | Sample count | Retries included? | Initialization included? | Admin API latency included? | Durations | Classification | Artifact |
|---|---|---|---|---:|---|---|---|---|---|---|
| 2026-05-05T13:57:00+02:00 | `health-dapr` | Python `time.perf_counter` around JSON-RPC `tools/call` | warm after initialize/tools-list | 3 | No | No | Yes | 8.378 ms, 1.895 ms, 1.411 ms; min 1.411 / avg 3.895 / max 8.378 | sample-only | `mcp-smoke-summary.json` |

## Blockers

| Time | Blocker class | Slice affected | First failure shape | Retry result | Final classification | Artifact | Next disposition |
|---|---|---|---|---|---|---|---|
| N/A | N/A | N/A | No runtime blocker encountered | N/A | N/A | N/A | N/A |

## Deferred-Work Dispositions

| Deferred-work item | Action | Marker | Evidence artifact | Notes |
|---|---|---|---|---|
| Admin DAPR / remote metadata live proof items | Close live evidence gap for this smoke run | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` | `admin-dapr-smoke-summary.json` | Sidecar/actors/pubsub remote metadata available |
| DAPR 1.17 pub/sub parser concern | No production change; live pub/sub endpoint returned 200 with empty subscriptions preserved | `NO-ACTION` | `admin-dapr-pubsub-response.json` | Empty subscription list means no live direct-rule sample was present |
| Epic 20 debugging live evidence / known limits | Close five-event smoke evidence while preserving JSON/large-stream debt | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`; `ACCEPTED-DEBT` | `debugging-smoke-summary.json` | DW3 still owns JSON delete/merge and large-stream hardening |
| Admin MCP runtime/protocol evidence | Close live startup/protocol/read/write-preview/session/latency evidence gap | `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` | `mcp-smoke-summary.json` | Stdout transcript body-shaped only; stderr diagnostics retained |

## How To Rerun

| Step | Command | Notes |
|---|---|---|
| Start Aspire | `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` | Skips Keycloak and uses symmetric dev JWT configuration |
| Inspect Aspire resources | Aspire MCP `list_resources` | Requires AppHost detected by Aspire MCP |
| Generate dev JWT | Generate HS256 token with issuer `hexalith-dev`, audience `hexalith-eventstore`, dev signing key, and admin/read claims in memory | Token must not be saved in artifacts |
| Admin DAPR smoke | GET `http://localhost:8090/api/v1/admin/dapr/{components,sidecar,actors,pubsub,resiliency}` plus `/api/v1/admin/health*` with redacted bearer token | Use redacted Authorization header |
| Seed stream | POST five `IncrementCounter` commands to `http://localhost:8080/api/v1/commands`, poll `/api/v1/commands/status/{correlationId}`, then recount Admin timeline | Use deterministic safe local identifiers; omit payloads |
| MCP smoke | `dotnet run --no-build --configuration Release --project src/Hexalith.EventStore.Admin.Mcp/Hexalith.EventStore.Admin.Mcp.csproj` | `EVENTSTORE_ADMIN_URL` and `EVENTSTORE_ADMIN_TOKEN` must be set; stdout/stderr captured separately |
| Structural validation | `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj --filter Dw2EvidenceIndexAtddTests` | Run after evidence/dispositions are complete |
