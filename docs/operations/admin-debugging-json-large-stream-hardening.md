# Admin Debugging JSON and Large-Stream Hardening

Date: 2026-05-05

This note records the DW3 architecture decision for Admin debugging surfaces. The CommandApi admin controllers are internal computation endpoints reached through DAPR service invocation by Admin.Server. Admin.Server remains the authorized facade for Admin UI, CLI, and MCP callers.

## Decision Ledger

| Topic | Decision | Disposition |
|---|---|---|
| JSON reconstruction model | Debugging state is reconstructed by merging stored event payload JSON. It does not execute domain `Apply` methods and is not canonical aggregate state. | supported |
| Omitted properties | Omission does not mean delete. The merge keeps previously reconstructed values and does not emit synthetic delete changes. | preserved-limitation |
| Explicit JSON `null` | Explicit `null` overwrites the previous JSON value and is visible as a field change. It is still only JSON-level evidence, not proof of a domain delete. | supported |
| Nested removal | Missing nested properties are treated like other omissions. No synthetic nested delete is inferred during event-payload replay. | preserved-limitation |
| Arrays | Arrays remain opaque leaf values. The diff reports the whole array path, not element paths or moves. | supported |
| Malformed JSON | Malformed payloads are skipped during reconstruction and are not copied into problem details. | supported |
| Non-object JSON | Valid non-object payloads are skipped; reconstruction only merges JSON objects. | supported |
| Empty field paths | Empty JSON property names are skipped before `FieldChange` creation to avoid surfacing 500 responses from malformed paths. | supported |
| Deep nesting | Payload parsing uses `System.Text.Json` depth limits before helper recursion. Payloads beyond parser limits are skipped as malformed JSON. | accepted-debt |
| Direct CommandApi bounds | Direct max parameters are rejected before actor reads when they exceed controller maximums. Non-positive values keep existing default-normalization behavior. | supported |
| Trace-map partial coverage | Any aggregate stream larger than the trace tail scan window reports `ScanCapped = true`, even when command-status metadata is absent or happens to match the found tail events. | supported |
| Internal trust boundary | `[AllowAnonymous]` on CommandApi admin controllers is accepted only for internal DAPR-invoked computation endpoints behind network/DAPR isolation. | accepted-debt |

## Direct Parameter Bounds

| Endpoint | Parameter | Default | Minimum behavior | Maximum | Above-limit behavior | Reason code |
|---|---:|---:|---|---:|---|---|
| timeline | `count` | 100 | `<= 0` normalizes to 100 | 1,000 | HTTP 400 before actor read | `count_above_limit` |
| blame | `maxEvents` | 10,000 | `<= 0` normalizes to 10,000 | 10,000 | HTTP 400 before actor read | `max_events_above_limit` |
| blame | `maxFields` | 5,000 | `<= 0` normalizes to 5,000 | 5,000 | HTTP 400 before actor read | `max_fields_above_limit` |
| bisect | `maxSteps` | 30 | `<= 0` normalizes to 30 | 30 | HTTP 400 before actor read | `max_steps_above_limit` |
| bisect | `maxFields` | 1,000 | `<= 0` normalizes to 1,000 | 1,000 | HTTP 400 before actor read | `max_fields_above_limit` |

Admin.Server facade options remain compatibility defaults. They are not the only protection; the CommandApi computation layer enforces the direct upper bounds above.

## Large-Stream Surface Matrix

| Surface | Current read pattern | Bound or signal | `GetEventsAsync(0)` disposition | Remaining debt |
|---|---|---|---|---|
| timeline | `GetEventsAsync(fromSequence)` materializes from the lower bound. | `count` is capped; response `TotalCount` and `ContinuationToken` expose truncation. | bounded-range-read | Actor still returns an array from the lower bound; true page-size range API remains future work. |
| event detail | `GetEventsAsync(sequenceNumber - 1)` reads from just before the target event. | 400 for sequence `< 1`; 404 when target is absent. | bounded-range-read | True single-event actor API would avoid materializing the tail. |
| blame | `GetEventsAsync(0)` full-stream read, then local `maxEvents` truncation. | `maxEvents` and `maxFields` capped; response keeps `IsTruncated` and `IsFieldsTruncated`. | preserve-legacy | Future actor range/snapshot API should avoid the full read. |
| bisect | `GetEventsAsync(0)` full-stream read for midpoint replay. | `maxSteps` and `maxFields` capped; `bad` beyond stream returns 400 with guidance. | preserve-legacy | Snapshot-aware reconstruction or range reads are future actor API work. |
| step-through | `GetEventsAsync(0)` full-stream read up to requested sequence. | `at` beyond stream returns 400. Empty or malformed field paths are skipped. | preserve-legacy | Future event-count metadata should distinguish count from highest sequence on non-contiguous streams. |
| sandbox | `GetEventsAsync(0)` full-stream read unless `AtSequence = 0`. | `AtSequence = 0` skips actor read; beyond-stream `AtSequence` returns 400. | preserve-legacy | Snapshot-aware state reconstruction is future actor API work. |
| diff helper | No standalone route; `JsonDiff` feeds blame, bisect, step-through, and sandbox responses. | Arrays are opaque, omitted properties are not deletes, empty paths are skipped. | accepted-debt | Domain `Apply` replay or JSON Patch semantics require a product decision. |
| trace map | `GetEventsAsync(0)` full stream, then latest 10,000 events are scanned. | `ScanCapped` and `ScanCapMessage` are set whenever the stream exceeds the scan window. | preserve-legacy | Actor tail/range scan API should replace full-stream loading. |

## Future Actor API Shape

A future actor API should expose bounded debugging reads without changing command processing:

- `GetEventsAsync(long fromSequence, int maxCount)` for bounded page reads.
- `GetEventsTailAsync(int maxCount)` for trace-map tail scanning.
- `GetEventCountAsync()` or stream metadata that distinguishes total event count from highest sequence number.
- `ReconstructStateAsync(long atSequence, long? snapshotSequence, int maxEvents)` or equivalent snapshot-aware debug reconstruction metadata.

Until that exists, Admin debugging output must describe truncation and partial coverage honestly and must not claim domain-state completeness from JSON payload replay.

## Trust Boundary

`AdminStreamQueryController` and `AdminTraceQueryController` use `[AllowAnonymous]` because they are internal CommandApi computation endpoints. They are intended to be reached through DAPR service invocation from Admin.Server, which applies user-facing authorization and forwards safe requests.

This is acceptable only when CommandApi is not exposed as a public internet endpoint and DAPR or network policy restricts service-to-service access. If CommandApi admin routes are exposed externally, if DAPR access control is weakened, or if multi-tenant admin callers bypass Admin.Server, service-to-service authentication work must be prioritized before relying on these endpoints.
