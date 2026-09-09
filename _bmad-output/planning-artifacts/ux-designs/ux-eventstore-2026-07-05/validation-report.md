# Validation Report — eventstore

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- **Run at:** 2026-09-09T09:49:54+02:00
- **Spines validated:** `status: final`, updated 2026-08-01
- **Selected lenses:** rubric walker; accessibility & support-safety
- **Additional evidence included:** pre-existing architecture & readiness review dated 2026-09-08, as required by the synthesis rule to read every `review-*.md`

## Synthesis

The pair is a usable but not yet clean downstream contract. Its canonical shape, 16-component taxonomy, principal operator flows, state vocabulary, accessibility floor, and semantic FrontComposer/Fluent UI V5 inheritance are largely coherent. The main contract risks are source drift after the 2026-08-01 finalization, a circular and partly historical source graph, the unreconciled AD-25 rule for idempotency keys, incomplete journey/state closure, and visual references that contain prohibited legacy Fluent variables.

The accessibility lens materially raises the risk: the spines are not yet safe as a WCAG 2.2 AA contract for regulated/support-critical use. The critical blocker is cross-scope destructive confirmation that does not freeze and revalidate tenant, environment, acting principal, target, blast radius, and reversibility. Architecture evidence adds three implementation blockers: the live `/types` route is missing from the tab map, Admin transport does not yet expose the evidence required by the projection-freshness indicator, and FrontComposer host composition is asserted but not specified.

No overall grade is assigned. Reviewer-reported raw counts are:

| Reviewer | Critical | High | Medium | Low |
|---|---:|---:|---:|---:|
| Rubric walker | 0 | 3 | 10 | 6 |
| Accessibility & support-safety | 1 | 9 | 6 | 1 |
| Architecture & readiness (existing evidence) | 0 | 4 | 12 | 10 |

## Rubric category verdicts

| Category | Verdict | Summary |
|---|---|---|
| Flow coverage | adequate | Six required flows are structurally complete, but several IA surfaces lack journeys. |
| Token completeness | adequate | All references resolve under Fluent inheritance; several bindings remain ambiguous or policy-incompatible. |
| Component coverage | adequate | All 16 canonical names align; several primitive choices remain uncommitted. |
| State coverage | adequate | EventStore tabs are covered; Sample, Tenants, route failures, and newer protection outcomes are incomplete. |
| Visual reference coverage | thin | Promoted mocks contain prohibited tokens, two rendered PNGs are orphaned, and references lack local context. |
| Bloat & overspecification | adequate | Mostly decision-dense, with repeated single-host and deferred-operation rules. |
| Inheritance discipline | thin | Names and references align, but source authority and freshness are unsafe. |
| Shape fit | strong | Canonical sections and triggered responsive/inspiration sections are present and ordered correctly. |

## Findings by severity

Findings are grouped by severity and retain their source lens. Repeated findings are intentionally retained where a second reviewer adds materially different evidence.

### Critical

- **[Accessibility] Cross-scope destructive confirmation cannot reliably prevent a wrong-tenant operation** (`EXPERIENCE.md:94-101,154,203-205`; both mocks expose “All tenants”). Freeze and lead with acting principal/role, environment, tenant, domain, target, authoritative state, effect, reversibility, blast radius, and audit reason; revalidate on submit, surface conflicts without submitting, require explicit acknowledgement for irreversible/cross-scope actions, and prohibit multi-tenant bulk mutation.

### High

