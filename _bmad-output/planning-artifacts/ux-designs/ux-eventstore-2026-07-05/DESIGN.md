---
name: Hexalith.EventStore Admin
description: Brownfield operations UX for administrators and platform operators. FrontComposer on Blazor Fluent UI V5, visually aligned to the Microsoft Fluent UI Blazor V5 documentation shell.
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
colors:
  app-bar-background: 'Fluent theme accentFill'
  app-bar-foreground: 'Fluent theme foregroundOnAccent'
  canvas: 'Fluent theme neutralLayer1'
  navigation-background: 'Fluent theme neutralLayer2'
  content-foreground: 'Fluent theme neutralForeground'
  secondary-foreground: 'Fluent theme neutralForegroundHint'
  border-subtle: 'Fluent theme neutralStroke'
  surface-subtle: 'Fluent theme neutralFill'
  callout-info-background: 'Fluent theme neutralFillLayer'
  status-success-background: 'FluentBadge Color=Success background'
  status-success-foreground: 'FluentBadge Color=Success foreground'
  status-warning-background: 'FluentBadge Color=Warning background'
  status-warning-foreground: 'FluentBadge Color=Warning foreground'
  status-danger-background: 'FluentBadge Color=Danger background'
  status-danger-foreground: 'FluentBadge Color=Danger foreground'
  status-neutral-background: 'FluentBadge Color=Neutral background'
  status-neutral-foreground: 'FluentBadge Color=Neutral foreground'
  focus-ring: 'Fluent theme focusStroke / accent focus treatment'
typography:
  body:
    fontFamily: 'Segoe UI, Segoe UI Web (West European), -apple-system, BlinkMacSystemFont, Roboto, Helvetica Neue, sans-serif'
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.45'
    letterSpacing: '0'
  page-title:
    fontFamily: 'Segoe UI, Segoe UI Web (West European), -apple-system, BlinkMacSystemFont, Roboto, Helvetica Neue, sans-serif'
    fontSize: 34px
    fontWeight: '400'
    lineHeight: '1.2'
    letterSpacing: '0'
  section-title:
    fontFamily: 'Segoe UI, Segoe UI Web (West European), -apple-system, BlinkMacSystemFont, Roboto, Helvetica Neue, sans-serif'
    fontSize: 18px
    fontWeight: '600'
    lineHeight: '1.3'
    letterSpacing: '0'
  metadata:
    fontFamily: 'Segoe UI, Segoe UI Web (West European), -apple-system, BlinkMacSystemFont, Roboto, Helvetica Neue, sans-serif'
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '1.35'
    letterSpacing: '0'
rounded:
  sm: 4px
  md: 6px
  lg: 8px
  full: 9999px
spacing:
  density-unit: 4px
  compact-gap: 8px
  standard-gap: 16px
  section-gap: 24px
  page-padding-x: 28px
  page-padding-y: 24px
components:
  module-entry:
    label: 'Event Store Admin'
    background-active: '{colors.canvas}'
    foreground: '{colors.content-foreground}'
    active-border: '{colors.app-bar-background}'
    min-height: 40px
  dashboard-shell:
    app-bar-background: '{colors.app-bar-background}'
    app-bar-foreground: '{colors.app-bar-foreground}'
    navigation-background: '{colors.navigation-background}'
    canvas: '{colors.canvas}'
    typography: '{typography.body}'
  dashboard-header:
    title: '{typography.page-title}'
    metadata: '{typography.metadata}'
    gap: '{spacing.compact-gap}'
  dashboard-tabs:
    component: 'FluentTabs'
    active-indicator: '{colors.app-bar-background}'
    border: '{colors.border-subtle}'
    min-height: 40px
  stat-summary:
    background: '{colors.canvas}'
    border: '{colors.border-subtle}'
    radius: '{rounded.md}'
    padding: '{spacing.standard-gap}'
  filter-bar:
    gap: '{spacing.compact-gap}'
    control-radius: '{rounded.sm}'
    border: '{colors.border-subtle}'
  evidence-grid:
    component: 'FluentDataGrid or FrontComposer grid primitive'
    header-background: '{colors.surface-subtle}'
    border: '{colors.border-subtle}'
    row-min-height: 36px
  status-badge:
    component: 'FluentBadge'
    success-background: '{colors.status-success-background}'
    success-foreground: '{colors.status-success-foreground}'
    warning-background: '{colors.status-warning-background}'
    warning-foreground: '{colors.status-warning-foreground}'
    danger-background: '{colors.status-danger-background}'
    danger-foreground: '{colors.status-danger-foreground}'
    neutral-background: '{colors.status-neutral-background}'
    neutral-foreground: '{colors.status-neutral-foreground}'
    radius: '{rounded.full}'
  issue-banner:
    component: 'FluentMessageBar'
    info-background: '{colors.callout-info-background}'
    warning-background: '{colors.status-warning-background}'
    danger-background: '{colors.status-danger-background}'
    border: '{colors.border-subtle}'
  operation-dialog:
    component: 'FluentDialog'
    radius: '{rounded.lg}'
    padding: '{spacing.section-gap}'
  detail-panel:
    component: 'FluentDrawer, FluentDialog, or FrontComposer panel primitive'
    radius: '{rounded.lg}'
    border: '{colors.border-subtle}'
  multi-section-panel:
    component: 'FluentAccordion'
    first-item: 'expanded'
    border: '{colors.border-subtle}'
  command-lifecycle-tracker:
    step-gap: '{spacing.compact-gap}'
    current-state: '{colors.status-warning-background}'
    terminal-success: '{colors.status-success-background}'
    terminal-failure: '{colors.status-danger-background}'
  projection-freshness-indicator:
    current: '{colors.status-success-background}'
    stale: '{colors.status-warning-background}'
    unknown: '{colors.status-neutral-background}'
  deferred-operation-placeholder:
    background: '{colors.status-neutral-background}'
    foreground: '{colors.status-neutral-foreground}'
    border: '{colors.border-subtle}'
  command-palette:
    component: 'FluentDialog plus FluentTextInput and list primitives'
    radius: '{rounded.lg}'
    active-result-background: '{colors.callout-info-background}'
