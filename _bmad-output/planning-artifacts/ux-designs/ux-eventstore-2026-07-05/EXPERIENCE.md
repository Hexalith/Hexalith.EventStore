---
name: Hexalith.EventStore Admin
status: draft
created: 2026-07-05
updated: 2026-09-09
reviewed_repository_revision: 0994c37814c37dac7667a209dbd0659125aac49e
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

Authority flows one way: the PRD defines product intent and current readiness state; architecture defines system decisions; epics define implementation slices and acceptance responsibility; and the brownfield architecture records observed legacy scope. The bound PRD validation report is snapshot evidence only that its captured baseline received a `Reject` verdict. Its detailed findings are stale at the individual-finding level and do not override the current PRD, architecture, or epics. This update did not rerun that validation. The official Fluent V5 documentation defines upstream component behavior. `ux.md`, this folder's `index.md`, mocks, review files, and this update's [source-safety reconciliation](reconcile-source-safety-update-2026-09-09.md) are downstream handoffs or evidence, not upstream authority.

| Reviewed source | Input SHA-256 captured on 2026-09-09 before downstream digest repinning |
|---|---|
| `docs/brownfield/architecture.md` | `3cddf6eb593fb28d90b8dd9d54562cb28bb4ea2a4f5f15c6a51b77f06bf5a0a3` |
| `_bmad-output/planning-artifacts/prd.md` | `b99effdb414209da373433d9b4d2075072ca950e89534b22b81155236f650662` |
| `_bmad-output/planning-artifacts/architecture.md` | `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51` |
| `_bmad-output/planning-artifacts/epics.md` | `d067c8fbffce47d7d0518396265f862093ec1a513cab73cb9fea1e88185cf33b` |
| `_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md` | `17f378d0686d9840938cf835ea9227a2163c39ba210597b2ada40f57aac801d3` |

The reviewed revision identifies the repository base; the input digests also capture newer in-workspace source changes. The epics digest changed only because its input-document digest metadata was repinned; no epic, story, requirement, or acceptance body changed. Any later digest or revision change reopens source reconciliation. Current PRD readiness is `blocked` / `reject`, and architecture is under a draft reconciliation; historical July and August readiness verdicts do not override either state.

### Finalization decisions

- The live `/types` catalog, including `events`, `commands`, and `aggregates` inner tabs, stays under Streams & Events. Story 7.14's current machine-validated route-manifest acceptance list omits `/types`; that source gap must be corrected without creating a second route implementation.
- The UX does not invent a numeric freshness horizon. Story 7.5 must bind the authoritative horizon and clock basis from the typed contract or configuration before dependent mutations can become available.
- Recovery mutation affordances exist only when the specific capability is delivered and the active environment has passed its required readiness, topology, authorization, audit, and evidence gates. Otherwise they are hidden or presented as read-only unavailable context; route or DTO presence is not delivery evidence.

## Information Architecture

The host exposes one `event-store-admin` module. Its dashboard owns ten ordered tabs and all detail routes.

| Dashboard tab | Canonical child/deep-link routes | Purpose | Visual coverage |
|---|---|---|---|
| Overview | `/` | Authorized health summary, activity, freshness, and recent evidence | Mocked: `mockups/dashboard-overview.html` |
| Commands | `/commands` | Command lifecycle, accepted versus terminal evidence, and investigation | Mocked: `mockups/command-investigation.html` |
| Streams & Events | `/streams`, `/streams/{tenant}/{domain}/{aggregate}`, `/events`, `/types` | Stream/event evidence, protected outcomes, and type catalog; `/types` retains its UX placement despite the Story 7.14 manifest gap | Spine-only |
| Projections | `/projections` | Operational status, authoritative lifecycle, lag, freshness, and rebuild evidence | Spine-only |
| Tenants & Access | `/tenants` | Tenant-visible access state and role mutation; tenant provisioning is not assumed to exist | Spine-only |
| Topology | `/dapr`, `/dapr/actors`, `/dapr/pubsub`, `/dapr/resiliency`, `/dapr/health-history`, `/services` | DAPR and service operations plus safe activated route/idempotency catalog generation and readiness; not the legacy tenant/domain navigator | Spine-only |
| Storage & Snapshots | `/storage`, `/snapshots` | Storage and implemented snapshot evidence without implying open runtime work is complete | Spine-only |
| Recovery | `/health`, `/health/dead-letters`, `/consistency` | Health, dead letters, poison handling, consistency, and conditionally available safe recovery | Spine-only |
| Deferred & Backlog | `/backups`, `/compaction` | Honest read-only unavailable-capability disposition | Spine-only |
| Settings | `/settings` | Implemented environment-safe preferences and role-visible settings | Spine-only |

