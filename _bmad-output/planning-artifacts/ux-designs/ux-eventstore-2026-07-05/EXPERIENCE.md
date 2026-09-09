---
name: Hexalith.EventStore Admin
status: draft
created: 2026-07-05
updated: 2026-09-09
reviewed_repository_revision: 0825f0dcde69915a74c1b6ebcbb36f14ded04283
sources:
  - docs/brownfield/architecture.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md
  - https://fluentui-blazor-v5.azurewebsites.net/
---

# Hexalith.EventStore Admin — Experience Spine

## Foundation

The product is a responsive operations web app inside the Hexalith module shell. The UI system is `Hexalith.FrontComposer.Shell` plus `Hexalith.FrontComposer.Contracts.UI`, with Blazor Fluent UI V5. `DESIGN.md` is the visual identity reference; this spine owns information architecture, behavior, states, interactions, accessibility, localization, and journeys.

This is a brownfield target contract. Stories 7.4, 7.5, 7.14, 7.19, and 7.20 remain backlog; the current UI contains partial legacy behavior. Neither this spine nor a future `status: final` value claims that the target is implemented or that implementation, release, deployment, migration, or readiness is authorized.

### Runtime and presentation identities

| Concern | Binding | Rule |
|---|---|---|
| Project and assembly | `src/Hexalith.EventStore.Admin.UI` / `Hexalith.EventStore.Admin.UI` | Evolve in place; no second UI executable or duplicate page implementation. |
| Admin API/service | `eventstore-admin` | Separate typed Admin service boundary; never confuse with UI identity. |
| UI service/resource/DAPR/container | `eventstore-admin-ui` | Retained across AppHost, deployment, and container publishing. |
| FrontComposer module | `event-store-admin` | Exactly one host-level module registration. |
| Visible label | **Event Store Admin** | Remains selected for every tab and deep link. |
| UI dependencies | `Hexalith.FrontComposer.Shell`, `Hexalith.FrontComposer.Contracts.UI`, Blazor Fluent UI V5 | Resolve the FrontComposer pair as one compatible Builds-catalog family in source and package modes; do not freeze a local version. |

### Source authority

Authority flows one way: the PRD defines product intent and the current readiness state; architecture defines system decisions; epics define implementation slices and acceptance ownership; the brownfield architecture records observed legacy scope; the bound PRD validation report is authoritative for the current `Reject` evidence; and the official Fluent V5 documentation defines upstream component behavior. `ux.md`, this folder's `index.md`, mocks, and review files are downstream handoffs or evidence, not upstream authority.

| Reviewed source | Input SHA-256 captured on 2026-09-09 before downstream digest repinning |
|---|---|
| `docs/brownfield/architecture.md` | `3cddf6eb593fb28d90b8dd9d54562cb28bb4ea2a4f5f15c6a51b77f06bf5a0a3` |
| `_bmad-output/planning-artifacts/prd.md` | `b99effdb414209da373433d9b4d2075072ca950e89534b22b81155236f650662` |
| `_bmad-output/planning-artifacts/architecture.md` | `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51` |
| `_bmad-output/planning-artifacts/epics.md` | `5a5c03d1205ee3741978dd96501d345867860cfd13a737dfecc8c011805e1009` |
| `_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md` | `17f378d0686d9840938cf835ea9227a2163c39ba210597b2ada40f57aac801d3` |

The reviewed revision identifies the repository base; the input digests also capture newer in-workspace source changes. Any later digest or revision change reopens source reconciliation. Current PRD readiness is `blocked` / `reject`, and architecture is under a draft reconciliation; historical July and August readiness verdicts do not override either state.

### Open assumptions

- `[ASSUMPTION]` The live `/types` catalog, including `events`, `commands`, and `aggregates` inner tabs, belongs under Streams & Events. Current sources require preserving the route but do not authorize its destination. Story 7.14 must ratify or replace this placement before implementation.
- The UX does not invent a numeric freshness horizon. Story 7.5 must bind the authoritative horizon and clock basis from the typed contract or configuration before dependent mutations can become available.

## Information Architecture

The host exposes one `event-store-admin` module. Its dashboard owns ten ordered tabs and all detail routes.

