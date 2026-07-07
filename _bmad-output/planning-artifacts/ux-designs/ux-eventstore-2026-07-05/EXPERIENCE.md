---
name: Hexalith.EventStore Admin
status: final
created: 2026-07-05
updated: 2026-07-05
sources:
  - docs/brownfield/architecture.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  - https://fluentui-blazor-v5.azurewebsites.net/
---

# Hexalith.EventStore Admin - Experience Spine

## Foundation

Responsive web inside the Hexalith module shell. The UI system is FrontComposer with Blazor Fluent UI V5. `DESIGN.md` is the visual identity reference; this spine owns information architecture, behavior, states, interactions, accessibility, localization evidence, and journeys.

This is brownfield UX. Legacy `src/Hexalith.EventStore.Admin.UI` proves the current feature inventory, but the target is the EventStore UI service. All EventStore admin features appear under one Hexalith module menu item: **Event Store Admin**. The opened surface is a dashboard with tabbed child pages.

Primary users are administrators and platform operators. Stakes include internal operations, customer-facing administration, and regulated/support-critical production support. The default posture is support-safe, auditable, and fail-closed for sensitive actions.

Visual dependencies resolve through `DESIGN.md` tokens such as `{colors.app-bar-background}`, `{colors.canvas}`, `{components.dashboard-tabs.active-indicator}`, `{components.status-badge.warning-background}`, and `{components.issue-banner.warning-background}`. Visual references: [Fluent UI V5 desktop capture](imports/fluent-ui-v5-home-desktop.png), [Fluent UI V5 mobile capture](imports/fluent-ui-v5-home-mobile.png), [dashboard overview mock](mockups/dashboard-overview.html), and [command investigation mock](mockups/command-investigation.html). The spines win on conflict.

## Information Architecture

All Hexalith module UIs expose exactly one host-level menu entry. Child pages live inside the module dashboard as tabs, panels, or deep links.

| UI host/module | Host menu item | Opens | UX responsibility |
|---|---|---|---|
| EventStore UI service | Event Store Admin | EventStore dashboard | Administer and diagnose EventStore operational surfaces |
| Sample Blazor UI | Sample | Sample dashboard | Demonstrate command accepted submission without implying downstream completion |
| Tenants UI | Tenants | Tenants dashboard | Preserve projection-confirmed success and support-safe tenant access states |

The EventStore dashboard owns these child tabs:

| Dashboard tab | Legacy source routes | Purpose |
|---|---|---|
| Overview | `/` | Health summary, stream/event activity, stale state, recent operational evidence |
| Commands | `/commands` | Command lifecycle, status, accepted vs completed evidence, retries, correlation trace |
| Streams & Events | `/streams`, `/streams/{tenant}/{domain}/{aggregate}`, `/events` | Stream inventory, event timeline, state diff, stream detail, protected payload state |
| Projections | `/projections` | Projection status, lag, freshness, replay/project evidence, SignalR freshness signals |
| Tenants & Access | `/tenants`, admin role controls | Tenant isolation, user roles, authorization state, fail-closed visibility |
| Topology | `/dapr`, `/dapr/actors`, `/dapr/pubsub`, `/dapr/resiliency`, `/dapr/health-history`, `/services` | DAPR resources, actors, pub/sub, resiliency, app health, sidecar metadata, domain services |
| Storage & Snapshots | `/storage`, `/snapshots` | Storage growth, hot streams, snapshot policies, snapshot evidence, bounded cost signals |
| Recovery | `/health`, `/health/dead-letters`, `/consistency` | Health, dead letters, poison handling, consistency checks, operational recovery |
| Deferred & Backlog | `/backups`, `/compaction`, future backlog items | Honest unavailable/deferred operations; hide, disable, or show explicit unsupported state |
| Settings | `/settings` | Dashboard preferences, environment-safe settings, role visibility when allowed |

Deep links may remain for bookmarked details and direct support handoff, but they resolve within the owning module dashboard and keep that module's single host menu item selected.

IA closure rule: every legacy Admin.UI feature has a target EventStore tab, every source-required UI host has a module dashboard model, and every target surface supports an administrator/operator journey.

## Source Traceability