The external Sample and Tenants UIs remain separate module dashboards. Sample demonstrates accepted submission without false completion; Tenants demonstrates projection-confirmed outcomes.

Restore and import have no canonical routes or useful read-only surfaces in this information architecture and remain hidden pending confirmation. Tenants & Access exposes authorized tenant visibility and access-role changes only; it does not invent a tenant-provisioning route or control. These are draft assumptions recorded under **Open Questions**, not confirmed product decisions.

### Canonical routing contract

The router is the source of truth. On arrival, it selects the owning module, tab, inner view, and bounded filters. Activating a tab navigates to that tab's canonical root URL. Back, forward, reload, and bookmarks replay router state; tabs never maintain a competing navigation state.

| Route | Owning tab/view | Allowed route state | Failure behavior |
|---|---|---|---|
| `/` | Overview | Authorized environment/tenant context | Unavailable shell shows bounded recovery; no false selected child view. |
| `/commands` | Commands | Allow-listed safe filters and opaque paging | Malformed filter is rejected inline; denied scope reveals no result existence. |
| `/streams` | Streams & Events / streams | Safe tenant/domain filters and opaque paging | Cross-tenant or malformed scope fails closed. |
| `/streams/{tenant}/{domain}/{aggregate}` | Streams & Events / stream detail | Typed, encoded path identities; tenant is explicit canonical lowercase, 1–64 characters; no raw payload in URL | Invalid, reserved `system`, wildcard, or conflicting tenant input fails closed before routing; denial does not confirm the stream. |
| `/events` | Streams & Events / events | Allow-listed safe filters and opaque paging | Invalid filter performs no query. |
| `/types` | Streams & Events / Type Catalog | `tab=events`, `tab=commands`, or `tab=aggregates`; omitted means the catalog default | Unknown tab falls back to the catalog default with a bounded notice. Story 7.14 must add this route to its manifest. |
| `/projections` | Projections | Safe status/filter state; opaque paging | Missing authoritative provenance renders `Unknown`. |
| `/tenants` | Tenants & Access | Exactly one explicit canonical lowercase tenant filter where tenant scope is required; role-change controls only | Missing, duplicate, conflicting, invalid, reserved `system`, or wildcard tenant input fails before lookup; denial reveals no tenant existence. |
| `/dapr` | Topology / summary | Safe activated route/idempotency catalog generation and readiness may render; no raw catalog bytes, configuration, digest, key, fence, token, or secret state | Unavailable, partial, mismatched, unknown, corrupt, or ambiguous activation is not empty, healthy, or ready. |
| `/dapr/actors` | Topology / actors | Allow-listed safe filters | Denied actor data is omitted without counts. |
| `/dapr/pubsub` | Topology / pub/sub | Allow-listed safe filters | Broker internals and credentials never render. |
| `/dapr/resiliency` | Topology / resiliency | Allow-listed safe filters | Raw configuration never renders. |
| `/dapr/health-history` | Topology / health history | Bounded time/filter state | Deferred history uses the canonical unavailable state. |
| `/services` | Topology / services | Allow-listed safe filters | Internal endpoints and claims never render. |
| `/storage` | Storage & Snapshots / storage | Safe filters and opaque paging | Unknown cost/storage evidence is labelled, never inferred. |
| `/snapshots` | Storage & Snapshots / snapshots | Implemented read evidence only | Open snapshot work is unavailable, not simulated. |
| `/health` | Recovery / health | Authorized safe health scope | API health semantics remain distinct from authenticated UI state. |
| `/health/dead-letters` | Recovery / dead letters | Safe canonical tenant/domain filters and opaque paging; mutations require delivered and environment-ready recovery evidence | Denied scope reveals no count or age; absent readiness keeps actions hidden or read-only unavailable. |
| `/consistency` | Recovery / consistency | Implemented read checks only | Unsupported mutation remains absent. |
| `/backups` | Deferred & Backlog / backup | Read-only tracking context | Exact unavailable state; no form, job, progress, or accepted result. |
| `/compaction` | Deferred & Backlog / compaction | Read-only tracking context | Exact unavailable state; no form, job, progress, or accepted result. |
| `/settings` | Settings | Implemented preferences only | Stale or revoked scope disables save with an associated reason. |