| Dashboard tab | Canonical child/deep-link routes | Purpose | Visual coverage |
|---|---|---|---|
| Overview | `/` | Authorized health summary, activity, freshness, and recent evidence | Mocked: `mockups/dashboard-overview.html` |
| Commands | `/commands` | Command lifecycle, accepted versus terminal evidence, and investigation | Mocked: `mockups/command-investigation.html` |
| Streams & Events | `/streams`, `/streams/{tenant}/{domain}/{aggregate}`, `/events`, `/types` | Stream/event evidence, protected outcomes, and type catalog | Spine-only |
| Projections | `/projections` | Operational status, authoritative lifecycle, lag, freshness, and rebuild evidence | Spine-only |
| Tenants & Access | `/tenants` | Tenant-visible access state and role mutation | Spine-only |
| Topology | `/dapr`, `/dapr/actors`, `/dapr/pubsub`, `/dapr/resiliency`, `/dapr/health-history`, `/services` | DAPR and service operations; not the legacy tenant/domain navigator | Spine-only |
| Storage & Snapshots | `/storage`, `/snapshots` | Storage and implemented snapshot evidence without implying open runtime work is complete | Spine-only |
| Recovery | `/health`, `/health/dead-letters`, `/consistency` | Health, dead letters, poison handling, consistency, and safe recovery | Spine-only |
| Deferred & Backlog | `/backups`, `/compaction` | Honest read-only unavailable-capability disposition | Spine-only |
| Settings | `/settings` | Implemented environment-safe preferences and role-visible settings | Spine-only |

The external Sample and Tenants UIs remain separate module dashboards. Sample demonstrates accepted submission without false completion; Tenants demonstrates projection-confirmed outcomes.

### Canonical routing contract

The router is the source of truth. On arrival, it selects the owning module, tab, inner view, and bounded filters. Activating a tab navigates to that tab's canonical root URL. Back, forward, reload, and bookmarks replay router state; tabs never maintain a competing navigation state.

| Route | Owning tab/view | Allowed route state | Failure behavior |
|---|---|---|---|
| `/` | Overview | Authorized environment/tenant context | Unavailable shell shows bounded recovery; no false selected child view. |
| `/commands` | Commands | Allow-listed safe filters and opaque paging | Malformed filter is rejected inline; denied scope reveals no result existence. |
| `/streams` | Streams & Events / streams | Safe tenant/domain filters and opaque paging | Cross-tenant or malformed scope fails closed. |
| `/streams/{tenant}/{domain}/{aggregate}` | Streams & Events / stream detail | Typed, encoded path identities; no raw payload in URL | Invalid identity shows a support-safe route error; denial does not confirm the stream. |
| `/events` | Streams & Events / events | Allow-listed safe filters and opaque paging | Invalid filter performs no query. |
| `/types` | Streams & Events / Type Catalog | `tab=events`, `tab=commands`, or `tab=aggregates`; omitted means the catalog default | Unknown tab falls back to the catalog default with a bounded notice. Placement is `[ASSUMPTION]`. |
| `/projections` | Projections | Safe status/filter state; opaque paging | Missing authoritative provenance renders `Unknown`. |
| `/tenants` | Tenants & Access | Authorized visible-scope filters only | Denial or wrong scope reveals no tenant existence. |
| `/dapr` | Topology / summary | No secret-bearing state | Unavailable data is not empty or healthy. |
| `/dapr/actors` | Topology / actors | Allow-listed safe filters | Denied actor data is omitted without counts. |
| `/dapr/pubsub` | Topology / pub/sub | Allow-listed safe filters | Broker internals and credentials never render. |
| `/dapr/resiliency` | Topology / resiliency | Allow-listed safe filters | Raw configuration never renders. |
| `/dapr/health-history` | Topology / health history | Bounded time/filter state | Deferred history uses the canonical unavailable state. |
| `/services` | Topology / services | Allow-listed safe filters | Internal endpoints and claims never render. |
| `/storage` | Storage & Snapshots / storage | Safe filters and opaque paging | Unknown cost/storage evidence is labelled, never inferred. |
| `/snapshots` | Storage & Snapshots / snapshots | Implemented read evidence only | Open snapshot work is unavailable, not simulated. |
| `/health` | Recovery / health | Authorized safe health scope | API health semantics remain distinct from authenticated UI state. |
| `/health/dead-letters` | Recovery / dead letters | Safe tenant/domain filters and opaque paging | Denied scope reveals no count or age. |
| `/consistency` | Recovery / consistency | Implemented read checks only | Unsupported mutation remains absent. |
| `/backups` | Deferred & Backlog / backup | Read-only tracking context | Exact unavailable state; no form, job, progress, or accepted result. |
| `/compaction` | Deferred & Backlog / compaction | Read-only tracking context | Exact unavailable state; no form, job, progress, or accepted result. |
| `/settings` | Settings | Implemented preferences only | Stale or revoked scope disables save with an associated reason. |

