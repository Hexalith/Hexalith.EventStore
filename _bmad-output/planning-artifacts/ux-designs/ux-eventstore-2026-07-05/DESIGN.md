---
name: Hexalith.EventStore Admin
description: Brownfield operations UX for administrators and platform operators, inheriting FrontComposer and Blazor Fluent UI V5.
status: draft
created: 2026-07-05
updated: 2026-09-09
reviewed_repository_revision: e302432ca6daf3aa0436c3c0011f7baa551bb449
sources:
  - docs/brownfield/architecture.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md
  - https://fluentui-blazor-v5.azurewebsites.net/
colors: {}
typography: {}
rounded: {}
spacing:
  density-unit: 4px
  compact-gap: 8px
  standard-gap: 16px
  section-gap: 24px
components:
  dashboard-shell:
    implementation: FrontComposerShell
    theme: 'FrontComposer and Fluent UI V5 inherited'
  module-navigation:
    implementation: FrontComposerNavigation
    module-id: event-store-admin
  page-layout:
    implementation: FcPageLayout
    region-gap: '{spacing.section-gap}'
  dashboard-header:
    implementation: FcPageHeader
    content-gap: '{spacing.compact-gap}'
  dashboard-tabs:
    implementation: FcPageTabs
    fluent-primitive: FluentTabs
  stat-summary:
    implementation: 'FluentCard with FluentText roles'
    item-gap: '{spacing.standard-gap}'
  filter-bar:
    implementation: 'FluentStack with FluentTextInput, FluentSelect, and FluentCheckbox'
    status-filter: FcStatusFilterChips
    reset-control: FcFilterResetButton
    filter-summary: FcFilterSummary
    control-gap: '{spacing.compact-gap}'
  evidence-grid:
    implementation: FluentDataGrid
    responsive-helper: FcColumnPrioritizer
  status-badge:
    implementation: FcStatusBadge
    fluent-primitive: FluentBadge
  issue-banner:
    implementation: FluentMessageBar
  operation-dialog:
    implementation: FluentDialog
    destructive-variant: FcDestructiveConfirmationDialog
    authorization-region: FcAuthorizedCommandRegion
    abandonment-guard: FcFormAbandonmentGuard
  detail-panel:
    implementation: FluentDrawer
  multi-section-panel:
    implementation: FluentAccordion
  command-lifecycle-tracker:
    implementation: 'EventStore-owned composition of FcStatusBadge and FluentText'
    pending-summary: FcPendingCommandSummary
  projection-freshness-indicator:
    implementation: FcProjectionConnectionStatus
    status-component: FcStatusBadge
  loading-skeleton:
    implementation: FcProjectionLoadingSkeleton
  empty-state:
    implementation: FcProjectionEmptyPlaceholder
  deferred-operation-placeholder:
    implementation: 'FluentMessageBar with disabled Fluent controls omitted'
  command-palette:
    implementation: FcCommandPalette
  refresh-controls:
    implementation: 'FluentButton and FluentSelect in FluentStack'
  live-status-regions:
    implementation: 'EventStore-owned semantic status regions with FluentText'
  protected-outcome:
    implementation: 'FcStatusBadge with bounded FluentText explanation'
---

## Brand & Style

Hexalith.EventStore Admin is an operations surface, not a marketing product or a local design system. It inherits the FrontComposer shell and Blazor Fluent UI V5 and keeps the established visual direction: compact host navigation, a neutral work canvas, dense evidence tables, restrained status surfaces, and system typography.

The product expression is operational honesty. Accepted, evidence-pending, projection-confirmed, stale, unavailable, deferred, denied, and failed are distinct states. Visual polish must never soften those distinctions or imply that a backlog capability is implemented.

The brownfield target remains `src/Hexalith.EventStore.Admin.UI`. It retains `eventstore-admin-ui` as its service, resource, DAPR, and container identity and registers one FrontComposer module, `event-store-admin`, labelled **Event Store Admin**. No second host, router, or page implementation is introduced.