| Source requirement / decision | UX coverage | Evidence expected from stories |
|---|---|---|
| Projection-Confirmed Success | State Patterns, Flow 2, Flow 3, Flow 6 | UI shows success only after read-model/projection evidence, never after HTTP 202 or SignalR alone |
| Support-Safe State | Support-Safe Operations, Accessibility Floor, all mutation flows | No tokens, decoded JWTs, raw metadata, raw payloads, cursors, ETags, stack traces, or secrets rendered |
| NFR14 / AD-4 | Foundation, Source Traceability, Sample and Tenants flows | Interactive UI hosts consume client libraries and host no generated or hand-written per-message MVC command/query controllers |
| NFR15 / FR34 / AD-10 | Deferred & Backlog tab, State Patterns, Flow 4 | Deferred operations are hidden, disabled, or backed by `501`; no fake functional forms |
| AD-8 | State Patterns, Interaction Primitives, Flow 3 | SignalR is a freshness nudge only; polling/query evidence confirms visible success |
| AD-14 | Projection freshness indicator, State Patterns | Fresh/current/stale/unknown evidence comes through gateway metadata, not ad hoc payload fields |
| AD-15 | Projection freshness indicator, State Patterns, Source Traceability | Current/stale rendered only for projection-backed route provenance; the gateway ETag is an opaque validator, never projection evidence; handler-computed/unknown render `unknown` |
| FrontComposer / Fluent UI V5 governance | DESIGN.md, Component Patterns | Components use FrontComposer and Blazor Fluent UI V5 primitives before custom HTML/CSS |
| Accessibility/localization evidence | Accessibility Floor, Voice and Tone | WCAG 2.2 AA behavior, localized strings, no runtime sentence fragments |

## Voice and Tone

Microcopy is direct and operational. Brand posture lives in `DESIGN.md`.

| Do | Don't |
|---|---|
| "Projection evidence is stale." | "Something went wrong." |
| "Command accepted. Waiting for projection evidence." | "Command completed." after HTTP 202 |
| "Unavailable in this release." | "Coming soon!" |
| "Access denied for this tenant." | "No data found." when authorization denied |
| "Retry publication" | "Fix issue" |
| "Dead letter archived. Audit record written." | "Done!" without evidence |

Use complete strings suitable for localization. Avoid runtime sentence fragments, concatenated clauses, and grammar that depends on English word order.

## Component Patterns

Behavioral rules. Visual specs live in `DESIGN.md.Components`; token-dependent states reference `DESIGN.md` paths.

| Component | Use | Behavioral rules |
|---|---|---|
| Module entry | Host shell | One entry only per Hexalith module. EventStore uses **Event Store Admin**. The entry routes to dashboard and remains selected for all child tabs/deep links. |
| Dashboard shell | Module host | Uses FrontComposer shell and Fluent layout primitives. It owns app status, tenant/environment context, and utility actions. |
| Dashboard header | Dashboard root and tabs | Shows title, visible tenant/environment scope, connection freshness, and safe utility actions. Do not use hero copy. |
| Dashboard tabs | Child navigation | Tabs are stable, named by operator job, keyboard-operable, URL-addressable, and visually governed by `{components.dashboard-tabs.active-indicator}`. |
| Stat summary | Overview and tab summaries | Shows current value plus evidence state. Stale values remain visible only with explicit stale label/timestamp. |
| Filter bar | Evidence-heavy tabs | Filters sit above the grid they affect, persist in URL where useful, and never disclose denied tenant existence. |
| Evidence grid | Most tabs | Sort/filter/paginate. Row click opens a detail panel or deep link. Protected payloads stay redacted. |
| Status badge | Any state label | Uses text plus Fluent badge styling from `{components.status-badge}`. Color alone is never the state. |
| Issue banner | Any degraded state | Names scope, operational consequence, and next action. Uses `{components.issue-banner}` and exposes no raw internals. |
| Operation dialog | Mutating admin actions | Requires exact target identity, expected effect, permission context, and confirmation. Shows accepted state first; confirmation follows only when evidence arrives. |
| Detail panel | Commands, streams, events, projections | Multi-section details use `FluentAccordion`; primary evidence section expanded by default. |
| Multi-section panel | Pages, dialogs, details | Use `FluentAccordion` when two or more titled sibling sections exist. Do not hide a page's primary grid inside the accordion. |
| Command lifecycle tracker | Commands | Separates Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut. Uses text and status tokens. |
| Projection freshness indicator | Projections, Tenants, command results | Renders current/stale/unknown from gateway metadata, and renders `current`/`stale` only for projection-backed route provenance (AD-15); handler-computed or unknown-provenance responses render `unknown`. Unknown/stale generally disables mutation actions unless an exception is explicitly documented. |
| Deferred operation placeholder | Deferred & Backlog tab or hidden action | If visible, disabled with reason and tracking title only. No fake forms for unavailable backup, restore, import, compaction, GDPR erasure, OIDC login, aggregate test kit, or generator hardening. |
| Command palette | Optional accelerator | Search/navigate/act across dashboard tabs. It must obey role/tenant filtering and never reveal hidden resources. |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| Cold load | Dashboard | Skeletons matching stat/grid layout; no spinner-only full page unless shell is not ready. |
| Admin API unavailable | Global/dashboard | Issue banner: cannot reach `eventstore-admin`; last-known data may render as stale if available. |
| Stale data | Any tab | Keep last-known data with explicit stale label and last successful refresh time. Mutation actions disabled unless the story documents an exception. |
| SignalR disconnected | Header/status | Show degraded freshness signal; polling remains fallback. |
| Command accepted | Commands / mutating dialogs / Sample UI | Show accepted/in-flight state, correlation reference if support-safe, and expected evidence source. Do not show success. |
| Projection confirmed | Commands / Tenants / Projections | Show success only after read-model/projection evidence confirms the outcome. |
| Access denied | Any restricted tab/action | Fail closed with a support-safe denied state. Do not imply the resource exists. Focus returns to the filter/action that caused the denied result. |
| Empty tenant/domain | Tenants / Streams | Empty state explains no configured data for visible scope. It does not mention hidden denied resources. |
| Dead letters present | Recovery / Overview | Prominent warning with count, oldest age, tenant/domain impact, and safe retry/archive actions by role. |
| Deferred operation | Deferred & Backlog / action locations | Hidden by default or disabled with "Unavailable in this release." Server endpoint returns `501` if reachable. |
| Protected payload | Stream/event detail | Show redacted/protected status and metadata needed for support; never expose raw protected data. |
| Validation failure | Dialog/form | Inline Fluent validation; command not submitted. |
| Oversized admin request | Dialog/form/API result | Fail safely with concise message and no raw payload echo. |