Story 7.14's machine-validated route manifest supplies exact parameter sizes and redirects. This spine does not invent numeric bounds. The current tenant/domain navigator becomes a Streams & Events filter/drill-in; **Topology** is reserved for DAPR and service operations.

The eleven journeys below cover the information architecture: every tab has a journey or is a named waypoint with an explicit action boundary, and the Sample and Tenants consumer surfaces retain separate journeys.

Component health under `/services` belongs to Topology. The aggregate health summary at `/health`, dead letters, and consistency belong to Recovery; shared health presentation never creates competing route ownership.

Story 7.14 aligns the brownfield shell title **Hexalith EventStore Admin** to the canonical **Event Store Admin** label and keeps breadcrumbs route-derived within the single module. Story 7.20 guards the development role switcher as local-only diagnostic behavior or retires it, and removes the wide-screen warning because triage and safe simple recovery remain usable below 960 CSS pixels.

## Voice and Tone

Microcopy is direct, complete, localizable, and evidence-specific. Brand posture lives in `DESIGN.md`.

| Do | Don't |
|---|---|
| “Command accepted. Waiting for authoritative evidence.” | “Command completed.” after HTTP `202` |
| “Outcome unknown—do not resubmit. Refresh status.” | “Try again.” after a timeout |
| “Projection evidence is stale.” | “Something went wrong.” |
| “Unavailable in this release.” | “Coming soon!” |
| “Access cannot be confirmed for this scope.” | “No data found.” after denial |
| “Protected value could not be read. Reason: malformed.” | A generic “redacted” label for every typed outcome |

## Evidence and Mutation Contract

### Authoritative evidence fields

The typed facet and outcome contracts in Story 7.5 must represent these concepts. Until they do, the UI renders `Unknown` and disables dependent mutations; pages never infer missing values.