- **[Rubric / Token completeness] Local pixel typography ramp conflicts with Fluent UI V5 governance** (`DESIGN.md:34-57,184-188`). Bind roles to exact `FluentText` parameters or Fluent 2 typography tokens.
- **[Rubric / Inheritance] The source graph is circular and includes a contradicted historical readiness report** (`DESIGN.md:6-11`; `EXPERIENCE.md:6-11`). Establish a one-way authority order and replace or label the July readiness report as historical.
- **[Rubric / Inheritance] Post-final source changes are unreconciled, including AD-25's ban on exposing idempotency keys** (`architecture.md:406-415`; `EXPERIENCE.md:199-205`). Reconcile FR37/NFR19/AD-23–25 and add idempotency material to the no-render/no-log contract.
- **[Accessibility] Component semantics are not implementable from “accessible names and roles” alone** (`EXPERIENCE.md:95,98-105,161-168`). Add exact tab, accordion, grid, dialog, badge, toast, keyboard, ownership, relationship, and state semantics.
- **[Accessibility] Live-region scope and deduplication are unspecified** (`EXPERIENCE.md:151,172-180`). Define one view status region plus one operation region; suppress initial/unchanged polling announcements and throttle/coalesce refresh summaries.
- **[Accessibility] Automatic polling lacks pause, cadence, and reading-state preservation** (`EXPERIENCE.md:113-116,151`). Add pause/resume, a bounded interval, and preservation of focus, scroll, filters, selection, expansion, and open dialogs.
- **[Accessibility] “Retry” after evidence timeout is ambiguous and can resubmit a late-successful mutation** (`EXPERIENCE.md:117-118,153,274-284`). Separate “Refresh status” from “Retry operation,” fence resubmission on authoritative terminal evidence, and preserve stable operation identity.
- **[Accessibility] Sample and Tenants state coverage is not closed and critical states are conflated** (`EXPERIENCE.md:31-35,131-145`). Add per-surface cold, refresh, empty, stale, offline, unavailable, expired, denied, unknown/malformed, conflict, pending, and terminal states with focus, announcement, mutation, disclosure, and recovery rules.
- **[Accessibility] Protected data is not specified as an assistive-technology-safe redaction outcome** (`EXPERIENCE.md:123,199-203`). Keep sensitive bytes out of the DOM, accessibility properties, tooltips, clipboard, URLs, exports, and telemetry; map typed unreadable outcomes to bounded localized copy and safe reason codes.
- **[Accessibility] Responsive rules do not define 320 CSS-pixel reflow, 400% zoom, text spacing, or focus containment** (`EXPERIENCE.md:189-197`). Bind acceptance to those conditions and add narrow product evidence for critical views and mutations.
- **[Accessibility] Product mocks model keyboard-inoperable primary controls** (`mockups/dashboard-overview.html:275-339`; `mockups/command-investigation.html:246-286`). Make controls semantic/keyboard-operable or annotate exact Fluent equivalents and behavior.
- **[Accessibility] Toasts are ungoverned despite false-success risk** (`EXPERIENCE.md:90-107,172-180`). Mutation evidence must persist outside toasts; accepted is neutral/pending; toasts must be dismissible, non-blocking, focus-safe, and deduplicated.
- **[Architecture] The tab map drops the live `/types` route** (`EXPERIENCE.md:39-54`; `TypeCatalog.razor:1`). Assign the route to a tab/subsurface and propagate it to UX-DR4 and Story 7.14.
- **[Architecture] Promoted HTML mocks use prohibited Fluent v4/FAST variables** (both mock files, lines 16-27). Replace them with exact Fluent 2 `--color*` roles or mark the CSS non-implementable.
- **[Architecture] Admin transport has no source for the mandated projection-freshness evidence** (`EXPERIENCE.md:105,118,126-129,138`; `DESIGN.md:140-147,229`). State which Admin DTO exposes provenance/lifecycle and render `Unknown` until that contract exists.
- **[Architecture] FrontComposer composition is asserted but absent and unspecified** (`EXPERIENCE.md:93,300`; `DESIGN.md:217`). Define the `event-store-admin` bounded context, `DomainManifest`, localized `FrontComposerNavEntry`, package/catalog prerequisites, and route.

### Medium

