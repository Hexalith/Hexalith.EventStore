---
name: Hexalith.EventStore Admin
description: Brownfield operations UX for administrators and platform operators, inheriting FrontComposer and Blazor Fluent UI V5.
status: final
created: 2026-07-05
updated: 2026-09-09
reviewed_repository_revision: 23a722a1ffe29099a9d87df266552be4e3addd82
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
    implementation: 'EventStore-owned labelled aside with FluentCard'
  multi-section-panel:
    implementation: FluentAccordion
  command-lifecycle-tracker:
    implementation: 'EventStore-owned composition of FcStatusBadge and FluentText'
    pending-summary: FcPendingCommandSummary
  projection-freshness-indicator:
    implementation: 'EventStore-owned composition of FluentBadge and FluentText'
    lifecycle-mapping: 'Tenants ProjectionLifecycleBadge contract'
  projection-connection-status:
    implementation: FcProjectionConnectionStatus
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

## Contract Scope

The brownfield target remains `src/Hexalith.EventStore.Admin.UI`. It retains `eventstore-admin-ui` as its service, resource, DAPR, and container identity and registers one FrontComposer module, `event-store-admin`, labelled **Event Store Admin**. No second host, router, or page implementation is introduced.

The reviewed repository revision is `23a722a1ffe29099a9d87df266552be4e3addd82`. `EXPERIENCE.md` records the input-snapshot digests and authority order. `status: final` means only that the UX contract is finalized. It does not authorize implementation, release, deployment, migration, or a readiness verdict.

Story 7.20 must inventory and retire the current local `--hexalith-status-*` and `--hexalith-brand` definitions in `wwwroot/css/app.css` plus the legacy `--neutral-stroke-rest` and `--neutral-layer-2` usage in `ProtectedContentPanel.razor`. Until then, those declarations are allow-listed brownfield migration debt, not reusable design tokens.

## Brand & Style

Hexalith.EventStore Admin is an operations surface, not a marketing product or a local design system. It inherits the FrontComposer shell and Blazor Fluent UI V5 and keeps the established visual direction: compact host navigation, a neutral work canvas, dense evidence tables, restrained status surfaces, and system typography.

The design must communicate operational state honestly. Accepted, evidence-pending, projection-confirmed, stale, unavailable, deferred, denied, and failed are distinct states. Visual polish must never soften those distinctions or imply that a backlog capability is implemented.

## Colors

All colors inherit from FrontComposer and Blazor Fluent UI V5. The empty `colors` map is deliberate: EventStore defines no brand or status palette and does not restate inherited theme values as local tokens.

- Use Fluent component appearances and current Fluent 2 roles for accent, neutral surfaces, foregrounds, borders, and focus. `FcStatusBadge` receives a FrontComposer `BadgeSlot`; do not invent a nonexistent `BadgeColor.Neutral` value.
- Projection lifecycle colors follow the shipped Tenants contract: `Current` → `Success`; `Stale` and `Unavailable` → `Severe`; `Rebuilding` → `Informative`; `Degraded` → `Warning`; `LocalOnly` and `Unknown` → `Important`. Text and icon remain the authoritative cues.
- Do not hard-code colors captured from reference screenshots.
- Do not use gradients, decorative color bands, custom status palettes, legacy Fluent v4/FAST tokens, or redefined theme primitives.
- Text contrast meets WCAG 2.2 AA. Focus indicators, control boundaries, selected states, and lifecycle graphics meet the 3:1 non-text contrast floor against adjacent colors.
- In forced-colors mode, preserve visible focus, boundaries, selection, and state text through system colors and component defaults. Never suppress forced-color adjustment for decoration.
- Color never carries state alone; every state has readable text and a programmatic value.

## Typography

Typography inherits FrontComposer and Fluent UI V5. The empty `typography` map is deliberate: EventStore does not reproduce the Fluent ramp in local CSS.

- `FcPageHeader` renders page and selected-tab titles. Use direct work-surface nouns for those titles, and expose exactly one focusable heading.
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

Frontmatter owns implementation bindings; this table owns visual role and unique constraints. Names match `EXPERIENCE.md` exactly.

| Component | Visual role and constraint |
|---|---|
| **Dashboard shell** | One inherited host frame; never a second EventStore shell. |
| **Module navigation** | One selected **Event Store Admin** entry for every child route. |
| **Page layout** | One work canvas with inherited width and spacing. |
| **Dashboard header** | One clear title followed by authorized scope and bounded utilities. |
| **Dashboard tabs** | Inherited selected, focus, and disabled appearances; horizontal overflow is a layout-only exception. |
| **Stat summary** | Repeated compact metrics with visible evidence state and observation time. |
| **Filter bar** | One compact control group directly above its evidence grid. |
| **Evidence grid** | Dense rows, strong header hierarchy, labelled overflow, and one action location. |
| **Status badge** | Inherited semantic slot with readable canonical state text. |
| **Issue banner** | Consequence-led intent; never borrows a lifecycle badge color. |
| **Operation dialog** | Visually separates frozen scope, effect, risk, reversibility, and confirmation. |
| **Detail panel** | A labelled evidence aside that keeps source context visible where space permits. |
| **Multi-section panel** | Titled sibling sections; primary evidence expanded by default. |
| **Command lifecycle tracker** | Text-first ordered states, never decorative progress. |
| **Projection freshness indicator** | Provenance, lifecycle, observation, and freshness; Tenants-consistent text/icon/color mapping. |
| **Projection connection status** | A bounded connection/reconciliation message bar; never lifecycle evidence. |
| **Loading skeleton** | Matches the eventual summary/grid layout; no reduced-motion shimmer. |
| **Empty state** | Names authorized scope and remains distinct from unavailable or denied. |
| **Deferred operation placeholder** | Read-only unavailable treatment with no form-like affordance. |
| **Command palette** | Inherited dialog/input/results styling; never replaces visible navigation. |
| **Refresh controls** | Compact manual refresh, pause/resume, and approved-cadence group. |
| **Live status regions** | Persistent terminal outcomes; never toast-only authority. |
| **Protected outcome** | Bounded typed explanation without protected bytes. |

The promoted [dashboard overview mock](mockups/dashboard-overview.html), [desktop render](mockups/dashboard-overview.png), and [mobile render](mockups/dashboard-overview-mobile.png) illustrate dashboard density and contained grid overflow. The [command investigation mock](mockups/command-investigation.html), [desktop render](mockups/command-investigation.png), and [mobile render](mockups/command-investigation-mobile.png) illustrate command-evidence hierarchy and the detail panel's narrow-screen move. The upstream Fluent [desktop](imports/fluent-ui-v5-home-desktop.png) and [mobile](imports/fluent-ui-v5-home-mobile.png) captures illustrate inherited shell density and responsive navigation. These artifacts are illustrative, current composition references and remain non-copyable; the two spines win on conflict.

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