---

## Brand & Style

Hexalith.EventStore Admin is an operations surface, not a marketing product and not a custom design system. It should feel like the Fluent UI Blazor V5 documentation site adapted for production operations: a stable blue app bar, compact left-side module navigation supplied by the host shell, a plain white content canvas, dense tables, restrained banners, and system typography.

The product expression is operational honesty. Surfaces must make state evidence clear: accepted, processing, projection-confirmed, stale, unavailable, deferred, denied, and failed are different states. The interface must not soften those distinctions with decorative success language.

The future EventStore UI service is the target. Legacy `Admin.UI` is source evidence, not the final IA. All EventStore admin features move under one Hexalith module menu item: **Event Store Admin**. That menu item opens a dashboard with tabbed child pages.

Visual references: [Fluent UI V5 desktop capture](imports/fluent-ui-v5-home-desktop.png), [Fluent UI V5 mobile capture](imports/fluent-ui-v5-home-mobile.png), [dashboard overview mock](mockups/dashboard-overview.html), and [command investigation mock](mockups/command-investigation.html). These are reference artifacts only; `DESIGN.md` and `EXPERIENCE.md` win on conflict.

## Colors

Colors inherit from FrontComposer and Blazor Fluent UI V5. The UX contract uses Fluent theme roles and Fluent component variants only; it does not define EventStore-specific hex values. A fixed custom color is allowed only when an upstream Fluent integration explicitly requires one, and that exception must be documented in the implementation story with the Fluent token/API it feeds.

- **App bar background** uses the current Fluent theme accent fill. Do not hard-code the captured documentation-site blue.
- **Canvas and navigation surfaces** use Fluent neutral layers. The shell may choose the appropriate neutral layer for light, dark, high-contrast, and system theme modes.
- **Foreground, secondary text, focus, and borders** use Fluent foreground, hint, focus, and stroke recipes/tokens.
- **Status colors** come from Fluent status components such as `FluentBadge`, `FluentMessageBar`, validation components, and lifecycle indicators. The UX names Success, Warning, Danger, and Neutral states; it does not specify their color values.
- **Callout color** is reserved for Fluent informational/warning surfaces through their theme variants, not a local background color.

Contrast target: all text and state labels meet WCAG 2.2 AA. Status may never be conveyed by color alone; every badge, banner, and lifecycle step carries readable text.

Avoid gradients, custom brand palettes, captured hex colors, decorative color bands, and color-only state. Do not redefine Fluent theme tokens and do not use legacy Fluent v4/FAST tokens.

## Typography

Use the Fluent UI Blazor V5 documentation site's system stack: Segoe UI first, then system fallbacks. Express type through `FluentText`, FrontComposer page primitives, or Fluent component parameters.

Page titles are work-surface labels, not hero copy. Use direct nouns such as "Event Store Admin", "Commands", "Streams", and "Security". Section titles stay compact. Metadata such as tenant id, domain, aggregate id, message id, and timestamp uses Fluent text roles plus monospace only where identifier scanning requires it.

Do not define a new heading ramp in CSS. Do not use negative letter spacing.