Tab-state coverage:

| Surface | Empty | Stale/offline | Permission denied | Mutation policy |
|---|---|---|---|---|
| Overview | No visible activity for selected scope | Last-known stats with timestamp | Hide denied counts | No direct mutation |
| Commands | No commands match filters | Disable retry/resubmit until evidence current | No result disclosure for denied tenant | Accepted -> evidence pending -> terminal state |
| Streams & Events | No visible streams/events | Timeline labels stale evidence | Hide protected or denied stream existence | Read-only unless approved support action exists |
| Projections | No projections for visible scope | Lag and unknown freshness explicit | Hide denied projections | Replay/project actions role-gated and evidence-confirmed |
| Tenants & Access | No visible tenants/users | Disable role mutation while stale | Denied state without confirming tenant existence | Role changes require projection-confirmed success |
| Topology | No visible DAPR/service resources | Show last check and degraded freshness | Hide denied service metadata | Read-only triage |
| Storage & Snapshots | No snapshot/storage data | Show stale cost/storage indicators | Hide denied storage metadata | Snapshot operations disabled unless implemented and current |
| Recovery | No dead letters or consistency issues | Warning with last successful check | Hide denied tenant/domain impact | Retry/archive audited and evidence-confirmed |
| Deferred & Backlog | No visible deferred items for role | No runnable mutation | Hidden unless role may see backlog state | Hidden or disabled; reachable server path returns `501` |
| Settings | No editable settings for role | Disable save while stale | Hide denied settings | Save only scoped preferences or implemented environment-safe options |

## Interaction Primitives

- Primary navigation: host module item -> dashboard tabs -> grid/detail drill-in.
- Keyboard: tabs, grids, dialogs, filters, command palette, and accordions must be fully keyboard-operable.
- Deep links: direct URLs land inside the dashboard and select the right tab/detail.
- Refresh: SignalR is a freshness nudge only; polling and explicit refresh fetch evidence.
- Filtering: tenant/domain/status filters are visible above the affected grid and persist in the URL where useful.
- Mutations: submit -> accepted -> evidence pending -> projection-confirmed or terminal non-success. Never collapse accepted into success.
- Destructive or recovery actions: role-gated, dialog-confirmed, audited, and support-safe.
- Mobile/narrow widths: read/triage workflows always work. Each mutation is either fully usable, disabled with a reason, or marked desktop-required in a support-safe message.

## Accessibility Floor

Behavioral accessibility. Visual contrast inherits from Fluent/FrontComposer and is governed by `DESIGN.md`.