AD-27 binds every tenant-bearing route, filter, dialog, and request to exactly one explicit tenant normalized to lowercase using 1–64 characters and `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`. Missing, duplicate, conflicting, invalid, reserved `system`, and wildcard-inferred tenant values fail before routing, state access, autocomplete, or existence disclosure. Internal cataloged platform scope is not a selectable managed tenant.

Story 7.14 must eventually supply the machine-validated route manifest, exact parameter policy, redirects, and single-owner implementation. Its current acceptance list omits the live `/types` route, so the manifest is not yet complete. The current tenant/domain navigator becomes a Streams & Events filter/drill-in; **Topology** is reserved for DAPR and service operations.

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
| “This idempotency key has expired. Do not retry with it.” | Echoing the key, digest, or fence value |
| “Runtime routing evidence cannot be verified. No action was taken.” | “Retry” for unknown, corrupt, or ambiguous catalog/fence evidence |

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
| Per-route projection outcome | For every configured route, show route-safe identity, checkpoint, and exactly one of `advanced`, `not advanced`, `retry`, or `failure`. Partial fan-out is never aggregate success. |
| Activated catalog readiness | Show only a safe activated route/idempotency catalog generation and readiness class. Raw configuration, retained bytes, digests, signatures, keys, fences, and secret references do not render. |
| Terminal command evidence | Names authoritative terminal state and source. `Completed` remains distinct from a later projection-confirmed read-model outcome when the operation requires both. |
| Stable operation/audit identity | Enables status refresh and audit agreement without resubmission; must be support-safe. |

`MessageId` is the command-status lookup key. `CorrelationId` is the bounded trace key shown only when authorized and support-safe. Applicable mutation views also carry a bounded request ID. The UI calls the typed Admin client and never constructs a status URL from any identifier.

FR4/NFR8 own projection provenance and lifecycle. FR36 owns consumer parity closure and does not define lifecycle. ETags, cursors, cache hits, response age, SignalR, elapsed time, and locally computed data are opaque or advisory and never prove currentness, version, or completion. `LocalOnly` never confirms success.

### Mutation progression

1. Verify that the mutation is delivered for the exact route and has passed the active environment's required readiness, topology, authorization, audit, and evidence gates. If not, expose no runnable control: hide it or show read-only “Unavailable in this release.” context.
2. Validate bounded input and current authorized scope. Invalid, oversized, duplicated, conflicting, reserved-tenant, or over-limit input performs no call and is not echoed.
3. Open the **Operation dialog** and freeze its displayed context.
4. Revalidate the authenticated human subject, service principal and bounded delegation when applicable, role, environment, canonical tenant, target, authoritative pre-state, freshness, reason, credential issuer/expiry, request/correlation/message IDs, effect, risk, blast radius, reversibility, and expected evidence on submit.
5. Persist or verify the fail-closed audit `prepare` intent before effect. If audit intent or current authority is unavailable, ambiguous, or rejected, perform no mutation.
6. Any mismatch, expiry, revocation, conflict, unknown/corrupt/ambiguous catalog or fence evidence, or expired idempotency outcome stops submission, clears protected transient input, cancels background retry, and returns focus to the changed fact or stable initiator. Raw keys, digests, fences, credentials, and claims never render.
7. Submit once through the typed client as the `effect` phase; show `Accepted`, then `EvidencePending`.
8. Bind the authoritative operation result and safe audit disposition in `commit`. If the effect may have occurred but commit evidence is incomplete, enter `recovery` with status-only refresh; never repeat the effect blindly.
9. **Refresh status** queries the same stable operation identity. It never replays the mutation.
10. Show route-specific success only when all authoritative terminal, projection-route, and audit evidence required by that route agrees.
11. A timeout or ambiguous transport result produces the persistent message “Outcome unknown—do not resubmit. Refresh status.”
12. **Retry mutation** appears only after authoritative, retryable terminal non-success and a renewed readiness/scope/audit check; expired idempotency and unknown/corrupt/ambiguous catalog or fence outcomes never offer blind retry.