The reviewed repository revision is `e302432ca6daf3aa0436c3c0011f7baa551bb449`. The source digests and authority order are recorded in `EXPERIENCE.md`. `status: final`, when restored after this update, means that the UX contract is finalized; it never authorizes implementation, release, deployment, migration, or a readiness verdict.

## Colors

All colors inherit from FrontComposer and Blazor Fluent UI V5. The empty `colors` map is deliberate: EventStore defines no brand or status palette and does not restate inherited theme values as local tokens.

- Use Fluent component appearances and current Fluent 2 roles for accent, neutral surfaces, foregrounds, borders, focus, and Success/Warning/Danger/Neutral treatments.
- Do not hard-code colors captured from reference screenshots.
- Do not use gradients, decorative color bands, custom status palettes, legacy Fluent v4/FAST tokens, or redefined theme primitives.
- Text contrast meets WCAG 2.2 AA. Focus indicators, control boundaries, selected states, and lifecycle graphics meet the 3:1 non-text contrast floor against adjacent colors.
- In forced-colors mode, preserve visible focus, boundaries, selection, and state text through system colors and component defaults. Never suppress forced-color adjustment for decoration.
- Color never carries state alone; every state has readable text and a programmatic value.

## Typography

Typography inherits FrontComposer and Fluent UI V5. The empty `typography` map is deliberate: EventStore does not reproduce the Fluent ramp in local CSS.

- `FcPageHeader` owns page and selected-tab titles; titles are direct work-surface nouns and expose one focusable heading.
- `FluentText` roles own body, label, status, and metadata hierarchy. Segoe UI and system fallbacks come from the inherited system.
- Identifiers may use an inherited monospace role only when it materially improves scanning.
- Do not create a local heading ramp, hard-code theme typography, use negative letter spacing, or style marketing hero copy.

## Layout & Spacing

`FrontComposerShell`, `FrontComposerNavigation`, `FcPageLayout`, `FcPageHeader`, and `FcPageTabs` own the shell geometry. EventStore adds no parallel navigation system.

The 4px density contract remains a product requirement. Express its 8px control gaps, 16px repeated-summary gaps, and 24px region gaps through Fluent/FrontComposer component parameters or current tokens. Use custom CSS only for layout that the component system does not own.

Evidence surfaces are dense but reflowable. Keep identity, scope, state, and safe recovery visible before secondary metadata. Two-dimensional overflow is permitted only inside a labelled data-grid region. Do not add nested cards, decorative floating sections, or a second dashboard grid.

Interactive targets have a 24 by 24 CSS pixel minimum unless a documented WCAG exception applies; prefer 44 by 44 CSS pixels for touch-critical actions without diluting desktop density.

## Elevation & Depth

Depth inherits from Fluent components. Use shell layers, dividers, dialogs, drawers, message bars, and the occasional repeated stat card to establish hierarchy. Add no custom shadow language and do not place ordinary page sections in floating cards.

## Shapes

Shapes inherit from Fluent components. Do not duplicate component radii in local CSS. Full rounding is reserved for inherited badges or circular icon controls; it is not a substitute for tabs, buttons, or panels.

## Components

The following names are canonical and match `EXPERIENCE.md` exactly.