- WCAG 2.2 AA target for dashboard and admin workflows.
- One focusable page title per dashboard view or selected tab.
- Host skip link reaches dashboard content.
- `FluentTabs`, `FluentDataGrid`, `FluentDialog`, `FluentAccordion`, and inputs expose accessible names and roles.
- Status badges include text, not color alone.
- Grids support screen-reader row context for tenant, domain, aggregate, status, and timestamp.
- Access-denied states include an accessible state label, route context, and a safe next action without confirming hidden resource existence.
- Keyboard focus returns to the initiating control after dialogs, denied results, validation failures, and failed mutations.
- Reduced motion avoids animated transitions for state changes; show final state directly.
- Stable `data-testid` selectors are part of the implementation contract for dashboard tabs, filters, status badges, dialogs, and evidence rows.

Live-region priority:

| Transition | Priority |
|---|---|
| Command accepted, evidence pending, projection confirmed | Polite unless the dialog is still active |
| Terminal command failure, access denied, destructive action rejected | Assertive |
| Stale-data transition, SignalR disconnect/reconnect | Polite |
| Validation failure in active form | Inline field association plus polite summary |

Localization evidence:

- All user-visible copy comes from resource-backed complete strings.
- No runtime sentence assembly, string concatenation, or English-only plural grammar in UI code.
- Tests may use stable selectors and state identifiers, not translated text, as their primary hooks.
- Screens that show identifiers keep labels translatable while preserving raw identifier values.

## Responsive & Platform

| Width | Behavior |
|---|---|
| `>= 1280px` | Full dashboard layout. Host navigation visible. Tabs remain horizontal. Grids use full columns. |
| `960-1279px` | Compact host navigation. Dashboard tabs may scroll horizontally. Grids prioritize core columns and move secondary metadata to detail panels. |
| `< 960px` | Host navigation collapses. Dashboard retains tabs with horizontal scrolling or overflow. Read/triage flows remain usable; operator mutations use viewport-sized Fluent dialogs or are disabled with a desktop-required reason. |

The primary surface is desktop/laptop operations. Mobile is supported for incident triage, status checks, and simple recovery visibility.

## Support-Safe Operations

The UI must never render bearer tokens, decoded JWT payloads, raw EventStore metadata, raw event payloads, raw protected payloads, stack traces, cursor internals, ETag internals, secret values, or unbounded SignalR metadata.

Support copy may show safe identifiers only when they are required for investigation and allowed by the relevant contract. Identifiers use EventStore naming and ULID-safe semantics; never validate or describe these IDs as GUIDs.

Admin state mutations must be attributable. The UI shows enough audit outcome to support operators without exposing sensitive internals.

Unavailable operation visibility policy:

| Capability state | Default UI | Privileged/admin visibility | Server behavior |
|---|---|---|---|
| Not implemented and not useful for current role | Hidden | Hidden | `404` or no route preferred |
| Deferred but useful for operator planning | Hidden from non-operators | Disabled/read-only backlog row with "Unavailable in this release." | `501` if reachable |
| Implemented but disabled by stale/unknown evidence | Disabled with stale/unknown reason | Disabled with evidence reason | No mutation submitted |
| Implemented and evidence current | Role-gated action | Role-gated action | Accepted -> evidence-confirmed |

## Inspiration & Anti-patterns

- **Lifted from Fluent UI Blazor V5 documentation:** app bar, dense sidebar/shell discipline, white content canvas, system typography, compact component examples, restrained informational callouts.
- **Lifted from FrontComposer:** one module entry per Hexalith module, generated/component-first UI, shell-owned navigation primitives, Fluent conformance.
- **Rejected - legacy Admin.UI route sprawl:** separate top-level menu entries for every feature do not fit the Hexalith module model.
- **Rejected - fake admin capability:** backup, restore, import, compaction, and backlog capabilities must not look operational until implemented.
- **Rejected - HTTP 202 success UI:** command acceptance is not user-visible success.
- **Rejected - raw diagnostic dump:** support-safe evidence beats exposing raw payloads, tokens, stack traces, cursors, ETags, or metadata.

## Key Flows

### Flow 1 - Incident triage (Nora, platform operator, during a tenant outage)