| Evidence concept | Required behavior |
|---|---|
| Provenance | Exact contract identity: `ProjectionBacked`, `HandlerComputed`, or `Unknown`. Only `ProjectionBacked` permits a concrete projection lifecycle. |
| Lifecycle | `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, or `LocalOnly`; missing, invalid, expired, handler-computed, or provenance-mismatched evidence renders the UX fallback `Unknown`. |
| Evidence source | Names the authoritative typed facet/producer, not a UI calculation. Operational projection status remains distinct from consumer projection lifecycle. |
| Observed at | Source observation time shown in the operator's locale/time zone. |
| Last refresh | Client retrieval time, kept distinct from observation time. |
| Freshness horizon | Supplied by the authoritative contract/configuration and evaluated on its declared clock basis. No local numeric horizon is invented. |
| Clock basis | Names the time basis used to evaluate expiry and skew. |
| Projection version | Present only when the authoritative projection contract supplies it. |
| Terminal command evidence | Names authoritative terminal state and source. `Completed` remains distinct from a later projection-confirmed read-model outcome when the operation requires both. |
| Stable operation/audit identity | Enables status refresh and audit agreement without resubmission; must be support-safe. |

`MessageId` is the command-status lookup key. `CorrelationId` is the trace key shown only when authorized and support-safe. The UI calls the typed Admin client and never constructs a status URL from either identifier.

FR4/NFR8 own projection provenance and lifecycle. FR36 owns consumer parity closure and does not define lifecycle. ETags, cursors, cache hits, response age, SignalR, elapsed time, and locally computed data are opaque or advisory and never prove currentness, version, or completion. `LocalOnly` never confirms success.

### Mutation progression

1. Validate input and current authorized scope; invalid or oversized input performs no call.
2. Open the **Operation dialog** and freeze its displayed context.
3. Revalidate principal, role, environment, tenant, target, pre-state, freshness, effect, risk, blast radius, and reversibility on submit.
4. Any mismatch, expiry, revocation, or conflict stops submission, clears protected transient input, cancels background retry, and returns focus to the changed fact or stable initiator.
5. Submit once through the typed client; show `Accepted`, then `EvidencePending`.
6. **Refresh status** queries the same stable operation identity. It never replays the mutation.
7. Show projection-confirmed success only when all authoritative terminal, projection, and audit evidence required by the route agrees.
8. A timeout or ambiguous transport result produces the persistent message “Outcome unknown—do not resubmit. Refresh status.”
9. **Retry mutation** appears only after authoritative, retryable terminal non-success; it preserves the approved operation identity policy and repeats scope confirmation.

## Component Patterns

Behavioral rules below pair exactly with `DESIGN.md.Components`.

| Component | Behavioral contract |
|---|---|
| Dashboard shell | **Dashboard shell** uses `FrontComposerShell`; exposes shell landmarks, auth/freshness state, and a skip target. Protected state is cleared on identity or scope invalidation. |
| Module navigation | **Module navigation** uses `FrontComposerNavigation`; one `event-store-admin` entry, route-derived selection, accessible name and current state, and no feature-level host entries. |
| Page layout | **Page layout** uses `FcPageLayout`; establishes main/content landmarks and `{spacing.section-gap}` without changing evidence semantics during reflow. |
| Dashboard header | **Dashboard header** uses `FcPageHeader`; one focusable title plus authorized environment, tenant, freshness, last-refresh, and bounded utilities. Scope changes announce once and invalidate stale action state. |
| Dashboard tabs | **Dashboard tabs** use `FcPageTabs`; `ActiveTabId` is derived from the route segment and `ActiveTabIdChanged` navigates to the canonical URL. The tablist is named, exposes selected/disabled state, and follows Fluent keyboard behavior. Horizontal tab scrolling is an allowed layout-only CSS exception, never alternate navigation state. |
| Stat summary | **Stat summary** pairs every number with evidence state, source, and observation time. Stale values remain visible only with an explicit stale label. |
| Filter bar | **Filter bar** labels each control, applies only to its associated grid, preserves safe filters in the URL, and never autocompletes denied identities. |
| Evidence grid | **Evidence grid** uses `FluentDataGrid`; exposes accessible row/column context, sort state, busy state, selection, pagination, and one discoverable row-action location. Updates preserve focus, scroll, selection, and expanded detail. |
| Status badge | **Status badge** uses `FcStatusBadge`; accessible name/value includes canonical state text and never depends on color. |
| Issue banner | **Issue banner** names affected visible scope, consequence, and a reachable safe action. It is persistent while the condition holds and never exposes raw internals. |
| Operation dialog | **Operation dialog** is modal, labelled, described, cancellable, and focus-contained. It freezes and displays principal, environment, tenant, target, authoritative pre-state, effect, blast radius, reversibility, and expected evidence, then revalidates all of them on submit. |
| Detail panel | **Detail panel** is an EventStore-owned labelled `aside` composed with `FluentCard`; it keeps the source row selected, moves below the grid when space is constrained, and returns focus to that row or a stable grid fallback when closed or removed. |
| Multi-section panel | **Multi-section panel** uses one `FluentAccordion` for two or more titled siblings; headers expose expanded state, primary evidence opens by default, and the only primary grid is never hidden in it. |
| Command lifecycle tracker | **Command lifecycle tracker** exposes an ordered list and current step for `Received`, `Processing`, `EventsStored`, `EventsPublished`, `Completed`, `Rejected`, `PublishFailed`, and `TimedOut`; it names source and observation time. |
| Projection freshness indicator | **Projection freshness indicator** renders `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, or `Unknown` only from the evidence contract below. Its text, icon, and Fluent V5 color mapping match the shipped Tenants lifecycle contract. |
| Projection connection status | **Projection connection status** uses `FcProjectionConnectionStatus` only for connection and reconciliation notices. It can trigger a bounded refetch but never supplies provenance, lifecycle, or completion evidence. |
| Loading skeleton | **Loading skeleton** matches the eventual layout, exposes one busy state on the owning region, and suppresses nonessential shimmer under reduced motion. |
| Empty state | **Empty state** appears only after an authoritative successful query for visible scope and has a safe next action. Loading, denied, unavailable, and stale are not empty. |
| Deferred operation placeholder | **Deferred operation placeholder** is hidden or read-only with tracking context and exact copy “Unavailable in this release.” It exposes no form, submit, accepted, retry, job, or progress behavior. |
| Command palette | **Command palette** uses `FcCommandPalette`; focus enters search, results announce changes without flooding, Escape closes, focus returns to the trigger, and entries obey current route, tenant, role, and deferred policy. |
| Refresh controls | **Refresh controls** separate manual status refresh, automatic-refresh pause/resume, and an approved cadence selector. Refresh never submits or retries a mutation and preserves view context. |
| Live status regions | **Live status regions** consist of one scoped view region and one operation region. They announce transitions only, coalesce repeats, and keep terminal outcomes visible outside transient toasts. |
| Protected outcome | **Protected outcome** maps a typed unreadable result to bounded localized copy and an authorized safe reason code while protected bytes remain absent from every client channel. |
## State Patterns