- **[Rubric / Flow] Topology, Storage & Snapshots, and Settings lack Key Flows; Projections is only indirect** (`EXPERIENCE.md:41-54,225-296`). Add concise flows or narrow the IA closure rule.
- **[Rubric / Token completeness] Several color values are recipe-like legacy labels rather than exact Fluent V5 bindings** (`DESIGN.md:15-23,32`). Use exact Fluent 2 token names or component parameters/intents.
- **[Rubric / Token completeness] `issue-banner` uses badge color semantics although it is a `FluentMessageBar`** (`DESIGN.md:117-122`). Bind message-bar intent/variant directly.
- **[Rubric / Components] Evidence grid, Detail panel, and Command palette leave primitive choices open** (`DESIGN.md:101-103,127-130,152-155`). Commit one preferred primitive and one explicit fallback condition.
- **[Rubric / States] Sample and Tenants are absent from the per-surface state matrix** (`EXPERIENCE.md:31-35,111-144`). Add complete rows for both.
- **[Rubric / States] Shell/route failure and timeout/cancellation lack explicit state rows** (`EXPERIENCE.md:114,284`). Define support-safe copy, focus, selection, and recovery.
- **[Rubric / States] NFR19 typed payload outcomes are collapsed into generic protected/redacted state** (`prd.md:335`; `EXPERIENCE.md:123,201`). Map the EventStore-visible subset and name consumer-owned outcomes.
- **[Rubric / Visuals] Promoted HTML mocks use prohibited legacy variables while claiming Fluent emission** (both mock files, lines 8-27). Replace the variables or clearly mark the mocks non-implementable.
- **[Rubric / Visuals] References are grouped rather than contextual and both rendered mock PNGs are orphaned** (`DESIGN.md:166`; `EXPERIENCE.md:25`). Link every promoted artifact beside the relevant rule and explain what it illustrates.
- **[Rubric / Inheritance] Module identity `event-store-admin` is missing although resource identity `eventstore-admin-ui` is present** (`DESIGN.md:164,216`; `EXPERIENCE.md:21,91,300`). State both identifiers and their distinct roles.
- **[Accessibility] Focus behavior is ambiguous for tabs, deep links, panels, disappearing rows, and dynamic validation** (`EXPERIENCE.md:150,162-168`). Define stable entry, movement, and restoration targets.
- **[Accessibility] Contrast commitments omit required non-text contrast and forced-color/theme evidence** (`DESIGN.md:168-180`). Require 3:1 for load-bearing indicators and test light/dark/system/forced-color states.
- **[Accessibility] Interactive targets have no minimum size** (`DESIGN.md:71-105`). Require WCAG 2.5.8's 24×24 CSS-pixel target or exception, with 44×44 preferred for touch-critical controls.
- **[Accessibility] Reduced-motion rules omit shimmer, scrolling, transitions, and static progress equivalence** (`EXPERIENCE.md:113,169`). Disable nonessential motion while preserving progress and state text.
- **[Accessibility] Disabled-action reasons may be unreachable** (`EXPERIENCE.md:105,128-144,155,207-214`). Render persistent associated explanation and a reachable safe next action.
- **[Accessibility] Localization omits accessible-only copy, announcements, culture-aware values, pseudo-locales, RTL, and safe truncation** (`EXPERIENCE.md:182-187`). Add them to the resource/test contract.
- **[Architecture] “Topology” conflicts with the existing tenant/domain stream navigator name** (`NavMenu.razor:29-56`; `EXPERIENCE.md:46`). Rename or explicitly relocate one concept.
- **[Architecture] `/health` ownership is ambiguous between Topology and Recovery** (`EXPERIENCE.md:46,48`). Split component health from recovery health and name shared-component ownership.
- **[Architecture] DESIGN color roles use v4/FAST vocabulary** (`DESIGN.md:15-32`). Replace values with exact Fluent 2 roles or component parameters.
- **[Architecture] `FluentBadge Color=Neutral` is not a V5 value** (`DESIGN.md:30-31,114-115,143-147`). Choose a supported value such as `Subtle` or `Informative`.
- **[Architecture] `FluentDrawer` is not present in the checked V5 catalog** (`DESIGN.md:128,226`). Use `FluentDialog` drawer mode or a verified FrontComposer primitive.
- **[Architecture] `FluentTabs` is neither a router nor an overflowing tab solution** (`EXPERIENCE.md:95,150,194`). Define route binding and the allowed layout-only overflow treatment.
- **[Architecture] Command investigation does not distinguish `MessageId` lookup from `CorrelationId` tracing** (`EXPERIENCE.md:58-69,255`). Add AD-17 and bind lookup to the typed client.
- **[Architecture] Lifecycle color mappings conflict with shipped Tenants behavior** (`DESIGN.md:141-147`). Establish the platform mapping or document an exception.
- **[Architecture] Sample Flow 5 contradicts the shipped Sample and has no owning story** (`EXPERIENCE.md:34,274-284`). Mark the implementation gap or assign an owner.
- **[Architecture] Traceability lacks owning stories and cites the superseded July readiness report** (`DESIGN.md:12`; `EXPERIENCE.md:11,58-69`). Add owning stories and current readiness evidence.
- **[Architecture] A developer still must invent DTO provenance fields, tab URL binding, and `data-testid` naming** (`EXPERIENCE.md:95,150,170`). Commit those three contracts.
- **[Architecture] `.memlog.md` still records the superseded “future consolidated EventStore UI service” model** (`.memlog.md:11,15,25`). Append dated correction events before any UX update.