### Projection fan-out and erasure claims

- Projection dispatch exposes each configured route and its own checkpoint plus `advanced`, `not advanced`, `retry`, or `failure` result. A summary may say “partial” or “not confirmed,” but cannot say success while any required route is not proven advanced.
- Projection read-model/checkpoint removal reports only that bounded projection removal. It does not mean source events, broker history, payload keys, backups, restore points, caches, exports, replicas, legal holds, or audit records were erased.
- A future full/GDPR erasure view reports logical, projection, cryptographic, broker, backup, restore-point, cache, export, replica, and legal-hold facets separately. It cannot show overall completion while any required facet is pending, unknown, or failed.

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
| Evidence grid | **Evidence grid** uses `FluentDataGrid`; exposes accessible row/column context, sort state, busy state, selection, pagination, and one discoverable row-action location. Projection fan-out presents every configured route's safe identity, checkpoint, and `advanced`, `not advanced`, `retry`, or `failure` outcome; partial fan-out never receives aggregate success. Updates preserve focus, scroll, selection, and expanded detail. |
| Status badge | **Status badge** uses `FcStatusBadge`; accessible name/value includes canonical state text and never depends on color. |
| Issue banner | **Issue banner** names affected visible scope, consequence, and a reachable safe action. It is persistent while the condition holds and never exposes raw internals. |
| Operation dialog | **Operation dialog** is modal, labelled, described, cancellable, and focus-contained. It freezes and displays bounded authenticated human subject, service principal/delegation where applicable, reason, credential issuer/expiry, environment, canonical tenant, target, authoritative pre-state, applicable request/correlation/message IDs, effect, blast radius, reversibility, expected evidence, and current `prepare`/`effect`/`commit`/`recovery` phase, then revalidates all of them on submit. Audit uncertainty fails closed; protected attribution material remains absent. |
| Detail panel | **Detail panel** is an EventStore-owned labelled `aside` composed with `FluentCard`; it keeps the source row selected, moves below the grid when space is constrained, and returns focus to that row or a stable grid fallback when closed or removed. |
| Multi-section panel | **Multi-section panel** uses one `FluentAccordion` for two or more titled siblings; headers expose expanded state, primary evidence opens by default, and the only primary grid is never hidden in it. |
| Command lifecycle tracker | **Command lifecycle tracker** exposes an ordered list and current step for `Received`, `Processing`, `EventsStored`, `EventsPublished`, `Completed`, `Rejected`, `PublishFailed`, and `TimedOut`; it names source and observation time. |
| Projection freshness indicator | **Projection freshness indicator** renders `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, or `Unknown` only from the evidence contract below. Its text, icon, and Fluent V5 color mapping match the shipped Tenants lifecycle contract. |
| Projection connection status | **Projection connection status** uses `FcProjectionConnectionStatus` only for connection and reconciliation notices. A disconnected, discarded, over-limit, or overflow notification triggers bounded refetch; when refetch cannot establish authority, the affected view becomes stale or unavailable. Notification metadata never supplies provenance, lifecycle, route advancement, or completion evidence. |
| Loading skeleton | **Loading skeleton** matches the eventual layout, exposes one busy state on the owning region, and suppresses nonessential shimmer under reduced motion. |
| Empty state | **Empty state** appears only after an authoritative successful query for visible scope and has a safe next action. Loading, denied, unavailable, and stale are not empty. |
| Deferred operation placeholder | **Deferred operation placeholder** is hidden or read-only with tracking context and exact copy “Unavailable in this release.” It exposes no form, submit, accepted, retry, job, or progress behavior. |
| Command palette | **Command palette** uses `FcCommandPalette`; focus enters search, results announce changes without flooding, Escape closes, focus returns to the trigger, and entries obey current route, tenant, role, and deferred policy. |
| Refresh controls | **Refresh controls** separate manual status refresh, automatic-refresh pause/resume, and an approved cadence selector. Refresh never submits or retries a mutation and preserves view context. |
| Live status regions | **Live status regions** consist of one scoped view region and one operation region. They announce transitions only, coalesce repeats, and keep terminal outcomes visible outside transient toasts. |
| Protected outcome | **Protected outcome** maps a typed unreadable, expired-idempotency, or unsafe catalog/fence result to bounded localized copy and an authorized safe reason class while protected bytes, raw keys, digests, fences, and catalog internals remain absent from every client channel. It never offers blind retry. |

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
| SignalR metadata discarded / over limit / overflow | Discard metadata without rendering or logging it, coalesce the signal, and perform a bounded authoritative refetch. If refetch cannot establish state, show stale or unavailable; the signal itself has no authority. |
| Unauthenticated | Clear protected/cache/transient state, cancel polling and background work, retain only a safe route shell, and offer the configured authentication recovery. Interactive OIDC controls remain unavailable until implemented. |
| Session expired | Same clearing/cancellation posture; announce expiry once and return focus to a stable recovery action. |
| Access denied | Fail closed without confirming resource existence; return focus to the initiator or stable route heading. |
| Wrong scope | Clear results and autocomplete, reject the transition, and offer authorized scope selection without echoing the rejected identity. |
| Permission revoked during action | Stop before mutation or treat an already accepted operation as status-only; clear protected input and do not retry. |
| Authentication provider unavailable | Show a bounded unavailable state; do not expose provider endpoints, tokens, or claims and do not offer fake login. |
| Conflict | Non-submitting state that names the changed safe fact and requires refresh/review. |
| Audit prepare unavailable or ambiguous | Fail closed before effect, show a bounded non-success, and offer only a safe refresh/escalation path. Audit failure never silently permits mutation. |
| Accepted | Neutral persistent state with stable safe operation reference; no success language. |
| Evidence pending | Show expected evidence source and status refresh. No mutation retry. |
| Timeout/cancelled | Preserve accepted identity when available; outcome is unknown until authoritative status resolves. Cancellation never implies server rollback. |
| Terminal failure | Persistent reason class and safe recovery. Retry appears only when authoritative evidence marks it retryable. |
| Idempotency key expired | Show bounded `idempotency_key_expired` guidance without the key, digest, verification tag, fence, or prior intent/result. No downstream call or blind retry occurs. |
| Catalog/fence evidence unknown, corrupt, or ambiguous | Fail closed before action, expose only a bounded reason class and readiness consequence, and never reveal raw catalog bytes, digest, signature, key, or fence value. No blind retry. |
| Projection fan-out partial | Keep per-route results visible and label the aggregate `Partial` or `Not confirmed`; never announce success while any required route is not proven advanced. |
| Erasure facets incomplete | Keep every required facet and its source visible. Pending, unknown, or failed projection, logical, cryptographic, broker, backup, restore-point, cache, export, replica, or legal-hold evidence blocks any full/GDPR erasure completion claim. |
| Unknown | Missing/invalid/expired/mismatched evidence; name consequence and disable dependent mutation. |

### Surface coverage and mutation disposition

| Surface | Empty / stale / unavailable | Auth and scope | Mutation boundary |
|---|---|---|---|
| Overview | No visible activity; last-known values labelled; global API banner | Denied counts omitted | Read-only |
| Commands | No filter matches; status expiry stays distinct from invalid ID | Cross-tenant matches never disclosed | Status refresh distinct from retry/resubmit |
| Streams & Events | No visible evidence; protected outcome typed; restore/import remain hidden under the draft assumption | Denied streams/types omitted | Read-only unless an approved support action exists; projection removal never claims broader erasure |
| Projections | No visible projections; lag/currentness never inferred; per-route fan-out remains explicit | Denied projection existence omitted | Rebuild/replay requires authoritative `Current`, delivered/environment-ready capability evidence, scope freeze, and explicit availability |
| Tenants & Access | No visible tenants/users; stale grid labelled; no provisioning control under the draft assumption | Wrong-scope, invalid/reserved tenant, and denial disclose nothing | Role change requires canonical tenant, projection, and fail-closed audit agreement |
| Topology | No visible resources; unavailable, partial, or catalog mismatch is not healthy/ready | Internal endpoint/claim/configuration/digest/key/fence data omitted | Read-only safe activated catalog generation/readiness and topology triage |
| Storage & Snapshots | No evidence; open snapshot work marked unavailable | Storage metadata remains scope-bound | Only explicitly implemented current operations are enabled |
| Recovery | No dead letters/issues after authoritative query; missing delivery/readiness evidence is unavailable, not empty | Denied count, age, and tenant impact omitted | Retry/archive render only when delivered and the active environment passes production-readiness, authorization, catalog, capture, audit, and evidence gates; otherwise hidden or read-only unavailable |
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

Admission and runtime-safety failures use the same bounded presentation discipline:

| Outcome | Bounded operator copy | Rule |
|---|---|---|
| `idempotency_key_expired` | “This idempotency key has expired. Do not retry with it.” | Reject before downstream work. Do not show the key, digest, verification tag, fence, prior intent, or prior result. |
| Catalog or fence `unknown` / `corrupt` / `ambiguous` | “Runtime routing evidence cannot be verified. No action was taken.” | Fail closed, show only a safe reason class, and offer no blind retry or raw diagnostic material. |

FR37 is committed post-MVP. Its prerequisite security specification is approved-authorized, but that approval does not deliver the engine/package, a production backend, dual-provider parity, rollback proof, or G5. Stories 8.2–8.11 and every successor gate must close before the capability can be presented as available. This vocabulary defines safe presentation where a typed contract exists; it does not claim delivery.

## Interaction Primitives

- Navigation: module entry → route-derived dashboard tab → grid/detail drill-in. The router owns state. Every tenant-bearing route/input uses one explicit AD-27-canonical tenant; no default, duplicate, reserved `system`, or wildcard inference is accepted.
- Keyboard: all tabs, grids, filters, panels, dialogs, accordions, palette results, refresh controls, and actions are fully operable in logical order.
- Refresh: SignalR is a freshness nudge. Polling and manual status refresh fetch authority. Repeated notices are coalesced and bounded. Discarded, over-limit, or overflow metadata triggers a bounded refetch; failed refetch yields stale/unavailable rather than inferred authority.
- Refresh preferences: pause/resume and approved cadence apply to automatic view refresh, not operation tracking; an accepted operation keeps a visible manual status path.
- Context preservation: background updates do not steal focus, scroll, selection, expansion, filters, or dialog state. If a trigger disappears, focus moves to the owning grid heading or page title.
- Destructive/recovery actions: exact scope, effect, risk, reversibility, human subject or bounded delegation/service principal, reason, issuer/expiry, request/correlation/message identities, audit phase, and evidence are confirmed; no viewport removes those gates. Recovery actions remain absent or read-only until delivery and environment-readiness gates are proven.
- Retry: refresh and recovery are identity-preserving status operations. Expired idempotency or unknown/corrupt/ambiguous catalog/fence evidence never triggers automatic or blind mutation retry.
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

Sensitive material never enters the DOM, accessibility tree or properties, tooltips, URLs, browser history, clipboard, export, logs, telemetry, client exceptions, or transient caches. This includes bearer tokens, decoded JWTs, raw claims, raw EventStore metadata or payloads, protected bytes, stack traces, cursor and ETag contents, secrets, connection strings, provider endpoints/credentials, raw idempotency keys, idempotency digests, verification tags, canonical-intent descriptors, fence values, retained route-catalog bytes/digests/signatures, and discarded, over-limit, or overflow SignalR metadata.

Only allow-listed, bounded identifiers and reason classes required for investigation may render. Tenant authorization and AD-27 canonicalization precede existence disclosure. Invalid or oversized input fails before transport and is not echoed. Only `/health`, `/alive`, and `/ready` are anonymously reachable platform health endpoints; that API rule does not create an anonymous Admin dashboard.

Topology may show the active safe route/idempotency catalog generation and a bounded readiness state so operators can distinguish ready, partial, mismatched, or unavailable activation. It never renders raw configuration, catalog contents, secrets, keys, digests, signatures, fences, internal endpoints, or credential references.

Projection removal is reported only as projection read-model/checkpoint removal. A full/GDPR erasure claim remains unavailable while any required logical, projection, cryptographic, broker, backup, restore-point, cache, export, replica, or legal-hold facet is pending, unknown, or failed.

Unavailable capabilities are hidden when no useful read-only context exists. If tracking context is useful, show the **Deferred operation placeholder**. If an authenticated endpoint is retained, it returns the typed `501` outcome after authentication, authorization, and validation; it performs no mutation or audit admission. Recovery is governed by the same rule until its route-specific delivery and environment-readiness gates are proven. Under the current draft assumptions, restore/import remain hidden and Tenants & Access exposes no tenant-provisioning affordance.

## Source Traceability

| Requirement / decision | UX contract coverage | Source implementation / gate reference |
|---|---|---|
| FR4 / NFR8 — provenance and projection lifecycle | Evidence fields, freshness indicator, mutation gates | 7.5 typed transport; 7.19 presentation |
| FR34 / NFR15 — admin honesty and delivery semantics | Deferred, conditionally available recovery, accepted/pending/terminal states | 7.1, 7.3–7.5, 7.19; AD-29 and AD-31 gates |
| FR37 / NFR19 / G5 — payload protection | Typed protected outcomes and unavailable boundary | Committed post-MVP; prerequisite spec approval is not engine/package/backend/parity/rollback/G5 delivery; Stories 8.2–8.11 remain gated |
| FR36 — consumer parity closure | Readiness/authority note only; not lifecycle semantics | Consumer parity stories; deployed parity remains open |
| AD-17 — command-status authority | `MessageId` lookup, `CorrelationId` tracing, typed-client URL ownership | 7.5 typed transport; 7.19 presentation |
| AD-19 — one-to-many projection dispatch | Per-route checkpoint and advancement outcome; no aggregate success for partial fan-out | Projection transport/evidence source plus 7.19 presentation |
| AD-21 / UX-DR1–5, 23 | Host identities, single module, tabs, routes, palette | 7.14 |
| AD-25 / AD-33 — admission and catalog safety | Expired-idempotency and unsafe catalog/fence fail-closed states; safe active generation/readiness | Durable admission/catalog sources plus 7.5/7.19 presentation |
| AD-27 — canonical tenant boundary | Explicit lowercase 1–64 tenant grammar, reserved `system` rejection, no wildcard inference | Shared boundary contract and all tenant-bearing adapters |
| AD-29 / AD-31 — audit and recovery readiness | Bounded attribution, phase state, audit fail-closed, recovery action gating | 7.3 and Operations production-wiring evidence before 7.19 actionability |
| AD-7 / AD-30 — projection removal and full erasure | Separate projection-removal result and per-facet erasure status; no premature completion | 1.14 projection removal; post-MVP legal-policy and erasure workflow gates |
| UX-DR24–30, 38 | Typed Admin outcomes, denial, validation, support safety | 7.5 and 7.19 |
| UX-DR10–21, 24–31, 38–41 | Operational components, evidence, mutations, critical journeys | 7.19 |
| UX-DR6–9, 32–37 | Theme inheritance, accessibility, localization, responsive behavior | 7.20 |
| UX-DR42 | Sample accepted submission and Tenants projection confirmation | Epic 2 consumer stories |

## Open Questions

- [ASSUMPTION] Restore and import remain hidden because no canonical route or useful read-only surface is currently defined. Backup and compaction remain the only Deferred & Backlog routes. Confirm before finalization.
- [ASSUMPTION] Tenant provisioning is absent from the current information architecture. Tenants & Access covers authorized tenant visibility and access-role changes only. Confirm before finalization.

## Key Flows

### Flow 1 — Conditional incident triage and recovery (Nora, platform operator, during a tenant outage)

1. Nora opens **Event Store Admin**; Overview shows stale health with observation and refresh times.
2. She opens Recovery and filters with one explicit AD-27-canonical tenant and her authorized domain scope.
3. A dead-letter row shows safe failure class, age, freshness, and protected outcome.
4. Nora opens the detail drawer; primary evidence is expanded and protected bytes are absent.
5. The UI checks the route-specific delivery, Operations wiring, environment readiness, catalog, capture-before-ack, authorization, and fail-closed audit gates. If any gate is absent or unproven, retry/archive is hidden or read-only with “Unavailable in this release.” and Nora escalates without mutation.
6. Only when every gate is proven does Nora choose retry. The dialog freezes the bounded human subject or delegation/service principal, reason, issuer/expiry, environment, canonical tenant, message, request/correlation identity, risk, reversibility, expected evidence, and `prepare` phase.
7. Audit `prepare` succeeds before the single `effect`; the UI then shows accepted/evidence-pending while `commit` or `recovery` evidence resolves.
8. **Climax:** status refresh shows authoritative dead-letter disposition/count, route outcome, and matching audit evidence; only then is recovery confirmed. If the capability is unavailable, the climax is the equally explicit non-actionable boundary rather than a fabricated recovery.

Failure: audit uncertainty prevents effect. A timeout or ambiguous effect persists “Outcome unknown—do not resubmit”; only status refresh/recovery is available until authoritative retryability resolves. Expired idempotency or unknown/corrupt/ambiguous catalog/fence evidence exposes no raw material and offers no blind retry.

### Flow 2 — Admin tenant access review (Marcel, administrator, onboarding support)

1. Marcel opens Tenants & Access and selects exactly one authorized tenant whose explicit input canonicalizes to AD-27 lowercase syntax; no tenant-provisioning control is assumed.
2. The grid shows `ProjectionBacked`, `Current`, observation time, and freshness horizon state.
3. He starts a role change; the dialog freezes human subject or bounded delegation/service principal, reason, issuer/expiry, tenant, user, role, pre-state, request/correlation/message IDs, effect, reversibility, evidence, and audit phase.
4. Submit revalidates every fact, completes fail-closed audit `prepare`, performs one effect, and shows accepted/evidence-pending until commit evidence agrees.
5. **Climax:** the authoritative role projection and audit record agree; the row changes and success is announced.

Failure: scope or permission changes before submit produce a non-submitting conflict, clear protected input, and return focus safely. Missing, duplicate, invalid, reserved `system`, or inferred-wildcard tenant input performs no lookup or mutation.

### Flow 3 — Command investigation (Lea, platform operator, tracing a customer report)

1. Lea opens Commands and searches for a safe message or correlation identifier.
2. The lifecycle distinguishes stored from published events and names source/observation time.
3. She opens the detail drawer and follows the safe stream link.
4. Protected content is represented by a typed outcome; projection evidence is stale.
5. **Climax:** Lea reports that the event was committed but publication evidence is missing and routes it to Recovery without resubmitting.

Failure: malformed or over-limit identifiers fail inline without calling the API, echoing the input, or describing the value as a GUID. Expired idempotency and unsafe catalog/fence evidence remain bounded non-retryable states.

### Flow 4 — Deferred operation discovery (Imani, administrator, looking for backup)

1. Imani searches the command palette; no runnable backup command appears.
2. She opens Deferred & Backlog and sees read-only tracking context.
3. `/backups` renders the same canonical unsupported view.
4. Restore and import remain absent under the current draft assumption because neither has a canonical route or useful read-only surface.
5. **Climax:** “Unavailable in this release.” makes the backup boundary explicit without a form, job, or progress state.

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
2. He inspects lag, provenance, observation time, last refresh, configured freshness evaluation, and every configured route's checkpoint plus `advanced`, `not advanced`, `retry`, or `failure` outcome.
3. If rebuild is delivered, environment-ready, and authorized, the dialog freezes bounded attribution, reason, issuer/expiry, scope, IDs, pre-state, blast radius, reversibility, expected evidence, and audit phase.
4. The last complete live model remains visible as rebuilding; partial output never becomes live, and partial fan-out is labelled `Partial` or `Not confirmed`, never aggregate success.
5. **Climax:** lifecycle returns to authoritative `Current` with a new observation and version, every required route is proven advanced, and audit commit agrees before dependent mutations re-enable.

Failure: missing lifecycle transport or any unknown/corrupt/ambiguous catalog/fence state renders `Unknown`; no rebuild action, blind retry, or inferred currentness appears. Projection read-model/checkpoint removal, if separately shown, never claims full/GDPR erasure.

### Flow 8 — Topology diagnosis (Samira, on-call operator, investigating a sidecar issue)

1. Samira opens Topology; service and DAPR evidence is read-only and scope-bound, and the safe active route/idempotency catalog generation/readiness is visible without raw configuration or secrets.
2. A service or catalog activation is unavailable, partial, mismatched, or unverified—not empty, healthy, or ready; the issue banner names consequence and last refresh.
3. She pauses automatic refresh while reading and opens a service detail.
4. **Climax:** manual refresh returns safe current evidence while her focus, expansion, and scroll remain stable.

Failure: authentication-provider, Admin API, or catalog-validation failure reveals no endpoints, claims, tokens, raw configuration, retained catalog bytes, digests, keys, signatures, or fences.

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

Failure: malformed, over-limit, denied, reserved `system`, wildcard-inferred, or cross-tenant route state fails closed and does not disclose whether the command exists or echo rejected route input.