### Cross-surface states

| State | Treatment and recovery |
|---|---|
| Cold load | **Loading skeleton** with one regional busy state; no spinner-only page. |
| Refreshing | Keep the last complete view, mark refresh in the view live region, and preserve focus, scroll, filters, selection, expansion, and open dialog. |
| Empty | **Empty state** only after a successful authoritative query for current visible scope. |
| Stale/offline | Keep last complete evidence with scope, observed-at, and last-refresh labels; disable dependent mutation. |
| Admin API unavailable | Global **Issue banner** with safe recovery; stale evidence may remain labelled. |
| SignalR disconnected/reconnected | Change freshness only and trigger bounded refetch; never announce service failure/recovery or operation completion. |
| Unauthenticated | Clear protected/cache/transient state, cancel polling and background work, retain only a safe route shell, and offer the configured authentication recovery. Interactive OIDC controls remain unavailable until implemented. |
| Session expired | Same clearing/cancellation posture; announce expiry once and return focus to a stable recovery action. |
| Access denied | Fail closed without confirming resource existence; return focus to the initiator or stable route heading. |
| Wrong scope | Clear results and autocomplete, reject the transition, and offer authorized scope selection without echoing the rejected identity. |
| Permission revoked during action | Stop before mutation or treat an already accepted operation as status-only; clear protected input and do not retry. |
| Authentication provider unavailable | Show a bounded unavailable state; do not expose provider endpoints, tokens, or claims and do not offer fake login. |
| Conflict | Non-submitting state that names the changed safe fact and requires refresh/review. |
| Accepted | Neutral persistent state with stable safe operation reference; no success language. |
| Evidence pending | Show expected evidence source and status refresh. No mutation retry. |
| Timeout/cancelled | Preserve accepted identity when available; outcome is unknown until authoritative status resolves. Cancellation never implies server rollback. |
| Terminal failure | Persistent reason class and safe recovery. Retry appears only when authoritative evidence marks it retryable. |
| Unknown | Missing/invalid/expired/mismatched evidence; name consequence and disable dependent mutation. |

### Surface coverage and mutation disposition

| Surface | Empty / stale / unavailable | Auth and scope | Mutation boundary |
|---|---|---|---|
| Overview | No visible activity; last-known values labelled; global API banner | Denied counts omitted | Read-only |
| Commands | No filter matches; status expiry stays distinct from invalid ID | Cross-tenant matches never disclosed | Status refresh distinct from retry/resubmit |
| Streams & Events | No visible evidence; protected outcome typed | Denied streams/types omitted | Read-only unless an approved support action exists |
| Projections | No visible projections; lag/currentness never inferred | Denied projection existence omitted | Rebuild/replay requires authoritative `Current`, scope freeze, and explicit availability |
| Tenants & Access | No visible tenants/users; stale grid labelled | Wrong-scope and denial disclose nothing | Role change requires projection and audit agreement |
| Topology | No visible resources; unavailable is not healthy | Internal endpoint/claim data omitted | Read-only triage |
| Storage & Snapshots | No evidence; open snapshot work marked unavailable | Storage metadata remains scope-bound | Only explicitly implemented current operations are enabled |
| Recovery | No dead letters/issues after authoritative query | Denied count, age, and tenant impact omitted | Retry/archive audited and evidence-confirmed |
| Deferred & Backlog | No visible items for role | Capability disclosure follows auth first | Never runnable while deferred |
| Settings | No editable settings; stale save disabled | Hidden settings reveal nothing | Implemented preference save only |
| Sample UI | Empty current projection; pending timeout remains stale/unknown | Current consumer auth rules | Submission is accepted-first; later read evidence confirms |
| Tenants UI | Empty visible tenant scope; read model stale/unavailable | Denied tenant/user existence omitted | Projection-confirmed access mutation only |

### Typed protected outcomes

| Outcome | Bounded operator copy | Rule |
|---|---|---|
| `deleted` | “Protected value was deleted.” | No reconstruction or stale cached value. |
| `missing` | “Protected value is not available.” | Do not imply deletion or denial. |
| `denied` | “Protected value cannot be shown for this scope.” | Do not confirm additional identity or content. |
| `unavailable` | “Protected value is temporarily unavailable.” | Offer safe status refresh only. |
| `malformed` | “Protected value could not be read. Reason: malformed.” | No raw bytes or parser detail. |
| `tampered` | “Protected value could not be verified. Reason: tampered.” | Treat as a security-relevant non-success. |
| `opaque` | “Protected value is intentionally opaque.” | Never decode in the client. |