### Low

- **[Rubric / Flow] General bookmarked legacy-route arrival is absent from journeys** (`EXPERIENCE.md:52,272`). Add a deep-link arrival step with selected module/tab state.
- **[Rubric / Tokens] Non-text contrast is not stated for focus, borders, and lifecycle indicators** (`DESIGN.md:137-139,178`). Add a 3:1 floor.
- **[Rubric / Components] Skeleton and inline validation behaviors have no paired component home** (`EXPERIENCE.md:113,124,179`). Add rows or declare inherited defaults.
- **[Rubric / Visuals] The Overview mock omits Settings** (`dashboard-overview.html:282-292`). Add it or annotate the omission.
- **[Rubric / Bloat] Single-host and deferred-operation rules repeat across several locations**. Keep one authority and cross-reference it.
- **[Rubric / Inheritance] `unknown` casing drifts from canonical `Unknown`** (`EXPERIENCE.md:66`). Normalize it.
- **[Accessibility] `data-testid` appears under Accessibility Floor and can be mistaken for semantics** (`EXPERIENCE.md:170`). Move it to testing and pair selector assertions with role/name/state/focus assertions.
- **[Architecture] Visible host title, breadcrumb, and development role switcher are not reconciled with the target header**. State their target disposition.
- **[Architecture] The existing “optimized for wider screens” alert contradicts the narrow-screen contract**. Mark it for retirement.
- **[Architecture] Existing custom status tokens and one Razor legacy token are not tracked as an allowlisted migration backlog**. Add the inventory under DESIGN Colors.
- **[Architecture] The explicit pixel heading ramp contradicts the Fluent-only typography rule**. Bind typography to Fluent V5 parameters/tokens.
- **[Architecture] FR36 is cited as the primary provenance authority although FR4/AD-15/AD-19/AD-20 own the rule**. Correct the trace label.
- **[Architecture] Any spine edit invalidates the three SHA-256 pins in `epics.md:16-18`**. Re-pin them with the same change.
- **[Architecture verification] Canonical `ux.md` currently satisfies the PRD artifact contract**. No spine fix required.
- **[Architecture] Epic 7.20's reconciliation says no `.resx` resources exist, but `AdminResources.resx` now exists**. Correct the story evidence; no spine fix is required.
- **[Architecture] The prior index/report freshness warning is resolved by this regenerated report**, but `index.md` should continue to identify the current review date.
- **[Architecture verification] No post-2026-08-01 route/navigation implementation has overtaken the spines**. Current drift is source/contract ahead of implementation.

## Mechanical notes

- All six direct sources resolve; no direct source defines separately numbered user journeys.
- All 35 distinct `{path.to.token}` references resolve. Route placeholders `{tenant}`, `{domain}`, and `{aggregate}` are not design-token references.
- All 16 canonical component names match across DESIGN frontmatter/prose and EXPERIENCE Component Patterns.
- DESIGN sections follow the locked canonical order; EXPERIENCE contains every required default plus correctly triggered Responsive & Platform and Inspiration & Anti-patterns.
- Visual inventory: two imports, two HTML mocks, two rendered mock PNGs, no wireframes. The PNG twins are not linked inline.
- No Mermaid blocks occur in either spine.
- The spines were not modified by validation.

## Reviewer files

- `review-rubric.md`
- `review-accessibility-support-safety.md`
- `review-architecture-readiness.md` (existing 2026-09-08 evidence)
