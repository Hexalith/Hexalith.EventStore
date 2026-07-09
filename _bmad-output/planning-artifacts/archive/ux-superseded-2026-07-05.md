# Hexalith.EventStore UX Handoff

Status: final
Updated: 2026-07-05

This is the canonical Phase 4 UX artifact for Hexalith.EventStore. The detailed peer contracts live in:

- [DESIGN.md](ux-designs/ux-eventstore-2026-07-05/DESIGN.md) - visual identity, tokens, component visual rules.
- [EXPERIENCE.md](ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md) - information architecture, behavior, states, accessibility, localization, journeys.

`DESIGN.md` and `EXPERIENCE.md` win on conflict with mockups, screenshots, or legacy `Admin.UI` behavior.

## UX Governance

All UI-affecting EventStore work uses FrontComposer UX and Blazor Fluent UI V5. Implementations must prefer FrontComposer/Fluent components over raw CSS, raw HTML controls, JavaScript, or third-party controls.

The UI must inherit Fluent/FrontComposer visual defaults. It must not redefine the application theme, introduce a custom EventStore palette, use captured hex colors from mockups/screenshots, use legacy Fluent v4/FAST tokens, or create decorative marketing-style layouts.

Color usage is limited to Fluent UI theme colors and Fluent component variants. A custom color is allowed only when a Fluent integration explicitly requires a seed/accent value or another documented token input, and the implementation story must justify that exception.

All Hexalith modules expose one host-level menu item. Child pages live inside the module dashboard as tabs, panels, or deep links.

For EventStore admin features, the single menu item is **Event Store Admin**. It opens a dashboard with tabbed child pages for Overview, Commands, Streams & Events, Projections, Tenants & Access, Topology, Storage & Snapshots, Recovery, Deferred & Backlog, and Settings.

Multi-section pages, dialogs, and detail panels use `FluentAccordion` with the primary section expanded by default.

## Required UI Semantics

| Semantics | UX rule |
|---|---|
| Accepted submission | HTTP 202, SignalR notifications, and command acceptance are not user-visible success. They render as accepted/evidence-pending. |
| Projection-confirmed success | Success appears only when read-model/projection evidence confirms the outcome through platform metadata. |
| Support-safe state | UI must never render bearer tokens, decoded JWT payloads, raw EventStore metadata, raw event payloads, protected payloads, cursors, ETags, stack traces, secrets, or unbounded SignalR metadata. |
| Deferred or unavailable operations | Backup, restore, import, compaction, GDPR erasure, OIDC login, aggregate test kit, and generator hardening must be hidden, disabled with an explicit unavailable reason, or backed by server `501`. |
| Tenant isolation | Denied filters, rows, and deep links fail closed and must not confirm whether hidden tenants or resources exist. |
| Generated REST boundary | Interactive UI hosts consume EventStore Client libraries and do not host generated or hand-written per-message MVC command/query controllers. |

## Surface Coverage

| Surface | Required behavior | Detailed coverage |
|---|---|---|
| EventStore Admin | One `Event Store Admin` menu item opens the dashboard. Legacy Admin.UI route sprawl becomes dashboard tabs. Admin operations are role-gated, audited, support-safe, and evidence-confirmed. | `EXPERIENCE.md` Information Architecture, State Patterns, Support-Safe Operations, Flows 1-4 |
| Sample Blazor UI | Demonstrates accepted command submission without claiming downstream completion. Projection changes are evidence, not HTTP acceptance. | `EXPERIENCE.md` Flow 5 |
| Tenants UI | Consumes client libraries, preserves projection-confirmed success, and hides or disables unsafe mutation when evidence is stale/unknown or access is denied. | `EXPERIENCE.md` Flow 6 |

## Source Traceability

| Source item | UX decision |
|---|---|
| PRD UI governance | FrontComposer and Blazor Fluent UI V5 are mandatory; no theme redefinition; no raw interactive controls where Fluent/FrontComposer exists. |
| PRD Sample UI accepted-submission behavior | Sample UI shows accepted/evidence-pending first and only shows confirmed state after projection/read-model evidence. |
| PRD Tenants UI projection-confirmed states | Tenants UI role/access changes show success only after projection evidence confirms the new visible state. |
| PRD Admin unavailable-operation behavior / NFR15 | Deferred operations are hidden, disabled with "Unavailable in this release.", or backed by `501`; no fake forms. |
| Architecture AD-4 / NFR14 | UI hosts use client libraries and do not host generated or hand-written per-message MVC command/query controllers. |
| Architecture AD-8 | SignalR is a freshness nudge only. Polling/query evidence confirms visible state. |
| Architecture AD-10 | Security fails closed; denied state is support-safe and does not disclose hidden resources. |
| Architecture AD-14 | Freshness, projection version, stale/current/unknown, paging, and ETag evidence cross the gateway as platform metadata. |

## Accessibility And Localization

The accessibility target is WCAG 2.2 AA for dashboard and admin workflows. Keyboard support is required for tabs, grids, dialogs, filters, command palette, and accordions.

Status must not rely on color alone. Badges, banners, lifecycle steps, and freshness indicators carry readable text and accessible names.

Live regions announce command lifecycle changes, stale-data transitions, SignalR connection changes, and completed evidence using the priority model in `EXPERIENCE.md`.

Localization evidence:

- All user-visible copy comes from resource-backed complete strings.
- UI code does not assemble runtime sentence fragments.
- Tests use stable selectors and state identifiers rather than translated strings as primary hooks.
- Identifier values may remain raw, but their labels must be localized.

## Visual References

Formal reference artifacts are stored with the detailed spines:

- [Fluent UI V5 desktop capture](ux-designs/ux-eventstore-2026-07-05/imports/fluent-ui-v5-home-desktop.png)
- [Fluent UI V5 mobile capture](ux-designs/ux-eventstore-2026-07-05/imports/fluent-ui-v5-home-mobile.png)
- [Dashboard overview mock](ux-designs/ux-eventstore-2026-07-05/mockups/dashboard-overview.html)
- [Command investigation mock](ux-designs/ux-eventstore-2026-07-05/mockups/command-investigation.html)

The screenshots and mockups are illustrative. `DESIGN.md` and `EXPERIENCE.md` are the implementation contracts.

## Validation

The validation pass was run on 2026-07-05 before this update. Reports are retained for audit:

- [validation-report.md](ux-designs/ux-eventstore-2026-07-05/validation-report.md)
- [validation-report.html](ux-designs/ux-eventstore-2026-07-05/validation-report.html)