FR37 is planned for post-MVP and remains unavailable until its implementation gate is satisfied. This vocabulary defines safe presentation where a typed contract exists; it does not claim delivery.

## Interaction Primitives

- Navigation: module entry → route-derived dashboard tab → grid/detail drill-in. The router owns state.
- Keyboard: all tabs, grids, filters, panels, dialogs, accordions, palette results, refresh controls, and actions are fully operable in logical order.
- Refresh: SignalR is a freshness nudge. Polling and manual status refresh fetch authority. Repeated notices are coalesced and bounded.
- Refresh preferences: pause/resume and approved cadence apply to automatic view refresh, not operation tracking; an accepted operation keeps a visible manual status path.
- Context preservation: background updates do not steal focus, scroll, selection, expansion, filters, or dialog state. If a trigger disappears, focus moves to the owning grid heading or page title.
- Destructive/recovery actions: exact scope, effect, risk, reversibility, role, audit, and evidence are confirmed; no viewport removes those gates.
- Disabled safety: a disabled control has persistent, programmatically associated reason text and a reachable safe next action; a tooltip alone is insufficient.
- Toasts: optional supplement only. They are deduplicated, pauseable, dismissible, focus-safe, and never the sole accepted, pending, failure, or success evidence.

## Accessibility Floor

Behavioral accessibility targets WCAG 2.2 AA; visual contrast is governed by `DESIGN.md`.

- Exactly one focusable page or selected-tab title; the host skip link reaches dashboard main content.
- Tablist/tab, grid/row/column, accordion header/panel, modal dialog, drawer, badge/status, filter, and action semantics expose accessible names, roles, values/states, relationships, validation, selection, expansion, busy state, and row context.
- Dialog focus enters the heading or first invalid field, stays modal, and returns to the initiating control or a documented stable fallback.
- One scoped view live region announces route, refresh, and freshness transitions politely. One operation live region announces accepted, pending, and confirmed states politely, and terminal failure, denial, or rejected destructive actions assertively.
- Regions announce transitions only, suppress initial and unchanged refresh chatter, coalesce bursts, and leave terminal outcomes visibly persistent.
- Reduced motion disables shimmer, nonessential transitions, auto-scroll, and animated state travel while retaining static progress and state text.
- Reflow succeeds at 320 CSS pixels, 400% browser zoom, and 200% text. Text-spacing overrides preserve content and controls; reading/focus order follows logical order.
- Two-dimensional scrolling is confined to a labelled grid region. Page-level horizontal scrolling, clipped dialogs, off-screen focus, and hover-only information are prohibited.
- Non-text focus, control boundary, selection, and lifecycle indicators remain perceivable in light, dark, system, and forced/high-contrast modes.
- Stable `data-testid` values support tests but are not accessibility evidence; tests also assert role, name, value/state, relationships, focus, and live messages.

## Responsive & Platform

| Width / condition | Required behavior |
|---|---|
| `>= 1280px` | Full host navigation, horizontal tabs, applicable full grid columns, dense filters, and side drawer where appropriate. |
| `960–1279px` | Compact navigation; keyboard-accessible scrolling tabs; identity, scope, state, and actions retained; secondary metadata moves to detail. |
| `< 960px` | Accessible collapsed navigation; tabs and evidence remain navigable; triage status and simple recovery actions remain visible; dialogs fill the viewport safely. |
| 320 CSS px / 400% zoom / 200% text | Single logical reading flow; no page-level two-dimensional scroll; labelled grid overflow only; critical context and focus remain visible. |

Per surface, keep tenant/environment, primary identity, canonical state, observation time, and safe next action before secondary metadata. Every narrow-screen mutation is either fully usable with all confirmation facts, disabled with an associated reason, or explicitly desktop-required with usable cancel/back. Viewport never changes authorization, evidence state, or feature availability.

The [dashboard mobile render](mockups/dashboard-overview-mobile.png) demonstrates a retained scope summary and grid-contained overflow. The [command mobile render](mockups/command-investigation-mobile.png) demonstrates the evidence panel moving below the grid while the unknown-outcome warning and status-refresh action remain visible.

