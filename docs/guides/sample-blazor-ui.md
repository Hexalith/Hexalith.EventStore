[<- Back to Documentation](../index.md)

# Sample Blazor UI

The sample Blazor UI demonstrates three ways a client can react to projection change notifications. It is a developer-facing demo, not an operations dashboard.

Open the `sample-blazor-ui` resource from the Aspire dashboard after the AppHost is running.

## Refresh Patterns

| Pattern | Page | Behavior | Use When |
|---------|------|----------|----------|
| Notification | `/pattern-notification` | Shows a persistent message when a projection changes. The user chooses when to refresh. | Users need control before replacing visible data. |
| Silent reload | `/pattern-silent-reload` | Re-runs the query automatically after a debounce window that follows a received signal. | The screen should refresh after received invalidation signals with minimal interruption. |
| Selective refresh | `/pattern-selective-refresh` | Lets independent components subscribe and refresh separately. | A page has multiple read-model widgets with different refresh needs. |

All three pages include the shared `CounterCommandForm` component so each pattern can submit `IncrementCounter`, `DecrementCounter`, and `ResetCounter` commands without leaving the page.

## Command Feedback Semantics

Green command feedback in the sample UI means the HTTP command submission succeeded, typically with `202 Accepted`.

It does not mean the command completed every downstream lifecycle stage. Domain processing, event persistence, projection writes, ETag regeneration, SignalR delivery, and UI refresh happen after the HTTP response. Use the command status endpoint, traces, logs, and query results when you need proof of final completion.

The sample intentionally shows only three submission states:

| State | Meaning |
|-------|---------|
| Idle | No command was submitted in the current browser session. |
| Success | The EventStore API accepted the command submission. |
| Error | The HTTP request failed or returned an unsuccessful response. |

Full command lifecycle visualization belongs to the Admin UI and dashboard surfaces, not this sample UI.

## SignalR and Query Boundaries

SignalR notifications are invalidation signals only. They do not contain projection data, ETags, command status, or a replay of missed signals. The Query API remains the authoritative source for current projection state.

The client should query the HTTP Query API on component initialization to establish baseline state before relying on future `ProjectionChanged` callbacks. It should then treat each `ProjectionChanged` callback as a prompt to re-query, usually with `If-None-Match`. A `304 Not Modified` response means the existing UI data can stay visible; a `200 OK` response provides the replacement payload and ETag.

Automatic reconnect and internal group rejoin restore future notification delivery only. They do not replay changes missed while the browser, circuit, or hub connection was down. If a client knows it reconnected, resumed from sleep, restored a page, or had other downtime, it should re-query the projections it displays. The current `EventStoreSignalRClient` does not expose a public reconnected callback for sample pages to subscribe to, so reconnect/resume refresh is application-owned lifecycle logic rather than behavior demonstrated by these pages.

| Situation | Sample behavior | Consumer responsibility |
|-----------|-----------------|-------------------------|
| Initial load | Each pattern queries the HTTP Query API during component initialization. | Establish baseline state before relying on future signals. |
| Signal receipt | Notification shows a refresh prompt; silent reload re-queries after debounce; selective refresh lets subscribed components re-query independently. | Treat the signal as a refresh hint, not as data. |
| Reconnect, resume, or page restore | The helper rejoins tracked groups internally for future signals; the sample pages do not demonstrate missed-signal replay. | Re-query displayed projections when the application knows downtime occurred. |
| User-triggered refresh | Notification pattern waits for the user to click refresh; other surfaces can add explicit refresh commands when needed. | Use explicit refresh to recover confidence when lifecycle detection is unavailable. |

## Smoke-Test Evidence Pattern

For future sample UI stories, record smoke-test evidence in the story's Dev Agent Record or under `_bmad-output/test-artifacts/{story-id}/`.

Use this minimum evidence block:

```markdown
### Sample Blazor UI Smoke Evidence

- Date:
- AppHost command:
- AppHost state: running / degraded / unavailable
- Resources checked:
  - eventstore:
  - sample:
  - sample-blazor-ui:
  - statestore:
  - pubsub:
- Browser target:
- Pattern pages:
  - Overview:
  - Notification:
  - Silent reload:
  - Selective refresh:
- Commands exercised:
  - IncrementCounter:
  - DecrementCounter:
  - ResetCounter:
- Observed results:
  - HTTP submission feedback:
  - Query/projection result:
  - SignalR or refresh behavior:
  - Error-path behavior, if tested:
- Evidence links:
  - Aspire trace:
  - Structured logs:
  - Console logs:
  - Screenshot or recording:
- Known gaps or skipped checks:
```

At minimum, include the commands exercised, observed result for each refresh pattern, and a link or reference to logs, traces, screenshots, or the reason those artifacts were unavailable.

## Component Test Decision

The current sample UI test strategy is build verification plus runtime smoke testing through the Aspire topology.

The repository already uses bUnit for the Admin UI, but the sample Blazor UI does not currently have a dedicated component test project. Keep smoke testing as the chosen strategy for these sample pages until a future story needs repeated component-level regression coverage. Revisit bUnit for the sample UI if future changes add complex client-only branching, state transitions that are hard to smoke test, or frequent UI refactors.

## Related Docs

- [Quickstart](../getting-started/quickstart.md)
- [Query & Projection API](../reference/query-api.md)
- [Command API](../reference/command-api.md)