1. Nora opens Hexalith and selects **Event Store Admin** from the module menu.
2. The dashboard opens on Overview. Health and stream activity show stale labels because the Admin API missed the last polling cycle.
3. Nora selects the **Recovery** tab.
4. The Dead Letters section shows a warning count, oldest age, affected tenant/domain, and safe action availability.
5. Nora opens the oldest dead-letter row. The detail panel expands primary evidence first and keeps raw payload protected.
6. She chooses **Retry publication**. A `FluentDialog` confirms target tenant/domain/message and required role.
7. The UI shows "Retry accepted. Waiting for delivery evidence."
8. SignalR nudges the dashboard; polling fetches updated dead-letter and projection evidence.
9. **Climax:** Recovery shows the dead-letter count reduced and the audit/evidence section confirms the retry outcome. Nora can cite the safe audit record in the incident channel.

Failure: retry remains pending or fails terminally -> the row stays in Recovery with terminal state, retry count, safe error classification, and no raw stack trace.

### Flow 2 - Admin tenant access review (Marcel, administrator, onboarding a support engineer)

1. Marcel opens **Event Store Admin** and selects **Tenants & Access**.
2. He filters to the tenant visible to his role.
3. The tenant row expands to users and roles. Freshness is current, so role mutation is enabled.
4. Marcel opens the add/change-role dialog, enters the engineer identity, and confirms.
5. The UI shows command accepted, then waits for projection evidence.
6. **Climax:** The tenant user grid updates with the engineer's role and shows projection-confirmed success. The action audit is visible without exposing tokens or decoded claims.

Failure: tenant visibility is denied -> the grid shows an access-denied state without confirming whether the tenant exists.

### Flow 3 - Command investigation (Lea, platform operator, tracing a customer-reported failure)

1. Lea opens **Event Store Admin** and selects **Commands**.
2. She searches by support-provided message/correlation id.
3. The command lifecycle row shows `EventsStored` but not `EventsPublished`.
4. Lea opens the detail panel. The pipeline tracker separates command status, persisted events, publish state, affected projections, and audit evidence.
5. She drills into **Streams & Events** using the safe stream link.
6. The stream timeline shows the persisted event and protected payload state; projection evidence remains stale.
7. **Climax:** Lea can report that the event was committed but publication evidence is missing, and she routes the issue to recovery without resubmitting the command as if it failed before persistence.

Failure: the search id is malformed -> inline validation explains accepted identifier shape without saying GUID.

### Flow 4 - Deferred operation discovery (Imani, administrator, looking for backup/restore)

1. Imani opens **Event Store Admin** and searches for backup from the command palette.
2. Because backup/restore/import are deferred, no runnable operation appears.
3. She opens **Deferred & Backlog**.
4. The page lists Backup/Restore/Import as unavailable in this release with implementation/backlog status, not operational controls.
5. **Climax:** Imani understands the operation is not available and cannot accidentally trigger a fake backup workflow.

Failure: an old deep link reaches `/backups` -> it resolves inside Deferred & Backlog or shows a disabled state backed by server `501`.

### Flow 5 - Sample accepted submission (Alex, developer evaluating the Counter sample)

1. Alex opens the Sample module from its single host menu item.
2. The Sample dashboard shows a command form and the current visible counter projection.
3. Alex submits an increment command.
4. The UI validates the form and sends the command through the EventStore client library.
5. The UI shows "Command accepted. Waiting for projection evidence." It does not show completion.
6. A freshness signal or explicit polling updates the visible projection.
7. **Climax:** The sample shows the counter value only when query/projection evidence changes. The accepted state remains separate from confirmed state.

Failure: command submission returns accepted but no projection evidence arrives before timeout -> the UI stays evidence-pending or stale, with a retry/refresh option and no success message.

### Flow 6 - Tenants projection-confirmed update (Priya, tenant administrator, changing access)

1. Priya opens the Tenants module from its single host menu item.
2. The Tenants dashboard shows only tenants visible to her role.
3. She opens a tenant user row and starts a role-change dialog.
4. The dialog confirms the exact tenant, user, role, and permission context.
5. Priya submits the change. The UI calls the client library and shows accepted/evidence-pending state.
6. The Tenants read model refreshes with current evidence metadata.
7. **Climax:** The row shows the new role with projection-confirmed success and a support-safe audit reference.

Failure: Priya lacks access or freshness is unknown -> the mutation is hidden or disabled, focus returns to the triggering control, and the UI does not confirm whether hidden tenants/users exist.

## Non-Blocking Assumptions

- The EventStore UI service will provide the FrontComposer host integration point for the single **Event Store Admin** module entry.
- Dashboard tab names may change, but the grouping must preserve the same feature coverage and single-menu-entry rule.
- Mobile/narrow support is for triage and read workflows first; full operational mutation ergonomics remain desktop-first.