## Localization and Formatting

- All visible, accessible-name, description, validation, status, dialog, disabled-reason, toast, and live-region copy comes from resource-backed complete strings.
- Format dates, observation times, durations, counts, and plurals with the active locale and an explicit operator-visible time zone where ambiguity matters.
- Preserve raw contractual identifiers while isolating them from surrounding translated grammar and bidirectional text.
- Test every configured locale, fallback locale, diagnostic pseudo-locale, RTL layout, long content, plural/duration forms, and safe truncation.
- Do not assemble translated sentences from fragments or put secret/protected values into format arguments.

## Support-Safe Operations

Sensitive material never enters the DOM, accessibility tree or properties, tooltips, URLs, browser history, clipboard, export, logs, telemetry, client exceptions, or transient caches. This includes bearer tokens, decoded JWTs, raw claims, raw EventStore metadata or payloads, protected bytes, stack traces, cursor and ETag contents, secrets, connection strings, provider endpoints/credentials, raw idempotency keys, idempotency digests, canonical-intent descriptors, and unbounded SignalR metadata.

Only allow-listed safe identifiers and reason codes required for investigation may render. Tenant authorization precedes existence disclosure. Only `/health`, `/alive`, and `/ready` are anonymously reachable platform health endpoints; that API rule does not create an anonymous Admin dashboard.

Unavailable capabilities are hidden when no useful read-only context exists. If tracking context is useful, show the **Deferred operation placeholder**. If an authenticated endpoint is retained, it returns the typed `501` outcome after authentication, authorization, and validation; it performs no mutation or audit admission.

## Source Traceability

| Requirement / decision | UX ownership | Story owner |
|---|---|---|
| FR4 / NFR8 — provenance and projection lifecycle | Evidence fields, freshness indicator, mutation gates | 7.5 typed transport; 7.19 presentation |
| FR34 / NFR15 — admin honesty and delivery semantics | Deferred, recovery, accepted/pending/terminal states | 7.4, 7.19 |
| FR37 / G5 — payload protection, post-MVP and unavailable | Typed protected outcomes and safe boundary | Future gated implementation; no delivery claim |
| FR36 — consumer parity closure | Readiness/authority note only; not lifecycle semantics | Consumer parity stories; deployed parity remains open |
| AD-17 — command-status authority | `MessageId` lookup, `CorrelationId` tracing, typed-client URL ownership | 7.5 typed transport; 7.19 presentation |
| AD-21 / UX-DR1–5, 23 | Host identities, single module, tabs, routes, palette | 7.14 |
| UX-DR24–30, 38 | Typed Admin outcomes, denial, validation, support safety | 7.5 and 7.19 |
| UX-DR10–21, 24–31, 38–41 | Operational components, evidence, mutations, critical journeys | 7.19 |
| UX-DR6–9, 32–37 | Theme inheritance, accessibility, localization, responsive behavior | 7.20 |
| UX-DR42 | Sample accepted submission and Tenants projection confirmation | Epic 2 consumer stories |

## Key Flows

### Flow 1 — Incident triage (Nora, platform operator, during a tenant outage)

1. Nora opens **Event Store Admin**; Overview shows stale health with observation and refresh times.
2. She opens Recovery and filters within her authorized tenant/domain scope.
3. A dead-letter row shows safe failure class, age, freshness, and protected outcome.
4. Nora opens the detail drawer; primary evidence is expanded and protected bytes are absent.
5. She chooses retry; the dialog freezes principal, environment, tenant, message, risk, and expected evidence.
6. The UI revalidates, submits once, and shows accepted then evidence-pending.
7. **Climax:** status refresh shows authoritative dead-letter disposition/count and matching audit evidence; only then is recovery confirmed.

Failure: timeout persists “Outcome unknown—do not resubmit”; only status refresh is available until authoritative retryability resolves.

### Flow 2 — Admin tenant access review (Marcel, administrator, onboarding support)

1. Marcel opens Tenants & Access and selects an authorized tenant.
2. The grid shows `ProjectionBacked`, `Current`, observation time, and freshness horizon state.
3. He starts a role change; the dialog freezes principal, tenant, user, role, pre-state, effect, and reversibility.
4. Submit revalidates every fact and shows accepted/evidence-pending.
5. **Climax:** the authoritative role projection and audit record agree; the row changes and success is announced.

Failure: scope or permission changes before submit produce a non-submitting conflict, clear protected input, and return focus safely.