## Layout & Spacing

The shell follows the Fluent UI Blazor V5 documentation layout:

- top app bar with product/module identity and small utility actions;
- one Hexalith module menu entry for **Event Store Admin**;
- the EventStore dashboard owns its child tabs;
- full-width content area on a white canvas;
- dense, responsive operational tables and stats.

The dashboard tab row replaces the legacy Admin.UI left-nav route sprawl. Tabs group features by operator job rather than by implementation project. Use `FluentTabs` for tab navigation, `FluentDataGrid` or FrontComposer grid primitives for evidence tables, `FluentStack`/`FluentGrid` for layout, and `FluentAccordion` for multi-section panels.

Spacing uses the 4px density unit. Use `{spacing.compact-gap}` for filters and toolbar controls, `{spacing.standard-gap}` for stat groups and repeated summary items, and `{spacing.section-gap}` between distinct dashboard regions. Do not add nested cards or decorative floating sections.

## Elevation & Depth

Depth is minimal. The shell, tabs, grids, accordions, dialogs, and message bars provide enough structure. Avoid custom shadows. Use Fluent cards only for repeated stat/evidence summary items, modals, and truly framed tools. Do not put page sections inside floating cards.

## Shapes

Use Fluent component radii. Cards and panels stay at `{rounded.lg}` or less. Badges and avatars may inherit `{rounded.full}`. Do not introduce pill-shaped custom controls where a Fluent icon button, badge, tab, or segmented control fits.

## Components

The canonical component names below must match `EXPERIENCE.md.Component Patterns`.

- **Module entry** - One top-level Hexalith navigation item: "Event Store Admin". It opens the dashboard. No separate top-level menu entries for Commands, Streams, DAPR, Storage, Tenants, etc. Active state uses `{components.module-entry.active-border}`.
- **Dashboard shell** - FrontComposer shell using `{components.dashboard-shell.app-bar-background}`, `{components.dashboard-shell.navigation-background}`, and `{components.dashboard-shell.canvas}`.
- **Dashboard header** - FrontComposer/Fluent page header with title, environment/tenant context, connection status, and bounded utility actions.
- **Dashboard tabs** - `FluentTabs` under the dashboard header. Tabs are the primary child navigation for EventStore admin features.
- **Stat summary** - Repeated stat cards or FrontComposer metric primitives. Each stat must disclose stale/unknown state in its label or associated badge.
- **Filter bar** - `FluentTextInput`, `FluentSelect`, `FluentCheckbox`, segmented `FluentButton` groups, and date/time pickers as needed. Search and filters sit above the grid they affect.
- **Evidence grid** - `FluentDataGrid` for commands, events, streams, tenants, projections, DAPR resources, storage, and audit evidence. Keep rows dense and scannable.
- **Status badge** - `FluentBadge` with accessible text. Never rely on color alone.
- **Issue banner** - `FluentMessageBar` or FrontComposer equivalent for degraded, stale, denied, unsupported, and unavailable states. Banners must name the operational consequence.
- **Operation dialog** - `FluentDialog` for destructive or state-mutating admin actions. Dialogs include exact target identity, expected evidence, permission context, and cancellation.
- **Detail panel** - `FluentDrawer`, `FluentDialog`, or FrontComposer panel primitive for drill-in evidence. Use `FluentAccordion` if it has multiple titled sections.
- **Multi-section panel** - `FluentAccordion` when a page, dialog, or detail panel has two or more titled sibling sections. Primary section expands by default.
- **Command lifecycle tracker** - Compact ordered state tracker for command progress and terminal states. It uses status colors only with text labels.
- **Projection freshness indicator** - Badge or compact status row for current, stale, and unknown evidence.
- **Deferred operation placeholder** - Disabled or read-only state for intentionally unavailable work. It must not look like a runnable form.
- **Command palette** - Optional power-user accelerator inside the EventStore dashboard. It must not replace visible tabs and controls.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Inherit FrontComposer and Fluent UI V5 visual defaults | Redefine theme tokens or create a custom EventStore palette |
| Use one module menu item and dashboard tabs | Add one host menu item per admin feature |
| Show stale, unknown, deferred, denied, and failed states explicitly | Collapse them into generic error/success copy |
| Use FluentDataGrid for evidence-heavy tables | Hand-roll raw tables or raw interactive controls |
| Use FluentAccordion for multi-section detail panels | Hide a page's only primary grid behind an accordion |
| Keep payloads, tokens, cursors, ETags, stack traces, and raw metadata out of UI | Paste raw operational internals into support panels |
| Match the Fluent UI Blazor V5 documentation shell density | Build a marketing-style dashboard with oversized cards |