- **Dashboard shell** — `FrontComposerShell`; inherits theme and landmark geometry. It contains the single module navigation and dashboard content regions.
- **Module navigation** — `FrontComposerNavigation`; renders exactly one `event-store-admin` entry labelled **Event Store Admin** and keeps it selected for all child routes.
- **Page layout** — `FcPageLayout`; owns the dashboard's content width, landmarks, and region spacing.
- **Dashboard header** — `FcPageHeader`; renders the focusable title, authorized environment/tenant context, connection freshness, and bounded utilities.
- **Dashboard tabs** — `FcPageTabs` over `FluentTabs`; uses inherited selected, focus, disabled, and overflow appearances.
- **Stat summary** — `FluentCard` with `FluentText`; used only for repeated metrics, with visible evidence state and observation time.
- **Filter bar** — `FluentStack` with `FluentTextInput`, `FluentSelect`, `FluentCheckbox`, `FcStatusFilterChips`, `FcFilterResetButton`, and `FcFilterSummary`; visually groups controls directly above their evidence grid.
- **Evidence grid** — `FluentDataGrid` with `FcColumnPrioritizer`; dense rows, visible header hierarchy, labelled overflow, and a single row-action location.
- **Status badge** — `FcStatusBadge` over `FluentBadge`; inherited status appearance plus readable canonical state text.
- **Issue banner** — `FluentMessageBar`; intent is selected from the operational consequence, never borrowed from badge colors.
- **Operation dialog** — `FluentDialog` with `FcAuthorizedCommandRegion` and `FcFormAbandonmentGuard`; destructive and recovery cases use `FcDestructiveConfirmationDialog`. It visually separates frozen scope, effect, risk, reversibility, and confirmation controls.
- **Detail panel** — `FluentDrawer`; keeps the selected evidence context visible where viewport permits.
- **Multi-section panel** — `FluentAccordion`; one item per titled sibling section, with primary evidence expanded by default.
- **Command lifecycle tracker** — EventStore composition of `FcPendingCommandSummary`, `FcStatusBadge`, and `FluentText`; ordered states remain text-first and do not resemble decorative progress.
- **Projection freshness indicator** — `FcProjectionConnectionStatus` with `FcStatusBadge`; renders provenance, lifecycle, observation, and freshness without inferring authority from color.
- **Loading skeleton** — `FcProjectionLoadingSkeleton`; matches the eventual summary/grid layout and has no shimmer when reduced motion is requested.
- **Empty state** — `FcProjectionEmptyPlaceholder`; names only the authorized visible scope and distinguishes empty from unavailable or denied.
- **Deferred operation placeholder** — `FluentMessageBar`; contains read-only tracking context and the exact text “Unavailable in this release.” It has no form-like styling.
- **Command palette** — `FcCommandPalette`; inherits Fluent dialog, input, and result-list appearances and never replaces visible navigation.
- **Refresh controls** — `FluentButton` and `FluentSelect` in `FluentStack`; a compact group for manual refresh, pause/resume, and approved cadence choices.
- **Live status regions** — semantic EventStore status containers with `FluentText`; visually persistent terminal outcomes, with no toast-only authority.
- **Protected outcome** — `FcStatusBadge` plus bounded `FluentText`; distinguishes typed unreadable outcomes without displaying protected bytes.

The promoted [dashboard overview mock](mockups/dashboard-overview.html) and its [rendered PNG](mockups/dashboard-overview.png) illustrate dashboard density. The [command investigation mock](mockups/command-investigation.html) and its [rendered PNG](mockups/command-investigation.png) illustrate command-evidence hierarchy. The [desktop](imports/fluent-ui-v5-home-desktop.png) and [mobile](imports/fluent-ui-v5-home-mobile.png) captures illustrate inherited shell density and responsive navigation. These artifacts are illustrative and non-copyable until regenerated against current bindings; the two spines win on conflict.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Inherit FrontComposer and Fluent UI V5 visual defaults | Recreate inherited colors, typography, radii, or component styling |
| Use one `event-store-admin` module entry and `FcPageTabs` | Add host-level entries for individual EventStore features |
| Show evidence source, time, freshness, and state in text | Treat HTTP `202`, SignalR, a toast, or elapsed time as success |
| Use the named FrontComposer component when it exists | Offer a generic list of interchangeable component choices |
| Keep disabled reasons visible and programmatically associated | Put essential safety reasons behind inaccessible-only tooltips |
| Preserve focus and state in forced colors and reduced motion | Use animation or color as the only state cue |
| Keep protected and idempotency material outside every rendered channel | Place raw operational internals in DOM, URLs, clipboard, export, logs, or telemetry |
| Show deferred capabilities as hidden or read-only | Render fake forms, jobs, progress, or success for backlog work |