### Flow 3 — Command investigation (Lea, platform operator, tracing a customer report)

1. Lea opens Commands and searches for a safe message or correlation identifier.
2. The lifecycle distinguishes stored from published events and names source/observation time.
3. She opens the detail drawer and follows the safe stream link.
4. Protected content is represented by a typed outcome; projection evidence is stale.
5. **Climax:** Lea reports that the event was committed but publication evidence is missing and routes it to Recovery without resubmitting.

Failure: malformed identifiers fail inline without calling the API or describing the value as a GUID.

### Flow 4 — Deferred operation discovery (Imani, administrator, looking for backup)

1. Imani searches the command palette; no runnable backup command appears.
2. She opens Deferred & Backlog and sees read-only tracking context.
3. `/backups` renders the same canonical unsupported view.
4. **Climax:** “Unavailable in this release.” makes the boundary explicit without a form, job, or progress state.

Failure: denial is evaluated before capability disclosure. Deferring backup does not automatically change the response to `501`.

### Flow 5 — Sample accepted submission (Alex, developer evaluating the sample)

1. Alex submits an increment through the Sample UI.
2. Validation succeeds and the typed client submits once.
3. The UI shows accepted/evidence-pending, not completion.
4. **Climax:** the visible counter changes only after authoritative read-model metadata changes.

Failure: timeout remains pending/stale with status refresh and no success or automatic resubmission.

### Flow 6 — Tenants projection-confirmed update (Priya, tenant administrator)

1. Priya opens the Tenants module within her authorized scope.
2. She confirms exact tenant, user, role, and permission context.
3. Submission shows accepted/evidence-pending.
4. **Climax:** the row changes only when current projection evidence and safe audit attribution agree.

Failure: `Unknown`, stale evidence, or denial disables mutation without confirming hidden tenants or users.

### Flow 7 — Projection rebuild oversight (Owen, platform operator, repairing lag)

1. Owen opens Projections and sees authoritative operational status separately from consumer lifecycle.
2. He inspects lag, provenance, observation time, last refresh, and the configured freshness evaluation.
3. If rebuild is implemented and authorized, the dialog freezes scope, pre-state, blast radius, reversibility, and expected evidence.
4. The last complete live model remains visible as rebuilding; partial output never becomes live.
5. **Climax:** lifecycle returns to authoritative `Current` with a new observation and version before dependent mutations re-enable.

Failure: missing lifecycle transport renders `Unknown`; no rebuild action or inferred currentness appears.

### Flow 8 — Topology diagnosis (Samira, on-call operator, investigating a sidecar issue)

1. Samira opens Topology; service and DAPR evidence is read-only and scope-bound.
2. A service is unavailable, not empty or healthy; the issue banner names consequence and last refresh.
3. She pauses automatic refresh while reading and opens a service detail.
4. **Climax:** manual refresh returns safe current evidence while her focus, expansion, and scroll remain stable.

Failure: authentication-provider or Admin API failure reveals no endpoints, claims, tokens, or raw configuration.

### Flow 9 — Snapshot evidence review (Gabriel, capacity operator, checking a hot stream)

1. Gabriel opens Storage & Snapshots and filters to authorized storage evidence.
2. The grid retains primary identity, state, and observation time; secondary metadata moves to detail on narrow width.
3. Open snapshot-runtime work appears unavailable rather than runnable.
4. **Climax:** Gabriel identifies the latest implemented snapshot evidence without mistaking open projection-cost work for delivered capability.

Failure: missing evidence is `Unknown`; no local age or ETag calculation fabricates freshness.

### Flow 10 — Safe preference change (Asha, administrator, reducing refresh frequency)

1. Asha opens Settings and chooses an approved automatic-refresh cadence.
2. The setting explains that operation status remains manually refreshable and mutations are never retried.
3. Scope is revalidated on save.
4. **Climax:** the preference persists, the page announces it once, and the current evidence view remains unchanged.

Failure: stale or revoked scope disables save with an associated reason and safe route recovery.

### Flow 11 — Bookmarked deep-link arrival (Mateo, support engineer, opening a shared command link)

1. Mateo opens an authorized `/commands` bookmark.
2. The router selects `event-store-admin`, activates Commands, restores safe filters, and focuses the page title.
3. He opens the referenced row without a legacy duplicate page.
4. **Climax:** browser Back returns to the same Commands filters, selection context, and module state.

Failure: malformed, denied, or cross-tenant route state fails closed and does not disclose whether the command exists.
