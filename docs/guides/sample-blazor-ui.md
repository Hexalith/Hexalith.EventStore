[<- Back to Documentation](../index.md)

# Sample Blazor UI

The sample Blazor UI demonstrates three ways a client can react to projection change notifications. It is a developer-facing demo, not an operations dashboard.

Open the `sample-blazor-ui` resource from the Aspire dashboard after the AppHost is running.

## Refresh Patterns

| Pattern | Page | Behavior | Use When |
|---------|------|----------|----------|
| Notification | `/pattern-notification` | Shows a persistent message when a projection changes. The user chooses when to refresh. | Users need control before replacing visible data. |
| Silent reload | `/pattern-silent-reload` | Re-runs the query automatically after a short debounce. | The screen should stay current with minimal interruption. |
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

SignalR projection messages are invalidation signals only. They do not carry authoritative projection data.

The client should treat a `ProjectionChanged` message as a prompt to re-query the HTTP Query API, usually with `If-None-Match`. The Query API remains the source of projection state. A `304 Not Modified` response means the existing UI data can stay visible; a `200 OK` response provides the replacement payload and ETag.

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
